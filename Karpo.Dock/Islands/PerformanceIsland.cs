// A live graph of how busy the machine is: one widget per reading — the
// processor, the graphics adapter, the memory, the busiest disk, the network.
//
// **One kind per reading.** This was one widget of four cells with a checkbox
// per metric until commit dbc9e2f took it out. A kind is the unit everything is
// built on now — a row in the catalogue, a shape, a card in a folder — and a
// widget whose contents were checkboxes could have none of those. Five kinds, one class: they
// are one drawing, and only the sampler behind them differs (Samplers\).
//
// **The reading and its history are the source's** (Sources\PerformanceData.cs): one per
// reading, read every second from the dock's start whatever graphs the config names — on a bar,
// in a folder nobody has opened, in a layout no display shows — so a graph drawn for the first
// time draws the history that is already there, and sixteen graphs of the processor are one
// reading of it and one history. The island is the drawing: it draws what the source holds, a
// step every pace it is set to (Series: a graph at five seconds draws five samples to a step).
// The counters under the sources are SharedSamplers', one read of each per pass; the last
// performance source to go releases the disk handles and the adapter list, so a config with no
// graph in it holds nothing at all.
//
// **The graph scrolls by being redrawn, not by being animated.** The old island
// drew the graph into a layer a step wider than its box and had DWM glide that
// layer left over the sampling interval, so the picture moved smoothly between
// samples. That is a visible state — where the graph is — living in a
// composition animation, and the rule is that no visible state may. So the two
// layers, the property set, the key frames and the deck that held them are gone:
// a sample is taken, the line is redrawn one step further along, and at a second
// a sample that is a step people read as movement. What was lost is the glide
// between samples, and the interval is the default it is (one second, Task
// Manager's own) partly to pay for it.
//
// The disk graph is the disk's active time, the graph Task Manager draws for a disk, with its read
// and write as the second reading — never a drive's storage space, which is the Drive widget's. It
// follows one disk ("Disk 1", PerformanceOptions.Disk) or all of them together, and where it
// follows one its name says which (Title). How full each volume on it is is in its tooltip.
//
// It is a line of a list folder (ListForm) at one of two sizes, each saying the same (§603): Compact the
// kind's glyph in the list's leading column, its name where the list puts a name, the reading at the
// line's end and its second reading under the name, all on the list's one gutter, over the graph a
// band edge to edge (docs\widgets.md §9, "One gutter", and §24); Full the top of what the reading's
// flyout was (§97): the figure large and the last minute as a graph with what its top stands for.
// Nothing more where its line is taller. The facts behind the figure are a widget of their
// own (2026-10-11: "the graphs and details should be separate widgets"), the reading's details, drawn
// by this class too (WidgetInfo.Facts): the flyout's grid of them as a line, which the reading's
// preset stands under the graph in one card. A click on either opens Task
// Manager: an app opening is the one thing a folder lets a click do. The widget never samples: the
// sources are gathered by the dock's one timer, and these five are always running
// (SourceKinds.Always).
//
// **On the taskbar it is a folder's face** (docs\widgets.md §97): the reading's preset wears the
// glyph and the figure the entry was, as wide as the figure it reads now (§244), and in the face's
// graph style (§295) the graph behind them, drawn as the line draws its own (Graphed). The face is
// drawn here (PaintFace) from the source the folder holds for it (FolderIsland.Performance), the
// entry and its flyout having gone.

using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using Karpo.Core;
using Karpo.Dock.Graphs;
using Karpo.Dock.Native;
using Karpo.Dock.Performance;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;

namespace Karpo.Dock;

internal sealed unsafe class PerformanceIsland : CellIsland, IListContent
{
    /// <summary>More samples than the widest graph a 160-DIP bar with large text shows.</summary>
    private const int Samples = PerformanceData.Samples;

    private readonly Metric metric;

    // perf: the reading's details — the facts behind its figure — rather than its graph (WidgetInfo.Facts).
    private readonly bool showsFacts;

    private readonly DataSources sources;

    private PerformanceOptions options = new();

    // What this graph draws: the newest reading and the history, which are its source's
    // (PerformanceData). A reading that is negative or null means "not known yet", which is
    // drawn as a dash in the secondary ink rather than as a zero: a machine that is idle and a
    // machine that has not answered are not the same thing, and a graph drawn from an invented
    // zero is a lie.
    private PerformanceData? source;

    // Whether the source is a stand-in of this graph's own, fed by a test or a picture (Feed)
    // rather than read of the machine, and so shared with nobody.
    private bool fed;

    public PerformanceIsland(string key, WidgetItem island, Gfx gfx, Config config)
        : this(key, island, gfx, config, DataSources.Shared)
    {
    }

    /// <summary>A graph reading its source from <paramref name="sources"/>: the dock's own, or a test's.</summary>
    internal PerformanceIsland(string key, WidgetItem island, Gfx gfx, Config config, DataSources sources)
        : base(key, island, gfx)
    {
        metric = Info.Metric
            ?? throw new ArgumentOutOfRangeException(nameof(island), Info.Id, "is not a performance graph.");
        showsFacts = Info.Facts;
        this.sources = sources;
        Configure(island, config);
    }

    /// <summary>The source this graph draws: every graph of its reading and device draws the same one, at any pace.</summary>
    internal PerformanceData Source => source!;

    /// <summary>list: the graph runs under a folder's rim to the line's edges; the details draw none.</summary>
    protected internal override bool UnderRim => !showsFacts;

    /// <summary>
    /// What the graph draws: the source's history a step every pace this graph is set to. A
    /// stand-in's (<see cref="Feed"/>) is fed a step at a time, so it is drawn as it was fed.
    /// </summary>
    internal Series Series => Source.Paced(fed ? 1 : options.Seconds);

    public override int IntervalMs => options.Seconds * 1000;

    protected override int Count => 1;

    /// <summary>
    /// The Segoe Fluent Icons glyph a reading is drawn with — its graph's own, from the registry
    /// (<see cref="WidgetInfos.For(Metric)"/>), so the cell and the catalogue row cannot show two
    /// different pictures of one widget.
    /// </summary>
    internal static string GlyphOf(Metric metric) => WidgetInfos.For(metric).Glyph;

    /// <summary>
    /// The <b>worst case</b> a reading of <paramref name="metric"/> can ever be, which is the room it
    /// keeps. A percentage is measured as a full hundred and a rate as the widest shape
    /// <see cref="Units.ShortRate"/> produces, so "9K" and "120M" are one width and a network going
    /// from a trickle to a torrent moves nothing (<see cref="Units.RateShape"/>).
    /// <para>
    /// Static, and given the reading rather than the island, because it is what
    /// <see cref="WidgetShapes"/> measures and a shape may not reach anything live.
    /// </para>
    /// </summary>
    internal static string Room(Metric metric) =>
        metric == Metric.Network ? Units.RateShape : Units.PercentRoom;

