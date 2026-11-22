// §586 (the person: "in the add widgets menu show how many widgets from which type are already placed and also sum this up
// over the sections itself"): what the add page counts (Karpo.Core\AddCounts.cs) — each widget of the layout the page adds
// into, on its bar and in its folders, by its kind; a tile its kind's count, a folder of the tree the sum of its kinds, each
// once; a premade widget's and a ready-made folder's tile nothing, as the file cannot tell what they were made from — and
// how the page carries it (AddList.Rows, PanelRows.Picks): a number at a tile's end and at a shut folder's head, nothing
// for none.

using Karpo.Dock;
using Karpo.Tests.Core;

namespace Karpo.Tests.Edit;

public sealed class AddCountsTests
{
    private const uint First = 1000;

    /// <summary>A layout's widgets counted by kind: keep awake twice on the bar and once in a folder is three; the folder itself is one.</summary>
    [Fact]
    public void TheLayoutsWidgetsAreCountedByKindOnTheBarAndInItsFolders()
    {
        var placed = AddCounts.Placed(Layout());
        Assert.Equal(3, placed["keepAwake"]);
        Assert.Equal(2, placed["clock"]);
        Assert.Equal(1, placed["weatherNow"]);
        Assert.Equal(1, placed["energySaver"]);
        Assert.Equal(1, placed[Configs.FolderKind(FolderView.List).New().Info!.Id]);
        Assert.False(placed.ContainsKey("battery"));
        Assert.Empty(AddCounts.Placed([]));
    }

    /// <summary>
    /// A tile counts its kind (Again, as Popular shows it, the same); a premade widget and a ready-made folder count nothing;
    /// a folder of the tree sums its kinds, each once however often it shows one.
    /// </summary>
    [Fact]
    public void ATileCountsItsKindAndAFolderSumsItsKindsOnce()
    {
        var picks = AddTree.Picks(AddTree.On(WidgetType.List).SelectMany(section => section.Rows)).ToList();
        var clock = picks.First(pick => pick is { Again: false, Entry: { Id: "clock", Premade: null } });
        var again = clock with { Again = true };
        var awake = picks.First(pick => pick is { Again: false, Entry: { Id: "keepAwake", Premade: null } });
        var premade = picks.First(pick => pick.Entry?.Premade is not null);
        // Ready-made folders are the bar's page's.
        var preset = AddTree.Picks(AddTree.On(WidgetType.Bar).SelectMany(section => section.Rows)).First(pick => pick.Preset is not null);
        // The premade's own widget is placed four times: its tile still says nothing.
        var placed = new Dictionary<string, int>(StringComparer.Ordinal) { ["clock"] = 2, ["keepAwake"] = 1, [premade.Entry!.Id] = 4 };
        Assert.Equal(2, AddCounts.Of(clock, placed));
        Assert.Equal(2, AddCounts.Of(again, placed));
        Assert.Equal(0, AddCounts.Of(premade, placed));
        Assert.Equal(0, AddCounts.Of(preset, placed));

        var shelf = AddTree.Browse.Rows.OfType<AddShelf>().First();
        var folder = new AddFolder(shelf, [clock, awake, again, premade, preset, new AddFolder(shelf, [awake])]);
        Assert.Equal(3, AddCounts.Of(folder, placed));
        Assert.Equal(0, AddCounts.Of(folder, new Dictionary<string, int>()));
    }

    /// <summary>
    /// The add page's rows carry the counts: a widget's its kind's, the same where Popular shows it again; a folder of the
    /// tree the sum of its kinds; none, nothing — and the page's tiles and heads say them as their value.
    /// </summary>
    [Fact]
    public void TheAddPageSaysTheCountsOnItsTilesAndFolders()
    {
        var placed = AddCounts.Placed(Layout());
        var (items, _, _) = AddList.Rows(WidgetType.List, turns: false, First, placed);
        var clocks = AddList.Picks(items).Where(item => item.Text == WidgetInfos.Find("clock")!.Title).ToList();
        Assert.True(clocks.Count >= 2, "the clock is not in Popular and at home");
        Assert.All(clocks, item => Assert.Equal(2, item.Placed));
        var awake = AddList.Picks(items).Where(item => item.Text == WidgetInfos.KeepAwake.AddTitle).ToList();
        Assert.True(awake.Count >= 2, "keep awake is not in Quick settings and at home");
        Assert.All(awake, item => Assert.Equal(3, item.Placed));
        Assert.All(AddList.Picks(items).Where(item => item.Text == WidgetInfos.Find("battery")!.Title), item => Assert.Equal(0, item.Placed));

        var sections = AddTree.On(WidgetType.List);
        var folders = items.Where(item => item.Children is not null).ToList();
        var trees = sections.SelectMany(section => section.Rows).OfType<AddFolder>().ToList();
        Assert.Equal(trees.Count, folders.Count);
        foreach (var (item, tree) in folders.Zip(trees))
        {
            Assert.Equal(AddCounts.Of(tree, placed), item.Placed);
        }
        Assert.Contains(folders, item => item.Placed > 0);
        Assert.Contains(folders, item => item.Placed == 0);

        var rows = PanelRows.Picks(items, "Add");
        var shown = rows.SelectMany(row => row.Rows.Prepend(row)).ToList();
        Assert.All(shown.Where(row => row.Kind is PanelRowKind.Pick or PanelRowKind.Expander),
            row => Assert.Equal(row.Item.Placed > 0 ? row.Item.Placed.ToString(System.Globalization.CultureInfo.InvariantCulture) : null, row.Value));
        // The words typed list the tiles with their counts too.
        Assert.All(PanelRows.Picks(items, "Add", "clock").Where(row => row.Item.Text == clocks[0].Text), row => Assert.Equal("2", row.Value));
        // Without a layout's counts, nothing is said.
        var (bare, _, _) = AddList.Rows(WidgetType.List, turns: false, First);
        Assert.All(PanelRows.Picks(bare, "Add").SelectMany(row => row.Rows.Prepend(row)), row => Assert.Null(row.Value));
    }

    /// <summary>A layout: keep awake twice and energy saver on the bar, a folder holding two clocks, the weather now and keep awake.</summary>
    private static List<BarItem> Layout()
    {
        var folder = Configs.FolderKind(FolderView.List).New();
        folder.Folder!.Put(ListKind.Clock.New());
        folder.Folder.Put(ListKind.Clock.New());
        folder.Folder.Put(ListKind.WeatherNow.New());
        folder.Folder.Put(ListKind.KeepAwake.New());
        return [BarKind.KeepAwake.New(), BarKind.KeepAwake.New(), BarKind.EnergySaver.New(), folder];
    }
}
