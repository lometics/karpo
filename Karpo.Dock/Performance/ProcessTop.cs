// The processes busiest on the processor now (docs\widgets.md §141): what the top processes' line
// shows (ProcessesLine), Task Manager's processes sorted by CPU.
//
// **One look, every process, no handles.** NtQuerySystemInformation(SystemProcessInformation) hands
// back every process with its image name and the processor time it has used — kernel and user,
// in 100-ns units — elevated and protected ones included, without opening any of them: what Task
// Manager reads too, and no WMI. Two looks apart are the figures: a process's time between them over
// the time that passed times the logical processors, its share of the whole processor as Task
// Manager's CPU column says it (Rank). The System Idle Process (id 0) is the processor doing nothing
// and is no process anybody wants listed.
//
// **An app's processes together**, as Task Manager's Apps group them ("Google Chrome (23)"): by
// image name, their shares summed. Where the image is is asked only for an app shown, once
// (SystemProcessIdInformation, again without a handle), for its name (its file's description) and
// its icon (ScreenTimeNames, which caches both).
//
// **The order** is the share as shown, to a tenth of a percent — two apps that both say 0.0 % are
// level, so nothing trades places for a difference nobody can see — then the time used since each
// started, then the name: the idle apps stand still at the foot, the heaviest first.
//
// **Ending a task** opens each process of the app for termination, only where it is still the one
// that was listed (its start time) and not one Windows cannot live without (IsProcessCritical), and
// never Karpo's own. Elevated processes refuse a process that is not, which is why the line's menu
// asks first whether any of them would end (CanEnd) and greys the row where none would.

using System.Diagnostics;
using System.Runtime.InteropServices;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock.Performance;

/// <summary>A process as one look saw it: its id, when it started, the processor time it has used (100 ns) and its image's name.</summary>
internal readonly record struct ProcessSample(uint Id, long Created, long Cpu, string Image);

/// <summary>
/// One look at every process: when it was taken, in 100-ns units of a clock that only goes forward,
/// and the logical processors the machine has.
/// </summary>
internal sealed record ProcessLook(long At, int Processors, ProcessSample[] Processes);

/// <summary>A process of an app, as a look listed it: enough to find it again, and to know it is still that one.</summary>
internal readonly record struct ProcessMember(uint Id, long Created);

/// <summary>An app's processes together, between two looks: its share of the whole processor, 0 to 1.</summary>
internal sealed record ProcessGroup(string Key, string Image, float Share, long Cpu, ProcessMember[] Members)
{
    /// <summary>How many processes it is.</summary>
    public int Count => Members.Length;
}

internal static class ProcessTop
{
    /// <summary>The System Idle Process: the processor doing nothing.</summary>
    public const uint IdleId = 0;

    /// <summary>The share as shown, in tenths of a percent: what the order compares.</summary>
    public static int Shown(float share) => (int)MathF.Round(Math.Clamp(share, 0f, 1f) * 1000f, MidpointRounding.AwayFromZero);

