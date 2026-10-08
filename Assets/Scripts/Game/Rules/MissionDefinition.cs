using UnityEngine;

public enum MissionPhase : byte
{
    None,
    Active,
    Completed,
    Failed
}

/// <summary>
/// A character's mission (one per character, assigned in CharacterData.mission).
/// Reaching the mission tile in the character's room starts it; completing it wins the game.
/// To add the real minigame later: make a prefab whose root has a MissionMinigame component
/// and assign it to <see cref="minigamePrefab"/> — no network or turn code changes needed.
/// </summary>
[CreateAssetMenu(fileName = "Mission", menuName = "PPOP/Mission Definition")]
public class MissionDefinition : ScriptableObject
{
    public string title = "Misión";
    [TextArea] public string description = "Descripción de la misión.";
    public Sprite icon;

    [Tooltip("UI prefab with a MissionMinigame component. Empty = placeholder minigame.")]
    public GameObject minigamePrefab;

    [Tooltip("Optional 3D marker placed on the mission tile. Empty = default glowing pillar.")]
    public GameObject tileMarkerPrefab;
}
