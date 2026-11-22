// The next meeting (docs\widgets.md §205): which events are meetings and which comes first
// (Karpo.Dock\Calendar\Meetings.cs) — the one on now, the next today, all-day, free, declined and
// ended ones passed by, two at once, a meeting about to start before one long on — measured between
// instants, so a night the clocks change and a zone of its own say the right length; the words for
// how soon, and what of the title and those words a face of a given width shows; what the face shows
// for each of its three settings (MeetingFaceDrawing.StateOf); and where the link and "free" come from
// in what the accounts give: Graph's event, a CalDAV event, the offline cache. Plain code: no window.

using System.Globalization;
using System.Text.Json.Nodes;
using Karpo.Accounts;
using Karpo.Accounts.Microsoft;
using Karpo.Dock;
using Karpo.Dock.Pim;
using Karpo.Tests.Core;

namespace Karpo.Tests.Calendars;

public sealed class MeetingTests
{
    private static readonly TimeZoneInfo Zurich = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
    private static readonly TimeZoneInfo Tokyo = TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time");

    private const string Teams = "https://teams.microsoft.com/l/meetup-join/19%3ameeting_abc%40thread.v2/0";
    private const string Zoom = "https://us02web.zoom.us/j/85550001111";

    /// <summary>A Friday in Zurich, a quarter to ten.</summary>
    private static readonly DateTime Now = new(2026, 10, 16, 9, 45, 0);

    private static string Time(DateTime at) => at.ToString("HH:mm", CultureInfo.InvariantCulture);

    private static CalendarEvent At(double hour, double hours, string title, string? join = null, string? where = null, bool free = false, bool allDay = false, int day = 0)
    {
        // To the whole minute: 9.8333 hours is 09:50.
        var start = Now.Date.AddDays(day).AddMinutes(Math.Round(hour * 60));
        return allDay
            ? new CalendarEvent(Now.Date.AddDays(day), Now.Date.AddDays(day + 1), true, title, null)
            : new CalendarEvent(start, start.AddMinutes(Math.Round(hours * 60)), false, title, where, Join: join, Free: free);
    }

    private static List<Meeting> Ahead(DateTime now, params CalendarEvent[] events) =>
        Meetings.Ahead(events, Stamp.WallToUtc(now, Zurich), Zurich);

    private static string[] Titles(IEnumerable<Meeting> meetings) => [.. meetings.Select(meeting => meeting.Event.Title)];

    // --- Which meetings, in which order -----------------------------------------------------------

    [Fact]
    public void TheOneOnNowComesFirstThenTheRestByTheirStart()
    {
        var meetings = Ahead(Now,
            At(14, 1, "Planning"),
            At(9.5, 0.5, "Standup"),
            At(11, 1, "Review"));

        Assert.Equal(["Standup", "Review", "Planning"], Titles(meetings));
        Assert.True(meetings[0].OnAt(Stamp.WallToUtc(Now, Zurich)));
    }

    [Fact]
    public void AllDayFreeDeclinedAndEndedEventsAreNoMeetings()
    {
        var meetings = Ahead(Now,
            At(0, 0, "Holiday", allDay: true),
            At(10, 1, "Focus time", free: true),
            At(8, 1, "Breakfast talk"),
            At(9, 0.75, "Ended right now"),
            At(12, 1, "Lunch with Sam"));

        Assert.Equal(["Lunch with Sam"], Titles(meetings));
    }

    [Fact]
    public void OfTwoOnAtOnceTheNewerIsTheOneTheyAreIn()
    {
        var meetings = Ahead(Now,
            At(9, 2, "Workshop"),
            At(9.5, 0.5, "Standup"));

        Assert.Equal(["Standup", "Workshop"], Titles(meetings));
    }

    [Fact]
    public void AMeetingLongOnGivesWayToOneAboutToStart()
    {
        // On for 45 minutes, the next in 5: that one is where they are going.
        var going = Ahead(Now, At(9, 2, "Workshop"), At(9.8333333333, 0.5, "Call with Anna"));
        Assert.Equal(["Call with Anna", "Workshop"], Titles(going));

        // Started two minutes ago: it stays first, however soon the next.
        var started = Ahead(Now, At(9.7166666667, 1, "Kick-off"), At(9.8333333333, 0.5, "Call with Anna"));
        Assert.Equal(["Kick-off", "Call with Anna"], Titles(started));

        // The next in twenty minutes: not yet.
        var later = Ahead(Now, At(9, 2, "Workshop"), At(10.0833333333, 0.5, "Call with Anna"));
        Assert.Equal(["Workshop", "Call with Anna"], Titles(later));
    }

