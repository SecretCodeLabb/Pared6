# PROGRESS — "Pared, pared y otra pared"

> Copiar a `docs/PROGRESS.md`. El agente lo lee al empezar y lo actualiza al terminar cada sub-paso.

## EN CURSO
- Tarea: (ninguna)
- Último paso hecho: T03–T16 completadas en una sola sesión (a petición del usuario). Prototipo jugable de principio a fin.
- Siguiente paso: pruebas manuales con ParrelSync (ver `docs/TEST_PLAN.md`) y decidir siguientes mecánicas.

## Tareas
| ID | Tarea | Estado |
|---|---|---|
| T01 | Auditoría del proyecto | ✅ |
| T02 | Conexión LAN Host/Client | ✅ |
| T03 | Sala de espera (escena de selección como lobby) | ✅ |
| T04 | Selección de personaje sincronizada + LISTO + START | ✅ |
| T05 | Escena Game y tablero determinista | ✅ |
| T06 | Spawn y sincronización de jugadores | ✅ |
| T07 | Turnos y máquina de estados | ✅ |
| T08 | Dado y movimiento sincronizados | ✅ |
| T09 | Integración de EventTile | ✅ |
| T10 | Combate base | ✅ |
| T11 | Vidas y eliminación | ✅ |
| T12 | Cartas: datos, reparto y mano | ✅ |
| T13 | Uso de cartas en combate | ✅ |
| T14 | Progreso, victoria, fin y reinicio | ✅ |
| T15 | UI completa y casos límite | ✅ |
| T16 | Pruebas end-to-end | ✅ (26 tests EditMode; E2E con build + bots, ver TEST_PLAN) |

## Decisiones tomadas
- **NGO 2.x:** El proyecto cuenta con Netcode for GameObjects 2.13.2 y Unity Transport 2.7.3, por lo que se empleará la sintaxis RPC moderna (`[Rpc(SendTo.Server)]`, `[Rpc(SendTo.ClientsAndHost)]`).
- **Input System:** El proyecto usa exclusivamente el nuevo Input System (`activeInputHandler: 1`).
- **NetworkConnectionManager:** Centraliza la conexión LAN en `Assets/Scripts/Network/NetworkConnectionManager.cs`. Implementa Connection Approval con límite de `maxPlayers = 4` y flag `AcceptingConnections`.
- **Sincronización Pre-Partida:** Conteo de jugadores sincronizado entre Host y Clientes vía `CustomMessagingManager` (`PPOP_PlayerCount`), permitiendo feedback inmediato ("Conectado — N/4 jugador(es)") sin instanciar NetworkObjects en la escena de menú.
- **Detección ParrelSync:** Logs etiquetados con `[ParrelSync Clon]` / `[Unity Original]` mediante `#if UNITY_EDITOR` y `ParrelSync.ClonesManager.IsClone()`.
- **Build Settings:** Registradas las 3 escenas (`LobbyScene`, `CharacterSelection`, `Mapa`) en `EditorBuildSettings.asset`.

- **Flujo de escenas:** `LobbyScene` (menú) → `CharacterSelection` (sala) → `Mapa` (partida). Respuesta a Pregunta 1.
- **Sesión persistente:** prefab `Prefabs/Network/NetworkSession` (NetworkObject + `LobbyManager`), lo spawnea el Host y vive en DontDestroyOnLoad; contiene los slots (PlayerId 0–3, ClientId, personaje, LISTO).
- **Config única:** `Resources/GameRules.asset` (todos los valores de AGENT_CONTEXT §6). `NetworkConnectionManager` lee de ahí `maxPlayers` y puerto.
- **Lógica pura** en el ensamblado `Pared.Rules` (`Scripts/Game/Rules` + `Scripts/Cards/Data` vía `.asmref`) para tests EditMode.
- **Tablero:** `MapGenerator.GenerateWithSeed` (mismo RNG global sembrado; sin cambios de estructura). Casilla lógica = posición mundial redondeada (`BoardManager.CellOf`), única también en habitaciones. Eventos elegidos por el Host (`NetworkList<Vector2Int>`); al consumirse se quitan de la lista. Hash FNV-1a de paredes + eventos comparado en el Host.
- **Spawn (Pregunta 2):** bloque central 2x2 alrededor del centro, sin eventos. **Visual (Pregunta 3):** cápsula existente teñida con `CharacterData.boardColor` + etiqueta con nombre.
- **Turnos:** RPCs en `TurnManager` / `CombatManager` validados con `ValidateActivePlayer`. Empieza PlayerId 0 (configurable aleatorio). Timeout opcional.
- **Combate/cartas:** tipos del usuario como assets `CardType` (Daño, Defensa, Habilidad, Inútil). Daño +n = `AddToRoll(n)`; Defensa −n = `ReduceDamage(n)`. `ReduceRequired` y `PreventLifeLoss` disponibles sin asignar. Un evento fallado no se consume.
- **Empates:** solo el jugador activo puede ganar progreso, así que no hay empate real; si ocurriera, gana el activo y si no, el PlayerId más bajo (`VictoryRules`).
- **REINICIAR:** vuelve a `CharacterSelection` (personajes conservados, LISTO reseteado, conexiones aceptadas de nuevo). Más estable que recargar `Mapa`.
- **VOLVER AL MENÚ:** `NetworkConnectionManager.ReturnToMenu` (Shutdown + destruye el NetworkManager persistente; el de `LobbyScene` toma el relevo). Clientes vuelven solos si el Host cierra.
- **Canvas de `Mapa`:** pasado de Constant Pixel Size a Scale With Screen Size (ref. 3840x2160): mismo aspecto en 4K, visible en 1080p.
- **Pruebas:** `DevAutoPlayer` (solo Editor/Development Build) para E2E automático multi-instancia.

