// An app's jump list, read the way the shell keeps it: Recent and Frequent
// through the documented IApplicationDocumentLists, what the user pinned from
// the app's AutomaticDestinations file, and the app's own categories and tasks
// from its CustomDestinations file. Read-only: both files are read shared into
// memory and parsed there, so the shell writing one at the same moment can at
// worst hand back the list as it was.

using Karpo.Dock.Native;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using static TerraFX.Interop.Windows.S;

namespace Karpo.Dock;

/// <summary>One line of a jump list.</summary>
/// <param name="Target">What <see cref="JumpLists.Invoke"/> starts: a path, a shell parsing name or a URL.</param>
/// <param name="IsTask">From the app's Tasks rather than a list of places or documents.</param>
/// <param name="App">
/// For a line a packaged app made through Windows.UI.StartScreen.JumpList (WhatsApp's chats, Snipping
/// Tool's timers): the app's AUMID, which <see cref="JumpLists.Invoke"/> activates with
/// <paramref name="Arguments"/> as the taskbar does. Null for a line that is a command line or a place.
/// </param>
public sealed record JumpEntry(string Name, string? Target, string? Arguments, string? Directory, Pixels? Icon, bool IsTask, string? App = null);

public sealed record JumpCategory(string Title, List<JumpEntry> Entries);

public static unsafe class JumpLists
{
    // The reflected CRC-64 the shell names the files with, over the AppID
    // upper-cased, in UTF-16. Checked against Explorer's well-known
    // f01b4d95cf55d32a and the files on a real profile.
    private const ulong Polynomial = 0x92C64265D32139A4;

    // Category kinds in a CustomDestinations file, and KNOWNDESTCATEGORY.
    private const uint CustomKind = 0;
    private const uint KnownKind = 1;
    private const uint TasksKind = 2;
    private const uint KnownFrequent = 1;
    private const uint KnownRecent = 2;
    private const uint CategoryEnd = 0xBABFFBAB;

    // System.AppUserModel.ActivationContext, .DestListProvidedTitle and .DestListLogoUri: what a
    // packaged app's line is activated with, named and drawn with.
    private static readonly Guid AppUserModel = new("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3");
    private static readonly PROPERTYKEY ActivationContextKey = new() { fmtid = AppUserModel, pid = 20 };
    private static readonly PROPERTYKEY ProvidedTitleKey = new() { fmtid = AppUserModel, pid = 27 };
    private static readonly PROPERTYKEY LogoKey = new() { fmtid = AppUserModel, pid = 29 };

    // Link strings are capped well below these by the shell.
    private const int PathChars = 1024;
    private const int ArgumentChars = 4096;

    /// <summary>
    /// The jump list of the app with <paramref name="aumid"/>, or, for an app without one, of
    /// <paramref name="exePath"/>. In Windows 11's order: Pinned, the app's own categories,
    /// Recent (or Frequent), Tasks. Each list holds at most <paramref name="limit"/> entries.
    /// </summary>
    public static List<JumpCategory> Read(string? aumid, string? exePath, int limit = 12) =>
        Read(aumid, exePath, limit, 32);

    /// <summary>As <see cref="Read(string?, string?, int)"/>, with icons <paramref name="iconSize"/> px square.</summary>
    public static List<JumpCategory> Read(string? aumid, string? exePath, int limit, int iconSize)
    {
        var categories = new List<JumpCategory>();
        var appId = AppId(aumid, exePath);
        if (appId is null || limit <= 0)
        {
            return categories;
        }
        var recent = Environment.GetFolderPath(Environment.SpecialFolder.Recent);
        var file = Hash(appId).ToString("x");
        if (string.IsNullOrWhiteSpace(aumid) && !HasFiles(recent, file) && ShortcutAppId(exePath!) is { } declared)
        {
            // The app names itself in a way its windows do not show.
            appId = declared;
            file = Hash(appId).ToString("x");
        }
        var opener = Opener.For(appId, exePath);

        // Pinned places are left out of Recent and Frequent, as the taskbar does.
        var shown = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pinned = Pinned(
            Path.Combine(recent, "AutomaticDestinations", file + ".automaticDestinations-ms"),
            limit,
            iconSize,
            opener,
            shown);
        if (pinned.Count > 0)
        {
            categories.Add(new JumpCategory(S.DockJumpPinned, pinned));
        }

        var parts = Custom(Path.Combine(recent, "CustomDestinations", file + ".customDestinations-ms"), limit, iconSize, opener, appId);
        List<JumpEntry>? tasks = null;
        if (parts is null)
        {
            // An app that never set a list of its own gets Windows' default one.
            AddKnown(categories, appId, KnownRecent, limit, iconSize, opener, shown);
        }
        else
        {
            // A list of its own shows Recent and Frequent only where the app
            // asked for them, and its tasks always last.
            var known = new HashSet<uint>();
            foreach (var part in parts)
            {
                if (part.Kind == CustomKind && part.Entries.Count > 0)
                {
                    categories.Add(new JumpCategory(part.Title, part.Entries));
                }
                else if (part.Kind == KnownKind && known.Add(part.Known))
                {
                    AddKnown(categories, appId, part.Known, limit, iconSize, opener, shown);
                }
                else if (part.Kind == TasksKind && part.Entries.Count > 0)
                {
                    tasks = part.Entries;
                }
            }
        }
        if (tasks is not null)
        {
            categories.Add(new JumpCategory(S.DockJumpTasks, tasks));
        }
        return categories;
    }

    /// <summary>
    /// Starts what <paramref name="entry"/> points at, as clicking it in the taskbar does: a packaged
    /// app's line activated with its arguments, anything else from its target (§491: the launcher's).
    /// </summary>
    public static void Invoke(JumpEntry entry)
    {
        if (entry.App is { } app)
        {
            Launch.Packaged(app, entry.Arguments ?? "");
            return;
        }
        if (string.IsNullOrEmpty(entry.Target))
        {
            return;
        }
        Launch.Jump(entry.Target, string.IsNullOrEmpty(entry.Arguments) ? null : entry.Arguments, string.IsNullOrEmpty(entry.Directory) ? null : entry.Directory);
    }

