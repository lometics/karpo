// pins: what a pin's and a folder's contents' Target shows (§425: a pin's kind by kind), asked of this PC's shell (§380; the
// person, 2026-10-28: "what it opens, for the pins should show the pin icons and the proper names, not
// the weird ass names"; "what it opens should also show a list of apps if possible").
//
//  - **Names.** A target by the name Windows gives it, as a pin to it says it on the bar (PinItems): a
//    shortcut's without ".lnk" ("File Explorer"), an app's as Start names it ("Spotify", never
//    "SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify"), a known folder's as File Explorer does ("This
//    PC", "Downloads"), a web address by its host. Until §380 the menu said the target's last part.
//  - **The apps** (InstalledApps): Start's All apps — the shell's Apps folder, packaged apps and the
//    Start menu's shortcuts alike — each by its name and opened through the Apps folder
//    ("shell:AppsFolder\<ID>"), as a pin dragged from Start is; read once a while, as it changes
//    only as apps come and go.
//  - **Pictures** (TargetPictures): each row's own icon, read off the menu's thread one after
//    another, and kept; the row wears its glyph until its picture has come, and the menu shows again
//    as each comes (Flyout.Pictures, as a team's logo comes, §369).
//
// What the menu offers is the core's (TargetChoices, OptionFields.OfPinTarget): this file only answers
// what the shell says.

using Karpo.Core;
using Karpo.Dock.Drives;
using Karpo.Dock.Native;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

/// <summary>An app of Start's All apps: its name, and the target a pin opens it by.</summary>
internal sealed record InstalledApp(string Name, string Target);

/// <summary>What a picker shows of a target, as this PC's shell says it; see the top of the file.</summary>
internal static unsafe class TargetLooks
{
    /// <summary>Windows' own folders a picker offers (§292): FOLDERID_Profile, _Desktop, _Documents, _Downloads, _Pictures, _Music and _Videos, each with its glyph.</summary>
    private static readonly (Guid Id, string Glyph)[] OwnFolders =
    [
        (new("5E6C858F-0E22-4760-9AFE-EA3317B67173"), OptionGlyphs.Home),
        (new("B4BFCC3A-DB2C-424C-B029-7FE99A87C641"), OptionGlyphs.Monitor),
        (new("FDD39AD0-238F-46AF-ADB4-6C85480369C7"), OptionGlyphs.Document),
        (new("374DE290-123F-4565-9164-39C4925E467B"), OptionGlyphs.Download),
        (new("33E28130-4E1E-4676-835A-98395C3BC3BB"), OptionGlyphs.Picture),
        (new("4BD8D571-6D19-48D3-BE97-422220080E43"), OptionGlyphs.Music),
        (new("18989B1D-99B5-455B-841C-AB7C74E4DDFC"), OptionGlyphs.Video),
    ];

    /// <summary>
    /// The name Windows gives <paramref name="target"/> (see the top of the file); a place on a network
    /// by its last part, never asked: a share that has gone keeps the shell waiting.
    /// </summary>
    public static string Name(string target)
    {
        var trimmed = target.Trim();
        return OnNetwork(trimmed)
            ? PinTargets.NameOf(trimmed)
            : PinItems.NameOf(trimmed);
    }

    /// <summary>§425: Windows' own places a pin may open as a folder, each with its glyph: This PC, the Recycle Bin.</summary>
    private static readonly (string Target, string Glyph)[] OwnPlaces =
    [
        ("shell:MyComputerFolder", OptionGlyphs.Monitor),
        ("shell:RecycleBinFolder", OptionGlyphs.Delete),
    ];

    /// <summary>§425: the most files used lately a pin's File offers.</summary>
    public const int MostRecent = 20;

