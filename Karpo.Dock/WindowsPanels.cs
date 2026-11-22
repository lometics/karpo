// search: Windows' own Search and Start panels, as the bar sees them. Two things are asked of
// them:
//
//   * a click on the Search button while Windows Search is open closes it rather than opening it
//     again (SearchToggle), as Windows' own taskbar button does;
//   * any flyout of Karpo's that opens closes Windows Search and Windows' Start first
//     (WindowsPanels.CloseFor, which FlyoutHost.Opening runs), so Karpo's flyout is not opened
//     under them.
//
// What is open is read from the windows: each panel is a Windows.UI.Core.CoreWindow of its own
// process — SearchHost.exe for Search on Windows 11 (SearchApp.exe and SearchUI.exe before it),
// StartMenuExperienceHost.exe for Start — and it is open while that window is visible and not
// cloaked. A closed panel's window is hidden or cloaked by the shell, or not there at all until
// the panel is first opened. The title is not read: it is "Search" and "Start" in English only.
//
// Windows 11 26xxx (seen on 26300, 2026-10-11) shows Start as two windows: Start's own host, a
// CoreWindow the size of the screen, and Search's host over it, which is Start's search box and has
// the foreground the whole time Start is open. So Search's window showing is Start's search box
// while Start's shows too, and Search is open only where Start's does not (PanelRules.Open). Start's
// going is then its cloaking, which comes some 30 ms after it closes (PanelWatch.Cloaked); the
// foreground leaving Search's host is noted as Search's, which no click on Start reads.
//
// Search is closed the way a person closes it from the keyboard: Escape, sent to it once it is the
// foreground window, which it is whenever it is open (the bar never takes the activation). Escape
// is never sent while anything else is in front, so it can't reach another app. Start is closed by
// the Windows key, as Windows' own Start button does: the shell's own toggle, whichever of Start's
// two windows is in front, and no foreground to take first — which the shell does not always give
// a background process, and Start's Escape needed (the person, 2026-10-11: "clicking the start menu
// button again does not close the start menu").
//
// A press on the bar can send a panel away before the dock reads it: the shell may dismiss Start
// and Search as the pointer goes down outside them, and a panel gone by the time the press is read
// looked closed, so the click opened it straight back — the fault the person saw on the Start
// button (2026-10-10: "when i click the start button when the start is already opened it should
// close it again"). So what a panel was at the press is not only read then: WinEvent hooks on the
// dock's thread note when each panel last went — lost the foreground or was cloaked (PanelWatch) —
// by the event's own time, and a panel that went at or after the press's own time (GetMessageTime)
// was open as the button went down and the press closed it. The click then does nothing, and never
// hands the foreground back to a panel on its way out.
//
// windowsMenus: three more of Windows' popups are panels too (§466), so a flyout of Karpo's closes
// them as it opens — one thing open at a time (the person, 2026-11-01: "when i open a karpo folder it
// should close the windows menu / flyout") — and the bar's buttons for them close what they opened on
// a second click, as Start's does. Each holds the foreground for as long as it is open and goes when it
// loses it, so each is read from the foreground window alone, by its process and its class (seen on
// 26300, 2026-11-01), never by looking for its window among the rest:
//
//   * the notifications and calendar (Win+N): a CoreWindow of ShellExperienceHost.exe;
//   * quick settings (Win+A) and its pages — sound output (Win+Ctrl+V), cast (Win+K), Wi-Fi: the
//     ControlCenterWindow of ShellHost.exe;
//   * the quick link menu (Win+X), and Explorer's other menus over the taskbar: a
//     Xaml_WindowedPopupClass of explorer.exe.
//
//   * §473: the Widgets board (Win+W): WidgetBoard.exe's WindowsDashboard, a top-most tool window that
//     may be activated (no WS_EX_NOACTIVATE) and is hidden while the board is closed (seen on 26300,
//     2026-11-01, closed; read only, never opened by a test).
//
// Each is closed as Search is, by Escape, which goes to it because it is the foreground window.
// Clipboard history (Win+V) and the emoji panel (Win+.) never take the foreground and are not read yet.
//
// Which button's press and click toggle which panel is WindowsMenus, one for the bar's glyph buttons and
// Windows' own widgets that wear Windows' face alike (§474: the Widgets button, quick settings' button,
// the clock), so each opens, closes on a second click and gives way to Karpo's flyouts the same way. The
// button of the panel in front is lit while it is, as Windows lights its own (WindowsMenus.Lit, the
// panel in front by the foreground hook, PanelWatch.Front).
//
// Everything that reads or touches the real windows is behind IWindowsPanels; the decisions are
// SearchToggle's, PanelWatch's and CloseFor's, and the tests give them a fake.

