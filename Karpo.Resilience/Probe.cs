// What the running Karpo looks like from outside, at one moment: its processes, every display and
// its work area, every bar window, Explorer's taskbars, the window in front, whether the dock answers,
// and what it holds (handles, GDI and USER objects, memory). Nothing here sends Karpo anything but a
// WM_NULL.

using System.Diagnostics;
using System.Runtime.InteropServices;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using ERROR = TerraFX.Interop.Windows.ERROR;

namespace Karpo.Resilience;

internal readonly record struct Box(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;
    public bool Empty => Width <= 0 || Height <= 0;
    public static Box Of(RECT rect) => new(rect.left, rect.top, rect.right, rect.bottom);
    public bool Intersects(Box other) => Left < other.Right && other.Left < Right && Top < other.Bottom && other.Top < Bottom;
    public bool Contains(Box other) => other.Left >= Left && other.Top >= Top && other.Right <= Right && other.Bottom <= Bottom;
    public override string ToString() => $"{Left},{Top}..{Right},{Bottom} ({Width}x{Height})";
}

internal sealed record DisplayFacts(string Device, Box Bounds, Box Work, uint Dpi, bool Primary);

/// <param name="Blank">Its window region is empty: shown, it draws nothing. Read for Explorer's taskbars alone, which Karpo blanks (§542).</param>
internal sealed record WindowSeen(nint Handle, string Class, Box Rect, bool Visible, bool Cloaked, bool Topmost, string? Display, int Process, string Title = "", bool Blank = false)
{
    /// <summary>Shown, and drawn: not cloaked, not blank.</summary>
    public bool InSight => Visible && !Cloaked && !Blank;
}

internal sealed record Snapshot(
    DateTime At,
    int? Dock,
    int? Guard,
    IReadOnlyList<DisplayFacts> Displays,
    IReadOnlyList<WindowSeen> Bars,
    IReadOnlyList<WindowSeen> Taskbars,
    WindowSeen? Front,
    bool? Answers,
    long AnswerMs,
    int Handles,
    uint Gdi,
    uint User,
    long PrivateBytes,
    int Threads,
    IReadOnlyList<WindowSeen> Popups,
    (int X, int Y) Cursor,
    WindowSeen? UnderCursor,
    IReadOnlyList<(string Display, int X, int Y)> Covers,
    TimeSpan Cpu,
    IReadOnlyList<int> Launched)
{
    /// <summary>The pointer is over a window of Karpo's (a bar's strip, a popup).</summary>
    public bool PointerOnKarpo => UnderCursor is { } under && under.Process == Dock;

    public DisplayFacts? DisplayOf(string device) => Displays.FirstOrDefault(display => display.Device == device);

    /// <summary>The bars on a display: Karpo's visible, uncloaked windows of the bar's class there.</summary>
    public IEnumerable<WindowSeen> BarsOn(string device) =>
        Bars.Where(bar => bar.Display == device && bar.Visible && !bar.Cloaked && !bar.Rect.Empty);
}

internal static unsafe class Probe
{
    /// <summary>The bar's window class (Karpo.Dock, Dock.cs).</summary>
    public const string BarClass = "karpo.dock";

