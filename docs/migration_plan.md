📋 Plan de Migración Cross-Platform

## Fase 1: Preparación y Análisis (1-2 semanas)
- [x] Auditoría completa del código WPF actual
- [x] Identificar dependencias Windows-specific
- [x] Documentar componentes custom y controles especializados

Resumen Fase 1 (hallazgos clave)
- WPF APIs detectadas: `System.Windows.*`, `PresentationCore`, `PresentationFramework`, `System.Windows.Media.Media3D`.
- Dependencias Windows-specific: `HelixToolkit.Wpf`, `System.Windows.Forms`, `System.Drawing`, `Microsoft.Win32` (diálogos).
- 3D viewer actual: basado en `HelixToolkit.Wpf/Media3D` (reemplazar por OpenGL/Skia en Avalonia).
- Diálogos/ventanas a migrar documentados: `SettingsWindow`, `GrblSettingsWindow`, `EditMacroItemWindow`, `EnterNumberWindow`, `WarningWindow`.

## Fase 2: Setup del Proyecto Avalonia (1 semana)
- [x] Crear nuevo proyecto Avalonia
- [x] Configurar estructura de carpetas
 - [x] Migrar lógica de negocio (no-UI)
- [x] Configurar inyección de dependencias

Resumen Fase 2 (TDD y migración de Core)
- Se migró el parser de G‑Code y modelos a `src/OpenCNCPilot.Core/GCode/*` evitando dependencias WPF.
- Se añadieron pruebas unitarias concisas en `tests/OpenCNCPilot.Core.Tests/GCode/GCodeParserTests.cs` cubriendo:
    - G0 rápidos sin feed obligatorio
    - G1 métricos con `F` definido
    - Error honesto en G1 sin feed (`ParseException`)
    - Palabras desconocidas generan warning
    - Velocidad de spindle negativa: warning y valor absoluto
- Los tests pasan en `net8.0` (`dotnet test` verde). Esto asegura base estable para Fases 3 y 4.
- Nota: En Core se ignoran por defecto ejes adicionales `A/B/C` (antes dependía de `Properties.Settings.Default.IgnoreAdditionalAxes`).

Integración en UI (avance)
- Inyectado `IGCodeParser` en DI (`App.axaml.cs`) y utilizado en `MainWindowViewModel` dentro de `LoadGCodeFileCommand`. Los warnings provienen del parser de Core y se muestran vía `IDialogService.ShowWarningsAsync`.
- Añadido test de integración ligero en `tests/OpenCNCPilot.UI.Tests/MainWindowViewModel_GCodeTests.cs` que verifica el flujo de warnings ante palabras desconocidas.

## Fase 3: Migración de UI (3-4 semanas)
- [x] Migrar ventana principal
- [x] Adaptar controles custom
- [x] Migrar diálogos y ventanas secundarias
    - [x] SettingsWindow → Avalonia
    - [x] GrblSettingsWindow → Avalonia
    - [x] EditMacroItemWindow → Avalonia
    - [x] EnterNumberWindow → Avalonia
    - [x] WarningWindow → Avalonia
    - [x] Wiring en `MainWindowViewModel` del diálogo de GRBL Settings + integración con `ISerialPortService`
 - [x] Implementar visualización 3D (OpenGL)
 - [x] PoC visor OpenGL/Skia (ejes básicos, AutoFit, zoom/fit)
 - [x] Rotación y pan (propiedades/commands MVVM) e interacción con ratón (drag/wheel)
 - [x] Reset del visor (comando y botón en toolbar)
 - [x] Sensibilidades configurables (Rotate/Pan/Zoom) con persistencia en Settings y bindings en `GCodeViewport`
 - [x] AutoFit centra el encuadre ajustando `PanX/PanY` al punto medio de los bounds
 - [x] AutoFit validado por TDD: `ViewportAutoFitTests` y helper puro `ViewportAutoFit.Compute()`
 - [x] Refactor `GCodeViewport.AutoFit()` -> usa `ViewportAutoFit` y corrige pan inconsistente
 - [x] Overlays del visor: Grid, Origin, Bounds con StyledProperties y render en Skia
 - [x] Toolbar del visor: toggles Grid/Origin/Bounds, ajuste directo de tamaño de grilla
 - [x] Atajos UX: botones "Snap to 2D" y "Fit + Reset"
 - [x] Persistencia de overlays en Settings + pruebas de round-trip
 - [x] Pruebas VM: comandos de visor (zoom/fit/reset/snap) y propiedades enlazadas

