using Lionear.SqlExplorer.Core.Ddl;
using Lionear.SqlExplorer.Core.Localization;
using Lionear.SqlExplorer.Sdk;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Lionear.SqlExplorer.App.ViewModels;

/// <summary>What <see cref="AlterObjectDialogViewModel"/> is confirming — a DROP (no extra input beyond
/// the SQL preview) or a column ALTER (needs a name/type input).</summary>
public enum AlterKind
{
    DropDatabase,
    DropSchema,
    DropTable,
    AddColumn,
    DropColumn,
    RenameColumn
}

/// <summary>
/// Backs the DROP/ALTER confirmation dialog — the destructive-and-alter counterpart to
/// <see cref="CreateObjectDialogViewModel"/>. Unlike DDL Create, the SQL here is built entirely
/// host-side (<see cref="AlterStatementBuilder"/>, no SDK member) since the syntax needed is close
/// enough across engines; only <see cref="AlterKind.RenameColumn"/> on SQL Server branches internally
/// on the provider id (T-SQL has no ALTER-based rename at all).
/// </summary>
public partial class AlterObjectDialogViewModel : ViewModelBase
{
    private ISqlDialect _dialect = null!;
    private string _providerId = string.Empty;
    private string? _schema;
    private string _target = string.Empty;
    private bool _isView;
    private string _existingColumn = string.Empty;

    [ObservableProperty]
    private string _newColumnName = string.Empty;

    [ObservableProperty]
    private string _newColumnType = string.Empty;

    [ObservableProperty]
    private bool _newColumnNullable = true;

    [ObservableProperty]
    private string _sqlPreview = string.Empty;

    public AlterObjectDialogViewModel(ILocalizer localizer)
    {
        Loc = localizer;
    }

    public ILocalizer Loc { get; }

    public AlterKind Kind { get; private set; }

    /// <summary>The object being dropped/altered, for the confirmation message (e.g. a table name).</summary>
    public string ObjectLabel { get; private set; } = string.Empty;

    public IReadOnlyList<string> ColumnTypes { get; private set; } = [];

    public bool IsAddColumn => Kind == AlterKind.AddColumn;

    public bool IsRenameColumn => Kind == AlterKind.RenameColumn;

    /// <summary>Drives the dialog's warning styling — every kind here except adding a column removes
    /// something and can't be undone through the app.</summary>
    public bool IsDestructive => Kind != AlterKind.AddColumn;

    public string DialogTitle => Kind switch
    {
        AlterKind.DropDatabase => Loc["DropDatabase"],
        AlterKind.DropSchema => Loc["DropSchema"],
        AlterKind.DropTable => Loc["DropTable"],
        AlterKind.AddColumn => Loc["AddColumnTitle"],
        AlterKind.DropColumn => Loc["DropColumn"],
        _ => Loc["RenameColumn"]
    };

    public bool CanConfirm => Kind switch
    {
        AlterKind.AddColumn => !string.IsNullOrWhiteSpace(NewColumnName) && !string.IsNullOrWhiteSpace(NewColumnType),
        AlterKind.RenameColumn => !string.IsNullOrWhiteSpace(NewColumnName) && NewColumnName != _existingColumn,
        _ => true
    };

    /// <summary>Reset the dialog for a specific drop/alter action — same DI-factory + Configure
    /// pattern as <see cref="CreateObjectDialogViewModel"/> (a per-invocation VM can't take
    /// constructor args from a zero-arg factory delegate).</summary>
    public void Configure(
        AlterKind kind, string providerId, ISqlDialect dialect, IReadOnlyList<string> columnTypes,
        string objectLabel, string? schema, string target, bool isView = false, string? existingColumn = null)
    {
        Kind = kind;
        _providerId = providerId;
        _dialect = dialect;
        ColumnTypes = columnTypes;
        ObjectLabel = objectLabel;
        _schema = schema;
        _target = target;
        _isView = isView;
        _existingColumn = existingColumn ?? string.Empty;

        NewColumnName = kind == AlterKind.RenameColumn ? _existingColumn : string.Empty;
        NewColumnType = columnTypes.FirstOrDefault() ?? string.Empty;
        NewColumnNullable = true;

        OnPropertyChanged(nameof(IsAddColumn));
        OnPropertyChanged(nameof(IsRenameColumn));
        OnPropertyChanged(nameof(IsDestructive));
        OnPropertyChanged(nameof(DialogTitle));
        OnPropertyChanged(nameof(ColumnTypes));
        RefreshPreview();
    }

    partial void OnNewColumnNameChanged(string value) => RefreshPreview();

    partial void OnNewColumnTypeChanged(string value) => RefreshPreview();

    partial void OnNewColumnNullableChanged(bool value) => RefreshPreview();

    private void RefreshPreview()
    {
        OnPropertyChanged(nameof(CanConfirm));

        if (Kind == AlterKind.AddColumn && string.IsNullOrWhiteSpace(NewColumnName))
        {
            SqlPreview = string.Empty;
            return;
        }

        try
        {
            SqlPreview = Kind switch
            {
                AlterKind.DropDatabase => AlterStatementBuilder.DropDatabase(_dialect, _target),
                AlterKind.DropSchema => AlterStatementBuilder.DropSchema(_dialect, _target),
                AlterKind.DropTable => AlterStatementBuilder.DropTable(_dialect, _schema, _target, _isView),
                AlterKind.AddColumn => AlterStatementBuilder.AddColumn(_dialect, _schema, _target, NewColumnName, NewColumnType, NewColumnNullable),
                AlterKind.DropColumn => AlterStatementBuilder.DropColumn(_dialect, _schema, _target, _existingColumn),
                AlterKind.RenameColumn => AlterStatementBuilder.RenameColumn(_providerId, _dialect, _schema, _target, _existingColumn, NewColumnName),
                _ => string.Empty
            };
        }
        catch (Exception ex)
        {
            SqlPreview = $"-- {ex.Message}";
        }
    }
}
