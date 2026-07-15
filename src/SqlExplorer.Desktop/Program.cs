using Avalonia;

namespace SqlExplorer.Desktop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Single instance: if the app is already running (possibly hidden in the tray), tell it to surface
        // its window and exit — don't open a second copy. The primary's listener is started in App.
        if (!SqlExplorer.App.SingleInstance.TryBecomePrimary())
        {
            return;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<SqlExplorer.App.App>()
            .UsePlatformDetect()
            .LogToTrace();
}