    /// <summary>
    /// The AppID the shell files the jump list under: <paramref name="aumid"/> when there is one,
    /// otherwise the one Windows derives from <paramref name="exePath"/> — the path with its
    /// nearest known folder written as that folder's id, e.g.
    /// <c>{1AC14E77-02E7-4E5D-B744-2EB1AE5198B7}\notepad.exe</c>.
    /// </summary>
    public static string? AppId(string? aumid, string? exePath)
    {
        if (!string.IsNullOrWhiteSpace(aumid))
        {
            return aumid;
        }
        return string.IsNullOrWhiteSpace(exePath) ? null : ImplicitAppId(exePath);
    }

    /// <summary>The number a jump list file is named with, written in hex without leading zeros.</summary>
    public static ulong Hash(string appId)
    {
        ulong crc = ulong.MaxValue;
        foreach (char c in appId.ToUpperInvariant())
        {
            crc = Step(Step(crc, (byte)c), (byte)(c >> 8));
        }
        return crc;
    }

    private static ulong Step(ulong crc, byte value)
    {
        crc ^= value;
        for (int bit = 0; bit < 8; bit++)
        {
            crc = (crc & 1) != 0 ? (crc >> 1) ^ Polynomial : crc >> 1;
        }
        return crc;
    }

    private static string ImplicitAppId(string exePath)
    {
        var clsid = CLSID.CLSID_KnownFolderManager;
        IKnownFolderManager* manager = null;
        IKnownFolder* folder = null;
        char* root = null;
        try
        {
            if (CoCreateInstance(
                    &clsid,
                    null,
                    (uint)CLSCTX.CLSCTX_INPROC_SERVER,
                    __uuidof<IKnownFolderManager>(),
                    (void**)&manager).FAILED)
            {
                return exePath;
            }
            fixed (char* path = exePath)
            {
                if (manager->FindFolderFromPath(path, FFFP_MODE.FFFP_NEARESTPARENTMATCH, &folder).FAILED)
                {
                    return exePath;
                }
            }
            Guid id;
            if (folder->GetId(&id).FAILED || folder->GetPath(0, &root).FAILED || root == null)
            {
                return exePath;
            }
            var prefix = new string(root).TrimEnd('\\');
            if (!exePath.StartsWith(prefix + "\\", StringComparison.OrdinalIgnoreCase))
            {
                return exePath;
            }
            return id.ToString("B").ToUpperInvariant() + exePath[prefix.Length..];
        }
        finally
        {
            CoTaskMemFree(root);
            ComUtil.Release(ref folder);
            ComUtil.Release(ref manager);
        }
    }

    private static bool HasFiles(string recent, string file) =>
        File.Exists(Path.Combine(recent, "AutomaticDestinations", file + ".automaticDestinations-ms"))
        || File.Exists(Path.Combine(recent, "CustomDestinations", file + ".customDestinations-ms"));

    /// <summary>The Start menu's and the taskbar pins' declared AppUserModelIDs, as last read.</summary>
    /// <param name="ByPath">A shortcut's target → the ID it declares.</param>
    /// <param name="ByFolder">
    /// A launcher's folder and the executable it starts → the ID. Squirrel installs an app as
    /// <c>app-&lt;version&gt;\app.exe</c> under a folder holding <c>Update.exe</c> and often a stub
    /// <c>app.exe</c>; shortcuts start either of those, never the executable that runs.
    /// </param>
    private sealed record Shortcuts(Dictionary<string, string> ByPath, Dictionary<(string Folder, string File), string> ByFolder, long At);

    private static volatile Shortcuts? shortcuts;
    private static int scanning;

