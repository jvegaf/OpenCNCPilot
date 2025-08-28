using System;
using System.Collections.Generic;
using System.Linq;
using OpenCNCPilot.Core.Geometry;

namespace OpenCNCPilot.Core.GCode.GCodeCommands;

public class Line : Motion
{
    public bool Rapid;
    public bool[] PositionValid = new bool[] { false, false, false };
    public bool StartValid = false;

    public override double Length
    {
        get
        {
            if (!StartValid || PositionValid.Any(v => !v))
                return 0;
            return Delta.Magnitude;
        }
    }

    public override Vector3 Interpolate(double ratio)
    {
        return Start + Delta * ratio;
    }

    public override IEnumerable<Motion> Split(double length)
    {
        if (Rapid || PositionValid.Any(isValid => !isValid) || !StartValid)
        {
            yield return this;
            yield break;
        }

        int divisions = (int)Math.Ceiling(Length / length);
        if (divisions < 1) divisions = 1;

        Vector3 lastEnd = Start;
        for (int i = 1; i <= divisions; i++)
        {
            Vector3 end = Interpolate(((double)i) / divisions);

            var immediate = new Line
            {
                Start = lastEnd,
                End = end,
                Feed = Feed,
                PositionValid = new bool[] { true, true, true },
                StartValid = true
            };

            yield return immediate;
            lastEnd = end;
        }
    }
}
