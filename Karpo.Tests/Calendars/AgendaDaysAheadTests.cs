// §496: the agenda's "Days ahead" (the person, 2026-11-03: "for the agenda you should be able to set how many days into
// the future it should show events"). Held here:
//
//  - **The agenda lists the days it is set to**, today the first: an event past them is left out, one within kept, a
//    longer event listed under its days within them alone; a week where nobody set it, as it always was.
//  - **The store is asked for those days and reads them**: the line asks its calendars from today's midnight to the
//    horizon's, and the calendars are read (and kept) far past the most anybody may set (PimCache.Window), so an
//    account's event at the far end is there for the largest asking line — from a stand-in account, no server.
//  - **The file**: calendar.daysAhead, held to 1–60, a week not written; it round-trips.
//  - **The row**: "Days ahead", a stepper of days on the agenda alone, which writes the option.

using Karpo.Accounts;
using Karpo.Dock;
using Karpo.Dock.Pim;
using Karpo.Tests.Core;
using Karpo.Tests.Islands;
using Karpo.Tests.Pim;

namespace Karpo.Tests.Calendars;

public sealed class AgendaDaysAheadTests
{
    private static readonly DateTime Now = new(2026, 9, 22, 14, 40, 0);

    private static CalendarEvent At(string title, DateTime start, double hours) => new(start, start.AddHours(hours), false, title, null);

    private static readonly List<CalendarEvent> Events =
    [
        At("Standup", new DateTime(2026, 9, 22, 9, 30, 0), 0.25),
        At("Focus time", new DateTime(2026, 9, 22, 14, 0, 0), 2),
        At("Night bus", new DateTime(2026, 9, 23, 0, 15, 0), 0.5),
        new(new DateTime(2026, 9, 23), new DateTime(2026, 9, 26), true, "Trip", null),
        At("Dentist", new DateTime(2026, 9, 24, 8, 0, 0), 1),
        At("Gym", new DateTime(2026, 9, 28, 6, 0, 0), 1),
        At("Planning", new DateTime(2026, 9, 29, 9, 0, 0), 1),
        At("Review", new DateTime(2026, 10, 14, 10, 0, 0), 1),
        At("Far off", new DateTime(2026, 11, 20, 10, 0, 0), 1),
        At("Too far", new DateTime(2026, 11, 21, 10, 0, 0), 1),
    ];

    /// <summary>A stand-in calendar of <see cref="Events"/> that writes down every range it is asked for.</summary>
    private sealed class Recording : ICalendarSource
    {
        private readonly ListCalendar inner = new("test:days", "Days", default, Events);

        public List<(DateTime From, DateTime To)> Asked { get; } = [];

        public event Action? Changed
        {
            add => inner.Changed += value;
            remove => inner.Changed -= value;
        }

        public string Id => inner.Id;

        public string Name => inner.Name;

        public Windows.UI.Color Colour => inner.Colour;

        public bool Loading => false;

        public string? Problem => null;

        public void Refresh()
        {
        }

        public IReadOnlyList<CalendarEvent> EventsIn(DateTime from, DateTime to, TimeZoneInfo zone)
        {
            Asked.Add((from, to));
            return inner.EventsIn(from, to, zone);
        }

        public void Dispose() => inner.Dispose();
    }

    /// <summary>A folder's agenda line as a file has it, <paramref name="calendar"/> its calendar record ("" none), read and repaired.</summary>
    private static CellItem Line(string calendar)
    {
        var record = calendar.Length == 0 ? "" : $$""", "calendar": {{calendar}}""";
        var folder = $$"""{ "id": "f", "kind": "folderList", "folder": { "lists": [ [ { "island": { "id": "c", "kind": "calendar"{{record}} } } ] ] } }""";
        return Configs.Islands(Configs.Repaired($$"""{ "layouts": [ { "id": "default", "name": "Default", "islands": [ {{folder}} ] } ] }"""))[0].Folder!.Cells[0].Island;
    }

