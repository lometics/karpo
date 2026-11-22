// The calendar's sources (Karpo.Dock\Calendar\CalendarSources.cs) and the store
// that merges them (Karpo.Dock\CalendarStore.cs): events carry their calendar
// and its colour, the same event in two calendars is listed once, the
// calendar's zone is honoured, each view's range finds its events, and the
// folder tile's "what comes next".
//
// A store made without accounts reaches nothing, which is what every test and
// --smoke uses: the views see CalendarEvents and nothing else, so a source the
// test owns (ListCalendar) is all they need. Reading the accounts is
// Karpo.Tests\Pim\CalendarOpeningTests.cs and Karpo.Tests\Accounts.

using System.Globalization;
using Karpo.Accounts;
using Karpo.Dock;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Calendars;

public sealed class CalendarSourcesTests
{
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
    private static readonly TimeZoneInfo Tokyo = TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time");

    private static DateTime At(string text) => DateTime.ParseExact(text, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    /// <summary>An event at a UTC instant, as a wall clock in <paramref name="zone"/>.</summary>
    private static CalendarEvent Utc(string title, string utc, TimeZoneInfo zone, double hours = 1)
    {
        var start = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(DateTime.ParseExact(utc, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), DateTimeKind.Utc),
            zone);
        return new CalendarEvent(start, start.AddHours(hours), false, title, null);
    }

    [Fact]
    public void EventsCarryTheirCalendarAndItsColour()
    {
        var store = new CalendarStore();
        store.Add(new ListCalendar("account:work", "Work", default, [Utc("Standup", "2026-09-17 07:00", Berlin)]));
        store.Add(new ListCalendar("account:family", "Family", CalendarColours.Palette[0], [Utc("Dinner", "2026-09-17 17:00", Berlin)]));

        var events = store.EventsIn(new DateTime(2026, 9, 17), new DateTime(2026, 9, 18), Berlin).ToDictionary(item => item.Title);

        // The first calendar takes the accent, which is the transparent default; the next the
        // palette's first colour, for calendars whose account gives none.
        Assert.Equal("Work", events["Standup"].Source!.Name);
        Assert.Equal(default, events["Standup"].Colour);
        Assert.Equal("Family", events["Dinner"].Source!.Name);
        Assert.Equal(CalendarColours.Palette[0], events["Dinner"].Colour);
        Assert.Equal(2, store.Sources.Count);
        Assert.Equal(2, store.Sources.Select(source => source.Id).Distinct().Count());
    }

    [Fact]
    public void TheSameEventInTwoCalendarsIsShownOnceAsTheFirstHasIt()
    {
        var store = new CalendarStore();
        store.Add(new ListCalendar("account:work", "Work", default, [Utc("Review", "2026-09-17 09:00", TimeZoneInfo.Utc)]));
        store.Add(new ListCalendar("account:team", "Team", CalendarColours.Palette[0], [Utc("Review", "2026-09-17 09:00", TimeZoneInfo.Utc)]));

        var review = Assert.Single(store.EventsOn(new DateOnly(2026, 9, 17), TimeZoneInfo.Utc));

        Assert.Equal("Work", review.Source!.Name);
    }

    [Fact]
    public void EventsAreAskedForInTheCalendarsZone()
    {
        // The source is asked for wall-clock times in a zone and answers in them; the store hands
        // its own frame down unchanged, and an account's source is what converts (CalendarOpeningTests).
        var store = new CalendarStore();
        store.Add(new Zoned(DateTime.SpecifyKind(At("2026-09-17 20:00"), DateTimeKind.Utc), "Release"));

        // 22:00 in Berlin, but 05:00 the next day in Tokyo.
        Assert.Equal(At("2026-09-17 22:00"), Assert.Single(store.EventsOn(new DateOnly(2026, 9, 17), Berlin)).Start);
        Assert.Empty(store.EventsOn(new DateOnly(2026, 9, 17), Tokyo));
        Assert.Equal(At("2026-09-18 05:00"), Assert.Single(store.EventsOn(new DateOnly(2026, 9, 18), Tokyo)).Start);
    }

    [Fact]
    public void ASourceShowsItsEventsAndItsNews()
    {
        var store = new CalendarStore();
        var raised = 0;
        store.Changed += () => raised++;
        var calendar = new ListCalendar("account:one", "One", Color.FromArgb(255, 0x12, 0x34, 0x56),
            [new CalendarEvent(At("2026-09-17 09:00"), At("2026-09-17 10:00"), false, "Planning", "Teams")]);

        store.Add(calendar);
        store.Deliver();
        Assert.Equal(1, raised);
        Assert.True(store.HasCalendars);
        var planning = Assert.Single(store.EventsOn(new DateOnly(2026, 9, 17)));
        Assert.Same(calendar, planning.Source);
        Assert.Equal(calendar.Colour, planning.Colour);

        // It has news: the store says so once delivered.
        calendar.Raise();
        store.Deliver();
        Assert.Equal(2, raised);

        store.Remove(calendar);
        Assert.True(calendar.Disposed);
        Assert.False(store.HasCalendars);
        Assert.Empty(store.EventsOn(new DateOnly(2026, 9, 17)));
    }

