// The notifications Windows keeps for its notification center, for the
// notifications preset: its bell face and its notification list's line
// (FolderIsland, NotificationPartTile).
//
// What is read is Windows' own store: %LOCALAPPDATA%\Microsoft\Windows\
// Notifications\wpndatabase.db, through the system's winsqlite3.dll, strictly
// read-only (SQLITE_OPEN_READONLY and a "mode=ro" URI). Windows' database is
// never written; should Windows hold it busy or locked, the database and its
// write-ahead log are copied into Karpo's local folder and the copy is read.
// The schema is checked every time: tables Notification (Id, HandlerId, Type,
// Payload = toast XML, ArrivalTime = FILETIME, ExpiryTime) and
// NotificationHandler (RecordId, PrimaryId = the app's AUMID); anything else
// and notifications are "not available". The database rather than
// UserNotificationListener, because the payload is the whole toast — its
// launch arguments, its buttons and its reply box (ToastOffers) — where the
// listener hands over its text and pictures only.
//
// Dismissing clears Windows' own list too, as Windows' notification center
// does: one, an app's or all of them go through UserNotificationListener
// (RemoveNotification, ClearNotifications), whose ids are the database's —
// measured on 26200, where the listener's ids and creation times were the
// database's Id and ArrivalTime for every toast. That listener was long
// believed to want package identity, which Karpo has not got; on Windows 11 it
// answers Allowed to an unpackaged process as well, under the privacy page's
// notification access, and where it does not Karpo does not ask (a prompt in the
// middle of a click is worse than a toast left in Windows' list). Karpo hides
// what it dismissed at once either way, so the list answers the click before
// Windows has written its database again; the dismissed ones and the arrival
// time "seen" up to live in notifications.json in the local folder, and a
// dismissal Windows then carries out is forgotten with the row it hid.
//
// The same table holds the badges packaged apps set (BadgeUpdateManager): rows of
// Type 'badge' whose Payload is <badge value="…"/>, one per app, which is how Mail's
// unread count and a chat app's status reach the taskbar. They are read with the
// toasts and kept apart (Badges), for the app buttons (AppSignals.cs).
//
// The database folder is watched; a change is read again once Windows' burst of
// writes has settled (150 ms, never more than 400 ms after its first write), off
// the dock's thread, and the islands are woken to take it (StoreSignal). It was a
// second after the last write: a chat app that sets its badge every second while
// messages come (WhatsApp, measured) kept the read waiting until it stopped, and
// every badge, up or down, came at least a second late.
//
// The watch is how a write is heard at once, not what says a write was made: the
// files' own size and last write are (WpnStamp). Every read takes them as it
// begins, and every five seconds they are looked at again (Check, two file
// attributes, nothing opened): changed for over a second with no read since is
// a write no read took — one the watch did not hear, or one whose read failed on
// the way (Windows held the database) — and it is read then. A folder watch is
// not told of every write: NTFS tells it of a commit SQLite does not flush (WAL,
// synchronous NORMAL, a connection that stays open, as Windows' service writes)
// only now and then, and .NET stops a FileSystemWatcher for good at any error but
// an overflow, which the store never noticed; a watch found stopped is watched
// again. Either kept a WhatsApp count until Karpo was started again (§569).
//
// **A store belongs to one thread**, the first to use it, and refuses any other
// (Own). Everything behind Items — the app cache, the dismissed set, the last
// read — is single-threaded by design, which is right for the dock, where there
// is one thread; the only thing that crosses is a finished read, handed over
// under a lock. What was missing was anything holding a second thread out, and
// two things let one in. The store asked for its thread's dispatcher queue in
// its constructor, so its news went to whichever thread *made* it rather than
// the one using it (docs\widgets.md §11: making it must ask Windows nothing, and
// the first look starts the work — so the queue is taken at the first Use now).
// And Shared was one store for the whole process, so in the test process, where
// every offscreen test has a UI thread of its own, a second thread was simply
// another test. Both together put two threads inside Recompute at once, and the
// app cache's Dictionary came apart under load
// (NotificationStoreThreadTests drives exactly that on purpose).
//
// Serialising onto the owner was chosen over making the state safe to share,
// because sharing it was wrong even where it did not corrupt anything: one
// thread's last Release stopped the other's watch, one thread's "Clear all"
// emptied the other's list, and the news went to the first thread there was.
// Concurrent collections would have turned an intermittent crash into
// intermittent wrong answers. Shared is per thread instead — the dock has one,
// so nothing changes there — and a store reached from a thread that is not its
// own says so at the call rather than three calls later on another thread.

using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Karpo.Core;
using Windows.Data.Xml.Dom;

namespace Karpo.Dock;

/// <summary>One notification as Karpo shows it.</summary>
/// <param name="Id">Windows' id of the notification.</param>
/// <param name="AppId">The app's AUMID; <c>shell:AppsFolder\&lt;AppId&gt;</c> opens it.</param>
/// <param name="Arrived">Local time.</param>
/// <param name="ImagePath">The toast's own logo (appLogoOverride) when it is a local file.</param>
/// <param name="Offer">Where a click on it goes, and its buttons and inputs; null for a toast of its words alone.</param>
/// <param name="HeroPath">Its hero picture (placement hero), drawn across the top of its card, when it is a local file.</param>
/// <param name="InlinePath">Its first inline picture (an image of no placement), under its words, when it is a local file.</param>
public sealed record NotificationEntry(
    long Id,
    string AppId,
    string AppName,
    Pixels? AppIcon,
    string Title,
    string Body,
    DateTime Arrived,
    string? ImagePath,
    ToastOffer? Offer = null,
    string? HeroPath = null,
    string? InlinePath = null);

public sealed class NotificationStore
{
    private const string StateFileName = "notifications.json";

    /// <summary>At most this many notifications are shown, newest first.</summary>
    private const int Most = 100;

    /// <summary>A change is read again this long after the last write, as Windows writes a change in a burst.</summary>
    internal const int SettleMs = 150;

    /// <summary>
    /// ...and never later than this after the first write not read yet. A chat app sets its badge
    /// every second or so while messages come (WhatsApp, measured), and a wait that every write
    /// pushed back read nothing until it stopped.
    /// </summary>
    internal const int MostWaitMs = 400;

    /// <summary>
    /// How often the database's and its log's size and last write are looked at beside the watch, for a
    /// write no read took (§569): one the watch did not hear, or one whose read failed on the way.
    /// </summary>
    internal const int CheckMs = 5000;

    /// <summary>A write the watch heard is being read within this long; one no read has taken by then went unheard (§569).</summary>
    internal const int HeardWithinMs = 1000;

    /// <summary>Unheard writes and watches found stopped are written to karpo.log at most this often, how many with them.</summary>
    private const long UnheardToldEveryMs = 10 * 60 * 1000;

    /// <summary>This thread's store (<see cref="Shared"/>), made the first time the thread asks.</summary>
    [ThreadStatic]
    private static NotificationStore? shared;

    private readonly Lock gate = new();
    private readonly string database;
    private readonly string copyFolder;
    private readonly string? stateFolder;
    private readonly Func<string, (string Name, Pixels? Icon)> resolve;
    private readonly INotificationRemover? remover;
    private readonly int checkMs;

    // Shared with the pool threads, under gate.
    private WpnRead? arrived;
    private Task? work;
    private bool again;

