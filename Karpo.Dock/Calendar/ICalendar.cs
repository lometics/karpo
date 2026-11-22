// Just enough iCalendar (RFC 5545) to show a feed's events: VEVENT with
// DTSTART/DTEND/DURATION as dates, UTC, zoned or floating times, SUMMARY,
// LOCATION, STATUS, RRULE (DAILY, WEEKLY, MONTHLY, YEARLY with INTERVAL, COUNT,
// UNTIL, BYDAY, BYMONTHDAY, BYMONTH, BYYEARDAY, BYWEEKNO, BYHOUR, BYMINUTE,
// BYSETPOS, WKST), RDATE, EXDATE and RECURRENCE-ID overrides; VTIMEZONE for
// zones neither Windows nor ICU knows by their TZID; and the calendar's own
// name and colour (X-WR-CALNAME / NAME, X-APPLE-CALENDAR-COLOR / COLOR); and
// for the next meeting (docs\widgets.md §205) an event's online meeting — its
// CONFERENCE (RFC 7986), Google's X-GOOGLE-CONFERENCE, its URL, else a link in its
// LOCATION or DESCRIPTION — and TRANSP:TRANSPARENT, an event shown as free.
// Everything else is skipped.
//
// Recurrences are expanded in the event's own wall-clock time, so a weekly
// 09:00 meeting stays at 09:00 on both sides of a daylight-saving change, and
// only then converted to the time of the zone the calendar is shown in (this
// PC's, or a clock's). A time the clocks skip is read with the offset before
// the gap and a time they pass twice is the first of the two, as RFC 5545
// says.

using System.Globalization;
using System.Text;
using Color = Windows.UI.Color;

namespace Karpo.Dock;

internal enum StampKind
{
    /// <summary>A whole day, the same day wherever the user is.</summary>
    Date,
    Utc,

    /// <summary>A wall-clock time in a named zone.</summary>
    Zoned,

    /// <summary>A wall-clock time wherever the user is.</summary>
    Floating,
}

/// <summary>A DTSTART-like value: the wall-clock time, what it is relative to, and the zone when it has one.</summary>
internal readonly record struct Stamp(DateTime Wall, StampKind Kind, TimeZoneInfo? Zone)
{
    /// <summary>The instant; a floating time or a date is read in <paramref name="frame"/>, the zone it is shown in.</summary>
    public DateTime ToUtc(TimeZoneInfo frame) => Kind switch
    {
        StampKind.Utc => DateTime.SpecifyKind(Wall, DateTimeKind.Utc),
        StampKind.Zoned => WallToUtc(Wall, Zone!),
        _ => WallToUtc(Wall, frame),
    };

    /// <summary>The same kind of stamp at another wall-clock time.</summary>
    public Stamp At(DateTime wall) => this with { Wall = wall };

    /// <summary>A UTC instant as a wall-clock time in this stamp's own frame.</summary>
    public DateTime WallOf(DateTime utc, TimeZoneInfo frame) => Kind switch
    {
        StampKind.Utc => DateTime.SpecifyKind(utc, DateTimeKind.Utc),
        StampKind.Zoned => TimeZoneInfo.ConvertTimeFromUtc(utc, Zone!),
        _ => TimeZoneInfo.ConvertTimeFromUtc(utc, frame),
    };

    /// <summary>
    /// <paramref name="other"/> as a wall-clock time in this stamp's frame: as it is when both are
    /// read the same way, else through its instant.
    /// </summary>
    public DateTime WallOf(Stamp other, TimeZoneInfo frame)
    {
        var same = other.Kind == Kind && (Kind != StampKind.Zoned || other.Zone?.Id == Zone?.Id);
        return same || other.Kind == StampKind.Date ? other.Wall : WallOf(other.ToUtc(frame), frame);
    }

    /// <summary>
    /// A wall-clock time in <paramref name="zone"/> as UTC. A time the clocks skip in spring is read
    /// with the offset before the gap (02:30 becomes 03:30 summer time), a time they pass twice in
    /// autumn is the first of the two, as RFC 5545 §3.3.5 says.
    /// </summary>
    public static DateTime WallToUtc(DateTime wall, TimeZoneInfo zone)
    {
        var time = DateTime.SpecifyKind(wall, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(time))
        {
            // Hours before the change the clocks still showed the old offset.
            var before = zone.GetUtcOffset(time.AddHours(-12));
            return DateTime.SpecifyKind(time - before, DateTimeKind.Utc);
        }
        if (zone.IsAmbiguousTime(time))
        {
            var offsets = zone.GetAmbiguousTimeOffsets(time);
            return DateTime.SpecifyKind(time - offsets.Max(), DateTimeKind.Utc);
        }
        return TimeZoneInfo.ConvertTimeToUtc(time, zone);
    }
}

internal enum Frequency
{
    Daily,
    Weekly,
    Monthly,
    Yearly,
}

internal sealed class RecurrenceRule
{
    public Frequency Frequency { get; private init; }
    public int Interval { get; private init; } = 1;
    public int? Count { get; private init; }
    public Stamp? Until { get; private init; }
    public List<(int Ordinal, DayOfWeek Day)> ByDay { get; } = [];
    public List<int> ByMonthDay { get; } = [];
    public List<int> ByMonth { get; } = [];
    public List<int> ByYearDay { get; } = [];
    public List<int> ByWeekNo { get; } = [];
    public List<int> ByHour { get; } = [];
    public List<int> ByMinute { get; } = [];
    public List<int> BySetPos { get; } = [];
    public DayOfWeek WeekStart { get; private init; } = DayOfWeek.Monday;

    // How far counting this rule's occurrences from its start has got (a COUNT rule's, ICalendar.Expand):
    // every so many periods, the period and how many occurrences came before it. A series is drawn again
    // and again — each frame of a view, each day of a month —, and without these each time would count
    // again from a start that may be decades back.
    private readonly Lock counting = new();
    private readonly List<(DateTime Period, int Count)> marks = [];
    private DateTime countedFrom;

    /// <summary>The furthest mark of counting from <paramref name="first"/> at or before <paramref name="period"/>; null where there is none.</summary>
    internal (DateTime Period, int Count)? Mark(DateTime first, DateTime period)
    {
        lock (counting)
        {
            if (countedFrom != first)
            {
                return null;
            }
            for (var i = marks.Count - 1; i >= 0; i--)
            {
                if (marks[i].Period <= period)
                {
                    return marks[i];
                }
            }
            return null;
        }
    }

