using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using FluentAssertions;
using OpenCNCPilot.UI.Services;
using OpenCNCPilot.UI.ViewModels;
using Xunit;

namespace OpenCNCPilot.UI.Tests;

[Trait("Category","UI-VM")]
public class GrblSettingsViewModelTests
{
    private sealed class TestDialogService : IDialogService
    {
        public string[]? NextOpenFiles;
        public string? NextOpenPath;
        public string? NextSaveFile;
        public string? NextSavePath;
        public string? PickedFolder;
        public readonly List<(string title, string message)> Alerts = new();

        public Task<string[]?> OpenFilesAsync(string title, string? initialDirectory = null, string[]? filters = null, bool allowMultiple = false)
            => Task.FromResult<string[]?>(NextOpenFiles ?? (NextOpenPath is null ? null : new[] { NextOpenPath }));

        public Task<string?> SaveFileAsync(string title, string? initialDirectory = null, string? defaultFileName = null, string[]? filters = null)
            => Task.FromResult<string?>(NextSaveFile ?? NextSavePath);

        public Task<string?> PickFolderAsync(string title, string? initialDirectory = null)
            => Task.FromResult(PickedFolder);

        public Task AlertAsync(string title, string message, string okText = "OK")
        {
            Alerts.Add((title, message));
            return Task.CompletedTask;
        }

        public Task<bool> ConfirmAsync(string title, string message, string confirmText = "OK", string cancelText = "Cancel")
            => Task.FromResult(true);

        public Task<double?> PromptNumberAsync(string title, string message, double? defaultValue = null, double? min = null, double? max = null, int decimals = 3)
            => Task.FromResult<double?>(null);

        public Task ShowWarningsAsync(string header, IEnumerable<string> warnings)
            => Task.CompletedTask;
    }

    [Fact]
    public async Task Import_Should_Populate_Items_And_Apply_Only_Changes()
    {
        // Arrange: temp file with settings
        var dir = Path.Combine(Path.GetTempPath(), "OpenCNCPilotTests", Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "grbl-settings.txt");
        await File.WriteAllLinesAsync(path, new[] { "$0=10", "$1=25", "$100=250.0" });

        var dialogs = new TestDialogService { NextOpenFiles = new[] { path } };
        var vm = new GrblSettingsViewModel(dialogs);

        // Act: import
        await vm.ImportCommand.Execute().FirstAsync();

        // Assert: items populated
        vm.Items.Select(i => i.Number).Should().BeEquivalentTo(new[] { 0, 1, 100 });

        // Apply should send only when there are changes
        string? sent = null;
        vm.SendLine += s => sent = s;
        // Change only one value
        var item100 = vm.Items.First(i => i.Number == 100);
        item100.TextValue = "260.500";
        await vm.ApplyCommand.Execute().FirstAsync();

        sent.Should().Be("$100=260.500");
    }

    [Fact]
    public async Task Export_Should_Write_File_With_Current_Values()
    {
        // Arrange: prepare items via LineReceived
        var dialogs = new TestDialogService();
        var vm = new GrblSettingsViewModel(dialogs);
        vm.LineReceived("$0=1");
        vm.LineReceived("$10=255");

        var dir = Path.Combine(Path.GetTempPath(), "OpenCNCPilotTests", Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        var outPath = Path.Combine(dir, "out.txt");
        dialogs.NextSaveFile = outPath;

        // Act
        await vm.ExportCommand.Execute().FirstAsync();

        // Assert
        File.Exists(outPath).Should().BeTrue();
        var lines = await File.ReadAllLinesAsync(outPath);
        lines.Should().BeEquivalentTo(new[] { "$0=1.000", "$10=255.000" });
    }

    [Fact]
    public void LineReceived_Parses_And_Adds_Items()
    {
        var dlg = new TestDialogService();
        var vm = new GrblSettingsViewModel(dlg, n => ("Test", "mm", "desc"));

        vm.LineReceived("$1=10.5");
        vm.LineReceived("$2=3");

        vm.Items.Should().HaveCount(2);
    vm.Items.First(i => i.Number == 1).TextValue.Should().Be("10.500");
    vm.Items.First(i => i.Number == 2).TextValue.Should().Be("3.000");
    }

    [Fact]
    public async Task Apply_Sends_Only_Changes_And_Validates()
    {
        var dlg = new TestDialogService();
        var vm = new GrblSettingsViewModel(dlg, n => ("S", "mm", "desc"));
        var sent = new List<string>();
        vm.SendLine += s => sent.Add(s);

        vm.LineReceived("$10=1");
        vm.LineReceived("$11=2");

        // change one value
        vm.Items.First(i => i.Number == 10).TextValue = "1.5";
    await vm.ApplyCommand.Execute().ToTask();

    sent.Should().Contain("$10=1.500");
    sent.Should().NotContain("$11=2.000");

        // invalid value blocks apply
        vm.Items.First(i => i.Number == 11).TextValue = "abc";
        sent.Clear();
        await vm.ApplyCommand.Execute().ToTask();
        sent.Should().BeEmpty();
        dlg.Alerts.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Import_And_Export_Workflow()
    {
        var dlg = new TestDialogService();
        var vm = new GrblSettingsViewModel(dlg, n => ("S", "mm", "desc"));

        var tmpIn = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".txt");
        await File.WriteAllTextAsync(tmpIn, "$1=1\n$2=2\n");
        dlg.NextOpenPath = tmpIn;
        await vm.ImportCommand.Execute().ToTask();
        vm.Items.Should().HaveCount(2);

        var tmpOut = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".txt");
        dlg.NextSavePath = tmpOut;
        await vm.ExportCommand.Execute().ToTask();

        var outText = await File.ReadAllTextAsync(tmpOut);
        outText.Should().Contain("$1=1");
        outText.Should().Contain("$2=2");
    }
}
