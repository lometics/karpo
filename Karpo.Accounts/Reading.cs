// What an account gives Karpo, the same shape whatever kind of account it is.
//
// A reader is one account's door: calendars and their events for a window of
// time, every contact, and the newest mail in the inbox and in each mail folder
// the person chose to sync (§639). Every call is a read
// made there and then — a reader keeps no sync state and no cursor; where one
// is needed the dock hands it in. The dock's stores decide when to call, hold
// the answer in memory, and keep what their widgets draw of it on disk for a
// start without a connection (Karpo.Dock\Pim\PimCache.cs, CalendarMirror.cs).
//
// Events come in one of two forms. A Microsoft or Google calendar answers
// occurrences in a window: Graph's calendarView and Calendar's single events
// expand a series themselves. A CalDAV calendar is
// kept whole by the dock and brought up to date (ICalendarSync, docs\widgets.md
// §616): the dock hands over what it holds — each object's href and etag, the
// calendar's sync token and tag — and the reader answers what changed, each
// object cut to what rows need (Dav\KeptICalendar.cs). The reader still keeps
// nothing: the cursor is the dock's. The dock expands the objects with the RFC
// 5545 code it has always had — servers differ too much in what their own
// expansion gets right for Karpo to lean on it.

namespace Karpo.Accounts;

/// <summary>One calendar of an account.</summary>
/// <param name="Id">Unique within the account and the same from run to run.</param>
/// <param name="Colour">The colour the account gives it, <c>#RRGGBB</c>; null where it gives none.</param>
public sealed record RemoteCalendar(string Id, string Name, string? Colour);

/// <summary>
/// One occurrence, before it is put in a zone: a whole day, a floating wall clock, or an instant.
/// </summary>
/// <param name="Start">A date or a wall clock when <paramref name="AllDay"/> or <paramref name="Floating"/>; else UTC.</param>
/// <param name="End">The same, exclusive; equal to <paramref name="Start"/> for an event without a length.</param>
/// <param name="Join">
/// Where its online meeting is joined (<see cref="MeetingLinks"/>): from the meeting's own field, its
/// place or its notes, as the account gave them; null for an event with none.
/// </param>
/// <param name="Free">
/// It takes none of the person's time: shown as free, or declined by them, as the account says. The
/// calendars draw it as any; the next meeting passes it by.
/// </param>
/// <param name="Link">
/// Where the event itself opens on its account's site, an https address (<see cref="CalendarLinks"/>): Graph's
/// webLink, Outlook on the web's page of it; null where the account names none.
/// </param>
public readonly record struct Occurrence(
    string Title,
    string? Location,
    bool AllDay,
    bool Floating,
    DateTime Start,
    DateTime End,
    string? Join = null,
    bool Free = false,
    string? Link = null);

/// <summary>What one calendar holds in a window: occurrences (Microsoft), or iCalendar objects to expand (CalDAV).</summary>
/// <param name="Off">
/// The person turned it off (<see cref="Account.Off"/>): listed, so the Accounts page can name it,
/// with nothing fetched.
/// </param>
public sealed record CalendarRead(
    RemoteCalendar Calendar,
    IReadOnlyList<Occurrence> Occurrences,
    IReadOnlyList<string> Objects,
    bool Off = false)
{
    /// <summary>
    /// Why this calendar alone could not be read, in the account's words — a shared calendar taken
    /// away (403), one gone (404), a server error for it —, the account's others read as ever; null
    /// where it was read. It holds nothing then, and the dock keeps what it held before (Parts).
    /// </summary>
    public string? Failed { get; init; }

    /// <summary>
    /// It has more than the read could take: the server gave only part of it (a CalDAV 507) or the read
    /// stopped at its most. What it holds is shown, and the person is told not all of it is.
    /// </summary>
    public bool Incomplete { get; init; }
}

/// <summary>One address book of an account: a CardDAV book, or a Microsoft contact folder.</summary>
/// <param name="Id">Unique within the account and the same from run to run.</param>
public sealed record AddressBook(string Id, string Name);