    /// <summary>Counting from <paramref name="first"/> reached <paramref name="period"/> with <paramref name="count"/> occurrences before it.</summary>
    internal void Remember(DateTime first, DateTime period, int count)
    {
        lock (counting)
        {
            if (countedFrom != first)
            {
                marks.Clear();
                countedFrom = first;
            }
            if (marks.Count == 0 || period > marks[^1].Period)
            {
                marks.Add((period, count));
            }
        }
    }

    /// <summary>Null for a rule this parser does not understand (hourly and finer, say), which is then left out.</summary>
    public static RecurrenceRule? Parse(string value)
    {
        var parts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in value.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var equals = part.IndexOf('=');
            if (equals > 0)
            {
                parts[part[..equals].Trim()] = part[(equals + 1)..].Trim();
            }
        }
        Frequency? frequency = parts.GetValueOrDefault("FREQ")?.ToUpperInvariant() switch
        {
            "DAILY" => Frequency.Daily,
            "WEEKLY" => Frequency.Weekly,
            "MONTHLY" => Frequency.Monthly,
            "YEARLY" => Frequency.Yearly,
            _ => null,
        };
        if (frequency is null)
        {
            return null;
        }
        var rule = new RecurrenceRule
        {
            Frequency = frequency.Value,
            Interval = Math.Max(1, Number(parts.GetValueOrDefault("INTERVAL")) ?? 1),
            Count = Number(parts.GetValueOrDefault("COUNT")) is { } count and > 0 ? count : null,
            Until = parts.GetValueOrDefault("UNTIL") is { } until && ICalendar.TryStamp(until, null, null, static _ => null, out var stamp) ? stamp : null,
            WeekStart = Day(parts.GetValueOrDefault("WKST") ?? "MO") ?? DayOfWeek.Monday,
        };
        foreach (var item in List(parts.GetValueOrDefault("BYDAY")))
        {
            // "MO", "2TU", "-1FR".
            var split = 0;
            while (split < item.Length && (char.IsAsciiDigit(item[split]) || item[split] is '+' or '-'))
            {
                split++;
            }
            if (Day(item[split..]) is { } day)
            {
                rule.ByDay.Add((split > 0 ? Number(item[..split]) ?? 0 : 0, day));
            }
        }
        rule.ByMonthDay.AddRange(Numbers(parts.GetValueOrDefault("BYMONTHDAY"), -31, 31, zero: false));
        rule.ByMonth.AddRange(Numbers(parts.GetValueOrDefault("BYMONTH"), 1, 12, zero: false));
        rule.ByYearDay.AddRange(Numbers(parts.GetValueOrDefault("BYYEARDAY"), -366, 366, zero: false));
        rule.ByWeekNo.AddRange(Numbers(parts.GetValueOrDefault("BYWEEKNO"), -53, 53, zero: false));
        rule.ByHour.AddRange(Numbers(parts.GetValueOrDefault("BYHOUR"), 0, 23, zero: true).Distinct().Order());
        rule.ByMinute.AddRange(Numbers(parts.GetValueOrDefault("BYMINUTE"), 0, 59, zero: true).Distinct().Order());
        rule.BySetPos.AddRange(Numbers(parts.GetValueOrDefault("BYSETPOS"), -366, 366, zero: false));
        return rule;
    }

    private static IEnumerable<string> List(string? value) =>
        value?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];

    private static IEnumerable<int> Numbers(string? value, int low, int high, bool zero) =>
        List(value).Select(Number).Where(n => n is { } v && (zero || v != 0) && v >= low && v <= high).Select(n => n!.Value);

    private static int? Number(string? value) =>
        int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number) ? number : null;

    internal static DayOfWeek? Day(string code) => code.ToUpperInvariant() switch
    {
        "SU" => DayOfWeek.Sunday,
        "MO" => DayOfWeek.Monday,
        "TU" => DayOfWeek.Tuesday,
        "WE" => DayOfWeek.Wednesday,
        "TH" => DayOfWeek.Thursday,
        "FR" => DayOfWeek.Friday,
        "SA" => DayOfWeek.Saturday,
        _ => null,
    };
}

/// <summary>A VEVENT as the feed has it, before its recurrences are worked out.</summary>
internal sealed class FeedEvent
{
    public string Uid { get; set; } = "";
    public string Summary { get; set; } = "";
    public string? Location { get; set; }
    public Stamp? Start { get; set; }
    public Stamp? End { get; set; }
    public TimeSpan? Duration { get; set; }
    public RecurrenceRule? Rule { get; set; }

    // Made only for an event that has them: most have neither, and a calendar of thousands keeps them all
    // (Pim\AccountCalendars.cs).
    private List<Stamp>? extra;
    private List<Stamp>? exceptions;

    /// <summary>Extra occurrences (RDATE).</summary>
    public IReadOnlyList<Stamp> Extra => extra ?? (IReadOnlyList<Stamp>)[];

    public IReadOnlyList<Stamp> Exceptions => exceptions ?? (IReadOnlyList<Stamp>)[];

    internal List<Stamp> ExtraList => extra ??= [];

    internal List<Stamp> ExceptionList => exceptions ??= [];

    public Stamp? RecurrenceId { get; set; }
    public bool Cancelled { get; set; }
    public bool Unsupported { get; set; }

    /// <summary>Its notes (DESCRIPTION), unescaped: read for a meeting's link and nothing else, and let go once it is found (<see cref="Seal"/>).</summary>
    public string? Description { get; set; }

    /// <summary>Its online meeting as the server marks it: CONFERENCE or X-GOOGLE-CONFERENCE, else URL.</summary>
    public string? Conference { get; set; }

    /// <summary>Its URL property, read after the conference's own.</summary>
    public string? Url { get; set; }

    /// <summary>TRANSP:TRANSPARENT: shown as free, none of the person's time.</summary>
    public bool Transparent { get; set; }

    private string? join;
    private bool joined;

    /// <summary>
    /// Where its meeting is joined (<see cref="Karpo.Accounts.MeetingLinks"/>): the conference's own
    /// link, else one in its URL, its place or its notes; null where none is a meeting's. Worked out
    /// once per event, however many times it recurs.
    /// </summary>
    public string? Join
    {
        get
        {
            if (!joined)
            {
                joined = true;
                join = Karpo.Accounts.MeetingLinks.Find(Conference, Url, Location, Description)?.Url;
            }
            return join;
        }
    }

