using System.Collections.Generic;

/// <summary>
/// Pure turn-order logic (no networking). States are indexed by PlayerId;
/// unused slots must be marked Disconnected.
/// </summary>
public static class TurnRotation
{
    public const int NoPlayer = -1;

    /// <summary>Next Alive player after <paramref name="currentPlayerId"/>, wrapping around. NoPlayer if none.</summary>
    public static int GetNextPlayer(int currentPlayerId, IReadOnlyList<PlayerLifeState> statesById)
    {
        int count = statesById.Count;

        if (count == 0)
            return NoPlayer;

        int start = currentPlayerId < 0 ? count - 1 : currentPlayerId;

        for (int offset = 1; offset <= count; offset++)
        {
            int candidate = (start + offset) % count;

            if (statesById[candidate] == PlayerLifeState.Alive)
                return candidate;
        }

        return NoPlayer;
    }

    /// <summary>The preferred player if Alive, otherwise the next Alive one.</summary>
    public static int GetFirstPlayer(IReadOnlyList<PlayerLifeState> statesById, int preferredPlayerId)
    {
        if (preferredPlayerId >= 0 &&
            preferredPlayerId < statesById.Count &&
            statesById[preferredPlayerId] == PlayerLifeState.Alive)
        {
            return preferredPlayerId;
        }

        return GetNextPlayer(preferredPlayerId, statesById);
    }

    public static int CountAlive(IReadOnlyList<PlayerLifeState> statesById)
    {
        int alive = 0;

        foreach (PlayerLifeState state in statesById)
        {
            if (state == PlayerLifeState.Alive)
                alive++;
        }

        return alive;
    }

    /// <summary>The only Alive player, or NoPlayer if zero or several remain.</summary>
    public static int GetLastStanding(IReadOnlyList<PlayerLifeState> statesById)
    {
        int found = NoPlayer;

        for (int i = 0; i < statesById.Count; i++)
        {
            if (statesById[i] != PlayerLifeState.Alive)
                continue;

            if (found != NoPlayer)
                return NoPlayer;

            found = i;
        }

        return found;
    }
}
