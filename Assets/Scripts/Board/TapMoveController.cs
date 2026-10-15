using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Touch movement (network game): while the local player can move, the cells they can step to
/// next (walls respected, see TapMoveRules) glow on the board; tapping one sends the same one-step
/// request as the keyboard arrows. Recomputed after every step the Host confirms.
///   - A tap is a short press without dragging and without a second finger (pinch = camera zoom).
///   - Taps on the HUD are ignored. Taps outside the glowing cells do nothing.
///   - While a step is on its way to the Host the glow is hidden (no double steps).
/// Picking uses the board plane (y = 0) and the grid maths, so it works the same on the procedural
/// maze and on the prefab rooms, whatever their models look like.
/// </summary>
public class TapMoveController : MonoBehaviour
{
    private const float MaxTapSeconds = 0.35f;
    private const float MaxTapPixels = 22f;
    private const float PendingTimeout = 1.5f;
    private const float HighlightHeight = 0.09f;

    private static readonly Color HighlightColor = new Color(0.35f, 1f, 0.45f, 1f);
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private readonly List<Vector2Int> targets = new List<Vector2Int>(4);
    private readonly List<Renderer> highlights = new List<Renderer>(4);
    private MaterialPropertyBlock block;
    private Material material;

    private float tileSize = 2f;
    private bool pending;
    private float pendingSince;
    private Vector2Int pendingFrom;
    private int pendingMoves;

    private bool pressing;
    private bool multiTouch;
    private float pressTime;
    private Vector2 pressPosition;

    public IReadOnlyList<Vector2Int> Targets => targets;
    public bool IsActive => targets.Count > 0 && !pending;

    private void Start()
    {
        MapGenerator map = FindFirstObjectByType<MapGenerator>();

        if (map != null)
            tileSize = map.tileSize;

        FxConfig fx = FxConfig.Instance;

        if (fx != null && fx.tubeMaterial != null)
        {
            // Own copy (the tube material is shared with the Backrooms lights), drawn after the props.
            material = new Material(fx.tubeMaterial) { renderQueue = 3100 };
        }
    }

    private void OnDestroy()
    {
        if (material != null)
            Destroy(material);
    }

    private void Update()
    {
        RefreshTargets();
        DrawHighlights();
        ReadTap();
    }

    // =========================================================
    // TARGETS
    // =========================================================

    private void RefreshTargets()
    {
        targets.Clear();

        TurnManager turns = TurnManager.Instance;
        PlayerNetworkData local = PlayerNetworkData.Local;
        BoardManager board = GameStateManager.Instance != null ? GameStateManager.Instance.Board : null;

        if (turns == null || local == null || board == null || !turns.CanLocalPlayerMove())
        {
            pending = false;
            return;
        }

        Vector2Int cell = local.GridPosition.Value;

        // Waiting for the Host to confirm the last step (position or moves changed) or to reject it.
        if (pending)
        {
            bool confirmed = cell != pendingFrom || turns.MovesRemaining.Value != pendingMoves;

            if (!confirmed && Time.unscaledTime - pendingSince < PendingTimeout)
                return;

            pending = false;
        }

        // Same checks as the Host (BoardManager.GetNeighbor / GetTile in TurnManager).
        List<Vector2Int> found = TapMoveRules.StepTargets(cell, turns.WallWalk.Value,
            (from, d) => board.GetNeighbor(board.GetTile(from), d) is Tile t && t.walkable,
            c => board.GetTile(c) is Tile t && t.walkable);

        targets.AddRange(found);
    }

    // =========================================================
    // INPUT
    // =========================================================

    private void ReadTap()
    {
        Touchscreen touch = Touchscreen.current;

        if (touch != null && touch.touches.Count > 0 && AnyTouch(touch))
        {
            int active = 0;
            Vector2 position = Vector2.zero;

            foreach (var t in touch.touches)
            {
                if (!t.press.isPressed)
                    continue;

                active++;
                position = t.position.ReadValue();
            }

            if (active >= 2)
                multiTouch = true;

            if (active >= 1 && !pressing)
                Begin(position);
            else if (active >= 1)
                Track(position);
            else if (pressing)
                End(touch.primaryTouch.position.ReadValue());

            return;
        }

        Mouse mouse = Mouse.current;

        if (mouse == null)
            return;

        Vector2 mousePosition = mouse.position.ReadValue();

        if (mouse.leftButton.wasPressedThisFrame)
            Begin(mousePosition);
        else if (mouse.leftButton.isPressed && pressing)
            Track(mousePosition);
        else if (mouse.leftButton.wasReleasedThisFrame && pressing)
            End(mousePosition);
    }

