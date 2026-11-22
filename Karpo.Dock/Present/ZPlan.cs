// The z-order plan (docs\widgets.md §359; the flicker design's R3, its last part): where each of
// Karpo's windows stands is not asked window by window — a raise here, a "keep over" there, a folder
// putting itself back as Windows moves it — but follows from what it is. Every window has a stratum,
// and the plan of the windows in sight is their strata, bottom first:
//
//   other apps < the dims < an arranged folder < the bars < the edit bars < popups (a folder at rest,
//   the calendar, quick settings, the window list) < menus < tooltips < what is carried
//
// so the edit bar is over the folder it arranges, the bars over an arranged folder, the folder over its
// dim, a menu over the folder it opened from, a tooltip over its menu, a carried block over all. In a
// stratum, a rank (a place with only its edge's band stands under its display's places that hold
// something), then the one shown or brought to the front last on top. The plan is computed as a frame
// ends and applied as one DeferWindowPos batch, each window just under the one before it, the first
// over every topmost window — Karpo's over another app's always-on-top ones, as each raise put them.
//
// Pure: WindowTable computes it, ZPlanTests holds every rule.

using TerraFX.Interop.Windows;

namespace Karpo.Dock;

/// <summary>What a window of Karpo's is, for where it stands in the z-order (<see cref="ZPlan"/>), bottom first.</summary>
internal enum Stratum
{
    /// <summary>Edit mode's dim (EditShade): over other apps, under everything edit mode shows.</summary>
    Dim,

    /// <summary>A folder arranged in edit mode (FlyoutHost.Spread): over its dim, under the bars and the edit bars (§279, §329).</summary>
    ArrangedFolder,

    /// <summary>The bars and the places along the edges.</summary>
    Bar,

    /// <summary>The displays' edit bars and the sections' edit bars, over the bars and an arranged folder.</summary>
    EditBar,

    /// <summary>A popup: a folder at rest, the calendar, quick settings, the window list.</summary>
    Popup,

    /// <summary>A menu and its submenus, over what it opened from.</summary>
    Menu,

    /// <summary>A tooltip, over what it is for.</summary>
    Tip,

    /// <summary>What goes with the pointer: a carried block, the selection rectangle, a carried tray icon.</summary>
    Carry,
}

/// <param name="Window">The window.</param>
/// <param name="Stratum">What it is.</param>
/// <param name="Rank">In its stratum: a higher rank stands over a lower one.</param>
/// <param name="Front">When it came into sight or was brought to the front: the latest on top of its stratum and rank.</param>
internal readonly record struct Layered(HWND Window, Stratum Stratum, int Rank, long Front);

internal static class ZPlan
{
    /// <summary>The windows of <paramref name="layered"/> top first: by stratum, then rank, then the latest brought to the front.</summary>
    public static List<HWND> Of(IEnumerable<Layered> layered) =>
        [.. layered
            .OrderByDescending(entry => entry.Stratum)
            .ThenByDescending(entry => entry.Rank)
            .ThenByDescending(entry => entry.Front)
            .Select(entry => entry.Window)];

    /// <summary>
    /// What <paramref name="window"/> goes just under in <paramref name="plan"/> (top first): the window
    /// planned over it; <see cref="HWND.HWND_TOPMOST"/> for the top one, or one the plan does not hold.
    /// </summary>
    public static HWND After(IReadOnlyList<HWND> plan, HWND window)
    {
        for (var at = 1; at < plan.Count; at++)
        {
            if (plan[at] == window)
            {
                return plan[at - 1];
            }
        }
        return HWND.HWND_TOPMOST;
    }

    /// <summary>Whether two plans stand the same windows in the same order.</summary>
    public static bool Same(IReadOnlyList<HWND> a, IReadOnlyList<HWND> b) => a.Count == b.Count && a.SequenceEqual(b);
}