    public static Snapshot Take(bool ask = true)
    {
        var (dock, guard) = Processes();
        var displays = Displays();
        var bars = new List<WindowSeen>();
        var taskbars = new List<WindowSeen>();
        var popups = new List<WindowSeen>();
        var all = TopWindows();
        foreach (var window in all)
        {
            var seen = See(window, displays);
            if (seen is null)
            {
                continue;
            }
            if (dock is { } pid && seen.Process == pid && seen.Class == BarClass)
            {
                bars.Add(seen);
            }
            // Any other window of the dock's in sight: a tooltip, a preview, a menu, a folder, a peek.
            else if (dock is { } own && seen.Process == own && seen.Visible && !seen.Cloaked && !seen.Rect.Empty && seen.Display is not null)
            {
                popups.Add(seen);
            }
            // Karpo makes a Shell_TrayWnd of its own, the tray that hears the icons: not Explorer's taskbar.
            else if (seen.Class is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" && seen.Process != dock)
            {
                taskbars.Add(seen with { Blank = IsBlank(window) });
            }
        }
        var foreground = (nint)GetForegroundWindow();
        var front = foreground != 0 ? See(foreground, displays) : null;
        bool? answers = null;
        long answerMs = 0;
        if (ask && dock is not null && bars.FirstOrDefault() is { } any)
        {
            var watch = Stopwatch.StartNew();
            nuint result;
            answers = SendMessageTimeoutW((HWND)any.Handle, WM.WM_NULL, 0, 0, SMTO_ABORTIFHUNG | SMTO_BLOCK, 3000, &result) != 0;
            answerMs = watch.ElapsedMilliseconds;
        }
        var (handles, gdi, user, privateBytes, threads, cpu) = Holds(dock);
        POINT cursor;
        GetCursorPos(&cursor);
        var hit = (nint)WindowFromPoint(cursor);
        var under = hit != 0 ? See((nint)GetAncestor((HWND)hit, GA.GA_ROOT), displays) : null;
        return new Snapshot(DateTime.Now, dock, guard, displays, bars, taskbars, front, answers, answerMs, handles, gdi, user, privateBytes, threads,
            popups, (cursor.x, cursor.y), under, Covers(dock, displays, bars), cpu, Launched(dock, guard));
    }

    /// <summary>
    /// The dock and its guard. The guard is the Karpo.exe the dock started (its parent is a Karpo.exe);
    /// the dock is the one that owns bar windows, else the other one.
    /// </summary>
    public static (int? Dock, int? Guard) Processes()
    {
        var karpos = new Dictionary<int, int>();
        var snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if (snapshot == INVALID_HANDLE_VALUE)
        {
            return (null, null);
        }
        try
        {
            PROCESSENTRY32W entry;
            entry.dwSize = (uint)sizeof(PROCESSENTRY32W);
            for (var more = Process32FirstW(snapshot, &entry); more; more = Process32NextW(snapshot, &entry))
            {
                if (string.Equals(new string(&entry.szExeFile.e0), "Karpo.exe", StringComparison.OrdinalIgnoreCase))
                {
                    karpos[(int)entry.th32ProcessID] = (int)entry.th32ParentProcessID;
                }
            }
        }
        finally
        {
            CloseHandle(snapshot);
        }
        int? guard = karpos.Where(entry => karpos.ContainsKey(entry.Value)).Select(entry => (int?)entry.Key).FirstOrDefault();
        int? dock = guard is { } g ? karpos[g] : karpos.Keys.Select(id => (int?)id).FirstOrDefault();
        return (dock, guard);
    }

    public static List<DisplayFacts> Displays()
    {
        var found = new List<DisplayFacts>();
        var handle = GCHandle.Alloc(found);
        try
        {
            EnumDisplayMonitors(HDC.NULL, null, &OnMonitor, GCHandle.ToIntPtr(handle));
        }
        finally
        {
            handle.Free();
        }
        return found;
    }

    [UnmanagedCallersOnly]
    private static BOOL OnMonitor(HMONITOR monitor, HDC dc, RECT* clip, LPARAM state)
    {
        var found = (List<DisplayFacts>)GCHandle.FromIntPtr(state).Target!;
        MONITORINFOEXW info;
        info.Base.cbSize = (uint)sizeof(MONITORINFOEXW);
        if (GetMonitorInfoW(monitor, (MONITORINFO*)&info))
        {
            uint x = 96, y = 96;
            _ = GetDpiForMonitor(monitor, MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, &x, &y);
            var device = new string(&info.szDevice.e0);
            found.Add(new DisplayFacts(device, Box.Of(info.Base.rcMonitor), Box.Of(info.Base.rcWork), x, (info.Base.dwFlags & MONITORINFOF_PRIMARY) != 0));
        }
        return true;
    }

    public static List<nint> TopWindows()
    {
        var found = new List<nint>();
        var handle = GCHandle.Alloc(found);
        try
        {
            EnumWindows(&OnWindow, GCHandle.ToIntPtr(handle));
        }
        finally
        {
            handle.Free();
        }
        return found;
    }

    [UnmanagedCallersOnly]
    private static BOOL OnWindow(HWND window, LPARAM state)
    {
        ((List<nint>)GCHandle.FromIntPtr(state).Target!).Add((nint)window);
        return true;
    }

    public static string ClassOf(nint window)
    {
        var buffer = stackalloc char[256];
        var length = GetClassNameW((HWND)window, buffer, 256);
        return length > 0 ? new string(buffer, 0, length) : "";
    }

    public static WindowSeen? See(nint window, IReadOnlyList<DisplayFacts> displays)
    {
        var hwnd = (HWND)window;
        if (!IsWindow(hwnd))
        {
            return null;
        }
        RECT rect;
        if (!GetWindowRect(hwnd, &rect))
        {
            return null;
        }
        uint pid;
        GetWindowThreadProcessId(hwnd, &pid);
        int cloaked = 0;
        _ = DwmGetWindowAttribute(hwnd, (uint)DWMWINDOWATTRIBUTE.DWMWA_CLOAKED, &cloaked, sizeof(int));
        var exStyle = GetWindowLongPtrW(hwnd, GWL.GWL_EXSTYLE);
        var box = Box.Of(rect);
        var text = stackalloc char[128];
        var length = GetWindowTextW(hwnd, text, 128);
        return new WindowSeen(window, ClassOf(window), box, IsWindowVisible(hwnd), cloaked != 0, (exStyle & WS.WS_EX_TOPMOST) != 0, DisplayHolding(box, displays), (int)pid, length > 0 ? new string(text, 0, length) : "");
    }

    /// <summary>Whether <paramref name="window"/>'s region is empty (Karpo.Dock's Surfaces.OtherAppBlank).</summary>
    public static bool IsBlank(nint window)
    {
        var probe = CreateRectRgn(0, 0, 0, 0);
        try
        {
            return GetWindowRgn((HWND)window, probe) == NULLREGION;
        }
        finally
        {
            _ = DeleteObject(probe);
        }
    }

    /// <summary>The display the middle of <paramref name="box"/> is on; else the one it overlaps most.</summary>
    public static string? DisplayHolding(Box box, IReadOnlyList<DisplayFacts> displays)
    {
        var x = (box.Left + box.Right) / 2;
        var y = (box.Top + box.Bottom) / 2;
        foreach (var display in displays)
        {
            if (x >= display.Bounds.Left && x < display.Bounds.Right && y >= display.Bounds.Top && y < display.Bounds.Bottom)
            {
                return display.Device;
            }
        }
        return null;
    }

    private static (int Handles, uint Gdi, uint User, long PrivateBytes, int Threads, TimeSpan Cpu) Holds(int? dock)
    {
        if (dock is not { } pid)
        {
            return default;
        }
        try
        {
            using var process = Process.GetProcessById(pid);
            var handle = OpenProcess(PROCESS.PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)pid);
            uint gdi = 0, user = 0;
            if (handle != HANDLE.NULL)
            {
                gdi = GetGuiResources(handle, C.GR_GDIOBJECTS);
                user = GetGuiResources(handle, C.GR_USEROBJECTS);
                CloseHandle(handle);
            }
            return (process.HandleCount, gdi, user, process.PrivateMemorySize64, process.Threads.Count, process.TotalProcessorTime);
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return default;
        }
    }

