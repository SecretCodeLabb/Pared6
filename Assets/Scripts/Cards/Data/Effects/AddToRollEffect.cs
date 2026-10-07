using UnityEngine;

/// <summary>Suma n al resultado de la tirada.</summary>
[CreateAssetMenu(fileName = "AddToRollEffect", menuName = "PPOP/Cards/Effects/AddToRoll")]
public class AddToRollEffect : CardEffect
{
    [Min(0)] public int amount = 1;

    public override void Apply(CombatContext context)
    {
        context.RollModifier += amount;
    }

    public override string ShortLabel => "+" + amount;
}
