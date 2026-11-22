// A made-up desktop for the one reconcile (Karpo.Dock\BarKeeper.cs, §272): displays, an Explorer that
// keeps an appbar list and works the work areas out of it — and that can be told to misbehave as the
// real one has (take a position and act on it late or never, drop one, refuse a registration, put the
// whole screen back on its own, count its taskbar for a while after it let go, restart) —, the windows
// in front, and the dock's side of it: what the dock does with each message (the same calls Dock.cs
// makes), the one settle timer, and the bars. Nothing here reaches the real shell, a real window or
// the real work area.
//
// The world also watches the rules the keeper must never break, at the moment they would be broken:
// nothing sent to the shell for a bar on a display with a full screen app, no work area set there, no
// bar shown there outside edit mode — and no message loop that never ends.

using Karpo.Core;
using Karpo.Dock;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Docking;

internal sealed class BarWorld : IAppbarShell, ITaskbarShell, IFrontWindows
{
    public const string One = @"\\.\DISPLAY1";
    public const string Two = @"\\.\DISPLAY2";

    /// <summary>Explorer's taskbar: 48 DIP at 150 %.</summary>
    public const int TaskbarThickness = 72;

    /// <summary>A 36-DIP bar at 150 %.</summary>
    public const int Band = 54;

    private const uint AutoHideState = 1;
    private const uint OnTopState = 2;

    // --- The displays -------------------------------------------------------------------------

    public sealed class Display(string device, nint monitor, RECT bounds)
    {
        public string Device { get; } = device;
        public nint Monitor { get; } = monitor;
        public RECT Bounds { get; set; } = bounds;

        /// <summary>The work area as Windows has it (GetMonitorInfo's rcWork).</summary>
        public RECT Work { get; set; } = bounds;
    }

    public readonly List<Display> Displays = [];

    public Display Of(string device) => Displays.Single(display => display.Device == device);

    private Display? ByMonitor(nint monitor) => Displays.Find(display => display.Monitor == monitor);

    private Display? Holding(RECT rect)
    {
        var x = (rect.left + rect.right) / 2;
        var y = (rect.top + rect.bottom) / 2;
        return Displays.Find(display => x >= display.Bounds.left && x < display.Bounds.right && y >= display.Bounds.top && y < display.Bounds.bottom);
    }

    public static RECT Rect(int left, int top, int right, int bottom) => new() { left = left, top = top, right = right, bottom = bottom };

    // --- Explorer -----------------------------------------------------------------------------

    /// <summary>Explorer runs; not yet, at a sign-in that started Karpo first.</summary>
    public bool Running { get; set; } = true;

    /// <summary>Its taskbar is auto-hidden (ABM_GETSTATE).</summary>
    public bool AutoHidden { get; private set; }

    /// <summary>Its taskbars are in sight.</summary>
    public bool TaskbarShown { get; private set; } = true;

    /// <summary>
    /// The auto-hide Windows keeps (StuckRects3): what a new Explorer comes up with. Here Explorer
    /// saves every ABM_SETSTATE there at once, Karpo's own included (§465; the real one saves later).
    /// </summary>
    public bool KeptAutoHide { get; set; }

    /// <summary>
    /// §591: the edge its taskbar stands on (TaskbarLocation), as a Windows with the position setting has it; null, one
    /// with no edge but the bottom. It auto-hides only at the bottom: on another edge it holds its strip whatever
    /// ABM_GETSTATE says (Microsoft: "not yet supported in alternate positions").
    /// </summary>
    public Edge? TaskbarEdge { get; set; }

    /// <summary>Whether Explorer's taskbar holds its strip: not auto-hidden, or on an edge it can't auto-hide on.</summary>
    private bool TaskbarHolds => Running && (!AutoHidden || (TaskbarEdge ?? Edge.Bottom) != Edge.Bottom);

