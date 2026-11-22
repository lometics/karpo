// The brightness of each display, as Windows 11's quick settings has it for a built-in screen, and
// here for an external monitor as well: the person asked for "yes, including external screens".
//
// Two ways in (ScreenMachine): a built-in screen through Windows' own — WMI's WmiMonitorBrightness
// and its WmiSetBrightness method, the path Settings' own slider takes — and an external monitor
// over DDC/CI, the Monitor Configuration API's VCP code 0x10 (luminance), where the monitor answers
// it. A display neither way reaches has no brightness to offer: Windows shows no slider for it, and
// neither does Karpo.
//
// DDC/CI is slow — tens to hundreds of ms a call, over the monitor's own wire, and a rest after
// each — so none of it runs on the dock's thread. One worker thread does every read and every write.
// What it sends each display is ScreenWrites' (docs\widgets.md §495): asking only records the level;
// each display is sent one command at a time, never before its rest after the last is over, and
// always the newest level asked; once the person stops, it is read back, and a level it did not take
// is sent again, a bounded number of times, a display that stays failing said in karpo.log. What the
// read back says is what every widget shows. What the machine learns about a display — which way
// reaches it, its handles, its range — it keeps per monitor handle until the displays change
// (DisplaysChanged), when it forgets it all, and for one display whenever that one fails, so a stale
// handle is opened again on the next try.
//
// DDC/CI drops a reply now and then. A display that answered once keeps the level it last said
// until it answers again, and a write it took without ever answering the read back stands as asked:
// a slider that vanished or jumped back for a missed reply would be worse than a level a moment old.
// Only finding the displays again decides which of them have a brightness at all.
//
// A widget has one slider, never one per display (the person: "it should dimm the screens brigness
// together with one slider"). It sets every display with a brightness to its level, or, where
// somebody chose it, the display the widget is on alone (BrightnessScreens); it shows the level of
// the display it is on, or of the first that has one where its own has none (Shown). A drag on it
// asks every display it sets at once, and each display is got to its newest level on its own pace:
// a slow monitor never queues a drag's every step, never holds a fast one back for longer than its
// own command takes, and the last level always reaches every display.
//
// CHANGES MADE ELSEWHERE (docs\widgets.md §439; the person: "when i change the brightness on my
// keyboard it does not change it on the widget"). A built-in screen's level moves without Karpo all
// the time — the keyboard's brightness keys, Windows' quick settings and Settings slider, adaptive
// brightness, the power plan dimming it — and the widget used to read it only when it started and
// after its own writes. Windows says so: the power setting GUID_VIDEO_CURRENT_MONITOR_BRIGHTNESS
// carries the level the panel shows now, sent to a window that registered for it (Follow) as
// WM_POWERBROADCAST / PBT_POWERSETTINGCHANGE, on every change and once at registration. Seen on this
// PC (2026-10-20, a probe's window, the built-in panel): it came within 20 ms of WmiSetBrightness —
// the road Settings' slider takes — and of the power plan's own brightness being applied, each time
// with the new level; GUID_DEVICE_POWER_POLICY_VIDEO_BRIGHTNESS came with it, but that is the plan's
// level, which dimming and adaptive brightness leave alone. The dock hears it whatever it is doing
// and hands it here (HeardIfMade); a level the built-in screens already have is our own write coming
// back, and nothing is read. Else the worker reads the built-in screens again — WMI, a few ms, never
// a monitor's slow DDC/CI — and the widgets draw the new level. Nothing polls, and no timer wakes.
//
// An external monitor's own buttons tell nobody: DDC/CI has no event. Its level is read again when a
// widget comes into view — a bar showing it, a folder opening — and when the pointer comes onto one,
// at most every LookEveryMs (Look), so a pointer passing over the bar is not a stream of DDC/CI reads.

using Karpo.Core;
using TerraFX.Interop.Windows;

namespace Karpo.Dock.QuickSettings.Services;

