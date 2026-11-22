// A section grows into the free room beside it (the person, 2026-10-08: "my center bottom dock bar
// for example shrinks its apps even though there is enough space on the left and right side"), and
// Start's place is chosen on the bottom edge's strip in edit mode. Drawn on the dock's real scene:
//
//  - section-growth-<dock|taskbar>-<before|after>-<theme>-<scale>.png: a display 1280 DIPs wide whose
//    bottom edge comes in on hover, revealed: a middle of Start and open apps, as a dock of 24
//    (nothing either side) and as a taskbar of 16 (the weather at its start, the clock at its end). Before, each
//    place in its third, as the dock had them (HoverPlaces.Third): the apps fold into "…". After,
//    sharing the edge by what each wants (HoverPlaces.Share, DockWindow.Wants): every app shows, the
//    middle centred on the display, the sides as they were.
//  - edit-start-<theme>-<scale>.png: a display in edit mode, Start at the left, the bottom strip's
//    Start button pressed and its two picture choices open over it, the note under them.
//
// To KARPO_PNG_DIR. Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using Windows.UI.Composition;
using Windows.UI.Composition.Desktop;
using static TerraFX.Interop.Windows.Windows;
using Vector2 = System.Numerics.Vector2;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{

    [UiFact]
    public void SectionGrowthSheets()
    {
        WindowShots.OnHiddenDesktop("section-growth", (gfx, _) =>
        {
            foreach (var dark in new[] { true, false })
            {
                foreach (var dpi in new uint[] { 96, 144 })
                {
                    var name = $"{(dark ? "dark" : "light")}-{dpi * 100 / 96}";
                    var look = AddListPictures.LookFor(dark) with { Animations = false };
                    var middle = new List<BarItem> { BarKind.Start.New(), BarKind.Apps.New() };
                    foreach (var island in middle)
                    {
                        island.Section = BarSection.Middle;
                    }
                    var weather = BarKind.WidgetsBoard.New();
                    weather.Section = BarSection.Start;
                    var clock = BarKind.NightLight.New();
                    clock.Section = BarSection.End;
                    foreach (var shared in new[] { false, true })
                    {
                        var when = shared ? "after" : "before";
                        var dock = Growth(gfx, dpi, look, EdgeLayout.Dock, middle, 24, shared, $"section-growth-dock-{when}-{name}.png");
                        var bar = Growth(gfx, dpi, look, EdgeLayout.Taskbar, [weather, .. middle, clock], 16, shared, $"section-growth-taskbar-{when}-{name}.png");
                        // Before, the middle's third folds its apps; after, every app shows.
                        Assert.Equal(!shared, dock.Folded);
                        Assert.Equal(!shared, bar.Folded);
                        if (shared)
                        {
                            Assert.InRange(dock.Centre - (1280f * dpi / 96f / 2f), -1f, 1f);
                        }
                    }
                }
            }
        });
    }

    /// <summary>
    /// A display 1280 DIPs by 720 at <paramref name="dpi"/> whose bottom edge comes in on hover in
    /// <paramref name="layout"/>, revealed, holding <paramref name="list"/> and <paramref name="apps"/> open apps: its places each in its
    /// third, or with <paramref name="shared"/> sharing the edge as the dock now does. Saved to
    /// <paramref name="file"/>; whether the middle folded apps into "…", and where its parts' centre is.
    /// </summary>
    private static (bool Folded, float Centre) Growth(Gfx gfx, uint dpi, Look look, EdgeLayout layout, List<BarItem> list, int apps, bool shared, string file)
    {
        Content ContentOf(List<BarItem> items, IslandCollection islands) => GrowthContent(items, islands, apps);
        var size = new Vector2(1280f, 720f) * (dpi / 96f);
        var bounds = new RectI(0, 0, (int)size.X, (int)size.Y);
        var screen = new RECT { left = 0, top = 0, right = bounds.Right, bottom = bounds.Bottom };
        var dressed = new Config { Height = 48 }.WithStyle(layout.Style());
        var wearing = HoverPlaces.Dressed(look);
        bool Filled(Edge edge, BarSection section) =>
            edge == Edge.Bottom && list.Exists(island => Sections.Of(island) == section);
        List<BarItem> ItemsOf(HoverPlace place) => place.Edge == Edge.Bottom ? Sections.Along(list).FindAll(island => Sections.Of(island) == place.Section) : [];
        var thirds = HoverPlaces.For(bounds, [], Filled, []).Where(place => place.Edge == Edge.Bottom).ToList();
        var places = thirds;
        if (shared)
        {
            var wants = PlaceWants(gfx, dpi, screen, dressed, wearing, editing: false, ContentOf,
                [.. thirds.Select(place => (place.Edge, place.Section, ItemsOf(place), HoverPlaces.Area(bounds, place)))]);
            places = [.. HoverPlaces.For(bounds, [], Filled, [], edge => wants[edge]).Where(place => place.Edge == Edge.Bottom)];
        }
        var banded = HoverPlaces.Banded(dressed.StyleOrDefault, Edge.Bottom, Filled);
        var wall = MakeWindow();
        var made = new List<(HWND Window, DesktopWindowTarget Target, Scene Scene, ContainerVisual Desktop, IslandCollection Islands)>();
        var (wallTarget, wallDesktop) = (default(DesktopWindowTarget), default(ContainerVisual));
        var folded = false;
        var centre = 0f;
        try
        {
            _ = SetWindowPos(wall, HWND.NULL, 0, 0, bounds.Right, bounds.Bottom, SWP.SWP_NOZORDER | SWP.SWP_NOACTIVATE);
            wallTarget = Target(gfx, wall);
            wallDesktop = Wallpaper(gfx, look, size, Vector2.Zero);
            wallTarget.Root = wallDesktop;
            _ = ShowWindow(wall, SW.SW_SHOWNOACTIVATE);
            var parts = new List<(HWND Window, RECT Part)> { (wall, screen) };
            foreach (var place in places)
            {
                // As the dock has it: a window for a place with something in it, and on an edge with a
                // band one for an empty place too, where it has any length (Dock.Arrange).
                if (!HoverPlaces.Wanted(place, Filled(place.Edge, place.Section), banded: banded) || place.Run.Length <= 0)
                {
                    continue;
                }
                var items = ItemsOf(place);
                var area = HoverPlaces.Area(bounds, place);
                var rect = new RECT { left = area.Left, top = area.Top, right = area.Right, bottom = area.Bottom };
                var (specs, islands) = Made(gfx, dressed, items);
                var content = ContentOf(items, islands);
                var laid = new Layout(gfx, dpi, screen, rect, Edge.Bottom, dressed, wearing, specs, content, only: place.Section);
                if (place.Section == BarSection.Middle)
                {
                    folded = laid.Overflowed.Count > 0;
                    var shown = Enumerable.Range(0, laid.Specs.Count).Where(laid.Placed).Select(laid.PartAt).ToList();
                    centre = laid.X + ((shown.Min(part => part.Left) + shown.Max(part => part.Right)) / 2f);
                }
                var window = MakeWindow();
                _ = SetWindowPos(window, HWND.NULL, laid.X, laid.Y, laid.Width, laid.Height, SWP.SWP_NOZORDER | SWP.SWP_NOACTIVATE);
                var target = Target(gfx, window);
                var scene = Scenes.Build(gfx, target, laid, content, wearing, new Icons(), shown: true);
                var desktop = Wallpaper(gfx, look, size, new Vector2(-laid.X, -laid.Y));
                var root = target.Root;
                target.Root = desktop;
                desktop.Children.InsertAtTop(root);
                made.Add((window, target, scene, desktop, islands));
                _ = ShowWindow(window, SW.SW_SHOWNOACTIVATE);
                parts.Add((window, new RECT { left = laid.X, top = laid.Y, right = laid.X + laid.Width, bottom = laid.Y + laid.Height }));
            }
            WindowShots.Settle(300);
            Assert.True(WindowShots.SaveTogether(parts, file, (0, 0, 0), margin: 0), $"{file} came back blank");
            return (folded, centre);
        }
        finally
        {
            foreach (var (window, target, scene, desktop, islands) in made)
            {
                scene.Dispose();
                target.Root = null;
                desktop.Dispose();
                foreach (var island in islands.Values)
                {
                    island.Dispose();
                }
                _ = DestroyWindow(window);
            }
            if (wallTarget is not null)
            {
                wallTarget.Root = null;
            }
            wallDesktop?.Dispose();
            _ = DestroyWindow(wall);
        }
    }

    /// <summary>What a place of the growth sheets shows: its widgets, the bar's clock, and where it holds the open apps, <paramref name="apps"/> of them.</summary>
    private static Content GrowthContent(List<BarItem> items, IslandCollection islands, int apps) => new()
    {
        Islands = islands,
        Tiles = items.Exists(island => island.Kind == BarKind.Apps) ? [.. Enumerable.Range(0, apps).Select(BusyApp)] : [],
    };

    /// <summary>
    /// What each edge's places want (<see cref="EdgeWants"/>), as the dock measures them
    /// (DockWindow.Wants): each place laid out once with what it holds (<paramref name="contentOf"/>), its natural length
    /// (<see cref="Layout.Natural"/>) and the gap it keeps; an edge none of these are on, its thirds.
    /// </summary>
    internal static Dictionary<Edge, EdgeWants> PlaceWants(Gfx gfx, uint dpi, RECT screen, Config config, Look look, bool editing, Func<List<BarItem>, IslandCollection, Content> contentOf, List<(Edge Edge, BarSection Section, List<BarItem> Items, RectI Area)> places)
    {
        var lengths = new Dictionary<(Edge, BarSection), (int Length, int Gap)>();
        foreach (var (edge, section, items, area) in places)
        {
            var (specs, islands) = Made(gfx, config, items);
            try
            {
                var rect = new RECT { left = area.Left, top = area.Top, right = area.Right, bottom = area.Bottom };
                var laid = new Layout(gfx, dpi, screen, rect, edge, config, look, specs, contentOf(items, islands), editing, section);
                lengths[(edge, section)] = (laid.Natural, laid.PlaceGap);
            }
            finally
            {
                foreach (var island in islands.Values)
                {
                    island.Dispose();
                }
            }
        }
        var wants = new Dictionary<Edge, EdgeWants>();
        foreach (var edge in ScreenEdges.All)
        {
            int? Of(BarSection section) => lengths.TryGetValue((edge, section), out var found) ? found.Length : 0;
            var gap = lengths.Where(pair => pair.Key.Item1 == edge).Select(pair => pair.Value.Gap).DefaultIfEmpty(0).Max();
            wants[edge] = lengths.Keys.Any(key => key.Item1 == edge)
                ? new EdgeWants(Of(BarSection.Start), Of(BarSection.Middle), Of(BarSection.End), gap)
                : default;
        }
        return wants;
    }
}
