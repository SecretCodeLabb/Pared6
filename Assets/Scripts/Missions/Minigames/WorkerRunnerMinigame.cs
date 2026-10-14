using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Random = UnityEngine.Random;

/// <summary>
/// Trabajador cansado's mission: 2D endless runner in the style of Chrome's dinosaur game.
/// The worker runs on his own; jump over the obstacles until the score reaches the target
/// (100 by default). Touching an obstacle fails the mission; it is retried next turn.
/// Only the mission player plays (Space / W / Up / click or the SALTAR button; holding jumps higher);
/// the others watch a live copy built from snapshots (SendState / OnRemoteState) and cannot interact.
/// Art: every sprite field is optional (coloured boxes when empty). Replace the PNGs in
/// Assets/Art/Minigames/Trabajador (or assign other sprites on the prefab) — no code changes.
/// </summary>
public class WorkerRunnerMinigame : MissionMinigame
{
    private enum Stage : byte { Intro, Running, Won, Lost }

    [Serializable]
    public class ObstacleType
    {
        public Sprite sprite;
        [Tooltip("Size on the field (UI units). The hitbox is a bit smaller (hitboxScale).")]
        public Vector2 size = new Vector2(40f, 50f);
        [Tooltip("Height of the obstacle's bottom above the ground (0 = on the ground).")]
        public float heightAboveGround;
        [Min(0f)] public float weight = 1f;
    }

    [Header("Sprites (optional: empty = coloured box)")]
    [SerializeField] private Sprite background;
    [Tooltip("Repeating ground strip, scrolls with the speed.")]
    [SerializeField] private Texture2D groundTexture;
    [Tooltip("Running animation. One frame is enough: the code adds a trot, a jump tilt and a red tint on crash.")]
    [SerializeField] private Sprite[] runFrames = new Sprite[0];
    [Tooltip("Optional: empty = the run frame.")]
    [SerializeField] private Sprite jumpSprite;
    [Tooltip("Optional: empty = the run frame tinted red.")]
    [SerializeField] private Sprite crashSprite;
    [SerializeField] private ObstacleType[] obstacles =
    {
        new ObstacleType { size = new Vector2(40f, 50f) },
        new ObstacleType { size = new Vector2(46f, 86f) },
        new ObstacleType { size = new Vector2(84f, 46f) },
    };

    [Header("Field (UI units)")]
    [SerializeField] private Vector2 fieldSize = new Vector2(880f, 380f);
    [SerializeField] private float groundHeight = 50f;
    [SerializeField] private Vector2 playerSize = new Vector2(100f, 88f);
    [SerializeField] private float playerX = -300f;
    [Range(0.4f, 1f)] [SerializeField] private float hitboxScale = 0.72f;

    [Header("Rules")]
    [SerializeField] private int targetScore = 100;
    [Tooltip("Points per second at the start speed (they grow with the speed).")]
    [SerializeField] private float pointsPerSecond = 7f;

    [Header("Movement")]
    [SerializeField] private float startSpeed = 380f;
    [SerializeField] private float maxSpeed = 720f;
    [Tooltip("Speed gained per second of running.")]
    [SerializeField] private float acceleration = 18f;
    [SerializeField] private float gravity = 2600f;
    [SerializeField] private float jumpVelocity = 900f;
    [Tooltip("Releasing the jump early cuts the upward speed (short hop).")]
    [Range(0f, 1f)] [SerializeField] private float shortHopFactor = 0.45f;
    [Tooltip("Gap between obstacles, in seconds at the current speed (min, max).")]
    [SerializeField] private Vector2 spawnGapSeconds = new Vector2(0.95f, 1.9f);

    [Header("Timing")]
    [SerializeField] private float introSeconds = 2.5f;
    [SerializeField] private float endSeconds = 1.5f;
    [SerializeField] private float snapshotInterval = 0.05f;
    [SerializeField] private float runFrameSeconds = 0.12f;

    private const int MaxObstacles = 12;

    private struct Obstacle
    {
        public byte Type;
        public float X;
    }

    // ---- Simulation state (authoritative on the mission player, mirrored on spectators) ----
    private Stage stage = Stage.Intro;
    private float stageTime;
    private float time;
    private float score;
    private float speed;
    private float playerY;          // height of the feet above the ground
    private float velocityY;
    private float groundScroll;
    private float nextSpawnX;
    private bool jumpHeld;
    private readonly List<Obstacle> obstaclesOnField = new List<Obstacle>();
    private float nextSnapshot;
    private bool reported;

    // ---- View ----
    private RectTransform field;
    private RawImage ground;
    private Image playerImage;
    private readonly List<Image> obstacleImages = new List<Image>();
    private TextMeshProUGUI scoreText;
    private TextMeshProUGUI centerText;
    private TextMeshProUGUI footerText;
    private HoldButton jumpButton;

