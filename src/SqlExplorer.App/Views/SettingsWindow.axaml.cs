using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SqlExplorer.App.ViewModels;

namespace SqlExplorer.App.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is SettingsViewModel vm)
            {
                vm.CloseRequested = Close;
            }
        };
    }

    // A File/Folder plugin setting: pick a path (a binary like mysqldump, or a default folder).
    private async void OnBrowseClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: PluginSettingFieldInput input })
        {
            return;
        }

        if (input.IsFolder)
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { AllowMultiple = false });
            if (folders.Count > 0)
            {
                input.Value = folders[0].TryGetLocalPath() ?? folders[0].Path.ToString();
            }

            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { AllowMultiple = false });
        if (files.Count > 0)
        {
            input.Value = files[0].TryGetLocalPath() ?? files[0].Path.ToString();
        }
    }

    // Copy the MCP bearer token to the clipboard.
    private async void OnCopyMcpTokenClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel { McpToken: { Length: > 0 } token }
            && TopLevel.GetTopLevel(this) is { Clipboard: { } clipboard })
        {
            await clipboard.SetTextAsync(token);
        }
    }

    // Copy the MCP server URL to the clipboard.
    private async void OnCopyMcpUrlClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel { McpUrl: { Length: > 0 } url }
            && TopLevel.GetTopLevel(this) is { Clipboard: { } clipboard })
        {
            await clipboard.SetTextAsync(url);
        }
    }
}
