// The clock's timer and focus session (Karpo.Dock\Clock\Countdown.cs), and the
// split of the old clock widget into the date and the clock (Karpo.Core\Config.cs,
// Repair): what a file that had a clock with a date becomes, and that the old key
// is never written again. The date is the calendar's taskbar entry now (OneCalendarTests).

using System.Globalization;
using Karpo.Dock;
using Karpo.Tests.Core;

namespace Karpo.Tests.Calendars;

public sealed class ClockWidgetTests
{
    private static readonly DateTime Start = new(2026, 9, 22, 14, 0, 0);

    private static Countdown Timer(int minutes) => Countdown.Start(CountdownKind.Timer, TimeSpan.FromMinutes(minutes), Start);

    private static TimeSpan Minutes(double minutes) => TimeSpan.FromMinutes(minutes);

    [Fact]
    public void ATimerCountsDownFromWhenItStarted()
    {
        var timer = Timer(5);

        Assert.True(timer.Ticking);
        Assert.Equal(TimeSpan.FromMinutes(5), timer.Remaining(Start));
        Assert.Equal(TimeSpan.FromSeconds(90), timer.Remaining(Start.AddSeconds(210)));
        Assert.Equal("1:30", Countdown.Span(timer.Remaining(Start.AddSeconds(210))));
        // Never past zero, and never below it.
        Assert.Equal(TimeSpan.Zero, timer.Remaining(Start.AddMinutes(9)));
    }

    [Fact]
    public void ATimerThatRunsOutIsOverAndStaysThere()
    {
        var timer = Timer(1);

        var still = timer.At(Start.AddSeconds(59));
        var over = timer.At(Start.AddSeconds(60));

        Assert.Equal(CountdownPhase.Running, still.Phase);
        Assert.Equal(CountdownPhase.Done, over.Phase);
        Assert.Equal("00:00", over.Cell(Start.AddSeconds(90)));
        Assert.Equal(over, over.At(Start.AddHours(2)));
    }

    [Fact]
    public void PausingHoldsWhatIsLeftAndTakingItUpAgainCountsOn()
    {
        var timer = Timer(10);

        var held = timer.Pause(Start.AddMinutes(4));
        Assert.Equal(CountdownPhase.Paused, held.Phase);
        Assert.Equal(TimeSpan.FromMinutes(6), held.Remaining(Start.AddMinutes(30)));
        Assert.False(held.Ticking);

        // Taken up half an hour later: still six minutes, from then.
        var again = held.Resume(Start.AddMinutes(30));
        Assert.Equal(TimeSpan.FromMinutes(6), again.Remaining(Start.AddMinutes(30)));
        Assert.Equal(TimeSpan.FromMinutes(1), again.Remaining(Start.AddMinutes(35)));
        Assert.Equal(CountdownPhase.Done, again.At(Start.AddMinutes(36)).Phase);
    }

    [Fact]
    public void ResettingKeepsTheLengthAndStartingAgainRunsIt()
    {
        var timer = Timer(5).At(Start.AddMinutes(9));

        var reset = timer.Reset();
        Assert.Equal(CountdownPhase.Paused, reset.Phase);
        Assert.Equal(TimeSpan.FromMinutes(5), reset.Remaining(Start));

        var again = reset.Again(Start.AddHours(1));
        Assert.Equal(CountdownPhase.Running, again.Phase);
        Assert.Equal(TimeSpan.FromMinutes(5), again.Remaining(Start.AddHours(1)));
    }

    [Fact]
    public void AFocusSessionIsOneStretchThatEndsWhenItRunsOut()
    {
        var focus = Countdown.Start(CountdownKind.Focus, TimeSpan.FromMinutes(25), Start);

        Assert.Equal("25:00", focus.Cell(Start));
        Assert.Equal("Focus time", focus.Says(Start));
        Assert.Equal(CountdownPhase.Running, focus.At(Start.AddMinutes(24)).Phase);

        var over = focus.At(Start.AddMinutes(25));

        Assert.Equal(CountdownPhase.Done, over.Phase);
        Assert.Equal("00:00", over.Cell(Start.AddMinutes(26)));
        Assert.Equal("Focus time is over", over.Says(Start.AddMinutes(26)));
        // Nothing follows it: it stays over.
        Assert.Equal(over, over.At(Start.AddHours(1)));
    }

    [Fact]
    public void ALengthIsKeptWithinWhatATimerMayBe()
    {
        Assert.Equal(Countdown.Longest, Countdown.Start(CountdownKind.Timer, TimeSpan.FromHours(30), Start).Length);
        Assert.Equal(Countdown.Shortest, Countdown.Start(CountdownKind.Timer, TimeSpan.Zero, Start).Length);
        Assert.Equal(Countdown.None, Countdown.Start(CountdownKind.None, TimeSpan.FromMinutes(5), Start));
    }

