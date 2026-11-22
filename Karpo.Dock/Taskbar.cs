// Hides the Windows taskbar while Karpo runs, and shows it again whenever Karpo does not (§465).
//
// The user, 2026-11-01: "when karpo is running the windows taskbar should never show. also not
// configurable or similar. when karpo is not running, if possible, the windows taskbar should show.
// (even if karpo crashes the windows taskbar should either appear or karpo should start again ...)"
// Before, a setting decided it, and putting the taskbar back meant setting "the person's own" state
// again, read from a record file or from StuckRects3. Explorer saves its live state into StuckRects3
// (it said auto-hide, 0x03, on the user's PC, and still did with the taskbar shown again), so an
// auto-hide of Karpo's that was saved there read back as the person's, and every put-back left the taskbar auto-hidden: turning
// the setting off changed nothing to see. Now nothing is kept: hidden is auto-hide on and its windows
// hidden; shown is auto-hide off and its windows shown, whoever shows it and however Karpo ended.
//
// Hiding takes three steps. Auto-hide gives the taskbar's strip of screen to
// maximized windows; hiding its windows stops it sliding in when the pointer
// reaches the edge, which is where Karpo's own reveal strip is. Explorer
// shows its taskbar again on its own whenever it lays it out (a notification icon
// coming or going, a work area, a display), so a WinEvent hook narrowed to
// the taskbar's thread hides it again, Task view or not (docs/widgets.md §89).
// That takes a few ms, and DWM drew the taskbar in them — over the bar, on a display
// where it stands at its edge — so every taskbar window has an empty window region
// too (§542): shown, it draws nothing and takes no press. PutBack gives it its own again.
//
// Showing it again is the one PutBack, from every way Karpo ends: a quit and the session ending
// (Dispose), an exception nothing caught (Program), and the `--guard` process, which waits for the
// dock to end however it ends, shows the taskbar at once and starts the dock again (DockGuard.cs).
// The dock watches its guard in turn and starts another should it go (Dock.SpawnGuard). Only both
// ended at the same moment leave the taskbar hidden, until Karpo starts again.
//
// Since build 26300.8493 Windows' taskbar may stand on any edge, but it auto-hides only at the bottom: hiding it moves
// it there first (TaskbarPosition.cs, §591). Putting it back leaves it there: no way Karpo ends moves it (§600).
//
// Explorer restarting is the one moment the hooks can't help: the new Explorer's
// taskbar is a new window on new threads, and shows itself. So the old Explorer's end is waited
// for (a thread-pool wait on its process, no polling), and from then until the new
// taskbar is covered one hook for every window shown anywhere hides it the moment
// it shows; the reconcile after TaskbarCreated (or the first one that finds the new
// taskbar) sets auto-hide again where it came up without it (Keep, Rehide). §272.

using System.Runtime.InteropServices;
using Karpo.Core;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

public sealed unsafe class Taskbar : IDisposable
{
    /// <summary>
    /// The name of the mutex a running dock holds, which is how a second dock
    /// and the guard both learn that one is up.
    /// </summary>
    public const string Instance = "Local\\karpo.dock";

    // The hook callback has no context argument, so it finds the live Taskbar
    // here. There is one at most: the dock drops the old one before hiding again.
    private static Taskbar? live;

    // The taskbar windows blanked (Blank), whether or not it took: Keep asks again only for a window not in here.
    private static readonly HashSet<HWND> blanked = [];

    private readonly List<HWINEVENTHOOK> hooks = [];
    private bool disposed;

    // The threads the hooks watch: a new Explorer's are others, and are hooked again (Keep).
    private List<(uint Process, uint Thread)> hooked = [];

    /// <summary>
    /// The window the old Explorer's end is told to (<see cref="ExplorerGone"/>), posted
    /// <see cref="WM_KARPO_EXPLORER_GONE"/>; the dock's host.
    /// </summary>
    public static HWND Host;