    /// <summary>
    /// The apps between <paramref name="before"/> and <paramref name="now"/>, busiest first: each
    /// app's processes' processor time between the two looks over the time that passed times the
    /// processors. A process the first look did not have started since, and all its time is in the
    /// interval; one whose start time differs is another with the same id. Empty where no time passed.
    /// </summary>
    public static List<ProcessGroup> Rank(ProcessLook before, ProcessLook now)
    {
        var budget = (double)(now.At - before.At) * Math.Max(1, now.Processors);
        if (budget <= 0)
        {
            return [];
        }
        var prior = new Dictionary<ProcessMember, long>(before.Processes.Length);
        foreach (var process in before.Processes)
        {
            prior[new ProcessMember(process.Id, process.Created)] = process.Cpu;
        }
        var apps = new Dictionary<string, (string Image, long Delta, long Cpu, List<ProcessMember> Members)>(StringComparer.OrdinalIgnoreCase);
        foreach (var process in now.Processes)
        {
            if (process.Id == IdleId)
            {
                continue;
            }
            var member = new ProcessMember(process.Id, process.Created);
            var delta = prior.TryGetValue(member, out var was) ? Math.Max(0, process.Cpu - was) : Math.Max(0, process.Cpu);
            var key = KeyOf(process.Image);
            if (!apps.TryGetValue(key, out var app))
            {
                app = (process.Image, 0, 0, []);
            }
            app.Members.Add(member);
            apps[key] = (app.Image, app.Delta + delta, app.Cpu + Math.Max(0, process.Cpu), app.Members);
        }
        var groups = new List<ProcessGroup>(apps.Count);
        foreach (var (key, app) in apps)
        {
            groups.Add(new ProcessGroup(key, app.Image, (float)Math.Clamp(app.Delta / budget, 0, 1), app.Cpu, [.. app.Members]));
        }
        groups.Sort(Order);
        return groups;
    }

    /// <summary>Busiest first by the share as shown; then the most time used; then the name.</summary>
    public static int Order(ProcessGroup a, ProcessGroup b)
    {
        var shown = Shown(b.Share).CompareTo(Shown(a.Share));
        if (shown != 0)
        {
            return shown;
        }
        var used = b.Cpu.CompareTo(a.Cpu);
        return used != 0 ? used : string.CompareOrdinal(a.Key, b.Key);
    }

    /// <summary>The app an image name is: the name itself, whatever its case; "" stays "".</summary>
    public static string KeyOf(string image) => image.ToLowerInvariant();

    /// <summary>A name with nothing else to go on: the image without ".exe" ("System", "Registry" as they are).</summary>
    public static string StemOf(string image) =>
        image.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && image.Length > 4 ? image[..^4] : image;

    /// <summary>"12.3%": a share to a tenth of a percent, as Task Manager's CPU column.</summary>
    public static string Percent(float share) =>
        (Shown(share) / 10.0).ToString("0.0", System.Globalization.CultureInfo.CurrentCulture) + "%";

    /// <summary>What every figure is measured as: the widest one, so the column never moves.</summary>
    public static string PercentRoom => (100.0).ToString("0.0", System.Globalization.CultureInfo.CurrentCulture) + "%";
}

/// <summary>Windows' processes, read without a handle; and ended, which takes one each.</summary>
internal static unsafe class ProcessReader
{
    private const int SystemProcessInformation = 5;
    private const int SystemProcessIdInformation = 88;
    private const int StatusInfoLengthMismatch = unchecked((int)0xC0000004);

    // ALL_PROCESSOR_GROUPS.
    private const ushort AllGroups = 0xFFFF;

    private static readonly Lock Gate = new();
    private static byte* buffer;
    private static nuint size;

    // The image paths found, by the process (its id and start): asked once each.
    private static readonly Dictionary<ProcessMember, string?> Paths = [];

    // The DOS devices each drive letter is ("\Device\HarddiskVolume3" for C:), read on first need and again on a miss.
    private static Dictionary<string, string>? devices;

    /// <summary>Every process now; null where Windows refused.</summary>
    public static ProcessLook? Read()
    {
        lock (Gate)
        {
            var at = (long)(Stopwatch.GetTimestamp() * (10_000_000.0 / Stopwatch.Frequency));
            if (buffer == null)
            {
                size = 512 * 1024;
                buffer = (byte*)NativeMemory.Alloc(size);
            }
            for (var tries = 0; tries < 6; tries++)
            {
                uint needed = 0;
                var status = NtQuerySystemInformation(SystemProcessInformation, buffer, (uint)size, &needed);
                if (status == StatusInfoLengthMismatch)
                {
                    size = Math.Max(size * 2, (nuint)needed + (64 * 1024));
                    NativeMemory.Free(buffer);
                    buffer = (byte*)NativeMemory.Alloc(size);
                    continue;
                }
                if (status < 0)
                {
                    return null;
                }
                var processes = new List<ProcessSample>(512);
                var entry = buffer;
                while (true)
                {
                    var head = (ProcessEntry*)entry;
                    var image = head->NameBuffer != null && head->NameLength > 0 ? new string(head->NameBuffer, 0, head->NameLength / 2) : "";
                    processes.Add(new ProcessSample((uint)head->UniqueProcessId, head->CreateTime, head->KernelTime + head->UserTime, image));
                    if (head->NextEntryOffset == 0)
                    {
                        break;
                    }
                    entry += head->NextEntryOffset;
                }
                var processors = (int)GetActiveProcessorCount(AllGroups);
                return new ProcessLook(at, processors > 0 ? processors : Environment.ProcessorCount, [.. processes]);
            }
            return null;
        }
    }

