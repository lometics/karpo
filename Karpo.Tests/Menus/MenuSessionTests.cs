// A menu's session (docs\widgets.md §356, §374; MenuSessions.cs, Flyout.Memory.cs, Dock.MenuSessions.cs): a menu
// shown again where it stood — its level, its scroll, its lit row, what was typed in its field — as the dock shows
// one again after a dialog of Windows' (a menu the person opens is at its top: MenuOpensAtTopTests); a window made
// again under it hands it on to its display's new one; another language finds its levels again by their rows' ids;
// the rows read again after a rebuild keep it where it stands, making nothing.

using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using Karpo.Tests.Present;
using TerraFX.Interop.Windows;
using Edge = Karpo.Core.Edge;

namespace Karpo.Tests.Menus;

public sealed class MenuSessionTests
{
    private static readonly Look Still = AddListPictures.LookFor(dark: true) with { Animations = false };

    private static readonly RECT Display = new() { right = 1280, bottom = 720 };

    private static readonly POINT Anchor = new() { x = 640, y = 720 };

    // --- The rule -----------------------------------------------------------------------------------

    /// <summary>A window made again under an open menu hands it on to its display's bar, else to another window of its display; none elsewhere.</summary>
    [Fact]
    public void AMenuIsHandedOnToItsDisplaysNewBar()
    {
        var gone = new Bar("A", Holds: true);
        var place = new Bar("A", Holds: false);
        var bar = new Bar("A", Holds: true);
        var other = new Bar("B", Holds: true);
        Assert.Same(bar, MenuSessions.Successor(gone, [other, place, bar], each => each.Display, each => each.Holds));
        Assert.Same(place, MenuSessions.Successor(gone, [other, place], each => each.Display, each => each.Holds));
        Assert.Null(MenuSessions.Successor(gone, [other], each => each.Display, each => each.Holds));
    }

    /// <summary>
    /// A row found again by its id where its words changed (another language; §365: a layout renamed in its own
    /// level), where only one row has it; its words still win over its id.
    /// </summary>
    [Fact]
    public void ARowIsFoundAgainByItsIdWhereItsWordsChanged()
    {
        List<FlyoutItem> now = [new(1, "Allgemein"), new(2, "Mehr"), new(0, "", Separator: true)];
        var was = new FlyoutItem(2, "More");
        Assert.Equal(1, MenuKeeps.Again(now, was));
        Assert.Equal(-1, MenuKeeps.Again([new(2, "A"), new(2, "B")], was));
        Assert.Equal(1, MenuKeeps.Again([new(3, "x"), new(7, "More"), new(2, "Mehr")], was));
        // Neither its words nor its id: gone.
        Assert.Equal(-1, MenuKeeps.Again([new(3, "x"), new(4, "Mehr")], was));
    }

    // --- The menu -----------------------------------------------------------------------------------

    /// <summary>Closed on a level, scrolled, and shown again: at its top; shown where it stood, where it was, the keys where they were.</summary>
    [UiFact]
    public void AMenuShownAgainWhereItStoodIsOnItsLevel()
    {
        WindowShots.OnHiddenDesktop("session-level", (gfx, instance) =>
        {
            using var menu = Configured(gfx, instance);
            menu.OpenLevel(40);
            menu.ScrollForTest(64);
            menu.PointTo(45);
            var left = menu.Remembered();
            Assert.NotNull(left);
            menu.Hide();
            Show(menu, gfx);
            Assert.Equal(0, menu.InlineDepth);
            menu.Recall(left!);
            Assert.Equal(1, menu.InlineDepth);
            Assert.Equal("More", menu.InlineTitle);
            Assert.Equal(64, menu.Scrolls[0]);
            // A long level's search field had the keys as it opened (Flyout.Inline.cs), and has them again.
            Assert.Equal(Flyout.InlineFilterId - 1, menu.FieldKeys);
        });
    }

