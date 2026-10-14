using System;
using System.Collections;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Host-authoritative encounters.
/// Event combat (T10/T13): CardPhase -> Rolling -> Resolving -> Won/Lost.
///   resultado = tirada + modificador, compared with requerido (GameRules.requiredCombatRoll).
/// Duel (two players on the same tile): both sides may play a card and roll their own die
///   (same dice + card system); the higher result wins, the loser takes damage, draw = nothing.
/// Then control returns to TurnManager.
/// </summary>
public class CombatManager : NetworkBehaviour
{
    public static CombatManager Instance { get; private set; }

    public enum Side
    {
        None,
        Attacker,
        Defender
    }

    // Shared / attacker side (the attacker is always the active player).
    public NetworkVariable<CombatPhase> Phase = new NetworkVariable<CombatPhase>(CombatPhase.None);
    public NetworkVariable<int> CombatPlayerId = new NetworkVariable<int>(TurnRotation.NoPlayer);
    public NetworkVariable<int> Roll = new NetworkVariable<int>(0);
    public NetworkVariable<int> RollModifier = new NetworkVariable<int>(0);
    public NetworkVariable<int> Required = new NetworkVariable<int>(0);
    public NetworkVariable<int> CardsUsed = new NetworkVariable<int>(0);
    public NetworkVariable<bool> LifeProtected = new NetworkVariable<bool>(false);
    public NetworkVariable<int> LifeLoss = new NetworkVariable<int>(0);
    /// <summary>Last card note as the other players read it ("Chismosa usó Canicas").</summary>
    public NetworkVariable<FixedString128Bytes> LastCardText = new NetworkVariable<FixedString128Bytes>();
    /// <summary>Same note for the player who played it ("Usaste Canicas").</summary>
    public NetworkVariable<FixedString128Bytes> LastCardSelfText = new NetworkVariable<FixedString128Bytes>();
    public NetworkVariable<int> LastCardPlayerId = new NetworkVariable<int>(TurnRotation.NoPlayer);

    // Duel only (defender side).
    public NetworkVariable<bool> IsDuel = new NetworkVariable<bool>(false);
    public NetworkVariable<int> DefenderPlayerId = new NetworkVariable<int>(TurnRotation.NoPlayer);
    public NetworkVariable<int> DefenderRoll = new NetworkVariable<int>(0);
    public NetworkVariable<int> DefenderModifier = new NetworkVariable<int>(0);
    public NetworkVariable<int> DefenderCardsUsed = new NetworkVariable<int>(0);
    public NetworkVariable<bool> DefenderProtected = new NetworkVariable<bool>(false);
    public NetworkVariable<int> DefenderLifeLoss = new NetworkVariable<int>(0);

