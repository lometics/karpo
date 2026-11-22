// The performance readings, shown large (Karpo.Dock\Performance\) and gathered once for everyone
// (Karpo.Dock\Sources\).
//
// The person, 2026-10-09: "if you click on one of the system graph widgets it should also show a
// flyout with more information about that specific metric", "the graph of the system widgets
// should also be removed in the taskbar view. the graph should show with the flyout again", and
// "there should be one service in the background that always gets the data for the graph and then
// all the widgets that show this data should pull the data from this one service. this service
// should always run."
//
// What that asks, held here without a window: one source per reading, running from the dock's start
// with no widget in the config; every widget of that reading — a line, a face — drawing the one
// source and none sampling on its own; the history it keeps being long enough for the flyout's
// minute and wrapping as a ring; and the words of that flyout being a function of the source and a
// frozen clock, never of this PC. The flyout went on 2026-10-10 (docs\widgets.md §97): its words are
// the reading's line at its tallest now, and the entries the readings' presets' faces; the drawing of
// both is in PerformancePresetTests.

using Karpo.Core;
using Karpo.Dock;
using Karpo.Dock.Performance;

namespace Karpo.Tests.Perf;

public sealed class PerformanceReadingTests
{
    private static readonly Metric[] Metrics = Enum.GetValues<Metric>();

    private static readonly ListKind[] Kinds = [ListKind.Cpu, ListKind.Gpu, ListKind.Memory, ListKind.Disk, ListKind.Network];

    /// <summary>The dock's own registry, the always-on list included, with sources that read 0.25 rather than this PC.</summary>
    private static DataSources Registry(List<PerformanceData>? made = null) => new((key, widget) =>
    {
        var metric = widget.Info!.Metric!.Value;
        var source = new PerformanceData(metric, PerformanceData.DeviceOf(metric, widget.Performance ?? new PerformanceOptions())) { Reading = () => 0.25f };
        made?.Add(source);
        return source;
    }, SourceKinds.Always());

    private static Config Empty() => new() { Layouts = [new TaskbarLayout { Id = "main", Name = "main", Islands = [] }], DefaultLayoutId = "main" };

    private static CellItem Entry(ListKind kind)
    {
        var entry = kind.New();
        entry.Id = kind.ToString().ToLowerInvariant();
        return entry;
    }

    /// <summary>A folder on the bar wearing the face <paramref name="face"/>, holding <paramref name="inside"/>.</summary>
    private static BarItem Folder(FolderFace face, params CellItem[] inside)
    {
        var folder = BarKind.FolderList.New();
        folder.Id = "folder";
        folder.Folder!.Face = face;
        folder.Folder.Lists = [[.. inside.Select(island => new FolderCell { Island = island })]];
        return folder;
    }

    // --- One source per reading, always running -----------------------------------------------------

    [Fact]
    public void EveryReadingHasASourceFromTheStartWithNoWidgetInTheConfig()
    {
        var sources = Registry();
        sources.Want(Empty());

        Assert.Equal(Metrics.Length, sources.Running.Count);
        foreach (var metric in Metrics)
        {
            var key = PerformanceData.KeyOf(metric, new PerformanceOptions());
            Assert.IsType<PerformanceData>(sources.Find(key));
        }
    }

    [Fact]
    public void TheAlwaysRunningListIsTheDefaultKeyOfEachReading()
    {
        var always = SourceKinds.Always();

        Assert.Equal(Metrics.Length, always.Count);
        Assert.Equal(Metrics.Length, always.Select(item => item.Key).Distinct().Count());
        foreach (var kind in Kinds)
        {
            Assert.Contains(SourceKinds.KeyOf(Entry(kind))!.Value, always.Select(item => item.Key));
        }
    }

    [Fact]
    public void TheSourcesGatherWithoutAnyWidgetAndKeepTheirHistory()
    {
        var sources = Registry();
        sources.Want(Empty());

        for (var tick = 0; tick < 5; tick++)
        {
            sources.Tick(10_000 + (tick * 1000L));
        }

        foreach (var metric in Metrics)
        {
            var source = (PerformanceData)sources.Find(PerformanceData.KeyOf(metric, new PerformanceOptions()))!;
            Assert.Equal(5, source.Gathered);
            Assert.Equal(5, source.History.Count);
        }
    }

