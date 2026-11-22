// What the two switchers decide, apart from any bar: what the button says, what
// the list holds, and what a row of it shows at its right.
//
// The user asked for the desktops to stop being a line of buttons: "it should
// just show the name of the current desktop, and it should have a flyout with
// all the desktops and the program icons on the right aligned... same should be
// done with a layout switcher". Both are one shape, so both are one set of rules
// (Karpo.Dock\Switchers\Switchers.cs), and the rules are pure: they are handed
// what was read rather than reading a registry or a config.
//
// The layouts' rows are deliberately not decided there. They are the bar's own
// right-click menu's rows (Dock.LayoutRows) turned into list rows, so that there
// is one answer to "how do I change layout" shown in two places, and the tests
// below hold the two together rather than describing them twice.

using Karpo.Dock;
using Karpo.Dock.QuickSettings;
// Karpo.Dock is a namespace here, so the class of that name needs saying.
using TheDock = Karpo.Dock.Dock;

// Not Karpo.Tests.Desktops: the core tests name BarKind.Desktops as "Desktops" (using static),
// which a namespace of that name would shadow.
namespace Karpo.Tests.DesktopsArea;

public sealed class SwitcherTests
{
    private static Item App(string target) =>
        new() { Target = target, Name = target, Kind = ItemKind.Shell, Keys = [target] };

    private static DesktopStrip Strip(int current, params DesktopButton[] buttons) => new(buttons, current);

    private static List<TaskbarLayout> Layouts(params string[] names) =>
        [.. names.Select(name => new TaskbarLayout { Id = name.ToLowerInvariant(), Name = name })];

    // --- What the button says ------------------------------------------------------

    /// <summary>
    /// The name of the desktop you are on, whatever Windows calls it. The buttons showed the
    /// number alone ("3") because a 1 x 1 square had room for nothing else; this one is a name.
    /// </summary>
    [Fact]
    public void TheButtonNamesTheDesktopYouAreOn()
    {
        Assert.Equal("Desktop 2", Switchers.DesktopName(["Desktop 1", "Desktop 2", "Work"], 1));
        Assert.Equal("Work", Switchers.DesktopName(["Desktop 1", "Desktop 2", "Work"], 2));
    }

    /// <summary>Nothing read yet, or an index past the end: Windows' own name for it, never blank.</summary>
    [Theory]
    [InlineData(0, "Desktop 1")]
    [InlineData(4, "Desktop 5")]
    [InlineData(-1, "Desktop 1")]
    public void AnUnknownDesktopStillHasAName(int current, string expected) =>
        Assert.Equal(expected, Switchers.DesktopName([], current));

    /// <summary>An empty name in the list is Windows' own name too: a button is never blank.</summary>
    [Fact]
    public void AnEmptyNameFallsBackToTheNumber() =>
        Assert.Equal("Desktop 2", Switchers.DesktopName(["Desktop 1", ""], 1));

    /// <summary>
    /// The room the button keeps is a fixed template and not the name: switching desktop must not
    /// move the bar under the hand that is switching. Ten characters, which is what Windows' own
    /// default names need at their widest.
    /// </summary>
    [Fact]
    public void TheRoomTheButtonKeepsIsAFixedTemplate()
    {
        Assert.Equal(10, Switchers.NameRoom.Length);
        // A default name of two figures fits it; that is what it was measured for.
        Assert.Equal(Switchers.NameRoom.Length, Switchers.Numbered(87).Length);
    }

    // --- The desktops' list --------------------------------------------------------

    /// <summary>A row per desktop, in Windows' order, with the one you are on marked.</summary>
    [Fact]
    public void ARowPerDesktopWithTheCurrentOneMarked()
    {
        var rows = Switchers.Desktops(
            Strip(1, new DesktopButton("Desktop 1", [], 0), new DesktopButton("Work", [], 0)),
            showApps: true,
            "G");

        Assert.Equal(["Desktop 1", "Work"], rows.Select(row => row.Name));
        Assert.Equal([false, true], rows.Select(row => row.Current));
        Assert.All(rows, row => Assert.Equal("G", row.Glyph));
    }

