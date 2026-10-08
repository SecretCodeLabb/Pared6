using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Game camera on the Main Camera of Mapa. Keeps the camera's own angle and field of view and
/// moves it smoothly between:
/// - Overview: the whole board (while loading, at game over, or when toggled with C);
/// - Follow: the active player's pawn (everyone sees the current turn), zoomable with the wheel.
/// MapGenerator calls SetBoardBounds after building the board. Offline it follows the local pawn.
/// </summary>
[RequireComponent(typeof(Camera))]
public class BoardCameraController : MonoBehaviour
{
    [Header("Follow")]
    [Tooltip("Follow the active player by default (C toggles overview).")]
    [SerializeField] private bool followActivePlayer = true;
    [Tooltip("Distance from the followed pawn.")]
    [SerializeField] private float followDistance = 28f;
    [SerializeField] private float minDistance = 12f;
    [Tooltip("Upper zoom limit as a multiple of the overview distance.")]
    [SerializeField] private float maxDistanceFactor = 1.15f;
    [Tooltip("Shift the followed point (world units) so the pawn is not hidden by the bottom HUD.")]
    [SerializeField] private Vector3 followOffset = new Vector3(0f, 0f, -2f);

    [Header("Overview")]
    [Tooltip("Extra space around the board in overview.")]
    [Min(1f)] [SerializeField] private float overviewMargin = 1.2f;

    [Header("Feel")]
    [SerializeField] private float smoothTime = 0.35f;
    [SerializeField] private float zoomStep = 3f;
    [SerializeField] private Key toggleKey = Key.C;

    private Camera cam;
    private bool hasBounds;
    private Vector3 boardCenter;
    private Vector2 boardMin;
    private Vector2 boardMax;
    private float overviewDistance = 40f;

    private Vector3 pivot;
    private Vector3 pivotVelocity;
    private float distance;
    private float distanceVelocity;

    private PlayerController offlinePawn;

    public bool IsFollowing => followActivePlayer;

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    /// <summary>Board extents on the ground plane (world units). Snaps the camera to the overview.</summary>
    public void SetBoardBounds(Vector2 min, Vector2 max)
    {
        boardMin = min;
        boardMax = max;
        boardCenter = new Vector3((min.x + max.x) * 0.5f, 0f, (min.y + max.y) * 0.5f);

        if (cam == null)
            cam = GetComponent<Camera>();

        float halfX = (max.x - min.x) * 0.5f * overviewMargin;
        float halfZ = (max.y - min.y) * 0.5f * overviewMargin;
        float vHalf = cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
        float hHalf = Mathf.Atan(Mathf.Tan(vHalf) * cam.aspect);
        overviewDistance = Mathf.Max(halfZ / Mathf.Tan(vHalf), halfX / Mathf.Tan(hHalf)) + 2f;
        cam.farClipPlane = Mathf.Max(cam.farClipPlane, overviewDistance * 3f);

        hasBounds = true;
        pivot = boardCenter;
        distance = overviewDistance;
        pivotVelocity = Vector3.zero;
        distanceVelocity = 0f;
        Apply();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard != null && keyboard[toggleKey].wasPressedThisFrame)
            followActivePlayer = !followActivePlayer;

        Mouse mouse = Mouse.current;

        if (mouse != null && followActivePlayer)
        {
            float scroll = mouse.scroll.ReadValue().y;

            if (Mathf.Abs(scroll) > 0.01f)
            {
                followDistance -= Mathf.Sign(scroll) * zoomStep;
                followDistance = Mathf.Clamp(followDistance, minDistance, overviewDistance * maxDistanceFactor);
            }
        }
    }

    private void LateUpdate()
    {
        if (!hasBounds)
            return;

        Transform target = followActivePlayer ? ResolveTarget() : null;

        Vector3 desiredPivot = boardCenter;
        float desiredDistance = overviewDistance;

        if (target != null)
        {
            Vector3 p = target.position + followOffset;
            desiredPivot = new Vector3(
                Mathf.Clamp(p.x, boardMin.x, boardMax.x),
                0f,
                Mathf.Clamp(p.z, boardMin.y, boardMax.y));
            desiredDistance = Mathf.Min(followDistance, overviewDistance * maxDistanceFactor);
        }

        pivot = Vector3.SmoothDamp(pivot, desiredPivot, ref pivotVelocity, smoothTime);
        distance = Mathf.SmoothDamp(distance, desiredDistance, ref distanceVelocity, smoothTime);
        Apply();
    }

    private void Apply()
    {
        transform.position = pivot - transform.forward * distance;
    }

    /// <summary>The pawn to follow, or null for the overview.</summary>
    private Transform ResolveTarget()
    {
        if (NetworkGameMode.IsActive)
        {
            TurnManager turns = TurnManager.Instance;

            if (turns == null || !turns.IsSpawned)
                return null;

            TurnState state = turns.State.Value;

            if (state == TurnState.WaitingForBoard || state == TurnState.GameOver)
                return null;

            PlayerNetworkData current = turns.CurrentPlayer;
            return current != null ? current.transform : null;
        }

        if (offlinePawn == null)
            offlinePawn = FindFirstObjectByType<PlayerController>();

        return offlinePawn != null ? offlinePawn.transform : null;
    }
}
