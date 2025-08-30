using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
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
    private bool _lastGlSurfaceOk;
    // Permite forzar el render por software (fallback) aunque GL/Skia estén disponibles
    public static readonly StyledProperty<bool> ForceSoftwareRenderingProperty =
        AvaloniaProperty.Register<GCodeViewport, bool>(nameof(ForceSoftwareRendering), false);
    public bool ForceSoftwareRendering
    {
        get => GetValue(ForceSoftwareRenderingProperty);
        set => SetValue(ForceSoftwareRenderingProperty, value);
    }

    // Muestra un overlay con el modo de render actual (GL o Software)
    public static readonly StyledProperty<bool> ShowRenderModeOverlayProperty =
        AvaloniaProperty.Register<GCodeViewport, bool>(nameof(ShowRenderModeOverlay), true);
    public bool ShowRenderModeOverlay
    {
        get => GetValue(ShowRenderModeOverlayProperty);
        set => SetValue(ShowRenderModeOverlayProperty, value);
    }
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

    // Optional simplification after flattening (Douglas–Peucker)
    public static readonly StyledProperty<bool> EnableSimplificationProperty =
        AvaloniaProperty.Register<GCodeViewport, bool>(nameof(EnableSimplification), false);
    public static readonly StyledProperty<double> SimplificationEpsilonProperty =
        AvaloniaProperty.Register<GCodeViewport, double>(nameof(SimplificationEpsilon), 0.10);
    public bool EnableSimplification
    {
        get => GetValue(EnableSimplificationProperty);
        set => SetValue(EnableSimplificationProperty, value);
    }
    public double SimplificationEpsilon
    {
        get => GetValue(SimplificationEpsilonProperty);
        set => SetValue(SimplificationEpsilonProperty, Math.Max(0, value));
    }

    private List<List<Vector3>>? _cachedRapidPaths;
    private List<List<Vector3>>? _cachedCutPaths;
    private GeometryData? _cachedGeometrySource;
    private double _cachedTolerance;
    private bool _cachedSimplifyEnabled;
    private double _cachedSimplifyEps;

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
    // Default palette (alta visibilidad)
    public static readonly StyledProperty<SKColor> RapidColorProperty =
        AvaloniaProperty.Register<GCodeViewport, SKColor>(nameof(RapidColor), new SKColor(0, 200, 255));
    public static readonly StyledProperty<SKColor> CutColorProperty =
        AvaloniaProperty.Register<GCodeViewport, SKColor>(nameof(CutColor), new SKColor(255, 200, 0));
    // For XAML bindings using uint ARGB from settings, also expose UInt properties that set SKColor behind the scenes
    public static readonly StyledProperty<uint> RapidColorArgbProperty =
        AvaloniaProperty.Register<GCodeViewport, uint>(nameof(RapidColorArgb), 0xFF00C8FF);
    public static readonly StyledProperty<uint> CutColorArgbProperty =
        AvaloniaProperty.Register<GCodeViewport, uint>(nameof(CutColorArgb), 0xFFFFC800);
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
            // Ensure we schedule a first frame; some platforms need an explicit request
            CoalesceRenderRequest();
        }
        catch (Exception)
        {
            // In headless/CI environments, GL might be unavailable
            _skiaContext = null;
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

        // Trigger an initial render once control is attached and sized
        this.AttachedToVisualTree += (_, __) => CoalesceRenderRequest();
        this.GetPropertyChangedObservable(BoundsProperty).Subscribe(_ => CoalesceRenderRequest());
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
        // Anchored zoom around cursor
        int width = Math.Max(1, (int)Bounds.Width);
        int height = Math.Max(1, (int)Bounds.Height);
        var pos = e.GetPosition(this);
        var (nz, npx, npy) = ViewportInteractionLogic.ApplyWheelZoomAnchored(Zoom, e.Delta.Y * 120, ZoomStepFactor, PanX, PanY, pos.X, pos.Y, width, height);
        Zoom = nz;
        PanX = npx;
        PanY = npy;
    }

    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        try
        {
            _skiaContext?.Dispose();
            _skiaGl?.Dispose();
            _skiaContext = null;
            _skiaGl = null;
            _lastGlSurfaceOk = false;
        }
        catch { }
    }

    protected override void OnOpenGlRender(GlInterface gl, int fb)
    {
        try
        {
            // Si se fuerza el render por software, no dibujar aquí
            if (ForceSoftwareRendering)
            {
                _lastGlSurfaceOk = false;
                return;
            }
            _lastGlSurfaceOk = false;
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
            // Fondo
            canvas.Clear(new SKColor(15, 18, 24));

            // Axes at screen center
            using var axisPaint = new SKPaint { IsAntialias = true, StrokeWidth = 1.75f, Style = SKPaintStyle.Stroke };
            float hw = width / 2f, hh = height / 2f;
            axisPaint.Color = new SKColor(255, 80, 80); // X
            canvas.DrawLine(0, hh, width, hh, axisPaint);
            axisPaint.Color = new SKColor(80, 255, 120); // Y
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
                // Aumenta ligeramente la opacidad para mejor visibilidad
                using var gridPaint = new SKPaint { IsAntialias = false, Color = new SKColor(200, 220, 255, 48), StrokeWidth = 1f, Style = SKPaintStyle.Stroke };
                double scale = Math.Max(1e-6, Zoom);
                var (worldStep, stepPx, lines) = ComputeGridParams(width, height, scale);

                // Draw a small set around current pan to avoid huge loops
                var (cxw, cyw) = (PanX, PanY); // approx center in world view-space
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

            // Overlays (OpenGL)
            if (ShowRenderModeOverlay)
            {
                // Línea 1: modo de render
                const string text = "Render: OpenGL";
                using var paint = new SKPaint { IsAntialias = true, Color = new SKColor(240, 240, 240), TextSize = 14f }; // ~14px
                var bounds = new SKRect();
                paint.MeasureText(text, ref bounds);
                float padX = 6f, padY = 4f;
                float rx = 8f, ry = 8f;
                float w = (bounds.Width) + padX * 2f;
                float h = (bounds.Height) + padY * 2f;
                using var bg = new SKPaint { Color = new SKColor(20, 20, 20, 180) };
                float x = 8f, y = 8f;
                canvas.DrawRoundRect(new SKRect(x, y, x + w, y + h), rx, ry, bg);
                canvas.DrawText(text, x + padX, y + padY - bounds.Top, paint);

                // Línea 2: estado vacío si no hay G-code
                bool hasContent = (Geometry is not null) || (Commands is { Count: > 0 });
                float lastBottom = y + h;
                if (!hasContent)
                {
                    const string emptyText = "No hay G-code cargado";
                    var bounds2 = new SKRect();
                    paint.MeasureText(emptyText, ref bounds2);
                    float w2 = (bounds2.Width) + padX * 2f;
                    float h2 = (bounds2.Height) + padY * 2f;
                    float y2 = lastBottom + 6f; // colocar debajo del primer badge
                    canvas.DrawRoundRect(new SKRect(x, y2, x + w2, y2 + h2), rx, ry, bg);
                    canvas.DrawText(emptyText, x + padX, y2 + padY - bounds2.Top, paint);
                    lastBottom = y2 + h2;
                }

                // Línea 3: debug paso rejilla
                double scale = Math.Max(1e-6, Zoom);
                var (_, stepPx, lines) = ComputeGridParams(width, height, scale);
                string dbg = $"Grid: {stepPx:F1}px step, {lines} lines";
                var bounds3 = new SKRect();
                paint.MeasureText(dbg, ref bounds3);
                float w3 = (bounds3.Width) + padX * 2f;
                float h3 = (bounds3.Height) + padY * 2f;
                float y3 = lastBottom + 6f;
                canvas.DrawRoundRect(new SKRect(x, y3, x + w3, y3 + h3), rx, ry, bg);
                canvas.DrawText(dbg, x + padX, y3 + padY - bounds3.Top, paint);
                lastBottom = y3 + h3;

                // Línea 4: métricas de movimientos y bounds
                var (rCount, cCount, bw, bh) = ComputeMetrics();
                string metrics = $"Movs: {rCount}/{cCount} | Bounds: {bw:F1}x{bh:F1} mm";
                var bounds4 = new SKRect();
                paint.MeasureText(metrics, ref bounds4);
                float w4 = (bounds4.Width) + padX * 2f;
                float h4 = (bounds4.Height) + padY * 2f;
                float y4 = lastBottom + 6f;
                canvas.DrawRoundRect(new SKRect(x, y4, x + w4, y4 + h4), rx, ry, bg);
                canvas.DrawText(metrics, x + padX, y4 + padY - bounds4.Top, paint);
            }
            _lastGlSurfaceOk = true;
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
            change.Property == CutColorArgbProperty ||
            change.Property == EnableSimplificationProperty ||
            change.Property == SimplificationEpsilonProperty ||
            change.Property == ForceSoftwareRenderingProperty ||
            change.Property == ShowRenderModeOverlayProperty)
        {
            if (change.Property == CommandsProperty || change.Property == GeometryProperty || change.Property == FitRequestIdProperty)
            {
                InvalidateFlattenCache();
                InvalidateSkPathCache();
                AutoFit();
            }
            else if (change.Property == ZoomProperty || change.Property == RotationXProperty || change.Property == RotationYProperty || change.Property == PanXProperty || change.Property == PanYProperty || change.Property == FlattenToleranceProperty || change.Property == EnableSimplificationProperty || change.Property == SimplificationEpsilonProperty)
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
        _cachedSimplifyEnabled = false;
        _cachedSimplifyEps = 0;
    }

    private void EnsureFlattenCache()
    {
        if (Geometry is null) { InvalidateFlattenCache(); return; }
        if (_cachedRapidPaths != null && ReferenceEquals(_cachedGeometrySource, Geometry) && Math.Abs(_cachedTolerance - FlattenTolerance) < 1e-12 && _cachedSimplifyEnabled == EnableSimplification && Math.Abs(_cachedSimplifyEps - SimplificationEpsilon) < 1e-12)
            return;
        var (rapids, cuts) = SegmentFlattener.Flatten(Geometry, FlattenTolerance);
        if (EnableSimplification && SimplificationEpsilon > 0)
        {
            for (int i = 0; i < rapids.Count; i++) rapids[i] = PolylineSimplifier.Simplify(rapids[i], SimplificationEpsilon);
            for (int i = 0; i < cuts.Count; i++) cuts[i] = PolylineSimplifier.Simplify(cuts[i], SimplificationEpsilon);
        }
        _cachedRapidPaths = rapids;
        _cachedCutPaths = cuts;
        _cachedGeometrySource = Geometry;
        _cachedTolerance = FlattenTolerance;
        _cachedSimplifyEnabled = EnableSimplification;
        _cachedSimplifyEps = SimplificationEpsilon;
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
        // Solicita un frame de render; esto cubre tanto la ruta GL como la de software
        RequestNextFrameRendering();
        if (!_renderTimer.IsEnabled)
            _renderTimer.Start();
    }

    // Calcula parámetros de la grilla en función del tamaño del viewport y el zoom
    // Devuelve: (worldStep, stepPx, lines)
    private (double worldStep, double stepPx, int lines) ComputeGridParams(int width, int height, double scale)
    {
        // pixels por unidad de mundo (px/mm)
        double pixelsPerWorld = 1.0 / Math.Max(1e-9, scale);
        double desiredStepPx = Math.Max(4.0, GridMinPixelStep);
        // convertir paso deseado en píxeles -> paso en mundo (mm)
        double rawWorldStep = desiredStepPx / pixelsPerWorld;
        // snap a 1/2/5 * 10^n
        double mag = Math.Pow(10, Math.Floor(Math.Log10(Math.Max(1e-12, rawWorldStep))));
        double baseVal = rawWorldStep / mag;
        double niceBase = baseVal <= 1 ? 1 : baseVal <= 2 ? 2 : baseVal <= 5 ? 5 : 10;
        double worldStep = niceBase * mag;
        double stepPx = worldStep / Math.Max(1e-9, scale);
        int lines = stepPx > 1 ? Math.Min(500, (int)Math.Ceiling(Math.Max(width, height) / (2.0 * stepPx)) + 2) : 500;
        return (worldStep, stepPx, lines);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        // Software fallback when GL/Skia is not available
        if (!ForceSoftwareRendering && _skiaContext != null && _lastGlSurfaceOk)
            return; // GL path will handle drawing

        var rect = new Rect(Bounds.Size);
        // Background
    context.FillRectangle(new SolidColorBrush(Color.FromRgb(15, 18, 24)), rect);

        int width = Math.Max(1, (int)Bounds.Width);
        int height = Math.Max(1, (int)Bounds.Height);
        float hw = width / 2f, hh = height / 2f;

        // Axes
    var axisPenX = new Pen(new SolidColorBrush(Color.FromRgb(255, 80, 80)), 1.75);
    var axisPenY = new Pen(new SolidColorBrush(Color.FromRgb(80, 255, 120)), 1.75);
        context.DrawLine(axisPenX, new Point(0, hh), new Point(width, hh));
        context.DrawLine(axisPenY, new Point(hw, 0), new Point(hw, height));

        double scale = Math.Max(1e-6, Zoom);

        // Origin cross
        if (ShowOrigin)
        {
            var (ovx, ovy) = ViewportMath.TransformWorldToView(0, 0, 0, RotationX, RotationY, PanX, PanY);
            float ox = hw + (float)(ovx / scale);
            float oy = hh - (float)(ovy / scale);
            var originPen = new Pen(new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)), 1);
            context.DrawLine(originPen, new Point(ox - 10, oy), new Point(ox + 10, oy));
            context.DrawLine(originPen, new Point(ox, oy - 10), new Point(ox, oy + 10));
        }

        // Grid
        if (ShowGrid)
        {
            // Aumenta ligeramente la opacidad para mejor visibilidad
            var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(48, 200, 220, 255)), 1);
            var (worldStep, stepPx, lines) = ComputeGridParams(width, height, scale);
            var (cxw, cyw) = (PanX, PanY);
            for (int i = -lines; i <= lines; i++)
            {
                double wx = (cxw + i * worldStep);
                var (vx1, vy1) = (wx, cyw - lines * worldStep);
                var (vx2, vy2) = (wx, cyw + lines * worldStep);
                float sx1 = hw + (float)(vx1 / scale);
                float sy1 = hh - (float)(vy1 / scale);
                float sx2 = hw + (float)(vx2 / scale);
                float sy2 = hh - (float)(vy2 / scale);
                context.DrawLine(gridPen, new Point(sx1, sy1), new Point(sx2, sy2));
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
                context.DrawLine(gridPen, new Point(sx1, sy1), new Point(sx2, sy2));
            }
        }

        // Toolpath lines (software)
        var rapidPen = new Pen(new SolidColorBrush(Color.FromUInt32(RapidColorArgb)), RapidStrokeWidth);
        var cutPen = new Pen(new SolidColorBrush(Color.FromUInt32(CutColorArgb)), CutStrokeWidth);

        if (Geometry is not null)
        {
            var (rapids, cuts) = SegmentFlattener.Flatten(Geometry, FlattenTolerance);
            if (EnableSimplification && SimplificationEpsilon > 0)
            {
                for (int i = 0; i < rapids.Count; i++) rapids[i] = PolylineSimplifier.Simplify(rapids[i], SimplificationEpsilon);
                for (int i = 0; i < cuts.Count; i++) cuts[i] = PolylineSimplifier.Simplify(cuts[i], SimplificationEpsilon);
            }
            DrawPolylinePathsSoftware(context, width, height, rapids, scale, rapidPen);
            DrawPolylinePathsSoftware(context, width, height, cuts, scale, cutPen);
        }
        else if (Commands is { Count: > 0 })
        {
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
                    context.DrawLine(line.Rapid ? rapidPen : cutPen, new Point(sx1, sy1), new Point(sx2, sy2));
                }
                else if (cmd is Arc arc)
                {
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
                        context.DrawLine(cutPen, new Point(sx1, sy1), new Point(sx2, sy2));
                        prev = cur;
                    }
                }
            }
        }

    // Bounds
        if (ShowBounds && (Geometry is not null || (Commands is { Count: > 0 })))
        {
            IReadOnlyList<Command>? cmds = Commands;
            if (Geometry is not null)
                cmds = BuildPseudoCommandsFromGeometry(Geometry);

            if (cmds is not null && ViewportAutoFit.TryComputeBounds(cmds, out var minX, out var minY, out var maxX, out var maxY))
            {
                var boundsPen = new Pen(new SolidColorBrush(Color.FromArgb(140, 255, 215, 0)), 1.5);
                var (vx1, vy1) = ViewportMath.TransformWorldToView(minX, minY, 0, RotationX, RotationY, PanX, PanY);
                var (vx2, vy2) = ViewportMath.TransformWorldToView(maxX, maxY, 0, RotationX, RotationY, PanX, PanY);
                float sx1 = hw + (float)(vx1 / scale);
                float sy1 = hh - (float)(vy1 / scale);
                float sx2 = hw + (float)(vx2 / scale);
                float sy2 = hh - (float)(vy2 / scale);
                var left = Math.Min(sx1, sx2);
                var top = Math.Min(sy1, sy2);
                var right = Math.Max(sx1, sx2);
                var bottom = Math.Max(sy1, sy2);
                context.DrawRectangle(null, boundsPen, new Rect(left, top, right - left, bottom - top));
            }
        }

        // Overlays (Software)
        if (ShowRenderModeOverlay)
        {
            var typeface = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Normal);
            double fontSize = 14;
            var pad = new Thickness(6, 4);
            var x = 8.0;
            var y = 8.0;

            // Línea 1: modo de render
            const string text = "Render: Software";
            var layout = new TextLayout(text, typeface, fontSize, Brushes.White);
            double approxW = text.Length * fontSize * 0.6;
            double approxH = fontSize * 1.4;
            var bgRect = new Rect(x, y, approxW + pad.Left + pad.Right, approxH + pad.Top + pad.Bottom);
            context.FillRectangle(new SolidColorBrush(Color.FromArgb(180, 20, 20, 20)), bgRect);
            layout.Draw(context, new Point(x + pad.Left, y + pad.Top));

            // Línea 2: estado vacío si no hay G-code
            bool hasContent = (Geometry is not null) || (Commands is { Count: > 0 });
            double lastBottom = y + bgRect.Height;
            if (!hasContent)
            {
                const string emptyText = "No hay G-code cargado";
                var layout2 = new TextLayout(emptyText, typeface, fontSize, Brushes.White);
                double approxW2 = emptyText.Length * fontSize * 0.6;
                double approxH2 = fontSize * 1.4;
                var bgRect2 = new Rect(x, lastBottom + 6, approxW2 + pad.Left + pad.Right, approxH2 + pad.Top + pad.Bottom);
                context.FillRectangle(new SolidColorBrush(Color.FromArgb(180, 20, 20, 20)), bgRect2);
                layout2.Draw(context, new Point(bgRect2.X + pad.Left, bgRect2.Y + pad.Top));
                lastBottom = bgRect2.Y + bgRect2.Height;
            }

            // Línea 3: debug paso rejilla
            var (_, stepPx, lines) = ComputeGridParams(width, height, scale);
            string dbg = $"Grid: {stepPx:F1}px step, {lines} lines";
            var layout3 = new TextLayout(dbg, typeface, fontSize, Brushes.White);
            double approxW3 = dbg.Length * fontSize * 0.6;
            double approxH3 = fontSize * 1.4;
            double y3 = lastBottom + 6;
            var bgRect3 = new Rect(x, y3, approxW3 + pad.Left + pad.Right, approxH3 + pad.Top + pad.Bottom);
            context.FillRectangle(new SolidColorBrush(Color.FromArgb(180, 20, 20, 20)), bgRect3);
            layout3.Draw(context, new Point(bgRect3.X + pad.Left, bgRect3.Y + pad.Top));

            // Línea 4: métricas de movimientos y bounds
            var (rCount, cCount, bw, bh) = ComputeMetrics();
            string metrics = $"Movs: {rCount}/{cCount} | Bounds: {bw:F1}x{bh:F1} mm";
            var layout4 = new TextLayout(metrics, typeface, fontSize, Brushes.White);
            double approxW4 = metrics.Length * fontSize * 0.6;
            double approxH4 = fontSize * 1.4;
            double y4 = bgRect3.Y + bgRect3.Height + 6;
            var bgRect4 = new Rect(x, y4, approxW4 + pad.Left + pad.Right, approxH4 + pad.Top + pad.Bottom);
            context.FillRectangle(new SolidColorBrush(Color.FromArgb(180, 20, 20, 20)), bgRect4);
            layout4.Draw(context, new Point(bgRect4.X + pad.Left, bgRect4.Y + pad.Top));
        }
    }

    private void DrawPolylinePathsSoftware(DrawingContext ctx, int width, int height, System.Collections.Generic.List<System.Collections.Generic.List<Vector3>> paths, double scale, Pen pen)
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
                ctx.DrawLine(pen, new Point(sx1, sy1), new Point(sx2, sy2));
                prev = cur;
            }
        }
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

    // Calcula métricas simples para overlay: recuento de movimientos rápidos/corte y tamaño de bounds (mm)
    private (int rapidCount, int cutCount, double boundsW, double boundsH) ComputeMetrics()
    {
        int r = 0, c = 0;
        double bw = 0, bh = 0;
        IReadOnlyList<Command>? cmds = Commands;
        if (Geometry is not null)
        {
            // Usar geometry para contar: considerar líneas y arcos por tipo
            foreach (var l in Geometry.Lines)
            {
                if (l.Type == MovementType.Rapid) r++; else c++;
            }
            foreach (var a in Geometry.Arcs)
            {
                // Arcos siempre se consideran corte por ahora
                c++;
            }
            cmds = BuildPseudoCommandsFromGeometry(Geometry);
        }
        else if (cmds is not null)
        {
            foreach (var cmd in cmds)
            {
                if (cmd is Line ln)
                {
                    if (ln.Rapid) r++; else c++;
                }
                else if (cmd is Arc)
                {
                    c++;
                }
            }
        }

        if (cmds is not null && ViewportAutoFit.TryComputeBounds(cmds, out var minX, out var minY, out var maxX, out var maxY))
        {
            bw = Math.Max(0, maxX - minX);
            bh = Math.Max(0, maxY - minY);
        }
        return (r, c, bw, bh);
    }
}
