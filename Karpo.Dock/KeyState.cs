// Whether a key is held, as the dock asks it: Shift or Ctrl with a key or the wheel, and Escape
// while a drag or a resize runs.
//
// Windows answers from the keyboard — GetKeyState as the keys stood when the message being handled
// was sent, GetAsyncKeyState as they are this moment — and on a test's thread that keyboard is the
// person's. A quick-settings test typed "Home offiX" for "Home offiXc": Shift held on the person's
// keyboard turned the test's Left into Shift+Left, a selection its next letter replaced. So every
// key the dock reads goes through here, and a test says on its own thread which keys it holds
// (Held), as InputDesktop and Clock stand in for the desktop with the input and the time. Nothing
// in the dock sets it.
//
// And a window on a desktop that is not receiving the input is pressed by nobody: there, no key is
// held, and Windows is not asked (InputDesktop) — the smoke pass's popups and every test on a
// desktop of its own.

using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

internal static class KeyState
{
    // Per thread, as the dock's windows are: a test holding Shift on its own thread cannot be seen
    // by another test's popup, or by the dock, running beside it.
    [ThreadStatic]
    private static Func<int, bool>? held;

    /// <summary>
    /// The keys a test holds, on this thread: its answer, for a virtual-key code, stands for the
    /// keyboard's everywhere the dock asks. Null — the keyboard's own answer — unless a test sets it.
    /// </summary>
    internal static Func<int, bool>? Held
    {
        get => held;
        set => held = value;
    }

    /// <summary>Whether <paramref name="key"/> was down when the message being handled was sent (GetKeyState).</summary>
    internal static bool Down(int key) =>
        held is { } stand ? stand(key) : InputDesktop.Holds() && GetKeyState(key) < 0;

    /// <summary>
    /// Whether <paramref name="key"/> is down this moment, whatever message is being handled
    /// (GetAsyncKeyState): Escape pressed while a drag or a resize runs, which a window without the
    /// keys never hears.
    /// </summary>
    internal static bool DownNow(int key) =>
        held is { } stand ? stand(key) : InputDesktop.Holds() && GetAsyncKeyState(key) < 0;
}
