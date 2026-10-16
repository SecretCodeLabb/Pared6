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
    /// <summary>Increments on every roll: clients play the dice animation when it changes.</summary>
    public NetworkVariable<int> RollCount = new NetworkVariable<int>(0);
    /// <summary>True while the dice animation plays (the roll is decided, movement not open yet).</summary>
    public NetworkVariable<bool> DiceRolling = new NetworkVariable<bool>(false);
    public NetworkVariable<int> MovesRemaining = new NetworkVariable<int>(0);
    public NetworkVariable<int> TurnNumber = new NetworkVariable<int>(0);

    /// <summary>Server time when the current turn times out; 0 = no timeout.</summary>
    public NetworkVariable<double> TurnDeadline = new NetworkVariable<double>(0);

    /// <summary>Raised on every instance when any turn value changes (for UI).</summary>
    public event Action Changed;

    [Tooltip("Seconds to let the last step animation finish before evaluating the tile.")]
    [SerializeField] private float checkTileDelay = 0.6f;

    private Coroutine pendingRoutine;

    // One roulette spin per turn (extra moves from the wheel do not spin it again).
    private bool rouletteDoneThisTurn;

    /// <summary>Special card "Cívica": the current movement ignores walls.</summary>
    public NetworkVariable<bool> WallWalk = new NetworkVariable<bool>(false);

    // One own-turn special card per turn; event cells dodged this turn are not re-triggered.
    private bool turnCardUsedThisTurn;
    /// <summary>The active player already used its character ability this turn.</summary>
    public NetworkVariable<bool> AbilityUsed = new NetworkVariable<bool>(false);
    private readonly HashSet<Vector2Int> avoidedEventsThisTurn = new HashSet<Vector2Int>();
    // Event cells where the dodge already failed this turn (decided once, never re-rolled).
    private readonly HashSet<Vector2Int> engagedEventsThisTurn = new HashSet<Vector2Int>();
    // Status effects tick once per turn even if ServerEndTurn is reached twice.
    private int effectsTickedTurn = -1;

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

    public bool CanLocalPlayerRoll() => IsSpawned && IsLocalPlayersTurn && State.Value == TurnState.WaitingForRoll && !DiceRolling.Value;

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
        GameStateManager.Instance.ServerBroadcast(firstPlayer, "¡Empieza la partida! Empiezas tú", $"¡Empieza la partida! Empieza {firstPlayer.CharacterLabel}");
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
        rouletteDoneThisTurn = false;
        turnCardUsedThisTurn = false;
        AbilityUsed.Value = false;
        avoidedEventsThisTurn.Clear();
        engagedEventsThisTurn.Clear();
        WallWalk.Value = false;
        DiceRolling.Value = false;
        RestartDeadline();
        State.Value = TurnState.WaitingForRoll;

        PlayerNetworkData player = PlayerNetworkData.GetByPlayerId(playerId);

        if (player == null)
            return;

        GameStateManager.Instance.ServerBroadcast(player, "<color=#FFD040>¡Es tu turno!</color>", $"Turno de {player.CharacterLabel}");

        // Failed mission: still on the mission tile, so the new turn retries it right away (no roll).
        if (MissionManager.Instance != null && IsPendingMissionCell(player, player.GridPosition.Value))
        {
            State.Value = TurnState.Mission;
            RestartDeadline();
            MissionManager.Instance.ServerBegin(player);
        }
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

        // "Next round" roulette effects of the player whose turn ends count down.
        PlayerNetworkData current = CurrentPlayer;

        if (current != null && effectsTickedTurn != TurnNumber.Value)
        {
            effectsTickedTurn = TurnNumber.Value;
            current.ServerTickTurnEffects();
        }

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

        List<PlayerLifeState> states = PlayerNetworkData.GetStatesById(Rules.maxPlayers);
        int next = TurnRotation.GetNextPlayer(CurrentPlayerId.Value, states);

        // Roulette "pierdes un turno": skip players with pending skips (each skip is consumed once).
        for (int guard = 0; guard < Rules.maxPlayers * 4 && next != TurnRotation.NoPlayer; guard++)
        {
            PlayerNetworkData candidate = PlayerNetworkData.GetByPlayerId(next);

            if (candidate == null || candidate.SkipTurns.Value <= 0)
                break;

            candidate.SkipTurns.Value--;
            candidate.ServerTickTurnEffects();
            GameStateManager.Instance.ServerBroadcast(candidate, "Pierdes este turno", $"{candidate.CharacterLabel} pierde este turno");
            next = TurnRotation.GetNextPlayer(next, states);
        }

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

        if (RouletteManager.Instance != null)
            RouletteManager.Instance.ServerAbort();

        if (MissionManager.Instance != null)
            MissionManager.Instance.ServerAbort();

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

        if (RouletteManager.Instance != null)
            RouletteManager.Instance.ServerAbort();

        if (MissionManager.Instance != null)
            MissionManager.Instance.ServerAbort();

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

        // The dice is already rolling (duplicate request).
        if (DiceRolling.Value)
            return;

        ServerRoll(player);
    }

    /// <summary>Decides the roll now; everyone sees the dice tumble before the movement opens.</summary>
    private void ServerRoll(PlayerNetworkData player)
    {
        int roll = UnityEngine.Random.Range(Rules.diceMin, Rules.diceMax + 1);

        LastRoll.Value = roll;
        RollCount.Value++;
        Debug.Log($"{NetworkConnectionManager.GetInstanceTag()} [Dado] {player.Name} lanza ({roll})");

        if (Rules.diceAnimationSeconds <= 0f)
        {
            ServerApplyRoll(player, roll);
            return;
        }

        DiceRolling.Value = true;
        StopPending();
        pendingRoutine = StartCoroutine(ApplyRollAfterAnimation(player, roll));
    }

    private IEnumerator ApplyRollAfterAnimation(PlayerNetworkData player, int roll)
    {
        yield return new WaitForSeconds(Rules.diceAnimationSeconds);
        pendingRoutine = null;
        DiceRolling.Value = false;

        if (State.Value == TurnState.WaitingForRoll && player != null && player.IsAlive && player.PlayerId.Value == CurrentPlayerId.Value)
            ServerApplyRoll(player, roll);
    }

    private void ServerApplyRoll(PlayerNetworkData player, int roll)
    {

        // Roulette "-movimiento" is consumed by this roll.
        int penalty = player.MovePenalty.Value;
        player.MovePenalty.Value = 0;

        if (penalty > 0)
            GameStateManager.Instance.ServerBroadcast(player, $"Sacaste {roll} - {penalty} por la ruleta", $"{player.CharacterLabel} sacó {roll} - {penalty} por la ruleta");

        MovesRemaining.Value = Mathf.Max(0, roll - penalty);
        State.Value = TurnState.Moving;

        Debug.Log($"{NetworkConnectionManager.GetInstanceTag()} {player.Name} tira el dado: {roll} (penalización {penalty})");

        if (MovesRemaining.Value == 0)
        {
            ServerFinishMovement(player);
            return;
        }

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

        // "Cívica" ignores walls but never leaves the board.
        Tile next = WallWalk.Value
            ? Board.GetTile(player.GridPosition.Value + direction)
            : Board.GetNeighbor(current, direction);

        if (next == null || !next.walkable)
        {
            // Wall: rejected without consuming a move.
            Debug.Log($"{player.Name}: movimiento bloqueado por una pared ({direction}).");
            return;
        }

        player.GridPosition.Value = BoardManager.CellOf(next);
        MovesRemaining.Value--;

        // Walking onto another player or (optionally) an active event stops the movement there.
        bool eventAhead = Rules.eventsTriggerWhenPassing && IsLiveEventFor(player, next);
        bool encounter = FindOpponentAt(player, player.GridPosition.Value) != null || eventAhead ||
                         IsPendingMissionCell(player, player.GridPosition.Value);

        if (encounter && MovesRemaining.Value > 0)
            Debug.Log($"{NetworkConnectionManager.GetInstanceTag()} {player.Name}: movimiento detenido por un encuentro en {player.GridPosition.Value} ({MovesRemaining.Value} pasos sin usar)");

        if (encounter || MovesRemaining.Value <= 0 || !HasAnyStep(next))
            ServerFinishMovement(player);
    }

    /// <summary>The cell is this player's own mission tile and the mission is not completed yet.</summary>
    private static bool IsPendingMissionCell(PlayerNetworkData player, Vector2Int cell)
    {
        return !player.MissionCompleted.Value &&
               GameStateManager.Instance.TryGetMissionCell(player, out Vector2Int missionCell) &&
               missionCell == cell;
    }

    private bool HasAnyStep(Tile tile)
    {
        if (tile == null)
            return false;

        if (!WallWalk.Value)
            return Board.HasAnyValidMove(tile);

        foreach (Vector2Int d in BoardLayout.Directions)
        {
            if (Board.GetTile(tile.gridPosition + d) != null)
                return true;
        }

        return false;
    }

    /// <summary>
    /// True if an active event on this tile will fight the player. A player under "Silencio líquido"
    /// rolls once per event tile per turn to dodge it (the event stays on the board).
    /// </summary>
    private bool IsLiveEventFor(PlayerNetworkData player, Tile tile)
    {
        if (!GameStateManager.Instance.Events.HasActiveEvent(tile))
            return false;

        Vector2Int cell = BoardManager.CellOf(tile);

        // The dodge is decided once per tile and turn (passing over it and then the tile check
        // must not roll twice).
        if (avoidedEventsThisTurn.Contains(cell))
            return false;

        if (engagedEventsThisTurn.Contains(cell))
            return true;

        if (ServerTryAvoidEntity(player))
        {
            avoidedEventsThisTurn.Add(cell);
            return false;
        }

        engagedEventsThisTurn.Add(cell);
        return true;
    }

    /// <summary>"Silencio líquido" chance roll against an entity fight (event or forced fight).</summary>
    private bool ServerTryAvoidEntity(PlayerNetworkData player)
    {
        if (player.EventShieldTurns.Value <= 0)
            return false;

        if (!SpecialCardEffect.Succeeds(player.EventShieldChance.Value, UnityEngine.Random.value))
        {
            GameStateManager.Instance.ServerBroadcast(player, "No logras esquivar a la entidad", $"{player.CharacterLabel} no logra esquivar a la entidad");
            return false;
        }

        GameStateManager.Instance.ServerBroadcast(player, "Esquivas a la entidad en silencio", $"{player.CharacterLabel} esquiva a la entidad en silencio");
        return true;
    }

    private void ServerFinishMovement(PlayerNetworkData player)
    {
        WallWalk.Value = false;
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
        else if (MissionManager.Instance != null && IsPendingMissionCell(player, cell))
        {
            // Reached the mission tile of the player's own character room.
            State.Value = TurnState.Mission;
            RestartDeadline();
            MissionManager.Instance.ServerBegin(player);
        }
        else if (CombatManager.Instance != null && IsLiveEventFor(player, tile))
        {
            State.Value = TurnState.Combat;
            RestartDeadline();
            CombatManager.Instance.ServerBeginCombat(player, cell);
        }
        else if (Rules.rouletteEnabled && !rouletteDoneThisTurn && RouletteManager.Instance != null)
        {
            // No combat and no duel: spin the roulette (once per turn).
            rouletteDoneThisTurn = true;
            State.Value = TurnState.Roulette;
            RestartDeadline();
            RouletteManager.Instance.ServerBegin(player);
        }
        else
        {
            ServerEndTurn();
        }
    }

    // =========================================================
    // SERVER: ROULETTE RESULTS
    // =========================================================

    /// <summary>Host only (roulette "+movimiento"): extra steps right now, same validation as a roll.</summary>
    public void ServerGrantExtraMoves(PlayerNetworkData player, int amount)
    {
        if (!IsServer || State.Value == TurnState.GameOver)
            return;

        MovesRemaining.Value = Mathf.Max(0, amount);
        State.Value = TurnState.Moving;
        RestartDeadline();

        Tile tile = Board.GetTile(player.GridPosition.Value);

        if (MovesRemaining.Value == 0 || tile == null || !Board.HasAnyValidMove(tile))
            ServerFinishMovement(player);
    }

    /// <summary>Host only (roulette "pelea obligatoria"): combat against an entity on the current tile.</summary>
    public void ServerStartForcedFight(PlayerNetworkData player)
    {
        if (!IsServer || State.Value == TurnState.GameOver || CombatManager.Instance == null)
        {
            ServerEndTurn();
            return;
        }

        if (ServerTryAvoidEntity(player))
        {
            ServerEndTurn();
            return;
        }

        State.Value = TurnState.Combat;
        RestartDeadline();
        CombatManager.Instance.ServerBeginCombat(player, player.GridPosition.Value, true);
    }

    // =========================================================
    // CHARACTER ABILITY (button next to the character card)
    // =========================================================

    /// <summary>The local player can use its character ability right now.</summary>
    public bool CanLocalUseAbility()
    {
        PlayerNetworkData local = PlayerNetworkData.Local;
        CharacterData character = local != null ? local.Character : null;

        return IsSpawned && IsLocalPlayersTurn && character != null && !DiceRolling.Value &&
               CharacterAbilityRules.CanUse(character.ability, State.Value, MovesRemaining.Value, WallWalk.Value, AbilityUsed.Value);
    }

    public void RequestUseAbilityFromLocal()
    {
        if (CanLocalUseAbility())
            RequestUseAbilityRpc();
    }

    [Rpc(SendTo.Server)]
    private void RequestUseAbilityRpc(RpcParams rpcParams = default)
    {
        if (!ValidateActivePlayer(rpcParams.Receive.SenderClientId, State.Value, out PlayerNetworkData player, "UseAbility"))
            return;

        CharacterData character = player.Character;

        if (character == null || DiceRolling.Value ||
            !CharacterAbilityRules.CanUse(character.ability, State.Value, MovesRemaining.Value, WallWalk.Value, AbilityUsed.Value))
        {
            Debug.LogWarning($"TurnManager: UseAbility rejected for {player.Name} (state {State.Value}, moves {MovesRemaining.Value}, used {AbilityUsed.Value}).");
            return;
        }

        AbilityUsed.Value = true;
        AbilityOutcome outcome = CharacterAbilityRules.Resolve(character.ability, UnityEngine.Random.value, character.abilityAmount, character.abilityFailChance);
        GameStateManager game = GameStateManager.Instance;

        if (outcome.Success)
        {
            MovesRemaining.Value += outcome.ExtraMoves;
            RestartDeadline();
            game.ServerBroadcast(player, $"¡Lograste avanzar {outcome.ExtraMoves} casillas más!", $"¡{player.CharacterLabel} avanza {outcome.ExtraMoves} casillas más!");
        }
        else
        {
            // The current movement goes on; the next turn is skipped.
            player.SkipTurns.Value += outcome.SkipTurns;
            game.ServerBroadcast(player, "¡Habilidad especial falló! Pierdes tu siguiente turno", $"¡La habilidad de {player.CharacterLabel} falló! Pierde su siguiente turno");
        }

        Debug.Log($"{NetworkConnectionManager.GetInstanceTag()} Habilidad {character.ability} de {player.Name}: {(outcome.Success ? "OK +" + outcome.ExtraMoves : "FALLÓ, pierde turno")}");
    }

    // =========================================================
    // SPECIAL CARDS PLAYED ON THE OWN TURN
    // =========================================================

    /// <summary>The local player can play this hand card now (own turn, before rolling).</summary>
    public bool CanLocalPlayTurnCard(int handIndex)
    {
        PlayerNetworkData local = PlayerNetworkData.Local;

        // One card per turn is enforced by the Host (it answers with a message).
        if (!CanLocalPlayerRoll() || local.CardsBlocked)
            return false;

        if (handIndex < 0 || handIndex >= local.Hand.Count)
            return false;

        return CardRules.CanPlayOnOwnTurn(CardDatabase.Instance.GetById(local.Hand[handIndex]));
    }

    public void RequestPlayTurnCardFromLocal(int handIndex)
    {
        if (!CanLocalPlayTurnCard(handIndex))
            return;

        RequestPlayTurnCardRpc(handIndex, PlayerNetworkData.Local.Hand[handIndex]);
    }

    [Rpc(SendTo.Server)]
    private void RequestPlayTurnCardRpc(int handIndex, int cardId, RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;

        if (!ValidateActivePlayer(sender, TurnState.WaitingForRoll, out PlayerNetworkData player, "PlayTurnCard") || DiceRolling.Value)
            return;

        GameStateManager game = GameStateManager.Instance;

        if (turnCardUsedThisTurn)
        {
            game.ServerMessageTo(sender, "Solo puedes usar una carta especial por turno.");
            return;
        }

        if (player.CardsBlocked)
        {
            game.ServerMessageTo(sender, "La ruleta te impide usar cartas este turno.");
            return;
        }

        if (handIndex < 0 || handIndex >= player.Hand.Count || player.Hand[handIndex] != cardId)
        {
            Debug.LogWarning($"TurnManager: card {cardId} not at index {handIndex} in {player.Name}'s hand.");
            return;
        }

        CardData card = CardDatabase.Instance.GetById(cardId);
        SpecialCardEffect special = card != null ? card.effect as SpecialCardEffect : null;

        if (special == null || special.Timing != CardTiming.OwnTurn)
        {
            game.ServerMessageTo(sender, card != null ? CardRules.TimingHint(card) : "Carta desconocida.");
            return;
        }

        if (special.ability == SpecialAbility.Heal && player.Lives.Value >= Rules.startingLives)
        {
            game.ServerMessageTo(sender, "Ya tienes todas tus vidas.");
            return;
        }

        player.ServerRemoveCardAt(handIndex);
        turnCardUsedThisTurn = true;
        game.ServerBroadcast(player, $"Usaste {card.displayName}", $"{player.CharacterLabel} usó {card.displayName}");

        switch (special.ability)
        {
            case SpecialAbility.AvoidEvents:
                player.EventShieldChance.Value = special.chance;
                player.EventShieldTurns.Value = Mathf.Max(player.EventShieldTurns.Value, special.turns);
                break;

            case SpecialAbility.Heal:
                player.Lives.Value = Mathf.Min(Rules.startingLives, player.Lives.Value + Mathf.Max(1, special.amount));
                break;

            case SpecialAbility.WallWalk:
                // Replaces the roll: exactly 'amount' steps through walls.
                LastRoll.Value = 0;
                WallWalk.Value = true;
                MovesRemaining.Value = Mathf.Max(1, special.amount);
                State.Value = TurnState.Moving;
                RestartDeadline();

                if (!HasAnyStep(Board.GetTile(player.GridPosition.Value)))
                    ServerFinishMovement(player);
                break;
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
                GameStateManager.Instance.ServerBroadcast(player, "Tiempo agotado: pierdes el turno", $"Tiempo agotado: {player?.CharacterLabel} pierde el turno");
                ServerEndTurn();
                break;

            case TurnState.Combat:
                GameStateManager.Instance.ServerBroadcast(player, "Tiempo agotado: se tira el dado por ti", $"Tiempo agotado: tirada automática de {player?.CharacterLabel}");
                CombatManager.Instance.ServerForceRoll();
                break;

            case TurnState.Roulette:
                GameStateManager.Instance.ServerBroadcast(player, "Tiempo agotado: tu ruleta gira sola", $"Tiempo agotado: la ruleta de {player?.CharacterLabel} gira sola");
                RouletteManager.Instance.ServerForceSpin();
                break;

            case TurnState.Mission:
                GameStateManager.Instance.ServerBroadcast(player, "Tiempo agotado: tu misión falla", $"Tiempo agotado: la misión de {player?.CharacterLabel} falla");
                MissionManager.Instance.ServerForceFail();
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
