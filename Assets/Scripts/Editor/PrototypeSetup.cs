using System.Collections.Generic;
using System.IO;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

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

        GameObject player = EnsurePlayerNetworkPrefab();
        RegisterNetworkPrefab(player);

        CreateExampleCards();
        CardDesignSetup.ApplyAll();

        SetupMenuScene(session);
        SetupGameScene(player);
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

    private static GameObject EnsurePlayerNetworkPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);

        if (root.GetComponent<NetworkObject>() == null)
            root.AddComponent<NetworkObject>();

        if (root.GetComponent<PlayerNetworkData>() == null)
            root.AddComponent<PlayerNetworkData>();

        PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        PrefabUtility.UnloadPrefabContents(root);

        return AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
    }

    // =========================================================
    // CARDS (T12)
    // =========================================================

    private const string CardsDir = "Assets/ScriptableObjects/Cards";

    [MenuItem("PPOP/Create Example Cards")]
    public static void CreateExampleCards()
    {
        EnsureFolder(CardsDir + "/Types");
        EnsureFolder(CardsDir + "/Effects");
        EnsureFolder(CardsDir + "/Cards");
        EnsureFolder(ResourcesDir + "/Cards");

        CardType damage = EnsureCardType("Daño", new Color(0.80f, 0.25f, 0.20f));
        CardType defense = EnsureCardType("Defensa", new Color(0.25f, 0.45f, 0.85f));
        CardType ability = EnsureCardType("Habilidad", new Color(0.55f, 0.35f, 0.80f));
        CardType useless = EnsureCardType("Inútil", new Color(0.45f, 0.45f, 0.45f));

        AddToRollEffect roll1 = EnsureAmountEffect<AddToRollEffect>("AddToRoll_1", 1);
        AddToRollEffect roll2 = EnsureAmountEffect<AddToRollEffect>("AddToRoll_2", 2);
        AddToRollEffect roll3 = EnsureAmountEffect<AddToRollEffect>("AddToRoll_3", 3);
        ReduceDamageEffect def1 = EnsureAmountEffect<ReduceDamageEffect>("ReduceDamage_1", 1);
        ReduceDamageEffect def2 = EnsureAmountEffect<ReduceDamageEffect>("ReduceDamage_2", 2);
        ReduceDamageEffect def3 = EnsureAmountEffect<ReduceDamageEffect>("ReduceDamage_3", 3);

        // Available for new cards (not used by the default deck).
        EnsureAmountEffect<ReduceRequiredEffect>("ReduceRequired_1", 1);
        EnsureEffect<PreventLifeLossEffect>("PreventLifeLoss");

        List<CardData> cards = new List<CardData>
        {
            EnsureCard(1, "Golpe +1", "Golpe_1", damage, "Suma +1 a tu tirada de combate.", roll1),
            EnsureCard(2, "Golpe +2", "Golpe_2", damage, "Suma +2 a tu tirada de combate.", roll2),
            EnsureCard(3, "Golpe +3", "Golpe_3", damage, "Suma +3 a tu tirada de combate.", roll3),
            EnsureCard(4, "Escudo -1", "Escudo_1", defense, "Resta 1 al daño recibido en este combate.", def1),
            EnsureCard(5, "Escudo -2", "Escudo_2", defense, "Resta 2 al daño recibido en este combate.", def2),
            EnsureCard(6, "Escudo -3", "Escudo_3", defense, "Resta 3 al daño recibido en este combate.", def3),
            EnsureCard(7, "Truco del oficio", "Truco", ability, "Habilidad especial. Todavía no tiene efecto.", null),
            EnsureCard(8, "Piedra brillante", "Piedra", useless, "No sirve para nada... ¿o sí?", null)
        };

        string dbPath = ResourcesDir + "/Cards/CardDatabase.asset";
        CardDatabase database = AssetDatabase.LoadAssetAtPath<CardDatabase>(dbPath);

        if (database == null)
        {
            database = ScriptableObject.CreateInstance<CardDatabase>();
            AssetDatabase.CreateAsset(database, dbPath);
        }

        foreach (CardData card in cards)
        {
            if (!database.cards.Contains(card))
                database.cards.Add(card);
        }

        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        Debug.Log("[PPOP] Cartas de ejemplo: " + database.cards.Count + " en " + dbPath);
    }

    private static CardType EnsureCardType(string displayName, Color color)
    {
        string path = CardsDir + "/Types/Type_" + displayName + ".asset";
        CardType type = AssetDatabase.LoadAssetAtPath<CardType>(path);

        if (type == null)
        {
            type = ScriptableObject.CreateInstance<CardType>();
            type.displayName = displayName;
            type.color = color;
            AssetDatabase.CreateAsset(type, path);
        }

        return type;
    }

    private static T EnsureEffect<T>(string assetName) where T : CardEffect
    {
        string path = CardsDir + "/Effects/Effect_" + assetName + ".asset";
        T effect = AssetDatabase.LoadAssetAtPath<T>(path);

        if (effect == null)
        {
            effect = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(effect, path);
        }

        return effect;
    }

    private static T EnsureAmountEffect<T>(string assetName, int amount) where T : CardEffect
    {
        T effect = EnsureEffect<T>(assetName);
        SerializedObject so = new SerializedObject(effect);
        so.FindProperty("amount").intValue = amount;
        so.ApplyModifiedPropertiesWithoutUndo();
        return effect;
    }

    private static CardData EnsureCard(int id, string displayName, string fileName, CardType type, string description, CardEffect effect)
    {
        string path = CardsDir + "/Cards/Card_" + id.ToString("00") + "_" + fileName + ".asset";
        CardData card = AssetDatabase.LoadAssetAtPath<CardData>(path);

        if (card == null)
        {
            card = ScriptableObject.CreateInstance<CardData>();
            AssetDatabase.CreateAsset(card, path);
        }

        card.id = id;
        card.displayName = displayName;
        card.type = type;
        card.description = description;
        card.effect = effect;
        EditorUtility.SetDirty(card);
        return card;
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

    private static void SetupGameScene(GameObject playerPrefab)
    {
        Scene scene = OpenScene(GameScenePath);

        BoardManager board = Object.FindFirstObjectByType<BoardManager>();
        MapGenerator map = Object.FindFirstObjectByType<MapGenerator>();
        EventManager events = Object.FindFirstObjectByType<EventManager>();

        if (board == null || map == null || events == null)
        {
            Debug.LogError("[PPOP] Mapa scene is missing BoardManager / MapGenerator / EventManager.");
            return;
        }

        // In-scene network object with all game authorities.
        GameObject network = GameObject.Find("GameNetwork");

        if (network == null)
            network = new GameObject("GameNetwork");

        SceneManager.MoveGameObjectToScene(network, scene);

        GetOrAdd<NetworkObject>(network);
        GameStateManager state = GetOrAdd<GameStateManager>(network);
        GetOrAdd<TurnManager>(network);
        GetOrAdd<CombatManager>(network);
        GetOrAdd<CardManager>(network);

        SerializedObject so = new SerializedObject(state);
        so.FindProperty("boardManager").objectReferenceValue = board;
        so.FindProperty("mapGenerator").objectReferenceValue = map;
        so.FindProperty("eventManager").objectReferenceValue = events;
        so.FindProperty("networkPlayerPrefab").objectReferenceValue = playerPrefab;
        so.ApplyModifiedPropertiesWithoutUndo();

        // HUD (network only; disables itself offline).
        GameObject hudObject = GameObject.Find("GameHUD");

        if (hudObject == null)
            hudObject = new GameObject("GameHUD");

        SceneManager.MoveGameObjectToScene(hudObject, scene);
        GameHUD hud = GetOrAdd<GameHUD>(hudObject);

        GameObject rollButton = GameObject.Find("RollButton");
        GameObject infoText = GameObject.Find("GameInfoText");

        SerializedObject hudSo = new SerializedObject(hud);
        hudSo.FindProperty("boardManager").objectReferenceValue = board;
        hudSo.FindProperty("rollButton").objectReferenceValue = rollButton != null ? rollButton.GetComponent<Button>() : null;
        hudSo.FindProperty("gameInfoText").objectReferenceValue = infoText != null ? infoText.GetComponent<TextMeshProUGUI>() : null;
        hudSo.ApplyModifiedPropertiesWithoutUndo();

        // The original canvas was Constant Pixel Size laid out for 3840x2160: scale it instead,
        // so it looks identical at 4K and stays on screen at other resolutions.
        if (rollButton != null)
        {
            CanvasScaler scaler = rollButton.GetComponentInParent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(3840f, 2160f);
            scaler.matchWidthOrHeight = 0.5f;
            EditorUtility.SetDirty(scaler);
        }

        SaveScene(scene);
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        return component != null ? component : go.AddComponent<T>();
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
