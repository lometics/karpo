// Graphs behind their words (backlog item 11) and a disk's read and write that stay put (item 12).
//
// The graphs — the processor, the graphics, the memory, the disk, the network — and a share's chart
// are a widget's ground now: across the whole taskbar entry and list line, faded where the words
// stand (Graphs\GraphBackdrop.cs). The sheets here draw every graph and the share on a busy
// history, so the line runs right through the figures, at several bar heights and list heights,
// light and dark, at 100 % and 150 % and at the largest text, for a person to look at
// (graphs-behind-*.png in KARPO_PNG_DIR).
//
// The disk's read and write used to come and go: whether they were drawn, and where, was measured
// on the figures showing, so "R 0K  W 8.8M" fitted where "R 88K  W 8.8M" did not, and the W walked
// along the line as the R changed. They are measured on the widest their figures can be now, each
// figure right-aligned in that room, and a figure not read yet is a dash in the same room — which
// DiskReadAndWriteStayPut holds, figure by figure, on every surface the disk is drawn on.

using Karpo.Core;
using Karpo.Dock;
using Karpo.Dock.Graphs;
using Karpo.Dock.Markets;
using Karpo.Tests.Folders;
using Karpo.Tests.Forecasts;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;

namespace Karpo.Tests.Perf;

[Collection(SamplerState.Collection)]
public sealed unsafe class GraphBackdropTests
{
    // --- The fade ---------------------------------------------------------------------------

    /// <summary>
    /// The fade holds the graph at its faint strength over the words and brings it back to full a
    /// ramp's length past them, on either side, and where two stretches of words are near each
    /// other the fainter of the two wins.
    /// </summary>
    [Fact]
    public void TheGraphIsFaintUnderTheWordsAndFullPastThem()
    {
        (float, float)[] words = [(0, 40), (160, 200)];
        Assert.Equal(GraphBackdrop.UnderWords, GraphBackdrop.Alpha(20, words, 10), 4);
        Assert.Equal(GraphBackdrop.UnderWords, GraphBackdrop.Alpha(180, words, 10), 4);
        Assert.Equal(1f, GraphBackdrop.Alpha(100, words, 10), 4);
        Assert.Equal(GraphBackdrop.UnderWords + ((1 - GraphBackdrop.UnderWords) / 2), GraphBackdrop.Alpha(45, words, 10), 4);
        Assert.Equal(GraphBackdrop.UnderWords + ((1 - GraphBackdrop.UnderWords) / 2), GraphBackdrop.Alpha(155, words, 10), 4);

        Span<(float At, float Alpha)> stops = stackalloc (float, float)[2 + (words.Length * 4)];
        var count = GraphBackdrop.Stops(0, 200, words, 10, stops);
        var made = stops[..count].ToArray();
        Assert.Equal(0, made[0].At);
        Assert.Equal(200, made[^1].At);
        Assert.True(made.Zip(made.Skip(1)).All(pair => pair.First.At < pair.Second.At), "the stops are in order");
        Assert.Contains(made, stop => stop.At == 50 && stop.Alpha == 1);
        Assert.Contains(made, stop => stop.At == 150 && stop.Alpha == 1);
    }

    // --- The disk's read and write ------------------------------------------------------------

