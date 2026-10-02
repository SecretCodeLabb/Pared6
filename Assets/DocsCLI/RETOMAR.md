# Prompt para RETOMAR una tarea interrumpida

Pegar tal cual en una sesión nueva:

```
Lee docs/AGENT_CONTEXT.md y docs/PROGRESS.md.
Hay una tarea en curso (campo EN CURSO). Retómala desde el "Siguiente paso".

Antes de escribir código:
1. Comprueba el estado real: `git status` / `git diff --stat` y revisa si el proyecto compila.
2. Si hay cambios a medias que no compilan, termínalos o revierte solo esos cambios.
3. Lee únicamente los archivos de la tarea, no todo el proyecto.

Después continúa hasta cumplir los criterios de aceptación del archivo docs/tasks/T**.md de esa tarea
y aplica el cierre estándar (AGENT_CONTEXT §9). No empieces otra tarea.
```
