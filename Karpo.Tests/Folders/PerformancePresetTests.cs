// The five performance readings are presets (docs\widgets.md §97): what a click on the processor,
// graphics, memory, disk or network entry gave — its flyout, Task Manager's page for the reading in
// a panel: the name, the figure large, the last minute with what its top stands for, the facts
// behind the figure in a grid, and "Open Task Manager" — is the reading's preset: its graph at its
// tallest, the name, the figure and the minute; its details under it, the facts, a widget of their
// own since 2026-10-11 ("the graphs and details should be separate widgets"); and the premade Task
// Manager footer, one card with no divider in it, under the reading's face: the glyph and the figure
// the entry was, painted again in place at its pace.
//
// Every reading here is a stand-in: a line fed (PerformanceIsland.Feed), sources that read what the
// test says (FolderIsland.PerformanceSources), the machine's figures made up
// (PerformanceIsland.FiguresStand). Nothing of this PC's is read, and Task Manager never opens.
//
// Pictured beside the flyouts they replace (rendered before they went: performance-<reading>-*.png,
// and the entries' perf-bar-*.png): performance-preset-<dark|light>.png at 150 %, and the faces on
// the bar, performance-face-<dark|light>.png.

using Karpo.Dock;
using Karpo.Dock.Performance;
using Karpo.Tests.Islands;
using Karpo.Tests.Perf;
using TerraFX.Interop.DirectX;
using static Karpo.Tests.Folders.ListFormPictures;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Folders;

[Collection(SamplerState.Collection)]
public sealed unsafe class PerformancePresetTests : IDisposable
{
    // The processor's top processes are the stand-in's: nothing of this PC's is read (ProcessStand).
    private readonly ProcessStand.Using processes = ProcessStand.Use();

    public void Dispose() => processes.Dispose();

    private static readonly PresetMachine Machine = new(HasBattery: false, FixedDrives: []);

    private static readonly Metric[] Metrics = Enum.GetValues<Metric>();

    private const ulong Gb = 1024UL * 1024 * 1024;

    /// <summary>The preset of <paramref name="metric"/>'s id.</summary>
    private static string IdOf(Metric metric) => metric switch
    {
        Metric.Cpu => Presets.CpuId,
        Metric.Gpu => Presets.GpuId,
        Metric.Memory => Presets.MemoryId,
        Metric.Disk => Presets.DiskId,
        _ => Presets.NetworkActivityId,
    };

    /// <summary>The reading's preset folder, as the add list makes it.</summary>
    private static BarItem Holder(Metric metric) => Presets.Folder(Presets.Find(IdOf(metric))!, Machine);

    /// <summary>A registry whose readings are the test's: 0.25, or 2 MB/s for the network.</summary>
    private static DataSources Registry() => new((key, widget) =>
    {
        var metric = widget.Info!.Metric!.Value;
        var device = PerformanceData.DeviceOf(metric, widget.Performance ?? new PerformanceOptions());
        return new PerformanceData(metric, device) { Reading = () => metric == Metric.Network ? 2 * 1024 * 1024 : 0.25f };
    });

    /// <summary>What the machine says besides the reading, the same on every PC.</summary>
    private static PerformanceFigures Figures(PerformanceData source) => source.Metric switch
    {
        Metric.Cpu => new PerformanceFigures
        {
            System = new SystemReading(412, 5120, 190_000, 0, 0, 0),
            Uptime = new TimeSpan(3, 4, 12, 45),
            BaseMhz = 3400,
            Logical = 16,
        },
        Metric.Memory => new PerformanceFigures { System = new SystemReading(1, 1, 1, 21 * Gb, 64 * Gb, 6 * Gb) },
        Metric.Gpu => new PerformanceFigures { Graphics = "NVIDIA GeForce RTX 4070" },
        Metric.Disk => new PerformanceFigures { DiskTitle = "Disk 1 (C:)" },
        _ => new PerformanceFigures { Adapter = "Intel(R) Wi-Fi 6 AX201 160MHz" },
    };

    /// <summary>
    /// The preset's folder of <paramref name="holder"/>, its faces' sources the test's
    /// (<paramref name="sources"/>, a fresh registry where none) and its lines' figures made up.
    /// </summary>
    private static FolderIsland Made(Gfx gfx, BarItem holder, DataSources? sources = null, string? key = null, Func<PerformanceData, PerformanceFigures>? figures = null)
    {
        FolderIsland.PerformanceSources = sources ?? Registry();
        PerformanceIsland.FiguresStand = figures ?? Figures;
        try
        {
            return new FolderIsland(key ?? Config.IslandKey(null, holder.Id), holder, gfx, new Config());
        }
        finally
        {
            FolderIsland.PerformanceSources = null;
            PerformanceIsland.FiguresStand = null;
        }
    }

    /// <summary>The folder's reading line: its graph.</summary>
    private static PerformanceIsland LineOf(FolderIsland folder) => (PerformanceIsland)folder.Tiles[0].Island;

    /// <summary>The folder's details line, under its graph.</summary>
    private static PerformanceIsland DetailsOf(FolderIsland folder) => (PerformanceIsland)folder.Tiles[1].Island;

