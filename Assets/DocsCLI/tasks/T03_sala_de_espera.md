# T03 — Sala de espera (escena de selección como lobby)
**Depende de:** T02 ✅ · **Tamaño:** M

> Antes de empezar: lee `docs/AGENT_CONTEXT.md` (§8 especialmente), `docs/PROGRESS.md` y `docs/PROJECT_AUDIT.md` (apartado de la escena de selección). Verifica que T02 figura ✅. Trabaja SOLO en esta tarea.

## Objetivo
Tras conectar, todos los jugadores aparecen en la **escena de selección existente**, que funciona como sala de espera con lista de jugadores en tiempo real. Todavía no se sincroniza el personaje (eso es T04).

## Qué hacer
1. **Carga de escena:** el Host carga la escena de selección con `NetworkSceneManager` al crear la partida; los clientes que se conectan entran en ella automáticamente. Añade la escena a Build Settings si falta.
2. Crea `LobbyManager` (`Scripts/Lobby/`, `NetworkBehaviour`) con una lista sincronizada de slots (`NetworkList`): `PlayerId`, `ClientId`, nombre (p. ej. "Jugador N"), `connected`, `ready`, `characterId` (-1 = sin elegir). Todavía solo se rellenan `PlayerId/ClientId/connected`.
3. El Host asigna el `PlayerId` libre más bajo al conectar y lo libera al desconectar. Prepara hasta 4 slots.
4. Crea `PlayerNetworkData` básico (`Scripts/Players/`) si aún no existe (ampliable en T06): `PlayerId`, `CharacterId`.
5. **UI de la escena:** añade a la escena existente una lista de 4 slots (estilo coherente con la escena) que muestre jugador, estado conectado y "eligiendo...". **No alteres** el aspecto/interacción de la selección actual.
6. Mientras la selección siga siendo local en esta tarea, **no escribas en el estado global antiguo** de forma que rompa el modo offline.

## Fuera de alcance
Sincronizar la elección de personaje, botón LISTO/START, carga de `Game`.

## Criterios de aceptación
- Host y Client están en la escena de selección y ambos ven 2 slots conectados con nombres distintos.
- Si el Client se desconecta, su slot se libera en el Host al instante; si se vuelve a conectar, recibe el slot libre.
- La escena sigue viéndose y comportándose igual que antes en modo offline.

## Verificación
Compila; prueba Host + clon ParrelSync (entrar, salir, reentrar).

## Cierre
Aplica el cierre estándar (AGENT_CONTEXT §9).
