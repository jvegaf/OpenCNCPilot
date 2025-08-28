---
name: "UI – OpenGL/Skia Viewer PoC"
about: Implement minimal 3D viewer control using Avalonia OpenGL
title: "ui: OpenGL/Skia viewer PoC"
labels: ["ui", "opengl", "poc"]
assignees: []
---

## Summary
Create `GCodeViewport` control using `OpenGlControlBase` with basic axes and interactions.

## Tasks
- [ ] Styled properties: `Zoom`, `RotationX`, `RotationY`
- [ ] Render loop: init/render/deinit; clear background
- [ ] Draw axes XYZ (red/green/blue)
- [ ] Mouse input: wheel zoom; RMB rotate; MMB pan
- [ ] Resize handling and aspect correction
- [ ] Headless/CI safety (skip GL if not available)
- [ ] Integrate into `MainWindow.axaml`

## Acceptance Criteria
- [ ] Control renders without exceptions on Windows/Linux
- [ ] Axes visible; interactions update view
- [ ] No WPF/OpenTK dependencies
