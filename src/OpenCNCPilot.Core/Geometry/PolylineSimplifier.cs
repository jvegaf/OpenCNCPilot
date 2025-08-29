using System;
using System.Collections.Generic;

namespace OpenCNCPilot.Core.Geometry;

/// <summary>
/// Douglas–Peucker polyline simplification for toolpaths. Works in XY plane, preserves first and last point.
/// </summary>
public static class PolylineSimplifier
{
    /// <summary>
    /// Simplifies a polyline using Douglas–Peucker with epsilon (max XY deviation in mm).
    /// Returns a new list; returns original points if shorter than 3 or epsilon <= 0.
    /// </summary>
    public static List<Vector3> Simplify(IReadOnlyList<Vector3> points, double epsilon)
    {
        if (points is null) throw new ArgumentNullException(nameof(points));
        if (points.Count < 3 || !(epsilon > 0))
            return new List<Vector3>(points);

        var keep = new bool[points.Count];
        keep[0] = true;
        keep[^1] = true;
        SimplifyRecursive(points, 0, points.Count - 1, epsilon, keep);

        var result = new List<Vector3>(points.Count);
        for (int i = 0; i < points.Count; i++)
            if (keep[i]) result.Add(points[i]);
        return result;
    }

    private static void SimplifyRecursive(IReadOnlyList<Vector3> pts, int first, int last, double eps, bool[] keep)
    {
        if (last <= first + 1) return;
        double maxDist = -1;
        int index = -1;
        var a = pts[first];
        var b = pts[last];
        for (int i = first + 1; i < last; i++)
        {
            var p = pts[i];
            double d = PerpDistanceXY(a, b, p);
            if (d > maxDist)
            {
                maxDist = d;
                index = i;
            }
        }

        if (maxDist > eps && index >= 0)
        {
            SimplifyRecursive(pts, first, index, eps, keep);
            keep[index] = true;
            SimplifyRecursive(pts, index, last, eps, keep);
        }
    }

    private static double PerpDistanceXY(in Vector3 a, in Vector3 b, in Vector3 p)
    {
        double ax = a.X, ay = a.Y;
        double bx = b.X, by = b.Y;
        double px = p.X, py = p.Y;
        double dx = bx - ax, dy = by - ay;
        double len2 = dx * dx + dy * dy;
        if (len2 <= 1e-24) // nearly a point
        {
            double dxp = px - ax, dyp = py - ay;
            return Math.Sqrt(dxp * dxp + dyp * dyp);
        }
        double t = ((px - ax) * dx + (py - ay) * dy) / len2;
        t = Math.Clamp(t, 0.0, 1.0);
        double cx = ax + t * dx, cy = ay + t * dy;
        double ex = px - cx, ey = py - cy;
        return Math.Sqrt(ex * ex + ey * ey);
    }
}
