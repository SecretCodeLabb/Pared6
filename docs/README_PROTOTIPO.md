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
   - **Ruleta:** si el movimiento termina **sin** combate ni duelo, aparece una ruleta (el jugador
     activo pulsa **GIRAR**; el Host decide el resultado y todos ven la misma animación). Una vez por turno.
     Efectos: +1/+2/+3 movimientos **ahora**; −1/−2/−3 a tu **próxima tirada**; **pierdes un turno**;
     **sin cartas** y **−1/−2 de daño** (resta a tus tiradas de combate y duelo) hasta el final de tu
     próximo turno; **pierdes una vida** (probabilidad baja); **pelea obligatoria** contra una entidad
     (combate normal, cuenta como evento superado si ganas); **nada**. Los efectos activos se ven en
     la lista de jugadores.
4. **Fin** (un único ganador): el **primero que completa la misión de su personaje** o el **último
   jugador en pie**. Si todos pierden sus vidas, la partida termina sin ganador.
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

Eventos (escena `Mapa`, objeto `EventManager`): `eventTileCount` (tablero central, 12),
`eventsPerRoom` (cada habitación, 4), `eventIndicatorScale` (tamaño del marcador, 3).
Tablero (`MapGenerator`): `width`/`height` del centro; tamaño de las habitaciones = `RoomBuilder.width/height`
del prefab `Room_01`; `autoFrameCamera` encuadra la cámara en todo el tablero.

## Ruleta: probabilidades y aspecto

- **Opciones** (`Assets/Resources/RouletteConfig.asset`): texto del segmento, texto del resultado,
  color, **peso** (probabilidad = peso / suma de pesos), tipo de efecto y valor. Peso 0 = no sale.
  También `autoSpinSeconds` (si nadie gira en ese tiempo, gira sola; 0 = nunca), `spinSeconds`, `resultSeconds` y `segmentSizeByWeight` (segmentos iguales o proporcionales).
  Activar/desactivar la ruleta: `GameRules.rouletteEnabled`.
- **Aspecto** (`Assets/Prefabs/UI/RouletteWheel.prefab`): cámbialo libremente (sprites, colores,
  fuentes, tamaños, puntero, borde, botón). Solo mantén asignadas en `RouletteWheelView` las
  referencias (el botón necesita *Raycast Target* activo en su imagen; el script lo fuerza) `wheel` (lo que gira), `segmentTemplate` (Image *Filled / Radial 360 / Top / clockwise*
  con un TMP hijo) y `spinButton`. Opcionales: textos de título, jugador, resultado y botón.
  Ajustes de animación en el mismo componente (vueltas, curva, posición de etiquetas).
- **PPOP/Create Roulette** crea los assets solo si no existen (nunca pisa tus cambios).

## Habitaciones laterales por personaje

Cada personaje tiene su habitación (`CharacterData.roomPrefab`). En cada partida el Host asigna
las 4 habitaciones: los personajes elegidos van en orden de jugador a **Norte, Este, Sur, Oeste**;
los lados libres toman personajes no elegidos (según la semilla). El laberinto, el tamaño y la
puerta de cada habitación siguen siendo procedurales e iguales para todas: el tema es **solo visual**.

El prefab de habitación (`RoomBuilder`) define el aspecto:
- `tilePrefab`: casilla de esa habitación (variante de `TilesRoom` con tus modelos/materiales de
  suelo y paredes; mantén `Tile` + `TileView` y los 4 muros `Wall_*`).
- `decorations`: modelos 3D que aparecen por el suelo (cualquier prefab), con `decorationChance`,
  `decorationScale` y `decorationRing` (se colocan en anillo dejando libre el centro de la casilla).
- `themeName` / `themeLabelColor`: nombre flotante sobre la habitación (vacío = sin nombre).

Ejemplos con formas básicas en `Assets/Prefabs/Rooms/<Personaje>/` (creados por
**PPOP/Create Example Room Themes**, que solo rellena personajes sin habitación). Para tu arte final:
cambia materiales/modelos de `RoomTile_<Personaje>` y la lista `decorations` de `Room_<Personaje>`.
El tamaño de las habitaciones y la densidad de caminos salen de `Room_01` (asignado en `MapGenerator`).
En modo offline sin selección se usa `Room_01` en los 4 lados.

## Cartas especiales (moradas)

