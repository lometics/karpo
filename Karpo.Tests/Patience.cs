// How long a test waits for something on its way, whatever else the machine is doing.
//
// A test here waits for work the code hands to another thread — a switch's first look on the pool,
// a notification store's read, a fetch standing in for the network — and it used to wait for it a
// fixed five or ten seconds. With ten builds and two suites running at once that was not enough, and
// not because the work was slow: when one of those waits ran out the pool had 25 threads and nearly
// 300 items waiting behind them (a failing switch test printed ThreadPool's own counts). The work was
// quick; it was queued behind the whole suite's, and the pool adds threads slowly while its own keep
// finishing items. So two things, and neither is a bigger number alone:
//
//   * **Room on the pool** (RoomOnThePool): the test process starts with as many pool threads as the
//     suite's parallel work can keep busy, so an item a test waits on is taken at once rather than
//     after whatever queued before it. The dock is one thread and a few pool items; the suite is
//     thousands of tests, many holding a thread while they wait on another, and it is the suite's
//     own crowding that is taken out of the picture.
//   * **A wait is on the thing itself, with a generous ceiling** (Ceiling, Until): a test that is
//     told, or sees, what it waits for goes on the moment it does — a passing run is no slower — and
//     only a test that would fail anyway waits the ceiling out. No test asserts how fast anything was.

using System.Runtime.CompilerServices;

namespace Karpo.Tests;

internal static class Patience
{
    /// <summary>
    /// The longest a test waits for what it was promised: long enough that only something that will
    /// never come reaches it, on a machine as busy as a gate running beside other gates.
    /// </summary>
    public static readonly TimeSpan Ceiling = TimeSpan.FromMinutes(2);

    /// <summary>The fewest pool threads the test process keeps ready (see the note at the top).</summary>
    private const int PoolThreads = 256;

    /// <summary>Waits until <paramref name="done"/> says so, or the <see cref="Ceiling"/>; whether it did.</summary>
    public static bool Until(Func<bool> done) => SpinWait.SpinUntil(done, Ceiling);

#pragma warning disable CA2255 // A test assembly is exactly where a module initializer belongs.
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void RoomOnThePool()
    {
        ThreadPool.GetMinThreads(out var workers, out var ports);
        ThreadPool.SetMinThreads(Math.Max(workers, PoolThreads), ports);
    }
}