    /// <summary>
    /// Starts reading the shortcuts <see cref="Read(string?, string?, int)"/> falls back on, in the
    /// background, so the first right-click does not wait for them. Calling it again while a read
    /// runs does nothing.
    /// </summary>
    public static void Prepare()
    {
        if (Interlocked.CompareExchange(ref scanning, 1, 0) != 0)
        {
            return;
        }
        // Loading a couple of hundred shortcuts takes a quarter of a second
        // warm and seconds cold: never on the thread that draws the dock.
        var thread = new Thread(() =>
        {
            try
            {
                shortcuts = ScanShortcuts();
            }
            catch (Exception)
            {
            }
            finally
            {
                Interlocked.Exchange(ref scanning, 0);
            }
        })
        {
            IsBackground = true,
            Name = "Karpo shortcuts", // not-ui: a thread's name
            Priority = ThreadPriority.BelowNormal,
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    /// <summary>
    /// The AppUserModelID an app's shortcut declares, for an app whose windows carry none:
    /// Windows gives a process started from a shortcut that shortcut's ID, and apps that set
    /// their own ID in code set the one their installer wrote there. Null until the background
    /// read has finished; the read is renewed after five minutes.
    /// </summary>
    private static string? ShortcutAppId(string exePath)
    {
        var known = shortcuts;
        if (known is null || Environment.TickCount64 - known.At > 5 * 60 * 1000)
        {
            Prepare();
        }
        if (known is null)
        {
            return null;
        }
        if (known.ByPath.TryGetValue(exePath, out var id))
        {
            return id;
        }
        var folder = Path.GetDirectoryName(exePath);
        var root = folder is null ? null : Path.GetDirectoryName(folder);
        return root is not null && known.ByFolder.TryGetValue((root, Path.GetFileName(exePath)), out id) ? id : null;
    }

    private static Shortcuts ScanShortcuts()
    {
        var byPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var byFolder = new Dictionary<(string, string), string>(new FolderKey());
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string[] folders =
        [
            Path.Combine(appData, @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar"), // not-ui: a path
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs"),
        ];
        var clsid = CLSID.CLSID_ShellLink;
        var path = new char[PathChars];
        var arguments = new char[ArgumentChars];
        foreach (var folder in folders)
        {
            List<string> links;
            try
            {
                links = Directory.EnumerateFiles(folder, "*.lnk", new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    MaxRecursionDepth = 4,
                    IgnoreInaccessible = true,
                    AttributesToSkip = FileAttributes.ReparsePoint,
                }).ToList();
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                continue;
            }
            foreach (var file in links)
            {
                IShellLinkW* link = null;
                IPersistFile* persist = null;
                IPropertyStore* store = null;
                try
                {
                    if (CoCreateInstance(&clsid, null, (uint)CLSCTX.CLSCTX_INPROC_SERVER, __uuidof<IShellLinkW>(), (void**)&link).FAILED
                        || link->QueryInterface(__uuidof<IPersistFile>(), (void**)&persist).FAILED)
                    {
                        continue;
                    }
                    fixed (char* name = file)
                    {
                        if (persist->Load(name, (uint)STGM.STGM_READ).FAILED)
                        {
                            continue;
                        }
                    }
                    if (link->QueryInterface(__uuidof<IPropertyStore>(), (void**)&store).FAILED
                        || StringProperty(store, PKEY.PKEY_AppUserModel_ID) is not { Length: > 0 } id)
                    {
                        continue;
                    }
                    fixed (char* p = path)
                    fixed (char* a = arguments)
                    {
                        if (link->GetPath(p, PathChars, null, 0).FAILED || p[0] == '\0')
                        {
                            continue;
                        }
                        var target = new string(p);
                        if (Path.GetDirectoryName(target) is not { } launcher)
                        {
                            continue;
                        }
                        if (Path.GetFileName(target).Equals("Update.exe", StringComparison.OrdinalIgnoreCase))
                        {
                            if (link->GetArguments(a, ArgumentChars).SUCCEEDED && SquirrelTarget(new string(a)) is { } started)
                            {
                                byFolder.TryAdd((launcher, started), id);
                            }
                            continue;
                        }
                        byPath.TryAdd(target, id);
                        // A stub beside app-<version> folders starts its namesake in one of them.
                        byFolder.TryAdd((launcher, Path.GetFileName(target)), id);
                    }
                }
                finally
                {
                    ComUtil.Release(ref store);
                    ComUtil.Release(ref persist);
                    ComUtil.Release(ref link);
                }
            }
        }
        return new Shortcuts(byPath, byFolder, Environment.TickCount64);
    }

    /// <summary>The <c>app.exe</c> of <c>--processStart "app.exe"</c>, quoted or not.</summary>
    private static string? SquirrelTarget(string arguments)
    {
        const string Switch = "--processStart";
        int at = arguments.IndexOf(Switch, StringComparison.OrdinalIgnoreCase);
        if (at < 0)
        {
            return null;
        }
        var rest = arguments[(at + Switch.Length)..].TrimStart();
        if (rest.StartsWith('"'))
        {
            int end = rest.IndexOf('"', 1);
            return end > 1 ? rest[1..end] : null;
        }
        int space = rest.IndexOf(' ');
        var name = space < 0 ? rest : rest[..space];
        return name.Length > 0 ? name : null;
    }

    private sealed class FolderKey : IEqualityComparer<(string Root, string File)>
    {
        public bool Equals((string Root, string File) x, (string Root, string File) y) =>
            string.Equals(x.Root, y.Root, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.File, y.File, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string Root, string File) key) =>
            HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(key.Root), StringComparer.OrdinalIgnoreCase.GetHashCode(key.File));
    }

    private static void AddKnown(
        List<JumpCategory> categories,
        string appId,
        uint known,
        int limit,
        int iconSize,
        Opener opener,
        HashSet<string> shown)
    {
        var type = known == KnownFrequent ? APPDOCLISTTYPE.ADLT_FREQUENT : APPDOCLISTTYPE.ADLT_RECENT;
        var entries = Documents(appId, type, limit, iconSize, opener, shown);
        if (entries.Count > 0)
        {
            categories.Add(new JumpCategory(known == KnownFrequent ? S.DockJumpFrequent : S.DockJumpRecent, entries));
        }
    }

    /// <summary>Recent or Frequent, through the documented list; pinned places skipped.</summary>
    private static List<JumpEntry> Documents(
        string appId,
        APPDOCLISTTYPE type,
        int limit,
        int iconSize,
        Opener opener,
        HashSet<string> shown)
    {
        var found = new List<JumpEntry>();
        var clsid = CLSID.CLSID_ApplicationDocumentLists;
        IApplicationDocumentLists* lists = null;
        IObjectArray* array = null;
        try
        {
            if (CoCreateInstance(
                    &clsid,
                    null,
                    (uint)CLSCTX.CLSCTX_INPROC_SERVER,
                    __uuidof<IApplicationDocumentLists>(),
                    (void**)&lists).FAILED)
            {
                return found;
            }
            fixed (char* id = appId)
            {
                if (lists->SetAppID(id).FAILED)
                {
                    return found;
                }
            }
            // Asked for enough to still have the limit once pinned ones are dropped.
            uint wanted = (uint)Math.Min(limit + shown.Count, 100);
            if (lists->GetList(type, wanted, __uuidof<IObjectArray>(), (void**)&array).FAILED)
            {
                return found;
            }
            uint count = 0;
            if (array->GetCount(&count).FAILED)
            {
                count = 0;
            }
            for (uint index = 0; index < count && found.Count < limit; index++)
            {
                IUnknown* unknown = null;
                if (array->GetAt(index, __uuidof<IUnknown>(), (void**)&unknown).FAILED)
                {
                    continue;
                }
                try
                {
                    if (PlaceOf(unknown, iconSize) is { } place && shown.Add(place.Path))
                    {
                        found.Add(opener.Entry(place));
                    }
                }
                finally
                {
                    unknown->Release();
                }
            }
        }
        finally
        {
            ComUtil.Release(ref array);
            ComUtil.Release(ref lists);
        }
        return found;
    }

