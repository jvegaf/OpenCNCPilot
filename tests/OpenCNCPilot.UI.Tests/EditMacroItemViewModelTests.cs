using System.Threading.Tasks;
using System.Reactive.Threading.Tasks;
using FluentAssertions;
using OpenCNCPilot.UI.ViewModels;
using Xunit;

namespace OpenCNCPilot.UI.Tests;

public class EditMacroItemViewModelTests
{
    [Fact]
    public async Task OkCommand_Returns_Result_When_Valid()
    {
        var vm = new EditMacroItemViewModel
        {
            MacroName = "Home",
            Commands = "G28",
            UseMacros = true
        };

        EditMacroResult? result = null;
        vm.Close.RegisterHandler(async interaction =>
        {
            result = interaction.Input;
            interaction.SetOutput(interaction.Input);
            await Task.CompletedTask;
        });

    var res = await vm.OkCommand.Execute().ToTask();
        result.Should().NotBeNull();
        res.Should().NotBeNull();
        res!.Name.Should().Be("Home");
        res.Commands.Should().Be("G28");
        res.UseMacros.Should().BeTrue();
    }

    [Fact]
    public async Task OkCommand_Blocked_When_Invalid_Characters()
    {
        var vm = new EditMacroItemViewModel
        {
            MacroName = "Bad:Name",
            Commands = "G0 X0;Y0"
        };

        EditMacroResult? result = null;
        vm.Close.RegisterHandler(async interaction =>
        {
            result = interaction.Input;
            interaction.SetOutput(interaction.Input);
            await Task.CompletedTask;
        });

    var res = await vm.OkCommand.Execute().ToTask();
        res.Should().BeNull();
        result.Should().BeNull();
    }
}
