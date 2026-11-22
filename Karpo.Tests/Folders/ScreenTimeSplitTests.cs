// Screen time split into widgets of fixed content (docs\widgets.md §603; the person, 2026-11-09: "split all of them.
// also remove the are chart option from the screen time, it should always show the bar chart and not the area
// chart"): today's total (screenTime) and the fortnight's bars (screenTimeHistory), each the same at Compact and at
// Full and nothing more where its line is taller; its height its size's, whatever the days; the history always bars,
// no area to choose and none read from a file. Pictured to KARPO_PNG_DIR: screentime-split-<dark|light>.png (each kind
// a folder of its Compact over its Full) and screentime-split-text225-dark.png.

using System.Text.Json;
using Karpo.Core;
using Karpo.Dock;
using TerraFX.Interop.DirectX;
using Karpo.Tests.Islands;
using static Karpo.Tests.Islands.Offscreen;

namespace Karpo.Tests.Folders;

[Collection(ScreenTimeLinesTests.Names)]
public sealed unsafe class ScreenTimeSplitTests
{
    private static readonly LineSize[] Sizes = [LineSize.Compact, LineSize.Full];

    private static readonly ListKind[] Kinds = [ListKind.ScreenTime, ListKind.ScreenTimeHistory];

    /// <summary>A line of <paramref name="kind"/> in <paramref name="size"/>.</summary>
    private static CellItem Made(ListKind kind, LineSize size)
    {
        var line = ListFormPictures.Line(kind);
        line.Size = size;
        line.Normalize();
        return line;
    }

    /// <summary>A list folder of <paramref name="lines"/>, its screen time reading <paramref name="days"/>, opened.</summary>
    private static FolderIsland Folder(Gfx gfx, ScreenTimeSnapshot days, params CellItem[] lines) =>
        ListFormPictures.Folder(gfx, made =>
        {
            foreach (var tile in made.Tiles)
            {
                ScreenTimeLinesTests.Feed(tile.Island, days);
            }
        }, lines);

    /// <summary>What a line says drawn into <paramref name="width"/> × <paramref name="height"/>: every word and figure.</summary>
    private static HashSet<string> Said(CellIsland line, float width, float height, in TileStyle style)
    {
        var said = new List<string>();
        var box = PopupPaint.Rect(0, 0, width, height);
        var drawn = style;
        PaintWatch.Said = (text, _) => said.Add(text);
        try
        {
            Bitmap(width, height, dc => line.PaintOn(dc, box, drawn, WidgetSurface.List), _ => { });
        }
        finally
        {
            PaintWatch.Said = null;
        }
        // One word or figure a token: Compact's caption says on one line what Full says as a label over its figure.
        // Segoe Fluent Icons' glyphs (the private use area) are pictures, not words: Full's ‹ › steps.
        return [.. said.SelectMany(text => text.Split([" ", ",", " ", " "], StringSplitOptions.RemoveEmptyEntries))
            .Where(word => !word.All(c => c is >= '\uE000' and <= '\uF8FF'))];
    }

