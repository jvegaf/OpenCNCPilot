using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace OpenCNCPilot.UI.Converters;

public class BooleanToColorConverter : IValueConverter
{
    public static readonly BooleanToColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue && parameter is string colorString)
        {
            var colors = colorString.Split('|');
            if (colors.Length == 2)
            {
                var trueColor = colors[0].Trim();
                var falseColor = colors[1].Trim();

                var colorName = boolValue ? trueColor : falseColor;

                return colorName.ToLowerInvariant() switch
                {
                    "green" => Colors.Green,
                    "red" => Colors.Red,
                    "blue" => Colors.Blue,
                    "yellow" => Colors.Yellow,
                    "orange" => Colors.Orange,
                    "gray" or "grey" => Colors.Gray,
                    _ => Color.Parse(colorName)
                };
            }
        }

        return Colors.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
