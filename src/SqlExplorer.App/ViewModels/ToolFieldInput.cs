using SqlExplorer.Sdk.Localization;
using SqlExplorer.Sdk.Tools;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SqlExplorer.App.ViewModels;

/// <summary>Editable state for one <see cref="ToolField"/> in the generic tool dialog (Route A).
/// Mirrors <see cref="ConnectionFieldInput"/>, plus a per-field password reveal toggle (§7).</summary>
public partial class ToolFieldInput : ObservableObject
{
    private readonly IPluginLocalizer _localizer;

    public ToolFieldInput(ToolField field, IPluginLocalizer localizer)
    {
        Field = field;
        _localizer = localizer;
        _value = field.Default;
    }

    public ToolField Field { get; }

    [ObservableProperty]
    private string? _value;

    /// <summary>Password field: when true the value shows as plain text (the 👁 toggle).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PasswordChar))]
    private bool _revealPassword;

    private string LabelText => _localizer.Resolve(Field.LabelKey, Field.Label);
    public string Label => Field.Required ? $"{LabelText} *" : LabelText;
    public string? Watermark =>
        _localizer.Resolve(Field.PlaceholderKey, Field.Placeholder ?? string.Empty) is { Length: > 0 } text ? text : null;
    public bool IsFile => Field.Type == ToolFieldType.File;
    public bool IsBool => Field.Type == ToolFieldType.Bool;
    public bool IsChoice => Field.Type == ToolFieldType.Choice;
    public bool IsPassword => Field.Type == ToolFieldType.Password;

    /// <summary>Text/Password/File all show the free-text box (File adds a Browse button beside it).</summary>
    public bool IsText => Field.Type is ToolFieldType.Text or ToolFieldType.Password or ToolFieldType.File;

    public IReadOnlyList<string> Choices => Field.Choices ?? [];

    // Masked bullet for a password unless revealed; (char)0 shows plaintext.
    public char PasswordChar => IsPassword && !RevealPassword ? '•' : '\0';

    public bool BoolValue
    {
        get => bool.TryParse(Value, out var b) && b;
        set => Value = value ? "true" : "false";
    }

    public bool IsFilled => !Field.Required || !string.IsNullOrWhiteSpace(Value);
}