    public override bool SupportsAutoPlay => true;

    private float HalfW => fieldSize.x * 0.5f;
    private float HalfH => fieldSize.y * 0.5f;
    private float GroundY => -HalfH + groundHeight;
    private bool OnGround => playerY <= 0f;

    // =========================================================
    // SETUP
    // =========================================================

    protected override void OnBegin()
    {
        speed = startSpeed;
        nextSpawnX = HalfW + 200f;
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

        Image panel = UIFactory.CreatePanel(transform, "Panel", new Color(0.08f, 0.07f, 0.10f, 0.98f));
        UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(fieldSize.x + 40f, fieldSize.y + 190f));

        string title = Definition != null ? Definition.title : "Misión";
        string header = IsLocalPlayer ? $"TU MISIÓN · {title}" : $"MISIÓN DE {CharacterName.ToUpper()} · {title}";
        TextMeshProUGUI headerText = UIFactory.CreateText(panel.transform, "Header", header, 26f);
        UIFactory.Place(headerText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(fieldSize.x, 36f));
        headerText.color = UIFactory.AccentColor;
        headerText.fontStyle = FontStyles.Bold;

        scoreText = UIFactory.CreateText(panel.transform, "Score", "", 22f, TextAlignmentOptions.Right);
        UIFactory.Place(scoreText.rectTransform, new Vector2(1f, 1f), new Vector2(-24f, -46f), new Vector2(400f, 30f));