    // Whether a read loop runs: cleared under the gate in the same step that finds no `again`, so a write heard just as the
    // loop ends starts a new one (work.IsCompleted lags that decision: a write in the gap was read only at the next write).
    private bool reading;
    private FileSystemWatcher? watcher;
    private Timer? debounce;

    /// <summary>Looks at the files every <see cref="checkMs"/> while the store is used (<see cref="Check"/>); null when it is not.</summary>
    private Timer? checker;

    /// <summary>When the first write not read yet came (Environment.TickCount64); null when every write was read.</summary>
    private long? pendingSince;

    /// <summary>The files as the last read that did not fail began; null until one has (<see cref="Check"/>).</summary>
    private WpnStamp? taken;

    /// <summary>Whether the last read failed on the way, so the check reads again without waiting for a write.</summary>
    private bool failed;

    // Writes the watch did not hear and watches found stopped since the last line about them, and when that was (Environment.TickCount64).
    private int unheard;
    private long? unheardTold;

    /// <summary>
    /// The thread the store belongs to: the first to touch its state, and 0 until one has. Set once
    /// and never changed (<see cref="Own"/>).
    /// </summary>
    private int owner;

    /// <summary>
    /// How the islands are told there is news: made, and looked at, at the first <see cref="Use"/>,
    /// because the dispatcher queue it raises through is the looking thread's and that must be the
    /// owner's (docs\widgets.md §11). Read by the pool's read once it is set, so it is only ever read
    /// through <see cref="Volatile"/> there.
    /// </summary>
    private StoreSignal? signal;

    // The owner's thread only (Own).
    private readonly HashSet<object> users = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<string, (string Name, Pixels? Icon)> apps = new(StringComparer.OrdinalIgnoreCase);
    private WpnRead? read;
    private NotificationState? state;

    /// <param name="database">wpndatabase.db; only ever opened read-only.</param>
    /// <param name="copyFolder">Where the database is copied when Windows holds it.</param>
    /// <param name="stateFolder">Where notifications.json goes; the local folder when null.</param>
    /// <param name="resolve">An app's name and icon by AUMID (the shell's, in the dock).</param>
    /// <param name="remover">
    /// Windows' own list, where a dismissal clears it too; null where nothing may be cleared in Windows
    /// — a store over a made-up database, and every isolated run (<see cref="Shared"/>).
    /// </param>
    /// <param name="checkMs">How often the files are looked at beside the watch (<see cref="CheckMs"/>); shorter in a test.</param>
    internal NotificationStore(string database, string copyFolder, string? stateFolder, Func<string, (string Name, Pixels? Icon)> resolve,
        INotificationRemover? remover = null, int checkMs = CheckMs)
    {
        // Nothing else: making a store asks Windows nothing and binds it to no thread (§11). The
        // thread and its queue are taken at the first look, which is the first Use.
        this.database = database;
        this.copyFolder = copyFolder;
        this.stateFolder = stateFolder;
        this.resolve = resolve;
        this.remover = remover;
        this.checkMs = checkMs;
    }

    /// <summary>
    /// The store the dock uses: <b>this thread's</b>, made the first time the thread asks. In the dock
    /// that is the one thread there is, so every island, folder card and flyout there shares one
    /// store. Elsewhere — the test process, where every offscreen test has a UI thread of its own —
    /// each thread has its own, because a store's state belongs to one thread (see the header).
    /// </summary>
    /// <para>
    /// Its dismissals clear Windows' own list — but never on a run that may not reach the person's
    /// world: an isolated run (every test, every smoke pass) reads the database and clears nothing
    /// in it, so no test can take a notification out of the person's list.
    /// </para>
    public static NotificationStore Shared => shared ??= new NotificationStore(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Windows\Notifications\wpndatabase.db"),
        Path.Combine(Config.LocalFolder, "wpn"),
        null,
        ResolveApp,
        Config.IsIsolated ? null : new WindowsNotificationRemover());

    /// <summary>
    /// The notifications, the unseen count or availability changed. Raised on the owner's thread (the
    /// dock's, in the dock), from an attached island's Refresh or through that thread's dispatcher
    /// queue; not raised by the changes the owner's own calls make (they are visible at once).
    /// </summary>
    public event Action? Changed;

    /// <summary>False when Windows' notification database can't be read on this PC.</summary>
    public bool Available => read?.Available ?? true;

    /// <summary>Whether its dismissals clear Windows' own list: never in a test (<see cref="Shared"/>).</summary>
    internal bool ClearsWindows => remover is WindowsNotificationRemover;

    /// <summary>The database has been read at least once.</summary>
    public bool Loaded => read is not null;

    /// <summary>Newest first; the dismissed and the expired left out.</summary>
    public IReadOnlyList<NotificationEntry> Items { get; private set; } = [];

    /// <summary>How many of <see cref="Items"/> arrived after the list was last opened.</summary>
    public int Unseen { get; private set; }

    /// <summary>Counts every change of what the store shows, so a view can tell it is current.</summary>
    public int Version { get; private set; }

    /// <summary>
    /// The badge each app shows now, by its AUMID (compared as Windows compares them): the newest
    /// badge row an app has, where that sets one and has not expired. Read with the toasts; nothing
    /// Karpo does to the toasts touches them.
    /// </summary>
    public IReadOnlyDictionary<string, Badge> Badges { get; private set; } = new Dictionary<string, Badge>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// <paramref name="user"/> (an island, or an open flyout) shows notifications: the store reads and
    /// watches the database while anyone does. An island user is woken when there is news. Dock thread.
    /// </summary>
    internal void Use(object user)
    {
        Own();
        // The first look: the signal takes this thread's dispatcher queue, so news the pool brings
        // back with no island to wake is raised here, on the thread that uses the store. Published
        // with a barrier because the pool's read picks it up from its own thread.
        var told = signal;
        if (told is null)
        {
            told = new StoreSignal(Raise);
            // Said out loud rather than left to an island's Add, because the user may not be an
            // island: the notification list opened from the clock's menu, with no bell on the bar,
            // gets its news through this queue alone.
            told.Look();
            Volatile.Write(ref signal, told);
        }
        if (user is CellIsland island)
        {
            told.Add(island);
        }
        if (users.Add(user) && users.Count == 1)
        {
            Start();
        }
    }

    /// <summary><paramref name="user"/> shows notifications no more; the last one stops the watch.</summary>
    internal void Release(object user)
    {
        Own();
        if (user is CellIsland island)
        {
            signal?.Remove(island);
        }
        if (users.Remove(user) && users.Count == 0)
        {
            Stop();
        }
    }

    /// <summary>On the owner's thread: takes a finished read and raises <see cref="Changed"/> when there is news.</summary>
    internal void Deliver()
    {
        Own();
        signal?.Deliver();
    }

    /// <summary>
    /// For a list about to open: what arrived is taken now, and the very first read is done at once
    /// rather than showing an empty list that fills a moment later.
    /// </summary>
    internal void Prepare()
    {
        Own();
        signal?.Deliver();
        if (read is null)
        {
            var now = Wpn.Read(database, copyFolder);
            lock (gate)
            {
                arrived = null;
            }
            read = now;
            Recompute();
        }
    }

    /// <summary>Everything shown now counts as seen: the badge goes.</summary>
    public void MarkSeen()
    {
        Own();
        var current = State();
        var newest = read is { Rows.Count: > 0 } known ? known.Rows.Max(row => row.Arrival) : 0;
        if (newest <= current.SeenUpTo)
        {
            return;
        }
        current.SeenUpTo = newest;
        Changes();
    }

