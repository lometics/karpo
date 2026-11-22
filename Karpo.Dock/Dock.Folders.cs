// The dock's part of folders and of the edit mode's "Add".
//
// Folders (FolderIsland on the bar, FolderFlyout for its list): opening a
// folder's list at its cell, routing its window's messages, and what closing it
// means: a widget's click that asked for items, a view or a panel is shown at
// the folder's cell (the folder hands the action over, FolderIsland.Forward).
// The folder's own settings are reached from its right-click menu, not from the
// list, and in edit mode from a click on the folder, which opens it and its page
// both (EditClicks.cs). The list follows the other islands' popups' light-dismiss rules
// (Dock.cs, "Karpo's calendar, notification list…"): closed by a press on the
// folder that opened it, it records that, so the click doesn't open it again.
// Nothing moves between the bar and a folder (Partners): a block carried over an
// open folder is refused, and the folder says why.
//
// "Add" in edit mode: the "+" on the edit bar beside every section of the bar
// (EditRail), a folder's column's "+" (§247)
// and the bar's "Add to taskbar…" all open one add list right where they were
// pressed (AddList.cs): the widgets of the library that fits there, and a pick
// adds one there at once. A line on the bar says where the widget will land
// while the list is open; it goes when the list closes.

using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using Karpo.Core;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

public sealed unsafe partial class Dock
{
    /// <summary>
    /// A folder's list standing open: its flyout, the bar and the cell it was opened from (for the
    /// auto-hide, so a press there closes it without the click opening it again, and so a widget's
    /// action shows at the right place), and the folder's key, to find it again when the config
    /// changes while the list is open. §357: a folder whose bar an arrange makes again is handed on to the
    /// window its display shows it on now (<see cref="FoldersFollow"/>): its window and its cell then.
    /// </summary>
    private sealed class OpenFolder(FolderFlyout flyout, DockWindow window, Hit hit, string key)
    {
        public FolderFlyout Flyout { get; } = flyout;

        public DockWindow Window { get; set; } = window;

        public Hit Hit { get; set; } = hit;

        public string Key { get; } = key;
    }

    /// <summary>
    /// §357: each folder's view, by its key, kept for the dock's life (<see cref="FolderViewsKept"/> at most):
    /// closed, it stays out of sight with everything it drew, so opening it again — or switching back to it —
    /// shows what it has (FolderFlyout.Retained.cs), never a window made empty.
    /// </summary>
    private readonly Dictionary<string, FolderFlyout> folderViews = new(StringComparer.OrdinalIgnoreCase);

    // §357: the folders' keys, the last opened last: the views of the ones opened longest ago go first.
    private readonly List<string> folderViewsUsed = [];

    /// <summary>§357: how many folders' views are kept at most, closed ones; past that the one opened longest ago goes.</summary>
    internal const int FolderViewsKept = 8;

    /// <summary>§357: where each folder was left in this edit session (FolderMemory.cs).</summary>
    private readonly FolderMemories folderMemories = new();

    /// <summary>§357: the view of the folder keyed <paramref name="key"/>: the one kept, else a new one, kept from now on.</summary>
    private FolderFlyout FolderView(string key)
    {
        folderViewsUsed.RemoveAll(each => string.Equals(each, key, StringComparison.OrdinalIgnoreCase));
        folderViewsUsed.Add(key);
        if (!folderViews.TryGetValue(key, out var view))
        {
            // §629: the add page shows its ghost in it through itself, wherever that moves.
            view = new FolderFlyout(gfx, instance, GCHandle.ToIntPtr(cell)) { GhostsMoved = SeeGhosts };
            folderViews[key] = view;
        }
        // The ones opened longest ago, closed, past what is kept.
        foreach (var old in folderViewsUsed.Take(Math.Max(0, folderViewsUsed.Count - FolderViewsKept)).ToList())
        {
            DropFolderView(old);
        }
        return view;
    }

    /// <summary>§357: the kept view of the folder keyed <paramref name="key"/> let go of, where it is not open.</summary>
    private void DropFolderView(string key)
    {
        if (folderViews.TryGetValue(key, out var view) && !openFolders.Exists(open => ReferenceEquals(open.Flyout, view)))
        {
            folderViews.Remove(key);
            folderViewsUsed.RemoveAll(each => string.Equals(each, key, StringComparison.OrdinalIgnoreCase));
            view.Dispose();
        }
    }

    /// <summary>§357: the views of folders no bar shows any more let go of: the folder went, or another kind of it came in its place.</summary>
    private void PruneFolderViews()
    {
        foreach (var key in folderViews.Keys.Where(key => islands.GetValueOrDefault(key) is not FolderIsland).ToList())
        {
            DropFolderView(key);
        }
    }

    /// <summary>§357: the kept view whose window is <paramref name="hwnd"/>, open or not; null for none.</summary>
    private FolderFlyout? FolderViewOf(HWND hwnd) =>
        hwnd == HWND.NULL ? null : folderViews.Values.FirstOrDefault(view => view.Hwnd == hwnd);

    /// <summary>
    /// The folder whose lists are open: <b>one at a time</b>, in edit mode as out of it (the person,
    /// 2026-10-09: "only one folder can be open at a time"), so this holds one at most. At rest it
    /// behaves as every other popup: light dismiss, and a second click on its button closes it. While
    /// its bar is edited a click elsewhere leaves it open; it closes on its own button again, another
    /// folder opening, Escape, or edit mode ending (<see cref="ShowFolder"/>).
    /// </summary>
    private readonly List<OpenFolder> openFolders = [];

    /// <summary>
    /// §576: the add page open for <paramref name="Spot"/> on <paramref name="Window"/>'s bar or in a folder of it: what its line
    /// on the bar marks, §593 where its ghost stands.
    /// </summary>
    /// <param name="Folder">folders: the open folder it adds into, or null for the bar itself.</param>
    /// <param name="Plus">The "+" that opened it (screen px), which opens it no more than it closes it (§430); null for the edit bar's Add.</param>
    private sealed record Adding(DockWindow Window, AddSpot Spot, OpenFolder? Folder, RECT? Plus = null);

    private Adding? adding;

    /// <summary>§593: what of the folder <see cref="adding"/> adds into its page keeps clear of (screen px), as it stood when the page opened.</summary>
    private RECT? addingClear;

    // --- Folders -------------------------------------------------------------

    /// <summary>
    /// The lists of the folder at <paramref name="hit"/>, at its cell — the one folder open: one
    /// already open closes first, by the same path its own button takes, so whatever it held is kept
    /// as it would be then, in edit mode as out of it. Outside edit mode whatever other popup was
    /// open closes too. This folder's own button closes it again.
    /// </summary>
    private void ShowFolder(DockWindow window, Hit hit)
    {
        // §474: one of Windows' own widgets wears a folder's faces and is no folder: nothing of it opens, arranged or not.
        if (window.IslandAt(hit) is not FolderIsland { IsFolder: true } folder)
        {
            return;
        }
        var arranging = Arranging(window);
        if (Opened(folder.Key) is { } already)
        {
            // Its own button again: closed, whichever mode this is (OneFolder).
            if (OneFolder.Closes(FolderCloser.OwnButton, arranging))
            {
                CloseFolder(already);
            }
            return;
        }
        // An empty folder has nothing to show at rest: it opens only to be arranged, where its empty
        // columns are what is added into (OneFolder.Opens; §279).
        if (!OneFolder.Opens(folder.Empty, arranging))
        {
            return;
        }
        // §446: one opening as another goes is there at once, the other going under it (PopupMotion.Of).
        var switching = openFolders.Exists(other => other.Flyout.IsOpen);
        // One folder at a time: the one open goes, in edit mode too, where nothing else closes it —
        // the edit bars as the frame leaves them, never back for a moment between the two (§359).
        foreach (var other in openFolders.ToArray())
        {
            if (OneFolder.Closes(FolderCloser.OtherFolder, arranging))
            {
                CloseFolder(other);
            }
        }
        if (!arranging)
        {
            CloseFolder();
        }
        PopupOpening(window);
        FolderFlyout? flyout = null;
        try
        {
            // §357: its view as it was kept, out of sight, with everything it drew; a new one the first time.
            flyout = FolderView(folder.Key);
            // §606: the add page's ghost of it standing open there, its click's: that very view, itself from now on.
            _ = GhostTaken(folder.Key);
            // edit: the widget last clicked wears the selected look in here too, where it is one of this
            // folder's, and the others selected theirs.
            flyout.Picked = arranging ? editPicked : null;
            flyout.AlsoPicked = arranging ? [.. editSelection.Keys.Where(key => !string.Equals(key, editPicked, StringComparison.OrdinalIgnoreCase))] : [];
            var open = new OpenFolder(flyout, window, hit, folder.Key);
            Equip(open);
            // edit: it takes the full height, its edit bar over it where it stands now (EditChrome, §329).
            flyout.Covered = arranging && chromes.TryGetValue(window.Id, out var over) ? over.Deck.Panel ?? default : default;
            // edit: and what is on the clipboard its marching ants (Dock.EditCopy.cs).
            flyout.Copied = arranging ? [.. copiedKeys] : [];
            openFolders.Add(open);
            // edit: a folder opened while its bar is edited is arranged in place (its widgets
            // carry outlines and can be dragged, its "+" buttons stand over it and beside it), and
            // stays open through the bar's presses. §357: in edit mode it opens where it was left in
            // this edit session (FolderMemory.cs). §651 attached, its ground on the wallpaper its bar is blurred over.
            flyout.Paper = window.Paper;
            flyout.Show(
                gfx,
                window.Look,
                window.UiScale,
                window.WidgetAnchor(hit),
                window.FolderWork(),
                window.Layout.Edge,
                folder,
                editing: arranging,
                display: Rect(window.Monitor.Bounds),
                recall: folderMemories.Recall(folder.Key, editing: arranging),
                motion: MotionOf(window, arranging, switching),
                dock: window.Layout.Floating);
            // edit: a selection holds the open folder's lines alone meanwhile, and the folder itself (EditScope).
            if (arranging)
            {
                ScopeSelection(open, opened: true);
            }
            // keys: opened by the keys on the bar (Dock.Keys.cs), the keys go into it.
            else if (keysOpening)
            {
                flyout.TakeKeys();
            }
            // notifications: opened to be looked at, the notifications in it count as seen, as the
            // bell's list marked them seen as it opened; opened to be arranged, nothing is (§100).
            if (!arranging)
            {
                folder.MarkSeen();
            }
        }
        catch (Exception error)
        {
            if (flyout is not null)
            {
                openFolders.RemoveAll(entry => ReferenceEquals(entry.Flyout, flyout));
                // A view that could not show is not kept.
                DropFolderView(folder.Key);
            }
            Report(error);
        }
        // edit: the display's edit bar the folder's while a folder is open (Dock.EditDeck.cs, §297).
        DecksFollowFolders();
        FacesShowOpen();
    }

