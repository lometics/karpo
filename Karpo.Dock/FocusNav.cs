// keys: the one way the keyboard moves between the things it stops at, on the bar and in a folder
// (docs\widgets.md, "One keyboard"). Plain code without windows, so the tests can reach it.
//
//   Tab, Shift+Tab   the next or the one before, round (Step with wrap)
//   arrows           the nearest stop that way (Toward): straight ahead first, then the least way
//                    off to the side; a stop beside it across the way never counts
//   Home, End        the first, the last
//
// A stop is a rectangle; where they stand decides, so a grid, a list and a row of buttons on a list's
// item are all walked by the same rule.

using TerraFX.Interop.Windows;

namespace Karpo.Dock;

/// <summary>Which way an arrow goes, in the space the stops stand in (mirroring is the caller's).</summary>
public enum FocusWay
{
    Left,
    Right,
    Up,
    Down,
}

/// <summary>The keyboard's moves between stops (see the top of the file).</summary>
public static class FocusNav
{
    /// <summary>
    /// Where the keyboard goes from stop <paramref name="at"/> of <paramref name="count"/> by
    /// <paramref name="step"/>: round from one end to the other with <paramref name="wrap"/>, else held at
    /// the ends; from nowhere (-1), to the first going forward and the last going back. -1 when there are none.
    /// </summary>
    public static int Step(int count, int at, int step, bool wrap = true)
    {
        if (count <= 0)
        {
            return -1;
        }
        if (at < 0 || at >= count)
        {
            return step >= 0 ? 0 : count - 1;
        }
        return wrap ? ((at + step) % count + count) % count : Math.Clamp(at + step, 0, count - 1);
    }

    /// <summary>The way an arrow key goes; null for any other key.</summary>
    public static FocusWay? WayOf(int vk) => vk switch
    {
        VK.VK_LEFT => FocusWay.Left,
        VK.VK_RIGHT => FocusWay.Right,
        VK.VK_UP => FocusWay.Up,
        VK.VK_DOWN => FocusWay.Down,
        _ => null,
    };

    /// <summary>
    /// The stop of <paramref name="stops"/> nearest stop <paramref name="from"/> going <paramref name="way"/>:
    /// one whose middle is past <paramref name="from"/>'s that way, scored by how far ahead it is and by how
    /// far off to the side it stands — nothing where the two overlap across the way, so a full-width item
    /// under a button is as straight ahead as the button under it, and the one nearer the middle wins the
    /// tie. -1 where nothing is that way; from nowhere, the first.
    /// </summary>
    public static int Toward(IReadOnlyList<RectF> stops, int from, FocusWay way)
    {
        if (stops.Count == 0)
        {
            return -1;
        }
        if (from < 0 || from >= stops.Count)
        {
            return 0;
        }
        var here = stops[from];
        var origin = here.Center;
        var best = -1;
        var bestScore = float.MaxValue;
        for (var index = 0; index < stops.Count; index++)
        {
            var there = stops[index];
            if (index == from || there.IsEmpty)
            {
                continue;
            }
            var delta = there.Center - origin;
            var horizontal = way is FocusWay.Left or FocusWay.Right;
            var along = way switch
            {
                FocusWay.Left => -delta.X,
                FocusWay.Right => delta.X,
                FocusWay.Up => -delta.Y,
                _ => delta.Y,
            };
            if (along <= 1)
            {
                continue;
            }
            // Off to the side: the room between the two across the way, none where they overlap.
            var gap = horizontal
                ? MathF.Max(0, MathF.Max(there.Y - here.Bottom, here.Y - there.Bottom))
                : MathF.Max(0, MathF.Max(there.X - here.Right, here.X - there.Right));
            var offMiddle = MathF.Abs(horizontal ? delta.Y : delta.X);
            var score = along + (2 * gap) + (0.05f * offMiddle);
            if (score < bestScore)
            {
                bestScore = score;
                best = index;
            }
        }
        return best;
    }

    /// <summary>
    /// The stops in reading order: top to bottom by their tops, a row's left to right — two whose tops
    /// are within <paramref name="row"/> px of each other stand on one row.
    /// </summary>
    public static List<int> ReadingOrder(IReadOnlyList<RectF> stops, float row = 2)
    {
        var order = Enumerable.Range(0, stops.Count).OrderBy(index => stops[index].Y).ToList();
        // A row's stops by their left; the rows by their first's top.
        var rows = new List<List<int>>();
        foreach (var index in order)
        {
            if (rows.Count > 0 && MathF.Abs(stops[rows[^1][0]].Y - stops[index].Y) <= row)
            {
                rows[^1].Add(index);
            }
            else
            {
                rows.Add([index]);
            }
        }
        return [.. rows.SelectMany(line => line.OrderBy(index => stops[index].X))];
    }

}
