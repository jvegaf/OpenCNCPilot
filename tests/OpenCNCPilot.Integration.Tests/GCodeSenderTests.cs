using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OpenCNCPilot.Hardware.Services;
using Xunit;

namespace OpenCNCPilot.Integration.Tests;

public class GCodeSenderTests
{
    private sealed class FakeSerial : ISerialPortService
    {
        public event EventHandler<string>? DataReceived;
        public event EventHandler<bool>? ConnectionStateChanged;

        public bool IsOpen { get; private set; } = true;
        public string? PortName => "FAKE";
        public int BaudRate => 115200;

        public string[] GetAvailablePorts() => new[] { "FAKE" };
        public void Open(string portName, int baudRate, System.IO.Ports.Parity parity = System.IO.Ports.Parity.None, int dataBits = 8, System.IO.Ports.StopBits stopBits = System.IO.Ports.StopBits.One)
            => IsOpen = true;
        public void Write(string data) { /* ignored */ }
        public void WriteLine(string data)
        {
            // Emular respuesta 'ok' con retardo para permitir pausar entre envíos
            Task.Run(async () => { await Task.Delay(100); DataReceived?.Invoke(this, "ok\n"); });
        }
        public void Close() { IsOpen = false; ConnectionStateChanged?.Invoke(this, false); }
        public void SetRtsEnable(bool enable) { }
        public void SetDtrEnable(bool enable) { }
        public void ClearInputBuffer() { }
        public void ClearOutputBuffer() { }
    }

    [Fact]
    public async Task Start_Should_Run_To_End_On_Ok_Responses()
    {
        var serial = new FakeSerial();
        using var sender = new GCodeSender(serial, NullLogger<GCodeSender>.Instance);
        sender.Load(new[] { "G0 X0", "G1 X1", "G1 X2" });

        sender.Start();

        // Esperar a que termine
        await Eventually(async () => sender.State == GCodeSenderState.Idle && sender.FilePosition == 3, TimeSpan.FromSeconds(1));

        sender.State.Should().Be(GCodeSenderState.Idle);
        sender.FilePosition.Should().Be(3);
        sender.FileLength.Should().Be(3);
    }

    [Fact]
    public async Task Pause_Should_Stop_Advancing_On_Ok()
    {
        var serial = new FakeSerial();
        using var sender = new GCodeSender(serial, NullLogger<GCodeSender>.Instance);
        sender.Load(new[] { "G0 X0", "G1 X1" });

        sender.Start();
        await Task.Delay(10);
        sender.Pause();

        var posAtPause = sender.FilePosition;

        // Enviar más 'ok' no debería avanzar
        await Task.Delay(30);
        sender.FilePosition.Should().Be(posAtPause);
        sender.State.Should().Be(GCodeSenderState.Paused);
    }

    [Fact]
    public async Task PauseOnHold_Should_Pause_After_Ok_When_MCode()
    {
        var serial = new FakeSerial();
        using var sender = new GCodeSender(serial, NullLogger<GCodeSender>.Instance) { PauseOnHold = true };
        sender.Load(new[] { "G0 X0", "M0", "G1 X2" });

        sender.Start();
        await Eventually(async () => sender.State == GCodeSenderState.Paused && sender.FilePosition == 1, TimeSpan.FromSeconds(1));

        sender.State.Should().Be(GCodeSenderState.Paused);
        sender.FilePosition.Should().Be(1); // se pausa tras completar la línea 0 y detectar M0 en la actual
    }

    [Fact]
    public async Task Error_Should_Pause_And_Raise_Event()
    {
        var serial = new FakeSerialWithErrorOnSecondLine();
        using var sender = new GCodeSender(serial, NullLogger<GCodeSender>.Instance);
        sender.Load(new[] { "G0 X0", "G1 X1", "G1 X2" });

        string? error = null;
        sender.ErrorOccurred += (_, msg) => error = msg;
        sender.Start();

        await Eventually(async () => sender.State == GCodeSenderState.Paused, TimeSpan.FromSeconds(1));
        error.Should().NotBeNull();
        sender.FilePosition.Should().Be(1); // no avanzó al 2
    }

    [Fact]
    public void Goto_Should_Work_When_Not_Sending()
    {
        var serial = new FakeSerial();
        using var sender = new GCodeSender(serial, NullLogger<GCodeSender>.Instance);
        sender.Load(new[] { "A", "B", "C" });
        sender.Goto(2);
        sender.FilePosition.Should().Be(2);

        Action act = () => sender.Start();
        act.Should().NotThrow();
    }

    [Fact]
    public void Goto_Should_Throw_When_Sending()
    {
        var serial = new FakeSerial();
        using var sender = new GCodeSender(serial, NullLogger<GCodeSender>.Instance);
        sender.Load(new[] { "A", "B" });
        sender.Start();
        Action act = () => sender.Goto(1);
        act.Should().Throw<InvalidOperationException>();
    }

    private sealed class FakeSerialWithErrorOnSecondLine : ISerialPortService
    {
        public event EventHandler<string>? DataReceived;
        public event EventHandler<bool>? ConnectionStateChanged;
        public bool IsOpen { get; private set; } = true;
        public string? PortName => "FAKE";
        public int BaudRate => 115200;
        public string[] GetAvailablePorts() => new[] { "FAKE" };
        public void Open(string portName, int baudRate, System.IO.Ports.Parity parity = System.IO.Ports.Parity.None, int dataBits = 8, System.IO.Ports.StopBits stopBits = System.IO.Ports.StopBits.One) => IsOpen = true;
        public void Write(string data) { }
        private int _count;
        public void WriteLine(string data)
        {
            _count++;
            Task.Run(async () =>
            {
                await Task.Delay(100);
                if (_count == 2) DataReceived?.Invoke(this, "error: oops\n");
                else DataReceived?.Invoke(this, "ok\n");
            });
        }
        public void Close() { IsOpen = false; ConnectionStateChanged?.Invoke(this, false); }
        public void SetRtsEnable(bool enable) { }
        public void SetDtrEnable(bool enable) { }
        public void ClearInputBuffer() { }
        public void ClearOutputBuffer() { }
    }

    private static async Task Eventually(Func<Task<bool>> condition, TimeSpan timeout)
    {
        var start = DateTime.UtcNow;
        while (DateTime.UtcNow - start < timeout)
        {
            if (await condition()) return;
            await Task.Delay(10);
        }
        throw new TimeoutException("Condition not met in time");
    }
}
