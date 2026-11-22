// menus: the icons of File Explorer's context menu, two colours each (the person, 2026-10-16: "in the
// whole karpo app context menus, use the icons that the file explorer uses. the ones that have two
// colors").
//
// They are not a font: Segoe Fluent Icons has no colour layers. Explorer draws its commands from SVGs
// of its own, in its package under Windows (FileExplorerExtensions\Assets\images), one set per theme —
// a light glyph with a cyan part in the dark one (#E0DFDF and #4CC2FF), a dark one with a blue part
// in the light one. Karpo reads them from there as a menu is drawn,
// the same files Explorer reads, and copies none: where they are missing (Windows 10, an older 11, a
// file renamed by an update) a row keeps its Segoe Fluent glyph.
//
// A menu row names its icon by its glyph (FlyoutIcon.Glyph), as every menu of Karpo's always has: the
// glyphs Explorer has a picture for draw as Explorer draws that command (For), so Copy is Explorer's
// Copy wherever a menu has it, and the rest stay glyphs, as Explorer's own rows without a picture do.

using System.Collections.Concurrent;

namespace Karpo.Core;

public static class ExplorerIcons
{
    /// <summary>The glyphs Explorer has a picture of, and the picture: its file's name without ".svg".</summary>
    private static readonly Dictionary<int, string> Pictures = new()
    {
        [0xE8C8] = "windows.copy",
        [0xE8C6] = "windows.cut",
        [0xE77F] = "windows.paste",
        [0xE74D] = "windows.ribbondelete",
        [0xE8AC] = "windows.rename",
        [0xE72D] = "Windows.ModernShare",
        [0xE713] = "windows.folderoptions",
        [0xE946] = "windows.properties",
        [0xE72C] = "windows.refresh",
        [0xE7A7] = "windows.undo",
        [0xE7A6] = "windows.redo",
        [0xE710] = "windows.newitem",
        [0xE8A7] = "windows.opennewwindow",
        [0xE838] = "windows.openfolder",
        [0xE8B7] = "windows.openfolder",
        [0xEC50] = "windows.openfolder",
        [0xE8DA] = "windows.shortcut.opencontaining",
        [0xE7AC] = "windows.openwith",
        [0xE718] = "windows.pintohome",
        [0xE77A] = "windows.unpinfromstartscreen",
        [0xE8B3] = "windows.selectall",
        [0xE8E6] = "windows.selectnone",
        [0xE8BB] = "windows.close",
        [0xE711] = "windows.close",
        [0xE7EF] = "RunAsAdmin",
        [0xE771] = "Personalize",
        [0xE7B3] = "windows.showhiddenfiles",
        [0xE890] = "View",
        [0xE8CB] = "SortBy",
        [0xE749] = "print",
        [0xE71B] = "link",
        [0xE896] = "windows.install",
    };

    /// <summary>
    /// Which of Explorer's sets. A high-contrast theme has none here: its rows turn the theme's colours
    /// under the pointer, which a picture of fixed colours can't, so they keep their glyphs.
    /// </summary>
    public enum Tone
    {
        Dark,
        Light,
    }

    // Explorer's pictures, by theme, in its package under Windows.
    private static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Windows),
        "SystemApps", "MicrosoftWindows.Client.FileExp_cw5n1h2txyewy", "FileExplorerExtensions", "Assets", "images");

    // Each file read once: its bytes, or null where it isn't there.
    private static readonly ConcurrentDictionary<(string Name, Tone Tone), byte[]?> Read = new();

    /// <summary>The name of Explorer's picture for <paramref name="glyph"/>; null where it has none.</summary>
    public static string? NameFor(string glyph) =>
        glyph.Length == 1 && Pictures.TryGetValue(glyph[0], out var name) ? name : null;

    /// <summary>Where Explorer keeps <paramref name="name"/> for <paramref name="tone"/>.</summary>
    public static string PathOf(string name, Tone tone) => Path.Combine(
        Folder,
        "contrast-standard",
        tone == Tone.Dark ? "theme-dark" : "theme-light",
        name + ".svg");

    /// <summary>
    /// The SVG Explorer draws <paramref name="glyph"/>'s command with in <paramref name="tone"/>, as it
    /// is on this PC; null where Explorer has no such picture or this Windows hasn't the file.
    /// </summary>
    public static byte[]? For(string glyph, Tone tone) =>
        NameFor(glyph) is { } name
            ? Read.GetOrAdd((name, tone), static key =>
            {
                try
                {
                    var path = PathOf(key.Name, key.Tone);
                    return File.Exists(path) ? File.ReadAllBytes(path) : null;
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    return null;
                }
            })
            : null;
}
