// edit: what is pending in the arranged folder (docs\widgets.md §239): the lines carried — one or
// several, lifted by a press, or come in by "Add a widget", a paste or a copy and carried with no button
// — and where they would land. One model for every one of them:
//
//  - **Which.** A press on one of several selected lines of the folder (Picked and AlsoPicked)
//    carries all of them (the person, 2026-10-12: "dragging multiple widgets in folder edit mode
//    should work"); a paste, a copy or an add of several carries them all (Carry with their keys).
//    Selected widgets elsewhere — on the bar, in another folder — stay where they are.
//  - **New lines are nowhere.** Lines come in (Expect, Carry) have no place in the folder until they
//    land: the folder at rest is laid out with them in no list (FolderPending.AtRest), so its columns
//    stand where they stood before the commit put them somewhere to be drawn — an empty spacer column
//    stays one, and the columns with widgets centred on the button stay the ones there were (the
//    person, 2026-10-19: "when adding a widget in the folder edit mode somehow the alignment goes back
//    to as if there are hidden columns there").
//  - **How they go.** Lifted (FolderFlyout.Drag.cs), each on its plate, stacked one under the other in
//    their order down the folder, the one taken hold of under the pointer, the stack kept inside the
//    window as a lifted line is.
//  - **Where they land** is one hit test (Aim): a slot it is over, a list of its own there; over the
//    columns, a gap of the list nearest (Between); else nowhere. One line lifted from its place reads
//    its own list by its leading edge (FolderDrag.Gap); new lines, or several, are read in the folder
//    with them in no list, the stack's middle against the lines' middles, so none of the lines left
//    gives way first and nothing flickers. The others give way for all of them at once, the folder laid
//    out by the same function with the landing in it (FolderFlyout.Build), and one ghost stands round
//    the room they take.
//  - **Let go,** they land through one move (Folders.MoveGroup, the dock's PlaceInFolder): a drop, a
//    click putting a carry down, Ctrl+arrow. New lines let go over no place land where the folder's edit
//    bar adds (home); Escape or a right press puts lifted lines back (CancelMove, EditKeys.Escape) and
//    takes new ones out (the dock's DropFollow).
//  - They are never carried out of the folder onto the bar: away from it they show nowhere to land.

using System.Numerics;
using Karpo.Core;
using TerraFX.Interop.DirectX;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

public sealed unsafe partial class FolderFlyout
{
    /// <summary>edit: the keys of the lines carried together, the carried one among them, in their order down the folder; empty for one alone.</summary>
    private IReadOnlyList<string> groupKeys = [];

    /// <summary>
    /// edit: the keys of the lines come into the folder to be carried — added, pasted, copied
    /// (<see cref="Expect"/>, <see cref="Carry(IReadOnlyList{string}, FolderPlace?)"/>) — which have no
    /// place in it until they land; none while the lines carried were lifted from their places.
    /// </summary>
    private IReadOnlyList<string> arriving = [];

    /// <summary>edit: where lines come in land when they are let go over no place of the folder; null for where they went in.</summary>
    private FolderPlace? home;

    // edit: the folder laid out with several lines carried off in no list, where their landing is read
    // (Between); made the first time a drag aims, gone with the drag.
    private FolderPlan? closedPlan;

    /// <summary>Tests only: the keys of the lines carried together now, in their order; empty for one alone.</summary>
    internal IReadOnlyList<string> CarriedTogether => Grouped ? groupKeys : [];

    /// <summary>edit: several lines are carried together.</summary>
    private bool Grouped => groupKeys.Count > 1;

    /// <summary>edit: the lines carried came in (<see cref="arriving"/>): they have no place in the folder yet.</summary>
    private bool Arriving => arriving.Count > 0;

    /// <summary>
    /// edit: what is pending now (<see cref="FolderPending"/>): the lines carried — or come in and not
    /// lifted yet — landing at <paramref name="to"/>; null where none is.
    /// </summary>
    private FolderPending? Pending(FolderPlace? to)
    {
        var lines = moving >= 0 ? GroupIndices() : [.. arriving.Select(IndexOfKey).Where(index => index >= 0)];
        return lines.Count == 0 ? null : new FolderPending(lines, to, Arriving);
    }

    /// <summary>What of <see cref="arriving"/> the layout depends on, beside the folder's own signature.</summary>
    private string ArrivingSignature => Arriving ? $"|{string.Join(",", arriving)}" : "";

