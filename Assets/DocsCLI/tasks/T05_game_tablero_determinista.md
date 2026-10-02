# T05 — Escena Game y tablero determinista
**Depende de:** T04 ✅ · **Tamaño:** M

> Antes de empezar: lee `docs/AGENT_CONTEXT.md` (§5 Tablero), `docs/PROGRESS.md` y el apartado de tablero/EventTile en `docs/PROJECT_AUDIT.md`. Verifica que T04 figura ✅. Trabaja SOLO en esta tarea.

## Objetivo
Todos los jugadores ven **exactamente el mismo tablero**, generado por semilla decidida por el Host.

## Qué hacer
1. Crea `GameStateManager` (`Scripts/Game/`, `NetworkBehaviour`) en la escena `Game`. El Host genera `boardSeed` y la lista de posiciones de `EventTile`, y los publica (`NetworkVariable<int>` + `NetworkList`).
2. Adapta `MapGenerator` (y lo que use `Random`) para generar a partir de una **semilla** inyectada, **sin cambiar el tamaño del mapa ni el resultado estructural** respecto a una semilla dada. Usa un generador local (`System.Random(seed)`) en vez de estado global si hace falta.
3. Las posiciones de eventos las decide el Host y se aplican en todos los clientes (el cliente no sortea eventos por su cuenta). Si el sistema `EventTile` actual sortea internamente, cámbialo para recibir las posiciones.
4. **Barrera de carga:** el tablero se genera en cada cliente cuando recibe la semilla y la escena está cargada (`SceneEvent` / `OnNetworkSpawn`). Solo cuando todos los clientes terminan, el Host avanza (flag `BoardReady`).
5. Calcula un **hash** del tablero (paredes + eventos) y loguéalo en Host y Client; el Host compara y avisa con `Debug.LogError` si difieren.
6. Mantén el modo offline: sin red, usa una semilla local y funciona como antes.

## Fuera de alcance
Spawn/sincronización de jugadores, turnos.

## Criterios de aceptación
- Los hashes coinciden en Host y Client en 5 partidas consecutivas.
- Mismo layout de paredes y mismas casillas de evento visibles en ambas instancias.
- Con la misma semilla en offline se reproduce el mismo mapa.
- El tablero sigue siendo 8x8 + habitaciones 5x5; paredes sin cambios.

## Verificación
Compila; Host + clon; compara hash y captura visual.

## Cierre
Aplica el cierre estándar (AGENT_CONTEXT §9).
