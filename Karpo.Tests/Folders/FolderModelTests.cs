// Folders in the config (Karpo.Core\Folders.cs): a folder's lists side by side and the lines in
// each, what a folder may hold, how Repair makes any file's folders keep the rule, and what the file
// looks like. A folder is a list folder: the grid folder went on 2026-10-09 (GridRemovedTests), and a
// folder holds several lists since the same day (the person: "several lists in one folder").

using System.Text.Json;
using Karpo.Tests.Core;
using Karpo.Core;
using static Karpo.Core.BarKind;

namespace Karpo.Tests.Folders;

public sealed class FolderModelTests
{
    /// <summary>A folder of lists of lines, each named by its id: "a b | c".</summary>
    private static FolderOptions Lists(string shape)
    {
        var folder = new FolderOptions { Lists = [] };
        foreach (var list in shape.Split('|'))
        {
            folder.Lists.Add([.. list.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(id => new FolderCell { Island = Line(id) })]);
        }
        return folder;
    }

    private static CellItem Line(string id)
    {
        var island = ListKind.Battery.New();
        island.Id = id;
        return island;
    }

    /// <summary>The folder's lists as their lines' ids: "a b | c", and "" for one empty list.</summary>
    private static string Shown(FolderOptions folder) =>
        string.Join(" | ", folder.Lists.Select(list => string.Join(" ", list.Select(cell => cell.Island.Id))));

    private static FolderCell Of(FolderOptions folder, string id) => Karpo.Core.Folders.CellOf(folder, id)!;

    // --- What a folder holds ------------------------------------------------------------

    /// <summary>
    /// A list folder takes what the list registry has a line of, and nothing else: every word of
    /// every registry is asked, so a kind added to one without a line is never put in a list.
    /// </summary>
    [Fact]
    public void OnlyListWidgetsGoIntoAListFolder()
    {
        var words = BarEntries.All.Select(entry => entry.Kind.Id()).Concat(ListWidgets.All.Select(line => line.Kind.Id())).Distinct();
        foreach (var word in words)
        {
            Assert.Equal(ListWidgets.Find(word) is not null, Karpo.Core.Folders.CanAdd(FolderList, word));
        }
        // A folder in a folder, the bar's own parts and a word nobody knows: never.
        foreach (var word in new[] { "folder", "folderList", "start", "apps", "tray", "separator", "hologram" })
        {
            Assert.False(Karpo.Core.Folders.CanAdd(FolderList, word), word);
        }
    }

    /// <summary>
    /// A file read in keeps in each folder only what a list takes (the rulings of 2026-09-26), and
    /// says it changed, so the file is written back once: a list loses its apps (a line of them
    /// would be a bar's run of buttons in a list). What it does take stays where it was.
    /// </summary>
    [Fact]
    public void AFileKeepsInAFolderOnlyWhatAListTakes()
    {
        var list = FolderList.New();
        list.Folder!.Put(Apps.New().AsCell());
        list.Folder.Put(Pin.New().AsCell());

        var again = Configs.RoundTrip(Configs.With(Start.New(), list), out var changed);
        var folders = Configs.Islands(again).Where(island => island.Folder is not null).ToList();

        Assert.True(changed, "a folder lost a widget and the file would not be written back");
        Assert.Equal(new ListKind?[] { ListKind.Pin }, folders.Single().Folder!.Cells.Select(cell => cell.Island.List));
        // Once is enough: read again, it is what the file says.
        Configs.RoundTrip(again, out var twice);
        Assert.False(twice);
    }

    [Fact]
    public void AFolderTakesASecondOfAKindItHasAlready()
    {
        var folder = new FolderOptions();
        Assert.NotNull(Karpo.Core.Folders.Add(FolderList, folder, ListKind.Battery.New()));

        Assert.True(Karpo.Core.Folders.CanAdd(FolderList, ListKind.Battery.Id()));
        Assert.NotNull(Karpo.Core.Folders.Add(FolderList, folder, ListKind.Battery.New()));
        Assert.True(Karpo.Core.Folders.CanAdd(FolderList, ListKind.Clock.Id()));
    }

    // --- The lists ----------------------------------------------------------------------------

