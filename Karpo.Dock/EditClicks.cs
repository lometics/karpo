// edit: a click on a widget while the bar is arranged selects it, and only that (the person,
// 2026-10-12: "the application looses focus when the settings window is opened automatically, so
// keyboard shortcuts dont work anymore then"). A click opened the widget's page in Karpo's settings
// once (2026-10-04), and the settings window came to the front with every click, so Delete, Ctrl+C,
// Ctrl+V and the arrows went to it and no longer to the bar. Now:
//
//  - **A click selects, and the settings panel follows** (§520 decision 3). On the bar and in a folder
//    standing open alike, by the pointer, by Space or Enter on the widget with the keyboard's focus,
//    and by UI Automation's Invoke (Narrator): it is selected, nothing pops up, and the settings panel,
//    where it stands open, shows its page (§407 to §519 its menu beside it).
//  - **A click on a folder's face opens it too** (§527, the person, 2026-11-07: "when i click on a
//    folder in edit mode it still does not open it, it only opens the settings, but it should toggle
//    open the folder too"): selected and opened arranged in place, the panel on its Folder page; its
//    face clicked again closes it (§252, OnLocked). Space, Enter and Invoke the same; Ctrl or Shift
//    held, the selection alone. §481 to §526 a click selected it, and its face's "Open" or a
//    double-click opened it; there is no "Open" any more.
//  - **A right click opens the menu beside the widget** (the person, 2026-10-19: "the separate right
//    click menus in the edit mode should be removed"; §214): the same one menu, a folder's too
//    (OnRight), selecting it; the menu key and
//    Shift+F10 the same. On the room round a bar's widgets the bar's own looks (§290, the person: "when i
//    right click in edit mode onto the taskbar all the appearance settings should appear in the context
//    menu"; Karpo.Core\BarLooks.cs); none in a folder's.
//  - **No settings window follows, none opens.** A widget's settings are its menu's (§228: "the
//    settings for the widgets in the main application should be removed"): the settings app has no
//    page of a widget's any more to follow the selection to (2026-10-12 to §226 it went there
//    quietly).
//
// A click is a press and a release that never left the system's drag threshold (SM_CXDRAG,
// SM_CYDRAG), the same threshold and the same side of it as every other drag of Karpo's
// (PastThreshold): past it the widget is being moved, and moving is all it does. A
// press on a folder card's corner is a resize from the start, and never a click.
//
// What a click on a block does besides selecting it (OnClick): a folder's face opens the folder, and
// nothing more on any other widget. A click on the widget whose menu (its right click's) stands open
// closes the menu and does nothing else; a click on another closes it and does that one's, in the one
// click.
//
// While a folder stands open arranged the bars are locked under it (§212), but a press on them is still
// the press it is on a bar not locked, the folder closed first as by Escape (the person, 2026-10-22:
// "its not possible to click another folder while one folder is open in edit mode"; §289; 2026-10-26:
// "when clicking away on the taskbar it should close the current folder if one is open", §366): another
// folder opens in the one click (§527), any other widget is selected (§287), the room
// round the widgets and the resize grip close the folder alone — edit mode stays. The open folder's own
// face closes it, and nothing else (OnLocked).
//
// The edit bars' buttons, the bar's resize grip and a folder's own chrome (its "+", steppers, titles
// toggle) keep their own clicks: none of them is a widget.
//
// A double-click is two clicks (the person, 2026-10-22: "double clicking a widget should not open its
// context menu, it should just behave like two normal clicks behind each other"). §213 to §276 told a
// double-click's second half apart (EditClickGuard); §481 to §526 a folder's second opened it. On a
// folder (§527) the first opens it and the second, on its face, closes it.
//
// The widget clicked is the selected one too (the person, 2026-10-05: "it should also show the
// widget as selected"): its block on the bar, or its card or line in a folder standing open, wears
// the accent's ring, as Windows 11 rings a selected item, until another widget is clicked or edit
// mode ends (Dock.EditPick, SelectedPart, FolderFlyout.Picked). The keyboard's focus rectangle is
// another thing, the keys' alone, and goes over the ring where both are on one widget.
//
// Plain code without windows, so the tests can reach it.

using Karpo.Core;
using TerraFX.Interop.Windows;

namespace Karpo.Dock;

/// <summary>What a click on a widget while the bar is arranged does besides selecting it (<see cref="EditClicks.OnClick"/>).</summary>
public enum EditClickAct
{
    /// <summary>Nothing: its press closed the widget's menu, which is all it does.</summary>
    None,

