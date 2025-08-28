using System;

namespace OpenCNCPilot.UI.Views.Controls;

public static class ViewportMath
{
    // Simple 3D -> 2D transform applying rotations around X and Y (degrees), then pan.
    // Returns projected XY in world units; viewport scaling is applied elsewhere.
    public static (double X, double Y) TransformWorldToView(double x, double y, double z, double rotXDeg, double rotYDeg, double panX, double panY)
    {
        double rx = rotXDeg * Math.PI / 180.0;
        double ry = rotYDeg * Math.PI / 180.0;

        // Rotate around X
        var cy = Math.Cos(rx);
        var sy = Math.Sin(rx);
        double y1 = y * cy - z * sy;
        double z1 = y * sy + z * cy;

        // Rotate around Y
        var cx = Math.Cos(ry);
        var sx = Math.Sin(ry);
        double x2 = x * cx + z1 * sx;
        double z2 = -x * sx + z1 * cx;

    // Orthographic projection XY, then pan (subtract pan offsets)
    return (x2 - panX, y1 - panY);
    }
}
