## OpenGL/Skia Viewer PoC Tasks

- [ ] Create control `GCodeViewport` in `src/OpenCNCPilot.UI/Views/Controls/` deriving `OpenGlControlBase`.
- [ ] Expose properties: `double Zoom` (default 1.0), `double RotationX`, `double RotationY` as `StyledProperty`.
- [ ] Implement `OnOpenGlInit`, `OnOpenGlRender`, `OnOpenGlDeinit` with safe error handling.
- [ ] Render axes (X=red, Y=green, Z=blue) with basic lines.
- [ ] Mouse input: pan (MMB drag), rotate (RMB drag), zoom (wheel) – no blocking UI thread.
- [ ] Resize handling: viewport aspect correction.
- [ ] Headless safety: skip GL calls if context unavailable (for CI).
- [ ] Integrate into `Views/MainWindow.axaml` replacing placeholder panel.
- [ ] Basic performance check: target 60 FPS idle; no exceptions under resize.
- [ ] Cleanup: dispose GL resources on `OnOpenGlDeinit`.
