// A taskbar entry's form fills its card (docs\widgets.md §56).
//
// The person: "the widgets themselves should have a little space to the taskbar and neighbouring
// widgets and should get their own background. they should then be designed in a way so that they
// use up the whole space that is available to them."
//
// Two halves.
//
// - The room (Layout.CardInsetPx): a card keeps a tenth of the bar's height off the bar's edge
//   across it, and the first and the last card of a section keep the same off the bar's ends; the
//   floating dock keeps its own look. Held at the bar heights from the thinnest to a tall one.
// - The fill: every entry a widget class draws, at 28, 36, 48 and 80 DIP at 100 % and 150 %, is
//   painted into its cell with room to spare around it. Nothing it draws leaves the cell (so
//   nothing is clipped at the thinnest bar), what it draws stands in the middle of the card across
//   the bar, and it takes at least MinFill of the card's height — no big empty bands above and
//   below a small glyph. Nothing is huge either: the ink never takes more than the cell.
//
// The sheets (bar-forms-<height>-<dark|light>.png, and the numbers beside them, .txt) go to
// KARPO_PNG_DIR before anything is asserted, so a failing run leaves its pictures.
//
// In the samplers' collection because the performance entries hold the process-wide samplers
// (Perf.SamplerState says why that matters).

using System.Globalization;
using System.Text;
using Karpo.Dock;
using Karpo.Dock.Native;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static Karpo.Tests.Sizing.OneRowTests;

namespace Karpo.Tests.Sizing;

[Collection(Perf.SamplerState.Collection)]
public sealed unsafe class BarFormTests
{
    private static readonly int[] Heights = [28, 36, 40, 48, 80];

    /// <summary>
    /// The share of the card's height an entry's ink takes, at the least: a third and a bit, the
    /// taskbar's own icons' (Windows' 16 DIP of its 48-DIP bar, docs\widgets.md §57; 24 of an 80-DIP
    /// bar's 64-DIP card is the least of them). It was 0.4 while those icons were 20 DIP.
    /// </summary>
    internal const float MinFill = 0.35f;

    // --- The room ------------------------------------------------------------------------------

    /// <summary>
    /// A card keeps room off the bar that grows with the bar: about a tenth of its height between 2
    /// and 10 DIPs, as much at the two ends of a section as above and below, and it is never so much
    /// that a card is shorter than the bar's icon. The floating dock keeps 2 DIPs and its panel.
    /// </summary>
    [Fact]
    public void ACardKeepsRoomOffTheBarThatGrowsWithIt()
    {
        var least = 0f;
        // At every height since §533: a thin bar keeps its room too; only a layout spaced 0 is flush (WidgetSpacingTests).
        foreach (var height in new[] { 28, 32, 36, 40, 48, 56, 64, 80, 100, 160 })
        {
            foreach (var dpi in (uint[])[96, 120, 144, 192])
            {
                var config = new Config { Height = height };
                var inset = Layout.CardInsetPx(dpi, config);
                var scale = dpi / 96f;
                var thickness = Layout.ThicknessPx(dpi, config);
                var where = $"{height} DIP at {dpi} DPI";
                Assert.True(inset >= MathF.Round(2 * scale), $"{where}: {inset} px is under the 2 DIPs of breath");
                Assert.True(inset <= MathF.Ceiling(10 * scale), $"{where}: {inset} px is over the 10 DIPs a card keeps at most");
                Assert.True(thickness - (2 * inset) >= Layout.IconPx(dpi, config), $"{where}: the card is shorter than the icon");
                // The card is the bar less that, above and below.
                Assert.Equal(thickness - (2 * inset), Layout.CellPx(dpi, config));
                if (dpi == 96)
                {
                    Assert.True(inset >= least, $"{where}: {inset} px is less than the thinner bar's {least}");
                    least = inset;
                }
            }
        }
        // 4 DIPs of 40, 3 of the thin 32 (§533: none there before, the bar flush by itself), none on a bar spaced None.
        Assert.Equal(4f, Layout.CardInsetPx(96, new Config { Height = 40 }));
        Assert.Equal(8f, Layout.CardInsetPx(96, new Config { Height = 80 }));
        Assert.Equal(4f, Layout.CardInsetPx(96, new Config { Height = 36 }));
        Assert.Equal(3f, Layout.CardInsetPx(96, new Config { Height = 32 }));
        Assert.Equal(0f, Layout.CardInsetPx(96, new Config { Height = 48, Layouts = [new TaskbarLayout { Spacing = 0 }] }));
        // The dock keeps its look.
        Assert.Equal(2f, Layout.CardInsetPx(96, new Config { Height = 80, Style = BarStyle.Floating }));
    }

