// Microsoft To Do's and Google Tasks' tasks, for the to-do line set to show an account's list
// (TodoTile, docs\accounts.md "To Do"): every task list of every account that gives tasks (ITaskReader),
// each list's open tasks and newest completed ones, and the changes made on the
// line that the account has not taken yet.
//
// It reads as the mail does (MailStore): from the dock's start (PimSync), each
// account its own sync at the online interval, a minute after a failure, at
// once when the accounts change, and when a line showing a list is opened and
// the last read is a minute old. A list the person turned off on the Accounts
// page is listed and not read. A list To Do refuses (a 4xx for its tasks) fails
// alone, saying why on its row and in the edit bar's errors; the account's other
// lists are read, and every list is listed with its switch (docs\widgets.md §399).
//
// It writes, as nothing else in Karpo.Dock\Pim does: a task ticked, added,
// renamed or deleted on the line is a TaskChange in the account's queue
// (Karpo.Accounts\Tasks.cs), shown at once — the server's tasks with the queue
// laid over them — and sent in order before the account is next read. A change
// the account cannot take now (no connection, a sign-in needed, a server error,
// a throttle) waits, kept on disk with the tasks, and goes when it can; only one
// it refuses for good (a 4xx but 408 and 429) is dropped. An Add whose answer
// never came is looked for in To Do before it goes again (TaskChange.Tried), so
// a retry never makes the task twice; a change over a newer version on
// the server is settled by the last writer (TaskQueue.OursWins). A list renamed or
// deleted in a to-do line's menu (§324) is shown so at once and sent there and
// then, the account read again after; it waits for nothing.
//
// An account added before Karpo asked for To Do signs in for its mail and its
// calendars but not for its tasks (MicrosoftAuth.TasksTokenAsync; a Google account whose person unticked
// Google Tasks, GoogleAuth.TokenAsync): its sync says
// so (Consent) and rests until accounts.json is written again — a reconnect on
// the Accounts page writes the account's new sign-in there — or the interval
// passes. Its mail and calendars never notice.
//
// Offline as the calendars and the mail are: tasks-cache.json (PimCache),
// DPAPI-encrypted for this Windows user, holds each account's lists, their
// tasks and the queue, taken in once before the first read. An account read
// there less than the interval ago is not read again before the interval since
// that read is up (PimCache.KeptRead) — a restart is no reason to read — while
// the changes it kept go at once: a pass that only sends reads nothing.

using Karpo.Accounts;
using Karpo.Core;

namespace Karpo.Dock.Pim;

/// <summary>What a to-do line shows of one To Do list.</summary>
internal enum TaskListState
{
    /// <summary>The list and its tasks, as read or kept.</summary>
    Shown,

    /// <summary>Nothing read yet, nor kept: the first read is on its way.</summary>
    Loading,

    /// <summary>No account of that address that gives tasks: removed, or the line names none.</summary>
    NoAccount,

    /// <summary>The account never consented to To Do: "Reconnect to sync tasks".</summary>
    Consent,

    /// <summary>The person turned the list's sync off on the Accounts page.</summary>
    Off,

    /// <summary>It could not be read and nothing was kept: <see cref="TaskListView.Problem"/> says why.</summary>
    Failed,
}

/// <summary>One To Do list as a line shows it, from memory.</summary>
/// <param name="Account">The account's id, which a change is made through; null where there is none.</param>
/// <param name="List">The list's id; null before the account's lists were read.</param>
internal sealed record TaskListView(
    TaskListState State,
    string? Account,
    string? List,
    string Name,
    IReadOnlyList<RemoteTask> Open,
    IReadOnlyList<RemoteTask> Completed,
    string? Problem,
    Freshness Freshness)
{
    public static TaskListView Nothing(TaskListState state, string? account = null, string? problem = null) =>
        new(state, account, null, S.WidgetTodoNewList, [], [], problem, default);
}

internal sealed class TaskStore : IDisposable
{
    /// <summary>Made on first use, which is on the dock's thread.</summary>
    private static readonly Lazy<TaskStore> SharedStore = new(() => new TaskStore(AccountSet.Shared, cache: PimCache.ForDock(), report: SyncReport.ForDock()));

    /// <summary>How long to wait before asking an account again after it failed.</summary>
    private static readonly TimeSpan Retry = TimeSpan.FromMinutes(1);

    /// <summary>A read younger than this is fresh enough for a line that opens.</summary>
    private static readonly TimeSpan Fresh = TimeSpan.FromMinutes(1);

    private readonly Lock gate = new();
    private readonly Lock keeping = new();
    private readonly StoreSignal signal;
    private readonly AccountSet accounts;
    private readonly Timer again;
    private readonly PimCache? cache;
    private readonly SyncReport? report;
    private readonly Func<DateTimeOffset> now;
    private readonly OnlineUpdates updates;

