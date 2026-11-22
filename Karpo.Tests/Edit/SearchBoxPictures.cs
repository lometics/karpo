// search: Windows' search button on the dock's real scene (backlog 34), as Windows 11's search box
// and as its icon, shown on a desktop of its own and captured as the compositor draws it
// (WindowShots), to KARPO_PNG_DIR as search-box-<what>-<look>-<bar>-<scale>.png: the box at rest,
// under the pointer and pressed — Windows' three looks of it, its own fill and edge in each
// (SearchPill.cs) —, with the keyboard's ring round it, and the icon, on the 48- and 40-DIP bars,
// and the box at rest on a 64-DIP bar; dark, light and high contrast. Its corners are a control's
// 4 DIPs (SearchPill.Radius), not Windows' pill.
// Start and Task view stand either side, as on Windows' taskbar. Every part is measured by the
// real Layout.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using System.Numerics;
using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using Windows.UI.Composition;
using Windows.UI.Composition.Desktop;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    /// <summary>Start, Search as the box or the icon, Task view, in the middle.</summary>
    private static List<PartSpec> SearchBar(bool box) =>
    [
        Spec("start", BarKind.Start) with { Section = BarSection.Middle },
        Spec("search", BarKind.Search) with { Box = box, Section = BarSection.Middle },
        Spec("taskview", BarKind.TaskView) with { Section = BarSection.Middle },
    ];

    [UiFact]
    public void SearchBoxSheets()
    {
        WindowShots.OnHiddenDesktop("search-box", (gfx, _) =>
        {
            foreach (var theme in new[] { "dark", "light", "contrast" })
            {
                foreach (var dpi in new uint[] { 96, 144 })
                {
                    foreach (var height in new[] { 48, 40, 64, 32 })
                    {
                        var look = MarkLook(theme);
                        var config = new Config { Height = height };
                        var area = new RECT { left = 0, top = 0, right = (int)(560 * dpi / 96), bottom = (int)(dpi * 10) };
                        var name = $"{theme}-{height}-{dpi * 100 / 96}";
                        var box = new Layout(gfx, dpi, area, area, Edge.Bottom, config, look, SearchBar(box: true), new Content());
                        Assert.True(box.Boxed(box.PartOf(Hit.Search)), $"the {height}-DIP bar shows no box");
                        Lit(gfx, box, look, null, $"search-box-rest-{name}.png");
                        if (height is 48 or 40 or 32)
                        {
                            Lit(gfx, box, look, Hit.Search, $"search-box-hover-{name}.png");
                            Lit(gfx, box, look, Hit.Search, $"search-box-pressed-{name}.png", pressed: true);
                            Lit(gfx, box, look, null, $"search-box-focus-{name}.png", focus: Hit.Search);
                        }
                        if (height == 48)
                        {
                            var icon = new Layout(gfx, dpi, area, area, Edge.Bottom, config, look, SearchBar(box: false), new Content());
                            Lit(gfx, icon, look, null, $"search-box-icon-{name}.png");
                        }
                    }
                }
            }
        });
    }

    /// <summary>
    /// search: Windows' search button shown as the box lays its fill and edge as a plate of its own,
    /// the pill's size, under the words: that is what changes as the pointer comes and the box is
    /// pressed, as Windows' does, rather than a shade under it. And the box does not shrink under a
    /// press, as a button's glyph does; Windows' does not.
    /// </summary>
    [UiFact]
    public void TheSearchBoxLaysItsOwnPlateAndDoesNotShrink()
    {
        OnABar(48, SearchBar(box: true), (layout, scene) =>
        {
            var part = layout.PartOf(Hit.Search);
            Assert.True(layout.Boxed(part));
            var plate = scene.Registered($"search-plate:{layout.Specs[part].Key}");
            Assert.NotNull(plate);
            var pill = layout.ItemRect(Hit.Search);
            Assert.Equal(new Vector2(pill.Width, pill.Height), plate!.Size);

            scene.Highlight(Hit.Search, pressed: true);
            scene.Press(Hit.Search, down: true);
            Assert.Equal(Vector3.One, scene.Registered("search")!.Scale);
            scene.Press(null, down: false);
        });
    }

    /// <summary>
    /// search: the box's plate, its highlight under the pointer and the keyboard's ring round it all
    /// take the box's corners, not the pill's half-height — on every bar that shows the box, in both
    /// looks: on an islands bar the card's (the box is the part's card, Layout.SearchBoxed), on a
    /// floating one a control's 4 DIPs (SearchPill.Radius).
    /// </summary>
    [UiTheory]
    [InlineData(32, BarStyle.Islands)]
    [InlineData(40, BarStyle.Islands)]
    [InlineData(48, BarStyle.Islands)]
    [InlineData(64, BarStyle.Islands)]
    [InlineData(48, BarStyle.Floating)]
    public void TheSearchBoxHasItsSlotsCorners(int height, BarStyle style)
    {
        OnABar(height, SearchBar(box: true), (layout, scene) =>
        {
            var part = layout.PartOf(Hit.Search);
            Assert.True(layout.Boxed(part));
            var radius = layout.SearchBoxRadius;
            Assert.Equal(style == BarStyle.Islands ? layout.CardRadius : SearchPill.Radius(layout.CellSide), radius);
            Assert.True(radius < layout.ItemRect(Hit.Search).Height / 4f, "the box is not a pill");
            var plate = Assert.IsType<ShapeVisual>(scene.Registered($"search-plate:{layout.Specs[part].Key}"));
            var shape = Assert.IsType<CompositionSpriteShape>(plate.Shapes[0]);
            Assert.Equal(radius, Assert.IsType<CompositionRoundedRectangleGeometry>(shape.Geometry).CornerRadius.X);
            scene.Highlight(Hit.Search, pressed: false);
            scene.KeyFocus(Hit.Search);
            Assert.Equal((radius, radius), scene.Corners);
        }, editing: false, style: style);
    }

    /// <summary>
    /// search: a search box fills its whole slot, the card's height and the part's length, and wears no
    /// card under it, so nothing shows round it (the person, 2026-10-11: "there is a faint edge on the
    /// top and bottom and left and right"). The box kept 2 DIPs in along the bar and 3 across, and the
    /// part's card showed in that room as a second, fainter edge. Seen in the pixels: just inside each
    /// of the slot's four edges, along its straight run, is the box's own fill, the same as deep inside
    /// it; where the card showed, those rows and columns were the card's colour. Windows' search button,
    /// at rest and under the pointer, dark and light, on a bottom and a top bar,
    /// at 100 % and 150 %, on the 40-, 48- and 64-DIP bars.
    /// </summary>
    [UiFact]
    public void TheSearchBoxCoversItsSlot()
    {
        WindowShots.OnHiddenDesktop("search-slot", (gfx, _) =>
        {
            var bar = new List<BarItem> { BarKind.Start.New(), BarKind.Search.New(), BarKind.TaskView.New() };
            bar[1].WindowsSearch = new WindowsSearchOptions { ShowBox = true };
            for (var at = 0; at < bar.Count; at++)
            {
                bar[at].Id = $"slot-{at}";
                bar[at].Section = BarSection.Middle;
            }
            foreach (var theme in new[] { "dark", "light" })
            {
                foreach (var dpi in new uint[] { 96, 144 })
                {
                    foreach (var height in new[] { 32, 40, 48, 64 })
                    {
                        foreach (var edge in new[] { Edge.Bottom, Edge.Top })
                        {
                            var look = MarkLook(theme);
                            var config = new Config { Height = height };
                            var area = new RECT { left = 0, top = 0, right = (int)(900 * dpi / 96), bottom = (int)(dpi * 10) };
                            var (specs, islands) = Made(gfx, config, bar);
                            try
                            {
                                var content = new Content { Islands = islands };
                                var layout = new Layout(gfx, dpi, area, area, edge, config, look, specs, content);
                                var windows = layout.PartOf(Hit.Search);
                                Assert.True(layout.SearchBoxed(windows), $"no box on the {height}-DIP bar");
                                foreach (var (hover, state) in new (Hit?, string)[] { (null, "rest"), (Hit.Search, "hover") })
                                {
                                    var name = $"search-slot-{theme}-{height}-{edge.ToString().ToLowerInvariant()}-{dpi * 100 / 96}-{state}.png";
                                    Lit(gfx, layout, look, hover, name, content: content, seen: (pixels, width) => CoversItsSlot(layout, windows, pixels, width, name));
                                }
                            }
                            finally
                            {
                                foreach (var island in islands.Values)
                                {
                                    island.Dispose();
                                }
                            }
                        }
                    }
                }
            }
        });
    }

    /// <summary>
    /// Part <paramref name="part"/>'s box fills its slot in <paramref name="pixels"/> (the panel's,
    /// <paramref name="width"/> px a row): the rows and columns just inside its edge, along its
    /// straight run, are the colour deep inside it, between its magnifier and its words.
    /// </summary>
    private static void CoversItsSlot(Layout layout, int part, byte[] pixels, int width, string name)
    {
        var panel = layout.PanelRect;
        var slot = layout.BlockRect(part);
        Assert.Equal(slot, layout.SearchBoxRect(Hit.Search.In(part)));
        // The first and last whole px inside the slot: on a bar whose cell is not whole px its edge
        // falls between two rows, which both take some of it.
        var (left, top) = ((int)MathF.Ceiling(slot.X - panel.X), (int)MathF.Ceiling(slot.Y - panel.Y));
        var (right, bottom) = ((int)MathF.Floor(slot.Right - panel.X) - 1, (int)MathF.Floor(slot.Bottom - panel.Y) - 1);
        var corner = (int)MathF.Ceiling(layout.SearchBoxRadius) + 1;
        // Past the box's edge, which the compositor smears over two px as it does every card's: two px
        // further in. The old box stood 3 DIPs in across the bar, so there it showed the card or its own edge.
        var line = (int)layout.Stroke + 2;
        // A square box (a flush bar's) has no corner to skip, but its side edges still run down the
        // ends of the rows checked along the top and the bottom.
        corner = Math.Max(corner, line + 1);
        int At(int x, int y) => ((y * width) + x) * 4;
        // Deep inside: the middle row, between the magnifier (27 DIPs in) and the words (37), where neither is; the box
        // is as long as its words since §510, so there is no room after them to look at.
        var between = (int)MathF.Round((SearchPill.EndDip + SearchPill.GlyphDip + SearchPill.TextLeftDip) / 2f * layout.CellSide / SearchPill.BaseCellDip);
        var deep = At(left + between, (top + bottom) / 2);
        void Same(int x, int y, string where)
        {
            var at = At(x, y);
            for (var channel = 0; channel < 3; channel++)
            {
                Assert.True(Math.Abs(pixels[at + channel] - pixels[deep + channel]) <= 1,
                    $"{name}: part {part}'s {where} at ({x}, {y}) of its slot {slot} on the panel {panel} is ({pixels[at + 2]}, {pixels[at + 1]}, {pixels[at]}), the box's fill ({pixels[deep + 2]}, {pixels[deep + 1]}, {pixels[deep]})");
            }
        }
        for (var x = left + corner; x <= right - corner; x++)
        {
            Same(x, top + line, "top");
            Same(x, bottom - line, "bottom");
        }
        for (var y = top + corner; y <= bottom - corner; y++)
        {
            Same(left + line, y, "left");
            Same(right - line, y, "right");
        }
    }

    /// <summary>
    /// §510: in the pixels, the room from the box's start to the magnifier's first inked column is the room from the
    /// words' last inked column to the box's end, within a px (the person, 2026-11-05: "the windows search box somehow
    /// has a different spacing left and right"). The box was Windows' 220 DIPs whatever its words, so after "Search"
    /// stood some 140 DIPs of empty box. At 100, 125, 150, 175 and 200 %, on the 40-, 48- and 64-DIP bars, dark and
    /// light, at rest and under the pointer, to KARPO_PNG_DIR as search-ends-&lt;look&gt;-&lt;bar&gt;-&lt;scale&gt;-&lt;state&gt;.png.
    /// </summary>
    [UiFact]
    public void TheSearchBoxKeepsTheSameRoomEitherSide()
    {
        WindowShots.OnHiddenDesktop("search-ends", (gfx, _) =>
        {
            foreach (var theme in new[] { "dark", "light" })
            {
                foreach (var dpi in new uint[] { 96, 120, 144, 168, 192 })
                {
                    foreach (var height in new[] { 40, 48, 64 })
                    {
                        var look = MarkLook(theme);
                        var config = new Config { Height = height };
                        var area = new RECT { left = 0, top = 0, right = (int)(560 * dpi / 96), bottom = (int)(dpi * 10) };
                        var layout = new Layout(gfx, dpi, area, area, Edge.Bottom, config, look, SearchBar(box: true), new Content());
                        var part = layout.PartOf(Hit.Search);
                        Assert.True(layout.SearchBoxed(part), $"no box on the {height}-DIP bar at {dpi} dpi");
                        foreach (var (hover, state) in new (Hit?, string)[] { (null, "rest"), (Hit.Search, "hover") })
                        {
                            var name = $"search-ends-{theme}-{height}-{dpi * 100 / 96}-{state}.png";
                            Lit(gfx, layout, look, hover, name, seen: (pixels, width) => SameRoomEitherSide(layout, part, pixels, width, name));
                        }
                    }
                }
            }
        });
    }

    /// <summary>
    /// Part <paramref name="part"/>'s box in <paramref name="pixels"/> (the panel's, <paramref name="width"/> px a row) has as
    /// much room before its first inked column (the magnifier) as after its last (the words), within a px. Inked is unlike
    /// the box's own fill (a row near its top, where neither is) along the box's middle rows, its edge and corners left out.
    /// </summary>
    private static void SameRoomEitherSide(Layout layout, int part, byte[] pixels, int width, string name)
    {
        var panel = layout.PanelRect;
        var slot = layout.SearchBoxRect(Hit.Search.In(part));
        var (left, top) = ((int)MathF.Ceiling(slot.X - panel.X), (int)MathF.Ceiling(slot.Y - panel.Y));
        var (right, bottom) = ((int)MathF.Floor(slot.Right - panel.X) - 1, (int)MathF.Floor(slot.Bottom - panel.Y) - 1);
        var line = (int)layout.Stroke + 2;
        var corner = (int)MathF.Ceiling(layout.SearchBoxRadius) + 1;
        int At(int x, int y) => ((y * width) + x) * 4;
        bool Inked(int x)
        {
            for (var y = top + corner; y <= bottom - corner; y++)
            {
                var fill = At(x, top + line);
                var at = At(x, y);
                var off = 0;
                for (var channel = 0; channel < 3; channel++)
                {
                    off += Math.Abs(pixels[at + channel] - pixels[fill + channel]);
                }
                if (off > 48)
                {
                    return true;
                }
            }
            return false;
        }
        var first = -1;
        var last = -1;
        for (var x = left + line; x <= right - line; x++)
        {
            if (Inked(x))
            {
                first = first < 0 ? x : first;
                last = x;
            }
        }
        Assert.True(first >= 0, $"{name}: nothing drawn in the box {slot}");
        var before = first - (slot.X - panel.X);
        var after = (slot.Right - panel.X) - (last + 1);
        Assert.True(MathF.Abs(after - before) <= 1f,
            $"{name}: the box {slot} has {before} px before its magnifier (column {first}) and {after} px after its words (column {last})");
    }

    /// <summary>The icon alone has no plate: it lights up as a button does.</summary>
    [UiFact]
    public void TheSearchIconHasNoPlate()
    {
        OnABar(48, SearchBar(box: false), (layout, scene) =>
            Assert.Null(scene.Registered($"search-plate:{layout.Specs[layout.PartOf(Hit.Search)].Key}")));
    }
    /// <summary>The scene of <paramref name="layout"/> (arranged where it was laid out so) with <paramref name="hover"/> lit (and pressed), its bar saved to <paramref name="file"/>.</summary>
    private static void Lit(Gfx gfx, Layout layout, Look look, Hit? hover, string file, bool pressed = false, Hit? focus = null, Content? content = null, Action<byte[], int>? seen = null)
    {
        var window = MakeWindow();
        DesktopWindowTarget? target = null;
        Scene? scene = null;
        try
        {
            _ = SetWindowPos(window, HWND.NULL, 0, 0, (int)layout.Width, (int)layout.Height, SWP.SWP_NOZORDER | SWP.SWP_NOACTIVATE);
            target = Target(gfx, window);
            scene = Scenes.Build(gfx, target, layout, content ?? new Content(), look, new Icons(), shown: true, edit: layout.Editing ? new EditView() : null);
            scene.Highlight(hover, pressed);
            scene.KeyFocus(focus);
            _ = ShowWindow(window, SW.SW_SHOWNOACTIVATE);
            WindowShots.Settle(250);
            var panel = layout.PanelRect;
            var part = new RECT { left = (int)panel.X, top = (int)panel.Y, right = (int)panel.Right, bottom = (int)panel.Bottom };
            Assert.True(WindowShots.Save(window, file, part, seen), $"{file} came back blank");
        }
        finally
        {
            scene?.Dispose();
            _ = DestroyWindow(window);
        }
    }
}
