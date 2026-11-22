// perf: the performance graphs keep their size whatever they are reading.
//
// This is docs\widgets.md §4 for the five kinds that are most tempted to break
// it. A graph's reading changes every second, and a widget measured for the
// reading it happens to be showing shoves the whole bar along once a second —
// which is exactly what the old island did: it measured a percentage as "88%"
// below a hundred and "100%" at it, so a processor hitting 100 % widened the
// widget and moved every neighbour.
//
// The fix is structural rather than remembered: the size comes from the
// reading's face (a folder's, since the entries became the readings' presets,
// docs\widgets.md §97), which is handed the settings and the bar's numbers and
// has no way to reach a sampler. These tests hold the two halves of it —
//
//   - the room a kind reserves is the worst case the formatter can ever
//     produce, which is checkable without a screen; and
//   - a widget fed every reading it can be in measures one length, which needs
//     Direct2D and so runs only with KARPO_UI_TESTS=1.
//
// The second is the real guard. The first is what says *why* it passes.

using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Islands;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Perf;

[Collection(SamplerState.Collection)]
public sealed unsafe class GraphShapeTests
{
    /// <summary>The bar heights the person's own files use, thin to thick.</summary>
    private static readonly int[] Heights = [28, 41, 72];

    /// <summary>What a line of <paramref name="kind"/> is.</summary>
    private static WidgetInfo InfoOf(ListKind kind) => ListWidgets.Of(kind).Info;

    private static readonly ListKind[] Kinds =
        [ListKind.Cpu, ListKind.Gpu, ListKind.Memory, ListKind.Disk, ListKind.Network];

    private static readonly PresetMachine Machine = new(HasBattery: false, FixedDrives: []);

    /// <summary>The preset of the reading <paramref name="kind"/> is, as the add list makes it: its line, and its face.</summary>
    private static BarItem Preset(ListKind kind) => Presets.Folder(Presets.Find(kind switch
    {
        ListKind.Cpu => Presets.CpuId,
        ListKind.Gpu => Presets.GpuId,
        ListKind.Memory => Presets.MemoryId,
        ListKind.Disk => Presets.DiskId,
        _ => Presets.NetworkActivityId,
    })!, Machine);

    /// <summary>Every reading a percentage widget can be in, including the two it is born in.</summary>
    private static readonly float[] Shares = [-1, 0f, 0.04f, 0.5f, 0.999f, 1f];

    /// <summary>
    /// Every rate a network widget can be in: nothing known, silence, a trickle, and a link
    /// saturated — "9 KB/s" to "120 MB/s" is the case the bar must not move for.
    /// </summary>
    private static readonly (double Down, double Up)[] Rates =
    [
        (-1, -1),
        (0, 0),
        (9 * 1024, 512),
        (120 * 1024 * 1024, 4 * 1024 * 1024),
        (12d * 1024 * 1024 * 1024, 0),
    ];

