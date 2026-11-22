// A folder's lists of widgets, side by side, each a panel of Karpo's popup chrome
// of its own (FlyoutHost.Pieces, Menu mode, one window for all of them), and no
// title (a folder has no name, §427). It opens at the
// folder's cell on the bar, takes the focus, and closes on a click elsewhere, on
// Escape, or on a second click on the folder; while its bar is arranged a click
// elsewhere leaves it open (the dock: one folder open at a time, in edit mode too).
// The folder's own settings are not repeated here: its right-click menu has them,
// as every widget's does.
//
// Each list is as wide as its own preset (narrow to widest), whatever the display: a folder too
// wide for it runs past its edge, and arranged says so (§320). Each widget gets a line as
// wide as its list, at least 40 DIPs tall: a component its own drawing (CellIsland.PaintOn),
// anything else its icon and its name. A list's panel is as tall as what is in it,
// up to the room beside the bar to the work area's far end less the flyouts' margin (Reach);
// the lines past that are not shown,
// whole ones from the list's end, and nothing scrolls — arranged, a warning button
// beside the list's width button says which, as the bar's edit bar does. The lists line up on the
// bar's side, a folder separator's room apart (SeparatorRoom, §346). In edit mode round buttons stand outside
// the panels — at each list's own end away from the bar, its "+", its width and its warning — and
// nothing else of the lists changes: the lines stand where they stand at rest. A column's "+" opens
// the add list for that column (the person, 2026-10-19: "add back the add button per column"; §246),
// as a menu's "Add" does for the selected line's column, else the first with room; no column is
// picked by a click (2026-10-19: "a column should not be selectable in general"). Arranged, the
// window reaches the display's edges, under its bars, and the folder's columns are drawn out
// across it (the person, 2026-10-12; 2026-10-19: "i want them to continue all the way through"):
// thin guide lines down the screen at every column's left and right edge, ending at the bars'
// edges (2026-10-22: "the column lines in edit mode still go over the taskbar, this should not
// be"; §287), and empty slots of a medium column's width repeated out to the work area's edges
// either side, each its guide lines, its "+" and width button and a place to drop a line — dropping
// into one, adding into it, or giving it a width, makes it a column there (FolderSlot). Nothing adds
// or removes a column as such (the person, 2026-10-19: "all the columns are already there
// (conceptually), you can just change their width"; §220). No close button (2026-10-22: "remove
// the close button in folder edit mode"; §287): its face on the bar, Escape, or a right click on a
// widget of the bar closes it.
//
// Hover, press and the keyboard focus only recolour or move composition
// shapes: moving the pointer draws nothing. A widget with news, and the
// flyout's own timer, draw the lines again.
//
// While it is open a timer ticks the widgets (FolderIsland.TickWidgets): every
// second when a clock in it shows seconds, else at each minute's start, and
// sooner for a widget whose interval is shorter. Nothing runs while it is closed.

using System.Numerics;
using Karpo.Core;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using Windows.UI.Composition;
using static TerraFX.Interop.Windows.Windows;
using Color = Windows.UI.Color;
using Edge = Karpo.Core.Edge;

namespace Karpo.Dock;

public enum FolderFlyoutAction
{
    None,

    /// <summary>The folder closed by itself: Escape, a click elsewhere, another window taking the focus.</summary>
    Dismissed,

    /// <summary>
    /// A widget's click asked for items, a view or a panel: the folder closed, and the dock shows
    /// <see cref="FolderFlyoutEvent.Action"/> at the folder's cell for <see cref="FolderFlyoutEvent.Widget"/>.
    /// </summary>
    Forward,

    /// <summary>
    /// folders: a widget was right-clicked: the dock shows its menu at <see cref="FolderFlyoutEvent.At"/>,
    /// over the folder, which stays open and holds itself open until the dock says the menu has
    /// ended (<see cref="FolderFlyout.MenuClosed"/>). edit: while the folder is arranged, by the menu
    /// key or a right click, <see cref="FolderFlyoutEvent.Field"/> is the line's card on the screen and
    /// the dock does what a click does there: selected, its one menu beside it (§214).
    /// </summary>
    Context,

    /// <summary>
    /// edit, §334: a right click on the arranged folder's own room, on no line: the dock shows the
    /// folders' look (BarLooks.Appearance) at <see cref="FolderFlyoutEvent.At"/>, as a right click on
    /// a bar's room shows the taskbar's (§290); the folder holds itself open until the menu has ended.
    /// </summary>
    Looks,

    /// <summary>
    /// pins: a widget is being dragged out of the folder, the pointer at <see cref="FolderFlyoutEvent.At"/>
    /// (screen px). The folder stays open; the dock shows where it would land on the bar under the pointer.
    /// </summary>
    DragMove,

    /// <summary>pins: the dragged widget was let go at <see cref="FolderFlyoutEvent.At"/>: the dock takes it out of the folder there.</summary>
    DragDrop,

    /// <summary>
    /// edit: a widget was carried to <see cref="FolderFlyoutEvent.Place"/> — between two lines of a
    /// list, at a list's end, or a new list of its own — and let go. The dock moves it there
    /// (Folders.Move) and saves.
    /// </summary>
    Place,

    /// <summary>
    /// edit: one of the selection's acts (<see cref="FolderFlyoutEvent.Act"/>, <see cref="EditKeys.OfTheSelection"/>) while
    /// the folder is arranged — Delete, Ctrl+X, Ctrl+C, Ctrl+V, Ctrl+A, Ctrl+Z, Ctrl+Y — which the dock does as on the bar
    /// (Dock.SelectionAct), on its lines alone: on <see cref="FolderFlyoutEvent.Widget"/>, the line the keys walked to, or
    /// every selected line where it is one of them or the keys walked to none (null). The folder stays open.
    /// </summary>
    Selection,

    /// <summary>
    /// edit: a column's "+" was pressed, or a slot's (<see cref="FolderAddKind.Add"/>): the add list
    /// opens at <see cref="FolderFlyoutEvent.At"/> (the button's side away from the bar) for
    /// <see cref="FolderFlyoutEvent.Place"/> — the list's end away from the bar, or a slot's list of
    /// its own there — where what is picked goes in at once (§246, §297). The button
    /// stands pressed until the list has gone (<see cref="FolderFlyout.MenuClosed"/>).
    /// </summary>
    Add,

    /// <summary>
    /// edit: a list's width button was pressed, or a slot's: the dock shows the three widths
    /// (<see cref="FolderListWidth"/>) for list <see cref="FolderPlace.List"/> of
    /// <see cref="FolderFlyoutEvent.Place"/>, its own ticked — for a slot
    /// (<see cref="FolderPlace.New"/>) medium, a pick making it an empty list that wide — at
    /// <see cref="FolderFlyoutEvent.At"/> (the button's side away from the bar). The folder holds itself open until the dock says the
    /// menu has ended (<see cref="FolderFlyout.MenuClosed"/>); a pick resizes the list in place.
    /// </summary>
    Width,

    /// <summary>
    /// edit: <see cref="FolderFlyoutEvent.Widget"/> was clicked while the folder is arranged — a press
    /// and release inside the drag threshold, not by its corner — or Enter or Space pressed on it: the
    /// dock opens its own page in Karpo's settings (EditClicks.cs), and the folder stays open.
    /// <see cref="FolderFlyoutEvent.At"/> is where the pointer was let go, screen px; nothing for the keys.
    /// </summary>
    Settings,

    /// <summary>
    /// edit: a part of <see cref="FolderFlyoutEvent.Widget"/>'s own card that edit mode edits was
    /// clicked while the folder is arranged (<see cref="CellIsland.CardEditTarget"/>) — a sound
    /// device, hidden or shown again (docs\widgets.md §159): the dock writes what the click changes
    /// (<see cref="CellIsland.EditClick"/>, <see cref="FolderFlyoutEvent.Part"/>) and saves it; the
    /// folder stays open.
    /// </summary>
    EditPart,

    /// <summary>
    /// A card or a line of a widget that is not set up was clicked, or Enter or Space pressed on it,
    /// at rest: the folder stays open, and the dock opens <see cref="FolderFlyoutEvent.Widget"/>'s own
    /// configuration menu over it at <see cref="FolderFlyoutEvent.At"/>, where what it needs is set
    /// (<see cref="CellIsland.Unset"/>; an account its Accounts level, §340) — a folder opens no flyout,
    /// and the card itself is the button.
    /// </summary>
    Setup,

    /// <summary>
    /// edit: a left press on the arranged folder's empty room — no line, no "+" — at
    /// <see cref="FolderFlyoutEvent.At"/> (screen px): the dock's selection rectangle may begin, as
    /// on the bar's empty room and the dim (Dock.EditSelect.cs). The folder holds the mouse until
    /// <see cref="BandReleased"/> or <see cref="BandLost"/>; it stays open throughout.
    /// </summary>
    BandPressed,

    /// <summary>edit: the pointer moved to <see cref="FolderFlyoutEvent.At"/> with the empty room's press held.</summary>
    BandMoved,

    /// <summary>edit: the empty room's press came up at <see cref="FolderFlyoutEvent.At"/>: a drag that selected, or a click.</summary>
    BandReleased,

    /// <summary>edit: the folder lost the mouse mid-drag: the rectangle goes, what it selected stays.</summary>
    BandLost,

    /// <summary>
    /// edit: Space on <see cref="FolderFlyoutEvent.Widget"/> while the folder is arranged: what a click
    /// on it does, as Space on a block of the bar (EditKeys.cs) — it is selected, an open settings
    /// window follows; nothing comes to the front.
    /// </summary>
    Select,

    /// <summary>
    /// edit: Shift and an arrow (<see cref="FolderFlyoutEvent.Part"/>) on <see cref="FolderFlyoutEvent.Widget"/> while the
    /// folder is arranged: the line a size larger or smaller, as its menu sizes it (EditResize.cs, §471).
    /// </summary>
    Resize,

    /// <summary>
    /// edit: a list's warning button was pressed (<see cref="FolderAddKind.Hidden"/>): the dock shows
    /// <see cref="FolderFlyoutEvent.Hidden"/>, the widgets that list has no room for, as the edit
    /// bar's "not shown" button does — one straight to its own menu, several listed by name — at
    /// <see cref="FolderFlyoutEvent.At"/>. The folder holds itself open until the menu has ended
    /// (<see cref="FolderFlyout.MenuClosed"/>).
    /// </summary>
    NotShown,

    /// <summary>
    /// folders: a line's dropdown was clicked, or Enter or Space pressed on it (<see cref="IslandView.Choices"/>):
    /// the dock shows <see cref="FolderFlyoutEvent.Widget"/>'s choices as a menu under its field,
    /// <see cref="FolderFlyoutEvent.Field"/>, over the folder, which holds itself open until the menu
    /// has ended (<see cref="FolderFlyout.MenuClosed"/>).
    /// </summary>
    Choices,
}

/// <param name="Kind">What happened.</param>
/// <param name="Widget">For <see cref="FolderFlyoutAction.Forward"/> and the drags: the widget it is about.</param>
/// <param name="Index">Its cell index the click stood for.</param>
/// <param name="Action">What its click asked for.</param>
/// <param name="At">For the drags and the menu: where the pointer is, in screen px.</param>
/// <param name="Place">edit, for <see cref="FolderFlyoutAction.Place"/> and <see cref="FolderFlyoutAction.Add"/>: where the widget goes; for <see cref="FolderFlyoutAction.Width"/> the list or the slot.</param>
/// <param name="Part">edit, for <see cref="FolderFlyoutAction.EditPart"/>: the part of the widget's card clicked; for <see cref="FolderFlyoutAction.Resize"/> the arrow.</param>
/// <param name="Keys">edit, for <see cref="FolderFlyoutAction.Place"/>: the lines carried together, in their order, the widget among them (FolderFlyout.Pending.cs); null for it alone.</param>
/// <param name="Hidden">edit, for <see cref="FolderFlyoutAction.NotShown"/>: the widgets the list has no room to show, in its order.</param>
/// <param name="Act">edit, for <see cref="FolderFlyoutAction.Selection"/>: which of the selection's acts.</param>
/// <param name="Field">
/// folders, for <see cref="FolderFlyoutAction.Choices"/>: the dropdown's field on the screen, which its list hangs under;
/// edit, for <see cref="FolderFlyoutAction.Settings"/>, <see cref="FolderFlyoutAction.Select"/> and <see cref="FolderFlyoutAction.Context"/>: the line's card on the screen, which its menu stands beside.
/// §576, for <see cref="FolderFlyoutAction.Add"/>: the "+" on the screen, which the add page hangs from.
/// </param>
public readonly record struct FolderFlyoutEvent(
    FolderFlyoutAction Kind,
    IIsland? Widget = null,
    int Index = 0,
    IslandAction Action = default,
    POINT At = default,
    FolderPlace Place = default,
    int Part = 0,
    IReadOnlyList<string>? Keys = null,
    IReadOnlyList<IIsland>? Hidden = null,
    RECT Field = default,
    EditKeyAct Act = EditKeyAct.None);

/// <summary>
/// edit: which round button of an open folder's column it is (<see cref="FolderAdd"/>): its "+", its
/// width and its warning, side by side in that order, centred on the column.
/// </summary>
internal enum FolderAddKind
{
    /// <summary>
    /// A list's width, or a slot's — a menu of the presets (<see cref="FolderListWidth"/>), the list's
    /// own ticked, a slot's medium (2026-10-10; a slot's 2026-10-12: "every column that you see on the
    /// screen, even if empty you should be able to change its width"). A slot's
    /// <see cref="FolderAdd.Place"/> is the slot's (<see cref="FolderPlace.Slot"/>): a width picked
    /// there makes it an empty list that wide. No column has a remove any more (2026-10-12:
    /// "deleting a column should not even be possible").
    /// </summary>
    Width,

    /// <summary>
    /// Beside a list's width button, only while the list has widgets it has no room to
    /// show (<see cref="FolderListPlan.Hidden"/>), the warning — the edit bar's "not shown" button as
    /// a round one (EditRail.Warning): Windows' warning orange, the hidden widget's own glyph or the
    /// warning's for several, its tooltip saying how many and its click their menu or a list of them
    /// (the person, 2026-10-12: "it should hide the widgets that dont fit and show a warning sign
    /// like the taskbar does it").
    /// </summary>
    Hidden,

    /// <summary>
    /// A list's "+", or a slot's (the person, 2026-10-19: "add back the add button per column"; §246):
    /// the add list for that column. Its <see cref="FolderAdd.Place"/> is where what is picked goes in
    /// at once (§297): the list's end away from the bar, or the slot's
    /// (<see cref="FolderPlace.Slot"/>), a list of its own there.
    /// </summary>
    Add,
}

/// <summary>
/// edit: a round button of an open folder's column (<see cref="FolderAddKind"/>), where it stands and
/// what it is for. Its <see cref="Box"/> is in panel px, outside every list's panel, before it is kept
/// on the screen (<see cref="FolderFlyout.KeepIn"/>).
/// </summary>
internal readonly record struct FolderAdd(D2D_RECT_F Box, FolderPlace Place, FolderAddKind Kind, int List);

/// <summary>
/// edit: an empty slot beside an arranged folder's lists (the person, 2026-10-12): a medium column's
/// width, the lists' gap from the next, repeated out to the work area's edge on either side. Its
/// <see cref="Box"/> is in panel px and as tall as the window, cut to the work area; a slot the
/// edge cuts is there where its "+" and width button still fit. Adding into it, giving it a width, or
/// dropping a line on it, makes it a list there, <see cref="Place"/> (<see cref="FolderPlace.Slot"/>): the slots nearer the lists
/// become empty lists, so the new one stands where its slot stood.
/// </summary>
internal readonly record struct FolderSlot(D2D_RECT_F Box, FolderPlace Place);

/// <summary>
/// edit: what is about to change in an arranged folder (docs\widgets.md §239), the one input the
/// folder's layout takes besides the folder (<see cref="FolderFlyout.Build"/>): <see cref="Lines"/>,
/// by their index among the folder's cells, carried — lifted by a press, or <see cref="New"/>: added,
/// pasted or copied, carried with no button and with no place of their own in the folder yet — and
/// <see cref="To"/>, where they would land: a gap of a list, a slot, or null for over no place, where
/// they are in no list. A drop, a paste, a widget added and a cut line put back all land through the
/// same move (<see cref="Folders.MoveGroup"/>), so they are laid out alike.
/// </summary>
internal readonly record struct FolderPending(IReadOnlyList<int> Lines, FolderPlace? To = null, bool New = false)
{
    /// <summary>
    /// What the folder at rest is laid out with while this is pending: new lines in no list — they are
    /// nowhere until they land, so the columns stand as they stood before they came in — and lifted
    /// ones at their places, which they leave only as the lines give way (null).
    /// </summary>
    public FolderPending? AtRest => New ? this with { To = null } : null;
}

/// <summary>
/// One list of an open folder laid out: its own panel (<see cref="Box"/>, panel px: as tall as the
/// widgets it shows, up to the most), the cards its dividers split it into (<see cref="Runs"/>), and
/// the widgets it has no room for (<see cref="Hidden"/>). A list never scrolls.
/// </summary>
internal sealed record FolderListPlan(D2D_RECT_F Box)
{
    /// <summary>
    /// The cards the list stands on, top to bottom, in panel px: a run of lines between two dividers
    /// each, the divider's gap between two (DividerTile); the whole list where it has no divider. A
    /// divider first, last or next to another parts nothing from nothing, and leaves its gap bare.
    /// </summary>
    public IReadOnlyList<D2D_RECT_F> Runs { get; init; } = [Box];

    /// <summary>
    /// The widgets of the list that do not fit under the most and are not shown, by their index in
    /// <see cref="FolderPlan.Cards"/>, in the list's order: whole lines from its end (the dividers
    /// among them left out — nothing to name). Arranged, its warning button says so
    /// (<see cref="FolderAddKind.Hidden"/>).
    /// </summary>
    public IReadOnlyList<int> Hidden { get; init; } = [];

    /// <summary>Equal where everything is, the cards one by one: a plan made twice for the same folder is the same plan.</summary>
    public bool Equals(FolderListPlan? other) =>
        other is not null && Box.Equals(other.Box) && Runs.SequenceEqual(other.Runs) && Hidden.SequenceEqual(other.Hidden);

    public override int GetHashCode() => HashCode.Combine(Box.left, Box.top, Box.right, Box.bottom, Runs.Count, Hidden.Count);

    public float Left => Box.left;
    public float Top => Box.top;

    /// <summary>How wide the list is: its own preset (<see cref="FolderOptions.WidthOf"/>); none for an empty list outside the ones with widgets at rest.</summary>
    public float Width => Box.right - Box.left;

    /// <summary>How tall the list's panel is: as much of its widgets as shows.</summary>
    public float Height => Box.bottom - Box.top;
}

/// <summary>
/// A folder's lists laid out side by side, each a panel of its own: every rectangle in panel px
/// (the box round all of them). No title (the person, 2026-10-10: "remove the folder names completely"):
/// a folder has no name (§427).
/// </summary>
internal sealed record FolderPlan
{
    /// <summary>How the widgets show: a list.</summary>
    public FolderView View { get; init; }

    /// <summary>The box round every panel of the folder: its lists.</summary>
    public required float Width { get; init; }
    public required float Height { get; init; }

    /// <summary>
    /// The box round the lists with widgets, from the first of them to the last and the empty ones
    /// between, in panel px: what is centred on the folder's button (<see cref="FlyoutHost.Core"/>;
    /// docs\widgets.md §158). An empty list outside them hangs off its side, arranged; at rest it has
    /// no width and the box is the whole panel. The whole panel where no list has a widget.
    /// </summary>
    public D2D_RECT_F Core { get; init; }

    /// <summary>A line's least height.</summary>
    public required float Side { get; init; }

    public required float Pad { get; init; }

    public required float Radius { get; init; }
    public required float Hairline { get; init; }


    /// <summary>
    /// The room between two lists of the folder, in px: the room a folder separator takes
    /// (<see cref="FolderFlyout.SeparatorRoom"/>), so two lists stand as far apart as two cards a separator
    /// splits do (the person, 2026-10-10). Nothing is drawn in it; the empty slots beside the lists stand as far apart while the folder is arranged.
    /// </summary>
    public required float ListGap { get; init; }

    /// <summary>The lists, first to last — left to right, on a right bar right to left (<see cref="FolderFlyout.FirstOnRight"/>): each one's own panel, its cards and what it has no room for.</summary>
    public required IReadOnlyList<FolderListPlan> Lists { get; init; }

    /// <summary>
    /// The folder's panels, for the host to give each its material, corners, rim and shadow
    /// (<see cref="FlyoutHost.Pieces"/>): each card of each list (<see cref="FolderListPlan.Runs"/>)
    /// that has any height.
    /// </summary>
    public IReadOnlyList<D2D_RECT_F> Pieces => [.. Lists.SelectMany(list => list.Runs).Where(run => run.bottom > run.top)];

    /// <summary>
    /// §651: the room between each two lists side by side (left to right, panel px across), where an attached folder draws
    /// its seams (<see cref="FlyoutHost.Seams"/>); a list of no width (an empty one outside at rest) none.
    /// </summary>
    public IReadOnlyList<(float Left, float Right)> Seams
    {
        get
        {
            var across = Lists.Where(list => list.Width > 0).OrderBy(list => list.Left).ToList();
            var seams = new List<(float Left, float Right)>();
            for (var at = 1; at < across.Count; at++)
            {
                if (across[at].Left > across[at - 1].Box.right)
                {
                    seams.Add((across[at - 1].Box.right, across[at].Left));
                }
            }
            return seams;
        }
    }

    /// <summary>Every widget the folder has no room to show, list by list (<see cref="FolderListPlan.Hidden"/>).</summary>
    public IEnumerable<int> Hidden => Lists.SelectMany(list => list.Hidden);

    /// <summary>
    /// Which of its list's <see cref="FolderListPlan.Runs"/> each of <see cref="Cards"/> stands on; -1 for a divider,
    /// which stands between two (§505: empty space), and a line not shown.
    /// </summary>
    public IReadOnlyList<int> CardRun { get; init; } = [];

    /// <summary>Each widget's line, in the order of the folder's tiles.</summary>
    public required IReadOnlyList<D2D_RECT_F> Cards { get; init; }

    /// <summary>Which list each of <see cref="Cards"/> is in.</summary>
    public required IReadOnlyList<int> CardList { get; init; }

    /// <summary>The line an empty list shows, faint, where its first widget would stand; nothing otherwise.</summary>
    public required IReadOnlyList<D2D_RECT_F> Empty { get; init; }

    /// <summary>The card of its list line <paramref name="index"/> stands on (<see cref="CardRun"/>); null for one standing on none.</summary>
    public D2D_RECT_F? RunOf(int index) =>
        Shows(index) && index < CardList.Count && index < CardRun.Count && CardList[index] is var list && list >= 0 && list < Lists.Count
            && CardRun[index] is var run && run >= 0 && run < Lists[list].Runs.Count
            ? Lists[list].Runs[run]
            : null;

    /// <summary>Whether card <paramref name="index"/> has a box to draw in and to be found in.</summary>
    public bool Shows(int index) =>
        index >= 0 && index < Cards.Count && Cards[index].bottom > Cards[index].top && Cards[index].right > Cards[index].left;

    /// <summary>
    /// edit: where each line is taken hold of — its card, but a divider's at least
    /// <see cref="FolderFlyout.DividerDip"/> tall, centred on its gap and laid over its neighbours'
    /// edges, since its room is narrow (<see cref="FolderFlyout.SeparatorRoom"/>).
    /// Nothing moves for it: the lines stand where they stand at rest. The cards outside edit mode.
    /// </summary>
    public IReadOnlyList<D2D_RECT_F> Grabs { get; init; } = [];

    /// <summary>
    /// Per line, arranged, a spacer's band of stripes (§524, <see cref="SpacerStripes"/>): its whole room, which would
    /// otherwise be nothing to see — a Fixed one's fixed room, a Grow one's all it grows to. Empty for any other line, a
    /// spacer not shown, and every one at rest: a separator is empty space (§505).
    /// </summary>
    public IReadOnlyList<RectF> Bands { get; init; } = [];

    /// <summary>Where line <paramref name="index"/> is taken hold of (<see cref="Grabs"/>).</summary>
    public D2D_RECT_F Grab(int index) => index >= 0 && index < Grabs.Count ? Grabs[index] : Cards[index];

    /// <summary>Whether line <paramref name="index"/> can be found under the pointer and ringed: its card shows, or its grab does.</summary>
    public bool Holds(int index) =>
        Shows(index) || (index >= 0 && index < Grabs.Count && Grabs[index].bottom > Grabs[index].top && Grabs[index].right > Grabs[index].left);

    /// <summary>
    /// edit: the columns' round buttons — each list's width and warning at its own end, each slot's
    /// width (<see cref="FolderAdd"/>). Empty outside edit mode. They stand outside the panels, so
    /// the layout is the same in edit mode and out of it.
    /// </summary>
    public required IReadOnlyList<FolderAdd> Adds { get; init; }

    /// <summary>
    /// edit: the empty slots beside the lists, left ones nearest first, then right ones nearest
    /// first (<see cref="FolderSlot"/>; <see cref="FolderFlyout.WithSlots"/>); their "+" and width
    /// buttons are the last of <see cref="Adds"/>, in the same order. None until the folder is placed
    /// on the screen.
    /// </summary>
    public IReadOnlyList<FolderSlot> Slots { get; init; } = [];

    /// <summary>
    /// edit: the lists with widgets that run past the screen's edge where the folder stands, nearest
    /// the screen first (§320; <see cref="FolderFlyout.WithSlots"/>): no width is refused for being
    /// too wide, and the arranged folder says which part is off screen (<see cref="FolderFlyout.WarningBox"/>).
    /// None at rest and until the folder is placed on the screen.
    /// </summary>
    public IReadOnlyList<int> OffScreen { get; init; } = [];

    /// <summary>
    /// edit: which of <see cref="Slots"/> the panel's x <paramref name="x"/> (px) is over, as far as
    /// <paramref name="slack"/> out either side of it; -1 for none (the room between two).
    /// </summary>
    public int SlotAt(float x, float slack = 0)
    {
        for (var slot = 0; slot < Slots.Count; slot++)
        {
            if (x >= Slots[slot].Box.left - slack && x < Slots[slot].Box.right + slack)
            {
                return slot;
            }
        }
        return -1;
    }

    /// <summary>Which list the panel's x <paramref name="x"/> (px) is over; -1 for none (the room between two).</summary>
    public int ListAt(float x)
    {
        for (var list = 0; list < Lists.Count; list++)
        {
            if (x >= Lists[list].Left && x < Lists[list].Left + Lists[list].Width)
            {
                return list;
            }
        }
        return -1;
    }

    /// <summary>Which list's panel the point <paramref name="point"/> (panel px) is on; -1 for none — the gaps, beside a shorter list.</summary>
    public int ListOn(Vector2 point) =>
        ListAt(point.X) is var list and >= 0 && point.Y >= Lists[list].Box.top && point.Y < Lists[list].Box.bottom ? list : -1;

    /// <summary>The list nearest the panel's x <paramref name="x"/> (px): the one it is over, else the closest.</summary>
    public int NearestList(float x)
    {
        var best = 0;
        var bestFar = float.MaxValue;
        for (var list = 0; list < Lists.Count; list++)
        {
            var left = Lists[list].Left;
            var right = left + Lists[list].Width;
            var far = x < left ? left - x : x >= right ? x - right : 0;
            if (far < bestFar)
            {
                (best, bestFar) = (list, far);
            }
        }
        return best;
    }

    /// <summary>
    /// edit: whether the folder is being arranged on the bar. Nothing moves while it is: only the
    /// selected line wears a ring, and the "+" buttons stand outside the panels (<see cref="Adds"/>).
    /// </summary>
    public bool Editing { get; init; }
    public required float TextPx { get; init; }
    public required float SmallPx { get; init; }
}

public sealed unsafe partial class FolderFlyout : IDisposable
{
    // The flyout's own timer; FlyoutHost's are above these.
    private const nuint TickTimer = 1;

    /// <summary>calendar: the quick tick while a stopwatch in it counts (<see cref="ArmQuick"/>): its hundredths, about thirty times a second.</summary>
    internal const nuint QuickTimer = 3;

    internal const uint QuickMs = 33;

    /// <summary>list: a line's items gliding to where the wheel sent them (<see cref="Glide"/>), a frame at a time.</summary>
    private const nuint GlideTimer = 5;

    private const uint GlideMs = 15;

    private const int NoTarget = -1;

    /// <summary>edit: the first of the folder's "+" buttons, as a target; the rest follow (<see cref="FolderPlan.Adds"/>).</summary>
    internal const int AddTarget = 3000;


    /// <summary>edit: how wide a "+" button is, in DIPs: round, so as tall too.</summary>
    internal const float AddDip = 24;

    /// <summary>edit: the room between a "+" outside the folder and the panel's edge, in DIPs.</summary>
    internal const float AddApartDip = 6;

    /// <summary>edit: the room between a column's round buttons side by side — its "+", its width, its warning — in DIPs.</summary>
    internal const float PairApartDip = 4;

    /// <summary>edit: how wide <paramref name="buttons"/> of a column's round buttons are side by side, in px at <paramref name="scale"/>: what is centred on a list or a slot.</summary>
    internal static float ButtonsWidth(float scale, int buttons) =>
        (buttons * PopupPaint.Round(AddDip * scale)) + (Math.Max(0, buttons - 1) * PopupPaint.Round(PairApartDip * scale));

    /// <summary>
    /// edit: the width button's glyph, Segoe Fluent Icons' vertical resize (‹•› upright) turned a
    /// quarter (<see cref="PaintAdd"/>): the font has no horizontal one.
    /// </summary>
    private static readonly string WidthGlyph = char.ConvertFromUtf32(0xECE7);

    /// <summary>edit: a column's "+" (Segoe Fluent Icons: Add).</summary>
    private static readonly string AddGlyph = char.ConvertFromUtf32(0xE710);

    /// <summary>
    /// How many of a folder's cells the panel of a folder with no widget at all is at rest, saying
    /// where they go: three rows. Arranged an empty list has no height (§297).
    /// </summary>
    internal const int EmptyCells = 6;

    /// <summary>
    /// edit: the room the window keeps round its panel while the folder is arranged, in DIPs, on
    /// every side but the bar's (<see cref="FlyoutHost.OutsideDip"/>): a "+" outside the panel with
    /// its room, and some air past it. The panel itself stands where it stood.
    /// </summary>
    internal const float OutsideRoomDip = AddApartDip + AddDip + AddApartDip;

    private readonly FlyoutHost host;
    private Gfx? gfx;
    private Look look;
    private float scale = 1;
    private FlyoutTheme theme = FlyoutTheme.For(default);
    private FolderIsland? folder;
    private FolderPlan? plan;

    // pins: a widget dragged out of the folder onto the bar, and where the press was.
    private int dragging = NoTarget;
    private POINT dragFrom;

    // edit: the folder is being arranged on the bar; the line being moved, where it would land —
    // between two lines, at a list's end, or a new list — and a line the footer says for a moment.
    private bool editing;

    /// <summary>
    /// §612: arranged, but drawn as at rest — the Appearance's preview (EditDress.Preview): its lines where they stand at rest,
    /// no guide lines, no "+", no ring, its window the panel's; edit mode's state (the selection, the clipboard) kept.
    /// </summary>
    private bool plain;

    /// <summary>§612: whether edit mode's dress shows: arranged, and not in the Appearance's preview.</summary>
    private bool Dressed => editing && !plain;

    private int moving = NoTarget;
    private FolderPlace? landing;
    private string note = "";

    // edit: the card pressed has been carried past the drag threshold since the press, so its
    // release is a move and never a click, wherever it is let go (EditClicks.cs).
    private bool travelled;

    // edit: the dock is turning edit mode on for a card carried past the threshold at rest
    // (HandOver): the capture going elsewhere meanwhile is no drag being lost.
    private bool handingOver;

    private ShapeVisual? targetVisual;
    private CompositionRoundedRectangleGeometry? targetBox;
    private IReadOnlyList<FolderTile> tiles = [];
    private string layoutSignature = "";

    private int wheel;

    // list: the line whose items glide to where the wheel sent them, and where that is (ListScroll.cs);
    // and where the pointer last was over the panel, so the row under it follows the items as they move.
    private int gliding = NoTarget;
    private float glideTo;
    private Vector2? pointerAt;
    private int hot = NoTarget;

    // Where the pointer last moved to over the window, on the screen; null since it left.
    private POINT? pointerScreen;

    // The line whose page (FolderIsland.Pages.cs) the lines shown are, so the keys go back to it with them.
    private string? pageOf;
    private int pressed = NoTarget;

    // quick: the widget holding the pointer and the card it is in — a slider being dragged. A
    // press it took is not a click: the moves are its and the release ends the drag.
    private (int Target, CellIsland Island)? holding;

    // edit: which column's button its menu or its add list is open for — a "+", a width, a warning
    // (an index of the plan's Adds) — so it stands pressed; -1 for none. The menu ending lets it go
    // (MenuClosed).
    private int sizing = NoTarget;

    // folders: the kind of a widget being carried over this folder from outside it, while one is,
    // and the sentence the footer says for it where the folder's type has no form of it.
    private WidgetItem? offering;
    private string refusing = "";

    // A draw is under way (Draw): ShowStates does not start another inside it.
    private bool drawing;

    // Which bar edge the panel opened from: the add list opens on the far side of a "+" from it,
    // and a list's end "+" stands at its end away from it.
    private Edge shownEdge = Edge.Bottom;

    // edit: the display the bar stands on, screen px: arranged, the window reaches its edges (Show).
    private RECT display;
    private int focused = NoTarget;
    private bool keyboard;

    /// <summary>edit: a right press put a line being moved back (<see cref="CancelMove"/>): its release opens no menu.</summary>
    private bool rightCancelled;
    private long minute;
    private bool disposed;

    // §357: the lists' views as the plan has them now, kept (FolderFlyout.Retained.cs). Each list's lines are
    // in a view of its own, clipped to its panel's rounded shape (in panel px, as everything here is).
    private readonly List<ContainerVisual> listViews = [];
    private readonly List<CompositionRoundedRectangleGeometry> listShapes = [];

    /// <summary>Tests only: how many pieces' chrome the host made, and how many times it put its window somewhere.</summary>
    internal (int Pieces, int Moves, int Materials, int Frames) HostMade => (host.PiecesMade, host.WindowMoves, host.MaterialsMade, host.FramesPlaced);

    // Each card a list's dividers split it into (FolderListPlan.Runs): a view of its own in its list's,
    // clipped to the card's rounded shape (PlaceLines), so a
    // widget's plate becomes that card's corner as it does the list's. Two a card: one inside the rim,
    // and over it one to the card's edge, under the rim, for the lines whose graph runs there (Rim).
    // §357: kept by their list, card and rim (FolderFlyout.Retained.cs); these, and the lines' below, are
    // the elements in the plan's order now.
    private readonly List<(ContainerVisual View, CompositionRoundedRectangleGeometry Shape, int List, int Run, bool Rim)> runViews = [];
    private readonly List<ContainerVisual> cards = [];