    /// <summary>Words typed in a field and not kept yet, and the keys in it, are back as the menu opens where it was left.</summary>
    [UiFact]
    public void WordsTypedAndLeftAreThereAgain()
    {
        WindowShots.OnHiddenDesktop("session-field", (gfx, instance) =>
        {
            using var menu = Configured(gfx, instance);
            menu.OpenLevel(50);
            Assert.Equal(51u, menu.FieldKeys);
            foreach (var c in "Bob")
            {
                _ = menu.Message(WM.WM_CHAR, (nuint)c, default);
            }
            var left = menu.Remembered();
            menu.Hide();
            Show(menu, gfx);
            menu.Recall(left!);
            Assert.Equal(1, menu.InlineDepth);
            Assert.Equal("Bob", menu.FieldOf(51)?.Text);
            Assert.Equal(51u, menu.FieldKeys);
        });
    }

    /// <summary>
    /// Another language (§356, the person's check: "switch the language in ⚙, it stays on its level"): every
    /// row's words new, its ids the same; the menu stays on its level, found by its row's id. §365: so does a
    /// level whose row is renamed in it; one whose row is gone leaves the menu's own rows standing.
    /// </summary>
    [UiFact]
    public void AnotherLanguageKeepsTheLevel()
    {
        WindowShots.OnHiddenDesktop("session-language", (gfx, instance) =>
        {
            using var menu = Configured(gfx, instance);
            menu.OpenLevel(40);
            menu.Refresh(Items("Mehr"));
            Assert.Equal(1, menu.InlineDepth);
            Assert.Equal("Mehr", menu.InlineTitle);
            menu.Refresh(Items("Plus"));
            Assert.Equal((1, "Plus"), (menu.InlineDepth, menu.InlineTitle));
            menu.Refresh([.. Items("Plus").Where(item => item.Id != 40)]);
            Assert.Equal(0, menu.InlineDepth);
        });
    }

    /// <summary>
    /// A rebuild with the menu open (Dock.MenuAgain after an arrange): its rows read again, the same, shown
    /// again where it stands, on its level, nothing made, nothing moved in any frame.
    /// </summary>
    [UiFact]
    public void ARebuildKeepsTheMenuOpenAndInPlace()
    {
        WindowShots.OnHiddenDesktop("film-session-rebuild", (gfx, instance) =>
        {
            using var menu = Configured(gfx, instance);
            menu.OpenLevel(40);
            menu.PointTo(43);
            FrameFilm.Settle();
            var stood = menu.PanelOnScreen;
            var made = menu.Made;
            var host = menu.HostMade;
            var film = FrameFilm.Shoot("menu-rebuild", gfx, stood, () => [menu.Hwnd], () => menu.Refresh(Items("More")));
            Assert.True(menu.IsOpen);
            Assert.Equal(1, menu.InlineDepth);
            Assert.Equal(43u, menu.HotId);
            Assert.Equal(stood, menu.PanelOnScreen);
            Assert.Equal(made, menu.Made);
            Assert.Equal(host, menu.HostMade);
            Assert.Empty(film.Between());
        });
    }

    // --- Helpers ---------------------------------------------------------------------------------

    private sealed record Bar(string Display, bool Holds);

    /// <summary>A configuration menu: a switch, a level of twenty rows, a name's level.</summary>
    private static Flyout Configured(Gfx gfx, HINSTANCE instance)
    {
        var menu = new Flyout(gfx, instance, 0) { Configures = true, Configuration = true, TypeTitle = "Clock" };
        Show(menu, gfx);
        FrameFilm.Settle();
        return menu;
    }

    private static void Show(Flyout menu, Gfx gfx) =>
        menu.Show(gfx, Still, 1f, Items("More"), FlyoutLayout.List, Anchor, Display, Edge.Bottom, null);

    private static List<FlyoutItem> Items(string more) =>
    [
        new FlyoutItem(1, "Switch", Control: new FlyoutControl.Toggle(true), Stays: true),
        new FlyoutItem(40, more, Children: [.. Enumerable.Range(41, 20).Select(id => new FlyoutItem((uint)id, $"Row {id}", Checked: id == 43, Stays: true))]),
        new FlyoutItem(50, "Name", Children: [new FlyoutItem(51, "Name", Control: new FlyoutField("Ann", "Name"))]),
    ];
}
