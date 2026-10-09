using System.Collections.Generic;
using System.Text;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Network game HUD (T07-T15): turn banner, players list (lives / progress / cards),
/// dice + movement, local hand, combat panel, messages and end screen.
/// Builds its own overlay canvas at runtime; reuses the scene's roll button and info text.
/// Disabled in offline mode.
/// </summary>
public class GameHUD : MonoBehaviour
{
    private const float RefreshInterval = 0.1f;
    private const float ToastSeconds = 4f;
    private const string Heart = "♥";

    // Tablets / monitors use DesignResolution; phones scale the UI up to PhoneHeight
    // (the layout below is built to fit 1280x720).
    private static readonly Vector2 DesignResolution = new Vector2(1600f, 900f);
    private const float PhoneHeight = 720f;

    [Header("Existing scene UI (reused)")]
    [SerializeField] private BoardManager boardManager;
    [SerializeField] private Button rollButton;
    [SerializeField] private TextMeshProUGUI gameInfoText;

    [Header("Roulette (Prefabs/UI/RouletteWheel)")]
    [SerializeField] private RouletteWheelView roulettePrefab;

    private RouletteWheelView roulette;
    private int shownSpinId;

    // Built at runtime
    private TextMeshProUGUI turnText;
    private TextMeshProUGUI localTurnText;
    private TextMeshProUGUI hintText;
    private TextMeshProUGUI livesText;
    private TextMeshProUGUI playersText;
    private TextMeshProUGUI objectiveText;

    private MissionMinigame missionMinigame;
    private int missionShownFor = -1;
    private MissionPhase missionResultShown = MissionPhase.None;
    private TextMeshProUGUI toastText;
    private TextMeshProUGUI networkText;
    private Button rollTextButton;
    private Button turnCardButton;
    private Button viewButton;
    private BoardCameraController boardCamera;
    private readonly List<Button> moveButtons = new List<Button>();

    private GameObject handPanel;
    private Transform handContainer;
    private TextMeshProUGUI handTitle;
    private readonly List<int> shownHand = new List<int>();
    private readonly List<GameObject> handCardGlows = new List<GameObject>();
    private int selectedHandIndex = -1;

    private GameObject combatPanel;
    private TextMeshProUGUI combatTitle;
    private TextMeshProUGUI combatBody;
    private TextMeshProUGUI combatResult;
    private Button useCardButton;
    private Button skipCardButton;
    private Button combatRollButton;

    private GameObject endPanel;
    private TextMeshProUGUI endTitle;
    private TextMeshProUGUI endBody;
    private Button restartButton;

    private readonly List<(string text, float until)> toasts = new List<(string, float)>();
    private float nextRefresh;

    private static GameRules Rules => GameRules.Instance;

    // =========================================================
    // LIFECYCLE
    // =========================================================

    private void Start()
    {
        if (!NetworkGameMode.IsActive)
        {
            // Offline fallback keeps the original single-player UI only.
            gameObject.SetActive(false);
            return;
        }

        // The scene's dice button keeps its original listener (BoardManager.RollDice),
        // which forwards to the Host in network mode; the HUD only toggles it.
        Build();

        GameStateManager.MessageReceived += ShowToast;
        Refresh();
    }

    private void OnDestroy()
    {
        GameStateManager.MessageReceived -= ShowToast;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefresh)
            return;

