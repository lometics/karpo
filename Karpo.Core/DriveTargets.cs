// Which drive a Drive or Network drive widget points at, and how that survives.
//
// A drive letter is not an identity. A stick that was E: comes back as F: when
// something else took E: first; a share is remapped; a card reader is empty one
// morning and full the next. So a widget keeps the volume's own durable name:
//
//   * a local volume — fixed, removable, optical — is named by its volume GUID
//     path, "\\?\Volume{xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx}\", which Windows
//     gives for a mount point and which stays the same whatever letter the
//     volume is mounted at (GetVolumeNameForVolumeMountPointW);
//   * a network drive is named by the share it is mapped to, "\\server\share",
//     which is the only thing about it that is the same twice
//     (WNetGetConnectionW).
//
// The letter is kept beside it, but only as what the file had before a durable
// name was written (a hand-edited config): a widget whose volume is named and
// not connected is shown broken rather than quietly pointing at whatever took
// its letter. The label is kept so a drive that is not connected still has a
// name to show.
//
// The matching itself (Find) is pure, so it is tested without a disk; the
// listing (Attached) is the Win32 part, used by the drive picker. The dock has its own
// reader (Karpo.Dock\Islands\DriveWatch), which must never wait for a dead
// share on its one thread.

using System.Runtime.InteropServices;

namespace Karpo.Core;

/// <summary>What <c>GetDriveTypeW</c> answers, named. The numbers are Windows'.</summary>
public enum DriveKind
{
    Unknown = 0,
    NoRootDirectory = 1,
    Removable = 2,
    Fixed = 3,
    Remote = 4,
    Optical = 5,
    RamDisk = 6,
}

/// <summary>
/// One drive as it is attached now. <paramref name="Volume"/> is its durable name
/// (see the top of this file), empty when Windows would not give one.
/// </summary>
/// <param name="Letter">"C:", or empty for a volume mounted without a letter.</param>
/// <param name="Label">The volume label, or the name Windows gives a drive without one.</param>
/// <param name="Total">Its size in bytes; 0 when it could not be read (no medium).</param>
/// <param name="Free">What this account can still write, as Explorer shows it.</param>
public readonly record struct DriveVolume(
    string Volume,
    string Letter,
    string Label,
    DriveKind Kind,
    ulong Total,
    ulong Free)
{
    /// <summary>Whether its size was read: false for a card reader without a card, or a share out of reach.</summary>
    public bool SpaceKnown => Total > 0;

    /// <summary>"Local Disk (C:)", as Explorer names a drive.</summary>
    public string Title => DriveTargets.Title(Label, Letter);
}

public static partial class DriveTargets
{
    /// <summary>What a Drive widget may point at: everything but a network drive.</summary>
    public static bool IsLocal(this DriveKind kind) =>
        kind is DriveKind.Fixed or DriveKind.Removable or DriveKind.Optical or DriveKind.RamDisk;

    /// <summary>
    /// What the removable-devices line and face list: media a person can take out. An optical
    /// drive counts — a disc is as removable as a stick, and Explorer ejects both.
    /// </summary>
    public static bool IsRemovable(this DriveKind kind) =>
        kind is DriveKind.Removable or DriveKind.Optical;

    /// <summary>Which kinds of drive a widget showing <paramref name="drives"/> may point at.</summary>
    public static bool Fits(DriveFilter drives, DriveKind kind) => drives switch
    {
        DriveFilter.Network => kind == DriveKind.Remote,
        DriveFilter.Removable => kind.IsRemovable(),
        _ => kind.IsLocal(),
    };

    /// <summary>What Windows calls this kind of drive.</summary>
    public static string TypeName(DriveKind kind) => kind switch
    {
        DriveKind.Removable => S.CoreDriveTypeRemovable,
        DriveKind.Remote => S.CoreDriveTypeNetwork,
        DriveKind.Optical => S.CoreDriveTypeOptical,
        DriveKind.RamDisk => S.CoreDriveTypeRamDisk,
        DriveKind.Fixed => S.CoreDriveTypeFixed,
        _ => S.CoreDriveTypeDrive,
    };

