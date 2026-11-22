// The public holidays as a calendar (Karpo.Dock\Calendar\HolidayCalendar.cs, docs\widgets.md §237): off it
// asks nothing and is no calendar; on, a year a request, this and the next, and the one a view reaches;
// a week old asked again; kept in holidays.json and shown from it offline; a failure with nothing kept is
// the calendar's problem. Answers are Nager.Date's as saved (HolidayAnswers); nothing reaches the network.

using Karpo.Dock;

namespace Karpo.Tests.Calendars;

public sealed class HolidayCalendarTests : IDisposable
{
    private static readonly DateTime October = new(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);

    private readonly string folder = Path.Combine(Path.GetTempPath(), "karpo-tests", "holidays-" + Guid.NewGuid().ToString("N"));
    private readonly List<(string Country, int Year)> asked = [];
    private DateTime now = October;
    private bool offline;

    public void Dispose()
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>
    /// Switzerland's 2026 and 2027 as saved, any other year 2026's moved to it; the United Kingdom's 2026,
    /// moved to any other year; offline, or for the country that fails, nothing.
    /// </summary>
    private Task<string> Fetch(string country, int year, CancellationToken cancel)
    {
        lock (asked)
        {
            asked.Add((country, year));
        }
        if (offline || country == failing)
        {
            throw new HttpRequestException("offline");
        }
        if (country == "GB")
        {
            return Task.FromResult(HolidayAnswers.Britain2026.Replace("\"2026-", $"\"{year}-", StringComparison.Ordinal));
        }
        return Task.FromResult(year switch
        {
            2026 => HolidayAnswers.Switzerland2026,
            2027 => HolidayAnswers.Switzerland2027,
            _ => HolidayAnswers.Switzerland2026.Replace("\"2026-", $"\"{year}-", StringComparison.Ordinal),
        });
    }

    /// <summary>The country whose every request fails.</summary>
    private string failing = "";

    private HolidayCalendar Calendar(bool keeps = true) => new(Fetch, keeps ? folder : null, () => now);

    private static HolidayOptions Zurich => Listing(("CH", "CH-ZH"));

    /// <summary>§265: the calendars of <paramref name="calendars"/>, a country and a region each.</summary>
    private static HolidayOptions Listing(params (string Country, string Region)[] calendars) =>
        new() { Calendars = [.. calendars.Select(calendar => new HolidayCountry { Country = calendar.Country, Region = calendar.Region })] };

    private static List<string> Titles(IEnumerable<CalendarEvent> events) => [.. events.Select(item => item.Title)];

    private static IReadOnlyList<CalendarEvent> Year(ICalendarSource calendar, int year) =>
        calendar.EventsIn(new DateTime(year, 1, 1), new DateTime(year + 1, 1, 1), TimeZoneInfo.Local);

    [Fact]
    public void OffItAsksNothingAndIsNoCalendar()
    {
        using var calendar = Calendar();
        var store = new CalendarStore(holidays: calendar);
        calendar.Configure(null);
        store.Open();
        calendar.Configure(new HolidayOptions());
        store.Open();
        Assert.Empty(store.EventsIn(new DateTime(2026, 12, 1), new DateTime(2027, 1, 1)));
        Assert.Empty(store.Sources);
        Assert.False(store.HasCalendars);
        Assert.Empty(asked);
        Assert.False(File.Exists(Path.Combine(folder, HolidayCalendar.FileName)));
    }

