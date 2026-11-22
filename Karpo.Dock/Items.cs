// What sits on the dock: resolving a pin to a name, an icon and the
// identities a running window can match, and opening it.

using Karpo.Core;
using Karpo.Dock.Native;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

public enum ItemKind
{
    /// <summary>
    /// Anything the shell can parse: files, folders, shortcuts, <c>shell:</c>
    /// paths, and apps by <c>shell:AppsFolder\&lt;AUMID&gt;</c>.
    /// </summary>
    Shell,

    /// <summary>
    /// A web address, or any other link the shell passes to whatever handles
    /// its scheme.
    /// </summary>
    Link,
}

public sealed record Item
{
    /// <summary>Exactly as written in the config: what gets opened.</summary>
    public required string Target { get; init; }

    public required string Name { get; init; }

    public required ItemKind Kind { get; init; }

    /// <summary>
    /// <c>aumid:</c> and <c>exe:</c> identities, lowercase. A window whose own identity
    /// is in here belongs to this item.
    /// </summary>
    public required List<string> Keys { get; init; }

    /// <summary>As registered, for asking Windows about the app's jump list.</summary>
    public string? Aumid { get; init; }

    // A running app's window, whose own icon is the last resort.
    /// <summary>For an open app: one of its windows, asked for its icon when nothing else has one.</summary>
    public HWND Window { get; init; }

    /// <summary>
    /// For a pin whose target this PC doesn't have, why (<see cref="PinTargets.Check"/>'s sentence):
    /// the bar draws it dimmed with a warning badge, and a click explains instead of opening. Null
    /// for everything that opens.
    /// </summary>
    public string? Broken { get; init; }

    /// <summary>
    /// A pin's own picture (<see cref="PinOptions.Icon"/>): a Segoe Fluent Icons glyph, or a file
    /// whose icon is drawn instead of the target's. Null for the target's own.
    /// </summary>
    public string? Picture { get; init; }

    /// <summary>Whether <see cref="Picture"/> is a glyph rather than a file.</summary>
    public bool PictureIsGlyph => Picture is { Length: 1 } glyph && glyph[0] is >= (char)0xE000 and <= (char)0xF8FF;

    /// <summary>
    /// Null for a target that is neither a link nor something the shell can
    /// find, so a stale entry costs a slot nobody sees rather than a blank icon.
    /// </summary>
    public static unsafe Item? Resolve(string target)
    {
        target = target.Trim();
        if (target.Length == 0)
        {
            return null;
        }
        // Before the shell: it can parse web addresses into the legacy
        // Internet namespace, which has neither a useful name nor an icon.
        if (Items.IsWeb(target))
        {
            return new Item { Target = target, Name = Items.Host(target), Kind = ItemKind.Link, Keys = [] };
        }
        var shell = Items.ShellItem(target);
        if (shell == null)
        {
            return Items.HasScheme(target)
                ? new Item { Target = target, Name = target, Kind = ItemKind.Link, Keys = [] }
                : null;
        }
        try
        {
            var name = Items.DisplayName(shell) ?? target;
            // §668: what the target names itself first, so a property the shell fails to read once costs no identity.
            var keys = Items.TargetKeys(target);
            // As the shell has it registered: Registered tells an entry the Apps folder really has by it.
            string? aumid = null;
            IShellItem2* rich = null;
            if (shell->QueryInterface(__uuidof<IShellItem2>(), (void**)&rich).SUCCEEDED)
            {
                try
                {
                    if (Items.StringProperty(rich, PKEY.PKEY_AppUserModel_ID) is { } registered)
                    {
                        if (!keys.Contains(Items.AumidKey(registered)))
                        {
                            keys.Add(Items.AumidKey(registered));
                        }
                        aumid = registered;
                    }
                    if (Items.StringProperty(rich, PKEY.PKEY_Link_TargetParsingPath) is { } linked
                        && Items.IsExe(linked) && !keys.Contains(Items.ExeKey(linked)))
                    {
                        keys.Add(Items.ExeKey(linked));
                    }
                }
                finally
                {
                    rich->Release();
                }
            }
            // A shortcut gets the ID the taskbar would give it, explicit or not,
            // which is what its app's windows resolve to as well.
            if (Items.IsShortcut(target))
            {
                using var resolver = new Resolver();
                if (resolver.Shortcut(shell) is { } id)
                {
                    var key = Items.AumidKey(id);
                    if (!keys.Contains(key))
                    {
                        keys.Add(key);
                    }
                    aumid ??= id;
                }
            }
            return new Item { Target = target, Name = name, Kind = ItemKind.Shell, Keys = keys, Aumid = aumid };
        }
        finally
        {
            shell->Release();
        }
    }

