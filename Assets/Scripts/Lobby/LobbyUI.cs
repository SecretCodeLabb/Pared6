using System.Collections.Generic;
using System.Text;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Network layer for the existing character selection scene: slot list, LISTO, START, SALIR.
/// Added at runtime by CharacterSelectionManager only when the network is running.
/// </summary>
public class LobbyUI : MonoBehaviour
{
    private const float MessageSeconds = 3f;

    private Button readyButton;
    private TextMeshProUGUI countText;
    private List<CharacterCardUI> cards;

    private readonly List<TextMeshProUGUI> slotRows = new List<TextMeshProUGUI>();
    private Button startButton;
    private TextMeshProUGUI statusText;
    private TextMeshProUGUI messageText;
    private float messageUntil;

    private LobbyManager subscribedLobby;

    public void Initialize(Button continueButton, TextMeshProUGUI selectedText, Transform characterGrid, List<CharacterCardUI> characterCards)
    {
        readyButton = continueButton;
        countText = selectedText;
        cards = characterCards;

        Canvas canvas = characterGrid.GetComponentInParent<Canvas>();
        BuildPanels(canvas.transform);

        if (readyButton != null)
        {
            readyButton.onClick.RemoveAllListeners();
            readyButton.onClick.AddListener(OnReadyClicked);
        }

        Refresh();
    }

    private void BuildPanels(Transform canvas)
    {
        // Left: player slots.
        Image slotsPanel = UIFactory.CreatePanel(canvas, "LobbySlotsPanel", UIFactory.PanelColor);
        UIFactory.Place(slotsPanel.rectTransform, new Vector2(0f, 0.5f), new Vector2(20f, 60f), new Vector2(330f, 420f));
        UIFactory.AddVerticalLayout(slotsPanel.gameObject, 10f, 14);

        TextMeshProUGUI title = UIFactory.CreateText(slotsPanel.transform, "Title", "JUGADORES", 30f);
        title.color = UIFactory.AccentColor;
        UIFactory.SetPreferredHeight(title, 40f);

        int maxPlayers = GameRules.Instance.maxPlayers;

        for (int i = 0; i < maxPlayers; i++)
        {
            Image row = UIFactory.CreatePanel(slotsPanel.transform, "Slot" + i, new Color(1f, 1f, 1f, 0.08f));
            UIFactory.SetPreferredHeight(row, 78f);
            TextMeshProUGUI text = UIFactory.CreateText(row.transform, "Text", "", 21f, TextAlignmentOptions.Left);
            UIFactory.Stretch(text.rectTransform, 8f);
            slotRows.Add(text);
        }

        // Right: status, START (host only) and leave.
        Image sidePanel = UIFactory.CreatePanel(canvas, "LobbyActionsPanel", UIFactory.PanelColor);
        UIFactory.Place(sidePanel.rectTransform, new Vector2(1f, 0.5f), new Vector2(-20f, 60f), new Vector2(330f, 420f));
        UIFactory.AddVerticalLayout(sidePanel.gameObject, 12f, 14);

        statusText = UIFactory.CreateText(sidePanel.transform, "Status", "", 22f);
        UIFactory.SetPreferredHeight(statusText, 120f);

        startButton = UIFactory.CreateButton(sidePanel.transform, "StartButton", "START", OnStartClicked, UIFactory.OkColor, 34f);
        UIFactory.SetPreferredHeight(startButton, 80f);

        Button leaveButton = UIFactory.CreateButton(sidePanel.transform, "LeaveButton", "SALIR", OnLeaveClicked, UIFactory.DangerColor, 26f);
        UIFactory.SetPreferredHeight(leaveButton, 56f);

        messageText = UIFactory.CreateText(sidePanel.transform, "Message", "", 20f);
        messageText.color = new Color(1f, 0.6f, 0.4f);
        UIFactory.SetPreferredHeight(messageText, 90f);
    }

