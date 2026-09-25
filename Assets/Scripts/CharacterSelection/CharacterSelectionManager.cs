using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class CharacterSelectionManager : MonoBehaviour
{
    [Header("UI References")]
    public Transform characterGrid;
    public GameObject characterCardPrefab;
    public TextMeshProUGUI selectedText;
    public Button continueButton;

    [Header("Data")]
    public List<CharacterData> availableCharacters;

    private List<CharacterData> selectedCharacters = new List<CharacterData>();
    private const int MinPlayers = 2;
    private const int MaxPlayers = 4;

    private void Start()
    {
        PopulateGrid();
        UpdateUI();
        
        if (continueButton != null)
        {
            continueButton.onClick.AddListener(OnContinueClicked);
        }
    }

    private void PopulateGrid()
    {
        foreach (Transform child in characterGrid)
        {
            Destroy(child.gameObject);
        }

        foreach (var data in availableCharacters)
        {
            var cardObj = Instantiate(characterCardPrefab, characterGrid);
            var cardUI = cardObj.GetComponent<CharacterCardUI>();
            if (cardUI != null)
            {
                cardUI.Setup(data, OnCharacterClicked);
            }
        }
    }

    private void OnCharacterClicked(CharacterData data, CharacterCardUI cardUI)
    {
        if (selectedCharacters.Contains(data))
        {
            selectedCharacters.Remove(data);
            cardUI.SetSelected(false);
        }
        else
        {
            if (selectedCharacters.Count < MaxPlayers)
            {
                selectedCharacters.Add(data);
                cardUI.SetSelected(true);
            }
            else
            {
                Debug.Log("Maximum number of players selected.");
            }
        }

        UpdateUI();
    }

    private void UpdateUI()
    {
        if (selectedText != null)
        {
            selectedText.text = $"Jugadores: {selectedCharacters.Count} / {MaxPlayers}";
        }

        if (continueButton != null)
        {
            continueButton.interactable = selectedCharacters.Count >= MinPlayers && selectedCharacters.Count <= MaxPlayers;
        }
    }

    private void OnContinueClicked()
    {
        if (selectedCharacters.Count >= MinPlayers && selectedCharacters.Count <= MaxPlayers)
        {
            if (GameSession.Instance == null)
            {
                var sessionObj = new GameObject("GameSession");
                sessionObj.AddComponent<GameSession>();
            }

            GameSession.Instance.SetPlayers(selectedCharacters);
            
            Debug.Log("Cargando escena Mapa con " + selectedCharacters.Count + " jugadores.");
            SceneManager.LoadScene("Mapa");
        }
    }
}