    /// <summary>
    /// An open app that isn't pinned. An app registered under its AUMID gets
    /// the name and icon the Start menu shows; anything else is named by its
    /// executable's description and drawn with its executable's icon, or with
    /// <paramref name="window"/>'s own when the executable has none.
    /// </summary>
    public static unsafe Item Running(string? aumid, string exe, HWND window = default)
    {
        var keys = new List<string> { Items.ExeKey(exe) };
        if (aumid != null)
        {
            keys.Add(Items.AumidKey(aumid));
            // Only an entry the Apps folder really has (see Registered).
            if (Registered(aumid) is { } found)
            {
                return found with { Keys = keys };
            }
        }
        string? described = null;
        var shell = Items.ShellItem(exe);
        if (shell != null)
        {
            IShellItem2* rich = null;
            try
            {
                if (shell->QueryInterface(__uuidof<IShellItem2>(), (void**)&rich).SUCCEEDED)
                {
                    described = Items.StringProperty(rich, PKEY.PKEY_FileDescription);
                }
            }
            finally
            {
                ComUtil.Release(ref rich);
                shell->Release();
            }
        }
        if (string.IsNullOrWhiteSpace(described))
        {
            described = null;
        }
        var name = described ?? Items.FileStem(exe) ?? exe;
        return new Item { Target = exe, Name = name, Kind = ItemKind.Shell, Keys = keys, Aumid = aumid, Window = window };
    }

    // apps-taskbar
    /// <summary>
    /// The Apps folder's entry for <paramref name="aumid"/>, when it really has
    /// one: the shell parses it, the entry carries that ID, and it has a picture.
    /// For an unpackaged app that sets no ID of its own, the ID the shell derives
    /// is often just its executable's path. The Apps folder can't parse that, and
    /// Resolve then takes "shell:AppsFolder\C:\..." for a link: a tile named
    /// "shell:..." with no icon, drawn as an "S".
    /// </summary>
    private static Item? Registered(string aumid)
    {
        var target = $@"shell:AppsFolder\{aumid}";
        return Resolve(target) is { Kind: ItemKind.Shell } found
            && string.Equals(found.Aumid, aumid, StringComparison.OrdinalIgnoreCase)
            && Items.Icon(target, 16) is not null
                ? found
                : null;
    }

    public bool Matches(string key) => Keys.Contains(key);

    /// <summary>
    /// Opens the item the way Explorer would: a folder in Explorer, a file in
    /// its default program, a link in the default browser. A broken target gets
    /// the shell's own error dialog, which says more than a dock could.
    /// </summary>
    public void Open() => Launch.Open(Target);

    /// <summary>
    /// The item's icon, fitted into <paramref name="size"/> px. Null for a link with no picture of
    /// its own, which is drawn as a letter tile instead.
    /// </summary>
    public Pixels? Icon(int size) => Icon(size, Items.Icon);

