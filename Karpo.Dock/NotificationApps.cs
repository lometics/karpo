// Who a notification came from: the name and the icon the notification list heads an app's group
// with, from the AUMID the notification database files it under (NotificationHandler.PrimaryId).
//
// The Start menu alone was asked before, and it knows only the apps it lists. Windows' own senders
// are not among them â€” Windows Security's toasts come as "Windows.Defender.SecurityCenter", Bluetooth's
// as "Windows.SystemToast.BthQuickPair" â€” nor are an app's second entries, toast-only registrations
// (ToastNotificationManagerCompat's hashes) or the notification area's balloons. Those showed their
// raw id and no icon. Windows names them from places of their own, and so does this, in order:
//
//   1. The Start menu's entry (shell:AppsFolder\<AUMID>), when it really carries that AUMID.
//   2. HKCU, then HKLM, Software\Classes\AppUserModelId\<AUMID>: DisplayName, often an indirect
//      string ("@%SystemRoot%\system32\x.dll,-12", "@{pri?ms-resource://â€¦}") resolved with
//      SHLoadIndirectString, and IconUri, a file, an "ms-resource://" of Windows' own resources or an
//      indirect string to one. Every Windows.SystemToast.* sender is registered there.
//   3. A packaged app's own name and logo (AppInfo), for the entries the Start menu doesn't list.
//   4. A notification area balloon's program: NotifyIconGeneratedAumid_<n> and
//      Microsoft.Explorer.Notification.{GUID} are the tray icon under HKCU\Control Panel\
//      NotifyIconSettings (by key, or by IconGuid), whose ExecutablePath names it and draws it.
//   5. Last, a name made from the id (NotificationText.FallbackName) â€” never the raw id where a word
//      of it will do â€” and, for Windows' own, the default system notification icon Windows uses.
//
// Windows draws several of its senders' icons as a white glyph on a coloured plate (IconUri with
// IconBackgroundColor). A white glyph is marked (Pixels.Mask) so the list draws it in its text colour,
// where white would vanish on a light theme.
//
// Everything is read only. The answer is kept per AUMID by the store (NotificationStore.Resolve).

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Win32;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

/// <summary>Where <see cref="NotificationApps"/> asks; Windows' own in the dock (<see cref="NotificationApps.Windows"/>), made up in tests.</summary>
internal interface INotificationAppSource
{
    /// <summary>The Start menu's name for the entry carrying <paramref name="aumid"/>; null where it has none.</summary>
    string? StartName(string aumid);

    /// <summary>That entry's icon.</summary>
    Pixels? StartIcon(string aumid, int px);

    /// <summary>A value of HKCU, else HKLM, <c>Software\Classes\AppUserModelId\&lt;aumid&gt;</c>, as written.</summary>
    string? Registered(string aumid, string value);

    /// <summary>A packaged app's display name; null for anything else.</summary>
    string? PackageName(string aumid);

    /// <summary>A packaged app's logo file.</summary>
    string? PackageLogo(string aumid);

    /// <summary>
    /// The program of a notification area icon, a full path: <paramref name="key"/> is its key under
    /// NotifyIconSettings, or "{GUID}", the IconGuid of one.
    /// </summary>
    string? TrayProgram(string key);

    /// <summary>An indirect string (<c>@â€¦</c>) resolved; null where it can't be.</summary>
    string? Indirect(string source);

    /// <summary>A program's description (its version resource's FileDescription).</summary>
    string? Describe(string program);

    /// <summary>A file's icon, as the shell draws it.</summary>
    Pixels? FileIcon(string path, int px);

    /// <summary>A picture file; null where there is none.</summary>
    Pixels? Picture(string path, int px);
}

internal static class NotificationApps
{
    /// <summary>Icons are made this big: 32 DIPs at 200 %; the list's 16 are drawn from it, sharp at 150 %.</summary>
    internal const int IconPx = 64;

    /// <summary>What Windows itself shows for a system sender that registered no icon.</summary>
    internal const string DefaultSystemIcon = "ms-resource://Windows.UI.ShellCommon/Files/Images/DefaultSystemNotification.png";

