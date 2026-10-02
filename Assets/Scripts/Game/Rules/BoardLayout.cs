using System.Collections.Generic;
using UnityEngine;

public enum BoardRegion : byte
{
    Center,
    North,
    South,
    East,
    West
}

/// <summary>
/// Pure, seed-deterministic board layout on ONE world-aligned grid: the central board
/// plus up to four side rooms attached to its middle exits. Every wall is stored on both
/// tiles that share it, so movement logic and visuals always agree. No rotations.
/// Grid coordinates map to world positions as (x * tileSize, 0, y * tileSize).
/// </summary>
public class BoardLayout
{
    public const int WallNorth = 1;
    public const int WallSouth = 2;
    public const int WallEast = 4;
    public const int WallWest = 8;
    public const int AllWalls = WallNorth | WallSouth | WallEast | WallWest;

    public struct Settings
    {
        public int centerWidth;
        public int centerHeight;
        public float centerExtraPathChance;
        public int roomWidth;   // along the side the room is attached to
        public int roomDepth;   // away from the central board
        public float roomExtraPathChance;
        public bool north;
        public bool south;
        public bool east;
        public bool west;
    }

    private readonly Dictionary<Vector2Int, int> walls = new Dictionary<Vector2Int, int>();
    private readonly Dictionary<Vector2Int, BoardRegion> regions = new Dictionary<Vector2Int, BoardRegion>();
    private readonly List<Vector2Int> orderedCells = new List<Vector2Int>();

    public readonly Dictionary<BoardRegion, Vector2Int> Entrances = new Dictionary<BoardRegion, Vector2Int>();

    public Settings Config { get; private set; }

    public IReadOnlyList<Vector2Int> Cells => orderedCells;

