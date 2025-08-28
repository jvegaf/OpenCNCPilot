using System;
using System.Linq;
using FluentAssertions;
using OpenCNCPilot.Core.GCode;
using OpenCNCPilot.Core.GCode.GCodeCommands;
using Xunit;

namespace OpenCNCPilot.Core.Tests.GCode;

public class GCodeParser_AdvancedTests
{
    [Fact]
    public void Parses_G2_G3_Arcs_With_IJ_In_XY()
    {
        var p = new GCodeParser();
        p.Parse(new[]{
            "G90 G21 G17",
            "G0 X0 Y0 Z5",
            "F100",
            "G2 X10 Y0 I5 J0",
            "G3 X10 Y10 I0 J5"
        });

        p.Commands.OfType<Arc>().Should().HaveCount(2);
        var a1 = p.Commands.OfType<Arc>().First();
        a1.Direction.Should().Be(ArcDirection.CW);
        a1.Plane.Should().Be(ArcPlane.XY);
        a1.Start.X.Should().BeApproximately(0, 1e-6);
        a1.End.X.Should().BeApproximately(10, 1e-6);

        var a2 = p.Commands.OfType<Arc>().Skip(1).First();
        a2.Direction.Should().Be(ArcDirection.CCW);
        a2.End.Y.Should().BeApproximately(10, 1e-6);
    }

    [Fact]
    public void Imperial_Units_Scale_Positions_And_Feed()
    {
        var p = new GCodeParser();
        p.Parse(new[]{
            "G90 G20",  // imperial
            "F10",      // 10 in/min -> 254 mm/min
            "G0 X1 Y2",
            "G1 X2 Y1"
        });

        p.Commands.OfType<Motion>().Should().HaveCount(2);
        var rapid = (Line)p.Commands[0];
        rapid.End.X.Should().BeApproximately(25.4, 1e-6);
        rapid.End.Y.Should().BeApproximately(50.8, 1e-6);
        var feed = (Line)p.Commands[1];
        feed.Feed.Should().BeApproximately(254.0, 1e-6);
    }

    [Fact]
    public void Incremental_Mode_Requires_Known_Start()
    {
        var p = new GCodeParser();
        Action act = () => p.Parse(new[]{
            "G91",
            "G1 X1"
        });
        act.Should().Throw<ParseException>()
           .WithMessage("*incremental motion is only allowed after an absolute position has been established*");
    }

    [Fact]
    public void Arc_IJK_And_R_Notation_Are_Mutually_Exclusive()
    {
        var p = new GCodeParser();
        Action act = () => p.Parse(new[]{
            "G90 G21 G0 X0 Y0 Z5",
            "F100",
            "G2 X10 Y0 I5 R5"
        });
        act.Should().Throw<ParseException>()
           .WithMessage("*both IJK and R notation used*");
    }
}
