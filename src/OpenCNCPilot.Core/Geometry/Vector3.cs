using System;
using System.Globalization;

namespace OpenCNCPilot.Core.Geometry;

/// <summary>
/// 3D vector implementation for cross-platform use (migrated from WPF version)
/// Removed WPF dependencies: Point3D, Vector3D references
/// </summary>
public struct Vector3 : IComparable, IComparable<Vector3>, IEquatable<Vector3>, IFormattable
{
    #region Class Variables

    private double x;
    private double y;
    private double z;

    #endregion

    #region Constructors

    public Vector3(double x, double y, double z)
    {
        // Pre-initialisation initialisation for struct
        this.x = 0;
        this.y = 0;
        this.z = 0;

        // Initialisation
        X = x;
        Y = y;
        Z = z;
    }

    public Vector3(double[] xyz)
    {
        x = 0;
        y = 0;
        z = 0;
        Array = xyz;
    }

    public Vector3(Vector3 v1)
    {
        x = 0;
        y = 0;
        z = 0;

        X = v1.X;
        Y = v1.Y;
        Z = v1.Z;
    }

    #endregion

    #region Accessors & Mutators

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

    public double Z
    {
        get { return z; }
        set { z = value; }
    }

    public double Magnitude
    {
        get => Math.Sqrt(SumComponentSqrs());
        set
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException("value", value, "The magnitude of a Vector3 must be positive");

            if (this == new Vector3(0, 0, 0))
                throw new ArgumentException("Cannot change the magnitude of Vector3(0,0,0)", "this");

            this = this * (value / Magnitude);
        }
    }

    public double[] Array
    {
        get { return new double[] { x, y, z }; }
        set
        {
            if (value.Length == 3)
            {
                x = value[0];
                y = value[1];
                z = value[2];
            }
            else
            {
                throw new ArgumentException("Array must contain exactly three components (x,y,z)");
            }
        }
    }

    public double this[int index]
    {
        get
        {
            return index switch
            {
                0 => X,
                1 => Y,
                2 => Z,
                _ => throw new ArgumentException("Array must contain exactly three components (x,y,z)", "index")
            };
        }
        set
        {
            switch (index)
            {
                case 0: X = value; break;
                case 1: Y = value; break;
                case 2: Z = value; break;
                default: throw new ArgumentException("Array must contain exactly three components (x,y,z)", "index");
            }
        }
    }

    #endregion

    #region Operators

    public static Vector3 operator +(Vector3 v1, Vector3 v2)
    {
        return new Vector3(v1.X + v2.X, v1.Y + v2.Y, v1.Z + v2.Z);
    }

    public static Vector3 operator -(Vector3 v1, Vector3 v2)
    {
        return new Vector3(v1.X - v2.X, v1.Y - v2.Y, v1.Z - v2.Z);
    }

    public static Vector3 operator *(Vector3 v1, double s2)
    {
        return new Vector3(v1.X * s2, v1.Y * s2, v1.Z * s2);
    }

    public static Vector3 operator *(double s1, Vector3 v2)
    {
        return v2 * s1;
    }

    public static Vector3 operator /(Vector3 v1, double s2)
    {
        return new Vector3(v1.X / s2, v1.Y / s2, v1.Z / s2);
    }

    public static Vector3 operator -(Vector3 v1)
    {
        return new Vector3(-v1.X, -v1.Y, -v1.Z);
    }

    public static Vector3 operator +(Vector3 v1)
    {
        return new Vector3(+v1.X, +v1.Y, +v1.Z);
    }

    public static bool operator <(Vector3 v1, Vector3 v2)
    {
        return v1.SumComponentSqrs() < v2.SumComponentSqrs();
    }

    public static bool operator >(Vector3 v1, Vector3 v2)
    {
        return v1.SumComponentSqrs() > v2.SumComponentSqrs();
    }

    public static bool operator <=(Vector3 v1, Vector3 v2)
    {
        return v1.SumComponentSqrs() <= v2.SumComponentSqrs();
    }

    public static bool operator >=(Vector3 v1, Vector3 v2)
    {
        return v1.SumComponentSqrs() >= v2.SumComponentSqrs();
    }

    public static bool operator ==(Vector3 v1, Vector3 v2)
    {
        return Math.Abs(v1.X - v2.X) <= EqualityTolerance &&
               Math.Abs(v1.Y - v2.Y) <= EqualityTolerance &&
               Math.Abs(v1.Z - v2.Z) <= EqualityTolerance;
    }

    public static bool operator !=(Vector3 v1, Vector3 v2)
    {
        return !(v1 == v2);
    }

    #endregion

    #region Functions

    public static Vector3 CrossProduct(Vector3 v1, Vector3 v2)
    {
        return new Vector3(
            v1.Y * v2.Z - v1.Z * v2.Y,
            v1.Z * v2.X - v1.X * v2.Z,
            v1.X * v2.Y - v1.Y * v2.X
        );
    }

    public Vector3 CrossProduct(Vector3 other)
    {
        return CrossProduct(this, other);
    }

    public static double DotProduct(Vector3 v1, Vector3 v2)
    {
        return v1.X * v2.X + v1.Y * v2.Y + v1.Z * v2.Z;
    }

    public double DotProduct(Vector3 other)
    {
        return DotProduct(this, other);
    }

    public static Vector3 Normalize(Vector3 v1)
    {
        if (v1.Magnitude == 0)
        {
            throw new DivideByZeroException("Cannot normalize a vector when its magnitude is zero");
        }

        double inverse = 1 / v1.Magnitude;
        return new Vector3(v1.X * inverse, v1.Y * inverse, v1.Z * inverse);
    }

    public void Normalize()
    {
        this = Normalize(this);
    }

    public static double Distance(Vector3 v1, Vector3 v2)
    {
        return Math.Sqrt((v1.X - v2.X) * (v1.X - v2.X) +
                        (v1.Y - v2.Y) * (v1.Y - v2.Y) +
                        (v1.Z - v2.Z) * (v1.Z - v2.Z));
    }

    public double Distance(Vector3 other)
    {
        return Distance(this, other);
    }

    public static Vector3 ElementwiseMax(Vector3 v1, Vector3 v2)
    {
        return new Vector3(
            Math.Max(v1.X, v2.X),
            Math.Max(v1.Y, v2.Y),
            Math.Max(v1.Z, v2.Z)
        );
    }

    public static Vector3 ElementwiseMin(Vector3 v1, Vector3 v2)
    {
        return new Vector3(
            Math.Min(v1.X, v2.X),
            Math.Min(v1.Y, v2.Y),
            Math.Min(v1.Z, v2.Z)
        );
    }

    #endregion

    #region Component Operations

    public static double SumComponents(Vector3 v1)
    {
        return v1.X + v1.Y + v1.Z;
    }

    public double SumComponents()
    {
        return SumComponents(this);
    }

    public static double SumComponentSqrs(Vector3 v1)
    {
        return v1.X * v1.X + v1.Y * v1.Y + v1.Z * v1.Z;
    }

    public double SumComponentSqrs()
    {
        return SumComponentSqrs(this);
    }

    #endregion

    #region Standard Functions

    public override string ToString()
    {
        return ToString(null, null);
    }

    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        if (string.IsNullOrEmpty(format))
            return $"({X}, {Y}, {Z})";

        char firstChar = format[0];
        string? remainder = format.Length > 1 ? format[1..] : null;

        return firstChar switch
        {
            'x' => X.ToString(remainder, formatProvider),
            'y' => Y.ToString(remainder, formatProvider),
            'z' => Z.ToString(remainder, formatProvider),
            _ => $"({X.ToString(format, formatProvider)}, {Y.ToString(format, formatProvider)}, {Z.ToString(format, formatProvider)})"
        };
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(X, Y, Z);
    }

    public override bool Equals(object? obj)
    {
        if (obj is Vector3 otherVector)
        {
            return otherVector == this;
        }
        return false;
    }

    public bool Equals(Vector3 other)
    {
        return other == this;
    }

    public int CompareTo(Vector3 other)
    {
        if (this < other) return -1;
        if (this > other) return 1;
        return 0;
    }

    public int CompareTo(object? obj)
    {
        if (obj is Vector3 vector)
        {
            return CompareTo(vector);
        }
        throw new ArgumentException("Cannot compare a Vector3 to a non-Vector3", nameof(obj));
    }

    #endregion

    #region Utility Methods

    public Vector2 GetXY()
    {
        return new Vector2(X, Y);
    }

    public Vector3 RollComponents(int turns)
    {
        Vector3 roll = new Vector3();
        for (int i = 0; i < 3; i++)
        {
            roll[i] = this[(i - turns + 300) % 3];
        }
        return roll;
    }

    public static Vector3 Parse(string input)
    {
        string[] components = input.Split(',');

        if (components.Length != 3)
            throw new FormatException("String does not contain 3 components");

        double[] values = new double[3];
        for (int i = 0; i < 3; i++)
            values[i] = double.Parse(components[i], CultureInfo.InvariantCulture);

        return new Vector3(values);
    }

    #endregion

    #region Cartesian Vectors

    public static readonly Vector3 Origin = new Vector3(0, 0, 0);
    public static readonly Vector3 XAxis = new Vector3(1, 0, 0);
    public static readonly Vector3 YAxis = new Vector3(0, 1, 0);
    public static readonly Vector3 ZAxis = new Vector3(0, 0, 1);

    #endregion

    #region Constants

    public const double EqualityTolerance = double.Epsilon;
    public static readonly Vector3 MinValue = new Vector3(double.MinValue, double.MinValue, double.MinValue);
    public static readonly Vector3 MaxValue = new Vector3(double.MaxValue, double.MaxValue, double.MaxValue);
    public static readonly Vector3 Epsilon = new Vector3(double.Epsilon, double.Epsilon, double.Epsilon);

    #endregion
}