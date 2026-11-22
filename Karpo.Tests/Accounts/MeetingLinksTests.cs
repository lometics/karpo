// An event's online meeting as the next meeting's Join opens it (Karpo.Accounts\MeetingLinks.cs,
// docs\widgets.md §205): the links of Teams, Zoom (its vanity hosts and zoommtg:), Google Meet, Webex,
// GoTo, Whereby, Jitsi, Slack's huddles, Chime and RingCentral, in the shapes their invitations write
// them, unwrapped from Outlook's Safe Links and Google's redirect; found in the meeting's field, its
// place or its notes, in that order; and nothing else taken for one. The app's own address where a
// link maps onto one, and what the Join opens with and without the app (MeetingJoin.Target).

using Karpo.Accounts;
using Karpo.Dock;

namespace Karpo.Tests.Accounts;

public sealed class MeetingLinksTests
{
    private const string TeamsJoin =
        "https://teams.microsoft.com/l/meetup-join/19%3ameeting_NzQ5ZjYxYjEtMDNiNi00YjQ0LWIwMDAtNjU0YTg2MjZlOWQ5%40thread.v2/0?context=%7b%22Tid%22%3a%2272f988bf-86f1-41af-91ab-2d7cd011db47%22%2c%22Oid%22%3a%22a5b7c3d1-1111-2222-3333-444455556666%22%7d";

    [Theory]
    [InlineData(TeamsJoin, MeetingService.Teams)]
    // Teams' newer short link, and the consumer Teams'.
    [InlineData("https://teams.microsoft.com/meet/2394857612345?p=AbCdEfGhIjKlMnOp", MeetingService.Teams)]
    [InlineData("https://teams.live.com/meet/9876543210123?p=XyZ123", MeetingService.Teams)]
    // The launcher page round a join link, and a government cloud's.
    [InlineData("https://teams.microsoft.com/dl/launcher/launcher.html?url=%2F_%23%2Fl%2Fmeetup-join%2F19%3Ameeting_abc%40thread.v2%2F0&type=meetup-join", MeetingService.Teams)]
    [InlineData("https://gov.teams.microsoft.us/l/meetup-join/19%3ameeting_gov%40thread.v2/0", MeetingService.Teams)]
    [InlineData("https://zoom.us/j/91234567890?pwd=aBcDeFgHiJkLmNoPqRsTuVwXyZ.1", MeetingService.Zoom)]
    // A company's vanity host, Zoom's regional web hosts, a webinar, a personal room, ZoomGov.
    [InlineData("https://acme-corp.zoom.us/j/81234567890?pwd=Q2xhc3NpYw", MeetingService.Zoom)]
    [InlineData("https://us02web.zoom.us/j/85550001111", MeetingService.Zoom)]
    [InlineData("https://us06web.zoom.us/w/98765432101?tk=abc", MeetingService.Zoom)]
    [InlineData("https://zoom.us/my/jane.doe", MeetingService.Zoom)]
    [InlineData("https://agency.zoomgov.com/j/1601234567", MeetingService.Zoom)]
    [InlineData("zoommtg://zoom.us/join?action=join&confno=81234567890&pwd=abc", MeetingService.Zoom)]
    [InlineData("https://meet.google.com/abc-defg-hij", MeetingService.Meet)]
    [InlineData("https://meet.google.com/abc-defg-hij?authuser=0&hs=122", MeetingService.Meet)]
    [InlineData("https://meet.google.com/lookup/a1b2c3d4e5", MeetingService.Meet)]
    [InlineData("https://acme.webex.com/meet/jdoe", MeetingService.Webex)]
    [InlineData("https://acme.webex.com/acme/j.php?MTID=m0123456789abcdef0123456789abcdef", MeetingService.Webex)]
    [InlineData("https://acme.webex.com/join/jdoe", MeetingService.Webex)]
    [InlineData("https://global.gotomeeting.com/join/123456789", MeetingService.GoTo)]
    [InlineData("https://meet.goto.com/123456789", MeetingService.GoTo)]
    [InlineData("https://whereby.com/team-standup", MeetingService.Whereby)]
    [InlineData("https://meet.jit.si/KarpoWeeklySync", MeetingService.Jitsi)]
    [InlineData("https://app.slack.com/huddle/T0123ABCD/C0456EFGH", MeetingService.Slack)]
    [InlineData("https://chime.aws/1234567890", MeetingService.Chime)]
    [InlineData("https://meetings.ringcentral.com/j/1234567890", MeetingService.RingCentral)]
    public void AMeetingLinkIsKnownByItsHostAndPath(string url, MeetingService service)
    {
        var link = MeetingLinks.Of(url);
        Assert.NotNull(link);
        Assert.Equal(service, link.Service);
        Assert.Equal(url, link.Url);
    }

