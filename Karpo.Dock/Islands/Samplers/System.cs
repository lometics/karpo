// What the machine says about itself that a reading's line at its tallest shows besides the
// reading, as its flyout did (PerformanceIsland, docs\widgets.md §97): the processes, threads and
// handles, what the system has committed and cached, how long it has run, and the processor's base
// speed. Each is one cheap call — K32's performance information is a few microseconds, the processor
// information is asked once — and it is read only for a line drawn tall enough to show it, once a
// gather of its source at most, never on the sources' tick: nothing here costs anything while
// nobody looks.
//
// Only what Windows answers the same way every time. The processor's *current* speed is left out:
// CallNtPowerInformation reports the base clock for it on most machines, and a figure that never
// moves under a label that says it does is worse than none.

using System.Runtime.InteropServices;

namespace Karpo.Dock;

/// <summary>What the system holds as a whole; zeros where the call failed (<see cref="Known"/>).</summary>
internal readonly record struct SystemReading(
    uint Processes,
    uint Threads,
    uint Handles,
    ulong Committed,
    ulong CommitLimit,
    ulong Cached)
{
    /// <summary>Whether Windows answered.</summary>
    public bool Known => Processes > 0;

    /// <summary>The system's counts and memory now; <see cref="Known"/> is false where it would not say.</summary>
    public static unsafe SystemReading Read()
    {
        var info = new PerformanceInformation { Size = (uint)sizeof(PerformanceInformation) };
        if (K32GetPerformanceInfo(&info, info.Size) == 0)
        {
            return default;
        }
        var page = (ulong)info.PageSize;
        return new SystemReading(info.ProcessCount, info.ThreadCount, info.HandleCount,
            (ulong)info.CommitTotal * page, (ulong)info.CommitLimit * page, (ulong)info.SystemCache * page);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PerformanceInformation
    {
        public uint Size;
        public nuint CommitTotal;
        public nuint CommitLimit;
        public nuint CommitPeak;
        public nuint PhysicalTotal;
        public nuint PhysicalAvailable;
        public nuint SystemCache;
        public nuint KernelTotal;
        public nuint KernelPaged;
        public nuint KernelNonpaged;
        public nuint PageSize;
        public uint HandleCount;
        public uint ProcessCount;
        public uint ThreadCount;
    }

    [DllImport("kernel32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern unsafe int K32GetPerformanceInfo(PerformanceInformation* info, uint size);
}

/// <summary>The processor, as Windows describes it; it does not change while the dock runs.</summary>
internal static unsafe class ProcessorInfo
{
    private const int ProcessorInformationLevel = 11;

    private static int baseMhz = -1;

    /// <summary>The logical processors this process can see.</summary>
    public static int Logical => Environment.ProcessorCount;

    /// <summary>The processor's base clock in MHz; 0 where Windows would not say. Asked once.</summary>
    public static int BaseMhz
    {
        get
        {
            if (baseMhz < 0)
            {
                baseMhz = ReadBase();
            }
            return baseMhz;
        }
    }

    private static int ReadBase()
    {
        var count = Math.Max(Logical, 1);
        var info = new ProcessorPowerInformation[count];
        fixed (ProcessorPowerInformation* at = info)
        {
            if (CallNtPowerInformation(ProcessorInformationLevel, null, 0, at, (uint)(count * sizeof(ProcessorPowerInformation))) != 0)
            {
                return 0;
            }
        }
        return (int)Math.Min(info[0].MaxMhz, 100_000);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessorPowerInformation
    {
        public uint Number;
        public uint MaxMhz;
        public uint CurrentMhz;
        public uint MhzLimit;
        public uint MaxIdleState;
        public uint CurrentIdleState;
    }

    [DllImport("powrprof.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int CallNtPowerInformation(int level, void* input, uint inputSize, ProcessorPowerInformation* output, uint outputSize);
}
