// A widget's target reaches past what it draws (the person, 2026-10-13: "the widgets on the taskbar
// have a little padding to the taskbar, i want to keep that padding, but the clickable area of the
// widget should actually go all the way out to the taskbar and also to the next widget of course").
//
// Fitts, as Windows 11's taskbar has it: the drawn widgets keep their inset and their gaps, and the
// pointer takes the widget across the bar's whole thickness out to the screen's edge, along the gap to
// its neighbour up to the gap's middle, and at the bar's ends to the end (Layout.Reach). The room a
// spacer grows to between two sections stays the bar's (its menu, the edit lasso), and so does the
// desktop between two floating docks. Held on bars laid out the dock's way (PartSpec.For), islands and floating, on all four
// edges, at 100 % and 150 %, and on both rows of a dock of two; hit-area pictures to KARPO_PNG_DIR
// with KARPO_UI_TESTS=1.

using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Sizing;

public sealed class WidgetReachTests
{
    private static BarItem In(BarKind kind, BarSection section)
    {
        var island = kind.New();
        island.Section = section;
        return island;
    }

    /// <summary>
    /// Start, search, the open apps and a widget at the start; two widgets in the middle; the
    /// tray and show desktop at the end.
    /// </summary>
    private static List<PartSpec> Specs()
    {
        List<BarItem> islands =
        [
            In(BarKind.Start, BarSection.Start),
            In(BarKind.Search, BarSection.Start),
            In(BarKind.Apps, BarSection.Start),
            In(BarKind.WidgetsBoard, BarSection.Start),
            In(BarKind.NightLight, BarSection.Middle),
            In(BarKind.Volume, BarSection.Middle),
            In(BarKind.Tray, BarSection.End),
            In(BarKind.ShowDesktop, BarSection.End),
        ];
        Sections.Order(islands);
        return [.. islands.Select((island, index) => PartSpec.For(Config.IslandKey(null, $"p{index}"), island))];
    }

    private static Config ConfigFor(BarStyle style) =>
        new() { Height = 48, Style = style };

    private static RECT Display(uint dpi) => new() { left = 0, top = 0, right = 1600 * (int)dpi / 96, bottom = 900 * (int)dpi / 96 };

    private static Layout Bar(BarStyle style, Edge edge, uint dpi, bool editing = false)
    {
        var config = ConfigFor(style);
        return Layout.ForTests(dpi, Display(dpi), edge, config, Specs(), Layout.CellPx(dpi, config), editing, tray: 3, trayOverflow: true, tiles: 3);
    }

    public static TheoryData<BarStyle, Edge, uint> Bars()
    {
        var data = new TheoryData<BarStyle, Edge, uint>();
        foreach (var style in new[] { BarStyle.Islands, BarStyle.Floating })
        {
            foreach (var edge in new[] { Edge.Bottom, Edge.Top, Edge.Left, Edge.Right })
            {
                foreach (var dpi in new uint[] { 96, 144 })
                {
                    data.Add(style, edge, dpi);
                }
            }
        }
        return data;
    }

    // --- Probing --------------------------------------------------------------------------------

    /// <summary>A window point <paramref name="along"/> the bar and <paramref name="across"/> it from the bar's outer side (the screen's edge).</summary>
    private static (float X, float Y) At(Layout layout, float along, float across) => layout.Edge switch
    {
        Edge.Top => (along, across),
        Edge.Left => (across, along),
        Edge.Right => (layout.Width - 1 - across, along),
        _ => (along, layout.Height - 1 - across),
    };

    /// <summary>How deep the bar is across, from the screen's edge to its inner side: the room it floats over and its thickness.</summary>
    private static int Depth(Layout layout) => (int)(layout.Edge switch
    {
        Edge.Top => layout.PanelBottom,
        Edge.Left => layout.PanelRight,
        Edge.Right => layout.Width - layout.PanelLeft,
        _ => layout.Height - layout.PanelTop,
    });

