// §557: what Karpo holds open on a drive, let go of when the drive is asked to go.
//
// A folder's contents line follows its folder (FolderWatch): a FileSystemWatcher, which keeps a handle open on the
// folder for as long as the line shows it. A handle open on a stick is what makes Windows answer an Eject — Explorer's,
// the tray's "Safely Remove Hardware", Karpo's own — with "This device is currently in use". Explorer and every
// well-behaved program let go when Windows asks: each volume it follows something on is registered with the dock's
// window (RegisterDeviceNotification, DBT_DEVTYP_HANDLE), and before the device goes — and before the volume is locked
// or dismounted, which an eject of a card does — Windows asks (DBT_DEVICEQUERYREMOVE, GUID_IO_VOLUME_LOCK,
// GUID_IO_VOLUME_DISMOUNT). Then every watch on that volume lets go of its watcher, and the handle the registration was
// made with is closed. A removal that fails (another program said no; the lock or the dismount failed, or ended) takes
// them back; one that went through leaves them let go, and the device arriving again (DBT_DEVICEARRIVAL) takes back
// those whose folder is there again — the line follows its folder without waiting for the minute's tick.
//
// The rules are this class's and pure; the handles are the notices' (IDriveNotices), which the dock hands in
// (WindowNotices) and a test fakes, so nothing a test does registers for or closes a real volume. A test's own folder
// source has no holds at all (FolderSource.Holds): only Windows' own folders are followed here.

using System.Runtime.InteropServices;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock.Drives;

/// <summary>What a device change asks of the holds (<see cref="DriveHolds.Meaning"/>).</summary>
internal enum DriveNotice
{
    /// <summary>Nothing of the holds'.</summary>
    None,

    /// <summary>The volume is about to be removed, locked or dismounted: let go of everything on it.</summary>
    LetGo,

    /// <summary>It was not, or no longer is (the removal failed; the lock failed or ended): take it all back.</summary>
    TakeBack,

    /// <summary>It went: the registration ends, and what was on it stays let go.</summary>
    Gone,

    /// <summary>A device arrived: what was let go of on a volume that is there again is taken back.</summary>
    Arrived,
}

/// <summary>
/// The handle a volume is followed by and its registration with the dock's window: the real ones are the dock's
/// (<see cref="WindowNotices"/>), a test's are made up.
/// </summary>
internal interface IDriveNotices
{
    /// <summary>A handle opened on the volume at <paramref name="root"/> and registered for its removal; the registration, or 0 where it cannot be.</summary>
    nint Follow(string root);

    /// <summary>The handle of <paramref name="notice"/> closed, the registration kept: the removal's outcome is still heard.</summary>
    void LetGo(nint notice);

    /// <summary>The registration <paramref name="notice"/> ended, its handle closed where it is still open.</summary>
    void End(nint notice);
}

/// <summary>The folders Karpo follows, by the volume each is on, and what Windows' questions about those volumes do to them.</summary>
internal sealed class DriveHolds
{
    // dbt.h.
    internal const uint DBT_DEVICEARRIVAL = 0x8000;
    internal const uint DBT_DEVICEQUERYREMOVE = 0x8001;
    internal const uint DBT_DEVICEQUERYREMOVEFAILED = 0x8002;
    internal const uint DBT_DEVICEREMOVEPENDING = 0x8003;
    internal const uint DBT_DEVICEREMOVECOMPLETE = 0x8004;
    internal const uint DBT_CUSTOMEVENT = 0x8006;
    internal const uint DBT_DEVTYP_HANDLE = 6;