    /// <summary>Each account's sync, by its id.</summary>
    private readonly Dictionary<string, Sync> syncs = new(StringComparer.Ordinal);

    private Task? working;
    private Task cacheRead = Task.CompletedTask;
    private long nextTry;
    private bool asked;
    private bool cacheTaken;

    /// <summary>tasks-cache.json was read and taken in: only from then on is it written (KeepFile).</summary>
    private bool cacheIn;
    private bool more;
    private volatile bool kept;

    /// <param name="now">Now, which a change is stamped with and a read dated by; tests give their own.</param>
    internal TaskStore(AccountSet accounts, OnlineUpdates? updates = null, PimCache? cache = null, Func<DateTimeOffset>? now = null, SyncReport? report = null)
    {
        this.accounts = accounts;
        this.updates = updates ?? OnlineUpdates.Shared;
        this.cache = cache;
        this.report = report;
        this.now = now ?? (() => Clock.RealOffsetUtc());
        signal = new StoreSignal(() => Changed?.Invoke());
        again = new Timer(_ => Troubles.Guard("Tasks: reading them again", Due));
        accounts.Changed += OnAccounts;
        accounts.Rewritten += OnRewritten;
        this.updates.Changed += OnInterval;
        this.updates.Back += OnBack;
    }

    /// <summary>The store the dock uses, made on first use.</summary>
    public static TaskStore Shared => SharedStore.Value;

    /// <summary>For tests: how long the timer was last set to wait before the next pass; null before it was, or while it is stopped.</summary>
    internal TimeSpan? Armed { get; private set; }

    /// <summary>The tasks changed, or reading them did. Raised on the dock's thread.</summary>
    public event Action? Changed;

    /// <summary>A line shows a list: it is woken when the tasks change. Attaching reads nothing.</summary>
    public void Attach(CellIsland island) => signal.Add(island);

    public void Release(CellIsland island) => signal.Remove(island);

    /// <summary>On the dock's thread: raises <see cref="Changed"/> once for all news since the last time.</summary>
    internal void Deliver() => signal.Deliver();

