// markets: the price is never cut (docs\widgets.md §262). Long prices — a share in the hundreds of
// thousands, a coin in the tens of thousands, a coin of small units to eight places — and long changes
// (+112.34 %) on a line one row to four high, at the narrow and the medium list, and on the face across a
// bar and down one, at 100 and 150 %, dark and light: markets-fit-lines-<dark|light>-<100|150>.png and
// markets-fit-faces-<dark|light>-<100|150>.png. The sheets go to KARPO_PNG_DIR (else
// %TEMP%\karpo-tests\pictures) before anything is asserted; runs only with KARPO_UI_TESTS=1.

using System.Globalization;
using Karpo.Dock;
using Karpo.Dock.Markets;
using Karpo.Tests.Folders;
using Karpo.Tests.Forecasts;
using Karpo.Tests.Islands;
using static Karpo.Tests.Islands.Offscreen;

namespace Karpo.Tests.Markets;

public sealed unsafe class MarketFitTests
{
    /// <summary>A day's answer of <paramref name="symbol"/> at <paramref name="price"/>, moved <paramref name="percent"/>, quoted to <paramref name="hint"/> places where given.</summary>
    private static string Day(string symbol, string name, string currency, double price, double percent, int? hint = null)
    {
        var previous = price / (1 + (percent / 100));
        var json = MarketOffscreenTests.Answer(symbol, name, currency, price, percent, previous, wander: Math.Max(price, 1e-6) * 0.002)
            // The answer writes its price to four places; a coin of small units is quoted to more.
            .Replace($"\"regularMarketPrice\": {price.ToString("0.####", CultureInfo.InvariantCulture)}", $"\"regularMarketPrice\": {price.ToString("R", CultureInfo.InvariantCulture)}", StringComparison.Ordinal);
        return hint is { } places
            ? json.Replace("\"currency\":", $"\"priceHint\": {places.ToString(CultureInfo.InvariantCulture)}, \"currency\":", StringComparison.Ordinal)
            : json;
    }

    /// <summary>The prices every picture reads: the long ones, and an ordinary share and rate beside them.</summary>
    private static Dictionary<string, string> Answers() => new()
    {
        ["BRK-A"] = Day("BRK-A", "Berkshire Hathaway Inc.", "USD", 734567.89, 12.34, hint: 2),
        ["NESN.SW"] = Day("NESN.SW", "Nestlé S.A.", "CHF", 1234.56, -0.05, hint: 2),
        ["BTC-EUR"] = Day("BTC-EUR", "Bitcoin EUR", "EUR", 67890.12, -0.05),
        ["SHIB-USD"] = Day("SHIB-USD", "Shiba Inu USD", "USD", 0.000123, 112.34),
        ["^GSPC"] = Day("^GSPC", "S&P 500", "USD", 6789.12, 1.23, hint: 2),
        ["MSFT"] = Day("MSFT", "Microsoft Corporation", "USD", 412.34, 1.24, hint: 2),
        ["EURCHF=X"] = Day("EURCHF=X", "EUR/CHF", "CHF", 0.9269, -0.02),
    };

    /// <summary>What each picture shows: a caption and its record.</summary>
    internal static List<(string Caption, MarketOptions Options)> Shown() =>
    [
        ("Share, six figures", new MarketOptions { Symbol = "BRK-A", Range = MarketRange.Day }),
        ("Share in francs", new MarketOptions { Symbol = "NESN.SW", Range = MarketRange.Day, Holding = 12 }),
        ("Coin, thousands", new MarketOptions { Symbol = "BTC-EUR", Range = MarketRange.Day }),
        ("Coin of small units", new MarketOptions { Symbol = "SHIB-USD", Range = MarketRange.Day }),
        ("Index", new MarketOptions { Symbol = "^GSPC", Range = MarketRange.Day }),
        ("Share, price only", new MarketOptions { Symbol = "BRK-A", Range = MarketRange.Day, Style = MarketStyle.Compact }),
        ("Share", new MarketOptions { Symbol = "MSFT", Range = MarketRange.Day }),
        ("Rate", new MarketOptions { Symbol = "EURCHF=X", Range = MarketRange.Day }),
    ];

    internal static MarketIsland Line(Gfx gfx, MarketStore store, string id, MarketOptions options)
    {
        var item = ListKind.Markets.New();
        item.Id = id;
        item.Market = options.Copy();
        item.Normalize();
        var line = new MarketIsland(Config.IslandKey(ListFormPictures.Owner, id), item, gfx, new Config(), store);
        _ = line.Refresh(gfx);
        return line;
    }

    /// <summary>The markets preset's folder, its face showing <paramref name="face"/>, over <paramref name="store"/>.</summary>
    internal static FolderIsland Folder(Gfx gfx, MarketStore store, string id, MarketOptions face)
    {
        var holder = Karpo.Tests.Forecasts.MarketOffscreenTests.OneLineFolder(new PresetMachine(false, []));
        holder.Id = id;
        holder.Folder!.Face.Market = face.Copy();
        holder.Folder.Cells.Single().Island.Market = face.Copy();
        holder.Normalize();
        MarketIsland.Made = store;
        try
        {
            var folder = new FolderIsland($"{ListFormPictures.Owner}/{id}", holder, gfx, new Config());
            _ = ((IIsland)folder).Refresh(gfx);
            return folder;
        }
        finally
        {
            MarketIsland.Made = null;
        }
    }

    /// <summary>
    /// The shorter forms a price and a change take where their room is too narrow, each the whole of what
    /// it says as far as it goes, and the change's room its widest shape with a plus.
    /// </summary>
    [Fact]
    public void APriceAndAChangeHaveShorterFormsNeverCutOnes()
    {
        Assert.Equal(["734,567.89", "734,568", "734568", "734.6K"], MarketText.PriceForms("734,567.89"));
        Assert.Equal(["67,890.12", "67,890", "67890", "67.9K"], MarketText.PriceForms("67,890.12"));
        Assert.Equal(["1,234.56", "1,235", "1235"], MarketText.PriceForms("1,234.56"));
        Assert.Equal(["2,345,678", "2345678", "2.3M"], MarketText.PriceForms("2,345,678"));
        Assert.Equal(["412.34", "412"], MarketText.PriceForms("412.34"));
        Assert.Equal(["0.000123"], MarketText.PriceForms("0.000123"));
        Assert.Equal(["–"], MarketText.PriceForms("–"));
        Assert.Equal(["+12.34%", "+12.3%", "+12%"], MarketText.ChangeForms("+12.34%"));
        Assert.Equal(["-0.05%", "-0.1%"], MarketText.ChangeForms("-0.05%"));
        Assert.Equal(["0.00%"], MarketText.ChangeForms("0.00%"));
        Assert.Equal("+88.88%", MarketText.ChangeShape("-12.34%"));
        Assert.Equal("+8.88%", MarketText.ChangeShape("-0.05%"));
        Assert.Equal("+888.88%", MarketText.ChangeShape("+112.34%"));
    }

    /// <summary>The list widths a line is drawn at: the narrowest a folder's list is, and its own.</summary>
    private static readonly FolderListWidth[] Widths = [FolderListWidth.Narrow, FolderListWidth.Medium];

    /// <summary>The scales and text sizes the pictures are drawn at, and their sheets' ends: 100 and 150 %, and 100 % with Windows' text at 150 %.</summary>
    internal static readonly (float Scale, float Text, string Name)[] Sizes = [(1f, 1f, "100"), (1.5f, 1f, "150"), (1f, 1.5f, "100-text150")];

    /// <summary>
    /// Each line one row to four high at the narrow and the medium list, at 100 and 150 % and with
    /// Windows' text at 150 %, dark and light: markets-fit-lines-&lt;look&gt;-&lt;size&gt;.png, a column a line.
    /// </summary>
    [UiFact]
    public void TheLinesAtEveryHeightAndWidth()
    {
        foreach (var (scale, text, size) in Sizes)
        {
            foreach (var dark in new[] { true, false })
            {
                OnStaThread(gfx =>
                {
                    var store = MarketOffscreenTests.Settled(Answers());
                    var lines = Shown().Select((shown, index) => Line(gfx, store, $"fit{index}", shown.Options)).ToList();
                    try
                    {
                        var look = ListFormPictures.LookFor(dark, text);
                        var theme = FlyoutTheme.For(look);
                        var style = FolderFlyout.StyleFor(scale, look, theme);
                        var gap = 12 * scale;
                        var boxes = Widths.SelectMany(width => Enumerable.Range(1, 4).Select(rows => (Width: FolderFlyout.ListWidth(scale, width), Height: LineRows.Px(rows, style)))).ToList();
                        var widest = boxes.Max(box => box.Width);
                        var sheetWidth = gap + (lines.Count * (widest + gap));
                        var sheetHeight = gap + boxes.Sum(box => box.Height + gap);
                        var shown = Shown();
                        // What each line drew at each box: its figures, each with the room it was given.
                        var drawn = new List<(string Where, MarketOptions Options, IReadOnlyList<MarketFigure> Figures)>();
                        Bitmap(sheetWidth, sheetHeight, dc =>
                        {
                            PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, sheetWidth, sheetHeight), 0, PopupPaint.Ink(theme.Base));
                            var y = gap;
                            foreach (var (width, height) in boxes)
                            {
                                var x = gap;
                                for (var index = 0; index < lines.Count; index++)
                                {
                                    var line = lines[index];
                                    var box = PopupPaint.Rect(x, y, width, height);
                                    PopupPaint.Fill(dc, box, 4 * style.Scale, PopupPaint.Ink(theme.CardBackground));
                                    line.PaintOn(dc, box, style, WidgetSurface.List);
                                    drawn.Add(($"{shown[index].Caption}, {width} by {height} px at {size}", shown[index].Options, [.. line.Figured]));
                                    x += widest + gap;
                                }
                                y += height + gap;
                            }
                        }, bitmap => Save(bitmap, Path.Combine(Pictures, $"markets-fit-lines-{(dark ? "dark" : "light")}-{size}.png")));

                        Assert.All(lines, line => Assert.Equal(MarketShows.Ready, line.Reading().State));
                        Assert.Equal(("734,567.89", "+12.34%"), (lines[0].Reading().Price, lines[0].Reading().Percent));
                        Assert.Equal(("0.000123", "+112.34%"), (lines[3].Reading().Price, lines[3].Reading().Percent));
                        foreach (var (where, options, figures) in drawn)
                        {
                            // The price is drawn on every line at every height, whole: its measure in the tabular
                            // figures it is drawn in within its room.
                            Assert.True(figures.Count > 0, $"{where}: no price");
                            Assert.All(figures, figure => Assert.True(
                                TextLine.Measure(gfx, figure.Text, figure.Px, figure.Weight, null) <= figure.Room + 0.5f,
                                $"{where}: \"{figure.Text}\" is {TextLine.Measure(gfx, figure.Text, figure.Px, figure.Weight, null)} px in {figure.Room} px"));
                            // At the app's own text size every line of the full style shows its change, whole, at every height.
                            if (text == 1f && options.Style != MarketStyle.Compact)
                            {
                                Assert.True(figures.Count >= 2, $"{where}: no change beside the price");
                            }
                        }
                    }
                    finally
                    {
                        lines.ForEach(line => line.Dispose());
                    }
                });
            }
        }
    }

    /// <summary>The bars a face is drawn on, in DIP: across one of 32 and of 48; down one of 32, 48 and 64.</summary>
    private static readonly (bool Vertical, float Dip)[] Bars = [(false, 32), (false, 48), (true, 32), (true, 48), (true, 64)];

    /// <summary>
    /// Each face across a bar of 32 and 48 DIP and down one of 32, 48 and 64, at 100 and 150 %, dark and
    /// light: markets-fit-faces-&lt;look&gt;-&lt;scale&gt;.png, a row a bar.
    /// </summary>
    [UiFact]
    public void TheFacesOnEveryBar()
    {
        foreach (var (scale, text, size) in Sizes)
        {
            foreach (var dark in new[] { true, false })
            {
                OnStaThread(gfx =>
                {
                    var store = MarketOffscreenTests.Settled(Answers());
                    var folders = Shown().Select((shown, index) => Folder(gfx, store, $"fitface{index}", shown.Options)).ToList();
                    try
                    {
                        var look = ListFormPictures.LookFor(dark, text);
                        var theme = FlyoutTheme.For(look);
                        var gap = 12 * scale;
                        var rows = Bars.Select(bar => new IslandStyle(scale, bar.Vertical, bar.Dip * scale, 24 * scale, look))
                            .Select(style => (Style: style, Sizes: folders.Select(folder => folder.CellSize(0, style)).ToList()))
                            .ToList();
                        var sheetWidth = gap + rows.Max(row => row.Sizes.Sum(size => size.Width + gap));
                        var sheetHeight = gap + rows.Sum(row => row.Sizes.Max(size => size.Height) + gap);
                        Bitmap(sheetWidth, sheetHeight, dc =>
                        {
                            PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, sheetWidth, sheetHeight), 0, PopupPaint.Ink(theme.Base));
                            var y = gap;
                            foreach (var (style, sizes) in rows)
                            {
                                var x = gap;
                                for (var index = 0; index < folders.Count; index++)
                                {
                                    var (w, h) = sizes[index];
                                    var folder = folders[index];
                                    PopupPaint.Fill(dc, PopupPaint.Rect(x, y, w, h), 4 * scale, PopupPaint.Ink(theme.CardBackground));
                                    Shifted(dc, x, y, () => folder.PaintCell(dc, 0, style, w, h));
                                    x += w + gap;
                                }
                                y += sizes.Max(size => size.Height) + gap;
                            }
                        }, bitmap => Save(bitmap, Path.Combine(Pictures, $"markets-fit-faces-{(dark ? "dark" : "light")}-{size}.png")));

                        var shown = Shown();
                        foreach (var (style, _) in rows)
                        {
                            for (var index = 0; index < folders.Count; index++)
                            {
                                var folder = folders[index];
                                var context = new FaceContext(gfx, style, folder.Options, folder);
                                var reading = folder.MarketReading();
                                var parts = MarketFaceDrawing.Parts(context, folder.Options.Face.Market!, reading);
                                var where = $"{shown[index].Caption} {(style.Vertical ? "down" : "across")} a bar of {style.Thickness} px at {size}";
                                if (!style.Vertical)
                                {
                                    // Across a bar the face is as long as what it says: the price as it reads, and the change.
                                    Assert.Equal(reading.Price, parts.Price);
                                    Assert.Equal(shown[index].Options.Style == MarketStyle.Compact ? "" : reading.Percent, parts.Change);
                                    continue;
                                }
                                // Down a bar its rows are the bar's room across: the price in it whole, in a shorter
                                // form where its own does not fit at the smallest size — and only where none fits at
                                // all, a bar too thin for any, the shortest; the change whole where it has a row.
                                var room = VerticalFit.Room(context.Fit);
                                var forms = MarketText.PriceForms(reading.Price);
                                var none = !forms.Any(form => VerticalFit.Fits(gfx, MarketIsland.Shape(form), room, Weight.Regular, VerticalFit.Ladder(context.Fit)[^1]));
                                Assert.Contains(parts.Price, forms);
                                if (none)
                                {
                                    Assert.Equal(forms[^1], parts.Price);
                                }
                                else
                                {
                                    Assert.True(parts.PriceWidth <= room + 0.5f, $"{where}: \"{parts.Price}\" is {parts.PriceWidth} px in {room} px");
                                }
                                if (parts.Change.Length > 0)
                                {
                                    Assert.Contains(parts.Change, MarketText.ChangeForms(reading.Percent));
                                    Assert.True(parts.ChangeWidth <= room + 0.5f, $"{where}: \"{parts.Change}\" is {parts.ChangeWidth} px in {room} px");
                                }
                            }
                        }
                        // The thousands of a share on a bar of 32 DIP are said in thousands, never cut: "734.6K".
                        var thin = new IslandStyle(scale, true, 32 * scale, 24 * scale, look);
                        Assert.Equal("734.6K", MarketFaceDrawing.Parts(new FaceContext(gfx, thin, folders[0].Options, folders[0]), folders[0].Options.Face.Market!, folders[0].MarketReading()).Price);
                    }
                    finally
                    {
                        folders.ForEach(folder => folder.Dispose());
                    }
                });
            }
        }
    }
}