    /// <summary>The appbars Explorer knows, in the order they registered, and the strip each holds (null: none yet).</summary>
    private readonly List<(HWND Bar, (Edge Edge, RECT Strip)? Held)> known = [];

    /// <summary>The next n ABM_NEW are refused.</summary>
    public int RefuseNew { get; set; }

    /// <summary>
    /// Explorer takes every ABM_SETPOS and keeps it, but works no work area out until this time — the
    /// dark screen of §212, and the slow starts before it. Then it does, on its own, and says so.
    /// </summary>
    public long StalledUntil { get; set; } = long.MinValue;

    /// <summary>Explorer applies each position this long after it was given, and says so then.</summary>
    public long ApplyDelay { get; set; }

    /// <summary>Every ABM_SETPOS is answered and dropped: neither kept nor acted on.</summary>
    public bool DropsEverything { get; set; }

    /// <summary>A work area the bars set themselves is put back at once to Explorer's own, and said so.</summary>
    public bool FightsDirectSets { get; set; }

    /// <summary>
    /// After its taskbar let go of its strip, Explorer still counts it on the second display this long
    /// (QUERYPOS puts a strip beside it), and drops a strip asked for on the edge there this long
    /// (the user's PC: up to 0.86 s).
    /// </summary>
    public long SecondCountsTaskbarFor { get; set; }

    public long SecondDropsEdgeFor { get; set; }

    private long letGoAt = long.MinValue;

    /// <summary>The taskbar let go of its strip less than <paramref name="span"/> ago; never, where it has not since Explorer started (no overflow from MinValue).</summary>
    private bool LetGoWithin(long span) => letGoAt != long.MinValue && Now - letGoAt < span;

    /// <summary>Every call the bars made to the shell, with the display it was for.</summary>
    public readonly List<(string Call, string Display)> Calls = [];

    // --- The windows ----------------------------------------------------------------------------

    public sealed class Window(nint handle, string className, string display, RECT rect)
    {
        public nint Handle { get; } = handle;
        public string Class { get; set; } = className;
        public string Display { get; set; } = display;
        public RECT Rect { get; set; } = rect;
        public bool Visible { get; set; } = true;
        public bool Minimized { get; set; }
        public bool Maximized { get; set; }
        public bool Captioned { get; set; } = true;
        public uint? Band { get; set; } = 1;
        public bool Own { get; set; }

        /// <summary>§702: held at the bottom of the z-order (a desktop widget, Themia's): coming to the front leaves it there.</summary>
        public bool Bottom { get; set; }

        /// <summary>§706: the window it was opened from (a flyout's, a menu's); 0 for none.</summary>
        public nint Owner { get; set; }
    }

    public readonly List<Window> Windows = [];

    /// <summary>§702: the z-order, top first: a window opens on top, and comes to the top as it comes to the front unless held at the bottom.</summary>
    public readonly List<nint> Stack = [];
    public nint ForegroundWindow { get; set; }
    public bool Direct3DFullScreen { get; set; }
    private nint nextWindow = 0x9000;

    public Window Open(string className, string display, RECT rect, bool captioned = true)
    {
        var window = new Window(nextWindow++, className, display, rect) { Captioned = captioned };
        Windows.Add(window);
        Stack.Insert(0, window.Handle);
        return window;
    }

    // --- The dock's side ------------------------------------------------------------------------

    public sealed class KeptBar(BarWorld world, HWND hwnd, string display) : IKeptBar
    {
        public string Display { get; set; } = display;
        public AppbarHolder Holder { get; } = new(world, hwnd, 0x8001, world.Trace, BarKeeper.Label(display), () => world.Now);
        AppbarHolder? IKeptBar.Holder => Holder;
        public HWND Hwnd { get; } = hwnd;
        public Edge Edge { get; set; } = Edge.Bottom;
        public int Thickness { get; set; } = BarWorld.Band;
        public bool MadeWay { get; private set; }
        public bool Shown => !MadeWay;

