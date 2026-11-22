// The calendars, shared by every calendar island, Karpo's calendar flyout and
// the calendar's folder tile. Each is a source (ICalendarSource): the
// calendars of the person's accounts, one source each (Pim\AccountCalendars.cs),
// and whatever a test or --smoke adds with Add. The store merges what they
// have, in the zone the calendar is shown in, and raises Changed on the dock's
// thread when any of them has news.
//
// **What the views read is in memory, always.** The accounts are read on the
// pool, each on its own (AccountCalendars), and what they answered is held
// until their next good read — through a failed one, with the stale mark
// (Freshness) — and kept on disk for the next weeks (PimCache), so a start
// without a connection shows them. No view waits for a read or a file.
//
// **Open is the only door to the accounts.** The dock opens Shared at its start
// (Pim\PimSync.cs), widget or not, so the accounts' calendars are synced for as
// long as it runs (docs\accounts.md, "When it reads"); a widget's first draw or
// click and the panel opening call it too,
// and then it starts only what is due. Making a store, making a widget and
// configuring one read nothing.
//
// **--smoke and the tests reach no account, by construction**: only Shared is
// built with the person's accounts, and only when the config is theirs. A run
// on an isolated config (KARPO_CONFIG_DIR, which every test and the smoke pass
// set) gets a store with no accounts in it at all, the same way such a run
// reads none of the person's drives, pins or startup entries. So a widget
// drawn offscreen may call Open as often as it likes: there is nobody to ask.

using Karpo.Core;
using Karpo.Dock.Pim;

namespace Karpo.Dock;

public sealed class CalendarStore
{
    // Made on first use. The signal raises Changed through the dispatcher queue of
    // the thread that first *looks* — an island attaching, or Open, which the
    // flyout goes through — rather than the one that happened to make it
    // (StoreSignal.Look, docs\widgets.md §11).
    //
    // It holds the accounts only on the person's own config: a test or the smoke
    // pass may not reach a server.
    private static readonly Lazy<CalendarStore> SharedStore =
        new(() => new CalendarStore(
            Config.IsIsolated ? null : new AccountCalendars(AccountSet.Shared, cache: PimCache.ForDock(), report: Karpo.Accounts.SyncReport.ForDock(), mirror: CalendarMirror.ForDock()),
            // holidays: Nager.Date's, for the person's own config alone, and asked nothing until a country is added (§237, §265).
            Config.IsIsolated ? null : HolidayCalendar.ForDock()));

    private readonly Lock gate = new();

    /// <summary>The sources that are not the accounts': what a test or <c>--smoke</c> added, in the order they came.</summary>
    private readonly List<ICalendarSource> added = [];

    private readonly StoreSignal signal;
    private readonly AccountCalendars? accounts;
    private readonly HolidayCalendar? holidays;

    /// <param name="accounts">
    /// The reading of the accounts' calendars, which the dock's store has and nobody else does: a
    /// store made without one shows only what is added to it, and can reach nothing.
    /// </param>
    /// <param name="holidays">The public holidays (§237): one of the store's calendars while the config has them on.</param>
    internal CalendarStore(AccountCalendars? accounts = null, HolidayCalendar? holidays = null)
    {
        this.accounts = accounts;
        this.holidays = holidays;
        signal = new StoreSignal(() => Changed?.Invoke());
        if (accounts is not null)
        {
            accounts.Changed += signal.Notify;
        }
        if (holidays is not null)
        {
            holidays.Changed += signal.Notify;
        }
    }

    /// <summary>holidays: the public holidays' calendar, which the dock configures from the config; null for a store without (every test's and the smoke's).</summary>
    internal HolidayCalendar? Holidays => holidays;

    /// <summary>The store the dock uses, made on first use. Making it reads nothing.</summary>
    public static CalendarStore Shared => SharedStore.Value;

    /// <summary>
    /// A source has news, or the set of sources changed. Raised on the dock's thread, from an
    /// attached island's Refresh or through the thread's dispatcher queue.
    /// </summary>
    public event Action? Changed;

