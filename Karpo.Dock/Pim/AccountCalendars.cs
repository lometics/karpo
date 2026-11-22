// The calendars of the person's accounts (Karpo.Accounts), read by Karpo
// itself.
//
// Each account that has calendars is asked for them and for what they hold in
// one window of time, which covers every calendar and grows when a view looks
// further; the work is done on the pool, Changed is raised through the store's
// StoreSignal, and the views only ever read a snapshot already in memory.
// Each account is its own sync: read at the one interval every online widget
// follows (OnlineUpdates), again a minute after it failed, and at once when the
// accounts change; a slow or failing account holds up nobody else's events. A
// change to the interval re-arms the timer at once.
//
// Offline (docs\accounts.md): what an account gave last stays on screen when
// its next read fails, and Freshness says so — the stale mark (StaleMark.cs)
// with when it was read. Every good read of a Microsoft account writes the
// events of the window to calendar-cache.json (PimCache); a CalDAV calendar is
// kept whole, a file of its own (CalendarMirror). The first read of a run takes
// both back in first, so a start without a connection shows them — and an
// account read there less than the interval ago is not read again until the
// interval since that read is up (PimCache.KeptRead): a restart is no reason
// to read.
//
// Each calendar is its own part of its account's read (Karpo.Accounts' Parts):
// one the account will not give — a shared calendar taken back, one gone, a
// server error for it — keeps what it held, stale, with why on its row, while
// the account's other calendars refresh; once its 403 failed the whole account,
// which then stayed as it was for good. A calendar not all there (the server cut
// its answer short, an event Karpo cannot show) is shown with a note that says so.
//
// A Microsoft calendar answers occurrences in a window — Graph's calendarView
// expands a series itself — and the window grows when a view looks further. A
// CalDAV calendar is kept whole and synced (ICalendarSync, docs\widgets.md
// §616): the store hands over what it holds and puts in what changed, parsed on
// the pool into an EventIndex, and expanded by ICalendar, Karpo's own RFC 5545
// code, in the zone the calendar is shown in, only near what a view shows. It
// has no window: every year is there, and a view looking further reads nothing.
// A first download shows its events in steps, a thousand at a time.
//
// Refresh is what starts the first read. The dock calls it at its start
// (PimSync), whether or not a calendar widget is placed, so the events of every
// account are synced for as long as it runs; with no account that gives
// calendars the timer stops until one is added. Want, which a view calls while
// drawing, only remembers what was asked for.
//
// What keeps --smoke and the offscreen pictures away from the person's
// accounts is not this file: only CalendarStore.Shared holds one of these, and
// only with the person's own AccountSet (docs\accounts.md).

using Karpo.Accounts;
using Karpo.Core;
using Color = Windows.UI.Color;

namespace Karpo.Dock.Pim;

/// <summary>An occurrence as Karpo draws it, in a zone.</summary>
internal static class Occurrences
{
    /// <summary><paramref name="occurrence"/> as wall-clock times in <paramref name="zone"/>.</summary>
    public static (DateTime Start, DateTime End) In(this Occurrence occurrence, TimeZoneInfo zone) =>
        occurrence.AllDay || occurrence.Floating
            ? (occurrence.Start, occurrence.End)
            : (TimeZoneInfo.ConvertTimeFromUtc(occurrence.Start, zone), TimeZoneInfo.ConvertTimeFromUtc(occurrence.End, zone));
}

/// <summary>
/// Karpo's reading of the accounts' calendars: one <see cref="AccountCalendar"/> per calendar, and
/// one window of events covering all of them, each account read on its own and kept until its next
/// good read.
/// </summary>
internal sealed class AccountCalendars : IDisposable
{
    /// <summary>What a calendar says while no account has one.</summary>
    internal static string NoAccount => S.DockAccountsNoCalendarAccount;

    /// <summary>How long to wait before asking an account again after it failed.</summary>
    private static readonly TimeSpan Retry = TimeSpan.FromMinutes(1);

    /// <summary>Months either side of this one the window may ever grow to.</summary>
    private const int MostMonths = 60;

    private readonly Lock gate = new();
    private readonly AccountSet accounts;
    private readonly Func<DateTime> now;
    private readonly Timer again;
    private readonly PimCache? cache;
    private readonly CalendarMirror mirror;
    private readonly SyncReport? report;

    // How often the window is read again while somebody is looking: every online widget's
    // interval, with no floor of its own — CalDAV servers and Graph answer a read every few
    // minutes without complaint, and the setting's least (5 minutes) is what the mail always did.
    private readonly OnlineUpdates updates;

    /// <summary>The calendars by their id, kept between reads so a source's identity (and colour) is stable.</summary>
    private readonly Dictionary<string, AccountCalendar> known = new(StringComparer.Ordinal);

    /// <summary>Each account's sync, by the account's id: what it gave last, and when it is due again.</summary>
    private readonly Dictionary<string, Sync> syncs = new(StringComparer.Ordinal);

    /// <summary>What each calendar holds in the window, by its id.</summary>
    private Dictionary<string, Held> held = new(StringComparer.Ordinal);

    private Task? working;
    private Task cacheRead = Task.CompletedTask;
    private CancellationTokenSource stopping = new();
    private long nextTry;
    private bool opened;
    private bool running;
    private bool stale;
    private bool cacheTaken;

    /// <summary>The CalDAV calendars kept were taken in: once a run, before the first sync of any.</summary>
    private bool mirrorsTaken;

    /// <summary>calendar-cache.json was read and taken in: only from then on is it written.</summary>
    private bool cacheIn;
    private DateTime from;
    private DateTime to;

