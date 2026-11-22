// The disk widget is the disk's activity, a disk at a time (docs\widgets.md §24).
//
// The person, 2026-10-04: "disk widget should show diskio as graph not the storage space". The
// widget they meant was the one a new bar came with, a Drive widget for the drive Windows is on —
// "Local Disk C:" beside a bar of how full it is. A new bar has the Disk widget there now, which
// graphs the disk's active time (Task Manager's Disk graph) over the history its source gathered
// from the dock's start, keeps its read and write figures, and follows every disk together or one
// disk ("Disk 1") as Task Manager's Disk 0 and Disk 1 do. How full each volume is moved to its
// tooltip; the Drive widget still shows it, in the add list.
//
// What is held here: the graph's data source is a disk's activity and never a drive's space; a
// widget following one disk draws that disk and no other, and says which; every disk followed has
// a history of its own from the start; the file keeps the disk by number; the settings card's list;
// the new bar. And pictures: the disk preset's face following one disk on the bar at 28, 41 and 72
// DIP and the line as one to six rows tall, at 100 and 150 %, dark and light (disk-activity-*.png in
// KARPO_PNG_DIR), with what it drew read back. The taskbar entry went with the readings' presets
// (docs\widgets.md §97): on the bar the disk is its preset's face now.
//
// Every reading here is handed over (Feed, PerformanceData.Reading) or made up (DiskSampler.Pick
// over readings the test chose), never this PC's: the pictures and the figures are the same on
// every machine.

using Karpo.Core;
using Karpo.Dock;
using Karpo.Dock.Graphs;
using Karpo.Tests.Folders;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;

namespace Karpo.Tests.Perf;

[Collection(SamplerState.Collection)]
public sealed unsafe class DiskActivityTests
{
    // --- What is plotted ---------------------------------------------------------------------

    /// <summary>
    /// The widget the person calls the disk widget draws a disk's activity: its kinds on the bar and
    /// in a list are the performance graph, whose source reads the disks' active time, and never the
    /// drive widget, which reads a volume's space.
    /// </summary>
    [Fact]
    public void TheDiskGraphsSourceIsTheDisksActivityNotADrivesSpace()
    {
        Assert.Equal(Metric.Disk, WidgetInfos.Disk.Metric);
        Assert.Null(WidgetInfos.Disk.Drives);
        Assert.Equal(WidgetSource.Performance, ListWidgets.Find(ListKind.Disk)!.Drawing);

        var widget = ListKind.Disk.New();
        var key = SourceKinds.KeyOf(widget);
        Assert.NotNull(key);
        var source = Assert.IsType<PerformanceData>(SourceKinds.Make(key!.Value, widget));
        Assert.Equal(Metric.Disk, source.Metric);
        Assert.Null(source.Disk);
        source.Dispose();
    }

    /// <summary>
    /// One disk is that disk's own reading — its active time, its read, its write — and a disk that
    /// was not read (unplugged, or never there) is not known throughout, never another disk's figures
    /// under its name and never a zero.
    /// </summary>
    [Fact]
    public void OneDiskIsThatDisksReadingAndNoOther()
    {
        DiskReading[] read = [new(0, 0.2f, 100, 200), new(1, 0.9f, 5_000, 0), new(3, 0.05f, 0, 70)];

        Assert.Equal((0.2f, 100d, 200d), DiskSampler.Pick(read, 0));
        Assert.Equal((0.9f, 5_000d, 0d), DiskSampler.Pick(read, 1));
        Assert.Equal((0.05f, 0d, 70d), DiskSampler.Pick(read, 3));
        Assert.Equal((-1f, -1d, -1d), DiskSampler.Pick(read, 2));
        Assert.Equal((-1f, -1d, -1d), DiskSampler.Pick([], 0));
    }

    /// <summary>
    /// Every disk together is the busiest one's active time and what they all read and write added
    /// up, as Task Manager sums them; a disk whose counter went backwards (-1) neither wins nor adds,
    /// and nothing read at all is not known.
    /// </summary>
    [Fact]
    public void AllDisksAreTheBusiestOnesTimeAndEveryonesBytes()
    {
        DiskReading[] read = [new(0, 0.2f, 100, 200), new(1, 0.9f, 5_000, 0), new(2, -1f, -1, -1)];

        Assert.Equal((0.9f, 5_100d, 200d), DiskSampler.Pick(read, null));
        Assert.Equal((-1f, -1d, -1d), DiskSampler.Pick([], null));
        Assert.Equal((-1f, 0d, 0d), DiskSampler.Pick([new(4, -1f, -1, -1)], null));
    }

