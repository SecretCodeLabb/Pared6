using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Random = UnityEngine.Random;

/// <summary>
/// Puppy's mission (a high-society girl leaving the party): 2D "catch" game in a panel. She walks along
/// the floor and catches what falls: calls and keys add points, receipts add seconds to the clock and
/// aguardiente takes points away. Reaching the target score wins; dropping to 0 points or running out of
/// time (30 s) loses. The closer to the target, the faster and denser it rains.
/// The mission is retried next turn on a loss (MissionManager / TurnManager).
/// Only the mission player plays (A/D, arrows, or dragging / holding the side buttons on touch);
/// the others watch a live copy built from snapshots (SendState / OnRemoteState) and cannot interact.
/// Art: every sprite is optional (coloured boxes when empty). Replace the PNGs in
/// Assets/Art/Minigames/Puppy (or assign other sprites on the prefab) — no code changes.
/// </summary>
public class PuppyCatchMinigame : MissionMinigame
{
    private enum Stage : byte { Intro, Playing, Won, Lost }

    [Serializable]
    public class FallingType
    {
        public string name = "Objeto";
        public Sprite sprite;
        public Color fallback = Color.white;
        [Tooltip("Points when caught (negative = takes points away).")]
        public int points = 1;
        [Tooltip("Seconds added to the clock when caught.")]
        public float seconds;
        public Vector2 size = new Vector2(56f, 56f);
        [Min(0f)] public float weight = 1f;
    }

    [Header("Sprites (optional: empty = coloured box)")]
    [SerializeField] private Sprite background;
    [SerializeField] private Sprite puppySprite;
    [SerializeField] private FallingType[] items =
    {
        new FallingType { name = "Llamada", fallback = new Color(1f, 0.55f, 0.75f), points = 1, size = new Vector2(64f, 60f), weight = 1.2f },
        new FallingType { name = "Llaves", fallback = new Color(0.85f, 0.7f, 0.45f), points = 2, size = new Vector2(56f, 64f), weight = 0.8f },
        new FallingType { name = "Recibo", fallback = new Color(0.95f, 0.95f, 0.95f), points = 0, seconds = 1f, size = new Vector2(46f, 66f), weight = 0.6f },
        new FallingType { name = "Aguardiente", fallback = new Color(0.6f, 0.8f, 0.85f), points = -2, size = new Vector2(34f, 76f), weight = 1f },
    };

    [Header("Field (UI units)")]
    [SerializeField] private Vector2 fieldSize = new Vector2(880f, 470f);
    [SerializeField] private Vector2 puppySize = new Vector2(72f, 132f);
    [Tooltip("Height of the floor the player walks on, from the bottom of the field.")]
    [SerializeField] private float groundHeight = 26f;

    [Header("Rules")]
    [SerializeField] private int targetScore = 20;
    [Tooltip("Points at the start (a bad catch before any good one loses straight away when this is low).")]
    [SerializeField] private int startScore = 3;
    [Tooltip("Seconds to reach the target. Running out loses; receipts add time.")]
    [SerializeField] private float timeLimit = 30f;

    [Header("Difficulty (start → when about to win)")]
    [SerializeField] private Vector2 fallSpeed = new Vector2(170f, 380f);
    [SerializeField] private Vector2 spawnEvery = new Vector2(0.95f, 0.42f);
    [Tooltip("Share of bad objects (start → end).")]
    [SerializeField] private Vector2 badShare = new Vector2(0.22f, 0.5f);
    [SerializeField] private float puppySpeed = 620f;

    [Header("Timing")]
    [SerializeField] private float introSeconds = 2.5f;
    [SerializeField] private float endSeconds = 1.5f;
    [SerializeField] private float snapshotInterval = 0.05f;

    private const int MaxFalling = 24;

    private struct Falling
    {
        public byte Type;
        public Vector2 Position;
        public float Speed;
    }

    private struct Popup
    {
        public Vector2 Position;
        public int Points;
        public int Seconds;
        public float Time;
    }

