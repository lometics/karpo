// edit: a widget that follows the pointer (the person, 2026-10-07: "the widget follows the mouse, rendered as it really looks,
// with the blue ghost marking the landing. A left click places it. Escape or right-click cancels") — §593 no longer a pick of the
// add page's, which goes in at the page's place at once, its ghost shown there first (Dock.AddGhost.cs).
//
//  - **A widget's copy** ("Copy" in its menu, Dock.EditCopy.cs). It is in the list meanwhile, at the place of its display
//    nearest the pointer, so a bar can draw it as it is; that bar carries it as a drag carries a block (DockWindow.Follow), with
//    no button held, and the click lands it wherever the drag would have. Called off, it comes out of the list again. The click,
//    the right click and Escape are heard by hooks wherever they land (EditFollowHook.cs): the bar may hold neither the mouse nor
//    the keys by then. Escape while one follows calls it off; the next Escape leaves edit mode. A folder's line follows in its
//    folder, which carries it there (FolderFlyout.Carry.cs) while the hooks here hear the click that puts it down and the one
//    that calls it off.
//
// A widget that follows lands as any carried block does, on any display (§519, Dock.BarOffer): it goes
// into the list of the display it was copied on, and moves to another's where it is put down there.
//
// Pasted widgets (Ctrl+V; Dock.EditSelect.cs) follow the pointer the same way,
// several at once: they are in the list side by side at the place nearest the pointer, carried
// together as several selected widgets are, landed together by the click, and all taken out again
// when called off.

using System.Numerics;
using Karpo.Core;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

public sealed unsafe partial class Dock
{
    /// <summary>
    /// edit: the widget following the pointer, by its key: from the add list, copied or pasted, so
    /// calling it off takes it out. <paramref name="Together"/>: the widgets following with it, it
    /// first — pasted copies (Dock.EditSelect.cs) —, all taken out again when called off; null for it alone.
    /// </summary>
    private sealed record Following(string Key, IReadOnlyList<string>? Together = null)
    {
        /// <summary>The folder a copied line follows the pointer in (Dock.EditCopy.cs); null for a widget of the bar.</summary>
        public OpenFolder? InFolder { get; init; }
    }

    /// <summary>edit: the widget following the pointer; null when none is.</summary>
    private Following? follow;

    /// <summary>edit: the hooks that hear the clicks and Escape while a widget follows (EditFollowHook.cs).</summary>
    private EditFollowHook? followHook;

    /// <summary>edit: no widget follows the pointer any more, and the hooks let go.</summary>
    private void Unfollow()
    {
        follow = null;
        followHook?.Release();
    }

    /// <summary>
    /// edit: the surface of display <paramref name="displayId"/> nearest <paramref name="at"/> (screen px)
    /// and the place along it nearest that point, as the list counts it (<see cref="DockWindow.ListSlot"/>):
    /// where a widget taken from the add list goes in before it follows the pointer.
    /// Null where edit mode shows nothing on that display to take it.
    /// </summary>
    private (DockWindow Window, BarSlot Slot)? NearestSlot(string displayId, POINT at)
    {
        var surfaces = windows.Where(window => window.Id == displayId && Arranging(window) && window.DropArea() is not null).ToList();
        if (surfaces.Count == 0)
        {
            return null;
        }
        var nearest = EditDrops.Nearest(at, [.. surfaces.Select(window => window.DropArea() ?? default)], [.. surfaces.Select(_ => true)]);
        var window = surfaces[Math.Max(0, nearest)];
        return (window, window.ListSlot(window.DropSlotAt(at)));
    }

    /// <summary>
    /// edit: the widget <paramref name="key"/>, just put into the list, follows the pointer from the
    /// surface that drew it (see the top of the file), and <paramref name="together"/> with it, it
    /// first, where several do (pasted copies). Where no surface shows it — no room for it on its bar —
    /// it stays where it went in.
    /// </summary>
    private void Follow(string key, IReadOnlyList<string>? together = null)
    {
        together = together is { Count: > 1 } ? together : null;
        follow = new Following(key, together);
        if (windows.Find(window => Arranging(window) && window.Layout.PartIndex(key) >= 0) is not { } holder || !holder.Follow(key, together))
        {
            Unfollow();
            return;
        }
        EditHook().Arm();
    }

