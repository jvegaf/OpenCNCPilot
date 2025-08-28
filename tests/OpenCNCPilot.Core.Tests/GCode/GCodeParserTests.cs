using FluentAssertions;
using OpenCNCPilot.Core.GCode;
using OpenCNCPilot.Core.GCode.GCodeCommands;
using OpenCNCPilot.Core.Geometry;
using Xunit;

namespace OpenCNCPilot.Core.Tests.GCode;

public class GCodeParserTests
{
    [Fact]
    public void Parses_G0_Rapid_Moves_Without_Feed()
    {
        var parser = new GCodeParser();
        parser.Parse(new[]
        {
            "G90",
            "G0 X0 Y0 Z5",
            "G0 X10 Y0"
        });

        parser.Commands.Should().HaveCount(2);
        var first = parser.Commands[0] as Line;
        first.Should().NotBeNull();
        first!.Rapid.Should().BeTrue();
        first.End.Should().Be(new Vector3(0, 0, 5));
        parser.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Parses_G1_With_Feed_In_Metric()
    {
        var parser = new GCodeParser();
        parser.Parse(new[]
        {
            "G90",
            "G0 X0 Y0 Z0",
            "F100",
            "G1 X10 Y10"
        });

        parser.Commands.Should().HaveCount(2); // one G0 + one G1
        var line = parser.Commands[1] as Line;
        line.Should().NotBeNull();
        line!.Rapid.Should().BeFalse();
        line.Feed.Should().Be(100);
        line.Start.Should().Be(new Vector3(0, 0, 0));
        line.End.Should().Be(new Vector3(10, 10, 0));
        parser.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void G1_Without_Feed_Throws()
    {
        var parser = new GCodeParser();
        var act = () => parser.Parse(new[]
        {
            "G90",
            "G0 X0 Y0 Z0",
            "G1 X1"
        });

        act.Should().Throw<ParseException>()
           .Which.Message.Should().Contain("feed rate undefined");
    }

    [Fact]
    public void Unknown_Word_Generates_Warning()
    {
        var parser = new GCodeParser();
        parser.Parse(new[]
        {
            "G90",
            "G0 X0 Y0 Z0 Q123"
        });

        parser.Warnings.Should().NotBeEmpty();
    }

    [Fact]
    public void Negative_Spindle_Speeds_Warn_But_Are_Absolute()
    {
        var parser = new GCodeParser();
        parser.Parse(new[]
        {
            "S-1200"
        });

        parser.Commands.Should().HaveCount(1);
        var spindle = parser.Commands[0] as Spindle;
        spindle.Should().NotBeNull();
        spindle!.Speed.Should().Be(1200);
        parser.Warnings.Should().NotBeEmpty();
    }
}
