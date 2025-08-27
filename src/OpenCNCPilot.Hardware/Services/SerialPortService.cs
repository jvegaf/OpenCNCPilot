using System;
using System.IO.Ports;
using Microsoft.Extensions.Logging;

namespace OpenCNCPilot.Hardware.Services;

/// <summary>
/// Cross-platform serial port service implementation using System.IO.Ports
/// Provides robust serial communication with proper error handling
/// </summary>
public class SerialPortService : ISerialPortService, IDisposable
{
    private readonly ILogger<SerialPortService> _logger;
    private SerialPort? _serialPort;
    private readonly object _lockObject = new object();
    private bool _disposed = false;

    public event EventHandler<string>? DataReceived;
    public event EventHandler<bool>? ConnectionStateChanged;

    public bool IsOpen => _serialPort?.IsOpen ?? false;
    public string? PortName => _serialPort?.PortName;
    public int BaudRate => _serialPort?.BaudRate ?? 0;

    public SerialPortService(ILogger<SerialPortService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string[] GetAvailablePorts()
    {
        try
        {
            var ports = SerialPort.GetPortNames();
            _logger.LogDebug("Found {PortCount} serial ports", ports.Length);
            return ports;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available serial ports");
            return Array.Empty<string>();
        }
    }

    public void Open(string portName, int baudRate, Parity parity = Parity.None, 
                     int dataBits = 8, StopBits stopBits = StopBits.One)
    {
        if (string.IsNullOrEmpty(portName))
            throw new ArgumentException("Port name cannot be null or empty", nameof(portName));

        lock (_lockObject)
        {
            try
            {
                // Close existing connection if open
                Close();

                _logger.LogInformation("Opening serial port {PortName} at {BaudRate} baud", portName, baudRate);

                _serialPort = new SerialPort(portName, baudRate, parity, dataBits, stopBits)
                {
                    ReadTimeout = 1000,
                    WriteTimeout = 1000,
                    NewLine = "\n",
                    Encoding = System.Text.Encoding.ASCII
                };

                _serialPort.DataReceived += OnDataReceived;
                _serialPort.ErrorReceived += OnErrorReceived;

                _serialPort.Open();

                _logger.LogInformation("Serial port {PortName} opened successfully", portName);
                ConnectionStateChanged?.Invoke(this, true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open serial port {PortName}", portName);
                Close();
                throw;
            }
        }
    }

    public void Write(string data)
    {
        if (string.IsNullOrEmpty(data)) return;

        lock (_lockObject)
        {
            if (!IsOpen)
            {
                _logger.LogWarning("Attempted to write to closed serial port");
                return;
            }

            try
            {
                _serialPort!.Write(data);
                _logger.LogTrace("Wrote data to serial port: {Data}", data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error writing to serial port");
                throw;
            }
        }
    }

    public void WriteLine(string data)
    {
        if (data == null) data = string.Empty;
        Write(data + "\n");
    }

    public void Close()
    {
        lock (_lockObject)
        {
            if (_serialPort != null)
            {
                try
                {
                    if (_serialPort.IsOpen)
                    {
                        _logger.LogInformation("Closing serial port {PortName}", _serialPort.PortName);
                        _serialPort.Close();
                    }
                    
                    _serialPort.DataReceived -= OnDataReceived;
                    _serialPort.ErrorReceived -= OnErrorReceived;
                    _serialPort.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error closing serial port");
                }
                finally
                {
                    _serialPort = null;
                    ConnectionStateChanged?.Invoke(this, false);
                }
            }
        }
    }

    public void SetRtsEnable(bool enable)
    {
        lock (_lockObject)
        {
            if (IsOpen)
            {
                try
                {
                    _serialPort!.RtsEnable = enable;
                    _logger.LogDebug("Set RTS to {RtsState}", enable);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error setting RTS");
                }
            }
        }
    }

    public void SetDtrEnable(bool enable)
    {
        lock (_lockObject)
        {
            if (IsOpen)
            {
                try
                {
                    _serialPort!.DtrEnable = enable;
                    _logger.LogDebug("Set DTR to {DtrState}", enable);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error setting DTR");
                }
            }
        }
    }

    public void ClearInputBuffer()
    {
        lock (_lockObject)
        {
            if (IsOpen)
            {
                try
                {
                    _serialPort!.DiscardInBuffer();
                    _logger.LogDebug("Cleared input buffer");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error clearing input buffer");
                }
            }
        }
    }

    public void ClearOutputBuffer()
    {
        lock (_lockObject)
        {
            if (IsOpen)
            {
                try
                {
                    _serialPort!.DiscardOutBuffer();
                    _logger.LogDebug("Cleared output buffer");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error clearing output buffer");
                }
            }
        }
    }

    private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            if (_serialPort?.IsOpen == true)
            {
                string data = _serialPort.ReadExisting();
                if (!string.IsNullOrEmpty(data))
                {
                    _logger.LogTrace("Received data: {Data}", data.Replace("\n", "\\n").Replace("\r", "\\r"));
                    DataReceived?.Invoke(this, data);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in data received handler");
        }
    }

    private void OnErrorReceived(object sender, SerialErrorReceivedEventArgs e)
    {
        _logger.LogError("Serial port error: {ErrorType}", e.EventType);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Close();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }

    ~SerialPortService()
    {
        Dispose();
    }
}