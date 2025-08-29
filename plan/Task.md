# Plan de Proyecto — Pestaña “File” (Avalonia)

Referencia principal: `plan/feature-file-tab-implementation-1.md`

Este documento sigue el prompt “GitHub Issue Planning & Project Automation” para convertir los requisitos de la pestaña “File” en un plan ejecutable con jerarquía Epic → Feature → Stories/Enablers → Tasks/Tests, dependencias, prioridades y estimaciones.

---

## 1) Project Overview

- Feature Summary: Migración y finalización de la pestaña “File” a Avalonia (MVVM + ReactiveUI), incluyendo apertura/guardado/limpieza de G‑Code, listado y selección de líneas, envío secuencial a GRBL con pausa, “Go to line”, estado (`FilePosition/FileLength`) y tiempos (`Runtime/EstimatedDuration`), “Pause on M0/M1/M30”, e integración con `GCodeViewport`.
- Business Value: Habilita el flujo principal de trabajo del usuario (cargar archivo, visualizar, enviar al CNC de forma segura, pausar y reanudar), imprescindible para una versión funcional cross‑platform.
- Success Criteria (KPIs):
  - Compila en .NET 8 (Windows/Linux) y la UI carga la pestaña “File”.
  - Puede abrir/guardar/limpiar G‑Code; lista y selecciona la línea actual.
  - Envío secuencial con `ok`/`error:` y pausa en `M0/M1/M30` (opcional).
  - Muestra `FilePosition/FileLength` y `Runtime/EstimatedDuration`.
  - Se deshabilitan acciones incompatibles durante envío.
  - Tests unitarios del sender y del VM cubren escenarios clave.
- Key Milestones:
  1. Enabler: `IGCodeSender` implementado y testeado (MVP una línea en vuelo).
  2. `FileViewModel` + comandos y bindings básicos.
  3. `FilePanel.axaml` con controles, bindings y virtualización de lista.
  4. Integración con `MainWindowViewModel` y `GCodeViewport` (autofit).
  5. Persistencia de settings (`LastGCodeDirectory`, `PauseFileOnHold`).
  6. Manejo de errores/UX, estados y deshabilitación condicional.
  7. Suite de tests y documentación actualizada.
- Risk Assessment:
  - I/O serial en Linux (permisos/grupo `dialout`) — Mitigar con documentación/scripts (ya previstos en repo).
  - Parser/estimador duración: MVP con heurística simple; refinar en siguiente iteración.
  - Rendimiento lista de líneas: usar virtualización en `ListBox` y colecciones observables.
  - Diferencias WPF→Avalonia (bindings, comandos, propiedades estiladas) — Mitigar con guías de migración.

---

## 2) Work Item Hierarchy

```mermaid
graph TD
    A[Epic: Migración UI a Avalonia] --> B[Feature: Pestaña File en Avalonia]

    B --> E1[Enabler 1: Servicio IGCodeSender]
    B --> E2[Enabler 2: Persistencia de Settings]

    B --> S1[Story 1: Cargar/Guardar/Limpiar G‑Code]
    B --> S2[Story 2: Envío Start/Pause con GRBL]
    B --> S3[Story 3: Go To y Pause on M0/M1/M30]
    B --> S4[Story 4: UI FilePanel + VM + Estado]
    B --> S5[Story 5: Integración Viewport y Autofit]

    S1 --> T1A[Tarea: Open/Save dialogs + parser]
    S1 --> T1B[Tarea: Warnings y Commands para viewport]
    S1 --> T1C[Tarea: Clear y reset estado]

    S2 --> T2A[Tarea: Secuencia línea→ok→siguiente]
    S2 --> T2B[Tarea: Manejo ok/error: y pausa en error]
    S2 --> T2C[Tarea: Estimación de duración MVP]

    S3 --> T3A[Tarea: Prompt número + validación rango]
    S3 --> T3B[Tarea: Pausa tras M0/M1/M30]

    S4 --> T4A[Tarea: ReactiveCommands y CanExecute]
    S4 --> T4B[Tarea: ListBox con virtualización, SelectedIndex]
    S4 --> T4C[Tarea: Deshabilitar acciones durante envío]

    S5 --> T5A[Tarea: Commands→GCodeViewport]
    S5 --> T5B[Tarea: FitRequestId para autofit]

    B --> Q1[Test Suite: Unit/Integration/UI-VM]
```

---

## 3) GitHub Issues Breakdown (plantillas cumplimentadas)

### Epic

Título sugerido: Epic: Migración UI a Avalonia — Pestaña “File”