    /// <summary>
    /// Each line's own fill, set to what it is now (<see cref="InkOf"/>: its own card at rest, lit under
    /// the pointer, §311). edit: while the folder is arranged its colour fades in and out in
    /// <see cref="EditLook.FadeMs"/> as a block's on the bar does (Scene.FadeTo); at rest a line lights
    /// at once as a menu's row does. A line wears a clear one rather than none: a sprite shape with
    /// neither a fill nor a stroke that is given a fill later is not drawn again by the compositor
    /// (backlog 21, ListLineHoverShots).
    /// </summary>
    private readonly List<CompositionColorBrush> washes = [];

    /// <summary>§464: each line's blur of the desktop, shown under a see-through background (<see cref="LineFill"/>).</summary>
    private readonly List<SpriteVisual> frosts = [];

    /// <summary>§464: each line's background, a sprite on whole px (its colour the line's wash).</summary>
    private readonly List<SpriteVisual> fills = [];

    /// <summary>Per line, a spacer's stripes while arranged (<see cref="FolderPlan.Bands"/>; §524).</summary>
    private readonly List<SpacerStripes> bands = [];

    /// <summary>Each line, in the folder's order: its content's sprite, and the surface its widget draws on.</summary>
    private readonly List<Line> contents = [];

    // Arabic and Urdu: each mirrored line's parts kept upright at its last drawing (Mirror.Keep), by
    // its widget, which the pointer is turned back in (CardSpace).
    private readonly Dictionary<IIsland, IReadOnlyList<(float From, float To)>> uprights = new(ReferenceEqualityComparer.Instance);

    /// <summary>Each line's words drawn cut short as it was last drawn, in its box's px, said whole in its tooltip (CutWords).</summary>
    private readonly Dictionary<IIsland, IReadOnlyList<CutRun>> cutRuns = new(ReferenceEqualityComparer.Instance);
    private SpriteVisual? empty;
    private CompositionSurfaceBrush? emptyBrush;
    private CompositionDrawingSurface? emptySurface;

    // edit: the columns' round buttons, one each of the plan's Adds, over everything and outside the
    // panels, and what each was last drawn from; §357: kept by what each is (AddKeyOf).
    private readonly List<Button> addVisuals = [];

    // edit: the guide lines down the screen at every list's and slot's edges (Guides), under
    // everything, and their ink; §357: made as many as were ever wanted, the rest out of sight.
    private readonly List<SpriteVisual> guideVisuals = [];
    private CompositionColorBrush? guideInk;

    // edit: the ring round the selected widget's card or line (Picked).
    private ShapeVisual? pickedVisual;
    private CompositionRoundedRectangleGeometry? pickedRing;

    // edit: the rings round the other selected lines (AlsoPicked), made as many are wanted.
    private readonly List<(ShapeVisual Visual, CompositionRoundedRectangleGeometry Ring)> alsoRings = [];

    // edit: the key of the widget last clicked while the bar is arranged (the dock's EditPick).
    private string? picked;

    // edit: the keys of the other selected widgets (the dock's selection), each ringed as the picked one.
    private IReadOnlyList<string> alsoPicked = [];

    // edit: a press on the arranged folder's empty room holds the mouse for the selection rectangle (the dock's EditBand).
    private bool banding;

    private ShapeVisual? focusVisual;
    private CompositionRoundedRectangleGeometry? focusOuter;
    private CompositionRoundedRectangleGeometry? focusInner;

    private CompositionColorBrush? targetOk;

    /// <param name="dock">Goes into the window's GWLP_USERDATA, so the dock's window procedure routes its messages here.</param>
    public FolderFlyout(Gfx gfx, HINSTANCE instance, nint dock)
        : this(gfx, instance, dock, Flyout.ClassName)
    {
    }

    /// <param name="className">The window's class: a test's own, whose window procedure is the test's.</param>
    internal FolderFlyout(Gfx gfx, HINSTANCE instance, nint dock, string className)
    {
        // A taskbar flyout's surface, as Windows' quick settings and calendar have it, in the folders' own look (FlyoutTheme.Folder, §334).
        host = new FlyoutHost(gfx, instance, dock, FlyoutMode.Menu, className) { Taskbar = true, Folder = true, UnderEditBar = true, Stratum = Stratum.Popup };
        tipInstance = instance;
        tipDock = dock;
    }

    // The folder's tooltip is a host of its own, made the first time a part has one (FolderFlyout.Tips.cs).
    private readonly HINSTANCE tipInstance;
    private readonly nint tipDock;

    /// <summary>The window; a new one after Alt+F4 destroyed the last, so read it afresh.</summary>
    public HWND Hwnd => host.Hwnd;

    public bool IsOpen => host.IsOpen;

    /// <summary>The folder shown, while it is open.</summary>
    internal FolderIsland? Folder => IsOpen ? folder : null;