    /// <summary>Two minutes of a wave fed to <paramref name="line"/>, and its latest reading: what the flyouts' pictures were drawn from.</summary>
    private static void Running(PerformanceIsland line, Metric metric, float phase = 0)
    {
        line.Forgotten();
        for (var at = 0; at < 120; at++)
        {
            var wave = 0.35f + (0.3f * MathF.Sin((at + phase) / 9f)) + (0.1f * MathF.Sin((at + phase) / 2.3f));
            line.Feed(
                reading: metric == Metric.Network ? 0 : wave,
                down: metric == Metric.Network ? (wave * 8 * 1024 * 1024) : -1,
                up: metric == Metric.Network ? (wave * 900 * 1024) : -1,
                memory: metric == Metric.Memory ? new MemoryReading((uint)(wave * 100), 32 * Gb, (ulong)((1 - wave) * 32 * Gb)) : null,
                read: metric == Metric.Disk ? wave * 40 * 1024 * 1024 : -1,
                written: metric == Metric.Disk ? wave * 6 * 1024 * 1024 : -1);
        }
    }

    private static IslandStyle BarStyle(int height, float scale = 1f, bool vertical = false, float text = 1f)
    {
        var config = new Config { Height = height };
        var dpi = (uint)MathF.Round(96 * scale);
        return new IslandStyle(scale, vertical, Layout.CellPx(dpi, config), Layout.IconPx(dpi, config), LookFor(dark: true, text));
    }

    /// <summary>The words <paramref name="line"/> draws into a box of <paramref name="rows"/> rows at 150 %; its own height for none.</summary>
    private static List<string> Said(PerformanceIsland line, int? cells, float text = 1f)
    {
        var look = LookFor(dark: true, text);
        var style = FolderFlyout.StyleFor(1.5f, look, FlyoutTheme.For(look));
        var width = FolderFlyout.ListWidth(1.5f);
        var box = PopupPaint.Rect(0, 0, width, cells is { } count ? ListFormPictures.Cells(count, 1.5f, 1f) : line.ListHeight(width, style));
        var said = new List<string>();
        PaintWatch.Said = (words, _) => said.Add(words);
        try
        {
            Offscreen.Bitmap(box.right, box.bottom, dc => line.PaintOn(dc, box, style, WidgetSurface.List), _ => { });
        }
        finally
        {
            PaintWatch.Said = null;
        }
        return said;
    }

    // --- The presets ------------------------------------------------------------------------------------

    /// <summary>
    /// Each reading's flyout is its preset: the reading's graph at a fixed height, its tallest form,
    /// its details under it at their own, then the premade Task Manager footer — one card with no
    /// divider in it (2026-10-11: "they should not have a divider in between them") — under the
    /// reading's face, whose options are the entry's: a pace, and the graphics' adapter and the
    /// disk's disk. System, the five as compact graphs, is kept.
    /// </summary>
    [Fact]
    public void EachReadingIsItsGraphAndItsDetailsOverTaskManager()
    {
        foreach (var metric in Metrics)
        {
            var preset = Presets.Find(IdOf(metric))!;
            var info = WidgetInfos.For(metric);
            var details = WidgetInfos.DetailsOf(metric);
            Assert.Equal((metric, true, info.Glyph), (details.Metric!.Value, details.Facts, details.Glyph));
            Assert.False(info.Facts);
            Assert.Equal(PerformanceFace.Of(metric).Id, preset.Face!.Kind);
            Assert.Equal(metric == Metric.Disk ? "" : info.Glyph, preset.Glyph);
            // The processor's has its top processes over the footer (§141).
            Assert.Equal(metric == Metric.Cpu ? [info.Id, details.Id, "cpuProcesses", "actions"] : [info.Id, details.Id, "actions"], preset.Items.Select(item => item.Form));
            Assert.DoesNotContain(preset.Items, item => item.IsDivider);
            Assert.Equal((LineSize.Full, null), (preset.Items[0].Size, preset.Items[0].Height));
            Assert.Null(preset.Items[1].Height);
            Assert.Same(PremadeWidgets.TaskManagerActions, preset.Items[^1].Premade);

            var folder = Holder(metric).Folder!;
            Assert.Equal(metric, PerformanceFace.MetricOf(folder.Face.Info));
            Assert.Equal(info.Glyph, folder.Face.Info.Glyph);
            var keys = folder.Face.Info.Options.Select(option => (option.Key, option.Kind)).ToList();
            Assert.Equal(
                metric switch
                {
                    Metric.Gpu => [(PerformanceFace.Adapter, FaceOptionKind.Adapter), (PerformanceFace.Style, FaceOptionKind.Choice), (PerformanceFace.Pace, FaceOptionKind.Pace)],
                    Metric.Disk => [(PerformanceFace.Disk, FaceOptionKind.Disk), (PerformanceFace.Style, FaceOptionKind.Choice), (PerformanceFace.Pace, FaceOptionKind.Pace)],
                    _ => new List<(string, FaceOptionKind)> { (PerformanceFace.Style, FaceOptionKind.Choice), (PerformanceFace.Pace, FaceOptionKind.Pace) },
                },
                keys);
            Assert.Equal(ListWidgets.All.Single(line => line.Info == info).Kind, folder.Cells[0].Island.List);
            Assert.Equal(ListWidgets.All.Single(line => line.Info == details).Kind, folder.Cells[1].Island.List);
            Assert.Equal(((string?)null, SettingsActions.TaskManagerId), (folder.Cells[^1].Island.Actions!.Left, folder.Cells[^1].Island.Actions!.Right));
        }
        var system = Presets.Find("systemGroup")!;
        Assert.Null(system.Face);
        Assert.Equal(["cpu", "divider", "memory", "divider", "disk", "network", "divider", "gpu", "divider", "battery", "actions"], system.Items.Select(item => item.Form));
        Assert.Same(PremadeWidgets.TaskManagerActions, system.Items[^1].Premade);
    }

