// How busy the disks are, read the way Task Manager reads them.
//
// The storage driver keeps a running idle-time counter per physical disk, and
// the share of wall time a disk was *not* idle is its active time — the number
// Task Manager's Disk graph draws. IOCTL_DISK_PERFORMANCE hands that counter
// over directly, so nothing here sets up a performance-counter query (which
// costs megabytes) and nothing walks WMI (which starts a service).
//
// The handle is opened with **no access rights at all** (dwDesiredAccess = 0).
// That is what makes this work unelevated: asking for GENERIC_READ on
// \\.\PhysicalDriveN needs administrator, and a query IOCTL does not need it.
// A Karpo that had to run as administrator to show a disk graph would not ship.
//
// §557: and it is open only for the query. A handle kept on \\.\PhysicalDriveN between samples is a
// handle on every disk, a USB stick's too, and Windows will not eject a device something holds open
// ("This device is currently in use") — Karpo's own Eject, Explorer's and the tray's alike. Opening
// a disk that is there is a few microseconds; a second's sample of four disks costs nothing to see.
//
// Reading one disk is about 10-30 µs, measured on an NVMe and a SATA SSD, so
// every disk is read on every sample and there is no sweep to stagger like the
// graphics sampler's. The list of disks is found once a minute, because an
// external drive appearing is worth noticing and probing sixteen device paths
// is the expensive part (about 1 ms, nearly all of it the opens that fail).
//
// The machine is as busy as its busiest disk, which is the same answer the
// graphics sampler gives for engines: a PC waiting on one saturated disk is
// waiting, however idle the others are. Averaging would hide exactly the
// case somebody puts the widget on their bar to see.
//
// Each disk's own reading is kept as well (Disks), so a widget can follow one
// disk the way Task Manager has a graph for Disk 0 and another for Disk 1
// (PerformanceOptions.Disk). Following one costs nothing more: every disk is
// read on every sample anyway, and which one a widget draws is picked from
// what was read (Pick). The letters of the volumes on each disk, which is how
// Task Manager names them ("Disk 0 (C:)"), are found with the disks, once a
// minute.

using System.Diagnostics;
using System.Runtime.InteropServices;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

internal sealed unsafe class DiskSampler : IDisposable
{
    /// <summary>CTL_CODE(IOCTL_DISK_BASE, 0x0008, METHOD_BUFFERED, FILE_ANY_ACCESS).</summary>
    private const uint IOCTL_DISK_PERFORMANCE = 0x00070020;

    /// <summary>How many <c>\\.\PhysicalDriveN</c> paths are probed. Past this nobody is watching a graph.</summary>
    private const int MostDisks = 16;

    /// <summary>A disk plugged in is noticed this late, which is the price of not probing every sample.</summary>
    private const long RescanSeconds = 60;

    /// <summary>CTL_CODE(IOCTL_STORAGE_BASE, 0x0420, METHOD_BUFFERED, FILE_ANY_ACCESS).</summary>
    private const uint IOCTL_STORAGE_GET_DEVICE_NUMBER = 0x002D1080;

    /// <summary>FILE_DEVICE_DISK: a volume on a disk, rather than on a CD drive or nothing at all.</summary>
    private const uint FILE_DEVICE_DISK = 7;

    private readonly List<Disk> disks = [];
    private List<DiskReading> readings = [];
    private long scanned;
    private long stamp;

    /// <summary>0 to 1 over the span between the last two samples; -1 while not known.</summary>
    public float Usage { get; private set; } = -1;

    /// <summary>Bytes a second read at the last sample; -1 while not known.</summary>
    public double Read { get; private set; } = -1;

    /// <inheritdoc cref="Read"/>
    public double Written { get; private set; } = -1;

    /// <summary>Nothing can be read: no disk answered the query.</summary>
    public bool Failed => disks.Count == 0 && scanned != 0;

    /// <summary>
    /// The disks being followed, by device number. No handle is held on any of them between two
    /// samples (§557): each is opened for its query and closed after it.
    /// </summary>
    internal IEnumerable<int> Tracked => disks.Select(disk => disk.Number);

    /// <summary>
    /// Each disk's reading at the last sample, by device number: what a widget that follows one
    /// disk draws (<see cref="Pick"/>). Empty until two samples have been taken.
    /// </summary>
    public IReadOnlyList<DiskReading> Disks => readings;