        /// <summary>A flyout or folder of this bar is open.</summary>
        public bool PopupOpen { get; set; }

        public void MakeWay(bool on)
        {
            MadeWay = on;
            if (on)
            {
                PopupOpen = false;
            }
            else if (world.Keeper.IsFullscreen(Display) && !world.Editing)
            {
                world.Broken.Add($"{Display}: a bar shown over a full screen app");
            }
        }

        public bool Reserve() =>
            !MadeWay && world.Displays.Exists(display => display.Device == Display)
            && Holder.Hold(Edge, Thickness, world.Of(Display).Bounds, world.Of(Display).Monitor);
    }

    public readonly List<KeptBar> Bars = [];
    public readonly BarKeeper Keeper;
    public readonly ExplorerSide Explorer;
    public AppbarTrace Trace { get; }
    public long Now { get; set; } = 1_000_000;

    /// <summary>Karpo is to hide Explorer's taskbar: always, but while the session ends (Dock.HidesTaskbar, §465).</summary>
    public bool HideTaskbar { get; set; } = true;

    public bool Editing { get; private set; }

    /// <summary>The rules broken, as they were broken.</summary>
    public readonly List<string> Broken = [];

    /// <summary>When the one settle timer fires; null while it is not armed.</summary>
    public long? SettleAt { get; private set; }

    /// <summary>How many times the settle timer fired.</summary>
    public int Settles { get; private set; }

    // What Explorer has said and not been heard yet: the dock's message queue, in order.
    private readonly Queue<Stir> queue = new();
    private readonly List<(long At, Action Do)> later = [];
    private nint nextBar = 0x1000;

    public BarWorld(AppbarTrace? trace = null, bool two = true)
    {
        Trace = trace ?? AppbarTrace.None;
        Displays.Add(new Display(One, 101, Rect(0, 0, 3840, 2160)));
        if (two)
        {
            Displays.Add(new Display(Two, 102, Rect(-3840, 0, 0, 2160)));
        }
        foreach (var display in Displays)
        {
            display.Work = Computed(display);
        }
        Explorer = new ExplorerSide(this);
        Keeper = new BarKeeper(Explorer, this, Trace, () => Now);
    }

    // --- What happens (each the way the dock hears it) ----------------------------------------

    /// <summary>Karpo starts: a bar a display, each registered as its window is made, then the first arrange.</summary>
    public void Start()
    {
        foreach (var display in Displays)
        {
            AddBar(display.Device);
        }
        Stirred(Stir.Start);
    }

    public KeptBar AddBar(string device)
    {
        var bar = new KeptBar(this, (HWND)nextBar, device);
        nextBar += 0x1000;
        Bars.Add(bar);
        bar.Holder.Register();
        return bar;
    }

    /// <summary>
    /// A reconcile for <paramref name="stir"/>, as Dock.Reconcile runs it, then everything it set off,
    /// as the message loop would deliver it.
    /// </summary>
    public void Stirred(Stir stir)
    {
        Reconcile(stir);
        Drain();
    }

    private void Reconcile(Stir stir)
    {
        if (Keeper.Reconcile(Bars, stir, HideTaskbar, Editing))
        {
            SettleAt = Now + BarKeeper.SettleMs;
        }
    }

    /// <summary>Delivers what Explorer said, until it has nothing more to say; fails on a loop.</summary>
    public void Drain()
    {
        for (var delivered = 0; queue.Count > 0; delivered++)
        {
            if (delivered > 200)
            {
                Broken.Add("a message loop that never ends: " + string.Join(", ", queue.Take(10)));
                queue.Clear();
                return;
            }
            Reconcile(queue.Dequeue());
        }
    }

