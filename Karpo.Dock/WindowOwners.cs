// app clicks: which button a window in front stands for, and which window a click brings forward, as
// Windows 11's taskbar has them.
//
// An app's dialog (Save As, Find, Properties) is a window of its own, owned by the app's window and
// given no button (Apps.Listed). In front, it is still the app that is in front: its button shows as
// active and a click on it minimizes the app, the dialog going with it. So the window in front is
// walked up its owners to the first one with a button (ButtonOf); only where none has one does it
// count as itself.
//
// And the other way: a window brought forward is its last active popup (GetLastActivePopup) where that
// is another window, shown and enabled — a modal dialog over a disabled owner is what has the keys,
// and activating the owner left the dialog behind it and the typing going nowhere. Alt+Tab does the
// same (ActivationTarget).

using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

internal static unsafe class WindowOwners
{
    /// <summary>How far an owner chain is followed: owners do not loop, but nothing here may hang if one did.</summary>
    private const int Deepest = 32;

    /// <summary>
    /// The window with a button that <paramref name="front"/> stands for: itself where it has one
    /// (<paramref name="listed"/>), else the first of its owners (<paramref name="ownerOf"/>, 0 for
    /// none) that has; <paramref name="front"/> itself where none has one.
    /// </summary>
    internal static nint ButtonOf(nint front, Func<nint, nint> ownerOf, Func<nint, bool> listed)
    {
        var at = front;
        for (var step = 0; at != 0 && step < Deepest; step++)
        {
            if (listed(at))
            {
                return at;
            }
            at = ownerOf(at);
        }
        return front;
    }

    /// <summary>
    /// What bringing <paramref name="window"/> forward activates: its last active popup
    /// <paramref name="popup"/> where that is another window, shown (<paramref name="shown"/>) and
    /// enabled (<paramref name="enabled"/>); else the window itself.
    /// </summary>
    internal static nint ActivationTarget(nint window, nint popup, bool shown, bool enabled) =>
        popup != 0 && popup != window && shown && enabled ? popup : window;

    /// <summary>
    /// §619: the modal dialog of a minimized <paramref name="window"/> that is to have the keys once it is
    /// restored: its last active popup <paramref name="popup"/> where that is another window, enabled
    /// (<paramref name="popupEnabled"/>), over an owner it disabled (<paramref name="ownerEnabled"/> false) —
    /// hidden while its owner is down, so <see cref="ActivationTarget(nint, nint, bool, bool)"/> can't pick it,
    /// and the restore activates the owner. 0 for none.
    /// </summary>
    internal static nint ModalOf(nint window, nint popup, bool popupEnabled, bool ownerEnabled) =>
        popup != 0 && popup != window && popupEnabled && !ownerEnabled ? popup : 0;

    /// <summary><see cref="ModalOf(nint, nint, bool, bool)"/> read off Windows for <paramref name="window"/>.</summary>
    internal static HWND ModalOf(HWND window)
    {
        var popup = GetLastActivePopup(window);
        return (HWND)ModalOf((nint)window, (nint)popup, popup != HWND.NULL && IsWindowEnabled(popup), IsWindowEnabled(window));
    }

    /// <summary><see cref="ButtonOf"/> read off Windows: the owners as GetWindow(GW_OWNER) gives them.</summary>
    internal static HWND ButtonOf(HWND front, Func<HWND, bool> listed) =>
        (HWND)ButtonOf((nint)front, at => (nint)GetWindow((HWND)at, GW_OWNER), at => listed((HWND)at));

    /// <summary><see cref="ActivationTarget"/> read off Windows for <paramref name="window"/>.</summary>
    internal static HWND ActivationTarget(HWND window)
    {
        var popup = GetLastActivePopup(window);
        return (HWND)ActivationTarget((nint)window, (nint)popup, popup != HWND.NULL && IsWindowVisible(popup), popup != HWND.NULL && IsWindowEnabled(popup));
    }
}
