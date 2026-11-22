// The app buttons as Windows 11's taskbar has them, the rules in one place:
//
// - "Combine taskbar buttons and hide labels" (Karpo.Core\TaskbarApps.cs): always, an app each
//   with its icon alone; never, a window each saying its title (Label); when taskbar is full, a
//   window each until they leave buttons behind "…", then an app each (Combines, Lay). One
//   setting for every bar: the main display's and the others' combine alike, through Lay.
// - A window each keeps the order the windows were first seen in; a pin with windows open stands as
//   a button per window in its own place, and with none open as its icon alone (Dock.Expanded).
// - Every bar shows every open window, wherever the open apps stand: Windows' "Show my taskbar apps
//   on" is not a setting here, and its "All taskbars" is what the bar does (Dock.TilesFor).
// - A labelled button is the whole cells its title needs, two to four. The bar's free room is the
//   buttons' — a pin's one cell or the open apps' untouched most never holds them (Layout.ButtonRoom)
//   —, and where it runs short the labelled buttons narrow alike, the longest first, down to two
//   cells (Layout.EvenCut); only then do the last buttons go behind "…" — all before any widget
//   gives way. "…" stands at the end of the open apps, else where the last pin would
//   (Layout.FitTiles).
//
// What needs no window to hold it is here; the rest is the layout's and the dock's (Dock.Pins.cs).

using Karpo.Core;

namespace Karpo.Dock;

internal static class AppButtons
{
    /// <summary>
    /// What a window's own button says beside its icon: its title, as Windows 11's taskbar labels a
    /// window, or its app's name where the window has none.
    /// </summary>
    internal static string Label(string? title, string app) =>
        string.IsNullOrWhiteSpace(title) ? app : title.Trim();

    /// <summary>
    /// Whether a bar shows an app each rather than a window each: always where they are combined
    /// always, never where they never are, and "when taskbar is full" exactly when a window each left
    /// buttons behind "…" (<paramref name="overflowed"/>) — the one test the bar has for being full,
    /// asked after the labels gave up what they could.
    /// </summary>
    internal static bool Combines(ButtonCombine mode, bool overflowed) => mode switch
    {
        ButtonCombine.Never => false,
        ButtonCombine.WhenFull => overflowed,
        _ => true,
    };

    /// <summary>
    /// What a bar shows, laid out by <paramref name="lay"/>: <paramref name="each"/>, its buttons a
    /// window each, where it has them and they are not combined, else <paramref name="apps"/>, an app
    /// each — which, where they combine "when taskbar is full", is found by laying the bar out a window
    /// each first and seeing buttons go behind "…" (<see cref="Combines"/>). <c>Full</c> says that
    /// happened. No list a window each is a bar whose buttons combine always.
    /// </summary>
    internal static (T Shown, Layout Layout, bool Full) Lay<T>(ButtonCombine mode, T? each, T apps, Func<T, Layout> lay)
        where T : class
    {
        if (each is null || ReferenceEquals(each, apps))
        {
            return (apps, lay(apps), false);
        }
        var layout = lay(each);
        return Combines(mode, layout.Overflowed.Count > 0) ? (apps, lay(apps), true) : (each, layout, false);
    }
}
