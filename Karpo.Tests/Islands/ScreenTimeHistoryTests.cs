// Screen time by day (Karpo.Dock\ScreenTime\ScreenTimeHistory.cs, restored with it on 2026-10-09): spans cut at
// local midnights, in zones whose days are 23 or 25 hours long or whose clocks
// jump at midnight; the days kept and pruned; screentime.json and
// screentime-history.json read, merged, migrated from the files before the
// history, and written atomically. The zones are made here, so the tests don't
// depend on the time zone data of the PC they run on.

using Karpo.Dock;

namespace Karpo.Tests.Islands;

public sealed class ScreenTimeHistoryTests
{
    private const string Firefox = @"C:\Program Files\Mozilla Firefox\firefox.exe";
    private const string Code = @"C:\Users\Someone\AppData\Local\Programs\Microsoft VS Code\Code.exe";
    private const string Calculator = "aumid:Microsoft.WindowsCalculator_8wekyb3d8bbwe!App";

    /// <summary>Central Europe: +1, summer time +2 from the last Sunday of March 02:00 to the last Sunday of October 03:00.</summary>
    private static readonly TimeZoneInfo Europe = TimeZoneInfo.CreateCustomTimeZone(
        "Test Europe", TimeSpan.FromHours(1), "Test Europe", "Test Europe", "Test Europe Summer",
        [
            TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
                DateTime.MinValue.Date, DateTime.MaxValue.Date, TimeSpan.FromHours(1),
                TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 2, 0, 0), 3, 5, DayOfWeek.Sunday),
                TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 3, 0, 0), 10, 5, DayOfWeek.Sunday)),
        ]);

    /// <summary>
    /// A southern zone whose clocks change at midnight, as Chile's do: −4, summer time −3 from the first
    /// Sunday of September 00:00 (midnight is skipped) to the first Sunday of April 00:00 summer time
    /// (the clocks go back to 23:00 of the Saturday).
    /// </summary>
    private static readonly TimeZoneInfo Midnight = TimeZoneInfo.CreateCustomTimeZone(
        "Test Midnight", TimeSpan.FromHours(-4), "Test Midnight", "Test Midnight", "Test Midnight Summer",
        [
            TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
                DateTime.MinValue.Date, DateTime.MaxValue.Date, TimeSpan.FromHours(1),
                TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 0, 0, 0), 9, 1, DayOfWeek.Sunday),
                TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 0, 0, 0), 4, 1, DayOfWeek.Sunday)),
        ]);

    private static DateTime Utc(int year, int month, int day, int hour, int minute = 0) =>
        new(year, month, day, hour, minute, 0, DateTimeKind.Utc);

    private static DateOnly Date(int year, int month, int day) => new(year, month, day);

    // --- Days in a time zone ---------------------------------------------------

    [Fact]
    public void ASpanOverMidnightCountsForBothDays()
    {
        var log = new ScreenTimeLog();

        // 23:30 to 00:45 in summer time (+2).
        var first = log.Add(Utc(2026, 9, 21, 21, 30), Utc(2026, 9, 21, 22, 45), Firefox, Europe);

        Assert.Equal(Date(2026, 9, 21), first);
        Assert.Equal(TimeSpan.FromMinutes(30), log.TotalOn(Date(2026, 9, 21)));
        Assert.Equal(TimeSpan.FromMinutes(45), log.TotalOn(Date(2026, 9, 22)));
        Assert.Equal(TimeSpan.FromMinutes(45), log.Day(Date(2026, 9, 22))!.Apps[Firefox]);
        Assert.Equal(new DateTime(2026, 9, 21, 23, 30, 0), log.Day(Date(2026, 9, 21))!.Since);
        Assert.Equal(new DateTime(2026, 9, 22, 0, 0, 0), log.Day(Date(2026, 9, 22))!.Since);
    }

    [Fact]
    public void TheDaySummerTimeStartsHasTwentyThreeHours()
    {
        var log = new ScreenTimeLog();
        // From the 29 March's midnight (+1) to the 30th's (+2).
        var start = Utc(2026, 3, 28, 23);
        var end = Utc(2026, 3, 29, 22);

        Assert.Equal(end, ScreenTimeClock.NextDay(start, Europe));
        log.Add(start, end, null, Europe);

        Assert.Equal(TimeSpan.FromHours(23), log.TotalOn(Date(2026, 3, 29)));
        Assert.Equal(1, log.Count);
    }

    [Fact]
    public void TheDaySummerTimeEndsHasTwentyFiveHours()
    {
        var log = new ScreenTimeLog();
        // From the 25 October's midnight (+2) to the 26th's (+1).
        var start = Utc(2026, 10, 24, 22);
        var end = Utc(2026, 10, 25, 23);

        Assert.Equal(end, ScreenTimeClock.NextDay(Utc(2026, 10, 25, 1, 30), Europe));
        log.Add(start, end, Code, Europe);

        Assert.Equal(TimeSpan.FromHours(25), log.TotalOn(Date(2026, 10, 25)));
        Assert.Equal(TimeSpan.FromHours(25), log.Day(Date(2026, 10, 25))!.Apps[Code]);
        Assert.Equal(1, log.Count);
    }

    [Fact]
    public void ClocksThatSkipMidnightStartTheDayAtTheJump()
    {
        // 6 September 2026: at 00:00 (−4, 04:00 UTC) the clocks go to 01:00 (−3).
        var jump = Utc(2026, 9, 6, 4);
        Assert.Equal(jump, ScreenTimeClock.NextDay(Utc(2026, 9, 6, 2), Midnight));

        var log = new ScreenTimeLog();
        log.Add(Utc(2026, 9, 6, 3, 30), Utc(2026, 9, 6, 4, 30), Firefox, Midnight);

        Assert.Equal(TimeSpan.FromMinutes(30), log.TotalOn(Date(2026, 9, 5)));
        Assert.Equal(TimeSpan.FromMinutes(30), log.TotalOn(Date(2026, 9, 6)));
        // The new day's first minute on the wall clock is 01:00.
        Assert.Equal(new DateTime(2026, 9, 6, 1, 0, 0), log.Day(Date(2026, 9, 6))!.Since);
    }

    [Fact]
    public void ClocksThatGoBackOverMidnightKeepTheDayUntilTheSecondMidnight()
    {
        // 5 April 2026: at 00:00 summer time (−3, 03:00 UTC) the clocks go back to 23:00 of the 4th (−4),
        // so the 4th lasts until 00:00 standard time, 04:00 UTC: 25 hours.
        var dayStart = Utc(2026, 4, 5, 4);
        Assert.Equal(dayStart, ScreenTimeClock.NextDay(Utc(2026, 4, 5, 2, 30), Midnight));

        var log = new ScreenTimeLog();
        log.Add(Utc(2026, 4, 5, 2, 30), Utc(2026, 4, 5, 4, 30), null, Midnight);

        Assert.Equal(TimeSpan.FromMinutes(90), log.TotalOn(Date(2026, 4, 4)));
        Assert.Equal(TimeSpan.FromMinutes(30), log.TotalOn(Date(2026, 4, 5)));
        Assert.Equal(TimeSpan.FromHours(25), ScreenTimeClock.NextDay(Utc(2026, 4, 4, 12), Midnight) - Utc(2026, 4, 4, 3));
    }

    /// <summary>A year of days in the made-up zones and a few of Windows' own: each begins where the one before ends, one date later.</summary>
    [Fact]
    public void EveryDayOfAYearFollowsTheOneBefore()
    {
        var zones = new List<TimeZoneInfo> { Europe, Midnight, TimeZoneInfo.Utc, TimeZoneInfo.Local };
        foreach (var id in new[] { "W. Europe Standard Time", "Pacific Standard Time", "Pacific SA Standard Time", "Lord Howe Standard Time", "Iran Standard Time", "Cuba Standard Time" })
        {
            if (TryZone(id) is { } zone)
            {
                zones.Add(zone);
            }
        }
        foreach (var zone in zones)
        {
            var start = ScreenTimeClock.NextDay(Utc(2025, 12, 31, 12), zone);
            var date = ScreenTimeClock.LocalDate(start, zone);
            for (var i = 0; i < 366; i++)
            {
                var next = ScreenTimeClock.NextDay(start, zone);
                var hours = (next - start).TotalHours;
                Assert.InRange(hours, 22, 26);
                Assert.Equal(date.AddDays(1), ScreenTimeClock.LocalDate(next, zone));
                // Nothing before the start of the next day is on it.
                Assert.Equal(date, ScreenTimeClock.LocalDate(next.AddTicks(-1), zone));
                (start, date) = (next, date.AddDays(1));
            }
        }
    }

    [Fact]
    public void ASpanOfDaysIsCutIntoEachOfThem()
    {
        var log = new ScreenTimeLog();

        // Three days and a bit, from 22:00 on the 20th to 01:00 on the 24th (+2).
        log.Add(Utc(2026, 9, 20, 20), Utc(2026, 9, 23, 23), Calculator, Europe);

        Assert.Equal(TimeSpan.FromHours(2), log.TotalOn(Date(2026, 9, 20)));
        Assert.Equal(TimeSpan.FromHours(24), log.TotalOn(Date(2026, 9, 21)));
        Assert.Equal(TimeSpan.FromHours(24), log.TotalOn(Date(2026, 9, 22)));
        Assert.Equal(TimeSpan.FromHours(24), log.TotalOn(Date(2026, 9, 23)));
        Assert.Equal(TimeSpan.FromHours(1), log.TotalOn(Date(2026, 9, 24)));
        Assert.Equal(Date(2026, 9, 20), log.Oldest);
    }

    [Fact]
    public void AnEmptySpanChangesNothing()
    {
        var log = new ScreenTimeLog();

        Assert.Null(log.Add(Utc(2026, 9, 22, 10), Utc(2026, 9, 22, 10), Firefox, Europe));
        Assert.Null(log.Add(Utc(2026, 9, 22, 10), Utc(2026, 9, 22, 9), Firefox, Europe));
        Assert.Equal(0, log.Count);
    }

    // --- Days kept ---------------------------------------------------------------

    [Fact]
    public void PruningKeepsTheLastNinetyDays()
    {
        var log = new ScreenTimeLog();
        var today = Date(2026, 9, 22);
        for (var i = 0; i <= 120; i++)
        {
            log.Ensure(today.AddDays(-i)).Total = TimeSpan.FromMinutes(i + 1);
        }

        Assert.True(log.Prune(today, ScreenTimeCount.KeptDays));
        Assert.Equal(today.AddDays(-ScreenTimeCount.KeptDays), log.Oldest);
        Assert.Equal(ScreenTimeCount.KeptDays + 1, log.Count);
        Assert.False(log.Prune(today, ScreenTimeCount.KeptDays));
    }

    [Fact]
    public void OfTwoRecordsOfADayTheLargerIsKept()
    {
        var log = new ScreenTimeLog();
        var day = Date(2026, 9, 21);
        var later = new ScreenTimeDay(day) { Total = TimeSpan.FromHours(3) };
        later.Apps[Firefox] = TimeSpan.FromHours(3);
        var earlier = new ScreenTimeDay(day) { Total = TimeSpan.FromHours(2.5) };

        Assert.True(log.Merge(earlier));
        Assert.True(log.Merge(later));
        Assert.False(log.Merge(earlier));
        Assert.Equal(TimeSpan.FromHours(3), log.TotalOn(day));
        Assert.Equal(TimeSpan.FromHours(3), log.Day(day)!.Apps[Firefox]);
    }

    [Fact]
    public void AppsAreRankedByTime()
    {
        var day = new ScreenTimeDay(Date(2026, 9, 22)) { Total = TimeSpan.FromMinutes(100) };
        day.Apps[Code] = TimeSpan.FromMinutes(30);
        day.Apps[Firefox] = TimeSpan.FromMinutes(50);
        day.Apps[Calculator] = TimeSpan.FromMinutes(10);
        day.Apps["b.exe"] = TimeSpan.FromMinutes(1);
        day.Apps["a.exe"] = TimeSpan.FromMinutes(1);

        Assert.Equal([Firefox, Code, Calculator, "a.exe", "b.exe"], day.Ranked().Select(app => app.App));
    }

    [Fact]
    public void ACopyIsItsOwn()
    {
        var log = new ScreenTimeLog();
        log.Add(Utc(2026, 9, 22, 8), Utc(2026, 9, 22, 9), Firefox, Europe);
        var copy = log.Copy();

        log.Add(Utc(2026, 9, 22, 9), Utc(2026, 9, 22, 10), Firefox, Europe);

        Assert.Equal(TimeSpan.FromHours(1), copy.TotalOn(Date(2026, 9, 22)));
        Assert.Equal(TimeSpan.FromHours(1), copy.Day(Date(2026, 9, 22))!.Apps[Firefox]);
        Assert.Equal(TimeSpan.FromHours(2), log.TotalOn(Date(2026, 9, 22)));
    }

    // --- Words -------------------------------------------------------------------

    [Fact]
    public void DaysAreNamedFromToday()
    {
        var today = Date(2026, 9, 22);
        static string Long(DateOnly day) => $"long {day:yyyy-MM-dd}";

        Assert.Equal("Today", ScreenTimeText.DayName(today, today, Long));
        Assert.Equal("Yesterday", ScreenTimeText.DayName(today.AddDays(-1), today, Long));
        Assert.Equal("long 2026-09-20", ScreenTimeText.DayName(today.AddDays(-2), today, Long));
    }

    [Theory]
    [InlineData(0, "0 min")]
    [InlineData(20, "< 1 min")]
    [InlineData(60, "1 min")]
    [InlineData(42 * 60 + 59, "42 min")]
    [InlineData(3 * 3600, "3 h")]
    [InlineData(3 * 3600 + 42 * 60, "3 h 42 min")]
    [InlineData(26 * 3600 + 60, "26 h 1 min")]
    public void DurationsReadLikeWindows(int seconds, string expected) =>
        Assert.Equal(expected, ScreenTimeText.Duration(TimeSpan.FromSeconds(seconds)));

    // --- Files ---------------------------------------------------------------------

    [Fact]
    public void TheTwoFilesGiveTheDaysBack()
    {
        var today = Date(2026, 9, 22);
        var five = new ScreenTimeLog();
        five.Add(Utc(2026, 9, 20, 6), Utc(2026, 9, 20, 9), Firefox, Europe);
        five.Add(Utc(2026, 9, 21, 7), Utc(2026, 9, 21, 8), Code, Europe);
        five.Add(Utc(2026, 9, 22, 6, 10), Utc(2026, 9, 22, 7), Calculator, Europe);
        var fifteen = new ScreenTimeLog();
        fifteen.Add(Utc(2026, 9, 21, 7), Utc(2026, 9, 21, 9), null, Europe);
        var logs = new SortedDictionary<int, ScreenTimeLog> { [5] = five, [15] = fifteen };

        var live = ScreenTimeFiles.ParseLive(ScreenTimeFiles.Serialize(ScreenTimeFiles.Live(logs, today)));
        var history = ScreenTimeFiles.ParseHistory(ScreenTimeFiles.Serialize(ScreenTimeFiles.History(logs, today)));
        var read = ScreenTimeFiles.Read(live, history, today, 5);

        Assert.False(read.HistoryStale);
        Assert.Equal([5, 15], read.Resumed.Order());
        Assert.Equal([5, 15], read.Logs.Keys);
        var back = read.Logs[5];
        Assert.Equal(3, back.Count);
        Assert.Equal(TimeSpan.FromHours(3), back.TotalOn(Date(2026, 9, 20)));
        Assert.Equal(TimeSpan.FromHours(3), back.Day(Date(2026, 9, 20))!.Apps[Firefox]);
        Assert.Equal(new DateTime(2026, 9, 20, 8, 0, 0), back.Day(Date(2026, 9, 20))!.Since);
        Assert.Equal(TimeSpan.FromMinutes(50), back.TotalOn(today));
        Assert.Equal(TimeSpan.FromMinutes(50), back.Day(today)!.Apps[Calculator]);
        Assert.Equal(new DateTime(2026, 9, 22, 8, 10, 0), back.Day(today)!.Since);
        Assert.Equal(TimeSpan.FromHours(2), read.Logs[15].TotalOn(Date(2026, 9, 21)));
        Assert.Empty(read.Logs[15].Day(Date(2026, 9, 21))!.Apps);
    }

    [Fact]
    public void TheHistoryHoldsEveryDayButTodayAndTodaysFileOnlyToday()
    {
        var today = Date(2026, 9, 22);
        var log = new ScreenTimeLog();
        for (var i = 0; i < 30; i++)
        {
            log.Ensure(today.AddDays(-i)).Total = TimeSpan.FromMinutes(10 + i);
        }
        var logs = new SortedDictionary<int, ScreenTimeLog> { [5] = log };

        var history = ScreenTimeFiles.History(logs, today);
        var live = ScreenTimeFiles.Live(logs, today);

        Assert.Equal(29, history.Rules!["5"].Count);
        Assert.DoesNotContain("2026-09-22", history.Rules["5"].Keys);
        Assert.Equal("2026-09-22", live.Rules!["5"].Day);
        Assert.Equal(600, live.Rules["5"].Seconds);
        // Today's file still carries the last two weeks' totals, as an older Karpo reads them.
        Assert.Equal(ScreenTimeFiles.LegacyDays, live.Rules["5"].Earlier!.Count);
        Assert.Equal("2026-09-22", live.Day);
        Assert.Equal(600, live.Seconds);
        Assert.Equal(5, live.Idle);
    }

    /// <summary>The file Karpo wrote before the history: today goes on from it with its apps, its earlier totals become days.</summary>
    [Fact]
    public void TodaysFileFromBeforeTheHistoryIsKept()
    {
        const string Json = """
            {
              "day": "2026-09-22", "seconds": 3600, "since": "2026-09-22T08:12:00+02:00",
              "earlier": { "2026-09-21": 7200, "2026-09-20": 5400 }, "idle": 5,
              "rules": {
                "5": {
                  "apps": { "C:\\Program Files\\Mozilla Firefox\\firefox.exe": 2400.5, "aumid:Microsoft.WindowsCalculator_8wekyb3d8bbwe!App": 1000 },
                  "day": "2026-09-22", "seconds": 3600, "since": "2026-09-22T08:12:00",
                  "earlier": { "2026-09-21": 7200, "2026-09-20": 5400 }
                }
              }
            }
            """;
        var today = Date(2026, 9, 22);

        var read = ScreenTimeFiles.Read(ScreenTimeFiles.ParseLive(Json), null, today, 15);

        Assert.True(read.HistoryStale);
        Assert.Equal([5], read.Resumed);
        var log = read.Logs[5];
        Assert.Equal(TimeSpan.FromHours(1), log.TotalOn(today));
        Assert.Equal(2400.5, log.Day(today)!.Apps[Firefox].TotalSeconds, 3);
        Assert.Equal(TimeSpan.FromSeconds(1000), log.Day(today)!.Apps[Calculator]);
        Assert.Equal(new DateTime(2026, 9, 22, 8, 12, 0), log.Day(today)!.Since);
        Assert.Equal(TimeSpan.FromHours(2), log.TotalOn(Date(2026, 9, 21)));
        Assert.Equal(TimeSpan.FromMinutes(90), log.TotalOn(Date(2026, 9, 20)));
        Assert.Empty(log.Day(Date(2026, 9, 21))!.Apps);
        Assert.Single(read.Logs);

        // Written back in the two files, nothing is lost.
        var again = ScreenTimeFiles.Read(
            ScreenTimeFiles.ParseLive(ScreenTimeFiles.Serialize(ScreenTimeFiles.Live(read.Logs, today))),
            ScreenTimeFiles.ParseHistory(ScreenTimeFiles.Serialize(ScreenTimeFiles.History(read.Logs, today))),
            today,
            15);
        Assert.False(again.HistoryStale);
        Assert.Equal(3, again.Logs[5].Count);
        Assert.Equal(TimeSpan.FromMinutes(90), again.Logs[5].TotalOn(Date(2026, 9, 20)));
        Assert.Equal(2, again.Logs[5].Day(today)!.Apps.Count);
    }

    [Fact]
    public void AFileFromBeforeRevisionEightIsTheFirstIdleTimes()
    {
        const string Json = """{ "day": "2026-09-22", "seconds": 1800, "earlier": { "2026-09-19": 600 } }""";
        var today = Date(2026, 9, 22);

        var read = ScreenTimeFiles.Read(ScreenTimeFiles.ParseLive(Json), null, today, 15);

        Assert.Equal([15], read.Logs.Keys);
        Assert.Equal([15], read.Resumed);
        Assert.Equal(TimeSpan.FromMinutes(30), read.Logs[15].TotalOn(today));
        Assert.Equal(TimeSpan.FromMinutes(10), read.Logs[15].TotalOn(Date(2026, 9, 19)));
    }

    /// <summary>Karpo last counted yesterday: yesterday is a day of the history now, apps and all, and today starts afresh.</summary>
    [Fact]
    public void AnOlderDayInTodaysFileBecomesADayOfTheHistory()
    {
        const string Json = """
            { "rules": { "5": { "day": "2026-09-21", "seconds": 5000, "since": "2026-09-21T07:00:00",
              "apps": { "C:\\Program Files\\Mozilla Firefox\\firefox.exe": 5000 } } } }
            """;
        var today = Date(2026, 9, 22);

        var read = ScreenTimeFiles.Read(ScreenTimeFiles.ParseLive(Json), null, today, 5);

        Assert.Empty(read.Resumed);
        Assert.True(read.HistoryStale);
        Assert.Equal(TimeSpan.FromSeconds(5000), read.Logs[5].Day(Date(2026, 9, 21))!.Apps[Firefox]);
        Assert.Null(read.Logs[5].Day(today));
    }

    /// <summary>
    /// Karpo stopped between writing the history (with yesterday whole) and today's file (still yesterday,
    /// from its last save): the history's larger record stays, and nothing needs writing again.
    /// </summary>
    [Fact]
    public void ADayHalfMovedIntoTheHistoryIsWholeAgain()
    {
        var today = Date(2026, 9, 22);
        var yesterday = Date(2026, 9, 21);
        var whole = new ScreenTimeLog();
        whole.Ensure(yesterday).Total = TimeSpan.FromMinutes(200);
        var older = new ScreenTimeLog();
        older.Ensure(yesterday).Total = TimeSpan.FromMinutes(195);

        var history = ScreenTimeFiles.History(new SortedDictionary<int, ScreenTimeLog> { [5] = whole }, today);
        var live = ScreenTimeFiles.Live(new SortedDictionary<int, ScreenTimeLog> { [5] = older }, yesterday);
        var read = ScreenTimeFiles.Read(live, history, today, 5);

        Assert.Equal(TimeSpan.FromMinutes(200), read.Logs[5].TotalOn(yesterday));
        Assert.False(read.HistoryStale);
        Assert.Empty(read.Resumed);

        // The other way round (today's file has more), the history is to be written again.
        var fresher = ScreenTimeFiles.Read(ScreenTimeFiles.Live(new SortedDictionary<int, ScreenTimeLog> { [5] = whole }, yesterday),
            ScreenTimeFiles.History(new SortedDictionary<int, ScreenTimeLog> { [5] = older }, today), today, 5);
        Assert.Equal(TimeSpan.FromMinutes(200), fresher.Logs[5].TotalOn(yesterday));
        Assert.True(fresher.HistoryStale);
    }

    [Fact]
    public void BrokenFilesReadAsNothing()
    {
        Assert.Null(ScreenTimeFiles.ParseLive("{ not json"));
        Assert.Null(ScreenTimeFiles.ParseHistory("[1, 2"));
        var read = ScreenTimeFiles.Read(null, null, Date(2026, 9, 22), 5);
        Assert.Empty(read.Logs);
        Assert.False(read.HistoryStale);
    }

    [Fact]
    public void OddEntriesAreSkipped()
    {
        const string History = """
            { "rules": {
                "5": { "2026-09-20": { "seconds": 60, "apps": { "": 10, "x.exe": -5, "y.exe": 30 } }, "not a day": { "seconds": 1 } },
                "0": { "2026-09-20": { "seconds": 60 } },
                "abc": { "2026-09-20": { "seconds": 60 } } } }
            """;

        var read = ScreenTimeFiles.Read(null, ScreenTimeFiles.ParseHistory(History), Date(2026, 9, 22), 5);

        Assert.Equal([5], read.Logs.Keys);
        var day = read.Logs[5].Day(Date(2026, 9, 20))!;
        Assert.Equal(["y.exe"], day.Apps.Keys);
        Assert.Equal(1, read.Logs[5].Count);
    }

    [Fact]
    public void WritingReplacesTheFileWholeAndKeepsTheLastOne()
    {
        var folder = Path.Combine(Path.GetTempPath(), "karpo-tests", "screentime-" + Guid.NewGuid().ToString("N"));
        try
        {
            var today = Date(2026, 9, 22);
            var log = new ScreenTimeLog();
            log.Ensure(today.AddDays(-1)).Total = TimeSpan.FromMinutes(10);
            var logs = new SortedDictionary<int, ScreenTimeLog> { [5] = log };
            Assert.True(ScreenTimeFiles.Write(folder, ScreenTimeFiles.HistoryName, ScreenTimeFiles.Serialize(ScreenTimeFiles.History(logs, today)), backup: true));
            log.Ensure(today.AddDays(-2)).Total = TimeSpan.FromMinutes(20);
            Assert.True(ScreenTimeFiles.Write(folder, ScreenTimeFiles.HistoryName, ScreenTimeFiles.Serialize(ScreenTimeFiles.History(logs, today)), backup: true));

            Assert.Equal([ScreenTimeFiles.HistoryName, ScreenTimeFiles.HistoryName + ".bak"], Directory.GetFiles(folder).Select(Path.GetFileName).Order());
            Assert.Equal(2, ScreenTimeFiles.ReadHistory(folder)!.Rules!["5"].Count);

            // A broken file (a disk that lost it): the one before is read instead.
            File.WriteAllText(Path.Combine(folder, ScreenTimeFiles.HistoryName), "{\"rules\": {\"5\": ");
            Assert.Single(ScreenTimeFiles.ReadHistory(folder)!.Rules!["5"]);

            Assert.True(ScreenTimeFiles.Write(folder, ScreenTimeFiles.LiveName, ScreenTimeFiles.Serialize(ScreenTimeFiles.Live(logs, today)), backup: false));
            Assert.Equal("2026-09-22", ScreenTimeFiles.ReadLive(folder)!.Rules!["5"].Day);
            Assert.DoesNotContain(Directory.GetFiles(folder), file => file.EndsWith(".tmp", StringComparison.Ordinal));
        }
        finally
        {
            try
            {
                Directory.Delete(folder, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    private static TimeZoneInfo? TryZone(string id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception error) when (error is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return null;
        }
    }
}
