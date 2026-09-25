using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[InitializeOnLoad]
public static class PokemonCardSetup
{
    static PokemonCardSetup()
    {
        EditorApplication.delayCall += () =>
        {
            if (!EditorPrefs.GetBool("PokemonCardSetupDone", false))
            {
                ApplyPokemonStyle();
                EditorPrefs.SetBool("PokemonCardSetupDone", true);
            }
        };
    }

    [MenuItem("Tools/Reapply Pokemon Card Style")]
    public static void ApplyPokemonStyle()
    {
        string prefabPath = "Assets/Prefabs/CharacterCard.prefab";
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
        if (prefabRoot == null) return;

        // Change card background
        Image cardImage = prefabRoot.GetComponent<Image>();
        if (cardImage != null)
        {
            cardImage.color = new Color(0.95f, 0.95f, 0.85f); // Light vintage card color
        }

        RectTransform cardRect = prefabRoot.GetComponent<RectTransform>();
        cardRect.sizeDelta = new Vector2(250, 350);

        CharacterCardUI uiScript = prefabRoot.GetComponent<CharacterCardUI>();

        // Find existing or create new GameObjects for texts
        TextMeshProUGUI abilityNameTxt = null;
        TextMeshProUGUI abilityDescTxt = null;

        foreach (Transform child in prefabRoot.transform)
        {
            if (child.name == "AbilityNameText") abilityNameTxt = child.GetComponent<TextMeshProUGUI>();
            if (child.name == "AbilityDescriptionText") abilityDescTxt = child.GetComponent<TextMeshProUGUI>();
        }

        if (abilityNameTxt == null)
        {
            GameObject anObj = new GameObject("AbilityNameText", typeof(RectTransform), typeof(TextMeshProUGUI));
            anObj.transform.SetParent(prefabRoot.transform, false);
            abilityNameTxt = anObj.GetComponent<TextMeshProUGUI>();
        }

        if (abilityDescTxt == null)
        {
            GameObject adObj = new GameObject("AbilityDescriptionText", typeof(RectTransform), typeof(TextMeshProUGUI));
            adObj.transform.SetParent(prefabRoot.transform, false);
            abilityDescTxt = adObj.GetComponent<TextMeshProUGUI>();
        }

        uiScript.abilityNameText = abilityNameTxt;
        uiScript.abilityDescriptionText = abilityDescTxt;

        // 1. Name Text (Top Left)
        if (uiScript.characterNameText != null)
        {
            RectTransform rt = uiScript.characterNameText.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.05f, 0.9f);
            rt.anchorMax = new Vector2(0.7f, 0.98f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            uiScript.characterNameText.alignment = TextAlignmentOptions.Left;
            uiScript.characterNameText.color = new Color(0.1f, 0.1f, 0.1f);
            uiScript.characterNameText.fontStyle = FontStyles.Bold;
            uiScript.characterNameText.fontSize = 20;
        }

        // 2. Lives Text (Top Right)
        if (uiScript.livesText != null)
        {
            RectTransform rt = uiScript.livesText.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.7f, 0.9f);
            rt.anchorMax = new Vector2(0.95f, 0.98f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            uiScript.livesText.alignment = TextAlignmentOptions.Right;
            uiScript.livesText.color = new Color(0.8f, 0.1f, 0.1f);
            uiScript.livesText.fontStyle = FontStyles.Bold;
            uiScript.livesText.fontSize = 20;
        }

        // 3. Portrait (Middle Top)
        Transform portraitObj = prefabRoot.transform.Find("Portrait");
        if (portraitObj != null)
        {
            RectTransform rt = portraitObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.05f, 0.5f);
            rt.anchorMax = new Vector2(0.95f, 0.88f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            
            Image img = portraitObj.GetComponent<Image>();
            img.color = Color.white;
            
            string imagePath = "Assets/image (8).png";
            
            // Ensure texture is imported as Sprite
            TextureImporter importer = AssetImporter.GetAtPath(imagePath) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.SaveAndReimport();
            }

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(imagePath);
            if (sprite != null)
            {
                img.sprite = sprite;
            }
            else
            {
                Debug.LogWarning("Could not load Sprite from " + imagePath);
            }
        }

        // 4. Ability Name
        if (abilityNameTxt != null)
        {
            RectTransform rt = abilityNameTxt.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.05f, 0.4f);
            rt.anchorMax = new Vector2(0.95f, 0.48f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            abilityNameTxt.alignment = TextAlignmentOptions.Center;
            abilityNameTxt.color = new Color(0.1f, 0.1f, 0.1f);
            abilityNameTxt.fontStyle = FontStyles.Bold;
            abilityNameTxt.fontSize = 18;
            abilityNameTxt.text = "Ability Name";
        }

        // 5. Ability Description
        if (abilityDescTxt != null)
        {
            RectTransform rt = abilityDescTxt.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.05f, 0.15f);
            rt.anchorMax = new Vector2(0.95f, 0.38f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            abilityDescTxt.alignment = TextAlignmentOptions.TopLeft;
            abilityDescTxt.color = new Color(0.2f, 0.2f, 0.2f);
            abilityDescTxt.fontSize = 14;
            abilityDescTxt.enableWordWrapping = true;
            abilityDescTxt.text = "Ability description goes here and can take up multiple lines.";
        }

        // 6. Damage Text
        if (uiScript.damageText != null)
        {
            RectTransform rt = uiScript.damageText.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.05f, 0.02f);
            rt.anchorMax = new Vector2(0.45f, 0.1f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            uiScript.damageText.alignment = TextAlignmentOptions.BottomLeft;
            uiScript.damageText.color = new Color(0.1f, 0.1f, 0.1f);
            uiScript.damageText.fontStyle = FontStyles.Bold;
        }

        // 7. Defense Text
        if (uiScript.defenseText != null)
        {
            RectTransform rt = uiScript.defenseText.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.55f, 0.02f);
            rt.anchorMax = new Vector2(0.95f, 0.1f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            uiScript.defenseText.alignment = TextAlignmentOptions.BottomRight;
            uiScript.defenseText.color = new Color(0.1f, 0.1f, 0.1f);
            uiScript.defenseText.fontStyle = FontStyles.Bold;
        }

        // 8. Selected Visual
        Transform selObj = prefabRoot.transform.Find("SelectedVisual");
        if (selObj != null)
        {
            RectTransform rt = selObj.GetComponent<RectTransform>();
            rt.offsetMin = new Vector2(-15, -15);
            rt.offsetMax = new Vector2(15, 15);
            Image img = selObj.GetComponent<Image>();
            img.color = new Color(1f, 0.8f, 0.2f, 1f); // solid gold
            // Let's create an outline effect visually by using border if we had a sprite, 
            // but just scaling it slightly behind the main card is what we do already.
        }

        PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
        PrefabUtility.UnloadPrefabContents(prefabRoot);

        Debug.Log("Pokemon Card style applied successfully!");
    }
}
