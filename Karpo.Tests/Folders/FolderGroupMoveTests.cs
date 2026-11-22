// edit: several lines of an arranged folder moved together, and copied together (the person,
// 2026-10-12: "dragging multiple widgets in folder edit mode should work" ... "same for copy pasting,
// copy pasting should also work into columns that are empty"). Pure: Folders.MoveGroup, the places a
// group drag reads (FolderDrag.ClosedGroup, Unclosed, GroupGhost, Stack), the dock's move of them
// (Dock.PlaceInFolderIn) and what a copy of one of several copies (Dock.CopiedWith, CopiedParts).

using Karpo.Core;
using Karpo.Dock;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Folders;

public sealed class FolderGroupMoveTests
{
    /// <summary>A folder of lists of lines, each named by its id: "a b | c"; " | a" has an empty first list.</summary>
    private static FolderOptions Lists(string shape)
    {
        var folder = new FolderOptions { Lists = [] };
        foreach (var list in shape.Split('|'))
        {
            folder.Lists.Add([.. list.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(id => new FolderCell { Island = Line(id) })]);
        }
        return folder;
    }

    private static CellItem Line(string id)
    {
        var island = ListKind.Battery.New();
        island.Id = id;
        return island;
    }

    private static string Shown(FolderOptions folder) =>
        string.Join(" | ", folder.Lists.Select(list => string.Join(" ", list.Select(cell => cell.Island.Id))));

    private static List<FolderCell> Of(FolderOptions folder, params string[] ids) => [.. ids.Select(id => Karpo.Core.Folders.CellOf(folder, id)!)];

    /// <summary>
    /// Lines carried together land one under the other, in the order given, above the first line at
    /// the place that is none of them — within their list, into another, into an empty column, at a
    /// list's end — and a list they all leave stays, an empty column, between two with widgets;
    /// outside them it goes (§328).
    /// </summary>
    [Fact]
    public void LinesCarriedTogetherLandInTheirOrderWhereverTheyGo()
    {
        var folder = Lists("a b c d | e");
        // Down their own list: above d, which is at 3 as the folder stands.
        Assert.True(Karpo.Core.Folders.MoveGroup(folder, Of(folder, "a", "b"), new FolderPlace(0, 3)));
        Assert.Equal("c a b d | e", Shown(folder));
        // Into another list, at its top.
        Assert.True(Karpo.Core.Folders.MoveGroup(folder, Of(folder, "a", "b"), new FolderPlace(1, 0)));
        Assert.Equal("c d | a b e", Shown(folder));
        // From two lists at once to a list's end: in the order given, not the order they stood in.
        Assert.True(Karpo.Core.Folders.MoveGroup(folder, Of(folder, "e", "c"), new FolderPlace(1, 3)));
        Assert.Equal("d | a b e c", Shown(folder));

        // Into an empty column, which takes them as any list does.
        var empty = Lists("a b |  | c");
        Assert.True(Karpo.Core.Folders.MoveGroup(empty, Of(empty, "a", "c"), new FolderPlace(1, 0)));
        Assert.Equal("b | a c", Shown(empty));
        // A place on one of their own: above the next line that is none of them.
        var own = Lists("a b c d");
        Assert.True(Karpo.Core.Folders.MoveGroup(own, Of(own, "a", "c"), new FolderPlace(0, 2)));
        Assert.Equal("b a c d", Shown(own));
    }

    /// <summary>
    /// As a new list — a slot beside the lists — they are a list of their own there, the lists they
    /// left staying — lines that are their list already, asked to be a list beside it, too: it stays
    /// behind, empty. Where they would stand exactly as they stand, nothing changes and the move says so.
    /// </summary>
    [Fact]
    public void LinesCarriedTogetherMakeANewListAndStayWhereNothingWouldChange()
    {
        var folder = Lists("a b | c");
        Assert.True(Karpo.Core.Folders.MoveGroup(folder, Of(folder, "a", "c"), FolderPlace.Slot(2, 1)));
        Assert.Equal("b |  |  | a c", Shown(folder));

        var beside = Lists("a b | c");
        Assert.True(Karpo.Core.Folders.MoveGroup(beside, Of(beside, "a", "b"), FolderPlace.Slot(0, 0)));
        Assert.Equal("a b |  | c", Shown(beside));

        var alone = Lists("a b | c");
        // Just where they stand, read above them, between them, or under them.
        Assert.False(Karpo.Core.Folders.MoveGroup(alone, Of(alone, "a", "b"), new FolderPlace(0, 0)));
        Assert.False(Karpo.Core.Folders.MoveGroup(alone, Of(alone, "a", "b"), new FolderPlace(0, 1)));
        Assert.False(Karpo.Core.Folders.MoveGroup(alone, Of(alone, "a", "b"), new FolderPlace(0, 2)));
        Assert.Equal("a b | c", Shown(alone));
        // Lines of none of the folder's move nothing.
        Assert.False(Karpo.Core.Folders.MoveGroup(alone, [new FolderCell { Island = Line("x") }], new FolderPlace(1, 0)));
    }

