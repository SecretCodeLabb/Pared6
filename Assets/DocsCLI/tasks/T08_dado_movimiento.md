# T08 — Dado y movimiento sincronizados
**Depende de:** T07 ✅ · **Tamaño:** L

> Antes de empezar: lee `docs/AGENT_CONTEXT.md` (§7), `docs/PROGRESS.md` y el apartado de dados/UI y `PlayerController` en `docs/PROJECT_AUDIT.md`. Verifica que T07 figura ✅. Trabaja SOLO en esta tarea.

## Objetivo
El jugador activo tira el dado, mueve casilla a casilla sin atravesar paredes, y todos ven lo mismo.

## Qué hacer
1. **Reutiliza** el sistema de dado/movimiento existente (lógica de pasos y paredes de `PlayerController`/`Tile`). Crea `DiceManager` solo si no hay equivalente.
2. Botón `TIRAR DADO`: activo solo para el jugador activo en estado `WaitingForRoll`. `RequestRollMovement()` (ServerRpc) → el **Host** genera 1–6, publica `LastRoll` y `MovesRemaining` (`NetworkVariable`) y pasa a `Moving`.
3. **Movimiento paso a paso:** el cliente solicita `RequestMoveStep(direction)`; el **Host valida** contra las conexiones north/south/east/west del tablero (misma lógica que el movimiento actual), actualiza `GridPosition`, decrementa `MovesRemaining`. Si el paso es inválido (pared), se rechaza sin consumir movimiento.
4. **Fin de movimiento:** al llegar `MovesRemaining` a 0 pasa a `CheckingTile`. Si no hay ningún paso válido posible antes de llegar a 0, el Host termina el movimiento (evita bloqueos). No cambies las reglas de movimiento existentes (retroceso, etc.).
5. Animación visual local a partir de `GridPosition`; el resto de jugadores ven el movimiento del activo.
6. **UI:** resultado del dado (`DADO: [5]`) y `MOVIMIENTOS: N` visibles para todos.
7. Protege contra doble clic/RPC duplicado: una segunda solicitud de tirada en el mismo turno se ignora.
8. Retira solo lo imprescindible del flujo de debug de T07; el botón `TERMINAR TURNO` puede seguir hasta T09.

## Fuera de alcance
Eventos, combate.

## Criterios de aceptación
- La tirada la decide el Host y es idéntica en ambas instancias.
- El jugador no atraviesa paredes y las posiciones coinciden en ambas instancias tras cada paso.
- Los jugadores no activos no pueden tirar ni mover (rechazo en servidor).
- Al terminar el movimiento el estado pasa a `CheckingTile`.

## Verificación
Compila; Host + clon; 10 turnos seguidos sin desincronización.

## Cierre
Aplica el cierre estándar (AGENT_CONTEXT §9).
