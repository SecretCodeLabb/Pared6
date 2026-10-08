using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Builds the board from a seed-deterministic BoardLayout: central board (width x height)
/// plus side rooms (size/tile/path chance taken from each room prefab's RoomBuilder).
/// All tiles share one world-aligned grid and no room is rotated, so a wall flag always
/// matches the visible wall and both tiles of an edge agree.
/// </summary>
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
    [Tooltip("Not used: rooms are attached directly to the board so their entrance lines up with the exit.")]
    public float roomDistance = 0f;

    [Header("Camera")]
    [Tooltip("Frame the main camera on the generated board (keeps its angle).")]
    public bool autoFrameCamera = true;
    [Min(1f)] public float cameraMargin = 1.25f;

    [Header("Seed")]
    public bool randomizeSeed = true;
    public int seed;

    /// <summary>The logical layout of the last generated board.</summary>
    public BoardLayout Layout { get; private set; }

    // Network mode: events are decided by the Host, not rolled here (T05).
    private bool generateEvents = true;

    private void Awake()
    {
        // Network mode: the Host's seed arrives later through GameStateManager.
        if (NetworkGameMode.IsActive)
            return;

        if (randomizeSeed)
        {
            seed = Random.Range(0, 999999);
        }

        Random.InitState(seed);

        ApplyOfflineRoomThemes();
        GenerateMap();
    }

    /// <summary>Offline fallback: rooms take the look of the characters picked in the local selection.</summary>
    private void ApplyOfflineRoomThemes()
    {
        if (GameSession.Instance == null || GameSession.Instance.Players.Count == 0)
            return;

        CharacterCatalog catalog = CharacterCatalog.Instance;
        List<int> chosen = new List<int>();

        foreach (PlayerState player in GameSession.Instance.Players)
            chosen.Add(catalog.IndexOf(player.characterData));

        int[] ids = RoomThemeAssignment.Assign(chosen, catalog.Count, seed);

        for (int i = 0; i < ids.Length; i++)
        {
            CharacterData character = catalog.Get(ids[i]);

            if (character != null && character.roomPrefab != null)
                SetRoomTheme(RoomThemeAssignment.SideOrder[i], character.roomPrefab);
        }
    }

    /// <summary>Deterministic generation from an injected seed (T05). Same seed => same board.</summary>
    public void GenerateWithSeed(int newSeed, bool includeEvents)
    {
        seed = newSeed;
        randomizeSeed = false;
        generateEvents = includeEvents;

        Random.InitState(seed);
        GenerateMap();
    }

    public void GenerateMap()
    {
        ClearMap();

        if (tilePrefab == null)
        {
            Debug.LogError("MapGenerator: No hay Tile Prefab asignado.");
            return;
        }

        RoomBuilder roomTemplate = GetRoomTemplate();

        BoardLayout.Settings settings = new BoardLayout.Settings
        {
            centerWidth = width,
            centerHeight = height,
            centerExtraPathChance = extraPathChance,
            roomWidth = roomTemplate != null ? roomTemplate.width : 5,
            roomDepth = roomTemplate != null ? roomTemplate.height : 5,
            roomExtraPathChance = roomTemplate != null ? roomTemplate.extraPathChance : extraPathChance,
            north = generateRooms && northConnection && northRoomPrefab != null,
            south = generateRooms && southConnection && southRoomPrefab != null,
            east = generateRooms && eastConnection && eastRoomPrefab != null,
            west = generateRooms && westConnection && westRoomPrefab != null
        };

        Layout = BoardLayout.Generate(settings, seed);

        GameObject defaultRoomTile = roomTemplate != null && roomTemplate.tilePrefab != null
            ? roomTemplate.tilePrefab
            : tilePrefab;

        Transform[] containers = new Transform[5];

        foreach (Vector2Int cell in Layout.Cells)
        {
            BoardRegion region = Layout.RegionOf(cell);
            bool central = region == BoardRegion.Center;
            Transform parent = central ? transform : GetContainer(containers, region);

            GameObject tileObject = Instantiate(
                central ? tilePrefab : RoomTilePrefab(region, defaultRoomTile),
                new Vector3(cell.x * tileSize, 0f, cell.y * tileSize),
                Quaternion.identity,
                parent
            );

            tileObject.name = central
                ? "Central_Tile_" + cell.x + "_" + cell.y
                : "Tile_" + cell.x + "_" + cell.y;

            Tile tile = tileObject.GetComponent<Tile>();

            if (tile == null)
            {
                Debug.LogError("MapGenerator: El Tile Prefab no tiene Tile.cs.");
                return;
            }

            tile.Initialize(cell);
            tile.region = region;
            tile.walkable = true;

            int mask = Layout.WallMask(cell);
            tile.northWall = (mask & BoardLayout.WallNorth) != 0;
            tile.southWall = (mask & BoardLayout.WallSouth) != 0;
            tile.eastWall = (mask & BoardLayout.WallEast) != 0;
            tile.westWall = (mask & BoardLayout.WallWest) != 0;
            tile.UpdateVisual();
        }

        DecorateRooms(containers);

        // EventManager.Awake may not have run yet (Awake order is not guaranteed).
        EventManager events = EventManager.Instance != null ? EventManager.Instance : FindFirstObjectByType<EventManager>();

        if (generateEvents && events != null)
        {
            events.GenerateEventTiles();
        }

        FrameCamera();

        Debug.Log("Mapa generado. Seed: " + seed + " · Casillas: " + Layout.Cells.Count);
    }

    /// <summary>
    /// Moves the main camera (keeping its angle and field of view) so the whole board fits on
    /// screen with a margin for the HUD panels. Disable autoFrameCamera to keep a hand-placed camera.
    /// </summary>
    private void FrameCamera()
    {
        Camera cam = Camera.main;

        if (!autoFrameCamera || cam == null || Layout == null || Layout.Cells.Count == 0)
            return;

        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);

        foreach (Vector2Int cell in Layout.Cells)
        {
            min = Vector2.Min(min, new Vector2(cell.x, cell.y));
            max = Vector2.Max(max, new Vector2(cell.x, cell.y));
        }

        min = (min - Vector2.one * 0.5f) * tileSize;
        max = (max + Vector2.one * 0.5f) * tileSize;

        // The game camera (follow / overview) takes over when present.
        if (cam.TryGetComponent(out BoardCameraController controller))
        {
            controller.SetBoardBounds(min, max);
            return;
        }

        Vector3 center = new Vector3((min.x + max.x) * 0.5f, 0f, (min.y + max.y) * 0.5f);
        float halfX = (max.x - min.x) * 0.5f * cameraMargin;
        float halfZ = (max.y - min.y) * 0.5f * cameraMargin;

        float vHalf = cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
        float hHalf = Mathf.Atan(Mathf.Tan(vHalf) * cam.aspect);
        float distance = Mathf.Max(halfZ / Mathf.Tan(vHalf), halfX / Mathf.Tan(hHalf)) + 2f;

        cam.transform.position = center - cam.transform.forward * distance;
        cam.farClipPlane = Mathf.Max(cam.farClipPlane, distance * 2f);
    }

    // =========================================================
    // ROOM THEMES (visual only — walls and sizes come from the layout)
    // =========================================================

    // Per-side theme chosen for this match (index = BoardRegion). Null = the side's own prefab.
    private readonly GameObject[] roomThemes = new GameObject[5];

    /// <summary>Overrides the look of one side room (e.g. the room of a chosen character). Call before generating.</summary>
    public void SetRoomTheme(BoardRegion side, GameObject roomPrefab)
    {
        roomThemes[(int)side] = roomPrefab;
    }

    public void ClearRoomThemes()
    {
        for (int i = 0; i < roomThemes.Length; i++)
            roomThemes[i] = null;
    }

    /// <summary>Room prefab that styles this side: the match theme if set, otherwise the side's prefab field.</summary>
    public GameObject GetRoomStylePrefab(BoardRegion side)
    {
        if (roomThemes[(int)side] != null)
            return roomThemes[(int)side];

        switch (side)
        {
            case BoardRegion.North: return northRoomPrefab;
            case BoardRegion.South: return southRoomPrefab;
            case BoardRegion.East: return eastRoomPrefab;
            case BoardRegion.West: return westRoomPrefab;
            default: return null;
        }
    }

    private RoomBuilder GetRoomStyle(BoardRegion side)
    {
        GameObject prefab = GetRoomStylePrefab(side);
        return prefab != null ? prefab.GetComponent<RoomBuilder>() : null;
    }

    private GameObject RoomTilePrefab(BoardRegion side, GameObject fallback)
    {
        RoomBuilder style = GetRoomStyle(side);
        return style != null && style.tilePrefab != null ? style.tilePrefab : fallback;
    }

    /// <summary>
    /// Scatters each room's decoration props on its floor and adds the room name. Uses its own
    /// seeded RNG, so every instance gets the same props without touching wall generation.
    /// </summary>
    private void DecorateRooms(Transform[] containers)
    {
        BoardRegion[] sides = { BoardRegion.North, BoardRegion.South, BoardRegion.East, BoardRegion.West };

        foreach (BoardRegion side in sides)
        {
            Transform container = containers[(int)side];
            RoomBuilder style = GetRoomStyle(side);

            if (container == null || style == null)
                continue;

            List<Vector2Int> cells = Layout.CellsIn(side);
            System.Random rng = new System.Random(unchecked(seed * 31 + (int)side * 7919));

            List<GameObject> props = style.decorations.FindAll(p => p != null);

            if (props.Count > 0)
            {
                Transform holder = new GameObject("Decorations").transform;
                holder.SetParent(container, false);

                foreach (Vector2Int cell in cells)
                {
                    if (rng.NextDouble() >= style.decorationChance)
                        continue;

                    GameObject prefab = props[rng.Next(props.Count)];
                    float angle = (float)(rng.NextDouble() * Mathf.PI * 2.0);
                    float radius = Mathf.Lerp(style.decorationRing.x, style.decorationRing.y, (float)rng.NextDouble()) * tileSize * 0.5f;
                    float scale = Mathf.Lerp(style.decorationScale.x, style.decorationScale.y, (float)rng.NextDouble());
                    float spin = (float)(rng.NextDouble() * 360.0);

                    Vector3 position = new Vector3(cell.x * tileSize + Mathf.Cos(angle) * radius, 0f, cell.y * tileSize + Mathf.Sin(angle) * radius);
                    GameObject prop = Instantiate(prefab, position, Quaternion.Euler(0f, spin, 0f) * prefab.transform.rotation, holder);
                    prop.transform.localScale = prefab.transform.localScale * scale;

                    // Props are purely visual.
                    foreach (Collider collider in prop.GetComponentsInChildren<Collider>())
                        Destroy(collider);
                }
            }

            if (!string.IsNullOrEmpty(style.themeName) && cells.Count > 0)
                CreateRoomLabel(container, cells, style);
        }
    }

    private void CreateRoomLabel(Transform container, List<Vector2Int> cells, RoomBuilder style)
    {
        Vector2 center = Vector2.zero;

        foreach (Vector2Int cell in cells)
            center += new Vector2(cell.x, cell.y);

        center = center / cells.Count * tileSize;

        GameObject labelObject = new GameObject("RoomLabel");
        labelObject.transform.SetParent(container, false);
        labelObject.transform.position = new Vector3(center.x, 3.5f, center.y);

        TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
        label.text = style.themeName;
        label.fontSize = 9f;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.color = style.themeLabelColor;
        label.outlineWidth = 0.25f;
        label.outlineColor = new Color32(0, 0, 0, 255);

        Camera cam = Camera.main;

        if (cam != null)
            labelObject.transform.rotation = cam.transform.rotation;
    }

    private RoomBuilder GetRoomTemplate()
    {
        GameObject[] prefabs = { northRoomPrefab, southRoomPrefab, eastRoomPrefab, westRoomPrefab };

        foreach (GameObject prefab in prefabs)
        {
            if (prefab != null && prefab.TryGetComponent(out RoomBuilder builder))
                return builder;
        }

        return null;
    }

    private Transform GetContainer(Transform[] containers, BoardRegion region)
    {
        int index = (int)region;

        if (containers[index] == null)
        {
            GameObject room = new GameObject("Room_" + region);
            room.transform.SetParent(transform, false);
            containers[index] = room.transform;
        }

        return containers[index];
    }

    // =========================================================
    // LIMPIAR
    // =========================================================

    private void ClearMap()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Destroy(transform.GetChild(i).gameObject);
        }
    }
}
