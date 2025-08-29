# Plan de implementación — Pestaña “File” (Avalonia)

## Overview
Migrar y completar la pestaña “File” del proyecto original WPF a la nueva UI Avalonia (MVVM + ReactiveUI), cubriendo apertura/guardado/limpieza de G‑Code, listado de líneas, envío a GRBL (start/pause), “go to line”, estado (`FilePosition/FileLength`) y tiempos (`Runtime/EstimatedDuration`), con “Pause on M0/M1/M30”, integrando `IGCodeParser`, `IDialogService`, `ISettingsService`, `ISerialPortService` y el control `GCodeViewport`.

## Requirements
- WHEN el usuario pulsa `Open`, THE SYSTEM SHALL abrir un diálogo, leer y parsear líneas, mostrar warnings si existen, poblar `Commands` (para viewport) y `GCodeLines`, actualizar `CurrentFileName` y `LastGCodeDirectory`.
- WHEN el usuario pulsa `Save`, THE SYSTEM SHALL abrir un diálogo y guardar el G‑Code en disco (MVP: usando las líneas cargadas).
- WHEN el usuario pulsa `Clear`, THE SYSTEM SHALL limpiar el archivo cargado, la lista y el estado (`FilePosition=0`, `Runtime=00:00:00`), refrescar viewport y marcar `HeightMapApplied=false`.
- WHEN el usuario pulsa `Start`, THE SYSTEM SHALL iniciar el envío secuencial (una línea → esperar `ok` → siguiente), avanzar `FilePosition` y actualizar `Runtime`.
- WHEN el usuario pulsa `Pause`, THE SYSTEM SHALL pausar el envío, manteniendo posición de archivo.
- WHEN el usuario ejecuta `Go To`, THE SYSTEM SHALL solicitar un número de línea, validar rango y reposicionar `FilePosition` si no se está enviando.
- WHILE `PauseOnHold` is enabled AND la línea contiene `M0`/`M1`/`M30`, THE SYSTEM SHALL pausar al finalizar esa línea (tras `ok`).
- WHEN se reciben respuestas GRBL `ok`/`error:`, THE SYSTEM SHALL avanzar o pausar y notificar error (MVP: pausar en error).
- THE SYSTEM SHALL deshabilitar `Open/Save/Clear/Goto` durante envío.
- THE SYSTEM SHALL mostrar `FilePosition/FileLength` y `Runtime/EstimatedDuration`, seleccionando la línea actual en la lista.
- THE SYSTEM SHALL encapsular el envío en un servicio testable (`IGCodeSender`) con estados observables.
- THE SYSTEM SHALL recordar `LastGCodeDirectory` y `PauseFileOnHold` en settings, y operar cross‑platform sin APIs Windows-only.

## Implementation Steps
1) Servicio de envío de G‑Code (`IGCodeSender`)
   - Ubicación: `src/OpenCNCPilot.Hardware/Services/`.
   - Interfaz:
     - Props: `State { Idle,Sending,Paused }`, `FilePosition`, `FileLength`, `Runtime: TimeSpan`, `EstimatedDuration: TimeSpan`, `PauseOnHold: bool`, `IsSending`, `CurrentLineText`.
     - Métodos: `Load(IEnumerable<string> lines)`, `Start()`, `Pause()`, `Clear()`, `Goto(int lineIndex)`, `Dispose()`.
     - Eventos/Observables: `StateChanged`, `PositionChanged`, `ErrorOccurred(string)`.
   - Implementación:
     - Usa `ISerialPortService.WriteLine` para emitir y se suscribe a `DataReceived` para consumir `ok`/`error:` (protocolo MVP: una línea en vuelo).
     - Maneja `PauseOnHold` detectando `M0/M1/M30` y forzando pausa tras `ok`.
     - Validaciones: rango `Goto`, prohibir `Goto` en `Sending`, rechazar `Start` sin líneas o sin puerto abierto.
     - Estimación de duración (MVP): heurística simple (p. ej., por cantidad de líneas o usando feeds si el parser los expone); próximo paso: portar cálculo preciso desde el original.

