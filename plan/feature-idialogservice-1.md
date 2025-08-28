---
goal: Cross-platform IDialogService for Avalonia
version: 1.0
date_created: 2025-08-28
last_updated: 2025-08-28
owner: UI Team
status: 'Planned'
tags: [feature, ui, migration]
---

# Introduction

![Status: Planned](https://img.shields.io/badge/status-Planned-blue)

Create an `IDialogService` abstraction and its Avalonia implementation to replace Windows-specific dialogs, enabling consistent cross-platform file pickers and alerts.

## 1. Requirements & Constraints

- **REQ-001**: Provide `OpenFile`, `SaveFile`, `OpenFolder`, `Alert`, `Confirm` methods.
- **REQ-002**: Asynchronous API (`Task`-based) suitable for Avalonia UI thread.
- **REQ-003**: No `Microsoft.Win32` or `System.Windows.Forms` usage.
- **CON-001**: .NET 8; Avalonia 11.
- **GUD-001**: Injectable via `ServiceCollection`; easily mockable for tests.

## 2. Implementation Steps

| ID | Task | Path | Completion Criteria |
|----|------|------|---------------------|
| TASK-001 | Define interface `IDialogService` | `src/OpenCNCPilot.UI/Services/IDialogService.cs` | Interface compiles with required methods |
| TASK-002 | Implement `AvaloniaDialogService` | `src/OpenCNCPilot.UI/Services/AvaloniaDialogService.cs` | Uses `TopLevel`/`Window` APIs; returns expected results |
| TASK-003 | Wire DI registration | `src/OpenCNCPilot.UI/App.axaml.cs` | `services.AddSingleton<IDialogService, AvaloniaDialogService>()` present |
| TASK-004 | Add unit-testable facade | `tests/OpenCNCPilot.UI.Tests/` | Mocks demonstrate call paths |

## 3. Alternatives

- **ALT-001**: Inline usage of `StorageProvider` across VMs — rejected (duplication).
- **ALT-002**: Third-party dialog library — not necessary.

## 4. Dependencies

- **DEP-001**: Avalonia `StorageProvider` APIs; `TopLevel`.

## 5. Files

- **FILE-001**: `src/OpenCNCPilot.UI/Services/IDialogService.cs`
- **FILE-002**: `src/OpenCNCPilot.UI/Services/AvaloniaDialogService.cs`

## 6. Testing

- **TEST-001**: Mock `IDialogService` in VM tests; verify command flows.
- **TEST-002**: Integration smoke test opening a dialog in a test window (optional/headless-safe).

## 7. Risks & Assumptions

- **RISK-001**: Headless CI storage provider limitations — mitigate by mocking in tests.
- **ASSUMPTION-001**: Top-level window available when invoking dialogs.

## 8. Related Specifications / Further Reading

- Avalonia StorageProvider docs
- docs/migration_guide.md