    /// <summary>Turned on while somebody looks: this year and the next, one request each, the days all-day in the calendar's red.</summary>
    [Fact]
    public async Task OnItFetchesThisYearAndTheNext()
    {
        using var calendar = Calendar();
        var store = new CalendarStore(holidays: calendar);
        store.Open();
        Assert.Empty(asked);
        calendar.Configure(Zurich);
        await calendar.Idle();
        Assert.Equal([("CH", 2026), ("CH", 2027)], asked.Order());
        Assert.Same(calendar, Assert.Single(store.Sources));
        var christmas = store.EventsOn(new DateOnly(2026, 12, 25)).Single();
        Assert.True(christmas.AllDay);
        Assert.Equal(new DateTime(2026, 12, 26), christmas.End);
        Assert.Same(calendar, christmas.Source);
        Assert.Equal(CalendarColours.Of(PublicHolidays.DefaultColour), christmas.Colour);
        Assert.Equal(9, Year(calendar, 2026).Count);

        // Another region, another colour (§423: the calendar's own): drawn from what is held, asked nothing.
        calendar.Configure(new HolidayOptions { Calendars = [new HolidayCountry { Country = "CH", Colour = "#498205" }] });
        await calendar.Idle();
        Assert.Equal(2, asked.Count);
        Assert.Equal(4, Year(calendar, 2026).Count);
        Assert.Equal(CalendarColours.Of("#498205"), Year(calendar, 2026)[0].Colour);
    }

    /// <summary>A week across the new year shows both years'; a view stepped back to last year asks for it then, once.</summary>
    [Fact]
    public async Task TheYearsAreAskedForAsTheViewsReachThem()
    {
        using var calendar = Calendar();
        calendar.Configure(Listing(("CH", "")));
        calendar.Refresh();
        await calendar.Idle();
        var turn = calendar.EventsIn(new DateTime(2026, 12, 21), new DateTime(2027, 1, 4), TimeZoneInfo.Local);
        Assert.Equal([new DateTime(2026, 12, 25), new DateTime(2027, 1, 1)], turn.Select(item => item.Start));

        Assert.Empty(Year(calendar, 2025));
        await calendar.Idle();
        Assert.Contains(("CH", 2025), asked);
        Assert.Equal(4, Year(calendar, 2025).Count);
        Year(calendar, 2025);
        await calendar.Idle();
        Assert.Single(asked, key => key.Year == 2025);
        // Far from now, nobody's view: never asked.
        Assert.Empty(Year(calendar, 2010));
        await calendar.Idle();
        Assert.DoesNotContain(asked, key => key.Year == 2010);
    }

    /// <summary>Kept on disk: a start days later shows them asking nothing; a week later asks again; offline, the kept ones stay.</summary>
    [Fact]
    public async Task KeptYearsAreShownAndAskedAgainAWeekOn()
    {
        using (var first = Calendar())
        {
            first.Configure(Zurich);
            first.Refresh();
            await first.Idle();
        }
        Assert.True(File.Exists(Path.Combine(folder, HolidayCalendar.FileName)));
        Assert.Equal(2, asked.Count);

        now = October.AddDays(3);
        using (var again = Calendar())
        {
            again.Configure(Zurich);
            again.Refresh();
            await again.Idle();
            Assert.Equal(2, asked.Count);
            Assert.Equal(9, Year(again, 2026).Count);
        }

        now = October.AddDays(8);
        offline = true;
        using (var stale = Calendar())
        {
            stale.Configure(Zurich);
            stale.Refresh();
            await stale.Idle();
            Assert.Equal(4, asked.Count);
            Assert.Equal(9, Year(stale, 2026).Count);
            Assert.Null(stale.Problem);
            Assert.False(stale.Loading);
        }
    }

    /// <summary>Nothing kept and no answer: the calendar's problem, and no second request before the retry is due.</summary>
    [Fact]
    public async Task NothingKeptAndOfflineIsTheProblem()
    {
        offline = true;
        using var calendar = Calendar(keeps: false);
        var changed = 0;
        calendar.Changed += () => changed++;
        calendar.Configure(Zurich);
        calendar.Refresh();
        Assert.True(calendar.Loading || asked.Count > 0);
        await calendar.Idle();
        Assert.Equal(S.DockHolidaysUnreachable, calendar.Problem);
        Assert.True(changed >= 2);
        calendar.Refresh();
        await calendar.Idle();
        Assert.Equal(2, asked.Count);

        now = October + HolidayCalendar.Retry;
        offline = false;
        calendar.Refresh();
        await calendar.Idle();
        Assert.Equal(4, asked.Count);
        Assert.Null(calendar.Problem);
        Assert.NotEmpty(Year(calendar, 2026));
    }

