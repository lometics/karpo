// The markets preset (docs\widgets.md §98, §246): what a click on the stock entry gave — its panel: the
// symbol and the company, the price large with its currency and its change, whether the market
// trades, the day or the range as a graph — is a folder holding the markets line at the height of its
// tallest form under the markets face: the ticker the entry was, "MSFT 412.34 +1.2%", painted again in
// place as the price moves. Every store here is a stand-in (MarketOffscreenTests' answers): nothing is
// fetched.
//
// Pictured beside the panel it replaces (rendered before it went: stock-<state>-<dark|light>.png and
// the entry's stock-bar-*.png): stock-preset-<dark|light>.png at 150 %, and the face on the bar,
// stock-face-<dark|light>.png.

using System.Numerics;
using System.Text.Json;
using TerraFX.Interop.DirectX;
using Karpo.Dock;
using Karpo.Dock.Markets;
using Karpo.Tests.Forecasts;
using Karpo.Tests.Islands;
using static Karpo.Tests.Folders.ListFormPictures;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Folders;

public sealed unsafe class MarketPresetTests
{
    private static readonly PresetMachine Machine = new(HasBattery: false, FixedDrives: []);

    /// <summary>
    /// The markets preset folder, as the add list makes it, its line set to <paramref name="options"/>
    /// and its face to <paramref name="face"/> — the line's own where none is named — in the compact
    /// style where <paramref name="change"/> is off, and a line more for each of <paramref name="more"/>.
    /// </summary>
    private static BarItem Holder(MarketOptions? options = null, bool change = true, MarketOptions? face = null, params MarketOptions[] more)
    {
        var holder = Karpo.Tests.Forecasts.MarketOffscreenTests.OneLineFolder(Machine);
        holder.Id = "stocks";
        holder.Folder!.Cells.Single().Island.Market = options ?? new MarketOptions();
        foreach (var share in more)
        {
            CellItems.Put(holder.Folder, new CellItem { Id = $"s{holder.Folder.Cells.Count()}", View = FolderView.List, Kind = "markets", Height = LineHeight.Exactly(4), Market = share });
        }
        holder.Folder.Face.Market = (face ?? options)?.Copy();
        if (!change)
        {
            MarketFaces.Kept(holder.Folder.Face).Style = MarketStyle.Compact;
        }
        holder.Normalize();
        return holder;
    }

    /// <summary>The preset's folder over <paramref name="store"/>, never the network; its face's share and its lines' prices taken once they came, where <paramref name="answers"/>.</summary>
    private static FolderIsland Made(Gfx gfx, MarketStore store, MarketOptions? options, bool change = true, string? key = null, bool answers = true, MarketOptions? face = null, params MarketOptions[] more)
    {
        var holder = Holder(options, change, face, more);
        MarketIsland.Made = store;
        try
        {
            var folder = new FolderIsland(key ?? $"{Owner}/{holder.Id}", holder, gfx, new Config());
            if (answers)
            {
                Assert.True(Patience.Until(() => !folder.MarketQuote.Fetching), "the face's fetch never came back");
                foreach (var line in folder.Tiles.Select(tile => tile.Island).OfType<MarketIsland>())
                {
                    Assert.True(Patience.Until(() => !line.Quote.Fetching), "a line's fetch never came back");
                }
            }
            _ = ((IIsland)folder).Refresh(gfx);
            return folder;
        }
        finally
        {
            MarketIsland.Made = null;
        }
    }

    private static MarketOptions Held(string symbol, string label = "") => MarketOffscreenTests.Held(symbol, label);

    /// <summary>A rate or a coin by the price service's symbol ("EURCHF=X"), over a day.</summary>
    private static MarketOptions Quoted(string symbol)
    {
        var options = new MarketOptions { Symbol = MarketSymbols.Canonical(symbol) };
        options.Range = MarketRange.Day;
        return options;
    }

    /// <summary>A store that kept a price <paramref name="agoMinutes"/> ago and cannot reach its service now: the last price stands, stale.</summary>
    private static MarketStore Stale(string symbol, string json, int agoMinutes = 90)
    {
        List<MarketCache> kept =
        [
            new MarketCache
            {
                FetchedUtc = Clock.Now().ToUniversalTime().AddMinutes(-agoMinutes),
                Key = MarketOffscreenTests.KeyOf(symbol),
                Saved = MarketSaved.Of(MarketQuotes.Parse(json)),
            },
        ];
        var file = JsonSerializer.Serialize(new MarketFile { Prices = kept }, MarketJson.Default.MarketFile);
        return new(new MarketAnswers((_, _) => throw new HttpRequestException("no route")), new MarketDisk { Read = () => file, Write = _ => { } });
    }

    private static IslandStyle Bar(int height = 48, uint dpi = 96, bool vertical = false, float text = 1f, bool dark = true)
    {
        var config = new Config { Height = height };
        return new IslandStyle(dpi / 96f, vertical, Layout.CellPx(dpi, config), Layout.IconPx(dpi, config), LookFor(dark, text));
    }

    // --- The preset ------------------------------------------------------------------------------------

    /// <summary>§432: the rows a markets line's Full form is where nothing measures it, its words and its chart.</summary>
    private static int FullRows => LineSizes.CellsOf(ListWidgets.Of(ListKind.Markets), LineSize.Full);

