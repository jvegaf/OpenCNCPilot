using System.Collections.Generic;
using OpenCNCPilot.Core.Geometry;

namespace OpenCNCPilot.Core.GCode.GCodeCommands;

public abstract class Motion : Command
{
    public Vector3 Start;
    public Vector3 End;
    public double Feed;

    public Vector3 Delta => End - Start;

    public abstract double Length { get; }
    public abstract Vector3 Interpolate(double ratio);
    public abstract IEnumerable<Motion> Split(double length);
}
