using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenCNCPilot.Core.GCode;
using OpenCNCPilot.Core.GCode.GCodeCommands;
using OpenCNCPilot.Hardware.Services;
using OpenCNCPilot.UI.Services;
using ReactiveUI;

namespace OpenCNCPilot.UI.ViewModels;

public class FileViewModel : ReactiveObject
{
    private readonly ILogger<FileViewModel> _logger;
    private readonly IGCodeParser _parser;
    private readonly IGCodeSender _sender;
    private readonly IDialogService _dialogs;
    private readonly ISettingsService _settings;

    private string _currentFileName = string.Empty;
    private int _filePosition;
    private int _fileLength;
    private TimeSpan _runtime = TimeSpan.Zero;
    private TimeSpan _estimated = TimeSpan.Zero;
    private bool _isBusy;
    private bool _pauseOnHold;

    public ObservableCollection<string> GCodeLines { get; } = new();
    private ReadOnlyObservableCollection<Command>? _parsedCommands;

    public string CurrentFileName { get => _currentFileName; private set => this.RaiseAndSetIfChanged(ref _currentFileName, value); }
    public int FilePosition { get => _filePosition; private set => this.RaiseAndSetIfChanged(ref _filePosition, value); }
    public int FileLength { get => _fileLength; private set => this.RaiseAndSetIfChanged(ref _fileLength, value); }
    public TimeSpan Runtime { get => _runtime; private set => this.RaiseAndSetIfChanged(ref _runtime, value); }
    public TimeSpan EstimatedDuration { get => _estimated; private set => this.RaiseAndSetIfChanged(ref _estimated, value); }
    public bool IsBusy { get => _isBusy; private set => this.RaiseAndSetIfChanged(ref _isBusy, value); }
    public ReadOnlyObservableCollection<Command>? ParsedCommands
    {
        get => _parsedCommands;
        private set => this.RaiseAndSetIfChanged(ref _parsedCommands, value);
    }
    public bool PauseOnHold
    {
        get => _pauseOnHold;
        set
        {
            this.RaiseAndSetIfChanged(ref _pauseOnHold, value);
            _sender.PauseOnHold = value;
            _ = SavePausePreferenceAsync(value);
        }
    }

    public ReactiveCommand<Unit, Unit> OpenCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearCommand { get; }
    public ReactiveCommand<Unit, Unit> StartCommand { get; }
    public ReactiveCommand<Unit, Unit> PauseCommand { get; }
    public ReactiveCommand<Unit, Unit> GotoCommand { get; }

    public FileViewModel(ILogger<FileViewModel> logger, IGCodeParser parser, IGCodeSender sender, IDialogService dialogs, ISettingsService settings)
    {
        _logger = logger;
        _parser = parser;
        _sender = sender;
        _dialogs = dialogs;
        _settings = settings;

        // Observables de estado del sender
        _sender.StateChanged += (_, __) => UpdateFromSender();
        _sender.PositionChanged += (_, __) => UpdateFromSender();

        var canIdle = this.WhenAnyValue(x => x.IsBusy).Select(b => !b);
        var canNotSending = canIdle;

        OpenCommand = ReactiveCommand.CreateFromTask(OpenAsync, canNotSending);
        SaveCommand = ReactiveCommand.CreateFromTask(SaveAsync, this.WhenAnyValue(x => x.FileLength).Select(n => n > 0).CombineLatest(canNotSending, (a,b) => a && b));
        ClearCommand = ReactiveCommand.Create(Clear, canNotSending);
    StartCommand = ReactiveCommand.Create(Start, this.WhenAnyValue(x => x.FileLength, x => x.IsBusy, (n, busy) => n > 0 && !busy));
    PauseCommand = ReactiveCommand.Create(Pause, this.WhenAnyValue(x => x.IsBusy));
        GotoCommand = ReactiveCommand.CreateFromTask(GotoAsync, canNotSending);

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        var s = await _settings.LoadAsync();
        PauseOnHold = s.PauseFileOnHold;
    }

    private void UpdateFromSender()
    {
        FilePosition = _sender.FilePosition;
        FileLength = _sender.FileLength;
        Runtime = _sender.Runtime;
        EstimatedDuration = _sender.EstimatedDuration;
        IsBusy = _sender.IsSending;
    }

    private async Task OpenAsync()
    {
        var s = await _settings.LoadAsync();
        var startDir = s.LastGCodeDirectory;
    var files = await _dialogs.OpenFilesAsync("Open G-Code", startDir, new[] { "*.nc;*.gcode;*.tap;*.ngc;*.gco;*.gc" }, allowMultiple: false);
        if (files == null || files.Length == 0) return;
        var file = files[0];
        string[] lines;
        try
        {
            lines = await File.ReadAllLinesAsync(file);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error leyendo archivo");
            return;
        }

        try
        {
            _parser.Parse(lines);
        }
        catch (ParseException pex)
        {
            _logger.LogWarning(pex, "GCode parse error");
            await _dialogs.AlertAsync("Parse Error", pex.Message);
            return;
        }

        if (_parser.Warnings.Count > 0)
        {
            const string header = "Warning! Parsing this file resulted in some warnings!\n\nDo not use OpenCNCPilot's edit functions unless you are sure that these warnings can be ignored!\n\nBe aware that the affected lines will likely move when using edit functions.";
            await _dialogs.ShowWarningsAsync(header, _parser.Warnings);
        }

        // Popular colecciones
        GCodeLines.Clear();
        foreach (var l in lines) GCodeLines.Add(l);
    _sender.Load(lines);
        UpdateFromSender();

        CurrentFileName = Path.GetFileName(file);

    // Snapshot de comandos parseados (para el viewport)
    var cmdList = new ObservableCollection<Command>(_parser.Commands);
    ParsedCommands = new ReadOnlyObservableCollection<Command>(cmdList);
        var dir = Path.GetDirectoryName(file);
        if (!string.IsNullOrEmpty(dir))
        {
            s.LastGCodeDirectory = dir!;
            await _settings.SaveAsync(s);
        }
    }

    private async Task SaveAsync()
    {
        var s = await _settings.LoadAsync();
        var startDir = s.LastGCodeDirectory;
    var path = await _dialogs.SaveFileAsync("Save G-Code", startDir, "output.nc", null);
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            await File.WriteAllLinesAsync(path, GCodeLines);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error guardando archivo");
        }
    }

    private void Clear()
    {
        _sender.Clear();
        GCodeLines.Clear();
        ParsedCommands = null;
        UpdateFromSender();
        CurrentFileName = string.Empty;
    }

    private void Start()
    {
        _sender.Start();
        UpdateFromSender();
    }

    private void Pause()
    {
        _sender.Pause();
        UpdateFromSender();
    }

    private async Task GotoAsync()
    {
        var input = await _dialogs.PromptNumberAsync("Go to line", "Enter 0-based line index", defaultValue: FilePosition, min: 0, max: FileLength);
        if (input == null) return;
        var idx = (int)input.Value;
        if (idx < 0 || idx > FileLength) return;
        _sender.Goto(idx);
        UpdateFromSender();
    }

    private async Task SavePausePreferenceAsync(bool value)
    {
        try
        {
            var s = await _settings.LoadAsync();
            if (s.PauseFileOnHold != value)
            {
                s.PauseFileOnHold = value;
                await _settings.SaveAsync(s);
            }
        }
        catch { /* ignore persistence errors in UI thread */ }
    }
}
