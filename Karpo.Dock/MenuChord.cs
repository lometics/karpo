// menus: a row's keyboard shortcut, shown at its right as Explorer's menus show theirs ("Copy as path
// Ctrl+Shift+C"), in the tooltip of a button of the row ("Cut (Ctrl+X)"), and taken by the menu while it
// stands open (Flyout.Press), so a shortcut a menu shows is one that works there (the person,
// 2026-10-16: "the keyboard shortcuts are also not shown in the context menus").
//
// Only what Karpo does on those keys anyway: in edit mode Ctrl+X cuts the widget with the focus, Ctrl+C
// copies it, Delete removes it, Escape leaves (EditKeys) — so "Cut", "Copy", "Remove" and "Done editing"
// have them; F2 renames a note or a list in its line's menu, as it renames in Explorer (§317). The
// caps are written as the menu's language writes them (Strg in German, Suppr in French), not as the
// keyboard's: Windows' own key names follow the keyboard layout, and an English menu on a Swiss German
// keyboard said "STRG".

using TerraFX.Interop.Windows;

namespace Karpo.Dock;

/// <summary>A shortcut: <paramref name="Key"/> (a virtual key, a letter as its capital), with Ctrl or not, and Shift or not.</summary>
public readonly record struct MenuChord(int Key, bool Control = false, bool Shift = false)
{
    /// <summary>Ctrl+X.</summary>
    public static MenuChord Cut => new('X', Control: true);

    /// <summary>Ctrl+C.</summary>
    public static MenuChord Copy => new('C', Control: true);

    /// <summary>Delete.</summary>
    public static MenuChord Delete => new(VK.VK_DELETE);

    /// <summary>Escape.</summary>
    public static MenuChord Escape => new(VK.VK_ESCAPE);

    /// <summary>F2, Explorer's rename: a note's or a list's in its line's menu (§317).</summary>
    public static MenuChord Rename => new(VK.VK_F2);

    /// <summary>What the menu shows: "Ctrl+C", "Ctrl+Shift+C", "Del", "Esc" in the menu's language.</summary>
    public string Text => (Control ? $"{S.DockKeysCtrl}+" : "") + (Shift ? $"{S.DockKeysShift}+" : "") + Cap;

    /// <summary>A tooltip with it: "Copy (Ctrl+C)", as Explorer's buttons say theirs.</summary>
    public string Tip(string name) => S.DockKeysTip(name, Text);

    /// <summary>Whether <paramref name="key"/>, with Ctrl and Shift down or not and no Alt, is this shortcut.</summary>
    public bool Is(int key, bool control, bool alt, bool shift = false) => !alt && key == Key && control == Control && shift == Shift;

    private string Cap => Key switch
    {
        VK.VK_DELETE => S.DockKeysDelete,
        VK.VK_ESCAPE => S.DockKeysEscape,
        >= VK.VK_F1 and <= VK.VK_F24 => $"F{Key - VK.VK_F1 + 1}",
        _ => ((char)Key).ToString(),
    };
}
