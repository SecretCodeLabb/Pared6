using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// All cards that can be dealt. Lives in Resources/Cards/CardDatabase.asset.
/// Adding a CardData to the list includes it in the deal.
/// </summary>
[CreateAssetMenu(fileName = "CardDatabase", menuName = "PPOP/Cards/Card Database")]
public class CardDatabase : ScriptableObject
{
    public const string ResourcePath = "Cards/CardDatabase";

    public List<CardData> cards = new List<CardData>();

    private static CardDatabase cached;

    public static CardDatabase Instance
    {
        get
        {
            if (cached == null)
            {
                cached = Resources.Load<CardDatabase>(ResourcePath);

                if (cached == null)
                {
                    Debug.LogError("CardDatabase: Resources/" + ResourcePath + ".asset not found.");
                    cached = CreateInstance<CardDatabase>();
                }
            }

            return cached;
        }
    }

    public CardData GetById(int id)
    {
        foreach (CardData card in cards)
        {
            if (card != null && card.id == id)
                return card;
        }

        return null;
    }

    public List<int> GetAllIds()
    {
        List<int> ids = new List<int>();

        foreach (CardData card in cards)
        {
            if (card != null)
                ids.Add(card.id);
        }

        return ids;
    }

    /// <summary>Random hand of card ids using the given RNG.</summary>
    public static List<int> DealHand(IReadOnlyList<int> pool, int handSize, bool allowDuplicates, System.Random rng)
    {
        List<int> hand = new List<int>();
        List<int> remaining = new List<int>(pool);

        for (int i = 0; i < handSize; i++)
        {
            if (remaining.Count == 0)
                break;

            int index = rng.Next(remaining.Count);
            hand.Add(remaining[index]);

            if (!allowDuplicates)
                remaining.RemoveAt(index);
        }

        return hand;
    }
}