    /// <summary>Clears one notification: hidden in Karpo at once, and taken out of Windows' own list.</summary>
    public void Dismiss(long id)
    {
        Own();
        // WpnRow is a struct: FirstOrDefault would hand back an empty row, not nothing.
        var index = read is { } known ? known.Rows.ToList().FindIndex(candidate => candidate.Id == id) : -1;
        if (index < 0)
        {
            return;
        }
        var row = read!.Rows[index];
        remover?.Remove([row.Id]);
        if (State().Dismissed.Add(NotificationState.KeyOf(row.Id, row.Arrival)))
        {
            Changes();
        }
    }

    /// <summary>Clears every notification of the app shown now, here and in Windows' list; new ones still come.</summary>
    public void DismissApp(string appId)
    {
        Own();
        var changed = false;
        var ids = new List<long>();
        foreach (var row in read?.Rows ?? [])
        {
            if (string.Equals(row.AppId, appId, StringComparison.OrdinalIgnoreCase))
            {
                ids.Add(row.Id);
                changed |= State().Dismissed.Add(NotificationState.KeyOf(row.Id, row.Arrival));
            }
        }
        if (ids.Count > 0)
        {
            remover?.Remove(ids);
        }
        if (changed)
        {
            Changes();
        }
    }

    /// <summary>"Clear all": everything shown now, and Windows' whole list with it, as Windows' own Clear all clears it.</summary>
    public void DismissAll()
    {
        Own();
        var changed = false;
        foreach (var row in read?.Rows ?? [])
        {
            changed |= State().Dismissed.Add(NotificationState.KeyOf(row.Id, row.Arrival));
        }
        if (read is { Rows.Count: > 0 })
        {
            remover?.Clear();
        }
        if (changed)
        {
            Changes();
        }
    }

    /// <summary>Reads the database again now, off the dock's thread (tests, and the dock on resume).</summary>
    internal void Refresh() => ReadSoon(0);

    /// <summary>Whether the watch hears Windows' writes now, for tests.</summary>
    internal bool Watching
    {
        get
        {
            lock (gate)
            {
                return watcher is { EnableRaisingEvents: true };
            }
        }
    }

    /// <summary>For tests: the watch stops raising, as .NET stops a FileSystemWatcher after an error other than an overflow.</summary>
    internal void Deafen()
    {
        lock (gate)
        {
            if (watcher is { } watch)
            {
                watch.EnableRaisingEvents = false;
            }
        }
    }

    /// <summary>
    /// Whether the files changed since the last read began (<paramref name="read"/>, <paramref name="now"/>) and
    /// the newest write is <see cref="HeardWithinMs"/> old at <paramref name="nowUtc"/> (ticks), so a read of it
    /// the watch heard would have begun by then.
    /// </summary>
    internal static bool Unheard(WpnStamp read, WpnStamp now, long nowUtc) =>
        now != read && nowUtc - now.Newest >= HeardWithinMs * TimeSpan.TicksPerMillisecond;

    /// <summary>
    /// What a <see cref="Check"/> finds: nothing while a read waits or runs (<paramref name="idle"/> false), as it
    /// takes every write so far; a write to read again where the last read <paramref name="failed"/> on the way;
    /// else an unheard write (<see cref="Unheard"/>) once a read has <paramref name="taken"/> the files.
    /// </summary>
    internal static CheckFinding Finds(bool idle, bool failed, WpnStamp? taken, WpnStamp now, long nowUtc) =>
        !idle ? CheckFinding.Nothing
        : failed ? CheckFinding.Failed
        : taken is { } read && Unheard(read, now, nowUtc) ? CheckFinding.Unheard
        : CheckFinding.Nothing;

    /// <summary>The read in flight, for tests.</summary>
    internal Task Idle()
    {
        lock (gate)
        {
            return work ?? Task.CompletedTask;
        }
    }

    private void Start()
    {
        state ??= NotificationState.Load(ReadState());
        lock (gate)
        {
            checker = new Timer(static self => Troubles.Guard("Notifications: looking at Windows' list", ((NotificationStore)self!).Check), this, checkMs, checkMs);
        }
        Rewatch();
        ReadSoon(0);
    }

    private void Stop()
    {
        FileSystemWatcher? watch;
        Timer? timer;
        Timer? looking;
        lock (gate)
        {
            (watch, watcher) = (watcher, null);
            (timer, debounce) = (debounce, null);
            (looking, checker) = (checker, null);
        }
        watch?.Dispose();
        timer?.Dispose();
        looking?.Dispose();
    }

