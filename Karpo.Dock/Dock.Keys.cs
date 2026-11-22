// keys: the dock's side of the taskbar's keys (TaskbarKeys.cs, where the rules are): the hook put
// in while Karpo's bars are the taskbar, what a chord does to the bar's app buttons, and the
// keyboard on a bar — Win+T on its app buttons, Win+B on its tray.
//
// The keyboard on a bar: the bar takes the keys (it never does from a click), the focus rectangle
// stands on one of its stops (KeyStops: the app buttons, the tray's icons, and everything else along
// it — Start, search, task view, Windows' buttons, every widget's cells, show desktop), and
//
//   Left, Right, Up, Down   the one before or after it along the bar in its region, round from the end
//   Home, End               the first, the last of its region
//   Tab, Shift+Tab          the next region along the bar, or the one before, round (KeyStops.Regions)
//   Enter, Space            what a click does: the app by the one rule (Shift for another one),
//                           "…" and the chevron open their lists, a tray icon hears NIN_KEYSELECT, a
//                           widget opens its folder or flyout with the keys in it (FolderFlyout.KeysIn)
//   Menu key, Shift+F10     what a right click opens: the jump list, the icon's menu (WM_CONTEXTMENU),
//                           a widget's own menu
//   Esc                     the keys back to the window that had them
//   Alt+Shift+arrows        the pinned button moved a place along the bar (Dock.AppList.cs)
//   Win+Alt+Enter           the taskbar's settings: edit mode, the settings panel's Taskbar page (§520)
//
// A button with windows shows their list while the keys are on it, as Windows' does for Win+T: the
// arrow away from the bar goes into the cards, the arrows along it walk them, the one towards the bar
// comes back; Enter is a card's click, Delete closes its window, the menu key opens its window's menu,
// and Esc puts the list away first (Dock.AppList.cs).
//
// It ends when anything else takes the keys — an app brought forward, a click elsewhere — and not
// when one of Karpo's own flyouts does (a jump list opened from it), which hands them back.

using Karpo.Core;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

public sealed unsafe partial class Dock
{
    /// <summary>keys: the hook that takes the taskbar's chords, while Karpo's bars are the taskbar.</summary>
    private TaskbarKeys? taskbarKeys;

    /// <summary>keys: the bar the keyboard is on, and the window that had the keys before it.</summary>
    private (DockWindow Window, HWND Return)? keyed;

    /// <summary>
    /// keys: the hook in while Karpo's bars are the taskbar — Explorer's hidden —, and out otherwise;
    /// put in again with every arrange, since Windows may have taken it away. The Windows key alone
    /// and Ctrl+Esc are never taken: Windows' own Start opens for them.
    /// </summary>
    private void SyncKeys()
    {
        if (taskbar is not null && windows.Count > 0)
        {
            taskbarKeys ??= new TaskbarKeys(host);
            taskbarKeys.Start();
            return;
        }
        taskbarKeys?.Dispose();
        taskbarKeys = null;
        if (keyed is { } on)
        {
            LeaveKeys(on.Window, giveBack: false);
        }
    }

    /// <summary>keys: the bar the chords are about: the main display's where it has <paramref name="has"/>, else the first that has.</summary>
    private DockWindow? KeysBar(Func<DockWindow, bool> has) =>
        windows.Where(has).OrderByDescending(window => window.Main).FirstOrDefault();

    /// <summary>What a button counts as for Win+N: its app or its pin, the same for every button it stands as.</summary>
    private static string NumberedAs(Tile tile) => $"{tile.Pinned}|{tile.Key}|{tile.Item.Target}";

