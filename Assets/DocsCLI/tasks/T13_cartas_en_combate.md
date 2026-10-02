# T13 — Uso de cartas en combate
**Depende de:** T12 ✅ · **Tamaño:** M

> Antes de empezar: lee `docs/AGENT_CONTEXT.md` (§7), `docs/PROGRESS.md`. Verifica que T12 figura ✅. Trabaja SOLO en esta tarea.

## Objetivo
Durante el combate, el jugador activo puede usar una carta que modifica el resultado, o saltarse la fase de cartas y tirar.

## Qué hacer
1. **Fase de cartas:** al entrar en combate, `CombatManager` pasa a `CardPhase`. El jugador activo ve su mano clicable y tres acciones: `USAR CARTA`, `NO USAR CARTA` y `TIRAR DADO`.
2. **`RequestUseCard(handIndex/cardId)`** (ServerRpc). El Host valida: jugador activo, estado `CardPhase`, la carta está en su mano, no se supera `maxCardsPerCombat`.
3. **Efectos** (`CardEffect.Apply(CombatContext)`), implementa solo estos 3 de ejemplo y asígnalos a las cartas de T12:
   - `AddToRoll(n)`: suma `n` al resultado.
   - `ReduceRequired(n)`: baja el valor requerido en `n`.
   - `PreventLifeLoss`: evita perder la vida si fallas.
   Las cartas sin efecto (inútiles) se pueden usar y se consumen sin cambiar nada.
4. Al usarse: se aplica el efecto sobre el `CombatContext`, se **elimina de la mano** (sincronizado), todos ven `[Jugador] usó [Carta]` y el contador de cartas se actualiza.
5. `TIRAR DADO` (T10) usa el `CombatContext` modificado: `resultado = tirada + modificador` comparado con `requerido`. Muestra el desglose en el panel: `tirada + bonus = resultado vs requerido`.
6. Fuera de `CardPhase`, las cartas no se pueden usar (servidor rechaza). Los jugadores no activos ven su mano pero no interactúan.
7. Estructura extensible: añadir un efecto nuevo = una clase/asset nueva, sin modificar `CombatManager`.

## Fuera de alcance
Más efectos, robo de cartas, repetir tirada.

## Criterios de aceptación
- Usar `AddToRoll(2)` con tirada 3 y requerido 4 convierte la derrota en victoria, en ambas instancias.
- `PreventLifeLoss` evita perder la vida al fallar.
- La carta usada desaparece de la mano y no se puede reutilizar (ni con RPC duplicado).
- Un jugador no activo no puede usar cartas.

## Verificación
Compila; Host + clon; prueba cada efecto y el caso "no usar carta".

## Cierre
Aplica el cierre estándar (AGENT_CONTEXT §9).