/// <summary>One display that has a brightness to set, as last read.</summary>
/// <param name="Monitor">The display's HMONITOR.</param>
/// <param name="Device">Its GDI name, "\\.\DISPLAY1": what a level is asked for by.</param>
/// <param name="Name">What the person calls it: the monitor's own name, or "Built-in display".</param>
/// <param name="BuiltIn">A laptop's own screen, rather than a monitor plugged into it.</param>
/// <param name="Level">Its brightness, 0 to 100, as the display last said.</param>
internal sealed record ScreenLevel(nint Monitor, string Device, string Name, bool BuiltIn, int Level);

/// <summary>A display as the machine found it, before anybody asked it for a level.</summary>
internal sealed record ScreenFound(nint Monitor, string Device, string Name, bool BuiltIn)
{
    /// <summary>The monitor's device path, which a built-in panel's WMI instance is matched by; empty where unknown.</summary>
    public string Path { get; init; } = "";
}

/// <summary>
/// What reads and sets the displays' brightness: the PC's own (<see cref="ScreenMachine"/>), or a
/// test's stand-in. Called on the brightness worker only, one call at a time.
/// </summary>
internal interface IScreenMachine
{
    /// <summary>The displays there are now, each with a way to its brightness or without one.</summary>
    IReadOnlyList<ScreenFound> Displays();

    /// <summary>Its brightness, 0 to 100; null where no way reaches it.</summary>
    int? Read(ScreenFound screen);

    /// <summary>Asks for a brightness, 0 to 100; false where it was not taken.</summary>
    bool Write(ScreenFound screen, int level);

    /// <summary>The displays changed: what was learnt about them — handles, ranges, ways in — is stale.</summary>
    void Forget();

    /// <summary>
    /// <paramref name="screen"/> failed a write or a read back: what was learnt about it alone is
    /// stale, and its way in is found again on the next call (a monitor's handle opened afresh).
    /// </summary>
    void Forget(ScreenFound screen);

    /// <summary>The least time, in ms, <paramref name="screen"/> needs after a command before the next: a DDC/CI monitor's rest.</summary>
    int Gap(ScreenFound screen);

    /// <summary>
    /// What a read back of <paramref name="screen"/> may say once it took <paramref name="level"/>:
    /// the level itself, or where the display has steps — a panel's own levels, a monitor's luminance
    /// range — the steps around it.
    /// </summary>
    (int Low, int High) ShowsFor(ScreenFound screen, int level);
}

/// <summary>
/// The displays' brightness for every widget: the levels as last read, and the one worker that reads
/// and writes them off the dock's thread (see the file's header).
/// </summary>
internal sealed class Brightness : IDisposable
{
    /// <summary>
    /// Tests only, on the thread that makes a widget: a stand-in for the PC's displays. A widget made
    /// with one set gets a brightness of its own over it, and nothing it does reaches a real screen.
    /// </summary>
    [ThreadStatic]
    internal static IScreenMachine? Stand;

    /// <summary>Tests only, beside <see cref="Stand"/>: the clock a widget's own brightness times its looks by (<see cref="Look"/>).</summary>
    [ThreadStatic]
    internal static TimeProvider? StandClock;

    private static readonly object SharedGate = new();
    private static Brightness? shared;
    private static Brightness? samples;

    private readonly IScreenMachine machine;
    private readonly object gate = new();
    private readonly SemaphoreSlim work = new(0);
    private readonly Thread worker;
    private readonly ScreenWrites writes;
    private readonly Action<string> report;
    private IReadOnlyList<ScreenLevel> screens = [];
    private IReadOnlyList<ScreenFound> found = [];
    private bool rescan = true;
    private bool forget;
    private bool read;
    private bool panels;
    private bool stopped;

    /// <summary>The shortest time between two looks at every display (<see cref="Look"/>), in ms.</summary>
    internal const int LookEveryMs = 3000;

