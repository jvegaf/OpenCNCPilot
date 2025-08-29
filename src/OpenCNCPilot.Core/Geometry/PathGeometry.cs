using System;
using System.Collections.Generic;
using OpenCNCPilot.Core.GCode.GCodeCommands;

namespace OpenCNCPilot.Core.Geometry;

public enum MovementType
{
    Rapid,
    Cut
}

public readonly struct LineSegment
{
    public readonly Vector3 Start;
    public readonly Vector3 End;
    public readonly MovementType Type;

    public LineSegment(Vector3 start, Vector3 end, MovementType type)
    {
        Start = start;
        End = end;
        Type = type;
    }
}

public readonly struct ArcSegment
{
    public readonly Vector3 Start;
    public readonly Vector3 End;
    public readonly MovementType Type;
    public readonly ArcPlane Plane;
    public readonly ArcDirection Direction;
    public readonly double U;
    public readonly double V;

    public ArcSegment(Vector3 start, Vector3 end, MovementType type, ArcPlane plane, ArcDirection direction, double u, double v)
    {
        Start = start;
        End = end;
        Type = type;
        Plane = plane;
        Direction = direction;
        U = u;
        V = v;
    }
}

public sealed class GeometryData
{
    public IReadOnlyList<LineSegment> Lines { get; }
    public IReadOnlyList<ArcSegment> Arcs { get; }
    public Bounds3 Bounds { get; }
    public int RapidCount { get; }
    public int CutCount { get; }

    public GeometryData(IReadOnlyList<LineSegment> lines, IReadOnlyList<ArcSegment> arcs, Bounds3 bounds)
    {
        Lines = lines;
        Arcs = arcs;
        Bounds = bounds;
        int rapid = 0, cut = 0;
        foreach (var l in lines) { if (l.Type == MovementType.Rapid) rapid++; else cut++; }
        foreach (var a in arcs) { if (a.Type == MovementType.Rapid) rapid++; else cut++; }
        RapidCount = rapid;
        CutCount = cut;
    }
}
