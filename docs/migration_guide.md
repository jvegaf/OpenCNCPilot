# Guía de Migración WPF → Avalonia

## Cambios Principales

### 1. Namespaces
```csharp
// WPF
using System.Windows;
using System.Windows.Controls;

// Avalonia
using Avalonia;
using Avalonia.Controls;
```

### 2. XAML
- Cambiar `Window` xmlns de WPF a Avalonia
- `x:Name` → puede mantenerse igual
- `Click` → `Command` con MVVM
- `Visibility` → `IsVisible`

### 3. Bindings
```xml
<!-- WPF -->
<TextBox Text="{Binding Path=Name, UpdateSourceTrigger=PropertyChanged}" />

<!-- Avalonia -->
<TextBox Text="{Binding Name}" />
```

### 4. Estilos y Recursos
```xml
<!-- Avalonia Style -->
<Style Selector="Button.primary">
    <Setter Property="Background" Value="#007ACC"/>
    <Setter Property="Foreground" Value="White"/>
</Style>
```

### 5. Controles Custom
- Heredar de `UserControl` o `TemplatedControl`
- Usar `StyledProperty` en lugar de `DependencyProperty`

### 6. MVVM + ReactiveUI
- Usar `ReactiveObject` para ViewModels y `RaiseAndSetIfChanged`.
- Comandos con `ReactiveCommand` en vez de event-handlers (`Click`).
- `Interaction<TIn, TOut>` para diálogos sin acoplar la vista.

### 7. Inyección de Dependencias (DI)
- Configurar `Microsoft.Extensions.DependencyInjection` en `App.axaml.cs`.
- Registrar servicios de UI (`IDialogService`, `ISettingsService`), hardware (`ISerialPortService`) y Core (`IGCodeParser`).
- Resolver ViewModels desde el contenedor.

### 8. Separación de Capas
- Core (`OpenCNCPilot.Core`): parser de G‑Code, modelos y lógica pura (sin Avalonia ni OS-specific).
- Hardware (`OpenCNCPilot.Hardware`): abstracciones + implementación de puertos seriales.
- UI (`OpenCNCPilot.UI`): Views, ViewModels y servicios de UI.

## Mapeo de Controles

| WPF | Avalonia | Notas |
|-----|----------|-------|
| `Window` | `Window` | Similar |
| `UserControl` | `UserControl` | Similar |
| `Grid` | `Grid` | Similar |
| `StackPanel` | `StackPanel` | Similar |
| `Border` | `Border` | Similar |
| `TextBox` | `TextBox` | Similar |
| `Button` | `Button` | Similar |
| `ComboBox` | `ComboBox` | Similar |
| `ListBox` | `ListBox` | Similar |
| `MenuItem` | `MenuItem` | Similar |
| `ToolBar` | `ToolBar` | Requiere NuGet adicional |
| `StatusBar` | Panel con estilo | No hay control directo |
| `Viewport3D` | OpenGL/SkiaSharp | Requiere implementación custom |

## Viewer (OpenGL + Skia)
- En Avalonia no existe `Viewport3D`; se usa `OpenGlControlBase` y se renderiza con Skia (`GRGlInterface` + `GRContext`).
- Control `GCodeViewport`:
  - `StyledProperty` de `Zoom`, `RotationX`, `RotationY`, `Commands` y `FitRequestId`.
  - Renderiza ejes y trayectorias (Line/Arc) en proyección XY.
  - `AutoFit()` al cambiar `Commands` y cuando `FitRequestId` aumenta.
- Enlazar en `MainWindow.axaml`:
  - `Commands="{Binding GCodeCommands}"`, `Zoom="{Binding ViewerZoom}"`, `FitRequestId="{Binding FitRequestId}"`.
  - Toolbar flotante con comandos `FitToView`, `ZoomIn`, `ZoomOut`, `ZoomReset`.

## Servicios de UI y Settings
- `IDialogService`: open/save, alert/confirm, warnings, prompt numérico.
- `ISettingsService`: persistencia JSON (System.Text.Json) con `AppSettings` (incluye `LastGCodeDirectory`, `IgnoreAdditionalAxes`).
- Los ViewModels no deben usar APIs de plataforma; delegar a servicios.

## Parser de G‑Code (Core)
- `IGCodeParser`/`GCodeParser` en `OpenCNCPilot.Core.GCode`:
  - Soporta G0/G1/G2/G3, units G20/G21, distancia G90/G91, planos G17/G18/G19, arcos IJK/R.
  - Emite `Warnings` y lanza `ParseException` ante errores.
  - Opción `IgnoreAdditionalAxes` para A/B/C.
