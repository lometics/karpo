// Graph's answers as Karpo reads them (Karpo.Accounts\Microsoft\MicrosoftReader.cs):
// events from calendarView, asked for in UTC; calendars and their colours;
// contacts; messages. The JSON is the shape Graph documents and sends.

using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Karpo.Accounts;
using Karpo.Accounts.Microsoft;

namespace Karpo.Tests.Accounts;

public sealed class MicrosoftReaderTests
{
    private static JsonObject Json(string text) => (JsonObject)JsonNode.Parse(text)!;

    [Fact]
    public void ATimedEventIsAnInstantInUtc()
    {
        var item = MicrosoftReader.Event(Json("""
            {
              "subject": "Standup",
              "isAllDay": false,
              "isCancelled": false,
              "start": { "dateTime": "2026-10-06T07:00:00.0000000", "timeZone": "UTC" },
              "end": { "dateTime": "2026-10-06T07:15:00.0000000", "timeZone": "UTC" },
              "location": { "displayName": "Room 4" }
            }
            """))!.Value;

        Assert.Equal("Standup", item.Title);
        Assert.Equal("Room 4", item.Location);
        Assert.False(item.AllDay);
        Assert.False(item.Floating);
        Assert.Equal(new DateTime(2026, 10, 6, 7, 0, 0, DateTimeKind.Utc), item.Start);
        Assert.Equal(DateTimeKind.Utc, item.Start.Kind);
        Assert.Equal(new DateTime(2026, 10, 6, 7, 15, 0, DateTimeKind.Utc), item.End);
    }

    [Fact]
    public void AnAllDayEventIsItsDatesWhateverZoneItCameIn()
    {
        var item = MicrosoftReader.Event(Json("""
            {
              "subject": "Holiday",
              "isAllDay": true,
              "start": { "dateTime": "2026-10-06T00:00:00.0000000", "timeZone": "UTC" },
              "end": { "dateTime": "2026-10-08T00:00:00.0000000", "timeZone": "UTC" },
              "location": { "displayName": "" }
            }
            """))!.Value;

        Assert.True(item.AllDay);
        Assert.Null(item.Location);
        Assert.Equal(new DateTime(2026, 10, 6), item.Start);
        Assert.Equal(DateTimeKind.Unspecified, item.Start.Kind);
        Assert.Equal(new DateTime(2026, 10, 8), item.End);
    }

    [Fact]
    public void ACancelledOccurrenceIsNotDrawnAndAnUntitledOneIsSaidSo()
    {
        Assert.Null(MicrosoftReader.Event(Json("""
            { "subject": "Gone", "isCancelled": true, "start": { "dateTime": "2026-10-06T07:00:00" }, "end": { "dateTime": "2026-10-06T08:00:00" } }
            """)));
        Assert.Null(MicrosoftReader.Event(Json("""{ "subject": "No start" }""")));
        Assert.Equal(S.DockCalendarNoTitle, MicrosoftReader.Event(Json("""
            { "subject": " ", "start": { "dateTime": "2026-10-06T07:00:00" }, "end": { "dateTime": "2026-10-06T06:00:00" } }
            """))!.Value.Title);
    }

    [Fact]
    public void AnEndBeforeItsStartIsAPointInTime()
    {
        var item = MicrosoftReader.Event(Json("""
            { "subject": "x", "start": { "dateTime": "2026-10-06T07:00:00" }, "end": { "dateTime": "2026-10-06T06:00:00" } }
            """))!.Value;
        Assert.Equal(item.Start, item.End);
    }

    [Theory]
    [InlineData("""{ "id": "c", "name": "Work", "color": "lightRed", "hexColor": "#4f6bed" }""", "#4f6bed")]
    [InlineData("""{ "id": "c", "name": "Work", "color": "lightTeal", "hexColor": "" }""", "#038387")]
    [InlineData("""{ "id": "c", "name": "Work", "color": "auto", "hexColor": "" }""", null)]
    public void ACalendarsColourIsItsHexElseItsNamedColourElseNone(string json, string? colour)
    {
        var calendar = MicrosoftReader.Calendar(Json(json))!;
        Assert.Equal("Work", calendar.Name);
        Assert.Equal(colour, calendar.Colour);
    }

