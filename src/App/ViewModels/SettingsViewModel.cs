using System.Collections.ObjectModel;
using System.Globalization;
using Lionear.SqlExplorer.App.Theming;
using Lionear.SqlExplorer.Core.Localization;
using Lionear.SqlExplorer.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Lionear.SqlExplorer.App.ViewModels;

/// <summary>
/// Backs the Preferences window: General/Appearance/Editor/Query categories. Works on a copy of
/// only the fields shown here — <see cref="ApplyAsync"/>/<see cref="ApplyAndCloseAsync"/> load the
/// *current* full <see cref="AppSettings"/> from the store, patch just these fields, and save, so a
/// concurrent window-geometry save (<c>MainWindow.PersistLayout</c>) is never clobbered. Theme/language
/// take effect immediately (no restart) via <see cref="ThemeApplier"/>/<see cref="ILocalizer.SetCulture"/>.
/// </summary>
public partial class SettingsViewModel : ViewModelBase
{
    private readonly IAppSettingsStore _store;

    [ObservableProperty]
    private string _selectedCategory = "General";

    [ObservableProperty]
    private string? _language;

    [ObservableProperty]
    private AppTheme _theme;

    [ObservableProperty]
    private double? _editorFontSize;

    [ObservableProperty]
    private bool _editorWordWrap;

    [ObservableProperty]
    private bool _confirmBeforeSave;

    public SettingsViewModel(IAppSettingsStore store, ILocalizer localizer)
    {
        _store = store;
        Loc = localizer;
        LoadFromStore();
    }

    public ILocalizer Loc { get; }

    public ObservableCollection<string> Categories { get; } = ["General", "Appearance", "Editor", "Query"];

    public IReadOnlyList<AppTheme> Themes { get; } = [AppTheme.System, AppTheme.Light, AppTheme.Dark];

    /// <summary>Set by the view; called to close the window (Apply and Close, or Cancel).</summary>
    public Action? CloseRequested { get; set; }

    private void LoadFromStore()
    {
        var settings = _store.Load();
        Language = settings.Language;
        Theme = settings.Theme;
        EditorFontSize = settings.EditorFontSize;
        EditorWordWrap = settings.EditorWordWrap;
        ConfirmBeforeSave = settings.ConfirmBeforeSave;
    }

    [RelayCommand]
    private void SetLanguage(string code) => Language = code;

    [RelayCommand]
    private void SetTheme(AppTheme theme) => Theme = theme;

    [RelayCommand]
    private void RestoreDefaults()
    {
        var defaults = new AppSettings();
        Language = defaults.Language;
        Theme = defaults.Theme;
        EditorFontSize = defaults.EditorFontSize;
        EditorWordWrap = defaults.EditorWordWrap;
        ConfirmBeforeSave = defaults.ConfirmBeforeSave;
    }

    [RelayCommand]
    private void Apply() => ApplyInternal();

    [RelayCommand]
    private void ApplyAndClose()
    {
        ApplyInternal();
        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke();

    private void ApplyInternal()
    {
        // Load-patch-save: never overwrite fields this window doesn't own (window geometry, sidebar).
        var settings = _store.Load();
        settings.Language = Language;
        settings.Theme = Theme;
        settings.EditorFontSize = EditorFontSize;
        settings.EditorWordWrap = EditorWordWrap;
        settings.ConfirmBeforeSave = ConfirmBeforeSave;
        _store.Save(settings);

        ThemeApplier.Apply(Theme);
        if (Language is { Length: > 0 } language)
        {
            Loc.SetCulture(CultureInfo.GetCultureInfo(language));
        }
    }
}
