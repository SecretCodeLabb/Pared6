using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

[InitializeOnLoad]
public static class CharacterSelectionSetup
{
    static CharacterSelectionSetup()
    {
        EditorApplication.delayCall += () => {
            if (!EditorPrefs.GetBool("CharacterSelectionSetupDone", false))
            {
                Setup();
                EditorPrefs.SetBool("CharacterSelectionSetupDone", true);
            }
        };
    }

    [MenuItem("Tools/Setup Character Selection")]
    public static void Setup()
    {
        Debug.Log("Starting Character Selection Setup...");
        
        // 1. Create ScriptableObjects
        List<CharacterData> characters = new List<CharacterData>();
        string dataPath = "Assets/Data/Characters";
        if (!AssetDatabase.IsValidFolder(dataPath))
        {
            System.IO.Directory.CreateDirectory(dataPath);
        }

        string[] names = { "Guerrero", "Mago", "Pícaro", "Paladín", "Cazador", "Clérigo" };
        int[] damages = { 5, 8, 6, 4, 7, 3 };
        int[] defenses = { 5, 2, 3, 7, 4, 6 };
        
        for (int i = 0; i < 6; i++)
        {
            string assetPath = $"{dataPath}/Character_{names[i]}.asset";
            CharacterData data = AssetDatabase.LoadAssetAtPath<CharacterData>(assetPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<CharacterData>();
                data.characterName = names[i];
                data.description = "Descripción de " + names[i];
                data.maxLives = 3;
                data.baseDamage = damages[i];
                data.baseDefense = defenses[i];
                data.abilityName = "Habilidad de " + names[i];
                data.abilityDescription = "Efecto especial de la habilidad.";
                
                AssetDatabase.CreateAsset(data, assetPath);
            }
            characters.Add(data);
        }
        AssetDatabase.SaveAssets();

        // 2. Create Prefab
        string prefabFolder = "Assets/Prefabs";
        if (!AssetDatabase.IsValidFolder(prefabFolder)) System.IO.Directory.CreateDirectory(prefabFolder);
        string prefabPath = $"{prefabFolder}/CharacterCard.prefab";
        
        GameObject cardObj = new GameObject("CharacterCard", typeof(RectTransform), typeof(Image), typeof(Button), typeof(CharacterCardUI));
        cardObj.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 300);
        cardObj.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f, 1f);

        // Selected Visual
        GameObject selectedObj = new GameObject("SelectedVisual", typeof(RectTransform), typeof(Image));
        selectedObj.transform.SetParent(cardObj.transform, false);
        var selRect = selectedObj.GetComponent<RectTransform>();
        selRect.anchorMin = Vector2.zero;
        selRect.anchorMax = Vector2.one;
        selRect.offsetMin = new Vector2(-10, -10);
        selRect.offsetMax = new Vector2(10, 10);
        selectedObj.GetComponent<Image>().color = new Color(1f, 0.8f, 0.2f); // Gold
        selectedObj.SetActive(false);
        selectedObj.transform.SetSiblingIndex(0); // Behind

        // Portrait Placeholder
        GameObject portraitObj = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
        portraitObj.transform.SetParent(cardObj.transform, false);
        var portRect = portraitObj.GetComponent<RectTransform>();
        portRect.anchorMin = new Vector2(0.1f, 0.4f);
        portRect.anchorMax = new Vector2(0.9f, 0.9f);
        portRect.offsetMin = Vector2.zero;
        portRect.offsetMax = Vector2.zero;
        portraitObj.GetComponent<Image>().color = new Color(0.4f, 0.4f, 0.4f);

        // Name text
        GameObject nameObj = new GameObject("NameText", typeof(RectTransform), typeof(TextMeshProUGUI));
        nameObj.transform.SetParent(cardObj.transform, false);
        var nameRect = nameObj.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0.1f, 0.25f);
        nameRect.anchorMax = new Vector2(0.9f, 0.35f);
        nameRect.offsetMin = Vector2.zero;
        nameRect.offsetMax = Vector2.zero;
        var nameTxt = nameObj.GetComponent<TextMeshProUGUI>();
        nameTxt.alignment = TextAlignmentOptions.Center;
        nameTxt.color = Color.white;
        nameTxt.fontSize = 20;

        // Damage text
        GameObject dmgObj = new GameObject("DamageText", typeof(RectTransform), typeof(TextMeshProUGUI));
        dmgObj.transform.SetParent(cardObj.transform, false);
        var dmgRect = dmgObj.GetComponent<RectTransform>();
        dmgRect.anchorMin = new Vector2(0.1f, 0.15f);
        dmgRect.anchorMax = new Vector2(0.9f, 0.22f);
        dmgRect.offsetMin = Vector2.zero;
        dmgRect.offsetMax = Vector2.zero;
        var dmgTxt = dmgObj.GetComponent<TextMeshProUGUI>();
        dmgTxt.alignment = TextAlignmentOptions.Center;
        dmgTxt.color = new Color(1f, 0.4f, 0.4f);
        dmgTxt.fontSize = 14;

        // Defense text
        GameObject defObj = new GameObject("DefenseText", typeof(RectTransform), typeof(TextMeshProUGUI));
        defObj.transform.SetParent(cardObj.transform, false);
        var defRect = defObj.GetComponent<RectTransform>();
        defRect.anchorMin = new Vector2(0.1f, 0.08f);
        defRect.anchorMax = new Vector2(0.9f, 0.15f);
        defRect.offsetMin = Vector2.zero;
        defRect.offsetMax = Vector2.zero;
        var defTxt = defObj.GetComponent<TextMeshProUGUI>();
        defTxt.alignment = TextAlignmentOptions.Center;
        defTxt.color = new Color(0.4f, 0.6f, 1f);
        defTxt.fontSize = 14;

        // Lives text
        GameObject livesObj = new GameObject("LivesText", typeof(RectTransform), typeof(TextMeshProUGUI));
        livesObj.transform.SetParent(cardObj.transform, false);
        var livesRect = livesObj.GetComponent<RectTransform>();
        livesRect.anchorMin = new Vector2(0.1f, 0.01f);
        livesRect.anchorMax = new Vector2(0.9f, 0.08f);
        livesRect.offsetMin = Vector2.zero;
        livesRect.offsetMax = Vector2.zero;
        var livesTxt = livesObj.GetComponent<TextMeshProUGUI>();
        livesTxt.alignment = TextAlignmentOptions.Center;
        livesTxt.color = new Color(0.4f, 1f, 0.4f);
        livesTxt.fontSize = 14;

        CharacterCardUI uiScript = cardObj.GetComponent<CharacterCardUI>();
        uiScript.characterNameText = nameTxt;
        uiScript.damageText = dmgTxt;
        uiScript.defenseText = defTxt;
        uiScript.livesText = livesTxt;
        uiScript.selectedVisual = selectedObj.GetComponent<Image>();
        uiScript.cardButton = cardObj.GetComponent<Button>();

        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(cardObj, prefabPath);
        Object.DestroyImmediate(cardObj);

        // 3. Create Scene
        Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        
        // Setup Camera
        GameObject camObj = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        camObj.tag = "MainCamera";
        Camera cam = camObj.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.1f, 0.1f, 0.15f);

        // Setup Canvas
        GameObject canvasObj = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // Setup EventSystem
        GameObject eventSystem = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));

        // Title
        GameObject titleObj = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleObj.transform.SetParent(canvasObj.transform, false);
        var titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0, -100);
        titleRect.sizeDelta = new Vector2(800, 100);
        var titleTxt = titleObj.GetComponent<TextMeshProUGUI>();
        titleTxt.text = "Selección de Personajes";
        titleTxt.fontSize = 64;
        titleTxt.alignment = TextAlignmentOptions.Center;

        // Grid
        GameObject gridObj = new GameObject("CharacterGrid", typeof(RectTransform), typeof(GridLayoutGroup));
        gridObj.transform.SetParent(canvasObj.transform, false);
        var gridRect = gridObj.GetComponent<RectTransform>();
        gridRect.anchorMin = new Vector2(0.5f, 0.5f);
        gridRect.anchorMax = new Vector2(0.5f, 0.5f);
        gridRect.pivot = new Vector2(0.5f, 0.5f);
        gridRect.anchoredPosition = new Vector2(0, 0);
        gridRect.sizeDelta = new Vector2(1200, 600);
        var grid = gridObj.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(250, 350);
        grid.spacing = new Vector2(50, 50);
        grid.childAlignment = TextAnchor.MiddleCenter;

        // Selected Text
        GameObject selTextObj = new GameObject("SelectedText", typeof(RectTransform), typeof(TextMeshProUGUI));
        selTextObj.transform.SetParent(canvasObj.transform, false);
        var selTextRect = selTextObj.GetComponent<RectTransform>();
        selTextRect.anchorMin = new Vector2(0.5f, 0f);
        selTextRect.anchorMax = new Vector2(0.5f, 0f);
        selTextRect.pivot = new Vector2(0.5f, 0f);
        selTextRect.anchoredPosition = new Vector2(0, 200);
        selTextRect.sizeDelta = new Vector2(400, 50);
        var selTxt = selTextObj.GetComponent<TextMeshProUGUI>();
        selTxt.text = "Jugadores: 0 / 4";
        selTxt.fontSize = 32;
        selTxt.alignment = TextAlignmentOptions.Center;

        // Continue Button
        GameObject btnObj = new GameObject("ContinueButton", typeof(RectTransform), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(canvasObj.transform, false);
        var btnRect = btnObj.GetComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.5f, 0f);
        btnRect.anchorMax = new Vector2(0.5f, 0f);
        btnRect.pivot = new Vector2(0.5f, 0f);
        btnRect.anchoredPosition = new Vector2(0, 80);
        btnRect.sizeDelta = new Vector2(300, 80);
        btnObj.GetComponent<Image>().color = new Color(0.2f, 0.6f, 0.2f);
        
        GameObject btnTextObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        btnTextObj.transform.SetParent(btnObj.transform, false);
        var btnTextRect = btnTextObj.GetComponent<RectTransform>();
        btnTextRect.anchorMin = Vector2.zero;
        btnTextRect.anchorMax = Vector2.one;
        btnTextRect.sizeDelta = Vector2.zero;
        var btnTxt = btnTextObj.GetComponent<TextMeshProUGUI>();
        btnTxt.text = "Continuar";
        btnTxt.fontSize = 36;
        btnTxt.alignment = TextAlignmentOptions.Center;
        btnTxt.color = Color.white;

        // Manager
        GameObject managerObj = new GameObject("CharacterSelectionManager", typeof(CharacterSelectionManager));
        var manager = managerObj.GetComponent<CharacterSelectionManager>();
        manager.characterGrid = gridObj.transform;
        manager.characterCardPrefab = savedPrefab;
        manager.selectedText = selTxt;
        manager.continueButton = btnObj.GetComponent<Button>();
        manager.availableCharacters = characters;

        // Save Scene
        string scenePath = "Assets/Scenes/CharacterSelection.unity";
        EditorSceneManager.SaveScene(newScene, scenePath);
        
        Debug.Log("Character Selection Setup Completed Successfully!");
    }
}
