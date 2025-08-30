using System;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OpenCNCPilot.Core.GCode;
using OpenCNCPilot.Hardware.Services;
using OpenCNCPilot.UI.Services;
using OpenCNCPilot.UI.ViewModels;
using Xunit;

public class FileViewModel_MetricsAndLoadingTests
{
    private sealed class DummyParser : IGCodeParser
    {
        public System.Collections.Generic.List<OpenCNCPilot.Core.GCode.GCodeCommands.Command> Commands { get; } = new();
        public System.Collections.Generic.List<string> Warnings { get; } = new();
        public bool IgnoreAdditionalAxes { get; set; }
        public void Reset() { Commands.Clear(); Warnings.Clear(); }
        public void ParseFile(string path) { }
        public void Parse(System.Collections.Generic.IEnumerable<string> file) { }
    }

    private sealed class DummySender : IGCodeSender
    {
        public GCodeSenderState State { get; private set; } = GCodeSenderState.Idle;
        public int FilePosition { get; private set; }
        public int FileLength { get; private set; }
        public TimeSpan Runtime { get; private set; }
        public TimeSpan EstimatedDuration { get; private set; }
        public bool PauseOnHold { get; set; }
        public bool IsSending => false;
        public string CurrentLineText => string.Empty;
        public event EventHandler<GCodeSenderState>? StateChanged;
        public event EventHandler<int>? PositionChanged;
        public event EventHandler<string>? ErrorOccurred;
        public void Dispose() { }
        public void Load(System.Collections.Generic.IEnumerable<string> lines)
        {
            FileLength = lines is System.Collections.Generic.ICollection<string> c ? c.Count : 0;
            FilePosition = 0;
            PositionChanged?.Invoke(this, FilePosition);
        }
        public void Start() { }
        public void Pause() { }
        public void Clear() { FileLength = 0; FilePosition = 0; PositionChanged?.Invoke(this, FilePosition); }
        public void Goto(int lineIndex) { FilePosition = Math.Clamp(lineIndex, 0, FileLength); PositionChanged?.Invoke(this, FilePosition); }
    }

    private sealed class InMemorySettings : ISettingsService
    {
        public AppSettings Current { get; private set; } = new();
        public Task<AppSettings> LoadAsync() => Task.FromResult(Current);
        public Task SaveAsync(AppSettings settings) { Current = settings; return Task.CompletedTask; }
    }

    [Fact]
    public void MoveSummary_Formats_Correctly()
    {
        var vm = new FileViewModel(NullLogger<FileViewModel>.Instance, new DummyParser(), new DummySender(), new AvaloniaDialogService(() => null), new InMemorySettings(), new OpenCNCPilot.Core.GCode.GCodePathBuilder());
        // set private backing fields via reflection for display test
        var fRapid = typeof(FileViewModel).GetField("_rapidCount", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var fCut = typeof(FileViewModel).GetField("_cutCount", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        fRapid.SetValue(vm, 3);
        fCut.SetValue(vm, 7);
        vm.MoveSummary.Should().Be("Moves: 10 (Rapid: 3, Cut: 7)");
    }

    [Fact]
    public void LoadTimingSummary_Shows_Total_And_Parts()
    {
        var vm = new FileViewModel(NullLogger<FileViewModel>.Instance, new DummyParser(), new DummySender(), new AvaloniaDialogService(() => null), new InMemorySettings(), new OpenCNCPilot.Core.GCode.GCodePathBuilder());
        var fRead = typeof(FileViewModel).GetField("_lastLoadReadMs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var fParse = typeof(FileViewModel).GetField("_lastLoadParseMs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var fBuild = typeof(FileViewModel).GetField("_lastLoadBuildMs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        fRead.SetValue(vm, 12.4);
        fParse.SetValue(vm, 23.6);
        fBuild.SetValue(vm, 33.0);
        vm.LastLoadTotalMs.Should().BeApproximately(69.0, 0.0001);
        vm.LoadTimingSummary.Should().Contain("Load:");
        vm.LoadTimingSummary.Should().Contain("read 12");
        vm.LoadTimingSummary.Should().Contain("parse 24");
        vm.LoadTimingSummary.Should().Contain("build 33");
    }

    [Fact]
    public void Commands_Disable_While_Loading()
    {
        var vm = new FileViewModel(NullLogger<FileViewModel>.Instance, new DummyParser(), new DummySender(), new AvaloniaDialogService(() => null), new InMemorySettings(), new OpenCNCPilot.Core.GCode.GCodePathBuilder());
        // Initially can interact
        vm.OpenCommand.CanExecute.FirstAsync().Wait().Should().BeTrue();
        // Turn loading on via non-public setter to trigger ReactiveUI notifications
        var pIsLoading = typeof(FileViewModel).GetProperty("IsLoading")!;
        var set = pIsLoading.GetSetMethod(true)!;
        set.Invoke(vm, new object[] { true });
        vm.OpenCommand.CanExecute.FirstAsync().Wait().Should().BeFalse();
        vm.ClearCommand.CanExecute.FirstAsync().Wait().Should().BeFalse();
        vm.GotoCommand.CanExecute.FirstAsync().Wait().Should().BeFalse();
        // Back to not loading
        set.Invoke(vm, new object[] { false });
        vm.OpenCommand.CanExecute.FirstAsync().Wait().Should().BeTrue();
    }
}