    private const string TrayPrefix = "NotifyIconGeneratedAumid_";
    private const string BalloonPrefix = "Microsoft.Explorer.Notification.";

    internal static INotificationAppSource Windows { get; } = new WindowsSource();

    /// <summary>The name and icon of the app <paramref name="aumid"/> names, by the steps in the header.</summary>
    public static (string Name, Pixels? Icon) Resolve(string aumid, INotificationAppSource from)
    {
        var name = Usable(from.StartName(aumid), aumid);
        var icon = name is null ? null : from.StartIcon(aumid, IconPx);
        var program = TrayKey(aumid) is { } key ? from.TrayProgram(key) : null;

        name ??= Usable(Text(from.Registered(aumid, "DisplayName"), from), aumid);
        name ??= Usable(Packaged(aumid) ? from.PackageName(aumid) : null, aumid);
        if (name is null && program is not null)
        {
            name = Usable(from.Describe(program), aumid) ?? Items.FileStem(program);
        }
        name ??= NotificationText.FallbackName(aumid);

        icon ??= Picture(from.Registered(aumid, "IconUri"), from);
        icon ??= Packaged(aumid) && from.PackageLogo(aumid) is { } logo ? Marked(from.Picture(logo, IconPx)) : null;
        icon ??= program is not null ? from.FileIcon(program, IconPx) : null;
        icon ??= NotificationText.IsWindows(aumid) ? Picture(DefaultSystemIcon, from) : null;
        return (name, icon);
    }

