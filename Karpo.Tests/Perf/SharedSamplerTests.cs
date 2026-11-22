// perf: what the performance graphs cost, and the sharing that is the reason
// they can be afforded at all.
//
// The dock has no render loop. Sampling is a timer, and the whole design rests
// on two claims:
//
//   1. Four graphs on a bar are not four readers of one counter. Each widget is
//      its own island with its own history and its own pace, but the counters
//      behind them are one sampler each, and a reading younger than a quarter
//      second is handed out again. That is held here by counting the reads that
//      actually reached the machine.
//
//   2. Nothing is sampled when nothing is showing a graph. There is no timer of
//      this file's own: a reading is taken when an island asks, and the last
//      island taken off the bar lets the disk handles and the adapter list go.
//
// The cost test measures rather than asserts a number somebody remembered: it
// prints what each sampler took and fails only on an order-of-magnitude
// regression, because a tight bound on a timing test is a test that fails on
// somebody else's machine for no reason.
//
// These touch the real counters of the machine they run on, so they are gated
// with the drawing tests.

using System.Diagnostics;
using Karpo.Dock;
using Xunit.Abstractions;

namespace Karpo.Tests.Perf;

/// <summary>
/// The samplers are process-wide on purpose — that is the whole point of them — so the tests that
/// read their counts and their handles run one at a time, as the ones that share config.json do.
/// Without it a widget built in another class could let the last handle go halfway through a count.
/// </summary>
[CollectionDefinition(Collection, DisableParallelization = true)]
public sealed class SamplerState
{
    public const string Collection = "the shared samplers";
}

[Collection(SamplerState.Collection)]
public sealed class SharedSamplerTests
{
    private readonly ITestOutputHelper output;

    public SharedSamplerTests(ITestOutputHelper output)
    {
        this.output = output;
        SharedSamplers.ForgetForTests();
    }

    /// <summary>
    /// Loose on purpose: a hundred times what these cost on the machines they were measured on, so
    /// the test catches a sampler that started loading a driver or walking WMI and nothing else.
    /// The graphics adapter's is the big one, and known to be — the header of Gpu.cs says why.
    /// </summary>
    private const double CheapMs = 5;

    private const double GraphicsMs = 50;

    /// <summary>
    /// Several widgets' worth of readings inside one freshness window cost <b>one</b> read of each
    /// counter. This is the claim that lets somebody put the processor, the graphics, the memory
    /// and the network on one bar: four islands, four histories, one read of each counter a pass.
    /// </summary>
    [Karpo.Tests.Islands.UiFact]
    public void ManyWidgetsAskingAtOnceCostOneReadOfEachCounter()
    {
        var now = Stopwatch.GetTimestamp();
        // Warm every counter, so what is counted below is the sharing and not the opening.
        _ = SharedSamplers.Cpu(now);
        _ = SharedSamplers.Network(now);
        _ = SharedSamplers.Disk(now);
        _ = SharedSamplers.Gpu(now);

        var before = SharedSamplers.Reads;
        // A moment later, as four widgets ticking in one pass are: well inside the window.
        var pass = now + (Stopwatch.Frequency / 100);
        for (var widget = 0; widget < 8; widget++)
        {
            _ = SharedSamplers.Cpu(pass);
            _ = SharedSamplers.Network(pass);
            _ = SharedSamplers.Disk(pass);
            _ = SharedSamplers.Gpu(pass);
        }

        Assert.Equal(before, SharedSamplers.Reads);
    }

    /// <summary>
    /// A widget asking after the window does get a fresh reading: sharing must not become
    /// staleness, or a graph at ten seconds would draw the same figure ten times.
    /// </summary>
    [Karpo.Tests.Islands.UiFact]
    public void AWidgetAskingAfterTheWindowGetsAFreshReading()
    {
        var now = Stopwatch.GetTimestamp();
        _ = SharedSamplers.Cpu(now);

        var before = SharedSamplers.Reads.Cpu;
        _ = SharedSamplers.Cpu(now + Stopwatch.Frequency);

        Assert.Equal(before + 1, SharedSamplers.Reads.Cpu);
    }