    /// <summary>It is selected, and nothing pops up: the settings panel follows (§520 decision 3).</summary>
    Select,

    /// <summary>§527: a folder's face: selected, and the folder opens arranged in place; its face closes it again (<see cref="EditClicks.OnLocked"/>).</summary>
    Open,
}

/// <summary>What a press and its release on the dim does in edit mode (<see cref="EditClicks.OnFrame"/>); edit mode goes on after each.</summary>
public enum FrameClick
{
    /// <summary>Nothing: a click with Ctrl or Shift held, which keeps the selection.</summary>
    Nothing,

    /// <summary>A drag, which selected the widgets its rectangle ran over as it went.</summary>
    Selected,

    /// <summary>Nothing is selected any more.</summary>
    Unselect,
}

/// <summary>What a left press while the bar is arranged begins (<see cref="EditClicks.OnPress"/>).</summary>
public enum EditPress
{
    /// <summary>The bar's resize grip: its height dragged.</summary>
    Resize,

    /// <summary>A widget, or a button of the frame: it takes the press — a widget is carried from here.</summary>
    Own,

    /// <summary>Empty room: the selection rectangle, past the drag threshold; a click clears the selection.</summary>
    Select,
}

/// <summary>What a key does to the block with the keyboard's focus while the bar is arranged.</summary>
public enum EditKeyPress
{
    /// <summary>Nothing of a click's or a menu's.</summary>
    Nothing,

    /// <summary>What a click on the block does (<see cref="EditClicks.OnClick"/>): it is selected.</summary>
    Click,

    /// <summary>Enter: what a click does, as Space.</summary>
    Open,

    /// <summary>The block's menu.</summary>
    Menu,
}

/// <summary>The rules of a click on a widget while the bar is arranged; see the file's head.</summary>
/// <summary>What a right click on an arranged bar opens (<see cref="EditClicks.OnRight"/>).</summary>
public enum EditRight
{
    /// <summary>Nothing: the press closed the widget's menu, which is all it does.</summary>
    None,

    /// <summary>The widget's menu, beside it.</summary>
    Widget,

    /// <summary>The bar's looks (Karpo.Core's BarLooks, §290).</summary>
    Bar,
}

public static class EditClicks
{
    /// <summary>
    /// What <paramref name="key"/> does to the block with the focus: Space and Enter press it as a
    /// click does (it is selected, a folder opened, §527) — each once however long
    /// held (<paramref name="repeat"/>); the menu key and Shift+F10 open its menu beside it, as a right
    /// click does.
    /// </summary>
    public static EditKeyPress OnKey(int key, bool shift, bool repeat) => EditKeys.Of(key, control: false, shift, repeat).Act switch
    {
        EditKeyAct.Open => EditKeyPress.Open,
        EditKeyAct.Click => EditKeyPress.Click,
        EditKeyAct.Menu => EditKeyPress.Menu,
        _ => EditKeyPress.Nothing,
    };

    /// <summary>
    /// Whether UI Automation's <paramref name="act"/> on an entry of <paramref name="kind"/> is a
    /// click on its block while the bar is arranged: pressed or switched (Narrator's primary
    /// action), it does what a click does and never the widget's own act — a switch is not switched
    /// while the bar is arranged. Focus, values and the menu go their own ways.
    /// </summary>
    public static bool ByAutomation(UiaAct act, HitKind kind) =>
        act is UiaAct.Invoke or UiaAct.Toggle;

    /// <summary>
    /// Whether a press and its release (<paramref name="dx"/>, <paramref name="dy"/> px apart, and
    /// never further on the way) is a click rather than a drag: inside the system's drag threshold
    /// (<paramref name="slopX"/>, <paramref name="slopY"/>), on the side a press that does not
    /// move stands on.
    /// </summary>
    public static bool IsClick(int dx, int dy, int slopX, int slopY) =>
        !PastThreshold(dx, dy, slopX, slopY);

    /// <summary>
    /// Whether the pointer has moved past the system's drag threshold, (<paramref name="slopX"/>,
    /// <paramref name="slopY"/>) px from where the press went down: past it, a press is a drag. The
    /// same threshold, and the same side of it, as every other drag of Karpo's.
    /// </summary>
    public static bool PastThreshold(int dx, int dy, int slopX, int slopY) =>
        Math.Abs(dx) > slopX || Math.Abs(dy) > slopY;