    [UiFact]
    public void TheAgendaListsTheDaysItIsSetToAndNoneAfter()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var source = new Recording();
            var store = new CalendarStore();
            store.Add(source);
            CalendarIsland.Made = store;
            try
            {
                List<string> Listed(string calendar, int days)
                {
                    using var island = new CalendarIsland("test/days", Line(calendar), gfx, new Config());
                    source.Asked.Clear();
                    var titles = island.Upcoming(Now).Select(item => item.Title).Order(StringComparer.Ordinal).ToList();
                    // Asked from today's midnight to the midnight after the last day listed: no further.
                    Assert.Equal([(Now.Date, Now.Date.AddDays(days))], source.Asked);
                    Assert.Equal(days, island.AgendaDays);
                    return titles;
                }

                // Nobody set it: a week, today and the six after, as it always was.
                Assert.Equal(["Dentist", "Focus time", "Gym", "Night bus", "Trip"], Listed("", 7));
                Assert.Equal(["Dentist", "Focus time", "Gym", "Night bus", "Trip"], Listed("""{ "daysAhead": 7 }""", 7));
                // Today alone: what has not ended today, nothing of tomorrow.
                Assert.Equal(["Focus time"], Listed("""{ "daysAhead": 1 }""", 1));
                // Two weeks: the 29th's planning in, the 14th of October's review out (the 23rd day, today the first).
                Assert.Equal(["Dentist", "Focus time", "Gym", "Night bus", "Planning", "Trip"], Listed("""{ "daysAhead": 14 }""", 14));
                Assert.Equal(["Dentist", "Focus time", "Gym", "Night bus", "Planning", "Review", "Trip"], Listed("""{ "daysAhead": 23 }""", 23));
                // The most, 60: the 20th of November is the 60th day, the 21st the 61st.
                Assert.Equal(["Dentist", "Far off", "Focus time", "Gym", "Night bus", "Planning", "Review", "Trip"], Listed("""{ "daysAhead": 60 }""", 60));
            }
            finally
            {
                CalendarIsland.Made = null;
            }
        });
    }

    [UiFact]
    public void AnEventLongerThanTheDaysIsListedUnderTheDaysWithinThemAlone()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var store = new CalendarStore();
            store.Add(new ListCalendar("test:days", "Days", default, Events));
            CalendarIsland.Made = store;
            try
            {
                // Two days: today and tomorrow; the trip of the 23rd to the 25th under tomorrow alone.
                using var island = new CalendarIsland("test/days", Line("""{ "daysAhead": 2 }"""), gfx, new Config());
                var agenda = island.Agenda(Now);
                Assert.Equal([new DateOnly(2026, 9, 23)], agenda.Where(entry => entry.Event.Title == "Trip").Select(entry => entry.Day));
                Assert.All(agenda, entry => Assert.True(entry.Day < DateOnly.FromDateTime(Now).AddDays(2)));

                // Four: under each of its three days.
                using var longer = new CalendarIsland("test/days4", Line("""{ "daysAhead": 4 }"""), gfx, new Config());
                Assert.Equal([new DateOnly(2026, 9, 23), new DateOnly(2026, 9, 24), new DateOnly(2026, 9, 25)],
                    longer.Agenda(Now).Where(entry => entry.Event.Title == "Trip").Select(entry => entry.Day));
            }
            finally
            {
                CalendarIsland.Made = null;
            }
        });
    }

    /// <summary>
    /// The calendars are read and kept (PimCache.Window) from last month to a year ahead: on every day of three years, at
    /// either end of the UTC day and with any zone's day beside it, past the most days an agenda may be set to.
    /// </summary>
    [Fact]
    public void TheWindowReadHoldsTheMostDaysAheadOnEveryDay()
    {
        for (var day = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc); day.Year < 2029; day = day.AddDays(1))
        {
            foreach (var utc in new[] { day, day.AddHours(23).AddMinutes(59) })
            {
                var (from, to) = PimCache.Window(utc);
                // The furthest local day an agenda reaches: a zone 14 hours ahead, its last day's end.
                var furthest = utc.AddHours(14).Date.AddDays(CalendarOptions.DaysAheadMost);
                Assert.True(to >= furthest, $"{utc:o}: read to {to:o}, an agenda reaches {furthest:o}");
                Assert.True(from <= utc.AddHours(-12).Date, $"{utc:o}: read from {from:o}");
            }
        }
    }

    /// <summary>
    /// An account's event on the 55th day ahead, read through the store from a stand-in account: listed by an agenda set to
    /// the most, not by one of a week — the read reached it for the largest asking line.
    /// </summary>
    [UiFact]
    public async Task AnAccountsEventFarAheadIsThereForTheLargestAskingLine()
    {
        var now = new DateTime(2026, 10, 31, 9, 0, 0, DateTimeKind.Utc);
        var reader = new FakeReader(AccountKind.Microsoft, "ann@example.com");
        reader.Calendars.Add(new CalendarRead(new RemoteCalendar("work", "Work", null),
            [new Occurrence("Retreat", null, false, false, now.AddDays(55), now.AddDays(55).AddHours(2)),
             new Occurrence("Standup", null, false, false, now.AddDays(1), now.AddDays(1).AddHours(1))], []));
        var store = new CalendarStore(new AccountCalendars(new AccountSet([reader]), () => now));
        store.Open();
        await store.Idle();
        Assert.True(reader.Window.To >= now.AddDays(CalendarOptions.DaysAheadMost + 1), $"read to {reader.Window.To:o}");
        var local = now.ToLocalTime();

        Offscreen.OnStaThread(gfx =>
        {
            CalendarIsland.Made = store;
            try
            {
                using var most = new CalendarIsland("test/most", Line($$"""{ "daysAhead": {{CalendarOptions.DaysAheadMost}} }"""), gfx, new Config());
                Assert.Equal(["Standup", "Retreat"], most.Upcoming(local).Select(item => item.Title));
                using var week = new CalendarIsland("test/week", Line(""), gfx, new Config());
                Assert.Equal(["Standup"], week.Upcoming(local).Select(item => item.Title));
            }
            finally
            {
                CalendarIsland.Made = null;
            }
        });
    }

    /// <summary>calendar.daysAhead in the file: kept, held to 1–60, a week (the default) and none not written; round-trips.</summary>
    [Fact]
    public void TheFileKeepsTheDaysAheadHeldToItsRange()
    {
        static int? Read(string calendar) => Line(calendar).Calendar?.DaysAhead;

        Assert.Null(Read(""));
        Assert.Null(Read("""{ "daysAhead": 7 }"""));
        Assert.Equal(14, Read("""{ "daysAhead": 14 }"""));
        Assert.Equal(1, Read("""{ "daysAhead": 1 }"""));
        Assert.Equal(1, Read("""{ "daysAhead": 0 }"""));
        Assert.Equal(1, Read("""{ "daysAhead": -3 }"""));
        Assert.Equal(60, Read("""{ "daysAhead": 60 }"""));
        Assert.Equal(60, Read("""{ "daysAhead": 99 }"""));

        var config = Configs.Repaired("""{ "layouts": [ { "id": "default", "name": "Default", "islands": [ { "id": "f", "kind": "folderList", "folder": { "lists": [ [ { "island": { "id": "c", "kind": "calendar", "calendar": { "daysAhead": 21 } } } ] ] } } ] } ] }""");
        var json = config.ToJson();
        Assert.Contains("\"daysAhead\": 21", json, StringComparison.Ordinal);
        var again = Configs.RoundTrip(config, out var changed);
        Assert.False(changed);
        Assert.Equal(21, Configs.Islands(again)[0].Folder!.Cells[0].Island.Calendar!.DaysAhead);
        Assert.Equal(json, again.ToJson());

        // A week, as nobody set it, is not written.
        Configs.Islands(config)[0].Folder!.Cells[0].Island.Calendar!.DaysAhead = 7;
        Assert.DoesNotContain("daysAhead", Configs.RoundTrip(config, out _).ToJson(), StringComparison.Ordinal);
    }

    /// <summary>
    /// "Days ahead": a stepper of days (R3) on the agenda's menu, 1 to 60, a week where nobody set it; "+" writes the
    /// option, "−" back to a week writes none. The day's events, one day, have no such row; nor has the day view.
    /// </summary>
    [Fact]
    public void TheRowIsAStepperOfDaysThatWritesTheOption()
    {
        var agenda = ListKind.Calendar.New();
        var row = Assert.Single(OptionRows.Of(agenda), each => each.Key == OptionRows.CalendarDaysAhead);
        Assert.Equal(OptionRowKind.Number, row.Kind);
        Assert.Equal(S.SettingsWidgetCalendarDaysAheadHeader, row.Title);
        Assert.Equal((7, 1, 60), (row.Value, row.Least, row.Most));
        Assert.Equal(OptionUnit.Days, row.Unit);
        Assert.Equal(S.SettingsWidgetCalendarDaysAheadValue(7), row.Shown);
        Assert.NotEqual(S.SettingsWidgetCalendarDaysAheadValue(1), S.SettingsWidgetCalendarDaysAheadValue(2));

        List<OptionPick> picks = [];
        _ = OptionMenu.Section(OptionRows.Of(agenda), picks);
        var more = picks.Single(pick => pick.Kind == OptionPickKind.Step && pick.Key == OptionRows.CalendarDaysAhead && pick.Value == 1);
        var fewer = picks.Single(pick => pick.Kind == OptionPickKind.Step && pick.Key == OptionRows.CalendarDaysAhead && pick.Value == -1);
        Assert.True(OptionMenu.Apply(agenda, more));
        Assert.Equal(8, agenda.Calendar!.DaysAhead);
        Assert.Contains("\"daysAhead\": 8", Json(agenda), StringComparison.Ordinal);
        Assert.True(OptionMenu.Apply(agenda, fewer));
        Assert.Null(agenda.Calendar!.DaysAhead);
        Assert.DoesNotContain("daysAhead", Json(agenda), StringComparison.Ordinal);

        // Its ends: no further than 60, no fewer than 1.
        agenda.Calendar.DaysAhead = 60;
        Assert.False(OptionMenu.Apply(agenda, more));
        agenda.Calendar.DaysAhead = 1;
        Assert.False(OptionMenu.Apply(agenda, fewer));

        Assert.DoesNotContain(OptionRows.Of(ListKind.Events.New()), each => each.Key == OptionRows.CalendarDaysAhead);
        Assert.DoesNotContain(OptionRows.Of(ListKind.DayView.New()), each => each.Key == OptionRows.CalendarDaysAhead);
    }

    private static string Json(CellItem item) => System.Text.Json.JsonSerializer.Serialize(item, ConfigJson.Default.CellItem);
}