    [Fact]
    public void TodayIsTodaysAndNextLooksIntoTheWeek()
    {
        var meetings = Ahead(Now, At(9, 0.5, "Over"), At(9, 0.5, "Monday standup", day: 3));

        Assert.Null(Meetings.Shown(meetings, MeetingFace.Today, TimeSpan.FromMinutes(10), Now, Zurich));
        Assert.Equal("Monday standup", Meetings.Shown(meetings, MeetingFace.Next, TimeSpan.FromMinutes(10), Now, Zurich)?.Event.Title);
    }

    [Fact]
    public void SoonShowsAMeetingOnlyFromItsLeadUntilItEnds()
    {
        var lead = TimeSpan.FromMinutes(10);
        var meetings = Ahead(Now, At(10, 0.5, "Review"));

        Assert.Null(Meetings.Shown(meetings, MeetingFace.Soon, lead, Now, Zurich));
        Assert.Equal("Review", Meetings.Shown(meetings, MeetingFace.Soon, lead, Now.AddMinutes(5), Zurich)?.Event.Title);
        Assert.Equal("Review", Meetings.Shown(Ahead(Now.AddMinutes(30), At(10, 0.5, "Review")), MeetingFace.Soon, lead, Now.AddMinutes(30), Zurich)?.Event.Title);
        Assert.False(Meetings.Lit(meetings[0], Stamp.WallToUtc(Now, Zurich), lead));
        Assert.True(Meetings.Lit(meetings[0], Stamp.WallToUtc(Now.AddMinutes(5), Zurich), lead));
    }

    // --- Lengths across zones and the clocks' changes ---------------------------------------------

    [Fact]
    public void TheNightTheClocksGoForwardFortyMinutesAreForty()
    {
        // 29 March 2026 in Zurich: 02:00 is 03:00. From 01:50 to 03:30 is forty minutes.
        var now = new DateTime(2026, 3, 29, 1, 50, 0);
        var start = new DateTime(2026, 3, 29, 3, 30, 0);
        var meeting = Assert.Single(Meetings.Ahead([new CalendarEvent(start, start.AddHours(1), false, "Night shift handover", null)], Stamp.WallToUtc(now, Zurich), Zurich));

        Assert.Equal("in 40 min", MeetingText.When(meeting, now, Zurich, Time));
    }

    [Fact]
    public void TheNightTheClocksGoBackFortyMinutesAreForty()
    {
        // 25 October 2026 in Zurich: 03:00 is 02:00 again; 02:30 is read as its first, summer time.
        var now = new DateTime(2026, 10, 25, 1, 50, 0);
        var start = new DateTime(2026, 10, 25, 2, 30, 0);
        var meeting = Assert.Single(Meetings.Ahead([new CalendarEvent(start, start.AddHours(1), false, "Night shift handover", null)], Stamp.WallToUtc(now, Zurich), Zurich));

        Assert.Equal("in 40 min", MeetingText.When(meeting, now, Zurich, Time));
    }

    [Fact]
    public void AnAccountsMeetingIsReadInTheZoneTheCalendarIsShownIn()
    {
        // Graph's 08:00 UTC is 17:00 in Tokyo; at 16:56 there it is four minutes away.
        var utc = new DateTime(2026, 10, 16, 8, 0, 0, DateTimeKind.Utc);
        var reader = new Pim.FakeReader();
        reader.Calendars.Add(new CalendarRead(new RemoteCalendar("work", "Work", "#0F6CBD"),
            [new Occurrence("Standup", null, false, false, utc, utc.AddMinutes(15), Teams)], []));
        var store = new CalendarStore(new AccountCalendars(new AccountSet([reader]), () => utc));
        store.Open();
        store.Idle().GetAwaiter().GetResult();
        var now = new DateTime(2026, 10, 16, 16, 56, 0);

        var meeting = Assert.Single(Meetings.Ahead(store, now, 1, Tokyo));

        Assert.Equal(new DateTime(2026, 10, 16, 17, 0, 0), meeting.Event.Start);
        Assert.Equal(Teams, meeting.Event.Join);
        Assert.Equal("in 4 min", MeetingText.When(meeting, now, Tokyo, Time));
    }

