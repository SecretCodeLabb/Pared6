using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fixed-look side room (floor, walls, decoration) placed over a square of board tiles.
/// Movement still uses the tiles underneath; the room's props can block it through
/// <see cref="blockedMoves"/> (walls between tiles, applied by MapGenerator).
///
/// Authoring convention: the entrance is in the middle of the local -Z wall and the room
/// extends towards +Z (the back, where the mission tile is). MapGenerator rotates the prefab
/// so the entrance faces the central board and, with fitToTiles, scales it so its footprint
/// covers exactly sizeInTiles x sizeInTiles tiles.
/// </summary>
public class RoomEnvironment : MonoBehaviour
{
    /// <summary>A move between two neighbouring room cells that a prop blocks (local grid, see below).</summary>
    [Serializable]
    public struct BlockedMove
    {
        public Vector2Int from;
        public Vector2Int to;
    }

    [Tooltip("Room size in board tiles (square). Every side room uses the default environment's size.")]
    [Min(3)] public int sizeInTiles = 7;

    [Tooltip("Scale the prefab at spawn so its floor covers exactly sizeInTiles x sizeInTiles tiles.")]
    public bool fitToTiles = true;

    [Tooltip("Size of one tile in the prefab's own units (the room is centred on its origin).")]
    [Min(0.1f)] public float cellSize = 2f;

    [Tooltip("Moves blocked by the room's props, baked by PPOP/Rooms (local grid: x from -X to +X, " +
             "y from the entrance (0) to the back (sizeInTiles-1)). Only used when fitToTiles is off.")]
    public List<BlockedMove> blockedMoves = new List<BlockedMove>();

    /// <summary>Centre of a local cell in prefab space (room centred on the origin, floor at y = 0).</summary>
    public Vector3 LocalCellCenter(Vector2Int cell)
    {
        float half = (sizeInTiles - 1) * 0.5f;
        return new Vector3((cell.x - half) * cellSize, 0f, (cell.y - half) * cellSize);
    }
}
