// A width per list of a folder (2026-10-10: "you should be able to change the width of individual
// columns in the folder view ... three presets or so. the global width configuration should be
// removed"): narrow, medium and wide, and since 2026-10-18 extra wide and widest (FolderListWidth;
// ColumnWidthTests), kept beside the lists (FolderOptions.Widths),
// moved with its list by every edit that moves a list, read tolerantly; and in the open folder, arranged, the width button
// beside each list's "+" that asks the dock for the menu, a pick resizing that list in place.
//
// The geometry (each list its own width, the button's place beside the "+", nothing moving between
// rest and edit mode) is ListFolderTests'; the pictures are MultiListPictures.ListWidthSheets.

using System.Text.Json;
using System.Text.Json.Nodes;
using Karpo.Dock;
using Karpo.Tests.Core;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using Color = Windows.UI.Color;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Folders;

public sealed unsafe class FolderListWidthTests
{
    private const string Owner = "layout:default";

    /// <summary>A folder of lists of lines, each named by its id: "a b | c".</summary>
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

    private static FolderCell Of(FolderOptions folder, string id) => Karpo.Core.Folders.CellOf(folder, id)!;

    /// <summary>Each list's width, first to last.</summary>
    private static FolderListWidth[] Widths(FolderOptions folder) => [.. Enumerable.Range(0, folder.Lists.Count).Select(folder.WidthOf)];

    // --- The presets ---------------------------------------------------------------------

    /// <summary>
    /// Five presets, offered narrow to widest; medium is the panels' own 360 and what a list is until
    /// somebody chooses; a value this build does not know is medium too.
    /// </summary>
    [Fact]
    public void FivePresetsMediumThePanelsOwn()
    {
        Assert.Equal([FolderListWidth.Narrow, FolderListWidth.Medium, FolderListWidth.Wide, FolderListWidth.ExtraWide, FolderListWidth.Widest], FolderListWidths.All);
        Assert.Equal(Flyout.PanelWidthDip, FolderListWidths.Dip(FolderListWidth.Medium));
        Assert.Equal([280, 360, 480, 640, 800], FolderListWidths.All.Select(FolderListWidths.Dip));
        Assert.Equal(FolderListWidths.MediumDip, FolderListWidths.Dip((FolderListWidth)(-1)));
        Assert.Equal(FolderListWidth.Medium, default(FolderListWidth));

        var folder = Lists("a | b");
        Assert.Equal([FolderListWidth.Medium, FolderListWidth.Medium], Widths(folder));
        Assert.Equal(FolderListWidth.Medium, folder.WidthOf(-1));
        Assert.Equal(FolderListWidth.Medium, folder.WidthOf(7));
    }

    // --- A list's width goes with the list -------------------------------------------------

    /// <summary>A width is set for a list the folder has, once; a list it has not, or the width it already is, changes nothing.</summary>
    [Fact]
    public void AWidthIsSetForOneList()
    {
        var folder = Lists("a | b | c");

        Assert.True(Karpo.Core.Folders.SetWidth(folder, 1, FolderListWidth.Wide));
        Assert.Equal([FolderListWidth.Medium, FolderListWidth.Wide, FolderListWidth.Medium], Widths(folder));
        Assert.False(Karpo.Core.Folders.SetWidth(folder, 1, FolderListWidth.Wide));
        Assert.False(Karpo.Core.Folders.SetWidth(folder, 3, FolderListWidth.Narrow));
        Assert.False(Karpo.Core.Folders.SetWidth(folder, -1, FolderListWidth.Narrow));
        Assert.False(Karpo.Core.Folders.SetWidth(folder, 0, (FolderListWidth)(-1)));
        Assert.True(Karpo.Core.Folders.SetWidth(folder, 2, FolderListWidth.Narrow));
        Assert.Equal([FolderListWidth.Medium, FolderListWidth.Wide, FolderListWidth.Narrow], Widths(folder));
    }