    [Fact]
    public void AContactHasItsAddressesAndItsNumbersMobileFirst()
    {
        var person = MicrosoftReader.Contact(Json("""
            {
              "id": "AAMk",
              "displayName": "Beat Hagenbuch",
              "companyName": "Air-Glacier SA",
              "emailAddresses": [ { "name": "Beat", "address": "beat@example.ch" }, { "address": "" } ],
              "businessPhones": [ "+41 27 329 14 15" ],
              "homePhones": [],
              "mobilePhone": "079 123 45 67"
            }
            """))!;

        Assert.Equal("Beat Hagenbuch", person.DisplayName);
        Assert.Equal("Air-Glacier SA", person.Organization);
        Assert.Equal(["beat@example.ch"], person.Emails);
        Assert.Equal(["079 123 45 67", "+41 27 329 14 15"], person.Phones);
    }

    [Fact]
    public void AContactWithoutANameIsCalledByItsAddress()
    {
        var person = MicrosoftReader.Contact(Json("""
            { "id": "x", "displayName": "", "emailAddresses": [ { "address": "who@example.com" } ] }
            """))!;
        Assert.Equal("who@example.com", person.DisplayName);
        Assert.Null(MicrosoftReader.Contact(Json("""{ "displayName": "No id" }""")));
    }

    [Fact]
    public void AMessageIsWhoFromWhatAndWhenAndWhetherItIsUnread()
    {
        var message = MicrosoftReader.Message(Json("""
            {
              "id": "AAMkAD",
              "subject": "Lunch?",
              "isRead": false,
              "bodyPreview": "  Are you free at noon?  ",
              "receivedDateTime": "2026-09-26T10:42:00Z",
              "from": { "emailAddress": { "name": "Sandra Gerber", "address": "sandra@example.ch" } }
            }
            """))!;

        Assert.Equal("Sandra Gerber", message.From);
        Assert.Equal("sandra@example.ch", message.FromAddress);
        Assert.Equal("Lunch?", message.Subject);
        Assert.Equal("Are you free at noon?", message.Preview);
        Assert.True(message.Unread);
        Assert.Equal(new DateTimeOffset(2026, 9, 26, 10, 42, 0, TimeSpan.Zero), message.Received);
    }

    /// <summary>An event opens where Graph's webLink says, its page in Outlook on the web (§434); only an https link is kept.</summary>
    [Fact]
    public void AnEventOpensAtItsWebLink()
    {
        const string Link = "https://outlook.office365.com/owa/?itemid=AAMkAGI2%3D&exvsurl=1&path=/calendar/item";
        const string Times = """
            "start": { "dateTime": "2026-10-06T07:00:00.0000000", "timeZone": "UTC" },
            "end": { "dateTime": "2026-10-06T07:15:00.0000000", "timeZone": "UTC" }
            """;
        Assert.Equal(Link, MicrosoftReader.Event(Json($$"""{ "subject": "Standup", {{Times}}, "webLink": "{{Link}}" }"""))!.Value.Link);
        Assert.Null(MicrosoftReader.Event(Json($$"""{ "subject": "Standup", {{Times}}, "webLink": "javascript:alert(1)" }"""))!.Value.Link);
        Assert.Null(MicrosoftReader.Event(Json($$"""{ "subject": "Standup", {{Times}} }"""))!.Value.Link);
        // Asked for: calendarView sends only what $select names.
        Assert.Contains("webLink", MicrosoftReader.EventFields.Split(','));
    }

