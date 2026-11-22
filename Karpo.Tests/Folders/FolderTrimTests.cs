// Empty columns outside a folder's widgets go (the person, 2026-10-23: "empty columns pile up and
// can't be removed"; docs\widgets.md §328): a move or a removal that leaves empty lists before the
// first list with widgets or after the last drops them (Folders.Trim), the ones between stay, spacers,
// and every kept list keeps its width. A file is read with the columns it has. A far slot is no empty
// column: a widget moved there still makes the lists between.

using Karpo.Tests.Core;

namespace Karpo.Tests.Folders;

public sealed class FolderTrimTests
{
    /// <summary>A folder of lists of battery lines, each named by its id: "a b | c", an empty list "".</summary>
    private static FolderOptions Lists(string shape, params FolderListWidth[] widths)
    {
        var folder = new FolderOptions { Lists = [], Widths = widths.Length > 0 ? [.. widths] : null };
        foreach (var list in shape.Split('|'))
        {
            folder.Lists.Add([.. list.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(id =>
            {
                var island = ListKind.Battery.New();
                island.Id = id;
                return new FolderCell { Island = island };
            })]);
        }
        return folder;
    }

    /// <summary>The folder's lists in the same words: "a b | c".</summary>
    private static string Shape(FolderOptions folder) =>
        string.Join(" | ", folder.Lists.Select(list => string.Join(' ', list.Select(cell => cell.Island.Id))));

    private static FolderCell Cell(FolderOptions folder, string id) => Karpo.Core.Folders.CellOf(folder, id)!;

    /// <summary>A list file of folders <paramref name="folders"/> (each "id": its JSON body), in the file's own words.</summary>
    private static string File(params (string Id, string Body)[] folders) => $$"""
        { "layouts": [ { "id": "default", "name": "Default", "islands": [
          {{string.Join(",\n", folders.Select(folder => $$"""{ "id": "{{folder.Id}}", "kind": "folderList", "section": "end", "folder": {{folder.Body}} }"""))}} ] } ] }
        """;

    /// <summary>Lists of <paramref name="counts"/> battery lines each, ids "<paramref name="id"/>0", "<paramref name="id"/>1" …, as the file writes them.</summary>
    private static string ListsJson(string id, params int[] counts)
    {
        var next = 0;
        return "[" + string.Join(", ", counts.Select(count => "[" + string.Join(", ", Enumerable.Range(0, count).Select(_ => $$"""{ "island": { "id": "{{id}}{{next++}}", "kind": "battery" } }""")) + "]")) + "]";
    }

    /// <summary>
    /// A file's empty columns are read as they are, outside its widgets too, each with its width: only a
    /// move or a removal drops one. Written back, they are the same.
    /// </summary>
    [Fact]
    public void AFilesEmptyColumnsAreReadAsTheyAre()
    {
        var config = Configs.Repaired(File(
            ("tides", $$"""{ "name": "Tides", "lists": {{ListsJson("t", 0, 5, 0, 1, 0)}}, "widths": ["narrow", "medium", "wide", "medium", "widest"] }""")));
        var tides = Karpo.Core.Folders.FolderIn(Configs.Islands(config), "tides")!;

        Assert.Equal(" | t0 t1 t2 t3 t4 |  | t5 | ", Shape(tides));
        Assert.Equal([FolderListWidth.Narrow, FolderListWidth.Medium, FolderListWidth.Wide, FolderListWidth.Medium, FolderListWidth.Widest], tides.Widths);
        var again = Configs.RoundTrip(config, out var changed);
        Assert.False(changed);
        Assert.Equal(" | t0 t1 t2 t3 t4 |  | t5 | ", Shape(Karpo.Core.Folders.FolderIn(Configs.Islands(again), "tides")!));
    }

