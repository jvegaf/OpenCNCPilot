---
goal: Migrate WarningWindow to Avalonia MVVM
version: 1.0
date_created: 2025-08-28
last_updated: 2025-08-28
owner: UI Team
status: 'Planned'
tags: [feature, migration, ui]
---

# Introduction

![Status: Planned](https://img.shields.io/badge/status-Planned-blue)

Migrate `OpenCNCPilot/WarningWindow.xaml(.cs)` to Avalonia with confirmations.

## 1. Requirements & Constraints

- **REQ-001**: Show warning text; buttons OK/Cancel or Yes/No.

## 2. Implementation Steps

| ID | Task | Path | Completion Criteria |
|----|------|------|---------------------|
| TASK-001 | ViewModel | `src/OpenCNCPilot.UI/ViewModels/WarningWindowViewModel.cs` | Message and result |
| TASK-002 | View | `src/OpenCNCPilot.UI/Views/WarningWindow.axaml` | Binds and returns result |

## 5. Files

- **FILE-001**: `src/OpenCNCPilot.UI/ViewModels/WarningWindowViewModel.cs`
- **FILE-002**: `src/OpenCNCPilot.UI/Views/WarningWindow.axaml`
