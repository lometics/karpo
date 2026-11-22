// What a right-click on a bar shows (Karpo.Dock\Dock.Menus.cs), which is a decision and not a
// drawing, so it is tested as one rather than clicked.
//
// The split the user asked for: "edit taskbar should only appear when you are clicking outside of
// a widget". So a widget's menu holds that widget's actions and nothing about the bar, and the
// bar's menu is the only one "Edit taskbar" and "Taskbar settings" are in — two rows, because
// "the edit mode should not automatically open the settings".
//
// The half of that decision the menus can't answer for themselves is what a click lands on, which
// is Layout.HitTest's job, so the facts the split rests on are checked here too: the room between
// two sections, an empty section and the room between two widgets are the bar, and the grip band
// is the bar even over a widget — which is
// what keeps edit mode reachable on a bar too full to have any other bare point.

using Karpo.Dock;
using TerraFX.Interop.Windows;
// This namespace has a Folders and an Islands of its own, which would shadow these.
using CoreFolders = Karpo.Core.Folders;
using DockIslands = Karpo.Dock.Islands;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Menus;

public sealed class BarMenuTests
{
    /// <summary>
    /// A broken pin's flyout is what can be done about it — find it again, or take it off — and no
    /// "Settings" under them any more (§228: what it opens is its menu's row), its own pin or not. It
    /// takes the pin away in a pin's own words, "Unpin from taskbar" on the bar and "Remove from
    /// folder" in a folder; its first row names the pin, why it can't open and its target under it, in
    /// one line of detail that wraps rather than cuts (Flyout's Detail).
    /// </summary>
    [Fact]
    public void ABrokenPinsFlyoutIsWhatMendsIt()
    {
        static List<string> Texts(List<FlyoutItem> rows) => [.. rows.Where(row => row.Id != 0).Select(row => row.Text)];

        var rows = TheDock.BrokenRows("sync", "Can't be found", @"C:\gone.exe", inFolder: false);
        Assert.Equal(["Locate…", "Unpin from taskbar"], Texts(rows));
        Assert.DoesNotContain(rows, row => row.Text == S.DockMenuSettings);
        Assert.Equal(("sync", "Can't be found\nC:\\gone.exe", false), (rows[0].Text, rows[0].Detail, rows[0].Enabled));
        Assert.Equal(["Choose what to pin…", "Remove from folder"], Texts(TheDock.BrokenRows("Pin", null, "", inFolder: true)));
        Assert.Equal(TheDock.AppRows([], "Notepad", new FlyoutIcon.Glyph("x"), pinned: true, open: 0, buttons: null, Edge.Bottom, inFolder: true).Last().Text, Texts(TheDock.BrokenRows("Pin", null, "", inFolder: true))[^1]);
    }

    private const string Edit = "Edit";
    private const string Lock = "Lock the taskbar";
    private const string Settings = "Taskbar settings";

    private static RECT Area(int width, int height) => new() { left = 0, top = 0, right = width, bottom = height };

    private static PartSpec Spec(string id, BarKind kind) => new(Config.IslandKey(null, id), kind);

    private static Layout Bar(IReadOnlyList<PartSpec> specs, int height = 48, int width = 1920, bool editing = false, BarStyle? style = null)
    {
        var config = new Config { Height = height, Style = style };
        return Layout.ForTests(96, Area(width, height + 40), Edge.Bottom, config, specs, Layout.CellPx(96, config), editing);
    }

    private static string[] Texts(IEnumerable<FlyoutItem> rows) =>
        [.. rows.Where(row => !row.Separator).Select(row => row.Text)];

    // --- Where a click landed ------------------------------------------------------

    [Theory]
    [InlineData(HitKind.Panel)]
    [InlineData(HitKind.ResizeGrip)]
    public void TheBarItselfAndItsGripAreTheBar(HitKind kind) =>
        Assert.Equal(MenuPlace.Bar, TheDock.PlaceOf(new Hit(kind)));

    [Fact]
    public void NothingUnderThePointerIsTheBar() => Assert.Equal(MenuPlace.Bar, TheDock.PlaceOf(null));