    /// <summary>
    /// The apps open on a desktop, right-aligned on its row, and "+N" for the ones past them. The
    /// count is of apps and not of windows: twelve windows of one app is one icon.
    /// </summary>
    [Fact]
    public void ARowShowsItsAppsAndCountsTheRest()
    {
        var apps = new[] { App("a"), App("b"), App("c"), App("d") };
        var rows = Switchers.Desktops(Strip(0, new DesktopButton("Desktop 1", apps, 8)), showApps: true, "G");

        var row = Assert.Single(rows);
        Assert.Equal(4, row.Apps.Count);
        Assert.Equal(8, row.More);
        Assert.Equal("+8", row.Plus);
    }

    /// <summary>A desktop with nothing open shows its name and nothing at its right.</summary>
    [Fact]
    public void AnEmptyDesktopsRowIsItsNameAlone()
    {
        var row = Assert.Single(Switchers.Desktops(Strip(0, new DesktopButton("Desktop 1", [], 0)), showApps: true, "G"));

        Assert.Empty(row.Apps);
        Assert.Equal(0, row.More);
        Assert.Equal("", row.Plus);
    }

    /// <summary>
    /// With the setting off the rows are names alone — the same as a desktop with nothing on it,
    /// and both mean "no icons to show here".
    /// </summary>
    [Fact]
    public void WithAppIconsOffTheRowsAreNamesAlone()
    {
        var rows = Switchers.Desktops(Strip(0, new DesktopButton("Desktop 1", [App("a")], 3)), showApps: false, "G");

        var row = Assert.Single(rows);
        Assert.Empty(row.Apps);
        Assert.Equal("", row.Plus);
    }

    /// <summary>A desktop Windows has not named is numbered in the list as it is on the button.</summary>
    [Fact]
    public void ARowWithoutANameIsNumbered()
    {
        var rows = Switchers.Desktops(Strip(0, new DesktopButton("", [], 0), new DesktopButton("", [], 0)), showApps: true, "G");

        Assert.Equal(["Desktop 1", "Desktop 2"], rows.Select(row => row.Name));
    }

    // --- The layouts' list ---------------------------------------------------------

    /// <summary>
    /// The layouts' rows are the bar's menu's rows: the same layouts in the same order, with the
    /// one this bar shows marked. Not a second list that has to be kept in step.
    /// </summary>
    [Fact]
    public void TheLayoutsRowsAreTheBarsOwnMenusRows()
    {
        var layouts = Layouts("Default", "Work", "Games");

        var rows = Switchers.Layouts(TheDock.LayoutRows(layouts, "work"));

        Assert.Equal(["Default", "Work", "Games"], rows.Select(row => row.Name));
        Assert.Equal([false, true, false], rows.Select(row => row.Current));
        Assert.All(rows, row => Assert.Empty(row.Apps));
    }

    /// <summary>
    /// Row i is layout i, which is what lets a chosen row be acted on by index (Dock.ChooseLayout,
    /// the menu's own act). If the menu ever reordered or filtered its rows, this fails.
    /// </summary>
    [Fact]
    public void RowOrderIsTheConfigsOrder()
    {
        var layouts = Layouts("One", "Two", "Three");

        var rows = Switchers.Layouts(TheDock.LayoutRows(layouts, "two"));

        for (var index = 0; index < layouts.Count; index++)
        {
            Assert.Equal(layouts[index].Name, rows[index].Name);
        }
    }

    /// <summary>
    /// The layouts line's rule: below two layouts there is nothing to choose, so there are no rows at all — and a list with
    /// no rows is a list nothing opens (LayoutsLine's Status says so). §561: the bar's menu shows the one layout all the same.
    /// </summary>
    [Fact]
    public void OneLayoutOffersNoRows()
    {
        Assert.Empty(Switchers.Layouts(TheDock.LayoutRows(Layouts("Default"), "default")));
        Assert.Empty(Switchers.Layouts(TheDock.LayoutRows([], "")));
        Assert.Equal(2, Switchers.Layouts(TheDock.LayoutRows(Layouts("Default", "Work"), "default")).Count);
    }