    /// <summary>
    /// The event read whole: its meeting's link worked out now, and what was read only for it (the notes, the
    /// conference, the URL) let go, so a calendar of thousands holds no notes.
    /// </summary>
    internal void Seal()
    {
        _ = Join;
        Description = null;
        Conference = null;
        Url = null;
    }
}

internal sealed class ICalendar
{
    /// <summary>More periods than this and a rule is given up on: a feed must not stall the pool thread.</summary>
    private const int MaxPeriods = 20_000;

    /// <summary>
    /// The same for a rule with a COUNT, which is counted from its start (Expand): a daily one of 2,700
    /// years, so only a rule no calendar means is ever given up on.
    /// </summary>
    private const int MaxCounted = 1_000_000;

    /// <summary>Periods between two of a COUNT rule's marks (RecurrenceRule.Remember).</summary>
    private const int CountMarks = 512;

    public List<FeedEvent> Events { get; } = [];

    /// <summary>
    /// Events it holds that are left out: one with no start Karpo can read, or a rule it does not know
    /// (hourly and finer). Counted, so a calendar can say it is not all shown (AccountCalendars.Held).
    /// </summary>
    public int Dropped { get; private set; }

    /// <summary>The calendar's own name (X-WR-CALNAME, or RFC 7986's NAME); null when it has none.</summary>
    public string? Name { get; private set; }

    /// <summary>The calendar's own colour (X-APPLE-CALENDAR-COLOR, or RFC 7986's COLOR); null when it has none.</summary>
    public Color? Colour { get; private set; }

    public static ICalendar Parse(string text)
    {
        var calendar = new ICalendar();
        var lines = Lines(text).ToList();
        // The zones the feed defines itself, for TZIDs Windows and ICU don't know: read from all its
        // lines, as a feed may put its VTIMEZONEs anywhere, and only once a TZID is not known. Most are,
        // and a calendar of thousands of objects, each with its VTIMEZONE, would build a zone for each.
        Dictionary<string, TimeZoneInfo>? defined = null;
        TimeZoneInfo? Resolve(string id) => Zones.Find(id) ?? (defined ??= VTimeZones.Read(lines)).GetValueOrDefault(id);
        FeedEvent? current = null;
        var nested = 0;
        var depth = 0;
        foreach (var (name, parameters, value) in lines)
        {
            switch (name)
            {
                case "BEGIN":
                    depth++;
                    if (current is null && value.Equals("VEVENT", StringComparison.OrdinalIgnoreCase))
                    {
                        current = new FeedEvent();
                        nested = 0;
                    }
                    else if (current is not null)
                    {
                        // VALARM and the like: their properties are not the event's.
                        nested++;
                    }
                    continue;
                case "END":
                    depth = Math.Max(0, depth - 1);
                    if (current is not null && nested == 0 && value.Equals("VEVENT", StringComparison.OrdinalIgnoreCase))
                    {
                        if (current.Start is not null && !current.Unsupported)
                        {
                            current.Seal();
                            calendar.Events.Add(current);
                        }
                        else
                        {
                            calendar.Dropped++;
                        }
                        current = null;
                    }
                    else if (nested > 0)
                    {
                        nested--;
                    }
                    continue;
            }
            if (current is null)
            {
                // The calendar's own properties sit right inside VCALENDAR.
                if (depth == 1)
                {
                    calendar.Own(name, value);
                }
                continue;
            }
            if (nested > 0)
            {
                continue;
            }
            var zone = parameters?.GetValueOrDefault("TZID");
            var type = parameters?.GetValueOrDefault("VALUE");
            switch (name)
            {
                case "UID":
                    current.Uid = value;
                    break;
                case "SUMMARY":
                    current.Summary = Unescape(value);
                    break;
                case "LOCATION":
                    current.Location = Unescape(value);
                    break;
                case "STATUS":
                    current.Cancelled = value.Equals("CANCELLED", StringComparison.OrdinalIgnoreCase);
                    break;
                case "DESCRIPTION":
                    current.Description = Unescape(value);
                    break;
                case "CONFERENCE" or "X-GOOGLE-CONFERENCE" when current.Conference is null:
                    current.Conference = Unescape(value);
                    break;
                case "URL":
                    current.Url = Unescape(value);
                    break;
                case "TRANSP":
                    current.Transparent = value.Trim().Equals("TRANSPARENT", StringComparison.OrdinalIgnoreCase);
                    break;
                case "DTSTART" when TryStamp(value, zone, type, Resolve, out var start):
                    current.Start = start;
                    break;
                case "DTEND" when TryStamp(value, zone, type, Resolve, out var end):
                    current.End = end;
                    break;
                case "DURATION" when TryDuration(value, out var duration):
                    current.Duration = duration;
                    break;
                case "RRULE":
                    current.Rule = RecurrenceRule.Parse(value);
                    // An unknown rule would show only the first occurrence,
                    // which is wrong in the other direction; leave it out.
                    current.Unsupported |= current.Rule is null;
                    break;
                case "RDATE":
                    AddStamps(current.ExtraList, value, zone, type, Resolve);
                    break;
                case "EXDATE":
                    AddStamps(current.ExceptionList, value, zone, type, Resolve);
                    break;
                case "RECURRENCE-ID" when TryStamp(value, zone, type, Resolve, out var original):
                    current.RecurrenceId = original;
                    break;
            }
        }
        // A UID matters only to an override, to find what it replaces: without one, a calendar of thousands keeps none.
        if (!calendar.Events.Exists(item => item.RecurrenceId is not null))
        {
            foreach (var item in calendar.Events)
            {
                item.Uid = "";
            }
        }
        return calendar;
    }

    /// <summary>
    /// The occurrences that overlap [<paramref name="from"/>, <paramref name="to"/>), both wall-clock
    /// times in <paramref name="zone"/> (this PC's when null), as wall-clock times there, by start.
    /// A floating time and a whole day are the same there as anywhere.
    /// </summary>
    public List<CalendarEvent> Between(DateTime from, DateTime to, TimeZoneInfo? zone = null)
    {
        var frame = zone ?? TimeZoneInfo.Local;
        var window = new Window(from, to, Stamp.WallToUtc(from, frame), Stamp.WallToUtc(to, frame), frame);
        var found = new List<CalendarEvent>();
        Each(window, (item, at, length, allDay) => Add(found, item, at, length, allDay, window));
        found.Sort(static (a, b) => a.Start != b.Start ? a.Start.CompareTo(b.Start) : b.AllDay.CompareTo(a.AllDay));
        return found;
    }

