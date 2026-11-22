// ICalendar (Karpo.Dock\Islands\ICalendar.cs): recurring events expanded over
// the range a view shows, in the zone the calendar is shown in. Daylight-saving
// changes on either side, all-day and multi-day events, zones, EXDATE, RDATE,
// overrides, and the RFC 5545 examples for the rule parts. Every zone is named,
// so the tests pass whatever this PC's own zone is.

using System.Globalization;
using Karpo.Dock;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Calendars;

public sealed class RecurrenceTests
{
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
    private static readonly TimeZoneInfo NewYork = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
    private static readonly TimeZoneInfo Tokyo = TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time");

    private static ICalendar Feed(params string[] events) =>
        Kept.Parse("BEGIN:VCALENDAR\r\nVERSION:2.0\r\n" + string.Concat(events) + "END:VCALENDAR\r\n");

    private static string Event(params string[] lines) =>
        "BEGIN:VEVENT\r\n" + string.Concat(lines.Select(line => line + "\r\n")) + "END:VEVENT\r\n";

    private static DateTime At(string text) => DateTime.ParseExact(text, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    private static DateTime Day(string text) => DateTime.ParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static List<string> Starts(IEnumerable<CalendarEvent> events) =>
        events.Select(item => item.Start.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)).ToList();

    [Fact]
    public void AWeeklyMeetingStaysAtItsTimeAcrossTheChangeToSummerTime()
    {
        var feed = Feed(Event("UID:w", "SUMMARY:Standup", "DTSTART;TZID=W. Europe Standard Time:20260319T090000", "DURATION:PT30M", "RRULE:FREQ=WEEKLY;COUNT=4"));

        // Europe changes on 29 March 2026: the wall-clock time stays, the instant moves.
        Assert.Equal(["2026-03-19 09:00", "2026-03-26 09:00", "2026-04-02 09:00", "2026-04-09 09:00"], Starts(feed.Between(Day("2026-03-01"), Day("2026-05-01"), Berlin)));
        Assert.Equal(["2026-03-19 08:00", "2026-03-26 08:00", "2026-04-02 07:00", "2026-04-09 07:00"], Starts(feed.Between(Day("2026-03-01"), Day("2026-05-01"), TimeZoneInfo.Utc)));
    }

    /// <summary>
    /// A daily series of 40,000 begun in 1970, so still going in 2026: its count runs from its start, and
    /// once the rule was given up on after 20,000 periods — 1970 plus 55 years — it had no occurrence today.
    /// The count goes on to the window now; the second look starts from the marks the first left, and they
    /// agree, and a COUNT that ran out is still out.
    /// </summary>
    [Fact]
    public void ACountedSeriesFromLongAgoStillHasTodaysOccurrence()
    {
        var feed = Feed(Event("UID:pill", "SUMMARY:Pill", "DTSTART:19700101T080000Z", "DURATION:PT5M", "RRULE:FREQ=DAILY;COUNT=40000"));

        Assert.Equal(["2026-10-31 08:00", "2026-11-01 08:00"], Starts(feed.Between(Day("2026-10-31"), Day("2026-11-02"), TimeZoneInfo.Utc)));
        Assert.Equal(["2026-11-01 08:00"], Starts(feed.Between(Day("2026-11-01"), Day("2026-11-02"), TimeZoneInfo.Utc)));
        Assert.Equal(["2026-10-01 08:00"], Starts(feed.Between(Day("2026-10-01"), Day("2026-10-02"), TimeZoneInfo.Utc)));
        // The 40,000th is on 7 July 2079 (day 39,999 after the 1st of January 1970), and none after it.
        Assert.Equal(["2079-07-06 08:00", "2079-07-07 08:00"], Starts(feed.Between(Day("2079-07-06"), Day("2079-07-09"), TimeZoneInfo.Utc)));
    }

    [Fact]
    public void AMeetingInNewYorkMovesInBerlinWhileOnlyOneSideHasSummerTime()
    {
        // The US changes on 8 March 2026, Europe on 29 March.
        var feed = Feed(Event("UID:ny", "SUMMARY:Sync", "DTSTART;TZID=Eastern Standard Time:20260302T090000", "DURATION:PT1H", "RRULE:FREQ=WEEKLY;COUNT=5"));

        Assert.Equal(
            ["2026-03-02 15:00", "2026-03-09 14:00", "2026-03-16 14:00", "2026-03-23 14:00", "2026-03-30 15:00"],
            Starts(feed.Between(Day("2026-03-01"), Day("2026-04-01"), Berlin)));
    }

