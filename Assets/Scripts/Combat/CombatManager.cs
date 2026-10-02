using System;
using System.Collections;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Event combat (T10) with card phase (T13). Host-authoritative:
/// CardPhase -> Rolling -> Resolving -> Won/Lost -> back to TurnManager.
/// resultado = tirada + modificador, compared with requerido (GameRules.requiredCombatRoll).
/// </summary>
public class CombatManager : NetworkBehaviour
{
    public static CombatManager Instance { get; private set; }

    public NetworkVariable<CombatPhase> Phase = new NetworkVariable<CombatPhase>(CombatPhase.None);
    public NetworkVariable<int> CombatPlayerId = new NetworkVariable<int>(TurnRotation.NoPlayer);
    public NetworkVariable<int> Roll = new NetworkVariable<int>(0);
    public NetworkVariable<int> RollModifier = new NetworkVariable<int>(0);
    public NetworkVariable<int> Required = new NetworkVariable<int>(0);
    public NetworkVariable<int> CardsUsed = new NetworkVariable<int>(0);
    public NetworkVariable<bool> LifeProtected = new NetworkVariable<bool>(false);
    public NetworkVariable<int> LifeLoss = new NetworkVariable<int>(0);
    public NetworkVariable<FixedString128Bytes> LastCardText = new NetworkVariable<FixedString128Bytes>();

    /// <summary>Raised on every instance when any combat value changes (for UI).</summary>
    public event Action Changed;

    private CombatContext context;
    private Vector2Int eventCell;
    private Coroutine finishRoutine;

    private static GameRules Rules => GameRules.Instance;

    private void Awake()
    {
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        Instance = this;

        Phase.OnValueChanged += OnPhaseChanged;
        CombatPlayerId.OnValueChanged += OnIntChanged;
        Roll.OnValueChanged += OnIntChanged;
        RollModifier.OnValueChanged += OnIntChanged;
        Required.OnValueChanged += OnIntChanged;
        CardsUsed.OnValueChanged += OnIntChanged;
        LifeLoss.OnValueChanged += OnIntChanged;
        LifeProtected.OnValueChanged += OnBoolChanged;
        LastCardText.OnValueChanged += OnTextChanged;
    }

    public override void OnNetworkDespawn()
    {
        Phase.OnValueChanged -= OnPhaseChanged;
        CombatPlayerId.OnValueChanged -= OnIntChanged;
        Roll.OnValueChanged -= OnIntChanged;
        RollModifier.OnValueChanged -= OnIntChanged;
        Required.OnValueChanged -= OnIntChanged;
        CardsUsed.OnValueChanged -= OnIntChanged;
        LifeLoss.OnValueChanged -= OnIntChanged;
        LifeProtected.OnValueChanged -= OnBoolChanged;
        LastCardText.OnValueChanged -= OnTextChanged;

        if (Instance == this)
            Instance = null;
    }

    private void OnPhaseChanged(CombatPhase previous, CombatPhase current) => Changed?.Invoke();
    private void OnIntChanged(int previous, int current) => Changed?.Invoke();
    private void OnBoolChanged(bool previous, bool current) => Changed?.Invoke();
    private void OnTextChanged(FixedString128Bytes previous, FixedString128Bytes current) => Changed?.Invoke();

    // =========================================================
    // LOCAL QUERIES
    // =========================================================

    public bool IsActive => Phase.Value != CombatPhase.None;

    public bool IsLocalFighter
    {
        get
        {
            PlayerNetworkData local = PlayerNetworkData.Local;
            return local != null && local.IsAlive && local.PlayerId.Value == CombatPlayerId.Value;
        }
    }

    public bool CanLocalUseCard()
    {
        PlayerNetworkData local = PlayerNetworkData.Local;

        return IsLocalFighter &&
               Phase.Value == CombatPhase.CardPhase &&
               CardsUsed.Value < Rules.maxCardsPerCombat &&
               local.Hand.Count > 0;
    }

    public bool CanLocalSkipCards() => IsLocalFighter && Phase.Value == CombatPhase.CardPhase;

