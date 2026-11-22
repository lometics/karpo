// An account that answers what a test gives it, counts how often it was asked,
// and can hold its answer or fail — so the stores are tried without a server.

using Karpo.Accounts;

namespace Karpo.Tests.Pim;

internal sealed class FakeReader : IAccountReader
{
    private readonly TaskCompletionSource held = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int calendarReads;
    private int contactReads;
    private int inboxReads;

    /// <param name="hold">Every read waits for <see cref="Answer"/>, for testing what shows meanwhile.</param>
    /// <param name="secret">The account's secret as accounts.json keeps it, for testing that it goes nowhere else.</param>
    public FakeReader(AccountKind kind = AccountKind.Microsoft, string address = "me@example.com", bool hold = false, string? secret = null)
        : this(new Account
        {
            Id = Account.NewId(),
            Kind = kind,
            Address = address,
            CalendarHome = kind == AccountKind.Dav ? "https://dav.example.com/calendars/me/" : null,
            BookHome = kind == AccountKind.Dav ? "https://dav.example.com/books/me/" : null,
            Secret = secret,
        }, hold)
    {
    }

    private FakeReader(Account account, bool hold)
    {
        Account = account;
        if (!hold)
        {
            held.SetResult();
        }
    }

    public Account Account { get; }

    /// <summary>What every read throws, instead of answering; null answers.</summary>
    public Exception? Failure { get; set; }

    public List<CalendarRead> Calendars { get; } = [];

    /// <summary>Who is in the account's one address book, "contacts", where <see cref="Books"/> is empty.</summary>
    public List<Person> People { get; } = [];

    /// <summary>The address books and who is in each; empty for the one book of <see cref="People"/>.</summary>
    public List<(AddressBook Book, List<Person> People)> Books { get; } = [];

    public Inbox Inbox { get; set; } = new(0, []);

    /// <summary>§639: the account's mail folders besides the inbox, and what each holds; listed always, read where the account syncs it.</summary>
    public List<(MailFolder Folder, Inbox Mail)> Folders { get; } = [];

    /// <summary>The folders besides the inbox the last mail read read, by id (§639: those turned on).</summary>
    public IReadOnlyCollection<string> Asked { get; private set; } = [];

    /// <summary>
    /// The calendars and books (by id) the account will not give, as a real reader answers one it alone could
    /// not read (Parts): listed, with nothing in it and why on it, the others read as ever.
    /// </summary>
    public HashSet<string> Failing { get; } = new(StringComparer.Ordinal);

    /// <summary>What a part in <see cref="Failing"/> says.</summary>
    internal const string Refused = "Microsoft Graph answered 403 (ErrorAccessDenied)";

    /// <summary>The window the last calendar read asked for.</summary>
    public (DateTime From, DateTime To) Window { get; private set; }

    public int CalendarReads => Volatile.Read(ref calendarReads);

    public int ContactReads => Volatile.Read(ref contactReads);

    public int InboxReads => Volatile.Read(ref inboxReads);

    /// <summary>
    /// The same account with <paramref name="key"/> turned on or off, answering what this one answers
    /// — as the reader made again from accounts.json would — for <c>AccountSet.Become</c>.
    /// </summary>
    public FakeReader Turned(string key, bool on)
    {
        var turned = new FakeReader(Account.Synced(key, on), hold: false) { Inbox = Inbox, Failure = Failure };
        turned.Calendars.AddRange(Calendars);
        turned.People.AddRange(People);
        turned.Books.AddRange(Books);
        turned.Failing.UnionWith(Failing);
        turned.Folders.AddRange(Folders);
        return turned;
    }

    /// <summary>§644: the same account named <paramref name="name"/> (as Graph names a Microsoft account), answering what this one answers.</summary>
    public FakeReader Named(string name)
    {
        var named = new FakeReader(Account with { Name = name }, hold: false) { Inbox = Inbox, Failure = Failure };
        named.Folders.AddRange(Folders);
        return named;
    }

