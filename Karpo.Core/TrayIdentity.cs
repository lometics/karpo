// What names one notification icon in the config, and why it is not simply the
// executable's path.
//
// An icon that brings a GUID is named by it: that is what its app chose, and it
// survives everything. Every other icon is named by its app's executable and
// the id the app gave it, because that is all Windows tells us apart by either
// (HKCU\Control Panel\NotifyIconSettings keys its records the same way).
//
// The snag is that a packaged app's executable moves with every update:
// `...\WindowsApps\SpotifyAB.SpotifyMusic_1.300.277.0_x64__zpdnekdrzrea0\Spotify.exe`
// became `...SpotifyMusic_1.301.234.0_x64...` and, to Windows and to Karpo, a
// different icon — so the choice made for it, the place it was dragged to, and
// Windows' own "keep this on the taskbar" were all left behind on a path no
// process runs from any more. Squirrel installers (`...\app-1.0.9243\...`) move
// the same way.
//
// So the version is taken out of the path: what is left is the package family
// (`SpotifyAB.SpotifyMusic__zpdnekdrzrea0`), which is what Windows itself calls
// a packaged app when it means "this app" rather than "this build of it". The
// rest of the path is untouched, and a path with no version folder in it comes
// back as it went in.
//
// A GUID is not always the app's own choice either: WhatsApp's packaged app gives
// its icon a new GUID with every build (NotifyIconSettings holds a record for
// each of 2.2587 to 2.2637, every one a different GUID), so named by its GUID it
// was a new icon after each update, and where it had been put was lost. An icon
// with a GUID whose executable lives in a version folder is therefore named by
// its app's family and the word "guid", which every build of it shares. A GUID
// icon from a path with no version in it keeps its GUID as its name.

namespace Karpo.Core;

/// <summary>How a tray's <see cref="TrayIconOptions.States"/> and <see cref="TrayIconOptions.Order"/> name one notification icon.</summary>
public static class TrayIdentity
{
    /// <summary>
    /// The name for an icon: its GUID when it has one, else its app's executable (with any version
    /// folder taken out, see <see cref="Stable"/>) and the id the app gave it. A GUID icon whose
    /// executable sits in a version folder is named by the app's family and "guid" instead, as such
    /// an app may give each build a GUID of its own (see the top of this file).
    /// </summary>
    public static string Of(string? path, uint uid, Guid? guid)
    {
        if (guid is { } id)
        {
            return path is { Length: > 0 } && Stable(path) is var family && !string.Equals(family, path, StringComparison.Ordinal)
                ? family.ToLowerInvariant() + "|guid"
                : id.ToString("B");
        }
        return Stable(path ?? "unknown").ToLowerInvariant() + "|" + uid;
    }

    /// <summary>
    /// <paramref name="path"/> with the version taken out of any folder that carries one, so the
    /// same app keeps one name across its updates. A path without such a folder comes back unchanged.
    /// </summary>
    public static string Stable(string path)
    {
        // An icon named by its GUID carries no path and is never touched.
        if (string.IsNullOrEmpty(path) || path[0] == '{' || path.IndexOf('_') < 0 && path.IndexOf('-') < 0)
        {
            return path;
        }
        var parts = path.Split('\\');
        var changed = false;
        for (var at = 0; at < parts.Length; at++)
        {
            var folder = Family(parts[at]);
            if (folder is not null)
            {
                parts[at] = folder;
                changed = true;
            }
        }
        return changed ? string.Join('\\', parts) : path;
    }

