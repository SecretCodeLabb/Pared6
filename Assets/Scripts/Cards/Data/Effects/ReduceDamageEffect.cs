using UnityEngine;

/// <summary>Resta n al daño recibido en este combate.</summary>
[CreateAssetMenu(fileName = "ReduceDamageEffect", menuName = "PPOP/Cards/Effects/ReduceDamage")]
public class ReduceDamageEffect : CardEffect
{
    [Min(0)] public int amount = 1;

    public override void Apply(CombatContext context)
    {
        context.DamageReduction += amount;
    }
}