/// <summary>Who is in one address book; none when it is <paramref name="Off"/> (as <see cref="CalendarRead.Off"/>).</summary>
public sealed record BookRead(AddressBook Book, IReadOnlyList<Person> People, bool Off = false)
{
    /// <summary>Why this book alone could not be read, as <see cref="CalendarRead.Failed"/>; null where it was read.</summary>
    public string? Failed { get; init; }

    /// <summary>It has more people than the read could take, as <see cref="CalendarRead.Incomplete"/>.</summary>
    public bool Incomplete { get; init; }
}

/// <summary>
/// The rule every reader keeps for the parts of an account — its calendars, its address books:
/// each is read on its own. A part the account will not give (an <see cref="AccountException"/>: a
/// refusal, a server error, an answer that does not read) fails alone and says why on its read
/// (<see cref="CalendarRead.Failed"/>), and the others are read as ever. A sign-in refused, the
/// network gone or a timeout is the account's, not a part's: it fails the whole read, as it would
/// fail every part anyway. And where every part the person has on failed, the account failed: the
/// first part's trouble is thrown, so the account is shown stale and tried again as any other.
/// </summary>
internal static class Parts
{
    /// <summary>Throws <paramref name="first"/>, a part's failure, where no part that is on was read.</summary>
    internal static void Check<T>(IReadOnlyList<T> reads, Func<T, bool> off, Func<T, bool> failed, AccountException? first)
    {
        if (first is not null && reads.All(read => off(read) || failed(read)))
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(first);
        }
    }
}

/// <summary>One person, as much of a contact as the contacts panel draws.</summary>
public sealed record Person(
    string Id,
    string DisplayName,
    string? Organization,
    IReadOnlyList<string> Emails,
    IReadOnlyList<string> Phones)
{
    /// <summary>The first email, which the list shows under the name when there is one.</summary>
    public string? Email => Emails.Count > 0 ? Emails[0] : null;

    /// <summary>The first phone, shown where there is no email at all.</summary>
    public string? Phone => Phones.Count > 0 ? Phones[0] : null;

    /// <summary>What the row shows under the name: the email, else the phone, else the organization.</summary>
    public string? Detail => Email ?? Phone ?? (string.IsNullOrWhiteSpace(Organization) ? null : Organization);

    /// <summary>
    /// Their address book's colour, <c>#RRGGBB</c>, where the dock shows people of more than one book
    /// (<see cref="SourceColours"/>); null where it shows one. The dock's to set, never a reader's.
    /// </summary>
    public string? Colour { get; init; }

    /// <summary>§455: their address book, as a widget picks it (as <see cref="MailItem.Part"/>); the dock's to set, never a reader's.</summary>
    public string? Book { get; init; }

    /// <summary>
    /// The version of their picture, where they have one or may (<see cref="IAccountReader.PhotoAsync"/>):
    /// a vCard's PHOTO, hashed; a Graph contact's changeKey, which any change to it moves, its picture's
    /// too. Null where they have none. A picture is fetched again only when this changes.
    /// </summary>
    public string? Photo { get; init; }
}

/// <summary>One message in an inbox.</summary>
/// <param name="From">The sender's name, or their address where they gave none.</param>
/// <param name="Link">
/// Where the message opens in the account's web mail (MailLinks): an https address, the message's own
/// where the account gives one, else its inbox's; null where Karpo knows none.
/// </param>
public sealed record MailItem(
    string Id,
    string From,
    string? FromAddress,
    string Subject,
    string? Preview,
    DateTimeOffset Received,
    bool Unread,
    string? Link = null)
{
    /// <summary>
    /// Its inbox's colour, <c>#RRGGBB</c>, where the dock shows more than one inbox
    /// (<see cref="SourceColours"/>); null where it shows one. The dock's to set, never a reader's.
    /// </summary>
    public string? Colour { get; init; }

    /// <summary>
    /// §455: the part it is of, as a widget picks it (Karpo.Core's WidgetSources.Key): its account's address and its
    /// inbox's or folder's key. The dock's to set, never a reader's; null for none.
    /// </summary>
    public string? Part { get; init; }

    /// <summary>
    /// §639: its folder's name ("Inbox", "Archive"), where the widget showing it shows a folder besides the inboxes; null
    /// where it shows inboxes alone. The dock's to set, never a reader's.
    /// </summary>
    public string? Folder { get; init; }
}

