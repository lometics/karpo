// app clicks: the rest of what Windows 11's taskbar does with its app buttons (docs\widgets.md §362),
// the dock's side of it:
//
// - The window list a click or the keys put up (held): a plain click on a button of several windows
//   shows their thumbnails and brings nothing forward (AppClicks); the list stays whatever the pointer
//   does, until a card is chosen, the button is clicked again, a press lands anywhere else (PressHook,
//   as a menu's), Esc is pressed (a hot key, taken only while it is held), or another window comes
//   forward. Win+T's focus on a button puts its list up the same way, and the arrows go into it.
// - A card's click is the rule of a button of one window — the window in front minimizes, any other
//   comes forward —; its right click is its window's own menu.
// - Files dragged onto a button of several windows rest there for Windows' hover time: their list
//   opens, and resting on a card brings that window forward, so the files can be dropped into it.
// - Attention: the button blinks Windows' flash count, then stays lit, until its window is activated
//   or the flashing stops (AttentionFlash).
// - An app run as administrator that would not be closed says so at its button (Apps.Close).
// - Alt+Shift+arrow moves the pinned button the keys are on; Win+Alt+Enter on it opens the taskbar's
//   settings (edit mode, Layout ▾'s Taskbar section).
// - ITaskbarList::ActivateTab / SetActiveAlt mark a button active (Front).

using Karpo.Core;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using ERROR = TerraFX.Interop.Windows.ERROR;

namespace Karpo.Dock;

public sealed unsafe partial class Dock
{
    /// <summary>app clicks: the hot key id Esc is taken by while a list is held (WM_HOTKEY on its bar).</summary>
    private const int EscapeHotkey = 0x4B31;

    /// <summary>app clicks: the window list's own timer for files resting on a card (the popup's are Preview's 1 and 2).</summary>
    private const nuint CardDragTimer = 3;

    /// <summary>signals: a host timer: the next beat of a blinking button, or a look at whether a flashing stopped.</summary>
    private const uint ATTENTION = 91;

    /// <summary>app clicks: Windows' Explorer\Advanced, where LastActiveClick and TaskbarFlashing are.</summary>
    private const string AdvancedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";

    /// <summary>app clicks: the window list is held — a click or the keys put it up — and the pointer's comings and goings leave it.</summary>
    private bool previewHeld;

    /// <summary>app clicks: the bar Esc is taken on while the list is held; null while it is not.</summary>
    private HWND escapeOn;

    /// <summary>app clicks: the card the keys are on in a held list, -1 while they are on the button.</summary>
    private int previewKeyCard = -1;

    /// <summary>app clicks: files dragged over the window list, resting on one of its cards.</summary>
    private DragHover? cardDrag;

    /// <summary>signals: Windows' "Show flashing on taskbar apps" (TaskbarFlashing), read as a flashing begins.</summary>
    private bool flashingShown = true;

    /// <summary>signals: Windows' foreground flash count and the caret's blink time, read as a flashing begins.</summary>
    private (int Count, uint BeatMs) flashing = (7, 530);

    /// <summary>A window an app marked active itself (ITaskbarList::ActivateTab, SetActiveAlt); null for none.</summary>
    private static HWND ActiveAltWindow => (HWND)AppSignals.Shared.ActiveAlt;

    // --- The list held -------------------------------------------------------------------------

    /// <summary>
    /// app clicks: the list up now is held: no wait or grace of the pointer's closes it any more; a
    /// press anywhere else does (<see cref="PreviewPressed"/>), and so does Esc, taken on its bar for
    /// as long (a hot key: the bar never has the keys from a click).
    /// </summary>
    private void HoldPreview()
    {
        if (preview is not { IsOpen: true } || previewOwner is not { } owner)
        {
            return;
        }
        owner.KillPreviewTimers();
        if (previewHeld)
        {
            return;
        }
        previewHeld = true;
        PressHook.Also(PreviewPressed);
        if (RegisterHotKey(owner.Hwnd, EscapeHotkey, MOD.MOD_NOREPEAT, VK.VK_ESCAPE))
        {
            escapeOn = owner.Hwnd;
        }
    }

