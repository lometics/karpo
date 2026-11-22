// The alarm as numbers (Karpo.Core\Alarms.cs): when it rings next — its days, a time already past,
// summer time's gap and winter time's repeated hour — and the book that rings it: once, late only by
// a little after sleep or a clock set forward, never again after the clock goes back, snoozed and
// dismissed, and one that does not repeat turning itself off. Its options as the file has them, and
// where it stands. Nothing here draws.

using Karpo.Dock;
using Karpo.Tests.Core;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Calendars;

public sealed class AlarmTests
{
    /// <summary>Central Europe: summer time from 02:00 on 29 March 2026 and back at 03:00 on 25 October 2026.</summary>
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");

    /// <summary>A wall-clock time in <see cref="Zone"/> as UTC.</summary>
    private static DateTime At(int month, int day, int hour, int minute = 0, int second = 0) =>
        TimeZoneInfo.ConvertTimeToUtc(new DateTime(2026, month, day, hour, minute, second, DateTimeKind.Unspecified), Zone);

    private static DateTime Wall(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(utc, Zone);

    private static AlarmOptions Options(string time, bool on = true, params string[] days) =>
        new() { Time = time, On = on, Days = days.Length > 0 ? days : null };

    private static AlarmPlan Plan(string time, params string[] days) => AlarmPlan.Of(Options(time, true, days));

    /// <summary>A book on <see cref="Zone"/> holding one alarm, "a", synced at <paramref name="armed"/>; what rang and what was switched, counted.</summary>
    private static (AlarmBook Book, List<string> Rang, List<string> Switched) Book(AlarmOptions options, DateTime armed)
    {
        var book = new AlarmBook(() => Zone);
        var rang = new List<string>();
        var switched = new List<string>();
        book.Rang += rang.Add;
        book.Switched += switched.Add;
        book.Sync([("a", options)], armed);
        return (book, rang, switched);
    }

    // --- When it rings next -----------------------------------------------------------------

    /// <summary>Set before its time, today's; at or after it, tomorrow's — never a moment already past.</summary>
    [Fact]
    public void ItRingsAtTheNextComingOfItsTime()
    {
        var plan = Plan("07:00");
        Assert.Equal(At(10, 12, 7), AlarmSchedule.Next(plan, At(10, 12, 6), Zone));
        Assert.Equal(At(10, 13, 7), AlarmSchedule.Next(plan, At(10, 12, 7), Zone));
        Assert.Equal(At(10, 13, 7), AlarmSchedule.Next(plan, At(10, 12, 7, 30), Zone));
        // Just before midnight, the next morning; just after, the same.
        Assert.Equal(At(10, 13, 7), AlarmSchedule.Next(plan, At(10, 12, 23, 59), Zone));
        Assert.Equal(At(10, 13, 7), AlarmSchedule.Next(plan, At(10, 13, 0, 1), Zone));
    }

    /// <summary>One that repeats rings on its days alone, the next of them; every day is every day.</summary>
    [Fact]
    public void ItRepeatsOnItsDays()
    {
        // 12 October 2026 is a Monday.
        var plan = Plan("07:00", "mon", "wed");
        Assert.Equal(At(10, 14, 7), AlarmSchedule.Next(plan, At(10, 12, 8), Zone));
        Assert.Equal(At(10, 14, 7), AlarmSchedule.Next(plan, At(10, 13, 12), Zone));
        Assert.Equal(At(10, 19, 7), AlarmSchedule.Next(plan, At(10, 14, 7), Zone));
        Assert.Equal(At(10, 18, 9), AlarmSchedule.Next(Plan("09:00", "sun"), At(10, 12, 7), Zone));
        var daily = Plan("22:15", "mon", "tue", "wed", "thu", "fri", "sat", "sun");
        Assert.Equal(At(10, 12, 22, 15), AlarmSchedule.Next(daily, At(10, 12, 22), Zone));
        Assert.Equal(At(10, 13, 22, 15), AlarmSchedule.Next(daily, At(10, 12, 22, 15), Zone));
    }

    /// <summary>A time summer time skips rings at the first minute after the gap; the day before and after, at its time.</summary>
    [Fact]
    public void ATimeSummerTimeSkipsRingsAfterTheGap()
    {
        var plan = Plan("02:30");
        var skipped = AlarmSchedule.Next(plan, At(3, 28, 12), Zone);
        Assert.Equal(new DateTime(2026, 3, 29, 3, 0, 0), Wall(skipped));
        Assert.Equal(new DateTime(2026, 3, 29, 1, 0, 0, DateTimeKind.Utc), skipped);
        Assert.Equal(new DateTime(2026, 3, 28, 2, 30, 0), Wall(AlarmSchedule.Next(plan, At(3, 27, 12), Zone)));
        Assert.Equal(new DateTime(2026, 3, 30, 2, 30, 0), Wall(AlarmSchedule.Next(plan, skipped, Zone)));
    }

    /// <summary>A time winter time says twice rings at the first of them, once: the second is no new ring.</summary>
    [Fact]
    public void ATimeWinterTimeRepeatsRingsOnce()
    {
        var plan = Plan("02:30", "mon", "tue", "wed", "thu", "fri", "sat", "sun");
        var first = AlarmSchedule.Next(plan, At(10, 24, 12), Zone);
        // 02:30 summer time is 00:30 UTC; the same wall time again, in winter time, is 01:30 UTC.
        Assert.Equal(new DateTime(2026, 10, 25, 0, 30, 0, DateTimeKind.Utc), first);
        Assert.Equal(new DateTime(2026, 10, 26, 1, 30, 0, DateTimeKind.Utc), AlarmSchedule.Next(plan, first, Zone));

        var (book, rang, _) = Book(Options("02:30", true, "sun"), At(10, 24, 12));
        book.Tick(first);
        book.Dismiss("a", first.AddMinutes(1));
        book.Tick(new DateTime(2026, 10, 25, 1, 30, 0, DateTimeKind.Utc));
        book.Tick(new DateTime(2026, 10, 25, 1, 31, 0, DateTimeKind.Utc));
        Assert.Equal(["a"], rang);
    }

    // --- The book ------------------------------------------------------------------------

    /// <summary>
    /// At its time it rings, once however often it is looked at; nobody answering, it stops after
    /// five minutes, and one that does not repeat is then off — in the very options it was handed.
    /// </summary>
    [Fact]
    public void ItRingsOnceAndAOneShotTurnsItselfOff()
    {
        var options = Options("07:00");
        var (book, rang, switched) = Book(options, At(10, 12, 6));
        Assert.Equal(At(10, 12, 7), book.Due(At(10, 12, 6)));
        Assert.False(book.Tick(At(10, 12, 6, 59, 59)));
        Assert.Empty(rang);

        Assert.True(book.Tick(At(10, 12, 7)));
        book.Tick(At(10, 12, 7, 0, 1));
        book.Tick(At(10, 12, 7, 2));
        Assert.Equal(["a"], rang);
        Assert.True(book.Ringing);
        Assert.Equal(AlarmPhase.Ringing, book.Of("a", options, At(10, 12, 7, 2)).Phase);
        Assert.Equal(At(10, 12, 7) + AlarmBook.RingFor, book.Due(At(10, 12, 7, 2)));

        book.Tick(At(10, 12, 7) + AlarmBook.RingFor);
        Assert.False(book.Ringing);
        Assert.False(options.On);
        Assert.Equal(["a"], switched);
        Assert.Equal(AlarmPhase.Off, book.Of("a", options, At(10, 12, 8)).Phase);
        Assert.Null(book.Due(At(10, 12, 8)));
        // Off, the next morning comes and goes.
        book.Tick(At(10, 13, 7));
        Assert.Equal(["a"], rang);
    }

    /// <summary>One that repeats rings on each of its days, and stays on.</summary>
    [Fact]
    public void ARepeatingAlarmStaysOn()
    {
        var options = Options("07:00", true, "mon", "tue");
        var (book, rang, switched) = Book(options, At(10, 11, 20));
        book.Tick(At(10, 12, 7));
        book.Dismiss("a", At(10, 12, 7, 1));
        Assert.True(options.On);
        Assert.Empty(switched);
        Assert.Equal(At(10, 13, 7), book.Of("a", options, At(10, 12, 7, 1)).Next);
        book.Tick(At(10, 13, 7, 0, 30));
        Assert.Equal(["a", "a"], rang);
        book.Dismiss("a", At(10, 13, 7, 1));
        Assert.Equal(At(10, 19, 7), book.Of("a", options, At(10, 13, 8)).Next);
    }

    /// <summary>
    /// A PC asleep through it: woken a few minutes late, it rings once then; woken later than five
    /// minutes, it is missed — no ring at all — and one that does not repeat is off.
    /// </summary>
    [Fact]
    public void LateBySleepItRingsOnlyIfJustLate()
    {
        var late = Book(Options("07:00"), At(10, 11, 23));
        late.Book.Tick(At(10, 12, 7, 3));
        Assert.Equal(["a"], late.Rang);

        var options = Options("07:00");
        var missed = Book(options, At(10, 11, 23));
        Assert.True(missed.Book.Tick(At(10, 12, 7, 10)));
        Assert.Empty(missed.Rang);
        Assert.False(options.On);
        Assert.Equal(["a"], missed.Switched);

        // Asleep for days, one that repeats steps over every morning it missed and rings the next.
        var daily = Options("07:00", true, "mon", "tue", "wed", "thu", "fri", "sat", "sun");
        var away = Book(daily, At(10, 11, 23));
        away.Book.Tick(At(10, 15, 12));
        Assert.Empty(away.Rang);
        Assert.True(daily.On);
        Assert.Equal(At(10, 16, 7), away.Book.Of("a", daily, At(10, 15, 12)).Next);
        away.Book.Tick(At(10, 16, 7));
        Assert.Equal(["a"], away.Rang);
    }

    /// <summary>
    /// Windows' clock set back after a ring never rings it again, set forward past its time by a
    /// little it rings, by a lot it is missed: each ring is reckoned after the last.
    /// </summary>
    [Fact]
    public void TheClockGoingBackNeverRingsItAgain()
    {
        var options = Options("07:00", true, "mon", "tue", "wed", "thu", "fri", "sat", "sun");
        var (book, rang, _) = Book(options, At(10, 12, 6));
        book.Tick(At(10, 12, 7));
        book.Dismiss("a", At(10, 12, 7, 1));
        // An hour back, and forward through seven again.
        book.Tick(At(10, 12, 6, 1));
        book.Tick(At(10, 12, 6, 59));
        book.Tick(At(10, 12, 7));
        book.Tick(At(10, 12, 7, 1));
        Assert.Equal(["a"], rang);
        // The next morning it rings.
        book.Tick(At(10, 13, 7));
        Assert.Equal(["a", "a"], rang);

        // Set back before it was even switched on: the morning still comes.
        var early = Book(Options("07:00"), At(10, 12, 8));
        early.Book.Tick(At(10, 12, 5));
        Assert.Equal(At(10, 12, 7), early.Book.Of("a", null, At(10, 12, 5)).Next);
        early.Book.Tick(At(10, 12, 7));
        Assert.Equal(["a"], early.Rang);
    }

    /// <summary>Snoozed it stops and rings again ten minutes on; dismissed while snoozed, it is over.</summary>
    [Fact]
    public void ItSnoozesAndIsDismissed()
    {
        var options = Options("07:00");
        var (book, rang, switched) = Book(options, At(10, 12, 6));
        book.Snooze("a", At(10, 12, 6, 30));
        Assert.Equal(AlarmPhase.Set, book.Of("a", options, At(10, 12, 6, 30)).Phase);

        book.Tick(At(10, 12, 7));
        book.Snooze("a", At(10, 12, 7, 1));
        Assert.False(book.Ringing);
        var snoozed = book.Of("a", options, At(10, 12, 7, 1));
        Assert.Equal(AlarmPhase.Snoozed, snoozed.Phase);
        Assert.Equal(At(10, 12, 7, 11), snoozed.Until);
        Assert.Equal(At(10, 12, 7, 11), book.Due(At(10, 12, 7, 5)));
        book.Tick(At(10, 12, 7, 10));
        Assert.Equal(["a"], rang);
        book.Tick(At(10, 12, 7, 11));
        Assert.Equal(["a", "a"], rang);
        Assert.True(options.On);

        book.Snooze("a", At(10, 12, 7, 12));
        book.Dismiss("a", At(10, 12, 7, 13));
        Assert.False(options.On);
        Assert.Equal(["a"], switched);
        book.Tick(At(10, 12, 7, 22));
        Assert.Equal(["a", "a"], rang);
    }

    /// <summary>
    /// Its switch, flipped on its line: on, it is set from now, a time already past today being
    /// tomorrow's; off, a ring stops. Each flip is in its options and said (Switched).
    /// </summary>
    [Fact]
    public void ItsSwitchSetsItFromNow()
    {
        var options = Options("07:00", on: false);
        var (book, rang, switched) = Book(options, At(10, 12, 6));
        Assert.Equal(AlarmPhase.Off, book.Of("a", options, At(10, 12, 7, 30)).Phase);
        book.Switch("a", options, At(10, 12, 7, 30));
        Assert.True(options.On);
        Assert.Equal(new AlarmStatus(AlarmPhase.Set, At(10, 13, 7)), book.Of("a", options, At(10, 12, 7, 30)));
        book.Tick(At(10, 13, 7));
        Assert.True(book.Ringing);
        book.Switch("a", options, At(10, 13, 7, 1));
        Assert.False(options.On);
        Assert.False(book.Ringing);
        Assert.Equal(["a", "a"], switched);
        Assert.Equal(["a"], rang);
    }

    /// <summary>
    /// Handed the config's alarms: one Karpo meets at its start, or set to another time, is set from
    /// then — nothing rings for a time before it; the same alarm read again (its options a new object
    /// after the file was read) keeps what it was doing; one removed goes.
    /// </summary>
    [Fact]
    public void SyncSetsFromNowAndKeepsWhatItWasDoing()
    {
        var (book, rang, _) = Book(Options("07:00"), At(10, 12, 7, 2));
        book.Tick(At(10, 12, 7, 2));
        Assert.Empty(rang);
        Assert.Equal(At(10, 13, 7), book.Of("a", null, At(10, 12, 7, 2)).Next);

        book.Tick(At(10, 13, 7));
        Assert.True(book.Ringing);
        book.Sync([("a", Options("07:00"))], At(10, 13, 7, 1));
        Assert.True(book.Ringing);
        // Set to 07:30 in Settings: its ring is over and it waits for the new time.
        book.Sync([("a", Options("07:30"))], At(10, 13, 7, 2));
        Assert.False(book.Ringing);
        Assert.Equal(At(10, 13, 7, 30), book.Of("a", null, At(10, 13, 7, 2)).Next);
        book.Sync([], At(10, 13, 7, 3));
        Assert.Empty(book.Keys);
        Assert.Null(book.Due(At(10, 13, 7, 3)));
    }

    // --- What its line says --------------------------------------------------------------

    /// <summary>"Off", "Ringing", "Snoozed until …", its days, or "Today" and "Tomorrow" for one that rings once.</summary>
    [Fact]
    public void ItsHintSaysWhatItWillDo()
    {
        static string Name(DayOfWeek day) => day.ToString()[..3];
        static string Time(DateTime at) => $"{at:HH:mm}";
        string Hint(AlarmStatus status, AlarmPlan plan, DateTime utc) => AlarmWords.Hint(status, plan, utc, Zone, DayOfWeek.Monday, Name, Time);

        var once = Plan("07:00");
        Assert.Equal(S.DockAlarmOff, Hint(new AlarmStatus(AlarmPhase.Off), once, At(10, 12, 6)));
        Assert.Equal(S.DockAlarmRinging, Hint(new AlarmStatus(AlarmPhase.Ringing, At(10, 13, 7)), once, At(10, 12, 7)));
        Assert.Equal(S.DockAlarmSnoozedUntil("07:11"), Hint(new AlarmStatus(AlarmPhase.Snoozed, At(10, 13, 7), At(10, 12, 7, 11)), once, At(10, 12, 7, 1)));
        Assert.Equal(S.DockAlarmToday, Hint(new AlarmStatus(AlarmPhase.Set, At(10, 12, 7)), once, At(10, 12, 6)));
        Assert.Equal(S.DockAlarmTomorrow, Hint(new AlarmStatus(AlarmPhase.Set, At(10, 13, 7)), once, At(10, 12, 8)));
        // Just before midnight, tomorrow is tomorrow's: the wall clock's day, not UTC's.
        Assert.Equal(S.DockAlarmTomorrow, Hint(new AlarmStatus(AlarmPhase.Set, At(10, 13, 7)), once, At(10, 12, 23, 59)));
        Assert.Equal("Mon, Wed", Hint(new AlarmStatus(AlarmPhase.Set, At(10, 14, 7)), Plan("07:00", "wed", "mon"), At(10, 12, 8)));
    }

    /// <summary>Every day, weekdays and weekends by their names; other days by theirs, in the region's week.</summary>
    [Fact]
    public void ItsDaysAreNamedAsAWeek()
    {
        static string Name(DayOfWeek day) => day.ToString()[..3];
        Assert.Equal(S.DockAlarmEveryDay, AlarmWords.Days(AlarmDays.Every, DayOfWeek.Monday, Name));
        Assert.Equal(S.DockAlarmWeekdays, AlarmWords.Days(AlarmDays.Mask(["mon", "tue", "wed", "thu", "fri"]), DayOfWeek.Monday, Name));
        Assert.Equal(S.DockAlarmWeekends, AlarmWords.Days(AlarmDays.Mask(["sat", "sun"]), DayOfWeek.Monday, Name));
        var some = AlarmDays.Mask(["sun", "mon", "fri"]);
        Assert.Equal("Mon, Fri, Sun", AlarmWords.Days(some, DayOfWeek.Monday, Name));
        Assert.Equal("Sun, Mon, Fri", AlarmWords.Days(some, DayOfWeek.Sunday, Name));
    }

    // --- Its options, and where it stands ---------------------------------------------------

    /// <summary>A time that is none reads as seven; days this build knows, each once, Monday first; none is left out.</summary>
    [Fact]
    public void ItsOptionsAreWhatTheyMayBe()
    {
        Assert.Equal(7 * 60 + 5, AlarmTimes.Parse("7:05"));
        Assert.Equal(23 * 60 + 59, AlarmTimes.Parse("23:59"));
        foreach (var nonsense in new[] { null, "", "7", "7:5", "24:00", "07:60", "-1:00", "seven", "07:00:00" })
        {
            Assert.Null(AlarmTimes.Parse(nonsense));
        }
        Assert.Equal("00:00", AlarmTimes.Text(AlarmTimes.Day));

        var options = new AlarmOptions { Time = "6:45", Days = ["Wed", "mon", "xyz", "mon"], Name = "  Wake   up \n" };
        options.Normalize();
        Assert.Equal("06:45", options.Time);
        Assert.Equal(["mon", "wed"], options.Days);
        Assert.Equal("Wake   up", options.Name);
        options.Days = ["nope"];
        options.Time = "late";
        options.Normalize();
        Assert.Null(options.Days);
        Assert.Equal("07:00", options.Time);
    }

    /// <summary>
    /// A new alarm line has its record, 07:00 and off; what is set is written and read back; a
    /// file from before the alarm — no "alarm" anywhere — reads with everything else as it was.
    /// </summary>
    [Fact]
    public void ItsOptionsGoThroughTheFile()
    {
        var line = ListKind.Alarm.New();
        line.Normalize();
        Assert.NotNull(line.Alarm);
        Assert.Equal("07:00", line.Alarm!.Time);
        Assert.False(line.Alarm.On);

        var folder = BarKind.FolderList.New();
        line.Id = "wake";
        line.Alarm = new AlarmOptions { Time = "06:30", Days = ["mon", "fri"], On = true, Name = "Gym" };
        folder.Folder!.Put(line);
        var config = Configs.With(folder);
        var read = Configs.RoundTrip(config, out _);
        var kept = Karpo.Core.Folders.All(read.DefaultLayout.Islands).Select(pair => pair.Item2).Single(island => island.Id == "wake");
        Assert.Equal(WidgetInfos.Alarm, kept.Info);
        Assert.Equal("06:30", kept.Alarm!.Time);
        Assert.Equal(["mon", "fri"], kept.Alarm.Days!);
        Assert.True(kept.Alarm.On);
        Assert.Equal("Gym", kept.Alarm.Name);
        Assert.Single(TheDock.AlarmsIn(read));

        // Another widget never carries one, and the file says nothing of it.
        var stopwatch = ListKind.Stopwatch.New();
        stopwatch.Alarm = new AlarmOptions();
        stopwatch.Normalize();
        Assert.Null(stopwatch.Alarm);
        Assert.DoesNotContain("\"alarm\"", new Config().ToJson(), StringComparison.Ordinal);
    }

    /// <summary>It is a list line of one row to six in the Time group, in the library's list page, with no taskbar entry and no preset.</summary>
    [Fact]
    public void ItStandsBesideTheStopwatch()
    {
        Assert.Null(BarEntries.Find("alarm"));
        var line = ListWidgets.Of(ListKind.Alarm);
        Assert.Equal(WidgetSource.Alarm, line.Drawing);
        Assert.Equal(ListLine.Card, line.Line);
        Assert.Equal(CellAct.Nothing, line.Act);
        Assert.Equal((2, 12), (line.Range.Least, line.Range.Most));
        Assert.Equal(WidgetCategory.Time, WidgetInfos.Alarm.Category);
        Assert.Equal(WidgetOptionsKind.Alarm, WidgetInfos.Alarm.Options);
        Assert.Contains(Catalogue.On(WidgetType.List), row => row.Key == "list/alarm");
        Assert.DoesNotContain(Catalogue.On(WidgetType.Bar), row => row.Key == "bar/alarm");
        Assert.Equal("alarm", ListKind.Alarm.Id());
    }
}
