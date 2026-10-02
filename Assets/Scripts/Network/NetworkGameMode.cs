using Unity.Netcode;

/// <summary>True when a LAN session is running; false keeps the legacy offline single-instance mode.</summary>
public static class NetworkGameMode
{
    public static bool IsActive =>
        NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
}
