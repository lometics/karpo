// What a bar asks of the shell about the strip of screen it holds, behind an interface so that
// the rules for holding it (AppbarHolder) can be tested against a shell that refuses, ignores
// and drifts on cue — and so that no test ever changes the real work area.

using Karpo.Core;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

internal interface IAppbarShell
{
    /// <summary>
    /// ABM_REMOVE, then ABM_NEW with <paramref name="callback"/> for the shell's notices. False
    /// when the shell would not take it — which it does now and then, and which is only ever
    /// noticed if someone looks (<see cref="AppbarHolder.Verify"/>).
    /// </summary>
    bool Register(HWND bar, uint callback);

    /// <summary>ABM_REMOVE: the shell forgets the bar and gives back what it held.</summary>
    void Remove(HWND bar);

    /// <summary>ABM_QUERYPOS: <paramref name="proposed"/> as the shell would have it, moved off any appbar already on that side.</summary>
    RECT Query(HWND bar, Edge edge, RECT proposed);

    /// <summary>
    /// ABM_SETPOS, and the rectangle the shell approved: <paramref name="strip"/> where it put it,
    /// moved off any appbar it counts on that side. The call's own answer is always TRUE and says
    /// nothing about whether the strip was taken; that is read from the work area (<see cref="WorkArea"/>).
    /// </summary>
    RECT SetPos(HWND bar, Edge edge, RECT strip);

    /// <summary>ABM_WINDOWPOSCHANGED: the bar's window moved.</summary>
    void Moved(HWND bar);

    /// <summary>
    /// The work area of the display <paramref name="monitor"/> as Windows has it now
    /// (GetMonitorInfo's rcWork): the ground truth of what the bars hold. Null when it can't be
    /// read, e.g. a display that has just gone.
    /// </summary>
    RECT? WorkArea(nint monitor);

    /// <summary>
    /// SPI_SETWORKAREA: the work area of the display <paramref name="work"/> lies on, set by the bar
    /// itself where the shell took its strip and did not keep the windows off it (AppbarHolder). False
    /// when it was not set — refused, or no Explorer on this desktop to be standing in for.
    /// </summary>
    bool SetWorkArea(RECT work);

    /// <summary>
    /// How the last register or position went, for the trace: which window SHAppBarMessage reached
    /// first — Explorer's taskbar, or Karpo's own tray on the way to it — and how long it took.
    /// Empty when there is nothing to say.
    /// </summary>
    string LastCall { get; }

    /// <summary>
    /// What Explorer is doing now, for the trace line of a check that found the strip missing: how
    /// long its taskbar takes to answer, whether it is shown, and how busy Karpo's tray has been passing
    /// icons on to it. Empty when there is nothing to say.
    /// </summary>
    string Describe();
}

/// <summary>The shell itself: SHAppBarMessage and GetMonitorInfo.</summary>
internal sealed unsafe class Win32AppbarShell : IAppbarShell
{
    public static readonly Win32AppbarShell Instance = new();

    private Win32AppbarShell()
    {
    }

    public string LastCall { get; private set; } = "";

    /// <summary>Notes, before one or more SHAppBarMessage calls, which Shell_TrayWnd they will reach first.</summary>
    private static (string Route, long Started) Begin() => (Route(), System.Diagnostics.Stopwatch.GetTimestamp());

    /// <summary>Records, after them, where they went and how long they took together (<see cref="LastCall"/>).</summary>
    private void End((string Route, long Started) begun) =>
        LastCall = $"{begun.Route}, {System.Diagnostics.Stopwatch.GetElapsedTime(begun.Started).TotalMilliseconds:0} ms";

    /// <summary>
    /// Where SHAppBarMessage goes: it finds the taskbar by class name, and while Karpo's tray runs its
    /// spy may be the first Shell_TrayWnd, in which case the call reaches Explorer through it.
    /// </summary>
    private static string Route()
    {
        HWND first;
        fixed (char* name = "Shell_TrayWnd")
        {
            first = FindWindowW(name, null);
        }
        if (first == HWND.NULL)
        {
            return "no taskbar";
        }
        uint process = 0;
        GetWindowThreadProcessId(first, &process);
        return process == GetCurrentProcessId() ? "through Karpo's tray" : "direct";
    }