    [Fact]
    public void ATimeTheClocksSkipIsReadWithTheOffsetBeforeTheGap()
    {
        // RFC 5545 §3.3.5: 02:30 on the morning New York springs forward is 03:30 summer time.
        var feed = Feed(Event("UID:gap", "SUMMARY:Early", "DTSTART;TZID=Eastern Standard Time:20260307T023000", "DURATION:PT15M", "RRULE:FREQ=DAILY;COUNT=3"));

        Assert.Equal(["2026-03-07 02:30", "2026-03-08 03:30", "2026-03-09 02:30"], Starts(feed.Between(Day("2026-03-07"), Day("2026-03-10"), NewYork)));
        Assert.Equal(At("2026-03-08 07:30"), feed.Between(Day("2026-03-08"), Day("2026-03-09"), TimeZoneInfo.Utc).Single().Start);
    }

    [Fact]
    public void ATimeTheClocksPassTwiceIsTheFirstOfTheTwo()
    {
        // RFC 5545 §3.3.5: 01:30 on the night New York falls back is 01:30 summer time, UTC−4.
        var feed = Feed(Event("UID:twice", "SUMMARY:Late", "DTSTART;TZID=Eastern Standard Time:20261101T013000", "DURATION:PT15M"));

        Assert.Equal(At("2026-11-01 05:30"), feed.Between(Day("2026-11-01"), Day("2026-11-02"), TimeZoneInfo.Utc).Single().Start);
    }

    [Fact]
    public void AnEventIsShownInTheZoneTheCalendarIsShownIn()
    {
        var feed = Feed(
            Event("UID:tokyo", "SUMMARY:Tokyo standup", "DTSTART;TZID=Tokyo Standard Time:20260917T090000", "DURATION:PT30M"),
            Event("UID:utc", "SUMMARY:Release", "DTSTART:20260917T120000Z", "DURATION:PT1H"),
            // Floating: the same wall-clock time wherever the calendar is shown.
            Event("UID:float", "SUMMARY:Lunch", "DTSTART:20260917T123000", "DURATION:PT1H"));

        var berlin = feed.Between(Day("2026-09-17"), Day("2026-09-18"), Berlin).ToDictionary(item => item.Title);
        var tokyo = feed.Between(Day("2026-09-17"), Day("2026-09-18"), Tokyo).ToDictionary(item => item.Title);

        Assert.Equal(At("2026-09-17 02:00"), berlin["Tokyo standup"].Start);
        Assert.Equal(At("2026-09-17 14:00"), berlin["Release"].Start);
        Assert.Equal(At("2026-09-17 12:30"), berlin["Lunch"].Start);
        Assert.Equal(At("2026-09-17 09:00"), tokyo["Tokyo standup"].Start);
        Assert.Equal(At("2026-09-17 21:00"), tokyo["Release"].Start);
        Assert.Equal(At("2026-09-17 12:30"), tokyo["Lunch"].Start);
    }

    [Fact]
    public void AnAllDayEventIsTheSameDayInEveryZone()
    {
        var feed = Feed(Event("UID:holiday", "SUMMARY:Holiday", "DTSTART;VALUE=DATE:20260917", "DTEND;VALUE=DATE:20260918"));

        foreach (var zone in new[] { Berlin, NewYork, Tokyo })
        {
            var holiday = feed.Between(Day("2026-09-17"), Day("2026-09-18"), zone).Single();
            Assert.True(holiday.AllDay);
            Assert.Equal(Day("2026-09-17"), holiday.Start);
            Assert.Equal(Day("2026-09-18"), holiday.End);
        }
    }

