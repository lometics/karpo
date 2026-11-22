// The contacts, shared by the contacts' lines (ContactsLine), read from the
// person's accounts (Karpo.Accounts).
//
// It follows MailStore: the dock keeps it reading from its start (PimSync,
// Keep), whether or not a contacts line is placed, so every account's address
// books are synced for as long as it runs (2026-10-29: "all the items from the
// accounts should always be synced even if someone does not have the contacts
// widget placed"). Each account is its own sync: read at the one interval every
// online widget follows (OnlineUpdates), a minute after it failed, at once when
// the accounts change or the line's Refresh asks. An account that cannot be read
// keeps the people it gave last, and so does one book of it that alone cannot
// (BookRead.Failed: a folder refused, the account's others read). With no account that gives contacts the timer
// stops until one is added; a change to the interval re-arms it at once.
//
// The store does its work on the pool and raises Changed on the dock's thread
// through a StoreSignal; a line only ever reads a snapshot already in memory. A
// few hundred people is a few hundred kilobytes, so there is no cursor: every
// read is the whole of each book.
//
// Offline as the calendars and the mail are: every good read writes each
// account's books and people to contacts-cache.json (PimCache, DPAPI-encrypted
// for this Windows user), and the first read of a run takes them back in first,
// on the pool, so a start without a connection finds the people — and an
// account read there less than the interval ago is not read again before the
// interval since that read is up (PimCache.KeptRead): a restart is no reason to
// read every address book again.
//
// Their pictures are fetched one by one and kept on disk too (ContactPhotos): after
// each read the store hands everyone over, and a line draws whatever picture is in
// memory (Photo).

using Karpo.Accounts;
using Karpo.Core;

namespace Karpo.Dock.Pim;

internal sealed class ContactStore : IDisposable
{
    /// <summary>What the line says while no account has contacts.</summary>
    internal static string NoAccount => S.DockAccountsNoContactsAccount;

    /// <summary>Made on first use, which is on the dock's thread.</summary>
    private static readonly Lazy<ContactStore> SharedStore = new(() => new ContactStore(AccountSet.Shared, report: SyncReport.ForDock(), cache: PimCache.ForDock(), photos: ContactPhotos.DockFolder()));

    /// <summary>How long to wait before asking an account again after it failed.</summary>
    private static readonly TimeSpan Retry = TimeSpan.FromMinutes(1);

    private readonly Lock gate = new();
    private readonly StoreSignal signal;
    private readonly AccountSet accounts;
    private readonly Timer again;
    private readonly SyncReport? report;
    private readonly PimCache? cache;
    private readonly Func<DateTime> now;
    private readonly ContactPhotos photos;

    // How often the books are read: every online widget's interval, with no floor of its own, as
    // the mail has none.
    private readonly OnlineUpdates updates;

    /// <summary>Each account's sync, by the account's id: what it gave last, and when it is due again.</summary>
    private readonly Dictionary<string, Sync> syncs = new(StringComparer.Ordinal);

    /// <summary>§455: each widget's people, by what it picked (<see cref="SourceFilter.Id"/>); made again after every change.</summary>
    private readonly Dictionary<string, IReadOnlyList<Person>> views = new(StringComparer.Ordinal);

    /// <summary>§455: every account's books of its last good read, in their order, which each view picks from.</summary>
    private IReadOnlyList<(Account Account, BookRead Book)> held = [];

    private Task? working;
    private Task cacheRead = Task.CompletedTask;
    private long nextTry;
    private bool asked;
    private bool cacheTaken;

    /// <summary>contacts-cache.json was read and taken in: only from then on is it written.</summary>
    private bool cacheIn;

    // Whether the timer is counting the interval from the last reads, rather than resting after a
    // failure or stopped for want of an account: only then does a new interval move it.
    private bool counting;

    // The dock keeps the contacts read for as long as it runs (PimSync); a test's store reads on
    // while somebody watches it.
    private volatile bool kept;

