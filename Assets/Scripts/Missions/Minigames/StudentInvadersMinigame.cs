using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Estudiante's mission: 2D "space invaders" in a panel. The student is the ship at the bottom;
/// two rows of different enemies come first, then a single big boss. Beat the boss before losing
/// every life to complete the mission. Failing ends the turn; the mission is retried next turn.
/// Only the mission player plays (keyboard or on-screen buttons); the others watch a live copy
/// built from snapshots (SendState / OnRemoteState) and cannot interact.
/// Art: every sprite field is optional (coloured boxes when empty). Replace the PNGs in
/// Assets/Art/Minigames/Estudiante (or assign other sprites on the prefab) — no code changes.
/// </summary>
public class StudentInvadersMinigame : MissionMinigame
{
    private enum Stage : byte { Intro, Rows, Boss, Won, Lost }

    [Header("Sprites (optional: empty = coloured box)")]
    [SerializeField] private Sprite background;
    [SerializeField] private Sprite playerSprite;
    [SerializeField] private Sprite enemyRowASprite;
    [SerializeField] private Sprite enemyRowBSprite;
    [SerializeField] private Sprite bossSprite;
    [SerializeField] private Sprite playerBulletSprite;
    [SerializeField] private Sprite enemyBulletSprite;

    [Header("Field (UI units)")]
    [SerializeField] private Vector2 fieldSize = new Vector2(880f, 500f);
    [SerializeField] private Vector2 playerSize = new Vector2(52f, 86f);
    [SerializeField] private Vector2 enemySize = new Vector2(54f, 54f);
    [SerializeField] private Vector2 bossSize = new Vector2(130f, 230f);
    [SerializeField] private Vector2 playerBulletSize = new Vector2(8f, 22f);
    [SerializeField] private Vector2 enemyBulletSize = new Vector2(10f, 22f);

    [Header("Player")]
    [Min(1)] [SerializeField] private int lives = 3;
    [SerializeField] private float playerSpeed = 460f;
    [SerializeField] private float fireCooldown = 0.28f;
    [SerializeField] private float playerBulletSpeed = 760f;
    [SerializeField] private float invulnerableSeconds = 1.2f;

    [Header("Enemy rows")]
    [Min(1)] [SerializeField] private int columns = 8;
    [SerializeField] private Vector2 enemySpacing = new Vector2(84f, 62f);
    [SerializeField] private float formationSpeed = 55f;
    [Tooltip("Extra speed when only one enemy is left (scales with the enemies destroyed).")]
    [SerializeField] private float formationSpeedBonus = 140f;
    [SerializeField] private float formationStepDown = 20f;
    [SerializeField] private float enemyFireInterval = 0.95f;
    [SerializeField] private float enemyBulletSpeed = 300f;

    [Header("Boss")]
    [Min(1)] [SerializeField] private int bossHealth = 20;
    [SerializeField] private float bossSpeed = 170f;
    [Tooltip("Seconds between boss volleys (higher = easier).")]
    [SerializeField] private float bossFireInterval = 1.8f;
    [Tooltip("Multiplier on the interval below half health (1 = no speed-up).")]
    [Range(0.3f, 1f)] [SerializeField] private float bossEnragedFireFactor = 0.85f;

    [Header("Timing")]
    [SerializeField] private float introSeconds = 2.5f;
    [SerializeField] private float endSeconds = 1.5f;
    [SerializeField] private float snapshotInterval = 0.05f;

    private const int Rows = 2;
    private const int MaxBullets = 40;

    private struct Bullet
    {
        public bool FromPlayer;
        public Vector2 Position;
    }

    // ---- Simulation state (authoritative on the mission player, mirrored on spectators) ----
    private Stage stage = Stage.Intro;
    private float stageTime;
    private float playerX;
    private int livesLeft;
    private float invulnerableUntil;
    private Vector2 formationOffset;
    private float formationDirection = 1f;
    private bool[] alive;
    private float bossX;
    private float bossY;
    private float bossDirection = 1f;
    private int bossHp;
    private readonly List<Bullet> bullets = new List<Bullet>();
    private float fireReadyAt;
    private float nextEnemyShot;
    private float nextSnapshot;
    private float time;
    private bool reported;

