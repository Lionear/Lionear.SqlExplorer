using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Lionear.SqlExplorer.Core.Localization;

namespace Lionear.SqlExplorer.App.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
    }

    public AboutWindow(ILocalizer loc) : this()
    {
        Title = loc["About"];
        TitleText.Text = loc["AboutText"];
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = version is null ? string.Empty : $"v{version.ToString(3)}";
        OkButton.Content = "OK";
    }

    private void OnOk(object? sender, RoutedEventArgs e) => Close();
}
