// A CalDAV calendar's events in memory, kept whole (docs\widgets.md §616): every
// iCalendar object of it, parsed once, by its href, with the span of time it
// can have anything in. A view asks for a month, a week, a year; only the
// objects whose span meets it are expanded (ICalendar.Between), so a calendar
// of ten thousand events draws a month as fast as one of a hundred.
//
// A span is in UTC and generous: an object's first start to its last end, a
// day wider each side for a floating time or a whole day, whose instant
// depends on the zone it is shown in. A series with an UNTIL ends with its
// last occurrence; one with a COUNT, or none at all, is open: it may have
// something in any year after it began, and is always expanded (the rule's
// marks keep a COUNT from long ago cheap, RecurrenceRule.Mark). An override
// (RECURRENCE-ID) and an RDATE widen the span to hold their own times.
//
// Immutable: a sync's changes make a new index (With), which the store
// swaps in under its lock, so a view reads one without any lock of its own.

namespace Karpo.Dock;

internal sealed class EventIndex
{
    public static readonly EventIndex Empty = new(new Dictionary<string, Entry>(StringComparer.Ordinal));

    /// <summary>The wider margin every span gets each side: a whole day or a floating time is the same wall clock in every zone.</summary>
    private static readonly TimeSpan Margin = TimeSpan.FromDays(1);

    private readonly Dictionary<string, Entry> byHref;

    /// <summary>Every entry with events, by the start of its span: a view's scan stops at the first that starts after it.</summary>
    private readonly Entry[] byStart;

    private EventIndex(Dictionary<string, Entry> byHref)
    {
        this.byHref = byHref;
        byStart = [.. byHref.Values.Where(entry => entry.Span is not null).OrderBy(entry => entry.Span!.Value.From)];
        Dropped = byHref.Values.Sum(entry => entry.Calendar.Dropped);
    }

    /// <summary>How many objects it holds.</summary>
    public int Count => byHref.Count;

    /// <summary>Events in its objects Karpo cannot show (<see cref="ICalendar.Dropped"/>): no start it can read, an hourly rule.</summary>
    public int Dropped { get; }

    /// <summary>An index of <paramref name="objects"/>.</summary>
    public static EventIndex Of(IEnumerable<(string Key, ICalendar Calendar)> objects)
    {
        var all = new Dictionary<string, Entry>(StringComparer.Ordinal);
        foreach (var (key, calendar) in objects)
        {
            all[key] = new Entry(calendar, Span(calendar));
        }
        return new EventIndex(all);
    }

    /// <summary>This index with <paramref name="changed"/> put in (in place of any it held by their key) and <paramref name="removed"/> taken out.</summary>
    public EventIndex With(IEnumerable<(string Key, ICalendar Calendar)> changed, IEnumerable<string> removed)
    {
        var all = new Dictionary<string, Entry>(byHref, StringComparer.Ordinal);
        foreach (var key in removed)
        {
            all.Remove(key);
        }
        foreach (var (key, calendar) in changed)
        {
            all[key] = new Entry(calendar, Span(calendar));
        }
        return new EventIndex(all);
    }

    /// <summary>
    /// The occurrences that overlap [<paramref name="from"/>, <paramref name="to"/>), wall-clock times in
    /// <paramref name="zone"/>, as <see cref="ICalendar.Between"/> gives them, object by object.
    /// </summary>
    public List<CalendarEvent> Between(DateTime from, DateTime to, TimeZoneInfo zone)
    {
        var found = new List<CalendarEvent>();
        foreach (var entry in Near(Stamp.WallToUtc(from, zone), Stamp.WallToUtc(to, zone)))
        {
            found.AddRange(entry.Calendar.Between(from, to, zone));
        }
        return found;
    }

    /// <summary>How many occurrences overlap [<paramref name="fromUtc"/>, <paramref name="toUtc"/>), a whole day or a floating time read as if in UTC.</summary>
    public int CountIn(DateTime fromUtc, DateTime toUtc)
    {
        var count = 0;
        foreach (var entry in Near(fromUtc, toUtc))
        {
            count += entry.Calendar.Between(fromUtc, toUtc, TimeZoneInfo.Utc).Count;
        }
        return count;
    }

    /// <summary>The entries whose span meets [<paramref name="fromUtc"/>, <paramref name="toUtc"/>).</summary>
    private IEnumerable<Entry> Near(DateTime fromUtc, DateTime toUtc)
    {
        foreach (var entry in byStart)
        {
            var (start, end) = entry.Span!.Value;
            if (start >= toUtc)
            {
                yield break;
            }
            if (end >= fromUtc)
            {
                yield return entry;
            }
        }
    }

