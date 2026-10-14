using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// One card of the local hand (GameHUD), card-game style and purely visual (always face-up):
///   - glides to its fan slot (position / tilt / scale given by GameHUD every frame);
///   - the open card (= the selected one) grows and rises so its text is easy to read;
///   - dealt in from below when created, lifts a little under the mouse.
/// The click itself is the card's Button (GameHUD), so the game logic is unchanged.
/// </summary>
public class HandCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private const float Smooth = 14f;

    private RectTransform rect;
    private Vector2 targetPosition;
    private float targetRotation;
    private float targetScale = 1f;
    private bool initialized;

    public bool Open { get; set; }
    public bool Hovered { get; private set; }

    private void Awake()
    {
        rect = (RectTransform)transform;
    }

    /// <summary>Fan slot to glide to.</summary>
    public void SetTarget(Vector2 position, float rotation, float cardScale)
    {
        targetPosition = position;
        targetRotation = rotation;
        targetScale = cardScale;

        // Dealt from below the screen the first time.
        if (!initialized)
        {
            initialized = true;
            rect.anchoredPosition = position + new Vector2(0f, -320f);
            rect.localRotation = Quaternion.Euler(0f, 0f, rotation + 25f);
        }
    }

    private void Update()
    {
        float k = 1f - Mathf.Exp(-Smooth * Time.unscaledDeltaTime);
        rect.anchoredPosition = Vector2.Lerp(rect.anchoredPosition, targetPosition, k);
        rect.localRotation = Quaternion.Slerp(rect.localRotation, Quaternion.Euler(0f, 0f, targetRotation), k);
        rect.localScale = Vector3.one * Mathf.Lerp(rect.localScale.x, targetScale, k);
    }

    public void OnPointerEnter(PointerEventData eventData) => Hovered = true;

    public void OnPointerExit(PointerEventData eventData) => Hovered = false;
}