    /// <summary>
    /// The item's icon as <see cref="Icon(int)"/> draws it, every file's asked of <paramref name="read"/>
    /// — the one precedence for every drawing of a pin (§319): its own picture (a glyph, which draws
    /// itself: null here; a file's icon), else its target's own. <paramref name="own"/>, where given,
    /// is where the target's own is read instead of the target: a folder pin's folder, read through
    /// the folder source as its contents are (FolderContentsView).
    /// </summary>
    internal Pixels? Icon(int size, Func<string, int, Pixels?> read, string? own = null) => Picture is { } picture && !PictureIsGlyph
        // A pin's own picture: the icon the shell has for that file.
        ? read(picture, size) ?? (own is not null ? read(own, size) : Kind == ItemKind.Shell ? read(Target, size) : null)
        : PictureIsGlyph ? null
        : own is not null ? read(own, size)
        : Kind switch
        {
            // The window's own icon when the target has none.
            ItemKind.Shell => read(Target, size) ?? Items.WindowIcon(Window, size),
            // A web address: the site's own favicon, when one has been fetched and kept
            // (Favicons\FaviconStore.cs). Null until then, null if the site has none, and null
            // where there is no dock to have started the store — which is what leaves the letter
            // tile in place. A pin never blanks waiting for a fetch, and its size does not depend
            // on whether one has arrived.
            ItemKind.Link => Favicons.FaviconStore.Current?.Picture(Target, size),
            _ => null,
        };

    /// <summary>
    /// What the icon caches key this item's picture by: its own picture when it has one, else its
    /// target; a broken pin's is marked apart, so a target that comes back draws afresh.
    /// </summary>
    public string PictureKey => (Picture ?? Target) + (Broken is null ? "" : "\n(broken)");
}

/// <summary>Premultiplied BGRA, top row first: what Direct2D takes without converting.</summary>
public sealed class Pixels
{
    public required byte[] Data;
    public required int Width;
    public required int Height;

    /// <summary>A white glyph, its shape alone meant: drawn in the text colour where it is drawn (NotificationApps).</summary>
    public bool Mask;
}

public static unsafe class Items
{
    public static string AumidKey(string aumid) => $"aumid:{aumid.ToLowerInvariant()}";

    public static string ExeKey(string path) => $"exe:{path.ToLowerInvariant()}";

    private const string AppsFolderPrefix = @"shell:AppsFolder\";

    /// <summary>
    /// §668: the identities <paramref name="target"/> names by itself, the shell not asked: an app of the Apps
    /// folder its app id — what its windows carry, wherever an update moved its program —, a program its path;
    /// none for the rest (a shortcut's app is known only by asking). So a pin holds its app's windows even when
    /// the shell has no answer for it at that moment.
    /// </summary>
    public static List<string> TargetKeys(string target) =>
        AppIdOf(target) is { } id ? [AumidKey(id)]
        : IsExe(target.Trim()) ? [ExeKey(target.Trim())]
        : [];

    /// <summary>The app id an Apps folder target (<c>shell:AppsFolder\&lt;id&gt;</c>) names; null for any other target.</summary>
    public static string? AppIdOf(string target)
    {
        var text = target.Trim();
        return text.Length > AppsFolderPrefix.Length && text.StartsWith(AppsFolderPrefix, StringComparison.OrdinalIgnoreCase)
            ? text[AppsFolderPrefix.Length..]
            : null;
    }

