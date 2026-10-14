using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// PPOP/Rooms/Build Test Room Prefab: turns the test model (Art/Rooms/TestEntorno/eltiestoese.fbx, a closed
/// 7x7-tile box) into a RoomEnvironment prefab for the side rooms. The model is baked centred on
/// the prefab origin and a one-tile doorway is cut in the middle of its -Z wall (the entrance).
/// Run it again after changing the FBX. Real per-character rooms should be modelled with the same
/// convention (door on -Z, room towards +Z) and assigned in RoomBuilder.environmentPrefab.
/// </summary>
public static class RoomEnvironmentSetup
{
    private const string Folder = "Assets/Art/Rooms/TestEntorno";
    private const string ModelPath = Folder + "/eltiestoese.fbx";
    private const string MeshPath = Folder + "/eltiestoese_Room.asset";
    // Own name: Prefabs/Rooms/Room_TestEntorno.prefab is the room actually used (built from Room_EntornoPersonajes.fbx).
    private const string PrefabPath = "Assets/Prefabs/Rooms/Room_TestEntorno_eltiestoese.prefab";
    private const int SizeInTiles = 7;

    [MenuItem("PPOP/Rooms/Build Test Room Prefab (eltiestoese)")]
    public static GameObject BuildTestRoom()
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        MeshFilter source = model != null ? model.GetComponentInChildren<MeshFilter>() : null;

        if (source == null || source.sharedMesh == null)
        {
            Debug.LogError("[PPOP] No se encontró la malla de " + ModelPath);
            return null;
        }

        Mesh mesh = BakeRoomMesh(source);
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);

        if (existing != null)
        {
            EditorUtility.CopySerialized(mesh, existing);
            mesh = existing;
        }
        else
        {
            AssetDatabase.CreateAsset(mesh, MeshPath);
        }

        GameObject root = new GameObject("Room_TestEntorno");
        RoomEnvironment environment = root.AddComponent<RoomEnvironment>();
        environment.sizeInTiles = SizeInTiles;
        environment.fitToTiles = true;

        GameObject visual = new GameObject("Model");
        visual.transform.SetParent(root.transform, false);
        visual.AddComponent<MeshFilter>().sharedMesh = mesh;
        visual.AddComponent<MeshRenderer>().sharedMaterials = source.GetComponent<MeshRenderer>().sharedMaterials;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();

        Debug.Log($"[PPOP] Prefab de habitación creado: {PrefabPath} ({SizeInTiles}x{SizeInTiles} casillas, puerta en -Z).");
        return prefab;
    }

    /// <summary>Copy of the model mesh in prefab space: centred on XZ, without the doorway triangles.</summary>
    private static Mesh BakeRoomMesh(MeshFilter source)
    {
        Mesh input = source.sharedMesh;
        Matrix4x4 toRoot = source.transform.localToWorldMatrix;

        Vector3[] vertices = input.vertices;
        Vector3[] normals = input.normals;
        Vector2[] uvs = input.uv;

        for (int i = 0; i < vertices.Length; i++)
            vertices[i] = toRoot.MultiplyPoint3x4(vertices[i]);

        for (int i = 0; i < normals.Length; i++)
            normals[i] = toRoot.MultiplyVector(normals[i]).normalized;

        Bounds bounds = new Bounds(vertices[0], Vector3.zero);

        foreach (Vector3 v in vertices)
            bounds.Encapsulate(v);

        Vector3 shift = new Vector3(-bounds.center.x, 0f, -bounds.center.z);

        for (int i = 0; i < vertices.Length; i++)
            vertices[i] += shift;

        float halfDepth = bounds.extents.z;
        float doorHalfWidth = bounds.size.x / SizeInTiles * 0.5f + 0.01f;
        float wallBand = bounds.size.z / SizeInTiles * 0.25f;
        float floorTop = bounds.min.y + bounds.size.y * 0.1f;

        Mesh output = new Mesh { name = "eltiestoese_Room" };
        output.vertices = vertices;

        if (normals.Length == vertices.Length)
            output.normals = normals;

        if (uvs.Length == vertices.Length)
            output.uv = uvs;

        output.subMeshCount = input.subMeshCount;

        for (int sub = 0; sub < input.subMeshCount; sub++)
        {
            int[] triangles = input.GetTriangles(sub);
            List<int> kept = new List<int>(triangles.Length);

            for (int t = 0; t < triangles.Length; t += 3)
            {
                Vector3 a = vertices[triangles[t]], b = vertices[triangles[t + 1]], c = vertices[triangles[t + 2]];

                bool inDoorColumn = Mathf.Abs(a.x) <= doorHalfWidth && Mathf.Abs(b.x) <= doorHalfWidth && Mathf.Abs(c.x) <= doorHalfWidth;
                bool onEntranceWall = Mathf.Max(a.z, b.z, c.z) <= -halfDepth + wallBand;
                bool aboveFloor = Mathf.Max(a.y, b.y, c.y) > floorTop;

                if (inDoorColumn && onEntranceWall && aboveFloor)
                    continue;

                kept.Add(triangles[t]);
                kept.Add(triangles[t + 1]);
                kept.Add(triangles[t + 2]);
            }

            output.SetTriangles(kept, sub);
        }

        output.RecalculateBounds();
        output.RecalculateTangents();
        return output;
    }
}