    [Fact]
    public void TheDocksTimerRunsAtTheSourcesPaceWithNothingInTheConfig()
    {
        var sources = Registry();
        sources.Want(Empty());

        Assert.Equal(1000, sources.Period);
    }

    [Fact]
    public void ALayoutThatNamesNoGraphDoesNotStopTheReadings()
    {
        var sources = Registry();
        sources.Want(new Config { Layouts = [new TaskbarLayout { Id = "main", Name = "main", Islands = [Folder(FolderFace.Of(FolderFaces.CpuId), Entry(ListKind.Cpu))] }], DefaultLayoutId = "main" });
        sources.Want(Empty());

        Assert.Equal(Metrics.Length, sources.Running.Count);
    }

    [Fact]
    public void EveryWidgetOfAReadingHoldsTheOneSource()
    {
        var made = new List<PerformanceData>();
        var sources = Registry(made);
        sources.Want(Empty());

        var bar = sources.Hold(SourceKinds.FaceLine(FolderFace.Of(FolderFaces.CpuId))!);
        var line = sources.Hold(ListKind.Cpu.New());

        Assert.Same(bar, line);
        Assert.Equal(Metrics.Length, made.Count);
        // Letting every widget go leaves the dock's own hold: it keeps running.
        sources.Release(bar);
        sources.Release(line);
        Assert.Same(bar, sources.Find(PerformanceData.KeyOf(Metric.Cpu, new PerformanceOptions())));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(10)]
    public void AWidgetAtAnotherPaceReadsTheOneSource(int seconds)
    {
        var sources = Registry();
        sources.Want(Empty());
        var slow = Entry(ListKind.Cpu);
        slow.Performance!.Seconds = seconds;

        var held = sources.Hold(slow);

        Assert.Same(held, sources.Find(PerformanceData.KeyOf(Metric.Cpu, new PerformanceOptions())));
        Assert.Equal(Metrics.Length, sources.Running.Count);
        Assert.Equal(1000, sources.Period);
    }

    [Fact]
    public void OnlyAnotherDeviceAddsASource()
    {
        var sources = Registry();
        sources.Want(Empty());
        var disk = Entry(ListKind.Disk);
        disk.Performance!.Disk = "Disk 1";
        disk.Performance.Seconds = 3;

        var held = sources.Hold(disk);

        Assert.NotSame(held, sources.Find(PerformanceData.KeyOf(Metric.Disk, new PerformanceOptions())));
        Assert.Equal(Metrics.Length + 1, sources.Running.Count);
    }

    // --- A step every so many samples ----------------------------------------------------------------

    private static History Counted(int count, int capacity = 64)
    {
        var history = new History(capacity);
        for (var i = 1; i <= count; i++)
        {
            history.Add(i);
        }
        return history;
    }

    [Fact]
    public void AtOneSecondTheSeriesIsTheHistory()
    {
        var history = Counted(10);
        var series = new Series(history, 1, 512);

        Assert.Equal(10, series.Count);
        for (var age = 0; age < 10; age++)
        {
            Assert.Equal(history.Ago(age), series.Ago(age));
        }
    }

    [Fact]
    public void AStepIsTheMeanOfItsSamplesAndTheOneStillFillingIsLeftOut()
    {
        // 1..12 at three a step: (1,2,3) (4,5,6) (7,8,9) (10,11,12), all whole.
        var whole = new Series(Counted(12), 3, 512);
        Assert.Equal(4, whole.Count);
        Assert.Equal(11, whole.Ago(0));
        Assert.Equal(2, whole.Ago(3));

        // 1..13: 13 is the first of a step still filling, and is not drawn yet.
        var filling = new Series(Counted(13), 3, 512);
        Assert.Equal(4, filling.Count);
        Assert.Equal(11, filling.Ago(0));
    }

    [Fact]
    public void AStepNeverChangesOnceItIsWhole()
    {
        var history = new History(64);
        var seen = new List<float>();
        for (var i = 1; i <= 40; i++)
        {
            history.Add(i * i);
            var series = new Series(history, 5, 512);
            if (series.Count > 0 && (seen.Count == 0 || series.Count > seen.Count))
            {
                seen.Add(series.Ago(0));
            }
            // Every step drawn before is drawn the same now.
            for (var age = 0; age < seen.Count; age++)
            {
                Assert.Equal(seen[seen.Count - 1 - age], series.Ago(age));
            }
        }
    }