    /// <summary>
    /// Today and the history say the same words and figures at Compact and at Full — today's day, total and average,
    /// an app picked its time, its name and the day's total; the history its legend — and drawn twice as tall
    /// nothing more.
    /// </summary>
    [UiFact]
    public void EachSizeSaysTheSameAndATallerLineNoMore()
    {
        var failures = new List<string>();
        ScreenTimeLinesTests.Standing(gfx =>
        {
            foreach (var picked in new[] { false, true })
            {
                foreach (var kind in Kinds)
                {
                    var said = new Dictionary<LineSize, HashSet<string>>();
                    foreach (var size in Sizes)
                    {
                        var folder = Folder(gfx, ScreenTimeLinesTests.Fortnight(), Made(kind, size));
                        try
                        {
                            if (picked)
                            {
                                ((IWidgetContainer)folder).Publish(Channels.App, new AppId(ScreenTimeLinesTests.Browser));
                            }
                            var look = ListFormPictures.LookFor(dark: true);
                            var (_, plan) = ListFormPictures.Plan(folder, look, 1f);
                            var card = plan.Cards[0];
                            var (width, height) = (card.right - card.left, card.bottom - card.top);
                            var style = FolderFlyout.StyleFor(1f, look, FlyoutTheme.For(look));
                            var line = (CellIsland)folder.Tiles[0].Island;
                            var at = Said(line, width, height, style);
                            var taller = Said(line, width, height * 2, style);
                            var name = $"{kind} {size}{(picked ? ", an app picked" : "")}";
                            if (!at.SetEquals(taller))
                            {
                                failures.Add($"{name}: twice as tall it says [{string.Join(", ", taller.Except(at))}] more and [{string.Join(", ", at.Except(taller))}] less");
                            }
                            if (at.Count == 0)
                            {
                                failures.Add($"{name}: says nothing");
                            }
                            said[size] = at;
                        }
                        finally
                        {
                            folder.Closed();
                            folder.Dispose();
                        }
                    }
                    var onlyFull = said[LineSize.Full].Except(said[LineSize.Compact]).ToList();
                    var onlyCompact = said[LineSize.Compact].Except(said[LineSize.Full]).ToList();
                    if (onlyFull.Count > 0 || onlyCompact.Count > 0)
                    {
                        failures.Add($"{kind}{(picked ? ", an app picked" : "")}: Full alone says [{string.Join(", ", onlyFull)}]; Compact alone [{string.Join(", ", onlyCompact)}]");
                    }
                }
            }
        });
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// A line's height is its size's whatever the days say — a fortnight, or nothing counted yet —, at 100 % and 225 %
    /// text; Compact the smaller; and in a folder at 100 % its cells the kind's form's (ListWidgets' Forms).
    /// </summary>
    [UiFact]
    public void ItsHeightIsItsSizesWhateverTheDaysSay()
    {
        var failures = new List<string>();
        ScreenTimeLinesTests.Standing(gfx =>
        {
            foreach (var textScale in ListFormPictures.TextScales)
            {
                var look = ListFormPictures.LookFor(dark: true, textScale);
                var style = FolderFlyout.StyleFor(1.5f, look, FlyoutTheme.For(look));
                var width = FolderFlyout.ListWidth(1.5f);
                foreach (var kind in Kinds)
                {
                    var heights = new List<float>();
                    foreach (var size in Sizes)
                    {
                        var counted = Folder(gfx, ScreenTimeLinesTests.Fortnight(), Made(kind, size));
                        var none = Folder(gfx, new ScreenTimeSnapshot(ScreenTimeLinesTests.Today, new ScreenTimeLog()), Made(kind, size));
                        try
                        {
                            var full = ((IListContent)counted.Tiles[0].Island).ListHeight(width, style);
                            var empty = ((IListContent)none.Tiles[0].Island).ListHeight(width, style);
                            if (full != empty)
                            {
                                failures.Add($"{kind} {size} at {textScale * 100:0} % text: {full:0} px with days, {empty:0} without");
                            }
                            heights.Add(full);
                            if (textScale == 1f)
                            {
                                var (_, plan) = ListFormPictures.Plan(counted, ListFormPictures.LookFor(dark: true), 1f);
                                var cells = MathF.Round((plan.Cards[0].bottom - plan.Cards[0].top) / FolderOptions.CellDip);
                                var form = ListWidgets.Of(kind).Forms!.Single(each => each.Size == size).Cells;
                                if (cells != form)
                                {
                                    failures.Add($"{kind} {size}: {cells} cells in a folder, its form says {form}");
                                }
                            }
                        }
                        finally
                        {
                            counted.Closed();
                            counted.Dispose();
                            none.Closed();
                            none.Dispose();
                        }
                    }
                    if (!(heights[0] < heights[1]))
                    {
                        failures.Add($"{kind} at {textScale * 100:0} % text: Compact {heights[0]:0} px, Full {heights[1]:0}");
                    }
                }
            }
        });
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// The history is always bars: no line of screen time offers a graph to choose (its Style row went with the area), and
    /// a file that still says <c>"screenTime": { "graph": "area" }</c> reads as nothing and writes nothing back.
    /// </summary>
    [Fact]
    public void TheHistoryIsAlwaysBars()
    {
        foreach (var kind in new[] { ListKind.ScreenTime, ListKind.ScreenTimeHistory, ListKind.ScreenTimeApps, ListKind.ScreenTimeChart })
        {
            var line = ListFormPictures.Line(kind);
            Assert.Equal(WidgetOptionsKind.None, ListWidgets.Of(kind).Info.Options);
            Assert.DoesNotContain(OptionRows.Of(line), row => row.Key.Contains("ScreenTime", StringComparison.Ordinal) || row.Key == OptionRows.Style);
        }
        const string file = """{ "id": "x", "kind": "screenTime", "size": "full", "screenTime": { "graph": "area" } }""";
        var read = JsonSerializer.Deserialize(file, ConfigJson.Default.CellItem)!;
        read.View = FolderView.List;
        read.Normalize();
        var written = JsonSerializer.Serialize(read, ConfigJson.Default.CellItem);
        Assert.DoesNotContain("screenTime\":", written.Replace("\"kind\": \"screenTime\"", "", StringComparison.Ordinal).Replace("\"kind\":\"screenTime\"", "", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.DoesNotContain("graph", written, StringComparison.Ordinal);
        Assert.DoesNotContain("area", written, StringComparison.Ordinal);
    }

    /// <summary>
    /// The pictures: today and the history, each a folder of its Compact over its Full, side by side, dark and light at
    /// 100 % text and dark at 225 %: screentime-split-[text225-]&lt;dark|light&gt;.png; and a day before today shown.
    /// </summary>
    [UiFact]
    public void ScreenTimeSplitPictures()
    {
        ScreenTimeLinesTests.Standing(gfx =>
        {
            foreach (var (dark, textScale) in new[] { (true, 1f), (false, 1f), (true, 2.25f) })
            {
                var look = ListFormPictures.LookFor(dark, textScale);
                var folders = Kinds.Select(kind => (Caption: ListWidgets.Of(kind).Info.Title, Folder: Folder(gfx, ScreenTimeLinesTests.Fortnight(), Made(kind, LineSize.Compact), Made(kind, LineSize.Full)))).ToList();
                var older = Folder(gfx, ScreenTimeLinesTests.Fortnight(), Made(ListKind.ScreenTime, LineSize.Compact), Made(ListKind.ScreenTime, LineSize.Full), Made(ListKind.ScreenTimeHistory, LineSize.Compact));
                ((IWidgetContainer)older).Publish(Channels.Day, ScreenTimeLinesTests.Today.AddDays(-3));
                try
                {
                    var file = $"screentime-split{(textScale > 1 ? "-text225" : "")}-{(dark ? "dark" : "light")}.png";
                    ListFormPictures.Sheet(gfx, file, 1.5f,
                    [
                        .. folders.Select(each => ($"{each.Caption}: Compact over Full, {textScale * 100:0} % text", look, each.Folder, ListFormPictures.Plan(each.Folder, look, 1.5f).Plan)),
                        ("Three days back: today Compact, Full, the history Compact", look, older, ListFormPictures.Plan(older, look, 1.5f).Plan),
                    ]);
                }
                finally
                {
                    foreach (var (_, folder) in folders)
                    {
                        folder.Closed();
                        folder.Dispose();
                    }
                    older.Closed();
                    older.Dispose();
                }
            }
        });
    }

    /// <summary>
    /// <paramref name="line"/> at each of its sizes side by side at 150 %, in <paramref name="look"/>, to
    /// <paramref name="file"/> in KARPO_PNG_DIR (the size left as it was found).
    /// </summary>
    internal static void SizesSheet(Gfx gfx, ScreenTimeLine line, Look look, string file)
    {
        const float scale = 1.5f;
        var kept = line.Settings.Size;
        var theme = FlyoutTheme.For(look);
        var style = FolderFlyout.StyleFor(scale, look, theme);
        var width = FolderFlyout.ListWidth(scale);
        var pad = PopupPaint.Round(16 * scale);
        var label = PopupPaint.Round(11 * scale);
        var labelLine = TilePaint.LineHeight(gfx, label);
        var heights = Sizes.Select(size =>
        {
            line.Settings.Size = size;
            return MathF.Ceiling(line.ListHeight(width, style));
        }).ToList();
        var sheetWidth = pad + (Sizes.Length * (width + pad));
        var sheetHeight = pad + labelLine + heights.Max() + pad;
        try
        {
            Bitmap(sheetWidth, sheetHeight, dc =>
            {
                PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, sheetWidth, sheetHeight), 0, PopupPaint.Ink(look.Dark ? Windows.UI.Color.FromArgb(255, 0x1C, 0x1C, 0x1C) : Windows.UI.Color.FromArgb(255, 0xE8, 0xE8, 0xE8)));
                for (var at = 0; at < Sizes.Length; at++)
                {
                    line.Settings.Size = Sizes[at];
                    var x = pad + (at * (width + pad));
                    PopupPaint.Text(dc, gfx, Sizes[at].ToString(), label, Weight.Regular, theme.TextSecondary, x, pad);
                    var box = PopupPaint.Rect(x, pad + labelLine, width, heights[at]);
                    PopupPaint.Fill(dc, box, PopupPaint.Round(8 * scale), PopupPaint.Ink(theme.Base));
                    PopupPaint.Stroke(dc, box, PopupPaint.Round(8 * scale), MathF.Max(1, MathF.Round(scale)), PopupPaint.Ink(theme.Stroke));
                    var context = dc;
                    var height = heights[at];
                    PopupPaint.Clipped(context, box, PopupPaint.Round(8 * scale), () => Shifted(context, box.left, box.top, () =>
                        FolderFlyout.PaintContent(context, line, PopupPaint.Rect(0, 0, width, height), style)));
                }
            }, bitmap => Save(bitmap, Path.Combine(Pictures, file)));
        }
        finally
        {
            line.Settings.Size = kept;
        }
    }
}
