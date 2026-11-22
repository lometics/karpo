// The next meeting (docs\widgets.md §205): which of the calendars' events are meetings still to come,
// which of them the bar shows, and how soon it starts in words that fit the room.
//
// A meeting is an event with a time of its own — an all-day event is a day, not a meeting — that
// takes the person's time (not shown as free, not declined: CalendarEvent.Free) and has not ended.
// The one on now comes first, the most recently started of several; then the rest by their start.
// One exception, for the hour two meetings touch: a meeting that has been on for ten minutes gives
// way to one that starts within five, as the person is about to leave it for the next.
//
// The events are wall-clock times in the zone they were asked for (this PC's, as Windows' region
// says), and so is now; every length is measured between the instants they are, so "in 40 min"
// across the night the clocks go forward is forty minutes and not a hundred.
//
// Pure: the face (MeetingFaceDrawing), the line (MeetingTile) and the tests ask the same questions.

using Karpo.Core;
using Karpo.Dock.Panels;

namespace Karpo.Dock;

/// <summary>One meeting still to come, with the instants it starts and ends at.</summary>
internal readonly record struct Meeting(CalendarEvent Event, DateTime StartUtc, DateTime EndUtc)
{
    /// <summary>Whether it is on at <paramref name="utc"/>: started and not ended.</summary>
    public bool OnAt(DateTime utc) => StartUtc <= utc && utc < EndUtc;

    /// <summary>Its online meeting's link, where it has one Karpo knows.</summary>
    public Karpo.Accounts.MeetingLink? Link => Karpo.Accounts.MeetingLinks.Of(Event.Join);
}

internal static class Meetings
{
    /// <summary>How long a meeting may have been on before one about to start is shown in its place.</summary>
    internal static readonly TimeSpan Settled = TimeSpan.FromMinutes(10);

    /// <summary>How soon a meeting must start to be shown in the place of one that has settled.</summary>
    internal static readonly TimeSpan Imminent = TimeSpan.FromMinutes(5);

    /// <summary>How far ahead the face that shows the next meeting whatever the day looks.</summary>
    internal const int DaysAhead = 7;

    /// <summary>
    /// The meetings of <paramref name="events"/> (wall-clock times in <paramref name="zone"/>) not over at
    /// <paramref name="nowUtc"/>: timed, taking the person's time, each once; the one on now first, then by
    /// start, but one about to start before one long on (see the top of the file).
    /// </summary>
    public static List<Meeting> Ahead(IEnumerable<CalendarEvent> events, DateTime nowUtc, TimeZoneInfo zone)
    {
        var on = new List<Meeting>();
        var coming = new List<Meeting>();
        foreach (var item in events)
        {
            if (item.AllDay || item.Free)
            {
                continue;
            }
            var start = Stamp.WallToUtc(item.Start, zone);
            var end = item.End > item.Start ? Stamp.WallToUtc(item.End, zone) : start;
            var meeting = new Meeting(item, start, end);
            if (meeting.OnAt(nowUtc))
            {
                on.Add(meeting);
            }
            else if (start >= nowUtc)
            {
                coming.Add(meeting);
            }
        }
        // The most recently started first: two on at once, the newer is the one the person is in.
        on.Sort(static (a, b) => b.StartUtc != a.StartUtc ? b.StartUtc.CompareTo(a.StartUtc) : a.EndUtc.CompareTo(b.EndUtc));
        coming.Sort(static (a, b) => a.StartUtc != b.StartUtc ? a.StartUtc.CompareTo(b.StartUtc) : string.CompareOrdinal(a.Event.Title, b.Event.Title));
        if (on.Count > 0 && coming.Count > 0
            && nowUtc - on[0].StartUtc >= Settled
            && coming[0].StartUtc - nowUtc <= Imminent)
        {
            var next = coming[0];
            coming.RemoveAt(0);
            on.Insert(0, next);
        }
        return [.. on, .. coming];
    }

