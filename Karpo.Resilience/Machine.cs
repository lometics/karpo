// What the harness does to the rest of the machine: the displays' power, sleep and hibernate with a
// wake timer set first (no wake timer, no sleep), Explorer ended (winlogon starts it again, as after
// a crash), the graphics driver restarted (Win+Ctrl+Shift+B), and test windows of its own.

using System.Diagnostics;
using System.Runtime.InteropServices;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using ERROR = TerraFX.Interop.Windows.ERROR;

namespace Karpo.Resilience;

internal static unsafe partial class Machine
{
    [LibraryImport("powrprof.dll", EntryPoint = "SetSuspendState")]
    [return: MarshalAs(UnmanagedType.U1)]
    private static partial bool SetSuspendState([MarshalAs(UnmanagedType.U1)] bool hibernate, [MarshalAs(UnmanagedType.U1)] bool force, [MarshalAs(UnmanagedType.U1)] bool wakeupEventsDisabled);

    /// <summary>
    /// Sleeps (S3) or hibernates (S4) and wakes <paramref name="seconds"/> later by a waitable timer
    /// set to resume the machine. Returns once the machine is back and this thread runs again; how
    /// long it was away, by the wall clock.
    /// </summary>
    public static string Suspend(bool hibernate, int seconds)
    {
        var timer = CreateWaitableTimerExW(null, null, 0, TIMER_ALL_ACCESS);
        if (timer == HANDLE.NULL)
        {
            return $"no wake timer: {Marshal.GetLastWin32Error()}";
        }
        try
        {
            // Relative, in 100 ns units: negative.
            var due = -(long)seconds * 10_000_000;
            if (!SetWaitableTimer(timer, (LARGE_INTEGER*)&due, 0, null, null, true))
            {
                return $"no wake timer: {Marshal.GetLastWin32Error()}";
            }
            if (Marshal.GetLastWin32Error() == ERROR.ERROR_NOT_SUPPORTED)
            {
                return "the machine cannot be woken by a timer: not suspending";
            }
            var before = DateTime.Now;
            var tick = Environment.TickCount64;
            if (!SetSuspendState(hibernate, false, false))
            {
                return $"SetSuspendState failed: {Marshal.GetLastWin32Error()}";
            }
            // SetSuspendState returns after the wake; the timer has fired by then (or the person woke it).
            WaitForSingleObject(timer, 120_000);
            var away = DateTime.Now - before;
            return $"{(hibernate ? "hibernated" : "slept")} and back after {away.TotalSeconds:0} s by the wall clock ({(Environment.TickCount64 - tick) / 1000.0:0} s by the tick count)";
        }
        finally
        {
            CloseHandle(timer);
        }
    }

    /// <summary>Every display off (SC_MONITORPOWER), then on again by a nudge of the pointer after <paramref name="seconds"/>.</summary>
    public static string DisplaysOff(int seconds)
    {
        var window = TestWindow.Hidden();
        DefWindowProcW((HWND)window, WM.WM_SYSCOMMAND, SC.SC_MONITORPOWER, 2);
        Thread.Sleep(seconds * 1000);
        Nudge();
        DestroyWindow((HWND)window);
        return $"displays off for {seconds} s, then a nudge of the pointer";
    }

    /// <summary>Moves the pointer one pixel and back: input, which lights the displays.</summary>
    public static void Nudge()
    {
        var inputs = stackalloc INPUT[2];
        inputs[0].type = INPUT.INPUT_MOUSE;
        inputs[0].Anonymous.mi.dx = 1;
        inputs[0].Anonymous.mi.dwFlags = C.MOUSEEVENTF_MOVE;
        inputs[1].type = INPUT.INPUT_MOUSE;
        inputs[1].Anonymous.mi.dx = -1;
        inputs[1].Anonymous.mi.dwFlags = C.MOUSEEVENTF_MOVE;
        SendInput(2, inputs, sizeof(INPUT));
    }

