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
}