    /// <summary>Time passes: what Explorer does late, and the settle timer, in the order they come.</summary>
    public void Wait(long ms)
    {
        var until = Now + ms;
        while (true)
        {
            var next = later.Count > 0 ? later.Min(item => item.At) : long.MaxValue;
            var settle = SettleAt ?? long.MaxValue;
            var due = Math.Min(next, settle);
            if (due > until)
            {
                break;
            }
            Now = Math.Max(Now, due);
            if (settle <= next)
            {
                SettleAt = null;
                Settles++;
                Reconcile(Stir.Settle);
            }
            else
            {
                var item = later.First(entry => entry.At == next);
                later.Remove(item);
                item.Do();
            }
            Drain();
        }
        Now = until;
    }

    /// <summary>Waits until nothing is pending any more, at most <paramref name="limit"/> ms.</summary>
    public void Settle(long limit = 600_000)
    {
        var end = Now + limit;
        while ((SettleAt is not null || later.Count > 0) && Now < end)
        {
            Wait(Math.Max(1, Math.Min((SettleAt ?? long.MaxValue) - Now, later.Count > 0 ? later.Min(item => item.At) - Now : long.MaxValue)));
        }
    }

    /// <summary>WM_DISPLAYCHANGE: the full screen look first, then the arrange and its reconcile (Dock.HostMessage).</summary>
    public void DisplaysChanged()
    {
        foreach (var display in Displays)
        {
            // Windows works the work area out again from nothing: the whole display.
            display.Work = display.Bounds;
        }
        Keeper.Look(Bars, Editing, Stir.Displays);
        if (StalledUntil <= Now)
        {
            foreach (var display in Displays)
            {
                Apply(display, say: false);
            }
        }
        Stirred(Stir.Displays);
    }

    /// <summary>A full screen app switches <paramref name="device"/> to a mode of its own (or back).</summary>
    public void Mode(string device, RECT bounds)
    {
        Of(device).Bounds = bounds;
        DisplaysChanged();
    }

    /// <summary>A display unplugged, or gone dark (a DisplayPort display asleep is a display removed).</summary>
    public void Unplug(string device)
    {
        Displays.RemoveAll(display => display.Device == device);
        foreach (var bar in Bars.Where(bar => bar.Display == device).ToList())
        {
            bar.Holder.Release();
            Bars.Remove(bar);
        }
        DisplaysChanged();
    }

    /// <summary>A display plugged in, or back from dark: a new display, its work area whole; a bar is made for it.</summary>
    public void Plug(string device, nint monitor, RECT bounds)
    {
        Displays.Add(new Display(device, monitor, bounds));
        AddBar(device);
        DisplaysChanged();
    }

    /// <summary>
    /// The screens went dark and came back (§212): every work area whole, and Explorer taking positions
    /// without working anything out for <paramref name="stall"/> ms.
    /// </summary>
    public void DarkScreen(long stall)
    {
        Stall(stall);
        foreach (var display in Displays)
        {
            display.Work = display.Bounds;
        }
        Stirred(Stir.DisplayLit);
    }

    /// <summary>
    /// Explorer takes every position from now on and works nothing out for <paramref name="ms"/>; then
    /// it does, on its own, from what it was given, and says so.
    /// </summary>
    public void Stall(long ms)
    {
        StalledUntil = Now + ms;
        At(Now + ms, () =>
        {
            foreach (var display in Displays)
            {
                Apply(display);
            }
        });
    }

    /// <summary>
    /// What may not be, even while Explorer is still catching up: a bar's strip under the windows — the
    /// work area reaching into it — on a display with no full screen app.
    /// </summary>
    public List<string> Overlapping() =>
        [.. Bars.Where(bar => bar.Thickness > 0 && !Keeper.IsFullscreen(bar.Display) && Displays.Exists(display => display.Device == bar.Display))
            .Where(bar => !AppbarGuard.Excludes(Of(bar.Display).Work, AppbarSpace.Fit(Of(bar.Display).Bounds, bar.Edge, bar.Thickness), bar.Edge))
            .Select(bar => $"{bar.Display}: the work area {AppbarHolder.Show(Of(bar.Display).Work)} reaches into the bar's strip")];

