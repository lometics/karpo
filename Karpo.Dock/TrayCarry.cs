// tray: the notification icon being dragged, under the pointer wherever on the screen it is, as
// Windows 11 carries a tray icon: across the bar and up into the overflow, in both directions. A
// bar's window is its strip along the edge and the overflow is a window of its own, so neither can
// draw it on the way between them; it is drawn in a window of its own that goes with the pointer
// (TrayCarrying.Corner, PointerWindow), as a widget carried in edit mode is (EditCarry). The bar and the overflow
// keep its place out of sight meanwhile and open the gap where it would land.
//
// The window takes no activation and no pointer: the bar or the overflow the drag began in holds the
// mouse throughout.

using System.Numerics;
using TerraFX.Interop.Windows;
using Windows.UI.Composition;

namespace Karpo.Dock;

/// <summary>The window a dragged tray icon is drawn in, at the pointer (see the top of the file).</summary>
internal sealed unsafe class TrayCarry : IDisposable
{
    private readonly Gfx gfx;
    private readonly PointerWindow window;
    private readonly SpriteVisual picture;
    private CompositionDrawingSurface? surface;
    private CompositionSurfaceBrush? brush;

    // What the picture was drawn from, so a move draws nothing again.
    private (Pixels? Pixels, int Size) drawn;

    public TrayCarry(Gfx gfx, HINSTANCE instance)
    {
        this.gfx = gfx;
        // tray: the icon carried is a little see-through, as one lifted on the bar was (Scene).
        window = new PointerWindow(gfx, instance, "Karpo tray carry") { Opacity = 0.9f }; // not-ui: a window's title
        picture = gfx.Compositor.CreateSpriteVisual();
        window.Content.Children.InsertAtTop(picture);
    }

    public HWND Hwnd => window.Hwnd;

    /// <summary>
    /// Shows <paramref name="pixels"/> in a <paramref name="size"/> px square held by its middle at
    /// <paramref name="pointer"/> (screen px), moved in the frame's commit with the gap where it would land
    /// (§363). An icon with no picture shows nothing: its gap still says where it would land.
    /// </summary>
    public void Show(Pixels? pixels, int size, POINT pointer)
    {
        if (pixels is null || size <= 0)
        {
            Hide();
            return;
        }
        if (!ReferenceEquals(drawn.Pixels, pixels) || drawn.Size != size)
        {
            var next = gfx.Icon(pixels, size);
            var nextBrush = gfx.Compositor.CreateSurfaceBrush(next);
            picture.Brush = nextBrush;
            picture.Size = new Vector2(size);
            brush?.Dispose();
            surface?.Dispose();
            (surface, brush, drawn) = (next, nextBrush, (pixels, size));
        }
        var at = TrayCarrying.Corner(pointer, size);
        window.Show(new RECT { left = at.x, top = at.y, right = at.x + size, bottom = at.y + size });
    }

    public void Hide() => window.Hide();

    public void Dispose() => window.Dispose(() =>
    {
        picture.Brush = null;
        brush?.Dispose();
        surface?.Dispose();
        picture.Dispose();
    });
}
