// The drives the bar knows about, listed once for every widget that wants them.
//
// A Drive or Network drive widget shows the one volume it was given, and the
// removable-devices line and face list what is plugged in; all of them pick from this
// one list, so ten drive widgets cost one reading of the drives, not ten.
//
// Each drive carries its durable name as well as its letter (Karpo.Core's
// DriveTargets: a volume GUID path for a local volume, the share for a mapped
// drive), which is what a widget is matched against — a stick that comes back
// as F: is still the same stick.
//
// A network drive is asked for on a pool thread and never waited for: a share
// that has gone away makes GetDiskFreeSpaceEx wait for the network to time out,
// tens of seconds, and the bar has one thread. Until its first answer the drive
// is pending — neither there nor missing — and the widgets that asked are told
// when an answer comes that changes whether it is there. A connection Windows
// remembers but has not made (WNetGetConnection's ERROR_CONNECTION_UNAVAIL,
// Explorer's red X) is not asked of the network at all: reading it would try to
// reconnect, which Explorer leaves to the person opening it.

using Karpo.Core;
using Karpo.Dock.Native;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

/// <summary>One drive as the last list saw it, or one a widget names and cannot find.</summary>
internal sealed class Drive
{
    /// <summary>0 for A: to 25 for Z:, or -1 for a drive with no letter (one that isn't connected).</summary>
    public required int Letter { get; init; }

    /// <summary>GetDriveTypeW's answer.</summary>
    public required DriveKind Kind { get; init; }

    /// <summary>
    /// Its durable name (<see cref="DriveTargets"/>): a volume GUID path, or a share's UNC path.
    /// Empty where Windows gives none.
    /// </summary>
    public string Volume { get; init; } = "";

    public string Label { get; set; } = "";

    public ulong Total { get; init; }

    public ulong Free { get; init; }

    /// <summary>
    /// A drive a widget names that is not connected: the widget shows it broken and keeps its
    /// size, and its flyout says so instead of reading anything.
    /// </summary>
    public bool Missing { get; init; }

    /// <summary>
    /// Not known yet: a network drive whose first answer is still out on the pool. Listed as a
    /// placeholder with only its letter, and handed to a widget as <see cref="Missing"/> and this
    /// together, which draws it neither as there nor as disconnected until the answer comes.
    /// </summary>
    public bool Pending { get; init; }

    /// <summary>"C:", or empty for a drive with no letter.</summary>
    public string Name => (uint)Letter < 26 ? DriveWatch.Names[Letter] : "";

    /// <summary>"C:\", or empty for a drive with no letter.</summary>
    public string Root => (uint)Letter < 26 ? DriveWatch.Roots[Letter] : "";

    /// <summary>"Local Disk (C:)", as Explorer names a drive.</summary>
    public string Title => DriveTargets.Title(Label, Name);

    /// <summary>0 to 1.</summary>
    public float Used => Total == 0 ? 0 : (float)((double)(Total - Math.Min(Free, Total)) / Total);

    /// <summary>
    /// What a widget draws for a drive it names and cannot find; <paramref name="pending"/> while a
    /// network drive's first answer is still out, so it may yet be found.
    /// </summary>
    public static Drive Absent(DriveTargetOptions? target, bool pending = false) => new()
    {
        Letter = DriveTargets.IndexOf(target?.Letter),
        Kind = DriveKind.Unknown,
        Volume = target?.Volume ?? "",
        Label = target?.Label ?? "",
        Missing = true,
        Pending = pending,
    };
}

/// <summary>
/// The drives every drive widget picks from, listed once for all of them: a list younger than a
/// second is handed out again. Used on the dock's thread.
/// </summary>
internal static unsafe class DriveWatch
{
    /// <summary>WNetGetConnectionW: a connection Windows remembers but has not made — Explorer's red X.</summary>
    internal const uint ConnectionUnavailable = 1201;

    /// <summary>"A:\" to "Z:\", made once.</summary>
    public static readonly string[] Roots = Enumerable.Range(0, 26).Select(i => $"{(char)('A' + i)}:\\").ToArray();

    /// <summary>"A:" to "Z:", made once.</summary>
    public static readonly string[] Names = Enumerable.Range(0, 26).Select(i => $"{(char)('A' + i)}:").ToArray();

    private static readonly Task<DriveReading>?[] probes = new Task<DriveReading>?[26];
    private static readonly DriveReading?[] answers = new DriveReading?[26];
    private static List<Drive> listed = [];
    private static long listedAt;
    private static bool listedRemote;
    private static bool listedLabels;

    // Set from the pool when a network drive's answer came in, so the next Read lists again
    // whatever the age of the last list.
    private static int answered;

    // Who asked while an answer was out, told from the pool when one comes that changes whether
    // a drive is there (each a widget's Wake, which posts to the dock).
    private static readonly Lock Gate = new();
    private static readonly HashSet<Action> waiting = [];

