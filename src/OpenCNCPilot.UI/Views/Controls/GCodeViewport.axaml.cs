using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
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

    private void AutoFit()
    {
        if (Commands is null || Commands.Count == 0)
            return;
        double minX = double.PositiveInfinity, minY = double.PositiveInfinity;
        double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
        void Accumulate(double x, double y)
        {
            if (x < minX) minX = x; if (x > maxX) maxX = x;
            if (y < minY) minY = y; if (y > maxY) maxY = y;
        }
        foreach (var c in Commands)
        {
            switch (c)
            {
                case Line l:
                    Accumulate(l.Start.X, l.Start.Y);
                    Accumulate(l.End.X, l.End.Y);
                    break;
                case Arc a:
                    Accumulate(a.Start.X, a.Start.Y);
                    Accumulate(a.End.X, a.End.Y);
                    break;
            }
        }
        if (!double.IsFinite(minX) || !double.IsFinite(minY) || !double.IsFinite(maxX) || !double.IsFinite(maxY))
            return;
        var w = Math.Max(1, Bounds.Width);
        var h = Math.Max(1, Bounds.Height);
        double spanX = Math.Max(1e-3, maxX - minX);
        double spanY = Math.Max(1e-3, maxY - minY);
        double margin = 1.1; // 10% margin
        double scaleX = spanX / (w / 2) * margin;
        double scaleY = spanY / (h / 2) * margin;
        Zoom = Math.Max(scaleX, scaleY);
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

            // Axes
            using var axisPaint = new SKPaint { IsAntialias = true, StrokeWidth = 1.5f, Style = SKPaintStyle.Stroke };
            float hw = width / 2f, hh = height / 2f;
            axisPaint.Color = SKColors.Red;
            canvas.DrawLine(0, hh, width, hh, axisPaint);
            axisPaint.Color = SKColors.LimeGreen;
            canvas.DrawLine(hw, 0, hw, height, axisPaint);

            // Toolpath lines (XY projection)
            if (Commands is { Count: > 0 })
            {
                using var pathPaint = new SKPaint { IsAntialias = true, Color = new SKColor(25, 180, 255), StrokeWidth = 1.5f, Style = SKPaintStyle.Stroke };
                double scale = Math.Max(1e-6, Zoom);
                foreach (var cmd in Commands)
                {
                    if (cmd is Line line)
                    {
                        float sx1 = hw + (float)(line.Start.X / scale);
                        float sy1 = hh - (float)(line.Start.Y / scale);
                        float sx2 = hw + (float)(line.End.X / scale);
                        float sy2 = hh - (float)(line.End.Y / scale);
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
                            float sx1 = hw + (float)(prev.X / scale);
                            float sy1 = hh - (float)(prev.Y / scale);
                            float sx2 = hw + (float)(cur.X / scale);
                            float sy2 = hh - (float)(cur.Y / scale);
                            canvas.DrawLine(sx1, sy1, sx2, sy2, pathPaint);
                            prev = cur;
                        }
                    }
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
            change.Property == FitRequestIdProperty)
        {
            if (change.Property == CommandsProperty || change.Property == FitRequestIdProperty)
                AutoFit();
            RequestNextFrameRendering();
        }
    }
}