    /// <summary>
    /// §446, §489: how a folder on <paramref name="window"/>'s bar comes in and goes: Karpo's one popup motion, as every
    /// popup's (<see cref="PopupMotion.Of"/>), at once with motion off, fading where it stands arranged, there at once in a
    /// switch.
    /// </summary>
    private static PopupMotion MotionOf(DockWindow window, bool arranging, bool switching) =>
        PopupMotion.Of(window.Look.Motion, PopupEntrance.Popup, arranging, switching);

    /// <summary>
    /// folders: the open folder's face lit on the bar it was opened from (<see cref="Scene.Open"/>), none on
    /// the others — the person, 2026-10-28: "the folder that is open in karpo should be highlighted in some
    /// way" (§376). After it opens, closes, or is handed on to another bar. §474: else the button of Windows' menu in front,
    /// on the bar of the display the menu is on, lit as Windows lights its own while its menu is open (<see cref="WindowsMenus.Lit"/>):
    /// one thing open at a time, so never both.
    /// </summary>
    private void FacesShowOpen()
    {
        var front = WindowsPanels.Instance.Front;
        var screen = front is { } panel && WindowsPanels.Instance.Window(panel) is { } shown ? (nint)MonitorFromWindow(shown, MONITOR.MONITOR_DEFAULTTONEAREST) : 0;
        foreach (var window in windows)
        {
            var specs = window.Layout.Specs;
            var lit = screen != 0 && window.Layout.Screen == screen ? windowsMenus.Lit(front, [.. specs.Select(spec => spec.Kind)]) : -1;
            window.Scene.Open(openFolders.Find(open => open.Window == window)?.Key ?? (lit >= 0 ? specs[lit].Key : null));
        }
    }

    /// <summary>
    /// What the open folder <paramref name="open"/> asks of the dock, by the window it stands on now
    /// (§357: an arrange may hand it on to another, <see cref="FoldersFollow"/>).
    /// </summary>
    private void Equip(OpenFolder open)
    {
        var flyout = open.Flyout;
        // edit: a line dragged in it at rest turns edit mode on, as a widget dragged on the bar does.
        flyout.ArrangeFromDrag = () => BeginEditFromFolderDrag(open);
        // §663: not on locked bars, whose lines stay where they stand at rest.
        flyout.Locked = () => config.LockTaskbar;
    }

    /// <summary>
    /// §357: after an arrange, each open folder whose bar it made again (another edge, type or way of
    /// showing; Dock.Arrange) handed on to the window its display shows it on now: shown there, where its
    /// face stands now, as it was (<see cref="FolderFlyout.Remembered"/>) — nothing of it made again, its
    /// window kept — and a menu standing over it with it, beside its line where that stands now
    /// (<see cref="MenuFollowsFolder"/>). One whose display shows it nowhere now closes, its menu with it.
    /// </summary>
    private void FoldersFollow()
    {
        var handed = false;
        foreach (var open in openFolders.ToArray())
        {
            if (windows.Contains(open.Window) || !openFolders.Contains(open))
            {
                continue;
            }
            handed = true;
            var successor = windows.Find(window => window.Id == open.Window.Id && window.Layout.PartIndex(open.Key) >= 0);
            if (successor is null || !open.Flyout.IsOpen || islands.GetValueOrDefault(open.Key) is not FolderIsland { Gone: false } folder)
            {
                CloseFolder(open);
                continue;
            }
            var hit = Hit.Cell(successor.Layout.PartIndex(open.Key), 0);
            var memory = open.Flyout.Remembered();
            open.Window = successor;
            open.Hit = hit;
            var arranging = Arranging(successor);
            try
            {
                open.Flyout.Paper = successor.Paper;
                open.Flyout.Show(
                    gfx,
                    successor.Look,
                    successor.UiScale,
                    successor.WidgetAnchor(hit),
                    successor.FolderWork(),
                    successor.Layout.Edge,
                    folder,
                    editing: arranging,
                    display: Rect(successor.Monitor.Bounds),
                    recall: memory,
                    motion: MotionOf(successor, arranging, switching: false),
                    dock: successor.Layout.Floating);
            }
            catch (Exception error)
            {
                Report(error);
                CloseFolder(open);
                continue;
            }
            if (pending is { } menu && ReferenceEquals(menu.Folder, open))
            {
                menu.Owner = successor;
                MenuFollowsFolder(menu, open);
            }
        }
        // edit: the display's edit bar over the folder where it stands now (§329); the arrange's build set the rest.
        if (handed && !disposed)
        {
            StirChrome();
            FacesShowOpen();
        }
    }

    /// <summary>
    /// §357: the open menu <paramref name="what"/>, standing beside a line of <paramref name="open"/> (edit
    /// mode's: <see cref="Pending.Beside"/>), moved beside that line where the folder shows it now; nothing
    /// where it stands beside nothing, the line is gone, or it stands where it stood.
    /// </summary>
    private void MenuFollowsFolder(Pending what, OpenFolder open)
    {
        if (flyout is not { IsOpen: true } list || what.Beside is not { } beside || open.Flyout.LineRect(what.Target) is not { } card
            || card.Equals(beside.Widget))
        {
            return;
        }
        what.Beside = beside with { Widget = card };
        list.Follow(what.Beside);
    }