    public static readonly Vector2Int[] Directions =
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.right, Vector2Int.left
    };

    // =========================================================
    // QUERIES
    // =========================================================

    public bool Contains(Vector2Int cell) => walls.ContainsKey(cell);

    public BoardRegion RegionOf(Vector2Int cell) => regions[cell];

    public int WallMask(Vector2Int cell) => walls[cell];

    public static int Bit(Vector2Int direction)
    {
        if (direction == Vector2Int.up) return WallNorth;
        if (direction == Vector2Int.down) return WallSouth;
        if (direction == Vector2Int.right) return WallEast;
        if (direction == Vector2Int.left) return WallWest;
        return 0;
    }

    public bool HasWall(Vector2Int cell, Vector2Int direction)
    {
        return !walls.TryGetValue(cell, out int mask) || (mask & Bit(direction)) != 0;
    }

    /// <summary>True if a step from cell in direction is allowed (no wall on either side, target exists).</summary>
    public bool CanMove(Vector2Int cell, Vector2Int direction)
    {
        Vector2Int next = cell + direction;
        return Contains(cell) && Contains(next) && !HasWall(cell, direction) && !HasWall(next, -direction);
    }

    public List<Vector2Int> CellsIn(BoardRegion region)
    {
        List<Vector2Int> result = new List<Vector2Int>();

        foreach (Vector2Int cell in orderedCells)
        {
            if (regions[cell] == region)
                result.Add(cell);
        }

        return result;
    }

    public Vector2Int CenterExit(BoardRegion side)
    {
        Settings s = Config;
        int midX = s.centerWidth / 2;
        int midY = s.centerHeight / 2;

        switch (side)
        {
            case BoardRegion.North: return new Vector2Int(midX, s.centerHeight - 1);
            case BoardRegion.South: return new Vector2Int(midX, 0);
            case BoardRegion.East: return new Vector2Int(s.centerWidth - 1, midY);
            case BoardRegion.West: return new Vector2Int(0, midY);
            default: return new Vector2Int(midX, midY);
        }
    }

    // =========================================================
    // GENERATION
    // =========================================================

    public static BoardLayout Generate(Settings settings, int seed)
    {
        BoardLayout layout = new BoardLayout { Config = settings };
        System.Random rng = new System.Random(seed);

        // Central board.
        layout.AddRect(BoardRegion.Center, 0, 0, settings.centerWidth, settings.centerHeight);
        layout.CarveRegion(BoardRegion.Center, new Vector2Int(settings.centerWidth / 2, settings.centerHeight / 2), settings.centerExtraPathChance, rng);

        // Rooms, centred on the middle exit of each side.
        if (settings.north) layout.AddRoom(BoardRegion.North, rng);
        if (settings.south) layout.AddRoom(BoardRegion.South, rng);
        if (settings.east) layout.AddRoom(BoardRegion.East, rng);
        if (settings.west) layout.AddRoom(BoardRegion.West, rng);

        return layout;
    }

    private void AddRoom(BoardRegion side, System.Random rng)
    {
        Settings s = Config;
        Vector2Int exit = CenterExit(side);
        int half = s.roomWidth / 2;
        int x, y, w, h;
        Vector2Int entrance;
        Vector2Int towardsRoom;

        switch (side)
        {
            case BoardRegion.North:
                x = exit.x - half; y = s.centerHeight; w = s.roomWidth; h = s.roomDepth;
                towardsRoom = Vector2Int.up;
                break;
            case BoardRegion.South:
                x = exit.x - half; y = -s.roomDepth; w = s.roomWidth; h = s.roomDepth;
                towardsRoom = Vector2Int.down;
                break;
            case BoardRegion.East:
                x = s.centerWidth; y = exit.y - half; w = s.roomDepth; h = s.roomWidth;
                towardsRoom = Vector2Int.right;
                break;
            default:
                x = -s.roomDepth; y = exit.y - half; w = s.roomDepth; h = s.roomWidth;
                towardsRoom = Vector2Int.left;
                break;
        }

        entrance = exit + towardsRoom;

        AddRect(side, x, y, w, h);
        CarveRegion(side, new Vector2Int(x + w / 2, y + h / 2), s.roomExtraPathChance, rng);

        // The only link between the room and the central board.
        Open(exit, towardsRoom);
        Entrances[side] = entrance;
    }

    private void AddRect(BoardRegion region, int x0, int y0, int w, int h)
    {
        for (int x = x0; x < x0 + w; x++)
        {
            for (int y = y0; y < y0 + h; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                walls[cell] = AllWalls;
                regions[cell] = region;
                orderedCells.Add(cell);
            }
        }
    }

    /// <summary>Spanning-tree maze (iterative DFS) inside the region, plus random extra openings.</summary>
    private void CarveRegion(BoardRegion region, Vector2Int start, float extraPathChance, System.Random rng)
    {
        HashSet<Vector2Int> visited = new HashSet<Vector2Int> { start };
        Stack<Vector2Int> stack = new Stack<Vector2Int>();
        stack.Push(start);

        List<Vector2Int> options = new List<Vector2Int>(4);

        while (stack.Count > 0)
        {
            Vector2Int current = stack.Peek();
            options.Clear();

            foreach (Vector2Int d in Directions)
            {
                Vector2Int next = current + d;

                if (InRegion(next, region) && !visited.Contains(next))
                    options.Add(d);
            }

            if (options.Count == 0)
            {
                stack.Pop();
                continue;
            }

            Vector2Int chosen = options[rng.Next(options.Count)];
            Open(current, chosen);
            visited.Add(current + chosen);
            stack.Push(current + chosen);
        }

        // Extra paths (only east/north so each inner edge is rolled once).
        foreach (Vector2Int cell in orderedCells)
        {
            if (regions[cell] != region)
                continue;

            if (InRegion(cell + Vector2Int.right, region) && rng.NextDouble() < extraPathChance)
                Open(cell, Vector2Int.right);

            if (InRegion(cell + Vector2Int.up, region) && rng.NextDouble() < extraPathChance)
                Open(cell, Vector2Int.up);
        }
    }

    private bool InRegion(Vector2Int cell, BoardRegion region)
    {
        return regions.TryGetValue(cell, out BoardRegion r) && r == region;
    }

    /// <summary>Removes the wall between cell and its neighbour on BOTH sides.</summary>
    private void Open(Vector2Int cell, Vector2Int direction)
    {
        Vector2Int next = cell + direction;

        if (!Contains(cell) || !Contains(next))
            return;

        walls[cell] &= ~Bit(direction);
        walls[next] &= ~Bit(-direction);
    }

    // =========================================================
    // CHECKS (used by tests and the runtime audit)
    // =========================================================

    /// <summary>Cells reachable from the start by legal moves.</summary>
    public HashSet<Vector2Int> Reachable(Vector2Int start)
    {
        HashSet<Vector2Int> seen = new HashSet<Vector2Int> { start };
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            Vector2Int cell = queue.Dequeue();

            foreach (Vector2Int d in Directions)
            {
                if (CanMove(cell, d) && seen.Add(cell + d))
                    queue.Enqueue(cell + d);
            }
        }

        return seen;
    }

    /// <summary>Number of edges where the two tiles disagree about the shared wall (must be 0).</summary>
    public int CountAsymmetricWalls()
    {
        int count = 0;

        foreach (Vector2Int cell in orderedCells)
        {
            foreach (Vector2Int d in Directions)
            {
                Vector2Int next = cell + d;

                if (Contains(next) && HasWall(cell, d) != HasWall(next, -d))
                    count++;
            }
        }

        return count;
    }
}
