// How busy the processor is: the share of processor time since the last
// sample that was not spent idle, over all cores.

using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

internal unsafe struct CpuSampler
{
    private ulong idle;
    private ulong total;

    /// <summary>0 to 1; -1 for the first sample, which only sets the baseline.</summary>
    public float Sample()
    {
        FILETIME idleTime, kernelTime, userTime;
        if (!GetSystemTimes(&idleTime, &kernelTime, &userTime))
        {
            return -1;
        }
        // Kernel time includes the idle time.
        ulong idleNow = Ticks(idleTime);
        ulong totalNow = Ticks(kernelTime) + Ticks(userTime);
        var usage = Busy(idle, total, idleNow, totalNow);
        idle = idleNow;
        total = totalNow;
        return usage;
    }

    /// <summary>
    /// The busy share between two readings of the system times, 0 to 1, and <b>-1 where there is
    /// no answer</b>: no baseline yet (the first sample), a total that did not move (two reads
    /// inside one clock tick), or a counter that went backwards (a wrap, or a resume that reset
    /// it). A wrap drawn as arithmetic rather than refused is a spike to 100 % that never happened,
    /// and the graph would keep it for as long as the history is.
    /// <para>
    /// Separated from the reading so that it can be tested: the reading needs a machine, and this
    /// needs four numbers.
    /// </para>
    /// </summary>
    internal static float Busy(ulong idleBefore, ulong totalBefore, ulong idleNow, ulong totalNow)
    {
        if (totalBefore == 0 || totalNow <= totalBefore || idleNow < idleBefore)
        {
            return -1;
        }
        var busy = 1.0 - ((double)(idleNow - idleBefore) / (totalNow - totalBefore));
        return (float)Math.Clamp(busy, 0, 1);
    }

    private static ulong Ticks(FILETIME time) => ((ulong)time.dwHighDateTime << 32) | time.dwLowDateTime;
}

/// <summary>Physical memory in use, as <c>GlobalMemoryStatusEx</c> reports it.</summary>
internal readonly record struct MemoryReading(uint Load, ulong Total, ulong Available)
{
    public ulong Used => Total > Available ? Total - Available : 0;

    public static unsafe MemoryReading? Read()
    {
        var status = new MEMORYSTATUSEX { dwLength = (uint)sizeof(MEMORYSTATUSEX) };
        return GlobalMemoryStatusEx(&status)
            ? new MemoryReading(Math.Min(status.dwMemoryLoad, 100u), status.ullTotalPhys, status.ullAvailPhys)
            : null;
    }
}