    [Fact]
    public void OpeningAStoreWithoutAccountsStartsItsSourcesAndReachesNothing()
    {
        // What --smoke and the offscreen pictures do: the panel opens, Open runs, and there is no
        // account to read because the store was made without any.
        var store = new CalendarStore();
        var calendar = new ListCalendar("test:one", "One", default, []);
        store.Add(calendar);

        store.Open();
        store.Open();

        Assert.Equal(2, calendar.Refreshed);
        Assert.Null(store.Problem);
        Assert.False(store.Loading);
        Assert.True(store.Idle().IsCompleted);
    }

    [Fact]
    public void EachViewsRangeFindsItsEvents()
    {
        var store = new CalendarStore();
        CalendarEvent Day(string day) => new(DateTime.ParseExact(day, "yyyy-MM-dd", CultureInfo.InvariantCulture).AddHours(9),
            DateTime.ParseExact(day, "yyyy-MM-dd", CultureInfo.InvariantCulture).AddHours(10), false, day, null);
        store.Add(new ListCalendar("account:one", "One", default,
            [Day("2026-08-31"), Day("2026-09-22"), Day("2026-10-11"), Day("2026-10-12"), Day("2026-12-31")]));
        var shown = new DateOnly(2026, 9, 22);

        int Count(CalendarView view)
        {
            var (start, end) = CalendarNavigation.Range(view, shown, DayOfWeek.Monday);
            return store.EventsIn(start.ToDateTime(TimeOnly.MinValue), end.ToDateTime(TimeOnly.MinValue)).Count;
        }

        Assert.Equal(1, Count(CalendarView.Day));
        Assert.Equal(1, Count(CalendarView.Week));
        // The grid from 31 August to 11 October.
        Assert.Equal(3, Count(CalendarView.Month));
        Assert.Equal(5, Count(CalendarView.Year));
    }

    [Fact]
    public void WhatComesNextLeavesOutWhatHasEnded()
    {
        var store = new CalendarStore();
        store.Add(new ListCalendar("account:one", "One", default,
        [
            new(At("2026-09-22 09:00"), At("2026-09-22 10:00"), false, "Ended", null),
            new(At("2026-09-22 10:00"), At("2026-09-22 11:00"), false, "Now", null),
            new(new DateTime(2026, 9, 22), new DateTime(2026, 9, 23), true, "Holiday", null),
            new(At("2026-09-23 09:00"), At("2026-09-23 09:30"), false, "Tomorrow", null),
            new(At("2026-10-05 09:00"), At("2026-10-05 09:30"), false, "Later", null),
        ]));
        var now = At("2026-09-22 10:30");

        Assert.Equal(["Holiday", "Now", "Tomorrow"], store.Upcoming(now, 5).Select(item => item.Title));
        Assert.Equal(["Holiday", "Now"], store.Upcoming(now, 2).Select(item => item.Title));
        Assert.Equal(["Holiday", "Now", "Tomorrow", "Later"], store.Upcoming(now, 5, days: 14).Select(item => item.Title));
    }

    [Fact]
    public void TheCalendarsPaletteIsTheAccountsOneOpaqueAndAColourReadsBackAsItsHex()
    {
        Assert.Equal(SourceColours.Palette, CalendarColours.Palette.Select(colour => CalendarColours.Hex(colour)));
        Assert.All(CalendarColours.Palette, colour => Assert.Equal(255, colour.A));
        // Transparent is no colour of its own: the accent, as an event made by hand has it.
        Assert.Null(CalendarColours.Hex(default(Color)));
        Assert.Equal("#3A96DD", CalendarColours.Hex(CalendarColours.Parse("#3a96ddff")));
    }

    /// <summary>A calendar holding one instant, which it answers in whatever zone it is asked for.</summary>
    private sealed class Zoned(DateTime utc, string title) : ICalendarSource
    {
        public event Action? Changed;

        public string Id => "test:zoned";

        public string Name => "Zoned";

        public Color Colour => default;

        public bool Loading => false;

        public string? Problem => null;

        public void Refresh()
        {
        }

        public IReadOnlyList<CalendarEvent> EventsIn(DateTime from, DateTime to, TimeZoneInfo zone)
        {
            var start = TimeZoneInfo.ConvertTimeFromUtc(utc, zone);
            var end = start.AddHours(1);
            _ = Changed;
            return CalendarStore.Overlaps(start, end, from, to)
                ? [new CalendarEvent(start, end, false, title, null, this)]
                : [];
        }

        public void Dispose()
        {
        }
    }
}