    /// <summary>
    /// A width stays with its list: a list emptied between two with widgets — its last line taken out
    /// or carried away — stays, and keeps its width (2026-10-11: empty columns are kept); one emptied
    /// outside them goes, and its width with it (§328); a new list for one line is medium, and the
    /// lists after it keep theirs.
    /// </summary>
    [Fact]
    public void AWidthGoesWithItsList()
    {
        var folder = Lists("a | b c | d");
        folder.Widths = [FolderListWidth.Narrow, FolderListWidth.Wide, FolderListWidth.Medium];

        // "c" a new list at the end: medium, the others as they were.
        Assert.True(Karpo.Core.Folders.Move(folder, Of(folder, "c"), FolderPlace.NewList(3)));
        Assert.Equal([FolderListWidth.Narrow, FolderListWidth.Wide, FolderListWidth.Medium, FolderListWidth.Medium], Widths(folder));

        // "b", alone in its wide list, carried into the first: the wide list stays where it was, empty.
        Assert.True(Karpo.Core.Folders.Move(folder, Of(folder, "b"), new FolderPlace(0, 0)));
        Assert.Equal([2, 0, 1, 1], folder.Lists.Select(list => list.Count));
        Assert.Equal([FolderListWidth.Narrow, FolderListWidth.Wide, FolderListWidth.Medium, FolderListWidth.Medium], Widths(folder));

        // "c", alone in the last list, taken out: that list goes, its width with it.
        Karpo.Core.Folders.Take(folder, Of(folder, "c"));
        Assert.Equal([FolderListWidth.Narrow, FolderListWidth.Wide, FolderListWidth.Medium], Widths(folder));

        // A new widget put in as a list of its own before the first: medium, where it went.
        var added = Karpo.Core.Folders.InsertLine(BarKind.FolderList, folder, Line("e"), FolderPlace.NewList(0));
        Assert.NotNull(added);
        Assert.Equal([FolderListWidth.Medium, FolderListWidth.Narrow, FolderListWidth.Wide, FolderListWidth.Medium], Widths(folder));
        Assert.Equal(folder.Lists.Count, folder.Widths!.Count);
    }

    /// <summary>
    /// Tidy keeps the widths beside the lists: a missing list taken out takes its width, an empty
    /// one stays with its own, and no width is kept past the last list.
    /// </summary>
    [Fact]
    public void TidyKeepsTheWidthsBesideTheLists()
    {
        var folder = Lists("a |  | b");
        folder.Lists.Insert(1, null!);
        folder.Widths = [FolderListWidth.Narrow, FolderListWidth.Medium, FolderListWidth.Wide, FolderListWidth.Wide, FolderListWidth.Narrow];

        Assert.True(Karpo.Core.Folders.Tidy(folder));
        Assert.Equal(3, folder.Lists.Count);
        Assert.Equal([FolderListWidth.Narrow, FolderListWidth.Wide, FolderListWidth.Wide], folder.Widths);
        Assert.False(Karpo.Core.Folders.Tidy(folder));

        // No list with a line: the empty lists stay, each with its width.
        var empty = Lists(" | ");
        empty.Widths = [FolderListWidth.Wide, FolderListWidth.Narrow];
        Assert.False(Karpo.Core.Folders.Tidy(empty));
        Assert.Equal(2, empty.Lists.Count);
        Assert.Equal([FolderListWidth.Wide, FolderListWidth.Narrow], empty.Widths);

        // No list at all: one empty list.
        var nothing = new FolderOptions { Lists = [] };
        Assert.True(Karpo.Core.Folders.Tidy(nothing));
        Assert.Single(nothing.Lists);

        // A file with no widths at all is left as it is.
        var none = Lists("a | b");
        none.Widths = null!;
        Karpo.Core.Folders.Tidy(none);
        Assert.Null(none.Widths);
        Assert.Equal([FolderListWidth.Medium, FolderListWidth.Medium], Widths(none));
    }

    // --- The file ------------------------------------------------------------------------

    /// <summary>A folder of three lists as the file has it, its options' JSON handed to <paramref name="edit"/> first.</summary>
    private static BarItem Read(Action<JsonObject> edit)
    {
        var holder = BarKind.FolderList.New();
        holder.Id = "box";
        holder.Folder!.Lists = Lists("a | b | c").Lists;
        var json = JsonNode.Parse(JsonSerializer.Serialize(holder, ConfigJson.Default.BarItem))!.AsObject();
        edit(json["folder"]!.AsObject());
        var read = JsonSerializer.Deserialize(json.ToJsonString(), ConfigJson.Default.BarItem)!;
        read.Normalize();
        return read;
    }

