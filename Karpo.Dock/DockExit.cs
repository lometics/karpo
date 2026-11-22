// How a dock's process ended, as its guard (DockGuard.cs) reads it.
//
// The guard waits on the dock's process handle. Once that is signalled it can still ask the
// handle two things: the exit code, and the process's own times, when it started and when it
// ended. One line of that goes to karpo.log (Karpo.Core\Troubles.cs), so an end that left nothing
// else behind (no crash report, no "removed" in appbar.log, as on 2026-09-27 at 01:00:24) still
// says when, after how long, and which kind of end it was.
//
// The exit code is the one witness every kind of end leaves:
//   0x4B510001   it quit, the settings app's Quit Karpo asked: DockHost.QuitBy's values are the exit
//   0x4B510002   it quit, Karpo.exe --quit asked              codes of the dock's one way out, given
//   0x4B510003   it quit, the edit bar's Quit Karpo asked     nowhere else (Closing.ExitCode)
//   0            ended without quitting: another program ended it with 0, as a job or a process tree
//                ended whole may (the app it was started from, updating itself, on 2026-10-22 and
//                -23), or a WM_QUIT Karpo did not post (the dock says so first)
//   Fatal (2)    an error ended it; Program.Main wrote the error to karpo.log first
//   1            ended from outside: Task Manager, taskkill /F and Node's kill all use 1 (as
//                did the dock's own error exit, before it had a number of its own)
//   0xFFFFFFFF   ended from outside: Stop-Process and .NET's Process.Kill use -1
//   0x40010004   DBG_TERMINATE_PROCESS: a debugger, or a tool that ends a process tree
//   0xC000013A   STATUS_CONTROL_C_EXIT: Ctrl+C, or a console closing
//   0xC0000000+  an NTSTATUS error: a crash, which Windows Error Reporting records as well

using System.Globalization;
using Karpo.Core;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

/// <summary>Which kind of end an exit code says.</summary>
internal enum ExitKind
{
    /// <summary>It quit, by its own way out, as Karpo asked.</summary>
    Quit,

    /// <summary>An error ended it, and it said which in karpo.log.</summary>
    Fatal,

    /// <summary>Another program ended it.</summary>
    Killed,

    /// <summary>It crashed.</summary>
    Crashed,

    /// <summary>A code nothing here gives.</summary>
    Unknown,
}

/// <summary>
/// A dock that ended: its process id, its exit code (null when the guard could not read it),
/// when it started (null likewise) and when it ended.
/// </summary>
internal readonly record struct DockEnd(uint Pid, uint? Code, DateTime? Started, DateTime Ended);

internal static unsafe class DockExit
{
    /// <summary>
    /// What <c>Program.Main</c> returns when an error ended the dock: not a quit's code, and not 0 or
    /// 1, what a job's end, Task Manager and taskkill /F leave, so the guard can tell the three apart.
    /// </summary>
    public const int Fatal = 2;

    /// <summary>GetExitCodeProcess's answer for a process that is still running.</summary>
    private const uint StillActive = 259;

    public static ExitKind Kind(uint code) => code switch
    {
        (uint)DockHost.QuitBy.Settings or (uint)DockHost.QuitBy.Command or (uint)DockHost.QuitBy.Menu => ExitKind.Quit,
        Fatal => ExitKind.Fatal,
        0 or 1 or 0xFFFFFFFF or 0x40010004 or 0xC000013A => ExitKind.Killed,
        StillActive => ExitKind.Unknown,
        >= 0xC0000000 => ExitKind.Crashed,
        _ => ExitKind.Unknown,
    };

