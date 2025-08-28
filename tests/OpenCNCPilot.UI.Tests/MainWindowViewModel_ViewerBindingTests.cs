using System;
using System.IO;
using System.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OpenCNCPilot.Core.GCode;
using OpenCNCPilot.Hardware.Services;
using OpenCNCPilot.UI.Services;
using OpenCNCPilot.UI.ViewModels;
using Xunit;

namespace OpenCNCPilot.UI.Tests;

public class MainWindowViewModel_ViewerBindingTests
{
    private sealed class DummySerial : ISerialPortService
    {
        public event EventHandler<bool>? ConnectionStateChanged;
        public event EventHandler<string>? DataReceived;
        public bool IsOpen => false;
        public string? PortName => null;
        public int BaudRate => 115200;
        public void Close() { }
        public string[] GetAvailablePorts() => Array.Empty<string>();
        public void Open(string portName, int baudRate, System.IO.Ports.Parity parity = System.IO.Ports.Parity.None, int dataBits = 8, System.IO.Ports.StopBits stopBits = System.IO.Ports.StopBits.One) { }
        public void Write(string data) { }
        public void WriteLine(string data) { }
        public void SetRtsEnable(bool enable) { }
        public void SetDtrEnable(bool enable) { }
        public void ClearInputBuffer() { }
        public void ClearOutputBuffer() { }
    }

    private sealed class DummyDialogs : IDialogService
    {
        public string[]? NextOpenFiles { get; set; }
        public Task AlertAsync(string title, string message, string okText = "OK") => Task.CompletedTask;
        public Task<bool> ConfirmAsync(string title, string message, string confirmText = "OK", string cancelText = "Cancel") => Task.FromResult(true);
        public Task<string[]?> OpenFilesAsync(string title, string? initialDirectory = null, string[]? filters = null, bool allowMultiple = false) => Task.FromResult(NextOpenFiles);
        public Task<string?> PickFolderAsync(string title, string? initialDirectory = null) => Task.FromResult<string?>(null);
        public Task<double?> PromptNumberAsync(string title, string message, double? defaultValue = null, double? min = null, double? max = null, int decimals = 3) => Task.FromResult<double?>(null);
        public Task<string?> SaveFileAsync(string title, string? initialDirectory = null, string? defaultFileName = null, string[]? filters = null) => Task.FromResult<string?>(null);
        public Task ShowWarningsAsync(string header, System.Collections.Generic.IEnumerable<string> warnings) => Task.CompletedTask;
    }

    private sealed class InMemorySettings : ISettingsService
    {
        public AppSettings Settings { get; } = new() { LastGCodeDirectory = Path.GetTempPath() };
        public Task<AppSettings> LoadAsync() => Task.FromResult(Settings);
        public Task SaveAsync(AppSettings settings) { return Task.CompletedTask; }
    }

    [Fact]
    public async Task After_Loading_File_GCodeCommands_Are_Populated()
    {
        var tmp = Path.GetTempFileName();
        await File.WriteAllLinesAsync(tmp, new[] { "G90", "G21", "G0 X0 Y0", "F100", "G1 X10 Y0" });

        var dialogs = new DummyDialogs { NextOpenFiles = new[] { tmp } };
        var vm = new MainWindowViewModel(new NullLogger<MainWindowViewModel>(), new DummySerial(), dialogs, new InMemorySettings(), new GCodeParser());
        await vm.LoadGCodeFileCommand.Execute().ToTask();

        vm.GCodeCommands.Should().NotBeNull();
        vm.GCodeCommands!.Count.Should().BeGreaterThan(0);
        vm.GCodeCommands!.Any(c => c is OpenCNCPilot.Core.GCode.GCodeCommands.Line).Should().BeTrue();
    }
}
