using Avalonia.Controls;
using Avalonia.Interactivity;
using SqlExplorer.App.ViewModels;

namespace SqlExplorer.App.Views;

public partial class AlterObjectDialog : Window
{
    public AlterObjectDialog()
    {
        InitializeComponent();
    }

    private void OnConfirm(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AlterObjectDialogViewModel { CanConfirm: true } vm)
        {
            Close(vm.SqlPreview);
        }
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
}