    /// <summary>app clicks: the list is no longer held (it closes): the presses and Esc are let go, and its bar may hide again.</summary>
    private void LetGoPreview()
    {
        previewKeyCard = -1;
        if (!previewHeld)
        {
            return;
        }
        previewHeld = false;
        PressHook.Also(null);
        if (escapeOn != HWND.NULL)
        {
            _ = UnregisterHotKey(escapeOn, EscapeHotkey);
            escapeOn = HWND.NULL;
        }
        previewOwner?.SettleSoon();
    }

    /// <summary>
    /// app clicks: a press at <paramref name="at"/> (screen px) while the list is held: anywhere but the
    /// list and the button it lists — whose own click decides — it closes, as a menu does.
    /// </summary>
    private void PreviewPressed(uint message, POINT at)
    {
        if (!previewHeld || preview is not { IsOpen: true } list || previewOwner is not { } owner || !InputDesktop.Holds())
        {
            return;
        }
        if (list.Holds(at) || (list.Tile is { } tile && owner.ButtonAt(at) == tile))
        {
            return;
        }
        ClosePreview(owner);
    }

    /// <summary>app clicks: Esc while the list is held: it closes; the keys, where they are on the bar, stay on its button.</summary>
    private void PreviewEscape()
    {
        if (previewOwner is { } owner)
        {
            ClosePreview(owner);
        }
    }

    // --- Cards ---------------------------------------------------------------------------------

    /// <summary>
    /// app clicks: card <paramref name="card"/> clicked (or Enter on it): the rule of a button of one
    /// window — the window in front minimized, any other brought forward, from another desktop too — and
    /// the list put away. A tab's card brings its window forward and tells the tab; it never minimizes,
    /// as the tab need not be the one its window shows (Dock.Signals.cs).
    /// </summary>
    private void CardClicked(DockWindow? owner, HWND card)
    {
        var window = TabsWindow(card);
        var decision = AppClicks.Decide([new AppWindow((nint)window, IsIconic(window))], (nint)Front(), AppAsk.Usual);
        ClosePreview(owner);
        if (decision.Kind == AppClickKind.Minimize && window == card)
        {
            Apps.Minimize(window);
            return;
        }
        Bring(window);
        TabActivated(card);
    }

    /// <summary>
    /// app clicks: card <paramref name="card"/> right-clicked (or the menu key on it): its window's own
    /// menu at the pointer, as Shift+right click on a button of one window opens it (<see cref="WindowMenu"/>).
    /// </summary>
    private void CardMenu(DockWindow? owner, HWND card, POINT? at = null)
    {
        if (owner is null)
        {
            return;
        }
        var window = TabsWindow(card);
        var tile = preview?.Tile ?? -1;
        var lines = WindowMenus.Rows(WindowMenus.Read(window), WindowMenus.ShapeOf(window));
        ClosePreview(owner);
        if (lines.Count == 0)
        {
            return;
        }
        var hit = tile >= 0 ? Hit.Tile(tile) : (Hit?)null;
        ShowFlyout(new Pending(Topic.Window, owner) { Hit = hit, Open = [window] }, lines, FlyoutLayout.List, at ?? Cursor());
    }

    // --- The keys in the list (Win+T) ----------------------------------------------------------

