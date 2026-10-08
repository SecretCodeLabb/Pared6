using System;
using UnityEngine;

/// <summary>
/// Base class for a character's mission minigame (UI prefab, shown on the HUD canvas).
/// How to add a real minigame:
///   1. Create a UI prefab whose root has a RectTransform and a component deriving from this class.
///   2. Build the game in <see cref="OnBegin"/>; call <see cref="Finish"/>(true/false) when it ends
///      (only matters on the mission player's machine: <see cref="IsLocalPlayer"/>).
///   3. Assign the prefab to the character's MissionDefinition.minigamePrefab.
/// The Host receives the result through MissionManager; networking and turns need no changes.
/// Other players see the same prefab with IsLocalPlayer = false (show a spectator view).
/// </summary>
public abstract class MissionMinigame : MonoBehaviour
{
    /// <summary>Raised once with the result (true = mission completed).</summary>
    public event Action<bool> Finished;

    public MissionDefinition Definition { get; private set; }
    public string PlayerName { get; private set; }
    public string CharacterName { get; private set; }
    public bool IsLocalPlayer { get; private set; }

    private bool finished;

    public void Begin(MissionDefinition definition, string playerName, string characterName, bool isLocalPlayer)
    {
        Definition = definition;
        PlayerName = playerName;
        CharacterName = characterName;
        IsLocalPlayer = isLocalPlayer;
        OnBegin();
    }

    /// <summary>Build / start the minigame here.</summary>
    protected abstract void OnBegin();

    /// <summary>Called on every instance when the Host has decided the result.</summary>
    public virtual void ShowResult(bool success)
    {
    }

    /// <summary>Report the result (ignored after the first call and on non-local instances).</summary>
    protected void Finish(bool success)
    {
        if (finished || !IsLocalPlayer)
            return;

        finished = true;
        Finished?.Invoke(success);
    }
}
