// Screen time's counting (Karpo.Dock\ScreenTime\ScreenTimeCount.cs) and the files it reads on a
// start: the window in front, being away, the lock, the sleep, a display going dark, a message lost
// and put right by the next look, midnight, a clock set back, the time since the logon — and the
// files Karpo has written since revision 8, read from a made-up copy of their shape (never the
// person's own: their days are theirs).

using Karpo.Dock;

namespace Karpo.Tests.Islands;

public sealed class ScreenTimeCountTests
{
    private const string Browser = @"C:\Program Files\Example\Browser\browser.exe";
    private const string Editor = @"C:\Users\Someone\AppData\Local\Programs\Editor\editor.exe";
    private const string Packaged = "aumid:Example.Notes_8wekyb3d8bbwe!App";

    /// <summary>A zone with no summer time, an hour ahead of UTC: midnight is 23:00 UTC.</summary>
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.CreateCustomTimeZone("Test Plus One", TimeSpan.FromHours(1), "Test Plus One", "Test Plus One");

    private static DateTime Utc(int day, int hour, int minute = 0, int second = 0) => new(2026, 10, day, hour, minute, second, DateTimeKind.Utc);

    private static DateOnly Date(int day) => new(2026, 10, day);

    /// <summary>A count started at <paramref name="now"/> with nothing in its files, the session unlocked.</summary>
    private static ScreenTimeCount Fresh(DateTime now) =>
        new(new ScreenTimeRead(new(), [], false), Zone, now, logon: null, input: now, lockedNow: false);

    // --- Counting ---------------------------------------------------------------------------------

    [Fact]
    public void EachAppCountsWhileItsWindowIsInFront()
    {
        var count = Fresh(Utc(9, 8));
        count.Foreground(Utc(9, 8), Utc(9, 8), Browser);
        count.Foreground(Utc(9, 8, 20), Utc(9, 8, 20), Editor);
        count.Foreground(Utc(9, 8, 30), Utc(9, 8, 30), Packaged);
        count.Tally(Utc(9, 8, 32), Utc(9, 8, 32));

        var day = count.Log.Day(Date(9))!;
        Assert.Equal(TimeSpan.FromMinutes(32), day.Total);
        Assert.Equal(TimeSpan.FromMinutes(20), day.Apps[Browser]);
        Assert.Equal(TimeSpan.FromMinutes(10), day.Apps[Editor]);
        Assert.Equal(TimeSpan.FromMinutes(2), day.Apps[Packaged]);
        Assert.Equal([Browser, Editor, Packaged], day.Ranked().Select(app => app.App));
    }

    [Fact]
    public void AwayCountsTheIdleTimeAndNoMore()
    {
        var count = Fresh(Utc(9, 8));
        count.Foreground(Utc(9, 8), Utc(9, 8), Browser);
        // Looked at by the minute: the last input at 8:10, so present until 8:15, then away.
        for (var minute = 1; minute <= 40; minute++)
        {
            count.Look(Utc(9, 8).AddMinutes(minute), Utc(9, 8).AddMinutes(Math.Min(minute, 10)), lockedNow: false);
        }
        Assert.Equal(TimeSpan.FromMinutes(10 + ScreenTimeCount.IdleMinutes), count.Log.TotalOn(Date(9)));

        // Back at 9:00: from the look before, as the look cannot tell when in its minute somebody
        // came back, and on from there.
        for (var minute = 41; minute <= 61; minute++)
        {
            count.Look(Utc(9, 8).AddMinutes(minute), Utc(9, 8).AddMinutes(minute < 60 ? 10 : minute), lockedNow: false);
        }
        Assert.Equal(TimeSpan.FromMinutes(17), count.Log.TotalOn(Date(9)));
    }