- Epic Description: Entregar la pestaña “File” funcional en Avalonia, base para flujo de trabajo CNC.
- Business Value: Núcleo de la aplicación para operar con archivos y control de máquina.
- Epic Acceptance Criteria:
  - [ ] Feature “Pestaña File en Avalonia” entregada con criterios de aceptación cumplidos.
  - [ ] Tests relevantes implementados y verdes en CI.
  - [ ] Documentación de migración y contrato de `IGCodeSender` actualizada.
- Definition of Done:
  - [ ] Compila y pasa tests en matriz (Ubuntu, Windows).
  - [ ] Cobertura mínima establecida en Core y sender (>70% en Core).
  - [ ] Docs actualizadas.

### Feature

Título sugerido: Feature: Pestaña “File” (Avalonia)

- Feature Acceptance Criteria (trazados desde Requirements del plan):
  - [ ] Open: diálogo, leer/parsear, warnings, `Commands` y `GCodeLines`, `CurrentFileName` y `LastGCodeDirectory`.
  - [ ] Save: diálogo y guardado en disco (MVP con líneas cargadas).
  - [ ] Clear: limpia archivo/lista/estado, refresca viewport, `HeightMapApplied=false`.
  - [ ] Start: envío secuencial con avance `FilePosition` y actualización `Runtime`.
  - [ ] Pause: pausa manteniendo posición.
  - [ ] Go To: solicitar número, validar y reposicionar si no se envía.
  - [ ] Pause on Hold: pausa tras `M0/M1/M30` cuando habilitado.
  - [ ] Respuestas GRBL: `ok` avanza, `error:` pausa y notifica (MVP).
  - [ ] Deshabilitar `Open/Save/Clear/Goto` durante envío.
  - [ ] Mostrar `FilePosition/FileLength` y `Runtime/EstimatedDuration` y seleccionar línea actual.
  - [ ] Envío encapsulado en `IGCodeSender` con estados observables.
  - [ ] Settings: recordar `LastGCodeDirectory` y `PauseFileOnHold` (cross‑platform).
- Definition of Done:
  - [ ] Historias y enablers completados, tests verdes, UX validada.

### Enabler 1: IGCodeSender

- Requisitos Técnicos:
  - [ ] Interfaz (`State`, `FilePosition`, `FileLength`, `Runtime`, `EstimatedDuration`, `PauseOnHold`, `IsSending`, `CurrentLineText`).
  - [ ] Métodos (`Load`, `Start`, `Pause`, `Clear`, `Goto`, `Dispose`).
  - [ ] Eventos/Observables (`StateChanged`, `PositionChanged`, `ErrorOccurred`).
  - [ ] Protocolo envío: una línea en vuelo; usa `ISerialPortService.WriteLine` y consume `DataReceived` (`ok`/`error:`).
  - [ ] `PauseOnHold` en `M0/M1/M30` al finalizar línea (tras `ok`).
  - [ ] Validaciones: `Goto` prohibido en `Sending`; rango; `Start` sin líneas o sin puerto abierto.
  - [ ] Estimación duración MVP.
- Tareas:
  - [ ] Definir interfaz en `src/OpenCNCPilot.Hardware/Services/`.
  - [ ] Implementación concreta con suscripción al puerto.
  - [ ] Tests unitarios (comportamientos y errores).

### Enabler 2: Persistencia de Settings

- Requisitos Técnicos:
  - [ ] Añadir `LastGCodeDirectory: string`, `PauseFileOnHold: bool` en `AppSettings`.
  - [ ] `JsonSettingsService` guarda/carga ambos campos.
- Tareas:
  - [ ] Actualizar modelo y servicio.
  - [ ] Tests de persistencia.

### Story 1: Cargar/Guardar/Limpiar G‑Code

- Acceptance Criteria:
  - [ ] `OpenCommand` abre, lee, parsea, muestra warnings, setea `Commands` y `GCodeLines`, actualiza settings y `FitRequestId`.
  - [ ] `SaveCommand` guarda líneas en ruta elegida.
  - [ ] `ClearCommand` limpia estado y viewport; `sender.Clear()`.
- Tareas:
  - [ ] Integrar `IDialogService` (open/save) y `IGCodeParser`.
  - [ ] Mapear warnings a UI (alerta).
  - [ ] Refrescar viewport y banderas.

### Story 2: Envío Start/Pause con GRBL

- Acceptance Criteria:
  - [ ] `StartCommand` inicia envío secuencial y actualiza `Runtime`.
  - [ ] `PauseCommand` pausa manteniendo posición.
  - [ ] Manejo `ok`/`error:` acorde a MVP.
