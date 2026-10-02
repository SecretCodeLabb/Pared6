using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Turn order + state machine (T07) and synchronized dice / movement (T08).
/// WaitingForRoll -> Moving -> CheckingTile -> [Combat] -> TurnEnd -> next player.
/// All decisions happen on the Host; clients only send requests.
/// </summary>
public class TurnManager : NetworkBehaviour
{
    public static TurnManager Instance { get; private set; }

    public NetworkVariable<int> CurrentPlayerId = new NetworkVariable<int>(TurnRotation.NoPlayer);
    public NetworkVariable<TurnState> State = new NetworkVariable<TurnState>(TurnState.WaitingForBoard);
    public NetworkVariable<int> LastRoll = new NetworkVariable<int>(0);
    public NetworkVariable<int> MovesRemaining = new NetworkVariable<int>(0);
    public NetworkVariable<int> TurnNumber = new NetworkVariable<int>(0);

    /// <summary>Server time when the current turn times out; 0 = no timeout.</summary>
    public NetworkVariable<double> TurnDeadline = new NetworkVariable<double>(0);

    /// <summary>Raised on every instance when any turn value changes (for UI).</summary>
    public event Action Changed;

    [Tooltip("Seconds to let the last step animation finish before evaluating the tile.")]
    [SerializeField] private float checkTileDelay = 0.6f;

    private Coroutine pendingRoutine;

    private static GameRules Rules => GameRules.Instance;

    private BoardManager Board => GameStateManager.Instance.Board;

    // =========================================================
    // LIFECYCLE
    // =========================================================

    private void Awake()
    {
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        Instance = this;

        CurrentPlayerId.OnValueChanged += OnIntChanged;
        LastRoll.OnValueChanged += OnIntChanged;
        MovesRemaining.OnValueChanged += OnIntChanged;
        TurnNumber.OnValueChanged += OnIntChanged;
        State.OnValueChanged += OnStateChanged;
        TurnDeadline.OnValueChanged += OnDeadlineChanged;
    }

    public override void OnNetworkDespawn()
    {
        CurrentPlayerId.OnValueChanged -= OnIntChanged;
        LastRoll.OnValueChanged -= OnIntChanged;
        MovesRemaining.OnValueChanged -= OnIntChanged;
        TurnNumber.OnValueChanged -= OnIntChanged;
        State.OnValueChanged -= OnStateChanged;
        TurnDeadline.OnValueChanged -= OnDeadlineChanged;

        if (Instance == this)
            Instance = null;
    }

    private void OnIntChanged(int previous, int current) => Changed?.Invoke();

    private void OnDeadlineChanged(double previous, double current) => Changed?.Invoke();

    private void OnStateChanged(TurnState previous, TurnState current)
    {
        Debug.Log($"{NetworkConnectionManager.GetInstanceTag()} Turno: {previous} -> {current} (jugador {CurrentPlayerId.Value})");
        Changed?.Invoke();
    }

    // =========================================================
    // LOCAL QUERIES (UI / input)
    // =========================================================

    public PlayerNetworkData CurrentPlayer => PlayerNetworkData.GetByPlayerId(CurrentPlayerId.Value);

    public bool IsLocalPlayersTurn
    {
        get
        {
            PlayerNetworkData local = PlayerNetworkData.Local;
            return local != null && local.IsAlive && local.PlayerId.Value == CurrentPlayerId.Value;
        }
    }

    public bool CanLocalPlayerRoll() => IsSpawned && IsLocalPlayersTurn && State.Value == TurnState.WaitingForRoll;

    public bool CanLocalPlayerMove() => IsSpawned && IsLocalPlayersTurn && State.Value == TurnState.Moving && MovesRemaining.Value > 0;

    /// <summary>Seconds left before timeout, or -1 when disabled.</summary>
    public float SecondsLeft
    {
        get
        {
            if (TurnDeadline.Value <= 0 || NetworkManager == null)
                return -1f;

            return Mathf.Max(0f, (float)(TurnDeadline.Value - NetworkManager.ServerTime.Time));
        }
    }

    public void RequestRollFromLocalPlayer()
    {
        if (CanLocalPlayerRoll())
            RequestRollRpc();
    }

    public void RequestMoveFromLocalPlayer(Vector2Int direction)
    {
        if (CanLocalPlayerMove())
            RequestMoveStepRpc(direction);
    }

    // =========================================================
    // SERVER: TURN FLOW (T07)
    // =========================================================

    /// <summary>Host only: called once every board is ready and players are spawned.</summary>
    public void ServerStartGame()
    {
        if (!IsServer)
            return;

        List<PlayerLifeState> states = PlayerNetworkData.GetStatesById(Rules.maxPlayers);
        int preferred = 0;

        if (Rules.randomStartingPlayer)
        {
            List<int> alive = new List<int>();

            for (int i = 0; i < states.Count; i++)
            {
                if (states[i] == PlayerLifeState.Alive)
                    alive.Add(i);
            }

            if (alive.Count > 0)
                preferred = alive[UnityEngine.Random.Range(0, alive.Count)];
        }

        int first = TurnRotation.GetFirstPlayer(states, preferred);

        if (first == TurnRotation.NoPlayer)
        {
            Debug.LogError("TurnManager: no Alive players to start the game.");
            return;
        }

        PlayerNetworkData firstPlayer = PlayerNetworkData.GetByPlayerId(first);
        GameStateManager.Instance.ServerBroadcast($"¡Empieza la partida! Comienza {firstPlayer.Name}");
        ServerBeginTurn(first);
    }

