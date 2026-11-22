// The low-level mouse hook that closes an open menu for a press outside it (FlyoutHost), on a
// thread that does nothing else.
//
// Windows calls a low-level hook on the thread that put it in, and holds the pointer — every move
// and every press, for the whole machine — until that thread has answered, a second and more
// (LowLevelHooksTimeout) before it gives up on it. The hook was the dock thread's: whenever that
// thread was busy with anything slow while a menu was open, the pointer froze with it (the person,
// 2026-10-10: "when i click connect on a network the mouse freezes for a second or so" — the Wi-Fi
// page's connect asked the WLAN service on the dock's thread, with the flyout's hook in).
//
// So the hook lives here, on a thread of its own, which only waits for messages and answers the hook
// at once: a button going down is posted to each thread that has a menu open, to a message-only
// window of that thread's (its sink), and the rest of what a press means is worked out there, by
// FlyoutHost.Pressed, as before. A message posted while the hook runs is read before the press
// itself reaches any window — posted messages come before input — so a menu still sees itself
// closed before the click that closed it, as it did when the hook was the dock thread's own.
//
// One hook for the process, in while any thread has a menu open. It is put in afresh with each
// menu shown, as before (Windows takes a hook away without a word once it answered too slowly).
//
// A menu over a folder uses up the press that closes it (UseUp): the hook answers that press, and
// its release, itself, so nothing under it (the folder, a bar, another app, the desktop) hears of
// it, and the menu alone closes; the next press closes the folder (the person, 2026-10-16: "when
// the right click context menu is open in a folder and i click away from the folder it closes
// both, but it should only close the context menu first. on the next click the folder then."), as
// Windows' own menus over Start or a flyout do. Decided here, at once, from what the menu's thread
// said beforehand (the windows that are the menu, and its desktop): asking that thread would hold
// the pointer again.

using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Karpo.Core;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

internal static unsafe class PressHook
{
    /// <summary>Posted to a thread's sink for a button going down: wParam the mouse message (with <see cref="UsedUpBit"/> where the hook used it up), lParam where (EditFollowHook.Pack).</summary>
    internal const uint WM_KARPO_PRESS = WM.WM_APP + 0x2D;

    /// <summary>In a <see cref="WM_KARPO_PRESS"/>'s wParam: the hook used the press up for a menu (<see cref="UseUp"/>).</summary>
    internal const uint UsedUpBit = 0x8000_0000;

    /// <summary>
    /// In a <see cref="WM_KARPO_PRESS"/>'s wParam: a stage test's press, posted in the hook's stead (Stage.On only):
    /// the stage's desktop never has the input, so no hook hears its presses (MenuToggleTests).
    /// </summary>
    internal const uint StagedBit = 0x4000_0000;

    /// <summary>A menu that uses up the presses outside it: its desktop, and the windows that are the menu (its own first, then its submenus').</summary>
    internal readonly record struct Eater(HDESK Desktop, nint[] Keeps);

    // The menus using up presses, by their host, from any thread; read by the hook.
    private static readonly ConcurrentDictionary<object, Eater> Eaters = new();

    // On the hook's thread only: the buttons whose press was used up, whose release is used up too.
    private static uint owed;

    private const uint WM_ARM = WM.WM_APP + 1;
    private const uint WM_DISARM = WM.WM_APP + 2;
    private const string SinkClass = "karpo.presses";

    // The sinks presses are posted to, one for each thread with a menu open.
    private static readonly ConcurrentDictionary<nint, byte> Sinks = new();
    private static readonly object Gate = new();
    private static Thread? thread;
    private static uint threadId;

    // On the hook's thread only.
    private static HHOOK hook;

    [ThreadStatic]
    private static HWND sink;

    /// <summary>The thread the hook runs on (for a test); 0 before it started.</summary>
    internal static uint HookThread => threadId;

    /// <summary>Whether the hook is in (for a test).</summary>
    internal static bool Armed => Volatile.Read(ref armed);

    private static bool armed;

    /// <summary>This thread has a menu open: its presses are posted to it, and the hook is put in afresh.</summary>
    public static void Listen()
    {
        if (Sink() is var mine && mine == HWND.NULL)
        {
            return;
        }
        Sinks[(nint)mine] = 0;
        Post(WM_ARM);
    }

    // app clicks: on this thread, what else hears its presses: the window list a click put up, which a
    // press anywhere else puts away as a menu is (Dock.HoldPreview). It keeps the sink while it listens.
    [ThreadStatic]
    private static Action<uint, POINT>? also;