    /// <summary>
    /// The person, 2026-11-09: "when i have a very old recurring event in the calendar like a birthday does it work correctly
    /// with karpo? lets say the start date is 10 years in the past." A yearly whole-day series begun in 2016 (and one begun
    /// in 1950, with a COUNT that runs past today) is on its day this year and next, in every zone, once each.
    /// </summary>
    [Fact]
    public void ABirthdayBegunLongAgoIsOnItsDayEveryYear()
    {
        var feed = Feed(
            Event("UID:birthday", "SUMMARY:Birthday", "DTSTART;VALUE=DATE:20161114", "DTEND;VALUE=DATE:20161115", "RRULE:FREQ=YEARLY"),
            Event("UID:grandma", "SUMMARY:Grandma", "DTSTART;VALUE=DATE:19500302", "RRULE:FREQ=YEARLY;COUNT=120"));

        foreach (var zone in new[] { Berlin, NewYork, Tokyo })
        {
            var birthday = feed.Between(Day("2026-11-01"), Day("2026-12-01"), zone).Single();
            Assert.Equal(("Birthday", true, Day("2026-11-14"), Day("2026-11-15")), (birthday.Title, birthday.AllDay, birthday.Start, birthday.End));
        }
        Assert.Equal(["2026-11-14 00:00", "2027-11-14 00:00"], Starts(feed.Between(Day("2026-06-01"), Day("2028-01-01"), Berlin).Where(each => each.Title == "Birthday")));
        Assert.Equal(["2027-03-02 00:00"], Starts(feed.Between(Day("2027-01-01"), Day("2028-01-01"), Berlin).Where(each => each.Title == "Grandma")));
    }

    [Fact]
    public void AYearlyEventOnTheTwentyNinthOfFebruaryIsOnlyInLeapYears()
    {
        var feed = Feed(Event("UID:leap", "SUMMARY:Leap day", "DTSTART;VALUE=DATE:20240229", "RRULE:FREQ=YEARLY"));

        Assert.Equal(["2024-02-29 00:00", "2028-02-29 00:00"], Starts(feed.Between(Day("2024-01-01"), Day("2029-01-01"), Berlin)));
    }

    [Fact]
    public void AMultiDayOccurrenceThatStartedBeforeTheRangeIsInIt()
    {
        var feed = Feed(
            Event("UID:trip", "SUMMARY:Trip", "DTSTART;VALUE=DATE:20260907", "DTEND;VALUE=DATE:20260910", "RRULE:FREQ=WEEKLY;COUNT=3"),
            Event("UID:night", "SUMMARY:Night shift", "DTSTART:20260916T220000Z", "DURATION:PT4H"));

        var trip = Assert.Single(feed.Between(Day("2026-09-16"), Day("2026-09-17"), Berlin), item => item.Title == "Trip");
        Assert.Equal(Day("2026-09-14"), trip.Start);
        Assert.Equal(Day("2026-09-17"), trip.End);
        var night = Assert.Single(feed.Between(Day("2026-09-17"), Day("2026-09-18"), TimeZoneInfo.Utc), item => item.Title == "Night shift");
        Assert.Equal(At("2026-09-16 22:00"), night.Start);
        Assert.Equal(At("2026-09-17 02:00"), night.End);
    }

    [Fact]
    public void ExceptionsAndOverridesReplaceTheirOccurrences()
    {
        var feed = Feed(
            Event("UID:team", "SUMMARY:Team", "DTSTART;TZID=W. Europe Standard Time:20260907T100000", "DURATION:PT1H",
                "RRULE:FREQ=WEEKLY;COUNT=5", "EXDATE;TZID=W. Europe Standard Time:20260914T100000"),
            // The 21st moved to the 22nd in the afternoon, the 28th called off.
            Event("UID:team", "SUMMARY:Team (moved)", "RECURRENCE-ID;TZID=W. Europe Standard Time:20260921T100000",
                "DTSTART;TZID=W. Europe Standard Time:20260922T150000", "DURATION:PT1H"),
            Event("UID:team", "SUMMARY:Team", "RECURRENCE-ID:20260928T080000Z", "DTSTART;TZID=W. Europe Standard Time:20260928T100000",
                "DURATION:PT1H", "STATUS:CANCELLED"));

        var found = feed.Between(Day("2026-09-01"), Day("2026-10-15"), Berlin);

        Assert.Equal(["2026-09-07 10:00", "2026-09-22 15:00", "2026-10-05 10:00"], Starts(found));
        Assert.Equal("Team (moved)", found[1].Title);
    }