    /// <param name="updates">The interval every online widget follows: the dock's, or a test's own.</param>
    /// <param name="report">Where what was read is told to the Accounts page; none tells nobody.</param>
    /// <param name="now">UTC now, which a read is stamped with; tests give their own.</param>
    /// <param name="cache">Where the people are kept for a start without a connection; none keeps nothing.</param>
    /// <param name="photos">Where the people's pictures are kept (ContactPhotos); none keeps them in memory alone.</param>
    internal ContactStore(AccountSet accounts, OnlineUpdates? updates = null, SyncReport? report = null, Func<DateTime>? now = null, PimCache? cache = null, string? photos = null)
    {
        this.accounts = accounts;
        this.updates = updates ?? OnlineUpdates.Shared;
        this.report = report;
        this.cache = cache;
        this.now = now ?? (() => Clock.RealUtc());
        signal = new StoreSignal(() => Changed?.Invoke());
        this.photos = new ContactPhotos(photos, signal.Notify);
        again = new Timer(_ => Troubles.Guard("Contacts: reading them again", Due));
        accounts.Changed += OnAccounts;
        accounts.Recoloured += OnRecoloured;
        accounts.Rewritten += OnRewritten;
        this.updates.Changed += OnInterval;
        this.updates.Back += OnBack;
    }

    /// <summary>For tests: how long the timer was last set to wait before the next read; null before it was, or while it is stopped.</summary>
    internal TimeSpan? Armed { get; private set; }

    /// <summary>The store the dock uses, made on first use.</summary>
    public static ContactStore Shared => SharedStore.Value;

    /// <summary>The contacts changed, or reading them did. Raised on the dock's thread.</summary>
    public event Action? Changed;

    /// <summary>The contacts, sorted by the name they show under. Never null, empty before the first read.</summary>
    public IReadOnlyList<Person> Contacts { get; private set; } = [];

    /// <summary>Nothing has been read yet and nothing has gone wrong: the first read is on its way.</summary>
    public bool Loading { get; private set; }

    /// <summary>A read is running now: a line's "Refresh" is left out meanwhile (IRefreshable).</summary>
    public bool Reading
    {
        get
        {
            lock (gate)
            {
                return working is { IsCompleted: false };
            }
        }
    }

    /// <summary>
    /// Why something is missing, in words for the person; null while all is well. An account that
    /// failed with people kept from an earlier read is not missing.
    /// </summary>
    public string? Problem { get; private set; }

    /// <summary>Whether any account gives contacts at all, as the last read found; true before one has.</summary>
    public bool HasAccount { get; private set; } = true;

    /// <summary>
    /// §455: the people of the address books a widget picked (<paramref name="shown"/>, WidgetSources), sorted as
    /// <see cref="Contacts"/> are and coloured where more than one of its books is shown; <see cref="Contacts"/> where
    /// it picked nothing. The same list until the people change, so a line works out its matches once.
    /// </summary>
    public IReadOnlyList<Person> View(SourceFilter shown)
    {
        lock (gate)
        {
            if (shown.Everything)
            {
                return Contacts;
            }
            if (!views.TryGetValue(shown.Id, out var view))
            {
                views[shown.Id] = view = Sorted(Painted(held, shown));
            }
            return view;
        }
    }

    /// <summary>
    /// <paramref name="person"/>'s picture, where it is in memory; null where they have none, or it has
    /// not come yet — a line draws their glyph meanwhile and is woken when it comes.
    /// </summary>
    public Pixels? Photo(Person person) => photos.Of(person);

    /// <summary>§661: the picture an address of <see cref="ContactPhotos.Address"/> asks for, where it is in memory; null otherwise. Windows search's people's.</summary>
    public Pixels? Photo(string address) => ContactPhotos.KeyOf(address) is { } key ? photos.Of(key) : null;

    /// <summary>An island shows contacts: it is woken when they change. Attaching reads nothing.</summary>
    public void Attach(CellIsland island) => signal.Add(island);

    /// <summary>That island is gone.</summary>
    public void Release(CellIsland island) => signal.Remove(island);

    /// <summary>The dock's start (PimSync): the first read starts now, and the timer goes on for good.</summary>
    public void Keep()
    {
        kept = true;
        Refresh();
    }

    /// <summary>
    /// Starts whatever read is due and returns at once: the first, or an account whose time has
    /// come. Safe to call from the dock's thread as often as it likes.
    /// </summary>
    public void Refresh()
    {
        lock (gate)
        {
            Start();
        }
    }

