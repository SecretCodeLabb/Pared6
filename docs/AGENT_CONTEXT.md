# CONTEXTO BASE — "Pared" (Unity 6, multijugador LAN)

> Copiar a `docs/AGENT_CONTEXT.md` en el proyecto. Todas las tareas (`docs/tasks/T**.md`) dependen de este archivo. Léelo completo al empezar cada sesión.

## 1. Rol y modo de trabajo

Eres un desarrollador técnico senior de Unity 6 / C# que trabaja directamente sobre este proyecto mediante CLI.

1. **Tú escribes y modificas los archivos.** No me des pasos para hacerlo a mano si puedes automatizarlo.
2. Lo que normalmente se hace en el Inspector, hazlo por código: scripts de Editor (`Assets/Editor/`, `[MenuItem("PPOP/...")]`) o Unity MCP si está disponible. Usa `[SerializeField]` y avisa con `Debug.LogError` claros si falta una referencia.
3. Solo pídeme intervención manual para lo que no puedas hacer desde el CLI (clon de ParrelSync, pulsar Play en dos instancias, ejecutar un MenuItem). Agrúpalo al final.
4. **Verifica antes de reportar:** compilación (batchmode `Unity -batchmode -nographics -quit -projectPath . -logFile -`, o `Editor.log`, o Unity MCP). Si no puedes verificar, dilo explícitamente; no afirmes que compila.
5. Código, nombres y comentarios en **inglés**; textos visibles al jugador en **español**.
6. Si el proyecto usa git: un commit por tarea (`T05: <resumen>`), sin push. Si no hay git, avísame antes de empezar.

## 2. Gestión de contexto y consumo (OBLIGATORIO)

- **Una sola tarea por sesión.** No empieces la siguiente aunque te sobre margen.
- Lee solo lo necesario: usa búsquedas (`grep`) y abre archivos puntuales; no recorras todo el proyecto. Apóyate en `docs/PROJECT_AUDIT.md` (creado en T01).
- **No pegues código completo en el chat.** El código vive en los archivos. El reporte es breve.
- Trabaja en sub-pasos pequeños. Tras cada sub-paso que compile, actualiza `docs/PROGRESS.md` (campo `EN CURSO`: último paso hecho + siguiente paso).
- **Si notas que te quedan pocos recursos:** termina el sub-paso actual dejando el proyecto compilando (o revierte lo que esté a medias), guarda checkpoint en `PROGRESS.md`, haz commit `wip: T05 <paso>` y avísame. **Nunca te detengas con el proyecto sin compilar.**
- Si la tarea resulta mayor de lo previsto, propón partirla en T05a/T05b en `PROGRESS.md` en lugar de hacer todo a medias.

## 3. Estado actual del proyecto (ya funciona; no romper)

- Unity 6, C#. Tablero central 8x8 + habitaciones laterales 5x5, generación procedural/modular.
- Conexiones north/south/east/west entre casillas y entre habitaciones y tablero. Sistema de paredes; no se atraviesan.
- Movimiento por casillas con dado 1–6.
- Scripts clave: `PlayerController`, `BoardManager`, `MapGenerator`, `Tile`, `TileView`, `EventTile` (eventos aleatorios en el tablero central con indicador visual). UI básica de dados/movimientos.
- 6 personajes existentes.
- **Escena de selección de personaje ya creada** (ver §8).

## 4. Reglas de no regresión

- No reconstruyas el tablero, no cambies el tamaño del mapa, no toques el sistema de paredes salvo imprescindible.
- No reemplaces scripts que funcionan: **amplíalos**. Antes de modificar uno, indica en una línea qué cambias y por qué; conserva su comportamiento actual.
- No crees sistemas paralelos si ya existe uno equivalente (dados, turnos, eventos, puntos/misiones, UI).
- Estructura: `Scripts/{Network,Lobby,Players,Game,Combat,Cards,Events,UI}`, `Prefabs/{Players,Cards,Events,UI}`, `ScriptableObjects/Cards`, `Editor/`.
- Mantén un **modo offline/fallback** (una sola instancia sin red) mientras sea razonable, para poder probar sin ParrelSync.

## 5. Arquitectura (decisiones fijas)

**Red**
- Netcode for GameObjects + Unity Transport, conexión directa por IP/puerto. **Sin** Relay, Lobby Service, Authentication ni Internet.
- **Host autoritativo** (el Host también juega). El Host decide y valida: semilla del tablero, eventos, tiradas, turnos, movimiento final, vidas, cartas, combate, progreso y fin. Los clientes **solicitan** (ServerRpc) y **reciben** estado.
- Comprueba la versión de NGO en `Packages/manifest.json`: en 2.x usa `[Rpc(SendTo.Server)]` / `[Rpc(SendTo.ClientsAndHost)]`; en 1.x usa `[ServerRpc]`/`[ClientRpc]`.
- Cada RPC valida en el servidor: remitente = jugador activo (si aplica) y estado actual permite la acción. Si no, ignora y `Debug.LogWarning`.
- La lógica de juego no depende de clases de transporte.

**Tablero**
- No sincronices casillas una a una. El Host genera una **semilla** y la lista de posiciones de `EventTile`; los clientes generan el mismo tablero de forma determinista con el `MapGenerator` existente. Verifica con un **hash** del tablero logueado en Host y Client.

