// edit: several widgets selected, and what is done to them together (the person, 2026-10-07).
//
//  - **Selecting.** A click selects a widget alone, Ctrl+click adds it or takes it out, Shift+click
//    selects the run from the widget last clicked (EditSelection.cs). A drag on the dim, or from a
//    bar's or a place's empty room (2026-10-12: "the lasso select should also work inside the
//    taskbar"; EditClicks.OnPress), draws Windows' selection rectangle — the accent, faint, edged in
//    the accent — and every widget of that display's bars and places it runs over is selected as it
//    goes; with Ctrl or Shift held, what was selected stays besides (EditBand). While a folder stands
//    open arranged, a drag begun anywhere — its own empty room, a bar's, any display's dim — selects
//    its lines alone, never a bar's widget; with none open, the bars' widgets alone (the person,
//    2026-10-12; EditScope, which the selection keeps to throughout). A click on any of them only
//    clears the selection.
//    Each selected widget wears the selected look (Scene, EditView.AlsoSelected; a folder's lines
//    FolderFlyout.AlsoPicked).
//  - **Dragging them together.** A press on one of several selected widgets of a display and a drag
//    carry all of that display's selected widgets: each goes with the pointer in the carry window, side
//    by side in their order, the one taken hold of under the pointer (Dock.EditCarry.cs); every surface
//    they came from closes up behind them; the surface nearest the pointer opens the room for all of
//    them and shows one blue landing round them all (Layout.Without, Layout.Landing with a group;
//    GroupOffer). Let go, they land there side by side, in their order (IslandEdits.MoveGroup).
//    Escape or a right press puts them back. A click without a drag is a click on the one pressed.
//  - **Removing them.** Delete, and a selected widget's "Remove", remove every selected widget, asked
//    first in Windows' dialog (FolderDeletes); one widget alone goes without a question (§253).
//  - **Cut.** Ctrl+X, and a widget's "Cut", copy as Ctrl+C does and take out as Delete does, in one
//    change, asking nothing: Ctrl+V puts them down again.
//  - **Copy and paste.** Ctrl+C copies the selected widgets (Karpo's own, never Windows' clipboard,
//    which edit mode leaves alone), and a widget's "Copy" puts the copy it hands the pointer there too
//    (Dock.EditCopy.cs); Ctrl+V puts copies of them, each with a new id, into the display under the
//    pointer and lets them follow the pointer, drawn as they are, as a widget from "Add a widget" does
//    (Dock.EditFollow.cs, EditFollowHook): a left click puts them down where the landing shows, Escape
//    or a right click takes them out again. A folder's line copied goes into the folder standing open
//    arranged. There is no "Paste" in a menu (the person, 2026-10-11).
//  - **A bar's empty room.** A click there clears the selection, as a click on the dim does.

using System.Numerics;
using System.Runtime.InteropServices;
using Karpo.Core;
using TerraFX.Interop.Windows;
using Windows.UI.Composition;
using static TerraFX.Interop.Windows.Windows;
using Color = Windows.UI.Color;

namespace Karpo.Dock;

public sealed unsafe partial class Dock
{
    /// <summary>edit: the widgets selected while edit mode is on (EditSelection.cs).</summary>
    private readonly EditSelection editSelection = new();

    /// <summary>edit: the widget last clicked, which a folder standing open rings (FolderFlyout.Picked).</summary>
    private string? editPicked => editSelection.Anchor;

    /// <summary>edit: the selection shown on every surface edit mode is on, and in every folder standing open.</summary>
    private void ShowSelection()
    {
        foreach (var window in windows)
        {
            if (Arranging(window))
            {
                window.ShowEdit();
            }
        }
        foreach (var open in openFolders)
        {
            open.Flyout.Picked = editPicked;
            open.Flyout.AlsoPicked = [.. editSelection.Keys.Where(key => !string.Equals(key, editPicked, StringComparison.OrdinalIgnoreCase))];
        }
        // edit: and the one selected alone has its flyout open (SelectedFlyout.cs), and its crumb on its
        // display's edit bar (§352).
        SyncSelectedFlyout(selectionChanged: true);
        StirChrome();
    }

    /// <summary>edit: a Ctrl+click on <paramref name="key"/>: into the selection, or out of it again.</summary>
    private void EditToggle(string key)
    {
        editSelection.Toggle(key);
        ShowSelection();
    }

    /// <summary>
    /// edit: a Shift+click on <paramref name="key"/>: the run from the widget last clicked to it, in
    /// its display's order (<see cref="DisplayOrder"/>).
    /// </summary>
    private void EditRange(string key, IReadOnlyList<string>? order = null)
    {
        editSelection.Range(key, order ?? DisplayOrder(Islands.OwnerOf(key)));
        ShowSelection();
    }

