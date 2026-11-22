// §667: Karpo answers Windows' "where is this window's button?", so a window minimizes into its button on Karpo's bar
// and is restored out of it — on a bar at the side, at the top, on any display — rather than into Explorer's hidden
// taskbar at the bottom.
//
// How Windows asks (measured on this Windows, 26300, 2026-11-12, with windows of a probe's own): as a window is minimized
// or restored, Windows sends WM_KLUDGEMINRECT (0x8B; wParam the window, lParam a RECT) to every shell hook window of the
// desktop (RegisterShellHookWindow), one after another in the order they were registered, waiting about 100 ms for each.
// DefWindowProc turns it into the documented message, the registered "SHELLHOOK" one with HSHELL_GETMINRECT: lParam a
// SHELLHOOKINFO whose rectangle is four SHORTs (left, top, right, bottom, screen px; ManagedShell found the same), which
// the window fills in and returns TRUE. The same RECT goes on to the next window, and Windows takes what it holds at the
// end: the last window registered that answers decides. Explorer's taskbar registered long before Karpo and answers with
// its own button, at the bottom of the display however hidden it is (§465, §591); Karpo's answer comes after it and wins.
//
// So Karpo has a shell hook window of its own for this, on a thread of its own: the dock's thread may be busy for longer
// than Windows waits. It reads the bars' places as the dock last published them (WindowButtons). A new Explorer (its
// taskbar made again, TaskbarCreated) registers after Karpo, so Karpo's window registers again then, last once more
// (Again): registering a window that is registered already moves nothing, so it goes and comes back.
//
// Never on the person's desktop from a Karpo on a config of its own (a test, --smoke): only a stage on a desktop of its own.

using System.Runtime.InteropServices;
using Karpo.Core;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

internal sealed unsafe class MinRect : IDisposable
{
    /// <summary>What Windows sends each shell hook window, which DefWindowProc turns into HSHELL_GETMINRECT (see the top of the file).</summary>
    internal const uint WM_KLUDGEMINRECT = 0x008B;

    /// <summary>The shell hook message's code for the question.</summary>
    internal const uint HSHELL_GETMINRECT = 5;

    private const string ClassName = "Karpo.MinRect";

    /// <summary>Posted to its window: register again, last (<see cref="Again"/>).</summary>
    private const uint WM_KARPO_AGAIN = WM.WM_APP + 1;

    /// <summary>Posted to its window: it goes, and its thread ends.</summary>
    private const uint WM_KARPO_END = WM.WM_APP + 2;

    // What the window procedure works with. Static: a window procedure gets no context, and there is one at a time.
    private static uint shellHook;
    private static Func<nint, RECT?> answer = _ => null;

    private readonly Thread thread;
    private HWND window;

    private MinRect(Func<nint, RECT?> rectOf)
    {
        answer = rectOf;
        // On the desktop of the thread that starts it — the dock's —, whose windows Windows asks about: a new thread starts
        // on the process's.
        var desktop = GetThreadDesktop(GetCurrentThreadId());
        // Not disposed here: a thread slower than the wait below still sets it, and must not throw.
        var ready = new ManualResetEventSlim(false);
        thread = new Thread(() => Troubles.Guard("Min rect", () => Run(desktop, ready))) { IsBackground = true, Name = "Karpo min rect" }; // not-ui: a thread's name
        thread.Start();
        _ = ready.Wait(TimeSpan.FromSeconds(5));
    }

    /// <summary>Its window, registered for the question; NULL where it could not be made.</summary>
    internal HWND Hwnd => window;

    /// <summary>
    /// Karpo's answer, on a thread of its own: <paramref name="rectOf"/> says where a window's button is (WindowButtons.For).
    /// Null where its window could not be made.
    /// </summary>
    public static MinRect? Start(Func<nint, RECT?> rectOf)
    {
        var started = new MinRect(rectOf);
        if (started.window == HWND.NULL)
        {
            started.Dispose();
            return null;
        }
        return started;
    }

