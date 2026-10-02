# Auditoría del Proyecto — "Pared" (Unity 6, NGO)

> Fecha de auditoría: 2026-10-02  
> Entregable de la tarea T01 (`docs/tasks/T01_auditoria.md`).  
> Este documento sirve de referencia técnica para todas las tareas posteriores (T02–T16).

---

## 1. Paquetes y Configuración del Proyecto

- **Versión de Unity:** `6000.3.7f1` (Unity 6).
- **Netcode for GameObjects (NGO):** `2.13.2` (`com.unity.netcode.gameobjects`).
  - Al ser NGO 2.x, se debe emplear la sintaxis de RPC moderna con atributos `[Rpc(SendTo.Server)]` y `[Rpc(SendTo.ClientsAndHost)]`, o `[Rpc(SendTo.SpecifiedInParams)]` según `AGENT_CONTEXT.md §5`.
- **Unity Transport:** `2.7.3` (`com.unity.transport`, resuelto en `Packages/packages-lock.json` como dependencia directa del ecosistema NGO).
- **Input System:** `1.18.0` (`com.unity.inputsystem`).
  - En `ProjectSettings/ProjectSettings.asset`, `activeInputHandler: 1` (Nuevo Input System activo exclusivamente).
  - Existe `Assets/Scripts/Editor/FixInputSystem.cs` para asegurar que los `EventSystem` de las escenas usen `InputSystemUIInputModule`.
- **UI / TextMeshPro:** Incluido mediante `com.unity.ugui: 2.0.0`.
- **ParrelSync:** `com.veriorpies.parrelsync` instalado vía git URL en `Packages/manifest.json`.
- **Falta:** No falta ningún paquete para la infraestructura LAN por IP/puerto directo.

---

## 2. Escenas y Build Settings

### En Build Settings (`ProjectSettings/EditorBuildSettings.asset`)
- `Assets/Scenes/LobbyScene.unity` (Índice 0, habilitada).
- **Faltan en Build Settings:**
  - `Assets/Scenes/CharacterSelection.unity`
  - `Assets/Scenes/Mapa.unity`

### Catálogo y Roles de Escenas
1. **`Assets/Scenes/LobbyScene.unity` (Menú de Conexión LAN):**
   - Posee un `NetworkManager` con componente `UnityTransport`.
   - UI en Canvas con `ipAddressText` (muestra IP local), `ipInputField` (para IP de destino) y botones para `StartHost` y `StartClient` vinculados a `NetworkConnectUI.cs`.
2. **`Assets/Scenes/CharacterSelection.unity` (Sala de Selección / Lobby):**
   - Posee un Canvas con grid de selección (`CharacterGrid`), botón `ContinueButton` y script `CharacterSelectionManager.cs`.
   - Actualmente configurada para selección local (sin red).
3. **`Assets/Scenes/Mapa.unity` (Escena de Juego):**
   - Contiene los sistemas de juego: `MapGenerator`, `BoardManager`, `EventManager`, `Canvas` con botón de dado `RollButton`, texto `GameInfoText`, panel `EventPanel` (`EventUI.cs`), cámara e iluminación.

---

## 3. Tablero (Generación, Conexiones y Determinismo)

### Conexión de Componentes
- `MapGenerator.cs` (GO `MapGenerator`):
  - Dimensiones: cuadrícula central de 8x8 (`width = 8`, `height = 8`, `tileSize = 2f`).
  - Genera 4 habitaciones cardinales de 5x5 (`RoomBuilder.cs`) adosadas al norte, sur, este y oeste.
  - Instancia `tilePrefab` (`Assets/Prefabs/Tile.prefab`) con componentes `Tile.cs` y `TileView.cs`.
  - `Tile.cs` gestiona `gridPosition` (Vector2Int), banderas de pared (`northWall`, `southWall`, `eastWall`, `westWall`), y notifica a `TileView.UpdateWalls(this)`.
- `BoardManager.cs` (GO `BoardManager`):
  - En `Start()` llama a `InitializeBoard()`, recolectando todos los `Tile` mediante `mapGenerator.GetComponentsInChildren<Tile>()` y registrando casillas transitables en `allTiles` (List<Tile>).
  - `GetNeighbor(currentTile, direction)` valida paredes lógicas y busca el tile más próximo en `allTiles` por distancia espacial (`Vector3.Distance < 0.01f`).

