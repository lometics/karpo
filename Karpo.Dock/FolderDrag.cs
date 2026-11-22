// edit: a line carried in an arranged folder, as the bar carries a block (the person, 2026-10-12:
// "the editing of the list views should be made as smooth as the taskbar, with that i mean the
// reordering specifically"). The rules, apart from any window so they can be tested; the folder's
// visuals follow them (FolderFlyout.Drag.cs).
//
// The carried line follows the pointer; where it would land is read from where the line itself
// is, not from the pointer alone. In its own list it passes a line once its leading edge is past
// that line's middle — its bottom going down, its top going up — so a tall line carried down past
// a short one takes its place as soon as it covers half of it, and a short one past a tall one
// once it reaches the tall one's middle. Read from the pointer (or the carried line's own middle)
// a tall line had to be carried right over a short one before they swapped: let go over the middle
// of the short one, it went back where it was. In another list, where nothing gave way for it yet,
// its middle against the lines' middles decides. Every line's place is read where it stands at
// rest, so the lines giving way never move the place under the pointer, and nothing flickers back
// and forth.
//
// The lines that give way stand where the folder will lay them out once the line is let go: the
// folder planned again with the move made (FolderFlyout.Build), each line where that plan has it,
// held to the bar's side of the panel as the lists are.

using System.Numerics;
using Karpo.Core;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;

namespace Karpo.Dock;

internal static class FolderDrag
{
    /// <summary>
    /// The gap of list <paramref name="list"/> a line carried to <paramref name="top"/>..<paramref name="bottom"/>
    /// (px of the plan) would land in, read as <see cref="Folders.Move"/>
    /// reads a place: the gap above the line standing there now, the list's count for under its
    /// last. <paramref name="places"/> is where each of the plan's lines stands in the folder (null
    /// for one it has lost), <paramref name="moving"/> the carried one's index among them.
    /// </summary>
    internal static int Gap(FolderPlan plan, IReadOnlyList<FolderPlace?> places, int moving, int list, float top, float bottom)
    {
        var lines = new List<(int Index, D2D_RECT_F Card)>();
        for (var index = 0; index < places.Count && index < plan.Cards.Count; index++)
        {
            // A line the list has no room to show is no place to land by (FolderListPlan.Hidden): under
            // the last one shown is before them, where what is let go is seen.
            if (index != moving && places[index] is { } at && at.List == list && plan.Holds(index))
            {
                lines.Add((at.Index, plan.Cards[index]));
            }
        }
        lines.Sort((a, b) => a.Index.CompareTo(b.Index));
        var end = lines.Count == 0 ? 0 : lines[^1].Index + 1;
        if (moving >= 0 && moving < places.Count && places[moving] is { } from && from.List == list)
        {
            // Its own list: past a line's middle with its leading edge, it takes that line's place.
            var gap = from.Index;
            for (var each = lines.Count - 1; each >= 0; each--)
            {
                if (lines[each].Index >= from.Index)
                {
                    continue;
                }
                if (top >= Middle(lines[each].Card))
                {
                    break;
                }
                gap = lines[each].Index;
            }
            if (gap != from.Index)
            {
                return gap;
            }
            foreach (var (index, card) in lines)
            {
                if (index <= from.Index)
                {
                    continue;
                }
                if (bottom <= Middle(card))
                {
                    break;
                }
                gap = index + 1;
            }
            return gap;
        }
        // Another list: above the first line whose middle is under the carried line's middle.
        var middle = (top + bottom) / 2;
        foreach (var (index, card) in lines)
        {
            if (middle < Middle(card))
            {
                return index;
            }
        }
        return end;
    }

    /// <summary>
    /// <paramref name="options"/> with <paramref name="cells"/> — one line, or several carried
    /// together in the order they go down in — moved to <paramref name="to"/>
    /// (<see cref="Folders.MoveKeepingLists"/>), a copy: what the folder would be if they were let go
    /// there, every list where it stands — the empty ones letting go drops (Folders.Trim, §328) too, so
    /// no line of the plan shifts by a column while the plan is read against the folder as it is.
    /// The lists are copied, not the cells, so a plan of it places the same cells. Where the move
    /// changes nothing, the copy is the folder as it is. Over no place the carried lines are left
    /// where they are and laid out in no list (FolderFlyout.Build's <c>away</c>, §230): no copy needed.
    /// </summary>
    internal static FolderOptions Moved(FolderOptions options, IReadOnlyList<FolderCell> cells, FolderPlace to)
    {
        var copy = Copy(options);
        _ = Folders.MoveKeepingLists(copy, cells, to);
        return copy;
    }

