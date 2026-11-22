// The places along a display's edge (HoverPlaces) on the dock's real scene, each in a window of its
// own over a desktop, as the dock lays them out (HoverPlaces.Area, Dressed): a display 1280 DIPs wide
// with its bar at the bottom; the same layout on a display whose Position is the top, where every
// widget stands where it stood; and a display with no bar ("None"), whose entries, all along the
// bottom, stand in the three places there. (§488: the places of a Top and bottom's other bar, on the
// top and the left, went with that type.) At rest the places are out of sight and only the bar shows;
// revealed, they have slid in, floating. To KARPO_PNG_DIR as
// places-<bottom|top|none>-<rest|revealed>-<theme>-<scale>.png, dark and light, at 100 % and 150 %.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using Windows.UI.Composition;
using Windows.UI.Composition.Desktop;
using static TerraFX.Interop.Windows.Windows;
using Color = Windows.UI.Color;
using Vector2 = System.Numerics.Vector2;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    [UiFact]
    public void HoverPlaceSheets()
    {
        WindowShots.OnHiddenDesktop("hover-places", (gfx, _) =>
        {
            foreach (var dark in new[] { true, false })
            {
                foreach (var dpi in new uint[] { 96, 144 })
                {
                    var name = $"{(dark ? "dark" : "light")}-{dpi * 100 / 96}";
                    var look = AddListPictures.LookFor(dark) with { Animations = false };
                    var config = new Config { Height = 48 };
                    var bar = Sections.DefaultLayout(pins: [], inputs: NoPenNoTouch);
                    foreach (var revealed in new[] { false, true })
                    {
                        var state = revealed ? "revealed" : "rest";
                        Display(gfx, dpi, config, look, bar, Edge.Bottom, revealed, $"places-bottom-{state}-{name}.png");
                        // The Position moves no widget: only which edge is the bar.
                        Display(gfx, dpi, config, look, bar, Edge.Top, revealed, $"places-top-{state}-{name}.png");
                        Display(gfx, dpi, config, look, bar, null, revealed, $"places-none-{state}-{name}.png");
                    }
                }
            }
        });
    }

    /// <summary>
    /// A display 1280 DIPs by 720 at <paramref name="dpi"/> showing <paramref name="list"/>: its bar on
    /// <paramref name="fixedEdge"/>, or none and its places along the bottom, a window for each place that has something in it,
    /// slid in where <paramref name="revealed"/>, all over one desktop, saved to <paramref name="file"/>.
    /// </summary>
    private static void Display(Gfx gfx, uint dpi, Config config, Look look, List<BarItem> list, Edge? fixedEdge, bool revealed, string file)
    {
        var size = new Vector2(1280f, 720f) * (dpi / 96f);
        var bounds = new RectI(0, 0, (int)size.X, (int)size.Y);
        var screen = new RECT { left = 0, top = 0, right = bounds.Right, bottom = bounds.Bottom };
        // The hover edges wear a look of their own here, floating (Config.WithEdge), as a person may choose.
        var dressed = config.WithStyle(BarStyle.Floating);
        var reserve = fixedEdge is null ? 0 : (int)MathF.Ceiling(Layout.ReserveFor(dpi, config));
        var along = fixedEdge ?? Edge.Bottom;
        bool Filled(Edge edge, BarSection section) =>
            edge == along && list.Exists(island => Sections.Of(island) == section);
        var places = HoverPlaces.For(bounds, fixedEdge is { } standing ? [standing] : [], Filled, []);
        var shown = new List<(RECT Area, Edge Edge, List<BarItem> Items, Config Config, Look Look, bool Shown)>();
        if (fixedEdge is { } barEdge)
        {
            shown.Add((screen, barEdge, Sections.Along(list), config, look, true));
        }
        foreach (var place in places.Where(place => HoverPlaces.Wanted(place, Filled(place.Edge, place.Section))))
        {
            var items = place.Edge == along ? Sections.Along(list).FindAll(island => Sections.Of(island) == place.Section) : [];
            var area = HoverPlaces.Area(bounds, place);
            var rect = new RECT { left = area.Left, top = area.Top, right = area.Right, bottom = area.Bottom };
            shown.Add((rect, place.Edge, items, dressed, HoverPlaces.Dressed(look), revealed));
        }
        var wall = MakeWindow();
        var made = new List<(HWND Window, DesktopWindowTarget Target, Scene Scene, ContainerVisual Desktop, IslandCollection Islands)>();
        var (wallTarget, wallDesktop) = (default(DesktopWindowTarget), default(ContainerVisual));
        try
        {
            _ = SetWindowPos(wall, HWND.NULL, 0, 0, bounds.Right, bounds.Bottom, SWP.SWP_NOZORDER | SWP.SWP_NOACTIVATE);
            wallTarget = Target(gfx, wall);
            wallDesktop = Wallpaper(gfx, look, size, Vector2.Zero);
            wallTarget.Root = wallDesktop;
            _ = ShowWindow(wall, SW.SW_SHOWNOACTIVATE);
            var parts = new List<(HWND Window, RECT Part)> { (wall, screen) };
            foreach (var (area, edge, items, dressing, wearing, visible) in shown)
            {
                var (specs, islands) = Made(gfx, dressing, items);
                var content = new Content { Islands = islands, Tiles = items.Exists(island => island.Kind == BarKind.Apps) ? [.. Enumerable.Range(0, 3).Select(BusyApp)] : [] };
                var layout = new Layout(gfx, dpi, screen, area, edge, dressing, wearing, specs, content);
                // The strip a place keeps out of the edges either side is what the bar's layout holds.
                Assert.Equal(dressing == config ? reserve : (int)MathF.Ceiling(Layout.ReserveFor(dpi, dressed)), (int)MathF.Ceiling(layout.Reserve));
                Assert.Equal(dressing != config, layout.Floating);
                var window = MakeWindow();
                _ = SetWindowPos(window, HWND.NULL, layout.X, layout.Y, layout.Width, layout.Height, SWP.SWP_NOZORDER | SWP.SWP_NOACTIVATE);
                var target = Target(gfx, window);
                var scene = Scenes.Build(gfx, target, layout, content, wearing, new Icons(), shown: visible);
                var desktop = Wallpaper(gfx, look, size, new Vector2(-layout.X, -layout.Y));
                var root = target.Root;
                target.Root = desktop;
                desktop.Children.InsertAtTop(root);
                made.Add((window, target, scene, desktop, islands));
                _ = ShowWindow(window, SW.SW_SHOWNOACTIVATE);
                parts.Add((window, new RECT { left = layout.X, top = layout.Y, right = layout.X + layout.Width, bottom = layout.Y + layout.Height }));
            }
            WindowShots.Settle(300);
            Assert.True(WindowShots.SaveTogether(parts, file, (0, 0, 0), margin: 0), $"{file} came back blank");
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

    /// <summary>
    /// The tests' desktop, <paramref name="size"/> px, as a window at <paramref name="offset"/> from
    /// the display's corner sees it: a wash from one corner of the display to the other, so the
    /// windows over it join into one picture.
    /// </summary>
    private static ContainerVisual Wallpaper(Gfx gfx, Look look, Vector2 size, Vector2 offset)
    {
        var c = gfx.Compositor;
        var paper = c.CreateSpriteVisual();
        paper.Size = size;
        paper.Offset = new System.Numerics.Vector3(offset, 0f);
        paper.Brush = Wash(gfx, look, size, Vector2.Zero);
        var desktop = c.CreateContainerVisual();
        desktop.Size = size;
        desktop.Children.InsertAtBottom(paper);
        return desktop;
    }

    /// <summary>
    /// The tests' desktop's wash across a display <paramref name="size"/> px, for something whose corner stands at
    /// <paramref name="corner"/> on it: §651 what an attached folder's ground stands on in a picture (FolderFlyout.Paper), as the
    /// bars over it see it.
    /// </summary>
    private static CompositionLinearGradientBrush Wash(Gfx gfx, Look look, Vector2 size, Vector2 corner)
    {
        var c = gfx.Compositor;
        var wash = c.CreateLinearGradientBrush();
        wash.MappingMode = CompositionMappingMode.Absolute;
        wash.StartPoint = -corner;
        wash.EndPoint = size - corner;
        var (from, to) = look.Dark
            ? (Color.FromArgb(255, 0x0B, 0x1E, 0x46), Color.FromArgb(255, 0x2A, 0x5C, 0xB0))
            : (Color.FromArgb(255, 0x8F, 0xBC, 0xEE), Color.FromArgb(255, 0xE6, 0xEF, 0xFA));
        wash.ColorStops.Add(c.CreateColorGradientStop(0f, from));
        wash.ColorStops.Add(c.CreateColorGradientStop(1f, to));
        return wash;
    }
}
