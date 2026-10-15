using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// PPOP/Minigames/Build Puppy Catch: creates placeholder pixel-art PNGs (only the missing ones, so
/// replaced art is kept), the catch minigame prefab, and assigns it to Mission_Puppy.
/// To change the art just overwrite the PNGs in Assets/Art/Minigames/Puppy (same names):
/// Puppy (player), Llamada + Llaves (points), Recibo (+1 s), Aguardiente (takes points) · Fondo.
/// </summary>
public static class PuppyCatchSetup
{
    private const string ArtFolder = "Assets/Art/Minigames/Puppy";
    private const string PrefabFolder = "Assets/Prefabs/Minigames";
    private const string PrefabPath = PrefabFolder + "/Minigame_Puppy.prefab";
    private const string MissionPath = "Assets/ScriptableObjects/Missions/Mission_Puppy.asset";

    // '#' main colour, 'o' second colour, 'k' dark details, '.' transparent. Rows top to bottom.
    private static readonly string[] PuppyArt =
    {
        "..oo.......oo...",
        ".ooo#######ooo..",
        ".oo#########oo..",
        "..###k###k###...",
        "..###########...",
        "..####kkk####...",
        "...#########....",
        "....#######.....",
        "..###########...",
        ".#############..",
        ".#############.o",
        ".#############o.",
        "..###########...",
        "..##..###..##...",
        "..##..###..##...",
        ".kk..kk..kk..kk.",
    };

    private static readonly string[] Ball =
    {
        "...####...",
        ".##oo####.",
        ".#oo#####.",
        "##########",
        "##########",
        "oooooooooo",
        "##########",
        ".########.",
        ".########.",
        "...####...",
    };

    private static readonly string[] Bone =
    {
        "##......##",
        "###....###",
        ".########.",
        "..######..",
        ".########.",
        "###....###",
        "##......##",
    };

    private static readonly string[] Cookie =
    {
        "..######..",
        ".###k####.",
        "###k###k##",
        "##########",
        "#k#####k##",
        "#####k####",
        ".#k######.",
        "..######..",
    };

    private static readonly string[] Plush =
    {
        "#..##..#..",
        "##.##.##..",
        "########..",
        "#k####k#..",
        "##oooo##..",
        "########..",
        ".######...",
        "########..",
        "##.##.##..",
    };

    private static readonly string[] Shoe =
    {
        "....####...",
        "....#oo#...",
        "....####...",
        "...######..",
        "..########.",
        "###########",
        "kkkkkkkkkkk",
    };

    private static readonly string[] Chocolate =
    {
        "oooooooooo",
        "o########o",
        "o#k##k##ko",
        "o########o",
        "o#k##k##ko",
        "o########o",
        "o#k##k##ko",
        "oooooooooo",
    };