    /// <summary>Posted to <see cref="Host"/> when the Explorer whose taskbar Karpo hid has ended.</summary>
    public const uint WM_KARPO_EXPLORER_GONE = WM.WM_APP + 0x60;

    // The wait on the Explorer process whose taskbar is covered, and that process's id.
    private RegisteredWaitHandle? explorerWait;
    private ProcessEnd? explorerEnd;
    private uint explorerId;

    // While an Explorer restarts: one hook for every window shown anywhere, which hides the new
    // taskbar as it shows (ExplorerGone), until Cover hooks the new taskbar's threads.
    private HWINEVENTHOOK catching;

    private Taskbar()
    {
    }

    /// <summary>
    /// Null when there is no taskbar, which means Explorer isn't running.
    /// <para>
    /// appbar: the bars asked for their strips just before this, beside the taskbar (while it is not
    /// auto-hidden the shell moves any strip on its edge off it), and ask again straight after, on
    /// the edge (BarKeeper.Reconcile): for a moment both are held — the windows shrink, then grow —
    /// and no display is ever without a strip. Asked for only after, the edge held nothing until the
    /// shell took the bar's strip: 438 ms on the user's second display.
    /// </para>
    /// </summary>
    public static Taskbar? Hide()
    {
        var shell = new LiveShell();
        if (!shell.Found)
        {
            return null;
        }
        var parked = TakeOver(shell);
        var taskbar = new Taskbar { Parked = TaskbarPosition.Says(parked, TaskbarPosition.Parked) };
        taskbar.Cover();
        return taskbar;
    }

    /// <summary>§591: what moving Windows' taskbar to the bottom at the takeover came to, for the trace; null where it stood there.</summary>
    public string? Parked { get; private init; }

    /// <summary>
    /// Explorer's taskbar set to auto-hide, the rest of its state as it is — §591 at the bottom first, the one edge it
    /// auto-hides on. What the move came to. Pure but for <paramref name="shell"/>.
    /// </summary>
    internal static TaskbarPosition.Moved TakeOver(ITaskbarShell shell)
    {
        var parked = shell.MoveTo(TaskbarPosition.Parked);
        shell.SetState(shell.State() | (uint)ABS_AUTOHIDE);
        return parked;
    }

    /// <summary>What showing the taskbar again came to (<see cref="PutBack"/>).</summary>
    internal enum Back
    {
        /// <summary>No Explorer taskbar: nothing asked.</summary>
        NoTaskbar,

        /// <summary>Auto-hide off and read back so, its windows shown.</summary>
        Done,

        /// <summary>Asked, but Explorer still says auto-hide.</summary>
        NotTaken,
    }

    /// <summary>
    /// Explorer's taskbar shown: auto-hide off, the rest of its state as it is (§600: on the edge it stands on, never
    /// moved), and every display's taskbar window shown. The same whoever asks (a quit, the guard, a crash). Pure but
    /// for <paramref name="shell"/>.
    /// </summary>
    internal static Back PutBack(ITaskbarShell shell)
    {
        if (!shell.Found)
        {
            return Back.NoTaskbar;
        }
        shell.SetState(shell.State() & ~(uint)ABS_AUTOHIDE);
        shell.ShowBars();
        return AutoHides(shell.State()) ? Back.NotTaken : Back.Done;
    }

    /// <summary>What <see cref="PutBack"/> came to, as the appbar trace and karpo.log say it. Pure.</summary>
    internal static string Says(Back back) => back switch
    {
        Back.NoTaskbar => "Explorer's taskbar to be shown again, but there is none", // not-ui: a log
        Back.Done => "Explorer's taskbar shown again, auto-hide off", // not-ui: a log
        _ => "Explorer's taskbar asked to stop auto-hiding, and it still auto-hides", // not-ui: a log
    };