    /// <param name="now">UTC now, which the window is measured from; tests give their own.</param>
    /// <param name="updates">The interval every online widget follows: the dock's, or a test's own.</param>
    /// <param name="cache">Where the events are kept for a start without a connection; none keeps nothing.</param>
    /// <param name="report">Where what was synced is told to the Accounts page; none tells nobody.</param>
    /// <param name="mirror">Where the CalDAV calendars are kept whole; none keeps them in memory only.</param>
    internal AccountCalendars(AccountSet accounts, Func<DateTime>? now = null, OnlineUpdates? updates = null, PimCache? cache = null, SyncReport? report = null, CalendarMirror? mirror = null)
    {
        this.accounts = accounts;
        this.now = now ?? (() => Clock.RealUtc());
        this.updates = updates ?? OnlineUpdates.Shared;
        this.cache = cache;
        this.mirror = mirror ?? new CalendarMirror(null);
        this.report = report;
        // The cache's window: what a start finds kept is what it would read (PimCache.Window).
        (from, to) = PimCache.Window(this.now());
        again = new Timer(_ => Troubles.Guard("Calendars: reading them again", Due));
        accounts.Changed += OnAccounts;
        accounts.Recoloured += OnRecoloured;
        accounts.Rewritten += OnRewritten;
        this.updates.Changed += OnInterval;
        this.updates.Back += OnBack;
    }

    /// <summary>For tests: how long the timer was last set to wait before the next read; null before it was.</summary>
    internal TimeSpan? Armed { get; private set; }

    /// <summary>The calendars, or the events in them, are not what they were. Raised on any thread.</summary>
    public event Action? Changed;

    /// <summary>The calendars, account by account in the order they were added. Empty before the first read.</summary>
    public IReadOnlyList<AccountCalendar> Calendars { get; private set; } = [];

    /// <summary>Nothing has been read yet, nor kept from before, and nothing has gone wrong: the first read is on its way.</summary>
    public bool Loading { get; private set; }

    /// <summary>
    /// Why something is missing, in words for the person; null while all is well. An account that
    /// failed with events kept from an earlier read is not missing: it is <see cref="Freshness"/>'s stale.
    /// </summary>
    public string? Problem { get; private set; }

    /// <summary>When the events shown were read, and whether a refresh failed since (docs\accounts.md, "Offline").</summary>
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

    /// <summary>Whether any account gives calendars at all, as the last read found; true before one has.</summary>
    public bool HasAccount { get; private set; } = true;

