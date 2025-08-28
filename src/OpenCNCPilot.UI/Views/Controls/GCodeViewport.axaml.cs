using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using OpenCNCPilot.Core.GCode.GCodeCommands;
using SkiaSharp;

namespace OpenCNCPilot.UI.Views.Controls;

public partial class GCodeViewport : OpenGlControlBase
{
    private GRGlInterface? _skiaGl;
    private GRContext? _skiaContext;
    /// <summary>
    /// Logical scale for XY projection. Interpreted as millimeters per half-screen unit
    /// (i.e., smaller values zoom in, larger values zoom out). Defaults to 1.0.
    /// </summary>
    public static readonly StyledProperty<double> ZoomProperty =
        AvaloniaProperty.Register<GCodeViewport, double>(nameof(Zoom), 1.0);

    public static readonly StyledProperty<double> RotationXProperty =
        AvaloniaProperty.Register<GCodeViewport, double>(nameof(RotationX), 30.0);

    public static readonly StyledProperty<double> RotationYProperty =
        AvaloniaProperty.Register<GCodeViewport, double>(nameof(RotationY), 45.0);

    public static readonly StyledProperty<double> PanXProperty =
        AvaloniaProperty.Register<GCodeViewport, double>(nameof(PanX), 0.0);

    public static readonly StyledProperty<double> PanYProperty =
        AvaloniaProperty.Register<GCodeViewport, double>(nameof(PanY), 0.0);

