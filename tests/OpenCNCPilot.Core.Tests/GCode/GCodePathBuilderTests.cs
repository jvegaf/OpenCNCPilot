using System.Linq;
using FluentAssertions;
using OpenCNCPilot.Core.GCode;
using OpenCNCPilot.Core.Geometry;
using Xunit;

namespace OpenCNCPilot.Core.Tests.GCode;

public class GCodePathBuilderTests
{
    [Fact]
    public void Builds_Lines_Rapids_And_Cuts_With_Bounds()
    {
        var parser = new GCodeParser();
        parser.Parse(new[]
        {
            "G90",
            "G0 X0 Y0 Z0",
            "F100",
            "G1 X10 Y0",
            "G1 X10 Y10",
        });

        var builder = new GCodePathBuilder();
        var geo = builder.Build(parser);

    geo.Lines.Count.Should().Be(3); // 1 rapid + 2 feeds => 3 lines
        geo.RapidCount.Should().Be(1);
        geo.CutCount.Should().Be(2);
        geo.Bounds.Min.X.Should().Be(0);
        geo.Bounds.Min.Y.Should().Be(0);
        geo.Bounds.Max.X.Should().Be(10);
        geo.Bounds.Max.Y.Should().Be(10);
    }

    [Fact]
    public void Builds_Arcs_As_Cuts()
    {
        var parser = new GCodeParser();
        parser.Parse(new[]
        {
            "G90",
            "G0 X0 Y0 Z0",
            "F200",
            "G2 X10 Y0 I5 J0"
        });

        var geo = new GCodePathBuilder().Build(parser);
        geo.Arcs.Should().HaveCount(1);
        geo.CutCount.Should().Be(1);
        geo.RapidCount.Should().Be(1);
        geo.Bounds.Max.X.Should().Be(10);
        geo.Bounds.Max.Y.Should().Be(0);
    }

    [Fact]
    public void Flattener_Respects_Tolerance()
    {
        var parser = new GCodeParser();
        parser.Parse(new[]
        {
            "G90",
            "G0 X0 Y0 Z0",
            "F1000",
            // quarter circle radius 10 centered at (10,0) from (0,0) to (10,10)
            "G3 X10 Y10 I10 J0"
        });

        var geo = new GCodePathBuilder().Build(parser);
        var arc = geo.Arcs.Single();

        var coarse = SegmentFlattener.FlattenArc(arc, tolerance: 1.0);
        var fine = SegmentFlattener.FlattenArc(arc, tolerance: 0.01);

        coarse.Count.Should().BeLessThan(fine.Count);
    coarse.First().Should().Be(arc.Start);
        coarse.Last().X.Should().BeApproximately(arc.End.X, 1e-9);
        coarse.Last().Y.Should().BeApproximately(arc.End.Y, 1e-9);
        coarse.Last().Z.Should().BeApproximately(arc.End.Z, 1e-9);
    }
}