    [Theory]
    [InlineData(HitKind.Island)]
    [InlineData(HitKind.Start)]
    [InlineData(HitKind.Tile)]
    [InlineData(HitKind.Tray)]
    [InlineData(HitKind.TrayOverflow)]
    [InlineData(HitKind.Search)]
    [InlineData(HitKind.TaskView)]
    [InlineData(HitKind.WindowsButton)]
    [InlineData(HitKind.ShowDesktop)]
    public void EveryOtherKindOfHitIsAWidget(HitKind kind) =>
        Assert.Equal(MenuPlace.Widget, TheDock.PlaceOf(new Hit(kind)));

    // --- The layout selector -------------------------------------------------------

    /// <summary>
    /// §561 (the person, 2026-11-08: "in the right click taskbar menu layouts should also show even if only one layout is
    /// present"; before, "the layout selector should not even show" with one): a lone layout is its row, ticked.
    /// </summary>
    [Fact]
    public void OneLayoutIsARowOfItsOwnTicked()
    {
        var only = new TaskbarLayout { Id = "a", Name = "Default" };

        var row = Assert.Single(TheDock.LayoutRows([only], only.Id));
        Assert.Equal("Default", row.Text);
        Assert.True(row.Checked);
    }

    [Fact]
    public void SeveralLayoutsAreARowEachWithTheBarsOwnTicked()
    {
        var work = new TaskbarLayout { Id = "a", Name = "Work" };
        var play = new TaskbarLayout { Id = "b", Name = "Play" };

        var rows = TheDock.LayoutRows([work, play], play.Id);

        Assert.Equal(["Work", "Play"], Texts(rows));
        Assert.Equal([false, true], rows.Select(row => row.Checked));
    }

    [Fact]
    public void TheBarsMenuKeepsItsOneLayoutInASectionOfItsOwn()
    {
        var only = new TaskbarLayout { Id = "a", Name = "Default" };

        var rows = TheDock.BarRows(TheDock.LayoutRows([only], only.Id));

        // §561: ruled off from Windows' rows (§581), as several are; §663 Lock the taskbar ruled off from Task Manager.
        Assert.Equal(["Default", "Task Manager", Lock, Settings], Texts(rows));
        Assert.Equal(2, rows.Count(row => row.Separator));
        Assert.True(rows.Single(row => row.Text == "Default").Checked);
    }

    [Fact]
    public void TheBarsMenuKeepsTheLayoutsInASectionOfTheirOwn()
    {
        var work = new TaskbarLayout { Id = "a", Name = "Work" };
        var play = new TaskbarLayout { Id = "b", Name = "Play" };

        var rows = TheDock.BarRows(TheDock.LayoutRows([work, play], work.Id));

        // §366: under no heading; §581 ruled off from Windows' rows.
        Assert.Equal(["Work", "Play", "Task Manager", Lock, Settings], Texts(rows));
        Assert.Equal(2, rows.Count(row => row.Separator));
        Assert.DoesNotContain(rows, row => row.Header);
    }

    // --- An app button's menu ------------------------------------------------------

    private static readonly FlyoutIcon AppIcon = new FlyoutIcon.Glyph("");

    private static FlyoutItem[] Jumps() =>
    [
        new(0, "Recent", Header: true),
        new(100, "notes.txt"),
        new(101, "budget.xlsx"),
    ];

    private static List<FlyoutItem> AppMenu(int open, bool pinned = true, bool pinWidget = false, bool jumps = true, Edge edge = Edge.Bottom, bool arranged = true) =>
        TheDock.AppRows(jumps ? Jumps() : [], "Notepad", AppIcon, pinned, open, TheDock.PinButtons(pinWidget, arranged), edge);

    /// <summary>The indexes of every row that closes something.</summary>
    private static int[] Closers(IReadOnlyList<FlyoutItem> rows) =>
        [.. rows.Index().Where(row => row.Item.Text.StartsWith("Close", StringComparison.Ordinal)).Select(row => row.Index)];

    /// <summary>
    /// The user's "with closest i meant it should be all the way at the bottom. so that it is the
    /// closest to reach": on a bottom bar the menu grows upward, so the last row is the one under
    /// the pointer.
    /// </summary>
    [Theory]
    [InlineData(1, "Close window")]
    [InlineData(3, "Close all windows")]
    public void ClosingComesLastOnABottomBar(int open, string text)
    {
        var rows = AppMenu(open);

        Assert.Equal(text, rows[^1].Text);
        Assert.False(rows[^1].Separator);
    }

