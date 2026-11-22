// Windows' own taskbar pins, kept in step with Karpo's (backlog 2).
//
// Two rules, one each way:
//
// - A pin added to Windows' taskbar — from Start, from an app's menu, by an installer — is added
//   to every layout a display shows now, after the last pin on that layout's main bar (AddTo,
//   AfterLastPin). Layouts no display shows are left alone, and so is a layout that already holds
//   the app, anywhere in it.
// - A pin removed in Karpo is taken off Windows' taskbar too, once no layout holds it any more
//   (Release): one layout of three letting it go leaves Windows' pin where it is.
//
// Where Windows keeps them. Explorer keeps its pins in
// HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Taskband, value Favorites: one entry per
// pin in the taskbar's order — a byte 0, the length of an item ID list, the list — ended by 0xFF
// (TaskbandFavorites). A desktop app's entry names a shortcut Explorer keeps in
// %APPDATA%\Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar; a packaged app's names
// the app in the Apps folder and has no shortcut at all, which is why reading the folder alone
// missed them. FavoritesResolve beside it holds the shortcuts' own link data, which nothing here
// needs. Where the value can't be read, the folder's shortcuts stand in, in name order.
//
// Nothing here writes either of them: deleting the shortcut does not tell Explorer, and there is
// no supported way to add a pin. Taking one off goes through Windows' own "Unpin from taskbar"
// (IWindowsPinShell, the dock's WindowsPinShell).
//
// No echo. Windows' pins are compared with the ones seen last (Seen), and only a pin Windows
// gained is ever added to Karpo; Karpo only ever takes pins off Windows' taskbar. So Karpo's own
// unpin, read back, is a pin Windows lost, which adds nothing; and Karpo adding a pin to its
// layouts never releases one. A pin Windows would not let go of stays in Seen, so it is not
// "gained" again either.
//
// Both rules act on the person's real taskbar, so each holds back wherever it can't be sure:
//
// - Only a read of Windows' pins that is whole moves what was seen (Read is null otherwise): a
//   read that came back short would forget pins, and the next whole one would add them again.
//   With nothing seen yet, the first whole read is taken as it is, and adds nothing.
// - More than MostGainedAtOnce pins gained in one read is not somebody pinning: it is a memory
//   that was lost, or a taskbar put back whole. They are taken as they are, and the log says so.
// - Release lets go only between two configs that are both the person's own (Config.StandsIn):
//   never from defaults the dock ran on while the file couldn't be read. The dock asks it only for
//   an edit made through Karpo (Dock.WindowsPins.cs).
// - A pin with no app id holds only a pin of the same program with none either, or with the one
//   Windows gives the program itself (IsDefaultId): a browser's pin never holds its web apps.
// - The review layouts (ReviewLayouts) neither get Windows' pins nor hold them.
// - A config that stands in for the person's file (Config.StandsIn) gets none either (Follow): what
//   Windows gained waits, unread, until their own config is in force.

using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace Karpo.Core;

/// <summary>What Karpo reads of Windows' taskbar pins. Tests stand in a list of their own.</summary>
public interface IWindowsPinStore
{
    /// <summary>
    /// The pins on Windows' taskbar in its order, each as the target a Karpo pin opens: a
    /// shortcut's path, or <c>shell:AppsFolder\&lt;app id&gt;</c> for a packaged app. Null when they
    /// can't be read now, which changes nothing.
    /// </summary>
    IReadOnlyList<string>? Read();
}

/// <summary>What Karpo asks of Windows' shell to take a pin off its taskbar. Tests stand in a fake.</summary>
public interface IWindowsPinShell
{
    /// <summary>
    /// Asks for the pin <paramref name="target"/> (as <see cref="IWindowsPinStore.Read"/> named it)
    /// to be taken off Windows' taskbar; true when it was asked. The shell may do it a moment later,
    /// and Windows' pins say whether it did.
    /// </summary>
    bool Unpin(string target);
}

/// <summary>
/// The identities a pin's app answers to, as the dock resolves them (<c>aumid:</c> and <c>exe:</c>
/// keys, lowercase); empty where nothing could be resolved. Two pins are the same app when their
/// targets are the same, when they share an app id, or — where one of them has no app id — when
/// they start the same program. Two different app ids are two apps even with one program (two
/// browser profiles, two web apps of one browser).
/// </summary>
public delegate IReadOnlyCollection<string> PinKeys(string target);