/// <summary>An inbox, or any mail folder: how many are unread in all, and the newest messages.</summary>
/// <param name="Total">How many messages it holds in all, where the account said.</param>
public sealed record Inbox(int Unread, IReadOnlyList<MailItem> Messages, int? Total = null);

/// <summary>
/// One mail folder of an account (docs\widgets.md §455): the inbox, or another a mail widget may show from.
/// </summary>
/// <param name="Id">Unique within the account and the same from run to run: IMAP's full name, Graph's id.</param>
/// <param name="Name">As the account names it, a folder in a folder after its parent's ("Inbox / Projects").</param>
/// <param name="Inbox">The account's inbox (<see cref="SyncKeys.Inbox"/>), listed first.</param>
/// <param name="Unread">§639: how many it holds unread, where the account says so with its list (Graph; IMAP's LIST-STATUS).</param>
/// <param name="Total">§639: how many it holds in all, the same.</param>
public sealed record MailFolder(string Id, string Name, bool Inbox = false, int? Unread = null, int? Total = null);

/// <summary>
/// What one mail folder holds (<see cref="IAccountReader.MailAsync"/>): its newest messages where the account syncs it
/// (<see cref="Account.Syncs"/>), none where it was only listed.
/// </summary>
public sealed record FolderRead(MailFolder Folder, Inbox? Mail)
{
    /// <summary>Why this folder alone could not be read, as <see cref="CalendarRead.Failed"/>; null where it was read or not asked for.</summary>
    public string? Failed { get; init; }
}

/// <summary>
/// What the dock holds of one calendar it keeps whole (<see cref="ICalendarSync"/>), handed back at the next sync so
/// only what changed since is fetched. The reader keeps none of it.
/// </summary>
public sealed record CalendarState
{
    /// <summary>The sync-collection token (RFC 6578) of the last sync that brought all its changes; null for none.</summary>
    public string? Token { get; init; }

    /// <summary>The calendar's ctag, else its sync-token property, as that sync found it: unchanged, nothing has.</summary>
    public string? Tag { get; init; }

    /// <summary>The server has no sync-collection for it: it is listed by its etags instead.</summary>
    public bool NoSyncReport { get; init; }

    /// <summary>It holds all of the calendar as of <see cref="Token"/> and <see cref="Tag"/>; false while a first download is on its way.</summary>
    public bool Complete { get; init; }

    /// <summary>When it was last compared whole with the server's listing, to catch a deletion a server lost.</summary>
    public DateTime? ReconciledUtc { get; init; }

    /// <summary>Each object it holds: its href (<c>DavClient.Key</c>) and its etag.</summary>
    public IReadOnlyDictionary<string, string> Etags { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
}

/// <summary>One object of a calendar as kept: its href, its etag, and its iCalendar cut to what rows need (null: it has no event).</summary>
public sealed record CalendarObject(string Href, string Etag, string? Text);

/// <summary>
/// What changed in one calendar since the state the dock handed over (<see cref="ICalendarSync"/>): the objects to put
/// in, by href, and those to take out, and the state to keep after.
/// </summary>
public sealed record CalendarChanges(RemoteCalendar Calendar)
{
    /// <summary>Turned off by the person (<see cref="CalendarRead.Off"/>): listed, nothing fetched; the dock drops what it held.</summary>
    public bool Off { get; init; }

    /// <summary>Why it alone could not be synced (<see cref="CalendarRead.Failed"/>); the dock keeps what it held.</summary>
    public string? Failed { get; init; }