    /// <summary>
    /// The line's Refresh: every account asked again now, however fresh its last read or however
    /// recently it failed — but not while a read runs.
    /// </summary>
    public void Reread()
    {
        lock (gate)
        {
            if (nextTry != long.MaxValue)
            {
                foreach (var sync in syncs.Values)
                {
                    sync.Due = 0;
                }
                nextTry = 0;
            }
            Start();
        }
    }

    /// <summary>On the dock's thread: raises <see cref="Changed"/> once for all news since the last time.</summary>
    internal void Deliver() => signal.Deliver();

    /// <summary>The work in flight, the pictures it asked for too, for tests.</summary>
    internal async Task Idle()
    {
        Task reading;
        lock (gate)
        {
            reading = working ?? Task.CompletedTask;
        }
        await reading.ConfigureAwait(false);
        await photos.Idle().ConfigureAwait(false);
    }

    /// <summary>For tests: <paramref name="person"/>'s picture is <paramref name="pixels"/>, as if fetched. Nothing in the dock calls it.</summary>
    internal void Pictured(Person person, Pixels? pixels)
    {
        photos.Put(person, pixels);
        signal.Notify();
    }

    /// <summary>For tests: the cache taken in, which comes before the first read of an account.</summary>
    internal Task CacheRead()
    {
        lock (gate)
        {
            return cacheRead;
        }
    }

    /// <summary>
    /// For tests: the store as a read that found <paramref name="contacts"/> leaves it, with the news
    /// counted for <see cref="Deliver"/>. Nothing is due after it, so the line opening over it reads
    /// nothing. Nothing in the dock calls it.
    /// </summary>
    internal void Answered(IReadOnlyList<Person> contacts)
    {
        lock (gate)
        {
            asked = true;
            cacheTaken = true;
            Contacts = contacts;
            views.Clear();
            Loading = false;
            Problem = null;
            HasAccount = true;
            nextTry = long.MaxValue;
        }
        signal.Notify();
    }

    /// <summary>
    /// For tests: the store as a read that found no account with contacts leaves it. Nothing is due
    /// after it. Nothing in the dock calls it.
    /// </summary>
    internal void NoAccountFound()
    {
        lock (gate)
        {
            asked = true;
            cacheTaken = true;
            Contacts = [];
            views.Clear();
            Loading = false;
            Problem = NoAccount;
            HasAccount = false;
            nextTry = long.MaxValue;
        }
        signal.Notify();
    }

    public void Dispose()
    {
        accounts.Changed -= OnAccounts;
        accounts.Recoloured -= OnRecoloured;
        accounts.Rewritten -= OnRewritten;
        updates.Changed -= OnInterval;
        updates.Back -= OnBack;
        again.Dispose();
    }

    /// <summary>Under <see cref="gate"/>: starts a read unless one is running or nothing is due.</summary>
    private void Start()
    {
        if (working is { IsCompleted: false } || Environment.TickCount64 < nextTry)
        {
            return;
        }
        if (!asked)
        {
            asked = true;
            Loading = true;
        }
        if (!cacheTaken)
        {
            // Before the first read: what was kept, on the pool, so a line never waits for a file.
            cacheTaken = true;
            cacheRead = cache is null ? Task.CompletedTask : Task.Run(() => Troubles.Guard("Contacts: taking in what was kept", TakeCache));
        }
        var first = cacheRead;
        working = Task.Run(() => Troubles.Guard("Contacts: reading them", async () =>
        {
            await first.ConfigureAwait(false);
            await WorkAsync().ConfigureAwait(false);
        }));
    }