    /// <summary>
    /// A new watch on the database's folder in place of the one there was, while the store is used; none where
    /// the folder can't be watched now, and the next <see cref="Check"/> tries again.
    /// </summary>
    private void Rewatch()
    {
        FileSystemWatcher? made = null;
        try
        {
            var folder = Path.GetDirectoryName(database);
            if (folder is not null && Directory.Exists(folder))
            {
                made = new FileSystemWatcher(folder, "wpndatabase.db*")
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName | NotifyFilters.CreationTime,
                };
                made.Changed += Written;
                made.Created += Written;
                made.Renamed += Written;
                made.Error += Lost;
                made.EnableRaisingEvents = true;
            }
        }
        catch (Exception error) when (error is IOException or ArgumentException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            // Without a watch the files are still looked at (Check), the list read when it opens and on resume.
            Console.Error.WriteLine($"Karpo: notifications not watched: {error.GetType().Name}");
            made?.Dispose();
            made = null;
        }
        FileSystemWatcher? old;
        lock (gate)
        {
            if (checker is null)
            {
                // Released meanwhile: nobody is to be told.
                (old, made) = (made, null);
            }
            else
            {
                (old, watcher) = (watcher, made);
            }
        }
        old?.Dispose();
    }

    /// <summary>
    /// The watch failed. An overflow lost what it would have said and it goes on; any other error stops it for
    /// good (.NET sets EnableRaisingEvents false and says nothing more), and it is watched again here. Either
    /// way the database is read, as a write may have been lost. On the watcher's thread, where nothing may be thrown.
    /// </summary>
    private void Lost(object sender, ErrorEventArgs failure) => Troubles.Guard("Notifications: hearing Windows' list change", () =>
    {
        var error = failure.GetException();
        var stopped = error is not InternalBufferOverflowException;
        Troubles.Report(stopped ? "Notifications: the watch on Windows' list stopped, watched again" : "Notifications: the watch on Windows' list overflowed", error);
        if (stopped)
        {
            Rewatch();
        }
        WrittenSoon();
    });

    /// <summary>
    /// Every <see cref="checkMs"/>, on a pool thread: a watch that stopped hearing is watched again, and a write no
    /// read took (<see cref="Unheard"/>) — or the write of a read that failed on the way — is read now.
    /// </summary>
    private void Check()
    {
        var now = WpnStamp.Of(database);
        bool rewatch;
        bool stopped;
        bool missed;
        bool retry;
        lock (gate)
        {
            if (checker is null)
            {
                return;
            }
            // None at all where the folder could not be watched; one that stopped where .NET gave it up.
            rewatch = watcher is not { EnableRaisingEvents: true };
            stopped = watcher is { EnableRaisingEvents: false };
            var finding = Finds(idle: !reading && pendingSince is null, failed, taken, now, Clock.RealUtc().Ticks);
            missed = finding == CheckFinding.Unheard;
            retry = finding == CheckFinding.Failed;
        }
        if (rewatch)
        {
            Rewatch();
        }
        if (missed || stopped)
        {
            TellUnheard(missed, stopped);
        }
        if (missed || retry)
        {
            WrittenSoon();
        }
    }

    /// <summary>
    /// A write the watch did not hear, or a watch found stopped, in karpo.log: the first, then at most every ten
    /// minutes with how many more — so a count that stayed can be told from Windows' own afterwards (§508, §569).
    /// </summary>
    private void TellUnheard(bool missed, bool stopped)
    {
        int count;
        lock (gate)
        {
            unheard++;
            var now = Environment.TickCount64;
            if (unheardTold is { } told && now - told < UnheardToldEveryMs)
            {
                return;
            }
            (count, unheard, unheardTold) = (unheard, 0, now);
        }
        var what = (missed, stopped) switch
        {
            (true, true) => "the watch on Windows' list had stopped, watched again, and a write it did not hear read now",
            (true, false) => "a write to Windows' list the watch did not hear, read now",
            _ => "the watch on Windows' list had stopped, watched again",
        };
        Troubles.Write($"Notifications: {what}{(count > 1 ? $" ({count} times since the last line)" : "")}"); // not-ui: karpo.log
    }

    /// <summary>
    /// The database or its log was written. Reading takes a read mark in the shared-memory file
    /// (-shm), and the folder holds Windows' other files: only the database and its log count, or
    /// every read would be followed by another. On the watcher's thread, where nothing may be thrown.
    /// </summary>
    private void Written(object sender, FileSystemEventArgs change) => Troubles.Guard("Notifications: hearing Windows' list change", () =>
    {
        var name = Path.GetFileName(change.FullPath);
        if (name.Equals("wpndatabase.db", StringComparison.OrdinalIgnoreCase)
            || name.Equals("wpndatabase.db-wal", StringComparison.OrdinalIgnoreCase))
        {
            WrittenSoon();
        }
    });

    /// <summary>
    /// A write was heard: read once it has settled (<see cref="ReadDelay"/>). A burst pushes the read
    /// back, but never past <see cref="MostWaitMs"/> after its first write.
    /// </summary>
    private void WrittenSoon()
    {
        lock (gate)
        {
            var now = Environment.TickCount64;
            pendingSince ??= now;
            ReadSoon(ReadDelay(pendingSince.Value, now));
        }
    }

    /// <summary>
    /// How long after a write at <paramref name="now"/> the read waits, the first write not read yet
    /// having come at <paramref name="first"/>: <see cref="SettleMs"/>, cut short so the read comes
    /// at most <see cref="MostWaitMs"/> after the first.
    /// </summary>
    internal static int ReadDelay(long first, long now) => (int)Math.Clamp(first + MostWaitMs - now, 0, SettleMs);

    /// <summary>A read <paramref name="delayMs"/> from now, in place of one already waiting.</summary>
    private void ReadSoon(int delayMs)
    {
        lock (gate)
        {
            debounce ??= new Timer(static self => Troubles.Guard("Notifications: reading Windows' list", ((NotificationStore)self!).ReadNow), this, Timeout.Infinite, Timeout.Infinite);
            debounce.Change(delayMs, Timeout.Infinite);
        }
    }

    /// <summary>On a pool thread: one read at a time; a change meanwhile reads once more after it.</summary>
    private void ReadNow()
    {
        lock (gate)
        {
            // Every write heard so far is in the read that starts now (or in the one after it).
            pendingSince = null;
            if (reading)
            {
                again = true;
                return;
            }
            reading = true;
            work = Task.Run(() => Troubles.Guard("Notifications: reading Windows' list", () =>
            {
                try
                {
                    while (true)
                    {
                        // The files as the read begins: a write after this is one this read may not have seen.
                        var stamp = WpnStamp.Of(database);
                        var result = Wpn.Read(database, copyFolder);
                        lock (gate)
                        {
                            arrived = result;
                            // A read that failed on the way took no write: the check reads again (Check).
                            failed = result.Transient;
                            if (!failed)
                            {
                                taken = stamp;
                            }
                        }
                        // Nobody may have used the store yet (a Refresh before the first Use): then the
                        // read waits in `arrived` for the first Deliver or Prepare, on the owner's thread.
                        Volatile.Read(ref signal)?.Notify();
                        lock (gate)
                        {
                            if (!again)
                            {
                                reading = false;
                                return;
                            }
                            again = false;
                        }
                    }
                }
                catch
                {
                    // A read that threw ends its loop too; the next write starts one. (Not a finally: a loop that ended
                    // cleanly may already have a successor, whose flag this must not clear.)
                    lock (gate)
                    {
                        reading = false;
                    }
                    throw;
                }
            }));
        }
    }

    /// <summary>On the dock's thread: the latest read, if one arrived.</summary>
    private void Apply()
    {
        WpnRead? fresh;
        lock (gate)
        {
            (fresh, arrived) = (arrived, null);
        }
        // A read that failed on the way leaves the last good one standing.
        if (fresh is not null && !(fresh.Transient && read is { Available: true }))
        {
            read = fresh;
            if (fresh.Available)
            {
                // Dismissals of notifications Windows no longer has are forgotten.
                State().Keep(fresh.Rows);
            }
            Recompute();
        }
    }

    /// <summary>After the dock changed the state: shown at once, kept, and the islands told.</summary>
    private void Changes()
    {
        SaveState();
        Recompute();
        signal?.Notify();
    }

    /// <summary>
    /// What the signal raises once for all the news since the last time: the finished read taken,
    /// and <see cref="Changed"/>. Only ever on the owner's thread — the signal is made there and takes
    /// its queue, and a direct <see cref="Deliver"/> from anywhere else is refused before it gets here.
    /// </summary>
    private void Raise()
    {
        Apply();
        Changed?.Invoke();
    }

    /// <summary>
    /// Claims the store for this thread on the first touch of its state, and refuses any other thread
    /// after that. A cross-thread call is a mistake in the caller, and saying so at the call is the
    /// difference between a failing line and a corrupted Dictionary three calls later on another
    /// thread. The dock has one thread, so this never fires there; the test process has one per
    /// offscreen test, and each has its own <see cref="Shared"/>.
    /// </summary>
    private void Own()
    {
        var me = Environment.CurrentManagedThreadId;
        var first = Interlocked.CompareExchange(ref owner, me, 0);
        if (first != 0 && first != me)
        {
            throw new InvalidOperationException(
                $"This notification store belongs to thread {first}, the first to use it, and was reached from thread {me}. "
                + "Its state is not safe across threads: each thread takes its own (NotificationStore.Shared is per thread).");
        }
    }

    private NotificationState State() => state ??= NotificationState.Load(ReadState());

    private void Recompute()
    {
        var current = State();
        var rows = read is { Available: true } known ? known.Rows : [];
        var now = Clock.RealUtc().ToFileTimeUtc();
        var shown = NotificationState.Visible(rows, current, now, Most);
        var items = new List<NotificationEntry>(shown.Count);
        foreach (var row in shown)
        {
            if (!apps.TryGetValue(row.AppId, out var app))
            {
                app = Resolve(row.AppId);
                apps[row.AppId] = app;
            }
            items.Add(new NotificationEntry(row.Id, row.AppId, app.Name, app.Icon, row.Title, row.Body,
                DateTime.FromFileTimeUtc(row.Arrival).ToLocalTime(), row.ImagePath, row.Offer, row.HeroPath, row.InlinePath));
        }
        Items = items;
        Unseen = shown.Count(row => row.Arrival > current.SeenUpTo);
        Badges = WpnBadges.Current(read is { Available: true } badged ? badged.Badges : null, now);
        Version++;
    }

    private (string Name, Pixels? Icon) Resolve(string appId)
    {
        try
        {
            return resolve(appId);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Console.Error.WriteLine($"Karpo: notification app not resolved: {error.GetType().Name}");
            return (NotificationText.FallbackName(appId), null);
        }
    }

    /// <summary>An app's name and icon as Windows' notification center shows them (NotificationApps).</summary>
    private static (string Name, Pixels? Icon) ResolveApp(string appId) => NotificationApps.Resolve(appId, NotificationApps.Windows);

    private string? ReadState()
    {
        if (stateFolder is null)
        {
            return IslandFiles.Read(StateFileName);
        }
        try
        {
            return File.ReadAllText(Path.Combine(stateFolder, StateFileName));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private void SaveState()
    {
        var json = State().Save();
        if (stateFolder is null)
        {
            IslandFiles.Write(StateFileName, json);
            return;
        }
        try
        {
            Directory.CreateDirectory(stateFolder);
            File.WriteAllText(Path.Combine(stateFolder, StateFileName), json);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Karpo: {StateFileName} not saved: {error.Message}");
        }
    }
}

/// <summary>One toast as read from the database, its XML already taken apart.</summary>
/// <param name="Arrival">FILETIME, UTC.</param>
/// <param name="Expiry">FILETIME, UTC; 0 for none.</param>
/// <param name="Offer">Its launch arguments, buttons and inputs (<see cref="ToastOffers"/>).</param>
internal readonly record struct WpnRow(long Id, long Arrival, long Expiry, string AppId, string Title, string Body, string? ImagePath, ToastOffer? Offer = null,
    string? HeroPath = null, string? InlinePath = null);

/// <summary>What one read of the database found.</summary>
/// <param name="Available">False when the database is missing, unreadable or of another shape.</param>
/// <param name="Transient">
/// The read failed on the way (an error from SQLite, a copy that failed), not because the
/// database is missing or of another shape: what the last good read found still stands.
/// </param>
/// <param name="Badges">The badge rows, newest first; null where none were read.</param>
internal sealed record WpnRead(bool Available, IReadOnlyList<WpnRow> Rows, string? Problem = null, bool Transient = false, IReadOnlyList<WpnBadge>? Badges = null);

/// <summary>What the store's look at the files found (<see cref="NotificationStore.Finds"/>).</summary>
internal enum CheckFinding
{
    /// <summary>Every write is read, or being read.</summary>
    Nothing,

    /// <summary>A write no read took, though the watch had its time to hear it.</summary>
    Unheard,

    /// <summary>The last read failed on the way (Windows held the database): its write is read again.</summary>
    Failed,
}

/// <summary>
/// The database's and its write-ahead log's size and last write (UTC ticks; -1 and 0 for a file that is not
/// there): what changes with every write Windows makes, looked at without opening either (§569).
/// </summary>
internal readonly record struct WpnStamp(long DbLength, long DbWrite, long WalLength, long WalWrite)
{
    public static WpnStamp Of(string database)
    {
        var db = new FileInfo(database);
        var wal = new FileInfo(database + "-wal");
        return new WpnStamp(db.Exists ? db.Length : -1, db.Exists ? db.LastWriteTimeUtc.Ticks : 0,
            wal.Exists ? wal.Length : -1, wal.Exists ? wal.LastWriteTimeUtc.Ticks : 0);
    }

    /// <summary>The newer of the two last writes.</summary>
    public long Newest => Math.Max(DbWrite, WalWrite);
}

/// <summary>One badge row: the app's AUMID, when it arrived and expires (FILETIME, UTC; 0 for never), and what it sets.</summary>
/// <param name="Badge">Null for a row that clears the badge (0, <c>none</c>, or nothing Windows shows).</param>
internal readonly record struct WpnBadge(string AppId, long Arrival, long Expiry, Badge? Badge);

/// <summary>Which badge each app shows, from the rows a read found.</summary>
internal static class WpnBadges
{
    /// <summary>
    /// Each app's newest row (<paramref name="rows"/> is newest first), where it sets a badge and has
    /// not expired by <paramref name="now"/>; an app whose newest row clears its badge shows none.
    /// </summary>
    public static IReadOnlyDictionary<string, Badge> Current(IReadOnlyList<WpnBadge>? rows, long now)
    {
        var badges = new Dictionary<string, Badge>(StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows ?? [])
        {
            if (!seen.Add(row.AppId))
            {
                continue;
            }
            if (row.Badge is { } badge && (row.Expiry == 0 || row.Expiry > now))
            {
                badges[row.AppId] = badge;
            }
        }
        return badges;
    }
}

/// <summary>Reading Windows' notification database, read-only.</summary>
internal static class Wpn
{
    /// <summary>Toasts, newest first, at most this many.</summary>
    private const int Limit = 500;

    /// <summary>
    /// The toasts in <paramref name="database"/>. Opened read-only; when Windows holds it busy or
    /// locked, a copy in <paramref name="copyFolder"/> is read instead. Never throws.
    /// </summary>
    public static WpnRead Read(string database, string copyFolder)
    {
        try
        {
            if (!File.Exists(database))
            {
                return new WpnRead(false, [], "no database");
            }
            var code = TryRead(ReadOnlyUri(database), Sqlite.OPEN_READONLY | Sqlite.OPEN_URI, out var result);
            if (code is Sqlite.BUSY or Sqlite.LOCKED or Sqlite.CANTOPEN)
            {
                var copy = Copy(database, copyFolder);
                // The copy is Karpo's own: opened for writing, so SQLite can
                // take the log into its fresh wal-index.
                code = copy is null ? code : TryRead(copy, Sqlite.OPEN_READWRITE, out result);
            }
            return result ?? new WpnRead(false, [], $"sqlite {code}", Transient: true);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            // Also a winsqlite3.dll that isn't there (DllNotFoundException): nothing to read with.
            return new WpnRead(false, [], error.GetType().Name, Transient: error is not (DllNotFoundException or EntryPointNotFoundException));
        }
    }

    /// <summary>A <c>file:</c> URI for SQLite that opens <paramref name="path"/> read-only, with every unsafe character escaped.</summary>
    internal static string ReadOnlyUri(string path)
    {
        var text = new StringBuilder("file:///");
        foreach (var b in Encoding.UTF8.GetBytes(path.Replace('\\', '/')))
        {
            if (char.IsAsciiLetterOrDigit((char)b) || b is (byte)'-' or (byte)'.' or (byte)'_' or (byte)'~' or (byte)'/' or (byte)':')
            {
                text.Append((char)b);
            }
            else
            {
                text.Append('%').Append(b.ToString("X2"));
            }
        }
        return text.Append("?mode=ro").ToString();
    }

    /// <summary>The primary result code; <paramref name="result"/> is set when the read was complete (available or not).</summary>
    private static int TryRead(string name, int flags, out WpnRead? result)
    {
        result = null;
        var code = Sqlite.Open(name, flags, out var db);
        try
        {
            if (code != Sqlite.OK)
            {
                return code;
            }
            Sqlite.BusyTimeout(db, 250);
            var notification = Columns(db, "Notification", out code);
            if (code != Sqlite.OK)
            {
                return code;
            }
            var handler = Columns(db, "NotificationHandler", out code);
            if (code != Sqlite.OK)
            {
                return code;
            }
            if (!Has(notification, "Id", "HandlerId", "Type", "Payload", "ArrivalTime") || !Has(handler, "RecordId", "PrimaryId"))
            {
                result = new WpnRead(false, [], "unknown schema");
                return Sqlite.OK;
            }
            var expiry = notification.Contains("ExpiryTime") ? "n.ExpiryTime" : "0";
            var sql = $"SELECT n.Id, n.ArrivalTime, {expiry}, n.Payload, h.PrimaryId FROM Notification AS n " // not-ui
                + "JOIN NotificationHandler AS h ON h.RecordId = n.HandlerId " // not-ui
                + $"WHERE n.Type = 'toast' ORDER BY n.ArrivalTime DESC, n.Id DESC LIMIT {Limit}"; // not-ui
            var rows = new List<WpnRow>();
            code = Sqlite.Query(db, sql, row =>
            {
                var appId = row.Text(4);
                if (string.IsNullOrWhiteSpace(appId) || ToastXml.Parse(row.Payload(3)) is not { } toast)
                {
                    return;
                }
                rows.Add(new WpnRow(row.Int64(0), row.Int64(1), row.Int64(2), appId, toast.Title, toast.Body, toast.ImagePath, toast.Offer, toast.HeroPath, toast.InlinePath));
            });
            if (code != Sqlite.OK)
            {
                return code;
            }
            // The badges, from the same table: a read of them that fails costs the badges, not the toasts.
            var badges = new List<WpnBadge>();
            var badged = Sqlite.Query(db, $"SELECT h.PrimaryId, n.ArrivalTime, {expiry}, n.Payload FROM Notification AS n " // not-ui
                + "JOIN NotificationHandler AS h ON h.RecordId = n.HandlerId " // not-ui
                + "WHERE n.Type = 'badge' ORDER BY n.ArrivalTime DESC, n.Id DESC", row => // not-ui
            {
                if (row.Text(0) is { } appId && !string.IsNullOrWhiteSpace(appId))
                {
                    badges.Add(new WpnBadge(appId, row.Int64(1), row.Int64(2), Badge.Parse(row.Payload(3))));
                }
            });
            result = new WpnRead(true, rows, Badges: badged == Sqlite.OK ? badges : null);
            return code;
        }
        finally
        {
            Sqlite.Close(db);
        }
    }

    private static HashSet<string> Columns(nint db, string table, out int code)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        code = Sqlite.Query(db, $"PRAGMA table_info({table})", row => names.Add(row.Text(1) ?? "")); // not-ui
        return names;
    }

    private static bool Has(HashSet<string> columns, params string[] names) => names.All(columns.Contains);

    /// <summary>
    /// The database and its write-ahead log copied into <paramref name="folder"/>, shared so Windows
    /// keeps writing; null when that fails too. The shared-memory index is left behind: a copy of it
    /// taken at another moment than the log's would be wrong, and SQLite builds a new one from the log.
    /// </summary>
    private static string? Copy(string database, string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            var target = Path.Combine(folder, Path.GetFileName(database));
            foreach (var suffix in new[] { "-wal", "-shm" })
            {
                File.Delete(target + suffix);
            }
            CopyShared(database, target);
            if (File.Exists(database + "-wal"))
            {
                CopyShared(database + "-wal", target + "-wal");
            }
            return target;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static void CopyShared(string from, string to)
    {
        using var source = new FileStream(from, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var target = new FileStream(to, FileMode.Create, FileAccess.Write, FileShare.None);
        source.CopyTo(target);
    }
}

/// <summary>The parts of a toast's XML the list shows.</summary>
internal static class ToastXml
{
    /// <summary>
    /// The first <c>binding</c> of template ToastGeneric (or else the first binding): its first
    /// text is the title, the others the body, one per line; attribution text is left out. The
    /// image with placement appLogoOverride, the hero (placement hero) and the first inline image (no
    /// placement), each where it is a local file. What it offers beyond its words — its launch
    /// arguments, its buttons and its inputs (<see cref="ToastOffers.Read"/>). Null for XML that
    /// can't be read or has no text.
    /// </summary>
    public static (string Title, string Body, string? ImagePath, ToastOffer Offer, string? HeroPath, string? InlinePath)? Parse(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return null;
        }
        XmlDocument document;
        try
        {
            document = new XmlDocument();
            document.LoadXml(xml);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            // Not XML, or not XML the system's parser takes: the toast is left out.
            return null;
        }
        IXmlNode? binding = null;
        foreach (var node in document.SelectNodes("//binding"))
        {
            binding ??= node;
            if (string.Equals(Attribute(node, "template"), "ToastGeneric", StringComparison.OrdinalIgnoreCase))
            {
                binding = node;
                break;
            }
        }
        if (binding is null)
        {
            return null;
        }
        var texts = new List<string>();
        foreach (var node in binding.SelectNodes("descendant::text"))
        {
            if (!string.Equals(Attribute(node, "placement"), "attribution", StringComparison.OrdinalIgnoreCase)
                && Clean(node.InnerText ?? "") is { Length: > 0 } line)
            {
                texts.Add(line);
            }
        }
        if (texts.Count == 0)
        {
            return null;
        }
        string? image = null;
        string? hero = null;
        string? inline = null;
        foreach (var node in binding.SelectNodes("descendant::image"))
        {
            var placement = Attribute(node, "placement") ?? "";
            if (LocalFile(Attribute(node, "src")) is not { } path)
            {
                continue;
            }
            if (string.Equals(placement, "appLogoOverride", StringComparison.OrdinalIgnoreCase))
            {
                image ??= path;
            }
            else if (string.Equals(placement, "hero", StringComparison.OrdinalIgnoreCase))
            {
                hero ??= path;
            }
            else if (placement.Length == 0 || string.Equals(placement, "inline", StringComparison.OrdinalIgnoreCase))
            {
                inline ??= path;
            }
        }
        var root = document.DocumentElement is { } element && string.Equals(element.NodeName, "toast", StringComparison.OrdinalIgnoreCase)
            ? element
            : null;
        return (texts[0], string.Join('\n', texts.Skip(1)), image, ToastOffers.Read(root), hero, inline);
    }

    private static string? Attribute(IXmlNode node, string name) => node.Attributes?.GetNamedItem(name)?.NodeValue as string;

    /// <summary>A local file's path from an image source: a file URI or an absolute path; not the web, not app data.</summary>
    internal static string? LocalFile(string? source)
    {
        var text = source?.Trim() ?? "";
        if (text.Length == 0)
        {
            return null;
        }
        string path;
        if (text.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || !uri.IsFile)
            {
                return null;
            }
            path = uri.LocalPath;
        }
        else if (Path.IsPathFullyQualified(text))
        {
            path = text;
        }
        else
        {
            return null;
        }
        return File.Exists(path) ? path : null;
    }

    /// <summary>Runs of white space as one space, lines kept apart, the ends trimmed.</summary>
    private static string Clean(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n')
            .Select(line => string.Join(' ', line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)))
            .Where(line => line.Length > 0);
        return string.Join('\n', lines);
    }
}

