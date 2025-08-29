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
using OpenCNCPilot.Core.Geometry;
using OpenCNCPilot.UI.Services;
using ReactiveUI;

namespace OpenCNCPilot.UI.ViewModels;

public class FileViewModel : ReactiveObject
{
    private readonly ILogger<FileViewModel> _logger;
    private readonly IGCodeParser _parser;
    private readonly IGCodeSender _sender;
    private readonly IGCodePathBuilder _pathBuilder;
    private readonly IDialogService _dialogs;
    private readonly ISettingsService _settings;

    private string _currentFileName = string.Empty;
    private int _filePosition;
    private int _fileLength;
    private TimeSpan _runtime = TimeSpan.Zero;
    private TimeSpan _estimated = TimeSpan.Zero;
    private bool _isBusy;
    private bool _pauseOnHold;
    private GeometryData? _geometryData;

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
    public GeometryData? GeometryData
    {
        get => _geometryData;
        private set => this.RaiseAndSetIfChanged(ref _geometryData, value);
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
    public ReactiveCommand<Unit, Unit> FindNextCommand { get; }
    public ReactiveCommand<Unit, Unit> FindPrevCommand { get; }

    // Search state
    private string _searchQuery = string.Empty;
    private int _searchMatchCount;
    private int _searchMatchIndex = -1; // 0-based position in matches
    private readonly System.Collections.Generic.List<int> _matchIndices = new();
    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            this.RaiseAndSetIfChanged(ref _searchQuery, value);
            _ = RecomputeMatchesAsync();
        }
    }
    public int SearchMatchCount { get => _searchMatchCount; private set => this.RaiseAndSetIfChanged(ref _searchMatchCount, value); }
    public int SearchMatchIndex { get => _searchMatchIndex; private set { this.RaiseAndSetIfChanged(ref _searchMatchIndex, value); this.RaisePropertyChanged(nameof(SearchStatus)); } }
    public string SearchStatus => (SearchMatchCount <= 0 || SearchMatchIndex < 0) ? "0/0" : $"{SearchMatchIndex + 1}/{SearchMatchCount}";

    public FileViewModel(ILogger<FileViewModel> logger, IGCodeParser parser, IGCodeSender sender, IDialogService dialogs, ISettingsService settings, IGCodePathBuilder pathBuilder)
    {
        _logger = logger;
        _parser = parser;
        _sender = sender;
        _dialogs = dialogs;
        _settings = settings;
        _pathBuilder = pathBuilder;

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
        // Goto is available when not busy; bounds are validated within the command
        GotoCommand = ReactiveCommand.CreateFromTask(GotoAsync, canNotSending);
        FindNextCommand = ReactiveCommand.CreateFromTask(FindNextAsync, canNotSending);
        FindPrevCommand = ReactiveCommand.CreateFromTask(FindPrevAsync, canNotSending);

        // Recompute matches when lines change and there is a query
        GCodeLines.CollectionChanged += async (_, __) => { if (!string.IsNullOrWhiteSpace(SearchQuery)) await RecomputeMatchesAsync(); };

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        var s = await _settings.LoadAsync();
        PauseOnHold = s.PauseFileOnHold;
        // Aplicar preferencia de ejes adicionales al parser
        _parser.IgnoreAdditionalAxes = s.IgnoreAdditionalAxes;
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
        // Use explicit filter format: "Name|*.ext;*.ext"; keep StorageProvider robust to glob-only too
        var gcodeFilter = "G-Code (*.nc;*.gcode;*.tap;*.ngc;*.gco;*.gc)|*.nc;*.gcode;*.tap;*.ngc;*.gco;*.gc";
        var files = await _dialogs.OpenFilesAsync("Open G-Code", startDir, new[] { gcodeFilter, "All files|*.*" }, allowMultiple: false);
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
            // Sincronizar flag del parser con settings actuales
            _parser.IgnoreAdditionalAxes = s.IgnoreAdditionalAxes;
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
    await RecomputeMatchesAsync();

        CurrentFileName = Path.GetFileName(file);

        // Snapshot de comandos parseados (para el viewport)
        var cmdList = new ObservableCollection<Command>(_parser.Commands);
        ParsedCommands = new ReadOnlyObservableCollection<Command>(cmdList);

        // Construir geometría (mm) para el viewer moderno
        if (_parser is GCodeParser concrete)
        {
            // Ejecutar en background para no bloquear UI en archivos grandes
            try
            {
                var geo = await Task.Run(() => _pathBuilder.Build(concrete));
                GeometryData = geo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error construyendo geometría para previsualización");
            }
        }
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
        var defaultName = string.IsNullOrWhiteSpace(CurrentFileName) ? "output.nc" : CurrentFileName;
        var gcodeFilter = "G-Code (*.nc;*.gcode;*.tap;*.ngc;*.gco;*.gc)|*.nc;*.gcode;*.tap;*.ngc;*.gco;*.gc";
        var path = await _dialogs.SaveFileAsync("Save G-Code", startDir, defaultName, new[] { gcodeFilter, "All files|*.*" });
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            await File.WriteAllLinesAsync(path, GCodeLines);
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                s.LastGCodeDirectory = dir!;
                await _settings.SaveAsync(s);
            }
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
        GeometryData = null;
        UpdateFromSender();
        CurrentFileName = string.Empty;
    _matchIndices.Clear();
    SearchMatchCount = 0;
    SearchMatchIndex = -1;
    this.RaisePropertyChanged(nameof(SearchStatus));
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
        var max = FileLength > 0 ? FileLength - 1 : (int?)null;
        var input = await _dialogs.PromptNumberAsync("Go to line", "Enter 0-based line index", defaultValue: FilePosition, min: 0, max: max);
        if (input == null) return;
        var idx = (int)input.Value;
        if (idx < 0 || idx >= FileLength) return;
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

    private Task RecomputeMatchesAsync()
    {
        _matchIndices.Clear();
        SearchMatchIndex = -1;
        SearchMatchCount = 0;
        var q = SearchQuery;
        if (string.IsNullOrWhiteSpace(q)) { this.RaisePropertyChanged(nameof(SearchStatus)); return Task.CompletedTask; }

        for (int i = 0; i < GCodeLines.Count; i++)
        {
            var line = GCodeLines[i];
            if (line?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                _matchIndices.Add(i);
        }
        SearchMatchCount = _matchIndices.Count;
        if (SearchMatchCount > 0)
        {
            // pick the first match at or after current position
            var idx = _matchIndices.FindIndex(x => x >= FilePosition);
            SearchMatchIndex = idx >= 0 ? idx : 0;
        }
        else
        {
            SearchMatchIndex = -1;
        }
        this.RaisePropertyChanged(nameof(SearchStatus));
        return Task.CompletedTask;
    }

    private Task FindNextAsync()
    {
        if (_matchIndices.Count == 0) return Task.CompletedTask;
        var pos = SearchMatchIndex;
        pos = (pos + 1) % _matchIndices.Count;
        SearchMatchIndex = pos;
        var target = _matchIndices[pos];
        _sender.Goto(target);
        UpdateFromSender();
        return Task.CompletedTask;
    }

    private Task FindPrevAsync()
    {
        if (_matchIndices.Count == 0) return Task.CompletedTask;
        var pos = SearchMatchIndex;
        pos = (pos - 1 + _matchIndices.Count) % _matchIndices.Count;
        SearchMatchIndex = pos;
        var target = _matchIndices[pos];
        _sender.Goto(target);
        UpdateFromSender();
        return Task.CompletedTask;
    }
}