    private readonly TimeProvider clock;

    // When every display was last read for a look (Look), in the clock's ms: as it is made, which
    // finds them all, so the widgets coming into view at the dock's start ask nothing more.
    private long looked;

    /// <param name="machine">What reads and sets the displays.</param>
    /// <param name="ready">
    /// Finds the displays and reads their levels before this returns, on the calling thread: for a
    /// stand-in, whose answers take no time, so a widget made over one draws its displays from its
    /// first paint. The PC's own are always found on the worker.
    /// </param>
    /// <param name="clock">What <see cref="Look"/> times its looks by: the PC's, or a test's.</param>
    /// <param name="pace">The writes' timings (<see cref="ScreenWrites"/>); the default for every display.</param>
    /// <param name="report">Where a display that stays failing is said: karpo.log for the PC's; the console for a stand-in.</param>
    internal Brightness(IScreenMachine machine, bool ready = false, TimeProvider? clock = null, ScreenPace? pace = null, Action<string>? report = null)
    {
        this.machine = machine;
        this.clock = clock ?? TimeProvider.System;
        writes = new ScreenWrites(pace ?? ScreenPace.Default);
        this.report = report ?? (line => Console.Error.WriteLine("Karpo: " + line)); // not-ui: the console
        looked = Now;
        if (ready)
        {
            rescan = false;
            _ = Step(forgetting: false, scanning: true, reading: true, panelsOnly: false);
        }
        worker = new Thread(Work) { IsBackground = true, Name = "Karpo brightness" }; // not-ui: a thread's name
        worker.Start();
        Wake();
    }

    /// <summary>The PC's own, made the first time a widget asks and kept for as long as Karpo runs.</summary>
    internal static Brightness Shared
    {
        get
        {
            lock (SharedGate)
            {
                return shared ??= new Brightness(new ScreenMachine(), report: Troubles.Write);
            }
        }
    }

    /// <summary>
    /// A made-up PC's displays, for every run that may not reach the person's (a test, a smoke pass,
    /// KARPO_QUICK_SAMPLES): two monitors with a brightness and one without.
    /// </summary>
    internal static Brightness Samples
    {
        get
        {
            lock (SharedGate)
            {
                return samples ??= new Brightness(ready: true, machine: new SampleScreens()
                    .Add(-1, @"\\.\SAMPLE1", "Sample monitor", 70) // not-ui: a sample display's name
                    .Add(-2, @"\\.\SAMPLE2", "Second sample monitor", 45) // not-ui: a sample display's name
                    .Add(-3, @"\\.\SAMPLE3", "Sample television", null)); // not-ui: a sample display's name
            }
        }
    }

    /// <summary>
    /// The displays changed (WM_DISPLAYCHANGE): the PC's brightness, where anything has made it,
    /// forgets what it learnt about them and finds them again. Nothing is made for it.
    /// </summary>
    internal static void DisplaysChangedIfMade()
    {
        Brightness? made;
        lock (SharedGate)
        {
            made = shared;
        }
        made?.DisplaysChanged();
    }

    /// <summary>
    /// The level a power setting notification (a WM_POWERBROADCAST's lParam) says the built-in
    /// screen shows now; null for any other setting, or data too short to hold one. Pure.
    /// </summary>
    public static unsafe int? Read(POWERBROADCAST_SETTING* setting)
    {
        if (setting == null || setting->PowerSetting != GUID.GUID_VIDEO_CURRENT_MONITOR_BRIGHTNESS || setting->DataLength < sizeof(uint))
        {
            return null;
        }
        return (int)Math.Min(*(uint*)&setting->Data, 100);
    }

