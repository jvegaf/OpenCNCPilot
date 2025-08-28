---
goal: Migrate SettingsWindow to Avalonia MVVM
version: 1.0
date_created: 2025-08-28
last_updated: 2025-08-28
owner: UI Team
status: 'Planned'
tags: [feature, migration, ui]
---

# Introduction

![Status: Planned](https://img.shields.io/badge/status-Planned-blue)

Migrate `OpenCNCPilot/SettingsWindow.xaml(.cs)` to Avalonia: `src/OpenCNCPilot.UI/Views/SettingsWindow.axaml` + `ViewModels/SettingsWindowViewModel.cs`.

## 1. Requirements & Constraints

- **REQ-001**: Parity for settings display, editing, and save/cancel.
- **REQ-002**: Validation via ReactiveUI or Avalonia Validation.
- **CON-001**: No code-behind business logic.

## 2. Implementation Steps

| ID | Task | Path | Completion Criteria |
|----|------|------|---------------------|
| TASK-001 | Create ViewModel | `src/OpenCNCPilot.UI/ViewModels/SettingsWindowViewModel.cs` | Exposes properties and Save/Cancel commands |
| TASK-002 | Create View | `src/OpenCNCPilot.UI/Views/SettingsWindow.axaml` | Bindings, layout, validation |
| TASK-003 | Wire DI | `App.axaml.cs` | ViewModel registered, open via `IDialogService` |
| TASK-004 | Tests | `tests/OpenCNCPilot.UI.Tests/` | VM tests pass |

## 3. Alternatives

- **ALT-001**: Direct code-behind — rejected.

## 4. Dependencies

- **DEP-001**: IDialogService plan.

## 5. Files

- **FILE-001**: `src/OpenCNCPilot.UI/ViewModels/SettingsWindowViewModel.cs`
- **FILE-002**: `src/OpenCNCPilot.UI/Views/SettingsWindow.axaml`

## 6. Testing

- **TEST-001**: Verify Save command writes settings to JSON service (stub if not yet implemented).

## 7. Risks & Assumptions

- **RISK-001**: Differences in layout sizing; verify on Windows and Linux.

## 8. Related Specifications / Further Reading

- docs/migration_guide.md
