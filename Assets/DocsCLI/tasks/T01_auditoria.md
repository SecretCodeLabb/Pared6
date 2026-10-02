# T01 — Auditoría del proyecto
**Depende de:** nada · **Tamaño:** M · **Modifica código:** NO

> Antes de empezar: lee `docs/AGENT_CONTEXT.md` y `docs/PROGRESS.md`. Trabaja SOLO en esta tarea.

## Objetivo
Entender el proyecto real para que las tareas siguientes reutilicen lo existente. Entregable: `docs/PROJECT_AUDIT.md` y `docs/PROGRESS.md` creado (copia la plantilla si no existe).

## Qué hacer
Inspecciona y documenta, de forma concisa:

1. **Paquetes** (`Packages/manifest.json`): NGO, Unity Transport, Input System (nuevo/antiguo), TextMeshPro, ParrelSync. Versión de Unity. Lo que falta.
2. **Escenas y Build Settings:** lista de escenas, cuáles están en build, cuál es la de menú, la de selección de personaje y la de juego.
3. **Tablero:** cómo se conectan `BoardManager`, `MapGenerator`, `Tile`, `TileView`. **¿La generación es determinista?** Busca usos de `Random`/`UnityEngine.Random`/`System.Random` y di qué haría falta para aceptar una semilla.
4. **Jugadores:** `PlayerController` y cómo/dónde `BoardManager` crea y posiciona jugadores; cómo se representa la posición (coordenadas lógicas vs transform).
5. **EventTile:** cómo se generan los eventos y **cuándo se detecta** el evento (¿al pasar o al terminar?).
6. **Dados y UI:** scripts del dado de movimiento, movimientos restantes y la UI actual.
7. **Escena de selección de personaje** (AGENT_CONTEXT §8): escena, GameObjects/Canvas/botones y `OnClick`; scripts que la controlan; **dónde se guarda la selección** (estática, ScriptableObject, `PlayerPrefs`, `DontDestroyOnLoad`...); **cómo la lee el tablero**; datos de cada personaje y de dónde salen; qué asume de un único jugador local.
8. **Sistemas ya existentes** que podrían solaparse: turnos, vidas, puntos/misiones, cartas, menú.
9. **Plan de cambios por tarea (T02–T16):** para cada una, qué scripts se amplían, cuáles se crean y qué riesgos hay.
10. **Riesgos y preguntas abiertas** (máximo 3 preguntas realmente imprescindibles para mí).

## Fuera de alcance
Modificar código, instalar paquetes, crear escenas.

## Criterios de aceptación
- `docs/PROJECT_AUDIT.md` cubre los 10 puntos con rutas de archivo concretas.
- Responde explícitamente: determinismo del generador, momento de activación del evento y mecanismo de guardado de la selección de personaje.
- `docs/PROGRESS.md` existe.

## Cierre
Aplica el cierre estándar (AGENT_CONTEXT §9). El reporte puede ser un resumen de 10 líneas + las preguntas abiertas.