    /// <summary>
    /// The same account with <paramref name="key"/> wearing <paramref name="colour"/> (null: its own
    /// again), answering what this one answers — as the reader made again from accounts.json would.
    /// </summary>
    public FakeReader Coloured(string key, string? colour)
    {
        var coloured = new FakeReader(Account.Coloured(key, colour), hold: false) { Inbox = Inbox, Failure = Failure };
        coloured.Calendars.AddRange(Calendars);
        coloured.People.AddRange(People);
        coloured.Books.AddRange(Books);
        coloured.Failing.UnionWith(Failing);
        coloured.Folders.AddRange(Folders);
        return coloured;
    }

    /// <summary>Lets the held reads answer.</summary>
    public void Answer() => held.TrySetResult();

    public async Task<IReadOnlyList<CalendarRead>> CalendarsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellation)
    {
        Interlocked.Increment(ref calendarReads);
        Window = (fromUtc, toUtc);
        await held.Task.ConfigureAwait(false);
        // As the real readers do: a calendar turned off is listed, with nothing fetched.
        return Failure is { } failure
            ? throw failure
            : [.. Calendars.Select(read => !Account.Syncs(SyncKeys.Calendar(read.Calendar.Id)) ? new CalendarRead(read.Calendar, [], [], Off: true)
                : Failing.Contains(read.Calendar.Id) ? new CalendarRead(read.Calendar, [], []) { Failed = Refused }
                : read)];
    }

    public async Task<IReadOnlyList<BookRead>> ContactsAsync(CancellationToken cancellation)
    {
        Interlocked.Increment(ref contactReads);
        await held.Task.ConfigureAwait(false);
        if (Failure is { } failure)
        {
            throw failure;
        }
        var books = Books.Count > 0 ? Books : [(new AddressBook("contacts", "Contacts"), People)];
        return [.. books.Select(book => !Account.Syncs(SyncKeys.Book(book.Book.Id)) ? new BookRead(book.Book, [], Off: true)
            : Failing.Contains(book.Book.Id) ? new BookRead(book.Book, []) { Failed = Refused }
            : new BookRead(book.Book, [.. book.People]))];
    }

    /// <summary>Each person's picture, by their id; one not here has none.</summary>
    public Dictionary<string, byte[]> Photos { get; } = new(StringComparer.Ordinal);

    /// <summary>What every picture asked for throws, instead of answering; null answers.</summary>
    public Exception? PhotoFailure { get; set; }

    private int photoReads;

    /// <summary>How many pictures were asked for.</summary>
    public int PhotoReads => Volatile.Read(ref photoReads);

    public Task<byte[]?> PhotoAsync(Person person, CancellationToken cancellation)
    {
        Interlocked.Increment(ref photoReads);
        return PhotoFailure is { } failure ? Task.FromException<byte[]?>(failure) : Task.FromResult(Photos.GetValueOrDefault(person.Id));
    }

    public async Task<IReadOnlyList<FolderRead>> MailAsync(int count, CancellationToken cancellation)
    {
        Interlocked.Increment(ref inboxReads);
        Asked = [.. Folders.Select(each => each.Folder.Id).Where(id => Account.Syncs(SyncKeys.MailFolder(id)))];
        await held.Task.ConfigureAwait(false);
        // As the real readers do: the inbox unless turned off, then every folder listed, read where it is turned on.
        return Failure is { } failure
            ? throw failure
            : [new FolderRead(new MailFolder("INBOX", "Inbox", Inbox: true), Account.Syncs(SyncKeys.Inbox) ? Inbox : null),
                .. Folders.Select(each => !Account.Syncs(SyncKeys.MailFolder(each.Folder.Id)) ? new FolderRead(each.Folder, null)
                    : Failing.Contains(each.Folder.Id) ? new FolderRead(each.Folder, null) { Failed = Refused }
                    : new FolderRead(each.Folder, each.Mail))];
    }
}
