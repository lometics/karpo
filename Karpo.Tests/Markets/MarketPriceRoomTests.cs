// markets: a line's price is never cut where it fits (the person, 2026-10-22: "the price of a stock is
// cutoff even though there would be space to show it", docs\widgets.md §277). The room a price keeps is its
// widest shape, every figure an eight; in the font's own figures a "2" is wider than an "8" at a list's
// figure size, so "262.24" overran the room "888.88" kept and was cut to "262.…". Its figures are measured
// and drawn in tabular figures now, as the face's are (§245).
//
// Prices full of wide figures (262.24, 158.00, 222.22) and a coin's and a rate's beside them, on a line one
// row to four high at the narrow and the medium list, at 100 and 150 %: the price and the change whole, never
// trimmed, and where the row is short the name gives way, never the price. markets-price-room-dark-<scale>.png
// (the lines, and the faces on a 41 DIP bar under them) go to KARPO_PNG_DIR. Needs Direct2D: KARPO_UI_TESTS=1.

using System.Globalization;
using Karpo.Dock;
using Karpo.Dock.Markets;
using Karpo.Tests.Folders;
using Karpo.Tests.Forecasts;
using Karpo.Tests.Islands;
using static Karpo.Tests.Islands.Offscreen;

namespace Karpo.Tests.Markets;

public sealed unsafe class MarketPriceRoomTests
{
    /// <summary>A day's answer of <paramref name="symbol"/> at <paramref name="price"/>, moved <paramref name="percent"/>, quoted to <paramref name="hint"/> places.</summary>
    private static string Day(string symbol, string name, string currency, double price, double percent, int hint)
    {
        var previous = price / (1 + (percent / 100));
        return MarketOffscreenTests.Answer(symbol, name, currency, price, percent, previous, wander: price * 0.002)
            .Replace("\"currency\":", $"\"priceHint\": {hint.ToString(CultureInfo.InvariantCulture)}, \"currency\":", StringComparison.Ordinal);
    }

    private static Dictionary<string, string> Answers() => new()
    {
        ["AAPL 5y"] = Day("AAPL", "Apple Inc.", "USD", 262.24, 0.35, 2),
        ["TWO"] = Day("TWO", "Two Inc.", "USD", 222.22, -22.22, 2),
        ["BTC-USD"] = Day("BTC-USD", "Bitcoin USD", "USD", 81912.34, -1.23, 2),
        ["JPY=X"] = Day("JPY=X", "USD/JPY", "JPY", 158.00, 0.12, 2),
        ["CHFUSD=X"] = Day("CHFUSD=X", "CHF/USD", "USD", 1.2105, -0.02, 4),
    };

    /// <summary>The person's own line (Apple over five years, named "Apple"), and the others with a long name that must give way first.</summary>
    private static List<MarketOptions> Shown() =>
    [
        new() { Symbol = "AAPL", Label = "Apple", Range = MarketRange.FiveYears, RangeYears = 3 },
        new() { Symbol = "TWO", Label = "Two Twenty-Two Holdings", Range = MarketRange.Day },
        new() { Symbol = "BTC-USD", Range = MarketRange.Day },
        new() { Symbol = "JPY=X", Range = MarketRange.Day },
        new() { Symbol = "CHFUSD=X", Range = MarketRange.Day },
    ];

