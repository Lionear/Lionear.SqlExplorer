using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using SqlExplorer.App.ViewModels;

namespace SqlExplorer.App.Views;

public partial class ConnectionManagerWindow : Window
{
    private const double DragThreshold = 5;

    private readonly TreeView _tree;

    // The pointer-press that may turn into a drag: its position, the node under it, and the original
    // args (DoDragDropAsync needs the PointerPressedEventArgs to capture the pointer).
    private Point _pressPoint;
    private ConnectionManagerNode? _pressNode;
    private PointerPressedEventArgs? _pressArgs;

    // The node currently being dragged, and the folder row currently lit as a drop target.
    private ConnectionManagerNode? _dragged;
    private ConnectionManagerNode? _highlighted;

    public ConnectionManagerWindow()
    {
        InitializeComponent();

        _tree = this.FindControl<TreeView>("ConnectionTree")!;
        DragDrop.SetAllowDrop(_tree, true);
        _tree.AddHandler(PointerPressedEvent, OnTreePointerPressed, RoutingStrategies.Tunnel);
        _tree.AddHandler(PointerMovedEvent, OnTreePointerMoved, RoutingStrategies.Tunnel);
        DragDrop.AddDragOverHandler(_tree, OnDragOver);
        DragDrop.AddDropHandler(_tree, OnDrop);

        DataContextChanged += (_, _) =>
        {
            if (DataContext is ConnectionManagerViewModel vm)
            {
                vm.ConfirmRequested = ShowConfirmAsync;
            }
        };
    }

    private ConnectionManagerViewModel? ViewModel => DataContext as ConnectionManagerViewModel;

    // File-type connection field: pick a path (moved here from the retired ConnectionDialog).
    private async void OnBrowseClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: ConnectionFieldInput input })
        {
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { AllowMultiple = false });
        if (files.Count > 0)
        {
            input.Value = files[0].TryGetLocalPath() ?? files[0].Path.ToString();
        }
    }

    private async Task<bool> ShowConfirmAsync(string title, string message)
    {
        var loc = ViewModel?.Loc;
        var dialog = new ConfirmDialog(title, message, loc?["Yes"] ?? "Yes", loc?["No"] ?? "No");
        return await dialog.ShowDialog<bool>(this);
    }

    // --- Drag & drop: reparent a connection/folder by dropping it onto a folder (or the root). ---

    private void OnTreePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(_tree).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _pressNode = NodeFrom(e.Source);
        _pressPoint = e.GetPosition(_tree);
        _pressArgs = e;
    }

    private async void OnTreePointerMoved(object? sender, PointerEventArgs e)
    {
        if (_pressNode is null || _pressArgs is null)
        {
            return;
        }

        if (!e.GetCurrentPoint(_tree).Properties.IsLeftButtonPressed)
        {
            _pressNode = null;
            _pressArgs = null;
            return;
        }

        var delta = e.GetPosition(_tree) - _pressPoint;
        if (Math.Abs(delta.X) < DragThreshold && Math.Abs(delta.Y) < DragThreshold)
        {
            return;
        }

        _dragged = _pressNode;
        var trigger = _pressArgs;
        _pressNode = null;
        _pressArgs = null;

        var transfer = new DataTransfer();
        transfer.Add(DataTransferItem.CreateText(_dragged.Name));
        try
        {
            await DragDrop.DoDragDropAsync(trigger, transfer, DragDropEffects.Move);
        }
        finally
        {
            ClearHighlight();
            _dragged = null;
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        var target = ResolveTargetFolder(NodeFrom(e.Source));
        Highlight(target);

        var allowed = _dragged is not null && ViewModel?.CanDrop(_dragged, target) == true;
        e.DragEffects = allowed ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        var target = ResolveTargetFolder(NodeFrom(e.Source));
        if (_dragged is not null)
        {
            ViewModel?.Drop(_dragged, target);
        }

        ClearHighlight();
        e.Handled = true;
    }

    // The folder a drop lands in: the hovered folder itself, or the folder a hovered connection lives in
    // (null = the root / ungrouped).
    private ConnectionManagerNode? ResolveTargetFolder(ConnectionManagerNode? hovered) => hovered switch
    {
        { IsFolder: true } folder => folder,
        { IsConnection: true } connection => ViewModel?.FindFolderNode(connection.Connection!.Folder),
        _ => null
    };

    private void Highlight(ConnectionManagerNode? target)
    {
        if (ReferenceEquals(target, _highlighted))
        {
            return;
        }

        ClearHighlight();
        if (target is { IsFolder: true })
        {
            target.IsDropTarget = true;
            _highlighted = target;
        }
    }

    private void ClearHighlight()
    {
        if (_highlighted is not null)
        {
            _highlighted.IsDropTarget = false;
            _highlighted = null;
        }
    }

    private static ConnectionManagerNode? NodeFrom(object? source) =>
        (source as Visual)?.FindAncestorOfType<TreeViewItem>()?.DataContext as ConnectionManagerNode;
}