    // ---- View ----
    private RectTransform field;
    private Image playerImage;
    private Image[] enemyImages;
    private Image bossImage;
    private RectTransform bossBar;
    private RectTransform bossBarFill;
    private readonly List<Image> bulletImages = new List<Image>();
    private TextMeshProUGUI livesText;
    private TextMeshProUGUI centerText;
    private TextMeshProUGUI footerText;
    private HoldButton leftButton;
    private HoldButton rightButton;
    private HoldButton fireButton;

    public override bool SupportsAutoPlay => true;

    private float HalfW => fieldSize.x * 0.5f;
    private float HalfH => fieldSize.y * 0.5f;
    private float PlayerY => -HalfH + playerSize.y * 0.5f + 12f;
    private int EnemyCount => Rows * columns;

    // =========================================================
    // SETUP
    // =========================================================

    protected override void OnBegin()
    {
        livesLeft = lives;
        bossHp = bossHealth;
        alive = new bool[EnemyCount];

        for (int i = 0; i < alive.Length; i++)
            alive[i] = true;

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

        Image panel = UIFactory.CreatePanel(transform, "Panel", new Color(0.06f, 0.05f, 0.12f, 0.98f));
        UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(fieldSize.x + 40f, fieldSize.y + 190f));

        string title = Definition != null ? Definition.title : "Misión";
        string header = IsLocalPlayer ? $"TU MISIÓN · {title}" : $"MISIÓN DE {CharacterName.ToUpper()} · {title}";
        TextMeshProUGUI headerText = UIFactory.CreateText(panel.transform, "Header", header, 26f);
        UIFactory.Place(headerText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(fieldSize.x, 36f));
        headerText.color = UIFactory.AccentColor;
        headerText.fontStyle = FontStyles.Bold;

        livesText = UIFactory.CreateText(panel.transform, "Lives", "", 22f, TextAlignmentOptions.Left);
        UIFactory.Place(livesText.rectTransform, new Vector2(0f, 1f), new Vector2(24f, -46f), new Vector2(400f, 30f));

