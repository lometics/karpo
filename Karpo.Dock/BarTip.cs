// A bar's tooltip, in a window of its own (docs\widgets.md §359, §360). Since §358 a bar's window stands still
// at its edge's most thickness, the tooltips' room on its inner side, and what of it takes the pointer
// is its region: the bar's panel. But the region is also all DWM shows of the window (§355's spike), so a
// tooltip drawn in the bar's window, beside the bar, was cut off — nothing of it showed on the screen.
// The region cannot take the tooltip in either: it would take the pointer there, over whatever is under.
//
// So the scene draws its tooltip into this window's tree instead (Scene's tips): a window as large as
// the bar's and standing where it stands — placed with it, never moved while it shows —, in the tooltips'
// stratum (ZPlan), clear while out of sight, shown as the tooltip comes and hidden as it goes, the tooltip
// itself as the scene keeps it (§358: never faded in again by a rebuild).
//
// §360: its region is the tooltip (Scene.TipPlaced), as a popup's is its panel (§355): what it shows is
// all of it that takes the pointer. It stands over the bar, and WS_EX_TRANSPARENT, which §359 counted on,
// lets no pointer through a window that is not layered: whole, it took every press meant for the bar for
// as long as a tooltip was up — and one is up whenever the pointer rests on anything — so nothing on a bar
// could be opened or dragged (InputReachesTheBarsTests).

using TerraFX.Interop.Windows;
using Windows.UI.Composition;
using Windows.UI.Composition.Desktop;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

internal sealed unsafe class BarTip : IDisposable
{
    private readonly DesktopWindowTarget target;
    private bool shown;

    public BarTip(Gfx gfx, HINSTANCE instance)
    {
        fixed (char* className = Preview.ClassName)
        fixed (char* title = "Karpo tooltip") // not-ui: a window's title
        {
            Hwnd = CreateWindowExW(
                WS.WS_EX_TOOLWINDOW | WS.WS_EX_TOPMOST | WS.WS_EX_NOACTIVATE | WS.WS_EX_NOREDIRECTIONBITMAP,
                className,
                title,
                WS.WS_POPUP,
                0,
                0,
                1,
                1,
                HWND.NULL,
                HMENU.NULL,
                instance,
                null);
        }
        if (Hwnd == HWND.NULL)
        {
            throw new InvalidOperationException($"{nameof(CreateWindowExW)} failed: 0x{System.Runtime.InteropServices.Marshal.GetLastPInvokeError():X8}");
        }
        BOOL on = BOOL.TRUE;
        _ = DwmSetWindowAttribute(Hwnd, (uint)DWMWINDOWATTRIBUTE.DWMWA_TRANSITIONS_FORCEDISABLED, &on, (uint)sizeof(BOOL));
        try
        {
            target = PointerWindow.CreateTarget(gfx.Compositor, Hwnd);
            Root = gfx.Compositor.CreateContainerVisual();
            target.Root = Root;
        }
        catch
        {
            Surfaces.Destroy(Hwnd);
            throw;
        }
        Surfaces.Enlist(Hwnd, Stratum.Tip, clear: true);
    }

    public HWND Hwnd { get; }

    /// <summary>What the window shows: the scene's tooltip, where the bar's window would show it.</summary>
    public ContainerVisual Root { get; }

    /// <summary>
    /// The scene's tooltip stands at <paramref name="place"/> (the window's px), or has gone (null): the
    /// window's region there — grown at once, shrunk once the frame's content is on the screen (Surfaces) —,
    /// and the window with it as the frame ends: clear until its commit brings the tooltip, out of sight
    /// once the tooltip has gone (§359).
    /// </summary>
    public void At(RectF? place)
    {
        if (place is not { } rect)
        {
            if (shown)
            {
                shown = false;
                Surfaces.Hide(Hwnd);
            }
            return;
        }
        Surfaces.Region(Hwnd, new RECT
        {
            left = (int)MathF.Floor(rect.X),
            top = (int)MathF.Floor(rect.Y),
            right = (int)MathF.Ceiling(rect.Right),
            bottom = (int)MathF.Ceiling(rect.Bottom),
        });
        if (!shown)
        {
            shown = true;
            Surfaces.Show(Hwnd);
        }
    }

    /// <summary>Let go of with the frame, its content closed with it.</summary>
    public void Dispose() => Surfaces.Destroy(Hwnd, Close);

    /// <summary>
    /// The window let go of with the frame, its content left for <see cref="Close"/>: the bar's scene holds a
    /// part of it, and closes that first (DockWindow.Dispose).
    /// </summary>
    public void Go() => Surfaces.Destroy(Hwnd);

    /// <summary>What it showed, closed.</summary>
    public void Close()
    {
        target.Root = null;
        Root.Dispose();
        target.Dispose();
    }
}
