// What the full screen look hears and reads (BarKeeper.Look, FullscreenApps): which window came to
// the front, and when one of the windows it depends on moves, shows, hides or ends.
//
// WinEvent hooks, out of context, so they call back on the dock's thread through its message loop and
// nothing is injected anywhere: one for EVENT_SYSTEM_FOREGROUND, for every app (and one for a window minimized
// or restored, which only the app buttons' active plate reads, §619), and two per thread of
// the windows the look depends on (the foreground window, and each full screen display's front:
// BarKeeper.Watched) — EVENT_OBJECT_DESTROY, _SHOW and _HIDE, and EVENT_OBJECT_LOCATIONCHANGE —, which
// is how a game going borderless full screen, a video leaving it, a window minimized or closed are
// heard; never the focus, selection and state events between them, which an app's thread raises at
// every key. A thread is watched only while one of its windows is, so a window dragged across the
// screen wakes the dock for its own moves and no other app's. The callbacks post one message to the
// host (WM_KARPO_FRONT), at most one waiting at a time; the reconcile it runs reads the windows as they
// are then, never what the event said.

using System.Runtime.InteropServices;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

internal sealed unsafe class FrontWatch : IFrontWindows
{
    /// <summary>Posted to the host: the foreground changed (wParam 0), or a watched window moved, showed, hid or ended (1).</summary>
    public const uint WM_KARPO_FRONT = WM.WM_APP + 0x61;

    /// <summary>
    /// §619: posted to the host: a window was minimized or restored (EVENT_SYSTEM_MINIMIZESTART, _END). The
    /// window minimized last may keep the foreground where nothing else takes it, and no activation says
    /// so: the active button is read again (Dock.Front, which never counts a minimized window).
    /// </summary>
    public const uint WM_KARPO_MINIMIZED = WM.WM_APP + 0x65;

    private const uint EventObjectDestroy = 0x8001;
    private const uint EventObjectHide = 0x8003;
    private const uint EventObjectLocationChange = 0x800B;
    private const uint DwmwaCloaked = 14;
    private const int QunsRunningD3DFullScreen = 3;

    // The callbacks have no context argument: the one live watch, and what it posts to.
    private static FrontWatch? live;

    private readonly HWND host;
    private HWINEVENTHOOK foregroundHook;
    private HWINEVENTHOOK minimizeHook;
    private readonly Dictionary<(uint Process, uint Thread), (HWINEVENTHOOK Shown, HWINEVENTHOOK Moved)> threadHooks = [];
    private HashSet<nint> watched = [];

    // One message waiting at a time, per kind.
    private bool frontPosted;
    private bool movePosted;
    private bool minimizedPosted;

    public FrontWatch(HWND host) => this.host = host;

    /// <summary>Starts hearing the foreground; again does nothing.</summary>
    public void Start()
    {
        if (foregroundHook != HWINEVENTHOOK.NULL)
        {
            return;
        }
        live = this;
        foregroundHook = SetWinEventHook(EVENT.EVENT_SYSTEM_FOREGROUND, EVENT.EVENT_SYSTEM_FOREGROUND, HMODULE.NULL, &Heard, 0, 0, WINEVENT_OUTOFCONTEXT);
        minimizeHook = SetWinEventHook(EVENT.EVENT_SYSTEM_MINIMIZESTART, EVENT.EVENT_SYSTEM_MINIMIZEEND, HMODULE.NULL, &Heard, 0, 0, WINEVENT_OUTOFCONTEXT);
    }

    /// <summary>Every hook lets go.</summary>
    public void Stop()
    {
        if (foregroundHook != HWINEVENTHOOK.NULL)
        {
            _ = UnhookWinEvent(foregroundHook);
            foregroundHook = HWINEVENTHOOK.NULL;
        }
        if (minimizeHook != HWINEVENTHOOK.NULL)
        {
            _ = UnhookWinEvent(minimizeHook);
            minimizeHook = HWINEVENTHOOK.NULL;
        }
        foreach (var hooks in threadHooks.Values)
        {
            Unhook(hooks);
        }
        threadHooks.Clear();
        watched = [];
        if (live == this)
        {
            live = null;
        }
    }