/// <summary>Windows' taskbar pins and Karpo's, kept in step (see the note at the top of this file).</summary>
public sealed class WindowsPinSync
{
    private readonly IWindowsPinStore store;
    private readonly IWindowsPinShell shell;
    private readonly PinKeys keysOf;

    /// <summary>Windows' pins as last seen whole; null until they have been.</summary>
    private List<string>? seen;

    /// <summary>
    /// More pins than this gained in one read are taken as they are rather than added: nobody pins
    /// that many at once, but a memory lost or a taskbar put back whole looks just like it.
    /// </summary>
    public const int MostGainedAtOnce = 3;

    /// <summary>The ids of the person's review layouts, rebuilt by a tool: never given Windows' pins, and holding none of them. The grid's went with the grid (2026-10-09).</summary>
    public static readonly string[] ReviewLayouts = ["widgets-list"];

    /// <summary>
    /// Starts from <paramref name="remembered"/>, Windows' pins as Karpo last saw them (kept across
    /// restarts, so a pin added while Karpo was not running is still found); where there is no such
    /// memory, or it is empty, from Windows' pins as they are now, so nothing already pinned counts
    /// as new — or, when they can't be read whole now, from the first read that is.
    /// </summary>
    public WindowsPinSync(IWindowsPinStore store, IWindowsPinShell shell, PinKeys keysOf, IEnumerable<string>? remembered = null)
    {
        this.store = store;
        this.shell = shell;
        this.keysOf = keysOf;
        seen = remembered?.ToList() is { Count: > 0 } kept ? kept : store.Read()?.ToList();
    }

    /// <summary>Windows' pins as last seen, in its order: what is worth remembering across a restart.</summary>
    public IReadOnlyList<string> Seen => seen ?? [];

    /// <summary>Whether Windows' pins have been seen whole yet; until then there is nothing worth remembering.</summary>
    public bool HasSeen => seen is not null;

    /// <summary>
    /// Windows' pins read again: the ones it gained since they were last seen, in its order, and
    /// none when they can't be read whole. What it lost is only forgotten — that is how Karpo's own
    /// unpin comes back, and it must not do anything. The first whole read, and one that gained
    /// more than <see cref="MostGainedAtOnce"/>, are taken as they are and gain nothing.
    /// </summary>
    public IReadOnlyList<string> Gained()
    {
        if (store.Read() is not { } now)
        {
            return [];
        }
        if (seen is not { } before)
        {
            seen = [.. now];
            return [];
        }
        var gained = now.Where(pin => !before.Any(old => SameTarget(old, pin))).ToList();
        seen = [.. now];
        if (gained.Count > MostGainedAtOnce)
        {
            Troubles.Write($"Windows' taskbar pins: {gained.Count} came at once, more than somebody pins; taken as they are, and none added to Karpo's layouts");
            return [];
        }
        return gained;
    }

    /// <summary>
    /// After Karpo's config went from <paramref name="before"/> to <paramref name="after"/>: every
    /// pin of Windows' that some layout held before and none holds now is taken off Windows'
    /// taskbar. Returns the ones the shell was asked to take off. A change that let go of no pin's
    /// target — nearly every change — asks nothing of anything.
    /// </summary>
    public IReadOnlyList<string> Release(Config before, Config after)
    {
        // Defaults run on for now, or a recovery, compared with the person's file is no removal.
        if (before.StandsIn || after.StandsIn || seen is not { } windows)
        {
            return [];
        }
        var then = Targets(before).ToList();
        var now = Targets(after).ToList();
        if (then.All(target => now.Any(other => SameTarget(target, other))))
        {
            return [];
        }
        var released = new List<string>();
        foreach (var pin in windows)
        {
            if (Holds(then, pin) && !Holds(now, pin))
            {
                released.Add(pin);
                _ = shell.Unpin(pin);
            }
        }
        return released;
    }