### ¿La generación es determinista?
- **RESPUESTA EXPLÍCITA:** Actualmente **NO es determinista**.
- `MapGenerator.Awake()` tiene `randomizeSeed = true`, lo que fuerza una semilla aleatoria en cada ejecución (`seed = Random.Range(0, 999999)`).
- Todo el algoritmo de apertura de caminos (`CreateMainPaths`, `CreateExtraPaths`) y de habitaciones (`RoomBuilder.GenerateRoom`) depende de `UnityEngine.Random` global.

### Qué hace falta para aceptar una semilla fija
1. Asignar `MapGenerator.seed` externamente antes de la generación y desactivar `randomizeSeed` (o exponer `GenerateMapWithSeed(int newSeed)`).
2. El Host debe ser la autoridad que elija la semilla y la distribuya a todos los Clientes (vía RPC o `NetworkVariable`).
3. Inicializar `Random.InitState(seed)` de forma sincronizada antes de generar la cuadrícula central, las habitaciones y los eventos.
4. Generar y loguear en consola un hash determinista (suma o CRC de posiciones y paredes) en Host y Clientes para verificar paridad exacta de tablero.

---

## 4. Jugadores

### Gestión y Spawn (`PlayerController.cs` y `BoardManager.cs`)
- En `BoardManager.SpawnPlayer()`:
  - Busca la casilla central con `FindStartingTile()` (`width / 2, height / 2`).
  - Instancia `playerPrefab` (`Assets/Prefabs/Player.prefab` con `PlayerController`).
  - Llama a `player.Initialize(startTile, this)`.
  - Actualmente soporta únicamente **1 jugador local** (`private PlayerController player`).

### Representación de Posición
- **Esquema Híbrido:**
  - **Lógica:** `player.currentTile` referencia directa al componente `Tile` (con coordenadas discretas `gridPosition` Vector2Int).
  - **Transform:** `transform.position` en el espacio 3D de Unity.
  - Al moverse, `PlayerController.MoveToTile` inicia la corrutina `MoveRoutine`, que interpola `transform.position` suavemente con `Vector3.MoveTowards` a velocidad `moveSpeed = 5f`.

---

## 5. EventTile

### Generación de Eventos
- `EventManager.GenerateEventTiles()` se ejecuta tras `MapGenerator.GenerateMap()`.
- Filtra tiles centrales hijos de `MapGenerator` (`IsCentralTile(tile)`).
- Elige `eventTileCount` (5 por defecto) casillas aleatorias sin repetición mediante `Random.Range(0, centralTiles.Count)`.
- Añade dinámicamente `EventTile.cs` a la casilla, asigna `hasEvent = true` e instancia el prefab visual `Assets/Scripts/Events/EventIndicator.prefab` a altura `y = 0.5f`.

### ¿Cuándo se detecta el evento?
- **RESPUESTA EXPLÍCITA:** Actualmente se detecta **AL PASAR** (en cada casilla intermedia por la que camina el jugador).
- **Evidencia en código:** En `PlayerController.cs` (líneas 151–154), al final de la corrutina de cada paso individual `MoveRoutine()`:
  ```csharp
  if (EventManager.Instance != null)
  {
      EventManager.Instance.CheckTile(currentTile);
  }
  ```
  `EventManager.CheckTile` invoca `eventTile.ActivateEvent()` inmediatamente, incluso si al jugador todavía le quedan pasos pendientes (`remainingMoves > 0`).
- **Ajuste requerido por `AGENT_CONTEXT.md §7`:** El evento debe dispararse **únicamente cuando el movimiento termina** en la casilla (`remainingMoves == 0`), no al cruzarla en tránsito.

---

## 6. Dados y UI

### Scripts y Lógica
- `BoardManager.RollDice()`: Si `player.remainingMoves <= 0`, obtiene un valor con `Random.Range(minDiceValue, maxDiceValue + 1)` (1 a 6) y llama a `player.SetMoves(result)`.
- `PlayerController.cs`: Mantiene `public int remainingMoves;`, decrementándolo en `MoveToTile()` por cada paso completado.
- El movimiento se solicita en `PlayerController.HandleInput()` leyendo teclas WASD / Flechas (`Keyboard.current`) y llamando a `boardManager.TryMove(direction)`.

### UI Existente en `Mapa.unity`
- `RollButton`: Botón en Canvas que invoca `BoardManager.RollDice()`.
- `GameInfoText`: Componente `TextMeshProUGUI` que muestra:
  `Dado: [X]\nMovimientos restantes: [Y]`.