    [Fact]
    public void AWholeDayExceptionTakesOutThatDaysOccurrenceOfATimedSeries()
    {
        var feed = Feed(Event("UID:daily", "SUMMARY:Daily", "DTSTART:20260914T070000Z", "DURATION:PT15M",
            "RRULE:FREQ=DAILY;COUNT=3", "EXDATE;VALUE=DATE:20260915"));

        Assert.Equal(["2026-09-14 07:00", "2026-09-16 07:00"], Starts(feed.Between(Day("2026-09-14"), Day("2026-09-20"), TimeZoneInfo.Utc)));
    }

    [Fact]
    public void ExtraDatesAddOccurrences()
    {
        var feed = Feed(
            Event("UID:r", "SUMMARY:Visits", "DTSTART:20260901T090000Z", "DURATION:PT1H",
                "RDATE:20260905T090000Z,20260910T090000Z", "RDATE;VALUE=PERIOD:20260912T090000Z/PT1H", "EXDATE:20260910T090000Z"),
            // With a rule, an extra date joins its occurrences.
            Event("UID:s", "SUMMARY:Rule", "DTSTART;VALUE=DATE:20260901", "RRULE:FREQ=MONTHLY;COUNT=2", "RDATE;VALUE=DATE:20260915"));

        var found = feed.Between(Day("2026-09-01"), Day("2026-11-01"), TimeZoneInfo.Utc);

        Assert.Equal(["2026-09-01 09:00", "2026-09-05 09:00", "2026-09-12 09:00"], Starts(found.Where(item => item.Title == "Visits")));
        Assert.Equal(["2026-09-01 00:00", "2026-09-15 00:00", "2026-10-01 00:00"], Starts(found.Where(item => item.Title == "Rule")));
    }

    [Fact]
    public void UntilIsInclusiveAndADateMeansTheWholeDay()
    {
        var feed = Feed(
            Event("UID:u", "SUMMARY:Timed", "DTSTART;TZID=W. Europe Standard Time:20260901T090000", "DURATION:PT1H", "RRULE:FREQ=DAILY;UNTIL=20260903T070000Z"),
            Event("UID:d", "SUMMARY:AllDay", "DTSTART;VALUE=DATE:20260901", "RRULE:FREQ=DAILY;UNTIL=20260903"));

        var found = feed.Between(Day("2026-09-01"), Day("2026-09-10"), Berlin);

        Assert.Equal(["2026-09-01 09:00", "2026-09-02 09:00", "2026-09-03 09:00"], Starts(found.Where(item => item.Title == "Timed")));
        Assert.Equal(["2026-09-01 00:00", "2026-09-02 00:00", "2026-09-03 00:00"], Starts(found.Where(item => item.Title == "AllDay")));
    }

    [Fact]
    public void EveryOtherWeekOnThreeDaysUntilChristmasEve()
    {
        // RFC 5545 §3.8.5.3: every other week on Monday, Wednesday and Friday until 24 December 1997.
        var feed = Feed(Event("UID:rfc", "SUMMARY:x", "DTSTART;TZID=Eastern Standard Time:19970901T090000",
            "RRULE:FREQ=WEEKLY;INTERVAL=2;UNTIL=19971224T000000Z;WKST=SU;BYDAY=MO,WE,FR"));

        var days = feed.Between(Day("1997-09-01"), Day("1998-01-01"), NewYork).Select(item => item.Start.ToString("MM-dd", CultureInfo.InvariantCulture));

        Assert.Equal(
            ["09-01", "09-03", "09-05", "09-15", "09-17", "09-19", "09-29", "10-01", "10-03", "10-13", "10-15", "10-17",
             "10-27", "10-29", "10-31", "11-10", "11-12", "11-14", "11-24", "11-26", "11-28", "12-08", "12-10", "12-12", "12-22"],
            days);
    }