| Carta | Cuándo se usa | Efecto |
|---|---|---|
| Silencio líquido | Tu turno, antes de tirar (botón **USAR CARTA ESPECIAL**) | Durante 2 turnos (el actual y el siguiente), 85% de esquivar cada combate contra entidades (casillas de evento y "¡Pelea!" de la ruleta). El evento esquivado sigue en el tablero. |
| Cívica | Tu turno, en lugar de tirar | Te mueves exactamente 6 casillas en cualquier dirección atravesando paredes (sin salir del tablero). Pasar por un evento o un jugador sigue deteniendo el movimiento. |
| Comida Frita | Tu turno, antes de tirar | +1 vida, hasta el máximo inicial (`GameRules.startingLives`). Con las vidas llenas no se gasta. |
| Gatos fotocopiados | Fase de cartas de un duelo (atacante o defensor) | 65% de escapar: el duelo termina sin daño. Si falla, la carta se gasta y el duelo sigue. |
| Salchipapa | Pasiva | Al llegar a 0 vidas se consume sola y revives con 1 vida. |
| Canicas | Fase de cartas de un combate contra entidad | 65% de escapar sin daño (el evento no se consume). Si falla, se gasta y el combate sigue. |

- Máximo **una carta especial por turno** fuera de combate; "Sin cartas" de la ruleta también las bloquea.
- **Editable sin código:** nombre, descripción e imagen (`icon`) en `ScriptableObjects/Cards/Cards/Card_09..14_*`;
  probabilidad, casillas, vidas y turnos en `ScriptableObjects/Cards/Effects/Effect_Special_*`
  (`SpecialCardEffect`: `chance`, `amount`, `turns`). PPOP/Create Example Cards ya no sobrescribe cartas existentes.
- Si una carta no se puede usar en ese momento, el jugador recibe un aviso con cuándo se usa.

## Cámara

`BoardCameraController` (en la Main Camera de `Mapa`) mantiene el ángulo de la cámara y:
- al empezar encuadra todo el tablero; luego **sigue al jugador del turno** (todos ven la acción);
- en la carga y en la pantalla final vuelve a la **vista general**;
- **rueda del ratón** = zoom, **C** = alternar seguimiento / vista general.
Ajustes en el Inspector: `followDistance`, `minDistance`, `maxDistanceFactor`, `followOffset`
(desplaza el punto seguido para que la mano de cartas no tape el peón), `overviewMargin`, `smoothTime`.
Sin el componente, `MapGenerator.autoFrameCamera` encuadra el tablero como antes.

## Android (APK) y UI responsive

**Requisito:** en Unity Hub, módulo *Android Build Support* con *Android SDK & NDK Tools* y *OpenJDK*
para 6000.3.7f1 (necesita ~13 GB libres en el disco del Editor).

- `PPOP/Android/Apply Android Settings`: nombre "Pared", paquete `com.ppop.pared`, IL2CPP ARM64,
  solo horizontal, permiso de Internet, APK (no .aab). Lo aplican también los menús de build.
- `PPOP/Android/Build APK` → `Builds/Android/Pared.apk` (`Build APK (Development)` incluye el bot de pruebas).
  Desde código: `AndroidBuildSetup.Build(false, "D:/JuegoPared/APK1/Pared.apk")`.
- **Gráficos: solo OpenGL ES 3.** Con Vulkan el tablero (casillas URP/Lit) no se dibujaba en GPUs Mali
  (moto g20); fichas y UI sí. No volver a activar Vulkan sin probarlo en un móvil real.
- Depurar en un móvil: Depuración USB + `adb logcat -s Unity` (adb está en el SDK de Unity:
  `...\6000.3.7f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools`). Para probar una partida,
  el móvil crea la partida y el Editor se une con la IP del móvil (misma Wi-Fi).
- Instalar: copiar el APK al móvil y abrirlo (permitir "orígenes desconocidos"). Todos los móviles en la
  misma Wi-Fi; el Host ve su IP en el menú.
- En Android la app se pausa al pasar a segundo plano: si el Host minimiza el juego, los demás se desconectan.

**UI en cualquier pantalla** (sin tocar las escenas):
- `ResponsiveCanvas` se añade solo a todos los Canvas: CanvasScaler en modo *Expand* (nada se corta en
  20:9, 16:9 ni 4:3) y contenido dentro de la *safe area* (notch). Los fondos a pantalla completa quedan fuera.
