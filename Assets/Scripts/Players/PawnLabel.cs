using TMPro;
using UnityEngine;

/// <summary>
/// Floating name above a pawn (the chosen character's name). Always faces the camera.
/// Used by PlayerNetworkData (network) and BoardManager (offline pawn).
/// </summary>
public class PawnLabel : MonoBehaviour
{
    [SerializeField] private float height = 2.6f;
    [SerializeField] private float fontSize = 6f;

    private TextMeshPro text;

    public static PawnLabel AddTo(GameObject pawn)
    {
        PawnLabel label = pawn.GetComponent<PawnLabel>();
        return label != null ? label : pawn.AddComponent<PawnLabel>();
    }

    private void EnsureText()
    {
        if (text != null)
            return;

        GameObject labelObject = new GameObject("NameLabel");
        labelObject.transform.SetParent(transform, false);
        labelObject.transform.localPosition = new Vector3(0f, height, 0f);

        text = labelObject.AddComponent<TextMeshPro>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = fontSize;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.outlineWidth = 0.25f;
        text.outlineColor = Color.black;
        text.color = Color.white;
    }

    public void SetText(string value)
    {
        EnsureText();
        text.text = value;
    }

    public void SetVisible(bool visible)
    {
        EnsureText();
        text.gameObject.SetActive(visible);
    }

    private void LateUpdate()
    {
        if (text != null && Camera.main != null)
            text.transform.rotation = Camera.main.transform.rotation;
    }
}
