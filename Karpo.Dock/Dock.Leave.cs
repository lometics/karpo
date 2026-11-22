// §491: what of Karpo's closes when something is launched (Launch.cs): everything that is open — the
// folder, arranged or not (edit mode itself stays), a menu or a flyout, quick settings, the window
// list —, as Windows' taskbar puts away what it has open when it starts something.
// The launcher asks it of the dock before it hands anything to Windows; a launch off the dock's thread
// posts it (Launch.WM_KARPO_LEAVE), and the dock does it then.

using Karpo.Accounts;
using TerraFX.Interop.Windows;

namespace Karpo.Dock;

public sealed unsafe partial class Dock
{
    /// <summary>The launcher's way to close what is open here (<see cref="LeaveForLaunch"/>), kept so it can be taken back.</summary>
    private Action? leaving;

    /// <summary>The dock starts: every launch closes what of its own is open first, and a sign-in's browser is a launch.</summary>
    private void HookLaunches()
    {
        leaving = LeaveForLaunch;
        Launch.Leaves(leaving, host);
        OAuthClient.Browser = static url => Launch.Open(url);
        HookStart();
    }

    /// <summary>The dock goes: nothing of its is left for a launch to close.</summary>
    private void UnhookLaunches()
    {
        if (leaving is { } closing)
        {
            Launch.LeavesNot(closing);
            leaving = null;
        }
        UnhookStart();
    }

    /// <summary>
    /// Something is about to be launched: everything of Karpo's that is open closes — a menu or a flyout,
    /// the window list, quick settings and the folder (arranged too: <see cref="FolderCloser.Launch"/>).
    /// A folder closing gives the keys back to the window it had them from while it
    /// still has them, so what is launched, handed the right to come to the front, takes them after.
    /// </summary>
    private void LeaveForLaunch()
    {
        if (disposed)
        {
            return;
        }
        hoverOpen = null;
        CloseFlyout();
        if (preview is { IsOpen: true })
        {
            ClosePreview(previewOwner);
        }
        CloseIslandPopups(FolderCloser.Launch);
    }
}
