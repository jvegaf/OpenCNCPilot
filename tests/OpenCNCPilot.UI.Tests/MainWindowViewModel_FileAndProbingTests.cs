using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using System.Reactive.Threading.Tasks;
using OpenCNCPilot.Core.GCode;
using OpenCNCPilot.Hardware.Services;
using OpenCNCPilot.UI.Services;
using OpenCNCPilot.UI.ViewModels;
using Xunit;

namespace OpenCNCPilot.UI.Tests;

[Trait("Category","UI-VM")]
public class MainWindowViewModel_FileAndProbingTests
{
    private sealed class DummySerial : ISerialPortService
    {
    #pragma warning disable CS0067
    public event EventHandler<bool>? ConnectionStateChanged;
    public event EventHandler<string>? DataReceived;
    #pragma warning restore CS0067
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
    public async Task ClearGCodeFileCommand_Resets_State()
    {
        var tmp = Path.GetTempFileName();
        await File.WriteAllLinesAsync(tmp, new[] { "G90", "G1 X1 Y1" });
        var dialogs = new DummyDialogs { NextOpenFiles = new[] { tmp } };
        var vm = new MainWindowViewModel(new NullLogger<MainWindowViewModel>(), new DummySerial(), dialogs, new InMemorySettings(), new GCodeParser());

    await vm.LoadGCodeFileCommand.Execute().ToTask();
    vm.GCodeCommandCount.Should().BeGreaterThanOrEqualTo(0);

        vm.ClearGCodeFileCommand.Execute().Subscribe();

        vm.GCodeCommandCount.Should().Be(0);
        vm.CurrentFileName.Should().Be("(none)");
    }

    [Fact]
    public void Probing_Properties_Are_Clamped()
    {
        var vm = new MainWindowViewModel(new NullLogger<MainWindowViewModel>(), new DummySerial(), new DummyDialogs(), new InMemorySettings(), new GCodeParser());

        vm.ProbeGridX = -5; vm.ProbeGridX.Should().Be(1);
        vm.ProbeGridY = 2000; vm.ProbeGridY.Should().Be(1000);
        vm.ProbeAreaWidth = 0; vm.ProbeAreaWidth.Should().Be(1);
        vm.ProbeAreaHeight = 200000; vm.ProbeAreaHeight.Should().Be(100000);
    }
}
