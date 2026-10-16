# Pared — resumen para retomar (estado al 2026-10-15)

Leer esto primero en una sesión nueva. Detalle extra: `docs/AGENT_CONTEXT.md` (reglas de trabajo), `docs/PROGRESS.md`, `docs/README_PROTOTIPO.md`.
El usuario escribe en español: responder en español.

## Proyecto
- Unity 6000.3.7f1, URP, juego de tablero **LAN multijugador** (Netcode for GameObjects, host autoritativo). Repo GitHub `SecretCodeLabb/Pared6`, rama `main`.
- Escenas: `LobbyScene` → `CharacterSelection` → `Mapa`.
- Código en `Assets/Scripts/` por carpetas: Board, Cards, CharacterSelection, Characters, Combat, Editor (setups de menú `PPOP/...`), Events, Fx, Game (+`Game/Rules` = lógica pura testeable, asmdef `Pared.Rules`), Lobby, Missions, Network, Players, UI.
- Config central: `Assets/Resources/GameRules.asset` (`GameRules.cs`), ruleta en `Resources/RouletteConfig`, cartas en `Resources/Cards/CardDatabase.asset`.
- `docs/` es la fuente de verdad; `Assets/DocsCLI/` es una copia (sincronizar al editar docs).

## Mecánicas
**Partida:** 2–4 jugadores, 3 vidas cada uno, mano inicial de 3 cartas (con duplicados). Turnos rotativos (salta eliminados/desconectados).

**Tablero:** laberinto central generado por semilla (determinista, igual en todos) + un **cuarto por personaje** a los lados, cada uno con una puerta y una casilla **MISIÓN** al fondo. Ambiente "Backrooms" (tubos fluorescentes).

**Turno:** tirar dado (1–6, animación 1.3 s) → mover tocando las casillas verdes (`TapMoveController`) → según dónde termine:
- **Casilla de evento** (5 en el tablero; se activa al pasar por encima): combate contra una **entidad**.
- **Casilla con otro jugador vivo**: **duelo**.
- Si no hubo combate: **ruleta**.

**Combate contra entidad:** resultado = dado + modificadores. Gana si resultado ≥ número de la entidad (por defecto 4). Si pierde: −1 vida (`damageOnFail`). Máx. 1 carta por combate; la carta se consume. Ganar da +1 de progreso.

**Duelo:** ambos tiran; resultado = dado + cartas propias − defensa del rival. Mayor gana; el perdedor pierde 1 vida; empate = nadie pierde.

**Cartas** (19 en el mazo; `CardData`: id, displayName, type, description, icon, **fullArt**, effect):

| Tipo | Cartas | Efecto |
|---|---|---|
| Daño | Golpe +1/+2/+3 | Suma N a tu resultado (`AddToRollEffect`) |
| Defensa | Video defensa personal +1, Jabón Rey +1, Señal de pare +2, Silla Rimax +2, Emulsión Scott +3 | **Resta N al número del rival**: su resultado en duelo o el número de la entidad (`DefenseEffect` → `CombatContext.RivalReduction`) |
| Inútil | Agua estancada, Autoayuda, Trompo, VIP gimnasio, Diploma | Sin efecto |
| Habilidad (morada) | Silencio líquido (85% evitar combates con entidades 2 turnos), Cívica (mover exactamente 6 atravesando paredes), Comida frita (+1 vida), Gatos fotocopiados (65% escapar de duelo), Salchipapa (revive con 1 vida al morir), Canicas (65% escapar de entidad) | `SpecialCardEffect` + `CardRules` (cuándo se pueden usar) |

- Defensa e inútiles usan `fullArt` (`Assets/Art/Cards/Defensa|Inutiles`, 600×825): la imagen ya trae nombre y texto, la mano dibuja **solo la imagen**. Las demás cartas usan marco por tipo + nombre + descripción generados.
- Efectos disponibles sin usar en el mazo: `ReduceDamageEffect`, `ReduceRequiredEffect`, `PreventLifeLossEffect`.

**Ruleta** (peso): Nada 20, +1/+2/+3 MOV (10/7/4), −1/−2/−3 en próxima tirada (8/6/4), Pierdes turno 5, ¡Pelea! (entidad ataca) 8, Sin cartas la siguiente ronda 6, −1/−2 "daño" (= resta a tus tiradas de combate hasta fin del siguiente turno) 6/4, −1 vida 2.