    [Fact]
    public void TheWeekStartDecidesWhichDaysAnEveryOtherWeekRuleTakes()
    {
        // RFC 5545 §3.3.10: the same rule with WKST=MO and WKST=SU.
        static List<string> For(string weekStart) => Feed(Event("UID:k", "SUMMARY:x", "DTSTART:19970805T090000Z",
                $"RRULE:FREQ=WEEKLY;INTERVAL=2;COUNT=4;BYDAY=TU,SU;WKST={weekStart}"))
            .Between(Day("1997-08-01"), Day("1997-09-30"), TimeZoneInfo.Utc)
            .Select(item => item.Start.ToString("MM-dd", CultureInfo.InvariantCulture)).ToList();

        Assert.Equal(["08-05", "08-10", "08-19", "08-24"], For("MO"));
        Assert.Equal(["08-05", "08-17", "08-19", "08-31"], For("SU"));
    }

    [Fact]
    public void MonthlyRulesPickTheirDays()
    {
        var feed = Feed(
            // The last working day of each month.
            Event("UID:last", "SUMMARY:Last", "DTSTART:20260930T160000Z", "RRULE:FREQ=MONTHLY;BYDAY=MO,TU,WE,TH,FR;BYSETPOS=-1;COUNT=3"),
            // The 31st: months without one are skipped.
            Event("UID:31", "SUMMARY:ThirtyFirst", "DTSTART;VALUE=DATE:20260131", "RRULE:FREQ=MONTHLY;COUNT=4"),
            // The second Tuesday.
            Event("UID:2tu", "SUMMARY:Patch", "DTSTART:20260908T170000Z", "RRULE:FREQ=MONTHLY;BYDAY=2TU;COUNT=3"));

        var found = feed.Between(Day("2026-01-01"), Day("2027-01-01"), TimeZoneInfo.Utc);

        Assert.Equal(["2026-09-30 16:00", "2026-10-30 16:00", "2026-11-30 16:00"], Starts(found.Where(item => item.Title == "Last")));
        Assert.Equal(["2026-01-31 00:00", "2026-03-31 00:00", "2026-05-31 00:00", "2026-07-31 00:00"], Starts(found.Where(item => item.Title == "ThirtyFirst")));
        Assert.Equal(["2026-09-08 17:00", "2026-10-13 17:00", "2026-11-10 17:00"], Starts(found.Where(item => item.Title == "Patch")));
    }

