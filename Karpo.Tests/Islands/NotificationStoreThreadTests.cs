// NotificationStore belongs to one thread (the store's own header, and docs\widgets.md §11).
//
// The state behind Items — the app cache, the dismissed set, the last read — is the dock thread's,
// and none of it is safe across threads. Two things let a second thread in, and these tests drive
// each of them on purpose rather than waiting for the full suite's load to line them up:
//
//   * the store bound its news to whichever thread *made* it, by asking for that thread's dispatcher
//     queue in its constructor, so a store made on one UI thread and used on another did its work —
//     Recompute, and the Dictionary inside it — on the first;
//   * nothing refused a second thread, so the first sign of one was that Dictionary corrupted
//     somewhere inside Recompute, intermittently, under load.
//
// The overlap is made certain rather than likely by holding the stranger inside the store's app
// lookup (the resolve function a store is handed) until the thread that uses the store has come in
// too. The lookup is where the real one spends its milliseconds — a shell call per app — so it is
// also exactly where the real window was.

using System.Collections.Concurrent;
using Karpo.Dock;
using Karpo.Dock.Native;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using IDispatcherQueueController = TerraFX.Interop.WinRT.IDispatcherQueueController;

namespace Karpo.Tests.Islands;

public sealed class NotificationStoreThreadTests : IDisposable
{
    /// <summary>How long a thread waits for another: the suite's own ceiling, which a passing run never reaches.</summary>
    private static readonly TimeSpan Patience = Karpo.Tests.Patience.Ceiling;

    private readonly string folder = Path.Combine(Path.GetTempPath(), "karpo-tests", "wpn-threads-" + Guid.NewGuid().ToString("N"));

    public NotificationStoreThreadTests() => Directory.CreateDirectory(folder);

    public void Dispose()
    {
        try
        {
            Directory.Delete(folder, recursive: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
        }
    }

    private string Database => Path.Combine(folder, "db", "wpndatabase.db");

    private NotificationStore Store(Func<string, (string Name, Pixels? Icon)> resolve) =>
        new(Database, Path.Combine(folder, "copy"), Path.Combine(folder, "state"), resolve);

    /// <summary>
    /// The store's work runs on the thread that uses it, never on the thread that happened to make
    /// it, and never on two threads at once.
    /// <para>
    /// Thread A makes the store and then does what any UI thread does — pumps its messages — which
    /// is the full suite's first touch of <see cref="NotificationStore.Shared"/> from some other
    /// test's thread. Thread B uses it, as an island or a flyout would. The first read arrives and
    /// the store is told; whichever thread runs the store's work goes into the app lookup, and a
    /// thread that is not B is held there. B then makes a change of its own, which recomputes on B.
    /// If the work went to A, the two are inside Recompute together, by construction.
    /// </para>
    /// </summary>
    [Fact]
    public async Task TheStoresWorkRunsOnTheThreadThatUsesItAndOnlyThere()
    {
        FlyoutOffscreenTests.BuildDatabase(Database, 3);
        var user = new object();
        var owner = 0;
        var inside = 0;
        var most = 0;
        var calls = 0;
        var strangers = new ConcurrentBag<int>();
        using var hold = new ManualResetEventSlim(false);

        (string Name, Pixels? Icon) Resolve(string app)
        {
            var now = Interlocked.Increment(ref inside);
            for (var seen = Volatile.Read(ref most); now > seen; seen = Volatile.Read(ref most))
            {
                if (Interlocked.CompareExchange(ref most, now, seen) == seen)
                {
                    break;
                }
            }
            Interlocked.Increment(ref calls);
            try
            {
                if (Environment.CurrentManagedThreadId != Volatile.Read(ref owner))
                {
                    strangers.Add(Environment.CurrentManagedThreadId);
                    // Held, so that the user's own recompute next is certain to overlap this one.
                    hold.Wait(Patience);
                }
                return (app, null);
            }
            finally
            {
                Interlocked.Decrement(ref inside);
            }
        }

        NotificationStore? store = null;
        using var made = new ManualResetEventSlim(false);
        using var stop = new ManualResetEventSlim(false);

        // Thread A: makes the store, then pumps as a UI thread does.
        var a = Sta(() =>
        {
            store = Store(Resolve);
            made.Set();
            Pump(() => stop.IsSet, Patience);
        });

        // Thread B: uses it.
        var b = Sta(() =>
        {
            Assert.True(made.Wait(Patience), "the store was never made");
            Volatile.Write(ref owner, Environment.CurrentManagedThreadId);
            store!.Use(user);
            try
            {
                // The first read arrives and the store is told. Pump until its work has run somewhere.
                Pump(() => Volatile.Read(ref calls) > 0, Patience);
                // A change of B's own, which recomputes here.
                store.MarkSeen();
            }
            finally
            {
                hold.Set();
                store.Release(user);
            }
        });

        b.Join();
        stop.Set();
        a.Join();
        if (store is not null)
        {
            await store.Idle().WaitAsync(Patience);
        }
        a.Rethrow();
        b.Rethrow();

        Assert.True(Volatile.Read(ref calls) > 0, "the store's work never ran at all, on any thread");
        // The race, and then its cause: two threads inside Recompute together, because the store's
        // news was delivered on the thread that made it rather than the one that used it.
        Assert.True(most == 1 && strangers.IsEmpty,
            $"{most} threads were inside the store's Recompute at once; its work ran on thread "
            + $"{string.Join(", ", strangers.Distinct())}, which made it, as well as on thread {owner}, which used it");
    }

    /// <summary>
    /// A thread that is not the store's is refused at the door, with a reason, rather than let in to
    /// corrupt state it cannot share. A cross-thread call is a mistake in the caller; saying so where
    /// it is made is the difference between a failing line and a corrupted Dictionary three calls
    /// later on another thread.
    /// </summary>
    [Fact]
    public async Task ASecondThreadIsRefusedRatherThanLetIn()
    {
        FlyoutOffscreenTests.BuildDatabase(Database, 3);
        var store = Store(app => (app, null));
        var user = new object();
        using var used = new ManualResetEventSlim(false);
        using var done = new ManualResetEventSlim(false);
        var owner = Sta(() =>
        {
            store.Use(user);
            used.Set();
            Assert.True(done.Wait(Patience), "the test never let the owner go");
            store.Release(user);
        });
        try
        {
            Assert.True(used.Wait(Patience), "the owner never used the store");

            var refused = Assert.Throws<InvalidOperationException>(store.MarkSeen);
            Assert.Contains("thread", refused.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Throws<InvalidOperationException>(() => store.Use(new object()));
            Assert.Throws<InvalidOperationException>(store.Deliver);
        }
        finally
        {
            done.Set();
            owner.Join();
        }
        await store.Idle().WaitAsync(Patience);
        owner.Rethrow();
    }

    /// <summary>
    /// The shared store is the <b>thread's</b>, which in the dock is the one thread there is and in
    /// the test process is each test's own. A store shared across the suite's threads is the thing
    /// that let a second thread into the first one's state; and sharing it was wrong even without the
    /// race, since one thread's last user stopped the other thread's watch and one thread's "Clear
    /// all" emptied the other's list.
    /// </summary>
    [Fact]
    public void EachThreadHasASharedStoreOfItsOwn()
    {
        var here = NotificationStore.Shared;
        NotificationStore? there = null;
        var other = new Thread(() => there = NotificationStore.Shared);
        other.Start();
        other.Join();

        Assert.Same(here, NotificationStore.Shared);
        Assert.NotNull(there);
        Assert.NotSame(here, there);
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
