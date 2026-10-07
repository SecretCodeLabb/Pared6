using UnityEngine;

/// <summary>
/// Card category as data: adding a new type = creating a new asset, no code changes.
/// Default types: Daño, Defensa, Habilidad, Inútil.
/// </summary>
[CreateAssetMenu(fileName = "CardType", menuName = "PPOP/Cards/Card Type")]
public class CardType : ScriptableObject
{
    public string displayName = "Tipo";
    public Color color = Color.gray;

    [Tooltip("Card art for this type (Assets/Resources/CardsDesign). Name goes on the banner, description in the black box.")]
    public Sprite frame;
}
