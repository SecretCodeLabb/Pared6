using UnityEngine;

[CreateAssetMenu(fileName = "Card", menuName = "PPOP/Cards/Card Data")]
public class CardData : ScriptableObject
{
    [Tooltip("Unique id, synchronized over the network.")]
    public int id;
    public string displayName;
    public CardType type;
    [TextArea] public string description;
    public Sprite icon;
    [Tooltip("Complete card image with the name and text already drawn on it. When set, the hand shows only this image (no frame, name or description on top).")]
    public Sprite fullArt;
    [Tooltip("Optional. Cards without effect can be played and are consumed without changing anything.")]
    public CardEffect effect;

    public string TypeName => type != null ? type.displayName : "-";

    public void Apply(CombatContext context)
    {
        if (effect != null)
            effect.Apply(context);
    }
}
