using OpenCNCPilot.UI.Views.Controls;
using Xunit;

namespace OpenCNCPilot.UI.Tests;

public class ViewportMathTests
{
    [Theory]
    [InlineData(10, 5, 0, 0, 0, 0, 0, 10, 5)]
    [InlineData(10, 5, 0, 0, 0, 2, -3, 8, 8)]
    public void Transform_NoRotation_AppliesPan(double x, double y, double z, double rx, double ry, double panX, double panY, double expX, double expY)
    {
        var (vx, vy) = ViewportMath.TransformWorldToView(x, y, z, rx, ry, panX, panY);
        Assert.Equal(expX, vx, 5);
        Assert.Equal(expY, vy, 5);
    }

    [Fact]
    public void Transform_RotateY_90_ReducesXToZero()
    {
        var (vx, vy) = ViewportMath.TransformWorldToView(10, 0, 0, 0, 90, 0, 0);
        Assert.InRange(vx, -1e-6, 1e-6);
        Assert.Equal(0, vy, 6);
    }

    [Fact]
    public void Transform_RotateX_90_ReducesYToZero()
    {
        var (vx, vy) = ViewportMath.TransformWorldToView(0, 10, 0, 90, 0, 0, 0);
        Assert.Equal(0, vx, 6);
        Assert.InRange(vy, -1e-6, 1e-6);
    }
}