- `MainWindowViewModel.LoadGCodeFileCommand` invoca el parser, muestra warnings y expone `GCodeCommands`.

## Gotchas (Linux/CI/OpenGL)
- Linux: el usuario debe pertenecer al grupo `dialout` para acceder a `/dev/ttyUSB*`/`/dev/ttyACM*`.
- OpenGL/Skia en CI/headless: puede no existir contexto GL; envolver init/render en try/catch para no crashear.
- Paquetes del sistema: puede requerir `libgl1`, `libx11-6` u otros para render.
- Tests de UI: evitar acceso a GL; probar ViewModels y bindings.

## Migración de la pestaña “File” (WPF → Avalonia)

- Patrón MVVM con ReactiveUI:
  - ViewModel: `FileViewModel` hereda de `ReactiveObject`, comandos con `ReactiveCommand` (`Open/Save/Clear/Start/Pause/Goto`).
  - Estados derivados (habilitación/deshabilitación) a partir de `IGCodeSender.IsSending` y `FileLength`.
  - Persistencia con `ISettingsService` (JSON) para `LastGCodeDirectory` y `PauseFileOnHold`.

- Vista (`FilePanel.axaml`):
  - `ListBox ItemsSource="{Binding GCodeLines}" SelectedIndex="{Binding FilePosition}"`.
  - Evitar propiedades WPF no soportadas en Avalonia como `IsVirtualizing`, `VirtualizationMode` o `Items` (usar `ItemsSource`). Avalonia 11 ya virtualiza por defecto con su panel de items.
  - Botones enlazados a `ReactiveCommand` y `CheckBox` a `PauseOnHold`.

- Integración con `MainWindowViewModel`:
  - `MainWindowViewModel` expone `FileTab: FileViewModel` y utiliza `this.WhenAnyValue(x => x.FileTab.ParsedCommands)` para propagar a `GCodeViewport` y disparar `FitRequestId++` tras abrir/limpiar.
  - Evitar suscripciones a propiedades constantes en `WhenAnyValue` (causa `Unsupported expression of type 'Constant'`).

- Diálogos (`IDialogService`):
  - Firmas relevantes en Avalonia:
    - `Task<string[]?> OpenFilesAsync(string title, string? initialDirectory = null, string[]? filters = null, bool allowMultiple = false)`
    - `Task<string?> SaveFileAsync(string title, string? initialDirectory = null, string? defaultFileName = null, string[]? filters = null)`
    - `Task<string?> PickFolderAsync(string title, string? initialDirectory = null)`
    - `Task AlertAsync(string title, string message, string okText = "OK")`
    - `Task<bool> ConfirmAsync(string title, string message, string confirmText = "OK", string cancelText = "Cancel")`
    - `Task<double?> PromptNumberAsync(string title, string message, double? defaultValue = null, double? min = null, double? max = null, int decimals = 3)`
  - En tests, los dummies deben implementar exactamente estas firmas para compilar.

- Sender de G‑Code (`IGCodeSender`):
  - Protocolo una‑línea‑en‑vuelo, eventos `StateChanged`, `PositionChanged`, `ErrorOccurred`.
  - `PauseOnHold` tras `ok` si la línea contenía `M0/M1/M30`.
  - `Goto` permitido sólo en `Idle/Paused`.

- Troubleshooting XAML:
  - Si aparecen errores de propiedades WPF en `ListBox`, limpiar artefactos (`bin/obj`) y verificar que no se usan props no soportadas.
  - Mensajes típicos: "Unable to resolve property IsVirtualizing / VirtualizationMode" o "setter/adder for property Items"; solución: usar `ItemsSource` y confiar en la virtualización por defecto.

## Dependencias Platform-Specific

### System.IO.Ports
- ✅ Funciona en Linux con .NET 8+
- ⚠️ Requiere permisos (grupo dialout)
- 💡 Considerar SerialPortStream para mejor compatibilidad

### Rutas de Archivo
- Usar `Path.Combine()` siempre
- Evitar hardcodear separadores
- Usar `Environment.SpecialFolder`

### Permisos
- Linux: Usuario debe estar en grupo `dialout`
- macOS: Puede requerir permisos adicionales
- Windows: Generalmente sin problemas

## Testing Cross-Platform

### GitHub Actions
```yaml
strategy:
  matrix:
    os: [ubuntu-latest, windows-latest, macos-latest]
```

### Docker
- Útil para probar diferentes distribuciones Linux
- Permite simular entornos sin GUI

### Consideraciones de UI
- Fuentes pueden renderizar diferente
- Tamaños de controles pueden variar
- Probar temas claro/oscuro