    /// <summary>
    /// Every drive that can be read: local ones always, network drives when
    /// <paramref name="remote"/>. With <paramref name="labels"/>, the volume labels are read again too.
    /// </summary>
    /// <summary>
    /// Where <see cref="Read"/> gets its answer. Windows', unless a test hands in its own: an
    /// offscreen picture of a drive widget must not depend on what is plugged into the PC it is
    /// drawn on, and neither must a check that the widget keeps its size.
    /// </summary>
    internal static Func<bool, bool, IReadOnlyList<Drive>>? Source { get; set; }

    public static IReadOnlyList<Drive> Read(bool labels, bool remote)
    {
        if (Source is { } source)
        {
            return source(labels, remote);
        }
        var now = Environment.TickCount64;
        var fresh = Interlocked.Exchange(ref answered, 0) != 0;
        if (!fresh && listedAt != 0 && now - listedAt < 1000 && (listedRemote || !remote) && (listedLabels || !labels))
        {
            return listed;
        }
        listed = List(labels, remote);
        (listedAt, listedRemote, listedLabels) = (now, remote, labels);
        return listed;
    }

    /// <summary>Forgets the last list, so the next <see cref="Read"/> asks Windows again (a device came or went).</summary>
    public static void Forget()
    {
        listedAt = 0;
        Changes++;
    }

    /// <summary>
    /// How many times the list has been forgotten (<see cref="Forget"/>): a widget that keeps what it
    /// read compares this with the count it read at, and reads again only when a device came or went
    /// since — not on every refresh some other widget of its folder asked for.
    /// </summary>
    public static int Changes { get; private set; }

    /// <summary>
    /// Where <see cref="Eject"/> and <see cref="Open"/> go: the shell's own verb on the drive, unless
    /// a test hands in its own — the drive's root and the verb, null for the shell's default. A card
    /// or a line of the removable devices ejects and opens through here, so nothing a test or an
    /// offscreen picture does can reach a real volume, as nothing it draws reads one (<see cref="Source"/>).
    /// </summary>
    internal static Action<string, string?>? Verb { get; set; }

    /// <summary>
    /// Ejects the drive at <paramref name="root"/> ("E:\") with the shell's own verb, which is what
    /// Explorer's "Eject" runs (Launch.Eject, §557): Windows flushes the volume, tells whatever still
    /// has it open, and shows its own notice either way, so nothing here claims an outcome of its own.
    /// </summary>
    public static void Eject(string root) => Run(root, MenuVerbs.Eject, () => Launch.Eject(root));

    /// <summary>Opens the drive at <paramref name="root"/> the way Explorer would: in a File Explorer window.</summary>
    public static void Open(string root) => Run(root, null, () => _ = Launch.Open(root));

    /// <summary>
    /// folders: ends the mapped network drive at <paramref name="root"/> ("Z:\"), as Explorer's
    /// "Disconnect" does — the letter goes, and does not come back at the next sign-in. Nothing is
    /// forced: a drive with files open on it stays connected, as Windows keeps it when nobody says
    /// to close them, and the widget goes on showing it.
    /// </summary>
    public static void Disconnect(string root) => Run(root, MenuVerbs.Disconnect, () =>
    {
        fixed (char* name = root.TrimEnd('\\'))
        {
            _ = WNetCancelConnection2W(name, CONNECT.CONNECT_UPDATE_PROFILE, BOOL.FALSE);
        }
    });

    /// <summary>What a drive's verb does: nothing without a root, the test's stand-in where it has one, else <paramref name="windows"/>.</summary>
    private static void Run(string root, string? verb, Action windows)
    {
        if (root.Length == 0)
        {
            return;
        }
        if (Verb is { } stand)
        {
            stand(root, verb);
            return;
        }
        windows();
    }

    /// <summary>
    /// The drive <paramref name="target"/> names among those connected now, or one marked
    /// <see cref="Drive.Missing"/> — and <see cref="Drive.Pending"/> too while a network drive's
    /// first answer is out. Never null: a drive widget is always on the bar. <paramref name="ready"/>
    /// is called, from the pool, when a network drive's answer comes that changes whether it is there.
    /// </summary>
    public static Drive For(DriveFilter drives, DriveTargetOptions? target, bool labels, Action? ready = null)
    {
        var remote = drives == DriveFilter.Network;
        if (remote && ready is not null && Source is null)
        {
            lock (Gate)
            {
                _ = waiting.Add(ready);
            }
        }
        return Pick(drives, target, Read(labels, remote));
    }

