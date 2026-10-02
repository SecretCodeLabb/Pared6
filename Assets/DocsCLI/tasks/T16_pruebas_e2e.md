# T16 — Pruebas end-to-end
**Depende de:** T15 ✅ · **Tamaño:** M

> Antes de empezar: lee `docs/AGENT_CONTEXT.md`, `docs/PROGRESS.md`. Verifica que T15 figura ✅. Trabaja SOLO en esta tarea.

## Objetivo
Validar la partida completa de principio a fin y dejar el prototipo documentado y estable.

## Qué hacer
1. **Tests automáticos (EditMode)** para la lógica pura (ya separada del transporte): rotación de turnos (con eliminados/desconectados), resolución de combate con `CombatContext`, efectos de carta, unicidad de personajes, condición de victoria. Si algo no es testeable sin red, haz el refactor mínimo necesario. Ejecútalos desde el CLI (`-runTests -testPlatform EditMode`) o indica cómo.
2. **`docs/TEST_PLAN.md`:** checklist manual con la prueba de aceptación:
   1. Instancia 1: `CREATE GAME`. Instancia 2: IP + `JOIN GAME`.
   2. Ambos en la escena de selección; eligen personajes distintos; `LISTO`.
   3. Host pulsa `START`; ambos entran a `Game` y ven el mismo tablero.
   4. Cada jugador tiene 3 vidas y 3 cartas; se muestra el jugador inicial.
   5. J1 tira, mueve y termina en casilla normal → pasa el turno.
   6. J2 termina en `EventTile` → combate, usa una carta, tira, compara con `requiredCombatRoll`.
   7. Gana → +1 progreso; pierde → −1 vida visible en ambos.
   8. El turno pasa y se sigue jugando.
   9. Al alcanzar `requiredEventsToWin` aparece la pantalla final en ambos.
   10. Volver al menú sin errores; reiniciar y jugar otra partida.
3. Ejecuta lo que puedas desde el CLI y marca el resultado de cada punto (✅/❌/no verificable) en `TEST_PLAN.md`. Lo que requiera dos Editores abiertos me lo dejas como checklist para mí.
4. **Corrige** los fallos de poca complejidad que encuentres; lo demás anótalo en `PROGRESS.md` > Problemas abiertos.
5. Crea `docs/README_PROTOTIPO.md`: cómo abrir con ParrelSync, flujo de partida, valores configurables y dónde, cómo añadir una carta nueva y un efecto nuevo, limitaciones conocidas.

## Criterios de aceptación
- Los tests EditMode pasan.
- `TEST_PLAN.md` completo con resultados reales.
- `README_PROTOTIPO.md` suficiente para que otra persona pruebe y amplíe el prototipo.

## Cierre
Aplica el cierre estándar (AGENT_CONTEXT §9) y entrega un resumen final: qué funciona, qué no y qué recomiendas hacer después.
