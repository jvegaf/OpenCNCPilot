using System.Collections.Generic;
using OpenCNCPilot.Core.GCode.GCodeCommands;
using OpenCNCPilot.Core.Geometry;
using OpenCNCPilot.UI.Views.Controls;
using Xunit;

namespace OpenCNCPilot.UI.Tests;

[Trait("Category","UI-VM")]
public class ViewportBoundsTests
{
    [Fact]
    public void ComputeBounds_Returns_MinMax_For_Rect()
    {
        var cmds = new List<Command>
        {
            new Line { Start = new Vector3(1,2,0), End = new Vector3(5,2,0), StartValid = true, PositionValid = new[]{true,true,true} },
            new Line { Start = new Vector3(5,2,0), End = new Vector3(5,7,0), StartValid = true, PositionValid = new[]{true,true,true} },
            new Line { Start = new Vector3(5,7,0), End = new Vector3(1,7,0), StartValid = true, PositionValid = new[]{true,true,true} },
            new Line { Start = new Vector3(1,7,0), End = new Vector3(1,2,0), StartValid = true, PositionValid = new[]{true,true,true} },
        };
        var has = ViewportAutoFit.TryComputeBounds(cmds, out var minX, out var minY, out var maxX, out var maxY);
        Assert.True(has);
        Assert.Equal(1, minX, 6);
        Assert.Equal(2, minY, 6);
        Assert.Equal(5, maxX, 6);
        Assert.Equal(7, maxY, 6);
    }

    [Fact]
    public void ComputeBounds_Returns_False_When_Empty()
    {
        var has = ViewportAutoFit.TryComputeBounds(new List<Command>(), out var minX, out var minY, out var maxX, out var maxY);
        Assert.False(has);
    }
}
