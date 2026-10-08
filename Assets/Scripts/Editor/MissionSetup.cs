using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates one placeholder MissionDefinition per character (only where CharacterData.mission is
/// empty — never overwrites your edits). PPOP/Create Missions (also run by PPOP/Setup Prototype).
/// </summary>
public static class MissionSetup
{
    private const string MissionsDir = "Assets/ScriptableObjects/Missions";

    // Placeholder title / description by catalog index (Chismosa, Puppy, Vagabundo, Skater, Trabajador, Estudiante).
    private static readonly string[,] Texts =
    {
        { "El chisme del siglo", "Reúne las pistas y difunde el chisme antes de que se enfríe." },
        { "La pelota perdida", "Encuentra la pelota escondida en tu cuarto." },
        { "Bolsa llena", "Rebusca entre las cajas y llena tu bolsa con lo necesario." },
        { "El truco perfecto", "Clava el truco en la rampa sin caerte." },
        { "Llegar al viernes", "Termina el papeleo antes de que se acabe el café." },
        { "Examen final", "Estudia lo justo y aprueba el examen." }
    };

    [MenuItem("PPOP/Create Missions")]
    public static void CreateAll()
    {
        CharacterCatalog catalog = AssetDatabase.LoadAssetAtPath<CharacterCatalog>("Assets/Resources/CharacterCatalog.asset");

        if (catalog == null)
        {
            Debug.LogError("[PPOP] Missions: CharacterCatalog not found.");
            return;
        }

        PrototypeSetup.EnsureFolder(MissionsDir);

        for (int i = 0; i < catalog.characters.Count; i++)
        {
            CharacterData character = catalog.characters[i];

            if (character == null || character.mission != null)
                continue;

            string safe = SafeName(character.characterName);
            string path = MissionsDir + "/Mission_" + safe + ".asset";
            MissionDefinition mission = AssetDatabase.LoadAssetAtPath<MissionDefinition>(path);

            if (mission == null)
            {
                mission = ScriptableObject.CreateInstance<MissionDefinition>();
                int row = i % Texts.GetLength(0);
                mission.title = Texts[row, 0];
                mission.description = Texts[row, 1];
                AssetDatabase.CreateAsset(mission, path);
            }

            character.mission = mission;
            EditorUtility.SetDirty(character);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[PPOP] Misiones listas.");
    }

    private static string SafeName(string name)
    {
        string result = "";

        foreach (char c in name)
            result += char.IsLetterOrDigit(c) ? c.ToString() : "";

        return string.IsNullOrEmpty(result) ? "Personaje" : result;
    }
}