    // ioevent.h: what a file system says of a volume to those registered for a handle on it.
    internal static readonly Guid VolumeLock = new(0x50708874, 0xc9af, 0x11d1, 0x8f, 0xef, 0x00, 0xa0, 0xc9, 0xa0, 0x6d, 0x32);
    internal static readonly Guid VolumeLockFailed = new(0xae2eed10, 0x0ba8, 0x11d2, 0x8f, 0xfb, 0x00, 0xa0, 0xc9, 0xa0, 0x6d, 0x32);
    internal static readonly Guid VolumeUnlock = new(0x9a8c3d68, 0xd0cb, 0x11d1, 0x8f, 0xef, 0x00, 0xa0, 0xc9, 0xa0, 0x6d, 0x32);
    internal static readonly Guid VolumeDismount = new(0xd16a55e8, 0x1059, 0x11d2, 0x8f, 0xfd, 0x00, 0xa0, 0xc9, 0xa0, 0x6d, 0x32);
    internal static readonly Guid VolumeDismountFailed = new(0xe3c5b178, 0x105d, 0x11d2, 0x8f, 0xfd, 0x00, 0xa0, 0xc9, 0xa0, 0x6d, 0x32);
    internal static readonly Guid VolumeMount = new(0xb5804878, 0x1a96, 0x11d2, 0x8f, 0xfd, 0x00, 0xa0, 0xc9, 0xa0, 0x6d, 0x32);

    /// <summary>Windows' own folders' holds (FolderSource.Windows).</summary>
    public static DriveHolds Shared { get; } = new();

    private readonly Lock gate = new();
    private readonly Dictionary<string, Hold> holds = new(StringComparer.OrdinalIgnoreCase);
    private IDriveNotices? notices;

    /// <summary>
    /// Where the volumes are registered: the dock's window while there is a dock, else nowhere — and then what is
    /// held is only kept count of. Set, every volume held is registered; taken away, every registration ends.
    /// </summary>
    public IDriveNotices? Notices
    {
        get => notices;
        set
        {
            lock (gate)
            {
                if (ReferenceEquals(value, notices))
                {
                    return;
                }
                foreach (var hold in holds.Values)
                {
                    End(hold);
                }
                notices = value;
                foreach (var (root, hold) in holds)
                {
                    if (!hold.LetGo)
                    {
                        Follow(root, hold);
                    }
                }
            }
        }
    }

    /// <summary>
    /// The volume <paramref name="path"/> is on, by its root: "E:\" for anything on E:, empty for what is on no lettered
    /// drive (a share, which is never watched; a relative path). Pure.
    /// </summary>
    internal static string RootOf(string path) =>
        path.Length >= 2 && char.IsAsciiLetter(path[0]) && path[1] == ':' && (path.Length == 2 || path[2] is '\\' or '/')
            ? $"{char.ToUpperInvariant(path[0])}:\\"
            : "";

    /// <summary>
    /// What a WM_DEVICECHANGE's <paramref name="change"/> (its wParam) asks of the holds; <paramref name="custom"/> is the
    /// event a file system said (a DBT_CUSTOMEVENT's), empty for any other. Pure.
    /// </summary>
    internal static DriveNotice Meaning(uint change, Guid custom) => change switch
    {
        DBT_DEVICEQUERYREMOVE => DriveNotice.LetGo,
        DBT_DEVICEQUERYREMOVEFAILED => DriveNotice.TakeBack,
        DBT_DEVICEREMOVEPENDING or DBT_DEVICEREMOVECOMPLETE => DriveNotice.Gone,
        DBT_DEVICEARRIVAL => DriveNotice.Arrived,
        DBT_CUSTOMEVENT when custom == VolumeLock || custom == VolumeDismount => DriveNotice.LetGo,
        DBT_CUSTOMEVENT when custom == VolumeLockFailed || custom == VolumeUnlock || custom == VolumeDismountFailed || custom == VolumeMount => DriveNotice.TakeBack,
        _ => DriveNotice.None,
    };

