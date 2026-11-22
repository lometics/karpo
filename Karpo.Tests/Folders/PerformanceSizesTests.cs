// §603: the performance graphs' two sizes as they draw (docs\widgets.md §603). The person, 2026-11-09: "split all of
// them" — the widgets that still showed more as their line grew say the same at each of their sizes.
//
// So each reading — the processor, the graphics, the memory, the disk, the network — says the same at Compact and at
// Full: its name, its figure and its second reading (the busiest moment, the memory in use of all there is, read and
// write, down and up). Full writes the figure and the name as fully as the flyout did ("5.6 MB/s" where Compact says
// "5.6M"; "Disk 1 (C:)") and adds the minute's axes — what the graph's top stands for, how far back it reaches and "0";
// the figure alone (the Numbers look) has neither graph nor axes. A box taller than its size makes it say nothing
// more, and its height is its size's whatever the readings are.
//
// Pictures (KARPO_PNG_DIR): performance-sizes-<dark|light>.png — each reading a folder of its Compact over its Full, at
// 150 %; performance-sizes-numbers-dark.png the Numbers look the same; performance-sizes-text225-dark.png at 225 % text.
// Needs Direct2D: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Dock.Performance;
using Karpo.Tests.Islands;
using static Karpo.Tests.Islands.Offscreen;

namespace Karpo.Tests.Folders;

public sealed unsafe class PerformanceSizesTests
{
    private const ulong Gb = 1024UL * 1024 * 1024;

    private static readonly LineSize[] Sizes = [LineSize.Compact, LineSize.Full];

    private static readonly ListKind[] Readings = [ListKind.Cpu, ListKind.Gpu, ListKind.Memory, ListKind.Disk, ListKind.Network];

    /// <summary>A line of <paramref name="kind"/> in <paramref name="size"/>, its graph drawn or the figure alone.</summary>
    private static CellItem Made(ListKind kind, LineSize size, bool graph = true)
    {
        var line = kind.New();
        line.Size = size;
        if (!graph)
        {
            line.Performance = new PerformanceOptions { ShowGraph = false };
        }
        line.Normalize();
        return line;
    }

    /// <summary>What the machine says besides the reading, the same on every PC.</summary>
    private static PerformanceFigures Figures(PerformanceData source) => source.Metric switch
    {
        Metric.Cpu => new PerformanceFigures { System = new SystemReading(412, 5120, 190_000, 0, 0, 0), BaseMhz = 3400, Logical = 16 },
        Metric.Gpu => new PerformanceFigures { Graphics = "NVIDIA GeForce RTX 4070" },
        Metric.Disk => new PerformanceFigures { DiskTitle = "Disk 1 (C:)" },
        _ => new PerformanceFigures(),
    };

    /// <summary>A folder of <paramref name="lines"/>, its figures the test's and each line fed two minutes of a wave.</summary>
    private static FolderIsland Folder(Gfx gfx, params CellItem[] lines)
    {
        PerformanceIsland.FiguresStand = Figures;
        try
        {
            var folder = ListFormPictures.Folder(gfx, lines);
            foreach (var tile in folder.Tiles)
            {
                if (tile.Island is PerformanceIsland graph)
                {
                    Fed(graph);
                }
            }
            return folder;
        }
        finally
        {
            PerformanceIsland.FiguresStand = null;
        }
    }

    /// <summary>Two minutes of a wave, and the latest reading of every kind: the memory in use, the disk's read and write, down and up.</summary>
    private static void Fed(PerformanceIsland line)
    {
        var metric = line.Source.Metric;
        line.Forgotten();
        for (var at = 0; at < 120; at++)
        {
            var wave = 0.35f + (0.3f * MathF.Sin(at / 9f)) + (0.1f * MathF.Sin(at / 2.3f));
            line.Feed(
                reading: metric == Metric.Network ? 0 : wave,
                down: metric == Metric.Network ? wave * 8 * 1024 * 1024 : -1,
                up: metric == Metric.Network ? wave * 900 * 1024 : -1,
                memory: metric == Metric.Memory ? new MemoryReading((uint)(wave * 100), 32 * Gb, (ulong)((1 - wave) * 32 * Gb)) : null,
                read: metric == Metric.Disk ? wave * 40 * 1024 * 1024 : -1,
                written: metric == Metric.Disk ? wave * 6 * 1024 * 1024 : -1);
        }
    }

