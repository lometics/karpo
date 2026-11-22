// start-placement (docs/widgets.md §666, docs/resilience.md §3): Windows' real Start, opened on the real desktop the way
// the person opens it, measured against Karpo's bar and button and against the one function Karpo places it by
// (Karpo.Dock\StartPlacement.cs, linked in). Opt-in: never part of "run all", for it opens Start on the person's screen.
//
// Each trial: Start opened through Karpo's own Start button (UI Automation's Invoke on the bar's "Start" element — the
// path Narrator takes, the button's own click; no pointer moved, no key sent), or with --keys by the Windows key and by
// Ctrl+Esc (real keystrokes, only with that flag); Start's host and search box watched every 15 ms until they stand still
// for 700 ms; Start's rectangle (the box less Windows' gap) compared with where StartPlacement says it belongs, with the
// bar, the button, the display and the gap; UI Automation's own rectangle of Start's panel read beside it; a picture of
// the place saved; Start closed the way it was opened. Per display with a bar holding a Start button, then quick
// reopenings. Nothing is set, moved or written but the pictures and the report.

using System.Diagnostics;
using System.Runtime.InteropServices;
using Karpo.Dock;
using Microsoft.Win32;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using CoreEdge = Karpo.Core.Edge;

namespace Karpo.Resilience;

internal static unsafe class StartScenario
{
    /// <summary>--keys: also open Start by the Windows key and by Ctrl+Esc (real keystrokes).</summary>
    public static bool WithKeys { get; set; }

    /// <summary>How far Start may stand from where it belongs, in pixels (XAML's own rounding).</summary>
    private const int Slack = 1;

    public static void Run(Run run)
    {
        if (run.Baseline.Dock is null)
        {
            throw new SkipException("Karpo is not running");
        }
        if (StartWindows() is { Host: not null })
        {
            throw new SkipException("Start is open already: close it first");
        }
        _ = CoInitializeEx(null, (uint)COINIT.COINIT_MULTITHREADED);
        var automation = Automation.Make();
        var buttons = new List<StartBar>();
        try
        {
            buttons = StartButtons(automation, Probe.Take(ask: false));
            if (buttons.Count == 0)
            {
                throw new SkipException("no bar of Karpo's has a Start button");
            }
            var centred = Advanced("TaskbarAl") is not 0;
            run.Say($"Windows' alignment: {(centred ? "centred (TaskbarAl 1)" : "left (TaskbarAl 0)")}; {buttons.Count} bar(s) with a Start button: "
                + string.Join("; ", buttons.Select(b => $"{b.Display} {b.Edge} bar {Show(b.Bar)}, button {Show(b.Button)}")));
            var trial = 0;
            foreach (var button in buttons)
            {
                Trial(run, ++trial, $"Karpo's Start button on {button.Display}", button, centred, open: () => button.Invoke(), close: () => button.Invoke());
            }
            // Opened again and again quickly: each opening placed.
            var first = buttons[0];
            for (var again = 0; again < 3; again++)
            {
                Trial(run, ++trial, $"Karpo's Start button on {first.Display}, quick reopening {again + 1}", first, centred, open: () => first.Invoke(), close: () => first.Invoke(), pause: 150);
            }
            if (WithKeys)
            {
                // The display Start opens on is Windows' choice; the button Karpo hangs it from is that display's first bar's.
                Trial(run, ++trial, "the Windows key", null, centred, open: () => Keys(VK.VK_LWIN), close: () => Keys(VK.VK_LWIN), buttons: buttons);
                Trial(run, ++trial, "Ctrl+Esc", null, centred, open: () => Keys(VK.VK_LCONTROL, VK.VK_ESCAPE), close: () => Keys(VK.VK_LCONTROL, VK.VK_ESCAPE), buttons: buttons);
            }
        }
        finally
        {
            foreach (var button in buttons)
            {
                button.Release();
            }
            Automation.Release(automation);
            if (StartWindows() is { Host: not null })
            {
                run.Fail("Start was left open");
            }
        }
    }