    /// <summary>Explorer puts a display's whole screen back on its own, saying so or not.</summary>
    public void ResetsWorkArea(string device, bool says)
    {
        var display = Of(device);
        display.Work = display.Bounds;
        if (says)
        {
            queue.Enqueue(Stir.WorkArea);
            Drain();
        }
    }

    /// <summary>
    /// Explorer ends and starts again — or starts for the first time, after Karpo, at a sign-in: it
    /// knows no appbar, and its taskbar comes up as the person has it, shown. Then TaskbarCreated.
    /// </summary>
    public void ExplorerRestarts()
    {
        known.Clear();
        Running = true;
        AutoHidden = KeptAutoHide;
        TaskbarShown = true;
        letGoAt = long.MinValue;
        foreach (var display in Displays)
        {
            Apply(display, say: false);
        }
        // Dock.TaskbarRestarted: the reconcile registers every bar again.
        Stirred(Stir.ExplorerBack);
    }

    /// <summary>The foreground changes to <paramref name="window"/> (EVENT_SYSTEM_FOREGROUND).</summary>
    public void Activate(Window? window)
    {
        ForegroundWindow = window?.Handle ?? 0;
        if (window is { Bottom: false })
        {
            Stack.Remove(window.Handle);
            Stack.Insert(0, window.Handle);
        }
        Stirred(Stir.Front);
    }

    /// <summary>A window moved, showed, hid or ended: heard only where FrontWatch watches it (BarKeeper.Watched).</summary>
    public void Changed(Window window)
    {
        if (Keeper.Watched().Contains(window.Handle))
        {
            Stirred(Stir.Moved);
        }
    }

    public void Close(Window window)
    {
        var watched = Keeper.Watched().Contains(window.Handle);
        Windows.Remove(window);
        if (ForegroundWindow == window.Handle)
        {
            ForegroundWindow = 0;
        }
        if (watched)
        {
            Stirred(Stir.Moved);
        }
    }

    public void BeginEdit()
    {
        Editing = true;
        Keeper.Look(Bars, Editing);
        Stirred(Stir.Build);
    }

    public void EndEdit()
    {
        Editing = false;
        Stirred(Stir.Build);
    }

    /// <summary>Karpo quits: Explorer's taskbar put back, every appbar removed (Dock.Shutdown).</summary>
    public void Quit()
    {
        if (Explorer.Taken)
        {
            Explorer.Give();
        }
        foreach (var bar in Bars)
        {
            bar.Holder.Release();
        }
        Bars.Clear();
        SettleAt = null;
    }

    /// <summary>
    /// Karpo crashes or is killed: nothing of its own runs; Explorer drops its appbars as their windows go,
    /// and the guard shows the taskbar (DockGuard: Taskbar.Restore, no state of its own).
    /// </summary>
    public void Crash()
    {
        foreach (var bar in Bars)
        {
            Remove(bar.Hwnd);
        }
        Bars.Clear();
        SettleAt = null;
        Taskbar.PutBack(this);
    }

    private void At(long when, Action action) => later.Add((when, action));

    // --- Explorer's work areas --------------------------------------------------------------------

    /// <summary>The work area Explorer works out for <paramref name="display"/> from its appbars and its taskbar.</summary>
    public RECT Computed(Display display)
    {
        var work = TaskbarHolds ? OffTaskbar(display.Bounds, display.Bounds, TaskbarEdge ?? Edge.Bottom) : display.Bounds;
        foreach (var (_, held) in known)
        {
            if (held is not { } one || AppbarSpace.IsEmpty(one.Strip) || Holding(one.Strip) != display)
            {
                continue;
            }
            work = AppbarGuard.Carved(work, one.Strip, one.Edge);
        }
        return work;
    }

