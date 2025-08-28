using Avalonia.Controls;
using OpenCNCPilot.UI.ViewModels;

namespace OpenCNCPilot.UI.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        this.DataContextChanged += (_, __) =>
        {
            if (DataContext is SettingsWindowViewModel vm)
            {
                vm.CloseRequested += (_, result) => Close(result);
            }
        };
    }
}
