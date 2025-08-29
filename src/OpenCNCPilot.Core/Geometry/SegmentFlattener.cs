using System;
using System.Collections.Generic;
using OpenCNCPilot.Core.GCode.GCodeCommands;

namespace OpenCNCPilot.Core.Geometry;

public static class SegmentFlattener
{
    /// <summary>
    /// Flattens a mix of line and arc segments into polylines grouped by movement type.
    /// Returns two lists: Rapid polyline points and Cut polyline points (each inner list is a path).
    /// </summary>
    public static (List<List<Vector3>> rapids, List<List<Vector3>> cuts) Flatten(GeometryData data, double tolerance = 0.05)
    {
        var rapidPaths = new List<List<Vector3>>();
        var cutPaths = new List<List<Vector3>>();

        void Emit(ref List<List<Vector3>> container, in Vector3 a, in Vector3 b)
        {
            if (container.Count == 0 || container[^1].Count == 0 || container[^1][^1] != a)
                container.Add(new List<Vector3> { a, b });
            else
                container[^1].Add(b);
        }

        foreach (var l in data.Lines)
        {
            if (l.Type == MovementType.Rapid)
                Emit(ref rapidPaths, l.Start, l.End);
            else
                Emit(ref cutPaths, l.Start, l.End);
        }

        foreach (var a in data.Arcs)
        {
            var points = FlattenArc(a, tolerance);
            var container = a.Type == MovementType.Rapid ? rapidPaths : cutPaths;
            if (points.Count < 2) continue;
            // Stitch sequence into container
            for (int i = 0; i < points.Count - 1; i++)
            {
                Emit(ref container, points[i], points[i + 1]);
            }
        }

        return (rapidPaths, cutPaths);
    }

    /// <summary>
    /// Returns a polyline for an arc segment with max sagitta error not exceeding tolerance.
    /// </summary>
    public static List<Vector3> FlattenArc(ArcSegment arc, double tolerance)
    {
        // Reconstruct an Arc helper instance for interpolation and geometry calculations
        var arcHelper = new Arc
        {
            Start = arc.Start,
            End = arc.End,
            Direction = arc.Direction,
            Plane = arc.Plane,
            U = arc.U,
            V = arc.V,
            Feed = 0
        };

        double radius = arcHelper.Radius;
        double span = Math.Abs(arcHelper.AngleSpan);
        if (radius <= 0 || span <= 0)
            return new List<Vector3> { arc.Start, arc.End };

        // Segments by sagitta: s = R - sqrt(R^2 - (c/2)^2) <= tolerance -> c <= 2*sqrt(2*R*t - t^2)
        double maxChord = 2 * Math.Sqrt(Math.Max(0, 2 * radius * tolerance - tolerance * tolerance));
        if (maxChord <= 0)
            maxChord = tolerance; // fallback

        int minSegments = 4;
        int segmentsByChord = (int)Math.Ceiling(arcHelper.Length / Math.Max(1e-9, maxChord));
        int segments = Math.Max(minSegments, segmentsByChord);

        var list = new List<Vector3>(segments + 1) { arc.Start };
        for (int i = 1; i <= segments; i++)
        {
            double t = (double)i / segments;
            list.Add(arcHelper.Interpolate(t));
        }
        return list;
    }
}
