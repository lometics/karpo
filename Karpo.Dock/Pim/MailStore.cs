// The mail, shared by the mail line and the mail face: every account's inbox,
// merged newest first, and how many are unread in all.
//
// It follows ContactStore, with one difference in when it reads: the dock keeps
// it reading from its start (PimSync, Keep), whether or not a mail widget is
// placed, so every inbox is synced for as long as it runs. Each inbox is its
// own sync: read at the one interval every online widget follows
// (OnlineUpdates), a minute after it failed, at once when the accounts change,
// and when the panel opens and its last try is more than a minute old. With no
// account that gives mail the timer stops until one is added. A change to the
// interval re-arms the timer at once.
//
// Offline (docs\accounts.md): an inbox that cannot be read keeps what it gave
// last on screen, and Freshness says so — the stale mark (StaleMark.cs) with
// when it was read. Every good read writes the newest messages, as a row shows
// them, to mail-cache.json (PimCache); the first read of a run takes them back
// in first, on the pool, so a start without a connection shows them, and an
// inbox read there less than the interval ago waits out the rest of it
// (PimCache.KeptRead): a restart is no reason to read.
//
// §639: an account syncs the folders the person turned on in ⚙ › Accounts beside its inbox (Account.On), and each
// read lists every folder the while (Karpo.Accounts' MailAsync). A mail widget picks among what is synced ("Show
// from", Karpo.Core\WidgetSources.cs, §455): its messages and unread count are its view of what is held (View), the
// inboxes' alone where it picked nothing; a mail face counts what its folder's mail line shows.
//
// Read-only, all of it: nothing is marked read, moved or opened. A row in the
// panel is a line to look at, not a door — no program on Windows can be asked
// to show "this message of that account", so the panel does not pretend to.

using Karpo.Accounts;
using Karpo.Core;

namespace Karpo.Dock.Pim;

internal sealed class MailStore : IDisposable
{
    /// <summary>What the panel says while no account has mail.</summary>
    internal static string NoAccount => S.DockAccountsNoMailAccount;

    /// <summary>What the panel says while every account's mail is turned off on the Accounts page.</summary>
    internal static string AllOff => S.DockAccountsMailAllOff;

    /// <summary>Messages kept from each inbox and shown in all.</summary>
    internal const int Newest = 30;

    /// <summary>Messages of each inbox shown at least, however much newer every other inbox's are (<see cref="Merged"/>).</summary>
    internal const int EachInbox = 5;

    /// <summary>Made on first use, which is on the dock's thread.</summary>
    private static readonly Lazy<MailStore> SharedStore = new(() => new MailStore(AccountSet.Shared, cache: PimCache.ForDock(), report: SyncReport.ForDock()));

    /// <summary>How long to wait before asking an account again after it failed.</summary>
    private static readonly TimeSpan Retry = TimeSpan.FromMinutes(1);

    /// <summary>A read younger than this is fresh enough for a panel that opens.</summary>
    private static readonly TimeSpan Fresh = TimeSpan.FromMinutes(1);

    private readonly Lock gate = new();
    private readonly StoreSignal signal;
    private readonly AccountSet accounts;
    private readonly Timer again;
    private readonly PimCache? cache;
    private readonly SyncReport? report;
    private readonly Func<DateTime> now;

    // How often the inboxes are read while somebody is looking: every online widget's interval,
    // with no floor of its own — the setting's least, 5 minutes, is what the mail always did, and
    // IMAP servers and Graph take a read that often without complaint. Neither is asked to push:
    // Karpo holds no IMAP IDLE connection and no Graph subscription, so the interval is all there is.
    private readonly OnlineUpdates updates;

    /// <summary>Each inbox's sync, by the account's id: what it gave last, and when it is due again.</summary>
    private readonly Dictionary<string, Sync> syncs = new(StringComparer.Ordinal);

    /// <summary>§455: each widget's view of what is held, by what it picked (<see cref="SourceFilter.Id"/>); made again after every change.</summary>
    private readonly Dictionary<string, MailView> views = new(StringComparer.Ordinal);

    private Task? working;
    private Task cacheRead = Task.CompletedTask;
    private long nextTry;
    private bool asked;
    private bool cacheTaken;

    /// <summary>mail-cache.json was read and taken in: only from then on is it written.</summary>
    private bool cacheIn;

    // Whether the timer is counting the interval from the last reads, rather than resting after a
    // failure or stopped for want of an account: only then does a new interval move it.
    private bool counting;