    /// <summary>
    /// Hides every taskbar window and watches their threads. Also what an
    /// Explorer restart needs: the new taskbar keeps the auto-hide state but
    /// shows itself.
    /// </summary>
    public void Cover()
    {
        Unhook();
        var threads = Threads(Bars(), hide: true);
        live = this;
        hooked = threads;
        // The new taskbar is hooked now; the hook for every window shown anywhere goes, and the
        // Explorer whose taskbar this is is the one whose end is waited for.
        StopCatching();
        WatchExplorer();
        foreach (var (process, thread) in threads)
        {
            // Out of context, so the callback runs on this thread through the
            // message loop and nothing is injected into Explorer.
            var hook = SetWinEventHook(
                (uint)EVENT.EVENT_OBJECT_SHOW,
                (uint)EVENT.EVENT_OBJECT_SHOW,
                HMODULE.NULL,
                &Shown,
                process,
                thread,
                (uint)WINEVENT_OUTOFCONTEXT);
            if (hook != HWINEVENTHOOK.NULL)
            {
                hooks.Add(hook);
            }
        }
    }

    /// <summary>The distinct threads of <paramref name="bars"/>, each hidden first with <paramref name="hide"/>.</summary>
    private static List<(uint Process, uint Thread)> Threads(List<HWND> bars, bool hide)
    {
        var threads = new List<(uint Process, uint Thread)>();
        foreach (var bar in bars)
        {
            if (hide)
            {
                // Blank first: a taskbar in sight goes at once, and every show after draws nothing (§542).
                Blank(bar);
                Surfaces.OtherApp(bar, SW.SW_HIDE);
            }
            uint owner = 0;
            uint thread = GetWindowThreadProcessId(bar, &owner);
            if (thread != 0 && !threads.Contains((owner, thread)))
            {
                threads.Add((owner, thread));
            }
        }
        return threads;
    }

    /// <summary>
    /// The taskbar hidden still as it should be (BarKeeper's reconcile): a new Explorer's taskbar
    /// may come up without auto-hide and shows itself, on threads the hooks don't watch. Auto-hide
    /// on again where Explorer lost it (<see cref="Rehide"/>), and the windows blanked, hidden and hooked again
    /// where any shows, the threads are new or a window has no blank region yet (<see cref="Off"/>). What the
    /// trace says; null when nothing was off.
    /// </summary>
    public string? Keep()
    {
        if (disposed)
        {
            return null;
        }
        var shell = new LiveShell();
        if (!shell.Found)
        {
            return null;
        }
        var lost = Rehide(shell);
        var bars = Bars();
        var shown = bars.Exists(bar => IsWindowVisible(bar));
        // As a set: the taskbars' order follows their z-order, which says nothing.
        var moved = !Threads(bars, hide: false).ToHashSet().SetEquals(hooked);
        // A taskbar window Explorer made anew (a display's, on a thread already watched) has no blank region.
        // One blanked in vain is not tried again here, or every reconcile would ask for one more.
        var bare = bars.Exists(bar => !blanked.Contains(bar) && !Surfaces.IsBlank(bar));
        if (Off(lost, shown, moved, bare) is not { } off)
        {
            return null;
        }
        Cover();
        return off;
    }

    /// <summary>
    /// What <see cref="Keep"/> found off, as the appbar trace says it, the first that holds; null where nothing
    /// was. <paramref name="lost"/>: auto-hide lost; <paramref name="shown"/>: a window shown; <paramref name="moved"/>:
    /// the windows on other threads than the hooks watch; <paramref name="bare"/>: a window without its blank
    /// region (§542). Pure.
    /// </summary>
    internal static string? Off(bool lost, bool shown, bool moved, bool bare) =>
        lost ? "Explorer's taskbar was not auto-hidden at the bottom (a new Explorer, Windows' settings): there and auto-hidden again, its windows hidden" // not-ui: the appbar trace
        : shown ? "Explorer's taskbar showed itself: hidden again" // not-ui: the appbar trace
        : moved ? "Explorer's taskbar has new windows: hidden and watched again" // not-ui: the appbar trace
        : bare ? "Explorer's taskbar has a window that would draw again: blanked and hidden" // not-ui: the appbar trace
        : null;

