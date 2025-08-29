using OpenCNCPilot.Core.Geometry;

namespace OpenCNCPilot.Core.GCode;

public interface IGCodePathBuilder
{
    GeometryData Build(GCodeParser parser);
}