    /// <summary>
    /// A power setting notification the dock's window heard: where it is the built-in screen's
    /// brightness, the PC's brightness — where anything has made it — follows it (<see cref="Heard"/>),
    /// and this says it was brightness's. Nothing is made for it.
    /// </summary>
    public static unsafe bool HeardIfMade(POWERBROADCAST_SETTING* setting)
    {
        if (Read(setting) is not { } level)
        {
            return false;
        }
        Brightness? made;
        lock (SharedGate)
        {
            made = shared;
        }
        made?.PanelSaid(level);
        return true;
    }

    /// <summary>
    /// <paramref name="window"/> is told of the built-in screen's brightness from now on (and once
    /// now); null where this Windows has no such setting to tell.
    /// </summary>
    public static nint? Follow(HWND window) => EnergySaver.Register(window, GUID.GUID_VIDEO_CURRENT_MONITOR_BRIGHTNESS);

    /// <summary>A power setting notification heard: where it is the built-in screen's brightness, it is followed (<see cref="PanelSaid"/>).</summary>
    public unsafe bool Heard(POWERBROADCAST_SETTING* setting)
    {
        if (Read(setting) is not { } level)
        {
            return false;
        }
        PanelSaid(level);
        return true;
    }

    /// <summary>
    /// Windows said the built-in screen is at <paramref name="level"/> now — the keyboard's keys,
    /// its own slider, adaptive brightness. Where every built-in screen is there already and none
    /// is being written, it is a write of ours coming back and nothing is read; else the worker
    /// reads the built-in screens again (see the header), and the widgets hear what they say.
    /// </summary>
    public void PanelSaid(int level)
    {
        var builtIn = Screens.Where(screen => screen.BuiltIn).ToList();
        lock (gate)
        {
            if (builtIn.Count > 0 && builtIn.All(screen => screen.Level == level && !writes.Busy(screen.Device)))
            {
                return;
            }
            panels = true;
        }
        Wake();
    }

    /// <summary>
    /// Reads every level again, unless it did within <see cref="LookEveryMs"/>: a widget coming into
    /// view, or the pointer coming onto one — what is left for a monitor's own buttons, which tell
    /// nobody (see the header).
    /// </summary>
    public void Look()
    {
        var now = Now;
        var last = Interlocked.Read(ref looked);
        if (now - last < LookEveryMs || Interlocked.CompareExchange(ref looked, now, last) != last)
        {
            return;
        }
        Refresh();
    }

    /// <summary>The clock's time in ms, for <see cref="Look"/>.</summary>
    private long Now => clock.GetUtcNow().ToUnixTimeMilliseconds();

    /// <summary>
    /// What a widget made on this thread reads and sets: a test's stand-in; the samples on a run that
    /// may not reach the person's world (Config.IsIsolated, KARPO_QUICK_SAMPLES); else the PC's.
    /// </summary>
    internal static Brightness ForWidget() =>
        Stand is { } stand ? new Brightness(stand, ready: true, StandClock)
        : Karpo.Core.Config.IsIsolated || QuickSources.UseSamples ? Samples
        : Shared;

    /// <summary>Raised on the worker whenever a level or the list of displays changed.</summary>
    public event Action? Changed;

    /// <summary>Every display with a brightness to set, as last read; empty until the first read, and on a PC with none.</summary>
    public IReadOnlyList<ScreenLevel> Screens => Volatile.Read(ref screens);

    /// <summary>
    /// The display <paramref name="monitor"/> names, where it has a brightness; for none (0) the
    /// first there is, which is the primary one where it has one.
    /// </summary>
    public ScreenLevel? On(nint monitor)
    {
        var all = Screens;
        foreach (var screen in all)
        {
            if (screen.Monitor == monitor)
            {
                return screen;
            }
        }
        return monitor == 0 && all.Count > 0 ? all[0] : null;
    }

    /// <summary>
    /// The display one slider shows, for a widget on <paramref name="monitor"/> that sets
    /// <paramref name="screens"/>: its own where it has a brightness. Setting every display, a widget
    /// on one that has none shows the first that has; setting its own alone, it shows none.
    /// </summary>
    public ScreenLevel? Shown(nint monitor, BrightnessScreens screens)
    {
        if (On(monitor) is { } own)
        {
            return own;
        }
        var all = Screens;
        return screens == BrightnessScreens.All && all.Count > 0 ? all[0] : null;
    }

