using System;
using System.Reactive;
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

    // Rangos recomendados
    private const double MinRotateSens = 0.05;
    private const double MaxRotateSens = 2.0;
    private const double MinPanSens = 0.001;
    private const double MaxPanSens = 1.0;
    private const double MinZoomStep = 1.01;
    private const double MaxZoomStep = 1.5;

    public SettingsWindowViewModel(ISettingsService settingsService)
    {
        this.settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        SaveCommand = ReactiveCommand.Create(Save);
        CancelCommand = ReactiveCommand.Create(Cancel);
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

    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    public event EventHandler<bool>? CloseRequested;

    private async void Save()
    {
        var s = new AppSettings
        {
            DefaultComPort = DefaultComPort,
            BaudRate = BaudRate,
            LastGCodeDirectory = LastGCodeDirectory,
            ViewerRotateSensitivity = ViewerRotateSensitivity,
            ViewerPanSensitivity = ViewerPanSensitivity,
            ViewerZoomStepFactor = ViewerZoomStepFactor
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
    }
}
