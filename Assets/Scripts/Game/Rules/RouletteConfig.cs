using System;
using System.Collections.Generic;
using UnityEngine;

public enum RouletteEffectType
{
    Nothing,
    ExtraMoves,          // amount = extra steps right now
    LessMovesNextRoll,   // amount = subtracted from the next movement roll
    LoseTurn,            // amount = turns skipped
    NoCardsNextTurn,     // cards blocked until the end of the player's next turn
    DamagePenalty,       // amount = subtracted from combat rolls until the end of the next turn
    LoseLife,            // amount = lives lost
    ForcedFight          // mandatory combat against an entity
}

[Serializable]
public class RouletteOption
{
    [Tooltip("Text on the wheel segment (short; use \\n for two lines).")]
    public string label = "NADA";

    [Tooltip("Text shown when this option wins.")]
    public string resultText = "No pasa nada";

    public Color color = Color.gray;

    [Tooltip("Relative probability. Chance = weight / sum of all weights.")]
    [Min(0f)] public float weight = 1f;

    public RouletteEffectType effect = RouletteEffectType.Nothing;

    [Min(0)] public int amount = 1;
}

/// <summary>
/// Roulette options and timings. Lives in Resources/RouletteConfig.asset:
/// edit labels, colours, weights and effects there without touching code.
/// </summary>
[CreateAssetMenu(fileName = "RouletteConfig", menuName = "PPOP/Roulette Config")]
public class RouletteConfig : ScriptableObject
{
    public const string ResourcePath = "RouletteConfig";

    [Tooltip("Seconds the wheel animation lasts.")]
    [Min(0.5f)] public float spinSeconds = 3.5f;

    [Tooltip("Seconds the result stays on screen before the effect is applied.")]
    [Min(0f)] public float resultSeconds = 1.8f;

    [Tooltip("True: segment size reflects its probability. False: equal segments (classic casino look).")]
    public bool segmentSizeByWeight = false;

    public List<RouletteOption> options = new List<RouletteOption>();

    private static RouletteConfig cached;

    public static RouletteConfig Instance
    {
        get
        {
            if (cached == null)
            {
                cached = Resources.Load<RouletteConfig>(ResourcePath);

                if (cached == null)
                {
                    Debug.LogError("RouletteConfig: Resources/" + ResourcePath + ".asset not found. Using defaults.");
                    cached = CreateInstance<RouletteConfig>();
                    cached.options = CreateDefaultOptions();
                }
            }

            return cached;
        }
    }

    public float TotalWeight
    {
        get
        {
            float total = 0f;

            foreach (RouletteOption option in options)
                total += Mathf.Max(0f, option.weight);

            return total;
        }
    }

    /// <summary>Probability (0..1) of the option at index.</summary>
    public float Chance(int index)
    {
        float total = TotalWeight;
        return total > 0f ? Mathf.Max(0f, options[index].weight) / total : 0f;
    }

    /// <summary>
    /// Weighted pick. roll01 must be in [0, 1). Options with weight 0 never win.
    /// Returns -1 if there is no option with positive weight.
    /// </summary>
    public static int Pick(IReadOnlyList<RouletteOption> options, double roll01)
    {
        double total = 0;

        foreach (RouletteOption option in options)
            total += Math.Max(0f, option.weight);

        if (total <= 0)
            return -1;

        double target = roll01 * total;
        double cumulative = 0;
        int last = -1;

        for (int i = 0; i < options.Count; i++)
        {
            float weight = Math.Max(0f, options[i].weight);

            if (weight <= 0f)
                continue;

            last = i;
            cumulative += weight;

            if (target < cumulative)
                return i;
        }

        return last;
    }

    /// <summary>Default wheel: movement, penalties, a rare life loss, forced fight and "nothing".</summary>
    public static List<RouletteOption> CreateDefaultOptions()
    {
        Color good = new Color(0.20f, 0.65f, 0.30f);
        Color bad = new Color(0.80f, 0.45f, 0.15f);
        Color penalty = new Color(0.55f, 0.25f, 0.70f);
        Color danger = new Color(0.80f, 0.12f, 0.15f);
        Color neutral = new Color(0.35f, 0.35f, 0.40f);

        return new List<RouletteOption>
        {
            Option("NADA", "No pasa nada", neutral, 20f, RouletteEffectType.Nothing, 0),
            Option("+1\nMOV", "¡+1 movimiento ahora!", good, 10f, RouletteEffectType.ExtraMoves, 1),
            Option("-1\nMOV", "-1 en tu próxima tirada", bad, 8f, RouletteEffectType.LessMovesNextRoll, 1),
            Option("+2\nMOV", "¡+2 movimientos ahora!", good, 7f, RouletteEffectType.ExtraMoves, 2),
            Option("PIERDES\nTURNO", "Pierdes tu próximo turno", penalty, 5f, RouletteEffectType.LoseTurn, 1),
            Option("-2\nMOV", "-2 en tu próxima tirada", bad, 6f, RouletteEffectType.LessMovesNextRoll, 2),
            Option("¡PELEA!", "¡Una entidad te ataca!", danger, 8f, RouletteEffectType.ForcedFight, 0),
            Option("+3\nMOV", "¡+3 movimientos ahora!", good, 4f, RouletteEffectType.ExtraMoves, 3),
            Option("SIN\nCARTAS", "No puedes usar cartas la siguiente ronda", penalty, 6f, RouletteEffectType.NoCardsNextTurn, 1),
            Option("-1\nDAÑO", "-1 de daño la siguiente ronda", penalty, 6f, RouletteEffectType.DamagePenalty, 1),
            Option("-3\nMOV", "-3 en tu próxima tirada", bad, 4f, RouletteEffectType.LessMovesNextRoll, 3),
            Option("-2\nDAÑO", "-2 de daño la siguiente ronda", penalty, 4f, RouletteEffectType.DamagePenalty, 2),
            Option("-1 ♥", "¡Pierdes una vida!", danger, 2f, RouletteEffectType.LoseLife, 1)
        };
    }

    private static RouletteOption Option(string label, string result, Color color, float weight, RouletteEffectType effect, int amount)
    {
        return new RouletteOption
        {
            label = label,
            resultText = result,
            color = color,
            weight = weight,
            effect = effect,
            amount = amount
        };
    }
}