    public void ServerBeginTurn(int playerId)
    {
        if (!IsServer || State.Value == TurnState.GameOver)
            return;

        StopPending();

        CurrentPlayerId.Value = playerId;
        LastRoll.Value = 0;
        MovesRemaining.Value = 0;
        TurnNumber.Value++;
        RestartDeadline();
        State.Value = TurnState.WaitingForRoll;
    }

    public void ServerSetState(TurnState state)
    {
        if (IsServer && State.Value != TurnState.GameOver)
            State.Value = state;
    }

    /// <summary>Host only. Ends the current turn (checks victory first) and passes to the next Alive player.</summary>
    public void ServerEndTurn()
    {
        if (!IsServer || State.Value == TurnState.GameOver)
            return;

        if (GameStateManager.Instance.ServerCheckVictory())
            return;

        StopPending();
        TurnDeadline.Value = 0;
        State.Value = TurnState.TurnEnd;
        pendingRoutine = StartCoroutine(NextPlayerAfterDelay());
    }

    private IEnumerator NextPlayerAfterDelay()
    {
        yield return new WaitForSeconds(Rules.turnEndDelaySeconds);
        pendingRoutine = null;

        if (State.Value == TurnState.GameOver)
            yield break;

        int next = TurnRotation.GetNextPlayer(
            CurrentPlayerId.Value,
            PlayerNetworkData.GetStatesById(Rules.maxPlayers));

        if (next == TurnRotation.NoPlayer)
        {
            GameStateManager.Instance.ServerCheckVictory();
            yield break;
        }

        ServerBeginTurn(next);
    }

    public void ServerSetGameOver()
    {
        if (!IsServer)
            return;

        StopPending();
        TurnDeadline.Value = 0;
        MovesRemaining.Value = 0;
        State.Value = TurnState.GameOver;
    }

    /// <summary>Host only: a player was eliminated or disconnected; don't let the game get stuck on them.</summary>
    public void ServerHandlePlayerRemoved(int playerId)
    {
        if (!IsServer || State.Value == TurnState.GameOver)
            return;

        CombatManager combat = CombatManager.Instance;
        bool wasDuelDefender = combat != null && combat.IsDuelParticipant(playerId) && playerId != CurrentPlayerId.Value;

        if (playerId != CurrentPlayerId.Value && !wasDuelDefender)
            return;

        if (combat != null)
            combat.ServerAbort();

        ServerEndTurn();
    }

    /// <summary>
    /// Host-side validation used by every gameplay RPC: sender is the active, Alive player
    /// and the turn is in the required state.
    /// </summary>
    public bool ValidateActivePlayer(ulong senderClientId, TurnState requiredState, out PlayerNetworkData player, string action)
    {
        player = PlayerNetworkData.GetByClientId(senderClientId);

        if (player == null)
        {
            Debug.LogWarning($"TurnManager: {action} rejected, client {senderClientId} has no player.");
            return false;
        }

        if (!player.IsAlive)
        {
            Debug.LogWarning($"TurnManager: {action} rejected, {player.Name} is {player.State.Value}.");
            return false;
        }

        if (player.PlayerId.Value != CurrentPlayerId.Value)
        {
            Debug.LogWarning($"TurnManager: {action} rejected, it is not {player.Name}'s turn.");
            return false;
        }

        if (State.Value != requiredState)
        {
            Debug.LogWarning($"TurnManager: {action} rejected for {player.Name}, state is {State.Value} (needs {requiredState}).");
            return false;
        }

        return true;
    }

    // =========================================================
    // SERVER: DICE + MOVEMENT (T08)
    // =========================================================

    [Rpc(SendTo.Server)]
    private void RequestRollRpc(RpcParams rpcParams = default)
    {
        // Duplicate requests fail validation because the state already left WaitingForRoll.
        if (!ValidateActivePlayer(rpcParams.Receive.SenderClientId, TurnState.WaitingForRoll, out PlayerNetworkData player, "Roll"))
            return;

        ServerRoll(player);
    }

    private void ServerRoll(PlayerNetworkData player)
    {
        int roll = UnityEngine.Random.Range(Rules.diceMin, Rules.diceMax + 1);

        LastRoll.Value = roll;
        MovesRemaining.Value = roll;
        State.Value = TurnState.Moving;

        Debug.Log($"{NetworkConnectionManager.GetInstanceTag()} {player.Name} tira el dado: {roll}");

        Tile tile = Board.GetTile(player.GridPosition.Value);

        if (tile == null || !Board.HasAnyValidMove(tile))
            ServerFinishMovement(player);
    }