    private static bool AnyTouch(Touchscreen touch)
    {
        foreach (var t in touch.touches)
        {
            if (t.press.isPressed || t.press.wasReleasedThisFrame)
                return true;
        }

        return false;
    }

    private void Begin(Vector2 position)
    {
        pressing = true;
        multiTouch = false;
        pressTime = Time.unscaledTime;
        pressPosition = position;
    }

    private void Track(Vector2 position)
    {
        // Dragging (camera) cancels the tap.
        if (Vector2.Distance(position, pressPosition) > MaxTapPixels)
            multiTouch = true;
    }

    private void End(Vector2 position)
    {
        pressing = false;

        if (multiTouch || Time.unscaledTime - pressTime > MaxTapSeconds)
            return;

        TapScreen(position);
    }

    /// <summary>Handles a tap at a screen position (also used by the test bot). True if a step was sent.</summary>
    public bool TapScreen(Vector2 screenPosition)
    {
        if (!IsActive || IsOverUI(screenPosition))
            return false;

        Camera cam = Camera.main;

        if (cam == null)
            return false;

        Ray ray = cam.ScreenPointToRay(screenPosition);
        Plane board = new Plane(Vector3.up, Vector3.zero);

        if (!board.Raycast(ray, out float distance))
            return false;

        Vector3 hit = ray.GetPoint(distance);
        Vector2Int? cell = TapMoveRules.PickTarget(new Vector2(hit.x, hit.z), targets, tileSize);

        if (cell == null)
            return false;

        PlayerNetworkData local = PlayerNetworkData.Local;
        Vector2Int direction = cell.Value - local.GridPosition.Value;

        pending = true;
        pendingSince = Time.unscaledTime;
        pendingFrom = local.GridPosition.Value;
        pendingMoves = TurnManager.Instance.MovesRemaining.Value;
        targets.Clear();
        DrawHighlights();

        TurnManager.Instance.RequestMoveFromLocalPlayer(direction);
        return true;
    }

    /// <summary>Screen point of the centre of a cell (test bot).</summary>
    public Vector2 CellToScreen(Vector2Int cell)
    {
        Camera cam = Camera.main;
        return cam != null ? (Vector2)cam.WorldToScreenPoint(new Vector3(cell.x * tileSize, 0f, cell.y * tileSize)) : Vector2.zero;
    }

    private static readonly List<RaycastResult> uiHits = new List<RaycastResult>();

    private static bool IsOverUI(Vector2 screenPosition)
    {
        EventSystem system = EventSystem.current;

        if (system == null)
            return false;

        PointerEventData data = new PointerEventData(system) { position = screenPosition };
        uiHits.Clear();
        system.RaycastAll(data, uiHits);
        return uiHits.Count > 0;
    }

    // =========================================================
    // HIGHLIGHTS
    // =========================================================

    private void DrawHighlights()
    {
        if (material == null)
            return;

        while (highlights.Count < targets.Count)
            highlights.Add(CreateHighlight());

        float pulse = 0.55f + Mathf.Sin(Time.unscaledTime * 5f) * 0.2f;

        for (int i = 0; i < highlights.Count; i++)
        {
            Renderer r = highlights[i];
            bool on = i < targets.Count;
            r.gameObject.SetActive(on);

            if (!on)
                continue;

            Vector2Int cell = targets[i];
            r.transform.position = new Vector3(cell.x * tileSize, HighlightHeight, cell.y * tileSize);
            r.transform.localScale = Vector3.one * tileSize * (0.82f + Mathf.Sin(Time.unscaledTime * 5f) * 0.03f);

            if (block == null)
                block = new MaterialPropertyBlock();

            r.GetPropertyBlock(block);
            block.SetColor(BaseColorId, new Color(HighlightColor.r, HighlightColor.g, HighlightColor.b, pulse));
            r.SetPropertyBlock(block);
        }
    }

    private Renderer CreateHighlight()
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "MoveTarget";
        Destroy(quad.GetComponent<Collider>());
        quad.transform.SetParent(transform, false);
        quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        Renderer renderer = quad.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }
}