    // --- Naming a disk -----------------------------------------------------------------------

    [Theory]
    [InlineData("Disk 0", 0)]
    [InlineData("disk 2", 2)]
    [InlineData("  Disk 3 ", 3)]
    [InlineData("Disk 1 (E:)", 1)]
    [InlineData("Disk 0 (C: D:)", 0)]
    [InlineData("DISK 12", 12)]
    [InlineData("Disk", null)]
    [InlineData("Disk x", null)]
    [InlineData("Disk 12x", null)]
    [InlineData("Disk -1", null)]
    [InlineData("All disks", null)]
    [InlineData("GPU 0: Intel UHD", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void ADiskIsReadBackByItsNumber(string? name, int? number) =>
        Assert.Equal(number, PerformanceOptions.DiskNumber(name));

    /// <summary>Task Manager's names: the number, and the letters of the volumes on it where it has any.</summary>
    [Fact]
    public void ADiskIsLabelledAsTaskManagerLabelsIt()
    {
        Assert.Equal("Disk 0", PerformanceOptions.DiskName(0));
        Assert.Equal("Disk 0 (C: D:)", PerformanceOptions.DiskLabel(0, ["C:", "D:"]));
        Assert.Equal("Disk 1 (E:)", PerformanceOptions.DiskLabel(1, ["E:"]));
        Assert.Equal("Disk 2", PerformanceOptions.DiskLabel(2, []));
        Assert.Equal("Disk 2", PerformanceOptions.DiskLabel(2, [""]));
        Assert.Equal(1, PerformanceOptions.DiskNumber(PerformanceOptions.DiskLabel(1, ["E:", "F:"])));
    }

    /// <summary>
    /// The file keeps a disk graph's disk as "Disk N" — read back however it was written — and keeps
    /// nothing for every disk together, for a name that is no disk, or for any other kind; and
    /// nothing is written for a disk not chosen, so a file from before reads and writes the same.
    /// </summary>
    [Fact]
    public void TheFileKeepsADiskGraphsDiskByNumber()
    {
        CellItem Disk(string? disk)
        {
            var widget = ListKind.Disk.New();
            widget.Id = "d";
            widget.Performance = new PerformanceOptions { Disk = disk };
            widget.Normalize();
            return widget;
        }

        Config Holding(CellItem line)
        {
            var folder = BarKind.FolderList.New();
            folder.Id = "f";
            folder.Folder!.Put(line);
            var config = new Config();
            config.DefaultLayout.Islands.Add(folder);
            return config;
        }
        Assert.Equal("Disk 1", Disk(" disk 1 ").Performance!.Disk);
        Assert.Equal("Disk 0", Disk("Disk 0 (C:)").Performance!.Disk);
        Assert.Null(Disk("All disks").Performance!.Disk);
        Assert.Null(Disk("").Performance!.Disk);
        Assert.Null(Disk(null).Performance!.Disk);

        // A graph in the processor's style keeps the disk for its disk style (§209), as a disk's.
        var cpu = ListKind.Cpu.New();
        cpu.Performance = new PerformanceOptions { Disk = "disk 1" };
        cpu.Normalize();
        Assert.Equal("Disk 1", cpu.Performance!.Disk);
        cpu.Performance.Disk = "All disks";
        cpu.Normalize();
        Assert.Null(cpu.Performance!.Disk);

        var text = Holding(Disk("Disk 1")).ToJson();
        Assert.Contains("\"disk\": \"Disk 1\"", text, StringComparison.Ordinal);

        var none = Holding(Disk(null));
        Assert.DoesNotContain("\"disk\": null", none.ToJson(), StringComparison.Ordinal);
        Assert.DoesNotContain("\"disk\": \"\"", none.ToJson(), StringComparison.Ordinal);
    }

    // --- The sources and their histories -------------------------------------------------------

    /// <summary>
    /// A disk followed is a source of its own — another disk is another series of samples — and the
    /// same disk however it was written is one source; every disk together is a third. Another kind
    /// is not split by a disk it cannot have.
    /// </summary>
    [Fact]
    public void EachDiskFollowedIsASourceOfItsOwn()
    {
        static SourceKey Key(Metric metric, string? disk, int seconds = 1) =>
            PerformanceData.KeyOf(metric, new PerformanceOptions { Disk = disk, Seconds = seconds });

        Assert.NotEqual(Key(Metric.Disk, null), Key(Metric.Disk, "Disk 1"));
        Assert.NotEqual(Key(Metric.Disk, "Disk 0"), Key(Metric.Disk, "Disk 1"));
        Assert.Equal(Key(Metric.Disk, "Disk 1"), Key(Metric.Disk, "disk 1"));
        Assert.Equal(Key(Metric.Disk, null), Key(Metric.Disk, "no such disk"));
        // The pace is the graph's, not the source's.
        Assert.Equal(Key(Metric.Disk, "Disk 1"), Key(Metric.Disk, "Disk 1", seconds: 5));
        Assert.Equal(Key(Metric.Cpu, null), Key(Metric.Cpu, "Disk 1"));
        Assert.Equal(Key(Metric.Gpu, null), Key(Metric.Gpu, "Disk 1"));

        Assert.Equal(1, new PerformanceData(Metric.Disk, "Disk 1", counting: false).Disk);
        Assert.Null(new PerformanceData(Metric.Disk, "", counting: false).Disk);
        Assert.Null(new PerformanceData(Metric.Cpu, "Disk 1", counting: false).Disk);
    }

    /// <summary>
    /// Every disk followed is gathered from the dock's start, in a folder nobody opened as on the
    /// bar: after N ticks each has N samples of its own disk — the busy one's and the quiet one's
    /// apart — and the line that follows one disk draws that disk's history.
    /// </summary>
    [Fact]
    public void EachDiskHasAHistoryOfItsOwnFromTheStart()
    {
        var sources = new DataSources((key, widget) =>
        {
            var options = widget.Performance ?? new PerformanceOptions();
            var metric = widget.Info!.Metric!.Value;
            var device = PerformanceData.DeviceOf(metric, options);
            // Disk 1 is busy, every disk together is quiet: two series a test can tell apart.
            return new PerformanceData(metric, device) { Reading = () => device == "Disk 1" ? 0.9f : 0.2f };
        });
        // The disk preset's face on the bar, following every disk, and a line following Disk 1.
        var bar = BarKind.FolderList.New();
        bar.Id = "all";
        bar.Folder!.Face = FolderFace.Of(FolderFaces.DiskId);
        var line = ListKind.Disk.New();
        line.Id = "line";
        line.Performance!.Disk = "Disk 1";
        var list = BarKind.FolderList.New();
        list.Id = "l";
        list.Folder!.Put(line);
        var config = new Config { Layouts = [new TaskbarLayout { Id = "main", Name = "main", Islands = [bar, list] }], DefaultLayoutId = "main" };

        sources.Want(config);
        const int Ticks = 7;
        for (var tick = 0; tick < Ticks; tick++)
        {
            sources.Tick(1_000 * (tick + 1));
        }

        var all = Assert.IsType<PerformanceData>(sources.Find(PerformanceData.KeyOf(Metric.Disk, new PerformanceOptions())));
        var one = Assert.IsType<PerformanceData>(sources.Find(PerformanceData.KeyOf(Metric.Disk, new PerformanceOptions { Disk = "Disk 1" })));
        Assert.Equal(2, sources.Running.Count);
        Assert.Equal(Ticks, all.History.Count);
        Assert.Equal(Ticks, one.History.Count);
        Assert.Equal(0.2f, all.History.Ago(0));
        Assert.Equal(0.9f, one.History.Ago(0));
        Assert.Same(one, sources.Hold(line));
        Assert.Same(all, sources.Hold(SourceKinds.FaceLine(bar.Folder.Face)!));
    }

    // --- A new bar -----------------------------------------------------------------------------

    /// <summary>
    /// A new bar is Windows 11's taskbar, which has no disk graph and no drive (2026-10-05): neither
    /// is on it, and both stay in the add list for whoever wants them — the Disk preset following
    /// every disk, its line a graph, where one is added, and the Drive preset.
    /// </summary>
    [Fact]
    public void ANewBarHasNeitherTheDiskGraphNorTheDrive()
    {
        var bar = Sections.DefaultLayout(pins: [], inputs: new InputHardware(Pen: false, Touch: false, Slate: false));

        Assert.DoesNotContain(bar, island => island.Folder?.Face.Kind is FolderFaces.DriveId or FolderFaces.NetworkDriveId);
        Assert.DoesNotContain(bar, island => island.Folder?.Face.Kind == FolderFaces.DiskId);
        Assert.NotNull(Presets.Find(Presets.DiskId));
        Assert.NotNull(Presets.Find(Presets.DriveId));
        var disk = ListKind.Disk.New();
        Assert.Null(disk.Performance!.Disk);
        Assert.True(disk.Performance.ShowGraph);
    }

    // --- The tooltip ---------------------------------------------------------------------------

    /// <summary>
    /// The tooltip names the disk, says its active time and its read and write, and how full each
    /// volume on it is — the space the drive used to show — leaving out a volume that will not say.
    /// </summary>
    [Fact]
    public void TheTooltipSaysHowFullEachVolumeIs()
    {
        const ulong Gb = 1UL << 30;
        var text = PerformanceIsland.DiskLines(
            "Disk 0 (C: D:)", 0.42f, 1_200_000, 310_000,
            [("C:", (419 * Gb, 931 * Gb)), ("D:", null), ("E:", (0UL, 0UL))]);
        var lines = text.Split('\n');

        Assert.Equal(3, lines.Length);
        Assert.Equal("Disk 0 (C: D:) 42%", lines[0]);
        Assert.StartsWith("Read ", lines[1], StringComparison.Ordinal);
        Assert.Contains(" · Write ", lines[1], StringComparison.Ordinal);
        Assert.Equal($"C: {Units.Size(419 * Gb)} free of {Units.Size(931 * Gb)}", lines[2]);

        // Not read yet: the name alone, and the space all the same.
        Assert.Equal($"Disk\nC: {Units.Size(419 * Gb)} free of {Units.Size(931 * Gb)}",
            PerformanceIsland.DiskLines("Disk", -1, -1, -1, [("C:", (419 * Gb, 931 * Gb))]));
    }

    // --- The settings card ---------------------------------------------------------------------

    /// <summary>
    /// The card's list: every disk together, then each disk as Task Manager labels it, then a disk
    /// the widget follows that is not attached now, kept; the choice is matched and stored by number.
    /// </summary>
    [Fact]
    public void TheCardListsEveryDiskAndKeepsOneThatIsGone()
    {
        string[] attached = ["Disk 0 (C:)", "Disk 1 (E: F:)"];

        var choices = PerformanceChoices.DiskChoices(attached, null);
        Assert.Equal([PerformanceChoices.AllDisks, "Disk 0 (C:)", "Disk 1 (E: F:)"], choices);
        Assert.Equal(0, PerformanceChoices.ChosenDiskIndex(choices, null));
        Assert.Equal(2, PerformanceChoices.ChosenDiskIndex(choices, "Disk 1"));
        Assert.Null(PerformanceChoices.PickedDisk(choices, 0));
        Assert.Equal("Disk 1", PerformanceChoices.PickedDisk(choices, 2));
        Assert.Null(PerformanceChoices.PickedDisk(choices, 9));

        var gone = PerformanceChoices.DiskChoices(attached, "Disk 3");
        Assert.Equal([PerformanceChoices.AllDisks, "Disk 0 (C:)", "Disk 1 (E: F:)", "Disk 3"], gone);
        Assert.Equal(3, PerformanceChoices.ChosenDiskIndex(gone, "Disk 3"));
        Assert.Equal([PerformanceChoices.AllDisks], PerformanceChoices.DiskChoices([], null));
    }

    // --- The widget ----------------------------------------------------------------------------

    /// <summary>
    /// A disk graph following one disk reads that disk's source and says which disk it is; one
    /// following every disk is "Disk", as before. Its tooltip begins with the disk.
    /// </summary>
    [UiFact]
    public void AGraphFollowingOneDiskSaysWhichDisk()
    {
        Offscreen.OnStaThread(gfx =>
        {
            using var one = Graph(gfx, "Disk 1");
            using var all = Graph(gfx, null);

            Assert.Equal(1, one.Disk);
            Assert.Equal(1, one.Source.Disk);
            Assert.Equal("Disk 1", one.Title);
            Assert.Null(all.Disk);
            Assert.Equal("Disk", all.Title);
            Assert.NotEqual(one.Source.Key, all.Source.Key);
            Assert.StartsWith("Disk 1", one.Tooltip(0), StringComparison.Ordinal);
            Assert.Contains("42%", one.Tooltip(0), StringComparison.Ordinal);
        });
    }

    // --- Pictures ------------------------------------------------------------------------------

    private static readonly int[] BarHeights = [28, 41, 72];

    private static readonly int[] LineRows = [1, 2, 3, 4, 5, 6];

    /// <summary>
    /// The disk following Disk 1, in every form: its preset's face on the bar at three heights and its
    /// line one to six rows tall — at 100 and 150 %, dark and light, to disk-activity-{scale}-{theme}.png.
    /// Each drew something and nothing past its box; the lines say "Disk 1" where they name it at
    /// all, with this PC's letters on it where a tall line names it as Task Manager does
    /// ("Disk 1 (D:)", PerformanceModel's DiskTitle), and its active time.
    /// </summary>
    [UiFact]
    public void TheDiskGraphInEveryForm()
    {
        var wrong = new List<string>();
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var scale in new[] { 1f, 1.5f })
            {
                foreach (var dark in new[] { true, false })
                {
                    wrong.AddRange(Sheet(gfx, scale, dark, $"disk-activity-{scale * 100:0}-{(dark ? "dark" : "light")}.png"));
                }
            }
        });
        Assert.Empty(wrong);
    }

    /// <summary>One sheet: the bar and the lines; what went wrong.</summary>
    private static List<string> Sheet(Gfx gfx, float scale, bool dark, string file)
    {
        var look = TestLooks.LookFor(dark);
        var theme = FlyoutTheme.For(look);
        var wrong = new List<string>();
        using var entry = Face(gfx, "Disk 1");

        using var line = Graph(gfx, "Disk 1");
        var pad = PopupPaint.Round(12 * scale);
        var gap = PopupPaint.Round(8 * scale);
        var dpi = (uint)MathF.Round(96 * scale);
        var bars = BarHeights.Select(height =>
        {
            var config = new Config { Height = height };
            return new IslandStyle(scale, false, Layout.CellPx(dpi, config), Layout.IconPx(dpi, config), look);
        }).ToList();
        var barWidth = bars.Sum(style => entry.CellSize(0, style).Width + gap);
        var lineStyle = FolderFlyout.StyleFor(scale, look, theme);
        var lineWidth = FolderFlyout.ListWidth(scale);
        var lineHeight = ListFormPictures.Rows(LineRows.Max(), scale, 1f);
        var width = pad + MathF.Max(barWidth, LineRows.Length * (lineWidth + gap)) + pad;
        var height = pad + bars.Max(style => style.Cell) + gap + lineHeight + pad;

        Offscreen.Bitmap(width, height, dc =>
        {
            PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, width, height), 0, PopupPaint.Ink(theme.Base));
            var x = pad;
            var y = pad;
            foreach (var style in bars)
            {
                var (cellWidth, cellHeight) = entry.CellSize(0, style);
                PopupPaint.Fill(dc, PopupPaint.Rect(x, y, cellWidth, cellHeight), GraphBackdrop.BarRadius(style), PopupPaint.Ink(theme.CardBackground));
                var shown = style;
                Offscreen.Shifted(dc, x, y, () => entry.PaintCell(dc, 0, shown, cellWidth, cellHeight));
                x += cellWidth + gap;
            }
            y += bars.Max(style => style.Cell) + gap;
            x = pad;
            foreach (var rows in LineRows)
            {
                var tall = ListFormPictures.Rows(rows, scale, 1f);
                PopupPaint.Fill(dc, PopupPaint.Rect(x, y, lineWidth, tall), PopupPaint.Round(4 * scale), PopupPaint.Ink(theme.CardBackground));
                Offscreen.Shifted(dc, x, y, () => line.PaintOn(dc, PopupPaint.Rect(0, 0, lineWidth, tall), lineStyle, WidgetSurface.List));
                x += lineWidth + gap;
            }
        }, bitmap => Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, file)));

        // The face alone at each height: it drew, and inside its cell.
        foreach (var style in bars)
        {
            var (cellWidth, cellHeight) = entry.CellSize(0, style);
            var shown = style;
            Inside(wrong, $"the face on a {style.Cell} px bar at {scale}", cellWidth, cellHeight, pad,
                dc => entry.PaintCell(dc, 0, shown, cellWidth, cellHeight));
        }

        // Each line alone, and what it says.
        foreach (var rows in LineRows)
        {
            var tall = ListFormPictures.Rows(rows, scale, 1f);
            var box = PopupPaint.Rect(0, 0, lineWidth, tall);
            Inside(wrong, $"a line {rows} rows tall at {scale}", lineWidth, tall, pad,
                dc => line.PaintOn(dc, box, lineStyle, WidgetSurface.List));
            var said = Said(line, box, lineStyle, WidgetSurface.List);
            if (!said.Contains("42%"))
            {
                wrong.Add($"a line {rows} rows tall at {scale} lost its active time: {string.Join(" | ", said)}");
            }
            // The letters are the one thing here that is this PC's: the disk's name is read off it.
            var named = PerformanceOptions.DiskLabel(1, SharedSamplers.DiskState.LettersOf(1));
            if (said.Any(text => text.StartsWith("Disk", StringComparison.Ordinal)) && !said.Contains("Disk 1") && !said.Contains(named) && !said.Contains("Disk"))
            {
                wrong.Add($"a line {rows} rows tall at {scale} names another disk: {string.Join(" | ", said)}");
            }
        }
        return wrong;
    }

    /// <summary>Draws <paramref name="paint"/> alone with a margin round it, and says so where it drew nothing or past its box.</summary>
    private static void Inside(List<string> wrong, string what, float width, float height, float margin, Offscreen.Painter paint)
    {
        var inked = default((float Left, float Top, float Right, float Bottom));
        Offscreen.Bitmap(
            width + (2 * margin),
            height + (2 * margin),
            dc => Offscreen.Shifted(dc, margin, margin, () => paint(dc)),
            bitmap => inked = Offscreen.InkOf(bitmap, (uint)MathF.Ceiling(width + (2 * margin)), (uint)MathF.Ceiling(height + (2 * margin))));
        if (inked.Right <= inked.Left)
        {
            wrong.Add($"{what} drew nothing at all");
        }
        else if (inked.Left < margin - 1 || inked.Top < margin - 1 || inked.Right > margin + width + 1 || inked.Bottom > margin + height + 1)
        {
            wrong.Add($"{what} drew past its box");
        }
    }

    /// <summary>The words <paramref name="island"/> says drawn into <paramref name="box"/>.</summary>
    private static List<string> Said(CellIsland island, D2D_RECT_F box, TileStyle style, WidgetSurface surface)
    {
        var said = new List<string>();
        PaintWatch.Said = (text, _) => said.Add(text);
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

    /// <summary>
    /// A disk graph's line following <paramref name="disk"/> (null for every disk), with a history of
    /// active time fed to it and its last reading 42 % with its read and write.
    /// </summary>
    private static PerformanceIsland Graph(Gfx gfx, string? disk)
    {
        var id = $"disk-list-{disk?.Replace(' ', '-').ToLowerInvariant() ?? "all"}";
        var line = ListFormPictures.Line(ListKind.Disk, item => item.Performance!.Disk = disk);
        line.Id = id;
        var made = Karpo.Dock.Islands.CreateLine(Config.IslandKey(null, id), line, ListKind.Disk, gfx, new Config());
        var island = made as PerformanceIsland ?? throw new Xunit.Sdk.XunitException("no disk graph was made");
        Fed(gfx, island);
        return island;
    }

    /// <summary>
    /// The disk preset's folder set to follow <paramref name="disk"/>, its line and its face alike,
    /// the line fed as <see cref="Graph"/>'s: the face shows what the line reads.
    /// </summary>
    private static FolderIsland Face(Gfx gfx, string disk)
    {
        var holder = Presets.Folder(Presets.Find(Presets.DiskId)!, new PresetMachine(HasBattery: false, FixedDrives: []));
        holder.Folder!.Cells[0].Island.Performance!.Disk = disk;
        holder.Folder.Face.Set(FolderFaces.Disk.Option(PerformanceFace.Disk)!, disk);
        var folder = new FolderIsland(Config.IslandKey(null, holder.Id), holder, gfx, new Config());
        var line = (PerformanceIsland)folder.Tiles[0].Island;
        Fed(gfx, line);
        Assert.Same(line.Source, folder.Performance);
        return folder;
    }

    /// <summary>Bursts of activity over a quiet floor fed to <paramref name="island"/>, then 42 % with its read and write.</summary>
    private static void Fed(Gfx gfx, PerformanceIsland island)
    {
        _ = island.Refresh(gfx);
        island.Forgotten();
        for (var step = 0; step < 300; step++)
        {
            // Bursts of activity over a quiet floor, the way a disk is busy: a flat line would hide
            // the stroke's joins and the fill.
            var wave = (MathF.Sin(step * 0.09f) + 1) / 2;
            var share = Math.Clamp(0.05f + (0.8f * wave * wave * wave), 0, 1);
            island.Feed(share, read: share * 40_000_000, written: share * 3_000_000);
        }
        island.Feed(0.42f, read: 1_200_000, written: 310_000);
    }
}
