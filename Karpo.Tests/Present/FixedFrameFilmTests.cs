// The transitions rule R2 fixes (docs\widgets.md §355), filmed as the dock's handler runs them
// (FrameFilm): a popup that changes its size or place while it shows — a menu shown again for rows of
// another height, a level of it opened in place, a folder spread for edit mode and back to rest, the
// edit bar grown for another scale. Its window used to move and size at once while its content followed
// at the commit: a frame of the new window round the old content. Each popup's window now stands still
// on its display while it shows, and what changes is composition, which lands whole in one commit.
//
// Tier 1 with animations off (every frame the before or the end), tier 2 (the part of the panel that
// is there before and after is in sight in every frame).

using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using Edge = Karpo.Core.Edge;
using Film = Karpo.Tests.Present.FrameFilm.Film;

namespace Karpo.Tests.Present;

public sealed class FixedFrameFilmTests
{
    private const string Owner = "layout:default";

    private static readonly Look Still = AddListPictures.LookFor(dark: true) with { Animations = false };

    private static readonly RECT Display = new() { right = 1280, bottom = 720 };

    private static readonly POINT Anchor = new() { x = 640, y = 720 };

    [UiFact]
    public void AMenuShownAgainTallerNeverShowsTheOldRowsInTheNewPanel() =>
        MenuFilm("menu-refresh-taller", Rows(5), menu => menu.Refresh(Rows(9)));

    [UiFact]
    public void AMenuShownAgainShorterNeverShowsTheOldRowsInTheNewPanel() =>
        MenuFilm("menu-refresh-shorter", Rows(9), menu => menu.Refresh(Rows(4)));

    [UiFact]
    public void ALevelOpenedInPlaceNeverShowsTheMenuInTheLevelsPanel() =>
        MenuFilm("menu-level", [.. Rows(3), new FlyoutItem(40, "More", Children: Rows(10))], menu => menu.OpenLevel(40));

    [UiFact]
    public void AFolderSpreadForEditModeNeverJumps()
    {
        WindowShots.OnHiddenDesktop("film-folder-arrange", (gfx, instance) =>
        {
            var island = Readings("arranged");
            using var folder = new FolderIsland($"{Owner}/{island.Id}", island, gfx, new Config());
            using var flyout = new FolderFlyout(gfx, instance, 0);
            flyout.Show(gfx, Still, 1f, Anchor, Display, Edge.Bottom, folder, display: Display);
            FrameFilm.Settle();
            var before = flyout.PanelOnScreen;
            var made = flyout.HostMade;
            var film = FrameFilm.Shoot("folder-arrange", gfx, Display, () => [flyout.Hwnd], flyout.Arrange);
            Assert.True(flyout.Plan is { Editing: true });
            // What changes is thin — guide lines, the buttons — so the film looks to the half per cent.
            Holds(film, before, flyout.PanelOnScreen, 0.005);
            // Its window where it was, its material the one it had (tier 3).
            Assert.Equal((made.Materials, made.Frames), (flyout.HostMade.Materials, flyout.HostMade.Frames));
        });
    }

    [UiFact]
    public void AFolderBackToRestNeverBlinks()
    {
        WindowShots.OnHiddenDesktop("film-folder-rest", (gfx, instance) =>
        {
            var island = Readings("rested");
            using var folder = new FolderIsland($"{Owner}/{island.Id}", island, gfx, new Config());
            using var flyout = new FolderFlyout(gfx, instance, 0);
            flyout.Show(gfx, Still, 1f, Anchor, Display, Edge.Bottom, folder, editing: true, display: Display);
            FrameFilm.Settle();
            var before = flyout.PanelOnScreen;
            var made = flyout.HostMade;
            var film = FrameFilm.Shoot("folder-rest", gfx, Display, () => [flyout.Hwnd], flyout.Rest);
            Assert.True(flyout.Plan is { Editing: false });
            // What changes is thin — guide lines, the buttons — so the film looks to the half per cent.
            Holds(film, before, flyout.PanelOnScreen, 0.005);
            // Its window where it was, its material the one it had (tier 3).
            Assert.Equal((made.Materials, made.Frames), (flyout.HostMade.Materials, flyout.HostMade.Frames));
        });
    }