    /// <summary>
    /// keys: the keyboard is on <paramref name="window"/>'s <paramref name="hit"/>: a button with windows
    /// shows its list, held, as Windows' taskbar shows the thumbnails of the button Win+T is on; any
    /// other stop puts a list the keys held away.
    /// </summary>
    private void KeysList(DockWindow window, Hit? hit)
    {
        if (hit is { Kind: HitKind.Tile } on && window.TileAt(on.Index) is { Open.Count: > 0 })
        {
            if (!(previewOwner == window && preview is { IsOpen: true } list && list.Tile == on.Index))
            {
                ClosePreview(window);
                OpenPreview(window, on.Index, held: true);
            }
            previewKeyCard = -1;
            preview?.Select(-1);
            return;
        }
        if (previewOwner == window && PreviewShowing)
        {
            ClosePreview(window);
        }
    }

    /// <summary>
    /// keys: key <paramref name="vk"/> while the keyboard is on button <paramref name="hit"/> of
    /// <paramref name="window"/> and its list is up: the arrow away from the bar goes into the cards,
    /// the arrows along the bar walk them, the one towards the bar comes back to the button; Enter
    /// is a card's click, Delete closes its window, the menu key opens its window's menu. True when the
    /// key was the list's.
    /// </summary>
    private bool ListKey(DockWindow window, Hit hit, uint vk)
    {
        if (hit.Kind != HitKind.Tile || previewOwner != window || preview is not { IsOpen: true } list || list.Tile != hit.Index || list.Cards == 0)
        {
            return false;
        }
        var step = ListStep(window.Layout.Edge, vk);
        if (previewKeyCard < 0)
        {
            if (step != ListMove.Into)
            {
                return false;
            }
            previewKeyCard = 0;
            list.Select(0);
            return true;
        }
        switch (step)
        {
            case ListMove.Back or ListMove.On:
                previewKeyCard = FocusNav.Step(list.Cards, previewKeyCard, step == ListMove.Back ? -1 : 1);
                list.Select(previewKeyCard);
                return true;
            case ListMove.Out:
                previewKeyCard = -1;
                list.Select(-1);
                return true;
            case ListMove.Into:
                return true;
        }
        if (list.CardWindow(previewKeyCard) is not { } card)
        {
            return false;
        }
        switch (vk)
        {
            case VK.VK_RETURN or VK.VK_SPACE:
                CardClicked(window, card);
                return true;
            case VK.VK_DELETE:
                Apps.Close(TabsWindow(card));
                return true;
            case VK.VK_APPS:
                CardMenu(window, card, window.Anchor(hit));
                return true;
        }
        return false;
    }

    /// <summary>keys: what an arrow does in a list over a bar at <paramref name="edge"/>.</summary>
    public enum ListMove
    {
        None,

        /// <summary>Away from the bar: from the button into the cards.</summary>
        Into,

        /// <summary>Towards the bar: from the cards back to the button.</summary>
        Out,

        /// <summary>Along the bar, back: the card before.</summary>
        Back,

        /// <summary>Along the bar, on: the card after.</summary>
        On,
    }

    /// <summary>keys: the move arrow <paramref name="vk"/> makes in a list over a bar at <paramref name="edge"/>; None for any other key.</summary>
    internal static ListMove ListStep(Edge edge, uint vk) => (edge, vk) switch
    {
        (Edge.Bottom, VK.VK_UP) or (Edge.Top, VK.VK_DOWN) or (Edge.Left, VK.VK_RIGHT) or (Edge.Right, VK.VK_LEFT) => ListMove.Into,
        (Edge.Bottom, VK.VK_DOWN) or (Edge.Top, VK.VK_UP) or (Edge.Left, VK.VK_LEFT) or (Edge.Right, VK.VK_RIGHT) => ListMove.Out,
        (Edge.Bottom or Edge.Top, VK.VK_LEFT) or (Edge.Left or Edge.Right, VK.VK_UP) => ListMove.Back,
        (Edge.Bottom or Edge.Top, VK.VK_RIGHT) or (Edge.Left or Edge.Right, VK.VK_DOWN) => ListMove.On,
        _ => ListMove.None,
    };

