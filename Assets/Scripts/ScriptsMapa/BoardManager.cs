using TMPro;
using System.Collections.Generic;
using UnityEngine;

public class BoardManager : MonoBehaviour
{
    [Header("Board")]
    public MapGenerator mapGenerator;

    [Header("Player")]
    public GameObject playerPrefab;

    [Header("Movement")]
    public int minDiceValue = 1;
    public int maxDiceValue = 6;

    [Header("UI")]
    public TextMeshProUGUI gameInfoText;

    private List<Tile> allTiles =
        new List<Tile>();

    private PlayerController player;

    private void Start()
    {
        InitializeBoard();
        SpawnPlayer();

        UpdateGameInfo();
    }

    // =========================================================
    // INICIALIZAR TABLERO
    // =========================================================

    private void InitializeBoard()
    {
        allTiles.Clear();

        Tile[] tiles =
            mapGenerator.GetComponentsInChildren<Tile>();

        foreach (Tile tile in tiles)
        {
            if (tile.walkable)
            {
                allTiles.Add(tile);
            }
        }

        Debug.Log(
            "BoardManager: " +
            allTiles.Count +
            " casillas registradas."
        );
    }

    // =========================================================
    // CREAR JUGADOR
    // =========================================================

    private void SpawnPlayer()
    {
        if (playerPrefab == null)
        {
            Debug.LogError(
                "BoardManager: No hay Player Prefab asignado."
            );

            return;
        }

        Tile startTile =
            FindStartingTile();

        if (startTile == null)
        {
            Debug.LogError(
                "BoardManager: No se encontró casilla inicial."
            );

            return;
        }

        GameObject playerObject =
            Instantiate(
                playerPrefab,
                startTile.transform.position,
                Quaternion.identity
            );

        player =
            playerObject.GetComponent<PlayerController>();

        if (player == null)
        {
            Debug.LogError(
                "BoardManager: El Player Prefab no tiene PlayerController."
            );

            return;
        }

        player.Initialize(
            startTile,
            this
        );
    }

    // =========================================================
    // CASILLA INICIAL
    // =========================================================

    private Tile FindStartingTile()
    {
        int centerX =
            mapGenerator.width / 2;

        int centerY =
            mapGenerator.height / 2;

        foreach (Tile tile in allTiles)
        {
            if (tile.gridPosition.x == centerX &&
                tile.gridPosition.y == centerY)
            {
                return tile;
            }
        }

        return null;
    }

    // =========================================================
    // DADO
    // =========================================================

    public void RollDice()
    {
        if (player == null)
            return;

        if (player.remainingMoves > 0)
        {
            Debug.Log(
                "Todavía tienes " +
                player.remainingMoves +
                " movimientos."
            );

            return;
        }

        int result =
            Random.Range(
                minDiceValue,
                maxDiceValue + 1
            );

        player.SetMoves(result);

        UpdateGameInfo(result);

        Debug.Log(
            "Dado: " +
            result
        );
    }

    // =========================================================
    // BUSCAR VECINO
    // =========================================================

    public Tile GetNeighbor(
        Tile currentTile,
        Vector2Int direction)
    {
        if (currentTile == null)
            return null;

        if (currentTile.HasWall(direction))
            return null;

        Vector3 targetPosition =
            currentTile.transform.position +
            new Vector3(
                direction.x,
                0f,
                direction.y
            ) *
            mapGenerator.tileSize;

        Tile closestTile = null;

        float closestDistance =
            float.MaxValue;

        foreach (Tile tile in allTiles)
        {
            if (tile == currentTile)
                continue;

            float distance =
                Vector3.Distance(
                    tile.transform.position,
                    targetPosition
                );

            if (distance < 0.01f &&
                distance < closestDistance)
            {
                closestTile = tile;
                closestDistance = distance;
            }
        }

        return closestTile;
    }

    // =========================================================
    // MOVIMIENTO
    // =========================================================

    public bool TryMove(
        Vector2Int direction)
    {
        if (player == null)
            return false;

        if (player.remainingMoves <= 0)
        {
            Debug.Log(
                "No tienes movimientos restantes."
            );

            return false;
        }

        Tile currentTile =
            player.currentTile;

        Tile nextTile =
            GetNeighbor(
                currentTile,
                direction
            );

        if (nextTile == null)
        {
            Debug.Log(
                "Movimiento bloqueado por una pared."
            );

            return false;
        }

        if (!nextTile.walkable)
            return false;

        player.MoveToTile(
            nextTile
        );

        UpdateGameInfo();

        return true;
    }

    // =========================================================
    // UI
    // =========================================================

    public void UpdateGameInfo()
    {
        if (gameInfoText == null)
            return;

        if (player == null)
        {
            gameInfoText.text =
                "Dado: -\n" +
                "Movimientos restantes: 0";

            return;
        }

        gameInfoText.text =
            "Dado: -\n" +
            "Movimientos restantes: " +
            player.remainingMoves;
    }

    public void UpdateGameInfo(int diceResult)
    {
        if (gameInfoText == null)
            return;

        gameInfoText.text =
            "Dado: " +
            diceResult +
            "\n" +
            "Movimientos restantes: " +
            player.remainingMoves;
    }
}