    /// <summary>The kind of end, in words for the log.</summary>
    public static string Says(uint code) => code switch
    {
        (uint)DockHost.QuitBy.Settings => "it quit (asked by the settings app's Quit Karpo)",
        (uint)DockHost.QuitBy.Command => "it quit (asked by Karpo.exe --quit)",
        (uint)DockHost.QuitBy.Menu => "it quit (asked by the edit bar's Quit Karpo)",
        0 => "ended without quitting (another program ended it with 0, as a job or a process tree ended whole may, or a WM_QUIT Karpo did not post)",
        Fatal => "an error ended it (the entry before this one says which)",
        1 => "ended from outside (Task Manager, taskkill /F and Node's kill use 1), or by an error in a build from before exit code 2",
        0xFFFFFFFF => "ended from outside (Stop-Process and .NET's Process.Kill use -1)",
        0x40010004 => "ended from outside, by a debugger or a process-tree kill (DBG_TERMINATE_PROCESS)",
        0xC000013A => "ended by Ctrl+C or a console closing",
        0xC0000005 => "crashed: an access violation",
        0xC00000FD => "crashed: a stack overflow",
        0xC0000374 => "crashed: heap corruption",
        0xC0000409 => "crashed: a fail-fast (an unhandled exception, or a failed check)",
        0xC000027B => "crashed: an error WinRT stowed",
        0xC0000142 => "crashed while starting: a DLL did not initialize",
        StillActive => "the code a running process has, which an ended one should not",
        >= 0xC0000000 => "crashed",
        _ => "a code nothing in Karpo gives",
    };

    /// <summary>An exit code as it reads best: small ones in decimal, NTSTATUS-like ones in hex.</summary>
    public static string Code(uint code) =>
        code < 0x10000 ? code.ToString(CultureInfo.InvariantCulture) : $"0x{code:X8}";

    /// <summary>The guard's line. Pure.</summary>
    public static string Line(DockEnd end)
    {
        var after = end.Started is DateTime started ? $" after {Span(end.Ended - started)}" : "";
        var how = end.Code is uint code ? $"exit code {Code(code)}: {Says(code)}" : "its exit code could not be read";
        return $"guard: dock {end.Pid} ended at {end.Ended.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture)}{after}, {how}";
    }

    /// <summary>A length of time in its two largest units ("2 h 22 min", "41 s"). Pure.</summary>
    public static string Span(TimeSpan span)
    {
        if (span < TimeSpan.FromSeconds(1))
        {
            return "under a second";
        }
        static string Two(long big, string bigUnit, long small, string smallUnit) =>
            small > 0 ? $"{big} {bigUnit} {small} {smallUnit}" : $"{big} {bigUnit}";
        return span.TotalDays >= 1 ? Two((long)span.TotalDays, "d", span.Hours, "h")
            : span.TotalHours >= 1 ? Two((long)span.TotalHours, "h", span.Minutes, "min")
            : span.TotalMinutes >= 1 ? Two((long)span.TotalMinutes, "min", span.Seconds, "s")
            : $"{(long)span.TotalSeconds} s";
    }

    /// <summary>
    /// The dock's process, open for waiting on and for reading how it ended; with only the right to
    /// wait where the other is refused. Null when there is no such process.
    /// </summary>
    public static HANDLE Open(uint pid)
    {
        if (pid == 0)
        {
            return HANDLE.NULL;
        }
        var process = OpenProcess((uint)(SYNCHRONIZE | PROCESS.PROCESS_QUERY_LIMITED_INFORMATION), BOOL.FALSE, pid);
        return process != HANDLE.NULL ? process : OpenProcess((uint)SYNCHRONIZE, BOOL.FALSE, pid);
    }

    /// <summary>Waits for <paramref name="process"/> to end, reads how, and closes the handle.</summary>
    public static DockEnd Wait(HANDLE process, uint pid)
    {
        try
        {
            WaitForSingleObject(process, INFINITE);
            uint code;
            uint? read = GetExitCodeProcess(process, &code) ? code : null;
            FILETIME creation, exit, kernel, user;
            if (GetProcessTimes(process, &creation, &exit, &kernel, &user))
            {
                return new DockEnd(pid, read, Local(creation), Local(exit));
            }
            return new DockEnd(pid, read, null, Clock.Real());
        }
        finally
        {
            CloseHandle(process);
        }
    }

    private static DateTime Local(FILETIME time) =>
        DateTime.FromFileTime(((long)time.dwHighDateTime << 32) | time.dwLowDateTime);
}
