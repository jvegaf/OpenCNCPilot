# Guía de Pruebas

Esta guía resume cómo ejecutar las pruebas de OpenCNCPilot de forma local y qué categorías existen para controlar su ejecución en CI.

## Categorías de pruebas

- `Performance` (opt‑in): Benchmarks sintéticos para medir Parse/Build/Flatten/Simplify con 10k/100k movimientos. Excluidas en CI por flakiness y variabilidad.
- `UI-GL` (opt‑out): Smokes de OpenGL (EGL en Linux, WGL en Windows) y una prueba dummy. Excluidas en CI para evitar fallos en entornos headless.
- `RequiresHardware` (opt‑out): Pruebas que dependen de hardware real (si existieran). Excluidas en CI.

## Comandos típicos

Ejecutar todo (local):
```bash
dotnet test ./OpenCNCPilot.Modern.sln -c Release
```

Como CI (excluye Performance y UI‑GL):
```bash
dotnet test ./OpenCNCPilot.Modern.sln -c Release --filter "Category!=Performance&Category!=UI-GL"
```

Sólo Performance (opt‑in):
```bash
dotnet test ./OpenCNCPilot.Modern.sln -c Release --filter "Category=Performance" -l "console;verbosity=detailed"
```

Sólo UI‑GL (smokes):
```bash
dotnet test ./OpenCNCPilot.Modern.sln -c Release --filter "Category=UI-GL"
```

Cobertura (similar a CI):
```bash
dotnet test ./OpenCNCPilot.Modern.sln -c Release \
  --collect:"XPlat Code Coverage" \
  --filter "Category!=RequiresHardware&Category!=Performance&Category!=UI-GL"
```

## Prerrequisitos (smokes UI‑GL)

- Linux: `libEGL.so.1` y `libGL.so.1` (por ejemplo, `libegl1`, `libgl1`). Si faltan, las pruebas retornan sin fallar.
- Windows: bibliotecas del sistema `user32.dll`, `gdi32.dll`, `opengl32.dll` disponibles por defecto.

## Notas

- Los smokes `UI‑GL` no renderizan UI; sólo validan que se puede crear un contexto y ejecutar `glClear` sin errores.
- Las pruebas de UI se centran en ViewModels y lógica UI‑agnóstica; no abren ventanas.
- Para detalles de performance y resultados, ver `docs/perf.md`.
