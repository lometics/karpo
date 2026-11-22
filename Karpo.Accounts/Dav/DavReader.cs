// A CalDAV and CardDAV account: its calendars, kept whole by the dock and
// brought up to date (ICalendarSync, CalendarSync), and every card in every
// address book. The homes were found when the account was added (DavDiscovery)
// and are kept on it, so a read starts there.
//
// A calendar is every iCalendar object it holds, whatever its time: the dock
// expands them itself (docs\widgets.md §616), so no time-range query is asked
// and no server's own expansion of a series is leaned on. Objects and cards
// are fetched in multigets, because addressbook-query is where servers
// disagree most. A person's picture is their card read again on its own
// (PhotoAsync).
//
// Each calendar and each book is read on its own (Parts): one the server
// refuses fails alone; a server busy (429, 503) fails the account, which is
// tried again as any failure. And a server that stops short says so with a 507
// in the multistatus (RFC 4791 §7.8.1's limits, RFC 6578's number-of-matches-
// within-limits): a multiget cut short is asked again in halves (MultigetAsync);
// what still does not come, and a book listed cut short, is shown with the
// person told that not all of it is.

using System.Text;
using System.Xml.Linq;

namespace Karpo.Accounts.Dav;

/// <param name="client">For tests: the client a read is made with, instead of one signed in with the account's password.</param>
/// <param name="plain">For tests: what a picture on another server is fetched with, signed in as nobody; else <see cref="Web.Following"/>.</param>
/// <param name="now">For tests: UTC now, which a calendar's daily comparison is measured from.</param>
internal sealed class DavReader(Account account, Func<DavClient>? client = null, HttpClient? plain = null, Func<DateTime>? now = null)
    : IAccountReader, ICalendarSync
{
    /// <summary>Cards asked for in one multiget.</summary>
    private const int Batch = 100;

    /// <summary>The status a server stops short with: "507 Insufficient Storage".</summary>
    private const int Truncated = 507;

    public Account Account { get; } = account;

    private DavClient Client() => client?.Invoke() ?? new(
        Web.Http,
        Account.User is { Length: > 0 } user ? user : Account.Address,
        AccountBook.Secret(Account) ?? throw new SignInRequiredException(S.CoreAccountsNoPassword(Account.Address)));

    /// <summary>
    /// Every calendar whole, as a first sync fetches it: the dock syncs instead (<see cref="SyncCalendarsAsync"/>), and
    /// this answers the same for a caller that keeps nothing. The window is no matter: every object is.
    /// </summary>
    public async Task<IReadOnlyList<CalendarRead>> CalendarsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellation)
    {
        var synced = await SyncCalendarsAsync(new Dictionary<string, CalendarState>(StringComparer.Ordinal), null, cancellation).ConfigureAwait(false);
        return [.. synced.Select(changes => new CalendarRead(changes.Calendar, [], [.. changes.Changed.Select(one => one.Text).OfType<string>()], changes.Off)
        {
            Failed = changes.Failed,
            Incomplete = changes.Incomplete,
        })];
    }

    public async Task<IReadOnlyList<CalendarChanges>> SyncCalendarsAsync(
        IReadOnlyDictionary<string, CalendarState> held, Func<CalendarChanges, Task>? checkpoint, CancellationToken cancellation)
    {
        if (!Uri.TryCreate(Account.CalendarHome, UriKind.Absolute, out var home))
        {
            return [];
        }
        var client = Client();
        // What each calendar's sync is gated on comes with the listing: its ctag, its sync-token, its reports.
        var listed = await client.PropfindAsync(
            home,
            "<displayname/><resourcetype/><c:supported-calendar-component-set/><i:calendar-color/><sync-token/><cs:getctag/><supported-report-set/>",
            1,
            cancellation).ConfigureAwait(false);

        var utcNow = (now ?? (() => DateTime.UtcNow))();
        var found = new List<CalendarChanges>();
        AccountException? first = null;
        foreach (var answer in listed.Answers)
        {
            if (Calendar(answer, listed.At) is not { } calendar)
            {
                continue;
            }
            if (!Account.Syncs(SyncKeys.Calendar(calendar.Id)))
            {
                found.Add(new CalendarChanges(calendar) { Off = true });
                continue;
            }
            try
            {
                var sync = new CalendarSync(client, answer, calendar, held.GetValueOrDefault(calendar.Id), utcNow, checkpoint);
                found.Add(await sync.RunAsync(cancellation).ConfigureAwait(false));
            }
            catch (AccountException refused) when (refused.Status is not (429 or 503))
            {
                // A calendar shared with the person and taken back (403), gone since it was listed (404): alone (Parts).
                // A server too busy (429, 503) is the account's: it is tried again as any failure.
                first ??= refused;
                found.Add(new CalendarChanges(calendar) { Failed = refused.Message });
            }
        }
        Parts.Check(found, changes => changes.Off, changes => changes.Failed is not null, first);
        return found;
    }

    public async Task<IReadOnlyList<BookRead>> ContactsAsync(CancellationToken cancellation)
    {
        if (!Uri.TryCreate(Account.BookHome, UriKind.Absolute, out var home))
        {
            return [];
        }
        var client = Client();
        var listed = await client.PropfindAsync(home, "<displayname/><resourcetype/>", 1, cancellation).ConfigureAwait(false);

        var books = new List<BookRead>();
        AccountException? first = null;
        foreach (var book in listed.Answers.Where(answer => IsA(answer, Dav.Card + "addressbook") && !DavClient.Same(answer.Href, listed.At)))
        {
            var named = Book(book);
            if (!Account.Syncs(SyncKeys.Book(named.Id)))
            {
                books.Add(new BookRead(named, [], Off: true));
                continue;
            }
            try
            {
                books.Add(await PeopleAsync(client, book.Href, named, cancellation).ConfigureAwait(false));
            }
            catch (AccountException refused)
            {
                // A book the server will not give fails alone, as a calendar does (Parts).
                first ??= refused;
                books.Add(new BookRead(named, []) { Failed = refused.Message });
            }
        }
        Parts.Check(books, read => read.Off, read => read.Failed is not null, first);
        return books;
    }

    /// <summary>
    /// Everyone in one book: its cards listed, then fetched a hundred at a time. A list the server cut short
    /// is what there is to fetch, and the book says it is incomplete; a multiget cut short is asked again in
    /// halves, down to one card.
    /// </summary>
    private static async Task<BookRead> PeopleAsync(DavClient client, Uri book, AddressBook named, CancellationToken cancellation)
    {
        var people = new List<Person>();
        var members = await client.PropfindAsync(book, "<getetag/><resourcetype/>", 1, cancellation).ConfigureAwait(false);
        var cut = members.Answers.Any(answer => answer.Status == Truncated);
        var cards = members.Answers
            .Where(answer => answer.Status is >= 200 and < 300 && !DavClient.Same(answer.Href, book) && !IsA(answer, Dav.D + "collection"))
            .Select(answer => answer.Href)
            .ToList();
        for (var start = 0; start < cards.Count; start += Batch)
        {
            var (got, stopped) = await MultigetAsync(client, book, cards.Skip(start).Take(Batch).ToList(), Multiget, cancellation).ConfigureAwait(false);
            cut |= stopped;
            foreach (var card in got)
            {
                if (card.Value(Dav.CardData) is { Length: > 0 } text && VCard.Parse(text, card.Href.AbsoluteUri) is { } person)
                {
                    people.Add(person);
                }
            }
        }
        return new BookRead(named, people) { Incomplete = cut };
    }

    /// <summary>
    /// <paramref name="hrefs"/> of <paramref name="collection"/> in one multiget (<paramref name="body"/>: a calendar's or
    /// an address book's), and whether any were still cut short: a multiget the server stops short on (a 507 among its
    /// answers, or as the whole answer's status) is asked again in halves for what did not come, down to one, so none
    /// that came is asked for twice. What came is every answer but the 507s: a 404 among them says that one is gone.
    /// </summary>
    internal static async Task<(List<DavAnswer> Came, bool Cut)> MultigetAsync(
        DavClient client, Uri collection, IReadOnlyList<Uri> hrefs, Func<IEnumerable<Uri>, string> body, CancellationToken cancellation)
    {
        IReadOnlyList<DavAnswer> got;
        try
        {
            got = (await client.ReportAsync(collection, body(hrefs), cancellation).ConfigureAwait(false)).Answers;
        }
        catch (AccountException whole) when (whole.Status == Truncated)
        {
            got = [new DavAnswer(collection, Truncated, [])];
        }
        var came = got.Where(answer => answer.Status != Truncated).ToList();
        if (came.Count == got.Count)
        {
            return (came, false);
        }
        var have = came.Select(answer => DavClient.Key(answer.Href)).ToHashSet(StringComparer.Ordinal);
        var left = hrefs.Where(href => !have.Contains(DavClient.Key(href))).ToList();
        if (left.Count == 0)
        {
            return (came, false);
        }
        if (hrefs.Count == 1)
        {
            return (came, true);
        }
        if (left.Count == 1)
        {
            var (one, oneCut) = await MultigetAsync(client, collection, left, body, cancellation).ConfigureAwait(false);
            return ([.. came, .. one], oneCut);
        }
        var half = left.Count / 2;
        var (early, earlyCut) = await MultigetAsync(client, collection, left[..half], body, cancellation).ConfigureAwait(false);
        var (late, lateCut) = await MultigetAsync(client, collection, left[half..], body, cancellation).ConfigureAwait(false);
        return ([.. came, .. early, .. late], earlyCut || lateCut);
    }

    /// <summary>
    /// A person's picture (<see cref="Person.Photo"/>): their card read again — the book's read keeps only
    /// a hash of its PHOTO — and the bytes it holds, or those at the address it names. That address is
    /// fetched signed in only on the account's own server; anywhere else it is a plain GET, so the
    /// password never goes to a host a card names. A card or picture gone (404) is no picture.
    /// </summary>
    public async Task<byte[]?> PhotoAsync(Person person, CancellationToken cancellation)
    {
        if (person.Photo is null || !Uri.TryCreate(person.Id, UriKind.Absolute, out var card))
        {
            return null;
        }
        var client = Client();
        try
        {
            var (data, url) = VCard.Photo(Encoding.UTF8.GetString(await client.GetAsync(card, cancellation).ConfigureAwait(false)));
            if (data is not null || url is null)
            {
                return data;
            }
            if (Uri.Compare(url, card, UriComponents.SchemeAndServer, UriFormat.UriEscaped, StringComparison.OrdinalIgnoreCase) == 0)
            {
                return await client.GetAsync(url, cancellation).ConfigureAwait(false);
            }
            using var answer = await (plain ?? Web.Following).GetAsync(url, cancellation).ConfigureAwait(false);
            return answer.IsSuccessStatusCode ? await answer.Content.ReadAsByteArrayAsync(cancellation).ConfigureAwait(false) : null;
        }
        catch (AccountException gone) when (gone.Status == 404)
        {
            return null;
        }
    }

    /// <summary>An address book from a PROPFIND of the home: its address, and its name or the last part of it.</summary>
    internal static AddressBook Book(DavAnswer answer)
    {
        var name = answer.Value(Dav.DisplayName);
        if (string.IsNullOrWhiteSpace(name))
        {
            name = Uri.UnescapeDataString(answer.Href.AbsolutePath.TrimEnd('/').Split('/')[^1]);
        }
        return new AddressBook(answer.Href.AbsoluteUri, name);
    }

    public Task<IReadOnlyList<FolderRead>> MailAsync(int count, CancellationToken cancellation) =>
        Task.FromResult<IReadOnlyList<FolderRead>>([]);

    /// <summary>A calendar that holds events, from a PROPFIND of the home; null for anything else.</summary>
    internal static RemoteCalendar? Calendar(DavAnswer answer, Uri home)
    {
        if (!IsA(answer, Dav.Cal + "calendar") || DavClient.Same(answer.Href, home))
        {
            return null;
        }
        // A server that lists the components a calendar takes: a task list has no events to show.
        if (answer.Prop(Dav.Components) is { } components
            && components.Elements(Dav.Cal + "comp").Any()
            && !components.Elements(Dav.Cal + "comp").Any(comp => string.Equals((string?)comp.Attribute("name"), "VEVENT", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }
        var name = answer.Value(Dav.DisplayName);
        if (string.IsNullOrWhiteSpace(name))
        {
            name = Uri.UnescapeDataString(answer.Href.AbsolutePath.TrimEnd('/').Split('/')[^1]);
        }
        return new RemoteCalendar(answer.Href.AbsoluteUri, name, Colour(answer.Value(Dav.CalendarColor)));
    }

    /// <summary>Apple's calendar-color, <c>#RRGGBB</c> or <c>#RRGGBBAA</c>, as <c>#RRGGBB</c>.</summary>
    internal static string? Colour(string? value)
    {
        var text = value?.Trim();
        return text is { Length: 7 or 9 } && text[0] == '#' && text[1..].All(char.IsAsciiHexDigit) ? text[..7] : null;
    }

    private static string Multiget(IEnumerable<Uri> hrefs)
    {
        var body = new StringBuilder();
        body.Append("""<?xml version="1.0" encoding="utf-8"?>""")
            .Append($"""<a:addressbook-multiget xmlns:d="DAV:" xmlns:a="{Dav.Card}">""")
            .Append("<d:prop><d:getetag/><a:address-data/></d:prop>");
        foreach (var href in hrefs)
        {
            body.Append("<d:href>").Append(DavClient.Escaped(DavClient.Path(href))).Append("</d:href>");
        }
        return body.Append("</a:addressbook-multiget>").ToString();
    }

    private static bool IsA(DavAnswer answer, XName type) =>
        answer.Prop(Dav.ResourceType)?.Element(type) is not null;
}
