using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// PPOP/Fx/Build Fx Assets: generates the textures (soft circle, ring, rounded square, dice face, pip),
/// the particle / ring materials and Resources/FxConfig.asset used by GameFx and the dice animation.
/// Existing PNGs are kept, so they can be replaced with custom art.
/// </summary>
public static class FxSetup
{
    private const string ArtFolder = "Assets/Art/Fx";
    private const string ConfigPath = "Assets/Resources/FxConfig.asset";

    [MenuItem("PPOP/Fx/Build Fx Assets")]
    public static FxConfig Build()
    {
        Directory.CreateDirectory(ArtFolder);

        Texture2D softTexture = Texture("SoftCircle", 64, (x, y) =>
        {
            float d = Distance01(x, y, 64);
            float a = Mathf.Clamp01(1f - d);
            return new Color(1f, 1f, 1f, a * a);
        });

        Texture2D ringTexture = Texture("Ring", 128, (x, y) =>
        {
            float d = Distance01(x, y, 128);
            float band = Mathf.Clamp01(1f - Mathf.Abs(d - 0.82f) / 0.12f);
            float glow = Mathf.Clamp01(1f - Mathf.Abs(d - 0.82f) / 0.35f) * 0.35f;
            return new Color(1f, 1f, 1f, Mathf.Max(band * band, glow) * (d < 1f ? 1f : 0f));
        });

        Texture2D squareTexture = Texture("RoundedSquare", 64, (x, y) => new Color(1f, 1f, 1f, RoundedAlpha(x, y, 64, 14f)));

        Texture2D diceTexture = Texture("DiceFace", 128, (x, y) =>
        {
            float a = RoundedAlpha(x, y, 128, 26f);
            float inner = RoundedAlpha(x, y, 128, 26f, 6f);
            Color face = Color.Lerp(new Color(0.80f, 0.78f, 0.74f), Color.white, inner);
            return new Color(face.r, face.g, face.b, a);
        });

        Texture2D pipTexture = Texture("Pip", 32, (x, y) =>
        {
            float d = Distance01(x, y, 32);
            return new Color(1f, 1f, 1f, Mathf.Clamp01((1f - d) * 6f));
        });

        Material particle = MaterialAsset("Fx_Particles", "Universal Render Pipeline/Particles/Unlit", softTexture, true);
        Material ring = MaterialAsset("Fx_Ring", "Universal Render Pipeline/Particles/Unlit", ringTexture, false);
        Material tube = MaterialAsset("Fx_Tube", "Universal Render Pipeline/Particles/Unlit", squareTexture, true);

        FxConfig config = AssetDatabase.LoadAssetAtPath<FxConfig>(ConfigPath);

        if (config == null)
        {
            config = ScriptableObject.CreateInstance<FxConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
        }

        config.particleMaterial = particle;
        config.ringMaterial = ring;
        config.tubeMaterial = tube;
        config.softCircle = Sprite(softTexture, 0);
        config.roundedSquare = Sprite(squareTexture, 18);
        config.diceFace = Sprite(diceTexture, 0);
        config.pip = Sprite(pipTexture, 0);
        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();

        Debug.Log("[PPOP] Assets de efectos listos: " + ConfigPath);
        return config;
    }

    private static float Distance01(int x, int y, int size)
    {
        float c = (size - 1) * 0.5f;
        return Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
    }

    private static float RoundedAlpha(int x, int y, int size, float radius, float inset = 0f)
    {
        float min = inset + radius;
        float max = size - 1 - inset - radius;
        float dx = Mathf.Max(min - x, 0f, x - max);
        float dy = Mathf.Max(min - y, 0f, y - max);
        float d = Mathf.Sqrt(dx * dx + dy * dy);
        return Mathf.Clamp01(radius - d + 0.5f);
    }

    /// <summary>Writes the PNG only if missing, then imports it as a clamped, bilinear texture/sprite.</summary>
    private static Texture2D Texture(string name, int size, System.Func<int, int, Color> pixel)
    {
        string path = $"{ArtFolder}/{name}.png";

        if (!File.Exists(path))
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    texture.SetPixel(x, y, pixel(x, y));

            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);

            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static Sprite Sprite(Texture2D texture, int border)
    {
        string path = AssetDatabase.GetAssetPath(texture);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);

        if (border > 0 && importer.spriteBorder == Vector4.zero)
        {
            importer.spriteBorder = new Vector4(border, border, border, border);
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static Material MaterialAsset(string name, string shaderName, Texture2D texture, bool additive)
    {
        string path = $"{ArtFolder}/{name}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find(shaderName);

        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = shader;
        material.SetTexture("_BaseMap", texture);
        material.SetColor("_BaseColor", Color.white);

        // URP particles: transparent surface; additive (glow) or alpha blend.
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", additive ? 2f : 0f);
        material.SetFloat("_ColorMode", 0f);
        material.SetOverrideTag("RenderType", "Transparent");
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", additive ? (int)UnityEngine.Rendering.BlendMode.One : (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.SetFloat("_Cull", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        EditorUtility.SetDirty(material);
        return material;
    }
}
