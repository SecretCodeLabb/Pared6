using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Creates the roulette assets the first time (never overwrites your edits):
/// - Resources/RouletteConfig.asset with the default options and weights;
/// - Prefabs/UI/RouletteWheel.prefab, a plain casino-style wheel to restyle by hand.
/// PPOP/Create Roulette (also run by PPOP/Setup Prototype). Delete an asset to regenerate it.
/// </summary>
public static class RouletteSetup
{
    public const string ConfigPath = "Assets/Resources/RouletteConfig.asset";
    public const string PrefabPath = "Assets/Prefabs/UI/RouletteWheel.prefab";

    private static readonly Color Gold = new Color(0.96f, 0.80f, 0.15f);

    [MenuItem("PPOP/Create Roulette")]
    public static GameObject EnsureAll()
    {
        EnsureConfig();
        return EnsurePrefab();
    }

    public static RouletteConfig EnsureConfig()
    {
        RouletteConfig config = AssetDatabase.LoadAssetAtPath<RouletteConfig>(ConfigPath);

        if (config != null)
            return config;

        config = ScriptableObject.CreateInstance<RouletteConfig>();
        config.options = RouletteConfig.CreateDefaultOptions();
        AssetDatabase.CreateAsset(config, ConfigPath);
        AssetDatabase.SaveAssets();
        return config;
    }

    public static GameObject EnsurePrefab()
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

        if (existing != null)
            return existing;

        PrototypeSetup.EnsureFolder("Assets/Prefabs/UI");

        Sprite rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        Sprite circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

        // Root: full-screen dimmer + view script.
        GameObject root = new GameObject("RouletteWheel", typeof(RectTransform), typeof(Image), typeof(RouletteWheelView));
        RectTransform rootRect = (RectTransform)root.transform;
        Stretch(rootRect);
        root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

        Image panel = NewImage(root.transform, "Panel", rounded, new Color(0.10f, 0.10f, 0.14f, 1f));
        Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640f, 860f));
        panel.type = Image.Type.Sliced;
        Outline outline = panel.gameObject.AddComponent<Outline>();
        outline.effectColor = Gold;
        outline.effectDistance = new Vector2(3f, -3f);

        TextMeshProUGUI title = NewText(panel.transform, "Title", "¡RULETA!", 56f, Gold, true);
        Place(title.rectTransform, Top, new Vector2(0f, -50f), new Vector2(600f, 70f));

        TextMeshProUGUI player = NewText(panel.transform, "PlayerText", "", 26f, Color.white, false);
        Place(player.rectTransform, Top, new Vector2(0f, -102f), new Vector2(600f, 36f));

        // Wheel area.
        RectTransform area = NewRect(panel.transform, "WheelArea");
        Place(area, Top, new Vector2(0f, -440f), new Vector2(520f, 520f));

        Image rim = NewImage(area, "Rim", circle, Gold);
        Place(rim.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(548f, 548f));

        RectTransform wheel = NewRect(area, "Wheel");
        Stretch(wheel);

        Image template = NewImage(wheel, "SegmentTemplate", circle, Color.white);
        Stretch(template.rectTransform);
        template.type = Image.Type.Filled;
        template.fillMethod = Image.FillMethod.Radial360;
        template.fillOrigin = (int)Image.Origin360.Top;
        template.fillClockwise = true;
        template.fillAmount = 0.1f;

        TextMeshProUGUI label = NewText(template.transform, "Label", "OPCIÓN", 20f, Color.white, true);
        label.rectTransform.sizeDelta = new Vector2(150f, 50f);
        label.enableAutoSizing = true;
        label.fontSizeMin = 10f;
        label.fontSizeMax = 22f;
        label.outlineWidth = 0.22f;
        label.outlineColor = new Color32(0, 0, 0, 255);
        label.lineSpacing = -20f;

        Image hub = NewImage(area, "Hub", circle, new Color(0.10f, 0.10f, 0.14f));
        Place(hub.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(90f, 90f));
        Outline hubOutline = hub.gameObject.AddComponent<Outline>();
        hubOutline.effectColor = Gold;
        hubOutline.effectDistance = new Vector2(3f, -3f);

        TextMeshProUGUI pointer = NewText(area, "Pointer", "▼", 64f, Gold, true);
        Place(pointer.rectTransform, Top, new Vector2(0f, 6f), new Vector2(80f, 70f));
        pointer.outlineWidth = 0.25f;
        pointer.outlineColor = new Color32(0, 0, 0, 255);

        TextMeshProUGUI result = NewText(panel.transform, "ResultText", "", 34f, Gold, true);
        Place(result.rectTransform, Top, new Vector2(0f, -738f), new Vector2(600f, 56f));

        Image buttonImage = NewImage(panel.transform, "SpinButton", rounded, Gold);
        buttonImage.type = Image.Type.Sliced;
        Place(buttonImage.rectTransform, Top, new Vector2(0f, -808f), new Vector2(300f, 70f));
        Button button = buttonImage.gameObject.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        TextMeshProUGUI buttonLabel = NewText(buttonImage.transform, "Label", "GIRAR", 36f, new Color(0.08f, 0.08f, 0.08f), true);
        Stretch(buttonLabel.rectTransform);

        // Wire the view.
        RouletteWheelView view = root.GetComponent<RouletteWheelView>();
        SerializedObject so = new SerializedObject(view);
        so.FindProperty("wheel").objectReferenceValue = wheel;
        so.FindProperty("segmentTemplate").objectReferenceValue = template;
        so.FindProperty("spinButton").objectReferenceValue = button;
        so.FindProperty("titleText").objectReferenceValue = title;
        so.FindProperty("playerText").objectReferenceValue = player;
        so.FindProperty("resultText").objectReferenceValue = result;
        so.FindProperty("spinButtonLabel").objectReferenceValue = buttonLabel;
        so.ApplyModifiedPropertiesWithoutUndo();

        template.gameObject.SetActive(false);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    // =========================================================
    // HELPERS
    // =========================================================

    private static readonly Vector2 Top = new Vector2(0.5f, 1f);

    private static RectTransform NewRect(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static Image NewImage(Transform parent, string name, Sprite sprite, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI NewText(Transform parent, string name, string text, float size, Color color, bool bold)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}
