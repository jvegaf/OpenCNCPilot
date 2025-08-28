using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using OpenCNCPilot.Core.GCode;
using OpenCNCPilot.Core.GCode.GCodeCommands;
using OpenCNCPilot.Core.Geometry;
using OpenCNCPilot.Hardware.Services;
using OpenCNCPilot.UI.Services;
using ReactiveUI;

namespace OpenCNCPilot.UI.ViewModels;

/// <summary>
/// Main window view model implementing MVVM pattern with ReactiveUI
/// </summary>
public class MainWindowViewModel : ReactiveObject
{
    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly ISerialPortService _serialPortService;
    private readonly IDialogService _dialogService;
    private readonly ISettingsService _settingsService;
    private readonly IGCodeParser _gcodeParser;

    private ReadOnlyObservableCollection<Command>? _gcodeCommands;
    private string _selectedPort = string.Empty;
    private string _status = "Disconnected";
    private bool _isConnected = false;
    private Vector3 _machinePosition = Vector3.Origin;
    private Vector3 _workPosition = Vector3.Origin;
    private double _viewerZoom = 1.0;
    private int _fitRequestId = 0;

    public MainWindowViewModel(ILogger<MainWindowViewModel> logger, ISerialPortService serialPortService, IDialogService dialogService, ISettingsService settingsService, IGCodeParser gcodeParser)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serialPortService = serialPortService ?? throw new ArgumentNullException(nameof(serialPortService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _gcodeParser = gcodeParser ?? throw new ArgumentNullException(nameof(gcodeParser));

        // Initialize commands
        var canConnectObs = this
            .WhenAnyValue(x => x.SelectedPort, x => x.IsConnected,
                (port, connected) => !connected && !string.IsNullOrEmpty(port))
            .ObserveOn(RxApp.MainThreadScheduler);

        ConnectCommand = ReactiveCommand.Create(Connect, canConnectObs, RxApp.MainThreadScheduler);
        var canDisconnectObs = this
            .WhenAnyValue(x => x.IsConnected)
            .ObserveOn(RxApp.MainThreadScheduler);

        DisconnectCommand = ReactiveCommand.Create(Disconnect, canDisconnectObs, RxApp.MainThreadScheduler);
        RefreshPortsCommand = ReactiveCommand.Create(RefreshPorts);
        OpenSettingsCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            var vm = (SettingsWindowViewModel?)App.Services?.GetService(typeof(SettingsWindowViewModel))
                     ?? new SettingsWindowViewModel(new UI.Services.JsonSettingsService());
            var result = await ShowSettings.Handle(vm);
            _logger.LogInformation("Settings dialog closed with result: {Result}", result);
        });

        // Viewer controls
        FitToViewCommand = ReactiveCommand.Create(() => { FitRequestId++; });
        ZoomInCommand = ReactiveCommand.Create(() => { ViewerZoom = Math.Max(1e-6, ViewerZoom * 0.8); });
        ZoomOutCommand = ReactiveCommand.Create(() => { ViewerZoom = Math.Max(1e-6, ViewerZoom / 0.8); });
        ZoomResetCommand = ReactiveCommand.Create(() => { ViewerZoom = 1.0; });

