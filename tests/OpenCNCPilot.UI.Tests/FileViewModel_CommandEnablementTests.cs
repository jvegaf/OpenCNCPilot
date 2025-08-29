using System;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OpenCNCPilot.Core.GCode;
using OpenCNCPilot.Core.GCode.GCodeCommands;
using OpenCNCPilot.Hardware.Services;
using OpenCNCPilot.UI.Services;
using OpenCNCPilot.UI.ViewModels;
using ReactiveUI;
using Xunit;

public class FileViewModel_CommandEnablementTests
{
    private sealed class DummyParser : IGCodeParser
    {
        public System.Collections.Generic.List<Command> Commands { get; } = new();
        public System.Collections.Generic.List<string> Warnings { get; } = new();
        public bool IgnoreAdditionalAxes { get; set; }
        public void Reset() { Commands.Clear(); Warnings.Clear(); }
        public void ParseFile(string path) { }
        public void Parse(System.Collections.Generic.IEnumerable<string> file)
        {
            Commands.Clear();
            Warnings.Clear();
            // no-op for this test
        }
    }

    private sealed class ToggleSender : IGCodeSender
    {
        public GCodeSenderState State { get; private set; } = GCodeSenderState.Idle;
        public int FilePosition { get; private set; }
        public int FileLength { get; private set; }
        public TimeSpan Runtime { get; private set; }
        public TimeSpan EstimatedDuration { get; private set; }
        public bool PauseOnHold { get; set; }
        public bool IsSending => State == GCodeSenderState.Sending;
        public string CurrentLineText => string.Empty;
        public event EventHandler<GCodeSenderState>? StateChanged;
        public event EventHandler<int>? PositionChanged;
        public event EventHandler<string>? ErrorOccurred;
        public void Dispose() { }
        public void Load(System.Collections.Generic.IEnumerable<string> lines)
        {
            FileLength = lines is System.Collections.Generic.ICollection<string> c ? c.Count : 0;
            FilePosition = 0;
            EstimatedDuration = TimeSpan.Zero;
            Runtime = TimeSpan.Zero;
            RaisePosition();
        }
        public void Start() { SetState(GCodeSenderState.Sending); }
        public void Pause() { SetState(GCodeSenderState.Paused); }
        public void Clear() { FileLength = 0; FilePosition = 0; SetState(GCodeSenderState.Idle); }
        public void Goto(int lineIndex) { FilePosition = Math.Clamp(lineIndex, 0, FileLength); RaisePosition(); }
        private void SetState(GCodeSenderState s) { State = s; StateChanged?.Invoke(this, s); }
        private void RaisePosition() { PositionChanged?.Invoke(this, FilePosition); }
    }

    private sealed class DummyDialogs : IDialogService
    {
        public string[]? NextOpenFiles;
        public System.Collections.Generic.List<string> LastWarnings { get; } = new();
        public System.Threading.Tasks.Task<string[]?> OpenFilesAsync(string title, string? initialDirectory = null, string[]? filters = null, bool allowMultiple = false) => System.Threading.Tasks.Task.FromResult(NextOpenFiles);
        public System.Threading.Tasks.Task<string?> SaveFileAsync(string title, string? initialDirectory = null, string? defaultFileName = null, string[]? filters = null) => System.Threading.Tasks.Task.FromResult<string?>(null);
        public System.Threading.Tasks.Task<string?> PickFolderAsync(string title, string? initialDirectory = null)
            => System.Threading.Tasks.Task.FromResult<string?>(null);

        public System.Threading.Tasks.Task AlertAsync(string title, string message, string okText = "OK") => System.Threading.Tasks.Task.CompletedTask;
        public System.Threading.Tasks.Task<bool> ConfirmAsync(string title, string message, string confirmText = "OK", string cancelText = "Cancel") => System.Threading.Tasks.Task.FromResult(false);
        public System.Threading.Tasks.Task<double?> PromptNumberAsync(string title, string message, double? defaultValue = null, double? min = null, double? max = null, int decimals = 3) => System.Threading.Tasks.Task.FromResult<double?>(null);
        public System.Threading.Tasks.Task ShowWarningsAsync(string header, System.Collections.Generic.IEnumerable<string> warnings) { LastWarnings.Clear(); LastWarnings.AddRange(warnings); return System.Threading.Tasks.Task.CompletedTask; }
    }

    private sealed class InMemorySettings : ISettingsService
    {
        public AppSettings Current { get; private set; } = new();
        public System.Threading.Tasks.Task<AppSettings> LoadAsync() => System.Threading.Tasks.Task.FromResult(Current);
        public System.Threading.Tasks.Task SaveAsync(AppSettings settings) { Current = settings; return System.Threading.Tasks.Task.CompletedTask; }
    }

    [Fact]
    public void Commands_Are_Disabled_While_Sending_And_Reenabled_On_Idle()
    {
        var parser = new DummyParser();
        var sender = new ToggleSender();
        var dialogs = new DummyDialogs();
        var settings = new InMemorySettings();
        var vm = new FileViewModel(NullLogger<FileViewModel>.Instance, parser, sender, dialogs, settings);

        // Cargar un archivo virtual para habilitar Start/Save
        sender.Load(new[] { "G0 X0", "G1 X1" });

        // Inicialmente Idle: Open/Save/Clear/Goto habilitados
        vm.OpenCommand.CanExecute.FirstAsync().Wait().Should().BeTrue();
        vm.SaveCommand.CanExecute.FirstAsync().Wait().Should().BeTrue();
        vm.ClearCommand.CanExecute.FirstAsync().Wait().Should().BeTrue();
        vm.GotoCommand.CanExecute.FirstAsync().Wait().Should().BeTrue();

        // Al iniciar envío: Open/Save/Clear/Goto deshabilitados, Pause habilitado
        sender.Start();
        vm.OpenCommand.CanExecute.FirstAsync().Wait().Should().BeFalse();
        vm.SaveCommand.CanExecute.FirstAsync().Wait().Should().BeFalse();
        vm.ClearCommand.CanExecute.FirstAsync().Wait().Should().BeFalse();
        vm.GotoCommand.CanExecute.FirstAsync().Wait().Should().BeFalse();
        vm.PauseCommand.CanExecute.FirstAsync().Wait().Should().BeTrue();

        // Volver a Idle: se re-habilitan
        sender.Pause();
        sender.Clear();
        vm.OpenCommand.CanExecute.FirstAsync().Wait().Should().BeTrue();
        vm.ClearCommand.CanExecute.FirstAsync().Wait().Should().BeTrue();
        vm.GotoCommand.CanExecute.FirstAsync().Wait().Should().BeTrue();
    }
}

