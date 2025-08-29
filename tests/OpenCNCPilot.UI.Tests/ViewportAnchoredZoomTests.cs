using OpenCNCPilot.UI.Views.Controls;
using Xunit;

namespace OpenCNCPilot.UI.Tests;

[Trait("Category","UI-VM")]
public class ViewportAnchoredZoomTests
{
    [Fact]
    public void AnchoredZoom_Keeps_Cursor_Point_Screen_Stationary()
    {
        // Given some initial zoom/pan and a cursor at screen center
        double zoom = 1.0;
        double panX = 100; // arbitrary
        double panY = -50; // arbitrary
        int width = 1000, height = 800;
        double sx = width / 2.0, sy = height / 2.0; // center

        // When zooming in (wheelDelta positive reduces logical zoom value)
        var (nz, npx, npy) = ViewportInteractionLogic.ApplyWheelZoomAnchored(zoom, 120, 1.1, panX, panY, sx, sy, width, height);

        // Then pan adjusts but screen center remains the same after transform
        // At screen center, view-space coords are (0,0), so world' = pan. New pan should equal old pan (no drift)
        Assert.Equal(panX, npx, 6);
        Assert.Equal(panY, npy, 6);
        Assert.True(nz < zoom); // zoom-in reduces scale value

        // For a non-center cursor, pan should change
        sx = width * 0.75; sy = height * 0.25;
        var (nz2, npx2, npy2) = ViewportInteractionLogic.ApplyWheelZoomAnchored(1.0, 120, 1.1, panX, panY, sx, sy, width, height);
        Assert.NotEqual(panX, npx2);
        Assert.NotEqual(panY, npy2);
    }
}
