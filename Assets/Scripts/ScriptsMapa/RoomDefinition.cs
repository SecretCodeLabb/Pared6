using UnityEngine;

[CreateAssetMenu(
    fileName = "RoomDefinition",
    menuName = "ParedPared/Room Definition"
)]
public class RoomDefinition : ScriptableObject
{
    [Header("Room Info")]
    public string roomName;

    [Header("Prefab")]
    public GameObject roomPrefab;

    [Header("Size")]
    public int width = 5;
    public int height = 5;

    [Header("Entrance")]
    public Vector2Int entrancePosition;
}