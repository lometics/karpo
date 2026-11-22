// The stopwatch as numbers (Karpo.Dock\Clock\Stopwatch.cs): its state machine — start, pause,
// resume, lap, reset — on a monotonic clock handed to it, the dock's store of them, and how its
// time is written. Nothing here draws.

using System.Collections.Immutable;
using Karpo.Dock;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Calendars;

public sealed class StopwatchTests
{
    private static TimeSpan S(double seconds) => TimeSpan.FromSeconds(seconds);

    /// <summary>Idle, it reads nought and has nothing to lap or reset; started, it counts from where it was started.</summary>
    [Fact]
    public void StartedItCountsFromTheStart()
    {
        var idle = StopwatchState.None;
        Assert.True(idle.Idle);
        Assert.Equal(TimeSpan.Zero, idle.Elapsed(S(500)));
        Assert.False(idle.CanLap);
        Assert.False(idle.CanReset);

        var running = idle.Act(S(100));
        Assert.Equal(StopwatchPhase.Running, running.Phase);
        Assert.Equal(S(12.34), running.Elapsed(S(112.34)));
        Assert.True(running.CanLap);
        Assert.True(running.CanReset);
    }

    /// <summary>Paused it holds what it had; taken up again it goes on from there, the pause not counted.</summary>
    [Fact]
    public void APauseIsNotCounted()
    {
        var paused = StopwatchState.Start(S(0)).Pause(S(10));
        Assert.Equal(StopwatchPhase.Paused, paused.Phase);
        Assert.Equal(S(10), paused.Elapsed(S(10)));
        Assert.Equal(S(10), paused.Elapsed(S(1000)));
        Assert.False(paused.CanLap);
        Assert.True(paused.CanReset);

        var resumed = paused.Act(S(60));
        Assert.Equal(StopwatchPhase.Running, resumed.Phase);
        Assert.Equal(S(15), resumed.Elapsed(S(65)));

        // Twice over: every pause is left out.
        var again = resumed.Pause(S(70)).Resume(S(200));
        Assert.Equal(S(30), again.Elapsed(S(210)));
    }

    /// <summary>The button's one rule: idle starts, running pauses, paused takes up again — round and round.</summary>
    [Fact]
    public void TheStartButtonGoesRound()
    {
        var state = StopwatchState.None;
        var phases = new List<StopwatchPhase>();
        for (var press = 0; press < 5; press++)
        {
            state = state.Act(S(press));
            phases.Add(state.Phase);
        }
        Assert.Equal([StopwatchPhase.Running, StopwatchPhase.Paused, StopwatchPhase.Running, StopwatchPhase.Paused, StopwatchPhase.Running], phases);
    }

    /// <summary>A lap is where it ended from the start, and how long it was: only while it runs, and no more than it keeps.</summary>
    [Fact]
    public void ALapEndsOnlyWhileItRuns()
    {
        var state = StopwatchState.Start(S(0)).Lap(S(10)).Lap(S(25.5));
        Assert.Equal([S(10), S(25.5)], state.Splits.ToArray());
        Assert.Equal((S(10), S(10)), state.LapAt(0));
        Assert.Equal((S(15.5), S(25.5)), state.LapAt(1));
        Assert.Null(state.LapAt(2));
        Assert.Null(state.LapAt(-1));

        // Paused, the lap button is greyed, and a lap pressed anyway is nothing.
        var paused = state.Pause(S(30));
        Assert.Equal(paused, paused.Lap(S(40)));
        Assert.Equal(StopwatchState.None, StopwatchState.None.Lap(S(1)));

        // It keeps a thousand less one, and takes no more.
        var full = StopwatchState.Start(S(0));
        for (var lap = 1; lap <= StopwatchState.MostLaps + 5; lap++)
        {
            full = full.Lap(S(lap));
        }
        Assert.Equal(StopwatchState.MostLaps, full.Splits.Length);
        Assert.False(full.CanLap);
    }