    /// <summary>
    /// The drag reads its landing among the lines left, its places the folder's own (§239): the gap
    /// above a line that is none of the group, the list's end where none is — so the place read with
    /// the group in no list is the place the move takes, with no closing up and back. The ghost shows
    /// only where letting go would change something; the move is planned on a copy.
    /// </summary>
    [Fact]
    public void AGroupDragReadsItsPlaceAmongTheLinesLeft()
    {
        var folder = Lists("a b c d | e");
        var group = Of(folder, "b", "e");

        // Above c.
        Assert.Equal("a b e c d | ", Shown(FolderDrag.Moved(folder, group, new FolderPlace(0, 2))));
        // Above b, which is carried: above the next line left, c.
        Assert.Equal("a b e c d | ", Shown(FolderDrag.Moved(folder, group, new FolderPlace(0, 1))));
        // The emptied list's top: past its own carried line, its end.
        Assert.Equal("a c d | b e", Shown(FolderDrag.Moved(folder, group, new FolderPlace(1, 0))));
        Assert.Equal("a c d |  |  | b e", Shown(FolderDrag.Moved(folder, group, FolderPlace.Slot(2, 1))));

        var moved = FolderDrag.Moved(folder, group, new FolderPlace(0, 4));
        Assert.Equal("a c d b e | ", Shown(moved));
        Assert.Equal("a b c d | e", Shown(folder));
        Assert.True(FolderDrag.Moves(folder, group, new FolderPlace(0, 4)));
        var together = Lists("a b c");
        Assert.False(FolderDrag.Moves(together, Of(together, "a", "b"), new FolderPlace(0, 2)));
    }

    /// <summary>The stack the carried lines stand in: one under the other, a gap apart.</summary>
    [Fact]
    public void CarriedLinesStackOneUnderTheOther()
    {
        Assert.Equal([0f, 42f, 64f], FolderDrag.Stack([40, 20, 40], 2));
        Assert.Empty(FolderDrag.Stack([], 2));
    }

    /// <summary>
    /// The dock moves lines carried together as one (Dock.PlaceInFolderIn): those of the first
    /// one's folder, in the order given; one alone as a single line is moved.
    /// </summary>
    [Fact]
    public void TheDockMovesTheLinesCarriedTogether()
    {
        var holder = BarKind.FolderList.New();
        holder.Id = "f";
        holder.Folder = Lists("a b c |  ");
        var config = new Config();
        config.Layouts.Clear();
        config.Layouts.Add(new TaskbarLayout { Id = "only", Name = "Default", Islands = [holder] });
        var owner = Config.LayoutOwner("only");
        string Key(string id) => Config.IslandKey(owner, Karpo.Core.Folders.Path("f", id));

        Assert.True(TheDock.PlaceInFolderIn(config, [Key("c"), Key("a"), Config.IslandKey(owner, "elsewhere")], new FolderPlace(1, 0)));
        Assert.Equal("b | c a", Shown(holder.Folder));
        Assert.True(TheDock.PlaceInFolderIn(config, [Key("b")], new FolderPlace(1, 1)));
        Assert.Equal("c b a", Shown(holder.Folder));
        Assert.False(TheDock.PlaceInFolderIn(config, [Key("gone")], new FolderPlace(0, 0)));
    }

    /// <summary>
    /// "Copy" on one of several selected copies those beside it: the same folder's lines, or the same
    /// bar's widgets — never a mix; it alone where it is not among them.
    /// </summary>
    [Fact]
    public void ACopyOfOneOfSeveralCopiesThoseBesideIt()
    {
        const string owner = "layout:default";
        var selected = new[] { $"{owner}/f/a", $"{owner}/f/b", $"{owner}/g/c", $"{owner}/clock", $"{owner}/f" };

        Assert.Equal([$"{owner}/f/a", $"{owner}/f/b"], TheDock.CopiedWith($"{owner}/f/b", selected));
        Assert.Equal([$"{owner}/clock", $"{owner}/f"], TheDock.CopiedWith($"{owner}/clock", selected));
        Assert.Equal([$"{owner}/f/z"], TheDock.CopiedWith($"{owner}/f/z", selected));
    }

    /// <summary>The bar's blocks on the clipboard, which wear the marching ants: none, some, by key without case.</summary>
    [Fact]
    public void TheBlocksOnTheClipboardAreMarked()
    {
        var keys = new[] { "layout:default/start", "layout:default/clock", "layout:default/tray" };
        Assert.Null(TheDock.CopiedParts([], keys));
        Assert.Null(TheDock.CopiedParts(["layout:default/gone"], keys));
        Assert.Equal([1, 2], TheDock.CopiedParts(["LAYOUT:default/Clock", "layout:default/tray"], keys));
    }
}
