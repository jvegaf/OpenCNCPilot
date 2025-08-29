using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace OpenCNCPilot.UI.Views.Controls;

/// <summary>
/// TextBlock that highlights occurrences of a query string inside the text.
/// </summary>
public class HighlightTextBlock : TextBlock
{
    public static readonly StyledProperty<string?> TextSourceProperty =
        AvaloniaProperty.Register<HighlightTextBlock, string?>(nameof(TextSource));

    public static readonly StyledProperty<string?> QueryProperty =
        AvaloniaProperty.Register<HighlightTextBlock, string?>(nameof(Query));

    public static readonly StyledProperty<IBrush> HighlightBrushProperty =
        AvaloniaProperty.Register<HighlightTextBlock, IBrush>(nameof(HighlightBrush), new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xD5, 0x00)));

    public string? TextSource
    {
        get => GetValue(TextSourceProperty);
        set => SetValue(TextSourceProperty, value);
    }

    public string? Query
    {
        get => GetValue(QueryProperty);
        set => SetValue(QueryProperty, value);
    }

    public IBrush HighlightBrush
    {
        get => GetValue(HighlightBrushProperty);
        set => SetValue(HighlightBrushProperty, value);
    }

    static HighlightTextBlock()
    {
        TextSourceProperty.Changed.AddClassHandler<HighlightTextBlock>((s, _) => s.BuildInlines());
        QueryProperty.Changed.AddClassHandler<HighlightTextBlock>((s, _) => s.BuildInlines());
        HighlightBrushProperty.Changed.AddClassHandler<HighlightTextBlock>((s, _) => s.BuildInlines());
    }

    private void BuildInlines()
    {
        Inlines?.Clear();
        var text = TextSource ?? string.Empty;
        var q = Query;
        if (string.IsNullOrEmpty(q))
        {
            // No query: render as a single run
            Inlines?.Add(new Run(text));
            return;
        }

        // Case-insensitive highlighting of all occurrences
        var spanStart = 0;
        var comparison = StringComparison.OrdinalIgnoreCase;
        while (true)
        {
            var idx = text.IndexOf(q, spanStart, comparison);
            if (idx < 0)
            {
                if (spanStart < text.Length)
                    Inlines?.Add(new Run(text.Substring(spanStart)));
                break;
            }

            if (idx > spanStart)
            {
                Inlines?.Add(new Run(text.Substring(spanStart, idx - spanStart)));
            }

            var match = new Run(text.Substring(idx, q.Length))
            {
                Background = HighlightBrush
            };
            Inlines?.Add(match);
            spanStart = idx + q.Length;
        }
    }
}
