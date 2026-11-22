// menus: rows of a menu put in order by dragging them among each other (the person, 2026-10-19:
// "reordering faces should be inlined on the first level and not on a sublevel"; §220), as the
// standing rule has every list that can be reordered: by its drag handle, never by arrows.
//
// The rows of one FlyoutItem.Reorder group — a folder's faces — are dragged among themselves: a press on
// one and a move past the system's drag distance picks it up; it follows the pointer up and down within
// the group, and the others slide aside to show where it would land, as the tray's overflow opens a gap
// (Flyout.DropGap, GapPlaces). Let go, the menu says where (FlyoutEventKind.Reorder) and stays open;
// whoever showed it moves the thing and shows the menu again (Refresh). Escape, a right press or the
// capture lost put the row back. A press and a release without the move is a click, as ever: the row's
// submenu.
//
// From the keys, Ctrl+Up and Ctrl+Down on the lit row move it one place, as edit mode's Ctrl+arrows
// move a widget along the bar. A row's grip (FlyoutTool.Grip) is the handle drawn, at the row's start
// before its icon (§226); the whole row takes the press.

using System.Numerics;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

public sealed unsafe partial class Flyout
{
    // The row pressed that may be dragged into order, and where (client px); -1 for none.
    private int reorderRow = -1;
    private POINT reorderFrom;

    // Whether it is being dragged, and the place among its own it would land at now (DropAt's answer).
    private bool reordering;
    private int reorderTarget = -1;

    /// <summary>The rows of <paramref name="group"/>, in their order.</summary>
    private List<int> ReorderGroup(int group)
    {
        var members = new List<int>();
        for (var index = 0; index < rows.Count; index++)
        {
            if (rows[index].Item.Reorder == group)
            {
                members.Add(index);
            }
        }
        return members;
    }

    /// <summary>
    /// The left button went down on row <paramref name="index"/> at <paramref name="x"/>, <paramref name="y"/>:
    /// it may be dragged into order where it is of a group of two or more, and the press is not on a
    /// button of its own.
    /// </summary>
    private void ReorderPress(int index, int x, int y)
    {
        ReorderForget();
        if (parent is not null || index < 0 || index >= rows.Count || rows[index].Item.Reorder <= 0
            || ReorderGroup(rows[index].Item.Reorder).Count < 2
            || ToolAt(index, x, y) is { Grip: false })
        {
            return;
        }
        reorderRow = index;
        reorderFrom = new POINT(x, y);
    }

    /// <summary>
    /// The pointer moved to <paramref name="x"/>, <paramref name="y"/> with a row pressed: past the drag
    /// distance it is picked up, and then follows the pointer. True while it is dragged, the move its.
    /// </summary>
    private bool ReorderMove(int x, int y)
    {
        if (reorderRow < 0 || reorderRow >= rows.Count)
        {
            return false;
        }
        if (!reordering)
        {
            if (Math.Abs(y - reorderFrom.y) <= GetSystemMetrics(SM.SM_CYDRAG) && Math.Abs(x - reorderFrom.x) <= GetSystemMetrics(SM.SM_CXDRAG))
            {
                return false;
            }
            reordering = true;
            CloseSubmenu();
            HideTip();
            SetHotTool(-1, -1);
            SetHot(reorderRow);
        }
        SetCursor(LoadCursorW(HINSTANCE.NULL, IDC.IDC_SIZENS));
        ShowReorder(y);
        return true;
    }

    /// <summary>
    /// The dragged row's place among its own for the pointer at <paramref name="y"/> (client px): it
    /// follows the pointer within the group's rows, and the others make room where it would land.
    /// </summary>
    private void ShowReorder(int y)
    {
        var group = ReorderGroup(rows[reorderRow].Item.Reorder);
        var held = group.IndexOf(reorderRow);
        var fills = group.Select(index => (rows[index].Bounds.X, rows[index].Bounds.Y, rows[index].Bounds.W, rows[index].Bounds.H)).ToList();
        var bounds = rows[reorderRow].Bounds;
        var dy = Math.Clamp(y - reorderFrom.y, fills[0].Y - bounds.Y, fills[^1].Y - bounds.Y);
        // Where it would land: by the middle of the row as it is carried, not the point it was taken by.
        var middle = bounds.Y + dy + (bounds.H / 2);
        reorderTarget = DropAt(fills, bounds.X + (bounds.W / 2), middle, grid: false, held, reorderTarget);
        var places = GapPlaces(fills, reorderTarget, held, grid: false);
        EnsureGapSlide();
        for (var at = 0; at < group.Count; at++)
        {
            var row = rows[group[at]];
            // §356: drawn again rather than kept, where it shows again.
            row.Displaced = true;
            var carried = at == held;
            var move = carried ? new Vector3(0, dy, 0) : new Vector3(0, places[at].Y - row.Bounds.Y, 0);
            foreach (var (visual, home) in row.Parts)
            {
                // The carried row with the pointer at once; the others slide.
                visual.ImplicitAnimations = carried ? null : gapSlide;
                Try(() => visual.Offset = home + move);
            }
        }
    }