    /// <summary>
    /// Reads and writes from nothing to gigabytes a second, and not read yet: on a list's compact
    /// line and its whole widget, at 100 % and 150 % and at the largest text, the read and the write are drawn whatever the figures — or never, the same way
    /// for every figure — and their labels and the figures' ends stand in one place throughout.
    /// </summary>
    [UiFact]
    public void DiskReadAndWriteStayPut()
    {
        (double Read, double Write)[] figures =
        [
            (-1, -1), (0, 0), (512, 900), (1_200_000, 310_000), (88_000_000, 3_000), (1.5e9, 2.2e9), (9_400, 64_000_000),
        ];
        var failures = new List<string>();
        Offscreen.OnStaThread(gfx =>
        {
            var line = Graph(gfx, ListKind.Disk, busy: false);
            try
            {
                foreach (var (scale, textScale) in new[] { (1f, 1f), (1.5f, 1f), (1f, 2.25f), (1.5f, 2.25f) })
                {
                    var look = TestLooks.LookFor(dark: true, textScale);
                    var theme = FlyoutTheme.For(look);
                    var lineStyle = FolderFlyout.StyleFor(scale, look, theme);
                    var width = FolderFlyout.ListWidth(scale);
                    line.Feed(0.4f, read: 1, written: 1);
                    var compact = line.ListHeight(width, lineStyle);
                    var surfaces = new List<(string Name, D2D_RECT_F Box)>
                    {
                        ("the compact line", PopupPaint.Rect(0, 0, width, compact)),
                        // Four rows at the list's text: at the largest text that is the row with the read and the write, the whole
                        // widget there (four rows of its larger cells, §442) leaving them out for the figure.
                        ("the whole line", PopupPaint.Rect(0, 0, width, ListFormPictures.Rows(4, scale, 1f))),
                    };
                    foreach (var (name, box) in surfaces)
                    {
                        var at = $"{name} at {scale * 100:0} %, {textScale * 100:0} % text";
                        List<(string Text, D2D_RECT_F Box)>? first = null;
                        foreach (var (read, write) in figures)
                        {
                            line.Feed(0.4f, read: read, written: write);
                            var said = Said(line, box, lineStyle, WidgetSurface.List);
                            var expected = Expected(read, write);
                            var shown = said.Where(item => expected.Contains(item.Text) || Label(item.Text)).ToList();
                            var labels = shown.Where(item => Label(item.Text)).ToList();
                            var values = shown.Where(item => !Label(item.Text)).ToList();
                            if (labels.Count != 2 || values.Count != 2)
                            {
                                failures.Add($"{at}, read {read} write {write}: {labels.Count} labels and {values.Count} figures where the read and the write are two of each ({string.Join(" | ", said.Select(item => item.Text))})");
                                continue;
                            }
                            if (first is null)
                            {
                                first = shown;
                                continue;
                            }
                            if (first.Count != shown.Count)
                            {
                                failures.Add($"{at}, read {read} write {write}: {shown.Count} pieces of the read and write where the first figures drew {first.Count}: they come and go");
                                continue;
                            }
                            for (var index = 0; index < shown.Count; index++)
                            {
                                var (text, drawn) = shown[index];
                                var was = first[index].Box;
                                // A label stands where it stood; a figure ends where it ended.
                                var moved = Label(text) ? MathF.Abs(drawn.left - was.left) : MathF.Abs(drawn.right - was.right);
                                if (moved > 1.01f || MathF.Abs(drawn.top - was.top) > 0.5f)
                                {
                                    failures.Add($"{at}, read {read} write {write}: \"{text}\" moved {moved:0.#} px from where \"{first[index].Text}\" stood");
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                line.Dispose();
            }
        });
        Assert.True(failures.Count == 0, $"{failures.Count} wrong:\n{string.Join("\n", failures.Take(40))}");

        static bool Label(string text) => text is "Read" or "Write" or "R " or "  W ";

        static HashSet<string> Expected(double read, double write) =>
        [
            read >= 0 ? Units.Rate(read) : "–", write >= 0 ? Units.Rate(write) : "–",
            read >= 0 ? Units.ShortRate(read) : "–", write >= 0 ? Units.ShortRate(write) : "–",
        ];
    }

    /// <summary>
    /// The widest every figure the read and the write can show keeps the room it is measured as —
    /// "888 MB/s" and "8.8M" — so the room kept is never less than a figure that arrives.
    /// </summary>
    [UiFact]
    public void TheShapesAreTheWidestFigures()
    {
        var wider = new List<string>();
        Offscreen.OnStaThread(gfx =>
        {
            const float px = 12;
            var rate = PopupPaint.Measure(gfx, Units.LongRateShape, px, Weight.Regular).X;
            var shortRate = PopupPaint.Measure(gfx, Units.RateShape, px, Weight.Regular).X;
            for (var power = 0.0; power < 13; power += 0.05)
            {
                var bytes = Math.Pow(10, power);
                if (PopupPaint.Measure(gfx, Units.Rate(bytes), px, Weight.Regular).X > rate)
                {
                    wider.Add(Units.Rate(bytes));
                }
                if (PopupPaint.Measure(gfx, Units.ShortRate(bytes), px, Weight.Regular).X > shortRate)
                {
                    wider.Add(Units.ShortRate(bytes));
                }
            }
            if (PopupPaint.Measure(gfx, "–", px, Weight.Regular).X > shortRate)
            {
                wider.Add("–");
            }
        });
        Assert.Empty(wider);
    }

    // --- The pictures -------------------------------------------------------------------------

    /// <summary>
    /// Every graph and a share behind their words on the bar (28, 48 and 72 DIP) and as list lines
    /// (one to five rows), on a history that runs
    /// right through the figures, light and dark, at 100 % and 150 %, and at the largest text. The
    /// assertion is that each drew; the sheets are for a person.
    /// </summary>
    [UiFact]
    public void GraphsBehindTheirWords()
    {
        var blank = new List<string>();
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var (scale, textScale) in new[] { (1f, 1f), (1.5f, 1f), (1f, 2.25f), (1.5f, 2.25f) })
            {
                foreach (var dark in new[] { true, false })
                {
                    var file = $"graphs-behind-{scale * 100:0}{(textScale > 1 ? "-bigtext" : "")}-{(dark ? "dark" : "light")}.png";
                    blank.AddRange(Sheet(gfx, scale, textScale, dark, file));
                }
            }
        });
        Assert.Empty(blank);
    }

    private static readonly ListKind[] Kinds = [ListKind.Cpu, ListKind.Gpu, ListKind.Memory, ListKind.Disk, ListKind.Network];

    /// <summary>One sheet: the bar at three heights, the lines; what drew nothing.</summary>
    private static List<string> Sheet(Gfx gfx, float scale, float textScale, bool dark, string file)
    {
        var look = TestLooks.LookFor(dark, textScale);
        var theme = FlyoutTheme.For(look);
        var blank = new List<string>();
        var store = Shares();
        CellIsland day = Share(gfx, store, MarketRange.Day, null);
        CellIsland year = Share(gfx, store, MarketRange.Year, null);
        CellIsland[] lines =
        [
            Graph(gfx, ListKind.Cpu, busy: true), Graph(gfx, ListKind.Memory, busy: true),
            Graph(gfx, ListKind.Disk, busy: true), Graph(gfx, ListKind.Network, busy: true),
            Share(gfx, store, MarketRange.FiveDays, WidgetSurface.List),
        ];
        try
        {
            var pad = PopupPaint.Round(12 * scale);
            var gap = PopupPaint.Round(8 * scale);
            var heights = new[] { 28, 48, 72 };
            var bars = heights.Select(height =>
            {
                var config = new Config { Height = height };
                var dpi = (uint)MathF.Round(96 * scale);
                return new IslandStyle(scale, false, Layout.CellPx(dpi, config), Layout.IconPx(dpi, config), look);
            }).ToList();
            // The readings are faces on the bar now, with no graph (docs\widgets.md §97): the stock's are the bar's graphs.
            CellIsland[] barCells = [day, year];
            var barWidth = bars.Max(style => barCells.Sum(island => island.CellSize(0, style).Width + gap));
            var lineStyle = FolderFlyout.StyleFor(scale, look, theme);
            var lineWidth = FolderFlyout.ListWidth(scale);
            var rowsShown = new[] { 1, 2, 3, 4, 5 };
            var lineHeight = ListFormPictures.Rows(rowsShown.Max(), scale, textScale);
            var width = pad + MathF.Max(barWidth, rowsShown.Length * (lineWidth + gap)) + pad;
            var height = pad + bars.Sum(style => style.Cell + gap) + (lines.Length * (lineHeight + gap)) + pad;

            Offscreen.Bitmap(width, height, dc =>
            {
                PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, width, height), 0, PopupPaint.Ink(theme.Base));
                var y = pad;
                foreach (var style in bars)
                {
                    var x = pad;
                    foreach (var island in barCells)
                    {
                        var (cellWidth, cellHeight) = island.CellSize(0, style);
                        PopupPaint.Fill(dc, PopupPaint.Rect(x, y, cellWidth, cellHeight), GraphBackdrop.BarRadius(style), PopupPaint.Ink(theme.CardBackground));
                        var shown = style;
                        Offscreen.Shifted(dc, x, y, () => island.PaintCell(dc, 0, shown, cellWidth, cellHeight));
                        x += cellWidth + gap;
                    }
                    y += style.Cell + gap;
                }
                foreach (var island in lines)
                {
                    var x = pad;
                    foreach (var rows in rowsShown)
                    {
                        var tall = ListFormPictures.Rows(rows, scale, textScale);
                        var at = PopupPaint.Rect(x, y, lineWidth, tall);
                        PopupPaint.Fill(dc, at, PopupPaint.Round(4 * scale), PopupPaint.Ink(theme.CardBackground));
                        Offscreen.Shifted(dc, x, y, () => island.PaintOn(dc, PopupPaint.Rect(0, 0, lineWidth, tall), lineStyle, WidgetSurface.List));
                        x += lineWidth + gap;
                    }
                    y += lineHeight + gap;
                }
            }, bitmap => Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, file)));