    /// <summary>
    /// The shortcuts pinned to the Windows taskbar, for a first run. Store apps
    /// pinned there have no shortcut and are left out.
    /// </summary>
    public static List<string> TaskbarPins()
    {
        var appdata = Environment.GetEnvironmentVariable("APPDATA") ?? "";
        var folder = Path.Combine(appdata, @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar"); // not-ui: a path
        var links = new List<string>();
        // A folder that is missing or unreadable means no pins, not a failure.
        try
        {
            foreach (var path in Directory.EnumerateFileSystemEntries(folder))
            {
                if (Extension(path) is { } extension && AsciiLower(extension) == "lnk")
                {
                    links.Add(path);
                }
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
        }
        links.Sort(string.CompareOrdinal);
        if (links.Count == 0)
        {
            var windows = Environment.GetEnvironmentVariable("WINDIR") ?? @"C:\Windows";
            links =
            [
                Path.Combine(windows, "explorer.exe"),
                Path.Combine(windows, @"System32\notepad.exe"),
                Path.Combine(windows, @"System32\Taskmgr.exe"),
            ];
        }
        return links;
    }

    public static bool IsWeb(string target)
    {
        var lower = AsciiLower(target);
        return lower.StartsWith("http://", StringComparison.Ordinal)
            || lower.StartsWith("https://", StringComparison.Ordinal);
    }

    /// <summary><c>scheme:rest</c> with a scheme of two or more characters, so <c>C:\</c> is a path.</summary>
    internal static bool HasScheme(string target)
    {
        int colon = target.IndexOf(':');
        if (colon < 0)
        {
            return false;
        }
        var scheme = target.AsSpan(0, colon);
        if (scheme.Length <= 1 || !char.IsAsciiLetter(scheme[0]))
        {
            return false;
        }
        foreach (var c in scheme)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c is not ('+' or '.' or '-'))
            {
                return false;
            }
        }
        return true;
    }

    internal static bool IsExe(string path) => AsciiLower(path).EndsWith(".exe", StringComparison.Ordinal);

    internal static bool IsShortcut(string path) => AsciiLower(path).EndsWith(".lnk", StringComparison.Ordinal);

    /// <summary><c>https://www.example.com:8080/a?b</c> → <c>example.com</c>.</summary>
    public static string Host(string url)
    {
        int scheme = url.IndexOf("://", StringComparison.Ordinal);
        var rest = scheme < 0 ? url : url[(scheme + 3)..];
        int end = rest.AsSpan().IndexOfAny('/', '?', '#');
        var authority = end < 0 ? rest : rest[..end];
        var host = authority[(authority.LastIndexOf('@') + 1)..];
        int port = host.IndexOf(':');
        if (port >= 0)
        {
            host = host[..port];
        }
        if (host.StartsWith("www.", StringComparison.Ordinal))
        {
            host = host[4..];
        }
        return host.Length == 0 ? url : host;
    }

    /// <summary>
    /// The target's icon, fitted into <paramref name="size"/> px. The shell picks the closest image
    /// it has and scales that, which beats scaling a small one ourselves.
    /// </summary>
    public static Pixels? Icon(string target, int size)
    {
        var shell = ShellItem(target);
        if (shell == null)
        {
            return null;
        }
        IShellItemImageFactory* factory = null;
        try
        {
            if (shell->QueryInterface(__uuidof<IShellItemImageFactory>(), (void**)&factory).FAILED)
            {
                return null;
            }
            const int flags = (int)(SIIGBF.SIIGBF_ICONONLY | SIIGBF.SIIGBF_SCALEUP);
            HBITMAP bitmap = default;
            if (factory->GetImage(new SIZE { cx = size, cy = size }, flags, &bitmap).FAILED)
            {
                return null;
            }
            try
            {
                return Read(bitmap);
            }
            finally
            {
                _ = DeleteObject((HGDIOBJ)bitmap.Value);
            }
        }
        finally
        {
            ComUtil.Release(ref factory);
            shell->Release();
        }
    }

    /// <summary>
    /// §498: the shell's thumbnail of <paramref name="path"/> — a picture's own, a video's frame — fitted into
    /// <paramref name="size"/> px in its own shape, as File Explorer's large icons show it; null where the shell has none
    /// (it never falls back to the icon here: the caller has that). Slow for a picture never shown before, so only ever
    /// off the dock's thread.
    /// </summary>
    public static Pixels? Thumbnail(string path, int size)
    {
        var shell = ShellItem(path);
        if (shell == null)
        {
            return null;
        }
        IShellItemImageFactory* factory = null;
        try
        {
            if (shell->QueryInterface(__uuidof<IShellItemImageFactory>(), (void**)&factory).FAILED)
            {
                return null;
            }
            const int flags = (int)SIIGBF.SIIGBF_THUMBNAILONLY;
            HBITMAP bitmap = default;
            if (factory->GetImage(new SIZE { cx = size, cy = size }, flags, &bitmap).FAILED)
            {
                return null;
            }
            try
            {
                return Read(bitmap);
            }
            finally
            {
                _ = DeleteObject((HGDIOBJ)bitmap.Value);
            }
        }
        finally
        {
            ComUtil.Release(ref factory);
            shell->Release();
        }
    }