    /// <summary>edit: the hooks that hear the clicks, Escape, undo and redo (EditFollowHook.cs), made with the first edit mode.</summary>
    private EditFollowHook EditHook() => followHook ??= new EditFollowHook(instance, () => host, EscapeIsTheirs, () => FlyoutOpen || PanelTyping);

    /// <summary>§520: the edit bar's search, a box of its flyout, has the keys: Ctrl+Z and Ctrl+Y are its own then, as a menu's field's are.</summary>
    private bool PanelTyping => panelOn is { } on && chromes.TryGetValue(on, out var chrome) && chrome.Deck.Typing;

    /// <summary>
    /// edit: the hooks heard a press or Escape while a widget followed the pointer (EditFollowHook.cs):
    /// a left press at <paramref name="at"/> (screen px) puts it down where it shows it would land; a
    /// right press or Escape calls it off. Where no surface carries it any more — a build took it
    /// from the one that did — it is put down where it is, or called off, never left following.
    /// Escape with none following leaves edit mode (<see cref="EscapeHeard"/>); Ctrl+Z undoes and
    /// Ctrl+Y redoes, wherever the keys are (§352).
    /// </summary>
    private void FollowHeard(FollowInput input, POINT at)
    {
        switch (input)
        {
            case FollowInput.Leave:
                EscapeHeard();
                return;
            case FollowInput.Undo:
                EditUndo();
                return;
            case FollowInput.Redo:
                EditRedo();
                return;
        }
        if (follow is not { } now)
        {
            return;
        }
        // edit: a copied line follows in its folder (Dock.EditCopy.cs): put down where the folder
        // shows it would land, else where it went in; called off, it comes out again.
        if (now.InFolder is { } open)
        {
            if (input == FollowInput.Place)
            {
                var landed = open.Flyout.LandCarry(at);
                Unfollow();
                if (landed is { Kind: FolderFlyoutAction.Place, Widget: { } line })
                {
                    PlaceInFolder(landed.Keys ?? [line.Key], landed.Place);
                }
            }
            else if (input == FollowInput.Cancel)
            {
                DropFollow();
            }
            return;
        }
        var holder = windows.Find(window => window.CarryingKey == now.Key);
        switch (input)
        {
            case FollowInput.Place when holder is not null:
                holder.LandFollow(at);
                break;
            case FollowInput.Place:
                FollowLanded(now.Key);
                break;
            case FollowInput.Cancel:
                if (holder is null || !holder.CancelDrag())
                {
                    DropFollow();
                }
                break;
        }
    }

    /// <summary>
    /// edit: Escape in edit mode, wherever the keys were (EditFollowHook.cs), as the bar being edited
    /// has it (EditKey): a block being dragged goes back, a widget following is called off, and
    /// where there is neither, edit mode ends.
    /// </summary>
    private void EscapeHeard()
    {
        if (editor is null)
        {
            return;
        }
        var cancelled = false;
        foreach (var window in windows.ToList())
        {
            cancelled |= window.CancelDrag();
        }
        // §551: a row of the settings panel dragged into order is carried too: put back first.
        foreach (var chrome in chromes.Values)
        {
            cancelled |= chrome.Deck.CancelReorder();
        }
        // edit: the one keymap's Escape (EditKeys.cs), as an open folder's: what is carried goes back,
        // then the marching ants (Dock.EditCopy.cs), then an open folder, then edit mode ends.
        EscapeTo(EscapeStep(cancelled || DropFollow()));
    }

