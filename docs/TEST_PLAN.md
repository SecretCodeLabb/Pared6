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

## Checklist manual para ti (2 Editores con ParrelSync)

1. Abre el clon (`My project_clone_0`) con ParrelSync y el original. Escena `LobbyScene` en ambos.
2. Original: CREAR. Clon: escribe la IP (o 127.0.0.1) y UNIRSE.
3. Elige personajes, intenta coger el del otro (debe salir el aviso), LISTO en ambos, START.
4. Juega con WASD/flechas o los botones; prueba usar una carta en un combate.
5. Lleva a tu personaje a la casilla MISIÓN de su habitación y pulsa COMPLETAR MISIÓN para llegar al final; REINICIAR dos veces.
6. Cierra el clon en mitad de partida y comprueba que el Host no se bloquea.
