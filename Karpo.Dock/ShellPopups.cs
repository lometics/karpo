// Windows' own panels and notifications kept off the bars (docs/widgets.md §540, §580).
//
// The user, 2026-11-07: "the windows notifications on the main screen overlap the taskbar. is this
// something that would be fixable?" — and 2026-11-09: "when the taskbar is on the side the start menu
// overlaps it. is this fixable? if yes fix it for all the flyouts, not just the start menu". Windows
// places every panel of its own by Explorer's taskbar, which it is told of through the shell's own
// channel (WindowsUdk.UI.Shell.TaskbarLayout), never by the work area. While Karpo runs that taskbar is
// at the bottom, the one edge Windows auto-hides it on (§591: Windows' own edge follows Karpo's bar only once
// Karpo ends), and auto-hidden (§465), so each panel stands where it would over a bottom taskbar —
// Start a taskbar's thickness above the screen's end (§591), Start and Search at the display's left (TaskbarAl 0) or its middle, the
// Widgets board and the quick link menu (Win+X) against its left end, the notifications, the
// notification centre and calendar and quick settings against its right end. Seen on the user's PC
// (26300, 150 %): a notification's window at 3246,1848..3840,2160 on a 3840x2160 display whose work
// area ended at 3777, Karpo's bar holding the right 63 px. Nothing an app can say through
// SHAppBarMessage reaches that channel, so a bar at the side is under whichever panel stands at its end.
//
// So each panel's window is moved off the strips the work area leaves out — what the bars hold — by the
// strip's thickness, Windows' own gap to the screen's end kept to the bar (ShellPopupPlace.Clear). Start is
// the one panel placed otherwise (§666, StartPlacement): its host, a CoreWindow as wide as the display with
// Start laid out at its foot, goes by as far as Start has to to hang from Karpo's Start button on the display
// Start opens on, and Start's search box (SearchHost's window over Start) goes where Start then stands. The
// rules of §580, §591, §601 and §640 for Start, each a guess at Start's place from its windows as Windows had
// put them, are gone with them.
//
// Which windows are panels is WindowsPanels.PanelOfWindow's one answer (by class and process), the same
// that closes them for a flyout of Karpo's. Heard by WinEvent hooks scoped to each panel's process
// (ShellPopups.Hosts), out of context, nothing injected: shown (EVENT_OBJECT_SHOW), uncloaked
// (EVENT_OBJECT_UNCLOAKED, how the shell shows its CoreWindows) and moved (EVENT_OBJECT_LOCATIONCHANGE:
// a notification is shown by moving it onto the screen, and a panel that puts itself back is heard
// doing it) — Explorer's moves not heard, its windows being every folder window's too: its menus
// show. A window off the screen is never pulled back. The move is asynchronous (SWP_ASYNCWINDOWPOS), so
// a host that hangs never holds up the dock, and counted (ShellPopupMoves): a host that put its window
// back every time is let be after a few, never fought.

using System.Runtime.InteropServices;
using Karpo.Core;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

/// <summary>Where one of Windows' panels goes to keep off the strips the bars hold. Pure.</summary>
internal static class ShellPopupPlace
{
    /// <summary>Whether <paramref name="window"/> is as wide as the display <paramref name="monitor"/>, or wider: a host, its panel inside it.</summary>
    internal static bool Spans(RECT window, RECT monitor) => window.right - window.left >= monitor.right - monitor.left;

    /// <summary>
    /// Where <paramref name="window"/>'s top left corner goes so that it lies inside <paramref name="work"/>,
    /// the work area of the display <paramref name="monitor"/>, at the distance from it that Windows left
    /// to the screen's end; null where it stays: inside the work area already, off the screen (a
    /// notification gone below it), empty, or larger than the work area (a window over the whole screen).
    /// </summary>
    public static (int X, int Y)? Clear(RECT window, RECT monitor, RECT work)
    {
        if (window.right <= window.left || window.bottom <= window.top
            || window.right <= monitor.left || window.left >= monitor.right
            || window.bottom <= monitor.top || window.top >= monitor.bottom)
        {
            return null;
        }
        var x = Axis(window.left, window.right, monitor.left, monitor.right, work.left, work.right);
        var y = Axis(window.top, window.bottom, monitor.top, monitor.bottom, work.top, work.bottom);
        if (x is null || y is null)
        {
            return null;
        }
        return x == window.left && y == window.top ? null : (x.Value, y.Value);
    }