using System.Runtime.InteropServices;
using Karpo.Core;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

/// <summary>One of Windows' own panels a Karpo flyout closes.</summary>
public enum WindowsPanel
{
    Search,
    Start,

    /// <summary>windowsMenus: the notifications and calendar (Win+N).</summary>
    Notifications,

    /// <summary>windowsMenus: quick settings (Win+A) and its pages: sound output, cast, Wi-Fi.</summary>
    QuickSettings,

    /// <summary>windowsMenus: the quick link menu (Win+X), and Explorer's other menus over the taskbar.</summary>
    ExplorerMenu,

    /// <summary>§473: the Widgets board (Win+W).</summary>
    WidgetsBoard,
}

/// <summary>What a click on the Search button does (<see cref="SearchToggle.Clicked"/>).</summary>
public enum SearchClick
{
    /// <summary>Opens Windows Search (Win+S): it wasn't open when the button went down.</summary>
    Open,

    /// <summary>Closes Windows Search: it was open when the button went down and still is.</summary>
    Close,

    /// <summary>Nothing: it was open when the button went down, and that press closed it already.</summary>
    Nothing,
}

/// <summary>The seam to Windows' Search and Start panels: whether each is open, and closing it.</summary>
internal interface IWindowsPanels
{
    /// <summary>Whether <paramref name="panel"/> shows now.</summary>
    bool IsOpen(WindowsPanel panel);

    /// <summary>The window of <paramref name="panel"/> while it shows; null while it doesn't.</summary>
    HWND? Window(WindowsPanel panel);

    /// <summary>Closes <paramref name="panel"/> where it shows; false where it could not be asked to.</summary>
    bool Dismiss(WindowsPanel panel);

    /// <summary>Whether <paramref name="window"/> is one of the panels' own windows, shown or not.</summary>
    bool Owns(HWND window);

    /// <summary>
    /// Whether <paramref name="panel"/> went — lost the foreground or was cloaked — at or after
    /// <paramref name="tick"/>, a time on the message clock (<see cref="PanelWatch.WentSince"/>).
    /// </summary>
    bool WentSince(WindowsPanel panel, uint tick);
}

/// <summary>
/// When each of Windows' panels last went, by the events Windows sends as it happens: the foreground
/// leaving the panel's window, or the shell cloaking it. Times are the message clock's
/// (GetTickCount's, which GetMessageTime and a WinEvent's time are too).
/// </summary>
internal sealed class PanelWatch
{
    /// <summary>The longest after a time a panel's going still counts as since it: a press held longer is no toggle.</summary>
    internal const uint RecentMs = 60_000;

    private readonly uint?[] wentAt = new uint?[WindowsPanels.All.Length];

    // The panel whose window has the foreground, as the last foreground event said; null for none.
    private WindowsPanel? front;

    /// <summary>§474: the panel in front now, as the last foreground event said; null for none.</summary>
    public WindowsPanel? Front => front;