    /// <summary>
    /// A new folder is one empty list, and a widget added without a place goes at the end of the last
    /// list, the end of the reading order, as many as you like.
    /// </summary>
    [Fact]
    public void ANewFolderIsOneEmptyListAndAnAddGoesAtTheEndOfTheLast()
    {
        var folder = new FolderOptions();
        Assert.Equal("", Shown(folder));
        Assert.Single(folder.Lists);

        Karpo.Core.Folders.Add(FolderList, folder, Line("a"));
        Karpo.Core.Folders.Add(FolderList, folder, Line("b"));
        Assert.Equal("a b", Shown(folder));

        var two = Lists("a | b");
        Karpo.Core.Folders.Add(FolderList, two, Line("c"));
        Assert.Equal("a | b c", Shown(two));
        Assert.All(two.Cells, cell => Assert.Equal(FolderView.List, cell.Island.View));
        // No limit: a hundred lines, all there.
        var long_ = new FolderOptions();
        for (var index = 0; index < 100; index++)
        {
            Assert.NotNull(Karpo.Core.Folders.Add(FolderList, long_, ListKind.Clock.New()));
        }
        Assert.Equal(100, long_.Cells.Count);
    }

    /// <summary>
    /// A widget put in a new list on the folder's left stands in a list of its own before the first,
    /// on its right after the last; beside an empty list too, which stays (2026-10-11: "there can be
    /// empty columns").
    /// </summary>
    [Fact]
    public void ANewListGoesLeftOrRightOfTheOthers()
    {
        var folder = Lists("a | b");

        Karpo.Core.Folders.InsertLine(FolderList, folder, Line("left"), FolderPlace.NewList(0));
        Karpo.Core.Folders.InsertLine(FolderList, folder, Line("right"), FolderPlace.NewList(folder.Lists.Count));
        Assert.Equal("left | a | b | right", Shown(folder));

        var empty = new FolderOptions();
        Karpo.Core.Folders.InsertLine(FolderList, empty, Line("only"), FolderPlace.NewList(1));
        Assert.Equal(" | only", Shown(empty));
    }

    /// <summary>
    /// A "+" on the seam between two lines puts the widget between them; a list's end "+" at its
    /// top (a bottom bar's) or its end (a top bar's); a place past a list's end is its end.
    /// </summary>
    [Fact]
    public void ALineIsInsertedBetweenTwoOrAtAnEnd()
    {
        var folder = Lists("a b c | d");

        Karpo.Core.Folders.InsertLine(FolderList, folder, Line("ab"), new FolderPlace(0, 1));
        Karpo.Core.Folders.InsertLine(FolderList, folder, Line("top"), new FolderPlace(1, 0));
        Karpo.Core.Folders.InsertLine(FolderList, folder, Line("end"), new FolderPlace(1, 99));
        Assert.Equal("a ab b c | top d end", Shown(folder));
        Assert.Equal(new FolderPlace(1, 2), Karpo.Core.Folders.PlaceOf(folder, Of(folder, "end")));
        // A kind no list has a line of goes nowhere.
        Assert.Null(Karpo.Core.Folders.InsertLine(FolderList, folder, Start.New().AsCell(), new FolderPlace(0, 0)));
        Assert.Equal(7, folder.Cells.Count);
    }

    /// <summary>
    /// A list between two with widgets stays when its last widget is taken out: an empty column
    /// (2026-10-11: "there can be empty columns"); one outside them goes (§328), and the folder's last
    /// widget leaves it one list. What was taken out says where it was — a new list where its list
    /// went — so it can be put back there.
    /// </summary>
    [Fact]
    public void AListStaysWhenItsLastWidgetIsTakenOut()
    {
        var folder = Lists("a | b c | d");

        Assert.Equal(new FolderPlace(1, 0), Karpo.Core.Folders.Take(folder, Of(folder, "b")));
        Assert.Equal("a | c | d", Shown(folder));
        Assert.Equal(new FolderPlace(1, 0), Karpo.Core.Folders.Take(folder, Of(folder, "c")));
        Assert.Equal("a |  | d", Shown(folder));
        Assert.Equal(FolderPlace.NewList(0), Karpo.Core.Folders.Take(folder, Of(folder, "a")));
        Assert.Equal("d", Shown(folder));
        var d = Of(folder, "d");
        Assert.Equal(new FolderPlace(0, 0), Karpo.Core.Folders.Take(folder, d));
        Assert.Equal("", Shown(folder));
        Assert.Single(folder.Lists);
        Assert.Null(Karpo.Core.Folders.Take(folder, d));

        // Put back where it was taken from.
        var again = Lists("a | b");
        var where = Karpo.Core.Folders.Take(again, Of(again, "a"))!.Value;
        Karpo.Core.Folders.InsertLine(FolderList, again, Line("a"), where);
        Assert.Equal("a | b", Shown(again));
    }

