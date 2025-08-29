using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using OpenCNCPilot.UI.Services;
using ReactiveUI;

namespace OpenCNCPilot.UI.ViewModels;

public class SettingsWindowViewModel : ReactiveObject
{
    private readonly ISettingsService settingsService;
    private string defaultComPort = string.Empty;
    private int baudRate = 115200;
    private string lastGCodeDirectory = string.Empty;
    private double viewerRotateSensitivity = 0.3;
    private double viewerPanSensitivity = 0.02;
    private double viewerZoomStepFactor = 1.1;
    private bool viewerShowGrid = true;
    private bool viewerShowOrigin = true;
    private bool viewerShowBounds = false;
    private double viewerGridMinPixelStep = 30.0;
    private bool viewerEnableSimplification = false;
    private double viewerSimplificationEpsilon = 0.10;
    private double viewerFlattenTolerance = 0.05;
    private uint viewerRapidColor = 0xFF19B4FF; // ARGB
    private uint viewerCutColor = 0xFFFF7828;   // ARGB
    private double viewerRapidStrokeWidth = 1.2;
    private double viewerCutStrokeWidth = 1.6;

    // Rangos recomendados
    private const double MinRotateSens = 0.05;
    private const double MaxRotateSens = 2.0;
    private const double MinPanSens = 0.001;
    private const double MaxPanSens = 1.0;
    private const double MinZoomStep = 1.01;
    private const double MaxZoomStep = 1.5;
    private const double MinFlattenTol = 0.001;
    private const double MaxFlattenTol = 1.0;

    public SettingsWindowViewModel(ISettingsService settingsService)
    {
        this.settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        SaveCommand = ReactiveCommand.CreateFromTask(SaveAsync);
        CancelCommand = ReactiveCommand.Create(Cancel);
        BrowseLastGCodeDirectoryCommand = ReactiveCommand.CreateFromTask(BrowseLastGCodeDirectoryAsync);
        _ = Load();
    }

    public string DefaultComPort
    {
        get => defaultComPort;
        set => this.RaiseAndSetIfChanged(ref defaultComPort, value);
    }

    public int BaudRate
    {
        get => baudRate;
        set => this.RaiseAndSetIfChanged(ref baudRate, value);
    }

    public string LastGCodeDirectory
    {
        get => lastGCodeDirectory;
        set => this.RaiseAndSetIfChanged(ref lastGCodeDirectory, value);
    }

    public Interaction<Unit, string?> PickFolderInteraction { get; } = new();

    public double ViewerRotateSensitivity
    {
        get => viewerRotateSensitivity;
        set => this.RaiseAndSetIfChanged(ref viewerRotateSensitivity, Math.Clamp(value, MinRotateSens, MaxRotateSens));
    }

    public double ViewerPanSensitivity
    {
        get => viewerPanSensitivity;
        set => this.RaiseAndSetIfChanged(ref viewerPanSensitivity, Math.Clamp(value, MinPanSens, MaxPanSens));
    }

    public double ViewerZoomStepFactor
    {
        get => viewerZoomStepFactor;
        set => this.RaiseAndSetIfChanged(ref viewerZoomStepFactor, Math.Clamp(value, MinZoomStep, MaxZoomStep));
    }

    public bool ViewerShowGrid
    {
        get => viewerShowGrid;
        set => this.RaiseAndSetIfChanged(ref viewerShowGrid, value);
    }

    public bool ViewerShowOrigin
    {
        get => viewerShowOrigin;
        set => this.RaiseAndSetIfChanged(ref viewerShowOrigin, value);
    }

    public bool ViewerShowBounds
    {
        get => viewerShowBounds;
        set => this.RaiseAndSetIfChanged(ref viewerShowBounds, value);
    }

    public double ViewerGridMinPixelStep
    {
        get => viewerGridMinPixelStep;
        set => this.RaiseAndSetIfChanged(ref viewerGridMinPixelStep, Math.Clamp(value, 4.0, 200.0));
    }

    public double ViewerFlattenTolerance
    {
        get => viewerFlattenTolerance;
        set => this.RaiseAndSetIfChanged(ref viewerFlattenTolerance, Math.Clamp(value, MinFlattenTol, MaxFlattenTol));
    }

    public bool ViewerEnableSimplification
    {
        get => viewerEnableSimplification;
        set => this.RaiseAndSetIfChanged(ref viewerEnableSimplification, value);
    }

