// sports: which match a widget shows, how soon it is asked for again, and the words it says
// (docs\widgets.md §239): a match on now first; a final for a few hours after its kick-off, until the
// next is near; else the next, one put off saying so; else the last played. Half a minute while a
// match is on, a minute as one begins, the online interval otherwise but never past a kick-off; a
// failure waiting longer each time. A football minute, a quarter, a period and an inning's half.

using Karpo.Dock;
using Karpo.Dock.Sports;

namespace Karpo.Tests.Sports;

public sealed class SportsRulesTests
{
    private static readonly DateTime Now = new(2026, 10, 19, 18, 0, 0, DateTimeKind.Utc);

    private static SportsAnswer Of(params SportsMatch[] matches) => new() { Matches = [.. matches] };

    [Fact]
    public void AMatchOnNowIsShownWhateverElseThereIs()
    {
        var answer = Of(
            SavedSports.Match("final", Now.AddHours(-2), MatchState.Final, score: "1", otherScore: "0"),
            SavedSports.Match("live", Now.AddMinutes(-50), MatchState.Live, score: "2", otherScore: "1"),
            SavedSports.Match("next", Now.AddMinutes(20), MatchState.Scheduled));

        var view = SportsPick.Of(answer, "83", Now);

        Assert.Equal((SportsShows.Live, "live"), (view.Shows, view.Match?.Id));
        Assert.Equal(["next"], view.Coming.Select(match => match.Id));
        Assert.Equal(["final"], view.Played.Select(match => match.Id));
    }

    [Fact]
    public void AFinalStaysForSixHoursFromItsKickOffThenTheNextTakesOver()
    {
        var final = SavedSports.Match("final", Now.AddHours(-3), MatchState.Final, score: "2", otherScore: "1");
        var next = SavedSports.Match("next", Now.AddDays(4), MatchState.Scheduled);

        Assert.Equal((SportsShows.Final, "final"), Shown(Of(final, next), Now));
        // Past six hours from its kick-off: the next match.
        Assert.Equal((SportsShows.Next, "next"), Shown(Of(final, next), Now.AddHours(3.5)));
        // The next an hour away or less wins over a final still in its hours.
        var soon = SavedSports.Match("soon", Now.AddMinutes(45), MatchState.Scheduled);
        Assert.Equal((SportsShows.Next, "soon"), Shown(Of(final, soon), Now));
    }

    [Fact]
    public void AMatchPutOffSaysSoInItsPlaceUntilItsDayHasGone()
    {
        var postponed = SavedSports.Match("off", Now.AddHours(2), MatchState.Postponed);
        var next = SavedSports.Match("next", Now.AddDays(3), MatchState.Scheduled);

        Assert.Equal((SportsShows.Next, "off"), Shown(Of(postponed, next), Now));
        Assert.Equal((SportsShows.Next, "next"), Shown(Of(postponed, next), Now.AddHours(9)));
        // A kick-off passed and not begun is still the next, for a late start; hours later it is not.
        var late = SavedSports.Match("late", Now.AddMinutes(-20), MatchState.Scheduled);
        Assert.Equal((SportsShows.Next, "late"), Shown(Of(late, next), Now));
        Assert.Equal((SportsShows.Next, "next"), Shown(Of(late, next), Now.AddHours(4)));
    }

    [Fact]
    public void WithNothingToComeTheLastMatchPlayedStandsAndWithNoneNothing()
    {
        var old = SavedSports.Match("old", Now.AddDays(-20), MatchState.Final, score: "0", otherScore: "3");
        var older = SavedSports.Match("older", Now.AddDays(-27), MatchState.Final, score: "1", otherScore: "1");

        var view = SportsPick.Of(Of(older, old), "83", Now);
        Assert.Equal((SportsShows.Last, "old"), (view.Shows, view.Match?.Id));
        Assert.Equal(["older"], view.Played.Select(match => match.Id));
        Assert.Equal(SportsShows.Nothing, SportsPick.Of(Of(), "83", Now).Shows);
        Assert.Equal(SportsShows.Nothing, SportsPick.Of(null, "83", Now).Shows);
    }

    [Fact]
    public void BarcelonasSavedAnswerShowsItsNextMatchAndMonzasItsMatchOnNow()
    {
        var barcelona = SportsPick.Of(SavedSports.BarcelonaAnswer(), "83", SavedSports.SavedUtc);
        // Two days after its 5–2 at Betis: the next, at Paris in the Champions League.
        Assert.Equal(SportsShows.Next, barcelona.Shows);
        Assert.Equal("PSG", barcelona.Match!.Home.Abbreviation);
        Assert.Equal("401882846", barcelona.Played[0].Id);

        var monza = SportsPick.Of(SavedSports.MonzaLive(), "4007", SavedSports.SavedUtc);
        Assert.Equal(SportsShows.Live, monza.Shows);
        Assert.Equal("PAR", Assert.Single(monza.Others).Home.Abbreviation);
    }