    /// <summary>
    /// §652: the icon Windows gives a kind of file — <paramref name="kind"/> its extension (".xlsx") — or a folder ("\"),
    /// fitted into <paramref name="size"/> px: the shell's own image list's, asked by the kind alone
    /// (SHGFI_USEFILEATTRIBUTES), so no file is read and none needs to be there — a sample's. Null where the shell has none.
    /// </summary>
    public static Pixels? KindIcon(string kind, int size)
    {
        // commctrl.h's ILD_TRANSPARENT, which TerraFX does not carry: the icon drawn on its own alpha.
        const uint IldTransparent = 0x1;
        var folder = kind == "\\";
        var name = folder ? "folder" : "file" + kind; // not-ui: a name the shell reads the kind of
        SHFILEINFOW info = default;
        fixed (char* path = name)
        {
            if (SHGetFileInfoW(path, (uint)(folder ? FILE.FILE_ATTRIBUTE_DIRECTORY : FILE.FILE_ATTRIBUTE_NORMAL), &info, (uint)sizeof(SHFILEINFOW), SHGFI.SHGFI_SYSICONINDEX | SHGFI.SHGFI_USEFILEATTRIBUTES) == 0)
            {
                return null;
            }
        }
        IImageList* list = null;
        if (SHGetImageList(size <= 16 ? SHIL_SMALL : size <= 32 ? SHIL_LARGE : SHIL_EXTRALARGE, __uuidof<IImageList>(), (void**)&list).FAILED || list == null)
        {
            return null;
        }
        try
        {
            HICON icon = default;
            if (list->GetIcon(info.iIcon, IldTransparent, &icon).FAILED || icon == HICON.NULL)
            {
                return null;
            }
            try
            {
                return Tray.Render(icon, size);
            }
            finally
            {
                _ = DestroyIcon(icon);
            }
        }
        finally
        {
            _ = list->Release();
        }
    }

    /// <summary>
    /// §498: what File Explorer's Type column calls a file named <paramref name="name"/> ("Microsoft Excel Worksheet",
    /// "PNG File"), asked of its kind alone — no disk is touched; null where the shell says nothing.
    /// </summary>
    public static string? TypeName(string name)
    {
        SHFILEINFOW info = default;
        fixed (char* path = name)
        {
            if (SHGetFileInfoW(path, FILE.FILE_ATTRIBUTE_NORMAL, &info, (uint)sizeof(SHFILEINFOW), SHGFI.SHGFI_TYPENAME | SHGFI.SHGFI_USEFILEATTRIBUTES) == 0)
            {
                return null;
            }
        }
        ReadOnlySpan<char> said = info.szTypeName;
        var end = said.IndexOf('\0');
        var text = (end >= 0 ? said[..end] : said).ToString().Trim();
        return text.Length > 0 ? text : null;
    }

