// edit, §551 (the person, 2026-11-07: "taskbar menu should show a preview and it should also be DnD and not arrows, remove the
// arrows."): every list of the settings panel put in order — the taskbar menu's entries, the layouts, a folder's faces, a
// toggles line's toggles: whatever the menus drag by a grip (FlyoutItem.Reorder, PanelRow.Drags) — is put in order by
// dragging its rows, as WinUI's ListView reorders (CanReorderItems): a press on a row (its handle, its name, an expander's
// head) and a move past the system's drag distance lifts it; it follows the pointer within its list, the others making room
// where it would land; let go, it is moved there as one change, one step of the undo (the menu row's own move:
// OptionMenu.Moved, Dock.PanelMoved). Escape, a right press or the pointer lost put it back. Near the page's top or foot the
// page scrolls under it. From the keys, Ctrl+Up and Ctrl+Down on any control of the row move it a place (Karpo's menus' own
// keys for a list put in order, dock.edit.faceDrag; Alt+Up and Alt+Down open a dropdown, §525).
//
// What it lands on is worked out here, apart from any window: pure, so the tests say it.

using Karpo.Core;

namespace Karpo.Dock;

/// <summary>
/// §551: a row being dragged into order: its key and the id of its menu row (what the move is sent as), the keys of the rows
/// of its list and where each stands with what is shown under it (page px, in order), which of them it is, and how far
/// down its own the press took it (<paramref name="Grab"/>, px); where the pointer is now (<see cref="Y"/>, page px).
/// </summary>
internal sealed record PanelDrag(string Key, uint Id, IReadOnlyList<string> Keys, IReadOnlyList<RectF> Spans, int From, float Grab)
{
    /// <summary>The pointer, page px.</summary>
    public float Y { get; init; }

    /// <summary>The place among its own it would land at now.</summary>
    public int To => PanelReorder.DropAt(Spans, From, Y - Grab + (Spans[From].Height / 2));

    /// <summary>How far the row lifted is drawn from where it stood (px): following the pointer.</summary>
    public float Lift => Y - Grab - Spans[From].Y;

    /// <summary>How far row <paramref name="at"/> of the list is drawn from where it stands, making room (px).</summary>
    public float Shift(int at) => PanelReorder.Shift(Spans, From, To, at);

    /// <summary>Which of the list's rows the page px <paramref name="y"/> stands in (with what is shown under it); -1 for none.</summary>
    public int Of(float y)
    {
        for (var at = 0; at < Spans.Count; at++)
        {
            if (y >= Spans[at].Y && y < Spans[at].Bottom)
            {
                return at;
            }
        }
        return -1;
    }
}

/// <summary>§551: where a row dragged into order lands, and how the others make room for it (see the top of the file). Pure.</summary>
internal static class PanelReorder
{
    /// <summary>
    /// The place among <paramref name="spans"/> (each row of a list with what is shown under it, page px, in order) that the
    /// row <paramref name="from"/> lands at, its middle at <paramref name="middle"/>: after every other whose middle it is past.
    /// </summary>
    public static int DropAt(IReadOnlyList<RectF> spans, int from, float middle)
    {
        var to = 0;
        for (var at = 0; at < spans.Count; at++)
        {
            if (at != from && middle > spans[at].Y + (spans[at].Height / 2))
            {
                to++;
            }
        }
        return to;
    }

    /// <summary>
    /// How far row <paramref name="at"/> moves to make room for row <paramref name="from"/> landing at <paramref name="to"/>
    /// (px): those it passes over move up or down by its height and the gap after it; the rest, and it, not at all.
    /// </summary>
    public static float Shift(IReadOnlyList<RectF> spans, int from, int to, int at)
    {
        if (at == from || from < 0 || from >= spans.Count)
        {
            return 0;
        }
        var gap = from + 1 < spans.Count ? spans[from + 1].Y - spans[from].Bottom : from > 0 ? spans[from].Y - spans[from - 1].Bottom : 0;
        var room = spans[from].Height + MathF.Max(0, gap);
        return from < to && at > from && at <= to ? -room
            : to < from && at >= to && at < from ? room
            : 0;
    }

    /// <summary>
    /// The place a row at <paramref name="from"/> of <paramref name="count"/> goes to for the key <paramref name="key"/> with
    /// Ctrl held (Ctrl+Up one earlier, Ctrl+Down one later); null for another key, or where it can go no further.
    /// </summary>
    public static int? Stepped(int key, bool control, int from, int count)
    {
        if (!control || key is not (TerraFX.Interop.Windows.VK.VK_UP or TerraFX.Interop.Windows.VK.VK_DOWN))
        {
            return null;
        }
        var to = from + (key == TerraFX.Interop.Windows.VK.VK_DOWN ? 1 : -1);
        return to >= 0 && to < count && from >= 0 ? to : null;
    }

    /// <summary>What a screen reader says of where it stands: "3 of 7".</summary>
    public static string Position(int at, int count) => S.DockPanelPosition(at: at + 1, count: count);
}