    /// <summary>A taskbar window of Explorer's blanked, where it is not yet (<see cref="Surfaces.OtherAppBlank"/>).</summary>
    private static void Blank(HWND bar)
    {
        if (!Surfaces.IsBlank(bar))
        {
            Surfaces.OtherAppBlank(bar, blank: true);
        }
        blanked.Add(bar);
    }

    /// <summary>
    /// Auto-hide on again where Explorer says it is off — a new Explorer after a restart, or the
    /// person's own switch in Windows' settings while Karpo runs —, §591 and Windows' taskbar back at the bottom where
    /// it was moved off it (Windows' settings again): on another edge it can't auto-hide, and holds its strip. True
    /// where either was off. Pure but for <paramref name="shell"/>.
    /// </summary>
    internal static bool Rehide(ITaskbarShell shell)
    {
        if (!shell.Found || (AutoHides(shell.State()) && shell.Edge is null or TaskbarPosition.Parked))
        {
            return false;
        }
        TakeOver(shell);
        return true;
    }

    /// <summary>
    /// Waits, on the thread pool, for the Explorer whose taskbar is covered to end: its end is the one
    /// moment no hook sees the new taskbar show (see the top of the file). Once per Explorer.
    /// </summary>
    private void WatchExplorer()
    {
        var tray = ExplorerTray();
        uint process = 0;
        if (tray == HWND.NULL || GetWindowThreadProcessId(tray, &process) == 0 || process == explorerId)
        {
            return;
        }
        StopWatchingExplorer();
        var handle = OpenProcess(SYNCHRONIZE, BOOL.FALSE, process);
        if (handle == HANDLE.NULL)
        {
            return;
        }
        explorerEnd = new ProcessEnd(handle);
        explorerWait = ThreadPool.RegisterWaitForSingleObject(explorerEnd, ExplorerEnded, null, Timeout.Infinite, executeOnlyOnce: true);
        explorerId = process;
    }

    private void StopWatchingExplorer()
    {
        // Not waited for: the callback only posts, and a post to a host that went is dropped.
        explorerWait?.Unregister(null);
        explorerWait = null;
        explorerEnd?.Dispose();
        explorerEnd = null;
        explorerId = 0;
    }

    /// <summary>On the thread pool: the Explorer whose taskbar is covered has ended. Told to the host.</summary>
    private static void ExplorerEnded(object? state, bool timedOut)
    {
        try
        {
            if (Host != HWND.NULL)
            {
                _ = PostMessageW(Host, WM_KARPO_EXPLORER_GONE, 0, 0);
            }
        }
        catch (Exception error)
        {
            Troubles.Report("Taskbar: Explorer ended", error);
        }
    }

    /// <summary>A process's handle, waited on for its end (SYNCHRONIZE).</summary>
    internal sealed class ProcessEnd : WaitHandle
    {
        public ProcessEnd(HANDLE process) => SafeWaitHandle = new Microsoft.Win32.SafeHandles.SafeWaitHandle((nint)process, ownsHandle: true);
    }

    /// <summary>
    /// The Explorer whose taskbar is covered has ended (<see cref="WM_KARPO_EXPLORER_GONE"/>): until
    /// the new one's taskbar is covered, every window shown anywhere is looked at, and a taskbar of
    /// Explorer's hidden the moment it shows. Out of context, so nothing is injected into any app.
    /// </summary>
    public void ExplorerGone()
    {
        if (disposed)
        {
            return;
        }
        StopWatchingExplorer();
        Unhook();
        hooked = [];
        if (catching == HWINEVENTHOOK.NULL)
        {
            catching = SetWinEventHook((uint)EVENT.EVENT_OBJECT_SHOW, (uint)EVENT.EVENT_OBJECT_SHOW, HMODULE.NULL, &Shown, 0, 0, (uint)WINEVENT_OUTOFCONTEXT);
        }
    }

    private void StopCatching()
    {
        if (catching != HWINEVENTHOOK.NULL)
        {
            UnhookWinEvent(catching);
            catching = HWINEVENTHOOK.NULL;
        }
    }