    /// <summary>
    /// The UTC span <paramref name="calendar"/> may have occurrences in, a <see cref="Margin"/> wider each side; null
    /// where it has no event to show. Open-ended (<see cref="DateTime.MaxValue"/>) for a series with a COUNT or no end.
    /// </summary>
    internal static (DateTime From, DateTime To)? Span(ICalendar calendar)
    {
        DateTime? first = null;
        DateTime? last = null;
        void Take(DateTime from, DateTime to)
        {
            first = first is { } had && had <= from ? had : from;
            last = last is { } was && was >= to ? was : to;
        }
        foreach (var item in calendar.Events)
        {
            var start = item.Start!.Value;
            var length = ICalendar.Length(item, start, TimeZoneInfo.Utc);
            var at = Instant(start);
            Take(Less(at), More(Add(at, length)));
            if (item.RecurrenceId is not null)
            {
                continue;
            }
            foreach (var extra in item.Extra)
            {
                // A whole-day RDATE of a timed event happens at DTSTART's time of day: within the day after it.
                var on = Instant(extra);
                Take(Less(on), More(Add(Add(on, length), Margin)));
            }
            if (item.Rule is { } rule)
            {
                Take(Less(at), More(Add(Add(Last(rule, start), length), Margin)));
            }
        }
        return first is { } from && last is { } to ? (from, to) : null;
    }

    /// <summary>
    /// When a series' last occurrence starts, as an instant a <see cref="Margin"/> early at most: its UNTIL, or its
    /// COUNT's last, counted once here where it is no more than <see cref="MostCounted"/> (two years of a daily one).
    /// <see cref="DateTime.MaxValue"/> where it has no end, or one too far to count now.
    /// </summary>
    private static DateTime Last(RecurrenceRule rule, Stamp start)
    {
        var last = rule.Until is { } until ? Add(Instant(until), Margin) : DateTime.MaxValue;
        if (rule.Count is { } count && count <= MostCounted)
        {
            // Twice the periods the count needs, were each to hold one: enough for any rule that picks one a period.
            var far = Periods(rule, start.Wall, (long)count * rule.Interval * 2 + 2);
            var walls = ICalendar.Expand(rule, start, start.Wall, far, TimeZoneInfo.Utc);
            if (walls.Count >= count && Instant(start.At(walls[^1])) is var counted && counted < last)
            {
                last = counted;
            }
        }
        return last;
    }

    /// <summary>A COUNT counted when an object is indexed, at most: past it a series stays open, and each view counts it.</summary>
    private const int MostCounted = 1000;

    /// <summary><paramref name="steps"/> of the rule's periods after <paramref name="wall"/>; the last day DateTime holds where that is past it.</summary>
    private static DateTime Periods(RecurrenceRule rule, DateTime wall, long steps)
    {
        try
        {
            return rule.Frequency switch
            {
                Frequency.Daily => wall.AddDays(steps),
                Frequency.Weekly => wall.AddDays(7 * steps),
                Frequency.Monthly => wall.AddMonths((int)Math.Min(steps, 12 * 9000)),
                _ => wall.AddYears((int)Math.Min(steps, 9000)),
            };
        }
        catch (ArgumentOutOfRangeException)
        {
            return DateTime.MaxValue.Date;
        }
    }

    /// <summary>A stamp's instant, a whole day or a floating time read as if in UTC (the margin covers every zone).</summary>
    private static DateTime Instant(Stamp stamp) => stamp.Kind == StampKind.Zoned ? stamp.ToUtc(TimeZoneInfo.Utc) : DateTime.SpecifyKind(stamp.Wall, DateTimeKind.Utc);

    private static DateTime Less(DateTime at) => at.Ticks - DateTime.MinValue.Ticks > Margin.Ticks ? at - Margin : DateTime.MinValue;

    private static DateTime More(DateTime at) => DateTime.MaxValue.Ticks - at.Ticks > Margin.Ticks ? at + Margin : DateTime.MaxValue;

    private static DateTime Add(DateTime at, TimeSpan length) => DateTime.MaxValue.Ticks - at.Ticks > length.Ticks ? at + length : DateTime.MaxValue;

    /// <summary>One object, and the span it may have occurrences in (null: none it can show).</summary>
    private sealed record Entry(ICalendar Calendar, (DateTime From, DateTime To)? Span);
}