    /// <summary>
    /// The disks being followed and the letters of the volumes on each, in number order: what the
    /// settings card offers ("Disk 0 (C:)") and what a tooltip names a disk by.
    /// </summary>
    public IReadOnlyList<(int Number, string[] Letters)> Named =>
        [.. disks.OrderBy(disk => disk.Number).Select(disk => (disk.Number, disk.Letters))];

    /// <summary>The letters of the volumes on disk <paramref name="number"/>; empty where it has none or is not followed.</summary>
    public string[] LettersOf(int number) => disks.FirstOrDefault(disk => disk.Number == number)?.Letters ?? [];

    /// <summary>Forgets everything, as after a resume: disks and counters may both have started over.</summary>
    public void Reset()
    {
        Close();
        scanned = 0;
        stamp = 0;
        Usage = -1;
        Read = -1;
        Written = -1;
        readings = [];
    }

    public void Sample(long now)
    {
        if (scanned == 0 || now - scanned > RescanSeconds * Stopwatch.Frequency)
        {
            Scan();
            scanned = now;
        }
        if (disks.Count == 0)
        {
            // The stamp moves on all the same: leaving it behind would make the span measured
            // against it the whole of the gap, the moment a disk came back.
            Usage = -1;
            readings = [];
            stamp = now;
            return;
        }
        // 100 ns, the unit the idle counter is kept in.
        var wall = stamp == 0 ? 0 : (now - stamp) * 10_000_000.0 / Stopwatch.Frequency;
        var seconds = (double)(now - stamp) / Stopwatch.Frequency;
        var read = new List<DiskReading>(disks.Count);
        for (int i = disks.Count - 1; i >= 0; i--)
        {
            var disk = disks[i];
            if (Reading(disk.Number) is not { } reading)
            {
                // Unplugged mid-sample: drop it and look for the new set next time.
                disks.RemoveAt(i);
                scanned = 0;
                continue;
            }
            if (disk.Counted && wall > 0)
            {
                read.Add(new DiskReading(
                    disk.Number,
                    Active(disk.Idle, reading.IdleTime, wall),
                    Since(reading.BytesRead, disk.Read) / seconds,
                    Since(reading.BytesWritten, disk.Written) / seconds));
            }
            disk.Idle = reading.IdleTime;
            disk.Read = reading.BytesRead;
            disk.Written = reading.BytesWritten;
            disk.Counted = true;
        }
        if (wall > 0)
        {
            read.Sort((a, b) => a.Number.CompareTo(b.Number));
            readings = read;
            (Usage, Read, Written) = Pick(readings, null);
        }
        stamp = now;
    }

    /// <summary>
    /// What a widget following <paramref name="number"/> draws from one sample's
    /// <paramref name="disks"/>: that disk's own reading, or <b>-1 throughout</b> where it was not
    /// read — a disk unplugged is not a disk at rest, and another disk's figures under its name
    /// would be a lie. Null is every disk together: the busiest one's active time (see the top of
    /// this file) and what all of them read and wrote, added up, as Task Manager's own summary
    /// does; -1 throughout where there was nothing to read.
    /// </summary>
    internal static (float Usage, double Read, double Written) Pick(IReadOnlyList<DiskReading> disks, int? number)
    {
        if (number is { } one)
        {
            foreach (var disk in disks)
            {
                if (disk.Number == one)
                {
                    return (disk.Usage, disk.Read, disk.Written);
                }
            }
            return (-1, -1, -1);
        }
        if (disks.Count == 0)
        {
            return (-1, -1, -1);
        }
        var busiest = -1f;
        var reads = 0.0;
        var writes = 0.0;
        foreach (var disk in disks)
        {
            busiest = MathF.Max(busiest, disk.Usage);
            reads += Math.Max(0, disk.Read);
            writes += Math.Max(0, disk.Written);
        }
        return (busiest, reads, writes);
    }