    // ---- Simulation state (authoritative on the mission player, mirrored on spectators) ----
    private Stage stage = Stage.Intro;
    private float stageTime;
    private float time;
    private int score;
    private float timeLeft;
    private bool outOfTime;
    private float puppyX;
    private float facing = 1f;
    private readonly List<Falling> falling = new List<Falling>();
    private readonly List<Popup> popups = new List<Popup>();
    private float nextSpawn;
    private float nextSnapshot;
    private float hitFlash = -10f;
    private bool reported;

    // ---- View ----
    private RectTransform field;
    private Image puppyImage;
    private readonly List<Image> fallingImages = new List<Image>();
    private readonly List<TextMeshProUGUI> popupTexts = new List<TextMeshProUGUI>();
    private Image progressFill;
    private TextMeshProUGUI scoreText;
    private TextMeshProUGUI timerText;
    private TextMeshProUGUI centerText;
    private TextMeshProUGUI footerText;
    private HoldButton leftButton;
    private HoldButton rightButton;

    public override bool SupportsAutoPlay => true;

    private float HalfW => fieldSize.x * 0.5f;
    private float HalfH => fieldSize.y * 0.5f;
    private float PuppyY => -HalfH + groundHeight + puppySize.y * 0.5f;
    private float Progress => Mathf.Clamp01(score / (float)Mathf.Max(1, targetScore));

    // =========================================================
    // SETUP
    // =========================================================

    protected override void OnBegin()
    {
        score = startScore;
        timeLeft = timeLimit;
        BuildView();
        Render();
    }

    private void BuildView()
    {
        RectTransform root = (RectTransform)transform;
        UIFactory.Stretch(root);

        // Full-screen dim: blocks every click on the board/HUD while the minigame is open.
        Image dim = gameObject.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.65f);