    /// <summary>
    /// edit: Escape's step now (<see cref="EditKeys.Escape"/>), from the inside out: what is carried (<paramref name="carrying"/>,
    /// put back already), the marching ants, §520 the search's words, §575 the flyout open, an open folder, edit mode.
    /// </summary>
    private EditEscape EscapeStep(bool carrying)
    {
        var deck = panelOn is { } on && chromes.TryGetValue(on, out var chrome) ? chrome.Deck : null;
        return EditKeys.Escape(carrying, copied: CopiedInSight(), folderOpen: openFolders.Count > 0, searching: deck is { HasWords: true }, flyoutOpen: panelMemory.Page is not null || deck is { FlyoutOpen: true });
    }

    /// <summary>edit: Escape's step taken (<see cref="EditKeys.Escape"/>), what was carried already put back.</summary>
    private void EscapeTo(EditEscape step)
    {
        switch (step)
        {
            case EditEscape.Uncopy:
                _ = ClearCopied();
                break;
            case EditEscape.Leave:
                LeaveEdit();
                break;
            // §520: the search's words, then §575 the flyout open.
            case EditEscape.ClearSearch or EditEscape.CloseFlyout:
                _ = PanelEscape(step);
                break;
            // edit: the folder closes first, as its own Escape closes it; the edit bar comes back
            // (the edit chrome, EditChrome), and the next Escape leaves.
            case EditEscape.CloseFolder:
                foreach (var open in openFolders.ToArray())
                {
                    CloseFolder(open);
                }
                break;
        }
    }

    /// <summary>
    /// edit: whether the window with the keys closes on Escape itself, so edit mode's hook lets it
    /// have it (<see cref="EscapeTheirs"/>); a bar, a frame or an edit bar never does.
    /// </summary>
    private bool EscapeIsTheirs() =>
        EscapeTheirs(GetForegroundWindow(), window => windows.Exists(bar => bar.Hwnd == window) || DeckWindow(window) || RailWindow(window));

    /// <summary>
    /// edit: whether <paramref name="foreground"/>, the window with the keys, closes on Escape itself:
    /// one of this thread's popups standing open — a menu, the add list, an open folder
    /// (<see cref="FlyoutHost.OpenMenus"/>) — or a task dialog of its own; never one of edit mode's
    /// surfaces (<paramref name="surface"/>), which leave edit mode on it.
    /// <para>
    /// Not any window of the thread (the person, 2026-10-19: "when i am in the folder edit mode and
    /// click esc i cannot click esc again to get out of edit mode"): a folder opened from a menu's
    /// row hands the keys back, as it closes on Escape, to the window that had them as it opened —
    /// that menu, closed and out of sight by then — and the folder itself keeps them through its fade.
    /// Either is the dock's own, and Escape went to it, which nothing there heard. A popup closed or
    /// hidden has nothing to close: the hook takes Escape, and edit mode ends.
    /// </para>
    /// </summary>
    internal static bool EscapeTheirs(HWND foreground, Func<HWND, bool> surface)
    {
        if (foreground == HWND.NULL || GetWindowThreadProcessId(foreground, null) != GetCurrentThreadId()
            || !IsWindowVisible(foreground) || surface(foreground))
        {
            return false;
        }
        if (Array.Exists(FlyoutHost.OpenMenus, host => host.Hwnd == foreground))
        {
            return true;
        }
        // A task dialog (TaskDialogs.cs): Windows' own dialog class.
        var name = stackalloc char[8];
        return GetClassNameW(foreground, name, 8) == 6 && new string(name, 0, 6) == "#32770";
    }

    /// <summary>edit: whether <paramref name="key"/> is the widget following the pointer.</summary>
    private bool Follows(string? key) => key is not null && follow?.Key == key;


    /// <summary>
    /// edit: the widget following the pointer has been put down — wherever the drop put it — and is
    /// following no more.
    /// </summary>
    private void FollowLanded(string key)
    {
        if (follow is not { } landed || landed.Key != key)
        {
            return;
        }
        Unfollow();
    }

