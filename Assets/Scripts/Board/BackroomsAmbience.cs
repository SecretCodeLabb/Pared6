using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// "Backrooms" mood for the central maze, built to be cheap on any phone:
///   - No extra real-time lights. The fluorescent ceiling lights are faked on the floor: a pool of
///     warm additive light + a bright tube reflection per light, every light merged into one mesh
///     per material (2 draw calls in total).
///   - Flicker = rewriting the vertex colours of those small meshes, plus a rare global flicker of
///     the main directional light (no shader or lighting cost).
///   - Warm, slightly dim lighting and a dark solid background instead of the skybox.
///   - Floating dust: one small particle system, only on capable devices.
/// Low-end phones (auto-detected, or forced with <see cref="quality"/>) get fewer lights and no dust.
/// MapGenerator calls <see cref="Build"/> after generating the board. Purely visual and local.
/// </summary>
public class BackroomsAmbience : MonoBehaviour
{
    public enum Quality { Auto, Low, High }

    [Tooltip("Auto: low on phones with little memory / few cores.")]
    [SerializeField] private Quality quality = Quality.Auto;

    [Header("Mood")]
    [SerializeField] private bool applyLighting = true;
    [SerializeField] private Color sunColor = new Color(1f, 0.92f, 0.68f);
    [SerializeField] private float sunIntensity = 0.85f;
    [SerializeField] private Color ambientColor = new Color(0.46f, 0.42f, 0.28f);
    [SerializeField] private Color backgroundColor = new Color(0.07f, 0.065f, 0.045f);

    [Header("Fluorescent lights (central maze)")]
    [Tooltip("One light every N cells (high quality / low quality).")]
    [Min(2)] [SerializeField] private int spacing = 3;
    [Min(2)] [SerializeField] private int lowSpacing = 4;
    [SerializeField] private float poolSize = 4.2f;
    [SerializeField] private Color poolColor = new Color(1f, 0.9f, 0.55f, 0.32f);
    [SerializeField] private Vector2 tubeSize = new Vector2(1.5f, 0.32f);
    [SerializeField] private Color tubeColor = new Color(1f, 0.97f, 0.82f, 0.85f);

    [Header("Flicker")]
    [Tooltip("Seconds between single-light flickers (min, max).")]
    [SerializeField] private Vector2 flickerEvery = new Vector2(2.5f, 6f);
    [Tooltip("Seconds between whole-room flickers (min, max).")]
    [SerializeField] private Vector2 blackoutEvery = new Vector2(25f, 50f);

    [Header("Dust (high quality only)")]
    [SerializeField] private bool dust = true;
    [Min(0)] [SerializeField] private int maxDust = 45;

    private const float PoolHeight = 0.035f;
    private const float TubeHeight = 0.04f;

    private Transform root;
    private Mesh poolMesh;
    private Mesh tubeMesh;
    private Color[] poolColors;
    private Color[] tubeColors;
    private int lightCount;

    private Light sun;
    private float sunBaseIntensity;

    private int flickering = -1;
    private float flickerUntil;
    private float nextFlicker;
    private int brokenLight = -1;
    private float blackoutStart = -1f;
    private float nextBlackout;

    public bool IsLowEnd
    {
        get
        {
            if (quality != Quality.Auto)
                return quality == Quality.Low;

            return Application.isMobilePlatform &&
                   (SystemInfo.systemMemorySize < 4000 || SystemInfo.processorCount < 6 || SystemInfo.graphicsMemorySize < 1500);
        }
    }

    /// <summary>Rebuilds the lights for the current board (called by MapGenerator after generating).</summary>
    public void Build(MapGenerator map)
    {
        Clear();

        FxConfig config = FxConfig.Instance;

        if (map == null || map.Layout == null || config == null || config.particleMaterial == null || config.tubeMaterial == null)
            return;

        bool low = IsLowEnd;

        if (applyLighting)
            ApplyMood();

        // World space, not under this object: the MapGenerator object is not at the origin and the
        // tiles are placed with world coordinates.
        root = new GameObject("BackroomsLights").transform;

        List<Vector3> positions = new List<Vector3>();
        int step = low ? lowSpacing : spacing;
        int offset = step / 2;

        foreach (Vector2Int cell in map.Layout.Cells)
        {
            if (map.Layout.RegionOf(cell) != BoardRegion.Center)
                continue;

            if ((cell.x - offset) % step == 0 && (cell.y - offset) % step == 0 && cell.x >= offset && cell.y >= offset)
                positions.Add(new Vector3(cell.x * map.tileSize, 0f, cell.y * map.tileSize));
        }

        lightCount = positions.Count;

        if (lightCount == 0)
            return;

        poolMesh = BuildQuads("LightPools", positions, new Vector2(poolSize, poolSize), PoolHeight, poolColor, config.particleMaterial, out poolColors);
        tubeMesh = BuildQuads("LightTubes", positions, tubeSize, TubeHeight, tubeColor, config.tubeMaterial, out tubeColors);

        // One light is "broken" and buzzes all the time.
        brokenLight = Random.Range(0, lightCount);
        nextFlicker = Time.time + Random.Range(flickerEvery.x, flickerEvery.y);
        nextBlackout = Time.time + Random.Range(blackoutEvery.x, blackoutEvery.y);

        if (dust && !low && maxDust > 0)
            BuildDust(map, config.particleMaterial);
    }

    private void Clear()
    {
        if (root != null)
            Destroy(root.gameObject);

        if (poolMesh != null) Destroy(poolMesh);
        if (tubeMesh != null) Destroy(tubeMesh);

        root = null;
        poolMesh = null;
        tubeMesh = null;
        lightCount = 0;
        flickering = -1;
    }

