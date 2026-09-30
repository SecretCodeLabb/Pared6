using System.Collections.Generic;
using UnityEngine;

public class EventManager : MonoBehaviour
{
    public static EventManager Instance;

    [Header("Event Settings")]
    [Range(1, 20)]
    public int eventTileCount = 5;

    [Header("References")]
    public MapGenerator mapGenerator;

    [Header("Visual")]
    public GameObject eventIndicatorPrefab;
    public float eventIndicatorHeight = 0.5f;

    private List<EventTile> eventTiles = new List<EventTile>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void GenerateEventTiles()
    {
        eventTiles.Clear();

        if (mapGenerator == null)
        {
            Debug.LogError("EventManager: No hay MapGenerator asignado.");
            return;
        }

        List<Tile> centralTiles = new List<Tile>();

        Tile[] allTiles = mapGenerator.GetComponentsInChildren<Tile>();

        foreach (Tile tile in allTiles)
        {
            if (IsCentralTile(tile))
            {
                centralTiles.Add(tile);
            }
        }

        if (centralTiles.Count == 0)
        {
            Debug.LogError("EventManager: No se encontraron tiles del mapa central.");
            return;
        }

        int amount = Mathf.Min(eventTileCount, centralTiles.Count);

        for (int i = 0; i < amount; i++)
        {
            int randomIndex = Random.Range(0, centralTiles.Count);

            Tile selectedTile = centralTiles[randomIndex];

            centralTiles.RemoveAt(randomIndex);

            EventTile eventTile = selectedTile.GetComponent<EventTile>();

            if (eventTile == null)
            {
                eventTile = selectedTile.gameObject.AddComponent<EventTile>();
            }

            eventTile.hasEvent = true;
            eventTile.ResetEvent();

            if (eventIndicatorPrefab != null)
            {
                GameObject indicator = Instantiate(
                    eventIndicatorPrefab,
                    selectedTile.transform
                );

                indicator.transform.localPosition = new Vector3(
                    0f,
                    eventIndicatorHeight,
                    0f
                );

                indicator.transform.localRotation = Quaternion.identity;
            }

            eventTiles.Add(eventTile);

            Debug.Log(
                "Casilla de evento asignada: " +
                selectedTile.gridPosition
            );
        }

        Debug.Log(
            "EventManager: " +
            eventTiles.Count +
            " casillas de evento generadas."
        );
    }

    private bool IsCentralTile(Tile tile)
    {
        if (tile == null)
            return false;

        Transform parent = tile.transform.parent;

        if (parent == null)
            return false;

        return parent.GetComponent<MapGenerator>() != null;
    }

    public void CheckTile(Tile tile)
    {
        if (tile == null)
            return;

        EventTile eventTile = tile.GetComponent<EventTile>();

        if (eventTile == null)
            return;

        eventTile.ActivateEvent();
    }

    public void TriggerEvent(EventTile eventTile)
    {
        if (eventTile == null)
            return;

        Debug.Log(
            "EVENTO ACTIVADO en la casilla " +
            eventTile.gameObject.name
        );

        if (EventUI.Instance != null)
        {
            EventUI.Instance.ShowEventMessage();
        }

        // Aquí posteriormente agregaremos
        // las diferentes mecánicas de eventos.
    }

    public List<EventTile> GetEventTiles()
    {
        return eventTiles;
    }
}