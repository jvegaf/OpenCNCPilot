# Copilot Instructions para OpenCNCPilot (Migración a Avalonia + CI/CD)

Estos lineamientos guían a GitHub Copilot para generar código, tests, automatizaciones y documentación consistentes con los objetivos del proyecto: migración a Avalonia (cross‑platform), cobertura de testing, y CI/CD con GitHub Actions (builds y releases).

IMPORTANTE: Sigue los principios de Context Engineering descritos aquí. Antes de implementar, investiga y valida.


## 1) Objetivo del Proyecto

- Migrar la app actual a una solución cross‑platform con Avalonia UI (Windows y Linux; opcionalmente macOS).
- Establecer arquitectura modular .NET 8 con separación clara entre UI, Core (dominio) y Hardware.
- Integrar testing (unitario, integración y performance) y medición de cobertura.
- Implementar CI/CD con GitHub Actions: build/test en matriz de SO; releases versionadas con artefactos por plataforma.


## 2) Principios Centrales de Context Engineering (obligatorios)

- Búsqueda web primero: consulta documentación oficial de Avalonia, .NET 8, System.IO.Ports, Skia/OpenGL, GitHub Actions.
- Inmersión en documentación y extracción de patrones: usa MVVM + ReactiveUI en UI; DI con Microsoft.Extensions.DependencyInjection; xUnit + FluentAssertions.
- Documentación de gotchas: dependencias OpenGL en Linux, permisos de puertos seriales, diferencias WPF vs Avalonia.
- Validación continua: cada PR debe compilar en todos los SO de la matriz, pasar tests y cumplir criterios de aceptación.


## 3) Stack Técnico y Decisiones