    /// <summary>
    /// The foreground went to a window that is <paramref name="panel"/> (null for any other) at <paramref name="time"/>;
    /// true where the panel in front changed.
    /// </summary>
    public bool Foreground(WindowsPanel? panel, uint time)
    {
        if (front is { } was && was != panel)
        {
            wentAt[(int)was] = time;
        }
        var changed = front != panel;
        front = panel;
        return changed;
    }

    /// <summary>
    /// A window that is <paramref name="panel"/> (null for any other) was cloaked at <paramref name="time"/>; true where
    /// it was the panel in front, which is none now.
    /// </summary>
    public bool Cloaked(WindowsPanel? panel, uint time)
    {
        if (panel is not { } cloaked)
        {
            return false;
        }
        wentAt[(int)cloaked] = time;
        if (front != cloaked)
        {
            return false;
        }
        front = null;
        return true;
    }

    /// <summary>
    /// Whether <paramref name="panel"/> went at or after <paramref name="tick"/>, and no longer than
    /// <see cref="RecentMs"/> after it: the clock wraps every 49.7 days, and a going weeks old must
    /// not read as one still to come.
    /// </summary>
    public bool WentSince(WindowsPanel panel, uint tick) => wentAt[(int)panel] is { } at && unchecked(at - tick) <= RecentMs;
}

/// <summary>
/// The Search button's click, decided from what Windows Search was when the button went down
/// (<see cref="Pressed"/>) and what it is when the click lands (<see cref="Clicked"/>). Both are
/// needed: a press on the bar may itself close Search as it lands, and the click must then not open
/// it again; or it may not, and the click must close it.
/// </summary>
internal sealed class SearchToggle(IWindowsPanels panels, WindowsPanel panel = WindowsPanel.Search)
{
    /// <summary>
    /// How long before the press's own time a panel's going still counts as the press's doing: the
    /// press's time and the event's are stamped apart, a tick (about 16 ms) at worst.
    /// </summary>
    internal const uint SlackMs = 50;

    // Whether Search showed as the button went down; null when no press is waiting for its click.
    private bool? openAtPress;

    // When the button went down, less the slack, on the message clock.
    private uint pressedAt;

    /// <summary>
    /// A button went down on the bar at <paramref name="at"/> (the press's message time): on the
    /// Search button (<paramref name="onSearch"/>), Search's state is kept for its click.
    /// </summary>
    public void Pressed(bool onSearch, uint at)
    {
        openAtPress = onSearch ? panels.IsOpen(panel) : null;
        pressedAt = unchecked(at - SlackMs);
    }

    /// <summary>
    /// The Search button was clicked: what to do (<see cref="Decide"/>). Search was open as the
    /// button went down where it showed then, or where it went since — the press sent it away
    /// before it was read; and it is open now only where it shows and has not gone since the press,
    /// so one on its way out is not closed a second time. A click with no press before it — the
    /// keyboard's, Narrator's — reads Search as it is now.
    /// </summary>
    public SearchClick Clicked()
    {
        var went = openAtPress is not null && panels.WentSince(panel, pressedAt);
        var now = panels.IsOpen(panel) && !went;
        var then = (openAtPress ?? now) || went;
        openAtPress = null;
        return Decide(then, now);
    }

    /// <summary>
    /// What a click does: open Search unless it was open as the button went down; if it was, close
    /// it if it still is, and nothing if the press closed it.
    /// </summary>
    public static SearchClick Decide(bool openAtPress, bool openNow) =>
        !openAtPress ? SearchClick.Open
        : openNow ? SearchClick.Close
        : SearchClick.Nothing;
}

/// <summary>
/// windowsMenus: the buttons of Windows' menus (§466) — the glyph buttons, and Windows' own widgets that wear Windows' face
/// (§474) — each toggling its panel as Start's button does: what the panel was as the button went down
/// (<see cref="Pressed"/>) and as the click lands (<see cref="Clicked"/>), one <see cref="SearchToggle"/> per panel. Quick
/// settings is one panel of four buttons' pages: the button that opened it closes it, another goes to its own page over it.
/// The button of the panel in front is lit while it is (<see cref="Lit"/>).
/// </summary>
internal sealed class WindowsMenus(IWindowsPanels panels)
{
    private readonly Dictionary<WindowsPanel, SearchToggle> toggles = WindowsPanels.All
        .Where(panel => panel is not (WindowsPanel.Search or WindowsPanel.Start))
        .ToDictionary(panel => panel, panel => new SearchToggle(panels, panel));