    /// <summary>
    /// keys: Alt+Shift+arrow on button <paramref name="hit"/>: a pinned one moves a place along the bar
    /// that way (its pin widget, as edit mode's Ctrl+arrow moves it), the keys going with it. An open
    /// app nothing pins keeps its place: its button stands where its window first came.
    /// </summary>
    private void MoveButton(DockWindow window, Hit hit, uint vk)
    {
        var along = window.Layout.Vertical ? vk is VK.VK_UP or VK.VK_DOWN : vk is VK.VK_LEFT or VK.VK_RIGHT;
        if (!along || hit.Kind != HitKind.Tile || window.TileAt(hit.Index) is not { Pinned: true, Key: { } key })
        {
            return;
        }
        ClosePreview(window);
        StepIsland(key, KeyStops.Arrow(vk));
        if (window.Tiles.FindIndex(tile => tile.Pinned && string.Equals(tile.Key, key, StringComparison.OrdinalIgnoreCase)) is >= 0 and var moved)
        {
            window.KeyFocus(Hit.Tile(moved), KeyRegion.Apps);
        }
    }

    // --- Files dragged onto a list -------------------------------------------------------------

    /// <summary>app clicks: the window list's popup, just made, hears files dragged over it (a drop target that takes nothing).</summary>
    private static Preview Made(Preview made)
    {
        var drop = DropTarget.Create(made.Hwnd, refuses: true);
        _ = RegisterDragDrop(made.Hwnd, drop);
        _ = drop->Release();
        return made;
    }

    /// <summary>
    /// app clicks: files dragged over the window list (its drop target, which takes nothing dropped):
    /// the card under them lights as under the pointer, and one they rest on for Windows' hover time
    /// brings its window forward (<see cref="CardDragDue"/>).
    /// </summary>
    private PreviewEvent PreviewDragged(Preview list, Drag drag)
    {
        var at = drag.At;
        _ = ScreenToClient(list.Hwnd, &at);
        if (drag.Phase is DragPhase.Enter or DragPhase.Over)
        {
            var happened = list.Moved(at.x, at.y);
            CardDragOver(list, list.CardAt(at.x, at.y));
            return happened;
        }
        CardDragOver(list, -1);
        return list.Left();
    }

    private void CardDragOver(Preview list, int card)
    {
        cardDrag ??= new DragHover(HoverTimeMs);
        if (cardDrag.Over(card, Environment.TickCount64) is { } wait)
        {
            _ = SetTimer(list.Hwnd, CardDragTimer, Math.Max(1u, wait), null);
        }
        else
        {
            _ = KillTimer(list.Hwnd, CardDragTimer);
        }
    }

    /// <summary>app clicks: files rested on a card long enough: its window comes forward, the list going, so they can be dropped into it.</summary>
    private void CardDragDue(Preview list)
    {
        _ = KillTimer(list.Hwnd, CardDragTimer);
        if (cardDrag?.Due(Environment.TickCount64) is >= 0 and var card && list.CardWindow(card) is { } window)
        {
            cardDrag.Leave();
            ClosePreview(previewOwner);
            Bring(TabsWindow(window));
            TabActivated(window);
        }
    }

    // --- Shell messages: attention, the marks apps make ----------------------------------------