    // The dock keeps the mail read for as long as it runs (PimSync), so a mail widget in a folder
    // nobody has opened, or placed later, draws the inboxes at once; a test's store reads on while
    // somebody watches it.
    private volatile bool kept;

    /// <param name="updates">The interval every online widget follows: the dock's, or a test's own.</param>
    /// <param name="cache">Where the newest messages are kept for a start without a connection; none keeps nothing.</param>
    /// <param name="now">UTC now, which a read is stamped with; tests give their own.</param>
    /// <param name="report">Where what was synced is told to the Accounts page; none tells nobody.</param>
    internal MailStore(AccountSet accounts, OnlineUpdates? updates = null, PimCache? cache = null, Func<DateTime>? now = null, SyncReport? report = null)
    {
        this.accounts = accounts;
        this.updates = updates ?? OnlineUpdates.Shared;
        this.cache = cache;
        this.report = report;
        this.now = now ?? (() => Clock.RealUtc());
        signal = new StoreSignal(() => Changed?.Invoke());
        again = new Timer(_ => Troubles.Guard("Mail: reading it again", Due));
        accounts.Changed += OnAccounts;
        accounts.Recoloured += OnRecoloured;
        accounts.Rewritten += OnRewritten;
        this.updates.Changed += OnInterval;
        this.updates.Back += OnBack;
    }

    /// <summary>For tests: how long the timer was last set to wait before the next read; null before it was, or while it is stopped.</summary>
    internal TimeSpan? Armed { get; private set; }

    /// <summary>The store the dock uses, made on first use.</summary>
    public static MailStore Shared => SharedStore.Value;

    /// <summary>The mail changed, or reading it did. Raised on the dock's thread.</summary>
    public event Action? Changed;

    /// <summary>The newest messages of every inbox, newest first. Empty before the first read.</summary>
    public IReadOnlyList<MailItem> Messages { get; private set; } = [];

    /// <summary>How many messages are unread, in every inbox together.</summary>
    public int Unread { get; private set; }

    /// <summary>Nothing has been read yet, nor kept from before, and nothing has gone wrong: the first read is on its way.</summary>
    public bool Loading { get; private set; }

    /// <summary>
    /// Why something is missing, in words for the person; null while all is well. An inbox that
    /// failed with messages kept from an earlier read is not missing: it is <see cref="Freshness"/>'s stale.
    /// </summary>
    public string? Problem { get; private set; }

    /// <summary>When the messages shown were read, and whether a refresh failed since (docs\accounts.md, "Offline").</summary>
    public Freshness Freshness { get; private set; }

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

    /// <summary>Whether any account gives mail at all, as the last read found.</summary>
    public bool HasAccount { get; private set; } = true;

    /// <summary>
    /// §455: what a widget that picked <paramref name="shown"/> shows: the messages of the inboxes and folders it picked,
    /// newest first, and how many of them are unread; <see cref="Messages"/> and <see cref="Unread"/> where it picked nothing.
    /// </summary>
    public MailView View(SourceFilter shown)
    {
        lock (gate)
        {
            if (shown.Everything)
            {
                return new MailView(Messages, Unread);
            }
            if (!views.TryGetValue(shown.Id, out var view))
            {
                views[shown.Id] = view = Viewed(shown);
            }
            return view;
        }
    }

    /// <summary>An island shows the mail: it is woken when it changes. Attaching reads nothing.</summary>
    public void Attach(CellIsland island) => signal.Add(island);

    /// <summary>The dock's start (PimSync): the first read starts now, and the timer goes on for good.</summary>
    public void Keep()
    {
        kept = true;
        Refresh();
    }

    /// <summary>That island is gone.</summary>
    public void Release(CellIsland island) => signal.Remove(island);

    /// <summary>
    /// Somebody is looking: the first read starts, or the next one when it is due. Returns at once;
    /// safe to call from the dock's thread as often as it likes.
    /// </summary>
    /// <param name="fresh">A panel opening: every inbox last tried more than a minute ago is read again.</param>
    /// <param name="force">Every inbox is read again now, however recently it was.</param>
    public void Refresh(bool fresh = false, bool force = false)
    {
        lock (gate)
        {
            if ((fresh || force) && nextTry != long.MaxValue)
            {
                var tick = Environment.TickCount64;
                foreach (var sync in syncs.Values)
                {
                    if (force || sync.Tried == long.MinValue || tick - sync.Tried > (long)Fresh.TotalMilliseconds)
                    {
                        sync.Due = 0;
                        nextTry = 0;
                    }
                }
            }
            Start();
        }
    }