    /// <summary>
    /// The reason is reach, not convention, so the row follows the bar: a top bar's menu grows
    /// downward and its first row is the one nearest the pointer. Written as "last" this would be
    /// wrong for anybody who moves their taskbar to the top, and wrong invisibly.
    /// </summary>
    [Fact]
    public void ClosingComesFirstOnATopBar()
    {
        var rows = AppMenu(open: 1, edge: Edge.Top);

        Assert.Equal("Close window", rows[0].Text);
        Assert.Equal([0], Closers(rows));
    }

    /// <summary>
    /// A vertical bar's menu is centred on the anchor rather than anchored at either end, so
    /// neither end is nearer and the bottom bar's answer stands.
    /// </summary>
    [Theory]
    [InlineData(Edge.Left)]
    [InlineData(Edge.Right)]
    public void AVerticalBarKeepsTheBottomBarsAnswer(Edge edge)
    {
        var rows = AppMenu(open: 1, edge: edge);

        Assert.Equal([rows.Count - 1], Closers(rows));
    }

    [Theory]
    [InlineData(Edge.Bottom, false)]
    [InlineData(Edge.Left, false)]
    [InlineData(Edge.Right, false)]
    [InlineData(Edge.Top, true)]
    public void TheNearestEndFollowsTheEdge(Edge edge, bool first) =>
        Assert.Equal(first, TheDock.NearestRowIsFirst(edge));

    /// <summary>
    /// A separator stands between the closing row and the rest, on whichever side the rest is: the
    /// row is a deliberate last stop rather than one a fast click can overshoot into.
    /// </summary>
    [Fact]
    public void ASeparatorStandsBetweenClosingAndTheRest()
    {
        Assert.True(AppMenu(open: 1)[^2].Separator);
        Assert.True(AppMenu(open: 1, edge: Edge.Top)[1].Separator);
    }

    /// <summary>
    /// "Close all windows" is the same row renamed, not a second one, so nothing that closes
    /// anything is left at the far end of the menu. Asserted as the whole set, so a second closing
    /// row appearing elsewhere cannot pass unnoticed.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public void ClosingIsOneRowAndOnlyTheLast(int open)
    {
        var rows = AppMenu(open);

        Assert.Equal([rows.Count - 1], Closers(rows));
    }

    [Fact]
    public void NothingOpenLeavesNoClosingRowAndNoSeparatorForOne()
    {
        var rows = AppMenu(open: 0);

        Assert.Empty(Closers(rows));
        Assert.False(rows[^1].Separator);
    }

    /// <summary>
    /// The jump list is Windows' and is passed through in Windows' order, above Karpo's own rows.
    /// Its own lines are never reordered by the rule above.
    /// </summary>
    [Fact]
    public void TheShellsRowsKeepTheirOrderAboveTheClosingRow()
    {
        var rows = Texts(AppMenu(open: 2, pinWidget: true));

        Assert.Equal(["Cut", "Copy", "Remove", "Recent", "notes.txt", "budget.xlsx", "Notepad", "Close all windows"], rows);
        Assert.Equal(["Recent", "notes.txt", "budget.xlsx", "Notepad", "Unpin from taskbar", "Close all windows"], Texts(AppMenu(open: 2, pinWidget: true, arranged: false)));
    }

    [Fact]
    public void AnAppWithNoJumpListIsTheSameMenuWithoutIt() =>
        Assert.Equal(
            ["Notepad", "Pin to taskbar", "Close window"],
            Texts(AppMenu(open: 1, pinned: false, jumps: false)));

    // --- Edit taskbar, and taskbar settings ----------------------------------------