    public double Zoom
    {
        get => GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    public double RotationX
    {
        get => GetValue(RotationXProperty);
        set => SetValue(RotationXProperty, value);
    }

    public double RotationY
    {
        get => GetValue(RotationYProperty);
        set => SetValue(RotationYProperty, value);
    }

    public double PanX
    {
        get => GetValue(PanXProperty);
        set => SetValue(PanXProperty, value);
    }

    public double PanY
    {
        get => GetValue(PanYProperty);
        set => SetValue(PanYProperty, value);
    }

    /// <summary>
    /// Toolpath commands to render in the viewport (XY projection). Arcs are drawn using
    /// a simple polyline approximation; future versions may adapt segment count by curvature.
    /// </summary>
    public static readonly StyledProperty<IReadOnlyList<Command>?> CommandsProperty =
        AvaloniaProperty.Register<GCodeViewport, IReadOnlyList<Command>?>(nameof(Commands));

    public IReadOnlyList<Command>? Commands
    {
        get => GetValue(CommandsProperty);
        set => SetValue(CommandsProperty, value);
    }

    /// <summary>
    /// Bumping this value triggers an autofit operation using current commands' bounds.
    /// </summary>
    public static readonly StyledProperty<int> FitRequestIdProperty =
        AvaloniaProperty.Register<GCodeViewport, int>(nameof(FitRequestId));

    public int FitRequestId
    {
        get => GetValue(FitRequestIdProperty);
        set => SetValue(FitRequestIdProperty, value);
    }

    private Point? _lastPointer;
    private bool _isRotating;
    private bool _isPanning;
    public static readonly StyledProperty<double> RotateSensitivityProperty =
        AvaloniaProperty.Register<GCodeViewport, double>(nameof(RotateSensitivity), 0.3);

    public static readonly StyledProperty<double> PanSensitivityProperty =
        AvaloniaProperty.Register<GCodeViewport, double>(nameof(PanSensitivity), 0.02);

    public static readonly StyledProperty<double> ZoomStepFactorProperty =
        AvaloniaProperty.Register<GCodeViewport, double>(nameof(ZoomStepFactor), 1.1);

    public double RotateSensitivity
    {
        get => GetValue(RotateSensitivityProperty);
        set => SetValue(RotateSensitivityProperty, value);
    }

    public double PanSensitivity
    {
        get => GetValue(PanSensitivityProperty);
        set => SetValue(PanSensitivityProperty, value);
    }

    public double ZoomStepFactor
    {
        get => GetValue(ZoomStepFactorProperty);
        set => SetValue(ZoomStepFactorProperty, value);
    }

    // Overlays
    public static readonly StyledProperty<bool> ShowGridProperty =
        AvaloniaProperty.Register<GCodeViewport, bool>(nameof(ShowGrid), true);
    public static readonly StyledProperty<bool> ShowOriginProperty =
        AvaloniaProperty.Register<GCodeViewport, bool>(nameof(ShowOrigin), true);
    public static readonly StyledProperty<bool> ShowBoundsProperty =
        AvaloniaProperty.Register<GCodeViewport, bool>(nameof(ShowBounds), false);
    public static readonly StyledProperty<double> GridMinPixelStepProperty =
        AvaloniaProperty.Register<GCodeViewport, double>(nameof(GridMinPixelStep), 30.0);

    public bool ShowGrid
    {
        get => GetValue(ShowGridProperty);
        set => SetValue(ShowGridProperty, value);
    }
    public bool ShowOrigin
    {
        get => GetValue(ShowOriginProperty);
        set => SetValue(ShowOriginProperty, value);
    }
    public bool ShowBounds
    {
        get => GetValue(ShowBoundsProperty);
        set => SetValue(ShowBoundsProperty, value);
    }
    public double GridMinPixelStep
    {
        get => GetValue(GridMinPixelStepProperty);
        set => SetValue(GridMinPixelStepProperty, value);
    }

    private void AutoFit()
    {
        if (Commands is null || Commands.Count == 0)
            return;
        int width = Math.Max(1, (int)Bounds.Width);
        int height = Math.Max(1, (int)Bounds.Height);
        var (zoom, panX, panY) = ViewportAutoFit.Compute(Commands, width, height, RotationX, RotationY);
        Zoom = zoom;
        PanX = panX;
        PanY = panY;
    }

    protected override void OnOpenGlInit(GlInterface gl)
    {
        try
        {
            gl.ClearColor(0.09f, 0.09f, 0.12f, 1f);

            _skiaGl = GRGlInterface.Create(name => gl.GetProcAddress(name));
            _skiaContext = _skiaGl != null ? GRContext.CreateGl(_skiaGl) : null;
        }
        catch (Exception)
        {
            // In headless/CI environments, GL might be unavailable
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _lastPointer = e.GetPosition(this);
        var properties = e.GetCurrentPoint(this).Properties;
        var mods = e.KeyModifiers;
        _isRotating = properties.IsRightButtonPressed || mods.HasFlag(KeyModifiers.Alt);
        _isPanning = properties.IsMiddleButtonPressed || mods.HasFlag(KeyModifiers.Shift);
        e.Pointer.Capture(this);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _isRotating = false;
        _isPanning = false;
        _lastPointer = null;
        e.Pointer.Capture(null);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_lastPointer is null)
            return;

        var pos = e.GetPosition(this);
        var dx = pos.X - _lastPointer.Value.X;
        var dy = pos.Y - _lastPointer.Value.Y;

        if (_isRotating)
        {
            var (rx, ry) = ViewportInteractionLogic.ApplyRotation(RotationX, RotationY, dx, dy, RotateSensitivity);
            RotationX = rx;
            RotationY = ry;
        }
        else if (_isPanning)
        {
            var (px, py) = ViewportInteractionLogic.ApplyPan(PanX, PanY, dx, dy, PanSensitivity * Zoom);
            PanX = px;
            PanY = py;
        }

        _lastPointer = pos;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        Zoom = ViewportInteractionLogic.ApplyWheelZoom(Zoom, e.Delta.Y * 120, ZoomStepFactor); // normalize to 120-steps
    }

    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        try
        {
            _skiaContext?.Dispose();
            _skiaGl?.Dispose();
            _skiaContext = null;
            _skiaGl = null;
        }
        catch { }
    }