    /// <summary>A message opens where Graph's webLink says, in Outlook on the web; only an https link is kept.</summary>
    [Fact]
    public void AMessageOpensAtItsWebLink()
    {
        const string Link = "https://outlook.live.com/owa/?ItemID=AAMkAD%3D&exvsurl=1&viewmodel=ReadMessageItem";
        var message = MicrosoftReader.Message(Json($$"""
            { "id": "m", "subject": "Hi", "isRead": true, "webLink": "{{Link}}" }
            """))!;
        Assert.Equal(Link, message.Link);
        Assert.Null(MicrosoftReader.Message(Json("""{ "id": "m", "webLink": "javascript:alert(1)" }"""))!.Link);
        Assert.Null(MicrosoftReader.Message(Json("""{ "id": "m", "webLink": "http://outlook.live.com/owa/" }"""))!.Link);
        Assert.Null(MicrosoftReader.Message(Json("""{ "id": "m" }"""))!.Link);
    }

    [Fact]
    public void AMessageWithoutASendersNameOrASubjectSaysSo()
    {
        var message = MicrosoftReader.Message(Json("""
            { "id": "m", "subject": "", "isRead": true, "from": { "emailAddress": { "address": "noreply@example.com" } } }
            """))!;
        Assert.Equal("noreply@example.com", message.From);
        Assert.Equal(S.CoreAccountsNoSubject, message.Subject);
        Assert.False(message.Unread);
        Assert.Null(message.Preview);
        // In the language the person reads Karpo in, as every word it shows.
        Assert.Equal(S.CoreAccountsUnknownSender, MicrosoftReader.Message(Json("""{ "id": "m" }"""))!.From);
        Assert.Equal(S.CoreAccountsNoName, MicrosoftReader.Contact(Json("""{ "id": "x" }"""))!.DisplayName);
    }

    // --- All-day events -------------------------------------------------------------------------------

    /// <summary>
    /// An all-day event of 2 November as Graph sends it under Prefer: outlook.timezone="UTC" for a mailbox that
    /// carries it to UTC: its midnight in the zone it was made in, as UTC. Taking that UTC time's date put an
    /// event of a zone east of UTC a day early (UTC+2: 1 November 22:00); west of UTC (UTC−8: 2 November 08:00)
    /// it happened to be right. Each is the 2nd now, and to the 3rd, by the zone it names or, where it names none,
    /// by the nearest midnight.
    /// </summary>
    [Theory]
    [InlineData("2026-11-01T22:00:00.0000000", "2026-11-02T22:00:00.0000000", "South Africa Standard Time")]
    [InlineData("2026-11-01T22:00:00.0000000", "2026-11-02T22:00:00.0000000", null)]
    [InlineData("2026-11-02T08:00:00.0000000", "2026-11-03T08:00:00.0000000", "Pacific Standard Time")]
    [InlineData("2026-11-02T08:00:00.0000000", "2026-11-03T08:00:00.0000000", null)]
    [InlineData("2026-11-02T00:00:00.0000000", "2026-11-03T00:00:00.0000000", "Pacific Standard Time")]
    [InlineData("2026-11-01T11:00:00.0000000", "2026-11-02T11:00:00.0000000", "Tonga Standard Time")]
    public void AnAllDayEventIsItsOwnDayInEveryZone(string start, string end, string? zone)
    {
        var made = zone is null ? "" : $$""", "originalStartTimeZone": "{{zone}}", "originalEndTimeZone": "{{zone}}" """;
        var item = MicrosoftReader.Event(Json($$"""
            {
              "subject": "Holiday",
              "isAllDay": true,
              "start": { "dateTime": "{{start}}", "timeZone": "UTC" },
              "end": { "dateTime": "{{end}}", "timeZone": "UTC" }{{made}}
            }
            """))!.Value;

        Assert.True(item.AllDay);
        Assert.Equal(new DateTime(2026, 11, 2), item.Start);
        Assert.Equal(new DateTime(2026, 11, 3), item.End);
        Assert.Contains("originalStartTimeZone", MicrosoftReader.EventFields.Split(','));
    }

    // --- A calendar or a folder that alone fails --------------------------------------------------------

