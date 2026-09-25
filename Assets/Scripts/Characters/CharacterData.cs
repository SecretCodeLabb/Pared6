using UnityEngine;

[CreateAssetMenu(fileName = "NewCharacterData", menuName = "Game/Character Data")]
public class CharacterData : ScriptableObject
{
    public string characterName;
    [TextArea] public string description;
    public int maxLives = 3;
    public int baseDamage;
    public int baseDefense;
    public string abilityName;
    [TextArea] public string abilityDescription;
}