    /// <summary>
    /// Starts the first read, or the next one when it is due, and returns at once. Safe to call from
    /// the dock's thread as often as it likes: a read already running is not started again.
    /// </summary>
    /// <param name="force">Somebody asked for it (a Refresh): every account is read, whatever is due.</param>
    public void Refresh(bool force = false)
    {
        lock (gate)
        {
            if (!opened)
            {
                opened = true;
                Loading = true;
            }
            if (force)
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

    /// <summary>
    /// A view asked for [<paramref name="wantFrom"/>, <paramref name="wantTo"/>): the window grows to
    /// cover it and is read again. Called while drawing, so it never starts a first read. A CalDAV
    /// calendar, kept whole, has no window: it is never read for this.
    /// </summary>
    public void Want(DateTime wantFrom, DateTime wantTo)
    {
        lock (gate)
        {
            // Wall-clock times in some zone, so a day either side covers every offset there is.
            var wide = DateTime.SpecifyKind(wantFrom.AddDays(-1), DateTimeKind.Utc);
            var far = DateTime.SpecifyKind(wantTo.AddDays(1), DateTimeKind.Utc);
            var month = Month(now());
            var least = month.AddMonths(-MostMonths);
            var most = month.AddMonths(MostMonths);
            var wanted = from;
            var wantedTo = to;
            if (wide < from)
            {
                wanted = Month(wide < least ? least : wide);
            }
            if (far > to)
            {
                wantedTo = Month(far > most ? most : far).AddMonths(1);
            }
            if (wanted >= from && wantedTo <= to)
            {
                return;
            }
            from = wanted;
            to = wantedTo;
            stale = true;
            if (opened)
            {
                // A wider window is not a retry: it goes now, whatever rest a failure asked for.
                nextTry = 0;
                Start();
            }
        }
    }

    /// <summary>The work in flight, for tests.</summary>
    internal Task Idle()
    {
        lock (gate)
        {
            return working ?? Task.CompletedTask;
        }
    }

    /// <summary>For tests: the cache taken in, which comes before the first read of an account.</summary>
    internal Task CacheRead()
    {
        lock (gate)
        {
            return cacheRead;
        }
    }

    /// <summary>What <paramref name="calendar"/> holds in the window now.</summary>
    internal Held Holding(string calendar)
    {
        lock (gate)
        {
            return held.TryGetValue(calendar, out var found) ? found : Held.Empty;
        }
    }

    public void Dispose()
    {
        accounts.Changed -= OnAccounts;
        accounts.Recoloured -= OnRecoloured;
        accounts.Rewritten -= OnRewritten;
        updates.Changed -= OnInterval;
        updates.Back -= OnBack;
        again.Dispose();
        stopping.Cancel();
    }

    private static DateTime Month(DateTime at) => new(at.Year, at.Month, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>Under <see cref="gate"/>: starts a read unless one is running or nothing is due.</summary>
    private void Start()
    {
        // A flag and not working.IsCompleted, because a read that has just widened its own window
        // starts the next one from inside itself, while its own task is still running.
        if (running || Environment.TickCount64 < nextTry)
        {
            return;
        }
        running = true;
        stale = false;
        var cancellation = stopping.Token;
        if (!cacheTaken)
        {
            // Before the first read: what was kept, on the pool, so a view never waits for a file.
            cacheTaken = true;
            cacheRead = cache is null && mirror.Folder is null ? Task.CompletedTask : Task.Run(() => Troubles.Guard("Calendars: taking in what was kept", TakeCacheAsync));
        }
        var first = cacheRead;
        working = Task.Run(() => Troubles.Guard("Calendars: reading them", async () =>
        {
            await first.ConfigureAwait(false);
            await WorkAsync(cancellation).ConfigureAwait(false);
        }));
    }

    /// <summary>
    /// What was kept, once, before the first read: each CalDAV calendar's mirror, and calendar-cache.json's events
    /// of every other account still here, as they were last read, so a start without a connection shows them. An
    /// account read since is left as it was read. A mirror of an account no longer here is deleted.
    /// </summary>
    private async Task TakeCacheAsync()
    {
        // Not while the accounts are not known (accounts.json held at the start): taken before a later read.
        if (!accounts.Known)
        {
            lock (gate)
            {
                cacheTaken = false;
            }
            return;
        }
        var readers = accounts.Giving(AccountData.Calendars);
        var here = readers.Select(reader => reader.Account.Id).ToHashSet(StringComparer.Ordinal);
        var took = false;
        if (!mirrorsTaken)
        {
            mirrorsTaken = true;
            var mirrored = readers.Where(reader => reader is ICalendarSync).Select(reader => reader.Account.Id).ToHashSet(StringComparer.Ordinal);
            await mirror.SweepAsync(mirrored, stopping.Token).ConfigureAwait(false);
            foreach (var id in mirrored)
            {
                var kept = await mirror.ReadAsync(id, stopping.Token).ConfigureAwait(false);
                if (kept.Count > 0)
                {
                    took |= TakeMirrors(id, kept);
                }
            }
        }
        // calendar-cache.json not while another program holds it: taken before a later read, nothing written over it before.
        if (cache?.ReadCalendars() is not { } file)
        {
            lock (gate)
            {
                cacheTaken = cache is null;
            }
            if (took)
            {
                Post();
            }
            return;
        }
        var whole = readers.Where(reader => reader is ICalendarSync).Select(reader => reader.Account.Id).ToHashSet(StringComparer.Ordinal);
        lock (gate)
        {
            cacheIn = true;
            var tick = Environment.TickCount64;
            var utcNow = now();
            foreach (var account in file.Accounts)
            {
                // Taken too where a read of this run failed before the file could be: its sync has no calendars yet.
                // A CalDAV account's is its mirror's, never the file's (an older file may hold one).
                if (!here.Contains(account.Account) || whole.Contains(account.Account) || syncs.TryGetValue(account.Account, out var had) && had.Calendars is not null)
                {
                    continue;
                }
                var calendars = account.Calendars
                    .Select(calendar => (calendar.Remote, new Held([.. calendar.Events.Select(item => item.Occurrence)], EventIndex.Empty)))
                    .ToList();
                var sync = had ?? (syncs[account.Account] = new Sync());
                sync.Calendars = calendars;
                sync.UpdatedUtc = DateTime.SpecifyKind(account.UpdatedUtc, DateTimeKind.Utc);
                // Read less than the interval ago, the whole window kept: as good as a read of this run, due the
                // interval after it and not now (PimCache.KeptRead). Not over a failure of this run, which keeps its rest.
                if (had is null && account is { From: { } keptFrom, To: { } keptTo }
                    && PimCache.KeptRead(sync.UpdatedUtc.Value, utcNow, updates.Interval, tick) is { } read)
                {
                    sync.From = DateTime.SpecifyKind(keptFrom, DateTimeKind.Utc);
                    sync.To = DateTime.SpecifyKind(keptTo, DateTimeKind.Utc);
                    sync.Read = read;
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
            Post();
        }
    }

    /// <summary>
    /// A CalDAV account's calendars as their mirrors keep them, before its first sync of this run: shown at once, and
    /// read less than the interval ago and each whole, as good as a read of this run (PimCache.KeptRead).
    /// </summary>
    private bool TakeMirrors(string account, List<MirroredCalendar> kept)
    {
        lock (gate)
        {
            if (syncs.TryGetValue(account, out var had) && had.Calendars is not null)
            {
                return false;
            }
            var sync = had ?? (syncs[account] = new Sync());
            sync.Mirrors = kept.ToDictionary(calendar => calendar.Calendar.Id, StringComparer.Ordinal);
            sync.Calendars = [.. kept.Select(calendar => (calendar.Calendar, HeldOf(calendar)))];
            sync.Whole = true;
            var oldest = kept.Min(calendar => calendar.Header.UpdatedUtc);
            sync.UpdatedUtc = DateTime.SpecifyKind(oldest, DateTimeKind.Utc);
            if (had is null && kept.All(calendar => calendar.Header.Complete)
                && PimCache.KeptRead(sync.UpdatedUtc.Value, now(), updates.Interval, Environment.TickCount64) is { } read)
            {
                (sync.From, sync.To) = (DateTime.MinValue, DateTime.MaxValue);
                sync.Read = read;
                sync.Due = read + (long)updates.Interval.TotalMilliseconds;
            }
            Rebuild();
            Loading = false;
            return true;
        }
    }

    /// <summary>What a calendar kept whole shows: its index, not all there where its last sync said so or objects did not read.</summary>
    private static Held HeldOf(MirroredCalendar calendar) =>
        new([], calendar.Index) { Incomplete = calendar.Short, Unread = calendar.Index.Dropped + calendar.Broken.Count };

    /// <summary>Asks every account that is due for its calendars and the window's events, each on its own.</summary>
    private async Task WorkAsync(CancellationToken cancellation)
    {
        // As this read starts: a failure is read again at once if the network came back meanwhile (RetryNow).
        var online = updates.Comebacks;
        DateTime asked, until;
        lock (gate)
        {
            (asked, until) = (from, to);
        }

        var readers = accounts.Giving(AccountData.Calendars);
        List<IAccountReader> due;
        lock (gate)
        {
            var tick = Environment.TickCount64;
            // A CalDAV account has no window: only its interval makes it due, never a view looking further.
            due = [.. readers.Where(reader => !syncs.TryGetValue(reader.Account.Id, out var sync)
                || sync.Due <= tick || (reader is not ICalendarSync && (sync.From > asked || sync.To < until)))];
        }

        // One sync per account, side by side: a slow server holds up nobody else's events.
        var answers = await Task.WhenAll(due.Select(reader => reader is ICalendarSync whole
            ? SyncAsync(reader, whole, cancellation)
            : ReadAsync(reader, asked, until, cancellation))).ConfigureAwait(false);
        if (cancellation.IsCancellationRequested)
        {
            return;
        }

        CalendarCacheFile? keep = null;
        List<(Account Account, Sync Sync)>? told = null;
        List<string>? removed = null;
        lock (gate)
        {
            var tick = Environment.TickCount64;
            var utcNow = now();
            var here = readers.Select(reader => reader.Account.Id).ToHashSet(StringComparer.Ordinal);
            // An account removed stops: its sync goes, and so does what the file kept of it, and its mirror.
            var write = false;
            foreach (var gone in syncs.Keys.Where(id => !here.Contains(id)).ToList())
            {
                syncs.Remove(gone);
                write = true;
                if (accounts.Known)
                {
                    (removed ??= []).Add(gone);
                }
            }
            foreach (var (reader, read, error, mirrors) in answers)
            {
                var sync = syncs.TryGetValue(reader.Account.Id, out var known) ? known : syncs[reader.Account.Id] = new Sync();
                if (mirrors is not null)
                {
                    // What a CalDAV account holds now, whether or not the rest of its sync went well.
                    sync.Mirrors = mirrors.Calendars;
                    sync.Unsaved = mirrors.Unsaved;
                }
                if (read is not null)
                {
                    // A calendar that alone failed keeps what it held before, stale, with why on it; the
                    // others are what was just read. It waits the interval as the rest: asked again in a
                    // minute, a calendar a server refuses would cost the account's whole read every minute.
                    var before = sync.Calendars?.DistinctBy(calendar => calendar.Calendar.Id, StringComparer.Ordinal).ToDictionary(calendar => calendar.Calendar.Id, calendar => calendar.Held, StringComparer.Ordinal);
                    sync.Calendars = [.. read.Select(calendar => (calendar.Calendar, Held.After(calendar.Held, before?.GetValueOrDefault(calendar.Calendar.Id), sync.UpdatedUtc)))];
                    sync.UpdatedUtc = utcNow;
                    sync.Read = tick;
                    sync.Whole = mirrors is not null;
                    (sync.From, sync.To) = sync.Whole ? (DateTime.MinValue, DateTime.MaxValue) : (asked, until);
                    sync.Problem = null;
                    sync.Consent = false;
                    sync.Due = tick + (long)updates.Interval.TotalMilliseconds;
                    // calendar-cache.json holds no CalDAV account: its calendars are its mirror's files.
                    write |= !sync.Whole;
                }
                else
                {
                    // What it gave before stays on screen; it is asked again in a minute.
                    sync.Problem = error is null ? null : Failures.Say(reader.Account, error);
                    sync.Consent = error is ConsentException;
                    sync.Due = updates.RetryNow(true, online) ? tick : tick + (long)Retry.TotalMilliseconds;
                }
            }
            Rebuild();
            Loading = false;
            HasAccount = readers.Count > 0;
            Problem = readers.Count == 0 ? NoAccount
                : readers.Select(reader => syncs.GetValueOrDefault(reader.Account.Id)).FirstOrDefault(sync => sync is { Calendars: null, Problem: not null })?.Problem
                ?? Failures.Missing(Shown().Select(shown => (shown.Calendar.Name, shown.Held.Failed, shown.Held.Kept, shown.Held.Short)));
            nextTry = syncs.Count == 0 ? tick + (long)updates.Interval.TotalMilliseconds : syncs.Values.Min(sync => sync.Due);
            // No account, nothing to come back for: an account added wakes the reading (OnAccounts).
            Arm(readers.Count == 0 ? null : TimeSpan.FromMilliseconds(Math.Max(0, nextTry - tick)));
            // Never before the file was taken in: written then, it would lose what it holds.
            if (write && cache is not null && cacheIn)
            {
                keep = Kept(readers, utcNow);
            }
            if (report is not null)
            {
                told = [.. readers.Where(reader => syncs.ContainsKey(reader.Account.Id)).Select(reader => (reader.Account, syncs[reader.Account.Id].Copy()))];
            }
            running = false;
            // A view asked for a wider window while this read was in flight.
            if (stale)
            {
                nextTry = 0;
                Start();
            }
        }
        if (keep is not null)
        {
            cache!.Write(keep);
        }
        if (removed is not null)
        {
            await mirror.SweepAsync(readers.Where(reader => reader is ICalendarSync).Select(reader => reader.Account.Id).ToHashSet(StringComparer.Ordinal), CancellationToken.None).ConfigureAwait(false);
        }
        if (told is not null)
        {
            Tell(told);
        }
        Post();
    }

    /// <summary>
    /// What was synced, for the Accounts page (<see cref="SyncReport"/>): each account's calendars with
    /// how many events each holds in the window read, and those turned off. Counted here, off the
    /// lock, from what is in memory: a CalDAV series is expanded to count its occurrences.
    /// </summary>
    private void Tell(List<(Account Account, Sync Sync)> told)
    {
        var parts = new List<(string, SyncPart)>();
        foreach (var (account, sync) in told)
        {
            // The window read in this run; for calendars taken from the file, the file's; for one kept whole, the cache's.
            var (from, to) = sync.From <= sync.To && !sync.Whole ? (sync.From, sync.To) : PimCache.Window(now());
            var part = Failures.Told(sync.UpdatedUtc, sync.Problem, sync.Consent);
            if (sync.Calendars is { } calendars)
            {
                (part.FromUtc, part.ToUtc) = (from, to);
                foreach (var (calendar, held) in calendars)
                {
                    var on = account.Syncs(SyncKeys.Calendar(calendar.Id));
                    part.Items.Add(new SyncItem
                    {
                        Id = calendar.Id,
                        Name = calendar.Name,
                        Colour = calendar.Colour,
                        Count = on ? Count(held, from, to) : null,
                        Off = !on,
                        // Why this calendar alone is stale or not all there: on its row, and in the edit bar's errors.
                        Problem = on ? Failures.Part(calendar.Name, held.Failed, held.Short) : null,
                    });
                }
            }
            parts.Add((account.Id, part));
        }
        report!.Put(AccountData.Calendars, parts);
    }

    /// <summary>How many occurrences <paramref name="held"/> has in [<paramref name="from"/>, <paramref name="to"/>), each of a series counted.</summary>
    internal static int Count(Held held, DateTime from, DateTime to)
    {
        var count = 0;
        foreach (var occurrence in held.Occurrences)
        {
            // A whole day or a floating time taken as if UTC, as the cache does: a day off at the edges at most.
            if (CalendarStore.Overlaps(DateTime.SpecifyKind(occurrence.Start, DateTimeKind.Utc), DateTime.SpecifyKind(occurrence.End, DateTimeKind.Utc), from, to))
            {
                count++;
            }
        }
        return count + held.Index.CountIn(from, to);
    }

    /// <summary>One account's calendars in the window, or why not.</summary>
    private static async Task<(IAccountReader Reader, List<(RemoteCalendar Calendar, Held Held)>? Read, Exception? Error, Mirrors? Mirrors)> ReadAsync(
        IAccountReader reader, DateTime asked, DateTime until, CancellationToken cancellation)
    {
        try
        {
            var reads = await reader.CalendarsAsync(asked, until, cancellation).ConfigureAwait(false);
            return (reader, [.. reads.Select(read => (read.Calendar, Held.Of(read)))], null, null);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            return (reader, null, null, null);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            return (reader, null, error, null);
        }
    }

    /// <summary>
    /// One CalDAV account's calendars brought up to date (§616): what is held handed over, what changed put in and
    /// parsed here on the pool, each calendar that changed written to its file, then merged by the caller. A first
    /// download's steps are written and shown as they come (<see cref="Step"/>). A calendar turned off, or no longer
    /// listed, loses its file; one that failed alone keeps what it held. A file that could not be written is kept to
    /// write again (<see cref="Sync.Unsaved"/>). What the account holds is answered even where its sync failed.
    /// </summary>
    private async Task<(IAccountReader Reader, List<(RemoteCalendar Calendar, Held Held)>? Read, Exception? Error, Mirrors? Mirrors)> SyncAsync(
        IAccountReader reader, ICalendarSync whole, CancellationToken cancellation)
    {
        var account = reader.Account.Id;
        Dictionary<string, MirroredCalendar> mirrors;
        HashSet<string> unsaved;
        lock (gate)
        {
            var had = syncs.GetValueOrDefault(account);
            mirrors = new Dictionary<string, MirroredCalendar>(had?.Mirrors ?? new Dictionary<string, MirroredCalendar>(), StringComparer.Ordinal);
            unsaved = new HashSet<string>(had?.Unsaved ?? [], StringComparer.Ordinal);
        }
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var utcNow = now();
        var stepped = 0;
        // What could not be written before goes first.
        foreach (var id in unsaved.ToList())
        {
            if (!mirrors.TryGetValue(id, out var waiting) || await mirror.WriteAsync(account, waiting, cancellation).ConfigureAwait(false))
            {
                unsaved.Remove(id);
            }
        }
        try
        {
            var held = mirrors.ToDictionary(pair => pair.Key, pair => pair.Value.State, StringComparer.Ordinal);
            var answer = await whole.SyncCalendarsAsync(held, async step =>
            {
                var calendar = CalendarMirror.Apply(mirrors.GetValueOrDefault(step.Calendar.Id), step, mirrors.GetValueOrDefault(step.Calendar.Id)?.Header.Order ?? mirrors.Count, utcNow, final: false);
                mirrors[step.Calendar.Id] = calendar;
                stepped += step.Changed.Count;
                if (!await mirror.WriteAsync(account, calendar, cancellation).ConfigureAwait(false))
                {
                    unsaved.Add(step.Calendar.Id);
                }
                Step(reader.Account, calendar, unsaved);
            }, cancellation).ConfigureAwait(false);

            var read = new List<(RemoteCalendar Calendar, Held Held)>(answer.Count);
            var (objects, out_, quiet) = (stepped, 0, 0);
            for (var order = 0; order < answer.Count; order++)
            {
                var changes = answer[order];
                var id = changes.Calendar.Id;
                var before = mirrors.GetValueOrDefault(id);
                MirroredCalendar? after;
                if (changes.Off)
                {
                    // Turned off: nothing of it is kept; turned on again, it is downloaded again (D9).
                    if (mirrors.Remove(id))
                    {
                        await mirror.DeleteAsync(account, id, cancellation).ConfigureAwait(false);
                    }
                    unsaved.Remove(id);
                    read.Add((changes.Calendar, Held.Empty));
                    continue;
                }
                if (changes.Failed is not null)
                {
                    read.Add((changes.Calendar, Held.Empty with { Failed = changes.Failed }));
                    continue;
                }
                if (changes.Unchanged && before is not null)
                {
                    after = CalendarMirror.Touched(before, changes.Calendar, order, utcNow);
                    quiet++;
                }
                else
                {
                    after = CalendarMirror.Apply(before, changes, order, utcNow, final: true);
                    objects += changes.Changed.Count;
                    out_ += changes.Removed.Count;
                }
                mirrors[id] = after;
                if (await mirror.WriteAsync(account, after, cancellation).ConfigureAwait(false))
                {
                    unsaved.Remove(id);
                }
                else
                {
                    unsaved.Add(id);
                }
                read.Add((changes.Calendar, HeldOf(after)));
            }
            // A calendar no longer listed, after a listing that went well: its file goes.
            var listed = answer.Select(changes => changes.Calendar.Id).ToHashSet(StringComparer.Ordinal);
            foreach (var gone in mirrors.Keys.Where(id => !listed.Contains(id)).ToList())
            {
                mirrors.Remove(gone);
                unsaved.Remove(gone);
                await mirror.DeleteAsync(account, gone, cancellation).ConfigureAwait(false);
            }
            if (objects > 0 || out_ > 0)
            {
                // How much, never what: no name, href, token or etag is logged.
                Console.Error.WriteLine(System.FormattableString.Invariant(
                    $"Karpo: CalDAV sync: {answer.Count} calendars, {objects} objects in, {out_} out, {quiet} unchanged, {mirrors.Values.Sum(one => one.Etags.Count)} held, {clock.ElapsedMilliseconds} ms"));
            }
            return (reader, read, null, new Mirrors(mirrors, unsaved));
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            return (reader, null, null, new Mirrors(mirrors, unsaved));
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            return (reader, null, error, new Mirrors(mirrors, unsaved));
        }
    }

    /// <summary>
    /// A step of a first download (§616's D8): <paramref name="calendar"/> as it stands, shown now, its account's others
    /// as they were. Its sync is not done: when it is, the account's calendars are what it answered.
    /// </summary>
    private void Step(Account account, MirroredCalendar calendar, HashSet<string> unsaved)
    {
        lock (gate)
        {
            var sync = syncs.TryGetValue(account.Id, out var had) ? had : syncs[account.Id] = new Sync();
            sync.Mirrors = new Dictionary<string, MirroredCalendar>(sync.Mirrors ?? new Dictionary<string, MirroredCalendar>(), StringComparer.Ordinal)
            {
                [calendar.Calendar.Id] = calendar,
            };
            sync.Unsaved = [.. unsaved];
            var calendars = sync.Calendars?.ToList() ?? [];
            var at = calendars.FindIndex(one => one.Calendar.Id == calendar.Calendar.Id);
            if (at >= 0)
            {
                calendars[at] = (calendar.Calendar, HeldOf(calendar));
            }
            else
            {
                calendars.Add((calendar.Calendar, HeldOf(calendar)));
            }
            sync.Calendars = calendars;
            sync.Whole = true;
            // What it shows is of now: should the download stop here, it is stale from now on.
            sync.UpdatedUtc ??= calendar.Header.UpdatedUtc;
            Rebuild();
            Loading = false;
        }
        Post();
    }

    /// <summary>A CalDAV account's calendars kept whole after a sync, by id, and those whose file is still to be written.</summary>
    private sealed record Mirrors(Dictionary<string, MirroredCalendar> Calendars, HashSet<string> Unsaved);

    /// <summary>
    /// Under <see cref="gate"/>: the calendars and what they hold, from every account's last good
    /// read in the order the accounts were added, and how fresh that is.
    /// </summary>
    private void Rebuild()
    {
        var all = new List<(Account, string, string?)>();
        foreach (var reader in accounts.Giving(AccountData.Calendars))
        {
            if (syncs.TryGetValue(reader.Account.Id, out var sync) && sync.Calendars is { } calendars)
            {
                // Every calendar takes its place in the colours, shown or not, so turning one off
                // recolours no other (and the Accounts page, which lists them all, says the same).
                all.AddRange(calendars.Select(calendar => (reader.Account, SyncKeys.Calendar(calendar.Calendar.Id), CalendarColours.Hex(CalendarColours.Parse(calendar.Calendar.Colour)))));
            }
        }
        var reads = Shown();
        Calendars = Listed(reads, SourceColours.Of(all));
        held = reads.ToDictionary(read => Id(read.Account, read.Calendar), read => read.Held, StringComparer.Ordinal);
        // Stale where an account failed its last read, and where a calendar of one alone did and shows
        // what it held before: from the oldest of those reads.
        Freshness = Freshness.Of(syncs.Values
            .Where(sync => sync is { Calendars: not null, UpdatedUtc: not null })
            .Select(sync => (sync.UpdatedUtc!.Value, sync.Problem is not null))
            .Concat(reads
                .Where(read => read.Held is { Failed: not null, UpdatedUtc: not null })
                .Select(read => (read.Held.UpdatedUtc!.Value, true))));
    }

    /// <summary>
    /// Under <see cref="gate"/>: the calendars shown and what they hold, from every account's last good read
    /// in the order the accounts were added — a calendar the person turned off is not, whatever was read
    /// or kept of it before.
    /// </summary>
    private List<(Account Account, RemoteCalendar Calendar, Held Held)> Shown()
    {
        var reads = new List<(Account Account, RemoteCalendar Calendar, Held Held)>();
        foreach (var reader in accounts.Giving(AccountData.Calendars))
        {
            var account = reader.Account;
            if (syncs.TryGetValue(account.Id, out var sync) && sync.Calendars is { } calendars)
            {
                reads.AddRange(calendars
                    .Where(calendar => account.Syncs(SyncKeys.Calendar(calendar.Calendar.Id)))
                    .Select(calendar => (account, calendar.Calendar, calendar.Held)));
            }
        }
        return reads;
    }

    /// <summary>Under <see cref="gate"/>: what calendar-cache.json keeps of every account with events.</summary>
    private CalendarCacheFile Kept(IReadOnlyList<IAccountReader> readers, DateTime utcNow)
    {
        var file = new CalendarCacheFile();
        foreach (var reader in readers)
        {
            if (reader is not ICalendarSync && syncs.TryGetValue(reader.Account.Id, out var sync) && sync is { Calendars: { } calendars, UpdatedUtc: { } updated })
            {
                // Nothing of a calendar turned off is kept: the file drops it at this write. Nor anything of one
                // that failed with nothing held — and the file is dated by its oldest calendar, so one kept from
                // before it failed is not taken for fresh at the next start.
                var shown = calendars.Where(calendar => reader.Account.Syncs(SyncKeys.Calendar(calendar.Calendar.Id))).ToList();
                var on = shown.Where(calendar => calendar.Held.Kept).ToList();
                var oldest = on.Select(calendar => calendar.Held.UpdatedUtc ?? updated).DefaultIfEmpty(updated).Min();
                // The window it read, where it is all there: one missing a calendar is read at the next start, to say so.
                var read = on.Count == shown.Count && sync.From <= sync.To ? (sync.From, sync.To) : ((DateTime, DateTime)?)null;
                file.Accounts.Add(PimCache.Keep(reader.Account.Id, oldest, on, utcNow, read));
            }
        }
        return file;
    }

    /// <summary>
    /// Under <see cref="gate"/>: the calendars as the accounts list them, keeping the object each one
    /// already had so a source on screen stays the same source, in the colour it wears now
    /// (<paramref name="colours"/>, <see cref="SourceColours.Of"/>): the person's pick, the account's, or the palette's.
    /// </summary>
    private IReadOnlyList<AccountCalendar> Listed(IReadOnlyList<(Account Account, RemoteCalendar Calendar, Held Held)> reads, IReadOnlyDictionary<string, (string Colour, string Automatic)> colours)
    {
        var found = new List<AccountCalendar>(reads.Count);
        foreach (var (account, remote, _) in reads)
        {
            var id = Id(account, remote);
            var colour = colours.TryGetValue(SourceColours.Key(account.Id, SyncKeys.Calendar(remote.Id)), out var hex)
                ? CalendarColours.Of(hex.Colour)
                : CalendarColours.Parse(remote.Colour) ?? CalendarColours.Palette[0];
            if (!known.TryGetValue(id, out var calendar))
            {
                calendar = new AccountCalendar(this, id, remote.Name, colour, account, WidgetSources.Key(account.Address, SyncKeys.Calendar(remote.Id)));
                known[id] = calendar;
            }
            calendar.Rename(remote.Name);
            calendar.Recolour(colour);
            found.Add(calendar);
        }
        return found;
    }

    /// <summary>The person picked a colour on the Accounts page: drawn at once, with nothing read.</summary>
    private void OnRecoloured()
    {
        lock (gate)
        {
            Rebuild();
        }
        Post();
    }

    /// <summary>"account id:calendar id": unique across accounts, and the same from run to run.</summary>
    private static string Id(Account account, RemoteCalendar calendar) => $"{account.Id}:{calendar.Id}";

    /// <summary>Everyone who draws these calendars is told, whether or not any calendar is listed yet.</summary>
    private void Post()
    {
        foreach (var calendar in Calendars)
        {
            calendar.Raise();
        }
        Changed?.Invoke();
    }

    /// <summary>The timer: an account is due again, if anyone has looked.</summary>
    private void Due()
    {
        lock (gate)
        {
            if (opened)
            {
                Start();
            }
        }
    }

    /// <summary>Under <see cref="gate"/>: the timer waits <paramref name="wait"/> before the next read, or is stopped (null).</summary>
    private void Arm(TimeSpan? wait)
    {
        Armed = wait;
        again.Change(wait ?? Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    /// <summary>
    /// The online interval was set to another: each account read well is due that long after its
    /// last read, at once where that has passed. A read on its way, a failure resting and a store
    /// nobody has looked at are left as they are — each arms the timer with the new interval when
    /// it is done.
    /// </summary>
    private void OnInterval()
    {
        lock (gate)
        {
            if (!opened || running || syncs.Count == 0 || syncs.Values.Any(sync => sync.Problem is not null || sync.Read == long.MinValue))
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
    /// The network came back (OnlineUpdates.Back): an account's calendars whose last read failed is read again now,
    /// not a minute later. A read on its way that fails is tried again as it ends (RetryNow).
    /// </summary>
    private void OnBack()
    {
        lock (gate)
        {
            var failed = syncs.Values.Where(sync => sync.Problem is not null).ToList();
            if (failed.Count == 0)
            {
                return;
            }
            foreach (var sync in failed)
            {
                sync.Due = 0;
            }
            nextTry = 0;
            if (opened)
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
            if (opened)
            {
                Start();
            }
        }
    }

    /// <summary>An account was added, removed or signed in again: read now, whatever a failure asked.</summary>
    private void OnAccounts()
    {
        lock (gate)
        {
            foreach (var sync in syncs.Values)
            {
                sync.Due = 0;
            }
            nextTry = 0;
            if (opened)
            {
                Start();
            }
        }
    }

    /// <summary>One account's sync: what it gave at its last good read, and where its next one stands.</summary>
    private sealed class Sync
    {
        /// <summary>Its calendars and what they hold, from its last good read or the cache; null before either.</summary>
        public List<(RemoteCalendar Calendar, Held Held)>? Calendars { get; set; }

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

        /// <summary>The window the last good read covered, or the cache's where it holds all of one; none before either.</summary>
        public DateTime From { get; set; } = DateTime.MaxValue;

        public DateTime To { get; set; } = DateTime.MinValue;

        /// <summary>A CalDAV account: its calendars kept whole (§616), with no window.</summary>
        public bool Whole { get; set; }

        /// <summary>A CalDAV account's calendars kept whole, by id: replaced at a sync, never changed.</summary>
        public Dictionary<string, MirroredCalendar>? Mirrors { get; set; }

        /// <summary>A CalDAV account's calendars whose file could not be written yet (another program held it): written at its next sync.</summary>
        public HashSet<string> Unsaved { get; set; } = new(StringComparer.Ordinal);

        /// <summary>As it is now, for reading off the lock: the list is replaced at a read, never changed.</summary>
        public Sync Copy() => (Sync)MemberwiseClone();
    }

    /// <summary>What one calendar holds: occurrences, or iCalendar objects expanded on demand (<see cref="EventIndex"/>).</summary>
    internal sealed record Held(IReadOnlyList<Occurrence> Occurrences, EventIndex Index)
    {
        public static readonly Held Empty = new([], EventIndex.Empty);

        /// <summary>
        /// Why its last read failed, the account's other calendars read (CalendarRead.Failed): what it holds is
        /// from <see cref="UpdatedUtc"/>, and shown stale; null where its last read went well.
        /// </summary>
        public string? Failed { get; init; }

        /// <summary>When what it holds was read, where that is before its account's last read (it <see cref="Failed"/> since); else null.</summary>
        public DateTime? UpdatedUtc { get; init; }

        /// <summary>The account has more of it than it holds: its answer was cut short (CalendarRead.Incomplete).</summary>
        public bool Incomplete { get; init; }

        /// <summary>How many of its iCalendar objects did not read, or events in them Karpo cannot show (ICalendar.Dropped).</summary>
        public int Unread { get; init; }

        /// <summary>A read's objects parsed now, on the pool, so a view never parses while drawing.</summary>
        public static Held Of(CalendarRead read)
        {
            var parsed = new List<(string, ICalendar)>(read.Objects.Count);
            var unread = 0;
            foreach (var text in read.Objects)
            {
                try
                {
                    var calendar = ICalendar.Parse(text);
                    // Its place in the read: a read handed over whole has no hrefs.
                    parsed.Add((parsed.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), calendar));
                    unread += calendar.Dropped;
                }
                catch (Exception error) when (error is not OutOfMemoryException)
                {
                    // One object that does not read leaves the calendar's others — counted, so the person
                    // is told the calendar is not all there (Short), never left to wonder where an event went.
                    unread++;
                }
            }
            return new Held(read.Occurrences, EventIndex.Of(parsed)) { Failed = read.Failed, Incomplete = read.Incomplete, Unread = unread };
        }

        /// <summary>Not all of it is held: its answer was cut short, or some of its objects did not read.</summary>
        public bool Short => Incomplete || Unread > 0;

        /// <summary>
        /// What a calendar holds after a read that gave it as <paramref name="read"/>: that read — or, where the
        /// calendar alone failed in it, what it held before (<paramref name="before"/>, read at
        /// <paramref name="beforeUtc"/>) with the failure on it, so a calendar a server refused for a while
        /// stays on screen, stale, while the account's others refresh.
        /// </summary>
        public static Held After(Held read, Held? before, DateTime? beforeUtc) =>
            read.Failed is null || before is not { } had || !had.Kept || (had.UpdatedUtc ?? beforeUtc) is not { } at
                ? read
                : had with { Failed = read.Failed, UpdatedUtc = at };

        /// <summary>It holds a read of its own: one that went well, or one kept from before it failed — not a failure with nothing.</summary>
        public bool Kept => Failed is null || UpdatedUtc is not null;
    }
}

/// <summary>
/// One calendar of an account as a source of Karpo's calendar views. It holds nothing itself: the
/// events and the reading are the <see cref="AccountCalendars"/> every calendar shares.
/// </summary>
/// <param name="account">The account it is of: whose web calendar its events open in (<see cref="Web"/>).</param>
internal sealed class AccountCalendar(AccountCalendars owner, string id, string name, Color colour, Account account, string part) : ICalendarSource
{
    public event Action? Changed;

    public string Id { get; } = id;

    /// <summary>§455: what a widget's "Show from" picks it by: its account's address and its id.</summary>
    public string? Part { get; } = part;

    public string Name { get; private set; } = name;

    /// <summary>What it wears now: the person may pick another on the Accounts page while it is shown.</summary>
    public Color Colour { get; private set; } = colour;

    public bool Loading => owner.Loading;

    public string? Problem => owner.Problem;

    public void Refresh() => owner.Refresh();

    public IReadOnlyList<CalendarEvent> EventsIn(DateTime from, DateTime to, TimeZoneInfo zone)
    {
        if (to <= from)
        {
            return [];
        }
        // What the view is looking at, so the window grows to hold it. It never waits: this answers
        // with what is already read, and the widening read raises Changed when it lands.
        owner.Want(from, to);

        var holding = owner.Holding(Id);
        var found = new List<CalendarEvent>();
        foreach (var occurrence in holding.Occurrences)
        {
            var (start, end) = occurrence.In(zone);
            if (CalendarStore.Overlaps(start, end, from, to))
            {
                found.Add(new CalendarEvent(start, end, occurrence.AllDay, occurrence.Title, occurrence.Location, this, Colour, occurrence.Join, occurrence.Free,
                    Link: occurrence.Link));
            }
        }
        foreach (var item in holding.Index.Between(from, to, zone))
        {
            found.Add(item with { Source = this, Colour = Colour });
        }
        return found;
    }

    /// <summary>Its account's web calendar on <paramref name="day"/> (§434): Outlook on the web's, Google's, iCloud's; null for another server.</summary>
    public string? Web(DateOnly day) => CalendarLinks.Day(account, day);

    /// <summary>The reading is the owner's, not this calendar's: a calendar going away stops nothing.</summary>
    public void Dispose()
    {
    }

    /// <summary>The account renamed the calendar; the object stays the same one.</summary>
    internal void Rename(string renamed) => Name = renamed;

    /// <summary>Its colour picked again, or the account's changed; the object stays the same one.</summary>
    internal void Recolour(Color colour) => Colour = colour;

    internal void Raise() => Changed?.Invoke();
}

/// <summary>What a store says when an account cannot be read.</summary>
internal static class Failures
{
    /// <summary>
    /// Whether <paramref name="error"/> is an account failing to answer — the person's problem to see,
    /// never a crash: a refused sign-in, a refusal, the network, a timeout, a server that answered
    /// nonsense.
    /// </summary>
    public static bool Expected(Exception error) => error is SignInRequiredException or AccountException or ConsentException
        or HttpRequestException or IOException or TimeoutException or OperationCanceledException
        or System.Net.Sockets.SocketException or System.Text.Json.JsonException or System.Xml.XmlException
        or MailKit.ProtocolException or MailKit.CommandException or MailKit.Security.SslHandshakeException;

    /// <summary>
    /// Why one part of an account — a calendar, an address book — is stale or not all there, in words that
    /// name it: it alone failed its last read (<paramref name="failed"/>, the account's words), or the
    /// account gave only part of it (<paramref name="incomplete"/>); null where neither.
    /// </summary>
    public static string? Part(string name, string? failed, bool incomplete) =>
        failed is { } said ? S.DockAccountsPartFailed(name: name, said: said)
        : incomplete ? S.DockAccountsPartIncomplete(name)
        : null;

    /// <summary>
    /// What a widget says is missing of the parts it shows: the first that failed with nothing kept of it,
    /// else the first not all there. A part that failed with what it held before kept is not missing — it
    /// is stale, which the stale mark says.
    /// </summary>
    public static string? Missing(IEnumerable<(string Name, string? Failed, bool Kept, bool Incomplete)> parts)
    {
        string? cut = null;
        foreach (var (name, failed, kept, incomplete) in parts)
        {
            if (failed is not null && !kept)
            {
                return Part(name, failed, false);
            }
            if (incomplete && failed is null)
            {
                cut ??= Part(name, null, true);
            }
        }
        return cut;
    }

    /// <summary>
    /// One kind of an account as the Accounts page shows it (SyncReport), before its parts: when it was read, and why the
    /// last try failed — or, §687, that it failed for the person's consent (<paramref name="consent"/>), which the page
    /// says in its own words with a Reconnect, as To Do's (TaskStore).
    /// </summary>
    public static SyncPart Told(DateTime? updatedUtc, string? problem, bool consent) =>
        new() { UpdatedUtc = updatedUtc, Problem = consent ? null : problem, Consent = consent ? true : null };

    /// <summary>The sentence a panel shows for it, naming the account. Anything unexpected is logged too.</summary>
    public static string Say(Account account, Exception error)
    {
        if (!Expected(error))
        {
            Console.Error.WriteLine($"Karpo: {account.Kind} account not read: {error}");
        }
        return error switch
        {
            SignInRequiredException or AccountException or ConsentException => $"{account.Address}: {error.Message}",
            OperationCanceledException or TimeoutException => S.DockAccountsNoAnswer(account.Address),
            HttpRequestException or System.Net.Sockets.SocketException or IOException => S.DockAccountsUnreachable(account.Address),
            _ => S.DockAccountsUnreadable(account.Address),
        };
    }
}