    /// <summary>
    /// A disk's active time between two readings of its idle counter, 0 to 1, and <b>-1 where
    /// there is no answer</b>: a counter that went backwards (a resume, a reset) rather than an
    /// invented 100 %. <paramref name="wall"/> is the span between the readings in 100 ns.
    /// <para>
    /// Clamped at the top as well, because the counter and the clock are read at slightly
    /// different moments: a disk that was idle throughout can report a shade more idle time than
    /// wall time, which is a negative activity nobody can draw.
    /// </para>
    /// </summary>
    internal static float Active(ulong idleBefore, ulong idleNow, double wall)
    {
        if (idleNow < idleBefore || wall <= 0)
        {
            return -1;
        }
        return (float)Math.Clamp(1 - ((idleNow - idleBefore) / wall), 0, 1);
    }

    /// <summary>
    /// The bytes since the last reading, and <b>nothing</b> where the counter went backwards: what
    /// it carried before the reset is lost to this sample rather than drawn as one enormous spike.
    /// </summary>
    internal static ulong Since(ulong now, ulong before) => now >= before ? now - before : 0;

    /// <summary>Forgets every disk. The next sample finds whatever is there then.</summary>
    public void Close() => disks.Clear();

    public void Dispose() => Close();

    private void Scan()
    {
        // Matched by device path: a disk that was there before keeps its counters, so one
        // appearing or going between two samples adds nothing rather than its whole history.
        var had = disks.ToDictionary(disk => disk.Number);
        disks.Clear();
        for (int number = 0; number < MostDisks; number++)
        {
            if (had.Remove(number, out var before))
            {
                disks.Add(before);
                continue;
            }
            // A device that is not there, or will not answer the query, is no use: the counters are
            // off, or it is not a disk at all. Better to have no graph than a flat line at nothing.
            if (Reading(number) is null)
            {
                continue;
            }
            disks.Add(new Disk(number));
        }
        // The letters on each disk, found again with the disks: a volume mounted or a letter
        // changed is noticed as late as a disk plugged in, and no later.
        var letters = disks.Count > 0 ? VolumesByDisk() : [];
        foreach (var disk in disks)
        {
            disk.Letters = letters.TryGetValue(disk.Number, out var on) ? [.. on] : [];
        }
    }

    /// <summary>
    /// Which disk each lettered volume is on, by disk number: the letters as "C:", in order. Asked
    /// of each fixed or removable volume with no access rights, as the disks are, so it works
    /// unelevated; a volume that spans disks, or will not say, is simply left out — the disk is
    /// still named "Disk 1", only without its letters. About as costly as the probe of the disks.
    /// </summary>
    internal static Dictionary<int, List<string>> VolumesByDisk()
    {
        var found = new Dictionary<int, List<string>>();
        var mask = GetLogicalDrives();
        for (var at = 0; at < 26; at++)
        {
            if ((mask & (1u << at)) == 0)
            {
                continue;
            }
            var letter = (char)('A' + at);
            uint type;
            fixed (char* root = $"{letter}:\\")
            {
                type = GetDriveTypeW(root);
            }
            // DRIVE_REMOVABLE, DRIVE_FIXED: a share, a CD drive or a RAM disk is on no disk here.
            if (type is not (2 or 3))
            {
                continue;
            }
            HANDLE volume;
            fixed (char* path = $@"\\.\{letter}:")
            {
                volume = CreateFileW(path, 0, FILE.FILE_SHARE_READ | FILE.FILE_SHARE_WRITE, null, OPEN.OPEN_EXISTING, 0, HANDLE.NULL);
            }
            if (volume == HANDLE.INVALID_VALUE)
            {
                continue;
            }
            try
            {
                STORAGE_DEVICE_NUMBER number = default;
                uint returned = 0;
                var ok = DeviceIoControl(volume, IOCTL_STORAGE_GET_DEVICE_NUMBER, null, 0, &number, (uint)sizeof(STORAGE_DEVICE_NUMBER), &returned, null);
                if (ok && returned >= (uint)sizeof(STORAGE_DEVICE_NUMBER) && number.DeviceType == FILE_DEVICE_DISK)
                {
                    var disk = (int)number.DeviceNumber;
                    if (!found.TryGetValue(disk, out var on))
                    {
                        found[disk] = on = [];
                    }
                    on.Add($"{letter}:");
                }
            }
            finally
            {
                _ = CloseHandle(volume);
            }
        }
        return found;
    }

