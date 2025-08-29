using System;
using System.Collections.Generic;
using FluentAssertions;
using OpenCNCPilot.Core.Geometry;
using Xunit;

namespace OpenCNCPilot.Core.Tests;

public class PolylineSimplifierTests
{
    [Fact]
    public void Simplify_Should_Preserve_Endpoints()
    {
        var pts = new List<Vector3>
        {
            new(0,0,0), new(1,0,0), new(2,0,0), new(3,0,0), new(4,0,0)
        };

        var result = PolylineSimplifier.Simplify(pts, 0.1);

        result.Should().NotBeEmpty();
        result[0].Should().Be(pts[0]);
        result[^1].Should().Be(pts[^1]);
    }

    [Fact]
    public void Simplify_WithZeroEpsilon_Should_Return_Original()
    {
        var pts = new List<Vector3> { new(0,0,0), new(1,0,0), new(2,0,0) };
        var result = PolylineSimplifier.Simplify(pts, 0);
        result.Should().Equal(pts);
    }

    [Fact]
    public void Simplify_StraightLine_Should_Reduce_To_Endpoints()
    {
        var pts = new List<Vector3> { new(0,0,0), new(0.5,0,0), new(1,0,0) };
        var result = PolylineSimplifier.Simplify(pts, 0.001);
        result.Should().HaveCount(2);
        result[0].Should().Be(pts[0]);
        result[1].Should().Be(pts[^1]);
    }

    [Fact]
    public void Simplify_WithNoise_Should_Remove_Small_Jitter()
    {
        var pts = new List<Vector3>
        {
            new(0,0,0), new(0.25, 0.001, 0), new(0.5, -0.001, 0), new(0.75, 0.001, 0), new(1,0,0)
        };
        var result = PolylineSimplifier.Simplify(pts, 0.01);
        result.Should().ContainInOrder(new Vector3(0,0,0), new Vector3(1,0,0));
        result.Should().HaveCountLessOrEqualTo(3);
    }
}