- `EventPanel` + `EventText`: Panel emergente controlado por `EventUI.cs` que exhibe `"EVENTO ACTIVADO"` durante 2 segundos tras activarse una casilla con evento.

---

## 7. Escena de Selección de Personaje

### Escena y Jerarquía UI
- Escena: `Assets/Scenes/CharacterSelection.unity`.
- Canvas con título `"Selección de Personajes"`, `CharacterGrid` (GridLayoutGroup), `SelectedText` y botón `ContinueButton` ("Continuar").
- Prefab de cartas: `Assets/Prefabs/CharacterCard.prefab` con componente `CharacterCardUI.cs` (nombre, ATK, DEF, HP, descripción de habilidad, retrato e imagen de selección `SelectedVisual`).

### Dónde se guarda la selección
- **RESPUESTA EXPLÍCITA:** Se guarda en `GameSession.Instance.Players`, un `MonoBehaviour` singleton persistente con `DontDestroyOnLoad` (`Assets/Scripts/Characters/GameSession.cs`) que almacena una `List<PlayerState>`.
- En `CharacterSelectionManager.OnContinueClicked()`, se puebla `GameSession.Instance.SetPlayers(selectedCharacters)` y se carga la escena `Mapa` con `SceneManager.LoadScene("Mapa")`.

### Cómo la lee el tablero
- **RESPUESTA EXPLÍCITA:** Actualmente **NO la lee**. `BoardManager.cs` ignora por completo `GameSession.Instance` y genera un único jugador fijo con `playerPrefab`.

### Datos de Personajes Disponibles
- Ubicados en `Assets/Data/Characters/` como `CharacterData` (ScriptableObjects):
  1. `Character_Guerrero.asset` (ATK 5, DEF 5, HP 3)
  2. `Character_Mago.asset` (ATK 8, DEF 2, HP 3)
  3. `Character_Pícaro.asset` (ATK 6, DEF 3, HP 3)
  4. `Character_Paladín.asset` (ATK 4, DEF 7, HP 3)
  5. `Character_Cazador.asset` (ATK 7, DEF 4, HP 3)
  6. `Character_Clérigo.asset` (ATK 3, DEF 6, HP 3)

### Supuestos de Jugador Único Local
- La UI actual asume que un solo usuario físico hace clic en entre 2 y 4 tarjetas desde la misma pantalla para armar un grupo local y continuar. No distingue clientes de red ni estados "Listo" individuales.

---

## 8. Sistemas Existentes y Posibles Solapamientos

1. **Turnos:** No existe aún ningún sistema de turnos multijugador ni máquina de estados de turno.
2. **Vidas y Atributos:** `PlayerState.cs` incluye `currentLives`, `TakeDamage(int damage)`, `damageBonus`, `defenseBonus` y `IsAlive`, pero solo opera en memoria local de `GameSession`. En la partida real (`Mapa`), el jugador no tiene vidas ni daño conectado.
3. **Puntos / Misiones:** Inexistentes en código.
4. **Cartas:** Inexistentes en código (se implementarán en T12 y T13).
5. **Menús y Prefabs Desconectados:**
   - Hay dos escenas de menú no enlazadas: `LobbyScene` (conexión LAN) y `CharacterSelection` (selección local).
   - Existen dos prefabs de jugador:
     - `Assets/Player.prefab`: Tiene `NetworkObject`, `NetworkTransform` y `CharacterMovement.cs` (movimiento continuo WASD libre con CharacterController, asignado a `NetworkManager`).
     - `Assets/Prefabs/Player.prefab`: Tiene `PlayerController.cs` (movimiento por casillas discreto, asignado a `BoardManager`).
     - *Riesgo:* Deben unificarse utilizando `Assets/Prefabs/Player.prefab` adaptado a red con `NetworkObject`.

---

## 9. Plan de Cambios por Tarea (T02–T16)