## Fase 4: Testing y Estabilización (2 semanas)
- [ ] Pruebas en Windows
- [ ] Pruebas en Linux (Ubuntu, Debian)
- [ ] Optimización de rendimiento
- [ ] Corrección de bugs
 - [ ] Cobertura mínima 70% en Core

## Fase 5: CI/CD con GitHub Actions (1 semana)
- [x] CI: build y test en matriz (ubuntu-latest, windows-latest)
- [x] CI: cobertura con Codecov en Linux
- [x] Release: workflow `release.yml` por tags `v*`
- [x] Release: publicar artefactos self-contained single-file por SO
- [x] Release: notas con changelog automático

Avances implementados (TDD Phase 3)
- Tests UI: `ViewportMathTests` valida transformaciones (rotación X/Y y pan) y `MainWindowViewModel_ViewportTests` cubre comandos de zoom/fit.
- Tests UI adicionales: `MainWindowViewModel_ViewportTests.ResetViewCommand_Resets_Viewer_Properties` y `GCodeViewport_SensitivitiesTests` (defaults y configurabilidad de sensibilidades).
- Nuevo helper `ViewportMath` encapsula la proyección ortográfica con rotaciones X/Y y pan.
- `GCodeViewport` integra rotación/pan (StyledProperties `RotationX`, `RotationY`, `PanX`, `PanY`) aplicadas en render Skia, y añade `RotateSensitivity`, `PanSensitivity`, `ZoomStepFactor` como `StyledProperty` para interacción configurable; `AutoFit()` ahora centra pan y ajusta `Zoom`.
 - Extraído `ViewportAutoFit` (puro) y cubierto con `ViewportAutoFitTests`:
     - `Compute_Sets_Zoom_To_Fit_Bounds_With_Margin`
     - `Compute_Centers_Using_Rotation_Transform`
 - `GCodeViewport.AutoFit()` refactorizado para delegar en `ViewportAutoFit` y usar `Bounds` reales del control; corrige lógica duplicada y pan contradictorio.
- `MainWindowViewModel` expone propiedades `ViewerRotationX/Y`, `ViewerPanX/Y` y comandos `Rotate*/Pan*`; `MainWindow.axaml` agrega bindings y botones de control.
- `MainWindowViewModel` añade `ResetViewCommand` y propiedades `ViewerRotateSensitivity`, `ViewerPanSensitivity`, `ViewerZoomStepFactor`; `MainWindow.axaml` enlaza estas propiedades al `GCodeViewport` y agrega botón "Reset".
- `SettingsWindow` incorpora campos para sensibilidades del visor (rotate/pan/zoom step) y las persiste vía `ISettingsService` (`JsonSettingsService`).
- Tests ejecutan verde en Linux; CI filtra `Category!=RequiresHardware` para evitar dependencias de hardware/GL.

- Tests UI (GRBL Settings): `MainWindowViewModel_GrblSettingsTests`
    - Verifica que `OpenGrblSettingsCommand` envía `$$` al estar conectado y abre el diálogo.
    - Simula `DataReceived` con líneas `$n=value` y valida que `GrblSettingsViewModel.Items` se puebla y actualiza (`$0=10`, `$1=255`, `$10=3`).
- Endurecido `App.Services` para tests headless: propiedad null-safe para evitar NRE cuando no hay `Application.Current` en pruebas.

3. Estructura de Testing Propuesta

OpenCNCPilot/
├── src/
│   ├── OpenCNCPilot.Core/           # Lógica de negocio
│   ├── OpenCNCPilot.UI/             # UI Avalonia
│   └── OpenCNCPilot.Hardware/       # Comunicación con hardware
├── tests/
│   ├── OpenCNCPilot.Core.Tests/     # Unit tests
│   ├── OpenCNCPilot.Integration.Tests/
│   └── OpenCNCPilot.UI.Tests/       # UI tests con Avalonia
└── benchmarks/
    └── OpenCNCPilot.Benchmarks/     # Performance tests