    /// <summary>
    /// contacts-cache.json, once, before the first read: the books and the people of every account still
    /// here, as they were last read. An account read since is left as it was read.
    /// </summary>
    private void TakeCache()
    {
        // Not while the accounts are not known (accounts.json held at the start) or the file is held by
        // another program: taken before a later read, and nothing is written over it before.
        if (!accounts.Known || cache!.ReadContacts() is not { } file)
        {
            lock (gate)
            {
                cacheTaken = false;
            }
            return;
        }
        var readers = accounts.Giving(AccountData.Contacts);
        var here = readers.Select(reader => reader.Account.Id).ToHashSet(StringComparer.Ordinal);
        var took = false;
        lock (gate)
        {
            cacheIn = true;
            var tick = Environment.TickCount64;
            var utcNow = now();
            foreach (var account in file.Accounts)
            {
                // Taken too where a read of this run failed before the file could be: its sync has no books yet.
                if (!here.Contains(account.Account) || syncs.TryGetValue(account.Account, out var had) && had.Books is not null)
                {
                    continue;
                }
                var sync = had ?? (syncs[account.Account] = new Sync());
                sync.Books = [.. account.Books.Select(book => book.Read)];
                sync.UpdatedUtc = DateTime.SpecifyKind(account.UpdatedUtc, DateTimeKind.Utc);
                // Read less than the interval ago, every book there: as good as a read of this run, due the interval
                // after it and not now (PimCache.KeptRead). One with a book that failed alone is read at once, to try
                // that book again; one that failed in this run keeps its rest.
                if (had is null && sync.Books.All(book => book.Failed is null)
                    && PimCache.KeptRead(sync.UpdatedUtc.Value, utcNow, updates.Interval, tick) is { } read)
                {
                    sync.Read = read;
                    sync.Due = read + (long)updates.Interval.TotalMilliseconds;
                }
                took = true;
            }
            if (took)
            {
                Rebuild(readers);
                Loading = false;
            }
        }
        if (took)
        {
            signal.Notify();
        }
    }

    /// <summary>Asks every account that gives contacts and is due for all of them, each on its own.</summary>
    private async Task WorkAsync()
    {
        // As this read starts: a failure is read again at once if the network came back meanwhile (RetryNow).
        var online = updates.Comebacks;
        var readers = accounts.Giving(AccountData.Contacts);
        List<IAccountReader> due;
        lock (gate)
        {
            var tick = Environment.TickCount64;
            due = [.. readers.Where(reader => !syncs.TryGetValue(reader.Account.Id, out var sync) || sync.Due <= tick)];
        }

        // One sync per account, side by side: a slow server holds up nobody else's people.
        var answers = await Task.WhenAll(due.Select(ReadAsync)).ConfigureAwait(false);

        ContactCacheFile? keep = null;
        List<(string, SyncPart)>? told = null;
        List<(IAccountReader, Person)> pictured;
        bool everyone;
        lock (gate)
        {
            var tick = Environment.TickCount64;
            var utcNow = now();
            var here = readers.Select(reader => reader.Account.Id).ToHashSet(StringComparer.Ordinal);
            // An account removed stops: its sync goes, and its people with it, from the file too.
            var write = false;
            foreach (var gone in syncs.Keys.Where(id => !here.Contains(id)).ToList())
            {
                syncs.Remove(gone);
                write = true;
            }
            foreach (var (reader, books, error) in answers)
            {
                var sync = syncs.TryGetValue(reader.Account.Id, out var known) ? known : syncs[reader.Account.Id] = new Sync();
                if (books is not null)
                {
                    sync.Books = Kept(sync.Books, books);
                    sync.UpdatedUtc = utcNow;
                    sync.Read = tick;
                    sync.Problem = null;
                    sync.Consent = false;
                    sync.Due = tick + (long)updates.Interval.TotalMilliseconds;
                    write = true;
                }
                else
                {
                    // What it gave before stays on screen; it is asked again in a minute.
                    sync.Problem = Failures.Say(reader.Account, error!);
                    sync.Consent = error is ConsentException;
                    sync.Due = updates.RetryNow(true, online) ? tick : tick + (long)Retry.TotalMilliseconds;
                }
            }
            Rebuild(readers);
            HasAccount = readers.Count > 0;
            Loading = false;
            Problem = readers.Count == 0 ? NoAccount
                : readers.Select(reader => syncs.GetValueOrDefault(reader.Account.Id)).FirstOrDefault(sync => sync is { Books: null, Problem: not null })?.Problem
                ?? Failures.Missing(readers
                    .SelectMany(reader => syncs.GetValueOrDefault(reader.Account.Id)?.Books ?? [])
                    .Where(book => !book.Off)
                    .Select(book => (book.Book.Name, book.Failed, book.People.Count > 0, book.Incomplete)));
            counting = readers.Count > 0 && syncs.Values.All(sync => sync.Problem is null);
            nextTry = syncs.Count == 0 ? tick + (long)updates.Interval.TotalMilliseconds : syncs.Values.Min(sync => sync.Due);
            // No account, nothing to come back for: an account added wakes the store (OnAccounts).
            Arm(readers.Count == 0 ? null : TimeSpan.FromMilliseconds(Math.Max(0, nextTry - tick)));
            // Never before the file was taken in: written then, it would lose what it holds.
            if (write && cache is not null && cacheIn)
            {
                keep = Keeping(readers);
            }
            if (report is not null)
            {
                told = [.. readers.Where(reader => syncs.ContainsKey(reader.Account.Id)).Select(reader => (reader.Account.Id, Told(reader.Account.Id, syncs[reader.Account.Id])))];
            }
            (pictured, everyone) = Pictured(readers);
        }
        if (keep is not null)
        {
            cache!.Write(keep);
        }
        if (told is not null)
        {
            report!.Put(AccountData.Contacts, told);
        }
        signal.Notify();
        // The people first, their pictures after: the rows fill in as each comes.
        photos.Want(pictured, prune: everyone);
    }

