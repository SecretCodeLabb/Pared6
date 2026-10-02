# T06 — Spawn y sincronización de jugadores
**Depende de:** T05 ✅ · **Tamaño:** L

> Antes de empezar: lee `docs/AGENT_CONTEXT.md` (§5 Jugadores), `docs/PROGRESS.md` y el apartado de jugadores en `docs/PROJECT_AUDIT.md`. Verifica que T05 figura ✅. Trabaja SOLO en esta tarea.

## Objetivo
Cada jugador conectado aparece en una casilla válida y distinta, con su personaje, y todos ven las mismas posiciones.

## Qué hacer
1. Convierte el jugador en un **prefab con `NetworkObject`** (reutiliza el prefab/`PlayerController` existente; no lo recrees). El Host hace el spawn por cada jugador de la sala con ownership del cliente correspondiente.
2. Amplía `PlayerNetworkData` (fuente única de verdad): `PlayerId`, `CharacterId`, `Lives` (= `startingLives`), `GridPosition` (`NetworkVariable<Vector2Int>`, coordenadas lógicas), `Progress` (0), `State` (`Alive/Eliminated/Disconnected`) y `Hand` (se rellena en T12).
3. **Posiciones iniciales:** usa la lógica de spawn de `BoardManager`. Deben ser casillas válidas y **distintas** para hasta 4 jugadores.
4. **Apariencia:** el personaje visual sale del `CharacterId` mediante el sistema existente.
5. Sincroniza la posición por **coordenadas lógicas**; el movimiento visual (interpolación) se hace localmente a partir de `GridPosition`.
6. Haz `PlayerController` consciente de red: el input solo se procesa para el **owner**, y por ahora queda **bloqueado por defecto** (se habilita en T07/T08). Conserva el comportamiento offline.
7. Identifica el "jugador local" para la UI y la cámara (si la cámara sigue al jugador, que siga al local).
8. Desactiva (tras flag `useNetwork`) el spawn local antiguo de `BoardManager` para no duplicar jugadores.

## Fuera de alcance
Turnos, movimiento sincronizado, cartas.

## Criterios de aceptación
- Con 2 instancias se ven 2 jugadores con personajes distintos en casillas distintas, idénticas en ambas.
- Cada instancia sabe cuál es su jugador local.
- No hay jugadores duplicados ni errores de spawn/ownership en consola.

## Verificación
Compila; Host + clon; repite 3 veces con distintas semillas.

## Cierre
Aplica el cierre estándar (AGENT_CONTEXT §9).