    private static Hit? HitAt(Layout layout, float along, float across, bool editing = false)
    {
        var (x, y) = At(layout, along, across);
        return layout.HitTest(x, y, editing);
    }

    /// <summary>The part a hit names, -1 for the bar, the grip and nothing.</summary>
    private static int Owner(Layout layout, Hit? hit) =>
        hit is { Kind: not (HitKind.Panel or HitKind.ResizeGrip) } on ? layout.PartOf(on) : -1;

    /// <summary>Across the middle of the bar, clear of the grip on its inner side.</summary>
    private static float Middle(Layout layout) => MathF.Floor(Depth(layout) - (layout.Thickness / 2f));

    /// <summary>The parts that take the pointer, along the bar: every placed part.</summary>
    private static List<int> Targets(Layout layout) =>
    [
        .. Enumerable.Range(0, layout.Specs.Count)
            .Where(layout.Placed)
            .OrderBy(part => layout.PartAt(part).Left),
    ];

    /// <summary>Which run part <paramref name="part"/>'s neighbours are found in: its section, on a floating bar its section's one dock (§511).</summary>
    private static int Zone(Layout layout, int part) => (int)layout.Specs[part].Section;

    private static (float From, float To) Along(Layout layout, RectF rect) => layout.Vertical ? (rect.Y, rect.Bottom) : (rect.X, rect.Right);

    // --- Across ---------------------------------------------------------------------------------

    /// <summary>
    /// Over every widget, every point from the screen's edge to the bar's inner side is the widget's
    /// — the room a floating bar stands off the edge included — but for the grip along the inner side,
    /// which drags the bar's height.
    /// </summary>
    [Theory]
    [MemberData(nameof(Bars))]
    public void AcrossTheBarEveryPointOverAWidgetIsIts(BarStyle style, Edge edge, uint dpi)
    {
        var layout = Bar(style, edge, dpi);
        var targets = Targets(layout);
        Assert.True(targets.Count >= 7, $"only {targets.Count} parts placed");
        foreach (var part in targets)
        {
            var block = layout.PartAt(part);
            var along = MathF.Floor((block.Left + block.Right) / 2f);
            for (var across = 0; across < Depth(layout); across++)
            {
                var hit = HitAt(layout, along, across);
                if (hit is { Kind: HitKind.ResizeGrip })
                {
                    continue;
                }
                Assert.True(Owner(layout, hit) == part, $"{layout.Specs[part].Kind} at {along} along, {across} across: {hit}");
            }
        }
    }

    // --- Along ----------------------------------------------------------------------------------

    /// <summary>
    /// Every point between two neighbouring widgets is one of theirs, the nearer one's.
    /// </summary>
    [Theory]
    [MemberData(nameof(Bars))]
    public void EveryPointBetweenTwoNeighboursIsTheNearerOnes(BarStyle style, Edge edge, uint dpi)
    {
        var layout = Bar(style, edge, dpi);
        var targets = Targets(layout);
        var across = Middle(layout);
        var gaps = 0;
        for (var at = 1; at < targets.Count; at++)
        {
            var (before, after) = (targets[at - 1], targets[at]);
            if (Zone(layout, before) != Zone(layout, after))
            {
                continue;
            }
            var (end, start) = (layout.PartAt(before).Right, layout.PartAt(after).Left);
            for (var along = MathF.Floor(end); along < start; along++)
            {
                var point = along + 0.5f;
                var owner = Owner(layout, HitAt(layout, point, across));
                if (point - end < start - point)
                {
                    Assert.True(owner == before, $"{point} between {layout.Specs[before].Kind} and {layout.Specs[after].Kind}: {owner}");
                }
                else if (point - end > start - point)
                {
                    Assert.True(owner == after, $"{point} between {layout.Specs[before].Kind} and {layout.Specs[after].Kind}: {owner}");
                }
                gaps++;
            }
        }
        // Islands keep a gap between two widgets, a floating dock a widget's gap too.
        Assert.True(gaps > 0, "no gap was probed");
    }

