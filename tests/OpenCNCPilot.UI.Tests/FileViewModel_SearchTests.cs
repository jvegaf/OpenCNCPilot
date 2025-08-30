using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Reactive.Linq;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OpenCNCPilot.Core.GCode;
using OpenCNCPilot.Hardware.Services;
using OpenCNCPilot.UI.Services;
using OpenCNCPilot.UI.ViewModels;
using Xunit;

public class FileViewModel_SearchTests
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
    public async Task Search_Next_Prev_Cycles_Through_Matches()
    {
        var vm = new FileViewModel(NullLogger<FileViewModel>.Instance, new DummyParser(), new DummySender(), new AvaloniaDialogService(() => null), new InMemorySettings(), new OpenCNCPilot.Core.GCode.GCodePathBuilder());
        vm.GCodeLines.Add("G0 X0 Y0");
        vm.GCodeLines.Add("G1 X10 Y0");
        vm.GCodeLines.Add("G1 X10 Y10");
        vm.GCodeLines.Add("G0 X0 Y10");

        vm.SearchQuery = "G1";
        await Task.Delay(10);

        vm.SearchMatchCount.Should().Be(2);
        vm.SearchStatus.Should().Be("1/2");

    vm.FindNextCommand.Execute().FirstAsync().Wait();
        vm.SearchStatus.Should().Be("2/2");

    vm.FindNextCommand.Execute().FirstAsync().Wait();
        vm.SearchStatus.Should().Be("1/2");

    vm.FindPrevCommand.Execute().FirstAsync().Wait();
        vm.SearchStatus.Should().Be("2/2");
    }
}