    /// <summary>keys: the hook posted a chord (TaskbarKeys.WM_KARPO_KEYS).</summary>
    private void ChordMade(nuint packed)
    {
        var chord = TaskbarChord.Unpack(packed);
        switch (chord.Kind)
        {
            case ChordKind.FocusApps:
            case ChordKind.FocusAppsBack:
                if (KeysBar(window => window.Stops(KeyRegion.Apps).Count > 0) is { } apps)
                {
                    EnterKeys(apps, KeyRegion.Apps, chord.Kind == ChordKind.FocusApps ? 1 : -1);
                }
                return;
            case ChordKind.FocusTray:
                if (KeysBar(window => window.Stops(KeyRegion.Tray).Count > 0) is { } trayBar)
                {
                    EnterKeys(trayBar, KeyRegion.Tray, 0);
                }
                return;
        }
        if (KeysBar(window => window.Tiles.Count > 0) is not { } bar
            || AppNumbers.Nth([.. bar.Tiles.Select(NumberedAs)], chord.Number) is not { } buttons)
        {
            // Past the last app, Win+N does nothing, as on Windows' taskbar.
            return;
        }
        var first = bar.Tiles[buttons.First];
        if (first.Broken is not null)
        {
            if (!bar.Shown)
            {
                bar.Peek();
            }
            BrokenFlyout(bar, buttons.First, first);
            return;
        }
        // Every window of the app's buttons on this bar, most recently used first.
        var open = bar.Tiles.Skip(buttons.First).Take(buttons.Count)
            .SelectMany(tile => tile.Open)
            .Distinct()
            .OrderBy(apps.Recency)
            .ToList();
        ClosePreview(bar);
        switch (chord.Kind)
        {
            case ChordKind.App:
                OpenApp(first.Item, open, AppAsk.Usual, () => bar.Scene.Bounce(buttons.First));
                break;
            case ChordKind.NewApp:
                OpenApp(first.Item, open, AppAsk.New, () => bar.Scene.Bounce(buttons.First));
                break;
            case ChordKind.ElevatedApp:
                OpenApp(first.Item, open, AppAsk.Elevated, () => bar.Scene.Bounce(buttons.First));
                break;
            case ChordKind.CycleApp:
            {
                var windowsOf = open.Select(hwnd => new AppWindow((nint)hwnd, IsIconic(hwnd))).ToList();
                var decision = AppKeys.Cycle(windowsOf, (nint)Front());
                if (decision.Kind == AppClickKind.Launch)
                {
                    OpenApp(first.Item, open, AppAsk.Usual, () => bar.Scene.Bounce(buttons.First));
                }
                else
                {
                    Bring((HWND)windowsOf[decision.Window].Handle);
                }
                break;
            }
            case ChordKind.JumpList:
                if (!bar.Shown)
                {
                    bar.Peek();
                }
                AppMenu(bar, buttons.First);
                break;
        }
    }

    /// <summary>
    /// keys: the keyboard on <paramref name="window"/>'s <paramref name="region"/>: the first stop
    /// going forward, the last going back, the first of the tray for 0; on it already, the next
    /// stop by <paramref name="step"/>, as Win+T pressed again walks the buttons.
    /// </summary>
    private void EnterKeys(DockWindow window, KeyRegion region, int step)
    {
        var stops = window.Stops(region);
        if (stops.Count == 0)
        {
            return;
        }
        var again = keyed is { } on && on.Window == window && window.KeyHit is not null && window.KeyRegion == region;
        var index = again && step != 0
            ? FocusNav.Step(stops.Count, stops.IndexOf(window.KeyHit!.Value), step)
            : step >= 0 ? 0 : stops.Count - 1;
        if (keyed is { } other && other.Window != window)
        {
            other.Window.KeyFocus(null, region);
        }
        var had = GetForegroundWindow();
        var giveBack = keyed?.Return ?? HWND.NULL;
        if (!windows.Any(bar => bar.Hwnd == had) && !OursWindow(had))
        {
            giveBack = had;
        }
        keyed = (window, giveBack);
        if (!window.Shown)
        {
            window.Peek();
        }
        // The keys first, so a screen reader hears the focus land on a bar that has them.
        Apps.Activate(window.Hwnd);
        window.KeyFocus(stops[index], region);
        // app clicks: and a button with windows shows their list (Dock.AppList.cs).
        KeysList(window, stops[index]);
    }

    /// <summary>
    /// narrator: the keyboard on <paramref name="hit"/> of <paramref name="window"/>, where a screen
    /// reader put the focus (UI Automation's SetFocus): its region, at that stop.
    /// </summary>
    private void EnterKeysAt(DockWindow window, Hit hit)
    {
        if (KeyStops.RegionOf(hit) is not { } region || !window.Stops(region).Contains(hit))
        {
            return;
        }
        if (keyed is not { } on || on.Window != window)
        {
            EnterKeys(window, region, 1);
        }
        window.KeyFocus(hit, region);
        KeysList(window, hit);
    }

