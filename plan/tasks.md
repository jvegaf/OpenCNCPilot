# Tasks — Previsualización de G-code en Avalonia

Estado: Draft
Owner:
Fecha: 2025-08-29

## Convenciones
- Estimación T-shirt: XS (<0.5d), S (0.5–1d), M (1–2d), L (2–4d)
- Estados: TODO / WIP / DONE / BLOCKED
- Dependencias referencian IDs de esta lista

---

## 1) Core — Geometría y Builder

1.1 Definir contratos de geometría (Line/Arc/Bounds/GeometryData)
- Descripción: Crear tipos inmutables en `OpenCNCPilot.Core/Geometry` y utilitarios de bounds.
- Aceptación: Tipos compilando con tests stub; `Bounds3` cubre expand/pad; sin dependencias UI.
- Dep: —
- Estimación: S
- Estado: DONE

1.2 Implementar `GCodePathBuilder`
- Descripción: Transformar `GCodeProgram` → `GeometryData` (mm); soportar G0/G1/G2/G3; G90/G91; G20/G21; arcos IJK/R.
- Aceptación: Tests unitarios para movimientos básicos, arcos y unidades pasan; bounds correctos.
- Dep: 1.1
- Estimación: M
- Estado: DONE

1.3 Implementar `SegmentFlattener`
- Descripción: Arcos → polilíneas con tolerancia (0.05 mm por defecto); API por tipo de movimiento.
- Aceptación: Tests de error máximo y conteo de segmentos razonable.
- Dep: 1.1, 1.2
- Estimación: S
- Estado: DONE

1.4 Tests Core (parser + builder + flattener)
- Descripción: xUnit + FluentAssertions; cubrir G0/G1, G2/G3 IJK/R, G20/G21, bounds.
- Aceptación: Cobertura Core ≥ 70%; todos verdes local.
- Dep: 1.2, 1.3
- Estimación: M
- Estado: DONE

---

## 2) UI — Control de visualización

2.1 Crear `GCodeViewer` (OpenGlControlBase + Skia)
- Descripción: Renderizar paths cacheados, ejes y grid; DP: `GeometryData`, `ShowGrid`, `ColorScheme`.
- Aceptación: Control dibuja datos de ejemplo con fit-to-view.
- Dep: 1.1, 1.3
- Estimación: M
- Estado: TODO

2.2 Interacciones (zoom/pan/fit) + transformaciones
- Descripción: Zoom alrededor del cursor; pan por arrastre; tecla/botón `Fit`.
- Aceptación: Comportamiento validado manualmente; tests de transformaciones UI-agnósticas.
- Dep: 2.1
- Estimación: S
- Estado: TODO

2.3 Caché de `SKPath` y throttling
- Descripción: Construcción/reuso de `SKPath` por tipo; invalidaciones a máx. 60 FPS.
- Aceptación: No hay jank visible en archivos de 10k–50k movimientos.
- Dep: 2.1
- Estimación: S
- Estado: TODO

---

## 3) ViewModels y flujo de carga

3.1 `FileViewModel` con `LoadGCodeCommand`
- Descripción: Abrir archivo, leer, `ParseAsync`→`PathBuilder.Build`; `IsLoading`, `Progress`, `Errors`, cancelación.
- Aceptación: Abrir archivo muestra preview; errores visibles; cancelación opera.
- Dep: 1.2, 1.3
- Estimación: S
- Estado: TODO

3.2 Integración en `MainView`/`WorkspaceView`
- Descripción: Insertar `GCodeViewer`; bindings a `GeometryData`; Menú/toolbar `Abrir…`, `Fit`, toggles.
- Aceptación: Flujo end-to-end funcionando.
- Dep: 2.1, 3.1
- Estimación: S
- Estado: TODO

3.3 DI y settings
- Descripción: Registrar `IGCodePathBuilder`, `IDialogService`, `ILogger`, `ISettingsService`; añadir opciones de viewer (tolerancia, colores, grosor, grid/ejes).
- Aceptación: Preferencias se aplican y persisten.
- Dep: 3.2
- Estimación: S
- Estado: TODO

---

## 4) Rendimiento y estabilidad

4.1 Simplificación adaptativa (opcional)
- Descripción: Douglas-Peucker configurable para polilíneas grandes.
- Aceptación: Activable por setting; mejora performance en >100k movimientos.
- Dep: 1.3, 2.1
- Estimación: S
- Estado: TODO

4.2 Medición de performance
- Descripción: Pruebas/benchmarks de parse+build para 10k y 100k movimientos.
- Aceptación: 10k < 1s en Linux; resultados documentados.
- Dep: 1.4
- Estimación: S
- Estado: TODO

---

## 5) Testing y CI

5.1 Tests UI-agnósticos (transformaciones)
- Descripción: Fit-to-view, zoom-anchored; sin usar GL.
- Aceptación: Tests verdes en CI.
- Dep: 2.2
- Estimación: XS
- Estado: TODO

5.2 Ajustes CI (si smoke UI)
- Descripción: Marcar `[Trait("Category","UI-GL")]` o usar Skia CPU offscreen; evitar fallos en headless.
- Aceptación: CI verde en Linux/Windows.
- Dep: 2.x
- Estimación: XS
- Estado: TODO

---

## 6) Documentación y UX

6.1 Documentar pipeline en `docs/ARCHITECTURE.md`
- Descripción: Sección “GCode Rendering Pipeline” con diagrama.
- Aceptación: PR con diagrama y explicación.
- Dep: 3.2
- Estimación: XS
- Estado: TODO

6.2 Guía de migración WPF→Avalonia (viewer)
- Descripción: Añadir diferencias clave (OpenGL/Skia, propiedades StyledProperty).
- Aceptación: Guía actualizada.
- Dep: 2.x
- Estimación: XS
- Estado: TODO

---

## Criterios de aceptación (globales)
- Preview aparece al abrir archivo; fit-to-view automático; 10k líneas < 1s.
- Colores diferenciados por tipo; ejes y grid conmutables.
- Arcos y unidades correctos; bounds precisos.
- UI responsiva; cancelación operativa; errores visibles.
- Cobertura Core ≥ 70%; CI verde (Linux/Windows).