    /// <summary>The name Explorer gives a drive with no volume label of its own.</summary>
    public static string PlainLabel(DriveKind kind) => kind switch
    {
        DriveKind.Removable => S.CoreDriveLabelUsb,
        DriveKind.Remote => S.CoreDriveLabelNetwork,
        DriveKind.Optical => S.CoreDriveLabelCd,
        _ => S.CoreDriveLabelLocal,
    };

    /// <summary>"Local Disk (C:)", or one of the two alone when the other is missing.</summary>
    public static string Title(string label, string letter)
    {
        label = label?.Trim() ?? "";
        letter = Letter(letter);
        if (label.Length == 0)
        {
            return letter.Length > 0 ? letter : S.CoreDriveTypeDrive;
        }
        return letter.Length > 0 ? $"{label} ({letter})" : label;
    }

    /// <summary>
    /// A drive letter as it is kept: one upper-case letter and a colon, from "c", "C:",
    /// "C:\" or "c:/". Empty for anything else, so a junk value never names a drive.
    /// </summary>
    public static string Letter(string? text)
    {
        var trimmed = text?.Trim() ?? "";
        if (trimmed.Length == 0 || !char.IsAsciiLetter(trimmed[0]))
        {
            return "";
        }
        var rest = trimmed[1..].TrimEnd('\\', '/');
        if (rest.Length > 1 || (rest.Length == 1 && rest[0] != ':'))
        {
            return "";
        }
        return $"{char.ToUpperInvariant(trimmed[0])}:";
    }

    /// <summary>0 for A: to 25 for Z:, or -1 when <paramref name="letter"/> names none.</summary>
    public static int IndexOf(string? letter)
    {
        var name = Letter(letter);
        return name.Length == 0 ? -1 : name[0] - 'A';
    }

    /// <summary>"C:\" for the letter, or empty when there is none.</summary>
    public static string Root(string? letter)
    {
        var name = Letter(letter);
        return name.Length == 0 ? "" : name + "\\";
    }

    /// <summary>
    /// The folder a drive's contents are listed from, as a folder pin's target (docs\widgets.md §119):
    /// its letter's root ("C:\"), which is what File Explorer names it by; a share or a volume with
    /// no letter, its durable name; empty for no drive chosen.
    /// </summary>
    public static string ContentsFolder(DriveTargetOptions? target)
    {
        if (target is null)
        {
            return "";
        }
        var root = Root(target.Letter);
        return root.Length > 0 ? root : (target.Volume ?? "").Trim();
    }

    /// <summary>
    /// The drive preset's one question (§119): the drive just chosen for its line,
    /// <paramref name="from"/>, given to each folder's contents line of <paramref name="folder"/> that
    /// has no folder yet — the drive's root (<see cref="ContentsFolder"/>). Asked once, as the preset
    /// is added; the line's folder is its own from then on. How many were given it.
    /// </summary>
    public static int SpreadContents(FolderOptions folder, DriveTargetOptions from)
    {
        var root = ContentsFolder(from);
        if (root.Length == 0)
        {
            return 0;
        }
        var given = 0;
        foreach (var cell in folder.Cells)
        {
            if (PinFolders.Lists(cell?.Island) && cell!.Island!.Pin is { } pin && string.IsNullOrWhiteSpace(pin.Target))
            {
                pin.Target = root;
                given++;
            }
        }
        return given;
    }

    /// <summary>
    /// The drive of <paramref name="target"/> among <paramref name="attached"/> for a widget showing
    /// <paramref name="drives"/>, or null when it is not connected (<see cref="Find(DriveFilter, DriveTargetOptions?, IReadOnlyList{DriveVolume})"/>).
    /// </summary>
    public static DriveVolume? Find(DriveFilter drives, DriveTargetOptions? target, IReadOnlyList<DriveVolume> attached)
    {
        if (target is null || attached is null)
        {
            return null;
        }
        if (target.Volume is { Length: > 0 } volume)
        {
            foreach (var drive in attached)
            {
                if (Same(drive.Volume, volume))
                {
                    return drive;
                }
            }
            return null;
        }
        var letter = Letter(target.Letter);
        if (letter.Length == 0)
        {
            return null;
        }
        foreach (var drive in attached)
        {
            if (Letter(drive.Letter) == letter && Fits(drives, drive.Kind))
            {
                return drive;
            }
        }
        return null;
    }