    [Fact]
    public void ARingThatWrappedHasOnlyWholeStepsAndNoMoreThanAGraphDraws()
    {
        // 70 samples in a ring of 64: the oldest whole step starts within what is kept.
        var history = Counted(70);
        var series = new Series(history, 5, 512);
        Assert.Equal(12, series.Count);
        Assert.Equal((66 + 67 + 68 + 69 + 70) / 5f, series.Ago(0));
        Assert.Equal((11 + 12 + 13 + 14 + 15) / 5f, series.Ago(11));

        Assert.Equal(3, new Series(history, 5, 3).Count);
    }

    [Fact]
    public void TheSourceKeepsTheWidestGraphAtTheSlowestPace()
    {
        var source = new PerformanceData(Metric.Cpu, "", counting: false);
        for (var i = 0; i < PerformanceData.Kept + 7; i++)
        {
            source.Feed(0.5f);
        }

        Assert.Equal(PerformanceData.Samples, source.Paced(PerformanceData.SlowestPace).Count);
        Assert.Equal(PerformanceData.Samples, source.Paced(1).Count);
        // A pace out of range is the nearest one there is.
        Assert.Equal(PerformanceData.SlowestPace, source.Paced(60).Pace);
        Assert.Equal(1, source.Paced(0).Pace);
    }

    // --- The history ---------------------------------------------------------------------------------

    [Fact]
    public void TheHistoryIsALongEnoughRingThatWraps()
    {
        var history = new History(PerformanceData.Samples);

        for (var i = 0; i < PerformanceData.Samples + 88; i++)
        {
            history.Add(i);
        }

        Assert.Equal(PerformanceData.Samples, history.Count);
        Assert.Equal(PerformanceData.Samples + 87, history.Ago(0));
        Assert.Equal(88, history.Ago(PerformanceData.Samples - 1));
        for (var age = 1; age < history.Count; age++)
        {
            Assert.Equal(history.Ago(age - 1) - 1, history.Ago(age));
        }
    }

    [Fact]
    public void ARingNotYetFullCountsWhatItHasAndAClearEmptiesIt()
    {
        var history = new History(8);
        history.Add(1);
        history.Add(2);

        Assert.Equal(2, history.Count);
        Assert.Equal(2, history.Ago(0));
        Assert.Equal(1, history.Ago(1));
        history.Clear();
        Assert.Equal(0, history.Count);
    }

    [Fact]
    public void TheFlyoutsMinuteIsSixtySecondsOfTheOneSource()
    {
        Assert.Equal(61, PerformanceModel.Samples);
        Assert.Equal(S.WidgetPerformanceSeconds(seconds: 60), PerformanceModel.Span);
        Assert.True(PerformanceData.Samples >= PerformanceModel.Samples);
    }

    [Fact]
    public void AFeedAddsToTheHistoryOfAStandIn()
    {
        var source = new PerformanceData(Metric.Cpu, "", counting: false);

        source.Feed(0.1f);
        source.Feed(0.2f);
        source.Feed();

        Assert.Equal(2, source.History.Count);
        Assert.Equal(0.2f, source.History.Ago(0));
    }

    // --- The click table ---------------------------------------------------------------------------

    /// <summary>
    /// A face set to a disk of its own wants that disk's source from the dock's start, as a line set
    /// so does; at its default it is the reading the dock always runs, and adds none. Its pace adds
    /// none either: the pace is how often it is painted.
    /// </summary>
    [Fact]
    public void AFaceWantsTheSourceOfItsDeviceAndNothingForItsPace()
    {
        var sources = Registry();
        var face = FolderFace.Of(FolderFaces.DiskId);
        face.Set(FolderFaces.Disk.Option(PerformanceFace.Pace)!, "5");
        sources.Want(new Config { Layouts = [new TaskbarLayout { Id = "main", Name = "main", Islands = [Folder(face)] }], DefaultLayoutId = "main" });
        Assert.Equal(Metrics.Length, sources.Running.Count);

        face.Set(FolderFaces.Disk.Option(PerformanceFace.Disk)!, "disk 1");
        sources.Want(new Config { Layouts = [new TaskbarLayout { Id = "main", Name = "main", Islands = [Folder(face)] }], DefaultLayoutId = "main" });

        Assert.Equal(Metrics.Length + 1, sources.Running.Count);
        Assert.NotNull(sources.Find(PerformanceData.KeyOf(Metric.Disk, new PerformanceOptions { Disk = "Disk 1" })));
    }

