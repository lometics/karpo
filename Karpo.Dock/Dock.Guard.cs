// appbar: the guard, from the dock's side (§465). While Karpo runs Windows' taskbar is hidden, and
// whenever it does not the taskbar shows: should the dock end without showing it (a crash, a kill, a
// hang ended from Task Manager), its guard (`Karpo.exe --guard <pid>`, DockGuard.cs) shows it at once
// and starts the dock again. So the dock starts its guard as it hides the taskbar, outside every job
// it is in where it may, and watches it in turn: a guard that ends while the dock runs (ended on its
// own in Task Manager) is started again, so there is always one waiting. A few times per dock at
// most: a guard that cannot stay up must not be started for ever.
//
// The guard's end is waited for on the thread pool (no polling) and posted to the host.

using Karpo.Core;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

public sealed unsafe partial class Dock
{
    /// <summary>Posted to the host from the pool thread the guard's end is heard on.</summary>
    internal const uint WM_KARPO_GUARD_GONE = WM.WM_APP + 0x2F;

    /// <summary>How many guards one dock starts at most: the first, and three more should they end.</summary>
    internal const int MostGuards = 4;

    private int guardsStarted;
    private RegisteredWaitHandle? guardWait;
    private Taskbar.ProcessEnd? guardEnd;

    /// <summary>
    /// Starts <c>Karpo.exe --guard &lt;pid&gt;</c>, which shows the taskbar and starts the dock again
    /// should this process end without doing it, where none is running for it. It hears how often
    /// this dock was restarted already, and the flags a restart keeps (DockGuard.cs).
    /// </summary>
    private void SpawnGuard()
    {
        if (guardWait is not null || guardsStarted >= MostGuards)
        {
            return;
        }
        var arguments = DockGuard.Arguments((uint)Environment.ProcessId, options.Restarts, options.Kept);
        // Out of this process's job where the job allows it: a job that is killed as a whole (a
        // terminal, a debugger, a test runner) would otherwise take the guard down with the dock it
        // guards. Where breaking away is refused, the guard starts inside the job.
        if ((StartGuard(arguments, (uint)CREATE.CREATE_BREAKAWAY_FROM_JOB) ?? StartGuard(arguments, 0)) is not { } guard)
        {
            appbarTrace.Write("the guard could not be started: should the dock end without showing Explorer's taskbar, nothing shows it"); // not-ui: the appbar trace
            return;
        }
        guardsStarted++;
        guardEnd = new Taskbar.ProcessEnd(guard);
        guardWait = ThreadPool.RegisterWaitForSingleObject(guardEnd, GuardEnded, (nint)host, Timeout.Infinite, executeOnlyOnce: true);
    }

    /// <summary>The guard started with <paramref name="flags"/>; its process handle, null where it did not start.</summary>
    private static HANDLE? StartGuard(string[] arguments, uint flags)
    {
        var exe = Environment.ProcessPath;
        if (exe is null)
        {
            return null;
        }
        var line = $"\"{exe}\" {string.Join(' ', arguments.Select(DockLauncher.Quote))}\0".ToCharArray();
        var startup = new STARTUPINFOW { cb = (uint)sizeof(STARTUPINFOW) };
        PROCESS_INFORMATION started;
        BOOL done;
        fixed (char* application = exe)
        fixed (char* command = line)
        {
            done = CreateProcessW(application, command, null, null, BOOL.FALSE, flags, null, null, &startup, &started);
        }
        if (!done)
        {
            return null;
        }
        CloseHandle(started.hThread);
        return started.hProcess;
    }

    /// <summary>On the thread pool: the guard ended. Told to the host.</summary>
    private static void GuardEnded(object? state, bool timedOut)
    {
        try
        {
            if (state is nint window)
            {
                _ = PostMessageW((HWND)window, WM_KARPO_GUARD_GONE, 0, 0);
            }
        }
        catch (Exception error)
        {
            Troubles.Report("Dock: the guard ended", error);
        }
    }

    /// <summary>
    /// The guard ended while this dock runs (<see cref="WM_KARPO_GUARD_GONE"/>): another is started
    /// where the taskbar is hidden, so one is always waiting; else the next takeover starts it.
    /// </summary>
    private void GuardGone()
    {
        guardWait?.Unregister(null);
        guardWait = null;
        guardEnd?.Dispose();
        guardEnd = null;
        if (disposed || taskbar is null)
        {
            return;
        }
        appbarTrace.Write(guardsStarted < MostGuards
            ? "the guard ended while the dock runs: another is started" // not-ui: the appbar trace
            : $"the guard ended while the dock runs, the {MostGuards}th: no other is started"); // not-ui: the appbar trace
        SpawnGuard();
    }
}