    /// <summary>
    /// §581 (the person, 2026-11-09: "the default right click menu on the taskbar should be like in windows: task manager and
    /// taskbar settings (the taskbar settings should open edit mode.)"): Windows 11's two rows, in its order, with its
    /// glyphs; Taskbar settings edit mode's command (§366 to §580 "Edit"). §663 ("locking the taskbar should be shown in the
    /// default right click context menu again"): "Lock the taskbar" between them, ruled off from Task Manager as Windows 10
    /// had it, its command the dock's own, ticked while the bars are locked.
    /// </summary>
    [Fact]
    public void TheBarsMenuIsWindowsTaskManagerLockAndTaskbarSettings()
    {
        var all = TheDock.BarRows([]);
        Assert.Equal([false, true, false, false], all.Select(row => row.Separator));
        var rows = all.Where(row => !row.Separator).ToList();
        Assert.Equal(["Task Manager", Lock, Settings], Texts(rows));
        Assert.Equal((TheDock.CMD_LOCK_TASKBAR, new FlyoutIcon.Glyph(OptionGlyphs.Lock), (bool?)true), (rows[1].Id, rows[1].Icon, rows[1].Checked));
        Assert.False(TheDock.BarRows([], locked: false).Single(row => row.Text == Lock).Checked);
        Assert.Equal((TheDock.CMD_TASK_MANAGER, new FlyoutIcon.Glyph("")), (rows[0].Id, rows[0].Icon));
        Assert.Equal((TheDock.CMD_TASKBAR_SETTINGS, new FlyoutIcon.Glyph("")), (rows[2].Id, rows[2].Icon));
        Assert.DoesNotContain(rows, row => row.Text is Edit);
        Assert.DoesNotContain(rows, row => row.Primary);
    }

    /// <summary>
    /// It says "Taskbar settings" and not "Settings", the word a widget's own page would be. Only the
    /// bar's menu carries it; no widget's menu has either (§228), the tray's neither since its icons
    /// are its menu's rows (§261).
    /// </summary>
    [Fact]
    public void TheBarsSettingsRowIsNotNamedLikeAWidgetsOwn()
    {
        foreach (var widget in Enum.GetValues<BarKind>().Select(kind => Texts(TheDock.WidgetRowsFor(kind.New()))))
        {
            Assert.DoesNotContain("Settings", widget);
            Assert.DoesNotContain(Settings, widget);
        }
        Assert.DoesNotContain("Settings", Texts(TheDock.WidgetRows()));
        Assert.DoesNotContain("Settings", Texts(TheDock.BarRows([])));
    }

    /// <summary>
    /// The user's "edit taskbar should only appear when you are clicking outside of a widget".
    /// A widget's menu is that widget's actions: nothing about the bar it happens to stand on.
    /// </summary>
    [Fact]
    public void AWidgetsMenuIsItsOwnActionsAndNothingOfTheBars()
    {
        var rows = Texts(TheDock.WidgetRows());

        Assert.Equal(["Cut", "Copy", "Remove"], rows);
        Assert.DoesNotContain(Edit, rows);
        Assert.DoesNotContain(Settings, rows);
    }

    [Fact]
    public void TheDesktopsAreAWidgetToo()
    {
        Assert.DoesNotContain(Edit, Texts(TheDock.DesktopRows(Switchers.ActionsFor(0, current: 0, count: 2, shell: true))));
        Assert.DoesNotContain(Settings, Texts(TheDock.DesktopRows(Switchers.ActionsFor(0, current: 0, count: 2, shell: true))));
    }

    /// <summary>
    /// A kind with nothing of its own — Search, show desktop — has the floor's row at rest, its page
    /// in Windows, and so a menu (RestMenuFor). Only a widget the file has lost has no rows and no menu.
    /// </summary>
    [Fact]
    public void AtRestAKindWithNothingOfItsOwnHasTheFloor()
    {
        var rows = TheDock.RestMenuRows(BarKind.ShowDesktop.New(), inFolder: false).Items;

        Assert.Equal(["Taskbar settings"], Texts(rows));
        Assert.Equal(RestMenu.Widget, TheDock.RestMenuFor(MenuPlace.Widget, rows.Count));
        Assert.Empty(TheDock.RestMenuRows(null, inFolder: false).Items);
        Assert.Equal(RestMenu.None, TheDock.RestMenuFor(MenuPlace.Widget, TheDock.RestMenuRows(null, inFolder: true).Items.Count));
    }

    /// <summary>"Copy" is greyed where the copy has nowhere to go; the tray's "Settings" and "Remove" never are.</summary>
    [Fact]
    public void DuplicateIsGreyedWhereItCannotBeDone()
    {
        var rows = TheDock.WidgetRowsFor(new BarItem { Id = "t", Kind = BarKind.Tray }, copy: false);

        Assert.False(rows.Single(row => row.Text == "Copy").Enabled);
        Assert.True(rows.Single(row => row.Text == "Cut").Enabled);
        Assert.True(rows.Single(row => row.Text == "Remove").Enabled);
        Assert.False(TheDock.WidgetRowsFor(ListKind.Battery.New(), copy: false).Single(row => row.Text == "Copy").Enabled);
    }