    private static JsonObject Written(BarItem holder) =>
        JsonNode.Parse(JsonSerializer.Serialize(holder, ConfigJson.Default.BarItem))!["folder"]!.AsObject();

    /// <summary>
    /// The widths are written by name, beside the lists, and only once a list was given one — a
    /// folder nobody resized writes none, and a new list of medium adds none; the folder-wide width
    /// is never written; and a picked width is what the file says when it is read again — what the
    /// other app reads.
    /// </summary>
    [Fact]
    public void TheWidthsAreWrittenByNameAndReadBack()
    {
        var fresh = BarKind.FolderList.New();
        Assert.False(Written(fresh).ContainsKey("width"));
        Assert.False(Written(fresh).ContainsKey("widths"));
        var lone = Read(_ => { });
        Assert.True(Karpo.Core.Folders.Move(lone.Folder!, Of(lone.Folder!, "a"), FolderPlace.NewList(3)));
        Assert.False(Written(lone).ContainsKey("widths"));

        var holder = Read(_ => { });
        Assert.True(Karpo.Core.Folders.SetWidth(holder.Folder!, 2, FolderListWidth.Wide));
        Assert.True(Karpo.Core.Folders.SetWidth(holder.Folder!, 0, FolderListWidth.Narrow));
        var written = Written(holder);
        Assert.False(written.ContainsKey("width"));
        Assert.Equal(["narrow", "medium", "wide"], written["widths"]!.AsArray().Select(node => node!.GetValue<string>()));

        var config = Configs.With(holder);
        var again = Configs.RoundTrip(config, out var changed);
        Assert.False(changed, "a folder of picked widths read back as something to repair");
        var folder = Configs.Islands(again).Single(island => island.Folder is not null).Folder!;
        Assert.Equal([FolderListWidth.Narrow, FolderListWidth.Medium, FolderListWidth.Wide], Widths(folder));
    }

    /// <summary>
    /// Read tolerantly: no widths is every list medium; fewer than the lists, the rest medium; a name
    /// this build does not know, or null, is medium where it is read and kept as it was; more than the
    /// lists, the extra go.
    /// </summary>
    [Fact]
    public void TheWidthsAreReadTolerantly()
    {
        Assert.Equal([FolderListWidth.Medium, FolderListWidth.Medium, FolderListWidth.Medium], Widths(Read(folder => folder.Remove("widths")).Folder!));
        Assert.Equal([FolderListWidth.Medium, FolderListWidth.Medium, FolderListWidth.Medium], Widths(Read(folder => folder["widths"] = null).Folder!));
        Assert.Equal([FolderListWidth.Wide, FolderListWidth.Medium, FolderListWidth.Medium], Widths(Read(folder => folder["widths"] = new JsonArray("wide")).Folder!));

        var odd = Read(folder => folder["widths"] = new JsonArray("narrow", "gigantic", null)).Folder!;
        Assert.Equal([FolderListWidth.Narrow, FolderListWidth.Medium, FolderListWidth.Medium], Widths(odd));
        Assert.Equal(3, odd.Widths!.Count);

        var many = Read(folder => folder["widths"] = new JsonArray("wide", "wide", "wide", "narrow", "narrow")).Folder!;
        Assert.Equal([FolderListWidth.Wide, FolderListWidth.Wide, FolderListWidth.Wide], many.Widths);
    }

    // --- The menu ------------------------------------------------------------------------

    /// <summary>A list's width menu is the five presets, narrow to widest, the list's own ticked and only it.</summary>
    [Fact]
    public void TheMenuTicksTheListsOwnWidth()
    {
        foreach (var current in FolderListWidths.All)
        {
            var rows = TheDock.FolderWidthRows(current);
            Assert.Equal(["Narrow", "Medium", "Wide", "Extra wide", "Widest"], rows.Select(row => row.Text));
            Assert.Equal(FolderListWidths.All.Select(width => width == current), rows.Select(row => row.Checked == true));
            Assert.Equal(5, rows.Select(row => row.Id).Distinct().Count());
        }
    }

    // --- The open folder -----------------------------------------------------------------

