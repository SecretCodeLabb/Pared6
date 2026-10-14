using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Visual feedback on the board (network game). Purely local: it watches the synced state
/// (pawns, lives, turn, combat, missions) every frame and plays effects on each instance, so it
/// never changes the rules or the network traffic.
///   - Steps: hop (PlayerController) + dust puff and small ring on landing.
///   - Turn: pulsing ring in the character's colour under the active pawn + steps left above it.
///   - Danger: red ring under a rival close to the active pawn (a duel may happen).
///   - Duel: clash burst between both pawns + shockwave + camera shake.
///   - Event fight: purple burst + shockwave + shake; won = green sparkles, escaped = blue puff.
///   - Lives: floating "-1 ♥" / "+1 ♥" with red / green bursts; elimination puff.
///   - Mission: golden burst on start, confetti when completed and for the winner.
/// Assets come from Resources/FxConfig (PPOP/Fx/Build Fx Assets). Without them it does nothing.
/// </summary>
public class GameFx : MonoBehaviour
{
    private static readonly Color DangerColor = new Color(1f, 0.25f, 0.2f);
    private static readonly Color EventColor = new Color(0.7f, 0.25f, 1f);
    private static readonly Color GoodColor = new Color(0.35f, 1f, 0.45f);
    private static readonly Color GoldColor = new Color(1f, 0.82f, 0.25f);

    private const int DangerDistance = 2;

    [Tooltip("Global size of the particles (the board is seen from far away).")]
    [SerializeField] private float particleScale = 2f;

    private int activeFloatTexts;

    private class PawnState
    {
        public int Lives = -1;
        public PlayerLifeState LifeState;
        public bool MissionDone;
    }

    private FxConfig config;
    private readonly Dictionary<PlayerNetworkData, PawnState> pawns = new Dictionary<PlayerNetworkData, PawnState>();

    private TurnState lastState = TurnState.WaitingForBoard;
    private CombatPhase lastCombatPhase = CombatPhase.None;
    private MissionPhase lastMissionPhase = MissionPhase.None;
    private bool winnerCelebrated;

    private Transform turnRing;
    private Renderer turnRingRenderer;
    private TextMeshPro stepsText;
    private int shownSteps = -1;
    private float stepsPop;
    private readonly List<Transform> dangerRings = new List<Transform>();

    // =========================================================
    // LIFECYCLE
    // =========================================================

    private void Awake()
    {
        config = FxConfig.Instance;

        if (config == null || config.particleMaterial == null || config.ringMaterial == null)
        {
            Debug.LogWarning("GameFx: falta Resources/FxConfig (PPOP/Fx/Build Fx Assets). Sin efectos visuales.");
            enabled = false;
            return;
        }

        PlayerController.StepLanded += OnStepLanded;
    }

    private void OnDestroy()
    {
        PlayerController.StepLanded -= OnStepLanded;
    }

    private void Update()
    {
        TurnManager turns = TurnManager.Instance;

        if (turns == null || !turns.IsSpawned)
            return;

        WatchPawns();
        WatchTurn(turns);
        WatchCombat();
        WatchMission();
        WatchGameOver(turns);
        UpdateTurnMarker(turns);
        UpdateDanger(turns);
    }

    // =========================================================
    // WATCHERS
    // =========================================================

    private void WatchPawns()
    {
        foreach (PlayerNetworkData p in PlayerNetworkData.All)
        {
            if (p == null)
                continue;

            if (!pawns.TryGetValue(p, out PawnState s))
            {
                pawns[p] = new PawnState { Lives = p.Lives.Value, LifeState = p.State.Value, MissionDone = p.MissionCompleted.Value };
                continue;
            }

            Vector3 at = p.transform.position;

            if (p.Lives.Value < s.Lives)
            {
                int lost = s.Lives - p.Lives.Value;
                Burst(at + Vector3.up, DangerColor, 26, 5f, 0.5f, 0.7f);
                FloatText(at, $"-{lost} ♥", DangerColor, 1.2f);
                BoardCameraController.Shake(p.IsOwner ? 0.6f : 0.3f, 0.35f);
            }
            else if (p.Lives.Value > s.Lives)
            {
                Burst(at + Vector3.up, GoodColor, 22, 3f, 0.45f, 0.9f, -1.5f);
                FloatText(at, $"+{p.Lives.Value - s.Lives} ♥", GoodColor, 1.2f);
            }

            if (p.State.Value == PlayerLifeState.Eliminated && s.LifeState != PlayerLifeState.Eliminated)
            {
                Burst(at + Vector3.up, new Color(0.6f, 0.6f, 0.6f), 40, 4f, 0.9f, 1.2f);
                Shockwave(at, new Color(0.5f, 0.5f, 0.5f), 5f, 0.7f);
                FloatText(at, "ELIMINADO", new Color(0.8f, 0.8f, 0.8f), 1.6f);
            }

            if (p.MissionCompleted.Value && !s.MissionDone)
                Confetti(at, 70);

            s.Lives = p.Lives.Value;
            s.LifeState = p.State.Value;
            s.MissionDone = p.MissionCompleted.Value;
        }
    }