    /// <summary>
    /// The first widget of the start reaches to the bar's start and the last of the end to its end; on
    /// a floating bar a dock's first and last reach to the dock's ends, and the desktop beyond them is
    /// nobody's.
    /// </summary>
    [Theory]
    [MemberData(nameof(Bars))]
    public void TheOutermostWidgetsReachTheBarsEnds(BarStyle style, Edge edge, uint dpi)
    {
        var layout = Bar(style, edge, dpi);
        var targets = Targets(layout);
        var across = Middle(layout);
        var (first, last) = (targets[0], targets[^1]);
        var (from, to) = (0f, layout.Length);
        if (layout.Floating)
        {
            (from, _) = Along(layout, layout.SectionPanels()[Zone(layout, first)]);
            (_, to) = Along(layout, layout.SectionPanels()[Zone(layout, last)]);
            Assert.Equal(-1, Owner(layout, HitAt(layout, MathF.Floor(from) - 1f, across)));
            Assert.Equal(-1, Owner(layout, HitAt(layout, MathF.Ceiling(to), across)));
        }
        else
        {
            Assert.True(layout.PartAt(first).Left > 0f && layout.PartAt(last).Right < layout.Length, "the bar keeps no room at its ends, so this proves nothing");
        }
        for (var along = MathF.Ceiling(from); along < layout.PartAt(first).Left; along++)
        {
            Assert.Equal(first, Owner(layout, HitAt(layout, along, across)));
        }
        for (var along = MathF.Ceiling(layout.PartAt(last).Right); along < MathF.Floor(to); along++)
        {
            Assert.Equal(last, Owner(layout, HitAt(layout, along, across)));
        }
        // The very corner of the screen, where the pointer is thrown.
        if (!layout.Floating)
        {
            Assert.Equal(first, Owner(layout, HitAt(layout, 0f, 0f)));
            Assert.Equal(last, Owner(layout, HitAt(layout, layout.Length - 1f, 0f)));
        }
    }

    /// <summary>
    /// The room between two sections, what a spacer grows to, is the bar's: its menu, and in edit mode
    /// the lasso — but for the sections' "+" there (§297), which no widget reaches over. The small gap
    /// between two widgets is theirs in edit mode too.
    /// </summary>
    [Theory]
    [MemberData(nameof(Bars))]
    public void TheRoomBetweenSectionsIsTheBars(BarStyle style, Edge edge, uint dpi)
    {
        foreach (var editing in new[] { false, true })
        {
            var layout = Bar(style, edge, dpi, editing);
            var targets = Targets(layout);
            var across = Middle(layout);
            var probed = 0;
            for (var at = 1; at < targets.Count; at++)
            {
                var (before, after) = (targets[at - 1], targets[at]);
                if (layout.Specs[before].Section == layout.Specs[after].Section)
                {
                    continue;
                }
                var (from, to) = (layout.PartAt(before).Right, layout.PartAt(after).Left);
                if (layout.Floating)
                {
                    // A floating dock's widgets reach to its ends: the room is from one dock to the next.
                    (from, to) = (Along(layout, layout.SectionPanels()[Zone(layout, before)]).To, Along(layout, layout.SectionPanels()[Zone(layout, after)]).From);
                }
                for (var along = MathF.Ceiling(from); along < to; along++)
                {
                    var hit = HitAt(layout, along, across, editing);
                    Assert.True(hit is null or { Kind: HitKind.Panel } || (editing && hit is { Kind: HitKind.Add }), $"{along} between the sections ({layout.Specs[before].Kind}, {layout.Specs[after].Kind}), editing {editing}: {hit}");
                    probed++;
                }
            }
            Assert.True(probed > 0, "no room between sections was probed");
        }
    }

