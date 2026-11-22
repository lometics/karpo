// Whether the desktop a thread's windows are on is the one the person's input goes to.
//
// A low-level mouse hook is the machine's, not a desktop's: it hears every press made on the desktop
// that has the input, whichever desktop the windows of the thread that put it in are on. Karpo's
// hooks — a menu's (FlyoutHost), which closes it for a press outside it, and the one that puts
// down a widget following the pointer (EditFollowHook) — would read every press they heard as one
// near their own windows, so a menu on a desktop nobody sees would close for the person's clicks on theirs: the smoke pass's menus
// (Smoke.cs, which runs on a hidden desktop), and a test's panel in the middle of the test, whenever
// anything pressed a button on the person's screen while the suite ran. A window reacts only to input
// on its own desktop, so each hook asks here first and lets a press on another desktop go by.
//
// Asked on the thread the hook's callback runs on, which is the thread that put the hook in and owns
// the windows it serves. The answer is Windows' own (UOI_IO), read at each press.

using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

internal static unsafe class InputDesktop
{
    /// <summary>winuser.h's UOI_IO, which TerraFX does not carry: whether a desktop is receiving the user's input.</summary>
    private const int UOI_IO = 6;

    // Per thread, as the hooks are: a test that stands in for the person's desktop on its own thread
    // cannot be seen by another test's hook (or by the dock's) running beside it.
    [ThreadStatic]
    private static Func<bool>? holds;

    /// <summary>
    /// Whether this thread's desktop — the one its windows are on — is the desktop receiving the
    /// input now: <see cref="Here"/>, except in the tests. A desktop a test makes never has the
    /// input, so a test that holds what a press on the person's own desktop does says so here, and
    /// puts <see cref="Here"/> back. Nothing in the dock sets it.
    /// </summary>
    internal static Func<bool> Holds
    {
        get => holds ?? Here;
        set => holds = value;
    }

    /// <summary>
    /// Windows' answer for this thread's desktop: whether it is the one receiving the input. Where
    /// Windows cannot say, a press is taken as the hooks always took it — as one on this desktop —
    /// so nothing that closes on a press stops closing on an error.
    /// </summary>
    internal static bool Here() => Receiving(GetThreadDesktop(GetCurrentThreadId()));

    /// <summary>As <see cref="Here"/>, for <paramref name="desktop"/>, from any thread: the mouse hook's, for a menu's (PressHook.UseUp).</summary>
    internal static bool Receiving(HDESK desktop)
    {
        if (desktop == HDESK.NULL)
        {
            return true;
        }
        BOOL receiving = default;
        uint needed = 0;
        return !GetUserObjectInformationW((HANDLE)desktop.Value, UOI_IO, &receiving, (uint)sizeof(BOOL), &needed) || receiving;
    }
}