    /// <summary>The calendars: the added ones first, then the accounts' in the order they came, then the public holidays while they are on.</summary>
    public IReadOnlyList<ICalendarSource> Sources
    {
        get
        {
            lock (gate)
            {
                List<ICalendarSource> sources = accounts is null ? [.. added] : [.. added, .. accounts.Calendars];
                if (holidays is { On: true })
                {
                    sources.Add(holidays);
                }
                return sources;
            }
        }
    }

    /// <summary>
    /// The person's web calendar on <paramref name="day"/>: the first calendar's that has one (<see cref="ICalendarSource.Web"/>),
    /// where an event of a calendar with none — a public holiday — opens on its day (§434); null where no calendar has one.
    /// </summary>
    public string? Web(DateOnly day) => Sources.Select(source => source.Web(day)).FirstOrDefault(link => link is not null);

    /// <summary>Whether there is any calendar at all to draw events from.</summary>
    public bool HasCalendars
    {
        get
        {
            lock (gate)
            {
                return added.Count > 0 || accounts?.Calendars.Count > 0 || holidays is { On: true };
            }
        }
    }

    /// <summary>
    /// No account gives calendars, as the last read found, and no calendar of another kind is
    /// here: the calendar widget's flyout says so with "Set up in Settings" (SetupPrompt.cs).
    /// </summary>
    public bool NoAccount => accounts is { HasAccount: false, Loading: false } && !HasCalendars;

    /// <summary>A calendar has neither events nor a problem yet: its first read is still on its way.</summary>
    public bool Loading => accounts?.Loading == true || Sources.Any(source => source.Loading);

    /// <summary>
    /// Why a calendar shows nothing new, in words for the user; null while every one is fine. The
    /// accounts' trouble comes first, because a calendar Karpo cannot reach has none of its own.
    /// </summary>
    public string? Problem =>
        accounts?.Problem ?? Sources.Select(source => source.Problem).FirstOrDefault(problem => problem is not null);

    /// <summary>
    /// When the accounts' events shown were read, and whether a refresh failed since: what the stale
    /// mark draws (StaleMark.cs). Never stale for a store without accounts.
    /// </summary>
    public Freshness Freshness => accounts?.Freshness ?? default;

    /// <summary>The accounts' calendars are being read now: a line's "Refresh" is left out meanwhile (IRefreshable).</summary>
    public bool Reading => accounts?.Reading ?? false;

    /// <summary>
    /// Whether this store reads accounts at all. False for every store a test or <c>--smoke</c>
    /// holds, which is what makes a drawn widget's <see cref="Open"/> harmless.
    /// </summary>
    internal bool CanReachAccounts => accounts is not null;

    /// <summary>How many sources were added rather than the accounts', for tests.</summary>
    internal int AddedCount
    {
        get
        {
            lock (gate)
            {
                return added.Count;
            }
        }
    }

    /// <summary>
    /// A calendar of its own, which the dock never has: what <c>--smoke</c> draws and what a test
    /// owns. The store shows its events, starts it with the others and disposes of it when it is
    /// removed.
    /// </summary>
    public void Add(ICalendarSource source)
    {
        lock (gate)
        {
            if (added.Contains(source))
            {
                return;
            }
            added.Add(source);
            source.Changed += signal.Notify;
        }
        signal.Notify();
    }

    /// <summary>Stops showing <paramref name="source"/> and disposes of it.</summary>
    public void Remove(ICalendarSource source)
    {
        lock (gate)
        {
            if (!added.Remove(source))
            {
                return;
            }
            source.Changed -= signal.Notify;
        }
        source.Dispose();
        signal.Notify();
    }

    /// <summary>
    /// <paramref name="island"/> shows these calendars: it is woken when they change.
    /// </summary>
    /// <remarks>
    /// It does <b>not</b> start a read: making a widget and configuring one are not looking at it.
    /// <see cref="Open"/> is what starts one: the dock's start (Pim.PimSync), a widget's first draw,
    /// or the panel opening (docs\widgets.md §11).
    /// </remarks>
    internal void Attach(CellIsland island) => signal.Add(island);

    /// <summary>That island is gone. The reading stays: another widget or the flyout may still want it.</summary>
    internal void Detach(CellIsland island) => signal.Remove(island);