    /// <summary>§619: the host read the active button again (<see cref="WM_KARPO_MINIMIZED"/>): the next minimize or restore posts again.</summary>
    public void MinimizedHandled() => minimizedPosted = false;

    /// <summary>The host handled what was posted (<paramref name="moved"/>: a watched window's): the next event posts again.</summary>
    public void Handled(bool moved)
    {
        if (moved)
        {
            movePosted = false;
        }
        else
        {
            frontPosted = false;
        }
    }

    /// <summary>
    /// The windows whose moves, shows, hides and end matter now (<see cref="BarKeeper.Watched"/>): their
    /// threads are hooked, and only theirs; a thread none of them is on any more lets go.
    /// </summary>
    public void Watch(IReadOnlyList<nint> windows)
    {
        watched = [.. windows];
        var threads = new HashSet<(uint Process, uint Thread)>();
        foreach (var window in windows)
        {
            uint process = 0;
            var thread = GetWindowThreadProcessId((HWND)window, &process);
            if (thread != 0 && process != GetCurrentProcessId())
            {
                threads.Add((process, thread));
            }
        }
        foreach (var gone in threadHooks.Keys.Where(key => !threads.Contains(key)).ToList())
        {
            Unhook(threadHooks[gone]);
            threadHooks.Remove(gone);
        }
        foreach (var (process, thread) in threads)
        {
            if (threadHooks.ContainsKey((process, thread)))
            {
                continue;
            }
            threadHooks[(process, thread)] = (
                SetWinEventHook(EventObjectDestroy, EventObjectHide, HMODULE.NULL, &Heard, process, thread, WINEVENT_OUTOFCONTEXT),
                SetWinEventHook(EventObjectLocationChange, EventObjectLocationChange, HMODULE.NULL, &Heard, process, thread, WINEVENT_OUTOFCONTEXT));
        }
    }

    private static void Unhook((HWINEVENTHOOK Shown, HWINEVENTHOOK Moved) hooks)
    {
        if (hooks.Shown != HWINEVENTHOOK.NULL)
        {
            _ = UnhookWinEvent(hooks.Shown);
        }
        if (hooks.Moved != HWINEVENTHOOK.NULL)
        {
            _ = UnhookWinEvent(hooks.Moved);
        }
    }

    [UnmanagedCallersOnly]
    private static void Heard(HWINEVENTHOOK hook, uint evt, HWND hwnd, int obj, int child, uint thread, uint time)
    {
        // Nothing thrown may unwind into user32.
        try
        {
            if (live is not { } watch || obj != OBJID.OBJID_WINDOW || child != 0)
            {
                return;
            }
            if (evt is EVENT.EVENT_SYSTEM_MINIMIZESTART or EVENT.EVENT_SYSTEM_MINIMIZEEND)
            {
                if (!watch.minimizedPosted)
                {
                    watch.minimizedPosted = PostMessageW(watch.host, WM_KARPO_MINIMIZED, 0, 0);
                }
            }
            else if (evt == EVENT.EVENT_SYSTEM_FOREGROUND)
            {
                if (!watch.frontPosted)
                {
                    watch.frontPosted = PostMessageW(watch.host, WM_KARPO_FRONT, 0, 0);
                }
            }
            else if (watch.watched.Contains((nint)hwnd) && !watch.movePosted)
            {
                watch.movePosted = PostMessageW(watch.host, WM_KARPO_FRONT, 1, 0);
            }
        }
        catch (Exception)
        {
            // A host gone between the event and the post: nothing to tell.
        }
    }

    // --- The windows as they are now (IFrontWindows) ---------------------------------------------

    public nint Foreground() => GetForegroundWindow();

