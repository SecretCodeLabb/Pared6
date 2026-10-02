using System.Collections.Generic;
using System.IO;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Idempotent setup for the LAN prototype: config assets, network prefabs and scene wiring.
/// Run from PPOP/Setup Prototype. Safe to run several times.
/// </summary>
public static class PrototypeSetup
{
    private const string MenuScenePath = "Assets/Scenes/LobbyScene.unity";
    private const string SelectionScenePath = "Assets/Scenes/CharacterSelection.unity";
    private const string GameScenePath = "Assets/Scenes/Mapa.unity";

    private const string ResourcesDir = "Assets/Resources";
    private const string NetworkPrefabDir = "Assets/Prefabs/Network";
    private const string SessionPrefabPath = NetworkPrefabDir + "/NetworkSession.prefab";
    private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    private const string NetworkPrefabsListPath = "Assets/DefaultNetworkPrefabs.asset";

    [MenuItem("PPOP/Setup Prototype")]
    public static void SetupAll()
    {
        EnsureFolder(ResourcesDir);
        EnsureFolder(NetworkPrefabDir);

        EnsureGameRules();
        CharacterCatalog catalog = EnsureCharacterCatalog();
        EnsureCharacterColors(catalog);
        GameObject session = EnsureSessionPrefab();

        RegisterNetworkPrefab(session);

        SetupMenuScene(session);
        SetupBuildSettings();

        AssetDatabase.SaveAssets();
        Debug.Log("[PPOP] Setup Prototype completado.");
    }

    // =========================================================
    // ASSETS
    // =========================================================

    public static GameRules EnsureGameRules()
    {
        string path = ResourcesDir + "/GameRules.asset";
        GameRules rules = AssetDatabase.LoadAssetAtPath<GameRules>(path);

        if (rules == null)
        {
            rules = ScriptableObject.CreateInstance<GameRules>();
            AssetDatabase.CreateAsset(rules, path);
        }

        return rules;
    }

    public static CharacterCatalog EnsureCharacterCatalog()
    {
        string path = ResourcesDir + "/CharacterCatalog.asset";
        CharacterCatalog catalog = AssetDatabase.LoadAssetAtPath<CharacterCatalog>(path);

        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<CharacterCatalog>();
            AssetDatabase.CreateAsset(catalog, path);
        }

        if (catalog.characters.Count == 0)
        {
            // Same order as the selection screen list.
            string[] names = { "Guerrero", "Mago", "Pícaro", "Paladín", "Cazador", "Clérigo" };

            foreach (string n in names)
            {
                CharacterData data = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/Data/Characters/Character_" + n + ".asset");

                if (data != null)
                    catalog.characters.Add(data);
                else
                    Debug.LogError("[PPOP] Missing CharacterData: " + n);
            }

            EditorUtility.SetDirty(catalog);
        }

        return catalog;
    }

    private static void EnsureCharacterColors(CharacterCatalog catalog)
    {
        Color[] palette =
        {
            new Color(0.85f, 0.25f, 0.20f),
            new Color(0.35f, 0.45f, 0.95f),
            new Color(0.30f, 0.80f, 0.35f),
            new Color(0.95f, 0.85f, 0.30f),
            new Color(0.70f, 0.40f, 0.20f),
            new Color(0.85f, 0.45f, 0.90f)
        };

        for (int i = 0; i < catalog.characters.Count; i++)
        {
            CharacterData data = catalog.characters[i];

            if (data == null || data.boardColor.a > 0f)
                continue;

            data.boardColor = palette[i % palette.Length];
            EditorUtility.SetDirty(data);
        }
    }

    private static GameObject EnsureSessionPrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SessionPrefabPath);

        if (prefab != null)
            return prefab;

        GameObject temp = new GameObject("NetworkSession");
        temp.AddComponent<NetworkObject>();
        temp.AddComponent<LobbyManager>();

        prefab = PrefabUtility.SaveAsPrefabAsset(temp, SessionPrefabPath);
        Object.DestroyImmediate(temp);
        return prefab;
    }

    public static void RegisterNetworkPrefab(GameObject prefab)
    {
        NetworkPrefabsList list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(NetworkPrefabsListPath);

        if (list == null)
        {
            Debug.LogError("[PPOP] " + NetworkPrefabsListPath + " not found.");
            return;
        }

        foreach (NetworkPrefab existing in list.PrefabList)
        {
            if (existing.Prefab == prefab)
                return;
        }

        list.Add(new NetworkPrefab { Prefab = prefab });
        EditorUtility.SetDirty(list);
    }

    // =========================================================
    // SCENES
    // =========================================================

    private static void SetupMenuScene(GameObject session)
    {
        Scene scene = OpenScene(MenuScenePath);

        NetworkConnectionManager connection = Object.FindFirstObjectByType<NetworkConnectionManager>();

        if (connection == null)
        {
            Debug.LogError("[PPOP] NetworkConnectionManager not found in LobbyScene. Run PPOP/Setup Network first.");
            return;
        }

        SerializedObject so = new SerializedObject(connection);
        so.FindProperty("sessionPrefab").objectReferenceValue = session;
        so.ApplyModifiedPropertiesWithoutUndo();

        SaveScene(scene);
    }

    private static void SetupBuildSettings()
    {
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(MenuScenePath, true),
            new EditorBuildSettingsScene(SelectionScenePath, true),
            new EditorBuildSettingsScene(GameScenePath, true)
        };
    }

    // =========================================================
    // HELPERS
    // =========================================================

    public static Scene OpenScene(string path)
    {
        Scene scene = SceneManager.GetSceneByPath(path);

        if (!scene.isLoaded)
            scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

        SceneManager.SetActiveScene(scene);
        return scene;
    }

    public static void SaveScene(Scene scene)
    {
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    public static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
