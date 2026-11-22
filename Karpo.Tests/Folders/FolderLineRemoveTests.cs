// edit: a list folder's line is removed as a widget on the bar is (the person, 2026-10-09: "i cannot
// remove widgets from the list view"). The ways there are, every one of them for every kind of line:
//
//  - **Its menu's "Remove"** while the bar is arranged (Dock.WidgetRowsFor), on the line clicked first
//    — which selects it — or on one not selected.
//  - **Delete** in the arranged folder: the folder has the keys, so it says so
//    (FolderFlyoutAction.Selection, EditKeyAct.Remove) with the line the keys are on, or none, and the dock removes what
//    that acts on (Dock.RemoveSelected).
//
// What it acts on is EditSelection.ActedOn, and what comes out of the file Dock.RemoveFrom. The root
// cause: what was selected was read through the displays' orders alone, which hold the bars' widgets
// and no folder's line, so a line clicked (and so selected) and then removed came to nothing.
//
// No way of the settings app removes a widget (the bar arranges, docs\widgets.md); dragging a line
// out over nothing was always a cancel, never a removal (§15, §70).

using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using CoreFolders = Karpo.Core.Folders;
using TheDock = Karpo.Dock.Dock;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Folders;

public sealed unsafe class FolderLineRemoveTests
{
    private static readonly string Owner = Config.LayoutOwner("only");
    private const string FolderId = "box";

    /// <summary>Every kind a list's line can be, the new lines of 2026-10-09 among them.</summary>
    public static TheoryData<ListKind> Kinds => new()
    {
        ListKind.Clock, ListKind.Battery, ListKind.Heading, ListKind.Divider, ListKind.Toggles,
        ListKind.Actions, ListKind.Month, ListKind.TimeHeader, ListKind.ScreenTime, ListKind.ScreenTimeApps,
    };

    private static string LineKey(string id) => Config.IslandKey(Owner, CoreFolders.Path(FolderId, id));

    /// <summary>A layout with a widget on the bar ("bar") and a list folder of <paramref name="kind"/>'s line ("line") over a battery ("other").</summary>
    private static Config WithLine(ListKind kind)
    {
        var folder = BarKind.FolderList.New();
        folder.Id = FolderId;
        var line = kind.New();
        line.Id = "line";
        var other = ListKind.Battery.New();
        other.Id = "other";
        folder.Folder!.Put(line);
        folder.Folder.Put(other);
        var config = new Config();
        config.Layouts.Clear();
        config.Layouts.Add(new TaskbarLayout { Id = "only", Name = "Default", Islands = [new BarItem { Id = "bar", Kind = BarKind.Search }, folder] });
        return config;
    }

    /// <summary>The bar's order the dock reads the selection through: the bar's widgets, no folder's line.</summary>
    private static string[] BarOrder(Config config) =>
        [.. config.ListFor(Owner).Select(island => Config.IslandKey(Owner, island.Id))];

    private static void Gone(Config config)
    {
        Assert.Null(config.FindIsland(Owner, CoreFolders.Path(FolderId, "line")));
        // The rest of the folder, and the bar, are as they were.
        Assert.NotNull(config.FindIsland(Owner, CoreFolders.Path(FolderId, "other")));
        Assert.NotNull(config.FindIsland(Owner, "bar"));
    }

    // --- The root cause ---------------------------------------------------------------------------

    /// <summary>
    /// Regression: a line clicked in the arranged folder is selected, and Delete or its "Remove" then
    /// act on what is selected. That was read through the displays' orders, which hold no folder's
    /// line, and came to nothing: the line stayed.
    /// </summary>
    [Fact]
    public void ALineSelectedInAFolderIsWhatDeleteAndRemoveActOn()
    {
        var config = WithLine(ListKind.Clock);
        var selection = new EditSelection();
        selection.Only(LineKey("line"));

        Assert.Equal([LineKey("line")], selection.ActedOn(LineKey("line"), BarOrder(config)));
        Assert.Equal([LineKey("line")], selection.ActedOn(null, BarOrder(config)));
    }