    /// <summary>
    /// Task Manager is a curated action of the actions line, as the calendar app is: opened by its
    /// program, Task Manager's glyph and name; the premade footer has it alone, on the right where
    /// the person put it (2026-10-11).
    /// </summary>
    [Fact]
    public void TaskManagerIsAnActionAndTheFooterIsItAlone()
    {
        var action = SettingsActions.Find(SettingsActions.TaskManagerId)!;
        Assert.Equal("taskmgr.exe", action.Uri);
        Assert.Equal("", action.Glyph);
        Assert.Equal("Task Manager", action.Name);
        Assert.Equal(((string?)null, SettingsActions.TaskManagerId), PremadeWidgets.TaskManagerActions.Actions);
        Assert.Equal(ListKind.Actions, PremadeWidgets.TaskManagerActions.Kind);
        Assert.Contains(PremadeWidgets.TaskManagerActions, PremadeWidgets.Of(ListKind.Actions));
    }

    /// <summary>The entries are gone from the bar, and with them their flyout; the lines are all there is.</summary>
    [Fact]
    public void TheEntriesAreGone()
    {
        foreach (var metric in Metrics)
        {
            var id = WidgetInfos.For(metric).Id;
            Assert.Null(BarEntries.Find(id));
            Assert.NotNull(ListWidgets.Find(id));
        }
        Assert.DoesNotContain(BarEntries.All, entry => entry.Drawing == WidgetSource.Performance);
        Assert.Null(Type.GetType("Karpo.Dock.Performance.PerformanceFlyout, Karpo"));
    }

    // --- The face ---------------------------------------------------------------------------------------

    /// <summary>
    /// The face reads its folder's line of its reading and device — the very source the line draws,
    /// a line fed by a test as well — and, where the folder holds none of that device, the source
    /// the dock gathers for it, held while it wears the face and let go when it is taken off.
    /// </summary>
    [UiFact]
    public void TheFaceReadsTheFoldersLineOrTheSharedSource()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var sources = Registry();
            using var folder = Made(gfx, Holder(Metric.Cpu), sources);
            var line = LineOf(folder);
            Assert.Same(line.Source, folder.Performance);
            line.Feed(0.42f);
            Assert.Same(line.Source, folder.Performance);

            // The disk's face set to a disk its line does not follow: the face's own source.
            var disk = Holder(Metric.Disk);
            disk.Folder!.Face.Set(FolderFaces.Disk.Option(PerformanceFace.Disk)!, "Disk 1");
            using var held = Made(gfx, disk, sources);
            var one = PerformanceData.KeyOf(Metric.Disk, new PerformanceOptions { Disk = "Disk 1" });
            Assert.Same(sources.Find(one), held.Performance);
            Assert.NotSame(LineOf(held).Source, held.Performance);

