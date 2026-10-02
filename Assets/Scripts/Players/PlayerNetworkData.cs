using System;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Single source of truth for one player (AGENT_CONTEXT §5). Written only by the Host.
/// Lives on the Player prefab next to the existing PlayerController (visuals/animation).
/// </summary>
public class PlayerNetworkData : NetworkBehaviour
{
    private static readonly List<PlayerNetworkData> all = new List<PlayerNetworkData>();

    public static IReadOnlyList<PlayerNetworkData> All => all;

    /// <summary>Raised on every instance when any player's synced data changes (for UI).</summary>
    public static event Action AnyChanged;

    public NetworkVariable<int> PlayerId = new NetworkVariable<int>(-1);
    public NetworkVariable<int> CharacterId = new NetworkVariable<int>(-1);
    public NetworkVariable<FixedString32Bytes> DisplayName = new NetworkVariable<FixedString32Bytes>();
    public NetworkVariable<int> Lives = new NetworkVariable<int>(0);
    public NetworkVariable<int> Progress = new NetworkVariable<int>(0);
    public NetworkVariable<Vector2Int> GridPosition = new NetworkVariable<Vector2Int>();
    public NetworkVariable<PlayerLifeState> State = new NetworkVariable<PlayerLifeState>(PlayerLifeState.Alive);
    public NetworkVariable<int> HandCount = new NetworkVariable<int>(0);

    /// <summary>Card ids. Only the owner (and the Host) receive it; others see HandCount.</summary>
    public NetworkList<int> Hand;

    private PlayerController controller;
    private TextMeshPro nameLabel;
    private Renderer[] renderers;

    public ulong OwnerClient => OwnerClientId;
    public bool IsAlive => State.Value == PlayerLifeState.Alive;
    public string Name => DisplayName.Value.ToString();
    public CharacterData Character => CharacterCatalog.Instance.Get(CharacterId.Value);

    private void Awake()
    {
        Hand = new NetworkList<int>(
            null,
            NetworkVariableReadPermission.Owner,
            NetworkVariableWritePermission.Server);

        controller = GetComponent<PlayerController>();
        renderers = GetComponentsInChildren<Renderer>();
    }

    // =========================================================
    // LOOKUPS
    // =========================================================

    public static PlayerNetworkData GetByPlayerId(int playerId)
    {
        foreach (PlayerNetworkData p in all)
        {
            if (p.PlayerId.Value == playerId)
                return p;
        }

        return null;
    }

    public static PlayerNetworkData GetByClientId(ulong clientId)
    {
        foreach (PlayerNetworkData p in all)
        {
            if (p.OwnerClientId == clientId)
                return p;
        }

        return null;
    }

    public static PlayerNetworkData Local
    {
        get
        {
            foreach (PlayerNetworkData p in all)
            {
                if (p.IsOwner)
                    return p;
            }

            return null;
        }
    }

    /// <summary>States indexed by PlayerId (size maxPlayers); empty slots are Disconnected.</summary>
    public static List<PlayerLifeState> GetStatesById(int maxPlayers)
    {
        List<PlayerLifeState> states = new List<PlayerLifeState>();

        for (int i = 0; i < maxPlayers; i++)
        {
            PlayerNetworkData p = GetByPlayerId(i);
            states.Add(p != null ? p.State.Value : PlayerLifeState.Disconnected);
        }

        return states;
    }

    public static List<int> GetProgressById(int maxPlayers)
    {
        List<int> progress = new List<int>();

        for (int i = 0; i < maxPlayers; i++)
        {
            PlayerNetworkData p = GetByPlayerId(i);
            progress.Add(p != null ? p.Progress.Value : 0);
        }

        return progress;
    }

    // =========================================================
    // LIFECYCLE
    // =========================================================

    public override void OnNetworkSpawn()
    {
        // Host: initial values are written here so they are part of the spawn sync.
        if (IsServer && pendingInit.HasValue)
        {
            ApplyInit(pendingInit.Value);
            pendingInit = null;
        }

        all.Add(this);

        PlayerId.OnValueChanged += OnIntChanged;
        CharacterId.OnValueChanged += OnCharacterChanged;
        Lives.OnValueChanged += OnIntChanged;
        Progress.OnValueChanged += OnIntChanged;
        HandCount.OnValueChanged += OnIntChanged;
        State.OnValueChanged += OnStateChanged;
        GridPosition.OnValueChanged += OnGridPositionChanged;
        Hand.OnListChanged += OnHandChanged;

        name = "Player_" + PlayerId.Value + "_" + Name;

        PlaceOnBoard();
        ApplyAppearance();
        ApplyStateVisual();

        Debug.Log($"{NetworkConnectionManager.GetInstanceTag()} Jugador spawn: {Name} (PlayerId {PlayerId.Value}, owner {OwnerClientId}, local {IsOwner}) en {GridPosition.Value}");
        AnyChanged?.Invoke();
    }

    public override void OnNetworkDespawn()
    {
        all.Remove(this);

        PlayerId.OnValueChanged -= OnIntChanged;
        CharacterId.OnValueChanged -= OnCharacterChanged;
        Lives.OnValueChanged -= OnIntChanged;
        Progress.OnValueChanged -= OnIntChanged;
        HandCount.OnValueChanged -= OnIntChanged;
        State.OnValueChanged -= OnStateChanged;
        GridPosition.OnValueChanged -= OnGridPositionChanged;
        Hand.OnListChanged -= OnHandChanged;

        AnyChanged?.Invoke();
    }

    // =========================================================
    // SERVER SETUP
    // =========================================================

