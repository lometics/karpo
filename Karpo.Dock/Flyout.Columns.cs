// menus, §627: a level whose rows stand in columns side by side (2026-11-09, the person, of a colour's picker: "the design
// should be not row wise but column wise"). A separator marked Column (FlyoutItem.Column) starts the next column: the rows
// after it stand beside the ones before it, from the level's top, a hairline between them in the middle of a rule's room
// (MenuMetrics.Rule, as a rule across is). The picker's: its swatches — None or Default, a line of recent colours, two lines
// of presets — beside its mixer — the spectrum, the hue, the opacity, the hex box (MenuPickers.Rows).
//
//  - **Widths.** A column of tiles is as wide as a line of them (FlyoutTile.Across cells of TileCellDip), so its lines are
//    whole; the others share what is left, alike. Every row of a column fills its column as a row fills the menu; a tile
//    is spread across its column's room as across the menu's (TileFill).
//  - **Heights.** The level is as tall as its tallest column.
//  - **The keys.** Tab goes through the rows in their order: down the first column, then the next. Up and Down stay in
//    their column; past its end, Down goes on to the next column's first row and Up back to the one before's last.

using Karpo.Core;

namespace Karpo.Dock;

public sealed unsafe partial class Flyout
{
    /// <summary>§627: the column a row stands in: which (<paramref name="Index"/>, from 0), from where across (content px) and how wide.</summary>
    internal readonly record struct Lane(int Index, float X, float W, int Count)
    {
        public bool First => Index == 0;

        public bool Last => Index == Count - 1;
    }

    /// <summary>§627: whether <paramref name="item"/> starts a column (<see cref="FlyoutItem.Column"/>).</summary>
    internal static bool StartsColumn(FlyoutItem item) => item.Separator && item.Column;

    /// <summary>
    /// §627: the column each of <paramref name="items"/> stands in across <paramref name="room"/> px from
    /// <paramref name="left"/>, a rule's <paramref name="rule"/> px between two: one of tiles as wide as a line of them
    /// (<paramref name="cell"/> px a cell), the rest sharing what is left; null where the level is one column.
    /// </summary>
    internal static Lane[]? Lanes(IReadOnlyList<FlyoutItem> items, float left, float room, float cell, float rule)
    {
        var count = 1 + items.Count(StartsColumn);
        if (count == 1)
        {
            return null;
        }
        // What each column's tiles need: a line of them, at most what the room holds.
        var own = new float?[count];
        var at = 0;
        foreach (var item in items)
        {
            if (StartsColumn(item))
            {
                at++;
            }
            else if (IsTile(item) && item.Icon is FlyoutTile tile)
            {
                var square = tile.CellDip > 0 ? cell * tile.CellDip / TileCellDip : cell;
                own[at] = MathF.Max(own[at] ?? 0, tile.Across * square);
            }
        }
        var free = room - ((count - 1) * rule);
        var fixedWide = own.Sum(each => each ?? 0);
        var shared = own.Count(each => each is null);
        var share = shared > 0 ? MathF.Floor(MathF.Max(0, free - fixedWide) / shared) : 0;
        var x = new float[count];
        var w = new float[count];
        var across = left;
        for (var column = 0; column < count; column++)
        {
            (x[column], w[column]) = (across, MathF.Min(own[column] ?? share, MathF.Max(0, left + room - across)));
            across += w[column] + rule;
        }
        // The last column to the room's end, whatever the rounding left.
        w[count - 1] = MathF.Max(0, left + room - x[count - 1]);
        var lanes = new Lane[items.Count];
        at = 0;
        for (var index = 0; index < items.Count; index++)
        {
            if (StartsColumn(items[index]))
            {
                at++;
            }
            lanes[index] = new Lane(at, x[at], w[at], count);
        }
        return lanes;
    }

    /// <summary>Tests (§627): each row shown, its column and its fill (content px).</summary>
    internal IReadOnlyList<(FlyoutItem Item, int Lane, float X, float Y, float W, float H)> RowsLaid =>
        rows.ConvertAll(row => (row.Item, row.Lane, row.Bounds.X, row.Bounds.Y, row.Bounds.W, row.Bounds.H));

    private sealed partial class Row
    {
        /// <summary>§627: the column it stands in (Flyout.Columns.cs); 0 in a level of one.</summary>
        public int Lane;
    }

    /// <summary>
    /// §627: from row <paramref name="from"/>, the row Up or Down goes to past its column's end (none in its column that way,
    /// <paramref name="best"/> -1): the next column's first row going down, the column before's last going up; else
    /// <paramref name="best"/>.
    /// </summary>
    private int AcrossColumns(int from, bool down, int best)
    {
        if (best >= 0 || from < 0)
        {
            return best;
        }
        var lane = rows[from].Lane + (down ? 1 : -1);
        return down ? rows.FindIndex(row => row.Lane == lane) : rows.FindLastIndex(row => row.Lane == lane);
    }
}
