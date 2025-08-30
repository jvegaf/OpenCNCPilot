using Avalonia.Media;
using OpenCNCPilot.UI.Views.Controls;
using Xunit;

namespace OpenCNCPilot.UI.Tests;

[Trait("Category","UI-VM")]
public class HighlightTextBlockTests
{
    [Fact]
    public void Builds_Inlines_With_Highlights()
    {
        var h = new HighlightTextBlock
        {
            TextSource = "G0 X0 Y0 ; move fast",
            Query = "x0",
            HighlightBrush = new SolidColorBrush(Colors.Yellow)
        };

        // Trigger building
        typeof(HighlightTextBlock).GetMethod("BuildInlines", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(h, null);

        Assert.NotNull(h.Inlines);
        Assert.True(h.Inlines!.Count >= 3); // prefix, match, suffix
    }
}
