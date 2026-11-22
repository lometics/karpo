// The accounts the dock reads, shared by the calendar, contacts and mail
// stores: one reader per account in accounts.json, made again whenever the
// settings app adds or removes one.
//
// **An isolated run has none, by construction.** Only Shared is made from
// the person's accounts, and only when the config is theirs: a test or
// --smoke (KARPO_CONFIG_DIR) gets a set with no reader in it, so a widget
// drawn offscreen reaches no server however often it asks — the same rule
// that keeps such a run away from the person's pins, drives and startup
// entries. Tests that want accounts hand a set their own fake readers.
//
// **An account the file did not say is not gone.** accounts.json held by a
// virus scanner or a backup at the moment it is read, or broken by a hand edit
// under way, is no news: the set keeps the readers it had (and a busy file is
// read again in a moment). Taken for no accounts, every store would drop the
// accounts' events, mail, contacts and tasks, and write its cache without them
// — the To Do changes not sent yet with them.

using Karpo.Accounts;
using Karpo.Core;

namespace Karpo.Dock.Pim;

internal sealed class AccountSet : IDisposable
{
    private static readonly Lazy<AccountSet> SharedSet = new(() => Config.IsIsolated ? new AccountSet([]) : Live());

    /// <summary>accounts.json held by another program: read again this much later, until it reads.</summary>
    private static readonly TimeSpan Again = TimeSpan.FromSeconds(2);

    private readonly Lock gate = new();
    private readonly Lock reloading = new();
    private readonly AccountWatch? watch;
    private readonly Timer? again;
    private readonly Func<(AccountsRead Read, IReadOnlyList<IAccountReader> Readers)>? load;
    private IReadOnlyList<IAccountReader> readers;
    private string shape;
    private string paint;
    private volatile bool known;

    /// <summary>A set of exactly these readers, which never changes: what a test holds.</summary>
    internal AccountSet(IReadOnlyList<IAccountReader> readers)
    {
        this.readers = readers;
        shape = Shape(readers.Select(reader => reader.Account));
        paint = Paint(readers.Select(reader => reader.Account));
        known = true;
    }

    /// <summary>
    /// A set read by <paramref name="load"/> now and whenever <paramref name="watch"/> hears the file
    /// change: the dock's (<see cref="Live"/>), or a test's over a file of its own. A file that does not
    /// read as it is (<see cref="AccountsRead.Busy"/>, <see cref="AccountsRead.Broken"/>) leaves the set
    /// as it was — empty and not <see cref="Known"/> at the start — and a busy one is read again soon.
    /// </summary>
    internal AccountSet(Func<(AccountsRead Read, IReadOnlyList<IAccountReader> Readers)> load, AccountWatch? watch)
    {
        this.load = load;
        this.watch = watch;
        again = new Timer(_ => Troubles.Guard("Accounts: reading them again", Reload));
        readers = [];
        shape = Shape([]);
        paint = Paint([]);
        var (read, fresh) = load();
        if (Reads(read))
        {
            readers = fresh;
            shape = Shape(fresh.Select(reader => reader.Account));
            paint = Paint(fresh.Select(reader => reader.Account));
            known = true;
        }
        else if (read == AccountsRead.Busy)
        {
            again.Change(Again, Timeout.InfiniteTimeSpan);
        }
        if (watch is not null)
        {
            watch.Changed += Reload;
        }
    }

    /// <summary>The dock's set: the person's accounts, or none on an isolated config.</summary>
    public static AccountSet Shared => SharedSet.Value;

    /// <summary>
    /// The accounts are not what they were: one added, removed, or signed in again. Raised on a
    /// pool thread. A token written back on its own is not a change.
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// Only the colours the person picked for parts of the accounts are not what they were: nothing
    /// needs reading, what is shown is drawn again in them. Raised on a pool thread.
    /// </summary>
    public event Action? Recoloured;

    /// <summary>
    /// accounts.json was written and nothing a store reads by changed: a token written back, or an
    /// account signed in to again for its tasks (TaskStore). Raised on a pool thread.
    /// </summary>
    public event Action? Rewritten;

    /// <summary>Whether this set reads the person's own accounts file (false for tests and isolated runs).</summary>
    internal bool IsLive => load is not null;

    /// <summary>
    /// The readers are the accounts accounts.json said when it last read: false only while the file
    /// has not read since the dock started (another program held it, or it does not read). A store
    /// takes nothing from its cache and writes nothing over it before: an account it would not find
    /// among none is not gone. <see cref="Changed"/> is said when it becomes true.
    /// </summary>
    internal bool Known => known;

    /// <summary>Every account's reader, in the order the accounts were added.</summary>
    public IReadOnlyList<IAccountReader> Readers
    {
        get
        {
            lock (gate)
            {
                return readers;
            }
        }
    }

    /// <summary>The readers of the accounts that give <paramref name="data"/>.</summary>
    public IReadOnlyList<IAccountReader> Giving(AccountData data) =>
        [.. Readers.Where(reader => reader.Account.Gives.HasFlag(data))];