    /// <summary>
    /// A WM_DEVICECHANGE as the dock's window heard it: what it asks (<see cref="Meaning"/>) and, for one about a handle,
    /// the registration it is about (0 for any other).
    /// </summary>
    internal static unsafe (DriveNotice What, nint Notice) Read(WPARAM wParam, LPARAM lParam)
    {
        var change = (uint)(nuint)wParam;
        if (change == DBT_DEVICEARRIVAL)
        {
            return (DriveNotice.Arrived, 0);
        }
        var header = (BroadcastHandle*)(nint)lParam;
        if (header == null || header->DeviceType != DBT_DEVTYP_HANDLE || header->Size < Offset(&header->EventGuid, header))
        {
            return (DriveNotice.None, 0);
        }
        var custom = change == DBT_CUSTOMEVENT && header->Size >= Offset(&header->NameOffset, header) ? header->EventGuid : Guid.Empty;
        return (Meaning(change, custom), header->Notify);
    }

    private static unsafe uint Offset(void* field, BroadcastHandle* header) => (uint)((byte*)field - (byte*)header);

    /// <summary>
    /// Does what <paramref name="what"/> asks of the volume registered as <paramref name="notice"/> (any volume, for an
    /// arrival). True where it was about something held: a question to let go is then answered yes.
    /// </summary>
    public bool Heard(DriveNotice what, nint notice)
    {
        if (what == DriveNotice.None)
        {
            return false;
        }
        lock (gate)
        {
            if (what == DriveNotice.Arrived)
            {
                var any = false;
                foreach (var (root, hold) in holds)
                {
                    if (hold.LetGo && hold.Watches.Count > 0 && TakeBack(root, hold))
                    {
                        any = true;
                    }
                }
                return any;
            }
            if (notice == 0 || Find(notice) is not { } found)
            {
                return false;
            }
            var (at, held) = found;
            switch (what)
            {
                case DriveNotice.LetGo:
                    LetGo(held);
                    break;
                // Only what was let go of: a mount or an unlock of a volume held all along changes nothing.
                case DriveNotice.TakeBack when held.LetGo:
                    _ = TakeBack(at, held);
                    break;
                case DriveNotice.Gone:
                    // Pulled out without being asked, too: its watches are let go of, for its arrival to take back.
                    LetGo(held);
                    End(held);
                    break;
            }
            return true;
        }
    }

    /// <summary>A watch started on a folder: its volume is followed from now on, registered on the first.</summary>
    public void Add(FolderWatch watch)
    {
        var root = RootOf(watch.Path);
        if (root.Length == 0)
        {
            return;
        }
        lock (gate)
        {
            if (!holds.TryGetValue(root, out var hold))
            {
                hold = new Hold();
                holds[root] = hold;
            }
            hold.Watches.Add(watch);
            if (watch.Watching)
            {
                // Its volume is there: followed, and what waited on it taken back with it.
                if (hold.LetGo)
                {
                    _ = TakeBack(root, hold);
                }
                else if (hold.Notice == 0)
                {
                    Follow(root, hold);
                }
            }
            else if (hold.Watches.Count == 1)
            {
                // A watch that could not start (its drive is not plugged in) waits, let go, for the drive to arrive.
                hold.LetGo = true;
            }
        }
    }

    /// <summary>A watch ended: its volume's registration with it, where it was the last on it.</summary>
    public void Remove(FolderWatch watch)
    {
        var root = RootOf(watch.Path);
        lock (gate)
        {
            if (root.Length == 0 || !holds.TryGetValue(root, out var hold) || !hold.Watches.Remove(watch) || hold.Watches.Count > 0)
            {
                return;
            }
            End(hold);
            _ = holds.Remove(root);
        }
    }

    /// <summary>tests: whether anything on the volume at <paramref name="root"/> is held, and whether it is let go of now.</summary>
    internal (int Watches, bool LetGo, nint Notice)? Of(string root)
    {
        lock (gate)
        {
            return holds.TryGetValue(root, out var hold) ? (hold.Watches.Count, hold.LetGo, hold.Notice) : null;
        }
    }