    // --- The steppers and the wheel ---------------------------------------------

    [Theory]
    // A minute at a time under an hour, up and down.
    [InlineData(5, 1, 6)]
    [InlineData(5, -1, 4)]
    [InlineData(59, 1, 60)]
    // Five at a time from an hour up, landing on the step's own grid.
    [InlineData(60, 1, 65)]
    [InlineData(60, -1, 55)]
    [InlineData(61, 1, 65)]
    [InlineData(61, -1, 60)]
    [InlineData(90, 1, 95)]
    public void TheMinutesStepperMovesByAMinuteOrFive(int from, int direction, int expected) =>
        Assert.Equal(Minutes(expected), Countdown.StepMinutes(Minutes(from), direction));

    [Fact]
    public void TheStepperStopsAtTheEndsAndNeverWraps()
    {
        // A minute is the shortest a timer may be: minus keeps it there rather than wrapping to
        // twelve hours, however often it is pressed.
        Assert.Equal(TimeSpan.FromMinutes(1), Countdown.Shortest);
        var shortest = Countdown.StepMinutes(Countdown.Shortest, -1);
        Assert.Equal(Countdown.Shortest, shortest);
        Assert.Equal(Countdown.Shortest, Countdown.StepMinutes(shortest, -1));
        Assert.Equal(Countdown.Shortest, Countdown.StepMinutes(TimeSpan.Zero, -1));
        // Two minutes down is one, not nothing.
        Assert.Equal(Countdown.Shortest, Countdown.StepMinutes(Minutes(2), -1));

        // And the longest: plus keeps it there rather than wrapping to nothing.
        var longest = Countdown.StepMinutes(Countdown.Longest, 1);
        Assert.Equal(Countdown.Longest, longest);
        Assert.Equal(Countdown.Longest, Countdown.StepMinutes(longest, 1));
    }

    [Fact]
    public void ATimerIsSetInWholeMinutes()
    {
        // Every step lands on a whole minute, from wherever it starts.
        var odd = TimeSpan.FromSeconds(145);
        Assert.Equal(Minutes(3), Countdown.StepMinutes(odd, 1));
        Assert.Equal(Minutes(2), Countdown.StepMinutes(odd, -1));
        Assert.Equal(0, Countdown.StepMinutes(odd, 1).Seconds);
        Assert.Equal("2", Countdown.MinutesOf(odd));
    }

    [Fact]
    public void TheWheelTurnsALengthAsTheButtonsDo()
    {
        // A notch of the wheel is one step, whichever way it turns.
        var length = Minutes(10);
        for (var notch = 0; notch < 3; notch++)
        {
            length = Countdown.StepMinutes(length, 1);
        }
        Assert.Equal(Minutes(13), length);
        for (var notch = 0; notch < 5; notch++)
        {
            length = Countdown.StepMinutes(length, -1);
        }
        Assert.Equal(Minutes(8), length);
        // A direction of nothing leaves it alone.
        Assert.Equal(Minutes(8), Countdown.StepMinutes(length, 0));
    }

    [Fact]
    public void TheSteppersShowTheirNumbers()
    {
        Assert.Equal("5", Countdown.MinutesOf(Minutes(5)));
        Assert.Equal("90", Countdown.MinutesOf(Minutes(90)));
        Assert.Equal("1", Countdown.MinutesOf(TimeSpan.FromSeconds(105)));
    }

    [Theory]
    [InlineData(0, "0:00")]
    [InlineData(9, "0:09")]
    [InlineData(90, "1:30")]
    [InlineData(600, "10:00")]
    [InlineData(3600, "1:00:00")]
    [InlineData(3725, "1:02:05")]
    [InlineData(-5, "0:00")]
    public void WhatIsLeftIsWrittenAsMinutesAndSeconds(int seconds, string expected) =>
        Assert.Equal(expected, Countdown.Span(TimeSpan.FromSeconds(seconds)));

    [Theory]
    // Below the shortest a timer may be, a length reads as that shortest.
    [InlineData(0, "1 min")]
    [InlineData(30, "1 min")]
    [InlineData(60, "1 min")]
    [InlineData(1500, "25 min")]
    [InlineData(3600, "1 h")]
    [InlineData(5400, "1 h 30 min")]
    public void ALengthIsOfferedInWords(int seconds, string expected) =>
        Assert.Equal(expected, Countdown.Words(TimeSpan.FromSeconds(seconds)));

