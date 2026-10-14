using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;

    [Header("Step feel (network game)")]
    [Tooltip("Height of the little hop on every step.")]
    public float hopHeight = 0.55f;
    [Tooltip("Minimum seconds per step.")]
    public float minStepSeconds = 0.22f;

    /// <summary>Raised when a pawn lands on a tile after a step (dust / sound effects).</summary>
    public static event System.Action<PlayerController> StepLanded;

    private Vector3 baseScale = Vector3.one;
    private bool hasBaseScale;

    [HideInInspector]
    public Tile currentTile;

    [HideInInspector]
    public int remainingMoves;

    private BoardManager boardManager;

    private bool isMoving;

    // Network mode (T06/T08): position comes from PlayerNetworkData.GridPosition;
    // input is only read for the owner and sent to the Host as a request.
    private PlayerNetworkData networkData;

    private readonly System.Collections.Generic.Queue<Vector3> pendingTargets =
        new System.Collections.Generic.Queue<Vector3>();

    [HideInInspector]
    public Vector3 visualOffset;

    public bool IsAnimating => isMoving || pendingTargets.Count > 0;

    // =========================================================
    // INICIALIZAR
    // =========================================================

    public void Initialize(
        Tile startingTile,
        BoardManager board)
    {
        currentTile = startingTile;
        boardManager = board;

        transform.position =
            currentTile.transform.position;

        remainingMoves = 0;
    }

    // =========================================================
    // ACTUALIZAR
    // =========================================================

    public void InitializeNetwork(
        Tile startingTile,
        BoardManager board,
        PlayerNetworkData data,
        Vector3 offset)
    {
        networkData = data;
        boardManager = board;
        currentTile = startingTile;
        visualOffset = offset;
        remainingMoves = 0;

        pendingTargets.Clear();
        StopAllCoroutines();
        isMoving = false;

        if (hasBaseScale)
            transform.localScale = baseScale;

        if (currentTile != null)
            transform.position = currentTile.transform.position + visualOffset;
    }

    /// <summary>Network mode: animate locally towards a tile already validated by the Host.</summary>
    public void AnimateToTile(Tile targetTile)
    {
        if (targetTile == null)
            return;

        currentTile = targetTile;
        pendingTargets.Enqueue(targetTile.transform.position + visualOffset);

        if (!isMoving)
            StartCoroutine(PlayPendingMoves());
    }

    private System.Collections.IEnumerator PlayPendingMoves()
    {
        if (!hasBaseScale)
        {
            baseScale = transform.localScale;
            hasBaseScale = true;
        }

        while (pendingTargets.Count > 0)
        {
            Vector3 start = transform.position;
            Vector3 target = pendingTargets.Dequeue();
            isMoving = true;

            // Hop from tile to tile: arc on Y, stretch while in the air, squash on landing.
            float duration = Mathf.Max(minStepSeconds, Vector3.Distance(start, target) / Mathf.Max(0.1f, moveSpeed));
            float hop = pendingTargets.Count > 2 ? hopHeight * 0.6f : hopHeight;

            for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
            {
                float arc = Mathf.Sin(t * Mathf.PI);
                transform.position = Vector3.Lerp(start, target, t) + Vector3.up * arc * hop;
                transform.localScale = Vector3.Scale(baseScale, new Vector3(1f - arc * 0.08f, 1f + arc * 0.12f, 1f - arc * 0.08f));
                yield return null;
            }

            transform.position = target;
            StepLanded?.Invoke(this);

            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.12f)
            {
                float squash = Mathf.Sin(t * Mathf.PI) * 0.18f;
                transform.localScale = Vector3.Scale(baseScale, new Vector3(1f + squash, 1f - squash, 1f + squash));
                yield return null;
            }

            transform.localScale = baseScale;
        }

        isMoving = false;
    }

    private void Update()
    {
        if (boardManager == null)
            return;

        if (networkData != null)
        {
            HandleNetworkInput();
            return;
        }

        if (isMoving)
            return;

        HandleInput();
    }

    // =========================================================
    // INPUT - NEW INPUT SYSTEM
    // =========================================================

    private void HandleInput()
    {
        if (Keyboard.current == null)
            return;

        Vector2Int direction =
            Vector2Int.zero;

        if (Keyboard.current.wKey.wasPressedThisFrame ||
            Keyboard.current.upArrowKey.wasPressedThisFrame)
        {
            direction = Vector2Int.up;
        }

        else if (Keyboard.current.sKey.wasPressedThisFrame ||
                 Keyboard.current.downArrowKey.wasPressedThisFrame)
        {
            direction = Vector2Int.down;
        }

        else if (Keyboard.current.dKey.wasPressedThisFrame ||
                 Keyboard.current.rightArrowKey.wasPressedThisFrame)
        {
            direction = Vector2Int.right;
        }

        else if (Keyboard.current.aKey.wasPressedThisFrame ||
                 Keyboard.current.leftArrowKey.wasPressedThisFrame)
        {
            direction = Vector2Int.left;
        }

        if (direction != Vector2Int.zero)
        {
            boardManager.TryMove(direction);
        }
    }

    private void HandleNetworkInput()
    {
        if (!networkData.IsOwner || Keyboard.current == null)
            return;

        TurnManager turns = TurnManager.Instance;

        if (turns == null || !turns.CanLocalPlayerMove())
            return;

        Vector2Int direction = ReadDirection();

        if (direction != Vector2Int.zero)
            turns.RequestMoveFromLocalPlayer(direction);
    }

    private static Vector2Int ReadDirection()
    {
        Keyboard k = Keyboard.current;

        if (k.wKey.wasPressedThisFrame || k.upArrowKey.wasPressedThisFrame)
            return Vector2Int.up;

        if (k.sKey.wasPressedThisFrame || k.downArrowKey.wasPressedThisFrame)
            return Vector2Int.down;

        if (k.dKey.wasPressedThisFrame || k.rightArrowKey.wasPressedThisFrame)
            return Vector2Int.right;

        if (k.aKey.wasPressedThisFrame || k.leftArrowKey.wasPressedThisFrame)
            return Vector2Int.left;

        return Vector2Int.zero;
    }

    // =========================================================
    // DADO
    // =========================================================

    public void SetMoves(int amount)
    {
        remainingMoves = amount;

        Debug.Log(
            "Movimientos disponibles: " +
            remainingMoves
        );
    }

    // =========================================================
    // MOVER
    // =========================================================

    public void MoveToTile(Tile targetTile)
    {
        if (targetTile == null)
            return;

        currentTile = targetTile;
        remainingMoves--;

        StopAllCoroutines();

        StartCoroutine(
            MoveRoutine(targetTile.transform.position)
        );

        Debug.Log("Movimientos restantes: " + remainingMoves);
    }

    // =========================================================
    // MOVIMIENTO SUAVE
    // =========================================================

    private System.Collections.IEnumerator MoveRoutine(Vector3 targetPosition)
    {
        isMoving = true;

        while (Vector3.Distance(transform.position, targetPosition) > 0.01f)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPosition,
                moveSpeed * Time.deltaTime
            );

            yield return null;
        }

        transform.position = targetPosition;

        isMoving = false;

        // Offline: the event triggers when the movement ends on it, or when passing
        // over it if GameRules.eventsTriggerWhenPassing (then the movement stops there).
        bool triggers = remainingMoves == 0 || GameRules.Instance.eventsTriggerWhenPassing;

        if (triggers && EventManager.Instance != null && EventManager.Instance.HasActiveEvent(currentTile))
        {
            remainingMoves = 0;
            EventManager.Instance.CheckTile(currentTile);

            if (boardManager != null)
                boardManager.UpdateGameInfo();
        }
    }
}