# Prompts reutilizables (bundle mínimo)

Este directorio contiene un conjunto curado de prompts de awesome-copilot para apoyar nuestro flujo Spec-Driven, .NET 8/Avalonia, pruebas y CI/CD.

Origen de los prompts: https://github.com/github/awesome-copilot (MIT).
Para actualizar a la última versión, usa `scripts/update-prompts.sh`.

## Incluidos

- review-and-refactor.prompt.md — Revisión y refactor guiado.
- breakdown-plan.prompt.md — Desglose de planes en épicas/historias/tareas.
- create-github-action-workflow-specification.prompt.md — Especificación de workflows CI/CD.
- create-github-issue-feature-from-specification.prompt.md — Crear issue desde especificación.
- create-github-issues-feature-from-implementation-plan.prompt.md — Issues por fase desde plan de implementación.
- create-github-pull-request-from-specification.prompt.md — PR a partir de especificación.
- csharp-xunit.prompt.md — Mejores prácticas de xUnit.
- dotnet-best-practices.prompt.md — Mejores prácticas .NET/C#.
- generate-custom-instructions-from-codebase.prompt.md — Generar instrucciones personalizadas desde el codebase.
- code-exemplars-blueprint-generator.prompt.md — Generador de “exemplars” del código.

## Uso rápido en VS Code

1) Abre el archivo del prompt que necesites en el editor.
2) Copia/ajusta variables de entrada si el prompt lo solicita.
3) Invoca Copilot Chat con el contenido del prompt como contexto (o pégalo directamente en el chat con referencias a rutas del repo cuando aplique).

Consejo: Consulta `docs/COPILOT_PROMPTS.md` para saber cuándo usar cada prompt en este proyecto.