    /// <summary>A layout's own icon is its row's; one without falls back on the layouts' glyph.</summary>
    [Fact]
    public void ALayoutsIconIsItsRows()
    {
        var layouts = Layouts("Default", "Work");
        layouts[1].Icon = "";

        var rows = Switchers.Layouts(TheDock.LayoutRows(layouts, "default"));

        Assert.Equal(TaskbarLayouts.LayoutGlyph, rows[0].Glyph);
        Assert.Equal("", rows[1].Glyph);
    }

    // --- What a row's own menu offers ----------------------------------------------
    //
    // A row names a desktop, so a desktop's actions are the row's. What it may actually do is not
    // Karpo's choice: renaming and closing another desktop are calls on the shell's own interface,
    // which Karpo makes only on the Windows builds its table was checked against
    // (DesktopControl.Known). Without them the one thing left is Ctrl+Win+F4, which closes the
    // desktop you are on and nothing else.

    /// <summary>With the shell's interface, every desktop can be renamed and closed.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void WithTheShellEveryRowRenamesAndDeletes(int row)
    {
        var actions = Switchers.ActionsFor(row, current: 1, count: 3, shell: true);

        Assert.True(actions.CanRename);
        Assert.Null(actions.WhyNotRename);
        Assert.True(actions.CanDelete);
        Assert.Null(actions.WhyNotDelete);
    }

    /// <summary>
    /// Without it, nothing can be renamed — there is no shortcut for a name — and only the desktop
    /// you are on can be closed, because the shortcut closes that one. A row that cannot says why
    /// rather than going quiet.
    /// </summary>
    [Fact]
    public void WithoutTheShellOnlyTheCurrentRowDeletesAndNoneRenames()
    {
        var current = Switchers.ActionsFor(1, current: 1, count: 3, shell: false);
        var other = Switchers.ActionsFor(2, current: 1, count: 3, shell: false);

        Assert.False(current.CanRename);
        Assert.Equal(Switchers.NoShellRename, current.WhyNotRename);
        Assert.True(current.CanDelete);
        Assert.Null(current.WhyNotDelete);

        Assert.False(other.CanDelete);
        Assert.Equal(Switchers.OnlyCurrentDeletes, other.WhyNotDelete);
    }

    /// <summary>
    /// The last desktop is never deletable, with the interface or without it: Windows keeps one,
    /// and a row offering a delete that Windows refuses would be lying about what it does.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheOnlyDesktopCannotBeDeleted(bool shell)
    {
        var actions = Switchers.ActionsFor(0, current: 0, count: 1, shell);

        Assert.False(actions.CanDelete);
        Assert.Equal(Switchers.LastDesktop, actions.WhyNotDelete);
        // Renaming the only desktop is still a rename: it is the interface that decides.
        Assert.Equal(shell, actions.CanRename);
    }

    /// <summary>
    /// Moving is the shell's own call and nothing else — Windows has no shortcut for it — so
    /// without the interface a row's gripper says so rather than offering a drag that cannot happen.
    /// </summary>
    [Fact]
    public void WithoutTheShellNothingMoves()
    {
        var actions = Switchers.ActionsFor(1, current: 0, count: 3, shell: false);

        Assert.False(actions.CanMove);
        Assert.Equal(Switchers.NoShellMove, actions.WhyNotMove);
    }

    /// <summary>
    /// With it every desktop can be dragged anywhere, the ends included: a drag goes where it is let
    /// go, so there is no "already first" to say any more.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void WithTheShellEveryRowMoves(int row)
    {
        var actions = Switchers.ActionsFor(row, current: 0, count: 3, shell: true);

        Assert.True(actions.CanMove);
        Assert.Null(actions.WhyNotMove);
    }

    /// <summary>The only desktop has nowhere to go, and its gripper says Windows keeps it alone.</summary>
    [Fact]
    public void TheOnlyDesktopMovesNowhere()
    {
        var actions = Switchers.ActionsFor(0, current: 0, count: 1, shell: true);

        Assert.False(actions.CanMove);
        Assert.Equal(Switchers.LastDesktop, actions.WhyNotMove);
    }

