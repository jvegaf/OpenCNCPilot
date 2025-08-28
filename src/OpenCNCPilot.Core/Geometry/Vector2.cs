using System;
using System.Globalization;

namespace OpenCNCPilot.Core.Geometry;

/// <summary>
/// 2D vector implementation for cross-platform use
/// </summary>
public struct Vector2 : IEquatable<Vector2>
{
    private double x;
    private double y;

    public Vector2(double x, double y)
    {
        // Pre-initialisation initialisation for struct
        this.x = 0;
        this.y = 0;

        // Initialisation
        X = x;
        Y = y;
    }

    public double X
    {
        get { return x; }
        set { x = value; }
    }

    public double Y
    {
        get { return y; }
        set { y = value; }
    }

    public double Magnitude => Math.Sqrt(X * X + Y * Y);

    public static Vector2 operator +(Vector2 v1, Vector2 v2)
    {
        return new Vector2(v1.X + v2.X, v1.Y + v2.Y);
    }

    public static Vector2 operator -(Vector2 v1, Vector2 v2)
    {
        return new Vector2(v1.X - v2.X, v1.Y - v2.Y);
    }

    public static Vector2 operator *(Vector2 v1, double s2)
    {
        return new Vector2(v1.X * s2, v1.Y * s2);
    }

    public static Vector2 operator *(double s1, Vector2 v2)
    {
        return v2 * s1;
    }

    public static Vector2 operator /(Vector2 v1, double s2)
    {
        return new Vector2(v1.X / s2, v1.Y / s2);
    }

    public static Vector2 operator -(Vector2 v1)
    {
        return new Vector2(-v1.X, -v1.Y);
    }

    public static bool operator ==(Vector2 v1, Vector2 v2)
    {
        return Math.Abs(v1.X - v2.X) <= EqualityTolerance &&
               Math.Abs(v1.Y - v2.Y) <= EqualityTolerance;
    }

    public static bool operator !=(Vector2 v1, Vector2 v2)
    {
        return !(v1 == v2);
    }

    public bool Equals(Vector2 other)
    {
        return other == this;
    }

    public override bool Equals(object? obj)
    {
        if (obj is Vector2 otherVector)
        {
            return otherVector == this;
        }
        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(X, Y);
    }

    public override string ToString()
    {
        return $"({X}, {Y})";
    }

    public const double EqualityTolerance = double.Epsilon;
}