    /// <summary>
    /// The meetings of <paramref name="store"/> not over at <paramref name="now"/> (a wall-clock time in
    /// <paramref name="zone"/>, this PC's when null), through the next <paramref name="days"/> days.
    /// </summary>
    public static List<Meeting> Ahead(CalendarStore store, DateTime now, int days, TimeZoneInfo? zone = null, SourceFilter? shown = null)
    {
        var frame = zone ?? TimeZoneInfo.Local;
        if (!store.HasCalendars)
        {
            return [];
        }
        var from = now.Date;
        return Ahead(store.EventsIn(from, from.AddDays(Math.Max(1, days)), frame, shown), Stamp.WallToUtc(now, frame), frame);
    }

    /// <summary>
    /// Of <paramref name="ahead"/>, those of today: on now, or starting before the midnight after
    /// <paramref name="now"/> (a wall-clock time in <paramref name="zone"/>).
    /// </summary>
    public static List<Meeting> Today(IReadOnlyList<Meeting> ahead, DateTime now, TimeZoneInfo zone)
    {
        var midnight = Stamp.WallToUtc(now.Date.AddDays(1), zone);
        var nowUtc = Stamp.WallToUtc(now, zone);
        return [.. ahead.Where(meeting => meeting.OnAt(nowUtc) || meeting.StartUtc < midnight)];
    }

    /// <summary>
    /// The meeting a face set to <paramref name="show"/> (<see cref="MeetingFace"/>) shows at
    /// <paramref name="now"/>: the first of today's; the first of the coming week; or the first only
    /// from <paramref name="lead"/> before it starts. Null where there is none to show.
    /// </summary>
    public static Meeting? Shown(IReadOnlyList<Meeting> ahead, string show, TimeSpan lead, DateTime now, TimeZoneInfo zone)
    {
        if (ahead.Count == 0)
        {
            return null;
        }
        var nowUtc = Stamp.WallToUtc(now, zone);
        return show switch
        {
            MeetingFace.Next => ahead[0],
            MeetingFace.Soon => ahead[0] is var first && (first.OnAt(nowUtc) || first.StartUtc - nowUtc <= lead) ? first : null,
            _ => Today(ahead, now, zone) is [var today, ..] ? today : null,
        };
    }

    /// <summary>Whether <paramref name="meeting"/> is lit at <paramref name="nowUtc"/>: on now, or starting within <paramref name="lead"/>.</summary>
    public static bool Lit(in Meeting meeting, DateTime nowUtc, TimeSpan lead) =>
        meeting.OnAt(nowUtc) || (meeting.StartUtc >= nowUtc && meeting.StartUtc - nowUtc <= lead);
}

/// <summary>How the next meeting is put into words.</summary>
internal static class MeetingText
{
    /// <summary>From how far ahead a meeting is said by its time ("at 15:00") rather than how soon ("in 3 h 10 min").</summary>
    internal static readonly TimeSpan Far = TimeSpan.FromHours(3);

    /// <summary>Between the title and when, as the bar's clock sets its parts apart.</summary>
    internal const string Dot = " · ";

    /// <summary>
    /// When <paramref name="meeting"/> is, from <paramref name="now"/> (a wall-clock time in
    /// <paramref name="zone"/>): "now" in its first minute, "started 3 min ago" after; "in 4 min",
    /// "in 1 h 5 min" on its day; "at 15:00" from <see cref="Far"/> ahead; "Tomorrow, 09:00" on another day.
    /// </summary>
    public static string When(in Meeting meeting, DateTime now, TimeZoneInfo zone, Func<DateTime, string>? time = null, Func<DateOnly, string>? longDate = null)
    {
        time ??= Nls.Time;
        var nowUtc = Stamp.WallToUtc(now, zone);
        if (meeting.OnAt(nowUtc))
        {
            var since = nowUtc - meeting.StartUtc;
            return since < TimeSpan.FromMinutes(1) ? S.DockMeetingNow : S.DockMeetingStarted(Length(since, up: false));
        }
        var day = DateOnly.FromDateTime(meeting.Event.Start);
        var today = DateOnly.FromDateTime(now);
        if (day != today)
        {
            return S.DockMeetingOn(CalendarText.DayName(day, today, longDate), time(meeting.Event.Start));
        }
        var until = meeting.StartUtc - nowUtc;
        if (until <= TimeSpan.Zero)
        {
            return S.DockMeetingNow;
        }
        return until >= Far ? S.DockMeetingAt(time(meeting.Event.Start)) : S.DockMeetingIn(Length(until, up: true));
    }

