using System.Collections.Generic;
using UnityEngine;

public class GameSession : MonoBehaviour
{
    public static GameSession Instance { get; private set; }

    public List<PlayerState> Players { get; private set; } = new List<PlayerState>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void SetPlayers(List<CharacterData> selectedCharacters)
    {
        Players.Clear();
        foreach (var data in selectedCharacters)
        {
            Players.Add(new PlayerState(data));
        }
    }
}
