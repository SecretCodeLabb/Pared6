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
    [Tooltip("Logic tile (Tile + TileView). Used by the side rooms, and by the central board when centralTilePrefab is empty.")]
    public GameObject tilePrefab;

    [Tooltip("Tile used for the central maze (e.g. the Nivel 0 model). Empty = tilePrefab.")]
    public GameObject centralTilePrefab;
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

    [Header("Prefab Rooms")]
    [Tooltip("Side rooms are a fixed prefab (RoomEnvironment) laid over an open square of tiles instead of a procedural maze. Movement, events and the mission tile keep using the tiles.")]
    public bool usePrefabRooms = true;

    [Tooltip("Room prefab for every character whose room style (RoomBuilder.environmentPrefab) has none. Its sizeInTiles sets the size of all side rooms.")]
    public GameObject defaultRoomEnvironment;

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

    private GameObject CentralTilePrefab => centralTilePrefab != null && centralTilePrefab.GetComponent<Tile>() != null ? centralTilePrefab : tilePrefab;

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
        bool prefabRooms = PrefabRoomsActive;
        int prefabRoomSize = prefabRooms ? PrefabRoomSize() : 0;

        BoardLayout.Settings settings = new BoardLayout.Settings
        {
            centerWidth = width,
            centerHeight = height,
            centerExtraPathChance = extraPathChance,
            roomWidth = prefabRooms ? prefabRoomSize : roomTemplate != null ? roomTemplate.width : 5,
            roomDepth = prefabRooms ? prefabRoomSize : roomTemplate != null ? roomTemplate.height : 5,
            roomExtraPathChance = roomTemplate != null ? roomTemplate.extraPathChance : extraPathChance,
            openRooms = prefabRooms,
            north = generateRooms && northConnection && northRoomPrefab != null,
            south = generateRooms && southConnection && southRoomPrefab != null,
            east = generateRooms && eastConnection && eastRoomPrefab != null,
            west = generateRooms && westConnection && westRoomPrefab != null
        };

        Layout = BoardLayout.Generate(settings, seed);

        // Props of the prefab rooms (ramps, rails...) become walls before the tiles are built.
        if (prefabRooms)
            ApplyRoomObstacles();

        GameObject defaultRoomTile = roomTemplate != null && roomTemplate.tilePrefab != null
            ? roomTemplate.tilePrefab
            : tilePrefab;

        Transform[] containers = new Transform[5];

        foreach (Vector2Int cell in Layout.Cells)
        {
            BoardRegion region = Layout.RegionOf(cell);
            bool central = region == BoardRegion.Center;
            Transform parent = central ? transform : GetContainer(containers, region);

            bool logicOnly = !central && prefabRooms;

            GameObject tileObject = Instantiate(
                central ? CentralTilePrefab : logicOnly ? tilePrefab : RoomTilePrefab(region, defaultRoomTile),
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

            // Prefab rooms: the tile only carries movement, events and the mission marker.
            if (logicOnly)
                HideTileVisuals(tileObject);
        }

        if (prefabRooms)
            SpawnRoomEnvironments(containers);

        DecorateRooms(containers, !prefabRooms);

        // EventManager.Awake may not have run yet (Awake order is not guaranteed).
        EventManager events = EventManager.Instance != null ? EventManager.Instance : FindFirstObjectByType<EventManager>();

        if (generateEvents && events != null)
        {
            events.GenerateEventTiles();
        }

        FrameCamera();

        // Backrooms lights / mood for the central maze (cheap, visual only).
        if (TryGetComponent(out BackroomsAmbience ambience))
            ambience.Build(this);

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
    private void DecorateRooms(Transform[] containers, bool scatterProps)
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

            List<GameObject> props = scatterProps ? style.decorations.FindAll(p => p != null) : new List<GameObject>();

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

    // =========================================================
    // PREFAB ROOMS
    // =========================================================

    private bool PrefabRoomsActive => usePrefabRooms && generateRooms && defaultRoomEnvironment != null;

    private int PrefabRoomSize()
    {
        return defaultRoomEnvironment.TryGetComponent(out RoomEnvironment environment) ? environment.sizeInTiles : 7;
    }

    /// <summary>Room prefab of a side: the character's own (RoomBuilder.environmentPrefab) or the default.</summary>
    public GameObject GetRoomEnvironmentPrefab(BoardRegion side)
    {
        RoomBuilder style = GetRoomStyle(side);
        return style != null && style.environmentPrefab != null ? style.environmentPrefab : defaultRoomEnvironment;
    }

    private static void HideTileVisuals(GameObject tileObject)
    {
        foreach (Renderer renderer in tileObject.GetComponentsInChildren<Renderer>(true))
            renderer.enabled = false;
    }

    /// <summary>
    /// Places each side's room prefab over its tiles: centred on the room, entrance (prefab -Z)
    /// facing the central board and, if fitToTiles, scaled to cover the room's tiles exactly.
    /// </summary>
    private void SpawnRoomEnvironments(Transform[] containers)
    {
        BoardRegion[] sides = { BoardRegion.North, BoardRegion.South, BoardRegion.East, BoardRegion.West };

        foreach (BoardRegion side in sides)
        {
            Transform container = containers[(int)side];
            GameObject prefab = GetRoomEnvironmentPrefab(side);
            List<Vector2Int> cells = Layout.CellsIn(side);

            if (container == null || prefab == null || cells.Count == 0)
                continue;

            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);

            foreach (Vector2Int cell in cells)
            {
                min = Vector2.Min(min, cell);
                max = Vector2.Max(max, cell);
            }

            Vector3 center = new Vector3((min.x + max.x) * 0.5f * tileSize, 0f, (min.y + max.y) * 0.5f * tileSize);
            float footprint = (max.x - min.x + 1f) * tileSize;

            // Spawn unrotated at the origin to measure the prefab's own footprint.
            GameObject room = Instantiate(prefab, Vector3.zero, Quaternion.identity, container);
            room.name = "Environment_" + side;

            Vector3 offset = Vector3.zero;

            if (room.TryGetComponent(out RoomEnvironment environment) && environment.fitToTiles &&
                TryGetRendererBounds(room, out Bounds bounds) && Mathf.Max(bounds.size.x, bounds.size.z) > 0.001f)
            {
                float scale = footprint / Mathf.Max(bounds.size.x, bounds.size.z);
                room.transform.localScale *= scale;
                offset = new Vector3(-bounds.center.x, 0f, -bounds.center.z) * scale;
            }

            Quaternion rotation = Quaternion.Euler(0f, EntranceYaw(side), 0f);
            room.transform.SetPositionAndRotation(center + rotation * offset, rotation);

            // Purely visual: tiles handle movement.
            foreach (Collider collider in room.GetComponentsInChildren<Collider>())
                Destroy(collider);
        }
    }

    /// <summary>World centre of a side room (its cells' bounding box), where its prefab is placed.</summary>
    private Vector3 RoomCenter(BoardRegion side)
    {
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);

        foreach (Vector2Int cell in Layout.CellsIn(side))
        {
            min = Vector2.Min(min, cell);
            max = Vector2.Max(max, cell);
        }

        return new Vector3((min.x + max.x) * 0.5f * tileSize, 0f, (min.y + max.y) * 0.5f * tileSize);
    }

    /// <summary>
    /// Turns each room prefab's blocked moves (RoomEnvironment.blockedMoves, baked from its props)
    /// into walls of the board, so players have to go around the props. Same prefab and seed on
    /// every instance, so the result is identical for everyone.
    /// </summary>
    private void ApplyRoomObstacles()
    {
        BoardRegion[] sides = { BoardRegion.North, BoardRegion.South, BoardRegion.East, BoardRegion.West };

        foreach (BoardRegion side in sides)
        {
            GameObject prefab = GetRoomEnvironmentPrefab(side);
            RoomEnvironment environment = prefab != null ? prefab.GetComponent<RoomEnvironment>() : null;

            if (environment == null || environment.blockedMoves == null || environment.blockedMoves.Count == 0 ||
                Layout.CellsIn(side).Count == 0)
                continue;

            if (environment.fitToTiles || environment.sizeInTiles != PrefabRoomSize())
            {
                Debug.LogWarning($"MapGenerator: {prefab.name} tiene obstáculos pero se reescala; se ignoran.");
                continue;
            }

            Vector3 center = RoomCenter(side);
            Quaternion rotation = Quaternion.Euler(0f, EntranceYaw(side), 0f);
            int added = 0;

            foreach (RoomEnvironment.BlockedMove move in environment.blockedMoves)
            {
                Vector2Int a = WorldToCell(center + rotation * environment.LocalCellCenter(move.from));
                Vector2Int b = WorldToCell(center + rotation * environment.LocalCellCenter(move.to));
                Vector2Int direction = b - a;

                if (Mathf.Abs(direction.x) + Mathf.Abs(direction.y) != 1 || !Layout.Contains(a) || !Layout.Contains(b) ||
                    Layout.RegionOf(a) != side || Layout.RegionOf(b) != side)
                    continue;

                Layout.Close(a, direction);
                added++;
            }

            // The bake keeps a path; this only reports a broken prefab.
            if (Layout.Entrances.TryGetValue(side, out Vector2Int entrance) && !Layout.Reachable(entrance).Contains(Layout.MissionCell(side)))
                Debug.LogError($"MapGenerator: en {prefab.name} los obstáculos cierran el camino a la misión. Regenera la habitación (PPOP/Rooms).");

            Debug.Log($"MapGenerator: {added} paredes de obstáculos en la habitación {side} ({prefab.name}).");
        }
    }

    private Vector2Int WorldToCell(Vector3 world)
    {
        return new Vector2Int(Mathf.RoundToInt(world.x / tileSize), Mathf.RoundToInt(world.z / tileSize));
    }

    /// <summary>Yaw that turns the prefab's entrance (-Z) towards the central board.</summary>
    private static float EntranceYaw(BoardRegion side)
    {
        switch (side)
        {
            case BoardRegion.South: return 180f;
            case BoardRegion.East: return 90f;
            case BoardRegion.West: return -90f;
            default: return 0f;
        }
    }

    private static bool TryGetRendererBounds(GameObject root, out Bounds bounds)
    {
        bounds = default;
        bool found = false;

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
        {
            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return found;
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
