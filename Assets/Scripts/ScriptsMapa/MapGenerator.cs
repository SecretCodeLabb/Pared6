using System.Collections.Generic;
using UnityEngine;

public class MapGenerator : MonoBehaviour
{
    [Header("Map Size")]
    public int width = 8;
    public int height = 8;

    [Header("Tile")]
    public GameObject tilePrefab;
    public float tileSize = 2f;

    [Header("Connections")]
    public bool northConnection = true;
    public bool southConnection = true;
    public bool eastConnection = true;
    public bool westConnection = true;

    [Header("Procedural Generation")]
    [Range(0f, 1f)]
    public float extraPathChance = 0.35f;

    [Header("Rooms")]
    public bool generateRooms = true;

    public GameObject northRoomPrefab;
    public GameObject southRoomPrefab;
    public GameObject eastRoomPrefab;
    public GameObject westRoomPrefab;

    [Header("Room Placement")]
    public float roomDistance = 0f;

    [Header("Seed")]
    public bool randomizeSeed = true;
    public int seed;

    private Tile[,] tiles;

    private void Awake()
    {
        if (randomizeSeed)
        {
            seed = Random.Range(0, 999999);
        }

        Random.InitState(seed);

        GenerateMap();
    }

    public void GenerateMap()
    {
        ClearMap();

        if (tilePrefab == null)
        {
            Debug.LogError(
                "MapGenerator: No hay Tile Prefab asignado." 
            );
            return;
        }

        tiles = new Tile[width, height];

        CreateTiles();
        CreateClosedMap();
        CreateMainPaths();
        CreateExtraPaths();
        CreateOuterWalls();
        CreateConnections();
        UpdateTileVisuals();

        if (generateRooms)
        {
            GenerateRooms();
        }

        Debug.Log("Mapa generado. Seed: " + seed);
    }

    // =========================================================
    // MAPA CENTRAL
    // =========================================================

    private void CreateTiles()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3 position = new Vector3(
                    x * tileSize,
                    0,
                    y * tileSize
                );

                GameObject tileObject = Instantiate(
                    tilePrefab,
                    position,
                    Quaternion.identity,
                    transform
                );

                tileObject.name =
                    "Central_Tile_" + x + "_" + y;

                Tile tile =
                    tileObject.GetComponent<Tile>();

                if (tile == null)
                {
                    Debug.LogError(
                        "MapGenerator: El Tile Prefab no tiene Tile.cs."
                    );
                    return;
                }

                tile.Initialize(
                    new Vector2Int(x, y)
                );

                tile.walkable = true;