    /// <summary>
    /// Every line, one row to four high at the narrow and the medium list, at 100 and 150 %: its price as
    /// it reads and its change, each drawn whole — never trimmed by DirectWrite, its tabular width within its
    /// room. A long name is what gives way.
    /// </summary>
    [UiFact]
    public void ThePriceIsWholeWhereverItFits()
    {
        var failures = new List<string>();
        foreach (var scale in new[] { 1f, 1.5f })
        {
            OnStaThread(gfx =>
            {
                var store = MarketOffscreenTests.Settled(Answers());
                var lines = Shown().Select((options, index) => MarketFitTests.Line(gfx, store, $"room{index}", options)).ToList();
                var folders = Shown().Select((options, index) => MarketFitTests.Folder(gfx, store, $"roomface{index}", options)).ToList();
                // What DirectWrite cut: the words of each layout drawn trimmed (PaintWatch).
                var cut = new List<string>();
                string? said = null;
                var trimmed = 0;
                PaintWatch.Said = (text, _) => said = text;
                PaintWatch.Watching = (_, _, colour) =>
                {
                    if (PaintWatch.Trimmed != trimmed)
                    {
                        trimmed = PaintWatch.Trimmed;
                        cut.Add(said ?? "");
                    }
                    return colour;
                };
                try
                {
                    var look = ListFormPictures.LookFor(dark: true);
                    var theme = FlyoutTheme.For(look);
                    var style = FolderFlyout.StyleFor(scale, look, theme);
                    var gap = 12 * scale;
                    var boxes = new[] { FolderListWidth.Narrow, FolderListWidth.Medium }
                        .SelectMany(width => Enumerable.Range(1, 4).Select(rows => (Width: FolderFlyout.ListWidth(scale, width), Height: LineRows.Px(rows, style))))
                        .ToList();
                    var widest = boxes.Max(box => box.Width);
                    var bar = new IslandStyle(scale, false, 41 * scale, 24 * scale, look);
                    var faces = folders.Select(folder => folder.CellSize(0, bar)).ToList();
                    var sheetWidth = MathF.Max(gap + (lines.Count * (widest + gap)), gap + faces.Sum(size => size.Width + gap));
                    var sheetHeight = gap + boxes.Sum(box => box.Height + gap) + faces.Max(size => size.Height) + gap;
                    Bitmap(sheetWidth, sheetHeight, dc =>
                    {
                        PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, sheetWidth, sheetHeight), 0, PopupPaint.Ink(theme.Base));
                        var y = gap;
                        foreach (var (width, height) in boxes)
                        {
                            var x = gap;
                            foreach (var line in lines)
                            {
                                var box = PopupPaint.Rect(x, y, width, height);
                                PopupPaint.Fill(dc, box, 4 * style.Scale, PopupPaint.Ink(theme.CardBackground));
                                cut.Clear();
                                line.PaintOn(dc, box, style, WidgetSurface.List);
                                var where = $"{line.Options.Symbol}, {width} by {height} px at {scale * 100:0} %";
                                var reading = line.Reading();
                                var figures = line.Figured;
                                if (!figures.Any(figure => figure.Text == reading.Price))
                                {
                                    failures.Add($"{where}: the price {reading.Price} is not drawn as it reads ({string.Join(", ", figures.Select(figure => figure.Text))})");
                                }
                                if (!figures.Any(figure => figure.Text == reading.Percent))
                                {
                                    failures.Add($"{where}: the change {reading.Percent} is not drawn");
                                }
                                foreach (var figure in figures)
                                {
                                    var drawn = TextLine.Measure(gfx, figure.Text, figure.Px, figure.Weight, null);
                                    if (drawn > figure.Room + 0.5f)
                                    {
                                        failures.Add($"{where}: \"{figure.Text}\" is {drawn} px in {figure.Room} px");
                                    }
                                    if (cut.Contains(figure.Text))
                                    {
                                        failures.Add($"{where}: \"{figure.Text}\" was cut");
                                    }
                                }
                                x += widest + gap;
                            }
                            y += height + gap;
                        }
                        var along = gap;
                        for (var index = 0; index < folders.Count; index++)
                        {
                            var (w, h) = faces[index];
                            var folder = folders[index];
                            PopupPaint.Fill(dc, PopupPaint.Rect(along, y, w, h), 4 * scale, PopupPaint.Ink(theme.CardBackground));
                            cut.Clear();
                            Shifted(dc, along, y, () => folder.PaintCell(dc, 0, bar, w, h));
                            if (cut.Count > 0)
                            {
                                failures.Add($"{folder.Options.Face.Market!.Symbol}'s face at {scale * 100:0} %: cut {string.Join(", ", cut)}");
                            }
                            along += w + gap;
                        }
                    }, bitmap => Save(bitmap, Path.Combine(Pictures, $"markets-price-room-dark-{scale * 100:0}.png")));
                }
                finally
                {
                    PaintWatch.Said = null;
                    PaintWatch.Watching = null;
                    lines.ForEach(line => line.Dispose());
                    folders.ForEach(folder => folder.Dispose());
                }
            });
        }
        Assert.True(failures.Count == 0, $"{failures.Count} prices not whole:\n{string.Join("\n", failures)}");
    }

    /// <summary>
    /// In the tabular figures a price is drawn in, every figure is an eight's width, so the room its shape
    /// keeps holds it; in the font's own a "2" is wider than an "8" at a list's figure size.
    /// </summary>
    [UiFact]
    public void ATabularPriceIsAsWideAsItsShape() => OnStaThread(gfx =>
    {
        foreach (var px in new[] { 14f, 20f, 30f, 45f })
        {
            foreach (var price in new[] { "262.24", "222.22", "158.00", "1.2105", "81,912.34", "0.000123" })
            {
                Assert.Equal(TextLine.Measure(gfx, MarketIsland.Shape(price), px, Weight.Semibold, null), TextLine.Measure(gfx, price, px, Weight.Semibold, null), 0.5f);
            }
        }
    });
}
