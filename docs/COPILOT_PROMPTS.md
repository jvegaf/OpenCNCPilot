# Playbook de Prompts (OpenCNCPilot)

Este playbook explica cuándo y cómo usar los prompts incluidos en `.github/prompts/` para soportar nuestro flujo de trabajo (migración a Avalonia, pruebas, CI/CD y documentación).

## Índice rápido
- review-and-refactor.prompt.md: Revisiones/refactors locales.
- breakdown-plan.prompt.md: Desglosar features/planes en issues jerárquicos.
- create-github-action-workflow-specification.prompt.md: Especificar workflows CI/CD.
- create-github-issue-feature-from-specification.prompt.md: Crear un issue desde una especificación.
- create-github-issues-feature-from-implementation-plan.prompt.md: Crear múltiples issues desde plan de implementación.
- create-github-pull-request-from-specification.prompt.md: Estandarizar PRs desde especificación/plantilla.
- csharp-xunit.prompt.md: Escribir tests con xUnit.
- dotnet-best-practices.prompt.md: Revisar código .NET/C#.
- generate-custom-instructions-from-codebase.prompt.md: Mantener instrucciones vivas.
- code-exemplars-blueprint-generator.prompt.md: Generar `exemplars.md` del código.

## Recomendaciones rápidas por caso
- Migración Avalonia o refactors grandes: usa `review-and-refactor.prompt.md` para guiar cambios seguros y `dotnet-best-practices.prompt.md` para validar estilo/arquitectura.
- Planificación de épicas/features: comienza con `breakdown-plan.prompt.md` y después usa los prompts de creación de issues/PR para poblar el backlog.
- CI/CD: redacta o mejora especificaciones de workflows con `create-github-action-workflow-specification.prompt.md` antes de cambiar YAMLs.
- Testing: consulta `csharp-xunit.prompt.md` para estructura de pruebas, data-driven y fixtures.
- Instrucciones vivas: si cambian patrones (DI, MVVM, serial), ejecuta `generate-custom-instructions-from-codebase.prompt.md` y actualiza `.github/copilot-instructions.md`.
- Documentar patrones del repo: ejecuta `code-exemplars-blueprint-generator.prompt.md` para crear `docs/exemplars.md` con casos reales del código.

## Uso en VS Code
1. Abre el archivo del prompt y ajusta variables de entrada si aplica.
2. Copia el bloque del prompt al chat de Copilot (o referencia rutas del repo en el mensaje).
3. Sigue el output estructurado del prompt; guarda artefactos en `docs/` o `.github/` según indique.

## Mantenimiento
- Actualiza los prompts con `scripts/update-prompts.sh`.
- Revisa `LICENSE.awesome-copilot` ante cambios de upstream.