    /// <summary>The pinned places, in the order the user pinned them.</summary>
    private static List<JumpEntry> Pinned(string file, int limit, int iconSize, Opener opener, HashSet<string> shown)
    {
        var found = new List<JumpEntry>();
        var bytes = ReadShared(file);
        if (bytes is null)
        {
            return found;
        }
        IStorage* storage = OpenStorage(bytes);
        if (storage == null)
        {
            return found;
        }
        try
        {
            var list = ReadStream(storage, "DestList");
            if (list is null)
            {
                return found;
            }
            foreach (var number in PinnedNumbers(list))
            {
                if (found.Count >= limit)
                {
                    break;
                }
                var link = LoadLink(storage, number.ToString("x"));
                if (link == null)
                {
                    continue;
                }
                try
                {
                    if (PlaceOf((IUnknown*)link, iconSize) is { } place && shown.Add(place.Path))
                    {
                        found.Add(opener.Entry(place));
                    }
                }
                finally
                {
                    link->Release();
                }
            }
        }
        finally
        {
            storage->Release();
        }
        return found;
    }

    /// <summary>
    /// The stream numbers of the pinned entries of a DestList stream, by pin position. The
    /// stream is a 32-byte header (format version, entry count, pinned count, ...) and one record
    /// per entry: its stream number at 88, its pin position at 108 (-1 when not pinned) and its
    /// path's length in characters at 112 in format 1 (Windows 7 and 8) or at 128 from format 3
    /// on, where four more bytes follow the path. Format 6, which Windows 11 writes, keeps that
    /// layout: checked against every file of a real profile, each walking to its exact end.
    /// </summary>
    internal static List<uint> PinnedNumbers(ReadOnlySpan<byte> list)
    {
        var pinned = new List<(int Position, uint Number)>();
        if (list.Length < 32)
        {
            return [];
        }
        uint version = U32(list, 0);
        // A format past these may have moved things; the walk's bounds
        // checks keep a wrong guess from reading past the stream.
        bool newer = version is >= 3 and <= 16;
        if (version != 1 && !newer)
        {
            return [];
        }
        int count = (int)Math.Min(U32(list, 4), 100_000);
        int fixedSize = newer ? 130 : 114;
        int offset = 32;
        for (int i = 0; i < count; i++)
        {
            if (offset + fixedSize > list.Length)
            {
                break;
            }
            uint number = U32(list, offset + 88);
            int position = (int)U32(list, offset + 108);
            int chars = U16(list, offset + (newer ? 128 : 112));
            int size = fixedSize + chars * 2 + (newer ? 4 : 0);
            if (offset + size > list.Length)
            {
                break;
            }
            if (position >= 0)
            {
                pinned.Add((position, number));
            }
            offset += size;
        }
        pinned.Sort();
        return pinned.ConvertAll(p => p.Number);
    }

    internal sealed record Part(uint Kind, string Title, uint Known, List<JumpEntry> Entries);

    /// <summary>
    /// The categories of a CustomDestinations file, in file order; null when the app has none.
    /// The file is a header (version 2, category count, reserved), then per category its kind:
    /// a custom one has a counted UTF-16 title and a counted list of persisted objects, a known
    /// one the KNOWNDESTCATEGORY, tasks a counted list; each category ends in 0xBABFFBAB. A
    /// persisted object is its CLSID and whatever its IPersistStream wrote, which only the
    /// object itself knows the length of — so each is loaded to find where the next begins.
    /// <paramref name="appId"/> is the app whose list it is, whose package an item's picture is in.
    /// </summary>
    internal static List<Part>? Custom(string file, int limit, int iconSize, Opener opener, string? appId)
    {
        var bytes = ReadShared(file);
        if (bytes is null)
        {
            return null;
        }
        var parts = new List<Part>();
        IStream* stream;
        fixed (byte* data = bytes)
        {
            stream = SHCreateMemStream(data, (uint)bytes.Length);
        }
        if (stream == null)
        {
            return parts;
        }
        try
        {
            if (!ReadU32(stream, out var version) || version != 2
                || !ReadU32(stream, out var count)
                || !ReadU32(stream, out _))
            {
                return null;
            }
            for (uint category = 0; category < count; category++)
            {
                if (!ReadU32(stream, out var kind))
                {
                    break;
                }
                string title = "";
                uint known = 0;
                var entries = new List<JumpEntry>();
                if (kind == CustomKind)
                {
                    if (!ReadU16(stream, out var chars) || !ReadText(stream, chars, out title)
                        || !ReadU32(stream, out var items) || !ReadObjects(stream, items, false, entries, limit, iconSize, opener, appId))
                    {
                        break;
                    }
                    title = Indirect(title) ?? title;
                }
                else if (kind == KnownKind)
                {
                    if (!ReadU32(stream, out known))
                    {
                        break;
                    }
                }
                else if (kind == TasksKind)
                {
                    if (!ReadU32(stream, out var items) || !ReadObjects(stream, items, true, entries, int.MaxValue, iconSize, opener, appId))
                    {
                        break;
                    }
                }
                else
                {
                    break;
                }
                parts.Add(new Part(kind, title, known, entries));
                if (!ReadU32(stream, out var end) || end != CategoryEnd)
                {
                    break;
                }
            }
        }
        finally
        {
            stream->Release();
        }
        return parts;
    }

    private static bool ReadObjects(
        IStream* stream,
        uint count,
        bool tasks,
        List<JumpEntry> entries,
        int limit,
        int iconSize,
        Opener opener,
        string? appId)
    {
        for (uint index = 0; index < count; index++)
        {
            Guid clsid;
            if (ReadClassStm(stream, &clsid).FAILED)
            {
                return false;
            }
            IPersistStream* persist = null;
            if (CoCreateInstance(
                    &clsid,
                    null,
                    (uint)CLSCTX.CLSCTX_INPROC_SERVER,
                    __uuidof<IPersistStream>(),
                    (void**)&persist).FAILED)
            {
                return false;
            }
            try
            {
                if (persist->Load(stream).FAILED)
                {
                    return false;
                }
                if (entries.Count < limit && Entry((IUnknown*)persist, tasks, iconSize, opener, appId) is { } entry)
                {
                    entries.Add(entry);
                }
            }
            finally
            {
                persist->Release();
            }
        }
        return true;
    }