    // The button that last opened each panel.
    private readonly Dictionary<WindowsPanel, BarKind> openedBy = [];

    /// <summary>
    /// A button went down on the bar at <paramref name="at"/> (the press's message time): on the button of
    /// <paramref name="kind"/> (null for anything else), its panel's state is kept for its click.
    /// </summary>
    public void Pressed(BarKind? kind, uint at)
    {
        var menu = kind is { } pressed ? WindowsPanels.PanelOf(pressed) : null;
        foreach (var (panel, toggle) in toggles)
        {
            toggle.Pressed(panel == menu, at);
        }
    }

    /// <summary>
    /// The button of <paramref name="kind"/> was clicked: true where it closed what it had opened (Escape to the panel), or
    /// the press already had, and nothing more is to be done; false where its menu is to be opened — as for a button whose
    /// menu no panel answers for.
    /// </summary>
    public bool Clicked(BarKind kind)
    {
        if (WindowsPanels.PanelOf(kind) is not { } panel)
        {
            return false;
        }
        // A panel Karpo did not open (its shortcut pressed on the keyboard) is on the page of its one button, where it has
        // one; quick settings' page is then not known, and another of its buttons goes to its own page as before.
        var same = openedBy.TryGetValue(panel, out var by) ? by == kind : OneButton(panel);
        switch (WindowsPanels.ForPage(toggles[panel].Clicked(), same))
        {
            case SearchClick.Close:
                _ = panels.Dismiss(panel);
                return true;
            case SearchClick.Nothing:
                return true;
            default:
                openedBy[panel] = kind;
                return false;
        }
    }

    /// <summary>Whether one button alone opens <paramref name="panel"/> (<see cref="WindowsPanels.PanelOf(BarKind)"/>): all but quick settings.</summary>
    internal static bool OneButton(WindowsPanel panel) => Enum.GetValues<BarKind>().Count(kind => WindowsPanels.PanelOf(kind) == panel) == 1;

    /// <summary>
    /// §474: which of a bar's entries, <paramref name="kinds"/> in its order, is lit while <paramref name="front"/> is in
    /// front, as Windows lights its own button while its menu is open (<see cref="Lit(WindowsPanel?, BarKind?, IReadOnlyList{BarKind})"/>,
    /// by the button that last opened it); -1 for none.
    /// </summary>
    public int Lit(WindowsPanel? front, IReadOnlyList<BarKind> kinds) =>
        Lit(front, front is { } panel && openedBy.TryGetValue(panel, out var by) ? by : null, kinds);

    /// <summary>
    /// §474: which of <paramref name="kinds"/> is lit while <paramref name="front"/> is in front: the first of the button
    /// that opened it (<paramref name="openedBy"/>), else the first that opens it whole (quick settings' button over its
    /// pages', <see cref="IsPage"/>), else the first of its buttons; -1 where none of them is here, or nothing is in front.
    /// </summary>
    internal static int Lit(WindowsPanel? front, BarKind? openedBy, IReadOnlyList<BarKind> kinds)
    {
        if (front is not { } panel)
        {
            return -1;
        }
        var opener = openedBy is { } kind ? IndexOf(kinds, each => each == kind) : -1;
        if (opener >= 0)
        {
            return opener;
        }
        var face = IndexOf(kinds, each => WindowsPanels.PanelOf(each) == panel && !IsPage(each));
        return face >= 0 ? face : IndexOf(kinds, each => WindowsPanels.PanelOf(each) == panel);
    }