    /// <summary>Every text a line draws into <paramref name="width"/> × <paramref name="height"/>, one by one.</summary>
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
        return [.. said.Where(text => !text.All(c => c is >= '' and <= ''))];
    }

    /// <summary>
    /// Every reading, graph and figure alone, says at Compact its name, its figure and its second reading and nothing
    /// else; at Full the same — its name and figure written as fully as they fit — and, with its graph, the minute's
    /// axes alone besides: what its top stands for, how far back it reaches and "0". Drawn twice as tall, neither says
    /// anything more.
    /// </summary>
    [UiFact]
    public void EachSizeSaysTheSameAndATallerLineNoMore()
    {
        var failures = new List<string>();
        OnStaThread(gfx =>
        {
            var look = ListFormPictures.LookFor(dark: true);
            var style = FolderFlyout.StyleFor(1f, look, FlyoutTheme.For(look));
            foreach (var graph in new[] { true, false })
            {
                foreach (var kind in Readings)
                {
                    foreach (var size in Sizes)
                    {
                        using var folder = Folder(gfx, Made(kind, size, graph));
                        try
                        {
                            var (_, plan) = ListFormPictures.Plan(folder, look, 1f);
                            var card = plan.Cards[0];
                            var (width, height) = (card.right - card.left, card.bottom - card.top);
                            var line = (PerformanceIsland)folder.Tiles[0].Island;
                            var at = Said(line, width, height, style);
                            var taller = Said(line, width, height * 2, style);
                            var what = $"{kind} {size}{(graph ? "" : " (numbers)")}";
                            if (!at.SetEquals(taller))
                            {
                                failures.Add($"{what}: twice as tall it says [{string.Join("], [", taller.Except(at))}] more");
                            }
                            var metric = line.Source.Metric;
                            var model = PerformanceModel.For(line.Source, Figures(line.Source));
                            var second = line.CompactParts().Select(part => part.Text).ToList();
                            var figure = size == LineSize.Compact ? PerformanceIsland.ReadingOf(line.Source).Text : model.Value;
                            string[] names = [.. (size == LineSize.Compact ? new[] { line.Title, PerformanceIsland.Drawn(metric) } : [model.Title, line.Title, PerformanceIsland.Drawn(metric)]), .. PerformanceIsland.Short(metric) is { } brief ? [brief] : (string[])[]];
                            if (second.Count == 0)
                            {
                                failures.Add($"{what}: has no second reading");
                            }
                            foreach (var word in second.Append(figure))
                            {
                                if (!at.Contains(word))
                                {
                                    failures.Add($"{what}: does not say [{word}]");
                                }
                            }
                            if (!at.Overlaps(names))
                            {
                                failures.Add($"{what}: says no name of [{string.Join("], [", names)}]");
                            }
                            var series = line.Series;
                            var full = metric == Metric.Network ? Karpo.Dock.Graphs.Sparkline.Round(Karpo.Dock.Graphs.Sparkline.Ceiling(series, PerformanceModel.Samples)) : 1f;
                            string[] axes = size == LineSize.Full && graph
                                ? [metric == Metric.Network ? Units.Rate(full) : Units.Percent(1), PerformanceModel.SpanAt(1), "0"]
                                : [];
                            var rest = at.Except(second).Except(names).Except([figure]).ToHashSet();
                            if (!rest.SetEquals(axes))
                            {
                                failures.Add($"{what}: besides its name, figure and second reading says [{string.Join("], [", rest)}], not [{string.Join("], [", axes)}]");
                            }
                        }
                        finally
                        {
                            folder.Closed();
                        }
                    }
                }
            }
        });
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// A reading's height is its size's, whatever it reads — two minutes of readings or none at all, its graph drawn
    /// or the figure alone at either —, at 100 % and 225 % text; and Compact is the smaller.
    /// </summary>
    [UiFact]
    public void ItsHeightIsItsSizesWhateverItReads()
    {
        var failures = new List<string>();
        OnStaThread(gfx =>
        {
            foreach (var textScale in ListFormPictures.TextScales)
            {
                var look = ListFormPictures.LookFor(dark: true, textScale);
                var style = FolderFlyout.StyleFor(1.5f, look, FlyoutTheme.For(look));
                var width = FolderFlyout.ListWidth(1.5f);
                foreach (var graph in new[] { true, false })
                {
                    foreach (var kind in Readings)
                    {
                        var heights = new List<float>();
                        foreach (var size in Sizes)
                        {
                            using var folder = Folder(gfx, Made(kind, size, graph));
                            try
                            {
                                var line = (PerformanceIsland)folder.Tiles[0].Island;
                                var read = line.ListHeight(width, style);
                                line.Forgotten();
                                var none = line.ListHeight(width, style);
                                if (read != none)
                                {
                                    failures.Add($"{kind} {size} graph {graph} at {textScale * 100:0} % text: {read:0} px reading, {none:0} with nothing read");
                                }
                                heights.Add(read);
                            }
                            finally
                            {
                                folder.Closed();
                            }
                        }
                        if (!(heights[0] < heights[1]))
                        {
                            failures.Add($"{kind} graph {graph} at {textScale * 100:0} % text: Compact {heights[0]:0} px, Full {heights[1]:0}");
                        }
                    }
                }
            }
        });
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>The pictures: each reading a folder of its Compact over its Full (see the top of the file).</summary>
    [UiFact]
    public void PerformanceSizesPictures()
    {
        OnStaThread(gfx =>
        {
            foreach (var (name, dark, textScale, graph) in new[]
            {
                ("performance-sizes-dark.png", true, 1f, true), ("performance-sizes-light.png", false, 1f, true),
                ("performance-sizes-numbers-dark.png", true, 1f, false), ("performance-sizes-numbers-light.png", false, 1f, false),
                ("performance-sizes-text225-dark.png", true, 2.25f, true),
            })
            {
                var look = ListFormPictures.LookFor(dark, textScale);
                var folders = Readings.Select(kind => (Kind: kind, Folder: Folder(gfx, Made(kind, LineSize.Compact, graph), Made(kind, LineSize.Full, graph)))).ToList();
                try
                {
                    ListFormPictures.Sheet(gfx, name, 1.5f,
                        [.. folders.Select(each => ($"{ListWidgets.Of(each.Kind).Info.Title}{(graph ? "" : " · " + S.CoreWidgetStylePerformanceFigure)}: Compact over Full, {textScale * 100:0} % text",
                            look, each.Folder, ListFormPictures.Plan(each.Folder, look, 1.5f).Plan))]);
                }
                finally
                {
                    foreach (var (_, folder) in folders)
                    {
                        folder.Closed();
                        folder.Dispose();
                    }
                }
            }
        });
    }
}