    /// <summary>Every occurrence near <paramref name="window"/>, handed to <paramref name="add"/>, which decides what overlaps.</summary>
    private void Each(Window window, Action<FeedEvent, Stamp, TimeSpan, bool> add)
    {
        var (from, to, fromUtc, toUtc, frame) = window;
        // Instances that an override (RECURRENCE-ID) replaces, by UID; none where there is no override, as for most.
        Dictionary<string, List<Stamp>>? moved = null;
        foreach (var item in Events)
        {
            if (item.RecurrenceId is { } original)
            {
                moved ??= new Dictionary<string, List<Stamp>>(StringComparer.Ordinal);
                if (!moved.TryGetValue(item.Uid, out var list))
                {
                    moved[item.Uid] = list = [];
                }
                list.Add(original);
            }
        }
        foreach (var item in Events)
        {
            if (item.Cancelled && item.RecurrenceId is null)
            {
                continue;
            }
            var start = item.Start!.Value;
            var allDay = start.Kind == StampKind.Date;
            var length = Length(item, start, frame);
            if ((item.Rule is null && item.Extra.Count == 0) || item.RecurrenceId is not null)
            {
                if (!item.Cancelled)
                {
                    add(item, start, length, allDay);
                }
                continue;
            }
            // What EXDATE and the overrides take out, as wall-clock times in the event's own frame;
            // a whole-day exception takes out that day's occurrences whatever their time.
            HashSet<DateTime>? skipped = null;
            HashSet<DateTime>? skippedDays = null;
            List<Stamp>? replaced = null;
            if (moved?.TryGetValue(item.Uid, out replaced) == true || item.Exceptions.Count > 0)
            {
                foreach (var exception in replaced is not null ? item.Exceptions.Concat(replaced) : item.Exceptions)
                {
                    if (exception.Kind == StampKind.Date && !allDay)
                    {
                        (skippedDays ??= []).Add(exception.Wall.Date);
                    }
                    else
                    {
                        var wall = start.WallOf(exception, frame);
                        (skipped ??= []).Add(allDay ? wall.Date : wall);
                    }
                }
            }
            // The window in the event's own wall-clock time, a day wider on each side for zone
            // differences, and the event's length earlier, for an occurrence that started before
            // the window and is still on.
            var wallFrom = (allDay ? from.Date : start.WallOf(fromUtc, frame)) - length - TimeSpan.FromDays(1);
            var wallTo = (allDay ? to.Date : start.WallOf(toUtc, frame)) + TimeSpan.FromDays(1);
            var walls = item.Rule is { } rule ? Expand(rule, start, wallFrom, wallTo, frame) : [];
            if (item.Rule is null && start.Wall >= wallFrom && start.Wall < wallTo)
            {
                // RDATE alone: DTSTART is the first of the set.
                walls.Add(start.Wall);
            }
            foreach (var extra in item.Extra)
            {
                // A whole-day RDATE of a timed event happens at DTSTART's time of day.
                var wall = extra.Kind == StampKind.Date && !allDay ? extra.Wall.Date + start.Wall.TimeOfDay : start.WallOf(extra, frame);
                if (wall >= wallFrom && wall < wallTo && !walls.Contains(wall))
                {
                    walls.Add(wall);
                }
            }
            foreach (var wall in walls)
            {
                if (skipped?.Contains(allDay ? wall.Date : wall) != true && skippedDays?.Contains(wall.Date) != true)
                {
                    add(item, start.At(allDay ? wall.Date : wall), length, allDay);
                }
            }
        }
    }

    /// <summary>
    /// The wall-clock starts of a recurring event from its rule, DTSTART first,
    /// that fall in [<paramref name="from"/>, <paramref name="to"/>). A floating
    /// start meets a UTC UNTIL in <paramref name="frame"/> (this PC's zone when null).
    /// </summary>
    internal static List<DateTime> Expand(RecurrenceRule rule, Stamp start, DateTime from, DateTime to, TimeZoneInfo? frame = null)
    {
        var result = new List<DateTime>();
        var first = start.Wall;
        // UNTIL is inclusive; a date means the whole of that day.
        DateTime? until = rule.Until is { } limit
            ? limit.Kind == StampKind.Date
                ? limit.Wall.Date.AddDays(1).AddTicks(-1)
                : limit.Kind == StampKind.Utc && start.Kind != StampKind.Date
                    ? start.WallOf(DateTime.SpecifyKind(limit.Wall, DateTimeKind.Utc), frame ?? TimeZoneInfo.Local)
                    : limit.Wall
            : null;
        var count = 1;
        // DTSTART is the first occurrence whether or not the rule would pick it.
        if (first >= from && first < to)
        {
            result.Add(first);
        }
        if (rule.Count is 1)
        {
            return result;
        }
        // Without a count, whole periods before the window cannot matter: jump
        // near it. With one, every occurrence from the start has to be counted —
        // however far back the start is: a daily series of 40,000 begun in 1970
        // is still going today. Given up on after MaxPeriods, as the rule without
        // a count is, it lost today's occurrences once its start was 55 years
        // back. So the count goes on to the window whatever it takes, and leaves
        // marks on the rule (Remember) that the next expansion starts from.
        var period = rule.Count is null ? Skip(rule, first, from) : PeriodOf(rule, first);
        if (rule.Count is not null && rule.Mark(first, PeriodOf(rule, from)) is { } mark && mark.Period > period)
        {
            (period, count) = mark;
        }
        var most = rule.Count is null ? MaxPeriods : MaxCounted;
        var candidates = new List<DateTime>();
        for (var guard = 0; guard < most; guard++)
        {
            if (rule.Count is not null && guard > 0 && guard % CountMarks == 0)
            {
                rule.Remember(first, period, count);
            }
            candidates.Clear();
            Candidates(rule, period, first, candidates);
            foreach (var candidate in candidates)
            {
                if (candidate <= first)
                {
                    continue;
                }
                if (until is { } end && candidate > end)
                {
                    return result;
                }
                if (candidate >= to)
                {
                    return result;
                }
                count++;
                if (candidate >= from)
                {
                    result.Add(candidate);
                }
                if (rule.Count is { } total && count >= total)
                {
                    return result;
                }
            }
            period = Next(rule, period);
            if (period > to || period.Year >= 9999)
            {
                return result;
            }
        }
        return result;
    }

