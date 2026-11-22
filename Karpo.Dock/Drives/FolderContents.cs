// What a folder on this PC holds, apart from how it is drawn: a folder's contents line
// (FolderContentsView) shows it, in the drive flyout's
// order and with its icons (DriveListing) — folders first, then files, names as a person reads them.
//
// Nothing here touches the disk by itself. What a folder holds comes through a seam
// (FolderSource): Windows' own file system and shell for the dock, and a test's made-up folders
// for a test, so every state — empty, missing, not allowed, a hundred items — can be shown and
// checked without a disk. The reading rules (what is skipped, what a failure is called, how many
// are listed) are FolderReader's and run the same over either. A list lists everything up to
// FolderReader.MostListed and scrolls; it never says how many it left out (§461).
//
// A folder that changes is followed: the source watches it (FileSystemWatcher for the real one),
// and a run of changes — a copy of a hundred files — is one read once it has settled
// (FolderWatch), not a hundred.

using System.IO;
using Karpo.Core;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock.Drives;

/// <summary>How far a folder's listing got.</summary>
internal enum FolderListState
{
    /// <summary>Still being read off the dock's thread.</summary>
    Reading,

    /// <summary>Read: <see cref="FolderContents.Entries"/> is what the folder holds.</summary>
    Listed,

    /// <summary>The folder was read and holds nothing this account is shown.</summary>
    Empty,

    /// <summary>No folder has been chosen yet.</summary>
    Unset,

    /// <summary>The folder is not there: moved, deleted, or on a drive that is not plugged in.</summary>
    Missing,

    /// <summary>This account may not see what the folder holds.</summary>
    Denied,

    /// <summary>Something else went wrong; <see cref="FolderContents.Problem"/> says what.</summary>
    Failed,
}

/// <summary>What one read of a folder found: its entries, as many as it was asked to list, in the drive flyout's order.</summary>
internal sealed record FolderContents
{
    public required FolderListState State { get; init; }

    /// <summary>The folder's path, as it was asked for.</summary>
    public string Path { get; init; } = "";

    /// <summary>What it holds, in the order it is shown (<see cref="DriveListing.Compare"/>), at most what the read asked for (<see cref="FolderRequest.Most"/>).</summary>
    public IReadOnlyList<DriveEntry> Entries { get; init; } = [];

    /// <summary>How many it holds past those: counted for <see cref="Count"/>, never listed and never said (§461).</summary>
    public int Unlisted { get; init; }

    /// <summary>The icons the rows draw, by <see cref="DriveListing.IconKey"/>: one per kind of file.</summary>
    public IReadOnlyDictionary<string, Pixels> Icons { get; init; } = NoIcons;

    /// <summary>Why a <see cref="FolderListState.Failed"/> read failed, in one sentence.</summary>
    public string Problem { get; init; } = "";

    /// <summary>The shell's picture of the folder itself, which File Explorer draws it with; null until asked, and where it has none.</summary>
    public Pixels? Picture { get; init; }

    /// <summary>
    /// §498: what File Explorer's Type column calls each kind of file the entries hold, by <see cref="FolderReader.TypeKey"/>;
    /// read only where the read asked for them (<see cref="FolderRequest.Types"/>), and a kind the shell says nothing of left out.
    /// </summary>
    public IReadOnlyDictionary<string, string> Types { get; init; } = NoTypes;

    /// <summary>How many items it holds in all: the ones listed and the ones past them.</summary>
    public int Count => Entries.Count + Unlisted;

    private static Dictionary<string, Pixels> NoIcons { get; } = [];

    private static Dictionary<string, string> NoTypes { get; } = [];

    /// <summary>Nothing read yet.</summary>
    public static FolderContents Reading(string path) => new() { State = FolderListState.Reading, Path = path };
}

/// <summary>One thing a folder holds as the file system tells it, before any rule is applied to it.</summary>
/// <param name="Attributes">Its attributes: hidden and system ones are skipped, as File Explorer skips them by default.</param>
/// <param name="Size">§498: a file's length in bytes, which the details and the tiles say; 0 for a folder, whose size is never added up.</param>
internal readonly record struct FolderItem(string Name, bool Folder, DateTime Modified, FileAttributes Attributes = FileAttributes.Normal, ulong Size = 0);

