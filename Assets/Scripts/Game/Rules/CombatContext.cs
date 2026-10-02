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

    public int Required => Mathf.Max(0, BaseRequired - RequiredReduction);

    public bool IsSuccess => Result >= Required;

    /// <summary>Lives lost if the combat is resolved now.</summary>
    public int LifeLoss
    {
        get
        {
            if (IsSuccess || PreventLifeLoss)
                return 0;

            return Mathf.Max(0, DamageOnFail - DamageReduction);
        }
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
