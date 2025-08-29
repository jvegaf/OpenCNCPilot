# Previsualización de G-code en Avalonia (Plan)

## Overview
Implementar la previsualización de G-code en la app moderna (.NET 8 + Avalonia 11) estableciendo el pipeline completo GCode → Geometría → Render (Skia/OpenGL), con interacciones (zoom/pan/fit), rendimiento para archivos grandes y arquitectura limpia (MVVM, DI).

## Requirements
- Funcionales
  - Al abrir `.gcode`/`.nc`, renderizar vista centrada y escalada (fit-to-view).
  - Colores por tipo: `G0` (rápidos) vs `G1/G2/G3` (corte). Ejes y grid opcionales.
  - Soportar unidades (`G20`/`G21`) y modos (G90/G91); arcos `IJK` y `R`.
  - Interacciones: zoom con rueda (alrededor del cursor), pan por arrastre, comando `Fit`.
  - Errores visibles sin bloquear; carga cancelable.
- Rendimiento
  - Parseo + construcción 10k líneas < 1s en Linux; render fluido hasta 200k movimientos.
  - Caché de `SKPath`, flatten de arcos con tolerancia, throttling de renders.
- Calidad y arquitectura
  - MVVM estricto, DI (`Microsoft.Extensions.DependencyInjection`), sin APIs Windows-only.
  - Tests Core ≥ 70% cobertura; CI verde (Linux/Windows).
- EARS (extracto)
  - WHEN se abre un archivo válido, THE SYSTEM SHALL parsearlo en segundo plano y actualizar el viewer con fit-to-view.
  - IF se detecta `G20`, THEN THE SYSTEM SHALL convertir a mm para render.
  - IF hay errores, THEN THE SYSTEM SHALL notificarlos sin bloquear y permitir reintentar.
  - WHILE se construyen trayectorias extensas, THE SYSTEM SHALL mantener la UI responsiva.

## Implementation Steps
1) Core: geometría y builder
- Definir contratos:
  - `MovementType { Rapid, Cut }`
  - `LineSegment`, `ArcSegment` (mm, inmutables)
  - `Bounds3` (Expand/Pad), `GeometryData` (segmentos, bounds, estadísticas)
- `GCodePathBuilder`
  - Input: `GCodeProgram` (parser existente)
  - Soporta G0/G1/G2/G3; G90/G91; G20/G21; arcos `IJK` y `R` (convención GRBL)
  - Output: `GeometryData` en mm + clasificación Rapid/Cut
- `SegmentFlattener`
  - Arcos → polilíneas con tolerancia (por defecto 0.05 mm)
  - API para obtener polilíneas agrupadas por tipo

2) UI: control de visualización (Skia/OpenGL)
- `GCodeViewer` derivado de `OpenGlControlBase`
- `OnOpenGlRender` con `GRContext` → `SKSurface`
- Transformaciones world→view: `scale`, `pan`, flip Y; `FitToView(bounds, margin)`
- Dibujo:
  - Ejes y grid opcionales
  - `SKPath` por tipo (Rapid/Cut) cacheado desde `GeometryData`
  - `SKPaint` antialias, grosor y colores configurables
- Interacciones:
  - Rueda: zoom centrado en cursor
  - Arrastre: pan
  - Tecla/botón `Fit`: ajuste a bounds
- Throttling: coalescer invalidaciones (máx. 60 FPS)

3) ViewModels y wiring
- `FileViewModel`
  - `IsLoading`, `Progress`, `Errors`, `Program?`, `GeometryData?`
  - `LoadGCodeCommand` (async): abrir → leer → `ParseAsync` → `PathBuilder.Build` → set `GeometryData`
  - Cancelación con `CancellationTokenSource`
- Integración en `MainView`/`WorkspaceView`
  - Insertar `GCodeViewer` con bindings a `GeometryData` y `FitToView`
  - Menú/toolbar: `Abrir…`, `Fit`, toggles de grid/ejes
- DI: registrar `IGCodePathBuilder`, `IDialogService`, `ILogger`, `ISettingsService`

4) Rendimiento y estabilidad
- Reconstruir `SKPath` sólo si cambia `GeometryData` o tolerancia
- Simplificación opcional de polilíneas (Douglas-Peucker) para archivos muy grandes
- Trabajo pesado fuera del hilo UI; `ConfigureAwait(false)`

5) UX y settings
- Barra de estado: líneas, movimientos, tiempo de carga
- Overlay: dimensiones (W×H×Z), unidades detectadas
- Settings JSON: tolerancia, colores, grosor, grid/ejes

6) CI y headless
- Tests Core/transformaciones sin GL
- Smoke gráfico (opcional) con Skia CPU offscreen o marcado `[Trait("Category","UI-GL")]`
- Sin cambios de runners a menos que se habilite smoke GL

## Testing
- Unit (Core)
  - Parser: G0/G1 abs/rel; G2/G3 IJK y R (CW/CCW, casos límite); `G20/G21`; comentarios y vacíos
  - PathBuilder: bounds correctos (incluye arcos completos); Rapid vs Cut; estadísticas
  - Flattener: error máximo ≤ tolerancia; conteo de segmentos razonable
- Unit (UI-agnóstico)
  - `FitToView`: escala/pan correctos en distintos aspect ratios
  - Zoom alrededor del cursor: punto anclado (±1 px)
- Integración
  - Pipeline 10k líneas → Parse+Build < 1s; `GeometryData` válida; bounds > 0
  - Casos borde: vacío, sólo G0, con errores: no crashea y muestra mensaje
- Performance
  - 100k movimientos: medir tiempos y memoria; documentar resultados
- Manual
  - 3 muestras (líneas, arcos mixtos, grande): validar zoom/pan/fit, colores, grid/ejes

## Criterios de aceptación
- Previsualización aparece tras abrir archivo válido con fit-to-view automático; 10k líneas en < 1s
- Colores diferenciados `G0` vs `G1/G2/G3`; ejes y grid conmutables
- Arcos y unidades correctos; bounds precisos
- UI responsiva durante carga; cancelación operativa; errores visibles
- Cobertura Core ≥ 70%; CI verde; sin APIs Windows-only
