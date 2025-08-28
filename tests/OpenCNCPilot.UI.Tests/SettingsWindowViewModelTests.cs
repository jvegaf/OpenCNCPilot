using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using OpenCNCPilot.UI.Services;
using OpenCNCPilot.UI.ViewModels;
using Xunit;

public class SettingsWindowViewModelTests
{
    [Fact]
    public async Task Load_And_Save_Roundtrip_And_Clamps()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "OpenCNCPilotTests", Path.GetRandomFileName());
        var svc = new JsonSettingsService(tmp);

        var vm = new SettingsWindowViewModel(svc);
        // Espera a que se ejecute Load() async en ctor
        await Task.Delay(10);

        // Asigna valores fuera de rango para validar clamps
        vm.ViewerRotateSensitivity = 10.0; // > max 2.0
        vm.ViewerPanSensitivity = 10.0;    // > max 1.0
        vm.ViewerZoomStepFactor = 10.0;    // > max 1.5
        vm.ViewerGridMinPixelStep = 1.0;   // < min 4.0
        vm.ViewerShowGrid = false;
        vm.ViewerShowOrigin = false;
        vm.ViewerShowBounds = true;

        bool? closed = null;
        vm.CloseRequested += (_, saved) => closed = saved;
        vm.SaveCommand.Execute().Subscribe();

        // espera a IO
        await Task.Delay(10);
        closed.Should().BeTrue();

        var s = await svc.LoadAsync();
        s.ViewerRotateSensitivity.Should().Be(2.0);
        s.ViewerPanSensitivity.Should().Be(1.0);
        s.ViewerZoomStepFactor.Should().Be(1.5);
        s.ViewerGridMinPixelStep.Should().BeGreaterOrEqualTo(4.0);
        s.ViewerShowGrid.Should().BeFalse();
        s.ViewerShowOrigin.Should().BeFalse();
        s.ViewerShowBounds.Should().BeTrue();
    }
}
