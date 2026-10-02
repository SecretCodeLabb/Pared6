using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>Small helpers to build prototype uGUI elements from code.</summary>
public static class UIFactory
{
    public static readonly Color PanelColor = new Color(0.08f, 0.09f, 0.12f, 0.88f);
    public static readonly Color ButtonColor = new Color(0.20f, 0.45f, 0.85f, 1f);
    public static readonly Color DangerColor = new Color(0.80f, 0.22f, 0.22f, 1f);
    public static readonly Color OkColor = new Color(0.22f, 0.65f, 0.30f, 1f);
    public static readonly Color AccentColor = new Color(1f, 0.82f, 0.25f, 1f);

    public static RectTransform CreateRect(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    /// <summary>Anchors a rect to a corner/edge with a fixed size.</summary>
    public static RectTransform Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    public static RectTransform Stretch(RectTransform rect, float padding = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(padding, padding);
        rect.offsetMax = new Vector2(-padding, -padding);
        return rect;
    }

    public static Image CreatePanel(Transform parent, string name, Color color)
    {
        RectTransform rect = CreateRect(parent, name);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    public static TextMeshProUGUI CreateText(
        Transform parent,
        string name,
        string text,
        float fontSize,
        TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        RectTransform rect = CreateRect(parent, name);
        TextMeshProUGUI tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        return tmp;
    }

    public static Button CreateButton(
        Transform parent,
        string name,
        string label,
        UnityAction onClick,
        Color color,
        float fontSize = 26f)
    {
        Image image = CreatePanel(parent, name, color);
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f);
        button.colors = colors;

        TextMeshProUGUI text = CreateText(image.transform, "Label", label, fontSize);
        Stretch(text.rectTransform, 4f);

        if (onClick != null)
            button.onClick.AddListener(onClick);

        return button;
    }

    public static void SetLabel(Button button, string label)
    {
        TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>(true);

        if (text != null)
            text.text = label;
    }

    public static VerticalLayoutGroup AddVerticalLayout(GameObject go, float spacing, int padding, bool controlHeight = true)
    {
        VerticalLayoutGroup layout = go.AddComponent<VerticalLayoutGroup>();
        layout.spacing = spacing;
        layout.padding = new RectOffset(padding, padding, padding, padding);
        layout.childControlWidth = true;
        layout.childControlHeight = controlHeight;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return layout;
    }

    public static LayoutElement SetPreferredHeight(Component component, float height)
    {
        LayoutElement element = component.GetComponent<LayoutElement>();

        if (element == null)
            element = component.gameObject.AddComponent<LayoutElement>();

        element.preferredHeight = height;
        element.minHeight = height;
        return element;
    }
}