    /// <summary>
    /// Where <paramref name="group"/>'s image is, as a path File Explorer knows: asked of its
    /// processes in turn until one says; null where none does (System, Registry).
    /// </summary>
    public static string? PathOf(ProcessGroup group)
    {
        foreach (var member in group.Members.Take(4))
        {
            if (PathOf(member) is { } path)
            {
                return path;
            }
        }
        return null;
    }

    private static string? PathOf(ProcessMember member)
    {
        if (member.Id <= 4)
        {
            return null;
        }
        lock (Gate)
        {
            if (Paths.TryGetValue(member, out var known))
            {
                return known;
            }
        }
        var found = DosPath(NtPath(member.Id));
        lock (Gate)
        {
            if (Paths.Count >= 1024)
            {
                Paths.Clear();
            }
            Paths[member] = found;
        }
        return found;
    }

    /// <summary>The image's path as the kernel says it ("\Device\HarddiskVolume3\Windows\explorer.exe"); null where it does not.</summary>
    private static string? NtPath(uint id)
    {
        var chars = 1024;
        for (var tries = 0; tries < 3; tries++)
        {
            var text = new char[chars];
            fixed (char* at = text)
            {
                var info = new ProcessIdEntry { ProcessId = (nint)id, Length = 0, MaximumLength = (ushort)(chars * 2), Buffer = at };
                var status = NtQuerySystemInformation(SystemProcessIdInformation, &info, (uint)sizeof(ProcessIdEntry), null);
                if (status == StatusInfoLengthMismatch && info.MaximumLength > chars * 2 && chars < 32767)
                {
                    chars = Math.Min(32767, info.MaximumLength / 2 + 1);
                    continue;
                }
                return status >= 0 && info.Length > 0 ? new string(at, 0, info.Length / 2) : null;
            }
        }
        return null;
    }

