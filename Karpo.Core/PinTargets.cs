// Pins and apps a config names that may not exist on this PC: files and folders
// that aren't there, shortcuts that point at nothing, apps that aren't
// installed. Settings brought from another PC (ConfigTransfer) name what only
// that PC has. They are kept, never dropped: the import says which ones, and the
// bar can draw them as broken with Check (the pins redesign, after this round).
//
// Only what can be told quickly and for certain is judged: a path on a drive
// this PC has, where a shortcut points on a local drive, and whether a packaged
// app's family is registered for this user. Web links, network paths (asking a
// share that isn't there can stall for many seconds), Windows' own places
// (shell:...) and apps Windows knows by a name of their own stay unchecked.

using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Win32;

namespace Karpo.Core;

/// <summary>What <see cref="PinTargets"/> asks of this PC. Tests stand in for the disk and the registry.</summary>
public interface ITargetSystem
{
    bool FileExists(string path);

    bool DirectoryExists(string path);

    /// <summary>The drive <paramref name="root"/> (such as "C:\") names; null when this PC has no such drive.</summary>
    DriveType? Drive(string root);

    /// <summary>A shortcut's bytes (its first 64 KB), or null when it can't be read.</summary>
    byte[]? ReadShortcut(string path);

    /// <summary>The names of the subkeys of HKEY_CURRENT_USER\<paramref name="path"/>; none when there is no such key.</summary>
    IReadOnlyList<string> UserSubKeys(string path);

    /// <summary><paramref name="text"/> with its environment variables expanded.</summary>
    string Expand(string text);

    /// <summary>A known folder's path; empty when this PC has none.</summary>
    string Folder(Environment.SpecialFolder folder);
}

/// <summary>Whether a pinned target is on this PC.</summary>
public enum TargetState
{
    Here,
    Missing,

    /// <summary>Can't be told quickly or for certain (a web link, a network path, one of Windows' own places), so taken as working.</summary>
    Unchecked,
}

/// <summary>What <see cref="PinTargets.Check"/> found, with a sentence for the person.</summary>
public sealed record TargetCheck(TargetState State, string Reason)
{
    public bool Missing => State == TargetState.Missing;
}

/// <summary>A pin that isn't on this PC, where the config has it, and why.</summary>
public sealed record MissingTarget(string Target, string Where, string Reason);

/// <summary>
/// §425: what kind of thing a pin opens, each a level of its own under its Target (OptionFields.OfPinTarget):
/// the one its target is ticked there.
/// </summary>
public enum PinKind
{
    /// <summary>Nothing yet, or a link of no kind of these (mailto:, a game's).</summary>
    None,

    /// <summary>An app of Start's, by its ID in the Apps folder.</summary>
    App,

    /// <summary>A folder, on a disk or a share, or one of Windows' own places (This PC).</summary>
    Folder,

    /// <summary>A file: a document, a program, a shortcut.</summary>
    File,

    /// <summary>A web address.</summary>
    Website,

    /// <summary>A page of Windows' Settings (ms-settings:).</summary>
    SettingsPage,
}

public static class PinTargets
{
    /// <summary>Where Windows registers the packaged apps installed for the user: one subkey per package, by its full name.</summary>
    public const string PackagesKey = @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages"; // not-ui: a registry key

    private const string AppsFolder = @"shell:AppsFolder\";

    /// <summary>This PC.</summary>
    public static ITargetSystem System { get; } = new ThisPc();

    /// <summary>
    /// What a pin's typed path means (its menu's Path field, as the settings app's box took it): a bare
    /// domain is a web address; anything with a scheme or a drive, or a UNC path, is taken as written.
    /// </summary>
    public static string Normalize(string typed)
    {
        var link = typed.Trim();
        // "%USERPROFILE%\Music" is a path too, its variables expanded where it opens (PinFolders.PathOf).
        return link.Length == 0 || link.Contains(':') || link.StartsWith('\\') || link.StartsWith('%') ? link : "https://" + link;
    }