    [UiFact]
    public void TheEditBarGrownForAnotherScaleNeverJumps()
    {
        WindowShots.OnHiddenDesktop("film-deck-wider", (gfx, instance) =>
        {
            using var deck = new EditDeck(gfx, instance, 0);
            var room = new EditDeckRoom(new RectI(0, 0, Display.right, Display.bottom), 0, 48);
            deck.Show(gfx, Still, 1f, room);
            FrameFilm.Settle();
            var before = deck.PanelOnScreen;
            var made = deck.HostMade;
            var film = FrameFilm.Shoot("deck-wider", gfx, new RECT { right = 1280, bottom = 160 }, () => [deck.Hwnd], () => deck.Show(gfx, Still, 1.25f, room));
            Assert.NotEqual(before, deck.PanelOnScreen);
            Holds(film, before, deck.PanelOnScreen);
            Assert.Equal(made, deck.HostMade);
        });
    }

    /// <summary>
    /// Tier 3: the edit bar on acrylic shown again for another subject — a folder opened, the look
    /// changed — and hidden and shown again wears the material it was made with: a new backdrop showed
    /// nothing for a frame, the edit bar see-through. Its window is placed once.
    /// </summary>
    [UiFact]
    public void TheEditBarShownAgainMakesNoMaterial()
    {
        WindowShots.OnHiddenDesktop("deck-material", (gfx, instance) =>
        {
            using var deck = new EditDeck(gfx, instance, 0);
            var glass = Still with { Glass = true };
            var room = new EditDeckRoom(new RectI(0, 0, Display.right, Display.bottom), 0, 48);
            deck.Show(gfx, glass, 1f, room);
            var made = deck.HostMade;
            Assert.Equal((1, 1), made);
            deck.Show(gfx, glass with { Dark = !glass.Dark }, 1f, room);
            deck.Hide();
            FrameFilm.Settle();
            deck.Show(gfx, glass, 1f, room with { TopDepth = 30 });
            Assert.Equal(made, deck.HostMade);
        });
    }

    // --- Helpers ---------------------------------------------------------------------------------

    private static void MenuFilm(string name, List<FlyoutItem> rows, Action<Flyout> act)
    {
        WindowShots.OnHiddenDesktop($"film-{name}", (gfx, instance) =>
        {
            using var menu = new Flyout(gfx, instance, 0);
            menu.Show(gfx, Still, 1f, rows, FlyoutLayout.List, Anchor, Display, Edge.Bottom, null);
            FrameFilm.Settle();
            var before = menu.PanelOnScreen;
            var made = menu.HostMade;
            var film = FrameFilm.Shoot(name, gfx, new RECT { left = 340, top = 0, right = 940, bottom = 720 }, () => [menu.Hwnd], () => act(menu));
            Assert.NotEqual(before.bottom - before.top, menu.PanelOnScreen.bottom - menu.PanelOnScreen.top);
            Holds(film, before, menu.PanelOnScreen);
            Assert.Equal(made, menu.HostMade);
        });
    }

    /// <summary>Tier 1 (within <paramref name="tolerance"/>), and tier 2 over what is panel before and after, a few px in from its rim.</summary>
    private static void Holds(Film film, RECT before, RECT after, double tolerance = 0.01)
    {
        film.Holds(tolerance);
        var both = new RECT
        {
            left = Math.Max(before.left, after.left) + 6,
            top = Math.Max(before.top, after.top) + 6,
            right = Math.Min(before.right, after.right) - 6,
            bottom = Math.Min(before.bottom, after.bottom) - 6,
        };
        if (both.right > both.left && both.bottom > both.top)
        {
            film.StaysInSight(0.6, both);
        }
    }

    private static List<FlyoutItem> Rows(int count) =>
        [.. Enumerable.Range(1, count).Select(index => new FlyoutItem((uint)index, $"Row {index}", new FlyoutIcon.Glyph("")))];

    /// <summary>A list folder of widgets that only read — a clock, the battery, a heading — fetching nothing, launching nothing.</summary>
    private static BarItem Readings(string id)
    {
        var folder = new FolderOptions();
        var island = BarKind.FolderList.New();
        island.Id = id;
        island.Folder = folder;
        foreach (var (line, kind) in new[] { ("clock", ListKind.Clock), ("battery", ListKind.Battery) })
        {
            var made = kind.New();
            made.Id = $"{id}-{line}";
            folder.Put(made);
        }
        var title = ListKind.Heading.New();
        title.Id = $"{id}-heading";
        title.Heading = new HeadingOptions { Text = "Notes" };
        folder.Put(title);
        return island;
    }
}