    /// <summary>
    /// edit: whether letting <paramref name="cells"/> go at <paramref name="want"/> changes
    /// <paramref name="options"/> (<see cref="Folders.MoveGroup"/>): false just above or below where
    /// they stand, where the landing is not shown and a release is still a click.
    /// </summary>
    internal static bool Moves(FolderOptions options, IReadOnlyList<FolderCell> cells, FolderPlace want) =>
        Folders.MoveKeepingLists(Copy(options), cells, want);

    /// <summary>
    /// edit: how far down from the top of the stack of lines carried together each stands, the
    /// lines <paramref name="heights"/> px tall one under the other <paramref name="gap"/> px apart.
    /// </summary>
    internal static float[] Stack(IReadOnlyList<float> heights, float gap)
    {
        var downs = new float[heights.Count];
        var down = 0f;
        for (var each = 0; each < heights.Count; each++)
        {
            downs[each] = down;
            down += heights[each] + gap;
        }
        return downs;
    }

    /// <summary>A copy of <paramref name="options"/> whose lists can change apart from it, the cells the same.</summary>
    private static FolderOptions Copy(FolderOptions options) => new()
    {
        Lists = [.. (options.Lists ?? []).Select(list => list is null ? [] : new List<FolderCell>(list))],
        Widths = options.Widths is { } widths ? [.. widths] : null,
    };

    /// <summary>
    /// How far down to move <paramref name="arranged"/>'s every point to stand it in
    /// <paramref name="rest"/>'s panel, both lined up on the bar's side as the lists are
    /// (<see cref="FolderFlyout.Build"/>, <see cref="FolderFlyout.Over"/>): nothing on a top bar, where
    /// the lists hang from their tops; on a bottom bar the difference of the two heights, their
    /// bottoms level; on a side bar half of it, their middles level.
    /// </summary>
    internal static float Down(FolderPlan rest, FolderPlan arranged, Edge edge) =>
        PopupPaint.Round((rest.Height - arranged.Height) * FolderFlyout.Over(edge));

    /// <summary>
    /// Where line <paramref name="index"/>'s top left corner stands in <paramref name="arranged"/>,
    /// in <paramref name="rest"/>'s panel px (<see cref="Down"/>).
    /// </summary>
    internal static Vector2 At(FolderPlan rest, FolderPlan arranged, int index, Edge edge) =>
        index >= 0 && index < arranged.Cards.Count
            ? new Vector2(arranged.Cards[index].left, arranged.Cards[index].top + Down(rest, arranged, edge))
            : Vector2.Zero;

    /// <summary>
    /// Where Ctrl+arrow <paramref name="key"/> takes a line standing at <paramref name="from"/> in
    /// <paramref name="options"/>, read as <see cref="Folders.Move"/> reads a place: a place up or
    /// down its list, or into the list left or right of it at the same place, its end where that
    /// list is shorter. Null at a list's top or bottom, beside the outer lists, and for any other key.
    /// </summary>
    internal static FolderPlace? KeyPlace(FolderOptions options, FolderPlace from, int key)
    {
        var lists = options.Lists ?? [];
        if (from.List < 0 || from.List >= lists.Count)
        {
            return null;
        }
        var count = lists[from.List]?.Count ?? 0;
        switch (key)
        {
            case VK.VK_UP:
                return from.Index > 0 ? new FolderPlace(from.List, from.Index - 1) : null;
            case VK.VK_DOWN:
                return from.Index < count - 1 ? new FolderPlace(from.List, from.Index + 2) : null;
            case VK.VK_LEFT or VK.VK_RIGHT:
                var list = from.List + (key == VK.VK_LEFT ? -1 : 1);
                return list >= 0 && list < lists.Count ? new FolderPlace(list, Math.Min(from.Index, lists[list]?.Count ?? 0)) : null;
            default:
                return null;
        }
    }

    private static float Middle(D2D_RECT_F card) => (card.top + card.bottom) / 2;
}