    /// <summary>
    /// One axis of <see cref="Clear"/>: where the window's start goes; null where it can't fit. Only a strip a bar holds
    /// moves it: one reaching past the screen's end where no bar stands is still on its way in (§580: Explorer's menus
    /// are shown below the screen and moved up a few ms later, seen 2026-11-09), and stays.
    /// </summary>
    private static int? Axis(int start, int end, int screenStart, int screenEnd, int workStart, int workEnd)
    {
        var size = end - start;
        if (workEnd <= workStart || size > workEnd - workStart)
        {
            return null;
        }
        int to;
        if (end > workEnd && workEnd < screenEnd)
        {
            // Into the strip at the far end: as far in from the work area's end as it was from the screen's.
            to = workEnd - Math.Max(0, screenEnd - end) - size;
        }
        else if (start < workStart && workStart > screenStart)
        {
            to = workStart + Math.Max(0, start - screenStart);
        }
        else
        {
            return start;
        }
        return Math.Clamp(to, workStart, workEnd - size);
    }
}

/// <summary>
/// How often one window is moved: at most <see cref="PerWindow"/> times in <see cref="WindowMs"/>, and §580 put back by its
/// host at most <see cref="PutBacks"/> times while it shows — never fought.
/// </summary>
internal sealed class ShellPopupMoves(Func<long> clock)
{
    /// <summary>
    /// §580: how often a window's host may put it back where it stood before Karpo moved it, while it shows, and be moved
    /// again: Windows places Start's host again some 200 ms into an opening; while Start stands open its host hears a few
    /// moves every five seconds or so, each ending where it stood (seen 2026-11-09). Should they be Windows putting it
    /// back, Start would step aside and back every few seconds: after the second it is let be until it shows again.
    /// </summary>
    public const int PutBacks = 2;

    // Per window: where it stood before its last move, and how often its host put it back there since it last showed.
    private readonly Dictionary<nint, (RECT From, int Count)> undone = [];

    /// <summary>
    /// §580: <paramref name="window"/> showed (shown, uncloaked): its put-backs count from none. §666: and its moves: a
    /// showing is an opening of its own, so Start opened again and again within the span is placed each time.
    /// </summary>
    public void Shown(nint window)
    {
        undone.Remove(window);
        moves.Remove(window);
    }

    /// <summary>
    /// §580: whether <paramref name="window"/>, standing at <paramref name="at"/>, may be moved now: not where its host
    /// put it back more than <see cref="PutBacks"/> times since it showed, nor past <see cref="May(nint)"/>. A yes is
    /// counted, and <paramref name="at"/> kept as where it was moved from.
    /// </summary>
    public bool May(nint window, RECT at)
    {
        var count = 0;
        if (undone.TryGetValue(window, out var last) && Same(last.From, at))
        {
            if (last.Count >= PutBacks)
            {
                return false;
            }
            count = last.Count + 1;
        }
        if (!May(window))
        {
            return false;
        }
        if (undone.Count > 64)
        {
            // Windows that went without showing again: a notification's, made as one comes and gone a while after.
            undone.Clear();
        }
        undone[window] = (at, count);
        return true;
    }

    private static bool Same(RECT one, RECT other) =>
        one.left == other.left && one.top == other.top && one.right == other.right && one.bottom == other.bottom;

    /// <summary>
    /// A notification's window is placed in a few steps as each one comes (seen: four in 2 ms), each read as it is when
    /// heard, so mostly one move. §580: Start's host is placed by Windows as it opens and again some 200 ms later and as
    /// it closes (seen 2026-11-09), up to three moves an opening; counted from its last showing (§666).
    /// </summary>
    public const int PerWindow = 12;

    /// <summary>The span the moves are counted in, longer than a notification shows (about 6 s).</summary>
    public const long WindowMs = 10_000;

    private readonly Dictionary<nint, List<long>> moves = [];

