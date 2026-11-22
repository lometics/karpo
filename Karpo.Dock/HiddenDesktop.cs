// A desktop nobody sees, for drawing that must not reach the screen: the smoke's (Smoke.cs) and the
// offscreen tests'. The thread that draws makes one and moves onto it before it owns a window; the
// thread that started that one closes it, once it has ended — here, and nowhere else.
//
// That last part is why this file exists. CloseDesktop refuses a desktop any thread of the process
// is still on: it fails with ERROR_BUSY (170). Every harness called it from the thread on the
// desktop, in a finally that threw the answer away, so no hidden desktop was ever closed before its
// process ended — a full KARPO_UI_TESTS run held some ninety at once. Closed from the thread that
// joined the one on it, each goes when its test does (a full run now holds three at most), and one
// that still can't be closed says which threads hold it.
//
// With one exception, which is Windows' and not ours: the text services (TSF) make threads of their
// own for the whole process the first time any thread of it engages text input, on that thread's
// desktop, and they stay for the life of the process. Which test that is depends on the order the
// tests run in and on the machine's input languages. That one desktop can't close before the
// process ends, whoever closes it; it is counted (LeftToTextServices), not thrown.

using System.Diagnostics;
using System.Runtime.InteropServices;
using Karpo.Dock.Native;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

internal static unsafe class HiddenDesktop
{
    /// <summary>What CloseDesktop says when a thread of this process is still on the desktop.</summary>
    internal const int Busy = 170; // ERROR_BUSY

    private const uint ThreadQueryInformation = 0x0040;
    private const int ThreadQuerySetWin32StartAddress = 9;
    private const uint ModuleFromAddress = 0x4;
    private const uint ModuleUnchangedRefcount = 0x2;

    /// <summary>Where the text services' own threads start: the only holders a desktop is left to.</summary>
    private static readonly string[] TextServices = ["msctf.dll", "msctfmonitor.dll"];

    /// <summary>How long a desktop no thread is on any more is given to be let go: 40 × 50 ms.</summary>
    private const int SettleTries = 40;

    private const int SettleStepMs = 50;

    private static int leftToTextServices;

    /// <summary>
    /// How many desktops this process had to leave to its end because the text services' threads
    /// were on them (<see cref="Close"/>). At most one: those threads are the process's, made once.
    /// </summary>
    internal static int LeftToTextServices => Volatile.Read(ref leftToTextServices);

    /// <summary>
    /// Closes <paramref name="desktop"/>, which no thread of this process may still be on: call it
    /// from the thread that started the one that was, after joining it. Nothing for a desktop never
    /// made. Throws when it can't be closed, naming where the threads still on it started — unless
    /// they are all the text services' own, when it is left for the process's end and counted.
    /// </summary>
    internal static void Close(HDESK desktop)
    {
        if (desktop == HDESK.NULL || CloseDesktop(desktop))
        {
            return;
        }
        var error = Marshal.GetLastSystemError();
        var holders = error == Busy ? Holders(desktop) : [];
        // A thread that has ended can hold its desktop a moment past the join: Windows lets go of
        // it as it tears the thread down, which a machine busy with other work (builds beside the
        // suite) can leave behind. With no thread of this process on it any more, it is tried
        // again for a while before it counts as held.
        for (var tries = 0; error == Busy && holders.Count == 0 && tries < SettleTries; tries++)
        {
            Thread.Sleep(SettleStepMs);
            if (CloseDesktop(desktop))
            {
                return;
            }
            error = Marshal.GetLastSystemError();
            holders = error == Busy ? Holders(desktop) : [];
        }
        if (holders.Count > 0 && holders.TrueForAll(module => TextServices.Contains(module, StringComparer.OrdinalIgnoreCase)))
        {
            Interlocked.Increment(ref leftToTextServices);
            return;
        }
        var why = error == Busy
            ? $" (ERROR_BUSY): threads of this process are still on it, started in {(holders.Count > 0 ? string.Join(", ", holders) : "nothing found")} — close it from the thread that joined the one that made it"
            : "";
        throw new NativeException($"CloseDesktop failed: error {error}{why}", HRESULT_FROM_WIN32((uint)error).Value);
    }

    /// <summary>The modules the threads of this process that are on <paramref name="desktop"/> started in.</summary>
    private static List<string> Holders(HDESK desktop)
    {
        var modules = new List<string>();
        using var process = Process.GetCurrentProcess();
        foreach (ProcessThread thread in process.Threads)
        {
            using (thread)
            {
                if (GetThreadDesktop((uint)thread.Id) == desktop)
                {
                    modules.Add(StartedIn((uint)thread.Id));
                }
            }
        }
        return modules;
    }

    /// <summary>The file name of the module thread <paramref name="id"/> started in, or "?".</summary>
    private static string StartedIn(uint id)
    {
        var thread = OpenThread(ThreadQueryInformation, BOOL.FALSE, id);
        if (thread == HANDLE.NULL)
        {
            return "?";
        }
        try
        {
            nint start = 0;
            HMODULE module;
            if (NtQueryInformationThread(thread, ThreadQuerySetWin32StartAddress, &start, (uint)sizeof(nint), null) != 0
                || !GetModuleHandleExW(ModuleFromAddress | ModuleUnchangedRefcount, (char*)start, &module))
            {
                return "?";
            }
            var name = stackalloc char[260];
            var length = GetModuleFileNameW(module, name, 260);
            return length == 0 ? "?" : Path.GetFileName(new string(name, 0, (int)length));
        }
        finally
        {
            _ = CloseHandle(thread);
        }
    }

    // ntdll, which TerraFX has no binding for.
    [DllImport("ntdll", ExactSpelling = true)]
    private static extern int NtQueryInformationThread(HANDLE thread, int informationClass, void* information, uint length, uint* returned);
}
