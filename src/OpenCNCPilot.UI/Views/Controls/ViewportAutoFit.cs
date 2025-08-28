using System;
using System.Collections.Generic;
using OpenCNCPilot.Core.GCode.GCodeCommands;

namespace OpenCNCPilot.UI.Views.Controls;

public static class ViewportAutoFit
{
    public static bool TryComputeBounds(
        IReadOnlyList<Command> commands,
        out double minX, out double minY,
        out double maxX, out double maxY)
    {
        double minX0 = double.PositiveInfinity, minY0 = double.PositiveInfinity;
        double maxX0 = double.NegativeInfinity, maxY0 = double.NegativeInfinity;
        if (commands is null || commands.Count == 0)
        {
            minX = minY = maxX = maxY = 0;
            return false;
        }

        foreach (var c in commands)
        {
            switch (c)
            {
                case Line l:
                    Accumulate(l.Start.X, l.Start.Y, ref minX0, ref minY0, ref maxX0, ref maxY0);
                    Accumulate(l.End.X, l.End.Y, ref minX0, ref minY0, ref maxX0, ref maxY0);
                    break;
                case Arc a:
                    Accumulate(a.Start.X, a.Start.Y, ref minX0, ref minY0, ref maxX0, ref maxY0);
                    Accumulate(a.End.X, a.End.Y, ref minX0, ref minY0, ref maxX0, ref maxY0);
                    break;
            }
        }

        if (!double.IsFinite(minX0) || !double.IsFinite(minY0) || !double.IsFinite(maxX0) || !double.IsFinite(maxY0))
        {
            minX = minY = maxX = maxY = 0;
            return false;
        }

        minX = minX0; minY = minY0; maxX = maxX0; maxY = maxY0;
        return true;
    }

    private static void Accumulate(double x, double y, ref double minX, ref double minY, ref double maxX, ref double maxY)
    {
        if (x < minX) minX = x; if (x > maxX) maxX = x;
        if (y < minY) minY = y; if (y > maxY) maxY = y;
    }

    public static (double zoom, double panX, double panY) Compute(
        IReadOnlyList<Command> commands,
        int width,
        int height,
        double rotXDeg,
        double rotYDeg,
        double margin = 1.1)
    {
        if (!TryComputeBounds(commands, out var minX, out var minY, out var maxX, out var maxY))
            return (1.0, 0.0, 0.0);

        width = Math.Max(1, width);
        height = Math.Max(1, height);

        double spanX = Math.Max(1e-9, maxX - minX);
        double spanY = Math.Max(1e-9, maxY - minY);
        double scaleX = spanX / (width / 2.0) * margin;
        double scaleY = spanY / (height / 2.0) * margin;
        double zoom = Math.Max(scaleX, scaleY);

        // center using transformed midpoint with zero pan
        double midX = (minX + maxX) / 2.0;
        double midY = (minY + maxY) / 2.0;
        var (vx, vy) = ViewportMath.TransformWorldToView(midX, midY, 0, rotXDeg, rotYDeg, 0, 0);
        double panX = vx;
        double panY = vy;

        return (zoom, panX, panY);
    }
}
