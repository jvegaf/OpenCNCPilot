---
name: "UI Migration – Dialog: <DialogName>"
about: Migrate a WPF dialog/window to Avalonia MVVM
title: "ui: migrate <DialogName> to Avalonia"
labels: ["ui", "migration", "dialog"]
assignees: []
---

## Summary
Migrate WPF dialog `<DialogName>` to Avalonia 11 using MVVM + ReactiveUI.

## Scope
- Source: `OpenCNCPilot/<DialogName>.xaml(.cs)`
- Target:
  - `src/OpenCNCPilot.UI/Views/<DialogName>.axaml`
  - `src/OpenCNCPilot.UI/ViewModels/<DialogName>ViewModel.cs`

## Tasks
- [ ] Create `ViewModel` with properties/commands replacing code-behind events
- [ ] Convert WPF XAML to Avalonia XAML (`.axaml`), map controls/bindings
- [ ] Replace `Click` handlers with `ReactiveCommand` bindings
- [ ] Wire DI in `App.axaml.cs` for the ViewModel
- [ ] Replace Windows-only APIs (e.g., `Microsoft.Win32` dialogs) with Avalonia dialogs
- [ ] Test on Windows and Linux (basic smoke test)

## Acceptance Criteria
- [ ] Builds and runs on Windows and Linux
- [ ] No WPF or `System.Windows.*` references in new code
- [ ] UI behavior parity with original dialog
