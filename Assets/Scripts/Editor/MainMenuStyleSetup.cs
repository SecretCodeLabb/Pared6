using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Light "game menu" restyle of the connection scene (LobbyScene). Reuses the existing
/// buttons / input / texts (NetworkConnectUI keeps its references) and only adds a background,
/// a title and a central panel. Everything stays editable by hand afterwards.
/// Idempotent: PPOP/Style Main Menu.
/// </summary>
public static class MainMenuStyleSetup
{
    private const string ScenePath = "Assets/Scenes/LobbyScene.unity";

    private static readonly Color Background = new Color(0.07f, 0.07f, 0.10f);
    private static readonly Color PanelColor = new Color(0.12f, 0.12f, 0.16f, 1f);
    private static readonly Color Accent = new Color(0.96f, 0.80f, 0.15f);
    private static readonly Color Join = new Color(0.20f, 0.55f, 0.85f);
    private static readonly Color Danger = new Color(0.80f, 0.22f, 0.22f);
    private static readonly Color TextDark = new Color(0.08f, 0.08f, 0.08f);

    [MenuItem("PPOP/Style Main Menu")]
    public static void Apply()
    {
        Scene scene = PrototypeSetup.OpenScene(ScenePath);
        GameObject canvasObject = GameObject.Find("Canvas");
        NetworkConnectUI ui = canvasObject != null ? canvasObject.GetComponent<NetworkConnectUI>() : null;

        if (ui == null)
        {
            Debug.LogError("[PPOP] LobbyScene: Canvas with NetworkConnectUI not found.");
            return;
        }

        SerializedObject so = new SerializedObject(ui);
        Button host = so.FindProperty("hostButton").objectReferenceValue as Button;
        Button join = so.FindProperty("clientButton").objectReferenceValue as Button;
        Button disconnect = so.FindProperty("disconnectButton").objectReferenceValue as Button;
        TMP_InputField ipInput = so.FindProperty("ipInputField").objectReferenceValue as TMP_InputField;
        TextMeshProUGUI ipText = so.FindProperty("ipAddressText").objectReferenceValue as TextMeshProUGUI;
        TextMeshProUGUI status = so.FindProperty("statusText").objectReferenceValue as TextMeshProUGUI;

        Transform canvas = canvasObject.transform;
        Sprite rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        // Canvas scales with the screen (reference 1920x1080).
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // ---------- Background + decoration ----------
        Image background = FindOrCreateImage(canvas, "MenuBackground");
        Stretch(background.rectTransform);
        background.color = Background;
        background.raycastTarget = false;
        background.transform.SetAsFirstSibling();

        Image topBar = FindOrCreateImage(canvas, "MenuTopBar");
        Anchor(topBar.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -6f), new Vector2(0f, 12f));
        topBar.color = Accent;
        topBar.raycastTarget = false;
        topBar.transform.SetSiblingIndex(1);

