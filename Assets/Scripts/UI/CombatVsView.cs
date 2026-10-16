using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Combat panel in the middle of the screen, "VS" style (built by GameHUD, purely visual):
///   left  = the local player's character card (or the attacker for spectators),
///   right = the entity / event card, or the rival's character card in a duel.
/// Under the cards: the card bonus played on each side; between them: each side's dice
/// (animated when it is rolled; the entity "rolls" its target number) and then the two results
/// and who wins. The losing card shakes, flashes red and shows the life it lost.
/// Buttons (only for the fighters): USAR CARTA (with the card opened in the hand), SIN CARTA, TIRAR DADO.
/// No maths is shown: only the final result of each side.
/// </summary>
public class CombatVsView : MonoBehaviour
{
    // Layout (700 x 460 panel, centred): title / hint on top, cards + dice row, card bonuses, buttons.
    private const float PanelWidth = 860f;
    private const float PanelHeight = 560f;
    private const float CardWidth = 230f;
    private const float CardHeight = CardWidth / CardLayout.Aspect;
    private const float CardX = 300f;
    private const float ResultX = 112f;
    private const float RowY = 4f;

    private static readonly Color WinColor = new Color(0.45f, 1f, 0.5f);
    private static readonly Color LoseColor = new Color(1f, 0.35f, 0.3f);
    private static readonly Color Gold = new Color(1f, 0.82f, 0.25f);

    private class Side
    {
        public RectTransform Card;
        public Image Portrait;
        public TextMeshProUGUI Name;
        public TextMeshProUGUI Body;
        public Image Flash;
        public TextMeshProUGUI Stamp;
        public TextMeshProUGUI Chips;
        public TextMeshProUGUI Result;
        public DiceRollView Dice;
        public TextMeshProUGUI Damage;

        public int ShownRoll;
        public float RevealAt = -1f;
        public float HitAt = -1f;
        public Vector2 Home;
    }

    private Image panel;
    private TextMeshProUGUI title;
    private TextMeshProUGUI hint;
    private TextMeshProUGUI vs;
    private TextMeshProUGUI outcome;
    private Side left;
    private Side right;
    private Button useCardButton;
    private Button skipButton;
    private Button rollButton;
    private Button escapeButton;
    private Action onUseCard;
    private Action onSkip;
    private Action onRoll;

    private Sprite frameSprite;
    private int sessionKey = int.MinValue;
    private CombatPhase shownOutcome = CombatPhase.None;

    public bool Visible => panel != null && panel.gameObject.activeSelf;

    public static CombatVsView Create(Transform parent, Action useCard, Action skip, Action roll)
    {
        RectTransform root = UIFactory.CreateRect(parent, "CombatVs");
        UIFactory.Place(root, new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(PanelWidth, PanelHeight));
        root.pivot = new Vector2(0.5f, 0.5f);
        CombatVsView view = root.gameObject.AddComponent<CombatVsView>();
        view.onUseCard = useCard;
        view.onSkip = skip;
        view.onRoll = roll;
        view.Build();
        return view;
    }

    // =========================================================
    // BUILD
    // =========================================================

