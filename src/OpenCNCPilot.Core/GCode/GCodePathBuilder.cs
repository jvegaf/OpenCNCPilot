using System;
using System.Collections.Generic;
using OpenCNCPilot.Core.GCode.GCodeCommands;
using OpenCNCPilot.Core.Geometry;

namespace OpenCNCPilot.Core.GCode;

public class GCodePathBuilder : IGCodePathBuilder
{
    public GeometryData Build(GCodeParser parser)
    {
        if (parser is null) throw new ArgumentNullException(nameof(parser));
        var lines = new List<LineSegment>();
        var arcs = new List<ArcSegment>();

        double minX = double.PositiveInfinity, minY = double.PositiveInfinity, minZ = double.PositiveInfinity;
        double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity, maxZ = double.NegativeInfinity;
        static bool IsFinite(Vector3 v)
            => double.IsFinite(v.X) && double.IsFinite(v.Y) && double.IsFinite(v.Z)
               && !(v.X == double.MinValue || v.Y == double.MinValue || v.Z == double.MinValue)
               && !(v.X == double.MaxValue || v.Y == double.MaxValue || v.Z == double.MaxValue);

        void Accumulate(Vector3 p)
        {
            if (!IsFinite(p)) return;
            if (p.X < minX) minX = p.X; if (p.X > maxX) maxX = p.X;
            if (p.Y < minY) minY = p.Y; if (p.Y > maxY) maxY = p.Y;
            if (p.Z < minZ) minZ = p.Z; if (p.Z > maxZ) maxZ = p.Z;
        }

        foreach (var cmd in parser.Commands)
        {
            switch (cmd)
            {
                case Line l:
                {
                    var type = l.Rapid ? MovementType.Rapid : MovementType.Cut;
                    var seg = new LineSegment(l.Start, l.End, type);
                    lines.Add(seg);
                    // Only accumulate start if it is valid; end is always valid for motion commands.
                    if (l.StartValid && l.PositionValid is { Length: 3 } && l.PositionValid[0] && l.PositionValid[1] && l.PositionValid[2])
                        Accumulate(l.Start);
                    Accumulate(l.End);
                    break;
                }
                case Arc a:
                {
                    var type = MovementType.Cut; // rapids are never arcs in our parser
                    var seg = new ArcSegment(a.Start, a.End, type, a.Plane, a.Direction, a.U, a.V);
                    arcs.Add(seg);
                    Accumulate(a.Start);
                    Accumulate(a.End);
                    break;
                }
                default:
                    break; // ignore non-motion
            }
        }

        Bounds3 finalBounds;
        if (double.IsInfinity(minX) || double.IsInfinity(minY) || double.IsInfinity(minZ) ||
            double.IsInfinity(maxX) || double.IsInfinity(maxY) || double.IsInfinity(maxZ))
        {
            finalBounds = new Bounds3(Vector3.Origin, Vector3.Origin);
        }
        else
        {
            finalBounds = new Bounds3(new Vector3(minX, minY, minZ), new Vector3(maxX, maxY, maxZ));
        }
        return new GeometryData(lines, arcs, finalBounds);
    }
}
