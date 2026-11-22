// The network page's calls into Windows, made on a thread of their own and never on the dock's.
//
// The person (2026-10-10): "when i click connect on a network the mouse freezes for a second or
// so." Connect asked the WLAN service on the dock's thread — a profile written, WlanConnect, then
// the list read again — and the service answers only once it is done with what it is doing, which
// while it joins a network is a second or more. The flyout stood still for that long, and so did
// the pointer of the whole machine: an open menu's low-level mouse hook was the dock thread's, and
// Windows holds every mouse move for a hook until its thread answers (see LowLevelHooks.cs, which
// moved the hooks off that thread too). The cable's list asks the Network List Manager, which is as
// slow while a network is being identified.
//
// So each source runs behind an OffThread: a thread of its own makes every call to Windows in turn
// (Start, a connect, a scan, a read), and keeps what it last read. The page reads that, at once, and
// hears through its Changes when there is something new — the source's own news, or a read that came
// out different. A connect shows "Connecting…" at once (or asks for the key, where that is plain from
// the network); its outcome follows as Windows reports it. As the flyout opens, the first read is
// waited for a moment so the page opens with its networks rather than growing into them; a service
// slower than that opens the page empty, and the list follows.

using Karpo.Core;

namespace Karpo.Dock.QuickSettings.Services;

/// <summary>
/// One source's calls into Windows, in turn on a thread of its own, and what it last read there
/// (<typeparamref name="T"/>), for the dock's thread to read at once.
/// </summary>
internal sealed class OffThread<T> : IDisposable
    where T : class
{
    /// <summary>How long the flyout opening waits for the first read before it shows what it has.</summary>
    internal static readonly TimeSpan FirstRead = TimeSpan.FromMilliseconds(250);

    /// <summary>A read the page asks for itself (a wake, nothing heard) is made at most this often.</summary>
    private const long StaleMs = 1000;

    private readonly System.Collections.Concurrent.BlockingCollection<Action> queue = [];
    private readonly string what;
    private readonly Func<T> read;
    private readonly Func<T, T, bool> same;
    private readonly int changed;
    private volatile T state;
    private Changes? outer;
    private Changes? inner;
    private long readAt;
    private int asked;
    private bool stopped;

    /// <param name="what">What the work is, for a trouble report and the thread's name.</param>
    /// <param name="before">What is read before the first read is done.</param>
    /// <param name="read">The read, on the worker.</param>
    /// <param name="same">Whether two reads show the same.</param>
    /// <param name="changed">The news a read that came out different brings, nothing else having been heard.</param>
    public OffThread(string what, T before, Func<T> read, Func<T, T, bool> same, int changed)
    {
        this.what = what;
        this.read = read;
        this.same = same;
        this.changed = changed;
        state = before;
        // Nothing is read before Start: the first read follows it.
        readAt = Environment.TickCount64;
        var thread = new Thread(() => Troubles.Guard(what, Run)) { IsBackground = true, Name = "Karpo " + what }; // not-ui: a thread's name
        // COM made here (the Network List Manager) is the multithreaded apartment's.
        thread.SetApartmentState(ApartmentState.MTA);
        thread.Start();
    }

    /// <summary>The thread the calls are made on (for a test).</summary>
    internal int WorkerThread { get; private set; }

    /// <summary>
    /// What was read last, at once. One older than a second is read again behind it, so a page that
    /// wakes to look (a connection still being identified) sees what is new without anybody waiting.
    /// </summary>
    public T State
    {
        get
        {
            if (Environment.TickCount64 - Interlocked.Read(ref readAt) > StaleMs && Interlocked.Exchange(ref asked, 1) == 0)
            {
                Post(null);
            }
            return state;
        }
    }

    /// <summary>What the page shows until the next read: what the work just asked for will look like.</summary>
    public void Show(T expected) => state = expected;

    /// <summary>
    /// Starts the source on the worker, handing it the Changes it reports to, and waits a moment for the
    /// first read (<see cref="FirstRead"/>).
    /// </summary>
    public void Start(Changes news, Action<Changes> start)
    {
        outer = news;
        inner = new Changes(_ => Post(null));
        Post(() => start(inner));
        _ = Idle(FirstRead);
    }

    /// <summary>Runs <paramref name="work"/> on the worker, then reads again; <paramref name="news"/> is said either way.</summary>
    public void Post(Action? work, int news = 0)
    {
        try
        {
            queue.Add(() =>
            {
                if (stopped)
                {
                    return;
                }
                work?.Invoke();
                Read(news);
            });
        }
        catch (InvalidOperationException)
        {
            // Disposed: nothing more is done.
        }
    }

    /// <summary>Whether everything asked so far was done within <paramref name="wait"/>.</summary>
    public bool Idle(TimeSpan wait)
    {
        using var done = new ManualResetEventSlim(false);
        try
        {
            queue.Add(done.Set);
        }
        catch (InvalidOperationException)
        {
            return true;
        }
        return done.Wait(wait);
    }

    /// <summary>Ends the source on the worker, after whatever was asked before; nobody waits for it.</summary>
    public void Dispose(Action end)
    {
        try
        {
            queue.Add(() =>
            {
                stopped = true;
                end();
            });
            queue.CompleteAdding();
        }
        catch (InvalidOperationException)
        {
        }
    }

    void IDisposable.Dispose() => Dispose(() => { });

    private void Read(int news)
    {
        Interlocked.Exchange(ref asked, 0);
        var was = state;
        var now = read();
        state = now;
        Interlocked.Exchange(ref readAt, Environment.TickCount64);
        news |= inner?.Take() ?? 0;
        if (news == 0 && !same(was, now))
        {
            news = changed;
        }
        if (news != 0)
        {
            outer?.Add(news);
        }
    }

    private void Run()
    {
        WorkerThread = Environment.CurrentManagedThreadId;
        foreach (var work in queue.GetConsumingEnumerable())
        {
            Troubles.Guard(what, work);
        }
    }
}