    private CombatContext context;
    private CombatContext defenderContext;
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
    }

    public override void OnNetworkDespawn()
    {
        if (Instance == this)
            Instance = null;
    }

    // =========================================================
    // LOCAL QUERIES (UI / input)
    // =========================================================

    public bool IsActive => Phase.Value != CombatPhase.None;

    public bool IsDuelParticipant(int playerId)
    {
        return IsActive && IsDuel.Value &&
               (playerId == CombatPlayerId.Value || playerId == DefenderPlayerId.Value);
    }

    /// <summary>Which side the local player fights on in the current encounter.</summary>
    public Side LocalSide
    {
        get
        {
            PlayerNetworkData local = PlayerNetworkData.Local;

            if (!IsActive || local == null || !local.IsAlive)
                return Side.None;

            if (local.PlayerId.Value == CombatPlayerId.Value)
                return Side.Attacker;

            if (IsDuel.Value && local.PlayerId.Value == DefenderPlayerId.Value)
                return Side.Defender;

            return Side.None;
        }
    }

    public bool IsLocalFighter => LocalSide != Side.None;

    public bool HasRolled(Side side) => side == Side.Defender ? DefenderRoll.Value > 0 : Roll.Value > 0;

    private int CardsUsedBy(Side side) => side == Side.Defender ? DefenderCardsUsed.Value : CardsUsed.Value;

    public bool CanLocalUseCard()
    {
        Side side = LocalSide;
        PlayerNetworkData local = PlayerNetworkData.Local;

        return side != Side.None &&
               !local.CardsBlocked &&
               Phase.Value == CombatPhase.CardPhase &&
               !HasRolled(side) &&
               CardsUsedBy(side) < Rules.maxCardsPerCombat &&
               local.Hand.Count > 0;
    }

    public bool CanLocalSkipCards() => !IsDuel.Value && LocalSide == Side.Attacker && Phase.Value == CombatPhase.CardPhase;

    public bool CanLocalRoll()
    {
        Side side = LocalSide;

        if (side == Side.None || HasRolled(side))
            return false;

        return Phase.Value == CombatPhase.CardPhase || Phase.Value == CombatPhase.Rolling;
    }

    // =========================================================
    // SERVER: START / ABORT
    // =========================================================

    /// <param name="forced">Roulette "pelea obligatoria": fight an entity even without an event tile.</param>
    public void ServerBeginCombat(PlayerNetworkData player, Vector2Int cell, bool forced = false)
    {
        if (!IsServer)
            return;

        ResetState(player, false);
        eventCell = cell;
        ApplyRoulettePenalty(player, context);
        SyncContext();

        bool canUseCards = Rules.maxCardsPerCombat > 0 && player.Hand.Count > 0 && !player.CardsBlocked;
        Phase.Value = canUseCards ? CombatPhase.CardPhase : CombatPhase.Rolling;

        string header = forced ? "¡PELEA OBLIGATORIA!" : "¡EVENTO!";
        AnnounceRpc(
            player.PlayerId.Value, header + "\n<size=70%>¡TE HAS ENCONTRADO CON UNA AMENAZA!</size>",
            TurnRotation.NoPlayer, null,
            header + $"\n<size=70%>¡{player.CharacterLabel.ToUpper()} SE HA ENCONTRADO CON UNA AMENAZA!</size>");
        Debug.Log($"{NetworkConnectionManager.GetInstanceTag()} ¡EVENTO! {player.Name} combate en {cell} (requerido {context.Required})");
    }

    public void ServerBeginDuel(PlayerNetworkData attacker, PlayerNetworkData defender)
    {
        if (!IsServer)
            return;

        ResetState(attacker, true);

        defenderContext = new CombatContext(defender.PlayerId.Value, 0, Rules.damageOnFail);
        DefenderPlayerId.Value = defender.PlayerId.Value;
        ApplyRoulettePenalty(attacker, context);
        ApplyRoulettePenalty(defender, defenderContext);
        SyncContext();

        // Cards (if any) can be played until each side rolls.
        Phase.Value = CombatPhase.CardPhase;

        string attackerName = attacker.CharacterLabel;
        string defenderName = defender.CharacterLabel;
        AnnounceRpc(
            attacker.PlayerId.Value, $"¡DUELO!\n<size=70%>Retas a {defenderName}</size>",
            defender.PlayerId.Value, $"¡DUELO!\n<size=70%>¡{attackerName} te reta!</size>",
            $"¡DUELO!\n<size=70%>{attackerName} contra {defenderName}</size>");
        GameStateManager.Instance.ServerBroadcast(
            attacker, $"¡Duelo contra {defenderName}!",
            defender, $"¡{attackerName} te reta a un duelo!",
            $"¡DUELO! {attackerName} contra {defenderName}");
        Debug.Log($"{NetworkConnectionManager.GetInstanceTag()} ¡DUELO! {attacker.Name} vs {defender.Name}");
    }

    private void ResetState(PlayerNetworkData attacker, bool duel)
    {
        StopFinish();

        context = new CombatContext(attacker.PlayerId.Value, duel ? 0 : Rules.requiredCombatRoll, Rules.damageOnFail);
        defenderContext = null;

        IsDuel.Value = duel;
        CombatPlayerId.Value = attacker.PlayerId.Value;
        DefenderPlayerId.Value = TurnRotation.NoPlayer;
        SetLastCard(null, "", "");
        LifeLoss.Value = 0;
        DefenderLifeLoss.Value = 0;
        SyncContext();
    }

    /// <summary>Host only: cancels the current encounter without applying a result.</summary>
    public void ServerAbort()
    {
        if (!IsServer)
            return;

        StopFinish();
        context = null;
        defenderContext = null;
        IsDuel.Value = false;
        Phase.Value = CombatPhase.None;
    }

    /// <summary>Big announcement: the subject, the other duelist and the spectators each read their own text.</summary>
    [Rpc(SendTo.ClientsAndHost)]
    private void AnnounceRpc(int subjectId, string toSubject, int otherId, string toOther, string toOthers)
    {
        string text = GameStateManager.PickPerspective(subjectId, toSubject, otherId, toOther ?? toOthers, toOthers);
        Debug.Log($"{NetworkConnectionManager.GetInstanceTag()} [Anuncio] {text.Replace('\n', ' ')}");

        if (EventUI.Instance != null)
            EventUI.Instance.ShowMessage(text, 2.5f);
    }

    private void SetLastCard(PlayerNetworkData player, string toPlayer, string toOthers)
    {
        LastCardPlayerId.Value = player != null ? player.PlayerId.Value : TurnRotation.NoPlayer;
        LastCardSelfText.Value = toPlayer;
        LastCardText.Value = toOthers;
    }

    /// <summary>The last card note as the local player reads it ("Usaste X" / "Chismosa usó X").</summary>
    public string LocalLastCardText()
    {
        PlayerNetworkData local = PlayerNetworkData.Local;
        bool mine = local != null && local.PlayerId.Value == LastCardPlayerId.Value;
        return (mine ? LastCardSelfText.Value : LastCardText.Value).ToString();
    }

    // =========================================================
    // CARDS (T13) — attacker, or either side in a duel
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
        if (!ValidateParticipant(rpcParams.Receive.SenderClientId, out PlayerNetworkData player, out Side side, "UseCard"))
            return;

        CombatContext sideContext = side == Side.Defender ? defenderContext : context;

        if (player.CardsBlocked)
        {
            Debug.LogWarning($"Combat: UseCard rejected, {player.Name} cannot use cards (roulette).");
            return;
        }

        if (Phase.Value != CombatPhase.CardPhase || sideContext.Roll > 0)
        {
            Debug.LogWarning($"Combat: UseCard rejected, phase is {Phase.Value} or {player.Name} already rolled.");
            return;
        }

        if (sideContext.CardsUsed >= Rules.maxCardsPerCombat)
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

        // Special cards only work in their own kind of fight (or outside combat).
        bool playable = IsDuel.Value ? CardRules.CanPlayInDuel(card) : CardRules.CanPlayInEventCombat(card);

        if (!playable)
        {
            GameStateManager.Instance.ServerMessageTo(rpcParams.Receive.SenderClientId, $"{card.displayName}: {CardRules.TimingHint(card)}");
            return;
        }

        if (card.effect is SpecialCardEffect escape)
        {
            ServerPlayEscapeCard(player, side, sideContext, handIndex, card, escape);
            return;
        }

        player.ServerRemoveCardAt(handIndex);
        card.Apply(sideContext);
        sideContext.CardsUsed++;
        sideContext.AppliedCards.Add(card.displayName);

        SetLastCard(player, $"Usaste {card.displayName}", $"{player.CharacterLabel} usó {card.displayName}");
        SyncContext();
        GameStateManager.Instance.ServerBroadcast(player, $"Usaste {card.displayName}", $"{player.CharacterLabel} usó {card.displayName}");

        if (!IsDuel.Value && context.CardsUsed >= Rules.maxCardsPerCombat)
            Phase.Value = CombatPhase.Rolling;
    }

    /// <summary>
    /// "Canicas" (event) / "Gatos fotocopiados" (duel): chance to leave the fight with no result.
    /// The card is consumed either way; on failure the fight goes on.
    /// </summary>
    private void ServerPlayEscapeCard(PlayerNetworkData player, Side side, CombatContext sideContext, int handIndex, CardData card, SpecialCardEffect escape)
    {
        player.ServerRemoveCardAt(handIndex);
        sideContext.CardsUsed++;
        sideContext.AppliedCards.Add(card.displayName);

        bool escaped = SpecialCardEffect.Succeeds(escape.chance, UnityEngine.Random.value);

        if (escaped)
        {
            string what = IsDuel.Value ? "del duelo" : "de la entidad";
            SetLastCard(player, $"¡Escapaste {what} con {card.displayName}!", $"¡{player.CharacterLabel} escapó {what} con {card.displayName}!");
            SyncContext();
            GameStateManager.Instance.ServerBroadcast(player, $"¡Escapaste {what}! ({card.displayName})", $"¡{player.CharacterLabel} escapó {what}! ({card.displayName})");

            // No damage, no progress; an event tile keeps its event.
            LifeLoss.Value = 0;
            DefenderLifeLoss.Value = 0;
            Phase.Value = CombatPhase.Escaped;
            finishRoutine = StartCoroutine(FinishAfterDelay());
            return;
        }

        SetLastCard(player, $"Usaste {card.displayName}... ¡pero no lograste escapar!", $"{player.CharacterLabel} usó {card.displayName}... ¡pero no logró escapar!");
        SyncContext();
        GameStateManager.Instance.ServerBroadcast(player, $"No lograste escapar ({card.displayName})", $"{player.CharacterLabel} no logró escapar ({card.displayName})");

        if (!IsDuel.Value && context.CardsUsed >= Rules.maxCardsPerCombat)
            Phase.Value = CombatPhase.Rolling;
    }

    [Rpc(SendTo.Server)]
    private void RequestSkipCardsRpc(RpcParams rpcParams = default)
    {
        if (!ValidateParticipant(rpcParams.Receive.SenderClientId, out PlayerNetworkData player, out Side side, "SkipCards"))
            return;

        if (IsDuel.Value || side != Side.Attacker || Phase.Value != CombatPhase.CardPhase)
            return;

        SetLastCard(player, "No usas carta", $"{player.CharacterLabel} no usa carta");
        Phase.Value = CombatPhase.Rolling;
    }

    // =========================================================
    // ROLL + RESOLVE
    // =========================================================

    [Rpc(SendTo.Server)]
    private void RequestCombatRollRpc(RpcParams rpcParams = default)
    {
        if (!ValidateParticipant(rpcParams.Receive.SenderClientId, out PlayerNetworkData player, out Side side, "CombatRoll"))
            return;

        // Duplicate rolls fail here: the phase moved on or this side already rolled.
        if (Phase.Value != CombatPhase.CardPhase && Phase.Value != CombatPhase.Rolling)
        {
            Debug.LogWarning($"Combat: roll rejected, phase is {Phase.Value}.");
            return;
        }

        ServerRollFor(side);
    }

    /// <summary>Host only (timeout): rolls for every side that has not rolled yet.</summary>
    public void ServerForceRoll()
    {
        if (!IsServer || context == null)
            return;

        if (Phase.Value != CombatPhase.CardPhase && Phase.Value != CombatPhase.Rolling)
            return;

        if (context.Roll == 0)
            ServerRollFor(Side.Attacker);

        if (IsDuel.Value && defenderContext != null && defenderContext.Roll == 0 && Phase.Value != CombatPhase.None)
            ServerRollFor(Side.Defender);
    }

    private void ServerRollFor(Side side)
    {
        CombatContext sideContext = side == Side.Defender ? defenderContext : context;

        if (sideContext == null || sideContext.Roll > 0)
        {
            Debug.LogWarning($"Combat: {side} already rolled.");
            return;
        }

        sideContext.Roll = UnityEngine.Random.Range(Rules.diceMin, Rules.diceMax + 1);
        SyncContext();

        if (!IsDuel.Value)
        {
            ServerResolveEvent();
            return;
        }

        if (context.Roll > 0 && defenderContext.Roll > 0)
            ServerResolveDuel();
    }

    private void ServerResolveEvent()
    {
        PlayerNetworkData player = PlayerNetworkData.GetByPlayerId(CombatPlayerId.Value);
        Phase.Value = CombatPhase.Resolving;

        CombatOutcome outcome = CombatResolver.Resolve(context);
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
            ApplyDamage(player, outcome.LifeLoss);
            Phase.Value = CombatPhase.Lost;
        }

        Debug.Log($"{NetworkConnectionManager.GetInstanceTag()} Combate {player.Name}: {context.Roll} {context.RollModifier:+0;-0;+0} = {context.Result} vs {context.Required} -> {(outcome.Won ? "SUPERADO" : "FALLADO")} (vidas -{outcome.LifeLoss})");

        finishRoutine = StartCoroutine(FinishAfterDelay());
    }

    private void ServerResolveDuel()
    {
        PlayerNetworkData attacker = PlayerNetworkData.GetByPlayerId(CombatPlayerId.Value);
        PlayerNetworkData defender = PlayerNetworkData.GetByPlayerId(DefenderPlayerId.Value);
        Phase.Value = CombatPhase.Resolving;

        DuelOutcome outcome = DuelResolver.Resolve(context, defenderContext);
        LifeLoss.Value = outcome.AttackerLifeLoss;
        DefenderLifeLoss.Value = outcome.DefenderLifeLoss;

        ApplyDamage(attacker, outcome.AttackerLifeLoss);
        ApplyDamage(defender, outcome.DefenderLifeLoss);

        Phase.Value = outcome.Winner > 0 ? CombatPhase.Won : outcome.Winner < 0 ? CombatPhase.Lost : CombatPhase.Draw;

        Debug.Log($"{NetworkConnectionManager.GetInstanceTag()} Duelo {attacker.Name} {context.Result} vs {defender.Name} {defenderContext.Result} -> {Phase.Value}");

        finishRoutine = StartCoroutine(FinishAfterDelay());
    }

    private static void ApplyDamage(PlayerNetworkData player, int amount)
    {
        if (player == null || amount <= 0)
            return;

        player.Lives.Value = Mathf.Max(0, player.Lives.Value - amount);

        if (player.Lives.Value <= 0)
            GameStateManager.Instance.ServerEliminate(player);
    }

    private IEnumerator FinishAfterDelay()
    {
        yield return new WaitForSeconds(Rules.resultDisplaySeconds);
        finishRoutine = null;

        context = null;
        defenderContext = null;
        IsDuel.Value = false;
        Phase.Value = CombatPhase.None;
        TurnManager.Instance.ServerEndTurn();
    }

    // =========================================================
    // HELPERS
    // =========================================================

    /// <summary>Sender must be Alive and be the attacker (active player) or, in a duel, the defender.</summary>
    private bool ValidateParticipant(ulong sender, out PlayerNetworkData player, out Side side, string action)
    {
        side = Side.None;
        player = PlayerNetworkData.GetByClientId(sender);

        if (player == null || !player.IsAlive || context == null || TurnManager.Instance.State.Value != TurnState.Combat)
        {
            Debug.LogWarning($"Combat: {action} rejected, no active encounter for client {sender}.");
            return false;
        }

        if (player.PlayerId.Value == CombatPlayerId.Value && player.PlayerId.Value == TurnManager.Instance.CurrentPlayerId.Value)
            side = Side.Attacker;
        else if (IsDuel.Value && player.PlayerId.Value == DefenderPlayerId.Value)
            side = Side.Defender;

        if (side == Side.None)
        {
            Debug.LogWarning($"Combat: {action} rejected, {player.Name} is not in this encounter.");
            return false;
        }

        return true;
    }

    /// <summary>Roulette "-daño la siguiente ronda": lowers this side's combat result.</summary>
    private static void ApplyRoulettePenalty(PlayerNetworkData player, CombatContext sideContext)
    {
        if (player != null && sideContext != null)
            sideContext.RollModifier -= player.ActiveDamagePenalty;
    }

    private void SyncContext()
    {
        Roll.Value = context.Roll;
        RollModifier.Value = context.RollModifier;
        Required.Value = context.Required;
        CardsUsed.Value = context.CardsUsed;
        LifeProtected.Value = context.DamageIfDefeated == 0;

        if (defenderContext != null)
        {
            DefenderRoll.Value = defenderContext.Roll;
            DefenderModifier.Value = defenderContext.RollModifier;
            DefenderCardsUsed.Value = defenderContext.CardsUsed;
            DefenderProtected.Value = defenderContext.DamageIfDefeated == 0;
        }
        else
        {
            DefenderRoll.Value = 0;
            DefenderModifier.Value = 0;
            DefenderCardsUsed.Value = 0;
            DefenderProtected.Value = false;
        }
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
