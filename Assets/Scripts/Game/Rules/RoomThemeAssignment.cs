using System.Collections.Generic;

/// <summary>
/// Decides which character styles each side room. Chosen characters fill the sides in player
/// order (North, East, South, West); free sides get characters nobody chose, picked with the
/// board seed so every instance agrees.
/// </summary>
public static class RoomThemeAssignment
{
    public const int None = -1;

    /// <summary>Sides in assignment order.</summary>
    public static readonly BoardRegion[] SideOrder =
    {
        BoardRegion.North, BoardRegion.East, BoardRegion.South, BoardRegion.West
    };

    /// <param name="chosenCharacterIds">Characters in the match, in PlayerId order.</param>
    /// <param name="characterCount">Characters in the catalog.</param>
    /// <returns>Character id per side, indexed like <see cref="SideOrder"/> (None if not enough characters).</returns>
    public static int[] Assign(IReadOnlyList<int> chosenCharacterIds, int characterCount, int seed)
    {
        int[] result = new int[SideOrder.Length];
        HashSet<int> used = new HashSet<int>();
        int side = 0;

        foreach (int id in chosenCharacterIds)
        {
            if (side >= result.Length)
                break;

            if (id < 0 || id >= characterCount || !used.Add(id))
                continue;

            result[side++] = id;
        }

        List<int> unused = new List<int>();

        for (int id = 0; id < characterCount; id++)
        {
            if (!used.Contains(id))
                unused.Add(id);
        }

        System.Random rng = new System.Random(seed);

        while (side < result.Length)
        {
            if (unused.Count == 0)
            {
                result[side++] = None;
                continue;
            }

            int index = rng.Next(unused.Count);
            result[side++] = unused[index];
            unused.RemoveAt(index);
        }

        return result;
    }
}