    /// <summary>The date's tooltip names the day's holiday; none on another day or while off.</summary>
    [Fact]
    public async Task ADaysNamesAreItsHolidays()
    {
        using var calendar = Calendar(keeps: false);
        calendar.Configure(Zurich);
        calendar.Refresh();
        await calendar.Idle();
        var national = calendar.NamesOn(new DateOnly(2026, 8, 1));
        Assert.Equal([PublicHolidays.Title(PublicHolidays.Parse(HolidayAnswers.Switzerland2026)!.Single(holiday => holiday.Date == new DateOnly(2026, 8, 1)), Strings.Language)], national);
        Assert.Empty(calendar.NamesOn(new DateOnly(2026, 8, 2)));
        calendar.Configure(null);
        Assert.Empty(calendar.NamesOn(new DateOnly(2026, 8, 1)));
    }

    /// <summary>
    /// §265: two countries are one calendar: each's years asked for once, their holidays together, one they
    /// share on a day under one name once, saying whose it is under its name and in the day's tooltip; one
    /// country left, nothing is said again and nothing asked.
    /// </summary>
    [Fact]
    public async Task SeveralCountriesAreOneCalendarSayingWhoseEachHolidayIs()
    {
        using var calendar = Calendar(keeps: false);
        var store = new CalendarStore(holidays: calendar);
        store.Open();
        calendar.Configure(Listing(("CH", "CH-ZH"), ("GB", "")));
        await calendar.Idle();
        Assert.Equal([("CH", 2026), ("CH", 2027), ("GB", 2026), ("GB", 2027)], asked.Order());
        Assert.Equal(["CH", "GB"], calendar.Countries);
        Assert.Same(calendar, Assert.Single(store.Sources));

        var (swiss, british) = (HolidayPlaces.Name("CH"), HolidayPlaces.Name("GB"));
        var christmas = Assert.Single(store.EventsOn(new DateOnly(2026, 12, 25)));
        Assert.Equal("Christmas Day", christmas.Title);
        Assert.EndsWith($"{swiss}, {british}", christmas.Location);
        // The agenda says it after when, from the event's Whose.
        Assert.Equal($"{swiss}, {british}", christmas.Whose);
        Assert.Equal([$"Christmas Day · {swiss}, {british}"], calendar.NamesOn(new DateOnly(2026, 12, 25)));
        Assert.Equal([$"St. Stephen's Day · {swiss}"], calendar.NamesOn(new DateOnly(2026, 12, 26)));
        Assert.Equal([$"St. Stephen's Day · {british}"], calendar.NamesOn(new DateOnly(2026, 12, 28)));
        // Under it its other name first, then whose it is.
        Assert.Equal($"Boxing Day · {british}", Assert.Single(calendar.EventsIn(new DateTime(2026, 12, 28), new DateTime(2026, 12, 29), TimeZoneInfo.Local)).Location);
        // The year's holidays are the two countries' together, a day of both once.
        var year = Year(calendar, 2026);
        Assert.Equal(year.Count, year.Select(item => (item.Start, item.Title)).Distinct().Count());
        Assert.True(year.Count > 9);
        Assert.Equal(year.OrderBy(item => item.Start).Select(item => item.Start), year.Select(item => item.Start));

        calendar.Configure(Zurich);
        await calendar.Idle();
        Assert.Equal(4, asked.Count);
        Assert.Equal(["Christmas Day"], calendar.NamesOn(new DateOnly(2026, 12, 25)));
        var alone = Assert.Single(store.EventsOn(new DateOnly(2026, 12, 25)));
        Assert.Equal("Weihnachten", alone.Location);
        Assert.Null(alone.Whose);
        Assert.Equal(9, Year(calendar, 2026).Count);
    }