    private static HANDLE Open(int number)
    {
        // No access rights at all: a query IOCTL needs none, and asking for GENERIC_READ here
        // would need administrator. Shared every way, because the file systems hold it open.
        fixed (char* path = $@"\\.\PhysicalDrive{number}")
        {
            return CreateFileW(
                path,
                0,
                FILE.FILE_SHARE_READ | FILE.FILE_SHARE_WRITE,
                null,
                OPEN.OPEN_EXISTING,
                0,
                HANDLE.NULL);
        }
    }

    /// <summary>
    /// Disk <paramref name="number"/>'s counters now, or null where it is not there or will not say: opened for the
    /// query and closed after it, so nothing of Karpo's is open on a disk between two samples (§557).
    /// </summary>
    private static DISK_PERFORMANCE? Reading(int number)
    {
        var handle = Open(number);
        if (handle == HANDLE.INVALID_VALUE)
        {
            return null;
        }
        try
        {
            return Query(handle);
        }
        finally
        {
            _ = CloseHandle(handle);
        }
    }

    private static DISK_PERFORMANCE? Query(HANDLE handle)
    {
        DISK_PERFORMANCE reading = default;
        uint returned = 0;
        var ok = DeviceIoControl(
            handle,
            IOCTL_DISK_PERFORMANCE,
            null,
            0,
            &reading,
            (uint)sizeof(DISK_PERFORMANCE),
            &returned,
            null);
        return ok && returned >= (uint)sizeof(DISK_PERFORMANCE) ? reading : null;
    }

    /// <summary>The size <see cref="DISK_PERFORMANCE"/> has in winioctl.h on x64.</summary>
    public const int ReadingSize = 88;

    /// <summary>Null when the declaration below matches winioctl.h (Windows SDK 10.0.26100); otherwise what is wrong.</summary>
    public static string? LayoutProblem()
    {
        DISK_PERFORMANCE reading = default;
        var at = (byte*)&reading;
        (string Name, long Actual, long Expected)[] checks =
        [
            ("DISK_PERFORMANCE", sizeof(DISK_PERFORMANCE), ReadingSize),
            ("BytesRead", (byte*)&reading.BytesRead - at, 0),
            ("BytesWritten", (byte*)&reading.BytesWritten - at, 8),
            ("ReadTime", (byte*)&reading.ReadTime - at, 16),
            ("WriteTime", (byte*)&reading.WriteTime - at, 24),
            ("IdleTime", (byte*)&reading.IdleTime - at, 32),
            ("QueryTime", (byte*)&reading.QueryTime - at, 56),
        ];
        foreach (var (name, actual, expected) in checks)
        {
            if (actual != expected)
            {
                return $"{name} is {actual}, expected {expected}";
            }
        }
        return null;
    }

    /// <summary>
    /// winioctl.h's DISK_PERFORMANCE, which TerraFX does not carry. The times are cumulative, in
    /// 100 ns units; <c>QueryTime</c> is a performance-counter value and is not read here, because
    /// every other sampler measures against <see cref="Stopwatch"/> and one clock is enough.
    /// </summary>
    public struct DISK_PERFORMANCE
    {
        public ulong BytesRead;
        public ulong BytesWritten;
        public ulong ReadTime;
        public ulong WriteTime;
        public ulong IdleTime;
        public uint ReadCount;
        public uint WriteCount;
        public uint QueueDepth;
        public uint SplitCount;
        public ulong QueryTime;
        public uint StorageDeviceNumber;
        public fixed char StorageManagerName[8];
    }

    /// <summary>One physical disk, by its number, and its counters at the last sample; no handle is kept on it (§557).</summary>
    private sealed class Disk(int number)
    {
        public int Number { get; } = number;

        /// <summary>Whether the counters below were ever filled in; the first sample only sets the baseline.</summary>
        public bool Counted { get; set; }

        public ulong Idle { get; set; }

        public ulong Read { get; set; }

        public ulong Written { get; set; }

        /// <summary>The letters of the volumes on it, "C:" and on; found with the disks.</summary>
        public string[] Letters { get; set; } = [];
    }
}

/// <summary>
/// One disk at one sample: its device number (the N of <c>\\.\PhysicalDriveN</c>, Task Manager's
/// "Disk N"), its active time 0 to 1, and the bytes a second it read and wrote — each -1 where it
/// was not known.
/// </summary>
internal readonly record struct DiskReading(int Number, float Usage, double Read, double Written);