    /// <summary>
    /// In edit mode the gap between two widgets is still theirs, so a press there takes a widget rather than starting
    /// the lasso.
    /// </summary>
    [Theory]
    [InlineData(Edge.Bottom, 96u)]
    [InlineData(Edge.Left, 144u)]
    public void InEditModeTheGapsAreTheWidgets(Edge edge, uint dpi)
    {
        var layout = Bar(BarStyle.Islands, edge, dpi, editing: true);
        var across = Middle(layout);
        var parts = Enumerable.Range(0, layout.Specs.Count).Where(layout.Placed).OrderBy(part => layout.PartAt(part).Left).ToList();
        for (var at = 1; at < parts.Count; at++)
        {
            var (before, after) = (parts[at - 1], parts[at]);
            if (layout.Specs[before].Section != layout.Specs[after].Section)
            {
                continue;
            }
            for (var along = MathF.Floor(layout.PartAt(before).Right); along < layout.PartAt(after).Left; along++)
            {
                var owner = Owner(layout, HitAt(layout, along + 0.5f, across, editing: true));
                Assert.True(owner == before || owner == after, $"{along + 0.5f} between {layout.Specs[before].Kind} and {layout.Specs[after].Kind} in edit mode: {owner}");
            }
        }
    }

    /// <summary>
    /// The outermost thing in a widget takes the room it reaches over: the gap after the open apps is
    /// their last button's, after the tray its last icon's.
    /// </summary>
    [Fact]
    public void TheOutermostButtonTakesTheRoomOnItsSide()
    {
        var layout = Bar(BarStyle.Islands, Edge.Bottom, 96);
        var across = Middle(layout);
        var apps = layout.PartIndex(layout.GetPart(BarKind.Apps)!.Value.Key);
        var tray = layout.PartIndex(layout.GetPart(BarKind.Tray)!.Value.Key);

        Assert.Equal(Hit.Tile(2).In(apps), HitAt(layout, layout.PartAt(apps).Right + 0.5f, across));
        Assert.Equal(Hit.Tray(2).In(tray), HitAt(layout, layout.PartAt(tray).Right + 0.5f, across));
    }

    // --- Pictures -------------------------------------------------------------------------------

    /// <summary>
    /// widget-reach-&lt;scale&gt;.png: every bar here, each pixel coloured by the part it hits (a hue a
    /// part, lighter and darker by button), the bar's own room grey, the grip red, nothing black, the
    /// drawn blocks outlined white — so the reach can be seen against what is drawn.
    /// </summary>
    [UiFact]
    public void ReachPictures()
    {
        WindowShots.OnHiddenDesktop("widget-reach", (_, _) =>
        {
            foreach (var dpi in new uint[] { 96, 144 })
            {
                var horizontal = new List<(byte[] Canvas, int Width, int Height)>();
                foreach (var layout in new[]
                {
                    Bar(BarStyle.Islands, Edge.Bottom, dpi),
                    Bar(BarStyle.Islands, Edge.Top, dpi),
                    Bar(BarStyle.Islands, Edge.Bottom, dpi, editing: true),
                    Bar(BarStyle.Floating, Edge.Bottom, dpi),
                })
                {
                    horizontal.Add(Paint(layout));
                }
                var vertical = new List<(byte[] Canvas, int Width, int Height)>
                {
                    Paint(Bar(BarStyle.Islands, Edge.Left, dpi)),
                    Paint(Bar(BarStyle.Islands, Edge.Right, dpi)),
                    Paint(Bar(BarStyle.Floating, Edge.Left, dpi)),
                };
                var scale = dpi * 100 / 96;
                WindowShots.SaveSideBySide([Stacked(horizontal, 6)], $"widget-reach-horizontal-{scale}.png", (0, 0, 0));
                WindowShots.SaveSideBySide(vertical, $"widget-reach-vertical-{scale}.png", (0, 0, 0), gap: 12);
            }
        });
    }

