// §616: what Karpo keeps of a CalDAV object (Karpo.Accounts\Dav\KeptICalendar.cs)
// draws the same rows as the object the server holds. Kept.Parse is what the
// recurrence and meeting tests parse every iCalendar of theirs with: it parses
// the raw text and the kept one and holds them to the same occurrences over
// several ranges and zones, so every rule part, zone, override and meeting link
// those tests try is tried stripped too. And what goes: notes, attendees, alarms,
// tasks.

using System.Globalization;
using Karpo.Accounts.Dav;
using Karpo.Dock;

namespace Karpo.Tests.Calendars;

/// <summary>The recurrence and meeting tests' parse: the raw object, held to draw what its kept form draws.</summary>
internal static class Kept
{
    private static readonly TimeZoneInfo[] Zones =
    [
        TimeZoneInfo.Utc,
        TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time"),
        TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"),
        TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time"),
    ];

    /// <summary>Years a recurrence or meeting test looks at: RFC 5545's examples, today's, a COUNT's end far ahead.</summary>
    private static readonly (DateTime From, DateTime To)[] Ranges =
    [
        (new DateTime(1996, 12, 1), new DateTime(2000, 3, 1)),
        (new DateTime(2023, 12, 1), new DateTime(2028, 1, 1)),
        (new DateTime(2026, 3, 1), new DateTime(2026, 3, 31)),
        (new DateTime(2026, 10, 25), new DateTime(2026, 11, 3)),
        (new DateTime(2079, 7, 1), new DateTime(2079, 8, 1)),
    ];

    /// <summary><paramref name="text"/> parsed, after checking its kept form shows the same occurrences in every range and zone.</summary>
    public static ICalendar Parse(string text)
    {
        var raw = ICalendar.Parse(text);
        Same(text, raw);
        return raw;
    }

    /// <summary>Asserts the kept form of <paramref name="text"/> shows what <paramref name="raw"/> (its parse) shows.</summary>
    public static void Same(string text, ICalendar raw)
    {
        var kept = KeptICalendar.Of(text);
        if (kept is null)
        {
            Assert.Empty(raw.Events);
            return;
        }
        var stripped = ICalendar.Parse(kept);
        Assert.Equal(raw.Dropped, stripped.Dropped);
        foreach (var zone in Zones)
        {
            foreach (var (from, to) in Ranges)
            {
                Assert.Equal(raw.Between(from, to, zone), stripped.Between(from, to, zone));
            }
        }
    }
}

public sealed class KeptICalendarTests
{
    private const string Raw = """
        BEGIN:VCALENDAR
        VERSION:2.0
        PRODID:-//Example Corp.//CalDAV Server//EN
        X-WR-CALNAME:Work
        BEGIN:VTIMEZONE
        TZID:Europe/Zurich
        X-LIC-LOCATION:Europe/Zurich
        BEGIN:DAYLIGHT
        TZOFFSETFROM:+0100
        TZOFFSETTO:+0200
        TZNAME:CEST
        DTSTART:19810329T020000
        RRULE:FREQ=YEARLY;BYMONTH=3;BYDAY=-1SU
        END:DAYLIGHT
        BEGIN:STANDARD
        TZOFFSETFROM:+0200
        TZOFFSETTO:+0100
        TZNAME:CET
        DTSTART:19961027T030000
        RRULE:FREQ=YEARLY;BYMONTH=10;BYDAY=-1SU
        END:STANDARD
        END:VTIMEZONE
        BEGIN:VEVENT
        UID:quarterly@example.com
        DTSTAMP:20260101T120000Z
        SUMMARY:Quarterly\, with everyone
        LOCATION:Room 4
        DTSTART;TZID=Europe/Zurich:20261102T090000
        DTEND;TZID=Europe/Zurich:20261102T100000
        RRULE:FREQ=WEEKLY;COUNT=6
        EXDATE;TZID=Europe/Zurich:20261109T090000
        DESCRIPTION:Secret agenda: the merger.\nJoin Zoom Meeting\nhttps://us02we
         b.zoom.us/j/8555000?pwd=abc\n\nPasscode: 123456
        ORGANIZER;CN=Anna Muster:mailto:anna@example.com
        ATTENDEE;CN=Beat Beispiel;PARTSTAT=ACCEPTED:mailto:beat@example.com
        ATTACH;FMTTYPE=application/pdf:https://files.example.com/merger.pdf
        COMMENT:Bring the numbers
        X-MOZ-GENERATION:3
        BEGIN:VALARM
        ACTION:DISPLAY
        DESCRIPTION:Reminder https://teams.microsoft.com/l/meetup-join/should-not-be-read
        TRIGGER:-PT15M
        END:VALARM
        END:VEVENT
        BEGIN:VEVENT
        UID:quarterly@example.com
        RECURRENCE-ID;TZID=Europe/Zurich:20261116T090000
        SUMMARY:Quarterly (moved)
        DTSTART;TZID=Europe/Zurich:20261117T140000
        DTEND;TZID=Europe/Zurich:20261117T150000
        DESCRIPTION:No link in here\, only words.
        END:VEVENT
        BEGIN:VTODO
        UID:todo@example.com
        SUMMARY:A task that is no event
        END:VTODO
        END:VCALENDAR
        """;

    /// <summary>
    /// <see cref="Raw"/> with each line ended by <paramref name="end"/>: a raw literal's line ends are its source
    /// file's, CRLF or LF as git checked it out, so they are set here, never taken from the file.
    /// </summary>
    private static string Object(string end) => Raw.ReplaceLineEndings(end) + end;