    /// <summary>
    /// The displays one slider sets, for a widget on <paramref name="monitor"/>: every display with
    /// a brightness, or its own alone where it has one. A display no way reaches is never among them.
    /// </summary>
    public IReadOnlyList<ScreenLevel> Dimmed(nint monitor, BrightnessScreens screens) =>
        screens == BrightnessScreens.All ? Screens
        : On(monitor) is { } own ? [own]
        : [];

    /// <summary>
    /// What one slider shows (<see cref="Shown"/>): the level being written while it is, else what
    /// the display took; null where it shows none.
    /// </summary>
    public int? LevelOf(nint monitor, BrightnessScreens screens) =>
        Shown(monitor, screens) is { } shown ? Asked(shown.Device) ?? shown.Level : null;

    /// <summary>
    /// Asks for <paramref name="level"/> on every display one slider sets (<see cref="Dimmed"/>), all
    /// at once: each display is got to its newest on its own pace (<see cref="ScreenWrites"/>), as a
    /// drag on one display's slider is. False where it sets none.
    /// </summary>
    public bool Set(nint monitor, BrightnessScreens screens, int level)
    {
        var dimmed = Dimmed(monitor, screens);
        if (dimmed.Count == 0)
        {
            return false;
        }
        lock (gate)
        {
            var now = Tick;
            foreach (var screen in dimmed)
            {
                writes.Want(screen.Device, level, now);
            }
        }
        Wake();
        return true;
    }

    /// <summary>
    /// Asks for <paramref name="level"/> on the display <paramref name="device"/> names. Returns at
    /// once; the worker sends it, unless a newer level for the same display came before it could.
    /// </summary>
    public void Set(string device, int level)
    {
        lock (gate)
        {
            writes.Want(device, level, Tick);
        }
        Wake();
    }

    /// <summary>
    /// The level asked for <paramref name="device"/>, while the display is being got there — written,
    /// and read back to confirm it; null once it said what it took.
    /// </summary>
    public int? Asked(string device)
    {
        lock (gate)
        {
            return writes.Wanted(device);
        }
    }

    /// <summary>The worker's time, in ms: what the writes' rests and waits are timed by.</summary>
    private static long Tick => Environment.TickCount64;

    /// <summary>Reads every level again: a flyout opening, or a monitor's own buttons may have moved one.</summary>
    public void Refresh()
    {
        lock (gate)
        {
            read = true;
        }
        Wake();
    }

    /// <summary>The displays changed (WM_DISPLAYCHANGE): forget what was learnt about them and find them again.</summary>
    public void DisplaysChanged()
    {
        lock (gate)
        {
            (forget, rescan) = (true, true);
        }
        Wake();
    }

    /// <summary>
    /// Wakes the worker for a round, unless one is already waiting to start: that round reads what
    /// was asked after this, so a drag's hundred moves are a handful of rounds, not a hundred.
    /// </summary>
    private void Wake()
    {
        if (work.CurrentCount == 0)
        {
            _ = work.Release();
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            stopped = true;
        }
        Wake();
    }

    private void Work()
    {
        var wake = long.MaxValue;
        while (true)
        {
            // Until something is asked, or the next errand's time: a display's rest over, its
            // quiet before the read back, a wait before it is sent again.
            var wait = wake == long.MaxValue ? Timeout.Infinite : (int)Math.Clamp(wake - Tick, 0, int.MaxValue);
            if (wait != 0)
            {
                _ = work.Wait(wait);
            }
            bool forgetting, scanning, reading, panelsOnly;
            lock (gate)
            {
                if (stopped)
                {
                    return;
                }
                (forgetting, scanning, reading, panelsOnly) = (forget, rescan, read, panels);
                (forget, rescan, read, panels) = (false, false, false, false);
            }
            try
            {
                wake = Step(forgetting, scanning, reading, panelsOnly);
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                Console.Error.WriteLine($"Karpo: brightness not read or set: {error.GetType().Name} 0x{error.HResult:X8}");
                // What was asked is tried again a moment later, nothing left on its way.
                lock (gate)
                {
                    writes.Abandon();
                }
                wake = Tick + writes.Pace.RetryMs;
            }
        }
    }