    /// <summary>On the dock's thread: raises <see cref="Changed"/> once for all news since the last time.</summary>
    internal void Deliver() => signal.Deliver();

    /// <summary>The work in flight, for tests.</summary>
    internal Task Idle()
    {
        lock (gate)
        {
            return working ?? Task.CompletedTask;
        }
    }

    /// <summary>For tests: the cache taken in, which comes before the first read of an inbox.</summary>
    internal Task CacheRead()
    {
        lock (gate)
        {
            return cacheRead;
        }
    }

    /// <summary>
    /// For tests: the store as a read that found <paramref name="messages"/> leaves it. Nothing is due
    /// after it, so a widget drawn over it reads nothing. Nothing in the dock calls it.
    /// </summary>
    internal void Answered(IReadOnlyList<MailItem> messages, int unread, Freshness freshness = default)
    {
        lock (gate)
        {
            asked = true;
            cacheTaken = true;
            Messages = messages;
            Unread = unread;
            Loading = false;
            Problem = null;
            Freshness = freshness;
            HasAccount = true;
            nextTry = long.MaxValue;
        }
        signal.Notify();
    }

    /// <summary>
    /// For tests: the store as a read that found no account with mail leaves it. Nothing is due
    /// after it. Nothing in the dock calls it.
    /// </summary>
    internal void NoAccountFound()
    {
        lock (gate)
        {
            asked = true;
            cacheTaken = true;
            Messages = [];
            Unread = 0;
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
            // Before the first read: what was kept, on the pool, so a widget never waits for a file.
            cacheTaken = true;
            cacheRead = cache is null ? Task.CompletedTask : Task.Run(() => Troubles.Guard("Mail: taking in what was kept", TakeCache));
        }
        var first = cacheRead;
        working = Task.Run(() => Troubles.Guard("Mail: reading it", async () =>
        {
            await first.ConfigureAwait(false);
            await WorkAsync().ConfigureAwait(false);
        }));
    }

    /// <summary>
    /// mail-cache.json, once, before the first read: the newest messages of every account still
    /// here, as they were last read. An inbox read since is left as it was read.
    /// </summary>
    private void TakeCache()
    {
        // Not while the accounts are not known (accounts.json held at the start) or the file is held by
        // another program: taken before a later read, and nothing is written over it before.
        if (!accounts.Known || cache!.ReadMail() is not { } file)
        {
            lock (gate)
            {
                cacheTaken = false;
            }
            return;
        }
        var here = Synced().Select(reader => reader.Account.Id).ToHashSet(StringComparer.Ordinal);
        var took = false;
        lock (gate)
        {
            cacheIn = true;
            var tick = Environment.TickCount64;
            var utcNow = now();
            foreach (var account in file.Accounts)
            {
                // Taken too where a read of this run failed before the file could be: its sync holds nothing yet.
                if (!here.Contains(account.Account) || syncs.TryGetValue(account.Account, out var had) && had.UpdatedUtc is not null)
                {
                    continue;
                }
                var sync = had ?? (syncs[account.Account] = new Sync());
                sync.Inbox = account.Inbox;
                sync.Boxes = (account.Folders ?? []).ToDictionary(folder => folder.Id, folder => new FolderRead(new MailFolder(folder.Id, folder.Name ?? folder.Id), folder.Mail), StringComparer.Ordinal);
                // §683: every folder it listed, as the last read listed them — a file of before none, its synced ones.
                sync.Listed = account.Listed is { } listed
                    ? [.. listed.Select(folder => sync.Boxes.GetValueOrDefault(folder.Id) is { } box ? box : new FolderRead(new MailFolder(folder.Id, folder.Name, Unread: folder.Unread, Total: folder.Total), null))]
                    : [.. sync.Boxes.Values];
                sync.UpdatedUtc = DateTime.SpecifyKind(account.UpdatedUtc, DateTimeKind.Utc);
                // Read less than the interval ago: as good as a read of this run, due the interval after it and not
                // now (PimCache.KeptRead) — and tried now, so the start is no panel opening on an old read. Not over
                // a failure of this run, which keeps its rest.
                if (had is null && PimCache.KeptRead(sync.UpdatedUtc.Value, utcNow, updates.Interval, tick) is { } read)
                {
                    sync.Read = read;
                    sync.Tried = tick;
                    sync.Due = read + (long)updates.Interval.TotalMilliseconds;
                }
                took = true;
            }
            if (took)
            {
                Rebuild();
                Loading = false;
            }
        }
        if (took)
        {
            signal.Notify();
        }
    }