/// <summary>What one read is asked for.</summary>
/// <param name="Path">The folder.</param>
/// <param name="Most">The most entries listed, past which they are only counted: <see cref="FolderReader.MostListed"/> for a list, which scrolls; fewer for the bar's buttons.</param>
/// <param name="IconPx">How large the rows' icons are drawn, in px.</param>
/// <param name="Types">§498: whether the kinds' type names are read too (<see cref="FolderContents.Types"/>), for a view that says them.</param>
internal readonly record struct FolderRequest(string Path, int Most, int IconPx, bool Types = false);

/// <summary>
/// Where folders and what they hold come from, and what opening one does. <see cref="Windows"/> is
/// this PC's; a test hands in its own (<see cref="Current"/>), so nothing a test or a picture does
/// reads or opens a real folder.
/// </summary>
internal sealed class FolderSource
{
    /// <summary>What <paramref name="path"/> holds, as the file system says it, or the exception it said instead.</summary>
    public required Func<string, IEnumerable<FolderItem>> Enumerate { get; init; }

    /// <summary>Whether <paramref name="path"/> is a folder that is there.</summary>
    public required Func<string, bool> Exists { get; init; }

    /// <summary>The shell's icon for a path at a size, or null where it has none.</summary>
    public required Func<string, int, Pixels?> Icon { get; init; }

    /// <summary>
    /// Watches a folder and calls back, from any thread, whenever something in it changes; null
    /// where it cannot be watched. Disposing the answer stops it.
    /// </summary>
    public required Func<string, Action, IDisposable?> Watch { get; init; }

    /// <summary>
    /// Opens a path the way a click on it should: a file with its own program, a folder in File
    /// Explorer (<paramref name="folder"/>).
    /// </summary>
    public required Action<string, bool> Open { get; init; }

    /// <summary>
    /// Runs a read: on a thread of its own for the real folders (a folder on a share that has gone
    /// waits for the network), at once for a test's.
    /// </summary>
    public required Action<Action> Run { get; init; }

    /// <summary>
    /// §498: what File Explorer's Type column calls a file of that name, asked of its kind alone; null where nothing says,
    /// and no such question at all where the source has none — the views then say the extension's "PNG File".
    /// </summary>
    public Func<string, string?>? TypeName { get; init; }

    /// <summary>
    /// §498: the shell's thumbnail of a picture or a video at a size, in its own shape; null where it has none, and none
    /// asked for where the source has no such question. Asked only off the dock's thread (FolderThumbnails).
    /// </summary>
    public Func<string, int, Pixels?>? Thumbnail { get; init; }

    /// <summary>
    /// §557: what keeps count of the folders watched by the drive each is on, and lets go of them while Windows asks for
    /// the drive (<see cref="DriveHolds"/>); none for a test's own folders, whose watches hold nothing.
    /// </summary>
    public DriveHolds? Holds { get; init; }

    /// <summary>This PC's folders, through .NET's file system and the shell.</summary>
    public static FolderSource Windows { get; } = new()
    {
        Enumerate = FolderReader.FileSystem,
        Exists = Directory.Exists,
        Icon = FolderReader.ShellIcon,
        Watch = FolderReader.Watch,
        Open = FolderReader.OpenWithShell,
        Run = work => DriveReader.Start(work),
        TypeName = FolderReader.ShellTypeName,
        Thumbnail = FolderReader.ShellThumbnail,
        Holds = DriveHolds.Shared,
    };

    /// <summary>The source the widgets and the flyouts read: Windows' own, unless a test has handed in its own.</summary>
    public static FolderSource Current { get; set; } = Windows;
}