    private void WatchTurn(TurnManager turns)
    {
        TurnState state = turns.State.Value;

        if (state == lastState)
            return;

        PlayerNetworkData current = turns.CurrentPlayer;

        // New turn: a small flash under the pawn.
        if (state == TurnState.WaitingForRoll && current != null)
            Shockwave(current.transform.position, PawnColor(current), 3.5f, 0.6f);

        if (state == TurnState.Roulette && current != null)
            Burst(current.transform.position + Vector3.up * 1.5f, GoldColor, 16, 3f, 0.35f, 0.6f);

        lastState = state;
    }

    private void WatchCombat()
    {
        CombatManager combat = CombatManager.Instance;
        CombatPhase phase = combat != null && combat.IsSpawned && combat.IsActive ? combat.Phase.Value : CombatPhase.None;

        if (phase == lastCombatPhase)
            return;

        PlayerNetworkData fighter = combat != null ? PlayerNetworkData.GetByPlayerId(combat.CombatPlayerId.Value) : null;
        bool started = lastCombatPhase == CombatPhase.None && phase != CombatPhase.None;

        if (started && fighter != null)
        {
            if (combat.IsDuel.Value)
            {
                PlayerNetworkData defender = PlayerNetworkData.GetByPlayerId(combat.DefenderPlayerId.Value);
                Vector3 a = fighter.transform.position;
                Vector3 b = defender != null ? defender.transform.position : a;
                Vector3 middle = (a + b) * 0.5f + Vector3.up * 1.2f;

                Burst(middle, new Color(1f, 0.6f, 0.2f), 50, 7f, 0.5f, 0.6f);
                Burst(middle, Color.white, 20, 9f, 0.3f, 0.35f);
                Shockwave(a, DangerColor, 6f, 0.6f);
                FloatText(middle, "¡DUELO!", new Color(1f, 0.55f, 0.25f), 1.4f, 1.6f);
                BoardCameraController.Shake(0.8f, 0.5f);
            }
            else
            {
                Vector3 at = fighter.transform.position;
                Burst(at + Vector3.up * 0.3f, EventColor, 60, 6f, 0.55f, 0.9f, -3f);
                Shockwave(at, EventColor, 7f, 0.8f);
                FloatText(at, "¡AMENAZA!", new Color(0.85f, 0.5f, 1f), 1.4f, 1.6f);
                BoardCameraController.Shake(0.7f, 0.45f);
            }
        }

        if (fighter != null)
        {
            Vector3 at = fighter.transform.position;

            if (phase == CombatPhase.Won && !combat.IsDuel.Value)
            {
                Burst(at + Vector3.up, GoodColor, 35, 4f, 0.4f, 1f, -2f);
                FloatText(at, "¡SUPERADO!", GoodColor, 1.3f);
            }
            else if (phase == CombatPhase.Escaped)
            {
                Burst(at + Vector3.up, new Color(0.4f, 0.7f, 1f), 35, 4f, 0.6f, 0.8f);
                FloatText(at, "¡ESCAPÓ!", new Color(0.5f, 0.8f, 1f), 1.3f);
            }
            else if (phase == CombatPhase.Draw)
            {
                FloatText(at, "EMPATE", GoldColor, 1.3f);
            }
        }

        lastCombatPhase = phase;
    }

    private void WatchMission()
    {
        MissionManager mission = MissionManager.Instance;
        MissionPhase phase = mission != null && mission.IsSpawned ? mission.Phase.Value : MissionPhase.None;

        if (phase == lastMissionPhase)
            return;

        PlayerNetworkData player = mission != null ? PlayerNetworkData.GetByPlayerId(mission.PlayerId.Value) : null;

        if (player != null && phase == MissionPhase.Active)
        {
            Vector3 at = player.transform.position;
            Burst(at, GoldColor, 60, 5f, 0.5f, 1.1f, -4f);
            Shockwave(at, GoldColor, 6f, 0.8f);
            FloatText(at, "¡MISIÓN!", GoldColor, 1.5f, 1.5f);
        }
        else if (player != null && phase == MissionPhase.Failed)
        {
            FloatText(player.transform.position, "FALLÓ", DangerColor, 1.3f);
        }

        lastMissionPhase = phase;
    }

