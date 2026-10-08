# TEST PLAN — "Pared" prototipo LAN

Resultados de la sesión del 2026-10-02. Leyenda: ✅ verificado · ❌ falla · ⬜ pendiente (manual).

**Cómo se probó:** Editor = Host. Clientes = build de desarrollo de Windows (`Builds/Dev/Pared.exe`)
controlado por `DevAutoPlayer` (`-autojoin 127.0.0.1 -autoplay`), que usa exactamente las mismas
solicitudes que la UI. El Host se manejó desde el Editor (CLI/MCP). Además, partidas de 1 jugador
en el Editor (con `minPlayers = 1` temporal) para forzar combates, victoria y eliminación.

## Tests automáticos (EditMode)

`Assets/Tests/EditMode/RulesTests.cs` — **26/26 ✅**. Cubre rotación de turnos (2/3/4 jugadores,
eliminados, desconectados, activo eliminado), victoria (objetivo, empate → activo, último en pie),
unicidad de personajes y condiciones de START, resolución de combate con `CombatContext`
y los efectos de carta (`AddToRoll`, `ReduceRequired`, `ReduceDamage`, `PreventLifeLoss`, carta inútil),
y reparto de manos.

Ejecutar: Window > General > Test Runner > EditMode > Run All, o
`unity test --mode EditMode` (con el Editor cerrado) / `unity command run_tests --mode editor`.

## Prueba de aceptación (checklist)

| # | Paso | Resultado |
|---|---|---|
| 1 | Instancia 1: CREAR (Host). Instancia 2: IP + UNIRSE | ✅ cliente conecta (`Conectado con éxito al Host`) |
| 2 | Ambos en la escena de selección; eligen personajes distintos; `LISTO` | ✅ slots sincronizados, cartas tomadas atenuadas con el nombre del dueño |
| 3 | Host pulsa `START`; ambos entran a `Game` y ven el mismo tablero | ✅ hash igual en Host y Cliente en 2 partidas (173586612, 558691888); segundo START ignorado |
| 4 | Cada jugador tiene 3 vidas y 3 cartas; se muestra el jugador inicial | ✅ HUD: ♥♥♥, 3 cartas, toast "Comienza Jugador 1" |
| 5 | J1 tira, mueve y termina en casilla normal → pasa el turno | ✅ paredes rechazan el paso sin gastar movimiento; turno pasa solo |
| 6 | J2 termina en EventTile → combate, usa carta, tira y compara con `requiredCombatRoll` | ✅ (cliente usó carta, `5 + 3 = 8 vs 4`) |
| 7 | Gana → +1 progreso; pierde → −1 vida visible en ambos | ✅ evento consumido en todas las instancias; derrota resta vida y a 0 → ELIMINADO |
| 8 | El turno pasa y se sigue jugando | ✅ 11+ turnos alternos sin desincronización |
| 9 | (Antes: al alcanzar `requiredEventsToWin`.) Desde T24 se gana completando la misión del personaje; la pantalla final aparece en ambos | ✅ `PARTIDA TERMINADA`, mismo ganador, `¡HAS COMPLETADO LA PARTIDA!` para él |
| 10 | Volver al menú sin errores; reiniciar y jugar otra partida | ✅ REINICIAR → selección (LISTO reseteado) → 2ª partida; VOLVER AL MENÚ → Host puede volver a crear |

## Casos límite

| Caso | Resultado |
|---|---|
| 3 jugadores (Host + 2 clientes) | ✅ lobby y START con 3 personajes distintos |
| Entrada tardía con la partida iniciada | ✅ rechazada: "Partida ya iniciada." |
| Cliente se cae en mitad de la partida | ✅ marcado DESCONECTADO tras el timeout del transporte (~30 s); queda 1 → gana el Host |
| Host cierra la partida | ✅ cliente vuelve al menú con mensaje "Desconectado: …" |
| Doble tirada / doble carta / doble START | ✅ ignorados por el servidor (warning) |
| Carta fuera de `CardPhase` o jugador no activo | ✅ rechazado por validación del Host |
| Modo offline (abrir `Mapa` sin red) | ✅ funciona como antes, sin errores |
| Consola durante partidas completas | ✅ 0 errores |
| Timeout de turno (`turnTimeoutSeconds > 0`) | ⬜ implementado, no probado (desactivado por defecto) |
| 4 jugadores reales | ⬜ manual (lógica cubierta por tests) |

## Prueba con 2 builds (2026-10-07)

Dos instancias de `Builds/Dev/Pared.exe` jugando solas con `DevAutoPlayer`:

```
Pared.exe -autohost -autoplay -players 2 -games 3 -quitonmenu [-hardrules] -logFile host.log
Pared.exe -autojoin 127.0.0.1 -autoplay -quitonmenu -logFile client1.log
```

El bot del Host crea la partida, pulsa START al tener 2 jugadores listos y REINICIAR tras cada final.
Los bots eligen personaje al azar, caminan casi siempre hacia su casilla de misión (camino más corto)
y a veces al azar, juegan cartas especiales en su turno y cartas válidas en combate/duelo, y fallan
la misión un 40 % de las veces. `-hardrules` (solo en ese proceso) sube `requiredCombatRoll` a 5 y
deja 2 vidas para forzar eliminaciones. Todos los mensajes del juego quedan en el log del Host (`[Juego]`).

5 partidas completas (2 normales + 3 con `-hardrules`), 0 excepciones, 0 desincronizaciones:

| Mecánica | Resultado |
|---|---|
| Tablero igual en ambas instancias | ✅ hash idéntico en las 5 partidas |
| Ruleta tras moverse sin combate | ✅ 51 tiradas (±movimiento, penalizaciones, entidad, nada, pierdes turno consumido) |
| Combates de evento con cartas | ✅ 14 combates (Golpe, Escudo, ruleta −daño aplicada) |
| Duelos al cruzarse | ✅ 3 duelos, 1 escape con Gatos fotocopiados |
| Cartas especiales en turno | ✅ Cívica, Comida Frita, Silencio líquido |
| Misiones | ✅ 5 iniciadas, 1 fallida + reintento, 4 completadas → victoria |
| Victoria por último en pie | ✅ J1 eliminado → gana J2 |
| REINICIAR y volver al menú | ✅ 3 reinicios; ambos procesos salen solos al final |

**Errores encontrados y corregidos**
- Silencio líquido tiraba dos veces el 85 % en la misma casilla (al pasar y al revisar la casilla):
  ahora la decisión es una sola por casilla y turno (`engagedEventsThisTurn`).
- Log de combate mostraba `6 + -2`; ahora `6 -2`.
- Bot: tiraba el dado justo después de jugar una carta de turno (warning "Roll rejected"); ahora espera.

## Checklist manual para ti (2 Editores con ParrelSync)

1. Abre el clon (`My project_clone_0`) con ParrelSync y el original. Escena `LobbyScene` en ambos.
2. Original: CREAR. Clon: escribe la IP (o 127.0.0.1) y UNIRSE.
3. Elige personajes, intenta coger el del otro (debe salir el aviso), LISTO en ambos, START.
4. Juega con WASD/flechas o los botones; prueba usar una carta en un combate.
5. Lleva a tu personaje a la casilla MISIÓN de su habitación y pulsa COMPLETAR MISIÓN para llegar al final; REINICIAR dos veces.
6. Cierra el clon en mitad de partida y comprueba que el Host no se bloquea.