    /// <summary><paramref name="path"/> from the kernel's devices to a drive letter or a share; null where it is neither.</summary>
    internal static string? DosPath(string? path, IReadOnlyDictionary<string, string>? map = null)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }
        if (path.StartsWith(@"\??\", StringComparison.Ordinal))
        {
            return path[4..];
        }
        if (path.StartsWith(@"\Device\Mup\", StringComparison.OrdinalIgnoreCase))
        {
            return @"\\" + path[@"\Device\Mup\".Length..];
        }
        for (var again = 0; again < 2; again++)
        {
            var known = map ?? Devices(again > 0);
            foreach (var (device, letter) in known)
            {
                if (path.Length > device.Length && path[device.Length] == '\\' && path.StartsWith(device, StringComparison.OrdinalIgnoreCase))
                {
                    return letter + path[device.Length..];
                }
            }
            if (map is not null)
            {
                break;
            }
        }
        return null;
    }

    private static Dictionary<string, string> Devices(bool fresh)
    {
        lock (Gate)
        {
            if (devices is not null && !fresh)
            {
                return devices;
            }
            var made = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var target = stackalloc char[1024];
            for (var letter = 'A'; letter <= 'Z'; letter++)
            {
                var drive = $"{letter}:";
                fixed (char* name = drive)
                {
                    var length = QueryDosDeviceW(name, target, 1024);
                    if (length > 0)
                    {
                        var device = new string(target);
                        if (device.Length > 0)
                        {
                            made.TryAdd(device, drive);
                        }
                    }
                }
            }
            devices = made;
            return made;
        }
    }

    /// <summary>
    /// Whether ending <paramref name="group"/> would end anything: not Karpo, not the kernel's own,
    /// and at least one of its processes opened for termination, still the one listed and not one
    /// Windows cannot live without.
    /// </summary>
    public static bool CanEnd(ProcessGroup group)
    {
        if (!Endable(group))
        {
            return false;
        }
        foreach (var member in group.Members.Take(8))
        {
            var process = OpenSame(member);
            if (process != HANDLE.NULL)
            {
                _ = CloseHandle(process);
                return true;
            }
        }
        return false;
    }

    /// <summary>Whether a group may be offered for ending at all: none of it Karpo's own process or the kernel's.</summary>
    internal static bool Endable(ProcessGroup group)
    {
        var own = (uint)Environment.ProcessId;
        return group.Members.Length > 0 && !group.Members.Any(member => member.Id <= 4 || member.Id == own);
    }

    /// <summary>Ends each of <paramref name="group"/>'s processes that may be ended; how many ended.</summary>
    public static int End(ProcessGroup group)
    {
        if (!Endable(group))
        {
            return 0;
        }
        var ended = 0;
        foreach (var member in group.Members)
        {
            var process = OpenSame(member);
            if (process == HANDLE.NULL)
            {
                continue;
            }
            try
            {
                if (TerminateProcess(process, 1))
                {
                    ended++;
                }
            }
            finally
            {
                _ = CloseHandle(process);
            }
        }
        return ended;
    }

    /// <summary>
    /// <paramref name="member"/> opened to be ended, where it is still the process that was listed
    /// (the same start) and not critical; null otherwise.
    /// </summary>
    private static HANDLE OpenSame(ProcessMember member)
    {
        var process = OpenProcess(PROCESS.PROCESS_TERMINATE | PROCESS.PROCESS_QUERY_LIMITED_INFORMATION, BOOL.FALSE, member.Id);
        if (process == HANDLE.NULL)
        {
            return HANDLE.NULL;
        }
        FILETIME creation, exit, kernel, user;
        BOOL critical = BOOL.TRUE;
        var same = GetProcessTimes(process, &creation, &exit, &kernel, &user)
            && (((long)creation.dwHighDateTime << 32) | creation.dwLowDateTime) == member.Created
            && IsProcessCritical(process, &critical) && !critical;
        if (!same)
        {
            _ = CloseHandle(process);
            return HANDLE.NULL;
        }
        return process;
    }

    // SYSTEM_PROCESS_INFORMATION's head, as far as it is read (winternl.h): every field to the id,
    // laid out as the 64-bit structure is.
    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessEntry
    {
        public uint NextEntryOffset;
        public uint NumberOfThreads;
        public long WorkingSetPrivateSize;
        public uint HardFaultCount;
        public uint NumberOfThreadsHighWatermark;
        public ulong CycleTime;
        public long CreateTime;
        public long UserTime;
        public long KernelTime;
        public ushort NameLength;
        public ushort NameMaximumLength;
        public char* NameBuffer;
        public int BasePriority;
        public nint UniqueProcessId;
    }

    // SYSTEM_PROCESS_ID_INFORMATION: the id asked about, and room for its image's path.
    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessIdEntry
    {
        public nint ProcessId;
        public ushort Length;
        public ushort MaximumLength;
        public char* Buffer;
    }

    [DllImport("ntdll.dll", ExactSpelling = true)]
    private static extern int NtQuerySystemInformation(int informationClass, void* information, uint length, uint* returned);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern uint GetActiveProcessorCount(ushort group);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern BOOL IsProcessCritical(HANDLE process, BOOL* critical);
}