    /// <summary>
    /// One round of the worker's: what changed, the displays found again, then every errand due
    /// (<see cref="Errands"/>). <paramref name="panelsOnly"/>: Windows said a built-in screen moved,
    /// and those are read again — every display, on a PC where none was found built in. A display
    /// being got to a level asked is not read for a look: its own read back is what it shows. Says
    /// when the next errand is due; long.MaxValue for none.
    /// </summary>
    private long Step(bool forgetting, bool scanning, bool reading, bool panelsOnly)
    {
        if (forgetting)
        {
            machine.Forget();
        }
        var changed = false;
        var levels = Screens.ToDictionary(screen => screen.Device, screen => screen.Level, StringComparer.OrdinalIgnoreCase);
        if (scanning)
        {
            found = machine.Displays();
            levels.Clear();
            reading = true;
            changed = true;
            lock (gate)
            {
                writes.Keep(found.Select(screen => screen.Device));
            }
        }
        if (reading)
        {
            foreach (var screen in found)
            {
                // Found afresh, a display that does not answer has no brightness; found before, it
                // keeps what it last said (see the header).
                if ((scanning || !Busy(screen)) && Paced(screen) is { } level)
                {
                    levels[screen.Device] = level;
                }
            }
            changed = true;
        }
        else if (panelsOnly)
        {
            var builtIn = found.Any(screen => screen.BuiltIn);
            foreach (var screen in found)
            {
                // Redrawn only where a level moved: a burst of the keys' notices is a burst of reads,
                // not of paints.
                if ((!builtIn || screen.BuiltIn) && !Busy(screen) && Paced(screen) is { } level && (!levels.TryGetValue(screen.Device, out var had) || had != level))
                {
                    levels[screen.Device] = level;
                    changed = true;
                }
            }
        }
        if (changed)
        {
            Publish(levels);
            Changed?.Invoke();
        }
        return Errands(levels);
    }

