// app clicks: "End task" in an app's jump list, where Windows' own developer setting puts it
// (Settings > System > For developers > End task: HKCU\...\Explorer\Advanced\TaskbarDeveloperSettings,
// TaskbarEndTask = 1), read each time a jump list opens.
//
// It ends the app's processes outright, as Task Manager's End task does, where Close window only
// asks. A store app's windows belong to its frame (ApplicationFrameHost), which every store app
// shares, so the process ended is the app's own inside the frame — or none, rather than every store
// app at once. Karpo's own process, and the system's, are never ended.

using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using ERROR = TerraFX.Interop.Windows.ERROR;

namespace Karpo.Dock;

/// <summary>app clicks: one window's process, as End task sees it: its id, its program, and for a store app's frame the app's own process inside it (0 for none).</summary>
public readonly record struct TaskWindow(uint Pid, string Exe, uint Inside = 0);

internal static unsafe class EndTask
{
    private const string Key = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\TaskbarDeveloperSettings";

    /// <summary>The frame every store app's windows belong to.</summary>
    private const string FrameHost = "applicationframehost.exe";

    /// <summary>Windows' own "End task" setting: on only where it says 1.</summary>
    internal static bool Enabled
    {
        get
        {
            uint data = 0;
            var size = (uint)sizeof(uint);
            fixed (char* key = Key)
            fixed (char* name = "TaskbarEndTask")
            {
                return RegGetValueW(HKEY.HKEY_CURRENT_USER, key, name, (uint)RRF.RRF_RT_REG_DWORD, null, &data, &size) == ERROR.ERROR_SUCCESS
                    && data == 1;
            }
        }
    }

    /// <summary>
    /// The processes End task ends for <paramref name="windows"/>: each window's, once — a store
    /// app's own inside its frame, never the frame —, and never <paramref name="own"/> (Karpo) or
    /// the system's (ids 0 and 4).
    /// </summary>
    internal static List<uint> Processes(IEnumerable<TaskWindow> windows, uint own)
    {
        var ends = new List<uint>();
        foreach (var window in windows)
        {
            var pid = Path.GetFileName(window.Exe).Equals(FrameHost, StringComparison.OrdinalIgnoreCase) ? window.Inside : window.Pid;
            if (pid is 0 or 4 || pid == own || ends.Contains(pid))
            {
                continue;
            }
            ends.Add(pid);
        }
        return ends;
    }

    /// <summary>Ends the processes of <paramref name="windows"/> (<see cref="Processes"/>); one that cannot be opened to end — an app run as administrator — is left.</summary>
    internal static void End(IEnumerable<HWND> windows)
    {
        var tasks = new List<TaskWindow>();
        foreach (var window in windows)
        {
            uint pid = 0;
            _ = GetWindowThreadProcessId(window, &pid);
            if (pid != 0)
            {
                var exe = Apps.ImagePath(pid) ?? "";
                tasks.Add(new TaskWindow(pid, exe, Path.GetFileName(exe).Equals(FrameHost, StringComparison.OrdinalIgnoreCase) ? Inside(window, pid) : 0));
            }
        }
        foreach (var pid in Processes(tasks, GetCurrentProcessId()))
        {
            var process = OpenProcess((uint)PROCESS.PROCESS_TERMINATE, BOOL.FALSE, pid);
            if (process != HANDLE.NULL)
            {
                _ = TerminateProcess(process, 1);
                _ = CloseHandle(process);
            }
        }
    }

    /// <summary>The process of the store app inside frame <paramref name="frame"/>: its core window's, the child that is not the frame's own.</summary>
    private static uint Inside(HWND frame, uint framePid)
    {
        var child = HWND.NULL;
        fixed (char* core = "Windows.UI.Core.CoreWindow")
        {
            while ((child = FindWindowExW(frame, child, core, null)) != HWND.NULL)
            {
                uint pid = 0;
                _ = GetWindowThreadProcessId(child, &pid);
                if (pid != 0 && pid != framePid)
                {
                    return pid;
                }
            }
        }
        return 0;
    }
}