    private static Look LookFor(bool dark, float textScale = 1f) => new(
        Glass: false,
        Dark: dark,
        Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4),
        AccentLight2: Color.FromArgb(255, 0x99, 0xEB, 0xFF),
        AccentDark1: Color.FromArgb(255, 0x00, 0x5F, 0xB8),
        TextScale: textScale);

    // --- What the room is, without a screen ---------------------------------------------------

    /// <summary>
    /// The room is a constant of the reading. It takes the metric and nothing else — no island, no
    /// sampler, no reading — which is the structural half of the rule: there is no argument to
    /// pass that could make a percentage widget reserve less than a full hundred.
    /// </summary>
    [Theory]
    [InlineData(Metric.Cpu)]
    [InlineData(Metric.Gpu)]
    [InlineData(Metric.Memory)]
    [InlineData(Metric.Disk)]
    public void APercentageAlwaysReservesAFullHundred(Metric metric) =>
        Assert.Equal("100%", PerformanceIsland.Room(metric));

    [Fact]
    public void ARateAlwaysReservesTheWidestShortRate() =>
        Assert.Equal(Units.RateShape, PerformanceIsland.Room(Metric.Network));

    /// <summary>
    /// Every percentage the formatter can produce fits the room reserved for it. Over all 101 of
    /// them, because "the ones I thought of" is how "100%" got missed the first time.
    /// </summary>
    [Fact]
    public void EveryPercentageFitsTheRoomAPercentageKeeps()
    {
        var room = Units.PercentRoom;
        for (var percent = 0; percent <= 100; percent++)
        {
            var text = Units.Percent(percent / 100f);
            Assert.True(
                text.Length <= room.Length,
                $"\"{text}\" is longer than the \"{room}\" a percentage widget keeps room for");
        }
    }

    /// <summary>
    /// Every short rate fits the room a rate keeps, over eleven orders of magnitude: a byte a
    /// second to sixteen gigabytes a second, which is past any link this will ever meet.
    /// </summary>
    [Fact]
    public void EveryRateFitsTheRoomARateKeeps()
    {
        var room = Units.RateShape;
        for (double rate = 0; rate < 16d * 1024 * 1024 * 1024; rate = rate < 1 ? 1 : rate * 1.07)
        {
            var text = Units.ShortRate(rate);
            Assert.True(
                text.Length <= room.Length,
                $"{rate:N0} B/s reads \"{text}\", which is longer than the \"{room}\" a network widget keeps room for");
        }
        // The ends, said plainly.
        Assert.True(Units.ShortRate(0).Length <= room.Length);
        Assert.True(Units.ShortRate(double.MaxValue).Length <= room.Length);
        // And a negative rate, which two counters read a moment apart can produce.
        Assert.True(Units.ShortRate(-5).Length <= room.Length);
    }

    /// <summary>
    /// A widget that is not one of the five has no reading, and a graph asked for one says so
    /// rather than handing back a percentage's room (docs\widgets.md §5: inside the boundary, every
    /// value came from this build's own enum).
    /// </summary>
    [Fact]
    public void AKindThatIsNotAGraphIsRefusedRatherThanGuessed()
    {
        Assert.Null(BarEntries.Of(BarKind.NightLight).Info.Metric);
        Assert.Null(BarEntries.Of(BarKind.WidgetsBoard).Info.Metric);
        Assert.Throws<ArgumentOutOfRangeException>(() => WidgetInfos.For((Metric)42));
    }

    /// <summary>The graph and the reading it shows are one answer read both ways.</summary>
    [Theory]
    [InlineData(ListKind.Cpu)]
    [InlineData(ListKind.Gpu)]
    [InlineData(ListKind.Memory)]
    [InlineData(ListKind.Disk)]
    [InlineData(ListKind.Network)]
    public void EveryGraphKindIsItsReadingAndBack(ListKind kind) =>
        Assert.Same(InfoOf(kind), WidgetInfos.For(InfoOf(kind).Metric!.Value));

    /// <summary>
    /// The five are the five: a sixth reading added to the registry without a sampler behind it
    /// fails here rather than throwing on the bar the first time somebody places one.
    /// </summary>
    [Fact]
    public void EveryKindThatSaysItIsAGraphHasAReadingBehindIt()
    {
        var graphs = ListWidgets.All.Where(entry => entry.Drawing == WidgetSource.Performance && !entry.Info.Facts).Select(entry => entry.Kind).ToArray();

        Assert.Equal(Kinds, graphs);
        // None is a taskbar entry: on the bar each is its preset's face, sized by the folder's shape.
        Assert.DoesNotContain(BarEntries.All, entry => entry.Drawing == WidgetSource.Performance);
        foreach (var kind in graphs)
        {
            Assert.Equal(WidgetOptionsKind.Performance, InfoOf(kind).Options);
            Assert.Equal(InfoOf(kind).Metric, PerformanceFace.MetricOf(Preset(kind).Folder!.Face.Info));
        }
    }

    // --- What the room is on a screen ---------------------------------------------------------

    /// <summary>
    /// The real guard: a reading's face fed every reading it can be in measures one length — across a
    /// bar all but a full hundred, a figure more (docs\widgets.md §244): no reading taken each second
    /// moves the bar as it crosses 9 % or a unit. Over the
    /// heights the person's files use, both orientations, and ordinary and the largest text — the
    /// last because a template that fits at 100 % text and not at 225 % is a bar that moves only
    /// for people who need large text, which is nobody we would hear from. The face reads its
    /// folder's line, which is fed here; no counter of this PC's is read.
    /// </summary>
    [Karpo.Tests.Islands.UiFact]
    public void AFaceIsTheSameWidthWhateverItIsReading()
    {
        var wrong = new List<string>();
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var kind in Kinds)
            foreach (var height in Heights)
            foreach (var vertical in new[] { false, true })
            foreach (var textScale in new[] { 1f, 2.25f })
            {
                const uint dpi = 96;
                var config = new Config { Height = height };
                var look = LookFor(dark: true, textScale);
                var style = new IslandStyle(dpi / 96f, vertical, Layout.CellPx(dpi, config), Layout.IconPx(dpi, config), look);
                var holder = Preset(kind);
                using var folder = new FolderIsland(Config.IslandKey(null, holder.Id), holder, gfx, config);
                var line = (PerformanceIsland)folder.Tiles[0].Island;

                var lengths = new List<(string What, float Length)>();
                foreach (var (what, feed) in Readings(kind))
                {
                    line.Forgotten();
                    feed(line);
                    Assert.Same(line.Source, folder.Performance);
                    lengths.Add((what, folder.Measure(style).CellLength));
                }
                // Across a bar a full hundred is a figure more (docs\widgets.md §244), and may be wider;
                // the dash before the first reading is its own width, and no wider.
                var hundred = Units.Percent(1f);
                var first = lengths.First(each => vertical || !each.What.StartsWith("nothing known", StringComparison.Ordinal));
                bool Wrong(string what, float length) =>
                    vertical ? length != first.Length
                    : what.StartsWith("nothing known", StringComparison.Ordinal) ? length > first.Length
                    : what == hundred ? length < first.Length
                    : length != first.Length;
                foreach (var (what, length) in lengths)
                {
                    if (Wrong(what, length))
                    {
                        wrong.Add(
                            $"{kind} at {height} DIP {(vertical ? "upright" : "across")} text×{textScale}: "
                            + $"{first.What} is {first.Length} px and {what} is {length} px");
                    }
                }
            }
        });
        Assert.Empty(wrong);
    }

    /// <summary>
    /// The face's pace and device change what it reads, never its room: a processor stepping every
    /// ten seconds and a disk following one disk are as long as at their defaults.
    /// </summary>
    [Karpo.Tests.Islands.UiFact]
    public void AFacesOptionsChangeNothingOnTheBar()
    {
        var moved = new List<string>();
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var kind in Kinds)
            foreach (var height in Heights)
            {
                const uint dpi = 96;
                var config = new Config { Height = height };
                var style = new IslandStyle(dpi / 96f, false, Layout.CellPx(dpi, config), Layout.IconPx(dpi, config), LookFor(dark: true));
                var holder = Preset(kind);
                using var folder = new FolderIsland(Config.IslandKey(null, holder.Id), holder, gfx, config);
                var before = folder.Measure(style).CellLength;

                var face = holder.Folder!.Face;
                face.Set(face.Info.Option(PerformanceFace.Pace)!, "10");
                if (face.Info.Option(PerformanceFace.Disk) is { } disk)
                {
                    face.Set(disk, "Disk 1");
                }
                folder.Configure(holder, config);
                var after = folder.Measure(style).CellLength;

                if (after != before)
                {
                    moved.Add($"{kind} at {height} DIP is {after} px set and {before} px at its defaults");
                }
            }
        });
        Assert.Empty(moved);
    }

    /// <summary>Every reading a widget of <paramref name="kind"/> can be in, and how to put it there.</summary>
    private static IEnumerable<(string What, Action<PerformanceIsland> Feed)> Readings(ListKind kind)
    {
        if (InfoOf(kind).Metric == Metric.Network)
        {
            foreach (var (downRate, upRate) in Rates)
            {
                var down = downRate;
                var up = upRate;
                yield return (down < 0 ? "nothing known" : $"{Units.Rate(down)} down", island => island.Feed(down: down, up: up));
            }
            yield break;
        }
        foreach (var value in Shares)
        {
            var share = value;
            yield return (share < 0 ? "nothing known" : Units.Percent(share), island => island.Feed(share));
        }
    }
}
