using UnityEngine;

/// <summary>Defense card: subtracts n from the rival's number (the other player's result in a duel, the entity's target number in an event).</summary>
[CreateAssetMenu(fileName = "DefenseEffect", menuName = "PPOP/Cards/Effects/Defense")]
public class DefenseEffect : CardEffect
{
    [Min(0)] public int amount = 1;

    public override void Apply(CombatContext context)
    {
        context.RivalReduction += amount;
    }

    public override string ShortLabel => "+" + amount;
}