    /// <summary>Explorer works <paramref name="display"/>'s work area out and, where it changed, says so (SPI_SETWORKAREA, ABN_POSCHANGED).</summary>
    private void Apply(Display display, bool say = true)
    {
        var work = Computed(display);
        var changed = !Same(work, display.Work);
        display.Work = work;
        if (changed && say)
        {
            queue.Enqueue(Stir.WorkArea);
            queue.Enqueue(Stir.Neighbours);
        }
    }

    private static bool Same(RECT a, RECT b) => a.left == b.left && a.top == b.top && a.right == b.right && a.bottom == b.bottom;

    private string DisplayOf(HWND bar) => Bars.Find(one => one.Hwnd == bar)?.Display ?? "?";

    private void Called(string call, HWND bar)
    {
        var display = DisplayOf(bar);
        Calls.Add((call, display));
        if (Keeper.IsFullscreen(display))
        {
            Broken.Add($"{display}: {call} sent for a bar on a display with a full screen app");
        }
    }

    // --- IAppbarShell ------------------------------------------------------------------------------

    public bool Register(HWND bar, uint callback)
    {
        Called("register", bar);
        Remove(bar, call: false);
        if (!Running)
        {
            return false;
        }
        if (RefuseNew > 0)
        {
            RefuseNew--;
            return false;
        }
        known.Add((bar, null));
        return true;
    }

    public void Remove(HWND bar) => Remove(bar, call: true);

    private void Remove(HWND bar, bool call)
    {
        if (call)
        {
            Calls.Add(("remove", DisplayOf(bar)));
        }
        var at = known.FindIndex(entry => entry.Bar == bar);
        if (at < 0)
        {
            return;
        }
        var held = known[at].Held;
        known.RemoveAt(at);
        if (held is { } one && Holding(one.Strip) is { } display && StalledUntil <= Now)
        {
            Apply(display);
        }
    }

    public RECT Query(HWND bar, Edge edge, RECT proposed)
    {
        Called("query", bar);
        var display = Holding(proposed);
        if (display is null)
        {
            return proposed;
        }
        var counts = TaskbarHolds
            || (display.Device == Two && LetGoWithin(SecondCountsTaskbarFor));
        return counts && edge == (TaskbarEdge ?? Edge.Bottom) ? OffTaskbar(proposed, display.Bounds, edge) : proposed;
    }

    /// <summary><paramref name="rect"/> off the strip Explorer's taskbar holds along <paramref name="edge"/> of <paramref name="bounds"/>.</summary>
    private static RECT OffTaskbar(RECT rect, RECT bounds, Edge edge)
    {
        switch (edge)
        {
            case Edge.Left:
                rect.left = Math.Max(rect.left, bounds.left + TaskbarThickness);
                break;
            case Edge.Top:
                rect.top = Math.Max(rect.top, bounds.top + TaskbarThickness);
                break;
            case Edge.Right:
                rect.right = Math.Min(rect.right, bounds.right - TaskbarThickness);
                break;
            default:
                rect.bottom = Math.Min(rect.bottom, bounds.bottom - TaskbarThickness);
                break;
        }
        return rect;
    }

    public RECT SetPos(HWND bar, Edge edge, RECT strip)
    {
        Called("setpos", bar);
        var at = known.FindIndex(entry => entry.Bar == bar);
        if (at < 0 || DropsEverything)
        {
            return strip;
        }
        // A strip of nothing lies on the display's very edge, in none of it: the bar's display.
        var display = AppbarSpace.IsEmpty(strip) ? Displays.Find(one => one.Device == DisplayOf(bar)) : Holding(strip);
        if (display is not null && display.Device == Two && LetGoWithin(SecondDropsEdgeFor) && strip.bottom == display.Bounds.bottom && !AppbarSpace.IsEmpty(strip))
        {
            // Asked for on the edge while Explorer still counts the taskbar that let go: dropped.
            return strip;
        }
        known[at] = (bar, (edge, strip));
        if (display is null || StalledUntil > Now)
        {
            return strip;
        }
        if (ApplyDelay > 0)
        {
            At(Now + ApplyDelay, () => Apply(display));
        }
        else
        {
            Apply(display);
        }
        return strip;
    }