        nextRefresh = Time.unscaledTime + RefreshInterval;
        Refresh();
    }

    // =========================================================
    // BUILD
    // =========================================================

    private void Build()
    {
        GameObject canvasObject = new GameObject("GameHUDCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        // Layout fits a 1280x720 logical canvas (phones); bigger screens get more room around it.
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = DesignResolution;
        ResponsiveCanvas responsive = ResponsiveCanvas.Ensure(canvas, DesignResolution.y / PhoneHeight);

        // HUD widgets live inside the safe area; full-screen overlays use the whole canvas.
        Transform root = responsive.Content;
        Transform overlays = canvasObject.transform;

        BuildTurnBanner(root);
        BuildPlayersPanel(root);
        BuildActionsPanel(root);
        BuildHandPanel(root);
        BuildCombatPanel(root);
        BuildTopButtons(root);
        AdoptSceneDice(root);

        toastText = UIFactory.CreateText(root, "Toasts", "", 22f);
        UIFactory.Place(toastText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -126f), new Vector2(600f, 110f));
        toastText.color = UIFactory.AccentColor;
        toastText.outlineWidth = 0.2f;
        toastText.outlineColor = Color.black;

        networkText = UIFactory.CreateText(root, "NetworkInfo", "", 13f, TextAlignmentOptions.BottomRight);
        UIFactory.Place(networkText.rectTransform, new Vector2(1f, 0f), new Vector2(-10f, 4f), new Vector2(420f, 22f));
        networkText.color = new Color(1f, 1f, 1f, 0.55f);

        BuildRoulette(overlays);
        AdoptEventAnnouncement(overlays);
        BuildEndPanel(overlays);
    }

    /// <summary>
    /// The scene's short announcement panel (EventUI: "¡PELEA OBLIGATORIA!", "EVENTO ACTIVADO")
    /// moves above the HUD, centred and readable on any screen.
    /// </summary>
    private void AdoptEventAnnouncement(Transform overlays)
    {
        EventUI eventUI = EventUI.Instance != null ? EventUI.Instance : FindFirstObjectByType<EventUI>();

        if (eventUI == null || eventUI.eventPanel == null)
            return;

        RectTransform panel = (RectTransform)eventUI.eventPanel.transform;
        panel.SetParent(overlays, false);
        panel.localScale = Vector3.one;
        panel.localRotation = Quaternion.identity;
        UIFactory.Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0f, 80f), new Vector2(720f, 150f));
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.gameObject.AddComponent<ScaleToFit>();

        // Opaque so it does not mix with the combat panel behind it.
        Image background = panel.GetComponent<Image>();

        if (background != null)
            background.color = new Color(0.10f, 0.06f, 0.08f, 0.96f);

        if (eventUI.eventText is TextMeshProUGUI text)
        {
            UIFactory.Stretch(text.rectTransform, 12f);
            text.rectTransform.localScale = Vector3.one;
            text.alignment = TextAlignmentOptions.Center;
            text.enableAutoSizing = true;
            text.fontSizeMin = 18f;
            text.fontSizeMax = 52f;
        }
    }

    private void BuildTopButtons(Transform root)
    {
        Button menuButton = UIFactory.CreateButton(root, "MenuButton", "SALIR", OnMenuClicked, UIFactory.DangerColor, 18f);
        UIFactory.Place((RectTransform)menuButton.transform, new Vector2(0f, 1f), new Vector2(12f, -12f), new Vector2(120f, 56f));

        // Overview / follow toggle (same as the C key) for touch screens.
        viewButton = UIFactory.CreateButton(root, "ViewButton", "VISTA", OnViewClicked, UIFactory.ButtonColor, 17f);
        UIFactory.Place((RectTransform)viewButton.transform, new Vector2(0f, 1f), new Vector2(142f, -12f), new Vector2(150f, 56f));
    }

    /// <summary>
    /// The Mapa scene's dice button and roll info (still used offline) move into the HUD, under the
    /// top buttons, so they follow the same responsive layout and safe area.
    /// </summary>
    private void AdoptSceneDice(Transform root)
    {
        if (rollButton != null)
        {
            RectTransform dice = (RectTransform)rollButton.transform;
            dice.SetParent(root, false);
            dice.localScale = Vector3.one;
            UIFactory.Place(dice, new Vector2(0f, 1f), new Vector2(16f, -80f), new Vector2(90f, 90f));
        }

        if (gameInfoText != null)
        {
            RectTransform info = gameInfoText.rectTransform;
            info.SetParent(root, false);
            info.localScale = Vector3.one;
            UIFactory.Place(info, new Vector2(0f, 1f), new Vector2(116f, -88f), new Vector2(200f, 70f));
            gameInfoText.fontSize = 18f;
            gameInfoText.alignment = TextAlignmentOptions.TopLeft;
        }
    }

    private void BuildTurnBanner(Transform root)
    {
        Image banner = UIFactory.CreatePanel(root, "TurnBanner", UIFactory.PanelColor);
        UIFactory.Place(banner.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(560f, 112f));

        turnText = UIFactory.CreateText(banner.transform, "TurnText", "", 28f);
        UIFactory.Place(turnText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(540f, 40f));
        AutoSize(turnText, 18f);

        localTurnText = UIFactory.CreateText(banner.transform, "LocalTurnText", "", 24f);
        UIFactory.Place(localTurnText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -44f), new Vector2(540f, 34f));

        hintText = UIFactory.CreateText(banner.transform, "HintText", "", 17f);
        UIFactory.Place(hintText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -78f), new Vector2(540f, 30f));
        hintText.color = new Color(0.85f, 0.85f, 0.85f);
        AutoSize(hintText, 12f);
    }

    private void BuildPlayersPanel(Transform root)
    {
        Image panel = UIFactory.CreatePanel(root, "PlayersPanel", UIFactory.PanelColor);
        UIFactory.Place(panel.rectTransform, new Vector2(1f, 1f), new Vector2(-12f, -12f), new Vector2(330f, 380f));

        TextMeshProUGUI title = UIFactory.CreateText(panel.transform, "Title", "JUGADORES", 22f);
        UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(310f, 30f));
        title.color = UIFactory.AccentColor;

        playersText = UIFactory.CreateText(panel.transform, "PlayersText", "", 17f, TextAlignmentOptions.TopLeft);
        UIFactory.Place(playersText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(310f, 280f));
        playersText.richText = true;
        AutoSize(playersText, 11f);

        objectiveText = UIFactory.CreateText(panel.transform, "Objective", "", 15f, TextAlignmentOptions.BottomLeft);
        UIFactory.Place(objectiveText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(310f, 54f));
        objectiveText.color = UIFactory.AccentColor;
        AutoSize(objectiveText, 11f);
    }

    private void BuildActionsPanel(Transform root)
    {
        Image panel = UIFactory.CreatePanel(root, "ActionsPanel", UIFactory.PanelColor);
        UIFactory.Place(panel.rectTransform, new Vector2(0f, 0f), new Vector2(12f, 12f), new Vector2(300f, 414f));

        livesText = UIFactory.CreateText(panel.transform, "LivesText", "", 26f);
        UIFactory.Place(livesText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(280f, 36f));

        rollTextButton = UIFactory.CreateButton(panel.transform, "RollDiceButton", "TIRAR DADO", OnRollClicked, UIFactory.ButtonColor, 26f);
        UIFactory.Place((RectTransform)rollTextButton.transform, new Vector2(0.5f, 1f), new Vector2(0f, -42f), new Vector2(280f, 62f));

        // Special cards played on the own turn (Silencio líquido, Cívica, Comida Frita).
        turnCardButton = UIFactory.CreateButton(panel.transform, "TurnCardButton", "USAR CARTA ESPECIAL", OnTurnCardClicked, new Color(0.45f, 0.18f, 0.70f), 18f);
        UIFactory.Place((RectTransform)turnCardButton.transform, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(280f, 50f));

        string moveHint = ResponsiveCanvas.IsTouchDevice ? "MOVER" : "MOVER (WASD / flechas)";
        TextMeshProUGUI moveLabel = UIFactory.CreateText(panel.transform, "MoveLabel", moveHint, 15f);
        UIFactory.Place(moveLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -164f), new Vector2(280f, 22f));

        // Big touch-friendly pad.
        AddMoveButton(panel.transform, "↑", Vector2Int.up, new Vector2(0f, -190f));
        AddMoveButton(panel.transform, "←", Vector2Int.left, new Vector2(-94f, -264f));
        AddMoveButton(panel.transform, "→", Vector2Int.right, new Vector2(94f, -264f));
        AddMoveButton(panel.transform, "↓", Vector2Int.down, new Vector2(0f, -338f));
    }

    private void AddMoveButton(Transform parent, string label, Vector2Int direction, Vector2 position)
    {
        Button button = UIFactory.CreateButton(parent, "Move" + direction, label, () => OnMoveClicked(direction), UIFactory.ButtonColor, 34f);
        UIFactory.Place((RectTransform)button.transform, new Vector2(0.5f, 1f), position, new Vector2(90f, 68f));
        moveButtons.Add(button);
    }

    private const float HandCardWidth = 140f;
    private const float HandCardHeight = HandCardWidth / CardLayout.Aspect;
    private const float HandPanelHeight = HandCardHeight + 40f;

    private void BuildHandPanel(Transform root)
    {
        Image panel = UIFactory.CreatePanel(root, "HandPanel", UIFactory.PanelColor);
        UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(500f, HandPanelHeight));
        handPanel = panel.gameObject;

        handTitle = UIFactory.CreateText(panel.transform, "Title", "MIS CARTAS", 18f);
        UIFactory.Place(handTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -3f), new Vector2(480f, 26f));
        handTitle.color = UIFactory.AccentColor;
        AutoSize(handTitle, 12f);

        RectTransform container = UIFactory.CreateRect(panel.transform, "Cards");
        UIFactory.Place(container, new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(480f, HandCardHeight + 4f));
        HorizontalLayoutGroup layout = container.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        handContainer = container;
    }

    private void BuildCombatPanel(Transform root)
    {
        // Above the hand so the cards stay visible and selectable during the fight.
        Image panel = UIFactory.CreatePanel(root, "CombatPanel", new Color(0.12f, 0.05f, 0.05f, 0.94f));
        UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, HandPanelHeight + 20f), new Vector2(600f, 326f));
        combatPanel = panel.gameObject;

        combatTitle = UIFactory.CreateText(panel.transform, "Title", "", 24f);
        UIFactory.Place(combatTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(580f, 36f));
        combatTitle.color = new Color(1f, 0.45f, 0.35f);
        AutoSize(combatTitle, 16f);

        combatBody = UIFactory.CreateText(panel.transform, "Body", "", 18f);
        UIFactory.Place(combatBody.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -46f), new Vector2(580f, 150f));
        AutoSize(combatBody, 12f);

        combatResult = UIFactory.CreateText(panel.transform, "Result", "", 30f);
        UIFactory.Place(combatResult.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -198f), new Vector2(580f, 46f));
        AutoSize(combatResult, 16f);

        useCardButton = UIFactory.CreateButton(panel.transform, "UseCardButton", "USAR CARTA", OnUseCardClicked, UIFactory.OkColor, 18f);
        UIFactory.Place((RectTransform)useCardButton.transform, new Vector2(0.5f, 0f), new Vector2(-195f, 14f), new Vector2(180f, 62f));

        skipCardButton = UIFactory.CreateButton(panel.transform, "SkipCardButton", "NO USAR CARTA", OnSkipCardClicked, UIFactory.ButtonColor, 18f);
        UIFactory.Place((RectTransform)skipCardButton.transform, new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(180f, 62f));

        combatRollButton = UIFactory.CreateButton(panel.transform, "CombatRollButton", "TIRAR DADO", OnCombatRollClicked, UIFactory.DangerColor, 18f);
        UIFactory.Place((RectTransform)combatRollButton.transform, new Vector2(0.5f, 0f), new Vector2(195f, 14f), new Vector2(180f, 62f));
    }

    private void BuildEndPanel(Transform root)
    {
        Image overlay = UIFactory.CreatePanel(root, "EndOverlay", new Color(0f, 0f, 0f, 0.75f));
        UIFactory.Stretch(overlay.rectTransform);
        endPanel = overlay.gameObject;

        Image panel = UIFactory.CreatePanel(overlay.transform, "EndPanel", UIFactory.PanelColor);
        UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720f, 440f));
        panel.gameObject.AddComponent<ScaleToFit>();

        endTitle = UIFactory.CreateText(panel.transform, "Title", "PARTIDA TERMINADA", 48f);
        UIFactory.Place(endTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(680f, 70f));
        endTitle.color = UIFactory.AccentColor;

        endBody = UIFactory.CreateText(panel.transform, "Body", "", 26f);
        UIFactory.Place(endBody.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(680f, 200f));
        AutoSize(endBody, 16f);

        restartButton = UIFactory.CreateButton(panel.transform, "RestartButton", "REINICIAR", OnRestartClicked, UIFactory.OkColor, 28f);
        UIFactory.Place((RectTransform)restartButton.transform, new Vector2(0.5f, 0f), new Vector2(-160f, 26f), new Vector2(290f, 78f));

        Button menuButton = UIFactory.CreateButton(panel.transform, "MenuButton", "VOLVER AL MENÚ", OnMenuClicked, UIFactory.ButtonColor, 24f);
        UIFactory.Place((RectTransform)menuButton.transform, new Vector2(0.5f, 0f), new Vector2(160f, 26f), new Vector2(290f, 78f));

        endPanel.SetActive(false);
    }

    /// <summary>Shrinks long texts instead of overflowing on small screens.</summary>
    private static void AutoSize(TextMeshProUGUI text, float minSize)
    {
        text.enableAutoSizing = true;
        text.fontSizeMin = minSize;
        text.fontSizeMax = text.fontSize;
    }

    // =========================================================
    // REFRESH
    // =========================================================

    private void Refresh()
    {
        if (turnText == null)
            return;

        TurnManager turns = TurnManager.Instance;
        GameStateManager game = GameStateManager.Instance;
        PlayerNetworkData local = PlayerNetworkData.Local;

        if (turns == null || game == null || !turns.IsSpawned || turns.State.Value == TurnState.WaitingForBoard)
        {
            turnText.text = "CARGANDO TABLERO...";
            localTurnText.text = "";
            hintText.text = "Esperando a que todos los jugadores estén listos";
            SetActionButtons(false, false);
            combatPanel.SetActive(false);
            handPanel.SetActive(false);
            RefreshToasts();
            return;
        }

        RefreshTurn(turns, local);
        RefreshPlayers(turns);
        RefreshLocal(turns, local);
        RefreshHand(local);
        RefreshCombat(local);
        RefreshRoulette();
        RefreshMission(game, local);
        RefreshEnd(turns, game, local);
        RefreshToasts();

        RefreshViewButton();

        NetworkManager nm = NetworkManager.Singleton;

        if (nm != null)
            networkText.text = (ResponsiveCanvas.IsTouchDevice ? "Pellizca: zoom     " : "Rueda: zoom · C: vista general     ") + (nm.IsHost ? "HOST" : "CLIENTE") + " · " + PlayerNetworkData.All.Count + " jugadores · Seed " + game.BoardSeed.Value;
    }

    private void RefreshTurn(TurnManager turns, PlayerNetworkData local)
    {
        PlayerNetworkData current = turns.CurrentPlayer;
        string currentName = current != null ? $"{current.Name} ({CharacterName(current)})" : "-";

        turnText.text = "TURNO DE: " + currentName;

        if (local == null || turns.State.Value == TurnState.GameOver)
        {
            localTurnText.text = "";
        }
        else if (!local.IsAlive)
        {
            localTurnText.text = local.State.Value == PlayerLifeState.Eliminated ? "<color=#FF5050>ESTÁS ELIMINADO</color>" : "";
        }
        else if (turns.IsLocalPlayersTurn)
        {
            localTurnText.text = "<color=#FFD040>ES TU TURNO</color>";
        }
        else
        {
            localTurnText.text = "<color=#AAAAAA>ESPERANDO TURNO...</color>";
        }

        string hint;

        switch (turns.State.Value)
        {
            case TurnState.WaitingForRoll: hint = turns.IsLocalPlayersTurn ? "Pulsa TIRAR DADO" : "Esperando la tirada"; break;
            case TurnState.Moving:
                hint = turns.IsLocalPlayersTurn ? $"Muévete con {(ResponsiveCanvas.IsTouchDevice ? "las flechas" : "WASD / flechas")} · {turns.MovesRemaining.Value} restantes" : $"Moviéndose · {turns.MovesRemaining.Value} restantes";
                if (turns.WallWalk.Value) hint += " · <color=#D080FF>atraviesa paredes</color>";
                break;
            case TurnState.CheckingTile: hint = "Comprobando casilla..."; break;
            case TurnState.Combat: hint = CombatManager.Instance != null && CombatManager.Instance.IsDuel.Value ? "¡Duelo en curso!" : "¡Combate en curso!"; break;
            case TurnState.TurnEnd: hint = "Fin del turno"; break;
            case TurnState.GameOver: hint = "Partida terminada"; break;
            case TurnState.Roulette: hint = "¡Ruleta!"; break;
            case TurnState.Mission: hint = "¡Misión en curso!"; break;
            default: hint = ""; break;
        }

        float secondsLeft = turns.SecondsLeft;

        if (secondsLeft >= 0f)
            hint += $"   <color=#FF8080>Tiempo: {Mathf.CeilToInt(secondsLeft)}s</color>";

        hintText.text = hint;

        if (gameInfoText != null)
        {
            string roll = turns.LastRoll.Value > 0 ? turns.LastRoll.Value.ToString() : "-";
            gameInfoText.text = $"DADO: [{roll}]\nMOVIMIENTOS: {turns.MovesRemaining.Value}";
        }
    }

    private void RefreshPlayers(TurnManager turns)
    {
        StringBuilder sb = new StringBuilder();
        List<PlayerNetworkData> players = new List<PlayerNetworkData>(PlayerNetworkData.All);
        players.Sort((a, b) => a.PlayerId.Value.CompareTo(b.PlayerId.Value));

        foreach (PlayerNetworkData p in players)
        {
            bool isCurrent = p.PlayerId.Value == turns.CurrentPlayerId.Value && turns.State.Value != TurnState.GameOver;
            string color = ColorUtility.ToHtmlStringRGB(p.Character != null && p.Character.boardColor.a > 0f ? p.Character.boardColor : Color.white);

            sb.Append(isCurrent ? "<color=#FFD040>► </color>" : "   ");
            sb.Append($"<color=#{color}><b>{p.Name}</b></color> — {CharacterName(p)}");

            if (p.IsOwner)
                sb.Append(" <color=#FFD040>(tú)</color>");

            sb.Append('\n').Append("     ");

            switch (p.State.Value)
            {
                case PlayerLifeState.Eliminated:
                    sb.Append("<color=#FF5050>ELIMINADO</color>");
                    break;
                case PlayerLifeState.Disconnected:
                    sb.Append("<color=#888888>DESCONECTADO</color>");
                    break;
                default:
                    sb.Append($"<color=#FF4060>{Hearts(p.Lives.Value)}</color>");
                    break;
            }

            string mission = p.MissionCompleted.Value ? "<color=#60FF60>Misión hecha</color>" : "Misión pendiente";
            sb.Append($"   {mission}   Cartas {p.HandCount.Value}\n");

            string status = p.IsAlive ? p.StatusSummary() : "";
            sb.Append(string.IsNullOrEmpty(status) ? "\n" : $"     <size=85%><color=#D080FF>{status}</color></size>\n\n");
        }

        playersText.text = sb.ToString();
    }

    private void RefreshLocal(TurnManager turns, PlayerNetworkData local)
    {
        livesText.text = local != null ? $"<color=#FF4060>{Hearts(local.Lives.Value)}</color>" : "";

        bool canRoll = turns.CanLocalPlayerRoll();
        bool canMove = turns.CanLocalPlayerMove();
        SetActionButtons(canRoll, canMove);

        if (turnCardButton != null)
            turnCardButton.interactable = turns.CanLocalPlayTurnCard(selectedHandIndex);
    }

    private void SetActionButtons(bool canRoll, bool canMove)
    {
        if (rollTextButton != null)
            rollTextButton.interactable = canRoll;

        if (rollButton != null)
            rollButton.interactable = canRoll;

        foreach (Button b in moveButtons)
            b.interactable = canMove;
    }

    private void RefreshHand(PlayerNetworkData local)
    {
        handPanel.SetActive(local != null);

        if (local == null)
            return;

        bool changed = shownHand.Count != local.Hand.Count;

        for (int i = 0; !changed && i < shownHand.Count; i++)
            changed = shownHand[i] != local.Hand[i];

        if (changed)
            RebuildHand(local);

        bool canUse = CombatManager.Instance != null && CombatManager.Instance.CanLocalUseCard();
        bool canTurnCard = TurnManager.Instance != null && TurnManager.Instance.CanLocalPlayTurnCard(selectedHandIndex);

        if (canUse)
            handTitle.text = "MIS CARTAS — elige una y pulsa USAR CARTA";
        else if (canTurnCard)
            handTitle.text = "MIS CARTAS — pulsa USAR CARTA ESPECIAL";
        else if (selectedHandIndex >= 0 && selectedHandIndex < local.Hand.Count)
            handTitle.text = "MIS CARTAS — " + CardRules.TimingHint(CardDatabase.Instance.GetById(local.Hand[selectedHandIndex]));
        else
            handTitle.text = "MIS CARTAS";

        for (int i = 0; i < handCardGlows.Count; i++)
            handCardGlows[i].SetActive(i == selectedHandIndex);
    }

    private void RebuildHand(PlayerNetworkData local)
    {
        foreach (Transform child in handContainer)
            Destroy(child.gameObject);

        shownHand.Clear();
        handCardGlows.Clear();
        selectedHandIndex = -1;

        for (int i = 0; i < local.Hand.Count; i++)
        {
            int cardId = local.Hand[i];
            shownHand.Add(cardId);
            handCardGlows.Add(CreateCardWidget(cardId, i));
        }

        if (local.Hand.Count == 0)
        {
            TextMeshProUGUI empty = UIFactory.CreateText(handContainer, "Empty", "Sin cartas", 22f);
            empty.rectTransform.sizeDelta = new Vector2(300f, 60f);
        }
    }

    /// <summary>
    /// Game card with the final art: type frame (CardType.frame), big value or icon in the
    /// picture window, name on the banner and description in the black box.
    /// Returns the selection glow object.
    /// </summary>
    private GameObject CreateCardWidget(int cardId, int index)
    {
        CardData card = CardDatabase.Instance.GetById(cardId);
        CardType type = card != null ? card.type : null;
        Color typeColor = type != null ? type.color : Color.gray;

        // Root: invisible hit area + button.
        Image root = UIFactory.CreatePanel(handContainer, "Card_" + cardId, new Color(0f, 0f, 0f, 0f));
        root.rectTransform.sizeDelta = new Vector2(HandCardWidth, HandCardHeight);

        Button button = root.gameObject.AddComponent<Button>();
        button.targetGraphic = root;
        button.onClick.AddListener(() => selectedHandIndex = selectedHandIndex == index ? -1 : index);

        // Selection glow behind the card.
        Image glow = UIFactory.CreatePanel(root.transform, "Glow", UIFactory.AccentColor);
        UIFactory.Stretch(glow.rectTransform, -6f);
        glow.raycastTarget = false;
        glow.gameObject.SetActive(false);

        CardLayout.AddImage(root.transform, "WindowFill", CardLayout.Window, null, CardLayout.WindowFill, false);

        if (card != null && card.icon != null)
        {
            CardLayout.AddImage(root.transform, "Icon", CardLayout.Picture, card.icon, Color.white, true);
        }
        else
        {
            string label = card != null && card.effect != null ? card.effect.ShortLabel : null;
            TextMeshProUGUI value = UIFactory.CreateText(root.transform, "Value", string.IsNullOrEmpty(label) ? "?" : label, 64f);
            CardLayout.Fit(value.rectTransform, CardLayout.Sub(CardLayout.Window, 0.15f, 0.05f, 1f, 0.95f));
            CardLayout.StyleText(value, 72f, typeColor, true);
            value.alignment = TextAlignmentOptions.Center;
            value.outlineWidth = 0.15f;
            value.outlineColor = new Color32(20, 20, 20, 255);
        }

        if (type != null && type.frame != null)
        {
            Image frame = CardLayout.AddImage(root.transform, "Frame", Rect.MinMaxRect(0f, 0f, 1f, 1f), type.frame, Color.white, false);
            frame.raycastTarget = false;
        }

        TextMeshProUGUI title = UIFactory.CreateText(root.transform, "Name", card != null ? card.displayName : "Carta " + cardId, 20f);
        CardLayout.Fit(title.rectTransform, CardLayout.Banner);
        CardLayout.StyleText(title, 20f, Color.white, true);
        title.alignment = TextAlignmentOptions.Center;
        title.outlineWidth = 0.2f;
        title.outlineColor = new Color32(0, 0, 0, 255);

        string typeName = type != null ? type.displayName.ToUpper() : "";
        string typeHex = ColorUtility.ToHtmlStringRGB(Color.Lerp(typeColor, Color.white, 0.35f));
        string body = $"<size=80%><color=#{typeHex}>{typeName}</color></size>\n{(card != null ? card.description : "")}";
        TextMeshProUGUI description = UIFactory.CreateText(root.transform, "Description", body, 14f);
        CardLayout.Fit(description.rectTransform, CardLayout.Body);
        CardLayout.StyleText(description, 15f, new Color(0.93f, 0.93f, 0.93f));
        description.alignment = TextAlignmentOptions.Center;

        return glow.gameObject;
    }

    private void RefreshCombat(PlayerNetworkData local)
    {
        CombatManager combat = CombatManager.Instance;

        if (combat == null || !combat.IsSpawned || !combat.IsActive)
        {
            combatPanel.SetActive(false);
            return;
        }

        combatPanel.SetActive(true);

        if (combat.IsDuel.Value)
            RefreshDuel(combat);
        else
            RefreshEventCombat(combat);

        CombatManager.Side side = combat.LocalSide;
        bool isFighter = side != CombatManager.Side.None;

        useCardButton.interactable = combat.CanLocalUseCard() && selectedHandIndex >= 0;
        skipCardButton.interactable = combat.CanLocalSkipCards();
        combatRollButton.interactable = combat.CanLocalRoll();

        useCardButton.gameObject.SetActive(isFighter);
        skipCardButton.gameObject.SetActive(isFighter && !combat.IsDuel.Value);
        combatRollButton.gameObject.SetActive(isFighter);
    }

    private void RefreshEventCombat(CombatManager combat)
    {
        combatTitle.text = "¡TE HAS ENCONTRADO CON UNA AMENAZA!";

        PlayerNetworkData fighter = PlayerNetworkData.GetByPlayerId(combat.CombatPlayerId.Value);
        string fighterName = fighter != null ? fighter.Name : "?";
        bool isLocal = combat.IsLocalFighter;

        StringBuilder body = new StringBuilder();
        body.Append(isLocal ? "<color=#FFD040>¡Te toca combatir!</color>" : $"{fighterName} está combatiendo");
        body.Append($"\nNecesitas sacar <b>{combat.Required.Value}</b> o más (dado 1–6 + bonus)");

        if (combat.LifeProtected.Value)
            body.Append("\n<color=#80C0FF>Protegido: no perderá vida si falla</color>");

        AppendLastCard(body, combat);

        CombatPhase phase = combat.Phase.Value;

        if ((phase == CombatPhase.Won || phase == CombatPhase.Lost || phase == CombatPhase.Resolving) && combat.Roll.Value > 0)
            body.Append($"\nTirada {RollText(combat.Roll.Value, combat.RollModifier.Value)} vs {combat.Required.Value}");
        else if (combat.RollModifier.Value != 0)
            body.Append($"\nBonus a la tirada: {Signed(combat.RollModifier.Value)}");

        combatBody.text = body.ToString();

        switch (phase)
        {
            case CombatPhase.Escaped:
                combatResult.text = "<color=#80C0FF>¡ESCAPÓ DE LA ENTIDAD!</color>";
                break;
            case CombatPhase.Won:
                combatResult.text = "<color=#60FF60>¡SUPERASTE EL EVENTO!</color>";
                break;
            case CombatPhase.Lost:
                combatResult.text = combat.LifeLoss.Value > 0
                    ? $"<color=#FF5050>FALLASTE EL EVENTO</color> <size=70%>(-{combat.LifeLoss.Value} {Heart})</size>"
                    : "<color=#FF5050>FALLASTE EL EVENTO</color> <size=70%>(sin daño)</size>";
                break;
            case CombatPhase.CardPhase:
                combatResult.text = isLocal ? "<size=70%>¿Usar una carta?</size>" : "";
                break;
            case CombatPhase.Rolling:
                combatResult.text = isLocal ? "<size=70%>¡Tira el dado!</size>" : "";
                break;
            default:
                combatResult.text = "";
                break;
        }
    }

    private void RefreshDuel(CombatManager combat)
    {
        combatTitle.text = "¡DUELO!";

        PlayerNetworkData attacker = PlayerNetworkData.GetByPlayerId(combat.CombatPlayerId.Value);
        PlayerNetworkData defender = PlayerNetworkData.GetByPlayerId(combat.DefenderPlayerId.Value);
        CombatManager.Side side = combat.LocalSide;

        StringBuilder body = new StringBuilder();

        if (side != CombatManager.Side.None)
            body.Append("<color=#FFD040>¡Estás en un duelo! Usa una carta si quieres y tira tu dado.</color>\n");
        else
            body.Append("Mayor resultado gana · el perdedor pierde una vida\n");

        body.Append(DuelLine(attacker, combat.Roll.Value, combat.RollModifier.Value, combat.LifeProtected.Value));
        body.Append("\n<size=80%>contra</size>\n");
        body.Append(DuelLine(defender, combat.DefenderRoll.Value, combat.DefenderModifier.Value, combat.DefenderProtected.Value));
        AppendLastCard(body, combat);
        combatBody.text = body.ToString();

        string attackerName = attacker != null ? attacker.Name : "?";
        string defenderName = defender != null ? defender.Name : "?";

        switch (combat.Phase.Value)
        {
            case CombatPhase.Won:
                combatResult.text = $"<color=#60FF60>¡GANA {attackerName}!</color>" + LossText(defenderName, combat.DefenderLifeLoss.Value);
                break;
            case CombatPhase.Lost:
                combatResult.text = $"<color=#60FF60>¡GANA {defenderName}!</color>" + LossText(attackerName, combat.LifeLoss.Value);
                break;
            case CombatPhase.Escaped:
                combatResult.text = "<color=#80C0FF>¡ESCAPÓ DEL DUELO!</color> <size=60%>nadie pierde vida</size>";
                break;
            case CombatPhase.Draw:
                combatResult.text = "<color=#FFD040>¡EMPATE!</color> <size=60%>nadie pierde vida</size>";
                break;
            default:
                combatResult.text = side != CombatManager.Side.None && combat.HasRolled(side)
                    ? "<size=70%>Esperando la tirada del rival...</size>"
                    : "";
                break;
        }
    }

    private static string DuelLine(PlayerNetworkData p, int roll, int modifier, bool protectedFromDamage)
    {
        string name = p != null ? p.Name : "?";
        string rollText = roll > 0 ? RollText(roll, modifier) : (modifier != 0 ? $"sin tirar (bonus {Signed(modifier)})" : "sin tirar");
        string shield = protectedFromDamage ? " <color=#80C0FF>(protegido)</color>" : "";
        return $"<b>{name}</b>: {rollText}{shield}";
    }

    private static string LossText(string loserName, int lifeLoss)
    {
        return lifeLoss > 0
            ? $" <size=60%>{loserName} -{lifeLoss} {Heart}</size>"
            : $" <size=60%>{loserName} sin daño</size>";
    }

    private static string RollText(int roll, int modifier)
    {
        return $"{roll} {Signed(modifier)} = <b>{roll + modifier}</b>";
    }

    private static string Signed(int value)
    {
        return value >= 0 ? "+ " + value : "- " + (-value);
    }

    private static void AppendLastCard(StringBuilder body, CombatManager combat)
    {
        string lastCard = combat.LastCardText.Value.ToString();

        if (!string.IsNullOrEmpty(lastCard))
            body.Append($"\n<color=#C0FFC0>{lastCard}</color>");
    }

    private void RefreshEnd(TurnManager turns, GameStateManager game, PlayerNetworkData local)
    {
        bool over = turns.State.Value == TurnState.GameOver;
        endPanel.SetActive(over);

        if (!over)
            return;

        int winnerId = game.WinnerPlayerId.Value;
        PlayerNetworkData winner = PlayerNetworkData.GetByPlayerId(winnerId);

        StringBuilder sb = new StringBuilder();

        if (winner != null)
        {
            sb.Append($"Ganador: <b>{winner.Name}</b> ({CharacterName(winner)})");
            sb.Append(winner.MissionCompleted.Value
                ? "\n<size=80%>¡completó su misión!</size>"
                : "\n<size=80%>último jugador en pie</size>");

            if (local != null && local.PlayerId.Value == winnerId)
                sb.Append("\n\n<color=#FFD040>¡HAS COMPLETADO LA PARTIDA!</color>");
        }
        else
        {
            sb.Append("Sin ganador\n<size=80%>todos los jugadores perdieron sus vidas</size>");
        }

        bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;

        if (!isHost)
            sb.Append("\n\n<size=70%>Esperando a que el Host reinicie...</size>");

        endBody.text = sb.ToString();
        restartButton.gameObject.SetActive(isHost);
    }

    // =========================================================
    // ROULETTE
    // =========================================================

    private void BuildRoulette(Transform root)
    {
        if (roulettePrefab == null)
        {
            Debug.LogError("GameHUD: roulettePrefab not assigned (Prefabs/UI/RouletteWheel). Run PPOP/Setup Prototype.");
            return;
        }

        roulette = Instantiate(roulettePrefab, root);
        roulette.name = "RouletteWheel";
        roulette.SpinClicked += OnSpinClicked;
        roulette.Build(RouletteConfig.Instance);
        // The wheel panel is designed big; shrink it on small screens.
        ScaleToFit.ApplyToChildren(roulette.transform);
        roulette.Hide();
    }

    /// <summary>Mirrors RouletteManager (Host state) on the wheel view.</summary>
    private void RefreshRoulette()
    {
        RouletteManager manager = RouletteManager.Instance;

        if (roulette == null)
            return;

        if (manager == null || !manager.IsVisible)
        {
            if (roulette.gameObject.activeSelf)
                roulette.Hide();

            return;
        }

        PlayerNetworkData spinner = PlayerNetworkData.GetByPlayerId(manager.PlayerId.Value);
        bool isLocalSpinner = spinner != null && spinner.IsOwner;
        roulette.Show(spinner != null ? spinner.Name : "?", isLocalSpinner, manager.CanLocalSpin(), manager.AutoSpinSecondsLeft);

        // New spin from the Host: animate to the same result on every instance.
        if (manager.SpinId.Value != shownSpinId && manager.Phase.Value != RoulettePhase.WaitingSpin)
        {
            shownSpinId = manager.SpinId.Value;

            if (manager.Phase.Value == RoulettePhase.Spinning)
                roulette.PlaySpin(manager.ResultIndex.Value, RouletteConfig.Instance.spinSeconds);
        }

        if (manager.Phase.Value == RoulettePhase.ShowingResult && manager.Result != null)
            roulette.ShowResult(manager.Result.resultText);
    }

    private void OnSpinClicked()
    {
        if (RouletteManager.Instance != null)
            RouletteManager.Instance.RequestSpinFromLocal();
    }

    // =========================================================
    // MISSIONS
    // =========================================================

    private void RefreshMission(GameStateManager game, PlayerNetworkData local)
    {
        // Objective of the local player.
        if (objectiveText != null)
        {
            string objective = "";

            if (local != null && local.MissionCompleted.Value)
                objective = "OBJETIVO: ¡misión completada!";
            else if (local != null && local.Character != null && game.TryGetMissionCell(local, out _))
                objective = $"OBJETIVO: llega a la casilla MISIÓN del Cuarto de {local.Character.characterName} y complétala";

            objectiveText.text = objective;
        }

        MissionManager manager = MissionManager.Instance;
        Transform canvas = turnText != null ? turnText.canvas.transform : null;

        if (manager == null || !manager.IsVisible || canvas == null)
        {
            CloseMinigame();
            return;
        }

        // Open the character's minigame once per mission (placeholder if none assigned).
        if (missionMinigame == null || missionShownFor != manager.PlayerId.Value)
        {
            CloseMinigame();

            MissionDefinition definition = manager.CurrentDefinition;
            PlayerNetworkData player = PlayerNetworkData.GetByPlayerId(manager.PlayerId.Value);
            GameObject instance;

            if (definition != null && definition.minigamePrefab != null && definition.minigamePrefab.GetComponent<MissionMinigame>() != null)
            {
                instance = Instantiate(definition.minigamePrefab, canvas, false);
            }
            else
            {
                instance = new GameObject("PlaceholderMission", typeof(RectTransform));
                instance.transform.SetParent(canvas, false);
                instance.AddComponent<PlaceholderMissionMinigame>();
            }

            missionMinigame = instance.GetComponent<MissionMinigame>();
            missionShownFor = manager.PlayerId.Value;
            missionResultShown = MissionPhase.None;
            missionMinigame.Finished += success => MissionManager.Instance?.RequestFinishFromLocal(success);
            missionMinigame.Begin(definition, player != null ? player.Name : "?", player != null ? CharacterName(player) : "?", manager.IsLocalMission);
            // Minigame panels are designed big; shrink them on small screens.
            ScaleToFit.ApplyToChildren(instance.transform);

            // Keep the end screen above the minigame.
            if (endPanel != null)
                endPanel.transform.SetAsLastSibling();
        }

        MissionPhase phase = manager.Phase.Value;

        if ((phase == MissionPhase.Completed || phase == MissionPhase.Failed) && missionResultShown != phase)
        {
            missionResultShown = phase;
            missionMinigame.ShowResult(phase == MissionPhase.Completed);
        }
    }

    private void CloseMinigame()
    {
        if (missionMinigame != null)
            Destroy(missionMinigame.gameObject);

        missionMinigame = null;
        missionShownFor = -1;
        missionResultShown = MissionPhase.None;
    }

    // =========================================================
    // TOASTS
    // =========================================================

    private void ShowToast(string message)
    {
        toasts.Add((message, Time.unscaledTime + ToastSeconds));

        if (toasts.Count > 3)
            toasts.RemoveAt(0);

        RefreshToasts();
    }

    private void RefreshToasts()
    {
        toasts.RemoveAll(t => t.until < Time.unscaledTime);

        StringBuilder sb = new StringBuilder();

        foreach ((string text, float _) in toasts)
            sb.AppendLine(text);

        toastText.text = sb.ToString();
    }

    // =========================================================
    // BUTTONS
    // =========================================================

    private void OnRollClicked()
    {
        if (boardManager != null)
            boardManager.RollDice();
        else if (TurnManager.Instance != null)
            TurnManager.Instance.RequestRollFromLocalPlayer();
    }

    private void OnMoveClicked(Vector2Int direction)
    {
        if (TurnManager.Instance != null)
            TurnManager.Instance.RequestMoveFromLocalPlayer(direction);
    }

    private void OnUseCardClicked()
    {
        PlayerNetworkData local = PlayerNetworkData.Local;

        if (local == null || selectedHandIndex < 0 || selectedHandIndex >= local.Hand.Count)
            return;

        CombatManager.Instance.RequestUseCardFromLocal(selectedHandIndex, local.Hand[selectedHandIndex]);
        selectedHandIndex = -1;
    }

    private void OnTurnCardClicked()
    {
        if (TurnManager.Instance == null)
            return;

        TurnManager.Instance.RequestPlayTurnCardFromLocal(selectedHandIndex);
        selectedHandIndex = -1;
    }

    private void OnSkipCardClicked()
    {
        CombatManager.Instance.RequestSkipCardsFromLocal();
    }

    private void OnCombatRollClicked()
    {
        CombatManager.Instance.RequestRollFromLocal();
    }

    private void OnViewClicked()
    {
        if (boardCamera == null)
            boardCamera = FindFirstObjectByType<BoardCameraController>();

        if (boardCamera != null)
            boardCamera.ToggleView();

        RefreshViewButton();
    }

    private void RefreshViewButton()
    {
        if (viewButton == null)
            return;

        if (boardCamera == null)
            boardCamera = FindFirstObjectByType<BoardCameraController>();

        viewButton.gameObject.SetActive(boardCamera != null);

        if (boardCamera != null)
            UIFactory.SetLabel(viewButton, boardCamera.IsFollowing ? "VER TABLERO" : "SEGUIR TURNO");
    }

    private void OnRestartClicked()
    {
        restartButton.interactable = false;

        if (GameStateManager.Instance != null)
            GameStateManager.Instance.ServerRestart();
    }

    private void OnMenuClicked()
    {
        if (NetworkConnectionManager.Instance != null)
            NetworkConnectionManager.Instance.ReturnToMenu("");
    }

    // =========================================================
    // HELPERS
    // =========================================================

    private static string Hearts(int lives)
    {
        StringBuilder sb = new StringBuilder();

        for (int i = 0; i < Mathf.Max(0, lives); i++)
            sb.Append(Heart).Append(' ');

        return lives > 0 ? sb.ToString().TrimEnd() : "0";
    }

    private static string CharacterName(PlayerNetworkData p)
    {
        CharacterData data = p.Character;
        return data != null ? data.characterName : "?";
    }
}