    /// <summary>
    /// edit: the widget following the pointer is called off — Escape, a right click, edit mode ending:
    /// it comes out of the list again, and the pasted copies with it. False where none was following.
    /// </summary>
    private bool DropFollow()
    {
        if (follow is not { } called)
        {
            return false;
        }
        Unfollow();
        // edit: a copied line comes out of its folder again (Dock.EditCopy.cs), and the lines copied
        // or pasted with it.
        if (called.InFolder is { } open)
        {
            open.Flyout.EndCarry();
            var without = config.Clone();
            if (RemoveFrom(without, called.Together ?? [called.Key]).Count > 0)
            {
                Apply(without);
            }
            editSelection.Keep(key => config.FindIsland(Islands.OwnerOf(key), Islands.IdOf(key)) is not null);
            ShowSelection();
            return true;
        }
        foreach (var window in windows)
        {
            if (window.CarryingKey == called.Key)
            {
                window.CancelDrag();
            }
        }
        var next = config.Clone();
        var list = next.ListFor(Islands.OwnerOf(called.Key));
        // edit: pasted copies following together all come out again.
        if (called.Together is { } copies)
        {
            list.RemoveAll(entry => entry is not null && copies.Any(key => string.Equals(entry.Id, Islands.IdOf(key), StringComparison.OrdinalIgnoreCase)));
            Apply(next);
            editSelection.Keep(key => config.FindIsland(Islands.OwnerOf(key), Islands.IdOf(key)) is not null);
            ShowSelection();
            return true;
        }
        if (list.Find(island => island is not null && string.Equals(island.Id, Islands.IdOf(called.Key), StringComparison.OrdinalIgnoreCase)) is not { } island)
        {
            return true;
        }
        list.Remove(island);
        Apply(next);
        return true;
    }

    private sealed partial class DockWindow
    {
        /// <summary>edit: the key of the block this window is carrying, if any.</summary>
        public string? CarryingKey => dragKey;

        /// <summary>
        /// edit: carries the block <paramref name="key"/> as a drag does, from wherever the pointer is:
        /// a widget just taken from the add list (Dock.EditFollow.cs), and
        /// <paramref name="together"/> with it where several follow (pasted copies), carried together
        /// as several selected are (Dock.EditSelect.cs). Held by its middle, and past the drag threshold
        /// already, so it shows where it would land from the start. False where it has no block here.
        /// </summary>
        public bool Follow(string key, IReadOnlyList<string>? together = null)
        {
            var part = layout.PartIndex(key);
            if (part < 0 || !layout.Placed(part))
            {
                return false;
            }
            EndDrag();
            pointer = Local(Cursor());
            // Held by the middle of its block, which is what the carry window shows of it (Scene.Carried).
            var block = layout.BlockRect(part);
            dragKey = key;
            group = together;
            focusKey = key;
            focusShown = false;
            grabOffset = layout.PartAt(part).Width / 2f;
            grabAt = new Vector2(block.Width / 2f, block.Height / 2f);
            pressAt = pointer;
            dragMoved = true;
            SetCapture(Hwnd);
            SetCursor(LoadCursorW(HINSTANCE.NULL, IDC.IDC_SIZEALL));
            // The poll that follows the pointer where no button is held (DRAG), and Escape.
            Arm(DRAG, DRAG_MS);
            scene.Edit(EditView());
            DragMoved();
            return true;
        }

        /// <summary>
        /// edit: the widget following the pointer is put down by a press at <paramref name="at"/>
        /// (screen px), heard by the hook wherever it landed: where it shows it would land, as the
        /// release of a drag lands a block.
        /// </summary>
        public void LandFollow(POINT at)
        {
            if (!dock.Follows(dragKey))
            {
                return;
            }
            var local = Local(at);
            Moved(local.X, local.Y);
            EditReleased(local.X, local.Y);
        }

        /// <summary>
        /// edit: the pointer where no button is held goes to the window under it, not to this one —
        /// another app's, past the dim — so a block following it is moved by the drag's tick too.
        /// </summary>
        private void FollowTick()
        {
            if (!dock.Follows(dragKey))
            {
                return;
            }
            var at = Local(Cursor());
            if ((int)MathF.Round(at.X) != (int)MathF.Round(pointer.X) || (int)MathF.Round(at.Y) != (int)MathF.Round(pointer.Y))
            {
                Moved(at.X, at.Y);
            }
        }
    }
}
