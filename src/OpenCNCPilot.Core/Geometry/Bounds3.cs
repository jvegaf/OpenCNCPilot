using System;

namespace OpenCNCPilot.Core.Geometry;

/// <summary>
/// Axis-aligned 3D bounding box with utility operations. All units are millimeters.
/// </summary>
public readonly struct Bounds3 : IEquatable<Bounds3>
{
    public readonly Vector3 Min;
    public readonly Vector3 Max;

    public Bounds3(Vector3 min, Vector3 max)
    {
        Min = new Vector3(Math.Min(min.X, max.X), Math.Min(min.Y, max.Y), Math.Min(min.Z, max.Z));
        Max = new Vector3(Math.Max(min.X, max.X), Math.Max(min.Y, max.Y), Math.Max(min.Z, max.Z));
    }

    public double Width => Max.X - Min.X;
    public double Height => Max.Y - Min.Y;
    public double Depth => Max.Z - Min.Z;

    public Vector3 Center => new((Min.X + Max.X) / 2.0, (Min.Y + Max.Y) / 2.0, (Min.Z + Max.Z) / 2.0);

    public static Bounds3 FromPoint(Vector3 p) => new(p, p);

    public Bounds3 Expand(Vector3 p)
    {
        var min = new Vector3(Math.Min(Min.X, p.X), Math.Min(Min.Y, p.Y), Math.Min(Min.Z, p.Z));
        var max = new Vector3(Math.Max(Max.X, p.X), Math.Max(Max.Y, p.Y), Math.Max(Max.Z, p.Z));
        return new Bounds3(min, max);
    }

    public Bounds3 Pad(double amount)
    {
        var v = new Vector3(amount, amount, amount);
        return new Bounds3(Min - v, Max + v);
    }

    public static Bounds3 Union(in Bounds3 a, in Bounds3 b)
    {
        var min = new Vector3(Math.Min(a.Min.X, b.Min.X), Math.Min(a.Min.Y, b.Min.Y), Math.Min(a.Min.Z, b.Min.Z));
        var max = new Vector3(Math.Max(a.Max.X, b.Max.X), Math.Max(a.Max.Y, b.Max.Y), Math.Max(a.Max.Z, b.Max.Z));
        return new Bounds3(min, max);
    }

    public bool Equals(Bounds3 other) => Min == other.Min && Max == other.Max;
    public override bool Equals(object? obj) => obj is Bounds3 o && Equals(o);
    public override int GetHashCode() => HashCode.Combine(Min, Max);
    public override string ToString() => $"[{Min} – {Max}]";
}