    /// <summary>
    /// Where a bar's window would take a press it should not: points just inside the work area along
    /// each bar's edge, above its strip, where the bar's window stands (it is taller than the bar)
    /// but only another app's window or the desktop may be. A bar's window takes the pointer only
    /// where its region says (Present/Surfaces.cs); WindowFromPoint goes by the region as input does.
    /// </summary>
    private static List<(string Display, int X, int Y)> Covers(int? dock, IReadOnlyList<DisplayFacts> displays, IReadOnlyList<WindowSeen> bars)
    {
        var found = new List<(string, int, int)>();
        if (dock is null)
        {
            return found;
        }
        foreach (var display in displays)
        {
            var bar = bars.FirstOrDefault(b => b.Display == display.Device && b.Visible && !b.Cloaked);
            if (bar is null || Rules.EdgeOf(bar.Rect, display.Bounds).Edge != Edge.Bottom || display.Work.Bottom >= display.Bounds.Bottom)
            {
                continue;
            }
            var y = display.Work.Bottom - 24;
            if (y <= bar.Rect.Top)
            {
                continue;
            }
            foreach (var share in new[] { 0.1, 0.5, 0.9 })
            {
                var point = new POINT { x = display.Bounds.Left + (int)(display.Bounds.Width * share), y = y };
                var hit = WindowFromPoint(point);
                uint pid = 0;
                if (hit != HWND.NULL)
                {
                    GetWindowThreadProcessId(hit, &pid);
                }
                if ((int)pid == dock && ClassOf((nint)GetAncestor(hit, GA.GA_ROOT)) == BarClass)
                {
                    found.Add((display.Device, point.x, point.y));
                }
            }
        }
        return found;
    }

    /// <summary>Processes the dock started (but its guard): an app Karpo opened by itself.</summary>
    private static List<int> Launched(int? dock, int? guard)
    {
        var found = new List<int>();
        if (dock is null)
        {
            return found;
        }
        var snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if (snapshot == INVALID_HANDLE_VALUE)
        {
            return found;
        }
        try
        {
            PROCESSENTRY32W entry;
            entry.dwSize = (uint)sizeof(PROCESSENTRY32W);
            for (var more = Process32FirstW(snapshot, &entry); more; more = Process32NextW(snapshot, &entry))
            {
                if ((int)entry.th32ParentProcessID == dock && (int)entry.th32ProcessID != guard)
                {
                    found.Add((int)entry.th32ProcessID);
                }
            }
        }
        finally
        {
            CloseHandle(snapshot);
        }
        return found;
    }
}
