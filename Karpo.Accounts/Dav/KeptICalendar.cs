// What Karpo keeps of a CalDAV object (docs\widgets.md §616): the iCalendar a
// server holds, cut to what a calendar's rows need, before it is held in memory
// or written to the mirror on disk. The rows draw a title, a place, times, the
// series they make, and a meeting's link; the dock's expansion (Karpo.Dock's
// ICalendar) reads only those. Kept:
//
// - the components VCALENDAR, VEVENT, VTIMEZONE and its STANDARD and DAYLIGHT;
// - of a VEVENT: UID, SUMMARY, LOCATION, STATUS, DTSTART, DTEND, DURATION,
//   RRULE, RDATE, EXDATE, RECURRENCE-ID, TRANSP, CONFERENCE,
//   X-GOOGLE-CONFERENCE and URL, each line as the server wrote it, unfolded;
//   its DESCRIPTION only as the meeting link MeetingLinks finds in it, and not
//   at all where it has none;
// - of a zone: its TZID, and each part's DTSTART, offsets, RRULE, RDATE, name.
//
// Gone: notes, attendees, the organiser, alarms, attachments, comments, any X-
// property of the server's, a task or a journal entry. An object with no VEVENT
// is none (null): the dock keeps its etag alone, so it is not fetched again.
//
// Version: what is kept changes only with it. The mirror's files carry the
// version they were written with; one of another is downloaded again whole
// (Karpo.Dock\Pim\CalendarMirror.cs), so a field added here reaches every
// event at the next sync with no migration.
//
// The rows of a stripped object are the rows of the raw one: KeptICalendarTests
// expands both over several ranges and zones for every iCalendar of the
// recurrence and meeting tests.

using System.Text;

namespace Karpo.Accounts.Dav;

public static class KeptICalendar
{
    /// <summary>What is kept, as a number: a mirror written under another is downloaded again whole.</summary>
    public const int Version = 1;

    private static readonly HashSet<string> EventKept = new(StringComparer.Ordinal)
    {
        "UID", "SUMMARY", "LOCATION", "STATUS", "DTSTART", "DTEND", "DURATION", "RRULE", "RDATE", "EXDATE",
        "RECURRENCE-ID", "TRANSP", "CONFERENCE", "X-GOOGLE-CONFERENCE", "URL",
    };

    private static readonly HashSet<string> ZoneKept = new(StringComparer.Ordinal)
    {
        "TZID", "DTSTART", "TZOFFSETFROM", "TZOFFSETTO", "RRULE", "RDATE", "TZNAME",
    };

    /// <summary><paramref name="text"/> as Karpo keeps it; null where it holds no event.</summary>
    public static string? Of(string text)
    {
        var kept = new StringBuilder(Math.Min(text.Length, 1024));
        // Each open component: its name, whether it is kept, and for a VEVENT its notes, written as its link at its end.
        var open = new List<(string Name, bool Kept)>();
        string? notes = null;
        var events = 0;
        foreach (var (line, name, value) in Lines(text))
        {
            var top = open.Count > 0 ? open[^1] : default;
            var inKept = open.Count == 0 || top.Kept;
            switch (name)
            {
                case "BEGIN":
                {
                    var component = value.Trim().ToUpperInvariant();
                    var keep = inKept && Takes(open.Count > 0 ? top.Name : null, component);
                    open.Add((component, keep));
                    if (keep)
                    {
                        kept.Append("BEGIN:").Append(component).Append("\r\n");
                        if (component == "VEVENT")
                        {
                            notes = null;
                            events++;
                        }
                    }
                    continue;
                }
                case "END":
                {
                    if (open.Count == 0)
                    {
                        continue;
                    }
                    var (component, keep) = open[^1];
                    open.RemoveAt(open.Count - 1);
                    if (keep)
                    {
                        // The notes as their meeting's link, where they hold one; the last DESCRIPTION is the one read.
                        if (component == "VEVENT" && MeetingLinks.In(Unescape(notes)) is { } link)
                        {
                            kept.Append("DESCRIPTION:").Append(Escape(link.Url)).Append("\r\n");
                        }
                        kept.Append("END:").Append(component).Append("\r\n");
                    }
                    continue;
                }
            }
            if (open.Count == 0 || !top.Kept)
            {
                continue;
            }
            switch (top.Name)
            {
                case "VEVENT" when name == "DESCRIPTION":
                    notes = value;
                    break;
                case "VEVENT" when EventKept.Contains(name):
                case "VTIMEZONE" or "STANDARD" or "DAYLIGHT" when ZoneKept.Contains(name):
                case "VCALENDAR" when name == "VERSION":
                    kept.Append(line).Append("\r\n");
                    break;
            }
        }
        return events > 0 ? kept.ToString() : null;
    }

    /// <summary>Whether a component named <paramref name="child"/> inside <paramref name="parent"/> (null: at the top) is kept.</summary>
    private static bool Takes(string? parent, string child) => (parent, child) switch
    {
        (null, "VCALENDAR" or "VEVENT" or "VTIMEZONE") => true,
        ("VCALENDAR", "VEVENT" or "VTIMEZONE") => true,
        ("VTIMEZONE", "STANDARD" or "DAYLIGHT") => true,
        _ => false,
    };

    /// <summary>The content lines unfolded, each whole, with its name (upper case) and value; a line with no name is none.</summary>
    private static IEnumerable<(string Line, string Name, string Value)> Lines(string text)
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

    /// <summary>A line's name, up to its first ';' or its unquoted ':', and its value; null where it has neither.</summary>
    private static (string, string, string)? Split(string line)
    {
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == '"')
            {
                quoted = !quoted;
            }
            else if (line[i] == ':' && !quoted)
            {
                if (i == 0)
                {
                    return null;
                }
                var head = line[..i];
                var semicolon = head.IndexOf(';', StringComparison.Ordinal);
                return (line, (semicolon < 0 ? head : head[..semicolon]).Trim().ToUpperInvariant(), line[(i + 1)..]);
            }
        }
        return null;
    }

    /// <summary>A TEXT value as written in the notes, read as the dock reads them: an escaped line break a space.</summary>
    private static string? Unescape(string? value)
    {
        if (value is null || !value.Contains('\\', StringComparison.Ordinal))
        {
            return value?.Trim();
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

    /// <summary>A link as a TEXT value (RFC 5545 §3.3.11).</summary>
    private static string Escape(string text) =>
        text.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace(";", "\\;", StringComparison.Ordinal)
            .Replace(",", "\\,", StringComparison.Ordinal);
}
