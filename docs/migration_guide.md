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