    /// <summary>One opening: open, watch until still, measure, picture, close.</summary>
    private static void Trial(Run run, int number, string how, StartBar? bar, bool centred, Action open, Action close, int pause = 1000, List<StartBar>? buttons = null)
    {
        run.Say($"[{number}] {how}");
        var watch = Stopwatch.StartNew();
        open();
        var seen = new List<(long Ms, Box Host, Box? Box)>();
        (Box Host, Box? Box)? still = null;
        long stillSince = 0;
        while (watch.ElapsedMilliseconds < 5000)
        {
            if (StartWindows() is { Host: { } host } now)
            {
                var (h, b) = (Box.Of(host.Rect), now.Box is { } box ? Box.Of(box.Rect) : (Box?)null);
                if (seen.Count == 0 || seen[^1].Host != h || seen[^1].Box != b)
                {
                    seen.Add((watch.ElapsedMilliseconds, h, b));
                    stillSince = watch.ElapsedMilliseconds;
                }
                else if (watch.ElapsedMilliseconds - stillSince >= 700)
                {
                    still = (h, b);
                    break;
                }
            }
            Thread.Sleep(15);
        }
        try
        {
            if (seen.Count == 0)
            {
                run.Fail($"[{number}] Start did not open within 5 s");
                return;
            }
            run.Say($"[{number}] host and box as heard: " + string.Join(" → ", seen.Select(s => $"+{s.Ms} ms host {s.Host.Left},{s.Host.Top} box {(s.Box is { } b ? $"{b.Left},{b.Top}" : "none")}")));
            if (still is not { } settled)
            {
                run.Fail($"[{number}] Start never stood still for 700 ms within 5 s");
                return;
            }
            Measure(run, number, settled.Host, settled.Box, bar, centred, buttons, seen.Count);
        }
        finally
        {
            close();
            var closing = Stopwatch.StartNew();
            while (StartWindows() is { Host: not null } && closing.ElapsedMilliseconds < 3000)
            {
                Thread.Sleep(30);
            }
            if (StartWindows() is { Host: not null })
            {
                run.Fail($"[{number}] Start did not close within 3 s of {how} again");
            }
            Thread.Sleep(pause);
        }
    }