/// <summary>
/// The reading rules, over whatever source: what is listed and in which order, how many, what a
/// failure is called, and which icons are asked for. Never throws: whatever went wrong comes back
/// as a state the widget has words for.
/// </summary>
internal static class FolderReader
{
    /// <summary>
    /// The most entries one read walks through and lists; past them it only counts. A list asks for all of them and
    /// scrolls (§461): a bound on the work a folder of thousands costs, never a "more" for the person.
    /// </summary>
    internal const int MostListed = 4096;

    private const int ErrorNotReady = 21;

    /// <summary><paramref name="request"/>'s folder, read through <paramref name="source"/>, without the icons.</summary>
    public static FolderContents List(FolderRequest request, FolderSource source)
    {
        var path = request.Path;
        if (path.Length == 0)
        {
            return new FolderContents { State = FolderListState.Unset };
        }
        var found = new List<DriveEntry>(64);
        var beyond = 0;
        try
        {
            if (!source.Exists(path))
            {
                return new FolderContents { State = FolderListState.Missing, Path = path };
            }
            foreach (var item in source.Enumerate(path))
            {
                // As File Explorer shows a folder by default: what is hidden, or the system's own, is not.
                if ((item.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0 || item.Name.Length == 0)
                {
                    continue;
                }
                if (found.Count >= MostListed)
                {
                    beyond++;
                    continue;
                }
                found.Add(new DriveEntry(item.Name, item.Folder, item.Folder ? 0 : item.Size, item.Modified));
            }
        }
        catch (UnauthorizedAccessException)
        {
            return new FolderContents { State = FolderListState.Denied, Path = path };
        }
        catch (DirectoryNotFoundException)
        {
            return new FolderContents { State = FolderListState.Missing, Path = path };
        }
        catch (IOException error) when ((error.HResult & 0xFFFF) == ErrorNotReady)
        {
            return new FolderContents { State = FolderListState.Missing, Path = path };
        }
        catch (Exception error) when (error is IOException or ArgumentException or NotSupportedException or System.Security.SecurityException)
        {
            return new FolderContents { State = FolderListState.Failed, Path = path, Problem = Sentence(error) };
        }
        if (found.Count == 0 && beyond == 0)
        {
            return new FolderContents { State = FolderListState.Empty, Path = path };
        }
        var (shown, unlisted) = DriveListing.Cap(DriveListing.Sort(found), Math.Max(1, request.Most), beyond);
        return new FolderContents { State = FolderListState.Listed, Path = path, Entries = shown, Unlisted = unlisted };
    }

    /// <summary>
    /// §380: one read of <paramref name="request"/>'s folder through <paramref name="source"/>, said to
    /// <paramref name="said"/> as it comes: the listing the moment it is listed — the names never wait on
    /// the shell, which may be slow to draw a picture —, then with the folder's own picture
    /// (<paramref name="picture"/>) where it has one, then with the items' icons. A picture that fails costs
    /// the folder its picture, not the list.
    /// </summary>
    public static void Pictured(FolderRequest request, FolderSource source, Action<FolderContents> said, Func<Pixels?> picture)
    {
        var listed = List(request, source);
        said(listed);
        if (listed.State is FolderListState.Unset or FolderListState.Missing)
        {
            return;
        }
        Pixels? own = null;
        try
        {
            own = picture();
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            // A handler that misbehaves costs the folder its picture, not the list.
        }
        if (own is not null)
        {
            listed = listed with { Picture = own };
            said(listed);
        }
        if (listed.Entries.Count > 0)
        {
            listed = listed with { Icons = Icons(request, listed.Entries, source) };
            said(listed);
            // §498: and, for a view that says them, what each kind of file is called.
            if (request.Types && source.TypeName is { } named)
            {
                said(listed with { Types = Types(listed.Entries, named) });
            }
        }
    }

    /// <summary>
    /// §498: the type names of the kinds of file <paramref name="entries"/> hold, one question of
    /// <paramref name="named"/> a kind (<see cref="TypeKey"/>); a kind it says nothing of, or fails on, is left out.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Types(IReadOnlyList<DriveEntry> entries, Func<string, string?> named)
    {
        var types = new Dictionary<string, string>(StringComparer.Ordinal);
        var asked = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            var key = TypeKey(entry);
            if (entry.Folder || !asked.Add(key))
            {
                continue;
            }
            try
            {
                if (named(entry.Name) is { Length: > 0 } type)
                {
                    types[key] = type;
                }
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                // A handler that misbehaves costs its kind its name, not the list.
            }
        }
        return types;
    }

