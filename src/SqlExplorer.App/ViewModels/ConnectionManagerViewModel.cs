using System.Collections.ObjectModel;
using SqlExplorer.Core.Connections;
using SqlExplorer.Core.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SqlExplorer.App.ViewModels;

/// <summary>Which detail panel the right side of the Connection Manager shows.</summary>
public enum ManagerPane
{
    Empty,
    Connection,
    Folder
}

/// <summary>
/// Backs the Connection Manager window (master-detail). The left tree groups saved connections into
/// nested folders derived from each connection's <c>/</c>-joined <see cref="SavedConnection.Folder"/>
/// path; the right side edits the selected connection (reusing <see cref="ConnectionDialogViewModel"/>),
/// renames/deletes the selected folder, or shows an empty state. Folders exist only as path prefixes,
/// so a freshly created, still-empty folder is kept in-memory (<see cref="_emptyFolders"/>) until a
/// connection lands in it.
/// </summary>
public partial class ConnectionManagerViewModel : ViewModelBase
{
    private readonly ConnectionService _connections;
    private readonly Func<ConnectionDialogViewModel> _detailFactory;

    // Folder paths that have no connection under them yet (New Folder / emptied by a move). Without this
    // they'd vanish on the next rebuild, since a folder is otherwise just a prefix on a connection.
    private readonly HashSet<string> _emptyFolders = new(StringComparer.OrdinalIgnoreCase);

    private ConnectionManagerNode? _selectedFolder;

    public ConnectionManagerViewModel(ConnectionService connections, Func<ConnectionDialogViewModel> detailFactory, ILocalizer localizer)
    {
        _connections = connections;
        _detailFactory = detailFactory;
        Loc = localizer;
        RebuildTree();
    }

    public ILocalizer Loc { get; }

    /// <summary>Set by the window so the VM can ask a yes/no question (title, message); false if unavailable.</summary>
    public Func<string, string, Task<bool>>? ConfirmRequested { get; set; }

    /// <summary>The manager tree: nested folder nodes + connection leaves.</summary>
    public ObservableCollection<ConnectionManagerNode> Nodes { get; } = [];

    [ObservableProperty]
    private ConnectionManagerNode? _selectedNode;

    [ObservableProperty]
    private string _filter = string.Empty;