    /// <summary>
    /// How soon, where that says more than the meeting's span beside it: on now or within
    /// <see cref="Far"/>, as <see cref="When"/> says it; empty for a meeting later today, whose time
    /// the span already says, and for one of another day.
    /// </summary>
    public static string Soon(in Meeting meeting, DateTime now, TimeZoneInfo zone, Func<DateTime, string>? time = null)
    {
        var nowUtc = Stamp.WallToUtc(now, zone);
        if (!meeting.OnAt(nowUtc) && (meeting.StartUtc - nowUtc >= Far || DateOnly.FromDateTime(meeting.Event.Start) != DateOnly.FromDateTime(now)))
        {
            return "";
        }
        return When(meeting, now, zone, time);
    }

    /// <summary>
    /// A length in whole minutes as the region's Windows says one: "4 min", "1 h", "1 h 5 min". Up
    /// rounds a part minute up — 3 min 10 s to go is "in 4 min", so "in 1 min" is the last before
    /// "now" — and down rounds it down, as a meeting started 3 min 50 s ago started "3 min ago".
    /// </summary>
    public static string Length(TimeSpan span, bool up)
    {
        var minutes = (int)(up ? Math.Ceiling(span.TotalMinutes - 1e-9) : Math.Floor(span.TotalMinutes));
        minutes = Math.Max(up ? 1 : 0, minutes);
        if (minutes < 60)
        {
            return S.WidgetTileMinutes(minutes);
        }
        var (hours, rest) = Math.DivRem(minutes, 60);
        return rest == 0 ? S.WidgetCommonHours(hours) : S.WidgetTileHoursMinutes(hours, rest);
    }

    /// <summary>
    /// The span of <paramref name="meeting"/> and where it is, as the line says it under its title:
    /// "10:00–10:30 · Room 3", the service where it is online and has no place ("10:00–10:30 · Microsoft Teams").
    /// </summary>
    public static string Detail(in Meeting meeting, DateTime now, Func<DateTime, string>? time = null)
    {
        time ??= Nls.Time;
        // On its own day: a meeting of tomorrow's is its span then, not "From 09:00" seen from today.
        _ = now;
        var span = CalendarText.Span(meeting.Event, DateOnly.FromDateTime(meeting.Event.Start), time);
        var place = meeting.Event.Location is { Length: > 0 } location && Karpo.Accounts.MeetingLinks.In(location) is null
            ? location
            : meeting.Link is { } link ? Karpo.Accounts.MeetingLinks.Name(link.Service) : null;
        return place is null ? span : span + Dot + place;
    }

    /// <summary>
    /// What fits in <paramref name="room"/> px of the title and when, the title and the dot after it
    /// measured by <paramref name="measure"/> and when by <paramref name="measureWhen"/> (its own weight;
    /// the same where null): both whole; the title cut with "…" before when, while two characters of it
    /// and the ellipsis still stand; else when alone, which the text line cuts where even it is too long.
    /// The title is empty where it is left out.
    /// </summary>
    public static (string Title, string When) Fit(string title, string when, float room, Func<string, float> measure, Func<string, float>? measureWhen = null)
    {
        title = title.Trim();
        var whenWidth = (measureWhen ?? measure)(when);
        bool Fits(string shown) => measure(shown + Dot) + whenWidth <= room;
        if (title.Length > 0 && Fits(title))
        {
            return (title, when);
        }
        // The longest cut of the title that leaves when whole; two characters at the least.
        var (least, most) = (2, title.Length - 1);
        string? best = null;
        while (least <= most)
        {
            var take = (least + most) / 2;
            var cut = Cut(title, take);
            if (Fits(cut))
            {
                best = cut;
                least = take + 1;
            }
            else
            {
                most = take - 1;
            }
        }
        return best is not null ? (best, when) : ("", when);
    }

    /// <summary>The first <paramref name="take"/> characters of <paramref name="title"/>, its spaces at the end trimmed, and "…".</summary>
    private static string Cut(string title, int take)
    {
        // Never half of a surrogate pair: an emoji in a title is cut before it or kept whole.
        if (take > 0 && take < title.Length && char.IsHighSurrogate(title[take - 1]))
        {
            take--;
        }
        return title[..take].TrimEnd() + "…";
    }
}
