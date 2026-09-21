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

    private void Update()
    {
        if (boardManager == null)
            return;

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

        currentTile =
            targetTile;

        remainingMoves--;

        StopAllCoroutines();

        StartCoroutine(
            MoveRoutine(
                targetTile.transform.position
            )
        );

        Debug.Log(
            "Movimientos restantes: " +
            remainingMoves
        );
    }

    // =========================================================
    // MOVIMIENTO SUAVE
    // =========================================================

    private System.Collections.IEnumerator MoveRoutine(
        Vector3 targetPosition)
    {
        isMoving = true;

        while (
            Vector3.Distance(
                transform.position,
                targetPosition
            ) > 0.01f)
        {
            transform.position =
                Vector3.MoveTowards(
                    transform.position,
                    targetPosition,
                    moveSpeed *
                    Time.deltaTime
                );

            yield return null;
        }

        transform.position =
            targetPosition;

        isMoving = false;
    }
}