    [Fact]
    public void ALiveMatchIsAskedForEveryHalfMinuteAndOneBeginningEveryMinute()
    {
        var interval = TimeSpan.FromMinutes(15);
        TimeSpan Pace(params SportsMatch[] matches) => SportsPace.Every(SportsPick.Of(Of(matches), "83", Now), Now, interval);

        Assert.Equal(TimeSpan.FromSeconds(30), Pace(SavedSports.Match("live", Now.AddMinutes(-10), MatchState.Live)));
        Assert.Equal(TimeSpan.FromMinutes(1), Pace(SavedSports.Match("soon", Now.AddMinutes(10), MatchState.Scheduled)));
        Assert.Equal(TimeSpan.FromMinutes(1), Pace(SavedSports.Match("late", Now.AddMinutes(-5), MatchState.Scheduled)));
        // Otherwise the online interval, but not past a quarter of an hour before the kick-off.
        Assert.Equal(interval, Pace(SavedSports.Match("later", Now.AddDays(2), MatchState.Scheduled)));
        Assert.Equal(TimeSpan.FromMinutes(5), Pace(SavedSports.Match("evening", Now.AddMinutes(20), MatchState.Scheduled)));
        // The next an hour away takes over from the final, and is asked for as it nears.
        Assert.Equal(TimeSpan.FromMinutes(45), SportsPace.Every(
            SportsPick.Of(Of(SavedSports.Match("final", Now.AddHours(-2), MatchState.Final), SavedSports.Match("next", Now.AddHours(1), MatchState.Scheduled)), "83", Now),
            Now, TimeSpan.FromHours(1)));
        Assert.Equal(interval, Pace());
    }

    [Fact]
    public void AFailureWaitsLongerEachTimeAndNeverPastHalfAnHour()
    {
        var live = TimeSpan.FromSeconds(30);
        Assert.Equal([30, 60, 120, 240, 480, 960, 1800, 1800], Enumerable.Range(1, 8).Select(failures => (int)SportsPace.Retry(live, failures).TotalSeconds));
        Assert.Equal(TimeSpan.FromMinutes(30), SportsPace.Retry(TimeSpan.FromHours(2), 1));
        Assert.Equal(TimeSpan.FromSeconds(30), SportsPace.Retry(TimeSpan.Zero, 1));
    }

    [Fact]
    public void WhileAMatchIsOnOnlyItsScoreboardIsReadAndTheScheduleEveryHalfHour()
    {
        var answer = Of(SavedSports.Match("live", Now.AddMinutes(-10), MatchState.Live));
        answer.ScheduledUtc = Now.AddMinutes(-5);
        var view = SportsPick.Of(answer, "83", Now);
        Assert.True(SportsPace.ScoreboardOnly(view, answer, Now));
        Assert.False(SportsPace.ScoreboardOnly(view, answer, Now.AddMinutes(26)));
        Assert.Equal("live", SportsPace.Scoreboard(view, Now)?.Id);

        var later = Of(SavedSports.Match("later", Now.AddDays(3), MatchState.Scheduled));
        later.ScheduledUtc = Now;
        var waiting = SportsPick.Of(later, "83", Now);
        Assert.False(SportsPace.ScoreboardOnly(waiting, later, Now));
        Assert.Null(SportsPace.Scoreboard(waiting, Now));
    }

    [Theory]
    // Football: the minute, the stoppage time, half-time.
    [InlineData(SportsClock.Minutes, "STATUS_SECOND_HALF", "53'", 2, "53'")]
    [InlineData(SportsClock.Minutes, "STATUS_FIRST_HALF", "45'+2'", 1, "45'+2'")]
    [InlineData(SportsClock.Minutes, "STATUS_HALFTIME", "45'", 1, "HT")]
    // Basketball's and American football's quarters, and overtime.
    [InlineData(SportsClock.Quarters, "STATUS_IN_PROGRESS", "5:32", 3, "Q3 5:32")]
    [InlineData(SportsClock.Quarters, "STATUS_IN_PROGRESS", "2:11", 5, "OT 2:11")]
    [InlineData(SportsClock.Quarters, "STATUS_IN_PROGRESS", "0:40", 6, "2OT 0:40")]
    [InlineData(SportsClock.Quarters, "STATUS_END_PERIOD", "0.0", 2, "Q2")]
    [InlineData(SportsClock.Halves, "STATUS_IN_PROGRESS", "12:10", 2, "H2 12:10")]
    [InlineData(SportsClock.Periods, "STATUS_IN_PROGRESS", "12:04", 2, "P2 12:04")]
    [InlineData(SportsClock.Periods, "STATUS_IN_PROGRESS", "3:00", 4, "OT 3:00")]
    // Baseball's half of the inning, from its words.
    [InlineData(SportsClock.Innings, "STATUS_IN_PROGRESS", "Top 5th", 5, "▲5")]
    [InlineData(SportsClock.Innings, "STATUS_IN_PROGRESS", "Bot 9th", 9, "▼9")]
    public void TheClockIsTheLeaguesOwn(SportsClock clock, string status, string shown, int period, string expected)
    {
        var match = SavedSports.Match("live", Now, MatchState.Live);
        match.Status = status;
        match.Clock = shown;
        match.Period = period;

        Assert.Equal(expected, SportsText.Clock(match, clock));
    }

