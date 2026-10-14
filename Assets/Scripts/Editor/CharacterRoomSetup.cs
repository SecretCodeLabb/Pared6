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
///   - props keep their positions. Movement, events and the mission tile still use the tiles.
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
    }

    private static readonly RoomSource[] Rooms =
    {
        // Skater: the free middle of the model's -Z wall is the door; mission at the back, between the two boxes.
        new RoomSource { Character = "Skater", ModelPath = "Assets/Art/Rooms/Skater/escenario5(Skater).fbx", ShellName = "eltiestoese", Yaw = 0f },
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

        foreach (Collider c in root.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(c);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);

        AssignToCharacter(source.Character, prefab);
        AssetDatabase.SaveAssets();
        Debug.Log($"[PPOP] Habitación de {source.Character}: {prefabPath} (puerta en -Z).");
        return prefab;
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