    // --- The words ---------------------------------------------------------------------------------

    [Fact]
    public void HowSoonIsSaidAsTheClockWouldSayIt()
    {
        string When(DateTime now, CalendarEvent item) =>
            MeetingText.When(Assert.Single(Ahead(now, item)), now, Zurich, Time, day => day.ToString("dddd d MMMM", CultureInfo.InvariantCulture));

        var review = At(10, 0.5, "Review");
        Assert.Equal("in 15 min", When(Now, review));
        // A part minute is a minute more: the last before "now" is "in 1 min".
        Assert.Equal("in 1 min", When(Now.AddMinutes(14).AddSeconds(30), review));
        Assert.Equal("now", When(Now.AddMinutes(15).AddSeconds(40), review));
        Assert.Equal("started 3 min ago", When(Now.AddMinutes(18).AddSeconds(50), review));
        Assert.Equal("in 1 h 5 min", When(Now, At(10.8333333333, 1, "Lunch")));
        Assert.Equal("in 2 h", When(Now, At(11.75, 1, "Lunch")));
        Assert.Equal("at 15:00", When(Now, At(15, 1, "Planning")));
        Assert.Equal("Tomorrow, 09:00", When(Now, At(9, 1, "Saturday run", day: 1)));
        Assert.Equal("Monday 19 October, 09:00", When(Now, At(9, 1, "Monday standup", day: 3)));
    }

    [Fact]
    public void ItsDetailIsItsTimeAndItsPlaceOrItsService()
    {
        var at = Ahead(Now, At(10, 0.5, "Review", where: "Room 3"), At(11, 0.5, "Sync", join: Teams), At(12, 0.5, "Zoom call", join: Zoom, where: Zoom));

        Assert.Equal("10:00–10:30 · Room 3", MeetingText.Detail(at[0], Now, Time));
        Assert.Equal("11:00–11:30 · Microsoft Teams", MeetingText.Detail(at[1], Now, Time));
        // A place that is only the link says the service, not the address.
        Assert.Equal("12:00–12:30 · Zoom", MeetingText.Detail(at[2], Now, Time));
    }

    /// <summary>Every character one px wide: the room is in characters.</summary>
    private static float Chars(string text) => text.Length;

    [Theory]
    // Room for all of it.
    [InlineData(40, "Design review", "Design review · in 4 min")]
    // The title cut to what leaves how soon whole, two characters of it at the least.
    [InlineData(20, "Design review", "Design r… · in 4 min")]
    [InlineData(17, "Design review", "Desig… · in 4 min")]
    [InlineData(14, "Design review", "De… · in 4 min")]
    // Under that, how soon alone.
    [InlineData(13, "Design review", "in 4 min")]
    [InlineData(5, "Design review", "in 4 min")]
    // A title that fits whole is never cut, nor one with its trailing space.
    [InlineData(18, "Standup  ", "Standup · in 4 min")]
    public void TheTitleGivesWayBeforeHowSoon(float room, string title, string shown)
    {
        var (fitted, when) = MeetingText.Fit(title, "in 4 min", room, Chars);
        Assert.Equal("in 4 min", when);
        Assert.Equal(shown, fitted.Length > 0 ? fitted + " · " + when : when);
        Assert.True(fitted.Length == 0 || Chars(fitted + " · " + when) <= room);
    }

    [Fact]
    public void AnEmojiIsNeverCutInHalf()
    {
        var (fitted, _) = MeetingText.Fit("🎉🎉🎉🎉 party", "now", 11, Chars);
        Assert.False(char.IsHighSurrogate(fitted[^2]), fitted);
    }

    // --- The face -----------------------------------------------------------------------------------

    private static CalendarStore Store(params CalendarEvent[] events)
    {
        var store = new CalendarStore();
        store.Add(new ListCalendar("work", "Work", CalendarColours.Of("#0F6CBD"), events));
        return store;
    }

    private static FolderFace Face(string show = MeetingFace.Today, string? lead = null)
    {
        var face = MeetingFace.New();
        face.Set(FolderFaces.Meeting.Option(MeetingFace.Show)!, show);
        if (lead is not null)
        {
            face.Set(FolderFaces.Meeting.Option(MeetingFace.Lead)!, lead);
        }
        return face;
    }