    public WindowFacts? Facts(nint window) => Read((HWND)window);

    /// <summary>The window as it is now; null where it is gone.</summary>
    internal static WindowFacts? Read(HWND window)
    {
        if (window == HWND.NULL || !IsWindow(window))
        {
            return null;
        }
        // Long enough for the longest shell class name there is ("Shell_SecondaryTrayWnd").
        var buffer = stackalloc char[64];
        var length = GetClassNameW(window, buffer, 64);
        var name = new string(buffer, 0, Math.Max(length, 0));
        RECT rect;
        if (!GetWindowRect(window, &rect))
        {
            return null;
        }
        var monitor = MonitorFromWindow(window, MONITOR.MONITOR_DEFAULTTONEAREST);
        var info = new MONITORINFOEXW();
        info.Base.cbSize = (uint)sizeof(MONITORINFOEXW);
        if (monitor == HMONITOR.NULL || !GetMonitorInfoW(monitor, (MONITORINFO*)&info))
        {
            return null;
        }
        var style = (uint)GetWindowLongPtrW(window, GWL.GWL_STYLE);
        uint cloaked = 0;
        _ = DwmGetWindowAttribute(window, DwmwaCloaked, &cloaked, sizeof(uint));
        uint process = 0;
        _ = GetWindowThreadProcessId(window, &process);
        return new WindowFacts(
            Class: name,
            Display: new string(&info.szDevice.e0),
            Rect: rect,
            DisplayBounds: info.Base.rcMonitor,
            Band: WindowBand.Of(window),
            Visible: IsWindowVisible(window),
            Minimized: IsIconic(window),
            Cloaked: cloaked != 0,
            Maximized: IsZoomed(window),
            Captioned: (style & WS.WS_CAPTION) == WS.WS_CAPTION,
            Own: FullscreenApps.IsKarpos(name, process == GetCurrentProcessId()));
    }

    public string Program(nint window)
    {
        uint process = 0;
        _ = GetWindowThreadProcessId((HWND)window, &process);
        return Path.GetFileName(Apps.ImagePath(process) ?? "");
    }

    /// <summary>§702: walks up the z-order from <paramref name="window"/>; under where it meets <paramref name="over"/>.</summary>
    public bool Under(nint window, nint over)
    {
        var at = (HWND)window;
        for (var step = 0; step < 4096 && at != HWND.NULL; step++)
        {
            at = GetWindow(at, GW_HWNDPREV);
            if (at == (HWND)over)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>§706: the root owner (GA_ROOTOWNER); 0 where it is the window itself or none.</summary>
    public nint Owner(nint window)
    {
        var owner = GetAncestor((HWND)window, GA.GA_ROOTOWNER);
        return owner == (HWND)window ? 0 : (nint)owner;
    }

    /// <summary>§707: walks down the z-order from the top; the first window that would front <paramref name="display"/>.</summary>
    public nint TopOn(string display)
    {
        var at = GetTopWindow(HWND.NULL);
        for (var step = 0; step < 4096 && at != HWND.NULL; step++, at = GetWindow(at, GW_HWNDNEXT))
        {
            var extended = (uint)GetWindowLongPtrW(at, GWL.GWL_EXSTYLE);
            if ((extended & ((uint)WS.WS_EX_NOACTIVATE | (uint)WS.WS_EX_TOOLWINDOW)) != 0 || !IsWindowVisible(at))
            {
                continue;
            }
            if (Read(at) is { } facts && FullscreenApps.Fronts(facts)
                && string.Equals(facts.Display, display, StringComparison.OrdinalIgnoreCase))
            {
                return at;
            }
        }
        return 0;
    }

    public bool Direct3D()
    {
        int state;
        return SHQueryUserNotificationState(&state) >= 0 && state == QunsRunningD3DFullScreen;
    }

    [DllImport("shell32.dll", ExactSpelling = true)]
    private static extern int SHQueryUserNotificationState(int* state);
}