    /// <summary>The path a widget is taken out by — the dock's menu, the settings app — takes its list too, outside the others (§328).</summary>
    [Fact]
    public void RemoveByPathLeavesAListThatEmpties()
    {
        var holder = FolderList.New();
        holder.Folder!.Lists = Lists("a | b").Lists;
        var list = new List<BarItem> { holder };

        Karpo.Core.Folders.Remove(list, Karpo.Core.Folders.Path(holder.Id, "a"));

        Assert.Equal("b", Shown(holder.Folder));
    }

    /// <summary>
    /// A line carried within its list lands above the line it was let go above; carried to another
    /// list it goes in there — an empty one too — and the list it left stays, empty where that was its
    /// last, between two with widgets, and goes outside them (§328); carried beside the folder it is a
    /// new list there — beside its own list too, alone in it, that list staying empty between. Letting it go just above or below itself moves nothing, which is what
    /// the folder shows by not showing a landing (FolderDrag.Moves).
    /// </summary>
    [Fact]
    public void ALineIsCarriedWithinAListBetweenListsAndIntoANewOne()
    {
        var folder = Lists("a b c | d");

        Assert.True(Karpo.Core.Folders.Move(folder, Of(folder, "a"), new FolderPlace(0, 3)));
        Assert.Equal("b c a | d", Shown(folder));
        Assert.True(Karpo.Core.Folders.Move(folder, Of(folder, "a"), new FolderPlace(0, 0)));
        Assert.Equal("a b c | d", Shown(folder));
        Assert.True(Karpo.Core.Folders.Move(folder, Of(folder, "b"), new FolderPlace(1, 0)));
        Assert.Equal("a c | b d", Shown(folder));
        Assert.True(Karpo.Core.Folders.Move(folder, Of(folder, "c"), FolderPlace.NewList(2)));
        Assert.Equal("a | b d | c", Shown(folder));
        // Out of the first list, alone in it: that list goes, outside the others.
        Assert.True(Karpo.Core.Folders.Move(folder, Of(folder, "a"), new FolderPlace(1, 2)));
        Assert.Equal("b d a | c", Shown(folder));

        Assert.False(Karpo.Core.Folders.Move(folder, Of(folder, "d"), new FolderPlace(0, 1)));
        Assert.False(Karpo.Core.Folders.Move(folder, Of(folder, "d"), new FolderPlace(0, 2)));
        Assert.Equal("b d a | c", Shown(folder));
        // Alone in its list, a new list beside it: that list stays, empty, and it stands next to it.
        var lone = Lists("a | c");
        Assert.True(Karpo.Core.Folders.Move(lone, Of(lone, "c"), FolderPlace.NewList(2)));
        Assert.Equal("a |  | c", Shown(lone));
        Assert.True(Karpo.Core.Folders.Move(lone, Of(lone, "a"), FolderPlace.NewList(0)));
        Assert.Equal("a |  |  | c", Shown(lone));

        // Into an empty column: it is a list like the others; the ones it leaves outside go.
        Assert.True(Karpo.Core.Folders.Move(lone, Of(lone, "c"), new FolderPlace(1, 0)));
        Assert.Equal("a | c", Shown(lone));
    }

    /// <summary>
    /// One line moves as a group of one does (Folders.Move is Folders.MoveGroup; §239), and changes
    /// nothing exactly just above or just below itself in its own list — where the folder shows no
    /// landing — for every line, place and new list of a few shapes.
    /// </summary>
    [Fact]
    public void ALineMovesExceptJustAboveOrBelowItself()
    {
        foreach (var shape in new[] { "a", "a b c", "a | b", "a b | c | d e f", " | a", "a |  | b c" })
        {
            var count = Lists(shape).Lists.Count;
            foreach (var id in Lists(shape).Cells.Select(cell => cell.Island.Id))
            {
                var wants = Enumerable.Range(0, count).SelectMany(list => Enumerable.Range(0, 4).Select(index => new FolderPlace(list, index)))
                    .Concat(Enumerable.Range(0, count + 1).Select(FolderPlace.NewList));
                foreach (var want in wants)
                {
                    var folder = Lists(shape);
                    var cell = Of(folder, id);
                    var from = Karpo.Core.Folders.PlaceOf(folder, cell)!.Value;
                    // A place past a list's end is its end, as the move reads it.
                    var asked = want.New ? want : want with { Index = Math.Min(want.Index, folder.Lists[want.List].Count) };
                    var moves = asked.New || asked.List != from.List || (asked.Index != from.Index && asked.Index != from.Index + 1);
                    Assert.Equal(moves, Karpo.Core.Folders.Move(folder, cell, asked));
                }
            }
        }
    }

