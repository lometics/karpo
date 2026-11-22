// pins: what each pin's target resolved to is kept in one cache for the whole process (PinItems),
// and more than one thread asks it — the dock's, and a test's or anything else that resolves a pin
// off the dock's thread. A plain dictionary written from two threads at once corrupts: it was
// found when two test classes made their pins at the same moment and one of them saw "Operations
// that change non-concurrent collections must have exclusive access". This holds that it cannot.

using System.Collections.Concurrent;
using Karpo.Core;
using Karpo.Dock;

namespace Karpo.Tests.Pinning;

public sealed class PinCacheTests
{
    /// <summary>How many times the threads meet: once is usually enough to break a plain dictionary, three all but always.</summary>
    private const int Rounds = 3;

    private const int Threads = 8;

    private const int Each = 2000;

    /// <summary>
    /// Eight threads resolve two thousand pins each, all at once, half of them the same targets on
    /// every thread and half each thread's own, while a ninth forgets them all over and over
    /// (<see cref="PinItems.Keep"/>, as the dock does when the settings change): the cache is read,
    /// written, grown and emptied from all of them together. Nothing throws, nothing hangs, and
    /// every pin comes back as its own target. None of the targets exists, so resolving one asks no
    /// shell and costs next to nothing, which is what makes the threads meet in the cache. Before the
    /// cache was made safe to share this failed on its first run.
    /// </summary>
    [Fact]
    public void PinsResolvedFromManyThreadsAtOnceEachComeBackAsTheirOwn()
    {
        try
        {
            for (var round = 0; round < Rounds; round++)
            {
                // Never made: every target under it is missing, and every round's are new.
                var root = Path.Combine(Path.GetTempPath(), "karpo-tests", "pin-cache-" + Guid.NewGuid().ToString("N"));
                var wrong = new ConcurrentQueue<string>();
                var resolving = Threads;
                using var start = new Barrier(Threads + 1);
                var threads = Enumerable.Range(0, Threads).Select(thread => Run(() =>
                {
                    start.SignalAndWait();
                    try
                    {
                        for (var index = 0; index < Each; index++)
                        {
                            var target = index % 2 == 0 ? Path.Combine(root, $"shared-{index}.exe") : Path.Combine(root, $"own-{thread}-{index}.exe");
                            var item = PinItems.For(new PinOptions { Target = target });
                            if (!string.Equals(item.Target, target, StringComparison.OrdinalIgnoreCase))
                            {
                                wrong.Enqueue($"{target} came back as {item.Target}");
                            }
                        }
                    }
                    catch (Exception error)
                    {
                        wrong.Enqueue($"thread {thread}: {error.GetType().Name}: {error.Message}");
                    }
                    finally
                    {
                        Interlocked.Decrement(ref resolving);
                    }
                })).ToList();
                threads.Add(Run(() =>
                {
                    start.SignalAndWait();
                    try
                    {
                        while (Volatile.Read(ref resolving) > 0)
                        {
                            PinItems.Keep([]);
                        }
                    }
                    catch (Exception error)
                    {
                        wrong.Enqueue($"forgetting: {error.GetType().Name}: {error.Message}");
                    }
                }));
                var finished = threads.All(thread => thread.Join(TimeSpan.FromSeconds(60)));
                Assert.True(finished, $"round {round}: a thread never finished — a lookup is going round a corrupted bucket");
                Assert.True(wrong.IsEmpty, $"round {round}: {wrong.Count} wrong:\n" + string.Join("\n", wrong.Take(10)));
            }
        }
        finally
        {
            // This takes them all out of the cache again.
            PinItems.Keep([]);
        }
    }

    /// <summary>A background thread running <paramref name="body"/>, started.</summary>
    private static Thread Run(Action body)
    {
        var thread = new Thread(() => body()) { IsBackground = true };
        thread.Start();
        return thread;
    }
}