    [Fact]
    public void NothingCountsWhileTheSessionIsLocked()
    {
        var count = Fresh(Utc(9, 8));
        count.Lock(Utc(9, 8, 10), Utc(9, 8, 10));
        // Somebody's input while locked is the lock screen's.
        count.Look(Utc(9, 8, 30), Utc(9, 8, 29), lockedNow: true);
        count.Unlock(Utc(9, 9), Utc(9, 9));
        count.Tally(Utc(9, 9, 5), Utc(9, 9, 5));

        Assert.Equal(TimeSpan.FromMinutes(15), count.Log.TotalOn(Date(9)));
    }

    [Fact]
    public void NothingCountsWhileThePcSleepsOrItsDisplaysAreDark()
    {
        var count = Fresh(Utc(9, 8));
        count.Sleep(Utc(9, 8, 10), Utc(9, 8, 10));
        // GetTickCount goes on through the sleep: the last input is still 8:10 at the wake.
        count.Wake(Utc(9, 10), Utc(9, 8, 10));
        count.Tally(Utc(9, 10, 1), Utc(9, 10, 1));
        Assert.Equal(TimeSpan.FromMinutes(11), count.Log.TotalOn(Date(9)));

        count.Dark(Utc(9, 10, 2), Utc(9, 10, 1));
        count.Look(Utc(9, 10, 30), Utc(9, 10, 1), lockedNow: false);
        count.Lit(Utc(9, 10, 40), Utc(9, 10, 40));
        count.Tally(Utc(9, 10, 41), Utc(9, 10, 41));
        Assert.Equal(TimeSpan.FromMinutes(13), count.Log.TotalOn(Date(9)));
    }

    [Fact]
    public void AnUnlockThatWasNeverHeardIsPutRightByTheNextLook()
    {
        var count = Fresh(Utc(9, 8));
        count.Lock(Utc(9, 8, 10), Utc(9, 8, 10));
        // The unlock came while the dock was in a menu's loop, and was lost: the look asks Windows.
        count.Look(Utc(9, 9, 1), Utc(9, 9, 1), lockedNow: false);
        count.Look(Utc(9, 9, 2), Utc(9, 9, 2), lockedNow: false);

        Assert.False(count.Away);
        Assert.Equal(TimeSpan.FromMinutes(11), count.Log.TotalOn(Date(9)));
    }

    [Fact]
    public void ALockThatWasNeverHeardStopsTheCountAtTheNextLook()
    {
        var count = Fresh(Utc(9, 8));
        count.Look(Utc(9, 8, 10), Utc(9, 8, 10), lockedNow: true);
        count.Look(Utc(9, 8, 40), Utc(9, 8, 39), lockedNow: true);

        Assert.True(count.Away);
        Assert.Equal(TimeSpan.FromMinutes(10), count.Log.TotalOn(Date(9)));
    }

    [Fact]
    public void AWakeThatWasNeverHeardIsPutRightByInputAfterTheSleep()
    {
        var count = Fresh(Utc(9, 8));
        count.Sleep(Utc(9, 8, 10), Utc(9, 8, 10));
        // No wake arrived; at 10:05 a look sees input from 10:00 on.
        count.Look(Utc(9, 10, 5), Utc(9, 10, 4), lockedNow: false);

        Assert.False(count.Away);
        // 8:00–8:10, and 10:04 (the input) to 10:05.
        Assert.Equal(TimeSpan.FromMinutes(11), count.Log.TotalOn(Date(9)));

        count.Dark(Utc(9, 10, 6), Utc(9, 10, 5));
        count.Look(Utc(9, 10, 20), Utc(9, 10, 19), lockedNow: false);
        Assert.False(count.Away);
        Assert.Equal(TimeSpan.FromMinutes(13), count.Log.TotalOn(Date(9)));
    }