    // --- Repairing ----------------------------------------------------------------------------

    /// <summary>
    /// A file's folder keeps the rule whatever it says: no missing list, no line without a widget,
    /// and one list at least. An empty list between two with widgets stays — an empty column is one
    /// somebody kept — and so does one the repair empties; a folder of none but empty lists is one
    /// (§328: a file's empty columns outside its widgets are trimmed once).
    /// </summary>
    [Fact]
    public void RepairKeepsTheListsRule()
    {
        var config = Configs.Repaired("""
            { "layouts": [ { "id": "L", "islands": [ { "id": "f", "kind": "folderList", "folder": { "lists": [
              [ { "island": { "id": "a", "kind": "start" } } ],
              [],
              null,
              [ { "island": { "id": "b", "kind": "battery" } }, null, { } ],
              [ { "island": { "id": "c", "kind": "clock" } } ] ] } },
              { "id": "e", "kind": "folderList", "folder": { "lists": [ [], [] ] } },
              { "id": "n", "kind": "folderList", "folder": { "lists": null } } ] } ] }
            """, out var changed);

        Assert.True(changed);
        var islands = Configs.Islands(config);
        Assert.Equal(" |  | b | c", Shown(islands[0].Folder!));
        // Empty lists are the file's own, kept as they are (§328).
        Assert.Equal(" | ", Shown(islands[1].Folder!));
        Assert.Single(islands[2].Folder!.Lists);
    }

    [Fact]
    public void RepairDropsWhatAFolderCantHold()
    {
        var config = Configs.Repaired("""
            { "layouts": [ { "id": "L", "islands": [ { "id": "f", "kind": "folderList", "folder": { "lists": [ [
              { "island": { "id": "a", "kind": "start" } },
              { "island": { "id": "b", "kind": "folderList" } },
              { "island": { "id": "c", "kind": "hologram" } },
              { "island": { "id": "d", "kind": "clock" } },
              null,
              { },
              { "island": { "id": "e", "kind": "battery" } },
              { "island": { "id": "g", "kind": "battery" } } ] ] } } ] } ] }
            """, out var changed);

        // Windows' own parts, a folder in a folder and a kind this build doesn't know go; a
        // second widget of a kind the folder has already stays.
        Assert.True(changed);
        var folder = Configs.Islands(config)[0].Folder!;
        Assert.Equal(["d", "e", "g"], folder.Cells.Select(cell => cell.Island.Id));
        Assert.NotNull(folder.Cells[0].Island.Clock);
    }

    [Fact]
    public void AWidgetInAFolderIsFoundByItsPath()
    {
        var config = Configs.Repaired("""
            { "layouts": [ { "id": "L", "islands": [
              { "id": "c", "kind": "touchKeyboard" },
              { "id": "f", "kind": "folderList", "folder": { "lists": [ [ { "island": { "id": "v", "kind": "clock" } } ], [ { "island": { "id": "w", "kind": "weatherNow" } } ] ] } } ] } ] }
            """);
        var owner = Config.LayoutOwner(config.DefaultLayout.Id);

        Assert.Equal(ListKind.WeatherNow, Assert.IsType<CellItem>(config.FindIsland(owner, "f/w")).List);
        Assert.Equal(ListKind.WeatherNow, Assert.IsType<CellItem>(config.FindIsland(owner, "F/W")).List);
        Assert.Equal(BarKind.TouchKeyboard, Assert.IsType<BarItem>(config.FindIsland(owner, "c")).Kind);
        Assert.Null(config.FindIsland(owner, "c/w"));
        Assert.Null(config.FindIsland(owner, "f/x"));
        Assert.Null(config.FindIsland(owner, "w"));
    }

    [Fact]
    public void RemoveTakesAWidgetOffTheBarOrOutOfItsFolder()
    {
        var list = new List<BarItem> { BarKind.TouchKeyboard.New(), FolderList.New() };
        var folder = list[1].Folder!;
        var weather = Karpo.Core.Folders.Add(FolderList, folder, ListKind.WeatherNow.New())!.Island;

        Assert.Same(weather, Karpo.Core.Folders.Remove(list, Karpo.Core.Folders.Path(list[1].Id, weather.Id)));
        Assert.Empty(folder.Cells);
        Assert.Equal(BarKind.TouchKeyboard, Assert.IsType<BarItem>(Karpo.Core.Folders.Remove(list, list[0].Id)).Kind);
        Assert.Null(Karpo.Core.Folders.Remove(list, "nothing"));
        Assert.Single(list);
    }

