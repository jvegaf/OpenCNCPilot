---
goal: Migrate EnterNumberWindow to Avalonia MVVM
version: 1.0
date_created: 2025-08-28
last_updated: 2025-08-28
owner: UI Team
status: 'Planned'
tags: [feature, migration, ui]
---

# Introduction

![Status: Planned](https://img.shields.io/badge/status-Planned-blue)

Migrate `OpenCNCPilot/EnterNumberWindow.xaml(.cs)` to Avalonia with numeric validation.

## 1. Requirements & Constraints

- **REQ-001**: Numeric input with min/max, OK/Cancel.

## 2. Implementation Steps

| ID | Task | Path | Completion Criteria |
|----|------|------|---------------------|
| TASK-001 | ViewModel | `src/OpenCNCPilot.UI/ViewModels/EnterNumberWindowViewModel.cs` | Numeric parsing, validation, commands |
| TASK-002 | View | `src/OpenCNCPilot.UI/Views/EnterNumberWindow.axaml` | Input bound with validation |

## 5. Files

- **FILE-001**: `src/OpenCNCPilot.UI/ViewModels/EnterNumberWindowViewModel.cs`
- **FILE-002**: `src/OpenCNCPilot.UI/Views/EnterNumberWindow.axaml`