            // Another face: nothing read, and the source let go.
            disk.Folder.Face = FolderFace.Of(FolderFaces.IconId);
            held.Configure(disk, new Config());
            Assert.Null(held.Performance);
            Assert.Null(sources.Find(one));
        });
    }

    /// <summary>
    /// The face is what the entry was: the figure (a share, or down and up together as a short
    /// rate), a dash before any reading; painted again in place at its pace in one width up to 99 %,
    /// a full hundred laying the bar out again (docs\widgets.md §244); its tooltip the entry's words,
    /// which only a screen reader hears over the bar.
    /// </summary>
    [UiFact]
    public void TheFaceSaysTheFigureAndPaintsAgainInPlaceAtItsPace()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var holder = Holder(Metric.Cpu);
            holder.Folder!.Face.Set(FolderFaces.Cpu.Option(PerformanceFace.Pace)!, "5");
            using var folder = Made(gfx, holder);
            Assert.Equal(5000, folder.IntervalMs);
            var line = LineOf(folder);
            var style = BarStyle(48);
            var size = folder.CellSize(0, style);
            _ = folder.Measure(style);
            line.Forgotten();
            _ = folder.Surface(0, style);
            Assert.Equal("–", folder.FaceShown);

            // The first reading takes two figures' room, wider than the dash: the bar is laid out again.
            line.Feed(0.42f);
            Assert.True(folder.Tick(gfx));
            size = folder.CellSize(0, style);
            folder.Forget();
            _ = folder.Measure(style);
            _ = folder.Surface(0, style);
            Assert.Equal("42%", folder.FaceShown);
            Assert.Equal(S.WidgetPerformanceProcessorShare("42%"), folder.Tooltip(0));

            // In one width up to 99 %; a full hundred is a figure more and lays the bar out again.
            line.Feed(0.07f);
            Assert.False(folder.Tick(gfx));
            Assert.Equal("7%", folder.FaceShown);
            Assert.Equal(size, folder.CellSize(0, style));
            line.Feed(1f);
            Assert.True(folder.Tick(gfx));
            Assert.True(folder.CellSize(0, style).Width > size.Width, "100% is no wider than 42%");
            folder.Forget();
            _ = folder.Measure(style);
            _ = folder.Surface(0, style);
            Assert.Equal("100%", folder.FaceShown);

            // The network: down and up together, as the entry said it.
            using var network = Made(gfx, Holder(Metric.Network));
            var net = LineOf(network);
            net.Feed(down: 3 * 1024 * 1024, up: 1024 * 1024);
            _ = network.Measure(style);
            _ = network.Surface(0, style);
            Assert.Equal(Units.ShortRate(4 * 1024 * 1024), network.FaceShown);
            Assert.Equal(1000, network.IntervalMs);
        });
    }

    /// <summary>
    /// The face is as wide as its figure (docs\widgets.md §244), never narrower than two figures or a
    /// rate's room: 0 % and 7 % one width, at every bar, across and down, at ordinary and
    /// the largest text; a full hundred across a bar no narrower, and a rate one width whatever it is.
    /// </summary>
    [UiFact]
    public void TheFaceIsOneWidthUpToTwoFigures()
    {
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var metric in Metrics)
            {
                using var folder = Made(gfx, Holder(metric));
                var line = LineOf(folder);
                foreach (var height in new[] { 28, 48, 72 })
                foreach (var vertical in new[] { false, true })
                foreach (var text in new[] { 1f, 2.25f })
                {
                    var style = BarStyle(height, 1.5f, vertical, text);
                    var sizes = new List<(float, float)>();
                    line.Forgotten();
                    sizes.Add(folder.CellSize(0, style));
                    foreach (var share in new[] { 0f, 0.07f, 1f })
                    {
                        line.Feed(share, down: share * 900 * 1024 * 1024, up: 0);
                        sizes.Add(folder.CellSize(0, style));
                    }
                    var where = $"{metric} at {height} DIP{(vertical ? ", vertical" : "")}, text ×{text}: {string.Join(", ", sizes)}";
                    // The first is what the line kept from the bar before: 0 % and 7 % are what is held.
                    var rate = metric == Metric.Network;
                    Assert.True(sizes.Skip(1).Take(rate || vertical ? 3 : 2).Distinct().Count() == 1, where);
                    Assert.True(sizes[3].Item1 >= sizes[1].Item1, where);
                }
            }
        });
    }

    /// <summary>
    /// The face's right click has Task Manager, which the folder's footer opens, and its line's
    /// Resource Monitor (Dock.FaceMenus.cs).
    /// </summary>
    [Fact]
    public void TheFacesRowsAreTaskManagerAndResourceMonitor()
    {
        foreach (var metric in Metrics)
        {
            Assert.Equal([WidgetAct.TaskManager, WidgetAct.ResourceMonitor], TheDock.ActsFor(PerformanceFace.Of(metric)));
        }
    }

    // --- The line at its Full size --------------------------------------------------------------------------

    /// <summary>
    /// At the preset's size, Full, the graph is the top of the flyout: its name (the disk's with its
    /// letters), the figure, what the graph's top stands for and how far back it reaches — and none
    /// of the facts, which are the details'. The details at their own height say every fact of the
    /// flyout, the processor's seven, and nothing of the graph's, at the largest text too. The graph
    /// Compact has no minute's axes (§603).
    /// </summary>
    [UiFact]
    public void AtThePresetsHeightTheGraphAndTheDetailsAreTheFlyout()
    {
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var metric in Metrics)
            {
                var holder = Holder(metric);
                using var folder = Made(gfx, holder);
                var line = LineOf(folder);
                var details = DetailsOf(folder);
                Running(line, metric);
                Running(details, metric);
                Assert.Equal(LineSize.Full, holder.Folder!.Cells[0].Island.Size);
                var model = PerformanceModel.For(line.Source, Figures(line.Source));
                var said = Said(line, null);
                Assert.Contains(model.Title, said);
                Assert.Contains(model.Value, said);
                Assert.Contains(PerformanceModel.Span, said);
                Assert.Contains(model.Top, said);
                Assert.DoesNotContain(model.Facts[^1].Label, said);

                var facts = PerformanceModel.For(details.Source, Figures(details.Source));
                var listed = Said(details, null);
                foreach (var (label, figure) in facts.Facts)
                {
                    Assert.Contains(label, listed);
                    Assert.Contains(figure, listed);
                }
                Assert.DoesNotContain(PerformanceModel.Span, listed);
                Assert.Equal(facts.Facts.Count * 2, listed.Count);
                if (metric == Metric.Cpu)
                {
                    Assert.Equal(7, facts.Facts.Count);
                }
                // At the largest text, in the most rows a line has: every fact still, none left out
                // (the processor's up time was, where each short fact took a row of its own).
                var large = Said(details, FolderOptions.MostCells, 2.25f);
                Assert.Equal(facts.Facts.Count * 2, large.Count);

                ((CellItem)line.Settings).Size = LineSize.Compact;
                var compact = Said(line, null);
                ((CellItem)line.Settings).Size = LineSize.Full;
                Assert.DoesNotContain(PerformanceModel.Span, compact);
                Assert.DoesNotContain(model.Top, compact);
            }
        });
    }

    /// <summary>
    /// The machine's figures are read once a gather of the source at most, and only for a line that
    /// shows them, Full (§603): paints between two readings read nothing again.
    /// </summary>
    [UiFact]
    public void TheFiguresAreReadOnceAReadingAndOnlyWhereShown()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var reads = 0;
            using var folder = Made(gfx, Holder(Metric.Cpu), figures: source =>
            {
                reads++;
                return Figures(source);
            });
            var line = LineOf(folder);
            Running(line, Metric.Cpu);
            ((CellItem)line.Settings).Size = LineSize.Compact;
            _ = Said(line, null);
            Assert.Equal(0, reads);
            ((CellItem)line.Settings).Size = LineSize.Full;
            _ = Said(line, 18);
            _ = Said(line, 18);
            Assert.Equal(1, reads);
            line.Feed(0.5f);
            _ = Said(line, 18);
            Assert.Equal(2, reads);
        });
    }

    // --- System (2026-10-11: "add more details and also make a divider in between") --------------------

    /// <summary>System's folder, as the add list makes it on a PC with no battery.</summary>
    private static BarItem SystemHolder() => Presets.Folder(Presets.Find("systemGroup")!, Machine);

    /// <summary>System's readings, each fed two minutes of a wave.</summary>
    private static List<PerformanceIsland> SystemLines(FolderIsland folder)
    {
        var lines = folder.Tiles.Select(tile => tile.Island).OfType<PerformanceIsland>().ToList();
        foreach (var line in lines)
        {
            Running(line, line.Source.Metric);
        }
        return lines;
    }

    /// <summary>
    /// Each of System's readings, Compact (§603), says its name, its figure and its second reading — the busiest moment,
    /// the memory in use of all there is, read and write, down and up — over its graph; and the folder, a card a reading
    /// in two lists, stands whole in the 70 % of a 1440p screen at 150 % over a 48-DIP bar a folder may take
    /// (FolderFlyout), with no scrolling.
    /// </summary>
    [UiFact]
    public void SystemsReadingsSayTheirSecondReadingAndFitA1440pScreen()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var holder = SystemHolder();
            using var folder = Made(gfx, holder);
            var lines = SystemLines(folder);
            folder.Opened(gfx);
            Assert.Equal([Metric.Cpu, Metric.Memory, Metric.Disk, Metric.Network, Metric.Gpu], lines.Select(line => line.Source.Metric));
            foreach (var line in lines)
            {
                var metric = line.Source.Metric;
                Assert.Equal(LineSize.Compact, LineSizes.Of(line.Settings));
                var said = Said(line, null);
                Assert.Contains(PerformanceIsland.Drawn(metric), said);
                foreach (var (text, _) in line.CompactParts())
                {
                    Assert.Contains(said, words => words.Contains(text.Trim(), StringComparison.Ordinal));
                }
                Assert.NotEmpty(line.CompactParts());
            }

            var plan = Plan(folder, LookFor(dark: true), 1.5f).Plan;
            Assert.Equal(2, plan.Lists.Count);
            Assert.True(plan.Height / 1.5f <= 0.7f * ((1440 / 1.5f) - 48), $"System stands {plan.Height / 1.5f} DIP");
        });
    }

    /// <summary>
    /// The processor's preset with its top processes — four apps under the graph and the details —
    /// stands whole in the 70 % of a 1440p screen at 150 % over a 48-DIP bar a folder may take.
    /// </summary>
    [UiFact]
    public void TheProcessorsPresetWithItsProcessesFitsA1440pScreen()
    {
        Offscreen.OnStaThread(gfx =>
        {
            using var folder = Made(gfx, Holder(Metric.Cpu));
            Running(LineOf(folder), Metric.Cpu);
            Running(DetailsOf(folder), Metric.Cpu);
            folder.Opened(gfx);
            var look = LookFor(dark: true);
            var plan = Plan(folder, look, 1.5f).Plan;
            Assert.True(plan.Height / 1.5f <= 0.7f * ((1440 / 1.5f) - 48), $"The processor stands {plan.Height / 1.5f} DIP");
            var top = Assert.IsType<ProcessesLine>(folder.Tiles[2].Island);
            var card = plan.Cards[2];
            var style = FolderFlyout.StyleFor(1.5f, look, FlyoutTheme.For(look));
            Assert.Equal(Presets.ProcessCells / LineHeight.Row, top.Plan(PopupPaint.Rect(0, 0, card.right - card.left, card.bottom - card.top), style).Count);
        });
    }

    /// <summary>System as it opens, at 150 %, dark and light, and at 225 % text: system-preset-&lt;dark|light&gt;.png.</summary>
    [UiFact]
    public void SystemPresetPictures()
    {
        foreach (var dark in new[] { true, false })
        {
            Offscreen.OnStaThread(gfx =>
            {
                var folders = new List<FolderIsland>();
                try
                {
                    var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
                    foreach (var (caption, text) in new[] { ("System", 1f), ("System: 225 % text", 2.25f) })
                    {
                        var look = LookFor(dark, text);
                        var holder = SystemHolder();
                        var folder = Made(gfx, holder, key: $"{Owner}/{holder.Id}-{panels.Count}");
                        folders.Add(folder);
                        SystemLines(folder);
                        folder.Opened(gfx);
                        panels.Add((caption, look, folder, Plan(folder, look, 1.5f).Plan));
                    }
                    Sheet(gfx, $"system-preset-{(dark ? "dark" : "light")}.png", 1.5f, panels);
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

    // --- Pictures --------------------------------------------------------------------------------------

    /// <summary>
    /// Each preset as it opens, at 150 %, to stand beside performance-&lt;reading&gt;-*.png of the
    /// flyouts: the five, a processor not read yet, and the processor at 225 % text.
    /// </summary>
    [UiFact]
    public void PerformancePresetPictures()
    {
        foreach (var dark in new[] { true, false })
        {
            Offscreen.OnStaThread(gfx =>
            {
                var folders = new List<FolderIsland>();
                try
                {
                    var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
                    void Add(string caption, Metric metric, bool read = true, float text = 1f)
                    {
                        var look = LookFor(dark, text);
                        var holder = Holder(metric);
                        var folder = Made(gfx, holder, key: $"{Owner}/{holder.Id}-{panels.Count}");
                        folders.Add(folder);
                        foreach (var line in new[] { LineOf(folder), DetailsOf(folder) })
                        {
                            if (read)
                            {
                                Running(line, metric);
                            }
                            else
                            {
                                line.Forgotten();
                            }
                        }
                        folder.Opened(gfx);
                        panels.Add((caption, look, folder, Plan(folder, look, 1.5f).Plan));
                    }

                    foreach (var metric in Metrics)
                    {
                        Add(WidgetInfos.For(metric).Title, metric);
                    }
                    Add("Processor: not read yet", Metric.Cpu, read: false);
                    Add("Processor: 225 % text", Metric.Cpu, text: 2.25f);
                    Sheet(gfx, $"performance-preset-{(dark ? "dark" : "light")}.png", 1.5f, panels);
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
    /// The five faces on a bar of 40 and 48 DIP and at 150 %, then down a bar on its side, and a
    /// processor not read yet — performance-face-&lt;dark|light&gt;.png, beside the entries'
    /// perf-bar-*.png before they went.
    /// </summary>
    [UiFact]
    public void PerformanceFacePictures()
    {
        foreach (var dark in new[] { true, false })
        {
            Offscreen.OnStaThread(gfx =>
            {
                var look = LookFor(dark);
                var theme = FlyoutTheme.For(look);
                var islands = Metrics.Select(metric =>
                {
                    var folder = Made(gfx, Holder(metric));
                    Running(LineOf(folder), metric);
                    return folder;
                }).ToList();
                var unread = Made(gfx, Holder(Metric.Cpu));
                LineOf(unread).Forgotten();
                islands.Add(unread);
                IslandStyle Style(int height, float scale, bool vertical = false)
                {
                    var config = new Config { Height = height };
                    var dpi = (uint)MathF.Round(96 * scale);
                    return new IslandStyle(scale, vertical, Layout.CellPx(dpi, config), Layout.IconPx(dpi, config), look);
                }
                var across = new[] { Style(40, 1f), Style(48, 1f), Style(48, 1.5f) };
                var down = Style(48, 1.5f, vertical: true);
                try
                {
                    const float gap = 12;
                    var width = gap + across.Sum(style => islands.Sum(island => island.CellSize(0, style).Width + gap) + gap);
                    var downHeight = islands.Sum(island => island.CellSize(0, down).Height + gap);
                    var height = gap + MathF.Max(across.Max(style => islands.Max(island => island.CellSize(0, style).Height)), downHeight) + gap;
                    width += down.Cell + gap;
                    Offscreen.Bitmap(width, height, dc =>
                    {
                        PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, width, height), 0, PopupPaint.Ink(theme.Base));
                        var x = gap;
                        foreach (var style in across)
                        {
                            foreach (var island in islands)
                            {
                                var (w, h) = island.CellSize(0, style);
                                PopupPaint.Fill(dc, PopupPaint.Rect(x, gap, w, h), 4 * style.Scale, PopupPaint.Ink(theme.CardBackground));
                                var shown = style;
                                Offscreen.Shifted(dc, x, gap, () => island.PaintCell(dc, 0, shown, w, h));
                                x += w + gap;
                            }
                            x += gap;
                        }
                        var y = gap;
                        foreach (var island in islands)
                        {
                            var (w, h) = island.CellSize(0, down);
                            PopupPaint.Fill(dc, PopupPaint.Rect(x, y, w, h), 4 * down.Scale, PopupPaint.Ink(theme.CardBackground));
                            var at = y;
                            Offscreen.Shifted(dc, x, at, () => island.PaintCell(dc, 0, down, w, h));
                            y += h + gap;
                        }
                    }, bitmap => Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, $"performance-face-{(dark ? "dark" : "light")}.png")));

                    // Each face alone: it drew, and inside its cell.
                    foreach (var style in across.Append(down))
                    {
                        foreach (var island in islands)
                        {
                            var (w, h) = island.CellSize(0, style);
                            var inked = default((float Left, float Top, float Right, float Bottom));
                            var shown = style;
                            Offscreen.Bitmap(w, h, dc => island.PaintCell(dc, 0, shown, w, h),
                                bitmap => inked = Offscreen.InkOf(bitmap, (uint)MathF.Ceiling(w), (uint)MathF.Ceiling(h)));
                            Assert.True(inked.Right > inked.Left, $"{island.Options.Face.Kind} drew nothing");
                        }
                    }
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

    // --- The graph style (§295) -------------------------------------------------------------------

    /// <summary>A face of <paramref name="metric"/>, in its graph style where <paramref name="graph"/>, else as a new one is: its figure.</summary>
    private static FolderFace Face(Metric metric, bool graph)
    {
        var face = FolderFace.Of(PerformanceFace.Of(metric).Id);
        if (graph)
        {
            face.Set(face.Info.Option(PerformanceFace.Style)!, PerformanceFace.Graph);
        }
        return face;
    }

    /// <summary>
    /// The system folder — a line of every reading — wearing <paramref name="faces"/>, each line fed two
    /// minutes of its own wave: every face reads its folder's line, so each draws a history.
    /// </summary>
    private static FolderIsland Wearing(Gfx gfx, params FolderFace[] faces)
    {
        var holder = Presets.Folder(Presets.Find("systemGroup")!, Machine);
        holder.Folder!.Faces = [.. faces];
        var folder = Made(gfx, holder);
        foreach (var line in folder.Tiles.Select(tile => tile.Island).OfType<PerformanceIsland>())
        {
            Running(line, line.Source.Metric, 17 * (int)line.Source.Metric);
        }
        return folder;
    }

    /// <summary>The folder's line of <paramref name="metric"/>.</summary>
    private static PerformanceIsland LineOf(FolderIsland folder, Metric metric) =>
        folder.Tiles.Select(tile => tile.Island).OfType<PerformanceIsland>().First(line => line.Source.Metric == metric);

    /// <summary>
    /// Every reading's face has one Style row (§292), the performance widget's own two words for it:
    /// Numbers, what the face always was and still is where nobody chose, and Graph, which the face then
    /// keeps as its line keeps it (ShowGraph).
    /// </summary>
    [Fact]
    public void EachReadingsFaceIsItsNumbersOrItsGraph()
    {
        foreach (var metric in Metrics)
        {
            var folder = Holder(metric).Folder!;
            var row = OptionRows.OfFace(folder, 0).Single(row => row.Slot == OptionSlot.Style);
            Assert.Equal((PerformanceFace.Style, OptionRowKind.Choice, S.SettingsWidgetStyleHeader), (row.Key, row.Kind, row.Title));
            Assert.Equal([Strings.Text("core.widgetStyle.performance.figure"), Strings.Text("core.widgetStyle.performance.graph")], row.Choices);
            Assert.Equal(["Numbers", "Graph"], new[] { PerformanceFace.Figure, PerformanceFace.Graph }.Select(word => Strings.Lookup($"core.widgetStyle.performance.{word}", "en")));
            Assert.Equal(0, row.Chosen);
            Assert.False(PerformanceFace.Options(folder.Face).ShowGraph);

            Assert.True(row.Set!(1));
            Assert.Equal(PerformanceFace.Graph, folder.Face.Text(folder.Face.Info.Option(PerformanceFace.Style)!));
            Assert.True(PerformanceFace.Options(folder.Face).ShowGraph);
            Assert.Equal(1, OptionRows.OfFace(folder, 0).Single(row => row.Key == PerformanceFace.Style).Chosen);
        }
    }

    /// <summary>
    /// The graph face is the figure's face and a room for its graph between the glyph and the figure, a
    /// square of the bar's thickness and never under 32 DIP, at every bar, across and down, at 100 and
    /// 150 % (§275: what it shows and its padding); its history is drawn in that room, and nothing is
    /// there before anything was read. Each step is a new reading, so a clock's tick paints it again in
    /// place and never lays the bar out.
    /// </summary>
    [UiFact]
    public void TheGraphFaceIsItsFigureAndARoomWhereItsHistoryIsDrawn()
    {
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var metric in Metrics)
            {
                using var figure = Wearing(gfx, Face(metric, graph: false));
                using var graph = Wearing(gfx, Face(metric, graph: true));
                foreach (var height in new[] { 28, 48, 72 })
                foreach (var vertical in new[] { false, true })
                foreach (var scale in new[] { 1f, 1.5f })
                {
                    var style = BarStyle(height, scale, vertical);
                    var fit = new Fit(style);
                    var room = PerformanceIsland.GraphRoom(fit);
                    var (figureWidth, figureHeight) = figure.CellSize(0, style);
                    var (graphWidth, graphHeight) = graph.CellSize(0, style);
                    var where = $"{metric} at {height} DIP, {scale * 100}%{(vertical ? ", vertical" : "")}";
                    Assert.True(room >= fit.Px(32) && room >= fit.Px(fit.Thick) - 0.5f, where);
                    Assert.InRange((vertical ? graphHeight - figureHeight : graphWidth - figureWidth) - room, -1, 1);
                    Assert.Equal(vertical ? figureWidth : figureHeight, vertical ? graphWidth : graphHeight);
                }

                foreach (var vertical in new[] { false, true })
                {
                    var style = BarStyle(48, 1.5f, vertical);
                    var fit = new Fit(style);
                    var (w, h) = graph.CellSize(0, style);
                    var box = FolderIsland.FaceBoxes(gfx, graph.Options, style, w, h, null, graph)[0];
                    // The room's middle, past the fade under the glyph and the figure.
                    var start = (vertical ? box.top : box.left) + PerformanceIsland.FaceRoom(gfx, metric, style).GlyphPx + (vertical ? fit.Px(2) : fit.Gap) + (2 * fit.Gap);
                    var end = start + PerformanceIsland.GraphRoom(fit) - (4 * fit.Gap);
                    var clip = vertical ? new D2D_RECT_F(0, start, w, end) : new D2D_RECT_F(start, 0, end, h);
                    (float Left, float Top, float Right, float Bottom) Inked()
                    {
                        var inked = default((float, float, float, float));
                        Offscreen.Bitmap(w, h, dc =>
                        {
                            var at = clip;
                            dc->PushAxisAlignedClip(&at, D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_ALIASED);
                            graph.PaintCell(dc, 0, style, w, h);
                            dc->PopAxisAlignedClip();
                        }, bitmap => inked = Offscreen.InkOf(bitmap, (uint)MathF.Ceiling(w), (uint)MathF.Ceiling(h)));
                        return inked;
                    }
                    // Two minutes at three quarters, so the graph crosses the room's middle whatever its reading.
                    var held = LineOf(graph, metric);
                    held.Forgotten();
                    for (var at = 0; at < 120; at++)
                    {
                        held.Feed(metric == Metric.Network ? -1 : 0.75f, down: metric == Metric.Network ? 6 * 1024 * 1024 : -1, up: metric == Metric.Network ? 0 : -1);
                    }
                    var drawn = Inked();
                    Assert.True(drawn.Right > drawn.Left, $"{metric}{(vertical ? " down a bar" : "")}: no graph in its room");
                    held.Forgotten();
                    var none = Inked();
                    Assert.True(none.Right <= none.Left, $"{metric}{(vertical ? " down a bar" : "")}: a graph before anything was read");
                    Running(LineOf(graph, metric), metric, 17 * (int)metric);
                }

                // A step more of the same figure: painted again in place, the bar not laid out again.
                var across = BarStyle(48);
                var line = LineOf(graph, metric);
                _ = graph.Measure(across);
                _ = graph.Surface(0, across);
                var shown = graph.FaceShown;
                var newest = line.Source.History.Ago(0);
                line.Feed(metric == Metric.Network ? -1 : newest, down: metric == Metric.Network ? newest : -1, up: metric == Metric.Network ? 0 : -1);
                Assert.False(graph.Tick(gfx), $"{metric}: a step laid the bar out again");
                _ = graph.Surface(0, across);
                Assert.Equal(shown.Split('|')[0], graph.FaceShown.Split('|')[0]);
                Assert.NotEqual(shown, graph.FaceShown);
            }
        });
    }

    /// <summary>
    /// The five faces in their numbers and in their graph, and three graphs in one button, on a 48 DIP
    /// bar at 100 and 150 %, then the graphs down a bar on its side — performance-graph-face-&lt;dark|light|contrast&gt;.png.
    /// </summary>
    [UiFact]
    public void PerformanceGraphFacePictures()
    {
        foreach (var theme in new[] { "dark", "light", "contrast" })
        {
            Offscreen.OnStaThread(gfx =>
            {
                var look = theme == "contrast"
                    ? LookFor(true) with
                    {
                        HighContrast = true,
                        HcWindow = Windows.UI.Color.FromArgb(255, 0, 0, 0),
                        HcText = Windows.UI.Color.FromArgb(255, 255, 255, 255),
                        HcHighlight = Windows.UI.Color.FromArgb(255, 0x1A, 0xEB, 0xFF),
                        HcHighlightText = Windows.UI.Color.FromArgb(255, 0, 0, 0),
                    }
                    : LookFor(theme == "dark");
                var flyout = FlyoutTheme.For(look);
                var figures = Metrics.Select(metric => Wearing(gfx, Face(metric, graph: false))).ToList();
                var graphs = Metrics.Select(metric => Wearing(gfx, Face(metric, graph: true))).ToList();
                var three = Wearing(gfx, Face(Metric.Cpu, true), Face(Metric.Memory, true), Face(Metric.Network, true));
                var rows = new List<FolderIsland>[] { figures, [.. graphs, three] };
                IslandStyle Style(float scale, bool vertical = false)
                {
                    var config = new Config { Height = 48 };
                    var dpi = (uint)MathF.Round(96 * scale);
                    return new IslandStyle(scale, vertical, Layout.CellPx(dpi, config), Layout.IconPx(dpi, config), look);
                }
                var across = new[] { Style(1f), Style(1.5f) };
                var down = Style(1.5f, vertical: true);
                try
                {
                    const float gap = 12;
                    var width = gap + across.Max(style => rows.Max(row => row.Sum(island => island.CellSize(0, style).Width + gap))) + gap + down.Cell + gap;
                    var acrossHeight = across.Sum(style => rows.Length * (style.Cell + gap));
                    var downHeight = graphs.Sum(island => island.CellSize(0, down).Height + gap);
                    var height = gap + MathF.Max(acrossHeight, downHeight) + gap;
                    Offscreen.Bitmap(width, height, dc =>
                    {
                        PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, width, height), 0, PopupPaint.Ink(flyout.Base));
                        var y = gap;
                        var right = 0f;
                        foreach (var style in across)
                        {
                            foreach (var row in rows)
                            {
                                var x = gap;
                                foreach (var island in row)
                                {
                                    var (w, h) = island.CellSize(0, style);
                                    PopupPaint.Fill(dc, PopupPaint.Rect(x, y, w, h), Karpo.Dock.Graphs.GraphBackdrop.BarRadius(style), PopupPaint.Ink(flyout.CardBackground));
                                    var shown = style;
                                    var at = (x, y);
                                    Offscreen.Shifted(dc, at.x, at.y, () => island.PaintCell(dc, 0, shown, w, h));
                                    x += w + gap;
                                }
                                right = MathF.Max(right, x);
                                y += style.Cell + gap;
                            }
                        }
                        var column = right + gap;
                        var top = gap;
                        foreach (var island in graphs)
                        {
                            var (w, h) = island.CellSize(0, down);
                            PopupPaint.Fill(dc, PopupPaint.Rect(column, top, w, h), Karpo.Dock.Graphs.GraphBackdrop.BarRadius(down), PopupPaint.Ink(flyout.CardBackground));
                            var at = top;
                            Offscreen.Shifted(dc, column, at, () => island.PaintCell(dc, 0, down, w, h));
                            top += h + gap;
                        }
                    }, bitmap => Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, $"performance-graph-face-{theme}.png")));
                }
                finally
                {
                    foreach (var island in figures.Concat(graphs).Append(three))
                    {
                        island.Dispose();
                    }
                }
            });
        }
    }
}
