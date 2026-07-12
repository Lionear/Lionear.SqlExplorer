using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Lionear.SqlExplorer.App.ViewModels;
using Lionear.SqlExplorer.Core.Settings;

namespace Lionear.SqlExplorer.App.Views;

public partial class MainWindow : Window
{
    private readonly IAppSettingsStore? _settingsStore;

    // Parameterless ctor keeps the XAML previewer happy; the real app uses the injected overload.
    public MainWindow() : this(null)
    {
    }

    public MainWindow(IAppSettingsStore? settingsStore)
    {
        _settingsStore = settingsStore;
        InitializeComponent();
        RestoreLayout();

        // macOS gets its menu bar from NativeMenu.Menu (set in XAML) — the in-window Menu would
        // otherwise render a second, redundant bar underneath the title bar there.
        if (OperatingSystem.IsMacOS())
        {
            AppMenu.IsVisible = false;
        }

        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainViewModel vm)
            {
                vm.AboutRequested = ShowAboutAsync;

                // A language switch fires Loc.PropertyChanged(null) — the correct "everything on
                // this object changed" signal, and Loc[key] does return the fresh string right away,
                // but that alone does not repaint anything already on screen (confirmed: neither more
                // dispatcher pumps nor an explicit InvalidateVisual on every control in the tree makes
                // a difference — the bindings themselves never re-pull the new value, this isn't a
                // paint/layout problem). Toggling DataContext off and back forces every binding under
                // it to tear down and re-create from scratch, which does re-read the fresh value —
                // the same "reuse a control, swap its DataContext" mechanism DocumentView already
                // relies on for tab reuse, just applied here to force a refresh instead.
                vm.Loc.PropertyChanged += (_, _) =>
                {
                    var dataContext = DataContext;
                    DataContext = null;
                    DataContext = dataContext;
                };
            }
        };
    }

    private async Task ShowAboutAsync()
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        await new AboutWindow(vm.Loc).ShowDialog(this);
    }

    private void RestoreLayout()
    {
        if (_settingsStore is null)
        {
            return;
        }

        var settings = _settingsStore.Load();

        if (settings.WindowWidth is { } w && settings.WindowHeight is { } h)
        {
            Width = w;
            Height = h;
        }

        // Only honour a stored position when both coordinates are present, so a partially
        // written file can't drop the window at an off-screen corner.
        if (settings.WindowX is { } x && settings.WindowY is { } y)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Position = new PixelPoint((int)x, (int)y);
        }

        if (settings.WindowMaximized)
        {
            WindowState = WindowState.Maximized;
        }

        Body.RestoreSidebarWidth(settings.SidebarWidth);
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        PersistLayout();
        base.OnClosing(e);
    }

    private void PersistLayout()
    {
        if (_settingsStore is null)
        {
            return;
        }

        var settings = _settingsStore.Load();
        var maximized = WindowState == WindowState.Maximized;
        settings.WindowMaximized = maximized;

        // When maximized, Width/Height/Position describe the maximized frame; keep the last
        // normal-state values so restoring un-maximizes to a sane size and place.
        if (!maximized)
        {
            settings.WindowWidth = Width;
            settings.WindowHeight = Height;
            settings.WindowX = Position.X;
            settings.WindowY = Position.Y;
        }

        settings.SidebarWidth = Body.SidebarWidth;

        try
        {
            _settingsStore.Save(settings);
        }
        catch (Exception)
        {
            // Never block window close on a failed preference write.
        }
    }
}