    /// <summary>Whether <paramref name="kind"/>'s button opens a page of quick settings (sound output, Wi-Fi, cast) rather than the whole of a panel.</summary>
    internal static bool IsPage(BarKind kind) => kind is BarKind.WindowsSound or BarKind.WindowsNetworks or BarKind.WindowsCast;

    private static int IndexOf(IReadOnlyList<BarKind> kinds, Func<BarKind, bool> match)
    {
        for (var at = 0; at < kinds.Count; at++)
        {
            if (match(kinds[at]))
            {
                return at;
            }
        }
        return -1;
    }
}

/// <summary>The rules for which of Windows' panels shows, from what their windows are (<see cref="WindowsPanels"/>).</summary>
internal static class PanelRules
{
    /// <summary>
    /// Whether <paramref name="panel"/> is open, from whether Search's host window and Start's show:
    /// Start where its own shows; Search where its shows and Start's does not, since over an open
    /// Start Search's window is Start's search box.
    /// </summary>
    public static bool Open(WindowsPanel panel, bool searchShows, bool startShows) =>
        panel == WindowsPanel.Start ? startShows : searchShows && !startShows;
}

/// <summary>Windows' Search and Start panels, read from their windows (<see cref="IWindowsPanels"/>).</summary>
internal sealed unsafe class WindowsPanels : IWindowsPanels
{
    private const string CoreWindowClass = "Windows.UI.Core.CoreWindow";

    /// <summary>windowsMenus: quick settings' window, ShellHost.exe's.</summary>
    private const string ControlCenterClass = "ControlCenterWindow";

    /// <summary>windowsMenus: a menu of Explorer's XAML, the quick link menu's among them.</summary>
    private const string ExplorerPopupClass = "Xaml_WindowedPopupClass";

    /// <summary>§473: the Widgets board's window, WidgetBoard.exe's.</summary>
    private const string WidgetsBoardClass = "WindowsDashboard";

    /// <summary>Every panel, in the order a flyout closes them.</summary>
    public static readonly WindowsPanel[] All = Enum.GetValues<WindowsPanel>();

    public static WindowsPanels Instance { get; } = new();

    /// <summary>When each panel last went; fed by the hooks <see cref="Watch"/> sets, on the dock's thread.</summary>
    private readonly PanelWatch watch = new();

    private HWINEVENTHOOK foregroundHook;
    private HWINEVENTHOOK cloakHook;

    public bool IsOpen(WindowsPanel panel) => Window(panel) is not null;

    public bool WentSince(WindowsPanel panel, uint tick) => watch.WentSince(panel, tick);

    /// <summary>§474: the panel in front now, by the foreground hook (<see cref="Watch"/>); null for none.</summary>
    public WindowsPanel? Front => watch.Front;

    /// <summary>§474: the panel in front changed, on the dock's thread: its button is lit, or no longer.</summary>
    public event Action? FrontChanged;

    /// <summary>
    /// Starts noting when each panel goes (<see cref="PanelWatch"/>), on the dock's thread: the hooks
    /// are out of context, so they call back on it through its message loop and nothing is injected
    /// anywhere. Again does nothing.
    /// </summary>
    public void Watch()
    {
        if (foregroundHook != HWINEVENTHOOK.NULL)
        {
            return;
        }
        _ = watch.Foreground(PanelOfWindow(GetForegroundWindow()), GetTickCount());
        foregroundHook = SetWinEventHook(EVENT.EVENT_SYSTEM_FOREGROUND, EVENT.EVENT_SYSTEM_FOREGROUND, HMODULE.NULL, &Seen, 0, 0, WINEVENT_OUTOFCONTEXT);
        cloakHook = SetWinEventHook(EVENT.EVENT_OBJECT_CLOAKED, EVENT.EVENT_OBJECT_CLOAKED, HMODULE.NULL, &Seen, 0, 0, WINEVENT_OUTOFCONTEXT);
    }