    /// <summary>
    /// The drive <paramref name="target"/> names in <paramref name="listed"/>: the one it matches,
    /// else missing — and pending where a network drive in the list has not answered yet, as the
    /// one it names may be that one. Pure, so the rule is tested without a share.
    /// </summary>
    internal static Drive Pick(DriveFilter drives, DriveTargetOptions? target, IReadOnlyList<Drive> listed)
    {
        var pending = false;
        foreach (var drive in listed)
        {
            if (Matches(drives, target, drive))
            {
                return drive;
            }
            pending |= drive.Pending && drives == DriveFilter.Network;
        }
        return Drive.Absent(target, pending && target is { IsSet: true });
    }

    /// <summary>
    /// Whether <paramref name="drive"/> is the one <paramref name="target"/> names: by its durable
    /// name, or — only for a file that has none — by its letter, and then only for a drive of a
    /// kind the widget may show. Pure, so the rule is tested without a disk.
    /// </summary>
    internal static bool Matches(DriveFilter drives, DriveTargetOptions? target, Drive drive)
    {
        if (target is null || drive.Missing || drive.Pending)
        {
            return false;
        }
        if (target.Volume.Length > 0)
        {
            return DriveTargets.Same(drive.Volume, target.Volume);
        }
        return DriveTargets.Letter(target.Letter) is { Length: > 0 } letter
            && letter == drive.Name
            && DriveTargets.Fits(drives, drive.Kind);
    }

    private static List<Drive> List(bool labels, bool remote)
    {
        var mask = GetLogicalDrives();
        var next = new List<Drive>(Math.Max(listed.Count, 4));
        uint mode;
        // No "insert a disk" box for an empty card reader; a failed read is enough.
        _ = SetThreadErrorMode(SEM_FAILCRITICALERRORS | SEM_NOOPENFILEERRORBOX, &mode);
        try
        {
            for (int letter = 0; letter < 26; letter++)
            {
                if ((mask & (1u << letter)) == 0)
                {
                    // Gone; a drive mapped to this letter later starts fresh.
                    answers[letter] = null;
                    continue;
                }
                uint raw;
                fixed (char* root = Roots[letter])
                {
                    raw = GetDriveTypeW(root);
                }
                var kind = (DriveKind)raw;
                if (kind == DriveKind.Remote)
                {
                    if (!remote)
                    {
                        continue;
                    }
                    var answer = Remote(letter);
                    if (answer is { Ok: true } there)
                    {
                        next.Add(new Drive
                        {
                            Letter = letter,
                            Kind = kind,
                            Volume = there.Share,
                            Label = there.Label,
                            Total = there.Total,
                            Free = there.Free,
                        });
                    }
                    else if (answer is null)
                    {
                        // Not answered yet: a placeholder, which no widget matches, so one that
                        // names a share is drawn pending rather than disconnected (Pick).
                        next.Add(new Drive { Letter = letter, Kind = kind, Pending = true });
                    }
                    continue;
                }
                if (!kind.IsLocal())
                {
                    continue;
                }
                var space = Space(letter);
                var known = Find(letter);
                var label = known is not null && known.Kind == kind && !labels ? known.Label : Label(letter, kind);
                next.Add(new Drive
                {
                    Letter = letter,
                    Kind = kind,
                    // The durable name is read even for a drive with no medium in it: the
                    // reader itself keeps its identity, so the widget stays pointed at it.
                    Volume = VolumeName(letter),
                    Label = label,
                    Total = space?.Total ?? 0,
                    Free = space?.Free ?? 0,
                });
            }
        }
        finally
        {
            _ = SetThreadErrorMode(mode, null);
        }
        return next;
    }

    private static Drive? Find(int letter)
    {
        foreach (var drive in listed)
        {
            if (drive.Letter == letter)
            {
                return drive;
            }
        }
        return null;
    }

    /// <summary>The volume GUID path mounted at the letter, which outlives the letter itself.</summary>
    private static string VolumeName(int letter)
    {
        var name = stackalloc char[64];
        fixed (char* root = Roots[letter])
        {
            return GetVolumeNameForVolumeMountPointW(root, name, 64) && name[0] != '\0' ? new string(name) : "";
        }
    }

