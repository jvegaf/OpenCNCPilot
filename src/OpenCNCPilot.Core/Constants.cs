using System;
using System.Globalization;

namespace OpenCNCPilot.Core;

/// <summary>
/// Core constants for OpenCNCPilot application
/// </summary>
public static class Constants
{
    public static NumberFormatInfo DecimalParseFormat = new NumberFormatInfo() { NumberDecimalSeparator = "." };

    public static NumberFormatInfo DecimalOutputFormat =>
        new NumberFormatInfo() { NumberDecimalSeparator = ".", NumberDecimalDigits = 3 };

    // File filters for different file types
    public const string FileFilterGCode = "GCode|*.tap;*.nc;*.ngc|All Files|*.*";
    public const string FileFilterHeightMap = "Height Maps|*.hmap|All Files|*.*";
    public const string FileFilterSettings = "Grbl settings|*.txt;*.gbl;*.nc;*.ngc|All Files|*.*";

    public const string LogFile = "log.txt";

    public static readonly char[] NewLines = { '\n', '\r' };

    public static readonly Version MinimumGrblVersion = new Version(1, 1, (int)'f');
}