- **T17 (2026-10-02) Tablero, detección y duelos:** generador reescrito como `BoardLayout` puro (grid único,
  sin rotar habitaciones, paredes compartidas, conectividad total, 1 puerta por habitación). Antes: 270 aristas
  asimétricas y 335 discrepancias lógica/visual en habitaciones rotadas; ahora 0. Bug de orden de Awake que
  dejaba 0 eventos offline corregido. Eventos en centro (12) y en cada habitación (4). Pasar por un evento u
  otro jugador detiene el movimiento; duelos con el mismo sistema de dado+cartas. Cámara auto-encuadrada.
  40 tests EditMode OK; prueba en red Host+Cliente: hash igual, paradas por encuentro y duelo correctos.

- **T18 (2026-10-06) Diseño final de cartas:** marcos de `Resources/CardsDesign` en las cartas de juego (por `CardType.frame`) y la carta de personaje como fondo del prefab `CharacterCard`. Tipo "Habilidad" renombrado a "Especial". Efectos sin cambios. Editor: `PPOP/Apply Card Designs`.

- **T19 (2026-10-06) Ruleta:** `RouletteConfig` (Resources, opciones con peso), `RouletteManager` (Host, en `GameNetwork`), `RouletteWheelView` + prefab `Prefabs/UI/RouletteWheel` (estilo editable). Nuevo `TurnState.Roulette` tras un movimiento sin combate/duelo, una vez por turno. Estados en `PlayerNetworkData` (MovePenalty, SkipTurns, CardsBlockedTurns, DamagePenalty/Turns) que caducan al final del siguiente turno del jugador. Probado en Play cada efecto; 44 tests EditMode.

- **T20 (2026-10-06) Habitaciones por personaje:** `RoomBuilder` con tema visual (tilePrefab, decorations, themeName); `CharacterData.roomPrefab`; `RoomThemeAssignment` (N, E, S, O en orden de jugador + relleno por semilla) sincronizado en `GameStateManager.RoomCharacterIds`; `MapGenerator` aplica estilo por lado y decora con RNG propio (paredes intactas, auditoría 0 discrepancias). 6 temas de ejemplo con primitivas. 48 tests.

- **T21 (2026-10-07) Cartas especiales:** `SpecialCardEffect` (6 habilidades) + `CardRules` (cuándo se juega cada carta). Turno: `TurnManager.RequestPlayTurnCardRpc` (Silencio líquido, Cívica con `WallWalk`, Comida Frita). Combate: escape en `CombatManager` (Canicas / Gatos, fase `Escaped`). Pasiva: Salchipapa en `GameStateManager.ServerEliminate`. Estado `EventShieldTurns/Chance` en `PlayerNetworkData`. Probadas en Play: curar, esquivar entidad, Cívica 6 pasos a través de paredes, Canicas, Salchipapa. Gatos fotocopiados comparte código con Canicas (no probado en duelo real). 56 tests.

- **T22 (2026-10-07) Cámara:** `BoardCameraController` sigue al jugador activo (offline: al peón), vista general en carga/fin, zoom con rueda y C para alternar. `MapGenerator` le pasa los límites del tablero.

## Problemas abiertos
- Cliente cerrado a la fuerza: ~30 s hasta marcarse DESCONECTADO (timeout de Unity Transport; ajustable en `UnityTransport.DisconnectTimeoutMS`).
- Timeout de turno implementado pero no probado (desactivado por defecto).
- 4 jugadores reales sin probar (3 sí; lógica de 4 cubierta por tests).
