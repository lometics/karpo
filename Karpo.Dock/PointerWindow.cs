// What goes with the pointer, over everything: a block carried in edit mode (EditCarry), the selection
// rectangle a drag over the dim draws (EditBand), a tray icon dragged (TrayCarry) — one window kind, made
// here once (docs\widgets.md §363).
//
// Each moved and sized its window to what it showed at every pointer move, at once, while what it belongs
// to — the bar's ghost where the block would land, the selection's marks, the tray's gap — reached the
// screen with the frame's commit: the window a frame ahead of its own content and of the rest (the flicker
// design's R2 and R3). Now the window stands still, as a popup's does (§355): it spans every display, a px
// in from the outer edges of them all (no display wholly covered, which Windows takes for an app in full
// screen), placed once — again only when the displays change. What it shows is placed in it in
// composition (Content's offset), so a move is in the same commit as everything else the frame changed,
// and its region is where it shows (Surfaces.Region: grown at once to hold the old place and the new,
// shrunk to the new once the frame's commit has landed) — all DWM shows of it, all it could take a press on.
//
// It takes no activation and no pointer: the bar, the overflow or the dim the drag began on holds the
// mouse throughout. Its content is clear while it is out of sight (§359): it comes and goes in the commit
// of what it belongs to.

using System.Numerics;
using Karpo.Dock.Native;
using TerraFX.Interop.Windows;
using Windows.UI.Composition;
using Windows.UI.Composition.Desktop;
using static TerraFX.Interop.Windows.Windows;
using ICompositorDesktopInterop = TerraFX.Interop.WinRT.ICompositorDesktopInterop;
using IDesktopWindowTarget = TerraFX.Interop.WinRT.IDesktopWindowTarget;

namespace Karpo.Dock;

/// <summary>A window that goes with the pointer, standing still over every display (see the top of the file).</summary>
internal sealed unsafe class PointerWindow : IDisposable
{
    private readonly DesktopWindowTarget target;

    // Clear while out of sight; what the owner draws, at the corner of where it shows, stands in it.
    private readonly ContainerVisual root;
    private float opacity = 1f;
    private RECT frame;

