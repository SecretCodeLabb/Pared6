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

    private CombatVsView combatView;
    private GameObject menuOverlay;
    private Button exitButton;
    private GameObject playersPanel;

    // Own character card (bottom-left) with the lives left under it.
    private RectTransform characterCard;
    private Image characterPortrait;
    private TextMeshProUGUI characterName;
    private TextMeshProUGUI characterAbility;
    private TextMeshProUGUI characterLives;
    private CanvasGroup characterGroup;

    // Character ability button, right of the character card (between it and the hand fan).
    private Button abilityButton;
    private TextMeshProUGUI abilityLabel;
    private CanvasGroup abilityGroup;
    private int shownCharacterId = -2;
    private int shownLives = -1;
    private float livesPunchAt = -10f;
    private float exitConfirmUntil;

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
        AnimateCharacterCard();

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
        BuildCharacterCard(root);
        BuildHandPanel(root);
        combatView = CombatVsView.Create(root, OnUseCardClicked, OnSkipCardClicked, OnCombatRollClicked);
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
        BuildGameMenu(overlays);
        BuildEndPanel(overlays);

        // Board effects (steps, turn marker, fights, lives, missions). Local and purely visual.
        if (GetComponent<GameFx>() == null)
            gameObject.AddComponent<GameFx>();

        // Touch movement: tap the glowing cells (keyboard arrows still work on PC).
        if (GetComponent<TapMoveController>() == null)
            gameObject.AddComponent<TapMoveController>();
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
        // Exit lives in a menu (the game keeps running) so nobody leaves by accident.
        Button menuButton = UIFactory.CreateButton(root, "MenuButton", "MENÚ", () => ShowGameMenu(true), new Color(0.18f, 0.18f, 0.24f, 0.95f), 18f);
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
        // Only texts (no background) so the board stays visible; dark outlines keep them readable.
        RectTransform panel = UIFactory.CreateRect(root, "PlayersPanel");
        UIFactory.Place(panel, new Vector2(1f, 1f), new Vector2(-12f, -12f), new Vector2(330f, 380f));
        playersPanel = panel.gameObject;

        TextMeshProUGUI title = UIFactory.CreateText(panel, "Title", "JUGADORES", 22f, TextAlignmentOptions.TopRight);
        UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(310f, 30f));
        title.color = UIFactory.AccentColor;
        Outline(title);

        playersText = UIFactory.CreateText(panel, "PlayersText", "", 18f, TextAlignmentOptions.TopRight);
        UIFactory.Place(playersText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(310f, 280f));
        playersText.richText = true;
        AutoSize(playersText, 11f);
        Outline(playersText);

        objectiveText = UIFactory.CreateText(panel, "Objective", "", 15f, TextAlignmentOptions.BottomRight);
        UIFactory.Place(objectiveText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(310f, 54f));
        objectiveText.color = UIFactory.AccentColor;
        AutoSize(objectiveText, 11f);
        Outline(objectiveText);
    }

    private void BuildActionsPanel(Transform root)
    {
        // Bottom-left container without background: only the arrows while moving.
        RectTransform panel = UIFactory.CreateRect(root, "ActionsPanel");
        UIFactory.Place(panel, new Vector2(0f, 0f), new Vector2(12f, 12f), new Vector2(300f, 414f));

        // Lives are already in the players list.
        livesText = UIFactory.CreateText(panel, "LivesText", "", 26f);
        livesText.gameObject.SetActive(false);

        // TIRAR DADO: big, in the middle of the screen and only on your turn.
        rollTextButton = UIFactory.CreateButton(root, "RollDiceButton", "TIRAR DADO", OnRollClicked, new Color(0.85f, 0.62f, 0.12f), 40f);
        UIFactory.Place((RectTransform)rollTextButton.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(340f, 110f));
        ((RectTransform)rollTextButton.transform).pivot = new Vector2(0.5f, 0.5f);
        UseRounded(rollTextButton.GetComponent<Image>());

        // Special cards played on the own turn (Silencio líquido, Cívica, Comida Frita), under the dice button.
        turnCardButton = UIFactory.CreateButton(root, "TurnCardButton", "USAR CARTA ESPECIAL", OnTurnCardClicked, new Color(0.45f, 0.18f, 0.70f), 20f);
        UIFactory.Place((RectTransform)turnCardButton.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -80f), new Vector2(300f, 56f));
        ((RectTransform)turnCardButton.transform).pivot = new Vector2(0.5f, 0.5f);
        UseRounded(turnCardButton.GetComponent<Image>());

        // Steps left while moving ("3 PASOS"); the arrows only show when they can be used.
        movesLabel = UIFactory.CreateText(panel, "MoveLabel", "", 26f);
        UIFactory.Place(movesLabel.rectTransform, new Vector2(0f, 0f), new Vector2(CharacterCardWidth + 14f, 40f), new Vector2(150f, 34f));
        movesLabel.alignment = TextAlignmentOptions.Left;
        movesLabel.fontStyle = FontStyles.Bold;
        Outline(movesLabel);

        // Big touch-friendly pad.
        AddMoveButton(panel, "↑", Vector2Int.up, new Vector2(0f, -190f));
        AddMoveButton(panel, "←", Vector2Int.left, new Vector2(-94f, -264f));
        AddMoveButton(panel, "→", Vector2Int.right, new Vector2(94f, -264f));
        AddMoveButton(panel, "↓", Vector2Int.down, new Vector2(0f, -338f));
    }

    private const float CharacterCardWidth = 200f;

    /// <summary>The local player's character card, always visible bottom-left, lives counted under it.</summary>
    private void BuildCharacterCard(Transform root)
    {
        RectTransform holder = UIFactory.CreateRect(root, "MyCharacter");
        UIFactory.Place(holder, new Vector2(0f, 0f), new Vector2(14f, 10f), new Vector2(CharacterCardWidth, CharacterCardWidth / CardLayout.Aspect + 40f));
        characterGroup = holder.gameObject.AddComponent<CanvasGroup>();
        characterGroup.blocksRaycasts = false;

        characterCard = UIFactory.CreateRect(holder, "Card");
        UIFactory.Place(characterCard, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(CharacterCardWidth, CharacterCardWidth / CardLayout.Aspect));
        characterCard.pivot = new Vector2(0.5f, 0.5f);
        characterCard.anchoredPosition = new Vector2(0f, -CharacterCardWidth / CardLayout.Aspect * 0.5f);

        CardLayout.AddImage(characterCard, "Window", CardLayout.Window, null, CardLayout.WindowFill, false);
        characterPortrait = CardLayout.AddImage(characterCard, "Portrait", CardLayout.Picture, null, Color.white, true);

        Sprite frame = Resources.Load<Sprite>("CardsDesign/CartaPersonaje");

        if (frame != null)
            CardLayout.AddImage(characterCard, "Frame", Rect.MinMaxRect(0f, 0f, 1f, 1f), frame, Color.white, false);

        characterName = UIFactory.CreateText(characterCard, "Name", "", 18f);
        CardLayout.Fit(characterName.rectTransform, CardLayout.Banner);
        CardLayout.StyleText(characterName, 18f, Color.white, true);
        characterName.alignment = TextAlignmentOptions.Center;
        characterName.outlineWidth = 0.2f;
        characterName.outlineColor = Color.black;

        characterAbility = UIFactory.CreateText(characterCard, "Ability", "", 16f);
        CardLayout.Fit(characterAbility.rectTransform, CardLayout.Body);
        CardLayout.StyleText(characterAbility, 16f, new Color(0.95f, 0.95f, 0.95f));
        characterAbility.alignment = TextAlignmentOptions.Center;

        characterLives = UIFactory.CreateText(holder, "Lives", "", 24f);
        UIFactory.Place(characterLives.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 2f), new Vector2(CharacterCardWidth + 40f, 34f));
        characterLives.fontStyle = FontStyles.Bold;
        Outline(characterLives);

        BuildAbilityButton(root);
    }

    private const float AbilityButtonWidth = 150f;
    private const float AbilityButtonHeight = 132f;

    /// <summary>
    /// Ability button beside the character card: the free strip between the card (left) and the hand
    /// fan (centre), above the steps counter, so it never covers the hand or the dice button.
    /// </summary>
    private void BuildAbilityButton(Transform root)
    {
        abilityButton = UIFactory.CreateButton(root, "AbilityButton", "", OnAbilityClicked, UIFactory.AccentColor, 20f);
        RectTransform rect = (RectTransform)abilityButton.transform;
        UIFactory.Place(rect, new Vector2(0f, 0f), new Vector2(14f + CharacterCardWidth + 14f, 84f), new Vector2(AbilityButtonWidth, AbilityButtonHeight));
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition += new Vector2(AbilityButtonWidth * 0.5f, AbilityButtonHeight * 0.5f);
        UseRounded(abilityButton.GetComponent<Image>());

        abilityLabel = abilityButton.GetComponentInChildren<TextMeshProUGUI>();
        abilityLabel.lineSpacing = -12f;
        abilityLabel.enableWordWrapping = false;
        Outline(abilityLabel);

        abilityGroup = abilityButton.gameObject.AddComponent<CanvasGroup>();
        abilityButton.gameObject.SetActive(false);
    }

    private void RefreshAbilityButton(TurnManager turns, PlayerNetworkData local)
    {
        if (abilityButton == null)
            return;

        CharacterData character = local != null ? local.Character : null;
        bool visible = character != null && CharacterAbilityRules.UsesTurnButton(character.ability) && local.IsAlive && turns.State.Value != TurnState.GameOver;
        abilityButton.gameObject.SetActive(visible);

        if (!visible)
            return;

        bool canUse = turns.CanLocalUseAbility();
        abilityButton.interactable = canUse;

        // Same as the character card: steps aside while an opened hand card sits on this corner in a fight.
        bool hidden = OpenCardOnSide && selectedHandIndex >= 0;
        abilityGroup.alpha = hidden ? 0f : canUse ? 1f : 0.55f;
        abilityGroup.blocksRaycasts = !hidden;

        Color color = CharacterColor(local);
        abilityButton.GetComponent<Image>().color = canUse ? color : Color.Lerp(color, new Color(0.15f, 0.15f, 0.18f), 0.6f);

        string state;

        if (canUse) state = $"{Mathf.RoundToInt(character.abilityFailChance * 100f)}% falla";
        else if (turns.IsLocalPlayersTurn && turns.AbilityUsed.Value) state = "usada";
        else state = "al moverte";

        abilityLabel.text = $"<size=150%><b>+{character.abilityAmount}</b></size>\nHABILIDAD\n<size=70%>{state}</size>";
    }

    private void OnAbilityClicked()
    {
        if (TurnManager.Instance != null)
            TurnManager.Instance.RequestUseAbilityFromLocal();
    }

    private void RefreshCharacterCard(PlayerNetworkData local)
    {
        if (characterCard == null)
            return;

        CharacterData character = local != null ? local.Character : null;
        characterGroup.gameObject.SetActive(character != null);

        if (character == null)
            return;

        // In a fight the opened hand card sits on this corner: the character card steps aside meanwhile.
        characterGroup.alpha = OpenCardOnSide && selectedHandIndex >= 0 ? 0f : 1f;

        if (local.CharacterId.Value != shownCharacterId)
        {
            shownCharacterId = local.CharacterId.Value;
            characterPortrait.sprite = character.characterImage;
            characterPortrait.color = character.characterImage != null ? Color.white : new Color(0f, 0f, 0f, 0f);
            characterName.text = character.characterName;
            characterAbility.text = string.IsNullOrEmpty(character.abilityName) ? "" : string.IsNullOrEmpty(character.abilityDescription)
                ? $"<b>{character.abilityName}</b>"
                : $"<b>{character.abilityName}</b>\n<size=80%>{character.abilityDescription}</size>";
        }

        int lives = local.Lives.Value;

        if (lives != shownLives)
        {
            if (shownLives >= 0)
                livesPunchAt = Time.unscaledTime;

            shownLives = lives;
        }

        bool eliminated = local.State.Value == PlayerLifeState.Eliminated;
        characterLives.text = eliminated
            ? "<color=#FF5050>ELIMINADO</color>"
            : $"<color=#FF4060>{Hearts(lives)}</color>  <color=#FFFFFF>{lives} {(lives == 1 ? "vida" : "vidas")}</color>";
        characterPortrait.color = eliminated ? new Color(0.4f, 0.4f, 0.4f) : (characterPortrait.sprite != null ? Color.white : characterPortrait.color);
    }

    /// <summary>Punch on the card when the lives change (every frame).</summary>
    private void AnimateCharacterCard()
    {
        if (characterCard == null)
            return;

        float t = Time.unscaledTime - livesPunchAt;
        float punch = t < 0.4f ? Mathf.Sin(t / 0.4f * Mathf.PI) * 0.12f : 0f;
        characterCard.localScale = Vector3.one * (1f + punch);
        characterLives.rectTransform.localScale = Vector3.one * (1f + punch * 1.5f);
    }

    private void AddMoveButton(Transform parent, string label, Vector2Int direction, Vector2 position)
    {
        Button button = UIFactory.CreateButton(parent, "Move" + direction, label, () => OnMoveClicked(direction), UIFactory.ButtonColor, 34f);
        UIFactory.Place((RectTransform)button.transform, new Vector2(0.5f, 1f), position, new Vector2(90f, 68f));
        moveButtons.Add(button);
    }

    private const float HandCardWidth = 190f;
    private const float HandCardHeight = HandCardWidth / CardLayout.Aspect;
    private const float HandPanelHeight = HandCardHeight + 40f;

    // Fan of cards (card-game style): overlapping and tilted along an arc; the open one grows.
    private const float HandFanWidth = 640f;
    private const float HandRaisedY = -8f;
    private const float HandLoweredY = -122f;
    private const float HandCombatY = -106f;
    private const float HandOpenY = 24f;
    private const float HandOpenScale = 1.8f;

    // In a fight the open card goes to the left of the VS panel so it never covers its buttons.
    private static readonly Vector2 HandCombatOpenPosition = new Vector2(-575f, 40f);
    private const float HandCombatOpenScale = 1.25f;

    private bool handInCombat;

    /// <summary>
    /// The open card goes to the left side (not the middle) while the board needs to be seen and
    /// tapped: in a fight and while the local player is moving (the glowing cells stay free).
    /// </summary>
    private bool OpenCardOnSide => handInCombat || (TurnManager.Instance != null && TurnManager.Instance.CanLocalPlayerMove());

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
        float baseY = handInCombat ? HandCombatY : handRaised ? HandRaisedY : HandLoweredY;
        HandCard openCard = null;

        // The title rides on top of the fan (hidden in a fight: the VS panel explains what to do).
        handTitle.rectTransform.anchoredPosition = new Vector2(0f, baseY + HandCardHeight + 6f);

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
                if (OpenCardOnSide)
                    card.SetTarget(HandCombatOpenPosition, 0f, HandCombatOpenScale);
                else
                    card.SetTarget(new Vector2(0f, HandOpenY), 0f, HandOpenScale);

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

    /// <summary>
    /// Menu opened from the MENÚ button: the game keeps running (nothing is paused). Leaving needs
    /// a second tap, so nobody quits by accident.
    /// </summary>
    private void BuildGameMenu(Transform overlays)
    {
        Image dim = UIFactory.CreatePanel(overlays, "GameMenu", new Color(0f, 0f, 0f, 0.6f));
        UIFactory.Stretch(dim.rectTransform);
        menuOverlay = dim.gameObject;

        // Tapping outside the panel closes the menu.
        Button outside = dim.gameObject.AddComponent<Button>();
        outside.transition = Selectable.Transition.None;
        outside.onClick.AddListener(() => ShowGameMenu(false));

        Image panel = UIFactory.CreatePanel(dim.transform, "Panel", UIFactory.PanelColor);
        UseRounded(panel);
        UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(460f, 330f));
        panel.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        panel.gameObject.AddComponent<ScaleToFit>();

        TextMeshProUGUI title = UIFactory.CreateText(panel.transform, "Title", "MENÚ", 40f);
        UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(420f, 50f));
        title.color = UIFactory.AccentColor;
        title.fontStyle = FontStyles.Bold;

        TextMeshProUGUI note = UIFactory.CreateText(panel.transform, "Note", "La partida sigue en curso", 18f);
        UIFactory.Place(note.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(420f, 28f));
        note.color = new Color(0.8f, 0.8f, 0.8f);

        Button back = UIFactory.CreateButton(panel.transform, "Back", "VOLVER A LA PARTIDA", () => ShowGameMenu(false), UIFactory.OkColor, 22f);
        UIFactory.Place((RectTransform)back.transform, new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(380f, 70f));

        exitButton = UIFactory.CreateButton(panel.transform, "Exit", "SALIR DE LA PARTIDA", OnExitClicked, UIFactory.DangerColor, 22f);
        UIFactory.Place((RectTransform)exitButton.transform, new Vector2(0.5f, 1f), new Vector2(0f, -210f), new Vector2(380f, 70f));

        menuOverlay.SetActive(false);
    }

    private void ShowGameMenu(bool show)
    {
        if (menuOverlay == null)
            return;

        menuOverlay.SetActive(show);
        exitConfirmUntil = 0f;
        UIFactory.SetLabel(exitButton, "SALIR DE LA PARTIDA");
    }

    /// <summary>First tap asks for confirmation; a second tap within 3 s leaves the game.</summary>
    private void OnExitClicked()
    {
        if (Time.unscaledTime > exitConfirmUntil)
        {
            exitConfirmUntil = Time.unscaledTime + 3f;
            UIFactory.SetLabel(exitButton, "¿SEGURO? TOCA OTRA VEZ");
            return;
        }

        OnMenuClicked();
    }

    private static void Outline(TextMeshProUGUI text)
    {
        text.outlineWidth = 0.22f;
        text.outlineColor = new Color32(0, 0, 0, 230);
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
            rollTextButton.gameObject.SetActive(false);
            combatView.Refresh(null, false);
            handPanel.SetActive(false);
            RefreshToasts();
            return;
        }

        RefreshDice(turns);
        RefreshTurn(turns, local);
        RefreshPlayers(turns);
        RefreshLocal(turns, local);
        RefreshCombat(local);
        RefreshHand(local);
        RefreshCharacterCard(local);
        RefreshAbilityButton(turns, local);
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
                hint = mine ? $"Toca una casilla verde · {Steps(turns.MovesRemaining.Value)}" : $"{who} se mueve";
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

        // Movement is done by tapping the glowing cells on the board (TapMoveController).
        foreach (Button b in moveButtons)
            b.gameObject.SetActive(false);

        // The dice button only exists on your turn (the dice animation then takes the middle).
        rollTextButton.gameObject.SetActive(canRoll);

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

        if (abilityButton != null)
            abilityButton.transform.localScale = abilityButton.interactable ? Vector3.one * pulse : Vector3.one;

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
        handTitle.gameObject.SetActive(!handInCombat && (handRaised || selectedHandIndex >= 0));

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

        // Final art with its own name and text: just the image.
        if (card != null && card.fullArt != null)
        {
            CardLayout.AddImage(root.transform, "FullArt", Rect.MinMaxRect(0f, 0f, 1f, 1f), card.fullArt, Color.white, true);
            return glow.gameObject;
        }

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
        combatView.Refresh(combat, selectedHandIndex >= 0);

        bool visible = combatView.Visible;
        handInCombat = visible && combat.IsLocalFighter;

        // Nothing on top of the VS panel: the big event banner and the messages step aside.
        if (visible && EventUI.Instance != null && EventUI.Instance.eventPanel != null && EventUI.Instance.eventPanel.activeSelf)
            EventUI.Instance.eventPanel.SetActive(false);

        toastText.gameObject.SetActive(!visible);

        // The VS cards already show both fighters' lives; the list would touch the panel on phones.
        playersPanel.SetActive(!visible);
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