**Personajes** (`CharacterData`): Chismosa, Estudiante, Puppy, Skater, Trabajador cansado, Vagabundo.
- **Habilidad de personaje**: botón HABILIDAD a la derecha de la carta del personaje (HUD, encima del contador de pasos; color del personaje, pulsa cuando se puede usar). Datos en `CharacterData.ability/abilityAmount/abilityFailChance`, reglas puras en `Game/Rules/CharacterAbilityRules.cs`, servidor en `TurnManager.RequestUseAbilityRpc` (1 vez por turno, `AbilityUsed`).
- Hecha: **Skater** (`SkaterSprint`): tras tirar y con pasos restantes, +2 casillas; 65% de fallar → "¡Habilidad especial falló! Pierdes tu siguiente turno" (sigue moviéndose con lo que tenía, `SkipTurns`+1). Éxito → "¡Lograste avanzar 2 casillas más!".
- Hecha: **Estudiante** (`StudentEscape`, "Scroll time"): botón **ESCAPAR** en el panel VS (`CombatVsView`, verde azulado), solo contra entidades (nunca en duelos) y antes de tirar. Termina el combate sin daño (fase `Escaped`, el evento sigue en su casilla). Cada escape suma `abilityStackChance` (25%) a `PlayerNetworkData.EscapedCombats` → probabilidad acumulada de que el siguiente combate contra entidad sea **doble** (`CombatManager.EntityCount/EntityRound`: 2 combates seguidos en la misma casilla, título "¡2 ENTIDADES! (1/2)"). En la pelea doble no se puede escapar; al salir doble el contador vuelve a 0. El panel muestra "ESCAPAR: riesgo de 2 entidades seguidas X% → Y%".
- `UsesTurnButton` decide qué habilidades usan el botón junto a la carta (solo Skater); el resto vive en el panel de combate.
- Pendientes: Chismosa, Puppy, Trabajador cansado, Vagabundo (por ahora solo texto). Para añadir una: valor nuevo en `CharacterAbility`, casos en `CanUse`/`Resolve`, aplicar en `RequestUseAbilityRpc`, tests en `CharacterAbilityTests`.

**Misiones / victoria:** llegar a la casilla MISIÓN del cuarto de tu personaje y superar su minijuego → gana. También gana el último en pie. Minijuegos hechos:
- Estudiante "Invasión en el aula" (`StudentInvadersMinigame`), Puppy "Escape de la fiesta" (`PuppyCatchMinigame`), Trabajador cansado "Llegar al viernes" (`WorkerRunnerMinigame`).
- Chismosa, Skater, Vagabundo: sin minijuego (placeholder).

**HUD** (`GameHUD`, `CombatVsView`): carta del personaje con vidas abajo-izquierda, mano de cartas abajo, panel VS centrado en combate (muestra números ya con cartas aplicadas y chips "+N carta", "-N por defensa", "ESCUDO"), MENÚ con salida confirmada, botón TIRAR DADO centrado.

## Herramientas y pruebas
- **Editor abierto** (no usar `-batchmode`): CLI `C:/Users/USER/AppData/Local/Unity/bin/unity.exe` → `status`, `recompile --json --timeout 300` (si da "400 Bad Request": esperar a `status` ready y reintentar), `command build_status`. MCP `unity` (`eval`, `run_tests`, `build`; cargar con ToolSearch).
- **Tests:** 74 EditMode, todos pasan (`mcp__unity__run_tests mode=editor`).
- **Build Windows de pruebas:** `Builds/Dev/Pared.exe` (Development). Partida automática con dos instancias:
  - host: `-autohost -autoplay -players 2 -games 1 -quitonmenu -screen-fullscreen 0 -screen-width 960 -screen-height 540 -logFile host.log`
  - cliente: `-autojoin 127.0.0.1 -autoplay ...`
  - extras: `-character Nombre`, `-hardrules`, `-demohand` (abre cartas de la mano), `-tapmove`, `-useability` (usa la habilidad del personaje siempre que puede), `-slowcombat` (el bot espera 3 s en cada combate, para capturas del panel VS).
  - Los logs muestran `plays combat card X`, `Combate ...`, `Duelo ...`.
- Capturas sin robar foco: script `winshot.ps1` (estaba en el scratchpad de la sesión; si falta, recrearlo con PrintWindow).
- **Android:** APK en `D:\JuegoPared\APK1\Pared.apk` (`AndroidBuildSetup.Build` cambia de plataforma y compila). Móvil moto g20 por adb (`.../AndroidPlayer/SDK/platform-tools/adb.exe`); IP del PC 192.168.2.83. El tablero invisible en Android era el SSAO del `PC_Renderer` (desactivado) — falta confirmarlo en el móvil.

## Estado / pendiente
- Commits locales sin push: `6248477` (movimiento táctil), `73fb404` (minijuego Puppy), `52a9821` (HUD).
- **Sin commit:** reemplazo de cartas de defensa/inútiles (arte `fullArt`, nuevas cartas id 15–20), regla nueva de defensa (`DefenseEffect`, `RivalReduction`, efectos `Effect_Defense_1..3`), chips del VS, tests nuevos, docs. `ProjectSettings.asset` se deja fuera a propósito (cambio automático de Unity).
- Sin seguimiento a propósito: `Pared6/`, `_Recovery`, `Referencia`.
- Por hacer: push (solo si el usuario lo pide), generar APK nuevo y probar en el móvil, minijuegos de Chismosa/Skater/Vagabundo, habilidades de los demás personajes.
- Hay actualización disponible de Unity CLI (beta.12 → beta.13): preguntar antes de `unity self-update`.
