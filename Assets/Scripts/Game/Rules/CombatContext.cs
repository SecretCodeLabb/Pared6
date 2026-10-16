using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mutable state of one combat. Card effects modify it; CombatResolver reads it.
/// resultado = tirada + modificador, compared against requerido.
/// </summary>
public class CombatContext
{
    public int PlayerId;
    public int Roll;
    public int RollModifier;
    public int BaseRequired;
    public int RequiredReduction;
    public int DamageOnFail = 1;
    public int DamageReduction;
    /// <summary>Defense cards: subtracted from the rival's number (other player's result or the entity's target).</summary>
    public int RivalReduction;
    public bool PreventLifeLoss;
    public int CardsUsed;
    public readonly List<string> AppliedCards = new List<string>();

    public CombatContext(int playerId, int baseRequired, int damageOnFail)
    {
        PlayerId = playerId;
        BaseRequired = baseRequired;
        DamageOnFail = damageOnFail;
    }

    public int Result => Roll + RollModifier;

    public int Required => Mathf.Max(0, BaseRequired - RequiredReduction - RivalReduction);

    /// <summary>This side's result in a duel, after the rival's defense cards.</summary>
    public int ResultAgainst(CombatContext rival) => Mathf.Max(0, Result - (rival != null ? rival.RivalReduction : 0));

    public bool IsSuccess => Result >= Required;

    /// <summary>Lives lost if the combat is resolved now.</summary>
    public int LifeLoss => IsSuccess ? 0 : DamageIfDefeated;

    /// <summary>Lives this side loses when defeated, after its own defence cards.</summary>
    public int DamageIfDefeated => PreventLifeLoss ? 0 : Mathf.Max(0, DamageOnFail - DamageReduction);
}

public struct DuelOutcome
{
    /// <summary>1 = attacker wins, -1 = defender wins, 0 = draw.</summary>
    public int Winner;
    public int AttackerLifeLoss;
    public int DefenderLifeLoss;
}

/// <summary>
/// Duel between two players with the same dice system: each side's result = roll + modifier,
/// minus the rival's defense cards. Higher result wins; the loser takes damage. Draw = no damage.
/// </summary>
public static class DuelResolver
{
    public static DuelOutcome Resolve(CombatContext attacker, CombatContext defender)
    {
        DuelOutcome outcome = new DuelOutcome();

        int attackerResult = attacker.ResultAgainst(defender);
        int defenderResult = defender.ResultAgainst(attacker);

        if (attackerResult > defenderResult)
        {
            outcome.Winner = 1;
            outcome.DefenderLifeLoss = defender.DamageIfDefeated;
        }
        else if (defenderResult > attackerResult)
        {
            outcome.Winner = -1;
            outcome.AttackerLifeLoss = attacker.DamageIfDefeated;
        }

        return outcome;
    }
}

public struct CombatOutcome
{
    public bool Won;
    public int LifeLoss;
    public int ProgressGain;
}

public static class CombatResolver
{
    public static CombatOutcome Resolve(CombatContext context)
    {
        bool won = context.IsSuccess;

        return new CombatOutcome
        {
            Won = won,
            LifeLoss = context.LifeLoss,
            ProgressGain = won ? 1 : 0
        };
    }
}