    /// <summary>
    /// What a picker of <paramref name="config"/>'s widgets offers: the folders the person's pins and
    /// folders show, then Windows' own wherever the person moved them, each once, by File Explorer's
    /// names; the apps as Start lists them; every target's name as Windows gives it. §425: a pin's This
    /// PC and the Recycle Bin, the files used lately, the websites its pins open; and whether a path is a folder.
    /// </summary>
    public static TargetChoices Choices(Config config)
    {
        List<OptionFound> folders = [.. OptionFields.PinsInUse(config, Local, Name)];
        foreach (var (id, glyph) in OwnFolders)
        {
            if (KnownFolder(id) is { Length: > 0 } path)
            {
                folders.Add(TargetChoices.Row(Name(path), path, glyph));
            }
        }
        return new TargetChoices
        {
            Folders = [.. folders.DistinctBy(row => (row.Value as string ?? "").TrimEnd('\\'), StringComparer.OrdinalIgnoreCase)],
            Places = [.. OwnPlaces.Select(place => TargetChoices.Row(Name(place.Target), place.Target, place.Glyph) with { Detail = "" })],
            Files = RecentFiles.List(),
            Sites = OptionFields.SitesInUse(config),
            Apps = [.. InstalledApps.List().Select(app => TargetChoices.App(app.Name, app.Target))],
            Name = Name,
            IsFolder = IsFolder,
        };
    }

    /// <summary>Whether <paramref name="path"/> is a folder on this PC's own disks: a share is never asked.</summary>
    private static bool Local(string path) => !OnNetwork(path) && Directory.Exists(path);

    /// <summary>
    /// §425: whether a pin's <paramref name="path"/> is a folder: one there on this PC's own disks, or one that is gone and
    /// has no extension; on a network, never asked, by its extension.
    /// </summary>
    private static bool IsFolder(string path) =>
        OnNetwork(path) ? !Path.HasExtension(path) : Directory.Exists(path) || (!File.Exists(path) && !Path.HasExtension(path));

    /// <summary>Whether <paramref name="target"/> is a place on a network, which the shell is never asked about: a share that has gone keeps it waiting.</summary>
    internal static bool OnNetwork(string target) => PinFolders.OnNetwork(target, NetworkDrive);

