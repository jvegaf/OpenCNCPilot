using System;
using System.Collections.Generic;
using System.Reactive;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OpenCNCPilot.Core.GCode;
using OpenCNCPilot.Hardware.Services;
using OpenCNCPilot.UI.Services;
using OpenCNCPilot.UI.ViewModels;
using Xunit;
using System.Reactive.Threading.Tasks;

namespace OpenCNCPilot.UI.Tests;

[Trait("Category","UI-VM")]
public class MainWindowViewModel_GrblSettingsTests
{
    private class DummySerial : ISerialPortService
    {
        public event EventHandler<string>? DataReceived;
    #pragma warning disable CS0067
    public event EventHandler<bool>? ConnectionStateChanged;
    #pragma warning restore CS0067
        public bool IsOpen { get; set; } = true;
        public string? PortName => "COM1";
        public int BaudRate => 115200;
        public readonly List<string> Writes = new();
        public string[] GetAvailablePorts() => new[] { "COM1" };
        public void Open(string portName, int baudRate, System.IO.Ports.Parity parity = System.IO.Ports.Parity.None, int dataBits = 8, System.IO.Ports.StopBits stopBits = System.IO.Ports.StopBits.One) => IsOpen = true;
        public void Write(string data) => Writes.Add(data);
        public void WriteLine(string data) => Writes.Add(data);
        public void Close() => IsOpen = false;
        public void SetRtsEnable(bool enable) { }
        public void SetDtrEnable(bool enable) { }
        public void ClearInputBuffer() { }
        public void ClearOutputBuffer() { }

        public void SimulateData(string data) => DataReceived?.Invoke(this, data);
    }

    private class DummyDialogs : IDialogService
    {
        public Task<string[]?> OpenFilesAsync(string title, string? initialDirectory = null, string[]? filters = null, bool allowMultiple = false) => Task.FromResult<string[]?>(null);
        public Task<string?> SaveFileAsync(string title, string? initialDirectory = null, string? defaultFileName = null, string[]? filters = null) => Task.FromResult<string?>(null);
        public Task<string?> PickFolderAsync(string title, string? initialDirectory = null) => Task.FromResult<string?>(null);
        public Task AlertAsync(string title, string message, string okText = "OK") => Task.CompletedTask;
        public Task<bool> ConfirmAsync(string title, string message, string confirmText = "OK", string cancelText = "Cancel") => Task.FromResult(true);
        public Task<double?> PromptNumberAsync(string title, string message, double? defaultValue = null, double? min = null, double? max = null, int decimals = 3) => Task.FromResult<double?>(null);
        public Task ShowWarningsAsync(string header, IEnumerable<string> warnings) => Task.CompletedTask;
    }

    private class DummySettings : ISettingsService
    {
        public Task<OpenCNCPilot.UI.Services.AppSettings> LoadAsync() => Task.FromResult(new OpenCNCPilot.UI.Services.AppSettings());
        public Task SaveAsync(OpenCNCPilot.UI.Services.AppSettings settings) => Task.CompletedTask;
    }

    private class DummyParser : IGCodeParser
    {
        public bool IgnoreAdditionalAxes { get; set; }
        public System.Collections.Generic.List<OpenCNCPilot.Core.GCode.GCodeCommands.Command> Commands { get; } = new();
        public System.Collections.Generic.List<string> Warnings { get; } = new();
        public void Reset() { Commands.Clear(); Warnings.Clear(); }
        public void ParseFile(string path) { }
        public void Parse(IEnumerable<string> file) { }
    }

    [Fact]
    public async Task OpenGrblSettings_Sends_DoubleDollar_When_Connected()
    {
        var serial = new DummySerial();
        var vm = new MainWindowViewModel(new NullLogger<MainWindowViewModel>(), serial, new DummyDialogs(), new DummySettings(), new DummyParser());
        vm.SelectedPort = "COM1";

        bool? shown = null;
        vm.ShowGrblSettings.RegisterHandler(async interaction =>
        {
            shown = true;
            interaction.SetOutput(true);
            await Task.CompletedTask;
        });

    await vm.OpenGrblSettingsCommand.Execute().ToTask();

        serial.Writes.Should().Contain("$$");
        shown.Should().BeTrue();
    }

    [Fact]
    public async Task OpenGrblSettings_Populates_Items_From_DataReceived()
    {
        var serial = new DummySerial();
        var vm = new MainWindowViewModel(new NullLogger<MainWindowViewModel>(), serial, new DummyDialogs(), new DummySettings(), new DummyParser());
        vm.SelectedPort = "COM1";

        var tcs = new TaskCompletionSource<bool>();
        GrblSettingsViewModel? dialogVm = null;

        vm.ShowGrblSettings.RegisterHandler(async interaction =>
        {
            dialogVm = interaction.Input;
            await Task.Delay(1);
            await tcs.Task;
            interaction.SetOutput(true);
        });

        var executing = vm.OpenGrblSettingsCommand.Execute().ToTask();

        // Simulate GRBL settings dump
        serial.SimulateData("$0=10\n$1=255\n$10=3\n");

        // Close the dialog and finish
        tcs.SetResult(true);
        await executing;

        dialogVm.Should().NotBeNull();
        dialogVm!.Items.Should().NotBeEmpty();
        dialogVm.Items.Select(i => i.Number).Should().Contain(new[] { 0, 1, 10 });
    dialogVm.Items.First(i => i.Number == 1).TextValue.Should().Be("255.000");
    }

    [Fact]
    public async Task OpenGrblSettings_Apply_Sends_Only_Changes_Via_Serial()
    {
        var serial = new DummySerial();
        var vm = new MainWindowViewModel(new NullLogger<MainWindowViewModel>(), serial, new DummyDialogs(), new DummySettings(), new DummyParser());
        vm.SelectedPort = "COM1";

        var tcs = new TaskCompletionSource<bool>();
        GrblSettingsViewModel? dialogVm = null;

        vm.ShowGrblSettings.RegisterHandler(async interaction =>
        {
            dialogVm = interaction.Input;
            await Task.Delay(1);
            await tcs.Task;
            interaction.SetOutput(true);
        });

        var executing = vm.OpenGrblSettingsCommand.Execute().ToTask();

        // Simulate initial values
        serial.SimulateData("$10=1\n$11=2\n");
        dialogVm.Should().NotBeNull();

        // Change only one and apply
        var item10 = dialogVm!.Items.First(i => i.Number == 10);
        item10.TextValue = "1.5";
        await dialogVm.ApplyCommand.Execute().ToTask();

    serial.Writes.Should().Contain("$$"); // initial request
    serial.Writes.Should().Contain("$10=1.500");
    serial.Writes.Should().NotContain("$11=2.000");

        // Close dialog
        tcs.SetResult(true);
        await executing;
    }
}