    /// <summary>
    /// Under <see cref="gate"/>: everyone whose picture a line may draw, each with the account that gave
    /// them, in the order a line lists them; and whether every account has been read, so that a kept
    /// picture of nobody listed is nobody's (ContactPhotos.Want).
    /// </summary>
    private (List<(IAccountReader, Person)> People, bool Everyone) Pictured(IReadOnlyList<IAccountReader> readers)
    {
        var people = readers
            .Where(reader => syncs.TryGetValue(reader.Account.Id, out var sync) && sync.Books is not null)
            .SelectMany(reader => syncs[reader.Account.Id].Books!.Where(book => !book.Off).SelectMany(book => book.People).Select(person => (Reader: reader, Person: person)))
            .Where(each => each.Person.Photo is not null)
            .OrderBy(each => ContactSearch.Fold(each.Person.DisplayName), StringComparer.Ordinal)
            .Select(each => (each.Reader, each.Person))
            .ToList();
        return (people, readers.All(reader => syncs.TryGetValue(reader.Account.Id, out var sync) && sync.Books is not null));
    }

    /// <summary>One account's books, every one turned off giving nobody, or why not.</summary>
    private static async Task<(IAccountReader Reader, IReadOnlyList<BookRead>? Books, Exception? Error)> ReadAsync(IAccountReader reader)
    {
        try
        {
            var books = await reader.ContactsAsync(CancellationToken.None).ConfigureAwait(false);
            // A book the person turned off gives nobody, whatever its reader answered.
            return (reader, [.. books.Select(book => !book.Off && reader.Account.Syncs(SyncKeys.Book(book.Book.Id)) ? book : book with { People = [], Off = true })], null);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            return (reader, null, error);
        }
    }

    /// <summary>
    /// Under <see cref="gate"/>: one account as the Accounts page shows it (SyncReport): each book
    /// with how many it holds. An account never read in this run that fails keeps what the report
    /// said of it last, said to have failed since.
    /// </summary>
    private SyncPart Told(string account, Sync sync)
    {
        if (sync.Books is null)
        {
            var last = report?.Read().For(account)?.Contacts;
            var kept = Failures.Told(last?.UpdatedUtc, sync.Problem, sync.Consent);
            kept.Items = last?.Items ?? [];
            return kept;
        }
        var part = Failures.Told(sync.UpdatedUtc, sync.Problem, sync.Consent);
        foreach (var book in sync.Books)
        {
            part.Items.Add(new SyncItem
            {
                Id = book.Book.Id,
                Name = book.Book.Name,
                Count = book.Off ? null : book.People.Count,
                Off = book.Off,
                Problem = book.Off ? null : Failures.Part(book.Book.Name, book.Failed, book.Incomplete),
            });
        }
        return part;
    }