    /// <summary>
    /// app clicks: <paramref name="heard"/> hears this thread's presses too, until it is null again —
    /// the hook in for it as for a menu, and out with the last of them.
    /// </summary>
    public static void Also(Action<uint, POINT>? heard)
    {
        also = heard;
        if (heard is not null)
        {
            Listen();
        }
        else if (!FlyoutHost.Listening)
        {
            Stop();
        }
    }

    /// <summary>This thread's last menu closed: nothing more is posted to it, and the hook comes out with the last one.</summary>
    public static void Stop()
    {
        // app clicks: a window list still listening keeps it (Also).
        if (also is not null)
        {
            return;
        }
        if (sink == HWND.NULL || !Sinks.TryRemove((nint)sink, out _))
        {
            return;
        }
        if (Sinks.IsEmpty)
        {
            Post(WM_DISARM);
        }
    }

    /// <summary>
    /// <paramref name="menu"/>, open on this thread with <paramref name="keeps"/> as its windows,
    /// uses up every press outside them; said again as its submenus open. The menu's thread says
    /// so before the person can press anything, so the hook can decide alone.
    /// </summary>
    public static void UseUp(object menu, IEnumerable<HWND> keeps) =>
        Eaters[menu] = new Eater(GetThreadDesktop(GetCurrentThreadId()), [.. keeps.Where(window => window != HWND.NULL).Select(window => (nint)window)]);

    /// <summary><paramref name="menu"/> uses up no press any more: it closed, or shows something that doesn't.</summary>
    public static void LetThrough(object menu) => Eaters.TryRemove(menu, out _);

    /// <summary>The windows of the menu whose own window is <paramref name="menu"/>, where it uses up presses; null where it doesn't (for a test).</summary>
    internal static nint[]? Keeping(HWND menu) =>
        Eaters.Values.Where(eater => eater.Keeps.Length > 0 && eater.Keeps[0] == (nint)menu).Select(eater => eater.Keeps).FirstOrDefault();

    /// <summary>
    /// Whether a press on <paramref name="under"/> (a top-level window, or null) is used up by one of
    /// <paramref name="menus"/>: one stands on the desktop with the input (<paramref name="receiving"/>)
    /// (a test's, on a desktop of its own, never takes the person's presses), and the press is on
    /// none of their windows.
    /// </summary>
    internal static bool UsesUp(nint under, IEnumerable<Eater> menus, Func<Eater, bool> receiving)
    {
        var eating = false;
        foreach (var menu in menus)
        {
            if (Array.IndexOf(menu.Keeps, under) >= 0)
            {
                return false;
            }
            eating |= receiving(menu);
        }
        return eating;
    }

    /// <summary>Which button a press or a release is of, as a bit; 0 for any other message.</summary>
    internal static uint Button(uint message) => message switch
    {
        WM.WM_LBUTTONDOWN or WM.WM_LBUTTONUP => 1,
        WM.WM_RBUTTONDOWN or WM.WM_RBUTTONUP => 2,
        WM.WM_MBUTTONDOWN or WM.WM_MBUTTONUP => 4,
        WM.WM_XBUTTONDOWN or WM.WM_XBUTTONUP => 8,
        _ => 0,
    };

    /// <summary>
    /// What the hook makes of a mouse message before anything hears it, on its own thread: a press a
    /// menu uses up is answered here, and so is that button's release after it, whether the menu
    /// still stands or not. True when the message goes no further.
    /// </summary>
    private static bool UsedUp(uint message, POINT at)
    {
        var button = Button(message);
        if (button == 0)
        {
            return false;
        }
        if (!IsPress(message))
        {
            var was = owed;
            owed &= ~button;
            return (was & button) != 0;
        }
        if (Eaters.IsEmpty)
        {
            return false;
        }
        var top = GetAncestor(WindowFromPoint(at), GA.GA_ROOT);
        // A menu stands while its window shows: one a thread left behind eats nothing.
        if (!UsesUp((nint)top, Eaters.Values, menu => menu.Keeps.Length > 0 && IsWindowVisible((HWND)menu.Keeps[0]) && InputDesktop.Receiving(menu.Desktop)))
        {
            return false;
        }
        owed |= button;
        return true;
    }

    /// <summary>Whether a mouse message is a button going down, the only ones a menu hears of.</summary>
    internal static bool IsPress(uint message) =>
        message is WM.WM_LBUTTONDOWN or WM.WM_RBUTTONDOWN or WM.WM_MBUTTONDOWN or WM.WM_XBUTTONDOWN
            or WM.WM_NCLBUTTONDOWN or WM.WM_NCRBUTTONDOWN or WM.WM_NCMBUTTONDOWN or WM.WM_NCXBUTTONDOWN;