        Image fieldImage = UIFactory.CreatePanel(panel.transform, "Field", background != null ? Color.white : new Color(0.85f, 0.85f, 0.80f, 1f));
        fieldImage.sprite = background;
        fieldImage.raycastTarget = false;
        field = UIFactory.Place(fieldImage.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -80f), fieldSize);
        field.gameObject.AddComponent<RectMask2D>();

        // Scrolling ground strip.
        RectTransform groundRect = UIFactory.CreateRect(field, "Ground");
        ground = groundRect.gameObject.AddComponent<RawImage>();
        ground.texture = groundTexture;
        ground.color = groundTexture != null ? Color.white : new Color(0.35f, 0.30f, 0.25f);
        ground.raycastTarget = false;
        groundRect.anchorMin = new Vector2(0f, 0f);
        groundRect.anchorMax = new Vector2(1f, 0f);
        groundRect.pivot = new Vector2(0.5f, 0f);
        groundRect.anchoredPosition = Vector2.zero;
        groundRect.sizeDelta = new Vector2(0f, groundHeight);

        playerImage = CreateSprite("Worker", null, new Color(0.25f, 0.45f, 0.75f), playerSize);
        playerImage.rectTransform.pivot = new Vector2(0.5f, 0f);

        centerText = UIFactory.CreateText(field, "Center", "", 40f);
        UIFactory.Stretch(centerText.rectTransform, 20f);
        centerText.fontStyle = FontStyles.Bold;
        centerText.color = new Color(0.15f, 0.12f, 0.10f);

        footerText = UIFactory.CreateText(panel.transform, "Footer", "", 18f);
        UIFactory.Place(footerText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(fieldSize.x, 80f));
        footerText.color = new Color(0.8f, 0.8f, 0.8f);

        if (IsLocalPlayer)
        {
            footerText.text = ResponsiveCanvas.IsTouchDevice ? "" : "Saltar: Espacio / W / flecha arriba (mantén para saltar más alto)";
            Button button = UIFactory.CreateButton(panel.transform, "Jump", "SALTAR", null, UIFactory.ButtonColor, 30f);
            UIFactory.Place((RectTransform)button.transform, new Vector2(1f, 0f), new Vector2(-20f, 14f), new Vector2(240f, 80f));
            jumpButton = button.gameObject.AddComponent<HoldButton>();

            if (!ResponsiveCanvas.IsTouchDevice)
                UIFactory.Place(footerText.rectTransform, new Vector2(0f, 0f), new Vector2(20f, 12f), new Vector2(fieldSize.x - 260f, 80f));
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
                    SetStage(Stage.Running);
                break;

            case Stage.Running:
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
            Debug.Log($"[Runner] {next} a los {time:0.0}s · puntos {Mathf.FloorToInt(score)}/{targetScore} · velocidad {speed:0}");
    }

    private void Simulate(float dt)
    {
        bool jump = ReadJump();

        // Jump: press on the ground; releasing early while rising cuts the jump (short hop).
        if (jump && !jumpHeld && OnGround)
            velocityY = jumpVelocity;
        else if (!jump && jumpHeld && velocityY > 0f)
            velocityY *= shortHopFactor;

        jumpHeld = jump;
        velocityY -= gravity * dt;
        playerY += velocityY * dt;

        if (playerY <= 0f)
        {
            playerY = 0f;
            velocityY = 0f;
        }

        speed = Mathf.Min(maxSpeed, speed + acceleration * dt);
        score += pointsPerSecond * (speed / startSpeed) * dt;
        groundScroll += speed * dt;

        for (int i = obstaclesOnField.Count - 1; i >= 0; i--)
        {
            Obstacle o = obstaclesOnField[i];
            o.X -= speed * dt;
            obstaclesOnField[i] = o;

            if (o.X < -HalfW - 150f)
                obstaclesOnField.RemoveAt(i);
        }

        nextSpawnX -= speed * dt;

        if (nextSpawnX <= HalfW + 100f && obstaclesOnField.Count < MaxObstacles && obstacles.Length > 0)
        {
            obstaclesOnField.Add(new Obstacle { Type = (byte)PickObstacleType(), X = HalfW + 100f });
            nextSpawnX = HalfW + 100f + speed * Random.Range(spawnGapSeconds.x, spawnGapSeconds.y);
        }

        if (Collides())
        {
            SetStage(Stage.Lost);
            return;
        }

        if (score >= targetScore)
        {
            score = targetScore;
            SetStage(Stage.Won);
        }
    }

    private bool ReadJump()
    {
        if (AutoPlay)
            return AutoJump();

        Keyboard keyboard = Keyboard.current;
        bool jump = keyboard != null && (keyboard.spaceKey.isPressed || keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed);
        return jump || (jumpButton != null && jumpButton.IsHeld);
    }

    /// <summary>Dev bot: full jump when the next obstacle is close enough.</summary>
    private bool AutoJump()
    {
        // Full jump timed so the top of the arc is over the obstacle's centre (works for wide ones).
        float timeToApex = jumpVelocity / gravity;

        foreach (Obstacle o in obstaclesOnField)
        {
            ObstacleType type = obstacles[o.Type];
            float toCentre = o.X - playerX;

            if (toCentre > -type.size.x * 0.5f && toCentre < speed * timeToApex)
                return OnGround || velocityY > 0f;
        }

        return false;
    }

    private int PickObstacleType()
    {
        float total = 0f;

        foreach (ObstacleType t in obstacles)
            total += Mathf.Max(0f, t.weight);

        float pick = Random.value * total;

        for (int i = 0; i < obstacles.Length; i++)
        {
            pick -= Mathf.Max(0f, obstacles[i].weight);

            if (pick <= 0f)
                return i;
        }

        return obstacles.Length - 1;
    }

    private bool Collides()
    {
        Rect player = new Rect(
            playerX - playerSize.x * hitboxScale * 0.5f,
            GroundY + playerY,
            playerSize.x * hitboxScale,
            playerSize.y * hitboxScale);

        foreach (Obstacle o in obstaclesOnField)
        {
            ObstacleType type = obstacles[o.Type];
            Vector2 size = type.size * hitboxScale;
            Rect box = new Rect(o.X - size.x * 0.5f, GroundY + type.heightAboveGround, size.x, size.y);

            if (player.Overlaps(box))
                return true;
        }

        return false;
    }

    // =========================================================
    // RENDER (same on the player and the spectators)
    // =========================================================

    private void Render()
    {
        Sprite sprite = PlayerSprite();
        bool crashed = stage == Stage.Lost;
        bool trotting = stage == Stage.Running && OnGround;

        // Procedural motion so a single still frame still feels alive: trot on the ground,
        // nose up while rising / down while falling, red flash on the crash.
        float trot = trotting ? Mathf.Abs(Mathf.Sin(time * 16f)) * 5f : 0f;
        float tilt = trotting ? Mathf.Sin(time * 16f) * 3f : (OnGround ? 0f : Mathf.Clamp(velocityY / jumpVelocity, -1f, 1f) * 14f);

        playerImage.rectTransform.anchoredPosition = new Vector2(playerX, GroundY + playerY + trot);
        playerImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, crashed ? -12f : tilt);
        playerImage.sprite = sprite;
        playerImage.preserveAspect = sprite != null;

        if (sprite != null)
            playerImage.color = crashed && crashSprite == null ? new Color(1f, 0.45f, 0.45f) : Color.white;
        else
            playerImage.color = crashed ? new Color(0.8f, 0.25f, 0.25f) : new Color(0.25f, 0.45f, 0.75f);

        if (ground.texture != null)
        {
            float tiles = fieldSize.x / Mathf.Max(1f, groundTexture.width * (groundHeight / Mathf.Max(1f, groundTexture.height)));
            float offset = groundScroll / fieldSize.x * tiles;
            ground.uvRect = new Rect(offset - Mathf.Floor(offset), 0f, tiles, 1f);
        }

        for (int i = 0; i < obstaclesOnField.Count; i++)
        {
            Obstacle o = obstaclesOnField[i];
            ObstacleType type = obstacles[Mathf.Clamp(o.Type, 0, obstacles.Length - 1)];

            if (i >= obstacleImages.Count)
            {
                Image created = CreateSprite("Obstacle" + i, null, Color.white, type.size);
                created.rectTransform.pivot = new Vector2(0.5f, 0f);
                obstacleImages.Add(created);
            }

            Image image = obstacleImages[i];
            image.enabled = true;
            image.sprite = type.sprite;
            image.preserveAspect = type.sprite != null;
            image.color = type.sprite != null ? Color.white : new Color(0.45f, 0.30f, 0.20f);
            image.rectTransform.sizeDelta = type.size;
            image.rectTransform.anchoredPosition = new Vector2(o.X, GroundY + type.heightAboveGround);
        }

        for (int i = obstaclesOnField.Count; i < obstacleImages.Count; i++)
            obstacleImages[i].enabled = false;

        // The player is drawn over the obstacles.
        playerImage.transform.SetAsLastSibling();
        centerText.transform.SetAsLastSibling();

        scoreText.text = $"PUNTOS: <b>{Mathf.FloorToInt(score):000}</b> / {targetScore}";
        centerText.text = CenterMessage();
    }

    private Sprite PlayerSprite()
    {
        if (stage == Stage.Lost && crashSprite != null)
            return crashSprite;

        if (!OnGround && jumpSprite != null)
            return jumpSprite;

        if (runFrames == null || runFrames.Length == 0)
            return jumpSprite != null ? jumpSprite : crashSprite;

        if (stage != Stage.Running)
            return runFrames[0];

        int frame = Mathf.FloorToInt(time / runFrameSeconds) % runFrames.Length;
        return runFrames[frame];
    }

    private string CenterMessage()
    {
        switch (stage)
        {
            case Stage.Intro:
                return IsLocalPlayer
                    ? $"¡Prepárate!\n<size=60%>Salta los obstáculos hasta llegar a {targetScore} puntos</size>"
                    : $"{CharacterName} se prepara...\n<size=60%>Debe llegar a {targetScore} puntos sin chocar</size>";
            case Stage.Running:
                return stageTime < 1f ? "¡CORRE!" : "";
            case Stage.Won:
                return IsLocalPlayer ? "<color=#1F9E3A>¡LLEGASTE!</color>" : $"<color=#1F9E3A>¡{CharacterName.ToUpper()} LLEGÓ!</color>";
            case Stage.Lost:
                return IsLocalPlayer ? "<color=#C02020>¡CHOCASTE!</color>" : $"<color=#C02020>¡{CharacterName.ToUpper()} CHOCÓ!</color>";
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
                ? "<color=#1F9E3A>¡MISIÓN COMPLETADA!</color>"
                : $"<color=#1F9E3A>¡{CharacterName.ToUpper()} COMPLETÓ SU MISIÓN!</color>";
        }
        else
        {
            stage = Stage.Lost;
            centerText.text = IsLocalPlayer
                ? "<color=#C02020>MISIÓN FALLIDA</color>\n<size=55%>Lo intentarás de nuevo en tu próximo turno</size>"
                : $"<color=#C02020>{CharacterName.ToUpper()} FALLÓ SU MISIÓN</color>\n<size=55%>Lo reintentará en su próximo turno</size>";
        }

        if (jumpButton != null) jumpButton.gameObject.SetActive(false);
        enabled = false;
    }

    // =========================================================
    // SNAPSHOTS (live spectating)
    // =========================================================

    private byte[] WriteState()
    {
        using (MemoryStream stream = new MemoryStream(128))
        using (BinaryWriter w = new BinaryWriter(stream))
        {
            w.Write((byte)stage);
            w.Write(stageTime);
            w.Write(time);
            w.Write(score);
            w.Write((short)playerY);
            w.Write(groundScroll);
            w.Write((byte)obstaclesOnField.Count);

            foreach (Obstacle o in obstaclesOnField)
            {
                w.Write(o.Type);
                w.Write((short)o.X);
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
                score = r.ReadSingle();
                playerY = r.ReadInt16();
                groundScroll = r.ReadSingle();

                int count = r.ReadByte();
                obstaclesOnField.Clear();

                for (int i = 0; i < count; i++)
                {
                    byte type = r.ReadByte();
                    obstaclesOnField.Add(new Obstacle { Type = type, X = r.ReadInt16() });
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
