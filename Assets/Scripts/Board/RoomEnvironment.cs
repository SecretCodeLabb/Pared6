using UnityEngine;

/// <summary>
/// Fixed-look side room (floor, walls, decoration) placed over a square of board tiles.
/// Movement still uses the tiles underneath; this prefab is visual only.
///
/// Authoring convention: the entrance is in the middle of the local -Z wall and the room
/// extends towards +Z (the back, where the mission tile is). MapGenerator rotates the prefab
/// so the entrance faces the central board and, with fitToTiles, scales it so its footprint
/// covers exactly sizeInTiles x sizeInTiles tiles.
/// </summary>
public class RoomEnvironment : MonoBehaviour
{
    [Tooltip("Room size in board tiles (square). Every side room uses the default environment's size.")]
    [Min(3)] public int sizeInTiles = 7;

    [Tooltip("Scale the prefab at spawn so its floor covers exactly sizeInTiles x sizeInTiles tiles.")]
    public bool fitToTiles = true;
}
