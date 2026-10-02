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
    [Tooltip("Optional. Cards without effect can be played and are consumed without changing anything.")]
    public CardEffect effect;

    public string TypeName => type != null ? type.displayName : "-";

    public void Apply(CombatContext context)
    {
        if (effect != null)
            effect.Apply(context);
    }
}