    /// <summary>
    /// edit: Ctrl+A: every widget of <paramref name="order"/> — a display's bars and places, or a
    /// folder's lines — selected, the first the anchor (<see cref="EditKeys"/>).
    /// </summary>
    private void SelectAll(IReadOnlyList<string> order)
    {
        editSelection.Set(order, []);
        ShowSelection();
    }

    /// <summary>
    /// edit: a click on <paramref name="key"/> with Ctrl or Shift held, on the bar or in a folder
    /// (<see cref="EditKeys.Click"/>): into the selection or out of it, or the run from the one last
    /// clicked along <paramref name="order"/> (null: its display's). False for a plain click, which
    /// is the caller's: it selects alone and does what a click does besides.
    /// </summary>
    private bool EditClickWith(string key, IReadOnlyList<string>? order = null)
    {
        switch (EditKeys.Click(KeyState.Down(VK.VK_CONTROL), KeyState.Down(VK.VK_SHIFT)))
        {
            case EditClickPick.Toggle:
                EditToggle(key);
                return true;
            case EditClickPick.Range:
                EditRange(key, order);
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// edit: the keys of <paramref name="owner"/>'s widgets on its bars and places, in its list's
    /// order: every edge's places, each in its sections' order.
    /// </summary>
    private List<string> DisplayOrder(string? owner) =>
        [.. config.ListFor(owner).Where(island => island is not null).Select(island => Config.IslandKey(owner, island.Id))];

    /// <summary>
    /// edit: the parts of <paramref name="layout"/> selected besides the widget last clicked, which
    /// wears its ring as the one selected (<see cref="EditClicks.SelectedPart"/>); null for none.
    /// </summary>
    private IReadOnlyCollection<int>? AlsoSelectedOn(Layout layout)
    {
        if (editSelection.Count < 2)
        {
            return null;
        }
        var parts = new List<int>();
        for (var part = 0; part < layout.Specs.Count; part++)
        {
            var key = layout.Specs[part].Key;
            if (editSelection.Contains(key) && !string.Equals(key, editPicked, StringComparison.OrdinalIgnoreCase))
            {
                parts.Add(part);
            }
        }
        return parts.Count > 0 ? parts : null;
    }

    /// <summary>
    /// edit: the keys of every selected widget, each display's in its list's order, the displays in
    /// the file's order, then the lines selected in a folder (<see cref="EditSelection.All"/>).
    /// </summary>
    private List<string> DisplaysOrder() =>
        [.. config.Screens.SelectMany(screen => DisplayOrder(Config.DisplayOwner(screen.Id)))];

    /// <summary>
    /// edit: what an act on <paramref name="key"/> acts on — Delete on the widget with the keys'
    /// focus, "Remove" or "Copy" on the one whose menu it is (<see cref="EditSelection.ActedOn"/>).
    /// One done in the open folder <paramref name="from"/> acts on its lines alone (EditScope).
    /// </summary>
    private List<string> ActedOn(string? key, OpenFolder? from = null) =>
        [.. editSelection.ActedOn(key, DisplaysOrder()).Where(each => from is null || EditScope.In(each, from.Key))];

    /// <summary>
    /// edit: the selection's acts (<see cref="EditKeys.OfTheSelection"/>), one way whatever asks: the keys of a bar
    /// (<see cref="EditKey"/>) or of a folder standing open arranged (FolderFlyoutAction.Selection), a menu's "Cut" and
    /// "Remove", and a key a menu over them passes on (<see cref="PassedFromMenu"/>). <paramref name="on"/> is the widget it is
    /// on — the menu's, the one the keys are on (<see cref="EditSelection.KeysOn"/>) — which acts on every selected widget
    /// where it is one of them (<see cref="ActedOn"/>); <paramref name="from"/> the folder whose lines alone it acts on.
    /// Ctrl+A selects <paramref name="window"/>'s display's widgets, or the folder's lines. False where nothing was done.
    /// </summary>
    private bool SelectionAct(EditKeyAct act, DockWindow window, string? on, OpenFolder? from = null)
    {
        switch (act)
        {
            case EditKeyAct.Remove:
                return RemoveSelected(window, on, from);
            case EditKeyAct.Cut:
                return CutSelected(on, from);
            case EditKeyAct.Copy:
                return CopySelected(on, from);
            case EditKeyAct.Paste:
                Paste();
                return true;
            case EditKeyAct.SelectAll:
                SelectAll(from is not null ? from.Flyout.LineKeys : DisplayOrder(Config.DisplayOwner(window.Screen.Id)));
                return true;
            case EditKeyAct.Undo:
                EditUndo();
                return true;
            case EditKeyAct.Redo:
                EditRedo();
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// edit: whether a menu open in edit mode hands <paramref name="key"/> on (Flyout.PassesKey): the selection's shortcuts
    /// (<see cref="EditKeys.OfTheSelection"/>), which a menu without a row for them kept, and did nothing with (the person,
    /// 2026-11-03: "when the context menu is open i should still be able to perform the same keyboard shortcuts like cut
    /// copy etc."). None at rest.
    /// </summary>
    private bool PassedFromMenu(int key) =>
        editor is not null && EditKeys.OfTheSelection(EditKeys.Of(key, KeyState.Down(VK.VK_CONTROL), KeyState.Down(VK.VK_SHIFT), repeat: false).Act);

    /// <summary>
    /// edit: <paramref name="key"/>, handed on by a menu that closed for it (<see cref="PassedFromMenu"/>), where it goes with
    /// no menu open: the folder standing open arranged, which has the keys while it is; else the bar the keys are on
    /// (<see cref="Keyed"/>). So it acts on the menu's widget, which its menu selected, and on those selected with it.
    /// </summary>
    private void KeyFromMenu(int key)
    {
        if (editor is null)
        {
            return;
        }
        if (ArrangedFolder() is { } open)
        {
            var happened = open.Flyout.KeyPassed(key);
            if (happened.Kind != FolderFlyoutAction.None)
            {
                FolderClosed(open, happened);
            }
            return;
        }
        if (Keyed is { } bar)
        {
            _ = EditKey(bar, key);
        }
    }

    // --- Removing ------------------------------------------------------------------------------

    /// <summary>
    /// edit: Delete, or "Remove" on <paramref name="key"/>: every widget it acts on (<see cref="ActedOn"/>)
    /// comes out of wherever it is, the bar's or a folder's, in one change. The keys' focus moves to
    /// the neighbour of the one it was on (<see cref="DockWindow.Removed"/>). False when none was there.
    /// The menu's "Remove" and Delete alike ask first only where several go, or a folder loses widgets (§253).
    /// </summary>
    private bool RemoveSelected(DockWindow window, string? key, OpenFolder? from = null)
    {
        var keys = ActedOn(key, from);
        if (keys.Count == 0)
        {
            return false;
        }
        // Several widgets, or a folder with widgets in it, ask first, in Windows' own dialog over the
        // bar; one widget or an empty folder goes at once (FolderDeletes, §253). The dialog's loop
        // runs the dock meanwhile: what it was asked about is read again after, and nothing goes
        // where edit mode ended under it.
        if (DeleteQuestionFor(config, keys) is { } question)
        {
            var editing = editor is not null;
            if (!DeleteAsk.Ask(window.Hwnd, question) || disposed || editing && editor is null)
            {
                return true;
            }
        }
        var next = config.Clone();
        var removed = RemoveFrom(next, keys);
        if (removed.Count == 0)
        {
            return false;
        }
        foreach (var each in removed)
        {
            foreach (var surface in windows)
            {
                surface.Removed(each);
            }
        }
        Apply(next);
        editSelection.Keep(each => config.FindIsland(Islands.OwnerOf(each), Islands.IdOf(each)) is not null);
        ShowSelection();
        return true;
    }

    /// <summary>
    /// edit: <paramref name="keys"/> taken out of <paramref name="config"/>, each from wherever it is,
    /// the bar's or a folder's; the keys of those that were there.
    /// </summary>
    internal static List<string> RemoveFrom(Config config, IEnumerable<string> keys) =>
        [.. keys.Where(each => Folders.Remove(config.ListFor(Islands.OwnerOf(each)), Islands.IdOf(each)) is not null)];

    /// <summary>
    /// edit: what removing <paramref name="keys"/> from <paramref name="config"/> asks first, or null
    /// where it goes without a question: one widget, an empty folder (<see cref="FolderDeletes.ForRemoving"/>).
    /// </summary>
    internal static DeleteQuestion? DeleteQuestionFor(Config config, IEnumerable<string> keys) =>
        FolderDeletes.ForRemoving([.. keys.Select(each => config.FindIsland(Islands.OwnerOf(each), Islands.IdOf(each)))]);

    /// <summary>edit: asks whether a delete that loses widgets goes ahead: Windows' task dialog, a test's answer.</summary>
    internal IDeleteAsk DeleteAsk { get; set; } = new TaskDialogs();

    // --- Copy and paste --------------------------------------------------------------------------

    /// <summary>edit: the widgets Ctrl+C or a menu's "Copy" took, as they were then; Karpo's own, never Windows' clipboard.</summary>
    private readonly List<BarItem> editClipboard = [];

    /// <summary>
    /// edit: whether what is on the clipboard (<see cref="editClipboard"/>, <see cref="lineClipboard"/>)
    /// was cut, not copied: Ctrl+V moves it then, its ids kept while nothing has them (<see cref="Cut"/>).
    /// </summary>
    private bool clipboardCut;

    /// <summary>Whether a bar's widgets were copied, which Ctrl+V puts on the bar.</summary>
    private bool CanPaste => editClipboard.Count > 0;

    /// <summary>
    /// edit: Ctrl+C on <paramref name="key"/>: the widgets it acts on (<see cref="ActedOn"/>) are
    /// copied as they are now — a bar's entries, a folder's widget with its folder — or, where it acts
    /// on one line of a folder alone, that line (Dock.EditCopy.cs). False where there was nothing to copy.
    /// </summary>
    private bool CopySelected(string? key, OpenFolder? from = null)
    {
        var keys = Clipped(config, ActedOn(key, from));
        if (keys.Count == 0)
        {
            return false;
        }
        Clip(keys, keys.Select(each => config.FindIsland(Islands.OwnerOf(each), Islands.IdOf(each))));
        return true;
    }

    /// <summary>
    /// edit: which of <paramref name="keys"/> a copy takes, in their order: the bar's widgets where
    /// there are any among them; else every line, of whichever folders (<see cref="Clip"/>). None for
    /// widgets the file has lost.
    /// </summary>
    internal static List<string> Clipped(Config config, IReadOnlyList<string> keys)
    {
        var acted = keys.Select(each => config.FindIsland(Islands.OwnerOf(each), Islands.IdOf(each))).ToList();
        var bar = acted.Exists(island => island is BarItem);
        return [.. keys.Where((_, at) => bar ? acted[at] is BarItem : acted[at] is CellItem)];
    }

    /// <summary>
    /// edit: Ctrl+X on <paramref name="key"/>: what Ctrl+C would copy of what it acts on
    /// (<see cref="Clipped"/>) goes on edit mode's clipboard as it is now and comes out of wherever it
    /// is, in one change (<see cref="Cut"/>). False where there was nothing to cut.
    /// </summary>
    private bool CutSelected(string? key, OpenFolder? from = null) => Cut(Clipped(config, ActedOn(key, from)));

    /// <summary>
    /// edit: <paramref name="keys"/> cut: on the clipboard as Ctrl+C puts them there, then out of the
    /// file in one change, as Delete takes them — but asking nothing, as Explorer's Cut asks nothing:
    /// Ctrl+V puts them down again, moved: with their own ids the first time, so a timer running in
    /// one, or a stopwatch, goes on running (<see cref="BarItem.Pasted"/>; the countdowns
    /// keep theirs meanwhile, <see cref="Countdowns.Carry"/>), and as copies after that. No marching
    /// ants: what they would mark is gone.
    /// </summary>
    private bool Cut(List<string> keys)
    {
        var items = keys.Select(each => config.FindIsland(Islands.OwnerOf(each), Islands.IdOf(each))).ToList();
        if (keys.Count == 0 || !items.Exists(item => item is not null))
        {
            return false;
        }
        Clip(keys, items, cut: true);
        var next = config.Clone();
        var removed = RemoveFrom(next, keys);
        if (removed.Count == 0)
        {
            return false;
        }
        foreach (var each in removed)
        {
            foreach (var surface in windows)
            {
                surface.Removed(each);
            }
        }
        Apply(next);
        editSelection.Keep(each => config.FindIsland(Islands.OwnerOf(each), Islands.IdOf(each)) is not null);
        ShowSelection();
        return true;
    }

    /// <summary>
    /// edit: Ctrl+V: copies of what was copied, each with a new id — what was cut, moved, the first
    /// time (<see cref="Cut"/>) — go into the display under the
    /// pointer, at its place nearest the pointer, and follow the pointer, drawn as they are, until a
    /// click puts them down; Escape or a right click takes them out again (<see cref="IslandEdits.Paste"/>,
    /// <see cref="Follow"/>). A folder's line copied goes into the folder standing open arranged
    /// (<see cref="PasteLine"/>). Nothing where nothing was copied, or one is following already.
    /// </summary>
    private void Paste()
    {
        if (lineClipboard.Count > 0)
        {
            _ = PasteLine();
            return;
        }
        if (!CanPaste || follow is not null || editor is null)
        {
            return;
        }
        var at = Cursor();
        var display = Homes.Find(bar => bar.Monitor.Bounds.Left <= at.x && at.x < bar.Monitor.Bounds.Right && bar.Monitor.Bounds.Top <= at.y && at.y < bar.Monitor.Bounds.Bottom) ?? editor;
        if (NearestSlot(display.Id, at) is not { } near)
        {
            return;
        }
        var owner = Config.DisplayOwner(display.Screen.Id);
        var next = config.Clone();
        var added = IslandEdits.Paste(next.ListFor(owner), editClipboard, near.Slot, clipboardCut, next.AllIslands);
        if (added.Count == 0)
        {
            return;
        }
        // licence: all of it or nothing, as a half-pasted selection is worse than a refusal (Dock.License.cs).
        if (!LicenseAllows(next, owner, display))
        {
            return;
        }
        Apply(next);
        var keys = added.Select(island => Config.IslandKey(owner, island.Id)).ToList();
        // The copies are the selection now, as a paste selects what it put down.
        editSelection.Set(keys, []);
        ShowSelection();
        Follow(keys[0], together: keys);
    }

    // --- Carrying several together ---------------------------------------------------------------

    /// <summary>
    /// edit: several widgets carried together, from <see cref="From"/>, the window holding the mouse:
    /// their keys in their order, each as the surface showing it measured it, and every surface of the
    /// display with its own of them and itself laid out without them; where they would land now.
    /// </summary>
    private sealed class GroupDrag(DockWindow from, IReadOnlyList<string> keys)
    {
        public DockWindow From { get; } = from;

        public IReadOnlyList<string> Keys { get; } = keys;

        public List<Layout.Carried> Carried { get; } = [];

        public List<(DockWindow Window, List<int> Mine, Layout Without)> Surfaces { get; } = [];

        public DockWindow? Target { get; set; }

        public BarSlot Slot { get; set; }

        /// <summary>Whether every surface has been shown its part of it once.</summary>
        public bool Shown { get; set; }
    }

    private GroupDrag? groupDrag;

    /// <summary>
    /// edit: the widgets a press on <paramref name="key"/> carries: every selected widget of its
    /// display that a surface shows, in its list's order, where it is one of two or more of them;
    /// null where it is carried alone.
    /// </summary>
    private IReadOnlyList<string>? GroupFor(string key)
    {
        if (editSelection.Count < 2 || !editSelection.Contains(key))
        {
            return null;
        }
        var owner = Islands.OwnerOf(key);
        var keys = editSelection.InOrder(DisplayOrder(owner))
            .Where(each => windows.Exists(window => Arranging(window) && window.Layout.PartIndex(each) >= 0))
            .ToList();
        return keys.Count >= 2 ? keys : null;
    }

    /// <summary>
    /// edit: the widgets <paramref name="keys"/>, carried together from <paramref name="from"/>, are
    /// at <paramref name="at"/> on the screen: every surface closes up behind its own of them, and the
    /// one nearest the pointer — of the display it is over, any display's (§519, <see cref="NearestSurface"/>)
    /// — opens the room for all of them where they would land, with one landing round them all. The
    /// counterpart of <see cref="BarOffer"/> for one widget.
    /// </summary>
    private void GroupOffer(DockWindow from, POINT at, IReadOnlyList<string> keys)
    {
        if (groupDrag is not { } drag || drag.From != from || !drag.Keys.SequenceEqual(keys))
        {
            GroupDone();
            drag = groupDrag = new GroupDrag(from, keys);
            var surfaces = DropSurfaces(from);
            foreach (var key in keys)
            {
                if (surfaces.Find(window => window.Layout.PartIndex(key) >= 0)?.Layout.Carry(key) is { } carried)
                {
                    drag.Carried.Add(carried);
                }
            }
            foreach (var window in surfaces)
            {
                var mine = keys.Select(window.Layout.PartIndex).Where(part => part >= 0).ToList();
                drag.Surfaces.Add((window, mine, mine.Count > 0 ? window.Layout.Without(mine) : window.Layout));
            }
        }
        var nearest = NearestSurface([.. drag.Surfaces.Select(surface => surface.Window)], at);
        var target = nearest >= 0 ? drag.Surfaces[nearest].Window : null;
        // Only what changed is shown again: the surface the group went to, or its slot there.
        var moved = !drag.Shown || drag.Target != target;
        foreach (var (window, mine, without) in drag.Surfaces)
        {
            if (window == target)
            {
                var slot = without.DropSlot(window.AlongOf(at), -1);
                if (!moved && drag.Slot == slot)
                {
                    continue;
                }
                var landed = without.Landing(slot, drag.Carried);
                window.ShowGroup(mine, landed with { Lefts = window.Layout.Remaining(mine, landed.Lefts) });
                drag.Slot = slot;
                continue;
            }
            if (!moved)
            {
                continue;
            }
            if (mine.Count > 0)
            {
                var closed = without.Landing(default, []);
                window.ShowGroup(mine, closed with { Lefts = window.Layout.Remaining(mine, closed.Lefts) });
            }
            else
            {
                window.ShowGroup(null, null);
            }
        }
        drag.Target = target;
        drag.Shown = true;
    }

    /// <summary>edit: where the widgets carried together would land now: the surface and its slot; null where none would.</summary>
    private (DockWindow Window, BarSlot Slot)? GroupLanding() =>
        groupDrag is { Target: { } target } drag ? (target, drag.Slot) : null;

    /// <summary>edit: nothing is carried together any more: every surface shows its widgets where they stand.</summary>
    private void GroupDone()
    {
        if (groupDrag is not { } drag)
        {
            return;
        }
        groupDrag = null;
        foreach (var (window, _, _) in drag.Surfaces)
        {
            window.ShowGroup(null, null);
        }
    }

    /// <summary>
    /// edit: the widgets <paramref name="keys"/> carried together, each drawn at the pointer
    /// (<paramref name="pointer"/>, screen px) in the carry window, side by side along
    /// <paramref name="from"/>'s axis, the one taken hold of (<paramref name="held"/>) held at
    /// <paramref name="grabAt"/> as one carried alone is.
    /// </summary>
    private void ShowGroupCarry(DockWindow from, IReadOnlyList<string> keys, string held, Vector2 grabAt, POINT pointer)
    {
        var lifted = DropSurfaces(from)
            .SelectMany(window => window.CarriedPieces())
            .ToList();
        var pieces = new List<(Visual Source, RectF Block)>();
        var holding = 0;
        foreach (var key in keys)
        {
            if (lifted.FindIndex(piece => string.Equals(piece.Key, key, StringComparison.OrdinalIgnoreCase)) is var found and >= 0)
            {
                if (string.Equals(key, held, StringComparison.OrdinalIgnoreCase))
                {
                    holding = pieces.Count;
                }
                pieces.Add((lifted[found].Source, lifted[found].Block));
            }
        }
        if (pieces.Count == 0)
        {
            HideCarry();
            return;
        }
        var gap = MathF.Round(4f * from.UiScale, MidpointRounding.AwayFromZero);
        ShowCarry(pieces, from.Layout.Vertical, holding, gap, new POINT { x = pointer.x - (int)MathF.Round(grabAt.X), y = pointer.y - (int)MathF.Round(grabAt.Y) });
    }

    // --- The selection rectangle -------------------------------------------------------------

    /// <summary>
    /// edit: the selection rectangle a drag over a display's dim draws, on which display and over what
    /// (screen px); null for none. Its display's chrome draws it (EditChrome, §359).
    /// </summary>
    private (string DisplayId, RECT Area)? bandArea;

    /// <summary>
    /// edit: a drag on a display's dim or a bar's empty room that may select: where it began (screen
    /// px), on which display, what stays selected besides (Ctrl or Shift held), and whether it has
    /// left the drag threshold. <c>In</c>: the folder standing open it began in, whose lines alone it
    /// selects; null for the display's bars and places.
    /// </summary>
    private (POINT From, string DisplayId, List<string> Kept, bool Moving, FolderFlyout? In)? banding;

    /// <summary>
    /// edit: a left press on display <paramref name="displayId"/>'s dim or a bar's empty room at
    /// <paramref name="at"/>, or on the empty room of the folder standing open; <paramref name="add"/>
    /// with Ctrl or Shift held. Wherever it went down, a folder standing open arranged has the band:
    /// it selects that folder's lines alone, and with none open the bars' widgets alone (EditScope);
    /// what stays selected besides is of the same kind.
    /// </summary>
    private void BandPressed(string displayId, POINT at, bool add)
    {
        var open = ArrangedFolder();
        banding = (at, displayId, add ? [.. editSelection.Keys.Where(key => EditScope.In(key, open?.Key))] : [], false, open?.Flyout);
    }

    /// <summary>edit: the folder standing open arranged, whose lines alone a selection holds meanwhile (EditScope); null for none.</summary>
    private OpenFolder? ArrangedFolder() =>
        openFolders.LastOrDefault(open => open.Flyout.IsOpen && windows.Contains(open.Window) && Arranging(open.Window));

    /// <summary>
    /// edit: a folder stands open arranged, and has edit mode to itself (§212): every bar and place is
    /// locked under it, shown as it is, not dimmed (§267) — a press, a drag or a lasso on their room
    /// does nothing, and the folder stays — until its face on the bar, Escape, or a click or a right
    /// click on another widget of a bar (which then does what it does on a bar not locked, §287, §289)
    /// closes it.
    /// </summary>
    private bool BarLocked => editor is not null && ArrangedFolder() is not null;

    /// <summary>
    /// edit: the selection as a folder opening or closing leaves it (EditScope): <paramref name="open"/>
    /// opened keeps of the bar's only the folder itself, the one clicked; closed (<paramref name="opened"/>
    /// false), its lines go.
    /// </summary>
    private void ScopeSelection(OpenFolder open, bool opened)
    {
        if (editor is null || disposed)
        {
            return;
        }
        var count = editSelection.Count;
        editSelection.Keep(key => opened ? EditScope.StaysOpening(key, open.Key) : EditScope.StaysClosing(key, open.Key));
        if (editSelection.Count != count)
        {
            ShowSelection();
        }
    }

    /// <summary>
    /// edit: the pointer moved to <paramref name="at"/> with the dim's button down: past the drag
    /// threshold, the rectangle from where it went down to here, and every widget of that display it
    /// runs over selected (<see cref="DockWindow.KeysIn"/>).
    /// </summary>
    private void BandMoved(POINT at)
    {
        if (banding is not { } now)
        {
            return;
        }
        if (!now.Moving && !EditClicks.PastThreshold(at.x - now.From.x, at.y - now.From.y, GetSystemMetrics(SM.SM_CXDRAG), GetSystemMetrics(SM.SM_CYDRAG)))
        {
            return;
        }
        banding = now with { Moving = true };
        var area = new RECT
        {
            left = Math.Min(now.From.x, at.x),
            top = Math.Min(now.From.y, at.y),
            right = Math.Max(now.From.x, at.x) + 1,
            bottom = Math.Max(now.From.y, at.y) + 1,
        };
        bandArea = (now.DisplayId, area);
        StirChrome();
        var picked = now.In is { } folder ? folder.KeysIn(area)
            : windows.Where(window => window.Id == now.DisplayId && Arranging(window)).SelectMany(window => window.KeysIn(area));
        var order = now.In is { } inFolder ? inFolder.Keys : DisplayOrder(Config.DisplayOwner(BarOf(now.DisplayId)?.Screen.Id ?? now.DisplayId));
        editSelection.Set(order.Where(key => picked.Contains(key, StringComparer.OrdinalIgnoreCase)), now.Kept);
        ShowSelection();
    }

    /// <summary>edit: the dim's button came up at <paramref name="at"/>: true where it was a drag that selected, false for a click.</summary>
    private bool BandReleased(POINT at)
    {
        var was = banding;
        banding = null;
        Unband();
        // edit: one widget it selected alone has its flyout open now (Dock.FlyoutPreview.cs).
        SyncSelectedFlyout(selectionChanged: true);
        return was is { Moving: true };
    }

    /// <summary>edit: the dim lost the mouse mid-drag: the rectangle goes, what it selected stays.</summary>
    private void BandCancelled()
    {
        var was = banding;
        banding = null;
        Unband();
        if (was is { Moving: true })
        {
            SyncSelectedFlyout(selectionChanged: true);
        }
    }

    /// <summary>edit: the selection rectangle goes, with the chrome.</summary>
    private void Unband()
    {
        if (bandArea is not null)
        {
            bandArea = null;
            StirChrome();
        }
    }

    // --- A bar's empty room -----------------------------------------------------------------------

    /// <summary>
    /// edit: a click on <paramref name="window"/>'s empty room clears the selection, as a click on the
    /// dim does. How the bar shows is its layout's, on the settings app's Layouts page.
    /// </summary>
    private void EmptyClicked(DockWindow window)
    {
        if (Arranging(window) && editSelection.Count > 0 && !KeyState.Down(VK.VK_CONTROL) && !KeyState.Down(VK.VK_SHIFT))
        {
            editSelection.Clear();
            ShowSelection();
        }
    }

    private sealed partial class DockWindow
    {
        /// <summary>
        /// edit: the widgets carried together with <see cref="dragKey"/>, this window holding the mouse,
        /// their keys in their order (Dock.GroupFor); null while one is carried alone.
        /// </summary>
        private IReadOnlyList<string>? group;

        /// <summary>edit: this window's own among widgets carried together, and how it stands meanwhile (Dock.GroupOffer).</summary>
        private (List<int> Parts, Landed Landing)? groupShown;

        /// <summary>edit: where a press on this window's empty room went down (window px), whose click clears the selection; null for none.</summary>
        private Vector2? emptyPressed;

        /// <summary>edit: widgets carried together: <paramref name="parts"/>, its own of them, out of sight, the bar standing as <paramref name="landing"/> says; none (null) shows it as it stands.</summary>
        public void ShowGroup(List<int>? parts, Landed? landing)
        {
            var had = groupShown is not null;
            groupShown = parts is not null && landing is not null ? (parts, landing) : null;
            if ((had || groupShown is not null) && Editing)
            {
                scene.Edit(EditView());
            }
        }

        /// <summary>edit: this window's own widgets carried together, lifted, for the carry window (Scene.CarriedGroup).</summary>
        public IReadOnlyList<(string Key, Visual Source, RectF Block)> CarriedPieces() => groupShown is null ? [] : scene.CarriedGroup();

        /// <summary>edit: where <paramref name="at"/> (screen px) is along this window's bar, in the layout's own px.</summary>
        public float AlongOf(POINT at) => AlongAt(at);

        /// <summary>
        /// edit: the keys of the widgets whose blocks <paramref name="area"/> (screen px) runs over: what a
        /// drag on the dim selects. None on a window out of sight or an anchor.
        /// </summary>
        public IEnumerable<string> KeysIn(RECT area)
        {
            if (!Shown || IsAnchor)
            {
                yield break;
            }
            var origin = new Vector2(layout.X, layout.Y);
            for (var part = 0; part < layout.Specs.Count; part++)
            {
                if (!layout.Placed(part))
                {
                    continue;
                }
                var block = layout.BlockRect(part);
                var left = origin.X + block.X;
                var top = origin.Y + block.Y;
                if (EditClicks.Overlaps(area, left, top, left + block.Width, top + block.Height))
                {
                    yield return layout.Specs[part].Key;
                }
            }
        }

        /// <summary>edit: nothing carried together from here any more; every surface shows its widgets where they stand.</summary>
        private void EndGroup()
        {
            if (group is null)
            {
                return;
            }
            group = null;
            dock.GroupDone();
        }
    }
}

/// <summary>
/// edit: the selection rectangle a drag on the dim draws (see the top of the file), as Windows
/// draws its own on the desktop and in Explorer: the accent, faint, edged in the accent. A window of
/// its own over everything — over the bars and places it runs across — that takes no activation and
/// no pointer: the dim holds the mouse throughout.
/// </summary>
internal sealed unsafe class EditBand : IDisposable
{
    private readonly PointerWindow window;
    private readonly SpriteVisual fill;
    private readonly ShapeVisual edge;
    private readonly CompositionRoundedRectangleGeometry line;
    private readonly CompositionSpriteShape stroke;
    private readonly CompositionColorBrush fillBrush;
    private readonly CompositionColorBrush strokeBrush;

    /// <param name="title">The window's title, never shown (§534: the same rectangle shows where a bar carried lands).</param>
    public EditBand(Gfx gfx, HINSTANCE instance, string title = "Karpo selection") // not-ui: a window's title
    {
        window = new PointerWindow(gfx, instance, title);
        var c = gfx.Compositor;
        fillBrush = c.CreateColorBrush();
        fill = c.CreateSpriteVisual();
        fill.Brush = fillBrush;
        window.Content.Children.InsertAtTop(fill);
        strokeBrush = c.CreateColorBrush();
        line = c.CreateRoundedRectangleGeometry();
        stroke = c.CreateSpriteShape(line);
        stroke.StrokeBrush = strokeBrush;
        edge = c.CreateShapeVisual();
        edge.Shapes.Add(stroke);
        window.Content.Children.InsertAtTop(edge);
    }

    public HWND Hwnd => window.Hwnd;

    /// <summary>The window under the pointer, for a test.</summary>
    internal PointerWindow Window => window;

    /// <summary>
    /// The rectangle over <paramref name="area"/> (screen px), in <paramref name="accent"/>, its edge one hairline at
    /// <paramref name="scale"/>: grown, shrunk and moved in the frame's commit, with what it selects (§363).
    /// </summary>
    public void Show(RECT area, Color accent, float scale)
    {
        var size = new Vector2(Math.Max(1, area.right - area.left), Math.Max(1, area.bottom - area.top));
        var hair = MathF.Max(1f, MathF.Floor(scale));
        fillBrush.Color = Color.FromArgb(0x40, accent.R, accent.G, accent.B);
        strokeBrush.Color = accent;
        fill.Size = size;
        edge.Size = size;
        line.Offset = new Vector2(hair / 2f, hair / 2f);
        line.Size = new Vector2(MathF.Max(0f, size.X - hair), MathF.Max(0f, size.Y - hair));
        stroke.StrokeThickness = hair;
        window.Show(area with { right = area.left + (int)size.X, bottom = area.top + (int)size.Y });
    }

    public void Hide() => window.Hide();

    public void Dispose() => window.Dispose(() =>
    {
        stroke.Dispose();
        line.Dispose();
        edge.Dispose();
        strokeBrush.Dispose();
        fill.Dispose();
        fillBrush.Dispose();
    });
}