    /// <summary>Whether <paramref name="window"/> may be moved now; a yes is counted.</summary>
    public bool May(nint window)
    {
        var now = clock();
        foreach (var key in moves.Keys.ToList())
        {
            moves[key].RemoveAll(at => now - at >= WindowMs);
            if (moves[key].Count == 0)
            {
                moves.Remove(key);
            }
        }
        if (!moves.TryGetValue(window, out var times))
        {
            moves[window] = times = [];
        }
        if (times.Count >= PerWindow)
        {
            return false;
        }
        times.Add(now);
        return true;
    }
}

/// <summary>
/// The hooks on the processes of Windows' panels and the moves (see the top of the file). <paramref name="centred"/>
/// reads whether Windows lays Start out in the middle of its host (TaskbarAl 1), asked as Start's windows are placed.
/// §601 <paramref name="startButton"/>: the Start button of Karpo's that Start, opened on a display (by its handle), hangs
/// from, null where none.
/// §703 <paramref name="settleLater"/>: asks for <see cref="Settled"/> on the dock's thread <see cref="SettleMs"/> from now, again
/// from now where already asked.
/// </summary>
internal sealed unsafe class ShellPopups(Action<string> trace, Func<bool> centred, Func<nint, StartButton?> startButton, Action settleLater)
{
    /// <summary>
    /// §703 (the person, 2026-11-16: "the notification just appeared at the wrong place. it shows clipped on the bottom right
    /// but half off screen"): how long a panel off the bars waits, unmoved, before it is moved. Windows places a notification
    /// in a few steps within a few ms (appbar.log: 3246,2088..3840,2153 then 3246,2023..3840,2403), each heard as it stood
    /// then; moving it from a step sent that step's top along with the new left, and the move, asynchronous, landed after
    /// Windows' last step: the notification stood beside the bar but reaching below the screen. Once it is still, it is read
    /// and moved once.
    /// </summary>
    public const uint SettleMs = 40;

    // §703: the panels heard moving, to be placed once they are still.
    private readonly HashSet<nint> settling = [];

    private const uint EventObjectShow = 0x8002;
    private const uint EventObjectLocationChange = 0x800B;
    private const uint EventObjectUncloaked = 0x8018;

    /// <summary>How long a look that found no process of a host waits before the next.</summary>
    private const long LookAgainMs = 5_000;

    /// <summary>
    /// §580: the processes whose windows are Windows' panels (<see cref="WindowsPanels.PanelOf(string?, string)"/>), and
    /// whether each one's moves are heard: Explorer's are not — its process holds every folder window, each of whose
    /// moves would be heard — and its menus are shown rather than moved on.
    /// </summary>
    internal static readonly (string Image, bool Moves)[] Hosts =
    [
        ("ShellExperienceHost", true),
        ("ShellHost", true),
        ("StartMenuExperienceHost", true),
        ("SearchHost", true),
        ("WidgetBoard", true),
        ("explorer", false),
    ];

    // The callback has no context argument: the one live instance.
    private static ShellPopups? live;

    private readonly ShellPopupMoves moves = new(() => Environment.TickCount64);

    /// <summary>§666: Start's size as its search box last said it, for the host heard before the box.</summary>
    private readonly StartSize startSize = new();
    private readonly Watched[] watched = [.. Hosts.Select(host => new Watched(host.Image, host.Moves))];
    private bool refused;

    /// <summary>One host's process as it is heard: its hooks, and when it was last looked for.</summary>
    private sealed class Watched(string image, bool hearsMoves)
    {
        public string Image { get; } = image;
        public bool HearsMoves { get; } = hearsMoves;
        public List<HWINEVENTHOOK> Hooks { get; } = [];
        public HANDLE Process { get; set; }
        public long? LookedAt { get; set; }
    }

    /// <summary>
    /// Hooked on each host's process running now: nothing for one whose hooked process runs, else it is
    /// looked for (at most every <see cref="LookAgainMs"/>), hooked, and its windows placed once. Called
    /// with every reconcile, so a host started (again) is found at the next change of the foreground —
    /// Start's, for one, as Start first opens.
    /// </summary>
    public void Ensure()
    {
        live = this;
        foreach (var host in watched)
        {
            Ensure(host);
        }
    }

