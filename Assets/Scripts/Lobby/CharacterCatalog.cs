using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Maps network CharacterId (index) to the existing CharacterData assets.
/// Lives in Resources/CharacterCatalog.asset. Does not duplicate character data.
/// </summary>
[CreateAssetMenu(fileName = "CharacterCatalog", menuName = "PPOP/Character Catalog")]
public class CharacterCatalog : ScriptableObject
{
    public List<CharacterData> characters = new List<CharacterData>();

    private static CharacterCatalog cached;

    public static CharacterCatalog Instance
    {
        get
        {
            if (cached == null)
            {
                cached = Resources.Load<CharacterCatalog>("CharacterCatalog");

                if (cached == null)
                {
                    Debug.LogError("CharacterCatalog: Resources/CharacterCatalog.asset not found.");
                    cached = CreateInstance<CharacterCatalog>();
                }
            }

            return cached;
        }
    }

    public int Count => characters.Count;

    public CharacterData Get(int characterId)
    {
        if (characterId < 0 || characterId >= characters.Count)
            return null;

        return characters[characterId];
    }

    public int IndexOf(CharacterData data)
    {
        return characters.IndexOf(data);
    }

    public string GetName(int characterId)
    {
        CharacterData data = Get(characterId);
        return data != null ? data.characterName : "-";
    }
}
