// perf: the arithmetic behind the performance graphs.
//
// Every sampler reads a *cumulative* counter and draws the difference between
// two readings. That arithmetic has three cases nobody can arrange on a
// machine, and all three of them are wrong in the same direction — they invent
// a reading that never happened and the graph keeps it for as long as its
// history is:
//
//   - the first sample, which has no baseline to subtract from;
//   - a counter that went backwards, because it wrapped or because a resume
//     reset it;
//   - a device that appeared between two samples, whose counter carries its
//     whole life rather than the last second of it.
//
// So the arithmetic is separated from the reading in each sampler, and it is
// these functions that are held here. Nothing in this file touches the machine.

using Karpo.Dock;

namespace Karpo.Tests.Perf;

public sealed class SamplerTests
{
    // --- The processor ------------------------------------------------------------------------

    /// <summary>
    /// The first sample has no baseline, so it can only set one: a share worked out from a zero
    /// baseline would be "everything this machine has ever done", which reads as 100 % busy for
    /// one sample every time Karpo starts.
    /// </summary>
    [Fact]
    public void ThereIsNoProcessorReadingUntilThereIsABaseline() =>
        Assert.Equal(-1, CpuSampler.Busy(idleBefore: 0, totalBefore: 0, idleNow: 800, totalNow: 1000));

    [Theory]
    // Half the time not idle is half busy.
    [InlineData(1000ul, 2000ul, 1500ul, 3000ul, 0.5f)]
    // Every tick idle.
    [InlineData(1000ul, 2000ul, 2000ul, 3000ul, 0f)]
    // No tick idle.
    [InlineData(1000ul, 2000ul, 1000ul, 3000ul, 1f)]
    public void TheProcessorsShareIsTheTimeItWasNotIdle(ulong idleBefore, ulong totalBefore, ulong idleNow, ulong totalNow, float expected) =>
        Assert.Equal(expected, CpuSampler.Busy(idleBefore, totalBefore, idleNow, totalNow), 4);

    /// <summary>
    /// A counter that went backwards wrapped or was reset. Refused, not computed: subtracting a
    /// larger number from a smaller one in unsigned arithmetic gives an enormous positive delta,
    /// which draws as a spike to the ceiling that the graph then carries for minutes.
    /// </summary>
    [Fact]
    public void AProcessorCounterThatWentBackwardsIsRefusedRatherThanWrapped()
    {
        // The idle time fell: a resume, or the counter wrapped.
        Assert.Equal(-1, CpuSampler.Busy(idleBefore: 900, totalBefore: 2000, idleNow: 100, totalNow: 3000));
        // The total fell, which is the same thing one level up.
        Assert.Equal(-1, CpuSampler.Busy(idleBefore: 100, totalBefore: 3000, idleNow: 200, totalNow: 2000));
    }

    /// <summary>Two reads inside one clock tick: no time passed, so there is nothing to divide by.</summary>
    [Fact]
    public void AProcessorReadingTakenTwiceInOneTickSaysNothing() =>
        Assert.Equal(-1, CpuSampler.Busy(idleBefore: 100, totalBefore: 2000, idleNow: 100, totalNow: 2000));

    /// <summary>
    /// The share is a share: a machine cannot be 140 % busy, however far the two counters drift
    /// from each other between the read of one and the read of the next.
    /// </summary>
    [Fact]
    public void TheProcessorsShareStaysAShare()
    {
        Assert.Equal(1f, CpuSampler.Busy(idleBefore: 100, totalBefore: 2000, idleNow: 100, totalNow: 2100));
        // More idle time passed than wall time, which the two reads being a moment apart allows.
        Assert.Equal(0f, CpuSampler.Busy(idleBefore: 0, totalBefore: 2000, idleNow: 200, totalNow: 2100));
    }

    // --- The disks ----------------------------------------------------------------------------

    [Theory]
    // A tenth of the span idle is nine tenths active. The span is in 100 ns, as the counter is.
    [InlineData(0ul, 1_000_000ul, 10_000_000.0, 0.9f)]
    [InlineData(0ul, 10_000_000ul, 10_000_000.0, 0f)]
    [InlineData(5_000_000ul, 5_000_000ul, 10_000_000.0, 1f)]
    public void ADisksActiveTimeIsTheSpanItWasNotIdle(ulong before, ulong now, double wall, float expected) =>
        Assert.Equal(expected, DiskSampler.Active(before, now, wall), 4);