- El HUD de partida está diseñado para caber en 1280×720 lógicos; en móviles la UI se agranda
  (`PhoneFactor` según el tamaño físico de la pantalla) y en tablets/monitores sobra espacio alrededor.
- `ScaleToFit` reduce bloques grandes que no caben (ruleta, minijuegos de misión, pantalla final).
  Si rediseñas la ruleta o un minijuego, se adaptan solos.
- Táctil: pellizco = zoom de cámara, botón VER TABLERO / SEGUIR TURNO (= tecla C), flechas grandes.
- Probar en el Editor: Game view a 2400×1080 y, en Play, `ResponsiveCanvas.ForcePhone = true`.

## Misiones por personaje (objetivo del juego)

- Cada habitación lateral es la de un personaje y tiene una **casilla de misión** al fondo, opuesta a
  la entrada (marcador con el color del personaje y el texto "MISIÓN"). Nunca tiene eventos encima.
- Tu misión está en la habitación de **tu** personaje (objetivo visible abajo en la lista de jugadores).
  Entrar en esa casilla detiene el movimiento y abre la misión (si hay otro jugador ahí, primero el duelo).
  Las casillas de misión de otros personajes no te afectan.
- **Completar** la misión gana la partida. **Fallar** termina el turno; puedes reintentar entrando otra vez.
- Ganar eventos ya no da la victoria (siguen dando su resultado normal).

**Datos** (`Assets/ScriptableObjects/Missions/Mission_<Personaje>.asset`, asignado en `CharacterData.mission`):
`title`, `description`, `icon`, `minigamePrefab` y `tileMarkerPrefab` (marcador 3D propio, opcional).

**Cómo añadir el minijuego de un personaje** (sin tocar red ni turnos):
1. Crea un prefab de UI (RectTransform) con un componente que herede de `MissionMinigame`.
2. Monta el juego en `OnBegin()`; llama `Finish(true)` si lo completa o `Finish(false)` si falla.
   `IsLocalPlayer` indica si es el jugador que juega la misión (los demás ven el mismo prefab como espectadores).
   `ShowResult(bool)` se llama en todos cuando el Host decide.
3. Asígnalo en `minigamePrefab` de su `MissionDefinition`. Mientras esté vacío se usa
   `PlaceholderMissionMinigame` (botones COMPLETAR / FALLAR para probar).

PPOP/Create Missions crea una misión provisional por personaje solo donde falte (no pisa tus cambios).

## Cómo añadir una carta

1. Clic derecho en `Assets/ScriptableObjects/Cards/Cards` > Create > PPOP > Cards > **Card Data**.
2. Rellena `id` (único), `displayName`, `type`, `description`, `icon` (opcional) y `effect` (opcional;
   sin efecto = carta que se consume sin hacer nada).
3. Añádela a la lista de `Assets/Resources/Cards/CardDatabase.asset`. Ya entra en el reparto.

**Nuevo tipo de carta:** Create > PPOP > Cards > **Card Type** (nombre, color y `frame` = sprite del diseño). No requiere código.

**Diseño de cartas** (`Assets/Resources/CardsDesign`): Daño = `CartaDamage`, Defensa = `CartaDefensa`,
Especial = `CartaEspecial`, Inútil = `CartaInutil` (asignados en el campo `frame` de cada `CardType`).
La carta de personaje (`CartaPersonaje`) es el fondo del prefab `CharacterCard`: cambia la imagen,
el nombre, la habilidad y los stats de cada personaje. Las zonas (ventana, banda, caja negra) están en
`Scripts/UI/CardLayout.cs`. Si cambias el arte, ejecuta **PPOP/Apply Card Designs**.
En la mano, la ventana muestra el `icon` de la carta si tiene, o su valor (+2, −2…) en grande.

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

## Efectos visuales y HUD

- **Dado animado:** al pulsar TIRAR DADO el Host decide la tirada al instante, pero todos ven un dado
  rodando en el centro de la pantalla durante `GameRules.diceAnimationSeconds` (1,3 s; 0 = sin animación)
  y solo después se abre el movimiento. `TurnManager.RollCount` / `DiceRolling` sincronizan la animación.
- **Efectos en el tablero** (`Scripts/Fx/GameFx`, solo visual y local en cada instancia):
  - Movimiento: salto por casilla con polvo al caer.
  - Turno: anillo del color del personaje bajo la ficha activa y número de pasos restantes encima.
  - Peligro: anillo rojo bajo un rival a 2 casillas o menos.
  - Duelo: chispas entre las fichas y temblor de cámara.
  - Evento: explosión morada y temblor de cámara; superado = chispas verdes, huida = azul.
  - Vidas: textos flotantes «-1 ♥» / «+1 ♥»; eliminación con nube gris.
  - Misión: destello dorado al empezar y confeti al completarla y al ganar.
