using System;
using System.Collections.Generic;
using OpenCNCPilot.Core.Geometry;

namespace OpenCNCPilot.Core.GCode.GCodeCommands;

public enum ArcPlane
{
    XY = 0,
    YZ = 1,
    ZX = 2
}

public enum ArcDirection
{
    CW,
    CCW
}

public class Arc : Motion
{
    public ArcPlane Plane;
    public ArcDirection Direction;
    public double U;
    public double V;

    public override double Length => Math.Abs(AngleSpan * Radius);

    public double StartAngle
    {
        get
        {
            Vector3 startInPlane = Start.RollComponents(-(int)Plane);
            double X = startInPlane.X - U;
            double Y = startInPlane.Y - V;
            return Math.Atan2(Y, X);
        }
    }

    public double EndAngle
    {
        get
        {
            Vector3 endInPlane = End.RollComponents(-(int)Plane);
            double X = endInPlane.X - U;
            double Y = endInPlane.Y - V;
            return Math.Atan2(Y, X);
        }
    }

    public double AngleSpan
    {
        get
        {
            double span = EndAngle - StartAngle;
            if (Direction == ArcDirection.CW)
            {
                if (span >= 0) span -= 2 * Math.PI;
            }
            else
            {
                if (span <= 0) span += 2 * Math.PI;
            }
            return span;
        }
    }

    public double Radius
    {
        get
        {
            Vector3 startplane = Start.RollComponents(-(int)Plane);
            Vector3 endplane = End.RollComponents(-(int)Plane);
            return (
                Math.Sqrt(Math.Pow(startplane.X - U, 2) + Math.Pow(startplane.Y - V, 2)) +
                Math.Sqrt(Math.Pow(endplane.X - U, 2) + Math.Pow(endplane.Y - V, 2))
                ) / 2;
        }
    }

    public override Vector3 Interpolate(double ratio)
    {
        double angle = StartAngle + AngleSpan * ratio;
        Vector3 onPlane = new Vector3(U + (Radius * Math.Cos(angle)), V + (Radius * Math.Sin(angle)), 0);
        double helix = (Start + (ratio * Delta)).RollComponents(-(int)Plane).Z;
        onPlane.Z = helix;
        return onPlane.RollComponents((int)Plane);
    }

    public override IEnumerable<Motion> Split(double length)
    {
        int divisions = (int)Math.Ceiling(Length / length);
        if (divisions < 1) divisions = 1;

        Vector3 lastEnd = Start;
        for (int i = 1; i <= divisions; i++)
        {
            Vector3 end = Interpolate(((double)i) / divisions);
            var immediate = new Arc
            {
                Start = lastEnd,
                End = end,
                Feed = Feed,
                Direction = Direction,
                Plane = Plane,
                U = U,
                V = V
            };
            yield return immediate;
            lastEnd = end;
        }
    }
}
