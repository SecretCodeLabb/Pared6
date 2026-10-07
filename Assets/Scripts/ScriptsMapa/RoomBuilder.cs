using System.Collections.Generic;
using UnityEngine;

public class RoomBuilder : MonoBehaviour
{
    [Header("Room Size")]
    public int width = 5;
    public int height = 5;

    [Header("Tile")]
    public GameObject tilePrefab;
    public float tileSize = 2f;

    [Header("Entrance")]
    public bool southEntrance = true;

    [Header("Procedural Generation")]
    [Range(0f, 1f)]
    public float extraPathChance = 0.35f;

    // =========================================================
    // ROOM THEME (visual only; walls stay procedural)
    // =========================================================

    [Header("Room Theme (visual only)")]
    [Tooltip("Name shown above the room in the game (e.g. \"Cuarto de Chismosa\"). Empty = no label.")]
    public string themeName;

    [Tooltip("Color of the floating room name.")]
    public Color themeLabelColor = Color.white;

    [Tooltip("Props scattered on the floor of this room (any 3D prefab). Colliders are removed at spawn.")]
    public List<GameObject> decorations = new List<GameObject>();

    [Tooltip("Chance per tile of spawning one decoration.")]
    [Range(0f, 1f)]
    public float decorationChance = 0.35f;

    [Tooltip("Random uniform scale range applied to each decoration.")]
    public Vector2 decorationScale = new Vector2(0.8f, 1.2f);

    [Tooltip("Decorations are placed in a ring around the tile centre (fraction of half a tile), leaving the centre free for pawns and events.")]
    public Vector2 decorationRing = new Vector2(0.55f, 0.8f);

    private Tile[,] tiles;

    public void GenerateRoom()
    {
        ClearRoom();

        if (tilePrefab == null)
        {
            Debug.LogError(
                "RoomBuilder: No hay Tile Prefab asignado."
            );

            return;
        }

        tiles = new Tile[width, height];

        CreateTiles();

        // 1. Cerramos toda la habitación
        CreateClosedRoom();

        // 2. Creamos una red conectada
        CreateMainPaths();

        // 3. Añadimos caminos adicionales
        CreateExtraPaths();

        // 4. Conservamos las paredes exteriores
        CreateOuterWalls();

        // 5. Abrimos la entrada
        CreateEntrance();

        // 6. Actualizamos las paredes visualmente
        UpdateVisuals();
    }

    // =========================================================
    // CREAR TILES
    // =========================================================

    private void CreateTiles()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                float offsetX =
                    (width - 1) * tileSize * 0.5f;

                float offsetZ =
                    (height - 1) * tileSize * 0.5f;

                Vector3 localPosition =
                    new Vector3(
                        x * tileSize - offsetX,
                        0f,
                        y * tileSize - offsetZ
                    );

                GameObject tileObject =
                    Instantiate(
                        tilePrefab,
                        transform
                    );

                tileObject.transform.localPosition =
                    localPosition;

                tileObject.transform.localRotation =
                    Quaternion.identity;

                tileObject.name =
                    "Tile_" + x + "_" + y;

                Tile tile =
                    tileObject.GetComponent<Tile>();

                if (tile == null)
                {
                    Debug.LogError(
                        "RoomBuilder: El Tile Prefab no tiene Tile.cs."
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

    // =========================================================
    // CERRAR HABITACIÓN
    // =========================================================

    private void CreateClosedRoom()
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

    // =========================================================
    // GENERACIÓN PRINCIPAL
    // =========================================================

    private void CreateMainPaths()
    {
        int startX = width / 2;
        int startY = height / 2;

        bool[,] visited =
            new bool[width, height];

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

        // Mezclar las direcciones
        for (int i = 0;
             i < directions.Count;
             i++)
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

        foreach (Vector2Int direction
                 in directions)
        {
            int nextX =
                x + direction.x;

            int nextY =
                y + direction.y;

            // Fuera del mapa
            if (nextX < 0 ||
                nextX >= width)
                continue;

            if (nextY < 0 ||
                nextY >= height)
                continue;

            // Ya visitado
            if (visited[nextX, nextY])
                continue;

            // Abrir conexión
            RemoveWallBetween(
                tiles[x, y],
                tiles[nextX, nextY]
            );

            // Continuar generando
            GenerateConnectedPaths(
                nextX,
                nextY,
                visited
            );
        }
    }

    // =========================================================
    // CAMINOS EXTRA
    // =========================================================

    private void CreateExtraPaths()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                // Conexión horizontal
                if (x < width - 1)
                {
                    if (Random.value <
                        extraPathChance)
                    {
                        RemoveWallBetween(
                            tiles[x, y],
                            tiles[x + 1, y]
                        );
                    }
                }

                // Conexión vertical
                if (y < height - 1)
                {
                    if (Random.value <
                        extraPathChance)
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

    // =========================================================
    // PAREDES EXTERIORES
    // =========================================================

    private void CreateOuterWalls()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Tile tile =
                    tiles[x, y];

                // Pared norte
                if (y == height - 1)
                {
                    tile.northWall = true;
                }

                // Pared sur
                if (y == 0)
                {
                    tile.southWall = true;
                }

                // Pared oeste
                if (x == 0)
                {
                    tile.westWall = true;
                }

                // Pared este
                if (x == width - 1)
                {
                    tile.eastWall = true;
                }
            }
        }
    }

    // =========================================================
    // ENTRADA
    // =========================================================

    private void CreateEntrance()
    {
        if (!southEntrance)
            return;

        int entranceX = width / 2;
        int entranceY = 0;

        Tile entranceTile = tiles[entranceX, entranceY];

        if (entranceTile == null)
        {
            Debug.LogError(
                "RoomBuilder: No se encontró el tile de entrada."
            );

            return;
        }

        // La entrada debe quedar SIEMPRE abierta.
        entranceTile.southWall = false;
    }

    // =========================================================
    // QUITAR PARED ENTRE TILES
    // =========================================================

    private void RemoveWallBetween(
        Tile a,
        Tile b)
    {
        if (a == null ||
            b == null)
            return;

        Vector2Int difference =
            b.gridPosition -
            a.gridPosition;

        if (difference ==
            Vector2Int.right)
        {
            a.eastWall = false;
            b.westWall = false;
        }
        else if (difference ==
                 Vector2Int.left)
        {
            a.westWall = false;
            b.eastWall = false;
        }
        else if (difference ==
                 Vector2Int.up)
        {
            a.northWall = false;
            b.southWall = false;
        }
        else if (difference ==
                 Vector2Int.down)
        {
            a.southWall = false;
            b.northWall = false;
        }
    }

    // =========================================================
    // ACTUALIZAR VISUALES
    // =========================================================

    private void UpdateVisuals()
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
    // LIMPIAR HABITACIÓN
    // =========================================================

    private void ClearRoom()
    {
        for (
            int i = transform.childCount - 1;
            i >= 0;
            i--
        )
        {
            Destroy(
                transform.GetChild(i).gameObject
            );
        }
    }
}