    private void Ensure(Watched host)
    {
        if (host.Process != HANDLE.NULL && WaitForSingleObject(host.Process, 0) == WAIT.WAIT_TIMEOUT)
        {
            return;
        }
        var now = Environment.TickCount64;
        if (host.Process == HANDLE.NULL && host.LookedAt is long at && now - at < LookAgainMs)
        {
            return;
        }
        Stop(host);
        host.LookedAt = now;
        var found = Find(host.Image);
        if (found == 0)
        {
            return;
        }
        var opened = OpenProcess((uint)SYNCHRONIZE, BOOL.FALSE, found);
        if (opened == HANDLE.NULL)
        {
            return;
        }
        host.Process = opened;
        uint[] events = host.HearsMoves ? [EventObjectShow, EventObjectLocationChange, EventObjectUncloaked] : [EventObjectShow, EventObjectUncloaked];
        foreach (var each in events)
        {
            var hook = SetWinEventHook(each, each, HMODULE.NULL, &Heard, found, 0, WINEVENT_OUTOFCONTEXT);
            if (hook != HWINEVENTHOOK.NULL)
            {
                host.Hooks.Add(hook);
            }
        }
        foreach (var window in Windows(found))
        {
            Place(window);
        }
    }

    /// <summary>Every hook lets go.</summary>
    public void Stop()
    {
        foreach (var host in watched)
        {
            Stop(host);
        }
        if (live == this)
        {
            live = null;
        }
    }

    private static void Stop(Watched host)
    {
        foreach (var hook in host.Hooks)
        {
            _ = UnhookWinEvent(hook);
        }
        host.Hooks.Clear();
        if (host.Process != HANDLE.NULL)
        {
            _ = CloseHandle(host.Process);
            host.Process = HANDLE.NULL;
        }
    }

    /// <summary>
    /// The process of <paramref name="image"/> in this session; 0 where none runs. Found by its process, not its
    /// windows: a notification's window is made as one comes and gone a while after (seen 2026-11-07). Explorer's is
    /// the one whose taskbar it is — another may hold folder windows only.
    /// </summary>
    private static uint Find(string image)
    {
        if (image == "explorer")
        {
            uint shell = 0;
            var tray = Taskbar.ExplorerTray();
            return tray != HWND.NULL && GetWindowThreadProcessId(tray, &shell) != 0 ? shell : 0;
        }
        var session = System.Diagnostics.Process.GetCurrentProcess().SessionId;
        uint found = 0;
        foreach (var each in System.Diagnostics.Process.GetProcessesByName(image))
        {
            using (each)
            {
                if (found == 0 && each.SessionId == session)
                {
                    found = (uint)each.Id;
                }
            }
        }
        return found;
    }

    /// <summary>The top-level windows of process <paramref name="host"/>, top first.</summary>
    private static List<HWND> Windows(uint host)
    {
        var found = new List<HWND>();
        var window = GetTopWindow(HWND.NULL);
        for (var guard = 0; window != HWND.NULL && guard < 10_000; guard++)
        {
            uint owner = 0;
            _ = GetWindowThreadProcessId(window, &owner);
            if (owner == host)
            {
                found.Add(window);
            }
            window = GetWindow(window, GW_HWNDNEXT);
        }
        return found;
    }

    [UnmanagedCallersOnly]
    private static void Heard(HWINEVENTHOOK hook, uint evt, HWND hwnd, int obj, int child, uint thread, uint time)
    {
        // Nothing thrown may unwind into user32.
        try
        {
            if (obj == OBJID.OBJID_WINDOW && child == 0 && live is { } popups)
            {
                if (evt != EventObjectLocationChange)
                {
                    popups.moves.Shown(hwnd);
                }
                popups.Place(hwnd);
            }
        }
        catch (Exception)
        {
            // A window gone between the event and the look: nothing to move.
        }
    }

