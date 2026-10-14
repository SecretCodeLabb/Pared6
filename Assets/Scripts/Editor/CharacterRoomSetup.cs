using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Turns a character's room model (FBX) into a side-room prefab (RoomEnvironment) and assigns it
/// to the character's room style (RoomBuilder.environmentPrefab):
///   - the room shell (a closed 7x7-tile box, 14 x 14 units) is centred on the prefab origin with
///     its floor at y = 0, so the room is not rescaled (props that stick out do not shrink it);
///   - a one-tile doorway is cut in the middle of the -Z wall (the entrance faces the board);
///   - props keep their positions and act as walls: every move between two neighbouring cells
///     whose path crosses a prop is baked into RoomEnvironment.blockedMoves (MapGenerator turns them
///     into board walls), always leaving a path from the door to the mission tile.
/// PPOP/Rooms/Build Skater Room. Add a line in <see cref="Rooms"/> for the next characters.
/// </summary>
public static class CharacterRoomSetup
{
    private struct RoomSource
    {
        public string Character;
        public string ModelPath;
        public string ShellName;
        public float Yaw;
        /// <summary>Props that never block movement (case-insensitive names).</summary>
        public string[] Walkable;
    }

    private static readonly RoomSource[] Rooms =
    {
        // Skater: the free middle of the model's -Z wall is the door; mission at the back, between the two boxes.
        new RoomSource { Character = "Skater", ModelPath = "Assets/Art/Rooms/Skater/escenario5(Skater).fbx", ShellName = "eltiestoese", Yaw = 0f, Walkable = new[] { "Semi_circulo" } },
    };

    private const int SizeInTiles = 7;
    private const float TileSize = 2f;

    [MenuItem("PPOP/Rooms/Build Skater Room")]
    public static GameObject BuildSkater() => Build(Rooms[0]);

    private static GameObject Build(RoomSource source)
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(source.ModelPath);

        if (model == null)
        {
            Debug.LogError("[PPOP] No se encontró el modelo " + source.ModelPath);
            return null;
        }

        string folder = $"Assets/Prefabs/Rooms/{source.Character}";
        string prefabPath = $"{folder}/Room_{source.Character}_Escenario.prefab";
        string meshPath = $"{folder}/Room_{source.Character}_ShellDoor.asset";

        GameObject root = new GameObject($"Room_{source.Character}_Escenario");
        RoomEnvironment environment = root.AddComponent<RoomEnvironment>();
        environment.sizeInTiles = SizeInTiles;
        environment.fitToTiles = false;

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
        instance.name = "Model";
        instance.transform.localRotation = Quaternion.Euler(0f, source.Yaw, 0f);

        Transform shell = FindChild(instance.transform, source.ShellName);
        MeshFilter shellFilter = shell != null ? shell.GetComponent<MeshFilter>() : null;

        if (shellFilter == null || shellFilter.sharedMesh == null)
        {
            Object.DestroyImmediate(root);
            Debug.LogError($"[PPOP] El modelo no tiene la caja '{source.ShellName}'.");
            return null;
        }

        // Shell centred on the origin, floor on y = 0.
        Bounds shellBounds = shellFilter.GetComponent<Renderer>().bounds;
        instance.transform.localPosition = new Vector3(-shellBounds.center.x, -shellBounds.min.y, -shellBounds.center.z);
        shellBounds = shellFilter.GetComponent<Renderer>().bounds;