    /// <summary>An idle counter that fell was reset; a span of nothing has nothing to divide by.</summary>
    [Fact]
    public void ADiskCounterThatWentBackwardsOrStoodStillIsRefused()
    {
        Assert.Equal(-1, DiskSampler.Active(idleBefore: 9_000_000, idleNow: 1_000, wall: 10_000_000));
        Assert.Equal(-1, DiskSampler.Active(idleBefore: 0, idleNow: 1_000, wall: 0));
    }

    /// <summary>
    /// A disk that reported more idle time than wall time is idle, not negatively busy: the
    /// counter and the clock are read at slightly different moments and the drift is real.
    /// </summary>
    [Fact]
    public void ADiskIsNeverMoreThanIdle() =>
        Assert.Equal(0f, DiskSampler.Active(idleBefore: 0, idleNow: 10_100_000, wall: 10_000_000));

    [Fact]
    public void BytesSinceTheLastReadingAreNothingWhenTheCounterWentBackwards()
    {
        Assert.Equal(500ul, DiskSampler.Since(now: 1500, before: 1000));
        Assert.Equal(0ul, DiskSampler.Since(now: 10, before: 1000));
    }

    // --- The network --------------------------------------------------------------------------

    /// <summary>
    /// An interface first seen this sample has no baseline, so it contributes nothing. Without
    /// this, plugging in a phone draws every byte it has ever carried as one second's traffic.
    /// </summary>
    [Fact]
    public void AnInterfaceWithNoBaselineContributesNothing()
    {
        Assert.Equal(0ul, NetworkSampler.Since(counted: false, now: 9_000_000_000, before: 0));
        Assert.Equal(1_000ul, NetworkSampler.Since(counted: true, now: 5_000, before: 4_000));
    }

    /// <summary>A counter that went backwards was reset: what it carried since is lost to this sample.</summary>
    [Fact]
    public void AnInterfaceCounterThatWentBackwardsContributesNothing() =>
        Assert.Equal(0ul, NetworkSampler.Since(counted: true, now: 10, before: 4_000));

    /// <summary>
    /// An adapter that disappears takes its counters with it, one that stays keeps them, and one
    /// that arrives starts with no baseline. That is the whole of what the rescan has to get
    /// right, and none of it can be arranged on a machine.
    /// </summary>
    [Fact]
    public void AnAdapterThatGoesIsForgottenAndOneThatArrivesHasNoBaseline()
    {
        var sampler = new NetworkSampler();
        sampler.Adopt([1, 2, 3]);

        // As a sample would leave them: every one of the three counted, with a reading each.
        sampler.Remember(1, received: 100, sent: 10);
        sampler.Remember(2, received: 200, sent: 20);
        sampler.Remember(3, received: 300, sent: 30);

        // 2 is unplugged and 9 is plugged in; 1 and 3 are untouched.
        sampler.Adopt([1, 3, 9]);

        var tracked = sampler.Tracked.ToDictionary(entry => entry.Luid);
        Assert.Equal([1ul, 3ul, 9ul], tracked.Keys.Order());
        Assert.Equal((100ul, 10ul, true), (tracked[1].Received, tracked[1].Sent, tracked[1].Counted));
        Assert.Equal((300ul, 30ul, true), (tracked[3].Received, tracked[3].Sent, tracked[3].Counted));
        // The new one: no baseline, so its first window adds nothing rather than its whole history.
        Assert.Equal((0ul, 0ul, false), (tracked[9].Received, tracked[9].Sent, tracked[9].Counted));
    }

    /// <summary>The one that went is gone rather than kept with a stale baseline under a reused slot.</summary>
    [Fact]
    public void AnAdapterThatGoesAndComesBackStartsOver()
    {
        var sampler = new NetworkSampler();
        sampler.Adopt([7]);
        sampler.Remember(7, received: 5_000, sent: 500);

        sampler.Adopt([]);
        sampler.Adopt([7]);

        var back = sampler.Tracked.Single();
        Assert.Equal((7ul, 0ul, 0ul, false), back);
    }

    /// <summary>Forgetting everything is what a resume does, and it must leave nothing behind.</summary>
    [Fact]
    public void AResetForgetsEveryInterfaceAndEveryRate()
    {
        var sampler = new NetworkSampler();
        sampler.Adopt([1, 2]);
        sampler.Remember(1, received: 100, sent: 10);

        sampler.Reset();

        Assert.Empty(sampler.Tracked);
        Assert.Equal(-1, sampler.Down);
        Assert.Equal(-1, sampler.Up);
    }

    // --- The graphics adapter -----------------------------------------------------------------

