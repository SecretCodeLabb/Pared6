# Pared — Prototipo LAN (Unity 6 + Netcode for GameObjects)

Juego de tablero por turnos para 2–4 jugadores en red local. El Host también juega y es la
autoridad: decide tablero, tiradas, movimiento, combates, vidas, cartas y el final.

## Cómo probarlo

**Con ParrelSync (2 Editores):**
1. ParrelSync > Clones Manager > abre el clon (ya existe `My project_clone_0`).
2. En ambos, abre `Assets/Scenes/LobbyScene.unity` y pulsa Play.
3. Original: **CREAR** (Host). Clon: IP `127.0.0.1` (o la IP LAN que muestra el Host) y **UNIRSE**.

**Con builds:** File > Build Profiles > Windows, Build. Lanza varias copias del `.exe`.
En builds de desarrollo existe un bot de pruebas:
`Pared.exe -autojoin 127.0.0.1 -autoplay -screen-fullscreen 0` (se une, elige personaje, pulsa LISTO y juega solo).

## Flujo de partida

1. **Menú (`LobbyScene`)**: CREAR / UNIRSE por IP, puerto 7777.
2. **Selección (`CharacterSelection`)**: la escena existente hace de sala. Cada jugador elige un
   personaje distinto (los tomados se atenúan con el nombre del dueño) y pulsa **LISTO**.
   El Host pulsa **START** (≥ `minPlayers`, todos listos). Nadie puede entrar con la partida iniciada.
3. **Partida (`Mapa`)**: el Host genera la semilla; todos construyen el mismo tablero (hash comprobado).
   - Tu turno: **TIRAR DADO** (1–6) y muévete casilla a casilla con **WASD / flechas** o los botones.
     Las paredes no se atraviesan.
   - Si **pasas por** (o terminas en) una casilla de evento activa (cápsula morada), el movimiento se
     detiene ahí y empieza el **combate** (`GameRules.eventsTriggerWhenPassing`; en false solo al terminar).
   - Si entras en una casilla con **otro jugador**, el movimiento se detiene y hay un **DUELO**
     (`GameRules.duelsEnabled`): cada uno puede usar una carta y tira su dado; gana el mayor
     resultado y el perdedor pierde 1 vida (las cartas de defensa le protegen); empate = nadie pierde.
     El duelo tiene prioridad sobre un evento en la misma casilla.
     Puedes **USAR CARTA** (elige una de tu mano), **NO USAR CARTA** o **TIRAR DADO DE COMBATE**.
     `tirada + bonus ≥ requerido` → `¡SUPERASTE EL EVENTO!` (+1 progreso, el evento desaparece);
     si no → `FALLASTE EL EVENTO` (−1 vida, salvo carta de defensa).
   - 0 vidas → ELIMINADO (sale de los turnos).
4. **Fin**: gana el primero en llegar a `requiredEventsToWin` o el último jugador vivo.
   Host: **REINICIAR** (vuelve a la selección conservando personajes). Todos: **VOLVER AL MENÚ**.

## Valores configurables

Todos en **`Assets/Resources/GameRules.asset`** (Inspector):

| Campo | Defecto | Uso |
|---|---|---|
| `minPlayers` / `maxPlayers` | 2 / 4 | START y límite de conexiones |
| `startingLives` | 3 | vidas iniciales |
| `port` | 7777 | puerto LAN |
| `randomStartingPlayer` | false | false = empieza el PlayerId 0 |
| `turnTimeoutSeconds` | 0 | 0 = sin límite; si > 0 se pierde el turno / se tira automáticamente |
| `requiredCombatRoll` | 4 | valor a igualar o superar en combate |
| `damageOnFail` | 1 | vidas perdidas al fallar |
| `consumeOnSuccess` | true | el evento desaparece al superarlo |
| `maxCardsPerCombat` | 1 | cartas por combate |
| `startingHandSize` / `allowDuplicateCards` | 3 / true | reparto inicial |
| `requiredEventsToWin` | 3 | eventos para ganar |

