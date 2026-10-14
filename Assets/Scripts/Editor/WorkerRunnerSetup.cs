using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// PPOP/Minigames/Build Trabajador Runner: creates placeholder pixel-art PNGs (only the missing ones,
/// so replaced art is kept), the runner minigame prefab, and assigns it to Mission_Trabajadorcansado.
/// To change the art just overwrite the PNGs in Assets/Art/Minigames/Trabajador (same names):
/// Trabajador, Obstaculo_Cafe, Obstaculo_Computadora, Obstaculo_Escritorio, Fondo, Suelo.
/// </summary>
public static class WorkerRunnerSetup
{
    private const string ArtFolder = "Assets/Art/Minigames/Trabajador";
    private const string PrefabFolder = "Assets/Prefabs/Minigames";
    private const string PrefabPath = PrefabFolder + "/Minigame_Trabajador.prefab";
    private const string MissionPath = "Assets/ScriptableObjects/Missions/Mission_Trabajadorcansado.asset";

    // Palette: '#' suit, 'o' skin, 't' tie, 'w' shirt, 'h' hair, 'k' shoes, '.' transparent.
    private static readonly string[] RunA =
    {
        ".....hhhh.......",
        "....hhhhhh......",
        "....oooooo......",
        "....o.oo.o......",
        "....oooooo......",
        ".....oooo.......",
        "...##wwtw##.....",
        "..###wwtw###....",
        "..o##wwtw##o....",
        "....#wwtw#......",
        "....#######.....",
        "....###.###.....",
        "...###...##.....",
        "..###.....##....",
        ".kk........kk...",
        ".k..........k...",
    };




    // Coffee cup (small), stack of folders (tall), briefcase (wide). '#' main, 'o' detail.
    private static readonly string[] Coffee =
    {
        "..o..o..",
        "...o..o.",
        "########",
        "#oooooo###",
        "#oooooo#.#",
        "#oooooo###",
        "#oooooo#..",
        ".######...",
    };

    private static readonly string[] Folders =
    {
        "oooooooo",
        "#######o",
        "########",
        "oooooooo",
        "########",
        "#######o",
        "oooooooo",
        "########",
        "########",
        "oooooooo",
        "#######o",
        "########",
        "oooooooo",
        "########",
    };

    private static readonly string[] Briefcase =
    {
        "....oooooo....",
        "....o....o....",
        "##############",
        "##############",
        "#######oo#####",
        "oooooooooooooo",
        "##############",
        "##############",
    };

