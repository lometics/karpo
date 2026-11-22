// CalendarNavigation (Karpo.Dock\Calendar\CalendarNavigation.cs): the days each
// view shows, page turns over month ends and year boundaries, what the keys do
// in each view, view switches, and week numbers as regions count them.

using System.Globalization;
using Karpo.Dock;

namespace Karpo.Tests.Calendars;

public sealed class CalendarNavigationTests
{
    private static DateOnly D(string text) => DateOnly.ParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    [Theory]
    [InlineData("2026-09-22", DayOfWeek.Monday, "2026-09-21")]
    [InlineData("2026-09-22", DayOfWeek.Sunday, "2026-09-20")]
    [InlineData("2026-09-22", DayOfWeek.Saturday, "2026-09-19")]
    [InlineData("2026-09-21", DayOfWeek.Monday, "2026-09-21")]
    // Across the new year.
    [InlineData("2027-01-01", DayOfWeek.Monday, "2026-12-28")]
    public void AWeekStartsOnTheRegionsFirstDay(string day, DayOfWeek first, string expected) =>
        Assert.Equal(D(expected), CalendarNavigation.WeekStart(D(day), first));

    [Theory]
    [InlineData(CalendarView.Day, "2026-09-22", DayOfWeek.Monday, "2026-09-22", "2026-09-23")]
    [InlineData(CalendarView.Week, "2026-09-22", DayOfWeek.Monday, "2026-09-21", "2026-09-28")]
    [InlineData(CalendarView.Week, "2026-09-22", DayOfWeek.Sunday, "2026-09-20", "2026-09-27")]
    // The month's six weeks, the days of August and October in its grid included.
    [InlineData(CalendarView.Month, "2026-09-22", DayOfWeek.Monday, "2026-08-31", "2026-10-12")]
    [InlineData(CalendarView.Month, "2026-02-10", DayOfWeek.Sunday, "2026-02-01", "2026-03-15")]
    [InlineData(CalendarView.Year, "2026-09-22", DayOfWeek.Monday, "2026-01-01", "2027-01-01")]
    public void EachViewShowsItsDays(CalendarView view, string shown, DayOfWeek first, string start, string end) =>
        Assert.Equal((D(start), D(end)), CalendarNavigation.Range(view, D(shown), first));

    [Theory]
    [InlineData(CalendarView.Day, "2026-12-31", 1, "2027-01-01")]
    [InlineData(CalendarView.Week, "2026-12-29", 1, "2027-01-05")]
    [InlineData(CalendarView.Week, "2027-01-05", -1, "2026-12-29")]
    [InlineData(CalendarView.Month, "2026-01-31", 1, "2026-02-28")]
    [InlineData(CalendarView.Month, "2026-12-15", 1, "2027-01-15")]
    [InlineData(CalendarView.Month, "2026-01-15", -1, "2025-12-15")]
    [InlineData(CalendarView.Year, "2024-02-29", 1, "2025-02-28")]
    [InlineData(CalendarView.Year, "2026-09-22", -3, "2023-09-22")]
    // At the ends of what a date holds, the page stays (the year 9999, the first day).
    [InlineData(CalendarView.Year, "9999-06-01", 1, "9999-12-01")]
    [InlineData(CalendarView.Day, "0001-01-01", -1, "0001-01-01")]
    public void PagesTurnByTheViewsUnit(CalendarView view, string shown, int pages, string expected) =>
        Assert.Equal(D(expected), CalendarNavigation.Turn(view, D(shown), pages));

    [Theory]
    [InlineData(CalendarView.Day, "2026-09-22", "2026-09-22", true)]
    [InlineData(CalendarView.Day, "2026-09-22", "2026-09-23", false)]
    [InlineData(CalendarView.Week, "2026-09-22", "2026-09-27", true)]
    [InlineData(CalendarView.Week, "2026-09-22", "2026-09-28", false)]
    // The grid's days of the next month are not the month's.
    [InlineData(CalendarView.Month, "2026-09-22", "2026-10-01", false)]
    [InlineData(CalendarView.Month, "2026-09-22", "2026-09-01", true)]
    [InlineData(CalendarView.Year, "2026-09-22", "2026-01-01", true)]
    [InlineData(CalendarView.Year, "2026-09-22", "2027-01-01", false)]
    public void WhatIsOnThePage(CalendarView view, string shown, string day, bool expected) =>
        Assert.Equal(expected, CalendarNavigation.OnPage(view, D(shown), D(day), DayOfWeek.Monday));

