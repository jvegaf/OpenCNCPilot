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
    private bool _isLoading;
    private double _loadProgress; // 0..1, por ahora se usa como indeterminado
    private System.Threading.CancellationTokenSource? _loadCts;
    private int _rapidCount;
    private int _cutCount;
    private double _lastLoadReadMs;
    private double _lastLoadParseMs;
    private double _lastLoadBuildMs;

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
    public bool IsLoading
    {
        get => _isLoading;
        private set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }
    public double LoadProgress
    {
        get => _loadProgress;
        private set => this.RaiseAndSetIfChanged(ref _loadProgress, value);
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
    public int RapidCount
    {
        get => _rapidCount;
        private set
        {
            this.RaiseAndSetIfChanged(ref _rapidCount, value);
            this.RaisePropertyChanged(nameof(MoveCount));
            this.RaisePropertyChanged(nameof(MoveSummary));
        }
    }
    public int CutCount
    {
        get => _cutCount;
        private set
        {
            this.RaiseAndSetIfChanged(ref _cutCount, value);
            this.RaisePropertyChanged(nameof(MoveCount));
            this.RaisePropertyChanged(nameof(MoveSummary));
        }
    }
    public int MoveCount => RapidCount + CutCount;
    public string MoveSummary => MoveCount <= 0 ? "Moves: 0" : $"Moves: {MoveCount} (Rapid: {RapidCount}, Cut: {CutCount})";
    public double LastLoadReadMs
    {
        get => _lastLoadReadMs;
        private set
        {
            this.RaiseAndSetIfChanged(ref _lastLoadReadMs, value);
            this.RaisePropertyChanged(nameof(LastLoadTotalMs));
            this.RaisePropertyChanged(nameof(LoadTimingSummary));
        }
    }
    public double LastLoadParseMs
    {
        get => _lastLoadParseMs;
        private set
        {
            this.RaiseAndSetIfChanged(ref _lastLoadParseMs, value);
            this.RaisePropertyChanged(nameof(LastLoadTotalMs));
            this.RaisePropertyChanged(nameof(LoadTimingSummary));
        }
    }
    public double LastLoadBuildMs
    {
        get => _lastLoadBuildMs;
        private set
        {
            this.RaiseAndSetIfChanged(ref _lastLoadBuildMs, value);
            this.RaisePropertyChanged(nameof(LastLoadTotalMs));
            this.RaisePropertyChanged(nameof(LoadTimingSummary));
        }
    }
    public double LastLoadTotalMs => LastLoadReadMs + LastLoadParseMs + LastLoadBuildMs;
    public string LoadTimingSummary => LastLoadTotalMs <= 0 ? string.Empty : $"Load: {LastLoadTotalMs:F0} ms (read {LastLoadReadMs:F0}, parse {LastLoadParseMs:F0}, build {LastLoadBuildMs:F0})";

    public ReactiveCommand<Unit, Unit> OpenCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearCommand { get; }
    public ReactiveCommand<Unit, Unit> StartCommand { get; }
    public ReactiveCommand<Unit, Unit> PauseCommand { get; }
    public ReactiveCommand<Unit, Unit> GotoCommand { get; }
    public ReactiveCommand<Unit, Unit> FindNextCommand { get; }
    public ReactiveCommand<Unit, Unit> FindPrevCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelLoadCommand { get; }

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

        var canInteract = this.WhenAnyValue(x => x.IsBusy, x => x.IsLoading, (busy, loading) => !busy && !loading);
        var canNotSending = canInteract;

    OpenCommand = ReactiveCommand.CreateFromTask(OpenAsync, canNotSending);
        SaveCommand = ReactiveCommand.CreateFromTask(SaveAsync, this.WhenAnyValue(x => x.FileLength).Select(n => n > 0).CombineLatest(canNotSending, (a,b) => a && b));
        ClearCommand = ReactiveCommand.Create(Clear, canNotSending);
        StartCommand = ReactiveCommand.Create(Start, this.WhenAnyValue(x => x.FileLength, x => x.IsBusy, x => x.IsLoading, (n, busy, loading) => n > 0 && !busy && !loading));
        PauseCommand = ReactiveCommand.Create(Pause, this.WhenAnyValue(x => x.IsBusy));
        // Goto is available when not busy; bounds are validated within the command
        GotoCommand = ReactiveCommand.CreateFromTask(GotoAsync, canNotSending);
        FindNextCommand = ReactiveCommand.CreateFromTask(FindNextAsync, canNotSending);
        FindPrevCommand = ReactiveCommand.CreateFromTask(FindPrevAsync, canNotSending);
    CancelLoadCommand = ReactiveCommand.Create(CancelLoad, this.WhenAnyValue(x => x.IsLoading));

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
        await LoadFromPathAsync(file);
    }

    public async Task LoadFromPathAsync(string file)
    {
        if (string.IsNullOrWhiteSpace(file) || !System.IO.File.Exists(file)) return;
        var s = await _settings.LoadAsync();
        IsLoading = true;
        LoadProgress = 0.0;
        CancelLoad(); // cancelar si había una carga previa en curso
        _loadCts = new System.Threading.CancellationTokenSource();
        var ct = _loadCts.Token;
        try
        {
            string[] lines;
            try
            {
                // Lectura de archivo (async)
                var swRead = System.Diagnostics.Stopwatch.StartNew();
                lines = await File.ReadAllLinesAsync(file, ct);
                swRead.Stop();
                LastLoadReadMs = swRead.Elapsed.TotalMilliseconds;
                LoadProgress = 0.2;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error leyendo archivo");
                return;
            }

            if (ct.IsCancellationRequested) return;

            try
            {
                // Parse en background
                _parser.IgnoreAdditionalAxes = s.IgnoreAdditionalAxes;
                var swParse = System.Diagnostics.Stopwatch.StartNew();
                await Task.Run(() => _parser.Parse(lines), ct);
                swParse.Stop();
                LastLoadParseMs = swParse.Elapsed.TotalMilliseconds;
                LoadProgress = 0.6;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ParseException pex)
            {
                _logger.LogWarning(pex, "GCode parse error");
                await _dialogs.AlertAsync("Parse Error", pex.Message);
                return;
            }

            if (ct.IsCancellationRequested) return;

            if (_parser.Warnings.Count > 0)
            {
                const string header = "Warning! Parsing this file resulted in some warnings!\n\nDo not use OpenCNCPilot's edit functions unless you are sure that these warnings can be ignored!\n\nBe aware that the affected lines will likely move when using edit functions.";
                await _dialogs.ShowWarningsAsync(header, _parser.Warnings);
            }

            // Popular colecciones (UI)
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
                try
                {
                    var swBuild = System.Diagnostics.Stopwatch.StartNew();
                    var geo = await Task.Run(() => _pathBuilder.Build(concrete), ct);
                    swBuild.Stop();
                    LastLoadBuildMs = swBuild.Elapsed.TotalMilliseconds;
                    GeometryData = geo;
                    RapidCount = geo?.RapidCount ?? 0;
                    CutCount = geo?.CutCount ?? 0;
                }
                catch (OperationCanceledException)
                {
                    return;
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
        finally
        {
            LoadProgress = 1.0;
            IsLoading = false;
            CancelLoad(); // limpiar CTS
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
        CancelLoad();
        IsLoading = false;
        LoadProgress = 0.0;
        _sender.Clear();
        GCodeLines.Clear();
        ParsedCommands = null;
        GeometryData = null;
        RapidCount = 0;
        CutCount = 0;
        LastLoadReadMs = LastLoadParseMs = LastLoadBuildMs = 0;
        this.RaisePropertyChanged(nameof(LastLoadTotalMs));
        this.RaisePropertyChanged(nameof(LoadTimingSummary));
        UpdateFromSender();
        CurrentFileName = string.Empty;
    _matchIndices.Clear();
    SearchMatchCount = 0;
    SearchMatchIndex = -1;
    this.RaisePropertyChanged(nameof(SearchStatus));
        this.RaisePropertyChanged(nameof(MoveSummary));
        this.RaisePropertyChanged(nameof(MoveCount));
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

    private void CancelLoad()
    {
        try
        {
            if (_loadCts != null)
            {
                if (!_loadCts.IsCancellationRequested)
                    _loadCts.Cancel();
                _loadCts.Dispose();
            }
        }
        catch { }
        finally { _loadCts = null; }
    }
}