    [MenuItem("PPOP/Minigames/Build Puppy Catch")]
    public static GameObject Build()
    {
        Directory.CreateDirectory(ArtFolder);
        Directory.CreateDirectory(PrefabFolder);

        // Placeholders only for missing PNGs (the final art lives in the same files).
        Color dark = new Color(0.12f, 0.08f, 0.06f);
        Sprite puppy = Art("Puppy", PuppyArt, new Color(0.96f, 0.82f, 0.88f), new Color(0.72f, 0.45f, 0.5f), dark);
        Sprite call = Art("Llamada", Ball, new Color(1f, 0.55f, 0.75f), Color.white, dark);
        Sprite keys = Art("Llaves", Bone, new Color(0.85f, 0.7f, 0.45f), Color.white, dark);
        Sprite receipt = Art("Recibo", Cookie, new Color(0.95f, 0.95f, 0.95f), Color.white, dark);
        Sprite aguardiente = Art("Aguardiente", Shoe, new Color(0.6f, 0.8f, 0.85f), Color.white, dark);
        Sprite background = Background("Fondo");

        GameObject root = new GameObject("Minigame_Puppy", typeof(RectTransform));
        PuppyCatchMinigame minigame = root.AddComponent<PuppyCatchMinigame>();

        SerializedObject so = new SerializedObject(minigame);
        so.FindProperty("background").objectReferenceValue = background;
        so.FindProperty("puppySprite").objectReferenceValue = puppy;

        // Calls and keys add points, receipts add a second, aguardiente takes points away.
        SerializedProperty items = so.FindProperty("items");
        items.arraySize = 4;
        SetItem(items.GetArrayElementAtIndex(0), "Llamada", call, 1, 0f, new Vector2(64f, 53f), 1.2f);
        SetItem(items.GetArrayElementAtIndex(1), "Llaves", keys, 2, 0f, new Vector2(56f, 65f), 0.8f);
        SetItem(items.GetArrayElementAtIndex(2), "Recibo", receipt, 0, 1f, new Vector2(46f, 68f), 0.6f);
        SetItem(items.GetArrayElementAtIndex(3), "Aguardiente", aguardiente, -2, 0f, new Vector2(36f, 80f), 1f);

        // The girl stands on the bar counter of the background.
        so.FindProperty("puppySize").vector2Value = new Vector2(72f, 132f);
        so.FindProperty("groundHeight").floatValue = 80f;

        so.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        MissionDefinition mission = AssetDatabase.LoadAssetAtPath<MissionDefinition>(MissionPath);

        if (mission != null)
        {
            mission.title = "Escape de la fiesta";
            mission.description = "Atrapa las llamadas y las llaves para salir de la fiesta antes de 30 s. Los recibos dan tiempo extra y el aguardiente te quita puntos.";
            mission.minigamePrefab = prefab;
            EditorUtility.SetDirty(mission);
        }
        else
        {
            Debug.LogError("[PPOP] No se encontró " + MissionPath);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[PPOP] Minijuego de Puppy: {PrefabPath} (arte en {ArtFolder}).");
        return prefab;
    }

    private static void SetItem(SerializedProperty item, string name, Sprite sprite, int points, float seconds, Vector2 size, float weight)
    {
        item.FindPropertyRelative("name").stringValue = name;
        item.FindPropertyRelative("sprite").objectReferenceValue = sprite;
        item.FindPropertyRelative("points").intValue = points;
        item.FindPropertyRelative("seconds").floatValue = seconds;
        item.FindPropertyRelative("size").vector2Value = size;
        item.FindPropertyRelative("weight").floatValue = weight;
    }

    /// <summary>Writes the PNG only if it does not exist (replaced art is never overwritten).</summary>
    private static Sprite Art(string name, string[] rows, Color main, Color second, Color dark)
    {
        string path = $"{ArtFolder}/{name}.png";

        if (!File.Exists(path))
        {
            int width = 0;

            foreach (string row in rows)
                width = Mathf.Max(width, row.Length);

            int height = rows.Length;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);

            for (int y = 0; y < height; y++)
            {
                string row = rows[height - 1 - y];

                for (int x = 0; x < width; x++)
                {
                    char c = x < row.Length ? row[x] : '.';
                    texture.SetPixel(x, y, c == '#' ? main : c == 'o' ? second : c == 'k' ? dark : Color.clear);
                }
            }

            WritePng(texture, path);
        }

        return ImportSprite(path);
    }

    private static Sprite Background(string name)
    {
        string path = $"{ArtFolder}/{name}.png";

        if (!File.Exists(path))
        {
            const int width = 220, height = 118;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // Sky gradient, two clouds and a grass strip at the bottom.
                    Color c = Color.Lerp(new Color(0.62f, 0.84f, 1f), new Color(0.38f, 0.66f, 0.98f), y / (float)height);
                    float c1 = ((x - 50f) * (x - 50f)) / 500f + ((y - 90f) * (y - 90f)) / 40f;
                    float c2 = ((x - 160f) * (x - 160f)) / 700f + ((y - 75f) * (y - 75f)) / 50f;

                    if (c1 < 1f || c2 < 1f)
                        c = new Color(0.97f, 0.98f, 1f);

                    if (y < 7)
                        c = (x / 3 + y) % 2 == 0 ? new Color(0.32f, 0.62f, 0.28f) : new Color(0.38f, 0.7f, 0.32f);

                    texture.SetPixel(x, y, c);
                }
            }

            WritePng(texture, path);
        }

        return ImportSprite(path);
    }

    private static void WritePng(Texture2D texture, string path)
    {
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path);
    }

    private static Sprite ImportSprite(string path)
    {
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);

        // Only placeholders get configured; replaced art keeps its own import settings.
        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