    /// <summary>Stops noting: the hooks let go.</summary>
    public void Unwatch()
    {
        if (foregroundHook != HWINEVENTHOOK.NULL)
        {
            _ = UnhookWinEvent(foregroundHook);
            foregroundHook = HWINEVENTHOOK.NULL;
        }
        if (cloakHook != HWINEVENTHOOK.NULL)
        {
            _ = UnhookWinEvent(cloakHook);
            cloakHook = HWINEVENTHOOK.NULL;
        }
    }

    [UnmanagedCallersOnly]
    private static void Seen(HWINEVENTHOOK hook, uint evt, HWND hwnd, int obj, int child, uint thread, uint time)
    {
        // Nothing thrown may unwind into user32.
        try
        {
            if (obj != OBJID.OBJID_WINDOW || hwnd == HWND.NULL)
            {
                return;
            }
            var panel = PanelOfWindow(hwnd);
            var changed = evt == EVENT.EVENT_SYSTEM_FOREGROUND ? Instance.watch.Foreground(panel, time) : Instance.watch.Cloaked(panel, time);
            if (changed)
            {
                Instance.FrontChanged?.Invoke();
            }
        }
        catch (Exception)
        {
            // A window may be gone between the event and the query; the going is then not noted.
        }
    }

    /// <summary>Which panel <paramref name="window"/> is, by its class and its process (<see cref="PanelOf(string?, string)"/>); null for any other window. §580: the one answer for the panels kept off the bars too (ShellPopups).</summary>
    internal static WindowsPanel? PanelOfWindow(HWND window)
    {
        if (window == HWND.NULL)
        {
            return null;
        }
        const int Room = 64;
        var name = stackalloc char[Room];
        var length = GetClassNameW(window, name, Room);
        var type = new ReadOnlySpan<char>(name, length);
        if (!type.SequenceEqual(CoreWindowClass) && !type.SequenceEqual(ControlCenterClass) && !type.SequenceEqual(ExplorerPopupClass) && !type.SequenceEqual(WidgetsBoardClass))
        {
            return null;
        }
        uint pid = 0;
        _ = GetWindowThreadProcessId(window, &pid);
        return PanelOf(Apps.ImagePath(pid), type.ToString());
    }

    public HWND? Window(WindowsPanel panel)
    {
        if (panel is not (WindowsPanel.Search or WindowsPanel.Start))
        {
            return Fronted(panel);
        }
        var (search, start) = (Host(WindowsPanel.Search), Host(WindowsPanel.Start));
        return PanelRules.Open(panel, search is not null, start is not null) ? (panel == WindowsPanel.Start ? start : search) : null;
    }

    /// <summary>
    /// windowsMenus: the window of <paramref name="panel"/> while it is open — the foreground window,
    /// which it is for as long as it is open, where that is it and shows; null otherwise.
    /// </summary>
    private static HWND? Fronted(WindowsPanel panel)
    {
        var front = GetForegroundWindow();
        return PanelOfWindow(front) == panel && Shows(IsWindowVisible(front), Cloaked(front)) ? front : null;
    }

    /// <summary>Whether <paramref name="host"/>'s window shows, whatever it shows for.</summary>
    private static bool Shows(WindowsPanel host) => Host(host) is not null;

    public bool Owns(HWND window) => PanelOfWindow(window) is not null;

    /// <summary>The window of <paramref name="host"/>'s process while it shows; null while it doesn't.</summary>
    private static HWND? Host(WindowsPanel host)
    {
        var after = HWND.NULL;
        fixed (char* name = CoreWindowClass)
        {
            // Every top-level CoreWindow: a handful at most, the open store apps' and the shell's.
            while ((after = FindWindowExW(HWND.NULL, after, name, null)) != HWND.NULL)
            {
                uint pid = 0;
                _ = GetWindowThreadProcessId(after, &pid);
                if (PanelOf(Apps.ImagePath(pid), CoreWindowClass) == host && Shows(IsWindowVisible(after), Cloaked(after)))
                {
                    return after;
                }
            }
        }
        return null;
    }

