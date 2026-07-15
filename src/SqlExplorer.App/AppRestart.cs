using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace SqlExplorer.App;

/// <summary>
/// Relaunches the application so <c>PluginMaintenance</c> can apply staged plugin install/remove/swap
/// changes at startup. Spawns a fresh instance of the current executable, then shuts the running one down.
/// <see cref="Environment.ProcessPath"/> resolves to the app host (the executable inside the .app bundle on
/// macOS), so starting it relaunches cleanly.
/// </summary>
public static class AppRestart
{
    public static void Restart()
    {
        if (Environment.ProcessPath is { } exe)
        {
            Process.Start(new ProcessStartInfo(exe) { UseShellExecute = false });
        }

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }
}