    /// <summary>The five readings have no taskbar entry: on the bar each is its preset's face (§97).</summary>
    [Fact]
    public void NoTaskbarEntryIsAReading()
    {
        Assert.DoesNotContain(BarEntries.All, entry => entry.Info.Metric is not null);
        foreach (var metric in Metrics)
        {
            Assert.Equal(metric, PerformanceFace.MetricOf(PerformanceFace.Of(metric)));
        }
    }

    [Theory]
    [InlineData(ListKind.Cpu)]
    [InlineData(ListKind.Gpu)]
    [InlineData(ListKind.Memory)]
    [InlineData(ListKind.Disk)]
    [InlineData(ListKind.Network)]
    public void ALineKeepsItsActAndOpensNothing(ListKind kind)
    {
        Assert.Equal(CellAct.Action, ListWidgets.Of(kind).Act);
    }

    // --- The words ---------------------------------------------------------------------------------

    private static PerformanceData Stand(Metric metric, string device = "") =>
        new(metric, device, counting: false);

    private static string[] Words(PerformanceModel model) =>
        [.. model.Facts.Select(fact => $"{fact.Label}: {fact.Value}")];

    [Fact]
    public void TheProcessorSaysItsLoadAndWhatTheSystemHolds()
    {
        var source = Stand(Metric.Cpu);
        source.Feed(0.234f);
        var figures = new PerformanceFigures
        {
            System = new SystemReading(412, 5120, 190_000, 0, 0, 0),
            Uptime = new TimeSpan(3, 4, 12, 45),
            BaseMhz = 3400,
            Logical = 16,
        };

        var model = PerformanceModel.For(source, figures);

        Assert.Equal(S.WidgetPerformanceProcessor, model.Title);
        Assert.Equal("23%", model.Value);
        Assert.True(model.Known);
        Assert.Equal(
            [
                "Utilization: 23%",
                "Base speed: 3.40 GHz",
                "Logical processors: 16",
                "Processes: 412",
                "Threads: 5120",
                "Handles: 190000",
                "Up time: 3:04:12:45",
            ],
            Words(model));
    }

    [Fact]
    public void WhatTheSystemWillNotSayIsLeftOutRatherThanShownAsZero()
    {
        var source = Stand(Metric.Cpu);
        source.Feed(0.5f);

        var model = PerformanceModel.For(source, new PerformanceFigures());

        Assert.Equal(["Utilization: 50%"], Words(model));
    }

    [Fact]
    public void TheMemorySaysInUseAvailableCommittedAndCached()
    {
        const ulong gb = 1024UL * 1024 * 1024;
        var source = Stand(Metric.Memory);
        source.Feed(0.58f, memory: new MemoryReading(58, 32 * gb, 13 * gb));
        var figures = new PerformanceFigures { System = new SystemReading(1, 1, 1, 40 * gb, 64 * gb, 6 * gb) };

        var model = PerformanceModel.For(source, figures);

        Assert.Equal(S.WidgetPerformanceMemory, model.Title);
        Assert.Equal("58%", model.Value);
        Assert.Equal(
            [
                "In use: 19.0 of 32.0 GB",
                "Available: 13.0 GB",
                "Committed: 40.0 of 64.0 GB",
                "Cached: 6.0 GB",
            ],
            Words(model));
    }

    /// <summary>One card, the one it follows, never a row for each card the PC has (2026-10-11).</summary>
    [Fact]
    public void TheGraphicsSaysItsLoadAndTheOneAdapterItFollows()
    {
        var source = Stand(Metric.Gpu);
        source.Feed(0.07f);

        var one = PerformanceModel.For(source, new PerformanceFigures { Graphics = "NVIDIA GeForce RTX 4070" });
        var gone = PerformanceModel.For(source, new PerformanceFigures());

        Assert.Equal("7%", one.Value);
        Assert.Equal(["Utilization: 7%", "Adapter: NVIDIA GeForce RTX 4070"], Words(one));
        Assert.Equal(["Utilization: 7%"], Words(gone));
    }