    /// <summary>The first day of the period (day, week, month or year) that holds <paramref name="day"/>.</summary>
    private static DateTime PeriodOf(RecurrenceRule rule, DateTime day) => rule.Frequency switch
    {
        Frequency.Daily => day.Date,
        Frequency.Weekly => day.Date.AddDays(-(((int)day.DayOfWeek - (int)rule.WeekStart + 7) % 7)),
        Frequency.Monthly => new DateTime(day.Year, day.Month, 1),
        _ => new DateTime(day.Year, 1, 1),
    };

    private static DateTime Next(RecurrenceRule rule, DateTime period) => Advance(rule, period, 1);

    private static DateTime Advance(RecurrenceRule rule, DateTime period, long steps)
    {
        var by = rule.Interval * steps;
        try
        {
            return rule.Frequency switch
            {
                Frequency.Daily => period.AddDays(by),
                Frequency.Weekly => period.AddDays(7 * by),
                Frequency.Monthly => period.AddMonths((int)Math.Min(by, 12 * 9000)),
                _ => period.AddYears((int)Math.Min(by, 9000)),
            };
        }
        catch (ArgumentOutOfRangeException)
        {
            // Past what DateTime holds: nothing more to find.
            return DateTime.MaxValue.Date;
        }
    }

    /// <summary>The period of the rule's own rhythm last before <paramref name="from"/>, one more back for safety.</summary>
    private static DateTime Skip(RecurrenceRule rule, DateTime first, DateTime from)
    {
        var period = PeriodOf(rule, first);
        var target = PeriodOf(rule, from);
        if (target <= period)
        {
            return period;
        }
        long units = rule.Frequency switch
        {
            Frequency.Daily => (target - period).Days,
            Frequency.Weekly => (target - period).Days / 7,
            Frequency.Monthly => (target.Year - period.Year) * 12L + target.Month - period.Month,
            _ => target.Year - period.Year,
        };
        var steps = units / rule.Interval - 1;
        return steps > 0 ? Advance(rule, period, steps) : period;
    }

    /// <summary>
    /// The occurrences a period holds, sorted: the days its rule parts pick, each at the times
    /// BYHOUR and BYMINUTE pick (the start's time of day by default), then BYSETPOS.
    /// </summary>
    private static void Candidates(RecurrenceRule rule, DateTime period, DateTime first, List<DateTime> into)
    {
        var days = new List<DateTime>();
        switch (rule.Frequency)
        {
            case Frequency.Daily:
                if (InMonths(rule, period) && InMonthDays(rule, period) && (rule.ByDay.Count == 0 || rule.ByDay.Exists(entry => entry.Day == period.DayOfWeek)))
                {
                    days.Add(period);
                }
                break;
            case Frequency.Weekly:
                for (var day = 0; day < 7; day++)
                {
                    var date = period.AddDays(day);
                    var weekday = rule.ByDay.Count > 0
                        ? rule.ByDay.Exists(entry => entry.Day == date.DayOfWeek)
                        : date.DayOfWeek == first.DayOfWeek;
                    if (weekday && InMonths(rule, date))
                    {
                        days.Add(date);
                    }
                }
                break;
            case Frequency.Monthly:
                if (InMonths(rule, period))
                {
                    MonthDays(rule, period, first, days);
                }
                break;
            default:
                YearDays(rule, period.Year, first, days);
                break;
        }
        var hours = rule.ByHour.Count > 0 ? rule.ByHour : [first.Hour];
        var minutes = rule.ByMinute.Count > 0 ? rule.ByMinute : [first.Minute];
        foreach (var day in days)
        {
            foreach (var hour in hours)
            {
                foreach (var minute in minutes)
                {
                    into.Add(day.Date + new TimeSpan(hour, minute, first.Second));
                }
            }
        }
        into.Sort();
        if (rule.BySetPos.Count > 0 && into.Count > 0)
        {
            var all = into.ToArray();
            into.Clear();
            foreach (var position in rule.BySetPos)
            {
                var index = position > 0 ? position - 1 : all.Length + position;
                if (index >= 0 && index < all.Length && !into.Contains(all[index]))
                {
                    into.Add(all[index]);
                }
            }
            into.Sort();
        }
    }

    /// <summary>The days of the month starting at <paramref name="month"/> that a monthly rule picks.</summary>
    private static void MonthDays(RecurrenceRule rule, DateTime month, DateTime first, List<DateTime> into)
    {
        var length = DateTime.DaysInMonth(month.Year, month.Month);
        var last = month.AddDays(length - 1);
        var nth = Ordinals(rule, month, last);
        for (var date = month; date <= last; date = date.AddDays(1))
        {
            if (rule.ByMonthDay.Count == 0 && rule.ByDay.Count == 0)
            {
                // A monthly event on the 31st skips the months without one, as RFC 5545 says.
                if (date.Day == first.Day)
                {
                    into.Add(date);
                }
                continue;
            }
            if (InMonthDays(rule, date) && InWeekdays(rule, date, nth))
            {
                into.Add(date);
            }
        }
    }

    /// <summary>The days of <paramref name="year"/> that a yearly rule picks.</summary>
    private static void YearDays(RecurrenceRule rule, int year, DateTime first, List<DateTime> into)
    {
        var start = new DateTime(year, 1, 1);
        var end = new DateTime(year, 12, 31);
        var dayParts = rule.ByWeekNo.Count > 0 || rule.ByYearDay.Count > 0 || rule.ByMonthDay.Count > 0 || rule.ByDay.Count > 0;
        if (!dayParts)
        {
            // Plain yearly, a birthday: DTSTART's day, in each month BYMONTH names or in DTSTART's month,
            // where the month has that day. No walk through the year's days: a calendar keeps hundreds.
            foreach (var month in rule.ByMonth.Count > 0 ? rule.ByMonth : [first.Month])
            {
                if (first.Day <= DateTime.DaysInMonth(year, month))
                {
                    into.Add(new DateTime(year, month, first.Day));
                }
            }
            return;
        }
        // An ordinal BYDAY counts within each month when BYMONTH is there, else within the
        // year; with BYWEEKNO it has none (RFC 5545 §3.3.10).
        HashSet<DateTime>? nth = null;
        if (rule.ByDay.Exists(entry => entry.Ordinal != 0) && rule.ByWeekNo.Count == 0)
        {
            if (rule.ByMonth.Count > 0)
            {
                nth = [];
                foreach (var month in rule.ByMonth)
                {
                    var from = new DateTime(year, month, 1);
                    nth.UnionWith(Ordinals(rule, from, from.AddMonths(1).AddDays(-1)) ?? []);
                }
            }
            else
            {
                nth = Ordinals(rule, start, end);
            }
        }
        for (var date = start; date <= end; date = date.AddDays(1))
        {
            if (!InMonths(rule, date))
            {
                continue;
            }
            if (rule.ByWeekNo.Count > 0 && !InWeeks(rule, date))
            {
                continue;
            }
            if (rule.ByYearDay.Count > 0 && !rule.ByYearDay.Exists(day => day > 0 ? date.DayOfYear == day : date.DayOfYear == (DateTime.IsLeapYear(year) ? 366 : 365) + day + 1))
            {
                continue;
            }
            if (!InMonthDays(rule, date))
            {
                continue;
            }
            if (rule.ByDay.Count > 0 && !InWeekdays(rule, date, nth))
            {
                continue;
            }
            // BYWEEKNO alone picks DTSTART's weekday in those weeks.
            if (rule.ByWeekNo.Count > 0 && rule.ByDay.Count == 0 && rule.ByYearDay.Count == 0 && rule.ByMonthDay.Count == 0
                && date.DayOfWeek != first.DayOfWeek)
            {
                continue;
            }
            into.Add(date);
        }
    }