    public void Moved(HWND bar)
    {
    }

    public RECT? WorkArea(nint monitor) => ByMonitor(monitor)?.Work;

    public bool SetWorkArea(RECT work)
    {
        var display = Holding(work);
        Calls.Add(("workarea", display?.Device ?? "?"));
        if (display is null || !Running)
        {
            return false;
        }
        if (Keeper.IsFullscreen(display.Device))
        {
            Broken.Add($"{display.Device}: the work area set on a display with a full screen app");
        }
        display.Work = work;
        queue.Enqueue(Stir.WorkArea);
        if (FightsDirectSets)
        {
            display.Work = Computed(display);
            queue.Enqueue(Stir.WorkArea);
        }
        return true;
    }

    public string LastCall => "";

    public string Describe() => "";

    // --- ITaskbarShell (what Taskbar.TakeOver, Rehide and PutBack ask) ---------------------------

    bool ITaskbarShell.Found => Running;

    uint ITaskbarShell.State() => (AutoHidden ? AutoHideState : 0) | OnTopState;

    void ITaskbarShell.SetState(uint state)
    {
        var hides = (state & AutoHideState) != 0;
        if (hides == AutoHidden)
        {
            return;
        }
        AutoHidden = hides;
        KeptAutoHide = hides;
        if (hides)
        {
            letGoAt = Now;
        }
        foreach (var display in Displays)
        {
            if (StalledUntil <= Now)
            {
                Apply(display);
            }
        }
        queue.Enqueue(Stir.Neighbours);
    }

    void ITaskbarShell.ShowBars() => TaskbarShown = true;

    Edge? ITaskbarShell.Edge => TaskbarEdge;

    TaskbarPosition.Moved ITaskbarShell.MoveTo(Edge edge)
    {
        if (TaskbarEdge is not { } now)
        {
            return TaskbarPosition.Moved.NotOffered;
        }
        if (now == edge)
        {
            return TaskbarPosition.Moved.Already;
        }
        MoveTaskbar(edge);
        return TaskbarPosition.Moved.Done;
    }

    /// <summary>§591: the person moves Windows' taskbar in Windows' settings: Explorer lays it out on that edge and says so.</summary>
    public void SettingsMoveTaskbar(Edge edge)
    {
        MoveTaskbar(edge);
        Drain();
    }

    private void MoveTaskbar(Edge edge)
    {
        TaskbarEdge = edge;
        foreach (var display in Displays)
        {
            if (StalledUntil <= Now)
            {
                Apply(display);
            }
        }
        queue.Enqueue(Stir.Neighbours);
    }

    // --- IFrontWindows -------------------------------------------------------------------------------

    nint IFrontWindows.Foreground() => ForegroundWindow;

    WindowFacts? IFrontWindows.Facts(nint handle) =>
        Windows.Find(window => window.Handle == handle) is { } window && Displays.Find(display => display.Device == window.Display) is { } display
            ? new WindowFacts(window.Class, window.Display, window.Rect, display.Bounds, window.Band, window.Visible, window.Minimized,
                Cloaked: false, window.Maximized, window.Captioned, window.Own)
            : null;

    bool IFrontWindows.Direct3D() => Direct3DFullScreen;

    string IFrontWindows.Program(nint window) => "game.exe";

    bool IFrontWindows.Under(nint window, nint over) =>
        Stack.IndexOf(window) is var at and >= 0 && Stack.IndexOf(over) is var top and >= 0 && at > top;

    nint IFrontWindows.TopOn(string display)
    {
        var self = (IFrontWindows)this;
        foreach (var handle in Stack)
        {
            if (self.Facts(handle) is { } facts && FullscreenApps.Fronts(facts) && facts.Display == display)
            {
                return handle;
            }
        }
        return 0;
    }

