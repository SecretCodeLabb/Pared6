using System.Collections.Generic;

/// <summary>
/// Pure victory logic. Tie rule: if several players reach the goal at once,
/// the active player wins; otherwise the lowest PlayerId wins.
/// </summary>
public static class VictoryRules
{
    /// <returns>Winner PlayerId or TurnRotation.NoPlayer if the game continues.</returns>
    public static int ResolveWinner(
        int activePlayerId,
        IReadOnlyList<int> progressById,
        IReadOnlyList<PlayerLifeState> statesById,
        int requiredEventsToWin,
        int startingPlayerCount)
    {
        if (IsEligible(activePlayerId, progressById, statesById, requiredEventsToWin))
            return activePlayerId;

        for (int i = 0; i < progressById.Count; i++)
        {
            if (IsEligible(i, progressById, statesById, requiredEventsToWin))
                return i;
        }

        int aliveCount = TurnRotation.CountAlive(statesById);

        // Last player standing wins only if the game actually had opponents.
        if (aliveCount == 1 && startingPlayerCount >= 2)
            return TurnRotation.GetLastStanding(statesById);

        return TurnRotation.NoPlayer;
    }

    private static bool IsEligible(
        int playerId,
        IReadOnlyList<int> progressById,
        IReadOnlyList<PlayerLifeState> statesById,
        int requiredEventsToWin)
    {
        if (playerId < 0 || playerId >= progressById.Count || playerId >= statesById.Count)
            return false;

        return statesById[playerId] == PlayerLifeState.Alive &&
               progressById[playerId] >= requiredEventsToWin;
    }
}