    [Fact]
    public void MidnightSplitsTheCountAndTheDayBeforeIsHistory()
    {
        var count = Fresh(Utc(9, 22, 30));
        count.Foreground(Utc(9, 22, 30), Utc(9, 22, 30), Browser);
        Assert.False(count.HistoryChanged);
        // 23:30 to 00:20 local: the zone's midnight is 23:00 UTC.
        count.Look(Utc(9, 23, 20), Utc(9, 23, 20), lockedNow: false);

        Assert.Equal(Date(10), count.Today(Utc(9, 23, 20)));
        Assert.Equal(TimeSpan.FromMinutes(30), count.Log.TotalOn(Date(9)));
        Assert.Equal(TimeSpan.FromMinutes(20), count.Log.TotalOn(Date(10)));
        Assert.Equal(TimeSpan.FromMinutes(20), count.Log.Day(Date(10))!.Apps[Browser]);
        Assert.True(count.HistoryChanged);
    }

    [Fact]
    public void AClockSetBackCountsNothingBackwards()
    {
        var count = Fresh(Utc(9, 8));
        count.Tally(Utc(9, 8, 30), Utc(9, 8, 30));
        count.Tally(Utc(9, 8, 10), Utc(9, 8, 10));
        count.Tally(Utc(9, 8, 15), Utc(9, 8, 15));

        Assert.Equal(TimeSpan.FromMinutes(35), count.Log.TotalOn(Date(9)));
        Assert.Equal(Utc(9, 8, 15), count.Counted);
    }

    [Fact]
    public void ADayStartedByAKarpoThatCameLaterCountsFromTheLogon()
    {
        var count = new ScreenTimeCount(new ScreenTimeRead(new(), [], false), Zone, Utc(9, 9), logon: Utc(9, 8), input: Utc(9, 9), lockedNow: false);
        Assert.Equal(TimeSpan.FromHours(1), count.Log.TotalOn(Date(9)));

        // A logon of yesterday's, or a day the files already counted, starts now.
        var yesterday = new ScreenTimeCount(new ScreenTimeRead(new(), [], false), Zone, Utc(9, 9), logon: Utc(8, 8), input: Utc(9, 9), lockedNow: false);
        Assert.Equal(TimeSpan.Zero, yesterday.Log.TotalOn(Date(9)));
        var kept = new ScreenTimeLog();
        kept.Add(Utc(9, 6), Utc(9, 7), Browser, Zone);
        var resumed = new ScreenTimeCount(new ScreenTimeRead(new() { [5] = kept }, [5], false), Zone, Utc(9, 9), logon: Utc(9, 5), input: Utc(9, 9), lockedNow: false);
        Assert.Equal(TimeSpan.FromHours(1), resumed.Log.TotalOn(Date(9)));
    }

    [Fact]
    public void ASnapshotIsCountedUpToNowAndItsOwn()
    {
        var count = Fresh(Utc(9, 8));
        var snapshot = count.Snapshot(Utc(9, 8, 10), Utc(9, 8, 10));
        count.Tally(Utc(9, 8, 20), Utc(9, 8, 20));

        Assert.Equal(Date(9), snapshot.Today);
        Assert.Equal(TimeSpan.FromMinutes(10), snapshot.Log.TotalOn(Date(9)));
        Assert.Equal(TimeSpan.FromMinutes(20), count.Log.TotalOn(Date(9)));
    }

    [Fact]
    public void TheLookDropsDaysPastTheKeptOnes()
    {
        var old = new ScreenTimeLog();
        old.Ensure(Date(9).AddDays(-ScreenTimeCount.KeptDays - 5)).Total = TimeSpan.FromHours(1);
        old.Ensure(Date(8)).Total = TimeSpan.FromHours(2);
        var count = new ScreenTimeCount(new ScreenTimeRead(new() { [5] = old }, [], false), Zone, Utc(9, 8), null, Utc(9, 8), false);
        Assert.False(count.HistoryChanged);

        count.Look(Utc(9, 8, 1), Utc(9, 8, 1), lockedNow: false);

        Assert.Equal(Date(8), count.Log.Oldest);
        Assert.True(count.HistoryChanged);
    }

    // --- Another idle time's days -----------------------------------------------------------------

