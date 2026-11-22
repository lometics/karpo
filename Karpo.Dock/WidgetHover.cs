// quick: opening a widget's flyout by resting the pointer on it, as rules rather than as timers.
//
// Off unless somebody asks for it (a layout's OpenOnHover, §345, §514). With it on, the pointer resting on a
// widget that has one of Karpo's flyouts opens that flyout after a wait, the pointer sliding to the
// next such widget swaps it at once, and it closes a moment after the pointer has left both the
// widget and the panel. A flyout a *click* opened is pinned: it is nobody's to close by moving the
// mouse away, which is what makes clicking still work exactly as it did.
//
// Rules rather than timers, for one reason: the dock's side of a hover is a handler, two timers
// and a close check, and that is where rules drift apart. Everything here is pure, so the tests can
// hold it without a bar, and Dock only arms timers and opens windows. (The desktop buttons had a
// set of these of their own until they became one button that opens a list on a click.)
//
// Q7 (§334): a folder on the bar opens on a rest by the same rules and §514 by the same switch as a widget's flyout
// (Opens): what a widget opens is one thing, a folder or a flyout (§346 to §515 the folders had a switch of their own).

using Karpo.Core;

namespace Karpo.Dock;

/// <summary>What the pointer coming to rest somewhere does to a hover-opened widget flyout.</summary>
public enum WidgetHoverAct
{
    /// <summary>Leave the flyout and the timers as they are (the timers are killed: nothing is waiting).</summary>
    Nothing,

    /// <summary>Nothing of ours is up: wait for the pointer to rest, then open.</summary>
    Arm,

    /// <summary>Ours is up for another widget: show this one's at once, without the wait.</summary>
    Switch,

    /// <summary>The pointer is no longer on the widget: stop waiting, and let ours go after the grace.</summary>
    Leave,
}

/// <summary>
/// The rules hover-opened widget flyouts follow. Every method is pure: the state it needs is passed
/// in, and what comes back is what the dock should do.
/// </summary>
public static class WidgetHover
{
    /// <summary>
    /// A pointer resting on a widget opens its flyout after this long: Windows' hover time.
    /// Long enough that crossing the bar on the way somewhere else opens nothing. The window list
    /// waits far less (PreviewHover.OpenMs), only as long as a pointer passing over a button takes.
    /// </summary>
    public const uint OpenMs = 400;

    /// <summary>
    /// The flyout closes this long after the pointer left both the widget and the panel: enough to
    /// cross the gap between them, or to clip a corner on the way in, without it going.
    /// </summary>
    public const uint CloseMs = 350;

    /// <summary>
    /// What the pointer arriving at <paramref name="hit"/> means.
    /// <paramref name="on"/> is the setting and <paramref name="editing"/> a bar being arranged:
    /// with either against it nothing opens, and anything hover opened goes.
    /// <paramref name="opens"/> is whether the widget under the pointer has a flyout of Karpo's at
    /// all — one that hasn't opens nothing and closes nothing.
    /// <paramref name="shown"/> is the widget whose flyout <em>hover</em> opened, null when none did:
    /// one a click opened is pinned and is not here, so the pointer never closes it.
    /// <paramref name="otherPopup"/> is any other popup having the screen (a menu, a jump list, a
    /// flyout a click pinned): a hover never opens a second one over it, as on the taskbar
    /// (docs\widgets.md §14; <c>Dock.AnyPopupOpen</c> is what counts as one).
    /// </summary>
    public static WidgetHoverAct Pointer(bool on, bool editing, bool opens, Hit? hit, Hit? shown, bool otherPopup)
    {
        if (!on || editing || hit is not { } over || !opens)
        {
            // Off the widget (or off altogether): what hover opened goes, and nothing is waiting.
            return shown is null ? WidgetHoverAct.Nothing : WidgetHoverAct.Leave;
        }
        if (shown is { } already)
        {
            // On the one that is open: it stays. On the next one along: swap at once, as the
            // taskbar's thumbnails do between tiles.
            return already == over ? WidgetHoverAct.Nothing : WidgetHoverAct.Switch;
        }
        return otherPopup ? WidgetHoverAct.Nothing : WidgetHoverAct.Arm;
    }

    /// <summary>
    /// Whether the hover-opened flyout stays when its grace runs out: the pointer is on the panel
    /// itself (<paramref name="insidePanel"/>) or back on the very widget it belongs to
    /// (<paramref name="onOpener"/>). Anything else lets it go.
    /// </summary>
    public static bool Stays(bool insidePanel, bool onOpener) => insidePanel || onOpener;

    /// <summary>
    /// Q7 (§334), §514: whether a rest on a widget of a bar of <paramref name="layout"/> opens what it opens: by the layout's one
    /// switch (<see cref="TaskbarLayout.OpenOnHover"/>), for whatever a widget opens alike — a folder with something to show
    /// (<paramref name="folderShows"/>, OneFolder.Opens), any other widget's flyout of Karpo's (<paramref name="hasFlyout"/>). §334
    /// to §515 the folders and the widgets' flyouts had a switch each.
    /// </summary>
    public static bool Opens(TaskbarLayout layout, bool folder, bool folderShows, bool hasFlyout) =>
        layout.OpenOnHover && (folder ? folderShows : hasFlyout);

    /// <summary>Whether anything opens on a rest on a bar of <paramref name="layout"/> at all (<see cref="Pointer"/>'s <c>on</c>).</summary>
    public static bool On(TaskbarLayout layout) => layout.OpenOnHover;
}
