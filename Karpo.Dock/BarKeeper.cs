// The one place that decides where Karpo's bars are, whether they are shown, whether they hold their
// strips, and whether Explorer's taskbar is hidden (§272).
//
// The user: "the karpo taskbar locking needs to be bullet proof in every scenario ... there should be
// zero bugs for the hiding / showing of the karpo bar, also the windows taskbar ... in general if you
// need timers anywhere try to solve it differently." Before this, each of those had its own mechanism
// — a check every half second, then every two, with a backoff, per bar; a 50 ms poll for five seconds
// at the takeover; wake bursts; a full screen app decided once, on the shell's word, against the
// display as the last arrange had it — and the full screen app was where they failed: Anno 1800
// switching the display to 640x480 for its exclusive full screen made the dock arrange, lay the bar
// out on the new mode (topmost, in front of the game), ask for its strip there and look after it with
// SPI_SETWORKAREA — and the game gave the display up (appbar.log, 2026-10-21 12:23 and 13:08: a strip
// asked for at 0,439..640,480, the display back at 3840x2160 a second later).
//
// Now there is one reconcile (Reconcile): what should be (the settings, the displays, which display
// has a full screen app) against what is (each display's work area, Explorer's taskbar, the bars'
// windows), and the least that brings one to the other. It runs on events and only on events:
//
//   from Windows                                   Stir           what it may have changed
//   WM_DISPLAYCHANGE, WM_DPICHANGED                Displays/Scale displays, modes, work areas
//   WM_POWERBROADCAST resume                       Resumed        displays, work areas
//   WTS_SESSION_UNLOCK                             Unlocked       displays (a remote session's)
//   GUID_SESSION_DISPLAY_STATUS on                 DisplayLit     a display back from dark
//   TaskbarCreated                                 ExplorerBack   Explorer knows no appbar
//   WM_SETTINGCHANGE(SPI_SETWORKAREA)              WorkArea       a work area
//   ABN_POSCHANGED, ABN_STATECHANGE                Neighbours     another appbar, the taskbar
//   EVENT_SYSTEM_FOREGROUND                        Front          which window is a display's front
//   the front windows' moves, shows, hides, ends   Moved          whether a display is full screen
//   ABN_FULLSCREENAPP                              Front          (taken as a hint, never as truth)
//   from Karpo
//   start, an arrange, a setting changed           Start/Build/Settings
//   edit mode on or off                            Build          bars shown whatever is in front
//
// A disturbance (Stirs.Disturbs) is something that can change a work area without a word to the
// bars: it makes every bar's repair budget whole again and asks for one settle. The others are the
// consequences of a change (often Karpo's own) and must never refill a budget, or a shell that puts
// the whole screen back at every repair would be fought for ever.
//
// A reconcile, in order:
//   1. Full screen (Look): each display's front window looked at as it is now, against the display as
//      it is now; a display that turned full screen has its bars hidden at once (not only put behind),
//      before anything is laid out — the arrange after a change of display mode builds no bar on it;
//      one that stopped being full screen has them laid out and shown, and counts as a disturbance.
//      Edit mode shows every bar whatever is in front.
//   2. Strips: each bar not on a full screen display pushes what it should hold (Reserve, sending only
//      a change), then each looks at its work area (Verify, asking the shell nothing). A bar that just
//      sent something is given until the settle to show it. A strip missing, or a registration the
//      shell refused, is repaired by the ladder (AppbarHolder.Repair), within the budget.
//   3. Explorer's taskbar: hidden (auto-hide, its windows hidden) whenever Karpo runs (§465) and the
//      session is not ending, shown (auto-hide off, its windows shown) otherwise; after an Explorer restart its
//      new taskbar is hidden and auto-hidden again (IExplorerTaskbar.Keep). After the strips, so at the
//      takeover every bar holds its strip beside the taskbar before the taskbar lets go of its own.
// A display with a full screen app is never touched: no layout, no topmost, no strip, no work area,
// no activation. Nothing here activates any window, ever.
//
// The one timer left (Dock's RECONCILE, one-shot, SettleMs): Explorer can take an ABM_SETPOS and not
// act on it, or a display can come back with its work area whole, and say nothing about it (§212). So
// a reconcile that sent something, or follows a disturbance, asks for one more look SettleMs later;
// that look repairs what is still missing, within the budget, and asks again only where it sent
// something itself. When all is well nothing runs at all. The budget (AppbarGuard.RepairsPerWindow in
// RepairWindowMs, and no repair within SettleMs of the last thing sent for a bar, so the several
// notices one change brings never spend it in a moment) is what makes "never a loop" hold for any
// shell, however it answers.
//
// After an Explorer restart (ExplorerBack) every bar is registered again first — a full screen app's
// display's only once the app is gone (AppbarHolder.Lost).