    /// <summary>
    /// The two halves are acted on by different code, so the commands they use must not collide:
    /// this is what lets the desktops keep a menu (and light-dismiss rules) of their
    /// own while its widget's rows are routed like any other widget's.
    /// </summary>
    [Fact]
    public void AKindsOwnCommandsAreNoneOfAWidgetsCommands()
    {
        var widget = TheDock.WidgetRows().Concat(TheDock.BarRows([])).Select(row => row.Id).Where(id => id != 0).ToHashSet();
        var kinds = TheDock.DesktopRows(Switchers.ActionsFor(0, current: 0, count: 2, shell: true));

        Assert.All(kinds, row => Assert.DoesNotContain(row.Id, widget));
    }

    /// <summary>
    /// "This desktop" is the one you are on, so the rows want Windows to have told Karpo what the
    /// desktops are; and the last desktop cannot be deleted, however well known it is.
    /// </summary>
    [Fact]
    public void RenamingAndDeletingSayWhatWindowsWillDo()
    {
        // No shell interface: nothing can be renamed, so no row offers it, and only the desktop you
        // are on closes — which the widget's menu is always about, so its delete still works.
        var unknown = TheDock.DesktopRows(Switchers.ActionsFor(0, current: 0, count: 2, shell: false));
        var onLast = TheDock.DesktopRows(Switchers.ActionsFor(0, current: 0, count: 1, shell: true));

        Assert.DoesNotContain(unknown, row => row.Text == "Rename this desktop");
        Assert.True(unknown.Single(row => row.Text == "Delete this desktop").Enabled);
        Assert.False(onLast.Single(row => row.Text == "Delete this desktop").Enabled);
        Assert.True(onLast.Single(row => row.Text == "Rename this desktop").Enabled);
    }


    // --- What the split rests on: where a click lands ------------------------------

    /// <summary>
    /// The room between two sections is room, not a thing to click, so a right-click there opens
    /// the bar's menu — and so does the place of an empty section, which takes no room at rest.
    /// This is what makes "click outside a widget" reachable on a bar whose widgets run to both
    /// ends.
    /// </summary>
    [Fact]
    public void TheRoomBetweenTheSectionsIsTheBar()
    {
        var layout = Bar([Spec("c", BarKind.NightLight), Spec("w", BarKind.WidgetsBoard) with { Section = BarSection.End }]);
        var (from, to) = (layout.Parts[0].Right, layout.Parts[1].Left);

        Assert.True(to - from > 100f, "the start and the end stand apart");
        foreach (var at in new[] { from + 2f, from + (to - from) / 2f, to - 2f })
        {
            var hit = layout.HitTest(at, layout.PanelRect.Center.Y);
            Assert.Equal(HitKind.Panel, hit?.Kind);
            Assert.Equal(MenuPlace.Bar, TheDock.PlaceOf(hit));
        }
    }

    /// <summary>
    /// The gap between two widgets is drawn, but it is the widgets' to click (§160): each
    /// takes it up to its middle. The bar is the room between the sections and the grip band.
    /// </summary>
    [Fact]
    public void TheGapBetweenTwoGroupsIsTheWidgets()
    {
        var layout = Bar([Spec("c", BarKind.NightLight), Spec("w", BarKind.WidgetsBoard)], style: BarStyle.Islands);
        var (between, next) = (layout.Parts[0].Right, layout.Parts[1].Left);

        Assert.True(next > between, "an islands bar draws a gap between its widgets");
        var before = layout.HitTest(between + 0.5f, layout.PanelRect.Center.Y);
        var after = layout.HitTest(next - 0.5f, layout.PanelRect.Center.Y);

        Assert.Equal(MenuPlace.Widget, TheDock.PlaceOf(before));
        Assert.Equal(0, before is { } one ? layout.PartOf(one) : -1);
        Assert.Equal(1, after is { } two ? layout.PartOf(two) : -1);
    }

