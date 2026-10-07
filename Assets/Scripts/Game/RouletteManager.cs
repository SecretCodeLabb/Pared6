using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Casino-style roulette after a movement that ended without combat or duel.
/// Host-authoritative: the active player asks to spin, the Host picks a weighted option
/// (Resources/RouletteConfig), every instance animates the same result, then the Host applies
/// the effect and hands control back to TurnManager. The wheel's look lives in RouletteWheelView.
/// </summary>
public class RouletteManager : NetworkBehaviour
{
    public static RouletteManager Instance { get; private set; }

    public NetworkVariable<RoulettePhase> Phase = new NetworkVariable<RoulettePhase>(RoulettePhase.Hidden);
    public NetworkVariable<int> PlayerId = new NetworkVariable<int>(TurnRotation.NoPlayer);
    public NetworkVariable<int> ResultIndex = new NetworkVariable<int>(-1);

    /// <summary>Incremented on every spin so clients know when to start the animation.</summary>
    public NetworkVariable<int> SpinId = new NetworkVariable<int>(0);

    private Coroutine routine;

    private static RouletteConfig Config => RouletteConfig.Instance;

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

    public bool IsVisible => IsSpawned && Phase.Value != RoulettePhase.Hidden;

    public RouletteOption Result =>
        ResultIndex.Value >= 0 && ResultIndex.Value < Config.options.Count ? Config.options[ResultIndex.Value] : null;

    public bool CanLocalSpin()
    {
        PlayerNetworkData local = PlayerNetworkData.Local;

        return IsSpawned &&
               Phase.Value == RoulettePhase.WaitingSpin &&
               local != null && local.IsAlive &&
               local.PlayerId.Value == PlayerId.Value;
    }

    public void RequestSpinFromLocal()
    {
        if (CanLocalSpin())
            RequestSpinRpc();
    }

    // =========================================================
    // SERVER FLOW
    // =========================================================

    /// <summary>Host only: shows the wheel for the active player (TurnState.Roulette).</summary>
    public void ServerBegin(PlayerNetworkData player)
    {
        if (!IsServer)
            return;

        StopRoutine();
        PlayerId.Value = player.PlayerId.Value;
        ResultIndex.Value = -1;
        Phase.Value = RoulettePhase.WaitingSpin;
    }

    [Rpc(SendTo.Server)]
    private void RequestSpinRpc(RpcParams rpcParams = default)
    {
        if (!TurnManager.Instance.ValidateActivePlayer(rpcParams.Receive.SenderClientId, TurnState.Roulette, out PlayerNetworkData player, "Spin"))
            return;

        // Duplicate requests are ignored: the phase already left WaitingSpin.
        if (Phase.Value != RoulettePhase.WaitingSpin || player.PlayerId.Value != PlayerId.Value)
        {
            Debug.LogWarning($"Roulette: spin rejected for {player.Name} (phase {Phase.Value}).");
            return;
        }

        ServerSpin(player);
    }

    /// <summary>Host only (turn timeout): spins for the player.</summary>
    public void ServerForceSpin()
    {
        if (!IsServer || Phase.Value != RoulettePhase.WaitingSpin)
            return;

        PlayerNetworkData player = PlayerNetworkData.GetByPlayerId(PlayerId.Value);

        if (player != null)
            ServerSpin(player);
    }

    private void ServerSpin(PlayerNetworkData player)
    {
        int index = RouletteConfig.Pick(Config.options, Random.value * 0.999999f);

        if (index < 0)
        {
            Debug.LogError("Roulette: RouletteConfig has no option with weight > 0.");
            ServerFinish(player, null);
            return;
        }

        ResultIndex.Value = index;
        Phase.Value = RoulettePhase.Spinning;
        SpinId.Value++;

        Debug.Log($"{NetworkConnectionManager.GetInstanceTag()} Ruleta {player.Name}: {Config.options[index].label.Replace('\n', ' ')} ({Config.Chance(index):P0})");

        routine = StartCoroutine(ResolveAfterSpin(player, Config.options[index]));
    }

    private IEnumerator ResolveAfterSpin(PlayerNetworkData player, RouletteOption option)
    {
        yield return new WaitForSeconds(Config.spinSeconds);
        Phase.Value = RoulettePhase.ShowingResult;

        yield return new WaitForSeconds(Config.resultSeconds);
        routine = null;
        ServerFinish(player, option);
    }

    /// <summary>Host only: hides the wheel without applying anything (disconnect, game over).</summary>
    public void ServerAbort()
    {
        if (!IsServer)
            return;

        StopRoutine();
        Phase.Value = RoulettePhase.Hidden;
    }

    // =========================================================
    // EFFECTS
    // =========================================================

    private void ServerFinish(PlayerNetworkData player, RouletteOption option)
    {
        Phase.Value = RoulettePhase.Hidden;

        TurnManager turns = TurnManager.Instance;

        if (turns.State.Value != TurnState.Roulette || player == null || !player.IsAlive)
            return;

        GameStateManager game = GameStateManager.Instance;
        RouletteEffectType effect = option != null ? option.effect : RouletteEffectType.Nothing;
        int amount = option != null ? Mathf.Max(0, option.amount) : 0;

        if (option != null)
            game.ServerBroadcast($"Ruleta · {player.Name}: {option.resultText}");

        switch (effect)
        {
            case RouletteEffectType.ExtraMoves:
                // Extra steps right now; the turn continues in Moving (no second spin this turn).
                turns.ServerGrantExtraMoves(player, amount);
                return;

            case RouletteEffectType.ForcedFight:
                turns.ServerStartForcedFight(player);
                return;

            case RouletteEffectType.LessMovesNextRoll:
                player.MovePenalty.Value += amount;
                break;

            case RouletteEffectType.LoseTurn:
                player.SkipTurns.Value += Mathf.Max(1, amount);
                break;

            case RouletteEffectType.NoCardsNextTurn:
                // 2 = rest of this turn + the player's next turn.
                player.CardsBlockedTurns.Value = Mathf.Max(player.CardsBlockedTurns.Value, 2);
                break;

            case RouletteEffectType.DamagePenalty:
                player.DamagePenalty.Value = Mathf.Max(player.DamagePenalty.Value, amount);
                player.DamagePenaltyTurns.Value = 2;
                break;

            case RouletteEffectType.LoseLife:
                player.Lives.Value = Mathf.Max(0, player.Lives.Value - Mathf.Max(1, amount));

                if (player.Lives.Value <= 0)
                    game.ServerEliminate(player);
                break;
        }

        turns.ServerEndTurn();
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