    protected override void OnOpenGlRender(GlInterface gl, int fb)
    {
        try
        {
            gl.Clear(Avalonia.OpenGL.GlConsts.GL_COLOR_BUFFER_BIT | Avalonia.OpenGL.GlConsts.GL_DEPTH_BUFFER_BIT);

            if (_skiaContext == null)
                return;

            int width = Math.Max(1, (int)Bounds.Width);
            int height = Math.Max(1, (int)Bounds.Height);

            // Build Skia render target from current framebuffer
            const uint GL_RGBA8 = 0x8058u;
            var fbInfo = new GRGlFramebufferInfo((uint)fb, GL_RGBA8);
            using var backendRt = new GRBackendRenderTarget(width, height, 0, 8, fbInfo);
            using var surface = SKSurface.Create(_skiaContext, backendRt, GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888);
            if (surface == null)
                return;

            var canvas = surface.Canvas;
            canvas.Clear(new SKColor(23, 23, 31));

            // Axes at screen center
            using var axisPaint = new SKPaint { IsAntialias = true, StrokeWidth = 1.5f, Style = SKPaintStyle.Stroke };
            float hw = width / 2f, hh = height / 2f;
            axisPaint.Color = SKColors.Red;
            canvas.DrawLine(0, hh, width, hh, axisPaint);
            axisPaint.Color = SKColors.LimeGreen;
            canvas.DrawLine(hw, 0, hw, height, axisPaint);

            // World origin cross (optional)
            if (ShowOrigin)
            {
                using var originPaint = new SKPaint { IsAntialias = true, Color = new SKColor(255,255,255,80), StrokeWidth = 1f, Style = SKPaintStyle.Stroke };
                double scale = Math.Max(1e-6, Zoom);
                var (ovx, ovy) = ViewportMath.TransformWorldToView(0, 0, 0, RotationX, RotationY, PanX, PanY);
                float ox = hw + (float)(ovx / scale);
                float oy = hh - (float)(ovy / scale);
                canvas.DrawLine(ox - 10, oy, ox + 10, oy, originPaint);
                canvas.DrawLine(ox, oy - 10, ox, oy + 10, originPaint);
            }

            // Toolpath lines (XY projection)
            if (Commands is { Count: > 0 })
            {
                using var pathPaint = new SKPaint { IsAntialias = true, Color = new SKColor(25, 180, 255), StrokeWidth = 1.5f, Style = SKPaintStyle.Stroke };
                double scale = Math.Max(1e-6, Zoom);
                foreach (var cmd in Commands)
                {
                    if (cmd is Line line)
                    {
                        var (vx1, vy1) = ViewportMath.TransformWorldToView(line.Start.X, line.Start.Y, line.Start.Z, RotationX, RotationY, PanX, PanY);
                        var (vx2, vy2) = ViewportMath.TransformWorldToView(line.End.X, line.End.Y, line.End.Z, RotationX, RotationY, PanX, PanY);
                        float sx1 = hw + (float)(vx1 / scale);
                        float sy1 = hh - (float)(vy1 / scale);
                        float sx2 = hw + (float)(vx2 / scale);
                        float sy2 = hh - (float)(vy2 / scale);
                        canvas.DrawLine(sx1, sy1, sx2, sy2, pathPaint);
                    }
                    else if (cmd is Arc arc)
                    {
                        // Simple polyline approximation for preview
                        int segments = Math.Max(6, (int)(Math.Abs(arc.AngleSpan) * 24 / Math.PI));
                        var prev = arc.Start;
                        for (int i = 1; i <= segments; i++)
                        {
                            double t = (double)i / segments;
                            var cur = arc.Interpolate(t);
                            var (vx1, vy1) = ViewportMath.TransformWorldToView(prev.X, prev.Y, prev.Z, RotationX, RotationY, PanX, PanY);
                            var (vx2, vy2) = ViewportMath.TransformWorldToView(cur.X, cur.Y, cur.Z, RotationX, RotationY, PanX, PanY);
                            float sx1 = hw + (float)(vx1 / scale);
                            float sy1 = hh - (float)(vy1 / scale);
                            float sx2 = hw + (float)(vx2 / scale);
                            float sy2 = hh - (float)(vy2 / scale);
                            canvas.DrawLine(sx1, sy1, sx2, sy2, pathPaint);
                            prev = cur;
                        }
                    }
                }
            }

            // Grid (optional) in world coordinates projected; coarse approach based on pixel step
            if (ShowGrid)
            {
                using var gridPaint = new SKPaint { IsAntialias = false, Color = new SKColor(255,255,255,20), StrokeWidth = 1f, Style = SKPaintStyle.Stroke };
                double scale = Math.Max(1e-6, Zoom);
                double pixelPerWorld = 1.0 / scale;
                double desiredStepPx = Math.Max(4.0, GridMinPixelStep);
                double rawWorldStep = desiredStepPx * pixelPerWorld;
                // snap to nice step 1/2/5 * 10^n
                double mag = Math.Pow(10, Math.Floor(Math.Log10(rawWorldStep)));
                double baseVal = rawWorldStep / mag;
                double niceBase = baseVal <= 1 ? 1 : baseVal <= 2 ? 2 : baseVal <= 5 ? 5 : 10;
                double worldStep = niceBase * mag;

                // Draw a small set around current pan to avoid huge loops
                var (cxw, cyw) = (PanX, PanY); // approx center in world view-space
                int lines = 50; // enough to fill typical view at various zoom levels
                for (int i = -lines; i <= lines; i++)
                {
                    double wx = (cxw + i * worldStep);
                    var (vx1, vy1) = (wx, cyw - lines * worldStep);
                    var (vx2, vy2) = (wx, cyw + lines * worldStep);
                    float sx1 = hw + (float)(vx1 / scale);
                    float sy1 = hh - (float)(vy1 / scale);
                    float sx2 = hw + (float)(vx2 / scale);
                    float sy2 = hh - (float)(vy2 / scale);
                    canvas.DrawLine(sx1, sy1, sx2, sy2, gridPaint);
                }
                for (int j = -lines; j <= lines; j++)
                {
                    double wy = (cyw + j * worldStep);
                    var (vx1, vy1) = (cxw - lines * worldStep, wy);
                    var (vx2, vy2) = (cxw + lines * worldStep, wy);
                    float sx1 = hw + (float)(vx1 / scale);
                    float sy1 = hh - (float)(vy1 / scale);
                    float sx2 = hw + (float)(vx2 / scale);
                    float sy2 = hh - (float)(vy2 / scale);
                    canvas.DrawLine(sx1, sy1, sx2, sy2, gridPaint);
                }
            }

            // Bounds (optional)
            if (ShowBounds && Commands is { Count: > 0 })
            {
                if (ViewportAutoFit.TryComputeBounds(Commands, out var minX, out var minY, out var maxX, out var maxY))
                {
                    using var boundsPaint = new SKPaint { IsAntialias = true, Color = new SKColor(255, 215, 0, 140), StrokeWidth = 1.5f, Style = SKPaintStyle.Stroke };
                    double scale = Math.Max(1e-6, Zoom);
                    var (vx1, vy1) = ViewportMath.TransformWorldToView(minX, minY, 0, RotationX, RotationY, PanX, PanY);
                    var (vx2, vy2) = ViewportMath.TransformWorldToView(maxX, maxY, 0, RotationX, RotationY, PanX, PanY);
                    float sx1 = hw + (float)(vx1 / scale);
                    float sy1 = hh - (float)(vy1 / scale);
                    float sx2 = hw + (float)(vx2 / scale);
                    float sy2 = hh - (float)(vy2 / scale);
                    // Normalize coordinates to build rect
                    var left = Math.Min(sx1, sx2);
                    var top = Math.Min(sy1, sy2);
                    var right = Math.Max(sx1, sx2);
                    var bottom = Math.Max(sy1, sy2);
                    canvas.DrawRect(SKRect.Create(left, top, right - left, bottom - top), boundsPaint);
                }
            }
        }
        catch
        {
            // Ignore render errors to avoid crashing UI in early PoC
        }
        finally
        {
            // Request another frame for smooth updates during interactions
            Dispatcher.UIThread.Post(RequestNextFrameRendering);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CommandsProperty ||
            change.Property == ZoomProperty ||
            change.Property == RotationXProperty ||
            change.Property == RotationYProperty ||
            change.Property == FitRequestIdProperty ||
            change.Property == PanXProperty ||
            change.Property == PanYProperty ||
            change.Property == RotateSensitivityProperty ||
            change.Property == PanSensitivityProperty ||
            change.Property == ZoomStepFactorProperty ||
            change.Property == ShowGridProperty ||
            change.Property == ShowOriginProperty ||
            change.Property == ShowBoundsProperty ||
            change.Property == GridMinPixelStepProperty)
        {
            if (change.Property == CommandsProperty || change.Property == FitRequestIdProperty)
                AutoFit();
            RequestNextFrameRendering();
        }
    }
}