    /// <summary>
    /// A compact bar draws no gaps at all, so a full one can cover every point along itself and
    /// leave no bare spot. The grip band is the way in that does not depend on one: it runs the
    /// bar's whole length and is hit before anything under it, widget or no widget.
    /// </summary>
    [Fact]
    public void TheGripBandIsTheBarEvenOverAWidget()
    {
        var layout = Bar([Spec("c", BarKind.NightLight), Spec("w", BarKind.FolderList)], height: 30);
        var widget = layout.ItemRect(Hit.Cell(1, 0));

        // The band covers the bar end to end, so no arrangement of widgets can crowd it out.
        Assert.Equal(0f, layout.GripRect.X);
        Assert.Equal(layout.PanelRect.Width, layout.GripRect.Width);
        Assert.False(widget.IsEmpty);
        // A point over the weather widget, in the band: the bar's, not the widget's.
        var hit = layout.HitTest(widget.Center.X, layout.GripRect.Center.Y);

        Assert.Equal(HitKind.ResizeGrip, hit?.Kind);
        Assert.Equal(MenuPlace.Bar, TheDock.PlaceOf(hit));
    }

    /// <summary>A compact bar: its widgets stand a widget's gap apart (§501), no more.</summary>
    [Fact]
    public void ACompactBarDrawsOnlyAMembersGapBetweenItsWidgets()
    {
        var layout = Bar([Spec("c", BarKind.NightLight), Spec("w", BarKind.WidgetsBoard)], height: 30, style: BarStyle.Floating);

        Assert.True(layout.Connected);
        Assert.Equal(layout.WidgetGap, layout.Parts[1].Left - layout.Parts[0].Right);
    }

    // --- Which menu opens where (2026-10-04) ---------------------------------------
    //
    // "edit taskbar and taskbar settings should only appear when right clicking on the taskbar area
    // that is not a widget". The bar's own menu on the bar alone; a widget's own actions and its
    // "Settings" on a widget (2026-10-05), "Settings" alone where it has none — never the bar's, and
    // never "Edit taskbar" in a widget's, however full the bar. This reverses F1/G5, where a widget
    // with nothing of its own fell back to the bar's menu and a bar covered end to end added "Edit
    // taskbar" to a widget's.

    /// <summary>The bar's own menu opens on the bar — its room, between the sections, the grip — and has the two rows.</summary>
    [Theory]
    [InlineData(HitKind.Panel)]
    [InlineData(HitKind.ResizeGrip)]
    public void TheBarsOwnMenuOpensOnTheBarAlone(HitKind kind)
    {
        Assert.Equal(RestMenu.Bar, TheDock.RestMenuFor(TheDock.PlaceOf(new Hit(kind)), ownRows: 0));
        Assert.Equal(RestMenu.Bar, TheDock.RestMenuFor(TheDock.PlaceOf(null), ownRows: 0));
        var rows = Texts(TheDock.BarRows([]));
        Assert.Contains(Settings, rows);
        Assert.DoesNotContain(Edit, rows);
    }

    /// <summary>
    /// A right click on a widget — Search, Task view, show desktop, the tray's chevron, a cell of an
    /// island — opens the widget's own menu, the floor's row at least (RestMenuRows), and never
    /// the bar's; a menu with no rows at all, a widget the file has lost, opens nothing.
    /// </summary>
    [Theory]
    [InlineData(HitKind.Island)]
    [InlineData(HitKind.Search)]
    [InlineData(HitKind.TaskView)]
    [InlineData(HitKind.ShowDesktop)]
    [InlineData(HitKind.TrayOverflow)]
    [InlineData(HitKind.WindowsButton)]
    public void AWidgetOpensItsOwnMenuOrNone(HitKind kind)
    {
        var place = TheDock.PlaceOf(new Hit(kind));

        Assert.Equal(MenuPlace.Widget, place);
        Assert.Equal(RestMenu.None, TheDock.RestMenuFor(place, ownRows: 0));
        Assert.Equal(RestMenu.Widget, TheDock.RestMenuFor(place, ownRows: 2));
    }

