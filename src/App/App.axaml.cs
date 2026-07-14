using System.Globalization;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Lionear.SqlExplorer.App.DependencyInjection;
using Lionear.SqlExplorer.App.Theming;
using Lionear.SqlExplorer.App.ViewModels;
using Lionear.SqlExplorer.App.Views;
using Lionear.SqlExplorer.Core.Localization;
using Lionear.SqlExplorer.Core.Settings;
using Lionear.SqlExplorer.Core.Shortcuts;
using Microsoft.Extensions.DependencyInjection;

namespace Lionear.SqlExplorer.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        var services = AppServices.Build();

        // Apply the saved theme/language before anything renders, so there's no flash of the
        // wrong theme or a Language toggle needed after every launch.
        var settingsStore = services.GetRequiredService<IAppSettingsStore>();
        var settings = settingsStore.Load();
        ThemeApplier.Apply(settings.Theme);
        if (settings.Language is { Length: > 0 } language)
        {
            services.GetRequiredService<ILocalizer>().SetCulture(CultureInfo.GetCultureInfo(language));
        }

        var viewModel = services.GetRequiredService<MainViewModel>();
        var keymap = services.GetRequiredService<KeymapService>();

        switch (ApplicationLifetime)
        {
            case IClassicDesktopStyleApplicationLifetime desktop:
                desktop.MainWindow = new MainWindow(settingsStore, keymap) { DataContext = viewModel };
                // Stop the MCP listener cleanly on exit so its loopback port is released promptly. Run it
                // OFF the UI thread with a timeout: awaiting StopAsync's continuations back onto the (blocked)
                // UI thread would deadlock and hang shutdown; the OS reclaims the port regardless, so this is
                // best-effort and must never block exit.
                desktop.ShutdownRequested += (_, _) =>
                {
                    try
                    {
                        Task.Run(() => services.GetRequiredService<Mcp.Hosting.McpService>().StopAsync())
                            .Wait(TimeSpan.FromSeconds(2));
                    }
                    catch
                    {
                        // Best-effort: never let a slow/failed stop hold the app open.
                    }
                };
                break;
            case ISingleViewApplicationLifetime singleView:
                singleView.MainView = new MainView { DataContext = viewModel };
                break;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