    /// <summary>
    /// The kept object has the rows' fields, the zone, the series and its override, the meeting's link alone in
    /// place of the notes; no notes, no organiser or attendees, no attachment, comment, alarm, X- property or task.
    /// </summary>
    [Fact]
    public void AnObjectKeepsWhatTheRowsDrawAndNothingElse()
    {
        var kept = KeptICalendar.Of(Object("\r\n"))!;

        foreach (var line in (string[])["UID:quarterly@example.com", @"SUMMARY:Quarterly\, with everyone", "LOCATION:Room 4", "TZID:Europe/Zurich",
            "RRULE:FREQ=YEARLY;BYMONTH=3;BYDAY=-1SU", "RRULE:FREQ=WEEKLY;COUNT=6", "EXDATE;TZID=Europe/Zurich:20261109T090000",
            "RECURRENCE-ID;TZID=Europe/Zurich:20261116T090000", "DESCRIPTION:https://us02web.zoom.us/j/8555000?pwd=abc"])
        {
            Assert.Contains(line + "\r\n", kept, StringComparison.Ordinal);
        }
        foreach (var gone in (string[])["merger", "Passcode", "ORGANIZER", "ATTENDEE", "anna@", "ATTACH", "COMMENT", "X-", "VALARM", "TRIGGER",
            "should-not-be-read", "VTODO", "A task", "No link", "DTSTAMP", "PRODID", "Work"])
        {
            Assert.DoesNotContain(gone, kept, StringComparison.Ordinal);
        }
        Assert.Equal(2, kept.Split("BEGIN:VEVENT").Length - 1);
    }

    /// <summary>The rows of the kept object are the raw one's: times, titles, places, the link, free or not.</summary>
    [Fact]
    public void TheKeptObjectDrawsWhatTheRawOneDraws()
    {
        var text = Object("\r\n");
        var raw = ICalendar.Parse(text);

        Kept.Same(text, raw);
        var events = ICalendar.Parse(KeptICalendar.Of(text)!).Between(new DateTime(2026, 11, 1), new DateTime(2026, 12, 31), TimeZoneInfo.Utc);
        Assert.Equal(
            ["2026-11-02 08:00 Quarterly, with everyone", "2026-11-17 13:00 Quarterly (moved)", "2026-11-23 08:00 Quarterly, with everyone",
                "2026-11-30 08:00 Quarterly, with everyone", "2026-12-07 08:00 Quarterly, with everyone"],
            events.OrderBy(one => one.Start).Select(one => one.Start.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " " + one.Title));
        Assert.Equal("https://us02web.zoom.us/j/8555000?pwd=abc", events[0].Join);
    }

    /// <summary>
    /// A server ends its lines with CRLF (RFC 5545 §3.1), a file or a test may with LF: either way the folded notes
    /// are one line and their meeting's link is kept, and the kept object draws what the raw one draws.
    /// </summary>
    [Theory]
    [InlineData("\r\n")]
    [InlineData("\n")]
    public void TheLinkIsKeptWhateverTheLinesEndWith(string end)
    {
        var text = Object(end);
        Assert.Equal(end == "\n" ? 0 : text.Split('\n').Length - 1, text.Count(c => c == '\r'));

        var kept = KeptICalendar.Of(text)!;

        Assert.Contains("DESCRIPTION:https://us02web.zoom.us/j/8555000?pwd=abc\r\n", kept, StringComparison.Ordinal);
        Kept.Same(text, ICalendar.Parse(text));
        Assert.Equal("https://us02web.zoom.us/j/8555000?pwd=abc",
            ICalendar.Parse(text).Between(new DateTime(2026, 11, 1), new DateTime(2026, 11, 3), TimeZoneInfo.Utc)[0].Join);
    }

    /// <summary>An object of a task or a journal entry only is none: the dock keeps its etag, never its text.</summary>
    [Fact]
    public void AnObjectWithNoEventIsNone()
    {
        Assert.Null(KeptICalendar.Of("BEGIN:VCALENDAR\r\nVERSION:2.0\r\nBEGIN:VTODO\r\nUID:t\r\nSUMMARY:Buy milk\r\nEND:VTODO\r\nEND:VCALENDAR\r\n"));
        Assert.Null(KeptICalendar.Of(""));
        Assert.NotNull(KeptICalendar.Of("BEGIN:VCALENDAR\nBEGIN:VEVENT\nUID:e\nDTSTART:20261101T090000Z\nEND:VEVENT\nEND:VCALENDAR\n"));
    }

    /// <summary>
    /// Notes with a link among words keep the link only, escaped as a TEXT value; the last DESCRIPTION is the one
    /// the dock reads, so it is the one kept; a link in an alarm's description is no meeting.
    /// </summary>
    [Fact]
    public void TheNotesAreTheirLinkAlone()
    {
        static string Event(string lines) => "BEGIN:VCALENDAR\r\nBEGIN:VEVENT\r\nUID:a\r\nDTSTART:20261016T080000Z\r\nDTEND:20261016T083000Z\r\nSUMMARY:Sync\r\n"
            + lines + "END:VEVENT\r\nEND:VCALENDAR\r\n";

        var last = Event("DESCRIPTION:https://zoom.us/j/111\r\nDESCRIPTION:Nothing to join\r\n");
        Assert.DoesNotContain("zoom", KeptICalendar.Of(last)!, StringComparison.Ordinal);
        Kept.Same(last, ICalendar.Parse(last));

        var teams = Event("DESCRIPTION:Join: https://teams.microsoft.com/l/meetup-join/19%3ameeting_abc%40thread.v2/0?context=%7b%22Tid%22%3a%221%22%7d\\, then wait\r\n");
        Kept.Same(teams, ICalendar.Parse(teams));
        Assert.NotNull(ICalendar.Parse(KeptICalendar.Of(teams)!).Between(new DateTime(2026, 10, 16), new DateTime(2026, 10, 17), TimeZoneInfo.Utc)[0].Join);
    }
}