    /// <summary>
    /// A line carried out of the outermost list, into another or into a far slot on the other side,
    /// takes the empty lists it leaves outside with it, each with its width; the ones between the lists
    /// with widgets stay, and so do the lists a far slot makes between. A move that changes nothing
    /// drops nothing. What a drag shows while it is carried keeps every list (MoveKeepingLists).
    /// </summary>
    [Fact]
    public void AMoveDropsTheEmptyColumnsItLeavesOutside()
    {
        var folder = Lists(" | a |  | b | c", FolderListWidth.Narrow, FolderListWidth.Wide, FolderListWidth.ExtraWide, FolderListWidth.Widest, FolderListWidth.Medium);
        Assert.True(Karpo.Core.Folders.Move(folder, Cell(folder, "a"), new FolderPlace(3, 0)));
        Assert.Equal("a b | c", Shape(folder));
        Assert.Equal([FolderListWidth.Widest, FolderListWidth.Medium], folder.Widths);

        // A far slot on the left: the lists between it and the others are made, the one left empty
        // on the right goes.
        Assert.True(Karpo.Core.Folders.Move(folder, Cell(folder, "c"), FolderPlace.Slot(0, 2)));
        Assert.Equal("c |  |  | a b", Shape(folder));
        Assert.Equal([FolderListWidth.Medium, FolderListWidth.Medium, FolderListWidth.Medium, FolderListWidth.Widest], folder.Widths);

        // Back beside the others: the far slot's lists were only between, and go with it.
        Assert.True(Karpo.Core.Folders.Move(folder, Cell(folder, "c"), FolderPlace.NewList(4)));
        Assert.Equal("a b | c", Shape(folder));
        Assert.Equal([FolderListWidth.Widest, FolderListWidth.Medium], folder.Widths);

        // Nothing moved, nothing dropped: an empty column outside set by its slot's width stays.
        Assert.True(Karpo.Core.Folders.SetSlotWidth(folder, FolderPlace.Slot(2, 0), FolderListWidth.Wide));
        Assert.False(Karpo.Core.Folders.Move(folder, Cell(folder, "c"), new FolderPlace(1, 0)));
        Assert.Equal("a b | c | ", Shape(folder));

        // Several carried together, the same rule; the drag's picture of it keeps every list.
        var group = Lists("x | y z |  | w");
        var shown = Lists("x | y z |  | w");
        Assert.True(Karpo.Core.Folders.MoveKeepingLists(shown, [Cell(shown, "w"), Cell(shown, "x")], new FolderPlace(1, 2)));
        Assert.Equal(" | y z w x |  | ", Shape(shown));
        Assert.True(Karpo.Core.Folders.MoveGroup(group, [Cell(group, "w"), Cell(group, "x")], new FolderPlace(1, 2)));
        Assert.Equal("y z w x", Shape(group));
    }

    /// <summary>
    /// A line taken out takes the empty lists it leaves outside with it, and says where it stood so
    /// putting it back makes its list again there: the first list's, a new first list; the last's, a
    /// new last one; one between, its place. A spacer between stays. The last widget of a folder
    /// leaves it one list, its first.
    /// </summary>
    [Fact]
    public void ARemovalDropsTheEmptyColumnsItLeavesOutsideAndPutsBack()
    {
        var folder = Lists("a |  | b c |  | d | ", FolderListWidth.Narrow, FolderListWidth.Wide, FolderListWidth.Medium, FolderListWidth.ExtraWide, FolderListWidth.Widest, FolderListWidth.Narrow);
        var a = Cell(folder, "a");
        var at = Karpo.Core.Folders.Take(folder, a);
        Assert.Equal(FolderPlace.NewList(0), at);
        Assert.Equal("b c |  | d", Shape(folder));
        Assert.Equal([FolderListWidth.Medium, FolderListWidth.ExtraWide, FolderListWidth.Widest], folder.Widths);
        Assert.NotNull(Karpo.Core.Folders.InsertLine(BarKind.FolderList, folder, a.Island, at!.Value));
        Assert.Equal("a | b c |  | d", Shape(folder));

        var d = Cell(folder, "d");
        at = Karpo.Core.Folders.Take(folder, d);
        Assert.Equal(FolderPlace.NewList(2), at);
        Assert.Equal("a | b c", Shape(folder));
        Assert.NotNull(Karpo.Core.Folders.InsertLine(BarKind.FolderList, folder, d.Island, at!.Value));
        Assert.Equal("a | b c | d", Shape(folder));

        var c = Cell(folder, "c");
        at = Karpo.Core.Folders.Take(folder, c);
        Assert.Equal(new FolderPlace(1, 1), at);
        Assert.Equal("a | b | d", Shape(folder));

        // Through the list's path, as the dock and Settings remove: the folder's last widget leaves one list.
        var holder = BarKind.FolderList.New();
        holder.Id = "f";
        holder.Folder = Lists(" | e | ", FolderListWidth.Wide, FolderListWidth.Narrow, FolderListWidth.Widest);
        List<BarItem> list = [holder];
        Assert.NotNull(Karpo.Core.Folders.Remove(list, "f/e"));
        Assert.Equal("", Shape(holder.Folder));
        Assert.Equal([FolderListWidth.Wide], holder.Folder.Widths);
    }
}
