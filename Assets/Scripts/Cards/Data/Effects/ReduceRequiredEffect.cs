using UnityEngine;

/// <summary>Baja el valor requerido en n.</summary>
[CreateAssetMenu(fileName = "ReduceRequiredEffect", menuName = "PPOP/Cards/Effects/ReduceRequired")]
public class ReduceRequiredEffect : CardEffect
{
    [Min(0)] public int amount = 1;

    public override void Apply(CombatContext context)
    {
        context.RequiredReduction += amount;
    }

    public override string ShortLabel => "-" + amount;
}
