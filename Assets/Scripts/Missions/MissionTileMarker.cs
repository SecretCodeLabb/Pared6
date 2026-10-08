using TMPro;
using UnityEngine;

/// <summary>
/// Visual marker on a mission tile: the MissionDefinition.tileMarkerPrefab if assigned,
/// otherwise a glowing disc + beam in the character's colour with a "MISIÓN" label.
/// Purely visual (no colliders).
/// </summary>
public class MissionTileMarker : MonoBehaviour
{
    public static MissionTileMarker Create(Tile tile, CharacterData character)
    {
        GameObject root = new GameObject("MissionMarker_" + character.characterName);
        root.transform.SetParent(tile.transform, false);
        MissionTileMarker marker = root.AddComponent<MissionTileMarker>();

        Color color = character.boardColor.a > 0f ? character.boardColor : Color.yellow;
        GameObject custom = character.mission != null ? character.mission.tileMarkerPrefab : null;

        if (custom != null)
        {
            Instantiate(custom, root.transform, false);
        }
        else
        {
            AddShape(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.12f, 0f), new Vector3(1.5f, 0.03f, 1.5f), color);
            AddShape(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 1.2f, 0f), new Vector3(0.18f, 1.1f, 0.18f), Color.Lerp(color, Color.white, 0.4f));
        }

        GameObject labelObject = new GameObject("Label");
        labelObject.transform.SetParent(root.transform, false);
        labelObject.transform.localPosition = new Vector3(0f, 2.8f, 0f);
        TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
        label.text = "<b>MISIÓN</b>\n<size=70%>" + character.characterName + "</size>";
        label.fontSize = 5f;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.color = Color.Lerp(color, Color.white, 0.3f);
        label.outlineWidth = 0.25f;
        label.outlineColor = new Color32(0, 0, 0, 255);
        marker.label = label;

        return marker;
    }

    private TextMeshPro label;

    private static void AddShape(Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Color color)
    {
        GameObject shape = GameObject.CreatePrimitive(type);
        shape.transform.SetParent(parent, false);
        shape.transform.localPosition = position;
        shape.transform.localScale = scale;
        Destroy(shape.GetComponent<Collider>());

        Renderer renderer = shape.GetComponent<Renderer>();
        Material material = renderer.material;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);

        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 0.6f);
        }
    }

    private void LateUpdate()
    {
        if (label != null && Camera.main != null)
            label.transform.rotation = Camera.main.transform.rotation;
    }
}
