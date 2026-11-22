// A Google account read through Google's APIs (Karpo.Accounts\Google\GoogleReader.cs, GoogleMail.cs):
// Calendar's events as occurrences — a day, an instant in UTC, cancelled, free, Meet's link —, its pages,
// a calendar refused alone and one turned off not asked for; People's contacts and their pictures (Google's
// drawn letter none); Gmail's labels as folders — named, in order, the categories left out —, the inbox
// off, a label turned on, unread; and a kind the person never granted, or a rate limit, as Google says them.

using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Karpo.Accounts;
using Karpo.Accounts.Google;

namespace Karpo.Tests.Accounts;

public sealed class GoogleReaderTests
{
    private static JsonObject Json(string text) => (JsonObject)JsonNode.Parse(text)!;

    private static readonly DateTime From = new(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);

    private static readonly DateTime To = new(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>Google, answering each request by its address (unescaped) and keeping what it was asked, with its token.</summary>
    internal sealed class Server(Func<string, (HttpStatusCode Status, string Body)> answer) : HttpMessageHandler
    {
        public List<(string Method, string Url, string? Bearer, string Body)> Asked { get; } = [];

        public IEnumerable<string> Urls => Asked.Select(asked => asked.Url);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = Uri.UnescapeDataString(request.RequestUri!.ToString());
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            lock (Asked)
            {
                Asked.Add((request.Method.Method, url, request.Headers.Authorization?.Parameter, body));
            }
            var (status, text) = answer(url);
            var response = new HttpResponseMessage(status) { Content = new StringContent(text, Encoding.UTF8, "application/json") };
            if (status == HttpStatusCode.TooManyRequests || status == HttpStatusCode.Forbidden)
            {
                response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.Zero);
            }
            return response;
        }
    }

    internal static GoogleReader Reader(Server server, Account? account = null, Func<AccountData, Task<string>>? token = null) =>
        new(account ?? new Account { Id = "g", Kind = AccountKind.Google, Address = "ada@gmail.com" }, new HttpClient(server), (kind, _) => token?.Invoke(kind) ?? Task.FromResult("token-" + kind));

    // --- Calendar ---------------------------------------------------------------------------------------

    [Fact]
    public void ATimedEventIsAnInstantInUtc()
    {
        var item = GoogleReader.Event(Json("""
            {
              "status": "confirmed", "summary": "Standup", "location": "Room 4",
              "start": { "dateTime": "2026-11-02T09:00:00+01:00", "timeZone": "Europe/Zurich" },
              "end": { "dateTime": "2026-11-02T09:15:00+01:00", "timeZone": "Europe/Zurich" },
              "htmlLink": "https://www.google.com/calendar/event?eid=abc"
            }
            """))!.Value;

        Assert.Equal(("Standup", "Room 4", false, false), (item.Title, item.Location, item.AllDay, item.Floating));
        Assert.Equal(new DateTime(2026, 11, 2, 8, 0, 0, DateTimeKind.Utc), item.Start);
        Assert.Equal(DateTimeKind.Utc, item.Start.Kind);
        Assert.Equal(new DateTime(2026, 11, 2, 8, 15, 0, DateTimeKind.Utc), item.End);
        Assert.Equal("https://www.google.com/calendar/event?eid=abc", item.Link);
        Assert.False(item.Free);
        Assert.Null(item.Join);
    }

    [Fact]
    public void AnAllDayEventIsItsDatesItsEndTheDayAfter()
    {
        var item = GoogleReader.Event(Json("""{ "summary": "Holiday", "start": { "date": "2026-11-06" }, "end": { "date": "2026-11-08" } }"""))!.Value;

        Assert.True(item.AllDay);
        Assert.Equal(new DateTime(2026, 11, 6), item.Start);
        Assert.Equal(DateTimeKind.Unspecified, item.Start.Kind);
        Assert.Equal(new DateTime(2026, 11, 8), item.End);
        Assert.Null(item.Location);
    }

    [Fact]
    public void CancelledWorkingLocationsAndNoStartAreNoEventsAndAnUntitledOneIsSaidSo()
    {
        Assert.Null(GoogleReader.Event(Json("""{ "status": "cancelled", "summary": "Gone", "start": { "date": "2026-11-06" }, "end": { "date": "2026-11-07" } }""")));
        Assert.Null(GoogleReader.Event(Json("""{ "eventType": "workingLocation", "summary": "Home", "start": { "date": "2026-11-06" }, "end": { "date": "2026-11-07" } }""")));
        Assert.Null(GoogleReader.Event(Json("""{ "summary": "No start" }""")));
        Assert.Equal(S.DockCalendarNoTitle, GoogleReader.Event(Json("""{ "summary": " ", "start": { "date": "2026-11-06" }, "end": { "date": "2026-11-07" } }"""))!.Value.Title);
    }

    [Fact]
    public void DeclinedOrShownAsFreeIsFree()
    {
        const string Times = """ "start": { "dateTime": "2026-11-02T09:00:00Z" }, "end": { "dateTime": "2026-11-02T10:00:00Z" } """;
        Assert.True(GoogleReader.Event(Json($$"""{ "summary": "Lunch", "transparency": "transparent", {{Times}} }"""))!.Value.Free);
        Assert.True(GoogleReader.Event(Json($$"""{ "summary": "Review", "attendees": [ { "email": "bob@x.ch", "responseStatus": "accepted" }, { "self": true, "responseStatus": "declined" } ], {{Times}} }"""))!.Value.Free);
        // Someone else declining takes none of the person's time away.
        Assert.False(GoogleReader.Event(Json($$"""{ "summary": "Review", "attendees": [ { "email": "bob@x.ch", "responseStatus": "declined" }, { "self": true, "responseStatus": "accepted" } ], {{Times}} }"""))!.Value.Free);
    }

    [Fact]
    public void TheMeetingsLinkIsMeetsOwnElseAConferencesVideoElseOneInItsPlaceOrNotes()
    {
        const string Times = """ "start": { "dateTime": "2026-11-02T09:00:00Z" }, "end": { "dateTime": "2026-11-02T10:00:00Z" } """;
        Assert.Equal("https://meet.google.com/abc-defg-hij", GoogleReader.Event(Json($$"""
            { "summary": "Sync", "hangoutLink": "https://meet.google.com/abc-defg-hij",
              "conferenceData": { "entryPoints": [ { "entryPointType": "phone", "uri": "tel:+41-44-000" }, { "entryPointType": "video", "uri": "https://meet.google.com/abc-defg-hij" } ] }, {{Times}} }
            """))!.Value.Join);
        Assert.Equal("https://contoso.zoom.us/j/81234567890", GoogleReader.Event(Json($$"""
            { "summary": "Zoom", "conferenceData": { "entryPoints": [ { "entryPointType": "video", "uri": "https://contoso.zoom.us/j/81234567890" } ] }, {{Times}} }
            """))!.Value.Join);
        Assert.StartsWith("https://teams.microsoft.com/l/meetup-join/", GoogleReader.Event(Json($$"""
            { "summary": "Teams", "description": "Join: https://teams.microsoft.com/l/meetup-join/19%3ameeting_x%40thread.v2/0", {{Times}} }
            """))!.Value.Join);
    }

    [Fact]
    public void ACalendarIsThePersonsNameForItAndItsColour()
    {
        Assert.Equal(new RemoteCalendar("ada@gmail.com", "Mine", "#9FE1E7"), GoogleReader.Calendar(Json("""{ "id": "ada@gmail.com", "summary": "ada@gmail.com", "summaryOverride": "Mine", "backgroundColor": "#9fe1e7" }""")));
        Assert.Equal("Holidays in Switzerland", GoogleReader.Calendar(Json("""{ "id": "h", "summary": "Holidays in Switzerland" }"""))!.Name);
        Assert.Equal(S.CoreAccountsUnnamedCalendar, GoogleReader.Calendar(Json("""{ "id": "x" }"""))!.Name);
        Assert.Null(GoogleReader.Calendar(Json("""{ "id": "gone", "deleted": true }""")));
        Assert.Null(GoogleReader.Calendar(Json("""{ "summary": "No id" }""")));
    }

    /// <summary>
    /// Three calendars: the second refused (shared and taken back) fails alone, the third turned off is listed and its events
    /// never asked for; the first's events come in two pages, the second asked for with the first's page token.
    /// </summary>
    [Fact]
    public async Task CalendarsArePagedOneFailsAloneAndOneOffIsNotAsked()
    {
        var account = new Account { Id = "g", Kind = AccountKind.Google, Address = "ada@gmail.com" }.Synced(SyncKeys.Calendar("home"), false);
        var server = new Server(url => url switch
        {
            _ when url.Contains("/users/me/calendarList") => (HttpStatusCode.OK, """{ "items": [ { "id": "work", "summary": "Work" }, { "id": "shared", "summary": "Shared" }, { "id": "home", "summary": "Home" } ] }"""),
            _ when url.Contains("/calendars/shared/") => (HttpStatusCode.NotFound, """{ "error": { "code": 404, "message": "Not Found", "status": "NOT_FOUND" } }"""),
            _ when url.Contains("pageToken=P2") => (HttpStatusCode.OK, """{ "items": [ { "summary": "Two", "start": { "date": "2026-11-07" }, "end": { "date": "2026-11-08" } } ] }"""),
            _ => (HttpStatusCode.OK, """{ "items": [ { "summary": "One", "start": { "date": "2026-11-06" }, "end": { "date": "2026-11-07" } } ], "nextPageToken": "P2" }"""),
        });

        var reads = await Reader(server, account).CalendarsAsync(From, To, CancellationToken.None);

        Assert.Equal(["work", "shared", "home"], reads.Select(read => read.Calendar.Id));
        Assert.Equal(["One", "Two"], reads[0].Occurrences.Select(occurrence => occurrence.Title));
        Assert.False(reads[0].Incomplete);
        Assert.Contains("Not Found", reads[1].Failed);
        Assert.True(reads[2].Off);
        Assert.DoesNotContain(server.Urls, url => url.Contains("/calendars/home/"));
        var events = server.Urls.First(url => url.Contains("/calendars/work/events"));
        Assert.Contains("singleEvents=true", events);
        Assert.Contains("orderBy=startTime", events);
        Assert.Contains("timeMin=2026-11-01T00:00:00Z", events);
        Assert.Contains("timeMax=2026-12-01T00:00:00Z", events);
        // The calendars' token, never another kind's.
        Assert.All(server.Asked, asked => Assert.Equal("token-Calendars", asked.Bearer));
    }

    [Fact]
    public async Task EveryCalendarRefusedIsTheAccountFailing()
    {
        var server = new Server(url => url.Contains("/users/me/calendarList")
            ? (HttpStatusCode.OK, """{ "items": [ { "id": "one", "summary": "Work" } ] }""")
            : (HttpStatusCode.InternalServerError, """{ "error": { "code": 500, "message": "Backend Error" } }"""));

        await Assert.ThrowsAsync<AccountException>(() => Reader(server).CalendarsAsync(From, To, CancellationToken.None));
    }

    /// <summary>
    /// Google's 403 for a token without the kind's scope is the person's consent missing for that kind — a ConsentException,
    /// not a refusal — and its 403 for a rate is a throttle, waited out and asked again.
    /// </summary>
    [Fact]
    public async Task A403ForAScopeIsConsentAndA403ForARateIsWaitedOut()
    {
        const string Scope = """{ "error": { "code": 403, "message": "Request had insufficient authentication scopes.", "status": "PERMISSION_DENIED", "details": [ { "@type": "type.googleapis.com/google.rpc.ErrorInfo", "reason": "ACCESS_TOKEN_SCOPE_INSUFFICIENT" } ] } }""";
        var missing = await Assert.ThrowsAsync<ConsentException>(() => Reader(new Server(_ => (HttpStatusCode.Forbidden, Scope))).CalendarsAsync(From, To, CancellationToken.None));
        Assert.Equal(AccountData.Calendars, missing.Kind);

        const string Older = """{ "error": { "code": 403, "message": "Insufficient Permission", "errors": [ { "domain": "global", "reason": "insufficientPermissions" } ] } }""";
        Assert.Equal(AccountData.Mail, (await Assert.ThrowsAsync<ConsentException>(() => Reader(new Server(_ => (HttpStatusCode.Forbidden, Older))).MailAsync(30, CancellationToken.None))).Kind);

        var tries = 0;
        var server = new Server(url =>
        {
            if (url.Contains("/users/me/calendarList") && ++tries == 1)
            {
                return (HttpStatusCode.Forbidden, """{ "error": { "code": 403, "message": "Rate Limit Exceeded", "errors": [ { "reason": "rateLimitExceeded" } ] } }""");
            }
            return (HttpStatusCode.OK, """{ "items": [] }""");
        });
        Assert.Empty(await Reader(server).CalendarsAsync(From, To, CancellationToken.None));
        Assert.Equal(2, tries);
    }

    /// <summary>A kind whose token is refused for consent fails that kind alone: the mail is read while the calendars are not.</summary>
    [Fact]
    public async Task AKindNotGrantedFailsAloneAndTheOthersAreRead()
    {
        var server = new Server(url => url switch
        {
            _ when url.EndsWith("/labels") => (HttpStatusCode.OK, """{ "labels": [ { "id": "INBOX", "name": "INBOX", "type": "system" } ] }"""),
            _ when url.Contains("/labels/INBOX") => (HttpStatusCode.OK, """{ "id": "INBOX", "messagesTotal": 0, "messagesUnread": 0 }"""),
            _ => (HttpStatusCode.OK, """{ "resultSizeEstimate": 0 }"""),
        });
        var reader = Reader(server, token: kind => kind == AccountData.Calendars
            ? Task.FromException<string>(new ConsentException(AccountKind.Google, AccountData.Calendars))
            : Task.FromResult("token"));

        Assert.Equal(AccountData.Calendars, (await Assert.ThrowsAsync<ConsentException>(() => reader.CalendarsAsync(From, To, CancellationToken.None))).Kind);
        var mail = await reader.MailAsync(30, CancellationToken.None);
        Assert.True(mail[0].Folder.Inbox);
        Assert.DoesNotContain(server.Urls, url => url.Contains("calendar"));
    }

    // --- Contacts ---------------------------------------------------------------------------------------

    [Fact]
    public void APersonIsTheirPrimaryNameAddressesNumbersAndOrganization()
    {
        var person = GoogleReader.Contact(Json("""
            {
              "resourceName": "people/c1",
              "names": [ { "displayName": "Ada L.", "metadata": { "primary": false } }, { "displayName": "Ada Lovelace", "metadata": { "primary": true } } ],
              "emailAddresses": [ { "value": "ada@example.com" }, { "value": " " } ],
              "phoneNumbers": [ { "value": "+41 44 000 00 00" } ],
              "organizations": [ { "name": "Analytical Engines" } ],
              "photos": [ { "url": "https://lh3.googleusercontent.com/a/letter=s100", "default": true, "metadata": { "primary": true } } ]
            }
            """))!;

        Assert.Equal(("people/c1", "Ada Lovelace", "Analytical Engines"), (person.Id, person.DisplayName, person.Organization));
        Assert.Equal(["ada@example.com"], person.Emails);
        Assert.Equal(["+41 44 000 00 00"], person.Phones);
        // Google's drawn letter is no picture of theirs.
        Assert.Null(person.Photo);

        var named = GoogleReader.Contact(Json("""{ "resourceName": "people/c2", "emailAddresses": [ { "value": "bob@example.com" } ] }"""))!;
        Assert.Equal("bob@example.com", named.DisplayName);
        Assert.Equal(S.CoreAccountsNoName, GoogleReader.Contact(Json("""{ "resourceName": "people/c3" }"""))!.DisplayName);
        Assert.Null(GoogleReader.Contact(Json("""{ "names": [ { "displayName": "No resource" } ] }""")));
    }

    [Fact]
    public void APicturesVersionIsItsAddressAndANewPictureMovesIt()
    {
        static string? Version(string url) => GoogleReader.Contact(Json($$"""{ "resourceName": "people/c1", "photos": [ { "url": "{{url}}" } ] }"""))!.Photo;

        Assert.NotNull(Version("https://lh3.googleusercontent.com/contacts/one=s100"));
        Assert.Equal(Version("https://lh3.googleusercontent.com/contacts/one=s100"), Version("https://lh3.googleusercontent.com/contacts/one=s100"));
        Assert.NotEqual(Version("https://lh3.googleusercontent.com/contacts/one=s100"), Version("https://lh3.googleusercontent.com/contacts/two=s100"));
        Assert.Equal("https://lh3.googleusercontent.com/contacts/one=s128", GoogleReader.Sized("https://lh3.googleusercontent.com/contacts/one=s100", 128));
        Assert.Equal("https://lh3.googleusercontent.com/contacts/one=s128", GoogleReader.Sized("https://lh3.googleusercontent.com/contacts/one", 128));
    }

    /// <summary>
    /// The contacts: one book, everyone in the person's contacts, paged; a picture asked of People again and fetched from
    /// Google's photo server with no token; a book turned off asks nothing.
    /// </summary>
    [Fact]
    public async Task TheContactsAreOneBookPagedAndAPictureIsFetchedWithoutTheToken()
    {
        var server = new Server(url => url switch
        {
            _ when url.Contains("pageToken=N") => (HttpStatusCode.OK, """{ "connections": [ { "resourceName": "people/c2", "names": [ { "displayName": "Bob" } ] } ] }"""),
            _ when url.Contains("/people/me/connections") => (HttpStatusCode.OK, """{ "connections": [ { "resourceName": "people/c1", "names": [ { "displayName": "Ada" } ], "photos": [ { "url": "https://lh3.googleusercontent.com/contacts/ada=s100" } ] } ], "nextPageToken": "N" }"""),
            _ when url.Contains("/people/c1?personFields=photos") => (HttpStatusCode.OK, """{ "resourceName": "people/c1", "photos": [ { "url": "https://lh3.googleusercontent.com/contacts/ada=s100" } ] }"""),
            _ when url.StartsWith("https://lh3.googleusercontent.com/") => (HttpStatusCode.OK, "PICTURE"),
            _ => (HttpStatusCode.NotFound, "{}"),
        });
        var reader = Reader(server);

        var books = await reader.ContactsAsync(CancellationToken.None);

        var book = Assert.Single(books);
        Assert.Equal(new AddressBook(GoogleReader.MyContacts, S.CoreAccountsDefaultBook), book.Book);
        Assert.Equal(["Ada", "Bob"], book.People.Select(person => person.DisplayName));
        Assert.Contains("personFields=names,emailAddresses,phoneNumbers,organizations,photos,metadata", server.Urls.First());

        var picture = await reader.PhotoAsync(book.People[0], CancellationToken.None);
        Assert.Equal("PICTURE", Encoding.UTF8.GetString(picture!));
        var fetched = server.Asked.Single(asked => asked.Url.StartsWith("https://lh3.googleusercontent.com/"));
        Assert.Equal("https://lh3.googleusercontent.com/contacts/ada=s128", fetched.Url);
        Assert.Null(fetched.Bearer);
        // Bob has no picture: nothing is asked.
        Assert.Null(await reader.PhotoAsync(book.People[1], CancellationToken.None));

        var off = Reader(server, new Account { Id = "g", Kind = AccountKind.Google, Address = "ada@gmail.com" }.Synced(SyncKeys.Book(GoogleReader.MyContacts), false));
        var asked = server.Asked.Count;
        Assert.True(Assert.Single(await off.ContactsAsync(CancellationToken.None)).Off);
        Assert.Equal(asked, server.Asked.Count);
    }

    // --- Gmail ------------------------------------------------------------------------------------------

    private const string Labels = """
        { "labels": [
          { "id": "CATEGORY_SOCIAL", "name": "CATEGORY_SOCIAL", "type": "system" },
          { "id": "Label_2", "name": "Work/Clients", "type": "user" },
          { "id": "TRASH", "name": "TRASH", "type": "system" },
          { "id": "UNREAD", "name": "UNREAD", "type": "system" },
          { "id": "INBOX", "name": "INBOX", "type": "system" },
          { "id": "CHAT", "name": "CHAT", "type": "system" },
          { "id": "SENT", "name": "SENT", "type": "system" },
          { "id": "Label_1", "name": "Work", "type": "user" },
          { "id": "Label_3", "name": "accounts", "type": "user" },
          { "id": "STARRED", "name": "STARRED", "type": "system" },
          { "id": "IMPORTANT", "name": "IMPORTANT", "type": "system" },
          { "id": "DRAFT", "name": "DRAFT", "type": "system" },
          { "id": "SPAM", "name": "SPAM", "type": "system" }
        ] }
        """;

    [Fact]
    public void LabelsAreFoldersTheInboxFirstThenGmailsOwnThenThePersonsByName()
    {
        var folders = GoogleReader.Folders(Web(Labels));

        Assert.Equal(
            [S.CoreAccountsInbox, S.CoreAccountsGmailSent, S.CoreAccountsGmailDrafts, S.CoreAccountsGmailSpam, S.CoreAccountsGmailTrash,
             S.CoreAccountsGmailStarred, S.CoreAccountsGmailImportant, "accounts", "Work", "Work / Clients"],
            folders.Select(folder => folder.Name));
        Assert.Equal(["INBOX", "SENT", "DRAFT", "SPAM", "TRASH", "STARRED", "IMPORTANT", "Label_3", "Label_1", "Label_2"], folders.Select(folder => folder.Id));
        Assert.True(folders[0].Inbox);
        Assert.All(folders.Skip(1), folder => Assert.False(folder.Inbox));
    }

    private static IReadOnlyList<JsonObject> Web(string labels) => [.. Json(labels)["labels"]!.AsArray().OfType<JsonObject>()];

    [Fact]
    public void AMessageIsItsHeadersItsSnippetAndWhetherItWearsUnread()
    {
        var message = GoogleReader.Message(Json("""
            {
              "id": "18c", "labelIds": [ "INBOX", "UNREAD", "CATEGORY_PERSONAL" ], "internalDate": "1762070400000",
              "snippet": "Here&#39;s the plan &amp; more",
              "payload": { "headers": [
                { "name": "From", "value": "\"Bob Builder\" <bob@example.com>" },
                { "name": "Subject", "value": "=?UTF-8?B?R3LDvGV6aQ==?=" },
                { "name": "Message-ID", "value": "<abc@mail.example.com>" } ] }
            }
            """), "ada@gmail.com")!;

        Assert.Equal(("18c", "Bob Builder", "bob@example.com", "Grüezi"), (message.Id, message.From, message.FromAddress, message.Subject));
        Assert.Equal("Here's the plan & more", message.Preview);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1762070400000), message.Received);
        Assert.True(message.Unread);
        Assert.Equal("https://mail.google.com/mail/u/ada%40gmail.com/#search/rfc822msgid%3Aabc%40mail.example.com", message.Link);

        var bare = GoogleReader.Message(Json("""{ "id": "18d", "labelIds": [ "INBOX" ], "payload": { "headers": [ { "name": "from", "value": "carol@example.com" } ] } }"""), "ada@gmail.com")!;
        Assert.Equal(("carol@example.com", "carol@example.com", S.CoreAccountsNoSubject), (bare.From, bare.FromAddress, bare.Subject));
        Assert.False(bare.Unread);
        Assert.Equal("https://mail.google.com/mail/u/ada%40gmail.com/#inbox", bare.Link);
    }

    /// <summary>
    /// The mail: every label listed, the inbox read — its counts, its newest messages, each by its headers —, a label turned
    /// on read beside it, one not turned on listed with nothing asked, one turned on that Gmail refuses failing alone; the
    /// inbox turned off is listed and not read.
    /// </summary>
    [Fact]
    public async Task TheInboxAndTheLabelsTurnedOnAreReadAndOneRefusedFailsAlone()
    {
        var account = new Account { Id = "g", Kind = AccountKind.Google, Address = "ada@gmail.com" }
            .Synced(SyncKeys.MailFolder("Label_1"), true)
            .Synced(SyncKeys.MailFolder("Label_2"), true);
        var server = new Server(url => url switch
        {
            _ when url.EndsWith("/labels") => (HttpStatusCode.OK, Labels),
            _ when url.Contains("/labels/INBOX") => (HttpStatusCode.OK, """{ "id": "INBOX", "messagesTotal": 1234, "messagesUnread": 12 }"""),
            _ when url.Contains("/labels/Label_1") => (HttpStatusCode.OK, """{ "id": "Label_1", "messagesTotal": 5, "messagesUnread": 0 }"""),
            _ when url.Contains("/labels/Label_2") => (HttpStatusCode.NotFound, """{ "error": { "code": 404, "message": "Requested entity was not found." } }"""),
            _ when url.Contains("/messages?labelIds=INBOX") => (HttpStatusCode.OK, """{ "messages": [ { "id": "m1" }, { "id": "m2" }, { "id": "gone" } ] }"""),
            _ when url.Contains("/messages?labelIds=Label_1") => (HttpStatusCode.OK, """{ "messages": [ { "id": "w1" } ] }"""),
            _ when url.Contains("/messages/gone?") => (HttpStatusCode.NotFound, """{ "error": { "code": 404, "message": "Not Found" } }"""),
            _ when url.Contains("/messages/m1?") => (HttpStatusCode.OK, """{ "id": "m1", "labelIds": [ "INBOX", "UNREAD" ], "internalDate": "1762070400000", "payload": { "headers": [ { "name": "Subject", "value": "Older" } ] } }"""),
            _ when url.Contains("/messages/m2?") => (HttpStatusCode.OK, """{ "id": "m2", "labelIds": [ "INBOX" ], "internalDate": "1762156800000", "payload": { "headers": [ { "name": "Subject", "value": "Newer" } ] } }"""),
            _ => (HttpStatusCode.OK, """{ "id": "w1", "labelIds": [ "Label_1" ], "internalDate": "1762070400000", "payload": { "headers": [ { "name": "Subject", "value": "Work" } ] } }"""),
        });

        var folders = await Reader(server, account).MailAsync(30, CancellationToken.None);

        Assert.Equal(10, folders.Count);
        var inbox = folders[0];
        Assert.Equal((true, 12, 1234), (inbox.Folder.Inbox, inbox.Folder.Unread, inbox.Folder.Total));
        Assert.Equal((12, 1234), (inbox.Mail!.Unread, inbox.Mail.Total));
        // Newest first; one deleted since it was listed is not there to show.
        Assert.Equal(["Newer", "Older"], inbox.Mail.Messages.Select(message => message.Subject));
        Assert.Equal([false, true], inbox.Mail.Messages.Select(message => message.Unread));
        Assert.Equal(["Work"], folders.Single(folder => folder.Folder.Id == "Label_1").Mail!.Messages.Select(message => message.Subject));
        Assert.Contains("not found", folders.Single(folder => folder.Folder.Id == "Label_2").Failed);
        Assert.Null(folders.Single(folder => folder.Folder.Id == "SENT").Mail);
        Assert.DoesNotContain(server.Urls, url => url.Contains("/labels/SENT") || url.Contains("labelIds=SENT"));
        Assert.Contains(server.Urls, url => url.Contains("/messages?labelIds=INBOX&maxResults=30"));
        Assert.Contains(server.Urls, url => url.Contains("/messages/m1?format=metadata&metadataHeaders=From&metadataHeaders=Subject&metadataHeaders=Message-ID"));
        Assert.All(server.Asked, asked => Assert.Equal("token-Mail", asked.Bearer));

        // The inbox off: listed, nothing of it asked; the labels turned on still read.
        var asked = server.Asked.Count;
        var off = await Reader(server, account.Synced(SyncKeys.Inbox, false)).MailAsync(30, CancellationToken.None);
        Assert.True(off[0].Folder.Inbox);
        Assert.Null(off[0].Mail);
        Assert.DoesNotContain(server.Urls.Skip(asked), url => url.Contains("INBOX"));
        Assert.NotNull(off.Single(folder => folder.Folder.Id == "Label_1").Mail);
    }

    [Fact]
    public async Task TheInboxRefusedFailsTheRead()
    {
        var server = new Server(url => url.EndsWith("/labels")
            ? (HttpStatusCode.OK, Labels)
            : (HttpStatusCode.InternalServerError, """{ "error": { "code": 500, "message": "Backend Error" } }"""));

        await Assert.ThrowsAsync<AccountException>(() => Reader(server).MailAsync(30, CancellationToken.None));
    }
}