    /// <summary>
    /// A task or a custom destination: a link that carries its own command line, a place, or a
    /// packaged app's own activation. A packaged app writes its list through
    /// Windows.UI.StartScreen.JumpList, and Windows stores each line as a link whose path is a
    /// made-up one (the Desktop folder and the AUMID) and whose name is a BSTR, carrying the app's
    /// AUMID, the arguments it is activated with and the picture it named for the line
    /// (System.AppUserModel.ID, .ActivationContext, .DestListLogoUri). Read as an ordinary link, every
    /// such line was named after that path — the AUMID, seven times over for WhatsApp — and opened nothing.
    /// </summary>
    private static JumpEntry? Entry(IUnknown* unknown, bool task, int iconSize, Opener opener, string? listApp)
    {
        IShellLinkW* link = null;
        if (unknown->QueryInterface(__uuidof<IShellLinkW>(), (void**)&link).FAILED)
        {
            if (PlaceOf(unknown, iconSize) is not { } place)
            {
                return null;
            }
            var entry = opener.Entry(place);
            return task ? entry with { IsTask = true } : entry;
        }
        try
        {
            IPropertyStore* store = null;
            string? title = null;
            string? app = null;
            string? context = null;
            string? logo = null;
            if (unknown->QueryInterface(__uuidof<IPropertyStore>(), (void**)&store).SUCCEEDED)
            {
                try
                {
                    // The rules between groups of tasks: nothing to list.
                    if (BoolProperty(store, PKEY.PKEY_AppUserModel_IsDestListSeparator))
                    {
                        return null;
                    }
                    title = StringProperty(store, PKEY.PKEY_Title) ?? StringProperty(store, ProvidedTitleKey);
                    app = StringProperty(store, PKEY.PKEY_AppUserModel_ID);
                    context = StringProperty(store, ActivationContextKey);
                    logo = StringProperty(store, LogoKey);
                }
                finally
                {
                    store->Release();
                }
            }
            if (app is not null && context is not null && WindowsActivationPlatform.PackageFamily(app) is { } family)
            {
                // A packaged app's own line: nothing but the app to start, with its arguments.
                var name = title is not null && Indirect(title) is { } plain ? PackageString(plain, family) : null;
                if (string.IsNullOrEmpty(name))
                {
                    return null;
                }
                // No picture of its own: the menu shows the app's, as it does for a task.
                var picture = logo is null ? null : LogoIcon(logo, app, iconSize);
                return new JumpEntry(name, $@"shell:AppsFolder\{app}", context, null, picture, task, app);
            }
            var path = new char[PathChars];
            var arguments = new char[ArgumentChars];
            var directory = new char[PathChars];
            string? target = null;
            string? args = null;
            string? folder = null;
            fixed (char* p = path)
            fixed (char* a = arguments)
            fixed (char* d = directory)
            {
                if (link->GetPath(p, PathChars, null, 0).SUCCEEDED && p[0] != '\0')
                {
                    target = new string(p);
                }
                if (link->GetArguments(a, ArgumentChars).SUCCEEDED && a[0] != '\0')
                {
                    args = new string(a);
                }
                if (link->GetWorkingDirectory(d, PathChars).SUCCEEDED && d[0] != '\0')
                {
                    folder = new string(d);
                }
                if (title is null && link->GetDescription(p, PathChars).SUCCEEDED && p[0] != '\0')
                {
                    title = new string(p);
                }
            }
            var linked = LinkedItem(link);
            try
            {
                if (target is null && linked != null)
                {
                    target = ParsingName(linked);
                }
                title = title is null ? null : Indirect(title);
                if (title is null && linked != null)
                {
                    title = DisplayName(linked);
                }
                if (string.IsNullOrEmpty(title) || target is null)
                {
                    return null;
                }
                // A picture named by address (Windows Terminal's profiles: ms-appx:///ProfileIcons/…)
                // is in the package of the app the list is for, or of the one the line names.
                var icon = LinkIcon(link, iconSize)
                    ?? (logo is null ? null : LogoIcon(logo, app ?? listApp, iconSize))
                    ?? (linked != null ? ItemIcon(linked, iconSize) : null);
                return new JumpEntry(title, target, args, folder, icon, task);
            }
            finally
            {
                if (linked != null)
                {
                    linked->Release();
                }
            }
        }
        finally
        {
            link->Release();
        }
    }

    /// <summary>A file, folder or other shell place, from an item or a link to one.</summary>
    internal readonly record struct Place(string Name, string Path, Pixels? Icon);

