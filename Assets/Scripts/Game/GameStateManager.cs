using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Game scene authority (in-scene NetworkObject "GameNetwork" in Mapa).
/// Host: board seed + event cells, load barrier with board hash check, player spawn,
/// victory / game over, disconnects and restart. Clients rebuild the same board from the seed.
/// </summary>
public class GameStateManager : NetworkBehaviour
{
    public const int NoWinner = -1;

    public static GameStateManager Instance { get; private set; }

    [Header("Scene references")]
    [SerializeField] private BoardManager boardManager;
    [SerializeField] private MapGenerator mapGenerator;
    [SerializeField] private EventManager eventManager;

    [Header("Network prefabs")]
    [Tooltip("Assets/Prefabs/Player.prefab (NetworkObject + PlayerNetworkData + PlayerController).")]
    [SerializeField] private GameObject networkPlayerPrefab;

    public NetworkVariable<int> BoardSeed = new NetworkVariable<int>(0);
    public NetworkVariable<bool> BoardReady = new NetworkVariable<bool>(false);
    public NetworkVariable<int> WinnerPlayerId = new NetworkVariable<int>(NoWinner);
    public NetworkVariable<int> StartingPlayerCount = new NetworkVariable<int>(0);

    /// <summary>Cells that still hold an active event. Removing a cell consumes it on every instance.</summary>
    public NetworkList<Vector2Int> EventCells;

    /// <summary>Raised on every instance with a short message for everyone (toast).</summary>
    public static event Action<string> MessageReceived;

    public BoardManager Board => boardManager;
    public EventManager Events => eventManager;
    public int LocalBoardHash { get; private set; }

    private readonly HashSet<ulong> boardReadyClients = new HashSet<ulong>();
    private readonly Dictionary<ulong, int> playerIdByClient = new Dictionary<ulong, int>();
    private bool playersSpawned;

    private void Awake()
    {
        Instance = this;
        EventCells = new NetworkList<Vector2Int>();
    }

    // =========================================================
    // SPAWN / BOARD (T05)
    // =========================================================

    public override void OnNetworkSpawn()
    {
        Instance = this;

        if (!CheckReferences())
            return;

        EventCells.OnListChanged += OnEventCellsChanged;

        if (IsServer)
        {
            NetworkManager.OnClientDisconnectCallback += ServerHandleClientDisconnected;

            int seed = new System.Random().Next(1, 999999);
            BoardSeed.Value = seed;
            BuildBoard(seed);

            System.Random eventRng = new System.Random(seed);
            HashSet<Vector2Int> excluded = new HashSet<Vector2Int>();

            foreach (Tile tile in boardManager.GetStartingTiles(GameRules.Instance.maxPlayers))
                excluded.Add(BoardManager.CellOf(tile));

            foreach (Vector2Int cell in eventManager.PickEventCells(boardManager.GetAllTiles(), eventRng, excluded))
                EventCells.Add(cell);

            ApplyEvents();

            boardReadyClients.Add(NetworkManager.LocalClientId);

            // Wait one frame so every NetworkBehaviour on this object has spawned.
            StartCoroutine(CompleteBarrierNextFrame());
        }
        else
        {
            BuildBoard(BoardSeed.Value);
            ApplyEvents();
            ReportBoardReadyRpc(LocalBoardHash);
        }
    }

    public override void OnNetworkDespawn()
    {
        EventCells.OnListChanged -= OnEventCellsChanged;

        if (NetworkManager != null)
            NetworkManager.OnClientDisconnectCallback -= ServerHandleClientDisconnected;

        if (Instance == this)
            Instance = null;
    }

    private bool CheckReferences()
    {
        bool ok = true;

        if (boardManager == null) { Debug.LogError("GameStateManager: boardManager not assigned."); ok = false; }
        if (mapGenerator == null) { Debug.LogError("GameStateManager: mapGenerator not assigned."); ok = false; }
        if (eventManager == null) { Debug.LogError("GameStateManager: eventManager not assigned."); ok = false; }
        if (networkPlayerPrefab == null) { Debug.LogError("GameStateManager: networkPlayerPrefab not assigned."); ok = false; }

        return ok;
    }