    /// <summary>§498: which kind of file an entry is, for its type name: its extension, lower case ("" for none).</summary>
    public static string TypeKey(in DriveEntry entry)
    {
        var dot = entry.Name.LastIndexOf('.');
        return dot > 0 ? entry.Name[dot..].ToLowerInvariant() : "";
    }

    /// <summary>
    /// §498: what File Explorer's Type column says of <paramref name="entry"/>: "File folder" for a folder; for a file what
    /// <paramref name="contents"/> read for its kind, else its extension's "PNG File", else "File".
    /// </summary>
    public static string TypeOf(in DriveEntry entry, FolderContents contents)
    {
        if (entry.Folder)
        {
            return S.DockFolderTypeFolder;
        }
        var key = TypeKey(entry);
        return contents.Types.TryGetValue(key, out var named) ? named
            : key.Length > 1 ? S.DockFolderTypeFile(key[1..].ToUpperInvariant())
            : S.DockFolderTypePlain;
    }

    /// <summary>
    /// One icon per <see cref="DriveListing.IconKey"/> the entries use, asked of
    /// <paramref name="source"/> for the first entry of each kind. A kind it has nothing for is left
    /// out, and its rows fall back to a glyph.
    /// </summary>
    public static IReadOnlyDictionary<string, Pixels> Icons(FolderRequest request, IReadOnlyList<DriveEntry> entries, FolderSource source)
    {
        var icons = new Dictionary<string, Pixels>(StringComparer.Ordinal);
        if (request.IconPx <= 0)
        {
            return icons;
        }
        var without = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            var key = DriveListing.IconKey(entry);
            if (icons.ContainsKey(key) || without.Contains(key))
            {
                continue;
            }
            Pixels? picture = null;
            try
            {
                picture = source.Icon(PathOf(request.Path, entry.Name), request.IconPx);
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                // A handler that misbehaves costs its kind its icon, not the list.
            }
            if (picture is not null)
            {
                icons[key] = picture;
            }
            else
            {
                without.Add(key);
            }
        }
        return icons;
    }

    /// <summary>The path of <paramref name="name"/> in the folder at <paramref name="folder"/>.</summary>
    public static string PathOf(string folder, string name) =>
        folder.EndsWith('\\') || folder.EndsWith('/') ? folder + name : $"{folder}\\{name}";

    /// <summary>
    /// What a folder is called: its own name, as File Explorer's title says it, or a drive's root
    /// as its letter ("D:\"). Empty for no folder.
    /// </summary>
    public static string NameOf(string path)
    {
        var trimmed = path.TrimEnd('\\', '/');
        if (trimmed.Length == 0)
        {
            return path;
        }
        if (trimmed.Length == 2 && trimmed[1] == ':')
        {
            return trimmed + "\\";
        }
        var cut = trimmed.LastIndexOfAny(['\\', '/']);
        return cut >= 0 && cut < trimmed.Length - 1 ? trimmed[(cut + 1)..] : trimmed;
    }

    /// <summary>"12 items", "1 item", "Empty": how much a folder holds, as File Explorer's status bar counts it.</summary>
    public static string CountOf(int count) => count switch
    {
        <= 0 => S.DockFolderEmptyWord,
        _ => S.DockFolderItems(count),
    };

    /// <summary>The one line shown in place of the items, or empty where there are items.</summary>
    public static string StateLine(FolderContents contents) => contents.State switch
    {
        FolderListState.Reading => S.DockDrivesReading,
        FolderListState.Empty => S.DockFolderEmpty,
        FolderListState.Unset => S.DockFolderUnset,
        FolderListState.Missing => S.DockFolderMissing,
        FolderListState.Denied => S.DockFolderDenied,
        FolderListState.Failed => contents.Problem.Length > 0 ? contents.Problem : S.DockFolderUnreadable,
        _ => "",
    };

    /// <summary>The same in a word or two, for a card or a line with no room for the sentence.</summary>
    public static string StateWord(FolderContents contents) => contents.State switch
    {
        FolderListState.Reading => S.DockDrivesReading,
        FolderListState.Empty => S.DockFolderEmptyWord,
        FolderListState.Unset => S.DockFolderUnsetWord,
        FolderListState.Missing => S.DockFolderMissingWord,
        FolderListState.Denied => S.DockFolderDeniedWord,
        FolderListState.Failed => S.DockFolderUnreadableWord,
        _ => "",
    };

    // --- Windows' own ----------------------------------------------------------------------------

    /// <summary>What a folder holds, as .NET's file system reads it; an entry it may not stat is skipped.</summary>
    internal static IEnumerable<FolderItem> FileSystem(string path)
    {
        var options = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
            RecurseSubdirectories = false,
            ReturnSpecialDirectories = false,
        };
        foreach (var info in new DirectoryInfo(path).EnumerateFileSystemInfos("*", options))
        {
            var folder = false;
            var modified = default(DateTime);
            var attributes = FileAttributes.Normal;
            ulong size = 0;
            try
            {
                attributes = info.Attributes;
                folder = (attributes & FileAttributes.Directory) != 0;
                modified = info.LastWriteTime;
                // Read by the enumeration with the rest: no further question of the disk.
                size = info is FileInfo file ? (ulong)Math.Max(0, file.Length) : 0;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentOutOfRangeException)
            {
                // An entry that went away mid-list keeps its name and loses its details.
            }
            yield return new FolderItem(info.Name, folder, modified, attributes, size);
        }
    }

    /// <summary>The shell's icon for <paramref name="path"/>; null where it has none or its handler failed.</summary>
    internal static Pixels? ShellIcon(string path, int px)
    {
        try
        {
            return Items.Icon(path, px);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            return null;
        }
    }

    /// <summary>§498: the shell's type name for a file named <paramref name="name"/>; null where it has none or failed.</summary>
    internal static string? ShellTypeName(string name)
    {
        try
        {
            return Items.TypeName(name);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            return null;
        }
    }

    /// <summary>§498: the shell's thumbnail of <paramref name="path"/>; null where it has none or its handler failed.</summary>
    internal static Pixels? ShellThumbnail(string path, int px)
    {
        try
        {
            return Items.Thumbnail(path, px);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            return null;
        }
    }

    /// <summary>A watcher on the folder at <paramref name="path"/>, its own entries only; null where it cannot be watched.</summary>
    internal static IDisposable? Watch(string path, Action changed)
    {
        try
        {
            var watcher = new FileSystemWatcher(path)
            {
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Attributes | NotifyFilters.LastWrite,
            };
            watcher.Created += (_, _) => Troubles.Guard("Folders: hearing a change", changed);
            watcher.Deleted += (_, _) => Troubles.Guard("Folders: hearing a change", changed);
            watcher.Renamed += (_, _) => Troubles.Guard("Folders: hearing a change", changed);
            watcher.Changed += (_, _) => Troubles.Guard("Folders: hearing a change", changed);
            // Too much at once for its buffer: read it all again.
            watcher.Error += (_, _) => Troubles.Guard("Folders: hearing a change", changed);
            watcher.EnableRaisingEvents = true;
            return watcher;
        }
        catch (Exception error) when (error is ArgumentException or IOException or PlatformNotSupportedException or UnauthorizedAccessException)
        {
            // A folder that isn't there, or a share that cannot be watched: it is read on the tick instead.
            return null;
        }
    }

    /// <summary>
    /// Opens <paramref name="path"/>: a folder in File Explorer, always — another program that
    /// registered itself for folders does not get it — and anything else with the shell, as a double
    /// click in File Explorer would. §380: the window it opens in comes to the front, the launcher
    /// handing on the right to it (§491).
    /// </summary>
    internal static void OpenWithShell(string path, bool folder)
    {
        if (path.Length == 0)
        {
            return;
        }
        if (folder)
        {
            Launch.Explorer(path);
            return;
        }
        _ = Launch.Open(path);
    }

    /// <summary>One line out of an exception, without the stack or a trailing full stop.</summary>
    private static string Sentence(Exception error)
    {
        var message = error.Message.ReplaceLineEndings(" ").Trim();
        var stop = message.IndexOf(". ", StringComparison.Ordinal);
        if (stop > 0)
        {
            message = message[..stop];
        }
        return message.TrimEnd('.');
    }
}