- Tareas:
  - [ ] Comandos y binding con `IGCodeSender`.
  - [ ] Actualización de estado y tiempos.

### Story 3: Go To y Pause on M0/M1/M30

- Acceptance Criteria:
  - [ ] `GotoCommand` pide número, valida rango, y ejecuta `sender.Goto` si permitido.
  - [ ] `PauseOnHold` detiene tras línea con `M0/M1/M30`.
- Tareas:
  - [ ] `PromptNumberAsync` y validación.
  - [ ] Detección M‑codes en sender.

### Story 4: UI FilePanel + VM + Estado

- Acceptance Criteria:
  - [ ] Botones y `CheckBox` con `IsEnabled` según estado.
  - [ ] `ListBox` con virtualización, `SelectedIndex` sigue `FilePosition`.
  - [ ] Mostrar `FilePosition/FileLength`, `Runtime/EstimatedDuration`.
- Tareas:
  - [ ] `FilePanel.axaml` + `.axaml.cs` mínimo.
  - [ ] `FileViewModel` y bindings.

### Story 5: Integración Viewport y Autofit

- Acceptance Criteria:
  - [ ] `GCodeViewport.Commands` enlazado a `FileViewModel.Commands`.
  - [ ] `FitRequestId` dispara autofit tras abrir/limpiar.
- Tareas:
  - [ ] Wiring en `MainWindow`/contenedor.
  - [ ] Actualizar al cargar/limpiar.

### Test Suite (Q1)

- Unit (xUnit + FluentAssertions): sender y VM.
- Integration (sin hardware): fake serial que devuelve `ok` con retardo.
- UI/VM: CanExecute y SelectedIndex.
- Performance: parser 10k líneas < 1s (dataset sintético si falta).

---

## 4) Prioridad, Valor y Estimaciones

- Prioridad/Valor:
  - Feature completo: `priority-high`, `value-high` (bloquea release usable).
  - E1 IGCodeSender: `priority-high` (bloquea Stories 2–5).
  - S1 y S4: `priority-high` (flujo base y UI).
  - S2 y S3: `priority-high` (operación con GRBL).
  - S5: `priority-medium` (mejora UX/viewport pero necesaria para fluidez).
- Estimaciones (Fibonacci):
  - E1 IGCodeSender: 5 pts
  - E2 Settings: 2 pts
  - S1 Cargar/Guardar/Limpiar: 3 pts
  - S2 Start/Pause + GRBL: 3 pts
  - S3 Go To + PauseOnHold: 3 pts
  - S4 UI + Estado: 3 pts
  - S5 Viewport + Autofit: 2 pts
  - Q1 Test Suite adicional: 3 pts
  - Total estimado: 24 pts (1–2 sprints según capacidad).

---

## 5) Dependency Management

```mermaid
graph LR
    A[E1 IGCodeSender] --> B[S2 Start/Pause]
    A --> C[S3 Go To + PauseOnHold]
    D[E2 Settings] --> E[S1 Cargar/Guardar/Limpiar]
    E[S1] --> F[S4 UI + Estado]
    B --> F
    C --> F
    F --> G[S5 Viewport + Autofit]
    E & B & C --> H[Q1 Test Suite]
```

Tipos:
- Blocks: E1→S2/S3; E2→S1; S1/S2/S3→S4; S4→S5.
- Related: S1↔S5 (viewport), S2↔S3 (controles de ejecución).
- Prerequisite: Configuración de permisos serial en Linux documentada.
- Parallel: E2 y maquetación base de S4 pueden iniciar en paralelo.

---

## 6) Tareas Detalladas y Checklist de Ejecución

- Enabler 1 — IGCodeSender
  - [ ] Definir interfaz en `src/OpenCNCPilot.Hardware/Services/IGCodeSender.cs`.
  - [ ] Implementación `GCodeSender` suscrita a `ISerialPortService.DataReceived`.
  - [ ] Detección M‑codes (`M0|M1|M30`) y pausa condicional.
  - [ ] Estimador de duración MVP (por línea o heurística simple).
  - [ ] Eventos observables y thread-safety para VM.
  - [ ] Tests unitarios de estados, errores y `PauseOnHold`.
- Enabler 2 — Settings
  - [ ] Ampliar `AppSettings` con `LastGCodeDirectory`, `PauseFileOnHold`.
  - [ ] Persistencia en `JsonSettingsService` + tests.
