using UnityEngine;

public class TileView : MonoBehaviour
{
    public GameObject northWall;
    public GameObject southWall;
    public GameObject eastWall;
    public GameObject westWall;

    public void UpdateWalls(Tile tile)
    {
        northWall.SetActive(tile.northWall);
        southWall.SetActive(tile.southWall);
        eastWall.SetActive(tile.eastWall);
        westWall.SetActive(tile.westWall);
    }
} 