    /// <summary>The open folder keyed <paramref name="key"/>, or null.</summary>
    private OpenFolder? Opened(string key) =>
        openFolders.Find(entry => string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// edit: a line pressed in the folder <paramref name="open"/> at rest has been carried past the
    /// drag threshold, so edit mode turns on for the folder's bar and the folder takes the drag on
    /// as edit mode's, the same line carried (<see cref="FolderFlyout.ArrangeFromDrag"/>) — the counterpart of
    /// <see cref="BeginEditFromDrag"/> for a widget dragged on the bar (the person, 2026-10-05: "when
    /// a widget is dragged in the grid or list view it should also automatically switch into edit
    /// mode"). True when that bar is arranged now and the folder is still open.
    /// <para>
    /// The folder stays open through it: it holds itself open before it asks
    /// (<see cref="FolderFlyout.StaysOpen"/>), and turning edit mode on closes no folder of the bar
    /// being arranged. Refused while <b>another</b> bar is arranged, whose ending would close every
    /// folder standing open, this one with it, under the pointer: the drag then carries the widget
    /// out of the folder, as it did before.
    /// </para>
    /// <para>
    /// It stays on once the drag is over, as on the bar: the person is arranging, and Escape, the
    /// rail's Close and the bar menu's Done each end it.
    /// </para>
    /// </summary>
    private bool BeginEditFromFolderDrag(OpenFolder open)
    {
        var window = open.Window;
        if (disposed || !windows.Contains(window) || !open.Flyout.IsOpen || editor is { } other && !Arranging(window))
        {
            return false;
        }
        // places: a folder open from a place along an edge hands over as one from the bar does: edit
        // mode is the display's, and the place stays in sight with its folder.
        BeginEdit(window);
        return Arranging(window) && open.Flyout.IsOpen && openFolders.Contains(open);
    }

    /// <summary>
    /// The window procedure's part for the folder list: every message of its window goes to it
    /// first, then to the default handling. False for any other window.
    /// </summary>
    private bool FolderMessage(HWND window, uint msg, WPARAM wParam, LPARAM lParam)
    {
        if (openFolders.Find(entry => entry.Flyout.Hwnd == window) is not { } open)
        {
            // §357: a folder kept out of sight still hears its own window — its closing fade's end — and
            // nothing it says is about a showing.
            if (FolderViewOf(window) is not { } kept)
            {
                return false;
            }
            _ = kept.Message(msg, wParam, lParam);
            return true;
        }
        // §430: what a press went down on: what its click opens is that line's or that "+"'s, and one of theirs the
        // hook closed for it stays closed.
        if (msg is WM.WM_LBUTTONDOWN or WM.WM_RBUTTONDOWN or WM.WM_MBUTTONDOWN)
        {
            _ = Pressing(window, open.Flyout.OpenerAt(lParam), LightDismiss.ButtonOf(msg), ScreenOf(window, lParam));
        }
        // §575: arranged, Escape from the inside out: the edit bar's flyout open (the folder's own, opened with it) closes
        // before the folder does (EditKeys.Escape); a line being moved is the folder's, first.
        if (msg == WM.WM_KEYDOWN && (int)(nuint)wParam == VK.VK_ESCAPE && editor is not null && panelMemory.Page is not null && !open.Flyout.Moving)
        {
            ClosePanel();
            return true;
        }
        var happened = open.Flyout.Message(msg, wParam, lParam);
        if (happened.Kind != FolderFlyoutAction.None)
        {
            FolderClosed(open, happened);
        }
        return true;
    }

    /// <summary>
    /// edit: whether the hit test <paramref name="lParam"/> (screen px) on <paramref name="window"/>
    /// falls through an arranged folder's window, which reaches the screen's edges for its guide lines
    /// and slots, to what stands under it — the dim, the bar, an edit bar (FolderFlyout.PassesThrough).
    /// </summary>
    private bool FolderPassesThrough(HWND window, LPARAM lParam) =>
        // §606: the add page's ghost of a folder is a kept view, open as no folder is.
        (openFolders.Find(entry => entry.Flyout.Hwnd == window)?.Flyout ?? FolderViewOf(window)) is { } open
        && open.PassesThrough(new POINT { x = (short)((nint)lParam & 0xFFFF), y = (short)(((nint)lParam >> 16) & 0xFFFF) });

    /// <summary>
    /// edit: a folder's window about to move in the z-order keeps to the arranged folder's level
    /// (FolderFlyout.Stand, §342) — from the window procedure, whatever the dock is doing: busy handling
    /// a message of its own, nothing else of the folder's is heard, and a menu closing there hands the
    /// folder the focus back, which raised it over its edit bar and its bars. Nothing for another window.
    /// </summary>
    /// <summary>Whether <paramref name="hwnd"/> is a folder list's, whose own posts are kept through a modal loop.</summary>
    private bool FolderOwns(HWND hwnd) => openFolders.Exists(entry => entry.Flyout.Hwnd == hwnd) || FolderViewOf(hwnd) is not null;

    /// <summary>A folder's list is open from <paramref name="window"/>: an auto-hiding bar stays up.</summary>
    private bool FolderOpenFor(DockWindow window) => openFolders.Exists(entry => entry.Window == window && entry.Flyout.IsOpen);

    /// <summary>
    /// The folder's list closed: the bar may hide again, and a widget's action shows at the
    /// folder's cell when that is why it closed.
    /// </summary>
    private void FolderClosed(OpenFolder opened, FolderFlyoutEvent happened)
    {
        // pins: a widget dragged out of the folder: the folder stays open until it is let go.
        if (happened.Kind is FolderFlyoutAction.DragMove or FolderFlyoutAction.DragDrop)
        {
            FolderDragged(opened, happened);
            return;
        }
        // folders: a widget's own menu, at the pointer, over the folder — which stays open behind it,
        // in edit mode and out of it (FolderMenu); a widget's "Set up", its configuration menu (§340).
        if (happened.Kind is FolderFlyoutAction.Context or FolderFlyoutAction.Setup)
        {
            FolderMenu(opened, happened);
            return;
        }
        // folders: a line's dropdown: its choices under its field, over the folder (FolderChoices).
        if (happened.Kind == FolderFlyoutAction.Choices)
        {
            FolderChoices(opened, happened);
            return;
        }

        // edit: arranging the folder on the bar leaves it open — only the file changes.
        switch (happened.Kind)
        {
            case FolderFlyoutAction.Place when happened.Widget is { } moved:
                PlaceInFolder(happened.Keys ?? [moved.Key], happened.Place);
                return;
            // edit: a column's "+", or a slot's: the add list for it, away from the bar (§246).
            case FolderFlyoutAction.Add:
                AddInFolder(opened, happened.Place, happened.Field);
                return;
            // edit: a list's width button, or a slot's: the three widths, over the folder, which stays open.
            case FolderFlyoutAction.Width:
                FolderWidthMenu(opened, happened.Place, happened.At);
                return;
            // edit: a list's warning: what it has no room for, as the edit bar's "not shown" shows it.
            case FolderFlyoutAction.NotShown:
                FolderNotShown(opened, happened.Hidden ?? [], happened.At);
                return;
            // edit: a right click on its own room: the folders' look, over the folder (§334).
            case FolderFlyoutAction.Looks:
                FolderLooksMenu(opened, happened.At);
                return;
            // edit: Delete, Ctrl+X, Ctrl+C, Ctrl+V, Ctrl+A, Ctrl+Z and Ctrl+Y in the arranged folder, as on the bar
            // (Dock.SelectionAct), on its lines alone (EditScope); the folder stays.
            case FolderFlyoutAction.Selection:
                SelectionAct(happened.Act, opened.Window, happened.Widget?.Key, opened);
                return;
            // edit: the selection rectangle from the arranged folder's empty room, its lines alone, as
            // from anywhere while it stands open (Dock.EditSelect.cs, EditScope); a click there clears
            // the selection, as on the bar. The folder stays.
            case FolderFlyoutAction.BandPressed:
                BandPressed(opened.Window.Id, happened.At, KeyState.Down(VK.VK_CONTROL) || KeyState.Down(VK.VK_SHIFT));
                return;
            case FolderFlyoutAction.BandMoved:
                BandMoved(happened.At);
                return;
            case FolderFlyoutAction.BandReleased:
                if (!BandReleased(happened.At))
                {
                    EmptyClicked(opened.Window);
                }
                return;
            case FolderFlyoutAction.BandLost:
                BandCancelled();
                return;
            // edit: a line clicked while the folder is arranged is the selected one, ringed in here
            // as a block is on the bar (EditClicks.cs); the folder stays, and nothing pops up: §520 its settings are the
            // settings panel's Widget page, which follows the selection. Enter on the line (the keys carry no point) too.
            case FolderFlyoutAction.Settings when happened.Widget is { } clicked:
                LineClicked(opened, clicked, happened);
                return;
            // edit: a part of a line's own card that edit mode edits — a sound device hidden or shown
            // again (§159): written and saved; the folder stays, the line drawn from what was saved.
            case FolderFlyoutAction.EditPart when happened.Widget is CellIsland edited:
                EditPart(edited, happened.Part);
                return;
            // edit: Space on a line, a click's (EditKeys.cs): selected.
            case FolderFlyoutAction.Select when happened.Widget is { } spaced:
                LineSelected(opened, () => EditPick(spaced.Key));
                return;

            // edit: Shift+arrows in the arranged folder, the line a size larger or smaller, as on the bar (EditResize.cs, §471).
            case FolderFlyoutAction.Resize when happened.Widget is { } sized:
                ResizeByKeys(opened.Window, sized.Key, happened.Part, opened.Flyout.RoomCells());
                return;
        }
        // clipboard: where the focus went back to, read before the folder is let go of.
        var back = opened.Flyout.Previous;
        Forget(opened);
        // A flyout, which a second click on its folder closes (PressedOn reads this).
        if (happened.Kind == FolderFlyoutAction.Dismissed)
        {
            dismissed = (new Pending(OpenedByClick, opened.Window) { Hit = opened.Hit }, GetTickCount());
        }
        var alive = !disposed && windows.Contains(opened.Window);
        if (alive && !FolderOpenFor(opened.Window))
        {
            opened.Window.FlyoutClosed();
        }
        switch (happened.Kind)
        {
            case FolderFlyoutAction.Forward when alive && happened.Widget is { } widget:
                FolderForward(opened.Window, opened.Hit, widget, happened.Index, happened.Action, back);
                break;
        }
    }

    /// <summary>
    /// folders: a right click on a widget in an open folder: that widget's own menu, at the pointer,
    /// over the folder, which stays open behind it and after it (the person: "for all widgets on
    /// right click it should not close the folder anymore"). The folder has held itself open for the
    /// menu already (FolderFlyout's right click), and is let go when the menu ends
    /// (<see cref="FolderMenuEnded"/>) — or at once, where there is no menu to show.
    /// <para>
    /// At rest a pin shows the bar's pin menu with its file's rows, and every other widget its kind's
    /// own acts (Dock.WidgetMenus.cs); either ends with its "Settings" (2026-10-05). While the bar is arranged it is
    /// those rows alone, as it is for a widget on the bar then, and it is the way a widget leaves a
    /// folder: no line carries a remove badge, and "Remove" means out of the folder. A widget in a
    /// folder is no part of a bar, so the menu is keyed on the
    /// widget's path (Dock.cs, WidgetMenu).
    /// </para>
    /// </summary>
    private void FolderMenu(OpenFolder opened, FolderFlyoutEvent happened)
    {
        if (!disposed && windows.Contains(opened.Window) && happened.Widget is { } widget)
        {
            // §340: a widget not set up: its configuration menu, at rest too, where what it needs is set — a
            // field its own row; §351: an account ⚙ › Accounts, where accounts are added (Dock.EditEntries.cs).
            if (happened.Kind == FolderFlyoutAction.Setup)
            {
                if (widget is CellIsland { Unset.Link.ToAccounts: true })
                {
                    OpenEditMenuLater(opened.Window, EditEntry.ManageAccounts);
                }
                else
                {
                    WidgetMenu(opened.Window, widget.Key, happened.At, opened.Hit, folder: opened, widget: widget, configure: true);
                }
            }
            else if (!Arranging(opened.Window) && widget is PinTile pin)
            {
                PinMenu(opened.Window, opened.Hit, pin, happened.At, opened);
            }
            // edit: the menu key's and a right click's, beside the line, as a click opens it (§212):
            // there is one menu in edit mode (§214).
            else if (Arranging(opened.Window) && happened.Field.right > happened.Field.left)
            {
                LineRightClicked(opened, widget, happened.Field);
                return;
            }
            else
            {
                WidgetMenu(opened.Window, widget.Key, happened.At, opened.Hit, folder: opened, widget: widget);
            }
        }
        // No menu came of it — a widget the file has lost has none — so the right click did
        // nothing, and the folder is as it was before it.
        if (!FlyoutOpen || !ReferenceEquals(pending?.Folder, opened))
        {
            opened.Flyout.MenuClosed();
        }
    }

    /// <summary>
    /// edit: a click on <paramref name="line"/> of <paramref name="opened"/>, arranged (§212): with Ctrl
    /// or Shift held it goes into the selection or out of it, or the run along the folder's lines is
    /// selected; else it is the one selected (Enter, no point, too). §520: nothing pops up — its settings are the settings
    /// panel's Widget page, which follows the selection. A click whose press closed the line's own (right click's) menu
    /// does only that, a double-click's second too (EditClicks.Acts).
    /// </summary>
    private void LineClicked(OpenFolder opened, IIsland line, FolderFlyoutEvent happened)
    {
        var key = line.Key;
        if (happened.At is { x: 0, y: 0 })
        {
            LineSelected(opened, () => EditPick(key));
            return;
        }
        // The press closed this line's menu, the hook's post read before the press (FlyoutHost.PointerDown).
        if (dismissed is { What: { Topic: Topic.LayoutEditor } was, At: var at } && ReferenceEquals(was.Folder, opened)
            && string.Equals(was.Target, key, StringComparison.OrdinalIgnoreCase) && GetTickCount() - at <= GetDoubleClickTime())
        {
            dismissed = null;
            opened.Flyout.MenuClosed();
            return;
        }
        // edit: Ctrl+click and Shift+click select more, as on the bar (EditKeys.Click); the run is along
        // the folder's lines.
        LineSelected(opened, () =>
        {
            if (!EditClickWith(key, opened.Flyout.LineKeys))
            {
                EditPick(key);
            }
        });
    }

    /// <summary>§520: a line of <paramref name="opened"/>, arranged, selected by <paramref name="select"/>; the folder goes back to rest.</summary>
    private void LineSelected(OpenFolder opened, Action select)
    {
        if (disposed || !windows.Contains(opened.Window) || !Arranging(opened.Window))
        {
            return;
        }
        select();
        opened.Flyout.MenuClosed();
    }

    /// <summary>
    /// edit: a right click on <paramref name="line"/> of <paramref name="opened"/>, arranged, or the
    /// menu key on it (the person, 2026-10-19: "the separate right click menus in the edit mode should
    /// be removed"): what a click does — selected, where it is not one of several selected, its one
    /// menu beside its <paramref name="card"/> (<see cref="LineMenu"/>);
    /// on the line whose menu the press closed only that, as a click there (§214).
    /// </summary>
    private void LineRightClicked(OpenFolder opened, IIsland line, RECT card)
    {
        var key = line.Key;
        var closed = dismissed is { What: { Topic: Topic.LayoutEditor } was, At: var at } && ReferenceEquals(was.Folder, opened)
            && string.Equals(was.Target, key, StringComparison.OrdinalIgnoreCase) && GetTickCount() - at <= GetDoubleClickTime();
        if (EditClicks.OnRight(onWidget: true, closed) == EditRight.None)
        {
            dismissed = null;
            opened.Flyout.MenuClosed();
            return;
        }
        LineMenu(opened, line, card, () =>
        {
            if (!editSelection.Contains(key))
            {
                EditPick(key);
            }
        });
    }

    /// <summary>
    /// edit: <paramref name="line"/> of <paramref name="opened"/>, arranged, selected by <paramref name="select"/>
    /// — alone, or with a Ctrl or Shift click more — and, where that leaves it selected, its menu over the
    /// folder beside its line <paramref name="card"/> (screen px): after it, else before it, else under or
    /// over it (<see cref="MenuSides"/>). The one path of a click on the line, Space, Enter, a right click,
    /// the menu key (§407) and an error's row (§394); Widget ▾ opens the same menu from the edit bar (§352).
    /// The folder stays open behind it.
    /// </summary>
    private void LineMenu(OpenFolder opened, IIsland line, RECT card, Action select)
    {
        if (disposed || !windows.Contains(opened.Window) || !Arranging(opened.Window))
        {
            return;
        }
        menuComing = true;
        try
        {
            select();
            if (editSelection.Contains(line.Key))
            {
                var beside = card.right > card.left && card.bottom > card.top
                    ? new MenuBeside(card, MenuSides.Order(onBar: false, opened.Window.Layout.Edge, Mirror.On))
                    : null;
                var anchor = new POINT { x = (card.left + card.right) / 2, y = (card.top + card.bottom) / 2 };
                WidgetMenu(opened.Window, line.Key, anchor, opened.Hit, unknown: line.Info.Title, folder: opened, widget: line, beside: beside);
            }
        }
        finally
        {
            menuComing = false;
        }
        if (!FlyoutOpen || !ReferenceEquals(pending?.Folder, opened))
        {
            opened.Flyout.MenuClosed();
        }
    }

    /// <summary>
    /// folders: a line's dropdown in <paramref name="opened"/> (the sound page's Output and Input): its
    /// devices as a menu under its field, its field's width, the one in use ticked (DeviceChoices), over
    /// the folder, which stays open behind it as for a line's menu. Downwards where there is room, else
    /// upwards (FlyoutPlacement.Roomiest), clear of the field either way. A pick makes the device
    /// Windows' default (RunAct), and the folder stays.
    /// </summary>
    private void FolderChoices(OpenFolder opened, FolderFlyoutEvent happened)
    {
        if (!disposed && windows.Contains(opened.Window) && happened.Widget is SoundPartTile sound && sound.Choices is { Count: > 0 } choices)
        {
            var (rows, acts) = DeviceChoices(choices);
            var (anchor, gap, width) = ChoicesPlacement(happened.Field, opened.Window.UiScale);
            var what = new Pending(Topic.LayoutEditor, opened.Window)
            {
                Target = sound.Key,
                Folder = opened,
                Acts = acts,
                Edge = Edge.Top,
                ListWidth = width,
                Gap = gap,
            };
            ShowFlyout(what, rows, FlyoutLayout.List, anchor, spoken: SoundPartTile.PickerHeadOf(sound.Region));
        }
        if (!FlyoutOpen || !ReferenceEquals(pending?.Folder, opened))
        {
            opened.Flyout.MenuClosed();
        }
    }


    /// <summary>
    /// folders: the menu that stood over <paramref name="held"/> has gone, whichever way. The folder
    /// lets go (FolderFlyout.MenuClosed) and stays open — unless another program took the foreground
    /// while the menu held it (<see cref="ForegroundTakenFromKarpo"/>), as a folder at rest closes for
    /// (<see cref="FolderAfterMenu"/>). §664: never because the foreground is not the folder itself at
    /// this moment — the menu still going, none in front, a window of Karpo's —: an in-place pick
    /// ("Refresh") closed the folder so. The press that closes the menu, on the folder or outside both,
    /// is used up for it and reaches nothing else (FlyoutHost.UsesUpPresses), so only the next one closes
    /// the folder (docs\widgets.md §185). A folder that left the bar while the menu held it (the file
    /// changed) could not close then, and closes now.
    /// </summary>
    private void FolderMenuEnded(OpenFolder held)
    {
        if (!openFolders.Exists(open => ReferenceEquals(open, held)) || !held.Flyout.IsOpen)
        {
            return;
        }
        held.Flyout.MenuClosed();
        if (held.Flyout.Folder is not { Gone: false }
            || FolderAfterMenu(Arranging(held.Window), handedAway: false, ForegroundTakenFromKarpo(GetForegroundWindow())) == AfterMenu.Closes)
        {
            CloseFolder(held);
        }
    }

    /// <summary>
    /// That folder is not open any more. Every way a folder closes comes through here. §357: its view is
    /// kept, out of sight, for its next showing (<see cref="folderViews"/>), and where it was left for the
    /// rest of the edit session (<see cref="folderMemories"/>); a menu that stood over it goes with it,
    /// kept where it stood as a menu that closes is (Dock.MenuSessions.cs).
    /// </summary>
    private void Forget(OpenFolder open)
    {
        openFolders.RemoveAll(entry => ReferenceEquals(entry, open));
        if (ReferenceEquals(pending?.Folder, open) && FlyoutOpen)
        {
            CloseFlyout();
        }
        if (open.Flyout.IsOpen)
        {
            open.Flyout.Hide();
        }
        folderMemories.Left(open.Key, open.Flyout.Left, editing: editor is not null);
        // edit: its lines are not selected once it is closed (EditScope).
        ScopeSelection(open, opened: false);
        // edit: and the edit bar's own buttons back once no folder is open, edit mode still on (Dock.EditDeck.cs),
        // as the frame ends: a folder opening in its place leaves them as they stand (§359).
        if (!disposed)
        {
            DecksFollowFolders();
            FacesShowOpen();
        }
    }

    /// <summary>
    /// What a widget's click in a folder asked for. Under the folder rule (docs\widgets.md §15)
    /// that is an app opening and nothing else, and only for a widget whose click acts
    /// (<see cref="CellAct.Action"/>): the folder forwards nothing more (FolderFlyout.Forwards),
    /// and a line has no way to ask for a popup to begin with.
    /// </summary>
    /// <param name="back">The window in front when the folder opened, which its closing gave the focus back to.</param>
    private void FolderForward(DockWindow window, Hit hit, IIsland widget, int index, IslandAction action, HWND back = default)
    {
        // pins: a pin in a folder follows the one rule for every app entry (Dock.Pins.cs).
        if (action.View == IslandView.App && widget is PinTile pin)
        {
            OpenApp(pin.Item, OpenWindowsOf(pin.Item), AppAsk.Usual);
        }
        // folder contents (§380): an item, or the folder, once the folder it stands in has gone, so what
        // opens comes up in front of where it stood. A subfolder whose navigation shows the contents never
        // gets here: it goes in in place, and the folder stays (§398).
        else if (action.View == IslandView.App && widget is FolderContentsLine contents)
        {
            contents.Open();
        }
        // The Snipping Tool's snip: once every folder is off the screen, so none is in the picture.
        // The folder clicked has been let go of already, its window going once it has faded; any
        // folder it stood in closes too. The snip waits for the screen to show them gone
        // (Surfaces.AfterPresent: their windows destroyed, then the screen composed without them).
        // rss: a headline's story, in the browser, as a pin's site opens: the folder has gone already.
        else if (action.View == IslandView.App && widget is Rss.RssIsland feed)
        {
            feed.Open(index);
        }
        // sports: the match on ESPN, in the browser, once the folder has gone (§239).
        else if (action.View == IslandView.App && widget is Sports.SportsTile sports)
        {
            sports.Open();
        }
        // pim: a message in the account's web mail, as a headline's story opens.
        else if (action.View == IslandView.App && widget is Pim.MailIsland mail)
        {
            mail.Open();
        }
        // markets, clocks: what a ticker tracks, or the time where a clock stands, searched for in the
        // browser, as a headline's story opens (§263, §377).
        else if (action.View == IslandView.App && widget is IOpensOnceGone search)
        {
            search.Open();
        }
        // quakes: an earthquake's page on the USGS's site, as a headline's story opens (§240).
        else if (action.View == IslandView.App && widget is QuakesLine quakeLine)
        {
            quakeLine.Open();
        }
        // camera: the camera's page, or its address, in the browser (§331).
        else if (action.View == IslandView.App && widget is CameraLine camera)
        {
            camera.Open();
        }
        // meeting: a meeting's Join, in its app or the browser, once the folder has gone (§205, §206).
        else if (action.View == IslandView.App && widget is IMeetingLine meeting)
        {
            meeting.Open();
        }
        // pim: a contact's Email or Call, handed to Windows' mailto: or tel: handler once the folder has gone.
        else if (action.View == IslandView.App && widget is Pim.ContactsLine contacts)
        {
            contacts.OpenChosen();
        }
        // §642: Windows search's result opened, its file's location shown, run as administrator, opened with, once the folder has gone;
        // §652 one of Karpo's own here (OpenKarpo).
        else if (action.View == IslandView.App && widget is WinSearch.WinSearchLine winSearch)
        {
            if (winSearch.OpenChosen() is { } karpo)
            {
                OpenKarpo(window, winSearch, karpo);
            }
        }
        // notifications: a notification's app brought forward with the toast's own arguments, now,
        // while the right to the foreground from the click is still Karpo's to pass on: the folder
        // has gone already (ToastActivation.cs).
        else if (action.View == IslandView.App && widget is NotificationPartTile notifications)
        {
            notifications.ActivatePending();
        }
        // clipboard: an item chosen in the clipboard's line is on the clipboard; it is pasted into the
        // window the folder gave the focus back to, once that window's focus has settled
        // (Dock.Clipboard.cs), as Win+V pastes it.
        else if (action.View == IslandView.App && widget is ClipboardLine clipboard)
        {
            if (clipboard.TakePaste())
            {
                Paste(back);
            }
        }
        // desktops: a desktop chosen in the desktops' line is switched to, the folder gone without
        // giving the focus back (FolderFlyout.Activate), as the switcher's row did (Dock.Switchers.cs).
        else if (action.View == IslandView.Desktops && widget is DesktopsLine line)
        {
            if (line.TakeSwitch() is { } desktop)
            {
                SwitchDesktop(window, desktop);
            }
        }
        // layouts: a layout chosen in the layouts' line, this display switched to it once the folder
        // has gone, as the bar's own menu switches (Dock.Switchers.cs).
        else if (action.View == IslandView.Layouts && widget is LayoutsLine layouts)
        {
            if (layouts.TakeChoice() is { } layout)
            {
                SwitchLayout(window, layout);
            }
            // §404: one layout, its Status's "Add": §485 Display ▾ at "Add a layout", the presets' one picker.
            else if (layouts.TakeAdd())
            {
                CloseFolder();
                OpenEditMenuLater(window, EditEntry.AddLayout);
            }
        }
        // drives: the drive in File Explorer, or an item of its contents with its own program, as
        // the drive flyout opened them: the folder has gone already.
        else if (action.View == IslandView.App && widget is DriveIsland drive)
        {
            drive.Open();
        }
        // screentime: a click on an app picks it in the folder and opens nothing; its right click's
        // "Open" is where it opens (Dock.WidgetMenus.cs, WidgetAct.OpenApp).
        else if (action.View == IslandView.App && widget is ShortcutTile shortcut)
        {
            CloseFolder();
            // Once the screen shows every folder gone (Surfaces.AfterPresent, §354).
            Surfaces.AfterPresent(shortcut.Act);
        }
        // power: a power button's command, once every folder has gone, as Windows' power menu goes
        // before the PC sleeps or the session ends; it runs off the dock's thread (PowerButtonTile).
        else if (action.View == IslandView.App && widget is PowerButtonTile power)
        {
            CloseFolder();
            power.Act();
        }
        // §641: a Start widget's app, file or folder opened, or its power command done, once the folder has gone.
        else if (action.View == IslandView.App && widget is StartWidgets.StartLine start)
        {
            CloseFolder();
            start.Act();
        }
    }

    /// <summary>Closes every open folder list: another popup opens (<paramref name="closer"/>'s default), edit mode ends, something is launched.</summary>
    private void CloseFolder(FolderCloser closer = FolderCloser.OtherPopup)
    {
        // edit: while a bar is arranged its folder is not closed by another popup opening - which
        // is what brings us here most often. A folder closes there on five things only (OneFolder): its
        // own button again, another folder opening (ShowFolder closes it itself), Escape, edit mode
        // ending, and §491 a launch.
        if (!OneFolder.Closes(closer, editor is not null))
        {
            return;
        }
        foreach (var open in openFolders.ToArray())
        {
            CloseFolder(open);
        }
    }

    /// <summary>Closes one open folder list, as its own button does.</summary>
    private void CloseFolder(OpenFolder open)
    {
        if (open.Flyout.IsOpen)
        {
            open.Flyout.Hide();
            FolderClosed(open, default);
            return;
        }
        Forget(open);
    }

    /// <summary>The folder lists go with the dock: its window points at them. §357: the views kept with them.</summary>
    private void DisposeFolder()
    {
        adding = null;
        (ghostFolder, ghostAside) = (null, null);
        foreach (var open in openFolders.ToArray())
        {
            Forget(open);
        }
        foreach (var view in folderViews.Values)
        {
            view.Dispose();
        }
        folderViews.Clear();
        folderViewsUsed.Clear();
        folderMemories.Clear();
    }

    // --- Arranging a folder on the bar ----------------------------------------

    /// <summary>
    /// edit: the widget keyed <paramref name="key"/> (a folder's, "&lt;owner&gt;/&lt;folder&gt;/&lt;id&gt;")
    /// goes to <paramref name="place"/> in its folder (<see cref="Folders.Move"/>): in between two
    /// lines of a list, at a list's end, or a new list of its own — the list it leaves going where
    /// that was its last widget. Nothing is saved where nothing moved.
    /// </summary>
    /// <param name="keys">The widget, or the lines carried together with it in their order, it among them (FolderFlyout.Pending.cs).</param>
    private void PlaceInFolder(IReadOnlyList<string> keys, FolderPlace place)
    {
        var next = config.Clone();
        if (PlaceInFolderIn(next, keys, place))
        {
            Apply(next);
        }
    }

    /// <summary>
    /// edit: <see cref="PlaceInFolder"/> done to <paramref name="config"/>: one line moved as
    /// <see cref="Folders.Move"/> moves it, several together as <see cref="Folders.MoveGroup"/> does —
    /// those of the first one's folder, in the order given. False where nothing moved.
    /// </summary>
    internal static bool PlaceInFolderIn(Config config, IReadOnlyList<string> keys, FolderPlace place)
    {
        if (keys.Count == 0)
        {
            return false;
        }
        var owner = Islands.OwnerOf(keys[0]);
        var path = Islands.IdOf(keys[0]);
        var cut = path.IndexOf('/');
        if (cut <= 0 || Folders.FolderIn(config.ListFor(owner), path[..cut]) is not { } folder)
        {
            return false;
        }
        var prefix = path[..(cut + 1)];
        var cells = keys
            .Where(key => string.Equals(Islands.OwnerOf(key), owner, StringComparison.OrdinalIgnoreCase)
                && Islands.IdOf(key).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(key => Folders.CellOf(folder, Islands.IdOf(key)[prefix.Length..]))
            .OfType<FolderCell>()
            .ToList();
        return cells.Count switch
        {
            0 => false,
            1 => Folders.Move(folder, cells[0], place),
            _ => Folders.MoveGroup(folder, cells, place),
        };
    }

    /// <summary>
    /// edit: the folder line keyed <paramref name="key"/> is <paramref name="height"/> tall now, as its
    /// kind allows (<see cref="LineHeightRows.Resize"/>), saved as every change of the dock's is; nothing
    /// is saved where nothing changed.
    /// </summary>
    private void SetLineHeight(string key, LineHeight height) => _ = ChangeWidget(key, item => LineHeightRows.Resize(item, height));

    /// <summary>§432: the sized folder line keyed <paramref name="key"/> shows <paramref name="size"/> now, saved; nothing where nothing changed.</summary>
    private void SetLineSize(string key, LineSize size) => _ = ChangeWidget(key, item => LineHeightRows.Resize(item, size));

    /// <summary>
    /// edit: Shift and <paramref name="arrow"/> on the widget keyed <paramref name="key"/>, a bar's block or a folder's
    /// line in <paramref name="window"/>'s edit mode: a size larger or smaller as its menu sizes it (EditResize.cs, §471),
    /// its folder's room <paramref name="room"/> cells at the most; saved as the menu's change is, one step of the undo,
    /// and what it is now told to a screen reader.
    /// </summary>
    private void ResizeByKeys(DockWindow window, string key, int arrow, int room)
    {
        if (ChangeWidget(key, item => EditResize.Step(item, arrow, room))
            && config.FindIsland(Islands.OwnerOf(key), Islands.IdOf(key)) is { } now && EditResize.Said(now, arrow) is { } said)
        {
            Uia.Announce(window.Hwnd, said);
        }
    }

    /// <summary>
    /// edit: the widget keyed <paramref name="key"/> changed by <paramref name="change"/> on a copy of the config, saved as
    /// every change of the dock's is (Apply: one step of the undo) where it says it changed; false where nothing did.
    /// </summary>
    private bool ChangeWidget(string key, Func<WidgetItem, bool> change)
    {
        var next = config.Clone();
        if (next.FindIsland(Islands.OwnerOf(key), Islands.IdOf(key)) is not { } item || !change(item))
        {
            return false;
        }
        Apply(next);
        return true;
    }

    /// <summary>
    /// menus: a row of a widget's settings acted (OptionMenu.cs): set on the widget keyed by the menu,
    /// or on its folder's face, as Settings sets it, and saved as every change of the dock's is — a
    /// line restyled is made again in its new style, a face added, taken off or moved changes the
    /// button. An action — a line's list deleted — asks first in Windows' own dialog where its row
    /// asks (§228). Where the menu stays open (<paramref name="list"/>, a toggle or a step) it shows
    /// the rows as they are now. A bar's looks are the bar's (<see cref="LooksPicked"/>, §290), the edit
    /// bar's ⚙ Settings its own (<see cref="SettingsPicked"/>, §335).
    /// </summary>
    private void OptionPicked(Pending what, OptionPick pick, Flyout? list, int? litFace = null)
    {
        // §351: a link: the menu has gone, the one that owns the setting opens at its level (Dock.EditEntries.cs).
        if (pick.Kind == OptionPickKind.Link)
        {
            OpenEditMenuLater(what.Owner, EditEntries.Of((OptionLink)pick.Value));
            return;
        }
        // menus: a pin's target, §532 a taskbar menu shortcut's, §553 on the Taskbar page: Windows' own open dialog (Dock.MenuPickers.cs).
        if (pick.Kind == OptionPickKind.Browse)
        {
            MenuBrowse(what, pick);
            return;
        }
        if (what.Looks is { } bar)
        {
            LooksPicked(what, bar, pick, list);
            return;
        }
        if (what.SettingsMenu is not null)
        {
            SettingsPicked(what, pick, list);
            return;
        }
        if (what.Keys.Count == 0)
        {
            return;
        }
        var next = config.Clone();
        // A switch too: an account's part's (§338) is a row of its accounts' level. §528: every widget of a page of several
        // selected, in the one copy of the file, saved once: one step of the undo.
        var targets = TargetsOf(next, what.Keys, sources: pick.Kind is OptionPickKind.Set or OptionPickKind.Act or OptionPickKind.Flip);
        if (OptionMenu.AskOf(targets, pick) is { } question
            && !Ask(what.Folder?.Flyout.Hwnd ?? what.Owner?.Hwnd ?? default, question))
        {
            return;
        }

        // §411 (R5): what is added is lit where it stands now, the menu back on its list: a face, a toggle.
        var added = pick.Kind == OptionPickKind.Flip && OptionMenu.Row(targets, pick) is { Ordered: true, On: false };
        if (OptionMenu.Apply(targets, pick))
        {
            Apply(next);
            if (pick.Kind == OptionPickKind.AddFace && (targets[0].Item as BarItem)?.Folder is { } folder)
            {
                litFace = folder.Faces.Count - 1;
            }
        }
        if (list is { IsOpen: true })
        {
            MenuAgain(what, litFace, added ? pick.Key : null);
        }
    }

    /// <summary>
    /// edit: edit target <paramref name="part"/> of <paramref name="widget"/>'s card was clicked in the
    /// arranged folder (<see cref="CellIsland.EditClick"/>): what it changes is written into the
    /// widget's settings and saved as every change of the dock's is; nothing where nothing changed.
    /// </summary>
    private void EditPart(CellIsland widget, int part)
    {
        var next = config.Clone();
        if (next.FindIsland(Islands.OwnerOf(widget.Key), Islands.IdOf(widget.Key)) is { } item && widget.EditClick(part, item))
        {
            Apply(next);
            _ = widget.Wake();
        }
    }

    /// <summary>
    /// edit: a height row of a folder line's menu acted (LineHeightRows.cs): the mode flipped or the
    /// rows stepped, saved at once, the line resizing behind the menu, which stays open and shows the
    /// height as it is now.
    /// </summary>
    private void MenuAdjusted(Pending what, Flyout list, uint id)
    {
        // §589: whether it was the one choice of its level, as the menu had it before it is shown again for the pick.
        var choseOne = list.TakeChoseOne();
        // menus: a toggle, a stepper or a choice of the widget's settings, or a face added, moved or
        // taken off (OptionMenu.cs); a text or a search field's words or pick, set first (Dock.MenuFields.cs).
        if (OptionMenu.PickOf(what.Options, id) is { } option)
        {
            var kept = list.TakeFieldCommit(id);
            if (kept is not null)
            {
                FieldKept(what, option, kept);
            }
            OptionPicked(what, option, list);
            // §520: a choice made in a popup of the settings panel closes it, as a ComboBox's list closes: §554 a search's
            // find, §589 an Add's row (a face, a toggle) too.
            if (what.Drops)
            {
                PanelDropped(what, choseOne);
            }
            return;
        }
        // menus: a widget's own switch or pick, done as a pick does it; the menu stays, its tick where
        // it went (MenuKeeps.cs, §219).
        if (ActRowOf(what.Acts, id) is { } act && MenuKeeps.Stays(act.Act))
        {
            RunAct(act, act.Works ?? (what.Folder is { } open ? open.Flyout.Folder?.Widget(what.Target) : null), OpenFromMenu);
            if (list.IsOpen)
            {
                list.Refresh(MenuKeeps.Ticked(list.ShownItems, what.Acts, id));
            }
            return;
        }
        // The menu stays, its Height or Size saying the line's as it is now.
        if (LineHeightActed(what, id) && list.IsOpen && what.Target is { Length: > 0 } key && config.FindIsland(Islands.OwnerOf(key), Islands.IdOf(key)) is { } item)
        {
            var shown = LineHeightRows.UpdatedSize(list.ShownItems, item);
            list.Refresh(LineHeightRows.For(item, RoomCells(what)) is { } now ? LineHeightRows.Updated(shown, now) : shown);
        }
    }

    /// <summary>
    /// A height row of the folder line <paramref name="what"/> is over acted (LineHeightRows.cs) — its menu's (MenuAdjusted)
    /// or, §573, its page's of the settings panel (Chosen): §432 a sized line's size picked, else the mode picked or the rows
    /// stepped, saved at once. Whether <paramref name="id"/> was one of those rows' (changed or not).
    /// </summary>
    private bool LineHeightActed(Pending what, uint id)
    {
        if (!LineHeightRows.Holds(id) || what.Target is not { Length: > 0 } key || config.FindIsland(Islands.OwnerOf(key), Islands.IdOf(key)) is not { } item)
        {
            return false;
        }
        if (LineHeightRows.ActSize(item, id) is { } size)
        {
            SetLineSize(key, size);
        }
        else if (LineHeightRows.For(item, RoomCells(what)) is { } choice && LineHeightRows.Act(choice, id) is { } height)
        {
            SetLineHeight(key, height);
        }
        return true;
    }

    /// <summary>
    /// menus: a folder's face's row <paramref name="id"/> dragged, or moved by the keys, to place
    /// <paramref name="to"/> among the faces (OptionMenu.Moved): the face moved there as the page's list
    /// moves it, the menu shown again with its row lit where it went.
    /// </summary>
    private void MenuReordered(Pending what, Flyout list, uint id, int to)
    {
        if (OptionMenu.Moved(OptionMenu.PickOf(what.Options, id), to) is { } move)
        {
            OptionPicked(what, move, list, litFace: to);
        }
    }

    /// <summary>The most rows a line of the folder <paramref name="what"/>'s menu is over may be set to: its room on its screen (§192).</summary>
    private static int RoomCells(Pending what) => what.Folder?.Flyout.RoomCells() ?? LineHeight.Most;

    /// <summary>The options of the folder keyed <paramref name="key"/> as the config has them, or null.</summary>
    private FolderOptions? FolderOptionsOf(string key) => (config.FindIsland(Islands.OwnerOf(key), Islands.IdOf(key)) as BarItem)?.Folder;

    /// <summary>edit: the first of a list's width menu's rows, by command; the rest follow (<see cref="FolderListWidths.All"/>).</summary>
    private const uint CMD_LIST_WIDTH = 5860;

    /// <summary>
    /// edit: a list's width button in an arranged folder (FolderFlyout, <see cref="FolderFlyoutAction.Width"/>),
    /// or an empty slot's: narrow, medium and wide at <paramref name="anchor"/>, the list
    /// <paramref name="column"/>'s own ticked — a slot's medium, its width — over the folder, which
    /// holds itself open for it as for a widget's menu and is let go when it ends
    /// (<see cref="FolderMenuEnded"/>, through <see cref="Pending.Folder"/>).
    /// </summary>
    private void FolderWidthMenu(OpenFolder opened, FolderPlace column, POINT anchor)
    {
        if (!disposed && windows.Contains(opened.Window) && Arranging(opened.Window) && FolderOptionsOf(opened.Key) is { } folder
            && (column.New || (column.List >= 0 && column.List < folder.Lists.Count)))
        {
            ShowFlyout(
                new Pending(Topic.FolderWidth, opened.Window) { Target = opened.Key, Column = column, Folder = opened },
                FolderWidthRows(column.New ? FolderListWidth.Medium : folder.WidthOf(column.List), column.New ? [] : opened.Flyout.RunsOffAt(column.List)),
                FlyoutLayout.List,
                anchor);
        }
        // No menu came of it: the button goes back to rest and the folder to what it was.
        if (!FlyoutOpen || !ReferenceEquals(pending?.Folder, opened))
        {
            opened.Flyout.MenuClosed();
        }
    }

    /// <summary>edit: the first of a list's warning list's rows, by command; one per widget it has no room for.</summary>
    private const uint CMD_FOLDER_HIDDEN = 5880;

    /// <summary>
    /// edit: a list's warning button in an arranged folder (<see cref="FolderFlyoutAction.NotShown"/>):
    /// the widgets that list has no room to show, as the edit bar's "not shown" button shows the bar's
    /// (Dock.EditRail.cs, ShowNotShown) — one straight to its own menu, several listed by name at
    /// <paramref name="anchor"/>, a row that widget's menu. Over the folder, which holds itself open
    /// for it and is let go when it ends (<see cref="FolderMenuEnded"/>).
    /// </summary>
    private void FolderNotShown(OpenFolder opened, IReadOnlyList<IIsland> hidden, POINT anchor)
    {
        if (!disposed && windows.Contains(opened.Window) && Arranging(opened.Window) && hidden.Count > 0)
        {
            if (hidden.Count == 1)
            {
                WidgetMenu(opened.Window, hidden[0].Key, anchor, opened.Hit, unknown: hidden[0].Info.Title, folder: opened, widget: hidden[0]);
            }
            else
            {
                ShowFlyout(
                    new Pending(Topic.FolderHidden, opened.Window) { Folder = opened, FolderHidden = [.. hidden] },
                    [.. hidden.Select((widget, index) => new FlyoutItem(CMD_FOLDER_HIDDEN + (uint)index, widget.Info.Title, Glyph(widget.Info.Glyph)))],
                    FlyoutLayout.List,
                    anchor,
                    S.DockFolderNoRoomList);
            }
        }
        // No menu came of it: the button goes back to rest and the folder to what it was.
        if (!FlyoutOpen || !ReferenceEquals(pending?.Folder, opened))
        {
            opened.Flyout.MenuClosed();
        }
    }

    /// <summary>edit: a row of a list's warning list: that widget's own menu, over the folder, where the list stood.</summary>
    private void FolderHiddenChosen(Pending what, uint id, POINT anchor)
    {
        var index = (int)(id - CMD_FOLDER_HIDDEN);
        if (id < CMD_FOLDER_HIDDEN || index >= what.FolderHidden.Count || what.Owner is not { } window || what.Folder is not { } opened
            || !openFolders.Exists(open => ReferenceEquals(open, opened)) || !opened.Flyout.IsOpen)
        {
            return;
        }
        var widget = what.FolderHidden[index];
        opened.Flyout.HoldOpen();
        WidgetMenu(window, widget.Key, anchor, opened.Hit, unknown: widget.Info.Title, folder: opened, widget: widget);
        if (!FlyoutOpen || !ReferenceEquals(pending?.Folder, opened))
        {
            opened.Flyout.MenuClosed();
        }
    }

    /// <summary>
    /// edit: a list's width menu: narrow, medium, wide, extra wide, widest, <paramref name="current"/>
    /// ticked; each of <paramref name="runsOff"/> — a width that would run the folder off the screen
    /// (FolderFlyout.RunsOffAt) — marked with the warning glyph and "Runs off the screen" under it, and
    /// still there to pick (§320: nothing is refused for not fitting; §328).
    /// </summary>
    internal static List<FlyoutItem> FolderWidthRows(FolderListWidth current, IReadOnlyCollection<FolderListWidth>? runsOff = null) =>
        [.. FolderListWidths.All.Select((width, index) => runsOff?.Contains(width) == true
            ? new FlyoutItem(CMD_LIST_WIDTH + (uint)index, FolderListWidths.Name(width), Glyph(GlyphWarning), Detail: S.DockFolderWidthOffScreen, Checked: width == current)
            : new FlyoutItem(CMD_LIST_WIDTH + (uint)index, FolderListWidths.Name(width), Checked: width == current))];

    /// <summary>
    /// edit: row <paramref name="id"/> of a list's width menu picked: that list of the folder
    /// <see cref="Pending.Target"/> keys is that wide from now on (<see cref="Folders.SetWidth"/>),
    /// saved, and the open folder lays itself out again — the list wider or narrower, the others
    /// moved along, nothing else. A slot's is an empty list that wide where the slot stood, the slots
    /// between it and the lists empty medium ones (<see cref="Folders.SetSlotWidth"/>). Nothing is
    /// saved where nothing changed.
    /// </summary>
    private void FolderWidthChosen(Pending what, uint id)
    {
        var index = (int)(id - CMD_LIST_WIDTH);
        if (id < CMD_LIST_WIDTH || index >= FolderListWidths.All.Count)
        {
            return;
        }
        var next = config.Clone();
        var width = FolderListWidths.All[index];
        if ((next.FindIsland(Islands.OwnerOf(what.Target), Islands.IdOf(what.Target)) as BarItem)?.Folder is not { } folder)
        {
            return;
        }
        if (what.Column.New ? Folders.SetSlotWidth(folder, what.Column, width) : Folders.SetWidth(folder, what.Column.List, width))
        {
            Apply(next);
        }
    }

    // --- The layouts in the bar's own menu ------------------------------------

    /// <summary>
    /// layouts: the named layouts for the bar's menu — rows of its own, between separators, not a
    /// submenu — with the one this bar's display shows ticked. Every layout can be shown on every
    /// display, so they are all offered; §561 a lone one too, ticked
    /// (<see cref="LayoutRows"/>). The menu is one width (Flyout.MenuWidthDip), so a long name is
    /// cut with an ellipsis, and past the flyout's greatest height the rows scroll.
    /// </summary>
    private List<FlyoutItem> LayoutItems(DockWindow window) =>
        LayoutRows(config.Layouts, config.ActiveLayout(window.Screen.Id).Id);

    /// <summary>
    /// layouts: the display of <paramref name="window"/> shows the layout chosen in its menu, or in
    /// the layouts' line, from now on (<see cref="Config.SelectLayout"/>): its bars come again in
    /// that layout's type, with its widgets.
    /// </summary>
    private void ChooseLayout(DockWindow window, int index)
    {
        if (index < 0 || index >= config.Layouts.Count || !windows.Contains(window))
        {
            return;
        }
        var next = config.Clone();
        if (index < next.Layouts.Count && next.SelectLayout(window.Screen.Id, next.Layouts[index].Id, window.Screen.Name))
        {
            Apply(next);
        }
    }

    // --- A widget carried from the bar over an open folder ---------------------
    //
    // A block carried along the bar in edit mode has the mouse captured, so the folders standing
    // open above it never see the pointer: the bar asks them as it moves (FolderOffer) and hands
    // the drop to the one it is over when it is let go (DropIntoOpenFolder). Nothing moves from the
    // bar into a folder (the person, 2026-10-09: "i would rather disable it completely"), so the
    // folder lights no place and says why (Partners.WhyNot), and the drop changes nothing.

    /// <summary>
    /// folders: <paramref name="widget"/> is being carried at <paramref name="screen"/>
    /// while the bar is edited. True when it is over an open folder, which then says why it won't
    /// take it; the bar's own drop marks give way to it.
    /// </summary>
    private bool FolderOffer(POINT screen, WidgetItem widget) =>
        Offered([.. openFolders.Select(open => open.Flyout)], screen, widget) >= 0;

    /// <summary>
    /// folders: which of <paramref name="open"/> a widget carried at <paramref name="screen"/> is
    /// over — the first one it is over takes it and shows where it would land, or says why it
    /// won't (<see cref="FolderFlyout.Offer"/>) — and every other one shows nothing. -1 for none.
    /// </summary>
    internal static int Offered(IReadOnlyList<FolderFlyout> open, POINT screen, WidgetItem widget)
    {
        var over = -1;
        for (var index = 0; index < open.Count; index++)
        {
            if (over < 0 && open[index].IsOpen && open[index].Offer(screen, widget))
            {
                over = index;
                continue;
            }
            open[index].NoOffer();
        }
        return over;
    }

    /// <summary>folders: nothing is being carried over any open folder any more.</summary>
    private void FolderNoOffer()
    {
        foreach (var open in openFolders)
        {
            open.Flyout.NoOffer();
        }
    }

    /// <summary>
    /// folders: what letting go now would do with a widget carried over the open folder: the folder
    /// it is over and why it won't take it — read before the carry ends, because ending it takes
    /// every offer back (<see cref="FolderNoOffer"/>).
    /// </summary>
    private readonly record struct FolderLetGo(OpenFolder Open, string Refusal);

    /// <summary>folders: the offer the open folder is making for a widget carried over it, or null where none is.</summary>
    private FolderLetGo? FolderOffered() =>
        openFolders.Find(entry => entry.Flyout.IsOpen && entry.Flyout.Refusal.Length > 0) is { } open
            ? new FolderLetGo(open, open.Flyout.Refusal)
            : null;

    /// <summary>
    /// folders: a widget carried from the bar was let go over the open folder that offered
    /// <paramref name="letGo"/>. Nothing moves from the bar into a folder: everything stays where it
    /// was, and the folder says why (<see cref="FolderFlyout.Refusal"/>, <see cref="Partners.WhyNot"/>).
    /// </summary>
    private void DropIntoOpenFolder(FolderLetGo letGo) =>
        letGo.Open.Flyout.Say(letGo.Refusal.Length > 0 ? letGo.Refusal : NoRoom);

    /// <summary>What a folder says when the place a widget was let go on is taken, or it has no room left.</summary>
    private static string NoRoom => S.DockFolderNoRoom;

    // --- The edit mode's "Add" -----------------------------------------------

    /// <summary>
    /// A "+" of <paramref name="window"/>'s bar (<see cref="Layout.Adds"/>, the one at <paramref name="index"/>): the add page
    /// for its end of its section — what is picked goes in before that section's first widget or after its last at once, the
    /// add list's line marking the place meanwhile. §576: the edit bar's add page (§575, Dock.AddOf), as every "+" opens it
    /// (§531 to §575 a menu of its own); §593 at the edit bar's top (§576 to §592 hung from the "+").
    /// </summary>
    private void AddAtPlus(DockWindow window, int index)
    {
        var all = window.Layout.Adds();
        if (index < 0 || index >= all.Count || !Arranging(window))
        {
            return;
        }
        OpenAdd(AtPlus(window, all[index]) with { Plus = window.OnScreen(all[index].Rect) });
    }

    /// <summary>
    /// The place of <paramref name="window"/>'s "+" <paramref name="plus"/>: its end of its section, in the list itself
    /// (DockWindow.ListSlot) and where it is drawn, by the window's own parts, which are not the list's — places: the list holds
    /// every edge, the window only its own.
    /// </summary>
    private static Adding AtPlus(DockWindow window, BarAdd plus)
    {
        var at = plus.Slot;
        return new Adding(window, new AddSpot(Config.DisplayOwner(window.Screen.Id), at with { Index = Math.Clamp(at.Index, 0, window.Layout.Specs.Count) }, To: window.ListSlot(at)), null);
    }

    /// <summary>
    /// §593: where the edit bar's own Add of display <paramref name="displayId"/> adds, there being no "+" pressed: into the
    /// folder open arranged there, at the place it adds at (<see cref="FolderFlyout.AddPlace"/>: its first list not full, at
    /// its end away from the bar; §298); else at the bar's last "+" — the end section's, before its first widget, where a
    /// taskbar keeps what it shows beside the clock (the bottom section's on a bar at the side) —, on the display's bar or, where
    /// that has none, its first surface that has one. Null where there is nowhere to add (a display that shows None).
    /// </summary>
    private Adding? AddAt(string displayId)
    {
        if (ArrangedFolder() is { } open && open.Window.Id == displayId)
        {
            return open.Flyout.IsOpen && open.Flyout.AddPlace() is { } place && Islands.OwnerOf(open.Key) is { } owner
                && string.Equals(Config.DisplayOwner(open.Window.Screen.Id), owner, StringComparison.OrdinalIgnoreCase)
                ? new Adding(open.Window, AddSpot.InFolder(owner, Islands.IdOf(open.Key), place), open)
                : null;
        }
        var surfaces = windows.Where(window => window.Id == displayId && Arranging(window) && window.Layout.Adds().Count > 0).ToList();
        return (surfaces.Find(window => window == BarOf(displayId)) ?? surfaces.FirstOrDefault()) is { } bar ? AtPlus(bar, bar.Layout.Adds()[^1]) : null;
    }

    /// <summary>
    /// folders: the add page for <paramref name="opened"/>'s place <paramref name="place"/> — a list's end
    /// (<see cref="FolderFlyout.AddPlace"/>), or a slot's list of its own; null for the end of its last list — from the column's
    /// "+" (<paramref name="from"/>, screen px), the folder staying open under it as it does through everything while its bar is
    /// arranged. What is picked goes in at <paramref name="place"/> at once (<see cref="AddChosen"/>, <see cref="AddSpot.InFolder"/>).
    /// A "+" that opened nothing goes back to rest.
    /// </summary>
    private void AddInFolder(OpenFolder opened, FolderPlace? place, RECT from)
    {
        if (Arranging(opened.Window) && Islands.OwnerOf(opened.Key) is { } owner
            && string.Equals(Config.DisplayOwner(opened.Window.Screen.Id), owner, StringComparison.OrdinalIgnoreCase))
        {
            OpenAdd(new Adding(opened.Window, AddSpot.InFolder(owner, Islands.IdOf(opened.Key), place), opened, from));
        }
        if (!ReferenceEquals(adding?.Folder, opened))
        {
            opened.Flyout.MenuClosed();
        }
    }

    /// <summary>
    /// §576: the add page opened for <paramref name="add"/> on its display's edit bar, §593 at its top; the "+" whose page this
    /// very press closed only closes it (§430).
    /// </summary>
    private void OpenAdd(Adding add)
    {
        if (addClosed is { } was && add.Plus is { } plus && was.left == plus.left && was.top == plus.top && was.right == plus.right && was.bottom == plus.bottom)
        {
            addClosed = null;
            return;
        }
        OpenPanel(add.Window.Id, PanelPage.Add, add: add);
    }

    /// <summary>§576: the "+" whose add page the press being handled closed (<see cref="Pressing"/>); null for none.</summary>
    private RECT? addClosed;

    /// <summary>
    /// A row of the add list: that widget, added at the place the list was opened for — at once (the person, 2026-10-23: "when
    /// i click the add on a specific column or specific slot then it should immediately place it there and not stick to my
    /// mouse"; §297), §593 every place known — with edit mode's focus on it, the answer to "where did it go"; or a ready-made
    /// group, all of it, the focus on its folder. §593 the very change its ghost showed (<see cref="Ghosted"/>): one step of the
    /// undo, the bar standing as the ghost had it; a folder put on the bar opens, arranged (the person, 2026-11-09: "it should
    /// also open the folder (if it was a folder widget that was placed of course)"); a folder's line picked, as one put down
    /// there is. False for a choice that is none of the list's.
    /// <para>
    /// Nothing opens the settings app: a widget is added and that is all (the person, 2026-10-12: "when adding a widget it
    /// should not open the settings automatically"). One that shows nothing until it is set up says so on its line or block,
    /// whose click opens its menu, where the field it needs is (CellIsland.Unset; §228).
    /// </para>
    /// </summary>
    private bool AddChosen(Pending what, uint id)
    {
        if (what.Topic != Topic.Library || what.Owner is not { } window || what.Spot is not { } spot
            || id < CMD_ADD_WIDGET || id - CMD_ADD_WIDGET >= (uint)(what.Library.Count + what.Presets.Count))
        {
            return false;
        }
        var owner = Config.DisplayOwner(window.Screen.Id);
        // Not arranged any more, or the display the list was opened for is not this bar's.
        if (!Arranging(window) || !string.Equals(owner, spot.Owner, StringComparison.OrdinalIgnoreCase) || Ghosted(what, id) is not { } made)
        {
            return true;
        }
        // licence: past the free version's widgets on this layout nothing is added, and it says why (Dock.License.cs).
        if (!LicenseAllows(made.Config, owner, window))
        {
            return true;
        }
        window.FocusOn(made.Key);
        Apply(made.Config);
        if (spot.FolderId is not null)
        {
            EditPick(made.Key);
        }
        else if (windows.Find(bar => Arranging(bar) && bar.Layout.PartIndex(made.Key) >= 0) is { } holder
            && holder.IslandAt(Hit.Cell(holder.Layout.PartIndex(made.Key), 0)) is FolderIsland { IsFolder: true })
        {
            // A folder, as a click on its face opens it (EditActivate): picked, then open arranged; a widget stays as it went in.
            EditPick(made.Key);
            OpenArranged(holder, made.Key);
        }
        return true;
    }

    /// <summary>
    /// The add page closed, picked from or not, another opened, or edit mode ended (§576): the place it marked goes back to how
    /// it was, §593 its ghost with it, the column's "+" to rest.
    /// </summary>
    private void EndAdding()
    {
        if (adding is not { } open)
        {
            return;
        }
        adding = null;
        addingClear = null;
        Unghost(open);
        // A column's "+" it was opened from goes back to rest.
        open.Folder?.Flyout.MenuClosed();
        if (!disposed && Arranging(open.Window))
        {
            open.Window.ShowEdit();
        }
    }

    private sealed partial class DockWindow
    {
        /// <summary>What edit mode shows on this bar, shown again: the "Add" marker came or went.</summary>
        public void ShowEdit()
        {
            if (Editing)
            {
                scene.Edit(EditView());
            }
        }

        /// <summary>
        /// pins: what the block being dragged in edit mode (part <paramref name="dragging"/>, the
        /// pointer at <paramref name="along"/>) shows on the bar: where it would land.
        /// <para>
        /// folders: over an open folder's panel (<see cref="overFolder"/>) the folder has it, and
        /// the bar stays as it stood when the pointer went onto the panel — nothing moves under the
        /// panel (<see cref="BarCarry"/>).
        /// </para>
        /// </summary>
        private BarCarry.Shown FolderDrop(int dragging, float along)
        {
            if (editHoldFor != pressAt)
            {
                editHoldFor = pressAt;
                carry = new BarCarry();
            }
            // places: another surface of the display under the pointer has the block as a folder does.
            return carry.Step(overFolder || overBar is not null, () => new BarCarry.Shown(along, layout.DropSlot(along, dragging)));
        }

        // folders: what the bar shows of the block it carries (BarCarry.cs).
        private BarCarry carry = new();

        /// <summary>Which drag <see cref="carry"/> belongs to: where that drag's press was.</summary>
        private Vector2 editHoldFor = new(float.NaN, float.NaN);

    }
}