    [Fact]
    public void ADuplicatedFolderGetsNewIdsForItsWidgetsToo()
    {
        var island = FolderList.New();
        var inside = Karpo.Core.Folders.Add(FolderList, island.Folder!, ListKind.Clock.New())!.Island;

        var copy = island.Copy();

        Assert.NotEqual(island.Id, copy.Id);
        Assert.NotEqual(inside.Id, copy.Folder!.Cells.Single().Island.Id);
        Assert.Equal(ListKind.Clock, copy.Folder.Cells.Single().Island.List);
    }

    [Fact]
    public void FreeIdAvoidsEveryIdOfTheListAndItsFolders()
    {
        var list = new List<BarItem> { FolderList.New() };
        var inside = Karpo.Core.Folders.Add(FolderList, list[0].Folder!, ListKind.Clock.New())!.Island;

        Assert.NotEqual(inside.Id, Karpo.Core.Folders.FreeId(list, inside.Id));
        Assert.NotEqual(list[0].Id, Karpo.Core.Folders.FreeId(list, list[0].Id.ToUpperInvariant()));
        Assert.Equal("fresh", Karpo.Core.Folders.FreeId(list, "fresh"));
        Assert.DoesNotContain('/', Karpo.Core.Folders.FreeId(list, "a/b"));
    }

    // --- The file ---------------------------------------------------------------------------------

    /// <summary>
    /// §427: a folder has no name. A file that still says one, and to show it, reads as the folder it
    /// is otherwise — its face, its lists — and is written without either.
    /// </summary>
    [Fact]
    public void AFoldersNameIsNotKept()
    {
        var config = Configs.Repaired("""
            { "layouts": [ { "id": "L", "islands": [
              { "id": "f", "kind": "folderList", "folder": { "name": "Tools", "showName": true,
                "face": { "kind": "icon", "options": {} }, "lists": [ [ { "island": { "id": "a", "kind": "battery" } } ] ] } } ] } ] }
            """, out _);

        var read = Configs.Islands(config).Single().Folder!;
        Assert.Equal(FolderFaces.IconId, read.Face.Kind);
        Assert.Single(read.Cells);
        var written = config.ToJson();
        Assert.DoesNotContain("\"showName\"", written, StringComparison.Ordinal);
        Assert.DoesNotContain("Tools", written, StringComparison.Ordinal);
    }

    [Fact]
    public void AFolderReadsBackTheSame()
    {
        var folder = FolderList.New();
        // Set, and not left to the default: what this holds is that a file reads back what was
        // written, which says nothing about what a new folder starts as.
        folder.Folder!.Align = FolderAlign.Right;
        Karpo.Core.Folders.Add(FolderList, folder.Folder, ListKind.WeatherNow.New());
        Karpo.Core.Folders.InsertLine(folder.Kind, folder.Folder, ListKind.Drive.New(), FolderPlace.NewList(1));
        Karpo.Core.Folders.Add(folder.Kind, folder.Folder, ListKind.Clock.New());
        var config = Configs.With(Start.New(), folder);

        var again = Configs.RoundTrip(config, out var changed);

        Assert.False(changed);
        Assert.Equal(config.ToJson(), again.ToJson());
        var read = Configs.Islands(again)[1].Folder!;
        Assert.Equal(FolderAlign.Right, read.Align);
        Assert.Equal(
            [[ListKind.WeatherNow], [ListKind.Drive, ListKind.Clock]],
            read.Lists.Select(list => list.Select(cell => cell.Island.List!.Value).ToArray()).ToArray());
    }

