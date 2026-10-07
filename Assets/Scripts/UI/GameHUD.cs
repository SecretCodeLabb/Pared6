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

    [Header("Existing scene UI (reused)")]
    [SerializeField] private BoardManager boardManager;
    [SerializeField] private Button rollButton;
    [SerializeField] private TextMeshProUGUI gameInfoText;

    // Built at runtime
    private TextMeshProUGUI turnText;
    private TextMeshProUGUI localTurnText;
    private TextMeshProUGUI hintText;
    private TextMeshProUGUI livesText;
    private TextMeshProUGUI playersText;
    private TextMeshProUGUI toastText;
    private TextMeshProUGUI networkText;
    private Button rollTextButton;
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

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        Transform root = canvasObject.transform;

        BuildTurnBanner(root);
        BuildPlayersPanel(root);
        BuildActionsPanel(root);
        BuildHandPanel(root);
        BuildCombatPanel(root);
        BuildEndPanel(root);

        toastText = UIFactory.CreateText(root, "Toasts", "", 26f);
        UIFactory.Place(toastText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(900f, 130f));
        toastText.color = UIFactory.AccentColor;
        toastText.outlineWidth = 0.2f;
        toastText.outlineColor = Color.black;

        networkText = UIFactory.CreateText(root, "NetworkInfo", "", 16f, TextAlignmentOptions.BottomRight);
        UIFactory.Place(networkText.rectTransform, new Vector2(1f, 0f), new Vector2(-12f, 8f), new Vector2(500f, 30f));
        networkText.color = new Color(1f, 1f, 1f, 0.6f);

        Button menuButton = UIFactory.CreateButton(root, "MenuButton", "SALIR AL MENÚ", OnMenuClicked, UIFactory.DangerColor, 18f);
        UIFactory.Place((RectTransform)menuButton.transform, new Vector2(1f, 1f), new Vector2(-12f, -12f), new Vector2(190f, 44f));
    }

    private void BuildTurnBanner(Transform root)
    {
        Image banner = UIFactory.CreatePanel(root, "TurnBanner", UIFactory.PanelColor);
        UIFactory.Place(banner.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(760f, 130f));

        turnText = UIFactory.CreateText(banner.transform, "TurnText", "", 36f);
        UIFactory.Place(turnText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(740f, 48f));

        localTurnText = UIFactory.CreateText(banner.transform, "LocalTurnText", "", 30f);
        UIFactory.Place(localTurnText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -52f), new Vector2(740f, 40f));

        hintText = UIFactory.CreateText(banner.transform, "HintText", "", 20f);
        UIFactory.Place(hintText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -94f), new Vector2(740f, 30f));
        hintText.color = new Color(0.85f, 0.85f, 0.85f);
    }

    private void BuildPlayersPanel(Transform root)
    {
        Image panel = UIFactory.CreatePanel(root, "PlayersPanel", UIFactory.PanelColor);
        UIFactory.Place(panel.rectTransform, new Vector2(1f, 1f), new Vector2(-12f, -66f), new Vector2(400f, 420f));

        TextMeshProUGUI title = UIFactory.CreateText(panel.transform, "Title", "JUGADORES", 26f);
        UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(380f, 34f));
        title.color = UIFactory.AccentColor;

        playersText = UIFactory.CreateText(panel.transform, "PlayersText", "", 21f, TextAlignmentOptions.TopLeft);
        UIFactory.Place(playersText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -48f), new Vector2(372f, 364f));
        playersText.richText = true;
    }

    private void BuildActionsPanel(Transform root)
    {
        Image panel = UIFactory.CreatePanel(root, "ActionsPanel", UIFactory.PanelColor);
        UIFactory.Place(panel.rectTransform, new Vector2(0f, 0f), new Vector2(12f, 12f), new Vector2(330f, 330f));

        livesText = UIFactory.CreateText(panel.transform, "LivesText", "", 30f);
        UIFactory.Place(livesText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(310f, 44f));

        rollTextButton = UIFactory.CreateButton(panel.transform, "RollDiceButton", "TIRAR DADO", OnRollClicked, UIFactory.ButtonColor, 28f);
        UIFactory.Place((RectTransform)rollTextButton.transform, new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(300f, 64f));

        TextMeshProUGUI moveLabel = UIFactory.CreateText(panel.transform, "MoveLabel", "MOVER (WASD / flechas)", 18f);
        UIFactory.Place(moveLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -132f), new Vector2(300f, 26f));

        AddMoveButton(panel.transform, "↑", Vector2Int.up, new Vector2(0f, -164f));
        AddMoveButton(panel.transform, "←", Vector2Int.left, new Vector2(-80f, -224f));
        AddMoveButton(panel.transform, "→", Vector2Int.right, new Vector2(80f, -224f));
        AddMoveButton(panel.transform, "↓", Vector2Int.down, new Vector2(0f, -264f));
    }

    private void AddMoveButton(Transform parent, string label, Vector2Int direction, Vector2 position)
    {
        Button button = UIFactory.CreateButton(parent, "Move" + direction, label, () => OnMoveClicked(direction), UIFactory.ButtonColor, 30f);
        UIFactory.Place((RectTransform)button.transform, new Vector2(0.5f, 1f), position, new Vector2(70f, 56f));
        moveButtons.Add(button);
    }

    private const float HandCardWidth = 172f;
    private const float HandCardHeight = HandCardWidth / CardLayout.Aspect;

    private void BuildHandPanel(Transform root)
    {
        Image panel = UIFactory.CreatePanel(root, "HandPanel", UIFactory.PanelColor);
        UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0f), new Vector2(60f, 12f), new Vector2(760f, HandCardHeight + 52f));
        handPanel = panel.gameObject;

        handTitle = UIFactory.CreateText(panel.transform, "Title", "MIS CARTAS", 22f);
        UIFactory.Place(handTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(740f, 30f));
        handTitle.color = UIFactory.AccentColor;

        RectTransform container = UIFactory.CreateRect(panel.transform, "Cards");
        UIFactory.Place(container, new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(740f, HandCardHeight + 4f));
        HorizontalLayoutGroup layout = container.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 16f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        handContainer = container;
    }

    private void BuildCombatPanel(Transform root)
    {
        Image panel = UIFactory.CreatePanel(root, "CombatPanel", new Color(0.12f, 0.05f, 0.05f, 0.94f));
        UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(760f, 430f));
        combatPanel = panel.gameObject;

        combatTitle = UIFactory.CreateText(panel.transform, "Title", "", 30f);
        UIFactory.Place(combatTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(730f, 44f));
        combatTitle.color = new Color(1f, 0.45f, 0.35f);

        combatBody = UIFactory.CreateText(panel.transform, "Body", "", 24f);
        UIFactory.Place(combatBody.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -58f), new Vector2(730f, 170f));

        combatResult = UIFactory.CreateText(panel.transform, "Result", "", 40f);
        UIFactory.Place(combatResult.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -214f), new Vector2(730f, 60f));

        useCardButton = UIFactory.CreateButton(panel.transform, "UseCardButton", "USAR CARTA", OnUseCardClicked, UIFactory.OkColor, 22f);
        UIFactory.Place((RectTransform)useCardButton.transform, new Vector2(0.5f, 0f), new Vector2(-245f, 24f), new Vector2(230f, 70f));

        skipCardButton = UIFactory.CreateButton(panel.transform, "SkipCardButton", "NO USAR CARTA", OnSkipCardClicked, UIFactory.ButtonColor, 22f);
        UIFactory.Place((RectTransform)skipCardButton.transform, new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(230f, 70f));

        combatRollButton = UIFactory.CreateButton(panel.transform, "CombatRollButton", "TIRAR DADO DE COMBATE", OnCombatRollClicked, UIFactory.DangerColor, 20f);
        UIFactory.Place((RectTransform)combatRollButton.transform, new Vector2(0.5f, 0f), new Vector2(245f, 24f), new Vector2(230f, 70f));
    }

    private void BuildEndPanel(Transform root)
    {
        Image overlay = UIFactory.CreatePanel(root, "EndOverlay", new Color(0f, 0f, 0f, 0.75f));
        UIFactory.Stretch(overlay.rectTransform);
        endPanel = overlay.gameObject;

        Image panel = UIFactory.CreatePanel(overlay.transform, "EndPanel", UIFactory.PanelColor);
        UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820f, 480f));

        endTitle = UIFactory.CreateText(panel.transform, "Title", "PARTIDA TERMINADA", 56f);
        UIFactory.Place(endTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(780f, 80f));
        endTitle.color = UIFactory.AccentColor;

        endBody = UIFactory.CreateText(panel.transform, "Body", "", 30f);
        UIFactory.Place(endBody.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(780f, 200f));

        restartButton = UIFactory.CreateButton(panel.transform, "RestartButton", "REINICIAR", OnRestartClicked, UIFactory.OkColor, 30f);
        UIFactory.Place((RectTransform)restartButton.transform, new Vector2(0.5f, 0f), new Vector2(-170f, 30f), new Vector2(300f, 80f));

        Button menuButton = UIFactory.CreateButton(panel.transform, "MenuButton", "VOLVER AL MENÚ", OnMenuClicked, UIFactory.ButtonColor, 28f);
        UIFactory.Place((RectTransform)menuButton.transform, new Vector2(0.5f, 0f), new Vector2(170f, 30f), new Vector2(300f, 80f));

        endPanel.SetActive(false);
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
        RefreshEnd(turns, game, local);
        RefreshToasts();

        NetworkManager nm = NetworkManager.Singleton;

        if (nm != null)
            networkText.text = (nm.IsHost ? "HOST" : "CLIENTE") + " · " + PlayerNetworkData.All.Count + " jugadores · Seed " + game.BoardSeed.Value;
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
            case TurnState.Moving: hint = turns.IsLocalPlayersTurn ? $"Muévete con WASD / flechas · {turns.MovesRemaining.Value} restantes" : $"Moviéndose · {turns.MovesRemaining.Value} restantes"; break;
            case TurnState.CheckingTile: hint = "Comprobando casilla..."; break;
            case TurnState.Combat: hint = CombatManager.Instance != null && CombatManager.Instance.IsDuel.Value ? "¡Duelo en curso!" : "¡Combate en curso!"; break;
            case TurnState.TurnEnd: hint = "Fin del turno"; break;
            case TurnState.GameOver: hint = "Partida terminada"; break;
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

            sb.Append($"   Eventos {p.Progress.Value}/{Rules.requiredEventsToWin}   Cartas {p.HandCount.Value}\n\n");
        }

        playersText.text = sb.ToString();
    }

    private void RefreshLocal(TurnManager turns, PlayerNetworkData local)
    {
        livesText.text = local != null ? $"<color=#FF4060>{Hearts(local.Lives.Value)}</color>" : "";

        bool canRoll = turns.CanLocalPlayerRoll();
        bool canMove = turns.CanLocalPlayerMove();
        SetActionButtons(canRoll, canMove);
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
        handTitle.text = canUse ? "MIS CARTAS — elige una y pulsa USAR CARTA" : "MIS CARTAS";

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

        if (phase == CombatPhase.Won || phase == CombatPhase.Lost || phase == CombatPhase.Resolving)
            body.Append($"\nTirada {RollText(combat.Roll.Value, combat.RollModifier.Value)} vs {combat.Required.Value}");
        else if (combat.RollModifier.Value != 0)
            body.Append($"\nBonus a la tirada: {Signed(combat.RollModifier.Value)}");

        combatBody.text = body.ToString();

        switch (phase)
        {
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

            if (local != null && local.PlayerId.Value == winnerId)
                sb.Append("\n\n<color=#FFD040>¡HAS COMPLETADO LA PARTIDA!</color>");
        }
        else
        {
            sb.Append("Sin ganador");
        }

        bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;

        if (!isHost)
            sb.Append("\n\n<size=70%>Esperando a que el Host reinicie...</size>");

        endBody.text = sb.ToString();
        restartButton.gameObject.SetActive(isHost);
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

    private void OnSkipCardClicked()
    {
        CombatManager.Instance.RequestSkipCardsFromLocal();
    }

    private void OnCombatRollClicked()
    {
        CombatManager.Instance.RequestRollFromLocal();
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