    private void BuildBoard(int seed)
    {
        mapGenerator.GenerateWithSeed(seed, false);
        boardManager.InitializeBoard();
    }

    private void ApplyEvents()
    {
        List<Vector2Int> cells = new List<Vector2Int>();

        foreach (Vector2Int cell in EventCells)
            cells.Add(cell);

        eventManager.ApplyEventCells(boardManager.GetAllTiles(), cells);
        LocalBoardHash = boardManager.ComputeBoardHash(cells);

        Debug.Log($"{NetworkConnectionManager.GetInstanceTag()} Tablero generado. Seed: {BoardSeed.Value} · Eventos: {cells.Count} · Hash: {LocalBoardHash}");
    }

    private void OnEventCellsChanged(NetworkListEvent<Vector2Int> change)
    {
        if (change.Type == NetworkListEvent<Vector2Int>.EventType.Remove ||
            change.Type == NetworkListEvent<Vector2Int>.EventType.RemoveAt)
        {
            eventManager.ConsumeEvent(boardManager.GetTile(change.Value));
        }
    }

    [Rpc(SendTo.Server)]
    private void ReportBoardReadyRpc(int boardHash, RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;

        if (boardHash != LocalBoardHash)
        {
            Debug.LogError($"{NetworkConnectionManager.GetInstanceTag()} ¡TABLERO DESINCRONIZADO! Cliente {sender} hash {boardHash} != Host {LocalBoardHash}");
        }
        else
        {
            Debug.Log($"{NetworkConnectionManager.GetInstanceTag()} Cliente {sender} tablero OK (hash {boardHash}).");
        }

        boardReadyClients.Add(sender);
        ServerTryCompleteBarrier();
    }

    private System.Collections.IEnumerator CompleteBarrierNextFrame()
    {
        yield return null;
        ServerTryCompleteBarrier();
    }

    private void ServerTryCompleteBarrier()
    {
        if (BoardReady.Value || !TurnManager.Instance.IsSpawned)
            return;

        foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
        {
            if (!boardReadyClients.Contains(clientId))
                return;
        }

        BoardReady.Value = true;
        Debug.Log($"{NetworkConnectionManager.GetInstanceTag()} Todos los tableros listos. Empezando partida.");

        ServerSpawnPlayers();

        if (CardManager.Instance != null)
            CardManager.Instance.ServerDealHands();

        TurnManager.Instance.ServerStartGame();
    }

    // =========================================================
    // PLAYERS (T06)
    // =========================================================

    private void ServerSpawnPlayers()
    {
        if (playersSpawned)
            return;

        playersSpawned = true;

        List<LobbySlot> slots = new List<LobbySlot>();

        if (LobbyManager.Instance != null)
        {
            foreach (LobbySlot slot in LobbyManager.Instance.GetSlotsSorted())
            {
                if (slot.Connected && NetworkManager.ConnectedClients.ContainsKey(slot.ClientId))
                    slots.Add(slot);
            }
        }

        List<Tile> startTiles = boardManager.GetStartingTiles(slots.Count);

        for (int i = 0; i < slots.Count; i++)
        {
            LobbySlot slot = slots[i];
            Tile start = startTiles[Mathf.Min(i, startTiles.Count - 1)];

            GameObject playerObject = Instantiate(networkPlayerPrefab, start.transform.position, Quaternion.identity);
            PlayerNetworkData data = playerObject.GetComponent<PlayerNetworkData>();
            data.ServerInitialize(slot, BoardManager.CellOf(start), GameRules.Instance.startingLives);

            NetworkObject networkObject = playerObject.GetComponent<NetworkObject>();
            networkObject.DontDestroyWithOwner = true;
            networkObject.SpawnWithOwnership(slot.ClientId, true);

            playerIdByClient[slot.ClientId] = slot.PlayerId;
        }

        StartingPlayerCount.Value = slots.Count;
    }