    [Fact]
    public void FilesOfAnotherIdleTimeGoOnAsTheFiveMinutesAndAreKept()
    {
        var ten = new ScreenTimeLog();
        ten.Ensure(Date(8)).Total = TimeSpan.FromHours(3);
        var twenty = new ScreenTimeLog();
        twenty.Ensure(Date(7)).Total = TimeSpan.FromHours(4);
        var count = new ScreenTimeCount(new ScreenTimeRead(new() { [10] = ten, [20] = twenty }, [], false), Zone, Utc(9, 8), null, Utc(9, 8), false);

        // The nearest to five minutes is the ten's: its days are the count's now, and it is written.
        Assert.Equal(TimeSpan.FromHours(3), count.Log.TotalOn(Date(8)));
        Assert.Equal(TimeSpan.Zero, count.Log.TotalOn(Date(7)));
        Assert.True(count.HistoryChanged);
        Assert.Equal([5, 10, 20], count.Logs.Keys);
        Assert.Same(twenty, count.Logs[20]);
    }

    // --- The files as Karpo wrote them ------------------------------------------------------------

    /// <summary>
    /// screentime.json as Karpo wrote it from revision 8 on (2026-09-26's shape): the five minutes'
    /// rule both at the top, as before revision 8, and under "rules", with today's apps by key in
    /// seconds and two weeks' totals. Made up: no app or path of anybody's.
    /// </summary>
    private const string Live = """
        {"day":"2026-09-26","seconds":16260,"since":"2026-09-26T00:00:00","earlier":{"2026-09-17":16666,"2026-09-21":3554,"2026-09-25":11615},"idle":5,
         "rules":{"5":{"apps":{"C:\\Program Files\\Example\\Browser\\browser.exe":6962.250834,"aumid:Example.Notes_8wekyb3d8bbwe!App":5489.0894888,"C:\\Windows\\explorer.exe":75.951},
          "day":"2026-09-26","seconds":16260,"since":"2026-09-26T00:00:00","earlier":{"2026-09-17":16666,"2026-09-21":3554,"2026-09-25":11615}}}}
        """;

    /// <summary>screentime-history.json as Karpo wrote it: every day but the live one, a day's apps in tenths of a second, where it kept them.</summary>
    private const string History = """
        {"rules":{"5":{"2026-09-17":{"seconds":16666},"2026-09-21":{"seconds":3554},
         "2026-09-25":{"seconds":11615,"since":"2026-09-25T07:12:03","apps":{"C:\\Program Files\\Example\\Browser\\browser.exe":8000.4,"aumid:Example.Notes_8wekyb3d8bbwe!App":3500.2}}}}}
        """;

    [Fact]
    public void TheFilesKarpoWroteAreReadAsTheyAre()
    {
        var today = new DateOnly(2026, 9, 26);
        var read = ScreenTimeFiles.Read(ScreenTimeFiles.ParseLive(Live), ScreenTimeFiles.ParseHistory(History), today, ScreenTimeCount.IdleMinutes);

        Assert.Equal([5], read.Logs.Keys);
        Assert.Equal([5], read.Resumed);
        Assert.False(read.HistoryStale);
        var log = read.Logs[5];
        Assert.Equal([new DateOnly(2026, 9, 17), new DateOnly(2026, 9, 21), new DateOnly(2026, 9, 25), today], log.Days.Select(day => day.Date));
        Assert.Equal(TimeSpan.FromSeconds(16260), log.TotalOn(today));
        Assert.Equal(new DateTime(2026, 9, 26, 0, 0, 0), log.Day(today)!.Since);
        Assert.Equal(TimeSpan.FromSeconds(6962.250834), log.Day(today)!.Apps[@"C:\Program Files\Example\Browser\browser.exe"]);
        Assert.Equal(TimeSpan.FromSeconds(11615), log.TotalOn(new DateOnly(2026, 9, 25)));
        Assert.Equal(TimeSpan.FromSeconds(3500.2), log.Day(new DateOnly(2026, 9, 25))!.Apps[Packaged]);
        Assert.Equal(new DateTime(2026, 9, 25, 7, 12, 3), log.Day(new DateOnly(2026, 9, 25))!.Since);
        Assert.Empty(log.Day(new DateOnly(2026, 9, 17))!.Apps);
    }