    /// <summary>
    /// Search, Task view, show desktop and Settings open their own menu at rest — their page in
    /// Windows, Task view's new desktop, Settings' Windows Update (the person, 2026-10-11: "most of
    /// the right click context menus of the taskbar widgets are empty, they should not be") — not the
    /// bar's, as they did (F1/G5), and not nothing, as after (G17); no button of edit mode's (§253).
    /// </summary>
    [Theory]
    [InlineData(BarKind.Search, "Search settings")]
    [InlineData(BarKind.TaskView, "New desktop")]
    [InlineData(BarKind.ShowDesktop, "Taskbar settings")]
    [InlineData(BarKind.WindowsSettings, "Windows Update settings")]
    public void AnEntryWithNoActsOfItsOwnOpensItsPage(BarKind kind, string first)
    {
        var rows = TheDock.RestMenuRows(kind.New(), inFolder: false).Items;

        Assert.Equal(first, Texts(rows)[0]);
        Assert.Equal(RestMenu.Widget, TheDock.RestMenuFor(MenuPlace.Widget, rows.Count));
    }

    /// <summary>
    /// No widget's menu at rest has the bar's rows, whatever its kind: every taskbar entry's own
    /// rows and the desktops', looked at for the bar's commands — "Edit taskbar" and Karpo's settings
    /// among them. An entry with no page of its own in Windows has Windows' "Taskbar settings" (§308:
    /// a page is "X settings"), another command than the bar's gear, which opens Karpo's.
    /// </summary>
    [Fact]
    public void NoWidgetsMenuAtRestHasTheBarsRows()
    {
        var desktops = TheDock.DesktopRows(Switchers.ActionsFor(0, current: 0, count: 2, shell: true));
        var menus = BarEntries.All
            .Select(entry => (Name: entry.Kind.ToString(), Rows: TheDock.RestMenuRows(entry.Kind.New(), inFolder: false).Items))
            .Append(("Desktops", desktops));
        var bar = TheDock.BarRows([]).Where(row => row.Id != 0).Select(row => row.Id).ToHashSet();
        foreach (var (name, rows) in menus)
        {
            Assert.DoesNotContain(Edit, Texts(rows));
            Assert.DoesNotContain(rows, row => bar.Contains(row.Id));
        }
    }

    /// <summary>
    /// A bar its buttons cover end to end — compact, no gaps, nothing bare along it — still has the
    /// bar under the pointer: its grip band runs the whole length, over every widget, and opens the
    /// bar's menu. That is what lets a widget's menu go without "Edit taskbar" on every bar.
    /// </summary>
    [Fact]
    public void ABarCoveredEndToEndStillHasTheBarInItsGripBand()
    {
        var slot = Bar([Spec("p0", BarKind.Pin)], height: 30).Slot;
        var pins = Enumerable.Range(0, 6).Select(index => Spec($"p{index}", BarKind.Pin)).ToList();
        var layout = Bar(pins, height: 30, width: (int)slot * 6);

        foreach (var part in Enumerable.Range(0, pins.Count))
        {
            var middle = layout.PartAt(part).Left + layout.PartAt(part).Width / 2f;
            var hit = layout.HitTest(middle, layout.GripRect.Center.Y);
            Assert.Equal(HitKind.ResizeGrip, hit?.Kind);
            Assert.Equal(RestMenu.Bar, TheDock.RestMenuFor(TheDock.PlaceOf(hit), ownRows: 0));
        }
    }

    /// <summary>In edit mode nothing changes: a widget's menu is the rows arranging takes, never "Edit taskbar".</summary>
    [Fact]
    public void EditModesRowsStayAsTheyWere()
    {
        Assert.Equal(["Cut", "Copy", "Remove"], Texts(TheDock.WidgetRowsFor(new BarItem { Id = "s", Kind = BarKind.Search })));
        Assert.DoesNotContain(Edit, Texts(TheDock.WidgetRows()));
    }

