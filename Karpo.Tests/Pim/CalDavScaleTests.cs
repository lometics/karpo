// §616: a CalDAV calendar kept whole, at the size a person's real one reaches —
// ten thousand objects, fifteen years of meetings, birthdays and series. Held
// here, with loose budgets and the figures printed: parsing them all, what they
// hold in memory, a month and a year drawn from the index, the mirror's file
// written and read, and a first sync against the fake server.

using System.Diagnostics;
using System.Globalization;
using System.Text;
using Karpo.Accounts;
using Karpo.Accounts.Dav;
using Karpo.Dock;
using Karpo.Dock.Pim;
using Karpo.Tests.Accounts;
using Xunit.Abstractions;

namespace Karpo.Tests.Pim;

/// <summary>A calendar of made-up but ordinary objects, the same every run.</summary>
internal static class GeneratedCalendar
{
    private const string Zurich = """
        BEGIN:VTIMEZONE
        TZID:Europe/Zurich
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
        """;

    /// <summary>
    /// <paramref name="count"/> objects as a server holds them, raw: most a meeting of an hour in Zurich's time with notes,
    /// attendees and an alarm, between 2012 and 2035; some whole days; some weekly series with an UNTIL, some with a
    /// COUNT; some yearly birthdays without end, a few with an override. By href, in order.
    /// </summary>
    public static List<(string Name, string Text)> Objects(int count)
    {
        var random = new Random(616);
        var objects = new List<(string, string)>(count);
        var first = new DateTime(2012, 1, 2, 7, 0, 0);
        for (var i = 0; i < count; i++)
        {
            var at = first.AddDays(random.Next(0, 24 * 365)).AddMinutes(30 * random.Next(0, 20));
            var kind = i % 100;
            var lines = new StringBuilder("BEGIN:VCALENDAR\r\nVERSION:2.0\r\nPRODID:-//Example Corp.//CalDAV Server//EN\r\n");
            lines.Append(Zurich.ReplaceLineEndings("\r\n")).Append("\r\n");
            lines.Append("BEGIN:VEVENT\r\n").Append(CultureInfo.InvariantCulture, $"UID:{i:D6}-{random.Next():x8}@example.com\r\n");
            lines.Append("DTSTAMP:20260101T120000Z\r\nCREATED:20250101T120000Z\r\nLAST-MODIFIED:20260101T120000Z\r\nSEQUENCE:2\r\n");
            lines.Append(CultureInfo.InvariantCulture, $"SUMMARY:Meeting number {i} about the quarterly plan\r\n");
            lines.Append(CultureInfo.InvariantCulture, $"LOCATION:Room {random.Next(1, 40)}\\, Building {random.Next(1, 5)}\r\n");
            lines.Append("DESCRIPTION:Agenda:\\n1. Review of last quarter\\n2. The plan\\n3. Anything else\\n\\nJoin: https://zoom.us/j/9")
                .Append(random.Next(100000000, 999999999).ToString(CultureInfo.InvariantCulture)).Append("\\nPlease be on time.\r\n");
            lines.Append("ORGANIZER;CN=Anna Muster:mailto:anna@example.com\r\n");
            lines.Append("ATTENDEE;CN=Beat Beispiel;PARTSTAT=ACCEPTED;ROLE=REQ-PARTICIPANT:mailto:beat@example.com\r\n");
            lines.Append("ATTENDEE;CN=Carla Exempel;PARTSTAT=NEEDS-ACTION;ROLE=OPT-PARTICIPANT:mailto:carla@example.com\r\n");
            if (kind < 5)
            {
                lines.Append(CultureInfo.InvariantCulture, $"DTSTART;VALUE=DATE:{at:yyyyMMdd}\r\nDTEND;VALUE=DATE:{at.AddDays(1):yyyyMMdd}\r\n");
            }
            else if (kind < 7)
            {
                // A birthday, every year from then on.
                lines.Append(CultureInfo.InvariantCulture, $"DTSTART;VALUE=DATE:{at.AddYears(-30):yyyyMMdd}\r\nDTEND;VALUE=DATE:{at.AddYears(-30).AddDays(1):yyyyMMdd}\r\nRRULE:FREQ=YEARLY\r\n");
            }
            else
            {
                lines.Append(CultureInfo.InvariantCulture, $"DTSTART;TZID=Europe/Zurich:{at:yyyyMMdd'T'HHmmss}\r\nDTEND;TZID=Europe/Zurich:{at.AddHours(1):yyyyMMdd'T'HHmmss}\r\n");
                if (kind < 12)
                {
                    lines.Append(CultureInfo.InvariantCulture, $"RRULE:FREQ=WEEKLY;UNTIL={at.AddMonths(6):yyyyMMdd'T'HHmmss'Z'}\r\nEXDATE;TZID=Europe/Zurich:{at.AddDays(14):yyyyMMdd'T'HHmmss}\r\n");
                }
                else if (kind < 14)
                {
                    lines.Append("RRULE:FREQ=WEEKLY;BYDAY=MO,WE;COUNT=20\r\n");
                }
            }
            lines.Append("TRANSP:OPAQUE\r\nSTATUS:CONFIRMED\r\n");
            lines.Append("BEGIN:VALARM\r\nACTION:DISPLAY\r\nDESCRIPTION:Reminder\r\nTRIGGER:-PT15M\r\nEND:VALARM\r\n");
            lines.Append("END:VEVENT\r\nEND:VCALENDAR\r\n");
            objects.Add(($"{i:D6}.ics", lines.ToString()));
        }
        return objects;
    }
}