    /// <summary>Its tag says nothing changed: nothing was fetched, and the dock's state stands.</summary>
    public bool Unchanged { get; init; }

    public IReadOnlyList<CalendarObject> Changed { get; init; } = [];

    public IReadOnlyList<string> Removed { get; init; } = [];

    /// <summary>The token and tag to keep after these changes (<see cref="CalendarState"/>).</summary>
    public string? Token { get; init; }

    public string? Tag { get; init; }

    public bool NoSyncReport { get; init; }

    /// <summary>
    /// Not all of it came: the server stopped short, an object did not come, or it holds more than the most kept. What
    /// came is put in and nothing unlisted is taken out; the state is not complete, so it is asked again whole.
    /// </summary>
    public bool Incomplete { get; init; }

    /// <summary>When it was compared whole with the server's listing in this sync; else the state's, as handed over.</summary>
    public DateTime? ReconciledUtc { get; init; }
}

/// <summary>
/// A reader whose calendars the dock keeps whole and brings up to date (CalDAV, RFC 6578): it gets what the dock
/// holds and answers only what changed. Stateless as every reader: the dock holds the cursor.
/// </summary>
public interface ICalendarSync
{
    /// <summary>
    /// Every calendar and what changed in it since <paramref name="held"/> (by <see cref="RemoteCalendar.Id"/>; a
    /// calendar missing from it is fetched whole). A long first download hands over what it has every so many
    /// objects to <paramref name="checkpoint"/> (their changes, not complete) and leaves those out of its answer.
    /// A calendar that alone fails says so (<see cref="CalendarChanges.Failed"/>); the account failing throws.
    /// </summary>
    Task<IReadOnlyList<CalendarChanges>> SyncCalendarsAsync(
        IReadOnlyDictionary<string, CalendarState> held, Func<CalendarChanges, Task>? checkpoint, CancellationToken cancellation);
}

/// <summary>One account's door. Every call reads there and then; nothing is kept.</summary>
public interface IAccountReader
{
    Account Account { get; }

    /// <summary>
    /// Every calendar, and what it holds in [<paramref name="fromUtc"/>, <paramref name="toUtc"/>); a
    /// calendar the person turned off is listed with nothing asked for (<see cref="CalendarRead.Off"/>).
    /// </summary>
    Task<IReadOnlyList<CalendarRead>> CalendarsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellation);

    /// <summary>Every address book, and everyone in it; a book turned off is listed with nobody (<see cref="BookRead.Off"/>).</summary>
    Task<IReadOnlyList<BookRead>> ContactsAsync(CancellationToken cancellation);

    /// <summary>
    /// Every mail folder, the inbox first; of each the account syncs (§639: the inbox unless turned off, another folder
    /// where turned on, <see cref="Account.Syncs"/>) the unread count and the newest <paramref name="count"/> messages,
    /// newest first, of every other none. The inbox failing fails the read; another folder fails alone
    /// (<see cref="FolderRead.Failed"/>). An account with no mail answers nothing.
    /// </summary>
    Task<IReadOnlyList<FolderRead>> MailAsync(int count, CancellationToken cancellation);

    /// <summary>
    /// <paramref name="person"/>'s picture as the account keeps it — a JPEG, a PNG — or null where they have
    /// none (<see cref="Person.Photo"/>). An account that gives no contacts has no pictures either.
    /// </summary>
    Task<byte[]?> PhotoAsync(Person person, CancellationToken cancellation) => Task.FromResult<byte[]?>(null);
}

public static class Readers
{
    /// <summary>The reader for <paramref name="account"/>'s kind.</summary>
    public static IAccountReader For(Account account) => account.Kind switch
    {
        AccountKind.Microsoft => new Microsoft.MicrosoftReader(account),
        AccountKind.Google => new Google.GoogleReader(account),
        AccountKind.Imap => new Mail.ImapReader(account),
        AccountKind.Dav => new Dav.DavReader(account),
        _ => throw new ArgumentOutOfRangeException(nameof(account), account.Kind, null),
    };
}
