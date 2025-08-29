using FluentAssertions;
using OpenCNCPilot.Core.Geometry;
using Xunit;

namespace OpenCNCPilot.Core.Tests.Geometry;

public class Bounds3Tests
{
    [Fact]
    public void ExpandAndPad_Work()
    {
        var b = Bounds3.FromPoint(new Vector3(1,2,3));
        b.Width.Should().Be(0);
        var b2 = b.Expand(new Vector3(-1, 4, 0));
        b2.Min.Should().Be(new Vector3(-1,2,0));
        b2.Max.Should().Be(new Vector3(1,4,3));
        var b3 = b2.Pad(1);
        b3.Min.Should().Be(new Vector3(-2,1, -1));
        b3.Max.Should().Be(new Vector3(2,5, 4));
    }
}