    [Theory]
    // A service's pages that are no meeting: Teams' chat and channel links, Zoom's and Webex's front
    // pages, Meet's own, Slack's channel, a site with a meeting word in it.
    [InlineData("https://teams.microsoft.com/l/chat/0/0?users=jane@example.com")]
    [InlineData("https://teams.microsoft.com/l/channel/19%3aabc%40thread.tacv2/General")]
    [InlineData("https://zoom.us/")]
    [InlineData("https://zoom.us/pricing")]
    [InlineData("https://acme.webex.com/")]
    [InlineData("https://meet.google.com/")]
    [InlineData("https://meet.google.com/new")]
    [InlineData("https://acme.slack.com/archives/C0456EFGH")]
    [InlineData("https://example.com/zoom/j/123456789")]
    [InlineData("https://notzoom.us.example.com/j/123456789")]
    [InlineData("https://wiki.example.com/join-the-meeting")]
    [InlineData("mailto:jane@example.com")]
    [InlineData("Room 4")]
    [InlineData("Microsoft Teams Meeting")]
    [InlineData("")]
    public void AnythingElseIsNoMeeting(string text) =>
        Assert.Null(MeetingLinks.In(text));

    [Fact]
    public void OutlooksSafeLinksAreUnwrapped()
    {
        var wrapped = "https://nam12.safelinks.protection.outlook.com/?url=" + Uri.EscapeDataString(TeamsJoin)
            + "&data=05%7C02%7Cjane%40example.com%7C0123456789%7C0%7C0%7C638000000000000000&sdata=AbCdEf%2BgHiJ%3D&reserved=0";

        var link = MeetingLinks.Of(wrapped);

        Assert.NotNull(link);
        Assert.Equal(MeetingService.Teams, link.Service);
        Assert.Equal(TeamsJoin, link.Url);
    }

    [Fact]
    public void AZoomLinkInSafeLinksInsideTextIsFound()
    {
        var zoom = "https://us02web.zoom.us/j/85550001111?pwd=dGVzdA";
        var notes = "Hi all,\n\nJoin Zoom Meeting\n<https://eur01.safelinks.protection.outlook.com/?url="
            + Uri.EscapeDataString(zoom) + "&data=05%7C01&sdata=x&reserved=0>\n\nMeeting ID: 855 5000 1111";

        var link = MeetingLinks.In(notes);

        Assert.NotNull(link);
        Assert.Equal(zoom, link.Url);
    }

    [Fact]
    public void TeamsSafeLinksPageAndGooglesRedirectAreUnwrapped()
    {
        var meet = "https://meet.google.com/abc-defg-hij";
        Assert.Equal(meet, MeetingLinks.Of("https://statics.teams.cdn.office.net/evergreen-assets/safelinks/1/atp-safelinks.html?url=" + Uri.EscapeDataString(meet))?.Url);
        Assert.Equal(meet, MeetingLinks.Of("https://www.google.com/url?q=" + Uri.EscapeDataString(meet) + "&sa=D&source=calendar&usg=AOvVaw0")?.Url);
    }

    [Theory]
    // A sentence's full stop, a bracket, Outlook's angle brackets, a quote, an HTML body's href with &amp;.
    [InlineData("Join here: https://meet.google.com/abc-defg-hij.", "https://meet.google.com/abc-defg-hij")]
    [InlineData("(https://zoom.us/j/91234567890)", "https://zoom.us/j/91234567890")]
    [InlineData("Click <https://acme.webex.com/meet/jdoe> to join", "https://acme.webex.com/meet/jdoe")]
    [InlineData("<a href=\"https://zoom.us/j/91234567890?pwd=abc&amp;from=addon\">Join</a>", "https://zoom.us/j/91234567890?pwd=abc&from=addon")]
    // A place that holds a bare Meet address, as Google Calendar writes one.
    [InlineData("meet.google.com/abc-defg-hij", "https://meet.google.com/abc-defg-hij")]
    public void ALinkIsTakenOutOfTheTextRoundIt(string text, string url) =>
        Assert.Equal(url, MeetingLinks.In(text)?.Url);