    public string Describe()
    {
        var parts = new List<string>();
        var explorer = Taskbar.ExplorerTray();
        if (explorer == HWND.NULL)
        {
            parts.Add("no Explorer taskbar");
        }
        else
        {
            // WM_NULL does nothing; how long it takes to come back is how busy Explorer's taskbar is.
            var watch = System.Diagnostics.Stopwatch.StartNew();
            nuint answer = 0;
            var sent = SendMessageTimeoutW(explorer, WM.WM_NULL, 0, 0, (uint)SMTO_ABORTIFHUNG, 100, &answer);
            parts.Add(sent.Value == 0 ? "Explorer does not answer within 100 ms" : $"Explorer answers in {watch.ElapsedMilliseconds} ms"); // not-ui: the appbar trace
            RECT rc;
            _ = GetWindowRect(explorer, &rc);
            parts.Add($"its taskbar {(IsWindowVisible(explorer) ? "shown" : "hidden")} at {rc.left},{rc.top}..{rc.right},{rc.bottom}");
        }
        var now = Environment.TickCount64;
        var (forwarded, last, announced) = Tray.Traffic;
        if (announced != 0)
        {
            parts.Add($"TaskbarCreated {(now - announced) / 1000.0:0.0} s ago"); // not-ui: the appbar trace
        }
        if (forwarded > 0)
        {
            parts.Add($"Karpo's tray passed {forwarded} messages on, the last {now - last} ms ago"); // not-ui: the appbar trace
        }
        parts.Add($"first Shell_TrayWnd {Route()}");
        return string.Join("; ", parts);
    }

    public bool Register(HWND bar, uint callback)
    {
        var data = new APPBARDATA
        {
            cbSize = (uint)sizeof(APPBARDATA),
            hWnd = bar,
            uCallbackMessage = callback,
        };
        // Removed first: ABM_NEW refuses a window it already knows, and a bar registered again
        // (a move to another side, an Explorer restart, a repair) must end up known exactly once.
        var begun = Begin();
        SHAppBarMessage(ABM.ABM_REMOVE, &data);
        var taken = SHAppBarMessage(ABM.ABM_NEW, &data) != 0;
        End(begun);
        return taken;
    }

    public void Remove(HWND bar)
    {
        var data = new APPBARDATA { cbSize = (uint)sizeof(APPBARDATA), hWnd = bar };
        SHAppBarMessage(ABM.ABM_REMOVE, &data);
    }

    public RECT Query(HWND bar, Edge edge, RECT proposed)
    {
        var data = Data(bar, edge, proposed);
        SHAppBarMessage(ABM.ABM_QUERYPOS, &data);
        return data.rc;
    }

    public RECT SetPos(HWND bar, Edge edge, RECT strip)
    {
        var data = Data(bar, edge, strip);
        var begun = Begin();
        SHAppBarMessage(ABM.ABM_SETPOS, &data);
        End(begun);
        // The shell writes back where it put the strip, as it does for a query.
        return data.rc;
    }

    public void Moved(HWND bar)
    {
        var data = new APPBARDATA { cbSize = (uint)sizeof(APPBARDATA), hWnd = bar };
        SHAppBarMessage(ABM.ABM_WINDOWPOSCHANGED, &data);
    }

    public RECT? WorkArea(nint monitor)
    {
        var info = new MONITORINFO { cbSize = (uint)sizeof(MONITORINFO) };
        return monitor != 0 && GetMonitorInfoW((HMONITOR)(void*)monitor, &info) ? info.rcWork : null;
    }

    public bool SetWorkArea(RECT work)
    {
        // The work area is the session's, not a desktop's: a dock on a desktop of its own (a test, a
        // stage) has no Explorer it stands in for and must never touch the person's.
        if (Config.IsIsolated || Stage.On || Taskbar.ExplorerTray() == HWND.NULL)
        {
            return false;
        }
        // Announced as Explorer announces it, so maximized windows and every bar work theirs out again.
        return SystemParametersInfoW(SPI.SPI_SETWORKAREA, 0, &work, SPIF_SENDCHANGE);
    }

    private static APPBARDATA Data(HWND bar, Edge edge, RECT rc) => new()
    {
        cbSize = (uint)sizeof(APPBARDATA),
        hWnd = bar,
        uEdge = edge switch
        {
            Edge.Top => (uint)ABE_TOP,
            Edge.Left => (uint)ABE_LEFT,
            Edge.Right => (uint)ABE_RIGHT,
            _ => (uint)ABE_BOTTOM,
        },
        rc = rc,
    };
}
