## OpenGL/Skia Viewer PoC Tasks

- [x] Create control `GCodeViewport` in `src/OpenCNCPilot.UI/Views/Controls/` deriving `OpenGlControlBase`.
- [x] Expose properties: `double Zoom` (default 1.0), `double RotationX`, `double RotationY` as `StyledProperty`.
- [x] Implement `OnOpenGlInit`, `OnOpenGlRender`, `OnOpenGlDeinit` with safe error handling.
- [x] Render axes (X=red, Y=green, Z=blue) with basic lines.
- [x] Mouse input: pan (MMB drag), rotate (RMB drag), zoom (wheel) – no blocking UI thread.
- [x] Resize handling: viewport aspect correction.
- [x] Headless safety: skip GL calls if context unavailable (for CI).
- [x] Integrate into `Views/MainWindow.axaml` replacing placeholder panel.
- [x] Basic performance check: target 60 FPS idle; no exceptions under resize.
- [x] Cleanup: dispose GL resources on `OnOpenGlDeinit`.
- [x] AutoFit helper extraído (`ViewportAutoFit`) con pruebas unitarias.
- [x] Overlays: Grid/Origin/Bounds como `StyledProperty` con render en Skia.
- [x] Enlaces: Bindings desde `MainWindowViewModel` hacia `GCodeViewport`.
- [x] Persistencia: overlay settings guardadas/cargadas vía `JsonSettingsService`.
- [x] Toolbar: toggles rápidos Grid/Origin/Bounds en la barra del viewport.
- [x] Toolbar: acciones adicionales “Snap to 2D” y “Fit + Reset”.
	- Pruebas unitarias en VM verifican rotaciones y FitRequestId.
