// menus: no widget's menu is its row of buttons alone (Karpo.Dock\Dock.MenuFloor.cs; the person,
// 2026-10-11: "make sure that there are also always items that are shown and not just the row").
// Every line of a folder, every entry of the bar and every folder has a row that does something now,
// at rest — its own, else the floor's; the buttons are edit mode's alone (§253) — and every one has
// arranging's rows while the bar is arranged. None of them moves the widget (2026-10-12: "move left move right should not be
// a context menu action"). Decisions, not drawings: the drawn menus are MenuModePictures'.

using Karpo.Dock;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Menus;

public sealed class MenuFloorTests
{
    private static string[] Texts(IEnumerable<FlyoutItem> rows) =>
        [.. rows.Select(row => row.Separator ? "—" : row.Header ? "# " + row.Text : row.Text)];

    private static readonly string[] Moves = ["Move up", "Move down", "Move left", "Move right"];

    /// <summary>
    /// Every kind of line, with no widget made to read — as a drive that is not plugged in or a player
    /// with nothing playing reads — has a live row below the buttons; every row is numbered as the acts
    /// are, the rows' commands are distinct, and none is a move.
    /// </summary>
    [Fact]
    public void EveryLineOfAFolderHasRowsBelowTheButtons()
    {
        foreach (var kind in ListWidgets.All.Select(entry => entry.Kind))
        {
            var line = kind.New();
            var (items, acts) = TheDock.Floored(TheDock.FolderMenuRows(line, widget: null), line);
            Assert.True(TheDock.HasLiveRow(items), $"the {kind} line has no live row below its buttons");
            Assert.Equal(acts.Count, items.Count(item => item.Id != 0));
            Assert.Equal(acts.Select(act => act.Id).Distinct().Count(), acts.Count);
            Assert.DoesNotContain(Texts(items), Moves.Contains);
        }
    }

    /// <summary>
    /// Every entry of the bar and every face of a folder has a live row below the buttons at rest: its
    /// own, else its page in Windows, else "Edit folder". A pin's menu is its jump list and the Start
    /// button's its quick links, which always have rows.
    /// </summary>
    [Fact]
    public void EveryEntryAndEveryFolderHasRowsBelowTheButtons()
    {
        BarKind[] ownMenus = [BarKind.Pin, BarKind.Start];
        foreach (var kind in Enum.GetValues<BarKind>().Where(kind => !ownMenus.Contains(kind) && BarEntries.Find(kind) is not null))
        {
            var entry = kind.New();
            var (items, acts) = TheDock.Floored(TheDock.BarMenuRows(entry), entry);
            Assert.True(TheDock.HasLiveRow(items), $"the {kind} entry has no live row below its buttons");
            Assert.Equal(acts.Count, items.Count(item => item.Id != 0));
            Assert.DoesNotContain(Texts(items), Moves.Contains);
        }
        foreach (var face in FolderFaces.All)
        {
            var folder = new BarItem { Id = "f", Kind = BarKind.FolderList, Folder = new FolderOptions { Faces = [FolderFace.Of(face.Id)] } };
            var (items, _) = TheDock.Floored(TheDock.BarMenuRows(folder), folder);
            Assert.True(TheDock.HasLiveRow(items), $"a folder wearing the {face.Id} face has no live row below its buttons");
        }

        // A divider's style is edit mode's (§253): at rest its folder's edit.
        Assert.Equal(["Edit folder"], Texts(TheDock.Floored(TheDock.FolderMenuRows(ListKind.Divider.New(), widget: null), ListKind.Divider.New()).Items));
    }

    /// <summary>
    /// Where a line's own rows are all greyed, or it has none, its folder's "Edit folder" stands under
    /// them, after a rule; a line with a live row of its own has nothing more at rest.
    /// </summary>
    [Fact]
    public void TheFloorIsOnlyUnderAMenuWithNothingLive()
    {
        var heading = ListKind.Heading.New();
        var (headed, headActs) = TheDock.Floored(TheDock.FolderMenuRows(heading, widget: null), heading);
        Assert.Equal(["Edit folder"], Texts(headed));
        Assert.Equal(WidgetAct.EditFolder, TheDock.ActRowOf(headActs, headed[0].Id)!.Act);

        // A to-do list with nothing done: no "Clear completed" (§308), so its folder's edit alone.
        var todo = ListKind.Todo.New();
        var (items, acts) = TheDock.Floored(TheDock.FolderMenuRows(todo, widget: null), todo);
        Assert.Equal(["Edit folder"], Texts(items));
        Assert.Equal([WidgetAct.EditFolder], items.Where(item => item.Id != 0).Select(item => TheDock.ActRowOf(acts, item.Id)!.Act));

        // A weather line with no place known has nothing to open nor to read again (§308): its folder's
        // edit. A clock's live row has nothing under it.
        var weather = ListKind.WeatherNow.New();
        Assert.Equal(["Edit folder"], Texts(TheDock.Floored(TheDock.FolderMenuRows(weather, widget: null), weather).Items));
        var clock = ListKind.Clock.New();
        Assert.Equal([WindowsPages.DateTime.Title], Texts(TheDock.Floored(TheDock.FolderMenuRows(clock, widget: null), clock).Items));
    }

    /// <summary>
    /// Arranged, a widget's menu has no row of its own under its buttons, a folder's "Open folder" gone
    /// too (§253: a click opens the folder): no "Done editing", which is the button where "Edit
    /// taskbar" stands (2026-10-12), never a move, and no "Select none" on one of several selected (the
    /// person, 2026-10-19: "select none should not be a menu action"; §240) — Escape and a click on
    /// empty room clear the selection.
    /// </summary>
    [Fact]
    public void ArrangedNoMenuHasSelectNone()
    {
        Assert.DoesNotContain("SelectNone", Enum.GetNames<WidgetAct>());
        Assert.DoesNotContain("OpenFolder", Enum.GetNames<WidgetAct>());
    }

    /// <summary>A line's "Edit folder" edits the folder it stands in; a folder's on the bar, itself.</summary>
    [Fact]
    public void ALinesEditFolderIsItsFolders()
    {
        Assert.Equal("layout:main/f", TheDock.FolderKeyOf("layout:main/f/line"));
        Assert.Equal("layout:main/f", TheDock.FolderKeyOf("layout:main/f"));
    }
}
