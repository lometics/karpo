// holidays: countries' public holidays as one more calendar (docs\widgets.md §237), beside the accounts'
// in the store (CalendarStore), so every view draws them as it draws any calendar's: all-day events in the
// calendar's colour, a month's day marked, the day's list naming them. Which are shown is the core's
// (Karpo.Core\Holidays.cs); this is the fetching and the keeping.
//
// **Several countries are one calendar** (§265, the person: "you should be able to add multiple holiday
// calendars"): the person's list (PublicHolidays.Calendars), shown together, a holiday two of them have on
// the same day under the same name once. Where more than one is listed a holiday says whose it is — under
// its name in a day's events (its Location), after when it is in the agenda (its Whose), after it in a
// day's tooltip (NamesOn) — by its country, or its region where two of one country are listed; with one,
// as before, nothing.
//
// **Nothing is asked until the person adds a country** (Configure, from the config at the dock's start and
// at every change): none listed, the calendar is no source at all, reads no file and asks no server. Then
// a year of a country is one request to Nager.Date, whatever regions of it are listed: this year and the
// next as soon as somebody looks (Refresh, the store's Open), and any other a view reaches (EventsIn: the
// year before, stepped back to), each asked again once it is a week old (Fresh), and after a failure not
// before Retry.
//
// **Offline** the answers are kept in holidays.json in the local folder (the islands' files, as the
// weather's forecast is, IslandFiles): read once, on the pool, before the first request, and written after
// every answer, the listed countries' years alone, each's newest first. Public days need no encryption.
// A year kept stays shown whatever its age while no answer comes; only a country of which nothing was
// ever had shows the calendar's problem.
//
// A test hands a fetch of its own and a folder of its own: only the dock's (ForDock) reaches the network.

using System.Text.Json.Nodes;
using Karpo.Core;
using Color = Windows.UI.Color;

namespace Karpo.Dock;

/// <summary>The public holidays of the person's countries as a calendar; see the top of the file.</summary>
internal sealed class HolidayCalendar : ICalendarSource
{
    internal const string FileName = "holidays.json";

    /// <summary>How old a year's answer may get before it is asked for again: the service's holidays change seldom, a new one being announced in the year.</summary>
    internal static readonly TimeSpan Fresh = TimeSpan.FromDays(7);

    /// <summary>How long after a failed request the year is asked for again.</summary>
    internal static readonly TimeSpan Retry = TimeSpan.FromMinutes(30);

    /// <summary>How often, at the most, the calendar looks whether anything is due while nothing else wakes it.</summary>
    private static readonly TimeSpan Look = TimeSpan.FromHours(6);

    /// <summary>Years kept on disk: the views' reach, a few years either side of this one.</summary>
    private const int KeptYears = 8;

    /// <summary>Years either side of this one a view may ask for.</summary>
    private const int MostYears = 5;

    private readonly Func<string, int, CancellationToken, Task<string>> fetch;
    private readonly string? folder;
    private readonly Func<DateTime> utcNow;
    private readonly Lock gate = new();
    private readonly Dictionary<(string Country, int Year), Kept> held = [];
    private readonly Dictionary<(string Country, int Year), DateTime> failed = [];
    private readonly HashSet<(string Country, int Year)> fetching = [];
    private readonly HashSet<int> wanted = [];
    private readonly Timer again;
    private readonly CancellationTokenSource stopping = new();
    private IReadOnlyList<HolidayShown> places = [];
    private IReadOnlyList<string> countries = [];
    private bool opened;
    private bool cacheAsked;
    private bool cacheTaken;
    private Task cacheRead = Task.CompletedTask;
    private int working;

    /// <param name="fetch">What a year of a country is asked with: the service's answer, as text. A test's answers without the network.</param>
    /// <param name="folder">Where holidays.json is kept; none keeps nothing.</param>
    /// <param name="utcNow">UTC now; a test's own.</param>
    internal HolidayCalendar(Func<string, int, CancellationToken, Task<string>> fetch, string? folder = null, Func<DateTime>? utcNow = null)
    {
        this.fetch = fetch;
        this.folder = folder;
        this.utcNow = utcNow ?? (() => Clock.RealUtc());
        again = new Timer(_ => Troubles.Guard("Holidays: looking whether a year is due", Wake));
    }

    /// <summary>The dock's: Nager.Date, kept in the local folder. Never made on an isolated run (CalendarStore.Shared).</summary>
    internal static HolidayCalendar ForDock() => new(Download, Config.LocalFolder);

    public event Action? Changed;

    public string Id => "holidays";

    public string Name => S.CoreHolidaysName;

