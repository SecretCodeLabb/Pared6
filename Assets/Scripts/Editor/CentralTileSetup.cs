using UnityEditor;
using UnityEngine;

/// <summary>
/// PPOP/Board/Build Central Tile (Nivel 0): wraps the "nivel 0" model (one 2x2 cell: floor + 4 walls)
/// in a tile prefab with Tile + TileView, so MapGenerator.centralTilePrefab can build the central maze
/// with it. The model's walls are switched on/off per cell like the old tile (TileView).
/// Model convention: floor centred on the origin, walls at +Z (north), +X (east), -X (west), -Z (south).
/// Run it again after re-exporting the FBX.
/// </summary>
public static class CentralTileSetup
{
    private const string ModelPath = "Assets/Art/Board/Nivel0/nivel 0.fbx";
    private const string PrefabPath = "Assets/Prefabs/Board/Tile_Nivel0.prefab";

    // Child names in the FBX for each wall.
    private const string NorthWall = "Cube.001";
    private const string EastWall = "Cube.002";
    private const string WestWall = "Cube.003";
    private const string SouthWall = "Cube.004";

    [MenuItem("PPOP/Board/Build Central Tile (Nivel 0)")]
    public static GameObject Build()
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);

        if (model == null)
        {
            Debug.LogError("[PPOP] No se encontró el modelo " + ModelPath);
            return null;
        }

        GameObject root = new GameObject("Tile_Nivel0");
        root.AddComponent<Tile>();
        TileView view = root.AddComponent<TileView>();

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
        instance.name = "Model";

        view.northWall = Find(instance.transform, NorthWall);
        view.eastWall = Find(instance.transform, EastWall);
        view.westWall = Find(instance.transform, WestWall);
        view.southWall = Find(instance.transform, SouthWall);

        if (view.northWall == null || view.eastWall == null || view.westWall == null || view.southWall == null)
        {
            Object.DestroyImmediate(root);
            Debug.LogError("[PPOP] El modelo no tiene los muros esperados (Cube.001-004).");
            return null;
        }

        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PrefabPath));
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        Debug.Log("[PPOP] Casilla central creada: " + PrefabPath);
        return prefab;
    }

    private static GameObject Find(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == name)
                return t.gameObject;
        }

        return null;
    }
}