    /// <summary>Unhooks and puts the taskbar back. Only the first call does anything.</summary>
    public void Dispose() => PutItBack();

    /// <summary>
    /// <see cref="Dispose"/>, saying what putting the taskbar back came to (<see cref="Says"/>), for
    /// the appbar trace; null where it was done already.
    /// </summary>
    public string? PutItBack()
    {
        if (disposed)
        {
            return null;
        }
        disposed = true;
        Unhook();
        StopCatching();
        StopWatchingExplorer();
        if (live == this)
        {
            live = null;
        }
        blanked.Clear();
        return Restore();
    }

    private void Unhook()
    {
        foreach (var hook in hooks)
        {
            UnhookWinEvent(hook);
        }
        hooks.Clear();
    }

    /// <summary>Shows the taskbar again (<see cref="PutBack"/>). Safe to call more than once, from any process. What it came to (<see cref="Says"/>).</summary>
    public static string Restore() => Says(PutBack(new LiveShell()));

    [UnmanagedCallersOnly]
    private static void Shown(HWINEVENTHOOK hook, uint kind, HWND hwnd, int idObject, int idChild, uint thread, uint time)
    {
        // Out-of-context events wait in this thread's queue, so one can still
        // arrive after the hooks came off; it must not hide a taskbar that was
        // just put back. Every window shown anywhere comes here, an app's
        // starting included: nothing thrown may unwind into user32.
        try
        {
            if (idObject == OBJID.OBJID_WINDOW && live is not null && IsBar(hwnd))
            {
                Surfaces.OtherApp(hwnd, SW.SW_HIDE);
                // Blank already, but for a window Explorer made anew: its next show draws nothing (§542).
                Blank(hwnd);
            }
        }
        catch (Exception error)
        {
            Troubles.Report("Taskbar: a window shown", error);
        }
    }

    /// <summary>
    /// A taskbar window of Explorer's. Karpo's tray spy has the same class and
    /// must never count: hiding it is pointless, and showing it again on
    /// restore would be wrong.
    /// </summary>
    private static bool IsBar(HWND hwnd)
    {
        char* name = stackalloc char[32];
        int length = GetClassNameW(hwnd, name, 32);
        if (new ReadOnlySpan<char>(name, length) is not ("Shell_TrayWnd" or "Shell_SecondaryTrayWnd"))
        {
            return false;
        }
        uint process = 0;
        GetWindowThreadProcessId(hwnd, &process);
        return process != GetCurrentProcessId();
    }

    /// <summary>
    /// Explorer's own Shell_TrayWnd. While Karpo's tray runs, its spy has the same
    /// class and sits first in z-order, so a plain FindWindow finds Karpo itself
    /// and would report a taskbar even with Explorer gone.
    /// </summary>
    internal static HWND ExplorerTray()
    {
        uint own = GetCurrentProcessId();
        fixed (char* name = "Shell_TrayWnd")
        {
            var candidate = HWND.NULL;
            while ((candidate = FindWindowExW(HWND.NULL, candidate, name, null)) != HWND.NULL)
            {
                uint process = 0;
                GetWindowThreadProcessId(candidate, &process);
                if (process != own)
                {
                    return candidate;
                }
            }
        }
        return HWND.NULL;
    }

    /// <summary>
    /// appbar: whether Explorer's taskbar holds a strip of the screen now — it is there, and not
    /// auto-hidden. A Karpo that ended with its guard (both killed at once, as ending Karpo's group
    /// in Task Manager does) leaves it auto-hidden, holding nothing, until the next start puts it
    /// back or takes over (2026-09-27, 21:11: the next start found every display's work area whole).
    /// </summary>
    public static bool HoldsStrip()
    {
        var tray = ExplorerTray();
        return tray != HWND.NULL && !AutoHides(State(tray));
    }

    /// <summary>Whether ABM_GETSTATE's answer says auto-hide.</summary>
    internal static bool AutoHides(uint state) => (state & ABS_AUTOHIDE) != 0;

