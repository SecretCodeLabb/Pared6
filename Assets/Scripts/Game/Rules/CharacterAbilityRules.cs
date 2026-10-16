using UnityEngine;

/// <summary>Character special ability (one per character, button next to the character card).</summary>
public enum CharacterAbility : byte
{
    None,
    /// <summary>Skater: +N steps while moving; may fail and cost the next turn.</summary>
    SkaterSprint,
    /// <summary>Estudiante: escape a fight against an entity (not a duel); every escape raises the chance of a double fight.</summary>
    StudentEscape
}

public struct AbilityOutcome
{
    public bool Success;
    public int ExtraMoves;
    public int SkipTurns;
}

/// <summary>Pure rules of the character abilities (testable, no Unity objects).</summary>
public static class CharacterAbilityRules
{
    /// <summary>Abilities used with the button next to the character card (the rest live in the combat panel).</summary>
    public static bool UsesTurnButton(CharacterAbility ability) => ability == CharacterAbility.SkaterSprint;

    /// <summary>
    /// Estudiante's ESCAPAR: only against entities (never a duel), before rolling, and not in the
    /// double fight caused by the escapes themselves.
    /// </summary>
    public static bool CanEscapeCombat(CharacterAbility ability, bool isDuel, CombatPhase phase, bool rolled, bool doubleFight)
    {
        return ability == CharacterAbility.StudentEscape && !isDuel && !rolled && !doubleFight &&
               (phase == CombatPhase.CardPhase || phase == CombatPhase.Rolling);
    }

    /// <summary>Chance that the next fight against an entity is against 2 entities in a row.</summary>
    public static float DoubleFightChance(int escapes, float chancePerEscape) => Mathf.Clamp01(Mathf.Max(0, escapes) * chancePerEscape);

    /// <summary>random01 in [0, 1): below the accumulated chance the fight is doubled.</summary>
    public static bool IsDoubleFight(int escapes, float chancePerEscape, float random01) => random01 < DoubleFightChance(escapes, chancePerEscape);

    /// <summary>When the ability button works: own turn, once per turn, and the ability's own moment.</summary>
    public static bool CanUse(CharacterAbility ability, TurnState state, int movesRemaining, bool wallWalk, bool usedThisTurn)
    {
        if (usedThisTurn)
            return false;

        switch (ability)
        {
            case CharacterAbility.SkaterSprint:
                // After rolling, while there are steps left (not during Cívica's exact wall walk).
                return state == TurnState.Moving && movesRemaining > 0 && !wallWalk;
            default:
                return false;
        }
    }

    /// <summary>random01 in [0, 1): below failChance the ability fails.</summary>
    public static AbilityOutcome Resolve(CharacterAbility ability, float random01, int amount, float failChance)
    {
        AbilityOutcome outcome = new AbilityOutcome();

        if (ability != CharacterAbility.SkaterSprint)
            return outcome;

        outcome.Success = random01 >= failChance;

        if (outcome.Success)
            outcome.ExtraMoves = amount;
        else
            outcome.SkipTurns = 1;

        return outcome;
    }
}