    private static Place? PlaceOf(IUnknown* unknown, int iconSize)
    {
        IShellItem* item = null;
        if (unknown->QueryInterface(__uuidof<IShellItem>(), (void**)&item).FAILED)
        {
            IShellLinkW* link = null;
            if (unknown->QueryInterface(__uuidof<IShellLinkW>(), (void**)&link).FAILED)
            {
                return null;
            }
            item = LinkedItem(link);
            link->Release();
            if (item == null)
            {
                return null;
            }
        }
        try
        {
            var name = DisplayName(item);
            var path = ParsingName(item);
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(path))
            {
                return null;
            }
            return new Place(name, path, ItemIcon(item, iconSize));
        }
        finally
        {
            item->Release();
        }
    }

    /// <summary>
    /// How this app opens a place. The taskbar opens a document from an app's list in that app,
    /// not in whatever owns the file type, so a desktop app gets the path on its command line.
    /// Explorer is the shell's own handler anyway, and a packaged app cannot be started from its
    /// executable, so those two open places the default way.
    /// </summary>
    internal readonly record struct Opener(string? Exe)
    {
        public static Opener For(string? aumid, string? exePath)
        {
            if (string.IsNullOrEmpty(exePath) || (aumid?.Contains('!') ?? false)
                || exePath.Contains(@"\WindowsApps\", StringComparison.OrdinalIgnoreCase)
                || System.IO.Path.GetFileName(exePath).Equals("explorer.exe", StringComparison.OrdinalIgnoreCase)
                || !File.Exists(exePath))
            {
                return default;
            }
            return new Opener(exePath);
        }

        public JumpEntry Entry(Place place)
        {
            if (Exe is null || !IsFileSystem(place.Path))
            {
                return new JumpEntry(place.Name, place.Path, null, null, place.Icon, false);
            }
            // A trailing backslash would escape the closing quote.
            var quoted = "\"" + (place.Path.EndsWith('\\') ? place.Path + "\\" : place.Path) + "\"";
            var folder = System.IO.Path.GetDirectoryName(place.Path);
            return new JumpEntry(place.Name, Exe, quoted, folder, place.Icon, false);
        }

        private static bool IsFileSystem(string path) =>
            (path.Length >= 3 && char.IsAsciiLetter(path[0]) && path[1] == ':' && path[2] == '\\')
            || path.StartsWith(@"\\", StringComparison.Ordinal);
    }

    private static IShellItem* LinkedItem(IShellLinkW* link)
    {
        ITEMIDLIST* pidl = null;
        IShellItem* item = null;
        try
        {
            if (link->GetIDList(&pidl).FAILED || pidl == null
                || SHCreateItemFromIDList(pidl, __uuidof<IShellItem>(), (void**)&item).FAILED)
            {
                return null;
            }
            return item;
        }
        finally
        {
            CoTaskMemFree(pidl);
        }
    }

    private static string? DisplayName(IShellItem* item)
    {
        char* raw = null;
        return item->GetDisplayName(SIGDN.SIGDN_NORMALDISPLAY, &raw).SUCCEEDED ? ComUtil.FromTaskMem(raw) : null;
    }

    private static string? ParsingName(IShellItem* item)
    {
        char* raw = null;
        return item->GetDisplayName(SIGDN.SIGDN_DESKTOPABSOLUTEPARSING, &raw).SUCCEEDED ? ComUtil.FromTaskMem(raw) : null;
    }

    /// <summary><c>@file.dll,-12</c> and <c>@{package?ms-resource://…}</c> strings, resolved; others as they are.</summary>
    private static string? Indirect(string text)
    {
        if (!text.StartsWith('@'))
        {
            return text;
        }
        var buffer = new char[PathChars];
        fixed (char* source = text)
        fixed (char* output = buffer)
        {
            // Unresolvable is nameless: a raw resource reference is no name to show.
            return SHLoadIndirectString(source, output, PathChars, null).SUCCEEDED ? new string(output) : null;
        }
    }

    /// <summary>
    /// A packaged app's name for a line: an <c>ms-resource:</c> reference looked up in the app's
    /// package <paramref name="family"/>, as the taskbar does; null where it can't be; anything else
    /// as it is.
    /// </summary>
    private static string? PackageString(string text, string family)
    {
        if (PackageResource(text, family) is not { } resource)
        {
            return text;
        }
        return PackageFullName(family) is { } full ? Indirect($"@{{{full}?{resource}}}") : null;
    }

    /// <summary>
    /// <c>ms-resource:Name</c>, <c>ms-resource:///Resources/Name</c> and
    /// <c>ms-resource://package/…</c>, as the full reference in the package of
    /// <paramref name="family"/>; null for text that is no such reference.
    /// </summary>
    internal static string? PackageResource(string text, string family)
    {
        const string Scheme = "ms-resource:";
        if (!text.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        var rest = text[Scheme.Length..];
        if (rest.StartsWith("//", StringComparison.Ordinal) && !rest.StartsWith("///", StringComparison.Ordinal))
        {
            return text;
        }
        var path = rest.TrimStart('/');
        // A bare name is a string of the package's Resources.
        if (!path.Contains('/'))
        {
            path = "Resources/" + path;
        }
        return $"ms-resource://{PackageName(family)}/{path}";
    }

    /// <summary>The name a package family starts with: <c>5319275A.WhatsAppDesktop</c> of <c>5319275A.WhatsAppDesktop_cv1g1gvanyjgm</c>.</summary>
    private static string PackageName(string family)
    {
        var cut = family.LastIndexOf('_');
        return cut > 0 ? family[..cut] : family;
    }

    /// <summary>The full name of the package of <paramref name="family"/> installed on this PC, or null.</summary>
    private static string? PackageFullName(string family)
    {
        uint count = 0;
        uint length = 0;
        fixed (char* name = family)
        {
            // Asked once for the sizes (ERROR_INSUFFICIENT_BUFFER), then for the names.
            _ = GetPackagesByPackageFamily(name, &count, null, &length, null);
            if (count == 0 || length == 0)
            {
                return null;
            }
            var names = stackalloc char*[(int)count];
            var buffer = new char[length];
            fixed (char* text = buffer)
            {
                return GetPackagesByPackageFamily(name, &count, names, &length, text) == 0 && count > 0 ? new string(names[0]) : null;
            }
        }
    }

    /// <summary>The picture a line names by address (<see cref="LogoPath"/>), fitted into <paramref name="size"/> px.</summary>
    private static Pixels? LogoIcon(string logo, string? appId, int size) =>
        LogoPath(logo, appId) is { } path ? NotificationPartTile.LoadImage(path, size) : null;

    /// <summary>
    /// The file a line's picture is: <c>ms-appx:///…</c> in the package of <paramref name="appId"/>,
    /// the scale or contrast variant its resources pick (WhatsApp has only NewChat.scale-200.png for
    /// NewChat.png); <c>ms-appdata:///local/…</c> in that package's data; or a path on this PC.
    /// Null where there is no such file.
    /// </summary>
    private static string? LogoPath(string logo, string? appId)
    {
        var family = appId is null ? null : WindowsActivationPlatform.PackageFamily(appId);
        string? path;
        if (logo.StartsWith("ms-appx:///", StringComparison.OrdinalIgnoreCase))
        {
            if (family is null || PackageFile(logo["ms-appx:///".Length..]) is not { } relative || PackageFullName(family) is not { } full)
            {
                return null;
            }
            path = Indirect($"@{{{full}?ms-resource://{PackageName(family)}/Files/{relative}}}");
        }
        else if (logo.StartsWith("ms-appdata:///", StringComparison.OrdinalIgnoreCase))
        {
            path = family is null ? null : AppDataFile(logo["ms-appdata:///".Length..], family, Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        }
        else
        {
            path = System.IO.Path.IsPathFullyQualified(logo) ? logo : null;
        }
        return path is not null && File.Exists(path) ? path : null;
    }

    /// <summary>
    /// A package-relative address's path, unescaped, with forward slashes; null for one that climbs
    /// out of where it is (<c>..</c>) or is empty.
    /// </summary>
    internal static string? PackageFile(string address)
    {
        var path = Uri.UnescapeDataString(address).Replace('\\', '/').TrimStart('/');
        return path.Length == 0 || path.Split('/').Any(part => part is ".." or ".") ? null : path;
    }

    /// <summary>
    /// <c>local/…</c>, <c>roaming/…</c> or <c>temp/…</c> of an <c>ms-appdata:///</c> address, as the
    /// file in the data of the package of <paramref name="family"/> under
    /// <paramref name="localAppData"/>; null for any other folder or a path that climbs out of it.
    /// </summary>
    internal static string? AppDataFile(string address, string family, string localAppData)
    {
        if (PackageFile(address) is not { } path)
        {
            return null;
        }
        var cut = path.IndexOf('/');
        if (cut <= 0 || cut == path.Length - 1)
        {
            return null;
        }
        var folder = path[..cut].ToLowerInvariant() switch
        {
            "local" => "LocalState",
            "roaming" => "RoamingState",
            "temp" => "TempState",
            _ => null,
        };
        return folder is null ? null : System.IO.Path.Combine(localAppData, "Packages", family, folder, path[(cut + 1)..].Replace('/', '\\'));
    }

    private static string? StringProperty(IPropertyStore* store, PROPERTYKEY key)
    {
        PROPVARIANT value = default;
        if (store->GetValue(&key, &value).FAILED)
        {
            return null;
        }
        try
        {
            // A packaged app's line has its name as a BSTR.
            return value.vt == (ushort)VARENUM.VT_LPWSTR && value.pwszVal != null ? new string(value.pwszVal)
                : value.vt == (ushort)VARENUM.VT_BSTR && value.bstrVal != null ? new string(value.bstrVal)
                : null;
        }
        finally
        {
            _ = PropVariantClear(&value);
        }
    }

    private static bool BoolProperty(IPropertyStore* store, PROPERTYKEY key)
    {
        PROPVARIANT value = default;
        if (store->GetValue(&key, &value).FAILED)
        {
            return false;
        }
        try
        {
            return value.vt == (ushort)VARENUM.VT_BOOL && value.boolVal != 0;
        }
        finally
        {
            _ = PropVariantClear(&value);
        }
    }

    /// <summary>The icon a link names for itself (tasks usually do), when it names one.</summary>
    private static Pixels? LinkIcon(IShellLinkW* link, int size)
    {
        var buffer = new char[PathChars];
        var expanded = new char[PathChars];
        int index = 0;
        fixed (char* location = buffer)
        fixed (char* path = expanded)
        {
            if (link->GetIconLocation(location, PathChars, &index).FAILED || location[0] == '\0')
            {
                return null;
            }
            // Links written by apps often name their icon with %SystemRoot% and the like.
            if (ExpandEnvironmentStringsW(location, path, PathChars) is 0 or > PathChars)
            {
                return null;
            }
            HICON icon = HICON.NULL;
            // S_FALSE: the file has no icon at that index.
            if (SHDefExtractIconW(path, index, 0, &icon, null, (uint)size).Value != S_OK || icon == HICON.NULL)
            {
                return null;
            }
            try
            {
                return FromIcon(icon);
            }
            finally
            {
                _ = DestroyIcon(icon);
            }
        }
    }

    private static Pixels? ItemIcon(IShellItem* item, int size)
    {
        IShellItemImageFactory* factory = null;
        if (item->QueryInterface(__uuidof<IShellItemImageFactory>(), (void**)&factory).FAILED)
        {
            return null;
        }
        try
        {
            const int flags = (int)(SIIGBF.SIIGBF_ICONONLY | SIIGBF.SIIGBF_SCALEUP);
            HBITMAP bitmap = default;
            if (factory->GetImage(new SIZE { cx = size, cy = size }, flags, &bitmap).FAILED)
            {
                return null;
            }
            try
            {
                return FromBitmap(bitmap);
            }
            finally
            {
                _ = DeleteObject((HGDIOBJ)bitmap.Value);
            }
        }
        finally
        {
            factory->Release();
        }
    }

    /// <summary>As Items reads the image factory's bitmaps: top-down BGRA, premultiplied if it was not.</summary>
    private static Pixels? FromBitmap(HBITMAP bitmap)
    {
        BITMAP shape = default;
        if (GetObjectW((HANDLE)bitmap, sizeof(BITMAP), &shape) == 0 || shape.bmWidth <= 0 || shape.bmHeight == 0)
        {
            return null;
        }
        int width = shape.bmWidth;
        int height = Math.Abs(shape.bmHeight);
        var header = new BITMAPINFO
        {
            bmiHeader = new BITMAPINFOHEADER
            {
                biSize = (uint)sizeof(BITMAPINFOHEADER),
                biWidth = width,
                biHeight = -height,
                biPlanes = 1,
                biBitCount = 32,
                biCompression = (uint)BI.BI_RGB,
            },
        };
        var data = new byte[width * height * 4];
        var screen = GetDC(HWND.NULL);
        int rows;
        fixed (byte* bits = data)
        {
            rows = GetDIBits(screen, bitmap, 0, (uint)height, bits, &header, (uint)DIB_RGB_COLORS);
        }
        _ = ReleaseDC(HWND.NULL, screen);
        if (rows != height)
        {
            return null;
        }
        Items.Premultiply(data);
        return new Pixels { Data = data, Width = width, Height = height };
    }

    /// <summary>
    /// An icon handle as premultiplied pixels. WIC applies an old icon's mask and keeps a new
    /// one's alpha, which reading the icon's bitmaps by hand would have to tell apart.
    /// </summary>
    private static Pixels? FromIcon(HICON icon)
    {
        var clsid = CLSID.CLSID_WICImagingFactory2;
        IWICImagingFactory* wic = null;
        IWICBitmap* bitmap = null;
        IWICFormatConverter* converter = null;
        try
        {
            if (CoCreateInstance(&clsid, null, (uint)CLSCTX.CLSCTX_INPROC_SERVER, __uuidof<IWICImagingFactory>(), (void**)&wic).FAILED
                || wic->CreateBitmapFromHICON(icon, &bitmap).FAILED
                || wic->CreateFormatConverter(&converter).FAILED)
            {
                return null;
            }
            var format = GUID.GUID_WICPixelFormat32bppPBGRA;
            if (converter->Initialize(
                    (IWICBitmapSource*)bitmap,
                    &format,
                    WICBitmapDitherType.WICBitmapDitherTypeNone,
                    null,
                    0,
                    WICBitmapPaletteType.WICBitmapPaletteTypeCustom).FAILED)
            {
                return null;
            }
            uint width;
            uint height;
            if (converter->GetSize(&width, &height).FAILED || width == 0 || height == 0 || width > 1024 || height > 1024)
            {
                return null;
            }
            var data = new byte[width * height * 4];
            var rect = new WICRect { X = 0, Y = 0, Width = (int)width, Height = (int)height };
            fixed (byte* pixels = data)
            {
                if (converter->CopyPixels(&rect, width * 4, (uint)data.Length, pixels).FAILED)
                {
                    return null;
                }
            }
            return new Pixels { Data = data, Width = (int)width, Height = (int)height };
        }
        finally
        {
            ComUtil.Release(ref converter);
            ComUtil.Release(ref bitmap);
            ComUtil.Release(ref wic);
        }
    }

    /// <summary>The whole file, read while the shell may have it open; null if it is not there.</summary>
    private static byte[]? ReadShared(string file)
    {
        try
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length is 0 or > 64 * 1024 * 1024)
            {
                return null;
            }
            var bytes = new byte[stream.Length];
            stream.ReadExactly(bytes);
            return bytes;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>A compound file opened from a copy in memory; null if it is not one.</summary>
    private static IStorage* OpenStorage(byte[] bytes)
    {
        var memory = GlobalAlloc((uint)GMEM.GMEM_MOVEABLE, (nuint)bytes.Length);
        if (memory == HGLOBAL.NULL)
        {
            return null;
        }
        var target = GlobalLock(memory);
        if (target == null)
        {
            _ = GlobalFree(memory);
            return null;
        }
        bytes.AsSpan().CopyTo(new Span<byte>(target, bytes.Length));
        _ = GlobalUnlock(memory);
        ILockBytes* lockBytes = null;
        if (CreateILockBytesOnHGlobal(memory, BOOL.TRUE, &lockBytes).FAILED)
        {
            _ = GlobalFree(memory);
            return null;
        }
        IStorage* storage = null;
        try
        {
            return StgOpenStorageOnILockBytes(
                lockBytes,
                null,
                (uint)(STGM.STGM_READ | STGM.STGM_SHARE_EXCLUSIVE),
                null,
                0,
                &storage).SUCCEEDED
                ? storage
                : null;
        }
        finally
        {
            // The storage holds its own reference; the memory goes with the last one.
            lockBytes->Release();
        }
    }

    private static IStream* OpenStream(IStorage* storage, string name)
    {
        IStream* stream = null;
        fixed (char* text = name)
        {
            return storage->OpenStream(text, null, (uint)(STGM.STGM_READ | STGM.STGM_SHARE_EXCLUSIVE), 0, &stream).SUCCEEDED
                ? stream
                : null;
        }
    }

    private static byte[]? ReadStream(IStorage* storage, string name)
    {
        var stream = OpenStream(storage, name);
        if (stream == null)
        {
            return null;
        }
        try
        {
            STATSTG stat = default;
            if (stream->Stat(&stat, (uint)STATFLAG.STATFLAG_NONAME).FAILED || stat.cbSize.QuadPart > 64 * 1024 * 1024)
            {
                return null;
            }
            var bytes = new byte[(int)stat.cbSize.QuadPart];
            uint read = 0;
            fixed (byte* data = bytes)
            {
                if (bytes.Length > 0 && (stream->Read(data, (uint)bytes.Length, &read).FAILED || read != bytes.Length))
                {
                    return null;
                }
            }
            return bytes;
        }
        finally
        {
            stream->Release();
        }
    }

    /// <summary>The shell link kept in a numbered stream of an AutomaticDestinations file.</summary>
    private static IShellLinkW* LoadLink(IStorage* storage, string name)
    {
        var stream = OpenStream(storage, name);
        if (stream == null)
        {
            return null;
        }
        var clsid = CLSID.CLSID_ShellLink;
        IPersistStream* persist = null;
        try
        {
            if (CoCreateInstance(&clsid, null, (uint)CLSCTX.CLSCTX_INPROC_SERVER, __uuidof<IPersistStream>(), (void**)&persist).FAILED
                || persist->Load(stream).FAILED)
            {
                return null;
            }
            IShellLinkW* link = null;
            return persist->QueryInterface(__uuidof<IShellLinkW>(), (void**)&link).SUCCEEDED ? link : null;
        }
        finally
        {
            ComUtil.Release(ref persist);
            stream->Release();
        }
    }

    private static bool ReadU32(IStream* stream, out uint value)
    {
        uint result = 0;
        uint read = 0;
        bool ok = stream->Read(&result, sizeof(uint), &read).SUCCEEDED && read == sizeof(uint);
        value = result;
        return ok;
    }

    private static bool ReadU16(IStream* stream, out ushort value)
    {
        ushort result = 0;
        uint read = 0;
        bool ok = stream->Read(&result, sizeof(ushort), &read).SUCCEEDED && read == sizeof(ushort);
        value = result;
        return ok;
    }

    private static bool ReadText(IStream* stream, int chars, out string text)
    {
        text = "";
        if (chars == 0)
        {
            return true;
        }
        var buffer = new char[chars];
        uint read = 0;
        fixed (char* data = buffer)
        {
            if (stream->Read(data, (uint)(chars * sizeof(char)), &read).FAILED || read != chars * sizeof(char))
            {
                return false;
            }
        }
        text = new string(buffer);
        return true;
    }

    private static uint U32(ReadOnlySpan<byte> data, int offset) =>
        System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(data[offset..]);

    private static ushort U16(ReadOnlySpan<byte> data, int offset) =>
        System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(data[offset..]);
}