    /// <summary>Graph as a test wants it: each URL answered with a status and a body from the table.</summary>
    private sealed class Server(Func<string, (HttpStatusCode Status, string Body)> answer) : HttpMessageHandler
    {
        public List<string> Asked { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = Uri.UnescapeDataString(request.RequestUri!.ToString());
            lock (Asked)
            {
                Asked.Add(url);
            }
            var (status, body) = answer(url);
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }

    private static MicrosoftReader Reader(Server server) =>
        new(new Account { Id = "a", Kind = AccountKind.Microsoft, Address = "me@example.com" }, new Graph(new HttpClient(server), _ => Task.FromResult("token")));

    private const string Standup = """{ "value": [ { "subject": "Standup", "start": { "dateTime": "2026-11-02T07:00:00" }, "end": { "dateTime": "2026-11-02T07:15:00" } } ] }""";

    private const string Denied = """{ "error": { "code": "ErrorAccessDenied", "message": "Access is denied. Check credentials and try again." } }""";

    /// <summary>
    /// The second of three calendars is one shared with the person and taken back: Graph says 403 for its
    /// calendarView. It fails alone — listed, with why — and the first and third are read; once its 403 failed
    /// the whole account's read, which then never refreshed.
    /// </summary>
    [Fact]
    public async Task ACalendarGraphRefusesFailsAloneAndTheOthersAreRead()
    {
        var server = new Server(url => url switch
        {
            _ when url.Contains("/me/calendars?") => (HttpStatusCode.OK, """{ "value": [ { "id": "one", "name": "Work" }, { "id": "two", "name": "Shared" }, { "id": "three", "name": "Home" } ] }"""),
            _ when url.Contains("/calendars/two/") => (HttpStatusCode.Forbidden, Denied),
            _ => (HttpStatusCode.OK, Standup),
        });

        var reads = await Reader(server).CalendarsAsync(new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc), CancellationToken.None);

        Assert.Equal(["one", "two", "three"], reads.Select(read => read.Calendar.Id));
        Assert.Equal([1, 0, 1], reads.Select(read => read.Occurrences.Count));
        Assert.Null(reads[0].Failed);
        Assert.Contains("Access is denied", reads[1].Failed);
        Assert.Null(reads[2].Failed);
    }

    /// <summary>Every calendar refused: that is the account failing, as before, so it is shown stale and tried again.</summary>
    [Fact]
    public async Task EveryCalendarRefusedIsTheAccountFailing()
    {
        var server = new Server(url => url.Contains("/me/calendars?")
            ? (HttpStatusCode.OK, """{ "value": [ { "id": "one", "name": "Work" }, { "id": "two", "name": "Shared" } ] }""")
            : (HttpStatusCode.InternalServerError, """{ "error": { "code": "InternalServerError", "message": "Down." } }"""));

        await Assert.ThrowsAsync<AccountException>(() => Reader(server).CalendarsAsync(DateTime.UtcNow, DateTime.UtcNow.AddDays(1), CancellationToken.None));
    }

    /// <summary>A contact folder Graph will not give (404, gone since it was listed) fails alone; the default and the other folder are read.</summary>
    [Fact]
    public async Task AContactFolderGraphRefusesFailsAlone()
    {
        const string Person = """{ "value": [ { "id": "p", "displayName": "Ruth Imboden" } ] }""";
        var server = new Server(url => url switch
        {
            _ when url.Contains("/me/contactFolders?") => (HttpStatusCode.OK, """{ "value": [ { "id": "f1", "displayName": "Family" }, { "id": "f2", "displayName": "Club" } ] }"""),
            _ when url.Contains("/contactFolders/f1/") => (HttpStatusCode.NotFound, """{ "error": { "code": "ErrorItemNotFound", "message": "The specified object was not found in the store." } }"""),
            _ => (HttpStatusCode.OK, Person),
        });

        var books = await Reader(server).ContactsAsync(CancellationToken.None);

        Assert.Equal([MicrosoftReader.DefaultBook, "f1", "f2"], books.Select(book => book.Book.Id));
        Assert.Equal([1, 0, 1], books.Select(book => book.People.Count));
        Assert.Contains("not found", books[1].Failed);
        Assert.Null(books[2].Failed);
    }

