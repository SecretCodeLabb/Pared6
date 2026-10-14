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

    // Network mode (T05/T06): board is built from the Host seed and players are
    // spawned by GameStateManager, so the legacy local spawn is skipped.
    public bool useNetwork { get; private set; }

    private readonly Dictionary<Vector2Int, Tile> tilesByCell =
        new Dictionary<Vector2Int, Tile>();

    public bool IsInitialized { get; private set; }

    private void Awake()
    {
        useNetwork = NetworkGameMode.IsActive;
    }

    private void Start()
    {
        if (useNetwork)
            return;

        InitializeBoard();
        SpawnPlayer();

        UpdateGameInfo();
    }

    // =========================================================
    // INICIALIZAR TABLERO
    // =========================================================

    public void InitializeBoard()
    {
        allTiles.Clear();
        tilesByCell.Clear();

        Tile[] tiles =
            mapGenerator.GetComponentsInChildren<Tile>();

        foreach (Tile tile in tiles)
        {
            if (tile.walkable)
            {
                allTiles.Add(tile);
                tilesByCell[CellOf(tile)] = tile;
            }
        }

        IsInitialized = true;

        Debug.Log(
            "BoardManager: " +
            allTiles.Count +
            " casillas registradas."
        );
    }

    // =========================================================
    // CELDAS LÓGICAS (RED)
    // =========================================================

    /// <summary>Logical, network-safe tile key: the tile's global grid coordinate.</summary>
    public static Vector2Int CellOf(Tile tile)
    {
        return tile.gridPosition;
    }

    public Tile GetTile(Vector2Int cell)
    {
        tilesByCell.TryGetValue(cell, out Tile tile);
        return tile;
    }

    public bool IsCentralTile(Tile tile)
    {
        return tile != null && tile.region == BoardRegion.Center;
    }

    public List<Tile> GetAllTiles()
    {
        return new List<Tile>(allTiles);
    }

    public List<Tile> GetCentralTiles()
    {
        List<Tile> result = new List<Tile>();

        foreach (Tile tile in allTiles)
        {
            if (IsCentralTile(tile))
                result.Add(tile);
        }

        return result;
    }

    /// <summary>Distinct start tiles: the central 2x2 block around the board centre (up to 4 players).</summary>
    public List<Tile> GetStartingTiles(int count)
    {
        int cx = mapGenerator.width / 2;
        int cy = mapGenerator.height / 2;

        Vector2Int[] grid =
        {
            new Vector2Int(cx, cy),
            new Vector2Int(cx - 1, cy),
            new Vector2Int(cx, cy - 1),
            new Vector2Int(cx - 1, cy - 1)
        };

        List<Tile> result = new List<Tile>();

        for (int i = 0; i < count && i < grid.Length; i++)
        {
            foreach (Tile tile in allTiles)
            {
                if (IsCentralTile(tile) && tile.gridPosition == grid[i])
                {
                    result.Add(tile);
                    break;
                }
            }
        }

        return result;
    }

    public bool HasAnyValidMove(Tile tile)
    {
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        foreach (Vector2Int direction in directions)
        {
            Tile next = GetNeighbor(tile, direction);

            if (next != null && next.walkable)
                return true;
        }

        return false;
    }

    /// <summary>Deterministic FNV-1a hash of walls + event cells, to compare Host and Client boards.</summary>
    public int ComputeBoardHash(IEnumerable<Vector2Int> eventCells)
    {
        List<Vector2Int> cells = new List<Vector2Int>(tilesByCell.Keys);
        cells.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));

        unchecked
        {
            uint hash = 2166136261;

            void Mix(int value)
            {
                hash = (hash ^ (uint)value) * 16777619;
            }

            foreach (Vector2Int cell in cells)
            {
                Tile tile = tilesByCell[cell];
                Mix(cell.x);
                Mix(cell.y);
                Mix((tile.northWall ? 1 : 0) | (tile.southWall ? 2 : 0) | (tile.eastWall ? 4 : 0) | (tile.westWall ? 8 : 0));
            }

            List<Vector2Int> events = new List<Vector2Int>(eventCells);
            events.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));

            foreach (Vector2Int cell in events)
            {
                Mix(cell.x * 1000 + cell.y);
            }

            return (int)hash;
        }
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

        // Offline: the pawn shows the first character picked in the local selection.
        if (GameSession.Instance != null && GameSession.Instance.Players.Count > 0 &&
            GameSession.Instance.Players[0].characterData != null)
        {
            PawnLabel.AddTo(playerObject).SetText("<b>" + GameSession.Instance.Players[0].characterData.characterName + "</b>");
        }
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
        // Network mode: the existing roll button becomes a request to the Host (T08).
        if (useNetwork)
        {
            if (TurnManager.Instance != null)
                TurnManager.Instance.RequestRollFromLocalPlayer();

            return;
        }

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

        // Both tiles of the edge must agree there is no wall (they always do with the
        // shared-wall generator; checking both keeps movement safe if a tile is edited).
        if (currentTile.HasWall(direction))
            return null;

        Tile next = GetTile(currentTile.gridPosition + direction);

        if (next == null || next.HasWall(-direction))
            return null;

        return next;
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