using TerraFX.Interop.Windows;

namespace Karpo.Dock;

/// <summary>What made a reconcile run (see the top of the file).</summary>
public enum Stir
{
    Start,
    Displays,
    Scale,
    Resumed,
    Unlocked,
    DisplayLit,
    ExplorerBack,
    Settings,
    Build,
    WorkArea,
    Neighbours,
    Settle,
    Front,
    Moved,
}

internal static class Stirs
{
    /// <summary>Whether <paramref name="stir"/> can change a work area without a word to the bars: every repair budget whole again, and a settle.</summary>
    public static bool Disturbs(this Stir stir) => stir is Stir.Start or Stir.Displays or Stir.Scale or Stir.Resumed
        or Stir.Unlocked or Stir.DisplayLit or Stir.ExplorerBack or Stir.Settings;

    /// <summary>
    /// Whether a reconcile for <paramref name="stir"/> asks the shell nothing it need not: the front
    /// window changed or moved. It looks at the full screen apps and at the work areas (GetMonitorInfo),
    /// and repairs only what it finds missing.
    /// </summary>
    public static bool Light(this Stir stir) => stir is Stir.Front or Stir.Moved;

    /// <summary>The words the trace says a disturbance with.</summary>
    public static string Says(this Stir stir) => stir switch
    {
        Stir.Start => "started",
        Stir.Displays => "the displays changed",
        Stir.Scale => "a display's scale changed",
        Stir.Resumed => "resumed",
        Stir.Unlocked => "unlocked",
        Stir.DisplayLit => "a display lit again",
        Stir.ExplorerBack => "Explorer's taskbar was made again", // not-ui: the appbar trace
        Stir.Settings => "the settings changed",
        _ => stir.ToString(),
    };
}

/// <summary>One of the dock's windows, as the keeper sees it.</summary>
internal interface IKeptBar
{
    /// <summary>The display it is on (\\.\DISPLAYn), as the last arrange listed it.</summary>
    string Display { get; }

    /// <summary>Its appbar; null for a window that holds none (a place).</summary>
    AppbarHolder? Holder { get; }

    /// <summary>It is out of the way of a full screen app.</summary>
    bool MadeWay { get; }

    /// <summary>Hidden for a full screen app on its display, or back (laid out and shown) once it is gone.</summary>
    void MakeWay(bool on);

    /// <summary>The strip it should hold now, pushed to its holder; true when that sent something to the shell.</summary>
    bool Reserve();
}

/// <summary>Explorer's taskbar as the keeper hides it and puts it back; faked in the tests.</summary>
internal interface IExplorerTaskbar
{
    /// <summary>Explorer runs, and has a taskbar.</summary>
    bool Found { get; }

    /// <summary>Karpo has hidden it, and it is to be put back.</summary>
    bool Taken { get; }

    /// <summary>Hides it: auto-hide on, its windows hidden, the guard started. What the trace says.</summary>
    string Take();