    private static void Measure(Run run, int number, Box hostBox, Box? boxBox, StartBar? pressed, bool centred, List<StartBar>? buttons, int steps)
    {
        var displays = Probe.Displays();
        var host = Rect(hostBox);
        var display = displays.FirstOrDefault(d => d.Device == Probe.DisplayHolding(hostBox, displays));
        if (display is null)
        {
            run.Fail($"[{number}] Start's host {hostBox} is on no display");
            return;
        }
        var bar = pressed ?? buttons?.FirstOrDefault(b => b.Display == display.Device);
        var all = displays.Select(Facts).ToList();
        var scene = new StartScene(Facts(display), bar is null ? null : new StartButton(Rect(bar.Button), Rect(bar.Bar), bar.Edge), centred, all);
        var gap = scene.Gap;
        if (boxBox is not { } boxed || StartPlacement.SizeOfBox(Rect(boxed), gap) is not { } size)
        {
            run.Fail($"[{number}] no search box over Start: its size can't be read");
            return;
        }
        var start = new Box(boxed.Left + gap, boxed.Top + gap, boxed.Right - gap, boxed.Bottom - gap);
        var inHost = Box.Of(StartPlacement.InHost(host, scene, size));
        var placed = StartPlacement.Host(host, scene, size) is { } to ? Moved(host, to.X, to.Y) : host;
        var expected = Box.Of(StartPlacement.InHost(placed, scene, size));
        run.Say($"[{number}] on {display.Device} ({display.Dpi} dpi, gap {gap} px): Start {start}, where it belongs {expected}; "
            + $"delta left {start.Left - expected.Left}, top {start.Top - expected.Top}, right {start.Right - expected.Right}, bottom {start.Bottom - expected.Bottom}; "
            + $"{steps} place(s) heard before it stood still");
        if (Far(start, expected))
        {
            run.Fail($"[{number}] Start stands {start}, not where it belongs {expected}");
        }
        if (Far(start, inHost))
        {
            run.Fail($"[{number}] Start's box {boxed} is not round Start as Windows lays it out in its host {hostBox} ({inHost}): §666's model of Start's windows does not hold here");
        }
        if (!display.Bounds.Contains(start))
        {
            run.Fail($"[{number}] Start {start} reaches off its display {display.Bounds}");
        }
        if (bar is not null)
        {
            if (bar.Display != display.Device)
            {
                run.Fail($"[{number}] Start opened on {display.Device}, the button pressed is on {bar.Display}");
            }
            if (start.Intersects(bar.Bar))
            {
                run.Fail($"[{number}] Start {start} overlaps the bar {bar.Bar}");
            }
            var across = bar.Edge switch
            {
                CoreEdge.Top => start.Top - bar.Bar.Bottom,
                CoreEdge.Left => start.Left - bar.Bar.Right,
                CoreEdge.Right => bar.Bar.Left - start.Right,
                _ => bar.Bar.Top - start.Bottom,
            };
            run.Say($"[{number}] {across} px between Start and the {bar.Edge} bar (Windows' gap {gap})");
        }
        if (UiaPanel(host) is { } panel)
        {
            var seenBy = Box.Of(panel);
            run.Say($"[{number}] UI Automation's Start panel {seenBy}: delta left {seenBy.Left - expected.Left}, top {seenBy.Top - expected.Top}, right {seenBy.Right - expected.Right}, bottom {seenBy.Bottom - expected.Bottom} (read beside, not judged)");
        }
        var picture = Picture(Union(Union(start, bar?.Bar ?? start), new Box(boxed.Left, boxed.Top, boxed.Right, boxed.Bottom)), display.Bounds, number);
        if (picture is not null)
        {
            run.Say($"[{number}] picture: {picture}");
        }
    }

    private static bool Far(Box one, Box other) =>
        Math.Abs(one.Left - other.Left) > Slack || Math.Abs(one.Top - other.Top) > Slack
        || Math.Abs(one.Right - other.Right) > Slack || Math.Abs(one.Bottom - other.Bottom) > Slack;

    private static StartDisplay Facts(DisplayFacts display) => new(Rect(display.Bounds), Rect(display.Work), display.Dpi);

    private static RECT Rect(Box box) => new() { left = box.Left, top = box.Top, right = box.Right, bottom = box.Bottom };

    private static RECT Moved(RECT rect, int x, int y) => new() { left = x, top = y, right = x + rect.right - rect.left, bottom = y + rect.bottom - rect.top };

    private static Box Union(Box one, Box other) =>
        new(Math.Min(one.Left, other.Left), Math.Min(one.Top, other.Top), Math.Max(one.Right, other.Right), Math.Max(one.Bottom, other.Bottom));

    private static string Show(Box box) => $"{box.Left},{box.Top}..{box.Right},{box.Bottom}";

    private static int? Advanced(string name) =>
        Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", name, null) is int value ? value : null;

    // ---- Start's windows ----

    private sealed record Seen(nint Handle, RECT Rect);