- **Ambiente Backrooms (laberinto central):** `Board/BackroomsAmbience` en el objeto `MapGenerator` de Mapa.
  Tubos fluorescentes y halos de luz falsos, sin luces en tiempo real (una malla por material, 2 llamadas
  de dibujo). El parpadeo cambia colores de vértices y, de vez en cuando, baja un instante la luz principal.
  Usa luz cálida y fondo oscuro sin skybox, y muestra polvo flotante solo en equipos capaces.
  `quality = Auto` reduce las luces y quita el polvo en móviles con poca memoria o CPU.
  Se desactiva quitando el componente.
- **Arte de efectos:** `Art/Fx/` (texturas + materiales) y `Resources/FxConfig.asset`. Se regeneran con
  **PPOP → Fx → Build Fx Assets** (los PNG existentes no se pisan, así que se pueden reemplazar).
- **HUD:**
  - Banner corto («¡TU TURNO!» / «● TURNO DE X» con el color del personaje).
  - Contador grande de pasos, y flechas de movimiento solo mientras se puede mover.
  - Botón de carta especial solo si tienes una utilizable.
  - El botón que espera tu acción palpita.
  - Lista de jugadores compacta.
  - Combate con el objetivo y las tiradas en números grandes, abajo a la derecha para no tapar la ficha.

## Estructura de carpetas (Assets)

- `Art/`: modelos, texturas y materiales.
  - `Board/Nivel0/`: casilla del laberinto central (`nivel 0.fbx` + texturas).
  - `Rooms/`: modelos de las habitaciones laterales.
  - `Minigames/<Personaje>/`: PNG reemplazables de cada minijuego.
  - `Characters/Cards/`: arte de las cartas de personaje.
  - `Materials/`, `UI/`.
- `Prefabs/`: `Board/` (casillas, indicador de evento), `Rooms/` (habitaciones), `Players/`, `Minigames/`, `UI/`, `Network/`.
- `ScriptableObjects/`: `Characters/`, `Missions/`, `Cards/`.
- `Resources/`: assets que se cargan por nombre en tiempo de ejecución (`GameRules`, `RouletteConfig`, `CharacterCatalog`, cartas). No mover.
- `Scripts/`: `Board/` (generación del tablero), `Game/` (turnos, reglas en `Game/Rules`), `Players/`, `Missions/` (+ `Minigames/`), `Combat/`, `Cards/`, `Events/`, `Lobby/`, `Network/`, `UI/`, `Editor/` (menús PPOP).
- `Scenes/`, `Settings/` (URP), `Tests/`.

**Casilla del laberinto central:** `MapGenerator.centralTilePrefab` (escena `Mapa`) = `Prefabs/Board/Tile_Nivel0`,
generado desde `Art/Board/Nivel0/nivel 0.fbx` con **PPOP → Board → Build Central Tile (Nivel 0)** (una celda de
2×2: suelo + 4 muros que se encienden según el laberinto). Las habitaciones laterales siguen usando `tilePrefab`
como casilla lógica invisible.

## Arquitectura (resumen)

- `Scripts/Game/Rules/` (ensamblado `Pared.Rules`, sin red, testeable): `GameRules`, `TurnRotation`,
  `VictoryRules`, `CharacterSelectionRules`, `CombatContext`/`CombatResolver`. Datos de cartas en `Scripts/Cards/Data/`.
- `Scripts/Network/NetworkConnectionManager`: conexión IP, aprobación, sesión, `ReturnToMenu`.
- `Scripts/Lobby/LobbyManager` (prefab `NetworkSession`, persiste entre escenas): slots, personaje, LISTO, START.
- `Scripts/Game/GameStateManager` (objeto `GameNetwork` en `Mapa`): semilla, eventos, barrera de carga, spawn, victoria, desconexiones.
- `Scripts/Game/TurnManager`: máquina de estados, dado y movimiento validados en el Host.
- `Scripts/Combat/CombatManager`, `Scripts/Cards/CardManager`.
- `Scripts/Players/PlayerNetworkData`: fuente única de verdad por jugador (en `Prefabs/Players/Player.prefab`).
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
