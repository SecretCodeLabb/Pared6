using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates placeholder room themes (one per character) to show how side rooms change look:
/// coloured floor/walls (tile prefab variant of TilesRoom) and simple primitive props.
/// Only fills characters whose CharacterData.roomPrefab is empty — never overwrites your work.
/// Replace the materials / props / tile prefab of Assets/Prefabs/Rooms/&lt;Character&gt; with real 3D art.
/// PPOP/Create Example Room Themes (also run by PPOP/Setup Prototype).
/// </summary>
public static class RoomThemeSetup
{
    private const string RoomsDir = "Assets/Prefabs/Rooms";
    private const string BaseRoomPath = "Assets/Prefabs/Room_01.prefab";
    private const string BaseTilePath = "Assets/Prefabs/TilesRoom.prefab";

    private struct Prop
    {
        public PrimitiveType shape;
        public Vector3 scale;
        public Vector3 rotation;
        public Color color;
        public string name;
    }

    private struct Theme
    {
        public Color floor;
        public Color wall;
        public Prop[] props;
    }

    // Placeholder looks by catalog index (Chismosa, Puppy, Vagabundo, Skater, Trabajador, Estudiante).
    private static Theme ThemeFor(int index)
    {
        switch (index % 6)
        {
            case 0: // gossip: pink, bubbles and phones
                return new Theme
                {
                    floor = new Color(0.96f, 0.72f, 0.82f), wall = new Color(0.85f, 0.35f, 0.62f),
                    props = new[]
                    {
                        P("Burbuja", PrimitiveType.Sphere, new Vector3(0.45f, 0.45f, 0.45f), Vector3.zero, new Color(1f, 0.9f, 0.95f)),
                        P("Telefono", PrimitiveType.Cube, new Vector3(0.18f, 0.4f, 0.08f), new Vector3(0f, 0f, 0f), new Color(0.55f, 0.2f, 0.7f))
                    }
                };
            case 1: // puppy: blue, bones and balls
                return new Theme
                {
                    floor = new Color(0.70f, 0.85f, 0.98f), wall = new Color(0.30f, 0.50f, 0.90f),
                    props = new[]
                    {
                        P("Hueso", PrimitiveType.Capsule, new Vector3(0.15f, 0.3f, 0.15f), new Vector3(0f, 0f, 90f), Color.white),
                        P("Pelota", PrimitiveType.Sphere, new Vector3(0.3f, 0.3f, 0.3f), Vector3.zero, new Color(0.9f, 0.2f, 0.2f))
                    }
                };
            case 2: // vagabundo: brown, boxes and cans
                return new Theme
                {
                    floor = new Color(0.55f, 0.42f, 0.30f), wall = new Color(0.35f, 0.25f, 0.18f),
                    props = new[]
                    {
                        P("Caja", PrimitiveType.Cube, new Vector3(0.45f, 0.35f, 0.4f), Vector3.zero, new Color(0.75f, 0.6f, 0.4f)),
                        P("Lata", PrimitiveType.Cylinder, new Vector3(0.18f, 0.15f, 0.18f), Vector3.zero, new Color(0.6f, 0.6f, 0.65f))
                    }
                };
            case 3: // skater: asphalt, cones and ramps
                return new Theme
                {
                    floor = new Color(0.45f, 0.45f, 0.48f), wall = new Color(0.95f, 0.55f, 0.15f),
                    props = new[]
                    {
                        P("Cono", PrimitiveType.Cylinder, new Vector3(0.2f, 0.3f, 0.2f), Vector3.zero, new Color(1f, 0.5f, 0.05f)),
                        P("Rampa", PrimitiveType.Cube, new Vector3(0.6f, 0.08f, 0.35f), new Vector3(0f, 0f, 15f), new Color(0.95f, 0.85f, 0.2f))
                    }
                };
            case 4: // tired worker: office beige, cups and papers
                return new Theme
                {
                    floor = new Color(0.85f, 0.80f, 0.68f), wall = new Color(0.45f, 0.52f, 0.62f),
                    props = new[]
                    {
                        P("Taza", PrimitiveType.Cylinder, new Vector3(0.18f, 0.12f, 0.18f), Vector3.zero, new Color(0.95f, 0.95f, 0.95f)),
                        P("Papeles", PrimitiveType.Cube, new Vector3(0.4f, 0.04f, 0.3f), Vector3.zero, new Color(0.98f, 0.98f, 0.9f))
                    }
                };
            default: // student: green, books
                return new Theme
                {
                    floor = new Color(0.62f, 0.85f, 0.60f), wall = new Color(0.20f, 0.50f, 0.30f),
                    props = new[]
                    {
                        P("LibroRojo", PrimitiveType.Cube, new Vector3(0.35f, 0.1f, 0.25f), Vector3.zero, new Color(0.85f, 0.2f, 0.2f)),
                        P("LibroAzul", PrimitiveType.Cube, new Vector3(0.3f, 0.12f, 0.22f), new Vector3(0f, 25f, 0f), new Color(0.2f, 0.35f, 0.85f))
                    }
                };
        }
    }

    private static Prop P(string name, PrimitiveType shape, Vector3 scale, Vector3 rotation, Color color)
    {
        return new Prop { name = name, shape = shape, scale = scale, rotation = rotation, color = color };
    }

