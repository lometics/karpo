// One sampler of each counter for every performance widget on every bar.
//
// A widget kind per metric means four graphs on a bar are four islands, and
// four islands must not be four readers of the same counter: the processor's
// idle time is one number about one machine, and reading it twice in a tick
// costs twice and says the same thing. So the samplers are static and a
// reading younger than Fresh is handed out again instead of taken afresh.
// Two CPU widgets ticking in the same pass therefore cost one GetSystemTimes,
// and a CPU widget beside a memory widget costs one of each.
//
// Freshness rather than a shared clock, because the widgets do not share one:
// each has its own interval, and one at a second beside one at ten seconds
// must both be served. A widget that asks mid-window gets a reading measured
// over a shorter span than its own interval, which is still the right
// utilisation — a share of wall time is a share of wall time whatever the
// window — and the alternative, a sampler per interval, is the duplication
// this file exists to prevent.
//
// **Nothing here runs unless a performance source asks.** There is no timer of
// its own: the dock ticks the sources (Sources\PerformanceData.cs) — the five
// readings it always keeps (SourceKinds.Always) and one for every other pace or
// device the config's graphs name, shown or not — and the reading is taken then.
// Release lets the disks and the adapters go when the last source stops, which
// is the dock closing.
//
// Used on the dock's thread only.

using System.Diagnostics;

namespace Karpo.Dock;

internal static class SharedSamplers
{
    /// <summary>A reading younger than this is handed out again.</summary>
    private static readonly long Fresh = Stopwatch.Frequency / 4;

    private static readonly GpuSampler gpu = new();
    private static readonly NetworkSampler network = new();
    private static readonly DiskSampler disk = new();
    private static CpuSampler cpu;
    private static float cpuNow = -1;
    private static long cpuAt;
    private static bool gpuOpen;
    private static long gpuAt;
    private static long gpuOpenedAt;
    private static long networkAt;
    private static long networkResetAt;
    private static long diskAt;
    private static int users;

    /// <summary>
    /// A performance widget exists. Nothing is opened here — the first reading does that — but the
    /// count is what lets <see cref="Release"/> know when the last one has gone.
    /// </summary>
    public static void Acquire() => users++;

    /// <summary>
    /// A performance widget has gone. With the last of them every handle is let go and every
    /// baseline forgotten, so a bar that had a disk graph and has one no longer holds no device
    /// handles at all. The next widget placed opens what it needs, a sample later.
    /// </summary>
    public static void Release()
    {
        if (--users > 0)
        {
            return;
        }
        users = 0;
        disk.Reset();
        network.Reset();
        gpu.Adapters.Clear();
        gpuOpen = false;
        (cpuAt, gpuAt, networkAt, diskAt, networkResetAt, gpuOpenedAt) = (0, 0, 0, 0, 0, 0);
        cpuNow = -1;
    }

    /// <summary>How many performance widgets are asking; 0 means nothing is sampled at all.</summary>
    internal static int Users => users;

    /// <summary>
    /// Forgets every baseline, every handle and every count, as if no widget had ever asked. The
    /// tests' way to a known start: these samplers are process-wide by design, so a test that left
    /// a timestamp behind would decide what the next one reads. Never called by the dock.
    /// </summary>
    internal static void ForgetForTests()
    {
        users = 1;
        Release();
        (cpuReads, gpuReads, networkReads, diskReads) = (0, 0, 0, 0);
    }

    /// <summary>
    /// How many times each counter has actually been read of the machine. The claim this whole
    /// file makes is that four graphs on a bar are not four readers of one counter, and a claim
    /// about a cost is only worth making if something holds it: <c>SharedSamplerTests</c> asks
    /// several widgets' worth of readings inside one window and expects one read of each.
    /// </summary>
    internal static (long Cpu, long Gpu, long Network, long Disk) Reads => (cpuReads, gpuReads, networkReads, diskReads);

    private static long cpuReads;
    private static long gpuReads;
    private static long networkReads;
    private static long diskReads;

    /// <summary>0 to 1; -1 while not known.</summary>
    public static float Cpu(long now)
    {
        if (cpuAt == 0 || now - cpuAt >= Fresh)
        {
            cpuNow = cpu.Sample();
            cpuReads++;
            cpuAt = now;
        }
        return cpuNow;
    }

    /// <summary>
    /// The graphics sampler after a reading. The first call opens the adapters,
    /// which is a baseline and no reading yet.
    /// </summary>
    public static GpuSampler Gpu(long now)
    {
        if (!gpuOpen)
        {
            ReopenGpu(now);
        }
        else if (now - gpuAt >= Fresh)
        {
            gpu.Sample(now);
            gpuReads++;
            gpuAt = now;
        }
        return gpu;
    }

    /// <summary>The graphics sampler as it is, for tooltips: nothing is read.</summary>
    public static GpuSampler GpuState => gpu;

    /// <summary>The adapters were opened and nothing can be read from them.</summary>
    public static bool GpuFailed => gpuOpen && gpu.Failed;

    /// <summary>Adapters may have come or gone: opens them again, once for a burst of refreshes.</summary>
    public static void ReopenGpu(long now)
    {
        if (gpuOpen && now - gpuOpenedAt < Fresh)
        {
            return;
        }
        gpu.Open();
        gpuReads++;
        gpuOpen = true;
        gpuOpenedAt = now;
        gpuAt = now;
    }

    /// <summary>The network sampler after a reading.</summary>
    public static NetworkSampler Network(long now)
    {
        if (networkAt == 0 || now - networkAt >= Fresh)
        {
            network.Sample(now);
            networkReads++;
            networkAt = now;
        }
        return network;
    }

    /// <summary>The network sampler as it is, for a flyout: nothing is read.</summary>
    public static NetworkSampler NetworkState => network;

    /// <summary>Counters may have started over (a resume): measures from here, once for a burst of refreshes.</summary>
    public static void ResetNetwork(long now)
    {
        if (networkResetAt != 0 && now - networkResetAt < Fresh)
        {
            return;
        }
        network.Reset();
        network.Sample(now);
        networkResetAt = now;
        networkAt = now;
    }

    /// <summary>The disk sampler after a reading.</summary>
    public static DiskSampler Disk(long now)
    {
        if (diskAt == 0 || now - diskAt >= Fresh)
        {
            disk.Sample(now);
            diskReads++;
            diskAt = now;
        }
        return disk;
    }

    /// <summary>The disk sampler as it is, for tooltips: nothing is read.</summary>
    public static DiskSampler DiskState => disk;

    /// <summary>Disks may have come or gone, or their counters started over (a resume): measures from here.</summary>
    public static void ResetDisk(long now)
    {
        disk.Reset();
        disk.Sample(now);
        diskAt = now;
    }
}
