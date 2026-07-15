using Avalonia.Controls;
using SqlExplorer.App.ViewModels;

namespace SqlExplorer.App.Views;

public partial class NodeInfoDialog : Window
{
    public NodeInfoDialog()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is NodeInfoDialogViewModel vm)
            {
                vm.CloseRequested = Close;
            }
        };
    }
}