    /// <summary>
    /// The window placed where it is one of Windows' panels: §666 Start's host, and Start's search box while Start's host
    /// shows on its display, hung from Karpo's Start button (<see cref="PlaceStart"/>); any other moved off the bars'
    /// strips where it stands on one (<see cref="ShellPopupPlace.Clear"/>) — §703 once it has been still a while
    /// (<paramref name="settled"/>: it has).
    /// </summary>
    private void Place(HWND window, bool settled = false)
    {
        if (window == HWND.NULL || GetAncestor(window, GA.GA_ROOT) != window || !Showing(window) || WindowsPanels.PanelOfWindow(window) is not { } panel)
        {
            return;
        }
        RECT rect;
        if (!GetWindowRect(window, &rect))
        {
            return;
        }
        var monitor = MonitorFromWindow(window, MONITOR.MONITOR_DEFAULTTONEAREST);
        var info = new MONITORINFO { cbSize = (uint)sizeof(MONITORINFO) };
        if (monitor == HMONITOR.NULL || !GetMonitorInfoW(monitor, &info))
        {
            return;
        }
        if (panel == WindowsPanel.Start && ShellPopupPlace.Spans(rect, info.rcMonitor))
        {
            PlaceStart(window, rect, monitor, box: StartBoxOn(monitor, info.rcMonitor));
            return;
        }
        if (panel == WindowsPanel.Search && !ShellPopupPlace.Spans(rect, info.rcMonitor) && StartHostOn(monitor, info.rcMonitor) is { } host)
        {
            RECT at;
            if (GetWindowRect(host, &at))
            {
                PlaceStart(host, at, monitor, box: (window, rect));
            }
            return;
        }
        if (ShellPopupPlace.Clear(rect, info.rcMonitor, info.rcWork) is { } to)
        {
            if (!settled)
            {
                // §703: still being placed, maybe: moved once it is still.
                settling.Add(window);
                settleLater();
                return;
            }
            Move(panel, window, rect, to, info.rcWork, "off the bars");
        }
    }

    /// <summary>§703: the panels heard moving have been still for <see cref="SettleMs"/>: each read as it stands now and moved off the bars.</summary>
    public void Settled()
    {
        var windows = settling.ToList();
        settling.Clear();
        foreach (var window in windows)
        {
            Place((HWND)window, settled: true);
        }
    }

    /// <summary>
    /// §666: Start hung from Karpo's Start button: its <paramref name="host"/> (standing at <paramref name="hostRect"/> on
    /// <paramref name="monitor"/>) goes by as far as Start has to, and its search <paramref name="box"/>, where it shows,
    /// where Start then stands (<see cref="StartPlacement"/>). Start's size is the box's, or the last box's before it
    /// shows; with none seen yet nothing moves (the box comes a few ms after the host, and places both).
    /// </summary>
    private void PlaceStart(HWND host, RECT hostRect, HMONITOR monitor, (HWND Window, RECT Rect)? box)
    {
        if (Scene(monitor) is { } scene)
        {
            PlaceStart(host, hostRect, scene, box);
        }
    }

    /// <summary>§666: Start's <paramref name="host"/> and <paramref name="box"/> placed for <paramref name="scene"/> (the hidden-desktop tests' way in).</summary>
    internal void PlaceStart(HWND host, RECT hostRect, StartScene scene, (HWND Window, RECT Rect)? box)
    {
        var size = box is { } seen ? startSize.Seen(seen.Rect, scene.Display.Dpi) : startSize.On(scene.Display.Dpi);
        if (size is not { } known)
        {
            return;
        }
        var how = scene.Button is null ? "where Windows stands it over a taskbar at the work area's end" : "for Karpo's Start button";
        if (StartPlacement.Host(hostRect, scene, known) is { } to)
        {
            Move(WindowsPanel.Start, host, hostRect, to, scene.Display.Work, how);
        }
        if (box is { } shown && StartPlacement.Box(shown.Rect, hostRect, scene) is { } boxTo)
        {
            Move(WindowsPanel.Search, shown.Window, shown.Rect, boxTo, scene.Display.Work, how);
        }
    }

    /// <summary>§666: what Start's place on <paramref name="monitor"/> depends on but its size, read as it is now; null where the display is gone.</summary>
    private StartScene? Scene(HMONITOR monitor)
    {
        var displays = Displays();
        foreach (var display in displays)
        {
            if (display.Handle == monitor)
            {
                return new StartScene(display.Display, startButton(monitor), centred(), [.. displays.Select(each => each.Display)]);
            }
        }
        return null;
    }

