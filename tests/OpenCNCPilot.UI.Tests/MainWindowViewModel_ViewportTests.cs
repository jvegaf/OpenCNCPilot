using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using OpenCNCPilot.Core.GCode;
using OpenCNCPilot.Hardware.Services;
using OpenCNCPilot.UI.Services;
using OpenCNCPilot.UI.ViewModels;
using Xunit;

namespace OpenCNCPilot.UI.Tests;

[Trait("Category","UI-VM")]
public class MainWindowViewModel_ViewportTests
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
        public OpenCNCPilot.UI.Services.AppSettings Settings { get; } = new() { LastGCodeDirectory = Path.GetTempPath() };
        public Task<OpenCNCPilot.UI.Services.AppSettings> LoadAsync() => Task.FromResult(Settings);
        public Task SaveAsync(OpenCNCPilot.UI.Services.AppSettings settings) => Task.CompletedTask;
    }

    [Fact]
    public void ZoomCommands_ModifyZoom_AsExpected()
    {
        var vm = new MainWindowViewModel(new NullLogger<MainWindowViewModel>(), new DummySerial(), new DummyDialogs(), new InMemorySettings(), new GCodeParser());
        var initial = vm.ViewerZoom;
        vm.ZoomInCommand.Execute().Subscribe();
        Assert.True(vm.ViewerZoom < initial);
        vm.ZoomOutCommand.Execute().Subscribe();
        Assert.True(vm.ViewerZoom > initial * 0.999); // allow small tolerance
        vm.ZoomResetCommand.Execute().Subscribe();
        Assert.Equal(1.0, vm.ViewerZoom, 6);
    }

    [Fact]
    public void FitToView_Increments_FitRequestId()
    {
        var vm = new MainWindowViewModel(new NullLogger<MainWindowViewModel>(), new DummySerial(), new DummyDialogs(), new InMemorySettings(), new GCodeParser());
        var before = vm.FitRequestId;
        vm.FitToViewCommand.Execute().Subscribe();
        Assert.Equal(before + 1, vm.FitRequestId);
    }

    [Fact]
    public void ResetViewCommand_Resets_Viewer_Properties()
    {
        var vm = new MainWindowViewModel(new NullLogger<MainWindowViewModel>(), new DummySerial(), new DummyDialogs(), new InMemorySettings(), new GCodeParser());

        // Mutate properties away from defaults
        vm.ViewerZoom = 3.14;
        vm.ViewerRotationX = -20;
        vm.ViewerRotationY = 123;
        vm.ViewerPanX = 42;
        vm.ViewerPanY = -17;

        vm.ResetViewCommand.Execute().Subscribe();

        Assert.Equal(1.0, vm.ViewerZoom, 6);
        Assert.Equal(30.0, vm.ViewerRotationX, 6);
        Assert.Equal(45.0, vm.ViewerRotationY, 6);
        Assert.Equal(0.0, vm.ViewerPanX, 6);
        Assert.Equal(0.0, vm.ViewerPanY, 6);
    }
}