    public bool Dismiss(WindowsPanel panel)
    {
        if (Window(panel) is not { } window)
        {
            return false;
        }
        // Escape, as for Search: not the Windows key, which a launcher that takes the key for itself
        // swallows (Raycast, 2026-10-25), nor the taskbar's SC_TASKLIST, which opens Start and never
        // shuts it. Over an open Start the foreground is mostly its search box, Search's host
        // window, and Escape there closes Start as well.
        if (panel == WindowsPanel.Start && GetForegroundWindow() is var front && PanelOfWindow(front) is WindowsPanel.Search or WindowsPanel.Start)
        {
            window = front;
        }
        // Escape goes to the foreground window, so the panel has to be it; it is whenever it is
        // open, and the dock, which the click that got here went to, may hand it the foreground
        // where something took it away. Nothing is sent where that did not work.
        if (GetForegroundWindow() != window)
        {
            _ = SetForegroundWindow(window);
            if (GetForegroundWindow() != window)
            {
                return false;
            }
        }
        var keys = EscapeTaps();
        fixed (INPUT* sent = keys)
        {
            if (SendInput((uint)keys.Length, sent, sizeof(INPUT)) != keys.Length)
            {
                return false;
            }
        }
        // A flyout of Karpo's takes the foreground straight after this, and Escape must have gone
        // to the panel by then, not to the flyout, which it would close. The panel gives the
        // foreground up as it takes the key; it is waited for that long and no longer.
        // §490: waited for with this thread's sent messages run: a low-level keyboard hook of its own (edit mode's,
        // EditFollowHook) is called through them, and the Escape reaches no window until that hook has answered — a plain
        // sleep held it back the whole wait, and it then went to the flyout opened meanwhile, the add list, and closed it.
        for (var waited = 0; waited < SettleMs && GetForegroundWindow() == window; waited += 10)
        {
            _ = MsgWaitForMultipleObjectsEx(0, null, 10, QS.QS_SENDMESSAGE, 0);
            MSG msg;
            _ = PeekMessageW(&msg, HWND.NULL, 0, 0, PM.PM_NOREMOVE | PM.PM_QS_SENDMESSAGE);
        }
        return true;
    }

    /// <summary>
    /// §490: the Escape, down and up, that closes a panel in front (<see cref="Dismiss"/>), marked as Karpo's own
    /// (<see cref="TaskbarKeys.Ours"/>), so no hook of Karpo's hears it as the person's — edit mode's took it for leaving
    /// edit mode, and the add list opening over Windows' menu closed with it.
    /// </summary>
    internal static INPUT[] EscapeTaps()
    {
        var keys = new INPUT[2];
        for (var index = 0; index < keys.Length; index++)
        {
            keys[index].type = INPUT.INPUT_KEYBOARD;
            keys[index].ki.wVk = VK.VK_ESCAPE;
            keys[index].ki.dwFlags = index == 0 ? 0 : (uint)KEYEVENTF_KEYUP;
            keys[index].ki.dwExtraInfo = TaskbarKeys.Ours;
        }
        return keys;
    }

    /// <summary>The longest the dock waits for a panel to take its Escape.</summary>
    private const int SettleMs = 150;

