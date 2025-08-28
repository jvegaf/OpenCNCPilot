---
goal: Migrate WPF dialogs to Avalonia MVVM (batch 1)
version: 1.0
date_created: 2025-08-28
last_updated: 2025-08-28
owner: UI Team
status: 'Planned'
tags: [feature, migration, ui]
---

# Introduction

![Status: Planned](https://img.shields.io/badge/status-Planned-blue)

Migrate five WPF dialogs/windows to Avalonia 11 using MVVM + ReactiveUI, preserving behavior while removing Windows-specific APIs.

## 1. Requirements & Constraints

- **REQ-001**: Migrate `SettingsWindow`, `GrblSettingsWindow`, `EditMacroItemWindow`, `EnterNumberWindow`, `WarningWindow`.
- **REQ-002**: Replace event handlers with `ReactiveCommand`.
- **REQ-003**: Remove `System.Windows.*` and `Microsoft.Win32` usage.
- **SEC-001**: No elevation or registry usage.
- **CON-001**: .NET 8; Avalonia 11; ReactiveUI.
- **GUD-001**: MVVM strict; DI via `ServiceCollection`.
- **PAT-001**: Use `IDialogService` abstraction where required.

## 2. Implementation Steps

| ID | Task | Path | Completion Criteria |
|----|------|------|---------------------|
| TASK-001 | Create `IDialogService` interface and `AvaloniaDialogService` | `src/OpenCNCPilot.UI/Services/` | Interface with Open/Save dialogs and alerts compiles, DI wired |
| TASK-002 | SettingsWindow migration (View + VM) | `src/OpenCNCPilot.UI/Views/SettingsWindow.axaml` `src/OpenCNCPilot.UI/ViewModels/SettingsWindowViewModel.cs` | Parity for settings load/save and validation |
| TASK-003 | GrblSettingsWindow migration | same as above | Can import/export GRBL settings without `Microsoft.Win32` |
| TASK-004 | EditMacroItemWindow migration | same as above | Create/update macros through VM |
| TASK-005 | EnterNumberWindow migration | same as above | Numeric input with validation and OK/Cancel |
| TASK-006 | WarningWindow migration | same as above | Confirmation dialog with result |
| TASK-007 | Wire routes to dialogs from MainWindow VM | `src/OpenCNCPilot.UI/ViewModels/MainWindowViewModel.cs` | Commands open dialogs via `IDialogService` |

## 3. Alternatives

- **ALT-001**: Keep Windows dialogs via `Microsoft.Win32` — rejected (Windows-only).
- **ALT-002**: Use community dialog libs — deferred; native Avalonia sufficient.

## 4. Dependencies

- **DEP-001**: Avalonia 11, Avalonia.ReactiveUI
- **DEP-002**: Microsoft.Extensions.* (DI, Logging)

## 5. Files

- **FILE-001**: `src/OpenCNCPilot.UI/Services/IDialogService.cs`
- **FILE-002**: `src/OpenCNCPilot.UI/Services/AvaloniaDialogService.cs`
- **FILE-003**: `src/OpenCNCPilot.UI/Views/*Window.axaml`
- **FILE-004**: `src/OpenCNCPilot.UI/ViewModels/*WindowViewModel.cs`

## 6. Testing

- **TEST-001**: Unit tests for `IDialogService` stubs/mocks
- **TEST-002**: VM tests for dialog commands (no UI)

## 7. Risks & Assumptions

- **RISK-001**: Behavioral differences WPF→Avalonia; mitigated with VM tests
- **ASSUMPTION-001**: No platform-specific settings storage logic

## 8. Related Specifications / Further Reading

- docs/migration_guide.md
- Avalonia Dialogs docs