    private static readonly Look Dark = new(Glass: false, Dark: true, Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4));
    private static readonly POINT Anchor = new() { x = 960, y = 1040 };
    private static readonly RECT Work = new() { right = 1920, bottom = 1040 };

    /// <summary>A list folder of two lists, "box": a clock's line, and the battery's.</summary>
    private static BarItem TwoLists()
    {
        var island = BarKind.FolderList.New();
        island.Id = "box";
        var clock = ListKind.Clock.New();
        clock.Id = "clock";
        var battery = ListKind.Battery.New();
        battery.Id = "battery";
        island.Folder!.Put(clock, 0);
        island.Folder.Put(battery, 1);
        return island;
    }

    /// <summary>
    /// Arranged, a press on a list's width button asks the dock for that list's menu, at the button,
    /// and the folder holds itself open for it; the "+" beside it keeps its own click.
    /// </summary>
    [UiFact]
    public void TheWidthButtonAsksForItsListsMenu()
    {
        WindowShots.OnHiddenDesktop("folder-list-width", (gfx, instance) =>
        {
            var folder = new FolderIsland($"{Owner}/box", TwoLists(), gfx, new Config());
            using var flyout = new FolderFlyout(gfx, instance, 0);
            flyout.Show(gfx, Dark, 1f, Anchor, Work, Edge.Bottom, folder, editing: true);
            var plan = flyout.Plan!;
            var second = plan.Adds.Select((add, index) => (add, index)).Single(entry => entry.add is { Kind: FolderAddKind.Width, List: 1 }).index;

            var at = FolderEditClickTests.Client(flyout.Hwnd, flyout.ScreenOfAdd(second));
            _ = flyout.Message(WM.WM_LBUTTONDOWN, 1, at);
            var asked = flyout.Message(WM.WM_LBUTTONUP, 0, at);
            Assert.Equal(FolderFlyoutAction.Width, asked.Kind);
            Assert.Equal(1, asked.Place.List);
            // Over the button, on its side away from the bar.
            var button = flyout.ScreenOfAdd(second);
            Assert.InRange(asked.At.x, button.x - 1, button.x + 1);
            Assert.True(asked.At.y < button.y, "the menu opens away from a bottom bar");
            Assert.True(flyout.IsOpen && flyout.StaysOpen);
            flyout.MenuClosed();
            Assert.True(flyout.IsOpen);
            flyout.Hide();
            folder.Dispose();
        });
    }

    /// <summary>
    /// A width picked lays the open folder out again in place: that list wider, the list after it
    /// moved along by as much, every line of it as wide as it is, the heights and the other list's
    /// width as they were; the folder still open and arranged.
    /// </summary>
    [UiFact]
    public void APickedWidthResizesTheListInPlace()
    {
        WindowShots.OnHiddenDesktop("folder-list-width-live", (gfx, instance) =>
        {
            var config = new Config();
            var item = TwoLists();
            var folder = new FolderIsland($"{Owner}/box", item, gfx, config);
            using var flyout = new FolderFlyout(gfx, instance, 0);
            flyout.Show(gfx, Dark, 1.5f, Anchor, Work, Edge.Bottom, folder, editing: true);
            var before = flyout.Plan!;

            var next = TwoLists();
            Assert.True(Karpo.Core.Folders.SetWidth(next.Folder!, 0, FolderListWidth.Wide));
            folder.Configure(next, config);

            var after = flyout.Plan!;
            Assert.True(flyout.IsOpen && flyout.Arranged);
            var grown = PopupPaint.Round(FolderListWidths.WideDip * 1.5f) - PopupPaint.Round(FolderListWidths.MediumDip * 1.5f);
            Assert.Equal(before.Lists[0].Width + grown, after.Lists[0].Width, 1);
            Assert.Equal(before.Lists[1].Width, after.Lists[1].Width, 1);
            Assert.Equal(before.Lists[1].Left + grown, after.Lists[1].Left, 1);
            Assert.Equal(before.Lists.Select(list => list.Height), after.Lists.Select(list => list.Height));
            Assert.Equal(after.Lists[0].Width, after.Cards[0].right - after.Cards[0].left, 1);
            // The group over the wider list follows its middle.
            var first = after.Adds.First(add => add.List == 0).Box;
            var last = after.Adds.Last(add => add.List == 0).Box;
            Assert.InRange((first.left + last.right) / 2, after.Lists[0].Left + (after.Lists[0].Width / 2) - 1, after.Lists[0].Left + (after.Lists[0].Width / 2) + 1);
            flyout.Hide();
            folder.Dispose();
        });
    }
}
