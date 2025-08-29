using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace OpenCNCPilot.UI.Converters;

public sealed class ArgbUintToHexConverter : IValueConverter
{
    public static readonly ArgbUintToHexConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is uint argb)
        {
            return $"#{argb:X8}"; // #AARRGGBB
        }
        return value is null ? "#FF000000" : value.ToString();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s)
        {
            s = s.Trim();
            if (string.IsNullOrWhiteSpace(s))
                return new BindingNotification(new FormatException("Empty color string"), BindingErrorType.Error);

            if (s.StartsWith("#", StringComparison.Ordinal)) s = s[1..];
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s[2..];

            // Support RRGGBB (assume FF alpha) or AARRGGBB
            if (s.Length == 6)
                s = "FF" + s;
            if (s.Length != 8)
                return new BindingNotification(new FormatException("Expected 6 or 8 hex digits"), BindingErrorType.Error);

            if (uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var argb))
                return argb;

            return new BindingNotification(new FormatException("Invalid hex color"), BindingErrorType.Error);
        }

        return new BindingNotification(new InvalidCastException("Expected string"), BindingErrorType.Error);
    }
}