        Mesh door = CutDoor(shellFilter, shellBounds);
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);

        if (existing != null)
        {
            EditorUtility.CopySerialized(door, existing);
            door = existing;
        }
        else
        {
            AssetDatabase.CreateAsset(door, meshPath);
        }

        shellFilter.sharedMesh = door;

        BakeBlockedMoves(root, environment, shellFilter, source.Walkable);

        foreach (Collider c in root.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(c);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);

        AssignToCharacter(source.Character, prefab);
        AssetDatabase.SaveAssets();
        Debug.Log($"[PPOP] Habitación de {source.Character}: {prefabPath} (puerta en -Z).");
        return prefab;
    }

    // =========================================================
    // PROPS AS WALLS
    // =========================================================

    private const float RasterStep = 0.1f;
    private const float MinPropHeight = 0.03f;

    /// <summary>
    /// Projects every prop (all meshes except the shell and the walkable ones) on the floor, then
    /// blocks each move between neighbouring cells whose centre-to-centre path touches a prop.
    /// A path from the entrance to the mission cell is always kept (fewest props removed).
    /// </summary>
    private static void BakeBlockedMoves(GameObject root, RoomEnvironment environment, MeshFilter shell, string[] walkable)
    {
        int n = environment.sizeInTiles;
        float half = n * environment.cellSize * 0.5f;
        int size = Mathf.CeilToInt(half * 2f / RasterStep);
        bool[,] occupied = new bool[size, size];
        Matrix4x4 toRoom = root.transform.worldToLocalMatrix;
        List<string> props = new List<string>();

        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter == shell || filter.sharedMesh == null || IsWalkable(filter.name, walkable))
                continue;

            props.Add(filter.name);
            Matrix4x4 matrix = toRoom * filter.transform.localToWorldMatrix;
            Vector3[] vertices = filter.sharedMesh.vertices;
            int[] triangles = filter.sharedMesh.triangles;
            Vector3[] room = new Vector3[vertices.Length];

            for (int i = 0; i < vertices.Length; i++)
                room[i] = matrix.MultiplyPoint3x4(vertices[i]);

            for (int t = 0; t < triangles.Length; t += 3)
            {
                Vector3 a = room[triangles[t]], b = room[triangles[t + 1]], c = room[triangles[t + 2]];

                // Flat decals on the floor do not block.
                if (Mathf.Max(a.y, b.y, c.y) < MinPropHeight)
                    continue;

                FillTriangle(occupied, size, half, a, b, c);
            }
        }

        // Moves between neighbouring cells whose path crosses a prop.
        HashSet<(Vector2Int, Vector2Int)> blocked = new HashSet<(Vector2Int, Vector2Int)>();

        for (int x = 0; x < n; x++)
        {
            for (int y = 0; y < n; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);

                foreach (Vector2Int d in new[] { Vector2Int.right, Vector2Int.up })
                {
                    Vector2Int next = cell + d;

                    if (next.x < n && next.y < n &&
                        PathTouches(occupied, size, half, environment.LocalCellCenter(cell), environment.LocalCellCenter(next)))
                        blocked.Add(Key(cell, next));
                }
            }
        }

        int reopened = KeepPath(blocked, n, new Vector2Int((n - 1) / 2, 0), new Vector2Int((n - 1) / 2, n - 1));

        environment.blockedMoves = new List<RoomEnvironment.BlockedMove>();

        foreach ((Vector2Int a, Vector2Int b) in blocked)
            environment.blockedMoves.Add(new RoomEnvironment.BlockedMove { from = a, to = b });

        Debug.Log($"[PPOP] Obstáculos: {props.Count} objetos ({string.Join(", ", props)}) · {blocked.Count} pasos bloqueados" +
                  (reopened > 0 ? $" · {reopened} reabiertos para llegar a la misión" : "") + "\n" + Map(blocked, n));
    }

    private static bool IsWalkable(string name, string[] walkable)
    {
        if (walkable == null)
            return false;

        foreach (string w in walkable)
        {
            if (string.Equals(name, w, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static (Vector2Int, Vector2Int) Key(Vector2Int a, Vector2Int b)
    {
        return a.x < b.x || (a.x == b.x && a.y < b.y) ? (a, b) : (b, a);
    }

    /// <summary>Marks the raster cells covered by the triangle (top view), its edges included (vertical faces).</summary>
    private static void FillTriangle(bool[,] grid, int size, float half, Vector3 a, Vector3 b, Vector3 c)
    {
        Vector2 p0 = new Vector2(a.x, a.z), p1 = new Vector2(b.x, b.z), p2 = new Vector2(c.x, c.z);
        int minX = Mathf.Max(0, ToIndex(Mathf.Min(p0.x, p1.x, p2.x), half));
        int maxX = Mathf.Min(size - 1, ToIndex(Mathf.Max(p0.x, p1.x, p2.x), half));
        int minY = Mathf.Max(0, ToIndex(Mathf.Min(p0.y, p1.y, p2.y), half));
        int maxY = Mathf.Min(size - 1, ToIndex(Mathf.Max(p0.y, p1.y, p2.y), half));
        float area = Cross(p1 - p0, p2 - p0);

        if (Mathf.Abs(area) > 1e-5f)
        {
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    Vector2 p = new Vector2(-half + (x + 0.5f) * RasterStep, -half + (y + 0.5f) * RasterStep);
                    float w0 = Cross(p1 - p, p2 - p) / area, w1 = Cross(p2 - p, p0 - p) / area;

                    if (w0 >= -0.01f && w1 >= -0.01f && 1f - w0 - w1 >= -0.01f)
                        grid[x, y] = true;
                }
            }
        }

        MarkLine(grid, size, half, p0, p1);
        MarkLine(grid, size, half, p1, p2);
        MarkLine(grid, size, half, p2, p0);
    }

    private static void MarkLine(bool[,] grid, int size, float half, Vector2 a, Vector2 b)
    {
        int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / (RasterStep * 0.5f)));

        for (int i = 0; i <= steps; i++)
        {
            Vector2 p = Vector2.Lerp(a, b, i / (float)steps);
            int x = ToIndex(p.x, half), y = ToIndex(p.y, half);

            if (x >= 0 && y >= 0 && x < size && y < size)
                grid[x, y] = true;
        }
    }

    private static bool PathTouches(bool[,] grid, int size, float half, Vector3 from, Vector3 to)
    {
        int steps = Mathf.CeilToInt(Vector3.Distance(from, to) / (RasterStep * 0.5f));

        for (int i = 0; i <= steps; i++)
        {
            Vector3 p = Vector3.Lerp(from, to, i / (float)steps);
            int x = ToIndex(p.x, half), y = ToIndex(p.z, half);

            if (x >= 0 && y >= 0 && x < size && y < size && grid[x, y])
                return true;
        }

        return false;
    }

    private static int ToIndex(float v, float half) => Mathf.FloorToInt((v + half) / RasterStep);

    private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    /// <summary>0-1 BFS (crossing a blocked move costs 1): reopens the fewest blocked moves needed to reach the goal.</summary>
    private static int KeepPath(HashSet<(Vector2Int, Vector2Int)> blocked, int n, Vector2Int start, Vector2Int goal)
    {
        Dictionary<Vector2Int, int> cost = new Dictionary<Vector2Int, int> { [start] = 0 };
        Dictionary<Vector2Int, Vector2Int> previous = new Dictionary<Vector2Int, Vector2Int>();
        LinkedList<Vector2Int> deque = new LinkedList<Vector2Int>();
        deque.AddFirst(start);
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        while (deque.Count > 0)
        {
            Vector2Int cell = deque.First.Value;
            deque.RemoveFirst();

            foreach (Vector2Int d in directions)
            {
                Vector2Int next = cell + d;

                if (next.x < 0 || next.y < 0 || next.x >= n || next.y >= n)
                    continue;

                int step = blocked.Contains(Key(cell, next)) ? 1 : 0;
                int total = cost[cell] + step;

                if (cost.TryGetValue(next, out int known) && known <= total)
                    continue;

                cost[next] = total;
                previous[next] = cell;

                if (step == 0) deque.AddFirst(next);
                else deque.AddLast(next);
            }
        }

        int reopened = 0;

        for (Vector2Int cell = goal; cell != start && previous.ContainsKey(cell); cell = previous[cell])
        {
            if (blocked.Remove(Key(previous[cell], cell)))
                reopened++;
        }

        return reopened;
    }

    /// <summary>ASCII top view (back row first): '.' cell, '|' / '-' blocked moves, D door, M mission.</summary>
    private static string Map(HashSet<(Vector2Int, Vector2Int)> blocked, int n)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        for (int y = n - 1; y >= 0; y--)
        {
            for (int x = 0; x < n; x++)
            {
                bool door = x == (n - 1) / 2 && y == 0, mission = x == (n - 1) / 2 && y == n - 1;
                sb.Append(mission ? 'M' : door ? 'D' : '.');

                if (x < n - 1)
                    sb.Append(blocked.Contains(Key(new Vector2Int(x, y), new Vector2Int(x + 1, y))) ? '|' : ' ');
            }

            sb.Append('\n');

            if (y > 0)
            {
                for (int x = 0; x < n; x++)
                    sb.Append(blocked.Contains(Key(new Vector2Int(x, y - 1), new Vector2Int(x, y))) ? "- " : "  ");

                sb.Append('\n');
            }
        }

        return sb.ToString();
    }

    /// <summary>Copy of the shell mesh without the wall triangles of the middle tile of the -Z wall.</summary>
    private static Mesh CutDoor(MeshFilter filter, Bounds roomBounds)
    {
        Mesh input = filter.sharedMesh;
        Matrix4x4 toRoom = filter.transform.localToWorldMatrix;
        Vector3[] vertices = input.vertices;
        Vector3[] world = new Vector3[vertices.Length];

        for (int i = 0; i < vertices.Length; i++)
            world[i] = toRoom.MultiplyPoint3x4(vertices[i]);

        float doorHalf = TileSize * 0.5f + 0.01f;
        float wallBand = TileSize * 0.25f;
        float minZ = roomBounds.min.z;
        float floorTop = roomBounds.min.y + roomBounds.size.y * 0.1f;

        Mesh output = Object.Instantiate(input);
        output.name = input.name + "_Door";
        int removed = 0;

        for (int sub = 0; sub < input.subMeshCount; sub++)
        {
            int[] triangles = input.GetTriangles(sub);
            List<int> kept = new List<int>(triangles.Length);

            for (int t = 0; t < triangles.Length; t += 3)
            {
                Vector3 a = world[triangles[t]], b = world[triangles[t + 1]], c = world[triangles[t + 2]];
                bool inDoor = Mathf.Abs(a.x) <= doorHalf && Mathf.Abs(b.x) <= doorHalf && Mathf.Abs(c.x) <= doorHalf;
                bool onEntranceWall = Mathf.Max(a.z, b.z, c.z) <= minZ + wallBand;
                bool aboveFloor = Mathf.Max(a.y, b.y, c.y) > floorTop;

                if (inDoor && onEntranceWall && aboveFloor)
                {
                    removed++;
                    continue;
                }

                kept.Add(triangles[t]);
                kept.Add(triangles[t + 1]);
                kept.Add(triangles[t + 2]);
            }

            output.SetTriangles(kept, sub);
        }

        output.RecalculateBounds();
        Debug.Log($"[PPOP] Puerta: {removed} triángulos quitados de {input.name}.");
        return output;
    }

    private static void AssignToCharacter(string character, GameObject prefab)
    {
        string stylePath = $"Assets/Prefabs/Rooms/{character}/Room_{character}.prefab";
        GameObject style = PrefabUtility.LoadPrefabContents(stylePath);

        if (style == null)
        {
            Debug.LogError("[PPOP] No se encontró " + stylePath);
            return;
        }

        RoomBuilder builder = style.GetComponent<RoomBuilder>();

        if (builder != null)
        {
            builder.environmentPrefab = prefab;
            PrefabUtility.SaveAsPrefabAsset(style, stylePath);
        }

        PrefabUtility.UnloadPrefabContents(style);
    }

    private static Transform FindChild(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == name)
                return t;
        }

        return null;
    }
}
