using UnityEngine;

/// <summary>Evita perder la vida si se falla el combate.</summary>
[CreateAssetMenu(fileName = "PreventLifeLossEffect", menuName = "PPOP/Cards/Effects/PreventLifeLoss")]
public class PreventLifeLossEffect : CardEffect
{
    public override void Apply(CombatContext context)
    {
        context.PreventLifeLoss = true;
    }
}