    private void Build()
    {
        frameSprite = Resources.Load<Sprite>("CardsDesign/CartaPersonaje");

        panel = gameObject.AddComponent<Image>();
        panel.color = new Color(0.07f, 0.05f, 0.06f, 1f);
        FxConfig fx = FxConfig.Instance;

        if (fx != null && fx.roundedSquare != null)
        {
            panel.sprite = fx.roundedSquare;
            panel.type = Image.Type.Sliced;
        }

        title = Text("Title", 36f, new Vector2(0f, -10f), new Vector2(800f, 44f), new Vector2(0.5f, 1f));
        title.fontStyle = FontStyles.Bold;
        title.color = new Color(1f, 0.55f, 0.4f);

        hint = Text("Hint", 21f, new Vector2(0f, -54f), new Vector2(800f, 48f), new Vector2(0.5f, 1f));
        hint.enableAutoSizing = true;
        hint.fontSizeMin = 12f;
        hint.fontSizeMax = 21f;
        hint.color = new Color(0.92f, 0.88f, 0.75f);


        outcome = Text("Outcome", 40f, new Vector2(0f, RowY - 90f), new Vector2(340f, 52f), new Vector2(0.5f, 0.5f));
        outcome.fontStyle = FontStyles.Bold;
        outcome.outlineWidth = 0.25f;
        outcome.outlineColor = Color.black;
        outcome.enableAutoSizing = true;
        outcome.fontSizeMin = 18f;
        outcome.fontSizeMax = 40f;

        left = BuildSide("Left", -1f);
        right = BuildSide("Right", 1f);

        vs = Text("VS", 62f, new Vector2(0f, RowY + 85f), new Vector2(160f, 74f), new Vector2(0.5f, 0.5f));
        vs.text = "VS";
        vs.fontStyle = FontStyles.Bold | FontStyles.Italic;
        vs.color = new Color(1f, 0.45f, 0.3f);
        vs.outlineWidth = 0.25f;
        vs.outlineColor = Color.black;

        useCardButton = Button("UseCard", "USAR CARTA", UIFactory.OkColor, () => onUseCard?.Invoke());
        skipButton = Button("Skip", "SIN CARTA", UIFactory.ButtonColor, () => onSkip?.Invoke());
        rollButton = Button("Roll", "TIRAR DADO", UIFactory.DangerColor, () => onRoll?.Invoke());
        escapeButton = Button("Escape", "ESCAPAR", new Color(0.08f, 0.60f, 0.55f), () => CombatManager.Instance?.RequestEscapeFromLocal());

        panel.gameObject.SetActive(false);
    }

    private Side BuildSide(string name, float sign)
    {
        Side side = new Side();
        side.Home = new Vector2(sign * CardX, RowY);

        // Card: window, portrait, frame, name banner, body.
        RectTransform card = UIFactory.CreateRect(transform, name + "Card");
        UIFactory.Place(card, new Vector2(0.5f, 0.5f), side.Home, new Vector2(CardWidth, CardHeight));
        card.pivot = new Vector2(0.5f, 0.5f);
        side.Card = card;

        CardLayout.AddImage(card, "Window", CardLayout.Window, null, CardLayout.WindowFill, false);
        side.Portrait = CardLayout.AddImage(card, "Portrait", CardLayout.Picture, null, Color.white, true);

        if (frameSprite != null)
            CardLayout.AddImage(card, "Frame", Rect.MinMaxRect(0f, 0f, 1f, 1f), frameSprite, Color.white, false);

        side.Name = UIFactory.CreateText(card, "Name", "", 20f);
        CardLayout.Fit(side.Name.rectTransform, CardLayout.Banner);
        CardLayout.StyleText(side.Name, 20f, Color.white, true);
        side.Name.alignment = TextAlignmentOptions.Center;
        side.Name.outlineWidth = 0.2f;
        side.Name.outlineColor = Color.black;

        side.Body = UIFactory.CreateText(card, "Body", "", 14f);
        CardLayout.Fit(side.Body.rectTransform, CardLayout.Body);
        CardLayout.StyleText(side.Body, 22f, new Color(0.95f, 0.95f, 0.95f));
        side.Body.alignment = TextAlignmentOptions.Center;

        side.Flash = UIFactory.CreatePanel(card, "Flash", new Color(1f, 0.1f, 0.1f, 0f));
        UIFactory.Stretch(side.Flash.rectTransform);
        side.Flash.raycastTarget = false;

        side.Stamp = UIFactory.CreateText(card, "Stamp", "", 30f);
        UIFactory.Stretch(side.Stamp.rectTransform);
        side.Stamp.fontStyle = FontStyles.Bold;
        side.Stamp.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 18f);
        side.Stamp.outlineWidth = 0.3f;
        side.Stamp.outlineColor = Color.black;


        // Card bonuses under the card.
        side.Chips = Text(name + "Chips", 17f, side.Home - new Vector2(0f, CardHeight * 0.5f + 13f), new Vector2(CardWidth + 60f, 24f), new Vector2(0.5f, 0.5f));
        side.Chips.fontStyle = FontStyles.Bold;

