// Which of Karpo's flyouts a widget's click opens (docs\widgets.md §6, stage 5).
//
// There were two switches on IslandView and they were the same table twice: one
// in Dock.IslandClick for a widget on the bar, one in Dock.FolderForward for a
// widget inside a folder, each calling the same Show methods with a different
// island. They had already drifted — the folder's copy had no arm for the
// clock, the notifications, a folder or any of the quick-settings pages, and
// fell through to handing the action back to the folder, which re-entered the
// first switch with the folder's own island rather than the widget's. Two
// tables that must agree is the shape this whole refactor exists to remove.
//
// So there is one table, keyed by the taskbar entry (BarKind), and it is handed
// the island that owns the action. Only a taskbar entry opens anything: a line of
// a list answers with CellAct, which has no member for it, so
// no folder can reach this table. The entry's descriptor says a click opens a
// flyout (BarEntry.Click) and this says which; a test holds the two together, so
// an entry that claims a flyout and has none fails, and so does a flyout nobody
// claims.
//
// The table lives here, in the dock, and not on the descriptor: a flyout is a
// dock type and the registry is shared with the settings app, which must never
// see one. The descriptor carries the question; the dock owns the answer.

using Karpo.Core;

namespace Karpo.Dock;

public sealed unsafe partial class Dock
{
    /// <summary>
    /// What the dock does when a widget's click asks to open something. <paramref name="island"/>
    /// is the widget whose click it was — the one on the bar, or the one in the folder — and
    /// <paramref name="hit"/> is the cell to open it at, which for a folder's widget is the
    /// folder's own cell on the bar.
    /// </summary>
    private delegate void OpenWidget(Dock dock, DockWindow window, Hit hit, IIsland island, int index);

    /// <summary>
    /// Every taskbar entry whose click opens one of Karpo's own flyouts, and which one. No
    /// fall-back arm: an entry that is not here opens nothing through this path, and saying so is
    /// the point — an arm that handed a new kind the nearest flyout is the defect this project has
    /// paid for four times over.
    /// </summary>
    private static readonly (BarKind Kind, OpenWidget Open)[] Flyouts =
    [
        // folders: the list of widgets. A folder is never inside a folder, so the island is the
        // one on the bar and the hit is its own cell.
        (BarKind.FolderList, static (dock, window, hit, island, index) => dock.ShowFolder(window, hit)),
        // quick: Karpo's own quick-settings pages. The network's and Bluetooth's are lines of their
        // presets' folders (QuickPageTile), the sound panel's regions of the sound preset's, and the
        // battery's and power button's rows of the battery and power presets'.
        // quick: Win+P's modes, a page of Karpo's own in place of Windows' panel (Win+P). Cast opens
        // Windows' own panel (Win+K) and the mobile hotspot's page is a line of its preset's folder.
    ];

    /// <summary>
    /// The flyout <paramref name="kind"/>'s click opens, or null where it opens none. Null rather
    /// than a stand-in, on the rule in docs\widgets.md §5: an entry nobody has answered for is a
    /// mistake, and it has to look like one.
    /// </summary>
    private static OpenWidget? FlyoutFor(BarKind kind)
    {
        foreach (var (candidate, open) in Flyouts)
        {
            if (candidate == kind)
            {
                return open;
            }
        }
        return null;
    }

    /// <summary>Whether a taskbar entry of <paramref name="kind"/> has a flyout of Karpo's own.</summary>
    internal static bool HasFlyout(BarKind kind) => FlyoutFor(kind) is not null;

    // --- The folder rule (docs\widgets.md §15) ------------------------------------
    //
    // In the user's words: "generally a widget cannot be clicked in any folder view. with this i
    // mean that no flyout should open when the widget is clicked."
    //
    // It is not a check here any more. This table is keyed by the taskbar entry, and a line of a
    // list is not one: what its click may do is its own descriptor's CellAct,
    // which has no member that opens anything. The folder carries out an app, which is what a pin
    // is for, and nothing else (FolderFlyout.Forwards); a switch flips its radio and a graph opens
    // Task Manager inside their own click; and a widget's own controls — a slider dragged, a media
    // button pressed — are not clicks on the widget and never reach a table at all.

    /// <summary>
    /// Opens the flyout the taskbar entry at <paramref name="hit"/> opens, for
    /// <paramref name="island"/>, the widget whose click it was. True when the table answered for
    /// it; false when the entry opens none, and the caller goes on to whatever else the action
    /// carried.
    /// </summary>
    private bool OpenWidgetFlyout(DockWindow window, Hit hit, IIsland island, int index)
    {
        if (FlyoutFor(window.KindAt(hit)) is not { } open)
        {
            return false;
        }
        open(this, window, hit, island, index);
        return true;
    }
}
