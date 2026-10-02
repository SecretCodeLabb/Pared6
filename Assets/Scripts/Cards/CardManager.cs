using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Host-side card dealing (T12). Hands are stored as card ids in PlayerNetworkData.Hand
/// (owner-only), everyone else sees HandCount. No card draw during the game.
/// </summary>
public class CardManager : MonoBehaviour
{
    public static CardManager Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>Host only: deals startingHandSize random cards to every player.</summary>
    public void ServerDealHands()
    {
        GameRules rules = GameRules.Instance;
        List<int> pool = CardDatabase.Instance.GetAllIds();

        if (pool.Count == 0)
        {
            Debug.LogError("CardManager: CardDatabase is empty. Run PPOP/Create Example Cards.");
            return;
        }

        System.Random rng = new System.Random();

        foreach (PlayerNetworkData player in PlayerNetworkData.All)
        {
            List<int> hand = CardDatabase.DealHand(pool, rules.startingHandSize, rules.allowDuplicateCards, rng);
            player.ServerSetHand(hand);
            Debug.Log($"{NetworkConnectionManager.GetInstanceTag()} Mano de {player.Name}: {string.Join(", ", hand)}");
        }
    }
}
