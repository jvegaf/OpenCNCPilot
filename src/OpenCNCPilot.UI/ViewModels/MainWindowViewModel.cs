using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;
using Microsoft.Extensions.Logging;
using OpenCNCPilot.Hardware.Services;
using OpenCNCPilot.Core.Geometry;
using Avalonia.Threading;

namespace OpenCNCPilot.UI.ViewModels;

/// <summary>
/// Main window view model implementing MVVM pattern with ReactiveUI
/// </summary>
public class MainWindowViewModel : ReactiveObject
{
    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly ISerialPortService _serialPortService;

    private string _selectedPort = string.Empty;
    private string _status = "Disconnected";
    private bool _isConnected = false;
    private Vector3 _machinePosition = Vector3.Origin;
    private Vector3 _workPosition = Vector3.Origin;

    public MainWindowViewModel(ILogger<MainWindowViewModel> logger, ISerialPortService serialPortService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serialPortService = serialPortService ?? throw new ArgumentNullException(nameof(serialPortService));

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

    #endregion

    #region Commands

    public ReactiveCommand<Unit, Unit> ConnectCommand { get; }
    public ReactiveCommand<Unit, Unit> DisconnectCommand { get; }
    public ReactiveCommand<Unit, Unit> RefreshPortsCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenSettingsCommand { get; }

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
}
