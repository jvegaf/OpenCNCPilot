using OpenCNCPilot.UI.Views.Controls;
using Xunit;

namespace OpenCNCPilot.UI.Tests;

[Trait("Category","UI-VM")]
public class GCodeViewport_OverlayPropertiesTests
{
    [Fact]
    public void Defaults_Are_Set()
    {
        var v = new GCodeViewport();
        Assert.True(v.ShowGrid);
        Assert.True(v.ShowOrigin);
        Assert.False(v.ShowBounds);
        Assert.Equal(30.0, v.GridMinPixelStep, 6);
    }

    [Fact]
    public void Properties_Are_Configurable()
    {
        var v = new GCodeViewport
        {
            ShowGrid = false,
            ShowOrigin = false,
            ShowBounds = true,
            GridMinPixelStep = 42.0
        };
        Assert.False(v.ShowGrid);
        Assert.False(v.ShowOrigin);
        Assert.True(v.ShowBounds);
        Assert.Equal(42.0, v.GridMinPixelStep, 6);
    }
}
