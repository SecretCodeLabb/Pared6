# T04 — Selección de personaje sincronizada, LISTO y START
**Depende de:** T03 ✅ · **Tamaño:** M

> Antes de empezar: lee `docs/AGENT_CONTEXT.md` (§8), `docs/PROGRESS.md` y el apartado de selección en `docs/PROJECT_AUDIT.md`. Verifica que T03 figura ✅. Trabaja SOLO en esta tarea.

## Objetivo
Cada jugador elige un personaje distinto, confirma con LISTO, y el Host inicia la partida cargando la escena `Game`.

## Qué hacer
1. **Elección:** la UI existente de selección envía `RequestSelectCharacter(characterId)` (ServerRpc). El Host valida (id válido, **no tomado por otro**, jugador no listo) y actualiza el slot + `PlayerNetworkData.CharacterId`. Si dos eligen a la vez, gana la primera solicitud.
2. **Cambio de elección:** permitido mientras no esté listo; al cambiar, se libera el anterior.
3. **UI:** personajes tomados quedan atenuados/bloqueados para los demás; se muestra quién eligió qué en la lista de slots. Mensaje si intenta elegir uno tomado.
4. **LISTO:** botón `LISTO` (reutiliza el de confirmar si existe), solo activo con personaje elegido. `RequestReady(bool)` ServerRpc.
5. **START GAME:** visible/activo solo para el Host, solo con ≥ `minPlayers` conectados, todos listos y personajes distintos. Al pulsarlo: `AcceptingConnections = false` y el Host carga la escena `Game` con `NetworkSceneManager` para todos.
6. **Desconexión en la sala:** el personaje del jugador que se va queda libre y su slot se actualiza para todos.
7. Los datos del personaje elegido (prefab/sprite/nombre/stats) se obtienen con el sistema existente a partir del `characterId`; no dupliques esa información.
8. Conserva el modo offline (una instancia) como fallback.

## Fuera de alcance
Generar el tablero en red, spawn de jugadores (T05–T06). Si el sistema de spawn local antiguo se ejecuta al cargar `Game`, déjalo tras un flag `useNetwork` para que no duplique jugadores, sin eliminarlo.

## Criterios de aceptación
- No se puede elegir el mismo personaje en dos jugadores, ni siquiera simultáneamente.
- Ambos ven en tiempo real las elecciones del otro.
- `START` solo lo ve/activa el Host y solo cumpliendo las condiciones.
- Al pulsar `START`, ambos instancias cargan `Game` y `PlayerNetworkData` conserva los personajes.

## Verificación
Compila; prueba Host + clon (conflicto de elección, cambio, LISTO, START).

## Cierre
Aplica el cierre estándar (AGENT_CONTEXT §9).