    /// <summary>
    /// app clicks: shell message <paramref name="code"/> on <paramref name="window"/>, after the windows
    /// took it: another window in front ends the mark an app made (ActivateTab, §619) and a list held
    /// by a click; a flash or a redraw keeps the blinking going (<see cref="AttentionDue"/>).
    /// </summary>
    private void AppsHeard(uint code, HWND window)
    {
        if (code is (uint)HSHELL.HSHELL_WINDOWACTIVATED or Apps.HSHELL_RUDEAPPACTIVATED && window != HWND.NULL && !OursWindow(window))
        {
            uint activated = 0;
            uint marked = 0;
            _ = GetWindowThreadProcessId(window, &activated);
            if (ActiveAltWindow != HWND.NULL)
            {
                _ = GetWindowThreadProcessId(ActiveAltWindow, &marked);
            }
            var button = WindowOwners.ButtonOf(window, hwnd => apps.Find(hwnd) is not null);
            _ = AppSignals.Shared.ActiveAltEnds((nint)button, apps.Find(button) is not null, activated == marked);
            // As Windows' does: a window brought forward any other way (Alt+Tab) puts a held list away.
            if (previewHeld && previewOwner is { } owner && keyed?.Window != owner)
            {
                ClosePreview(owner);
            }
        }
        if (code == Apps.HSHELL_FLASH)
        {
            flashingShown = Advanced("TaskbarFlashing") != 0;
            uint count = 7;
            _ = SystemParametersInfoW(SPI_GETFOREGROUNDFLASHCOUNT, 0, &count, 0);
            var beat = GetCaretBlinkTime();
            flashing = ((int)Math.Clamp(count, 0u, 100u), beat is 0 or uint.MaxValue ? 530u : Math.Clamp(beat, 100u, 2000u));
        }
        if (code is Apps.HSHELL_FLASH or (uint)HSHELL.HSHELL_REDRAW)
        {
            AttentionSoon();
        }
    }

    /// <summary>SPI_GETFOREGROUNDFLASHCOUNT: how many times Windows flashes a button whose app was refused the foreground.</summary>
    private const uint SPI_GETFOREGROUNDFLASHCOUNT = 0x2004;

    /// <summary>signals: whether <paramref name="win"/>'s button shows its attention lit at <paramref name="now"/>: flashing, Windows' flashing on, and on a lit beat.</summary>
    private bool AttentionLit(Win? win, long now) =>
        win is { Attention: true } && flashingShown && AttentionFlash.Lit(now - win.AttentionAt, flashing.Count, flashing.BeatMs);

    /// <summary>signals: whether one of <paramref name="open"/> just began asking for attention, when its button bounces once.</summary>
    private bool AttentionBegan(IEnumerable<HWND> open)
    {
        var now = Environment.TickCount64;
        return flashingShown && open.Any(hwnd => apps.Get(hwnd) is { Attention: true } win && AttentionFlash.Began(now - win.AttentionAt, flashing.BeatMs));
    }

    /// <summary>
    /// signals: the beat timer set for the next moment a button changes by itself — its next blink, or
    /// a flashing's stop settling (<see cref="AttentionFlash.NextMs"/>) —, and off while none will: a
    /// button lit for good costs nothing until a message comes.
    /// </summary>
    private void AttentionSoon()
    {
        var now = Environment.TickCount64;
        long? wait = null;
        // Windows' flashing off: no button shows any, so nothing is drawn again for it.
        foreach (var win in flashingShown ? apps.All : [])
        {
            if (win.Attention && AttentionFlash.NextMs(now - win.AttentionAt, win.AttentionOffAt == 0 ? null : now - win.AttentionOffAt, flashing.Count, flashing.BeatMs) is { } ms)
            {
                wait = wait is { } sooner ? Math.Min(sooner, ms) : ms;
            }
        }
        if (wait is { } next)
        {
            Arm(ATTENTION, (uint)Math.Clamp(next, 1, AttentionFlash.SettleMs));
        }
        else
        {
            Kill(ATTENTION);
        }
    }

    /// <summary>signals: a beat: the flashings that stopped end, and the blinking buttons are lit or dark again.</summary>
    private void AttentionDue()
    {
        var now = Environment.TickCount64;
        var change = apps.SettleAttention(now);
        var blinking = apps.All.Any(win => win.Attention && AttentionFlash.Blinking(Math.Max(0, now - win.AttentionAt - flashing.BeatMs), flashing.Count, flashing.BeatMs));
        if (change != Change.None || blinking)
        {
            WindowsChanged(change == Change.None ? Change.States : change);
        }
        AttentionSoon();
    }