    [Fact]
    public void TheFaceSaysTodaysNextOrThatThereIsNone()
    {
        var store = Store(At(10, 0.5, "Design review", join: Teams));

        var next = MeetingFaceDrawing.StateOf(Face(), store, Now, Zurich);
        Assert.Equal("Design review", next.Title);
        Assert.Equal("in 15 min", next.When);
        Assert.False(next.Lit);
        Assert.Equal(MeetingService.Teams, next.Link?.Service);

        var lit = MeetingFaceDrawing.StateOf(Face(), store, Now.AddMinutes(6), Zurich);
        Assert.True(lit.Lit);

        var after = MeetingFaceDrawing.StateOf(Face(), store, Now.AddHours(1), Zurich);
        Assert.Null(after.Shown);
        Assert.Equal("No more meetings today", after.Placeholder);
        Assert.False(after.Hidden);
    }

    [Fact]
    public void TheFaceSetToSoonTakesNoRoomUntilThen()
    {
        var store = Store(At(10, 0.5, "Design review"));

        Assert.True(MeetingFaceDrawing.StateOf(Face(MeetingFace.Soon, lead: MeetingFace.Five), store, Now, Zurich).Hidden);
        var shown = MeetingFaceDrawing.StateOf(Face(MeetingFace.Soon, lead: MeetingFace.Fifteen), store, Now, Zurich);
        Assert.False(shown.Hidden);
        Assert.True(shown.Lit);
    }

    [Fact]
    public void WithNoCalendarTheFaceIsADash()
    {
        var state = MeetingFaceDrawing.StateOf(Face(), new CalendarStore(), Now, Zurich);
        Assert.Null(state.Shown);
        // §388: an empty face is its glyph and a dash, no words like "No calendar".
        Assert.Equal(FaceDrawings.Dash, state.Placeholder);
    }

    // --- Where the link and "free" come from -------------------------------------------------------

    private static JsonObject Json(string text) => (JsonObject)JsonNode.Parse(text)!;

    [Fact]
    public void GraphsOnlineMeetingIsTheLinkAndItsDeclinedAndFreeAreFree()
    {
        var teams = MicrosoftReader.Event(Json($$"""
            {
              "subject": "Standup", "isAllDay": false,
              "start": { "dateTime": "2026-10-16T08:00:00" }, "end": { "dateTime": "2026-10-16T08:15:00" },
              "location": { "displayName": "Microsoft Teams Meeting" },
              "isOnlineMeeting": true, "onlineMeeting": { "joinUrl": "{{Teams}}" },
              "showAs": "busy", "responseStatus": { "response": "accepted" },
              "bodyPreview": "________________________________________________________________________________ Microsoft Teams meeting"
            }
            """))!.Value;
        Assert.Equal(Teams, teams.Join);
        Assert.False(teams.Free);

        // Zoom's add-in puts its link in the place; a pasted invitation in the notes' first lines.
        var zoom = MicrosoftReader.Event(Json($$"""
            { "subject": "Zoom call", "start": { "dateTime": "2026-10-16T09:00:00" }, "end": { "dateTime": "2026-10-16T09:30:00" },
              "location": { "displayName": "{{Zoom}}" }, "onlineMeeting": null, "showAs": "tentative" }
            """))!.Value;
        Assert.Equal(Zoom, zoom.Join);
        var pasted = MicrosoftReader.Event(Json("""
            { "subject": "Sync", "start": { "dateTime": "2026-10-16T09:00:00" }, "end": { "dateTime": "2026-10-16T09:30:00" },
              "bodyPreview": "Join Google Meet: https://meet.google.com/abc-defg-hij Or dial +41 44 000 00 00" }
            """))!.Value;
        Assert.Equal("https://meet.google.com/abc-defg-hij", pasted.Join);

        var declined = MicrosoftReader.Event(Json("""
            { "subject": "Optional", "start": { "dateTime": "2026-10-16T09:00:00" }, "end": { "dateTime": "2026-10-16T09:30:00" },
              "responseStatus": { "response": "declined" } }
            """))!.Value;
        var free = MicrosoftReader.Event(Json("""
            { "subject": "Focus", "start": { "dateTime": "2026-10-16T09:00:00" }, "end": { "dateTime": "2026-10-16T09:30:00" }, "showAs": "free" }
            """))!.Value;
        Assert.True(declined.Free);
        Assert.True(free.Free);
        Assert.Null(free.Join);
    }