    /// <summary>
    /// Windows' pins read again (<see cref="Gained"/>) and the ones it gained added to
    /// <paramref name="layoutIds"/>, the layouts the displays show (<see cref="AddTo"/>), on a copy of
    /// <paramref name="config"/>: the config to take, or null where nothing changed. A config that
    /// stands in for a file not read as the person left it (<see cref="Config.StandsIn"/>) reads
    /// nothing at all: what Windows gained stays gained until the person's own config is in force,
    /// rather than going into defaults that a save would merge into their file.
    /// </summary>
    public Config? Follow(Config config, IEnumerable<string> layoutIds)
    {
        if (config.StandsIn || Gained() is not { Count: > 0 } gained)
        {
            return null;
        }
        var next = config.Clone();
        return AddTo(next, layoutIds, gained) ? next : null;
    }

    /// <summary>
    /// Adds each of <paramref name="gained"/> to every layout of <paramref name="layoutIds"/> (the
    /// ones the displays show) that doesn't hold it yet anywhere — in a slot its type hides or in a
    /// folder too — after that layout's last pin on its main bar, in Windows' order
    /// (<see cref="AfterLastPin"/>). A review layout is left alone. True when <paramref name="config"/> changed.
    /// </summary>
    public bool AddTo(Config config, IEnumerable<string> layoutIds, IReadOnlyList<string> gained)
    {
        var changed = false;
        foreach (var id in layoutIds.Distinct(StringComparer.Ordinal))
        {
            if (config.Layouts.FirstOrDefault(layout => layout.Id == id) is not { } layout || IsReview(layout))
            {
                continue;
            }
            foreach (var target in gained)
            {
                if (Holds(Targets(layout.Islands).ToList(), target))
                {
                    continue;
                }
                var pin = BarKind.Pin.New();
                pin.Pin!.Target = target;
                pin.Id = Folders.FreeId(layout.Islands, pin.Id);
                AfterLastPin(layout, pin);
                changed = true;
            }
        }
        return changed;
    }

    /// <summary>
    /// Puts <paramref name="pin"/> after the last pin standing on <paramref name="layout"/>'s bar itself,
    /// in that pin's section; where there is none, into the middle after the open apps standing there,
    /// else at its end. A pin in a folder is not where the new one goes: it would not be seen there.
    /// </summary>
    internal static void AfterLastPin(TaskbarLayout layout, BarItem pin)
    {
        var list = layout.Islands;
        Sections.Order(list);
        var last = list.FindLastIndex(island => island?.Kind == BarKind.Pin);
        if (last >= 0)
        {
            Sections.Put(list, pin, new BarSlot(Sections.Of(list[last]), last + 1));
            return;
        }
        var apps = list.FindIndex(island => island?.Kind == BarKind.Apps && Sections.Of(island) == BarSection.Middle);
        Sections.Put(list, pin, new BarSlot(BarSection.Middle, apps >= 0 ? apps + 1 : Sections.Range(list, BarSection.Middle).To));
    }

    /// <summary>Whether any of <paramref name="targets"/> is the same app as Windows' pin <paramref name="pin"/>.</summary>
    private bool Holds(List<string> targets, string pin) =>
        targets.Any(target => SameTarget(target, pin)) || targets.Any(target => SameApp(KeysOf(target), KeysOf(pin)));

    /// <summary>Every pin target of every layout but the review layouts, in folders too.</summary>
    private static IEnumerable<string> Targets(Config config) =>
        (config.Layouts ?? []).Where(layout => !IsReview(layout)).SelectMany(layout => Targets(layout.Islands ?? []));

