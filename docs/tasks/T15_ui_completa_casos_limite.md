# T15 — UI completa y casos límite
**Depende de:** T14 ✅ · **Tamaño:** M

> Antes de empezar: lee `docs/AGENT_CONTEXT.md`, `docs/PROGRESS.md`. Verifica que T14 figura ✅. Trabaja SOLO en esta tarea.

## Objetivo
HUD coherente y robusto, y manejo de situaciones anómalas de red/juego.

## Qué hacer
1. **HUD unificado** (amplía el `UIManager` existente o crea `GameHUD`): turno actual, estado local (`ES TU TURNO`/`ESPERANDO TURNO...`), vidas propias y de todos, dado y movimientos, mano de cartas, panel de combate, progreso y mensajes de estado/error de red.
2. **Botones habilitados por estado:** `TIRAR DADO`, `TIRAR DADO DE COMBATE`, `USAR CARTA`, etc. se habilitan/deshabilitan según el estado y el jugador activo (no basta con ocultarlos). Elimina los botones de depuración (`#if UNITY_EDITOR`, `TEMP`) que ya no hagan falta.
3. **Desconexión de un cliente en partida:** marca `Disconnected`, pasa el turno si era el suyo, libera sus cosas; continúa si quedan ≥2; si queda 1, ese gana (reutiliza T14).
4. **Desconexión del Host:** los clientes vuelven al menú con mensaje claro y la red apagada.
5. **Entrada tardía:** una conexión con la partida ya iniciada se rechaza (connection approval).
6. **RPC duplicados / spam:** tirar dos veces, usar la misma carta dos veces o pulsar `START` dos veces no duplica efectos.
7. **Timeout de turno opcional** (`turnTimeoutSeconds`, 0 = desactivado): si se activa, pasa el turno automáticamente al agotarse; muestra cuenta atrás.
8. Revisa que no queden `// TEMP`, `Debug.Log` ruidosos ni referencias sin asignar.

## Fuera de alcance
Arte final, animaciones, nuevas mecánicas.

## Criterios de aceptación
- Cada botón solo es utilizable cuando corresponde, en ambos jugadores.
- Desconectar al cliente en mitad de la partida no bloquea al Host.
- Cerrar el Host devuelve al cliente al menú sin excepciones.
- No hay errores en consola durante una partida completa.

## Verificación
Compila; Host + clon; fuerza desconexiones en distintos momentos (turno propio, combate, fin de partida).

## Cierre
Aplica el cierre estándar (AGENT_CONTEXT §9).