    /// <summary>Start's host (StartMenuExperienceHost's CoreWindow as wide as its display) and its search box (SearchHost's, not), showing.</summary>
    private static (Seen? Host, Seen? Box) StartWindows()
    {
        var starts = Ids("StartMenuExperienceHost");
        var searches = Ids("SearchHost");
        Seen? host = null, box = null;
        var after = HWND.NULL;
        fixed (char* name = "Windows.UI.Core.CoreWindow")
        {
            while ((after = FindWindowExW(HWND.NULL, after, name, null)) != HWND.NULL)
            {
                uint pid, cloaked = 0;
                RECT rect;
                _ = GetWindowThreadProcessId(after, &pid);
                if (!IsWindowVisible(after) || !GetWindowRect(after, &rect)
                    || (DwmGetWindowAttribute(after, (uint)DWMWINDOWATTRIBUTE.DWMWA_CLOAKED, &cloaked, sizeof(uint)).SUCCEEDED && cloaked != 0))
                {
                    continue;
                }
                var monitor = MonitorFromWindow(after, MONITOR.MONITOR_DEFAULTTONEAREST);
                var info = new MONITORINFO { cbSize = (uint)sizeof(MONITORINFO) };
                var wide = GetMonitorInfoW(monitor, &info) && rect.right - rect.left >= info.rcMonitor.right - info.rcMonitor.left;
                if (host is null && wide && starts.Contains((int)pid))
                {
                    host = new Seen(after, rect);
                }
                else if (box is null && !wide && searches.Contains((int)pid))
                {
                    box = new Seen(after, rect);
                }
            }
        }
        return (host, host is null ? null : box);
    }

    private static HashSet<int> Ids(string image)
    {
        var ids = new HashSet<int>();
        foreach (var process in Process.GetProcessesByName(image))
        {
            using (process)
            {
                ids.Add(process.Id);
            }
        }
        return ids;
    }

    // ---- Karpo's Start buttons, through UI Automation ----

    /// <summary>A bar of Karpo's with a Start button: the bar's own rectangle and edge, the button's, and its Invoke.</summary>
    private sealed class StartBar(string display, Box bar, CoreEdge edge, Box button, void* element)
    {
        public string Display { get; } = display;
        public Box Bar { get; } = bar;
        public CoreEdge Edge { get; } = edge;
        public Box Button { get; } = button;

        /// <summary>The button pressed as Narrator presses it (IUIAutomationInvokePattern.Invoke): its click.</summary>
        public void Invoke() => Automation.Invoke(element);

        public void Release() => Automation.Release(element);
    }

    private static List<StartBar> StartButtons(void* automation, Snapshot snapshot)
    {
        var found = new List<StartBar>();
        foreach (var bar in snapshot.Bars.Where(bar => bar.Visible && !bar.Cloaked && bar.Display is not null))
        {
            if (found.Any(each => each.Display == bar.Display))
            {
                continue;
            }
            var root = Automation.FromHandle(automation, bar.Handle);
            if (root == null)
            {
                continue;
            }
            var barRect = Box.Of(Automation.Bounds(root));
            foreach (var child in Automation.Children(automation, root))
            {
                if (Automation.Id((void*)child) == "Start" && snapshot.DisplayOf(bar.Display!) is { } display)
                {
                    found.Add(new StartBar(bar.Display!, barRect, EdgeOf(barRect, display), Box.Of(Automation.Bounds((void*)child)), (void*)child));
                }
                else
                {
                    Automation.Release((void*)child);
                }
            }
            ((IUnknown*)root)->Release();
        }
        return found;
    }

    /// <summary>The edge a bar stands on: its long side's, nearest its display's end.</summary>
    private static CoreEdge EdgeOf(Box bar, DisplayFacts display)
    {
        var bounds = display.Bounds;
        return bar.Width >= bar.Height
            ? (bar.Top - bounds.Top <= bounds.Bottom - bar.Bottom ? CoreEdge.Top : CoreEdge.Bottom)
            : (bar.Left - bounds.Left <= bounds.Right - bar.Right ? CoreEdge.Left : CoreEdge.Right);
    }