    /// <summary>
    /// A bar's widget and a folder's line are never selected together by a click (2026-10-12,
    /// EditScope): a Ctrl+click on the other kind starts over. Where both are acted on all the same,
    /// both go: the bar's first, in its order, then the lines in the order they were selected.
    /// </summary>
    [Fact]
    public void TheBarsWidgetsAndAFoldersLinesSelectedTogetherAllGo()
    {
        var config = WithLine(ListKind.Toggles);
        var selection = new EditSelection();
        selection.Only(LineKey("line"));
        selection.Toggle(Config.IslandKey(Owner, "bar"));
        Assert.Equal([Config.IslandKey(Owner, "bar")], selection.Keys);

        selection.Set([LineKey("line"), Config.IslandKey(Owner, "bar"), LineKey("other")], []);
        var acted = selection.ActedOn(LineKey("other"), BarOrder(config));
        Assert.Equal([Config.IslandKey(Owner, "bar"), LineKey("line"), LineKey("other")], acted);
        Assert.Equal(acted, TheDock.RemoveFrom(config, acted));
        Assert.Empty(((BarItem)config.FindIsland(Owner, FolderId)!).Folder!.Cells);
        Assert.Null(config.FindIsland(Owner, "bar"));
    }

    /// <summary>"Remove" on a line not selected removes it alone, whatever else is selected.</summary>
    [Fact]
    public void RemoveOnALineNotSelectedRemovesItAlone()
    {
        var config = WithLine(ListKind.Month);
        var selection = new EditSelection();
        selection.Only(LineKey("other"));

        Assert.Equal([LineKey("line")], selection.ActedOn(LineKey("line"), BarOrder(config)));
    }

    /// <summary>
    /// A line removed alone in its list leaves the list, empty, where it stands between two with
    /// widgets (Folders.Take, as a drag out does; 2026-10-11: "there can be empty columns"); outside
    /// them the empty lists go (§328), and the last widget leaves one list.
    /// </summary>
    [Fact]
    public void TheLastLineOfAListLeavesTheListEmpty()
    {
        var config = WithLine(ListKind.Clock);
        var folder = ((BarItem)config.FindIsland(Owner, FolderId)!).Folder!;
        var alone = ListKind.Month.New();
        alone.Id = "alone";
        folder.Put(alone, 1);
        var far = ListKind.Month.New();
        far.Id = "far";
        folder.Put(far, 2);
        Assert.Equal(3, folder.Lists.Count);

        Assert.Equal([LineKey("alone")], TheDock.RemoveFrom(config, [LineKey("alone")]));
        Assert.Equal(["line", "other"], folder.Lists[0].Select(cell => cell.Island.Id));
        Assert.Empty(folder.Lists[1]);
        Assert.Equal(3, folder.Lists.Count);

        Assert.Equal([LineKey("far")], TheDock.RemoveFrom(config, [LineKey("far")]));
        Assert.Equal(["line", "other"], Assert.Single(folder.Lists).Select(cell => cell.Island.Id));

        Assert.Equal([LineKey("line"), LineKey("other")], TheDock.RemoveFrom(config, [LineKey("line"), LineKey("other")]));
        Assert.Empty(Assert.Single(folder.Lists));
    }

    // --- Every kind, every way ----------------------------------------------------------------------

    /// <summary>The line's menu while the bar is arranged has "Remove", and it takes the line out of the file — clicked first or not.</summary>
    [Theory]
    [MemberData(nameof(Kinds))]
    public void ItsMenusRemoveTakesTheLineOut(ListKind kind)
    {
        var rows = TheDock.WidgetRowsFor(WithLine(kind).FindIsland(Owner, CoreFolders.Path(FolderId, "line")));
        Assert.Contains(rows, row => row.Text == "Remove");

        // Clicked first, so selected; then "Remove" on it.
        var clicked = WithLine(kind);
        var selection = new EditSelection();
        selection.Only(LineKey("line"));
        Assert.Equal([LineKey("line")], TheDock.RemoveFrom(clicked, selection.ActedOn(LineKey("line"), BarOrder(clicked))));
        Gone(clicked);

        // Right-clicked straight away, with nothing selected.
        var straight = WithLine(kind);
        Assert.Equal([LineKey("line")], TheDock.RemoveFrom(straight, new EditSelection().ActedOn(LineKey("line"), BarOrder(straight))));
        Gone(straight);
    }