    /// <summary>
    /// The key of the notification area icon a balloon's AUMID stands for: the number of
    /// NotifyIconGeneratedAumid_&lt;n&gt;, the "{GUID}" of Microsoft.Explorer.Notification.{GUID}.
    /// </summary>
    internal static string? TrayKey(string aumid)
    {
        if (aumid.StartsWith(TrayPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var number = aumid[TrayPrefix.Length..];
            return number.Length > 0 && number.All(char.IsAsciiDigit) ? number : null;
        }
        if (aumid.StartsWith(BalloonPrefix, StringComparison.OrdinalIgnoreCase)
            && Guid.TryParse(aumid.AsSpan(BalloonPrefix.Length), out var id))
        {
            return id.ToString("B").ToUpperInvariant();
        }
        return null;
    }

    /// <summary>
    /// A registered name as words: an indirect string resolved; a resource reference written without
    /// its "@" ("C:\x.dll,103") taken as one; null for what stays a reference.
    /// </summary>
    internal static string? Text(string? raw, INotificationAppSource from)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }
        raw = raw.Trim();
        if (raw.StartsWith('@'))
        {
            return from.Indirect(raw);
        }
        var comma = raw.LastIndexOf(',');
        if (comma > 0 && raw.Contains(":\\", StringComparison.Ordinal) && int.TryParse(raw.AsSpan(comma + 1), out var number))
        {
            return from.Indirect($"@{raw[..comma]},-{Math.Abs(number)}");
        }
        return raw;
    }

    /// <summary>
    /// The file a registered IconUri means: an indirect string resolved, an "ms-resource://" of Windows'
    /// own resources found through Windows' resource index, a file: URI's path, a path as written.
    /// </summary>
    internal static string? PicturePath(string? uri, INotificationAppSource from)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            return null;
        }
        uri = uri.Trim();
        if (uri.StartsWith('@'))
        {
            return from.Indirect(uri);
        }
        if (uri.StartsWith("ms-resource:", StringComparison.OrdinalIgnoreCase))
        {
            return from.Indirect($"@{{Windows?{uri}}}");
        }
        if (uri.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            return Uri.TryCreate(uri, UriKind.Absolute, out var file) ? file.LocalPath : null;
        }
        // ms-appx: and the like belong to a package that is not named here.
        return Items.HasScheme(uri) ? null : Environment.ExpandEnvironmentVariables(uri);
    }

    private static Pixels? Picture(string? uri, INotificationAppSource from) =>
        PicturePath(uri, from) is { Length: > 0 } path ? Marked(from.Picture(path, IconPx)) : null;

    /// <summary><paramref name="pixels"/>, marked when they are a white glyph (see the header).</summary>
    internal static Pixels? Marked(Pixels? pixels)
    {
        if (pixels is not null && IsWhiteGlyph(pixels.Data))
        {
            pixels.Mask = true;
        }
        return pixels;
    }

    /// <summary>
    /// Whether premultiplied BGRA <paramref name="data"/> is white alone: every pixel seen is as light as it
    /// is opaque, and some are seen.
    /// </summary>
    internal static bool IsWhiteGlyph(ReadOnlySpan<byte> data)
    {
        var seen = false;
        for (var i = 0; i + 3 < data.Length; i += 4)
        {
            int alpha = data[i + 3];
            if (alpha < 16)
            {
                continue;
            }
            seen = true;
            // Near white: each channel within an eighth of the alpha it is premultiplied by.
            var floor = alpha - (alpha / 8);
            if (data[i] < floor || data[i + 1] < floor || data[i + 2] < floor)
            {
                return false;
            }
        }
        return seen;
    }

    /// <summary>The last colour each glyph was drawn in, and the pixels it was drawn with.</summary>
    private static readonly ConditionalWeakTable<Pixels, Tint> Tints = new();

    private sealed record Tint(DXGI_RGBA Colour, Pixels Pixels);

    /// <summary>
    /// <paramref name="pixels"/> as drawn in <paramref name="colour"/>: a <see cref="Pixels.Mask"/> glyph's
    /// shape in that colour, kept until the colour changes; anything else as it is.
    /// </summary>
    internal static Pixels Drawn(Pixels pixels, DXGI_RGBA colour)
    {
        if (!pixels.Mask)
        {
            return pixels;
        }
        if (Tints.TryGetValue(pixels, out var kept) && kept.Colour.Equals(colour))
        {
            return kept.Pixels;
        }
        var data = new byte[pixels.Data.Length];
        var (b, g, r, a) = (Math.Clamp(colour.b, 0, 1), Math.Clamp(colour.g, 0, 1), Math.Clamp(colour.r, 0, 1), Math.Clamp(colour.a, 0, 1));
        for (var i = 0; i + 3 < data.Length; i += 4)
        {
            // Premultiplied: each channel is the colour's times the glyph's coverage.
            var alpha = pixels.Data[i + 3] * a;
            data[i] = (byte)MathF.Round(b * alpha);
            data[i + 1] = (byte)MathF.Round(g * alpha);
            data[i + 2] = (byte)MathF.Round(r * alpha);
            data[i + 3] = (byte)MathF.Round(alpha);
        }
        var made = new Pixels { Data = data, Width = pixels.Width, Height = pixels.Height };
        Tints.AddOrUpdate(pixels, new Tint(colour, made));
        return made;
    }

    /// <summary>A name that says more than the id: not blank, not the id, no reference left unresolved.</summary>
    private static string? Usable(string? name, string aumid)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }
        name = name.Trim();
        return name.Equals(aumid, StringComparison.OrdinalIgnoreCase)
            || name.StartsWith('@')
            || name.StartsWith("shell:", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("ms-resource:", StringComparison.OrdinalIgnoreCase)
                ? null
                : name;
    }

    private static bool Packaged(string aumid) => WindowsActivationPlatform.PackageFamily(aumid) is { } family && family.Contains('_');

    /// <summary>Windows' own places, read only.</summary>
    private sealed unsafe class WindowsSource : INotificationAppSource
    {
        private const uint Chars = 1024;

        public string? StartName(string aumid) => Start(aumid)?.Name;

        public Pixels? StartIcon(string aumid, int px) => Items.Icon($@"shell:AppsFolder\{aumid}", px);

        /// <summary>The Apps folder's entry, only where it carries the AUMID (Item.Registered's test).</summary>
        private static Item? Start(string aumid) =>
            Item.Resolve($@"shell:AppsFolder\{aumid}") is { Kind: ItemKind.Shell } found
            && string.Equals(found.Aumid, aumid, StringComparison.OrdinalIgnoreCase)
                ? found
                : null;

        public string? Registered(string aumid, string value)
        {
            foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
            {
                try
                {
                    using var key = hive.OpenSubKey($@"Software\Classes\AppUserModelId\{aumid}");
                    if (key?.GetValue(value) is string text && !string.IsNullOrWhiteSpace(text))
                    {
                        return text;
                    }
                }
                catch (Exception error) when (error is System.Security.SecurityException or UnauthorizedAccessException or IOException or ArgumentException)
                {
                    // Unreadable is the same as absent.
                }
            }
            return null;
        }

        public string? PackageName(string aumid) =>
            Package(aumid, app => app.DisplayInfo?.DisplayName, package => package.DisplayName);

        public string? PackageLogo(string aumid) =>
            Package(aumid, app => app.Package?.Logo is { IsFile: true } logo ? logo.LocalPath : null, package => package.Logo is { IsFile: true } logo ? logo.LocalPath : null);

        /// <summary>
        /// What the app entry says (AppInfo), else its package: an entry Windows keeps no AppInfo for (an
        /// app's setup, say) still has its package's name and logo.
        /// </summary>
        private static string? Package(string aumid, Func<global::Windows.ApplicationModel.AppInfo, string?> app,
            Func<global::Windows.ApplicationModel.Package, string?> package)
        {
            try
            {
                if (global::Windows.ApplicationModel.AppInfo.GetFromAppUserModelId(aumid) is { } info && app(info) is { Length: > 0 } said)
                {
                    return said;
                }
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                // No entry of that name: its package is asked.
            }
            try
            {
                return WindowsActivationPlatform.PackageFamily(aumid) is { } family
                    && new global::Windows.Management.Deployment.PackageManager().FindPackagesForUser("", family).FirstOrDefault() is { } found
                        ? package(found)
                        : null;
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                // Not a package this user has.
                return null;
            }
        }

        public string? TrayProgram(string key)
        {
            try
            {
                using var icons = Registry.CurrentUser.OpenSubKey(@"Control Panel\NotifyIconSettings"); // not-ui
                if (icons is null)
                {
                    return null;
                }
                if (!key.StartsWith('{'))
                {
                    using var icon = icons.OpenSubKey(key);
                    return Expand(icon?.GetValue("ExecutablePath") as string);
                }
                foreach (var name in icons.GetSubKeyNames())
                {
                    using var icon = icons.OpenSubKey(name);
                    if (icon?.GetValue("IconGuid") is string guid && string.Equals(guid, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return Expand(icon.GetValue("ExecutablePath") as string);
                    }
                }
            }
            catch (Exception error) when (error is System.Security.SecurityException or UnauthorizedAccessException or IOException or ArgumentException)
            {
            }
            return null;
        }

        /// <summary><c>{GUID}\rest</c>, where the GUID names a known folder, to a full path.</summary>
        private static string? Expand(string? path)
        {
            if (path is null || path.Length < 40 || path[0] != '{' || path[37] != '}' || path[38] != '\\'
                || !Guid.TryParse(path.AsSpan(0, 38), out var id))
            {
                return path;
            }
            char* raw = null;
            var found = SHGetKnownFolderPath(&id, 0, HANDLE.NULL, &raw);
            // The buffer is the caller's to free even when the call fails.
            var folder = Native.ComUtil.FromTaskMem(raw);
            return found.SUCCEEDED && folder is not null ? string.Concat(folder, path.AsSpan(38)) : null;
        }

        public string? Indirect(string source)
        {
            var buffer = new char[Chars];
            fixed (char* from = source)
            fixed (char* output = buffer)
            {
                return SHLoadIndirectString(from, output, Chars, null).SUCCEEDED && output[0] != '\0' ? new string(output) : null;
            }
        }

        public string? Describe(string program)
        {
            try
            {
                return File.Exists(program) ? FileVersionInfo.GetVersionInfo(program).FileDescription : null;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }

        public Pixels? FileIcon(string path, int px) => File.Exists(path) ? Items.Icon(path, px) : null;

        public Pixels? Picture(string path, int px) => File.Exists(path) ? NotificationPartTile.LoadImage(path, px) : null;
    }
}