    /// <summary>
    /// The lap under way, what a line of one row shows beside its time: since the last lap ended, or
    /// since the start before the first; held while paused, back to nought on a reset. The person: "for
    /// the stopwatch the flag does not do anything (at least when shown one row high.)" (2026-10-11).
    /// </summary>
    [Fact]
    public void TheLapUnderWayRunsFromTheLastLap()
    {
        var running = StopwatchState.Start(S(100));
        Assert.Equal(S(4), running.CurrentLap(S(104)));
        var lapped = running.Lap(S(110)).Lap(S(117.5));
        Assert.Equal(S(2.5), lapped.CurrentLap(S(120)));
        var paused = lapped.Pause(S(121));
        Assert.Equal(S(3.5), paused.CurrentLap(S(500)));
        Assert.Equal(S(4.5), paused.Resume(S(600)).CurrentLap(S(601)));
        Assert.Equal(TimeSpan.Zero, StopwatchState.Reset().CurrentLap(S(700)));
        Assert.Equal(TimeSpan.Zero, StopwatchState.None.CurrentLap(S(700)));
        // A clock read before the lap ended is no lap under way of less than nought.
        Assert.Equal(TimeSpan.Zero, lapped.CurrentLap(S(117)));
    }

    /// <summary>Reset is nought and no laps, from any state; a paused stopwatch's laps go with it.</summary>
    [Fact]
    public void ResetIsNoughtAndNoLaps()
    {
        var lapped = StopwatchState.Start(S(0)).Lap(S(3)).Pause(S(5));
        Assert.Equal(StopwatchState.None, StopwatchState.Reset());
        var store = new Stopwatches();
        store.Set("w", lapped);
        store.Reset("w");
        Assert.True(store.Of("w").Idle);
        Assert.Empty(store.Of("w").Splits);
        Assert.Empty(store.Keys);
    }

    /// <summary>
    /// The clock it counts on is monotonic, and even handed one that went back it never reads less
    /// than nought, nor less than it had banked; and it stops at the most its figures hold.
    /// </summary>
    [Fact]
    public void ItNeverCountsBackwards()
    {
        var running = StopwatchState.Start(S(100));
        Assert.Equal(TimeSpan.Zero, running.Elapsed(S(50)));
        var resumed = running.Pause(S(110)).Resume(S(200));
        Assert.Equal(S(10), resumed.Elapsed(S(150)));
        Assert.Equal(StopwatchState.Longest, StopwatchState.Start(S(0)).Elapsed(TimeSpan.FromHours(200)));

        // The real clock moves forward between two reads.
        var first = StopwatchClock.Now();
        var second = StopwatchClock.Now();
        Assert.True(second >= first);
    }

    /// <summary>A state is equal by what it holds, its laps included, so the store raises Changed for a lap and for nothing else.</summary>
    [Fact]
    public void TheStoreSaysWhatChanged()
    {
        var store = new Stopwatches();
        var changed = new List<string>();
        store.Changed += changed.Add;
        store.Act("a", S(0));
        store.Lap("a", S(1));
        store.Lap("b", S(1));
        store.Set("a", store.Of("a") with { Laps = [.. store.Of("a").Splits] });
        Assert.Equal(["a", "a"], changed);
        Assert.True(store.Running);
        Assert.True(store.Of("b").Idle);

        store.Act("a", S(2));
        Assert.False(store.Running);
        Assert.Equal(S(2), store.Of("a").Elapsed(S(99)));
        Assert.Equal(StopwatchState.None, default(StopwatchState) with { Laps = ImmutableArray<TimeSpan>.Empty });
    }

    /// <summary>
    /// A widget removed takes its stopwatch with it, as a countdown goes with its widget; one still on
    /// a bar or in a folder keeps it (Countdowns.KeysIn names every stopwatch).
    /// </summary>
    [Fact]
    public void AStopwatchGoesWithItsWidget()
    {
        var kept = ListKind.Stopwatch.New();
        kept.Id = "kept";
        var folder = Karpo.Tests.Core.Configs.FolderKind(FolderView.List).New();
        folder.Id = "f";
        var inside = ListKind.Stopwatch.New();
        inside.Id = "inside";
        folder.Folder!.Put(inside);
        var config = Karpo.Tests.Core.Configs.With(Karpo.Tests.Core.Configs.Holding(kept), folder);
        var keys = Countdowns.KeysIn(config).ToList();
        Assert.Contains("kept", keys);
        Assert.Contains("inside", keys);

        var countdowns = new Countdowns();
        foreach (var key in new[] { "kept", "inside", "gone" })
        {
            countdowns.Stopwatches.Act(key, S(0));
        }
        countdowns.Keep(keys);
        Assert.Equal(["inside", "kept"], countdowns.Stopwatches.Keys.Order());
    }