    nint IFrontWindows.Owner(nint handle)
    {
        var owner = Windows.Find(window => window.Handle == handle)?.Owner ?? 0;
        while (Windows.Find(window => window.Handle == owner) is { Owner: not 0 } up)
        {
            owner = up.Owner;
        }
        return owner;
    }

    // --- Explorer's taskbar as the keeper hides it (Dock.LiveExplorer, with the real pure rules) ---

    public sealed class ExplorerSide(BarWorld world) : IExplorerTaskbar
    {
        public bool Found => world.Running;

        public bool Taken { get; private set; }

        /// <summary>How many times Karpo took over; the guard started at each.</summary>
        public int Takes { get; private set; }

        public string Take()
        {
            Taskbar.TakeOver(world);
            world.TaskbarShown = false;
            Taken = true;
            Takes++;
            return "took over";
        }

        public string? Keep()
        {
            var lost = Taskbar.Rehide(world);
            if (!lost && !world.TaskbarShown)
            {
                return null;
            }
            world.TaskbarShown = false;
            return "kept";
        }

        public string Give()
        {
            var back = Taskbar.PutBack(world);
            Taken = false;
            return Taskbar.Says(back);
        }
    }

    // --- What should be -------------------------------------------------------------------------------

    /// <summary>
    /// What the person should see, now that everything has settled: each bar on a display with no full
    /// screen app shown and its strip held — the work area ending exactly at it —, each bar on one
    /// hidden (outside edit mode), and Explorer's taskbar hidden and auto-hidden while Karpo hides it,
    /// shown and not auto-hidden otherwise. What is wrong, or nothing.
    /// </summary>
    public List<string> Wrong()
    {
        var wrong = new List<string>(Broken);
        foreach (var bar in Bars)
        {
            if (Displays.Find(display => display.Device == bar.Display) is not { } display)
            {
                continue;
            }
            var full = Keeper.IsFullscreen(bar.Display);
            if (full && !Editing)
            {
                if (bar.Shown)
                {
                    wrong.Add($"{bar.Display}: a bar shown over a full screen app");
                }
                continue;
            }
            if (!bar.Shown)
            {
                wrong.Add($"{bar.Display}: a bar hidden with no full screen app");
            }
            if (bar.Thickness <= 0 || full)
            {
                continue;
            }
            var strip = AppbarSpace.Fit(display.Bounds, bar.Edge, bar.Thickness);
            if (!AppbarGuard.Excludes(display.Work, strip, bar.Edge))
            {
                wrong.Add($"{bar.Display}: the work area {AppbarHolder.Show(display.Work)} reaches into the bar's strip {AppbarHolder.Show(strip)}");
            }
            else if (HideTaskbar && !AppbarGuard.EndsAt(display.Work, strip, bar.Edge))
            {
                wrong.Add($"{bar.Display}: the work area {AppbarHolder.Show(display.Work)} ends short of the bar's strip {AppbarHolder.Show(strip)}");
            }
        }
        if (Running)
        {
            if (HideTaskbar && (!AutoHidden || TaskbarShown))
            {
                wrong.Add($"Explorer's taskbar {(TaskbarShown ? "shown" : "in sight")}, auto-hide {(AutoHidden ? "on" : "off")}, while Karpo runs");
            }
            if (!HideTaskbar && (AutoHidden || !TaskbarShown))
            {
                wrong.Add($"Explorer's taskbar not shown: auto-hide {(AutoHidden ? "on" : "off")}, {(TaskbarShown ? "shown" : "hidden")}");
            }
            // §591: on the bottom while Karpo runs, the one edge it auto-hides on.
            if (HideTaskbar && TaskbarEdge is { } at && at != Edge.Bottom)
            {
                wrong.Add($"Explorer's taskbar on the {at} while Karpo runs, holding its strip");
            }
        }
        return wrong;
    }
}
