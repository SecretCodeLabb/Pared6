using Unity.Netcode;
using System;

public struct PlayerData : INetworkSerializable, IEquatable<PlayerData>
{
    public ulong ClientId;
    public int CharacterId; // 0 al 5 para tus 6 personajes. -1 si no ha elegido.
    public bool IsLockedIn;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref CharacterId);
        serializer.SerializeValue(ref IsLockedIn);
    }

    public bool Equals(PlayerData other)
    {
        return ClientId == other.ClientId &&
               CharacterId == other.CharacterId &&
               IsLockedIn == other.IsLockedIn;
    }
}