    /// <summary>
    /// The last graph taken off the bar lets everything go: the disk handles, the adapter list and
    /// every baseline. "It costs nothing when nothing is showing a graph" is otherwise a claim
    /// about a timer rather than about what the process is holding.
    /// </summary>
    [Karpo.Tests.Islands.UiFact]
    public void TheLastWidgetTakenOffLetsEveryHandleGo()
    {
        var now = Stopwatch.GetTimestamp();
        SharedSamplers.Acquire();
        SharedSamplers.Acquire();
        _ = SharedSamplers.Disk(now);
        _ = SharedSamplers.Gpu(now);

        // One of two going changes nothing: the other is still drawing a graph.
        SharedSamplers.Release();
        Assert.Equal(1, SharedSamplers.Users);
        Assert.NotEmpty(SharedSamplers.DiskState.Tracked);

        SharedSamplers.Release();

        Assert.Equal(0, SharedSamplers.Users);
        Assert.Empty(SharedSamplers.DiskState.Tracked);
        Assert.Empty(SharedSamplers.GpuState.Adapters);
    }

    /// <summary>
    /// What one sample of each counter costs on this machine, printed so the number in the notes
    /// can be checked against the machine rather than believed. The bound is an order of magnitude
    /// loose: this is a guard against a sampler that started doing something expensive, not a
    /// benchmark.
    /// </summary>
    [Karpo.Tests.Islands.UiFact]
    public void EverySamplerIsCheapEnoughToRunEverySecond()
    {
        var costs = new List<(string What, double Ms, double Bound)>();

        // Opened first, and not timed: the first reading of each is a baseline and the graphics
        // one enumerates the adapters, which no later sample does.
        var warm = Stopwatch.GetTimestamp();
        _ = SharedSamplers.Cpu(warm);
        _ = SharedSamplers.Network(warm);
        _ = SharedSamplers.Disk(warm);
        _ = SharedSamplers.Gpu(warm);

        costs.Add(("processor", Cost(now => _ = SharedSamplers.Cpu(now)), CheapMs));
        costs.Add(("memory", Cost(now => _ = MemoryReading.Read()), CheapMs));
        costs.Add(("network", Cost(now => _ = SharedSamplers.Network(now)), CheapMs));
        costs.Add(("disk", Cost(now => _ = SharedSamplers.Disk(now)), CheapMs));
        // The graphics sampler reads the 3D engines and the recently busy ones every sample and
        // sweeps the idle rest every fourth, so a run of samples is what its cost means.
        costs.Add(("graphics", Cost(now => _ = SharedSamplers.Gpu(now)), GraphicsMs));

        // Always said, not only on a failure: what these cost is a number the notes quote, and a
        // number in notes that nobody can re-measure stops being true without anybody noticing.
        var said = string.Join("\n", costs.Select(cost => $"  {cost.What,-10} {cost.Ms:F3} ms a sample"));
        output.WriteLine($"one sample of each counter on this machine:\n{said}");

        var over = costs.Where(cost => cost.Ms > cost.Bound).ToList();
        Assert.True(over.Count == 0, $"a sampler got expensive:\n{said}");
    }

    /// <summary>
    /// One sample's cost in milliseconds: runs of samples, each with a timestamp past the freshness
    /// window so none of them is handed back the last one's answer, and the cheapest run's mean.
    /// A run is four samples, so each holds one of the graphics sampler's sweeps of its idle
    /// engines; the cheapest rather than the mean of all, because a sampler that got expensive is
    /// expensive in every run, while a machine busy with other work (builds beside the suite) slows
    /// only some of them, and that failed the test on the machine rather than on the sampler.
    /// </summary>
    private static double Cost(Action<long> sample)
    {
        const int Runs = 3;
        const int PerRun = 4;
        var now = Stopwatch.GetTimestamp();
        // Once more before timing, so nothing is a first touch of a page or a JIT.
        sample(now);
        var cheapest = double.MaxValue;
        var taken = 0;
        for (var run = 0; run < Runs; run++)
        {
            var started = Stopwatch.GetTimestamp();
            for (var one = 0; one < PerRun; one++)
            {
                sample(now + (++taken * Stopwatch.Frequency));
            }
            cheapest = Math.Min(cheapest, Stopwatch.GetElapsedTime(started).TotalMilliseconds / PerRun);
        }
        return cheapest;
    }
}