    /// <summary>keys: the keyboard leaves the bar; <paramref name="giveBack"/> hands the keys to the window that had them (Esc).</summary>
    private void LeaveKeys(DockWindow window, bool giveBack)
    {
        var back = keyed is { } on && on.Window == window ? on.Return : HWND.NULL;
        keyed = null;
        window.KeyFocus(null, window.KeyRegion);
        // app clicks: a list the keys held goes with them.
        if (previewOwner == window && previewHeld)
        {
            ClosePreview(window);
        }
        if (giveBack && back != HWND.NULL && IsWindow(back) && GetForegroundWindow() == window.Hwnd)
        {
            SetForegroundWindow(back);
        }
    }

    /// <summary>Whether <paramref name="hwnd"/> is one of this process's windows: a flyout, a menu, the rail.</summary>
    private static bool OursWindow(HWND hwnd)
    {
        if (hwnd == HWND.NULL)
        {
            return false;
        }
        uint process = 0;
        _ = GetWindowThreadProcessId(hwnd, &process);
        return process == GetCurrentProcessId();
    }

    /// <summary>
    /// keys: the bar lost the activation to <paramref name="to"/>. To anything but Karpo's own
    /// windows the keyboard leaves it; a flyout opened from it (a jump list) hands the keys back
    /// when it closes, and the keyboard is where it was.
    /// </summary>
    private void KeysDeactivated(DockWindow window, HWND to)
    {
        if (keyed is { } on && on.Window == window && !OursWindow(to))
        {
            LeaveKeys(window, giveBack: false);
        }
    }

    /// <summary>keys: a key on the bar the keyboard is on (see the top of the file). True when it was the keyboard's.</summary>
    private bool KeysKey(DockWindow window, uint vk)
    {
        if (keyed is not { } on || on.Window != window || window.KeyHit is not { } hit)
        {
            return false;
        }
        var region = window.KeyRegion;
        var stops = window.Stops(region);
        var at = stops.IndexOf(hit);
        var shift = KeyState.Down(VK.VK_SHIFT);
        var alt = KeyState.Down(VK.VK_MENU);
        // app clicks: Alt+Shift+arrow moves the button the keys are on, Win+Alt+Enter opens the
        // taskbar's settings, as on Windows 11's taskbar; the arrows, Enter and Delete go into an
        // open list (Dock.AppList.cs).
        if (alt && shift && vk is VK.VK_LEFT or VK.VK_RIGHT or VK.VK_UP or VK.VK_DOWN)
        {
            MoveButton(window, hit, vk);
            return true;
        }
        if (alt && vk == VK.VK_RETURN && (KeyState.Down(VK.VK_LWIN) || KeyState.Down(VK.VK_RWIN)))
        {
            OpenEditMenuLater(window, EditEntry.TaskbarSettings);
            return true;
        }
        if (ListKey(window, hit, vk))
        {
            return true;
        }
        switch (vk)
        {
            case VK.VK_LEFT or VK.VK_RIGHT or VK.VK_UP or VK.VK_DOWN:
                if (FocusNav.Step(stops.Count, at, KeyStops.Arrow(vk)) is var next and >= 0)
                {
                    window.KeyFocus(stops[next], region);
                    KeysList(window, stops[next]);
                }
                return true;
            case VK.VK_HOME or VK.VK_END:
                if (stops.Count > 0)
                {
                    var end = stops[vk == VK.VK_HOME ? 0 : stops.Count - 1];
                    window.KeyFocus(end, region);
                    KeysList(window, end);
                }
                return true;
            case VK.VK_TAB:
            {
                // keys: the next region along the bar, its first stop; back, the one before, its last.
                var other = KeyStops.Next(window.Regions(), region, shift ? -1 : 1);
                var there = window.Stops(other);
                if (there.Count > 0)
                {
                    var to = there[shift && other != region ? there.Count - 1 : 0];
                    window.KeyFocus(to, other);
                    KeysList(window, to);
                }
                return true;
            }
            case VK.VK_RETURN or VK.VK_SPACE:
                KeyAct(window, hit, menu: false);
                return true;
            case VK.VK_APPS:
                KeyAct(window, hit, menu: true);
                return true;
            case VK.VK_F10 when shift:
                KeyAct(window, hit, menu: true);
                return true;
            case VK.VK_ESCAPE:
                LeaveKeys(window, giveBack: true);
                return true;
        }
        return false;
    }