    /// <summary>
    /// §648: the Watchlist's four shares, each the markets line at its Full form, under the S&amp;P 500's ticker — set, so
    /// nothing in it asks to be set up; the face has no name and one option of its own, where it stands. One preset of shares:
    /// the Watchlist ("Stocks") and Markets, its one line with no symbol yet, were the same widget twice, and are this one.
    /// </summary>
    [Fact]
    public void ThePresetIsTheSharesUnderTheIndexsTicker()
    {
        var preset = Presets.Find(Presets.MarketId)!;
        Assert.Equal(FolderFaces.MarketsId, preset.Face!.Kind);
        Assert.Equal(["markets", "markets", "markets", "markets"], preset.Items.Select(item => item.Form));
        Assert.All(preset.Items, item => Assert.Equal((null, LineSize.Full), (item.Height, item.Size)));
        Assert.Equal([FolderListWidth.Wide], preset.Widths);

        var folder = Presets.Folder(preset, Machine).Folder!;
        Assert.Same(FolderFaces.Markets, folder.Face.Info);
        Assert.Empty(folder.Face.Info.Options);
        Assert.Equal(MarketStyle.Full, folder.Face.Market!.StyleOf);
        Assert.Equal(WidgetInfos.Markets.Glyph, folder.Face.Info.Glyph);
        Assert.True(folder.Face.Market.IsSet);
        Assert.Equal(("^GSPC", MarketKind.Stock), (folder.Face.Market.Symbol, MarketFaces.Of(folder)!.KindOf));
        Assert.Equal(["MSFT", "AAPL", "NVDA", "^GSPC"], folder.Cells.Select(cell => cell.Island.Market!.Symbol));
        Assert.All(folder.Cells, cell =>
        {
            Assert.Equal(ListKind.Markets, cell.Island.List);
            Assert.Null(WidgetSetup.Needs(cell.Island));
        });
        Assert.Null(Presets.Find("stocksGroup"));
        Assert.Single(Presets.All, each => each.Items.All(item => item.Form == "markets"));
    }

    /// <summary>The entry is gone from the bar, and with it the switch only the bar read.</summary>
    [Fact]
    public void TheEntryIsGone()
    {
        Assert.Null(BarEntries.Find("stock"));
        Assert.Null(ListWidgets.Find("stock"));
        Assert.NotNull(ListWidgets.Find("markets"));
        Assert.DoesNotContain("stock", Enum.GetValues<BarKind>().Select(kind => kind.Id()));
        Assert.Null(typeof(MarketOptions).GetProperty("ShowChange"));
    }

