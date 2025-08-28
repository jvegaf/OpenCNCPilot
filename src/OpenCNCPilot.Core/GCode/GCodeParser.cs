using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using OpenCNCPilot.Core.GCode.GCodeCommands;
using OpenCNCPilot.Core.Geometry;

namespace OpenCNCPilot.Core.GCode;

public enum ParseDistanceMode
{
    Absolute,
    Incremental
}

public enum ParseUnit
{
    Metric,
    Imperial
}

internal class ParserState
{
    public Vector3 Position;
    public bool[] PositionValid;
    public ArcPlane Plane;
    public double Feed;
    public ParseDistanceMode DistanceMode;
    public ParseDistanceMode ArcDistanceMode;
    public ParseUnit Unit;
    public int LastMotionMode;

    public ParserState()
    {
        Position = Vector3.MinValue;
        PositionValid = new bool[] { false, false, false };
        Plane = ArcPlane.XY;
        Feed = 0;
        DistanceMode = ParseDistanceMode.Absolute;
        ArcDistanceMode = ParseDistanceMode.Incremental;
        Unit = ParseUnit.Metric;
        LastMotionMode = -1;
    }
}

internal struct Word
{
    public char Command;
    public double Parameter;
    public override string ToString() => $"{Command}{Parameter}";
}

public class GCodeParser : IGCodeParser
{
    public bool IgnoreAdditionalAxes { get; set; } = true;
    internal ParserState State { get; private set; } = new ParserState();
    public static readonly Regex GCodeSplitter = new(@"([A-Z])\s*(\-?\d+\.?\d*)", RegexOptions.Compiled);
    private static readonly double[] MotionCommands = new double[] { 0, 1, 2, 3 };
    private readonly string _validWords = "GMXYZIJKFRSP";
    private readonly string _ignoreAxes = "ABC";

    public List<Command> Commands { get; private set; } = new();
    public List<string> Warnings { get; private set; } = new();

    public void Reset()
    {
        State = new ParserState();
        Commands = new List<Command>();
        Warnings = new List<string>();
    }

    public void ParseFile(string path)
    {
        Parse(File.ReadLines(path));
    }

    public void Parse(IEnumerable<string> file)
    {
        Reset();
        int i = 0;
        foreach (string linei in file)
        {
            i++;
            string line = CleanupLine(linei, i);
            if (string.IsNullOrWhiteSpace(line))
                continue;
            ParseLine(line.ToUpperInvariant(), i);
        }
    }

    private static string CleanupLine(string line, int lineNumber)
    {
        int commentIndex = line.IndexOf(';');
        if (commentIndex > -1)
            line = line.Remove(commentIndex);
        int start = -1;
        while ((start = line.IndexOf('(')) != -1)
        {
            int end = line.IndexOf(')');
            if (end < start)
                throw new ParseException("mismatched parentheses", lineNumber);
            line = line.Remove(start, end - start);
        }
        return line;
    }

