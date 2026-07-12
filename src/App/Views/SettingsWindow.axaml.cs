using Avalonia.Controls;
using Lionear.SqlExplorer.App.ViewModels;

namespace Lionear.SqlExplorer.App.Views;

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
}
