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
    private const float ToastSeconds = 3.5f;
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
    private Image turnBanner;
    private TextMeshProUGUI movesLabel;
    private DiceRollView diceView;
    private int shownRollCount = -1;

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
    private readonly List<HandCard> handCards = new List<HandCard>();
    private bool handRaised;
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
        PulseButtons();
        LayoutHand();

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
        diceView = DiceRollView.Create(overlays);
        BuildEndPanel(overlays);

        // Board effects (steps, turn marker, fights, lives, missions). Local and purely visual.
        if (GetComponent<GameFx>() == null)
            gameObject.AddComponent<GameFx>();
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
        UIFactory.Place(panel, new Vector2(0.5f, 1f), new Vector2(0f, -178f), new Vector2(680f, 130f));
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

            // Duplicate of the TIRAR DADO button (offline UI); the HUD button and the dice animation replace it.
            dice.gameObject.SetActive(false);
        }

        if (gameInfoText != null)
        {
            RectTransform info = gameInfoText.rectTransform;
            info.SetParent(root, false);
            info.localScale = Vector3.one;
            UIFactory.Place(info, new Vector2(0f, 1f), new Vector2(116f, -88f), new Vector2(200f, 70f));
            gameInfoText.fontSize = 18f;
            gameInfoText.alignment = TextAlignmentOptions.TopLeft;
            gameInfoText.gameObject.SetActive(false);
        }
    }

    private void BuildTurnBanner(Transform root)
    {
        turnBanner = UIFactory.CreatePanel(root, "TurnBanner", UIFactory.PanelColor);
        UseRounded(turnBanner);
        UIFactory.Place(turnBanner.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(560f, 92f));

        turnText = UIFactory.CreateText(turnBanner.transform, "TurnText", "", 34f);
        UIFactory.Place(turnText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(540f, 46f));
        turnText.fontStyle = FontStyles.Bold;
        AutoSize(turnText, 20f);

        hintText = UIFactory.CreateText(turnBanner.transform, "HintText", "", 20f);
        UIFactory.Place(hintText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -52f), new Vector2(540f, 32f));
        hintText.color = new Color(0.9f, 0.9f, 0.9f);
        AutoSize(hintText, 13f);

        // Same place as the hint: replaces it when the local player is eliminated.
        localTurnText = UIFactory.CreateText(turnBanner.transform, "LocalTurnText", "", 20f);
        UIFactory.Place(localTurnText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -52f), new Vector2(540f, 32f));
    }

    private void BuildPlayersPanel(Transform root)
    {
        Image panel = UIFactory.CreatePanel(root, "PlayersPanel", UIFactory.PanelColor);
        UIFactory.Place(panel.rectTransform, new Vector2(1f, 1f), new Vector2(-12f, -12f), new Vector2(330f, 380f));

        UseRounded(panel);
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
        UseRounded(panel);
        UIFactory.Place(panel.rectTransform, new Vector2(0f, 0f), new Vector2(12f, 12f), new Vector2(300f, 414f));

        livesText = UIFactory.CreateText(panel.transform, "LivesText", "", 26f);
        UIFactory.Place(livesText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(280f, 36f));

        rollTextButton = UIFactory.CreateButton(panel.transform, "RollDiceButton", "TIRAR DADO", OnRollClicked, UIFactory.ButtonColor, 26f);
        UIFactory.Place((RectTransform)rollTextButton.transform, new Vector2(0.5f, 1f), new Vector2(0f, -42f), new Vector2(280f, 62f));

        // Special cards played on the own turn (Silencio líquido, Cívica, Comida Frita).
        turnCardButton = UIFactory.CreateButton(panel.transform, "TurnCardButton", "USAR CARTA ESPECIAL", OnTurnCardClicked, new Color(0.45f, 0.18f, 0.70f), 18f);
        UIFactory.Place((RectTransform)turnCardButton.transform, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(280f, 50f));

        // Steps left while moving ("3 PASOS"); the arrows only show when they can be used.
        movesLabel = UIFactory.CreateText(panel.transform, "MoveLabel", "", 24f);
        UIFactory.Place(movesLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -160f), new Vector2(280f, 30f));
        movesLabel.fontStyle = FontStyles.Bold;

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

    // Fan of cards (card-game style): overlapping and tilted along an arc; the open one grows.
    private const float HandFanWidth = 560f;
    private const float HandRaisedY = 18f;
    private const float HandLoweredY = -78f;
    private const float HandOpenLift = 125f;
    private const float HandOpenScale = 1.45f;

    private void BuildHandPanel(Transform root)
    {
        // No background: the cards themselves are the hand, like in a card game.
        RectTransform panel = UIFactory.CreateRect(root, "HandPanel");
        UIFactory.Place(panel, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(HandFanWidth + 200f, HandPanelHeight));
        handPanel = panel.gameObject;

        handTitle = UIFactory.CreateText(panel, "Title", "MIS CARTAS", 18f);
        UIFactory.Place(handTitle.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, HandRaisedY + HandCardHeight + 6f), new Vector2(560f, 26f));
        handTitle.color = UIFactory.AccentColor;
        handTitle.outlineWidth = 0.2f;
        handTitle.outlineColor = Color.black;
        AutoSize(handTitle, 12f);

        RectTransform container = UIFactory.CreateRect(panel, "Cards");
        UIFactory.Place(container, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(HandFanWidth, HandCardHeight));
        handContainer = container;
    }

    /// <summary>Opens (selects) a card of the hand, or closes it with -1 (same as clicking it).</summary>
    public void SelectHandCard(int index)
    {
        selectedHandIndex = index >= 0 && index < handCards.Count ? index : -1;
    }

    /// <summary>
    /// Fan slots every frame: raised on your turn (or when you can play a card in a fight), half
    /// hidden at the bottom otherwise; the open card (= selected) grows and rises above the others.
    /// </summary>
    private void LayoutHand()
    {
        int n = handCards.Count;

        if (n == 0)
            return;

        float spacing = n > 1 ? Mathf.Min(HandCardWidth * 0.8f, (HandFanWidth - HandCardWidth) / (n - 1)) : 0f;
        float middle = (n - 1) * 0.5f;
        float baseY = handRaised ? HandRaisedY : HandLoweredY;
        HandCard openCard = null;

        for (int i = 0; i < n; i++)
        {
            HandCard card = handCards[i];

            if (card == null)
                continue;

            float offset = i - middle;
            bool open = i == selectedHandIndex;
            card.Open = open;

            if (open)
            {
                card.SetTarget(new Vector2(offset * spacing * 0.6f, baseY + HandOpenLift - (handRaised ? 0f : HandLoweredY - HandRaisedY)), 0f, HandOpenScale);
                openCard = card;
                continue;
            }

            float lift = card.Hovered ? 22f : 0f;
            card.SetTarget(new Vector2(offset * spacing, baseY - offset * offset * 5f + lift), -offset * 5f, card.Hovered ? 1.06f : 1f);
            card.transform.SetSiblingIndex(i);
        }

        // The open card is drawn above the rest.
        if (openCard != null)
            openCard.transform.SetAsLastSibling();
    }

    private void BuildCombatPanel(Transform root)
    {
        // Bottom-right, under the players list: the board centre (the fighting pawns and their
        // effects) stays visible, and the hand stays free to pick cards.
        Image panel = UIFactory.CreatePanel(root, "CombatPanel", new Color(0.12f, 0.05f, 0.05f, 0.94f));
        UseRounded(panel);
        UIFactory.Place(panel.rectTransform, new Vector2(1f, 0f), new Vector2(-12f, 12f), new Vector2(440f, 310f));
        combatPanel = panel.gameObject;

        combatTitle = UIFactory.CreateText(panel.transform, "Title", "", 24f);
        UIFactory.Place(combatTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(420f, 36f));
        combatTitle.color = new Color(1f, 0.45f, 0.35f);
        AutoSize(combatTitle, 16f);

        combatBody = UIFactory.CreateText(panel.transform, "Body", "", 20f);
        UIFactory.Place(combatBody.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -44f), new Vector2(420f, 140f));
        AutoSize(combatBody, 12f);

        combatResult = UIFactory.CreateText(panel.transform, "Result", "", 30f);
        UIFactory.Place(combatResult.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -186f), new Vector2(420f, 44f));
        AutoSize(combatResult, 16f);

        useCardButton = UIFactory.CreateButton(panel.transform, "UseCardButton", "USAR CARTA", OnUseCardClicked, UIFactory.OkColor, 18f);
        UIFactory.Place((RectTransform)useCardButton.transform, new Vector2(0.5f, 0f), new Vector2(-142f, 12f), new Vector2(132f, 62f));

        skipCardButton = UIFactory.CreateButton(panel.transform, "SkipCardButton", "SIN CARTA", OnSkipCardClicked, UIFactory.ButtonColor, 18f);
        UIFactory.Place((RectTransform)skipCardButton.transform, new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(132f, 62f));

        combatRollButton = UIFactory.CreateButton(panel.transform, "CombatRollButton", "TIRAR DADO", OnCombatRollClicked, UIFactory.DangerColor, 18f);
        UIFactory.Place((RectTransform)combatRollButton.transform, new Vector2(0.5f, 0f), new Vector2(142f, 12f), new Vector2(132f, 62f));
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

    /// <summary>Rounded corners (FxConfig.roundedSquare, 9-sliced) when available.</summary>
    private static void UseRounded(Image image)
    {
        FxConfig config = FxConfig.Instance;

        if (config == null || config.roundedSquare == null)
            return;

        image.sprite = config.roundedSquare;
        image.type = Image.Type.Sliced;
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

        RefreshDice(turns);
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

    /// <summary>Plays the dice animation once per roll (TurnManager.RollCount).</summary>
    private void RefreshDice(TurnManager turns)
    {
        int count = turns.RollCount.Value;

        // Joining mid-game (or the first refresh) must not replay an old roll.
        if (shownRollCount < 0 || diceView == null)
        {
            shownRollCount = count;
            return;
        }

        if (count == shownRollCount)
            return;

        shownRollCount = count;
        PlayerNetworkData current = turns.CurrentPlayer;
        int roll = turns.LastRoll.Value;
        string label = turns.IsLocalPlayersTurn ? $"¡{roll}!" : $"{(current != null ? current.CharacterLabel : "?")}: {roll}";
        diceView.Play(roll, Rules.diceAnimationSeconds, CharacterColor(current), label);
    }

    private void RefreshTurn(TurnManager turns, PlayerNetworkData local)
    {
        PlayerNetworkData current = turns.CurrentPlayer;
        string who = current != null ? current.CharacterLabel : "-";
        bool mine = turns.IsLocalPlayersTurn;
        Color whoColor = CharacterColor(current);
        string dot = $"<color=#{ColorUtility.ToHtmlStringRGB(whoColor)}>●</color> ";

        // Big and short: "¡TU TURNO!" for the active player, the character (with its colour) for the rest.
        if (turns.State.Value == TurnState.GameOver)
            turnText.text = "PARTIDA TERMINADA";
        else
            turnText.text = mine ? "<color=#FFD040>¡TU TURNO!</color>" : dot + "TURNO DE " + who.ToUpper();

        // The banner glows with the colour of whoever is playing (pulses on your turn).
        Color panel = UIFactory.PanelColor;

        if (turns.State.Value != TurnState.GameOver && current != null)
        {
            float glow = mine ? 0.35f + Mathf.Sin(Time.unscaledTime * 5f) * 0.12f : 0.22f;
            panel = Color.Lerp(UIFactory.PanelColor, whoColor * 0.6f, glow);
            panel.a = UIFactory.PanelColor.a;
        }

        turnBanner.color = panel;

        bool eliminated = local != null && local.State.Value == PlayerLifeState.Eliminated && turns.State.Value != TurnState.GameOver;
        localTurnText.text = eliminated ? "<color=#FF5050>ESTÁS ELIMINADO · solo miras</color>" : "";

        string hint;
        CombatManager combat = CombatManager.Instance;

        switch (turns.State.Value)
        {
            case TurnState.WaitingForRoll:
                if (turns.DiceRolling.Value) hint = "Tirando el dado...";
                else hint = mine ? "Tira el dado" : $"{who} va a tirar";
                break;
            case TurnState.Moving:
                hint = mine ? $"Muévete: {Steps(turns.MovesRemaining.Value)}" : $"{who} se mueve";
                if (turns.WallWalk.Value) hint += " · <color=#D080FF>atraviesa paredes</color>";
                break;
            case TurnState.CheckingTile: hint = "..."; break;
            case TurnState.Combat:
                if (combat != null && combat.IsDuel.Value)
                {
                    PlayerNetworkData defender = PlayerNetworkData.GetByPlayerId(combat.DefenderPlayerId.Value);
                    string defenderName = defender != null ? defender.CharacterLabel : "?";

                    if (combat.LocalSide == CombatManager.Side.Attacker) hint = $"<color=#FF8060>¡Duelo contra {defenderName}!</color>";
                    else if (combat.LocalSide == CombatManager.Side.Defender) hint = $"<color=#FF8060>¡{who} te reta!</color>";
                    else hint = $"Duelo: {who} vs {defenderName}";
                }
                else
                {
                    hint = mine ? "<color=#D080FF>¡Amenaza!</color>" : $"{who} combate";
                }
                break;
            case TurnState.TurnEnd: hint = ""; break;
            case TurnState.GameOver: hint = ""; break;
            case TurnState.Roulette: hint = mine ? "¡Gira la ruleta!" : $"{who} gira la ruleta"; break;
            case TurnState.Mission: hint = mine ? "¡Tu misión!" : $"{who} juega su misión"; break;
            default: hint = ""; break;
        }

        float secondsLeft = turns.SecondsLeft;

        if (secondsLeft >= 0f && !string.IsNullOrEmpty(hint))
            hint += $"   <color=#FF8080>{Mathf.CeilToInt(secondsLeft)}s</color>";

        hintText.text = eliminated ? "" : hint;
    }

    private static string Steps(int n) => n == 1 ? "1 paso" : n + " pasos";

    private static Color CharacterColor(PlayerNetworkData p)
    {
        CharacterData character = p != null ? p.Character : null;
        Color c = character != null && character.boardColor.a > 0f ? character.boardColor : UIFactory.AccentColor;
        return new Color(c.r, c.g, c.b, 1f);
    }

    private void RefreshPlayers(TurnManager turns)
    {
        StringBuilder sb = new StringBuilder();
        List<PlayerNetworkData> players = new List<PlayerNetworkData>(PlayerNetworkData.All);
        players.Sort((a, b) => a.PlayerId.Value.CompareTo(b.PlayerId.Value));

        foreach (PlayerNetworkData p in players)
        {
            bool isCurrent = p.PlayerId.Value == turns.CurrentPlayerId.Value && turns.State.Value != TurnState.GameOver;
            string color = ColorUtility.ToHtmlStringRGB(CharacterColor(p));

            // ► marks the turn; ● is the character colour (same as the pawn and its turn ring).
            sb.Append(isCurrent ? "<color=#FFD040>►</color> " : "   ");
            sb.Append($"<color=#{color}>●</color> <b>{CharacterName(p)}</b>");

            if (p.IsOwner)
                sb.Append(" <size=80%><color=#FFD040>(tú)</color></size>");

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

            // Cards as a count, the mission as a single gold word when done.
            sb.Append($"   <color=#9FB8FF>■ {p.HandCount.Value}</color>");

            if (p.MissionCompleted.Value)
                sb.Append("   <color=#FFD040>MISIÓN</color>");

            string status = p.IsAlive ? p.StatusSummary() : "";
            sb.Append(string.IsNullOrEmpty(status) ? "\n\n" : $"\n     <size=80%><color=#D080FF>{status}</color></size>\n\n");
        }

        playersText.text = sb.ToString();
    }

    private void RefreshLocal(TurnManager turns, PlayerNetworkData local)
    {
        livesText.text = local != null ? $"<color=#FF4060>{Hearts(local.Lives.Value)}</color>" : "";

        bool canRoll = turns.CanLocalPlayerRoll();
        bool canMove = turns.CanLocalPlayerMove();
        SetActionButtons(canRoll, canMove);

        // Steps left in big letters while moving; nothing otherwise.
        if (movesLabel != null)
        {
            movesLabel.text = canMove ? Steps(turns.MovesRemaining.Value).ToUpper() : "";
            movesLabel.color = CharacterColor(local);
        }

        foreach (Button b in moveButtons)
            b.gameObject.SetActive(canMove);

        if (turnCardButton != null)
        {
            // Only offered when the player holds a card that can be played now.
            turnCardButton.gameObject.SetActive(canRoll && HasTurnCard(local));
            turnCardButton.interactable = turns.CanLocalPlayTurnCard(selectedHandIndex);
        }
    }

    private static bool HasTurnCard(PlayerNetworkData local)
    {
        if (local == null || local.CardsBlocked)
            return false;

        foreach (int id in local.Hand)
        {
            if (CardRules.CanPlayOnOwnTurn(CardDatabase.Instance.GetById(id)))
                return true;
        }

        return false;
    }

    /// <summary>Breathing scale on a button that is waiting for the player (every frame).</summary>
    private void PulseButtons()
    {
        float pulse = 1f + Mathf.Sin(Time.unscaledTime * 6f) * 0.05f;

        if (rollTextButton != null)
            rollTextButton.transform.localScale = rollTextButton.interactable ? Vector3.one * pulse : Vector3.one;

        if (rollButton != null)
            rollButton.transform.localScale = rollButton.interactable ? Vector3.one * pulse : Vector3.one;

        if (combatRollButton != null)
            combatRollButton.transform.localScale = combatRollButton.interactable && combatRollButton.gameObject.activeInHierarchy ? Vector3.one * pulse : Vector3.one;
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

        // The hand rises on your turn and whenever you are fighting (cards may be played then).
        TurnManager turnManager = TurnManager.Instance;
        bool myTurn = turnManager != null && turnManager.IsLocalPlayersTurn && turnManager.State.Value != TurnState.GameOver;
        bool fighting = CombatManager.Instance != null && CombatManager.Instance.IsSpawned && CombatManager.Instance.IsActive && CombatManager.Instance.IsLocalFighter;
        handRaised = myTurn || fighting;
        handTitle.gameObject.SetActive(handRaised || selectedHandIndex >= 0);

        if (canUse)
            handTitle.text = "<color=#7CFF8A>▲ Toca una carta y USAR</color>";
        else if (canTurnCard)
            handTitle.text = "<color=#C9A0FF>▲ USAR CARTA ESPECIAL</color>";
        else if (selectedHandIndex >= 0 && selectedHandIndex < local.Hand.Count)
            handTitle.text = CardRules.TimingHint(CardDatabase.Instance.GetById(local.Hand[selectedHandIndex]));
        else
            handTitle.text = "MIS CARTAS";

        // No yellow frame: the open card is shown by growing and rising (HandCard).
        for (int i = 0; i < handCardGlows.Count; i++)
            handCardGlows[i].SetActive(false);
    }

    private void RebuildHand(PlayerNetworkData local)
    {
        foreach (Transform child in handContainer)
            Destroy(child.gameObject);

        shownHand.Clear();
        handCardGlows.Clear();
        handCards.Clear();
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

        // Root: invisible hit area + button. Pivot at the bottom so the fan tilts around it.
        Image cardRoot = UIFactory.CreatePanel(handContainer, "Card_" + cardId, new Color(0f, 0f, 0f, 0f));
        UIFactory.Place(cardRoot.rectTransform, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(HandCardWidth, HandCardHeight));
        cardRoot.rectTransform.pivot = new Vector2(0.5f, 0f);

        // Click = open the card (it becomes the selected card: bigger, raised) / close it again.
        Button button = cardRoot.gameObject.AddComponent<Button>();
        button.targetGraphic = cardRoot;
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => selectedHandIndex = selectedHandIndex == index ? -1 : index);

        // Face: the card art (always face-up).
        RectTransform root = UIFactory.CreateRect(cardRoot.transform, "Face");
        UIFactory.Stretch(root);

        HandCard handCard = cardRoot.gameObject.AddComponent<HandCard>();
        handCards.Add(handCard);

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
        PlayerNetworkData fighter = PlayerNetworkData.GetByPlayerId(combat.CombatPlayerId.Value);
        string who = fighter != null ? fighter.CharacterLabel : "?";
        bool isLocal = combat.IsLocalFighter;

        combatTitle.text = isLocal ? "¡AMENAZA!" : $"¡AMENAZA PARA {who.ToUpper()}!";

        // The target in big: "Saca 4+" (dice + bonus).
        StringBuilder body = new StringBuilder();
        body.Append(isLocal ? "Saca " : $"{who} necesita ");
        body.Append($"<size=190%><b><color=#FFD040>{combat.Required.Value}+</color></b></size>");

        if (combat.LifeProtected.Value)
            body.Append(isLocal
                ? "\n<color=#80C0FF>Estás protegido: no perderás vida si fallas</color>"
                : "\n<color=#80C0FF>Protegido: no perderá vida si falla</color>");

        AppendLastCard(body, combat);

        CombatPhase phase = combat.Phase.Value;

        if ((phase == CombatPhase.Won || phase == CombatPhase.Lost || phase == CombatPhase.Resolving) && combat.Roll.Value > 0)
            body.Append($"\n<size=140%>{RollText(combat.Roll.Value, combat.RollModifier.Value)}</size>");
        else if (combat.RollModifier.Value != 0)
            body.Append($"\n<color=#7CFF8A>Bonus {Signed(combat.RollModifier.Value)}</color>");

        combatBody.text = body.ToString();

        string damage = combat.LifeLoss.Value > 0 ? $"(-{combat.LifeLoss.Value} {Heart})" : "(sin daño)";

        switch (phase)
        {
            case CombatPhase.Escaped:
                combatResult.text = isLocal
                    ? "<color=#80C0FF>¡ESCAPASTE DE LA ENTIDAD!</color>"
                    : $"<color=#80C0FF>¡{who.ToUpper()} ESCAPÓ DE LA ENTIDAD!</color>";
                break;
            case CombatPhase.Won:
                combatResult.text = isLocal
                    ? "<color=#60FF60>¡SUPERASTE EL EVENTO!</color>"
                    : $"<color=#60FF60>¡{who.ToUpper()} SUPERÓ EL EVENTO!</color>";
                break;
            case CombatPhase.Lost:
                combatResult.text = (isLocal
                    ? "<color=#FF5050>FALLASTE EL EVENTO</color>"
                    : $"<color=#FF5050>{who.ToUpper()} FALLÓ EL EVENTO</color>") + $" <size=70%>{damage}</size>";
                break;
            case CombatPhase.CardPhase:
                combatResult.text = isLocal ? "<size=70%>¿Usar una carta?</size>" : $"<size=70%>{who} puede usar una carta...</size>";
                break;
            case CombatPhase.Rolling:
                combatResult.text = isLocal ? "<size=70%>¡Tira el dado!</size>" : $"<size=70%>Esperando la tirada de {who}...</size>";
                break;
            default:
                combatResult.text = "";
                break;
        }
    }

    private void RefreshDuel(CombatManager combat)
    {
        PlayerNetworkData attacker = PlayerNetworkData.GetByPlayerId(combat.CombatPlayerId.Value);
        PlayerNetworkData defender = PlayerNetworkData.GetByPlayerId(combat.DefenderPlayerId.Value);
        CombatManager.Side side = combat.LocalSide;

        string attackerName = attacker != null ? attacker.CharacterLabel : "?";
        string defenderName = defender != null ? defender.CharacterLabel : "?";
        string rivalName = side == CombatManager.Side.Attacker ? defenderName : attackerName;

        combatTitle.text = side != CombatManager.Side.None ? $"¡DUELO CONTRA {rivalName.ToUpper()}!" : "¡DUELO!";

        StringBuilder body = new StringBuilder();

        if (side != CombatManager.Side.None)
            body.Append("<color=#FFD040>Carta (opcional) y tira el dado</color>\n");
        else
            body.Append("<size=85%>Gana el mayor · el perdedor pierde 1 ♥</size>\n");

        body.Append(DuelLine(attacker, combat.Roll.Value, combat.RollModifier.Value, combat.LifeProtected.Value));
        body.Append("\n<size=90%><color=#FF8060>VS</color></size>\n");
        body.Append(DuelLine(defender, combat.DefenderRoll.Value, combat.DefenderModifier.Value, combat.DefenderProtected.Value));
        AppendLastCard(body, combat);
        combatBody.text = body.ToString();

        switch (combat.Phase.Value)
        {
            case CombatPhase.Won:
                combatResult.text = DuelResult(side, CombatManager.Side.Attacker, attackerName, defender, combat.DefenderLifeLoss.Value);
                break;
            case CombatPhase.Lost:
                combatResult.text = DuelResult(side, CombatManager.Side.Defender, defenderName, attacker, combat.LifeLoss.Value);
                break;
            case CombatPhase.Escaped:
                combatResult.text = "<color=#80C0FF>¡DUELO INTERRUMPIDO!</color> <size=60%>nadie pierde vida</size>";
                break;
            case CombatPhase.Draw:
                combatResult.text = "<color=#FFD040>¡EMPATE!</color> <size=60%>nadie pierde vida</size>";
                break;
            default:
                if (side != CombatManager.Side.None && combat.HasRolled(side))
                    combatResult.text = $"<size=70%>Esperando la tirada de {rivalName}...</size>";
                else if (side == CombatManager.Side.None)
                    combatResult.text = "<size=70%>Esperando las tiradas...</size>";
                else
                    combatResult.text = "";
                break;
        }
    }

    /// <summary>"¡GANASTE EL DUELO!" / "PERDISTE EL DUELO" for the duelists, "¡GANA X!" for the spectators.</summary>
    private static string DuelResult(CombatManager.Side localSide, CombatManager.Side winnerSide, string winnerName, PlayerNetworkData loser, int lifeLoss)
    {
        string loss = lifeLoss > 0 ? $"-{lifeLoss} {Heart}" : "sin daño";

        if (localSide == winnerSide)
            return $"<color=#60FF60>¡GANASTE EL DUELO!</color> <size=60%>{(loser != null ? loser.CharacterLabel : "?")} {loss}</size>";

        if (localSide != CombatManager.Side.None)
            return $"<color=#FF5050>PERDISTE EL DUELO</color> <size=60%>{(lifeLoss > 0 ? $"pierdes {lifeLoss} {Heart}" : "sin daño")}</size>";

        return $"<color=#60FF60>¡GANA {winnerName.ToUpper()}!</color> <size=60%>{(loser != null ? loser.CharacterLabel : "?")} {loss}</size>";
    }

    private static string DuelLine(PlayerNetworkData p, int roll, int modifier, bool protectedFromDamage)
    {
        string name = p == null ? "?" : p.IsOwner ? "Tú" : p.CharacterLabel;
        string rollText = roll > 0 ? $"<size=150%>{RollText(roll, modifier)}</size>" : (modifier != 0 ? $"? <color=#7CFF8A>({Signed(modifier)})</color>" : "?");
        string shield = protectedFromDamage ? " <color=#80C0FF>(escudo)</color>" : "";
        return $"<color=#{ColorUtility.ToHtmlStringRGB(CharacterColor(p))}>●</color> <b>{name}</b>  {rollText}{shield}";
    }

    private static string RollText(int roll, int modifier)
    {
        return modifier == 0 ? $"<b>{roll}</b>" : $"{roll} {Signed(modifier)} = <b>{roll + modifier}</b>";
    }

    private static string Signed(int value)
    {
        return value >= 0 ? "+ " + value : "- " + (-value);
    }

    private static void AppendLastCard(StringBuilder body, CombatManager combat)
    {
        string lastCard = combat.LocalLastCardText();

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
        bool localWon = winner != null && local != null && local.PlayerId.Value == winnerId;

        StringBuilder sb = new StringBuilder();

        if (localWon)
        {
            sb.Append("<color=#FFD040><b>¡HAS GANADO!</b></color>");
            sb.Append(winner.MissionCompleted.Value
                ? "\n<size=80%>¡completaste tu misión!</size>"
                : "\n<size=80%>eres el último jugador en pie</size>");
        }
        else if (winner != null)
        {
            sb.Append($"Ganador: <b>{winner.CharacterLabel}</b> ({winner.Name})");
            sb.Append(winner.MissionCompleted.Value
                ? "\n<size=80%>¡completó su misión!</size>"
                : "\n<size=80%>último jugador en pie</size>");

            if (local != null)
                sb.Append("\n\n<color=#FF8080>Has perdido</color>");
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
        roulette.Show(spinner != null ? spinner.CharacterLabel : "?", isLocalSpinner, manager.CanLocalSpin(), manager.AutoSpinSecondsLeft);

        // New spin from the Host: animate to the same result on every instance.
        if (manager.SpinId.Value != shownSpinId && manager.Phase.Value != RoulettePhase.WaitingSpin)
        {
            shownSpinId = manager.SpinId.Value;

            if (manager.Phase.Value == RoulettePhase.Spinning)
                roulette.PlaySpin(manager.ResultIndex.Value, RouletteConfig.Instance.spinSeconds);
        }

        if (manager.Phase.Value == RoulettePhase.ShowingResult && manager.Result != null)
            roulette.ShowResult(isLocalSpinner || spinner == null ? manager.Result.resultText : manager.Result.OthersText(spinner.CharacterLabel));
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
            missionMinigame.Begin(definition, player != null ? player.CharacterLabel : "?", player != null ? CharacterName(player) : "?", manager.IsLocalMission);
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