    /// <summary>
    /// An account's books after a read that gave <paramref name="read"/>: a book that alone failed in it keeps
    /// the people it gave before (<paramref name="before"/>), with why on it — a folder Graph refused for a
    /// while is still searchable, and the account's other books are what was just read.
    /// </summary>
    internal static IReadOnlyList<BookRead> Kept(IReadOnlyList<BookRead>? before, IReadOnlyList<BookRead> read)
    {
        var had = (before ?? []).Where(book => !book.Off).DistinctBy(book => book.Book.Id, StringComparer.Ordinal).ToDictionary(book => book.Book.Id, StringComparer.Ordinal);
        return [.. read.Select(book => book.Failed is { } failed && had.TryGetValue(book.Book.Id, out var last)
            ? last with { Book = book.Book, Failed = failed }
            : book)];
    }

    /// <summary>Under <see cref="gate"/>: what contacts-cache.json keeps of every account with books.</summary>
    private ContactCacheFile Keeping(IReadOnlyList<IAccountReader> readers)
    {
        var file = new ContactCacheFile();
        foreach (var reader in readers)
        {
            if (syncs.TryGetValue(reader.Account.Id, out var sync) && sync is { Books: { } books, UpdatedUtc: { } updated })
            {
                file.Accounts.Add(new ContactCacheAccount { Account = reader.Account.Id, UpdatedUtc = updated, Books = [.. books.Select(CachedBook.Of)] });
            }
        }
        return file;
    }

    /// <summary>Under <see cref="gate"/>: everyone of every account's last good read, each in its book's colour.</summary>
    private void Rebuild(IReadOnlyList<IAccountReader> readers)
    {
        views.Clear();
        held = [.. readers
            .Where(reader => syncs.TryGetValue(reader.Account.Id, out var sync) && sync.Books is not null)
            .SelectMany(reader => syncs[reader.Account.Id].Books!.Select(book => (reader.Account, book)))];
        Contacts = Sorted(Painted(held));
    }

    /// <summary>
    /// Everyone in the books read that <paramref name="shown"/> picks (§455; every book where it picked nothing), each
    /// with their book (<see cref="Person.Book"/>) and wearing its colour (<see cref="SourceColours"/>) where more than
    /// one book is shown — the person's pick, else the palette's by the book's place among every account's books, shown
    /// or not — so a list of people from two books tells them apart; one book alone marks nobody.
    /// </summary>
    internal static IEnumerable<Person> Painted(IReadOnlyList<(Account Account, BookRead Book)> books, SourceFilter? shown = null)
    {
        shown ??= SourceFilter.All;
        string Part((Account Account, BookRead Book) book) => WidgetSources.Key(book.Account.Address, SyncKeys.Book(book.Book.Book.Id));
        var several = books.Count(book => !book.Book.Off && shown.Shows(Part(book))) > 1;
        var colours = SourceColours.Of(books.Select(book => (book.Account, SyncKeys.Book(book.Book.Book.Id), (string?)null)));
        foreach (var each in books)
        {
            var (account, book) = each;
            if (book.Off || !shown.Shows(Part(each)))
            {
                continue;
            }
            var colour = several ? colours[SourceColours.Key(account.Id, SyncKeys.Book(book.Book.Id))].Colour : null;
            var part = Part(each);
            foreach (var person in book.People)
            {
                yield return person with { Colour = colour, Book = part };
            }
        }
    }

    /// <summary>The person picked a colour on the Accounts page: everyone of the last read wears it at once, with nothing read.</summary>
    private void OnRecoloured()
    {
        lock (gate)
        {
            Rebuild(accounts.Giving(AccountData.Contacts));
        }
        signal.Notify();
    }

    /// <summary>
    /// Everyone by the name they show under, each once: the same person in two accounts (the same
    /// name and the same first address or number) is listed once, the first account's.
    /// </summary>
    internal static IReadOnlyList<Person> Sorted(IEnumerable<Person> people)
    {
        var seen = new HashSet<(string, string?)>();
        var found = new List<Person>();
        foreach (var person in people)
        {
            if (seen.Add((person.DisplayName.ToUpperInvariant(), (person.Email ?? person.Phone)?.ToUpperInvariant())))
            {
                found.Add(person);
            }
        }
        found.Sort(static (a, b) =>
        {
            var byName = string.Compare(ContactSearch.Fold(a.DisplayName), ContactSearch.Fold(b.DisplayName), StringComparison.Ordinal);
            return byName != 0 ? byName : string.CompareOrdinal(a.Id, b.Id);
        });
        return found;
    }