    [Fact]
    public void NothingSetShowsNothingOnTheBar()
    {
        Assert.Equal("", Countdown.None.Cell(Start));
        Assert.True(Countdown.None.Idle);
        Assert.False(Countdown.None.Ticking);
    }

    [Fact]
    public void TheDocksCountdownRingsOnceWhenItRunsOut()
    {
        var countdowns = new Countdowns();
        var changes = new List<string>();
        var ends = new List<(string Key, Countdown State)>();
        countdowns.Changed += key => changes.Add(key);
        countdowns.Ended += (key, state) => ends.Add((key, state));

        countdowns.Start("t", CountdownKind.Timer, TimeSpan.FromMinutes(1), Start);
        Assert.True(countdowns.Ticking);
        Assert.True(countdowns.Tick(Start.AddSeconds(30)));
        Assert.Empty(ends);
        Assert.True(countdowns.Tick(Start.AddSeconds(60)));
        var (key, state) = Assert.Single(ends);
        Assert.Equal(("t", CountdownPhase.Done), (key, state.Phase));
        // Over: it stays over, rings once, and stops the second's tick.
        Assert.False(countdowns.Ticking);
        Assert.False(countdowns.Tick(Start.AddSeconds(61)));
        Assert.Single(ends);
        Assert.Contains("t", countdowns.Keys);

        countdowns.Clear("t");
        Assert.Empty(countdowns.Keys);
        Assert.All(changes, changed => Assert.Equal("t", changed));
        Assert.True(changes.Count > 0);
    }

    /// <summary>
    /// The action button of a countdown, one rule for a timer's entry, card and line, a focus
    /// session's, and the clock's two panes (Countdowns.Act): it starts one that is not running,
    /// pauses one that runs, takes up again one that is paused, and starts again one that is over,
    /// at the length set now. A focus session on another key runs beside it and takes nothing from it.
    /// </summary>
    [Fact]
    public void TheActionButtonStartsPausesAndTakesUpAgain()
    {
        var countdowns = new Countdowns();
        Assert.True(countdowns.Of("t").Idle);

        countdowns.Act("t", CountdownKind.Timer, Minutes(5), Start);
        var timer = countdowns.Of("t");
        Assert.Equal((CountdownKind.Timer, CountdownPhase.Running, Minutes(5)), (timer.Kind, timer.Phase, timer.Length));
        Assert.True(countdowns.Of("f").Idle);

        countdowns.Act("t", CountdownKind.Timer, Minutes(5), Start.AddMinutes(1));
        Assert.Equal(CountdownPhase.Paused, countdowns.Of("t").Phase);
        Assert.Equal(Minutes(4), countdowns.Of("t").Left);

        countdowns.Act("t", CountdownKind.Timer, Minutes(5), Start.AddMinutes(3));
        Assert.Equal(CountdownPhase.Running, countdowns.Of("t").Phase);
        Assert.Equal(Start.AddMinutes(7), countdowns.Of("t").Until);

        // Over: the button starts it again, at the length set now.
        _ = countdowns.Tick(Start.AddMinutes(7));
        Assert.Equal(CountdownPhase.Done, countdowns.Of("t").Phase);
        countdowns.Act("t", CountdownKind.Timer, Minutes(10), Start.AddMinutes(8));
        Assert.Equal((CountdownPhase.Running, Minutes(10)), (countdowns.Of("t").Phase, countdowns.Of("t").Length));

        // A focus session's button starts its own, and the timer runs on.
        countdowns.Act("f", CountdownKind.Focus, Minutes(25), Start.AddMinutes(9));
        Assert.Equal((CountdownKind.Focus, CountdownPhase.Running), (countdowns.Of("f").Kind, countdowns.Of("f").Phase));
        Assert.Equal((CountdownKind.Timer, CountdownPhase.Running), (countdowns.Of("t").Kind, countdowns.Of("t").Phase));
    }

    /// <summary>
    /// A step of a length, as a stepper, the wheel and a menu take it: a minute, five from an hour
    /// up, from the length of the widget it belongs to, kept in its options in whole minutes.
    /// </summary>
    [Fact]
    public void AStepIsAMinuteFromItsWidgetsLength()
    {
        var widget = ListKind.Timer.New();
        int Step(int direction) => CountdownSource.Whole(Countdown.StepMinutes(CountdownTile.LengthOf(widget, CountdownKind.Timer), direction));

        // Nobody set it: its kind's length.
        Assert.Null(widget.Countdown);
        Assert.Equal(Minutes(5), CountdownTile.LengthOf(widget, CountdownKind.Timer));
        Assert.Equal(6, Step(1));
        widget.Countdown = new CountdownOptions { Minutes = 5 };
        Assert.Equal(4, Step(-1));
        widget.Countdown.Minutes = 60;
        Assert.Equal(65, Step(1));
    }