    [MenuItem("PPOP/Minigames/Build Trabajador Runner")]
    public static GameObject Build()
    {
        Directory.CreateDirectory(ArtFolder);
        Directory.CreateDirectory(PrefabFolder);

        // One worker sprite: the minigame animates it (trot, jump tilt, red tint on crash).
        Sprite worker = Worker("Trabajador", RunA);
        Sprite coffee = Art("Obstaculo_Cafe", Coffee, new Color(0.95f, 0.95f, 0.92f), new Color(0.45f, 0.28f, 0.15f));
        Sprite computer = Art("Obstaculo_Computadora", Folders, new Color(0.30f, 0.45f, 0.75f), new Color(0.95f, 0.90f, 0.75f));
        Sprite desk = Art("Obstaculo_Escritorio", Briefcase, new Color(0.40f, 0.24f, 0.12f), new Color(0.85f, 0.70f, 0.25f));
        Sprite background = Background("Fondo");
        Texture2D ground = Ground("Suelo");

        GameObject root = new GameObject("Minigame_Trabajador", typeof(RectTransform));
        WorkerRunnerMinigame minigame = root.AddComponent<WorkerRunnerMinigame>();

        SerializedObject so = new SerializedObject(minigame);
        so.FindProperty("background").objectReferenceValue = background;
        so.FindProperty("groundTexture").objectReferenceValue = ground;
        so.FindProperty("jumpSprite").objectReferenceValue = null;
        so.FindProperty("crashSprite").objectReferenceValue = null;

        SerializedProperty frames = so.FindProperty("runFrames");
        frames.arraySize = 1;
        frames.GetArrayElementAtIndex(0).objectReferenceValue = worker;

        SerializedProperty types = so.FindProperty("obstacles");
        types.arraySize = 3;
        SetObstacle(types.GetArrayElementAtIndex(0), coffee, new Vector2(46f, 46f), 1.2f);
        SetObstacle(types.GetArrayElementAtIndex(1), computer, new Vector2(76f, 58f), 1f);
        SetObstacle(types.GetArrayElementAtIndex(2), desk, new Vector2(100f, 63f), 0.8f);
        so.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        MissionDefinition mission = AssetDatabase.LoadAssetAtPath<MissionDefinition>(MissionPath);

        if (mission != null)
        {
            mission.description = "Corre hacia el viernes saltando los obstáculos de la oficina hasta llegar a 100 puntos.";
            mission.minigamePrefab = prefab;
            EditorUtility.SetDirty(mission);
        }
        else
        {
            Debug.LogError("[PPOP] No se encontró " + MissionPath);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[PPOP] Minijuego del Trabajador: {PrefabPath} (arte en {ArtFolder}).");
        return prefab;
    }

    private static void SetObstacle(SerializedProperty element, Sprite sprite, Vector2 size, float weight)
    {
        element.FindPropertyRelative("sprite").objectReferenceValue = sprite;
        element.FindPropertyRelative("size").vector2Value = size;
        element.FindPropertyRelative("heightAboveGround").floatValue = 0f;
        element.FindPropertyRelative("weight").floatValue = weight;
    }

    private static Sprite Worker(string name, string[] rows)
    {
        return Draw(name, rows, c =>
        {
            switch (c)
            {
                case '#': return new Color(0.22f, 0.25f, 0.32f);
                case 'o': return new Color(0.96f, 0.78f, 0.62f);
                case 't': return new Color(0.80f, 0.15f, 0.20f);
                case 'w': return new Color(0.95f, 0.95f, 0.95f);
                case 'h': return new Color(0.30f, 0.20f, 0.12f);
                case 'k': return new Color(0.10f, 0.08f, 0.06f);
                case 'x': return new Color(0.10f, 0.08f, 0.06f);
                default: return Color.clear;
            }
        });
    }

    private static Sprite Art(string name, string[] rows, Color main, Color detail)
    {
        return Draw(name, rows, c => c == '#' ? main : c == 'o' ? detail : Color.clear);
    }

    /// <summary>Writes the PNG only if it does not exist (replaced art is never overwritten).</summary>
    private static Sprite Draw(string name, string[] rows, System.Func<char, Color> palette)
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
                    texture.SetPixel(x, y, x < row.Length ? palette(row[x]) : Color.clear);
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
            const int width = 220, height = 95;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color wall = new Color(0.86f, 0.84f, 0.76f);
            Color stripe = new Color(0.80f, 0.78f, 0.70f);
            Color window = new Color(0.62f, 0.80f, 0.92f);
            Color frame = new Color(0.55f, 0.52f, 0.46f);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color c = (x / 6) % 2 == 0 ? wall : stripe;
                    int wx = x % 73;

                    // Windows along the wall.
                    if (wx >= 12 && wx <= 48 && y >= 40 && y <= 78)
                        c = wx == 12 || wx == 48 || wx == 30 || y == 40 || y == 78 || y == 59 ? frame : window;

                    texture.SetPixel(x, y, c);
                }
            }

            WritePng(texture, path);
        }

        return ImportSprite(path);
    }

    private static Texture2D Ground(string name)
    {
        string path = $"{ArtFolder}/{name}.png";

        if (!File.Exists(path))
        {
            const int width = 32, height = 10;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color top = new Color(0.30f, 0.25f, 0.22f);
            Color floor = new Color(0.55f, 0.45f, 0.35f);
            Color seam = new Color(0.45f, 0.36f, 0.28f);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color c = y >= height - 2 ? top : (x % 16 == 0 ? seam : floor);
                    texture.SetPixel(x, y, c);
                }
            }

            WritePng(texture, path);
        }

        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);

        if (importer.textureType != TextureImporterType.Default || importer.wrapMode != TextureWrapMode.Repeat || importer.filterMode != FilterMode.Point)
        {
            importer.textureType = TextureImporterType.Default;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
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