    /// <summary>
    /// Somebody is looking: the accounts are read if they have not been, and every source starts
    /// whatever work is due. Returns at once, and news comes through <see cref="Changed"/>.
    /// </summary>
    /// <remarks>
    /// Called at the dock's start (Pim.PimSync), the first time a calendar widget is drawn or
    /// clicked, and when the calendar panel opens. Safe to call
    /// from the dock's thread as often as it likes: a read already
    /// running is not started again, and a failure rests before the next try. The reading goes to
    /// the pool, and until it arrives the views show no events rather than a wrong count.
    /// </remarks>
    public void Open()
    {
        // The first look, and so the thread this store's news is raised on when no island is
        // attached. It has to be here and not only in Attach: the calendar flyout opened from the
        // clock, with no calendar widget on the bar, subscribes to Changed and never attaches or
        // delivers, so its news reaches it through the signal's queue alone — and the queue must
        // be the flyout's thread, which is this one, not the thread that first touched Shared.
        signal.Look();
        accounts?.Refresh();
        // holidays: a look even while they are off, which asks nothing, so turning them on fetches at once.
        holidays?.Refresh();
        foreach (var source in Sources.Where(source => source != holidays))
        {
            source.Refresh();
        }
    }

    /// <summary>
    /// Somebody asked for the calendars again (a widget's Refresh): every account is read now, not
    /// only those due. Returns at once; a read already running is not started twice.
    /// </summary>
    public void Refresh()
    {
        signal.Look();
        accounts?.Refresh(force: true);
        foreach (var source in Sources)
        {
            source.Refresh();
        }
    }

    /// <summary>On the dock's thread: raises <see cref="Changed"/> once for all news since the last time.</summary>
    internal void Deliver() => signal.Deliver();

    /// <summary>The work in flight, for tests.</summary>
    internal Task Idle() => accounts?.Idle() ?? Task.CompletedTask;

    /// <summary>The cache taken in, which comes before the first read, for tests.</summary>
    internal Task CacheRead() => accounts?.CacheRead() ?? Task.CompletedTask;

    /// <summary>
    /// The events of every calendar that are on <paramref name="day"/> in <paramref name="zone"/> (this
    /// PC's when null): all-day events first, then by start. An event that spans several days is on
    /// each of them.
    /// </summary>
    public IReadOnlyList<CalendarEvent> EventsOn(DateOnly day, TimeZoneInfo? zone = null)
    {
        var start = day.ToDateTime(TimeOnly.MinValue);
        return OnDay(EventsIn(start, start.AddDays(1), zone), day);
    }

    /// <summary>
    /// The events of every calendar that overlap [<paramref name="from"/>, <paramref name="to"/>),
    /// wall-clock times in <paramref name="zone"/> (this PC's when null), as wall-clock times there:
    /// by start (all-day first), each once however many calendars have it. §455: of the calendars
    /// <paramref name="shown"/> picks, a widget's "Show from" (every one where it picked none), before the
    /// same event of two calendars is made one, so a calendar not shown hides none of another's.
    /// </summary>
    public IReadOnlyList<CalendarEvent> EventsIn(DateTime from, DateTime to, TimeZoneInfo? zone = null, SourceFilter? shown = null)
    {
        var frame = zone ?? TimeZoneInfo.Local;
        var picked = shown ?? SourceFilter.All;
        var all = new List<CalendarEvent>();
        foreach (var source in Sources)
        {
            try
            {
                all.AddRange(source.EventsIn(from, to, frame, picked));
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                // One calendar's failure leaves the others' events.
                Console.Error.WriteLine($"Karpo: calendar {source.Id} not read: {error.GetType().Name}");
            }
        }
        return Merge(all);
    }

    /// <summary>
    /// What comes next from <paramref name="now"/> (a wall-clock time in <paramref name="zone"/>): at most
    /// <paramref name="count"/> events that have not ended, within <paramref name="days"/> days, by
    /// start, all-day events of a day before its timed ones. For the calendar's folder tile.
    /// </summary>
    public IReadOnlyList<CalendarEvent> Upcoming(DateTime now, int count, int days = 7, TimeZoneInfo? zone = null)
    {
        var found = new List<CalendarEvent>();
        foreach (var item in EventsIn(now.Date, now.Date.AddDays(Math.Clamp(days, 1, 62)), zone))
        {
            if (found.Count >= count)
            {
                break;
            }
            if (item.End > now || (item.End <= item.Start && item.Start >= now))
            {
                found.Add(item);
            }
        }
        return found;
    }