    /// <summary>§423: the first calendar's colour, else the holidays' red; each holiday wears its own calendar's (<see cref="Gather"/>).</summary>
    public Color Colour
    {
        get
        {
            lock (gate)
            {
                return CalendarColours.Of(places.Count > 0 ? places[0].Colour : PublicHolidays.DefaultColour);
            }
        }
    }

    /// <summary>Whether the person listed a country the service has: only then a calendar at all.</summary>
    public bool On
    {
        get
        {
            lock (gate)
            {
                return IsOn;
            }
        }
    }

    /// <summary>The countries shown, each once, in the order listed (Windows' region's for one that follows it); none while off.</summary>
    internal IReadOnlyList<string> Countries
    {
        get
        {
            lock (gate)
            {
                return countries;
            }
        }
    }

    /// <summary>A country shown has neither a year kept nor a failure yet.</summary>
    public bool Loading
    {
        get
        {
            lock (gate)
            {
                return IsOn && opened && countries.Any(code => !HasAny(code) && !Failed(code));
            }
        }
    }

    /// <summary>A country shown has nothing kept and its request failed: the calendar's problem, whatever the others have.</summary>
    public string? Problem
    {
        get
        {
            lock (gate)
            {
                return IsOn && countries.Any(code => !HasAny(code) && Failed(code)) ? S.DockHolidaysUnreachable : null;
            }
        }
    }

    /// <summary>How many requests were made, for tests.</summary>
    internal int Requests { get; private set; }

    private bool IsOn => places.Count > 0;

    private bool HasAny(string code) => held.Keys.Any(key => key.Country == code);

    private bool Failed(string code) => failed.Keys.Any(key => key.Country == code);