        // Play field (local coordinates centred on its middle).
        Image fieldImage = UIFactory.CreatePanel(panel.transform, "Field", background != null ? Color.white : new Color(0.02f, 0.02f, 0.06f, 1f));
        fieldImage.sprite = background;
        fieldImage.raycastTarget = false;
        field = UIFactory.Place(fieldImage.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -80f), fieldSize);
        field.gameObject.AddComponent<RectMask2D>();

        enemyImages = new Image[EnemyCount];

        for (int i = 0; i < EnemyCount; i++)
        {
            bool rowA = i / columns == 0;
            enemyImages[i] = CreateSprite("Enemy" + i, rowA ? enemyRowASprite : enemyRowBSprite,
                rowA ? new Color(0.95f, 0.35f, 0.35f) : new Color(0.35f, 0.75f, 1f), enemySize);
        }

        bossImage = CreateSprite("Boss", bossSprite, new Color(0.75f, 0.25f, 0.85f), bossSize);
        playerImage = CreateSprite("Player", playerSprite, new Color(1f, 0.85f, 0.3f), playerSize);

        // Boss health bar (top of the field).
        Image barBack = UIFactory.CreatePanel(field, "BossBar", new Color(0.2f, 0.05f, 0.05f, 0.9f));
        bossBar = UIFactory.Place(barBack.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(fieldSize.x * 0.6f, 14f));
        Image fill = UIFactory.CreatePanel(bossBar, "Fill", new Color(0.95f, 0.2f, 0.3f));
        bossBarFill = fill.rectTransform;
        bossBarFill.anchorMin = Vector2.zero;
        bossBarFill.anchorMax = Vector2.one;
        bossBarFill.pivot = new Vector2(0f, 0.5f);
        bossBarFill.offsetMin = Vector2.zero;
        bossBarFill.offsetMax = Vector2.zero;

        centerText = UIFactory.CreateText(field, "Center", "", 40f);
        UIFactory.Stretch(centerText.rectTransform, 20f);
        centerText.fontStyle = FontStyles.Bold;

        footerText = UIFactory.CreateText(panel.transform, "Footer", "", 18f);
        UIFactory.Place(footerText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(fieldSize.x, 80f));
        footerText.color = new Color(0.8f, 0.8f, 0.8f);

        if (IsLocalPlayer)
        {
            footerText.text = ResponsiveCanvas.IsTouchDevice ? "" : "Mover: A/D o flechas · Disparar: Espacio";
            leftButton = CreateHoldButton(panel.transform, "Left", "◄", new Vector2(0f, 0f), new Vector2(20f, 14f), new Vector2(130f, 80f));
            rightButton = CreateHoldButton(panel.transform, "Right", "►", new Vector2(0f, 0f), new Vector2(165f, 14f), new Vector2(130f, 80f));
            fireButton = CreateHoldButton(panel.transform, "Fire", "DISPARAR", new Vector2(1f, 0f), new Vector2(-20f, 14f), new Vector2(220f, 80f));
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

    private static HoldButton CreateHoldButton(Transform parent, string name, string label, Vector2 anchor, Vector2 position, Vector2 size)
    {
        Button button = UIFactory.CreateButton(parent, name, label, null, UIFactory.ButtonColor, 30f);
        UIFactory.Place((RectTransform)button.transform, anchor, position, size);
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
                    SetStage(Stage.Rows);
                break;

            case Stage.Rows:
            case Stage.Boss:
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
            Debug.Log($"[Invaders] {next} a los {time:0.0}s · vidas {livesLeft} · enemigos vivos {CountAlive()} · jefe {bossHp}/{bossHealth}");

        if (next == Stage.Boss)
        {
            bossX = 0f;
            bossY = HalfH - bossSize.y * 0.5f - 30f;
            bossHp = bossHealth;
            nextEnemyShot = time + 1f;
        }
    }

    private void Simulate(float dt)
    {
        ReadInput(out float move, out bool fire);

        playerX = Mathf.Clamp(playerX + move * playerSpeed * dt, -HalfW + playerSize.x * 0.5f, HalfW - playerSize.x * 0.5f);

        if (fire && time >= fireReadyAt && CountBullets(true) < 3)
        {
            fireReadyAt = time + fireCooldown;
            AddBullet(true, new Vector2(playerX, PlayerY + playerSize.y * 0.5f));
        }

        if (stage == Stage.Rows)
            SimulateRows(dt);
        else
            SimulateBoss(dt);

        SimulateBullets(dt);
    }

    private void ReadInput(out float move, out bool fire)
    {
        move = 0f;
        fire = false;

        if (AutoPlay)
        {
            float target = stage == Stage.Boss ? bossX : NearestEnemyX();
            move = Mathf.Clamp((target - playerX) / 40f, -1f, 1f);
            fire = true;
            return;
        }

        Keyboard keyboard = Keyboard.current;

        if (keyboard != null)
        {
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) move -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) move += 1f;
            fire |= keyboard.spaceKey.isPressed || keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed;
        }

        if (leftButton != null && leftButton.IsHeld) move -= 1f;
        if (rightButton != null && rightButton.IsHeld) move += 1f;
        if (fireButton != null && fireButton.IsHeld) fire = true;

        move = Mathf.Clamp(move, -1f, 1f);
    }

    private float NearestEnemyX()
    {
        float best = 0f;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < alive.Length; i++)
        {
            if (!alive[i])
                continue;

            float x = EnemyPosition(i).x;
            float distance = Mathf.Abs(x - playerX);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = x;
            }
        }

        return best;
    }

    private void SimulateRows(float dt)
    {
        int remaining = 0;

        foreach (bool a in alive)
            if (a) remaining++;

        if (remaining == 0)
        {
            bullets.RemoveAll(b => !b.FromPlayer);
            SetStage(Stage.Boss);
            return;
        }

        // Formation: sideways, one step down at each edge, faster as enemies fall.
        float speed = formationSpeed + formationSpeedBonus * (1f - remaining / (float)EnemyCount);
        formationOffset.x += formationDirection * speed * dt;

        GetFormationBounds(out float minX, out float maxX, out float minY);

        if (maxX > HalfW - 10f || minX < -HalfW + 10f)
        {
            formationOffset.x -= formationDirection * speed * dt;
            formationDirection = -formationDirection;
            formationOffset.y -= formationStepDown;
        }

        // The enemies reached the student: mission failed.
        if (minY - enemySize.y * 0.5f <= PlayerY + playerSize.y * 0.5f)
        {
            livesLeft = 0;
            SetStage(Stage.Lost);
            return;
        }

        if (time >= nextEnemyShot)
        {
            nextEnemyShot = time + enemyFireInterval * Random.Range(0.6f, 1.3f);
            int shooter = RandomBottomEnemy();

            if (shooter >= 0)
                AddBullet(false, EnemyPosition(shooter) - new Vector2(0f, enemySize.y * 0.5f));
        }
    }

    private void SimulateBoss(float dt)
    {
        float speed = bossSpeed * (bossHp <= bossHealth / 2 ? 1.6f : 1f);
        bossX += bossDirection * speed * dt;

        float limit = HalfW - bossSize.x * 0.5f - 10f;

        if (Mathf.Abs(bossX) > limit)
        {
            bossX = Mathf.Clamp(bossX, -limit, limit);
            bossDirection = -bossDirection;
        }

        if (time >= nextEnemyShot)
        {
            float interval = bossFireInterval * (bossHp <= bossHealth / 2 ? bossEnragedFireFactor : 1f);
            nextEnemyShot = time + interval;
            Vector2 mouth = new Vector2(bossX, bossY - bossSize.y * 0.5f);
            AddBullet(false, mouth);
            AddBullet(false, mouth + new Vector2(-bossSize.x * 0.35f, 10f));
            AddBullet(false, mouth + new Vector2(bossSize.x * 0.35f, 10f));
        }
    }

    private void SimulateBullets(float dt)
    {
        Rect playerRect = CenteredRect(new Vector2(playerX, PlayerY), playerSize * 0.8f);

        for (int i = bullets.Count - 1; i >= 0; i--)
        {
            Bullet b = bullets[i];
            b.Position.y += (b.FromPlayer ? playerBulletSpeed : -enemyBulletSpeed) * dt;
            bullets[i] = b;

            if (Mathf.Abs(b.Position.y) > HalfH + 30f)
            {
                bullets.RemoveAt(i);
                continue;
            }

            Rect bulletRect = CenteredRect(b.Position, b.FromPlayer ? playerBulletSize : enemyBulletSize);

            if (b.FromPlayer)
            {
                if (HitEnemy(bulletRect))
                    bullets.RemoveAt(i);
            }
            else if (bulletRect.Overlaps(playerRect))
            {
                bullets.RemoveAt(i);

                if (time >= invulnerableUntil)
                {
                    livesLeft--;
                    invulnerableUntil = time + invulnerableSeconds;

                    if (livesLeft <= 0)
                    {
                        SetStage(Stage.Lost);
                        return;
                    }
                }
            }
        }
    }

    private bool HitEnemy(Rect bulletRect)
    {
        if (stage == Stage.Rows)
        {
            for (int e = 0; e < alive.Length; e++)
            {
                if (alive[e] && bulletRect.Overlaps(CenteredRect(EnemyPosition(e), enemySize)))
                {
                    alive[e] = false;
                    return true;
                }
            }

            return false;
        }

        if (stage == Stage.Boss && bulletRect.Overlaps(CenteredRect(new Vector2(bossX, bossY), bossSize * 0.85f)))
        {
            bossHp--;

            if (bossHp <= 0)
            {
                bossHp = 0;
                bullets.Clear();
                SetStage(Stage.Won);
            }

            return true;
        }

        return false;
    }

    private int RandomBottomEnemy()
    {
        List<int> candidates = new List<int>();

        for (int c = 0; c < columns; c++)
        {
            for (int r = Rows - 1; r >= 0; r--)
            {
                int index = r * columns + c;

                if (alive[index])
                {
                    candidates.Add(index);
                    break;
                }
            }
        }

        return candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : -1;
    }

    private void GetFormationBounds(out float minX, out float maxX, out float minY)
    {
        minX = float.MaxValue;
        maxX = float.MinValue;
        minY = float.MaxValue;

        for (int i = 0; i < alive.Length; i++)
        {
            if (!alive[i])
                continue;

            Vector2 p = EnemyPosition(i);
            minX = Mathf.Min(minX, p.x - enemySize.x * 0.5f);
            maxX = Mathf.Max(maxX, p.x + enemySize.x * 0.5f);
            minY = Mathf.Min(minY, p.y);
        }
    }

    private Vector2 EnemyPosition(int index)
    {
        int row = index / columns;
        int column = index % columns;
        float x = (column - (columns - 1) * 0.5f) * enemySpacing.x;
        float y = HalfH - 60f - row * enemySpacing.y;
        return new Vector2(x, y) + formationOffset;
    }

    private void AddBullet(bool fromPlayer, Vector2 position)
    {
        if (bullets.Count < MaxBullets)
            bullets.Add(new Bullet { FromPlayer = fromPlayer, Position = position });
    }

    private int CountBullets(bool fromPlayer)
    {
        int count = 0;

        foreach (Bullet b in bullets)
            if (b.FromPlayer == fromPlayer) count++;

        return count;
    }

    private static Rect CenteredRect(Vector2 center, Vector2 size)
    {
        return new Rect(center - size * 0.5f, size);
    }

    // =========================================================
    // RENDER (same on the player and the spectators)
    // =========================================================

    private void Render()
    {
        bool blink = time < invulnerableUntil && Mathf.Repeat(time * 10f, 1f) < 0.5f;
        playerImage.rectTransform.anchoredPosition = new Vector2(playerX, PlayerY);
        playerImage.enabled = stage != Stage.Lost && !blink;

        bool rows = stage == Stage.Intro || stage == Stage.Rows;

        for (int i = 0; i < enemyImages.Length; i++)
        {
            enemyImages[i].enabled = rows && alive[i];
            enemyImages[i].rectTransform.anchoredPosition = EnemyPosition(i);
        }

        bool boss = stage == Stage.Boss || (stage == Stage.Lost && bossHp < bossHealth && AllRowsCleared());
        bossImage.enabled = boss;
        bossImage.rectTransform.anchoredPosition = new Vector2(bossX, bossY);
        bossBar.gameObject.SetActive(boss);
        bossBarFill.anchorMax = new Vector2(bossHp / (float)bossHealth, 1f);

        for (int i = 0; i < bullets.Count; i++)
        {
            if (i >= bulletImages.Count)
                bulletImages.Add(CreateSprite("Bullet" + i, null, Color.white, playerBulletSize));

            Image image = bulletImages[i];
            Bullet b = bullets[i];
            image.enabled = true;
            image.sprite = b.FromPlayer ? playerBulletSprite : enemyBulletSprite;
            image.color = image.sprite != null ? Color.white : (b.FromPlayer ? new Color(1f, 0.95f, 0.5f) : new Color(1f, 0.35f, 0.25f));
            image.rectTransform.sizeDelta = b.FromPlayer ? playerBulletSize : enemyBulletSize;
            image.rectTransform.anchoredPosition = b.Position;
        }

        for (int i = bullets.Count; i < bulletImages.Count; i++)
            bulletImages[i].enabled = false;

        livesText.text = "Vidas: <color=#FF4060>" + new string('♥', Mathf.Max(0, livesLeft)) + "</color>";
        centerText.text = CenterMessage();
    }

    private bool AllRowsCleared() => CountAlive() == 0;

    private int CountAlive()
    {
        int count = 0;

        foreach (bool a in alive)
            if (a) count++;

        return count;
    }

    private string CenterMessage()
    {
        switch (stage)
        {
            case Stage.Intro:
                return IsLocalPlayer
                    ? "¡Prepárate!\n<size=60%>Destruye las dos filas y luego al jefe</size>"
                    : $"{CharacterName} se prepara...\n<size=60%>Debe destruir dos filas y luego al jefe</size>";
            case Stage.Boss:
                return stageTime < 1.5f ? "<color=#FF60FF>¡JEFE FINAL!</color>" : "";
            case Stage.Won:
                return IsLocalPlayer ? "<color=#60FF60>¡GANASTE!</color>" : $"<color=#60FF60>¡{CharacterName.ToUpper()} GANÓ!</color>";
            case Stage.Lost:
                return IsLocalPlayer ? "<color=#FF5050>¡TE DESTRUYERON!</color>" : $"<color=#FF5050>¡DESTRUYERON A {CharacterName.ToUpper()}!</color>";
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
                ? "<color=#60FF60>¡MISIÓN COMPLETADA!</color>"
                : $"<color=#60FF60>¡{CharacterName.ToUpper()} COMPLETÓ SU MISIÓN!</color>";
        }
        else
        {
            stage = Stage.Lost;
            centerText.text = IsLocalPlayer
                ? "<color=#FF5050>MISIÓN FALLIDA</color>\n<size=55%>Lo intentarás de nuevo en tu próximo turno</size>"
                : $"<color=#FF5050>{CharacterName.ToUpper()} FALLÓ SU MISIÓN</color>\n<size=55%>Lo reintentará en su próximo turno</size>";
        }

        if (leftButton != null) leftButton.gameObject.SetActive(false);
        if (rightButton != null) rightButton.gameObject.SetActive(false);
        if (fireButton != null) fireButton.gameObject.SetActive(false);
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
            w.Write((short)playerX);
            w.Write((byte)Mathf.Max(0, livesLeft));
            w.Write(invulnerableUntil - time);
            w.Write((short)formationOffset.x);
            w.Write((short)formationOffset.y);

            uint mask = 0;

            for (int i = 0; i < alive.Length && i < 32; i++)
                if (alive[i]) mask |= 1u << i;

            w.Write(mask);
            w.Write((short)bossX);
            w.Write((short)bossY);
            w.Write((byte)bossHp);
            w.Write((byte)bullets.Count);

            foreach (Bullet b in bullets)
            {
                w.Write(b.FromPlayer);
                w.Write((short)b.Position.x);
                w.Write((short)b.Position.y);
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
                playerX = r.ReadInt16();
                livesLeft = r.ReadByte();
                invulnerableUntil = time + r.ReadSingle();
                formationOffset = new Vector2(r.ReadInt16(), r.ReadInt16());

                uint mask = r.ReadUInt32();

                for (int i = 0; i < alive.Length && i < 32; i++)
                    alive[i] = (mask & (1u << i)) != 0;

                bossX = r.ReadInt16();
                bossY = r.ReadInt16();
                bossHp = r.ReadByte();

                int count = r.ReadByte();
                bullets.Clear();

                for (int i = 0; i < count; i++)
                {
                    bool fromPlayer = r.ReadBoolean();
                    bullets.Add(new Bullet { FromPlayer = fromPlayer, Position = new Vector2(r.ReadInt16(), r.ReadInt16()) });
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