    /// <summary>
    /// UI Automation's rectangle of Start's panel inside its <paramref name="host"/>: the largest element two levels down
    /// that is smaller than the host; null where none is read.
    /// </summary>
    private static RECT? UiaPanel(RECT host)
    {
        void* automation = null;
        try
        {
            automation = Automation.Make();
            var (handle, _) = StartWindows();
            if (handle is null)
            {
                return null;
            }
            var root = Automation.FromHandle(automation, handle.Handle);
            if (root == null)
            {
                return null;
            }
            RECT? best = null;
            long area = 0;
            var level = new List<nint> { (nint)root };
            for (var depth = 0; depth < 3 && level.Count > 0; depth++)
            {
                var next = new List<nint>();
                foreach (var each in level)
                {
                    foreach (var child in Automation.Children(automation, (void*)each))
                    {
                        var rect = Automation.Bounds((void*)child);
                        var size = (long)(rect.right - rect.left) * (rect.bottom - rect.top);
                        if (size > area && rect.right - rect.left < host.right - host.left && rect.left >= host.left && rect.right <= host.right)
                        {
                            (best, area) = (rect, size);
                        }
                        next.Add((nint)child);
                    }
                    Automation.Release((void*)each);
                }
                level = next;
            }
            foreach (var left in level)
            {
                Automation.Release((void*)left);
            }
            return best;
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            Automation.Release(automation);
        }
    }

    // ---- Keys (only with --keys) ----

    private static void Keys(params int[] keys)
    {
        var inputs = stackalloc INPUT[keys.Length * 2];
        for (var i = 0; i < keys.Length; i++)
        {
            inputs[i].type = INPUT.INPUT_KEYBOARD;
            inputs[i].Anonymous.ki.wVk = (ushort)keys[i];
            inputs[keys.Length * 2 - 1 - i].type = INPUT.INPUT_KEYBOARD;
            inputs[keys.Length * 2 - 1 - i].Anonymous.ki.wVk = (ushort)keys[i];
            inputs[keys.Length * 2 - 1 - i].Anonymous.ki.dwFlags = KEYEVENTF_KEYUP;
        }
        _ = SendInput((uint)(keys.Length * 2), inputs, sizeof(INPUT));
    }

    // ---- A picture of the place (a 24-bit BMP, cut to Start, the bar and the box with a margin) ----

    private static string? Picture(Box around, Box display, int number)
    {
        var margin = 60;
        var cut = new Box(Math.Max(display.Left, around.Left - margin), Math.Max(display.Top, around.Top - margin),
            Math.Min(display.Right, around.Right + margin), Math.Min(display.Bottom, around.Bottom + margin));
        if (cut.Empty)
        {
            return null;
        }
        var screen = GetDC(HWND.NULL);
        var memory = CreateCompatibleDC(screen);
        var info = new BITMAPINFO();
        info.bmiHeader.biSize = (uint)sizeof(BITMAPINFOHEADER);
        info.bmiHeader.biWidth = cut.Width;
        info.bmiHeader.biHeight = cut.Height;
        info.bmiHeader.biPlanes = 1;
        info.bmiHeader.biBitCount = 24;
        void* bits;
        var bitmap = CreateDIBSection(screen, &info, DIB_RGB_COLORS, &bits, HANDLE.NULL, 0);
        try
        {
            var old = SelectObject(memory, (HGDIOBJ)bitmap);
            _ = BitBlt(memory, 0, 0, cut.Width, cut.Height, screen, cut.Left, cut.Top, SRCCOPY | CAPTUREBLT);
            _ = SelectObject(memory, old);
            var stride = (cut.Width * 3 + 3) & ~3;
            var folder = Path.Combine(Runner.Output, "start-placement");
            Directory.CreateDirectory(folder);
            var file = Path.Combine(folder, $"start-{DateTime.Now:yyyyMMdd-HHmmss}-{number}.bmp");
            using var stream = File.Create(file);
            using var writer = new BinaryWriter(stream);
            var size = stride * cut.Height;
            writer.Write((ushort)0x4D42);
            writer.Write(14 + 40 + size);
            writer.Write(0);
            writer.Write(14 + 40);
            writer.Write(40);
            writer.Write(cut.Width);
            writer.Write(cut.Height);
            writer.Write((ushort)1);
            writer.Write((ushort)24);
            writer.Write(0);
            writer.Write(size);
            writer.Write(2835);
            writer.Write(2835);
            writer.Write(0);
            writer.Write(0);
            writer.Write(new ReadOnlySpan<byte>(bits, size));
            return $"{file} (screen {Show(cut)})";
        }
        finally
        {
            _ = DeleteObject((HGDIOBJ)bitmap);
            _ = DeleteDC(memory);
            _ = ReleaseDC(HWND.NULL, screen);
        }
    }
}

