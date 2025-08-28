---
goal: Migrate EditMacroItemWindow to Avalonia MVVM
version: 1.0
date_created: 2025-08-28
last_updated: 2025-08-28
owner: UI Team
status: 'Planned'
tags: [feature, migration, ui]
---

# Introduction

![Status: Planned](https://img.shields.io/badge/status-Planned-blue)

Migrate `OpenCNCPilot/EditMacroItemWindow.xaml(.cs)` to Avalonia with reactive form.

## 1. Requirements & Constraints

- **REQ-001**: Create/Edit macro fields; OK/Cancel.

## 2. Implementation Steps

| ID | Task | Path | Completion Criteria |
|----|------|------|---------------------|
| TASK-001 | ViewModel | `src/OpenCNCPilot.UI/ViewModels/EditMacroItemWindowViewModel.cs` | Reactive properties and commands |
| TASK-002 | View | `src/OpenCNCPilot.UI/Views/EditMacroItemWindow.axaml` | Form binds and validates |

## 5. Files

- **FILE-001**: `src/OpenCNCPilot.UI/ViewModels/EditMacroItemWindowViewModel.cs`
- **FILE-002**: `src/OpenCNCPilot.UI/Views/EditMacroItemWindow.axaml`