    /// <summary>Every display as it is now: its handle, rectangle, work area and DPI.</summary>
    private static List<(HMONITOR Handle, StartDisplay Display)> Displays()
    {
        var found = new List<(HMONITOR, StartDisplay)>(4);
        var handle = GCHandle.Alloc(found);
        try
        {
            _ = EnumDisplayMonitors(HDC.NULL, null, &EachDisplay, GCHandle.ToIntPtr(handle));
        }
        finally
        {
            handle.Free();
        }
        return found;
    }

    [UnmanagedCallersOnly]
    private static BOOL EachDisplay(HMONITOR monitor, HDC dc, RECT* clip, LPARAM data)
    {
        try
        {
            var found = (List<(HMONITOR, StartDisplay)>)GCHandle.FromIntPtr(data).Target!;
            var info = new MONITORINFO { cbSize = (uint)sizeof(MONITORINFO) };
            uint dpiX = 96, dpiY = 96;
            if (GetMonitorInfoW(monitor, &info))
            {
                if (GetDpiForMonitor(monitor, MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, &dpiX, &dpiY).FAILED || dpiX == 0)
                {
                    dpiX = 96;
                }
                found.Add((monitor, new StartDisplay(info.rcMonitor, info.rcWork, dpiX)));
            }
        }
        catch (Exception)
        {
            // Nothing thrown may unwind into user32.
        }
        return BOOL.TRUE;
    }

    /// <summary>One window of a panel's moved to <paramref name="to"/>, counted (<see cref="ShellPopupMoves"/>) and said once in appbar.log.</summary>
    private void Move(WindowsPanel panel, HWND window, RECT rect, (int X, int Y) to, RECT work, string how)
    {
        if (!moves.May(window, rect))
        {
            return;
        }
        var moved = Surfaces.OtherAppMove(window, to.X, to.Y);
        if (moved || !refused)
        {
            // One line per move, and the first refusal only (appbar.log).
            trace($"Windows' {panel} {AppbarHolder.Show(rect)} {(moved ? $"moved {how} to" : "could not be moved to")} {to.X},{to.Y} (work area {AppbarHolder.Show(work)})");
        }
        refused |= !moved;
    }

    /// <summary>Whether <paramref name="window"/> shows: visible, and not cloaked (placed as it is uncloaked, EVENT_OBJECT_UNCLOAKED).</summary>
    private static bool Showing(HWND window)
    {
        uint cloaked = 0;
        return IsWindowVisible(window)
            && !(DwmGetWindowAttribute(window, (uint)DWMWINDOWATTRIBUTE.DWMWA_CLOAKED, &cloaked, sizeof(uint)).SUCCEEDED && cloaked != 0);
    }

    /// <summary>§666: Start's host showing on <paramref name="monitor"/> (a window of Start's as wide as the display), or null.</summary>
    private static HWND? StartHostOn(HMONITOR monitor, RECT display) =>
        ShowingOn(monitor, window => WindowsPanels.PanelOfWindow(window) == WindowsPanel.Start, display, wide: true) is { } host ? host.Window : null;

    /// <summary>
    /// §666: Start's search box showing on <paramref name="monitor"/> while Start's host shows there (Search's window not as
    /// wide as the display), or null.
    /// </summary>
    private static (HWND Window, RECT Rect)? StartBoxOn(HMONITOR monitor, RECT display) =>
        ShowingOn(monitor, window => WindowsPanels.PanelOfWindow(window) == WindowsPanel.Search, display, wide: false);

    /// <summary>The top-level CoreWindow showing on <paramref name="monitor"/> that <paramref name="panel"/> says is one, as wide as the display or not (<paramref name="wide"/>).</summary>
    private static (HWND Window, RECT Rect)? ShowingOn(HMONITOR monitor, Func<HWND, bool> panel, RECT display, bool wide)
    {
        var after = HWND.NULL;
        fixed (char* name = "Windows.UI.Core.CoreWindow")
        {
            // Every top-level CoreWindow: a handful at most, the open store apps' and the shell's.
            while ((after = FindWindowExW(HWND.NULL, after, name, null)) != HWND.NULL)
            {
                RECT rect;
                if (Showing(after) && MonitorFromWindow(after, MONITOR.MONITOR_DEFAULTTONEAREST) == monitor && panel(after)
                    && GetWindowRect(after, &rect) && ShellPopupPlace.Spans(rect, display) == wide)
                {
                    return (after, rect);
                }
            }
        }
        return null;
    }
}