- Story 1 — Cargar/Guardar/Limpiar
  - [ ] `OpenCommand`: diálogo, lectura, parseo, warnings.
  - [ ] Poblar `Commands` para viewport y `GCodeLines`.
  - [ ] Actualizar `CurrentFileName` y `LastGCodeDirectory`.
  - [ ] `SaveCommand`: escribir archivo (MVP).
  - [ ] `ClearCommand`: reset estado + `sender.Clear()` + `FitRequestId++`.
- Story 2 — Start/Pause con GRBL
  - [ ] `StartCommand` y `PauseCommand` con `CanExecute` según `IsSending`.
  - [ ] Avance por `ok` y pausa en `error:` (alerta y estado `Paused`).
- Story 3 — Go To y Pause on Hold
  - [ ] `GotoCommand`: `PromptNumberAsync`, validación y `sender.Goto` en `Idle/Paused`.
  - [ ] `PauseOnHold`: `CheckBox` mapea a `sender.PauseOnHold` + persistencia.
- Story 4 — UI + Estado
  - [ ] `FilePanel.axaml` con botones, checkbox, indicadores y `ListBox` virtualizada.
  - [ ] `SelectedIndex` ← `FilePosition`; mostrar tiempos y longitudes.
  - [ ] Deshabilitar `Open/Save/Clear/Goto` durante envío.
- Story 5 — Viewport + Autofit
  - [ ] Bind `Commands` a `GCodeViewport`.
  - [ ] `FitRequestId` tras `Open`/`Clear`.
- Integración y DI
  - [ ] Registrar `IGCodeSender` y `FileViewModel` en `Program.cs`.
  - [ ] Exponer `File` en `MainWindowViewModel`.
  - [ ] Eliminar duplicación previa de carga/limpieza.
- Documentación
  - [ ] Actualizar `docs/migration_guide.md` (pestaña “File”).
  - [ ] Documentar contrato/estados de `IGCodeSender`.

---

## 7) Testing Plan Resumido (derivado del plan)

- Unit (sender):
  - [ ] Start→ok avanza `FilePosition`; Idle al finalizar.
  - [ ] Pause detiene consumo de líneas.
  - [ ] `PauseOnHold=true` pausa tras línea con `M0/M1/M30`.
  - [ ] `Goto` en `Idle/Paused` OK; en `Sending` falla.
  - [ ] En `error:` → `Paused` + `ErrorOccurred`.
- Unit (FileViewModel):
  - [ ] `OpenCommand` actualiza settings, parser y colecciones.
  - [ ] `SaveCommand` escribe archivo; cancelación no altera estado.
  - [ ] `ClearCommand` limpia y llama `sender.Clear()`.
  - [ ] `GotoCommand` valida y llama `sender.Goto`.
- Integration (sin hardware):
  - [ ] `Open → Start → ok... → PauseOnHold → Pause`.
- UI/VM:
  - [ ] `CanExecute` según `IsSending`.
  - [ ] `SelectedIndex` sigue `FilePosition`.
- Performance:
  - [ ] Parser 10k líneas < 1s en Linux.

---

## 8) Definition of Done y Quality Gates

- Build: `dotnet build -c Release` sin errores en matriz OS.
- Lint/Format: `dotnet format` limpio; estilos por `.editorconfig`.
- Tests: 100% de pruebas del scope pasan; cobertura Core ≥ 70% (objetivo).
- Smoke test: abrir archivo grande, enviar primeras líneas y pausar sin crash.
- Docs: guía de migración y contrato `IGCodeSender` actualizados.
- Seguridad/Runtime: no usar APIs Windows‑only en Core/Hardware; serial Linux documentado.

---

## 9) Sugerencia de Sprint Plan (ejemplo)

- Sprint N (Objetivo): Entregar MVP operativo de la pestaña “File”.
- Historias comprometidas:
  - E1 (5), E2 (2), S1 (3), S2 (3), S4 (3) → 16 pts.
- Criterios de éxito: abrir/enviar/pausar/visualizar; tests verdes.
- Siguiente Sprint: S3 (3), S5 (2), Q1 extra (3) y refino de estimador.

---

## 10) Campos/Labels sugeridos para Issues

- Labels: `epic`, `feature`, `user-story`, `enabler`, `test`, `priority-high`, `value-high`, `component-ui`, `component-hardware`, `component-core`.
- Component: `Frontend` (UI/VM), `Backend` (Hardware sender), `Testing`.
- Estimate: puntos de historia asignados arriba.

---

## 11) Notas de Automatización (opcional)

- Se recomienda un workflow manual para crear issues desde este plan usando `actions/github-script`, enlazando dependencias y aplicando labels y estimaciones. No incluido aquí por simplicidad del repositorio.

---

Fin del documento.
