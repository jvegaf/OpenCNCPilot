using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using System.Reactive.Threading.Tasks;
using OpenCNCPilot.UI.Services;
using OpenCNCPilot.UI.ViewModels;
using Xunit;

namespace OpenCNCPilot.UI.Tests;

[Trait("Category","UI-VM")]
public class GrblSettingsViewModelTests
{
    private class DummyDialogService : IDialogService
    {
        public string? NextOpenPath { get; set; }
        public string? NextSavePath { get; set; }
        public readonly List<(string title, string message)> Alerts = new();

        public Task AlertAsync(string title, string message, string okText = "OK")
        {
            Alerts.Add((title, message));
            return Task.CompletedTask;
        }

        public Task<bool> ConfirmAsync(string title, string message, string confirmText = "OK", string cancelText = "Cancel")
            => Task.FromResult(true);

        public Task<string[]?> OpenFilesAsync(string title, string? initialDirectory = null, string[]? filters = null, bool allowMultiple = false)
            => Task.FromResult<string[]?>(NextOpenPath is null ? null : new[] { NextOpenPath });

        public Task<string?> PickFolderAsync(string title, string? initialDirectory = null)
            => Task.FromResult<string?>(null);

        public Task<double?> PromptNumberAsync(string title, string message, double? defaultValue = null, double? min = null, double? max = null, int decimals = 3)
            => Task.FromResult<double?>(null);

        public Task<string?> SaveFileAsync(string title, string? initialDirectory = null, string? defaultFileName = null, string[]? filters = null)
            => Task.FromResult<string?>(NextSavePath);

        public Task ShowWarningsAsync(string header, IEnumerable<string> warnings)
            => Task.CompletedTask;
    }

    [Fact]
    public void LineReceived_Parses_And_Adds_Items()
    {
        var dlg = new DummyDialogService();
        var vm = new GrblSettingsViewModel(dlg, n => ("Test", "mm", "desc"));

        vm.LineReceived("$1=10.5");
        vm.LineReceived("$2=3");

        vm.Items.Should().HaveCount(2);
        vm.Items.First(i => i.Number == 1).TextValue.Should().Be("10.5");
        vm.Items.First(i => i.Number == 2).TextValue.Should().Be("3");
    }

    [Fact]
    public async Task Apply_Sends_Only_Changes_And_Validates()
    {
        var dlg = new DummyDialogService();
        var vm = new GrblSettingsViewModel(dlg, n => ("S", "mm", "desc"));
        var sent = new List<string>();
        vm.SendLine += s => sent.Add(s);

        vm.LineReceived("$10=1");
        vm.LineReceived("$11=2");

        // change one value
        vm.Items.First(i => i.Number == 10).TextValue = "1.5";

    await vm.ApplyCommand.Execute().ToTask();

        sent.Should().Contain("$10=1.5");
        sent.Should().NotContain("$11=2");

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
        var dlg = new DummyDialogService();
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