        // Dice + result between the card and the VS.
        Vector2 resultPos = new Vector2(sign * ResultX, RowY);
        side.Result = Text(name + "Result", 84f, resultPos, new Vector2(130f, 104f), new Vector2(0.5f, 0.5f));
        side.Result.fontStyle = FontStyles.Bold;
        side.Result.outlineWidth = 0.25f;
        side.Result.outlineColor = Color.black;

        side.Dice = DiceRollView.Create(transform, 100f, 0.35f);
        ((RectTransform)side.Dice.transform).anchoredPosition = resultPos;

        side.Damage = Text(name + "Damage", 30f, side.Home, new Vector2(CardWidth + 40f, 40f), new Vector2(0.5f, 0.5f));
        side.Damage.fontStyle = FontStyles.Bold;
        side.Damage.outlineWidth = 0.3f;
        side.Damage.outlineColor = Color.black;
        side.Damage.color = LoseColor;

        return side;
    }

    private TextMeshProUGUI Text(string name, float size, Vector2 position, Vector2 box, Vector2 anchor)
    {
        TextMeshProUGUI text = UIFactory.CreateText(transform, name, "", size);
        UIFactory.Place(text.rectTransform, anchor, position, box);
        text.rectTransform.pivot = new Vector2(0.5f, anchor.y >= 1f ? 1f : 0.5f);
        return text;
    }

    private Button Button(string name, string label, Color color, UnityEngine.Events.UnityAction click)
    {
        Button button = UIFactory.CreateButton(transform, name, label, click, color, 24f);
        UIFactory.Place((RectTransform)button.transform, new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(190f, 62f));
        ((RectTransform)button.transform).pivot = new Vector2(0.5f, 0f);
        return button;
    }

    // =========================================================
    // REFRESH (every HUD refresh)
    // =========================================================

    /// <param name="cardSelected">A card of the hand is open (selected) and can be played.</param>
    public void Refresh(CombatManager combat, bool cardSelected)
    {
        bool active = combat != null && combat.IsSpawned && combat.IsActive;

        if (!active)
        {
            if (panel.gameObject.activeSelf)
                panel.gameObject.SetActive(false);

            sessionKey = int.MinValue;
            return;
        }

        panel.gameObject.SetActive(true);

        bool duel = combat.IsDuel.Value;
        PlayerNetworkData attacker = PlayerNetworkData.GetByPlayerId(combat.CombatPlayerId.Value);
        PlayerNetworkData defender = duel ? PlayerNetworkData.GetByPlayerId(combat.DefenderPlayerId.Value) : null;
        CombatManager.Side localSide = combat.LocalSide;

        // New fight: reset dice / effects.
        int key = combat.CombatPlayerId.Value * 1000 + (duel ? combat.DefenderPlayerId.Value + 1 : 0);

        if (key != sessionKey || (combat.Phase.Value == CombatPhase.CardPhase && shownOutcome != CombatPhase.None))
        {
            sessionKey = key;
            ResetSide(left);
            ResetSide(right);
            shownOutcome = CombatPhase.None;
        }

        // The local player is always on the left when fighting.
        bool flip = localSide == CombatManager.Side.Defender;
        PlayerNetworkData leftPlayer = flip ? defender : attacker;
        PlayerNetworkData rightPlayer = flip ? attacker : defender;

        int attackerRoll = combat.Roll.Value, defenderRoll = combat.DefenderRoll.Value;
        // Defense cards lower the rival's result.
        int attackerDefense = combat.RivalReduction.Value, defenderDefense = duel ? combat.DefenderRivalReduction.Value : 0;
        int attackerTotal = Mathf.Max(0, attackerRoll + combat.RollModifier.Value - defenderDefense);
        int defenderTotal = Mathf.Max(0, defenderRoll + combat.DefenderModifier.Value - attackerDefense);

        FillPlayer(left, leftPlayer);
        UpdateSideRoll(left, flip ? defenderRoll : attackerRoll, flip ? defenderTotal : attackerTotal, combat.DiceSeconds, CharacterColor(leftPlayer));
        SetChips(left, flip ? combat.DefenderModifier.Value : combat.RollModifier.Value, flip ? combat.DefenderProtected.Value : combat.LifeProtected.Value,
            flip ? attackerDefense : defenderDefense);

        if (duel)
        {
            FillPlayer(right, rightPlayer);
            UpdateSideRoll(right, flip ? attackerRoll : defenderRoll, flip ? attackerTotal : defenderTotal, combat.DiceSeconds, CharacterColor(rightPlayer));
            SetChips(right, flip ? combat.RollModifier.Value : combat.DefenderModifier.Value, flip ? combat.LifeProtected.Value : combat.DefenderProtected.Value,
                flip ? defenderDefense : attackerDefense);
        }
        else
        {
            // The entity "rolls" its target number together with the player.
            FillEntity(right, combat.Required.Value);
            UpdateSideRoll(right, attackerRoll > 0 ? Mathf.Max(1, combat.Required.Value) : 0, combat.Required.Value, combat.DiceSeconds, new Color(0.7f, 0.3f, 1f));
            SetChips(right, 0, false, attackerDefense);
        }

        // Titles.
        string leftName = Name(leftPlayer), rightName = duel ? Name(rightPlayer) : "la entidad";
        bool fighter = localSide != CombatManager.Side.None;
        title.text = duel ? "¡DUELO!" : combat.EntityCount.Value > 1 ? $"¡2 ENTIDADES! ({combat.EntityRound.Value}/{combat.EntityCount.Value})" : "¡AMENAZA!";
        hint.text = Hint(combat, fighter, localSide, leftName, rightName);

        string lastCard = combat.LocalLastCardText();

        if (!string.IsNullOrEmpty(lastCard))
            hint.text += $"\n<size=85%><color=#9CFFA8>{lastCard}</color></size>";

        UpdateOutcome(combat, flip, fighter, leftName, rightName);

        // Buttons (fighters only), centred in the row.
        bool canUse = combat.CanLocalUseCard();
        SetButton(useCardButton, fighter && canUse, cardSelected);
        SetButton(skipButton, fighter && combat.CanLocalSkipCards(), true);
        SetButton(rollButton, fighter && combat.CanLocalRoll(), true);
        SetButton(escapeButton, fighter && combat.CanLocalEscape(), true);

        if (escapeButton.gameObject.activeSelf)
            hint.text += $"\n<size=85%><color=#9CC8FF>{EscapeRisk()}</color></size>";
        LayoutButtons();
    }

    private string Hint(CombatManager combat, bool fighter, CombatManager.Side localSide, string leftName, string rightName)
    {
        CombatPhase phase = combat.Phase.Value;

        if (phase != CombatPhase.CardPhase && phase != CombatPhase.Rolling)
            return fighter ? "" : $"{leftName} contra {rightName}";

        if (!fighter)
            return $"{leftName} contra {rightName}";

        if (combat.HasRolled(localSide))
        {
            CombatManager.Side other = localSide == CombatManager.Side.Attacker ? CombatManager.Side.Defender : CombatManager.Side.Attacker;
            return combat.IsDuel.Value && !combat.HasRolled(other) ? $"Esperando la tirada de {rightName}..." : "";
        }

        return combat.CanLocalUseCard()
            ? "Abre una carta de tu mano y pulsa USAR CARTA (opcional) · luego TIRA EL DADO"
            : "¡Tira el dado!";
    }

    private void UpdateOutcome(CombatManager combat, bool flip, bool fighter, string leftName, string rightName)
    {
        CombatPhase phase = combat.Phase.Value;
        bool resolved = phase == CombatPhase.Won || phase == CombatPhase.Lost || phase == CombatPhase.Draw || phase == CombatPhase.Escaped;

        // Results are only shown once both dice are on screen.
        bool revealed = left.RevealAt >= 0f && Time.unscaledTime >= left.RevealAt &&
                        (right.RevealAt < 0f || Time.unscaledTime >= right.RevealAt);

        if (!resolved || (phase != CombatPhase.Escaped && !revealed))
        {
            outcome.text = "";
            return;
        }

        // Winner from the attacker's point of view, then mapped to left / right.
        bool attackerWon = phase == CombatPhase.Won;
        bool leftWon = flip ? !attackerWon : attackerWon;
        Side winner = leftWon ? left : right;
        Side loser = leftWon ? right : left;

        // Life lost by the loser: the defender's when the attacker won, the attacker's otherwise.
        int loss = attackerWon ? combat.DefenderLifeLoss.Value : combat.LifeLoss.Value;

        if (phase == CombatPhase.Escaped)
        {
            outcome.text = "<color=#80C8FF>¡ESCAPÓ!</color>";
        }
        else if (phase == CombatPhase.Draw)
        {
            outcome.text = "<color=#FFD040>EMPATE</color>";
        }
        else if (fighter)
        {
            outcome.text = leftWon ? $"<color=#73FF80>¡GANASTE!</color>" : $"<color=#FF5A4D>¡PERDISTE!</color>";
        }
        else
        {
            outcome.text = $"<color=#73FF80>¡GANA {(leftWon ? leftName : rightName).ToUpper()}!</color>";
        }

        if (shownOutcome == phase)
            return;

        shownOutcome = phase;

        if (phase == CombatPhase.Won || phase == CombatPhase.Lost)
        {
            bool entityLost = !combat.IsDuel.Value && attackerWon;
            Hit(loser, entityLost ? "VENCIDA" : null, entityLost ? "" : loss > 0 ? $"-{loss} ♥" : "sin daño");
            winner.Result.color = WinColor;
            loser.Result.color = LoseColor;
        }
    }

    // =========================================================
    // SIDES
    // =========================================================

    private void FillPlayer(Side side, PlayerNetworkData player)
    {
        CharacterData character = player != null ? player.Character : null;
        side.Portrait.sprite = character != null ? character.characterImage : null;
        side.Portrait.color = side.Portrait.sprite != null ? Color.white : new Color(0f, 0f, 0f, 0f);
        side.Name.text = player != null && player.IsOwner ? $"{Name(player)} <color=#FFD040>(tú)</color>" : Name(player);
        side.Body.text = player != null ? $"<color=#FF4060>{Hearts(player.Lives.Value)}</color>" : "";
    }

    private void FillEntity(Side side, int required)
    {
        FxConfig fx = FxConfig.Instance;
        side.Portrait.sprite = fx != null ? fx.entityPortrait : null;
        side.Portrait.color = side.Portrait.sprite != null ? Color.white : new Color(0.2f, 0.1f, 0.25f);
        side.Name.text = "Entidad";
        side.Body.text = $"Vence con <b>{required}</b> o más";
    }

    /// <param name="rivalDefense">Rival's defense cards, already subtracted from this side's number.</param>
    private void SetChips(Side side, int modifier, bool shield, int rivalDefense)
    {
        string chips = "";

        if (modifier > 0) chips += $"<color=#73FF80>+{modifier} carta</color>";
        else if (modifier < 0) chips += $"<color=#FF7060>{modifier} penalización</color>";

        if (shield) chips += (chips.Length > 0 ? "   " : "") + "<color=#80C8FF>ESCUDO</color>";
        if (rivalDefense > 0) chips += (chips.Length > 0 ? "   " : "") + $"<color=#80C8FF>-{rivalDefense} por defensa</color>";

        side.Chips.text = chips;
    }

    /// <summary>Starts the dice when the side's roll appears; shows the final result when it lands.</summary>
    private void UpdateSideRoll(Side side, int roll, int total, float seconds, Color tint)
    {
        if (roll > 0 && side.ShownRoll == 0)
        {
            side.ShownRoll = roll;
            side.Dice.Play(roll, seconds, tint, "");
            side.RevealAt = Time.unscaledTime + seconds + 0.4f;
            side.Result.text = "";
        }

        if (side.ShownRoll > 0 && Time.unscaledTime >= side.RevealAt)
        {
            if (string.IsNullOrEmpty(side.Result.text))
            {
                side.Result.color = Color.white;

                // The dice gives way to the number.
                side.Dice.gameObject.SetActive(false);
            }

            side.Result.text = total.ToString();
        }
        else
        {
            side.Result.text = side.ShownRoll > 0 ? "" : "?";
            side.Result.color = new Color(1f, 1f, 1f, 0.35f);
        }
    }

    private void ResetSide(Side side)
    {
        side.ShownRoll = 0;
        side.RevealAt = -1f;
        side.HitAt = -1f;
        side.Result.text = "?";
        side.Stamp.text = "";
        side.Damage.text = "";
        side.Flash.color = new Color(1f, 0.1f, 0.1f, 0f);
        side.Card.anchoredPosition = side.Home;
        side.Card.localScale = Vector3.one;
        side.Portrait.color = Color.white;
        side.Dice.gameObject.SetActive(false);
    }

    private void Hit(Side side, string stamp, string damage)
    {
        side.HitAt = Time.unscaledTime;
        side.Stamp.text = string.IsNullOrEmpty(stamp) ? "" : $"<color=#FF5A4D>{stamp}</color>";
        side.Damage.text = damage;
    }

    private void Update()
    {
        if (left == null || !Visible)
            return;

        Animate(left);
        Animate(right);

        // The button waiting for the player breathes.
        float pulse = 1f + Mathf.Sin(Time.unscaledTime * 6f) * 0.05f;
        rollButton.transform.localScale = rollButton.gameObject.activeSelf && rollButton.interactable ? Vector3.one * pulse : Vector3.one;
    }

    /// <summary>Hit: shake + red flash on the card, damage text rising above it.</summary>
    private static void Animate(Side side)
    {
        if (side.HitAt < 0f)
            return;

        float t = Time.unscaledTime - side.HitAt;
        float shake = t < 0.5f ? (1f - t / 0.5f) * 12f : 0f;
        side.Card.anchoredPosition = side.Home + new Vector2(Mathf.Sin(t * 70f) * shake, Mathf.Cos(t * 55f) * shake * 0.4f);
        side.Flash.color = new Color(1f, 0.1f, 0.1f, Mathf.Clamp01(0.6f - t * 0.9f));
        side.Card.localScale = Vector3.one * (t < 0.15f ? 1f + t : Mathf.Lerp(1.15f, 0.96f, Mathf.Clamp01((t - 0.15f) / 0.3f)));

        RectTransform dmg = side.Damage.rectTransform;
        dmg.anchoredPosition = side.Home + new Vector2(0f, Mathf.Min(t, 0.8f) * 60f);
        side.Damage.alpha = t < 2f ? 1f : Mathf.Clamp01(1f - (t - 2f));
    }

    // =========================================================
    // HELPERS
    // =========================================================

    private static void SetButton(Button button, bool visible, bool interactable)
    {
        button.gameObject.SetActive(visible);
        button.interactable = interactable;
    }

    private void LayoutButtons()
    {
        Button[] buttons = { useCardButton, skipButton, escapeButton, rollButton };
        int count = 0;

        foreach (Button b in buttons)
            if (b.gameObject.activeSelf) count++;

        float spacing = 210f, x = -(count - 1) * spacing * 0.5f;

        foreach (Button b in buttons)
        {
            if (!b.gameObject.activeSelf)
                continue;

            ((RectTransform)b.transform).anchoredPosition = new Vector2(x, 14f);
            x += spacing;
        }
    }

    /// <summary>Estudiante: what escaping costs (chance of 2 entities in a row in the next fight).</summary>
    private static string EscapeRisk()
    {
        PlayerNetworkData local = PlayerNetworkData.Local;
        CharacterData character = local != null ? local.Character : null;

        if (character == null)
            return "";

        int now = Mathf.RoundToInt(CharacterAbilityRules.DoubleFightChance(local.EscapedCombats.Value, character.abilityStackChance) * 100f);
        int next = Mathf.RoundToInt(CharacterAbilityRules.DoubleFightChance(local.EscapedCombats.Value + 1, character.abilityStackChance) * 100f);
        return $"ESCAPAR: riesgo de 2 entidades seguidas {now}% → {next}%";
    }

    private static string Name(PlayerNetworkData p) => p != null ? p.CharacterLabel : "?";

    private static string Hearts(int lives)
    {
        return lives > 0 ? new string('♥', lives) : "0";
    }

    private static Color CharacterColor(PlayerNetworkData p)
    {
        CharacterData c = p != null ? p.Character : null;
        Color color = c != null && c.boardColor.a > 0f ? c.boardColor : Gold;
        return new Color(color.r, color.g, color.b, 1f);
    }
}
