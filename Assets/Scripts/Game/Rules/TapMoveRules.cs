using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pure rules of the touch movement (no Unity scene, testable): which cells a player can step to
/// next, and which of them a tap on the board picks. The Host still validates every step.
/// </summary>
public static class TapMoveRules
{
    /// <summary>
    /// Cells one step away that the player may move to: the four neighbours without a wall between
    /// (<paramref name="canStep"/>), or — with "Cívica" (<paramref name="wallWalk"/>) — any neighbour
    /// that exists on the board (<paramref name="exists"/>), walls ignored.
    /// </summary>
    public static List<Vector2Int> StepTargets(Vector2Int cell, bool wallWalk,
        Func<Vector2Int, Vector2Int, bool> canStep, Func<Vector2Int, bool> exists)
    {
        List<Vector2Int> targets = new List<Vector2Int>(4);

        foreach (Vector2Int d in BoardLayout.Directions)
        {
            bool allowed = wallWalk ? exists(cell + d) : canStep(cell, d);

            if (allowed)
                targets.Add(cell + d);
        }

        return targets;
    }

    /// <summary>
    /// The target nearest to the tapped point (board plane, world units), if it is within
    /// <paramref name="radius"/> tiles of its centre; otherwise null. A radius over 0.5 makes the
    /// touch area a bit bigger than the tile, which helps on phones.
    /// </summary>
    public static Vector2Int? PickTarget(Vector2 tapWorld, IReadOnlyList<Vector2Int> targets, float tileSize, float radius = 0.75f)
    {
        Vector2Int? best = null;
        float bestDistance = radius * tileSize;

        foreach (Vector2Int target in targets)
        {
            float distance = Vector2.Distance(tapWorld, new Vector2(target.x * tileSize, target.y * tileSize));

            if (distance <= bestDistance)
            {
                bestDistance = distance;
                best = target;
            }
        }

        return best;
    }
}
