using System.Collections.Generic;

/// <summary>
/// Pure victory logic. A single winner: the first player who completes their character's
/// mission, or the last player standing. Tie rule (cannot normally happen, only the active
/// player plays a mission): the active player wins, otherwise the lowest PlayerId.
/// If nobody is alive the game ends without a winner (handled by the caller).
/// </summary>
public static class VictoryRules
{
    /// <returns>Winner PlayerId or TurnRotation.NoPlayer if the game continues.</returns>
    public static int ResolveWinner(
        int activePlayerId,
        IReadOnlyList<bool> missionCompletedById,
        IReadOnlyList<PlayerLifeState> statesById,
        int startingPlayerCount)
    {
        if (IsEligible(activePlayerId, missionCompletedById, statesById))
            return activePlayerId;

        for (int i = 0; i < missionCompletedById.Count; i++)
        {
            if (IsEligible(i, missionCompletedById, statesById))
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
        IReadOnlyList<bool> missionCompletedById,
        IReadOnlyList<PlayerLifeState> statesById)
    {
        if (playerId < 0 || playerId >= missionCompletedById.Count || playerId >= statesById.Count)
            return false;

        return statesById[playerId] == PlayerLifeState.Alive && missionCompletedById[playerId];
    }
}
