using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// PPOP/Minigames/Build Estudiante Invaders: creates placeholder pixel-art PNGs (only the missing ones,
/// so replaced art is kept), the minigame prefab, and assigns it to Mission_Estudiante.
/// To change the art just overwrite the PNGs in Assets/Art/Minigames/Estudiante (same names).
/// </summary>
public static class StudentInvadersSetup
{
    private const string ArtFolder = "Assets/Art/Minigames/Estudiante";
    private const string PrefabFolder = "Assets/Prefabs/Minigames";
    private const string PrefabPath = PrefabFolder + "/Minigame_Estudiante.prefab";
    private const string MissionPath = "Assets/ScriptableObjects/Missions/Mission_Estudiante.asset";

    // '#' main colour, 'o' second colour, '.' transparent. Rows top to bottom.
    private static readonly string[] Student =
    {
        "......oooo......",
        ".....oooooo.....",
        ".....o#oo#o.....",
        ".....oooooo.....",
        "......oooo......",
        ".......##.......",
        "....########....",
        "...##########...",
        "..###.####.###..",
        "..##..####..##..",
        ".###..####..###.",
        "####.######.####",
        "################",
        "##.##########.##",
        "#...###..###...#",
        "....##....##....",
    };

    private static readonly string[] EnemyA =
    {
        "..#.....#..",
        "...#...#...",
        "..#######..",
        ".##.###.##.",
        "###########",
        "#.#######.#",
        "#.#.....#.#",
        "...##.##...",
    };

    private static readonly string[] EnemyB =
    {
        "....####....",
        ".##########.",
        "############",
        "###..##..###",
        "############",
        "...##..##...",
        "..##.##.##..",
        "##........##",
    };

    private static readonly string[] Boss =
    {
        "........oooooooo........",
        ".....oooooooooooooo.....",
        "...oooooooooooooooooo...",
        "..oooo##oooooooo##oooo..",
        ".ooooo##oooooooo##ooooo.",
        "oooooooooooooooooooooooo",
        "oooooooooooooooooooooooo",
        "ooo#oooo########oooo#ooo",
        "oo###oo##########oo###oo",
        ".ooooooooooooooooooooooo",
        "..ooo..oooo..oooo..ooo..",
        "..oo....oo....oo....oo..",
        ".oo....oo......oo....oo.",
        "oo....oo........oo....oo",
    };

    private static readonly string[] PlayerBullet = { "#", "#", "o", "o" };
    private static readonly string[] EnemyBullet = { ".#.", "#o#", ".#.", "#o#", ".#." };

    [MenuItem("PPOP/Minigames/Build Estudiante Invaders")]
    public static GameObject Build()
    {
        Directory.CreateDirectory(ArtFolder);
        Directory.CreateDirectory(PrefabFolder);

        Sprite player = Art("Estudiante_Nave", Student, new Color(1f, 0.82f, 0.25f), new Color(0.98f, 0.78f, 0.62f));
        Sprite enemyA = Art("Enemigo_FilaA", EnemyA, new Color(0.95f, 0.35f, 0.35f), Color.white);
        Sprite enemyB = Art("Enemigo_FilaB", EnemyB, new Color(0.35f, 0.75f, 1f), Color.white);
        Sprite boss = Art("Jefe", Boss, new Color(1f, 0.95f, 0.3f), new Color(0.72f, 0.25f, 0.85f));
        Sprite playerBullet = Art("Disparo_Estudiante", PlayerBullet, new Color(1f, 1f, 0.7f), new Color(1f, 0.8f, 0.2f));
        Sprite enemyBullet = Art("Disparo_Enemigo", EnemyBullet, new Color(1f, 0.4f, 0.25f), new Color(1f, 0.9f, 0.4f));
        Sprite background = Background("Fondo");

        GameObject root = new GameObject("Minigame_Estudiante", typeof(RectTransform));
        StudentInvadersMinigame minigame = root.AddComponent<StudentInvadersMinigame>();

        SerializedObject so = new SerializedObject(minigame);
        so.FindProperty("background").objectReferenceValue = background;
        so.FindProperty("playerSprite").objectReferenceValue = player;
        so.FindProperty("enemyRowASprite").objectReferenceValue = enemyA;
        so.FindProperty("enemyRowBSprite").objectReferenceValue = enemyB;
        so.FindProperty("bossSprite").objectReferenceValue = boss;
        so.FindProperty("playerBulletSprite").objectReferenceValue = playerBullet;
        so.FindProperty("enemyBulletSprite").objectReferenceValue = enemyBullet;
        so.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        MissionDefinition mission = AssetDatabase.LoadAssetAtPath<MissionDefinition>(MissionPath);

        if (mission != null)
        {
            mission.title = "Invasión en el aula";
            mission.description = "Destruye las dos filas de enemigos y luego al jefe final antes de que te destruyan.";
            mission.minigamePrefab = prefab;
            EditorUtility.SetDirty(mission);
        }
        else
        {
            Debug.LogError("[PPOP] No se encontró " + MissionPath);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[PPOP] Minijuego del Estudiante: {PrefabPath} (arte en {ArtFolder}).");
        return prefab;
    }

    /// <summary>Writes the PNG only if it does not exist (replaced art is never overwritten).</summary>
    private static Sprite Art(string name, string[] rows, Color main, Color second)
    {
        string path = $"{ArtFolder}/{name}.png";

        if (!File.Exists(path))
        {
            int height = rows.Length;
            int width = rows[0].Length;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    char c = rows[height - 1 - y][x];
                    texture.SetPixel(x, y, c == '#' ? main : c == 'o' ? second : Color.clear);
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
            const int width = 220, height = 125;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            System.Random rng = new System.Random(7);

            for (int y = 0; y < height; y++)
            {
                Color row = Color.Lerp(new Color(0.02f, 0.02f, 0.07f), new Color(0.08f, 0.04f, 0.16f), y / (float)height);

                for (int x = 0; x < width; x++)
                    texture.SetPixel(x, y, rng.NextDouble() < 0.012 ? new Color(0.9f, 0.9f, 1f) : row);
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

        // Only placeholders get configured; replaced art keeps its own import settings (filter, size...).
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