    private struct InitData
    {
        public LobbySlot Slot;
        public Vector2Int StartCell;
        public int StartingLives;
    }

    private InitData? pendingInit;

    /// <summary>Host only, before Spawn: initial values from the lobby slot (applied in OnNetworkSpawn).</summary>
    public void ServerInitialize(LobbySlot slot, Vector2Int startCell, int startingLives)
    {
        pendingInit = new InitData { Slot = slot, StartCell = startCell, StartingLives = startingLives };
    }

    private void ApplyInit(InitData init)
    {
        PlayerId.Value = init.Slot.PlayerId;
        CharacterId.Value = init.Slot.CharacterId;
        DisplayName.Value = init.Slot.Name;
        Lives.Value = init.StartingLives;
        Progress.Value = 0;
        GridPosition.Value = init.StartCell;
        State.Value = PlayerLifeState.Alive;
        HandCount.Value = 0;
    }

    public void ServerSetHand(List<int> cardIds)
    {
        Hand.Clear();

        foreach (int id in cardIds)
            Hand.Add(id);

        HandCount.Value = Hand.Count;
    }

    /// <summary>Host only. Removes the card at index; returns its id or -1.</summary>
    public int ServerRemoveCardAt(int handIndex)
    {
        if (handIndex < 0 || handIndex >= Hand.Count)
            return -1;

        int cardId = Hand[handIndex];
        Hand.RemoveAt(handIndex);
        HandCount.Value = Hand.Count;
        return cardId;
    }

    // =========================================================
    // VISUALS (local, from synced data)
    // =========================================================

    private Vector3 VisualOffset
    {
        get
        {
            // Small per-player offset so pawns sharing a tile stay visible.
            float d = 0.45f;
            switch (PlayerId.Value)
            {
                case 0: return new Vector3(-d, 0f, d);
                case 1: return new Vector3(d, 0f, d);
                case 2: return new Vector3(-d, 0f, -d);
                case 3: return new Vector3(d, 0f, -d);
                default: return Vector3.zero;
            }
        }
    }

    private void PlaceOnBoard()
    {
        BoardManager board = FindFirstObjectByType<BoardManager>();

        if (board == null || controller == null)
        {
            Debug.LogError("PlayerNetworkData: BoardManager or PlayerController missing.");
            return;
        }

        Tile tile = board.GetTile(GridPosition.Value);

        if (tile == null)
            Debug.LogError($"PlayerNetworkData: start cell {GridPosition.Value} not found on this board.");

        controller.InitializeNetwork(tile, board, this, VisualOffset);
    }

    private void ApplyAppearance()
    {
        CharacterData data = Character;
        Color color = data != null && data.boardColor.a > 0f ? data.boardColor : Color.white;

        foreach (Renderer r in renderers)
        {
            // Instance material so each pawn keeps its own tint.
            Material material = r.material;

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);

            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
        }

        if (nameLabel == null)
        {
            GameObject labelObject = new GameObject("NameLabel");
            labelObject.transform.SetParent(transform, false);
            labelObject.transform.localPosition = new Vector3(0f, 2.6f, 0f);
            nameLabel = labelObject.AddComponent<TextMeshPro>();
            nameLabel.alignment = TextAlignmentOptions.Center;
            nameLabel.fontSize = 6f;
            nameLabel.textWrappingMode = TextWrappingModes.NoWrap;
            nameLabel.outlineWidth = 0.25f;
            nameLabel.outlineColor = Color.black;
        }

        string characterName = data != null ? data.characterName : "?";
        nameLabel.text = (IsOwner ? "<color=#FFD040>" : "") + Name + (IsOwner ? " (tú)</color>" : "") + "\n<size=70%>" + characterName;
        nameLabel.color = Color.white;
    }

    private void LateUpdate()
    {
        // Billboard the label towards the camera.
        if (nameLabel != null && Camera.main != null)
            nameLabel.transform.rotation = Camera.main.transform.rotation;
    }

    private void ApplyStateVisual()
    {
        bool visible = State.Value != PlayerLifeState.Disconnected;
        bool dimmed = State.Value == PlayerLifeState.Eliminated;

        foreach (Renderer r in renderers)
        {
            r.enabled = visible;

            Material material = r.material;
            string property = material.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";

            if (!material.HasProperty(property))
                continue;

            Color c = material.GetColor(property);
            c = dimmed ? Color.Lerp(c, Color.black, 0.7f) : c;
            material.SetColor(property, c);
        }

        if (nameLabel != null)
        {
            nameLabel.gameObject.SetActive(visible);

            if (dimmed)
                nameLabel.text = Name + "\n<color=#FF5050>ELIMINADO</color>";
        }
    }

    // =========================================================
    // CHANGE CALLBACKS
    // =========================================================

    private void OnIntChanged(int previous, int current)
    {
        AnyChanged?.Invoke();
    }

    private void OnCharacterChanged(int previous, int current)
    {
        ApplyAppearance();
        AnyChanged?.Invoke();
    }

    private void OnStateChanged(PlayerLifeState previous, PlayerLifeState current)
    {
        ApplyStateVisual();
        AnyChanged?.Invoke();
    }

    private void OnGridPositionChanged(Vector2Int previous, Vector2Int current)
    {
        if (controller == null)
            return;

        BoardManager board = FindFirstObjectByType<BoardManager>();
        Tile tile = board != null ? board.GetTile(current) : null;

        if (tile != null)
            controller.AnimateToTile(tile);
    }

    private void OnHandChanged(NetworkListEvent<int> change)
    {
        AnyChanged?.Invoke();
    }
}