        Image bottomBar = FindOrCreateImage(canvas, "MenuBottomBar");
        Anchor(bottomBar.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 6f), new Vector2(0f, 12f));
        bottomBar.color = Accent;
        bottomBar.raycastTarget = false;
        bottomBar.transform.SetSiblingIndex(2);

        // ---------- Title ----------
        TextMeshProUGUI title = FindOrCreateText(canvas, "MenuTitle");
        Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(1400f, 170f));
        title.text = "PARED";
        title.fontSize = 150f;
        title.fontStyle = FontStyles.Bold;
        title.characterSpacing = 18f;
        title.color = Accent;
        title.alignment = TextAlignmentOptions.Center;
        title.outlineWidth = 0.18f;
        title.outlineColor = new Color32(0, 0, 0, 255);

        TextMeshProUGUI subtitle = FindOrCreateText(canvas, "MenuSubtitle");
        Anchor(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -255f), new Vector2(1400f, 50f));
        subtitle.text = "pared, pared y otra pared";
        subtitle.fontSize = 34f;
        subtitle.fontStyle = FontStyles.Italic;
        subtitle.color = new Color(0.85f, 0.85f, 0.85f);
        subtitle.alignment = TextAlignmentOptions.Center;

        // ---------- Central panel ----------
        Image panel = FindOrCreateImage(canvas, "MenuPanel");
        Anchor(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -95f), new Vector2(640f, 520f));
        panel.sprite = rounded;
        panel.type = Image.Type.Sliced;
        panel.color = PanelColor;
        panel.raycastTarget = true;

        Outline panelOutline = panel.GetComponent<Outline>();

        if (panelOutline == null)
            panelOutline = panel.gameObject.AddComponent<Outline>();

        panelOutline.effectColor = new Color(Accent.r, Accent.g, Accent.b, 0.6f);
        panelOutline.effectDistance = new Vector2(3f, -3f);

        Transform p = panel.transform;

        if (ipText != null)
        {
            ipText.transform.SetParent(p, false);
            Anchor(ipText.rectTransform, Top, Top, new Vector2(0f, -45f), new Vector2(580f, 44f));
            ipText.fontSize = 26f;
            ipText.color = new Color(0.75f, 0.75f, 0.80f);
            ipText.alignment = TextAlignmentOptions.Center;
        }

        if (host != null)
        {
            host.transform.SetParent(p, false);
            Anchor((RectTransform)host.transform, Top, Top, new Vector2(0f, -125f), new Vector2(520f, 86f));
            StyleButton(host, "CREAR PARTIDA", Accent, TextDark, 38f, rounded);
        }

        TextMeshProUGUI divider = FindOrCreateText(p, "JoinLabel");
        Anchor(divider.rectTransform, Top, Top, new Vector2(0f, -205f), new Vector2(580f, 36f));
        divider.text = "— o únete con la IP del Host —";
        divider.fontSize = 24f;
        divider.color = new Color(0.65f, 0.65f, 0.70f);
        divider.alignment = TextAlignmentOptions.Center;

        if (ipInput != null)
        {
            ipInput.transform.SetParent(p, false);
            Anchor((RectTransform)ipInput.transform, Top, Top, new Vector2(0f, -265f), new Vector2(520f, 64f));
            StyleInput(ipInput, rounded);
        }

        if (join != null)
        {
            join.transform.SetParent(p, false);
            Anchor((RectTransform)join.transform, Top, Top, new Vector2(0f, -360f), new Vector2(520f, 80f));
            StyleButton(join, "UNIRSE", Join, Color.white, 36f, rounded);
        }

        if (disconnect != null)
        {
            // Shown instead of the buttons once connected (NetworkConnectUI toggles it).
            disconnect.transform.SetParent(p, false);
            Anchor((RectTransform)disconnect.transform, Top, Top, new Vector2(0f, -360f), new Vector2(520f, 80f));
            StyleButton(disconnect, "DESCONECTAR", Danger, Color.white, 32f, rounded);
        }

        if (status != null)
        {
            status.transform.SetParent(p, false);
            Anchor(status.rectTransform, Top, Top, new Vector2(0f, -455f), new Vector2(580f, 70f));
            status.fontSize = 24f;
            status.alignment = TextAlignmentOptions.Center;
            status.textWrappingMode = TextWrappingModes.Normal;
        }

        // ---------- Footer ----------
        TextMeshProUGUI footer = FindOrCreateText(canvas, "MenuFooter");
        Anchor(footer.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 45f), new Vector2(1400f, 36f));
        footer.text = "Multijugador LAN · 2 a 4 jugadores · Prototipo";
        footer.fontSize = 22f;
        footer.color = new Color(1f, 1f, 1f, 0.45f);
        footer.alignment = TextAlignmentOptions.Center;

        PrototypeSetup.SaveScene(scene);
        Debug.Log("[PPOP] Menú principal reestilizado.");
    }

    private static readonly Vector2 Top = new Vector2(0.5f, 1f);

    // =========================================================
    // HELPERS
    // =========================================================

    private static void StyleButton(Button button, string label, Color color, Color textColor, float fontSize, Sprite sprite)
    {
        // NetworkConnectUI wires these buttons at runtime; a listener saved in the scene
        // made every click run twice ("NetworkManager ya está activo").
        for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            UnityEditor.Events.UnityEventTools.RemovePersistentListener(button.onClick, i);

        Image image = button.GetComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.color = Color.white;

        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.25f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.25f);
        colors.selectedColor = color;
        colors.disabledColor = new Color(0.4f, 0.4f, 0.4f, 0.7f);
        colors.colorMultiplier = 1f;
        button.colors = colors;

        TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>(true);

        if (text != null)
        {
            text.text = label;
            text.fontSize = fontSize;
            text.fontStyle = FontStyles.Bold;
            text.color = textColor;
            text.alignment = TextAlignmentOptions.Center;
            Stretch(text.rectTransform);
        }

        EditorUtility.SetDirty(button);
    }

    private static void StyleInput(TMP_InputField input, Sprite sprite)
    {
        Image image = input.GetComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.color = new Color(0.92f, 0.92f, 0.95f);

        if (input.textComponent != null)
        {
            input.textComponent.fontSize = 30f;
            input.textComponent.alignment = TextAlignmentOptions.Center;
            input.textComponent.color = TextDark;
        }

        if (input.placeholder is TextMeshProUGUI placeholder)
        {
            placeholder.text = "IP del Host (ej. 192.168.1.20)";
            placeholder.fontSize = 26f;
            placeholder.alignment = TextAlignmentOptions.Center;
        }

        if (input.textViewport != null)
        {
            input.textViewport.offsetMin = new Vector2(12f, 6f);
            input.textViewport.offsetMax = new Vector2(-12f, -6f);
        }

        EditorUtility.SetDirty(input);
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

    private static TextMeshProUGUI FindOrCreateText(Transform parent, string name)
    {
        Transform existing = parent.Find(name);

        if (existing != null)
            return existing.GetComponent<TextMeshProUGUI>();

        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.raycastTarget = false;
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void Anchor(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        // The original menu widgets were scaled up by hand; sizes are now explicit.
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }
}