    private static bool InMonths(RecurrenceRule rule, DateTime date) => rule.ByMonth.Count == 0 || rule.ByMonth.Contains(date.Month);

    private static bool InMonthDays(RecurrenceRule rule, DateTime date) =>
        rule.ByMonthDay.Count == 0 || rule.ByMonthDay.Exists(day =>
            day > 0 ? date.Day == day : date.Day == DateTime.DaysInMonth(date.Year, date.Month) + day + 1);

    /// <summary>Whether BYDAY takes <paramref name="date"/>: its weekday, or its place among them (<paramref name="nth"/>).</summary>
    private static bool InWeekdays(RecurrenceRule rule, DateTime date, HashSet<DateTime>? nth) =>
        rule.ByDay.Count == 0
        || rule.ByDay.Exists(entry => entry.Ordinal == 0 && entry.Day == date.DayOfWeek)
        || (nth?.Contains(date) ?? false);

    /// <summary>The days BYDAY's ordinals ("2TU", "-1FR") pick between two dates; null when it has none.</summary>
    private static HashSet<DateTime>? Ordinals(RecurrenceRule rule, DateTime start, DateTime end)
    {
        HashSet<DateTime>? days = null;
        foreach (var (ordinal, weekday) in rule.ByDay)
        {
            if (ordinal != 0 && Nth(start, end, ordinal, weekday) is { } date)
            {
                (days ??= []).Add(date);
            }
        }
        return days;
    }

    /// <summary>Whether <paramref name="date"/> lies in a week BYWEEKNO names, weeks counted from WKST as ISO 8601 does.</summary>
    private static bool InWeeks(RecurrenceRule rule, DateTime date)
    {
        var (week, weeks) = WeekNumber(date, rule.WeekStart);
        return rule.ByWeekNo.Exists(entry => entry > 0 ? entry == week : weeks + entry + 1 == week);
    }

    /// <summary>
    /// The week of its year <paramref name="date"/> is in, and how many weeks that year has: week 1 is
    /// the first week (from <paramref name="weekStart"/>) with at least four days of the year.
    /// </summary>
    internal static (int Week, int Weeks) WeekNumber(DateTime date, DayOfWeek weekStart)
    {
        static DateTime FirstWeek(int year, DayOfWeek weekStart)
        {
            var january = new DateTime(year, 1, 1);
            var into = ((int)january.DayOfWeek - (int)weekStart + 7) % 7;
            return into <= 3 ? january.AddDays(-into) : january.AddDays(7 - into);
        }
        var year = date.Year;
        var begins = FirstWeek(year, weekStart);
        if (date < begins)
        {
            year--;
            begins = FirstWeek(year, weekStart);
        }
        else if (year < 9999 && date >= FirstWeek(year + 1, weekStart))
        {
            year++;
            begins = FirstWeek(year, weekStart);
        }
        var ends = year < 9999 ? FirstWeek(year + 1, weekStart) : new DateTime(9999, 12, 31);
        return ((date - begins).Days / 7 + 1, (ends - begins).Days / 7);
    }

    /// <summary>The <paramref name="ordinal"/>th <paramref name="weekday"/> between two dates; negative counts from the end.</summary>
    private static DateTime? Nth(DateTime start, DateTime end, int ordinal, DayOfWeek weekday)
    {
        if (ordinal > 0)
        {
            var date = start.AddDays(((int)weekday - (int)start.DayOfWeek + 7) % 7 + 7 * (ordinal - 1));
            return date <= end ? date : null;
        }
        var last = end.AddDays(-(((int)end.DayOfWeek - (int)weekday + 7) % 7) + 7 * (ordinal + 1));
        return last >= start ? last : null;
    }

    internal static TimeSpan Length(FeedEvent item, Stamp start, TimeZoneInfo frame)
    {
        if (item.End is { } end)
        {
            var length = start.Kind == StampKind.Date || end.Kind == StampKind.Date
                ? end.Wall.Date - start.Wall.Date
                : end.ToUtc(frame) - start.ToUtc(frame);
            return length > TimeSpan.Zero ? length : start.Kind == StampKind.Date ? TimeSpan.FromDays(1) : TimeSpan.Zero;
        }
        if (item.Duration is { } duration && duration > TimeSpan.Zero)
        {
            return duration;
        }
        return start.Kind == StampKind.Date ? TimeSpan.FromDays(1) : TimeSpan.Zero;
    }

    /// <summary>The window asked for: wall-clock times in <paramref name="Frame"/> and the instants they are.</summary>
    private readonly record struct Window(DateTime From, DateTime To, DateTime FromUtc, DateTime ToUtc, TimeZoneInfo Frame);