    /// <summary>
    /// An empty list between two with widgets is kept in the file and read back where it was, with its
    /// width beside it: an empty column persists (2026-10-11), as an empty array among the lists.
    /// </summary>
    [Fact]
    public void AnEmptyListIsWrittenAndReadBack()
    {
        var folder = FolderList.New();
        Karpo.Core.Folders.Add(FolderList, folder.Folder!, ListKind.Battery.New());
        folder.Folder!.Lists.Add([]);
        Karpo.Core.Folders.InsertLine(FolderList, folder.Folder!, ListKind.Clock.New(), FolderPlace.NewList(2));
        Karpo.Core.Folders.SetWidth(folder.Folder!, 1, FolderListWidth.Wide);
        var config = Configs.With(folder);

        using (var file = JsonDocument.Parse(config.ToJson()))
        {
            var options = file.RootElement.GetProperty("layouts")[0].GetProperty("islands")[0].GetProperty("folder");
            Assert.Equal([1, 0, 1], options.GetProperty("lists").EnumerateArray().Select(list => list.GetArrayLength()));
            Assert.Equal(["medium", "wide", "medium"], options.GetProperty("widths").EnumerateArray().Select(width => width.GetString()));
        }
        var again = Configs.RoundTrip(config, out var changed);

        Assert.False(changed);
        var read = Configs.Islands(again)[0].Folder!;
        Assert.Equal([1, 0, 1], read.Lists.Select(list => list.Count));
        Assert.Equal(FolderListWidth.Wide, read.WidthOf(1));
    }

    /// <summary>
    /// A folder is written as its lists, an array of arrays of lines in their order: no columns,
    /// rows, spans or height any more (shape 22).
    /// </summary>
    [Fact]
    public void AFolderIsWrittenAsItsLists()
    {
        var folder = FolderList.New();
        Karpo.Core.Folders.Add(FolderList, folder.Folder!, ListKind.Battery.New());
        Karpo.Core.Folders.InsertLine(FolderList, folder.Folder!, ListKind.Clock.New(), FolderPlace.NewList(1));
        var config = Configs.With(folder);

        using var file = JsonDocument.Parse(config.ToJson());
        var written = file.RootElement.GetProperty("layouts")[0].GetProperty("islands")[0];

        Assert.Equal("folderList", written.GetProperty("kind").GetString());
        var options = written.GetProperty("folder");
        // No "width": each list has its own ("widths", written once a list is given one; FolderListWidthTests).
        // No "cellSize": a cell is fixed (FolderOptions.CellPx). "face" is what its button wears,
        // its word and its options (FolderFaces). No "glyph": an icon face has its own (§259).
        Assert.Equal(
            ["face", "lists"],
            options.EnumerateObject().Select(property => property.Name));
        Assert.Equal("icon", options.GetProperty("face").GetProperty("kind").GetString());
        var lists = options.GetProperty("lists");
        Assert.Equal(JsonValueKind.Array, lists.ValueKind);
        Assert.Equal([1, 1], lists.EnumerateArray().Select(list => list.GetArrayLength()));
        var cell = lists[0][0];
        Assert.Equal(["island"], cell.EnumerateObject().Select(property => property.Name));
        Assert.Equal("battery", cell.GetProperty("island").GetProperty("kind").GetString());
        Assert.Equal("clock", lists[1][0].GetProperty("island").GetProperty("kind").GetString());
    }

    [Fact]
    public void TheFolderKindIsAList()
    {
        Assert.Equal(FolderView.List, Karpo.Core.Folders.View(FolderList));
        Assert.True(FolderList.IsFolder());
        Assert.NotNull(FolderList.New().Folder);
        Assert.Equal([FolderList], Enum.GetValues<BarKind>().Where(kind => kind.IsFolder()));
    }

    /// <summary>The keys walk, and a 2×2 face shows, list by list and each top to bottom.</summary>
    [Fact]
    public void TheReadingOrderIsListByList()
    {
        var folder = Lists("a b | c | d e");

        Assert.Equal(["a", "b", "c", "d", "e"], Karpo.Core.Folders.ReadingOrder(folder).Select(cell => cell.Island.Id));
    }

    [Fact]
    public void AFileWithoutFoldersIsUnchanged()
    {
        var config = Configs.Repaired("""{ "layouts": [ { "id": "L", "name": "Work", "islands": [ { "id": "c", "kind": "timer" } ] } ] }""", out _);

        Configs.RoundTrip(config, out var changed);

        Assert.False(changed);
        Assert.DoesNotContain("\"folder\"", config.ToJson());
    }

    [Fact]
    public void TheFolderKindIsPlaceableSeveralTimes()
    {
        List<BarItem> list = [FolderList.New()];

        Assert.True(Sections.CanAdd(list, FolderList));
        Assert.True(FolderList.IsIsland());
        Assert.Equal("Folder", FolderList.Title());
        // The kind's own glyph is the list's; a folder's button shows the folder glyph (FolderOptions.FolderGlyph).
        Assert.Equal("", FolderList.Glyph());
        Assert.Equal("\"folderList\"", JsonSerializer.Serialize(FolderList));
    }
}
