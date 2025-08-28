using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ReactiveUI;
using OpenCNCPilot.UI.Services;
using Microsoft.Extensions.Logging;

namespace OpenCNCPilot.UI.ViewModels;

public class GrblSettingItem : ReactiveObject
{
    public int Number { get; }
    public string Name { get; }
    public string Unit { get; }
    public string? Description { get; }

    private string textValue;
    public string TextValue
    {
        get => textValue;
        set => this.RaiseAndSetIfChanged(ref textValue, value);
    }

    public GrblSettingItem(int number, string name, string unit, string? description, double value)
    {
        Number = number;
        Name = name;
        Unit = unit;
        Description = description;
        textValue = value.ToString(OpenCNCPilot.Core.Constants.DecimalOutputFormat);
    }
}

public class GrblSettingsViewModel : ReactiveObject
{
    private static readonly Regex SettingParser = new(@"^\$([0-9]+)=([+-]?(?:\d+)(?:[\.,]\d+)?)\s*$");

    private readonly IDialogService dialogService;
    private readonly ILogger? logger;
    private readonly Func<int, (string Name, string Unit, string Description)?> settingLabelProvider;
    private readonly IFormatProvider parseFormat = OpenCNCPilot.Core.Constants.DecimalParseFormat;
    private const int SendDelayMs = 30; // fallback small delay to avoid tight loops

    public ObservableCollection<GrblSettingItem> Items { get; } = new();

    public ReactiveCommand<Unit, Unit> ApplyCommand { get; }
    public ReactiveCommand<Unit, Unit> ImportCommand { get; }
    public ReactiveCommand<Unit, Unit> ExportCommand { get; }

    private readonly Dictionary<int, double> current = new();

    public event Action<string>? SendLine;

    public GrblSettingsViewModel(IDialogService dialogService)
        : this(dialogService, _ => null)
    { }

    public GrblSettingsViewModel(IDialogService dialogService,
        Func<int, (string Name, string Unit, string Description)?> labelProvider,
        ILogger<GrblSettingsViewModel>? logger = null)
    {
        this.dialogService = dialogService;
        this.settingLabelProvider = labelProvider;
        this.logger = logger;

        ApplyCommand = ReactiveCommand.CreateFromTask(ApplyAsync);
        ImportCommand = ReactiveCommand.CreateFromTask(ImportAsync);
        ExportCommand = ReactiveCommand.CreateFromTask(ExportAsync);
    }

    public void ResetAll()
    {
        Items.Clear();
        current.Clear();
    }

    public void LineReceived(string line)
    {
        if (string.IsNullOrWhiteSpace(line) || line[0] != '$')
        {
            logger?.LogDebug("GRBL settings: ignoring non-setting line: {Line}", line);
            return;
        }

        var m = SettingParser.Match(line);
        if (!m.Success)
        {
            logger?.LogDebug("GRBL settings: regex no match for line: {Line}", line);
            return;
        }

        if (!int.TryParse(m.Groups[1].Value, out var number))
        {
            logger?.LogDebug("GRBL settings: invalid number in line: {Line}", line);
            return;
        }

        if (!double.TryParse(m.Groups[2].Value, System.Globalization.NumberStyles.Float, parseFormat, out var value))
        {
            logger?.LogDebug("GRBL settings: invalid value in line: {Line}", line);
            return;
        }

        var existing = Items.FirstOrDefault(i => i.Number == number);
        if (existing is null)
        {
            var label = settingLabelProvider(number);
            var item = new GrblSettingItem(number, label?.Name ?? $"${number}", label?.Unit ?? string.Empty, label?.Description, value);
            Items.Add(item);
        }
        else
        {
            existing.TextValue = value.ToString(OpenCNCPilot.Core.Constants.DecimalOutputFormat);
        }

        current[number] = value;
    }

    private async Task ApplyAsync()
    {
        if (SendLine is null) return;

        foreach (var item in Items)
        {
            if (!double.TryParse(item.TextValue, System.Globalization.NumberStyles.Float, parseFormat, out var newval))
            {
                await dialogService.AlertAsync("Invalid value", $"Value \"{item.TextValue}\" is invalid for Setting \"{item.Name}\"");
                return;
            }

            if (current.TryGetValue(item.Number, out var old) && Math.Abs(old - newval) < 1e-12)
                continue;

            SendLine?.Invoke($"${item.Number}={newval.ToString(OpenCNCPilot.Core.Constants.DecimalOutputFormat)}");
            current[item.Number] = newval;
            await Task.Delay(SendDelayMs);
        }
    }

    private async Task ImportAsync()
    {
    var file = await dialogService.OpenFilesAsync("Import settings", allowMultiple: false, filters: new[] { OpenCNCPilot.Core.Constants.FileFilterSettings });
        var path = file?.FirstOrDefault();
        if (path is null) return;

        // preserve current values for change detection
        var previous = new Dictionary<int, double>(current);
        var lines = await File.ReadAllLinesAsync(path);
        foreach (var line in lines)
        {
            LineReceived(line);
        }
        // restore previous to allow Apply to detect changes
        foreach (var kv in previous)
            current[kv.Key] = kv.Value;
    }

    private async Task ExportAsync()
    {
    var path = await dialogService.SaveFileAsync("Export settings", defaultFileName: "grbl-settings.txt", filters: new[] { OpenCNCPilot.Core.Constants.FileFilterSettings });
        if (path is null) return;

        var lines = new List<string>();
        foreach (var item in Items)
        {
            if (!double.TryParse(item.TextValue, System.Globalization.NumberStyles.Float, parseFormat, out var newval))
            {
                await dialogService.AlertAsync("Invalid value", $"Value \"{item.TextValue}\" is invalid for Setting \"{item.Name}\"");
                return;
            }
            lines.Add($"${item.Number}={newval.ToString(OpenCNCPilot.Core.Constants.DecimalOutputFormat)}");
        }

        await File.WriteAllLinesAsync(path, lines);
    }

    private static (string Name, string Unit, string Description)? GetLabel(int number) => null;
}