2) ViewModel de archivo (`FileViewModel`)
   - Ubicación: `src/OpenCNCPilot.UI/ViewModels/FileViewModel.cs`.
   - Dependencias: `IDialogService`, `ISettingsService`, `IGCodeParser`, `IGCodeSender`, `ILogger<>`.
   - Props:
     - Archivo: `CurrentFilePath`, `CurrentFileName`, `ObservableCollection<string> GCodeLines`.
     - Envío: `FilePosition`, `FileLength`, `Runtime`, `EstimatedDuration`, `PauseOnHold`, `IsSending`.
     - Viewer: `ReadOnlyObservableCollection<Command> Commands`, `FitRequestId` (para autofit).
   - Comandos (ReactiveCommand):
     - `OpenCommand`: `OpenFilesAsync` → leer → actualizar `LastGCodeDirectory` → `_gcodeParser.Parse(lines)` → mostrar warnings → setear `Commands`, `GCodeLines` → `sender.Load(lines)` → `FitRequestId++`.
     - `SaveCommand`: `SaveFileAsync` → guardar líneas (MVP).
     - `ClearCommand`: limpiar props y `sender.Clear()` → `FitRequestId++`.
     - `StartCommand`, `PauseCommand`, `GotoCommand` (este último via `PromptNumberAsync` y validación).
   - Suscripciones a `IGCodeSender` para refrescar `FilePosition`, `Runtime`, `IsSending` y seleccionar línea en lista.

3) Vista de la pestaña (`FilePanel.axaml`)
   - Ubicación: `src/OpenCNCPilot.UI/Views/Panels/FilePanel.axaml` (+ `.axaml.cs` mínimo).
   - Contenido:
     - Botones `Open`, `Save`, `Clear` (bindings a comandos, `IsEnabled` según estado).
     - Indicadores: `RunFilePosition/RunFileLength`, `RunFileRunTime/RunFileDuration`.
     - `ListBox Items="{Binding GCodeLines}"` con virtualización; `SelectedIndex="{Binding FilePosition}"`.
     - Botones `Start`, `Pause`, `Go To`; `CheckBox` “Pause on M0/M1/M30”.
   - Integración:
     - Insertar en `MainWindow.axaml` (o vista equivalente) dentro de `Expander Header="File"`.
     - `GCodeViewport.Commands` ← `FileViewModel.Commands`; disparar autofit con `FitRequestId`.

4) Integración con `MainWindowViewModel` y DI
   - Añadir propiedad `File` de tipo `FileViewModel` en `MainWindowViewModel` (o inyectar y componer).
   - Delegar/retirar lógica duplicada de carga/limpieza de G‑Code en `MainWindowViewModel`.
   - Registrar `IGCodeSender` y `FileViewModel` en DI (`Program.cs`); `IGCodeSender` gestiona internamente la suscripción a `ISerialPortService.DataReceived`.

5) Settings y persistencia
   - Confirmar/añadir en `AppSettings`: `LastGCodeDirectory: string`, `PauseFileOnHold: bool` (default `false`).
   - `JsonSettingsService`: guardar/cargar ambos campos.

6) Errores y UX
   - Deshabilitar acciones incompatibles durante `Sending`.
   - `Goto` fuera de rango: `AlertAsync`.
   - `Start` sin archivo o sin conexión: `AlertAsync`.
   - `error:` de GRBL: pausar y `AlertAsync` (MVP); backlog para reintentar/omitir/detener.

7) Documentación
   - Actualizar `docs/migration_guide.md` con detalles de la pestaña “File” y diferencias WPF→Avalonia.
   - Describir contrato/estados de `IGCodeSender`.

## Testing
- Unit (xUnit + FluentAssertions)
  - `IGCodeSender`:
    - Start→ok avanza `FilePosition`; finaliza en `Idle` al terminar.
    - Pause en `Sending` cambia a `Paused` y no consume más líneas.
    - `PauseOnHold=true` pausa tras línea con `M0`/`M1`/`M30`.
    - `Goto` permitido en `Idle/Paused` y prohibido en `Sending`; rango inválido falla.
    - En `error:` pasa a `Paused` y emite `ErrorOccurred`.
  - `FileViewModel`:
    - `OpenCommand` carga, actualiza settings, invoca parser, muestra warnings, setea `Commands`/`GCodeLines` y `FitRequestId`.
    - `SaveCommand` escribe archivo elegido; en cancelación no cambia estado.
    - `ClearCommand` limpia y llama `sender.Clear()`.
    - `GotoCommand` usa `PromptNumberAsync`, valida y llama `sender.Goto`.

- Integration (sin hardware real)
  - Fake `ISerialPortService`: capta `WriteLine` y emite `ok` con retardo.
  - Flujo: `Open → Start → ok... → PauseOnHold → Pause`.

- UI/VM (binding)
  - CanExecute de comandos según `IsSending`.
  - `ListBox.SelectedIndex` sigue `FilePosition`.

- Performance
  - Parser con dataset de 10k líneas < 1s en Linux (si no existe, crear caso sintético).