    /// <summary>Registers again, so it is the last to answer: a new Explorer registered after it (see the top of the file).</summary>
    public void Again()
    {
        if (window != HWND.NULL)
        {
            _ = PostMessageW(window, WM_KARPO_AGAIN, 0, 0);
        }
    }

    public void Dispose()
    {
        if (window != HWND.NULL)
        {
            _ = PostMessageW(window, WM_KARPO_END, 0, 0);
        }
        _ = thread.Join(TimeSpan.FromSeconds(2));
        answer = _ => null;
    }

    /// <summary>
    /// The answer to HSHELL_GETMINRECT: <paramref name="info"/> is the SHELLHOOKINFO Windows hands over — the window, then
    /// its rectangle as four SHORTs —, filled in with where <paramref name="rectOf"/> says its button is. False, the
    /// rectangle left as it came, where there is no answer.
    /// </summary>
    internal static bool Answer(void* info, Func<nint, RECT?> rectOf)
    {
        if (info == null || rectOf(*(nint*)info) is not { } rect)
        {
            return false;
        }
        var at = (short*)((byte*)info + sizeof(nint));
        at[0] = Short(rect.left);
        at[1] = Short(rect.top);
        at[2] = Short(rect.right);
        at[3] = Short(rect.bottom);
        return true;
    }

    private static short Short(int value) => (short)Math.Clamp(value, short.MinValue, short.MaxValue);

    private void Run(HDESK desktop, ManualResetEventSlim ready)
    {
        try
        {
            if (desktop == HDESK.NULL || !SetThreadDesktop(desktop))
            {
                return;
            }
            fixed (char* name = "SHELLHOOK")
            {
                shellHook = RegisterWindowMessageW(name);
            }
            var instance = (HINSTANCE)(void*)GetModuleHandleW(null);
            fixed (char* className = ClassName)
            {
                var type = new WNDCLASSW { lpfnWndProc = &Procedure, hInstance = instance, lpszClassName = className };
                // Fails when an earlier one registered it, which is fine: same procedure.
                _ = RegisterClassW(&type);
                // Never shown: a plain hidden window is what Windows was seen asking.
                window = CreateWindowExW((uint)WS.WS_EX_TOOLWINDOW, className, null, WS.WS_POPUP, 0, 0, 0, 0, HWND.NULL, HMENU.NULL, instance, null);
            }
            if (window == HWND.NULL || !RegisterShellHookWindow(window))
            {
                if (window != HWND.NULL)
                {
                    // §354: a window that never shows, let go of through Surfaces as every one of Karpo's is.
                    Surfaces.Gone(window);
                    window = HWND.NULL;
                }
                return;
            }
        }
        finally
        {
            ready.Set();
        }
        MSG msg;
        while (GetMessageW(&msg, HWND.NULL, 0, 0) > 0)
        {
            _ = DispatchMessageW(&msg);
        }
    }

    [UnmanagedCallersOnly]
    private static LRESULT Procedure(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        try
        {
            if (shellHook != 0 && msg == shellHook)
            {
                return (nuint)wParam == HSHELL_GETMINRECT && Answer((void*)(nint)lParam, answer) ? 1 : 0;
            }
            switch (msg)
            {
                case WM_KARPO_AGAIN:
                    _ = DeregisterShellHookWindow(hwnd);
                    _ = RegisterShellHookWindow(hwnd);
                    return 0;
                case WM_KARPO_END:
                    _ = DeregisterShellHookWindow(hwnd);
                    Surfaces.Gone(hwnd);
                    PostQuitMessage(0);
                    return 0;
            }
        }
        catch (Exception error)
        {
            // Nothing may unwind into user32; the window being minimized goes where Explorer's answer has it.
            Troubles.Report("Min rect: no answer", error);
            return 0;
        }
        // WM_KLUDGEMINRECT included: DefWindowProc asks this window again as HSHELL_GETMINRECT, above.
        return Closing.Default(hwnd, msg, wParam, lParam);
    }
}