    /// <summary>
    /// What the widget shows when the drive it names isn't connected: the name the file
    /// remembers, so a broken widget still says which drive it is waiting for.
    /// </summary>
    public static string MissingTitle(DriveTargetOptions? target) =>
        target is null || !target.IsSet ? S.CoreDriveNoneChosen : Title(target.Label, target.Letter);

    /// <summary>The one line that says why a widget showing <paramref name="drives"/> is drawn broken.</summary>
    public static string MissingLine(DriveFilter drives, DriveTargetOptions? target)
    {
        if (target is null || !target.IsSet)
        {
            return S.CoreDriveNoneChosenLine;
        }
        return drives == DriveFilter.Network
            ? S.CoreDriveNetworkNotConnected
            : S.CoreDriveNotConnected;
    }

    /// <summary>
    /// What a drive's cell calls it in the room it has: its whole name with its letter
    /// ("Local Disk (C:)"), else its name alone ("Local Disk"), else its letter ("C:"), which
    /// always fits. <paramref name="width"/> measures a string in the same units as
    /// <paramref name="room"/>.
    ///
    /// The bar's cell and the settings preview both call this, so the two say the same thing
    /// about the same drive instead of one ellipsising where the other abbreviates.
    /// </summary>
    public static string CellName(string label, string letter, float room, Func<string, float> width)
    {
        letter = Letter(letter);
        label = label?.Trim() ?? "";
        var fallback = letter.Length > 0 ? letter : label;
        foreach (var wanted in (string[])[Title(label, letter), label, letter])
        {
            if (wanted.Length > 0 && width(wanted) <= room)
            {
                return wanted;
            }
        }
        return fallback;
    }

    /// <summary>Two durable names, or two letters, as the same thing. Windows ignores case in both.</summary>
    public static bool Same(string? left, string? right) =>
        string.Equals(left?.Trim() ?? "", right?.Trim() ?? "", StringComparison.OrdinalIgnoreCase);

    /// <summary>Fills <paramref name="target"/> in from a drive that is attached now.</summary>
    public static void PointAt(DriveTargetOptions target, in DriveVolume drive)
    {
        target.Volume = drive.Volume;
        target.Letter = Letter(drive.Letter);
        target.Label = drive.Label;
    }

    // --- Reading what is attached ----------------------------------------------

    /// <summary>
    /// Where <see cref="Attached"/> gets its answer. The real one asks Windows; a test hands in
    /// its own, so nothing here depends on the drives of the PC it runs on.
    /// </summary>
    internal static Func<bool, bool, IReadOnlyList<DriveVolume>> Reader { get; set; } = Default;

    /// <summary>Windows' answer, or none while the config is isolated (tests, the dock's smoke run).</summary>
    private static IReadOnlyList<DriveVolume> Default(bool network, bool space) =>
        Config.Isolated is null ? Read(network, space) : [];

    /// <summary>
    /// Every drive attached now, letter by letter.
    /// </summary>
    /// <param name="network">
    /// Include mapped network drives. Their names come from the redirector, which answers at once
    /// even for a share that is down.
    /// </param>
    /// <param name="space">
    /// Also read each drive's size. This is the part that waits: a share that has gone away makes
    /// the call wait for the network to time out, tens of seconds. Ask for it only where something
    /// can afford to wait — never on a thread a window is drawn from.
    /// </param>
    public static IReadOnlyList<DriveVolume> Attached(bool network = false, bool space = true) =>
        Reader(network, space);