/// <summary>
/// A folder followed for changes: the source's watcher, and a run of its calls back made one, a
/// moment after the last of them (<see cref="SettleMs"/>) — a copy of a hundred files is one read,
/// not a hundred. Disposing it stops both. §557: a watch on Windows' own folders is held by its
/// volume (<see cref="FolderSource.Holds"/>), which lets go of its watcher — the handle that would stop
/// the drive's eject — while Windows asks for the drive, and takes it back where the drive stays or
/// comes back.
/// </summary>
internal sealed class FolderWatch : IDisposable
{
    /// <summary>How long a folder has to be still before it is read again.</summary>
    public const int SettleMs = 300;

    private readonly Lock gate = new();
    private readonly Action settled;
    private readonly FolderSource source;
    private IDisposable? watcher;
    private Timer? timer;
    private bool disposed;

    /// <summary>Follows <paramref name="path"/> through <paramref name="source"/>, calling <paramref name="settled"/> once each run of changes has settled.</summary>
    public FolderWatch(FolderSource source, string path, Action settled)
    {
        this.settled = settled;
        this.source = source;
        Path = path;
        if (path.Length > 0)
        {
            watcher = source.Watch(path, Changed);
            source.Holds?.Add(this);
        }
    }

    /// <summary>The folder followed.</summary>
    public string Path { get; }