    public void Dispose()
    {
        watch?.Dispose();
        again?.Dispose();
    }

    /// <summary>
    /// For tests: the set is <paramref name="now"/> from here on, said as accounts.json changing to
    /// it would be. Nothing in the dock calls it.
    /// </summary>
    internal void Become(IReadOnlyList<IAccountReader> now)
    {
        var shaped = Shape(now.Select(reader => reader.Account));
        var painted = Paint(now.Select(reader => reader.Account));
        bool recoloured;
        lock (gate)
        {
            // Only a colour picked is drawn, not read, as accounts.json changing so would be.
            recoloured = shaped == shape && painted != paint;
            readers = now;
            shape = shaped;
            paint = painted;
        }
        (recoloured ? Recoloured : Changed)?.Invoke();
    }

    /// <summary>For tests: accounts.json written with nothing changed, as a reconnect writes it.</summary>
    internal void Rewrite() => Rewritten?.Invoke();

    private static AccountSet Live() => new(
        static () => (AccountBook.Load(out var accounts), [.. accounts.Select(Karpo.Accounts.Readers.For)]),
        new AccountWatch());

    /// <summary>What the file said is the accounts: it read, or there is none. Not when it is held or does not read.</summary>
    private static bool Reads(AccountsRead read) => read is AccountsRead.Ok or AccountsRead.Missing;

    /// <summary>
    /// accounts.json changed: read again, and said where the accounts are not what they were. On
    /// the watch's timer, a pool thread with nobody above it: whatever reading the file or a store
    /// hearing the news throws is reported, and the set keeps the readers it had. So does a file that
    /// is held by another program — read again in a moment, as the change heard is not known yet — or
    /// does not read (a hand edit under way: read again when it is saved): an account the file did
    /// not say is not one gone, and no store drops what it holds of it.
    /// </summary>
    internal void Reload()
    {
        try
        {
            // One at a time: the watch's timer and the one for a busy file may come together.
            lock (reloading)
            {
                ReloadNow();
            }
        }
        catch (Exception error)
        {
            Troubles.Report("Accounts: reading them again", error);
        }
    }

    private void ReloadNow()
    {
        var (read, fresh) = load!();
        if (!Reads(read))
        {
            if (read == AccountsRead.Busy)
            {
                again!.Change(Again, Timeout.InfiniteTimeSpan);
            }
            Console.Error.WriteLine($"Karpo: accounts.json {(read == AccountsRead.Busy ? "held by another program" : "does not read")}; the accounts stay as they were");
            return;
        }
        // The first read since a start the file did not read at: whatever it says is news to the stores.
        var first = !known;
        bool recoloured;
        var now = Shape(fresh.Select(reader => reader.Account));
        var painted = Paint(fresh.Select(reader => reader.Account));
        var rewritten = false;
        lock (gate)
        {
            // Readers read their secret when they ask, so a token written back needs no new reader;
            // a set whose shape is the same keeps its readers and says only that it was written.
            if (now == shape && !first)
            {
                rewritten = painted == paint;
            }
        }
        if (rewritten)
        {
            Rewritten?.Invoke();
            return;
        }
        lock (gate)
        {
            if (now == shape && !first)
            {
                // A colour picked: the new readers carry it, and the stores draw it without a read.
                readers = fresh;
                paint = painted;
                recoloured = true;
            }
            else
            {
                readers = fresh;
                shape = now;
                paint = painted;
                recoloured = false;
            }
        }
        // After the readers: a store that sees the set known sees what the file said.
        known = true;
        (recoloured ? Recoloured : Changed)?.Invoke();
    }

    /// <summary>
    /// What a store cares about in the accounts: which there are, where they read from and what of
    /// them the person turned off. A password is part of it (typing it again is a change); a refresh token (Microsoft, Google)
    /// is not, because Karpo writes Microsoft's back every hour — signing in again adds the account anew, and a Reconnect
    /// is heard as accounts.json rewritten (Rewritten).
    /// </summary>
    internal static string Shape(IEnumerable<Account> accounts) => string.Join('\n', accounts.Select(account =>
        string.Join('|',
            account.Id,
            account.Kind,
            account.Address,
            account.User,
            account.Host,
            account.Port,
            account.Security,
            account.CalendarHome,
            account.BookHome,
            account.InBrowser ? "" : account.Secret,
            // A part turned off or on again — §639: a mail folder turned on —: fetched again, or dropped, at once.
            string.Join(',', account.Off ?? []),
            string.Join(',', account.On ?? []))));

    /// <summary>The colours the person picked, account by account: a change in them alone is drawn, not read (<see cref="Recoloured"/>).</summary>
    internal static string Paint(IEnumerable<Account> accounts) => string.Join('\n', accounts.Select(account =>
        account.Id + "|" + string.Join(',', (account.Colours ?? new Dictionary<string, string>())
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => pair.Key + "=" + pair.Value))));
}
