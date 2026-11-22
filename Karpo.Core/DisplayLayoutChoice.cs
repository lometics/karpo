// A display's "Layout on this display" dropdown on the Displays page (the person, 2026-10-08): every
// named layout by name, then None, which hides Karpo on that display. At least one display attached
// shows Karpo, so the last one that does is offered no None (Config.CanHide says whether another
// still shows it); a display that shows None already keeps it in its list, so the dropdown can say so.
//
// The same list is the bar's own in its menu in edit mode, its Layouts level (BarLooks.Layouts, §309,
// §361: each layout's "Show on this display" and None).

namespace Karpo.Core;

/// <summary>What choosing an entry of a display's dropdown does.</summary>
public enum DisplayPick
{
    /// <summary>Nothing: the entry it shows already, or none.</summary>
    Nothing,

    /// <summary>The display shows the layout at that place in the list, and Karpo there if it was hidden.</summary>
    Show,

    /// <summary>None: Karpo is hidden on the display.</summary>
    Hide,

    /// <summary>None on the last display that shows Karpo: refused, the dropdown goes back.</summary>
    Refused,
}

/// <summary>The entries of a display's dropdown, the one it shows, and what a choice does.</summary>
public static class DisplayLayoutChoice
{
    /// <summary>
    /// The entries: <paramref name="layouts"/>' names in their order, then None — where Karpo may be
    /// hidden (<paramref name="canHide"/>), or is already (not <paramref name="shown"/>).
    /// </summary>
    public static List<string> Choices(IEnumerable<string> layouts, bool shown, bool canHide)
    {
        var names = layouts.ToList();
        if (canHide || !shown)
        {
            names.Add(S.SettingsDisplaysNone);
        }
        return names;
    }

    /// <summary>
    /// The entry the dropdown shows: the layout the display shows (<paramref name="layout"/>, its
    /// place among <paramref name="count"/>, the first where it is none of them), or None, after them,
    /// where Karpo is hidden.
    /// </summary>
    public static int IndexOf(int layout, int count, bool shown) => shown ? Math.Clamp(layout, 0, Math.Max(0, count - 1)) : count;

    /// <summary>
    /// What choosing the entry <paramref name="index"/> does, where the dropdown shows <paramref name="now"/>
    /// and there are <paramref name="count"/> layouts: a layout is shown, None hides Karpo there - unless
    /// it can't be hidden (<paramref name="canHide"/>), the last display that shows it.
    /// </summary>
    public static DisplayPick Choose(int index, int now, int count, bool canHide) =>
        index < 0 || index == now ? DisplayPick.Nothing
        : index < count ? DisplayPick.Show
        : canHide ? DisplayPick.Hide
        : DisplayPick.Refused;
}