    public override void Configure(WidgetItem island, Config config)
    {
        // The shape is a function of the settings, so the shape must be given the settings just
        // handed over: keeping the ones this island was born with leaves the size measured for the
        // old options while the drawing uses the new ones (docs\widgets.md §4).
        Settings = island;
        var next = island.Performance ?? new PerformanceOptions();
        options = new PerformanceOptions
        {
            ShowGraph = next.ShowGraph,
            Seconds = PerformanceChoices.Nearest(next.Seconds),
            GpuAdapter = next.GpuAdapter ?? "",
            Disk = next.Disk,
        };
        // Another pace is the same source drawn a step every so many samples (Series); another
        // adapter or disk is a different machine, and another source with a history of its own.
        var key = PerformanceData.KeyOf(metric, options);
        if (source is null || source.Key != key)
        {
            var was = source;
            var held = fed ? null : sources.Hold(key, island);
            if (held is PerformanceData shared)
            {
                source = shared;
            }
            else
            {
                sources.Release(held);
                source = StandIn();
            }
            if (!fed)
            {
                sources.Release(was);
            }
        }
        Forget();
    }

    /// <summary>A source of this graph's own, read of nothing: for readings handed to it (Feed), or where no source could be made.</summary>
    private PerformanceData StandIn() => new(metric, PerformanceData.DeviceOf(metric, options), counting: false);

    /// <summary>
    /// The second thing worth knowing, which the line's one row has no room for: which adapter,
    /// how many gigabytes, up against down; the disk's volumes. What a test and a screen reader read,
    /// and the reading's face too (<see cref="Tip"/>).
    /// </summary>
    public override string? Tooltip(int index) => Tip(Source);

    /// <summary>
    /// What <paramref name="source"/> says beyond its figure: the processor's share, the graphics
    /// adapters and their loads, the memory in use, the disk's card (<see cref="DiskTooltip"/>), down
    /// and up. The line's tooltip and its face's, for a screen reader (none over the bar, §367).
    /// </summary>
    internal static string Tip(PerformanceData source)
    {
        switch (source.Metric)
        {
            case Metric.Cpu:
                return source.Share < 0 ? S.WidgetPerformanceProcessor : S.WidgetPerformanceProcessorShare(Units.Percent(source.Share));
            case Metric.Gpu:
            {
                var gpu = SharedSamplers.GpuState;
                if (SharedSamplers.GpuFailed)
                {
                    return S.WidgetPerformanceGraphicsUnavailable;
                }
                // One adapter, the one it follows (PerformanceData.Chosen), never a list of them all.
                var text = source.Share < 0 ? S.WidgetPerformanceGraphics : S.WidgetPerformanceGraphicsShare(Units.Percent(source.Share));
                return gpu.Followed(source.Adapter, AdapterName) is { } index
                    ? $"{text}\n{gpu.Adapters[index].Name}"
                    : source.Adapter.Length > 0 ? $"{text}\n{source.Adapter}" : text;
            }
            case Metric.Memory:
                return source.Memory is { } reading
                    ? S.WidgetPerformanceMemoryShare(load: (int)reading.Load, amount: Units.Of(reading.Used, reading.Total))
                    : S.WidgetPerformanceMemory;
            case Metric.Disk:
                return DiskTooltip(source);
            default:
                return source.Down < 0
                    ? S.WidgetPerformanceNetwork
                    : $"{S.WidgetPerformanceNetwork}\n{S.WidgetPerformanceDownUp(down: Units.Rate(source.Down), up: Units.Rate(source.Up))}";
        }
    }

    /// <summary>The disk a disk graph follows, by number; null for every disk together, and for the other kinds.</summary>
    internal int? Disk => metric == Metric.Disk ? PerformanceOptions.DiskNumber(options.Disk) : null;

    /// <summary>
    /// A disk graph's card: which disk, as Task Manager names it ("Disk 0 (C:)"), its active time,
    /// what it reads and writes, and then how full each volume on it is — the storage space a new
    /// bar's drive widget used to show (Sections.DefaultLayout), a hover away on the graph that
    /// took its place, as Task Manager puts a disk's capacity under its graph.
    /// </summary>
    private static string DiskTooltip(PerformanceData source)
    {
        var sampler = SharedSamplers.DiskState;
        if (sampler.Failed)
        {
            return S.WidgetPerformanceDiskUnavailable;
        }
        string title;
        string[] letters;
        if (source.Disk is { } number)
        {
            letters = sampler.LettersOf(number);
            title = PerformanceOptions.DiskLabel(number, letters);
        }
        else
        {
            letters = [.. sampler.Named.SelectMany(disk => disk.Letters)];
            title = S.WidgetPerformanceDisk;
        }
        return DiskLines(title, source.Share, source.DiskRead, source.DiskWritten, letters.Select(letter => (letter, Space(letter))));
    }

    /// <summary>
    /// The disk card's words, from what is known: the title alone until a reading has been taken,
    /// then the active time beside it and the read and write under it, and a line for each volume
    /// that says how full it is ("C: 419 GB free of 931 GB"); a volume that will not say is left out.
    /// </summary>
    internal static string DiskLines(string title, float share, double read, double written, IEnumerable<(string Letter, (ulong Free, ulong Total)? Space)> volumes)
    {
        var text = share < 0
            ? title
            : $"{title} {Units.Percent(share)}\n{S.WidgetPerformanceReadWrite(read: Units.Rate(read), write: Units.Rate(written))}";
        foreach (var (letter, space) in volumes)
        {
            if (space is { Total: > 0 } known)
            {
                text += $"\n{S.WidgetPerformanceVolumeFree(letter: letter, free: Units.Size(Math.Min(known.Free, known.Total)), total: Units.Size(known.Total))}";
            }
        }
        return text;
    }

