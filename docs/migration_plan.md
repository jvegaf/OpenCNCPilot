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
 - [ ] Migrar lógica de negocio (no-UI)
- [x] Configurar inyección de dependencias

## Fase 3: Migración de UI (3-4 semanas)
- [x] Migrar ventana principal
- [ ] Adaptar controles custom
- [ ] Migrar diálogos y ventanas secundarias
    - [x] SettingsWindow → Avalonia
    - [ ] GrblSettingsWindow → Avalonia
    - [ ] EditMacroItemWindow → Avalonia
    - [x] EnterNumberWindow → Avalonia
    - [x] WarningWindow → Avalonia
- [ ] Implementar visualización 3D (OpenGL)
 - [ ] PoC visor OpenGL/Skia (ejes básicos, zoom/rotación)

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
- [ ] Release: publicar artefactos self-contained single-file por SO
- [ ] Release: notas con changelog automático

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
 - Control OpenGL `GCodeViewport` integrado (placeholder de render con frame loop).
 - Diálogo de entrada numérica soportado mediante `PromptNumberAsync` en `IDialogService`.
 - `WarningWindow` migrado y expuesto vía `IDialogService.ShowWarningsAsync`; flujos de warnings deben usar este servicio en la UI moderna.
 - Corregido error de build AVLN:0004 añadiendo paquete `Avalonia.Controls.DataGrid` y manteniendo `StyleInclude` del tema.
 - Agregado `DemoWarningsCommand` en `MainWindowViewModel` y botón "Show Parse Warnings (Demo)" para verificación manual del `WarningWindow`.
 - Inyectado `ISettingsService` en `MainWindowViewModel` y creado `LoadGCodeFileCommand`:
     - Usa `IDialogService.OpenFilesAsync` con filtros de G‑Code y directorio inicial desde `LastGCodeDirectory`.
     - Actualiza `LastGCodeDirectory` al seleccionar archivo (persistido en JSON).
     - Implementa heurística temporal de warnings (Q desconocida, `S-` negativo, múltiples G0/1/2/3 en una línea) y muestra `ShowWarningsAsync` si corresponde.
 - Build Release de `OpenCNCPilot.UI` exitoso en Linux tras las correcciones.