- Lenguaje/Runtime: .NET 8 (C# 12).
- UI: Avalonia 11 (Avalonia.Desktop, Avalonia.ReactiveUI, Themes.Fluent).
- Render 2D/3D: SkiaSharp sobre OpenGL (OpenGlControlBase). Evitar WPF 3D/Viewport3D.
- Serial: System.IO.Ports (con abstracción ISerialPortService). Considerar SerialPortStream si hay issues en Linux.
- Configuración: JSON (System.Text.Json) en directorio apropiado por SO.
- Tests: xUnit + FluentAssertions; categorías para pruebas que requieren hardware.
- CI/CD: GitHub Actions con matriz [ubuntu-latest, windows-latest, macos-latest (opcional)], publish self-contained y single-file.
- DI y Modularidad: Microsoft.Extensions.DependencyInjection; separar Core/UI/Hardware.
- Registro: ILogger (Microsoft.Extensions.Logging) con sinks simples (consola/archivo).
- Estilo: .editorconfig + dotnet format; convenciones de nombres PascalCase para tipos, camelCase para miembros.


## 4) Estructura de Solución (propuesta)

- src/
  - OpenCNCPilot.Core/           Lógica de dominio (parser G-code, modelos, servicios puros)
  - OpenCNCPilot.Hardware/       Abstracciones + implementación de puertos seriales
  - OpenCNCPilot.UI/             Avalonia + MVVM (Views, ViewModels, Services UI)
- tests/
  - OpenCNCPilot.Core.Tests/
  - OpenCNCPilot.Integration.Tests/
  - OpenCNCPilot.UI.Tests/       Pruebas de VM y servicios UI (sin UI real)
- scripts/
  - setup-linux-permissions.sh   Permisos serial y reglas udev
- .github/workflows/
  - ci.yml                       Build + test + coverage
  - release.yml                  Publish + empaquetado + subida a GitHub Releases
- docs/
  - MIGRATION_GUIDE.md           Guía WPF → Avalonia
  - ARCHITECTURE.md              Diagramas y decisiones


## 5) Reglas de Arquitectura y Codificación

- No crear archivos > 500 líneas. Divide en módulos.
- MVVM estricto en UI: lógica en ViewModels; Views sin lógica de negocio.
- Usar ReactiveCommand/Observables donde aplique; evitar event-handlers en code-behind.
- Abstrae dependencias OS‑specific (serial, FS, diálogos) detrás de interfaces en Core/Hardware/Services.
- No usar APIs Windows-only (System.Windows.*, Registry, WMI) en código compartido.
- Manejo de errores completo: excepciones con contexto, logging y mensajes útiles.
- Paths: usar Path.Combine, normalización cross‑platform y carpetas de datos por SO.
- Serial en Linux: documentar y/o automatizar pertenencia a grupo dialout y reglas udev.
- OpenGL/Skia: inicializar y liberar recursos de forma segura; contemplar headless en CI (no crashear tests).


## 6) Tareas y Criterios de Aceptación

- Migración base a Avalonia
  - La app inicia en Windows y Linux.
  - Se lista puertos seriales correctamente por SO.
  - Diálogos de Open/Save funcionan en ambos SO.
  - Viewer de trayectorias renderiza placeholder sin errores (Skia/OpenGL).
- Abstracciones de Hardware
  - ISerialPortService con implementación CrossPlatformSerialPort.
  - Timeouts, RTS/DTR configurables; lectura no bloqueante.
- Configuración
  - Servicio de settings con persistencia JSON por SO.
- Testing
  - Unit: cobertura mínima 70% en Core.
  - Integration: tests seriados marcados y omitidos en CI si no hay hardware.
  - Performance: parsing de 10k líneas < 1s en agente Linux.
- CI
  - Matriz de OS compila y ejecuta tests.
  - Coverage report subido (Codecov u otro) en Linux.
- Release
  - Al tag vX.Y.Z se crean artefactos self-contained single-file para cada SO definido.
  - Notas de release incluyen changelog generado por commits o PRs.


## 7) Convenciones de Branch, Commits y PRs

- Branches: feature/…, fix/…, chore/…, docs/…, refactor/…, test/…
- Commits: Conventional Commits
  - feat(ui): migrar MainWindow a Avalonia
  - fix(hardware): evitar bloqueo al cerrar puerto en Linux
- PRs:
  - Incluir PRP (Plan de Requisitos de Producto):
    - Research (enlaces), Plan (tareas), Implementación (resumen), Validación (evidencia), Documentación.
  - Criterios de aceptación y resultados de CI visibles.
  - Tamaño moderado; refactors separados de features.


## 8) Patrones que Copilot Debe Usar

- Interfaces primero: define ISerialPortService, ISettingsService, IDialogService.
- Inyección de dependencias en UI Program/App con ServiceCollection.
- MVVM con ReactiveUI: ReactiveObject, ReactiveCommand, observables.
- Render 2D/3D: controles derivados de OpenGlControlBase + SkiaSharp.
- Tests: xUnit + FluentAssertions; categorías [Trait("Category","RequiresHardware")].
- GitHub Actions: matrices, cache de NuGet, artefactos por OS, publish self-contained.

Ejemplo de archivos que Copilot puede generar:

```csharp
// src/OpenCNCPilot.Hardware/ISerialPortService.cs
public interface ISerialPortService
{
    event EventHandler<string> DataReceived;
    bool IsOpen { get; }
    string[] GetAvailablePorts();
    void Open(string portName, int baudRate);
    void Write(string data);
    void WriteLine(string data);
    void Close();
}
```

```csharp
// src/OpenCNCPilot.Core/Configuration/AppSettings.cs
public class AppSettings
{
    public string DefaultComPort { get; set; } = string.Empty;
    public int BaudRate { get; set; } = 115200;
    public string LastGCodeDirectory { get; set; } = string.Empty;
}
```


## 9) Gotchas y Casos Borde (documentar siempre)

- Linux serial:
  - Usuario debe pertenecer a grupo dialout; reglas udev para CH340/FTDI/Silabs.
  - Dispositivos comunes: /dev/ttyUSB*, /dev/ttyACM*.
- OpenGL/Skia:
  - Dependencias del sistema (libgl1-mesa-glx, libx11, etc.) en runners Linux.
  - Evitar acceso a GL en entornos headless durante tests unitarios.
- Avalonia vs WPF:
  - No hay Viewport3D; migrar a Skia/OpenGL.
  - DP vs StyledProperty; Commands vs Click.
- Paths:
  - Separadores y ubicaciones de AppData específicas por SO.
- CI:
  - Saltar tests que requieren hardware.
  - Publicación single-file puede expandir nativos; usar IncludeNativeLibrariesForSelfExtract=true.


## 10) Workflows de GitHub Actions (plantillas que Copilot debe seguir)

```yaml
# .github/workflows/ci.yml (resumen)
name: CI
on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main, develop ]
jobs:
  build-test:
    strategy:
      matrix:
        os: [ubuntu-latest, windows-latest]
    runs-on: ${{ matrix.os }}
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: 8.0.x }
      - run: dotnet restore
      - run: dotnet build -c Release --no-restore
      - run: dotnet test -c Release --no-build --logger trx
```

```yaml
# .github/workflows/release.yml (resumen)
name: Release
on:
  push:
    tags: [ 'v*' ]
jobs:
  publish:
    strategy:
      matrix:
        include:
          - os: ubuntu-latest
            rid: linux-x64
            artifact: OpenCNCPilot-linux-x64
          - os: windows-latest
            rid: win-x64
            artifact: OpenCNCPilot-win-x64
    runs-on: ${{ matrix.os }}
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: 8.0.x }
      - run: >
          dotnet publish src/OpenCNCPilot.UI/OpenCNCPilot.UI.csproj
          -c Release -r ${{ matrix.rid }} --self-contained true
          -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
          -o ./publish/${{ matrix.rid }}
      - name: Archive
        if: matrix.os == 'windows-latest'
        run: Compress-Archive -Path ./publish/${{ matrix.rid }}/* -DestinationPath ${{ matrix.artifact }}.zip
      - name: Archive (tar.gz)
        if: matrix.os != 'windows-latest'
        run: tar -czf ${{ matrix.artifact }}.tar.gz -C ./publish/${{ matrix.rid }} .
      - name: Upload Release Asset
        uses: softprops/action-gh-release@v2
        with:
          files: |
            ${{ matrix.artifact }}.zip
            ${{ matrix.artifact }}.tar.gz
```


## 11) Prompts de Alta Calidad para usar con Copilot

- Generar ViewModel Avalonia con ReactiveUI:
  - “Crea un ViewModel ‘MachineConnectionViewModel’ con propiedades PortNames (observable), SelectedPort, BaudRate y comandos Connect/Disconnect usando ReactiveCommand, validando estados y exponiendo IsConnected.”

- Control OpenGL + Skia para viewer:
  - “Implementa un control derivado de OpenGlControlBase que cree un SKSurface y renderice ejes XYZ y trayectorias primitivas. Expón propiedades Zoom, RotationX/RotationY y métodos LoadPaths(List<Line3D>).”

- Servicio de puertos seriales:
  - “Crea ISerialPortService y una implementación CrossPlatformSerialPort usando System.IO.Ports; soporta Linux (DTR/RTS on), lectura asíncrona con DataReceived y método GetAvailablePorts que busque /dev/ttyUSB*, /dev/ttyACM* en Linux.”

- Tests de parser:
  - “Escribe pruebas xUnit para GCodeParser que validen parseo de G0/G1 con tolerancia decimal y un test de performance con 10k líneas <1s.”

- Workflows:
  - “Crea ci.yml que compile y ejecute tests en ubuntu/windows, genere cobertura en ubuntu, y suba artifacts de resultados de test.”


## 12) Validación y Checklist por PR

- [ ] Compila en .NET 8 en Windows y Linux.
- [ ] Tests unitarios pasan; cobertura >= 70% en Core.
- [ ] No hay APIs Windows-only en Core/Hardware.
- [ ] UI sigue MVVM; nada de lógica de negocio en Views.
- [ ] Paths y diálogos son cross‑platform.
- [ ] Logs y manejo de errores adecuados.
- [ ] Workflows actualizados y verdes.
- [ ] Documentación (README/Docs) actualizada con cambios.


## 13) Enlaces de Referencia (investigar antes de codificar)

- Avalonia UI docs: https://docs.avaloniaui.net
- Avalonia OpenGL control: https://docs.avaloniaui.net/docs/controls/opengl
- SkiaSharp docs: https://docs.microsoft.com/dotnet/api/skiasharp
- System.IO.Ports: https://learn.microsoft.com/dotnet/api/system.io.ports.serialport
- GitHub Actions .NET: https://github.com/actions/setup-dotnet
- Publicación self-contained: https://learn.microsoft.com/dotnet/core/deploying/


— Fin de instrucciones —