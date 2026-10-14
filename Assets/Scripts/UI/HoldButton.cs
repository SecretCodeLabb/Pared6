using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>On-screen button that reports whether it is being held (touch controls for minigames).</summary>
public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public bool IsHeld { get; private set; }

    public void OnPointerDown(PointerEventData eventData) => IsHeld = true;

    public void OnPointerUp(PointerEventData eventData) => IsHeld = false;

    public void OnPointerExit(PointerEventData eventData) => IsHeld = false;

    private void OnDisable() => IsHeld = false;
}
