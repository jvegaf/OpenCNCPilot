---
goal: Migrate GrblSettingsWindow to Avalonia MVVM
version: 1.0
date_created: 2025-08-28
last_updated: 2025-08-28
owner: UI Team
status: 'Planned'
tags: [feature, migration, ui]
---

# Introduction

![Status: Planned](https://img.shields.io/badge/status-Planned-blue)

Migrate `OpenCNCPilot/GrblSettingsWindow.xaml(.cs)` to Avalonia with import/export using `IDialogService`.

## 1. Requirements & Constraints

- **REQ-001**: Display GRBL settings, allow import/export without `Microsoft.Win32`.

## 2. Implementation Steps

| ID | Task | Path | Completion Criteria |
|----|------|------|---------------------|
| TASK-001 | ViewModel | `src/OpenCNCPilot.UI/ViewModels/GrblSettingsWindowViewModel.cs` | Properties + Import/Export commands |
| TASK-002 | View | `src/OpenCNCPilot.UI/Views/GrblSettingsWindow.axaml` | Binds to VM |
| TASK-003 | Dialog IO | use `IDialogService` | Open/Save works on Windows/Linux |

## 3. Dependencies

- **DEP-001**: `IDialogService` implemented.

## 4. Files

- **FILE-001**: `src/OpenCNCPilot.UI/ViewModels/GrblSettingsWindowViewModel.cs`
- **FILE-002**: `src/OpenCNCPilot.UI/Views/GrblSettingsWindow.axaml`