    /// <summary>The bar's strip of <paramref name="layout"/>'s window, coloured by what each pixel hits.</summary>
    private static (byte[] Canvas, int Width, int Height) Paint(Layout layout)
    {
        // Only the bar's strip, from the screen's edge to its inner side; the tooltip room is left off.
        var depth = Depth(layout) + 4;
        var (width, height) = layout.Vertical ? (depth, layout.Height) : (layout.Width, depth);
        var canvas = new byte[width * height * 4];
        // Window px from canvas px.
        (int X, int Y) Window(int x, int y) => layout.Edge switch
        {
            Edge.Bottom => (x, layout.Height - height + y),
            Edge.Right => (layout.Width - width + x, y),
            _ => (x, y),
        };
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var (wx, wy) = Window(x, y);
                var hit = layout.HitTest(wx + 0.5f, wy + 0.5f, layout.Editing);
                var (b, g, r) = hit switch
                {
                    null => ((byte)0, (byte)0, (byte)0),
                    { Kind: HitKind.Panel } => ((byte)70, (byte)70, (byte)70),
                    { Kind: HitKind.ResizeGrip } => ((byte)30, (byte)30, (byte)150),
                    { } on => Hue(layout.PartOf(on), on.Index),
                };
                var at = ((y * width) + x) * 4;
                (canvas[at], canvas[at + 1], canvas[at + 2], canvas[at + 3]) = (b, g, r, 255);
            }
        }
        // The drawn blocks, outlined.
        foreach (var part in Enumerable.Range(0, layout.Specs.Count).Where(layout.Placed))
        {
            var rect = layout.BlockRect(part);
            var (left, top) = Canvas(rect.X, rect.Y);
            var (right, bottom) = Canvas(rect.Right - 1f, rect.Bottom - 1f);
            for (var x = left; x <= right; x++)
            {
                Dot(x, top);
                Dot(x, bottom);
            }
            for (var y = top; y <= bottom; y++)
            {
                Dot(left, y);
                Dot(right, y);
            }
        }
        return (canvas, width, height);

        (int X, int Y) Canvas(float x, float y) => layout.Edge switch
        {
            Edge.Bottom => ((int)x, (int)y - (layout.Height - height)),
            Edge.Right => ((int)x - (layout.Width - width), (int)y),
            _ => ((int)x, (int)y),
        };

        void Dot(int x, int y)
        {
            if (x >= 0 && y >= 0 && x < width && y < height)
            {
                var at = ((y * width) + x) * 4;
                (canvas[at], canvas[at + 1], canvas[at + 2]) = (255, 255, 255);
            }
        }
    }

    /// <summary>A colour for part <paramref name="part"/>, lighter or darker for every other button in it.</summary>
    private static (byte B, byte G, byte R) Hue(int part, int index)
    {
        (byte, byte, byte)[] hues =
        [
            (200, 120, 40), (60, 170, 60), (40, 120, 220), (180, 60, 180), (170, 170, 40), (40, 180, 200), (90, 90, 220), (200, 80, 120), (120, 200, 120), (220, 160, 90),
        ];
        var (b, g, r) = hues[Math.Abs(part) % hues.Length];
        return index % 2 == 0 ? (b, g, r) : ((byte)(b * 2 / 3), (byte)(g * 2 / 3), (byte)(r * 2 / 3));
    }

    /// <summary><paramref name="pictures"/> one under another, <paramref name="gap"/> px of black between.</summary>
    private static (byte[] Canvas, int Width, int Height) Stacked(List<(byte[] Canvas, int Width, int Height)> pictures, int gap)
    {
        var width = pictures.Max(picture => picture.Width);
        var height = pictures.Sum(picture => picture.Height) + (gap * (pictures.Count - 1));
        var canvas = new byte[width * height * 4];
        for (var at = 3; at < canvas.Length; at += 4)
        {
            canvas[at] = 255;
        }
        var y0 = 0;
        foreach (var (pixels, w, h) in pictures)
        {
            for (var y = 0; y < h; y++)
            {
                Buffer.BlockCopy(pixels, y * w * 4, canvas, (((y0 + y) * width)) * 4, w * 4);
            }
            y0 += h + gap;
        }
        return (canvas, width, height);
    }
}