    /// <summary>What the hook does with a mouse message, on its own thread: a press is posted to every sink, saying whether it was used up, and nothing waits.</summary>
    internal static void Heard(uint message, POINT at, bool usedUp = false)
    {
        if (!IsPress(message))
        {
            return;
        }
        var where = EditFollowHook.Pack(at);
        var said = message | (usedUp ? UsedUpBit : 0);
        foreach (var target in Sinks.Keys)
        {
            _ = PostMessageW((HWND)target, WM_KARPO_PRESS, (WPARAM)(nuint)said, where);
        }
    }

    /// <summary>This thread's sink, made on first use; null where Windows made none.</summary>
    private static HWND Sink()
    {
        if (sink != HWND.NULL && IsWindow(sink))
        {
            return sink;
        }
        var instance = (HINSTANCE)(void*)GetModuleHandleW(null);
        fixed (char* name = SinkClass)
        {
            var type = new WNDCLASSW { lpfnWndProc = &SinkProcedure, hInstance = instance, lpszClassName = name };
            // Fails when another thread registered it, which is fine: same procedure.
            RegisterClassW(&type);
            // Message-only: the posts reach it, and nothing on screen is made.
            sink = CreateWindowExW(0, name, null, 0, 0, 0, 0, 0, HWND.HWND_MESSAGE, HMENU.NULL, instance, null);
        }
        return sink;
    }

    private static void Post(uint message)
    {
        lock (Gate)
        {
            if (thread is null)
            {
                // Not disposed here: a thread slower than the wait below still sets it, and must not throw.
                var ready = new ManualResetEventSlim(false);
                thread = new Thread(() => Troubles.Guard("Menus: the mouse hook", () => Run(ready))) { IsBackground = true, Name = "Karpo mouse hook", Priority = ThreadPriority.Highest }; // not-ui: a thread's name
                thread.Start();
                // Its queue is there once it is ready; it only makes that and so is ready at once.
                ready.Wait(TimeSpan.FromSeconds(5));
            }
        }
        _ = PostThreadMessageW(threadId, message, 0, 0);
    }

    private static void Run(ManualResetEventSlim ready)
    {
        MSG msg;
        // A message queue before anybody posts to it.
        _ = PeekMessageW(&msg, HWND.NULL, 0, 0, PM.PM_NOREMOVE);
        threadId = GetCurrentThreadId();
        ready.Set();
        while (GetMessageW(&msg, HWND.NULL, 0, 0) > 0)
        {
            switch (msg.message)
            {
                case WM_ARM:
                    Unhook();
                    if (!Sinks.IsEmpty)
                    {
                        hook = SetWindowsHookExW(WH.WH_MOUSE_LL, &Pointer, GetModuleHandleW(null), 0);
                        Volatile.Write(ref armed, hook != HHOOK.NULL);
                    }
                    break;
                case WM_DISARM:
                    if (Sinks.IsEmpty)
                    {
                        Unhook();
                    }
                    break;
            }
        }
        Unhook();
    }

    private static void Unhook()
    {
        if (hook != HHOOK.NULL)
        {
            _ = UnhookWindowsHookEx(hook);
            hook = HHOOK.NULL;
        }
        Volatile.Write(ref armed, false);
    }

    [UnmanagedCallersOnly]
    private static LRESULT Pointer(int code, WPARAM wParam, LPARAM lParam)
    {
        if (code >= 0)
        {
            try
            {
                var message = (uint)(nuint)wParam;
                var at = ((MSLLHOOKSTRUCT*)(nint)lParam)->pt;
                var usedUp = UsedUp(message, at);
                Heard(message, at, usedUp);
                if (usedUp)
                {
                    return 1;
                }
            }
            catch (Exception)
            {
                // Nothing may escape into the input system's callback.
            }
        }
        return CallNextHookEx(HHOOK.NULL, code, wParam, lParam);
    }

    [UnmanagedCallersOnly]
    private static LRESULT SinkProcedure(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        try
        {
            if (msg == WM_KARPO_PRESS)
            {
                var said = (uint)(nuint)wParam;
                var message = said & ~(UsedUpBit | StagedBit);
                FlyoutHost.Pressed(message, EditFollowHook.Unpack(lParam), (said & UsedUpBit) != 0, staged: (said & StagedBit) != 0 && Stage.On);
                also?.Invoke(message, EditFollowHook.Unpack(lParam));
                return 0;
            }
            return DefWindowProcW(hwnd, msg, wParam, lParam);
        }
        catch (Exception error)
        {
            // Nothing may leave a window procedure: it would end the dock (NativeBoundaryTests).
            Troubles.Note("Menus: a press elsewhere", error);
            return 0;
        }
    }
}