/// <summary>What Karpo remembers about notifications: which are dismissed, and up to when they were seen.</summary>
internal sealed class NotificationState
{
    /// <summary>The newest arrival (FILETIME) the list was open for.</summary>
    public long SeenUpTo { get; set; }

    /// <summary>"id:arrival", so an id Windows hands out again doesn't stay hidden.</summary>
    public HashSet<string> Dismissed { get; } = new(StringComparer.Ordinal);

    public static string KeyOf(long id, long arrival) => $"{id}:{arrival}";

    public static NotificationState Load(string? json)
    {
        var state = new NotificationState();
        if (string.IsNullOrWhiteSpace(json))
        {
            return state;
        }
        try
        {
            if (JsonSerializer.Deserialize(json, IslandJson.Default.NotificationsFile) is { } file)
            {
                state.SeenUpTo = Math.Max(0, file.SeenUpTo);
                foreach (var key in file.Dismissed ?? [])
                {
                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        state.Dismissed.Add(key);
                    }
                }
            }
        }
        catch (JsonException)
        {
        }
        return state;
    }

    public string Save() => JsonSerializer.Serialize(
        new NotificationsFile { SeenUpTo = SeenUpTo, Dismissed = [.. Dismissed.Order(StringComparer.Ordinal)] },
        IslandJson.Default.NotificationsFile);

    /// <summary>Forgets the dismissals of notifications that are no longer in <paramref name="rows"/>.</summary>
    public void Keep(IReadOnlyList<WpnRow> rows)
    {
        var present = rows.Select(row => KeyOf(row.Id, row.Arrival)).ToHashSet(StringComparer.Ordinal);
        Dismissed.RemoveWhere(key => !present.Contains(key));
    }

    /// <summary>
    /// What the list shows of <paramref name="rows"/> at <paramref name="now"/> (FILETIME): not
    /// dismissed, not expired, newest first, at most <paramref name="most"/>.
    /// </summary>
    public static List<WpnRow> Visible(IReadOnlyList<WpnRow> rows, NotificationState state, long now, int most) =>
        rows.Where(row => (row.Expiry <= 0 || row.Expiry > now) && !state.Dismissed.Contains(KeyOf(row.Id, row.Arrival)))
            .OrderByDescending(row => row.Arrival)
            .ThenByDescending(row => row.Id)
            .Take(most)
            .ToList();
}

