using System;
using Avalonia.Controls;
using OpenCNCPilot.UI.ViewModels;

namespace OpenCNCPilot.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += MainWindow_DataContextChanged;
    }

    private void MainWindow_DataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.ShowSettings.RegisterHandler(async interaction =>
            {
                var dlg = new SettingsWindow
                {
                    DataContext = interaction.Input,
                };
                var result = await dlg.ShowDialog<bool?>(this);
                interaction.SetOutput(result);
            });

            vm.ShowGrblSettings.RegisterHandler(async interaction =>
            {
                var dlg = new GrblSettingsWindow
                {
                    DataContext = interaction.Input,
                };
                var result = await dlg.ShowDialog<bool?>(this);
                interaction.SetOutput(result);
            });
        }
    }
}
