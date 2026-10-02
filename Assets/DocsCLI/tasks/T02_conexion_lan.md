# T02 — Conexión LAN Host/Client
**Depende de:** T01 ✅ · **Tamaño:** M

> Antes de empezar: lee `docs/AGENT_CONTEXT.md`, `docs/PROGRESS.md` y `docs/PROJECT_AUDIT.md`. Verifica que T01 figura ✅. Trabaja SOLO en esta tarea.

## Objetivo
Un Host y un Client pueden conectarse por IP local desde el menú principal y desconectarse limpiamente.

## Qué hacer
1. Añade/actualiza los paquetes necesarios (NGO, Unity Transport) editando `manifest.json` si faltan; confirma que resuelven y compilan.
2. Crea un `NetworkManager` + `UnityTransport` en la escena de menú, persistente entre escenas (`DontDestroyOnLoad`). Configura vía script de Editor (`PPOP/Setup Network`).
3. Crea `NetworkConnectionManager` (en `Scripts/Network/`) con: `StartHost()`, `StartClient(ip, port)`, `Shutdown()`, eventos `OnConnected/OnDisconnected/OnConnectionFailed` y límite `maxPlayers`. Valida formato de IP y gestiona timeout.
4. **Connection approval:** rechaza si hay `maxPlayers` conectados. Deja un flag `AcceptingConnections` (por defecto `true`) que T04 pondrá en `false` al iniciar la partida.
5. **UI del menú principal** (reutiliza la existente si hay): `CREATE GAME`, `JOIN GAME`, campo de IP, texto con la IP local del Host, y texto de estado/errores (IP inválida, timeout, partida llena).
6. **ParrelSync:** deja el proyecto listo (no dependas de ParrelSync en código de juego). Si ayuda, un helper opcional con `#if` que detecte clon para etiquetar la instancia en el log.
7. Tras conectar, **no cargues escenas todavía**: muestra "Conectado — N jugador(es)" para validar.

## Fuera de alcance
Lobby, selección de personaje, carga de escenas, spawn de jugadores.

## Criterios de aceptación
- Host crea partida y ve su IP; el Client introduce la IP, conecta y ambos ven el contador de jugadores.
- Un tercer/quinto cliente (según `maxPlayers`) es rechazado con mensaje.
- Desconectar (cliente o host) limpia la red y vuelve al estado de menú sin errores en consola.
- No se usa Relay, Lobby Service ni Authentication.

## Verificación
Compila; prueba Host + clon ParrelSync (conectar, desconectar, reconectar). Si no puedes ejecutarlo tú, deja el checklist exacto para mí.

## Cierre
Aplica el cierre estándar (AGENT_CONTEXT §9).
