using Avalonia;
using OpenCNCPilot.UI.Views.Controls;
using Xunit;

namespace OpenCNCPilot.UI.Tests;

[Trait("Category","UI-VM")]
public class GCodeViewport_SensitivitiesTests
{
    [Fact]
    public void Defaults_Are_Set_As_Expected()
    {
        var ctl = new GCodeViewport();
        Assert.Equal(0.3, ctl.RotateSensitivity, 6);
        Assert.Equal(0.02, ctl.PanSensitivity, 6);
        Assert.Equal(1.1, ctl.ZoomStepFactor, 6);
    }

    [Fact]
    public void Sensitivities_Are_Configurable()
    {
        var ctl = new GCodeViewport
        {
            RotateSensitivity = 0.5,
            PanSensitivity = 0.05,
            ZoomStepFactor = 1.2
        };

        Assert.Equal(0.5, ctl.RotateSensitivity, 6);
        Assert.Equal(0.05, ctl.PanSensitivity, 6);
        Assert.Equal(1.2, ctl.ZoomStepFactor, 6);
    }
}
