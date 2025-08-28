using Avalonia.Controls;
using Avalonia.Interactivity;

namespace OpenCNCPilot.UI.Views;

public partial class WarningWindow : Window
{
    public WarningWindow()
    {
        InitializeComponent();
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        Close(true);
    }
}
