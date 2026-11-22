// The strip of screen a bar holds for everyone else: what maximized windows
// stop at, and what the shell takes out of the work area when the dock
// registers it (ABM_SETPOS, see AppbarHolder, which also checks it is held). The three decisions
// worth keeping apart from the Win32 calls live here: how thick that strip is
// for each way an edge shows, where it lies on a display's edge, and which messages
// the shell has to be sent to get from the strip it holds now to that one.

using Karpo.Core;
using TerraFX.Interop.Windows;

namespace Karpo.Dock;

/// <summary>
/// One thing the shell has to be told. <see cref="AppbarSpace.Plan"/> puts them in the order
/// they must be sent.
/// </summary>
internal enum AppbarStep
{
    /// <summary>ABM_REMOVE then ABM_NEW: registered afresh, holding nothing.</summary>
    Register,

    /// <summary>ABM_QUERYPOS then ABM_SETPOS: the strip this bar holds, which may be empty.</summary>
    Position,
}

internal static class AppbarSpace
{
    /// <summary>
    /// How thick the strip is in physical pixels for a bar <paramref name="thickness"/> px
    /// thick on an edge shown as <paramref name="mode"/> says: the whole bar where it is always there
    /// and holds its space, and nothing at all otherwise — a bar shown over the windows leaves the
    /// display to them, and so does one that comes in on hover, lying over them when it comes.
    /// </summary>
    public static int Thickness(EdgeMode mode, float thickness) =>
        mode == EdgeMode.Reserve ? (int)MathF.Ceiling(thickness) : 0;

    /// <summary>Whether <paramref name="rc"/> is a strip of no thickness, which holds nothing.</summary>
    public static bool IsEmpty(RECT rc) => rc.right <= rc.left || rc.bottom <= rc.top;

    /// <summary>
    /// What the shell has to be told for this bar to hold a strip <paramref name="thickness"/> px
    /// thick on <paramref name="edge"/>, given what it holds now, in the order the messages go out.
    /// Empty when there is nothing to say.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The shell records which side of the display an appbar is on when it registers. Handing an
    /// already-registered appbar a rectangle on another side (ABM_SETPOS with a different uEdge)
    /// does not reliably move the strip: the old side stays out of the work area and the new side
    /// reserves nothing — a bar moved from the bottom to the right left a gap along the bottom and
    /// let maximized windows run underneath it on the right. So a <b>move</b> registers again.
    /// </para>
    /// <para>
    /// <b>Giving the space back does not.</b> It used to: a bar that started hiding
    /// (<paramref name="thickness"/> 0) was removed and registered again, because ABM_REMOVE gives
    /// the strip back for certain. It does — but ABM_NEW is refused now and then, and a bar whose
    /// registration the shell would not take back holds nothing, hears nothing, and sends nothing:
    /// switching back to always-there then found no appbar to set a position on, so the work area
    /// was never worked out again and maximized windows kept the height they had while the bar was
    /// hiding. ABM_SETPOS with a strip of no thickness gives the space back just as completely, and
    /// the registration — with it the full-screen and position notices, and the right to take the
    /// space again — is never put at risk. So the round trip never-hide → auto-hide → never-hide
    /// touches the registration not at all.
    /// </para>
    /// </remarks>
    /// <param name="registered">The shell knows this bar (ABM_NEW was accepted).</param>
    /// <param name="positioned">
    /// It has been given a strip on <paramref name="held"/>, which may be the empty one a hiding
    /// bar holds. What the shell was last <i>told</i>, not what it holds, because the side it was
    /// told is the side it keeps the appbar on.
    /// </param>
    /// <param name="held">The side of that strip; meaningless unless <paramref name="positioned"/>.</param>
    /// <param name="edge">The side it is to be on.</param>
    /// <param name="thickness">How thick the strip is to be; 0 to hold nothing.</param>
    public static AppbarStep[] Plan(bool registered, bool positioned, Edge held, Edge edge, int thickness)
    {
        // Registered even while it hides: that is how the bar hears about full-screen apps.
        // A move registers again whether the bar holds space or not, since what the shell will
        // not do is move an appbar off the side it was registered on.
        if (!registered || (positioned && held != edge))
        {
            // Registering gives the old strip back on the way, so a bar that wants none is done.
            return thickness == 0 ? [AppbarStep.Register] : [AppbarStep.Register, AppbarStep.Position];
        }
        // Never positioned and wanting nothing: there is nothing the shell has to be told.
        // (Positioned and wanting nothing is a position of the empty strip, which is a no-op
        // once it is already the empty strip — Reserve sends only a change.)
        return !positioned && thickness == 0 ? [] : [AppbarStep.Position];
    }

    /// <summary>
    /// <paramref name="rc"/> trimmed to a strip <paramref name="thickness"/> px thick
    /// along <paramref name="edge"/>, flush with that side of it. A thickness of 0 gives an
    /// empty strip on that side, which is what holds nothing (<see cref="Plan"/>).
    /// </summary>
    public static RECT Fit(RECT rc, Edge edge, int thickness)
    {
        switch (edge)
        {
            case Edge.Top:
                rc.bottom = rc.top + thickness;
                break;
            case Edge.Left:
                rc.right = rc.left + thickness;
                break;
            case Edge.Right:
                rc.left = rc.right - thickness;
                break;
            default:
                rc.top = rc.bottom - thickness;
                break;
        }
        return rc;
    }
}