    private void OnDestroy()
    {
        Clear();
    }

    private void ApplyMood()
    {
        sun = RenderSettings.sun;

        if (sun == null)
        {
            foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.type == LightType.Directional)
                {
                    sun = l;
                    break;
                }
            }
        }

        if (sun != null)
        {
            sun.color = sunColor;
            sun.intensity = sunIntensity;
            sunBaseIntensity = sunIntensity;
        }

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = ambientColor;

        // A solid colour is cheaper than the skybox and reads as "endless dark rooms".
        Camera cam = Camera.main;

        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = backgroundColor;
        }
    }

    private Mesh BuildQuads(string name, List<Vector3> centres, Vector2 size, float height, Color color, Material material, out Color[] colors)
    {
        int n = centres.Count;
        Vector3[] vertices = new Vector3[n * 4];
        Vector2[] uvs = new Vector2[n * 4];
        int[] triangles = new int[n * 6];
        colors = new Color[n * 4];

        Vector3 hx = new Vector3(size.x * 0.5f, 0f, 0f);
        Vector3 hz = new Vector3(0f, 0f, size.y * 0.5f);

        for (int i = 0; i < n; i++)
        {
            Vector3 c = centres[i] + Vector3.up * height;
            int v = i * 4;
            vertices[v] = c - hx - hz;
            vertices[v + 1] = c - hx + hz;
            vertices[v + 2] = c + hx + hz;
            vertices[v + 3] = c + hx - hz;
            uvs[v] = new Vector2(0f, 0f);
            uvs[v + 1] = new Vector2(0f, 1f);
            uvs[v + 2] = new Vector2(1f, 1f);
            uvs[v + 3] = new Vector2(1f, 0f);

            for (int k = 0; k < 4; k++)
                colors[v + k] = color;

            int t = i * 6;
            triangles[t] = v;
            triangles[t + 1] = v + 1;
            triangles[t + 2] = v + 2;
            triangles[t + 3] = v;
            triangles[t + 4] = v + 2;
            triangles[t + 5] = v + 3;
        }

        Mesh mesh = new Mesh { name = name };
        mesh.MarkDynamic();
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.colors = colors;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();

        GameObject go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        return mesh;
    }

    private void BuildDust(MapGenerator map, Material material)
    {
        float size = Mathf.Max(map.width, map.height) * map.tileSize;
        Vector3 centre = new Vector3((map.width - 1) * 0.5f * map.tileSize, 1.6f, (map.height - 1) * 0.5f * map.tileSize);

        GameObject go = new GameObject("Dust");
        go.transform.SetParent(root, false);
        go.transform.position = centre;

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
        main.startColor = new Color(1f, 0.95f, 0.75f, 0.35f);
        main.maxParticles = maxDust;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.prewarm = true;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = maxDust / 7f;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(size, 2.4f, size);

        ParticleSystem.ColorOverLifetimeModule colorOverLife = ps.colorOverLifetime;
        colorOverLife.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        colorOverLife.color = fade;

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ps.Play();
    }

    // =========================================================
    // FLICKER
    // =========================================================

    private void Update()
    {
        if (lightCount == 0)
            return;

        float now = Time.time;

        if (flickering < 0 && now >= nextFlicker)
        {
            flickering = Random.Range(0, lightCount);
            flickerUntil = now + Random.Range(0.4f, 1.2f);
            nextFlicker = now + Random.Range(flickerEvery.x, flickerEvery.y);
        }

        if (blackoutStart < 0f && now >= nextBlackout)
        {
            blackoutStart = now;
            nextBlackout = now + Random.Range(blackoutEvery.x, blackoutEvery.y);
        }

        float global = GlobalLevel(now);

        // Only the lights whose level changes are rewritten (a few vertices per frame).
        bool changed = false;

        for (int i = 0; i < lightCount; i++)
        {
            float level = global;

            if (i == flickering)
                level *= Mathf.PerlinNoise(now * 22f, i) > 0.45f ? 1f : 0.15f;

            if (i == brokenLight)
                level *= 0.55f + 0.45f * (Mathf.PerlinNoise(now * 9f, 3.7f) > 0.35f ? 1f : 0.2f);

            changed |= SetLevel(i, level);
        }

        if (flickering >= 0 && now >= flickerUntil)
            flickering = -1;

        if (changed)
        {
            poolMesh.colors = poolColors;
            tubeMesh.colors = tubeColors;
        }

        if (sun != null)
            sun.intensity = sunBaseIntensity * Mathf.Lerp(0.55f, 1f, global);
    }

    /// <summary>Whole-room flicker: quick dips during ~0.6 s, then back to normal.</summary>
    private float GlobalLevel(float now)
    {
        if (blackoutStart < 0f)
            return 1f;

        float t = now - blackoutStart;

        if (t > 0.65f)
        {
            blackoutStart = -1f;
            return 1f;
        }

        return t < 0.08f || (t > 0.18f && t < 0.24f) || (t > 0.4f && t < 0.5f) ? 0.1f : 1f;
    }

    private bool SetLevel(int index, float level)
    {
        int v = index * 4;
        Color pool = new Color(poolColor.r, poolColor.g, poolColor.b, poolColor.a * level);

        if (Mathf.Approximately(poolColors[v].a, pool.a))
            return false;

        Color tube = new Color(tubeColor.r, tubeColor.g, tubeColor.b, tubeColor.a * Mathf.Max(0.12f, level));

        for (int k = 0; k < 4; k++)
        {
            poolColors[v + k] = pool;
            tubeColors[v + k] = tube;
        }

        return true;
    }
}