    [MenuItem("PPOP/Create Example Room Themes")]
    public static void CreateAll()
    {
        CharacterCatalog catalog = AssetDatabase.LoadAssetAtPath<CharacterCatalog>("Assets/Resources/CharacterCatalog.asset");
        RoomBuilder baseRoom = AssetDatabase.LoadAssetAtPath<GameObject>(BaseRoomPath)?.GetComponent<RoomBuilder>();
        GameObject baseTile = AssetDatabase.LoadAssetAtPath<GameObject>(BaseTilePath);

        if (catalog == null || baseRoom == null || baseTile == null)
        {
            Debug.LogError("[PPOP] Room themes: CharacterCatalog, Room_01 or TilesRoom missing.");
            return;
        }

        PrototypeSetup.EnsureFolder(RoomsDir);
        floorTop = MeasureFloorTop(baseTile);

        for (int i = 0; i < catalog.characters.Count; i++)
        {
            CharacterData character = catalog.characters[i];

            if (character == null || character.roomPrefab != null)
                continue;

            character.roomPrefab = CreateTheme(character, ThemeFor(i), baseRoom, baseTile);
            EditorUtility.SetDirty(character);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[PPOP] Room themes listos.");
    }

    private static GameObject CreateTheme(CharacterData character, Theme theme, RoomBuilder baseRoom, GameObject baseTile)
    {
        string safe = SafeName(character.characterName);
        string dir = RoomsDir + "/" + safe;
        PrototypeSetup.EnsureFolder(dir);

        // Tile variant with this character's floor and wall colours (walls stay procedural).
        Material floor = CopyMaterial(baseTile, "Floor", dir + "/Mat_" + safe + "_Suelo.mat", theme.floor);
        Material wall = CopyMaterial(baseTile, "Wall_North", dir + "/Mat_" + safe + "_Paredes.mat", theme.wall);

        GameObject tileInstance = (GameObject)PrefabUtility.InstantiatePrefab(baseTile);

        foreach (Renderer renderer in tileInstance.GetComponentsInChildren<Renderer>(true))
            renderer.sharedMaterial = renderer.name == "Floor" ? floor : wall;

        GameObject tileVariant = PrefabUtility.SaveAsPrefabAsset(tileInstance, dir + "/RoomTile_" + safe + ".prefab");
        Object.DestroyImmediate(tileInstance);

        // Simple primitive props.
        List<GameObject> props = new List<GameObject>();

        foreach (Prop prop in theme.props)
            props.Add(CreateProp(prop, dir, safe));

        // Room prefab: same size/paths as Room_01, only the look changes.
        GameObject room = new GameObject("Room_" + safe);
        RoomBuilder builder = room.AddComponent<RoomBuilder>();
        builder.width = baseRoom.width;
        builder.height = baseRoom.height;
        builder.tileSize = baseRoom.tileSize;
        builder.southEntrance = baseRoom.southEntrance;
        builder.extraPathChance = baseRoom.extraPathChance;
        builder.tilePrefab = tileVariant;
        builder.themeName = "Cuarto de " + character.characterName;
        builder.themeLabelColor = character.boardColor.a > 0f ? Color.Lerp(character.boardColor, Color.white, 0.3f) : Color.white;
        builder.decorations = props;
        builder.decorationChance = 0.4f;
        builder.decorationScale = new Vector2(1.5f, 2.2f);

        GameObject roomPrefab = PrefabUtility.SaveAsPrefabAsset(room, dir + "/Room_" + safe + ".prefab");
        Object.DestroyImmediate(room);
        return roomPrefab;
    }

    private static GameObject CreateProp(Prop prop, string dir, string safe)
    {
        Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.SetColor("_BaseColor", prop.color);
        AssetDatabase.CreateAsset(material, dir + "/Mat_" + safe + "_" + prop.name + ".mat");

        // Root sits on the floor; the shape is lifted so it rests on it.
        GameObject root = new GameObject("Deco_" + prop.name);
        GameObject shape = GameObject.CreatePrimitive(prop.shape);
        shape.name = prop.name;
        shape.transform.SetParent(root.transform, false);
        shape.transform.localScale = prop.scale;
        shape.transform.localRotation = Quaternion.Euler(prop.rotation);
        shape.GetComponent<Renderer>().sharedMaterial = material;
        Object.DestroyImmediate(shape.GetComponent<Collider>());

        Bounds bounds = shape.GetComponent<Renderer>().bounds;
        shape.transform.localPosition = new Vector3(0f, floorTop - bounds.min.y, 0f);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, dir + "/Deco_" + safe + "_" + prop.name + ".prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static float floorTop = 0.1f;

    /// <summary>Height of the top of the tile floor, so props rest on it.</summary>
    private static float MeasureFloorTop(GameObject tilePrefab)
    {
        GameObject temp = (GameObject)PrefabUtility.InstantiatePrefab(tilePrefab);
        temp.transform.position = Vector3.zero;
        float top = 0.1f;

        foreach (Renderer renderer in temp.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.name == "Floor")
                top = renderer.bounds.max.y;
        }

        Object.DestroyImmediate(temp);
        return top;
    }

    private static Material CopyMaterial(GameObject tile, string rendererName, string path, Color color)
    {
        Material source = null;

        foreach (Renderer renderer in tile.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.name == rendererName)
                source = renderer.sharedMaterial;
        }

        Material material = source != null ? new Material(source) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.SetColor("_BaseColor", color);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static string SafeName(string name)
    {
        string result = "";

        foreach (char c in name)
            result += char.IsLetterOrDigit(c) ? c.ToString() : "";

        return string.IsNullOrEmpty(result) ? "Personaje" : result;
    }
}
