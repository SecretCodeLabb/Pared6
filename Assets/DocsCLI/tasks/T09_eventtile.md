# T09 — Integración de EventTile
**Depende de:** T08 ✅ · **Tamaño:** M

> Antes de empezar: lee `docs/AGENT_CONTEXT.md` (§7), `docs/PROGRESS.md` y el apartado EventTile en `docs/PROJECT_AUDIT.md`. Verifica que T08 figura ✅. Trabaja SOLO en esta tarea.

## Objetivo
El evento se activa **solo si el movimiento termina** sobre una casilla de evento, de forma controlada por el Host y visible para todos.

## Qué hacer
1. Revisa cómo `EventTile` detecta hoy al jugador (¿al pasar?). **Adáptalo** para que la detección durante el movimiento no dispare el evento; el evento se evalúa solo en estado `CheckingTile`.
2. Crea `EventManager` (`Scripts/Events/`) en el Host: en `CheckingTile`, consulta la casilla actual:
   - **Casilla normal** → `TurnEnd` → siguiente jugador.
   - **Casilla de evento** → estado `Combat` y notificación a todos.
3. UI para todos: `¡EVENTO!` y `¡TE HAS ENCONTRADO CON UNA AMENAZA!`.
4. **Consumo del evento:** con `consumeOnSuccess = true` la casilla se consume al superarlo (se oculta el indicador visual en todos los clientes). Hasta T10 no hay éxito/fracaso, así que deja el hook `OnEventResolved(playerId, won)`.
5. **Stub temporal de combate:** mientras no exista T10, tras mostrar el evento ofrece un botón temporal `CONTINUAR` (solo jugador activo) que cierra el evento y termina el turno. Márcalo con `// TEMP-T10` para eliminarlo en T10.
6. Mantén el indicador visual de evento existente.

## Fuera de alcance
Tirada de combate, vidas, cartas.

## Criterios de aceptación
- Pasar por una casilla de evento sin detenerse **no** activa nada.
- Terminar el movimiento sobre un evento muestra `¡EVENTO!` en ambas instancias y pone el estado en `Combat`.
- Terminar en casilla normal pasa el turno automáticamente.
- El evento no se activa dos veces ni en dos jugadores a la vez.

## Verificación
Compila; Host + clon; prueba pasar por encima vs. detenerse en un evento.

## Cierre
Aplica el cierre estándar (AGENT_CONTEXT §9).
