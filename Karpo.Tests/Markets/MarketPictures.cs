// markets: the widget drawn (docs\widgets.md §246) — its line at one row to six and its face on the bar, for
// a share, a rate (full, compact and the converter) and a coin, dark at 150 %: markets-lines-<dark|light>.png,
// markets-faces-<dark|light>.png (across a bar and down one). The sheets go to KARPO_PNG_DIR (else
// %TEMP%\karpo-tests\pictures) before anything is asserted; runs only with KARPO_UI_TESTS=1.
//
// What is held: each face is as wide as what it says now (§245) — the converter's sentence wider than a
// rate alone, the change wider than none — and one size down a bar on its side whatever it reads; the
// converter types either way and turns its pair round.

using Karpo.Dock;
using Karpo.Dock.Markets;
using Karpo.Tests.Folders;
using Karpo.Tests.Forecasts;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using static Karpo.Tests.Islands.Offscreen;

namespace Karpo.Tests.Markets;

public sealed unsafe class MarketPictures
{
    /// <summary>The prices every picture reads, kept a moment ago: a share's day, a rate's day and month, a coin's month.</summary>
    private static Dictionary<string, string> Answers() => new()
    {
        ["MSFT"] = MarketOffscreenTests.Answer("MSFT", "Microsoft Corporation", price: 412.34, percent: 1.24, previous: 407.3),
        ["EURCHF=X"] = MarketOffscreenTests.Answer("EURCHF=X", "EUR/CHF", "CHF", price: 0.9269, percent: -0.0205, previous: 0.927, wander: 0.001),
        ["EURCHF=X 1mo"] = MarketRangeTests.Ranged("EURCHF=X", 150, 3600 * 4, 0.938, 0.9269, 0.941, wander: 0.004, name: "EUR/CHF"),
        ["USDJPY=X"] = MarketOffscreenTests.Answer("USDJPY=X", "USD/JPY", "JPY", price: 151.23, percent: 0.31, previous: 150.76, wander: 0.2),
        ["BTC-USD 1mo"] = MarketRangeTests.Ranged("BTC-USD", 150, 3600 * 5, 76200, 81917.38, 75100, wander: 2400, name: "Bitcoin USD"),
        ["ETH-CHF 1mo"] = MarketRangeTests.Ranged("ETH-CHF", 150, 3600 * 5, 3400, 3212.5, 3480, wander: 120, name: "Ethereum USD"),
    };

    /// <summary>What each picture shows: a caption and its record.</summary>
    private static List<(string Caption, MarketOptions Options)> Shown() =>
    [
        ("Share, a day", new MarketOptions { Symbol = "MSFT", Range = MarketRange.Day, Holding = 10 }),
        ("Rate, a month", new MarketOptions { Symbol = "EURCHF=X", Range = MarketRange.Month }),
        ("Rate alone", new MarketOptions { Symbol = "USDJPY=X", Range = MarketRange.Day, Style = MarketStyle.Compact }),
        ("Converter", new MarketOptions { Symbol = "EURCHF=X", Amount = 100, Range = MarketRange.Day, Style = MarketStyle.Converter }),
        ("Coin, a month", new MarketOptions { Symbol = "BTC-USD", Range = MarketRange.Month, Holding = 0.5 }),
        ("Coin in francs", new MarketOptions { Symbol = "ETH-CHF", Range = MarketRange.Month }),
    ];

    private static MarketIsland Line(Gfx gfx, MarketStore store, string id, MarketOptions options)
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
    private static FolderIsland Folder(Gfx gfx, MarketStore store, string id, MarketOptions face)
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

