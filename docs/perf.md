# Benchmarks de Render y Construcción de Geometría

Objetivo: 10k movimientos < 1s en Linux (parse + build + flatten [+ simplify]).

## Plan
- Dataset sintético: generar líneas y arcos (10k y 100k movimientos).
- Pipeline: `GCodeParser.Parse` → `GCodePathBuilder.Build` → `SegmentFlattener.Flatten` → `PolylineSimplifier.Simplify` (opcional).
- Medir tiempos parciales y totales; repetir 5x y promediar.

## Comandos sugeridos (local)
```bash
# Ejecutar sólo benchmarks etiquetados
dotnet test ./OpenCNCPilot.Modern.sln -c Release --no-build --filter "Category=Performance"
```

## Resultados (Linux, Release, .NET 8)
- 10k:
  - Parse: 140 ms
  - Build: 9 ms
  - Flatten: 19 ms
  - Simplify: 4 ms
  - Total aprox.: 172 ms
- 100k:
  - Parse: 797 ms
  - Build: 22 ms
  - Flatten: 6 ms
  - Simplify: 2 ms
  - Total aprox.: 827 ms

Notas de entorno:
- SO: Linux (Ubuntu-like), CPU x64
- Configuración: Release
- Comando: `dotnet test -c Release --filter "Category=Performance"`

## Notas
- Validar en Ubuntu 22.04+ y Windows Server 2022 runners.
- Si GL no está disponible (CI), estas mediciones no dependen de render.
 - En CI se recomienda excluir `Category=Performance` para evitar flakiness.