Eventos (escena `Mapa`, objeto `EventManager`): `eventTileCount` (tablero central, 12),
`eventsPerRoom` (cada habitación, 4), `eventIndicatorScale` (tamaño del marcador, 3).
Tablero (`MapGenerator`): `width`/`height` del centro; tamaño de las habitaciones = `RoomBuilder.width/height`
del prefab `Room_01`; `autoFrameCamera` encuadra la cámara en todo el tablero.

## Cómo añadir una carta

1. Clic derecho en `Assets/ScriptableObjects/Cards/Cards` > Create > PPOP > Cards > **Card Data**.
2. Rellena `id` (único), `displayName`, `type`, `description`, `icon` (opcional) y `effect` (opcional;
   sin efecto = carta que se consume sin hacer nada).
3. Añádela a la lista de `Assets/Resources/Cards/CardDatabase.asset`. Ya entra en el reparto.

**Nuevo tipo de carta:** Create > PPOP > Cards > **Card Type** (nombre + color). No requiere código.

## Cómo añadir un efecto

1. Crea una clase en `Assets/Scripts/Cards/Data/Effects/`:
   ```csharp
   [CreateAssetMenu(menuName = "PPOP/Cards/Effects/MiEfecto")]
   public class MiEfecto : CardEffect
   {
       public override void Apply(CombatContext context) { /* modifica el contexto */ }
   }
   ```
   `CombatContext` expone `RollModifier`, `RequiredReduction`, `DamageReduction`, `PreventLifeLoss`…
2. Crea el asset del efecto y asígnalo a una carta. `CombatManager` no cambia.

Efectos incluidos: `AddToRoll(n)`, `ReduceRequired(n)`, `ReduceDamage(n)`, `PreventLifeLoss`.
Mazo por defecto (tipos del usuario): Golpe +1/+2/+3 (Daño), Escudo −1/−2/−3 (Defensa),
Truco del oficio (Habilidad, sin efecto aún), Piedra brillante (Inútil).

## Arquitectura (resumen)

- `Scripts/Game/Rules/` (ensamblado `Pared.Rules`, sin red, testeable): `GameRules`, `TurnRotation`,
  `VictoryRules`, `CharacterSelectionRules`, `CombatContext`/`CombatResolver`. Datos de cartas en `Scripts/Cards/Data/`.
- `Scripts/Network/NetworkConnectionManager`: conexión IP, aprobación, sesión, `ReturnToMenu`.
- `Scripts/Lobby/LobbyManager` (prefab `NetworkSession`, persiste entre escenas): slots, personaje, LISTO, START.
- `Scripts/Game/GameStateManager` (objeto `GameNetwork` en `Mapa`): semilla, eventos, barrera de carga, spawn, victoria, desconexiones.
- `Scripts/Game/TurnManager`: máquina de estados, dado y movimiento validados en el Host.
- `Scripts/Combat/CombatManager`, `Scripts/Cards/CardManager`.
- `Scripts/Players/PlayerNetworkData`: fuente única de verdad por jugador (en `Prefabs/Player.prefab`).
- `Scripts/UI/GameHUD`: HUD de red (se desactiva en modo offline).
- `PPOP/Setup Prototype` (Editor): recrea assets, prefabs y referencias de escena. `PPOP/Create Example Cards`.

## Limitaciones conocidas

- Tablero: un único grid alineado (centro + 4 habitaciones sin rotación) generado por `BoardLayout`
  (`Scripts/Game/Rules`). Cada pared se guarda en las dos casillas que la comparten: lógica y visual
  siempre coinciden (auditoría en tiempo de ejecución: 0 discrepancias; tests con 600 semillas).
- Un cliente que se cierra a la fuerza tarda ~30 s (timeout de Unity Transport) en marcarse DESCONECTADO.
- Las habilidades de personaje y las cartas de Habilidad/Inútil aún no tienen efecto.
- Los peones son la cápsula existente teñida con el color del personaje (`CharacterData.boardColor`).
- Sin reconexión: un jugador que se va no puede volver a la partida en curso.
