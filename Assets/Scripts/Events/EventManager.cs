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

            CreateIndicator(eventTile);

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

    private void CreateIndicator(EventTile eventTile)
    {
        if (eventIndicatorPrefab == null || eventTile.indicator != null)
            return;

        GameObject indicator = Instantiate(
            eventIndicatorPrefab,
            eventTile.transform
        );

        indicator.transform.localPosition = new Vector3(
            0f,
            eventIndicatorHeight,
            0f
        );

        indicator.transform.localRotation = Quaternion.identity;
        eventTile.indicator = indicator;
    }

    // =========================================================
    // RED (T05 / T09): el Host decide, todos aplican
    // =========================================================

    /// <summary>Host only: picks event cells among central tiles, excluding the given cells.</summary>
    public List<Vector2Int> PickEventCells(
        BoardManager board,
        System.Random rng,
        ICollection<Vector2Int> excluded)
    {
        List<Vector2Int> candidates = new List<Vector2Int>();

        foreach (Tile tile in board.GetCentralTiles())
        {
            Vector2Int cell = BoardManager.CellOf(tile);

            if (!excluded.Contains(cell))
                candidates.Add(cell);
        }

        candidates.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));

        List<Vector2Int> picked = new List<Vector2Int>();
        int amount = Mathf.Min(eventTileCount, candidates.Count);

        for (int i = 0; i < amount; i++)
        {
            int index = rng.Next(candidates.Count);
            picked.Add(candidates[index]);
            candidates.RemoveAt(index);
        }

        return picked;
    }

    /// <summary>All instances: marks the given cells as event tiles with the existing indicator.</summary>
    public void ApplyEventCells(BoardManager board, IEnumerable<Vector2Int> cells)
    {
        eventTiles.Clear();

        foreach (Vector2Int cell in cells)
        {
            Tile tile = board.GetTile(cell);

            if (tile == null)
            {
                Debug.LogError("EventManager: event cell " + cell + " does not exist on this board.");
                continue;
            }

            EventTile eventTile = tile.GetComponent<EventTile>();

            if (eventTile == null)
                eventTile = tile.gameObject.AddComponent<EventTile>();

            eventTile.hasEvent = true;
            eventTile.ResetEvent();
            CreateIndicator(eventTile);
            eventTiles.Add(eventTile);
        }

        Debug.Log("EventManager: " + eventTiles.Count + " casillas de evento aplicadas (red).");
    }

    public bool HasActiveEvent(Tile tile)
    {
        if (tile == null)
            return false;

        EventTile eventTile = tile.GetComponent<EventTile>();
        return eventTile != null && eventTile.hasEvent;
    }

    /// <summary>All instances: the event was beaten, hide its indicator and disable it.</summary>
    public void ConsumeEvent(Tile tile)
    {
        if (tile == null)
            return;

        EventTile eventTile = tile.GetComponent<EventTile>();

        if (eventTile == null)
            return;

        eventTile.hasEvent = false;

        if (eventTile.indicator != null)
            eventTile.indicator.SetActive(false);
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