    [Fact]
    public void EveryFridayTheThirteenth()
    {
        // RFC 5545 §3.8.5.3, DTSTART itself taken out.
        var feed = Feed(Event("UID:13", "SUMMARY:x", "DTSTART;TZID=Eastern Standard Time:19970902T090000",
            "EXDATE;TZID=Eastern Standard Time:19970902T090000", "RRULE:FREQ=MONTHLY;BYDAY=FR;BYMONTHDAY=13"));

        var days = feed.Between(Day("1997-09-01"), Day("2000-12-31"), NewYork).Select(item => item.Start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

        Assert.Equal(["1998-02-13", "1998-03-13", "1998-11-13", "1999-08-13", "2000-10-13"], days);
    }

    [Fact]
    public void YearlyRulesByWeekNumberAndByDayOfTheYear()
    {
        var feed = Feed(
            // RFC 5545: Monday of week number 20.
            Event("UID:w20", "SUMMARY:Week20", "DTSTART;TZID=Eastern Standard Time:19970512T090000", "RRULE:FREQ=YEARLY;BYWEEKNO=20;BYDAY=MO"),
            // RFC 5545: every third year on the 1st, 100th and 200th day, ten times.
            Event("UID:yd", "SUMMARY:YearDays", "DTSTART;TZID=Eastern Standard Time:19970101T090000", "RRULE:FREQ=YEARLY;INTERVAL=3;COUNT=10;BYYEARDAY=1,100,200"));

        var found = feed.Between(Day("1997-01-01"), Day("2008-01-01"), NewYork);

        Assert.Equal(["1997-05-12", "1998-05-11", "1999-05-17"],
            found.Where(item => item.Title == "Week20").Take(3).Select(item => item.Start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        Assert.Equal(
            ["1997-01-01", "1997-04-10", "1997-07-19", "2000-01-01", "2000-04-09", "2000-07-18", "2003-01-01", "2003-04-10", "2003-07-19", "2006-01-01"],
            found.Where(item => item.Title == "YearDays").Select(item => item.Start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void ByHourGivesSeveralOccurrencesADay()
    {
        var feed = Feed(Event("UID:h", "SUMMARY:Pills", "DTSTART:20260901T090000Z", "RRULE:FREQ=DAILY;BYHOUR=9,17;COUNT=4"));

        Assert.Equal(["2026-09-01 09:00", "2026-09-01 17:00", "2026-09-02 09:00", "2026-09-02 17:00"], Starts(feed.Between(Day("2026-09-01"), Day("2026-09-10"), TimeZoneInfo.Utc)));
    }

    [Fact]
    public void ARuleFarBackIsExpandedOnlyNearTheRange()
    {
        var feed = Feed(Event("UID:old", "SUMMARY:Old", "DTSTART:20000103T090000Z", "RRULE:FREQ=DAILY"));

        var found = feed.Between(Day("2026-09-01"), Day("2026-09-08"), TimeZoneInfo.Utc);

        Assert.Equal(7, found.Count);
        Assert.Equal(At("2026-09-01 09:00"), found[0].Start);
    }

    [Fact]
    public void AnUnknownRuleLeavesTheEventOut()
    {
        var feed = Feed(Event("UID:hourly", "SUMMARY:Hourly", "DTSTART:20260901T090000Z", "RRULE:FREQ=HOURLY;COUNT=5"));

        Assert.Empty(feed.Between(Day("2026-09-01"), Day("2026-09-02"), TimeZoneInfo.Utc));
    }

    [Fact]
    public void AZoneTheFeedDefinesItselfIsUsed()
    {
        const string zone =
            "BEGIN:VTIMEZONE\r\nTZID:(UTC+01:00) Amsterdam, Berlin, Bern, Rome, Stockholm, Vienna\r\n"
            + "BEGIN:STANDARD\r\nDTSTART:16010101T030000\r\nTZOFFSETFROM:+0200\r\nTZOFFSETTO:+0100\r\nRRULE:FREQ=YEARLY;BYDAY=-1SU;BYMONTH=10\r\nEND:STANDARD\r\n"
            + "BEGIN:DAYLIGHT\r\nDTSTART:16010101T020000\r\nTZOFFSETFROM:+0100\r\nTZOFFSETTO:+0200\r\nRRULE:FREQ=YEARLY;BYDAY=-1SU;BYMONTH=3\r\nEND:DAYLIGHT\r\n"
            + "END:VTIMEZONE\r\n";
        var feed = Feed(zone,
            Event("UID:summer", "SUMMARY:Summer", "DTSTART;TZID=\"(UTC+01:00) Amsterdam, Berlin, Bern, Rome, Stockholm, Vienna\":20260715T090000", "DURATION:PT1H"),
            Event("UID:winter", "SUMMARY:Winter", "DTSTART;TZID=\"(UTC+01:00) Amsterdam, Berlin, Bern, Rome, Stockholm, Vienna\":20260115T090000", "DURATION:PT1H"));

        var found = feed.Between(Day("2026-01-01"), Day("2027-01-01"), TimeZoneInfo.Utc).ToDictionary(item => item.Title);

        Assert.Equal(At("2026-07-15 07:00"), found["Summer"].Start);
        Assert.Equal(At("2026-01-15 08:00"), found["Winter"].Start);
    }

    [Fact]
    public void TheCalendarsOwnNameAndColourAreRead()
    {
        var apple = ICalendar.Parse("BEGIN:VCALENDAR\r\nX-WR-CALNAME:Work\r\nX-APPLE-CALENDAR-COLOR:#FF2968FF\r\nEND:VCALENDAR\r\n");
        var rfc = ICalendar.Parse("BEGIN:VCALENDAR\r\nNAME:Family\r\nCOLOR:turquoise\r\nEND:VCALENDAR\r\n");
        var none = ICalendar.Parse("BEGIN:VCALENDAR\r\nEND:VCALENDAR\r\n");

        Assert.Equal("Work", apple.Name);
        Assert.Equal(Color.FromArgb(255, 0xFF, 0x29, 0x68), apple.Colour);
        Assert.Equal("Family", rfc.Name);
        Assert.Equal(Color.FromArgb(255, 0x40, 0xE0, 0xD0), rfc.Colour);
        Assert.Null(none.Name);
        Assert.Null(none.Colour);
    }
}