/// <summary>An app's notifications as the panel groups them: newest first, the app named and pictured once.</summary>
internal sealed record NotificationGroup(string AppId, string AppName, Pixels? AppIcon, List<NotificationEntry> Items);

/// <summary>Windows' own notification list, where a dismissal in Karpo clears it too.</summary>
internal interface INotificationRemover
{
    /// <summary>Takes these out of Windows' list. Returns at once; Windows is asked off the calling thread.</summary>
    void Remove(IReadOnlyList<long> ids);

    /// <summary>Clears Windows' whole list, as its own "Clear all" does. Returns at once.</summary>
    void Clear();
}

/// <summary>
/// UserNotificationListener, which Windows' notification center's own clearing goes through. Only
/// where Windows says access is allowed: Karpo does not ask (see the top of this file). Every call
/// is on the pool, because each is a call into the notification platform's service.
/// </summary>
internal sealed class WindowsNotificationRemover : INotificationRemover
{
    public void Remove(IReadOnlyList<long> ids)
    {
        var copy = ids.ToArray();
        _ = Task.Run(() => Listening(listener =>
        {
            foreach (var id in copy)
            {
                if (id is > 0 and <= uint.MaxValue)
                {
                    listener.RemoveNotification((uint)id);
                }
            }
        }));
    }