    /// <summary>For the life of the dock (PimSync): the tasks are read whether or not a line shows them.</summary>
    public void Keep()
    {
        kept = true;
        Refresh();
    }

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
    /// Somebody is looking: the first read starts, or the next one when it is due. Returns at once.
    /// </summary>
    /// <param name="fresh">A line opening: every account last tried more than a minute ago is read again.</param>
    /// <param name="force">Every account is read again now, however recently it was.</param>
    public void Refresh(bool fresh = false, bool force = false)
    {
        lock (gate)
        {
            if (fresh || force)
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

    /// <summary>The work in flight, for tests.</summary>
    internal Task Idle()
    {
        lock (gate)
        {
            return Task.WhenAll(working ?? Task.CompletedTask, keeps);
        }
    }

    /// <summary>The writes of tasks-cache.json a change asked for off the caller's thread (KeepSoon): what Idle waits for too.</summary>
    private Task keeps = Task.CompletedTask;

    /// <summary>tasks-cache.json written as the store is now, on the pool: the caller is the dock's thread.</summary>
    private void KeepSoon()
    {
        var write = Task.Run(() => Troubles.Guard("Tasks: keeping a change", KeepFile));
        lock (gate)
        {
            keeps = Task.WhenAll(keeps, write);
        }
    }

    // --- What a line shows --------------------------------------------------------------------------

    /// <summary>
    /// The list <paramref name="list"/> of the account <paramref name="address"/> — its
    /// default list where <paramref name="list"/> is empty or gone — from memory, at once.
    /// </summary>
    public TaskListView Show(string address, string? list)
    {
        // The accounts are in memory from the start: no account of that address is known at once.
        if (Reader(address) is not { } reader)
        {
            return TaskListView.Nothing(TaskListState.NoAccount);
        }
        var account = reader.Account;
        lock (gate)
        {
            if (!syncs.TryGetValue(account.Id, out var sync) || sync.Lists is null)
            {
                return sync switch
                {
                    { Consent: true } => TaskListView.Nothing(TaskListState.Consent, account.Id),
                    { Problem: { } problem } => TaskListView.Nothing(TaskListState.Failed, account.Id, problem),
                    _ => TaskListView.Nothing(TaskListState.Loading, account.Id),
                };
            }
            var shown = Pick(sync.Lists, list);
            if (shown is null)
            {
                return TaskListView.Nothing(sync.Consent ? TaskListState.Consent : TaskListState.Failed, account.Id, sync.Problem);
            }
            if (!account.Syncs(SyncKeys.TaskList(shown.Id)))
            {
                return new TaskListView(TaskListState.Off, account.Id, shown.Id, shown.Name, [], [], null, default);
            }
            if (sync.Consent && !sync.Tasks.ContainsKey(shown.Id))
            {
                return new TaskListView(TaskListState.Consent, account.Id, shown.Id, shown.Name, [], [], null, default);
            }
            // §399: a list To Do refused shows why, over what it had where it had anything.
            var why = sync.Problem ?? sync.Refused.GetValueOrDefault(shown.Id);
            if (why is not null && !sync.Tasks.ContainsKey(shown.Id))
            {
                return new TaskListView(TaskListState.Failed, account.Id, shown.Id, shown.Name, [], [], why, default);
            }
            var tasks = TaskQueue.Apply(sync.Tasks.GetValueOrDefault(shown.Id) ?? [], sync.Pending, shown.Id);
            var freshness = sync.UpdatedUtc is { } updated ? new Freshness(updated, why is not null || sync.Consent) : default;
            return new TaskListView(TaskListState.Shown, account.Id, shown.Id, shown.Name, TaskQueue.Open(tasks), TaskQueue.Completed(tasks), why, freshness);
        }
    }

    /// <summary>The list <paramref name="id"/> of <paramref name="lists"/>; else the default one; else the first. Pure.</summary>
    internal static RemoteTaskList? Pick(IReadOnlyList<RemoteTaskList> lists, string? id) =>
        lists.FirstOrDefault(list => !string.IsNullOrEmpty(id) && list.Id == id)
        ?? lists.FirstOrDefault(list => list.Default)
        ?? lists.FirstOrDefault();

    /// <summary>
    /// §687: what the account <paramref name="address"/> calls its tasks ("Microsoft To Do", "Google Tasks"), which a
    /// task's or a list's delete names; Microsoft's for an account not known.
    /// </summary>
    public string Service(string address) => ConsentException.Service(Reader(address)?.Account.Kind ?? AccountKind.Microsoft, AccountData.Tasks);

    /// <summary>The account of <paramref name="address"/> whose tasks are read; null where there is none.</summary>
    private ITaskReader? Reader(string address) =>
        Readers().FirstOrDefault(reader => string.Equals(reader.Account.Address, address, StringComparison.OrdinalIgnoreCase));

    private IReadOnlyList<ITaskReader> Readers() => [.. accounts.Giving(AccountData.Tasks).OfType<ITaskReader>()];

    // --- Changes ------------------------------------------------------------------------------------

    /// <summary>A task saying <paramref name="title"/> at the end of the list; its id until the server gives it one; null for words of spaces.</summary>
    public string? Add(string account, string list, string title)
    {
        var words = Notebook.TaskText(title);
        if (words.Length == 0)
        {
            return null;
        }
        var id = TaskQueue.NewId();
        Change(account, new TaskChange(TaskChangeKind.Add, list, id, words, false, now()));
        return id;
    }

    /// <summary>The task ticked off, or put back.</summary>
    public void SetDone(string account, string list, string task, bool done) =>
        Change(account, new TaskChange(TaskChangeKind.Done, list, task, null, done, now()));

    /// <summary>The task's words; words of spaces delete it, as the local list's do.</summary>
    public void Rename(string account, string list, string task, string title)
    {
        var words = Notebook.TaskText(title);
        Change(account, words.Length == 0
            ? new TaskChange(TaskChangeKind.Delete, list, task, null, false, now())
            : new TaskChange(TaskChangeKind.Rename, list, task, words, false, now()));
    }

    public void Delete(string account, string list, string task) =>
        Change(account, new TaskChange(TaskChangeKind.Delete, list, task, null, false, now()));

    /// <summary>Queued, shown at once, kept on disk, and sent now where the account answers.</summary>
    private void Change(string account, TaskChange change)
    {
        lock (gate)
        {
            if (!syncs.TryGetValue(account, out var sync))
            {
                syncs[account] = sync = new Sync();
            }
            sync.Queue = TaskQueue.Enqueue(sync.Queue, change);
            // Sent now; the account is read again only when it is due.
            nextTry = 0;
            Start();
        }
        signal.Notify();
        if (cache is not null)
        {
            KeepSoon();
        }
    }

    // --- Lists (§324) -------------------------------------------------------------------------------

    /// <summary>
    /// §324: the list <paramref name="list"/> of the account <paramref name="address"/> called
    /// <paramref name="name"/> (one line, trimmed, as a notebook's list's name), shown so at once — on the
    /// lines, in the menus and on the Accounts page — and sent to To Do now. Never its default list, which
    /// To Do keeps as it is. False where nothing changed. Unlike a task's change it waits for nothing: where
    /// To Do does not take it (no connection), the account's next read shows the list as To Do has it.
    /// </summary>
    public bool RenameList(string address, string list, string name)
    {
        var words = Notebook.Name(name);
        if (words.Length == 0 || Reader(address) is not { } reader)
        {
            return false;
        }
        lock (gate)
        {
            if (!syncs.TryGetValue(reader.Account.Id, out var sync) || sync.Lists is not { } lists
                || lists.FirstOrDefault(one => one.Id == list) is not { Default: false } found || found.Name == words)
            {
                return false;
            }
            sync.Lists = [.. lists.Select(one => one.Id == list ? one with { Name = words } : one)];
        }
        Shown();
        SendList(reader, cancel => reader.RenameListAsync(list, words, cancel));
        return true;
    }

    /// <summary>
    /// §324: the list <paramref name="list"/> of the account <paramref name="address"/> deleted in To Do,
    /// its tasks and the changes waiting for them with it, gone at once from what is shown; a line showing
    /// it shows the account's default list. Never the default list. False where there is no such list.
    /// </summary>
    public bool DeleteList(string address, string list)
    {
        if (Reader(address) is not { } reader)
        {
            return false;
        }
        lock (gate)
        {
            if (!syncs.TryGetValue(reader.Account.Id, out var sync) || sync.Lists is not { } lists
                || !lists.Any(one => one.Id == list && !one.Default))
            {
                return false;
            }
            sync.Lists = [.. lists.Where(one => one.Id != list)];
            sync.Tasks = sync.Tasks.Where(pair => pair.Key != list).ToDictionary(StringComparer.Ordinal);
            sync.Queue = [.. sync.Queue.Where(change => change.List != list)];
        }
        Shown();
        SendList(reader, cancel => reader.DeleteListAsync(list, cancel));
        return true;
    }

    /// <summary>§324: what is shown changed: the lines told, tasks-cache.json and the Accounts page's report written.</summary>
    private void Shown()
    {
        signal.Notify();
        if (cache is not null)
        {
            KeepSoon();
        }
        Report(Readers());
    }

    /// <summary>§324: a list's change sent to To Do now; the account read again after, whether it took it or not.</summary>
    private void SendList(ITaskReader reader, Func<CancellationToken, Task> send) =>
        _ = Task.Run(() => Troubles.Guard("Tasks: changing a list", async () =>
        {
            try
            {
                await send(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception error) when (Failures.Expected(error))
            {
                Console.Error.WriteLine($"Karpo: a To Do list was not changed ({error.GetType().Name}).");
            }
            lock (gate)
            {
                if (syncs.TryGetValue(reader.Account.Id, out var sync))
                {
                    sync.Due = 0;
                }
                nextTry = 0;
                Start();
            }
        }));

    /// <summary>
    /// tasks-cache.json written as the store is now. One write at a time, each of what is there when it
    /// starts, so a write of a moment ago never lands over a newer one.
    /// </summary>
    private void KeepFile()
    {
        lock (keeping)
        {
            TaskCacheFile file;
            lock (gate)
            {
                if (!cacheIn)
                {
                    // What the file holds is not taken in yet: written now, it would lose it.
                    return;
                }
                file = Kept();
            }
            cache!.Write(file);
        }
    }

    // --- Reading and sending ------------------------------------------------------------------------

    /// <summary>Under <see cref="gate"/>: starts a pass unless one is running or nothing is due.</summary>
    private void Start()
    {
        if (working is { IsCompleted: false })
        {
            // A change made while a pass runs is sent by one more pass after it.
            more |= syncs.Values.Any(sync => sync.Queue.Count > 0);
            return;
        }
        if (Environment.TickCount64 < nextTry)
        {
            return;
        }
        asked = true;
        if (!cacheTaken)
        {
            cacheTaken = true;
            cacheRead = cache is null ? Task.CompletedTask : Task.Run(() => Troubles.Guard("Tasks: taking in what was kept", TakeCache));
        }
        var first = cacheRead;
        working = Task.Run(() => Troubles.Guard("Tasks: reading them", async () =>
        {
            await first.ConfigureAwait(false);
            while (true)
            {
                await WorkAsync().ConfigureAwait(false);
                lock (gate)
                {
                    if (!more)
                    {
                        return;
                    }
                    more = false;
                }
            }
        }));
    }

    /// <summary>tasks-cache.json, once, before the first read: the lists, tasks and queue of every account still here.</summary>
    private void TakeCache()
    {
        // Not while the accounts are not known (accounts.json held at the start) or the file is held by
        // another program: taken at the next pass, and nothing is written over it before (KeepFile) — it
        // is the only copy of the changes not sent yet.
        if (!accounts.Known || cache!.ReadTasks() is not { } file)
        {
            lock (gate)
            {
                cacheTaken = false;
            }
            return;
        }
        var here = Readers().Select(reader => reader.Account.Id).ToHashSet(StringComparer.Ordinal);
        var took = false;
        lock (gate)
        {
            cacheIn = true;
            var tick = Environment.TickCount64;
            var utcNow = now().UtcDateTime;
            foreach (var account in file.Accounts)
            {
                if (!here.Contains(account.Account))
                {
                    continue;
                }
                if (!syncs.TryGetValue(account.Account, out var sync))
                {
                    syncs[account.Account] = sync = new Sync();
                }
                if (sync.Lists is null && account.Lists.Count > 0)
                {
                    sync.Lists = [.. account.Lists.Select(list => list.Remote)];
                    sync.Tasks = account.Lists.Where(list => list.Read).ToDictionary(list => list.Id, list => (IReadOnlyList<RemoteTask>)list.Tasks, StringComparer.Ordinal);
                    sync.UpdatedUtc = DateTime.SpecifyKind(account.UpdatedUtc, DateTimeKind.Utc);
                    // Read less than the interval ago: as good as a read of this run, due the interval after it and
                    // not now (PimCache.KeptRead) — and tried now, so the bar's lines showing at the start are no
                    // line opened on an old read (Refresh's fresh). Changes kept still go at once: sending is not
                    // reading (PassAsync). Not over a try of this run, which keeps its own rest.
                    if (sync.Tried == long.MinValue && PimCache.KeptRead(sync.UpdatedUtc.Value, utcNow, updates.Interval, tick) is { } read)
                    {
                        sync.Read = read;
                        sync.Tried = tick;
                        sync.Due = read + (long)updates.Interval.TotalMilliseconds;
                    }
                }
                // A change made before this run's first read goes after what was kept.
                sync.Queue = [.. account.Queue, .. sync.Queue];
                took = true;
            }
        }
        if (took)
        {
            signal.Notify();
        }
    }

    /// <summary>Every account that is due, or has changes to send: each on its own, side by side.</summary>
    private async Task WorkAsync()
    {
        var readers = Readers();
        List<ITaskReader> due;
        lock (gate)
        {
            var tick = Environment.TickCount64;
            due = [.. readers.Where(reader => !syncs.TryGetValue(reader.Account.Id, out var sync) || sync.Due <= tick || sync.Queue.Count > 0 && !sync.Consent)];
        }

        await Task.WhenAll(due.Select(PassAsync)).ConfigureAwait(false);

        // Written where an account was passed or went: a start that finds all it kept fresh passes nobody, and writes
        // nothing back over the file it has just read.
        var write = due.Count > 0;
        lock (gate)
        {
            var tick = Environment.TickCount64;
            var here = readers.Select(reader => reader.Account.Id).ToHashSet(StringComparer.Ordinal);
            foreach (var gone in syncs.Keys.Where(id => !here.Contains(id)).ToList())
            {
                syncs.Remove(gone);
                write = true;
            }
            nextTry = syncs.Count == 0 ? tick + (long)updates.Interval.TotalMilliseconds : syncs.Values.Min(sync => sync.Due);
            Arm(readers.Count == 0 ? null : TimeSpan.FromMilliseconds(Math.Max(0, nextTry - tick)));
        }
        if (cache is not null && write)
        {
            KeepFile();
        }
        Report(readers);
        signal.Notify();
    }

    /// <summary>The Accounts page's report (SyncReport) of <paramref name="readers"/>' tasks, as the store has them now.</summary>
    private void Report(IReadOnlyList<ITaskReader> readers)
    {
        if (report is null)
        {
            return;
        }
        List<(string, SyncPart)> told;
        lock (gate)
        {
            told = [.. readers.Where(reader => syncs.ContainsKey(reader.Account.Id)).Select(reader => (reader.Account.Id, Told(reader.Account, syncs[reader.Account.Id])))];
        }
        report.Put(AccountData.Tasks, told);
    }

    /// <summary>One account: its changes sent, then, where that went and it is due, its lists and tasks read.</summary>
    private async Task PassAsync(ITaskReader reader)
    {
        // As this pass starts: a failure is passed again at once if the network came back meanwhile (RetryNow).
        var online = updates.Comebacks;
        var account = reader.Account;
        Sync sync;
        bool due;
        lock (gate)
        {
            if (!syncs.TryGetValue(account.Id, out sync!))
            {
                syncs[account.Id] = sync = new Sync();
            }
            due = sync.Due <= Environment.TickCount64;
        }
        string? problem = null;
        var consent = false;
        try
        {
            await SendAsync(reader, sync).ConfigureAwait(false);
            if (!due)
            {
                // Only changes to send: what they came to is shown, and the read waits its turn.
                lock (gate)
                {
                    sync.Problem = null;
                }
                return;
            }
            IReadOnlyList<RemoteTaskList> lists;
            try
            {
                lists = await reader.TaskListsAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (AccountException refusal) when (Refusal(refusal))
            {
                throw new AccountException(S.DockAccountsTaskListsRefused(service: Service(account.Address), said: refusal.Message), refusal.Status);
            }
            var read = new Dictionary<string, IReadOnlyList<RemoteTask>>(StringComparer.Ordinal);
            var refusing = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var list in lists.Where(list => account.Syncs(SyncKeys.TaskList(list.Id))))
            {
                try
                {
                    read[list.Id] = await reader.TasksAsync(list.Id, CancellationToken.None).ConfigureAwait(false);
                }
                catch (AccountException refusal) when (Refusal(refusal))
                {
                    // §399: To Do will not give this list (no sign-in, no connection: those fail the account): it fails
                    // alone, keeping what it had, and says so on its row — the others are read, every list is listed.
                    refusing[list.Id] = S.DockAccountsTaskListRefused(service: Service(account.Address), list: list.Name, said: refusal.Message);
                    lock (gate)
                    {
                        if (sync.Tasks.TryGetValue(list.Id, out var had))
                        {
                            read[list.Id] = had;
                        }
                    }
                }
            }
            // Changes made while it read are laid over what it read, and sent next time.
            lock (gate)
            {
                sync.Lists = lists;
                sync.Tasks = read;
                sync.Refused = refusing;
                sync.UpdatedUtc = now().UtcDateTime;
            }
        }
        catch (ConsentException)
        {
            consent = true;
        }
        catch (Exception error) when (Failures.Expected(error))
        {
            problem = Failures.Say(account, error);
        }
        lock (gate)
        {
            var tick = Environment.TickCount64;
            sync.Tried = tick;
            sync.Problem = problem;
            sync.Consent = consent;
            // Consent rests for the interval: only signing in again mends it, which OnRewritten hears. A list refused
            // (Refused) waits the interval too: asking again in a minute would hear the same.
            sync.Due = problem is not null && updates.RetryNow(true, online) ? tick : tick + (long)(problem is null ? updates.Interval : Retry).TotalMilliseconds;
            if (problem is null && !consent)
            {
                sync.Read = tick;
            }
        }
    }

    /// <summary>
    /// §399: To Do said no to the request itself (a 4xx), for good: not the sign-in, the network or a server
    /// down — and not "too slow" (408) or "too many" (429, a Retry-After longer than Graph waits out), which
    /// are a later answer, not a no.
    /// </summary>
    internal static bool Refusal(AccountException error) => error.Status is >= 400 and < 500 and not 408 and not 429;

    /// <summary>How far before a change's time a task made by it may be dated by To Do: this PC's clock and the server's disagree a little.</summary>
    private static readonly TimeSpan Skew = TimeSpan.FromMinutes(10);

    /// <summary>
    /// The account's queue sent in order, each change taken off as it goes. Only a change the account
    /// refuses for good (<see cref="Refusal"/>) is dropped. Anything else — no connection, a sign-in, a
    /// server error after Graph's own tries, a long throttle, an answer that did not read — puts the change
    /// back and stops, and throws: the account shows why, and the change goes at its next pass, however
    /// many it takes. An Add that may have reached To Do goes back marked (<see cref="TaskChange.Tried"/>).
    /// </summary>
    private async Task SendAsync(ITaskReader reader, Sync sync)
    {
        while (true)
        {
            TaskChange change;
            lock (gate)
            {
                if (sync.Queue.Count == 0)
                {
                    return;
                }
                change = sync.Queue[0];
                sync.Queue = [.. sync.Queue.Skip(1)];
                sync.Sending = change;
            }
            try
            {
                var result = await SendOneAsync(reader, sync, change).ConfigureAwait(false);
                lock (gate)
                {
                    sync.Sending = null;
                    Took(sync, change, result);
                }
            }
            catch (AccountException refused) when (Refusal(refused))
            {
                // The account will not take it (a list gone, a word it refuses): it is dropped, not sent
                // for ever, and the next read shows what the account has.
                Console.Error.WriteLine($"Karpo: a To Do change was refused ({refused.Status}).");
                lock (gate)
                {
                    sync.Sending = null;
                }
            }
            catch (Exception error) when (Failures.Expected(error))
            {
                // Neither a sign-in to make nor the consent asked for reached To Do; anything else may have.
                var unsure = change.Kind == TaskChangeKind.Add && error is not (ConsentException or SignInRequiredException);
                lock (gate)
                {
                    sync.Sending = null;
                    sync.Queue = [unsure ? change with { Tried = true } : change, .. sync.Queue];
                }
                throw;
            }
        }
    }

    /// <summary>What sending one change came to: the task as the server has it after, or gone (null), or nothing to say.</summary>
    private readonly record struct Sent(bool Known, RemoteTask? Task);

    private async Task<Sent> SendOneAsync(ITaskReader reader, Sync sync, TaskChange change)
    {
        var cancel = CancellationToken.None;
        string? etag;
        lock (gate)
        {
            etag = sync.Tasks.GetValueOrDefault(change.List)?.FirstOrDefault(task => task.Id == change.Task)?.ETag;
        }
        if (change.Kind == TaskChangeKind.Add)
        {
            if (change.Tried && await MadeAsync(reader, sync, change).ConfigureAwait(false) is { } made)
            {
                // To Do made it the time its answer never came: that task is this one, and nothing is sent.
                return new Sent(true, made);
            }
            return new Sent(true, await reader.AddTaskAsync(change.List, change.Title ?? "", change.Done, cancel).ConfigureAwait(false));
        }
        if (TaskQueue.IsNew(change.Task))
        {
            // Its Add never went (refused): nothing to change there.
            return default;
        }
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                if (change.Kind == TaskChangeKind.Delete)
                {
                    await reader.DeleteTaskAsync(change.List, change.Task, etag, cancel).ConfigureAwait(false);
                    return new Sent(true, null);
                }
                var edit = change.Kind == TaskChangeKind.Rename ? new TaskEdit(Title: change.Title) : new TaskEdit(Done: change.Done);
                return new Sent(true, await reader.UpdateTaskAsync(change.List, change.Task, edit, etag, cancel).ConfigureAwait(false));
            }
            catch (TaskConflictException) when (attempt == 0)
            {
                // Changed there since it was read: the last writer wins.
                var current = await reader.TaskAsync(change.List, change.Task, cancel).ConfigureAwait(false);
                if (!TaskQueue.OursWins(change, current))
                {
                    return new Sent(true, current);
                }
                etag = current!.ETag;
            }
            catch (TaskConflictException)
            {
                // Changed there again meanwhile: that is the newer one.
                return new Sent(true, await reader.TaskAsync(change.List, change.Task, cancel).ConfigureAwait(false));
            }
        }
    }

    /// <summary>
    /// The task an Add sent once (<see cref="TaskChange.Tried"/>) made after all, where To Do has one: of its
    /// words, made since the Add was (give or take the clocks' <see cref="Skew"/>), and none the store already
    /// knows — which a task another Add of the same words was found to be is. Graph takes no key a request
    /// could be known again by, so this is how the retry tells "made" from "never reached it". The oldest
    /// such, where there are several; null where there is none, and the Add is sent again.
    /// </summary>
    private async Task<RemoteTask?> MadeAsync(ITaskReader reader, Sync sync, TaskChange change)
    {
        var there = await reader.TasksAsync(change.List, CancellationToken.None).ConfigureAwait(false);
        var words = (change.Title ?? "").Trim();
        HashSet<string> known;
        lock (gate)
        {
            known = [.. (sync.Tasks.GetValueOrDefault(change.List) ?? []).Select(task => task.Id)];
        }
        return there
            .Where(task => !known.Contains(task.Id) && task.Title == words && task.Created >= change.At - Skew)
            .OrderBy(task => task.Created)
            .FirstOrDefault();
    }

    /// <summary>Under <see cref="gate"/>: what the server said of a change sent, put into what is shown.</summary>
    private static void Took(Sync sync, TaskChange change, Sent sent)
    {
        if (!sent.Known)
        {
            return;
        }
        var tasks = new List<RemoteTask>(sync.Tasks.GetValueOrDefault(change.List) ?? []);
        var at = tasks.FindIndex(task => task.Id == change.Task);
        if (change.Kind == TaskChangeKind.Add && sent.Task is { } made)
        {
            // The changes after it that name its own id name the server's now.
            sync.Queue = TaskQueue.Rebind(sync.Queue, change.Task, made.Id);
            tasks.Add(made);
        }
        else if (sent.Task is { } changed)
        {
            if (at >= 0)
            {
                tasks[at] = changed;
            }
            else
            {
                tasks.Add(changed);
            }
        }
        else if (at >= 0)
        {
            tasks.RemoveAt(at);
        }
        sync.Tasks = new Dictionary<string, IReadOnlyList<RemoteTask>>(sync.Tasks, StringComparer.Ordinal) { [change.List] = tasks };
    }

    /// <summary>One account as the Accounts page shows it (SyncReport): each list with its open tasks, or off.</summary>
    private static SyncPart Told(Account account, Sync sync)
    {
        var part = Failures.Told(sync.UpdatedUtc, sync.Problem, sync.Consent);
        foreach (var list in sync.Lists ?? [])
        {
            var off = !account.Syncs(SyncKeys.TaskList(list.Id));
            part.Items.Add(new SyncItem
            {
                Id = list.Id,
                Name = list.Name,
                Off = off,
                Default = list.Default ? true : null,
                Count = !off && sync.Tasks.TryGetValue(list.Id, out var tasks) ? tasks.Count(task => !task.Done) : null,
                Problem = off ? null : sync.Refused.GetValueOrDefault(list.Id),
            });
        }
        return part;
    }

    /// <summary>Under <see cref="gate"/>: what tasks-cache.json keeps of every account.</summary>
    private TaskCacheFile Kept()
    {
        var file = new TaskCacheFile();
        foreach (var (id, sync) in syncs)
        {
            if (sync.Lists is null && sync.Queue.Count == 0 && sync.Sending is null)
            {
                continue;
            }
            file.Accounts.Add(new TaskCacheAccount
            {
                Account = id,
                UpdatedUtc = sync.UpdatedUtc ?? default,
                Lists = [.. (sync.Lists ?? []).Select(list => CachedTaskList.Of(list, sync.Tasks.GetValueOrDefault(list.Id)))],
                // What is on its way goes first: it is sent again where the dock ends before it lands.
                Queue = [.. sync.Sending is { } sending ? [sending] : Array.Empty<TaskChange>(), .. sync.Queue],
            });
        }
        return file;
    }

    /// <summary>The timer: an account is due again, while anything keeps the store or a line shows it.</summary>
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

    private void Arm(TimeSpan? wait)
    {
        Armed = wait;
        again.Change(wait ?? Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    /// <summary>The online interval changed: each account is due that long after its last good read.</summary>
    private void OnInterval()
    {
        lock (gate)
        {
            if (working is { IsCompleted: false } || syncs.Count == 0 || !asked)
            {
                return;
            }
            foreach (var sync in syncs.Values.Where(sync => sync.Problem is null && sync.Read != long.MinValue))
            {
                sync.Due = sync.Read + (long)updates.Interval.TotalMilliseconds;
            }
            nextTry = syncs.Values.Min(sync => sync.Due);
            Arm(TimeSpan.FromMilliseconds(Math.Max(0, nextTry - Environment.TickCount64)));
        }
    }

    /// <summary>
    /// The network came back (OnlineUpdates.Back): an account whose last read failed is read again now,
    /// not a minute later. A read on its way that fails is tried again as it ends (RetryNow).
    /// </summary>
    private void OnBack()
    {
        lock (gate)
        {
            var failed = syncs.Values.Where(sync => sync.Problem is not null && !sync.Consent).ToList();
            if (failed.Count == 0)
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

    /// <summary>An account was added, removed, or a part of one turned off or on: read at once.</summary>
    private void OnAccounts()
    {
        lock (gate)
        {
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
    /// accounts.json was written with nothing a store cares about changed — a token written back, or
    /// an account signed in to again for its tasks (Reconnect): an account waiting for consent tries now.
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

    public void Dispose()
    {
        accounts.Changed -= OnAccounts;
        accounts.Rewritten -= OnRewritten;
        updates.Changed -= OnInterval;
        updates.Back -= OnBack;
        again.Dispose();
    }

    /// <summary>One account's sync: its lists, their tasks, its queue, and where its next read stands.</summary>
    private sealed class Sync
    {
        /// <summary>Every task list, from the last good read or the cache; null before either.</summary>
        public IReadOnlyList<RemoteTaskList>? Lists { get; set; }

        /// <summary>Each list's tasks as the server has them, by the list's id; a list turned off has none.</summary>
        public IReadOnlyDictionary<string, IReadOnlyList<RemoteTask>> Tasks { get; set; } = new Dictionary<string, IReadOnlyList<RemoteTask>>(StringComparer.Ordinal);

        /// <summary>§399: why To Do refused each list it refused at the last read of the lists, by the list's id.</summary>
        public IReadOnlyDictionary<string, string> Refused { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>The changes not sent yet, in the order they were made.</summary>
        public IReadOnlyList<TaskChange> Queue { get; set; } = [];

        /// <summary>The change on its way to the server, shown as if taken.</summary>
        public TaskChange? Sending { get; set; }

        /// <summary>The changes a view lays over the server's tasks: the one on its way, then the queue.</summary>
        public IEnumerable<TaskChange> Pending => Sending is { } sending ? [sending, .. Queue] : Queue;

        public DateTime? UpdatedUtc { get; set; }

        public string? Problem { get; set; }

        /// <summary>The account signs in, but not for its tasks.</summary>
        public bool Consent { get; set; }

        public long Due { get; set; }

        public long Read { get; set; } = long.MinValue;

        public long Tried { get; set; } = long.MinValue;
    }
}
