# Chatmodes de GitHub Copilot (awesome-copilot)

Este directorio aloja chatmodes personalizados sugeridos desde el repositorio github/awesome-copilot para acelerar nuestro flujo de trabajo (.NET 8 + Avalonia, TDD, planificación y refactor).

No versionamos el contenido de los chatmodes aquí para respetar licencias aguas arriba; en su lugar, usa el script de sincronización para descargarlos desde la fuente oficial.

## Cómo sincronizar

Ejecuta en Linux/macOS:

```bash
./scripts/sync-chatmodes.sh
```

O con Makefile:

```bash
make chatmodes-sync
```

Archivos descargados en este directorio:
- expert-dotnet-software-engineer.chatmode.md
- csharp-dotnet-janitor.chatmode.md
- tdd-red.chatmode.md
- tdd-green.chatmode.md
- tdd-refactor.chatmode.md
- plan.chatmode.md
- specification.chatmode.md
- implementation-plan.chatmode.md
- debug.chatmode.md
- tech-debt-remediation-plan.chatmode.md
- task-planner.chatmode.md
- address-comments.chatmode.md

## Activación en VS Code

- Abre el archivo `.chatmode.md` en VS Code y usa la paleta: “Chat: Install Chat Mode from File”.
- Alternativamente, sigue las instrucciones de instalación en el README de awesome-copilot.