    /// <summary>Where the line keyed <paramref name="key"/> is among the folder's lines; <see cref="NoTarget"/> for none.</summary>
    private int IndexOfKey(string key)
    {
        for (var index = 0; index < tiles.Count; index++)
        {
            if (string.Equals(tiles[index].Island.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }
        return NoTarget;
    }

    /// <summary>
    /// edit: the lines keyed <paramref name="keys"/> are about to come into the folder, to be carried
    /// (<see cref="Carry(IReadOnlyList{string}, FolderPlace?)"/>): the layout the commit that puts them
    /// in brings leaves them in no list, so nothing of the folder moves for them (§239). Said before
    /// the commit, as the dock's <c>CarryInto</c> does.
    /// </summary>
    internal void Expect(IReadOnlyList<string> keys)
    {
        arriving = [.. keys];
        home = null;
    }

    /// <summary>
    /// edit: the keys of the folder's selected lines (<see cref="Picked"/>, <see cref="AlsoPicked"/>)
    /// where line <paramref name="index"/> is one of several of them, in their order down the folder;
    /// none where it is alone or not selected. A line the folder has no room to show (FolderListPlan.Hidden)
    /// is never carried with them.
    /// </summary>
    private IReadOnlyList<string> SelectedWith(int index)
    {
        if (index < 0 || index >= tiles.Count)
        {
            return [];
        }
        var selected = new HashSet<string>(alsoPicked, StringComparer.OrdinalIgnoreCase);
        if (picked is not null)
        {
            selected.Add(picked);
        }
        if (!selected.Contains(tiles[index].Island.Key))
        {
            return [];
        }
        var keys = Ordered(tiles.Where((_, each) => plan is not { } shown || shown.Holds(each)).Select(tile => tile.Island.Key).Where(selected.Contains));
        return keys.Count > 1 ? keys : [];
    }

    /// <summary>edit: <paramref name="keys"/> of the folder's lines in their order down it — list by list, each from the top; keys of none of its lines left out.</summary>
    private List<string> Ordered(IEnumerable<string> keys)
    {
        if (folder is not { } island)
        {
            return [];
        }
        var placed = new List<(string Key, FolderPlace At)>();
        foreach (var key in keys)
        {
            if (tiles.FirstOrDefault(tile => string.Equals(tile.Island.Key, key, StringComparison.OrdinalIgnoreCase)) is { } tile
                && Folders.PlaceOf(island.Options, tile.Cell) is { } at && !placed.Exists(each => string.Equals(each.Key, key, StringComparison.OrdinalIgnoreCase)))
            {
                placed.Add((tile.Island.Key, at));
            }
        }
        return [.. placed.OrderBy(each => each.At.List).ThenBy(each => each.At.Index).Select(each => each.Key)];
    }

    /// <summary>edit: the lines carried together from now on: <paramref name="keys"/> in their order down the folder, or none (one alone).</summary>
    private void Group(IReadOnlyList<string> keys)
    {
        groupKeys = keys.Count > 1 ? Ordered(keys) : [];
        if (groupKeys.Count < 2)
        {
            groupKeys = [];
        }
        closedPlan = null;
    }

    /// <summary>edit: the indices of the lines carried together, in their order; the carried one alone where it is alone.</summary>
    private List<int> GroupIndices()
    {
        if (!Grouped)
        {
            return moving >= 0 ? [moving] : [];
        }
        return [.. groupKeys.Select(IndexOfKey).Where(index => index >= 0)];
    }

    /// <summary>edit: the cells of the lines carried together, in their order.</summary>
    private List<FolderCell> GroupCells() => [.. GroupIndices().Select(index => tiles[index].Cell)];

    /// <summary>The keys the dock moves for the carried line: the lines carried with it, or null for it alone.</summary>
    private IReadOnlyList<string>? PlacedKeys => Grouped ? [.. groupKeys] : null;

    /// <summary>
    /// edit: how far down from the carried line's top each line carried stands, by index, the stack one
    /// line under the other a lines' gap apart (<see cref="FolderDrag.Stack"/>); and the stack's top
    /// and bottom from the carried line's top — the line's own for one alone.
    /// </summary>
    private (Dictionary<int, float> Downs, float Top, float Bottom) StackOf(FolderPlan shown)
    {
        var members = GroupIndices().Where(index => index < shown.Cards.Count).ToList();
        var heights = members.Select(index => shown.Cards[index].bottom - shown.Cards[index].top).ToList();
        var downs = FolderDrag.Stack(heights, PopupPaint.Round(2 * scale));
        var mine = members.IndexOf(moving) is var at and >= 0 ? downs[at] : 0;
        var result = new Dictionary<int, float>();
        for (var each = 0; each < members.Count; each++)
        {
            result[members[each]] = downs[each] - mine;
        }
        var total = members.Count == 0 ? 0 : downs[^1] + heights[^1];
        return (result, -mine, total - mine);
    }

    /// <summary>edit: the lines carried with the carried one lifted onto plates of their own, where the stack has them, its top left corner at <paramref name="at"/>.</summary>
    private void RaiseGroup(FolderPlan shown, Vector2 at)
    {
        if (!Grouped)
        {
            return;
        }
        var (downs, _, _) = StackOf(shown);
        foreach (var (index, down) in downs)
        {
            if (index != moving)
            {
                _ = Raise(index, at + new Vector2(0, down), plate: true);
            }
        }
    }

    /// <summary>edit: the lines carried with the carried one, standing where the stack has them, its corner at <paramref name="at"/>.</summary>
    private void FollowGroup(FolderPlan shown, Vector2 at, float opacity)
    {
        if (!Grouped)
        {
            return;
        }
        var (downs, _, _) = StackOf(shown);
        foreach (var (index, down) in downs)
        {
            if (index != moving && RaisedOf(index) is { } lifted)
            {
                lifted.At = at + new Vector2(0, down);
                lifted.Carrier.StopAnimation("Offset");
                lifted.Carrier.Offset = new Vector3(lifted.At, 0);
                lifted.Carrier.Opacity = opacity;
            }
        }
    }

    /// <summary>
    /// edit: the one hit test of a carry (§239): where the lines carried would land with the pointer at
    /// <paramref name="point"/> (panel px) — in the empty slot beside the lists it is over, half the
    /// lists' gap either side of it its too, a list of its own there; over the lists' columns — their
    /// panels, the gaps between them, the room over and under them — a gap of the list it is over or
    /// nearest (<see cref="Between"/>); anywhere else nowhere. The landing is shown where letting go
    /// there moves something (<see cref="Lands"/>); the lines give way for it either way
    /// (<see cref="ShowArranged"/>), gliding there where <paramref name="animate"/>. A drag, a carry with
    /// no button, a drag handed over from rest and a layout under a carry all aim here. True where the
    /// pointer is over the folder's columns or a slot.
    /// </summary>
    private bool Aim(Vector2 point, bool animate = true)
    {
        if (plan is not { } shown || moving < 0 || moving >= tiles.Count || moving >= shown.Cards.Count || folder is null)
        {
            return false;
        }
        var slot = SlotAt(shown, point);
        var over = slot is null && OverColumns(shown, point);
        // Over a slot the lines are in no list: they close up behind the carried ones (§230).
        aimed = over ? Between(shown, point) : null;
        landing = slot is { } into ? Lands(into.Place) : aimed is { } place ? Lands(place) : null;
        ShowArranged(animate);
        ShowStates();
        return slot is not null || over;
    }

    /// <summary>
    /// edit: the gap of the list nearest <paramref name="point"/> (panel px) the carried lines would land
    /// in, read as <see cref="Folders.MoveGroup"/> reads a place (<see cref="FolderDrag.Gap"/>). One line
    /// lifted from its place is read in its own list by its leading edge, against the lines where they
    /// stand; new lines, or several, in the folder with them in no list (<see cref="closedPlan"/>; the
    /// folder at rest is that already for new ones), the stack's middle against the lines' middles.
    /// </summary>
    private FolderPlace Between(FolderPlan shown, Vector2 point)
    {
        var island = folder!;
        var lines = GroupIndices();
        var lone = lines.Count == 1 && !Arriving;
        var frame = lone || Arriving ? shown : closedPlan ??= Layout(island, Pending(null), dragNatural);
        var list = shown.NearestList(point.X);
        var (_, top, bottom) = StackOf(shown);
        var corner = Grabbed(shown, point).Y - FolderDrag.Down(shown, frame, shownEdge);
        // A line the list has no room to show now is no place to land by (FolderListPlan.Hidden), even
        // where the folder with the carried ones in none would show it: under the last one shown is before them.
        var places = tiles.Select((tile, index) => (!lone && lines.Contains(index)) || !shown.Holds(index) ? null : Folders.PlaceOf(island.Options, tile.Cell)).ToList();
        return new FolderPlace(list, FolderDrag.Gap(frame, places, lone ? moving : NoTarget, list, corner + top, corner + bottom));
    }

    /// <summary>
    /// edit: where to show the carried lines landing for <paramref name="want"/>: there where letting go
    /// there changes the folder (<see cref="FolderDrag.Moves"/>) — always for lines come in, which have
    /// no place yet — or null where it would leave it as it is, just above or below where they stand.
    /// Until it would move, letting go is still a click.
    /// </summary>
    private FolderPlace? Lands(FolderPlace want) =>
        Arriving || folder is { } island && FolderDrag.Moves(island.Options, GroupCells(), want) ? want : null;

    /// <summary>
    /// edit: the room the lines carried take in <paramref name="laid"/>, where their ghost stands: from
    /// the first one's top to the last one's bottom, as wide as the first; the carried one's own for it
    /// alone. Each its card: a divider's its empty room (§505), with nothing in it.
    /// </summary>
    private (int First, D2D_RECT_F Box) GroupRoom(FolderPlan laid)
    {
        var members = GroupIndices().Where(index => index < laid.Cards.Count).ToList();
        if (members.Count == 0)
        {
            return (moving, moving >= 0 && moving < laid.Cards.Count ? laid.Cards[moving] : default);
        }
        var first = laid.Cards[members[0]];
        var bottom = members.Max(index => laid.Cards[index].bottom);
        return (members[0], new D2D_RECT_F(first.left, first.top, first.right, bottom));
    }
}
