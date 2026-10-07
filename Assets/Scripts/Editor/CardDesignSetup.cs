using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Applies the final card art in Assets/Resources/CardsDesign:
/// - game card types get their frame (Daño, Defensa, Especial, Inútil); effects are untouched;
/// - CharacterCard.prefab uses CartaPersonaje as background, keeping each character's
///   portrait, name, ability and stats in the art's zones.
/// Idempotent: PPOP/Apply Card Designs (also run by PPOP/Setup Prototype).
/// </summary>
public static class CardDesignSetup
{
    private const string DesignDir = "Assets/Resources/CardsDesign/";
    private const string TypesDir = "Assets/ScriptableObjects/Cards/Types/";
    private const string CharacterCardPath = "Assets/Prefabs/CharacterCard.prefab";
    private const string SelectionScenePath = "Assets/Scenes/CharacterSelection.unity";

    [MenuItem("PPOP/Apply Card Designs")]
    public static void ApplyAll()
    {
        ApplyTypeFrames();
        RestyleCharacterCard();
        ResizeSelectionGrid();
        AssetDatabase.SaveAssets();
        Debug.Log("[PPOP] Diseños de carta aplicados.");
    }

    private static Sprite LoadSprite(string file)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(DesignDir + file + ".png");

        if (sprite == null)
            Debug.LogError("[PPOP] Missing card art: " + DesignDir + file + ".png (import it as Sprite).");