    // The connection form shown when a connection (or a new draft) is selected; reused from the old modal.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanActOnConnection))]
    private ConnectionDialogViewModel? _detail;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConnectionPane))]
    [NotifyPropertyChangedFor(nameof(IsFolderPane))]
    [NotifyPropertyChangedFor(nameof(IsEmptyPane))]
    private ManagerPane _pane;

    /// <summary>Editable folder name in the folder-detail panel.</summary>
    [ObservableProperty]
    private string _folderName = string.Empty;

    public bool IsConnectionPane => Pane == ManagerPane.Connection;
    public bool IsFolderPane => Pane == ManagerPane.Folder;
    public bool IsEmptyPane => Pane == ManagerPane.Empty;

    /// <summary>Duplicate/Delete apply only to a saved (persisted) connection in the detail panel.</summary>
    public bool CanActOnConnection => Detail is { IsEditing: true };

    /// <summary>Existing folder paths (including every intermediate parent), for the detail form's
    /// folder type-ahead. Distinct + sorted; a connection may still type a brand-new path.</summary>
    public IReadOnlyList<string> FolderSuggestions =>
        _connections.List()
            .Select(c => c.Folder)
            .Concat(_emptyFolders)
            .SelectMany(AncestorsAndSelf)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>Footer summary: "{connections} · {folders}".</summary>
    public string Summary
    {
        get
        {
            var connections = _connections.List().Count;
            var folders = AllFolders(Nodes).Count();
            return Loc.Get("ManagerSummary", connections, folders);
        }
    }

    // --- Pre-positioning called by MainViewModel before the window opens ---

    /// <summary>Open with an existing connection selected (right-click Edit… from the sidebar).</summary>
    public void SelectConnection(string id)
    {
        if (FindConnection(Nodes, id) is { } node)
        {
            SelectedNode = node;
        }
    }

    /// <summary>Open on a fresh connection draft, optionally pre-filed into <paramref name="folder"/>.</summary>
    public void StartNewConnection(string? folder)
    {
        var detail = _detailFactory();
        if (!string.IsNullOrWhiteSpace(folder))
        {
            detail.Folder = folder;
        }

        Detail = detail;
        SelectedNode = null;
        _selectedFolder = null;
        Pane = ManagerPane.Connection;
    }

    // --- Selection ---

    partial void OnSelectedNodeChanged(ConnectionManagerNode? value)
    {
        // Null is only ever set programmatically (e.g. while starting a draft) — leave the pane alone.
        if (value is null)
        {
            return;
        }

        if (value.IsFolder)
        {
            _selectedFolder = value;
            FolderName = value.Name;
            Detail = null;
            Pane = ManagerPane.Folder;
        }
        else
        {
            _selectedFolder = null;
            var detail = _detailFactory();
            detail.LoadForEdit(value.Connection!);
            Detail = detail;
            Pane = ManagerPane.Connection;
        }
    }

    partial void OnFilterChanged(string value) => RebuildTree(SelectedNode?.Connection?.Id);

    // --- Connection actions ---

    [RelayCommand]
    private void SaveConnection()
    {
        if (Detail is null)
        {
            return;
        }

        var saved = Detail.Save();
        _emptyFolders.Remove(saved.Folder ?? string.Empty);
        RebuildTree(saved.Id);
        OnPropertyChanged(nameof(Summary));
    }

    [RelayCommand]
    private void NewConnection() => StartNewConnection(SelectedFolderContext());

    [RelayCommand]
    private void Discard()
    {
        Detail = null;
        SelectedNode = null;
        Pane = ManagerPane.Empty;
    }

    [RelayCommand]
    private async Task DuplicateConnection()
    {
        if (Detail is not { IsEditing: true } || SelectedNode?.Connection is not { } connection)
        {
            return;
        }

        var copy = _connections.Duplicate(connection.Id, $"{connection.Name} {Loc["CopySuffix"]}");
        RebuildTree(copy.Id);
        OnPropertyChanged(nameof(Summary));
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task DeleteConnection()
    {
        if (Detail is not { IsEditing: true } || SelectedNode?.Connection is not { } connection)
        {
            return;
        }

        if (ConfirmRequested is not null
            && !await ConfirmRequested(Loc["Delete"], Loc.Get("DeleteConnectionConfirm", connection.Name)))
        {
            return;
        }

        _connections.Delete(connection.Id);
        Detail = null;
        SelectedNode = null;
        Pane = ManagerPane.Empty;
        RebuildTree();
        OnPropertyChanged(nameof(Summary));
    }

    // --- Folder actions ---

    [RelayCommand]
    private void NewFolder()
    {
        var parent = SelectedFolderContext();
        var path = UniqueFolderPath(parent, Loc["NewFolderName"]);
        _emptyFolders.Add(path);
        RebuildTree(selectFolderPath: path);
        OnPropertyChanged(nameof(Summary));
    }

    [RelayCommand]
    private void RenameFolder()
    {
        if (_selectedFolder is not { FolderPath: { } oldPath })
        {
            return;
        }

        var newName = FolderName.Trim();
        if (newName.Length == 0 || newName.Contains('/') || string.Equals(newName, _selectedFolder.Name, StringComparison.Ordinal))
        {
            return;
        }

        var parent = ParentPath(oldPath);
        var newPath = parent is null ? newName : $"{parent}/{newName}";
        MoveFolderPath(oldPath, newPath);
        RebuildTree(selectFolderPath: newPath);
    }

    [RelayCommand]
    private async Task DeleteFolder()
    {
        if (_selectedFolder is not { FolderPath: { } path })
        {
            return;
        }

        var affected = _connections.List().Where(c => IsUnderOrEqual(c.Folder, path)).ToList();
        if (affected.Count > 0)
        {
            if (ConfirmRequested is null
                || !await ConfirmRequested(Loc["DeleteFolder"], Loc.Get("DeleteFolderConfirm", affected.Count)))
            {
                return;
            }

            foreach (var connection in affected)
            {
                _connections.SetFolder(connection, null);
            }
        }

        _emptyFolders.RemoveWhere(p => IsUnderOrEqual(p, path));
        _selectedFolder = null;
        SelectedNode = null;
        Pane = ManagerPane.Empty;
        RebuildTree();
        OnPropertyChanged(nameof(Summary));
    }

    // --- Drag & drop (invoked by the window) ---

    /// <summary>True when <paramref name="dragged"/> may be dropped onto <paramref name="targetFolder"/>
    /// (or the root when null): never onto itself, its own descendant, or its current parent (a no-op).</summary>
    public bool CanDrop(ConnectionManagerNode dragged, ConnectionManagerNode? targetFolder)
    {
        if (targetFolder is not null && !targetFolder.IsFolder)
        {
            return false;
        }

        var targetPath = targetFolder?.FolderPath;
        if (dragged.IsConnection)
        {
            return !string.Equals(dragged.Connection!.Folder, targetPath, StringComparison.Ordinal);
        }

        // A folder can't move into itself or one of its own descendants, nor stay where it already is.
        if (dragged.FolderPath is not { } sourcePath || string.Equals(ParentPath(sourcePath), targetPath, StringComparison.Ordinal))
        {
            return false;
        }

        return targetPath is null || (!string.Equals(targetPath, sourcePath, StringComparison.OrdinalIgnoreCase)
            && !IsUnderOrEqual(targetPath, sourcePath));
    }

    /// <summary>The folder node for a given path (null path/empty = the root, returns null).</summary>
    public ConnectionManagerNode? FindFolderNode(string? path) =>
        string.IsNullOrEmpty(path) ? null : FindFolder(Nodes, path);

    /// <summary>Reparent a dragged connection/folder under <paramref name="targetFolder"/> (root when null).</summary>
    public void Drop(ConnectionManagerNode dragged, ConnectionManagerNode? targetFolder)
    {
        if (!CanDrop(dragged, targetFolder))
        {
            return;
        }

        var targetPath = targetFolder?.FolderPath;
        if (dragged.IsConnection)
        {
            _connections.SetFolder(dragged.Connection!, targetPath);
            RebuildTree(dragged.Connection!.Id);
        }
        else if (dragged.FolderPath is { } sourcePath)
        {
            var newPath = targetPath is null ? dragged.Name : $"{targetPath}/{dragged.Name}";
            MoveFolderPath(sourcePath, newPath);
            RebuildTree(selectFolderPath: newPath);
        }

        OnPropertyChanged(nameof(Summary));
    }

    // --- Tree building ---

    private void RebuildTree(string? selectConnectionId = null, string? selectFolderPath = null)
    {
        Nodes.Clear();

        var filter = Filter.Trim();
        var connections = _connections.List()
            .Where(c => filter.Length == 0
                || c.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || (c.Folder?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false))
            .OrderBy(c => c.Folder ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var connection in connections)
        {
            ResolveFolderChildren(connection.Folder).Add(ConnectionManagerNode.ForConnection(connection));
        }

        // Keep still-empty folders visible (hidden while filtering, since they can't match a query).
        if (filter.Length == 0)
        {
            foreach (var path in _emptyFolders)
            {
                EnsureFolderPath(path);
            }
        }

        RestoreSelection(selectConnectionId, selectFolderPath);
        OnPropertyChanged(nameof(FolderSuggestions));
    }

    private void RestoreSelection(string? connectionId, string? folderPath)
    {
        if (connectionId is not null && FindConnection(Nodes, connectionId) is { } connectionNode)
        {
            SelectedNode = connectionNode;
        }
        else if (folderPath is not null && FindFolder(Nodes, folderPath) is { } folderNode)
        {
            SelectedNode = folderNode;
        }
        else if (SelectedNode is null && Detail is null)
        {
            // Nothing selected and no draft in progress → empty state. A draft (Detail set, no tree row)
            // is left alone so filtering the list doesn't discard a half-filled new connection.
            Pane = ManagerPane.Empty;
        }
    }

    // Walk the /-path, creating folder nodes as needed; return the collection the connection lives in.
    private ObservableCollection<ConnectionManagerNode> ResolveFolderChildren(string? folderPath)
    {
        var current = Nodes;
        var soFar = string.Empty;
        foreach (var segment in SplitPath(folderPath))
        {
            soFar = soFar.Length == 0 ? segment : $"{soFar}/{segment}";
            var folder = current.FirstOrDefault(n => n.IsFolder && n.Name == segment)
                ?? AddSorted(current, ConnectionManagerNode.ForFolder(segment, soFar));
            current = folder.Children;
        }

        return current;
    }

    private void EnsureFolderPath(string fullPath)
    {
        var current = Nodes;
        var soFar = string.Empty;
        foreach (var segment in SplitPath(fullPath))
        {
            soFar = soFar.Length == 0 ? segment : $"{soFar}/{segment}";
            var folder = current.FirstOrDefault(n => n.IsFolder && n.Name == segment)
                ?? AddSorted(current, ConnectionManagerNode.ForFolder(segment, soFar));
            current = folder.Children;
        }
    }

    // Folders sort before connections, each alphabetically — keeps the tree stable across rebuilds.
    private static ConnectionManagerNode AddSorted(ObservableCollection<ConnectionManagerNode> container, ConnectionManagerNode node)
    {
        var index = 0;
        while (index < container.Count && Ranks(container[index], node) <= 0)
        {
            index++;
        }

        container.Insert(index, node);
        return node;
    }

    private static int Ranks(ConnectionManagerNode existing, ConnectionManagerNode incoming)
    {
        if (existing.IsFolder != incoming.IsFolder)
        {
            return existing.IsFolder ? -1 : 1;
        }

        return string.Compare(existing.Name, incoming.Name, StringComparison.OrdinalIgnoreCase);
    }

    // --- Folder path helpers ---

    // Rewrite every connection (and empty-folder entry) under oldPath so it hangs off newPath instead.
    private void MoveFolderPath(string oldPath, string newPath)
    {
        foreach (var connection in _connections.List().Where(c => IsUnderOrEqual(c.Folder, oldPath)).ToList())
        {
            _connections.SetFolder(connection, newPath + connection.Folder![oldPath.Length..]);
        }

        var moved = _emptyFolders.Where(p => IsUnderOrEqual(p, oldPath)).ToList();
        foreach (var path in moved)
        {
            _emptyFolders.Remove(path);
            _emptyFolders.Add(newPath + path[oldPath.Length..]);
        }
    }

    // The folder path context for a New Connection/New Folder: the selected folder, or the folder the
    // selected connection lives in (null = root).
    private string? SelectedFolderContext() => SelectedNode switch
    {
        { IsFolder: true, FolderPath: var path } => path,
        { IsConnection: true, Connection.Folder: var folder } => folder,
        _ => null
    };

    private string UniqueFolderPath(string? parent, string baseName)
    {
        string Candidate(string name) => parent is null ? name : $"{parent}/{name}";

        var path = Candidate(baseName);
        var counter = 2;
        while (FolderPathExists(path))
        {
            path = Candidate($"{baseName} {counter++}");
        }

        return path;
    }

    private bool FolderPathExists(string path) =>
        _emptyFolders.Contains(path) || _connections.List().Any(c => IsUnderOrEqual(c.Folder, path));

    // "Klanten/Klant A" -> "Klanten", "Klanten/Klant A" so parent folders are suggestible on their own.
    private static IEnumerable<string> AncestorsAndSelf(string? path)
    {
        var soFar = string.Empty;
        foreach (var segment in SplitPath(path))
        {
            soFar = soFar.Length == 0 ? segment : $"{soFar}/{segment}";
            yield return soFar;
        }
    }

    private static string[] SplitPath(string? folderPath) =>
        string.IsNullOrWhiteSpace(folderPath)
            ? []
            : folderPath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string? ParentPath(string path)
    {
        var index = path.LastIndexOf('/');
        return index < 0 ? null : path[..index];
    }

    // True when `folder` equals `path` or sits beneath it (so "Klanten/Klant A" is under "Klanten").
    private static bool IsUnderOrEqual(string? folder, string path) =>
        folder is not null
        && (string.Equals(folder, path, StringComparison.Ordinal) || folder.StartsWith(path + "/", StringComparison.Ordinal));

    private static ConnectionManagerNode? FindConnection(IEnumerable<ConnectionManagerNode> nodes, string id)
    {
        foreach (var node in nodes)
        {
            if (node.IsConnection && node.Connection!.Id == id)
            {
                return node;
            }

            if (node.IsFolder && FindConnection(node.Children, id) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private static ConnectionManagerNode? FindFolder(IEnumerable<ConnectionManagerNode> nodes, string path)
    {
        foreach (var node in nodes.Where(n => n.IsFolder))
        {
            if (string.Equals(node.FolderPath, path, StringComparison.Ordinal))
            {
                return node;
            }

            if (FindFolder(node.Children, path) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private static IEnumerable<ConnectionManagerNode> AllFolders(IEnumerable<ConnectionManagerNode> nodes)
    {
        foreach (var node in nodes.Where(n => n.IsFolder))
        {
            yield return node;
            foreach (var nested in AllFolders(node.Children))
            {
                yield return nested;
            }
        }
    }
}
