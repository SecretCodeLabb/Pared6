# T12 — Cartas: datos, reparto y mano
**Depende de:** T11 ✅ · **Tamaño:** M

> Antes de empezar: lee `docs/AGENT_CONTEXT.md` (§6 y §7, incluidos los tipos de carta), `docs/PROGRESS.md`. Verifica que T11 figura ✅. Trabaja SOLO en esta tarea.

## Objetivo
Cada jugador recibe 3 cartas aleatorias al empezar, sincronizadas, y el jugador local las ve en su mano. Todavía no se pueden usar.

## Qué hacer
1. **Datos** (`Scripts/Cards/`, `ScriptableObjects/Cards/`):
   - `CardType`: usa los tipos escritos en `AGENT_CONTEXT §7` (o los de defecto) de forma que añadir/quitar tipos no requiera tocar la lógica (enum simple o ScriptableObject).
   - `CardData : ScriptableObject`: `id`, `displayName`, `type`, `description`, `icon` (opcional), `effect` (referencia a `CardEffect`).
   - `CardEffect : ScriptableObject` abstracto, **solo la clase base** (los efectos reales llegan en T13).
   - `CardDatabase : ScriptableObject`: lista de `CardData` con búsqueda por `id`.
2. **Assets de ejemplo:** script de Editor `PPOP/Create Example Cards` que genere 6–8 `CardData` (incluye al menos una de tipo "inútil"/sin efecto).
3. **`CardManager`** (Host): al iniciar la partida reparte `startingHandSize` cartas aleatorias por jugador (`allowDuplicates` configurable, por defecto `true`) y las guarda en la mano del jugador. La mano se sincroniza por **ids de carta** (`NetworkList<int>`), visible para su dueño; el resto de jugadores ven solo el **número** de cartas (`HandCount`).
4. **UI de mano local:** panel `MIS CARTAS` con prefab simple de carta (fondo + texto: nombre, tipo, descripción; icono si existe). Reutiliza el Canvas del juego. Las cartas se pueden seleccionar/resaltar pero **aún no usar**.
5. En la lista de jugadores muestra cuántas cartas tiene cada uno.

## Fuera de alcance
Efectos, usar cartas en combate, robo de cartas.

## Criterios de aceptación
- Al empezar la partida cada jugador tiene exactamente `startingHandSize` cartas; el reparto lo decide el Host.
- El jugador local ve sus cartas; los demás ven solo el conteo.
- Añadir una `CardData` nueva al `CardDatabase` la incluye en el reparto sin tocar código.
- Añadir un tipo nuevo no requiere cambiar la lógica del juego.

## Verificación
Compila; Host + clon; verifica manos distintas y consistentes en ambas instancias.

## Cierre
Aplica el cierre estándar (AGENT_CONTEXT §9).
