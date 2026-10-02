using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class CharacterCardUI : MonoBehaviour
{
    public TextMeshProUGUI characterNameText;
    public TextMeshProUGUI damageText;
    public TextMeshProUGUI defenseText;
    public TextMeshProUGUI livesText;
    public TextMeshProUGUI abilityNameText;
    public TextMeshProUGUI abilityDescriptionText;
    public Image selectedVisual;
    public Image portraitImage;
    public Button cardButton;

    private CharacterData currentData;
    private Action<CharacterData, CharacterCardUI> onClickCallback;
    private bool isSelected;

    public void Setup(CharacterData data, Action<CharacterData, CharacterCardUI> callback)
    {
        currentData = data;
        onClickCallback = callback;

        if (characterNameText != null) characterNameText.text = data.characterName;
        if (damageText != null) damageText.text = "ATK: " + data.baseDamage;
        if (defenseText != null) defenseText.text = "DEF: " + data.baseDefense;
        if (livesText != null) livesText.text = "HP: " + data.maxLives;
        if (abilityNameText != null) abilityNameText.text = data.abilityName;
        if (abilityDescriptionText != null) abilityDescriptionText.text = data.abilityDescription;
        
        if (portraitImage != null && data.characterImage != null) 
        {
            portraitImage.sprite = data.characterImage;
        }

        SetSelected(false);

        if (cardButton != null)
        {
            cardButton.onClick.RemoveAllListeners();
            cardButton.onClick.AddListener(OnCardClicked);
        }
    }

    private void OnCardClicked()
    {
        onClickCallback?.Invoke(currentData, this);
    }

    public CharacterData Data => currentData;

    private CanvasGroup canvasGroup;
    private TextMeshProUGUI ownerLabel;

    /// <summary>Network lobby: dims the card and shows who owns it. Empty owner = free.</summary>
    public void SetOwner(string ownerName, bool takenByOther)
    {
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        canvasGroup.alpha = takenByOther ? 0.4f : 1f;

        if (cardButton != null)
            cardButton.interactable = !takenByOther;

        if (ownerLabel == null)
        {
            Image band = UIFactory.CreatePanel(transform, "OwnerBand", new Color(0f, 0f, 0f, 0.75f));
            UIFactory.Place(band.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(230f, 40f));
            band.raycastTarget = false;
            ownerLabel = UIFactory.CreateText(band.transform, "OwnerLabel", "", 24f);
            UIFactory.Stretch(ownerLabel.rectTransform);
        }

        ownerLabel.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(ownerName));
        ownerLabel.text = ownerName;
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        if (selectedVisual != null)
        {
            selectedVisual.gameObject.SetActive(isSelected);
        }
    }
}
