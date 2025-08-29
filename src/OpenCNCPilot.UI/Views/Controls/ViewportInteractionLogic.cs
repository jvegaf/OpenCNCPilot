using System;

namespace OpenCNCPilot.UI.Views.Controls;

public static class ViewportInteractionLogic
{
    public static (double rotX, double rotY) ApplyRotation(double rotX, double rotY, double deltaX, double deltaY, double sensitivity, double minX = -89.0, double maxX = 89.0)
    {
        rotY += deltaX * sensitivity;
        rotX = Math.Clamp(rotX + deltaY * sensitivity, minX, maxX);
        return (rotX, rotY);
    }

    public static (double panX, double panY) ApplyPan(double panX, double panY, double deltaX, double deltaY, double sensitivity)
    {
        panX += deltaX * sensitivity;
        panY -= deltaY * sensitivity; // screen Y grows downwards
        return (panX, panY);
    }

    public static double ApplyWheelZoom(double zoom, double wheelDelta, double stepFactor = 1.1, double minZoom = 1e-4, double maxZoom = 1e6)
    {
        // wheelDelta positive -> zoom in (reduce scale value), negative -> zoom out
        var factor = Math.Pow(stepFactor, wheelDelta / 120.0); // 120 is typical delta unit
        zoom = Math.Clamp(zoom / factor, minZoom, maxZoom);
        return zoom;
    }

    /// <summary>
    /// Anchored zoom: returns new zoom and adjusted pan so that the world point under the given screen
    /// coordinates (sx, sy) remains stationary on screen. Coordinates are in pixels; width/height are viewport size.
    /// </summary>
    public static (double zoom, double panX, double panY) ApplyWheelZoomAnchored(
        double zoom,
        double wheelDelta,
        double stepFactor,
        double panX,
        double panY,
        double sx,
        double sy,
        int width,
        int height,
        double minZoom = 1e-4,
        double maxZoom = 1e6)
    {
        double oldZoom = zoom;
        double newZoom = ApplyWheelZoom(zoom, wheelDelta, stepFactor, minZoom, maxZoom);
        if (Math.Abs(newZoom - oldZoom) < 1e-15)
            return (zoom, panX, panY);

        double hw = Math.Max(1, width) / 2.0;
        double hh = Math.Max(1, height) / 2.0;
        // View-space coordinates at cursor with old zoom
        double vxc = (sx - hw) * oldZoom;
        double vyc = (hh - sy) * oldZoom;
        // New view-space coordinates at cursor with new zoom
        double vxPrime = (sx - hw) * newZoom;
        double vyPrime = (hh - sy) * newZoom;
        // Maintain world' = v + pan => newPan = oldWorld' - v'
        double newPanX = (vxc + panX) - vxPrime;
        double newPanY = (vyc + panY) - vyPrime;
        return (newZoom, newPanX, newPanY);
    }
}
