using UnityEngine;

/// <summary>
/// Base class for card effects. A new effect = a new subclass + asset;
/// CombatManager never needs to change.
/// </summary>
public abstract class CardEffect : ScriptableObject
{
    public abstract void Apply(CombatContext context);
}