    /// <summary>
    /// The newest answer for a network drive, read on a pool thread and never waited for. While a
    /// question is still out, nothing new is asked and the last answer stands, which is none for a
    /// drive not yet read: it is pending, and whoever asked is told when the answer comes.
    /// </summary>
    private static DriveReading? Remote(int letter)
    {
        Collect(letter);
        if (probes[letter] is null)
        {
            var probe = Task.Factory.StartNew(
                static state => ReadRemote((int)state!),
                letter,
                CancellationToken.None,
                TaskCreationOptions.DenyChildAttach,
                TaskScheduler.Default);
            probes[letter] = probe;
            // Told once the probe is complete, so the read the news brings collects it.
            _ = probe.ContinueWith(
                static (done, state) =>
                {
                    try
                    {
                        Interlocked.Exchange(ref answered, 1);
                        if (state is not DriveReading was || was.Ok != done.Result.Ok)
                        {
                            Tell();
                        }
                    }
                    catch (Exception error) when (error is not OutOfMemoryException)
                    {
                        // Nobody to tell is no reason to end the bar; the next tick reads again.
                    }
                },
                answers[letter],
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
        return answers[letter];
    }

    /// <summary>Tells everyone waiting that a network drive came, went or answered for the first time.</summary>
    private static void Tell()
    {
        Action[] told;
        lock (Gate)
        {
            told = [.. waiting];
            waiting.Clear();
        }
        foreach (var ready in told)
        {
            try
            {
                ready();
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                // A widget gone in the meantime has nothing to redraw.
            }
        }
    }

    private static void Collect(int letter)
    {
        if (probes[letter] is { IsCompleted: true } probe)
        {
            // ReadRemote does not throw, so the result is always there.
            answers[letter] = probe.Result;
            probes[letter] = null;
            probe.Dispose();
        }
    }

    private static DriveReading ReadRemote(int letter)
    {
        uint mode;
        _ = SetThreadErrorMode(SEM_FAILCRITICALERRORS | SEM_NOOPENFILEERRORBOX, &mode);
        try
        {
            var (status, share) = Connection(letter);
            return Reading(status, share, () => Space(letter), () => Label(letter, DriveKind.Remote));
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            return default;
        }
        finally
        {
            _ = SetThreadErrorMode(mode, null);
        }
    }

    /// <summary>
    /// A network drive's answer from what WNetGetConnection said of it (<paramref name="status"/>,
    /// <paramref name="share"/>): a connection Windows remembers but has not made is not there, and
    /// nothing is asked of its share — a read would try to reconnect, which Explorer leaves to the
    /// person opening the drive; any other is there when its space can be read. Pure, so the rule
    /// is tested without a network.
    /// </summary>
    internal static DriveReading Reading(uint status, string share, Func<(ulong Total, ulong Free)?> space, Func<string> label)
    {
        if (status == ConnectionUnavailable)
        {
            return new DriveReading(false, 0, 0, "", share);
        }
        return space() is { } read
            ? new DriveReading(true, read.Total, read.Free, label(), share)
            : new DriveReading(false, 0, 0, "", share);
    }

    private static (ulong Total, ulong Free)? Space(int letter)
    {
        ULARGE_INTEGER available;
        ULARGE_INTEGER total;
        ULARGE_INTEGER free;
        fixed (char* root = Roots[letter])
        {
            // What this user can still write, as Explorer shows it; the same as the free bytes
            // unless a quota applies, and then the total is the quota too.
            return GetDiskFreeSpaceExW(root, &available, &total, &free) && total.QuadPart > 0
                ? (total.QuadPart, available.QuadPart)
                : null;
        }
    }

    /// <summary>The name Explorer shows: the volume label, or what it calls a drive without one.</summary>
    public static string Label(int letter, DriveKind kind)
    {
        if (kind == DriveKind.Remote)
        {
            var share = Share(letter);
            return share.Length > 0 ? ShareLabel(share) : DriveTargets.PlainLabel(kind);
        }
        var name = stackalloc char[MAX.MAX_PATH + 1];
        fixed (char* root = Roots[letter])
        {
            if (GetVolumeInformationW(root, name, MAX.MAX_PATH + 1, null, null, null, null, 0) && name[0] != '\0')
            {
                return new string(name);
            }
        }
        return DriveTargets.PlainLabel(kind);
    }

    /// <summary>The share "\\server\share" a drive is mapped to; empty when it cannot be read.</summary>
    private static string Share(int letter) => Connection(letter).Share;

    /// <summary>
    /// What WNetGetConnection says of the letter — the redirector's own record, not a question to
    /// the network — and the share it is mapped to, empty where it has none.
    /// </summary>
    private static (uint Status, string Share) Connection(int letter)
    {
        var path = stackalloc char[1024];
        uint length = 1024;
        uint status;
        fixed (char* local = Names[letter])
        {
            status = WNetGetConnectionW(local, path, &length);
        }
        // A remembered connection that is not connected right now still has its path.
        if (status != 0 && status != ConnectionUnavailable)
        {
            return (status, "");
        }
        return (status, new string(path).TrimEnd('\\'));
    }

    /// <summary>"share (\\server)" for a drive mapped to \\server\share, as Explorer names it.</summary>
    internal static string ShareLabel(string share)
    {
        var cut = share.LastIndexOf('\\');
        return cut > 2 && cut < share.Length - 1 ? $"{share[(cut + 1)..]} ({share[..cut]})" : share;
    }
}

/// <summary>A network drive's answer, read on the thread pool.</summary>
internal readonly record struct DriveReading(bool Ok, ulong Total, ulong Free, string Label, string Share);
