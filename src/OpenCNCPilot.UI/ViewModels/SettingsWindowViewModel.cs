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

    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    public event EventHandler<bool>? CloseRequested;

    private async void Save()
    {
        var s = new AppSettings
        {
            DefaultComPort = DefaultComPort,
            BaudRate = BaudRate,
            LastGCodeDirectory = LastGCodeDirectory
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
    }
}
