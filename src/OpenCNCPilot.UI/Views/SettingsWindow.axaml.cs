using Avalonia.Controls;
using OpenCNCPilot.UI.ViewModels;
using OpenCNCPilot.UI.Services;

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
                vm.PickFolderInteraction.RegisterHandler(async ctx =>
                {
                    var dialogs = (IDialogService?)App.Services?.GetService(typeof(IDialogService));
                    string? folder = null;
                    if (dialogs is not null)
                    {
                        folder = await dialogs.PickFolderAsync("Select folder", vm.LastGCodeDirectory);
                    }
                    ctx.SetOutput(folder);
                });
            }
        };
    }
}