    [Fact]
    public void WhatTheBarShowsIsAlwaysTheSameFiveFiguresWide()
    {
        // The cell must not move as the countdown runs, so every value it can show is five
        // figures: minutes and seconds under an hour, hours and minutes from an hour up.
        var timer = Countdown.Start(CountdownKind.Timer, Minutes(90), Start);
        var seen = new List<string>();
        foreach (var second in new[] { 0, 1, 59, 60, 1800, 1801, 5399, 5400 })
        {
            var state = timer.At(Start.AddSeconds(second));
            seen.Add(state.Cell(Start.AddSeconds(second)));
        }
        Assert.All(seen, text => Assert.Equal(5, text.Length));
        Assert.Equal("01:30", seen[0]);
        Assert.Equal("01:00", seen[4]);
        Assert.Equal("59:59", seen[5]);
        Assert.Equal("00:00", seen[^1]);
        // Paused and over say it the same way; nothing set says nothing at all.
        Assert.Equal(5, timer.Pause(Start.AddMinutes(1)).Cell(Start.AddHours(2)).Length);
        Assert.Equal("", Countdown.None.Cell(Start));
    }

    // --- A length in the config ------------------------------------------------

    /// <summary>
    /// A timer's and a focus session's length is theirs, in the config: a new one has none and starts at
    /// its kind's, a nonsense one reads as its kind's, one past twelve hours as twelve hours, and a name
    /// is kept trimmed.
    /// </summary>
    [Theory]
    [InlineData(0, 5)]
    [InlineData(-5, 5)]
    [InlineData(1, 1)]
    [InlineData(90, 90)]
    [InlineData(720, 720)]
    [InlineData(5000, 720)]
    public void ATimersLengthIsKeptWithinWhatATimerMayBe(int minutes, int kept)
    {
        var timer = new CellItem { Kind = "timer", Countdown = new CountdownOptions { Minutes = minutes, Name = "  Tea " } };
        timer.Normalize();
        Assert.Equal((kept, "Tea"), (timer.Countdown!.Minutes, timer.Countdown.Name));

        // Nobody set it: nothing is written, and it starts at its kind's length.
        var focus = new CellItem { Kind = "focus" };
        focus.Normalize();
        Assert.Null(focus.Countdown);
        Assert.Equal(Minutes(CountdownOptions.FocusMinutes), CountdownTile.LengthOf(focus, CountdownKind.Focus));
    }

    [Fact]
    public void WhatIsLeftIsWrittenTheSameWhereverItIsShown()
    {
        var timer = Timer(25);

        // The bar's cell, the tooltip and the flyout all say the same thing.
        Assert.Equal("24:30", timer.Cell(Start.AddSeconds(30)));
        Assert.Equal("Timer", timer.Says(Start));
        Assert.Equal("24:30", Countdown.Span(timer.Remaining(Start.AddSeconds(30))));
        Assert.Equal("Paused", timer.Pause(Start.AddSeconds(30)).Says(Start));
        Assert.Equal(
            "24:30",
            timer.Pause(Start.AddSeconds(30)).Cell(Start.AddMinutes(10)).ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// The seam the picture tests freeze the clock with is for them alone: left as it is, it is this
    /// PC's own clock, and a thread that freezes it is the only thread that sees it frozen — two
    /// picture tests drawing beside each other would otherwise fight over what the time is.
    /// </summary>
    [Fact]
    public void TheClockIsThisPcsUntilAPictureFreezesIt()
    {
        var before = DateTime.Now;
        Assert.InRange(Clock.Now(), before.AddSeconds(-5), DateTime.Now.AddSeconds(5));

        var frozen = new DateTime(2026, 9, 22, 14, 40, 7, DateTimeKind.Local);
        try
        {
            Clock.Now = () => frozen;
            Assert.Equal(frozen, Clock.Now());
            // Everything the widgets read goes through it: the clock, the date and the zones.
            Assert.Equal(frozen, Clock.NowIn(""));
            Assert.Equal(frozen.ToUniversalTime(), Clock.NowIn("UTC"), TimeSpan.FromSeconds(1));

            var elsewhere = default(DateTime);
            var thread = new Thread(() => elsewhere = Clock.Now());
            thread.Start();
            thread.Join();
            Assert.NotEqual(frozen, elsewhere);
            Assert.InRange(elsewhere, before.AddSeconds(-5), DateTime.Now.AddSeconds(5));
        }
        finally
        {
            Clock.Now = Clock.Real;
        }

        Assert.InRange(Clock.Now(), before.AddSeconds(-5), DateTime.Now.AddSeconds(5));
    }
}