    /// <summary>
    /// Every errand due now (<see cref="ScreenWrites"/>), one after another: a level written, or read
    /// back to confirm it. Stops where none is due yet, or something else was asked of the worker,
    /// and says when the next is due.
    /// </summary>
    private long Errands(Dictionary<string, int> levels)
    {
        while (true)
        {
            Errand? errand;
            long wake;
            lock (gate)
            {
                if (stopped || forget || rescan || read || panels)
                {
                    return Tick;
                }
                errand = writes.Next(Tick, out wake);
            }
            if (errand is null)
            {
                return wake;
            }
            var screen = found.FirstOrDefault(each => string.Equals(each.Device, errand.Device, StringComparison.OrdinalIgnoreCase));
            if (screen is null || !levels.ContainsKey(errand.Device))
            {
                // No way reaches it: nothing to send.
                lock (gate)
                {
                    writes.Drop(errand.Device);
                }
                continue;
            }
            var landed = errand.Kind == ErrandKind.Write ? Write(screen, errand) : Verify(screen, errand);
            if (landed is null)
            {
                continue;
            }
            if (landed.Level is { } level)
            {
                levels[errand.Device] = level;
            }
            if (landed.Trouble is { } trouble)
            {
                report(trouble);
            }
            // Published as the level asked stops being, so a wheel turned next steps from where the
            // display is.
            Publish(levels);
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// The level written. A refusal may be a stale handle — a monitor that slept, a dock that dropped
    /// it — so the next goes through one opened afresh; its rest is taken before it is forgotten.
    /// </summary>
    private Landed? Write(ScreenFound screen, Errand errand)
    {
        var took = Safely(() => machine.Write(screen, errand.Level));
        var gap = machine.Gap(screen);
        if (!took)
        {
            machine.Forget(screen);
        }
        lock (gate)
        {
            return writes.Wrote(errand, took, Tick, gap);
        }
    }

    /// <summary>The level read back; no answer may be a stale handle too.</summary>
    private Landed? Verify(ScreenFound screen, Errand errand)
    {
        var said = Safely(() => machine.Read(screen));
        var gap = machine.Gap(screen);
        var shows = machine.ShowsFor(screen, errand.Level);
        if (said is null)
        {
            machine.Forget(screen);
        }
        lock (gate)
        {
            return writes.Verified(errand, said, Tick, gap, shows);
        }
    }

    /// <summary>Whether <paramref name="screen"/> is being got to a level asked.</summary>
    private bool Busy(ScreenFound screen)
    {
        lock (gate)
        {
            return writes.Busy(screen.Device);
        }
    }

    /// <summary><paramref name="screen"/>'s level read for a look: not before its rest after the last command is over.</summary>
    private int? Paced(ScreenFound screen)
    {
        long at;
        lock (gate)
        {
            at = writes.FreeAt(screen.Device);
        }
        var now = Tick;
        if (at > now)
        {
            Thread.Sleep((int)Math.Min(at - now, 1000));
        }
        var level = machine.Read(screen);
        var gap = machine.Gap(screen);
        lock (gate)
        {
            writes.Spoke(screen.Device, Tick, gap);
        }
        return level;
    }

    /// <summary>A display's call, where what it throws is a failure like any other: never an errand left on its way.</summary>
    private static T? Safely<T>(Func<T> call)
    {
        try
        {
            return call();
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Console.Error.WriteLine($"Karpo: brightness not read or set: {error.GetType().Name} 0x{error.HResult:X8}");
            return default;
        }
    }

    /// <summary>The displays found that have a level, at <paramref name="levels"/>: what every widget reads next.</summary>
    private void Publish(Dictionary<string, int> levels) =>
        Volatile.Write(ref screens, [.. found.Where(screen => levels.ContainsKey(screen.Device)).Select(screen => new ScreenLevel(screen.Monitor, screen.Device, screen.Name, screen.BuiltIn, levels[screen.Device]))]);
}

/// <summary>
/// A make-believe set of displays for the tests (<see cref="Brightness.Stand"/>): each display's level
/// kept in memory, what was asked of it counted, and a display's own steps and refusals played. Nothing
/// here reaches a real screen.
/// </summary>
internal sealed class SampleScreens : IScreenMachine
{
    private readonly object gate = new();
    private readonly List<(ScreenFound Screen, int? Level)> displays = [];

    /// <summary>A display; <paramref name="level"/> null for one no way reaches.</summary>
    public SampleScreens Add(nint monitor, string device, string name, int? level, bool builtIn = false)
    {
        lock (gate)
        {
            displays.Add((new ScreenFound(monitor, device, name, builtIn), level));
        }
        return this;
    }

    private readonly List<(string Device, int Level)> writes = [];

    /// <summary>Every level taken, in order: display and level, as the tests read it while the worker writes.</summary>
    public IReadOnlyList<(string Device, int Level)> Writes
    {
        get
        {
            lock (gate)
            {
                return [.. writes];
            }
        }
    }

    /// <summary>How many times the displays were forgotten.</summary>
    public int Forgotten { get; private set; }

    /// <summary>How many times they were listed.</summary>
    public int Listed { get; private set; }

    /// <summary>How many levels were read, answered or not.</summary>
    public int Reads => Volatile.Read(ref reads);

    private int reads;

    private readonly HashSet<string> unanswered = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>From now, <paramref name="device"/>'s replies are missed (or answered again), as DDC/CI drops one now and then; it still takes a level.</summary>
    public void Miss(string device, bool missed = true)
    {
        lock (gate)
        {
            _ = missed ? unanswered.Add(device) : unanswered.Remove(device);
        }
    }

    /// <summary>The steps a display takes a level in, as a monitor may: 1 for every level.</summary>
    public int Step { get; set; } = 1;

    /// <summary>Held while set: a write waits for it, as a slow monitor keeps the worker.</summary>
    public ManualResetEventSlim Open { get; } = new(true);

    /// <summary>How many writes have begun, taken or not: one waiting on <see cref="Open"/> counts.</summary>
    public int Started => Volatile.Read(ref started);

    private int started;

    /// <summary>
    /// <paramref name="device"/> moved to <paramref name="level"/> without being asked here: the
    /// keyboard's keys, Windows' own slider, a monitor's buttons. Counted as no write.
    /// </summary>
    public void Turn(string device, int level)
    {
        lock (gate)
        {
            var at = displays.FindIndex(each => each.Screen.Device == device);
            displays[at] = (displays[at].Screen, level);
        }
    }

    /// <summary>What a display is at now, as the tests read it.</summary>
    public int? LevelOf(string device)
    {
        lock (gate)
        {
            return displays.FirstOrDefault(each => each.Screen.Device == device).Level;
        }
    }

    public IReadOnlyList<ScreenFound> Displays()
    {
        lock (gate)
        {
            Listed++;
            return [.. displays.Select(each => each.Screen)];
        }
    }

    public int? Read(ScreenFound screen)
    {
        _ = Interlocked.Increment(ref reads);
        lock (gate)
        {
            return unanswered.Contains(screen.Device) ? null : displays.FirstOrDefault(each => each.Screen.Device == screen.Device).Level;
        }
    }

    private readonly Dictionary<string, int> dropping = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> refusing = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// <paramref name="device"/>'s next <paramref name="writes"/> writes are said taken and never
    /// applied, as a monitor sent a command too soon drops it without a word.
    /// </summary>
    public void Drop(string device, int writes)
    {
        lock (gate)
        {
            dropping[device] = writes;
        }
    }

    /// <summary>From now, <paramref name="device"/> refuses every write (or takes them again).</summary>
    public void Refuse(string device, bool refused = true)
    {
        lock (gate)
        {
            _ = refused ? refusing.Add(device) : refusing.Remove(device);
        }
    }

    /// <summary>Every display's rest after a command, as <see cref="IScreenMachine.Gap"/> says it: none by default.</summary>
    public int GapMs { get; set; }

    public bool Write(ScreenFound screen, int level)
    {
        _ = Interlocked.Increment(ref started);
        Open.Wait();
        lock (gate)
        {
            var at = displays.FindIndex(each => each.Screen.Device == screen.Device);
            if (at < 0 || displays[at].Level is null || refusing.Contains(screen.Device))
            {
                return false;
            }
            writes.Add((screen.Device, level));
            if (dropping.TryGetValue(screen.Device, out var left) && left > 0)
            {
                dropping[screen.Device] = left - 1;
                return true;
            }
            displays[at] = (displays[at].Screen, Stepped(level));
            return true;
        }
    }

    public void Forget()
    {
        lock (gate)
        {
            Forgotten++;
        }
    }

    /// <summary>How many times one display was forgotten after it failed.</summary>
    public int ForgottenOne => Volatile.Read(ref forgottenOne);

    private int forgottenOne;

    public void Forget(ScreenFound screen) => Interlocked.Increment(ref forgottenOne);

    public int Gap(ScreenFound screen) => GapMs;

    public (int Low, int High) ShowsFor(ScreenFound screen, int level) => (Stepped(level), Stepped(level));

    private int Stepped(int level) => (int)Math.Round((double)level / Step, MidpointRounding.AwayFromZero) * Step;
}