    [Fact]
    public void TheDiskSaysItsActiveTimeAndTheSpeedsItReadsAndWrites()
    {
        var source = Stand(Metric.Disk);
        source.Feed(0.07f, read: 1.5 * 1024 * 1024, written: 300 * 1024);

        var model = PerformanceModel.For(source, new PerformanceFigures { DiskTitle = "Disk 1 (C:)" });

        Assert.Equal("Disk 1 (C:)", model.Title);
        Assert.Equal("7%", model.Value);
        Assert.Equal(["Active time: 7%", "Read speed: 1.5 MB/s", "Write speed: 300 KB/s"], Words(model));
        Assert.Equal(S.WidgetPerformanceDisk, PerformanceModel.For(source, new PerformanceFigures()).Title);
    }

    [Fact]
    public void TheNetworkSaysSendReceiveAndTheAdapter()
    {
        var source = Stand(Metric.Network);
        source.Feed(reading: 0, down: 2 * 1024 * 1024, up: 512 * 1024);

        var model = PerformanceModel.For(source, new PerformanceFigures { Adapter = "Intel(R) Wi-Fi 6 AX201 160MHz" });

        Assert.Equal(S.WidgetPerformanceNetwork, model.Title);
        // Down and up together, which is what the graph plots.
        Assert.Equal(Units.Rate((2 * 1024 * 1024) + (512 * 1024)), model.Value);
        Assert.Equal(["Send: 512 KB/s", "Receive: 2.0 MB/s", "Adapter: Intel(R) Wi-Fi 6 AX201 160MHz"], Words(model));
    }

    /// <summary>
    /// The graph's top says what it stands for: a hundred per cent for a share, and for the network
    /// the busiest moment of the minute drawn rounded up to a power of two, never under the floor
    /// that keeps chatter a ripple.
    /// </summary>
    [Fact]
    public void TheGraphsTopIsAHundredPercentOrTheMinutesBusiestRate()
    {
        var cpu = Stand(Metric.Cpu);
        cpu.Feed(0.2f);
        Assert.Equal("100%", PerformanceModel.For(cpu, new PerformanceFigures()).Top);

        var network = Stand(Metric.Network);
        network.Feed(down: 1024, up: 0);
        Assert.Equal(Units.Rate(Karpo.Dock.Graphs.Sparkline.NetworkFloor), PerformanceModel.For(network, new PerformanceFigures()).Top);
        network.Feed(down: 6 * 1024 * 1024, up: 1024 * 1024);
        for (var second = 0; second < PerformanceModel.Samples - 1; second++)
        {
            network.Feed(down: 1024, up: 0);
        }
        Assert.Equal(Units.Rate(8 * 1024 * 1024), PerformanceModel.For(network, new PerformanceFigures()).Top);
        // A minute later the burst has scrolled out, and the scale with it.
        network.Feed(down: 1024, up: 0);
        Assert.Equal(Units.Rate(Karpo.Dock.Graphs.Sparkline.NetworkFloor), PerformanceModel.For(network, new PerformanceFigures()).Top);
    }

    [Theory]
    [InlineData(Metric.Cpu)]
    [InlineData(Metric.Gpu)]
    [InlineData(Metric.Memory)]
    [InlineData(Metric.Disk)]
    [InlineData(Metric.Network)]
    public void ABeforeAnyReadingTheFigureIsADashAndNothingIsInvented(Metric metric)
    {
        var model = PerformanceModel.For(Stand(metric), new PerformanceFigures());

        Assert.False(model.Known);
        Assert.Equal(PerformanceModel.Unknown, model.Value);
        Assert.DoesNotContain(model.Facts, fact => fact.Value is "0%" or "0 KB/s");
    }

    [Theory]
    [InlineData(0, "0:00:00:00")]
    [InlineData(59, "0:00:00:59")]
    [InlineData(3600 + 60 + 1, "0:01:01:01")]
    [InlineData((3 * 86400) + (4 * 3600) + (12 * 60) + 45, "3:04:12:45")]
    [InlineData(-5, "0:00:00:00")]
    public void TheTimeTheSystemHasRunIsWrittenAsTaskManagerWritesIt(int seconds, string expected)
    {
        Assert.Equal(expected, PerformanceModel.Uptime(TimeSpan.FromSeconds(seconds)));
    }

    [Theory]
    [InlineData(3400, "3.40 GHz")]
    [InlineData(2995, "3.00 GHz")]
    [InlineData(800, "0.80 GHz")]
    public void AClockIsWrittenInGigahertzToTwoPlaces(int mhz, string expected)
    {
        Assert.Equal(expected, PerformanceModel.Clock(mhz));
    }
}