    /// <summary>
    /// §423 (the person, 2026-10-30: "the color for the public holidays should be settable per country / entry"):
    /// each calendar's holidays in its own colour — one picked, else the holidays' red —, which every view draws
    /// them in (the agenda's stripe, the month's mark, the day's and the week's bars all take the event's); a day
    /// two share in the first listed's. Two regions of one country each their own too. Changed, shown at once,
    /// asked nothing.
    /// </summary>
    [Fact]
    public async Task EachCalendarsHolidaysWearItsOwnColour()
    {
        using var calendar = Calendar(keeps: false);
        var store = new CalendarStore(holidays: calendar);
        store.Open();
        var (green, blue, red) = (CalendarColours.Of("#498205"), CalendarColours.Of("#3A96DD"), CalendarColours.Of(PublicHolidays.DefaultColour));
        calendar.Configure(new HolidayOptions { Calendars = [new HolidayCountry { Country = "CH", Region = "CH-ZH", Colour = "#498205" }, new HolidayCountry { Country = "GB" }] });
        await calendar.Idle();
        Assert.Equal(green, Assert.Single(store.EventsOn(new DateOnly(2026, 12, 26))).Colour);
        Assert.Equal(red, Assert.Single(store.EventsOn(new DateOnly(2026, 12, 28))).Colour);
        Assert.Equal(green, Assert.Single(store.EventsOn(new DateOnly(2026, 12, 25))).Colour);

        var changed = 0;
        calendar.Changed += () => changed++;
        calendar.Configure(new HolidayOptions { Calendars = [new HolidayCountry { Country = "CH", Region = "CH-ZH", Colour = "#498205" }, new HolidayCountry { Country = "GB", Colour = "#3A96DD" }] });
        Assert.Equal(1, changed);
        Assert.Equal(blue, Assert.Single(store.EventsOn(new DateOnly(2026, 12, 28))).Colour);
        Assert.Equal(4, asked.Count);

        calendar.Configure(new HolidayOptions { Calendars = [new HolidayCountry { Country = "CH", Region = "CH-ZH" }, new HolidayCountry { Country = "CH", Region = "CH-TI", Colour = "#3A96DD" }] });
        await calendar.Idle();
        Assert.Equal(blue, Assert.Single(store.EventsOn(new DateOnly(2026, 1, 6))).Colour);
        Assert.Equal(red, Assert.Single(store.EventsOn(new DateOnly(2026, 12, 25))).Colour);
    }

    /// <summary>§265: two regions of one country are one request a year, told apart by their regions' names.</summary>
    [Fact]
    public async Task TwoRegionsOfOneCountryAreToldApartByTheirRegions()
    {
        using var calendar = Calendar(keeps: false);
        calendar.Configure(Listing(("CH", "CH-ZH"), ("CH", "CH-TI")));
        calendar.Refresh();
        await calendar.Idle();
        Assert.Equal([("CH", 2026), ("CH", 2027)], asked.Order());
        Assert.Equal(["Epiphany · Ticino"], calendar.NamesOn(new DateOnly(2026, 1, 6)));
        Assert.Equal(["St. Stephen's Day · Zürich, Ticino"], calendar.NamesOn(new DateOnly(2026, 12, 26)));
        Assert.Equal([$"Christmas Day · Zürich, Ticino"], calendar.NamesOn(new DateOnly(2026, 12, 25)));
    }

    /// <summary>§265: a country that cannot be had is the calendar's problem, the others' holidays shown all the same; kept, nothing is written of it.</summary>
    [Fact]
    public async Task ACountryThatCannotBeHadIsTheProblemTheOthersStillShow()
    {
        failing = "GB";
        using var calendar = Calendar();
        calendar.Configure(Listing(("CH", "CH-ZH"), ("GB", "")));
        calendar.Refresh();
        await calendar.Idle();
        Assert.Equal(S.DockHolidaysUnreachable, calendar.Problem);
        Assert.False(calendar.Loading);
        Assert.Equal(9, Year(calendar, 2026).Count);
        Assert.DoesNotContain("\"GB\"", File.ReadAllText(Path.Combine(folder, HolidayCalendar.FileName)), StringComparison.Ordinal);

        // Taken out, the problem goes with it.
        calendar.Configure(Zurich);
        Assert.Null(calendar.Problem);
    }
}
