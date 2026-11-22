// CalendarStore (Karpo.Dock\CalendarStore.cs): a day's events in order, events
// over several days, an event without a length, the same event from two
// calendars; and the calendar's texts and month grid.
//
// There is no download and no kept file here: the accounts are read by
// AccountCalendars (Karpo.Tests\Pim\CalendarOpeningTests.cs), and the store only
// merges what its sources have.

using System.Globalization;
using Karpo.Dock;

namespace Karpo.Tests.Islands;

public sealed class CalendarStoreTests
{

    private static CalendarEvent Timed(string title, int day, int hour, int hours) =>
        new(new DateTime(2026, 9, day, hour, 0, 0), new DateTime(2026, 9, day, hour, 0, 0).AddHours(hours), false, title, null);

    private static CalendarEvent AllDay(string title, int day, int days = 1) =>
        new(new DateTime(2026, 9, day), new DateTime(2026, 9, day).AddDays(days), true, title, null);

    private static readonly DateOnly Sep17 = new(2026, 9, 17);

    [Fact]
    public void ADayListsAllDayEventsFirstThenByStart()
    {
        var events = new[] { Timed("late", 17, 15, 1), AllDay("holiday", 17), Timed("early", 17, 9, 1), Timed("other day", 18, 9, 1) };

        var day = CalendarStore.OnDay(events, Sep17);

        Assert.Equal(["holiday", "early", "late"], day.Select(item => item.Title));
    }

    [Fact]
    public void AnEventOverSeveralDaysIsOnEachOfThem()
    {
        var trip = AllDay("trip", 16, 3);
        var night = Timed("night shift", 17, 22, 4);

        Assert.Equal(["trip"], CalendarStore.OnDay([trip], new DateOnly(2026, 9, 16)).Select(item => item.Title));
        Assert.Equal(["trip"], CalendarStore.OnDay([trip], new DateOnly(2026, 9, 18)).Select(item => item.Title));
        Assert.Empty(CalendarStore.OnDay([trip], new DateOnly(2026, 9, 19)));
        Assert.Single(CalendarStore.OnDay([night], Sep17));
        Assert.Single(CalendarStore.OnDay([night], new DateOnly(2026, 9, 18)));
    }

    [Fact]
    public void AnEventWithoutLengthIsOnTheDayItStarts()
    {
        var reminder = new CalendarEvent(new DateTime(2026, 9, 17), new DateTime(2026, 9, 17), false, "at midnight", null);

        Assert.Single(CalendarStore.OnDay([reminder], Sep17));
        Assert.Empty(CalendarStore.OnDay([reminder], new DateOnly(2026, 9, 16)));
    }

    [Fact]
    public void TheSameEventFromTwoCalendarsIsListedOnce()
    {
        var meeting = Timed("meeting", 17, 10, 1);

        Assert.Single(CalendarStore.OnDay([meeting, meeting], Sep17));
    }

    [Fact]
    public void ChangedIsRaisedOnceForAllNewsWhenDelivered()
    {
        var store = new CalendarStore();
        var raised = 0;
        store.Changed += () => raised++;
        var calendar = new ListCalendar("test:one", "One", default, []);

        store.Add(calendar);
        calendar.Raise();
        store.Deliver();
        store.Deliver();

        Assert.Equal(1, raised);
    }

    [Fact]
    public void AStoreWithNoAccountsHasNoCalendarAndNoTroubleToReport()
    {
        // What every test and offscreen picture holds: nothing to connect to, so nothing to say.
        var store = new CalendarStore();

        Assert.False(store.HasCalendars);
        Assert.False(store.Loading);
        Assert.Null(store.Problem);
        Assert.Empty(store.Sources);
        Assert.Empty(store.EventsOn(Sep17));
    }

    [Fact]
    public void SpansSayWhatFallsOnTheDay()
    {
        static string Time(DateTime at) => at.ToString("HH:mm", CultureInfo.InvariantCulture);
        var day = Sep17;

        Assert.Equal("All day", CalendarText.Span(AllDay("x", 17), day, Time));
        Assert.Equal("09:00–10:00", CalendarText.Span(Timed("x", 17, 9, 1), day, Time));
        Assert.Equal("From 22:00", CalendarText.Span(Timed("x", 17, 22, 4), day, Time));
        Assert.Equal("Until 02:00", CalendarText.Span(Timed("x", 16, 22, 4), day, Time));
        Assert.Equal("All day", CalendarText.Span(new CalendarEvent(new DateTime(2026, 9, 16, 8, 0, 0), new DateTime(2026, 9, 18, 8, 0, 0), false, "x", null), day, Time));
        Assert.Equal("12:00", CalendarText.Span(new CalendarEvent(new DateTime(2026, 9, 17, 12, 0, 0), new DateTime(2026, 9, 17, 12, 0, 0), false, "x", null), day, Time));
    }

    [Fact]
    public void DayNamesAreRelativeNearToday()
    {
        static string Long(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        Assert.Equal("Today", CalendarText.DayName(Sep17, Sep17, Long));
        Assert.Equal("Tomorrow", CalendarText.DayName(Sep17.AddDays(1), Sep17, Long));
        Assert.Equal("Yesterday", CalendarText.DayName(Sep17.AddDays(-1), Sep17, Long));
        Assert.Equal("2026-09-20", CalendarText.DayName(Sep17.AddDays(3), Sep17, Long));
    }

    [Theory]
    [InlineData(2026, 9, DayOfWeek.Monday, "2026-08-31")]
    [InlineData(2026, 9, DayOfWeek.Sunday, "2026-08-30")]
    [InlineData(2026, 9, DayOfWeek.Tuesday, "2026-09-01")]
    [InlineData(2026, 2, DayOfWeek.Sunday, "2026-02-01")]
    public void TheGridStartsOnTheFirstDayOfTheWeek(int year, int month, DayOfWeek first, string expected) =>
        Assert.Equal(DateOnly.ParseExact(expected, "yyyy-MM-dd", CultureInfo.InvariantCulture), CalendarGrid.First(year, month, first));

    [Theory]
    [InlineData("2026-01-31", 1, "2026-02-28")]
    [InlineData("2026-03-31", -1, "2026-02-28")]
    [InlineData("2026-12-15", 1, "2027-01-15")]
    [InlineData("2026-01-15", -1, "2025-12-15")]
    [InlineData("2024-01-31", 1, "2024-02-29")]
    public void MonthStepsKeepTheDayWhereTheMonthHasIt(string from, int months, string expected) =>
        Assert.Equal(
            DateOnly.ParseExact(expected, "yyyy-MM-dd", CultureInfo.InvariantCulture),
            CalendarGrid.AddMonths(DateOnly.ParseExact(from, "yyyy-MM-dd", CultureInfo.InvariantCulture), months));
}
