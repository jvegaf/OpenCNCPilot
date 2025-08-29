using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using OpenCNCPilot.Core.GCode.GCodeCommands;
using SkiaSharp;
using OpenCNCPilot.Core.Geometry;

namespace OpenCNCPilot.UI.Views.Controls;

public partial class GCodeViewport : OpenGlControlBase
{
    private GRGlInterface? _skiaGl;
    private GRContext? _skiaContext;
    private readonly DispatcherTimer _renderTimer;
    private bool _renderInvalidated;
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

    // New geometry-based pipeline
    public static readonly StyledProperty<GeometryData?> GeometryProperty =
        AvaloniaProperty.Register<GCodeViewport, GeometryData?>(nameof(Geometry));
    public GeometryData? Geometry
    {
        get => GetValue(GeometryProperty);
        set => SetValue(GeometryProperty, value);
    }

    public static readonly StyledProperty<double> FlattenToleranceProperty =
        AvaloniaProperty.Register<GCodeViewport, double>(nameof(FlattenTolerance), 0.05);
    public double FlattenTolerance
    {
        get => GetValue(FlattenToleranceProperty);
        set => SetValue(FlattenToleranceProperty, value);
    }

    private List<List<Vector3>>? _cachedRapidPaths;
    private List<List<Vector3>>? _cachedCutPaths;
    private GeometryData? _cachedGeometrySource;
    private double _cachedTolerance;

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

    // Stroke appearance (exposed for future settings binding)
    public static readonly StyledProperty<SKColor> RapidColorProperty =
        AvaloniaProperty.Register<GCodeViewport, SKColor>(nameof(RapidColor), new SKColor(25, 180, 255));
    public static readonly StyledProperty<SKColor> CutColorProperty =
        AvaloniaProperty.Register<GCodeViewport, SKColor>(nameof(CutColor), new SKColor(255, 120, 40));
    // For XAML bindings using uint ARGB from settings, also expose UInt properties that set SKColor behind the scenes
    public static readonly StyledProperty<uint> RapidColorArgbProperty =
        AvaloniaProperty.Register<GCodeViewport, uint>(nameof(RapidColorArgb), 0xFF19B4FF);
    public static readonly StyledProperty<uint> CutColorArgbProperty =
        AvaloniaProperty.Register<GCodeViewport, uint>(nameof(CutColorArgb), 0xFFFF7828);
    public static readonly StyledProperty<double> RapidStrokeWidthProperty =
        AvaloniaProperty.Register<GCodeViewport, double>(nameof(RapidStrokeWidth), 1.2);
    public static readonly StyledProperty<double> CutStrokeWidthProperty =
        AvaloniaProperty.Register<GCodeViewport, double>(nameof(CutStrokeWidth), 1.6);

    public SKColor RapidColor { get => GetValue(RapidColorProperty); set => SetValue(RapidColorProperty, value); }
    public SKColor CutColor { get => GetValue(CutColorProperty); set => SetValue(CutColorProperty, value); }
    public uint RapidColorArgb { get => GetValue(RapidColorArgbProperty); set => SetValue(RapidColorArgbProperty, value); }
    public uint CutColorArgb { get => GetValue(CutColorArgbProperty); set => SetValue(CutColorArgbProperty, value); }
    public double RapidStrokeWidth { get => GetValue(RapidStrokeWidthProperty); set => SetValue(RapidStrokeWidthProperty, value); }
    public double CutStrokeWidth { get => GetValue(CutStrokeWidthProperty); set => SetValue(CutStrokeWidthProperty, value); }