    private void ParseLine(string line, int lineNumber)
    {
        var matches = GCodeSplitter.Matches(line);
        List<Word> words = new(matches.Count);
        foreach (Match match in matches)
        {
            words.Add(new Word
            {
                Command = match.Groups[1].Value[0],
                Parameter = double.Parse(match.Groups[2].Value, Constants.DecimalParseFormat)
            });
        }

        for (int i = 0; i < words.Count; i++)
        {
            if (words[i].Command == 'N')
            {
                words.RemoveAt(i--);
                continue;
            }

            if (IgnoreAdditionalAxes && _ignoreAxes.Contains(words[i].Command))
            {
                words.RemoveAt(i--);
                continue;
            }

            if (!_validWords.Contains(words[i].Command))
            {
                Warnings.Add($"ignoring unknown word (letter): \"{words[i]}\". (line {lineNumber})");
                words.RemoveAt(i--);
                continue;
            }

            if (words[i].Command != 'F')
                continue;

            State.Feed = words[i].Parameter;
            if (State.Unit == ParseUnit.Imperial)
                State.Feed *= 25.4;
            words.RemoveAt(i--);
        }

        for (int i = 0; i < words.Count; i++)
        {
            if (words[i].Command == 'M')
            {
                int param = (int)words[i].Parameter;
                if (param != words[i].Parameter || param < 0)
                    throw new ParseException("M code can only have positive integer parameters", lineNumber);
                Commands.Add(new MCode { Code = param, LineNumber = lineNumber });
                words.RemoveAt(i--);
                continue;
            }

            if (words[i].Command == 'S')
            {
                double param = words[i].Parameter;
                if (param < 0)
                    Warnings.Add($"spindle speed must be positive. (line {lineNumber})");
                Commands.Add(new Spindle { Speed = Math.Abs(param), LineNumber = lineNumber });
                words.RemoveAt(i--);
                continue;
            }

            if (words[i].Command == 'G' && !MotionCommands.Contains(words[i].Parameter))
            {
                double param = words[i].Parameter;
                if (param == 90) { State.DistanceMode = ParseDistanceMode.Absolute; words.RemoveAt(i--); continue; }
                if (param == 91) { State.DistanceMode = ParseDistanceMode.Incremental; words.RemoveAt(i--); continue; }
                if (param == 90.1) { State.ArcDistanceMode = ParseDistanceMode.Absolute; words.RemoveAt(i--); continue; }
                if (param == 91.1) { State.ArcDistanceMode = ParseDistanceMode.Incremental; words.RemoveAt(i--); continue; }
                if (param == 21) { State.Unit = ParseUnit.Metric; words.RemoveAt(i--); continue; }
                if (param == 20) { State.Unit = ParseUnit.Imperial; words.RemoveAt(i--); continue; }
                if (param == 17) { State.Plane = ArcPlane.XY; words.RemoveAt(i--); continue; }
                if (param == 18) { State.Plane = ArcPlane.ZX; words.RemoveAt(i--); continue; }
                if (param == 19) { State.Plane = ArcPlane.YZ; words.RemoveAt(i--); continue; }
                if (param == 4)
                {
                    if (words.Count >= 2 && words[i + 1].Command == 'P')
                    {
                        if (words[i + 1].Parameter < 0)
                            Warnings.Add($"dwell time must be positive. (line {lineNumber})");
                        Commands.Add(new Dwell { Seconds = Math.Abs(words[i + 1].Parameter), LineNumber = lineNumber });
                        words.RemoveAt(i + 1);
                        words.RemoveAt(i--);
                        continue;
                    }
                }
                Warnings.Add($"ignoring unknown command G{param}. (line {lineNumber})");
                words.RemoveAt(i--);
            }
        }

        if (words.Count == 0)
            return;

        int motionMode = State.LastMotionMode;
        if (words.First().Command == 'G')
        {
            motionMode = (int)words.First().Parameter;
            State.LastMotionMode = motionMode;
            words.RemoveAt(0);
        }
        if (motionMode < 0)
            throw new ParseException("no motion mode active", lineNumber);

        double unitMultiplier = (State.Unit == ParseUnit.Metric) ? 1 : 25.4;
        Vector3 endPos = State.Position;
        var startValid = State.PositionValid.All(isValid => isValid);
        if (State.DistanceMode == ParseDistanceMode.Incremental && !startValid)
            throw new ParseException("incremental motion is only allowed after an absolute position has been established (eg. with \"G90 G0 X0 Y0 Z5\")", lineNumber);
        if ((motionMode == 2 || motionMode == 3) && !startValid)
            throw new ParseException("arcs (G2/G3) are only allowed after an absolute position has been established (eg. with \"G90 G0 X0 Y0 Z5\")", lineNumber);

        // Find end position (XYZ)
        int inc = (State.DistanceMode == ParseDistanceMode.Incremental) ? 1 : 0;
        for (int i = 0; i < words.Count; i++)
            if (words[i].Command == 'X') { endPos.X = words[i].Parameter * unitMultiplier + inc * endPos.X; words.RemoveAt(i); State.PositionValid[0] = true; break; }
        for (int i = 0; i < words.Count; i++)
            if (words[i].Command == 'Y') { endPos.Y = words[i].Parameter * unitMultiplier + inc * endPos.Y; words.RemoveAt(i); State.PositionValid[1] = true; break; }
        for (int i = 0; i < words.Count; i++)
            if (words[i].Command == 'Z') { endPos.Z = words[i].Parameter * unitMultiplier + inc * endPos.Z; words.RemoveAt(i); State.PositionValid[2] = true; break; }

        if (motionMode != 0 && State.Feed <= 0)
            throw new ParseException("feed rate undefined", lineNumber);

        if (motionMode == 1 && !startValid)
            Warnings.Add($"a feed move is used before an absolute position is established, height maps will not be applied to this motion. (line {lineNumber})");

        if (motionMode <= 1)
        {
            if (words.Count > 0)
                Warnings.Add($"motion command must be last in line (ignoring unused words {string.Join(" ", words)} in block). (line {lineNumber})");
            var motion = new Line
            {
                Start = State.Position,
                End = endPos,
                Feed = State.Feed,
                Rapid = motionMode == 0,
                LineNumber = lineNumber,
                StartValid = startValid
            };
            State.PositionValid.CopyTo(motion.PositionValid, 0);
            Commands.Add(motion);
            State.Position = endPos;
            return;
        }

        double U, V;
        bool ijkUsed = false;
        switch (State.Plane)
        {
            default: U = State.Position.X; V = State.Position.Y; break;
            case ArcPlane.YZ: U = State.Position.Y; V = State.Position.Z; break;
            case ArcPlane.ZX: U = State.Position.Z; V = State.Position.X; break;
        }

        int arcInc = (State.ArcDistanceMode == ParseDistanceMode.Incremental) ? 1 : 0;
        for (int i = 0; i < words.Count; i++)
        {
            if (words[i].Command != 'I') continue;
            switch (State.Plane)
            {
                case ArcPlane.XY: U = words[i].Parameter * unitMultiplier + arcInc * State.Position.X; break;
                case ArcPlane.YZ: throw new ParseException("current plane is YZ, I word is invalid", lineNumber);
                case ArcPlane.ZX: V = words[i].Parameter * unitMultiplier + arcInc * State.Position.X; break;
            }
            ijkUsed = true; words.RemoveAt(i); break;
        }
        for (int i = 0; i < words.Count; i++)
        {
            if (words[i].Command != 'J') continue;
            switch (State.Plane)
            {
                case ArcPlane.XY: V = words[i].Parameter * unitMultiplier + arcInc * State.Position.Y; break;
                case ArcPlane.YZ: U = words[i].Parameter * unitMultiplier + arcInc * State.Position.Y; break;
                case ArcPlane.ZX: throw new ParseException("current plane is ZX, J word is invalid", lineNumber);
            }
            ijkUsed = true; words.RemoveAt(i); break;
        }
        for (int i = 0; i < words.Count; i++)
        {
            if (words[i].Command != 'K') continue;
            switch (State.Plane)
            {
                case ArcPlane.XY: throw new ParseException("current plane is XY, K word is invalid", lineNumber);
                case ArcPlane.YZ: V = words[i].Parameter * unitMultiplier + arcInc * State.Position.Z; break;
                case ArcPlane.ZX: U = words[i].Parameter * unitMultiplier + arcInc * State.Position.Z; break;
            }
            ijkUsed = true; words.RemoveAt(i); break;
        }

        for (int i = 0; i < words.Count; i++)
        {
            if (words[i].Command != 'R') continue;
            if (ijkUsed) throw new ParseException("both IJK and R notation used", lineNumber);
            if (State.Position == endPos) throw new ParseException("arcs in R-notation must have non-coincident start and end points", lineNumber);
            double radius = words[i].Parameter * unitMultiplier;
            if (radius == 0) throw new ParseException("radius can't be zero", lineNumber);
            double A, B;
            switch (State.Plane)
            {
                default: A = endPos.X; B = endPos.Y; break;
                case ArcPlane.YZ: A = endPos.Y; B = endPos.Z; break;
                case ArcPlane.ZX: A = endPos.Z; B = endPos.X; break;
            }
            A -= U; B -= V;
            double h_x2_div_d = 4.0 * (radius * radius) - (A * A + B * B);
            if (h_x2_div_d < 0) throw new ParseException("arc radius too small to reach both ends", lineNumber);
            h_x2_div_d = -Math.Sqrt(h_x2_div_d) / Math.Sqrt(A * A + B * B);
            if ((motionMode == 3) ^ (radius < 0)) h_x2_div_d = -h_x2_div_d;
            U += 0.5 * (A - (B * h_x2_div_d));
            V += 0.5 * (B + (A * h_x2_div_d));
            words.RemoveAt(i); break;
        }

        if (words.Count > 0)
            Warnings.Add($"motion command must be last in line (ignoring unused words {string.Join(" ", words)} in block). (line {lineNumber})");

        var arc = new Arc
        {
            Start = State.Position,
            End = endPos,
            Feed = State.Feed,
            Direction = (motionMode == 2) ? ArcDirection.CW : ArcDirection.CCW,
            U = U,
            V = V,
            LineNumber = lineNumber,
            Plane = State.Plane
        };
        Commands.Add(arc);
        State.Position = endPos;
    }
}
