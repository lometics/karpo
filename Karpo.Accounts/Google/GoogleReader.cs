// A Google account read through Google's own APIs (docs\accounts.md, "Google"): its calendars through
// Calendar v3, its contacts through People, its mail through Gmail (GoogleMail.cs) and its tasks
// through Tasks, read and written (GoogleTasks.cs). Each kind has its own door (GoogleApi) on the one
// token the account has, refused for a kind the person did not grant (GoogleAuth.TokenAsync), so a
// kind left unticked on Google's consent screen fails alone and the others are read as ever.
//
// Calendar v3's events, asked for one by one (singleEvents), come as a series' occurrences with their
// exceptions applied, as Graph's calendarView does, in [timeMin, timeMax): an event's own start and
// end with their offsets, read here as UTC, or an all-day event's dates (its end the day after, as an
// Occurrence's end is). One cancelled is not drawn, and a working location (Google's "Home", "Office")
// is no event at all. One the person declined, or shows as free (transparency), is free; its meeting's
// link is Meet's own (hangoutLink), a conference's video entry point, else one in its place or its notes.
//
// The contacts are one book, "Contacts" — Google's myContacts, everyone in the person's contacts. A
// person's picture is the non-default one of theirs (Google's default is a drawn letter, which the
// contacts line draws itself): its version is a short hash of its address, and it is fetched from that
// address as nobody — the address is Google's photo server, never the API, and the token never goes there.

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace Karpo.Accounts.Google;

internal sealed partial class GoogleReader : IAccountReader, ITaskReader
{
    /// <summary>The one address book: Google's contact group of everyone in the person's contacts.</summary>
    internal const string MyContacts = "myContacts";

    /// <summary>What People is asked for of each person: all <see cref="Contact"/> reads.</summary>
    internal const string PersonFields = "names,emailAddresses,phoneNumbers,organizations,photos,metadata";

    /// <summary>What Calendar is asked for of each event: all <see cref="Event"/> reads.</summary>
    internal const string EventFields = "nextPageToken,items(status,eventType,summary,location,description,start,end,transparency,attendees(self,responseStatus),hangoutLink,conferenceData(entryPoints(entryPointType,uri)),htmlLink)";

    /// <summary>The size a contact's picture is asked for at: twice what the line shows, cut down to 64 px when kept.</summary>
    private const int PhotoSize = 128;

    private readonly HttpClient http;
    private readonly GoogleApi calendar;
    private readonly GoogleApi people;
    private readonly GoogleApi gmail;
    private readonly GoogleApi tasks;

    public GoogleReader(Account account)
        : this(account, Web.Http, (kind, cancellation) => GoogleAuth.Shared.TokenAsync(account, kind, cancellation))
    {
    }

    /// <summary>Without Google: <paramref name="http"/> answers every API and <paramref name="token"/> is each kind's token.</summary>
    internal GoogleReader(Account account, HttpClient http, Func<AccountData, CancellationToken, Task<string>> token)
    {
        Account = account;
        this.http = http;
        calendar = Api(AccountData.Calendars);
        people = Api(AccountData.Contacts);
        gmail = Api(AccountData.Mail);
        tasks = Api(AccountData.Tasks);

        GoogleApi Api(AccountData kind) => new(http, cancellation => token(kind, cancellation), kind);
    }

    public Account Account { get; }