    public void Clear() => _ = Task.Run(() => Listening(listener => listener.ClearNotifications()));

    private static void Listening(Action<Windows.UI.Notifications.Management.UserNotificationListener> act)
    {
        try
        {
            var listener = Windows.UI.Notifications.Management.UserNotificationListener.Current;
            if (listener.GetAccessStatus() == Windows.UI.Notifications.Management.UserNotificationListenerAccessStatus.Allowed)
            {
                act(listener);
            }
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            // Hidden in Karpo all the same: the list answered the click, and Windows' list keeps it.
            Troubles.Note("Notifications: clearing them in Windows' list", error);
        }
    }
}

/// <summary>notifications.json.</summary>
internal sealed class NotificationsFile
{
    public long SeenUpTo { get; set; }

    public List<string>? Dismissed { get; set; }
}

/// <summary>How the notification list puts times and apps into words, and groups the list.</summary>
internal static class NotificationText
{
    /// <summary>
    /// "Now" under a minute, "5m" under an hour, "2h" earlier today, "Yesterday", and then the
    /// date: <paramref name="monthDay"/> this year ("Sep 15"), <paramref name="shortDate"/> before.
    /// </summary>
    public static string Relative(DateTime arrived, DateTime now, Func<DateOnly, string> monthDay, Func<DateOnly, string> shortDate)
    {
        var age = now - arrived;
        if (age < TimeSpan.FromMinutes(1))
        {
            return S.DockNotificationsNow;
        }
        if (age < TimeSpan.FromHours(1))
        {
            return S.DockNotificationsMinutesAgo((int)age.TotalMinutes);
        }
        if (arrived.Date == now.Date)
        {
            return S.DockNotificationsHoursAgo((int)age.TotalHours);
        }
        if (arrived.Date == now.Date.AddDays(-1))
        {
            return S.DockNotificationsYesterday;
        }
        var day = DateOnly.FromDateTime(arrived);
        return arrived.Year == now.Year ? monthDay(day) : shortDate(day);
    }

    /// <summary>
    /// Every notification, newest first, whichever app it came from: the order of a folder's
    /// notification list, whose rows each carry their own app's icon, and the order within an
    /// app's group of the panel's (<see cref="Groups"/>).
    /// </summary>
    public static List<NotificationEntry> Newest(IEnumerable<NotificationEntry> items) =>
        [.. items.OrderByDescending(item => item.Arrived).ThenByDescending(item => item.Id)];