    /// <summary>
    /// The timer: an account is due again, if the dock keeps the store (Keep) or somebody watches it.
    /// When nobody does, the clock stops; the next look starts it again.
    /// </summary>
    private void Due()
    {
        if (!kept && !signal.Watched && Changed is null)
        {
            return;
        }
        lock (gate)
        {
            Start();
        }
    }

    /// <summary>Under <see cref="gate"/>: the timer waits <paramref name="wait"/> before the next read, or is stopped (null).</summary>
    private void Arm(TimeSpan? wait)
    {
        Armed = wait;
        again.Change(wait ?? Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    /// <summary>
    /// The online interval was set to another: each account is due that long after its last read, at
    /// once where that has passed. A read on its way, a failure resting, a store with no account and
    /// one nobody has looked at are left as they are — each counts the new interval from its next read.
    /// </summary>
    private void OnInterval()
    {
        lock (gate)
        {
            if (!counting || working is { IsCompleted: false } || nextTry == long.MaxValue || syncs.Count == 0)
            {
                return;
            }
            foreach (var sync in syncs.Values)
            {
                sync.Due = sync.Read + (long)updates.Interval.TotalMilliseconds;
            }
            nextTry = syncs.Values.Min(sync => sync.Due);
            Arm(TimeSpan.FromMilliseconds(Math.Max(0, nextTry - Environment.TickCount64)));
        }
    }

    /// <summary>
    /// The network came back (OnlineUpdates.Back): an address book whose last read failed is read again now,
    /// not a minute later. A read on its way that fails is tried again as it ends (RetryNow).
    /// </summary>
    private void OnBack()
    {
        lock (gate)
        {
            var failed = syncs.Values.Where(sync => sync.Problem is not null).ToList();
            if (failed.Count == 0 || nextTry == long.MaxValue)
            {
                return;
            }
            foreach (var sync in failed)
            {
                sync.Due = 0;
            }
            nextTry = 0;
            if (asked)
            {
                Start();
            }
        }
    }

    /// <summary>An account was added, removed, turned or signed in again: read at once if anyone has looked.</summary>
    private void OnAccounts()
    {
        lock (gate)
        {
            if (nextTry == long.MaxValue)
            {
                return;
            }
            foreach (var sync in syncs.Values)
            {
                sync.Due = 0;
            }
            nextTry = 0;
            if (asked)
            {
                Start();
            }
        }
    }

    /// <summary>
    /// §687: accounts.json written with nothing a read depends on changed — a Reconnect's new sign-in (AccountSet.Rewritten):
    /// an account waiting for the person's consent is read at once, as To Do's is (TaskStore).
    /// </summary>
    private void OnRewritten()
    {
        lock (gate)
        {
            var waiting = syncs.Values.Where(sync => sync.Consent).ToList();
            if (waiting.Count == 0)
            {
                return;
            }
            foreach (var sync in waiting)
            {
                sync.Due = 0;
            }
            nextTry = 0;
            if (asked)
            {
                Start();
            }
        }
    }

    /// <summary>One account's sync: what it gave at its last good read, and where its next one stands.</summary>
    private sealed class Sync
    {
        /// <summary>Its books and their people, from its last good read or the cache; null before either.</summary>
        public IReadOnlyList<BookRead>? Books { get; set; }

        /// <summary>When that was read; null before it was.</summary>
        public DateTime? UpdatedUtc { get; set; }

        /// <summary>Why the last try failed; null when it went well.</summary>
        public string? Problem { get; set; }

        /// <summary>§687: the last try failed for the person's consent (ConsentException): the Accounts page offers Reconnect.</summary>
        public bool Consent { get; set; }

        /// <summary>When it is to be read again (Environment.TickCount64).</summary>
        public long Due { get; set; }

        /// <summary>
        /// When the last good read in this run finished, or the cache's on this run's clock where it is less than
        /// the interval old (PimCache.KeptRead); long.MinValue before either.
        /// </summary>
        public long Read { get; set; } = long.MinValue;
    }
}