    /// <summary>
    /// Whether two paths are the same app, the version aside: <c>a\b_1.2_x64__p\x.exe</c> and
    /// <c>a\b_1.3_x64__p\x.exe</c> are.
    /// </summary>
    public static bool SameApp(string? one, string? other) =>
        one is not null && other is not null
            && string.Equals(Stable(one), Stable(other), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// One folder without its version, or null when it carries none:
    /// <c>Name_1.2.3.4_x64__Publisher</c> becomes <c>Name__Publisher</c> (a packaged app's family),
    /// and <c>app-1.0.9243</c> becomes <c>app</c> (what Squirrel installers lay down).
    /// </summary>
    private static string? Family(string folder)
    {
        if (folder.Length == 0)
        {
            return null;
        }
        // A packaged app: Name_Version_Architecture__PublisherId. The publisher is after the last
        // "__", so a name with its own underscores is safe.
        var publisher = folder.LastIndexOf("__", StringComparison.Ordinal);
        if (publisher > 0)
        {
            var fields = folder[..publisher].Split('_');
            // …_<version>_<architecture>: both go, and the name keeps whatever it had.
            if (fields.Length >= 3 && IsVersion(fields[^2]))
            {
                return string.Join('_', fields[..^2]) + folder[publisher..];
            }
            return null;
        }
        // A Squirrel install: app-<version>, beside Update.exe. The version must carry a dot, so a
        // folder that merely ends in a number ("office-2021") keeps its name.
        var dash = folder.LastIndexOf('-');
        return dash > 0 && folder.IndexOf('.', dash) > 0 && IsVersion(folder.AsSpan(dash + 1)) ? folder[..dash] : null;
    }

    /// <summary>A version as these folders write one: 1, 1.2, 1.0.9243, 1.300.277.0.</summary>
    private static bool IsVersion(ReadOnlySpan<char> text)
    {
        if (text.Length == 0 || !char.IsAsciiDigit(text[0]) || !char.IsAsciiDigit(text[^1]))
        {
            return false;
        }
        for (var at = 1; at < text.Length; at++)
        {
            // A dot only between numbers, so "1..2" and "1.x" are no version.
            if (!char.IsAsciiDigit(text[at]) && (text[at] != '.' || !char.IsAsciiDigit(text[at - 1])))
            {
                return false;
            }
        }
        return true;
    }
}

/// <summary>
/// tray: one name per live icon, where two would share one. <see cref="TrayIdentity.Of"/> names an
/// icon by its app and its uID, or by its GUID, which is what lasts across restarts and updates —
/// but two windows of one app may each add an icon with the same uID, and every build of an app
/// in a version folder shares one GUID name (<c>family|guid</c>), so two GUID icons of one such app
/// shared it too. Windows tells them apart (by window and uID, or by GUID), and so must the choice
/// and the place made for each: under one name a drag of one moved them both.
/// <para>
/// The first of an app's icons keeps the plain name, so a lone icon is named exactly as before and
/// its choice and place carry over; the next ones get <c>#2</c>, <c>#3</c>… after it. A name, once
/// given, stays with its icon while the icon lives, so one window closing doesn't hand its
/// neighbour's place over; a name freed is given to the next icon that needs it. Windows itself
/// can't tell two icons of one path and one uID apart across sessions either (NotifyIconSettings
/// holds one record for both), so which of the two is <c>#2</c> after a restart is the order they
/// came back in.
/// </para>
/// </summary>
public sealed class TrayNames
{
    private readonly Dictionary<string, string> given = new(StringComparer.Ordinal);

    /// <summary>
    /// The name of each of <paramref name="icons"/> — each a live icon's key (its window and uID, or
    /// its GUID) and its <see cref="TrayIdentity.Of"/> name — in their order. Icons not among them
    /// any more give their names back.
    /// </summary>
    public List<string> Name(IReadOnlyList<(string Key, string Identity)> icons)
    {
        var live = new HashSet<string>(icons.Select(icon => icon.Key), StringComparer.Ordinal);
        foreach (var gone in given.Keys.Where(key => !live.Contains(key)).ToList())
        {
            _ = given.Remove(gone);
        }
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var names = new string?[icons.Count];
        // The names already given first, so a newcomer never takes one from an icon that had it.
        for (var at = 0; at < icons.Count; at++)
        {
            var (key, identity) = icons[at];
            if (given.TryGetValue(key, out var name) && string.Equals(Base(name), identity, StringComparison.OrdinalIgnoreCase) && taken.Add(name))
            {
                names[at] = name;
            }
        }
        for (var at = 0; at < icons.Count; at++)
        {
            if (names[at] is not null)
            {
                continue;
            }
            var (key, identity) = icons[at];
            var name = identity;
            for (var count = 2; !taken.Add(name); count++)
            {
                name = identity + "#" + count;
            }
            names[at] = name;
            given[key] = name;
        }
        return [.. names.Select(name => name!)];
    }

    /// <summary>The name given to the live icon <paramref name="key"/>, or null when none was.</summary>
    public string? Of(string key) => given.TryGetValue(key, out var name) ? name : null;

    /// <summary><paramref name="name"/> without the <c>#n</c> that told it from another icon of its app.</summary>
    public static string Base(string name)
    {
        var mark = name.LastIndexOf('#');
        return mark > 0 && mark < name.Length - 1 && name.AsSpan(mark + 1).IndexOfAnyExceptInRange('0', '9') < 0 ? name[..mark] : name;
    }
}