| Tarea | Scripts a Ampliar | Scripts a Crear | Riesgos y Mitigaciones |
|---|---|---|---|
| **T02: Conexión LAN** | `NetworkConnectUI.cs` | `NetworkManagerHelper.cs` (si necesario) | Mantener sincronizado el estado de conexión sin relay externo. |
| **T03: Sala de espera** | `CharacterSelectionManager.cs` | `LobbyNetworkManager.cs` | Transición sincronizada desde conexión hasta la sala; no romper UI existente. |
| **T04: Selección sincronizada** | `CharacterSelectionManager.cs`, `CharacterCardUI.cs` | `PlayerLobbyState.cs` (o uso de `PlayerData.cs`) | Evitar selección concurrente del mismo personaje; validación en Host. |
| **T05: Tablero determinista** | `MapGenerator.cs`, `BoardManager.cs`, `EventManager.cs` | `BoardNetworkSync.cs` | Desfase de semillas en Clientes; mitigar con hash de verificación. |
| **T06: Spawn de jugadores** | `BoardManager.cs`, `PlayerController.cs` | `PlayerNetworkData.cs` | Spawns múltiples en la misma casilla inicial o casillas designadas; pertenencia de red (`IsOwner`). |
| **T07: Turnos y FSM** | `BoardManager.cs`, `PlayerController.cs` | `TurnManager.cs` | Bloqueo si un cliente se desconecta; temporizador o control del Host. |
| **T08: Dado y movimiento** | `BoardManager.cs`, `PlayerController.cs` | `MovementNetworkValidator.cs` | Desincronización de pasos; el Host debe validar cada paso por casillas transitables. |
| **T09: EventTile** | `EventManager.cs`, `EventTile.cs`, `PlayerController.cs` | — | Corregir detección al terminar movimiento en lugar de al pasar. |
| **T10: Combate base** | `EventManager.cs`, `EventUI.cs` | `CombatManager.cs` | Transición ordenada entre estado de tablero y estado de combate sin congelar la partida. |
| **T11: Vidas y eliminación** | `PlayerNetworkData.cs`, `CombatManager.cs` | — | Manejo de jugadores eliminados para que no reciban turnos. |
| **T12: Cartas (datos y mano)** | `PlayerNetworkData.cs` | `CardData.cs`, `CardEffect.cs`, `HandManager.cs` | Reparto justo y sincronización segura de manos privadas por jugador. |
| **T13: Cartas en combate** | `CombatManager.cs`, `HandManager.cs` | — | Consumo correcto de cartas y aplicación de modificadores de ATK/DEF. |
| **T14: Victoria y reinicio** | `TurnManager.cs`, `BoardManager.cs` | `GameEndUI.cs` | Reinicio limpio de partida sin fugas de memoria en `NetworkManager`. |
| **T15: UI completa y bordes** | `EventUI.cs`, Canvas de HUD | `GameHUD.cs` | Desconexiones intempestivas en mitad de turno o combate. |
| **T16: Pruebas E2E** | — | Scripts de test / checklist de verificación | Validar sesión completa de 2 a 4 jugadores con ParrelSync. |

---

## 10. Riesgos Principales y Preguntas Abiertas

### Riesgos Técnicos Clave
1. **Doble Prefab de Jugador:** `Assets/Player.prefab` (con física libre y `CharacterMovement`) vs `Assets/Prefabs/Player.prefab` (con movimiento por cuadrícula en `PlayerController`). La solución obligatoria es adaptar `Assets/Prefabs/Player.prefab` a NGO (`NetworkObject`) y descartar el movimiento continuo libre.
2. **Activación Prematura de Eventos:** `PlayerController.MoveRoutine` activa el evento en casillas intermedias. Debe corregirse en T09 para verificar `remainingMoves == 0`.
3. **Semilla de Tablero Desincronizada:** Al generarse el mapa central y las 4 habitaciones con llamadas múltiples a `UnityEngine.Random`, cualquier invocación asíncrona desfasaría la generación en clientes. El Host debe enviar la semilla exacta y garantizar la secuencia de generación.

### Preguntas Abiertas para el Usuario (Máximo 3)
1. **Flujo de Escenas:** ¿Confirmas que el flujo previsto es `LobbyScene` (menú de conexión IP) ➔ `CharacterSelection` (sala de espera multijugador con personajes) ➔ `Mapa` (tablero de juego)?
2. **Posiciones de Spawn de Jugadores:** Al iniciar en `Mapa`, ¿los 4 jugadores deben iniciar todos en la casilla central de la sala 8x8, o cada jugador iniciará en una de las 4 habitaciones cardinales (Norte, Sur, Este, Oeste)?
3. **Representación Visual de Personajes en Tablero:** Dado que los 6 personajes cuentan con `CharacterData` (con Sprite 2D de retrato) pero el prefab de jugador es un cilindro/cápsula 3D básico, ¿la diferenciación visual en el tablero se realizará mediante colores de material / billboard con Sprite, o se mantendrá el modelo actual por ahora?
