// Where a shared store's news is raised (Karpo.Dock\Islands\Shared.cs, StoreSignal).
//
// A store tells its islands it has news by waking them; with no island to wake, it raises Changed
// through a thread's dispatcher queue. Which thread's was decided in StoreSignal's field
// initialiser — the thread that *made* the store — and a store is often made by whichever thread
// first touched a Lazy, which is not necessarily the one using it (docs\widgets.md §11: making it
// asks Windows nothing, and the first look starts the work). Now the queue is taken at the first
// look: an island attaching, a Deliver, or a store's own door (CalendarStore.Open, a notification
// store's first Use) — and, for the one store made on purpose on the thread it serves, its making.
//
// Each test makes the store on one thread that pumps, as any UI thread does, and uses it from
// another; the question is only which of the two Changed arrives on.

using System.Collections.Concurrent;
using Karpo.Dock;
using Karpo.Dock.Favicons;
using Karpo.Dock.Native;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using DispatcherQueue = Windows.System.DispatcherQueue;
using IDispatcherQueueController = TerraFX.Interop.WinRT.IDispatcherQueueController;

namespace Karpo.Tests.Islands;

public sealed class StoreSignalThreadTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    /// <summary>
    /// A store made on one thread and used on another raises Changed on the one using it. Thread A
    /// makes a calendar store — the suite's first touch of a Lazy, from some other test's thread —
    /// and pumps; thread B opens it, which is the calendar's own first look, listens, and adds a
    /// calendar. With no island attached, the only way the news can reach B's listener is the
    /// signal's queue, so which thread the listener runs on is which queue the signal took.
    /// </summary>
    [Fact]
    public void ChangedArrivesOnTheThreadThatUsesTheStore()
    {
        CalendarStore? store = null;
        var raisedOn = new ConcurrentBag<int>();
        using var made = new ManualResetEventSlim(false);
        using var stop = new ManualResetEventSlim(false);
        var user = 0;

        var a = Sta(() =>
        {
            store = new CalendarStore();
            made.Set();
            Pump(() => stop.IsSet, TimeSpan.FromSeconds(30));
        });
        var b = Sta(() =>
        {
            Assert.True(made.Wait(Patience), "the store was never made");
            user = Environment.CurrentManagedThreadId;
            store!.Changed += () => raisedOn.Add(Environment.CurrentManagedThreadId);
            store.Open();
            store.Add(new ListCalendar("test:one", "One", default, []));
            Pump(() => !raisedOn.IsEmpty, Patience);
        });

        b.Join();
        stop.Set();
        a.Join();
        a.Rethrow();
        b.Rethrow();

        Assert.False(raisedOn.IsEmpty, "Changed was never raised on any thread");
        Assert.True(raisedOn.All(thread => thread == user),
            $"Changed was raised on thread {string.Join(", ", raisedOn.Distinct())}; the store was used on thread {user}");
    }

    /// <summary>
    /// A Deliver is a look: it is what an island's Refresh calls, on the dock's thread, and a store
    /// whose islands deliver its news — the contacts, the notifications — has its queue taken by
    /// the first of them rather than by whoever made it.
    /// </summary>
    [Fact]
    public void ADeliverIsALook()
    {
        StoreSignal? signal = null;
        var raisedOn = new ConcurrentBag<int>();
        using var made = new ManualResetEventSlim(false);
        using var stop = new ManualResetEventSlim(false);
        var user = 0;

        var a = Sta(() =>
        {
            signal = new StoreSignal(() => raisedOn.Add(Environment.CurrentManagedThreadId));
            made.Set();
            Pump(() => stop.IsSet, TimeSpan.FromSeconds(30));
        });
        var b = Sta(() =>
        {
            Assert.True(made.Wait(Patience), "the signal was never made");
            user = Environment.CurrentManagedThreadId;
            signal!.Deliver();
            // News from off the dock's thread, where a store's reads finish.
            Elsewhere(signal.Notify);
            Pump(() => !raisedOn.IsEmpty, Patience);
        });

        b.Join();
        stop.Set();
        a.Join();
        a.Rethrow();
        b.Rethrow();

        Assert.False(raisedOn.IsEmpty, "the news was never raised on any thread");
        Assert.True(raisedOn.All(thread => thread == user),
            $"the news was raised on thread {string.Join(", ", raisedOn.Distinct())}; the signal was delivered on thread {user}");
    }

    /// <summary>
    /// News that comes before anybody has looked is not raised on the thread that happened to make
    /// the store, and it is not lost either: it waits, and the first look raises it once, on the
    /// looker's thread, without the looker having to deliver. Nothing reads the queue before the
    /// look, because until then there is no thread it would be right to raise it on.
    /// </summary>
    [Fact]
    public void NewsBeforeTheFirstLookWaitsForIt()
    {
        StoreSignal? signal = null;
        DispatcherQueue? makers = null;
        var raisedOn = new ConcurrentBag<int>();
        using var made = new ManualResetEventSlim(false);
        using var stop = new ManualResetEventSlim(false);
        var user = 0;

        var a = Sta(() =>
        {
            signal = new StoreSignal(() => raisedOn.Add(Environment.CurrentManagedThreadId));
            makers = DispatcherQueue.GetForCurrentThread();
            made.Set();
            Pump(() => stop.IsSet, TimeSpan.FromSeconds(30));
        });
        var b = Sta(() =>
        {
            Assert.True(made.Wait(Patience), "the signal was never made");
            user = Environment.CurrentManagedThreadId;
            // News, with nobody looking yet.
            Elsewhere(signal!.Notify);
            // Everything the maker's queue was given before this marker has run once the marker
            // has: a queue runs in order. So if the news went there, it has been raised by now.
            using var drained = new ManualResetEventSlim(false);
            Assert.True(makers!.TryEnqueue(drained.Set), "the maker's queue took nothing");
            Assert.True(drained.Wait(Patience), "the maker's queue never ran");
            // And now the first look — a store's own door, not a delivery — and this thread pumps.
            signal.Look();
            Pump(() => !raisedOn.IsEmpty, Patience);
        });

        b.Join();
        stop.Set();
        a.Join();
        a.Rethrow();
        b.Rethrow();

        Assert.Equal([user], raisedOn.ToArray());
    }

    /// <summary>
    /// The one store that relies on the thread it is made on keeps it. The dock makes the favicon
    /// store at startup on its own thread because that is where a fetch posts its news back to, and
    /// nothing ever attaches an island to it or delivers it: its making is its only look. A signal
    /// that waited for a look it never gets would mean favicons that never arrive.
    /// <para>
    /// It waits on the fetch and then on the queue, never on a clock. It used to pump for five
    /// seconds and fail if nothing had come, and on a loaded machine — the whole suite at once —
    /// the pool can take longer than that to start a fetch at all, so it failed now and then with
    /// nothing wrong. The fetch is the store's own task (<see cref="FaviconStore.Working"/>); it
    /// queues its news before it finishes, so once it has finished the news is in this thread's
    /// queue, and a marker queued after it has run only once the news has.
    /// </para>
    /// </summary>
    [Fact]
    public void TheFaviconStoreRaisesOnTheThreadThatMadeIt()
    {
        var raisedOn = new ConcurrentBag<int>();
        var maker = 0;
        var a = Sta(() =>
        {
            maker = Environment.CurrentManagedThreadId;
            var store = new FaviconStore(new FaviconSource
            {
                Page = (_, _) => Task.FromResult<string?>(null),
                Fetch = (_, _) => Task.FromResult<(byte[], string?)?>((OnePixel, "image/png")),
            }, FaviconDisk.None);
            store.Changed += () => raisedOn.Add(Environment.CurrentManagedThreadId);
            _ = store.Picture("https://example.com/", 16);
            var work = store.Working("https://example.com");
            Assert.NotNull(work);
            // On the pool, and needing nothing of this thread: however long a busy machine takes to
            // run it, it ends, and this waits for exactly that.
            work.Wait();
            Assert.Equal(TaskStatus.RanToCompletion, work.Status);
            Drain();
        });

        a.Join();
        a.Rethrow();

        Assert.False(raisedOn.IsEmpty, "the favicon store never said a picture had arrived");
        Assert.Equal([maker], raisedOn.ToArray());
    }

    /// <summary>
    /// Runs everything this thread's dispatcher queue holds now, and nothing is left to chance
    /// about when: a marker queued behind it runs after it, as a queue runs in order, so once the
    /// marker has run so has all that was there before it. No clock is involved; the loop ends when
    /// the queue reaches the marker.
    /// </summary>
    private static unsafe void Drain()
    {
        var reached = false;
        Assert.True(DispatcherQueue.GetForCurrentThread().TryEnqueue(() => reached = true), "this thread's queue took nothing");
        while (!reached)
        {
            MSG message;
            if (GetMessageW(&message, HWND.NULL, 0, 0) <= 0)
            {
                break;
            }
            _ = TranslateMessage(&message);
            _ = DispatchMessageW(&message);
        }
        Assert.True(reached, "this thread's queue stopped before it reached the marker");
    }

    /// <summary>A transparent PNG of one pixel: the least a fetch can find and decode.</summary>
    private static readonly byte[] OnePixel = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    /// <summary>
    /// Runs <paramref name="news"/> on a thread with no dispatcher queue and waits for it: where a
    /// store's reads finish. Which thread says there is news never decides where it is raised.
    /// </summary>
    private static void Elsewhere(Action news)
    {
        var thread = new Thread(() => news());
        thread.Start();
        thread.Join();
    }

    // --- Threads with a dispatcher queue ------------------------------------------------

    /// <summary>A started STA thread with a dispatcher queue of its own, running <paramref name="body"/>.</summary>
    private static unsafe Worker Sta(Action body)
    {
        var worker = new Worker();
        worker.Thread = new Thread(() =>
        {
            try
            {
                var options = new DispatcherQueueOptions
                {
                    dwSize = (uint)sizeof(DispatcherQueueOptions),
                    threadType = DISPATCHERQUEUE_THREAD_TYPE.DQTYPE_THREAD_CURRENT,
                    apartmentType = DISPATCHERQUEUE_THREAD_APARTMENTTYPE.DQTAT_COM_STA,
                };
                IDispatcherQueueController* controller = null;
                ComUtil.Check(CreateDispatcherQueueController(options, &controller));
                try
                {
                    body();
                }
                finally
                {
                    controller->Release();
                }
            }
            catch (Exception error)
            {
                worker.Failed = error;
            }
        });
        worker.Thread.SetApartmentState(ApartmentState.STA);
        worker.Thread.Start();
        return worker;
    }

    /// <summary>Dispatches this thread's messages — its dispatcher queue's work among them — until <paramref name="done"/>.</summary>
    private static unsafe void Pump(Func<bool> done, TimeSpan patience)
    {
        var until = DateTime.UtcNow + patience;
        while (!done() && DateTime.UtcNow < until)
        {
            MSG message;
            while (PeekMessageW(&message, HWND.NULL, 0, 0, PM.PM_REMOVE))
            {
                _ = TranslateMessage(&message);
                _ = DispatchMessageW(&message);
            }
            Thread.Sleep(1);
        }
    }

    private sealed class Worker
    {
        public Thread Thread { get; set; } = null!;

        public Exception? Failed { get; set; }

        public void Join() => Thread.Join();

        public void Rethrow()
        {
            if (Failed is not null)
            {
                throw new Xunit.Sdk.XunitException("On a store's thread: " + Failed);
            }
        }
    }
}