        // Add demo warnings command for manual verification
        DemoWarningsCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            var sample = new System.Collections.Generic.List<string>
            {
                "ignoring unknown word (letter): \"Q\". (line 42)",
                "spindle speed must be positive. (line 1337)",
                "motion command must be last in line (ignoring unused words). (line 7)"
            };
            await ShowParseWarningsAsync(sample);
        });

        // Load G-Code command: pick file, update last directory, parse con Core y mostrar warnings
        LoadGCodeFileCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            var settings = await _settingsService.LoadAsync();
            var filters = new[] { "G-Code (*.gcode;*.nc;*.ngc)|*.gcode;*.nc;*.ngc", "All Files (*.*)|*.*" };
            var files = await _dialogService.OpenFilesAsync("Open G-Code", settings.LastGCodeDirectory, filters, allowMultiple: false);
            if (files == null || files.Length == 0)
                return;

            var file = files[0];
            try
            {
                var dir = Path.GetDirectoryName(file);
                if (!string.IsNullOrEmpty(dir))
                {
                    settings.LastGCodeDirectory = dir!;
                    await _settingsService.SaveAsync(settings);
                }

                var lines = await File.ReadAllLinesAsync(file);
                try
                {
                    _gcodeParser.IgnoreAdditionalAxes = settings.IgnoreAdditionalAxes;
                    _gcodeParser.Parse(lines);
                }
                catch (ParseException pex)
                {
                    _logger.LogWarning(pex, "GCode parse error");
                    await _dialogService.AlertAsync("Parse Error", pex.Message);
                    return;
                }

                if (_gcodeParser.Warnings.Count > 0)
                    await ShowParseWarningsAsync(_gcodeParser.Warnings);

                // Exponer comandos parseados a la vista (snapshot de solo lectura)
                var list = new ObservableCollection<Command>(_gcodeParser.Commands);
                GCodeCommands = new ReadOnlyObservableCollection<Command>(list);

                _logger.LogInformation("Loaded G-Code file: {File}", file);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load G-Code file: {File}", file);
                await _dialogService.AlertAsync("Error", $"Could not load G-Code file.\n{ex.Message}");
            }
        });

        // Subscribe to serial port events
        _serialPortService.ConnectionStateChanged += OnConnectionStateChanged;
        _serialPortService.DataReceived += OnDataReceived;

        // Initialize data
        RefreshPorts();

        _logger.LogInformation("MainWindowViewModel initialized");
    }

    #region Properties

    public string WindowTitle => "OpenCNCPilot - Cross Platform";

    public ObservableCollection<string> AvailablePorts { get; } = new();

    public string SelectedPort
    {
        get => _selectedPort;
        set => this.RaiseAndSetIfChanged(ref _selectedPort, value);
    }

    public string Status
    {
        get => _status;
        set => this.RaiseAndSetIfChanged(ref _status, value);
    }

    public bool IsConnected
    {
        get => _isConnected;
        private set => this.RaiseAndSetIfChanged(ref _isConnected, value);
    }

    public bool CanConnect => !IsConnected && !string.IsNullOrEmpty(SelectedPort);

    public Vector3 MachinePosition
    {
        get => _machinePosition;
        private set => this.RaiseAndSetIfChanged(ref _machinePosition, value);
    }

    public Vector3 WorkPosition
    {
        get => _workPosition;
        private set => this.RaiseAndSetIfChanged(ref _workPosition, value);
    }

    public ReadOnlyObservableCollection<Command>? GCodeCommands
    {
        get => _gcodeCommands;
        private set => this.RaiseAndSetIfChanged(ref _gcodeCommands, value);
    }

    public double ViewerZoom
    {
        get => _viewerZoom;
        set => this.RaiseAndSetIfChanged(ref _viewerZoom, value);
    }

    public int FitRequestId
    {
        get => _fitRequestId;
        set => this.RaiseAndSetIfChanged(ref _fitRequestId, value);
    }

    #endregion

    #region Commands

    public ReactiveCommand<Unit, Unit> ConnectCommand { get; }
    public ReactiveCommand<Unit, Unit> DisconnectCommand { get; }
    public ReactiveCommand<Unit, Unit> RefreshPortsCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenSettingsCommand { get; }
    public ReactiveCommand<Unit, Unit> DemoWarningsCommand { get; }
    public ReactiveCommand<Unit, Unit> LoadGCodeFileCommand { get; }
    public ReactiveCommand<Unit, Unit> FitToViewCommand { get; }
    public ReactiveCommand<Unit, Unit> ZoomInCommand { get; }
    public ReactiveCommand<Unit, Unit> ZoomOutCommand { get; }
    public ReactiveCommand<Unit, Unit> ZoomResetCommand { get; }

    public Interaction<SettingsWindowViewModel, bool?> ShowSettings { get; } = new();

    #endregion

    #region Command Implementations

    private void Connect()
    {
        try
        {
            _logger.LogInformation("Attempting to connect to port {Port}", SelectedPort);
            Status = "Connecting...";
            _serialPortService.Open(SelectedPort, 115200); // Standard GRBL baud rate
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to port {Port}", SelectedPort);
            Status = "Connection failed";
        }
    }

    private void Disconnect()
    {
        try
        {
            _logger.LogInformation("Disconnecting from port");
            _serialPortService.Close();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during disconnect");
        }
    }

    private void RefreshPorts()
    {
        try
        {
            AvailablePorts.Clear();
            var ports = _serialPortService.GetAvailablePorts();

            foreach (var port in ports)
            {
                AvailablePorts.Add(port);
            }

            // Auto-select first port if available and none selected
            if (AvailablePorts.Count > 0 && string.IsNullOrEmpty(SelectedPort))
            {
                SelectedPort = AvailablePorts[0];
            }

            _logger.LogInformation("Found {PortCount} serial ports", ports.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing serial ports");
        }
    }

    #endregion

    #region Event Handlers

    private void OnConnectionStateChanged(object? sender, bool isConnected)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsConnected = isConnected;
            Status = isConnected ? "Connected" : "Disconnected";
            this.RaisePropertyChanged(nameof(CanConnect));
            _logger.LogInformation("Connection state changed to {IsConnected}", isConnected);
        });
    }

    private void OnDataReceived(object? sender, string data)
    {
        // TODO: Parse GRBL responses and update position/status
        // For now, just log the received data
        Dispatcher.UIThread.Post(() =>
        {
            _logger.LogDebug("Received data: {Data}", data.Trim());
        });
    }

    #endregion

    #region Warning Flows

    public async System.Threading.Tasks.Task ShowParseWarningsAsync(System.Collections.Generic.IEnumerable<string> warnings)
    {
        if (warnings == null)
            return;

        const string header = "Warning! Parsing this file resulted in some warnings!\n\nDo not use OpenCNCPilot's edit functions unless you are sure that these warnings can be ignored!\n\nBe aware that the affected lines will likely move when using edit functions.";
        await _dialogService.ShowWarningsAsync(header, warnings);
    }

    #endregion
}