    /// <summary>Whether <paramref name="layout"/> is one of the person's review layouts (<see cref="ReviewLayouts"/>).</summary>
    private static bool IsReview(TaskbarLayout layout) =>
        ReviewLayouts.Contains(layout.Id, StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<string> Targets(IEnumerable<BarItem> list) =>
        Folders.All(list)
            .Where(entry => entry.Island.Info == WidgetInfos.Pin && entry.Island.Pin?.Target is { Length: > 0 })
            .Select(entry => entry.Island.Pin!.Target);

    private readonly Dictionary<string, IReadOnlyCollection<string>> keys = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The keys of <paramref name="target"/>, asked once: resolving one asks the shell.</summary>
    private IReadOnlyCollection<string> KeysOf(string target)
    {
        if (keys.Count > 512)
        {
            keys.Clear();
        }
        var normal = Normal(target);
        if (!keys.TryGetValue(normal, out var found))
        {
            keys[normal] = found = keysOf(target);
        }
        return found;
    }

    /// <summary>
    /// Whether two pins with <paramref name="one"/> and <paramref name="other"/> keys are one app:
    /// a shared app id; or, where one has none, a shared program — and then only when the other
    /// has no app id either, or only the one Windows gives that program itself (<see cref="IsDefaultId"/>).
    /// Two different app ids are two apps, whatever program they start: a pin of a browser holds
    /// neither its web apps nor a shortcut that names an app id of its own.
    /// </summary>
    public static bool SameApp(IReadOnlyCollection<string> one, IReadOnlyCollection<string> other)
    {
        var ids = one.Where(IsAumid).ToList();
        var otherIds = other.Where(IsAumid).ToList();
        if (ids.Count > 0 && otherIds.Count > 0)
        {
            return ids.Intersect(otherIds, StringComparer.OrdinalIgnoreCase).Any();
        }
        var programs = one.Where(key => !IsAumid(key)).Intersect(other.Where(key => !IsAumid(key)), StringComparer.OrdinalIgnoreCase).ToList();
        // The side that has app ids: each must be the shared program's own.
        var named = ids.Count > 0 ? ids : otherIds;
        return programs.Count > 0 && named.All(id => programs.Any(program => IsDefaultId(id, program)));
    }

    private static bool IsAumid(string key) => key.StartsWith("aumid:", StringComparison.Ordinal);

    /// <summary>
    /// Whether the app id <paramref name="aumid"/> is the one Windows gives the program
    /// <paramref name="exe"/> itself, when it sets none of its own: its path, or its path with the
    /// known folder it is in written as that folder's id (<c>{6D809377-…}\Brave\brave.exe</c> for
    /// Program Files). Either may carry its <c>aumid:</c> or <c>exe:</c> key's prefix.
    /// </summary>
    public static bool IsDefaultId(string aumid, string exe)
    {
        var id = aumid.StartsWith("aumid:", StringComparison.Ordinal) ? aumid["aumid:".Length..] : aumid;
        var path = exe.StartsWith("exe:", StringComparison.Ordinal) ? exe["exe:".Length..] : exe;
        if (id.Length == 0 || path.Length == 0)
        {
            return false;
        }
        if (string.Equals(id, path, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        // "{known folder}\rest": the rest is the end of the path, from a folder boundary.
        var close = id.IndexOf("}\\", StringComparison.Ordinal);
        if (id[0] != '{' || close < 0 || !Guid.TryParse(id[..(close + 1)], out _))
        {
            return false;
        }
        var rest = id[(close + 1)..];
        return rest.Length > 1 && path.EndsWith(rest, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The same target, letter case and a trailing slash aside.</summary>
    public static bool SameTarget(string? a, string? b) =>
        a is not null && b is not null && string.Equals(Normal(a), Normal(b), StringComparison.OrdinalIgnoreCase);

    private static string Normal(string target)
    {
        var text = target.Trim();
        while (text.Length > 3 && text[^1] is '\\' or '/' && text[^2] is not (':' or '/'))
        {
            text = text[..^1];
        }
        return text;
    }
}

/// <summary>One entry of Explorer's Favorites value: its item ID list, and whether it names an app in the Apps folder.</summary>
public sealed record TaskbandFavorite(byte[] Pidl, bool InAppsFolder);

/// <summary>Explorer's Favorites value read (see the note at the top of this file). Pure, for the tests.</summary>
public static class TaskbandFavorites
{
    /// <summary>The Apps folder (FOLDERID_AppsFolder's shell folder, CLSID {4234D49B-0245-4DF3-B780-3893943456E1}).</summary>
    public static readonly Guid AppsFolder = new("4234D49B-0245-4DF3-B780-3893943456E1");

    /// <summary>
    /// The entries of <paramref name="blob"/> in the taskbar's order. Null when it isn't the shape
    /// Explorer writes — a byte other than 0 or 0xFF where an entry starts, a length past the end,
    /// an ID list that doesn't end inside its entry — so a value Windows changed the shape of reads
    /// as unreadable, never as half of itself.
    /// </summary>
    public static IReadOnlyList<TaskbandFavorite>? Parse(ReadOnlySpan<byte> blob)
    {
        var found = new List<TaskbandFavorite>();
        var at = 0;
        while (true)
        {
            if (at >= blob.Length)
            {
                // Ended without its 0xFF: Explorer always writes one.
                return null;
            }
            if (blob[at] == 0xFF)
            {
                return found;
            }
            if (blob[at] != 0 || at + 5 > blob.Length)
            {
                return null;
            }
            var length = BinaryPrimitives.ReadUInt32LittleEndian(blob[(at + 1)..]);
            var start = at + 5;
            if (length < 2 || length > (uint)(blob.Length - start))
            {
                return null;
            }
            var pidl = blob.Slice(start, (int)length);
            if (!Ends(pidl))
            {
                return null;
            }
            found.Add(new TaskbandFavorite(pidl.ToArray(), InApps(pidl)));
            at = start + (int)length;
        }
    }

    /// <summary>Whether the ID list ends (a zero size) inside <paramref name="pidl"/>.</summary>
    private static bool Ends(ReadOnlySpan<byte> pidl)
    {
        var at = 0;
        while (at + 2 <= pidl.Length)
        {
            var size = BinaryPrimitives.ReadUInt16LittleEndian(pidl[at..]);
            if (size == 0)
            {
                return true;
            }
            if (size < 2)
            {
                return false;
            }
            at += size;
        }
        return false;
    }

    /// <summary>Whether the first item is the Apps folder's: a root item (0x1F) holding its CLSID.</summary>
    private static bool InApps(ReadOnlySpan<byte> pidl) =>
        pidl.Length >= 20
        && BinaryPrimitives.ReadUInt16LittleEndian(pidl) >= 20
        && pidl[2] == 0x1F
        && new Guid(pidl.Slice(4, 16)) == AppsFolder;

    /// <summary>
    /// The pin target an entry stands for, from the shell's name for it (its desktop-absolute
    /// parsing name): a packaged app's is its app id, which a Karpo pin opens as
    /// <c>shell:AppsFolder\&lt;app id&gt;</c>; a shortcut's is its path. Null for anything else.
    /// </summary>
    public static string? Target(TaskbandFavorite entry, string? name) =>
        name is null ? null : TargetOf(entry, name);

    /// <summary>
    /// The targets of <paramref name="entries"/> in order, each named by <paramref name="name"/>
    /// (the shell's name for an item ID list), or null when any entry gets no name: a list without
    /// it would forget a pin that is there. An entry that is named but is neither a shortcut nor a
    /// packaged app is left out, the same every time.
    /// </summary>
    public static IReadOnlyList<string>? Targets(IEnumerable<TaskbandFavorite> entries, Func<byte[], string?> name)
    {
        var pins = new List<string>();
        foreach (var entry in entries)
        {
            if (name(entry.Pidl) is not { } named || string.IsNullOrWhiteSpace(named))
            {
                return null;
            }
            if (TargetOf(entry, named) is { } target)
            {
                pins.Add(target);
            }
        }
        return pins;
    }

    private static string? TargetOf(TaskbandFavorite entry, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }
        if (entry.InAppsFolder)
        {
            return name.Contains('\\') ? null : $@"shell:AppsFolder\{name}";
        }
        return Path.IsPathFullyQualified(name) ? name : null;
    }
}

/// <summary>Windows' taskbar pins as this PC has them (<see cref="IWindowsPinStore"/>). Reads only.</summary>
public sealed class WindowsTaskbarPins : IWindowsPinStore
{
    /// <summary>The key Explorer keeps its pins under, in HKEY_CURRENT_USER.</summary>
    public const string TaskbandKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Taskband";

    /// <summary>The folder of the shortcuts behind desktop apps' pins.</summary>
    public static string Folder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar"); // not-ui: a path

    public static WindowsTaskbarPins This { get; } = new();

    /// <summary>
    /// Windows' pins from Explorer's Favorites value, whole, or null: where the value can't be
    /// read, or the shell gives no name for one of its entries, a list of the rest would forget
    /// pins that are there (<see cref="TaskbandFavorites.Targets"/>). The folder's shortcuts never
    /// stand in here: they have no packaged apps.
    /// </summary>
    public IReadOnlyList<string>? Read() =>
        Favorites() is { } entries ? TaskbandFavorites.Targets(entries, Name) : null;

    /// <summary>
    /// Windows' pins as well as they can be had, for a new bar's pins (<see cref="Sections.TaskbarPins"/>):
    /// the Favorites entries the shell names, else the folder's shortcuts in name order, else null —
    /// nothing read at all, and then a new bar takes the pins Windows gives a new taskbar
    /// (<see cref="WindowsTaskbar.DefaultPins"/>). Never for keeping in step: a short list there
    /// would look like pins gone.
    /// </summary>
    public static IReadOnlyList<string>? ReadForNewBar() =>
        ForNewBar(Favorites()?.Select(entry => TaskbandFavorites.Target(entry, Name(entry.Pidl))).ToList(), Shortcuts());

    /// <summary>
    /// A new bar's pins from Windows' Favorites value, read as its entries' targets
    /// (<paramref name="favorites"/>, null where the value can't be read, an entry null where the shell
    /// gives it no name), and from the pins' folder (<paramref name="shortcuts"/>, null where it can't
    /// be read or isn't there). A Favorites value with no entry is a taskbar the person unpinned
    /// everything from, which a new bar follows with none; one whose entries all go unnamed, or no
    /// value, falls back to the folder's shortcuts; with none of those either, nothing is known: null.
    /// </summary>
    public static IReadOnlyList<string>? ForNewBar(IReadOnlyList<string?>? favorites, IReadOnlyList<string>? shortcuts)
    {
        if (favorites is not null)
        {
            var named = favorites.OfType<string>().ToList();
            if (named.Count > 0 || favorites.Count == 0)
            {
                return named;
            }
        }
        return shortcuts is { Count: > 0 } ? shortcuts : null;
    }

    /// <summary>Explorer's Favorites value, read; null where it is missing or not in the shape it writes.</summary>
    private static IReadOnlyList<TaskbandFavorite>? Favorites()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(TaskbandKey);
            return key?.GetValue("Favorites") is byte[] blob ? TaskbandFavorites.Parse(blob) : null;
        }
        catch (Exception error) when (error is System.Security.SecurityException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>The shortcuts in the pins' folder, in name order: what stands in where Favorites can't be read. Null where the folder isn't there or can't be read.</summary>
    private static List<string>? Shortcuts()
    {
        try
        {
            var links = Directory.EnumerateFiles(Folder, "*.lnk").ToList();
            links.Sort(string.CompareOrdinal);
            return links;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or System.Security.SecurityException)
        {
            // DirectoryNotFoundException is an IOException: no folder is nothing known either.
            return null;
        }
    }

    /// <summary>The shell's desktop-absolute parsing name for an item ID list; null where it has none.</summary>
    private static unsafe string? Name(byte[] pidl)
    {
        const int SigdnDesktopAbsoluteParsing = unchecked((int)0x80028000);
        fixed (byte* list = pidl)
        {
            if (SHGetNameFromIDList((nint)list, SigdnDesktopAbsoluteParsing, out var name) != 0 || name == 0)
            {
                return null;
            }
            try
            {
                return Marshal.PtrToStringUni(name);
            }
            finally
            {
                Marshal.FreeCoTaskMem(name);
            }
        }
    }

    [DllImport("shell32.dll", ExactSpelling = true)]
    private static extern int SHGetNameFromIDList(nint pidl, int sigdn, out nint name);
}

/// <summary>
/// What Windows' taskbar had, as Karpo last saw it, kept across restarts (windows-pins.txt in the
/// local folder). The file is written whole or not at all — a temporary file beside it, through
/// to the disk, then moved over it — and starts and ends with a line of its own, so a file a crash
/// or a power cut left empty or short reads as no memory at all, never as "Windows had no pins".
/// </summary>
public static class WindowsPinMemory
{
    private const string First = "# Karpo: Windows' taskbar pins as last seen";
    private const string Last = "# end";

    /// <summary>The pins kept in <paramref name="path"/>; null when there is no such file, or it isn't one written whole.</summary>
    public static List<string>? Read(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }
            var lines = File.ReadAllLines(path);
            if (lines.Length < 2 || lines[0] != First || lines[^1] != Last)
            {
                return null;
            }
            return [.. lines[1..^1].Where(line => line.Length > 0)];
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Keeps <paramref name="pins"/> in <paramref name="path"/>, replacing what was there in one step. Throws <see cref="IOException"/> when it can't.</summary>
    public static void Write(string path, IEnumerable<string> pins)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = $"{path}.{Environment.ProcessId}.tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            using (var writer = new StreamWriter(stream))
            {
                writer.WriteLine(First);
                foreach (var pin in pins)
                {
                    writer.WriteLine(pin);
                }
                writer.WriteLine(Last);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }
}