    /// <summary>Hidden still as it should be — after an Explorer restart its new taskbar is auto-hidden and hidden again. What the trace says; null when nothing was off.</summary>
    string? Keep();

    /// <summary>Shows it: auto-hide off, its windows shown. What the trace says.</summary>
    string Give();
}

/// <summary>The windows on the screen as the full screen look reads them; faked in the tests.</summary>
internal interface IFrontWindows
{
    /// <summary>GetForegroundWindow; 0 for none.</summary>
    nint Foreground();

    /// <summary>The window as it is now; null where it is gone.</summary>
    WindowFacts? Facts(nint window);

    /// <summary>Windows says a Direct3D app runs full screen (QUNS_RUNNING_D3D_FULL_SCREEN).</summary>
    bool Direct3D();

    /// <summary>The window's program's file name, for the trace; asked only as a display turns full screen.</summary>
    string Program(nint window);

    /// <summary>
    /// §702: whether <paramref name="window"/> lies under <paramref name="over"/> in the z-order — come to the
    /// foreground, yet still behind it, as a desktop widget held at the bottom takes the keys.
    /// </summary>
    bool Under(nint window, nint over);

    /// <summary>
    /// §706: the window that owns <paramref name="window"/> at the root of its owners (GA_ROOTOWNER); 0 where it has none.
    /// A flyout, menu or popup is a top-level window of its own, owned by the window it opened from.
    /// </summary>
    nint Owner(nint window);

    /// <summary>
    /// §707: the window highest in the z-order that would be <paramref name="display"/>'s front (<see cref="FullscreenApps.Fronts"/>),
    /// leaving out the windows that never take the foreground (WS_EX_NOACTIVATE, tool windows); 0 for none.
    /// </summary>
    nint TopOn(string display);
}

/// <summary>The reconcile (see the top of the file).</summary>
internal sealed class BarKeeper
{
    /// <summary>How long after a reconcile that sent something, or a disturbance, the one look more comes (Dock's SETTLE).</summary>
    public const uint SettleMs = 1_000;

    private readonly IExplorerTaskbar explorer;
    private readonly IFrontWindows windows;
    private readonly AppbarTrace trace;
    private readonly Func<long> clock;

    // Each display's front window: the last app window that came to the front there.
    private readonly Dictionary<string, nint> fronts = new(StringComparer.OrdinalIgnoreCase);

    // The displays with a full screen app, as the last look found them.
    private HashSet<string> full = new(StringComparer.OrdinalIgnoreCase);

    // What Windows last said of Direct3D full screen: asked at every look but a move's.
    private bool direct3D;

    // The last disturbance on the trace, and when: a burst of the same says it once.
    private Stir? lastWoke;
    private long lastWokeAt;

    /// <param name="clock">Milliseconds, ever increasing: <see cref="Environment.TickCount64"/> unless a test says otherwise.</param>
    public BarKeeper(IExplorerTaskbar explorer, IFrontWindows windows, AppbarTrace trace, Func<long>? clock = null)
    {
        this.explorer = explorer;
        this.windows = windows;
        this.trace = trace;
        this.clock = clock ?? (() => Environment.TickCount64);
    }

    /// <summary>Whether <paramref name="display"/> has a full screen app in front, as the last look found it.</summary>
    public bool IsFullscreen(string display) => full.Contains(display);

    /// <summary>The displays with a full screen app, as the last look found them.</summary>
    public IReadOnlyCollection<string> Fullscreen => full;

    /// <summary>
    /// §714: whether the desktop is in front (it has the foreground) and stands over one of <paramref name="bars"/> in
    /// the z-order. Show desktop (Win+D, the taskbar's corner, a three-finger swipe) raises the desktop's window past
    /// every window, the always-on-top ones too: the bars are Karpo's windows, not Explorer's taskbar, so they went
    /// under it with the apps. A click on the wallpaper gives the desktop the foreground and raises nothing: false.
    /// </summary>
    public bool DesktopOver(IEnumerable<nint> bars)
    {
        var desktop = windows.Foreground();
        return DesktopInFront() && bars.Any(bar => windows.Under(bar, desktop));
    }