    /// <summary>Delete with the line selected and the keys on none, and with the keys on it, takes it out.</summary>
    [Theory]
    [MemberData(nameof(Kinds))]
    public void DeleteTakesTheLineOut(ListKind kind)
    {
        var selected = WithLine(kind);
        var selection = new EditSelection();
        selection.Only(LineKey("line"));
        TheDock.RemoveFrom(selected, selection.ActedOn(null, BarOrder(selected)));
        Gone(selected);

        var focused = WithLine(kind);
        TheDock.RemoveFrom(focused, new EditSelection().ActedOn(LineKey("line"), BarOrder(focused)));
        Gone(focused);
    }

    // --- The folder standing open -------------------------------------------------------------------

    private static readonly Look Dark = new(Glass: false, Dark: true, Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4));
    private static readonly POINT Anchor = new() { x = 960, y = 1040 };
    private static readonly RECT Work = new() { right = 1920, bottom = 1040 };

    /// <summary>
    /// In the arranged folder Delete says to remove the line the keys are on, or — the keys on none, a
    /// line only clicked — what is selected; the folder stays open. Once the file has lost the line the
    /// open folder has lost it too. At rest Delete removes nothing.
    /// </summary>
    [UiTheory]
    [MemberData(nameof(Kinds))]
    public void DeleteInTheArrangedFolderRemovesAndTheFolderLosesTheLine(ListKind kind)
    {
        WindowShots.OnHiddenDesktop($"folder-remove-{kind}", (gfx, instance) =>
        {
            var config = WithLine(kind);
            var settings = (BarItem)config.FindIsland(Owner, FolderId)!;
            var folder = new FolderIsland(Config.IslandKey(Owner, FolderId), settings, gfx, config);
            using var flyout = new FolderFlyout(gfx, instance, 0);
            flyout.Show(gfx, Dark, 1f, Anchor, Work, Edge.Bottom, folder, editing: true);
            Assert.NotEqual(default, flyout.ScreenOfCard(1));

            // A line clicked: the keys are on none, so it is what is selected.
            var line = FolderEditClickTests.Client(flyout.Hwnd, flyout.ScreenOfCard(0));
            _ = flyout.Message(WM.WM_LBUTTONDOWN, 1, line);
            _ = flyout.Message(WM.WM_LBUTTONUP, 0, line);
            var clicked = flyout.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)VK.VK_DELETE, 0);
            Assert.Equal((FolderFlyoutAction.Selection, EditKeyAct.Remove), (clicked.Kind, clicked.Act));
            Assert.Null(clicked.Widget);

            // The keys on the line: it, by name.
            _ = flyout.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)VK.VK_HOME, 0);
            var keyed = flyout.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)VK.VK_DELETE, 0);
            Assert.Equal((FolderFlyoutAction.Selection, EditKeyAct.Remove), (keyed.Kind, keyed.Act));
            Assert.Equal(LineKey("line"), keyed.Widget?.Key);
            Assert.True(flyout.IsOpen);

            // The dock takes it out of the file; the folder standing open has lost it.
            TheDock.RemoveFrom(config, [keyed.Widget!.Key]);
            Gone(config);
            folder.Configure(config.FindIsland(Owner, FolderId)!, config);
            Assert.DoesNotContain(folder.Tiles, tile => tile.Island.Key == LineKey("line"));
            Assert.Single(folder.Tiles);
            Assert.NotEqual(default, flyout.ScreenOfCard(0));
            Assert.Equal(default, flyout.ScreenOfCard(1));
            Assert.True(flyout.IsOpen);
            flyout.Hide();

            // At rest Delete is no removal.
            flyout.Show(gfx, Dark, 1f, Anchor, Work, Edge.Bottom, folder);
            _ = flyout.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)VK.VK_HOME, 0);
            Assert.NotEqual(FolderFlyoutAction.Selection, flyout.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)VK.VK_DELETE, 0).Kind);
            flyout.Hide();
            folder.Dispose();
        });
    }
}