    /// <summary>
    /// The panel's list, grouped by app as Windows 11's notification center groups it: an app's
    /// notifications together under its name, newest first, and the apps in the order of their
    /// newest — the app something last came from at the top. Two arrivals in the same instant keep
    /// the order of their ids, as <see cref="Newest"/> does, so a group's order never wobbles.
    /// </summary>
    public static List<NotificationGroup> Groups(IEnumerable<NotificationEntry> items)
    {
        var groups = new List<NotificationGroup>();
        var at = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in Newest(items))
        {
            if (at.TryGetValue(item.AppId, out var index))
            {
                groups[index].Items.Add(item);
                continue;
            }
            at[item.AppId] = groups.Count;
            groups.Add(new NotificationGroup(item.AppId, item.AppName, item.AppIcon, [item]));
        }
        return groups;
    }

    /// <summary>
    /// Whether <paramref name="appId"/> is one of Windows' own senders: a system toast, a Windows
    /// component, or a balloon of the notification area, which Explorer sends.
    /// </summary>
    public static bool IsWindows(string appId) =>
        appId.StartsWith("Windows.", StringComparison.OrdinalIgnoreCase)
        || appId.StartsWith("Microsoft.Windows.", StringComparison.OrdinalIgnoreCase)
        || appId.StartsWith("Microsoft.Explorer.", StringComparison.OrdinalIgnoreCase)
        || appId.StartsWith("NotifyIconGeneratedAumid_", StringComparison.OrdinalIgnoreCase);

    /// <summary>The words of an id that say nothing of the app: versions, and what kind of id it is.</summary>
    private static readonly HashSet<string> Noise = new(StringComparer.OrdinalIgnoreCase)
    {
        "app", "application", "desktop", "exe", "local", "notification", "notifications", "toast", "toasts",
    };

    /// <summary>
    /// A name for an app nothing else names (NotificationApps' last step), made from its AUMID: Windows'
    /// own are "Windows"; a package its name's last word; a path its program; a dotted id its last word
    /// that says something ("com.squirrel.Discord.Discord" → "Discord", "Microsoft.Office.OUTLOOK.EXE.15"
    /// → "Outlook"); a trailing hash and "Toast" left off ("ThunderbirdToast-D78BF5DD" → "Thunderbird").
    /// </summary>
    public static string FallbackName(string appId)
    {
        if (IsWindows(appId))
        {
            return "Windows";
        }
        var name = appId.Trim();
        var bang = name.IndexOf('!');
        if (bang > 0)
        {
            name = name[..bang];
        }
        var slash = name.LastIndexOfAny(['\\', '/']);
        if (slash >= 0 && slash < name.Length - 1)
        {
            name = Path.GetFileNameWithoutExtension(name[(slash + 1)..]);
            return name.Length > 0 ? name : appId;
        }
        var underscore = name.LastIndexOf('_');
        if (underscore > 0)
        {
            name = name[..underscore];
        }
        var dash = name.LastIndexOf('-');
        if (dash > 0 && IsHash(name.AsSpan(dash + 1)))
        {
            name = name[..dash];
        }
        var words = name.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var word = words.LastOrDefault(each => !Noise.Contains(each) && !each.All(char.IsAsciiDigit) && !IsHash(each));
        if (word is null)
        {
            return name.Length > 0 && !IsHash(name) ? name : appId;
        }
        if (word.Length > 5 && word.EndsWith("Toast", StringComparison.Ordinal))
        {
            word = word[..^5];
        }
        if (word.Length > 3 && word.All(each => !char.IsAsciiLetterLower(each)) && word.Any(char.IsAsciiLetterUpper))
        {
            // OUTLOOK → Outlook.
            word = word[0] + word[1..].ToLowerInvariant();
        }
        return char.IsAsciiLetterLower(word[0]) ? char.ToUpperInvariant(word[0]) + word[1..] : word;
    }

    /// <summary>Eight or more hex digits: a hash or a GUID's piece, no word.</summary>
    private static bool IsHash(ReadOnlySpan<char> text) => text.Length >= 8 && !text.ContainsAnyExcept("0123456789abcdefABCDEF{}-");
}

/// <summary>
/// The few winsqlite3.dll calls Karpo needs. winsqlite3 ships with Windows (System32), so reading
/// the notification database brings no SQLite of Karpo's own.
/// </summary>
internal static unsafe class Sqlite
{
    public const int OK = 0;
    public const int BUSY = 5;
    public const int LOCKED = 6;
    public const int CANTOPEN = 14;
    public const int ROW = 100;
    public const int DONE = 101;

    public const int OPEN_READONLY = 0x0000_0001;
    public const int OPEN_READWRITE = 0x0000_0002;
    public const int OPEN_CREATE = 0x0000_0004;
    public const int OPEN_URI = 0x0000_0040;

    private const int NULL = 5;

    /// <summary>A result row, valid only inside the callback it is passed to.</summary>
    public readonly ref struct Row(nint statement)
    {
        public long Int64(int column) => sqlite3_column_int64(statement, column);

        public string? Text(int column)
        {
            var text = sqlite3_column_text(statement, column);
            return text == null ? null : Encoding.UTF8.GetString(text, sqlite3_column_bytes(statement, column));
        }

        /// <summary>A text or blob column as text: UTF-8, or UTF-16 when it starts with that byte order mark.</summary>
        public string Payload(int column)
        {
            if (sqlite3_column_type(statement, column) == NULL)
            {
                return "";
            }
            var data = (byte*)sqlite3_column_blob(statement, column);
            var length = sqlite3_column_bytes(statement, column);
            if (data == null || length <= 0)
            {
                return "";
            }
            var bytes = new ReadOnlySpan<byte>(data, length);
            if (bytes.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xFE]))
            {
                return Encoding.Unicode.GetString(bytes[2..]);
            }
            if (bytes.StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]))
            {
                bytes = bytes[3..];
            }
            return Encoding.UTF8.GetString(bytes);
        }
    }

    /// <summary>The row callback of <see cref="Query"/>.</summary>
    public delegate void Each(Row row);

    public static int Open(string name, int flags, out nint db)
    {
        nint handle = 0;
        var bytes = Encoding.UTF8.GetBytes(name + "\0");
        int code;
        fixed (byte* path = bytes)
        {
            code = sqlite3_open_v2(path, &handle, flags, null);
        }
        db = handle;
        return code & 0xFF;
    }

    /// <summary>Closes a connection from <see cref="Open"/>; a zero handle is fine.</summary>
    public static void Close(nint db)
    {
        if (db != 0)
        {
            _ = sqlite3_close_v2(db);
        }
    }

    public static void BusyTimeout(nint db, int ms) => _ = sqlite3_busy_timeout(db, ms);

    /// <summary>Runs one statement and hands each row to <paramref name="each"/>; the primary result code.</summary>
    public static int Query(nint db, string sql, Each each)
    {
        nint statement = 0;
        var bytes = Encoding.UTF8.GetBytes(sql + "\0");
        int code;
        fixed (byte* text = bytes)
        {
            code = sqlite3_prepare_v2(db, text, -1, &statement, null) & 0xFF;
        }
        if (code != OK)
        {
            return code;
        }
        try
        {
            while (true)
            {
                code = sqlite3_step(statement) & 0xFF;
                if (code == ROW)
                {
                    each(new Row(statement));
                    continue;
                }
                return code == DONE ? OK : code;
            }
        }
        finally
        {
            _ = sqlite3_finalize(statement);
        }
    }

    /// <summary>Runs statements that return nothing (tests build their databases with it).</summary>
    public static int Execute(nint db, string sql)
    {
        var bytes = Encoding.UTF8.GetBytes(sql + "\0");
        fixed (byte* text = bytes)
        {
            return sqlite3_exec(db, text, null, null, null) & 0xFF;
        }
    }

    [DllImport("winsqlite3.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int sqlite3_open_v2(byte* filename, nint* db, int flags, byte* vfs);

    [DllImport("winsqlite3.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int sqlite3_close_v2(nint db);

    [DllImport("winsqlite3.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int sqlite3_busy_timeout(nint db, int ms);

    [DllImport("winsqlite3.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int sqlite3_prepare_v2(nint db, byte* sql, int bytes, nint* statement, byte** tail);

    [DllImport("winsqlite3.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int sqlite3_step(nint statement);

    [DllImport("winsqlite3.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int sqlite3_finalize(nint statement);

    [DllImport("winsqlite3.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern long sqlite3_column_int64(nint statement, int column);

    [DllImport("winsqlite3.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern byte* sqlite3_column_text(nint statement, int column);

    [DllImport("winsqlite3.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern void* sqlite3_column_blob(nint statement, int column);

    [DllImport("winsqlite3.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int sqlite3_column_bytes(nint statement, int column);

    [DllImport("winsqlite3.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int sqlite3_column_type(nint statement, int column);

    [DllImport("winsqlite3.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int sqlite3_exec(nint db, byte* sql, void* callback, void* argument, byte** error);
}