    /// <summary>Whether it is watched now: the source could watch it, and its drive is not let go of (§557).</summary>
    public bool Watching
    {
        get
        {
            lock (gate)
            {
                return watcher is not null;
            }
        }
    }

    /// <summary>
    /// §557: the watcher stopped, and the handle it keeps on the folder with it, while the watch itself goes on
    /// (<see cref="Resume"/>); false where nothing was watching.
    /// </summary>
    internal bool Release()
    {
        IDisposable? was;
        lock (gate)
        {
            (was, watcher) = (watcher, null);
        }
        was?.Dispose();
        return was is not null;
    }

    /// <summary>
    /// §557: watched again after <see cref="Release"/>, and read again, as the folder may have changed meanwhile; false
    /// where it already is, the watch has ended, or the folder still cannot be watched (its drive is not there).
    /// </summary>
    internal bool Resume()
    {
        lock (gate)
        {
            if (disposed || watcher is not null)
            {
                return false;
            }
        }
        var made = source.Watch(Path, Changed);
        if (made is null)
        {
            return false;
        }
        lock (gate)
        {
            if (!disposed && watcher is null)
            {
                (watcher, made) = (made, null);
            }
        }
        if (made is not null)
        {
            made.Dispose();
            return false;
        }
        Changed();
        return true;
    }

    /// <summary>Something changed: the read waits until nothing has for <see cref="SettleMs"/>.</summary>
    internal void Changed()
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }
            timer ??= new Timer(_ => Troubles.Guard("Folders: reading a folder again", Settle), null, Timeout.Infinite, Timeout.Infinite);
            timer.Change(SettleMs, Timeout.Infinite);
        }
    }

    private void Settle()
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }
        }
        settled();
    }

    public void Dispose()
    {
        IDisposable? was;
        lock (gate)
        {
            if (disposed)
            {
                return;
            }
            disposed = true;
            timer?.Dispose();
            timer = null;
            (was, watcher) = (watcher, null);
        }
        was?.Dispose();
        source.Holds?.Remove(this);
    }
}
