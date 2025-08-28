using System.Collections.Generic;
using OpenCNCPilot.Core.GCode.GCodeCommands;

namespace OpenCNCPilot.Core.GCode;

public interface IGCodeParser
{
    List<Command> Commands { get; }
    List<string> Warnings { get; }
    bool IgnoreAdditionalAxes { get; set; }
    void Reset();
    void ParseFile(string path);
    void Parse(IEnumerable<string> file);
}
