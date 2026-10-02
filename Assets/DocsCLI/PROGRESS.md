# PROGRESS — "Pared, pared y otra pared"

> Copiar a `docs/PROGRESS.md`. El agente lo lee al empezar y lo actualiza al terminar cada sub-paso.

## EN CURSO
- Tarea: (ninguna)
- Último paso hecho: T01 - Auditoría completa del proyecto y generación de docs/PROJECT_AUDIT.md.
- Siguiente paso: T02 - Conexión LAN Host/Client.

## Tareas
| ID | Tarea | Estado |
|---|---|---|
| T01 | Auditoría del proyecto | ✅ |
| T02 | Conexión LAN Host/Client | ⬜ |
| T03 | Sala de espera (escena de selección como lobby) | ⬜ |
| T04 | Selección de personaje sincronizada + LISTO + START | ⬜ |
| T05 | Escena Game y tablero determinista | ⬜ |
| T06 | Spawn y sincronización de jugadores | ⬜ |
| T07 | Turnos y máquina de estados | ⬜ |
| T08 | Dado y movimiento sincronizados | ⬜ |
| T09 | Integración de EventTile | ⬜ |
| T10 | Combate base | ⬜ |
| T11 | Vidas y eliminación | ⬜ |
| T12 | Cartas: datos, reparto y mano | ⬜ |
| T13 | Uso de cartas en combate | ⬜ |
| T14 | Progreso, victoria, fin y reinicio | ⬜ |
| T15 | UI completa y casos límite | ⬜ |
| T16 | Pruebas end-to-end | ⬜ |

## Decisiones tomadas
- **NGO 2.x:** El proyecto cuenta con Netcode for GameObjects 2.13.2 y Unity Transport 2.7.3, por lo que se empleará la sintaxis RPC moderna (`[Rpc(SendTo.Server)]`, `[Rpc(SendTo.ClientsAndHost)]`).
- **Input System:** El proyecto usa exclusivamente el nuevo Input System (`activeInputHandler: 1`).
- **Determinismo del Tablero:** Confirmado que `MapGenerator` no es determinista por defecto (`randomizeSeed = true`); se requerirá paso de semilla desde el Host y sincronización antes de generar.
- **Detección de Eventos:** Confirmado que actualmente `PlayerController` dispara el evento en cada casilla intermedia recorrida. Se corregirá en T09 para activarse solo al terminar el movimiento (`remainingMoves == 0`).
- **Selección de Personaje:** Actualmente guardada en el singleton `GameSession` (sin red); en T03/T04 se migrará a sincronización mediante NGO autoritativo en Host.

## Problemas abiertos
- Pregunta 1: Confirmar flujo de escenas: `LobbyScene` ➔ `CharacterSelection` ➔ `Mapa`.
- Pregunta 2: Confirmar posiciones de spawn de 2-4 jugadores en `Mapa` (¿casilla central compartida o repartidos en habitaciones cardinales?).
- Pregunta 3: Confirmar representación visual de los personajes en el tablero 3D (¿distinción por color/material o icono de retrato?).
