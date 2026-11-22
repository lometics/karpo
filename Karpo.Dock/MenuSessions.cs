// menus: an open menu is a session of a menu by what it is, not of the bar window it opened from (docs\widgets.md
// §356; the flicker design's §5.4):
//
//  - **A window made again** under it (another edge, type or way of showing, a display change: Dock.Arrange)
//    hands it on to the window that stands for its display now (Successor), and it stays open, its rows read
//    again from the config (Dock.MenuAgain). It closed with the old window before.
//  - **Another language** reads its rows again in the new words, its levels found by their rows' ids; it closed
//    and ⚙ opened again at its top before.
//  - **Closed**, it is over: opened again it is at its top (§374, the person, 2026-10-28: "the settings menus
//    dont always open at the root level although they should"). Only a menu the dock itself closed for a dialog
//    of Windows' and shows again comes back where it stood (Dock.Resume).

namespace Karpo.Dock;

internal static class MenuSessions
{
    /// <summary>
    /// §356: the window a menu opened from <paramref name="gone"/>, a window going away, stands on once the
    /// windows are <paramref name="next"/>: its display's (<paramref name="display"/>) window holding the
    /// display's strip (<paramref name="holds"/>: its bar, or its anchor), else any of its display's; none where
    /// the display has none — the menu then closes.
    /// </summary>
    public static T? Successor<T>(T gone, IReadOnlyList<T> next, Func<T, string> display, Func<T, bool> holds)
        where T : class
    {
        var mine = next.Where(window => string.Equals(display(window), display(gone), StringComparison.OrdinalIgnoreCase)).ToList();
        return mine.FirstOrDefault(holds) ?? mine.FirstOrDefault();
    }
}
