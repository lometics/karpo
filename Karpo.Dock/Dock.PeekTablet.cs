// peek and tablet: the dock's side of the desktop peek on the Show desktop corner (DesktopPeek,
// LivePeek) and of the bar at touch size in tablet posture (TabletPosture). The rules are in those
// files and tested there.

using Karpo.Core;

namespace Karpo.Dock;

public sealed unsafe partial class Dock
{
    /// <summary>tablet: whether the bars stand at touch size (<see cref="TabletPosture"/>), as last read.</summary>
    private bool touching = TabletPosture.Read();

    /// <summary>
    /// tablet: the config a bar along <paramref name="edge"/> of <paramref name="screen"/> is laid out
    /// with — the person's, at that edge's thickness and in its look (<see cref="Config.WithEdge"/>),
    /// grown to touch size in tablet posture.
    /// </summary>
    private Config SizedFor(ScreenEdges edges, Edge edge) => TabletPosture.Shown(config.WithEdge(edges[edge]), touching);

    /// <summary>tablet: the posture read again; true when it changed, so the bars are built again at their new size.</summary>
    private bool TouchChanged()
    {
        var now = TabletPosture.Read();
        if (now == touching)
        {
            return false;
        }
        touching = now;
        return true;
    }

    private sealed partial class DockWindow
    {
        /// <summary>peek: the pointer has rested on Show desktop for Windows' desktop peek time.</summary>
        private const uint DESKTOP_PEEK = 22;

        /// <summary>
        /// peek: the pointer is on <paramref name="hit"/>: on Show desktop, the peek waits for its
        /// rest where Windows' "Use Peek" is on; anywhere else, a desktop peek showing ends.
        /// </summary>
        private void DesktopPeekHover(Hit? hit)
        {
            if (hit is { Kind: HitKind.ShowDesktop } && LivePeek.DesktopEnabled)
            {
                Arm(DESKTOP_PEEK, Math.Max(1u, LivePeek.DesktopDelayMs));
                return;
            }
            Kill(DESKTOP_PEEK);
            LivePeek.EndDesktop();
        }

        /// <summary>peek: the rest is up: the desktop shows, the bar above it, while the pointer is still on Show desktop.</summary>
        private void DesktopPeekDue()
        {
            Kill(DESKTOP_PEEK);
            if (hovering && !Editing && hovered is { Kind: HitKind.ShowDesktop } && !dock.FlyoutOpen)
            {
                LivePeek.ShowDesktop(Hwnd);
            }
        }
    }
}