    /// <summary>
    /// What the person set going is in memory only, as a timer's is: a dock made afresh — Karpo
    /// restarted — has no stopwatch running, and a stopwatch writes nothing into its widget.
    /// </summary>
    [Fact]
    public void ARestartClearsIt()
    {
        Assert.Empty(new Countdowns().Stopwatches.Keys);
        Assert.Equal(WidgetOptionsKind.None, WidgetInfos.Stopwatch.Options);
    }

    // --- How it is written ----------------------------------------------------------------

    [Theory]
    [InlineData(0, ".", "0:00.0")]
    [InlineData(1.99, ".", "0:01.9")]
    [InlineData(59.95, ".", "0:59.9")]
    [InlineData(754.5, ".", "12:34.5")]
    [InlineData(3723.4, ".", "1:02:03.4")]
    [InlineData(754.5, ",", "12:34,5")]
    [InlineData(-3, ",", "0:00,0")]
    [InlineData(1_000_000, ".", "99:59:59.9")]
    public void TenthsAreWrittenAsAStopwatchReadsThem(double seconds, string separator, string written) =>
        Assert.Equal(written, StopwatchText.Tenths(TimeSpan.FromSeconds(seconds), separator));

    /// <summary>The tenth begun is the tenth shown, never one rounded up: 0.09 s is still nought.</summary>
    [Fact]
    public void ATenthIsNeverRoundedUp()
    {
        Assert.Equal("0:00.0", StopwatchText.Tenths(TimeSpan.FromMilliseconds(99), "."));
        Assert.Equal("0:00.1", StopwatchText.Tenths(TimeSpan.FromMilliseconds(100), "."));
    }

    [Theory]
    [InlineData(0, "00:00")]
    [InlineData(299.9, "04:59")]
    [InlineData(3900, "01:05")]
    [InlineData(1_000_000, "99:59")]
    public void TheBarsReadingIsFiveFigures(double seconds, string written) =>
        Assert.Equal(written, StopwatchText.Cell(TimeSpan.FromSeconds(seconds)));

    /// <summary>The widest any reading is, with the sign the region sets off a tenth with.</summary>
    [Fact]
    public void TheTemplateHoldsEveryReading()
    {
        foreach (var separator in new[] { ".", "," })
        {
            var template = StopwatchText.Template(separator);
            foreach (var seconds in new[] { 0, 59.9, 3599.9, 35_999.9, 1_000_000 })
            {
                Assert.True(StopwatchText.Tenths(TimeSpan.FromSeconds(seconds), separator).Length <= template.Length);
            }
        }
    }

    // --- Where it stands ------------------------------------------------------------------

    /// <summary>
    /// It is a list line of one row to six, in the Time group beside the timer and the focus time;
    /// with nothing to set. Its taskbar entry is the stopwatch preset now (docs\widgets.md §92).
    /// </summary>
    [Fact]
    public void ItStandsBesideTheTimer()
    {
        Assert.Null(BarEntries.Find("stopwatch"));
        var line = ListWidgets.Of(ListKind.Stopwatch);
        Assert.Equal(WidgetSource.Stopwatch, line.Drawing);
        Assert.Equal(ListLine.Card, line.Line);
        Assert.Equal((2, 12), (line.Range.Least, line.Range.Most));
        Assert.Equal(WidgetInfos.Timer.Category, WidgetInfos.Stopwatch.Category);
        Assert.DoesNotContain(Catalogue.On(WidgetType.Bar), row => row.Key == "bar/stopwatch");
        Assert.Contains(Catalogue.On(WidgetType.List), row => row.Key == "list/stopwatch");
        Assert.Equal([WidgetAct.StartPause, WidgetAct.Lap, WidgetAct.Reset], TheDock.ActsFor(ListKind.Stopwatch));
    }
}
