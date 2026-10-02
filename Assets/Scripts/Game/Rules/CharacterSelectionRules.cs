using System.Collections.Generic;

/// <summary>Pure lobby validation (character uniqueness and start conditions).</summary>
public static class CharacterSelectionRules
{
    public const int NoCharacter = -1;

    public struct SlotView
    {
        public int PlayerId;
        public int CharacterId;
        public bool Ready;
        public bool Connected;
    }

    public enum SelectResult
    {
        Ok,
        InvalidCharacter,
        AlreadyTaken,
        PlayerIsReady,
        UnknownPlayer
    }

    public static SelectResult CanSelect(
        int requesterPlayerId,
        int characterId,
        int catalogCount,
        IReadOnlyList<SlotView> slots)
    {
        if (characterId < 0 || characterId >= catalogCount)
            return SelectResult.InvalidCharacter;

        bool requesterFound = false;

        foreach (SlotView slot in slots)
        {
            if (slot.PlayerId == requesterPlayerId)
            {
                requesterFound = true;

                if (slot.Ready)
                    return SelectResult.PlayerIsReady;

                continue;
            }

            if (slot.Connected && slot.CharacterId == characterId)
                return SelectResult.AlreadyTaken;
        }

        return requesterFound ? SelectResult.Ok : SelectResult.UnknownPlayer;
    }

    public static bool CanStart(IReadOnlyList<SlotView> slots, int minPlayers, int maxPlayers)
    {
        int connected = 0;
        HashSet<int> used = new HashSet<int>();

        foreach (SlotView slot in slots)
        {
            if (!slot.Connected)
                continue;

            connected++;

            if (!slot.Ready || slot.CharacterId == NoCharacter)
                return false;

            if (!used.Add(slot.CharacterId))
                return false;
        }

        return connected >= minPlayers && connected <= maxPlayers;
    }

    /// <summary>Lowest PlayerId not used by any slot, or -1 if full.</summary>
    public static int GetLowestFreePlayerId(IReadOnlyList<SlotView> slots, int maxPlayers)
    {
        for (int id = 0; id < maxPlayers; id++)
        {
            bool used = false;

            foreach (SlotView slot in slots)
            {
                if (slot.PlayerId == id)
                {
                    used = true;
                    break;
                }
            }

            if (!used)
                return id;
        }

        return -1;
    }
}