    /// <summary>
    /// On an islands bar the first and the last card of a section stand the same room off the bar's
    /// ends as the cards stand off its edges: the start section starts at the inset, the end section
    /// ends at the bar's length less it, on every edge and at every height.
    /// </summary>
    [Fact]
    public void TheFirstAndTheLastCardKeepThatRoomOffTheBarsEnds()
    {
        foreach (var edge in new[] { Edge.Bottom, Edge.Top, Edge.Left, Edge.Right })
        {
            foreach (var height in Heights)
            {
                foreach (var dpi in (uint[])[96, 144])
                {
                    var config = new Config { Height = height };
                    var inset = Layout.CardInsetPx(dpi, config);
                    List<PartSpec> specs =
                    [
                        new PartSpec("a", BarKind.Start) { Section = BarSection.Start },
                        new PartSpec("b", BarKind.EnergySaver) { Section = BarSection.Start },
                        new PartSpec("c", BarKind.TouchKeyboard) { Section = BarSection.End },
                        new PartSpec("d", BarKind.EnergySaver) { Section = BarSection.End },
                    ];
                    var area = edge.IsVertical() ? new RECT { right = 1200, bottom = 1920 } : new RECT { right = 1920, bottom = 1200 };
                    var layout = Layout.ForTests(dpi, area, edge, config, specs, Layout.CellPx(dpi, config));
                    var length = edge.IsVertical() ? 1920f : 1920f;
                    var first = layout.BlockRect(0);
                    var last = layout.BlockRect(3);
                    var where = $"{edge}, {height} DIP, {dpi} DPI";
                    if (edge.IsVertical())
                    {
                        Assert.Equal(inset, first.Y);
                        Assert.Equal(length - inset, last.Bottom);
                    }
                    else
                    {
                        Assert.Equal(inset, first.X);
                        Assert.Equal(length - inset, last.Right);
                    }
                    // Across the bar the card is the bar less the same room both sides.
                    var across = edge.IsVertical() ? (first.X, first.Width) : (first.Y, first.Height);
                    var panel = layout.PanelRect;
                    var off = edge.IsVertical() ? across.Item1 - panel.X : across.Item1 - panel.Y;
                    var below = edge.IsVertical() ? panel.Right - (first.X + first.Width) : panel.Bottom - (first.Y + first.Height);
                    Assert.True(off == inset && below == inset, $"{where}: {off} px above the card and {below} below it, and the inset is {inset}");
                    var thickness = Layout.ThicknessPx(dpi, config);
                    Assert.Equal(thickness - (2 * inset), across.Item2);
                }
            }
        }
    }

    /// <summary>The dock's widgets stand flush in their panel and the panel keeps its own room: the ends are the panel's.</summary>
    [Fact]
    public void TheFloatingDockKeepsItsLook()
    {
        foreach (var height in Heights)
        {
            var config = new Config { Height = height, Style = BarStyle.Floating };
            var specs = new List<PartSpec> { new("a", BarKind.Start) { Section = BarSection.Start } };
            var layout = Layout.ForTests(96, new RECT { right = 1920, bottom = 1200 }, Edge.Bottom, config, specs, Layout.CellPx(96, config));
            // 8 DIPs off the screen's end and the panel's own padding, as before.
            Assert.True(layout.BlockRect(0).X >= 8f, $"{height} DIP: the dock's first widget is at {layout.BlockRect(0).X}");
            Assert.Equal(2f, Layout.CardInsetPx(96, config));
        }
    }

    // --- The fill ------------------------------------------------------------------------------

    /// <summary>One entry measured at one bar.</summary>
    private readonly record struct Fill(string Name, float Width, float Height, float Left, float Top, float Right, float Bottom, float BoxTop = 0f, float BoxBottom = 0f)
    {
        public bool Drew => Right > Left;

        /// <summary>
        /// The share of the card's height the content's boxes take: from the top of the highest line
        /// of text, glyph or shape to the bottom of the lowest. The ink is less than this for words
        /// without capitals or descenders and for glyphs drawn flat (a disk, a battery), and the
        /// line is what a design sized.
        /// </summary>
        public float Share => Drew && BoxBottom > BoxTop ? (BoxBottom - BoxTop) / Height : 0f;

        /// <summary>The share of the card's height the ink itself takes.</summary>
        public float InkShare => Drew ? (Bottom - Top) / Height : 0f;

        /// <summary>How far the ink's middle is below the card's, in px.</summary>
        public float Offset => Drew ? ((Top + Bottom) / 2f) - (Height / 2f) : 0f;
    }