    /// <summary>A calendar with more than a read takes (Graph's pages go on past the most): what was read, said to be incomplete.</summary>
    [Fact]
    public async Task ACalendarLongerThanTheMostIsSaidToBeIncomplete()
    {
        var server = new Server(url => url switch
        {
            _ when url.Contains("/me/calendars?") => (HttpStatusCode.OK, """{ "value": [ { "id": "one", "name": "Work" } ] }"""),
            _ => (HttpStatusCode.OK, """{ "value": [ { "subject": "Standup", "start": { "dateTime": "2026-11-02T07:00:00" }, "end": { "dateTime": "2026-11-02T07:15:00" } } ], "@odata.nextLink": "https://graph.microsoft.com/v1.0/next" }"""),
        });
        var graph = new Graph(new HttpClient(server), _ => Task.FromResult("token"));

        var pages = await graph.AllAsync("https://graph.microsoft.com/v1.0/me/calendars/one/calendarView", CancellationToken.None, most: 3);

        Assert.Equal(3, pages.Count);
        Assert.True(pages.More);
        var short1 = await graph.AllAsync("https://graph.microsoft.com/v1.0/me/calendars?x", CancellationToken.None);
        Assert.False(short1.More);
    }

    // --- A contact's picture (docs\widgets.md §457) -------------------------------------------------------

    /// <summary>
    /// A contact's picture's version is the contact's changeKey, which the read asks for; a contact Graph
    /// gives none of stands under its id. Every contact may have a picture: Graph does not say.
    /// </summary>
    [Fact]
    public void AContactsPhotoVersionIsItsChangeKey()
    {
        Assert.Equal("EQAAABYAAAA", MicrosoftReader.Contact(Json("""{ "id": "p", "displayName": "Ruth", "changeKey": "EQAAABYAAAA" }"""))!.Photo);
        Assert.Equal("p", MicrosoftReader.Contact(Json("""{ "id": "p", "displayName": "Ruth" }"""))!.Photo);
    }

    /// <summary>
    /// A contact's picture is its photo's $value, under the read's own token and Contacts.Read: its bytes as
    /// they come, and none where Graph has none (404); a refusal is a refusal, tried again later.
    /// </summary>
    [Fact]
    public async Task AContactsPhotoIsItsValueAndNoneWhereGraphHasNone()
    {
        var server = new Server(url => url switch
        {
            _ when url.Contains("/me/contacts?") => (HttpStatusCode.OK, """{ "value": [ { "id": "p=1", "displayName": "Ruth Imboden", "changeKey": "k1" } ] }"""),
            _ when url.Contains("/me/contactFolders?") => (HttpStatusCode.OK, """{ "value": [] }"""),
            _ when url.EndsWith("/me/contacts/p=1/photo/$value", StringComparison.Ordinal) => (HttpStatusCode.OK, "JFIF picture"),
            _ when url.Contains("/refused/") => (HttpStatusCode.Forbidden, Denied),
            _ => (HttpStatusCode.NotFound, """{ "error": { "code": "ImageNotFound", "message": "The photo wasn't found." } }"""),
        });
        var reader = Reader(server);
        var ruth = (await reader.ContactsAsync(CancellationToken.None))[0].People.Single();

        Assert.Equal("k1", ruth.Photo);
        Assert.Equal(Encoding.UTF8.GetBytes("JFIF picture"), await reader.PhotoAsync(ruth, CancellationToken.None));
        Assert.Null(await reader.PhotoAsync(ruth with { Id = "nobody" }, CancellationToken.None));
        await Assert.ThrowsAsync<AccountException>(() => reader.PhotoAsync(ruth with { Id = "refused/x" }, CancellationToken.None));
        Assert.Contains(server.Asked, url => url.Contains("changeKey", StringComparison.Ordinal) && url.Contains("/me/contacts?", StringComparison.Ordinal));
    }
}