    /// <summary>
    /// What a press and its release on the dim does in edit mode (Dock.ShadeMessage):
    /// a drag past the threshold (<paramref name="dragged"/>) has selected what it ran over; a click
    /// with Ctrl or Shift held (<paramref name="adding"/>) keeps the selection; any other click clears
    /// it, as a click on the desktop's empty room does in Windows. None of them ends edit mode (the
    /// person, 2026-10-07: "remove that you go out of edit mode if you click into the middle").
    /// </summary>
    public static FrameClick OnFrame(bool dragged, bool adding) =>
        dragged ? FrameClick.Selected
        : adding ? FrameClick.Nothing
        : FrameClick.Unselect;

    /// <summary>
    /// What a left press while the bar is arranged begins: on the bar's resize grip
    /// (<paramref name="grip"/>), a resize; on a widget or a button that takes it
    /// (<paramref name="taken"/>), that — a widget is carried, as before; anywhere else — the room
    /// between and round the widgets and the sections, the bar's own background, a folder's room
    /// between and round its lines — the selection rectangle, as on the dim (the person, 2026-10-12:
    /// "the lasso select should also work inside the taskbar, right now it only works if started
    /// outside").
    /// </summary>
    public static EditPress OnPress(bool grip, bool taken) =>
        grip ? EditPress.Resize
        : taken ? EditPress.Own
        : EditPress.Select;

    /// <summary>
    /// edit: whether a press on part <paramref name="part"/> of a bar locked under a folder open
    /// arranged (§212) is on that folder's face, part <paramref name="closing"/>, whose click closes
    /// it (§251), drawn as itself (§267). Anywhere else on the bars a press closes the folder, as by
    /// Escape, and goes on as on a bar not locked — the rule a right click keeps (§287, §289, §366).
    /// </summary>
    public static bool OnLocked(int part, int closing) => closing >= 0 && part == closing;

    /// <summary>Whether the box <paramref name="left"/>..<paramref name="bottom"/> (screen px) and <paramref name="area"/> overlap: what a selection rectangle selects.</summary>
    public static bool Overlaps(RECT area, float left, float top, float right, float bottom) =>
        left < area.right && right > area.left && top < area.bottom && bottom > area.top;

    /// <summary>
    /// What a click on a block of <paramref name="kind"/> does — the pointer's, Space's, Enter's, Narrator's Invoke: it is
    /// selected and the settings panel follows (§520), a folder's face opened too (§527); the folder open, its face's press
    /// closes it before any click (<see cref="OnLocked"/>). Where its press closed the widget's own menu
    /// (<paramref name="closedItsMenu"/>) nothing, as a second click on what opened a menu does in Windows.
    /// </summary>
    public static EditClickAct OnClick(BarKind kind, bool closedItsMenu) =>
        closedItsMenu ? EditClickAct.None
        : kind.IsFolder() ? EditClickAct.Open
        : EditClickAct.Select;

    /// <summary>
    /// What a right click opens on an arranged bar (the person, 2026-10-19: "the separate right click
    /// menus in the edit mode should be removed"; §214): on a widget (<paramref name="onWidget"/>) its
    /// one menu, beside it, selecting it alone where it was not selected — on one of several selected,
    /// which stay so as Windows' lists keep a selection a right click lands in, the menu for them all —
    /// a folder's too, its face's (§481); but on the widget whose menu its press
    /// closed (<paramref name="closedItsMenu"/>) nothing more. On the room round the widgets the bar's
    /// own looks (§290: "when i right click in edit mode onto the taskbar all the appearance settings
    /// should appear in the context menu").
    /// </summary>
    public static EditRight OnRight(bool onWidget, bool closedItsMenu) =>
        !onWidget ? EditRight.Bar : closedItsMenu ? EditRight.None : EditRight.Widget;

    /// <summary>
    /// Which of a bar's parts, keyed <paramref name="keys"/> in its order, is the selected widget
    /// keyed <paramref name="picked"/> — the one last clicked while the bar is arranged, which wears
    /// the selected look until another is clicked or edit mode ends; null where none is, as for a
    /// widget in a folder (its folder's card or line wears it there) or one gone from the bar. By
    /// key, so the selection keeps to its widget through every rebuild that moves the parts.
    /// </summary>
    public static int? SelectedPart(string? picked, IReadOnlyList<string> keys)
    {
        for (var part = 0; picked is not null && part < keys.Count; part++)
        {
            if (string.Equals(keys[part], picked, StringComparison.OrdinalIgnoreCase))
            {
                return part;
            }
        }
        return null;
    }
}