    /// <summary>
    /// An engine's share is its running time over the wall time between two readings. The first
    /// reading has no stamp, so it can only be a baseline.
    /// </summary>
    [Fact]
    public void ThereIsNoEngineReadingUntilThereIsABaseline() =>
        Assert.Equal(-1, GpuSampler.Share(runningBefore: 0, runningNow: 5_000_000, span: 0));

    [Theory]
    [InlineData(0L, 5_000_000L, 10_000_000.0, 0.5f)]
    [InlineData(1_000_000L, 1_000_000L, 10_000_000.0, 0f)]
    [InlineData(0L, 10_000_000L, 10_000_000.0, 1f)]
    // Running longer than the wall clock: several queues on one engine, or the two reads drifting.
    // A share is a share.
    [InlineData(0L, 30_000_000L, 10_000_000.0, 1f)]
    public void AnEnginesShareIsItsRunningTimeOverTheWallTime(long before, long now, double span, float expected) =>
        Assert.Equal(expected, GpuSampler.Share(before, now, span), 4);

    /// <summary>A running time that fell was reset — a driver restart — and says nothing this sample.</summary>
    [Fact]
    public void AnEngineCounterThatWentBackwardsIsRefused() =>
        Assert.Equal(0f, GpuSampler.Share(runningBefore: 9_000_000, runningNow: 10, span: 10_000_000));

    // --- Which graphics card ------------------------------------------------------------------

    private const ulong Mb = 1024 * 1024;

    /// <summary>
    /// A graphics widget shows one card, the main one where nobody named any: the person, "the
    /// graphics widget should only show information / details about one graphics card not all
    /// installed once" (2026-10-11). The main one has the most video memory of its own — the discrete
    /// card beside an integrated one, whichever drives the display — then the one with a display,
    /// then the first. Software adapters never reach the list (the sampler drops them).
    /// </summary>
    [Fact]
    public void TheMainCardHasTheMostVideoMemoryThenADisplayThenComesFirst()
    {
        Assert.Equal(-1, GpuSampler.MainOf([]));
        Assert.Equal(0, GpuSampler.MainOf([(128 * Mb, true)]));
        // A desktop: Intel UHD 770 first in DXGI's order, an RTX 4060 Ti beside it.
        Assert.Equal(1, GpuSampler.MainOf([(128 * Mb, true), (8192 * Mb, true)]));
        // A laptop: the integrated adapter drives the panel, the discrete one is the capable one.
        Assert.Equal(1, GpuSampler.MainOf([(128 * Mb, true), (6144 * Mb, false)]));
        // Two alike: the one a display is attached to, else the first.
        Assert.Equal(1, GpuSampler.MainOf([(8192 * Mb, false), (8192 * Mb, true)]));
        Assert.Equal(0, GpuSampler.MainOf([(8192 * Mb, true), (8192 * Mb, true)]));
        // DXGI did not answer: nothing known of any, so the first.
        Assert.Equal(0, GpuSampler.MainOf([(0, false), (0, false)]));
    }

    /// <summary>
    /// A widget follows the card it names, or the main one with none named — a settings file from
    /// before, whose empty name meant "the busiest", now reads as the main card. A card named and
    /// not attached is followed by nothing rather than by another.
    /// </summary>
    [Fact]
    public void AWidgetFollowsTheCardItNamesOrTheMainOne()
    {
        var gpu = new GpuSampler();
        Assert.Null(gpu.Followed("", PerformanceIsland.AdapterName));
        gpu.Adapters.Add(new GpuSampler.Adapter { Luid = default, Name = "Intel UHD Graphics 770", Engines = [], Memory = 128 * Mb, Displays = true });
        gpu.Adapters.Add(new GpuSampler.Adapter { Luid = default, Name = "NVIDIA GeForce RTX 4060 Ti", Engines = [], Memory = 8188 * Mb, Displays = true });
        Assert.Equal(1, gpu.MainIndex);
        Assert.Equal(1, gpu.Followed("", PerformanceIsland.AdapterName));
        Assert.Equal(0, gpu.Followed("GPU 0: Intel UHD Graphics 770", PerformanceIsland.AdapterName));
        Assert.Equal(1, gpu.Followed("GPU 1: NVIDIA GeForce RTX 4060 Ti", PerformanceIsland.AdapterName));
        Assert.Null(gpu.Followed("GPU 2: Razer Core X", PerformanceIsland.AdapterName));
        // The number is part of the name: the same model at another place is another card.
        Assert.Null(gpu.Followed("GPU 0: NVIDIA GeForce RTX 4060 Ti", PerformanceIsland.AdapterName));
    }
}