    [Fact]
    public void ACalDavEventsConferenceIsTheLinkAndTransparentIsFree()
    {
        static ICalendar Feed(string lines) => Kept.Parse(
            "BEGIN:VCALENDAR\r\nVERSION:2.0\r\nBEGIN:VEVENT\r\nUID:a\r\nDTSTART:20261016T080000Z\r\nDTEND:20261016T083000Z\r\nSUMMARY:Sync\r\n"
            + lines + "END:VEVENT\r\nEND:VCALENDAR\r\n");
        var from = new DateTime(2026, 10, 16, 0, 0, 0);

        var google = Feed("X-GOOGLE-CONFERENCE:https://meet.google.com/abc-defg-hij\r\nDESCRIPTION:Notes: https://zoom.us/j/91234567890\r\n");
        var item = Assert.Single(google.Between(from, from.AddDays(1), TimeZoneInfo.Utc));
        Assert.Equal("https://meet.google.com/abc-defg-hij", item.Join);
        Assert.False(item.Free);

        // Folded notes, escaped as RFC 5545 writes them; the link is still whole.
        var notes = Feed("DESCRIPTION:Join Zoom Meeting\\nhttps://us02web.zoom.us/j/8555000\r\n 1111?pwd=abc\\n\\nMeeting ID: 855 5000 1111\r\nTRANSP:TRANSPARENT\r\n");
        var kept = Assert.Single(notes.Between(from, from.AddDays(1), TimeZoneInfo.Utc));
        Assert.Equal("https://us02web.zoom.us/j/85550001111?pwd=abc", kept.Join);
        Assert.True(kept.Free);
    }

    [Fact]
    public void TheOfflineCacheKeepsTheLinkAndFree()
    {
        var utc = new DateTime(2026, 10, 16, 8, 0, 0, DateTimeKind.Utc);
        var occurrence = new Occurrence("Standup", null, false, false, utc, utc.AddMinutes(15), Teams, Free: true);

        Assert.Equal(occurrence, CachedEvent.Of(occurrence).Occurrence);
        // A file from before has neither: the event reads with no link and as taking time.
        var old = new CachedEvent { Title = "Old", Start = utc, End = utc.AddHours(1) }.Occurrence;
        Assert.Null(old.Join);
        Assert.False(old.Free);
    }

    // --- The settings -------------------------------------------------------------------------------

    [Fact]
    public void ThePresetsFolderRoundTripsWithItsFaceAndItsLine()
    {
        var folder = Presets.Folder(Presets.Find(Presets.MeetingId)!, new PresetMachine(HasBattery: false, FixedDrives: []));
        var face = folder.Folder!.Face;
        face.Set(FolderFaces.Meeting.Option(MeetingFace.Show)!, MeetingFace.Soon);
        face.Set(FolderFaces.Meeting.Option(MeetingFace.Width)!, MeetingFace.Wide);
        face.Set(FolderFaces.Meeting.Option(MeetingFace.Join)!, false);

        var read = Configs.RoundTrip(Configs.With(folder), out _);

        var kept = Configs.Islands(read).Single(island => island.Folder is not null).Folder!;
        Assert.Equal(FolderFaces.MeetingId, kept.Face.Kind);
        Assert.Equal(MeetingFace.Soon, MeetingFace.ShowOf(kept.Face));
        Assert.Equal(MeetingFace.Wide, MeetingFace.WidthOf(kept.Face));
        Assert.False(MeetingFace.Joins(kept.Face));
        Assert.Equal(TimeSpan.FromMinutes(10), MeetingFace.LeadOf(kept.Face));
        Assert.Contains(kept.Cells, cell => cell.Island.List == ListKind.Meeting);
    }

    [Fact]
    public void AnOptionThisBuildDoesNotKnowReadsAsItsDefault()
    {
        var face = MeetingFace.New();
        face.Options[MeetingFace.Show] = System.Text.Json.JsonDocument.Parse("\"whenever\"").RootElement.Clone();
        face.Options[MeetingFace.Lead] = System.Text.Json.JsonDocument.Parse("7").RootElement.Clone();

        Assert.Equal(MeetingFace.Today, MeetingFace.ShowOf(face));
        Assert.Equal(TimeSpan.FromMinutes(10), MeetingFace.LeadOf(face));
        Assert.True(MeetingFace.Joins(face));
    }
}
