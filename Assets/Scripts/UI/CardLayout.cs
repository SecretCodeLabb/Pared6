using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Zones of the final card art (Assets/Resources/CardsDesign, 2400x3300), normalized from the
/// bottom-left corner so any card size can reuse them. Shared by the character cards (selection
/// scene) and the game cards (hand). Layer order: window fill, picture, frame, texts.
/// </summary>
public static class CardLayout
{
    public const float Aspect = 2400f / 3300f;

    /// <summary>The drawn card inside the PNG (the image has a transparent margin).</summary>
    public static readonly Rect Art = Rect.MinMaxRect(0.045f, 0.045f, 0.952f, 0.958f);

    /// <summary>Transparent picture window (behind the frame).</summary>
    public static readonly Rect Window = Rect.MinMaxRect(0.10f, 0.39f, 0.89f, 0.92f);

    /// <summary>Coloured name banner.</summary>
    public static readonly Rect Banner = Rect.MinMaxRect(0.11f, 0.325f, 0.81f, 0.39f);

    /// <summary>Black description box.</summary>
    public static readonly Rect Body = Rect.MinMaxRect(0.13f, 0.11f, 0.86f, 0.30f);

    /// <summary>Picture area that is not covered by the type icon in the top-left corner.</summary>
    public static readonly Rect Picture = Rect.MinMaxRect(0.12f, 0.40f, 0.88f, 0.90f);

    public static readonly Color WindowFill = new Color(0.96f, 0.94f, 0.88f, 1f);

    public static RectTransform Fit(RectTransform rect, Rect zone)
    {
        rect.anchorMin = zone.min;
        rect.anchorMax = zone.max;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        return rect;
    }

    /// <summary>Sub-zone of a zone (fractions of that zone).</summary>
    public static Rect Sub(Rect zone, float xMin, float yMin, float xMax, float yMax)
    {
        return Rect.MinMaxRect(
            Mathf.Lerp(zone.xMin, zone.xMax, xMin),
            Mathf.Lerp(zone.yMin, zone.yMax, yMin),
            Mathf.Lerp(zone.xMin, zone.xMax, xMax),
            Mathf.Lerp(zone.yMin, zone.yMax, yMax));
    }

    /// <summary>Card text style: auto-sized, white with a dark outline so it reads on any banner.</summary>
    public static void StyleText(TextMeshProUGUI text, float maxSize, Color color, bool bold = false)
    {
        text.enableAutoSizing = true;
        text.fontSizeMin = 6f;
        text.fontSizeMax = maxSize;
        text.color = color;
        text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        text.margin = Vector4.zero;
    }

    public static Image AddImage(Transform parent, string name, Rect zone, Sprite sprite, Color color, bool preserveAspect)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.preserveAspect = preserveAspect;
        image.raycastTarget = false;

        Fit((RectTransform)go.transform, zone);
        return image;
    }
}