    /// <summary>The room round a cell in a measuring picture, where ink that leaves the cell is seen.</summary>
    private const float Margin = 12f;

    private static Fill Measure(string name, CellIsland island, IslandStyle style)
    {
        var sizes = Enumerable.Range(0, island.CellCount).Select(cell => island.CellSize(cell, style)).ToList();
        var along = sizes.Sum(size => size.Width);
        var across = sizes.Count == 0 ? 0f : sizes.Max(size => size.Height);
        if (sizes.Count == 0)
        {
            return new Fill(name, 0, 0, 0, 0, 0, 0);
        }
        var shown = style;
        var ink = (0f, 0f, 0f, 0f);
        var boxTop = float.MaxValue;
        var boxBottom = float.MinValue;
        PaintWatch.Watching = (box, what, color) =>
        {
            if (box.bottom > box.top)
            {
                boxTop = MathF.Min(boxTop, box.top);
                boxBottom = MathF.Max(boxBottom, box.bottom);
            }
            return color;
        };
        try
        {
            Offscreen.Bitmap(
                along + (2 * Margin),
                across + (2 * Margin),
                dc =>
                {
                    var at = Margin;
                    for (var cell = 0; cell < sizes.Count; cell++)
                    {
                        var (w, h) = sizes[cell];
                        var index = cell;
                        Offscreen.Shifted(dc, at, Margin, () => island.PaintCell(dc, index, shown, w, h));
                        at += w;
                    }
                },
                bitmap => ink = Offscreen.InkOf(bitmap, (uint)MathF.Ceiling(along + (2 * Margin)), (uint)MathF.Ceiling(across + (2 * Margin))));
        }
        finally
        {
            PaintWatch.Watching = null;
        }
        return new Fill(name, along, across, ink.Item1 - Margin, ink.Item2 - Margin, ink.Item3 - Margin, ink.Item4 - Margin, boxTop, boxBottom);
    }

    /// <summary>
    /// Whether the ink's middle is held to the card's: a drive that is not connected carries its
    /// warning badge hanging off the icon's corner, which is the picture's own and not the content's.
    /// </summary>
    private static bool Centred(string name) => name is not ("Drive" or "NetworkDrive");

    [UiFact]
    public void EveryEntrysFormFillsItsCardAndLeavesNoneOfIt()
    {
        var failures = new List<string>();
        Offscreen.OnStaThread(gfx =>
        {
            var made = Make(gfx);
            try
            {
                foreach (var dark in new[] { true, false })
                {
                    foreach (var height in Heights)
                    {
                        failures.AddRange(Sheet(gfx, made, height, 96, dark));
                    }
                }
                foreach (var height in Heights)
                {
                    failures.AddRange(Sheet(gfx, made, height, 144, dark: true, picture: false));
                }
            }
            finally
            {
                Dispose(made);
            }
        });
        Assert.True(failures.Count == 0, $"{failures.Count} forms do not fill their card:\n{string.Join("\n", failures)}");
    }