    private static IReadOnlyList<DriveVolume> Read(bool network, bool space)
    {
        var drives = new List<DriveVolume>(8);
        uint mask;
        try
        {
            mask = GetLogicalDrives();
        }
        catch (DllNotFoundException)
        {
            return drives;
        }
        // No "insert a disk" box for an empty card reader: a failed read is enough.
        _ = SetThreadErrorMode(SemFailCriticalErrors | SemNoOpenFileErrorBox, out var mode);
        try
        {
            for (var index = 0; index < 26; index++)
            {
                if ((mask & (1u << index)) == 0)
                {
                    continue;
                }
                var letter = $"{(char)('A' + index)}:";
                var root = letter + "\\";
                var kind = (DriveKind)GetDriveTypeW(root);
                if ((kind == DriveKind.Remote && !network) || kind is DriveKind.Unknown or DriveKind.NoRootDirectory)
                {
                    continue;
                }
                var (total, free) = space ? Space(root) : (0ul, 0ul);
                var share = kind == DriveKind.Remote ? Share(letter) : "";
                drives.Add(new DriveVolume(
                    kind == DriveKind.Remote ? share : VolumeName(root),
                    letter,
                    // A share's own name is free; a volume label costs a read of the volume,
                    // which is what waits on a drive that isn't answering.
                    kind == DriveKind.Remote ? ShareLabel(share) : space ? LabelOf(root, kind) : PlainLabel(kind),
                    kind,
                    total,
                    free));
            }
        }
        finally
        {
            _ = SetThreadErrorMode(mode, out _);
        }
        return drives;
    }

    /// <summary>"share (\\server)" for \\server\share, as Explorer names a mapped drive.</summary>
    private static string ShareLabel(string share)
    {
        var path = share.TrimEnd('\\');
        var cut = path.LastIndexOf('\\');
        return cut > 2 && cut < path.Length - 1 ? $"{path[(cut + 1)..]} ({path[..cut]})" : path.Length > 0 ? path : PlainLabel(DriveKind.Remote);
    }

    /// <summary>The volume GUID path of the volume mounted at <paramref name="root"/>; empty when Windows gives none.</summary>
    private static string VolumeName(string root)
    {
        var name = new char[64];
        return GetVolumeNameForVolumeMountPointW(root, name, (uint)name.Length)
            ? Text(name)
            : "";
    }

    /// <summary>The share "\\server\share" a network drive is mapped to; empty when it cannot be read.</summary>
    private static string Share(string letter)
    {
        const uint ConnectionUnavailable = 1201;
        var path = new char[1024];
        var length = (uint)path.Length;
        var status = WNetGetConnectionW(letter, path, ref length);
        // A remembered connection that isn't connected right now still knows its path.
        return status == 0 || status == ConnectionUnavailable ? Text(path).TrimEnd('\\') : "";
    }

    private static string LabelOf(string root, DriveKind kind)
    {
        var name = new char[261];
        return GetVolumeInformationW(root, name, (uint)name.Length, out _, out _, out _, null, 0) && Text(name) is { Length: > 0 } label
            ? label
            : PlainLabel(kind);
    }

    private static (ulong Total, ulong Free) Space(string root) =>
        GetDiskFreeSpaceExW(root, out var available, out var total, out _) && total > 0
            ? (total, available)
            : (0ul, 0ul);

    private static string Text(char[] buffer)
    {
        var end = Array.IndexOf(buffer, '\0');
        return new string(buffer, 0, end < 0 ? buffer.Length : end);
    }

    private const uint SemFailCriticalErrors = 0x0001;
    private const uint SemNoOpenFileErrorBox = 0x8000;

    [LibraryImport("kernel32.dll")]
    private static partial uint GetLogicalDrives();

    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial uint GetDriveTypeW(string root);

    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetVolumeNameForVolumeMountPointW(string mountPoint, [Out] char[] name, uint length);

    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetVolumeInformationW(
        string root,
        [Out] char[] name,
        uint nameLength,
        out uint serial,
        out uint componentLength,
        out uint flags,
        char[]? fileSystem,
        uint fileSystemLength);

    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetDiskFreeSpaceExW(string root, out ulong available, out ulong total, out ulong free);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetThreadErrorMode(uint mode, out uint previous);

    [LibraryImport("mpr.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial uint WNetGetConnectionW(string local, [Out] char[] remote, ref uint length);
}
