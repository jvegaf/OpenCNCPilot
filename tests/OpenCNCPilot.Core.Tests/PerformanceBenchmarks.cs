using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using OpenCNCPilot.Core.GCode;
using OpenCNCPilot.Core.Geometry;
using Xunit;

namespace OpenCNCPilot.Core.Tests;

public class PerformanceBenchmarks
{
    private static string GenerateGCodeLines(int count)
    {
        // Header: metric units, absolute, set a feed rate
        var sb = new StringBuilder();
        sb.AppendLine("G21 G90");
        sb.AppendLine("F1000");
        double x = 0, y = 0;
        for (int i = 0; i < count; i++)
        {
            x += 0.5; y += (i % 2 == 0 ? 0.25 : -0.25);
            sb.Append("G1 X").Append(x.ToString("F3", Constants.DecimalParseFormat))
              .Append(" Y").Append(y.ToString("F3", Constants.DecimalParseFormat))
              .Append('\n');
        }
        return sb.ToString();
    }

    [Trait("Category", "Performance")]
    [Fact]
    public void Pipeline_10k_Lines_Should_Run_Quickly()
    {
        var gcode = GenerateGCodeLines(10_000);

        var parser = new GCodeParser();
        var swParse = Stopwatch.StartNew();
        parser.Parse(gcode.Split('\n'));
        swParse.Stop();

        var builder = new GCodePathBuilder();
        var swBuild = Stopwatch.StartNew();
        var geometry = builder.Build(parser);
        swBuild.Stop();

        var swFlatten = Stopwatch.StartNew();
        var (rapids, cuts) = SegmentFlattener.Flatten(geometry, 0.05);
        swFlatten.Stop();

        // Optional simplification to observe cost (no assertion)
        var swSimplify = Stopwatch.StartNew();
        for (int i = 0; i < cuts.Count; i++) cuts[i] = PolylineSimplifier.Simplify(cuts[i], 0.10);
        for (int i = 0; i < rapids.Count; i++) rapids[i] = PolylineSimplifier.Simplify(rapids[i], 0.10);
        swSimplify.Stop();

        // No hard assertions to avoid flakiness in CI; write to console for manual tracking
        Console.WriteLine($"Perf 10k: Parse={swParse.ElapsedMilliseconds} ms, Build={swBuild.ElapsedMilliseconds} ms, Flatten={swFlatten.ElapsedMilliseconds} ms, Simplify={swSimplify.ElapsedMilliseconds} ms");
    }

    [Trait("Category", "Performance")]
    [Fact]
    public void Pipeline_100k_Lines_Sanity_Check()
    {
        var gcode = GenerateGCodeLines(100_000);

        var parser = new GCodeParser();
        var swParse = Stopwatch.StartNew();
        parser.Parse(gcode.Split('\n'));
        swParse.Stop();

        var builder = new GCodePathBuilder();
        var swBuild = Stopwatch.StartNew();
        var geometry = builder.Build(parser);
        swBuild.Stop();

        var swFlatten = Stopwatch.StartNew();
        var (rapids, cuts) = SegmentFlattener.Flatten(geometry, 0.05);
        swFlatten.Stop();

        var swSimplify = Stopwatch.StartNew();
        for (int i = 0; i < cuts.Count; i++) cuts[i] = PolylineSimplifier.Simplify(cuts[i], 0.10);
        for (int i = 0; i < rapids.Count; i++) rapids[i] = PolylineSimplifier.Simplify(rapids[i], 0.10);
        swSimplify.Stop();

        Console.WriteLine($"Perf 100k: Parse={swParse.ElapsedMilliseconds} ms, Build={swBuild.ElapsedMilliseconds} ms, Flatten={swFlatten.ElapsedMilliseconds} ms, Simplify={swSimplify.ElapsedMilliseconds} ms");
    }
}
