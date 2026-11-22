// The widgets' data, gathered from the dock's start (Karpo.Dock\Sources\DataSources.cs).
//
// The person, 2026-09-27: "widgets like the processor display / graph only get initialized when i
// open the folder. they should get initialized on start so that they can already show the
// accumulated historical data. this should go for all widgets". What that asks of the data, held
// here without a window: a source for every widget the config names — in a folder nobody opened,
// in a layout no display shows — gathered from the start at its own interval; one source per kind
// of data, whose history every widget of it draws; and a source no widget needs any more stopped.
//
// The registry under test is one of the test's own, whose performance sources read numbers the
// test chose rather than this PC's counters, so N ticks are N samples whatever the machine is
// doing, and nothing here reaches another test's sources.

using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Islands;

namespace Karpo.Tests.Sources;

public sealed class DataSourcesTests
{
    /// <summary>A registry whose performance sources read 0.5 and whose other sources stand in (<see cref="Standing"/>).</summary>
    private static DataSources Registry(List<DataSource>? made = null) => new((key, widget) =>
    {
        DataSource source = widget.Info?.Metric is { } metric
            ? new PerformanceData(metric, widget.Performance?.GpuAdapter ?? "") { Reading = () => 0.5f }
            : new Standing(key);
        made?.Add(source);
        return source;
    });

    /// <summary>A source of any other kind: counts its start and its stop, and reads nothing.</summary>
    private sealed class Standing(SourceKey key) : DataSource(key)
    {
        public int Started { get; private set; }

        public bool Stopped { get; private set; }

        public override int IntervalMs => 60_000;

        protected internal override void Start() => Started++;

        protected override bool Gather(long now) => false;

        public override void Dispose() => Stopped = true;
    }

    private static CellItem Line(ListKind kind, string id)
    {
        var line = kind.New();
        line.Id = id;
        return line;
    }

    private static BarItem Entry(BarKind kind, string id)
    {
        var entry = kind.New();
        entry.Id = id;
        return entry;
    }

    /// <summary>A folder on the bar wearing the processor's face (docs\widgets.md §97), which reads the processor.</summary>
    private static BarItem Processing(string id)
    {
        var folder = BarKind.FolderList.New();
        folder.Id = id;
        folder.Folder!.Face = FolderFace.Of(FolderFaces.CpuId);
        return folder;
    }

    /// <summary>A folder of <paramref name="kind"/> holding <paramref name="widgets"/>, one line each in its list.</summary>
    private static BarItem Folder(BarKind kind, string id, params CellItem[] widgets)
    {
        var folder = kind.New();
        folder.Id = id;
        folder.Folder!.Lists = CellItems.OneList(widgets);
        return folder;
    }

    private static TaskbarLayout Layout(string id, params BarItem[] islands) => new() { Id = id, Name = id, Islands = [.. islands] };

    /// <summary>A config of <paramref name="layouts"/>, the first of them the one every display shows.</summary>
    private static Config Of(params TaskbarLayout[] layouts) => new() { Layouts = [.. layouts], DefaultLayoutId = layouts[0].Id };

    private static readonly SourceKey Processor = PerformanceData.KeyOf(Metric.Cpu, new PerformanceOptions());

    /// <summary>
    /// A graph in a folder that has never opened is gathered all the same: the config names it, so
    /// its source runs from the start, and after N ticks of the dock's clock — one an interval — its
    /// history holds N samples. No folder is made and nothing is drawn: the history is waiting for
    /// the folder when it opens.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(PerformanceData.Kept + 30)]
    public void AFoldersWidgetIsGatheredBeforeTheFolderHasEverOpened(int ticks)
    {
        var sources = Registry();
        sources.Want(Of(Layout("main", Folder(BarKind.FolderList, "f", Line(ListKind.Cpu, "cpu")))));

        for (var tick = 0; tick < ticks; tick++)
        {
            sources.Tick(10_000 + (tick * 1000L));
        }

        var cpu = Assert.IsType<PerformanceData>(sources.Find(Processor));
        Assert.Equal(Math.Min(ticks, PerformanceData.Kept), cpu.History.Count);
        Assert.Equal(ticks, cpu.Gathered);
        Assert.Equal(0.5f, cpu.History.Ago(0));
    }

    /// <summary>
    /// A source gathers once an interval however many ask: the dock's timer and every widget of it
    /// that ticks (Catch) share one sample, and an ask before the interval has passed reads nothing.
    /// </summary>
    [Fact]
    public void ASourceGathersOnceAnIntervalHoweverManyAsk()
    {
        var sources = Registry();
        sources.Want(Of(Layout("main", Processing("cpu"))));
        var cpu = Assert.IsType<PerformanceData>(sources.Find(Processor));

        sources.Tick(1_000);
        cpu.Catch(1_000);
        cpu.Catch(1_400);
        sources.Tick(1_500);
        Assert.Equal(1, cpu.History.Count);

        // The next interval, less the slack a timer's jitter is allowed.
        sources.Tick(1_900, slack: 250);
        cpu.Catch(1_950, slack: 250);
        Assert.Equal(2, cpu.History.Count);
    }