    private void WatchGameOver(TurnManager turns)
    {
        if (turns.State.Value != TurnState.GameOver)
        {
            winnerCelebrated = false;
            return;
        }

        if (winnerCelebrated || GameStateManager.Instance == null)
            return;

        winnerCelebrated = true;
        PlayerNetworkData winner = PlayerNetworkData.GetByPlayerId(GameStateManager.Instance.WinnerPlayerId.Value);

        if (winner != null)
            StartCoroutine(Celebrate(winner.transform));
    }

    private IEnumerator Celebrate(Transform winner)
    {
        for (int i = 0; i < 4 && winner != null; i++)
        {
            Confetti(winner.position, 50);
            yield return new WaitForSeconds(0.45f);
        }
    }

    private void OnStepLanded(PlayerController pawn)
    {
        if (pawn == null)
            return;

        Vector3 at = pawn.transform.position;
        Burst(at + Vector3.up * 0.1f, new Color(0.85f, 0.8f, 0.65f, 0.7f), 8, 1.6f, 0.35f, 0.45f, 2f);
        Shockwave(at, new Color(1f, 1f, 1f, 0.5f), 1.6f, 0.3f);
    }

    // =========================================================
    // PERSISTENT MARKERS
    // =========================================================

    private void UpdateTurnMarker(TurnManager turns)
    {
        PlayerNetworkData current = turns.CurrentPlayer;
        bool show = current != null && current.IsAlive && turns.State.Value != TurnState.GameOver && turns.State.Value != TurnState.WaitingForBoard;

        if (turnRing == null)
        {
            turnRing = CreateRing("TurnRing", 2.2f).transform;
            turnRingRenderer = turnRing.GetComponent<Renderer>();
            stepsText = CreateWorldText("StepsLeft", 12f);
        }

        turnRing.gameObject.SetActive(show);

        if (!show)
        {
            stepsText.gameObject.SetActive(false);
            return;
        }

        Vector3 at = current.transform.position;
        float pulse = 1f + Mathf.Sin(Time.time * 4f) * 0.08f;
        turnRing.position = new Vector3(at.x, 0.06f, at.z);
        turnRing.localScale = Vector3.one * 2.2f * pulse;
        turnRing.Rotate(Vector3.up, 40f * Time.deltaTime, Space.World);
        SetRingColor(turnRingRenderer, PawnColor(current), 0.75f + Mathf.Sin(Time.time * 4f) * 0.2f);

        // Steps left floating above the active pawn while it moves.
        int steps = turns.State.Value == TurnState.Moving ? turns.MovesRemaining.Value : -1;
        stepsText.gameObject.SetActive(steps > 0);

        if (steps > 0)
        {
            if (steps != shownSteps)
                stepsPop = 1f;

            stepsPop = Mathf.MoveTowards(stepsPop, 0f, Time.deltaTime * 4f);
            stepsText.text = steps.ToString();
            stepsText.color = PawnColor(current);
            stepsText.transform.position = at + Vector3.up * (4.9f + Mathf.Sin(Time.time * 3f) * 0.15f);
            stepsText.transform.localScale = Vector3.one * (1f + stepsPop * 0.5f);
            FaceCamera(stepsText.transform);
        }

        shownSteps = steps;
    }

    private void UpdateDanger(TurnManager turns)
    {
        PlayerNetworkData current = turns.CurrentPlayer;
        int used = 0;

        if (current != null && current.IsAlive && turns.State.Value != TurnState.GameOver && GameRules.Instance.duelsEnabled)
        {
            foreach (PlayerNetworkData p in PlayerNetworkData.All)
            {
                if (p == null || p == current || !p.IsAlive)
                    continue;

                Vector2Int d = p.GridPosition.Value - current.GridPosition.Value;

                if (Mathf.Abs(d.x) + Mathf.Abs(d.y) > DangerDistance)
                    continue;

                if (used >= dangerRings.Count)
                    dangerRings.Add(CreateRing("DangerRing", 2f).transform);

                Transform ring = dangerRings[used++];
                Vector3 at = p.transform.position;
                float pulse = 1f + Mathf.Sin(Time.time * 9f) * 0.12f;
                ring.gameObject.SetActive(true);
                ring.position = new Vector3(at.x, 0.07f, at.z);
                ring.localScale = Vector3.one * 2f * pulse;
                SetRingColor(ring.GetComponent<Renderer>(), DangerColor, 0.6f + Mathf.Sin(Time.time * 9f) * 0.3f);
            }
        }

        for (int i = used; i < dangerRings.Count; i++)
            dangerRings[i].gameObject.SetActive(false);
    }

    // =========================================================
    // EFFECT PRIMITIVES
    // =========================================================

