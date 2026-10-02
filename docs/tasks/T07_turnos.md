# T07 — Turnos y máquina de estados
**Depende de:** T06 ✅ · **Tamaño:** M

> Antes de empezar: lee `docs/AGENT_CONTEXT.md` (§7), `docs/PROGRESS.md`. Verifica que T06 figura ✅. Trabaja SOLO en esta tarea.

## Objetivo
El juego sabe de quién es el turno y en qué fase está; solo el jugador activo puede actuar.

## Qué hacer
1. Crea `TurnManager` (`Scripts/Game/`, `NetworkBehaviour`, lógica en el Host) con `NetworkVariable<int> CurrentPlayerId` y `NetworkVariable<TurnState> State` (`WaitingForRoll, Moving, CheckingTile, Combat, TurnEnd, GameOver`).
2. **Jugador inicial** elegido por el Host (aleatorio o `PlayerId` 0; configurable). Rotación por `PlayerId`, solo jugadores `Alive`, saltando `Eliminated/Disconnected`.
3. Expón métodos de servidor: `BeginTurn(playerId)`, `EndTurn()`, `SetState(...)`, y un helper `ValidateActivePlayer(senderClientId, requiredState)` que usarán todos los RPC de tareas siguientes.
4. Habilita el input de `PlayerController` **solo** para el jugador activo y solo en el estado correcto.
5. **UI mínima:** `TURNO DE: [Nombre/Personaje]` para todos, y `ES TU TURNO` / `ESPERANDO TURNO...` para el local.
6. Para probar la rotación, añade un botón de depuración `TERMINAR TURNO` (solo activo para el jugador activo) marcado con `#if UNITY_EDITOR || DEVELOPMENT_BUILD`; se retirará en T15.
7. Separa la lógica de rotación en una clase C# pura (sin `NetworkBehaviour`) para poder probarla con tests en T16.

## Fuera de alcance
Dado, movimiento, eventos, combate.

## Criterios de aceptación
- El turno rota correctamente con 2, 3 y 4 jugadores simulados (para 3–4 usa tests o jugadores de prueba si no hay más instancias).
- Un cliente que no es el activo no puede avanzar el turno (el servidor lo rechaza y registra warning).
- La UI muestra el turno actual en ambas instancias de forma coherente.

## Verificación
Compila; Host + clon; alterna turnos con el botón de depuración.

## Cierre
Aplica el cierre estándar (AGENT_CONTEXT §9).