    /// <summary>
    /// Two widgets of one kind read one source and draw one history: a graph on the bar and its
    /// lines in two list folders, at any pace, are one source — made
    /// once, gathered once a tick, and handed to every widget that asks for it.
    /// </summary>
    [Fact]
    public void TwoWidgetsOfOneKindShareOneSourceAndOneHistory()
    {
        var made = new List<DataSource>();
        var sources = Registry(made);
        var bar = Processing("bar");
        var card = Line(ListKind.Cpu, "card");
        var line = Line(ListKind.Cpu, "line");
        sources.Want(Of(Layout("main", bar, Folder(BarKind.FolderList, "g", card), Folder(BarKind.FolderList, "l", line))));

        Assert.Single(made);
        Assert.Equal([Processor], sources.Running);
        var fromBar = sources.Hold(SourceKinds.FaceLine(bar.Folder!.Face)!);
        var fromCard = sources.Hold(card);
        var fromLine = sources.Hold(line);
        Assert.Same(fromBar, fromCard);
        Assert.Same(fromBar, fromLine);

        sources.Tick(5_000);
        var history = Assert.IsType<PerformanceData>(fromBar).History;
        Assert.Same(history, Assert.IsType<PerformanceData>(fromCard).History);
        Assert.Equal(1, history.Count);

        // Another pace is the same source drawn a step every so many samples.
        var slow = Line(ListKind.Cpu, "slow");
        slow.Performance!.Seconds = 5;
        Assert.Same(fromBar, sources.Hold(slow));
        Assert.Single(made);
    }

    /// <summary>
    /// Every kind of data has its one key, asked of what a widget is and how it is set, never of
    /// where it stands: the weather's widget and list line for one place are one source, and
    /// so are every button of one switch, every security widget and every volume slider.
    /// </summary>
    [Fact]
    public void EveryKindOfDataHasOneKey()
    {
        WeatherOptions Zurich() => new() { Place = "Zurich", Latitude = 47.37, Longitude = 8.54 };
        var bar = Line(ListKind.WeatherDays, "w1");
        bar.Weather = Zurich();
        var row = Line(ListKind.WeatherNow, "w2");
        row.Weather = Zurich();
        Assert.NotNull(SourceKinds.KeyOf(bar));
        Assert.Equal(SourceKinds.KeyOf(bar), SourceKinds.KeyOf(row));

        // No place set: nothing to fetch, so no source. A new line is the device's location, one source.
        var placeless = Line(ListKind.WeatherHours, "w3");
        Assert.NotNull(SourceKinds.KeyOf(placeless));
        placeless.Weather = new WeatherOptions();
        Assert.Null(SourceKinds.KeyOf(placeless));

        Assert.Equal(SourceKinds.KeyOf(Entry(BarKind.AirplaneMode, "a1")), SourceKinds.KeyOf(Line(ListKind.AirplaneMode, "a2")));
        Assert.NotEqual(SourceKinds.KeyOf(Entry(BarKind.AirplaneMode, "a1")), SourceKinds.KeyOf(Entry(BarKind.NightLight, "n1")));
        Assert.Equal(SourceKinds.KeyOf(Entry(BarKind.Security, "s1")), SourceKinds.KeyOf(Line(ListKind.Security, "s2")));
        Assert.Equal(SourceKinds.KeyOf(Entry(BarKind.Volume, "v1")), SourceKinds.KeyOf(Entry(BarKind.Volume, "v2")));
        Assert.Equal(SourceKinds.KeyOf(Line(ListKind.SoundOutput, "o1")), SourceKinds.KeyOf(Line(ListKind.SoundMixer, "m1")));

        // The accounts' widgets — the calendar's lines, the mail, the contacts — have no source: the
        // dock syncs every account from its start, whatever the config names (PimSync, 2026-10-29).
        foreach (var kind in new[] { ListKind.Calendar, ListKind.Events, ListKind.Month, ListKind.DayView, ListKind.WeekView, ListKind.YearView, ListKind.Mail, ListKind.Contacts })
        {
            Assert.Null(SourceKinds.KeyOf(Line(kind, "c3")));
        }
        Assert.Null(SourceKinds.KeyOf(Line(ListKind.DateHeader, "c4")));

        // What gathers nothing has no source.
        Assert.Null(SourceKinds.KeyOf(Entry(BarKind.Start, "start")));
    }

    /// <summary>
    /// The last widget of a kind taken out of the config stops its source; one still named keeps it,
    /// and a source a widget still holds — an island the dock has not disposed yet — stops when the
    /// widget lets it go.
    /// </summary>
    [Fact]
    public void RemovingTheLastWidgetOfAKindStopsItsSource()
    {
        var made = new List<DataSource>();
        var sources = Registry(made);
        var security = Entry(BarKind.Security, "shield");
        sources.Want(Of(Layout("main", Processing("one"), Folder(BarKind.FolderList, "f", Line(ListKind.Cpu, "two")), security)));
        var shield = Assert.IsType<Standing>(sources.Find(SourceKinds.KeyOf(security)!.Value));
        Assert.Equal(1, shield.Started);

        // One graph of the two goes: the other still needs the processor.
        sources.Want(Of(Layout("main", Folder(BarKind.FolderList, "f", Line(ListKind.Cpu, "two")), security)));
        Assert.Contains(Processor, sources.Running);

        // The last goes, and so does its source.
        sources.Want(Of(Layout("main", security)));
        Assert.DoesNotContain(Processor, sources.Running);
        Assert.Null(sources.Find(Processor));

        // A widget still holding its source keeps it past the config, until it lets it go.
        var held = sources.Hold(security);
        Assert.Same(shield, held);
        sources.Want(Of(Layout("main")));
        Assert.False(shield.Stopped);
        Assert.Contains(shield.Key, sources.Running);
        sources.Release(held);
        Assert.True(shield.Stopped);
        Assert.Empty(sources.Running);
    }

