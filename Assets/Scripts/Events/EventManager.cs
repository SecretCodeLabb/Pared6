using System.Collections.Generic;
using UnityEngine;

public class EventManager : MonoBehaviour
{
    public static EventManager Instance;

    [Header("Event Settings")]
    [Tooltip("Event tiles on the central board.")]
    [Range(1, 40)]
    public int eventTileCount = 5;

    [Tooltip("Event tiles inside each side room.")]
    [Range(0, 20)]
    public int eventsPerRoom = 3;

    [Header("References")]
    public MapGenerator mapGenerator;

    [Header("Visual")]
    public GameObject eventIndicatorPrefab;
    public float eventIndicatorHeight = 0.5f;
    [Tooltip("Multiplier on the indicator prefab's scale so events are easy to spot.")]
    [Min(0.1f)] public float eventIndicatorScale = 1f;

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

    // =========================================================
    // OFFLINE: el generador local sortea los eventos
    // =========================================================

    public void GenerateEventTiles()
    {
        if (mapGenerator == null)
        {
            Debug.LogError("EventManager: No hay MapGenerator asignado.");
            return;
        }

        List<Tile> tiles = new List<Tile>(mapGenerator.GetComponentsInChildren<Tile>());
        System.Random rng = new System.Random(Random.Range(int.MinValue, int.MaxValue));

        List<Vector2Int> cells = PickEventCells(tiles, rng, new HashSet<Vector2Int>());
        ApplyEventCells(tiles, cells);
    }

    // =========================================================
    // RED (T05 / T09): el Host decide, todos aplican
    // =========================================================

    /// <summary>
    /// Picks event cells spread over the whole map: eventTileCount on the central board and
    /// eventsPerRoom inside every room. Deterministic for a given RNG; excluded cells are skipped.
    /// </summary>
    public List<Vector2Int> PickEventCells(
        IEnumerable<Tile> tiles,
        System.Random rng,
        ICollection<Vector2Int> excluded)
    {
        Dictionary<BoardRegion, List<Vector2Int>> byRegion = new Dictionary<BoardRegion, List<Vector2Int>>();

        foreach (Tile tile in tiles)
        {
            Vector2Int cell = BoardManager.CellOf(tile);

            if (excluded.Contains(cell))
                continue;

            if (!byRegion.TryGetValue(tile.region, out List<Vector2Int> list))
            {
                list = new List<Vector2Int>();
                byRegion[tile.region] = list;
            }

            list.Add(cell);
        }

        List<Vector2Int> picked = new List<Vector2Int>();
        BoardRegion[] order = { BoardRegion.Center, BoardRegion.North, BoardRegion.South, BoardRegion.East, BoardRegion.West };

        foreach (BoardRegion region in order)
        {
            if (!byRegion.TryGetValue(region, out List<Vector2Int> candidates))
                continue;

            candidates.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            int amount = Mathf.Min(region == BoardRegion.Center ? eventTileCount : eventsPerRoom, candidates.Count);

            for (int i = 0; i < amount; i++)
            {
                int index = rng.Next(candidates.Count);
                picked.Add(candidates[index]);
                candidates.RemoveAt(index);
            }
        }

        return picked;
    }

    /// <summary>All instances: marks the given cells as event tiles with the existing indicator.</summary>
    public void ApplyEventCells(IEnumerable<Tile> tiles, IEnumerable<Vector2Int> cells)
    {
        Dictionary<Vector2Int, Tile> lookup = new Dictionary<Vector2Int, Tile>();

        foreach (Tile tile in tiles)
            lookup[BoardManager.CellOf(tile)] = tile;

        eventTiles.Clear();

        foreach (Vector2Int cell in cells)
        {
            if (!lookup.TryGetValue(cell, out Tile tile))
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

        Debug.Log("EventManager: " + eventTiles.Count + " casillas de evento.");
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
        indicator.transform.localScale *= eventIndicatorScale;
        eventTile.indicator = indicator;
    }

    /// <summary>Active (not beaten, not yet triggered offline) event on this tile.</summary>
    public bool HasActiveEvent(Tile tile)
    {
        if (tile == null)
            return false;

        EventTile eventTile = tile.GetComponent<EventTile>();
        return eventTile != null && eventTile.hasEvent && !eventTile.IsTriggered();
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

    // =========================================================
    // OFFLINE (comportamiento original)
    // =========================================================

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