    public bool CanLocalRoll() => IsLocalFighter && (Phase.Value == CombatPhase.CardPhase || Phase.Value == CombatPhase.Rolling);

    // =========================================================
    // SERVER: START / ABORT
    // =========================================================

    public void ServerBeginCombat(PlayerNetworkData player, Vector2Int cell)
    {
        if (!IsServer)
            return;

        StopFinish();

        context = new CombatContext(player.PlayerId.Value, Rules.requiredCombatRoll, Rules.damageOnFail);
        eventCell = cell;

        CombatPlayerId.Value = player.PlayerId.Value;
        LastCardText.Value = "";
        LifeLoss.Value = 0;
        SyncContext();

        bool canUseCards = Rules.maxCardsPerCombat > 0 && player.Hand.Count > 0;
        Phase.Value = canUseCards ? CombatPhase.CardPhase : CombatPhase.Rolling;

        AnnounceEventRpc(player.Name);
        Debug.Log($"{NetworkConnectionManager.GetInstanceTag()} ¡EVENTO! {player.Name} combate en {cell} (requerido {context.Required})");
    }

    /// <summary>Host only: cancels the current combat without applying a result.</summary>
    public void ServerAbort()
    {
        if (!IsServer)
            return;

        StopFinish();
        context = null;
        Phase.Value = CombatPhase.None;
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void AnnounceEventRpc(string playerName)
    {
        if (EventUI.Instance != null)
            EventUI.Instance.ShowMessage("¡EVENTO!\n<size=70%>¡TE HAS ENCONTRADO CON UNA AMENAZA!</size>\n<size=55%>" + playerName + "</size>", 2.5f);
    }

    // =========================================================
    // CARD PHASE (T13)
    // =========================================================

    public void RequestUseCardFromLocal(int handIndex, int cardId)
    {
        if (CanLocalUseCard())
            RequestUseCardRpc(handIndex, cardId);
    }

    public void RequestSkipCardsFromLocal()
    {
        if (CanLocalSkipCards())
            RequestSkipCardsRpc();
    }

    public void RequestRollFromLocal()
    {
        if (CanLocalRoll())
            RequestCombatRollRpc();
    }

    [Rpc(SendTo.Server)]
    private void RequestUseCardRpc(int handIndex, int cardId, RpcParams rpcParams = default)
    {
        if (!ValidateFighter(rpcParams.Receive.SenderClientId, out PlayerNetworkData player, "UseCard"))
            return;

        if (Phase.Value != CombatPhase.CardPhase)
        {
            Debug.LogWarning($"Combat: UseCard rejected, phase is {Phase.Value}.");
            return;
        }

        if (context.CardsUsed >= Rules.maxCardsPerCombat)
        {
            Debug.LogWarning("Combat: UseCard rejected, maxCardsPerCombat reached.");
            return;
        }

        // The index must still point to the same card (protects against duplicate/stale requests).
        if (handIndex < 0 || handIndex >= player.Hand.Count || player.Hand[handIndex] != cardId)
        {
            Debug.LogWarning($"Combat: UseCard rejected, card {cardId} not at index {handIndex} in {player.Name}'s hand.");
            return;
        }

        CardData card = CardDatabase.Instance.GetById(cardId);

        if (card == null)
        {
            Debug.LogWarning($"Combat: UseCard rejected, unknown card id {cardId}.");
            return;
        }

        player.ServerRemoveCardAt(handIndex);
        card.Apply(context);
        context.CardsUsed++;
        context.AppliedCards.Add(card.displayName);

        LastCardText.Value = $"{player.Name} usó {card.displayName}";
        SyncContext();
        GameStateManager.Instance.ServerBroadcast($"{player.Name} usó {card.displayName}");

        if (context.CardsUsed >= Rules.maxCardsPerCombat)
            Phase.Value = CombatPhase.Rolling;
    }

    [Rpc(SendTo.Server)]
    private void RequestSkipCardsRpc(RpcParams rpcParams = default)
    {
        if (!ValidateFighter(rpcParams.Receive.SenderClientId, out PlayerNetworkData player, "SkipCards"))
            return;

        if (Phase.Value != CombatPhase.CardPhase)
            return;

        LastCardText.Value = $"{player.Name} no usa carta";
        Phase.Value = CombatPhase.Rolling;
    }

    // =========================================================
    // ROLL + RESOLVE (T10)
    // =========================================================

    [Rpc(SendTo.Server)]
    private void RequestCombatRollRpc(RpcParams rpcParams = default)
    {
        if (!ValidateFighter(rpcParams.Receive.SenderClientId, out PlayerNetworkData player, "CombatRoll"))
            return;

        // Duplicate rolls fail here because the phase already moved to Resolving.
        if (Phase.Value != CombatPhase.CardPhase && Phase.Value != CombatPhase.Rolling)
        {
            Debug.LogWarning($"Combat: roll rejected, phase is {Phase.Value}.");
            return;
        }

        ServerResolve(player);
    }

    /// <summary>Host only (timeout): rolls for the active fighter.</summary>
    public void ServerForceRoll()
    {
        if (!IsServer || context == null)
            return;

        if (Phase.Value != CombatPhase.CardPhase && Phase.Value != CombatPhase.Rolling)
            return;

        PlayerNetworkData player = PlayerNetworkData.GetByPlayerId(CombatPlayerId.Value);

        if (player != null)
            ServerResolve(player);
    }

    private void ServerResolve(PlayerNetworkData player)
    {
        Phase.Value = CombatPhase.Resolving;

        context.Roll = UnityEngine.Random.Range(Rules.diceMin, Rules.diceMax + 1);
        CombatOutcome outcome = CombatResolver.Resolve(context);
        SyncContext();
        LifeLoss.Value = outcome.LifeLoss;

        if (outcome.Won)
        {
            player.Progress.Value += outcome.ProgressGain;

            if (Rules.consumeOnSuccess)
                GameStateManager.Instance.ServerConsumeEvent(eventCell);

            Phase.Value = CombatPhase.Won;
        }
        else
        {
            player.Lives.Value = Mathf.Max(0, player.Lives.Value - outcome.LifeLoss);
            Phase.Value = CombatPhase.Lost;

            if (player.Lives.Value <= 0)
                GameStateManager.Instance.ServerEliminate(player);
        }

        Debug.Log($"{NetworkConnectionManager.GetInstanceTag()} Combate {player.Name}: {context.Roll} + {context.RollModifier} = {context.Result} vs {context.Required} -> {(outcome.Won ? "SUPERADO" : "FALLADO")} (vidas -{outcome.LifeLoss})");

        finishRoutine = StartCoroutine(FinishAfterDelay());
    }

    private IEnumerator FinishAfterDelay()
    {
        yield return new WaitForSeconds(Rules.resultDisplaySeconds);
        finishRoutine = null;

        context = null;
        Phase.Value = CombatPhase.None;
        TurnManager.Instance.ServerEndTurn();
    }

    // =========================================================
    // HELPERS
    // =========================================================

    private bool ValidateFighter(ulong sender, out PlayerNetworkData player, string action)
    {
        if (!TurnManager.Instance.ValidateActivePlayer(sender, TurnState.Combat, out player, action))
            return false;

        if (context == null || player.PlayerId.Value != CombatPlayerId.Value)
        {
            Debug.LogWarning($"Combat: {action} rejected, no combat for this player.");
            return false;
        }

        return true;
    }

    private void SyncContext()
    {
        Roll.Value = context.Roll;
        RollModifier.Value = context.RollModifier;
        Required.Value = context.Required;
        CardsUsed.Value = context.CardsUsed;
        LifeProtected.Value = context.PreventLifeLoss || context.DamageReduction >= context.DamageOnFail;
    }

    private void StopFinish()
    {
        if (finishRoutine != null)
        {
            StopCoroutine(finishRoutine);
            finishRoutine = null;
        }
    }
}