    /// <summary>Ends Explorer as a crash would; winlogon starts a new one. How long until its taskbar was back.</summary>
    public static string EndExplorer()
    {
        var shellPid = ExplorerTray();
        var shell = shellPid != 0 ? Process.GetProcessById(shellPid) : null;
        if (shell is null)
        {
            return "no Explorer with a taskbar";
        }
        shell.Kill();
        shell.WaitForExit(5000);
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed < TimeSpan.FromSeconds(30))
        {
            if (ExplorerTray() is var now && now != 0 && now != shellPid)
            {
                return $"Explorer {shellPid} ended; a new taskbar after {watch.ElapsedMilliseconds} ms";
            }
            Thread.Sleep(100);
        }
        // winlogon did not start it (AutoRestartShell off): start it ourselves.
        Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = false });
        return $"Explorer {shellPid} ended; winlogon did not start it again within 30 s, so it was started";
    }

    /// <summary>Win+Ctrl+Shift+B: Windows restarts the graphics driver (the screen goes black for a moment).</summary>
    public static string ResetGraphics()
    {
        ushort[] keys = [(ushort)VK.VK_LWIN, (ushort)VK.VK_LCONTROL, (ushort)VK.VK_LSHIFT, (ushort)'B'];
        var inputs = stackalloc INPUT[keys.Length * 2];
        for (var i = 0; i < keys.Length; i++)
        {
            inputs[i].type = INPUT.INPUT_KEYBOARD;
            inputs[i].Anonymous.ki.wVk = keys[i];
            inputs[keys.Length * 2 - 1 - i].type = INPUT.INPUT_KEYBOARD;
            inputs[keys.Length * 2 - 1 - i].Anonymous.ki.wVk = keys[i];
            inputs[keys.Length * 2 - 1 - i].Anonymous.ki.dwFlags = KEYEVENTF_KEYUP;
        }
        var sent = SendInput((uint)(keys.Length * 2), inputs, sizeof(INPUT));
        return $"Win+Ctrl+Shift+B sent ({sent} of {keys.Length * 2} inputs)";
    }

    /// <summary>
    /// The process of Explorer's own taskbar (0 for none). Karpo makes a Shell_TrayWnd of its own (the tray
    /// that hears the icons), so the taskbar is the Shell_TrayWnd of an explorer.exe.
    /// </summary>
    public static int ExplorerTray()
    {
        var shells = Process.GetProcessesByName("explorer").Select(p => p.Id).ToHashSet();
        foreach (var window in Probe.TopWindows())
        {
            if (Probe.ClassOf(window) == "Shell_TrayWnd")
            {
                uint pid;
                GetWindowThreadProcessId((HWND)window, &pid);
                if (shells.Contains((int)pid))
                {
                    return (int)pid;
                }
            }
        }
        return 0;
    }

    /// <summary>Whether Explorer's taskbar is set to auto-hide (ABM_GETSTATE); null with no Explorer taskbar.</summary>
    public static bool? TaskbarAutoHides()
    {
        var shell = ExplorerTray();
        foreach (var window in Probe.TopWindows())
        {
            uint pid;
            if (Probe.ClassOf(window) == "Shell_TrayWnd" && GetWindowThreadProcessId((HWND)window, &pid) != 0 && (int)pid == shell)
            {
                var data = new APPBARDATA { cbSize = (uint)sizeof(APPBARDATA), hWnd = (HWND)window };
                return (SHAppBarMessage(ABM.ABM_GETSTATE, &data) & ABS_AUTOHIDE) != 0;
            }
        }
        return null;
    }

    /// <summary>Ends a process at once, as Task Manager's End task on it does: no code of its own runs.</summary>
    public static void Kill(int pid)
    {
        using var process = Process.GetProcessById(pid);
        process.Kill();
        process.WaitForExit(5000);
    }
}