    // =========================================================
    // MESSAGES
    // =========================================================

    /// <summary>Host only: shows a short message to everyone.</summary>
    public void ServerBroadcast(string message)
    {
        if (IsServer)
            ShowMessageRpc(message);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ShowMessageRpc(string message)
    {
        MessageReceived?.Invoke(message);
    }

    // =========================================================
    // EVENTS / PROGRESS / VICTORY (T09-T14)
    // =========================================================

    /// <summary>Host only: removes the event at the cell for everyone.</summary>
    public void ServerConsumeEvent(Vector2Int cell)
    {
        if (IsServer && EventCells.Contains(cell))
            EventCells.Remove(cell);
    }

    /// <summary>Host only. Ends the game if someone reached the goal or is the last one standing.</summary>
    public bool ServerCheckVictory()
    {
        if (!IsServer || TurnManager.Instance.State.Value == TurnState.GameOver)
            return TurnManager.Instance.State.Value == TurnState.GameOver;

        GameRules rules = GameRules.Instance;
        List<PlayerLifeState> states = PlayerNetworkData.GetStatesById(rules.maxPlayers);

        int winner = VictoryRules.ResolveWinner(
            TurnManager.Instance.CurrentPlayerId.Value,
            PlayerNetworkData.GetProgressById(rules.maxPlayers),
            states,
            rules.requiredEventsToWin,
            StartingPlayerCount.Value);

        if (winner != TurnRotation.NoPlayer)
        {
            ServerDeclareWinner(winner);
            return true;
        }

        if (TurnRotation.CountAlive(states) == 0)
        {
            ServerDeclareWinner(NoWinner);
            return true;
        }

        return false;
    }

    private void ServerDeclareWinner(int playerId)
    {
        WinnerPlayerId.Value = playerId;

        if (CombatManager.Instance != null)
            CombatManager.Instance.ServerAbort();

        TurnManager.Instance.ServerSetGameOver();

        PlayerNetworkData winner = PlayerNetworkData.GetByPlayerId(playerId);
        Debug.Log($"{NetworkConnectionManager.GetInstanceTag()} PARTIDA TERMINADA. Ganador: {(winner != null ? winner.Name : "nadie")}");
    }

    /// <summary>Host only: lives reached 0 -> Eliminated (T11).</summary>
    public void ServerEliminate(PlayerNetworkData player)
    {
        if (player.State.Value != PlayerLifeState.Alive)
            return;

        player.State.Value = PlayerLifeState.Eliminated;
        ServerBroadcast($"{player.Name} ha sido ELIMINADO");
        Debug.Log($"{NetworkConnectionManager.GetInstanceTag()} OnPlayerEliminated({player.PlayerId.Value})");
    }

    // =========================================================
    // DISCONNECTS (T15)
    // =========================================================

    private void ServerHandleClientDisconnected(ulong clientId)
    {
        if (!playerIdByClient.TryGetValue(clientId, out int playerId))
        {
            // Disconnected while the board was loading.
            ServerTryCompleteBarrier();
            return;
        }

        PlayerNetworkData player = PlayerNetworkData.GetByPlayerId(playerId);

        if (player == null || player.State.Value == PlayerLifeState.Disconnected)
            return;

        player.State.Value = PlayerLifeState.Disconnected;
        player.ServerSetHand(new List<int>());
        ServerBroadcast($"{player.Name} se ha desconectado");

        if (TurnManager.Instance.State.Value == TurnState.GameOver)
            return;

        if (ServerCheckVictory())
            return;

        TurnManager.Instance.ServerHandlePlayerRemoved(playerId);
    }

    // =========================================================
    // RESTART (T14)
    // =========================================================

    /// <summary>Host only: back to character selection; the game scene and its objects are rebuilt from scratch.</summary>
    public void ServerRestart()
    {
        if (!IsServer || LobbyManager.Instance == null)
            return;

        LobbyManager.Instance.ServerReturnToLobby();
    }
}