    /// <summary>
    /// The preset's rows hold the line a new stock asks for — its words and its chart at its tallest
    /// under them — at 100 % and 150 %.
    /// </summary>
    [UiFact]
    public void ThePresetsRowsHoldTheLine()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var options = Held("MSFT");
            options.Range = MarketRange.FiveYears;
            options.Holding = 12.5;
            var store = MarketOffscreenTests.Settled(new() { ["MSFT 5y"] = MarketRangeTests.Ranged("MSFT", 261, 7 * 86400, 289, 412.34, 289.1, wander: 40) });
            using var line = MarketOffscreenTests.Made(gfx, new Config(), store, "s", options, answers: false);
            Assert.Equal(MarketShows.Ready, line.Reading().State);
            foreach (var scale in new[] { 1f, 1.5f })
            {
                var look = LookFor(true);
                var style = FolderFlyout.StyleFor(scale, look, FlyoutTheme.For(look));
                var asked = line.ListHeight(FolderFlyout.ListWidth(scale), style);
                var rows = LineRows.Px(FullRows, style);
                Assert.True(asked <= rows + 0.5f, $"the line asks {asked:0} px, its Full form's {FullRows} rows {rows:0} px at {scale * 100:0} %");
            }
        });
    }

    /// <summary>
    /// The compact look at every height (2026-10-11: "i would rather have it look like it does when it
    /// is less high"): the chart holds the line's foot and never grows past ChartDip, however tall the
    /// line; a short line's chart is the whole line, as it was.
    /// </summary>
    [Fact]
    public void TheChartStaysAtTheFootAtEveryHeight()
    {
        var look = LookFor(true);
        var style = FolderFlyout.StyleFor(1.5f, look, FlyoutTheme.For(look));
        var width = FolderFlyout.ListWidth(1.5f);
        for (var rows = 1; rows <= 12; rows++)
        {
            var box = PopupPaint.Rect(0, 0, width, LineRows.Px(rows, style));
            var chart = MarketChart.Box(box, style);
            Assert.Equal(box.bottom, chart.bottom);
            Assert.True(TilePaint.Height(chart) <= style.Px(MarketChart.ChartDip) + 0.5f, $"{rows} rows: the chart is {TilePaint.Height(chart):0} px tall");
            Assert.Equal(MathF.Min(TilePaint.Height(box), style.Px(MarketChart.ChartDip)), TilePaint.Height(chart), 0.5f);
        }
    }

    /// <summary>
    /// The line is one thing at every height: no part of it is a target of its own — "Refresh" is its
    /// menu's (§451) — and a click anywhere is the whole line's: it searches what it tracks once the folder has gone (§263).
    /// </summary>
    [UiFact]
    public void TheLineHasNoTargetsOfItsOwn()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var store = MarketOffscreenTests.Settled(new() { ["AAPL"] = MarketOffscreenTests.Answer() });
            using var line = MarketOffscreenTests.Made(gfx, new Config(), store, "s", Held("AAPL"), answers: false);
            var look = LookFor(true);
            var style = FolderFlyout.StyleFor(1.5f, look, FlyoutTheme.For(look));
            var box = PopupPaint.Rect(0, 0, FolderFlyout.ListWidth(1.5f), LineRows.Px(8, style));
            Assert.Null(Target(line, box, style, 1));
            var opened = new List<string>();
            line.Opened = opened.Add;
            Assert.Equal(new IslandAction(View: IslandView.App), line.Click(0));
            line.Open();
            Assert.Equal(["https://www.google.com/search?q=AAPL+stock"], opened);
            Assert.Equal([WidgetAct.OpenWebsite], TheDock.ActsFor(ListKind.Markets));
        });
    }

    /// <summary>
    /// Where a refresh failed and the last price stands, the line says "Last updated … — couldn't
    /// refresh" over it, anywhere on it (§77).
    /// </summary>
    [UiFact]
    public void AStalePriceSaysSoOverTheLine()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var store = Stale("AAPL", MarketOffscreenTests.Answer());
            using var line = new MarketIsland(Config.IslandKey(null, "s"), MarketOffscreenTests.Widget("s", Held("AAPL")), gfx, new Config(), store);
            Assert.True(Patience.Until(() => !line.Quote.Fetching));
            _ = line.Refresh(gfx);
            var reading = line.Reading();
            Assert.Equal(MarketShows.Ready, reading.State);
            Assert.NotNull(reading.Problem);
            Assert.True(reading.Stale);

            var look = LookFor(true);
            var style = FolderFlyout.StyleFor(1.5f, look, FlyoutTheme.For(look));
            var box = PopupPaint.Rect(0, 0, FolderFlyout.ListWidth(1.5f), LineRows.Px(FullRows, style));
            var tip = line.CardTip(new Vector2(box.right / 2, box.bottom / 2), box, style, WidgetSurface.List)?.Text;
            Assert.NotNull(tip);
            Assert.Contains(Karpo.Dock.Panels.Nls.Time(line.Quote.Shown!.FetchedUtc.ToLocalTime()), tip, StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// The line at one row to eight at 150 %, a day up and five years down, dark and light:
    /// stock-line-heights-&lt;dark|light&gt;.png — the same look at every height, the chart at the foot.
    /// </summary>
    [UiFact]
    public void StockLineHeightPictures()
    {
        foreach (var dark in new[] { true, false })
        {
            Offscreen.OnStaThread(gfx =>
            {
                var up = MarketOffscreenTests.Settled(new() { ["MSFT"] = Microsoft });
                var ranged = Held("NVDA");
                ranged.Range = MarketRange.FiveYears;
                var down = MarketOffscreenTests.Settled(new() { ["NVDA 5y"] = MarketRangeTests.Ranged("NVDA", 261, 7 * 86400, 289, 181.2, 220.5, wander: 60) });
                using var day = MarketOffscreenTests.Made(gfx, new Config(), up, "a", Held("MSFT"), answers: false);
                using var years = MarketOffscreenTests.Made(gfx, new Config(), down, "b", ranged, answers: false);
                var look = LookFor(dark);
                var theme = FlyoutTheme.For(look);
                var style = FolderFlyout.StyleFor(1.5f, look, theme);
                var width = FolderFlyout.ListWidth(1.5f);
                const float gap = 16;
                var heights = Enumerable.Range(1, 8).Select(rows => LineRows.Px(rows, style)).ToList();
                var sheetWidth = gap + (2 * (width + gap));
                var sheetHeight = gap + heights.Sum(height => height + gap);
                Offscreen.Bitmap(sheetWidth, sheetHeight, dc =>
                {
                    PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, sheetWidth, sheetHeight), 0, PopupPaint.Ink(theme.Base));
                    var y = gap;
                    foreach (var height in heights)
                    {
                        var x = gap;
                        foreach (var line in new[] { day, years })
                        {
                            var box = PopupPaint.Rect(x, y, width, height);
                            PopupPaint.Fill(dc, box, 4 * style.Scale, PopupPaint.Ink(theme.CardBackground));
                            line.PaintOn(dc, box, style, WidgetSurface.List);
                            x += width + gap;
                        }
                        y += height + gap;
                    }
                }, bitmap => Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, $"stock-line-heights-{(dark ? "dark" : "light")}.png")));
            });
        }
    }

    /// <summary>
    /// The line over two rows and more wherever it has the height (2026-10-11: "all the information is
    /// crammed into one line which is way to much for most widths"): at a list's three widths, 100 and
    /// 150 %, one row, two, three (the person's Stocks folder) and six (their Stock folder), for a share
    /// trading, one over five years with a holding, and one whose market is shut with a long name.
    /// stock-line-widths-&lt;scale&gt;.png.
    /// </summary>
    [UiFact]
    public void StockLineWidthPictures()
    {
        foreach (var scale in new[] { 1f, 1.5f })
        {
            Offscreen.OnStaThread(gfx =>
            {
                var ranged = Held("NVDA");
                ranged.Range = MarketRange.FiveYears;
                ranged.Holding = 12.5;
                var store = MarketOffscreenTests.Settled(new()
                {
                    ["MSFT"] = Microsoft,
                    ["NVDA 5y"] = MarketRangeTests.Ranged("NVDA", 261, 7 * 86400, 289, 181.2, 220.5, wander: 60),
                    ["BRK-B"] = MarketOffscreenTests.Answer("BRK-B", "Berkshire Hathaway Inc. New Class B", price: 471.05, percent: -0.42, previous: 473.04, nowHour: 20, tradedHour: 16, daysBack: 1),
                });
                using var day = MarketOffscreenTests.Made(gfx, new Config(), store, "a", Held("MSFT"), answers: false);
                using var years = MarketOffscreenTests.Made(gfx, new Config(), store, "b", ranged, answers: false);
                using var shut = MarketOffscreenTests.Made(gfx, new Config(), store, "c", Held("BRK-B"), answers: false);
                var look = LookFor(true);
                var theme = FlyoutTheme.For(look);
                var style = FolderFlyout.StyleFor(scale, look, theme);
                var widths = FolderListWidths.All.Select(width => FolderFlyout.ListWidth(scale, width)).ToList();
                var gap = 16 * scale;
                var heights = new[] { 1, 2, 3, 6 }.Select(rows => LineRows.Px(rows, style)).ToList();
                var lines = new[] { day, years, shut };
                var sheetWidth = gap + widths.Sum(width => width + gap);
                var sheetHeight = gap + (lines.Length * heights.Sum(height => height + gap));
                Offscreen.Bitmap(sheetWidth, sheetHeight, dc =>
                {
                    PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, sheetWidth, sheetHeight), 0, PopupPaint.Ink(theme.Base));
                    var y = gap;
                    foreach (var line in lines)
                    {
                        foreach (var height in heights)
                        {
                            var x = gap;
                            foreach (var width in widths)
                            {
                                var box = PopupPaint.Rect(x, y, width, height);
                                PopupPaint.Fill(dc, box, 4 * style.Scale, PopupPaint.Ink(theme.CardBackground));
                                line.PaintOn(dc, box, style, WidgetSurface.List);
                                x += width + gap;
                            }
                            y += height + gap;
                        }
                    }
                }, bitmap => Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, $"stock-line-widths-{scale * 100:0}.png")));
            });
        }
    }

    private static void Draw(MarketIsland line, D2D_RECT_F box, in TileStyle style)
    {
        var size = box;
        var copy = style;
        Offscreen.Bitmap(size.right, size.bottom, dc => line.PaintOn(dc, size, copy, WidgetSurface.List), _ => { });
    }

    /// <summary>A point of <paramref name="box"/> whose target is <paramref name="target"/>, or null.</summary>
    private static Vector2? Target(CellIsland line, D2D_RECT_F box, in TileStyle style, int target)
    {
        for (var y = box.top; y < box.bottom; y += 3)
        {
            for (var x = box.left; x < box.right; x += 3)
            {
                if (line.TargetOn(new Vector2(x, y), box, style, WidgetSurface.List) == target)
                {
                    return new Vector2(x, y);
                }
            }
        }
        return null;
    }

    // --- The face --------------------------------------------------------------------------------------

    private static string Microsoft => MarketOffscreenTests.Answer("MSFT", "Microsoft Corp.", price: 512.4, percent: 0.86, previous: 508.03);

    private static string Nvidia => MarketOffscreenTests.Answer("NVDA", "NVIDIA Corporation", price: 181.2, percent: 2.31, previous: 177.11);

    /// <summary>
    /// The face shows its own share, not its folder's line of another: MSFT on the bar over a line of
    /// AAPL, said to a screen reader as a ticker reads; the bar shows a folder no tooltip. Its menu's row
    /// is its own (the folder, <see cref="FolderIsland.FaceWidget"/>).
    /// </summary>
    [UiFact]
    public void TheFaceReadsItsOwnShare()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var store = MarketOffscreenTests.Settled(new() { ["MSFT"] = Microsoft, ["AAPL"] = MarketOffscreenTests.Answer() });
            using var folder = Made(gfx, store, Held("AAPL"), answers: false, face: Held("MSFT"));
            var line = Assert.IsType<MarketIsland>(Assert.Single(folder.Tiles).Island);
            Assert.Equal("AAPL", line.Options.Symbol);
            Assert.Same(folder, folder.FaceWidget);
            Assert.Equal("MSFT 512.40 +0.86%", folder.Tooltip(0));
            Paint(folder, Bar());
            Assert.Contains("512.40", folder.FaceShown, StringComparison.Ordinal);
            Assert.DoesNotContain("336.16", folder.FaceShown, StringComparison.Ordinal);
            Assert.Equal([WidgetAct.OpenWebsite], TheDock.ActsFor(FolderFaces.Markets));
        });
    }

    /// <summary>
    /// A folder holds several shares, none of them the face's: a face of MSFT over lines of AAPL, NVDA
    /// and MSFT. Each symbol is asked for once however many show it (the face and the MSFT line are
    /// one fetch); a line's refresh moves nothing on it.
    /// </summary>
    [UiFact]
    public void AFolderHoldsSeveralSharesAndTheFaceIsNoneOfThem()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var asked = new System.Collections.Concurrent.ConcurrentDictionary<string, int>();
            var answers = new Dictionary<string, string> { ["MSFT"] = Microsoft, ["AAPL"] = MarketOffscreenTests.Answer(), ["NVDA"] = Nvidia };
            var store = new MarketStore(
                new MarketAnswers((ask, _) =>
                {
                    asked.AddOrUpdate(ask.Symbol, 1, (_, count) => count + 1);
                    return Task.FromResult(MarketQuotes.Parse(answers[ask.Symbol]));
                }),
                MarketDisk.None);
            using var folder = Made(gfx, store, Held("AAPL"), face: Held("MSFT"), more: [Held("NVDA"), Held("MSFT")]);
            var lines = folder.Tiles.Select(tile => tile.Island).OfType<MarketIsland>().ToList();
            Assert.Equal(["AAPL", "NVDA", "MSFT"], lines.Select(line => line.Options.Symbol));
            Assert.Equal(new Dictionary<string, int> { ["AAPL"] = 1, ["MSFT"] = 1, ["NVDA"] = 1 }, asked.OrderBy(pair => pair.Key).ToDictionary());
            Paint(folder, Bar());
            Assert.Contains("512.40", folder.FaceShown, StringComparison.Ordinal);

            // A line's refresh is its own symbol's: the face says what it said.
            var shown = folder.FaceShown;
            Assert.True(lines[1].Refresh());
            Assert.True(Patience.Until(() => !lines[1].Quote.Fetching));
            Assert.Equal(2, asked["NVDA"]);
            Assert.False(((IIsland)folder).Refresh(gfx));
            Paint(folder, Bar());
            Assert.Equal(shown, folder.FaceShown);
        });
    }

    /// <summary>
    /// A face with no symbol yet — the preset's before its question is answered, or one forgotten —
    /// is the kind's glyph and "Set up", asks the store for nothing, says no ticker and keeps its
    /// size whatever its folder's lines show.
    /// </summary>
    [UiFact]
    public void AFaceWithNoSymbolSaysSetUp()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var asked = 0;
            var store = new MarketStore(new MarketAnswers((_, _) => { Interlocked.Increment(ref asked); return Task.FromResult(MarketQuotes.Parse(MarketOffscreenTests.Answer())); }), MarketDisk.None);
            using var folder = Made(gfx, store, Held("AAPL"), face: new MarketOptions());
            Assert.False(MarketFaces.Of(folder.Options)!.IsSet);
            Assert.Equal((null, null, false), folder.MarketQuote);
            Assert.Equal(1, asked);
            // No ticker to a screen reader: the folder's own words, its line's price nowhere in them.
            Assert.DoesNotContain("336.16", folder.Tooltip(0) ?? "", StringComparison.Ordinal);
            foreach (var style in new[] { Bar(), Bar(72), Bar(48, 144, text: 2.25f) })
            {
                Assert.True(folder.CellSize(0, style).Width > folder.CellSize(0, style).Height, "the glyph and \"Set up\"");
            }
            using var lineless = Made(gfx, MarketOffscreenTests.Waiting(), null, face: new MarketOptions(), answers: false);
            Assert.Equal(folder.CellSize(0, Bar()), lineless.CellSize(0, Bar()));
            Paint(folder, Bar());
            Paint(folder, Bar(vertical: true));
        });
    }

    /// <summary>
    /// Across a bar the face is as wide as the ticker reads now (docs\widgets.md §244): "1,512.40
    /// +12.86%" wider than "336.16 -1.06%", a price still coming or one that failed — a dash — no
    /// wider; down a bar on its side one size. It follows its settings: the change switched off is
    /// narrower, a longer label wider.
    /// </summary>
    [UiFact]
    public void TheFaceIsAsWideAsTheTickerReads()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var folders = new List<FolderIsland>
            {
                Made(gfx, MarketOffscreenTests.Settled(new() { ["AAPL"] = MarketOffscreenTests.Answer() }), Held("AAPL"), key: "a", answers: false),
                Made(gfx, MarketOffscreenTests.Settled(new() { ["AAPL"] = MarketOffscreenTests.Answer(price: 1512.4, percent: 12.86, previous: 1340.03) }), Held("AAPL"), key: "b", answers: false),
                Made(gfx, MarketOffscreenTests.Waiting(), Held("AAPL"), key: "c", answers: false),
                Made(gfx, MarketOffscreenTests.Broken(), Held("AAPL"), key: "d"),
            };
            using var plain = Made(gfx, MarketOffscreenTests.Settled(new() { ["AAPL"] = MarketOffscreenTests.Answer() }), Held("AAPL"), change: false, key: "e", answers: false);
            using var named = Made(gfx, MarketOffscreenTests.Settled(new() { ["AAPL"] = MarketOffscreenTests.Answer() }), Held("AAPL", label: "Apple"), key: "f", answers: false);
            try
            {
                foreach (var height in new[] { 28, 48, 72, 115 })
                {
                    foreach (var vertical in new[] { false, true })
                    {
                        foreach (var text in new[] { 1f, 2.25f })
                        {
                            var style = Bar(height, 144, vertical, text);
                            var sizes = folders.Select(folder => folder.CellSize(0, style)).ToList();
                            var where = $"{height} DIP, text ×{text}{(vertical ? ", vertical" : "")}: {string.Join(", ", sizes)}";
                            if (vertical)
                            {
                                Assert.True(sizes.Distinct().Count() == 1, where);
                                continue;
                            }
                            var (fall, rise, waiting, broken) = (sizes[0].Width, sizes[1].Width, sizes[2].Width, sizes[3].Width);
                            Assert.True(fall <= rise && (height > 48 || fall < rise) && waiting <= fall && broken <= fall, where);
                            Assert.True(plain.CellSize(0, style).Width <= fall);
                            Assert.True(named.CellSize(0, style).Width >= fall);
                        }
                    }
                }
                Assert.True(plain.CellSize(0, Bar()).Width < folders[0].CellSize(0, Bar()).Width, "the change switched off takes no room");
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

    /// <summary>
    /// A new price for the face's own share paints the face again in place where it reads the same
    /// shape, and lays the bar out again where it reads wider — a line of the same share asking for it,
    /// the two reading one fetch; the folder's line of another share is not what it shows.
    /// </summary>
    [UiFact]
    public void TheFacePaintsAgainInPlaceAsThePriceMoves()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var json = MarketOffscreenTests.Answer();
            var store = new MarketStore(
                new MarketAnswers((ask, _) => Task.FromResult(MarketQuotes.Parse(ask.Symbol == "AAPL" ? json : Nvidia))),
                MarketDisk.None);
            using var folder = Made(gfx, store, Held("NVDA"), face: Held("AAPL"), more: [Held("AAPL")]);
            var style = Bar();
            var size = folder.CellSize(0, style);
            _ = folder.Measure(style);
            _ = folder.Surface(0, style);
            Assert.Contains("336.16", folder.FaceShown, StringComparison.Ordinal);
            var same = folder.Tiles.Select(tile => tile.Island).OfType<MarketIsland>().Single(line => line.Options.Symbol == "AAPL");

            // A price of the same shape, still falling: in place, the same width (tabular figures).
            json = MarketOffscreenTests.Answer(price: 335.5, percent: -1.25, previous: 339.75);
            Assert.True(same.Refresh());
            Assert.True(Patience.Until(() => !same.Quote.Fetching && !folder.MarketQuote.Fetching));
            Assert.False(((IIsland)folder).Refresh(gfx));
            Assert.Contains("335.50", folder.FaceShown, StringComparison.Ordinal);
            Assert.Equal(size, folder.CellSize(0, style));

            // The line of the face's share asks: one fetch, which the face takes too. A rise says "+",
            // wider than "-": the bar is laid out again (docs\widgets.md §244), the face wider in it.
            json = MarketOffscreenTests.Answer(price: 352.25, percent: 3.67, previous: 339.75);
            Assert.True(same.Refresh());
            Assert.True(Patience.Until(() => !same.Quote.Fetching));
            Assert.True(((IIsland)folder).Refresh(gfx));
            Assert.True(folder.CellSize(0, style).Width > size.Width, "a rise is no wider than a fall");
            folder.Forget();
            _ = folder.Measure(style);
            _ = folder.Surface(0, style);
            Assert.Contains("352.25", folder.FaceShown, StringComparison.Ordinal);
        });
    }

    private static void Paint(FolderIsland folder, in IslandStyle style)
    {
        var (width, height) = folder.CellSize(0, style);
        var copy = style;
        Offscreen.Bitmap(width, height, dc => folder.PaintCell(dc, 0, copy, width, height), _ => { });
    }

    // --- Pictures --------------------------------------------------------------------------------------

    /// <summary>
    /// The preset as it opens: a day's price, a 5-year range with a holding, a price still coming, one
    /// that failed with nothing kept, a stale price, no symbol yet, and at 225 % text — at 150 %, to
    /// stand beside stock-*.png of the panel.
    /// </summary>
    [UiFact]
    public void StockPresetPictures()
    {
        foreach (var dark in new[] { true, false })
        {
            Offscreen.OnStaThread(gfx =>
            {
                var folders = new List<FolderIsland>();
                try
                {
                    var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
                    void Add(string caption, MarketStore store, MarketOptions? options, float text = 1f, bool answers = false)
                    {
                        var look = LookFor(dark, text);
                        var folder = Made(gfx, store, options, key: $"{Owner}/stocks-{panels.Count}", answers: answers);
                        folders.Add(folder);
                        folder.Opened(gfx);
                        panels.Add((caption, look, folder, Plan(folder, look, 1.5f).Plan));
                    }

                    var ranged = Held("MSFT");
                    ranged.Range = MarketRange.FiveYears;
                    ranged.Holding = 12.5;
                    Add("A day", MarketOffscreenTests.Settled(new() { ["AAPL"] = MarketOffscreenTests.Answer() }), Held("AAPL"));
                    Add("Five years, a holding", MarketOffscreenTests.Settled(new() { ["MSFT 5y"] = MarketRangeTests.Ranged("MSFT", 261, 7 * 86400, 289, 412.34, 289.1, wander: 40) }), ranged);
                    Add("Loading", MarketOffscreenTests.Waiting(), Held("TSLA"));
                    Add("Failed", MarketOffscreenTests.Broken(), Held("NOPE"), answers: true);
                    Add("Stale", Stale("AAPL", MarketOffscreenTests.Answer()), Held("AAPL"), answers: true);
                    Add("No symbol", MarketOffscreenTests.Waiting(), null);
                    Add("A day: 225 % text", MarketOffscreenTests.Settled(new() { ["AAPL"] = MarketOffscreenTests.Answer() }), Held("AAPL"), 2.25f);
                    Sheet(gfx, $"stock-preset-{(dark ? "dark" : "light")}.png", 1.5f, panels);
                }
                finally
                {
                    foreach (var folder in folders)
                    {
                        folder.Closed();
                        folder.Dispose();
                    }
                }
            });
        }
    }

    /// <summary>
    /// The face on a bar of 28, 48 and 72 DIP, and down one, at 100 %: a fall, a rise, an index with a
    /// label, the change off, a price coming, one failed, one stale, no symbol —
    /// stock-face-&lt;dark|light&gt;.png, beside the entry's stock-bar-*.png before it went.
    /// </summary>
    [UiFact]
    public void StockFacePictures()
    {
        foreach (var dark in new[] { true, false })
        {
            Offscreen.OnStaThread(gfx =>
            {
                var look = LookFor(dark);
                var theme = FlyoutTheme.For(look);
                var islands = new List<FolderIsland>
                {
                    Made(gfx, MarketOffscreenTests.Settled(new() { ["AAPL"] = MarketOffscreenTests.Answer() }), Held("AAPL"), key: "a", answers: false),
                    Made(gfx, MarketOffscreenTests.Settled(new() { ["MSFT"] = MarketOffscreenTests.Answer("MSFT", "Microsoft Corp.", price: 512.4, percent: 0.86, previous: 508.03) }), Held("MSFT"), key: "b", answers: false),
                    Made(gfx, MarketOffscreenTests.Settled(new() { ["^GSPC"] = MarketOffscreenTests.Answer("^GSPC", "S&P 500", price: 6417.29, percent: 0.02, previous: 6416.02) }), Held("^GSPC", label: "S&P"), key: "c", answers: false),
                    Made(gfx, MarketOffscreenTests.Settled(new() { ["AAPL"] = MarketOffscreenTests.Answer() }), Held("AAPL"), change: false, key: "d", answers: false),
                    Made(gfx, MarketOffscreenTests.Waiting(), Held("TSLA"), key: "e", answers: false),
                    Made(gfx, MarketOffscreenTests.Broken(), Held("NOPE"), key: "f"),
                    Made(gfx, Stale("AAPL", MarketOffscreenTests.Answer()), Held("AAPL"), key: "g"),
                    Made(gfx, MarketOffscreenTests.Waiting(), null, key: "h", answers: false),
                };
                var styles = new[] { Bar(28, dark: dark), Bar(48, dark: dark), Bar(72, dark: dark), Bar(48, vertical: true, dark: dark) };
                try
                {
                    const float gap = 12;
                    var rows = styles.Select(style => (Style: style, Sizes: islands.Select(island => island.CellSize(0, style)).ToList())).ToList();
                    var width = gap + rows.Max(row => row.Style.Vertical ? row.Sizes.Max(size => size.Width) + gap : row.Sizes.Sum(size => size.Width + gap));
                    var height = gap + rows.Sum(row => (row.Style.Vertical ? row.Sizes.Sum(size => size.Height + gap) : row.Sizes.Max(size => size.Height) + gap));
                    Offscreen.Bitmap(width, height, dc =>
                    {
                        PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, width, height), 0, PopupPaint.Ink(theme.Base));
                        var y = gap;
                        foreach (var (style, sizes) in rows)
                        {
                            var x = gap;
                            var top = y;
                            for (var index = 0; index < islands.Count; index++)
                            {
                                var (w, h) = sizes[index];
                                PopupPaint.Fill(dc, PopupPaint.Rect(x, y, w, h), 4 * style.Scale, PopupPaint.Ink(theme.CardBackground));
                                var island = islands[index];
                                var at = (x, y);
                                Offscreen.Shifted(dc, at.x, at.y, () => island.PaintCell(dc, 0, style, w, h));
                                if (style.Vertical)
                                {
                                    y += h + gap;
                                }
                                else
                                {
                                    x += w + gap;
                                }
                            }
                            y = style.Vertical ? y : top + sizes.Max(size => size.Height) + gap;
                        }
                    }, bitmap => Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, $"stock-face-{(dark ? "dark" : "light")}.png")));
                }
                finally
                {
                    foreach (var island in islands)
                    {
                        island.Dispose();
                    }
                }
            });
        }
    }

    /// <summary>
    /// §648: the Markets preset as the add list makes it — the four shares under the S&amp;P 500's ticker — open above its
    /// button on a 48 DIP bar at 150 %, over the samples' prices (Samples.Markets, never the network):
    /// markets-preset-&lt;dark|light&gt;.png.
    /// </summary>
    [UiFact]
    public void MarketsPresetPictures()
    {
        foreach (var dark in new[] { true, false })
        {
            Offscreen.OnStaThread(gfx =>
            {
                var look = LookFor(dark);
                var theme = FlyoutTheme.For(look);
                var holder = Presets.Folder(Presets.Find(Presets.MarketId)!, Machine);
                holder.Id = "markets";
                MarketIsland.Made = Samples.Markets();
                FolderIsland made;
                try
                {
                    made = new FolderIsland($"{Owner}/{holder.Id}", holder, gfx, new Config());
                }
                finally
                {
                    MarketIsland.Made = null;
                }
                using var folder = made;
                Assert.True(Patience.Until(() => !folder.MarketQuote.Fetching && folder.Tiles.Select(tile => tile.Island).OfType<MarketIsland>().All(line => !line.Quote.Fetching)));
                _ = ((IIsland)folder).Refresh(gfx);
                Assert.Equal(4, folder.Tiles.Count);
                folder.Opened(gfx);
                try
                {
                    const float scale = 1.5f;
                    var plan = Plan(folder, look, scale).Plan;
                    var style = Bar(48, 144, dark: dark);
                    var (faceWidth, faceHeight) = folder.CellSize(0, style);
                    const float margin = 24;
                    var width = (2 * margin) + MathF.Max(plan.Width, faceWidth);
                    var height = margin + plan.Height + margin + faceHeight;
                    Offscreen.Bitmap(width, height, dc =>
                    {
                        var backdrop = dark ? new DXGI_RGBA(0.20f, 0.24f, 0.30f, 1) : new DXGI_RGBA(0.72f, 0.78f, 0.86f, 1);
                        dc->Clear(&backdrop);
                        foreach (var piece in plan.Pieces)
                        {
                            var plate = new D2D_RECT_F(margin + piece.left, margin + piece.top, margin + piece.right, margin + piece.bottom);
                            PopupPaint.Fill(dc, plate, plan.Radius, PopupPaint.Ink(theme.Base));
                            PopupPaint.Stroke(dc, plate, plan.Radius, MathF.Round(scale), PopupPaint.Ink(theme.Stroke));
                        }
                        var context = dc;
                        Offscreen.Shifted(context, margin, margin, () => ListFormPictures.Panel(context, gfx, folder, plan, look, theme, scale));
                        var top = margin + plan.Height + margin;
                        PopupPaint.Fill(dc, PopupPaint.Rect(0, top, width, faceHeight), 0, PopupPaint.Ink(theme.Base));
                        PopupPaint.Fill(dc, PopupPaint.Rect(margin, top, faceWidth, faceHeight), 4 * style.Scale, PopupPaint.Ink(theme.CardBackground));
                        Offscreen.Shifted(context, margin, top, () => folder.PaintCell(context, 0, style, faceWidth, faceHeight));
                    }, bitmap => Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, $"markets-preset-{(dark ? "dark" : "light")}.png")));
                }
                finally
                {
                    folder.Closed();
                }
            });
        }
    }

    /// <summary>
    /// A folder whose face is one share and whose lines are two others, open above its button on a
    /// 48 DIP bar at 150 %: the face MSFT, the lines AAPL (the panel) and NVDA (a compact line) —
    /// stock-face-own-&lt;dark|light&gt;.png. Beside it the same face with no symbol yet: "Set up".
    /// </summary>
    [UiFact]
    public void StockFaceOwnSharePictures()
    {
        foreach (var dark in new[] { true, false })
        {
            Offscreen.OnStaThread(gfx =>
            {
                var look = LookFor(dark);
                var theme = FlyoutTheme.For(look);
                var store = MarketOffscreenTests.Settled(new() { ["MSFT"] = Microsoft, ["AAPL"] = MarketOffscreenTests.Answer(), ["NVDA"] = Nvidia });
                using var folder = Made(gfx, store, Held("AAPL"), key: $"{Owner}/own", answers: false, face: Held("MSFT"), more: [Held("NVDA")]);
                using var unset = Made(gfx, MarketOffscreenTests.Waiting(), Held("AAPL"), key: $"{Owner}/unset", answers: false, face: new MarketOptions());
                folder.Opened(gfx);
                try
                {
                    const float scale = 1.5f;
                    var plan = Plan(folder, look, scale).Plan;
                    var style = Bar(48, 144, dark: dark);
                    var (faceWidth, faceHeight) = folder.CellSize(0, style);
                    var (unsetWidth, _) = unset.CellSize(0, style);
                    const float margin = 24;
                    var width = (2 * margin) + MathF.Max(plan.Width, faceWidth + margin + unsetWidth);
                    var height = margin + plan.Height + margin + faceHeight;
                    Offscreen.Bitmap(width, height, dc =>
                    {
                        var backdrop = dark ? new DXGI_RGBA(0.20f, 0.24f, 0.30f, 1) : new DXGI_RGBA(0.72f, 0.78f, 0.86f, 1);
                        dc->Clear(&backdrop);
                        foreach (var piece in plan.Pieces)
                        {
                            var plate = new D2D_RECT_F(margin + piece.left, margin + piece.top, margin + piece.right, margin + piece.bottom);
                            PopupPaint.Fill(dc, plate, plan.Radius, PopupPaint.Ink(theme.Base));
                            PopupPaint.Stroke(dc, plate, plan.Radius, MathF.Round(scale), PopupPaint.Ink(theme.Stroke));
                        }
                        var context = dc;
                        Offscreen.Shifted(context, margin, margin, () => ListFormPictures.Panel(context, gfx, folder, plan, look, theme, scale));
                        // The bar under it, the face at the folder's left edge and the unset one beside it.
                        var top = margin + plan.Height + margin;
                        PopupPaint.Fill(dc, PopupPaint.Rect(0, top, width, faceHeight), 0, PopupPaint.Ink(theme.Base));
                        PopupPaint.Fill(dc, PopupPaint.Rect(margin, top, faceWidth, faceHeight), 4 * style.Scale, PopupPaint.Ink(theme.CardBackground));
                        Offscreen.Shifted(context, margin, top, () => folder.PaintCell(context, 0, style, faceWidth, faceHeight));
                        var next = margin + faceWidth + margin;
                        Offscreen.Shifted(context, next, top, () => unset.PaintCell(context, 0, style, unsetWidth, faceHeight));
                    }, bitmap => Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, $"stock-face-own-{(dark ? "dark" : "light")}.png")));
                }
                finally
                {
                    folder.Closed();
                }
            });
        }
    }

    // --- Rates (docs\widgets.md §246) ------------------------------------------------------------

    private static string Franc => MarketOffscreenTests.Answer("EURCHF=X", "EUR/CHF", "CHF", price: 0.9269, percent: -0.0205, previous: 0.927, wander: 0.001);

    private static string Yen => MarketOffscreenTests.Answer("USDJPY=X", "USD/JPY", "JPY", price: 158.004, percent: 0.42, previous: 157.34, wander: 0.2);

    /// <summary>
    /// A folder whose face is a currency pair and whose lines are a share and another pair, open above
    /// its button on a 48 DIP bar at 150 %: the face EUR/CHF, the lines MSFT (the panel) and USD/JPY (a
    /// compact line); beside the face, a share's and the yen's — stock-pair-&lt;dark|light&gt;.png.
    /// </summary>
    [UiFact]
    public void StockPairPictures()
    {
        foreach (var dark in new[] { true, false })
        {
            Offscreen.OnStaThread(gfx =>
            {
                var look = LookFor(dark);
                var theme = FlyoutTheme.For(look);
                var answers = new Dictionary<string, string> { ["EURCHF=X"] = Franc, ["MSFT"] = Microsoft, ["USDJPY=X"] = Yen };
                using var folder = Made(gfx, MarketOffscreenTests.Settled(answers), Held("MSFT"), key: $"{Owner}/pair", answers: false, face: Quoted("EURCHF=X"), more: [Quoted("USDJPY=X")]);
                using var share = Made(gfx, MarketOffscreenTests.Settled(answers), Held("MSFT"), key: $"{Owner}/pair-share", answers: false, face: Held("MSFT"));
                using var yen = Made(gfx, MarketOffscreenTests.Settled(answers), Quoted("USDJPY=X"), key: $"{Owner}/pair-yen", answers: false, face: Quoted("USDJPY=X"));
                folder.Opened(gfx);
                try
                {
                    const float scale = 1.5f;
                    var plan = Plan(folder, look, scale).Plan;
                    var style = Bar(48, 144, dark: dark);
                    var faces = new[] { folder, share, yen };
                    var sizes = faces.Select(face => face.CellSize(0, style)).ToList();
                    const float margin = 24;
                    var faceHeight = sizes.Max(size => size.Height);
                    var width = (2 * margin) + MathF.Max(plan.Width, sizes.Sum(size => size.Width + margin));
                    var height = margin + plan.Height + margin + faceHeight;
                    Offscreen.Bitmap(width, height, dc =>
                    {
                        var backdrop = dark ? new DXGI_RGBA(0.20f, 0.24f, 0.30f, 1) : new DXGI_RGBA(0.72f, 0.78f, 0.86f, 1);
                        dc->Clear(&backdrop);
                        foreach (var piece in plan.Pieces)
                        {
                            var plate = new D2D_RECT_F(margin + piece.left, margin + piece.top, margin + piece.right, margin + piece.bottom);
                            PopupPaint.Fill(dc, plate, plan.Radius, PopupPaint.Ink(theme.Base));
                            PopupPaint.Stroke(dc, plate, plan.Radius, MathF.Round(scale), PopupPaint.Ink(theme.Stroke));
                        }
                        var context = dc;
                        Offscreen.Shifted(context, margin, margin, () => ListFormPictures.Panel(context, gfx, folder, plan, look, theme, scale));
                        var top = margin + plan.Height + margin;
                        PopupPaint.Fill(dc, PopupPaint.Rect(0, top, width, faceHeight), 0, PopupPaint.Ink(theme.Base));
                        var x = margin;
                        for (var index = 0; index < faces.Length; index++)
                        {
                            var (w, h) = sizes[index];
                            var face = faces[index];
                            var left = x;
                            PopupPaint.Fill(dc, PopupPaint.Rect(left, top, w, h), 4 * style.Scale, PopupPaint.Ink(theme.CardBackground));
                            Offscreen.Shifted(context, left, top, () => face.PaintCell(context, 0, style, w, h));
                            x += w + margin;
                        }
                    }, bitmap => Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, $"stock-pair-{(dark ? "dark" : "light")}.png")));
                }
                finally
                {
                    folder.Closed();
                }
            });
        }
    }
}