    /// <summary>keys: Enter (or, with <paramref name="menu"/>, the menu key) on <paramref name="hit"/>.</summary>
    private void KeyAct(DockWindow window, Hit hit, bool menu)
    {
        switch (hit.Kind)
        {
            case HitKind.Tile when window.TileAt(hit.Index) is { } tile:
                if (menu)
                {
                    AppMenu(window, hit.Index);
                }
                else
                {
                    TileClicked(window, hit.Index, tile, middle: false);
                }
                break;
            case HitKind.AppOverflow:
                AppOverflowFlyout(window);
                break;
            case HitKind.TrayOverflow:
                OverflowFlyout(window, hit);
                break;
            case HitKind.Tray when tray is not null && TrayIconAt(window, hit) is { } icon:
                PublishTrayRects(window, hit);
                tray.Key(icon, menu, window.Hwnd);
                break;
            // keys: everything else along the bar, as the pointer: its right click's menu, or its click,
            // and a folder that click opens opens with the keys in it (ShowFolder, keysOpening).
            case HitKind.Start or HitKind.Search or HitKind.TaskView or HitKind.WindowsButton or HitKind.Island or HitKind.ShowDesktop:
                if (menu)
                {
                    var rect = window.Layout.ItemRect(hit);
                    Context(window, hit, rect.X + (rect.Width / 2f), rect.Y + (rect.Height / 2f));
                    break;
                }
                keysOpening = true;
                try
                {
                    Click(window, hit, middle: false);
                }
                finally
                {
                    keysOpening = false;
                }
                break;
        }
    }

    /// <summary>keys: a click is being made by the keys (<see cref="KeyAct"/>): a folder it opens takes them.</summary>
    private bool keysOpening;

    private sealed partial class DockWindow
    {
        // keys: where the keyboard is on this bar, and in which of its regions; null while it is not.
        private Hit? keyHit;

        /// <summary>keys: the app button or tray icon the keyboard is on; null while it is not on this bar.</summary>
        public Hit? KeyHit => keyHit;

        /// <summary>keys: the region the keyboard is in.</summary>
        public KeyRegion KeyRegion { get; private set; }

        /// <summary>keys: the stops of <paramref name="region"/> on this bar, along it (KeyStops.Of).</summary>
        public List<Hit> Stops(KeyRegion region) =>
            Karpo.Dock.KeyStops.Of(layout, region, Tiles.Count);

        /// <summary>keys: the regions of this bar that have stops, in their order along it (KeyStops.Regions).</summary>
        public List<KeyRegion> Regions() =>
            Karpo.Dock.KeyStops.Regions(layout, Tiles.Count);

        /// <summary>
        /// keys: the keyboard on <paramref name="hit"/> of <paramref name="region"/>, and its focus
        /// rectangle with it; null takes both away, and a bar that hides by itself hides again.
        /// </summary>
        public void KeyFocus(Hit? hit, KeyRegion region)
        {
            var moved = keyHit != hit;
            keyHit = hit;
            KeyRegion = region;
            scene.KeyFocus(hit);
            if (hit is null && !hovering)
            {
                Arm(SETTLE, 0);
            }
            // narrator: a screen reader says where the keyboard went (Dock.Uia.cs, Accessibility\Uia.cs).
            if (moved && hit is not null)
            {
                Uia.FocusMoved(Hwnd);
            }
        }

        /// <summary>
        /// keys: the bar was built again: the keyboard stays on the same stop where it still is —
        /// an app button by its place, which is the same app unless apps came or went — and else on
        /// the nearest one.
        /// </summary>
        private void KeepKeyFocus()
        {
            if (keyHit is not { } hit)
            {
                return;
            }
            var stops = Stops(KeyRegion);
            if (stops.Count == 0)
            {
                keyHit = null;
                scene.KeyFocus(null);
                return;
            }
            if (!stops.Contains(hit))
            {
                keyHit = hit.Kind == HitKind.Tile
                    ? stops.Where(stop => stop.Kind == HitKind.Tile).OrderBy(stop => Math.Abs(stop.Index - hit.Index)).DefaultIfEmpty(stops[^1]).First()
                    : stops[^1];
            }
            scene.KeyFocus(keyHit);
        }
    }
}