    [Fact]
    public void TheFilesKarpoWroteComeBackInTheirOwnShape()
    {
        var today = new DateOnly(2026, 9, 26);
        var read = ScreenTimeFiles.Read(ScreenTimeFiles.ParseLive(Live), ScreenTimeFiles.ParseHistory(History), today, ScreenTimeCount.IdleMinutes);

        var live = ScreenTimeFiles.Serialize(ScreenTimeFiles.Live(read.Logs, today));
        var history = ScreenTimeFiles.Serialize(ScreenTimeFiles.History(read.Logs, today));

        // The keys an older Karpo reads, where it read them.
        Assert.StartsWith("{\"day\":\"2026-09-26\",\"seconds\":16260,\"since\":\"2026-09-26T00:00:00\",\"earlier\":{", live, StringComparison.Ordinal);
        Assert.Contains("\"idle\":5,\"rules\":{\"5\":{\"apps\":{", live, StringComparison.Ordinal);
        Assert.StartsWith("{\"rules\":{\"5\":{\"2026-09-17\":{\"seconds\":16666}", history, StringComparison.Ordinal);
        // And they read back as the same days.
        var again = ScreenTimeFiles.Read(ScreenTimeFiles.ParseLive(live), ScreenTimeFiles.ParseHistory(history), today, ScreenTimeCount.IdleMinutes);
        Assert.Equal(read.Logs[5].Days.Select(day => (day.Date, day.Total)), again.Logs[5].Days.Select(day => (day.Date, day.Total)));
        Assert.Equal(TimeSpan.FromSeconds(3500.2), again.Logs[5].Day(new DateOnly(2026, 9, 25))!.Apps[Packaged]);
    }

    [Fact]
    public void ABrokenHistoryIsReadFromItsBackup()
    {
        var folder = Path.Combine(Path.GetTempPath(), "karpo-screentime-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            File.WriteAllText(Path.Combine(folder, ScreenTimeFiles.HistoryName), "{\"rules\":{\"5\":{\"2026-09-1");
            File.WriteAllText(Path.Combine(folder, ScreenTimeFiles.HistoryName + ".bak"), History);
            File.WriteAllText(Path.Combine(folder, ScreenTimeFiles.LiveName), Live);

            var history = ScreenTimeFiles.ReadHistory(folder);
            var read = ScreenTimeFiles.Read(ScreenTimeFiles.ReadLive(folder), history, new DateOnly(2026, 9, 26), ScreenTimeCount.IdleMinutes);

            Assert.NotNull(history);
            Assert.Equal(TimeSpan.FromSeconds(11615), read.Logs[5].TotalOn(new DateOnly(2026, 9, 25)));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void ADayAfterTheLiveFilesDayStartsAndTheLiveDayIsHistory()
    {
        // Karpo starts on the 27th with the 26th's file: the 26th is a day of the history now.
        var today = new DateOnly(2026, 9, 27);
        var read = ScreenTimeFiles.Read(ScreenTimeFiles.ParseLive(Live), ScreenTimeFiles.ParseHistory(History), today, ScreenTimeCount.IdleMinutes);
        var count = new ScreenTimeCount(read, Zone, new DateTime(2026, 9, 27, 7, 0, 0, DateTimeKind.Utc), null, new DateTime(2026, 9, 27, 7, 0, 0, DateTimeKind.Utc), false);

        Assert.Empty(read.Resumed);
        Assert.True(count.HistoryChanged);
        Assert.Equal(TimeSpan.FromSeconds(16260), count.Log.TotalOn(new DateOnly(2026, 9, 26)));
        Assert.Contains("\"2026-09-26\":{\"seconds\":16260", ScreenTimeFiles.Serialize(ScreenTimeFiles.History(count.Logs, today)), StringComparison.Ordinal);
    }
}