    /// <summary>
    /// A widget the bar's end cuts off reads as the bar: its item rectangle does not fit, and a
    /// part with no rectangle hit-tests to the bar, whose menu it opens. The taskbar's own buttons
    /// are what an end can still cut: an app button goes behind "…" instead (AppOverflowTests).
    /// </summary>
    [Fact]
    public void TheEndCutsAWidgetBackToTheBar()
    {
        var buttons = Enumerable.Range(0, 6).Select(index => Spec($"t{index}", BarKind.TaskView)).ToList();
        // Two pixels short of where the last button ends with room to spare: it is over the end,
        // so the bar keeps its place.
        var roomy = Bar(buttons, height: 40, width: 4000);
        var layout = Bar(buttons, height: 40, width: (int)MathF.Ceiling(roomy.Parts[^1].Right) - 2);
        var last = layout.Parts[^1];

        Assert.True(last.Right > layout.PanelRect.Width);
        var hit = layout.HitTest(last.Left + last.Width / 2f, layout.PanelRect.Center.Y);
        Assert.Equal(HitKind.Panel, hit?.Kind);
        Assert.Equal(RestMenu.Bar, TheDock.RestMenuFor(TheDock.PlaceOf(hit), ownRows: 0));
    }

    // --- The same widget, wherever it is -------------------------------------------

    /// <summary>
    /// folders: a widget in a folder is no part of a bar, so a menu keyed on a bar part could
    /// never reach it. Keyed on the widget's path it is the same lookup as on the bar, and so the
    /// same menu.
    /// </summary>
    [Fact]
    public void AWidgetInAFolderIsFoundByItsPathLikeOneOnTheBar()
    {
        var config = Nest(out var folderId, out var clock);
        var owner = Config.LayoutOwner(config.Layouts[0].Id);

        Assert.Same(clock, config.FindIsland(owner, CoreFolders.Path(folderId, clock.Id)));
        Assert.Same(clock, config.FindIsland(owner, DockIslands.IdOf(Config.IslandKey(owner, CoreFolders.Path(folderId, clock.Id)))));
    }

    /// <summary>
    /// Nothing in a widget's menu is about where it stands, so the clock in a folder and the same
    /// clock on the bar get one answer, not two. This is the gap it closes: a widget in a folder
    /// right-clicked to nothing at all, and in a folder the remove badge was the only way a
    /// non-pin widget could ever be deleted.
    /// </summary>
    [Fact]
    public void AWidgetsMenuIsTheSameRowsInAFolderAsOnTheBar()
    {
        var nested = Nest(out var folderId, out var inFolder);
        var owner = Config.LayoutOwner(nested.Layouts[0].Id);
        var bare = new Config();
        bare.Layouts.Clear();
        bare.Layouts.Add(new TaskbarLayout { Id = "only", Name = "Default", Islands = [new BarItem { Id = "clock1", Kind = BarKind.NightLight }] });

        var folded = TheDock.WidgetRowsFor(nested.FindIsland(owner, CoreFolders.Path(folderId, inFolder.Id)));
        var onBar = TheDock.WidgetRowsFor(bare.FindIsland(Config.LayoutOwner("only"), "clock1"));

        Assert.Equal(["Cut", "Copy", "Remove"], Texts(folded));
        Assert.Equal(Texts(onBar), Texts(folded));
        Assert.Equal(onBar.Select(row => row.Id), folded.Select(row => row.Id));
    }

    [Fact]
    public void AWidgetTheConfigHasLostHasNoMenuAtAll() => Assert.Empty(TheDock.WidgetRowsFor(null));

    /// <summary>
    /// "Remove" in a folder takes the widget out of the folder — it does not move it to the bar.
    /// </summary>
    [Fact]
    public void RemovingAWidgetInAFolderTakesItOutOfTheFolder()
    {
        var config = Nest(out var folderId, out var clock);
        var list = config.ListFor(Config.LayoutOwner(config.Layouts[0].Id));

        Assert.Same(clock, CoreFolders.Remove(list, CoreFolders.Path(folderId, clock.Id)));

        Assert.Empty(CoreFolders.FolderIn(list, folderId)!.Cells);
        Assert.DoesNotContain(list, island => island.Kind == BarKind.NightLight);
    }

    /// <summary>A layout whose bar holds one folder, and the folder one clock.</summary>
    private static Config Nest(out string folderId, out CellItem clock)
    {
        clock = new CellItem { Id = "clock1", Kind = ListKind.Clock.Id() };
        var folder = new BarItem
        {
            Id = "folder1",
            Kind = BarKind.FolderList,
            Folder = new FolderOptions { Lists = CellItems.OneList(clock) },
        };
        var config = new Config();
        config.Layouts.Clear();
        config.Layouts.Add(new TaskbarLayout { Id = "only", Name = "Default", Islands = [folder] });
        folderId = folder.Id;
        return config;
    }
}