    [Fact]
    public void TheMeetingsOwnFieldComesBeforeItsPlaceAndItsNotes()
    {
        const string zoom = "https://zoom.us/j/91234567890";
        const string meet = "https://meet.google.com/abc-defg-hij";

        Assert.Equal(TeamsJoin, MeetingLinks.Find(TeamsJoin, zoom, "Notes: " + meet)?.Url);
        Assert.Equal(zoom, MeetingLinks.Find(null, "Zoom: " + zoom, "Notes: " + meet)?.Url);
        Assert.Equal(meet, MeetingLinks.Find(null, "Room 4", "Agenda: https://wiki.example.com/q4 then " + meet)?.Url);
        Assert.Null(MeetingLinks.Find(null, "Room 4", "Agenda: https://wiki.example.com/q4"));
    }

    [Fact]
    public void TeamsAndZoomGoToTheirAppsWhereTheLinkMapsOntoIt()
    {
        var teams = MeetingLinks.Of(TeamsJoin)!;
        Assert.Equal("msteams:" + TeamsJoin["https://teams.microsoft.com".Length..], MeetingLinks.AppUri(teams));

        var zoom = MeetingLinks.Of("https://acme-corp.zoom.us/j/81234567890?pwd=Q2xhc3NpYw.1")!;
        Assert.Equal("zoommtg://acme-corp.zoom.us/join?action=join&confno=81234567890&pwd=Q2xhc3NpYw.1", MeetingLinks.AppUri(zoom));

        // A personal room has no number to dial, Meet has no app, a Teams short link no msteams: path.
        Assert.Null(MeetingLinks.AppUri(MeetingLinks.Of("https://zoom.us/my/jane.doe")!));
        Assert.Null(MeetingLinks.AppUri(MeetingLinks.Of("https://meet.google.com/abc-defg-hij")!));
        Assert.Null(MeetingLinks.AppUri(MeetingLinks.Of("https://teams.microsoft.com/meet/2394857612345?p=Ab")!));
    }

    [Fact]
    public void JoinOpensTheAppWhereItIsInstalledElseTheLink()
    {
        var teams = MeetingLinks.Of(TeamsJoin)!;
        var meet = MeetingLinks.Of("https://meet.google.com/abc-defg-hij")!;

        Assert.StartsWith("msteams:/l/meetup-join/", MeetingJoin.Target(teams, scheme => scheme == "msteams"));
        Assert.Equal(TeamsJoin, MeetingJoin.Target(teams, _ => false));
        Assert.Equal(meet.Url, MeetingJoin.Target(meet, _ => true));
    }

    /// <summary>
    /// §665 (the person: "when i click the join button on a meeting nothing happens … (i tried a teams meeting for
    /// example)"): a Teams meeting as Graph gives it — its join link in onlineMeeting.joinUrl, no onlineMeetingUrl, the
    /// invitation's notes without a link — has that link, whole; its Join hands the launcher Teams' own address made
    /// from it, escaped as it came, with the link itself to fall back on, and says a failure in the meeting's words.
    /// </summary>
    [Fact]
    public void ATeamsMeetingFromGraphJoinsTeamsWithItsExactLink()
    {
        var occurrence = Karpo.Accounts.Microsoft.MicrosoftReader.Event((System.Text.Json.Nodes.JsonObject)System.Text.Json.Nodes.JsonNode.Parse($$"""
            { "subject": "Design review", "isAllDay": false,
              "start": { "dateTime": "2026-11-10T14:00:00" }, "end": { "dateTime": "2026-11-10T14:30:00" },
              "location": { "displayName": "Microsoft Teams Meeting" },
              "isOnlineMeeting": true, "onlineMeeting": { "joinUrl": "{{TeamsJoin}}" }, "onlineMeetingUrl": null,
              "bodyPreview": "________________ Microsoft Teams Need help? Join the meeting now Meeting ID: 123 456 789 Passcode: abc" }
            """)!)!.Value;
        Assert.Equal(TeamsJoin, occurrence.Join);

        using var stand = Launch.Stand();
        MeetingJoin.Open(MeetingLinks.Of(occurrence.Join)!, scheme => scheme == "msteams");
        Assert.Equal("leave", stand.Said[0]);
        var launched = Assert.IsType<Launched>(stand.Said[1]);
        Assert.Equal("msteams:" + TeamsJoin["https://teams.microsoft.com".Length..], launched.Target);
        Assert.Equal(TeamsJoin, launched.Otherwise);
        Assert.Equal(S.DockMeetingJoinFailed, launched.Failure);

        // No Teams on this PC: the link itself, in the browser, nothing to fall back on.
        stand.Said.Clear();
        MeetingJoin.Open(MeetingLinks.Of(occurrence.Join)!, _ => false);
        launched = Assert.IsType<Launched>(stand.Said[1]);
        Assert.Equal((TeamsJoin, (string?)null), (launched.Target, launched.Otherwise));
    }
}
