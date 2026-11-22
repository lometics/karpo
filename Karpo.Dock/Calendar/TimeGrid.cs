// The day and week views' time grid, as numbers: which events go to the
// all-day row and in which lane, the timed parts of events on each day, and how
// events that overlap sit side by side in a day's column. Pure, so every rule
// has a test (Karpo.Tests\Calendar).

namespace Karpo.Dock;

/// <summary>An event's place in a day's column: its column among those its overlapping group needs, how many that is, and how many it spans.</summary>
internal readonly record struct Placement(int Column, int Columns, int Span);

/// <summary>The part of a timed event on one day of the grid, in minutes from that day's midnight.</summary>
/// <param name="Day">The day's index in the grid, from 0.</param>
internal readonly record struct Segment(int Day, double Start, double End, CalendarEvent Event);

/// <summary>An event on the all-day row: the days it covers in the grid, [First, Last), and its lane.</summary>
internal readonly record struct Bar(int First, int Last, int Lane, CalendarEvent Event);

internal static class TimeGrid
{
    public const double DayMinutes = 24 * 60;

    /// <summary>
    /// Whether an event goes to the all-day row rather than the hours: all-day events, and timed
    /// ones of a whole day or more, as Outlook shows them.
    /// </summary>
    public static bool AllDayRow(CalendarEvent item) => item.AllDay || item.End - item.Start >= TimeSpan.FromDays(1);

    /// <summary>
    /// The timed parts of <paramref name="events"/> on the <paramref name="days"/> days from
    /// <paramref name="first"/>: an event over midnight has a part on each day. Those for the all-day
    /// row are left out.
    /// </summary>
    public static List<Segment> Segments(IEnumerable<CalendarEvent> events, DateOnly first, int days)
    {
        var found = new List<Segment>();
        var origin = first.ToDateTime(TimeOnly.MinValue);
        foreach (var item in events)
        {
            if (AllDayRow(item))
            {
                continue;
            }
            for (var day = 0; day < days; day++)
            {
                var start = origin.AddDays(day);
                var end = start.AddDays(1);
                if (!CalendarStore.Overlaps(item, start, end))
                {
                    continue;
                }
                var from = item.Start > start ? (item.Start - start).TotalMinutes : 0;
                var to = item.End < end ? (item.End - start).TotalMinutes : DayMinutes;
                found.Add(new Segment(day, from, Math.Max(from, to), item));
            }
        }
        return found;
    }

    /// <summary>
    /// The all-day row's bars for the <paramref name="days"/> days from <paramref name="first"/>, each
    /// in the first lane free over all its days; longer ones first where they start together. Also
    /// how many lanes that takes.
    /// </summary>
    public static (List<Bar> Bars, int Lanes) AllDayBars(IEnumerable<CalendarEvent> events, DateOnly first, int days)
    {
        var origin = first.ToDateTime(TimeOnly.MinValue);
        var spans = new List<(int First, int Last, CalendarEvent Event)>();
        foreach (var item in events)
        {
            if (!AllDayRow(item) || !CalendarStore.Overlaps(item, origin, origin.AddDays(days)))
            {
                continue;
            }
            // An all-day event's end is the midnight after its last day; a timed one's last day is
            // the one its end falls in, unless that is a midnight.
            var from = (int)Math.Floor((item.Start - origin).TotalDays);
            var to = (int)Math.Ceiling((item.End - origin).TotalDays);
            spans.Add((Math.Max(0, from), Math.Clamp(to, Math.Max(0, from) + 1, days), item));
        }
        var lanes = Lanes(spans.ConvertAll(span => (span.First, span.Last)));
        var bars = new List<Bar>(spans.Count);
        for (var i = 0; i < spans.Count; i++)
        {
            bars.Add(new Bar(spans[i].First, spans[i].Last, lanes.Lane[i], spans[i].Event));
        }
        return (bars, lanes.Count);
    }

    /// <summary>
    /// A lane for each span [First, Last): the first one free over all its days, spans taken by their
    /// first day and the longer first. Also how many lanes that takes.
    /// </summary>
    public static (int[] Lane, int Count) Lanes(IReadOnlyList<(int First, int Last)> spans)
    {
        var order = Enumerable.Range(0, spans.Count)
            .OrderBy(i => spans[i].First)
            .ThenByDescending(i => spans[i].Last - spans[i].First)
            .ThenBy(i => i)
            .ToArray();
        var lane = new int[spans.Count];
        var ends = new List<int>();
        foreach (var i in order)
        {
            var free = ends.FindIndex(end => end <= spans[i].First);
            if (free < 0)
            {
                free = ends.Count;
                ends.Add(0);
            }
            ends[free] = spans[i].Last;
            lane[i] = free;
        }
        return (lane, ends.Count);
    }

    /// <summary>
    /// Side by side where they overlap, as Outlook lays out a day: events that overlap one another
    /// (directly or through others) form a group, each goes to the first column of its group that is
    /// free at its start (by start, the longer first), and then reaches right over the columns that
    /// have nothing it overlaps. Each item is [Start, End) minutes, taken as at least
    /// <paramref name="minimum"/> long, as it is drawn.
    /// </summary>
    public static Placement[] Arrange(IReadOnlyList<(double Start, double End)> items, double minimum)
    {
        var count = items.Count;
        var result = new Placement[count];
        double Start(int i) => items[i].Start;
        double End(int i) => Math.Max(items[i].End, items[i].Start + minimum);
        var order = Enumerable.Range(0, count)
            .OrderBy(Start)
            .ThenByDescending(End)
            .ThenBy(i => i)
            .ToArray();
        var column = new int[count];
        var at = 0;
        while (at < count)
        {
            // The group: every event that starts before all before it have ended.
            var group = new List<int> { order[at] };
            var groupEnd = End(order[at]);
            var next = at + 1;
            while (next < count && Start(order[next]) < groupEnd)
            {
                group.Add(order[next]);
                groupEnd = Math.Max(groupEnd, End(order[next]));
                next++;
            }
            var ends = new List<double>();
            foreach (var i in group)
            {
                var free = ends.FindIndex(end => end <= Start(i));
                if (free < 0)
                {
                    free = ends.Count;
                    ends.Add(0);
                }
                ends[free] = End(i);
                column[i] = free;
            }
            foreach (var i in group)
            {
                var span = 1;
                for (var right = column[i] + 1; right < ends.Count; right++)
                {
                    var blocked = group.Exists(other => column[other] == right && Start(other) < End(i) && Start(i) < End(other));
                    if (blocked)
                    {
                        break;
                    }
                    span++;
                }
                result[i] = new Placement(column[i], ends.Count, span);
            }
            at = next;
        }
        return result;
    }
}