    /// <summary>
    /// The image factory does not document whether its alpha is premultiplied.
    /// Premultiplied data never has a colour channel above its alpha, so a single
    /// such pixel settles it. No alpha at all means an old opaque icon, which
    /// premultiplying would turn invisible.
    /// </summary>
    public static void Premultiply(Span<byte> data)
    {
        // Whole pixels only: a trailing partial one is left alone.
        int end = data.Length - data.Length % 4;
        bool anyAlpha = false;
        for (int i = 0; i < end; i += 4)
        {
            if (data[i + 3] != 0)
            {
                anyAlpha = true;
                break;
            }
        }
        if (!anyAlpha)
        {
            for (int i = 0; i < end; i += 4)
            {
                data[i + 3] = 255;
            }
            return;
        }
        bool straight = false;
        for (int i = 0; i < end; i += 4)
        {
            byte alpha = data[i + 3];
            if (data[i] > alpha || data[i + 1] > alpha || data[i + 2] > alpha)
            {
                straight = true;
                break;
            }
        }
        if (!straight)
        {
            return;
        }
        for (int i = 0; i < end; i += 4)
        {
            int alpha = data[i + 3];
            for (int channel = i; channel < i + 3; channel++)
            {
                data[channel] = (byte)((data[channel] * alpha + 127) / 255);
            }
        }
    }

    // apps-taskbar
    /// <summary>A window's own icon, drawn at <paramref name="size"/> px; null without a live window or an icon.</summary>
    internal static Pixels? WindowIcon(HWND window, int size)
    {
        if (window == HWND.NULL || !IsWindow(window))
        {
            return null;
        }
        var icon = Apps.WindowIcon(window, small: size <= 16);
        return icon == HICON.NULL ? null : Tray.Render(icon, size);
    }

    /// <summary>The shell item for <paramref name="target"/>, owned by the caller; null if the shell cannot parse it.</summary>
    internal static IShellItem* ShellItem(string target)
    {
        IShellItem* item = null;
        fixed (char* path = target)
        {
            return SHCreateItemFromParsingName(path, null, __uuidof<IShellItem>(), (void**)&item).SUCCEEDED
                ? item
                : null;
        }
    }

    internal static string? DisplayName(IShellItem* shell)
    {
        char* raw = null;
        return shell->GetDisplayName(SIGDN.SIGDN_NORMALDISPLAY, &raw).SUCCEEDED
            ? ComUtil.FromTaskMem(raw)
            : null;
    }

    internal static string? StringProperty(IShellItem2* shell, PROPERTYKEY key)
    {
        char* raw = null;
        return shell->GetString(&key, &raw).SUCCEEDED ? ComUtil.FromTaskMem(raw) : null;
    }

    /// <summary>
    /// Rust's <c>Path::file_stem</c>: the file name up to its last dot, unless
    /// that dot leads the name, and nothing for a path without a file name.
    /// </summary>
    internal static string? FileStem(string path)
    {
        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
        if (name.Length == 0 || name is "." or "..")
        {
            return null;
        }
        int dot = name.LastIndexOf('.');
        return dot > 0 ? name[..dot] : name;
    }

    /// <summary>Rust's <c>Path::extension</c>: none when the only dot leads the name, as in <c>.lnk</c>.</summary>
    private static string? Extension(string path)
    {
        var name = Path.GetFileName(path);
        int dot = name.LastIndexOf('.');
        return dot > 0 ? name[(dot + 1)..] : null;
    }

    /// <summary>
    /// Rust's <c>to_ascii_lowercase</c>: only A-Z fold, so no other letter can
    /// pass for one in <c>http</c> or <c>.exe</c> the way full case folding
    /// would let it.
    /// </summary>
    private static string AsciiLower(string text) =>
        string.Create(text.Length, text, static (lower, source) =>
        {
            for (int i = 0; i < source.Length; i++)
            {
                char c = source[i];
                lower[i] = char.IsAsciiLetterUpper(c) ? (char)(c | 0x20) : c;
            }
        });

    private static Pixels? Read(HBITMAP bitmap)
    {
        BITMAP shape = default;
        int wrote = GetObjectW((HANDLE)bitmap, sizeof(BITMAP), &shape);
        if (wrote == 0 || shape.bmWidth <= 0 || shape.bmHeight == 0)
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
                // Negative asks for top-down rows whichever way the bitmap is stored.
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
        Premultiply(data);
        return new Pixels { Data = data, Width = width, Height = height };
    }
}
