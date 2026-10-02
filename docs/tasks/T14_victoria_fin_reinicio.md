# T14 — Progreso, victoria, fin de partida y reinicio
**Depende de:** T13 ✅ · **Tamaño:** M

> Antes de empezar: lee `docs/AGENT_CONTEXT.md` (§6 y §7), `docs/PROGRESS.md` y, si existe, el apartado de puntos/misiones en `docs/PROJECT_AUDIT.md`. Verifica que T13 figura ✅. Trabaja SOLO en esta tarea.

## Objetivo
La partida termina con un ganador, se muestra la pantalla final a todos y se puede reiniciar o volver al menú.

## Qué hacer
1. **Progreso:** reutiliza el sistema de puntos/misiones existente si lo hay; si no, usa `Progress` de `PlayerNetworkData` (ya se incrementa en T10). Muestra el progreso de cada jugador en la lista (`2/3`).
2. **Victoria:** tras cada resolución de combate, el Host comprueba `Progress >= requiredEventsToWin` (editable en Inspector). También gana el último jugador `Alive` (`OnLastPlayerStanding` de T11).
3. **GameOver:** estado `GameOver` sincronizado, bloquea todas las acciones. Panel `PARTIDA TERMINADA` para todos con el ganador y mensaje `¡HAS COMPLETADO LA PARTIDA!` para él.
4. **Botones:**
   - `REINICIAR` (solo Host): resetea el estado y vuelve a la escena de selección de personaje o recarga `Game` (elige lo más simple y estable y documéntalo en `PROGRESS.md`). Debe limpiar vidas, progreso, manos, eventos y objetos spawneados.
   - `VOLVER AL MENÚ`: apaga la red limpiamente (`Shutdown`) y carga `MainMenu`. Un cliente puede volver al menú sin cerrar al Host.
5. Gestiona empates simultáneos de forma determinista (p. ej. gana el jugador activo; documéntalo).

## Fuera de alcance
Estadísticas, ranking, animaciones finales.

## Criterios de aceptación
- Al alcanzar el objetivo, ambas instancias muestran `PARTIDA TERMINADA` con el mismo ganador.
- `REINICIAR` deja el estado totalmente limpio y se puede jugar otra partida sin reiniciar el Editor.
- `VOLVER AL MENÚ` funciona en Host y Client sin errores de red en consola.

## Verificación
Compila; Host + clon; juega hasta ganar (baja `requiredEventsToWin` a 1 para probar rápido) y reinicia dos veces seguidas.

## Cierre
Aplica el cierre estándar (AGENT_CONTEXT §9).
