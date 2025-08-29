using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
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
    public FileViewModel FileTab { get; }

    private ReadOnlyObservableCollection<Command>? _gcodeCommands;
    private string _selectedPort = string.Empty;
    private string _status = "Disconnected";
    private bool _isConnected = false;
    private Vector3 _machinePosition = Vector3.Origin;
    private Vector3 _workPosition = Vector3.Origin;
    private double _viewerZoom = 1.0;
    private int _fitRequestId = 0;
    private double _viewerRotationX = 30.0;
    private double _viewerRotationY = 45.0;
    private double _viewerPanX = 0.0;
    private double _viewerPanY = 0.0;
    private double _viewerRotateSensitivity = 0.3;
    private double _viewerPanSensitivity = 0.02;
    private double _viewerZoomStepFactor = 1.1;
    private bool _viewerShowGrid = true;
    private bool _viewerShowOrigin = true;
    private bool _viewerShowBounds = false;
    private double _viewerGridMinPixelStep = 30.0;
    private double _viewerFlattenTolerance = 0.05;
    private string _currentFilePath = string.Empty;
    private int _probeGridX = 5;
    private int _probeGridY = 5;
    private double _probeAreaWidth = 100.0;
    private double _probeAreaHeight = 100.0;

    private sealed class NoopSender : OpenCNCPilot.Hardware.Services.IGCodeSender
    {
        public OpenCNCPilot.Hardware.Services.GCodeSenderState State => OpenCNCPilot.Hardware.Services.GCodeSenderState.Idle;
        public int FilePosition => 0;
        public int FileLength => 0;
        public TimeSpan Runtime => TimeSpan.Zero;
        public TimeSpan EstimatedDuration => TimeSpan.Zero;
        public bool PauseOnHold { get; set; }
        public bool IsSending => false;
        public string CurrentLineText => string.Empty;
        #pragma warning disable CS0067 // event is never used in Noop dummy implementation
        public event EventHandler<OpenCNCPilot.Hardware.Services.GCodeSenderState>? StateChanged;
        public event EventHandler<int>? PositionChanged;
        public event EventHandler<string>? ErrorOccurred;
        #pragma warning restore CS0067
        public void Clear() { }
        public void Dispose() { }
        public void Goto(int lineIndex) { }
        public void Load(System.Collections.Generic.IEnumerable<string> lines) { }
        public void Pause() { }
        public void Start() { }
    }

    public MainWindowViewModel(ILogger<MainWindowViewModel> logger, ISerialPortService serialPortService, IDialogService dialogService, ISettingsService settingsService, IGCodeParser gcodeParser, FileViewModel? fileTab = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serialPortService = serialPortService ?? throw new ArgumentNullException(nameof(serialPortService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _gcodeParser = gcodeParser ?? throw new ArgumentNullException(nameof(gcodeParser));
        var pathBuilder = (IGCodePathBuilder?)App.Services?.GetService(typeof(IGCodePathBuilder)) ?? new GCodePathBuilder();
        FileTab = fileTab ?? new FileViewModel(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<FileViewModel>.Instance,
            _gcodeParser,
            new NoopSender(),
            _dialogService,
            _settingsService,
            pathBuilder);
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
            void OnClose(object? s, bool saved)
            {
                if (saved)
                {
                    // refresh sensitivities from persisted settings
                    _ = Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        var s1 = await _settingsService.LoadAsync();
                        ViewerRotateSensitivity = s1.ViewerRotateSensitivity;
                        ViewerPanSensitivity = s1.ViewerPanSensitivity;
                        ViewerZoomStepFactor = s1.ViewerZoomStepFactor;
                        ViewerShowGrid = s1.ViewerShowGrid;
                        ViewerShowOrigin = s1.ViewerShowOrigin;
                        ViewerShowBounds = s1.ViewerShowBounds;
                        ViewerGridMinPixelStep = s1.ViewerGridMinPixelStep;
                        ViewerFlattenTolerance = s1.ViewerFlattenTolerance;
                    });
                }
                vm.CloseRequested -= OnClose;
            }
            vm.CloseRequested += OnClose;
            var result = await ShowSettings.Handle(vm);
            _logger.LogInformation("Settings dialog closed with result: {Result}", result);
        });

        OpenGrblSettingsCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            var gvm = (GrblSettingsViewModel?)App.Services?.GetService(typeof(GrblSettingsViewModel))
                      ?? new GrblSettingsViewModel(_dialogService);

            Action<string> sendHandler = s => _serialPortService.WriteLine(s);
            gvm.SendLine += sendHandler;

            EventHandler<string>? recvHandler = null;
            recvHandler = (_, data) =>
            {
                if (string.IsNullOrEmpty(data)) return;
                var lines = data.Split(Core.Constants.NewLines, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    var t = line.Trim();
                    if (t.StartsWith("$")) gvm.LineReceived(t);
                }
            };
            _serialPortService.DataReceived += recvHandler;

            try
            {
                if (_serialPortService.IsOpen)
                    _serialPortService.WriteLine("$$");
                await ShowGrblSettings.Handle(gvm);
            }
            finally
            {
                _serialPortService.DataReceived -= recvHandler;
                gvm.SendLine -= sendHandler;
            }
        });

        // Sync viewport with File tab parsed commands
        this.WhenAnyValue(x => x.FileTab.ParsedCommands)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(cmds =>
            {
                GCodeCommands = cmds;
                // Disparar autofit tanto al abrir como al limpiar
                FitRequestId++;
            });

        // Viewer controls
        FitToViewCommand = ReactiveCommand.Create(() => { FitRequestId++; });
        ZoomInCommand = ReactiveCommand.Create(() => { ViewerZoom = Math.Max(1e-6, ViewerZoom * 0.8); });
        ZoomOutCommand = ReactiveCommand.Create(() => { ViewerZoom = Math.Max(1e-6, ViewerZoom / 0.8); });
        ZoomResetCommand = ReactiveCommand.Create(() => { ViewerZoom = 1.0; });
        ResetViewCommand = ReactiveCommand.Create(() =>
        {
            ViewerZoom = 1.0;
            ViewerRotationX = 30.0;
            ViewerRotationY = 45.0;
            ViewerPanX = 0.0;
            ViewerPanY = 0.0;
        });
        SnapTo2DCommand = ReactiveCommand.Create(() =>
        {
            ViewerRotationX = 0.0;
            ViewerRotationY = 0.0;
        });
        FitAndResetViewCommand = ReactiveCommand.Create(() =>
        {
            ViewerRotationX = 30.0;
            ViewerRotationY = 45.0;
            ViewerPanX = 0.0;
            ViewerPanY = 0.0;
            FitRequestId++;
        });
    RotateLeftCommand = ReactiveCommand.Create(() => { ViewerRotationY -= 5.0; });
    RotateRightCommand = ReactiveCommand.Create(() => { ViewerRotationY += 5.0; });
    RotateUpCommand = ReactiveCommand.Create(() => { ViewerRotationX = Math.Clamp(ViewerRotationX - 5.0, -89.0, 89.0); });
    RotateDownCommand = ReactiveCommand.Create(() => { ViewerRotationX = Math.Clamp(ViewerRotationX + 5.0, -89.0, 89.0); });
    PanLeftCommand = ReactiveCommand.Create(() => { ViewerPanX -= 5.0; });
    PanRightCommand = ReactiveCommand.Create(() => { ViewerPanX += 5.0; });
    PanUpCommand = ReactiveCommand.Create(() => { ViewerPanY += 5.0; });
    PanDownCommand = ReactiveCommand.Create(() => { ViewerPanY -= 5.0; });

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

        // Delegate to FileTab.OpenCommand so there is a single source of truth
        LoadGCodeFileCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            await FileTab.OpenCommand.Execute()
                .Catch(Observable.Empty<Unit>())
                .ToTask();
        });

        ClearGCodeFileCommand = ReactiveCommand.Create(() =>
        {
            FileTab.ClearCommand.Execute().Subscribe();
        });

        StartProbeCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            await _dialogService.AlertAsync("Probing", "La función de probing aún no está implementada en esta fase.");
        });

        // Subscribe to serial port events
        _serialPortService.ConnectionStateChanged += OnConnectionStateChanged;
        _serialPortService.DataReceived += OnDataReceived;

        // Initialize data
        RefreshPorts();

        // Load viewer sensitivities from settings
        _ = LoadViewerSettingsAsync();

        _logger.LogInformation("MainWindowViewModel initialized");
    }

    // Backwards-compatible overload for tests and callers that haven't been updated yet
    public MainWindowViewModel(ILogger<MainWindowViewModel> logger, ISerialPortService serialPortService, IDialogService dialogService, ISettingsService settingsService, IGCodeParser gcodeParser)
        : this(logger, serialPortService, dialogService, settingsService, gcodeParser, null)
    {
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

    public double ViewerRotationX
    {
        get => _viewerRotationX;
        set => this.RaiseAndSetIfChanged(ref _viewerRotationX, value);
    }

    public double ViewerRotationY
    {
        get => _viewerRotationY;
        set => this.RaiseAndSetIfChanged(ref _viewerRotationY, value);
    }

    public double ViewerPanX
    {
        get => _viewerPanX;
        set => this.RaiseAndSetIfChanged(ref _viewerPanX, value);
    }

    public double ViewerPanY
    {
        get => _viewerPanY;
        set => this.RaiseAndSetIfChanged(ref _viewerPanY, value);
    }

    public double ViewerRotateSensitivity
    {
        get => _viewerRotateSensitivity;
        set => this.RaiseAndSetIfChanged(ref _viewerRotateSensitivity, value);
    }

    public double ViewerPanSensitivity
    {
        get => _viewerPanSensitivity;
        set => this.RaiseAndSetIfChanged(ref _viewerPanSensitivity, value);
    }

    public double ViewerZoomStepFactor
    {
        get => _viewerZoomStepFactor;
        set => this.RaiseAndSetIfChanged(ref _viewerZoomStepFactor, value);
    }

    public bool ViewerShowGrid
    {
        get => _viewerShowGrid;
        set => this.RaiseAndSetIfChanged(ref _viewerShowGrid, value);
    }

    public bool ViewerShowOrigin
    {
        get => _viewerShowOrigin;
        set => this.RaiseAndSetIfChanged(ref _viewerShowOrigin, value);
    }

    public bool ViewerShowBounds
    {
        get => _viewerShowBounds;
        set => this.RaiseAndSetIfChanged(ref _viewerShowBounds, value);
    }

    public double ViewerGridMinPixelStep
    {
        get => _viewerGridMinPixelStep;
        set => this.RaiseAndSetIfChanged(ref _viewerGridMinPixelStep, value);
    }

    public double ViewerFlattenTolerance
    {
        get => _viewerFlattenTolerance;
        set => this.RaiseAndSetIfChanged(ref _viewerFlattenTolerance, value);
    }

    public string CurrentFilePath
    {
        get => _currentFilePath;
        private set => this.RaiseAndSetIfChanged(ref _currentFilePath, value);
    }

    public string CurrentFileName => string.IsNullOrEmpty(CurrentFilePath) ? "(none)" : Path.GetFileName(CurrentFilePath);

    public int GCodeCommandCount => GCodeCommands?.Count ?? 0;

    #endregion

    #region Commands

    public ReactiveCommand<Unit, Unit> ConnectCommand { get; }
    public ReactiveCommand<Unit, Unit> DisconnectCommand { get; }
    public ReactiveCommand<Unit, Unit> RefreshPortsCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenSettingsCommand { get; }
    public ReactiveCommand<Unit, Unit> DemoWarningsCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenGrblSettingsCommand { get; }
    public ReactiveCommand<Unit, Unit> LoadGCodeFileCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearGCodeFileCommand { get; }
    public ReactiveCommand<Unit, Unit> FitToViewCommand { get; }
    public ReactiveCommand<Unit, Unit> ZoomInCommand { get; }
    public ReactiveCommand<Unit, Unit> ZoomOutCommand { get; }
    public ReactiveCommand<Unit, Unit> ZoomResetCommand { get; }
    public ReactiveCommand<Unit, Unit> ResetViewCommand { get; }
    public ReactiveCommand<Unit, Unit> SnapTo2DCommand { get; }
    public ReactiveCommand<Unit, Unit> FitAndResetViewCommand { get; }
    public ReactiveCommand<Unit, Unit> StartProbeCommand { get; }
    public ReactiveCommand<Unit, Unit> RotateLeftCommand { get; }
    public ReactiveCommand<Unit, Unit> RotateRightCommand { get; }
    public ReactiveCommand<Unit, Unit> RotateUpCommand { get; }
    public ReactiveCommand<Unit, Unit> RotateDownCommand { get; }
    public ReactiveCommand<Unit, Unit> PanLeftCommand { get; }
    public ReactiveCommand<Unit, Unit> PanRightCommand { get; }
    public ReactiveCommand<Unit, Unit> PanUpCommand { get; }
    public ReactiveCommand<Unit, Unit> PanDownCommand { get; }

    public Interaction<SettingsWindowViewModel, bool?> ShowSettings { get; } = new();
    public Interaction<GrblSettingsViewModel, bool?> ShowGrblSettings { get; } = new();

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

    #region Helpers

    private async System.Threading.Tasks.Task LoadViewerSettingsAsync()
    {
        try
        {
            var s = await _settingsService.LoadAsync();
            ViewerRotateSensitivity = s.ViewerRotateSensitivity;
            ViewerPanSensitivity = s.ViewerPanSensitivity;
            ViewerZoomStepFactor = s.ViewerZoomStepFactor;
            ViewerShowGrid = s.ViewerShowGrid;
            ViewerShowOrigin = s.ViewerShowOrigin;
            ViewerShowBounds = s.ViewerShowBounds;
            ViewerGridMinPixelStep = s.ViewerGridMinPixelStep;
            ViewerFlattenTolerance = s.ViewerFlattenTolerance;
        }
        catch { }
    }

    #endregion

    #region Probing Settings

    public int ProbeGridX
    {
        get => _probeGridX;
        set => this.RaiseAndSetIfChanged(ref _probeGridX, Math.Clamp(value, 1, 1000));
    }

    public int ProbeGridY
    {
        get => _probeGridY;
        set => this.RaiseAndSetIfChanged(ref _probeGridY, Math.Clamp(value, 1, 1000));
    }

    public double ProbeAreaWidth
    {
        get => _probeAreaWidth;
        set => this.RaiseAndSetIfChanged(ref _probeAreaWidth, Math.Clamp(value, 1, 100000));
    }

    public double ProbeAreaHeight
    {
        get => _probeAreaHeight;
        set => this.RaiseAndSetIfChanged(ref _probeAreaHeight, Math.Clamp(value, 1, 100000));
    }

    #endregion
}