    /// <param name="title">The window's title, never shown: what a window list or a test says it is.</param>
    public PointerWindow(Gfx gfx, HINSTANCE instance, string title)
    {
        fixed (char* className = Preview.ClassName)
        fixed (char* name = title)
        {
            Hwnd = CreateWindowExW(
                WS.WS_EX_TOOLWINDOW | WS.WS_EX_TOPMOST | WS.WS_EX_NOACTIVATE | WS.WS_EX_NOREDIRECTIONBITMAP | WS.WS_EX_TRANSPARENT,
                className,
                name,
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
        // It goes with the pointer from the first move: no transition of its own.
        BOOL on = BOOL.TRUE;
        _ = DwmSetWindowAttribute(Hwnd, (uint)DWMWINDOWATTRIBUTE.DWMWA_TRANSITIONS_FORCEDISABLED, &on, (uint)sizeof(BOOL));
        try
        {
            var c = gfx.Compositor;
            target = CreateTarget(c, Hwnd);
            root = c.CreateContainerVisual();
            Content = c.CreateContainerVisual();
            root.Children.InsertAtTop(Content);
            root.Opacity = 0f;
            target.Root = root;
        }
        catch
        {
            Surfaces.Destroy(Hwnd);
            throw;
        }
        // With the pointer, over everything (ZPlan).
        Surfaces.Enlist(Hwnd, Stratum.Carry, clear: true);
    }

    public HWND Hwnd { get; }

    /// <summary>What the owner draws: its (0, 0) at the corner of the area last shown.</summary>
    public ContainerVisual Content { get; }

    /// <summary>How see-through what it shows is while it shows: a tray icon carried is a little (TrayCarry).</summary>
    public float Opacity
    {
        get => opacity;
        set
        {
            opacity = value;
            if (root.Opacity > 0f)
            {
                root.Opacity = value;
            }
        }
    }

    /// <summary>Where the window stands on the screen (screen px), for a test.</summary>
    internal RECT Frame => frame;

    /// <summary>Tests only: how many times the window was placed.</summary>
    internal int FramesPlaced { get; private set; }

    /// <summary>
    /// In sight, in the frame's commit: <see cref="Content"/>'s corner at <paramref name="area"/>'s (screen px),
    /// the area what shows of it. The window placed only where it stands on no display that is there now.
    /// </summary>
    public void Show(RECT area)
    {
        var wanted = FrameOver(Displays());
        if (!Same(wanted, frame))
        {
            // Out of sight this costs nothing; in sight (a display come or gone mid-drag) it is the one move.
            if (Surfaces.Place(Hwnd, wanted.left, wanted.top, wanted.right - wanted.left, wanted.bottom - wanted.top))
            {
                frame = wanted;
                FramesPlaced++;
            }
        }
        Content.Offset = new Vector3(area.left - frame.left, area.top - frame.top, 0f);
        Surfaces.Region(Hwnd, new RECT
        {
            left = area.left - frame.left,
            top = area.top - frame.top,
            right = Math.Max(area.left + 1, area.right) - frame.left,
            bottom = Math.Max(area.top + 1, area.bottom) - frame.top,
        });
        root.Opacity = opacity;
        Surfaces.Show(Hwnd);
    }

    /// <summary>Clear in the frame's commit, out of sight once that is on the screen; then <paramref name="then"/>.</summary>
    public void Hide(Action? then = null)
    {
        root.Opacity = 0f;
        Surfaces.Hide(Hwnd, then);
    }

    /// <summary>Let go of once out of sight, <paramref name="closed"/> closing what the owner drew.</summary>
    public void Dispose(Action? closed)
    {
        Surfaces.Destroy(Hwnd, () =>
        {
            target.Root = null;
            closed?.Invoke();
            Content.Dispose();
            root.Dispose();
            target.Dispose();
        });
    }

    public void Dispose() => Dispose(null);

    /// <summary>
    /// The window's frame over <paramref name="displays"/> (screen px): the box round them all, a px in from
    /// each of its sides, so no display is wholly covered (§355: Windows takes a window covering a display for an
    /// app in full screen); one window over every display, so nothing it shows is cut where a display ends.
    /// </summary>
    internal static RECT FrameOver(IReadOnlyList<RECT> displays)
    {
        if (displays.Count == 0)
        {
            return new RECT { right = 1, bottom = 1 };
        }
        var box = new RECT
        {
            left = displays.Min(display => display.left),
            top = displays.Min(display => display.top),
            right = displays.Max(display => display.right),
            bottom = displays.Max(display => display.bottom),
        };
        return new RECT
        {
            left = box.left + 1,
            top = box.top + 1,
            right = Math.Max(box.left + 2, box.right - 1),
            bottom = Math.Max(box.top + 2, box.bottom - 1),
        };
    }

    /// <summary>The displays attached now, each's rectangle (screen px).</summary>
    private static List<RECT> Displays()
    {
        var found = new List<RECT>();
        var handle = System.Runtime.InteropServices.GCHandle.Alloc(found);
        try
        {
            _ = EnumDisplayMonitors(HDC.NULL, null, &Found, System.Runtime.InteropServices.GCHandle.ToIntPtr(handle));
        }
        finally
        {
            handle.Free();
        }
        return found;
    }

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static BOOL Found(HMONITOR monitor, HDC dc, RECT* rect, LPARAM data)
    {
        try
        {
            if (System.Runtime.InteropServices.GCHandle.FromIntPtr(data).Target is List<RECT> found)
            {
                found.Add(*rect);
            }
        }
        catch (Exception)
        {
            // Nothing may escape into the window manager's callback.
        }
        return BOOL.TRUE;
    }

    private static bool Same(RECT a, RECT b) => a.left == b.left && a.top == b.top && a.right == b.right && a.bottom == b.bottom;

    internal static DesktopWindowTarget CreateTarget(Compositor c, HWND hwnd)
    {
        var desktop = ComUtil.Query<ICompositorDesktopInterop>(c);
        try
        {
            IDesktopWindowTarget* raw = null;
            ComUtil.Check(desktop->CreateDesktopWindowTarget(hwnd, BOOL.TRUE, &raw));
            return ComUtil.Adopt<DesktopWindowTarget>(raw);
        }
        finally
        {
            desktop->Release();
        }
    }
}