    /// <summary>Asks every inbox that is due for its newest messages, each on its own.</summary>
    private async Task WorkAsync()
    {
        // As this read starts: a failure is read again at once if the network came back meanwhile (RetryNow).
        var online = updates.Comebacks;
        var giving = accounts.Giving(AccountData.Mail);
        var readers = Synced();
        List<IAccountReader> due;
        lock (gate)
        {
            var tick = Environment.TickCount64;
            due = [.. readers.Where(reader => !syncs.TryGetValue(reader.Account.Id, out var sync) || sync.Due <= tick)];
        }

        // One sync per account, side by side: a slow server holds up nobody else's mail.
        var answers = await Task.WhenAll(due.Select(ReadAsync)).ConfigureAwait(false);

        MailCacheFile? keep = null;
        List<(string, SyncPart)>? told = null;
        lock (gate)
        {
            var tick = Environment.TickCount64;
            var utcNow = now();
            var here = readers.Select(reader => reader.Account.Id).ToHashSet(StringComparer.Ordinal);
            // An inbox removed stops: its sync goes, and so does what the file kept of it.
            var write = false;
            foreach (var gone in syncs.Keys.Where(id => !here.Contains(id)).ToList())
            {
                syncs.Remove(gone);
                write = true;
            }
            foreach (var (reader, read, error) in answers)
            {
                var sync = syncs.TryGetValue(reader.Account.Id, out var known) ? known : syncs[reader.Account.Id] = new Sync();
                sync.Tried = tick;
                if (read is not null)
                {
                    // Null where the person turned the inbox off and only folders on (§639).
                    sync.Inbox = read.FirstOrDefault(folder => folder.Folder.Inbox)?.Mail;
                    // §639: every folder listed; of those synced what each gave, one that failed alone keeping what it had.
                    var had = sync.Boxes;
                    sync.Listed = [.. read.Where(folder => !folder.Folder.Inbox)];
                    sync.Boxes = sync.Listed
                        .Select(folder => folder.Mail is not null ? folder : folder.Failed is not null && had.GetValueOrDefault(folder.Folder.Id) is { } kept ? kept with { Folder = folder.Folder } : null)
                        .OfType<FolderRead>()
                        .ToDictionary(folder => folder.Folder.Id, StringComparer.Ordinal);
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
            Rebuild();
            HasAccount = giving.Count > 0;
            Loading = false;
            Problem = giving.Count == 0 ? NoAccount
                : readers.Count == 0 ? AllOff
                : readers.Select(reader => syncs.GetValueOrDefault(reader.Account.Id)).FirstOrDefault(sync => sync is { UpdatedUtc: null, Problem: not null })?.Problem;
            counting = readers.Count > 0 && syncs.Values.All(sync => sync.Problem is null);
            nextTry = syncs.Count == 0 ? tick + (long)updates.Interval.TotalMilliseconds : syncs.Values.Min(sync => sync.Due);
            // No account, nothing to come back for: an account added wakes the store (OnAccounts).
            Arm(readers.Count == 0 ? null : TimeSpan.FromMilliseconds(Math.Max(0, nextTry - tick)));
            // Never before the file was taken in: written then, it would lose what it holds.
            if (write && cache is not null && cacheIn)
            {
                keep = Kept(readers);
            }
            if (report is not null)
            {
                told = [.. readers.Where(reader => syncs.ContainsKey(reader.Account.Id)).Select(reader => (reader.Account.Id, Told(reader.Account, syncs[reader.Account.Id])))];
            }
        }
        if (keep is not null)
        {
            cache!.Write(keep);
        }
        if (told is not null)
        {
            // An account whose mail is all turned off is in no part: the Accounts page reads it off the account.
            report!.Put(AccountData.Mail, told);
        }
        signal.Notify();
    }

    /// <summary>
    /// One account's mail as the Accounts page shows it (SyncReport): its inbox, how many it holds and how many are
    /// unread, or that it is off; and its other folders (§639), each synced or not, with its counts where it was read or
    /// the account listed them.
    /// </summary>
    private static SyncPart Told(Account account, Sync sync)
    {
        var part = Failures.Told(sync.UpdatedUtc, sync.Problem, sync.Consent);
        if (sync.UpdatedUtc is null)
        {
            return part;
        }
        part.Items.Add(sync.Inbox is { } inbox && account.Syncs(SyncKeys.Inbox)
            ? new SyncItem { Id = SyncKeys.Inbox, Name = S.CoreAccountsInbox, Count = inbox.Total, Unread = inbox.Unread }
            : new SyncItem { Id = SyncKeys.Inbox, Name = S.CoreAccountsInbox, Off = true });
        part.Folders = sync.Listed.Count == 0 ? null
            : [.. sync.Listed.Select(folder =>
            {
                var mail = sync.Boxes.GetValueOrDefault(folder.Folder.Id)?.Mail;
                return new SyncItem
                {
                    Id = folder.Folder.Id,
                    Name = folder.Folder.Name,
                    Count = mail?.Total ?? folder.Folder.Total,
                    Unread = mail?.Unread ?? folder.Folder.Unread,
                    Off = !account.Syncs(SyncKeys.MailFolder(folder.Folder.Id)),
                    Problem = folder.Failed,
                };
            })];
        return part;
    }

    /// <summary>The accounts whose mail is read: those that give mail, but for any whose inbox and folders the person all turned off (§639).</summary>
    private IReadOnlyList<IAccountReader> Synced() =>
        [.. accounts.Giving(AccountData.Mail).Where(reader => reader.Account.SyncsMail)];

    /// <summary>One account's inbox and the folders it syncs, or why not.</summary>
    private static async Task<(IAccountReader Reader, IReadOnlyList<FolderRead>? Read, Exception? Error)> ReadAsync(IAccountReader reader)
    {
        try
        {
            return (reader, await reader.MailAsync(Newest, CancellationToken.None).ConfigureAwait(false), null);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            return (reader, null, error);
        }
    }

    /// <summary>Under <see cref="gate"/>: the messages and the count from every inbox's last good read, and how fresh that is.</summary>
    private void Rebuild()
    {
        views.Clear();
        (Messages, Unread) = Viewed(SourceFilter.All);
        Freshness = Freshness.Of(syncs.Values
            .Where(sync => sync.UpdatedUtc is not null)
            .Select(sync => (sync.UpdatedUtc!.Value, sync.Problem is not null)));
    }

    /// <summary>Under <see cref="gate"/>: what mail-cache.json keeps of every account's mail read.</summary>
    private MailCacheFile Kept(IReadOnlyList<IAccountReader> readers)
    {
        var file = new MailCacheFile();
        foreach (var reader in readers)
        {
            if (syncs.TryGetValue(reader.Account.Id, out var sync) && sync is { UpdatedUtc: { } updated })
            {
                file.Accounts.Add(PimCache.Keep(reader.Account.Id, updated, sync.Inbox ?? new Inbox(0, []), sync.Boxes.Values, sync.Listed));
            }
        }
        return file;
    }

    /// <summary>
    /// Under <see cref="gate"/>: every inbox and folder held that <paramref name="shown"/> picks (§455: the inboxes where
    /// it picked nothing) and its account syncs (§639: a part turned off shows nowhere, not even what was kept of it), by
    /// part (<see cref="MailItem.Part"/>), with its name.
    /// </summary>
    private List<(Account Account, string Part, string Name, Inbox Mail)> Boxes(IReadOnlyList<Account> giving, SourceFilter shown)
    {
        var boxes = new List<(Account Account, string Part, string Name, Inbox Mail)>();
        foreach (var account in giving)
        {
            if (!syncs.TryGetValue(account.Id, out var sync) || sync.UpdatedUtc is null)
            {
                continue;
            }
            var inbox = WidgetSources.Key(account.Address, SyncKeys.Inbox);
            if (sync.Inbox is { } mail && account.Syncs(SyncKeys.Inbox) && shown.Shows(inbox))
            {
                boxes.Add((account, inbox, S.CoreAccountsInbox, mail));
            }
            foreach (var (id, read) in sync.Boxes)
            {
                var folder = WidgetSources.Key(account.Address, SyncKeys.MailFolder(id));
                if (read.Mail is { } held && account.Syncs(SyncKeys.MailFolder(id)) && shown.Shows(folder, byDefault: false))
                {
                    boxes.Add((account, folder, read.Folder.Name, held));
                }
            }
        }
        return boxes;
    }

    /// <summary>
    /// The messages of <paramref name="boxes"/>, each with its part (<see cref="MailItem.Part"/>), wearing its account's
    /// inbox's colour (<see cref="SourceColours"/>) where more than one account's are shown — the person's pick, else the
    /// palette's by the inbox's place among every account that gives mail, shown or not, so turning one off recolours no
    /// other; one account's mail marks nothing —, and §639 its folder's name where more than one is shown and not
    /// inboxes alone (<see cref="MailItem.Folder"/>).
    /// </summary>
    private static IEnumerable<IEnumerable<MailItem>> Painted(IReadOnlyList<Account> giving, List<(Account Account, string Part, string Name, Inbox Mail)> boxes)
    {
        var several = boxes.Select(box => box.Account.Id).Distinct(StringComparer.Ordinal).Count() > 1;
        var named = boxes.Count > 1 && boxes.Any(box => !box.Part.EndsWith("|" + SyncKeys.Inbox, StringComparison.Ordinal));
        var colours = SourceColours.Of(giving.Select(account => (account, SyncKeys.Inbox, (string?)null)));
        foreach (var (account, part, name, mail) in boxes)
        {
            var colour = several ? colours[SourceColours.Key(account.Id, SyncKeys.Inbox)].Colour : null;
            var folder = named ? name : null;
            yield return mail.Messages.Select(message => message with { Colour = colour, Part = part, Folder = folder });
        }
    }

    /// <summary>Under <see cref="gate"/>: what a widget that picked <paramref name="shown"/> shows of what is held (<see cref="View"/>).</summary>
    private MailView Viewed(SourceFilter shown)
    {
        var giving = accounts.Giving(AccountData.Mail).Select(reader => reader.Account).ToList();
        var boxes = Boxes(giving, shown);
        return new MailView(Merged(Painted(giving, boxes)), boxes.Sum(box => box.Mail.Unread));
    }

    /// <summary>The person picked a colour on the Accounts page: every message wears it at once, with nothing read.</summary>
    private void OnRecoloured()
    {
        lock (gate)
        {
            Rebuild();
        }
        signal.Notify();
    }

    /// <summary>
    /// Every inbox's messages together, newest first: the <see cref="Newest"/> of all of them, and of every
    /// inbox at least its own newest <see cref="EachInbox"/> among them. Only the newest of all, a quiet
    /// inbox beside a busy one showed nothing — its newest a day old, the other's thirty of the last hour —
    /// as if it were not read at all; now it has its own few at the foot of the list, where they belong by
    /// their time.
    /// </summary>
    internal static IReadOnlyList<MailItem> Merged(IEnumerable<IEnumerable<MailItem>> inboxes)
    {
        var each = inboxes.Select(inbox => inbox.OrderByDescending(message => message.Received).ToList()).ToList();
        var kept = new HashSet<MailItem>(ReferenceEqualityComparer.Instance);
        kept.UnionWith(each.SelectMany(inbox => inbox).OrderByDescending(message => message.Received).Take(Newest));
        kept.UnionWith(each.SelectMany(inbox => inbox.Take(EachInbox)));
        return [.. kept.OrderByDescending(message => message.Received)];
    }

    /// <summary>
    /// The timer: an inbox is due again, if the dock keeps the store (Keep) or somebody watches it.
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
    /// The online interval was set to another: each inbox is due that long after its last read, at
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
    /// The network came back (OnlineUpdates.Back): an inbox whose last read failed is read again now,
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

    /// <summary>An account was added, removed or signed in again: read at once if anyone has looked.</summary>
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

    /// <summary>One inbox's sync: what it gave at its last good read, and where its next one stands.</summary>
    private sealed class Sync
    {
        /// <summary>Its inbox's unread count and newest messages, from its last good read or the cache; null before either, or where it is off.</summary>
        public Inbox? Inbox { get; set; }

        /// <summary>§639: its other folders as the last good read listed them, with their counts where the account gave them.</summary>
        public IReadOnlyList<FolderRead> Listed { get; set; } = [];

        /// <summary>§639: what each folder it syncs gave at its last good read or the cache, by its id.</summary>
        public Dictionary<string, FolderRead> Boxes { get; set; } = new(StringComparer.Ordinal);

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

        /// <summary>When it was last tried, well or not, or taken from the cache as a read of this run.</summary>
        public long Tried { get; set; } = long.MinValue;
    }
}

/// <summary>§455: what one mail widget shows of the store (<see cref="MailStore.View"/>): its messages, newest first, and how many of them are unread.</summary>
internal sealed record MailView(IReadOnlyList<MailItem> Messages, int Unread);