    [Fact]
    public void FootballsScoreIsWrittenHomeFirstAndAmericasVisitorsFirst()
    {
        var monza = SportsPick.Of(SavedSports.MonzaLive(), "4007", SavedSports.SavedUtc).Match!;
        Assert.Equal("MON 2–0 CAG", SportsText.Score(monza, homeFirst: true));
        Assert.Equal("CAG 0–2 MON", SportsText.Score(monza, homeFirst: false));
        Assert.Equal("vs CAG", SportsText.Opponent(monza, "4007"));
        Assert.Equal("@ MON", SportsText.Opponent(monza, "2925"));
        Assert.Equal("@ Monza", SportsText.Opponent(monza, "2925", named: true));
        // Before kick-off a scoreboard's zeros are no score.
        var parma = SavedSports.MonzaLive().Others[0];
        Assert.Equal("PAR – TOR", SportsText.Score(parma, homeFirst: true));
    }

    [Fact]
    public void WhatTheFaceSaysForEachState()
    {
        var local = Now.ToLocalTime();
        string Said(SportsOptions team, SportsAnswer answer, DateTime at) =>
            SportsReading.From(team, new SportsHeld(answer, at, null, false), at, TimeSpan.FromMinutes(15)).Said(at.ToLocalTime());

        Assert.Equal("MON 2–0 CAG 53'", Said(SavedSports.Monza(), SavedSports.MonzaLive(), SavedSports.SavedUtc));
        Assert.Equal("BAR 2–1 RMA Final", Said(SavedSports.Barcelona(), Of(SavedSports.Match("final", Now.AddHours(-2), MatchState.Final, score: "2", otherScore: "1")), Now));
        Assert.Equal("@ RMA Postponed", Said(SavedSports.Barcelona(), Of(SavedSports.Match("off", Now.AddHours(5), MatchState.Postponed, home: false)), Now));
        var tomorrow = new DateTime(local.Year, local.Month, local.Day, 21, 0, 0, DateTimeKind.Local).AddDays(1);
        Assert.Equal($"vs RMA Tomorrow {Karpo.Dock.Panels.Nls.Time(tomorrow)}", Said(SavedSports.Barcelona(), Of(SavedSports.Match("next", tomorrow.ToUniversalTime(), MatchState.Scheduled)), Now));
        Assert.Equal("No matches coming up", Said(SavedSports.Barcelona(), Of(), Now));
        // Nothing yet: it says it is getting the scores; a failure with nothing to show, why.
        Assert.Equal("Getting the scores…", SportsReading.From(SavedSports.Barcelona(), default, Now, TimeSpan.FromMinutes(15)).Said(local));
        Assert.Equal("ESPN can't be reached", SportsReading.From(SavedSports.Barcelona(), new SportsHeld(null, null, S.DockSportsUnreachable, false), Now, TimeSpan.FromMinutes(15)).Said(local));
    }

    [Fact]
    public void AReadingIsStaleAfterAFailureOrOnceItIsOlderThanItsPaceAllows()
    {
        var live = SavedSports.MonzaLive();
        var fetched = SavedSports.SavedUtc;
        SportsReading At(DateTime now, string? problem = null) =>
            SportsReading.From(SavedSports.Monza(), new SportsHeld(live, fetched, problem, false), now, TimeSpan.FromMinutes(15));

        Assert.False(At(fetched.AddSeconds(40)).Stale);
        // Live, asked for every half minute: three minutes old is stale, and says its age.
        var old = At(fetched.AddMinutes(3));
        Assert.True(old.Stale);
        Assert.Equal("3 min ago", old.Updated); // §388: one meta wording
        Assert.True(At(fetched.AddSeconds(10), S.DockSportsNoAnswer).Stale);
        // The score stays what it was: nothing made up.
        Assert.Equal("MON 2–0 CAG 53'", old.Said(fetched.AddMinutes(3).ToLocalTime()));
    }

    private static (SportsShows, string?) Shown(SportsAnswer answer, DateTime now)
    {
        var view = SportsPick.Of(answer, "83", now);
        return (view.Shows, view.Match?.Id);
    }
}
