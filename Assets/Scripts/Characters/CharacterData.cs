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

    [Tooltip("Special ability used with the button next to the character card (None = no button).")]
    public CharacterAbility ability = CharacterAbility.None;
    [Tooltip("Ability value (Skater: extra steps).")]
    public int abilityAmount = 2;
    [Tooltip("Probability (0-1) that the ability fails (Skater: then the next turn is lost).")]
    [Range(0f, 1f)] public float abilityFailChance = 0.65f;
    [Tooltip("Estudiante: chance added by every escape that the next fight against an entity is against 2 entities in a row.")]
    [Range(0f, 1f)] public float abilityStackChance = 0.25f;

    [Tooltip("Pawn tint on the board (network game). Assigned by PPOP/Setup Prototype if empty.")]
    public Color boardColor = Color.clear;

    [Tooltip("Side room style for this character (room prefab with RoomBuilder: tile prefab + decorations). Used when the character is in the match.")]
    public GameObject roomPrefab;

    [Tooltip("Mission played on the mission tile of this character's room. Completing it wins the game.")]
    public MissionDefinition mission;
}
