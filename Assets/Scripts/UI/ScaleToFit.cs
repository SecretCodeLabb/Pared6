using UnityEngine;

/// <summary>
/// Shrinks a fixed-size UI block (uniform scale) when its parent is smaller than it, so panels
/// designed for big screens (roulette, minigames...) still fit on phones. Never enlarges.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class ScaleToFit : MonoBehaviour
{
    [Range(0.5f, 1f)] public float margin = 0.96f;

    private RectTransform rect;
    private Vector2 lastParentSize;
    private Vector2 designPosition;

    /// <summary>Adds the component to every non-stretched child of root.</summary>
    public static void ApplyToChildren(Transform root)
    {
        foreach (Transform child in root)
        {
            if (child is RectTransform r && r.anchorMin == r.anchorMax && child.GetComponent<ScaleToFit>() == null)
                child.gameObject.AddComponent<ScaleToFit>();
        }
    }

    private void Awake()
    {
        rect = (RectTransform)transform;
        designPosition = rect.anchoredPosition;
    }

    private void OnEnable()
    {
        lastParentSize = Vector2.zero;
        Fit();
    }

    private void LateUpdate()
    {
        Fit();
    }

    private void Fit()
    {
        RectTransform parent = rect.parent as RectTransform;

        if (parent == null)
            return;

        Vector2 parentSize = parent.rect.size;

        if (parentSize == lastParentSize)
            return;

        lastParentSize = parentSize;
        Vector2 size = rect.rect.size;

        if (size.x <= 0f || size.y <= 0f)
            return;

        float scale = Mathf.Min(1f, parentSize.x * margin / size.x, parentSize.y * margin / size.y);
        rect.localScale = new Vector3(scale, scale, 1f);

        // A centred block keeps its offset proportional instead of drifting off-screen.
        if (rect.anchorMin == new Vector2(0.5f, 0.5f) && rect.anchorMax == rect.anchorMin)
            rect.anchoredPosition = designPosition * scale;
    }
}
