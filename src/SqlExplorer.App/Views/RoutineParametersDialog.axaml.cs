using Avalonia.Controls;
using SqlExplorer.App.ViewModels;

namespace SqlExplorer.App.Views;

public partial class RoutineParametersDialog : Window
{
    public RoutineParametersDialog()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is RoutineParametersDialogViewModel vm)
            {
                vm.CloseRequested = Close;
            }
        };
    }
}