    /// <summary>
    /// A new widget's source starts when the widget appears in the config — at the next change of it,
    /// with nothing drawn — and a widget in a layout no display shows has its source like any other:
    /// switching a display to that layout later draws what was gathered meanwhile.
    /// </summary>
    [Fact]
    public void AWidgetInALayoutNoDisplayShowsHasItsSource()
    {
        var sources = Registry();
        var shown = Layout("shown", Entry(BarKind.TouchKeyboard, "timer"));
        sources.Want(Of(shown));
        Assert.Empty(sources.Running);

        var hidden = Layout("review", Folder(BarKind.FolderList, "l", Line(ListKind.Gpu, "gpu"), Line(ListKind.Memory, "memory")));
        var config = Of(shown, hidden);
        // No display shows "review": every display takes the default layout, "shown".
        Assert.DoesNotContain(config.Screens, screen => screen.ActiveLayoutId == "review");
        sources.Want(config);

        var gpu = PerformanceData.KeyOf(Metric.Gpu, new PerformanceOptions());
        var memory = PerformanceData.KeyOf(Metric.Memory, new PerformanceOptions());
        Assert.Equal([gpu, memory], sources.Running.OrderBy(key => key.ToString(), StringComparer.Ordinal));
        sources.Tick(1_000);
        sources.Tick(2_000);
        Assert.Equal(2, Assert.IsType<PerformanceData>(sources.Find(gpu)).History.Count);
    }

    /// <summary>
    /// A graph made for the first time — a folder opening, a layout shown — draws the history its
    /// source already gathered, and two graphs of one reading draw the one history. A test that
    /// feeds a graph gives it a stand-in of its own, which reaches no other graph.
    /// </summary>
    [UiFact]
    public void AGraphDrawnForTheFirstTimeDrawsTheHistoryAlreadyGathered()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var config = new Config();
            var sources = Registry();
            var card = Line(ListKind.Cpu, "card");
            var line = Line(ListKind.Cpu, "line");
            sources.Want(Of(Layout("main", Folder(BarKind.FolderList, "g", card), Folder(BarKind.FolderList, "l", line))));
            for (var tick = 0; tick < 9; tick++)
            {
                sources.Tick(1_000 * (tick + 1));
            }

            using var first = new PerformanceIsland(Config.IslandKey(null, "g/card"), card, gfx, config, sources);
            using var second = new PerformanceIsland(Config.IslandKey(null, "l/line"), line, gfx, config, sources);
            Assert.Same(sources.Find(Processor), first.Source);
            Assert.Same(first.Source, second.Source);
            Assert.Equal(9, first.Source.History.Count);

            // Fed, a graph is its own: what the test hands it never reaches the other.
            first.Feed(0.9f);
            Assert.NotSame(first.Source, second.Source);
            Assert.Equal(1, first.Source.History.Count);
            Assert.Equal(9, second.Source.History.Count);
        });
    }

    /// <summary>
    /// Every button of one switch reads one switch, started once for all of them and followed since
    /// the start: sixteen hotspot buttons are one look at the network stack, not sixteen. The same
    /// for Windows' security state and the output volume.
    /// </summary>
    [UiFact]
    public void EveryButtonOfOneSwitchReadsOneSwitch()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var config = new Config();
            var sources = new DataSources(SourceKinds.Make);
            var buttons = Enumerable.Range(0, 3).Select(index => new SwitchIsland(Config.IslandKey(null, $"a{index}"), Entry(BarKind.AirplaneMode, $"a{index}"), gfx, config, sources)).ToList();
            var shields = Enumerable.Range(0, 2).Select(index => new SecurityIsland(Config.IslandKey(null, $"s{index}"), Entry(BarKind.Security, $"s{index}"), gfx, config, sources)).ToList();
            try
            {
                var airplane = Assert.IsType<SwitchData>(sources.Find(SwitchData.KeyOf(QuickSwitch.AirplaneMode)));
                Assert.Equal(3, airplane.Listening);
                var security = Assert.IsType<SecurityData>(sources.Find(SecurityData.KeyOf()));
                Assert.Equal(2, security.Listening);
                Assert.Equal(2, sources.Running.Count);
            }
            finally
            {
                buttons.ForEach(button => button.Dispose());
                shields.ForEach(shield => shield.Dispose());
            }
            Assert.Empty(sources.Running);
        });
    }
}