    public async Task<IReadOnlyList<CalendarRead>> CalendarsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellation)
    {
        var listed = await calendar.AllAsync(
            $"{GoogleApi.Calendar}/users/me/calendarList?maxResults=250&fields={Uri.EscapeDataString("nextPageToken,items(id,summary,summaryOverride,backgroundColor,deleted)")}",
            "items", cancellation, most: 1000).ConfigureAwait(false);

        var found = new List<CalendarRead>();
        AccountException? first = null;
        foreach (var item in listed)
        {
            if (Calendar(item) is not { } each)
            {
                continue;
            }
            if (!Account.Syncs(SyncKeys.Calendar(each.Id)))
            {
                found.Add(new CalendarRead(each, [], [], Off: true));
                continue;
            }
            var url = $"{GoogleApi.Calendar}/calendars/{Uri.EscapeDataString(each.Id)}/events?singleEvents=true&orderBy=startTime"
                + $"&timeMin={Stamp(fromUtc)}&timeMax={Stamp(toUtc)}&maxResults=2500&fields={Uri.EscapeDataString(EventFields)}";
            try
            {
                var events = await calendar.AllAsync(url, "items", cancellation).ConfigureAwait(false);
                found.Add(new CalendarRead(each, [.. events.Select(Event).OfType<Occurrence>()], []) { Incomplete = events.More });
            }
            catch (AccountException refused)
            {
                // A calendar shared and taken back (403, 404), a server error for it: it fails alone (Parts).
                first ??= refused;
                found.Add(new CalendarRead(each, [], []) { Failed = refused.Message });
            }
        }
        Parts.Check(found, read => read.Off, read => read.Failed is not null, first);
        return found;
    }

    public async Task<IReadOnlyList<BookRead>> ContactsAsync(CancellationToken cancellation)
    {
        var book = new AddressBook(MyContacts, S.CoreAccountsDefaultBook);
        if (!Account.Syncs(SyncKeys.Book(book.Id)))
        {
            return [new BookRead(book, [], Off: true)];
        }
        // The one book: People refusing it is the account's contacts failing, as every part failing is (Parts).
        var found = await people.AllAsync(
            $"{GoogleApi.People}/people/me/connections?personFields={PersonFields}&pageSize=1000", "connections", cancellation).ConfigureAwait(false);
        return [new BookRead(book, [.. found.Select(Contact).OfType<Person>().DistinctBy(person => person.Id)]) { Incomplete = found.More }];
    }

    /// <summary>
    /// <paramref name="person"/>'s picture: their address at Google's photo server asked of People again (a reader keeps
    /// nothing), then fetched from there as nobody; null where they have none, or are gone.
    /// </summary>
    public async Task<byte[]?> PhotoAsync(Person person, CancellationToken cancellation)
    {
        if (person.Photo is null || !person.Id.StartsWith("people/", StringComparison.Ordinal))
        {
            return null;
        }
        JsonObject found;
        try
        {
            found = await people.GetAsync($"{GoogleApi.People}/people/{Uri.EscapeDataString(person.Id["people/".Length..])}?personFields=photos", cancellation).ConfigureAwait(false);
        }
        catch (AccountException gone) when (gone.Status == 404)
        {
            return null;
        }
        if (MailLinks.Web(PhotoUrl(found)) is not { } url)
        {
            return null;
        }
        using var response = await http.GetAsync(Sized(url, PhotoSize), cancellation).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        return response.IsSuccessStatusCode
            ? await response.Content.ReadAsByteArrayAsync(cancellation).ConfigureAwait(false)
            : throw new AccountException(S.CoreAccountsOauthAnswered(provider: "Google", answer: ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture)), (int)response.StatusCode);
    }

    /// <summary>One calendar as calendarList lists it; null for one deleted, or without an id.</summary>
    internal static RemoteCalendar? Calendar(JsonObject item)
    {
        if (Web.Bool(item["deleted"]) == true || Web.Str(item["id"]) is not { Length: > 0 } id)
        {
            return null;
        }
        // The person's own name for it, where they gave one; the owner's else (the primary one's is the address).
        var name = Web.Str(item["summaryOverride"]) is { Length: > 0 } own ? own : Web.Str(item["summary"]);
        return new RemoteCalendar(id, string.IsNullOrWhiteSpace(name) ? S.CoreAccountsUnnamedCalendar : name.Trim(), SourceColours.Normal(Web.Str(item["backgroundColor"])));
    }

    /// <summary>One occurrence as Calendar's events give it; null for one cancelled, a working location, or one with no start.</summary>
    internal static Occurrence? Event(JsonObject item)
    {
        if (Web.Str(item["status"]) == "cancelled" || Web.Str(item["eventType"]) == "workingLocation")
        {
            return null;
        }
        DateTime start, end;
        bool allDay;
        if (Date(item["start"]) is { } day)
        {
            (allDay, start, end) = (true, day, Date(item["end"]) ?? day.AddDays(1));
        }
        else if (Instant(item["start"]) is { } at)
        {
            (allDay, start, end) = (false, at, Instant(item["end"]) ?? at);
        }
        else
        {
            return null;
        }
        if (end < start)
        {
            end = start;
        }
        var title = Web.Str(item["summary"]);
        var location = Web.Str(item["location"]);
        // Its meeting's link: Meet's own, a conference's (an add-on's: Zoom's, Teams') video entry point, else one in
        // its place or its notes — kept as the link alone, never the notes.
        var video = Web.Items(item["conferenceData"], "entryPoints").FirstOrDefault(point => Web.Str(point["entryPointType"]) == "video");
        var join = MeetingLinks.Find(Web.Str(item["hangoutLink"]), Web.Str(video?["uri"]), location, Web.Str(item["description"]));
        // Shown as free, or declined by the person: none of their time, which the next meeting passes by.
        var free = Web.Str(item["transparency"]) == "transparent"
            || Web.Items(item, "attendees").Any(attendee => Web.Bool(attendee["self"]) == true && Web.Str(attendee["responseStatus"]) == "declined");
        return new Occurrence(
            string.IsNullOrWhiteSpace(title) ? S.DockCalendarNoTitle : title,
            string.IsNullOrWhiteSpace(location) ? null : location,
            allDay,
            false,
            start,
            end,
            join?.Url,
            free,
            // Its page in Google Calendar, where a click on it opens (§434).
            MailLinks.Web(Web.Str(item["htmlLink"])));
    }

    /// <summary>An all-day event's date (<c>date</c>): the same day wherever it is read.</summary>
    private static DateTime? Date(JsonNode? value) =>
        DateTime.TryParseExact(Web.Str(value?["date"]), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
            ? DateTime.SpecifyKind(day, DateTimeKind.Unspecified)
            : null;

    /// <summary>A timed event's instant (<c>dateTime</c>, RFC 3339 with its offset), as UTC.</summary>
    private static DateTime? Instant(JsonNode? value) =>
        DateTimeOffset.TryParse(Web.Str(value?["dateTime"]), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at)
            ? at.UtcDateTime
            : null;

    /// <summary>One person of the contacts; null for one without a resource name.</summary>
    internal static Person? Contact(JsonObject item)
    {
        if (Web.Str(item["resourceName"]) is not { Length: > 0 } id)
        {
            return null;
        }
        var emails = Web.Items(item, "emailAddresses").Select(each => Web.Str(each["value"])?.Trim()).OfType<string>().Where(each => each.Length > 0).ToList();
        var phones = Web.Items(item, "phoneNumbers").Select(each => Web.Str(each["value"])?.Trim()).OfType<string>().Where(each => each.Length > 0).ToList();
        var organization = Web.Str(Primary(Web.Items(item, "organizations"))?["name"]);
        var name = Web.Str(Primary(Web.Items(item, "names"))?["displayName"]);
        if (string.IsNullOrWhiteSpace(name))
        {
            name = emails.FirstOrDefault() ?? phones.FirstOrDefault() ?? organization ?? S.CoreAccountsNoName;
        }
        return new Person(id, name.Trim(), string.IsNullOrWhiteSpace(organization) ? null : organization.Trim(), emails, phones)
        {
            Photo = PhotoUrl(item) is { } url ? Version(url) : null,
        };
    }

    /// <summary>The address of a person's own picture: their first that is not Google's drawn letter (<c>default</c>); null for none.</summary>
    internal static string? PhotoUrl(JsonObject person) =>
        Web.Items(person, "photos").Where(photo => Web.Bool(photo["default"]) != true).Select(photo => Web.Str(photo["url"])).FirstOrDefault(url => url is { Length: > 0 });

    /// <summary>A picture's address asked for at <paramref name="size"/> px: Google's photo server takes the size after the last '='.</summary>
    internal static string Sized(string url, int size)
    {
        var mark = url.LastIndexOf('=');
        var slash = url.LastIndexOf('/');
        var bare = mark > slash && mark + 1 < url.Length && url[mark + 1] == 's' ? url[..mark] : url;
        return $"{bare}=s{size.ToString(CultureInfo.InvariantCulture)}";
    }

    /// <summary>A short hash of a picture's address: its version, which a new picture moves.</summary>
    private static string Version(string url) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url)))[..16];

    /// <summary>The item marked primary of a person's names or organizations, else the first.</summary>
    private static JsonObject? Primary(IReadOnlyList<JsonObject> items) =>
        items.FirstOrDefault(item => Web.Bool(item["metadata"]?["primary"]) == true) ?? items.FirstOrDefault();

    private static string Stamp(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
}
