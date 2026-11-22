// Adding a widget into a folder while it is being arranged: no column has a "+" of its own any more
// (the person, 2026-10-19; docs\widgets.md §214) — the folder's own edit bar adds, into the selected
// line's column, the column picked, else the first with room (FolderFlyout.AddPlaceIn). The columns'
// width buttons' geometry is ListFolderTests'; the empty slots beside the lists FolderSlotTests'; the
// dock's own add list AddListTests'.

using Karpo.Dock;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Folders;

public sealed class FolderAddTests
{
    private static readonly Look Light = new(Glass: false, Dark: false, Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4));

    private static FolderPlan Plan(FolderOptions options, bool editing) =>
        FolderFlyout.Build(Light, 1f, FolderView.List, options, options.Cells, 1040, editing: editing);

    /// <summary>Outside edit mode a folder offers nothing: nothing on a folder adds by itself.</summary>
    [Fact]
    public void NothingOffersAnAddWhenTheFolderIsNotBeingArranged()
    {
        var options = new FolderOptions();
        options.Put(BarKind.Pin.New().AsCell());
        options.Put(BarKind.Pin.New().AsCell(), list: 1);

        Assert.Empty(Plan(options, editing: false).Adds);
    }

    /// <summary>
    /// Arranged, every column has its "+" and its width (§246) — no remove (2026-10-12: a column
    /// stays) — none for a new list (the slots come with the folder's place on the screen), and it
    /// takes no room for any of them.
    /// </summary>
    [Fact]
    public void EveryColumnHasItsPlusAndItsWidth()
    {
        var options = new FolderOptions();
        options.Put(BarKind.Pin.New().AsCell());
        options.Put(BarKind.Pin.New().AsCell());
        options.Put(BarKind.Pin.New().AsCell());
        options.Put(BarKind.Pin.New().AsCell(), list: 1);

        var plan = Plan(options, editing: true);
        var resting = Plan(options, editing: false);

        Assert.Equal([FolderAddKind.Add, FolderAddKind.Width, FolderAddKind.Add, FolderAddKind.Width], plan.Adds.Select(add => add.Kind));
        Assert.Equal([0, 0, 1, 1], plan.Adds.Select(add => add.List));
        Assert.Equal((resting.Width, resting.Height), (plan.Width, plan.Height));
        Assert.Equal([FolderAddKind.Add, FolderAddKind.Width], Plan(new FolderOptions(), editing: true).Adds.Select(add => add.Kind));
    }

    /// <summary>
    /// Where the folder's edit bar adds (§214), and a column's "+" (§246): into the chosen column — the
    /// selected line's, or the "+"'s — else the first that shows everything it has, else the first; at
    /// the list's end away from the bar: its top over a bottom or side bar, its end under a top bar.
    /// </summary>
    [Fact]
    public void TheEditBarAddsIntoTheChosenColumnElseTheFirstWithRoom()
    {
        int[] counts = [4, 0, 2];
        bool[] none = [false, false, false];
        Assert.Equal(new FolderPlace(2, 0), FolderFlyout.AddPlaceIn(counts, none, chosen: 2, fromTop: false));
        Assert.Equal(new FolderPlace(2, 2), FolderFlyout.AddPlaceIn(counts, none, chosen: 2, fromTop: true));
        // An empty column picked: the widget is its first.
        Assert.Equal(new FolderPlace(1, 0), FolderFlyout.AddPlaceIn(counts, none, chosen: 1, fromTop: true));
        // None chosen: the first.
        Assert.Equal(new FolderPlace(0, 4), FolderFlyout.AddPlaceIn(counts, none, chosen: null, fromTop: true));
        // The first with room: a list that hides lines has none.
        Assert.Equal(new FolderPlace(1, 0), FolderFlyout.AddPlaceIn(counts, [true, false, false], chosen: null, fromTop: true));
        // A chosen column goes before room: the person asked for it.
        Assert.Equal(new FolderPlace(0, 0), FolderFlyout.AddPlaceIn(counts, [true, false, false], chosen: 0, fromTop: false));
        // None with room: the first. A column the folder does not have: as none chosen.
        Assert.Equal(new FolderPlace(0, 0), FolderFlyout.AddPlaceIn(counts, [true, true, true], chosen: null, fromTop: false));
        Assert.Equal(new FolderPlace(1, 0), FolderFlyout.AddPlaceIn(counts, [true, false, false], chosen: 7, fromTop: false));
        // A folder of one empty list.
        Assert.Equal(new FolderPlace(0, 0), FolderFlyout.AddPlaceIn([0], [false], chosen: null, fromTop: true));
    }
}