/// <summary>Wi-Fi with every call into Windows on a thread of its own (see the top of the file).</summary>
internal sealed class OffThreadWifi : IWifiSource
{
    private readonly IWifiSource source;
    private readonly OffThread<WifiState> work;

    // What a connect asked for shows until it has been asked: a read made before then would take it back.
    private WifiAttempt? foreseen;
    private int connecting;

    public OffThreadWifi(IWifiSource source)
    {
        this.source = source;
        work = new OffThread<WifiState>(
            "Wi-Fi", // not-ui: a trouble report's subject
            new WifiState(null, false, WifiAccess.Allowed, [], true, null, null),
            Fresh,
            Same,
            WifiNews.Networks);
    }

    /// <summary>The worker (for a test).</summary>
    internal OffThread<WifiState> Work => work;

    public void Start(Changes changes) => work.Start(changes, source.Start);

    public WifiState Read() => work.State;

    public void Scan()
    {
        work.Show(work.State with { Scanning = true });
        work.Post(source.Scan);
    }

    public void SetRadio(bool on) => work.Post(() => source.SetRadio(on));

    public void Connect(WifiNetwork network, string? key, bool automatic, WifiSignIn? signIn = null)
    {
        // A hidden network's profile is looked up by the name typed, which only Windows can say.
        var shown = WifiProfiles.Foreseen(network, key, signIn, network.Hidden && network.Profile is null ? null : network.Profile is not null)
            ?? new WifiAttempt(network.Ssid, WifiOutcome.Connecting);
        Volatile.Write(ref foreseen, shown);
        Interlocked.Increment(ref connecting);
        work.Show(work.State with { Attempt = shown });
        work.Post(
            () =>
            {
                try
                {
                    source.Connect(network, key, automatic, signIn);
                }
                finally
                {
                    Interlocked.Decrement(ref connecting);
                }
            },
            WifiNews.Connection);
    }

    public bool ShowProperties(WifiNetwork network) => source.ShowProperties(network);

    public void Disconnect() => work.Post(source.Disconnect, WifiNews.Networks);

    public void Forget()
    {
        Volatile.Write(ref foreseen, null);
        work.Show(work.State with { Attempt = null });
        work.Post(source.Forget);
    }

    public void Dispose() => work.Dispose(source.Dispose);

    private WifiState Fresh()
    {
        var now = source.Read();
        return Volatile.Read(ref connecting) > 0 && Volatile.Read(ref foreseen) is { } shown ? now with { Attempt = shown } : now;
    }

    /// <summary>Whether two reads show the same: the networks by what they show, their names' bytes by value.</summary>
    internal static bool Same(WifiState a, WifiState b) =>
        a.RadioOn == b.RadioOn && a.CanSwitch == b.CanSwitch && a.Access == b.Access && a.Scanning == b.Scanning
        && a.Attempt == b.Attempt && a.Problem == b.Problem && a.Networks.Count == b.Networks.Count
        && a.Networks.Zip(b.Networks).All(pair => pair.First with { SsidBytes = pair.Second.SsidBytes } == pair.Second
            && pair.First.SsidBytes.AsSpan().SequenceEqual(pair.Second.SsidBytes));
}

/// <summary>The cables with every call into Windows on a thread of its own (see the top of the file).</summary>
internal sealed class OffThreadWired : IWiredSource
{
    private readonly IWiredSource source;
    private readonly OffThread<WiredState> work;

    public OffThreadWired(IWiredSource source)
    {
        this.source = source;
        work = new OffThread<WiredState>(
            "Wired connections", // not-ui: a trouble report's subject
            new WiredState([]),
            source.Read,
            static (a, b) => a.Adapters.SequenceEqual(b.Adapters),
            WiredNews.Adapters);
    }

    public void Start(Changes changes) => work.Start(changes, source.Start);

    public WiredState Read() => work.State;

    public void Dispose() => work.Dispose(source.Dispose);
}
