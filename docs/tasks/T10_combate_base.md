# T10 — Combate base
**Depende de:** T09 ✅ · **Tamaño:** M

> Antes de empezar: lee `docs/AGENT_CONTEXT.md` (§6 y §7), `docs/PROGRESS.md`. Verifica que T09 figura ✅. Trabaja SOLO en esta tarea.

## Objetivo
Un combate simple: el jugador activo tira un dado y se compara con `requiredCombatRoll`. Sin cartas todavía.

## Qué hacer
1. Crea `CombatManager` (`Scripts/Combat/`, `NetworkBehaviour`). Estados internos: `Rolling → Resolving → Won/Lost`. Reserva el estado `CardPhase` (se usa en T13) para entrar a `Rolling` sin romper nada.
2. Variable editable: `[SerializeField] private int requiredCombatRoll = 4;` (nunca el 4 fijo en el código).
3. **Panel de combate** (todos lo ven; solo el activo tiene botón): mensaje de amenaza, valor requerido y botón `TIRAR DADO DE COMBATE`. `RequestCombatRoll()` ServerRpc: valida jugador activo + estado; el **Host** genera 1–6 y evalúa.
4. Crea `CombatContext` (tirada, modificador, requerido, evitarDaño) y evalúa con él, para que T13 pueda modificarlo sin reescribir.
5. **Resultado:**
   - `>= requerido` → `¡SUPERASTE EL EVENTO!`; `Progress += 1`; consume el evento si `consumeOnSuccess`.
   - `< requerido` → `FALLASTE EL EVENTO`; `Lives -= 1` (solo decremento en Host sobre `PlayerNetworkData`; UI y eliminación llegan en T11).
6. Tras mostrar el resultado unos segundos (configurable), pasa a `TurnEnd` y siguiente jugador.
7. **Elimina el stub `CONTINUAR`** (`// TEMP-T10`) de T09.
8. Durante `Combat` no se puede mover ni tirar el dado de movimiento.
9. Protege contra doble tirada de combate.

## Fuera de alcance
UI de vidas completa, eliminación, cartas, condición de victoria.

## Criterios de aceptación
- El resultado y el mensaje son idénticos en ambas instancias.
- Cambiar `requiredCombatRoll` en el Inspector cambia el umbral sin tocar código.
- Ganar suma progreso; perder resta 1 vida en `PlayerNetworkData`.
- Un cliente no activo no puede tirar el dado de combate.

## Verificación
Compila; Host + clon; fuerza varios combates cambiando temporalmente `requiredCombatRoll` a 1 y a 6.

## Cierre
Aplica el cierre estándar (AGENT_CONTEXT §9).
