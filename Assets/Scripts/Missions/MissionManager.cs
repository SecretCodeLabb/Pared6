using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Character missions (Host-authoritative). Reaching the mission tile of your character's room
/// starts the mission (TurnState.Mission); every instance shows the character's minigame
/// (MissionDefinition.minigamePrefab, or the placeholder); the mission player's result is sent
/// to the Host. Completing it wins the game (VictoryRules); failing it ends the turn and the
/// player retries it at the start of their next turn while still on the tile (TurnManager).
/// Minigames can stream snapshots to the spectators (SendStateFromLocal / StateReceived).
/// </summary>
public class MissionManager : NetworkBehaviour
{
    public static MissionManager Instance { get; private set; }

    public NetworkVariable<MissionPhase> Phase = new NetworkVariable<MissionPhase>(MissionPhase.None);
    public NetworkVariable<int> PlayerId = new NetworkVariable<int>(TurnRotation.NoPlayer);
    public NetworkVariable<int> CharacterId = new NetworkVariable<int>(-1);

    [Tooltip("Seconds the mission result stays on screen before the game continues.")]
    [SerializeField] private float resultSeconds = 2.5f;

    private Coroutine routine;

    private void Awake()
    {
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        Instance = this;
    }

    public override void OnNetworkDespawn()
    {
        if (Instance == this)
            Instance = null;
    }

    // =========================================================
    // LOCAL QUERIES
    // =========================================================

    public bool IsVisible => IsSpawned && Phase.Value != MissionPhase.None;

    public MissionDefinition CurrentDefinition
    {
        get
        {
            CharacterData character = CharacterCatalog.Instance.Get(CharacterId.Value);
            return character != null ? character.mission : null;
        }
    }

    public bool IsLocalMission
    {
        get
        {
            PlayerNetworkData local = PlayerNetworkData.Local;
            return local != null && local.PlayerId.Value == PlayerId.Value;
        }
    }

    public void RequestFinishFromLocal(bool success)
    {
        if (IsSpawned && Phase.Value == MissionPhase.Active && IsLocalMission)
            RequestFinishRpc(success);
    }

    // =========================================================
    // LIVE SPECTATING (minigame snapshots: mission player -> Host -> everyone else)
    // =========================================================

    private const int MaxStateBytes = 1024;

    /// <summary>Raised on spectators with each snapshot of the mission minigame.</summary>
    public static event System.Action<byte[]> StateReceived;

    public void SendStateFromLocal(byte[] state)
    {
        if (IsSpawned && Phase.Value == MissionPhase.Active && IsLocalMission && state != null && state.Length <= MaxStateBytes)
            SubmitStateRpc(state);
    }

    [Rpc(SendTo.Server, Delivery = RpcDelivery.Unreliable)]
    private void SubmitStateRpc(byte[] state, RpcParams rpcParams = default)
    {
        if (Phase.Value != MissionPhase.Active || state == null || state.Length > MaxStateBytes)
            return;

        PlayerNetworkData sender = PlayerNetworkData.GetByClientId(rpcParams.Receive.SenderClientId);

        if (sender == null || sender.PlayerId.Value != PlayerId.Value)
            return;

        RelayStateRpc(state);
    }

    [Rpc(SendTo.ClientsAndHost, Delivery = RpcDelivery.Unreliable)]
    private void RelayStateRpc(byte[] state)
    {
        if (!IsLocalMission && Phase.Value == MissionPhase.Active)
            StateReceived?.Invoke(state);
    }

    // =========================================================
    // SERVER
    // =========================================================

    public void ServerBegin(PlayerNetworkData player)
    {
        if (!IsServer)
            return;

        StopRoutine();
        PlayerId.Value = player.PlayerId.Value;
        CharacterId.Value = player.CharacterId.Value;
        Phase.Value = MissionPhase.Active;

        MissionDefinition definition = CurrentDefinition;
        string title = definition != null ? definition.title : "su misión";
        GameStateManager.Instance.ServerBroadcast(player, $"¡Empiezas tu misión: {title}!", $"¡{player.CharacterLabel} inicia su misión: {title}!");
        Debug.Log($"{NetworkConnectionManager.GetInstanceTag()} Misión de {player.Name} ({title}) iniciada.");
    }

    [Rpc(SendTo.Server)]
    private void RequestFinishRpc(bool success, RpcParams rpcParams = default)
    {
        if (!TurnManager.Instance.ValidateActivePlayer(rpcParams.Receive.SenderClientId, TurnState.Mission, out PlayerNetworkData player, "FinishMission"))
            return;

        // Duplicate results are ignored: the phase already left Active.
        if (Phase.Value != MissionPhase.Active || player.PlayerId.Value != PlayerId.Value)
        {
            Debug.LogWarning($"Mission: result rejected for {player.Name} (phase {Phase.Value}).");
            return;
        }

        ServerFinish(player, success);
    }

    /// <summary>Host only (turn timeout): the mission counts as failed.</summary>
    public void ServerForceFail()
    {
        if (!IsServer || Phase.Value != MissionPhase.Active)
            return;

        PlayerNetworkData player = PlayerNetworkData.GetByPlayerId(PlayerId.Value);

        if (player != null)
            ServerFinish(player, false);
    }

    /// <summary>Host only: closes the mission without a result (disconnect, game over).</summary>
    public void ServerAbort()
    {
        if (!IsServer)
            return;

        StopRoutine();
        Phase.Value = MissionPhase.None;
    }

    private void ServerFinish(PlayerNetworkData player, bool success)
    {
        Phase.Value = success ? MissionPhase.Completed : MissionPhase.Failed;

        if (success)
            player.MissionCompleted.Value = true;

        GameStateManager.Instance.ServerBroadcast(player,
            success ? "¡Completaste tu misión!" : "Fallaste tu misión",
            success ? $"¡{player.CharacterLabel} completó su misión!" : $"{player.CharacterLabel} falló su misión");

        Debug.Log($"{NetworkConnectionManager.GetInstanceTag()} Misión de {player.Name}: {(success ? "COMPLETADA" : "FALLIDA")}");

        StopRoutine();
        routine = StartCoroutine(EndAfterDelay());
    }

    private IEnumerator EndAfterDelay()
    {
        yield return new WaitForSeconds(resultSeconds);
        routine = null;

        Phase.Value = MissionPhase.None;

        // Completing the mission ends the game here (ServerEndTurn checks victory first).
        TurnManager.Instance.ServerEndTurn();
    }

    private void StopRoutine()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
    }
}