    /// <summary>Each line at one row to six, side by side: markets-lines-&lt;dark|light&gt;.png at 150 %.</summary>
    [UiFact]
    public void TheLinesAtEveryHeight()
    {
        foreach (var dark in new[] { true, false })
        {
            OnStaThread(gfx =>
            {
                var store = MarketOffscreenTests.Settled(Answers());
                var lines = Shown().Select((shown, index) => (shown.Caption, Line: Line(gfx, store, $"m{index}", shown.Options))).ToList();
                try
                {
                    var look = ListFormPictures.LookFor(dark);
                    var theme = FlyoutTheme.For(look);
                    var style = FolderFlyout.StyleFor(1.5f, look, theme);
                    var width = FolderFlyout.ListWidth(1.5f);
                    const float gap = 18;
                    var heights = Enumerable.Range(1, 6).Select(rows => LineRows.Px(rows, style)).ToList();
                    var sheetWidth = gap + (lines.Count * (width + gap));
                    var sheetHeight = gap + heights.Sum(height => height + gap);
                    Bitmap(sheetWidth, sheetHeight, dc =>
                    {
                        PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, sheetWidth, sheetHeight), 0, PopupPaint.Ink(theme.Base));
                        var y = gap;
                        foreach (var height in heights)
                        {
                            var x = gap;
                            foreach (var (_, line) in lines)
                            {
                                var box = PopupPaint.Rect(x, y, width, height);
                                PopupPaint.Fill(dc, box, 4 * style.Scale, PopupPaint.Ink(theme.CardBackground));
                                line.PaintOn(dc, box, style, WidgetSurface.List);
                                x += width + gap;
                            }
                            y += height + gap;
                        }
                    }, bitmap => Save(bitmap, Path.Combine(Pictures, $"markets-lines-{(dark ? "dark" : "light")}.png")));

                    // Each reads what it should: a share its company, a rate its pair's names, a coin its name and currency.
                    Assert.All(lines, each => Assert.Equal(MarketShows.Ready, each.Line.Reading().State));
                    Assert.Equal("Microsoft Corporation", lines[0].Line.Reading().Name);
                    Assert.Equal(("EUR/CHF", "0.9269", "CHF"), (lines[1].Line.Reading().Symbol, lines[1].Line.Reading().Price, lines[1].Line.Reading().Currency));
                    Assert.Equal(("USD/JPY", "151.23"), (lines[2].Line.Reading().Symbol, lines[2].Line.Reading().Price));
                    Assert.NotNull(lines[3].Line.Converter);
                    // §494: with an amount owned, its worth first and the price of one where the worth stood.
                    Assert.Equal(("BTC", "Bitcoin · USD", "40,958.69"), (lines[4].Line.Reading().Symbol, lines[4].Line.Reading().Name, lines[4].Line.Reading().Price));
                    Assert.Equal(("0.5 BTC × 81,917.38 USD", "81,917.38 USD"), lines[4].Line.Reading().Holding);
                    Assert.Equal("3,212.50", lines[5].Line.Reading().Price);
                }
                finally
                {
                    foreach (var (_, line) in lines)
                    {
                        line.Dispose();
                    }
                }
            });
        }
    }

    /// <summary>
    /// Each face on a 48 DIP bar at 150 %, across and down: markets-faces-&lt;dark|light&gt;.png — as wide as it
    /// reads, the converter's sentence wider than the rate alone, and one size down a bar on its side.
    /// </summary>
    [UiFact]
    public void TheFacesOnTheBar()
    {
        foreach (var dark in new[] { true, false })
        {
            OnStaThread(gfx =>
            {
                var store = MarketOffscreenTests.Settled(Answers());
                var folders = Shown().Select((shown, index) => Folder(gfx, store, $"face{index}", shown.Options)).ToList();
                try
                {
                    var look = ListFormPictures.LookFor(dark);
                    var theme = FlyoutTheme.For(look);
                    var across = new IslandStyle(1.5f, false, 72, 36, look);
                    var down = new IslandStyle(1.5f, true, 72, 36, look);
                    const float gap = 18;
                    var wide = folders.Select(folder => folder.CellSize(0, across)).ToList();
                    var tall = folders.Select(folder => folder.CellSize(0, down)).ToList();
                    var width = gap + Math.Max(wide.Sum(size => size.Width + gap), tall.Sum(size => size.Width + gap));
                    var height = gap + wide.Max(size => size.Height) + gap + tall.Max(size => size.Height) + gap;
                    Bitmap(width, height, dc =>
                    {
                        PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, width, height), 0, PopupPaint.Ink(theme.Base));
                        var x = gap;
                        for (var index = 0; index < folders.Count; index++)
                        {
                            var (w, h) = wide[index];
                            var folder = folders[index];
                            PopupPaint.Fill(dc, PopupPaint.Rect(x, gap, w, h), 4 * across.Scale, PopupPaint.Ink(theme.CardBackground));
                            Shifted(dc, x, gap, () => folder.PaintCell(dc, 0, across, w, h));
                            x += w + gap;
                        }
                        x = gap;
                        var top = gap + wide.Max(size => size.Height) + gap;
                        for (var index = 0; index < folders.Count; index++)
                        {
                            var (w, h) = tall[index];
                            var folder = folders[index];
                            PopupPaint.Fill(dc, PopupPaint.Rect(x, top, w, h), 4 * down.Scale, PopupPaint.Ink(theme.CardBackground));
                            Shifted(dc, x, top, () => folder.PaintCell(dc, 0, down, w, h));
                            x += w + gap;
                        }
                    }, bitmap => Save(bitmap, Path.Combine(Pictures, $"markets-faces-{(dark ? "dark" : "light")}.png")));

                    // As wide as it says now: the converter's sentence ("100 EUR = 92.69 CHF") wider than a rate
                    // alone ("USD/JPY 151.23"), and a rate with its change ("EUR/CHF 0.9269 -0.02%") wider than that.
                    Assert.Contains("92.69", folders[3].FaceShown, StringComparison.Ordinal);
                    Assert.True(wide[3].Width > wide[2].Width, $"the converter {wide[3].Width} px, a rate alone {wide[2].Width} px");
                    Assert.True(wide[1].Width > wide[2].Width, $"a rate with its change {wide[1].Width} px, alone {wide[2].Width} px");
                }
                finally
                {
                    foreach (var folder in folders)
                    {
                        folder.Dispose();
                    }
                }
            });
        }
    }

    /// <summary>
    /// §494's pictures, dark and light at 150 %: a share listed in dollars as listed and shown in francs, each
    /// without and with ten owned, on a line one to four rows high (markets-currency-lines-&lt;look&gt;.png) and
    /// on its face across a bar and down one (markets-currency-faces-&lt;look&gt;.png). Each reads in the currency
    /// it is shown in, a holding's worth first, and hovering a line shown in francs says both and the rate.
    /// </summary>
    [UiFact]
    public void TheCurrencyShown()
    {
        var answers = new Dictionary<string, string>
        {
            ["MSFT"] = MarketOffscreenTests.Answer("MSFT", "Microsoft Corporation", price: 640, percent: 1.24, previous: 632.16, wander: 2),
            ["USDCHF=X"] = MarketOffscreenTests.Answer("USDCHF=X", "USD/CHF", "CHF", price: 0.8, percent: 0.1, previous: 0.799, wander: 0.0001),
        };
        List<(string Caption, MarketOptions Options)> shown =
        [
            ("As listed", new MarketOptions { Symbol = "MSFT", Range = MarketRange.Day }),
            ("In francs", new MarketOptions { Symbol = "MSFT", Range = MarketRange.Day, Currency = "CHF" }),
            ("As listed, 10 owned", new MarketOptions { Symbol = "MSFT", Range = MarketRange.Day, Holding = 10 }),
            ("In francs, 10 owned", new MarketOptions { Symbol = "MSFT", Range = MarketRange.Day, Currency = "CHF", Holding = 10 }),
        ];
        foreach (var dark in new[] { true, false })
        {
            OnStaThread(gfx =>
            {
                var store = MarketOffscreenTests.Settled(answers);
                var lines = shown.Select((each, index) => Line(gfx, store, $"cur{index}", each.Options)).ToList();
                var folders = shown.Take(2).Select((each, index) => Folder(gfx, store, $"curf{index}", each.Options)).ToList();
                try
                {
                    var look = ListFormPictures.LookFor(dark);
                    var theme = FlyoutTheme.For(look);
                    var style = FolderFlyout.StyleFor(1.5f, look, theme);
                    var width = FolderFlyout.ListWidth(1.5f);
                    const float gap = 18;
                    var heights = Enumerable.Range(1, 4).Select(rows => LineRows.Px(rows, style)).ToList();
                    var sheetWidth = gap + (lines.Count * (width + gap));
                    var sheetHeight = gap + heights.Sum(height => height + gap);
                    Bitmap(sheetWidth, sheetHeight, dc =>
                    {
                        PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, sheetWidth, sheetHeight), 0, PopupPaint.Ink(theme.Base));
                        var y = gap;
                        foreach (var height in heights)
                        {
                            var x = gap;
                            foreach (var line in lines)
                            {
                                var box = PopupPaint.Rect(x, y, width, height);
                                PopupPaint.Fill(dc, box, 4 * style.Scale, PopupPaint.Ink(theme.CardBackground));
                                line.PaintOn(dc, box, style, WidgetSurface.List);
                                x += width + gap;
                            }
                            y += height + gap;
                        }
                    }, bitmap => Save(bitmap, Path.Combine(Pictures, $"markets-currency-lines-{(dark ? "dark" : "light")}.png")));

                    var across = new IslandStyle(1.5f, false, 72, 36, look);
                    var down = new IslandStyle(1.5f, true, 72, 36, look);
                    var wide = folders.Select(folder => folder.CellSize(0, across)).ToList();
                    var tall = folders.Select(folder => folder.CellSize(0, down)).ToList();
                    var faceWidth = gap + wide.Sum(size => size.Width + gap) + tall.Sum(size => size.Width + gap);
                    var faceHeight = gap + Math.Max(wide.Max(size => size.Height), tall.Max(size => size.Height)) + gap;
                    Bitmap(faceWidth, faceHeight, dc =>
                    {
                        PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, faceWidth, faceHeight), 0, PopupPaint.Ink(theme.Base));
                        var x = gap;
                        foreach (var (folder, (w, h), cell) in folders.Zip(wide).Select(pair => (pair.First, pair.Second, across))
                            .Concat(folders.Zip(tall).Select(pair => (pair.First, pair.Second, down))))
                        {
                            PopupPaint.Fill(dc, PopupPaint.Rect(x, gap, w, h), 4 * cell.Scale, PopupPaint.Ink(theme.CardBackground));
                            Shifted(dc, x, gap, () => folder.PaintCell(dc, 0, cell, w, h));
                            x += w + gap;
                        }
                    }, bitmap => Save(bitmap, Path.Combine(Pictures, $"markets-currency-faces-{(dark ? "dark" : "light")}.png")));

                    var said = lines.Select(line => line.Reading()).Select(reading => (reading.Price, reading.Currency, reading.Holding?.Short)).ToList();
                    Assert.Equal(("640.00", "USD", (string?)null), said[0]);
                    Assert.Equal(("512.00", "CHF", (string?)null), said[1]);
                    Assert.Equal(("6,400.00", "USD", "640.00 USD"), said[2]);
                    Assert.Equal(("5,120.00", "CHF", "512.00 CHF"), said[3]);
                    var box = PopupPaint.Rect(0, 0, width, heights[2]);
                    Assert.Null(lines[0].CardTip(default, box, style, WidgetSurface.List));
                    Assert.Equal("512.00 CHF (640.00 USD × 0.8000)", lines[1].CardTip(default, box, style, WidgetSurface.List)?.Text);
                    Assert.Equal("5,120.00 CHF (6,400.00 USD × 0.8000)\n10 shares × 512.00 CHF", lines[3].CardTip(default, box, style, WidgetSurface.List)?.Text);
                    Assert.Contains("512.00", folders[1].FaceShown, StringComparison.Ordinal);
                    Assert.True(wide[1].Width > 0 && tall[1].Height > 0);
                }
                finally
                {
                    lines.ForEach(line => line.Dispose());
                    folders.ForEach(folder => folder.Dispose());
                }
            });
        }
    }

    /// <summary>§258: the answers the chart styles are drawn from: open, high, low and close a point, but the closes of the last.</summary>
    private static Dictionary<string, string> Charted()
    {
        var yesterday = DateTime.UtcNow.Date.AddHours(-2);
        return new()
        {
            ["MSFT 1mo"] = MarketChartTests.Bars("MSFT", 24 * 30, 3600, 405.2, 412.34, 9, "Microsoft Corporation"),
            ["EURCHF=X"] = MarketChartTests.Bars("EURCHF=X", 264, 300, 0.9262, 0.9271, 0.0012, "EUR/CHF", "CHF", yesterday, day: true),
            ["BTC-USD 6mo"] = MarketChartTests.Bars("BTC-USD", 182, 86400, 62000, 81917.38, 6000, "Bitcoin USD"),
            ["ETH-CHF 1mo"] = MarketRangeTests.Ranged("ETH-CHF", 150, 3600 * 5, 3400, 3212.5, 3480, wander: 120, name: "Ethereum USD"),
        };
    }

    /// <summary>§258: a share over a month, a pair over a day and a coin over six months, each in the area, the line and candles; and candles of closes alone.</summary>
    private static List<(string Caption, MarketOptions Options)> Charts()
    {
        List<(string, MarketOptions)> shown = [];
        foreach (var (caption, symbol, range) in new[] { ("Share", "MSFT", MarketRange.Month), ("Pair", "EURCHF=X", MarketRange.Day), ("Coin", "BTC-USD", MarketRange.SixMonths) })
        {
            foreach (var chart in MarketRows.Charts)
            {
                shown.Add(($"{caption} {chart}", new MarketOptions { Symbol = symbol, Range = range, Chart = chart }));
            }
        }
        shown.Add(("Closes alone, candles", new MarketOptions { Symbol = "ETH-CHF", Range = MarketRange.Month, Chart = MarketChartStyle.Candles }));
        return shown;
    }

    /// <summary>
    /// §258's pictures, dark and light at 150 %: each chart style on a line one to three rows high
    /// (markets-charts-lines-&lt;look&gt;.png, a row of lines each) and on the face across a bar and down one
    /// (markets-charts-faces-&lt;look&gt;.png) — and each reads the chart it is set to.
    /// </summary>
    [UiFact]
    public void TheChartStyles()
    {
        foreach (var dark in new[] { true, false })
        {
            OnStaThread(gfx =>
            {
                var store = MarketOffscreenTests.Settled(Charted());
                var shown = Charts();
                var lines = shown.Select((each, index) => Line(gfx, store, $"c{index}", each.Options)).ToList();
                var folders = shown.Select((each, index) => Folder(gfx, store, $"cf{index}", each.Options)).ToList();
                try
                {
                    var look = ListFormPictures.LookFor(dark);
                    var theme = FlyoutTheme.For(look);
                    var style = FolderFlyout.StyleFor(1.5f, look, theme);
                    var width = FolderFlyout.ListWidth(1.5f);
                    const float gap = 18;
                    var heights = Enumerable.Range(1, 3).Select(rows => LineRows.Px(rows, style)).ToList();
                    var sheetWidth = gap + heights.Count * (width + gap);
                    var sheetHeight = gap + (lines.Count * (heights[^1] + gap));
                    Bitmap(sheetWidth, sheetHeight, dc =>
                    {
                        PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, sheetWidth, sheetHeight), 0, PopupPaint.Ink(theme.Base));
                        var y = gap;
                        foreach (var line in lines)
                        {
                            var x = gap;
                            foreach (var height in heights)
                            {
                                var box = PopupPaint.Rect(x, y, width, height);
                                PopupPaint.Fill(dc, box, 4 * style.Scale, PopupPaint.Ink(theme.CardBackground));
                                line.PaintOn(dc, box, style, WidgetSurface.List);
                                x += width + gap;
                            }
                            y += heights[^1] + gap;
                        }
                    }, bitmap => Save(bitmap, Path.Combine(Pictures, $"markets-charts-lines-{(dark ? "dark" : "light")}.png")));

                    var across = new IslandStyle(1.5f, false, 72, 36, look);
                    var down = new IslandStyle(1.5f, true, 72, 36, look);
                    var wide = folders.Select(folder => folder.CellSize(0, across)).ToList();
                    var tall = folders.Select(folder => folder.CellSize(0, down)).ToList();
                    var faceWidth = gap + wide.Max(size => size.Width) + gap + tall.Max(size => size.Width) + gap;
                    var faceHeight = gap + folders.Select((_, index) => Math.Max(wide[index].Height, tall[index].Height) + gap).Sum();
                    Bitmap(faceWidth, faceHeight, dc =>
                    {
                        PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, faceWidth, faceHeight), 0, PopupPaint.Ink(theme.Base));
                        var y = gap;
                        for (var index = 0; index < folders.Count; index++)
                        {
                            var folder = folders[index];
                            var (w, h) = wide[index];
                            PopupPaint.Fill(dc, PopupPaint.Rect(gap, y, w, h), 4 * across.Scale, PopupPaint.Ink(theme.CardBackground));
                            Shifted(dc, gap, y, () => folder.PaintCell(dc, 0, across, w, h));
                            var x = gap + wide.Max(size => size.Width) + gap;
                            var (tw, th) = tall[index];
                            PopupPaint.Fill(dc, PopupPaint.Rect(x, y, tw, th), 4 * down.Scale, PopupPaint.Ink(theme.CardBackground));
                            Shifted(dc, x, y, () => folder.PaintCell(dc, 0, down, tw, th));
                            y += Math.Max(h, th) + gap;
                        }
                    }, bitmap => Save(bitmap, Path.Combine(Pictures, $"markets-charts-faces-{(dark ? "dark" : "light")}.png")));

                    for (var index = 0; index < lines.Count; index++)
                    {
                        var reading = lines[index].Reading();
                        Assert.Equal((MarketShows.Ready, shown[index].Options.Chart), (reading.State, reading.Chart));
                        Assert.True(reading.Candles.Count >= 2, shown[index].Caption);
                    }
                }
                finally
                {
                    lines.ForEach(line => line.Dispose());
                    folders.ForEach(folder => folder.Dispose());
                }
            });
        }
    }

    /// <summary>
    /// The converter types either way: an amount in the first field is worth its rate in the second, one in
    /// the second its worth back in the first; the swap turns the pair round, the amount given where it was.
    /// </summary>
    [UiFact]
    public void TheConverterTypesEitherWayAndSwaps()
    {
        OnStaThread(gfx =>
        {
            var store = MarketOffscreenTests.Settled(Answers());
            using var line = Line(gfx, store, "converter", Shown()[3].Options);
            var converter = line.Converter!;
            var reading = line.Reading();
            Assert.Equal(("100", "92.69"), converter.Texts(reading));
            converter.Begin(1, selectAll: true, reading);
            converter.Type("50");
            Assert.Equal(("53.94", "50"), converter.Texts(line.Reading()));
            converter.StopTyping();
            converter.Swap();
            Assert.True(converter.Swapped);
            // CHF to EUR now: the 50 given stays in the second field, in euros, worth its francs in the first.
            Assert.Equal(("46.34", "50"), converter.Texts(line.Reading()));
            Assert.StartsWith("100 EUR = 92.69 CHF\n1 EUR = 0.9269 CHF", line.Reading().Tooltip(), StringComparison.Ordinal);
        });
    }
}