    /// <summary>
    /// The config says this now (null: never set): which countries and regions are shown, none for off, each in
    /// which colour (§423). Changed is raised where that changes what is shown; a country added while somebody
    /// looks, what is due is asked for at once.
    /// </summary>
    public void Configure(HolidayOptions? said)
    {
        var nextPlaces = PublicHolidays.Calendars(said?.Normalized());
        bool changed;
        lock (gate)
        {
            changed = !nextPlaces.SequenceEqual(places);
            places = nextPlaces;
            countries = [.. nextPlaces.Select(place => place.Country).Distinct()];
            if (changed && IsOn && opened)
            {
                Start();
            }
        }
        if (changed)
        {
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Somebody looks: what is due is asked for, the kept years read first. Nothing while off, but the look
    /// is remembered, so turning them on fetches at once.
    /// </summary>
    public void Refresh()
    {
        lock (gate)
        {
            opened = true;
            if (IsOn)
            {
                Start();
            }
        }
    }

    public IReadOnlyList<CalendarEvent> EventsIn(DateTime from, DateTime to, TimeZoneInfo zone) =>
        [.. Gather(from, to).Select(found => found.Event)];

    /// <summary>§455: the holidays of the countries a widget's "Show from" picks (<see cref="WidgetSources.Holidays"/>), each its own part.</summary>
    public IReadOnlyList<CalendarEvent> EventsIn(DateTime from, DateTime to, TimeZoneInfo zone, SourceFilter shown) =>
        [.. Gather(from, to, shown).Select(found => found.Event)];

    /// <summary>
    /// What <paramref name="day"/> is a holiday as, for the date's tooltip: its names, none on a day that
    /// is none or while off; where several countries are listed, each says whose it is
    /// ("Christmas Day · Switzerland, United Kingdom").
    /// </summary>
    public IReadOnlyList<string> NamesOn(DateOnly day)
    {
        var start = day.ToDateTime(TimeOnly.MinValue);
        return [.. Gather(start, start.AddDays(1)).Select(found => found.Whose is { } whose ? $"{found.Event.Title} · {whose}" : found.Event.Title)];
    }

    /// <summary>
    /// The holidays of every calendar listed that overlap [<paramref name="from"/>, <paramref name="to"/>),
    /// by date, a day's in the calendars' order; one that a second calendar has on the same day under the
    /// same name is one event. Where several are listed, whose each is (<c>Whose</c>, the event's
    /// <see cref="CalendarEvent.Whose"/> too: "Switzerland, United Kingdom"), said under its name too after
    /// its other name; with one, null, as before.
    /// </summary>
    private List<(CalendarEvent Event, string? Whose)> Gather(DateTime from, DateTime to, SourceFilter? shown = null)
    {
        shown ??= SourceFilter.All;
        List<(HolidayShown Place, IReadOnlyList<Holiday> Holidays)> years = [];
        int several;
        lock (gate)
        {
            if (!IsOn)
            {
                return [];
            }
            several = places.Count;
            var thisYear = utcNow().Year;
            var last = (to > from ? to.AddTicks(-1) : from).Year;
            var asked = false;
            // The calendars in their order, each's years in theirs, so a day's holidays list as the calendars do.
            foreach (var place in places.Where(place => shown.Shows(WidgetSources.Holidays(place))))
            {
                for (var year = Math.Max(from.Year, thisYear - MostYears); year <= Math.Min(last, thisYear + MostYears); year++)
                {
                    if (held.TryGetValue((place.Country, year), out var kept))
                    {
                        years.Add((place, kept.Holidays));
                    }
                    else if (wanted.Add(year))
                    {
                        asked = true;
                    }
                }
            }
            // A year a view reached that nobody had asked for: fetched now, never waited for.
            if (asked && opened)
            {
                Start();
            }
        }
        var language = Strings.Language;
        // A day's holiday by its date and its name, with the labels of the calendars that have it; §423 in the colour
        // of the first that has it.
        var days = new Dictionary<(DateOnly Date, string Title), (Holiday Holiday, List<string> Whose, string Colour)>();
        var order = new List<(DateOnly Date, string Title)>();
        foreach (var (place, holidays) in years)
        {
            foreach (var holiday in PublicHolidays.Shown(holidays, place.Country, place.Region))
            {
                var start = holiday.Date.ToDateTime(TimeOnly.MinValue);
                if (!CalendarStore.Overlaps(start, start.AddDays(1), from, to))
                {
                    continue;
                }
                var key = (holiday.Date, PublicHolidays.Title(holiday, language));
                if (!days.TryGetValue(key, out var day))
                {
                    days[key] = day = (holiday, [], place.Colour);
                    order.Add(key);
                }
                if (!day.Whose.Contains(place.Label))
                {
                    day.Whose.Add(place.Label);
                }
            }
        }
        var found = new List<(CalendarEvent, string?)>();
        // By date; a day's in the order they were found, which a sort by date alone keeps.
        foreach (var (date, title) in order.OrderBy(key => key.Date))
        {
            var (holiday, labels, colour) = days[(date, title)];
            var start = date.ToDateTime(TimeOnly.MinValue);
            var whose = several > 1 ? string.Join(", ", labels) : null;
            var other = PublicHolidays.Other(holiday, language);
            var under = whose is null ? other : other is null ? whose : $"{other} · {whose}";
            found.Add((new CalendarEvent(start, start.AddDays(1), true, title, under, this, CalendarColours.Of(colour), Whose: whose), whose));
        }
        return found;
    }

    /// <summary>The work in flight, for tests: the kept years read, and every request answered.</summary>
    internal async Task Idle()
    {
        await cacheRead.ConfigureAwait(false);
        for (var turn = 0; turn < 500 && Volatile.Read(ref working) > 0; turn++)
        {
            await Task.Delay(10).ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        stopping.Cancel();
        again.Dispose();
    }

    /// <summary>Under the gate: the kept years first, once; then every year due of every country listed asked for, and the timer set for the next.</summary>
    private void Start()
    {
        if (!cacheAsked && folder is not null)
        {
            cacheAsked = true;
            cacheRead = Task.Run(() => Troubles.Guard("Holidays: reading the kept years", TakeCache));
            return;
        }
        if (folder is not null && !cacheTaken)
        {
            // The read under way starts what is due when it is done.
            return;
        }
        var now = utcNow();
        var soonest = now + Look;
        foreach (var key in countries.SelectMany(code => Years(now).Select(year => (code, year))))
        {
            if (fetching.Contains(key))
            {
                continue;
            }
            var due = held.TryGetValue(key, out var kept) ? kept.FetchedUtc + Fresh : now;
            if (failed.TryGetValue(key, out var fail) && fail + Retry > due)
            {
                due = fail + Retry;
            }
            if (due <= now)
            {
                Fetch(key);
            }
            else if (due < soonest)
            {
                soonest = due;
            }
        }
        // A new year is two years to have.
        var newYear = new DateTime(now.Year + 1, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        if (newYear < soonest)
        {
            soonest = newYear;
        }
        again.Change(soonest - now < TimeSpan.FromMinutes(1) ? TimeSpan.FromMinutes(1) : soonest - now, Timeout.InfiniteTimeSpan);
    }

    /// <summary>This year, the next, and the years a view reached.</summary>
    private IEnumerable<int> Years(DateTime now)
    {
        var years = new SortedSet<int> { now.Year, now.Year + 1 };
        foreach (var year in wanted)
        {
            if (Math.Abs(year - now.Year) <= MostYears)
            {
                years.Add(year);
            }
        }
        return years;
    }

    private void Wake()
    {
        lock (gate)
        {
            if (IsOn && opened)
            {
                Start();
            }
        }
    }

    /// <summary>Under the gate: one year of one country asked for, on the pool.</summary>
    private void Fetch((string Country, int Year) key)
    {
        fetching.Add(key);
        Requests++;
        Interlocked.Increment(ref working);
        var cancel = stopping.Token;
        _ = Task.Run(async () =>
        {
            var news = false;
            try
            {
                var text = await fetch(key.Country, key.Year, cancel).ConfigureAwait(false);
                var holidays = PublicHolidays.Parse(text) ?? throw new InvalidDataException("not a list of holidays");
                lock (gate)
                {
                    held[key] = new Kept(utcNow(), text, holidays);
                    failed.Remove(key);
                }
                news = true;
                Save();
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                if (cancel.IsCancellationRequested)
                {
                    return;
                }
                Console.Error.WriteLine($"Karpo: holidays of {key.Country} {key.Year} not read: {error.GetType().Name}");
                lock (gate)
                {
                    // A first failure of a country with nothing kept is news: its problem shows.
                    news = !held.Keys.Any(had => had.Country == key.Country) && !failed.ContainsKey(key);
                    failed[key] = utcNow();
                }
            }
            finally
            {
                lock (gate)
                {
                    fetching.Remove(key);
                    if (IsOn && opened)
                    {
                        Start();
                    }
                }
                Interlocked.Decrement(ref working);
            }
            if (news)
            {
                Changed?.Invoke();
            }
        }, cancel);
    }

    /// <summary>On the pool: holidays.json taken in, then what is due started.</summary>
    private void TakeCache()
    {
        var any = false;
        try
        {
            if (Read() is { } kept)
            {
                lock (gate)
                {
                    foreach (var (key, year) in kept)
                    {
                        if (!held.ContainsKey(key))
                        {
                            held[key] = year;
                            any = true;
                        }
                    }
                }
            }
        }
        finally
        {
            lock (gate)
            {
                cacheTaken = true;
                if (IsOn && opened)
                {
                    Start();
                }
            }
        }
        if (any)
        {
            Changed?.Invoke();
        }
    }

    /// <summary>What holidays.json keeps, by country and year; null where there is none or it does not read.</summary>
    private Dictionary<(string Country, int Year), Kept>? Read()
    {
        try
        {
            if (JsonNode.Parse(File.ReadAllText(Path.Combine(folder!, FileName)))?["years"] is not JsonArray years)
            {
                return null;
            }
            var found = new Dictionary<(string Country, int Year), Kept>();
            foreach (var entry in years.OfType<JsonObject>())
            {
                if (entry["country"]?.GetValue<string>() is { Length: 2 } code
                    && entry["year"]?.GetValue<int>() is { } year
                    && entry["fetchedUtc"]?.GetValue<DateTime>() is { } fetched
                    && entry["answer"]?.GetValue<string>() is { } answer
                    && PublicHolidays.Parse(answer) is { } holidays)
                {
                    found[(code, year)] = new Kept(DateTime.SpecifyKind(fetched, DateTimeKind.Utc), answer, holidays);
                }
            }
            return found;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    /// <summary>The listed countries' years into holidays.json, each's newest first, written through a temporary file.</summary>
    private void Save()
    {
        if (folder is null)
        {
            return;
        }
        JsonArray years = [];
        lock (gate)
        {
            foreach (var ((code, year), kept) in countries.SelectMany(each => held.Where(pair => pair.Key.Country == each).OrderByDescending(pair => pair.Value.FetchedUtc).Take(KeptYears)))
            {
                years.Add((JsonNode)new JsonObject
                {
                    ["country"] = code,
                    ["year"] = year,
                    ["fetchedUtc"] = kept.FetchedUtc,
                    ["answer"] = kept.Answer,
                });
            }
        }
        var text = new JsonObject { ["version"] = 1, ["years"] = years }.ToJsonString();
        lock (Writing)
        {
            try
            {
                Directory.CreateDirectory(folder);
                var path = Path.Combine(folder, FileName);
                File.WriteAllText(path + ".tmp", text);
                File.Move(path + ".tmp", path, overwrite: true);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine($"Karpo: {FileName} not saved: {error.Message}");
            }
        }
    }

    private static readonly Lock Writing = new();

    /// <summary>The service's answer for a year of a country; a country it has no holidays of that year is none.</summary>
    private static async Task<string> Download(string country, int year, CancellationToken cancel)
    {
        using var response = await IslandWeb.Client.GetAsync(PublicHolidays.Url(year, country), HttpCompletionOption.ResponseHeadersRead, cancel).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
        {
            return "[]";
        }
        response.EnsureSuccessStatusCode();
        return await IslandWeb.Text(response, 1 << 20, cancel).ConfigureAwait(false) ?? throw new InvalidDataException("holidays too long");
    }

    /// <summary>A year's answer as it came, when, and what it lists.</summary>
    private sealed record Kept(DateTime FetchedUtc, string Answer, IReadOnlyList<Holiday> Holidays);
}