    private (string Root, Hold Hold)? Find(nint notice)
    {
        foreach (var (root, hold) in holds)
        {
            if (hold.Notice == notice)
            {
                return (root, hold);
            }
        }
        return null;
    }

    private void Follow(string root, Hold hold) => hold.Notice = notices?.Follow(root) ?? 0;

    private void LetGo(Hold hold)
    {
        foreach (var watch in hold.Watches)
        {
            watch.Release();
        }
        if (hold.Notice != 0)
        {
            notices?.LetGo(hold.Notice);
        }
        hold.LetGo = true;
    }

    /// <summary>Every watch on the volume started again, and the volume registered anew; false where none could start.</summary>
    private bool TakeBack(string root, Hold hold)
    {
        End(hold);
        var any = false;
        foreach (var watch in hold.Watches)
        {
            any |= watch.Resume() || watch.Watching;
        }
        hold.LetGo = !any;
        if (any)
        {
            Follow(root, hold);
        }
        return any;
    }

    private void End(Hold hold)
    {
        if (hold.Notice != 0)
        {
            notices?.End(hold.Notice);
            hold.Notice = 0;
        }
    }

    private sealed class Hold
    {
        public List<FolderWatch> Watches { get; } = [];

        /// <summary>The volume's registration with the dock's window, 0 where there is none.</summary>
        public nint Notice { get; set; }

        /// <summary>Let go of: the watches stopped, waiting for the volume to be there again.</summary>
        public bool LetGo { get; set; }
    }

    /// <summary>dbt.h's DEV_BROADCAST_HANDLE, without its data.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct BroadcastHandle
    {
        public uint Size;
        public uint DeviceType;
        public uint Reserved;
        public nint Handle;
        public nint Notify;
        public Guid EventGuid;
        public int NameOffset;
        public byte Data;
    }
}

/// <summary>
/// The dock's window told of the volumes the holds follow: a handle opened on each one's root, and registered with the
/// window for its removal (RegisterDeviceNotification). Used on the dock's thread.
/// </summary>
internal sealed unsafe class WindowNotices(HWND window) : IDriveNotices
{
    private readonly Dictionary<nint, HANDLE> open = [];

    public nint Follow(string root)
    {
        HANDLE file;
        uint mode;
        // No "insert a disk" box for an empty card reader; a failed open is enough.
        _ = SetThreadErrorMode(SEM_FAILCRITICALERRORS | SEM_NOOPENFILEERRORBOX, &mode);
        try
        {
            fixed (char* path = root)
            {
                file = CreateFileW(
                    path,
                    FILE.FILE_READ_ATTRIBUTES,
                    FILE.FILE_SHARE_READ | FILE.FILE_SHARE_WRITE | FILE.FILE_SHARE_DELETE,
                    null,
                    OPEN.OPEN_EXISTING,
                    FILE.FILE_FLAG_BACKUP_SEMANTICS,
                    HANDLE.NULL);
            }
        }
        finally
        {
            _ = SetThreadErrorMode(mode, null);
        }
        if (file == HANDLE.INVALID_VALUE || file == HANDLE.NULL)
        {
            return 0;
        }
        var filter = new DriveHolds.BroadcastHandle
        {
            Size = (uint)sizeof(DriveHolds.BroadcastHandle),
            DeviceType = DriveHolds.DBT_DEVTYP_HANDLE,
            Handle = (nint)file.Value,
        };
        var notice = (nint)RegisterDeviceNotificationW((HANDLE)window.Value, &filter, (uint)DEVICE.DEVICE_NOTIFY_WINDOW_HANDLE);
        if (notice == 0)
        {
            _ = CloseHandle(file);
            return 0;
        }
        open[notice] = file;
        return notice;
    }

    public void LetGo(nint notice)
    {
        if (open.Remove(notice, out var file))
        {
            _ = CloseHandle(file);
        }
    }

    public void End(nint notice)
    {
        LetGo(notice);
        _ = UnregisterDeviceNotification((HDEVNOTIFY)notice);
    }
}