        Image panel = UIFactory.CreatePanel(transform, "Panel", new Color(0.07f, 0.08f, 0.13f, 0.98f));
        UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(fieldSize.x + 40f, fieldSize.y + 190f));

        string title = Definition != null ? Definition.title : "Misión";
        string header = IsLocalPlayer ? $"TU MISIÓN · {title}" : $"MISIÓN DE {CharacterName.ToUpper()} · {title}";
        TextMeshProUGUI headerText = UIFactory.CreateText(panel.transform, "Header", header, 26f);
        UIFactory.Place(headerText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(fieldSize.x, 36f));
        headerText.color = UIFactory.AccentColor;
        headerText.fontStyle = FontStyles.Bold;

        // Score + progress bar towards the target.
        scoreText = UIFactory.CreateText(panel.transform, "Score", "", 22f, TextAlignmentOptions.Left);
        UIFactory.Place(scoreText.rectTransform, new Vector2(0f, 1f), new Vector2(24f, -46f), new Vector2(260f, 30f));

        timerText = UIFactory.CreateText(panel.transform, "Timer", "", 22f, TextAlignmentOptions.Right);
        UIFactory.Place(timerText.rectTransform, new Vector2(1f, 1f), new Vector2(-24f, -46f), new Vector2(170f, 30f));
        timerText.fontStyle = FontStyles.Bold;

        Image bar = UIFactory.CreatePanel(panel.transform, "Bar", new Color(0.15f, 0.15f, 0.2f));
        UIFactory.Place(bar.rectTransform, new Vector2(1f, 1f), new Vector2(-210f, -52f), new Vector2(fieldSize.x - 460f, 18f));
        progressFill = UIFactory.CreatePanel(bar.transform, "Fill", new Color(0.45f, 0.9f, 0.45f));
        progressFill.rectTransform.anchorMin = Vector2.zero;
        progressFill.rectTransform.anchorMax = new Vector2(0f, 1f);
        progressFill.rectTransform.pivot = new Vector2(0f, 0.5f);
        progressFill.rectTransform.offsetMin = Vector2.zero;
        progressFill.rectTransform.offsetMax = Vector2.zero;

        Image fieldImage = UIFactory.CreatePanel(panel.transform, "Field", background != null ? Color.white : new Color(0.45f, 0.7f, 0.95f, 1f));
        fieldImage.sprite = background;
        fieldImage.raycastTarget = false;
        field = UIFactory.Place(fieldImage.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -80f), fieldSize);
        field.gameObject.AddComponent<RectMask2D>();

        if (background == null)
        {
            Image grass = UIFactory.CreatePanel(field, "Ground", new Color(0.35f, 0.65f, 0.3f));
            grass.raycastTarget = false;
            grass.rectTransform.anchorMin = Vector2.zero;
            grass.rectTransform.anchorMax = new Vector2(1f, 0f);
            grass.rectTransform.pivot = new Vector2(0.5f, 0f);
            grass.rectTransform.sizeDelta = new Vector2(0f, groundHeight);
        }

        puppyImage = CreateSprite("Puppy", puppySprite, new Color(0.95f, 0.75f, 0.85f), puppySize);

        centerText = UIFactory.CreateText(field, "Center", "", 40f);
        UIFactory.Stretch(centerText.rectTransform, 20f);
        centerText.fontStyle = FontStyles.Bold;
        centerText.outlineWidth = 0.25f;
        centerText.outlineColor = Color.black;

        footerText = UIFactory.CreateText(panel.transform, "Footer", "", 18f);
        UIFactory.Place(footerText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(fieldSize.x - 340f, 80f));
        footerText.color = new Color(0.85f, 0.85f, 0.85f);

        if (IsLocalPlayer)
        {
            footerText.text = ResponsiveCanvas.IsTouchDevice ? "Mantén ◄ ► para moverte" : "Mover: A/D o flechas (o mantén ◄ ►)";
            leftButton = CreateHoldButton(panel.transform, "Left", "◄", new Vector2(0f, 0f), new Vector2(20f, 14f));
            rightButton = CreateHoldButton(panel.transform, "Right", "►", new Vector2(1f, 0f), new Vector2(-20f, 14f));
        }
        else
        {
            footerText.text = $"{CharacterName} está jugando su misión · solo puedes mirar";
        }
    }

    private Image CreateSprite(string name, Sprite sprite, Color fallback, Vector2 size)
    {
        Image image = UIFactory.CreatePanel(field, name, sprite != null ? Color.white : fallback);
        image.sprite = sprite;
        image.preserveAspect = sprite != null;
        image.raycastTarget = false;
        UIFactory.Place(image.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, size);
        image.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        return image;
    }

    private static HoldButton CreateHoldButton(Transform parent, string name, string label, Vector2 anchor, Vector2 position)
    {
        Button button = UIFactory.CreateButton(parent, name, label, null, UIFactory.ButtonColor, 34f);
        UIFactory.Place((RectTransform)button.transform, anchor, position, new Vector2(150f, 80f));
        return button.gameObject.AddComponent<HoldButton>();
    }

    // =========================================================
    // LOOP
    // =========================================================

    private void Update()
    {
        if (field == null || !IsLocalPlayer)
            return;

        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        time += dt;
        stageTime += dt;

        switch (stage)
        {
            case Stage.Intro:
                if (stageTime >= introSeconds)
                    SetStage(Stage.Playing);
                break;

            case Stage.Playing:
                Simulate(dt);
                break;

            case Stage.Won:
            case Stage.Lost:
                if (!reported && stageTime >= endSeconds)
                {
                    reported = true;
                    Finish(stage == Stage.Won);
                }
                break;
        }

        Render();

        if (time >= nextSnapshot)
        {
            nextSnapshot = time + snapshotInterval;
            SendState(WriteState());
        }
    }

    private void SetStage(Stage next)
    {
        stage = next;
        stageTime = 0f;

        if (next == Stage.Won || next == Stage.Lost)
            Debug.Log($"[Catch] {next} a los {time:0.0}s · puntos {score}/{targetScore} · tiempo {timeLeft:0.0}s{(outOfTime ? " (sin tiempo)" : "")}");
    }

    private void Simulate(float dt)
    {
        timeLeft -= dt;

        if (timeLeft <= 0f)
        {
            timeLeft = 0f;
            outOfTime = true;
            falling.Clear();
            SetStage(Stage.Lost);
            return;
        }

        float move = ReadInput();
        puppyX = Mathf.Clamp(puppyX + move * puppySpeed * dt, -HalfW + puppySize.x * 0.5f, HalfW - puppySize.x * 0.5f);

        if (Mathf.Abs(move) > 0.1f)
            facing = Mathf.Sign(move);

        float p = Progress;

        if (time >= nextSpawn && falling.Count < MaxFalling && items.Length > 0)
        {
            nextSpawn = time + Mathf.Lerp(spawnEvery.x, spawnEvery.y, p) * Random.Range(0.75f, 1.25f);
            int type = PickType(Mathf.Lerp(badShare.x, badShare.y, p));
            float halfWidth = items[type].size.x * 0.5f;

            falling.Add(new Falling
            {
                Type = (byte)type,
                Position = new Vector2(Random.Range(-HalfW + halfWidth, HalfW - halfWidth), HalfH + items[type].size.y),
                Speed = Mathf.Lerp(fallSpeed.x, fallSpeed.y, p) * Random.Range(0.85f, 1.2f)
            });
        }

        Rect catcher = new Rect(puppyX - puppySize.x * 0.45f, PuppyY - puppySize.y * 0.2f, puppySize.x * 0.9f, puppySize.y * 0.65f);

        for (int i = falling.Count - 1; i >= 0; i--)
        {
            Falling f = falling[i];
            f.Position.y -= f.Speed * dt;
            falling[i] = f;

            FallingType type = items[f.Type];
            Rect box = new Rect(f.Position - type.size * 0.35f, type.size * 0.7f);

            if (box.Overlaps(catcher))
            {
                // Remove first: winning / losing clears the whole list.
                falling.RemoveAt(i);
                Catch(type, f.Position);

                if (stage != Stage.Playing)
                    return;

                continue;
            }

            if (f.Position.y < -HalfH - type.size.y)
                falling.RemoveAt(i);
        }

        for (int i = popups.Count - 1; i >= 0; i--)
            if (time - popups[i].Time > 0.9f) popups.RemoveAt(i);
    }

    private void Catch(FallingType type, Vector2 at)
    {
        score = Mathf.Max(0, score + type.points);
        timeLeft += type.seconds;
        popups.Add(new Popup { Position = at, Points = type.points, Seconds = Mathf.RoundToInt(type.seconds), Time = time });

        if (type.points < 0)
            hitFlash = time;

        if (score >= targetScore)
        {
            score = targetScore;
            falling.Clear();
            SetStage(Stage.Won);
        }
        else if (score <= 0)
        {
            falling.Clear();
            SetStage(Stage.Lost);
        }
    }

    private int PickType(float badChance)
    {
        bool bad = Random.value < badChance;
        float total = 0f;

        foreach (FallingType t in items)
            if ((t.points < 0) == bad) total += Mathf.Max(0f, t.weight);

        if (total <= 0f)
            return Random.Range(0, items.Length);

        float pick = Random.value * total;

        for (int i = 0; i < items.Length; i++)
        {
            if ((items[i].points < 0) != bad)
                continue;

            pick -= Mathf.Max(0f, items[i].weight);

            if (pick <= 0f)
                return i;
        }

        return items.Length - 1;
    }

    private float ReadInput()
    {
        if (AutoPlay)
            return AutoMove();

        float move = 0f;
        Keyboard keyboard = Keyboard.current;

        if (keyboard != null)
        {
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) move -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) move += 1f;
        }

        if (leftButton != null && leftButton.IsHeld) move -= 1f;
        if (rightButton != null && rightButton.IsHeld) move += 1f;

        return Mathf.Clamp(move, -1f, 1f);
    }

    /// <summary>Dev bot: chases the lowest good object, steps away from bad ones about to land on it.</summary>
    private float AutoMove()
    {
        float target = puppyX;
        float bestY = float.MaxValue;

        foreach (Falling f in falling)
        {
            FallingType t = items[f.Type];

            if ((t.points > 0 || t.seconds > 0f) && f.Position.y < bestY && f.Position.y > PuppyY)
            {
                bestY = f.Position.y;
                target = f.Position.x;
            }
        }

        foreach (Falling f in falling)
        {
            FallingType t = items[f.Type];

            if (t.points < 0 && f.Position.y - PuppyY < 160f && f.Position.y > PuppyY - 20f && Mathf.Abs(f.Position.x - puppyX) < puppySize.x * 0.8f)
                target = puppyX + (f.Position.x > puppyX ? -1f : 1f) * puppySize.x * 1.5f;
        }

        return Mathf.Clamp((target - puppyX) / 30f, -1f, 1f);
    }

    // =========================================================
    // RENDER (same on the player and the spectators)
    // =========================================================

    private void Render()
    {
        bool hurt = time - hitFlash < 0.35f;
        float hop = stage == Stage.Playing ? Mathf.Abs(Mathf.Sin(time * 12f)) * 4f : 0f;
        puppyImage.rectTransform.anchoredPosition = new Vector2(puppyX, PuppyY + hop);
        puppyImage.rectTransform.localScale = new Vector3(puppySprite != null ? facing : 1f, 1f, 1f);
        puppyImage.color = puppySprite != null ? (hurt ? new Color(1f, 0.45f, 0.45f) : Color.white) : (hurt ? Color.red : new Color(0.95f, 0.75f, 0.85f));

        for (int i = 0; i < falling.Count; i++)
        {
            if (i >= fallingImages.Count)
                fallingImages.Add(CreateSprite("Item" + i, null, Color.white, Vector2.one * 50f));

            Falling f = falling[i];
            FallingType type = items[Mathf.Clamp(f.Type, 0, items.Length - 1)];
            Image image = fallingImages[i];
            image.enabled = true;
            image.sprite = type.sprite;
            image.preserveAspect = type.sprite != null;
            image.color = type.sprite != null ? Color.white : type.fallback;
            image.rectTransform.sizeDelta = type.size;
            image.rectTransform.anchoredPosition = f.Position;
            image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * 3f + i) * 12f);
        }

        for (int i = falling.Count; i < fallingImages.Count; i++)
            fallingImages[i].enabled = false;

        for (int i = 0; i < popups.Count; i++)
        {
            if (i >= popupTexts.Count)
            {
                TextMeshProUGUI t = UIFactory.CreateText(field, "Popup" + i, "", 30f);
                UIFactory.Place(t.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 40f));
                t.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                t.fontStyle = FontStyles.Bold;
                t.outlineWidth = 0.25f;
                t.outlineColor = Color.black;
                popupTexts.Add(t);
            }

            Popup p = popups[i];
            float age = time - p.Time;
            TextMeshProUGUI text = popupTexts[i];
            text.enabled = true;
            text.text = p.Seconds > 0 && p.Points == 0 ? $"+{p.Seconds}s" : p.Points > 0 ? "+" + p.Points : p.Points.ToString();
            text.color = p.Seconds > 0 && p.Points == 0 ? new Color(0.55f, 0.85f, 1f, 1f - age)
                : p.Points > 0 ? new Color(0.5f, 1f, 0.5f, 1f - age) : new Color(1f, 0.35f, 0.3f, 1f - age);
            text.rectTransform.anchoredPosition = new Vector2(p.Position.x, PuppyY + 60f + age * 70f);
        }

        for (int i = popups.Count; i < popupTexts.Count; i++)
            popupTexts[i].enabled = false;

        scoreText.text = $"PUNTOS: <b>{score}</b> / {targetScore}";
        int seconds = Mathf.CeilToInt(timeLeft);
        timerText.text = $"TIEMPO: <b>{seconds}s</b>";
        timerText.color = seconds <= 10 && stage == Stage.Playing && Mathf.Repeat(time * 2f, 1f) < 0.5f
            ? new Color(1f, 0.35f, 0.3f) : seconds <= 10 ? new Color(1f, 0.6f, 0.4f) : Color.white;
        progressFill.rectTransform.anchorMax = new Vector2(Progress, 1f);
        progressFill.color = Color.Lerp(new Color(1f, 0.45f, 0.35f), new Color(0.45f, 0.9f, 0.45f), Progress);
        centerText.text = CenterMessage();
        centerText.transform.SetAsLastSibling();
    }

    private string CenterMessage()
    {
        switch (stage)
        {
            case Stage.Intro:
                return IsLocalPlayer
                    ? $"¡Sal de la fiesta!\n<size=50%>Atrapa llamadas y llaves · {targetScore} puntos en {Mathf.RoundToInt(timeLimit)} s\nLos recibos dan tiempo · el aguardiente resta puntos</size>"
                    : $"{CharacterName} intenta salir de la fiesta...\n<size=50%>Debe llegar a {targetScore} puntos en {Mathf.RoundToInt(timeLimit)} s</size>";
            case Stage.Playing:
                return stageTime < 1f ? "¡YA!" : "";
            case Stage.Won:
                return IsLocalPlayer ? "<color=#73FF80>¡LO LOGRASTE!</color>" : $"<color=#73FF80>¡{CharacterName.ToUpper()} LO LOGRÓ!</color>";
            case Stage.Lost:
                if (outOfTime)
                    return IsLocalPlayer ? "<color=#FF5A4D>¡SE ACABÓ EL TIEMPO!</color>" : $"<color=#FF5A4D>¡A {CharacterName.ToUpper()} SE LE ACABÓ EL TIEMPO!</color>";

                return IsLocalPlayer ? "<color=#FF5A4D>¡TE QUEDASTE SIN PUNTOS!</color>" : $"<color=#FF5A4D>¡{CharacterName.ToUpper()} SE QUEDÓ SIN PUNTOS!</color>";
            default:
                return "";
        }
    }

    public override void ShowResult(bool success)
    {
        if (success)
        {
            stage = Stage.Won;
            centerText.text = IsLocalPlayer
                ? "<color=#73FF80>¡MISIÓN COMPLETADA!</color>"
                : $"<color=#73FF80>¡{CharacterName.ToUpper()} COMPLETÓ SU MISIÓN!</color>";
        }
        else
        {
            stage = Stage.Lost;
            centerText.text = IsLocalPlayer
                ? "<color=#FF5A4D>MISIÓN FALLIDA</color>\n<size=55%>Lo intentarás de nuevo en tu próximo turno</size>"
                : $"<color=#FF5A4D>{CharacterName.ToUpper()} FALLÓ SU MISIÓN</color>\n<size=55%>Lo reintentará en su próximo turno</size>";
        }

        if (leftButton != null) leftButton.gameObject.SetActive(false);
        if (rightButton != null) rightButton.gameObject.SetActive(false);
        enabled = false;
    }

    // =========================================================
    // SNAPSHOTS (live spectating)
    // =========================================================

    private byte[] WriteState()
    {
        using (MemoryStream stream = new MemoryStream(256))
        using (BinaryWriter w = new BinaryWriter(stream))
        {
            w.Write((byte)stage);
            w.Write(stageTime);
            w.Write(time);
            w.Write((short)score);
            w.Write(timeLeft);
            w.Write(outOfTime);
            w.Write((short)puppyX);
            w.Write((sbyte)facing);
            w.Write(time - hitFlash);
            w.Write((byte)falling.Count);

            foreach (Falling f in falling)
            {
                w.Write(f.Type);
                w.Write((short)f.Position.x);
                w.Write((short)f.Position.y);
            }

            w.Write((byte)popups.Count);

            foreach (Popup p in popups)
            {
                w.Write((short)p.Position.x);
                w.Write((sbyte)p.Points);
                w.Write((sbyte)p.Seconds);
                w.Write(time - p.Time);
            }

            return stream.ToArray();
        }
    }

    protected override void OnRemoteState(byte[] state)
    {
        if (field == null || !enabled)
            return;

        try
        {
            using (BinaryReader r = new BinaryReader(new MemoryStream(state)))
            {
                stage = (Stage)r.ReadByte();
                stageTime = r.ReadSingle();
                time = r.ReadSingle();
                score = r.ReadInt16();
                timeLeft = r.ReadSingle();
                outOfTime = r.ReadBoolean();
                puppyX = r.ReadInt16();
                facing = r.ReadSByte();
                hitFlash = time - r.ReadSingle();

                falling.Clear();
                int count = r.ReadByte();

                for (int i = 0; i < count; i++)
                {
                    byte type = r.ReadByte();
                    falling.Add(new Falling { Type = type, Position = new Vector2(r.ReadInt16(), r.ReadInt16()) });
                }

                popups.Clear();
                count = r.ReadByte();

                for (int i = 0; i < count; i++)
                {
                    float x = r.ReadInt16();
                    int points = r.ReadSByte();
                    int secs = r.ReadSByte();
                    popups.Add(new Popup { Position = new Vector2(x, 0f), Points = points, Seconds = secs, Time = time - r.ReadSingle() });
                }
            }
        }
        catch (EndOfStreamException)
        {
            return;
        }

        Render();
    }
}