## Notas de progreso (resumen)
- Proyecto Avalonia creado en `src/OpenCNCPilot.UI/` con MVVM (ReactiveUI) y `MainWindow` funcional.
- Inyección de dependencias configurada en `App.axaml.cs` usando `Microsoft.Extensions.DependencyInjection`.
- Estructura modular creada: `Core`, `Hardware`, `UI` y `tests/`.
- Servicio de puertos seriales implementado (`ISerialPortService` y `SerialPortService`).
 - CI configurado: build/test en ubuntu y windows; cobertura enviada a Codecov.
 - Servicio de diálogos implementado (`IDialogService` + `AvaloniaDialogService`) y registrado en DI.
 - `SettingsWindow` migrada a Avalonia (View + ViewModel) y accesible desde `MainWindow` vía comando.
 - Control OpenGL `GCodeViewport` integrado con render Skia, ejes básicos, líneas/arcos y frame loop.
 - Diálogo de entrada numérica soportado mediante `PromptNumberAsync` en `IDialogService`.
 - `WarningWindow` migrado y expuesto vía `IDialogService.ShowWarningsAsync`; flujos de warnings deben usar este servicio en la UI moderna.
 - Corregido error de build AVLN:0004 añadiendo paquete `Avalonia.Controls.DataGrid` y manteniendo `StyleInclude` del tema.
 - Agregado `DemoWarningsCommand` en `MainWindowViewModel` y botón "Show Parse Warnings (Demo)" para verificación manual del `WarningWindow`.
 - Inyectado `ISettingsService` en `MainWindowViewModel` y creado `LoadGCodeFileCommand`:
     - Usa `IDialogService.OpenFilesAsync` con filtros de G‑Code y directorio inicial desde `LastGCodeDirectory`.
     - Actualiza `LastGCodeDirectory` al seleccionar archivo (persistido en JSON).
     - Invoca parser de Core con `IgnoreAdditionalAxes` desde settings; muestra warnings generados por Core mediante `ShowWarningsAsync`.
 - Viewer: `GCodeCommands` se exponen en el VM y se enlazan al viewport; `AutoFit()` al cambiar `Commands`; toolbar flotante con `Fit/+/−/100%` enlazada a comandos del VM; propiedades `Zoom` y `FitRequestId` enlazadas.
- Overlays del visor (Grid, Origin, Bounds) y dibujo de bounds; toolbar con toggles y campo de grilla; comandos adicionales Snap to 2D y Fit+Reset.
 - Build Release de `OpenCNCPilot.UI` exitoso en Linux tras las correcciones.

Release y Changelog
- Publicación self-contained single-file activada en `release.yml` usando `-p:PublishSingleFile=true` y `-p:IncludeNativeLibrariesForSelfExtract=true`, con empaquetado `.zip` (Windows) y `.tar.gz` (Linux) por RID.
- Notas de release automatizadas: `softprops/action-gh-release@v2` recoge assets y genera notas a partir de commits/prs (`generate_release_notes: true`), permitiendo changelog automático por tag `v*`.

### Gotchas (OpenGL/Skia en Linux y CI)
- En runners headless puede no haber contexto GL; el control captura excepciones en init/render para no crashear.
- Dependencias del sistema: puede requerir paquetes de X/GL (ej. `libgl1`, `libx11-6`); documentar para empaquetado.
- Las pruebas de UI no ejercen GL; validan ViewModel y bindings para evitar fallos en entornos sin GL.

Sensibilidades del visor (rangos y efecto)
- RotateSensitivity (deg/pixel): recomendado 0.05–2.0; valores altos rotan muy rápido.
- PanSensitivity (world units/pixel): recomendado 0.001–1.0; se multiplica por `Zoom` en el control para mantener sensación.
- ZoomStepFactor (>1): recomendado 1.01–1.5; cuanto mayor, más brusco el zoom por tick.