    /// <summary>
    /// The face's right click is the switcher's own, as the switcher's was: a new desktop, the one you
    /// are on renamed or deleted, Task view. A delete Windows refuses stays, disabled, saying why; a
    /// rename that cannot be done is no row at all (the person, 2026-10-22: "if you cant rename a
    /// desktop it should also not show up as context menu action") — without the shell's call, or with
    /// no desktop Windows has told of.
    /// </summary>
    [Fact]
    public void TheFacesMenuIsTheSwitchersOwn()
    {
        var rows = TheDock.DesktopRows(Switchers.ActionsFor(0, current: 0, count: 1, shell: false));

        // One order for every desktop menu: New desktop, Task view, Rename, Delete (§308).
        Assert.Equal([S.DockMenuNewDesktop, S.DockMenuTaskView, S.DockMenuDeleteDesktop], rows.Select(row => row.Text));
        Assert.Equal(Switchers.LastDesktop, rows[2].Detail);
        Assert.False(rows[2].Enabled);

        var renames = TheDock.DesktopRows(Switchers.ActionsFor(0, current: 0, count: 1, shell: true));
        Assert.Equal([S.DockMenuNewDesktop, S.DockMenuTaskView, S.DockMenuRenameDesktop, S.DockMenuDeleteDesktop], renames.Select(row => row.Text));
        Assert.True(renames[2].Enabled);
        Assert.Null(renames[2].Detail);

        // No desktop known (Windows has said nothing yet), or one past those it knows: nothing to rename.
        Assert.DoesNotContain(TheDock.DesktopRows(Switchers.ActionsFor(0, current: 0, count: 0, shell: true)), row => row.Text == S.DockMenuRenameDesktop);
        Assert.False(Switchers.ActionsFor(-1, current: -1, count: 2, shell: true).CanRename);
        Assert.False(Switchers.ActionsFor(2, current: 0, count: 2, shell: true).CanRename);
    }

    // --- What the tooltips say -----------------------------------------------------

    /// <summary>The whole name, which the button may have had to cut, and where it stands.</summary>
    [Fact]
    public void TheDesktopTooltipCarriesTheWholeNameAndThePlace()
    {
        Assert.Equal("A very long desktop name\nDesktop 2 of 3", Switchers.DesktopTooltip("A very long desktop name", 1, 3));
        // One desktop: there is no "of" to say.
        Assert.Equal("Desktop 1", Switchers.DesktopTooltip("Desktop 1", 0, 1));
    }

    /// <summary>
    /// The layout switcher says why it opens nothing where it is the only layout: a button that
    /// answers no click has to say so somewhere, and the tooltip is the only place left.
    /// </summary>
    [Fact]
    public void TheLayoutTooltipSaysWhenThereIsNothingToChoose()
    {
        Assert.Contains("click to show another", Switchers.LayoutTooltip("Work", 3), StringComparison.Ordinal);
        Assert.Contains("only taskbar layout", Switchers.LayoutTooltip("Default", 1), StringComparison.Ordinal);
        Assert.StartsWith("Default", Switchers.LayoutTooltip("Default", 1), StringComparison.Ordinal);
    }

    // --- What the dock publishes ---------------------------------------------------

    /// <summary>
    /// The desktops the dock read, for a widget to draw. It answers whether anything changed, so a
    /// bar is built again for a switch or a rename and not for a read that found the same thing.
    /// </summary>
    [Fact]
    public void TheStateSaysWhetherAnythingChanged()
    {
        DesktopState.Set(["Desktop 1", "Desktop 2"], 0);

        Assert.False(DesktopState.Set(["Desktop 1", "Desktop 2"], 0));
        Assert.True(DesktopState.Set(["Desktop 1", "Desktop 2"], 1));
        Assert.True(DesktopState.Set(["Desktop 1", "Work"], 1));
        Assert.Equal("Work", DesktopState.Name);
        Assert.Equal(2, DesktopState.Count);

        // A PC always has a desktop, even before anything has been read.
        DesktopState.Set([], 0);
        Assert.Equal(1, DesktopState.Count);
        Assert.Equal("Desktop 1", DesktopState.Name);
    }
}