        return sprite;
    }

    // =========================================================
    // GAME CARDS
    // =========================================================

    private static void ApplyTypeFrames()
    {
        SetType("Type_Daño", "Daño", "CartaDamage", new Color(0.78f, 0.05f, 0.10f));
        SetType("Type_Defensa", "Defensa", "CartaDefensa", new Color(0.05f, 0.55f, 0.65f));
        SetType("Type_Habilidad", "Especial", "CartaEspecial", new Color(0.45f, 0.15f, 0.70f));
        SetType("Type_Inútil", "Inútil", "CartaInutil", new Color(0.45f, 0.45f, 0.40f));
    }

    private static void SetType(string assetName, string displayName, string art, Color color)
    {
        CardType type = AssetDatabase.LoadAssetAtPath<CardType>(TypesDir + assetName + ".asset");

        if (type == null)
        {
            Debug.LogError("[PPOP] Missing card type " + assetName + ". Run PPOP/Create Example Cards first.");
            return;
        }

        type.displayName = displayName;
        type.color = color;
        type.frame = LoadSprite(art);
        EditorUtility.SetDirty(type);
    }

    // =========================================================
    // CHARACTER CARD
    // =========================================================

    private static void RestyleCharacterCard()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(CharacterCardPath);
        CharacterCardUI ui = root.GetComponent<CharacterCardUI>();

        // Root keeps the click area but draws nothing; the frame shows the press tint.
        Image rootImage = root.GetComponent<Image>();
        rootImage.sprite = null;
        rootImage.color = new Color(1f, 1f, 1f, 0f);

        int order = 0;

        // Selection glow stays behind everything.
        if (ui.selectedVisual != null)
        {
            ui.selectedVisual.transform.SetSiblingIndex(order++);
            ui.selectedVisual.color = new Color(1f, 0.85f, 0.2f, 0.9f);

            // Thin glow hugging the drawn card (the PNG has a transparent margin).
            RectTransform glow = ui.selectedVisual.rectTransform;
            CardLayout.Fit(glow, CardLayout.Art);
            glow.offsetMin = new Vector2(-7f, -7f);
            glow.offsetMax = new Vector2(7f, 7f);
        }

        Image fill = FindOrCreateImage(root.transform, "WindowFill");
        fill.sprite = null;
        fill.color = CardLayout.WindowFill;
        fill.raycastTarget = false;
        CardLayout.Fit(fill.rectTransform, CardLayout.Window);
        fill.transform.SetSiblingIndex(order++);

        if (ui.portraitImage != null)
        {
            ui.portraitImage.preserveAspect = true;
            ui.portraitImage.raycastTarget = false;
            CardLayout.Fit(ui.portraitImage.rectTransform, CardLayout.Picture);
            ui.portraitImage.transform.SetSiblingIndex(order++);
        }

        Image frame = FindOrCreateImage(root.transform, "Frame");
        frame.sprite = LoadSprite("CartaPersonaje");
        frame.color = Color.white;
        frame.raycastTarget = false;
        CardLayout.Fit(frame.rectTransform, Rect.MinMaxRect(0f, 0f, 1f, 1f));
        frame.transform.SetSiblingIndex(order++);

        if (ui.cardButton != null)
            ui.cardButton.targetGraphic = frame;

        Color dark = new Color(0.08f, 0.08f, 0.06f);
        Color light = new Color(0.93f, 0.93f, 0.93f);

        PlaceText(ui.characterNameText, CardLayout.Banner, 22f, dark, true, TextAlignmentOptions.Center, ref order);
        PlaceText(ui.livesText, CardLayout.Sub(CardLayout.Window, 0.66f, 0.86f, 0.98f, 0.99f), 18f, new Color(0.85f, 0.1f, 0.1f), true, TextAlignmentOptions.Right, ref order);
        PlaceText(ui.abilityNameText, CardLayout.Sub(CardLayout.Body, 0f, 0.70f, 1f, 1f), 16f, new Color(0.95f, 0.85f, 0.3f), true, TextAlignmentOptions.Center, ref order);
        PlaceText(ui.abilityDescriptionText, CardLayout.Sub(CardLayout.Body, 0f, 0.22f, 1f, 0.70f), 13f, light, false, TextAlignmentOptions.Center, ref order);
        PlaceText(ui.damageText, CardLayout.Sub(CardLayout.Body, 0f, 0f, 0.5f, 0.22f), 12f, new Color(1f, 0.55f, 0.5f), true, TextAlignmentOptions.Left, ref order);
        PlaceText(ui.defenseText, CardLayout.Sub(CardLayout.Body, 0.5f, 0f, 1f, 0.22f), 12f, new Color(0.5f, 0.85f, 1f), true, TextAlignmentOptions.Right, ref order);

        if (ui.livesText != null)
        {
            ui.livesText.outlineWidth = 0.25f;
            ui.livesText.outlineColor = new Color32(255, 255, 255, 255);
        }

        PrefabUtility.SaveAsPrefabAsset(root, CharacterCardPath);
        PrefabUtility.UnloadPrefabContents(root);
    }

    private static void PlaceText(TextMeshProUGUI text, Rect zone, float maxSize, Color color, bool bold, TextAlignmentOptions alignment, ref int order)
    {
        if (text == null)
            return;

        CardLayout.Fit(text.rectTransform, zone);
        CardLayout.StyleText(text, maxSize, color, bold);
        text.alignment = alignment;
        text.transform.SetSiblingIndex(order++);
    }

    private static Image FindOrCreateImage(Transform parent, string name)
    {
        Transform existing = parent.Find(name);

        if (existing != null)
            return existing.GetComponent<Image>();

        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        return go.GetComponent<Image>();
    }

    // =========================================================
    // SELECTION GRID
    // =========================================================

    private static void ResizeSelectionGrid()
    {
        Scene scene = SceneManager.GetSceneByPath(SelectionScenePath);
        bool openedHere = !scene.isLoaded;

        if (openedHere)
            scene = EditorSceneManager.OpenScene(SelectionScenePath, OpenSceneMode.Additive);

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            GridLayoutGroup grid = root.GetComponentInChildren<GridLayoutGroup>(true);

            if (grid == null)
                continue;

            float width = 255f;
            grid.cellSize = new Vector2(width, Mathf.Round(width / CardLayout.Aspect));
            grid.spacing = new Vector2(45f, 22f);
            grid.childAlignment = TextAnchor.MiddleCenter;
            EditorUtility.SetDirty(grid);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        if (openedHere)
            EditorSceneManager.CloseScene(scene, true);
    }
}