    /// <summary>
    /// The primary taskbar and one per extra monitor, Explorer's: Karpo's tray spy has the class too.
    /// <para>
    /// By FindWindowEx, never EnumWindows: EnumWindows lists only band-1 windows (desktop apps'), and
    /// Explorer lifts its taskbars to band 6 while Task view is up, where hiding them on a new Explorer
    /// or showing them as Karpo ends would miss them (2026-10-11).
    /// </para>
    /// </summary>
    internal static List<HWND> Bars()
    {
        var found = new List<HWND>(2);
        uint own = GetCurrentProcessId();
        foreach (var bar in TopLevel("Shell_TrayWnd").Concat(TopLevel("Shell_SecondaryTrayWnd")))
        {
            uint process = 0;
            GetWindowThreadProcessId(bar, &process);
            if (process != own)
            {
                found.Add(bar);
            }
        }
        return found;
    }

    /// <summary>
    /// The top-level windows of class <paramref name="name"/> (a few at most), hidden ones included,
    /// in whatever z-band: FindWindowEx walks them all, where EnumWindows skips all but band 1.
    /// </summary>
    internal static List<HWND> TopLevel(string name)
    {
        var found = new List<HWND>(2);
        fixed (char* className = name)
        {
            var window = HWND.NULL;
            while (found.Count < 16 && (window = FindWindowExW(HWND.NULL, window, className, null)) != HWND.NULL)
            {
                found.Add(window);
            }
        }
        return found;
    }

    private static uint State(HWND tray)
    {
        var data = new APPBARDATA
        {
            cbSize = (uint)sizeof(APPBARDATA),
            hWnd = tray,
        };
        return (uint)SHAppBarMessage((uint)ABM.ABM_GETSTATE, &data);
    }

    private static void SetState(HWND tray, uint state)
    {
        var data = new APPBARDATA
        {
            cbSize = (uint)sizeof(APPBARDATA),
            hWnd = tray,
            lParam = (nint)state,
        };
        SHAppBarMessage((uint)ABM.ABM_SETSTATE, &data);
    }

    /// <summary>Explorer's taskbar as it is, for <see cref="TakeOver"/> and <see cref="PutBack"/>.</summary>
    private sealed class LiveShell : ITaskbarShell
    {
        private static readonly LiveTaskbarPosition Position = new();
        private readonly HWND tray = ExplorerTray();

        public Edge? Edge => TaskbarPosition.Current(Position);

        public TaskbarPosition.Moved MoveTo(Edge edge) => TaskbarPosition.Move(Position, edge);

        public bool Found => tray != HWND.NULL;

        public uint State() => Taskbar.State(tray);

        public void SetState(uint state) => Taskbar.SetState(tray, state);

        public void ShowBars()
        {
            foreach (var bar in Bars())
            {
                // Its region its own again first (§542): a taskbar shown blank is no taskbar.
                Surfaces.OtherAppBlank(bar, blank: false);
                Surfaces.OtherApp(bar, SW.SW_SHOWNA);
            }
        }
    }
}

/// <summary>Explorer's taskbar as hiding it and putting it back see it (Taskbar.TakeOver, Taskbar.PutBack); faked in the tests.</summary>
internal interface ITaskbarShell
{
    /// <summary>Explorer runs, and has a taskbar.</summary>
    bool Found { get; }

    /// <summary>ABM_GETSTATE.</summary>
    uint State();

    /// <summary>ABM_SETSTATE.</summary>
    void SetState(uint state);

    /// <summary>Every taskbar of Explorer's shown again, each display's, its own region back (§542).</summary>
    void ShowBars();

    /// <summary>§591: the edge Windows' taskbar stands on; null where it has none but the bottom (<see cref="TaskbarPosition.Current"/>).</summary>
    Edge? Edge { get; }

    /// <summary>§591: Windows' taskbar moved to <paramref name="edge"/> as Settings moves it (<see cref="TaskbarPosition.Move"/>).</summary>
    TaskbarPosition.Moved MoveTo(Edge edge);
}
