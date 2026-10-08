using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Temporary minigame used while a character has no MissionDefinition.minigamePrefab:
/// shows the mission and lets the player mark it as completed or failed.
/// Replace it per character with a real MissionMinigame prefab.
/// </summary>
public class PlaceholderMissionMinigame : MissionMinigame
{
    private TextMeshProUGUI resultText;
    private Button completeButton;
    private Button failButton;

    protected override void OnBegin()
    {
        RectTransform root = (RectTransform)transform;
        UIFactory.Stretch(root);

        Image dim = gameObject.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.6f);

        Image panel = UIFactory.CreatePanel(transform, "Panel", new Color(0.10f, 0.08f, 0.16f, 0.97f));
        UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 520f));

        string title = Definition != null ? Definition.title : "Misión";
        string description = Definition != null ? Definition.description : "";

        TextMeshProUGUI header = UIFactory.CreateText(panel.transform, "Header", "MISIÓN DE " + CharacterName.ToUpper(), 26f);
        UIFactory.Place(header.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(720f, 36f));
        header.color = UIFactory.AccentColor;

        TextMeshProUGUI titleText = UIFactory.CreateText(panel.transform, "Title", title, 44f);
        UIFactory.Place(titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -85f), new Vector2(720f, 60f));
        titleText.fontStyle = FontStyles.Bold;

        TextMeshProUGUI body = UIFactory.CreateText(panel.transform, "Description", description, 26f);
        UIFactory.Place(body.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -175f), new Vector2(700f, 110f));

        TextMeshProUGUI note = UIFactory.CreateText(panel.transform, "Note",
            IsLocalPlayer ? "<i>(Minijuego por diseñar: elige el resultado para probar)</i>" : $"{PlayerName} está jugando su misión...", 20f);
        UIFactory.Place(note.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -260f), new Vector2(700f, 40f));
        note.color = new Color(0.8f, 0.8f, 0.8f);

        resultText = UIFactory.CreateText(panel.transform, "Result", "", 38f);
        UIFactory.Place(resultText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -320f), new Vector2(700f, 60f));

        if (!IsLocalPlayer)
            return;

        completeButton = UIFactory.CreateButton(panel.transform, "CompleteButton", "COMPLETAR MISIÓN", () => Finish(true), UIFactory.OkColor, 26f);
        UIFactory.Place((RectTransform)completeButton.transform, new Vector2(0.5f, 0f), new Vector2(-170f, 40f), new Vector2(300f, 80f));

        failButton = UIFactory.CreateButton(panel.transform, "FailButton", "FALLAR", () => Finish(false), UIFactory.DangerColor, 26f);
        UIFactory.Place((RectTransform)failButton.transform, new Vector2(0.5f, 0f), new Vector2(170f, 40f), new Vector2(300f, 80f));
    }

    public override void ShowResult(bool success)
    {
        if (resultText != null)
            resultText.text = success ? "<color=#60FF60>¡MISIÓN COMPLETADA!</color>" : "<color=#FF5050>MISIÓN FALLIDA</color>";

        if (completeButton != null) completeButton.interactable = false;
        if (failButton != null) failButton.interactable = false;
    }
}
