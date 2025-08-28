# Arquitectura UI Moderna (Avalonia)

## GCodeViewport
StyledProperties: `Zoom`, `RotationX`, `RotationY`, `PanX`, `PanY`, `Commands`, `FitRequestId`.
Sensibilidades configurables: `RotateSensitivity` (deg/px), `PanSensitivity` (world/px), `ZoomStepFactor` (>1).
Render: OpenGlControlBase + Skia (proyección ortográfica XY). Maneja ausencia de GL de forma segura.
AutoFit: calcula bounds de `Commands`, ajusta `Zoom` y centra `PanX/PanY` al punto medio de los bounds.

## MainWindowViewModel
Expone propiedades espejo para el visor: `ViewerZoom`, `ViewerRotationX/Y`, `ViewerPanX/Y`, `ViewerRotateSensitivity`, `ViewerPanSensitivity`, `ViewerZoomStepFactor`.
Comandos: `FitToView`, `ZoomIn/Out/Reset`, `Rotate*/Pan*`, `ResetViewCommand`.
Persiste sensibilidades vía `ISettingsService` y recarga tras guardar en `SettingsWindow`.

## SettingsWindow
Usa `NumericUpDown` para validar rangos:
  - Rotate: 0.05–2.0
  - Pan: 0.001–1.0
  - Zoom step: 1.01–1.5
Los valores se clamping en el ViewModel para robustez adicional.

## Testing
Tests de VM y matemáticas del visor en `OpenCNCPilot.UI.Tests` (sin acceso GL).
Parser y modelos en `OpenCNCPilot.Core.Tests`.# Arquitectura de OpenCNCPilot (Moderna)

## Visión General
OpenCNCPilot se estructura en tres capas principales siguiendo principios de modularidad y portabilidad:

- `OpenCNCPilot.Core` (lógica de dominio): parser de G‑Code, modelos geométricos, utilidades puras.
- `OpenCNCPilot.Hardware` (acceso a hardware): abstracciones + implementación de puertos seriales.
- `OpenCNCPilot.UI` (presentación): Avalonia UI con MVVM + ReactiveUI, servicios de UI y visor de trayectorias.

## Principios
- Cross‑platform (.NET 8, Avalonia 11).
- MVVM estricto: lógica en ViewModels; Views sin reglas de negocio.
- Inyección de dependencias con `Microsoft.Extensions.DependencyInjection`.
- Tests unitarios en Core; tests de UI centrados en ViewModels/servicios.
- CI/CD con GitHub Actions (matriz OS, cobertura, releases self‑contained).

## Componentes

### Core (`src/OpenCNCPilot.Core`)
- `GCode/IGCodeParser.cs` y `GCode/GCodeParser.cs`: parseo de G‑Code con soporte para
  - G0/G1/G2/G3, `F`, `S`, unidades G20/G21, modos G90/G91, planos G17/G18/G19.
  - Arcos con IJK o R; modos G90.1/G91.1 (si aplica).
  - `Warnings` acumulados y `ParseException` con contexto.
  - Opción `IgnoreAdditionalAxes` para A/B/C.
- `GCode/GCodeCommands/*`: modelos de comandos (`Line`, `Arc`, `Dwell`, `MCode`, `Spindle`).
- Utilidades/Geometría en `Geometry/*`.

### Hardware (`src/OpenCNCPilot.Hardware`)
- `Services/ISerialPortService.cs`: interfaz para puertos seriales (eventos `ConnectionStateChanged`, `DataReceived`).
- Implementación concreta con `System.IO.Ports` (cross‑platform) y consideraciones Linux (RTS/DTR, timeouts).

### UI (`src/OpenCNCPilot.UI`)
- Views en `Views/*` y ViewModels en `ViewModels/*`.
- `MainWindowViewModel`:
  - Conecta/Desconecta puerto, lista puertos, carga archivos de G‑Code.
  - Integra `IGCodeParser` y expone `GCodeCommands` para el visor.
  - Expone controles del visor: `ViewerZoom`, `FitRequestId` y comandos `FitToView/ZoomIn/ZoomOut/ZoomReset`.
- `Views/Controls/GCodeViewport.axaml.cs`:
  - Deriva de `OpenGlControlBase` y renderiza con Skia (GRContext).
  - `StyledProperty`: `Zoom`, `RotationX`, `RotationY`, `Commands`, `FitRequestId`.
  - `Zoom`: escala lógica interpretada como mm por half‑screen unit (valores pequeños = zoom in).
  - Arcos: se aproximan con polilíneas; a futuro, segmentación adaptativa por curvatura/longitud.
  - `AutoFit()` sobre `Commands`/`FitRequestId` y dibuja ejes + líneas/arcos.
- Servicios de UI (`UI/Services/*`):
  - `IDialogService`: alertas, confirmaciones, open/save, warnings, prompt de números.
  - `ISettingsService`: persistencia JSON de `AppSettings` (ej. `LastGCodeDirectory`, `IgnoreAdditionalAxes`).

### DI y Configuración
- Registro de servicios en `App.axaml.cs` usando `ServiceCollection`.
- Uso de `ILogger` (Microsoft.Extensions.Logging) para logs.
  - `IGCodeParser`: registrado como Singleton porque no mantiene estado entre invocaciones de `Parse()`. Si en el futuro almacena estado por sesión, cambiar a `Transient`.

## Flujo de Datos (Cargar y Visualizar G‑Code)
1. Usuario ejecuta `LoadGCodeFileCommand` en `MainWindowViewModel`.
2. `IDialogService.OpenFilesAsync` devuelve la ruta; se actualiza `LastGCodeDirectory` vía `ISettingsService`.
3. `IGCodeParser.Parse` procesa líneas → `Commands` + `Warnings`.
4. El VM muestra warnings (`IDialogService.ShowWarningsAsync`) y expone `GCodeCommands`.
5. La vista enlaza `GCodeViewport.Commands` a `GCodeCommands` → `AutoFit()` y render.
6. Toolbar de visor invoca comandos (`FitToView`, `ZoomIn/Out`, `ZoomReset`) que actualizan `ViewerZoom`/`FitRequestId`.

## Testing
- `tests/OpenCNCPilot.Core.Tests`: unit tests del parser (G0/1/2/3, unidades, modos, arcos, errores, performance básica).
- `tests/OpenCNCPilot.UI.Tests`: pruebas de ViewModel/servicios de UI (warnings, carga de archivo, zoom/fit del visor).
- Se evitan dependencias reales de GL en tests; se prueban bindings y lógica del VM.

## CI/CD
- Workflow de CI: build + test (ubuntu-latest, windows-latest), cobertura en Linux.
- Workflow de release: publicación self‑contained single-file por OS con artefactos por plataforma.

## Roadmap Próximo (UI/Viewer)
- Rotación y pan interactivos (ratón/gestos), límites y suavizado.
- Capas/estilos de trayectorias, resaltado de selección y puntero de herramienta.
- Manejo de offsets/planos avanzados y preview 3D opcional.
