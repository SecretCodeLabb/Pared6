using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Persistent session state (lives on the NetworkSession prefab, DontDestroyOnLoad).
/// Host-authoritative player slots, character choice, ready flags and game start.
/// </summary>
public class LobbyManager : NetworkBehaviour
{
    public static LobbyManager Instance { get; private set; }

    public NetworkList<LobbySlot> Slots;

    public NetworkVariable<bool> GameInProgress = new NetworkVariable<bool>(false);

    /// <summary>Raised on every instance when the slot list changes.</summary>
    public event Action SlotsChanged;

    /// <summary>Raised on the local instance with a message for the player (e.g. rejected choice).</summary>
    public event Action<string> LocalMessage;

    private bool startRequested;

    private void Awake()
    {
        Slots = new NetworkList<LobbySlot>();
    }

    public override void OnNetworkSpawn()
    {
        Instance = this;
        DontDestroyOnLoad(gameObject);

        Slots.OnListChanged += HandleSlotsChanged;

        if (IsServer)
        {
            NetworkManager.OnClientConnectedCallback += ServerHandleClientConnected;
            NetworkManager.OnClientDisconnectCallback += ServerHandleClientDisconnected;

            foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
                ServerAddSlot(clientId);
        }

        SlotsChanged?.Invoke();
    }

    public override void OnNetworkDespawn()
    {
        Slots.OnListChanged -= HandleSlotsChanged;

        if (NetworkManager != null)
        {
            NetworkManager.OnClientConnectedCallback -= ServerHandleClientConnected;
            NetworkManager.OnClientDisconnectCallback -= ServerHandleClientDisconnected;
        }

        if (Instance == this)
            Instance = null;
    }

    private void HandleSlotsChanged(NetworkListEvent<LobbySlot> change)
    {
        SlotsChanged?.Invoke();
    }

    // =========================================================
    // QUERIES
    // =========================================================

    public List<LobbySlot> GetSlotsSorted()
    {
        List<LobbySlot> list = new List<LobbySlot>();

        foreach (LobbySlot slot in Slots)
            list.Add(slot);

        list.Sort((a, b) => a.PlayerId.CompareTo(b.PlayerId));
        return list;
    }

    public bool TryGetSlotByClient(ulong clientId, out LobbySlot slot)
    {
        foreach (LobbySlot s in Slots)
        {
            if (s.ClientId == clientId)
            {
                slot = s;
                return true;
            }
        }

        slot = default;
        return false;
    }

    public bool TryGetLocalSlot(out LobbySlot slot)
    {
        return TryGetSlotByClient(NetworkManager.Singleton.LocalClientId, out slot);
    }

    public int ConnectedCount
    {
        get
        {
            int count = 0;

            foreach (LobbySlot slot in Slots)
            {
                if (slot.Connected)
                    count++;
            }

            return count;
        }
    }

    public List<CharacterSelectionRules.SlotView> GetSlotViews()
    {
        List<CharacterSelectionRules.SlotView> views = new List<CharacterSelectionRules.SlotView>();

        foreach (LobbySlot slot in Slots)
            views.Add(slot.ToView());

        return views;
    }

    public bool CanStartGame()
    {
        GameRules rules = GameRules.Instance;
        return !GameInProgress.Value &&
               CharacterSelectionRules.CanStart(GetSlotViews(), rules.minPlayers, rules.maxPlayers);
    }

    // =========================================================
    // SERVER: CONNECTIONS
    // =========================================================

    private void ServerHandleClientConnected(ulong clientId)
    {
        ServerAddSlot(clientId);
    }

    private void ServerHandleClientDisconnected(ulong clientId)
    {
        int index = IndexOfClient(clientId);

        if (index < 0)
            return;

        Debug.Log($"{NetworkConnectionManager.GetInstanceTag()} Lobby: slot {Slots[index].PlayerId} liberado (cliente {clientId}).");
        Slots.RemoveAt(index);
    }

    private void ServerAddSlot(ulong clientId)
    {
        if (IndexOfClient(clientId) >= 0)
            return;

        int playerId = CharacterSelectionRules.GetLowestFreePlayerId(GetSlotViews(), GameRules.Instance.maxPlayers);

        if (playerId < 0)
        {
            Debug.LogWarning($"Lobby: no free slot for client {clientId}.");
            return;
        }

        Slots.Add(new LobbySlot
        {
            PlayerId = playerId,
            ClientId = clientId,
            Name = LobbySlot.DefaultName(playerId),
            Connected = true,
            Ready = false,
            CharacterId = CharacterSelectionRules.NoCharacter
        });

        Debug.Log($"{NetworkConnectionManager.GetInstanceTag()} Lobby: cliente {clientId} -> PlayerId {playerId}.");
    }

