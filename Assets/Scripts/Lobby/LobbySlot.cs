using System;
using Unity.Collections;
using Unity.Netcode;

/// <summary>One player in the waiting room. PlayerId is the stable slot (0-3); ClientId is the network id.</summary>
public struct LobbySlot : INetworkSerializable, IEquatable<LobbySlot>
{
    public int PlayerId;
    public ulong ClientId;
    public FixedString32Bytes Name;
    public bool Connected;
    public bool Ready;
    public int CharacterId;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref PlayerId);
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref Name);
        serializer.SerializeValue(ref Connected);
        serializer.SerializeValue(ref Ready);
        serializer.SerializeValue(ref CharacterId);
    }

    public bool Equals(LobbySlot other)
    {
        return PlayerId == other.PlayerId &&
               ClientId == other.ClientId &&
               Name.Equals(other.Name) &&
               Connected == other.Connected &&
               Ready == other.Ready &&
               CharacterId == other.CharacterId;
    }

    public CharacterSelectionRules.SlotView ToView()
    {
        return new CharacterSelectionRules.SlotView
        {
            PlayerId = PlayerId,
            CharacterId = CharacterId,
            Ready = Ready,
            Connected = Connected
        };
    }

    public static string DefaultName(int playerId)
    {
        return "Jugador " + (playerId + 1);
    }
}