            // Each graph alone, on nothing: a graph that drew nothing is what a picture test catches.
            foreach (var island in barCells)
            {
                var style = bars[1];
                var (cellWidth, cellHeight) = island.CellSize(0, style);
                var inked = default((float Left, float Top, float Right, float Bottom));
                Offscreen.Bitmap(cellWidth, cellHeight, dc => island.PaintCell(dc, 0, style, cellWidth, cellHeight),
                    bitmap => inked = Offscreen.InkOf(bitmap, (uint)MathF.Ceiling(cellWidth), (uint)MathF.Ceiling(cellHeight)));
                if (inked.Right <= inked.Left)
                {
                    blank.Add($"{island.GetType().Name} drew nothing in {file}");
                }
            }
        }
        finally
        {
            foreach (var island in lines.Concat([day, year]))
            {
                island.Dispose();
            }
        }
        return blank;
    }

    /// <summary>
    /// A widget of <paramref name="kind"/> as the place it stands makes it — a taskbar entry where
    /// <paramref name="on"/> is null, else a list folder's line — with
    /// <paramref name="set"/> run on its settings first.
    /// </summary>
    private static CellIsland Made(Gfx gfx, BarKind kind, WidgetSurface? on, Action<WidgetItem>? set = null)
    {
        var name = kind.ToString();
        var id = $"{name.ToLowerInvariant()}-{on?.ToString().ToLowerInvariant() ?? "bar"}";
        var key = Config.IslandKey(null, id);
        IIsland? made;
        switch (on)
        {
            case WidgetSurface.List:
            {
                var list = Enum.Parse<ListKind>(name);
                var line = ListFormPictures.Line(list, item => set?.Invoke(item));
                line.Id = id;
                made = Karpo.Dock.Islands.CreateLine(key, line, list, gfx, new Config());
                break;
            }
            default:
            {
                var entry = kind.New();
                entry.Id = id;
                set?.Invoke(entry);
                made = Karpo.Dock.Islands.Create(key, entry, gfx, new Config());
                break;
            }
        }
        var island = made as CellIsland ?? throw new Xunit.Sdk.XunitException($"{kind} on {on?.ToString() ?? "the bar"}: no widget was made.");
        _ = island.Refresh(gfx);
        return island;
    }

    /// <summary>
    /// A graph of <paramref name="kind"/> as a list folder's line, with a history of its own: busy —
    /// between about half and all of it, so the line runs through the figures — or gentle.
    /// </summary>
    private static PerformanceIsland Graph(Gfx gfx, ListKind kind, bool busy)
    {
        var id = $"{kind.ToString().ToLowerInvariant()}-list";
        var line = ListFormPictures.Line(kind, _ => { });
        line.Id = id;
        var island = (PerformanceIsland)Karpo.Dock.Islands.CreateLine(Config.IslandKey(null, id), line, kind, gfx, new Config())!;
        _ = island.Refresh(gfx);
        island.Forgotten();
        var phase = Array.IndexOf(Kinds, kind) * 0.9f;
        for (var step = 0; step < 300; step++)
        {
            var wave = (MathF.Sin((step * 0.09f) + phase) + 1) / 2;
            var share = busy ? 0.45f + (0.55f * wave) : 0.1f + (0.3f * wave);
            switch (kind)
            {
                case ListKind.Network:
                    island.Feed(down: share * 24 * 1024 * 1024, up: share * 3 * 1024 * 1024);
                    break;
                case ListKind.Memory:
                    island.Feed(share, memory: new MemoryReading((uint)(share * 100), 32UL << 30, (ulong)((1 - share) * (32UL << 30))));
                    break;
                case ListKind.Disk:
                    island.Feed(share, read: share * 40_000_000, written: share * 3_000_000);
                    break;
                default:
                    island.Feed(share);
                    break;
            }
        }
        return island;
    }

    /// <summary>A store holding MSFT's day, its five days and its year, kept a moment ago, that never asks the network.</summary>
    private static MarketStore Shares()
    {
        var day = MarketOffscreenTests.Answer("MSFT", "Microsoft Corporation", price: 517.53, percent: 0.92, previous: 512.8, nowHour: 15.9, tradedHour: 15.9);
        var fiveDays = MarketRangeTests.Ranged("MSFT", 130, 900, 505, 517.5, 516.17, sessionPoints: 26, wander: 4);
        var year = MarketRangeTests.Ranged("MSFT", 251, 86400, 420, 517.5, 515.74, wander: 30);
        List<MarketCache> kept = [Kept("MSFT", day), Kept("MSFT 5d", fiveDays), Kept("MSFT 1y", year)];
        var file = System.Text.Json.JsonSerializer.Serialize(new MarketFile { Prices = kept }, MarketJson.Default.MarketFile);
        return new MarketStore(
            new MarketAnswers((_, cancel) => new TaskCompletionSource<MarketQuote>().Task.WaitAsync(cancel)),
            new MarketDisk { Read = () => file, Write = _ => { } });

        static MarketCache Kept(string key, string json) => new()
        {
            FetchedUtc = DateTime.UtcNow,
            Key = MarketOffscreenTests.KeyOf(key),
            Saved = MarketSaved.Of(MarketQuotes.Parse(json)),
        };
    }

    /// <summary>
    /// MSFT over <paramref name="range"/> where <paramref name="on"/> says, reading <paramref name="store"/>:
    /// a list's line, or on the bar the stock preset's folder wearing its face (docs\widgets.md §98).
    /// </summary>
    private static CellIsland Share(Gfx gfx, MarketStore store, MarketRange range, WidgetSurface? on)
    {
        MarketIsland.Made = store;
        try
        {
            if (on is null)
            {
                var holder = Karpo.Tests.Forecasts.MarketOffscreenTests.OneLineFolder(new PresetMachine(false, []));
                holder.Id = $"stock-{range}";
                holder.Folder!.Cells.Single().Island.Market = new MarketOptions { Symbol = "MSFT", Range = range };
                var folder = new FolderIsland(Config.IslandKey(null, holder.Id), holder, gfx, new Config());
                _ = ((IIsland)folder).Refresh(gfx);
                return folder;
            }
            var id = $"stock-{range}-list";
            var line = ListFormPictures.Line(ListKind.Markets, item => item.Market = new MarketOptions { Symbol = "MSFT", Range = range });
            line.Id = id;
            var made = (CellIsland)Karpo.Dock.Islands.CreateLine(Config.IslandKey(null, id), line, ListKind.Markets, gfx, new Config())!;
            _ = made.Refresh(gfx);
            return made;
        }
        finally
        {
            MarketIsland.Made = null;
        }
    }

    /// <summary>The words <paramref name="island"/> says drawn into <paramref name="box"/>, each with its box, in the order drawn.</summary>
    private static List<(string Text, D2D_RECT_F Box)> Said(CellIsland island, D2D_RECT_F box, TileStyle style, WidgetSurface surface)
    {
        var said = new List<(string, D2D_RECT_F)>();
        PaintWatch.Said = (text, at) => said.Add((text, at));
        try
        {
            Offscreen.Bitmap(TilePaint.Width(box), TilePaint.Height(box), dc => island.PaintOn(dc, box, style, surface), _ => { });
        }
        finally
        {
            PaintWatch.Said = null;
        }
        return said;
    }
}
