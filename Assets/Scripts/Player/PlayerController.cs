using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;

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
        while (pendingTargets.Count > 0)
        {
            Vector3 target = pendingTargets.Dequeue();
            isMoving = true;

            while (Vector3.Distance(transform.position, target) > 0.01f)
            {
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    target,
                    moveSpeed * Time.deltaTime
                );

                yield return null;
            }

            transform.position = target;
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

        // The event only triggers when the movement ENDS on the tile (T09),
        // not when passing over it.
        if (remainingMoves == 0 && EventManager.Instance != null)
        {
            EventManager.Instance.CheckTile(currentTile);
        }
    }
}