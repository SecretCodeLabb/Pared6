using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkConnectionManager : MonoBehaviour
{
        public static NetworkConnectionManager Instance { get; private set; }

        private const string MSG_PLAYER_COUNT = "PPOP_PlayerCount";

        [Header("Configuración de Red")]
        [SerializeField] private ushort defaultPort = 7777;
        [SerializeField] private int maxPlayers = 4;
        [SerializeField] private float connectionTimeoutSeconds = 8f;

        [Header("Sesión (T03)")]
        [Tooltip("Prefab with NetworkObject + LobbyManager, spawned by the Host and kept across scenes.")]
        [SerializeField] private GameObject sessionPrefab;

        [Header("Estado de Conexión")]
        [SerializeField] private bool acceptingConnections = true;

        public bool AcceptingConnections
        {
            get => acceptingConnections;
            set => acceptingConnections = value;
        }

        public int MaxPlayers => maxPlayers;
        public ushort DefaultPort => defaultPort;
        public int ConnectedPlayerCount { get; private set; } = 0;
        public bool IsConnecting { get; private set; } = false;
        public bool IsConnected { get; private set; } = false;

        public string CurrentIP { get; private set; } = "";
        public ushort CurrentPort { get; private set; } = 0;

        /// <summary>Message to show in the menu after returning from a session (e.g. host closed).</summary>
        public static string PendingMenuMessage { get; set; } = "";

        private bool returningToMenu;

        // Eventos
        public event Action OnConnected;
        public event Action<string> OnDisconnected;
        public event Action<string> OnConnectionFailed;
        public event Action<int> OnPlayerCountChanged;

        private Coroutine timeoutCoroutine;

        private void Awake()
        {
            // An instance that is returning to the menu is being torn down: the new menu scene's one replaces it.
            if (Instance != null && Instance != this && !Instance.returningToMenu)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Single source of truth for limits (AGENT_CONTEXT §6).
            maxPlayers = GameRules.Instance.maxPlayers;
            defaultPort = GameRules.Instance.port;
        }

        private void Start()
        {
            // After returning to the menu the old NetworkManager may still have been the singleton
            // when this scene's one was enabled.
            NetworkManager sceneNetworkManager = GetComponent<NetworkManager>();

            if (sceneNetworkManager != null && NetworkManager.Singleton != sceneNetworkManager)
                sceneNetworkManager.SetSingleton();

            EnsureNetworkManagerConfig();
        }

        private void EnsureNetworkManagerConfig()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.NetworkConfig.ConnectionApproval = true;
            }
        }

        public bool StartHost()
        {
            if (NetworkManager.Singleton == null)
            {
                Debug.LogError($"{GetInstanceTag()} No se encontró NetworkManager.Singleton.");
                OnConnectionFailed?.Invoke("NetworkManager no disponible.");
                return false;
            }

            if (NetworkManager.Singleton.IsListening)
            {
                Debug.LogWarning($"{GetInstanceTag()} NetworkManager ya está activo.");
                return false;
            }

            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport == null)
            {
                Debug.LogError($"{GetInstanceTag()} No se encontró UnityTransport en NetworkManager.");
                OnConnectionFailed?.Invoke("UnityTransport no encontrado.");
                return false;
            }

            transport.SetConnectionData("0.0.0.0", defaultPort);

            // Configurar Connection Approval en Host
            NetworkManager.Singleton.NetworkConfig.ConnectionApproval = true;
            NetworkManager.Singleton.ConnectionApprovalCallback = HandleConnectionApproval;

            // Subscribir callbacks
            UnsubscribeEvents();
            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;

            if (NetworkManager.Singleton.StartHost())
            {
                IsConnecting = false;
                IsConnected = true;
                CurrentIP = GetLocalIPAddress();
                CurrentPort = defaultPort;
                ConnectedPlayerCount = 1;

                RegisterMessagingHandlers();

                Debug.Log($"{GetInstanceTag()} Host iniciado con éxito en IP: {CurrentIP}:{defaultPort} (Max: {maxPlayers})");
                OnConnected?.Invoke();
                OnPlayerCountChanged?.Invoke(ConnectedPlayerCount);

                acceptingConnections = true;
                SpawnSessionAndOpenLobby();
                return true;
            }
            else
            {
                Debug.LogError($"{GetInstanceTag()} Falló al iniciar el Host.");
                UnsubscribeEvents();
                OnConnectionFailed?.Invoke("No se pudo iniciar el Host.");
                return false;
            }
        }

        public bool StartClient(string ip, ushort port = 0)
        {
            if (NetworkManager.Singleton == null)
            {
                Debug.LogError($"{GetInstanceTag()} No se encontró NetworkManager.Singleton.");
                OnConnectionFailed?.Invoke("NetworkManager no disponible.");
                return false;
            }

            if (NetworkManager.Singleton.IsListening)
            {
                Debug.LogWarning($"{GetInstanceTag()} NetworkManager ya está activo.");
                return false;
            }

            string targetIP = string.IsNullOrWhiteSpace(ip) ? "127.0.0.1" : ip.Trim();
            if (targetIP.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            {
                targetIP = "127.0.0.1";
            }

            if (!IsValidIP(targetIP))
            {
                string error = $"Dirección IP no válida: '{targetIP}'";
                Debug.LogError($"{GetInstanceTag()} {error}");
                OnConnectionFailed?.Invoke(error);
                return false;
            }

            ushort targetPort = port > 0 ? port : defaultPort;

            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport == null)
            {
                Debug.LogError($"{GetInstanceTag()} No se encontró UnityTransport en NetworkManager.");
                OnConnectionFailed?.Invoke("UnityTransport no encontrado.");
                return false;
            }

            transport.SetConnectionData(targetIP, targetPort);

            // Subscribir callbacks
            UnsubscribeEvents();
            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;

            IsConnecting = true;
            IsConnected = false;
            CurrentIP = targetIP;
            CurrentPort = targetPort;

            if (NetworkManager.Singleton.StartClient())
            {
                Debug.Log($"{GetInstanceTag()} Conectando como Cliente a {targetIP}:{targetPort}...");

                if (timeoutCoroutine != null)
                {
                    StopCoroutine(timeoutCoroutine);
                }
                timeoutCoroutine = StartCoroutine(ConnectTimeoutRoutine());
                return true;
            }
            else
            {
                IsConnecting = false;
                UnsubscribeEvents();
                Debug.LogError($"{GetInstanceTag()} Falló al inicializar el Cliente.");
                OnConnectionFailed?.Invoke("No se pudo iniciar el Cliente.");
                return false;
            }
        }

        private IEnumerator ConnectTimeoutRoutine()
        {
            float elapsed = 0f;
            while (elapsed < connectionTimeoutSeconds)
            {
                if (IsConnected)
                {
                    yield break;
                }
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (IsConnecting && !IsConnected)
            {
                Debug.LogWarning($"{GetInstanceTag()} Timeout agotado ({connectionTimeoutSeconds}s) conectando a {CurrentIP}:{CurrentPort}.");
                Shutdown();
                OnConnectionFailed?.Invoke("Tiempo de espera agotado al conectar al Host.");
            }
        }

        public void Shutdown()
        {
            if (timeoutCoroutine != null)
            {
                StopCoroutine(timeoutCoroutine);
                timeoutCoroutine = null;
            }

            bool wasConnectedOrListening = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

            if (NetworkManager.Singleton != null)
            {
                UnsubscribeEvents();

                if (NetworkManager.Singleton.CustomMessagingManager != null)
                {
                    NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler(MSG_PLAYER_COUNT);
                }

                if (NetworkManager.Singleton.IsListening)
                {
                    NetworkManager.Singleton.Shutdown();
                }
            }

            IsConnecting = false;
            IsConnected = false;
            ConnectedPlayerCount = 0;
            CurrentIP = "";
            CurrentPort = 0;

            if (wasConnectedOrListening)
            {
                Debug.Log($"{GetInstanceTag()} Red apagada limpiamente (Shutdown).");
            }

            OnDisconnected?.Invoke("Desconectado de la partida.");
            OnPlayerCountChanged?.Invoke(0);
        }

        private void HandleConnectionApproval(
            NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            bool gameInProgress = LobbyManager.Instance != null && LobbyManager.Instance.GameInProgress.Value;

            if (!acceptingConnections || gameInProgress)
            {
                response.Approved = false;
                response.Reason = "Partida ya iniciada.";
                Debug.LogWarning($"{GetInstanceTag()} Conexión rechazada para cliente {request.ClientNetworkId}: {response.Reason}");
                return;
            }

            int currentConnected = NetworkManager.Singleton.ConnectedClientsIds.Count;
            if (currentConnected >= maxPlayers)
            {
                response.Approved = false;
                response.Reason = $"Partida llena (máximo {maxPlayers} jugadores).";
                Debug.LogWarning($"{GetInstanceTag()} Conexión rechazada para cliente {request.ClientNetworkId}: {response.Reason} (actual: {currentConnected})");
                return;
            }

            response.Approved = true;
            response.CreatePlayerObject = false; // En T02 no se realiza spawn de jugador
            response.Pending = false;
            Debug.Log($"{GetInstanceTag()} Conexión aprobada para cliente {request.ClientNetworkId}. Total tras entrar: {currentConnected + 1}/{maxPlayers}");
        }

        private void HandleClientConnected(ulong clientId)
        {
            if (NetworkManager.Singleton == null) return;

            // Si somos Host
            if (NetworkManager.Singleton.IsHost)
            {
                ConnectedPlayerCount = NetworkManager.Singleton.ConnectedClientsIds.Count;
                Debug.Log($"{GetInstanceTag()} Jugador conectado (ID {clientId}). Total: {ConnectedPlayerCount}/{maxPlayers}");
                OnPlayerCountChanged?.Invoke(ConnectedPlayerCount);
                BroadcastPlayerCount(ConnectedPlayerCount);
            }
            // Si somos Cliente y es nuestra propia conexión
            else if (clientId == NetworkManager.Singleton.LocalClientId)
            {
                IsConnecting = false;
                IsConnected = true;

                if (timeoutCoroutine != null)
                {
                    StopCoroutine(timeoutCoroutine);
                    timeoutCoroutine = null;
                }

                RegisterMessagingHandlers();
                Debug.Log($"{GetInstanceTag()} Conectado con éxito al Host (LocalClientId: {clientId}).");
                OnConnected?.Invoke();
            }
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            if (NetworkManager.Singleton == null) return;

            string reason = NetworkManager.Singleton.DisconnectReason;

            // En Host: un cliente se ha desconectado
            if (NetworkManager.Singleton.IsHost)
            {
                if (clientId != NetworkManager.Singleton.LocalClientId)
                {
                    ConnectedPlayerCount = NetworkManager.Singleton.ConnectedClientsIds.Count;
                    Debug.Log($"{GetInstanceTag()} Jugador {clientId} desconectado. Total restante: {ConnectedPlayerCount}/{maxPlayers}");
                    OnPlayerCountChanged?.Invoke(ConnectedPlayerCount);
                    BroadcastPlayerCount(ConnectedPlayerCount);
                }
            }
            // En Cliente
            else
            {
                if (clientId == NetworkManager.Singleton.LocalClientId || clientId == NetworkManager.ServerClientId)
                {
                    string msg = !string.IsNullOrEmpty(reason) ? reason : "Desconectado del servidor.";
                    Debug.LogWarning($"{GetInstanceTag()} Desconectado del servidor. Motivo: {msg}");

                    if (IsConnecting)
                    {
                        if (timeoutCoroutine != null)
                        {
                            StopCoroutine(timeoutCoroutine);
                            timeoutCoroutine = null;
                        }
                        IsConnecting = false;
                        UnsubscribeEvents();
                        OnConnectionFailed?.Invoke(msg);
                    }
                    else if (SceneManager.GetActiveScene().name != SceneNames.Menu)
                    {
                        ReturnToMenu("Desconectado: " + msg);
                    }
                    else
                    {
                        Shutdown();
                        OnDisconnected?.Invoke(msg);
                    }
                }
            }
        }

        // =========================================================
        // SESSION / SCENE FLOW
        // =========================================================

        private void SpawnSessionAndOpenLobby()
        {
            if (sessionPrefab == null)
            {
                Debug.LogError($"{GetInstanceTag()} NetworkConnectionManager: sessionPrefab sin asignar. Ejecuta PPOP/Setup Prototype.");
                return;
            }

            GameObject session = Instantiate(sessionPrefab);
            session.GetComponent<NetworkObject>().Spawn(false);

            NetworkManager.Singleton.SceneManager.LoadScene(SceneNames.CharacterSelection, LoadSceneMode.Single);
        }

        /// <summary>
        /// Leaves the session cleanly (Shutdown) and loads the menu scene. Works on Host and Client.
        /// The menu scene brings its own NetworkManager, so the persistent one is destroyed.
        /// </summary>
        public void ReturnToMenu(string message)
        {
            if (returningToMenu)
                return;

            returningToMenu = true;
            PendingMenuMessage = message ?? "";

            Debug.Log($"{GetInstanceTag()} Volviendo al menú. {message}");

            Shutdown();

            GameObject networkManagerObject = NetworkManager.Singleton != null ? NetworkManager.Singleton.gameObject : null;

            SceneManager.LoadScene(SceneNames.Menu, LoadSceneMode.Single);

            if (networkManagerObject != null)
                Destroy(networkManagerObject);

            if (networkManagerObject != gameObject)
                Destroy(gameObject);
        }

        private void RegisterMessagingHandlers()
        {
            if (NetworkManager.Singleton == null || NetworkManager.Singleton.CustomMessagingManager == null)
                return;

            NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler(MSG_PLAYER_COUNT);
            NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(MSG_PLAYER_COUNT, (senderClientId, reader) =>
            {
                reader.ReadValueSafe(out int count);
                ConnectedPlayerCount = count;
                Debug.Log($"{GetInstanceTag()} Recibida actualización de jugadores: {count}/{maxPlayers}");
                OnPlayerCountChanged?.Invoke(count);
            });
        }

        private void BroadcastPlayerCount(int count)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsHost || NetworkManager.Singleton.CustomMessagingManager == null)
                return;

            using (var writer = new FastBufferWriter(sizeof(int), Allocator.Temp))
            {
                writer.WriteValueSafe(count);
                NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll(MSG_PLAYER_COUNT, writer);
            }
        }

        private void UnsubscribeEvents()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
            }
        }

        public bool IsValidIP(string ip)
        {
            if (string.IsNullOrWhiteSpace(ip)) return false;
            string trimmed = ip.Trim();
            if (trimmed.Equals("localhost", StringComparison.OrdinalIgnoreCase)) return true;
            return IPAddress.TryParse(trimmed, out IPAddress address) && address.AddressFamily == AddressFamily.InterNetwork;
        }

        public string GetLocalIPAddress()
        {
            try
            {
                string hostName = Dns.GetHostName();
                IPHostEntry host = Dns.GetHostEntry(hostName);

                foreach (IPAddress ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                    {
                        return ip.ToString();
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"{GetInstanceTag()} Error al obtener IP local: {e.Message}");
            }

            return "127.0.0.1";
        }

        public static string GetInstanceTag()
        {
#if UNITY_EDITOR
            if (ParrelSync.ClonesManager.IsClone())
            {
                return "[ParrelSync Clon]";
            }
            return "[Unity Original]";
#else
            return "[StandAlone]";
#endif
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