    public double ViewerSimplificationEpsilon
    {
        get => viewerSimplificationEpsilon;
        set => this.RaiseAndSetIfChanged(ref viewerSimplificationEpsilon, Math.Clamp(value, 0.0, 10_000.0));
    }

    public uint ViewerRapidColor
    {
        get => viewerRapidColor;
        set => this.RaiseAndSetIfChanged(ref viewerRapidColor, value);
    }

    public uint ViewerCutColor
    {
        get => viewerCutColor;
        set => this.RaiseAndSetIfChanged(ref viewerCutColor, value);
    }

    public double ViewerRapidStrokeWidth
    {
        get => viewerRapidStrokeWidth;
        set => this.RaiseAndSetIfChanged(ref viewerRapidStrokeWidth, Math.Clamp(value, 0.1, 10.0));
    }

    public double ViewerCutStrokeWidth
    {
        get => viewerCutStrokeWidth;
        set => this.RaiseAndSetIfChanged(ref viewerCutStrokeWidth, Math.Clamp(value, 0.1, 10.0));
    }

    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }
    public ReactiveCommand<Unit, Unit> BrowseLastGCodeDirectoryCommand { get; }

    public event EventHandler<bool>? CloseRequested;

    private async System.Threading.Tasks.Task SaveAsync()
    {
        var s = new AppSettings
        {
            DefaultComPort = DefaultComPort,
            BaudRate = BaudRate,
            LastGCodeDirectory = LastGCodeDirectory,
            ViewerRotateSensitivity = ViewerRotateSensitivity,
            ViewerPanSensitivity = ViewerPanSensitivity,
            ViewerZoomStepFactor = ViewerZoomStepFactor,
            ViewerShowGrid = ViewerShowGrid,
            ViewerShowOrigin = ViewerShowOrigin,
            ViewerShowBounds = ViewerShowBounds,
            ViewerGridMinPixelStep = ViewerGridMinPixelStep,
            ViewerFlattenTolerance = ViewerFlattenTolerance,
            ViewerEnableSimplification = ViewerEnableSimplification,
            ViewerSimplificationEpsilon = ViewerSimplificationEpsilon,
            ViewerRapidColor = ViewerRapidColor,
            ViewerCutColor = ViewerCutColor,
            ViewerRapidStrokeWidth = ViewerRapidStrokeWidth,
            ViewerCutStrokeWidth = ViewerCutStrokeWidth
        };
        await settingsService.SaveAsync(s);
        CloseRequested?.Invoke(this, true);
    }

    private void Cancel()
    {
        CloseRequested?.Invoke(this, false);
    }

    private async System.Threading.Tasks.Task Load()
    {
        var s = await settingsService.LoadAsync();
        DefaultComPort = s.DefaultComPort;
        BaudRate = s.BaudRate;
        LastGCodeDirectory = s.LastGCodeDirectory;
        ViewerRotateSensitivity = s.ViewerRotateSensitivity;
        ViewerPanSensitivity = s.ViewerPanSensitivity;
        ViewerZoomStepFactor = s.ViewerZoomStepFactor;
        ViewerShowGrid = s.ViewerShowGrid;
        ViewerShowOrigin = s.ViewerShowOrigin;
        ViewerShowBounds = s.ViewerShowBounds;
        ViewerGridMinPixelStep = s.ViewerGridMinPixelStep;
        ViewerFlattenTolerance = s.ViewerFlattenTolerance;
        ViewerEnableSimplification = s.ViewerEnableSimplification;
        ViewerSimplificationEpsilon = s.ViewerSimplificationEpsilon;
        ViewerRapidColor = s.ViewerRapidColor;
        ViewerCutColor = s.ViewerCutColor;
        ViewerRapidStrokeWidth = s.ViewerRapidStrokeWidth;
        ViewerCutStrokeWidth = s.ViewerCutStrokeWidth;
    }

    private async System.Threading.Tasks.Task BrowseLastGCodeDirectoryAsync()
    {
        try
        {
            var selected = await PickFolderInteraction.Handle(Unit.Default).FirstAsync().ToTask();
            if (!string.IsNullOrWhiteSpace(selected))
            {
                LastGCodeDirectory = selected!;
            }
        }
        catch { }
    }
}
