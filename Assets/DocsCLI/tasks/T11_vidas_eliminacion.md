# T11 — Vidas y eliminación
**Depende de:** T10 ✅ · **Tamaño:** S

> Antes de empezar: lee `docs/AGENT_CONTEXT.md` (§7), `docs/PROGRESS.md`. Verifica que T10 figura ✅. Trabaja SOLO en esta tarea.

## Objetivo
Las vidas se muestran y actualizan en tiempo real para todos, y quedar sin vidas elimina al jugador de los turnos.

## Qué hacer
1. **UI de vidas:** corazones `♥ ♥ ♥` para el jugador local (HUD) y una lista de **todos** los jugadores con nombre/personaje y vidas (`Jugador 1 — ♥♥♥`). Se actualiza al cambiar `Lives` (suscripción a `OnValueChanged`).
2. Lógica en el Host: si `Lives <= 0` → `State = Eliminated` y evento `OnPlayerEliminated(playerId)`.
3. `TurnManager` ya salta jugadores no `Alive`: comprueba que funciona también si el eliminado era el del turno actual.
4. Feedback sencillo: el peón del eliminado se atenúa/oculta y se muestra `ELIMINADO` en la lista. Un eliminado no puede actuar (el servidor rechaza sus RPC).
5. Si solo queda **un** jugador `Alive`, lanza el evento `OnLastPlayerStanding(playerId)` (la pantalla de victoria llega en T14; por ahora solo log y mensaje).

## Fuera de alcance
Pantalla final, cartas.

## Criterios de aceptación
- Perder un combate actualiza los corazones en ambas instancias al instante.
- A 0 vidas el jugador queda `Eliminated` y el turno lo salta.
- Si el eliminado es el jugador activo, el turno pasa correctamente sin bloquearse.

## Verificación
Compila; Host + clon; fuerza derrotas (p. ej. `requiredCombatRoll = 7` temporal) hasta eliminar a uno.

## Cierre
Aplica el cierre estándar (AGENT_CONTEXT §9).