    /// <summary>
    /// What a pin to <paramref name="target"/> is called before Windows is asked (§292: its menu's row
    /// and its name's hint; the dock asks Windows, §380): a web address's host, a path's last part
    /// ("Work", "notepad.exe", "Tools" for "D:\Tools\"), a shortcut's without ".lnk" as File Explorer
    /// shows it, an app by its package's name ("SpotifyMusic", never its ID), the target itself where it
    /// has none ("D:\"); empty for nothing.
    /// </summary>
    public static string NameOf(string? target)
    {
        var trimmed = (target ?? "").Trim().Trim('"');
        if (trimmed.Length == 0)
        {
            return "";
        }
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" && uri.Host.Length > 0)
        {
            return uri.Host;
        }
        if (trimmed.StartsWith(AppsFolder, StringComparison.OrdinalIgnoreCase) && AppNameOf(trimmed[AppsFolder.Length..]) is { Length: > 0 } app)
        {
            return app;
        }
        // §425: a page of Settings by its name in Settings' own words.
        if (SettingsActions.Page(trimmed) is { } page)
        {
            return page.Name;
        }
        var name = Path.GetFileName(trimmed.TrimEnd('\\', '/'));
        if (name.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) && name.Length > 4)
        {
            name = name[..^4];
        }
        return name.Length > 0 ? name : trimmed;
    }

    /// <summary>
    /// "Microsoft.OutlookForWindows_8wekyb3d8bbwe!Microsoft.OutlookforWindows" → "OutlookForWindows": a packaged
    /// app by its package's name; empty for an app ID that is no package's (a program's path, a name of its own).
    /// </summary>
    public static string AppNameOf(string id)
    {
        var bang = id.IndexOf('!');
        if (bang <= 0)
        {
            return "";
        }
        var family = id[..bang].Split('_')[0];
        var last = family.LastIndexOf('.');
        return last >= 0 && last + 1 < family.Length ? family[(last + 1)..] : family;
    }

    /// <summary>
    /// §425: the kind of <paramref name="target"/>: an app by its ID in the Apps folder, a page of Settings, a web
    /// address, one of Windows' own places (a folder), and a path a folder where <paramref name="isFolder"/> says so
    /// or it ends in "\" — without one to ask, where it has no extension —, else a file. None for nothing and for a
    /// link of another kind.
    /// </summary>
    public static PinKind KindOf(string? target, Func<string, bool>? isFolder = null)
    {
        var text = (target ?? "").Trim().Trim('"');
        if (text.Length == 0)
        {
            return PinKind.None;
        }
        if (text.StartsWith(AppsFolder, StringComparison.OrdinalIgnoreCase))
        {
            return PinKind.App;
        }
        if (text.StartsWith("shell:", StringComparison.OrdinalIgnoreCase) || text.StartsWith("::", StringComparison.Ordinal))
        {
            return PinKind.Folder;
        }
        if (text.StartsWith("ms-settings:", StringComparison.OrdinalIgnoreCase))
        {
            return PinKind.SettingsPage;
        }
        if (text.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || text.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return PinKind.Website;
        }
        if (HasScheme(text))
        {
            return PinKind.None;
        }
        var path = PinFolders.PathOf(text);
        return path.EndsWith('\\') || (isFolder is null ? !Path.HasExtension(path) : isFolder(path)) ? PinKind.Folder : PinKind.File;
    }

    /// <summary>
    /// §425: <paramref name="typed"/> as a website's address — an http or https address with a host that is a
    /// name with a dot, localhost or an IP address; a bare domain ("example.com/news") https's —, as typed;
    /// null for words that are none.
    /// </summary>
    public static string? Site(string? typed)
    {
        var text = (typed ?? "").Trim();
        if (text.Length == 0 || text.Any(char.IsWhiteSpace))
        {
            return null;
        }
        var address = text.Contains("://", StringComparison.Ordinal) ? text : "https://" + text;
        return Uri.TryCreate(address, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"
            && (uri.Host.Contains('.') || uri.IsLoopback || uri.HostNameType is UriHostNameType.IPv6)
            && !uri.Host.StartsWith('.') && !uri.Host.EndsWith('.')
            ? address
            : null;
    }

    /// <summary>§380: what a picker's row asks for to show <paramref name="target"/>'s picture (OptionFound.Picture), which the dock reads off the shell.</summary>
    public static string PictureOf(string target) => PictureScheme + target.Trim();

    /// <summary>What every target's picture address starts with: no web address does.</summary>
    public const string PictureScheme = "karpo-target:";

    /// <summary>
    /// Whether <paramref name="target"/>, a pin as the config has it, opens on this PC. For the bar,
    /// to draw a pin whose target is gone as broken instead of hiding it.
    /// </summary>
    public static TargetCheck Check(string target, ITargetSystem system) =>
        Check(target, system, new Lazy<HashSet<string>?>(() => Families(system)));

    /// <summary>
    /// Every pin in <paramref name="config"/> that isn't on this PC: the pin widgets, in folders
    /// too, each target once, in the order the file has them.
    /// </summary>
    public static IReadOnlyList<MissingTarget> Missing(Config config, ITargetSystem system)
    {
        var families = new Lazy<HashSet<string>?>(() => Families(system));
        var found = new List<MissingTarget>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Found(string? target, string where)
        {
            if (target is { } text && seen.Add(text.Trim()) && Check(text, system, families) is { Missing: true } check)
            {
                found.Add(new MissingTarget(text, where, check.Reason));
            }
        }
        void Walk(JsonNode? node, string? property, string? layout, bool folder)
        {
            switch (node)
            {
                case JsonObject entry:
                    if (property == "layouts")
                    {
                        layout = entry.TryGetPropertyValue("name", out var named) && named is JsonValue text && text.GetValueKind() == JsonValueKind.String
                            ? text.GetValue<string>()
                            : "";
                    }
                    folder |= property == "folder";
                    foreach (var (key, value) in entry.ToList())
                    {
                        // A pin widget: its target.
                        if (key == "pin" && value is JsonObject widget)
                        {
                            if (widget.TryGetPropertyValue("target", out var target) && target is JsonValue chosen && chosen.GetValueKind() == JsonValueKind.String)
                            {
                                Found(chosen.GetValue<string>(), Where(layout, folder));
                            }
                            continue;
                        }
                        Walk(value, key, layout, folder);
                    }
                    break;
                case JsonArray list:
                    foreach (var item in list)
                    {
                        Walk(item, property, layout, folder);
                    }
                    break;
            }
        }
        Walk(JsonNode.Parse(config.ToJson()), null, null, false);
        return found;
    }

    /// <summary>Where a pin stands: in a folder of <paramref name="layout"/> (a folder has no name, §427), or in it.</summary>
    private static string Where(string? layout, bool folder) =>
        layout is not { Length: > 0 } ? S.CorePinSomewhere
        : folder ? S.CorePinInFolder(layout)
        : S.CorePinInLayout(layout);

    private static TargetCheck Check(string target, ITargetSystem system, Lazy<HashSet<string>?> families)
    {
        var text = target.Trim();
        if (text.Length == 0)
        {
            return new TargetCheck(TargetState.Unchecked, S.CorePinCheckNotATarget);
        }
        if (text.StartsWith(AppsFolder, StringComparison.OrdinalIgnoreCase))
        {
            return App(text[AppsFolder.Length..], system, families);
        }
        if (text.StartsWith("shell:", StringComparison.OrdinalIgnoreCase) || text.StartsWith("::", StringComparison.Ordinal))
        {
            return new TargetCheck(TargetState.Unchecked, S.CorePinCheckWindowsPlace);
        }
        if (HasScheme(text))
        {
            return new TargetCheck(TargetState.Unchecked, S.CorePinCheckLink);
        }
        return PathCheck(text, system, followShortcut: true);
    }

    /// <summary>
    /// An app by its ID in the Apps folder: a packaged app (family!app) is installed when its family is
    /// registered for the user; an app without an ID of its own is named by a known folder and its
    /// program's path, which is checked as a path.
    /// </summary>
    private static TargetCheck App(string id, ITargetSystem system, Lazy<HashSet<string>?> families)
    {
        var bang = id.IndexOf('!');
        if (bang > 0)
        {
            if (families.Value is not { } installed)
            {
                return new TargetCheck(TargetState.Unchecked, S.CorePinCheckAppsUnlisted);
            }
            return installed.Contains(id[..bang])
                ? new TargetCheck(TargetState.Here, S.CorePinCheckInstalled)
                : new TargetCheck(TargetState.Missing, S.CorePinCheckAppMissing);
        }
        var close = id.IndexOf('}');
        if (id.StartsWith('{') && close > 0 && Guid.TryParse(id[..(close + 1)], out var guid) && KnownFolders.TryGetValue(guid, out var special))
        {
            var folder = system.Folder(special);
            return folder.Length == 0
                ? new TargetCheck(TargetState.Unchecked, S.CorePinCheckNoSuchFolder)
                : PathCheck(Path.Combine(folder, id[(close + 1)..].TrimStart('\\')), system, followShortcut: false);
        }
        return Path.IsPathFullyQualified(id)
            ? PathCheck(id, system, followShortcut: false)
            : new TargetCheck(TargetState.Unchecked, S.CorePinCheckNamedApp);
    }

    private static TargetCheck PathCheck(string path, ITargetSystem system, bool followShortcut)
    {
        var full = system.Expand(path.Trim().Trim('"'));
        if (!Path.IsPathFullyQualified(full))
        {
            return new TargetCheck(TargetState.Unchecked, S.CorePinCheckNotFullPath);
        }
        if (full.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return new TargetCheck(TargetState.Unchecked, S.CorePinCheckNetworkLocation);
        }
        var root = Path.GetPathRoot(full) ?? "";
        switch (system.Drive(root))
        {
            case null:
                return new TargetCheck(TargetState.Missing, S.CorePinCheckNoDrive(root.TrimEnd('\\')));
            case DriveType.Network:
                return new TargetCheck(TargetState.Unchecked, S.CorePinCheckOnNetworkDrive);
        }
        if (system.DirectoryExists(full))
        {
            return new TargetCheck(TargetState.Here, S.CorePinCheckHere);
        }
        if (!system.FileExists(full))
        {
            return new TargetCheck(TargetState.Missing, S.CorePinCheckNotHere);
        }
        if (followShortcut && full.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)
            && system.ReadShortcut(full) is { } link
            && ShortcutTarget(link, Path.GetDirectoryName(full)) is { } points
            && PathCheck(points, system, followShortcut: false) is { Missing: true })
        {
            return new TargetCheck(TargetState.Missing, S.CorePinCheckShortcutBroken(system.Expand(points)));
        }
        return new TargetCheck(TargetState.Here, S.CorePinCheckHere);
    }

    /// <summary><c>scheme:rest</c> with a scheme of two characters or more, so that <c>C:\</c> is a path.</summary>
    private static bool HasScheme(string target)
    {
        var colon = target.IndexOf(':');
        if (colon < 2 || !char.IsAsciiLetter(target[0]))
        {
            return false;
        }
        foreach (var character in target.AsSpan(0, colon))
        {
            if (!char.IsAsciiLetterOrDigit(character) && character is not ('+' or '.' or '-'))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>The package families installed for the user (name_publisher, lower case); null when they can't be listed.</summary>
    private static HashSet<string>? Families(ITargetSystem system)
    {
        var families = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var package in system.UserSubKeys(PackagesKey))
        {
            // name_version_architecture_resource_publisher: the family is the first and the last.
            var parts = package.Split('_');
            if (parts.Length >= 5)
            {
                families.Add($"{parts[0]}_{parts[^1]}");
            }
        }
        return families.Count > 0 ? families : null;
    }

    /// <summary>The known folders the Apps folder names programs by, for apps without an ID of their own.</summary>
    private static readonly Dictionary<Guid, Environment.SpecialFolder> KnownFolders = new()
    {
        [new Guid("6D809377-6AF0-444B-8957-A3773F02200E")] = Environment.SpecialFolder.ProgramFiles,
        [new Guid("905E63B6-C1BF-494E-B29C-65B732D3D21A")] = Environment.SpecialFolder.ProgramFiles,
        [new Guid("7C5A40EF-A0FB-4BFC-874A-C0F2E0B9FA8E")] = Environment.SpecialFolder.ProgramFilesX86,
        [new Guid("1AC14E77-02E7-4E5D-B744-2EB1AE5198B7")] = Environment.SpecialFolder.System,
        [new Guid("D65231B0-B2F1-4857-A4CE-A8E7C6EA7D27")] = Environment.SpecialFolder.SystemX86,
        [new Guid("F38BF404-1D43-42F2-9305-67DE0B28FC23")] = Environment.SpecialFolder.Windows,
        [new Guid("F1B32785-6FBA-4FCF-9D55-7B8E7F157091")] = Environment.SpecialFolder.LocalApplicationData,
        [new Guid("3EB685DB-65F9-4CF6-A03A-E3EF65729F3D")] = Environment.SpecialFolder.ApplicationData,
        [new Guid("62AB5D82-FDC1-4DC3-A9DD-070D1D495D97")] = Environment.SpecialFolder.CommonApplicationData,
        [new Guid("5E6C858F-0E22-4760-9AFE-EA3317B67173")] = Environment.SpecialFolder.UserProfile,
        [new Guid("A77F5D77-2E2B-44C3-A6A2-ABA601054A51")] = Environment.SpecialFolder.Programs,
        [new Guid("0139D44E-6AFE-49F2-8690-3DAFCAE6FFB8")] = Environment.SpecialFolder.CommonPrograms,
        [new Guid("B4BFCC3A-DB2C-424C-B029-7FE99A87C641")] = Environment.SpecialFolder.DesktopDirectory,
        [new Guid("FDD39AD0-238F-46AF-ADB4-6C85480369C7")] = Environment.SpecialFolder.MyDocuments,
    };

    // --- Shortcuts (MS-SHLLINK) ------------------------------------------------------

    private const int HeaderSize = 0x4C;
    private const uint HasLinkTargetIdList = 0x1;
    private const uint HasLinkInfo = 0x2;
    private const uint HasName = 0x4;
    private const uint HasRelativePath = 0x8;
    private const uint HasWorkingDir = 0x10;
    private const uint HasArguments = 0x20;
    private const uint HasIconLocation = 0x40;
    private const uint IsUnicode = 0x80;
    private const uint ForceNoLinkInfo = 0x100;
    private const uint VolumeIdAndLocalBasePath = 0x1;
    private const uint EnvironmentVariableBlock = 0xA0000001;

    /// <summary>The class id every shell link starts with, 00021401-0000-0000-C000-000000000046, as stored.</summary>
    private static ReadOnlySpan<byte> LinkClsid => [0x01, 0x14, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46];

    /// <summary>
    /// The path a shortcut (.lnk) points at, read from its bytes: the target with its environment
    /// variables, else its local path, else its path relative to <paramref name="folder"/> (the
    /// shortcut's own). Null when it has none of them, such as a shortcut to a packaged app, one on a
    /// network share, or one Windows Installer resolves itself; and for anything that isn't a shortcut.
    /// </summary>
    public static string? ShortcutTarget(ReadOnlySpan<byte> link, string? folder = null)
    {
        if (link.Length < HeaderSize || BinaryPrimitives.ReadUInt32LittleEndian(link) != HeaderSize || !link.Slice(4, 16).SequenceEqual(LinkClsid))
        {
            return null;
        }
        var flags = BinaryPrimitives.ReadUInt32LittleEndian(link[20..]);
        var at = HeaderSize;
        if ((flags & HasLinkTargetIdList) != 0)
        {
            if (at + 2 > link.Length)
            {
                return null;
            }
            at += 2 + BinaryPrimitives.ReadUInt16LittleEndian(link[at..]);
        }
        string? local = null;
        if ((flags & HasLinkInfo) != 0)
        {
            if (at + 4 > link.Length)
            {
                return null;
            }
            var size = (int)BinaryPrimitives.ReadUInt32LittleEndian(link[at..]);
            if (size < 0x1C || at + size > link.Length)
            {
                return null;
            }
            if ((flags & ForceNoLinkInfo) == 0)
            {
                local = LocalPath(link.Slice(at, size));
            }
            at += size;
        }
        string? relative = null;
        foreach (var (bit, wanted) in (ReadOnlySpan<(uint, bool)>)[(HasName, false), (HasRelativePath, true), (HasWorkingDir, false), (HasArguments, false), (HasIconLocation, false)])
        {
            if ((flags & bit) == 0)
            {
                continue;
            }
            if (at + 2 > link.Length)
            {
                break;
            }
            var count = BinaryPrimitives.ReadUInt16LittleEndian(link[at..]);
            var length = (flags & IsUnicode) != 0 ? count * 2 : count;
            at += 2;
            if (at + length > link.Length)
            {
                break;
            }
            if (wanted)
            {
                relative = (flags & IsUnicode) != 0 ? Encoding.Unicode.GetString(link.Slice(at, length)) : Ascii(link.Slice(at, length));
            }
            at += length;
        }
        string? environment = null;
        while (at + 8 <= link.Length)
        {
            var size = (int)BinaryPrimitives.ReadUInt32LittleEndian(link[at..]);
            if (size < 8 || at + size > link.Length)
            {
                break;
            }
            // TargetAnsi (260 bytes), then TargetUnicode (520).
            if (BinaryPrimitives.ReadUInt32LittleEndian(link[(at + 4)..]) == EnvironmentVariableBlock && size >= 8 + 260 + 520)
            {
                environment = Wide(link.Slice(at + 8 + 260, 520)) ?? Narrow(link.Slice(at + 8, 260));
            }
            at += size;
        }
        var target = environment ?? local ?? (relative is { Length: > 0 } && folder is not null ? Path.GetFullPath(Path.Combine(folder, relative)) : null);
        return string.IsNullOrWhiteSpace(target) ? null : target;
    }

    /// <summary>LinkInfo's local base path and common path suffix, the Unicode ones when it has them.</summary>
    private static string? LocalPath(ReadOnlySpan<byte> info)
    {
        var headerSize = BinaryPrimitives.ReadUInt32LittleEndian(info[4..]);
        var infoFlags = BinaryPrimitives.ReadUInt32LittleEndian(info[8..]);
        if ((infoFlags & VolumeIdAndLocalBasePath) == 0)
        {
            return null;
        }
        string? basePath = null;
        string? suffix = null;
        if (headerSize >= 0x24 && info.Length >= 0x24)
        {
            basePath = Wide(From(info, BinaryPrimitives.ReadUInt32LittleEndian(info[28..])));
            suffix = Wide(From(info, BinaryPrimitives.ReadUInt32LittleEndian(info[32..])));
        }
        basePath ??= Narrow(From(info, BinaryPrimitives.ReadUInt32LittleEndian(info[16..])));
        suffix ??= Narrow(From(info, BinaryPrimitives.ReadUInt32LittleEndian(info[24..])));
        return basePath is null ? null : basePath + suffix;
    }

    private static ReadOnlySpan<byte> From(ReadOnlySpan<byte> data, uint offset) =>
        offset == 0 || offset >= data.Length ? [] : data[(int)offset..];

    /// <summary>A NUL-ended UTF-16 string; null when empty or unended.</summary>
    private static string? Wide(ReadOnlySpan<byte> data)
    {
        for (var index = 0; index + 1 < data.Length; index += 2)
        {
            if (data[index] == 0 && data[index + 1] == 0)
            {
                return index == 0 ? null : Encoding.Unicode.GetString(data[..index]);
            }
        }
        return null;
    }

    /// <summary>
    /// A NUL-ended string in the system's ANSI code page. Only ASCII is taken: other characters
    /// depend on a code page this may not share, and a wrongly read path would count as missing.
    /// </summary>
    private static string? Narrow(ReadOnlySpan<byte> data)
    {
        var end = data.IndexOf((byte)0);
        return end <= 0 ? null : Ascii(data[..end]);
    }

    private static string? Ascii(ReadOnlySpan<byte> data)
    {
        foreach (var value in data)
        {
            if (value >= 0x80)
            {
                return null;
            }
        }
        return Encoding.ASCII.GetString(data);
    }

    /// <summary>The disk, the registry and the environment of this PC.</summary>
    private sealed class ThisPc : ITargetSystem
    {
        public bool FileExists(string path) => File.Exists(path);

        public bool DirectoryExists(string path) => Directory.Exists(path);

        public DriveType? Drive(string root)
        {
            try
            {
                var type = new DriveInfo(root).DriveType;
                return type == DriveType.NoRootDirectory ? null : type;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        public byte[]? ReadShortcut(string path)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                var bytes = new byte[Math.Min(stream.Length, 64 * 1024)];
                stream.ReadExactly(bytes);
                return bytes;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }

        public IReadOnlyList<string> UserSubKeys(string path)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(path);
                return key?.GetSubKeyNames() ?? [];
            }
            catch (Exception error) when (error is global::System.Security.SecurityException or IOException or UnauthorizedAccessException)
            {
                return [];
            }
        }

        public string Expand(string text) => Environment.ExpandEnvironmentVariables(text);

        public string Folder(Environment.SpecialFolder folder) => Environment.GetFolderPath(folder);
    }
}