    /// <summary>
    /// Which panel a window of class <paramref name="type"/> of the process running
    /// <paramref name="image"/> is: a CoreWindow of Search's host on Windows 11 and before it, of
    /// Start's host, of ShellExperienceHost (the notifications); ShellHost's ControlCenterWindow (quick
    /// settings); a XAML menu of Explorer's (the quick link menu); WidgetBoard's WindowsDashboard (the
    /// Widgets board). Null for any other window.
    /// </summary>
    internal static WindowsPanel? PanelOf(string? image, string type) => (Path.GetFileName(image ?? "").ToLowerInvariant(), type) switch
    {
        ("searchhost.exe" or "searchapp.exe" or "searchui.exe", CoreWindowClass) => WindowsPanel.Search,
        ("startmenuexperiencehost.exe", CoreWindowClass) => WindowsPanel.Start,
        ("shellexperiencehost.exe", CoreWindowClass) => WindowsPanel.Notifications,
        ("shellhost.exe", ControlCenterClass) => WindowsPanel.QuickSettings,
        ("explorer.exe", ExplorerPopupClass) => WindowsPanel.ExplorerMenu,
        ("widgetboard.exe", WidgetsBoardClass) => WindowsPanel.WidgetsBoard,
        _ => null,
    };

    /// <summary>
    /// windowsMenus: the panel the bar's button of <paramref name="kind"/> opens, whose second click
    /// closes it (§466) — Windows' own widgets that wear Windows' face included (§474); null for a
    /// button that opens none of these.
    /// </summary>
    internal static WindowsPanel? PanelOf(BarKind kind) => kind switch
    {
        BarKind.QuickLink => WindowsPanel.ExplorerMenu,
        BarKind.WindowsNotifications => WindowsPanel.Notifications,
        BarKind.WindowsQuickSettings or BarKind.WindowsSound or BarKind.WindowsNetworks or BarKind.WindowsCast => WindowsPanel.QuickSettings,
        BarKind.WidgetsBoard => WindowsPanel.WidgetsBoard,
        _ => null,
    };

    /// <summary>
    /// windowsMenus: what a click on a button of a panel that holds pages (quick settings) does, where
    /// <paramref name="click"/> is the panel's toggle's answer: the page it showed last
    /// (<paramref name="samePage"/>) closes as Start does; any other page opens, over it — the sound
    /// button over quick settings goes to its sound page, not away.
    /// </summary>
    internal static SearchClick ForPage(SearchClick click, bool samePage) => samePage ? click : SearchClick.Open;

    /// <summary>Whether a panel's window shows: visible and not cloaked, which is how the shell hides a closed one.</summary>
    internal static bool Shows(bool visible, uint cloaked) => visible && cloaked == 0;

    private static uint Cloaked(HWND window)
    {
        uint cloaked = 0;
        return DwmGetWindowAttribute(window, (uint)DWMWINDOWATTRIBUTE.DWMWA_CLOAKED, &cloaked, sizeof(uint)).SUCCEEDED ? cloaked : 0;
    }

    /// <summary>
    /// A flyout of Karpo's is opening over <paramref name="foreground"/>: Windows Search and Start
    /// close first where they show, and the notifications, quick settings, the quick link menu and the
    /// Widgets board where one is in front (§466, §473), so Karpo's flyout is not opened under them. True when
    /// <paramref name="foreground"/> was one of them, so the flyout does not hand the focus back to
    /// a panel that has gone.
    /// </summary>
    internal static bool CloseFor(IWindowsPanels panels, HWND foreground)
    {
        var closed = false;
        foreach (var panel in All)
        {
            if (panels.Window(panel) is not { } window)
            {
                continue;
            }
            closed |= window == foreground;
            _ = panels.Dismiss(panel);
        }
        // Start's search box in front of Start is Search's window, which is no panel of its own.
        return closed || (foreground != HWND.NULL && panels.Owns(foreground));
    }

    /// <summary>
    /// §490: edit mode is coming in over <paramref name="foreground"/>: every one of Windows' menus open closes first, as
    /// for a flyout of Karpo's (<see cref="CloseFor"/>; the person, 2026-11-02: "when entering edit mode it should
    /// therefore just out of default close all the windows menus open"). The window the keys go back to as edit mode
    /// ends: <paramref name="foreground"/>, none where it was one of them.
    /// </summary>
    internal static HWND ForEditMode(IWindowsPanels panels, HWND foreground) => CloseFor(panels, foreground) ? HWND.NULL : foreground;
}
