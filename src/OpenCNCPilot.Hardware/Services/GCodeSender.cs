using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace OpenCNCPilot.Hardware.Services
{
    /// <summary>
    /// Implementación del servicio de envío de G-Code con protocolo simple (una línea en vuelo)
    /// </summary>
    public sealed class GCodeSender : IGCodeSender
    {
        private readonly ISerialPortService _serial;
        private readonly ILogger<GCodeSender> _logger;
        private readonly object _sync = new();
        private readonly Queue<string> _pending = new();
        private readonly Regex _holdRegex = new("\\bM(0|1|30)\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private List<string> _lines = new();
        private int _pos = 0; // Índice de la siguiente línea a enviar
        private bool _inFlight = false; // hay una línea enviada esperando respuesta
        private DateTime? _startTime;
        private TimeSpan _runtime = TimeSpan.Zero;
        private GCodeSenderState _state = GCodeSenderState.Idle;
        private string _currentLine = string.Empty;

        public event EventHandler<GCodeSenderState>? StateChanged;
        public event EventHandler<int>? PositionChanged;
        public event EventHandler<string>? ErrorOccurred;

        public GCodeSender(ISerialPortService serial, ILogger<GCodeSender> logger)
        {
            _serial = serial ?? throw new ArgumentNullException(nameof(serial));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serial.DataReceived += OnSerialData;
            _serial.ConnectionStateChanged += OnConnChanged;
        }

        public GCodeSenderState State { get { lock (_sync) return _state; } }
        public int FilePosition { get { lock (_sync) return Math.Min(_pos, _lines.Count); } }
        public int FileLength { get { lock (_sync) return _lines.Count; } }
        public TimeSpan Runtime { get { lock (_sync) return _runtime; } }
        public TimeSpan EstimatedDuration { get { lock (_sync)
            {
                // Heurística MVP: 15 ms por línea como estimación básica
                var remaining = Math.Max(0, _lines.Count - _pos);
                return _runtime + TimeSpan.FromMilliseconds(remaining * 15);
            } } }
        public bool PauseOnHold { get; set; }
        public bool IsSending => State == GCodeSenderState.Sending;
        public string CurrentLineText { get { lock (_sync) return _currentLine; } }

        public void Load(IEnumerable<string> lines)
        {
            if (lines is null) throw new ArgumentNullException(nameof(lines));
            lock (_sync)
            {
                EnsureIdleOrPaused();
                _lines = lines.Select(l => l?.TrimEnd('\r', '\n') ?? string.Empty).ToList();
                _pos = 0;
                _runtime = TimeSpan.Zero;
                _startTime = null;
                _currentLine = string.Empty;
                _inFlight = false;
            }
            RaisePositionChanged();
        }

        public void Start()
        {
            lock (_sync)
            {
                if (_lines.Count == 0)
                    throw new InvalidOperationException("No hay líneas cargadas");
                if (!_serial.IsOpen)
                    throw new InvalidOperationException("Puerto serial no está abierto");
                if (_state == GCodeSenderState.Sending)
                    return;

                _state = GCodeSenderState.Sending;
                _startTime ??= DateTime.UtcNow;
            }
            RaiseStateChanged();
            TrySendNext();
        }

        public void Pause()
        {
            bool changed = false;
            lock (_sync)
            {
                if (_state != GCodeSenderState.Paused)
                {
                    _state = GCodeSenderState.Paused;
                    changed = true;
                }
            }
            if (changed) RaiseStateChanged();
        }

        public void Clear()
        {
            lock (_sync)
            {
                _lines.Clear();
                _pos = 0;
                _inFlight = false;
                _currentLine = string.Empty;
                _state = GCodeSenderState.Idle;
                _startTime = null;
                _runtime = TimeSpan.Zero;
            }
            RaiseStateChanged();
            RaisePositionChanged();
        }

        public void Goto(int lineIndex)
        {
            lock (_sync)
            {
                EnsureIdleOrPaused();
                if (lineIndex < 0 || lineIndex > _lines.Count)
                    throw new ArgumentOutOfRangeException(nameof(lineIndex));
                _pos = lineIndex;
                _inFlight = false;
                _currentLine = string.Empty;
            }
            RaisePositionChanged();
        }

        private void TrySendNext()
        {
            lock (_sync)
            {
                if (_state != GCodeSenderState.Sending) return;
                if (_inFlight) return;
                if (_pos >= _lines.Count)
                {
                    // fin de archivo
                    _state = GCodeSenderState.Idle;
                    _runtime = ComputeRuntime();
                    _startTime = null;
                    _currentLine = string.Empty;
                    _inFlight = false;
                    // notificar
                    goto NotifyState;
                }

                var line = _lines[_pos];
                _currentLine = line;
                _inFlight = true;
                try
                {
                    _serial.WriteLine(line);
                    _logger.LogDebug("TX[{Index}]: {Line}", _pos, line);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error enviando línea");
                    TransitionToPaused($"Error al enviar: {ex.Message}");
                }
            }
            return;

        NotifyState:
            RaiseStateChanged();
            RaisePositionChanged();
        }

        private void OnSerialData(object? sender, string data)
        {
            if (string.IsNullOrEmpty(data)) return;

            // El hardware puede agrupar múltiples líneas; procesar por tokens
            var tokens = data.Replace("\r", string.Empty)
                             .Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var t in tokens)
            {
                var token = t.Trim();
                if (token.Length == 0) continue;

                if (token.StartsWith("ok", StringComparison.OrdinalIgnoreCase))
                {
                    HandleOk();
                }
                else if (token.StartsWith("error:", StringComparison.OrdinalIgnoreCase))
                {
                    TransitionToPaused(token);
                }
                else
                {
                    // Otras líneas de estado se ignoran en MVP
                    _logger.LogTrace("RX: {Token}", token);
                }
            }
        }

        private void HandleOk()
        {
            bool requestNext = false;
            bool atEnd = false;
            lock (_sync)
            {
                if (_state != GCodeSenderState.Sending) return;
                if (!_inFlight) return;

                // completar la línea actual
                _inFlight = false;

                // Pausa por M-codes si está activo
                if (PauseOnHold && _holdRegex.IsMatch(_currentLine))
                {
                    _logger.LogInformation("Pausa por M-code en línea {Index}", _pos);
                    _state = GCodeSenderState.Paused;
                    _runtime = ComputeRuntime();
                    _startTime = null;
                    requestNext = false;
                }
                else
                {
                    _pos++;
                    _runtime = ComputeRuntime();
                    atEnd = _pos >= _lines.Count;
                    requestNext = !atEnd;
                    if (atEnd)
                    {
                        _state = GCodeSenderState.Idle;
                        _startTime = null;
                    }
                }
            }

            RaisePositionChanged();
            RaiseStateChanged();
            if (requestNext) TrySendNext();
        }

        private void TransitionToPaused(string errorMessage)
        {
            lock (_sync)
            {
                _state = GCodeSenderState.Paused;
                _inFlight = false;
                _runtime = ComputeRuntime();
                _startTime = null;
            }
            ErrorOccurred?.Invoke(this, errorMessage);
            RaiseStateChanged();
            RaisePositionChanged();
        }

        private void OnConnChanged(object? sender, bool connected)
        {
            if (!connected)
            {
                TransitionToPaused("Conexión serial cerrada");
            }
        }

        private TimeSpan ComputeRuntime()
        {
            if (_startTime.HasValue)
                return DateTime.UtcNow - _startTime.Value;
            return _runtime;
        }

        private void EnsureIdleOrPaused()
        {
            if (_state == GCodeSenderState.Sending)
                throw new InvalidOperationException("Operación no permitida durante envío");
        }

        private void RaiseStateChanged() => StateChanged?.Invoke(this, State);
        private void RaisePositionChanged() => PositionChanged?.Invoke(this, FilePosition);

        public void Dispose()
        {
            _serial.DataReceived -= OnSerialData;
            _serial.ConnectionStateChanged -= OnConnChanged;
        }
    }
}