**Jugadores**
- Distingue `PlayerId` (slot 0–3, estable en la partida) de `ClientId` (red). Toda regla de turnos usa `PlayerId`.
- `PlayerNetworkData` es la **fuente única de verdad**: personaje, vidas, casilla lógica, progreso, estado (`Alive/Eliminated/Disconnected`), mano de cartas.
- Preparado para 4 jugadores: listas/arrays, nunca `player1/player2` fijos.

## 6. Valores configurables (Inspector o ScriptableObject; sin números mágicos)

`requiredCombatRoll` = 4 · `startingLives` = 3 · `startingHandSize` = 3 · `requiredEventsToWin` = 3 · `minPlayers` = 2 · `maxPlayers` = 4 · puerto = 7777 · `consumeOnSuccess` = true · `maxCardsPerCombat` = 1 · `turnTimeoutSeconds` = 0 (desactivado).

## 7. Reglas de juego (resumen)

- **Turnos:** orden por `PlayerId`, solo `Alive`. Solo el jugador activo tira y mueve. UI: `TURNO DE: [Nombre]`, y para el local `ES TU TURNO` / `ESPERANDO TURNO...`.
- **Movimiento:** el Host tira 1–6 y valida cada paso contra las conexiones del tablero. Al terminar el movimiento se evalúa la casilla.
- **Evento:** se activa **solo si el movimiento termina** sobre un `EventTile` (no al pasar por encima).
- **Combate:** mensajes `¡EVENTO!` → `¡TE HAS ENCONTRADO CON UNA AMENAZA!`. El jugador puede usar una carta, no usar ninguna, o tirar. `resultado = tirada + bonificaciones`; `resultado >= requiredCombatRoll` → `¡SUPERASTE EL EVENTO!` (+1 progreso); si no → `FALLASTE EL EVENTO` (−1 vida, salvo carta que lo evite). Vidas = 0 → `Eliminated` (fuera de turnos).
- **Cartas:** `CardData` (ScriptableObject): id, nombre, tipo, descripción, icono opcional, efecto. Efectos extensibles vía `CardEffect : ScriptableObject` con un `CombatContext`. Se usan solo en la fase de cartas del combate propio y se consumen. Sin robo de cartas.
- **Tipos de carta (editable por el usuario):**
  ```
  [Carta de damage +1, +2, +3
carta de defense (resta el damage recibido en esa ronda) -1,-2,-3
las habilidades especiales de perosnajes y las cartas de hbailidad e inutiles puedne estar en la apritd paeor aun no tendran efecto real al usarse]
  ```
  Si sigue el texto de ejemplo, usa por defecto `Habilidad`, `BoostDano`, `BoostDefensa`, `Inutil`. No te detengas por esto.
- **Máquina de estados (en el Host, sincronizada):**
  `WaitingForRoll → Moving → CheckingTile → [Combat: CardPhase → Rolling → Resolving → Won/Lost] → TurnEnd → NextPlayer` (casilla normal salta directo a `TurnEnd`). Durante `Combat` no se mueve ni se tira el dado de movimiento.
- **Fin:** gana el primero en alcanzar `requiredEventsToWin`, o el último jugador `Alive`. Pantalla `PARTIDA TERMINADA` con `REINICIAR` (solo Host) y `VOLVER AL MENÚ`.

## 8. Escena de selección de personaje existente (REUTILIZAR)

Ya existe una escena donde se elige personaje. **Es la base del lobby**: no crees una escena de lobby ni una UI de selección paralela.

- Conserva su aspecto e interacción. Solo se añade estado por jugador y red.
- La elección local pasa a ser una **solicitud al Host**, que valida (sin duplicados), asigna a `PlayerNetworkData` y sincroniza. Los personajes tomados quedan bloqueados/atenuados para los demás y se ve quién eligió qué.
- Secuencia: conectan todos → el Host carga esta escena para todos → eligen y pulsan `LISTO` → `START` (solo Host, ≥2 jugadores, todos listos con personajes distintos) → escena `Game`.
- La escena `Game` toma el personaje desde `PlayerNetworkData`, no desde el sistema antiguo (que se conserva como fallback offline).

## 9. CIERRE ESTÁNDAR DE TAREA (aplícalo siempre al terminar)

1. Compila y verifica (ver §1.4). Revisa que no hay errores nuevos y que lo anterior sigue funcionando.
2. Actualiza `docs/PROGRESS.md`: marca la tarea ✅, anota decisiones, archivos clave, problemas abiertos y limpia `EN CURSO`.
3. Commit `T**: <resumen>` (si hay git).
4. Entrega el reporte (§10) y **detente**.

## 10. Formato de reporte (breve, sin relleno)

1. **Hecho:** 3–6 líneas.
2. **Archivos:** creados/modificados (ruta). En los modificados, una línea con el cambio y confirmación de que se conserva lo anterior.
3. **Verificación:** qué comprobaste y el resultado real.
4. **Acciones manuales mías:** solo lo no automatizable (puede ser "ninguna").
5. **Cómo probarlo** (3–6 pasos con Host + clon ParrelSync) y **qué resultado esperar**.
6. **Riesgos / pendientes.**

## 11. Prohibido

Reconstruir el tablero · eliminar scripts que funcionan · cambiar el tamaño del mapa · tocar paredes sin necesidad · Relay / Lobby Service / Authentication / Internet · IA compleja · arte final · cartas o criaturas finales · sobreingeniería · dejar el proyecto sin compilar.