    [Theory]
    [InlineData(CalendarView.Month, CalendarKey.Left, "2026-09-01", "2026-08-31")]
    [InlineData(CalendarView.Month, CalendarKey.Right, "2026-09-30", "2026-10-01")]
    [InlineData(CalendarView.Month, CalendarKey.Up, "2026-09-22", "2026-09-15")]
    [InlineData(CalendarView.Month, CalendarKey.Down, "2026-09-22", "2026-09-29")]
    [InlineData(CalendarView.Month, CalendarKey.PageUp, "2026-03-31", "2026-02-28")]
    [InlineData(CalendarView.Month, CalendarKey.PageDown, "2026-12-22", "2027-01-22")]
    [InlineData(CalendarView.Week, CalendarKey.Right, "2026-09-27", "2026-09-28")]
    // The day and week views slide by the day: Page Up and Down too, as a notch of the wheel.
    [InlineData(CalendarView.Week, CalendarKey.PageDown, "2026-09-22", "2026-09-23")]
    [InlineData(CalendarView.Day, CalendarKey.Left, "2026-01-01", "2025-12-31")]
    [InlineData(CalendarView.Day, CalendarKey.PageUp, "2026-09-22", "2026-09-21")]
    [InlineData(CalendarView.Year, CalendarKey.Left, "2026-03-31", "2026-02-28")]
    [InlineData(CalendarView.Year, CalendarKey.Right, "2026-12-15", "2027-01-15")]
    // A row of four small months.
    [InlineData(CalendarView.Year, CalendarKey.Down, "2026-09-22", "2027-01-22")]
    [InlineData(CalendarView.Year, CalendarKey.Up, "2026-02-10", "2025-10-10")]
    [InlineData(CalendarView.Year, CalendarKey.PageDown, "2024-02-29", "2025-02-28")]
    [InlineData(CalendarView.Week, CalendarKey.Home, "2027-05-05", "2026-09-22")]
    public void KeysMoveTheSelection(CalendarView view, CalendarKey key, string selected, string expected) =>
        Assert.Equal(D(expected), CalendarNavigation.Key(view, D(selected), key, D("2026-09-22"), yearColumns: 4));

    [Theory]
    [InlineData(CalendarView.Week, CalendarKey.Right, "2026-09-29")]
    [InlineData(CalendarView.Week, CalendarKey.Left, "2026-09-15")]
    [InlineData(CalendarView.Week, CalendarKey.PageUp, "2026-09-15")]
    [InlineData(CalendarView.Day, CalendarKey.PageDown, "2026-09-29")]
    // Months and years don't widen: Ctrl there is the dock's, not a week.
    [InlineData(CalendarView.Month, CalendarKey.Right, "2026-09-23")]
    public void WithCtrlOrShiftTheDaysMoveAWeek(CalendarView view, CalendarKey key, string expected) =>
        Assert.Equal(D(expected), CalendarNavigation.Key(view, D("2026-09-22"), key, D("2026-09-22"), wide: true));

    [Theory]
    [InlineData(CalendarView.Day, CalendarKey.Up)]
    [InlineData(CalendarView.Week, CalendarKey.Down)]
    public void UpAndDownScrollTheHoursInsteadOfMovingADay(CalendarView view, CalendarKey key) =>
        Assert.Null(CalendarNavigation.Key(view, D("2026-09-22"), key, D("2026-09-22")));

    [Theory]
    // ISO 8601 (first week with four days, Monday): Europe.
    [InlineData("2026-01-01", DayOfWeek.Monday, 2, 1)]
    [InlineData("2026-09-22", DayOfWeek.Monday, 2, 39)]
    [InlineData("2026-12-31", DayOfWeek.Monday, 2, 53)]
    [InlineData("2027-01-03", DayOfWeek.Monday, 2, 53)]
    [InlineData("2027-01-04", DayOfWeek.Monday, 2, 1)]
    [InlineData("2024-12-30", DayOfWeek.Monday, 2, 1)]
    // The week that holds January 1st (Sunday): the United States.
    [InlineData("2026-12-27", DayOfWeek.Sunday, 0, 1)]
    [InlineData("2026-12-26", DayOfWeek.Sunday, 0, 52)]
    [InlineData("2026-01-01", DayOfWeek.Sunday, 0, 1)]
    // The first whole week.
    [InlineData("2026-01-05", DayOfWeek.Monday, 1, 1)]
    [InlineData("2026-01-04", DayOfWeek.Monday, 1, 52)]
    public void WeekNumbersFollowTheRegion(string day, DayOfWeek first, int rule, int expected) =>
        Assert.Equal(expected, CalendarNavigation.WeekOfYear(D(day), first, rule));

    [Theory]
    [InlineData(2026, 9, DayOfWeek.Monday, "2026-08-31")]
    [InlineData(2026, 9, DayOfWeek.Sunday, "2026-08-30")]
    [InlineData(2026, 9, DayOfWeek.Tuesday, "2026-09-01")]
    [InlineData(1, 1, DayOfWeek.Sunday, "0001-01-01")]
    public void TheMonthsGridStartsOnTheFirstDayOfTheWeek(int year, int month, DayOfWeek first, string expected) =>
        Assert.Equal(D(expected), CalendarGrid.First(year, month, first));
}