    /// <summary>
    /// How much is free on the volume at <paramref name="letter"/> ("C:") and how large it is; null
    /// where it will not say (a card reader with no card). Asked on a hover, never on a tick. The
    /// tests' way to a volume of their own, as <c>DriveWatch.Source</c> is the drive widget's.
    /// </summary>
    internal static Func<string, (ulong Free, ulong Total)?> Space { get; set; } = letter =>
    {
        try
        {
            var drive = new DriveInfo(letter);
            return drive.IsReady ? ((ulong)drive.AvailableFreeSpace, (ulong)drive.TotalSize) : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    };

    /// <summary>
    /// Task Manager: an app opening is the one thing a folder lets a click do, and Task Manager is
    /// where everything else about the reading lives.
    /// </summary>
    public override IslandAction Click(int index)
    {
        Launch.Open(SettingsActions.TaskManagerTarget);
        return default;
    }

    public override void Dispose()
    {
        if (!fed)
        {
            sources.Release(source);
        }
        base.Dispose();
    }

    protected override bool Sample(bool everything)
    {
        if (fed)
        {
            return false;
        }
        if (everything)
        {
            // A device change or a resume: the source measures from here.
            Source.Refresh(Environment.TickCount64);
        }
        // Nothing is read here. The source is gathered by the dock's one timer, which ticks the
        // sources before the islands, so a tick draws what was just read; a widget never samples
        // on its own (docs\widgets.md, "The performance sources").
        return false;
    }

    /// <summary>
    /// How an adapter is named in the settings and matched back: "GPU 0: Intel UHD 770", as Task
    /// Manager names them. Two adapters of one model are told apart by the number, which is why the
    /// name alone would not do.
    /// </summary>
    internal static string AdapterName(int index, string name) => $"GPU {index}: {name}"; // not-ui: matched back in the settings

    /// <summary>Every adapter this PC has, for the settings page's list; empty until one is read.</summary>
    internal static IReadOnlyList<string> Adapters()
    {
        var gpu = SharedSamplers.GpuState;
        var names = new List<string>(gpu.Adapters.Count);
        for (var index = 0; index < gpu.Adapters.Count; index++)
        {
            names.Add(AdapterName(index, gpu.Adapters[index].Name));
        }
        return names;
    }

    /// <summary>A reading is a line of a list on the bar's side of things: the bar shows its face (<see cref="PaintFace"/>).</summary>
    internal override void PaintCell(ID2D1DeviceContext* dc, int index, in IslandStyle style, float width, float height) =>
        throw new NotSupportedException($"{Info.Id} is a list's line; on the bar it is its preset's face.");

    // --- In a folder ---------------------------------------------------------------------------

    protected internal override void PaintCard(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, WidgetSurface surface) =>
        Line(dc, box, style);

    /// <summary>The graph a list's line draws, in <paramref name="graph"/>, clipped to it.</summary>
    private void Graph(ID2D1DeviceContext* dc, D2D_RECT_F graph, in TileStyle style) =>
        Graphed(dc, metric, Series, TilePaint.Accent(style), graph, MathF.Max(1, style.Px(2)), MathF.Max(1, style.Px(1.5f)));

    // --- A line of a list ----------------------------------------------------------------------
    //
    // §603 (the person, 2026-11-09: "split all of them"): the graph's line says the same at both of its sizes — the
    // reading's name, its figure and its second reading — and nothing more where the line is taller (no tiers):
    //
    //   Compact  the glyph in the list's leading column, the name, the figure at the trailing gutter, the second reading
    //            (the memory in use, down and up, read and write, the busiest moment) under the name, and the graph a
    //            band under them, edge to edge (§401)
    //   Full     the top of what the reading's flyout was (§97): the name over the figure large, what the graph's top
    //            stands for at the figure's row's other end, the second reading under the figure, and the last minute
    //            edge to edge on its quarters' hairlines with how far back it reaches and "0" under it
    //
    // The figure alone (the Numbers look, ShowGraph off) is each size without its graph: no band at Compact; at Full no
    // minute, no scale, no span. A graph is the one thing that crosses the gutters: it runs the line's whole width,
    // with nothing behind it but its own fill (docs\widgets.md §9, "One gutter", and §24).

    /// <summary>
    /// The least the graph's band under a Compact line's words is, in DIPs: a graph, not a line of text. It runs to the
    /// line's foot, so the form fills its whole cells (§442) at any text size.
    /// </summary>
    private const float BandDips = 28;

    /// <summary>The least the minute's graph at Full is, in DIP; it stretches to fill the line's whole cells.</summary>
    private const float MinuteGraphDips = 56;

    /// <summary>The figure at Full, in DIP: the flyout's, larger than a line's reading.</summary>
    private const float FigureDips = 28;

    /// <summary>
    /// list: its size's form, whatever the reading (§603): a graph's Compact its row, its second reading and its band,
    /// its Full the name, the figure large, the second reading and the minute with its captions; the details', their
    /// first fact on a row (Compact) or every fact (Full, <see cref="FactsHeight"/>).
    /// </summary>
    public float ListHeight(float width, in TileStyle style)
    {
        var form = ListForm.Of(Gfx, style);
        if (showsFacts)
        {
            // §432: the first fact on a row (Compact; at a large text, its name over its figure where they do not
            // stand on one line), or every fact (Full).
            if (LineSizes.Of(Settings) != LineSize.Compact)
            {
                return FactsHeight(width, style, form);
            }
            var listed = FactsListed(PerformanceModel.For(Source, FiguresNow()), width - form.Inset - form.TextLeft, style, form);
            return listed.Length == 0 || listed[0] ? LineRows.Px(1, style) : MathF.Max(LineRows.Px(1, style), (2 * form.Pad) + form.SmallLine + form.Line);
        }
        var graphs = options.ShowGraph;
        if (LineSizes.Of(Settings) == LineSize.Compact)
        {
            return form.Pad + CompactRow(style, form) + form.SmallLine + (graphs ? form.Gap + style.Px(BandDips) : 0) + form.Pad;
        }
        return form.Pad + form.Line + TilePaint.LineHeight(Gfx, style.Text(FigureDips), Weight.Semibold) + form.SmallLine
            + (graphs ? style.Px(6) + style.Px(MinuteGraphDips) + style.Px(4) + form.SmallLine + Foot(style, form) : form.Pad);
    }

    /// <summary>
    /// Under the minute's captions at Full: the line's pad less what the captions' own line keeps under their letters,
    /// so there is no more air under the form than over it (§442).
    /// </summary>
    private static float Foot(in TileStyle style, in ListForm form) => MathF.Max(0, form.Pad - style.Text(6));

    /// <summary>A Compact line's row: the name's line, or the figure's where that is taller.</summary>
    private float CompactRow(in TileStyle style, in ListForm form) =>
        MathF.Max(TilePaint.LineHeight(Gfx, ListTiers.ValuePx(style), Weight.Semibold), form.Line);

    /// <summary>The line at its size (see above); the details' grid for the details.</summary>
    private void Line(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style)
    {
        var form = ListForm.Of(Gfx, style);
        if (showsFacts)
        {
            LineDetails(dc, box, style, form);
        }
        else if (LineSizes.Of(Settings) == LineSize.Compact)
        {
            LineCompact(dc, box, style, form);
        }
        else
        {
            LineFull(dc, box, style, form);
        }
    }

    /// <summary>
    /// The Compact line: the glyph, the name and the figure on a row, the second reading under the name, and the graph
    /// a band under them, edge to edge, never behind the words.
    /// </summary>
    private void LineCompact(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, in ListForm form)
    {
        var theme = style.Theme;
        var valuePx = ListTiers.ValuePx(style);
        var valueLine = TilePaint.LineHeight(Gfx, valuePx, Weight.Semibold);
        var row = CompactRow(style, form);
        var (text, known) = Reading();
        var ink = known ? theme.Text : theme.TextSecondary;
        var left = box.left + form.TextLeft;
        var right = box.right - form.Inset;
        var top = box.top + form.Pad;
        var figure = MathF.Min(PopupPaint.Measure(Gfx, Room(metric), valuePx, Weight.Semibold).X, right - left);
        var nameRoom = MathF.Max(1, right - figure - style.Gap - left);
        var y = top + row;
        if (options.ShowGraph)
        {
            Backdrop(dc, box with { top = MathF.Min(y + form.SmallLine + form.Gap, box.bottom) }, style, down: true, []);
        }
        PopupPaint.Glyph(dc, Gfx, GlyphOf(metric), ListTiers.GlyphPx(form, style, ListTier.Compact), theme.Text, form.IconBox(box.left, top, row));
        TilePaint.LineRight(dc, Gfx, text, valuePx, Weight.Semibold, ink, right, top + PopupPaint.Round((row - valueLine) / 2), figure);
        if (ListTiers.Name(Gfx, Names(), form.TextPx, Weight.Regular, nameRoom) is { } name)
        {
            TilePaint.Line(dc, Gfx, name, form.TextPx, Weight.Regular, theme.Text, left, top + PopupPaint.Round((row - form.Line) / 2), nameRoom);
        }
        Second(dc, style, form, left, right, y);
    }

    /// <summary>
    /// The second reading under the name or the figure (<see cref="CompactParts"/>), measured on the widest its figures
    /// can be, never on the figures showing, so it cannot come and go as a disk's read goes from nothing to a torrent:
    /// where even that does not fit across the line (a narrow folder at a large text), not at all.
    /// </summary>
    private void Second(ID2D1DeviceContext* dc, in TileStyle style, in ListForm form, float left, float right, float top)
    {
        var second = CompactParts();
        if (second.Count > 0 && TilePaint.FieldsWidth(Gfx, second, form.SmallPx, Weight.Regular) <= right - left)
        {
            TilePaint.Fields(dc, Gfx, second, form.SmallPx, Weight.Regular, style.Theme.TextSecondary, left, top);
        }
    }

    // --- The details: what the flyout was ----------------------------------------------------
    //
    // The reading's flyout (Task Manager's page for it in a panel, until 2026-10-10) is two widgets
    // now (2026-10-11), which its preset stands one over the other in one card (docs\widgets.md §97):
    // the graph's line at its Full size — the reading's name, the figure large with what the graph's
    // top stands for at the other end of its row, the last minute as a graph edge to edge on its
    // quarters' hairlines with how far back it reaches and "0" under it — and the details' line, the
    // facts behind the figure in two columns, a fact too long for one across both.

    /// <summary>Between the facts' two columns, in DIP, as the flyout had them.</summary>
    private const float FactsColumnGapDips = 16;

    /// <summary>A fact's place in the grid: its row and column, and whether it runs across both.</summary>
    private readonly record struct FactCell(int Row, int Column, bool Wide);

    /// <summary>
    /// Tests only, on the thread that makes the line: the figures beside the source's reading (what
    /// the system holds, its clock, the adapters' names), instead of this PC's.
    /// </summary>
    [ThreadStatic]
    internal static Func<PerformanceData, PerformanceFigures>? FiguresStand;

    private readonly Func<PerformanceData, PerformanceFigures> figures = FiguresStand ?? PerformanceFigures.Read;

    // The figures last read, and what the source had gathered then: they are read again once a
    // gather at most, and only for a line showing them.
    private (int Gathered, long Total, PerformanceFigures Figures)? read;

    /// <summary>The machine's figures beside the source's reading, read again where the source has gathered since.</summary>
    private PerformanceFigures FiguresNow()
    {
        var at = (Source.Gathered, Source.History.Total);
        if (read is { } kept && (kept.Gathered, kept.Total) == at)
        {
            return kept.Figures;
        }
        var now = figures(Source);
        read = (at.Gathered, at.Total, now);
        return now;
    }

    /// <summary>
    /// The facts of <paramref name="model"/> in two columns between <paramref name="left"/> and
    /// <paramref name="right"/>: each fact's place, a fact too long for its column (an adapter's
    /// name) across both on a row of its own; and how many rows they take.
    /// </summary>
    private (List<FactCell> Cells, int Rows) FactsGrid(PerformanceModel model, float left, float right, in TileStyle style, in ListForm form)
    {
        var gap = style.Px(FactsColumnGapDips);
        var column = MathF.Floor((right - left - gap) / 2);
        var cells = new List<FactCell>(model.Facts.Count);
        var (row, at) = (0, 0);
        foreach (var fact in model.Facts)
        {
            var wide = PopupPaint.Measure(Gfx, fact.Value, form.TextPx, Weight.Regular).X > column
                || PopupPaint.Measure(Gfx, fact.Label, form.SmallPx, Weight.Regular).X > column;
            if (wide && at == 1)
            {
                (row, at) = (row + 1, 0);
            }
            cells.Add(new FactCell(row, at, wide));
            (row, at) = wide || at == 1 ? (row + 1, 0) : (row, 1);
        }
        return (cells, at == 1 ? row + 1 : row);
    }

    /// <summary>A fact's row in the grid, in px: its name over its figure, and the gap under it.</summary>
    private static float FactRow(in TileStyle style, in ListForm form) => form.SmallLine + form.Line + style.Px(6);

    /// <summary>A fact on a row of its own (<see cref="FactsListed"/>), in px: its name and figure on one line, or the one over the other.</summary>
    private float FactListedRow(bool inline, in TileStyle style, in ListForm form) =>
        inline ? form.Line + style.Px(6) : FactRow(style, form);

    /// <summary>
    /// The facts of <paramref name="model"/> a row each, <paramref name="room"/> px wide: whether
    /// each one's name and figure share its line, the name leading and the figure trailing, as
    /// Settings' rows have them — or, where the two do not fit beside each other (an adapter's name),
    /// the name over the figure.
    /// </summary>
    private bool[] FactsListed(PerformanceModel model, float room, in TileStyle style, in ListForm form)
    {
        var gap = style.Px(FactsColumnGapDips);
        var inline = new bool[model.Facts.Count];
        for (var index = 0; index < inline.Length; index++)
        {
            var (label, figure) = model.Facts[index];
            inline[index] = PopupPaint.Measure(Gfx, label, form.TextPx, Weight.Regular).X + gap
                + PopupPaint.Measure(Gfx, figure, form.TextPx, Weight.Regular).X <= room;
        }
        return inline;
    }

    /// <summary>The grid's height for <paramref name="rows"/> rows, padding included; one line's worth for none.</summary>
    private static float GridHeight(int rows, in TileStyle style, in ListForm form) =>
        rows == 0
            ? form.Pad + form.Line + form.Pad
            : form.Pad + (rows * FactRow(style, form)) - style.Px(6) + form.Pad;

    /// <summary>The most a line of the list starts with, in px: the most cells a line has (FolderOptions.MostCells).</summary>
    private static float MostPx(in TileStyle style) => LineRows.Cells(FolderOptions.MostCells, style);

    /// <summary>
    /// How tall the details' line is at <paramref name="width"/> px: its grid of every fact with the
    /// list's padding over and under it; one line's worth while there are none yet. Where the grid
    /// would be taller than the most a line is given — at the largest text nearly every fact is too
    /// long for a column and takes a row of the grid alone, and the processor's last ones were left
    /// out — the facts a row each (<see cref="FactsListed"/>), which takes about half the height.
    /// </summary>
    private float FactsHeight(float width, in TileStyle style, in ListForm form)
    {
        var model = PerformanceModel.For(Source, FiguresNow());
        var (_, rows) = FactsGrid(model, form.TextLeft, width - form.Inset, style, form);
        var grid = GridHeight(rows, style, form);
        if (rows == 0 || grid <= MostPx(style) + 0.5f)
        {
            return grid;
        }
        var height = form.Pad - style.Px(6) + form.Pad;
        foreach (var inline in FactsListed(model, width - form.Inset - form.TextLeft, style, form))
        {
            height += FactListedRow(inline, style, form);
        }
        return height;
    }

    /// <summary>
    /// The details' line: the facts behind the reading's figure in two columns, each its name over
    /// its figure, from the line's top down — what the flyout had under its minute, and what the
    /// graph's line stands over in the reading's preset with no divider between them. Where the
    /// grid is taller than the line, a row each (<see cref="FactsHeight"/>). Rows past the line's
    /// foot are left out whole, never cut.
    /// </summary>
    private void LineDetails(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, in ListForm form)
    {
        var theme = style.Theme;
        var model = PerformanceModel.For(Source, FiguresNow());
        var left = box.left + form.TextLeft;
        var right = box.right - form.Inset;
        var (cells, rows) = FactsGrid(model, left, right, style, form);
        if (GridHeight(rows, style, form) > TilePaint.Height(box) + 0.5f)
        {
            var listed = FactsListed(model, right - left, style, form);
            var y = box.top + form.Pad;
            for (var index = 0; index < listed.Length; index++)
            {
                var (label, figure) = model.Facts[index];
                var inline = listed[index];
                if (y + (inline ? form.Line : form.SmallLine + form.Line) > box.bottom + 0.5f)
                {
                    break;
                }
                if (inline)
                {
                    var figureWidth = TilePaint.LineRight(dc, Gfx, figure, form.TextPx, Weight.Regular, theme.Text, right, y, right - left);
                    TilePaint.Line(dc, Gfx, label, form.TextPx, Weight.Regular, theme.TextSecondary, left, y, right - left - figureWidth);
                }
                else
                {
                    TilePaint.Line(dc, Gfx, label, form.SmallPx, Weight.Regular, theme.TextSecondary, left, y, right - left);
                    TilePaint.Line(dc, Gfx, figure, form.TextPx, Weight.Regular, theme.Text, left, y + form.SmallLine, right - left);
                }
                y += FactListedRow(inline, style, form);
            }
            return;
        }
        var gap = style.Px(FactsColumnGapDips);
        var column = MathF.Floor((right - left - gap) / 2);
        var cell = FactRow(style, form);
        for (var index = 0; index < cells.Count; index++)
        {
            var (label, figure) = model.Facts[index];
            var place = cells[index];
            var y = box.top + form.Pad + (place.Row * cell);
            if (y + form.SmallLine + form.Line > box.bottom + 0.5f)
            {
                break;
            }
            var x = left + (place.Column * (column + gap));
            var span = place.Wide ? right - left : column;
            TilePaint.Line(dc, Gfx, label, form.SmallPx, Weight.Regular, theme.TextSecondary, x, y, span);
            TilePaint.Line(dc, Gfx, figure, form.TextPx, Weight.Regular, theme.Text, x, y + form.SmallLine, span);
        }
    }

    /// <summary>
    /// The Full line: the flyout's name, the figure large with what the graph's top stands for at its row's end, the
    /// second reading under it, and the minute with how far back it reaches and "0"; the figure alone without the graph.
    /// </summary>
    private void LineFull(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, in ListForm form)
    {
        var theme = style.Theme;
        var left = box.left + form.TextLeft;
        var right = box.right - form.Inset;
        var top = box.top + form.Pad;
        var model = PerformanceModel.For(Source, FiguresNow());
        // The flyout's figure: the network's down and up together written out ("5.6 MB/s"), where
        // the Compact line says it short.
        var ink = model.Known ? theme.Text : theme.TextSecondary;
        var graphs = options.ShowGraph;

        // The name, with the glyph in the list's leading column.
        PopupPaint.Glyph(dc, Gfx, GlyphOf(metric), ListTiers.GlyphPx(form, style, ListTier.Full), theme.Text, form.IconBox(box.left, top, form.Line));
        if (ListTiers.Name(Gfx, [model.Title, .. Names()], form.TextPx, Weight.Semibold, right - left) is { } name)
        {
            TilePaint.Line(dc, Gfx, name, form.TextPx, Weight.Semibold, theme.Text, left, top, MathF.Max(1, right - left));
        }

        // The figure large, and what the graph's top stands for at the row's other end, on the
        // figure's foot, as Task Manager puts "100%" over its graph's right end.
        var series = Series;
        var full = metric == Metric.Network ? Graphs.Sparkline.Round(Graphs.Sparkline.Ceiling(series, PerformanceModel.Samples)) : 1f;
        var scale = metric == Metric.Network ? Units.Rate(full) : Units.Percent(1);
        var scaleWidth = graphs ? MathF.Ceiling(PopupPaint.Measure(Gfx, scale, form.SmallPx, Weight.Regular).X) : 0;
        var figurePx = style.Text(FigureDips);
        var figureTop = top + form.Line;
        var figureLine = TilePaint.LineHeight(Gfx, figurePx, Weight.Semibold);
        TilePaint.Line(dc, Gfx, model.Value, figurePx, Weight.Semibold, ink, left, figureTop, MathF.Max(1, right - left - (graphs ? scaleWidth + style.Gap : 0)));
        if (graphs)
        {
            TilePaint.LineRight(dc, Gfx, scale, form.SmallPx, Weight.Regular, theme.TextSecondary, right, figureTop + figureLine - form.SmallLine, scaleWidth);
        }
        var y = figureTop + figureLine;
        Second(dc, style, form, left, right, y);
        if (!graphs)
        {
            return;
        }

        // The minute, edge to edge on its quarters' hairlines, the newest at the right, down to its captions at the foot:
        // the rest of the line's whole cells, never less than its least (ListHeight).
        var graphTop = y + form.SmallLine + style.Px(6);
        var graph = new D2D_RECT_F(box.left, graphTop, box.right, MathF.Max(graphTop + style.Px(MinuteGraphDips), box.bottom - Foot(style, form) - form.SmallLine - style.Px(4)));
        History(dc, graph, style, series, full);
        var captionTop = graph.bottom + style.Px(4);
        var pace = fed ? 1 : options.Seconds;
        TilePaint.Line(dc, Gfx, PerformanceModel.SpanAt(pace), form.SmallPx, Weight.Regular, theme.TextSecondary, left, captionTop, right - left);
        TilePaint.LineRight(dc, Gfx, "0", form.SmallPx, Weight.Regular, theme.TextSecondary, right, captionTop, right - left);
    }

    /// <summary>
    /// The last minute in <paramref name="box"/>, as the flyout drew it: the newest
    /// <see cref="PerformanceModel.Samples"/> steps across the whole width, on hairlines at the box's
    /// top, its quarters and its foot, scaled to <paramref name="full"/>.
    /// </summary>
    private void History(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, in Series series, float full)
    {
        ID2D1SolidColorBrush* brush = null;
        try
        {
            brush = Gfx.Brush(dc, PopupPaint.Ink(style.Theme.Divider));
            var hair = MathF.Max(1, style.Px(1));
            var height = box.bottom - box.top;
            for (var quarter = 0; quarter <= 4; quarter++)
            {
                var y = MathF.Min(box.top + MathF.Round(height * quarter / 4), box.bottom - hair);
                var rule = new D2D_RECT_F(box.left, y, box.right, y + hair);
                dc->FillRectangle(&rule, (ID2D1Brush*)brush);
            }
            var accent = TilePaint.Accent(style);
            var step = (box.right - box.left) / (PerformanceModel.Samples - 1);
            // The oldest step stands past the left edge, so the line runs out of the box rather than stopping short.
            dc->PushAxisAlignedClip(&box, D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_ALIASED);
            Graphs.Sparkline.Paint(dc, brush, accent, box, step, MathF.Max(1, style.Px(1.5f)), series, full, Points);
            dc->PopAxisAlignedClip();
        }
        finally
        {
            ComUtil.Release(ref brush);
        }
    }

    /// <summary>The graph behind a list's line, <paramref name="box"/> edge to edge, faded where its <paramref name="words"/> stand.</summary>
    private void Backdrop(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, bool down, ReadOnlySpan<(float From, float To)> words)
    {
        var shown = style;
        GraphBackdrop.Paint(dc, box, 0, down, words, style.Px(down ? 28 : 16), () => Graph(dc, box, shown));
    }

    /// <summary>The reading's names, fullest first: its title, and its short word where it has one ("Processor", "CPU").</summary>
    private string[] Names() => Shorter is { } shorter ? [Title, shorter] : [Title];

    /// <summary>
    /// A line's second reading, under the name or the figure, as figures in fields (<see cref="TilePaint.Fields"/>):
    /// the card's own second line — the memory in use, down and up, read and write — or, for a
    /// reading that has none, its busiest moment.
    /// </summary>
    internal IReadOnlyList<(string Text, string Shape)> CompactParts() => metric switch
    {
        Metric.Cpu or Metric.Gpu when Series.Count > 0 => [($"{S.WidgetPerformancePeak} ", $"{S.WidgetPerformancePeak} "), (Units.Percent(Peak()), Units.PercentRoom)],
        _ => CardParts(),
    };

    /// <summary>
    /// The card's second line as fields: the memory in use, down and up, read and write — each
    /// figure in the room of the widest it can be, so the line keeps its width and its places as
    /// the figures change. A disk's read and write are there whether or not they have been read
    /// yet, a dash standing for one that has not. None where the reading has nothing more to say.
    /// </summary>
    internal IReadOnlyList<(string Text, string Shape)> CardParts() => metric switch
    {
        Metric.Memory when Source.Memory is { } reading => [(Units.Of(reading.Used, reading.Total), Units.OfShape)],
        Metric.Network when Source.Down >= 0 =>
            [("↓ ", "↓ "), (Units.ShortRate(Source.Down), Units.RateShape), ("  ↑ ", "  ↑ "), (Units.ShortRate(Source.Up), Units.RateShape)],
        Metric.Disk =>
            [($"{S.WidgetPerformanceReadShort} ", $"{S.WidgetPerformanceReadShort} "), (Source.DiskRead >= 0 ? Units.ShortRate(Source.DiskRead) : "–", Units.RateShape),
             ($"  {S.WidgetPerformanceWriteShort} ", $"  {S.WidgetPerformanceWriteShort} "), (Source.DiskWritten >= 0 ? Units.ShortRate(Source.DiskWritten) : "–", Units.RateShape)],
        _ => [],
    };

    /// <summary>The busiest step of what the graph can draw (<see cref="Series"/>).</summary>
    private float Peak()
    {
        var series = Series;
        var most = 0f;
        for (var age = 0; age < series.Count; age++)
        {
            most = MathF.Max(most, series.Ago(age));
        }
        return most;
    }

    /// <summary>
    /// The name a reading draws beside itself: the reading's own word ("Processor"), not its graph's
    /// catalogue title ("Processor graph", told apart there from its details), and "Network" for the
    /// network's, which beside its own reading says it.
    /// </summary>
    internal static string Drawn(Metric metric) => metric switch
    {
        Metric.Cpu => S.WidgetPerformanceProcessor,
        Metric.Gpu => S.WidgetPerformanceGraphics,
        Metric.Memory => S.WidgetPerformanceMemory,
        Metric.Disk => S.WidgetPerformanceDisk,
        _ => S.WidgetPerformanceNetwork,
    };

    /// <summary>
    /// The name this graph draws beside its reading: its kind's (<see cref="Drawn"/>), or, for a
    /// disk graph that follows one disk, that disk as Task Manager names it — "Disk 1" — so two
    /// disk graphs side by side say which is which.
    /// </summary>
    internal string Title => Disk is { } number ? PerformanceOptions.DiskName(number) : Drawn(metric);

    /// <summary>
    /// This graph's shorter name where its title does not fit: "CPU", "GPU", and "Disk" for a disk
    /// graph following one disk; null where there is none.
    /// </summary>
    private string? Shorter => Disk is not null ? Drawn(metric) : Short(metric);

    /// <summary>A reading's name in a word that is shorter, where it has one; null where it does not.</summary>
    internal static string? Short(Metric metric) => metric switch
    {
        Metric.Cpu => S.WidgetPerformanceProcessorShort,
        Metric.Gpu => S.WidgetPerformanceGraphicsShort,
        _ => null,
    };

    // --- What the reading says ---------------------------------------------------------------

    /// <summary>The figure the line shows, and whether it is a reading at all.</summary>
    private (string Text, bool Known) Reading() => ReadingOf(Source);

    /// <summary>
    /// The figure <paramref name="source"/> stands at — a share, or for the network down and up
    /// together as a short rate — and whether it is a reading at all: a dash while not known.
    /// </summary>
    internal static (string Text, bool Known) ReadingOf(PerformanceData source) => source.Metric switch
    {
        Metric.Network when source.Down >= 0 => (Units.ShortRate(source.Down + source.Up), true),
        Metric.Network => ("–", false),
        _ when source.Share >= 0 => (Units.Percent(source.Share), true),
        _ => ("–", false),
    };

    // --- The face on the bar --------------------------------------------------------------------
    //
    // What the entry on the bar was, drawn for the reading's preset's face (FaceDrawings): a function
    // of the bar's numbers and the reading alone, never of what it reads, so the face is measured on
    // the worst case and no figure can move the bar (docs\widgets.md §4).

    /// <summary>
    /// The face's pieces on a bar of <paramref name="style"/>: the glyph's size, the figure's text
    /// size, the room its worst case keeps (<see cref="Room"/>) and its line's height. Across a bar
    /// the glyph is as tall as the line, as every glyph beside its text on the bar is, up to a glyph
    /// button's; down one it names the reading over it at a glyph button's size, as the network's
    /// and the sound's glyphs beside it are. The figure is the bar's main size, but down a bar too narrow for the widest of the
    /// five it steps down to the largest at which that fits the room across (<see cref="VerticalFit"/>),
    /// so every reading's face of a style reads alike and none is cut.
    /// </summary>
    internal static (float GlyphPx, float TextPx, float Field, float Row) FaceRoom(Gfx gfx, Metric metric, in IslandStyle style)
    {
        var fit = new Fit(style);
        var px = fit.Primary;
        if (style.Vertical)
        {
            px = VerticalFit.Across(gfx, Units.RateShape, VerticalFit.Room(fit), Weight.Regular, [fit.Primary, fit.Secondary, fit.Small, 0.85f * fit.Small]).Px;
        }
        var (field, row) = TextLine.Template(gfx, Room(metric), px, Weight.Regular);
        var glyph = style.Vertical
            ? fit.Glyph()
            : MathF.Min(fit.GlyphFor(row), fit.Glyph());
        return (glyph, px, field, row);
    }

    /// <summary>
    /// The room a face in its graph style keeps for its graph between the glyph and the figure, along
    /// the bar: about a square of the bar's thickness, 32 DIP at the least (§295). Fixed for a bar, so
    /// the history scrolling through it never moves the bar.
    /// </summary>
    internal static float GraphRoom(in Fit fit) => fit.Px(MathF.Max(32, fit.Thick));

    /// <summary>
    /// The face's length along the bar: the glyph and the figure side by side across it, the glyph
    /// over the figure down it, and in its graph style (<paramref name="graph"/>) the graph's room
    /// between them (<see cref="GraphRoom"/>). Across it the figure is <paramref name="text"/>, the one
    /// it reads now (docs\widgets.md §244), in tabular figures, so only a figure more or fewer moves it
    /// — and never narrower than two figures ("88%") or a rate's room ("8.8M") once a reading is
    /// <paramref name="known"/>: a reading taken every second would otherwise move the bar each time
    /// it crossed 9 % or a unit. The dash before the first reading is its own width. Null keeps the
    /// worst case's room.
    /// </summary>
    internal static float FaceLength(Gfx gfx, Metric metric, in IslandStyle style, string? text = null, bool known = true, bool graph = false)
    {
        var fit = new Fit(style);
        var (glyph, px, field, row) = FaceRoom(gfx, metric, style);
        if (!style.Vertical && text is not null)
        {
            var least = metric == Metric.Network ? Units.RateShape : Units.Percent(0.88f);
            field = TextLine.Template(gfx, text, px, Weight.Regular).Width;
            field = known ? MathF.Max(field, TextLine.Template(gfx, least, px, Weight.Regular).Width) : field;
        }
        var room = graph ? GraphRoom(fit) : 0;
        return MathF.Ceiling(style.Vertical ? glyph + fit.Px(2) + room + row : glyph + fit.Gap + room + field);
    }

    /// <summary>
    /// The face in <paramref name="box"/>, as the entry drew itself: the glyph in the quieter ink and
    /// the figure in the text's (the quieter too while not known), the figure right-aligned at the
    /// box's end; down a bar on its side, the glyph over the figure, both centred. In its graph style
    /// <paramref name="graph"/> is drawn behind them as a reading's line draws its graph
    /// (<see cref="Graphed"/>): the button's ground, across the bar edge to edge and along it to half
    /// the room between faces, on the card's corners, faded under the glyph and the figure and full in
    /// the room kept between them.
    /// </summary>
    internal static void PaintFace(ID2D1DeviceContext* dc, Gfx gfx, Metric metric, in IslandStyle style, D2D_RECT_F box, string text, bool known, Series? graph = null)
    {
        var fit = new Fit(style);
        var ink = new IslandInk(style.Look);
        var (glyphPx, px, _, row) = FaceRoom(gfx, metric, style);
        var room = graph is null ? 0 : GraphRoom(fit);
        var width = box.right - box.left;
        // The room after the glyph and the graph is the figure's: what the face was measured for it (FaceLength).
        var field = MathF.Max(0, width - glyphPx - fit.Gap - room);
        var height = box.bottom - box.top;
        // Along the bar the graph reaches past the face to half the room between two faces, so two
        // graphs side by side never overlap and a lone one nearly fills its button.
        var reach = MathF.Min(fit.Edge, FolderIsland.Between(fit) / 2);
        var ground = style.Vertical
            ? box with { top = box.top - reach, bottom = box.bottom + reach }
            : box with { left = box.left - reach, right = box.right + reach };
        dc->SetTextAntialiasMode(D2D1_TEXT_ANTIALIAS_MODE.D2D1_TEXT_ANTIALIAS_MODE_GRAYSCALE);
        ID2D1SolidColorBrush* brush = null;
        TextLine value = default;
        TextLine glyph = default;
        try
        {
            value = TextLine.Lay(gfx, text, px, Weight.Regular, style.Vertical ? width : null);
            glyph = TextLine.Lay(gfx, GlyphOf(metric), glyphPx, Weight.Regular, null, gfx.IconFamily);
            var figure = known ? ink.Text : ink.Secondary;
            if (style.Vertical)
            {
                var top = box.top + TextLine.Round((height - (glyphPx + fit.Px(2) + room + row)) / 2);
                var under = top + glyphPx + fit.Px(2) + room;
                Behind(dc, metric, graph, ground, ink.Accent, fit, style, [(ground.top, top + glyphPx), (under, ground.bottom)]);
                brush = Gfx.Brush(dc, ink.Text);
                glyph.Draw(dc, brush, ink.Secondary, box.left + ((width - glyph.Width) / 2), top + ((glyphPx - glyph.Height) / 2));
                value.Draw(dc, brush, figure, box.left + ((width - value.Width) / 2), under);
                return;
            }
            var rowTop = box.top + TextLine.Round((height - row) / 2);
            var left = box.left + glyphPx + fit.Gap + room + field - value.Width;
            Behind(dc, metric, graph, ground, ink.Accent, fit, style, [(ground.left, box.left + glyphPx), (left, ground.right)]);
            brush = Gfx.Brush(dc, ink.Text);
            glyph.Draw(dc, brush, ink.Secondary, box.left + ((glyphPx - glyph.Width) / 2), rowTop + ((row - glyph.Height) / 2));
            value.Draw(dc, brush, figure, left, rowTop);
        }
        finally
        {
            glyph.Release();
            value.Release();
            ComUtil.Release(ref brush);
        }
    }

    /// <summary>A face's graph where it is in its graph style, over <paramref name="ground"/>, faded where its <paramref name="words"/> stand.</summary>
    private static void Behind(ID2D1DeviceContext* dc, Metric metric, Series? graph, D2D_RECT_F ground, DXGI_RGBA accent, Fit fit, IslandStyle style,
        ReadOnlySpan<(float From, float To)> words)
    {
        if (graph is not { } series)
        {
            return;
        }
        GraphBackdrop.Paint(dc, ground, GraphBackdrop.BarRadius(style), style.Vertical, words, 2 * fit.Gap,
            () => Graphed(dc, metric, series, accent, ground, fit.Spacing, fit.Stroke));
    }

    // --- The graph ----------------------------------------------------------------------------

    // The points a graph is drawn through, one array a thread for every graph drawn on it, so a tick allocates none.
    [ThreadStatic]
    private static D2D_POINT_2F[]? points;

    private static D2D_POINT_2F[] Points => points ??= new D2D_POINT_2F[Samples + 2];

    /// <summary>
    /// <paramref name="series"/> of <paramref name="metric"/> as a filled area under a line in
    /// <paramref name="accent"/>, newest at <paramref name="box"/>'s right, one step every
    /// <paramref name="step"/> px back to its left, clipped to the box (<see cref="Sparkline.Paint"/>): a
    /// share against the whole, a rate against the busiest step kept (<see cref="Sparkline.Ceiling"/>).
    /// The one drawing of a reading's graph, a list's line's and its face's on the bar (§295).
    /// <para>
    /// This is the whole of the scrolling: a sample is added and the cell is redrawn, so the line
    /// is one step further along. Nothing animates it, and nothing outside this call knows where
    /// the graph is.
    /// </para>
    /// </summary>
    internal static void Graphed(ID2D1DeviceContext* dc, Metric metric, in Series series, DXGI_RGBA accent, D2D_RECT_F box, float step, float stroke)
    {
        ID2D1SolidColorBrush* brush = null;
        try
        {
            brush = Gfx.Brush(dc, accent);
            dc->PushAxisAlignedClip(&box, D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_ALIASED);
            var full = metric == Metric.Network ? Graphs.Sparkline.Ceiling(series, series.Count) : 1f;
            Graphs.Sparkline.Paint(dc, brush, accent, box, step, stroke, series, full, Points);
            dc->PopAxisAlignedClip();
        }
        finally
        {
            ComUtil.Release(ref brush);
        }
    }

    /// <summary>What this widget has read, for the tests and for a card that wants the whole of it.</summary>
    internal (float Share, double Down, double Up) Now => (Source.Share, Source.Down, Source.Up);

    /// <summary>
    /// Puts a reading in as a sample would, without touching the machine. The tests' way in, and
    /// the only one: "the same width at 4 % as at 100 %" needs both readings, and a picture of a
    /// graph needs a history to draw. The dock never calls this. The first reading handed over
    /// gives this graph a source of its own (a stand-in), so what a test feeds one graph never
    /// reaches another graph of the same reading, and nothing the machine reads reaches it.
    /// </summary>
    /// <param name="reading">0 to 1, or -1 for a widget that has not been told anything yet.</param>
    /// <param name="down">Bytes a second, for a network widget; -1 for not known.</param>
    /// <param name="up"><inheritdoc cref="down"/></param>
    /// <param name="memory">What the memory holds, for a memory widget's second line; kept when not given.</param>
    /// <param name="read">Bytes a second read, for a disk widget's second line; -1 for not known.</param>
    /// <param name="written"><inheritdoc cref="read"/></param>
    internal void Feed(float reading = -1, double down = -1, double up = -1, MemoryReading? memory = null, double read = -1, double written = -1)
    {
        Fed().Feed(reading, down, up, memory, read, written);
    }

    /// <summary>Forgets every sample: for a test that wants an empty graph. The graph has a stand-in of its own from here, as <see cref="Feed"/> gives it.</summary>
    internal void Forgotten() => Fed().Forget();

    /// <summary>This graph's stand-in, made the first time a reading is handed to it: the shared source is let go.</summary>
    private PerformanceData Fed()
    {
        if (!fed)
        {
            sources.Release(source);
            source = StandIn();
            fed = true;
        }
        return Source;
    }
}