    private int IndexOfClient(ulong clientId)
    {
        for (int i = 0; i < Slots.Count; i++)
        {
            if (Slots[i].ClientId == clientId)
                return i;
        }

        return -1;
    }

    // =========================================================
    // CHARACTER SELECTION
    // =========================================================

    [Rpc(SendTo.Server)]
    public void RequestSelectCharacterRpc(int characterId, RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        int index = IndexOfClient(sender);

        if (GameInProgress.Value || index < 0)
        {
            Debug.LogWarning($"Lobby: select rejected for client {sender} (no slot or game in progress).");
            return;
        }

        LobbySlot slot = Slots[index];

        // Clicking your own character again releases it.
        if (slot.CharacterId == characterId && !slot.Ready)
        {
            slot.CharacterId = CharacterSelectionRules.NoCharacter;
            Slots[index] = slot;
            return;
        }

        CharacterSelectionRules.SelectResult result = CharacterSelectionRules.CanSelect(
            slot.PlayerId,
            characterId,
            CharacterCatalog.Instance.Count,
            GetSlotViews());

        if (result != CharacterSelectionRules.SelectResult.Ok)
        {
            Debug.LogWarning($"Lobby: select {characterId} rejected for PlayerId {slot.PlayerId}: {result}");
            SendMessageToClient(sender, RejectMessage(result));
            return;
        }

        slot.CharacterId = characterId;
        Slots[index] = slot;
    }

    [Rpc(SendTo.Server)]
    public void RequestReadyRpc(bool ready, RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        int index = IndexOfClient(sender);

        if (GameInProgress.Value || index < 0)
            return;

        LobbySlot slot = Slots[index];

        if (ready && slot.CharacterId == CharacterSelectionRules.NoCharacter)
        {
            SendMessageToClient(sender, "Elige un personaje antes de estar LISTO.");
            return;
        }

        slot.Ready = ready;
        Slots[index] = slot;
    }

    private static string RejectMessage(CharacterSelectionRules.SelectResult result)
    {
        switch (result)
        {
            case CharacterSelectionRules.SelectResult.AlreadyTaken:
                return "Ese personaje ya lo eligió otro jugador.";
            case CharacterSelectionRules.SelectResult.PlayerIsReady:
                return "Quita LISTO para cambiar de personaje.";
            default:
                return "Selección no válida.";
        }
    }

    private void SendMessageToClient(ulong clientId, string message)
    {
        ShowMessageRpc(message, RpcTarget.Single(clientId, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void ShowMessageRpc(string message, RpcParams rpcParams = default)
    {
        LocalMessage?.Invoke(message);
    }

    // =========================================================
    // START / RESTART (HOST)
    // =========================================================

    /// <summary>Host only. Loads the game scene for everyone if all start conditions hold.</summary>
    public bool ServerStartGame()
    {
        if (!IsServer)
            return false;

        if (startRequested || !CanStartGame())
        {
            Debug.LogWarning("Lobby: START ignored (conditions not met or already starting).");
            return false;
        }

        startRequested = true;
        GameInProgress.Value = true;

        if (NetworkConnectionManager.Instance != null)
            NetworkConnectionManager.Instance.AcceptingConnections = false;

        Debug.Log($"{NetworkConnectionManager.GetInstanceTag()} Lobby: START con {ConnectedCount} jugadores.");
        NetworkManager.SceneManager.LoadScene(SceneNames.Game, LoadSceneMode.Single);
        return true;
    }

    /// <summary>Host only. Back to character selection keeping characters, clearing ready flags.</summary>
    public void ServerReturnToLobby()
    {
        if (!IsServer)
            return;

        for (int i = 0; i < Slots.Count; i++)
        {
            LobbySlot slot = Slots[i];
            slot.Ready = false;
            Slots[i] = slot;
        }

        startRequested = false;
        GameInProgress.Value = false;

        if (NetworkConnectionManager.Instance != null)
            NetworkConnectionManager.Instance.AcceptingConnections = true;

        NetworkManager.SceneManager.LoadScene(SceneNames.CharacterSelection, LoadSceneMode.Single);
    }
}
