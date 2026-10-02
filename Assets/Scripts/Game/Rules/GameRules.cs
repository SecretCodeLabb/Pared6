using UnityEngine;

/// <summary>
/// Single place for every tunable game value (AGENT_CONTEXT §6).
/// The asset lives in Resources/GameRules.asset; edit it in the Inspector.
/// </summary>
[CreateAssetMenu(fileName = "GameRules", menuName = "PPOP/Game Rules")]
public class GameRules : ScriptableObject
{
    [Header("Players")]
    [Min(1)] public int minPlayers = 2;
    [Min(1)] public int maxPlayers = 4;
    [Min(1)] public int startingLives = 3;

    [Header("Network")]
    public ushort port = 7777;

    [Header("Turns")]
    [Tooltip("If false, PlayerId 0 always starts.")]
    public bool randomStartingPlayer = false;
    [Tooltip("0 = disabled. Seconds before the active player's turn is auto-resolved.")]
    [Min(0f)] public float turnTimeoutSeconds = 0f;
    [Min(0f)] public float turnEndDelaySeconds = 0.8f;

    [Header("Dice")]
    public int diceMin = 1;
    public int diceMax = 6;

    [Header("Board")]
    [Range(1, 20)] public int eventTileCount = 5;

    [Header("Combat")]
    public int requiredCombatRoll = 4;
    [Min(0)] public int damageOnFail = 1;
    public bool consumeOnSuccess = true;
    [Min(0)] public int maxCardsPerCombat = 1;
    [Min(0f)] public float resultDisplaySeconds = 2.5f;

    [Header("Cards")]
    [Min(0)] public int startingHandSize = 3;
    public bool allowDuplicateCards = true;

    [Header("Victory")]
    [Min(1)] public int requiredEventsToWin = 3;

    private static GameRules cached;

    /// <summary>Loads Resources/GameRules, or falls back to defaults if the asset is missing.</summary>
    public static GameRules Instance
    {
        get
        {
            if (cached == null)
            {
                cached = Resources.Load<GameRules>("GameRules");

                if (cached == null)
                {
                    Debug.LogError("GameRules: Resources/GameRules.asset not found. Using default values.");
                    cached = CreateInstance<GameRules>();
                }
            }

            return cached;
        }
    }
}