    /// <summary>One-shot particle burst (additive glow).</summary>
    public void Burst(Vector3 position, Color color, int count, float speed, float size, float lifetime, float gravity = 1f)
    {
        GameObject go = new GameObject("FxBurst");
        go.transform.position = position;

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.duration = 0.2f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.6f, lifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f * particleScale, size * particleScale);
        main.startColor = new ParticleSystem.MinMaxGradient(color, Color.Lerp(color, Color.white, 0.4f));
        main.gravityModifier = gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = count;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.3f * particleScale;

        ParticleSystem.SizeOverLifetimeModule sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

        ParticleSystem.ColorOverLifetimeModule colorOverLife = ps.colorOverLifetime;
        colorOverLife.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        colorOverLife.color = fade;

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = config.particleMaterial;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;

        ps.Play();
        Destroy(go, lifetime + 0.5f);
    }

    /// <summary>Multicolour pieces falling around a point (mission / victory).</summary>
    public void Confetti(Vector3 position, int count)
    {
        Color[] colors = { GoldColor, new Color(1f, 0.35f, 0.5f), new Color(0.35f, 0.8f, 1f), GoodColor, new Color(0.8f, 0.45f, 1f) };

        foreach (Color c in colors)
            Burst(position + Vector3.up * 2.5f, c, count / colors.Length, 7f, 0.35f, 1.6f, 1.2f);
    }

    /// <summary>Flat ring on the board that grows and fades.</summary>
    public void Shockwave(Vector3 position, Color color, float maxSize, float duration)
    {
        StartCoroutine(ShockwaveRoutine(position, color, maxSize, duration));
    }

    private IEnumerator ShockwaveRoutine(Vector3 position, Color color, float maxSize, float duration)
    {
        GameObject ring = CreateRing("FxShockwave", 0.5f);
        Renderer renderer = ring.GetComponent<Renderer>();
        ring.transform.position = new Vector3(position.x, 0.08f, position.z);

        for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
        {
            float eased = 1f - (1f - t) * (1f - t);
            ring.transform.localScale = Vector3.one * Mathf.Lerp(0.5f, maxSize, eased);
            SetRingColor(renderer, color, (1f - t) * color.a);
            yield return null;
        }

        Destroy(ring);
    }

    /// <summary>World text that rises and fades above a point.</summary>
    public void FloatText(Vector3 position, string text, Color color, float duration, float scale = 1f)
    {
        StartCoroutine(FloatTextRoutine(position, text, color, duration, scale));
    }

    private IEnumerator FloatTextRoutine(Vector3 position, string value, Color color, float duration, float scale)
    {
        TextMeshPro text = CreateWorldText("FxText", 8f);
        text.text = value;

        // Simultaneous texts (duel + damage + eliminated) stack instead of overlapping.
        Vector3 start = position + Vector3.up * (3.6f + activeFloatTexts * 1.3f * scale);
        activeFloatTexts++;

        for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
        {
            float pop = t < 0.15f ? Mathf.Lerp(0.3f, 1.25f, t / 0.15f) : Mathf.Lerp(1.25f, 1f, Mathf.Clamp01((t - 0.15f) / 0.15f));
            text.transform.position = start + Vector3.up * (t * 1.6f);
            text.transform.localScale = Vector3.one * pop * scale;
            text.color = new Color(color.r, color.g, color.b, t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f);
            FaceCamera(text.transform);
            yield return null;
        }

        activeFloatTexts = Mathf.Max(0, activeFloatTexts - 1);
        Destroy(text.gameObject);
    }

    // =========================================================
    // HELPERS
    // =========================================================

    private GameObject CreateRing(string name, float size)
    {
        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Quad);
        ring.name = name;
        Destroy(ring.GetComponent<Collider>());
        ring.transform.SetParent(transform, false);
        ring.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        ring.transform.localScale = Vector3.one * size;

        Renderer renderer = ring.GetComponent<Renderer>();
        renderer.sharedMaterial = config.ringMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return ring;
    }

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private MaterialPropertyBlock block;

    private void SetRingColor(Renderer renderer, Color color, float alpha)
    {
        if (block == null)
            block = new MaterialPropertyBlock();

        renderer.GetPropertyBlock(block);
        block.SetColor(BaseColorId, new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha)));
        renderer.SetPropertyBlock(block);
    }

    private TextMeshPro CreateWorldText(string name, float fontSize)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);

        TextMeshPro text = go.AddComponent<TextMeshPro>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.outlineWidth = 0.3f;
        text.outlineColor = Color.black;
        return text;
    }

    private static void FaceCamera(Transform target)
    {
        if (Camera.main != null)
            target.rotation = Camera.main.transform.rotation;
    }

    private static Color PawnColor(PlayerNetworkData p)
    {
        CharacterData character = p != null ? p.Character : null;
        Color c = character != null && character.boardColor.a > 0f ? character.boardColor : GoldColor;
        return new Color(c.r, c.g, c.b, 1f);
    }
}
