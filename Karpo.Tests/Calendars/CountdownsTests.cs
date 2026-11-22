// Every timer and every focus session is a countdown of its own (Karpo.Dock\Clock\Countdown.cs,
// Countdowns): "the timer has only one store. but you should be able to add many different timers
// with their own default times", and "same for focus time" (the person, 2026-09-27). Each widget's
// countdown is kept by its id in its dock's store and runs, pauses, ends and rings on its own; its
// length and its name are its own settings (CountdownOptions), which the config keeps, a move
// between folders carries, and a step on its face sets for it alone; a widget removed takes its
// countdown with it.
// The clock's two panes are the clock's own. And the store is a dock's, never the whole process's.
//
// The pure half runs everywhere; what draws or holds a widget runs with KARPO_UI_TESTS=1.

using System.Numerics;
using System.Reflection;
using Karpo.Dock;
using Karpo.Tests.Core;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Calendars;

public sealed unsafe class CountdownsTests
{
    private static readonly DateTime Start = new(2026, 9, 27, 9, 0, 0, DateTimeKind.Local);

    private static TimeSpan Minutes(double minutes) => TimeSpan.FromMinutes(minutes);

    private static Look LookFor(bool dark) => new(
        Glass: false,
        Dark: dark,
        Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4),
        AccentLight2: Color.FromArgb(255, 0x99, 0xEB, 0xFF),
        AccentDark1: Color.FromArgb(255, 0x00, 0x5F, 0xB8));

    // --- Each its own ------------------------------------------------------------------

    /// <summary>
    /// Two timers started together run apart: one paused, the other keeps going; each ends at its own
    /// time, and each rings once — the one paused rings when it runs out after it is taken up again.
    /// </summary>
    [Fact]
    public void TwoTimersRunEachOnTheirOwn()
    {
        var countdowns = new Countdowns();
        var now = Start;
        var ends = new List<(string Key, DateTime At)>();
        countdowns.Ended += (key, _) => ends.Add((key, now));

        countdowns.Act("a", CountdownKind.Timer, Minutes(5), now);
        countdowns.Act("b", CountdownKind.Timer, Minutes(10), now);
        Assert.Equal(CountdownPhase.Running, countdowns.Of("a").Phase);
        Assert.Equal(CountdownPhase.Running, countdowns.Of("b").Phase);

        // One paused: the other keeps going.
        now = Start.AddMinutes(1);
        countdowns.Act("a", CountdownKind.Timer, Minutes(5), now);
        now = Start.AddMinutes(3);
        Assert.True(countdowns.Tick(now));
        Assert.Equal((CountdownPhase.Paused, Minutes(4)), (countdowns.Of("a").Phase, countdowns.Of("a").Remaining(now)));
        Assert.Equal((CountdownPhase.Running, Minutes(7)), (countdowns.Of("b").Phase, countdowns.Of("b").Remaining(now)));

        // Each ends at its own time, and rings once.
        for (now = Start.AddMinutes(3); now <= Start.AddMinutes(11); now = now.AddSeconds(30))
        {
            _ = countdowns.Tick(now);
        }
        Assert.Equal([("b", Start.AddMinutes(10))], ends);
        Assert.Equal(CountdownPhase.Paused, countdowns.Of("a").Phase);
        now = Start.AddMinutes(11);
        countdowns.Act("a", CountdownKind.Timer, Minutes(5), now);
        for (; now <= Start.AddMinutes(20); now = now.AddSeconds(30))
        {
            _ = countdowns.Tick(now);
        }
        Assert.Equal([("b", Start.AddMinutes(10)), ("a", Start.AddMinutes(15))], ends);
        Assert.Equal((CountdownPhase.Done, CountdownPhase.Done), (countdowns.Of("a").Phase, countdowns.Of("b").Phase));
        Assert.False(countdowns.Ticking);
    }

    /// <summary>
    /// Each that runs out rings once, the first one too; several running out in the same second ring
    /// once between them, and one a second later rings again.
    /// </summary>
    [Fact]
    public void EachRingsOnceAndTogetherTheyRingOnce()
    {
        var rings = 0;
        var ringer = new Ringer(() => rings++);
        Assert.True(ringer.Ended(0));
        Assert.Equal(1, rings);
        Assert.False(ringer.Ended(400));
        Assert.False(ringer.Ended(999));
        Assert.Equal(1, rings);
        Assert.True(ringer.Ended(1000));
        Assert.True(ringer.Ended(60_000));
        Assert.Equal(3, rings);
        Assert.True(new Ringer(() => rings++).Ended(long.MaxValue));
    }

    /// <summary>A timer and a focus session run at the same time, and stopping one leaves the other running.</summary>
    [Fact]
    public void ATimerAndAFocusSessionRunAtOnce()
    {
        var countdowns = new Countdowns();
        countdowns.Act("t", CountdownKind.Timer, Minutes(5), Start);
        countdowns.Act("f", CountdownKind.Focus, Minutes(25), Start.AddMinutes(1));
        Assert.Equal((CountdownKind.Timer, CountdownPhase.Running), (countdowns.Of("t").Kind, countdowns.Of("t").Phase));
        Assert.Equal((CountdownKind.Focus, CountdownPhase.Running), (countdowns.Of("f").Kind, countdowns.Of("f").Phase));
        Assert.True(countdowns.Ticking);

        countdowns.Clear("t");
        Assert.True(countdowns.Of("t").Idle);
        Assert.Equal(CountdownPhase.Running, countdowns.Of("f").Phase);
        Assert.True(countdowns.Ticking);
    }

    /// <summary>Each starts at its own length: its widget's, through the source its faces read it by.</summary>
    [Fact]
    public void EachStartsAtItsOwnLength()
    {
        var countdowns = new Countdowns();
        (string Id, CountdownKind Kind, CountdownOptions Options)[] widgets =
        [
            ("tea", CountdownKind.Timer, new CountdownOptions { Minutes = 3, Name = "Tea" }),
            ("laundry", CountdownKind.Timer, new CountdownOptions { Minutes = 45 }),
            ("deep", CountdownKind.Focus, new CountdownOptions { Minutes = 90, Name = "Deep work" }),
        ];
        foreach (var (id, kind, options) in widgets)
        {
            var widget = (kind == CountdownKind.Focus ? ListKind.Focus : ListKind.Timer).New();
            widget.Countdown = options;
            countdowns.Act(id, kind, CountdownTile.LengthOf(widget, kind), Start);
        }
        Assert.Equal([Minutes(3), Minutes(45), Minutes(90)], widgets.Select(widget => countdowns.Of(widget.Id).Length));
    }

    // --- Where it goes --------------------------------------------------------------------

    /// <summary>
    /// A timer carried into another list of its folder, as a folder's own drag carries it, keeps its
    /// id, and its running countdown is still there once the dock keeps only what the config holds;
    /// removed from the folder, it takes its countdown with it.
    /// </summary>
    [Fact]
    public void ACountdownFollowsItsWidgetIntoAnotherListAndGoesWithIt()
    {
        var timer = ListKind.Timer.New();
        timer.Countdown = new CountdownOptions { Minutes = 5, Name = "Tea" };
        var folder = Configs.FolderKind(FolderView.List).New();
        folder.Id = "into";
        var line = folder.Folder!.Put(timer);
        folder.Folder.Put(ListKind.Clock.New());
        var config = Configs.With(folder);
        var countdowns = new Countdowns();
        countdowns.Act(timer.Id, CountdownKind.Timer, Minutes(5), Start);

        Assert.True(Karpo.Core.Folders.Move(folder.Folder, line, FolderPlace.NewList(1)));
        countdowns.Keep(Countdowns.KeysIn(config));
        Assert.Equal(2, folder.Folder.Lists.Count);
        var cell = Assert.Single(folder.Folder.Lists[1]).Island;
        Assert.Equal((timer.Id, "Tea"), (cell.Id, cell.Countdown!.Name));
        Assert.Equal(CountdownPhase.Running, countdowns.Of(cell.Id).Phase);

        Karpo.Core.Folders.Remove(config.DefaultLayout.Islands, Karpo.Core.Folders.Path(folder.Id, timer.Id));
        countdowns.Keep(Countdowns.KeysIn(config));
        Assert.True(countdowns.Of(timer.Id).Idle);
        Assert.Empty(countdowns.Keys);
    }

    /// <summary>
    /// Removing a widget drops its countdown and no other: a timer gone takes its own, and the focus
    /// session in the clock's folder runs on; the folder gone takes it too.
    /// </summary>
    [Fact]
    public void RemovingAWidgetDropsItsCountdownAndNoOther()
    {
        var timer = ListKind.Timer.New();
        var clock = Presets.Folder(Presets.Find(Presets.ClockId)!, new PresetMachine(false, []));
        var focus = clock.Folder!.Cells.Single(cell => cell.Island.List == ListKind.Focus).Island;
        var holder = Configs.Holding(timer);
        var config = Configs.With(holder, clock);
        var countdowns = new Countdowns();
        countdowns.Act(timer.Id, CountdownKind.Timer, Minutes(5), Start);
        countdowns.Act(focus.Id, CountdownKind.Focus, Minutes(25), Start);
        var dropped = new List<string>();
        countdowns.Changed += key => dropped.Add(key);

        // What the config holds keeps its countdowns: nothing is dropped.
        countdowns.Keep(Countdowns.KeysIn(config));
        Assert.Empty(dropped);

        config.DefaultLayout.Islands.Remove(holder);
        countdowns.Keep(Countdowns.KeysIn(config));
        Assert.Equal([timer.Id], dropped);
        Assert.True(countdowns.Of(timer.Id).Idle);
        Assert.Equal(CountdownPhase.Running, countdowns.Of(focus.Id).Phase);

        config.DefaultLayout.Islands.Remove(clock);
        countdowns.Keep(Countdowns.KeysIn(config));
        Assert.True(countdowns.Of(focus.Id).Idle);
    }

    /// <summary>
    /// A config's keys are every timer's, focus session's and stopwatch's own, in every layout and
    /// folder — the clock preset's three among them, as the widgets they are — and nothing else.
    /// </summary>
    [Fact]
    public void AConfigsKeysAreItsTimersAndFocusSessions()
    {
        var clock = Presets.Folder(Presets.Find(Presets.ClockId)!, new PresetMachine(false, []));
        var timer = ListKind.Timer.New();
        var list = Configs.FolderKind(FolderView.List).New();
        var line = ListKind.Focus.New();
        list.Folder!.Put(line);
        var config = Configs.With(clock, Configs.Holding(timer), list);
        var inClock = clock.Folder!.Cells.Where(cell => cell.Island.Info?.Options == WidgetOptionsKind.Countdown || cell.Island.Info == WidgetInfos.Stopwatch).Select(cell => cell.Island.Id);
        Assert.Equal(3, inClock.Count());
        Assert.Equal(
            inClock.Concat([timer.Id, line.Id]).Order(StringComparer.Ordinal),
            Countdowns.KeysIn(config).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// The store is a dock's: nothing in the dock keeps one in a static, where the whole process would
    /// share it — which is what made two tests running at once see each other's countdowns — and the
    /// one countdown and its lengths file, which were, are gone.
    /// </summary>
    [Fact]
    public void TheStoreIsADocksNotTheProcesss()
    {
        var assembly = typeof(Countdowns).Assembly;
        var statics = assembly.GetTypes()
            .SelectMany(type => type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            .Where(field => field.FieldType == typeof(Countdowns))
            .Select(field => $"{field.DeclaringType}.{field.Name}");
        Assert.Empty(statics);
        Assert.Null(assembly.GetType("Karpo.Dock.ClockTimers"));
        Assert.Null(assembly.GetType("Karpo.Dock.ClockLengths"));
        Assert.NotSame(new Countdowns(), new Countdowns());
    }

    // --- On their faces --------------------------------------------------------------------

    /// <summary>
    /// Timer and focus widgets on one store: each starts at its own length; the wheel on one steps its
    /// own length alone, and the dock hears whose; a step sets nothing while it runs.
    /// </summary>
    [UiFact]
    public void EachWidgetStartsAtItsLengthAndStepsAlone()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var countdowns = new Countdowns();
            var stepped = new List<string>();
            countdowns.Stepped += key => stepped.Add(key);
            var widgets = new (string Id, ListKind Kind, int Minutes)[] { ("tea", ListKind.Timer, 3), ("eggs", ListKind.Timer, 12), ("deep", ListKind.Focus, 50) }
                .Select(each =>
                {
                    var item = each.Kind.New();
                    item.Id = each.Id;
                    item.Countdown = new CountdownOptions { Minutes = each.Minutes };
                    return (Item: item, Tile: new CountdownTile(Config.IslandKey(null, each.Id), item, gfx, new Config(), countdowns, () => Start));
                })
                .ToList();
            try
            {
                foreach (var (_, tile) in widgets)
                {
                    _ = tile.Click((int)CountdownTargetKind.Action);
                }
                Assert.Equal([Minutes(3), Minutes(12), Minutes(50)], widgets.Select(widget => countdowns.Of(widget.Item.Id).Length));
                Assert.Equal([CountdownKind.Timer, CountdownKind.Timer, CountdownKind.Focus], widgets.Select(widget => countdowns.Of(widget.Item.Id).Kind));

                var look = LookFor(true);
                var style = FolderFlyout.StyleFor(1f, look, FlyoutTheme.For(look));
                var box = PopupPaint.Rect(0, 0, 360, 2 * 64);
                var middle = new Vector2(box.right / 2, box.bottom / 2);

                // Running, the wheel sets nothing.
                Assert.False(widgets[0].Tile.WheelOn(middle, box, style, WidgetSurface.List, 1));
                Assert.Empty(stepped);

                // Stopped, it steps its own length and no other.
                foreach (var (_, tile) in widgets)
                {
                    tile.Stop();
                }
                Assert.True(widgets[0].Tile.WheelOn(middle, box, style, WidgetSurface.List, 1));
                Assert.Equal([4, 12, 50], widgets.Select(widget => widget.Item.Countdown!.Minutes));
                Assert.Equal(["tea"], stepped);
                Assert.True(widgets[2].Tile.WheelOn(middle, box, style, WidgetSurface.List, -1));
                Assert.Equal([4, 12, 49], widgets.Select(widget => widget.Item.Countdown!.Minutes));
                Assert.Equal(["tea", "deep"], stepped);
            }
            finally
            {
                foreach (var (_, tile) in widgets)
                {
                    tile.Dispose();
                }
            }
        });
    }

    /// <summary>
    /// What a menu asks — a folder's right-click on a line too short for its buttons — goes through the
    /// widget's own two presses, on its own countdown: its start button (start, pause, take up again)
    /// and the button that clears it; and the phase a menu reads the start button's word by.
    /// </summary>
    [UiFact]
    public void AMenuPressesItsOwnButtons()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var countdowns = new Countdowns();
            var now = Start;
            var item = ListKind.Focus.New();
            item.Id = "deep";
            using var tile = new CountdownTile(Config.IslandKey(null, "deep"), item, gfx, new Config(), countdowns, () => now);
            Assert.Equal(CountdownPhase.Idle, tile.Phase);
            tile.Act();
            Assert.Equal((CountdownPhase.Running, Minutes(CountdownOptions.FocusMinutes)), (tile.Phase, countdowns.Of("deep").Length));
            now = now.AddMinutes(5);
            tile.Act();
            Assert.Equal(CountdownPhase.Paused, tile.Phase);
            tile.Act();
            Assert.Equal(CountdownPhase.Running, tile.Phase);
            Assert.Equal(Minutes(20), countdowns.Of("deep").Remaining(now));
            tile.Stop();
            Assert.Equal(CountdownPhase.Idle, tile.Phase);
            Assert.Empty(countdowns.Keys);
        });
    }

    /// <summary>
    /// A folder whose timer rang while it was shut wears a mark on its button — an accent dot on the
    /// glyph's corner, inside the cell — until it is opened: nothing in a folder opens a popup, so the
    /// mark is how the bar says which folder rang. The folder knows its own widgets by id.
    /// </summary>
    [UiFact]
    public void AFolderIsMarkedForWhatRangInItUntilItIsOpened()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var config = new Config();
            var folder = Configs.FolderKind(FolderView.List).New();
            folder.Id = "glance";
            var timer = ListKind.Timer.New();
            timer.Id = "t";
            folder.Folder!.Put(timer);
            using var island = new FolderIsland($"layout:default/{folder.Id}", folder, gfx, config, new Countdowns());
            Assert.True(island.Holds("t"));
            Assert.False(island.Holds("other"));
            Assert.False(island.Due);

            var style = new IslandStyle(1f, false, Layout.CellPx(96, new Config { Height = 48 }), Layout.IconPx(96, new Config { Height = 48 }), LookFor(true));
            var (width, height) = island.CellSize(0, style);
            (float Left, float Top, float Right, float Bottom) Ink()
            {
                (float, float, float, float) ink = default;
                Offscreen.Bitmap(width, height, dc => island.PaintCell(dc, 0, style, width, height), bitmap =>
                    ink = Offscreen.InkOf(bitmap, (uint)MathF.Ceiling(width), (uint)MathF.Ceiling(height)));
                return ink;
            }
            var plain = Ink();
            Assert.True(island.SetDue(true));
            Assert.False(island.SetDue(true));
            var marked = Ink();
            Assert.True(marked.Top < plain.Top, $"the mark does not show over the glyph: {marked} against {plain}");
            Assert.True(marked.Top >= 0 && marked.Right <= width + 0.5f, "the mark runs out of the cell");

            island.Opened(gfx);
            Assert.False(island.Due);
            Assert.Equal(plain, Ink());
            island.Closed();
        });
    }
}