    /// <summary>§714: the desktop (Progman, a WorkerW) has the foreground.</summary>
    public bool DesktopInFront() =>
        windows.Foreground() is var foreground and not 0 && windows.Facts(foreground) is { } front && FullscreenApps.IsDesktop(front.Class);

    /// <summary>
    /// The windows whose moves, shows, hides and end can change what the look finds: the foreground
    /// window and every full screen display's front (FrontWatch hooks their threads).
    /// </summary>
    public IReadOnlyList<nint> Watched()
    {
        var watched = new List<nint>();
        var foreground = windows.Foreground();
        if (foreground != 0)
        {
            watched.Add(foreground);
        }
        foreach (var display in full)
        {
            if (fronts.TryGetValue(display, out var front) && !watched.Contains(front))
            {
                watched.Add(front);
            }
        }
        return watched;
    }

    /// <summary>
    /// Step 1 of a reconcile on its own (see the top of the file): which displays have a full screen
    /// app, and every bar hidden or shown to match. The arrange runs it before it lays anything out,
    /// so no bar is ever laid out on a display a full screen app holds. The displays whose full screen
    /// app went.
    /// </summary>
    public List<string> Look(IReadOnlyList<IKeptBar> bars, bool editing, Stir stir = Stir.Front)
    {
        var foreground = windows.Foreground();
        var front = foreground != 0 ? windows.Facts(foreground) : null;
        // Why a full screen display stops being one, for the trace.
        var why = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (front is { } came && FullscreenApps.Fronts(came) && !Behind(came, foreground))
        {
            foreach (var display in fronts.Where(entry => entry.Value == foreground).Select(entry => entry.Key).ToList())
            {
                fronts.Remove(display);
            }
            if (full.Contains(came.Display) && fronts.TryGetValue(came.Display, out var was) && was != foreground)
            {
                why[came.Display] = $"{windows.Program(foreground)} ({came.Class}) came to the front at {AppbarHolder.Show(came.Rect)}";
            }
            fronts[came.Display] = foreground;
        }
        if (stir != Stir.Moved)
        {
            direct3D = windows.Direct3D();
        }
        // §707: a display with no front yet (Karpo just started, or every window there went) has the window on top of it.
        foreach (var display in bars.Select(bar => bar.Display).Distinct(StringComparer.OrdinalIgnoreCase).ToList())
        {
            if (!fronts.ContainsKey(display) && windows.TopOn(display) is var top and not 0)
            {
                fronts[display] = top;
            }
        }
        var now = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (display, first) in fronts.ToList())
        {
            var window = first;
            var facts = window == foreground ? front : windows.Facts(window);
            if (Gone(facts, display) && windows.TopOn(display) is var next and not 0 && next != window)
            {
                // §707: the front went from the display: the window it leaves on top there is the display's front now.
                fronts[display] = next;
                (window, facts) = (next, next == foreground ? front : windows.Facts(next));
            }
            if (facts is not { } seen || !string.Equals(seen.Display, display, StringComparison.OrdinalIgnoreCase))
            {
                // Gone, or moved to another display: this one has no front until one comes.
                why.TryAdd(display, facts is null ? "its window closed" : "its window moved to another display");
                fronts.Remove(display);
                continue;
            }
            var exclusive = direct3D && window == foreground;
            if (!FullscreenApps.Fills(seen, exclusive))
            {
                if (full.Contains(display))
                {
                    why.TryAdd(display, Left(seen));
                }
                continue;
            }
            now.Add(display);
            if (!full.Contains(display))
            {
                trace.Write($"[{Label(display)}] full screen app in front: {windows.Program(window)} ({seen.Class}) at {AppbarHolder.Show(seen.Rect)} on {AppbarHolder.Show(seen.DisplayBounds)}{(exclusive ? ", Direct3D full screen" : "")}; its bars make way and are left alone");
            }
        }
        var over = full.Where(display => !now.Contains(display)).ToList();
        foreach (var display in over)
        {
            trace.Write($"[{Label(display)}] full screen app gone: its bars come back ({why.GetValueOrDefault(display, "no front")})");
        }
        full = now;
        foreach (var bar in bars)
        {
            var hide = full.Contains(bar.Display) && !editing;
            if (bar.MadeWay != hide)
            {
                bar.MakeWay(hide);
            }
        }
        return over;
    }

    /// <summary>
    /// §702 (the person, 2026-11-15: "the karpo taskbar sometimes clips infront of a fullscreen video… when i go into
    /// edit mode in themia"): whether <paramref name="came"/>, come to the foreground, still lies under its display's
    /// full screen app. Themia's widgets sit on the desktop, under every app, and take the keys in edit mode without
    /// rising: the video stays over them on the screen, and so it keeps its display.
    /// <para>
    /// §706 (the person, 2026-11-19: "in fullscreen mode when clicking themia sometimes karpo comes to front"): the desktop
    /// too. appbar.log, 2026-11-18 13:32–13:44: a video full screen in Brave on DISPLAY1, then "full screen app gone: its bars
    /// come back (explorer.exe (Progman) came to the front at -3840,0..3840,2160)" three times in eight seconds, the video
    /// back in front a second or two after each. The desktop window spans every display, so MonitorFromWindow names the
    /// primary for it wherever it was clicked: a click on the desktop of the other display took the video's display, and
    /// the bars came back over the video. Clicked, the desktop stays at the bottom of the z-order, under the video; Show
    /// desktop minimizes the video (its window left full screen) or raises the desktop over it, and either still gives the
    /// display back. And a captionless window (a flyout, a menu, a popup: a top-level window of its own) is where its owner
    /// is: one opened from a widget under the video leaves the video its display, wherever in the z-order it stands.
    /// </para>
    /// </summary>
    private bool Behind(WindowFacts came, nint window) =>
        full.Contains(came.Display)
        && fronts.TryGetValue(came.Display, out var was)
        && was != window
        && (windows.Under(window, was) || OwnedUnder(came, window, was));

    /// <summary>
    /// §707 (the person, 2026-11-19: "right now karpo is in the front again. try to fix the issue again"): whether a
    /// display's front has gone from it — closed, moved to another display, minimized, hidden or cloaked. appbar.log,
    /// 2026-11-19 14:17:51: a video full screen in Brave on DISPLAY1, then "full screen app gone: its bars come back
    /// (WhatsApp.Root.exe (WinUIDesktopWin32WindowClass) came to the front …)"; WhatsApp went again, the video stood
    /// over the whole display, yet the display kept the gone window as its front and its bars stayed over the video until a
    /// click on the video. The front a display is left with is the window on top of it then (<see cref="IFrontWindows.TopOn"/>),
    /// as it is on the screen.
    /// </summary>
    private static bool Gone(WindowFacts? facts, string display) =>
        facts is not { } seen
        || !string.Equals(seen.Display, display, StringComparison.OrdinalIgnoreCase)
        || seen.Minimized || !seen.Visible || seen.Cloaked;

    /// <summary>§706: a captionless window whose root owner lies under <paramref name="was"/>.</summary>
    private bool OwnedUnder(WindowFacts came, nint window, nint was) =>
        !came.Captioned
        && windows.Owner(window) is var owner and not 0
        && owner != window && owner != was
        && windows.Under(owner, was);

    /// <summary>
    /// The reconcile (see the top of the file). <paramref name="hideTaskbar"/>: Explorer's taskbar
    /// should be hidden; <paramref name="editing"/>: edit mode is on. True when the dock should look
    /// once more <see cref="SettleMs"/> from now.
    /// </summary>
    public bool Reconcile(IReadOnlyList<IKeptBar> bars, Stir stir, bool hideTaskbar, bool editing)
    {
        var now = clock();
        var disturbs = stir.Disturbs();
        if (disturbs && (stir != lastWoke || now - lastWokeAt > 2_000))
        {
            trace.Write($"woke: {stir.Says()}");
        }
        if (disturbs)
        {
            lastWoke = stir;
            lastWokeAt = now;
        }
        var over = Look(bars, editing, stir);
        if (stir == Stir.Moved && over.Count == 0)
        {
            return false;
        }
        if (stir == Stir.ExplorerBack)
        {
            // A new Explorer knows none of the appbars, nor the space they held: every bar registered
            // again — but a full screen app's display's only once the app is gone.
            foreach (var bar in bars)
            {
                if (bar.Holder is not { } holder)
                {
                    continue;
                }
                if (full.Contains(bar.Display))
                {
                    holder.Lost();
                }
                else
                {
                    holder.Register();
                }
            }
        }
        // A display whose full screen app went is looked after as fully as after a disturbance.
        var light = stir.Light() && over.Count == 0;
        var settle = disturbs || over.Count > 0;
        var held = new List<IKeptBar>();
        foreach (var bar in bars)
        {
            if (bar.Holder is not { } holder || full.Contains(bar.Display))
            {
                continue;
            }
            if (disturbs || over.Contains(bar.Display, StringComparer.OrdinalIgnoreCase))
            {
                holder.Refill();
            }
            held.Add(bar);
        }
        if (!light)
        {
            foreach (var bar in held)
            {
                settle |= bar.Reserve();
            }
        }
        foreach (var bar in held)
        {
            var holder = bar.Holder!;
            var (state, why) = holder.Verify();
            switch (state)
            {
                case AppbarState.Held:
                case AppbarState.Idle:
                    holder.Settled();
                    break;
                case AppbarState.Missing:
                case AppbarState.Short:
                case AppbarState.Unregistered:
                    if (holder.Waiting(now, SettleMs))
                    {
                        // Just asked (here, or by a build): given until the settle to show in the work area.
                        settle = true;
                    }
                    else if (holder.MayRepair(now))
                    {
                        holder.Repair(now, why, state);
                        settle = true;
                    }
                    else
                    {
                        holder.Spent(why);
                    }
                    break;
            }
        }
        if (!light && Taskbar(hideTaskbar))
        {
            settle = true;
            // Explorer's taskbar let go of its strip, or took it back: every strip asked for again
            // where the shell would put it now — on the edge, where it was beside the taskbar.
            foreach (var bar in held)
            {
                bar.Reserve();
            }
        }
        return settle;
    }

    /// <summary>Step 3: Explorer's taskbar hidden or put back as <paramref name="hide"/> says. True when it was changed.</summary>
    private bool Taskbar(bool hide)
    {
        if (hide)
        {
            if (!explorer.Taken)
            {
                if (!explorer.Found)
                {
                    // Not there yet: its TaskbarCreated brings it.
                    return false;
                }
                trace.Write(explorer.Take());
                return true;
            }
            if (explorer.Keep() is { } kept)
            {
                trace.Write(kept);
                return true;
            }
            return false;
        }
        if (explorer.Taken)
        {
            trace.Write(explorer.Give());
            return true;
        }
        return false;
    }

    /// <summary>What the trace says of a display's front that no longer fills it.</summary>
    private static string Left(WindowFacts window) =>
        window.Minimized ? "its window was minimized"
        : !window.Visible ? "its window was hidden"
        : window.Cloaked ? "its window was cloaked"
        : $"its window left full screen: at {AppbarHolder.Show(window.Rect)}";

    /// <summary>What the trace calls a display: its device name without the \\.\ before it.</summary>
    internal static string Label(string display) => display.TrimStart('\\', '.');
}
