using System.Collections.Generic;
using OpenCNCPilot.Core.GCode.GCodeCommands;
using OpenCNCPilot.Core.Geometry;
using OpenCNCPilot.UI.Views.Controls;
using Xunit;

namespace OpenCNCPilot.UI.Tests;

[Trait("Category","UI-VM")]
public class ViewportAutoFitTests
{
    private static IReadOnlyList<Command> MakeRect(double width, double height)
    {
        return new List<Command>
        {
            new Line { Start = new Vector3(0,0,0), End = new Vector3(width,0,0), StartValid = true, PositionValid = new[]{true,true,true} },
            new Line { Start = new Vector3(width,0,0), End = new Vector3(width,height,0), StartValid = true, PositionValid = new[]{true,true,true} },
            new Line { Start = new Vector3(width,height,0), End = new Vector3(0,height,0), StartValid = true, PositionValid = new[]{true,true,true} },
            new Line { Start = new Vector3(0,height,0), End = new Vector3(0,0,0), StartValid = true, PositionValid = new[]{true,true,true} }
        };
    }

    [Fact]
    public void Compute_Sets_Zoom_To_Fit_Bounds_With_Margin()
    {
        var cmds = MakeRect(100, 50);
        // viewport 1000x500px -> half extents 500,250; spanX 100 -> scaleX= 100/(1000/2)=0.2; spanY 50/(500/2)=0.2
        // margin 1.1 => expected zoom = 0.22
        var (zoom, panX, panY) = ViewportAutoFit.Compute(cmds, 1000, 500, rotXDeg: 0, rotYDeg: 0, margin: 1.1);
        Assert.Equal(0.22, zoom, 6);
        // With no rotation, pan should center at (midX, midY) => (50,25)
        // TransformWorldToView returns (x - panX, y - panY). To center, pan equals midpoint.
        Assert.Equal(50, panX, 6);
        Assert.Equal(25, panY, 6);
    }

    [Fact]
    public void Compute_Centers_Using_Rotation_Transform()
    {
        var cmds = MakeRect(200, 200);
        // Use non-zero rotations; midpoint is (100,100,0)
        double rx = 30, ry = 45;
        var (zoom, panX, panY) = ViewportAutoFit.Compute(cmds, 800, 600, rx, ry);
        // Expected pan equals transformed midpoint with zero pan
        var (vx, vy) = ViewportMath.TransformWorldToView(100, 100, 0, rx, ry, 0, 0);
        Assert.Equal(vx, panX, 6);
        Assert.Equal(vy, panY, 6);
        Assert.True(zoom > 0);
    }
}