/// <summary>A timer's length and name, and a clock's two lengths, through a real save and load of config.json.</summary>
[Collection(ConfigFile.Collection)]
public sealed class CountdownSettingsFileTests : IDisposable
{
    public CountdownSettingsFileTests() => ConfigFile.Delete();

    public void Dispose() => ConfigFile.Delete();

    /// <summary>
    /// Lengths and names survive a save and a load, for a timer and a focus session on the bar, for a
    /// line of a list folder, and for a clock's own two; a clock or a timer nobody set writes no length
    /// at all.
    /// </summary>
    [Fact]
    public void LengthsAndNamesSurviveASaveAndALoad()
    {
        var tea = ListKind.Timer.New();
        tea.Countdown = new CountdownOptions { Minutes = 4, Name = "Tea" };
        var deep = ListKind.Focus.New();
        deep.Countdown = new CountdownOptions { Minutes = 90, Name = "Deep work" };
        var unset = ListKind.Timer.New();
        var list = Configs.FolderKind(FolderView.List).New();
        var bread = ListKind.Timer.New();
        bread.Countdown = new CountdownOptions { Minutes = CountdownOptions.MostMinutes, Name = "Bread" };
        list.Folder!.Put(bread);
        var config = Configs.With(Configs.Holding(tea), Configs.Holding(deep), list, Configs.Holding(unset));
        config.Save();

        var loaded = Configs.Islands(Config.Load()).Select(folder => Assert.Single(folder.Folder!.Cells).Island).ToList();
        Assert.Equal((4, "Tea"), (loaded[0].Countdown!.Minutes, loaded[0].Countdown!.Name));
        Assert.Equal((90, "Deep work"), (loaded[1].Countdown!.Minutes, loaded[1].Countdown!.Name));
        Assert.Equal((CountdownOptions.MostMinutes, "Bread"), (loaded[2].Countdown!.Minutes, loaded[2].Countdown!.Name));
        Assert.Null(loaded[3].Countdown);

        // A timer nobody set writes nothing more than it did.
        using var document = System.Text.Json.JsonDocument.Parse(ConfigFile.Text());
        var islands = document.RootElement.GetProperty("layouts")[0].GetProperty("islands");
        Assert.Null(Configs.Find(islands[3], "countdown"));
        Assert.Equal("Tea", Configs.Find(islands[0], "countdown")!.Value.GetProperty("name").GetString());
    }
}
