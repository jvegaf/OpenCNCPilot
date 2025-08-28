using System;
using System.Runtime.Serialization;

namespace OpenCNCPilot.Core.GCode;

[Serializable]
public class ParseException : Exception
{
    public int Line { get; }
    public string Error { get; }

    public ParseException(string error, int line)
    {
        Line = line;
        Error = error;
    }

    public ParseException(string error, int line, Exception? inner)
        : base(error, inner)
    {
        Line = line;
        Error = error;
    }

    [Obsolete("Formatter-based serialization is obsolete in .NET 8+")]
    protected ParseException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        Line = info.GetInt32(nameof(Line));
        Error = info.GetString(nameof(Error)) ?? string.Empty;
    }

    [Obsolete("Formatter-based serialization is obsolete in .NET 8+")]
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
#pragma warning disable SYSLIB0051 // Formatter-based serialization is obsolete
        base.GetObjectData(info, context);
#pragma warning restore SYSLIB0051
        info.AddValue(nameof(Line), Line);
        info.AddValue(nameof(Error), Error);
    }

    public override string Message => $"Error while reading GCode File in Line {Line}:\n{Error}";
}