    private static List<string> Sheet(Gfx gfx, List<(Entry Entry, CellIsland Island)> made, int height, uint dpi, bool dark, bool picture = true)
    {
        var config = new Config { Height = height };
        var look = LookFor(dark);
        var theme = FlyoutTheme.For(look);
        var failures = new List<string>();
        var rows = new List<Fill>();
        var drawn = new List<(string Name, float Width, float Height, Action<IntPtr> Paint)>();
        var cell = Layout.CellPx(dpi, config);
        var tolerance = 0.5f;
        foreach (var (entry, island) in made)
        {
            var style = StyleFor(dpi, config, look, entry.Widget);
            var fill = Measure(entry.Name, island, style);
            rows.Add(fill);
            var where = $"{entry.Name} at {height} DIP, {dpi} DPI, {(dark ? "dark" : "light")}";
            if (fill.Drew)
            {
                if (fill.Left < -tolerance || fill.Right > fill.Width + tolerance || fill.Top < -tolerance || fill.Bottom > fill.Height + tolerance)
                {
                    failures.Add($"{where}: draws {fill.Left:0.#},{fill.Top:0.#}-{fill.Right:0.#},{fill.Bottom:0.#} outside its {fill.Width:0.#} by {fill.Height:0.#} card");
                }
                else if (fill.Share < MinFill)
                {
                    failures.Add($"{where}: its content is {fill.Share:P0} of the card's height, and the least is {MinFill:P0}");
                }
                if (Centred(entry.Name) && MathF.Abs(fill.Offset) > MathF.Max(2f, 0.1f * fill.Height))
                {
                    failures.Add($"{where}: its ink is {fill.Offset:+0.0;-0.0} px off the middle of the card");
                }
            }
            if (picture && island.CellCount > 0)
            {
                var sizes = Enumerable.Range(0, island.CellCount).Select(index => island.CellSize(index, style)).ToList();
                var painted = island;
                var shown = style;
                drawn.Add((entry.Name, fill.Width, fill.Height, pointer =>
                {
                    var dc = (ID2D1DeviceContext*)pointer;
                    var at = 0f;
                    for (var index = 0; index < sizes.Count; index++)
                    {
                        var (w, h) = sizes[index];
                        var which = index;
                        Offscreen.Shifted(dc, at, 0, () => painted.PaintCell(dc, which, shown, w, h));
                        at += w;
                    }
                }));
            }
        }
        if (picture)
        {
            var name = $"bar-forms-{height}-{(dark ? "dark" : "light")}";
            Pictures(gfx, theme, cell, drawn, Path.Combine(Offscreen.Pictures, name + ".png"));
            File.WriteAllText(Path.Combine(Offscreen.Pictures, name + ".txt"), Table(height, dark, rows));
        }
        return failures;
    }

    private static string Table(int height, bool dark, List<Fill> rows)
    {
        var text = new StringBuilder();
        text.AppendLine(CultureInfo.InvariantCulture, $"Bar forms: {height} DIP bar, {(dark ? "dark" : "light")}, 96 DPI. Px from the card's corner.");
        text.AppendLine("entry                      width  height  ink box                 box share  ink share  off middle");
        foreach (var item in rows)
        {
            var box = item.Drew ? $"{item.Left:0.#},{item.Top:0.#}-{item.Right:0.#},{item.Bottom:0.#}" : "nothing";
            text.AppendLine(CultureInfo.InvariantCulture, $"{item.Name,-26} {item.Width,5:0}  {item.Height,6:0}  {box,-22}  {item.Share,9:P0}  {item.InkShare,9:P0}  {item.Offset,8:+0.0;-0.0;0.0}");
        }
        return text.ToString();
    }

    /// <summary>The sheet: each entry on a card with the bar's corner and edge, its name over it, in lines as wide as a screen.</summary>
    private static void Pictures(Gfx gfx, FlyoutTheme theme, float cell, List<(string Name, float Width, float Height, Action<IntPtr> Paint)> drawn, string path)
    {
        const float pad = 12f;
        const float label = 16f;
        const float most = 1900f;
        var places = new List<(float X, float Y)>();
        var x = pad;
        var y = pad;
        var widest = 0f;
        foreach (var (_, width, _, _) in drawn)
        {
            if (x > pad && x + width > most)
            {
                x = pad;
                y += label + cell + pad;
            }
            places.Add((x, y));
            x += MathF.Max(width, 60f) + pad;
            widest = MathF.Max(widest, x);
        }
        var total = y + label + cell + pad;
        Offscreen.Bitmap(widest, total, dc =>
        {
            PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, widest, total), 0, PopupPaint.Ink(theme.Base));
            for (var index = 0; index < drawn.Count; index++)
            {
                var (name, width, height, paint) = drawn[index];
                var (left, top) = places[index];
                PopupPaint.Text(dc, gfx, name, 11f, Weight.Regular, theme.TextSecondary, left, top, MathF.Max(width, 60f));
                var card = top + label + MathF.Round((cell - height) / 2f);
                PopupPaint.Fill(dc, PopupPaint.Rect(left, card, width, height), 4f, PopupPaint.Ink(theme.CardBackground));
                Offscreen.Shifted(dc, left, card, () => paint((IntPtr)dc));
            }
        }, bitmap => Offscreen.Save(bitmap, path));
    }
}
