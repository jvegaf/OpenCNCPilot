using OpenCNCPilot.UI.Views.Controls;
using Xunit;

namespace OpenCNCPilot.UI.Tests;

public class ViewportInteractionLogicTests
{
    [Fact]
    public void Rotation_Clamps_X_And_Accumulates_Y()
    {
        var (rx, ry) = ViewportInteractionLogic.ApplyRotation(80, 10, 0, 20, 1);
        Assert.Equal(89.0, rx, 6);
        Assert.Equal(10, ry, 6);
        (rx, ry) = ViewportInteractionLogic.ApplyRotation(-80, 0, 0, -20, 1);
        Assert.Equal(-89.0, rx, 6);
    }

    [Fact]
    public void Pan_Accumulates_Deltas_With_Inverted_Y()
    {
        var (px, py) = ViewportInteractionLogic.ApplyPan(0, 0, 10, 5, 0.5);
        Assert.Equal(5.0, px, 6);
        Assert.Equal(-2.5, py, 6);
    }

    [Fact]
    public void Wheel_Zooms_In_And_Out()
    {
        var z = 1.0;
        var inZ = ViewportInteractionLogic.ApplyWheelZoom(z, 120); // zoom in
        Assert.True(inZ < z);
        var outZ = ViewportInteractionLogic.ApplyWheelZoom(z, -120); // zoom out
        Assert.True(outZ > z);
    }
}
