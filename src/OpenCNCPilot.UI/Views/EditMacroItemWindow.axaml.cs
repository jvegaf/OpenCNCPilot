using System.Threading.Tasks;
using Avalonia.Controls;
using OpenCNCPilot.UI.ViewModels;

namespace OpenCNCPilot.UI.Views;

public partial class EditMacroItemWindow : Window
{
    public EditMacroItemWindow()
    {
        InitializeComponent();
        if (DataContext is EditMacroItemViewModel vm)
        {
            Hook(vm);
        }
        this.DataContextChanged += (_, __) =>
        {
            if (DataContext is EditMacroItemViewModel vm)
                Hook(vm);
        };
    }

    private void Hook(EditMacroItemViewModel vm)
    {
        vm.Close.RegisterHandler(async interaction =>
        {
            var result = interaction.Input;
            Close(result);
            await Task.CompletedTask;
        });
    }
}