    /// <summary>app clicks: a DWORD of Windows' Explorer\Advanced; null where it is not set.</summary>
    private static uint? Advanced(string name)
    {
        uint data = 0;
        var size = (uint)sizeof(uint);
        fixed (char* key = AdvancedKey)
        fixed (char* value = name)
        {
            return RegGetValueW(HKEY.HKEY_CURRENT_USER, key, value, (uint)RRF.RRF_RT_REG_DWORD, null, &data, &size) == ERROR.ERROR_SUCCESS ? data : null;
        }
    }

    /// <summary>app clicks: Windows' LastActiveClick, read with each click: 1 has a click on several walk them (<see cref="AppClicks.Lists"/>).</summary>
    private static uint? LastActiveClick => Advanced("LastActiveClick");

    // --- Every desktop's windows (§362) -------------------------------------------------------

    /// <summary>
    /// desktops: the windows are listed with every virtual desktop's while any layout shows them
    /// (<see cref="TaskbarLayout.AllDesktops"/>), each bar taking those its own layout shows (TilesFor);
    /// read again where that changed.
    /// </summary>
    private void SyncAllDesktops()
    {
        var all = config.Layouts.Any(layout => layout.AllDesktops);
        if (apps.AllDesktops == all)
        {
            return;
        }
        apps.AllDesktops = all;
        Guid? current = desktops is { } state && state.Current < state.Ids.Count ? state.Ids[state.Current] : null;
        _ = apps.Scan(current);
    }

    // --- An app run as administrator ----------------------------------------------------------

    /// <summary>
    /// app clicks: Windows would not let Karpo close <paramref name="hwnd"/>, an app run as administrator
    /// (Apps.Close): its button says so, in its tooltip, for a few seconds.
    /// </summary>
    private void CantClose(HWND hwnd)
    {
        if (!IsWindow(hwnd))
        {
            return;
        }
        var title = apps.Get(hwnd)?.Title ?? "";
        foreach (var window in windows)
        {
            var index = window.Tiles.FindIndex(tile => tile.Open.Contains(hwnd));
            if (index >= 0)
            {
                window.Say(S.DockWindowMenuCantClose(window.Tiles[index].Item.Name is { Length: > 0 } name ? name : title), Hit.Tile(index));
                return;
            }
        }
    }

    private sealed partial class DockWindow
    {
        /// <summary>app clicks: a bar timer: what <see cref="Say"/> put up goes.</summary>
        private const uint SAID = 23;

        /// <summary>app clicks: how long <see cref="Say"/> stands.</summary>
        private const uint SaidMs = 5000;

        /// <summary>app clicks: <paramref name="text"/> in a tooltip at <paramref name="hit"/>, for a few seconds, the bar coming into view for it.</summary>
        public void Say(string text, Hit hit)
        {
            if (!Shown && !Fullscreen)
            {
                Peek();
            }
            scene.Tooltip(dock.gfx, text, hit);
            Arm(SAID, SaidMs);
        }

        /// <summary>app clicks: the time of what <see cref="Say"/> put up is over.</summary>
        private void Unsay()
        {
            Kill(SAID);
            if (!hovering)
            {
                scene.Tooltip(dock.gfx, null, null);
            }
        }

        /// <summary>app clicks: the button at screen point <paramref name="screen"/>, -1 for none (a held list's press, Dock.PreviewPressed).</summary>
        public int ButtonAt(POINT screen)
        {
            var local = Local(screen);
            return Over(local.X, local.Y) && layout.HitTest(local.X, local.Y, Editing) is { Kind: HitKind.Tile } over ? over.Index : -1;
        }

        /// <summary>app clicks: a held list closed: the bar hides again in a moment, where it hides and the pointer is not on it.</summary>
        public void SettleSoon()
        {
            if (!hovering)
            {
                leaving = true;
                Arm(SETTLE, HIDE_DELAY_MS);
            }
        }
    }
}
