# Changelog

All notable changes to this project will be documented in this file.

Format follows Keep a Changelog and Semantic Versioning.

## [Unreleased]
### Added
- UI: Botones “Snap to 2D” y “Fit + Reset” en la toolbar del visor.
- VM: Comandos `SnapTo2DCommand` y `FitAndResetViewCommand`.
- Tests: Casos para verificar reseteo/rotaciones y `FitRequestId` en VM.
- Docs: Actualización de `docs/opengl_poc_tasks.md`.
- VM: `FileViewModel` ahora soporta carga en background con cancelación (`IsLoading`, `LoadProgress`, `CancelLoadCommand`).
- VM: Métricas de tiempo de carga (`LastLoadReadMs`, `LastLoadParseMs`, `LastLoadBuildMs`, `LastLoadTotalMs`) y resumen `LoadTimingSummary`.
- VM: Contadores de movimientos (`RapidCount`, `CutCount`, `MoveCount`) y `MoveSummary` con desglose.
- UI: `MainWindow.axaml` muestra `LoadTimingSummary` y `MoveSummary` en la barra de estado.
- Tests UI: `FileViewModel_SearchTests`, `FileViewModel_MetricsAndLoadingTests` cubren búsqueda, deshabilitado por carga y formato de métricas.

### Changed
- Integramos nuevos comandos en `MainWindow.axaml` y enlazados al `GCodeViewport`.
- Endurecido el enablement de comandos en `FileViewModel` para considerar `IsLoading` además de `IsBusy`.

### Fixed
-

### Deprecated
-

### Removed
-

### Security
-

## [vX.Y.Z] - YYYY-MM-DD
### Highlights
-

### Added
-

### Changed
-

### Fixed
-

### Breaking Changes
-

---

Release artifacts are available in GitHub Releases.
