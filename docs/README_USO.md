# Cómo usar este paquete de prompts

El prompt original se dividió en **1 contexto base + 16 tareas cortas**. Cada tarea cabe en una sesión, termina con el proyecto compilando y deja un checkpoint, así un corte por consumo de tokens no pierde trabajo.

## 1. Preparación (una sola vez)

Copia estos archivos en tu proyecto de Unity:

```
<proyecto>/docs/AGENT_CONTEXT.md      ← AGENT_CONTEXT.md
<proyecto>/docs/PROGRESS.md           ← PROGRESS_inicial.md (renombrado)
<proyecto>/docs/tasks/T01_...T16_...  ← carpeta tasks/
```

Antes de lanzar T12, edita en `AGENT_CONTEXT.md` (§7) el bloque `[ESCRIBIR AQUÍ LOS TIPOS DE CARTA]` con tus tipos de carta (si no, usará los de defecto).

## 2. Cómo lanzar cada tarea (una sesión por tarea)

Abre una sesión nueva del CLI (limpia el contexto de la anterior) y pega:

```
Lee docs/AGENT_CONTEXT.md y docs/PROGRESS.md.
Ejecuta únicamente la tarea docs/tasks/T05_game_tablero_determinista.md.
Aplica el cierre estándar al terminar y detente.
```

Cambia el nombre del archivo por la tarea que toque. Si el CLI lo permite, puedes referenciar los archivos directamente.

## 3. Si una sesión se corta

Pega el contenido de `RETOMAR.md` en una sesión nueva. El agente lee `PROGRESS.md` (campo `EN CURSO`), comprueba el estado real con `git status` y compila antes de seguir.

## 4. Orden y dependencias

| Tarea | Contenido | Tamaño | Depende de |
|---|---|---|---|
| T01 | Auditoría (sin tocar código) | M | — |
| T02 | Conexión LAN Host/Client | M | T01 |
| T03 | Sala de espera (escena de selección como lobby) | M | T02 |
| T04 | Selección sincronizada + LISTO + START | M | T03 |
| T05 | Escena Game y tablero determinista | M | T04 |
| T06 | Spawn y sincronización de jugadores | L | T05 |
| T07 | Turnos y máquina de estados | M | T06 |
| T08 | Dado y movimiento sincronizados | L | T07 |
| T09 | Integración de EventTile | M | T08 |
| T10 | Combate base | M | T09 |
| T11 | Vidas y eliminación | S | T10 |
| T12 | Cartas: datos, reparto y mano | M | T11 |
| T13 | Uso de cartas en combate | M | T12 |
| T14 | Progreso, victoria, fin y reinicio | M | T13 |
| T15 | UI completa y casos límite | M | T14 |
| T16 | Pruebas end-to-end | M | T15 |

**Puntos de partida jugables** (por si quieres parar y probar):
- Tras **T04**: conexión + selección de personaje en red.
- Tras **T08**: partida con turnos y movimiento sincronizado.
- Tras **T11**: ya hay combate y vidas.
- Tras **T14**: partida completa con cartas y victoria.

## 4b. Reglas del flujo

- Una tarea por sesión, en orden. Cada tarea verifica que la anterior esté ✅ en `PROGRESS.md`.
- Las tareas **L** (T06, T08) son las más propensas a cortarse: si ves que se alarga, deja que el agente las parta en `a/b` (ya está permitido en `AGENT_CONTEXT §2`).
- Tras cada tarea haz un commit y, si algo falla en la prueba con ParrelSync, **corrígelo antes de pasar a la siguiente**.

## 5. Consejos para gastar menos tokens

- Sesión nueva por tarea (el contexto acumulado es lo que más consume).
- Pide siempre reportes breves; el código se revisa en los archivos/diff, no en el chat.
- No pidas "revisa todo el proyecto": T01 ya genera `PROJECT_AUDIT.md` y el resto de tareas lo reutiliza.
- Si un error se alarga, pide al agente que guarde el diagnóstico en `PROGRESS.md` > Problemas abiertos y lo retome en otra sesión con `RETOMAR.md`.

## 6. Archivos del paquete

- `AGENT_CONTEXT.md` — reglas, arquitectura y mecánicas compartidas.
- `PROGRESS_inicial.md` — plantilla de seguimiento.
- `RETOMAR.md` — prompt para reanudar tareas cortadas.
- `tasks/T01…T16` — una tarea por archivo.