[Collection(Alone.Name)]
public sealed class CalDavScaleTests(ITestOutputHelper output)
{
    private const int Size = 10_000;

    private static readonly TimeZoneInfo Zurich = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");

    /// <summary>
    /// The index expands only the objects whose span meets a range, and misses none: what it shows of a month or a
    /// year is what every object expanded shows, in Zurich, Tokyo and Hawaii, from 2010 to 2036 — whole days, series
    /// with an UNTIL or a COUNT, birthdays without end, overrides, an RDATE before its DTSTART.
    /// </summary>
    [Fact]
    public void TheIndexShowsWhatEveryObjectShows()
    {
        var texts = GeneratedCalendar.Objects(600).Select(one => one.Text).Concat(
        [
            "BEGIN:VCALENDAR\r\nBEGIN:VEVENT\r\nUID:o\r\nSUMMARY:Series\r\nDTSTART:20200106T090000Z\r\nDURATION:PT1H\r\nRRULE:FREQ=WEEKLY;COUNT=30\r\nEND:VEVENT\r\n"
                + "BEGIN:VEVENT\r\nUID:o\r\nRECURRENCE-ID:20200113T090000Z\r\nSUMMARY:Moved far\r\nDTSTART:20310101T090000Z\r\nDURATION:PT1H\r\nEND:VEVENT\r\nEND:VCALENDAR\r\n",
            "BEGIN:VCALENDAR\r\nBEGIN:VEVENT\r\nUID:r\r\nSUMMARY:Added\r\nDTSTART:20300301T080000Z\r\nDURATION:PT1H\r\nRDATE:20110101T080000Z,20330505T080000Z\r\nEND:VEVENT\r\nEND:VCALENDAR\r\n",
            "BEGIN:VCALENDAR\r\nBEGIN:VEVENT\r\nUID:f\r\nSUMMARY:Floating\r\nDTSTART:20261231T233000\r\nDTEND:20270101T003000\r\nEND:VEVENT\r\nEND:VCALENDAR\r\n",
            "BEGIN:VCALENDAR\r\nBEGIN:VEVENT\r\nUID:d\r\nSUMMARY:Daily, hours\r\nDTSTART:20250101T060000Z\r\nRRULE:FREQ=DAILY;BYHOUR=6,18;COUNT=900\r\nEND:VEVENT\r\nEND:VCALENDAR\r\n",
        ]).ToList();
        var parsed = texts.Select(ICalendar.Parse).ToList();
        var index = EventIndex.Of(parsed.Select((calendar, i) => (i.ToString(CultureInfo.InvariantCulture), calendar)));

        foreach (var zone in new[] { Zurich, TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time"), TimeZoneInfo.FindSystemTimeZoneById("Hawaiian Standard Time") })
        {
            for (var month = new DateTime(2010, 1, 1); month < new DateTime(2036, 1, 1); month = month.AddMonths(month.Year % 3 == 0 ? 1 : 5))
            {
                var to = month.AddMonths(month.Month == 1 ? 12 : 1);
                var all = parsed.SelectMany(calendar => calendar.Between(month, to, zone)).OrderBy(one => one.Start).ThenBy(one => one.Title).ThenBy(one => one.End).ToList();
                Assert.Equal(all, index.Between(month, to, zone).OrderBy(one => one.Start).ThenBy(one => one.Title).ThenBy(one => one.End).ToList());
            }
        }
        Assert.Equal(parsed.Sum(calendar => calendar.Between(new DateTime(2026, 1, 1), new DateTime(2027, 1, 1), TimeZoneInfo.Utc).Count),
            index.CountIn(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
    }

    /// <summary>
    /// A calendar of <see cref="Size"/> objects as the store holds it after a sync (CalendarMirror.Apply), and how many MB it
    /// holds: the server's answer made first, so only what is kept is counted.
    /// </summary>
    private static (MirroredCalendar Held, double Megabytes) Held()
    {
        var objects = GeneratedCalendar.Objects(Size)
            .Select(one => new CalendarObject($"https://dav.example.com/cal/work/{one.Name}", $"\"{one.Name.GetHashCode(StringComparison.Ordinal):x8}-0001\"", KeptICalendar.Of(one.Text)))
            .ToList();
        var changes = new CalendarChanges(new RemoteCalendar("w", "Work", null)) { Changed = objects };
        var before = GC.GetTotalMemory(forceFullCollection: true);
        var held = CalendarMirror.Apply(null, changes, 0, DateTime.UtcNow, final: true);
        var after = GC.GetTotalMemory(forceFullCollection: true);
        GC.KeepAlive(changes);
        return (held, (after - before) / 1024.0 / 1024.0);
    }

    /// <summary>
    /// Ten thousand objects: a first sync against the fake server in under 5 s; parsed into the index in under a second;
    /// held, with their kept text deflated and their etags, in under 15 MB; a month drawn in under 2 ms on average, a
    /// year in under 10 ms; the mirror's file written and read back in under half a second each. Alone, so no other
    /// test's work is counted.
    /// </summary>
    [Fact]
    public async Task TenThousandObjectsSyncHoldLittleAndDrawFast()
    {
        var server = new FakeCalDav().With("work");
        foreach (var (name, text) in GeneratedCalendar.Objects(Size))
        {
            server.Put("work", name, text);
        }
        var account = new Account { Id = "big", Kind = AccountKind.Dav, Address = "me@example.com", CalendarHome = FakeCalDav.Host + FakeCalDav.HomePath };
        var reader = new DavReader(account, () => new DavClient(new HttpClient(server), "me", "secret"));

        // The first sync, as the store makes it: the server's answers, each step put in and parsed.
        var clock = Stopwatch.StartNew();
        MirroredCalendar? calendar = null;
        var answer = await reader.SyncCalendarsAsync(new Dictionary<string, CalendarState>(), step =>
        {
            calendar = CalendarMirror.Apply(calendar, step, 0, DateTime.UtcNow, final: false);
            return Task.CompletedTask;
        }, CancellationToken.None);
        calendar = CalendarMirror.Apply(calendar, Assert.Single(answer), 0, DateTime.UtcNow, final: true);
        var sync = clock.Elapsed;
        Assert.Equal(Size, calendar.Etags.Count);
        Assert.True(calendar.Header.Complete);

        // Parsing alone, and what is held once the sync's garbage is gone.
        var kept = GeneratedCalendar.Objects(Size).Select(one => (one.Name, Text: KeptICalendar.Of(one.Text)!)).ToList();
        clock.Restart();
        var parsed = EventIndex.Of(kept.Select(one => (one.Name, ICalendar.Parse(one.Text))));
        var parse = clock.Elapsed;
        GC.KeepAlive(parsed);
        parsed = null;
        kept = null;
        answer = null;
        var (held, megabytes) = Held();
        var index = held.Index;

        // A month and a year at a time, over the years the calendar spans, as the views step through them.
        clock.Restart();
        var months = 0;
        var shown = 0;
        for (var month = new DateTime(2014, 1, 1); month < new DateTime(2034, 1, 1); month = month.AddMonths(1), months++)
        {
            shown += index.Between(month, month.AddMonths(1), Zurich).Count;
        }
        var perMonth = clock.Elapsed.TotalMilliseconds / months;
        clock.Restart();
        var years = 0;
        for (var year = 2014; year < 2034; year++, years++)
        {
            shown += index.Between(new DateTime(year, 1, 1), new DateTime(year + 1, 1, 1), Zurich).Count;
        }
        var perYear = clock.Elapsed.TotalMilliseconds / years;

        // Its file, written and read back.
        var mirror = new CalendarMirror(Path.Combine(Isolation.Folder, "scale-" + Guid.NewGuid().ToString("N")));
        clock.Restart();
        Assert.True(await mirror.WriteAsync("big", calendar, CancellationToken.None));
        var write = clock.Elapsed;
        clock.Restart();
        var read = Assert.Single(await mirror.ReadAsync("big", CancellationToken.None));
        var reread = clock.Elapsed;
        var size = new FileInfo(Assert.Single(Directory.GetFiles(Path.Combine(mirror.Folder!, "big")))).Length / 1024.0 / 1024.0;

        output.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"{Size} objects: first sync {sync.TotalMilliseconds:F0} ms ({server.Count("multiget")} multigets), parsed {parse.TotalMilliseconds:F0} ms, held {megabytes:F1} MB (deflated text {held.Packed.Length / 1024.0 / 1024.0:F2} MB), a month {perMonth:F2} ms, a year {perYear:F2} ms, file {size:F2} MB written {write.TotalMilliseconds:F0} ms read {reread.TotalMilliseconds:F0} ms, {shown} shown"));
        GC.KeepAlive(held);
        Assert.Equal(Size, read.Index.Count);
        Assert.Equal(Size, index.Count);
        Assert.True(shown > Size);
        Assert.True(sync < TimeSpan.FromSeconds(5), $"first sync in {sync.TotalMilliseconds:F0} ms");
        Assert.True(parse < TimeSpan.FromSeconds(1), $"parsed in {parse.TotalMilliseconds:F0} ms");
        Assert.True(megabytes < 15, $"held {megabytes:F1} MB");
        Assert.True(perMonth < 2, $"a month in {perMonth:F2} ms");
        Assert.True(perYear < 10, $"a year in {perYear:F2} ms");
        Assert.True(write < TimeSpan.FromMilliseconds(500), $"written in {write.TotalMilliseconds:F0} ms");
        Assert.True(reread < TimeSpan.FromMilliseconds(500), $"read in {reread.TotalMilliseconds:F0} ms");
    }
}

/// <summary>Tests that measure time and memory, run with no other test beside them.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class Alone
{
    public const string Name = "Measured alone";
}