    private static void Add(List<CalendarEvent> found, FeedEvent item, Stamp at, TimeSpan length, bool allDay, Window window)
    {
        var local = window.Frame == TimeZoneInfo.Local;
        DateTime start;
        DateTime end;
        if (allDay)
        {
            // The same day wherever the calendar is shown.
            start = DateTime.SpecifyKind(at.Wall.Date, local ? DateTimeKind.Local : DateTimeKind.Unspecified);
            end = start + length;
            if (end <= window.From || start >= window.To)
            {
                return;
            }
        }
        else
        {
            var startUtc = at.ToUtc(window.Frame);
            var endUtc = startUtc + length;
            // An event without length is on at its start.
            if ((length > TimeSpan.Zero ? endUtc <= window.FromUtc : startUtc < window.FromUtc) || startUtc >= window.ToUtc)
            {
                return;
            }
            start = TimeZoneInfo.ConvertTimeFromUtc(startUtc, window.Frame);
            end = TimeZoneInfo.ConvertTimeFromUtc(endUtc, window.Frame);
        }
        var title = item.Summary.Length > 0 ? item.Summary : S.DockCalendarNoTitle;
        found.Add(new CalendarEvent(start, end, allDay, title, string.IsNullOrWhiteSpace(item.Location) ? null : item.Location,
            Join: item.Join, Free: item.Transparent));
    }

    /// <summary>A calendar-level property: its name and colour.</summary>
    private void Own(string name, string value)
    {
        switch (name)
        {
            case "X-WR-CALNAME":
            case "NAME" when Name is null:
                if (Unescape(value) is { Length: > 0 } title)
                {
                    Name = title;
                }
                break;
            case "X-APPLE-CALENDAR-COLOR":
            case "COLOR" when Colour is null:
                if (CalendarColours.Parse(value) is { } colour)
                {
                    Colour = colour;
                }
                break;
        }
    }

    private static void AddStamps(List<Stamp> into, string value, string? zone, string? type, Func<string, TimeZoneInfo?> resolve)
    {
        foreach (var item in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            // A PERIOD ("start/end" or "start/duration") counts from its start.
            var slash = item.IndexOf('/');
            var text = slash > 0 ? item[..slash] : item;
            var kind = string.Equals(type, "PERIOD", StringComparison.OrdinalIgnoreCase) ? null : type;
            if (TryStamp(text, zone, kind, resolve, out var stamp))
            {
                into.Add(stamp);
            }
        }
    }

    internal static bool TryStamp(string value, string? zoneId, string? type, Func<string, TimeZoneInfo?> resolve, out Stamp stamp)
    {
        value = value.Trim();
        stamp = default;
        if (string.Equals(type, "DATE", StringComparison.OrdinalIgnoreCase) || value.Length == 8)
        {
            if (DateTime.TryParseExact(value.AsSpan(0, Math.Min(8, value.Length)), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                stamp = new Stamp(date, StampKind.Date, null);
                return true;
            }
            return false;
        }
        var utc = value.EndsWith('Z') || value.EndsWith('z');
        var text = utc ? value[..^1] : value;
        if (!DateTime.TryParseExact(text, "yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var wall)
            && !DateTime.TryParseExact(text, "yyyyMMdd'T'HHmm", CultureInfo.InvariantCulture, DateTimeStyles.None, out wall))
        {
            return false;
        }
        if (utc)
        {
            stamp = new Stamp(wall, StampKind.Utc, null);
        }
        else if (zoneId is not null && resolve(zoneId.Trim('"')) is { } zone)
        {
            stamp = new Stamp(wall, StampKind.Zoned, zone);
        }
        else
        {
            // No zone, or one neither Windows, ICU nor the feed knows: the calendar's own time is the best guess.
            stamp = new Stamp(wall, StampKind.Floating, null);
        }
        return true;
    }

    /// <summary>"PT1H30M", "P1D", "P2W", "-PT15M".</summary>
    internal static bool TryDuration(string value, out TimeSpan duration)
    {
        duration = TimeSpan.Zero;
        var text = value.Trim().ToUpperInvariant();
        var negative = text.StartsWith('-');
        text = text.TrimStart('+', '-');
        if (!text.StartsWith('P'))
        {
            return false;
        }
        var number = 0;
        var inTime = false;
        var any = false;
        foreach (var c in text.AsSpan(1))
        {
            if (char.IsAsciiDigit(c))
            {
                number = checked(number * 10 + (c - '0'));
                continue;
            }
            switch (c)
            {
                case 'T':
                    inTime = true;
                    continue;
                case 'W' when !inTime:
                    duration += TimeSpan.FromDays(7 * number);
                    break;
                case 'D' when !inTime:
                    duration += TimeSpan.FromDays(number);
                    break;
                case 'H' when inTime:
                    duration += TimeSpan.FromHours(number);
                    break;
                case 'M' when inTime:
                    duration += TimeSpan.FromMinutes(number);
                    break;
                case 'S' when inTime:
                    duration += TimeSpan.FromSeconds(number);
                    break;
                default:
                    return false;
            }
            any = true;
            number = 0;
        }
        if (negative)
        {
            duration = -duration;
        }
        return any;
    }

    /// <summary>Unfolded content lines, split into name, parameters and value.</summary>
    internal static IEnumerable<(string Name, Dictionary<string, string>? Parameters, string Value)> Lines(string text)
    {
        var line = new StringBuilder();
        using var reader = new StringReader(text);
        while (reader.ReadLine() is { } raw)
        {
            if (raw.Length > 0 && raw[0] is ' ' or '\t')
            {
                line.Append(raw, 1, raw.Length - 1);
                continue;
            }
            if (line.Length > 0 && Split(line.ToString()) is { } parsed)
            {
                yield return parsed;
            }
            line.Clear().Append(raw);
        }
        if (line.Length > 0 && Split(line.ToString()) is { } last)
        {
            yield return last;
        }
    }

    private static (string, Dictionary<string, string>?, string)? Split(string line)
    {
        var quoted = false;
        var colon = -1;
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == '"')
            {
                quoted = !quoted;
            }
            else if (line[i] == ':' && !quoted)
            {
                colon = i;
                break;
            }
        }
        if (colon <= 0)
        {
            return null;
        }
        var head = line[..colon];
        var value = line[(colon + 1)..];
        var semicolon = head.IndexOf(';');
        if (semicolon < 0)
        {
            return (head.Trim().ToUpperInvariant(), null, value);
        }
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in SplitOutsideQuotes(head[(semicolon + 1)..], ';'))
        {
            var equals = part.IndexOf('=');
            if (equals > 0)
            {
                parameters[part[..equals].Trim()] = part[(equals + 1)..].Trim().Trim('"');
            }
        }
        return (head[..semicolon].Trim().ToUpperInvariant(), parameters, value);
    }

    private static IEnumerable<string> SplitOutsideQuotes(string text, char separator)
    {
        var quoted = false;
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '"')
            {
                quoted = !quoted;
            }
            else if (text[i] == separator && !quoted)
            {
                yield return text[start..i];
                start = i + 1;
            }
        }
        yield return text[start..];
    }

    private static string Unescape(string value)
    {
        if (!value.Contains('\\'))
        {
            return value.Trim();
        }
        var text = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '\\' && i + 1 < value.Length)
            {
                i++;
                text.Append(value[i] is 'n' or 'N' ? ' ' : value[i]);
            }
            else
            {
                text.Append(value[i]);
            }
        }
        return text.ToString().Trim();
    }
}