/// <summary>The few calls of UI Automation's client the scenario needs, by hand (TerraFX has no UIAutomationClient.h).</summary>
internal static unsafe class Automation
{
    private static readonly Guid Clsid = new("ff48dba4-60ef-4201-aa87-54103eef594e");
    private static readonly Guid Iid = new("30cbe57d-d9d0-452a-ab13-7ac5ac4825ee");
    private static readonly Guid InvokeIid = new("fb377fbe-8ea6-46d5-9c73-6499642d3059");
    private const int InvokePatternId = 10000;
    private const int TreeScopeChildren = 2;

    public static void* Make()
    {
        var (clsid, iid) = (Clsid, Iid);
        void* automation = null;
        Marshal.ThrowExceptionForHR(CoCreateInstance(&clsid, null, (uint)CLSCTX.CLSCTX_INPROC_SERVER, &iid, &automation));
        return automation;
    }

    public static void Release(void* unknown)
    {
        if (unknown != null)
        {
            ((IUnknown*)unknown)->Release();
        }
    }

    private static void** Table(void* com) => *(void***)com;

    /// <summary>IUIAutomation::ElementFromHandle (6).</summary>
    public static void* FromHandle(void* automation, nint window)
    {
        void* element = null;
        return ((delegate* unmanaged<void*, HWND, void**, int>)Table(automation)[6])(automation, (HWND)window, &element) >= 0 ? element : null;
    }

    /// <summary>The element's children (IUIAutomation::CreateTrueCondition (21), IUIAutomationElement::FindAll (6), the array's get_Length (3) and GetElement (4)).</summary>
    public static List<nint> Children(void* automation, void* element)
    {
        var children = new List<nint>();
        void* condition = null;
        void* array = null;
        try
        {
            if (((delegate* unmanaged<void*, void**, int>)Table(automation)[21])(automation, &condition) < 0
                || ((delegate* unmanaged<void*, int, void*, void**, int>)Table(element)[6])(element, TreeScopeChildren, condition, &array) < 0 || array == null)
            {
                return children;
            }
            int length;
            if (((delegate* unmanaged<void*, int*, int>)Table(array)[3])(array, &length) < 0)
            {
                return children;
            }
            for (var i = 0; i < length; i++)
            {
                void* child = null;
                if (((delegate* unmanaged<void*, int, void**, int>)Table(array)[4])(array, i, &child) >= 0 && child != null)
                {
                    children.Add((nint)child);
                }
            }
            return children;
        }
        finally
        {
            Release(array);
            Release(condition);
        }
    }

    /// <summary>IUIAutomationElement::get_CurrentBoundingRectangle (43).</summary>
    public static RECT Bounds(void* element)
    {
        RECT rect = default;
        _ = ((delegate* unmanaged<void*, RECT*, int>)Table(element)[43])(element, &rect);
        return rect;
    }

    /// <summary>IUIAutomationElement::get_CurrentAutomationId (29).</summary>
    public static string Id(void* element)
    {
        char* text = null;
        if (((delegate* unmanaged<void*, char**, int>)Table(element)[29])(element, &text) < 0 || text == null)
        {
            return "";
        }
        try
        {
            return new string(text);
        }
        finally
        {
            SysFreeString(text);
        }
    }

    /// <summary>IUIAutomationElement::GetCurrentPatternAs (14) the Invoke pattern, then its Invoke (3).</summary>
    public static void Invoke(void* element)
    {
        var iid = InvokeIid;
        void* pattern = null;
        Marshal.ThrowExceptionForHR(((delegate* unmanaged<void*, int, Guid*, void**, int>)Table(element)[14])(element, InvokePatternId, &iid, &pattern));
        try
        {
            Marshal.ThrowExceptionForHR(((delegate* unmanaged<void*, int>)Table(pattern)[3])(pattern));
        }
        finally
        {
            Release(pattern);
        }
    }
}
