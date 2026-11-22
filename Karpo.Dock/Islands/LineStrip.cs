// A part of a line that moves on the compositor (docs\widgets.md §424; first the month's weeks, MonthTile): drawn
// once into a surface of its own, taller than the window it shows through, and slid there by the line itself — a
// property set's "y" it animates with the compositor's own key frames, the host's sprite bound to it by an
// expression — so a scroll is never a frame drawn on the UI thread. The host (FolderFlyout.DrawCard) asks where the
// window is and how big the strip is, draws the strip as it draws the line, the line leaving that part out of its
// own drawing (Hosting), and on its quick tick asks whether the line must be drawn again (Slid): a heading that
// changed, a strip that no longer holds what is on its way into view. Where nothing hosts it (a picture, the bar),
// the line draws the part itself where it stands.

using System.Numerics;
using TerraFX.Interop.DirectX;
using Windows.UI.Composition;

namespace Karpo.Dock;

/// <summary>Where a line's strip shows (line px, the line's box at 0,0) and how big its surface is (px).</summary>
internal readonly record struct LineStrip(D2D_RECT_F Window, Vector2 Size);

/// <summary>A line with a part that moves on the compositor (see the top of the file).</summary>
internal unsafe interface IStripLine
{
    /// <summary>The strip of a line drawn in <paramref name="box"/>: its window and its size; null where the line shows none.</summary>
    LineStrip? Strip(D2D_RECT_F box, in TileStyle style);

    /// <summary>The strip <see cref="Strip"/> last answered, drawn: its top left at 0,0.</summary>
    void PaintStrip(ID2D1DeviceContext* dc, in TileStyle style);

    /// <summary>
    /// What moves the strip: "top" − "y" is its offset in the window, which the host's sprite is bound to. Asked by
    /// the host that shows the strip; from then on the line glides on the compositor.
    /// </summary>
    CompositionPropertySet Motion(Compositor compositor);

    /// <summary>Set by the host round the line's own drawing: the strip is the host's to draw, and the line leaves it out.</summary>
    bool Hosting { set; }

    /// <summary>On the host's quick tick while the line is <see cref="CellIsland.Animating"/>, and after a wheel: true where the line must be drawn again.</summary>
    bool Slid();
}
