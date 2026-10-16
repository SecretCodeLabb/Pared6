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
    private const string PlayerPrefabPath = "Assets/Prefabs/Players/Player.prefab";
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
        RouletteSetup.EnsureAll();
        RoomThemeSetup.CreateAll();
        MissionSetup.CreateAll();

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
                CharacterData data = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/ScriptableObjects/Characters/Character_" + n + ".asset");

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
        // Defense cards subtract their number from the rival's number.
        DefenseEffect def1 = EnsureAmountEffect<DefenseEffect>("Defense_1", 1);
        DefenseEffect def2 = EnsureAmountEffect<DefenseEffect>("Defense_2", 2);
        DefenseEffect def3 = EnsureAmountEffect<DefenseEffect>("Defense_3", 3);

        // Available for new cards (not used by the default deck).
        EnsureAmountEffect<ReduceDamageEffect>("ReduceDamage_1", 1);
        EnsureAmountEffect<ReduceDamageEffect>("ReduceDamage_2", 2);
        EnsureAmountEffect<ReduceDamageEffect>("ReduceDamage_3", 3);
        EnsureAmountEffect<ReduceRequiredEffect>("ReduceRequired_1", 1);
        EnsureEffect<PreventLifeLossEffect>("PreventLifeLoss");

        List<CardData> cards = new List<CardData>
        {
            EnsureCard(1, "Golpe +1", "Golpe_1", damage, "Suma +1 a tu tirada de combate.", roll1),
            EnsureCard(2, "Golpe +2", "Golpe_2", damage, "Suma +2 a tu tirada de combate.", roll2),
            EnsureCard(3, "Golpe +3", "Golpe_3", damage, "Suma +3 a tu tirada de combate.", roll3),

            // Defense and useless cards use final art with the name and text drawn on it (Art/Cards).
            EnsureCard(4, "Video defensa personal", "VideoDefensaPersonal", defense,
                "Este video te cambiará la vida. Espera hasta el final: te podrá dar una técnica definitiva. Defensa: resta 1 al número del rival.", def1, "Defensa/VideoDefensaPersonal"),
            EnsureCard(5, "Señal de pare", "SenalDePare", defense,
                "No puedes golpearme. Estoy sosteniendo un objeto reglamentario. ¡PARA! Defensa: resta 2 al número del rival.", def2, "Defensa/SenalDePare"),
            EnsureCard(6, "Emulsión Scott", "EmulsionScott", defense,
                "Tómese la cucharada, mijo. ¡Que esa entidad no lo vaya a encontrar desnutrido! Defensa: resta 3 al número del rival.", def3, "Defensa/EmulsionScott"),
            EnsureCard(15, "Jabón Rey", "JabonRey", defense,
                "¡Cuidado! Tu perseguidor acaba de deslizarse con estilo. Defensa: resta 1 al número del rival.", def1, "Defensa/JabonRey"),
            EnsureCard(16, "Silla Rimax", "SillaRimax", defense,
                "Sirve para sentarse, cargar el mercado y bloquear ataques interdimensionales. No incluye cojín. Defensa: resta 2 al número del rival.", def2, "Defensa/SillaRimax"),
            EnsureCard(8, "Agua estancada", "AguaEstancada", useless,
                "Tu cuerpo necesita agua. No se especifica que tuviera que ser buena. Solo hidrátate.", null, "Inutiles/AguaEstancada"),
            EnsureCard(17, "Autoayuda", "Autoayuda", useless,
                "Las personas exitosas nunca llegan a rendirse. Este libro tiene +400 páginas y todavía no llega a la solución.", null, "Inutiles/Autoayuda"),
            EnsureCard(18, "Trompo", "Trompo", useless,
                "¡Mira cómo gira! Seguro que la entidad, tal vez, pueda esperar a que termine.", null, "Inutiles/Trompo"),
            EnsureCard(19, "VIP gimnasio", "VipGimnasio", useless,
                "Ser tu mejor versión nunca fue tan fácil. Solo tienes que pagar por ella.", null, "Inutiles/VipGimnasio"),
            EnsureCard(20, "Diploma", "Diploma", useless,
                "Invertiste años de tu vida para conseguir este título. Ahora puedes invertir otros años buscando qué hacer con él.", null, "Inutiles/Diploma"),

            // Special (purple) cards. Name, description, icon and values are editable on the assets.
            EnsureCard(9, "Silencio líquido", "SilencioLiquido", ability,
                "Durante 2 turnos tienes un 85% de evitar los combates contra entidades.",
                EnsureSpecial("SilencioLiquido", SpecialAbility.AvoidEvents, 0, 0.85f, 2)),
            EnsureCard(10, "Cívica", "Civica", ability,
                "En vez de tirar, muévete exactamente 6 casillas en cualquier dirección atravesando paredes.",
                EnsureSpecial("Civica", SpecialAbility.WallWalk, 6, 1f, 1)),
            EnsureCard(11, "Comida Frita", "ComidaFrita", ability,
                "Te cura una vida.",
                EnsureSpecial("ComidaFrita", SpecialAbility.Heal, 1, 1f, 1)),
            EnsureCard(12, "Gatos fotocopiados", "GatosFotocopiados", ability,
                "En un duelo contra otro jugador: 65% de probabilidad de escapar.",
                EnsureSpecial("GatosFotocopiados", SpecialAbility.EscapeDuel, 0, 0.65f, 1)),
            EnsureCard(13, "Salchipapa", "Salchipapa", ability,
                "Si pierdes todas tus vidas y tienes esta carta, revives con 1 vida.",
                EnsureSpecial("Salchipapa", SpecialAbility.Revive, 1, 1f, 1)),
            EnsureCard(14, "Canicas", "Canicas", ability,
                "En un combate contra una entidad: 65% de probabilidad de escapar.",
                EnsureSpecial("Canicas", SpecialAbility.EscapeEvent, 0, 0.65f, 1))
        };

        // The old placeholder special card is replaced by the real ones.
        CardData oldTrick = AssetDatabase.LoadAssetAtPath<CardData>(CardsDir + "/Cards/Card_07_Truco.asset");

        string dbPath = ResourcesDir + "/Cards/CardDatabase.asset";
        CardDatabase database = AssetDatabase.LoadAssetAtPath<CardDatabase>(dbPath);

        if (database == null)
        {
            database = ScriptableObject.CreateInstance<CardDatabase>();
            AssetDatabase.CreateAsset(database, dbPath);
        }

        if (oldTrick != null)
            database.cards.Remove(oldTrick);

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

    private static CardData EnsureCard(int id, string displayName, string fileName, CardType type, string description, CardEffect effect, string fullArt = null)
    {
        string path = CardsDir + "/Cards/Card_" + id.ToString("00") + "_" + fileName + ".asset";
        CardData card = AssetDatabase.LoadAssetAtPath<CardData>(path);

        // Existing cards keep whatever you edited (name, description, icon, type, effect).
        if (card != null)
            return card;

        card = ScriptableObject.CreateInstance<CardData>();
        card.id = id;
        card.displayName = displayName;
        card.type = type;
        card.description = description;
        card.effect = effect;

        if (fullArt != null)
            card.fullArt = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Cards/" + fullArt + ".png");

        AssetDatabase.CreateAsset(card, path);
        return card;
    }

    private static SpecialCardEffect EnsureSpecial(string assetName, SpecialAbility ability, int amount, float chance, int turns)
    {
        string path = CardsDir + "/Effects/Effect_Special_" + assetName + ".asset";
        SpecialCardEffect effect = AssetDatabase.LoadAssetAtPath<SpecialCardEffect>(path);

        if (effect != null)
            return effect;

        effect = ScriptableObject.CreateInstance<SpecialCardEffect>();
        effect.ability = ability;
        effect.amount = amount;
        effect.chance = chance;
        effect.turns = turns;
        AssetDatabase.CreateAsset(effect, path);
        return effect;
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
        GetOrAdd<RouletteManager>(network);
        GetOrAdd<MissionManager>(network);

        // Game camera: follows the active player, C toggles the overview, wheel zooms.
        Camera mainCamera = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();

        if (mainCamera != null)
            GetOrAdd<BoardCameraController>(mainCamera.gameObject);

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

        GameObject roulettePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RouletteSetup.PrefabPath);
        hudSo.FindProperty("roulettePrefab").objectReferenceValue = roulettePrefab != null ? roulettePrefab.GetComponent<RouletteWheelView>() : null;
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
