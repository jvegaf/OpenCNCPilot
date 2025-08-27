using System;

namespace OpenCNCPilot.Hardware.Services;

/// <summary>
/// Cross-platform serial port service abstraction
/// Provides hardware communication interface independent of platform
/// </summary>
public interface ISerialPortService
{
    /// <summary>
    /// Event raised when data is received from the serial port
    /// </summary>
    event EventHandler<string> DataReceived;

    /// <summary>
    /// Event raised when connection state changes
    /// </summary>
    event EventHandler<bool> ConnectionStateChanged;

    /// <summary>
    /// Gets whether the port is currently open
    /// </summary>
    bool IsOpen { get; }

    /// <summary>
    /// Gets the currently configured port name
    /// </summary>
    string? PortName { get; }

    /// <summary>
    /// Gets the currently configured baud rate
    /// </summary>
    int BaudRate { get; }

    /// <summary>
    /// Gets available serial ports on the system
    /// </summary>
    /// <returns>Array of available port names</returns>
    string[] GetAvailablePorts();

    /// <summary>
    /// Opens a serial connection with specified parameters
    /// </summary>
    /// <param name="portName">Port name to connect to</param>
    /// <param name="baudRate">Baud rate for communication</param>
    /// <param name="parity">Parity setting</param>
    /// <param name="dataBits">Data bits</param>
    /// <param name="stopBits">Stop bits</param>
    void Open(string portName, int baudRate, System.IO.Ports.Parity parity = System.IO.Ports.Parity.None, 
              int dataBits = 8, System.IO.Ports.StopBits stopBits = System.IO.Ports.StopBits.One);

    /// <summary>
    /// Writes data to the serial port
    /// </summary>
    /// <param name="data">Data to write</param>
    void Write(string data);

    /// <summary>
    /// Writes a line to the serial port (adds newline)
    /// </summary>
    /// <param name="data">Data to write</param>
    void WriteLine(string data);

    /// <summary>
    /// Closes the serial connection
    /// </summary>
    void Close();

    /// <summary>
    /// Sets RTS (Request to Send) control
    /// </summary>
    /// <param name="enable">True to enable RTS</param>
    void SetRtsEnable(bool enable);

    /// <summary>
    /// Sets DTR (Data Terminal Ready) control  
    /// </summary>
    /// <param name="enable">True to enable DTR</param>
    void SetDtrEnable(bool enable);

    /// <summary>
    /// Clears the input buffer
    /// </summary>
    void ClearInputBuffer();

    /// <summary>
    /// Clears the output buffer
    /// </summary>
    void ClearOutputBuffer();
}