    /// <summary>Whether the drive at <paramref name="root"/> ("X:\") is mapped to a share: the redirector's answer, never the share's.</summary>
    private static bool NetworkDrive(string root)
    {
        try
        {
            return new DriveInfo(root).DriveType == System.IO.DriveType.Network;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>§658: where the person's Downloads folder is (FOLDERID_Downloads, wherever they moved it); null where Windows has none.</summary>
    internal static string? Downloads() => KnownFolder(OwnFolders[3].Id);

    /// <summary>Where the known folder <paramref name="id"/> is; null where Windows has none.</summary>
    private static string? KnownFolder(Guid id)
    {
        char* raw = null;
        var found = SHGetKnownFolderPath(&id, 0, HANDLE.NULL, &raw);
        // The buffer is the caller's to free even when the call fails.
        var path = ComUtil.FromTaskMem(raw);
        return found.SUCCEEDED ? path : null;
    }
}

/// <summary>Start's All apps (see the top of the file), read once a while.</summary>
internal static unsafe class InstalledApps
{
    private const string AppsFolder = @"shell:AppsFolder\";

    /// <summary>How long a reading stands before the next asks again: apps come and go seldom.</summary>
    private static readonly TimeSpan Kept = TimeSpan.FromMinutes(1);

    private static readonly Lock Gate = new();
    private static (IReadOnlyList<InstalledApp> Apps, long At)? last;

    /// <summary>The apps, by their names in the order a person reads them, each once; none where the shell lists none.</summary>
    public static IReadOnlyList<InstalledApp> List()
    {
        var now = Environment.TickCount64;
        lock (Gate)
        {
            if (last is { } known && now - known.At < Kept.TotalMilliseconds)
            {
                return known.Apps;
            }
        }
        var read = Read();
        lock (Gate)
        {
            last = (read, now);
        }
        return read;
    }

    /// <summary>The Apps folder's items, asked of the shell: each one's name and the ID it is parsed by in the folder.</summary>
    private static IReadOnlyList<InstalledApp> Read()
    {
        var found = new List<InstalledApp>();
        var folder = Items.ShellItem("shell:AppsFolder");
        if (folder == null)
        {
            return found;
        }
        IEnumShellItems* items = null;
        try
        {
            var handler = BHID.BHID_EnumItems;
            if (folder->BindToHandler(null, &handler, __uuidof<IEnumShellItems>(), (void**)&items).FAILED || items == null)
            {
                return found;
            }
            IShellItem* item = null;
            uint fetched = 0;
            while (items->Next(1, &item, &fetched).Value == 0 && fetched == 1 && item != null)
            {
                try
                {
                    char* raw = null;
                    var id = item->GetDisplayName(SIGDN.SIGDN_PARENTRELATIVEPARSING, &raw).SUCCEEDED ? ComUtil.FromTaskMem(raw) : null;
                    if (Items.DisplayName(item) is { Length: > 0 } name && id is { Length: > 0 })
                    {
                        found.Add(new InstalledApp(name, AppsFolder + id));
                    }
                }
                finally
                {
                    item->Release();
                    item = null;
                }
            }
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            // A shell that fails to list them lists none: the folders are still offered.
            Troubles.Report("Pins: listing the apps", error);
        }
        finally
        {
            ComUtil.Release(ref items);
            folder->Release();
        }
        return [.. found.DistinctBy(app => app.Target, StringComparer.OrdinalIgnoreCase).OrderBy(app => app.Name, StringComparer.CurrentCultureIgnoreCase)];
    }
}

/// <summary>§655: a file or a folder used lately, as Windows' Recent folder keeps a shortcut to it: where it is, when it was used, and which it is.</summary>
internal readonly record struct RecentItem(string Path, DateTime UsedUtc, bool Folder);

/// <summary>
/// §425: the files used lately, as Windows' Recent folder keeps a shortcut to each, newest first: each a file on this
/// PC's own disks that is still there, once, at most <see cref="TargetLooks.MostRecent"/>; read once a while. §655 the one
/// reader of Windows' Recent folder: the pins' picker's files, Start's Recommended and Windows search's home (StartStore)
/// read its <see cref="Items"/>, files and folders alike.
/// </summary>
internal static class RecentFiles
{
    /// <summary>The most shortcuts read: the newest; the rest are older than any offered.</summary>
    private const int MostRead = 60;

    private static readonly TimeSpan Kept = TimeSpan.FromMinutes(1);

    private static readonly Lock Gate = new();
    private static (IReadOnlyList<RecentItem> Items, long At)? last;

    /// <summary>The files used lately, as the pins' picker offers them.</summary>
    public static IReadOnlyList<OptionFound> List() =>
        [.. Items().Where(item => !item.Folder).Take(TargetLooks.MostRecent).Select(item => TargetChoices.File(System.IO.Path.GetFileName(item.Path), item.Path))];

    /// <summary>
    /// §655: what the newest shortcuts of Windows' Recent folder point at, newest first: each a file or a folder on this PC's
    /// own disks still there, once, with when it was used; §658 where Windows keeps none (the person turned recent items
    /// off), what changed last on the Desktop, in Documents and in Downloads. Read at most once a minute, off whatever
    /// thread asks.
    /// </summary>
    public static IReadOnlyList<RecentItem> Items()
    {
        var now = Environment.TickCount64;
        lock (Gate)
        {
            if (last is { } known && now - known.At < Kept.TotalMilliseconds)
            {
                return known.Items;
            }
        }
        var read = Read(Environment.GetFolderPath(Environment.SpecialFolder.Recent), NewestPlaces());
        lock (Gate)
        {
            last = (read, now);
        }
        return read;
    }

    /// <summary>§658: where what changed last is taken from where Windows keeps no recent items: the Desktop, Documents, Downloads.</summary>
    private static List<string> NewestPlaces() =>
    [
        .. new[] { Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), TargetLooks.Downloads() }
            .OfType<string>().Where(path => path.Length > 0 && !TargetLooks.OnNetwork(path)),
    ];

    /// <summary>
    /// What the newest shortcuts of <paramref name="recent"/> (Windows' Recent folder) point at (see <see cref="Items"/>); where
    /// they point at nothing still there, what changed last in <paramref name="newestIn"/>.
    /// </summary>
    internal static IReadOnlyList<RecentItem> Read(string recent, IReadOnlyList<string> newestIn)
    {
        var found = Linked(recent);
        return found.Count > 0 ? found : Newest(newestIn);
    }

    private static List<RecentItem> Linked(string recent)
    {
        List<RecentItem> found = [];
        try
        {
            if (recent.Length == 0 || !Directory.Exists(recent))
            {
                return found;
            }
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var link in new DirectoryInfo(recent).EnumerateFiles("*.lnk").OrderByDescending(file => file.LastWriteTimeUtc).Take(MostRead))
            {
                if (PinTargets.ShortcutTarget(File.ReadAllBytes(link.FullName), recent) is not { Length: > 0 } linked)
                {
                    continue;
                }
                // A share is never asked: one that has gone keeps the shell waiting.
                var target = Environment.ExpandEnvironmentVariables(linked);
                if (TargetLooks.OnNetwork(target) || !seen.Add(target))
                {
                    continue;
                }
                if (File.Exists(target))
                {
                    found.Add(new RecentItem(target, link.LastWriteTimeUtc, Folder: false));
                }
                else if (Directory.Exists(target))
                {
                    found.Add(new RecentItem(target, link.LastWriteTimeUtc, Folder: true));
                }
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // What was read stands; none where the folder cannot be read.
        }
        return found;
    }

    /// <summary>
    /// Where Windows keeps no recent items (the person turned them off): the files and the folders changed last in
    /// <paramref name="places"/>, newest first, none hidden, so the pins' picker, Start's Recommended and Windows search's home
    /// still offer something.
    /// </summary>
    private static List<RecentItem> Newest(IReadOnlyList<string> places)
    {
        List<FileSystemInfo> found = [];
        foreach (var place in places)
        {
            try
            {
                if (Directory.Exists(place))
                {
                    found.AddRange(new DirectoryInfo(place).EnumerateFileSystemInfos().Where(Shown));
                }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                // That place offers none.
            }
        }
        return [.. found.OrderByDescending(each => each.LastWriteTimeUtc).Take(MostRead)
            .Select(each => new RecentItem(each.FullName, each.LastWriteTimeUtc, Folder: each is DirectoryInfo))];
    }

    /// <summary>What File Explorer shows by default: nothing hidden or of the system's, nothing named with a dot first, no desktop.ini.</summary>
    private static bool Shown(FileSystemInfo item) =>
        (item.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0
        && !item.Name.StartsWith('.')
        && !item.Name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// The pictures a picker's rows wear (<see cref="PinTargets.PictureOf"/>): each target's icon, read off the
/// asking thread one after another and kept; null while it comes, and the one waiting told once it has.
/// §661: kept by the target and the size it is read at (<see cref="Bucket"/>), so a row's icon is the shell's own image for
/// a row's size and a tile's or a pane's for theirs, as File Explorer picks its 16-, 24-, 48- or 96-px image by its view —
/// never one size scaled for all.
/// </summary>
internal sealed class TargetPictures(Func<string, int, Pixels?> read)
{
    /// <summary>How large each is read, in px, where nobody says: a menu row's 16 DIP icon at 200 %.</summary>
    public const int Px = 32;

    /// <summary>The most kept; past it they are read again as they are asked for.</summary>
    private const int MostKept = 512;

    /// <summary>§661: the sizes the shell keeps icons in (Windows' icon guidelines): what an ask is read at, the next one up.</summary>
    private static readonly int[] Sizes = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256];

    /// <summary>One picture asked for: a target at a size; the target's case aside, as a path's.</summary>
    private readonly record struct Ask(string Target, int Px)
    {
        public bool Equals(Ask other) => Px == other.Px && string.Equals(Target, other.Target, StringComparison.OrdinalIgnoreCase);

        public override int GetHashCode() => HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(Target), Px);
    }

    private readonly Lock gate = new();
    private readonly Dictionary<Ask, Pixels?> known = [];
    private readonly Dictionary<Ask, List<Action>> waiting = [];
    private readonly Queue<Ask> queue = new();
    private bool reading;

    // §425: the sites read before their picture had come, and who waits for it: asked again by Again.
    private readonly Dictionary<Ask, List<Action>> unseen = [];

    /// <summary>The dock's: the shell's icons, a web address's site's own (Favicons) where one has been kept.</summary>
    public static TargetPictures Shared { get; } = new(Shell);

    /// <summary>
    /// §641, §652, §661: the shell's icons at the size each is drawn, kept for every line that lists apps and files — Start's
    /// lines and Windows search's —: an app's, a file's or a folder's own (its path, its Apps folder address: the one File
    /// Explorer shows, a program's, a shortcut's or a customised folder's own included), and a kind of file's Windows gives
    /// its type (<see cref="WinSearchHit.KindIcon"/>), which reads no file: a sample's.
    /// </summary>
    public static TargetPictures Sized { get; } = new(static (target, px) =>
        target.StartsWith(WinSearchHit.KindScheme, StringComparison.Ordinal) ? Items.KindIcon(target[WinSearchHit.KindScheme.Length..], px) : Items.Icon(target, px));

    /// <summary>§661: the size an icon drawn <paramref name="px"/> px across is read at: the shell's next size up, 256 at the most.</summary>
    public static int Bucket(float px)
    {
        foreach (var size in Sizes)
        {
            if (size + 0.5f >= px)
            {
                return size;
            }
        }
        return Sizes[^1];
    }

    /// <summary>Whether <paramref name="address"/> is a target's picture, rather than a web picture's address.</summary>
    public static bool Owns(string address) => address.StartsWith(PinTargets.PictureScheme, StringComparison.Ordinal);

    /// <summary>The picture <paramref name="address"/> asks for; null while it comes, <paramref name="came"/> called once it has, and null for a target with none.</summary>
    public Pixels? Get(string address, Action came) => Get(address, came, Px);

    /// <summary>§661: <see cref="Get(string, Action)"/> drawn <paramref name="px"/> px across, read at its <see cref="Bucket"/>.</summary>
    public Pixels? Get(string address, Action came, float px)
    {
        var ask = new Ask(address[PinTargets.PictureScheme.Length..], Bucket(px));
        lock (gate)
        {
            if (known.TryGetValue(ask, out var pixels))
            {
                return pixels;
            }
            if (waiting.TryGetValue(ask, out var told) || unseen.TryGetValue(ask, out told))
            {
                // §658: told once however often it asks while it comes. A line asks at every drawing, and each picture that
                // came woke it once per ask before: the first search's dozens of icons became thousands of redraws, the
                // dock frozen until they were done.
                if (!told.Contains(came))
                {
                    told.Add(came);
                }
                return null;
            }
            waiting[ask] = [came];
            queue.Enqueue(ask);
            if (reading)
            {
                return null;
            }
            reading = true;
        }
        _ = DriveReader.Start(Drain);
        return null;
    }

    /// <summary>Reads what is asked for, one after another, until nothing is.</summary>
    private void Drain()
    {
        while (true)
        {
            Ask ask;
            lock (gate)
            {
                if (!queue.TryDequeue(out ask))
                {
                    reading = false;
                    return;
                }
            }
            Pixels? pixels = null;
            try
            {
                pixels = read(ask.Target, ask.Px);
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                // A handler that misbehaves costs the row its picture: it keeps its glyph.
            }
            List<Action> told;
            lock (gate)
            {
                if (known.Count >= MostKept)
                {
                    known.Clear();
                }
                told = waiting.Remove(ask, out var those) ? those : [];
                // §425: a site's picture is fetched (Favicons): none yet may come, so it is asked again then (Again).
                if (pixels is null && Items.IsWeb(ask.Target))
                {
                    unseen[ask] = told;
                }
                else
                {
                    known[ask] = pixels;
                }
            }
            if (pixels is not null)
            {
                foreach (var each in told)
                {
                    Troubles.Guard("Pins: a picture came", each);
                }
            }
        }
    }

    /// <summary>
    /// §425: the sites whose picture had not come when they were read, read again — a site's picture has arrived
    /// (the dock's FaviconArrived) —, who waits for one told once it is in.
    /// </summary>
    public void Again()
    {
        lock (gate)
        {
            foreach (var (ask, told) in unseen)
            {
                waiting[ask] = told;
                queue.Enqueue(ask);
            }
            unseen.Clear();
            if (reading || queue.Count == 0)
            {
                return;
            }
            reading = true;
        }
        _ = DriveReader.Start(Drain);
    }

    /// <summary>A target's icon as the shell draws it; a web address's site's, where one is kept; a network place's never, which may keep it waiting.</summary>
    private static Pixels? Shell(string target, int px) =>
        Items.IsWeb(target) ? Favicons.FaviconStore.Current?.Picture(target, px)
        : TargetLooks.OnNetwork(target) ? null
        : Items.Icon(target, px);
}
