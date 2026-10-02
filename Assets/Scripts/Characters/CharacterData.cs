using UnityEngine;

[CreateAssetMenu(fileName = "NewCharacterData", menuName = "Game/Character Data")]
public class CharacterData : ScriptableObject
{
    public string characterName;
    public Sprite characterImage;
    [TextArea] public string description;
    public int maxLives = 3;
    public int baseDamage;
    public int baseDefense;
    public string abilityName;
    [TextArea] public string abilityDescription;

    [Tooltip("Pawn tint on the board (network game). Assigned by PPOP/Setup Prototype if empty.")]
    public Color boardColor = Color.clear;
}
