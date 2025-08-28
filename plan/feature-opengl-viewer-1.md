---
goal: OpenGL/Skia GCode Viewer PoC (Axes + Interaction)
version: 1.0
date_created: 2025-08-28
last_updated: 2025-08-28
owner: UI Team
status: 'Planned'
tags: [feature, opengl, ui]
---

# Introduction

![Status: Planned](https://img.shields.io/badge/status-Planned-blue)

Implement a minimal 3D viewer control using Avalonia `OpenGlControlBase` capable of rendering XYZ axes and basic camera interactions.

## 1. Requirements & Constraints

- **REQ-001**: Provide `GCodeViewport` control with `Zoom`, `RotationX`, `RotationY` styled properties.
- **REQ-002**: Render XYZ axes with distinct colors.
- **REQ-003**: Mouse interactions: wheel zoom, RMB rotate, MMB pan.
- **REQ-004**: Safe in headless/CI environments.
- **CON-001**: .NET 8, Avalonia 11, no WPF/OpenTK.
- **GUD-001**: No logic in code-behind beyond rendering and input wiring.

## 2. Implementation Steps

| ID | Task | Path | Completion Criteria |
|----|------|------|---------------------|
| TASK-001 | Create control files | `src/OpenCNCPilot.UI/Views/Controls/GCodeViewport.*` | Compiles; properties available |
| TASK-002 | Draw axes in `OnOpenGlRender` | same | Axes visible with correct colors |
| TASK-003 | Add input handling | same | Zoom/rotate/pan work smoothly |
| TASK-004 | Integrate into MainWindow | `src/OpenCNCPilot.UI/Views/MainWindow.axaml` | Control replaces placeholder |
| TASK-005 | Headless guards | same | No exceptions in CI |

## 3. Alternatives

- **ALT-001**: SkiaSharp-only 2D — insufficient for 3D camera
- **ALT-002**: OpenTK — adds dependency and complexity

## 4. Dependencies

- **DEP-001**: Avalonia.OpenGL

## 5. Files

- **FILE-001**: `src/OpenCNCPilot.UI/Views/Controls/GCodeViewport.axaml`
- **FILE-002**: `src/OpenCNCPilot.UI/Views/Controls/GCodeViewport.axaml.cs`

## 6. Testing

- **TEST-001**: Smoke test: window renders without exceptions
- **TEST-002**: Property changes update view (unit test with property change notifications if possible)

## 7. Risks & Assumptions

- **RISK-001**: GL context issues on Linux runners; mitigated with try/catch and guards
- **ASSUMPTION-001**: GPU drivers present locally for development

## 8. Related Specifications / Further Reading

- docs/opengl_poc_tasks.md
- Avalonia OpenGL docs
