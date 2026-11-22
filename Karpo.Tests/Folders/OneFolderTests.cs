// One folder open at a time, in edit mode as out of it (the person, 2026-10-09), and what closes it
// (Karpo.Dock\OneFolder.cs): at rest anything a popup closes for; while the bar is arranged only its
// own button clicked again, another folder opening, Escape and edit mode ending — never a press
// elsewhere or another popup. The rule as a table, and then on real folder windows on a desktop of
// their own (KARPO_UI_TESTS=1): what the folder's window does for a press elsewhere and for Escape.

using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using static Karpo.Tests.Folders.FolderEditClickTests;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Folders;

public sealed class OneFolderTests
{
    private const string Owner = "layout:default";

    private static readonly Look Dark = new(Glass: false, Dark: true, Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4));
    private static readonly POINT Anchor = new() { x = 960, y = 1040 };
    private static readonly RECT Work = new() { right = 1920, bottom = 1040 };

    [Theory]
    [InlineData(FolderCloser.OwnButton, true, true)]
    [InlineData(FolderCloser.OtherFolder, true, true)]
    [InlineData(FolderCloser.Escape, true, true)]
    [InlineData(FolderCloser.EditEnds, true, true)]
    [InlineData(FolderCloser.PressElsewhere, true, false)]
    [InlineData(FolderCloser.OtherPopup, true, false)]
    [InlineData(FolderCloser.Launch, true, true)]
    [InlineData(FolderCloser.Launch, false, true)]
    [InlineData(FolderCloser.OwnButton, false, true)]
    [InlineData(FolderCloser.OtherFolder, false, true)]
    [InlineData(FolderCloser.Escape, false, true)]
    [InlineData(FolderCloser.PressElsewhere, false, true)]
    [InlineData(FolderCloser.OtherPopup, false, true)]
    public void WhatClosesTheOpenFolder(FolderCloser what, bool arranging, bool closes) =>
        Assert.Equal(closes, OneFolder.Closes(what, arranging));

    /// <summary>
    /// Another folder opening closes the open one in both modes: one folder at a time, with no
    /// second one kept open beside it in edit mode any more (FolderPair is gone).
    /// </summary>
    [Fact]
    public void AnotherFolderClosesTheOpenOneInBothModes()
    {
        Assert.True(OneFolder.Closes(FolderCloser.OtherFolder, arranging: true));
        Assert.True(OneFolder.Closes(FolderCloser.OtherFolder, arranging: false));
        Assert.Null(typeof(Karpo.Dock.Dock).Assembly.GetType("Karpo.Dock.FolderPair"));
    }

    /// <summary>
    /// Arranged, a folder's window stays open when another window takes the focus or a press lands
    /// elsewhere (the hook's dismiss), and closes on Escape; at rest the same press closes it.
    /// </summary>
    [UiFact]
    public void ArrangedAFolderStaysThroughAPressElsewhereAndClosesOnEscape()
    {
        WindowShots.OnHiddenDesktop("one-folder-arranged", (gfx, instance) =>
        {
            var folder = new FolderIsland($"{Owner}/box", List(), gfx, new Config());
            using var flyout = new FolderFlyout(gfx, instance, 0);

            flyout.Show(gfx, Dark, 1f, Anchor, Work, Edge.Bottom, folder, editing: true);
            Assert.True(flyout.StaysOpen);
            Assert.Equal(FolderFlyoutAction.None, flyout.Message(WM.WM_ACTIVATE, WA_INACTIVE, 0).Kind);
            Assert.Equal(FolderFlyoutAction.None, flyout.Message(WM.WM_CANCELMODE, 0, 0).Kind);
            Assert.True(flyout.IsOpen, "an arranged folder closed for a press elsewhere");
            Assert.Equal(FolderFlyoutAction.Dismissed, flyout.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)VK.VK_ESCAPE, 0).Kind);
            Assert.False(flyout.IsOpen);

            flyout.Show(gfx, Dark, 1f, Anchor, Work, Edge.Bottom, folder, editing: false);
            Assert.False(flyout.StaysOpen);
            Assert.Equal(FolderFlyoutAction.Dismissed, flyout.Message(WM.WM_ACTIVATE, WA_INACTIVE, 0).Kind);
            Assert.False(flyout.IsOpen);

            // A folder at rest arranged where it stands (edit mode begun with it open) holds itself
            // open from then on, as one opened arranged does.
            flyout.Show(gfx, Dark, 1f, Anchor, Work, Edge.Bottom, folder, editing: false);
            flyout.Arrange();
            Assert.True(flyout.StaysOpen);
            Assert.Equal(FolderFlyoutAction.None, flyout.Message(WM.WM_ACTIVATE, WA_INACTIVE, 0).Kind);
            Assert.True(flyout.IsOpen);
            flyout.Hide();
            folder.Dispose();
        });
    }

    /// <summary>
    /// §348 (the person, 2026-10-25: "when closing the edit mode it should only close the edit mode but
    /// not also the folder"): the edit bar's cross ends edit mode and the arranged folder stays open, at
    /// rest where it stands (FolderFlyout.Rest) — its "+" buttons gone, its window the panel's again and
    /// no longer the display's, the panel where it stood — and from then on a press elsewhere closes it,
    /// as any folder at rest. An empty one closes instead: it never opens at rest. The dock's way: the
    /// cross alone keeps it (EndEdit's keepFolder); every other end of edit mode closes it. §487: every end the person asks for keeps it (LeaveEdit); the displays changing, the session locking or the bar going close it.
    /// </summary>
    [UiFact]
    public void AFolderKeptAsEditModeEndsRestsWhereItStands()
    {
        Assert.True(OneFolder.Rests(empty: false));
        Assert.False(OneFolder.Rests(empty: true));

        WindowShots.OnHiddenDesktop("one-folder-rest", (gfx, instance) =>
        {
            var folder = new FolderIsland($"{Owner}/box", List(), gfx, new Config());
            using var flyout = new FolderFlyout(gfx, instance, 0);
            flyout.Show(gfx, Dark, 1f, Anchor, Work, Edge.Bottom, folder, editing: true);
            Assert.True(flyout.Arranged);
            Assert.NotEqual(default, flyout.ScreenOfAdd(0));
            var panel = flyout.PanelOnScreen;
            var arranged = EditDeckClickTests.WindowOf(flyout.Hwnd);

            flyout.Rest();
            Assert.True(flyout.IsOpen);
            Assert.False(flyout.Arranged);
            Assert.False(flyout.StaysOpen);
            Assert.Equal(default, flyout.ScreenOfAdd(0));
            Assert.Equal((panel.left, panel.top), (flyout.PanelOnScreen.left, flyout.PanelOnScreen.top));
            var rest = EditDeckClickTests.WindowOf(flyout.Hwnd);
            Assert.True(rest.right - rest.left < arranged.right - arranged.left, $"its window still spans {arranged}: {rest}");
            // Twice: nothing more.
            flyout.Rest();
            Assert.True(flyout.IsOpen);
            Assert.Equal(FolderFlyoutAction.Dismissed, flyout.Message(WM.WM_ACTIVATE, WA_INACTIVE, 0).Kind);
            Assert.False(flyout.IsOpen);
            folder.Dispose();
        });

        var repo = RepoSource.Root();
        var dock = File.ReadAllText(Path.Combine(repo, "Karpo.Dock", "Dock.cs"));
        Assert.Contains("var resting = keepFolder ? openFolders.Where(open => open.Flyout.IsOpen && OneFolder.Rests(open.Flyout.Folder?.Empty ?? true)).ToList() : [];", dock);
        Assert.Contains("CloseFolder(FolderCloser.EditEnds);", dock);
        Assert.Contains("open.Flyout.Rest();", dock);
        var decks = File.ReadAllText(Path.Combine(repo, "Karpo.Dock", "Dock.EditDeck.cs"));
        // §487: every end of edit mode the person asks for (LeaveEdit: Done, Escape) keeps it, and it takes the keys.
        Assert.Contains("Report(LeaveEdit);", decks);
        Assert.Contains("EndEdit(rebuild: true, keepFolder: true);", decks);
        Assert.Contains("if (OneFolder.KeysBack(foreground == window.Hwnd || foreground == hadKeys, resting.Count > 0)", dock);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(decks + dock, @"keepFolder: true"));
    }

    /// <summary>
    /// §487 (the person, 2026-11-02: "when clicking done in edit mode it should not close the current folder that you are
    /// in"): edit mode ending with a folder kept open hands the keys to that folder, never back to the window that had
    /// them before edit mode began. Handed back, that window took the focus from the folder now at rest, which a folder
    /// at rest closes for, as any popup does ("another window taking the focus"); the stage's desktop never has the
    /// input, so its probe (ArrangedFolderStandsTests.DoneLeavesTheFolderOpenAtRest) never saw it.
    /// </summary>
    [Theory]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    public void TheKeysGoBackOnlyWhereNoFolderStaysOpen(bool editHadThem, bool folderRests, bool back) =>
        Assert.Equal(back, OneFolder.KeysBack(editHadThem, folderRests));

    /// <summary>
    /// The person, 2026-10-22: "when a folder is empty it shows empty areas where "add widgets here
    /// text appears", it should actually not be openable when it is empty in viewing mode, only in
    /// edit mode" (§279). An empty folder opens only arranged; one with a widget in it always. The
    /// dock asks before anything else opens or closes for it — a click, Enter, Space and UI
    /// Automation's invoke all come to ShowFolder.
    /// </summary>
    [UiFact]
    public void AnEmptyFolderOpensOnlyArranged()
    {
        Assert.True(OneFolder.Opens(empty: true, arranging: true));
        Assert.False(OneFolder.Opens(empty: true, arranging: false));
        Assert.True(OneFolder.Opens(empty: false, arranging: true));
        Assert.True(OneFolder.Opens(empty: false, arranging: false));

        WindowShots.OnHiddenDesktop("one-folder-empty", (gfx, instance) =>
        {
            var nothing = BarKind.FolderList.New();
            nothing.Id = "box";
            var empty = new FolderIsland($"{Owner}/box", nothing, gfx, new Config());
            var full = new FolderIsland($"{Owner}/full", List(), gfx, new Config());
            Assert.True(empty.Empty);
            Assert.False(full.Empty);
            empty.Dispose();
            full.Dispose();
        });

        var repo = RepoSource.Root();
        var folders = File.ReadAllText(Path.Combine(repo, "Karpo.Dock", "Dock.Folders.cs"));
        var show = folders[folders.IndexOf("private void ShowFolder(", StringComparison.Ordinal)..];
        var asked = show.IndexOf("if (!OneFolder.Opens(folder.Empty, arranging))", StringComparison.Ordinal);
        Assert.True(asked > 0, "ShowFolder does not ask whether the folder opens");
        Assert.True(asked < show.IndexOf("PopupOpening(window);", StringComparison.Ordinal), "something opens or closes before ShowFolder asks");
    }

    /// <summary>
    /// The person, 2026-10-22: "if a folder does not have any contents to be viewed / opened it should
    /// not have an animation when i click it" (§287). One rule for a widget's click at rest and for its
    /// press (CellIsland.ClickDoes, Dock.ClickDoes): an empty folder's click does nothing, so its press
    /// shows nothing — no give, no pressed fill — and so does brightness's sun's, the slider being the
    /// widget; a folder with something in it acts and shows its press. The dock asks it before the
    /// click is asked of the widget, and for what the press shows (DockWindow.PressShown), where the
    /// greyed widgets' rule (§247) was asked before.
    /// </summary>
    [UiFact]
    public void APressShowsOnlyWhereTheClickDoesSomething()
    {
        WindowShots.OnHiddenDesktop("one-folder-press", (gfx, instance) =>
        {
            var nothing = BarKind.FolderList.New();
            nothing.Id = "box";
            var empty = new FolderIsland($"{Owner}/box", nothing, gfx, new Config());
            var full = new FolderIsland($"{Owner}/full", List(), gfx, new Config());
            var sun = BarKind.Brightness.New();
            sun.Id = "sun";
            var brightness = Assert.IsAssignableFrom<CellIsland>(Karpo.Dock.Islands.Create($"{Owner}/sun", sun, gfx, new Config()));
            Assert.False(empty.ClickDoes(0));
            Assert.True(full.ClickDoes(0));
            Assert.False(brightness.ClickDoes(0));
            empty.Dispose();
            full.Dispose();
            brightness.Dispose();
        });

        var repo = RepoSource.Root();
        var dock = File.ReadAllText(Path.Combine(repo, "Karpo.Dock", "Dock.cs"));
        Assert.Contains("window.IslandAt(hit) is not CellIsland island || island.ClickDoes(window.Layout.Screen);", dock);
        Assert.Contains("if (window.IslandAt(hit) is not { } island || !ClickDoes(window, hit))", dock);
        Assert.Contains("private Hit? PressShown => pressed is { } down && dock.ClickDoes(this, down) ? Lit(down) : null;", dock);
        Assert.Contains("scene.Press(PressShown, true);", dock);
        Assert.Contains("scene.Highlight(lit, lit is not null && lit == PressShown);", dock);
        Assert.DoesNotContain("scene.Press(Lit(pressed), true);", dock);
        Assert.DoesNotContain("lit == pressed)", dock);
    }

    private const int WA_INACTIVE = 0;
}
