// edit: lines come into the arranged folder carried with no button held — a widget picked in its add
// list, a copy "Copy" made, lines Ctrl+V pasted (Dock.AddChosen, Dock.EditCopy.cs; the person,
// 2026-10-11: "when copying a widget it should go into edit mode, and then not just place it next but
// actually pin it to the mouse until i left click somewhere i want it"; 2026-10-19: a widget added in
// the folder the same).
//
// It is the folder's own carry of a pressed line (moving), begun without the press: the folder takes
// the mouse, its moves aim the lines as a drag's do (FolderFlyout.Pending.cs: one hit test, one
// layout), and they are never carried out of the folder — away from it they only show nowhere to land.
// They came in to be drawn and have no place in the folder until they land, so the folder is laid out
// with them in no list from before the commit that put them in (Expect) until they land. The press
// that puts them down and the one that calls them off are the dock's to hear (EditFollowHook),
// wherever they land; it asks the folder where they would go (LandCarry), or lets them go (EndCarry).

using Karpo.Core;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

public sealed unsafe partial class FolderFlyout
{
    /// <summary>edit: a line is carried with no button held (<see cref="Carry(IReadOnlyList{string}, FolderPlace?)"/>), until it is put down or called off.</summary>
    private bool carrying;

    /// <summary>Tests only: whether a line is carried with no button held.</summary>
    internal bool Carrying => carrying;

    /// <summary>
    /// edit: the line keyed <paramref name="key"/>, come into the folder, follows the pointer in the
    /// arranged folder, from where the pointer is now. False, with nothing changed, where the folder is
    /// not open, or holds no such line.
    /// </summary>
    internal bool Carry(string key) => Carry([key]);

    /// <summary>
    /// edit: as <see cref="Carry(string)"/>, the lines keyed <paramref name="keys"/> together — several
    /// copied or pasted at once (FolderFlyout.Pending.cs) — taken hold of by the first. They have no
    /// place in the folder until they land (§239): over no place they land at <paramref name="home"/>,
    /// where the folder adds (<see cref="AddPlace"/>), or, for none, stay where they went in.
    /// </summary>
    internal bool Carry(IReadOnlyList<string> keys, FolderPlace? home = null)
    {
        if (keys.Count == 0 || !IsOpen || plan is null)
        {
            return false;
        }
        Arrange();
        // The lines as the file has them now, in no list (Expect said so before the commit, or this does).
        arriving = [.. keys];
        this.home = home;
        FolderChanged();
        var index = IndexOfKey(keys[0]);
        if (!editing || index < 0)
        {
            arriving = [];
            this.home = null;
            FolderChanged();
            return false;
        }
        carrying = true;
        moving = index;
        Group(keys);
        pressed = NoTarget;
        dragging = NoTarget;
        landing = null;
        travelled = true;
        SetHot(NoTarget);
        _ = SetCapture(Hwnd);
        POINT at;
        _ = GetCursorPos(&at);
        // Lifted at once, taken hold of in its middle (FolderFlyout.Drag.cs).
        TakeHoldInMiddle(index);
        _ = host.ToPanel(at, out var over);
        Follow(over);
        _ = Aim(over);
        return true;
    }

    /// <summary>
    /// edit: the carried lines are put down by a press at <paramref name="at"/> (screen px): where they
    /// show they would land, else, over no place, at their home — <see cref="FolderFlyoutAction.Place"/>
    /// for the dock to move them there, as a drop is (Dock.PlaceInFolder) — or nothing, where that moves
    /// nothing and they stand where they went in. Either way they are carried no more.
    /// </summary>
    internal FolderFlyoutEvent LandCarry(POINT at)
    {
        if (!carrying || !IsOpen || plan is null || folder is not { } island || moving < 0 || moving >= tiles.Count)
        {
            EndCarry();
            return default;
        }
        _ = Aim(Panel(at));
        var place = landing ?? home;
        var moves = place is { } to && FolderDrag.Moves(island.Options, GroupCells(), to);
        var carried = moving;
        var together = PlacedKeys;
        var came = Arriving;
        EndCarry();
        if (!moves && came)
        {
            // Nothing moves, so no commit lays the folder out again: they stand where they went in.
            layoutSignature = "";
            FolderChanged();
        }
        return moves && carried < tiles.Count
            ? new FolderFlyoutEvent(FolderFlyoutAction.Place, tiles[carried].Island, carried, Place: place!.Value, Keys: together)
            : default;
    }

    /// <summary>
    /// edit: nothing is carried with no button held any more: the folder lets go of the mouse and shows
    /// its lines where they stand. Lines come in and called off are the dock's to take out again
    /// (DropFollow), which lays the folder out without them.
    /// </summary>
    internal void EndCarry()
    {
        if (!carrying)
        {
            return;
        }
        carrying = false;
        arriving = [];
        home = null;
        // It settles from where it shows, wherever it goes (FolderFlyout.Drag.cs).
        Drop();
        moving = NoTarget;
        landing = null;
        travelled = false;
        Group([]);
        if (GetCapture() == Hwnd)
        {
            _ = ReleaseCapture();
        }
        if (IsOpen)
        {
            ShowStates();
        }
    }

    /// <summary><paramref name="at"/> (screen px) in the panel's own px, inside it or not.</summary>
    private System.Numerics.Vector2 Panel(POINT at)
    {
        _ = host.ToPanel(at, out var point);
        return point;
    }
}
