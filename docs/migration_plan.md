📋 Plan de Migración Cross-Platform

## Fase 1: Preparación y Análisis (1-2 semanas)
- [ ] Auditoría completa del código WPF actual
- [ ] Identificar dependencias Windows-specific
- [ ] Crear branch `feature/cross-platform-migration`
- [ ] Documentar componentes custom y controles especializados

## Fase 2: Setup del Proyecto Avalonia (1 semana)
- [ ] Crear nuevo proyecto Avalonia
- [ ] Configurar estructura de carpetas
- [ ] Migrar lógica de negocio (no-UI)
- [ ] Configurar inyección de dependencias

## Fase 3: Migración de UI (3-4 semanas)
- [ ] Migrar ventana principal
- [ ] Adaptar controles custom
- [ ] Migrar diálogos y ventanas secundarias
- [ ] Implementar visualización 3D (OpenGL)

## Fase 4: Testing y Estabilización (2 semanas)
- [ ] Pruebas en Windows
- [ ] Pruebas en Linux (Ubuntu, Debian)
- [ ] Optimización de rendimiento
- [ ] Corrección de bugs

3. Estructura de Testing Propuesta

OpenCNCPilot/
├── src/
│   ├── OpenCNCPilot.Core/           # Lógica de negocio
│   ├── OpenCNCPilot.UI/             # UI Avalonia
│   └── OpenCNCPilot.Hardware/       # Comunicación con hardware
├── tests/
│   ├── OpenCNCPilot.Core.Tests/     # Unit tests
│   ├── OpenCNCPilot.Integration.Tests/
│   └── OpenCNCPilot.UI.Tests/       # UI tests con Avalonia
└── benchmarks/
    └── OpenCNCPilot.Benchmarks/     # Performance tests