    /// <summary>
    /// edit: the key of the widget last clicked while the bar is arranged, on the bar or in a folder
    /// (the dock's EditPick), or null. Where it is one of this folder's and the folder is arranged,
    /// its card or its line wears the selected look: the accent's ring round it, as the bar's block
    /// wears it, until another widget is clicked or edit mode ends.
    /// </summary>
    internal string? Picked
    {
        get => picked;
        set
        {
            if (string.Equals(picked, value, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            picked = value;
            ShowStates();
        }
    }

    /// <summary>
    /// edit: the list a folder's menu's "Add" adds into: the selected line's
    /// — the one last clicked, else the first of the others selected, in the folder's order; null
    /// where none of this folder's is selected.
    /// </summary>
    internal int? ChosenColumn()
    {
        if (plan is not { Editing: true } shown)
        {
            return null;
        }
        var line = PickedCard(picked, tiles);
        for (var index = 0; line < 0 && index < tiles.Count; index++)
        {
            if (alsoPicked.Contains(tiles[index].Island.Key, StringComparer.OrdinalIgnoreCase))
            {
                line = index;
            }
        }
        return line >= 0 && line < shown.CardList.Count ? shown.CardList[line] : null;
    }

    /// <summary>
    /// edit: where a menu's "Add" adds while the folder is arranged: into list <paramref name="into"/>
    /// where one is given (a line's "Add", a column's "+"), else the chosen column
    /// (<see cref="ChosenColumn"/>), else the first that shows everything it has — the first of all
    /// where none does — at the list's own end away from the bar, where its "+" stands
    /// (<see cref="AddPlaceIn"/>). Null at rest.
    /// </summary>
    internal FolderPlace? AddPlace(int? into = null)
    {
        if (!IsOpen || plan is not { Editing: true } shown || folder is null)
        {
            return null;
        }
        var held = folder.Options.Lists;
        return AddPlaceIn(
            [.. Enumerable.Range(0, shown.Lists.Count).Select(list => list < held.Count ? held[list]?.Count ?? 0 : 0)],
            [.. shown.Lists.Select(list => list.Hidden.Count > 0)],
            into ?? ChosenColumn(),
            shownEdge == Edge.Top);
    }

    /// <summary>
    /// edit: where a widget added to a folder of <paramref name="counts"/> lines in each list goes:
    /// into <paramref name="chosen"/> where it is one of them, else the first list not
    /// <paramref name="full"/> — showing everything it has — else the first; at its end away from the
    /// bar (<paramref name="fromTop"/>: a top bar's lists hang from it, so their end; any other bar's,
    /// their top). Pure.
    /// </summary>
    internal static FolderPlace AddPlaceIn(IReadOnlyList<int> counts, IReadOnlyList<bool> full, int? chosen, bool fromTop)
    {
        var lists = Math.Max(1, counts.Count);
        var list = chosen is { } wanted && wanted >= 0 && wanted < lists ? wanted : 0;
        if (chosen is null || chosen < 0 || chosen >= lists)
        {
            for (var each = 0; each < counts.Count; each++)
            {
                if (each >= full.Count || !full[each])
                {
                    list = each;
                    break;
                }
            }
        }
        return new FolderPlace(list, fromTop && list < counts.Count ? counts[list] : 0);
    }

    /// <summary>
    /// edit: the keys of the widgets selected besides <see cref="Picked"/> (the dock's selection,
    /// EditSelection.cs). Those of this folder's lines wear the same ring, as several selected blocks
    /// on the bar do.
    /// </summary>
    internal IReadOnlyList<string> AlsoPicked
    {
        get => alsoPicked;
        set
        {
            if (alsoPicked.SequenceEqual(value, StringComparer.OrdinalIgnoreCase))
            {
                return;
            }
            alsoPicked = [.. value];
            ShowStates();
        }
    }

    /// <summary>
    /// §593: the key of the add page's ghost among the folder's lines — the widget under the pointer on the add page, put in
    /// where the page adds (Dock.AddGhost.cs) —: §625 drawn whole, as it will stand, the ghost of a carried line's outline over
    /// it; null for none.
    /// </summary>
    internal string? Ghost
    {
        get => ghostKey;
        set
        {
            if (string.Equals(ghostKey, value, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            ghostKey = value;
            OverTheLines();
            ShowStates();
        }
    }

    private string? ghostKey;

    /// <summary>
    /// §606: the whole folder the add page's ghost — a ready-made group under the pointer, put on the bar in the ghost's copy
    /// of the config and opened as its click opens it (Dock.AddGhost.cs) —: all of it, panels, lines and buttons, §625 whole,
    /// §621 the ghost of a carried line's outline round each of its lines; nothing of it takes the
    /// pointer (<see cref="PassesThrough"/>). A showing starts without it.
    /// </summary>
    internal bool Ghosted
    {
        get => ghosted;
        set
        {
            if (ghosted == value)
            {
                return;
            }
            ghosted = value;
            Faint();
            OverTheLines();
            ShowStates();
        }
    }

    private bool ghosted;

    /// <summary>
    /// §677 (the person, 2026-11-12: "when showing the ghost previews of widgets in a folder all the add buttons and also column
    /// width buttons should be removed / not shown. this should also be when adding a widget to an existing folder"): the
    /// columns' round buttons — "+", width, the hidden widgets' warning — none, nothing of them to press or reach with the keys,
    /// while the folder is the add page's ghost (<see cref="Ghosted"/>) or the add page adds into it (<see cref="Ghost"/>, its
    /// empty ghost from the page's opening). Where they stand stays theirs: nothing moves as they go and come back.
    /// </summary>
    internal bool ButtonsOff => ghosted || ghostKey is not null;

    /// <summary>
    /// §621: the add page's ghost's outlines round each line over the lines, as the bar's ghost stands over its parts: under
    /// them the lines' own cards hid all of it but the folder's edge — §625 a ghost line's card too, whole now. A carried
    /// line's ghost, the gap it would land in, under them.
    /// </summary>
    private void OverTheLines()
    {
        if (contentRoot is not { } root || ghostVisual is not { } ghost || listsLayer is not { } linesLayer)
        {
            return;
        }
        var over = ghosted || ghostKey is not null;
        Try(() =>
        {
            root.Children.Remove(ghost);
            if (over)
            {
                root.Children.InsertAbove(ghost, linesLayer);
            }
            else
            {
                root.Children.InsertBelow(ghost, linesLayer);
            }
        });
    }

    /// <summary>
    /// §606: the folder open arranged out of sight while the add page's ghost of another folder stands open in its place —
    /// one folder at a time, so the ghost's click closes it (Dock.ShowFolder) —, back as it was once the ghost goes. Nothing of
    /// it takes the pointer meanwhile. A showing starts without it.
    /// </summary>
    internal bool Aside
    {
        get => aside;
        set
        {
            if (aside == value)
            {
                return;
            }
            aside = value;
            Faint();
        }
    }

    private bool aside;

    /// <summary>
    /// §606: how much of the folder shows: none aside, whole else — §625 the ghost too, as it will open: faint, the dim
    /// showed through its panels, which in light mode read dark.
    /// </summary>
    private void Faint() => host.Opacity = aside ? 0 : 1;

    /// <summary>Tests only: §677 whether the columns' round buttons ("+", width, warning) show: arranged, not a ghost, nothing added into it.</summary>
    internal bool AddButtonsShown => addsLayer is { IsVisible: true } && plan is { Editing: true, Adds.Count: > 0 };

    /// <summary>Tests only: §606 how much of the folder shows (<see cref="Ghosted"/>, <see cref="Aside"/>).</summary>
    internal float Shows => host.Opacity;

    /// <summary>
    /// §617: the add page's ghost among the lines (<see cref="Ghost"/>) the empty one — no pick under the pointer, the page's
    /// place alone —: none of its line drawn, only the ghost of a carried line round its room. Set before <see cref="Ghost"/>.
    /// </summary>
    internal bool GhostEmpty
    {
        get => ghostEmpty;
        set
        {
            if (ghostEmpty == value)
            {
                return;
            }
            ghostEmpty = value;
            ShowStates();
        }
    }

    private bool ghostEmpty;

    /// <summary>
    /// §617: where the add page's ghost's outlines last stood (panel px), which the next ones grow from (§621
    /// <see cref="EditLook.GhostFrom"/>); none for none.
    /// </summary>
    private RectF[] addGhostStood = [];

    /// <summary>Tests only: §621 each outline of the add page's ghost in the folder (panel px), where it stands; none for none.</summary>
    internal IReadOnlyList<RectF> AddGhostsShown => ghostVisual is { IsVisible: true } && Carried() is null ? addGhostStood : [];

    /// <summary>§629: the add page's ghost in the folder came, went or moved (<see cref="GhostSight"/>): the page it shows through follows.</summary>
    internal Action? GhostsMoved { get; set; }

    /// <summary>
    /// §629: the add page's ghost in the folder — its line, or each line of a ghost folder (§621) — as the page lets it show
    /// through: the folder's window, the ghost's outlines in it; null for none.
    /// </summary>
    internal GhostSight? GhostSight => host.Whole is { } whole && plan is { } shown && AddGhostsShown is { Count: > 0 } cards
        ? new GhostSight(whole.Tree, whole.Origin, [.. cards.Select(card => card with { X = card.X + host.PanelAt.X, Y = card.Y + host.PanelAt.Y })], shown.Radius)
        : null;

    /// <summary>
    /// §593 the ghost of a carried line round the add page's ghost: §621 one round each of <paramref name="cards"/> (panel px)
    /// — its line, or each line of a ghost folder —, in the whole panel's visual, so the empty ghost's room grows into the
    /// pick's and one splits into several (<see cref="EditLook.GhostFrom"/>) rather than jumping to it; come anew, put there.
    /// None for none.
    /// </summary>
    private void ShowGhosts(FolderPlan shown, IReadOnlyList<RectF> cards)
    {
        if (ghostVisual is null || ghostBox is null)
        {
            return;
        }
        var was = ghostVisual.IsVisible ? addGhostStood : [];
        ghostVisual.IsVisible = cards.Count > 0;
        var moved = !was.SequenceEqual(cards);
        addGhostStood = [.. cards];
        if (moved)
        {
            GhostsMoved?.Invoke();
        }
        // §625: a pick's outlines alone, its lines as they will look; the empty ghost's room washed.
        ShowMoreGhosts(cards.Count - 1, wash: ghostEmpty && !ghosted);
        if (cards.Count == 0)
        {
            return;
        }
        var edge = EditLook.Ring(EditLook.Hair(scale));
        ghostVisual.StopAnimation("Offset");
        ghostVisual.Offset = Vector3.Zero;
        ghostVisual.Size = new Vector2(shown.Width, shown.Height);
        ((CompositionSpriteShape)ghostVisual.Shapes[0]).StrokeThickness = edge;
        for (var index = 0; index < cards.Count; index++)
        {
            var box = index == 0 ? ghostBox : ghostMore[index - 1].Box;
            var from = EditLook.GhostFrom(was, cards.Count, index);
            if (from is { } start && was.Length != cards.Count)
            {
                Outline(box, start, animate: false);
            }
            Outline(box, cards[index], animate: from is not null);
        }

        void Outline(CompositionRoundedRectangleGeometry box, RectF card, bool animate)
        {
            var (offset, size, corner) = EditLook.Stroke(card.Size, MathF.Min(shown.Radius, card.Height / 2), edge);
            Reshape(box, offset + new Vector2(card.X, card.Y), size, animate);
            box.CornerRadius = new Vector2(corner, corner);
        }
    }

    /// <summary>
    /// §621: <paramref name="count"/> of the ghost's more rectangles shown (<see cref="ghostMore"/>, made as first asked for, in
    /// the ghost's wash and outline), the rest none — a carried line's ghost, or the add page's of one line, is ghostBox's alone.
    /// §625 <paramref name="wash"/>: the wash in each, the first's too — a room with nothing in it yet (a carried line's gap,
    /// the empty ghost); none round a pick's lines, which show as they will.
    /// </summary>
    private void ShowMoreGhosts(int count, bool wash = true)
    {
        if (ghostVisual is null || ghostFill is null || ghostStroke is null)
        {
            return;
        }
        ((CompositionSpriteShape)ghostVisual.Shapes[0]).FillBrush = wash ? ghostFill : null;
        var c = host.Compositor;
        while (ghostMore.Count < count)
        {
            var box = Own(c.CreateRoundedRectangleGeometry());
            var shape = Own(c.CreateSpriteShape(box));
            ghostVisual.Shapes.Add(shape);
            ghostMore.Add((shape, box));
        }
        var edge = EditLook.Ring(EditLook.Hair(scale));
        for (var index = 0; index < ghostMore.Count; index++)
        {
            var (shape, _) = ghostMore[index];
            shape.FillBrush = index < count && wash ? ghostFill : null;
            shape.StrokeBrush = index < count ? ghostStroke : null;
            shape.StrokeThickness = edge;
        }
    }

    /// <summary>edit: the keys of the folder's widgets, in its order: what a selection rectangle in it picks among.</summary>
    internal IReadOnlyList<string> Keys => [.. tiles.Select(tile => tile.Island.Key)];

    /// <summary>
    /// edit: the keys of the folder's widgets whose lines, as much of them as their lists show,
    /// <paramref name="area"/> (screen px) runs over: what a selection rectangle in it selects.
    /// None while it is not arranged.
    /// </summary>
    internal List<string> KeysIn(RECT area)
    {
        List<string> keys = [];
        if (plan is not { Editing: true } shown)
        {
            return keys;
        }
        var origin = new POINT { x = (int)MathF.Round(host.Panel.X), y = (int)MathF.Round(host.Panel.Y) };
        _ = ClientToScreen(host.Hwnd, &origin);
        for (var index = 0; index < shown.Cards.Count && index < tiles.Count; index++)
        {
            if (!shown.Holds(index))
            {
                continue;
            }
            var box = Shown(shown, shown.CardList[index], shown.Cards[index]);
            if (box.bottom <= box.top)
            {
                continue;
            }
            if (EditClicks.Overlaps(area, origin.x + box.left, origin.y + box.top, origin.x + box.right, origin.y + box.bottom))
            {
                keys.Add(tiles[index].Island.Key);
            }
        }
        return keys;
    }

    /// <summary>Tests only: how many lines wear the ring of one selected besides the picked (<see cref="AlsoPicked"/>).</summary>
    internal int AlsoPickedShown => alsoRings.Count(ring => ring.Visual.IsVisible);

    /// <summary>Tests only: whether the selected widget's ring shows now (<see cref="Picked"/>).</summary>
    internal bool PickedShown => pickedVisual?.IsVisible == true;

    /// <summary>Tests only: where the selected widget's ring stands in the popup's box (px, its window before §355), its stroke at the box's edge; empty where none shows.</summary>
    internal RECT PickedRing =>
        pickedVisual is { IsVisible: true } ring
            ? new RECT
            {
                left = (int)MathF.Round(host.Room.Left + ring.Offset.X),
                top = (int)MathF.Round(host.Room.Top + ring.Offset.Y),
                right = (int)MathF.Round(host.Room.Left + ring.Offset.X + ring.Size.X),
                bottom = (int)MathF.Round(host.Room.Top + ring.Offset.Y + ring.Size.Y),
            }
            : default;

    /// <summary>
    /// edit: which card or line of <paramref name="tiles"/> is the selected widget, keyed
    /// <paramref name="key"/>; <see cref="NoTarget"/> where none of them is.
    /// </summary>
    internal static int PickedCard(string? key, IReadOnlyList<FolderTile> tiles)
    {
        for (var index = 0; key is not null && index < tiles.Count; index++)
        {
            if (string.Equals(tiles[index].Island.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }
        return NoTarget;
    }

    /// <summary>Tests only: the line the pointer lights, the one the keys are on, and whether the keys show it.</summary>
    internal (int Hot, int Focused, bool Keyboard) Lit => (hot, focused, keyboard);

    /// <summary>Tests only: what the footer says now (<see cref="Say"/>).</summary>
    internal string Said => note;

    /// <summary>Tests only: where on the screen the middle of the <paramref name="index"/>th card, or line, stands.</summary>
    internal POINT ScreenOfCard(int index) =>
        plan is { } shown && index >= 0 && index < shown.Cards.Count ? OnScreen(shown.Cards[index]) : default;

    /// <summary>Tests only: the <paramref name="index"/>th card, or line, on the screen, as a click on it hands the dock (§212).</summary>
    internal RECT ScreenRectOfCard(int index) =>
        plan is { } shown && index >= 0 && index < shown.Cards.Count ? CardOnScreen(shown.Cards[index]) : default;

    /// <summary>
    /// §357: the line of the widget keyed <paramref name="key"/> on the screen, as a click on it hands the
    /// dock its card — arranged, where it is taken hold of: a divider's grab —,
    /// which its menu stands beside (§212); null where the folder is not open or does not show it.
    /// </summary>
    internal RECT? LineRect(string key) =>
        IsOpen && plan is { } shown && IndexOfKey(key) is var index and >= 0 && shown.Holds(index) ? CardOnScreen(shown.Grab(index)) : null;

    /// <summary>Tests only: where on the screen the middle of the <paramref name="index"/>th "+" stands.</summary>
    internal POINT ScreenOfAdd(int index) =>
        plan is { } shown && index >= 0 && index < shown.Adds.Count ? OnScreen(AddBox(shown, index)) : default;

    /// <summary>Tests only (§397): where on the screen the middle of each of its columns' "+" stands, open; none closed.</summary>
    internal IReadOnlyList<POINT> AddsOnScreen => IsOpen && plan is { } shown
        ? [.. Enumerable.Range(0, shown.Adds.Count).Where(index => shown.Adds[index].Kind == FolderAddKind.Add).Select(ScreenOfAdd)]
        : [];

    /// <summary>
    /// edit: the widgets list <paramref name="list"/> of <paramref name="shown"/> has no room to show
    /// (<see cref="FolderListPlan.Hidden"/>), in its order.
    /// </summary>
    private List<IIsland> HiddenIn(FolderPlan shown, int list) =>
        list >= 0 && list < shown.Lists.Count
            ? [.. shown.Lists[list].Hidden.Where(index => index < tiles.Count).Select(index => tiles[index].Island)]
            : [];

    /// <summary>
    /// edit: what a list's warning button says (<see cref="FolderAddKind.Hidden"/>), as the edit
    /// bar's "not shown" button says it (EditRail): one widget by name, several counted.
    /// </summary>
    internal static string HiddenTitle(IReadOnlyList<IIsland> hidden) => hidden.Count == 1
        ? S.DockFolderNotShownOne(hidden[0].Info.Title)
        : S.DockFolderNotShownMany(hidden.Count);

    /// <summary>
    /// edit: the glyph on a list's warning button: the hidden widget's own where it is one — naming it
    /// at a glance — and the warning where there are several, as the edit bar's (EditRail).
    /// </summary>
    internal static string HiddenGlyph(IReadOnlyList<IIsland> hidden) =>
        hidden.Count == 1 ? hidden[0].Info.Glyph : EditRailPlan.Glyphs.NotShown;

    /// <summary>Tests only: the plan the open folder is laid out by.</summary>
    internal FolderPlan? Plan => IsOpen ? plan : null;

    /// <summary>tests: §462: each line's pattern's layer, where its card stands in the panel, in the order of the plan's cards.</summary>
    internal IReadOnlyList<(Vector3 Offset, PatternLayer Pattern)> LinePatterns => [.. contents.Select(line => (line.Card.Offset, line.Pattern))];

    /// <summary>Tests only: the panels the host wears its chrome on now (<see cref="FlyoutHost.Pieces"/>).</summary>
    internal IReadOnlyList<D2D_RECT_F>? PiecesShown => host.Pieces;

    /// <summary>
    /// tests, §647: the ground the open folder stands on, on the screen, and its colour; null for a folder of cards (or
    /// none open).
    /// </summary>
    internal (RECT Box, Color Color)? GroundShown
    {
        get
        {
            if (!IsOpen || host.Ground is not { } color)
            {
                return null;
            }
            var (on, g) = (host.PanelOnScreen, host.GroundBox);
            return (new RECT { left = on.left + (int)g.left, top = on.top + (int)g.top, right = on.left + (int)g.right, bottom = on.top + (int)g.bottom }, color);
        }
    }

    /// <summary>tests, §651: the ground as it is dressed now (FlyoutHost.GroundDressed); null for none.</summary>
    internal (Color Fill, bool Papered, Color Edge, Color Seam)? GroundDressed => IsOpen ? host.GroundDressed : null;

    /// <summary>tests: the theme the open folder's chrome wears.</summary>
    internal FlyoutTheme Theme => host.Theme;

    /// <summary>
    /// tests, §649: each line as it shows now, in the plan's order: its fill (its card at rest, lit under the pointer), whether
    /// the desktop's blur shows under it, and its divider line where one shows (panel px); and each panel's rim.
    /// </summary>
    internal (IReadOnlyList<(Color Fill, bool Blur, D2D_RECT_F? Rule)> Lines, IReadOnlyList<Color> Rims) DressShown =>
        ([.. contents.Select(line => (line.Wash.Color, line.Frost.IsVisible, line.Rule is { IsVisible: true } rule
            ? new D2D_RECT_F { left = line.Card.Offset.X + rule.Offset.X, top = line.Card.Offset.Y + rule.Offset.Y, right = line.Card.Offset.X + rule.Offset.X + rule.Size.X, bottom = line.Card.Offset.Y + rule.Offset.Y + rule.Size.Y }
            : (D2D_RECT_F?)null))], host.RimsShown);

    /// <summary>tests, §647: whether this showing set off coming in (out of its bar, attached).</summary>
    internal bool Entered => host.Entered;

    /// <summary>
    /// The middle of <paramref name="box"/>'s side away from the bar (panel px, as it shows now), on
    /// the screen: where a popup about it opens, clear of it rather than over its middle.
    /// </summary>
    private POINT AwayOnScreen(D2D_RECT_F box) => OnScreen(shownEdge switch
    {
        Edge.Top => box with { top = box.bottom },
        Edge.Left => box with { left = box.right },
        Edge.Right => box with { right = box.left },
        _ => box with { bottom = box.top },
    });

    /// <summary>The middle of <paramref name="box"/> (panel px, as it shows now), on the screen.</summary>
    private POINT OnScreen(D2D_RECT_F box)
    {
        var panel = host.Panel;
        var at = new POINT
        {
            x = (int)MathF.Round(panel.X + ((box.left + box.right) / 2)),
            y = (int)MathF.Round(panel.Y + ((box.top + box.bottom) / 2)),
        };
        _ = ClientToScreen(host.Hwnd, &at);
        return at;
    }

    /// <summary>
    /// <paramref name="field"/>, in the own px of the line drawn in <paramref name="card"/> (panel px),
    /// on the screen: where the line is mirrored (Mirror.cs), mirrored back as it is drawn.
    /// </summary>
    private RECT FieldOnScreen(D2D_RECT_F card, D2D_RECT_F field)
    {
        var width = card.right - card.left;
        var (left, right) = Mirror.On ? (width - field.right, width - field.left) : (field.left, field.right);
        var panel = host.Panel;
        var from = new POINT { x = (int)MathF.Round(panel.X + card.left + left), y = (int)MathF.Round(panel.Y + card.top + field.top) };
        var to = new POINT { x = (int)MathF.Round(panel.X + card.left + right), y = (int)MathF.Round(panel.Y + card.top + field.bottom) };
        _ = ClientToScreen(host.Hwnd, &from);
        _ = ClientToScreen(host.Hwnd, &to);
        return new RECT { left = from.x, top = from.y, right = to.x, bottom = to.y };
    }


    /// <summary>edit: <paramref name="card"/> (panel px, as it shows now) on the screen: what a line's menu stands beside (§212).</summary>
    private RECT CardOnScreen(D2D_RECT_F card)
    {
        var panel = host.Panel;
        var from = new POINT { x = (int)MathF.Floor(panel.X + card.left), y = (int)MathF.Floor(panel.Y + card.top) };
        var to = new POINT { x = (int)MathF.Ceiling(panel.X + card.right), y = (int)MathF.Ceiling(panel.Y + card.bottom) };
        _ = ClientToScreen(host.Hwnd, &from);
        _ = ClientToScreen(host.Hwnd, &to);
        return new RECT { left = from.x, top = from.y, right = to.x, bottom = to.y };
    }

    private static D2D_RECT_F Up(D2D_RECT_F box, float by) => box with { top = box.top - by, bottom = box.bottom - by };

    private static D2D_RECT_F Down(D2D_RECT_F box, float by) => Up(box, -by);

    /// <summary>
    /// edit: where the "+" <paramref name="index"/> shows now, in panel px: kept where the window can
    /// draw it and the display shows it (<see cref="KeepIn"/>) — over the folder's edge, where the
    /// work area has no room beside it.
    /// </summary>
    private D2D_RECT_F AddBox(FolderPlan shown, int index) => KeepIn(shown.Adds[index].Box, Visible());

    /// <summary>
    /// The window, in panel px: the panel and the room it keeps round it — arranged, the whole
    /// display, under its bars too (<see cref="FlyoutHost.Spread"/>).
    /// </summary>
    private D2D_RECT_F Window()
    {
        var room = host.Room;
        var panel = host.PanelSize;
        return PopupPaint.Rect(-room.Left, -room.Top, panel.X + room.Left + room.Right, panel.Y + room.Top + room.Bottom);
    }

    /// <summary>
    /// edit: where something of the folder outside the panel can be seen and taken hold of, in panel
    /// px: inside the window, inside the work area the dock handed over — the display's less every
    /// bar of it, its own too (DockWindow.FolderWork) — and never past the bar's inner side (the
    /// anchor's), which the window reaches under while it is arranged (§246) — buttons, slots,
    /// landings and guide lines stay off the bars (§287).
    /// </summary>
    private D2D_RECT_F Visible()
    {
        var window = Window();
        var on = host.PanelOnScreen;
        // The work area the folder was placed in — the bar's display's, as the dock handed it over.
        var work = host.Work;
        if (work.right <= work.left || work.bottom <= work.top)
        {
            return window;
        }
        var bar = host.Placement.Anchor;
        return new D2D_RECT_F
        {
            left = MathF.Max(window.left, Math.Max(work.left, shownEdge == Edge.Left ? bar.x : work.left) - on.left),
            top = MathF.Max(window.top, Math.Max(work.top, shownEdge == Edge.Top ? bar.y : work.top) - on.top),
            right = MathF.Min(window.right, Math.Min(work.right, shownEdge == Edge.Right ? bar.x : work.right) - on.left),
            bottom = MathF.Min(window.bottom, Math.Min(work.bottom, shownEdge == Edge.Bottom ? bar.y : work.bottom) - on.top),
        };
    }

    /// <summary>
    /// edit: <paramref name="next"/> with its slots where the panel stands now (<see cref="WithSlots"/>),
    /// its buttons clear of the edit bar over it (<see cref="Covered"/>); itself at rest.
    /// </summary>
    private FolderPlan Slotted(FolderPlan next)
    {
        if (!editing || host.Spread is null)
        {
            return next;
        }
        var on = host.PanelOnScreen;
        var over = covered.right > covered.left && covered.bottom > covered.top
            ? new D2D_RECT_F { left = covered.left - on.left, top = covered.top - on.top, right = covered.right - on.left, bottom = covered.bottom - on.top }
            : default;
        return WithSlots(next, scale, Visible(), shownEdge, over);
    }

    private RECT covered;

    /// <summary>
    /// edit: where the edit bar of its display stands over the arranged folder, in screen px (the
    /// person, 2026-10-23: "show full height, in edit mode just display the edit bar over the folder";
    /// §329): the folder takes the full height under it, and a column's buttons it would cover stand
    /// just past it (<see cref="WithSlots"/>). The dock's, as the edit bar shows; a change lays the open
    /// folder out again. Empty: nothing over it.
    /// </summary>
    internal RECT Covered
    {
        get => covered;
        set
        {
            if (value.Equals(covered))
            {
                return;
            }
            covered = value;
            if (IsOpen && editing && folder is { } island && gfx is not null)
            {
                LayOut(island, Natural(island));
            }
        }
    }

    /// <summary>
    /// edit: whether a hit test at <paramref name="screen"/> goes through the arranged folder's window
    /// to what stands under it: the window reaches the display's edges for its guide lines and slots,
    /// over the bars too (<see cref="FlyoutHost.Spread"/>), and only its own things take the pointer —
    /// the lists and the room round them inside the work area, and a column's button (a slot's too).
    /// The rest — the slots' room, the bars, which stay locked under it — goes to what is under it.
    /// Never at rest.
    /// </summary>
    internal bool PassesThrough(POINT screen)
    {
        // §606: the add page's ghost, or a folder aside for it: nothing of it is there to press.
        if (IsOpen && (ghosted || aside))
        {
            return true;
        }
        if (!IsOpen || !editing || host.Spread is null || plan is not { } shown || Hwnd == HWND.NULL)
        {
            return false;
        }
        var local = screen;
        if (!ScreenToClient(Hwnd, &local))
        {
            return false;
        }
        var point = new Vector2(local.x - host.Panel.X, local.y - host.Panel.Y);
        var keep = PopupPaint.Round(OutsideRoomDip * scale);
        var room = Visible();
        if (point.X >= MathF.Max(-keep, room.left) && point.X < MathF.Min(shown.Width + keep, room.right)
            && point.Y >= MathF.Max(-keep, room.top) && point.Y < MathF.Min(shown.Height + keep, room.bottom))
        {
            return false;
        }
        return TargetAt(point, onPanel: false) == NoTarget;
    }

    /// <summary>
    /// edit: the slot a line carried to <paramref name="point"/> (panel px) would become a list in:
    /// the one it is over, half the lists' gap either side of it its too (<see cref="FolderPlan.SlotAt"/>),
    /// inside the window's room.
    /// </summary>
    private FolderSlot? SlotAt(FolderPlan shown, Vector2 point)
    {
        var room = Visible();
        return point.Y >= room.top && point.Y < room.bottom && point.X >= room.left && point.X < room.right
            && shown.SlotAt(point.X, shown.ListGap / 2) is var slot and >= 0
                ? shown.Slots[slot]
                : null;
    }

    /// <summary>edit: whether <paramref name="point"/> (panel px) is over the lists' columns: on the panel, or over or under it inside the window's room, half the lists' gap out either side.</summary>
    private bool OverColumns(FolderPlan shown, Vector2 point)
    {
        var half = shown.ListGap / 2;
        // Not where the room ends: an empty column hanging off under a side bar is no place (§303).
        var room = Visible();
        if (point.X < MathF.Max(-half, room.left) || point.X >= MathF.Min(shown.Width + half, room.right))
        {
            return false;
        }
        return (point.Y >= 0 && point.Y < shown.Height) || (host.Spread is not null && point.Y >= room.top && point.Y < room.bottom);
    }


    /// <summary>
    /// <paramref name="box"/> moved, as little as it takes, to lie inside <paramref name="bounds"/>:
    /// a "+" outside the folder that the window or the display has no room for comes in over the
    /// folder's edge rather than off the screen. Where it is larger than the bounds it starts at
    /// their start.
    /// </summary>
    internal static D2D_RECT_F KeepIn(D2D_RECT_F box, D2D_RECT_F bounds)
    {
        var width = box.right - box.left;
        var height = box.bottom - box.top;
        var left = MathF.Max(bounds.left, MathF.Min(box.left, bounds.right - width));
        var top = MathF.Max(bounds.top, MathF.Min(box.top, bounds.bottom - height));
        return PopupPaint.Rect(left, top, width, height);
    }

    /// <summary>For tests: what the left button went down on and is still down on — a card's place, a control, or none.</summary>
    internal int Pressed => pressed;

    /// <summary>
    /// Opens <paramref name="island"/>'s lists, <paramref name="anchor"/> being the middle of the
    /// folder cell's inner side; the widgets start ticking. Arranged (<paramref name="editing"/>) its
    /// window reaches the edges of <paramref name="display"/> — the bar's display, its bars too; the
    /// work area where none is given — and keeps room round the panel for the buttons outside it;
    /// the panel stands where it would either way. §357: a folder shown again keeps what it made
    /// (FolderFlyout.Retained.cs); <paramref name="recall"/>, where it was left (<see cref="FolderMemory"/>):
    /// its lines scrolled, its day, its page and the line with the keys as they were, else at their start.
    /// §385: it stands along the bar where the folder's alignment says (<see cref="PlacementOf"/>);
    /// <paramref name="align"/>, a picture's or a test's placement of its own instead. §446, §489: <paramref name="motion"/>, how
    /// it comes in where it was shut and goes as it closes (<see cref="PopupMotion"/>, the dock's); none, at once (a picture's, a test's).
    /// §656: <paramref name="dock"/>, its bar a dock (Layout.Floating): whatever its style, it stands as cards.
    /// </summary>
    internal void Show(Gfx gfx, Look look, float scale, POINT anchor, RECT work, Edge edge, FolderIsland island, FlyoutAlign? align = null, bool editing = false, RECT? display = null, FolderMemory? recall = null, PopupMotion? motion = null, bool dock = false)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        Closed();
        this.gfx = gfx;
        this.dock = dock;
        // §647: the bar's own look, whose colour an attached folder's ground is.
        barLook = look;
        // §514: as a flyout stands open (Look.Flyout): its outline the Appearance's Flyouts'; §649 attached, its lines on its
        // ground. Attached, it hangs from the bar's inner side itself, on its ground (§647).
        host.Ground = GroundOf(island);
        this.look = look = Wearing(barLook, host.Ground);
        this.scale = scale;
        this.editing = editing;
        plain = false;
        this.display = display ?? work;
        shownEdge = edge;
        // edit: the folder stays open through the presses and drags on the bar behind it (OneFolder).
        host.StaysOpen = !OneFolder.Closes(FolderCloser.PressElsewhere, editing);
        host.OutsideDip = editing ? OutsideRoomDip : 0;
        // edit: the window to the display's edges, for the guide lines and the slots; centred on its anchor.
        host.Spread = editing ? this.display : null;
        moving = NoTarget;
        landing = null;
        note = "";
        offering = null;
        refusing = "";
        folder = island;
        wheel = 0;
        hot = pressed = focused = NoTarget;
        keyboard = false;
        keyPart = null;
        island.Opened(gfx);
        // A folder opens with every line's items at their start, as a Windows list does — but where it
        // opens where it was left (§357), its page first, as the lines laid out are the page's.
        StopGlide();
        if (recall is not null && !editing)
        {
            island.RecallPage(recall);
        }
        tiles = island.Tiles;
        pageOf = island.PageOf;
        // §489: what each line showed is this showing's from now on: a showing is no swap.
        drawnIslands.Clear();
        foreach (var tile in tiles)
        {
            if (tile.Island is CellIsland cells)
            {
                cells.ListScroll = 0;
                cells.Arranged = editing;
                // keys: none of the keys of a showing before.
                cells.KeyedPart = 0;
            }
        }
        if (recall is not null)
        {
            island.Recall(recall);
            tiles = island.Tiles;
            focused = recall.Focused is { } line ? IndexOfKey(line) : NoTarget;
            keyboard = focused >= 0 && recall.Keyboard;
        }
        var natural = Natural(island);
        alignGiven = align;
        faceAnchor = anchor;
        var placement = new FlyoutPlacement(AnchorOf(island, work), work, edge, Align: AlignOf(island));
        // §651: attached, its room from the bar itself to the work area's far end (FlyoutPlacement.Flush), as the host places it.
        var room = (Attached(island) ? placement.Flush : placement).Room(scale);
        var next = Build(look, scale, island.View, island.Laid, tiles.Select(tile => tile.Cell).ToList(), work.bottom - work.top, editing, natural, edge, room, resting: island.Resting, attached: Attached(island));
        plan = next;
        layoutSignature = Signature(island, natural);
        // Each list a panel of its own, or a card of one for each run between its dividers (FolderPlan.Pieces).
        host.Pieces = next.Pieces;
        // §651: attached, its columns parted by a seam across the ground.
        host.Seams = next.Seams;
        // The lists with widgets centred on the button, an outer empty one hanging off their side (FolderPlan.Core).
        host.Core = next.Core;
        // §446, §489: how it comes in, the dock's (Dock.Folders MotionOf); none, at once.
        host.Moves = motion ?? PopupMotion.None;
        host.Show(gfx, look, scale, new Vector2(next.Width, next.Height), placement);
        // §606: a showing is the folder itself, whole — the add page's ghost of it made one by its click, where it stands, the
        // focus its as a folder opening takes it; the ghost's own look is put on after (Dock.AddGhost.cs).
        (ghosted, aside) = (false, false);
        Faint();
        if (!FlyoutHost.Previewing)
        {
            host.Own();
        }
        theme = host.Theme;
        plan = Slotted(next);
        try
        {
            // §357: what this folder showed before kept, put where this showing has it.
            Reconcile();
            Draw();
        }
        catch
        {
            Hide();
            throw;
        }
        island.Changed += FolderChanged;
        minute = Clock.Real().Ticks / TimeSpan.TicksPerMinute;
        // narrator: a screen reader finds its lines and their stops (FolderFlyout.Uia.cs).
        RegisterUia();
        Arm();
    }

    public void Hide() => Hide(restore: true);

    // §385: the placement along the edge Show was handed, a picture's or a test's; null: the folder's own.
    private FlyoutAlign? alignGiven;

    /// <summary>§385: where along the edge <paramref name="island"/> stands: as Show was told, else as its alignment says.</summary>
    private FlyoutAlign AlignOf(FolderIsland island) => alignGiven ?? AlongOf(island.Options.Align);

    // §647: the look of the bar the folder opened from, its ground's colour; and the middle of its face's inner side, as Show was handed it.
    private Look barLook;
    private POINT faceAnchor;

    /// <summary>
    /// §647: the ground under <paramref name="island"/>'s panels: attached (<see cref="FolderStyle.Attached"/>), its bar's
    /// colour as the bar paints it (<see cref="AttachedGround"/>); null for cards.
    /// </summary>
    private Color? GroundOf(FolderIsland island) => Attached(island) ? AttachedGround(barLook) : null;

    /// <summary>
    /// §647: whether <paramref name="island"/> stands attached to its bar (<see cref="FolderStyle.Attached"/>); §656 never beside
    /// a dock (<see cref="FolderStyles.Worn"/>).
    /// </summary>
    private bool Attached(FolderIsland island) => FolderStyles.Worn(island.Options.Style, dock) == FolderStyle.Attached;

    /// <summary>§656: the bar it was opened from is a dock (Layout.Floating), beside which a folder stands as cards. Set by <see cref="Show"/>.</summary>
    private bool dock;

    /// <summary>
    /// §649: the open folder's look over its bar's <paramref name="bar"/> (<see cref="Look.Flyout"/>): attached, on its
    /// <paramref name="ground"/>, its lines with no card — their text, accent and highlight of the ground —; else as a flyout.
    /// </summary>
    internal static Look Wearing(Look bar, Color? ground) => bar.Flyout(ground is { } under ? new Argb(under.A, under.R, under.G, under.B) : null);

    /// <summary>
    /// §649 (the person, 2026-11-09: "the widgets inside should not keep their own cards"): whether the lines stand flat on the
    /// folder's ground — attached to its bar: no card under them, no outline round a list, no corner rounded; what lights
    /// lights on the ground, edge to edge.
    /// </summary>
    internal bool Flat => host.Grounded;

    /// <summary>
    /// §649: its look as its style and its bar's look have it now (<see cref="Wearing"/>), its ground with it, put on the host
    /// where either changed; whether one did.
    /// </summary>
    private bool Rewear(FolderIsland? island)
    {
        var ground = island is not null ? GroundOf(island) : null;
        var worn = Wearing(barLook, ground);
        if (worn == look && ground == host.Ground)
        {
            return false;
        }
        host.Ground = ground;
        look = worn;
        host.Restyle(worn);
        theme = host.Theme;
        return true;
    }

    /// <summary>
    /// §647: where <paramref name="island"/> hangs from: its face's inner side (<see cref="faceAnchor"/>), or attached the bar's
    /// inner side across from it (<see cref="AtBar"/>), so the air between the bar and its cards is the gap, whatever the face.
    /// </summary>
    private POINT AnchorOf(FolderIsland island, RECT work) =>
        Attached(island) && work.right > work.left ? AtBar(faceAnchor, work, shownEdge) : faceAnchor;

    /// <summary>
    /// §647: <paramref name="anchor"/> moved across the bar along <paramref name="edge"/> onto <paramref name="work"/>'s edge there —
    /// the bar's inner side, the folder's work area being the display's less the bar (DockWindow.FolderWork) —, along it where it was. Pure.
    /// </summary>
    internal static POINT AtBar(POINT anchor, RECT work, Edge edge) => edge switch
    {
        Edge.Top => anchor with { y = work.top },
        Edge.Left => anchor with { x = work.left },
        Edge.Right => anchor with { x = work.right },
        _ => anchor with { y = work.bottom },
    };

    /// <summary>
    /// §647: an attached folder's ground in the look of the bar it opened from: the bar's colour (the Appearance's Taskbar, in
    /// Windows' mode, or Windows' own). §651 (the person: "it looks like it is part of it") painted as the bar paints itself
    /// (<see cref="Scene.GroundOf"/>): a see-through bar's colour at the bar's opacity, which the host lays on the wallpaper the
    /// bar is blurred over (FlyoutHost.Paper), so bar and ground are one surface; an opaque bar's solid. A bar of nothing (Clear)
    /// or of Windows' acrylic its colour solid: a ground of nothing would leave the lines' text on whatever is behind. §649 (the
    /// person: "the background should be configurable in the settings") the Appearance's Flyouts' ground in its place where it
    /// says one, as said, its opacity its own; never in high contrast. Pure.
    /// </summary>
    internal static Color AttachedGround(Look bar)
    {
        if (!bar.HighContrast && bar.Worn.Flyouts.Ground.Color is { } said)
        {
            return Color.FromArgb(said.A, said.R, said.G, said.B);
        }
        var tokens = Scene.Theme(bar, 1, 1);
        return tokens.Material is Material.Translucent or Material.Opaque ? Scene.GroundOf(tokens) : FlyoutTheme.Opaque(tokens.Tint);
    }

    /// <summary>
    /// §651: what a see-through ground stands on (FlyoutHost.Paper): the wallpaper its bar is blurred over, for the ground's
    /// rectangle on the screen — the dock's (DockWindow.Paper), a picture's own; null for none: drawn solid. Set before a showing.
    /// </summary>
    internal Func<RECT, CompositionBrush?>? Paper
    {
        get => host.Paper;
        set => host.Paper = value;
    }

    /// <summary>§651: the ground's paper asked for again: the wallpaper composed for it since, or changed.</summary>
    internal void Repaper() => host.Repaper();

    /// <summary>
    /// §385: a folder set to <paramref name="align"/> hung off <paramref name="anchor"/> — the middle of its
    /// face's inner side — beside a bar along <paramref name="edge"/> of <paramref name="work"/>: Automatic
    /// centred on the face, as every folder stood before and as Windows 11's jump lists stand on their
    /// button; Left and Right against the work area's ends (its top and bottom down a side bar), Center in
    /// its middle. Kept inside the work area by the margins every popup keeps (<see cref="FlyoutPlacement.Along"/>).
    /// </summary>
    internal static FlyoutPlacement PlacementOf(POINT anchor, RECT work, Edge edge, FolderAlign align) =>
        new(anchor, work, edge, Align: AlongOf(align));

    /// <summary>§385: a folder's alignment as a popup's placement along the edge.</summary>
    private static FlyoutAlign AlongOf(FolderAlign align) => align switch
    {
        FolderAlign.Left => FlyoutAlign.Start,
        FolderAlign.Center => FlyoutAlign.Middle,
        FolderAlign.Right => FlyoutAlign.End,
        _ => FlyoutAlign.Center,
    };

    /// <summary>§357: where the folder was left as it last closed — its lines scrolled, its day, its page, the line with the keys —; null before it ever did.</summary>
    internal FolderMemory? Left { get; private set; }

    /// <summary>§357: where the open folder stands now (<see cref="FolderMemory"/>); null where it is not open.</summary>
    internal FolderMemory? Remembered() =>
        folder is { } island && plan is not null
            ? island.Remember(focused >= 0 && focused < tiles.Count ? tiles[focused].Island.Key : null, keyboard)
            : null;

    /// <summary>
    /// §334: the open folder drawn in <paramref name="next"/> where it stands, as the dock's look
    /// changed — the folders' material, opacity or colours set in their menu, Windows' mode: its
    /// panels in the folders' look as it is now (<see cref="FlyoutHost.Restyle"/>), its lines' cards,
    /// outlines and highlight in it. Nothing where the look is the one it has. §357: in place — the
    /// brushes coloured again, what is drawn drawn again, nothing of the lines made again (<see cref="Dress"/>).
    /// </summary>
    internal void Relook(Look next)
    {
        if (!IsOpen || gfx is null)
        {
            return;
        }
        // §647: the bar's colour, an attached folder's ground, may change with nothing of the flyouts' look; §649 its lines' look with it.
        barLook = next;
        var changed = Rewear(folder);
        // §651: the bar's blur or its opacity changed: the ground's paper with it.
        host.Repaper();
        if (!changed)
        {
            return;
        }
        Reconcile();
        Draw();
    }

    /// <summary>Closes the folder; <paramref name="restore"/>: the focus goes back to the window in front before it opened.</summary>
    private void Hide(bool restore)
    {
        host.Close(restore);
        Closed();
    }

    /// <summary>
    /// What a message to the folder's window means. The dock passes every one of them here, then its
    /// default handling; an event other than None means the folder has closed.
    /// </summary>
    public FolderFlyoutEvent Message(uint msg, WPARAM wParam, LPARAM lParam)
    {
        // The Escape order in the open folder: a line's text box with the keys has every key first —
        // Escape ends the typing there (notes) — and every character typed; then an app picked in it
        // goes before the folder does: Escape takes the pick back (FolderIsland.Escape); only then
        // does the host close the folder.
        // Arranged, Escape is edit mode's (EditKeys.Escape), as on the bar: a line being moved goes
        // back, and the lines carried with it (FolderFlyout.Pending.cs); only then does the host close the folder, one level out, and edit mode stays on.
        if (Typing(msg, wParam)
            || (msg == WM.WM_KEYDOWN && (int)(nuint)wParam == VK.VK_ESCAPE && IsOpen && !editing && (EscapeHeld() || folder?.Escape() == true))
            || (msg == WM.WM_KEYDOWN && (int)(nuint)wParam == VK.VK_ESCAPE && IsOpen && editing && EditKeys.Escape(carrying: CancelMove()) == EditEscape.Cancel))
        {
            return default;
        }
        // notes: an IME's composition starts at the caret of the line typing, and its candidates
        // under it; the default handling then composes and sends what was chosen as WM_CHAR.
        if (msg == WM.WM_IME_STARTCOMPOSITION)
        {
            PlaceComposition();
        }
        // The folder left the bar (FolderChanged posted this): nothing holds it open any more, not
        // even edit mode's hold, so a folder removed while it is arranged closes at once.
        if (msg == FlyoutHost.WM_KARPO_HOST_DISMISS && folder is { Gone: true })
        {
            host.StaysOpen = false;
        }
        var happened = host.Message(msg, wParam, lParam);
        if (happened.Kind == FlyoutHostEventKind.Dismissed)
        {
            Closed();
            return new FolderFlyoutEvent(FolderFlyoutAction.Dismissed);
        }
        if (!IsOpen || plan is null)
        {
            return default;
        }
        switch (msg)
        {
            case WM.WM_MOUSEMOVE:
                pointerScreen = Screen(lParam);
                // quick: the pointer is the widget's while it holds it (a slider being dragged),
                // and it keeps it past the panel's edge, as the bar's slider does.
                if (holding is not null)
                {
                    _ = host.ToPanel(lParam, out var slid);
                    DragHeld(slid);
                    return default;
                }
                // edit: the selection rectangle, from a press on the empty room, is the dock's to draw.
                if (banding)
                {
                    return new FolderFlyoutEvent(FolderFlyoutAction.BandMoved, At: Screen(lParam));
                }
                // edit: a line carried in the folder lands where the pointer is: in an empty slot
                // beside the lists, a new list there; anywhere else over the lists' columns — their
                // panels, the gaps between them, the room over and under them — between two lines of
                // the list it is over or nearest. Anywhere else it
                // is still carried, and the dock hears where, so a bar under it can say why it
                // won't take it.
                if (editing && moving >= 0 && moving < tiles.Count)
                {
                    travelled = travelled || Past(Screen(lParam));
                    var overFolder = host.ToPanel(lParam, out var over);
                    if (overFolder && dragging >= 0)
                    {
                        // Back over the folder: it is arranged here again, not carried out.
                        dragging = NoTarget;
                    }
                    // Lifted at the press, the line follows the pointer from the first move, as a block of
                    // the bar does (FolderFlyout.Drag.cs); past the threshold it is no click any more.
                    Follow(over);
                    SetHot(NoTarget);
                    // Where it would land, the one hit test (FolderFlyout.Pending.cs): a slot, a gap, or nowhere.
                    if (Aim(over))
                    {
                        return default;
                    }
                    // edit: a line carried with no button held is never carried out (FolderFlyout.Carry.cs).
                    if (!carrying && !Grouped && Dragged(Screen(lParam)))
                    {
                        return new FolderFlyoutEvent(FolderFlyoutAction.DragMove, tiles[moving].Island, moving, At: Screen(lParam));
                    }
                    return default;
                }
                // edit: past the drag threshold at rest, a pressed card turns edit mode on and is
                // carried on as edit mode carries it (HandOver), as a widget dragged on the bar is.
                // A slider took its press above (holding), so a drag on it never gets here.
                // A page's lines are not the folder's to carry: nothing is dragged while one stands.
                if (folder is { Paged: true } && !editing)
                {
                    var onPage = host.ToPanel(lParam, out var pagePoint);
                    var under = TargetAt(pagePoint, onPage);
                    pointerAt = onPage ? pagePoint : null;
                    SetHot(under);
                    HoverOver(under < AddTarget ? under : NoTarget, onPage ? pagePoint : null);
                    return default;
                }
                // §663: on a locked bar neither: a line pressed at rest stays where it stands, as a widget on the bar
                // does (BarMoves.Arranges, the one gate); what the line drags itself took its press above (holding).
                var arranges = BarMoves.Arranges(Locked?.Invoke() == true, editing);
                if (arranges && !editing && pressed >= 0 && pressed < tiles.Count && dragging < 0 && Past(Screen(lParam)) && HandOver(lParam))
                {
                    return default;
                }
                // pins: past the drag threshold a pressed widget is being taken out of the folder.
                if (arranges && pressed >= 0 && pressed < tiles.Count && Dragged(Screen(lParam)) && dragging < tiles.Count)
                {
                    return new FolderFlyoutEvent(FolderFlyoutAction.DragMove, tiles[dragging].Island, dragging, At: Screen(lParam));
                }
                var onPanel = host.ToPanel(lParam, out var point);
                var card = TargetAt(point, onPanel);
                pointerAt = onPanel ? point : null;
                SetHot(card);
                HoverOver(card < AddTarget ? card : NoTarget, onPanel ? point : null);
                return default;
            case WM.WM_MOUSELEAVE:
                pointerAt = null;
                pointerScreen = null;
                SetHot(NoTarget);
                HoverOver(NoTarget, null);
                return default;
            case WM.WM_LBUTTONDOWN:
            {
                var downOnPanel = host.ToPanel(lParam, out var down);
                var target = TargetAt(down, downOnPanel);
                // edit: a "+" outside the panel takes its press too, and an empty slot's room (§220);
                // anywhere else outside it is nobody's.
                // notes: a press anywhere but on the line typing in it ends its typing, as a click
                // away from a text box does; what was typed is kept.
                if (Typer() is { } typer && typer.Index != target)
                {
                    StopTyping(typer.Island);
                }
                if (downOnPanel || target != NoTarget)
                {
                    keyboard = false;
                    // keys: the pointer took over; the next key starts from the line, not a stop it left.
                    keyPart = null;
                    HideTip();
                    pressed = target;
                    dragging = NoTarget;
                    moving = NoTarget;
                    landing = null;
                    dragFrom = Screen(lParam);
                    travelled = false;
                    // edit: a press on a line picks it up, where it was taken hold of (FolderFlyout.Drag.cs).
                    TakeHold(pressed, down);
                    Group([]);
                    if (editing && pressed >= 0 && pressed < tiles.Count)
                    {
                        moving = pressed;
                        // edit: one of several selected lines carries them all (FolderFlyout.Pending.cs).
                        Group(SelectedWith(moving));
                        // edit: lifted at once, under the move cursor, as a block of the bar is at its
                        // press (Dock.EditPressed): a click lets it settle back.
                        Follow(down);
                        _ = SetCursor(LoadCursorW(HINSTANCE.NULL, IDC.IDC_SIZEALL));
                    }
                    // quick: a widget that takes the pointer itself (the volume slider) is asked
                    // first: it gets the press, the moves and the release, and the click that
                    // would have followed is the drag's. Not while the folder is arranged, where
                    // a press picks the widget up.
                    TakePointer(pressed, down);
                    SetHot(pressed);
                    ShowStates();
                    if (pressed != NoTarget)
                    {
                        _ = SetCapture(Hwnd);
                    }
                    // edit: a press on the arranged folder's empty room — between and round its
                    // lines — may draw the selection rectangle, as on the bar's (EditClicks.OnPress).
                    else if (editing && EditClicks.OnPress(grip: false, taken: target != NoTarget) == EditPress.Select)
                    {
                        banding = true;
                        _ = SetCapture(Hwnd);
                        return new FolderFlyoutEvent(FolderFlyoutAction.BandPressed, At: Screen(lParam));
                    }
                }
                return default;
            }
            case WM.WM_LBUTTONUP:
            {
                // edit: the selection rectangle's press came up: the dock's to end.
                if (banding)
                {
                    banding = false;
                    if (GetCapture() == Hwnd)
                    {
                        _ = ReleaseCapture();
                    }
                    return new FolderFlyoutEvent(FolderFlyoutAction.BandReleased, At: Screen(lParam));
                }
                // quick: the release ends the widget's own drag; it is not a click on the card.
                if (holding is not null)
                {
                    ReleaseHeld();
                    pressed = NoTarget;
                    dragging = NoTarget;
                    if (GetCapture() == Hwnd)
                    {
                        _ = ReleaseCapture();
                    }
                    var back = host.ToPanel(lParam, out var after);
                    SetHot(TargetAt(after, back));
                    ShowStates();
                    return default;
                }
                var was = pressed;
                var dragged = dragging;
                pressed = NoTarget;
                dragging = NoTarget;
                if (GetCapture() == Hwnd)
                {
                    _ = ReleaseCapture();
                }
                var inside = host.ToPanel(lParam, out var up);
                var under = TargetAt(up, inside);
                // edit: let go where it was shown landing, a widget goes there. Where it would have
                // changed nothing — its own place — nothing was shown (Ghost), and nothing happens.
                var placed = editing && moving >= 0 ? landing : null;
                var carried = moving;
                var together = PlacedKeys;
                // It settles from where it shows, wherever it goes (FolderFlyout.Drag.cs).
                Drop();
                moving = NoTarget;
                landing = null;
                Group([]);
                ShowStates();
                if (placed is { } place && carried >= 0 && carried < tiles.Count)
                {
                    return new FolderFlyoutEvent(FolderFlyoutAction.Place, tiles[carried].Island, carried, Place: place, Keys: together);
                }
                // pins: dragged out of the folder, it is the dock's to put on the bar under the pointer.
                if (dragged >= 0 && dragged < tiles.Count)
                {
                    return new FolderFlyoutEvent(FolderFlyoutAction.DragDrop, tiles[dragged].Island, dragged, At: Screen(lParam));
                }
                // edit: a line carried past the drag threshold was being arranged: let go anywhere,
                // even back on its own place, it is no click.
                var arranged = editing && was >= 0 && was < tiles.Count && (travelled || Past(Screen(lParam)));
                travelled = false;
                if (arranged || was == NoTarget || was != under)
                {
                    return default;
                }
                var clicked = Activate(was, up);
                // edit: where it was clicked, a click of the pointer's rather than the keys'; and
                // where a line's own menu opens that its click asked for.
                return clicked.Kind is FolderFlyoutAction.Settings or FolderFlyoutAction.Context or FolderFlyoutAction.Setup ? clicked with { At = Screen(lParam) } : clicked;
            }
            // edit: a right press while a line is being moved puts it back, as on the bar, and its
            // release opens no menu.
            case WM.WM_RBUTTONDOWN when CancelMove():
                rightCancelled = true;
                return default;
            // folders: a widget's own menu, shown by the dock at the pointer, over the folder.
            case WM.WM_RBUTTONUP:
            {
                if (rightCancelled)
                {
                    rightCancelled = false;
                    return default;
                }
                // edit: a line's right click is its click's (§214), no second menu of its own (the
                // person, 2026-10-19: "the separate right click menus in the edit mode should be
                // removed"); the arranged folder's own room, on no line, has the folders' look, as a
                // bar's room has the taskbar's (§290, §334).
                if (!host.ToPanel(lParam, out var where) || TargetAt(where, true) is var target && (target < 0 || target >= tiles.Count))
                {
                    if (editing && host.Contains(Screen(lParam)) && host.ToPanel(lParam, out var inside) && TargetAt(inside, true) == NoTarget)
                    {
                        host.StaysOpen = true;
                        return new FolderFlyoutEvent(FolderFlyoutAction.Looks, At: Screen(lParam));
                    }
                    return default;
                }
                // The folder stays open behind the menu, in edit mode and out of it (the person:
                // "for all widgets on right click it should not close the folder anymore"): a
                // menu about a widget in a folder is a menu over the folder, not instead of it. It
                // holds itself open while the menu has the foreground — as a switcher's list does
                // for a row's menu — and the dock lets it go when the menu ends (MenuClosed).
                //
                // Regression: at rest the folder used to close first, and closing it empties its
                // tiles, so the widget read after it was an index into nothing and every right
                // click on a widget in a folder threw instead of showing a menu.
                // A page's lines have no menu: they are no widgets of the folder's (FolderIsland.Pages.cs).
                if (folder is { Paged: true })
                {
                    return default;
                }
                var widget = tiles[target].Island;
                host.StaysOpen = true;
                // edit: arranged, the line's card on the screen, which the one menu stands beside, as
                // a click's does (Dock.LineClicked): a right click selects and opens what a click does.
                return new FolderFlyoutEvent(FolderFlyoutAction.Context, widget, target, At: Screen(lParam),
                    Field: editing && plan is { } arranged && target < arranged.Cards.Count ? CardOnScreen(arranged.Grab(target)) : default);
            }
            case WM.WM_CAPTURECHANGED:
                if (banding && (HWND)(void*)(nint)lParam != Hwnd)
                {
                    banding = false;
                    return new FolderFlyoutEvent(FolderFlyoutAction.BandLost);
                }
                if (pressed != NoTarget && (HWND)(void*)(nint)lParam != Hwnd && !handingOver)
                {
                    // quick: the capture went elsewhere; a widget holding the pointer lets it go
                    // where the drag had got to, as the bar's slider does.
                    ReleaseHeld();
                    Drop();
                    pressed = NoTarget;
                    dragging = NoTarget;
                    moving = NoTarget;
                    landing = null;
                    Group([]);
                    ShowStates();
                }
                return default;
            case WM.WM_MOUSEWHEEL:
            {
                var delta = (short)(((nuint)wParam >> 16) & 0xFFFF);
                // A line of items under the pointer that has more than it shows scrolls them, by
                // what the wheel or the touchpad said, until they reach their end (ListScroll.cs);
                // past it the wheel does nothing — the folder itself never scrolls (2026-10-12). A
                // line that takes the wheel itself (a mixer's sliders) is asked by the notch, below, first.
                var over = new POINT { x = (short)((nint)lParam & 0xFFFF), y = (short)(((nint)lParam >> 16) & 0xFFFF) };
                // §424: a line whose part moves on the compositor (the month's weeks) takes the wheel as it turned,
                // a touchpad's fine steps too, and moves by what it says.
                if (WheelStrip(over, delta))
                {
                    wheel = 0;
                    return default;
                }
                if (!TakesWheel(over) && ScrollLine(over, delta))
                {
                    wheel = 0;
                    return default;
                }
                wheel += delta;
                var notches = wheel / 120;
                if (notches != 0)
                {
                    wheel -= notches * 120;
                    // The widget under the pointer is asked first, and only a
                    // line that said it takes the wheel is asked at all (Interaction.Wheel, which
                    // the widget its factory made carries - the declaration, rather than a second
                    // answer grown in here); else a line of items that takes the wheel scrolls its
                    // items. (The wheel's lParam is in screen px, not the window's, so it is read as
                    // it is rather than through Screen.)
                    var turned = new POINT { x = (short)((nint)lParam & 0xFFFF), y = (short)(((nint)lParam >> 16) & 0xFFFF) };
                    if (!WheelOver(turned, notches) && TakesWheel(turned))
                    {
                        _ = ScrollLine(turned, notches * 120);
                    }
                }
                return default;
            }
            case WM.WM_KEYDOWN:
                // Bit 30 of lParam: the key was down already, a repeat of a key held down.
                return Key((int)(nuint)wParam, ((nint)lParam & (1 << 30)) != 0);
            // Alt+Up and Alt+Down come as system keys: offered to the line with the focus, which moves
            // the row it is on a place (a desktop, a task), and nothing else of the folder's.
            case WM.WM_SYSKEYDOWN when !editing && (int)(nuint)wParam is VK.VK_UP or VK.VK_DOWN && FocusedWidget() is { } moving:
                try
                {
                    if (moving.KeyOn((int)(nuint)wParam, WidgetSurface.List))
                    {
                        ShowStates();
                    }
                }
                catch (Exception error) when (error is not OutOfMemoryException)
                {
                    Console.Error.WriteLine($"Karpo: a folder's {moving.Info.Id} key failed: {error.Message}");
                }
                return default;
            // clipboard: a line that took a key's press to act on its release (CellIsland.KeyUpOn)
            // acts now, as a click on it would: the clipboard's line pastes only once the key that
            // chose an item is let go, so none of its repeats reaches the window pasted into.
            case WM.WM_KEYUP when !editing && FocusedWidget() is { } letGo:
            {
                bool acts;
                try
                {
                    acts = letGo.KeyUpOn((int)(nuint)wParam, WidgetSurface.List);
                }
                catch (Exception error) when (error is not OutOfMemoryException)
                {
                    Console.Error.WriteLine($"Karpo: a folder's {letGo.Info.Id} key failed: {error.Message}");
                    return default;
                }
                return acts ? Activate(focused, null) : default;
            }
            case WM.WM_TIMER when (nuint)wParam == TickTimer:
                Tick();
                return default;
            // edit: a settle done (FolderFlyout.Drag.cs).
            case WM.WM_TIMER when (nuint)wParam == SettleTimer:
                _ = DragTimer((nuint)wParam);
                return default;
            case WM.WM_TIMER when (nuint)wParam == GlideTimer:
                Glide();
                return default;
            case WM.WM_TIMER when (nuint)wParam == QuickTimer:
                QuickTick();
                return default;
            case WM.WM_TIMER when (nuint)wParam == TipTimer:
                ShowTip();
                return default;
            case WM.WM_TIMER when (nuint)wParam == NoteTimer:
                NoteDone();
                return default;
            default:
                return default;
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        Closed();
        // §357: what the folder kept goes with its window, once its last fade has run out.
        ReleaseKept();
        host.Dispose();
        tip?.Dispose();
        noteTip?.Dispose();
    }

    // --- What it shows -----------------------------------------------------------

    /// <summary>
    /// list: <paramref name="left"/> px shared among Fill lines that can each take
    /// <paramref name="rooms"/> more (§168): equally, in whole px, the first lines a px more where it
    /// does not divide and the first the fraction left; a line's share past its room goes to the
    /// others; what none can take is not given.
    /// </summary>
    internal static float[] FillShares(float left, IReadOnlyList<float> rooms)
    {
        var given = new float[rooms.Count];
        var rest = left;
        var open = Enumerable.Range(0, rooms.Count).Where(at => rooms[at] > 0).ToList();
        while (rest > 0.001f && open.Count > 0)
        {
            var share = MathF.Max(MathF.Min(1, rest), MathF.Floor(rest / open.Count));
            foreach (var at in open)
            {
                var take = MathF.Min(MathF.Min(share, rest), rooms[at] - given[at]);
                given[at] += take;
                rest -= take;
            }
            open.RemoveAll(at => rooms[at] - given[at] <= 0.001f);
        }
        return given;
    }

    /// <summary>
    /// The folder laid out for <paramref name="options"/> and its <paramref name="cells"/> at
    /// <paramref name="scale"/>: its lists side by side, each a panel of its own, as wide as its own
    /// preset (<see cref="FolderOptions.WidthOf"/>) and as tall as what is in it — up to all of <paramref name="room"/> less an arranged
    /// list's "+" (<see cref="Reach"/>; §173, §185), past which its last lines are not shown (<see cref="FolderListPlan.Hidden"/>) — lined up on the bar's side, a folder
    /// separator's room apart (<see cref="SeparatorRoom"/>). Pure, so it is also what a test or an offscreen picture
    /// draws.
    /// <para>
    /// <b>Arranging changes none of it</b> (<paramref name="editing"/>): the same lines in the same
    /// places at the same size, and only the "+" buttons added, outside the panels
    /// (<see cref="FolderPlan.Adds"/>), so nothing moves when edit mode begins or ends (the person,
    /// 2026-10-09: list spacing "must be identical in edit and view mode"). A list's end "+" stands
    /// at its own end away from the bar's <paramref name="edge"/>.
    /// </para>
    /// <para>
    /// Which list a cell is in is <paramref name="options"/>' (<see cref="FolderOptions.Lists"/>); one
    /// it does not have stands at the end of the first. <paramref name="view"/> is the folder's (a
    /// list, the one view there is, kept for the callers' shape). Never narrowed to its display
    /// (<see cref="ListWidths"/>, §320).
    /// </para>
    /// </summary>
    /// <summary>
    /// The most a folder's lists may reach away from the bar, in px at <paramref name="scale"/>:
    /// <paramref name="room"/> (<see cref="FlyoutPlacement.Room(float)"/>, or for a plan drawn without
    /// a placement <see cref="FlyoutPlacement.Room(float, float)"/> of <paramref name="workHeight"/>):
    /// the work area's far end less the flyouts' margin, nothing kept back (§320). Arranged, the
    /// buttons of a list as tall as that stand just inside its far end (<see cref="WithSlots"/>)
    /// rather than every folder keeping a row of room for them at rest too, which stopped a Fill line
    /// 30 DIPs short of where it should end.
    /// </summary>
    internal static float Reach(float workHeight, float room, float scale) =>
        MathF.Floor(room > 0 ? room : FlyoutPlacement.Room(workHeight, scale));

    /// <summary>
    /// The tallest a list may be, in px at <paramref name="scale"/> (<see cref="Build"/>): all the room
    /// the folder has (<see cref="Reach"/>), and never less than 70 % of <paramref name="workHeight"/>.
    /// </summary>
    internal static float ListMost(float workHeight, float room, float scale) =>
        MathF.Max(MathF.Floor(workHeight * 0.7f), Reach(workHeight, room, scale));

    /// <summary>
    /// The most whole cells a list's room holds (<see cref="ListMost"/>): what a line may be set to on this screen,
    /// the real most now that no kind has one of its own (§192).
    /// </summary>
    internal static int RoomCells(float workHeight, float room, float scale, float textScale) =>
        LineHeight.Room(ListMost(workHeight, room, scale), CellPx(scale, textScale));

    /// <summary>
    /// §442: the folder's cell in px at <paramref name="scale"/> and <paramref name="textScale"/>, half the list's row
    /// (<see cref="FolderOptions.CellDip"/>), on a whole px: every line of a folder is whole cells, and n cells are n
    /// times this, exactly, so lines that add up to the same cells end level whatever they are.
    /// </summary>
    internal static float CellPx(float scale, float textScale) =>
        PopupPaint.Round(FolderOptions.CellDip * scale * Math.Clamp(textScale > 0 ? textScale : 1, 1, 2.25f));

    /// <summary>
    /// How much of the room a shorter column leaves beside the tallest stands over it, on a bar along
    /// <paramref name="edge"/>: none on a top bar, where the columns hang from it; all of it on a
    /// bottom bar, where they stand on it; half on a side bar, where each column is centred along the
    /// bar on the folder's face, as a bottom bar's columns are centred across it (§303).
    /// </summary>
    internal static float Over(Edge edge) => edge switch
    {
        Edge.Top => 0,
        Edge.Left or Edge.Right => 0.5f,
        _ => 1,
    };

    /// <summary>
    /// Whether the first column stands on the right, the others leftward: on a right bar, so the
    /// first column is next to the bar as on a left bar; on a bottom or top bar the first is on the
    /// left, where the bar starts (§303).
    /// </summary>
    internal static bool FirstOnRight(Edge edge) => edge == Edge.Right;

    /// <summary>
    /// §440: of one list's lines in their <paramref name="order"/>, those gone at rest: the lines of
    /// <paramref name="idle"/> (§327, a now-playing line while nothing plays), and each divider that, with
    /// them gone, would stand first or last in the list or right after another divider. Only a divider
    /// beside a line gone: one the person put at an end of a list stays where they put it.
    /// </summary>
    internal static HashSet<int> GoneAtRest(IReadOnlyList<int> order, IReadOnlyCollection<int> idle, Func<int, bool> divides)
    {
        var gone = new HashSet<int>(order.Where(idle.Contains));
        if (gone.Count == 0)
        {
            return gone;
        }
        // Beside a line gone, or a divider gone with one.
        bool Beside(int at) => (at > 0 && gone.Contains(order[at - 1])) || (at + 1 < order.Count && gone.Contains(order[at + 1]));
        var kept = new List<int>();
        for (var at = 0; at < order.Count; at++)
        {
            var index = order[at];
            if (gone.Contains(index))
            {
                continue;
            }
            if (divides(index) && Beside(at) && (kept.Count == 0 || divides(kept[^1])))
            {
                _ = gone.Add(index);
                continue;
            }
            kept.Add(index);
        }
        while (kept.Count > 0 && divides(kept[^1]) && Beside(IndexIn(order, kept[^1])))
        {
            _ = gone.Add(kept[^1]);
            kept.RemoveAt(kept.Count - 1);
        }
        return gone;

        static int IndexIn(IReadOnlyList<int> order, int index)
        {
            for (var at = 0; at < order.Count; at++)
            {
                if (order[at] == index)
                {
                    return at;
                }
            }
            return -1;
        }
    }

    internal static FolderPlan Build(
        Look look,
        float scale,
        FolderView view,
        FolderOptions options,
        IReadOnlyList<FolderCell> cells,
        float workHeight,
        bool editing = false,
        IReadOnlyList<float>? natural = null,
        Edge edge = Edge.Bottom,
        float room = 0,
        FolderPending? pending = null,
        IReadOnlyCollection<int>? resting = null,
        bool attached = false)
    {
        // edit: what is about to change (FolderPending, §239): lines landing somewhere are laid out
        // there, the folder moved as the drop would move it; lines over no place are in no list.
        var carried = pending is { } change ? [.. change.Lines.Where(index => index >= 0 && index < cells.Count)] : new List<int>();
        if (pending is { To: { } to })
        {
            options = FolderDrag.Moved(options, [.. carried.Select(index => cells[index])], to);
        }
        IReadOnlyCollection<int>? away = pending is { To: null } ? carried : null;
        // §327: at rest a line with nothing to show, set to hide while it has not (CellIsland.HiddenAtRest),
        // is in no list: no room, no box, its list closed up and, emptied, no list at all. Arranged
        // it stands where it stands, greyed, so nothing moves as edit mode comes.
        var idle = editing ? null : resting;
        var textScale = Math.Clamp(look.TextScale > 0 ? look.TextScale : 1, 1, 2.25f);
        float D(float dip) => PopupPaint.Round(dip * scale);
        var pad = D(12);
        // A line per widget, in its list's order. What is drawn on a line is the kind's own question
        // (Sections.CardInList): a component draws its card, anything else draws the list's row of
        // an icon and a name. How tall the line is is a different question and a different number
        // (Sections.ListRowsOf) — tying the two together is what padded the day switcher to twice
        // its content (docs\widgets.md §9). A list is one column, so a widget's height is all it
        // declares and the width is the folder's.
        // **The grid** (§442): every line is whole cells, half a row each, at the text size and the scale, with no
        // gap between them, so n cells are n cells' px exactly and lines that add up to the same cells end level.
        var cellPx = CellPx(scale, textScale);
        var line = LineHeight.Row * cellPx;
        float CellsPx(int count) => count * cellPx;
        // **How tall a line is given** (LineHeight, docs\widgets.md §76): exactly its rows where its
        // height is fixed; else what its content works out to, from one row up to its rows. The
        // kind's list measurement is only where a new line's height starts.
        //
        // The content's own height is not something this can measure: Build takes no device, by
        // design, so that a test, a picture and the live panel agree about geometry without one. It
        // arrives in <natural>, measured by whoever has the widgets (the live panel asks each
        // component, IListContent), in px at this scale. Where nothing measured a line, it is what
        // its kind declares (ListRowsOf).
        //
        // An automatic line is its content rounded up to whole cells (§442): never more than a cell of air, and on the
        // grid with every other line.
        float Tall(int index)
        {
            if (cells[index]?.Island is not { } island)
            {
                return line;
            }
            // A divider is the room every folder separator takes, whatever its range says: a row of
            // the list's height for one line would push its two neighbours apart as far as a widget does.
            if (Divides(cells[index]))
            {
                return SeparatorRoom(scale);
            }
            // A heading is a section heading's height and not a row's: its words and the air over
            // and under them (HeadingTile.Height; the person, 2026-10-11: "it does not even have to
            // conform to the 1cell height, it can have its own size"). Nobody sets it.
            if (Heads(cells[index]))
            {
                // A page's head (PageHeadTile) says its own: a quick settings page's title row.
                return natural is { } said && index < said.Count && said[index] > 0 ? said[index] : HeadingTile.Height(scale, textScale);
            }
            var kind = ListWidgets.Find(island.Kind);
            // §432, §442: a kind of one form or a few is its size's form, measured, in whole cells, the form designed to
            // fill them and standing in their middle (CellIsland.Scrolled).
            var sized = kind is { Forms: not null };
            var declared = CellsPx(sized ? LineSizes.CellsOf(island) : kind is { } row ? row.Kind.ListCellsOf() : LineHeight.Row);
            var content = natural is { } measured && index < measured.Count && measured[index] > 0
                ? measured[index]
                : declared;
            return sized ? CellsPx(LineHeight.CellsFor(content, cellPx)) : LineHeight.For(island).Given(content, cellPx);
        }
        bool Fills(int index) => cells[index]?.Island is { } island && !Divides(cells[index]) && !Heads(cells[index]) && LineHeight.For(island).Fill;
        // §435: a divider that grows is its gap made taller, to all the room the folder has (§482), which it shares
        // with the column's other growing dividers and its Fill lines, evenly.
        bool Takes(int index) => Fills(index) || Grows(cells[index]);
        bool Gives(int index) => cells[index]?.Island is { } island && !Divides(cells[index]) && !Heads(cells[index]) && LineHeight.For(island) is { GivesWay: true, Fill: false };
        var hairline = SeparatorInk.Hairline(scale);
        // **Each list is a panel of its own** (the person, 2026-10-09: "they should actually be
        // shown separately, also have their separate heights"), its own material, corners, rim and
        // shadow — never one panel with a hairline between its lists — apart from the next by the
        // divider gap (the person, 2026-10-10: "the spacing between list views should be the same
        // as the divider spacing"): two lists stand as far apart as a separator's room. §651 attached, on one ground with no
        // air in it, they meet: apart by the seam drawn between them (FlyoutHost.Seams), the folder's outline's weight.
        var listGap = attached ? SeamOf(look, scale) : SeparatorRoom(scale);
        var lists = Math.Max(1, options.Lists?.Count ?? 0);
        // Which list each cell is in, and where in it; a cell the folder does not have, at the end of
        // the first, after everything there.
        var listOf = new int[cells.Count];
        var rankOf = new int[cells.Count];
        for (var index = 0; index < cells.Count; index++)
        {
            (listOf[index], rankOf[index]) = cells[index] is { } cell && Folders.PlaceOf(options, cell) is { } at
                ? (at.List, at.Index)
                : (0, int.MaxValue);
        }
        var heights = new float[cells.Count];
        for (var index = 0; index < cells.Count; index++)
        {
            heights[index] = Tall(index);
        }
        // Each list's lines, top to bottom.
        var orders = new List<int>[lists];
        for (var list = 0; list < lists; list++)
        {
            var at = list;
            orders[list] = [.. Enumerable.Range(0, cells.Count).Where(index => listOf[index] == at && away?.Contains(index) != true)
                .OrderBy(index => rankOf[index]).ThenBy(index => index)];
        }
        // §327: the lines gone at rest leave the flow; §440: and a divider they leave at an end of their
        // list or right after another goes with them (GoneAtRest), as if they were not there.
        if (idle is { Count: > 0 })
        {
            var gone = new HashSet<int>();
            foreach (var order in orders)
            {
                gone.UnionWith(GoneAtRest(order, idle, index => Divides(cells[index])));
                order.RemoveAll(gone.Contains);
            }
            idle = gone;
        }
        // **Each list as wide as its own preset** (FolderOptions.Widths; the person, 2026-10-10:
        // "change the width of individual columns"): narrow, medium — every panel's width, and a
        // list's until somebody chooses — or wide. The same answer whatever measures a line's
        // content is given (ListWidth), so the two cannot be a pixel apart.
        //
        // **At rest an empty list before the first with widgets, or after the last, is no list**
        // (the person, 2026-10-12: "an empty column is sometimes not viewed as non existing column
        // which leads to the folder being shifted to the left without actually having widgets on
        // the right side"): no width and no gap, so the box round the lists — what is centred on
        // the folder's button — is the lists that show. One between two lists with widgets is its
        // width of room, a spacer, as before. Arranged every list keeps its width (§149), and an
        // outer one hangs off the side of the lists with widgets, which stand where they stood at
        // rest: they are what is centred (FolderPlan.Core; the person, 2026-10-13: "even if a column
        // is empty it is part of the container that gets centered, this should not be").
        var first = Array.FindIndex(orders, order => order.Count > 0);
        var last = Array.FindLastIndex(orders, order => order.Count > 0);
        bool Outer(int list) => !editing && first >= 0 && (list < first || list > last);
        // Each list's preset, whatever the display: one too wide for it runs past its edge (§320).
        var fitted = ListWidths(options, scale);
        var listWidths = new float[lists];
        var lefts = new float[lists];
        var reach = 0f;
        var started = false;
        for (var list = 0; list < lists; list++)
        {
            // An outer empty list stands at the edge of the lists beside it, of no width.
            if (Outer(list))
            {
                lefts[list] = started ? reach : 0;
                continue;
            }
            listWidths[list] = fitted[list];
            lefts[list] = started ? reach + listGap : 0;
            reach = lefts[list] + listWidths[list];
            started = true;
        }
        // On a right bar the first column is next to the bar, the others leftward (FirstOnRight).
        if (FirstOnRight(edge))
        {
            for (var list = 0; list < lists; list++)
            {
                lefts[list] = reach - lefts[list] - listWidths[list];
            }
        }
        float LeftOf(int list) => lefts[list];
        float WidthOf(int list) => listWidths[list];
        // From the list's own top edge: the only spacing in a list is the widgets' own.
        //
        // **No line of the list's own between two widgets.** It drew a hairline between every two
        // lines; the person ruled those out (2026-10-05: "the divider in between widgets should in
        // list view should not be there per default, you have to add it deliberately and can also
        // move it around then"). What parts two widgets is a divider somebody put there, a line of
        // the list like the others that splits it into two cards (DividerTile).
        //
        // **A list's items reach the panel's own edge**, with no inset between a line and the
        // border, at the person's asking: "the general contents should not get a spacing, the
        // widgets should basically touch the border of the list folder view" — and on every side:
        // "the folder list view i belive has additional padding at the top and bottom. this should
        // not be. the widgets own spacing should be the only one that is applied." The panel clips
        // its content to a rounded geometry (FlyoutHost), so a plate flush with the top is not cut
        // by the corner, it becomes the corner.
        //
        // **A divider splits the list into cards** (the person, 2026-10-09: "a divider inside a list
        // splits that list's panel background into separate cards with a gap ... NO line"): its
        // gap is the whole distance from the line over it to the line under it, and the lines
        // between two dividers stand on one card (runs).
        //
        // **The lines of a card stand edge to edge** (the person, 2026-10-29: "the widgets themselves
        // in the folder have a gap between them, this should not be, there should bascially be no
        // folder background visible at all"; §410): no room between two, so they tile their card.
        var cards = new D2D_RECT_F[cells.Count];
        var cardRun = new int[cells.Count];
        var contentHeights = new float[lists];
        var runsOf = new List<(float Top, float Bottom)>[lists];
        var hiddenOf = new List<int>[lists];
        var emptyCells = new List<(int List, D2D_RECT_F Box)>();
        // The tallest a list may be: **all the room the folder has** (Reach), and never less than one
        // line with its padding. A Fill line fills it (the person, 2026-10-14: "the notifications
        // should go up to the top of the screen"; §173), and every other folder may be as tall too
        // (the person, 2026-10-16: "the column still has a lot of space at the top but it already
        // says that it does not have any space left for some widgets"; §185): the 70 % every other
        // panel stops at left the top third of the screen empty over lines it did not show. Never
        // less than that 70 % either, for a plan drawn on a work area taller than any screen.
        var far = Reach(workHeight, room, scale);
        var most = MathF.Max(line + (2 * pad), ListMost(workHeight, room, scale));
        var fillRoom = MathF.Max(line, far);
        var listMost = MathF.Max(line, most);
        // What the Fill lines' lists grow to: the room, under the most (a sheet's work area taller
        // than any screen keeps every line and still fills to a screen's room).
        var fillTo = MathF.Min(listMost, fillRoom);
        for (var list = 0; list < lists; list++)
        {
            // **No list scrolls** (the person, 2026-10-12: "a folder should never show a scrollbar,
            // it should hide the widgets that dont fit and show a warning sign like the taskbar does
            // it"): the lines that do not fit under the most are not shown, whole lines from the
            // list's end, as the bar drops the widgets it has no room for (Layout.NotShown); arranged,
            // the list's warning button says which (FolderAddKind.Hidden). A first line taller than
            // the most is given the most: it is all the list shows, and a line of items scrolls its
            // own items (ListScroll.cs).
            hiddenOf[list] = [];
            // No line is taller than the room (§192): a height set past it — in Settings, which knows
            // no screen, or on a taller one — asks for all of the room, and is given what the lines
            // over it leave of it (below), never left out for being as tall as the person asked.
            var roomy = new HashSet<int>();
            foreach (var index in orders[list])
            {
                if (heights[index] >= listMost)
                {
                    heights[index] = listMost;
                    roomy.Add(index);
                }
            }
            var flowed = Flow(list, orders[list]);
            // **A Fill line gives way first** (2026-10-14, the person: "then you can actually make the
            // notifications widget to grow"; §171): where the list is past the most, its Fill lines
            // give back what it is over, shared as the room is shared, each down to one row, before
            // any line is left out — the notifications over the calendar grow with their count up to
            // what the calendar leaves under the most, and scroll past it, never pushing it off.
            var over = flowed - listMost;
            var yielding = orders[list].Where(Fills).ToList();
            if (over > 0.5f && yielding.Count > 0)
            {
                var back = FillShares(over, [.. yielding.Select(index => MathF.Max(0, heights[index] - line))]);
                for (var at = 0; at < yielding.Count; at++)
                {
                    heights[yielding[at]] -= back[at];
                }
                flowed = Flow(list, orders[list]);
            }
            // **A page's line that gives way** (LineHeight.GivesWay; the sound page's mixer, §200) is
            // its content's height, every app, until its list would be past 80 % of the work area or
            // the most, whichever is less; past that it gives back what the list is over, down to one
            // row, and scrolls its items, so the lines over and under it stay.
            var giving = orders[list].Where(Gives).ToList();
            var givingMost = MathF.Min(listMost, MathF.Max(line, MathF.Floor(workHeight * LineHeight.GivingMost)));
            if (flowed - givingMost is var past && past > 0.5f && giving.Count > 0)
            {
                var back = FillShares(past, [.. giving.Select(index => MathF.Max(0, heights[index] - line))]);
                for (var at = 0; at < giving.Count; at++)
                {
                    heights[giving[at]] = MathF.Floor(heights[giving[at]] - back[at]);
                }
                flowed = Flow(list, orders[list]);
            }
            var cut = orders[list].FindIndex(index => cards[index].bottom > listMost + 0.5f);
            // A line that asked for the whole room, where it is the first past the most, takes what is
            // left of it down to one row; the lines under it are then the ones left out.
            if (cut > 0 && orders[list][cut] is var tall && roomy.Contains(tall) && listMost - cards[tall].top >= line)
            {
                heights[tall] = MathF.Floor(listMost - cards[tall].top);
                flowed = Flow(list, orders[list]);
                cut = orders[list].FindIndex(index => cards[index].bottom > listMost + 0.5f);
            }
            if (cut > 0)
            {
                // A divider left last parts nothing from nothing: it goes with what it stood over.
                while (cut > 0 && Divides(cells[orders[list][cut - 1]]))
                {
                    cut--;
                }
                hiddenOf[list] = orders[list][cut..];
                flowed = Flow(list, orders[list][..cut]);
                foreach (var index in hiddenOf[list])
                {
                    // No box, at the foot of what the list shows: drawn nowhere and found nowhere,
                    // and a line carried under the last shown lands before it (FolderDrag.Gap).
                    cards[index] = PopupPaint.Rect(LeftOf(list), flowed, WidthOf(list), 0);
                    cardRun[index] = -1;
                }
            }
            contentHeights[list] = flowed;
        }
        // **Fill** (the person, 2026-10-14: "grow / take up remaining space ... only in the folder";
        // §168, §173): a list with Fill lines is as tall as all the room the folder has (the most
        // above), its lines laid out as above first — the Fill ones at their content — and the room
        // left shared among its Fill lines equally, in px — only a chart that stretches has a most of its own (§432), so
        // every other Fill line reaches the top of a tall screen too. The other lists
        // keep their heights. The room is the work area's, never an empty list's slot arranged, so
        // edit mode moves nothing; growing stops at it, so it never hides a line. **A growing divider** (§435) is
        // alike (the person, 2026-11-02: "it only grows to the highest widget in the folder and not all the way to the
        // top or bottom"; §482): its list too is as tall as all that room, which it shares with the list's other growing
        // dividers and its Fill lines, evenly — not the room beside the folder's tallest list.
        for (var list = 0; list < lists; list++)
        {
            if (orders[list].Except(hiddenOf[list]).Any(Takes))
            {
                Grow(list, fillTo);
            }
        }
        // List <list>'s room left under <to> shared among the lines that take it (Fill lines and growing dividers), evenly in
        // whole px (FillShares), in the list's order, and the list laid out again. No Fill line has a most of its own since
        // §603: the charts that stretched to one are sized.
        void Grow(int list, float to)
        {
            var shown = orders[list].Except(hiddenOf[list]).ToList();
            var taking = shown.Where(Takes).ToList();
            var left = to - contentHeights[list];
            if (taking.Count == 0 || left <= 0)
            {
                return;
            }
            var given = FillShares(left, [.. taking.Select(_ => float.PositiveInfinity)]);
            for (var at = 0; at < taking.Count; at++)
            {
                heights[taking[at]] += given[at];
            }
            var flowed = Flow(list, shown);
            foreach (var index in hiddenOf[list])
            {
                cards[index] = PopupPaint.Rect(LeftOf(list), flowed, WidthOf(list), 0);
            }
            contentHeights[list] = flowed;
        }
        // List <list>'s lines <shown> laid out from its own top (cards, runs): how tall the list is. Each line's
        // edges on whole px (§410): two lines meeting on a fraction of one would each cover part of it, and the
        // panel would show through the seam between them as a faint line.
        float Flow(int list, IReadOnlyList<int> shown)
        {
            var flowing = 0f;
            var runs = runsOf[list] = [];
            float? runTop = null;
            var runBottom = 0f;
            foreach (var index in shown)
            {
                // A divider's room is all there is between the lines either side.
                var (top, bottom) = (PopupPaint.Round(flowing), PopupPaint.Round(flowing + heights[index]));
                cards[index] = PopupPaint.Rect(LeftOf(list), top, WidthOf(list), bottom - top);
                flowing += heights[index];
                if (Divides(cells[index]))
                {
                    // §505: empty space, which splits the list into two cards.
                    cardRun[index] = -1;
                    if (runTop is { } started)
                    {
                        runs.Add((started, runBottom));
                        runTop = null;
                    }
                    continue;
                }
                runTop ??= top;
                cardRun[index] = runs.Count;
                if (heights[index] > 0)
                {
                    runBottom = bottom;
                }
            }
            if (runTop is { } last)
            {
                runs.Add((last, MathF.Max(last, runBottom)));
            }
            if (shown.Count == 0)
            {
                // **An empty list** (the person, 2026-10-11: "when a column is empty it should not
                // show up at all except for space, but if you are in edit mode it should actually
                // have some height to it and a text about placing widgets there"): at rest it is
                // its width of room and nothing else — no panel, no height. Arranged it is a slot of
                // its width (the person, 2026-10-12: "an empty column should not display a drag
                // widgets here text in edit mode. it should keep its width though"): no panel and
                // no words, its guide lines the whole way down. Nor any height (the person,
                // 2026-10-23: "when a column is empty the add and width button should be all the
                // way on the bottom or top of the column"; §297): its "+" and width button stand at
                // its end as a filled list's do, where its first widget would, level with a slot's;
                // a drop lands in it anywhere down its guide lines (OverColumns). A folder with no
                // widget at all shows a panel saying where they go at rest, or opening it would show
                // nothing — nor one whose every line is gone at rest (§327).
                if (!editing && first < 0)
                {
                    var room = CellsPx(EmptyCells);
                    emptyCells.Add((list, PopupPaint.Rect(LeftOf(list), 0, WidthOf(list), room)));
                    runs.Add((0, room));
                    return room;
                }
                return 0;
            }
            // A list is as tall as what is in it — exactly that, with no air of the folder's at
            // either end.
            var height = MathF.Max(line, PopupPaint.Round(flowing));
            // A list of no card — empty, or nothing but dividers, whatever they look like — is one card, the whole of it.
            if (shown.All(index => Divides(cells[index])))
            {
                runs.Clear();
                runs.Add((0, height));
            }
            return height;
        }
        // edit: a line carried off its place where it would land nowhere (§230), or come in and not
        // landed yet (§239), is in no list's flow: no gap, and its box — its own size, which its plate
        // is lifted at — past its list's far end, outside the list's panel, so drawn nowhere and found
        // nowhere. Its list closes up on the bar's side and is as tall as the lines left in it, as the
        // bar's blocks close up behind one.
        foreach (var index in away ?? [])
        {
            var list = listOf[index];
            cards[index] = PopupPaint.Rect(LeftOf(list), edge == Edge.Top ? contentHeights[list] : -heights[index], WidthOf(list), heights[index]);
            cardRun[index] = -1;
        }
        // §327: a line gone at rest has no box, at its list's top: drawn nowhere and found nowhere.
        foreach (var index in idle ?? [])
        {
            if (index >= 0 && index < cells.Count)
            {
                cards[index] = PopupPaint.Rect(LeftOf(listOf[index]), 0, WidthOf(listOf[index]), 0);
                cardRun[index] = -1;
            }
        }
        // Each list is as tall as the widgets it shows (2026-10-09: "folder height = automatic
        // only"), never more than the most; the box round the lists as tall as the tallest.
        var shownHeights = contentHeights.Select(content => MathF.Min(content, listMost)).ToArray();
        var tallest = shownHeights.Max();
        var width = reach;
        var height = tallest;
        // **The bar side is where they line up.** On a top bar the lists hang from it, their tops
        // level and their bottoms ragged; on a bottom bar their bottoms are level and their tops
        // ragged (the person, 2026-10-09). A side bar's columns stand beside it, each centred along
        // it on the folder's face (Over; the person, 2026-10-23: "when shown on the left or right
        // the columns from the middle folders should all be centered"). Nothing stands between them
        // and the bar: the title row went (the person, 2026-10-10: "remove the folder names completely").
        var fromTop = edge == Edge.Top;
        var lining = Over(edge);
        float TopOf(float tall) => PopupPaint.Round((height - tall) * lining);
        var listPlans = new FolderListPlan[lists];
        for (var list = 0; list < lists; list++)
        {
            var top = TopOf(shownHeights[list]);
            listPlans[list] = new FolderListPlan(PopupPaint.Rect(LeftOf(list), top, WidthOf(list), shownHeights[list]))
            {
                Runs = [.. runsOf[list].Select(run => PopupPaint.Rect(LeftOf(list), top + run.Top, WidthOf(list), MathF.Max(0, MathF.Min(run.Bottom, shownHeights[list]) - run.Top)))],
                Hidden = [.. hiddenOf[list].Where(index => !Divides(cells[index]))],
            };
        }
        // The lines down from their own list's top.
        for (var index = 0; index < cells.Count; index++)
        {
            cards[index] = Down(cards[index], listPlans[listOf[index]].Top);
        }
        var empties = emptyCells.Select(each => Down(each.Box, listPlans[each.List].Top)).ToList();
        // What is centred on the button: the lists from the first with widgets to the last, as tall
        // as the tallest of them, lined up as they are; the whole box where none has any.
        var core = PopupPaint.Rect(0, 0, width, height);
        if (first >= 0)
        {
            var coreTall = Enumerable.Range(first, last - first + 1).Max(list => shownHeights[list]);
            var coreLeft = MathF.Min(LeftOf(first), LeftOf(last));
            var coreRight = MathF.Max(LeftOf(first) + WidthOf(first), LeftOf(last) + WidthOf(last));
            core = PopupPaint.Rect(coreLeft, TopOf(coreTall), coreRight - coreLeft, coreTall);
        }
        // A divider is empty space (§505): nothing drawn at rest. edit: its whole room is a band of
        // moving stripes (§524, SpacerStripes), which would otherwise be nothing to see — a Fixed one's
        // fixed room, a Grow one's all it grows to; and it is taken hold of over a grab at least
        // DividerDip tall, centred on its room. Neither takes room: the lines stand where they stand at rest.
        var grabs = new D2D_RECT_F[cells.Count];
        var bands = new RectF[cells.Count];
        for (var index = 0; index < cells.Count; index++)
        {
            grabs[index] = cards[index];
            if (!Divides(cells[index]))
            {
                continue;
            }
            var card = cards[index];
            if (editing)
            {
                bands[index] = new RectF(card.left, card.top, card.right - card.left, card.bottom - card.top);
                var tall = MathF.Max(card.bottom - card.top, D(DividerDip));
                var top = PopupPaint.Round(((card.top + card.bottom) / 2) - (tall / 2));
                grabs[index] = PopupPaint.Rect(card.left, top, card.right - card.left, tall);
            }
        }
        var adds = new List<FolderAdd>();
        if (editing)
        {
            // edit: the columns' round buttons, outside the panels, never in a place of their own —
            // the lines stand where they stand at rest. At each list's own end away from the bar,
            // outside its panel, wherever that end is: over it on a bottom bar, under it on a top bar.
            //
            // The list's "+" (FolderAddKind.Add; the person, 2026-10-19: "add back the add button per
            // column"), its add list for this column; on its right the list's width
            // (FolderAddKind.Width), its menu the presets. No remove: a column stays (2026-10-12:
            // "deleting a column should not even be possible anymore"). Where the list has lines it
            // cannot show, then the warning (FolderAddKind.Hidden), as the bar's edit bar has its "not
            // shown" button (EditRail), the group centred on the list (2026-10-11: "the group should
            // be centered"), as a slot's is (WithSlots).
            var side = D(AddDip);
            var apart = D(AddApartDip);
            var step = side + D(PairApartDip);
            for (var list = 0; list < lists; list++)
            {
                var box = listPlans[list].Box;
                var y = fromTop ? box.bottom + apart : box.top - apart - side;
                FolderAddKind[] kinds = listPlans[list].Hidden.Count > 0
                    ? [FolderAddKind.Add, FolderAddKind.Width, FolderAddKind.Hidden]
                    : [FolderAddKind.Add, FolderAddKind.Width];
                var x = LeftOf(list) + PopupPaint.Round((WidthOf(list) - ButtonsWidth(scale, kinds.Length)) / 2);
                for (var at = 0; at < kinds.Length; at++)
                {
                    adds.Add(new FolderAdd(PopupPaint.Rect(x + (at * step), y, side, side), new FolderPlace(list, 0), kinds[at], list));
                }
            }
        }
        // The folder has no button of its own, at rest or arranged (2026-10-05: "remove the edit
        // button and close button from the folder views again"): the bar's edit mode is the way in,
        // and the folder's own button, Escape and edit mode ending are the ways out.
        return new FolderPlan
        {
            Editing = editing,
            View = FolderView.List,
            Width = width,
            Height = height,
            Core = core,
            Side = line,
            Pad = pad,
            // §649: attached, its lines flat on its ground: no corner rounded, a list's edge square as the bar's.
            // §704: Liquid glass's corners where its palette is worn, as the host rounds its pieces (FlyoutTheme.CornerDip).
            Radius = attached ? 0 : D(look.Liquid?.RadiusDip ?? 8),
            // Every line the folder draws — an empty list's, a guide line
            // — is the one hairline (SeparatorInk.Hairline), so two of them cannot land a pixel apart.
            Hairline = hairline,
            ListGap = listGap,
            Lists = listPlans,
            Cards = cards,
            CardRun = cardRun,
            Grabs = grabs,
            Bands = bands,
            CardList = listOf,
            Empty = empties,
            Adds = adds,
            TextPx = 14 * scale * textScale,
            SmallPx = 12 * scale * textScale,
        };
    }

    /// <summary>
    /// edit: <paramref name="plan"/> with its empty slots (<see cref="FolderSlot"/>) out to the edges
    /// of <paramref name="room"/> — where the window and the work area let them stand, in panel px —
    /// on either side of its lists: each a medium list's width, the lists' gap apart, as many as
    /// there is room for; one the edge cuts is there where its buttons fit with their room. Each
    /// slot's "+" and width button go after the plan's own (<see cref="FolderPlan.Adds"/>), centred on
    /// what shows of it, level with an empty list's: at the lists' side by the bar, where a first line would stand.
    /// Every list's own buttons are where they show in the room too, and the lists with widgets past
    /// its edge are <see cref="FolderPlan.OffScreen"/> (§320). Buttons the edit bar would cover
    /// (<paramref name="covered"/>, panel px; <see cref="Covered"/>) stand just past it, away from the
    /// screen's edge it stands at (§329).
    /// Nothing for a folder at rest. Pure: what the open folder, a test and a picture all ask.
    /// </summary>
    internal static FolderPlan WithSlots(FolderPlan plan, float scale, D2D_RECT_F room, Edge edge, D2D_RECT_F covered = default)
    {
        if (!plan.Editing)
        {
            return plan;
        }
        var width = ListWidth(scale);
        var gap = plan.ListGap;
        var side = PopupPaint.Round(AddDip * scale);
        var apart = PopupPaint.Round(AddApartDip * scale);
        var step = side + PopupPaint.Round(PairApartDip * scale);
        // An empty column's buttons and a slot's: its "+" and its width, at its end as a list's are —
        // an empty one's end is the lists' side by the bar, as it has no height (§297) — lined up as
        // the lists are (Over): beside a side bar their middle.
        var pair = ButtonsWidth(scale, 2);
        var emptyAt = PopupPaint.Round(plan.Height * Over(edge));
        var y = edge == Edge.Top ? emptyAt + apart : emptyAt - apart - side;
        // A row of buttons wide px from left at top, past the edit bar where it would stand under it
        // (§329): under one at the top, over one at the bottom.
        float Uncovered(float left, float wide, float top) =>
            covered.right > covered.left && left < covered.right && left + wide > covered.left && top < covered.bottom && top + side > covered.top
                ? (covered.top + covered.bottom) / 2 < (room.top + room.bottom) / 2 ? covered.bottom + apart : covered.top - apart - side
                : top;
        var slots = new List<FolderSlot>();
        var adds = plan.Adds.Where(add => add.Place is not { New: true }).ToList();
        var lists = plan.Lists.Count;
        // **Every list's buttons where they show** (§320), one rule for a list with widgets and an
        // empty one: centred on what shows of it inside the room, as a slot's are, and none — no
        // box, drawn nowhere and found nowhere — where that cannot hold them, rather than piled at the
        // screen's edge. A list may run past the screen's edge: no width is refused for that, an
        // empty list outside the ones with widgets hangs off their side (Core), and one as tall as
        // the room has no room past its far end, so there its buttons stand just inside that end,
        // over its first line (§303's side bars, and every bar since Reach keeps no row for them).
        var offScreen = new List<int>();
        for (var list = 0; list < lists; list++)
        {
            var box = plan.Lists[list].Box;
            var own = Enumerable.Range(0, adds.Count).Where(index => adds[index].List == list).ToList();
            var (from, to) = (MathF.Max(box.left, room.left), MathF.Min(box.right, room.right));
            var group = ButtonsWidth(scale, own.Count);
            var start = PopupPaint.Round(((from + to) / 2) - (group / 2));
            for (var at = 0; at < own.Count; at++)
            {
                var add = adds[own[at]];
                var top = add.Box.top;
                if ((top < room.top || top + side > room.bottom) && box.bottom > box.top)
                {
                    top = top >= box.bottom ? box.bottom - apart - side : box.top + apart;
                }
                top = Uncovered(start, group, top);
                adds[own[at]] = add with
                {
                    Box = to - from < group + (2 * apart)
                        ? PopupPaint.Rect(add.Box.left, add.Box.top, 0, 0)
                        : PopupPaint.Rect(start + (at * step), top, side, side),
                };
            }
            // A list with widgets past the screen's edge, by half a pixel or more.
            if (plan.Lists[list].Runs.Count > 0 && box.right > box.left
                && (box.left < room.left - 0.5f || box.right > room.right + 0.5f || box.top < room.top - 0.5f || box.bottom > room.bottom + 0.5f))
            {
                offScreen.Add(list);
            }
        }
        // Nearest the screen first: the one cut, before the ones past it.
        offScreen = [.. offScreen.OrderBy(list => Past(plan.Lists[list].Box, room))];
        // A slot is there while what shows of it holds its buttons with the room either side; a
        // sanity bound past any screen.
        bool Put(float left, FolderPlace place)
        {
            var (from, to) = (MathF.Max(left, room.left), MathF.Min(left + width, room.right));
            if (to - from < pair + (2 * apart) || slots.Count > 64)
            {
                return false;
            }
            slots.Add(new FolderSlot(new D2D_RECT_F { left = left, top = room.top, right = left + width, bottom = room.bottom }, place));
            var x = PopupPaint.Round(((from + to) / 2) - (pair / 2));
            var at = Uncovered(x, pair, y);
            adds.Add(new FolderAdd(PopupPaint.Rect(x, at, side, side), place, FolderAddKind.Add, -1));
            adds.Add(new FolderAdd(PopupPaint.Rect(x + step, at, side, side), place, FolderAddKind.Width, -1));
            return true;
        }
        // Before the first list on its side (FirstOnRight), after the last on the other.
        var (before, after) = FirstOnRight(edge) ? (lists, 0) : (0, lists);
        for (var apartFrom = 0; Put(-gap - width - (apartFrom * (width + gap)), FolderPlace.Slot(before, apartFrom)); apartFrom++)
        {
        }
        for (var apartFrom = 0; Put(plan.Width + gap + (apartFrom * (width + gap)), FolderPlace.Slot(after, apartFrom)); apartFrom++)
        {
        }
        return plan with { Slots = slots, Adds = adds, OffScreen = offScreen };
    }

    /// <summary>How far <paramref name="box"/> starts past <paramref name="room"/>'s nearest edge, in px: less than nothing where some of it is inside.</summary>
    private static float Past(D2D_RECT_F box, D2D_RECT_F room) =>
        MathF.Max(MathF.Max(box.left - room.right, room.left - box.right), MathF.Max(box.top - room.bottom, room.top - box.bottom));

    /// <summary>edit: the room kept between the off-screen warning and the screen's edge it stands against, in DIPs: a flyout's from a work area's end.</summary>
    internal const float WarningMarginDip = 12;

    /// <summary>
    /// edit: where the arranged folder's warning that part of it is off screen stands (§320), a
    /// <paramref name="size"/> px chip, in panel px: on the first list of <see cref="FolderPlan.OffScreen"/>,
    /// at its middle, against the edge of <paramref name="room"/> it runs past, <see cref="WarningMarginDip"/>
    /// in, and kept inside the room. Nothing where no list is off screen. Pure, as <see cref="WithSlots"/> is.
    /// </summary>
    internal static D2D_RECT_F WarningBox(FolderPlan plan, D2D_RECT_F room, Vector2 size, float scale)
    {
        if (plan.OffScreen.Count == 0 || plan.OffScreen[0] is var first && (first < 0 || first >= plan.Lists.Count))
        {
            return default;
        }
        var box = plan.Lists[plan.OffScreen[0]].Box;
        var margin = PopupPaint.Round(WarningMarginDip * scale);
        // Across: against the edge it runs past; along it, on the middle of what shows of it.
        float Place(float low, float high, float from, float to, float length) =>
            to > high + 0.5f ? high - margin - length
            : from < low - 0.5f ? low + margin
            : PopupPaint.Round(((MathF.Max(from, low) + MathF.Min(to, high)) / 2) - (length / 2));
        var at = PopupPaint.Rect(
            Place(room.left, room.right, box.left, box.right, size.X),
            Place(room.top, room.bottom, box.top, box.bottom, size.Y),
            size.X,
            size.Y);
        return KeepIn(at, room);
    }

    /// <summary>
    /// edit: the guide lines of an arranged folder (the person, 2026-10-12: "lines going from the top
    /// to the bottom of the screen which mark the left and right edge of the folders"): a hairline
    /// down <paramref name="room"/> (panel px) at each list's left and right edge, just inside it as
    /// the panel's rim is, but over the list's own panel, which marks its edges itself; and at each
    /// slot's, where it stands inside the room. Pure, as <see cref="WithSlots"/> is.
    /// </summary>
    internal static List<D2D_RECT_F> Guides(FolderPlan plan, D2D_RECT_F room)
    {
        var hair = plan.Hairline;
        var lines = new List<D2D_RECT_F>();
        void Line(float x, float top, float bottom)
        {
            if (x < room.left || x + hair > room.right)
            {
                return;
            }
            var (from, to) = (MathF.Max(top, room.top), MathF.Min(bottom, room.bottom));
            if (to > from)
            {
                lines.Add(new D2D_RECT_F { left = x, top = from, right = x + hair, bottom = to });
            }
        }
        void Edges(D2D_RECT_F box, float skipTop, float skipBottom)
        {
            foreach (var x in new[] { box.left, box.right - hair })
            {
                Line(x, room.top, skipTop);
                Line(x, skipBottom, room.bottom);
            }
        }
        for (var index = 0; index < plan.Lists.Count; index++)
        {
            var list = plan.Lists[index];
            // An empty list has no panel to keep off: its edges the whole way down, as a slot's —
            // where it is there: not where too little of it shows to hold its buttons (WithSlots),
            // past a screen's edge or under a side bar (§303).
            if (list.Runs.Count == 0)
            {
                if (plan.Adds.Any(add => add.List == index && add.Box.right > add.Box.left))
                {
                    Edges(list.Box, room.bottom, room.bottom);
                }
            }
            else
            {
                Edges(list.Box, list.Box.top, list.Box.bottom);
            }
        }
        foreach (var slot in plan.Slots)
        {
            Edges(slot.Box, room.bottom, room.bottom);
        }
        return lines;
    }

    /// <summary>Where the pointer is, in screen px, from a mouse message's lParam (client px of this window).</summary>
    private POINT Screen(LPARAM lParam)
    {
        var at = new POINT { x = (short)((nint)lParam & 0xFFFF), y = (short)(((nint)lParam >> 16) & 0xFFFF) };
        _ = ClientToScreen(Hwnd, &at);
        return at;
    }

    /// <summary>edit: whether <paramref name="at"/> (screen px) is past the system's drag threshold from the press.</summary>
    private bool Past(POINT at) =>
        !EditClicks.IsClick(at.x - dragFrom.x, at.y - dragFrom.y, GetSystemMetrics(SM.SM_CXDRAG), GetSystemMetrics(SM.SM_CYDRAG));

    /// <summary>
    /// pins: whether the pressed widget is being dragged out of the folder — the pointer has left the
    /// system's drag threshold since the press. The card it came from goes faint while it is.
    /// </summary>
    private bool Dragged(POINT at)
    {
        if (dragging >= 0)
        {
            return true;
        }
        if (Math.Abs(at.x - dragFrom.x) <= GetSystemMetrics(SM.SM_CXDRAG) && Math.Abs(at.y - dragFrom.y) <= GetSystemMetrics(SM.SM_CYDRAG))
        {
            return false;
        }
        dragging = pressed;
        SetHot(NoTarget);
        ShowStates();
        return true;
    }

    /// <summary>
    /// edit: asked when a card pressed in this folder at rest is carried past the drag threshold —
    /// the dock turns edit mode on for the folder's bar, as a widget dragged on the bar does
    /// (Dock.BeginEditFromFolderDrag), and answers true when that bar is arranged now. Null, or
    /// false, and the drag is what it was before: the widget is carried out of the folder.
    /// </summary>
    internal Func<bool>? ArrangeFromDrag { get; set; }

    /// <summary>
    /// §663: whether the bars are locked ("Lock the taskbar"): then a line pressed at rest and dragged is neither carried
    /// into edit mode (<see cref="ArrangeFromDrag"/>) nor out of the folder. Null: not locked.
    /// </summary>
    internal Func<bool>? Locked { get; set; }

    /// <summary>Tests only: whether the folder is arranged, as its bar is in edit mode.</summary>
    internal bool Arranged => IsOpen && editing;

    /// <summary>
    /// edit: a card pressed at rest has been carried past the drag threshold, the pointer at
    /// <paramref name="lParam"/>: edit mode turns on, and the drag goes on as edit mode's — the
    /// folder arranged where it stands and still open, the same card carried, its landing shown
    /// where the pointer is, and the release places it (the person, 2026-10-08: dragging a widget
    /// into edit mode keeps the drag). True when edit mode took it; false, with nothing changed,
    /// where the dock said no.
    /// <para>
    /// At the threshold, not at the press, for the bar's reason: a press that turns out to be a
    /// click would otherwise flash the whole edit dress. And the drag is <em>adopted</em>, not
    /// begun again — the person is already holding the card. It was let go for a while
    /// (2026-10-07), when edit mode shrank the screen under the pointer; edit mode is edited in
    /// place now, so nothing shifts.
    /// </para>
    /// <para>
    /// The folder holds itself open <b>before</b> the dock is asked: edit mode gives the bar the
    /// keys, and a folder at rest closes when another window takes the focus. The carried card is
    /// kept by what it is, not where it stood, as <see cref="FolderChanged"/> keeps it: the rebuild
    /// edit mode brings may lay the folder out again under the press.
    /// </para>
    /// </summary>
    private bool HandOver(LPARAM lParam)
    {
        if (ArrangeFromDrag is not { } ask)
        {
            return false;
        }
        var widget = tiles[pressed].Island;
        host.StaysOpen = true;
        handingOver = true;
        bool arranged;
        try
        {
            arranged = ask();
        }
        finally
        {
            handingOver = false;
        }
        if (!arranged)
        {
            if (IsOpen)
            {
                host.StaysOpen = editing;
            }
            return false;
        }
        if (!IsOpen)
        {
            // Closed meanwhile: there is nothing left to carry.
            return true;
        }
        pressed = IndexOf(widget);
        moving = pressed;
        landing = null;
        dragging = NoTarget;
        // Past the threshold already — that is why edit mode is on — so the release is a move,
        // never a click that would open the widget's page.
        travelled = true;
        // Moving is set first: the folder is held from here on, so the new size settles no pair under the pointer.
        Arrange();
        if (pressed == NoTarget)
        {
            // The widget went with the rebuild: the press is over.
            if (GetCapture() == Hwnd)
            {
                _ = ReleaseCapture();
            }
            ShowStates();
            return true;
        }
        if (GetCapture() != Hwnd)
        {
            _ = SetCapture(Hwnd);
        }
        _ = host.ToPanel(lParam, out var over);
        Follow(over);
        _ = Aim(over);
        return true;
    }

    /// <summary>
    /// edit: the footer's Edit (<see cref="IslandView.Arrange"/>, Windows' pencil): the dock turns edit
    /// mode on for the folder's bar, as a line dragged at rest does (<see cref="ArrangeFromDrag"/>), and
    /// the folder is arranged where it stands. The folder holds itself open first: edit mode takes the keys.
    /// </summary>
    private void ArrangeAsked()
    {
        if (ArrangeFromDrag is not { } ask || editing)
        {
            return;
        }
        host.StaysOpen = true;
        handingOver = true;
        bool arranged;
        try
        {
            arranged = ask();
        }
        finally
        {
            handingOver = false;
        }
        if (!IsOpen)
        {
            return;
        }
        if (!arranged)
        {
            host.StaysOpen = editing;
            return;
        }
        pressed = NoTarget;
        Arrange();
        ShowStates();
    }

    /// <summary>
    /// edit: the bar this folder stands on is being arranged now, so the folder is too, where it
    /// stands and without closing — in edit mode's dress (its guide lines and its "+" buttons, the
    /// lines where they were), holding itself open as a folder opened while arranged does
    /// (<see cref="Show"/>). Nothing for a folder already arranged.
    /// </summary>
    internal void Arrange()
    {
        if (!IsOpen || folder is not { } island || gfx is null || editing)
        {
            return;
        }
        // A page goes back to the folder's lines: what is arranged is the folder.
        if (island.ClosePage(tell: false))
        {
            tiles = island.Tiles;
            pageOf = null;
            pressed = hot = focused = NoTarget;
        }
        editing = true;
        plain = false;
        // §446: arranged it goes by a fade where it stands, as one opened arranged: its guide lines reach the display's edges.
        if (host.Moves is { ShiftDip: > 0 } moves)
        {
            host.Moves = moves with { ShiftDip = 0 };
        }
        host.StaysOpen = !OneFolder.Closes(FolderCloser.PressElsewhere, arranging: true);
        // Room round the panel for the buttons outside it, the window to the display's edges; the panel stays where it is.
        host.OutsideDip = OutsideRoomDip;
        host.Spread = display;
        // While arranging the pointer is the drag's: the widget it rested on hears it has gone.
        HoverOver(NoTarget, null);
        TellArranged(true);
        LayOut(island, Natural(island));
    }

    /// <summary>
    /// edit, §348: <see cref="Arrange"/> undone — edit mode ended by the edit bar's cross and the folder
    /// stays open (OneFolder.Rests): where it stands, without closing, at rest as a folder opened at rest
    /// is — no guide lines, no "+" buttons, nothing selected or on the clipboard, a line carried put back;
    /// its window the panel's again, topmost over the bars, with the keys; a press elsewhere closes it.
    /// Nothing for a folder not arranged.
    /// </summary>
    internal void Rest()
    {
        if (!IsOpen || folder is not { } island || gfx is null || !editing)
        {
            return;
        }
        _ = CancelMove();
        editing = false;
        plain = false;
        pressed = hot = focused = sizing = NoTarget;
        picked = null;
        alsoPicked = [];
        copied = [];
        covered = default;
        host.StaysOpen = !OneFolder.Closes(FolderCloser.PressElsewhere, arranging: false);
        host.OutsideDip = 0;
        host.Spread = null;
        TellArranged(false);
        LayOut(island, Natural(island));
        ShowStates();
        host.Front();
        host.Focus();
    }

    /// <summary>
    /// §612: the arranged folder in edit mode's dress (<paramref name="worn"/>) or plain, as at rest — the Appearance's preview
    /// (EditDress.Preview) —, where it stands, without closing, nothing of edit mode's state let go of: its window the
    /// display's (for its guide lines and its "+") or the panel's, its lines gliding from where they showed to where they
    /// stand now, what comes in fading in (<see cref="Settle"/>). Nothing for a folder not arranged, or dressed so already.
    /// </summary>
    internal void Dress(bool worn)
    {
        if (!IsOpen || folder is not { } island || gfx is null || !editing || plain != worn)
        {
            return;
        }
        var before = Take();
        // A line carried put back: the preview carries nothing.
        _ = CancelMove();
        plain = !worn;
        host.OutsideDip = worn ? OutsideRoomDip : 0;
        host.Spread = worn ? display : null;
        HoverOver(NoTarget, null);
        TellArranged(worn);
        LayOut(island, Natural(island));
        Settle(before);
        ShowStates();
    }

    /// <summary>Tests only (§612): whether the folder wears edit mode's dress: arranged, and not in the Appearance's preview.</summary>
    internal bool DressWorn => IsOpen && Dressed;

    /// <summary>edit: every line hears whether the folder is arranged (<see cref="CellIsland.Arranged"/>).</summary>
    private void TellArranged(bool arranged)
    {
        foreach (var tile in tiles)
        {
            if (tile.Island is CellIsland cells)
            {
                cells.Arranged = arranged;
            }
        }
    }

    /// <summary>
    /// The air at a list row's leading and trailing edge, in px. Every row keeps it, whatever
    /// draws the row — a pin, a kind's glyph, or a component drawing its own card (ListForm.Gutter
    /// asks for this same number).
    /// </summary>
    internal static float RowInset(float textPx) => PopupPaint.Round(textPx * 12f / 14f);

    /// <summary>
    /// The width of a list row's leading icon column, in px — <b>one column, whatever stands in
    /// it</b>: a pin's own icon, a kind's glyph centred in it, an app's logo.
    /// <para>
    /// It exists because the names have to line up. A pin's icon was 20 and a glyph 16, each
    /// followed by the same inset, so a pin's name began 4 px further in than everything else's,
    /// and an app list built on its own numbers began 14 further in than either. Nobody wrote
    /// those differences down; they are what three places arriving at a layout separately produces,
    /// and the user reads them off a screenshot straight away.
    /// </para>
    /// </summary>
    internal static float RowIcon(float textPx) => PopupPaint.Round(textPx * 20f / 14f);

    /// <summary>Where a list row's text begins, in px from the row's leading edge.</summary>
    internal static float RowText(float textPx) => RowInset(textPx) + RowIcon(textPx) + RowInset(textPx);

    /// <summary>
    /// A widget of a folder shown as a list: its icon — a pin's own, else its kind's glyph — and its
    /// name, always shown, on one line in <paramref name="box"/> (px of its surface). A pin that has
    /// windows open says so with the indicator at the line's start, as the bar does under its icon,
    /// and its app's badge stands on its icon's corner, as on the bar.
    /// </summary>
    internal static void PaintListLine(ID2D1DeviceContext* dc, Gfx gfx, IIsland widget, CellItem island, D2D_RECT_F box, FolderPlan plan, FlyoutTheme theme, float scale)
    {
        var inset = RowInset(plan.TextPx);
        // One column for whatever leads the row, so every name begins in the same place.
        var column = RowIcon(plan.TextPx);
        var left = box.left + RowText(plan.TextPx);
        var middle = (box.top + box.bottom) / 2;
        var name = widget is PinTile pin ? pin.Item.Name : IslandEdits.Header(island);
        if (widget is PinTile drawn)
        {
            var icon = PopupPaint.Rect(box.left + inset, middle - column / 2, column, column);
            drawn.Icon(dc, icon, theme.Text, theme.TextSecondary, scale, PopupPaint.Round(plan.TextPx * 16f / 14f));
            // Its app's badge, or its window's overlay, on the icon's corner as on the bar.
            drawn.Signal(dc, icon, SignalPaint.BadgeInk.Of(theme));
            // The running indicator stands at the line's start, as Windows marks a chosen row.
            drawn.PaintPill(dc, box.left + inset / 2, middle + PopupPaint.Round(8 * scale), PopupPaint.Round(3 * scale), PopupPaint.Round(16 * scale), theme);
            var space = MathF.Max(1, box.right - inset - left);
            var measured = PopupPaint.Measure(gfx, name, plan.TextPx, Weight.Regular, space);
            PopupPaint.Text(dc, gfx, name, plan.TextPx, Weight.Regular, drawn.Item.Broken is null ? theme.Text : theme.TextTertiary, left, middle - measured.Y / 2, space);
            return;
        }
        // The glyph is smaller than the column and stands in the middle of it, so a row of glyphs
        // and a row of app icons keep one edge. A shortcut whose app is not installed is faint with
        // the warning badge, and its name in the faint ink, as a broken pin's line.
        var glyph = PopupPaint.Round(plan.TextPx * 16f / 14f);
        // A power command this PC's power menu does not offer is drawn the same way (PowerButtonTile).
        var missing = widget is ShortcutTile { Missing: not null } or PowerButtonTile { Missing: not null };
        if (missing)
        {
            ShortcutTile.Glyph(dc, gfx, widget.Info.Glyph, missing, theme.Text, theme.TextSecondary,
                PopupPaint.Rect(box.left + inset + ((column - glyph) / 2), middle - (glyph / 2), glyph, glyph));
        }
        else
        {
            PopupPaint.Glyph(dc, gfx, widget.Info.Glyph, glyph, theme.Text, PopupPaint.Rect(box.left + inset, middle - column / 2, column, column));
        }
        var room = MathF.Max(1, box.right - inset - left);
        var size = PopupPaint.Measure(gfx, name, plan.TextPx, Weight.Regular, room);
        PopupPaint.Text(dc, gfx, name, plan.TextPx, Weight.Regular, missing ? theme.TextTertiary : theme.Text, left, middle - size.Y / 2, room);
    }

    /// <summary>
    /// The widget's content on its line (<paramref name="box"/>, px of the surface): its own
    /// drawing, or its bar cells centred.
    /// </summary>
    internal static void PaintContent(ID2D1DeviceContext* dc, IIsland island, D2D_RECT_F box, in TileStyle style)
    {
        try
        {
            // One way in, with the surface as an argument.
            if (island is CellIsland cells)
            {
                // Clipped to its own line: a widget that does not fit its line may not scribble on
                // the lines around it.
                dc->PushAxisAlignedClip(&box, D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_ALIASED);
                try
                {
                    cells.PaintOn(dc, box, style, WidgetSurface.List);
                }
                finally
                {
                    dc->PopAxisAlignedClip();
                }
            }
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            // One widget that can't draw leaves the others.
            Console.Error.WriteLine($"Karpo: a folder's {island.Info.Id} not drawn: {error.Message}");
        }
    }


    /// <summary>
    /// edit: whether a press elsewhere leaves this folder open. True while its bar is being
    /// arranged, and while a widget's menu stands over it (<see cref="MenuClosed"/>): the dock's
    /// own press handling reads it (Dock.PressedOn), and so do the messages the host answers for
    /// itself (an outside press, another window taking the focus, WM_CANCELMODE).
    /// </summary>
    internal bool StaysOpen => host.StaysOpen;

    /// <summary>
    /// The menu a widget's right click opened over the folder has ended, whichever way it ended:
    /// the folder goes back to closing for a press elsewhere, or, while its bar is arranged, to
    /// staying open as it does then. Not the folder's own to decide — the dock knows when the
    /// menu went.
    /// </summary>
    internal void MenuClosed()
    {
        host.StaysOpen = editing;
        // edit: a list's width menu, or its warning's, was the menu: its button goes back to rest.
        if (sizing != NoTarget)
        {
            sizing = NoTarget;
            ShowStates();
        }
    }

    /// <summary>
    /// edit: a menu stands over the folder (a widget's from a list's warning, Dock.Folders.cs): the folder holds
    /// itself open as it does for a widget's menu, until the dock says the menu has ended (<see cref="MenuClosed"/>).
    /// </summary>
    internal void HoldOpen() => host.StaysOpen = true;

    /// <summary>Whether <paramref name="screen"/> is on the open panel.</summary>
    internal bool Contains(POINT screen) => host.Contains(screen);

    /// <summary>
    /// Where line <paramref name="index"/> is on the screen, as wide as the folder, held to what its list shows;
    /// the folder where the line is not shown. For tests and pictures.
    /// </summary>
    internal RECT LineOnScreen(int index)
    {
        var on = host.PanelOnScreen;
        if (plan is not { } shown || index < 0 || index >= shown.Cards.Count)
        {
            return on;
        }
        var panel = ListOnScreen(shown, shown.CardList[index]);
        var box = shown.Cards[index];
        // In the panel's px, kept to what its list shows.
        var top = Math.Clamp((int)MathF.Round(on.top + box.top), panel.top, panel.bottom);
        var bottom = Math.Clamp((int)MathF.Round(on.top + box.bottom), top, panel.bottom);
        return new RECT { left = on.left, top = top, right = on.right, bottom = bottom };
    }

    /// <summary>List <paramref name="list"/>'s own panel on the screen.</summary>
    private RECT ListOnScreen(FolderPlan shown, int list)
    {
        var on = host.PanelOnScreen;
        var box = shown.Lists[Math.Clamp(list, 0, shown.Lists.Count - 1)].Box;
        return new RECT
        {
            left = on.left + (int)box.left,
            top = on.top + (int)box.top,
            right = on.left + (int)box.right,
            bottom = on.top + (int)box.bottom,
        };
    }

    /// <summary>
    /// Tests only: the card or line under a mouse message's point, and the widget's own target there, as a click
    /// there asks it (<see cref="PartOf"/>); (-1, 0) for none.
    /// </summary>
    internal (int Line, int Part) PartAt(LPARAM lParam)
    {
        if (!host.ToPanel(lParam, out var point) || TargetAt(point) is not (>= 0 and var line)
            || plan is not { } shown || line >= tiles.Count || line >= shown.Cards.Count)
        {
            return (NoTarget, 0);
        }
        return (line, PartOf(shown, line, point));
    }

    /// <summary>The widget of card <paramref name="target"/>'s own target at <paramref name="point"/> (panel px); 0 where it has none.</summary>
    private int PartOf(FolderPlan shown, int target, Vector2 point)
    {
        if (!DrawsOwnCard(target) || tiles[target].Island is not CellIsland cells)
        {
            return 0;
        }
        var (box, local, style) = CardSpace(shown, target, point);
        // The same surface the widget was drawn on, so what a click lands on is what is there.
        return cells.TargetOn(local, box, style, WidgetSurface.List);
    }

    /// <summary>The window in front when the folder opened, which closing it gives the focus back to.</summary>
    internal HWND Previous => host.Previous;

    /// <summary>The open panel on the screen, for tests and pictures.</summary>
    internal RECT PanelOnScreen => host.PanelOnScreen;

    /// <summary>
    /// §593: what of the folder is there to see and press, on the screen: its lists and their buttons (the "+" a column adds with
    /// among them), the box round them all, a line's room more at either end for a line coming in (the add page's ghost); empty
    /// while it shows none. What the add page keeps clear of while it adds into it.
    /// </summary>
    internal RECT ContentOnScreen
    {
        get
        {
            if (!IsOpen || plan is not { } shown)
            {
                return default;
            }
            List<D2D_RECT_F> boxes = [.. shown.Lists.Select(list => list.Box), .. Enumerable.Range(0, shown.Adds.Count).Select(add => AddBox(shown, add))];
            boxes.RemoveAll(box => box.right <= box.left || box.bottom <= box.top);
            if (boxes.Count == 0)
            {
                return default;
            }
            var on = host.PanelOnScreen;
            var line = (int)MathF.Ceiling(shown.Side);
            return new RECT
            {
                left = on.left + (int)MathF.Floor(boxes.Min(box => box.left)),
                top = on.top + (int)MathF.Floor(boxes.Min(box => box.top)) - line,
                right = on.left + (int)MathF.Ceiling(boxes.Max(box => box.right)),
                bottom = on.top + (int)MathF.Ceiling(boxes.Max(box => box.bottom)) + line,
            };
        }
    }

    /// <summary>Tests only: the guide lines shown now, on the screen (<see cref="PlaceGuides"/>).</summary>
    internal List<RECT> GuidesOnScreen
    {
        get
        {
            var on = host.PanelOnScreen;
            return [.. guideVisuals.Where(line => line.IsVisible).Select(line => new RECT
            {
                left = on.left + (int)MathF.Round(line.Offset.X),
                top = on.top + (int)MathF.Round(line.Offset.Y),
                right = on.left + (int)MathF.Round(line.Offset.X + line.Size.X),
                bottom = on.top + (int)MathF.Round(line.Offset.Y + line.Size.Y),
            })];
        }
    }

    /// <summary>
    /// What lies under the lines (panel px): on an empty list's panel, its words, centred
    /// in the secondary text — "Add widgets here" (<see cref="FolderPlan.Empty"/>). Nothing between
    /// two lists — each is a panel of its own — and no hairlines of a list's own between its widgets:
    /// what parts two of them is a divider somebody put there, a gap between two cards (DividerTile).
    /// </summary>
    internal static void PaintEmpty(ID2D1DeviceContext* dc, Gfx gfx, FolderPlan plan, FlyoutTheme theme)
    {
        foreach (var cell in plan.Empty)
        {
            PopupPaint.Centred(dc, gfx, S.DockFolderColumnEmpty, plan.TextPx, Weight.Regular, theme.TextSecondary, cell, ink: false);
        }
    }

    /// <summary>edit: how a "+" button stands (<see cref="PaintAdd"/>).</summary>
    internal enum AddLook
    {
        /// <summary>Not drawn yet.</summary>
        None,

        Rest,
        Hover,

        /// <summary>Pressed, or the add list is open for it.</summary>
        Pressed,
    }

    /// <summary>
    /// edit: one round button of a column in <paramref name="box"/> (px of its surface): a list's
    /// "+" (<see cref="FolderAddKind.Add"/>) and width (<see cref="FolderAddKind.Width"/>), and a
    /// slot's, Windows' round accent button — the accent filling the circle, lighter under the pointer
    /// and darker pressed or while its menu or add list is open — wearing the Add glyph, or the resize
    /// glyph across, in the text the accent takes. A list's warning
    /// (<see cref="FolderAddKind.Hidden"/>) is the same round button in the edit bar's warning orange
    /// (EditRail.Warning), <paramref name="mark"/> on it in its ink.
    /// </summary>
    internal static void PaintAdd(ID2D1DeviceContext* dc, Gfx gfx, D2D_RECT_F box, FlyoutTheme theme, AddLook look, FolderAddKind kind = FolderAddKind.Width, string? mark = null)
    {
        var side = MathF.Min(box.right - box.left, box.bottom - box.top);
        var centre = new Vector2((box.left + box.right) / 2, (box.top + box.bottom) / 2);
        if (kind == FolderAddKind.Hidden)
        {
            var warning = EditRail.Warning(theme);
            var fill = look switch
            {
                AddLook.Hover => warning.Hover,
                AddLook.Pressed => warning.Pressed,
                _ => warning.Fill,
            };
            PopupPaint.Circle(dc, centre, side / 2, FlyoutTheme.Ink(fill));
            PopupPaint.Glyph(dc, gfx, mark ?? EditRailPlan.Glyphs.NotShown, MathF.Max(8, PopupPaint.Round(side / 2)), warning.Glyph, box);
            return;
        }
        var accent = look switch
        {
            AddLook.Hover => theme.AccentHover,
            AddLook.Pressed => theme.AccentPressed,
            _ => theme.Accent,
        };
        PopupPaint.Circle(dc, centre, side / 2, FlyoutTheme.Ink(accent));
        if (kind == FolderAddKind.Add)
        {
            PopupPaint.Glyph(dc, gfx, AddGlyph, MathF.Max(8, PopupPaint.Round(side / 2)), theme.TextOnAccent, box);
            return;
        }
        // The upright resize glyph turned a quarter about the button's middle: ‹•› across. Its ink
        // is a smaller part of its em than the Add glyph's, so it is drawn larger to weigh as much.
        var glyph = MathF.Max(8, PopupPaint.Round(side * 0.7f));
        D2D_MATRIX_3X2_F was;
        dc->GetTransform(&was);
        var turned = Matrix3x2.CreateRotation(MathF.PI / 2, centre)
            * new Matrix3x2(was._11, was._12, was._21, was._22, was._31, was._32);
        var turn = default(D2D_MATRIX_3X2_F);
        turn._11 = turned.M11;
        turn._12 = turned.M12;
        turn._21 = turned.M21;
        turn._22 = turned.M22;
        turn._31 = turned.M31;
        turn._32 = turned.M32;
        dc->SetTransform(&turn);
        try
        {
            PopupPaint.Glyph(dc, gfx, WidthGlyph, glyph, theme.TextOnAccent, box);
        }
        finally
        {
            dc->SetTransform(&was);
        }
    }


    /// <summary>A line's style at <paramref name="scale"/>.</summary>
    internal static TileStyle StyleFor(float scale, Look look, FlyoutTheme theme) => new(scale, look, theme, 1, 1);

    // --- The showing ---------------------------------------------------------------

    /// <summary>
    /// Places every visual for the plan and draws every surface. Never inside itself: the cards'
    /// drawing asks <see cref="ShowStates"/> again, and a want that changed while drawing went
    /// round Draw, DrawCards and ShowStates until the stack ran out (a test host ended that way
    /// twice under load). Inside a draw, ShowStates only sets the states;
    /// the draw already under way is the one that paints.
    /// </summary>
    private void Draw()
    {
        var outer = !drawing;
        drawing = true;
        try
        {
            DrawNow();
        }
        finally
        {
            if (outer)
            {
                drawing = false;
            }
        }
    }

    private void DrawNow()
    {
        if (plan is not { } shown || gfx is null || empty is null)
        {
            return;
        }
        var drawer = gfx;
        var colours = theme;
        for (var list = 0; list < listViews.Count; list++)
        {
            listViews[list].Size = new Vector2(shown.Width, shown.Height);
            if (list < shown.Lists.Count && list < listShapes.Count)
            {
                var box = shown.Lists[list].Box;
                listShapes[list].Offset = new Vector2(box.left, box.top);
                listShapes[list].Size = new Vector2(box.right - box.left, box.bottom - box.top);
            }
        }
        foreach (var (view, _, _, _, _) in runViews)
        {
            view.Size = new Vector2(shown.Width, shown.Height);
        }
        empty.Size = new Vector2(shown.Width, shown.Height);
        // §357: the empty lists' words drawn again only where they, the folder's size or its look changed.
        var emptyFor = (shown.Width, shown.Height, shown.TextPx, string.Join(";", shown.Empty.Select(cell => $"{cell.left},{cell.top},{cell.right},{cell.bottom}")));
        if (!emptyFor.Equals(emptyDrawn))
        {
            emptySurface = Paint(emptySurface, shown.Width, shown.Height, dc => PaintEmpty(dc, drawer, shown, colours), emptyBrush!);
            emptyDrawn = emptyFor;
        }
        // §704: Liquid glass's lines that meet in a piece one glass shape, a hairline between two (LiquidGlass.Join).
        var seams = Seams(shown);
        for (var index = 0; index < cards.Count && index < shown.Cards.Count; index++)
        {
            var rect = shown.Cards[index];
            // §410: a rectangle, square; its section's clip rounds the section's corners. §624 no outline of its own.
            var visual = cards[index];
            visual.Size = new Vector2(rect.right - rect.left, rect.bottom - rect.top);
            if (index < frosts.Count)
            {
                // §464: its blur and its background its box on whole px, so two lines meet with no seam between them.
                // Whole px of the window: the views it stands in may stand on a half one.
                var shift = Placed(visual.Parent);
                var (from, to) = (Snap(new Vector2(rect.left, rect.top) + shift) - shift, Snap(new Vector2(rect.right, rect.bottom) + shift) - shift);
                var at = new Vector3(from - new Vector2(rect.left, rect.top), 0);
                // A sprite's leading edge is smoothed: where two lines meet, the lower one's first px would let a seam of what
                // is under them through. So each runs a px on under the line below it, which stands over that, and a line
                // with one above it starts its blur a px down, under its background, not over the line above's.
                var below = Vector2.UnitY;
                var above = shown.RunOf(index) is { } run && rect.top > run.top + 0.5f ? Vector2.UnitY : Vector2.Zero;
                (frosts[index].Offset, frosts[index].Size) = (at + new Vector3(above, 0), Vector2.Max(Vector2.Zero, to - from + below - above));
                if (index < fills.Count)
                {
                    (fills[index].Offset, fills[index].Size) = (at, Vector2.Max(Vector2.Zero, to - from + below));
                }
            }
            // §462: its pattern, the piece of its section's one sheet it stands on: the section's lines run on into each other.
            var section = shown.RunOf(index) ?? rect;
            if (index < contents.Count)
            {
                contents[index].Pattern.Show(drawer, LineColors(index).CardPattern, scale, visual.Size, new Vector2(rect.left - section.left, rect.top - section.top));
                // §649: attached, a divider a line of the folder's outline across its room.
                PlaceRule(contents[index], index, rect);
                PlaceSeam(contents[index], rect, index < seams.Count ? seams[index] : null);
            }
        }
        DrawCards();
        // §357: a button drawn again only where it changed (DrawAdds), a showing again too: it is kept.
        DrawAdds();
        PlaceGuides(shown);
        PlaceWarning(shown);
        PlaceLines();
        ShowStates();
    }

    /// <summary>
    /// edit: the guide lines' ink: the strong fill a scroll indicator is in, so a line shows on the
    /// dim and the desktop under it in both themes, at <see cref="GuideStrength"/> of it, so it marks
    /// and does not compete with the lists (the person, 2026-10-23: "make the column lines more
    /// faint"; it was 0.6). High contrast keeps the system's colour whole: a hairline is faint enough.
    /// </summary>
    internal static Color GuideInk(FlyoutTheme theme) =>
        theme.HighContrast ? theme.ScrollIndicator : theme.ScrollIndicator with { A = (byte)MathF.Round(theme.ScrollIndicator.A * GuideStrength) };

    /// <summary>edit: how much of the scroll indicator's ink the guide lines are drawn in (<see cref="GuideInk"/>).</summary>
    internal const float GuideStrength = 0.3f;

    /// <summary>
    /// edit: the guide lines where <paramref name="shown"/> has them (<see cref="Guides"/>); none at
    /// rest. Down the display (the person, 2026-10-19: "i want them to continue all the way through";
    /// §246) to its top, where no edit bar stands while a folder is open (§251), and to the bars'
    /// edges, no further (2026-10-22: "the column lines in edit mode still go over the taskbar";
    /// §287): what of the folder stands outside its panel stands off the bars (<see cref="Visible"/>),
    /// which stand in front of its window, so nothing of a line shows on a bar, through its material
    /// or between a floating one's panels.
    /// </summary>
    private void PlaceGuides(FolderPlan shown)
    {
        var lines = shown.Editing && host.Spread is not null ? Guides(shown, Visible()) : [];
        Try(() =>
        {
            while (guideVisuals.Count < lines.Count && guideInk is not null && guidesLayer is not null)
            {
                var line = Own(host.Compositor.CreateSpriteVisual());
                line.Brush = guideInk;
                guidesLayer.Children.InsertAtTop(line);
                guideVisuals.Add(line);
            }
            for (var index = 0; index < guideVisuals.Count; index++)
            {
                var line = guideVisuals[index];
                line.IsVisible = index < lines.Count;
                if (index < lines.Count)
                {
                    var box = lines[index];
                    line.Offset = new Vector3(box.left, box.top, 0);
                    line.Size = new Vector2(box.right - box.left, box.bottom - box.top);
                }
            }
        });
    }

    /// <summary>
    /// edit: each "+" in the look it should have now — pressed while the add list is open for it or
    /// the button is held, lit under the pointer, at rest otherwise — drawn again only where that, its
    /// glyph or its size changed (§357: kept through every layout and showing), and placed where it
    /// shows (<see cref="AddBox"/>).
    /// </summary>
    private void DrawAdds()
    {
        if (plan is not { } shown || gfx is null)
        {
            return;
        }
        for (var index = 0; index < addVisuals.Count && index < shown.Adds.Count; index++)
        {
            var target = AddTarget + index;
            var wanted = index == sizing || (pressed == target && hot == target) ? AddLook.Pressed
                : hot == target ? AddLook.Hover
                : AddLook.Rest;
            var button = addVisuals[index];
            var sprite = button.Sprite;
            var box = AddBox(shown, index);
            var kind = shown.Adds[index].Kind;
            var (width, height) = (box.right - box.left, box.bottom - box.top);
            var glyph = kind == FolderAddKind.Hidden ? HiddenGlyph(HiddenIn(shown, shown.Adds[index].List)) : null;
            var drawn = (wanted, kind, glyph, width, height);
            if (!drawn.Equals(button.Drawn))
            {
                var drawer = gfx;
                var colours = theme;
                try
                {
                    button.Surface = Paint(button.Surface, width, height, dc => PaintAdd(dc, drawer, PopupPaint.Rect(0, 0, width, height), colours, wanted, kind, glyph), button.Brush);
                    button.Drawn = drawn;
                    ButtonsDrawn++;
                }
                catch (Exception error) when (error is not OutOfMemoryException)
                {
                    Console.Error.WriteLine($"Karpo: a folder's column button not drawn: {error.Message}");
                }
            }
            Try(() =>
            {
                sprite.Size = new Vector2(width, height);
                // edit: a list's buttons go with it while lines give way (FolderFlyout.Drag.cs);
                // set only where that changed, so a glide is left alone.
                if (AddAt(index, box) is var at && sprite.Offset != at)
                {
                    sprite.Offset = at;
                }
            });
        }
    }

    /// <summary>Every line's content drawn again: the widgets' news, a tick, a new minute.</summary>
    private void DrawCards()
    {
        if (plan is not { } shown || gfx is null)
        {
            return;
        }
        for (var index = 0; index < contents.Count && index < tiles.Count && index < shown.Cards.Count; index++)
        {
            DrawCard(shown, index);
        }
        ShowStates();
    }

    /// <summary>
    /// One line drawn again: its widget's content, and in edit mode the dress over it. Split out of
    /// <see cref="DrawCards"/> so the pointer can redraw the one card it moved on or off
    /// (<see cref="HoverOver"/>) rather than the whole folder.
    /// </summary>
    private void DrawCard(FolderPlan shown, int index)
    {
        if (gfx is null || index < 0 || index >= contents.Count || index >= tiles.Count || index >= shown.Cards.Count)
        {
            return;
        }
        if (!shown.Shows(index))
        {
            // A widget a fixed height had no room for. It has no box, so there is nothing to
            // draw into and nothing for the pointer to find; the line at the foot says it is here.
            var blank = contents[index].Sprite;
            blank.Size = new Vector2(0, 0);
            blank.IsVisible = false;
            cards[index].IsVisible = false;
            return;
        }
        contents[index].Sprite.IsVisible = true;
        cards[index].IsVisible = true;
        SwapLine(index);
        {
            var rect = shown.Cards[index];
            var width = rect.right - rect.left;
            var height = rect.bottom - rect.top;
            var line = contents[index];
            var sprite = line.Sprite;
            var island = tiles[index].Island;
            var entry = tiles[index].Cell.Island;
            var style = LineStyle with { Screen = Screen() };
            var box = PopupPaint.Rect(0, 0, width, height);
            var drawer = gfx;
            var colours = theme;
            var turned = Mirror.On;
            // §424: a line with a part on the compositor (the month's weeks) leaves it to its strip, drawn after it.
            var sliding = island as IStripLine;
            LineStrip? strip = null;
            try
            {
                if (sliding is not null && island is CellIsland drawsCard && drawsCard.DrawsCard(box, style)
                    && ListWidgets.Find(entry.Kind) is { Line: ListLine.Card })
                {
                    strip = sliding.Strip(box, style);
                }
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                Console.Error.WriteLine($"Karpo: a folder's line laid out no strip: {error.Message}");
            }
            try
            {
                var barScale = scale;
                if (sliding is not null)
                {
                    sliding.Hosting = strip is not null;
                }
                // Its words cut short, heard as it paints, for its tooltip (CutWords).
                CompositionDrawingSurface? painted = null;
                cutRuns[island] = CutWords.While(() => painted = Paint(line.Surface, width, height, dc =>
                {
                    // Arabic and Urdu: the line mirrored, its words and pictures upright (Mirror.cs);
                    // the pointer is mirrored back into it (CardSpace).
                    var was = turned ? Mirror.Begin(dc, box.left, box.right) : default;
                    try
                    {
                        // A component draws its own card on its line; an ordinary widget draws the list's
                        // row, its icon and its name (Sections.CardInList). Which drawing a line takes is
                        // a question about the kind, not about how tall the line is.
                        if (ListWidgets.Find(entry.Kind) is not { Line: ListLine.Card })
                        {
                            // §507: its name in its own look's font, as its card would be.
                            using var typeface = drawer.Wearing(style.Look.Typeface);
                            PaintListLine(dc, drawer, island, entry, box, shown, colours, barScale);
                        }
                        else
                        {
                            PaintContent(dc, island, box, style);
                        }
                    }
                    finally
                    {
                        if (turned)
                        {
                            uprights[island] = Mirror.End(dc, was);
                        }
                        else
                        {
                            uprights.Remove(island);
                        }
                    }
                }, line.Brush));
                line.Surface = painted!;
                PlaceStrip(line, sliding, strip, width, style, turned);
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                Console.Error.WriteLine($"Karpo: a folder's line not drawn: {error.Message}");
            }
            finally
            {
                if (sliding is not null)
                {
                    sliding.Hosting = false;
                }
            }
            // edit: where it stands now, given way or lifted (FolderFlyout.Drag.cs); set only where
            // that changed, so a tick's draw leaves a line's glide alone.
            if (LineOffset(index, shown.Cards[index]) is var offset && sprite.Offset != offset)
            {
                sprite.Offset = offset;
            }
            sprite.Size = new Vector2(width, height);
        }
    }

    /// <summary>
    /// <paramref name="paint"/> into <paramref name="surface"/> when it has the size, else into a new one that
    /// <paramref name="brush"/> shows from now on; a replaced surface is closed at once. §357: the surface is
    /// its element's, which closes it as it goes.
    /// </summary>
    private CompositionDrawingSurface Paint(CompositionDrawingSurface? surface, float width, float height, Paint paint, CompositionSurfaceBrush brush)
    {
        if (surface is not null && surface.Size.Width == MathF.Max(width, 1) && surface.Size.Height == MathF.Max(height, 1))
        {
            gfx!.Redraw(surface, paint);
            return surface;
        }
        var fresh = gfx!.Draw(width, height, paint);
        surfacesDrawn++;
        brush.Surface = fresh;
        if (surface is not null)
        {
            try
            {
                surface.Dispose();
            }
            catch (Exception)
            {
            }
        }
        return fresh;
    }

    /// <summary>
    /// A card's clip, <paramref name="down"/> px lower: the card less the host's rim where the rim lies
    /// outside the material (<see cref="FlyoutHost.RimInset"/>), so a line's plate — the actions line's
    /// footer band — stops at the rim, as Windows' footer does, and the rim shows what is behind it.
    /// Under the <paramref name="rim"/>, the whole card: a graph runs to its edges, the rim over it.
    /// </summary>
    private (Vector2 Offset, Vector2 Size) CardClip(D2D_RECT_F card, float down, bool rim = false)
    {
        var inside = rim ? 0 : host.RimInset;
        return (new Vector2(card.left + inside, card.top + down + inside),
            Vector2.Max(new Vector2(card.right - card.left - (2 * inside), card.bottom - card.top - (2 * inside)), Vector2.Zero));
    }

    /// <summary>
    /// Each card, line and spacer's stripes where the plan has them, and the host's panels with them. No
    /// list scrolls: what does not fit is not shown (<see cref="FolderListPlan.Hidden"/>).
    /// </summary>
    private void PlaceLines()
    {
        if (plan is not { } shown)
        {
            return;
        }
        Try(() =>
        {
            // Each card, and the host's panels with them (Pieces).
            foreach (var (_, shape, list, run, rim) in runViews)
            {
                if (list < shown.Lists.Count && run < shown.Lists[list].Runs.Count)
                {
                    (shape.Offset, shape.Size) = CardClip(shown.Lists[list].Runs[run], 0, rim);
                }
            }
            host.SetPieces(shown.Pieces);
            // Set only where it changed, so a line gliding (a drag's give) is left alone.
            for (var index = 0; index < cards.Count && index < shown.Cards.Count; index++)
            {
                var rect = shown.Cards[index];
                var at = new Vector3(rect.left, rect.top, 0);
                if (cards[index].Offset != at)
                {
                    cards[index].Offset = at;
                }
                if (index < contents.Count && contents[index].Sprite.Offset != at)
                {
                    contents[index].Sprite.Offset = at;
                }
            }
            for (var index = 0; index < bands.Count && index < shown.Cards.Count; index++)
            {
                // §502: a lifted spacer's stripes stand on its carrier (Raise).
                if (RaisedOf(index) is not null)
                {
                    continue;
                }
                // Arranged, a spacer's room striped, as the plan has it (FolderPlan.Bands).
                var room = index < shown.Bands.Count ? shown.Bands[index] : default;
                var drawn = !room.IsEmpty && index != dragging;
                bands[index].Visual.IsVisible = drawn;
                if (drawn)
                {
                    bands[index].Visual.Offset = new Vector3(room.X, room.Y, 0);
                    bands[index].Lay(room.Size, scale);
                }
            }
        });
        // edit: lines giving way for a carried one, or lifted, stay where they are (FolderFlyout.Drag.cs).
        AfterPlaced();
    }

    /// <summary>What a line is filled with (<see cref="FillOf"/>): under the pointer, pressed, or nothing.</summary>
    internal enum CardFill
    {
        None,
        LineHover,
        LinePressed,
    }

    /// <summary>
    /// What fills a line: nothing at rest, and under the pointer the subtle fill alone — the fill
    /// Windows' lists and Karpo's own menus light a row with (backlog 21). A <paramref name="quiet"/>
    /// line — a heading — is not a thing to press and never lights. A <paramref name="still"/> line
    /// (<see cref="LightsWhole"/> false: its click does nothing, or its rows light themselves) keeps
    /// as it is at rest under the pointer and pressed: only the parts of it that act light, each on
    /// its own.
    /// </summary>
    internal static CardFill FillOf(bool hot, bool pressed, bool quiet, bool still = false)
    {
        if (still)
        {
            hot = pressed = false;
        }
        return quiet ? CardFill.None : pressed ? CardFill.LinePressed : hot ? CardFill.LineHover : CardFill.None;
    }

    /// <summary>
    /// Whether <paramref name="cell"/>'s line, drawn by <paramref name="island"/>, lights as a whole
    /// under the pointer outside arranging: only where the whole of it is the one thing a press does
    /// (2026-10-11: "the widgets in the list view should not have a hover over animation, but all the
    /// logic subitems that have an logic/ action behind them"). That is a line whose click acts
    /// (<see cref="CellAct.Action"/>: a pin, a switch, a graph opening Task Manager, Task view, a power
    /// button) and is not made of rows that light themselves (CellIsland.LightsItsRows), or a widget
    /// not set up, whose whole line is its "Set up in Settings". A line whose click does nothing — a
    /// slider's, the media's, a timer's, a stock's — lights only its own buttons, rows and thumbs.
    /// </summary>
    internal static bool LightsWhole(FolderCell cell, IIsland? island) =>
        island is CellIsland { Unset: not null }
        || (ActOf(cell) == CellAct.Action && island is not CellIsland { LightsItsRows: true }
            // Nor one this PC hasn't got, which a press does nothing on (§247).
            && (island as CellIsland)?.UnavailableOn(0) is null);

    /// <summary>
    /// The colour of <paramref name="fill"/> in <paramref name="theme"/>, as the folder's brushes are made
    /// from it, the line's <paramref name="own"/> colours over them (§311, §405: its look's, <see cref="Look.In"/>'s
    /// rule; a line has nothing of the bar's under them): its card at rest, its highlight under the
    /// pointer and, at two thirds of it as on the bar, pressed — laid over its card, as the bar's highlight is (§417). One
    /// brush, its colour faded between them while arranged (washFade), which the compositor interpolates straight: the
    /// highlight in place of a card (a 70 % card to a 10 % highlight) went through a half-clear light grey, a flash
    /// brighter than either; laid over it the colour changes by the lift alone, and with no card only the highlight's
    /// alpha changes.
    /// </summary>
    internal static Color InkOf(FlyoutTheme theme, CardFill fill, Palette own = default, bool carded = true)
    {
        var hover = own.Highlight is { } lit ? ColorOf(lit) : theme.SubtleHover;
        var card = LineFill(theme, own, carded).Fill is { A: > 0 } plate ? plate : Color.FromArgb(0, hover.R, hover.G, hover.B);
        return fill switch
        {
            CardFill.LineHover => Blend(card, hover),
            CardFill.LinePressed => Blend(card, own.Highlight is { } held ? ColorOf(held with { A = (byte)MathF.Round(held.A * 2f / 3f) }) : theme.SubtlePressed),
            _ => card,
        };
    }

    private static Color ColorOf(Argb color) => Color.FromArgb(color.A, color.R, color.G, color.B);

    /// <summary>
    /// §464 (the person: "if a folder face is transparent … it should show the desktop directly. same for the widgets in the
    /// folder"): what a line is laid on, straight over the desktop — nothing under it, the folder has no panel: its
    /// background (<paramref name="own"/>'s card), the blur of the desktop under it where it is see-through and Windows'
    /// transparency effects are on (<see cref="FlyoutTheme.Glass"/>), §546 at its opacity straight over the desktop where
    /// they are off; None nothing; Windows' own Windows' taskbar flyout's surface (<see cref="FlyoutTheme.Folder"/>). Nothing for what is no
    /// line of a card (<paramref name="carded"/> false: a divider between two cards), nor in high contrast, whose panel is
    /// solid.
    /// </summary>
    internal static (Color Fill, bool Blur) LineFill(FlyoutTheme theme, Palette own, bool carded = true)
    {
        if (!carded || theme.HighContrast)
        {
            return (default, false);
        }
        return own.Card switch
        {
            null => theme.Glass ? (theme.Tint, true) : (theme.Base, false),
            { A: 0 } => (default, false),
            { A: 255 } solid => (ColorOf(solid), false),
            { } seeThrough => (ColorOf(seeThrough), theme.Glass),
        };
    }

    /// <summary>§464: where <paramref name="view"/>'s origin stands in its window: its offset and those of the views it stands in.</summary>
    private static Vector2 Placed(Visual? view)
    {
        var at = Vector2.Zero;
        for (var each = view; each is not null; each = each.Parent)
        {
            at += new Vector2(each.Offset.X, each.Offset.Y);
        }
        return at;
    }

    private static Vector2 Snap(Vector2 point) => new(MathF.Round(point.X), MathF.Round(point.Y));
    /// <summary>§464: whether line <paramref name="index"/> is a line of a card — a widget's, or a divider drawn across one — rather than a gap between two.</summary>
    private bool OnCard(int index) =>
        !Flat && index >= 0 && index < tiles.Count && (Carded(tiles[index].Cell) || (Divides(tiles[index].Cell) && plan?.RunOf(index) is not null));

    /// <summary>Whether <paramref name="cell"/>'s line stands on a card of its own: a widget's, any but a divider (a gap).</summary>
    internal static bool Carded(FolderCell? cell) => cell?.Island?.Info is { } info && info != WidgetInfos.Divider;

    /// <summary>
    /// §311, §405: the colours line <paramref name="index"/> wears in the mode it is drawn in: the folder's look's (§567: the
    /// Appearance's Widgets in folders, every line's alike); a divider the lines' where it is drawn across a card (§410: its
    /// room is the card's; §462: its pattern too, the section's sheet running through it), none between two cards; none in
    /// high contrast.
    /// </summary>
    private Palette LineColors(int index) =>
        look.HighContrast || index < 0 || index >= tiles.Count ? default
        : Carded(tiles[index].Cell) ? look.Colors
        : Divides(tiles[index].Cell) && plan?.RunOf(index) is not null ? new Palette(Card: look.Colors.Card, CardPattern: look.Colors.CardPattern)
        : default;

    /// <summary>§405: a line's style: the folder's, its text and accent its look's.</summary>
    private TileStyle LineStyle => StyleFor(scale, look, theme);

    /// <summary>§311: line <paramref name="index"/>'s outline: its own card outline or the folders', clear where neither has one.</summary>

    /// <summary>
    /// Whether <paramref name="cell"/>'s line is one that never lights under the pointer: a
    /// heading, which is a title and not a thing to press (HeadingTile), the time and date over a
    /// clock's flyout, which is not either (TimeHeaderTile), or a divider, which is a gap
    /// (DividerTile).
    /// </summary>
    internal static bool Quiet(FolderCell cell) =>
        Heads(cell)
        || string.Equals(cell.Island?.Kind, WidgetInfos.TimeHeader.Id, StringComparison.Ordinal) || Divides(cell);

    /// <summary>Whether <paramref name="cell"/>'s line is a heading (HeadingTile).</summary>
    internal static bool Heads(FolderCell? cell) => string.Equals(cell?.Island?.Kind, WidgetInfos.Heading.Id, StringComparison.Ordinal);

    /// <summary>
    /// §649 (the person, 2026-11-09: "dividers become an outline of the folder"): a divider of a folder standing flat on its
    /// ground, drawn: a line <paramref name="stroke"/> px thick — the folder's outline's weight (FlyoutHost.Rim) — across its
    /// <paramref name="room"/>'s whole width, the column's, in its middle, on whole px. Where the cards stood apart, each in its
    /// outline, the one line of the outline that parts them. Pure.
    /// </summary>
    internal static D2D_RECT_F RuleOf(D2D_RECT_F room, float stroke)
    {
        var top = MathF.Round((room.top + room.bottom - stroke) / 2);
        return new D2D_RECT_F { left = room.left, top = top, right = room.right, bottom = top + stroke };
    }

    /// <summary>
    /// §649: the ink of an attached folder's dividers: the folder's outline (the Appearance's Flyouts' Outline, Windows' own
    /// flyout's stroke unset); where that is None, the theme's divider (DividerStrokeColorDefault), so a divider still parts.
    /// </summary>
    internal static Color RuleInk(FlyoutTheme theme) => theme.Stroke.A > 0 ? theme.Stroke : theme.Divider;

    /// <summary>Whether <paramref name="cell"/>'s line is a divider (DividerTile).</summary>
    internal static bool Divides(FolderCell? cell) => string.Equals(cell?.Island?.Kind, WidgetInfos.Divider.Id, StringComparison.Ordinal);

    /// <summary>§435: whether <paramref name="cell"/>'s line is a divider that grows (<see cref="DividerStyle.Grow"/>).</summary>
    internal static bool Grows(FolderCell? cell) => Divides(cell) && cell!.Island.Divider?.Style == DividerStyle.Grow;

    /// <summary>
    /// edit: the least height a divider is taken hold of over, in DIPs, centred on its gap
    /// (<see cref="FolderPlan.Grabs"/>), and far less than a line of a widget (40 DIPs).
    /// </summary>
    internal const int DividerDip = 13;

    /// <summary>
    /// §505: the empty room a folder separator keeps either side of its middle, in DIPs: the 3 its room alone always
    /// had (§78). Nobody sets it; nothing is drawn in it.
    /// </summary>
    internal const int SeparatorSpaceDip = 3;

    /// <summary>
    /// The room a folder separator takes down its list at <paramref name="scale"/>, in whole px (§307): its room both
    /// sides together, rounded once (a gap of 6 DIPs is 9 px at 150 %) — the gap the cards either side stand apart,
    /// and two lists too (<see cref="FolderPlan.ListGap"/>). It is all the room between the two lines.
    /// </summary>
    internal static float SeparatorRoom(float scale) => PopupPaint.Round(2 * SeparatorSpaceDip * scale);

    /// <summary>
    /// §651: how far apart an attached folder's columns stand, in px at <paramref name="scale"/>: the seam between them, the
    /// folder's outline's weight — a whole px or two, as the host draws it (FlyoutHost.Rim) — and no air.
    /// </summary>
    internal static float SeamOf(Look look, float scale) => MathF.Max(PopupPaint.Round(FlyoutTheme.For(look).StrokeDip * scale), 1);

    /// <summary>
    /// The hovered or pressed line lit, each "+" in its look, where a carried widget would land, the
    /// selected widget's ring and the focus rectangle.
    /// </summary>
    private void ShowStates()
    {
        if (plan is not { } shown || focusVisual is null)
        {
            return;
        }
        // edit: a button is drawn again only where its look changed.
        DrawAdds();
        Try(() =>
        {
            // §677: none of them while the folder is the add page's ghost or it adds into it.
            if (addsLayer is not null && addsLayer.IsVisible == ButtonsOff)
            {
                addsLayer.IsVisible = !ButtonsOff;
            }
            // A line wears no card: it is its own furniture, and lights with the subtle fill alone —
            // unless it has a card, an outline or a highlight of its own (§311); a heading does not
            // light at all. A line being dragged out of the folder goes faint.
            var chosen = shown.Editing ? alsoPicked.Select(key => PickedCard(key, tiles)).Append(PickedCard(picked, tiles)).ToHashSet() : [];
            // §593: the add page's ghost among the lines, while nothing is carried.
            var ghostCard = shown.Editing && Carried() is null ? PickedCard(ghostKey, tiles) : NoTarget;
            for (var index = 0; index < cards.Count; index++)
            {
                var quiet = index < tiles.Count && Quiet(tiles[index].Cell);
                // folders: only a line that is one thing to press lights as a whole (LightsWhole) —
                // except while the folder is arranged, where a press picks up the whole widget.
                var still = !shown.Editing && index < tiles.Count && !LightsWhole(tiles[index].Cell, tiles[index].Island);
                var hotHere = index == hot;
                var fill = FillOf(hotHere, index == pressed && index == hot, quiet, still);
                // edit: arranged, the line's own wash fades to it (washes); at rest it is set at once.
                if (index < washes.Count)
                {
                    var carded = OnCard(index);
                    washes[index].Color = InkOf(theme, fill, LineColors(index), carded);
                    if (index < frosts.Count)
                    {
                        frosts[index].IsVisible = LineFill(theme, LineColors(index), carded).Blur;
                    }
                }
                // §524: a spacer's stripes stronger under the pointer and selected.
                if (index < bands.Count)
                {
                    bands[index].Wear(shown.Editing && (hotHere || chosen.Contains(index)) ? bandStrong : bandInk);
                }
                if (index < contents.Count)
                {
                    // Lifted, it is the carrier that goes faint (EditLook.CarriedOutOpacity), not the line in it too. §593 the
                    // add page's ghost, §625 whole, as it will stand; §617 the empty one nothing of its line drawn, §625 on its
                    // card all the same: without it the room showed the dim under the folder, dark in light mode too.
                    contents[index].Sprite.Opacity = index == dragging && RaisedOf(index) is null ? 0.4f : index == ghostCard && ghostEmpty ? 0f : 1f;
                }
            }
            // §593: the ghost of a carried line round the add page's ghost (FolderFlyout.Drag.cs), its card as it stands; none
            // where none stands — not while a line is carried, whose ghost it is then.
            if (ghostVisual is not null && ghostBox is not null && Carried() is null)
            {
                // §606 the whole folder the ghost: §621 round each of its lines on its own, as much of it as its list shows, never
                // one outline round them all.
                IEnumerable<int> ghostCards = ghosted && shown.Editing ? Enumerable.Range(0, shown.Cards.Count)
                    : ghostCard >= 0 && ghostCard < shown.Cards.Count ? [ghostCard] : [];
                ShowGhosts(shown, [.. ghostCards
                    .Select(index => Shown(shown, shown.CardList[index], shown.Cards[index]))
                    .Where(card => card.bottom > card.top && card.right > card.left)
                    .Select(card => new RectF(card.left, card.top, card.right - card.left, card.bottom - card.top))]);
            }
            // edit: where the carried widget would land — a bar across its list, in between two lines
            // or at an end. A slot it would be a list in shows its ghost (FolderFlyout.Drag.cs).
            if (targetVisual is not null && targetBox is not null)
            {
                // Not while the line is lifted: the lines open its gap, its ghost in it (FolderFlyout.Drag.cs).
                var mark = landing is { New: false } place && Carried() is null ? MarkFor(shown, place) : default;
                var show = mark.right > mark.left;
                targetVisual.IsVisible = show;
                if (show)
                {
                    targetVisual.Size = new Vector2(shown.Width, shown.Height);
                    targetBox.Offset = new Vector2(mark.left, mark.top);
                    targetBox.Size = new Vector2(mark.right - mark.left, mark.bottom - mark.top);
                    var round = (mark.bottom - mark.top) / 2;
                    targetBox.CornerRadius = new Vector2(round, round);
                }
            }
            // edit: the selected widget's ring, while the folder is arranged and the widget is not
            // being carried.
            if (pickedVisual is not null && pickedRing is not null)
            {
                Ring(pickedVisual, pickedRing, shown.Editing ? PickedCard(picked, tiles) : NoTarget);
                // edit: and every other selected line of it, in the same ring (AlsoPicked).
                var also = shown.Editing
                    ? alsoPicked.Select(key => PickedCard(key, tiles)).Where(card => card >= 0 && card != PickedCard(picked, tiles)).ToList()
                    : [];
                while (alsoRings.Count < also.Count && ringsLayer is not null)
                {
                    var c = host.Compositor;
                    var visual = Own(c.CreateShapeVisual());
                    var box = Own(c.CreateRoundedRectangleGeometry());
                    var stroke = Own(c.CreateSpriteShape(box));
                    var ink = Own(c.CreateColorBrush(theme.Accent));
                    stroke.StrokeBrush = ink;
                    alsoInks.Add(ink);
                    visual.Shapes.Add(stroke);
                    ringsLayer.Children.InsertBelow(visual, pickedVisual);
                    alsoRings.Add((visual, box));
                }
                for (var index = 0; index < alsoRings.Count; index++)
                {
                    Ring(alsoRings[index].Visual, alsoRings[index].Ring, index < also.Count ? also[index] : NoTarget);
                }
            }

            // The ring round card <paramref name="chosen"/>: shown where it is not carried and its list shows it.
            void Ring(ShapeVisual visual, CompositionRoundedRectangleGeometry ring, int chosen)
            {
                var rect = chosen >= 0 && chosen < shown.Cards.Count ? Shown(shown, shown.CardList[chosen], shown.Grab(chosen)) : default;
                // Hidden while it is carried, in the folder or out of it, as a block's on the bar; back
                // as soon as it is let go — a click lifts it too, and it is selected at once. The lines
                // carried with it the same (FolderFlyout.Pending.cs).
                var carriedNow = Carried() is not null && RaisedOf(chosen) is not null;
                // A line on the clipboard wears the marching ants in its ring's place (FolderFlyout.Ants.cs).
                var show = chosen >= 0 && chosen != dragging && !carriedNow && !IsCopied(chosen) && shown.Holds(chosen) && rect.bottom > rect.top;
                visual.IsVisible = show;
                if (show)
                {
                    // As the bar rings a block (EditLook.cs): twice the hairline, on the line's edge,
                    // inside it. On whole pixels, so its stroke is crisp where a line stands on a half one.
                    var thick = EditLook.Ring(EditLook.Hair(scale));
                    var (left, top) = (MathF.Round(rect.left), MathF.Round(rect.top));
                    var (right, bottom) = (MathF.Round(rect.right), MathF.Round(rect.bottom));
                    visual.Offset = new Vector3(left, top, 0);
                    visual.Size = new Vector2(right - left, bottom - top);
                    var (offset, size, corner) = EditLook.Stroke(visual.Size, shown.Radius, thick);
                    ring.Offset = offset;
                    ring.Size = size;
                    ring.CornerRadius = new Vector2(corner, corner);
                    ((CompositionSpriteShape)visual.Shapes[0]).StrokeThickness = thick;
                }
            }
            // edit: the lines on the clipboard, in the marching ants (FolderFlyout.Ants.cs), and the spacers' stripes moving.
            ShowAnts(shown);
            MoveBands(shown);
            // The focus ring goes round the line or the "+" the keys are on: a line as much of it as
            // its list shows, a "+" round.
            var (focusRect, focusRadius) = FocusRing(shown);
            if (keyboard && focused >= 0 && focused < shown.Cards.Count && focusRect.right > focusRect.left && focusRect.bottom > focusRect.top)
            {
                // A line's, as the bar's block's (EditLook.cs): the focus ink's band on its edge and the
                // window ink's hairline inside that, both inside the line, over its ring.
                var hair = EditLook.Hair(scale);
                var (left, top) = (MathF.Round(focusRect.left), MathF.Round(focusRect.top));
                focusVisual.Offset = new Vector3(left, top, 0);
                focusVisual.Size = new Vector2(MathF.Round(focusRect.right) - left, MathF.Round(focusRect.bottom) - top);
                var (outerAt, outerSize, outerCorner) = EditLook.Stroke(focusVisual.Size, focusRadius, EditLook.Ring(hair));
                focusOuter!.Offset = outerAt;
                focusOuter.Size = outerSize;
                focusOuter.CornerRadius = new Vector2(outerCorner, outerCorner);
                ((CompositionSpriteShape)focusVisual.Shapes[0]).StrokeThickness = EditLook.Ring(hair);
                var (innerAt, innerSize, innerCorner) = EditLook.Stroke(focusVisual.Size, focusRadius, hair, EditLook.Ring(hair));
                focusInner!.Offset = innerAt;
                focusInner.Size = innerSize;
                focusInner.CornerRadius = new Vector2(innerCorner, innerCorner);
                ((CompositionSpriteShape)focusVisual.Shapes[1]).StrokeThickness = hair;
                focusVisual.IsVisible = true;
            }
            // A "+": a button's, outside it, as the edit bars' buttons have it.
            else if (keyboard && focusRect.right > focusRect.left && focusRect.bottom > focusRect.top)
            {
                var rect = focusRect;
                var hair = EditLook.Hair(scale);
                var outside = EditLook.Ring(hair);
                var (width, height) = (rect.right - rect.left, rect.bottom - rect.top);
                focusVisual.Offset = new Vector3(rect.left - outside, rect.top - outside, 0);
                focusVisual.Size = new Vector2(width + 2 * outside, height + 2 * outside);
                focusOuter!.Offset = new Vector2(hair, hair);
                focusOuter.Size = new Vector2(width + 2 * outside - 2 * hair, height + 2 * outside - 2 * hair);
                focusOuter.CornerRadius = new Vector2(focusRadius + outside, focusRadius + outside);
                ((CompositionSpriteShape)focusVisual.Shapes[0]).StrokeThickness = 2 * hair;
                focusInner!.Offset = new Vector2(outside + hair / 2, outside + hair / 2);
                focusInner.Size = new Vector2(width - hair, height - hair);
                focusInner.CornerRadius = new Vector2(focusRadius, focusRadius);
                ((CompositionSpriteShape)focusVisual.Shapes[1]).StrokeThickness = hair;
                focusVisual.IsVisible = true;
            }
            else
            {
                focusVisual.IsVisible = false;
            }
        });
    }

    /// <summary>
    /// Where the focus ring goes, panel px, and its corner: round the line or the "+" the keys are on — a line as much
    /// of it as its list shows, a "+" round; at rest round the stop of the line the keys are on (FolderFlyout.Keys.cs),
    /// as a line's; nowhere while a line's own keys ring what they are on themselves (one ring, §471).
    /// </summary>
    private (D2D_RECT_F Box, float Radius) FocusRing(FolderPlan shown) =>
        OwnKeyed() is { RingsOwnKeys: true } ? (default, 0f)
        : focused >= 0 && focused < shown.Cards.Count && KeyRing() is { } stop
            ? (Shown(shown, shown.CardList[focused], stop.Box), stop.Radius)
        : focused >= 0 && focused < shown.Cards.Count
            ? (Shown(shown, shown.CardList[focused], shown.Grab(focused)), shown.Radius)
        : focused >= AddTarget && focused - AddTarget < shown.Adds.Count && AddBox(shown, focused - AddTarget) is var add
            ? (add, (add.right - add.left) / 2)
            : (default, 0f);

    /// <summary>Tests: whether the folder's own focus ring shows (the keys in use, round something).</summary>
    internal bool FocusRingShows => keyboard && plan is { } shown && FocusRing(shown).Box is var box && box.right > box.left && box.bottom > box.top;

    /// <summary>As much of <paramref name="box"/> (panel px) as list <paramref name="list"/>'s panel shows.</summary>
    private static D2D_RECT_F Shown(FolderPlan shown, int list, D2D_RECT_F box)
    {
        if (list < 0 || list >= shown.Lists.Count)
        {
            return box;
        }
        var panel = shown.Lists[list].Box;
        var top = MathF.Max(box.top, panel.top);
        return box with { top = top, bottom = MathF.Max(top, MathF.Min(box.bottom, panel.bottom)) };
    }

    /// <summary>Where the widget of card <paramref name="index"/> stands in the folder, or null where the folder has lost it.</summary>
    private FolderPlace? PlaceOfCard(int index) =>
        folder is { } island && index >= 0 && index < tiles.Count ? Folders.PlaceOf(island.Options, tiles[index].Cell) : null;

    /// <summary>
    /// edit: what a landing between two lines looks like: a bar of the accent across the list, on the
    /// seam where the widget would go in — over the first line it would stand above, or under the
    /// list's last — held inside the lists' room (panel px).
    /// </summary>
    private D2D_RECT_F MarkFor(FolderPlan shown, FolderPlace place)
    {
        if (place.List < 0 || place.List >= shown.Lists.Count)
        {
            return default;
        }
        var thick = MathF.Max(2, PopupPaint.Round(3 * scale));
        float? seam = null;
        float? last = null;
        for (var index = 0; index < shown.Cards.Count && index < tiles.Count; index++)
        {
            if (shown.CardList[index] != place.List || PlaceOfCard(index) is not { } at)
            {
                continue;
            }
            var card = shown.Cards[index];
            if (at.Index >= place.Index && (seam is null || card.top < seam))
            {
                seam = card.top;
            }
            if (last is null || card.bottom > last)
            {
                last = card.bottom;
            }
        }
        var panel = shown.Lists[place.List].Box;
        var y = (seam ?? last ?? panel.top);
        y = Math.Clamp(y, panel.top + thick / 2, MathF.Max(panel.top + thick / 2, panel.bottom - thick / 2));
        return PopupPaint.Rect(shown.Lists[place.List].Left, y - thick / 2, shown.Lists[place.List].Width, thick);
    }

    /// <summary>
    /// Whether the widget at <paramref name="index"/> draws its own card here, rather than the
    /// folder drawing a line for it: true for a component (<see cref="Sections.CardInList"/>) — the
    /// same question the drawing asks, so what a click lands on is what is there.
    /// <para>
    /// A list's lines used to be the folder's drawing whatever was in them, and four places said
    /// so by leaving a list alone: the click, the hover, the pointer a widget takes and the
    /// keyboard's widget. That stopped being true the day a component could ask for a card in a
    /// list, and a panel converted into a list folder then drew correctly and did nothing.
    /// </para>
    /// </summary>
    private bool DrawsOwnCard(int index) =>
        index >= 0 && index < tiles.Count && ListWidgets.Find(tiles[index].Cell.Island.Kind) is { Line: ListLine.Card };

    /// <summary>
    /// folders: <paramref name="widget"/> is being carried over this folder from the bar with the
    /// pointer at <paramref name="screen"/>. True while it is over the panel. Nothing moves between
    /// the taskbar and a folder (<see cref="Partners"/>), so no place lights — there is none it could
    /// take — and the footer says why for as long as it is carried there (<see cref="Refusal"/>).
    /// One folder stands open at a time, so nothing is ever carried in from another.
    /// </summary>
    internal bool Offer(POINT screen, WidgetItem widget)
    {
        if (!IsOpen || plan is not { Editing: true } shown || folder is null || !host.ToPanel(screen, out _))
        {
            NoOffer();
            return false;
        }
        offering = widget;
        SayRefusal(Partners.WhyNot(widget, Partners.TypeOf(shown.View)));
        ShowStates();
        return true;
    }

    /// <summary>folders: nothing is being carried over this folder any more.</summary>
    internal void NoOffer()
    {
        if (offering is null)
        {
            return;
        }
        offering = null;
        SayRefusal("");
        ShowStates();
    }

    /// <summary>
    /// folders: why the folder won't take what is being carried over it, in the sentence its footer
    /// says when it is let go there (<see cref="Partners.WhyNot"/>). Empty while nothing is carried
    /// over it.
    /// </summary>
    internal string Refusal =>
        offering is not { } kind || folder is not { } island ? "" : Partners.WhyNot(kind, Partners.TypeOf(island.View));

    /// <summary>
    /// folders: the footer says <paramref name="why"/> for the widget carried over the folder, or,
    /// given nothing, stops saying what it said for the last one. Only its own sentence is taken
    /// back: a line the dock put on the footer since stays.
    /// </summary>
    private void SayRefusal(string why)
    {
        if (why == refusing)
        {
            return;
        }
        var said = refusing;
        refusing = why;
        if (why.Length > 0)
        {
            Say(why);
        }
        else if (note == said)
        {
            Say("");
        }
    }


    /// <summary>
    /// edit: a line the folder says for a moment — why a widget let go on the folder was refused —
    /// in a tip over the folder, on its side away from the bar (<see cref="ShowNote"/>): the folder
    /// has no title row to say it on any more. The dock sets it, and so does the folder for a widget
    /// carried over it that may not stand here (<see cref="Offer"/>), which it says for as long as
    /// the widget is over it; anything else goes after <see cref="NoteMs"/>.
    /// </summary>
    internal void Say(string said)
    {
        note = said;
        if (Hwnd != HWND.NULL)
        {
            _ = KillTimer(Hwnd, NoteTimer);
            if (said.Length > 0 && said != refusing)
            {
                _ = SetTimer(Hwnd, NoteTimer, NoteMs, null);
            }
        }
        ShowNote();
    }

    /// <summary>edit: the moment of a note the dock set is over.</summary>
    private void NoteDone()
    {
        _ = KillTimer(Hwnd, NoteTimer);
        if (note != refusing)
        {
            note = "";
            ShowNote();
        }
    }

    private void SetHot(int target)
    {
        if (target == hot)
        {
            return;
        }
        hot = target;
        ShowStates();
    }

    /// <summary>
    /// What is under <paramref name="point"/> (panel px), <paramref name="onPanel"/> saying whether it
    /// is on the panel: a line's index, and while the folder is arranged its "+" — one outside the
    /// panel too, where the window keeps room for it.
    /// </summary>
    private int TargetAt(Vector2 point, bool onPanel = true)
    {
        if (plan is not { } shown)
        {
            return NoTarget;
        }
        if (shown.Editing && !ButtonsOff)
        {
            // A column's button wherever it stands: outside the panels, or over a list's edge where it came in.
            for (var index = 0; index < shown.Adds.Count; index++)
            {
                if (Round(AddBox(shown, index), point))
                {
                    return AddTarget + index;
                }
            }
        }
        if (!onPanel)
        {
            return NoTarget;
        }
        var list = shown.ListOn(point);
        // edit: a divider's grab lies over its neighbours' edges, and wins there.
        for (var index = 0; shown.Editing && list >= 0 && index < shown.Grabs.Count && index < shown.Cards.Count; index++)
        {
            if (shown.CardList[index] == list && index < tiles.Count && Divides(tiles[index].Cell) && Inside(shown.Grab(index), point))
            {
                return index;
            }
        }
        for (var index = 0; list >= 0 && index < shown.Cards.Count; index++)
        {
            if (shown.CardList[index] == list && Inside(shown.Cards[index], point))
            {
                return index;
            }
        }
        return NoTarget;
    }

    /// <summary>
    /// §430: what a press at <paramref name="lParam"/> (the window's px) goes down on, as what it opens remembers its
    /// opener (Openers): a line by its widget's key, a "+" by its place; null for the folder's room.
    /// </summary>
    internal object? OpenerAt(LPARAM lParam)
    {
        var onPanel = host.ToPanel(lParam, out var point);
        var target = TargetAt(point, onPanel);
        return target == NoTarget ? null : target >= 0 && target < tiles.Count ? tiles[target].Island.Key : target;
    }

    /// <summary>Whether <paramref name="point"/> is on the round button in <paramref name="box"/>.</summary>
    private static bool Round(D2D_RECT_F box, Vector2 point)
    {
        var radius = (box.right - box.left) / 2;
        var centre = new Vector2((box.left + box.right) / 2, (box.top + box.bottom) / 2);
        // A button of no box is not there (WithSlots).
        return radius > 0 && Vector2.DistanceSquared(point, centre) <= radius * radius;
    }

    /// <summary>
    /// A click on <paramref name="target"/> at <paramref name="point"/> (panel px; null from the keys):
    /// the widget's own click, which may be handled by the widget (the folder stays open) or ask
    /// the dock for items or a view (the folder closes and says so).
    /// </summary>
    private FolderFlyoutEvent Activate(int target, Vector2? point, int? keyed = null)
    {
        // edit: a widget's own click does nothing while the folder is being arranged: the click
        // opens the widget's settings instead.
        if (editing && plan is { } editPlan)
        {
            // A column's "+": the add list for it, on the button's side away from the bar, held
            // pressed while the list stands; what is picked goes in at once at the list's end away
            // from the bar (AddPlace), or as a list of its own where a slot stands (§246, §297).
            if (target >= AddTarget && target - AddTarget < editPlan.Adds.Count
                && editPlan.Adds[target - AddTarget] is { Kind: FolderAddKind.Add } plus
                && (plus.Place.New ? plus.Place : AddPlace(plus.List)) is { } into)
            {
                sizing = target - AddTarget;
                host.StaysOpen = true;
                ShowStates();
                return new FolderFlyoutEvent(FolderFlyoutAction.Add, Place: into, Field: CardOnScreen(AddBox(editPlan, sizing)));
            }
            // A list's width button: its menu of the three widths, over the folder, which holds
            // itself open for it as for a widget's menu until the dock says it ended (MenuClosed).
            // A slot's the same, for the list it would become.
            if (target >= AddTarget && target - AddTarget < editPlan.Adds.Count
                && editPlan.Adds[target - AddTarget].Kind == FolderAddKind.Width)
            {
                sizing = target - AddTarget;
                host.StaysOpen = true;
                ShowStates();
                return new FolderFlyoutEvent(
                    FolderFlyoutAction.Width,
                    At: AwayOnScreen(AddBox(editPlan, sizing)),
                    Place: editPlan.Adds[sizing].Place);
            }
            // A list's warning: what it has no room for, as the edit bar's "not shown" button shows
            // it, the button held pressed while its menu stands as the width button is.
            if (target >= AddTarget && target - AddTarget < editPlan.Adds.Count
                && editPlan.Adds[target - AddTarget] is { Kind: FolderAddKind.Hidden } warning)
            {
                sizing = target - AddTarget;
                host.StaysOpen = true;
                ShowStates();
                return new FolderFlyoutEvent(
                    FolderFlyoutAction.NotShown,
                    At: AwayOnScreen(AddBox(editPlan, sizing)),
                    Place: warning.Place,
                    Hidden: HiddenIn(editPlan, warning.List));
            }

            // A widget's own click never acts while the folder is arranged: a click on its card or
            // its line opens its own page in Karpo's settings instead, as one on the bar does
            // (the dock; EditClicks.cs). The folder stays open.
            // A part of a card's own that edit mode edits — a sound device hidden or shown again
            // (§159) — is the card's: asked first, but for Ctrl and Shift, which only select more.
            if (target >= 0 && target < tiles.Count && target < editPlan.Cards.Count && point is { } editAt
                && DrawsOwnCard(target) && tiles[target].Island is CellIsland editable
                && !KeyState.Down(VK.VK_CONTROL) && !KeyState.Down(VK.VK_SHIFT))
            {
                var (box, local, style) = CardSpace(editPlan, target, editAt);
                if (editable.EditTargetOn(local, box, style, WidgetSurface.List) is > 0 and var part)
                {
                    return new FolderFlyoutEvent(FolderFlyoutAction.EditPart, editable, target, Part: part);
                }
            }
            if (target >= 0 && target < tiles.Count)
            {
                // Its card on the screen — a divider's grab — which the menu its click
                // opens stands beside (§212).
                return new FolderFlyoutEvent(FolderFlyoutAction.Settings, tiles[target].Island, target,
                    Field: target < editPlan.Cards.Count ? CardOnScreen(editPlan.Grab(target)) : default);
            }
            return default;
        }
        if (plan is not { } shown || target < 0 || target >= tiles.Count || target >= shown.Cards.Count)
        {
            return default;
        }
        var tile = tiles[target];
        // A widget that is not set up is its "Set up", the whole of its card or its line: a folder
        // opens no flyout, and such a widget has nothing else a click could do. What it needs is its
        // own setting, or an account, in its configuration menu (§228, §340), which opens over the
        // folder as a right click's menu does.
        if (tile.Island is CellIsland { Unset: not null })
        {
            host.StaysOpen = true;
            return new FolderFlyoutEvent(FolderFlyoutAction.Setup, tile.Island, target, At: point is null ? OnScreen(shown.Cards[target]) : default);
        }
        // One whose thing this PC hasn't got is greyed, and a click on it does nothing (§247).
        if (tile.Island is CellIsland cells && cells.UnavailableOn(Screen()) is not null)
        {
            return default;
        }
        // A list's line is the whole widget: its first cell's click — unless the widget draws its
        // own card there, when the click is asked of the card, as it is anywhere else.
        // keys: the stop the keys are on (FolderFlyout.Keys.cs), which is the target a click there reaches.
        var index = keyed ?? (point is { } at ? PartOf(shown, target, at) : 0);
        IslandAction action;
        try
        {
            action = tile.Island.Click(index);
            // calendar: a stopwatch started or paused here starts or stops the quick tick.
            ArmQuick();
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Console.Error.WriteLine($"Karpo: a folder's {tile.Island.Info.Id} click failed: {error.Message}");
            return default;
        }
        if (Forwards(action, ActOf(tile.Cell, tile.Island)))
        {
            // desktops: a desktop chosen closes the folder without giving the focus back: the window
            // that had it is on the desktop being left, and activated as the switch goes it would
            // switch Windows straight back. The dock switches (Dock.Folders.cs).
            Hide(restore: action.View != IslandView.Desktops);
            return new FolderFlyoutEvent(FolderFlyoutAction.Forward, tile.Island, index, action);
        }
        // A line's ">": its page in its column, in place (FolderIsland.Pages.cs); the folder stays open.
        if (action.View == IslandView.Page && folder is { } paged)
        {
            _ = paged.OpenPageOf(tile.Island.Key);
            return default;
        }
        // The footer's Edit: the folder arranged where it stands, its bar's edit mode on.
        if (action.View == IslandView.Arrange)
        {
            ArrangeAsked();
            return default;
        }
        // A line's dropdown (the sound page's Output and Input) opens its list under its field, over the
        // folder, which holds itself open for it.
        if (action.View == IslandView.Choices && tile.Island is SoundPartTile { ChoicesField: { } field } && target < shown.Cards.Count)
        {
            host.StaysOpen = true;
            return new FolderFlyoutEvent(FolderFlyoutAction.Choices, tile.Island, target, Field: FieldOnScreen(shown.Cards[target], field));
        }
        // A line's control that is its own menu — the now-playing line's picker — opens that menu at
        // the pointer over the folder, as a right click does, and the folder holds itself open for it.
        if (action.View == IslandView.Menu)
        {
            host.StaysOpen = true;
            return new FolderFlyoutEvent(FolderFlyoutAction.Context, tile.Island, target);
        }
        // Handled by the widget (a song skipped, a slider moved), or refused by the folder rule
        // (docs\widgets.md §15) — in which case nothing happens at all, and the folder stays open.
        return default;
    }

    /// <summary>
    /// Whether a click's <paramref name="action"/> leaves the folder for the dock to carry out: an
    /// app opening, from a widget whose click acts (<paramref name="act"/>, its line's
    /// <see cref="CellAct"/>), and nothing else — every other thing a click can carry (a
    /// flyout, a panel, a list of items) is a popup, and a folder opens none (docs\widgets.md §15).
    /// <para>
    /// Asked before the folder hides itself, so a click that does nothing leaves the folder open:
    /// click the weather, and the panel stays where it is.
    /// </para>
    /// </summary>
    internal static bool Forwards(in IslandAction action, CellAct act) =>
        act == CellAct.Action && action.View is IslandView.App or IslandView.Desktops or IslandView.Layouts;

    /// <summary>
    /// What a click on <paramref name="cell"/>'s widget may do in a folder: its line's
    /// <see cref="CellAct"/>, and nothing for a widget a list does not hold.
    /// </summary>
    internal static CellAct ActOf(FolderCell cell) =>
        ListWidgets.Find(cell.Island.Kind) is { } line ? line.Act : CellAct.Nothing;

    /// <summary>
    /// The same for the widget <paramref name="island"/> drawn there: a line that lists meetings acts
    /// where its click is a meeting's Join (IMeetingLine, docs\widgets.md §206) — the rest of it answers
    /// nothing, so its line's own act is still the folder's for everything else.
    /// </summary>
    internal static CellAct ActOf(FolderCell cell, IIsland island) =>
        island is IMeetingLine ? CellAct.Action : ActOf(cell);

    /// <summary>
    /// Line <paramref name="target"/>'s own space: the box its widget is drawn in and the style it
    /// is drawn with, with <paramref name="point"/> (panel px) moved into that box, and mirrored
    /// back where the line is mirrored (Mirror.cs). The caller has already made sure the card is there.
    /// </summary>
    private (D2D_RECT_F Box, Vector2 Local, TileStyle Style) CardSpace(FolderPlan shown, int target, Vector2 point)
    {
        var rect = shown.Cards[target];
        var local = point - new Vector2(rect.left, rect.top);
        // A mirrored line's own code knows a point by where it would stand unmirrored (Mirror.cs).
        if (Mirror.On)
        {
            local.X = Mirror.Back(local.X, rect.right - rect.left, Upright(target));
        }
        return (
            PopupPaint.Rect(0, 0, rect.right - rect.left, rect.bottom - rect.top),
            local,
            LineStyle with { Screen = Screen() });
    }

    /// <summary>The parts of line <paramref name="target"/> kept upright in its mirrored drawing (Mirror.Keep); none where it has none.</summary>
    private IReadOnlyList<(float From, float To)>? Upright(int target) =>
        target >= 0 && target < tiles.Count && uprights.TryGetValue(tiles[target].Island, out var kept) ? kept : null;

    /// <summary>The display the folder opened on, which a widget that sets its display's brightness sets.</summary>
    private nint Screen() => (nint)MonitorFromWindow(Hwnd, MONITOR.MONITOR_DEFAULTTONEAREST);

    /// <summary>
    /// quick: offers the press at <paramref name="point"/> (panel px) to the widget in card
    /// <paramref name="target"/>, which takes the pointer when it owns it — the volume slider does,
    /// so its thumb follows the pointer here as it does on the bar. Not while the folder is being
    /// arranged, where a press picks the widget up, and not in a list, whose lines are the widget's
    /// name and glyph rather than its own drawing (<see cref="Activate"/> reads a list the same way).
    /// </summary>
    private void TakePointer(int target, Vector2 point)
    {
        holding = null;
        if (editing || plan is not { } shown
            || target < 0 || target >= tiles.Count || target >= shown.Cards.Count
            || !DrawsOwnCard(target)
            || tiles[target].Island is not CellIsland cells)
        {
            return;
        }
        var (box, local, style) = CardSpace(shown, target, point);
        try
        {
            if (cells.PressOn(local, box, style, WidgetSurface.List))
            {
                holding = (target, cells);
                // The keys go where the pointer went to work: a field pressed on a card is typed in.
                focused = target;
            }
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            // One widget that throws where the pointer is handed to it leaves the folder standing.
            Console.Error.WriteLine($"Karpo: a folder's {cells.Info.Id} press failed: {error.Message}");
        }
    }

    /// <summary>
    /// list: the wheel or the touchpad turned <paramref name="delta"/> (WHEEL_DELTA a notch) over the
    /// line at <paramref name="screen"/>: its items glide that way where it is a line of items with
    /// more than it shows (ListScroll.cs). False where there is no such line, or its items are at
    /// their end that way - and then nothing moves: the folder itself never scrolls.
    /// </summary>
    private bool ScrollLine(POINT screen, int delta)
    {
        if (delta == 0 || moving >= 0 || plan is not { } shown || !host.ToPanel(screen, out var point))
        {
            return false;
        }
        var target = TargetAt(point, true);
        if (target < 0 || target >= tiles.Count || target >= shown.Cards.Count || !DrawsOwnCard(target)
            || tiles[target].Island is not CellIsland cells)
        {
            return false;
        }
        var (box, _, style) = CardSpace(shown, target, point);
        var laid = cells.Scrolled(box, style, WidgetSurface.List);
        if (!laid.Scrolls)
        {
            return false;
        }
        if (gliding != target)
        {
            StopGlide();
        }
        var from = gliding == target ? glideTo : laid.Offset;
        var to = Math.Clamp(from - (delta / 120f * ListScrolled.Notch(style, laid.Window.bottom - laid.Window.top)), 0, laid.Most);
        if (MathF.Abs(to - from) < 0.01f)
        {
            return false;
        }
        gliding = target;
        glideTo = to;
        _ = SetTimer(Hwnd, GlideTimer, GlideMs, null);
        Glide();
        return true;
    }

    /// <summary>
    /// list: a frame of a line's glide: its items a share of the way to where the wheel sent them -
    /// a Windows list's ease-out, quick at first and settling - drawn there, and the row under the
    /// pointer lit as it passes; done when they are there.
    /// </summary>
    private void Glide()
    {
        if (plan is not { } shown || gliding < 0 || gliding >= tiles.Count || gliding >= shown.Cards.Count
            || tiles[gliding].Island is not CellIsland cells)
        {
            StopGlide();
            return;
        }
        var line = gliding;
        var now = cells.ListScroll;
        var next = now + ((glideTo - now) * 0.35f);
        var done = MathF.Abs(glideTo - next) < 0.5f;
        cells.ListScroll = done ? glideTo : next;
        if (done)
        {
            StopGlide();
        }
        // The row under the pointer is another one now: the widget is told, and the line drawn.
        if (pointerAt is { } at && ReferenceEquals(hovering, TileAt(line)))
        {
            HoverOver(line, at);
        }
        else
        {
            DrawCard(shown, line);
        }
    }

    /// <summary>
    /// Whether the line at <paramref name="screen"/> takes the wheel itself where it has a part for it
    /// (Interaction.Wheel: a stepper's value, a mixer's sliders): it is asked by the notch first, and
    /// its items scroll only where it did not take it.
    /// </summary>
    private bool TakesWheel(POINT screen) =>
        !editing && plan is not null && host.ToPanel(screen, out var point) && TargetAt(point, true) is var target
        && target >= 0 && target < tiles.Count && tiles[target].Island is CellIsland cells && cells.Interaction.HasFlag(Interaction.Wheel);

    /// <summary>list: the glide ends where it has got to.</summary>
    private void StopGlide()
    {
        gliding = NoTarget;
        if (Hwnd != HWND.NULL)
        {
            _ = KillTimer(Hwnd, GlideTimer);
        }
    }

    /// <summary>Tests only: the wheel turned <paramref name="delta"/> over the line at <paramref name="screen"/>, as WM_MOUSEWHEEL hands it in; true where a line's items took it.</summary>
    internal bool WheelLine(POINT screen, int delta) => ScrollLine(screen, delta);

    /// <summary>Tests only: the glide run to its end at once.</summary>
    internal void GlideDone()
    {
        for (var frame = 0; frame < 200 && gliding != NoTarget; frame++)
        {
            Glide();
        }
    }

    /// <summary>
    /// quick: the wheel at <paramref name="screen"/> offered to the widget under it; true when the
    /// widget took it. Not while the folder is being arranged,
    /// where the wheel is the panel's, and not in a list, whose lines are the folder's drawing
    /// rather than the widget's.
    /// </summary>
    private bool WheelOver(POINT screen, int notches)
    {
        if (editing || plan is not { } shown || !host.ToPanel(screen, out var point))
        {
            return false;
        }
        var target = TargetAt(point, true);
        if (target < 0 || target >= tiles.Count || target >= shown.Cards.Count
            || tiles[target].Island is not CellIsland cells)
        {
            return false;
        }
        // Who takes this gesture is the card's or the line's own declaration, as the bar reads its
        // entry's (Interaction.Wheel): a folder does not grow a second answer to it.
        if (!cells.Interaction.HasFlag(Interaction.Wheel))
        {
            return false;
        }
        var (box, local, style) = CardSpace(shown, target, point);
        try
        {
            return cells.WheelOn(local, box, style, WidgetSurface.List, notches);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            // One widget that throws where the wheel is handed to it leaves the folder standing.
            Console.Error.WriteLine($"Karpo: a folder's {cells.Info.Id} wheel failed: {error.Message}");
            return false;
        }
    }

    /// <summary>
    /// §424: the wheel turned <paramref name="delta"/> (WHEEL_DELTA a notch, a touchpad's less) over a line with a part
    /// on the compositor (IStripLine) that takes the wheel: handed it as it turned; the line drawn again only where it
    /// says so, and its quick tick started. False where no such line took it. Not while the folder is arranged.
    /// </summary>
    private bool WheelStrip(POINT screen, int delta)
    {
        if (editing || delta == 0 || plan is not { } shown || !host.ToPanel(screen, out var point))
        {
            return false;
        }
        var target = TargetAt(point, true);
        if (target < 0 || target >= tiles.Count || target >= shown.Cards.Count
            || tiles[target].Island is not CellIsland cells || cells is not IStripLine sliding || !cells.Interaction.HasFlag(Interaction.Wheel))
        {
            return false;
        }
        var (box, local, style) = CardSpace(shown, target, point);
        try
        {
            if (!cells.WheelDeltaOn(local, box, style, WidgetSurface.List, delta))
            {
                return false;
            }
            if (sliding.Slid())
            {
                DrawCard(shown, target);
            }
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Console.Error.WriteLine($"Karpo: a folder's {cells.Info.Id} wheel failed: {error.Message}");
            return false;
        }
        ArmQuick();
        return true;
    }

    /// <summary>
    /// quick: where the pointer rests inside a card, told to the widget drawn there so it can light
    /// the part of itself under it - a stepper's button, a switch's pill. The card it left is told
    /// the pointer has gone. Not while arranging, where the pointer belongs to the drag.
    /// </summary>
    private void HoverOver(int target, Vector2? point)
    {
        if (plan is not { } shown)
        {
            return;
        }
        void Tell(int index, Vector2? at)
        {
            if (index < 0 || index >= tiles.Count || index >= shown.Cards.Count
                || !DrawsOwnCard(index)
                || tiles[index].Island is not CellIsland cells)
            {
                return;
            }
            var (box, local, style) = CardSpace(shown, index, at ?? default);
            try
            {
                cells.HoverOn(at is null ? null : local, box, style, WidgetSurface.List);
                // notes: over text to type in, the pointer is the text cursor, as the search box's is.
                if (at is not null && cells.TextOn(local, box, style))
                {
                    _ = SetCursor(LoadCursorW(HINSTANCE.NULL, IDC.IDC_IBEAM));
                }
                // desktops: over a row's drag handle, the move cursor: the row can be carried.
                else if (at is not null && cells.GripOn(local, box, style))
                {
                    _ = SetCursor(LoadCursorW(HINSTANCE.NULL, IDC.IDC_SIZEALL));
                }
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                Console.Error.WriteLine($"Karpo: a folder's {cells.Info.Id} hover failed: {error.Message}");
            }
            // The folder told it where the pointer is, so the folder draws what it made of that.
            // A widget with hover of its own (a list of rows lighting the one under the pointer)
            // has nothing else to ask: it cannot reach the surface it is drawn into, and a
            // whole-folder refresh per row boundary is the alternative nobody should pay.
            DrawCard(shown, index);
        }
        // The widget it leaves hears the pointer has gone, by what it is, not where it stood: the lines
        // laid out again (a page come over it, or gone back) may have moved it, or taken it out of them.
        var widget = TileAt(target);
        if (!ReferenceEquals(hovering, widget))
        {
            if (IndexOf(hovering) is var left and >= 0)
            {
                Tell(left, null);
            }
            else
            {
                Gone(hovering);
            }
            hovering = widget;
        }
        Tell(target, editing ? null : point);
        // The part under the pointer may have a tooltip (FolderFlyout.Tips.cs).
        TipOver(target, editing ? null : point);
    }

    // quick: the widget last told where the pointer is, so the one it leaves hears that it has gone.
    private IIsland? hovering;

    /// <summary>quick: <paramref name="widget"/>, on no line of the folder's now, hears the pointer has gone from it.</summary>
    private void Gone(IIsland? widget)
    {
        if (widget is not CellIsland cells)
        {
            return;
        }
        try
        {
            cells.HoverOn(null, default, StyleFor(scale, look, theme) with { Screen = Screen() }, WidgetSurface.List);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Console.Error.WriteLine($"Karpo: a folder's {cells.Info.Id} hover failed: {error.Message}");
        }
    }

    /// <summary>The pointer moved while a widget holds it: it is the widget's, wherever it has got to.</summary>
    private void DragHeld(Vector2 point)
    {
        if (holding is not { } held || plan is not { } shown || held.Target >= shown.Cards.Count)
        {
            return;
        }
        var (box, local, style) = CardSpace(shown, held.Target, point);
        try
        {
            held.Island.DragOn(local, box, style, WidgetSurface.List);
            // desktops: a row carried by its handle keeps the move cursor wherever the pointer goes.
            if (held.Island.Gripping)
            {
                _ = SetCursor(LoadCursorW(HINSTANCE.NULL, IDC.IDC_SIZEALL));
            }
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Console.Error.WriteLine($"Karpo: a folder's {held.Island.Info.Id} drag failed: {error.Message}");
        }
    }

    /// <summary>The widget lets the pointer go: the button went up, the capture went, or the folder closed.</summary>
    private void ReleaseHeld()
    {
        if (holding is not { } held)
        {
            return;
        }
        holding = null;
        try
        {
            held.Island.ReleaseOn(WidgetSurface.List);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Console.Error.WriteLine($"Karpo: a folder's {held.Island.Info.Id} release failed: {error.Message}");
        }
    }

    /// <summary>
    /// The keys: at rest the line with the focus has them first, then the folder's keys walk the lines
    /// and their stops (FolderFlyout.Keys.cs); Delete removes while arranged
    /// (<see cref="FolderFlyoutAction.Remove"/>). Escape is the host's. While the folder is arranged
    /// a widget's click is its settings opening, which a key held down (<paramref name="repeat"/>)
    /// asks for once.
    /// </summary>
    /// <summary>edit: a key a menu over the folder handed on as it closed (Dock.KeyFromMenu), as if it had come here.</summary>
    internal FolderFlyoutEvent KeyPassed(int key) => Key(key);

    private FolderFlyoutEvent Key(int key, bool repeat = false)
    {
        if (plan is not { } shown)
        {
            return default;
        }
        // quick: the widget with the focus is offered the key first - a slider's arrows change its
        // level, a stepper's step it - and only what it leaves moves the focus. Tab and Escape are
        // the panel's whatever has the focus, or there is no way out of a widget that answers
        // everything. Not while arranging, where the keys are the folder's.
        // keys: a line that walks its own keys has them only while they are on its own, not on a stop beside them.
        if (!editing && key is not (VK.VK_TAB or VK.VK_ESCAPE) && FocusedWidget() is { } widget
            && (!widget.WalksItsOwnKeys || OwnKeyed() is not null))
        {
            try
            {
                if (widget.KeyOn(key, WidgetSurface.List))
                {
                    OwnKeysMoved(widget);
                    ShowStates();
                    UiaFocusMoved();
                    return default;
                }
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                Console.Error.WriteLine($"Karpo: a folder's {widget.Info.Id} key failed: {error.Message}");
            }
        }
        var order = Order();
        var shift = KeyState.Down(VK.VK_SHIFT);
        // edit: arranged, the keys are edit mode's one keymap, the bar's too (EditKeys.cs).
        if (editing && EditKey(key, shift, repeat, order, shown) is { } edited)
        {
            return edited;
        }
        // keys: at rest, the folder's keymap (FolderFlyout.Keys.cs).
        return KeyAtRest(key, shift, order, shown);
    }

    /// <summary>
    /// edit: a key while the folder is arranged, by edit mode's one keymap (<see cref="EditKeys"/>),
    /// as the bar reads it (Dock.EditKey): what it does here (nothing scrolls: Page Up and
    /// Down do nothing). Escape is not here: the folder's
    /// <see cref="Message"/> has it before the host closes the folder.
    /// </summary>
    private FolderFlyoutEvent? EditKey(int key, bool shift, bool repeat, List<int> order, FolderPlan shown)
    {
        var line = focused >= 0 && focused < tiles.Count ? tiles[focused].Island : null;
        var stroke = EditKeys.Of(key, KeyState.Down(VK.VK_CONTROL), shift, repeat);
        switch (stroke.Act)
        {
            case EditKeyAct.Next or EditKeyAct.Previous when order.Count > 0:
            {
                var back = stroke.Act == EditKeyAct.Previous;
                var at = order.IndexOf(focused);
                focused = order[at < 0 ? (back ? order.Count - 1 : 0) : (at + (back ? -1 : 1) + order.Count) % order.Count];
                break;
            }
            // Ctrl+arrows move the line the keys are on, as on the bar (FolderFlyout.Drag.cs).
            case EditKeyAct.Move when line is not null:
                return KeyMove(focused, stroke.Key);
            // Shift+arrows size it, as on the bar (EditResize.cs, §471): the dock writes it, as its menu's change.
            case EditKeyAct.Resize when line is not null:
                keyboard = true;
                return new FolderFlyoutEvent(FolderFlyoutAction.Resize, line, focused, Part: stroke.Key);
            case EditKeyAct.Focus or EditKeyAct.Move:
                focused = focused is >= 0 && focused < shown.Cards.Count
                    ? Nearest(focused, stroke.Key)
                    : order.FirstOrDefault(NoTarget);
                break;
            case EditKeyAct.First:
                focused = order.FirstOrDefault(NoTarget);
                break;
            case EditKeyAct.Last:
                focused = order.LastOrDefault(NoTarget);
                break;
            // Space on a line: what a click on it does — it is selected, an open settings window
            // follows. On a "+", what a click on that does.
            // Its menu beside it too (§212).
            case EditKeyAct.Click when line is not null:
                return new FolderFlyoutEvent(FolderFlyoutAction.Select, line, focused,
                    Field: focused < shown.Cards.Count ? CardOnScreen(shown.Cards[focused]) : default);
            case EditKeyAct.Click or EditKeyAct.Open:
                return focused == NoTarget ? new FolderFlyoutEvent() : Activate(focused, null);
            // The menu key and Shift+F10: the line's menu, beside the line, as a click opens it (§212).
            case EditKeyAct.Menu when line is not null && focused < shown.Cards.Count:
                host.StaysOpen = true;
                return new FolderFlyoutEvent(FolderFlyoutAction.Context, line, focused, At: OnScreen(shown.Cards[focused]), Field: CardOnScreen(shown.Cards[focused]));
            // Delete, Ctrl+X, Ctrl+C, Ctrl+V, Ctrl+A, Ctrl+Z, Ctrl+Y: the selection's acts, as on the bar (Dock.SelectionAct) —
            // on the line the keys walked to, else what is selected (the line clicked). The folder has the keys while it is
            // arranged, so the bar never hears them.
            case var act when EditKeys.OfTheSelection(act):
                return new FolderFlyoutEvent(FolderFlyoutAction.Selection, keyboard ? line : null, focused, Act: act);
            default:
                return new FolderFlyoutEvent();
        }
        keyboard = true;
        ShowStates();
        return new FolderFlyoutEvent();
    }

    /// <summary>edit: the keys of the folder's lines in its reading order: what Ctrl+A selects and a Shift+click runs along.</summary>
    internal IReadOnlyList<string> LineKeys => [.. tiles.Select(tile => tile.Island.Key)];

    /// <summary>edit: the line keyed <paramref name="key"/>, whose menu the edit bar's widget crumb opens (§352); null where the folder has none.</summary>
    internal IIsland? LineOf(string key) =>
        tiles.FirstOrDefault(tile => string.Equals(tile.Island.Key, key, StringComparison.OrdinalIgnoreCase))?.Island;

    /// <summary>
    /// notes: the line whose text box has the keys (<see cref="CellIsland.TypingOn"/>), and where it
    /// is; null while none has. Never while the folder is arranged, where the keys are the folder's.
    /// </summary>
    private (int Index, CellIsland Island)? Typer()
    {
        if (editing)
        {
            return null;
        }
        for (var index = 0; index < tiles.Count; index++)
        {
            if (tiles[index].Island is CellIsland { TypingOn: true } typing)
            {
                return (index, typing);
            }
        }
        return null;
    }

    /// <summary>
    /// notes: a key or a character for a line's text box. The line typing has every key first; a
    /// character goes to it, else to the line with the focus (a to-do's first letter starts a task).
    /// True when one of them took it, and the folder does nothing more with it.
    /// </summary>
    private bool Typing(uint msg, WPARAM wParam)
    {
        if (!IsOpen || plan is null || editing || msg is not (WM.WM_KEYDOWN or WM.WM_CHAR))
        {
            return false;
        }
        var widget = Typer()?.Island ?? (msg == WM.WM_CHAR ? FocusedWidget() : null);
        if (widget is null)
        {
            return false;
        }
        bool took;
        try
        {
            took = msg == WM.WM_KEYDOWN
                ? widget.KeyOn((int)(nuint)wParam, WidgetSurface.List)
                : widget.CharOn((char)(nuint)wParam, WidgetSurface.List);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Console.Error.WriteLine($"Karpo: a folder's {widget.Info.Id} typing failed: {error.Message}");
            return false;
        }
        if (took)
        {
            FolderChanged();
        }
        return took;
    }

    /// <summary>notes: the IME's composition window at the typing line's caret, its candidates kept clear of it.</summary>
    private void PlaceComposition()
    {
        if (plan is not { } shown || Typer() is not { } typer || typer.Index >= shown.Cards.Count || typer.Island.CaretOn is not { } caret)
        {
            return;
        }
        var card = shown.Cards[typer.Index];
        var at = host.PanelAt + new Vector2(card.left, card.top);
        var left = (int)(at.X + caret.left);
        var top = (int)(at.Y + caret.top);
        var bottom = (int)MathF.Ceiling(at.Y + caret.bottom);
        var context = ImmGetContext(Hwnd);
        if (context == HIMC.NULL)
        {
            return;
        }
        try
        {
            var composition = new COMPOSITIONFORM { dwStyle = CFS_POINT, ptCurrentPos = new POINT { x = left, y = top } };
            _ = ImmSetCompositionWindow(context, &composition);
            var candidates = new CANDIDATEFORM
            {
                dwIndex = 0,
                dwStyle = CFS_EXCLUDE,
                ptCurrentPos = new POINT { x = left, y = bottom },
                rcArea = new RECT { left = left, top = top, right = left + 1, bottom = bottom },
            };
            _ = ImmSetCandidateWindow(context, &candidates);
        }
        finally
        {
            _ = ImmReleaseContext(Hwnd, context);
        }
    }

    /// <summary>notes: <paramref name="widget"/>'s text box lets the keys go, and the folder draws what that left.</summary>
    private void StopTyping(CellIsland widget)
    {
        try
        {
            widget.StopTypingOn();
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Console.Error.WriteLine($"Karpo: a folder's {widget.Info.Id} typing did not end: {error.Message}");
        }
        FolderChanged();
    }

    /// <summary>
    /// quick: the widget drawn in the card the keys are on, or null - when the focus is on a "+", a
    /// list's line (whose drawing is the folder's, not the widget's), or nothing at all.
    /// </summary>
    private CellIsland? FocusedWidget() =>
        plan is { } shown && focused >= 0 && focused < tiles.Count && DrawsOwnCard(focused)
            ? tiles[focused].Island as CellIsland
            : null;

    /// <summary>The lines in reading order — the first list's top to bottom, then the next's — and in edit mode the "+" buttons after them.</summary>
    private List<int> Order()
    {
        // The tiles are in the folder's reading order already (FolderIsland.Configure).
        var order = Enumerable.Range(0, tiles.Count)
            .Where(index => plan is not { } shown || shown.Shows(index))
            .ToList();
        // edit: every "+" is walked too, after the widgets, so the keys reach every "+" the pointer can.
        if (plan is { Editing: true } arranged && !ButtonsOff)
        {
            order.AddRange(Enumerable.Range(0, arranged.Adds.Count).Select(index => AddTarget + index));
        }
        return order;
    }

    /// <summary>The card nearest to <paramref name="from"/> in the arrow's direction; itself when there is none.</summary>
    private int Nearest(int from, int key)
    {
        if (plan is not { } shown)
        {
            return from;
        }
        // Where the lines show, so an arrow goes to what is beside; a line not shown is never one.
        var origin = Centre(shown.Cards[from]);
        var best = from;
        var bestScore = float.MaxValue;
        for (var index = 0; index < shown.Cards.Count; index++)
        {
            if (index == from || !shown.Holds(index))
            {
                continue;
            }
            var delta = Centre(shown.Cards[index]) - origin;
            var (along, across) = key switch
            {
                VK.VK_LEFT => (-delta.X, delta.Y),
                VK.VK_RIGHT => (delta.X, delta.Y),
                VK.VK_UP => (-delta.Y, delta.X),
                _ => (delta.Y, delta.X),
            };
            if (along <= 1)
            {
                continue;
            }
            // Mostly in the arrow's direction; straight ahead beats diagonally.
            var score = along + 2 * MathF.Abs(across);
            if (score < bestScore)
            {
                bestScore = score;
                best = index;
            }
        }
        return best;
    }

    /// <summary>The widgets had news, or the folder itself changed: its lines drawn again, or the list laid out again.</summary>
    private void FolderChanged()
    {
        // keys: what the lines show may have moved their stops (FolderFlyout.Keys.cs).
        stopsKept.Clear();
        if (!IsOpen || folder is not { } island || gfx is null)
        {
            return;
        }
        // At rest a folder with nothing left to show closes as an empty one never opens (§280, §327):
        // its last line gone while it has nothing to show.
        if (island.Gone || (!editing && island.Empty))
        {
            // The folder left the bar: the flyout closes as a light dismiss would, so the dock hears it.
            _ = PostMessageW(Hwnd, FlyoutHost.WM_KARPO_HOST_DISMISS, (WPARAM)(nuint)host.Generation, default);
            return;
        }
        var natural = Natural(island);
        // menus: a setting changed in a line's menu while the folder stands open (§224) configures the
        // folder again, which hands each line its new settings — a new cell, and for a style changed a
        // widget made again under the same key, as tall as before. The layout's signature is the same
        // then, so the lines drawn are the folder's own now, never the ones this list held before: a
        // widget made again is laid out as any other change of widget is; a line kept takes its new
        // cell, which is what draws its name and its look.
        if (Signature(island, natural) + ArrivingSignature != layoutSignature || !SameWidgets(island.Tiles))
        {
            // Other widgets: laid out afresh where it is, at once, whether or not
            // the pointer is on the panel. A list folder's height follows what is in it while
            // somebody is looking (the person asked for that by name), and it used to hold a
            // shrink while the pointer was on it, so the lines under the one that went did not
            // come up under the pointer; the person then asked for the layout never to hold back
            // for the pointer ("it should always update the layout"), so it shrinks there and then.
            //
            // What the pointer carries, and what it pressed, is kept by what it is, not by where
            // it stood: a widget before it may have come or gone (the file changed under the
            // folder, a line grew or shrank), and an index kept across that names another widget,
            // or none — a move would put the wrong widget down, a drag out take the wrong one to
            // the bar, a release click another card than the one pressed.
            var carried = TileAt(moving);
            var draggedOut = TileAt(dragging);
            var pressedWidget = TileAt(pressed);
            // edit: where every line shows, for the lines to glide from to their new places — where
            // they showed when the carried one was let go, if that is what this is — and a line
            // carried meanwhile is lifted again where the pointer has it (FolderFlyout.Drag.cs).
            var before = editing ? dropped ?? Take() : null;
            dropped = null;
            var carriedAt = Carried() is not null ? dragPointer : null;
            Settled();
            // The keys stay on the line they were on: one Ctrl+arrow moved is where they move it from next.
            // Back from a page, they are on the line it was of, as Windows' own pages give them back.
            var focusedWidget = focused < AddTarget ? TileAt(focused) : null;
            if (pageOf is { } back && island.PageOf is null && island.Tiles.FirstOrDefault(tile => string.Equals(tile.Island.Key, back, StringComparison.OrdinalIgnoreCase)) is { } line)
            {
                focusedWidget = line.Island;
            }
            // §489: a page opened in a column (+1) or closed (−1): its lines come in by the one motion's swap.
            var paged = !editing && !string.Equals(pageOf, island.PageOf, StringComparison.Ordinal) ? (island.PageOf is null ? -1 : 1) : 0;
            var shownBefore = paged == 0 ? null : tiles.Select(tile => tile.Island.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
            pageOf = island.PageOf;
            tiles = island.Tiles;
            TellArranged(Dressed);
            if (IndexOf(focusedWidget) is var refocused and >= 0)
            {
                focused = refocused;
            }
            moving = IndexOf(carried);
            dragging = IndexOf(draggedOut);
            pressed = PressedAfter(pressed, pressedWidget, tiles);
            if (moving == NoTarget)
            {
                landing = null;
            }
            // A slider being dragged goes on in its card where that card is now, or lets go with it.
            if (holding is { } held)
            {
                var still = IndexOf(held.Island);
                if (still == NoTarget)
                {
                    ReleaseHeld();
                }
                else
                {
                    holding = (still, held.Island);
                }
            }
            LayOut(island, natural);
            if (shownBefore is not null)
            {
                SwapLines(shownBefore, paged);
            }
            if (carriedAt is { } pointer && moving >= 0)
            {
                Follow(pointer);
                _ = Aim(pointer, animate: false);
            }
            else
            {
                Settle(before);
            }
            // desktops: a line that started a slide is drawn on the quick tick from now on.
            ArmQuick();
            return;
        }
        // menus: the same widgets, their cells as the folder has them now (§224), each dressed in its own look as a showing
        // dresses it (§311): §560 its pattern and its outline's shape too, which DrawCards alone left as they were until
        // something laid the folder out again.
        tiles = island.Tiles;
        Draw();
        ArmQuick();
    }

    /// <summary>menus: whether <paramref name="now"/> holds the very widgets this list shows, in its order (§224).</summary>
    private bool SameWidgets(IReadOnlyList<FolderTile> now) =>
        now.Count == tiles.Count && now.Select(tile => tile.Island).SequenceEqual(tiles.Select(tile => tile.Island), ReferenceEqualityComparer.Instance);

    /// <summary>menus: the lines as this list draws them (§224).</summary>
    internal IReadOnlyList<FolderTile> Lines => tiles;

    /// <summary>Tests only: the open folder laid out again where it stands, as a change of its widgets lays it out (§357).</summary>
    internal void LayOutAgain()
    {
        if (IsOpen && folder is { } island && gfx is not null)
        {
            LayOut(island, Natural(island));
        }
    }

    /// <summary>Tests only: the composition objects line <paramref name="key"/> and column button <paramref name="button"/> are drawn in now, to tell kept from made again.</summary>
    internal (object? Line, object? Button) ElementsOf(string key, string? button = null) =>
        (lines.GetValueOrDefault(key)?.Sprite, button is not null ? buttons.GetValueOrDefault(button)?.Sprite : null);

    /// <summary>
    /// The open folder laid out afresh where it stands, for the widgets it holds now and for
    /// whether it is arranged: a new plan, the panel's new size, its visuals put where it has them and drawn.
    /// </summary>
    private void LayOut(FolderIsland island, IReadOnlyList<float>? natural)
    {
        // §357: the button the keys are on and the one whose menu stands open, by what each is: a warning
        // come beside an earlier list moves them along the buttons, never onto another one.
        var was = plan;
        var focusedAdd = was is not null && focused >= AddTarget && focused - AddTarget < was.Adds.Count ? AddKeyOf(was.Adds[focused - AddTarget]) : null;
        var sizingAdd = was is not null && sizing >= 0 && sizing < was.Adds.Count ? AddKeyOf(was.Adds[sizing]) : null;
        layoutSignature = Signature(island, natural) + ArrivingSignature;
        // §647, §649: its style as it is now, set on its page while it stands open: on its ground from the bar, its lines flat on
        // it, or cards off its face.
        _ = Rewear(island);
        // edit: lines come in and carried are in no list until they land (FolderPending.AtRest, §239).
        var next = Layout(island, Pending(null)?.AtRest, natural);
        // edit: placed as a showing places it, centred on its button: a list come or gone, wider or
        // narrower, moves the others at once to where opening the folder again puts them (the person,
        // 2026-10-12: "when adding a widget to a column the column does not immediately shift, you
        // have to reopen the layout" — the left edge held through a layout kept them where they stood).
        plan = next;
        // The host's panels where the plan has them, moved and sized in place
        // (FlyoutHost.PlacePieces), and its window left alone where its size did not change.
        host.Pieces = next.Pieces;
        // §651: attached, its columns parted by a seam across the ground.
        host.Seams = next.Seams;
        host.Core = next.Core;
        host.Reanchor(AnchorOf(island, host.Work));
        // §385: where its alignment has it now, set in its menu while it stands open.
        host.Resize(new Vector2(next.Width, next.Height), AlignOf(island));
        // edit: the slots out to the screen's edges from where the panel stands now.
        plan = next = Slotted(next);
        // What lights is what the pointer is on now, in the new layout (and the part of it, below).
        pointerAt = PointerOnPanel();
        hot = pointerAt is { } now ? TargetAt(now) : NoTarget;
        focused = focusedAdd is not null
            ? AddIndexOf(next, focusedAdd) is var keyed and >= 0 ? AddTarget + keyed : NoTarget
            : Math.Min(focused, tiles.Count - 1);
        sizing = sizingAdd is not null ? AddIndexOf(next, sizingAdd) : NoTarget;
        // **In place** (§357): every line, list and button the folder showed is kept and put where the
        // new layout has it — a line that grew or shrank (a day picked whose agenda is longer, a timer
        // started, a notification come), a widget come or gone or moved to another card, a list more,
        // edit mode's "+" buttons and a list's warning beside them. Only what is new is made, and only
        // what is gone let go of. Making them all again, the host's panels with them, is what made the
        // whole folder blink for a frame when a day was picked (the person, 2026-10-10), and the "+"
        // flicker with any other change.
        Reconcile();
        Draw();
        // The widget under the pointer told where it is on it, the one it was on that it has gone: a line
        // clicked into its page is not lit, back from it, until the pointer is on it again (§409). Not
        // while the pointer is a drag's or a held widget's.
        if (holding is null && moving < 0 && dragging < 0)
        {
            HoverOver(hot < AddTarget ? hot : NoTarget, pointerAt);
        }
    }

    /// <summary>The widget of the card at <paramref name="index"/>, or null for no card.</summary>
    private IIsland? TileAt(int index) => index >= 0 && index < tiles.Count ? tiles[index].Island : null;

    /// <summary>
    /// What a press under way went down on, after the folder was laid out again: a line is the
    /// line of the same <paramref name="widget"/> where it is now (<see cref="NoTarget"/> where it
    /// has gone, so the release clicks nothing); and an edit "+" — a place, not a thing, which a
    /// new layout may put elsewhere — lets go.
    /// </summary>
    internal static int PressedAfter(int pressed, IIsland? widget, IReadOnlyList<FolderTile> tiles)
    {
        if (pressed == NoTarget || pressed >= AddTarget)
        {
            return NoTarget;
        }
        for (var index = 0; widget is not null && index < tiles.Count; index++)
        {
            if (ReferenceEquals(tiles[index].Island, widget))
            {
                return index;
            }
        }
        return NoTarget;
    }

    /// <summary>
    /// Where the pointer is on the panel now (panel px), where the panel has seen it; null otherwise: where it
    /// last moved to on the screen, as the panel stands now — moved or grown under a pointer at rest.
    /// </summary>
    private Vector2? PointerOnPanel() =>
        host.Inside && pointerScreen is { } at && host.ToPanel(at, out var point) ? point : null;

    /// <summary>Where <paramref name="widget"/>'s card is now, or <see cref="NoTarget"/> where it has none.</summary>
    private int IndexOf(IIsland? widget)
    {
        for (var index = 0; widget is not null && index < tiles.Count; index++)
        {
            if (ReferenceEquals(tiles[index].Island, widget))
            {
                return index;
            }
        }
        return NoTarget;
    }

    /// <summary>
    /// The height of the work area the flyout was placed in, as the dock handed it to <see cref="Show"/>,
    /// in px. Not the monitor's own: the dock's leaves out the other bars on it, and a folder laid out
    /// again against more room than it opened in kept lines it opened without, and stood taller than
    /// the room it has (a line grown in its menu left nothing out).
    /// </summary>
    private float WorkHeight() => host.Work.bottom - host.Work.top;

    /// <summary>The whole room beside the bar where the flyout was placed (FlyoutPlacement.Room): what a folder with a Fill line is laid out against (§173).</summary>
    private float Room() => host.Work.bottom > host.Work.top ? host.Placement.Room(scale) : 0;

    /// <summary>The text size the folder is drawn at, which its cells grow with (§442).</summary>
    public float TextScale => Math.Clamp(look.TextScale > 0 ? look.TextScale : 1, 1, 2.25f);

    /// <summary>
    /// The most cells a line of this folder may be set to where it is open: as many as its lists' room
    /// holds (<see cref="RoomCells(float, float, float, float)"/>; §192); <see cref="LineHeight.Most"/> where it
    /// has not been placed.
    /// </summary>
    public int RoomCells()
    {
        var workHeight = WorkHeight();
        return workHeight > 0 ? RoomCells(workHeight, Room(), scale, TextScale) : LineHeight.Most;
    }

    /// <summary>
    /// How tall each line's content is, measured by the widgets that can say
    /// (<see cref="IListContent"/>), in px at this scale and at the width the list draws its lines;
    /// 0 for a widget that cannot, which keeps the line at what its kind declares.
    /// <para>
    /// Measured here, by the open folder, because this is what has both the widgets and a device;
    /// <see cref="Build"/> takes neither on purpose and is handed the answer. The colours a card
    /// would be drawn in play no part in a height, so the theme is the look's own and not the
    /// host's — which is not made until the panel it would size has been planned.
    /// </para>
    /// </summary>
    private IReadOnlyList<float>? Natural(FolderIsland island) =>
        Natural(island.Tiles, island.Laid, scale, look);

    /// <summary>
    /// list: the content heights of <paramref name="tiles"/> in a list of <paramref name="options"/>
    /// at <paramref name="scale"/> — what the open folder hands <see cref="Build"/>, and what a
    /// picture hands it, so the two measure the same way: each line at its list's width
    /// (<see cref="ListWidths"/>).
    /// </summary>
    internal static float[] Natural(IReadOnlyList<FolderTile> tiles, FolderOptions options, float scale, Look look)
    {
        var style = StyleFor(scale, look, FlyoutTheme.For(look));
        var heights = new float[tiles.Count];
        var widths = ListWidths(options, scale);
        for (var index = 0; index < tiles.Count; index++)
        {
            // Each line at its own list's width: a month in a narrow list is taller than in a wide one.
            var list = Folders.PlaceOf(options, tiles[index].Cell)?.List ?? 0;
            var width = widths[Math.Clamp(list, 0, widths.Length - 1)];
            // A line not set up yet is its one Status row (§388), whatever its kind measures.
            heights[index] = tiles[index].Island is CellIsland { Unset: not null } ? LineRows.Px(1, style)
                : tiles[index].Island is IListContent content ? MathF.Max(0, content.ListHeight(width, style)) : 0;
            // §432: a sized line draws its form in what was measured of it, at the top of its whole rows.
            if (tiles[index].Island is CellIsland cell)
            {
                cell.FormMeasured = heights[index];
            }
        }
        return heights;
    }

    /// <summary>
    /// list: how wide a list of a folder and its lines are, in px, at <paramref name="width"/>
    /// (<see cref="FolderListWidths"/>; medium, every panel's width, unless somebody chose). One
    /// answer, asked by the plan that places the lines and by whatever measures what goes in them,
    /// so the two cannot be a pixel apart.
    /// </summary>
    internal static float ListWidth(float scale, FolderListWidth width = FolderListWidth.Medium) =>
        PopupPaint.Round(FolderListWidths.Dip(width) * scale);

    /// <summary>
    /// How wide each list of <paramref name="options"/> is drawn, in px at <paramref name="scale"/>:
    /// its preset (<see cref="FolderOptions.WidthOf"/>), whatever the display. One answer, asked by
    /// the plan and by whatever measures the lines, so the two cannot be a pixel apart. A folder wider
    /// than its display is never narrowed to fit it (§320): what does not fit runs past the screen's
    /// edge, and arranged the folder says so (<see cref="OffScreen"/>).
    /// </summary>
    internal static float[] ListWidths(FolderOptions options, float scale)
    {
        var lists = Math.Max(1, options.Lists?.Count ?? 0);
        var set = new float[lists];
        for (var list = 0; list < lists; list++)
        {
            set[list] = ListWidth(scale, options.WidthOf(list));
        }
        return set;
    }

    /// <summary>
    /// The one layout of the open folder (docs\widgets.md §239): <see cref="Build"/> for the folder as
    /// <paramref name="island"/> lays it out, its lines as they are now, in the room it was placed in,
    /// with <paramref name="pending"/> — nothing, the lines come in and not landed yet, or a carry's
    /// landing. What it stands by at rest, what the lines give way to, and what a landing is read in.
    /// </summary>
    private FolderPlan Layout(FolderIsland island, FolderPending? pending, IReadOnlyList<float>? natural)
    {
        var workHeight = WorkHeight();
        return Build(
            look,
            scale,
            island.View,
            island.Laid,
            tiles.Select(tile => tile.Cell).ToList(),
            workHeight > 0 ? workHeight : (plan?.Height ?? 0) / 0.7f,
            Dressed,
            natural,
            shownEdge,
            Room(),
            pending,
            island.Resting,
            Attached(island));
    }

    /// <summary>What decides the lists' layout: their width, and each widget's kind, list, place and height.</summary>
    private static string Signature(FolderIsland island, IReadOnlyList<float>? natural) =>
        // Everything Build reads. A setting left out of this is a setting that changes the file and
        // nothing on screen, because the open folder decides by this whether to plan again.
        $"{island.View}|{string.Join(",", Enumerable.Range(0, island.Laid.Lists.Count).Select(island.Laid.WidthOf))}|"
        // §385: where it stands along the bar, which places it again.
        + $"{island.Options.Align}|"
        // §647: whether it stands on its ground from the bar, which places it again.
        + $"{island.Options.Style}|"
        // list: what each line is given is its content held to its height, so both are here — the
        // content, measured (a notification arriving is a line that grew), in whole px so a
        // fraction of a pixel of text is not news; and the person's height for it, or a height
        // chosen on the widget's page would change the file and leave the open folder as it was.
        + string.Join(";", island.Tiles.Select((tile, index) =>
            $"{tile.Island.Key}:{Folders.PlaceOf(island.Laid, tile.Cell)},"
            + $"{(natural is { } measured && index < measured.Count ? MathF.Round(measured[index]) : 0)},"
            + $"{(tile.Cell.Island is { } line ? LineHeight.For(line) : default)},"
            // §487: a divider's own Style (§435), which Build reads (StyleOf, Takes): Grow takes the folder's room, the
            // folders' look a gap. Left out, a divider set to Grow or back while the folder stood open changed the file and
            // nothing on screen until something else laid it out.
            + $"{Grows(tile.Cell)},"
            // §327: a line gone at rest, or back, is a line come or gone.
            + $"{tile.Island is CellIsland { HiddenAtRest: true }}"));

    // --- Ticking while open --------------------------------------------------------

    /// <summary>The next tick: in a second with a seconds clock, else at the next minute, or sooner for a widget that wants it.</summary>
    private void Arm()
    {
        if (!IsOpen || folder is not { } island || Hwnd == HWND.NULL)
        {
            return;
        }
        var due = island.Seconds ? Clock.UntilNextSecond() : Clock.UntilNextMinute();
        if (island.Interval is > 0 and var interval && (uint)interval < due)
        {
            due = (uint)interval;
        }
        _ = SetTimer(Hwnd, TickTimer, Math.Max(due, 250u), null);
        ArmQuick();
    }

    /// <summary>
    /// calendar: whether the quick tick runs, and so whether a stopwatch counts in it now: its tenths
    /// move only while the folder is open and it runs. For tests.
    /// </summary>
    internal bool Quick { get; private set; }

    /// <summary>
    /// calendar: the quick tick, armed while a stopwatch in the open folder counts and killed when
    /// none does. Apart from the folder's own tick, which draws every line, and never under its
    /// quarter of a second: a tenth draws the counting lines alone (<see cref="QuickTick"/>).
    /// </summary>
    internal void ArmQuick()
    {
        // camera: and while a line's picture moves (§331), drawn only where a newer one came.
        var counting = IsOpen && Hwnd != HWND.NULL && tiles.Any(tile => Quickens(tile.Island) || tile.Island is CameraLine { Streaming: true });
        if (counting)
        {
            // desktops: a row sliding into its place is drawn at a display's pace, not a tenth's.
            _ = SetTimer(Hwnd, QuickTimer, tiles.Any(tile => tile.Island is CellIsland { Animating: true }) ? SlideMs : QuickMs, null);
        }
        else if (Quick && Hwnd != HWND.NULL)
        {
            _ = KillTimer(Hwnd, QuickTimer);
            // The tenth it stopped on, wherever it was stopped from.
            DrawCards();
        }
        Quick = counting;
    }

    /// <summary>A tenth: each line whose stopwatch counts is drawn again, and nothing else of the folder.</summary>
    private void QuickTick()
    {
        if (!IsOpen || plan is not { } shown || gfx is null)
        {
            ArmQuick();
            return;
        }
        for (var index = 0; index < tiles.Count; index++)
        {
            // §424: a part on the compositor moves by itself; the line is drawn only where it says so (a heading turned).
            if (tiles[index].Island is IStripLine sliding)
            {
                if (TryStrip(sliding))
                {
                    DrawCard(shown, index);
                }
                continue;
            }
            if (Quickens(tiles[index].Island) || tiles[index].Island is CameraLine { Streaming: true, Fresh: true })
            {
                DrawCard(shown, index);
            }
        }
        ArmQuick();
    }

    /// <summary>§424: whether a line with a part on the compositor must be drawn again now (IStripLine.Slid); one that throws is not.</summary>
    private static bool TryStrip(IStripLine sliding)
    {
        try
        {
            return sliding.Slid();
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Console.Error.WriteLine($"Karpo: a folder's sliding line failed: {error.Message}");
            return false;
        }
    }

    /// <summary>Whether <paramref name="widget"/> is drawn on the quick tick now: a stopwatch counting, a row sliding.</summary>
    private static bool Quickens(IIsland widget) => widget is StopwatchTile { Counting: true } or CellIsland { Animating: true };

    /// <summary>desktops: the quick tick while a row slides, in ms: about a frame of a 60 Hz display.</summary>
    internal const uint SlideMs = 16;

    /// <summary>desktops: Escape for the line holding the pointer, which puts a row it is dragging back; true where it took it.</summary>
    private bool EscapeHeld()
    {
        if (holding is not { } held)
        {
            return false;
        }
        bool took;
        try
        {
            took = held.Island.EscapeOn();
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Console.Error.WriteLine($"Karpo: a folder's {held.Island.Info.Id} escape failed: {error.Message}");
            return false;
        }
        if (took)
        {
            ArmQuick();
        }
        return took;
    }

    private void Tick()
    {
        if (!IsOpen || folder is not { Gone: false } island || gfx is null)
        {
            _ = KillTimer(Hwnd, TickTimer);
            return;
        }
        var now = Clock.Real().Ticks / TimeSpan.TicksPerMinute;
        if (now != minute)
        {
            minute = now;
            island.MinuteWidgets(gfx);
        }
        island.TickWidgets(gfx);
        DrawCards();
        Arm();
    }

    /// <summary>
    /// The showing ended; the widgets stop ticking. §357: what the folder made is kept for its next showing
    /// (FolderFlyout.Retained.cs), out of sight once its host's fade has run; where it was left is kept as
    /// <see cref="Left"/>.
    /// </summary>
    private void Closed()
    {
        stopsKept.Clear();
        // quick: a widget still holding the pointer (a slider mid-drag) gets it back first.
        ReleaseHeld();
        HideTip();
        note = "";
        ShowNote();
        // §357: where it was left, read before the widgets forget it as the folder closes.
        if (Remembered() is { } memory)
        {
            Left = memory;
        }
        // A line lifted goes back into its list: its card and content are the line's, kept; and nothing
        // kept is left gliding into the next showing.
        Unlift();
        StillKept();
        if (Hwnd != HWND.NULL)
        {
            _ = KillTimer(Hwnd, TickTimer);
            _ = KillTimer(Hwnd, QuickTimer);
            StopGlide();
            Quick = false;
            if (GetCapture() == Hwnd)
            {
                _ = ReleaseCapture();
            }
        }
        if (folder is { } island)
        {
            island.Changed -= FolderChanged;
            island.Closed();
        }
        folder = null;
        plan = null;
        TellArranged(false);
        tiles = [];
        pageOf = null;
        pointerScreen = null;
        Gone(hovering);
        hovering = null;
        hot = pressed = focused = NoTarget;
        banding = false;
        sizing = NoTarget;
        WarningShown = default;
        moving = NoTarget;
        carrying = false;
        arriving = [];
        home = null;
        landing = null;
        ForgetDrag();
        dropped = null;
    }

    private static Vector2 Centre(D2D_RECT_F rect) => new((rect.left + rect.right) / 2, (rect.top + rect.bottom) / 2);

    private static bool Inside(D2D_RECT_F rect, Vector2 point) =>
        point.X >= rect.left && point.X < rect.right && point.Y >= rect.top && point.Y < rect.bottom;

    /// <summary><paramref name="over"/> laid over <paramref name="under"/>, as the compositor would blend them: a line's card lit (here), a plate at rest lit (Scene, §506).</summary>
    internal static Color Blend(Color under, Color over)
    {
        float a = over.A / 255f;
        float b = under.A / 255f;
        var alpha = a + b * (1 - a);
        if (alpha <= 0)
        {
            return Color.FromArgb(0, 0, 0, 0);
        }
        byte Mix(byte top, byte bottom) => (byte)MathF.Round((top * a + bottom * b * (1 - a)) / alpha);
        return Color.FromArgb((byte)MathF.Round(alpha * 255), Mix(over.R, under.R), Mix(over.G, under.G), Mix(over.B, under.B));
    }

    // Composition calls on a popup mid-teardown fail harmlessly.
    private static void Try(Action change)
    {
        try
        {
            change();
        }
        catch (Exception)
        {
        }
    }
}