    private void Update()
    {
        LobbyManager lobby = LobbyManager.Instance;

        // The session object may spawn after this scene on clients.
        if (lobby != subscribedLobby)
        {
            Unsubscribe();

            if (lobby != null)
            {
                lobby.SlotsChanged += Refresh;
                lobby.LocalMessage += ShowMessage;
                subscribedLobby = lobby;
            }

            Refresh();
        }

        if (messageText != null && messageText.text.Length > 0 && Time.time > messageUntil)
            messageText.text = "";
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    private void Unsubscribe()
    {
        if (subscribedLobby != null)
        {
            subscribedLobby.SlotsChanged -= Refresh;
            subscribedLobby.LocalMessage -= ShowMessage;
            subscribedLobby = null;
        }
    }

    private void ShowMessage(string message)
    {
        messageText.text = message;
        messageUntil = Time.time + MessageSeconds;
    }

    // =========================================================
    // ACTIONS
    // =========================================================

    public void RequestCharacter(CharacterData data)
    {
        LobbyManager lobby = LobbyManager.Instance;

        if (lobby == null)
            return;

        int characterId = CharacterCatalog.Instance.IndexOf(data);

        if (characterId < 0)
        {
            Debug.LogError("LobbyUI: character '" + data.name + "' is missing from Resources/CharacterCatalog.");
            return;
        }

        if (lobby.TryGetLocalSlot(out LobbySlot local) && local.Ready)
        {
            ShowMessage("Quita LISTO para cambiar de personaje.");
            return;
        }

        lobby.RequestSelectCharacterRpc(characterId);
    }

    private void OnReadyClicked()
    {
        LobbyManager lobby = LobbyManager.Instance;

        if (lobby == null || !lobby.TryGetLocalSlot(out LobbySlot local))
            return;

        lobby.RequestReadyRpc(!local.Ready);
    }

    private void OnStartClicked()
    {
        LobbyManager lobby = LobbyManager.Instance;

        if (lobby == null || !NetworkManager.Singleton.IsHost)
            return;

        if (lobby.ServerStartGame())
            startButton.interactable = false;
    }

    private void OnLeaveClicked()
    {
        if (NetworkConnectionManager.Instance != null)
            NetworkConnectionManager.Instance.ReturnToMenu("");
    }

    // =========================================================
    // VIEW
    // =========================================================

    private void Refresh()
    {
        LobbyManager lobby = LobbyManager.Instance;
        bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
        int maxPlayers = GameRules.Instance.maxPlayers;

        if (startButton != null)
            startButton.gameObject.SetActive(isHost);

        if (lobby == null)
        {
            if (statusText != null)
                statusText.text = "Conectando a la sala...";

            if (readyButton != null)
                readyButton.interactable = false;

            return;
        }

        List<LobbySlot> slots = lobby.GetSlotsSorted();
        bool hasLocal = lobby.TryGetLocalSlot(out LobbySlot local);
        CharacterCatalog catalog = CharacterCatalog.Instance;

        // Slot rows.
        for (int i = 0; i < slotRows.Count; i++)
        {
            LobbySlot? slot = null;

            foreach (LobbySlot s in slots)
            {
                if (s.PlayerId == i)
                    slot = s;
            }

            slotRows[i].text = slot.HasValue ? DescribeSlot(slot.Value, hasLocal && slot.Value.PlayerId == local.PlayerId) : "<color=#888>— libre —</color>";
        }

        // Cards: mine / taken / free.
        foreach (CharacterCardUI card in cards)
        {
            int id = catalog.IndexOf(card.Data);
            string owner = "";
            bool mine = false;
            bool takenByOther = false;

            foreach (LobbySlot s in slots)
            {
                if (s.CharacterId != id)
                    continue;

                mine = hasLocal && s.PlayerId == local.PlayerId;
                takenByOther = !mine;
                owner = mine ? s.Name + " (tú)" : s.Name.ToString();
            }

            card.SetSelected(mine);
            card.SetOwner(owner, takenByOther || (hasLocal && local.Ready && !mine));
        }

        // Ready button.
        if (readyButton != null)
        {
            bool hasCharacter = hasLocal && local.CharacterId != CharacterSelectionRules.NoCharacter;
            readyButton.interactable = hasCharacter;
            UIFactory.SetLabel(readyButton, hasLocal && local.Ready ? "NO LISTO" : "LISTO");
        }

        if (countText != null)
            countText.text = $"Conectados: {lobby.ConnectedCount} / {maxPlayers}";

        // Status + START.
        bool canStart = lobby.CanStartGame();

        if (startButton != null)
            startButton.interactable = isHost && canStart;

        if (statusText != null)
        {
            string role = isHost ? "Eres el HOST" : "Conectado como CLIENTE";
            string hint = canStart
                ? (isHost ? "Todos listos. ¡Pulsa START!" : "Esperando a que el Host pulse START...")
                : $"Mínimo {GameRules.Instance.minPlayers} jugadores, todos LISTOS y con personajes distintos.";
            statusText.text = role + "\n" + hint;
        }
    }

    private static string DescribeSlot(LobbySlot slot, bool isLocal)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("<b>").Append(slot.Name).Append("</b>");

        if (slot.ClientId == NetworkManager.ServerClientId)
            sb.Append(" (Host)");

        if (isLocal)
            sb.Append(" <color=#FFD040>(tú)</color>");

        sb.Append('\n');

        if (slot.CharacterId == CharacterSelectionRules.NoCharacter)
            sb.Append("<color=#AAA>eligiendo...</color>");
        else
            sb.Append(CharacterCatalog.Instance.GetName(slot.CharacterId));

        sb.Append(slot.Ready ? "  <color=#5F5>LISTO</color>" : "");
        return sb.ToString();
    }
}