    private void AutoFit()
    {
        int width = Math.Max(1, (int)Bounds.Width);
        int height = Math.Max(1, (int)Bounds.Height);
        if (Geometry is not null)
        {
            var cmds = BuildPseudoCommandsFromGeometry(Geometry);
            var (zoom, panX, panY) = ViewportAutoFit.Compute(cmds, width, height, RotationX, RotationY);
            Zoom = zoom; PanX = panX; PanY = panY;
            return;
        }

        if (Commands is null || Commands.Count == 0) return;
        var (zoom2, panX2, panY2) = ViewportAutoFit.Compute(Commands, width, height, RotationX, RotationY);
        Zoom = zoom2; PanX = panX2; PanY = panY2;
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

    public GCodeViewport()
    {
        // Coalesce render requests to ~60 FPS max
        _renderTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _renderTimer.Tick += (_, __) =>
        {
            if (_renderInvalidated)
            {
                _renderInvalidated = false;
                RequestNextFrameRendering();
            }
        };
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
            if (Geometry is not null)
            {
                EnsureFlattenCache();
                double scale = Math.Max(1e-6, Zoom);
                EnsureSkPathCache(width, height, scale);
                using var rapidPaint = new SKPaint { IsAntialias = true, Color = RapidColor, StrokeWidth = (float)RapidStrokeWidth, Style = SKPaintStyle.Stroke };
                using var cutPaint = new SKPaint { IsAntialias = true, Color = CutColor, StrokeWidth = (float)CutStrokeWidth, Style = SKPaintStyle.Stroke };

                if (_cachedRapidPathSk is not null)
                    canvas.DrawPath(_cachedRapidPathSk, rapidPaint);
                if (_cachedCutPathSk is not null)
                    canvas.DrawPath(_cachedCutPathSk, cutPaint);
            }
            else if (Commands is { Count: > 0 })
            {
                using var pathPaint = new SKPaint { IsAntialias = true, Color = RapidColor, StrokeWidth = (float)Math.Max(RapidStrokeWidth, CutStrokeWidth), Style = SKPaintStyle.Stroke };
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
            if (ShowBounds && (Geometry is not null || (Commands is { Count: > 0 })))
            {
                IReadOnlyList<Command>? cmds = Commands;
                if (Geometry is not null)
                    cmds = BuildPseudoCommandsFromGeometry(Geometry);

                if (cmds is not null && ViewportAutoFit.TryComputeBounds(cmds, out var minX, out var minY, out var maxX, out var maxY))
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
        finally { }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CommandsProperty ||
            change.Property == GeometryProperty ||
            change.Property == ZoomProperty ||
            change.Property == RotationXProperty ||
            change.Property == RotationYProperty ||
            change.Property == FitRequestIdProperty ||
            change.Property == PanXProperty ||
            change.Property == PanYProperty ||
            change.Property == FlattenToleranceProperty ||
            change.Property == RotateSensitivityProperty ||
            change.Property == PanSensitivityProperty ||
            change.Property == ZoomStepFactorProperty ||
            change.Property == ShowGridProperty ||
            change.Property == ShowOriginProperty ||
            change.Property == ShowBoundsProperty ||
            change.Property == GridMinPixelStepProperty ||
            change.Property == RapidColorProperty ||
            change.Property == CutColorProperty ||
            change.Property == RapidStrokeWidthProperty ||
            change.Property == CutStrokeWidthProperty ||
            change.Property == RapidColorArgbProperty ||
            change.Property == CutColorArgbProperty)
        {
            if (change.Property == CommandsProperty || change.Property == GeometryProperty || change.Property == FitRequestIdProperty)
            {
                InvalidateFlattenCache();
                InvalidateSkPathCache();
                AutoFit();
            }
            else if (change.Property == ZoomProperty || change.Property == RotationXProperty || change.Property == RotationYProperty || change.Property == PanXProperty || change.Property == PanYProperty || change.Property == FlattenToleranceProperty)
            {
                InvalidateSkPathCache();
            }
            else if (change.Property == RapidColorArgbProperty)
            {
                var c = GetValue(RapidColorArgbProperty);
                RapidColor = new SKColor((byte)((c >> 16) & 0xFF), (byte)((c >> 8) & 0xFF), (byte)(c & 0xFF), (byte)((c >> 24) & 0xFF));
            }
            else if (change.Property == CutColorArgbProperty)
            {
                var c = GetValue(CutColorArgbProperty);
                CutColor = new SKColor((byte)((c >> 16) & 0xFF), (byte)((c >> 8) & 0xFF), (byte)(c & 0xFF), (byte)((c >> 24) & 0xFF));
            }
            CoalesceRenderRequest();
        }
    }

    private void InvalidateFlattenCache()
    {
        _cachedRapidPaths = null;
        _cachedCutPaths = null;
        _cachedGeometrySource = null;
        _cachedTolerance = 0;
    }

    private void EnsureFlattenCache()
    {
        if (Geometry is null) { InvalidateFlattenCache(); return; }
        if (_cachedRapidPaths != null && ReferenceEquals(_cachedGeometrySource, Geometry) && Math.Abs(_cachedTolerance - FlattenTolerance) < 1e-12)
            return;
        var (rapids, cuts) = SegmentFlattener.Flatten(Geometry, FlattenTolerance);
        _cachedRapidPaths = rapids;
        _cachedCutPaths = cuts;
        _cachedGeometrySource = Geometry;
        _cachedTolerance = FlattenTolerance;
        InvalidateSkPathCache();
    }

    private void DrawPolylinePaths(SKCanvas canvas, int width, int height, List<List<Vector3>>? paths, double scale, SKPaint paint)
    {
        if (paths is null) return;
        float hw = width / 2f, hh = height / 2f;
        foreach (var path in paths)
        {
            if (path.Count < 2) continue;
            var prev = path[0];
            for (int i = 1; i < path.Count; i++)
            {
                var cur = path[i];
                var (vx1, vy1) = ViewportMath.TransformWorldToView(prev.X, prev.Y, prev.Z, RotationX, RotationY, PanX, PanY);
                var (vx2, vy2) = ViewportMath.TransformWorldToView(cur.X, cur.Y, cur.Z, RotationX, RotationY, PanX, PanY);
                float sx1 = hw + (float)(vx1 / scale);
                float sy1 = hh - (float)(vy1 / scale);
                float sx2 = hw + (float)(vx2 / scale);
                float sy2 = hh - (float)(vy2 / scale);
                canvas.DrawLine(sx1, sy1, sx2, sy2, paint);
                prev = cur;
            }
        }
    }

    // SKPath cache in screen coordinates to reduce draw calls
    private SKPath? _cachedRapidPathSk;
    private SKPath? _cachedCutPathSk;
    private GeometryData? _skPathGeometrySource;
    private double _skPathTol, _skPathZoom, _skPathRx, _skPathRy, _skPathPanX, _skPathPanY;
    private int _skPathWidth, _skPathHeight;

    private void InvalidateSkPathCache()
    {
        _cachedRapidPathSk?.Dispose();
        _cachedCutPathSk?.Dispose();
        _cachedRapidPathSk = null;
        _cachedCutPathSk = null;
        _skPathGeometrySource = null;
        _skPathWidth = _skPathHeight = 0;
    }

    private void EnsureSkPathCache(int width, int height, double scale)
    {
        if (Geometry is null || _cachedRapidPaths is null || _cachedCutPaths is null)
        {
            InvalidateSkPathCache();
            return;
        }
        if (_cachedRapidPathSk != null &&
            ReferenceEquals(_skPathGeometrySource, Geometry) &&
            Math.Abs(_skPathTol - FlattenTolerance) < 1e-12 &&
            Math.Abs(_skPathZoom - Zoom) < 1e-12 &&
            Math.Abs(_skPathRx - RotationX) < 1e-12 &&
            Math.Abs(_skPathRy - RotationY) < 1e-12 &&
            Math.Abs(_skPathPanX - PanX) < 1e-12 &&
            Math.Abs(_skPathPanY - PanY) < 1e-12 &&
            _skPathWidth == width && _skPathHeight == height)
        {
            return;
        }

        InvalidateSkPathCache();
        float hw = width / 2f, hh = height / 2f;
        _cachedRapidPathSk = new SKPath();
        foreach (var path in _cachedRapidPaths)
        {
            if (path.Count < 2) continue;
            var prev = path[0];
            var (vx1, vy1) = ViewportMath.TransformWorldToView(prev.X, prev.Y, prev.Z, RotationX, RotationY, PanX, PanY);
            float sx1 = hw + (float)(vx1 / scale);
            float sy1 = hh - (float)(vy1 / scale);
            _cachedRapidPathSk.MoveTo(sx1, sy1);
            for (int i = 1; i < path.Count; i++)
            {
                var cur = path[i];
                var (vx2, vy2) = ViewportMath.TransformWorldToView(cur.X, cur.Y, cur.Z, RotationX, RotationY, PanX, PanY);
                float sx2 = hw + (float)(vx2 / scale);
                float sy2 = hh - (float)(vy2 / scale);
                _cachedRapidPathSk.LineTo(sx2, sy2);
            }
        }
        _cachedCutPathSk = new SKPath();
        foreach (var path in _cachedCutPaths)
        {
            if (path.Count < 2) continue;
            var prev = path[0];
            var (vx1, vy1) = ViewportMath.TransformWorldToView(prev.X, prev.Y, prev.Z, RotationX, RotationY, PanX, PanY);
            float sx1 = hw + (float)(vx1 / scale);
            float sy1 = hh - (float)(vy1 / scale);
            _cachedCutPathSk.MoveTo(sx1, sy1);
            for (int i = 1; i < path.Count; i++)
            {
                var cur = path[i];
                var (vx2, vy2) = ViewportMath.TransformWorldToView(cur.X, cur.Y, cur.Z, RotationX, RotationY, PanX, PanY);
                float sx2 = hw + (float)(vx2 / scale);
                float sy2 = hh - (float)(vy2 / scale);
                _cachedCutPathSk.LineTo(sx2, sy2);
            }
        }

        _skPathGeometrySource = Geometry;
        _skPathTol = FlattenTolerance;
        _skPathZoom = Zoom;
        _skPathRx = RotationX;
        _skPathRy = RotationY;
        _skPathPanX = PanX;
        _skPathPanY = PanY;
        _skPathWidth = width;
        _skPathHeight = height;
    }

    private void CoalesceRenderRequest()
    {
        _renderInvalidated = true;
        if (!_renderTimer.IsEnabled)
            _renderTimer.Start();
    }
    private static IReadOnlyList<Command> BuildPseudoCommandsFromGeometry(GeometryData geometry)
    {
        // Build lightweight pseudo-commands for bounds/fit reuse
        var list = new List<Command>(geometry.Lines.Count + geometry.Arcs.Count);
        foreach (var l in geometry.Lines)
        {
            list.Add(new Line { Start = l.Start, End = l.End, Feed = 0, Rapid = l.Type == MovementType.Rapid, StartValid = true, PositionValid = new[] { true, true, true } });
        }
        foreach (var a in geometry.Arcs)
        {
            list.Add(new Arc { Start = a.Start, End = a.End, Feed = 0, Direction = a.Direction, Plane = a.Plane, U = a.U, V = a.V });
        }
        return list;
    }
}