                tiles[x, y] = tile;
            }
        }
    }

    private void CreateClosedMap()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Tile tile = tiles[x, y];

                tile.northWall = true;
                tile.southWall = true;
                tile.eastWall = true;
                tile.westWall = true;
            }
        }
    }

    private void CreateMainPaths()
    {
        int startX = width / 2;
        int startY = height / 2;

        bool[,] visited = new bool[width, height];

        GenerateConnectedPaths(
            startX,
            startY,
            visited
        );
    }

    private void GenerateConnectedPaths(
        int x,
        int y,
        bool[,] visited)
    {
        visited[x, y] = true;

        List<Vector2Int> directions =
            new List<Vector2Int>
            {
                Vector2Int.up,
                Vector2Int.down,
                Vector2Int.left,
                Vector2Int.right
            };

        for (int i = 0; i < directions.Count; i++)
        {
            int randomIndex =
                Random.Range(
                    i,
                    directions.Count
                );

            Vector2Int temp =
                directions[i];

            directions[i] =
                directions[randomIndex];

            directions[randomIndex] =
                temp;
        }

        foreach (Vector2Int direction in directions)
        {
            int nextX = x + direction.x;
            int nextY = y + direction.y;

            if (nextX < 0 || nextX >= width)
                continue;

            if (nextY < 0 || nextY >= height)
                continue;

            if (visited[nextX, nextY])
                continue;

            RemoveWallBetween(
                tiles[x, y],
                tiles[nextX, nextY]
            );

            GenerateConnectedPaths(
                nextX,
                nextY,
                visited
            );
        }
    }

    private void CreateExtraPaths()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (x < width - 1)
                {
                    if (Random.value < extraPathChance)
                    {
                        RemoveWallBetween(
                            tiles[x, y],
                            tiles[x + 1, y]
                        );
                    }
                }

                if (y < height - 1)
                {
                    if (Random.value < extraPathChance)
                    {
                        RemoveWallBetween(
                            tiles[x, y],
                            tiles[x, y + 1]
                        );
                    }
                }
            }
        }
    }

    private void CreateOuterWalls()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Tile tile = tiles[x, y];

                if (y == height - 1)
                    tile.northWall = true;

                if (y == 0)
                    tile.southWall = true;

                if (x == 0)
                    tile.westWall = true;

                if (x == width - 1)
                    tile.eastWall = true;
            }
        }
    }

    private void CreateConnections()
    {
        int centerX = width / 2;
        int centerY = height / 2;

        if (northConnection)
        {
            tiles[centerX, height - 1].northWall = false;
        }

        if (southConnection)
        {
            tiles[centerX, 0].southWall = false;
        }

        if (eastConnection)
        {
            tiles[width - 1, centerY].eastWall = false;
        }

        if (westConnection)
        {
            tiles[0, centerY].westWall = false;
        }
    }

    // =========================================================
    // HABITACIONES
    // =========================================================

    private void GenerateRooms()
    {
        if (northConnection && northRoomPrefab != null)
        {
            CreateRoom(
                northRoomPrefab,
                Direction.North
            );
        }

        if (southConnection && southRoomPrefab != null)
        {
            CreateRoom(
                southRoomPrefab,
                Direction.South
            );
        }

        if (eastConnection && eastRoomPrefab != null)
        {
            CreateRoom(
                eastRoomPrefab,
                Direction.East
            );
        }

        if (westConnection && westRoomPrefab != null)
        {
            CreateRoom(
                westRoomPrefab,
                Direction.West
            );
        }
    }

    private void CreateRoom(
        GameObject roomPrefab,
        Direction direction)
    {
        GameObject room =
            Instantiate(
                roomPrefab,
                Vector3.zero,
                Quaternion.identity,
                transform
            );

        room.name = "Room_" + direction;

        RoomBuilder roomBuilder =
            room.GetComponent<RoomBuilder>();

        if (roomBuilder == null)
        {
            Debug.LogError(
                "RoomBuilder no encontrado en " +
                roomPrefab.name
            );

            return;
        }

        // -----------------------------------------------------
        // DIMENSIONES DE LA HABITACIÓN
        // -----------------------------------------------------

        float roomWidth =
            roomBuilder.width * tileSize;

        float roomHeight =
            roomBuilder.height * tileSize;

        // -----------------------------------------------------
        // CENTRO DEL MAPA
        // -----------------------------------------------------

        float mapCenterX =
            (width - 1) * tileSize * 0.5f;

        float mapCenterZ =
            (height - 1) * tileSize * 0.5f;

        // -----------------------------------------------------
        // EXTENSIONES DEL MAPA
        // -----------------------------------------------------

        float mapHalfWidth =
            width * tileSize * 0.5f;

        float mapHalfHeight =
            height * tileSize * 0.5f;

        // -----------------------------------------------------
        // ROTACIÓN Y POSICIÓN
        // -----------------------------------------------------

        switch (direction)
        {
            case Direction.North:

                room.transform.rotation =
                    Quaternion.Euler(
                        0f,
                        0f,
                        0f
                    );

                room.transform.position =
                    new Vector3(
                        mapCenterX,
                        0f,
                        mapCenterZ +
                        mapHalfHeight +
                        roomHeight * 0.5f +
                        roomDistance
                    );

                break;


            case Direction.South:

                room.transform.rotation =
                    Quaternion.Euler(
                        0f,
                        180f,
                        0f
                    );

                room.transform.position =
                    new Vector3(
                        mapCenterX,
                        0f,
                        mapCenterZ -
                        mapHalfHeight -
                        roomHeight * 0.5f -
                        roomDistance
                    );

                break;


            case Direction.East:

                room.transform.rotation =
                    Quaternion.Euler(
                        0f,
                        90f,
                        0f
                    );

                room.transform.position =
                    new Vector3(
                        mapCenterX +
                        mapHalfWidth +
                        roomWidth * 0.5f +
                        roomDistance,
                        0f,
                        mapCenterZ
                    );

                break;


            case Direction.West:

                room.transform.rotation =
                    Quaternion.Euler(
                        0f,
                        270f,
                        0f
                    );

                room.transform.position =
                    new Vector3(
                        mapCenterX -
                        mapHalfWidth -
                        roomWidth * 0.5f -
                        roomDistance,
                        0f,
                        mapCenterZ
                    );

                break;
        }

        // Generar los tiles después de colocar la habitación
        roomBuilder.GenerateRoom();
    }

    // =========================================================
    // PAREDES
    // =========================================================

    private void RemoveWallBetween(
        Tile a,
        Tile b)
    {
        if (a == null || b == null)
            return;

        Vector2Int difference =
            b.gridPosition -
            a.gridPosition;

        if (difference == Vector2Int.right)
        {
            a.eastWall = false;
            b.westWall = false;
        }
        else if (difference == Vector2Int.left)
        {
            a.westWall = false;
            b.eastWall = false;
        }
        else if (difference == Vector2Int.up)
        {
            a.northWall = false;
            b.southWall = false;
        }
        else if (difference == Vector2Int.down)
        {
            a.southWall = false;
            b.northWall = false;
        }
    }

    private void UpdateTileVisuals()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                tiles[x, y].UpdateVisual();
            }
        }
    }

    // =========================================================
    // LIMPIAR
    // =========================================================

    private void ClearMap()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Destroy(
                transform.GetChild(i).gameObject
            );
        }
    }

    // =========================================================
    // DIRECCIONES
    // =========================================================

    private enum Direction
    {
        North,
        South,
        East,
        West
    }
}