    /// <summary>The slide the rows make aside, the tray's (DropGap); made the first time, where the look animates.</summary>
    private void EnsureGapSlide()
    {
        if (!look.Animations || gapSlide is not null)
        {
            return;
        }
        var c = host.Compositor;
        var slide = Own(c.CreateVector3KeyFrameAnimation());
        slide.Target = "Offset";
        slide.InsertExpressionKeyFrame(1f, "this.FinalValue", Own(MotionEase.Of(c, MotionKind.Move)));
        slide.Duration = look.Motion.Time(MotionKind.Move);
        gapSlide = Own(c.CreateImplicitAnimationCollection());
        gapSlide["Offset"] = slide;
    }

    /// <summary>
    /// The left button came up: a row dragged is let go — the menu says where it goes among its own
    /// where that is somewhere else, its rows back in place until it is shown again — and the button's
    /// coming up is the drag's (an empty event where it lands where it was). Null where nothing was
    /// dragged: the release is a click's.
    /// </summary>
    private FlyoutEvent? ReorderRelease()
    {
        if (!reordering || reorderRow < 0 || reorderRow >= rows.Count)
        {
            ReorderForget();
            return null;
        }
        var group = ReorderGroup(rows[reorderRow].Item.Reorder);
        var held = group.IndexOf(reorderRow);
        var to = ReorderPlace(held, reorderTarget);
        var id = rows[reorderRow].Item.Id;
        ReorderCancel();
        return to != held && to >= 0 ? new FlyoutEvent(FlyoutEventKind.Reorder, id, To: to) : new FlyoutEvent(FlyoutEventKind.Nothing);
    }

    /// <summary>
    /// Where a row at <paramref name="held"/> among its own lands for a drop before
    /// <paramref name="target"/> (DropAt's answer, its own place counted): one less past itself.
    /// </summary>
    internal static int ReorderPlace(int held, int target) => target < 0 ? held : target > held ? target - 1 : target;

    /// <summary>A row dragged goes back where it was, the others with it; nothing is dragged any more.</summary>
    private void ReorderCancel()
    {
        if (reordering && reorderRow >= 0 && reorderRow < rows.Count)
        {
            foreach (var index in ReorderGroup(rows[reorderRow].Item.Reorder))
            {
                foreach (var (visual, home) in rows[index].Parts)
                {
                    visual.ImplicitAnimations = null;
                    Try(() => visual.Offset = home);
                }
            }
        }
        ReorderForget();
    }

    /// <summary>Nothing pressed to be dragged into order, nor dragged.</summary>
    private void ReorderForget()
    {
        reorderRow = -1;
        reordering = false;
        reorderTarget = -1;
    }

    /// <summary>
    /// Ctrl+Up or Ctrl+Down on the lit row of a group of two or more: one place earlier or later among
    /// its own, where it is not at that end already. Null for any other key or row.
    /// </summary>
    private FlyoutEvent? ReorderKey(int key)
    {
        if (parent is not null || key is not (VK.VK_UP or VK.VK_DOWN) || !KeyState.Down(VK.VK_CONTROL)
            || hot < 0 || hot >= rows.Count || rows[hot].Item.Reorder <= 0)
        {
            return null;
        }
        var group = ReorderGroup(rows[hot].Item.Reorder);
        var at = group.IndexOf(hot);
        var to = at + (key == VK.VK_UP ? -1 : 1);
        return to >= 0 && to < group.Count && group.Count > 1
            ? new FlyoutEvent(FlyoutEventKind.Reorder, rows[hot].Item.Id, To: to)
            : new FlyoutEvent(FlyoutEventKind.Nothing);
    }

    /// <summary>Tests only: the middle of row <paramref name="id"/>, in window px; null where there is none.</summary>
    internal POINT? RowMiddle(uint id)
    {
        if (!IsOpen || rows.FirstOrDefault(row => row.Item.Id == id) is not { } found)
        {
            return null;
        }
        return new POINT((int)MathF.Round(viewport.X + found.Bounds.X + (found.Bounds.W / 2)), (int)MathF.Round(viewport.Y + found.Bounds.Y + (found.Bounds.H / 2) - scroll));
    }

    /// <summary>Tests only: the middle of row <paramref name="id"/>'s tool <paramref name="tool"/>, in window px; null where there is none.</summary>
    internal POINT? ToolMiddle(uint id, int tool)
    {
        if (!IsOpen || rows.FirstOrDefault(row => row.Item.Id == id) is not { } found || tool < 0 || tool >= found.Tools.Count)
        {
            return null;
        }
        var box = found.Tools[tool].Box;
        return new POINT((int)MathF.Round(viewport.X + box.X + (box.W / 2)), (int)MathF.Round(viewport.Y + box.Y + (box.H / 2) - scroll));
    }
}