    [Rpc(SendTo.Server)]
    private void RequestMoveStepRpc(Vector2Int direction, RpcParams rpcParams = default)
    {
        if (!ValidateActivePlayer(rpcParams.Receive.SenderClientId, TurnState.Moving, out PlayerNetworkData player, "Move"))
            return;

        bool isUnitStep = Mathf.Abs(direction.x) + Mathf.Abs(direction.y) == 1;

        if (!isUnitStep || MovesRemaining.Value <= 0)
        {
            Debug.LogWarning($"TurnManager: invalid step {direction} from {player.Name}.");
            return;
        }

        Tile current = Board.GetTile(player.GridPosition.Value);
        Tile next = Board.GetNeighbor(current, direction);

        if (next == null || !next.walkable)
        {
            // Wall: rejected without consuming a move.
            Debug.Log($"{player.Name}: movimiento bloqueado por una pared ({direction}).");
            return;
        }

        player.GridPosition.Value = BoardManager.CellOf(next);
        MovesRemaining.Value--;

        // Walking onto another player or (optionally) an active event stops the movement there.
        bool encounter = FindOpponentAt(player, player.GridPosition.Value) != null ||
                         (Rules.eventsTriggerWhenPassing && GameStateManager.Instance.Events.HasActiveEvent(next));

        if (encounter && MovesRemaining.Value > 0)
            Debug.Log($"{NetworkConnectionManager.GetInstanceTag()} {player.Name}: movimiento detenido por un encuentro en {player.GridPosition.Value} ({MovesRemaining.Value} pasos sin usar)");

        if (encounter || MovesRemaining.Value <= 0 || !Board.HasAnyValidMove(next))
            ServerFinishMovement(player);
    }

    private void ServerFinishMovement(PlayerNetworkData player)
    {
        MovesRemaining.Value = 0;
        State.Value = TurnState.CheckingTile;

        StopPending();
        pendingRoutine = StartCoroutine(CheckTileAfterDelay(player));
    }

    // =========================================================
    // SERVER: TILE CHECK (T09)
    // =========================================================

    private IEnumerator CheckTileAfterDelay(PlayerNetworkData player)
    {
        yield return new WaitForSeconds(checkTileDelay);
        pendingRoutine = null;

        if (State.Value != TurnState.CheckingTile)
            yield break;

        Vector2Int cell = player.GridPosition.Value;
        Tile tile = Board.GetTile(cell);
        PlayerNetworkData opponent = FindOpponentAt(player, cell);

        // Encounters are evaluated only here, once the movement has stopped on the tile.
        // A duel with another player takes priority over an event on the same tile.
        if (opponent != null && CombatManager.Instance != null)
        {
            State.Value = TurnState.Combat;
            RestartDeadline();
            CombatManager.Instance.ServerBeginDuel(player, opponent);
        }
        else if (GameStateManager.Instance.Events.HasActiveEvent(tile) && CombatManager.Instance != null)
        {
            State.Value = TurnState.Combat;
            RestartDeadline();
            CombatManager.Instance.ServerBeginCombat(player, cell);
        }
        else
        {
            ServerEndTurn();
        }
    }

    /// <summary>Lowest-PlayerId Alive opponent standing on the cell, or null (duels disabled = always null).</summary>
    private static PlayerNetworkData FindOpponentAt(PlayerNetworkData mover, Vector2Int cell)
    {
        if (!Rules.duelsEnabled)
            return null;

        PlayerNetworkData found = null;

        foreach (PlayerNetworkData other in PlayerNetworkData.All)
        {
            if (other == mover || !other.IsAlive || other.GridPosition.Value != cell)
                continue;

            if (found == null || other.PlayerId.Value < found.PlayerId.Value)
                found = other;
        }

        return found;
    }

    // =========================================================
    // TIMEOUT (T15)
    // =========================================================

    private void RestartDeadline()
    {
        float timeout = Rules.turnTimeoutSeconds;
        TurnDeadline.Value = timeout > 0f ? NetworkManager.ServerTime.Time + timeout : 0;
    }

    private void Update()
    {
        if (!IsServer || !IsSpawned || TurnDeadline.Value <= 0)
            return;

        if (NetworkManager.ServerTime.Time < TurnDeadline.Value)
            return;

        TurnDeadline.Value = 0;
        PlayerNetworkData player = CurrentPlayer;

        switch (State.Value)
        {
            case TurnState.WaitingForRoll:
            case TurnState.Moving:
                GameStateManager.Instance.ServerBroadcast($"Tiempo agotado: {player?.Name} pierde el turno");
                ServerEndTurn();
                break;

            case TurnState.Combat:
                GameStateManager.Instance.ServerBroadcast("Tiempo agotado: tirada automática");
                CombatManager.Instance.ServerForceRoll();
                break;
        }
    }

    private void StopPending()
    {
        if (pendingRoutine != null)
        {
            StopCoroutine(pendingRoutine);
            pendingRoutine = null;
        }
    }
}