    /// <summary>
    /// Whether an event running [<paramref name="start"/>, <paramref name="end"/>) is on in
    /// [<paramref name="from"/>, <paramref name="to"/>); an event without a length at its start.
    /// </summary>
    internal static bool Overlaps(DateTime start, DateTime end, DateTime from, DateTime to) =>
        end > start ? start < to && end > from : start >= from && start < to;

    /// <summary>Whether <paramref name="item"/> is on in [<paramref name="from"/>, <paramref name="to"/>).</summary>
    internal static bool Overlaps(CalendarEvent item, DateTime from, DateTime to) =>
        Overlaps(item.Start, item.End, from, to);

    /// <summary>
    /// The events of <paramref name="events"/> that are on <paramref name="day"/>, each once:
    /// those overlapping the day, and those without a length that start on it. All-day events
    /// first, then by start, end and title.
    /// </summary>
    internal static List<CalendarEvent> OnDay(IEnumerable<CalendarEvent> events, DateOnly day)
    {
        var start = day.ToDateTime(TimeOnly.MinValue);
        var end = start.AddDays(1);
        var found = Merge(events.Where(item => Overlaps(item, start, end)));
        found.Sort(static (a, b) =>
            a.AllDay != b.AllDay ? (a.AllDay ? -1 : 1)
            : a.Start != b.Start ? a.Start.CompareTo(b.Start)
            : a.End != b.End ? a.End.CompareTo(b.End)
            : string.CompareOrdinal(a.Title, b.Title));
        return found;
    }

    /// <summary>
    /// Each event once, the first calendar's where two have the same one (a meeting in a work and a
    /// family calendar); by start, all-day first, then end and title.
    /// </summary>
    internal static List<CalendarEvent> Merge(IEnumerable<CalendarEvent> events)
    {
        var seen = new HashSet<(DateTime, DateTime, bool, string, string?)>();
        var found = new List<CalendarEvent>();
        foreach (var item in events)
        {
            if (seen.Add((item.Start, item.End, item.AllDay, item.Title, item.Location)))
            {
                found.Add(item);
            }
        }
        found.Sort(static (a, b) =>
            a.Start != b.Start ? a.Start.CompareTo(b.Start)
            : a.AllDay != b.AllDay ? (a.AllDay ? -1 : 1)
            : a.End != b.End ? a.End.CompareTo(b.End)
            : string.CompareOrdinal(a.Title, b.Title));
        return found;
    }
}

/// <summary>How the calendar island and flyout put events and days into words.</summary>
internal static class CalendarText
{
    /// <summary>
    /// The part of <paramref name="item"/> that falls on <paramref name="day"/>: "10:30–11:00",
    /// "From 10:30", "Until 11:00", or "All day" (an all-day event, or a day in the middle of a
    /// longer one). An event without a length is its start time.
    /// </summary>
    public static string Span(CalendarEvent item, DateOnly day, Func<DateTime, string>? time = null)
    {
        time ??= Dates.Time;
        if (item.AllDay)
        {
            return S.DockCalendarAllDay;
        }
        if (item.End <= item.Start)
        {
            return time(item.Start);
        }
        var start = day.ToDateTime(TimeOnly.MinValue);
        var starts = item.Start >= start;
        var ends = item.End <= start.AddDays(1);
        return (starts, ends) switch
        {
            (true, true) => $"{time(item.Start)}–{time(item.End)}",
            (true, false) => S.DockCalendarFrom(time(item.Start)),
            (false, true) => S.DockCalendarUntil(time(item.End)),
            _ => S.DockCalendarAllDay,
        };
    }

    /// <summary>"Today", "Tomorrow", "Yesterday", or the region's long date without the year.</summary>
    public static string DayName(DateOnly day, DateOnly today, Func<DateOnly, string>? longDate = null) =>
        day == today ? S.DockCalendarToday
        : day == today.AddDays(1) ? S.DockCalendarTomorrow
        : day == today.AddDays(-1) ? S.DockCalendarYesterday
        : (longDate ?? Panels.Nls.LongDateWithoutYear)(day);
}