/// <summary>
/// The zones a feed defines itself (VTIMEZONE), for TZIDs that neither Windows nor ICU knows, such as
/// Outlook's "(UTC+01:00) Amsterdam, Berlin, …". The current rules only: the STANDARD and DAYLIGHT
/// parts that start last, each a yearly rule on the nth (or last) weekday of a month, or a fixed date.
/// </summary>
internal static class VTimeZones
{
    public static Dictionary<string, TimeZoneInfo> Read(List<(string Name, Dictionary<string, string>? Parameters, string Value)> lines)
    {
        var zones = new Dictionary<string, TimeZoneInfo>(StringComparer.OrdinalIgnoreCase);
        string? id = null;
        Part? part = null;
        Part? standard = null;
        Part? daylight = null;
        foreach (var (name, _, value) in lines)
        {
            switch (name)
            {
                case "BEGIN" when value.Equals("VTIMEZONE", StringComparison.OrdinalIgnoreCase):
                    (id, standard, daylight) = ("", null, null);
                    break;
                case "BEGIN" when id is not null && (value.Equals("STANDARD", StringComparison.OrdinalIgnoreCase) || value.Equals("DAYLIGHT", StringComparison.OrdinalIgnoreCase)):
                    part = new Part(value.Equals("DAYLIGHT", StringComparison.OrdinalIgnoreCase));
                    break;
                case "END" when part is not null && (value.Equals("STANDARD", StringComparison.OrdinalIgnoreCase) || value.Equals("DAYLIGHT", StringComparison.OrdinalIgnoreCase)):
                    // The part that starts last holds the rules in force.
                    if (part.Daylight)
                    {
                        daylight = Later(daylight, part);
                    }
                    else
                    {
                        standard = Later(standard, part);
                    }
                    part = null;
                    break;
                case "END" when value.Equals("VTIMEZONE", StringComparison.OrdinalIgnoreCase):
                    if (id is { Length: > 0 } && standard is not null && Build(id, standard, daylight) is { } zone)
                    {
                        zones[id] = zone;
                    }
                    id = null;
                    break;
                case "TZID" when id is not null && part is null:
                    id = value.Trim().Trim('"');
                    break;
                case "DTSTART" when part is not null:
                    if (DateTime.TryParseExact(value.Trim(), "yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start))
                    {
                        part.Start = start;
                    }
                    break;
                case "TZOFFSETTO" when part is not null:
                    part.To = Offset(value);
                    break;
                case "RRULE" when part is not null:
                    part.Rule = RecurrenceRule.Parse(value);
                    break;
            }
        }
        return zones;
    }

    private static Part? Later(Part? known, Part next) => known is null || next.Start >= known.Start ? next : known;

    private static TimeZoneInfo? Build(string id, Part standard, Part? daylight)
    {
        if (standard.To is not { } baseOffset)
        {
            return null;
        }
        try
        {
            if (daylight is null || daylight.To is not { } summer || summer == baseOffset
                || Transition(daylight) is not { } begins || Transition(standard) is not { } ends)
            {
                return TimeZoneInfo.CreateCustomTimeZone(id, baseOffset, id, id);
            }
            var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
                DateTime.MinValue.Date, DateTime.MaxValue.Date, summer - baseOffset, begins, ends);
            return TimeZoneInfo.CreateCustomTimeZone(id, baseOffset, id, id, id, [rule]);
        }
        catch (Exception error) when (error is ArgumentException or InvalidTimeZoneException)
        {
            return null;
        }
    }

    /// <summary>When a part starts each year, in the wall-clock time before it (its DTSTART's time of day).</summary>
    private static TimeZoneInfo.TransitionTime? Transition(Part part)
    {
        var time = new DateTime(1, 1, 1) + part.Start.TimeOfDay;
        if (part.Rule is not { Frequency: Frequency.Yearly } rule)
        {
            return null;
        }
        var month = rule.ByMonth.Count > 0 ? rule.ByMonth[0] : part.Start.Month;
        if (rule.ByDay.Count > 0)
        {
            var (ordinal, day) = rule.ByDay[0];
            // TransitionTime counts weeks 1 to 5, 5 being the last.
            var week = ordinal is >= 1 and <= 4 ? ordinal : ordinal < 0 || ordinal == 5 ? 5 : 1;
            if (rule.ByMonthDay.Count > 0 && rule.ByMonthDay[0] > 0)
            {
                // "The Sunday on or after the 8th" and the like: the week the day falls in.
                week = Math.Clamp((rule.ByMonthDay[0] - 1) / 7 + 1, 1, 5);
            }
            return TimeZoneInfo.TransitionTime.CreateFloatingDateRule(time, month, week, day);
        }
        var date = rule.ByMonthDay.Count > 0 && rule.ByMonthDay[0] > 0 ? rule.ByMonthDay[0] : part.Start.Day;
        return TimeZoneInfo.TransitionTime.CreateFixedDateRule(time, month, date);
    }

    /// <summary>"+0100", "-0530", "+013000".</summary>
    private static TimeSpan? Offset(string value)
    {
        var text = value.Trim();
        if (text.Length < 5 || text[0] is not ('+' or '-'))
        {
            return null;
        }
        if (!int.TryParse(text.AsSpan(1, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var hours)
            || !int.TryParse(text.AsSpan(3, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var minutes))
        {
            return null;
        }
        var offset = new TimeSpan(hours, minutes, 0);
        return text[0] == '-' ? -offset : offset;
    }

    /// <summary>A STANDARD or DAYLIGHT part: from when (in the wall-clock time before it), to which offset, and how it repeats.</summary>
    private sealed class Part(bool daylight)
    {
        public bool Daylight { get; } = daylight;
        public DateTime Start { get; set; }
        public TimeSpan? To { get; set; }
        public RecurrenceRule? Rule { get; set; }
    }
}
