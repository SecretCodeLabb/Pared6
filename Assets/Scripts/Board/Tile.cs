using UnityEngine;

public class Tile : MonoBehaviour
{
    /// <summary>Global grid coordinate (unique across the central board and the rooms).</summary>
    public Vector2Int gridPosition;

    public BoardRegion region = BoardRegion.Center;

    public bool walkable = true;

    public bool northWall;
    public bool southWall;
    public bool eastWall;
    public bool westWall;

    private TileView tileView;

    private void Awake()
    {
        tileView = GetComponent<TileView>();
    }

    public void Initialize(Vector2Int position)
    {
        gridPosition = position;
    }

    public void UpdateVisual()
    {
        if (tileView != null)
        {
            tileView.UpdateWalls(this);
        }
    }

    public bool HasWall(Vector2Int direction)
    {
        if (direction == Vector2Int.up)
            return northWall;

        if (direction == Vector2Int.down)
            return southWall;

        if (direction == Vector2Int.right)
            return eastWall;

        if (direction == Vector2Int.left)
            return westWall;

        return true;
    }
}