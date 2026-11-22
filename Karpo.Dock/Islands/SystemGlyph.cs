// The status glyphs Windows' taskbar shows for the network, Bluetooth and the volume, drawn as it
// draws them: one drawing for the three folders' faces that wear them (FaceDrawings'
// StatusFaceDrawing), so they still look as one. It was the sound, Wi-Fi and Bluetooth buttons'
// island (SystemControlIsland); the buttons are folders' faces now (2026-10-10, the Wi-Fi,
// Bluetooth and sound presets), and this is what was left of it: the painting.
//
// A VPN connected is the network glyph's to say, as it is Windows' (the person,
// 2026-10-04: "same like Windows"): its shield badge (F8C0) over the network
// glyph cut away for it, at the glyph's own size and place. It is not a status
// icon of its own.

using Karpo.Core;
using Karpo.Dock.Native;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

internal static class SystemGlyph
{
    /// <summary>
    /// The tray's chevron for a bar on <paramref name="edge"/>, pointing the way its flyout opens,
    /// away from the bar: up from the bottom (E70E, Windows' own), down from the top, right from the
    /// left and left from the right (the person, 2026-10-13, on bars down the sides). Turned glyphs
    /// of the icon font, never a turned drawing.
    /// </summary>
    internal static string Chevron(Edge edge) => edge switch
    {
        Edge.Top => "\uE70D",
        Edge.Left => "\uE76C",
        Edge.Right => "\uE76B",
        _ => "\uE70E",
    };

    /// <summary>
    /// A system glyph as the taskbar draws it, centred by its ink in <paramref name="box"/>: the
    /// arcs a weak signal does not reach (<paramref name="back"/>) faded under it, dimmed where
    /// <paramref name="dim"/> (a radio that is off), and a VPN's shield (<paramref name="badge"/>)
    /// over it. The Wi-Fi, Bluetooth and sound faces' drawing.
    /// </summary>
    internal static unsafe void Paint(ID2D1DeviceContext* dc, Gfx gfx, in IslandStyle style, string glyph, string? back, bool dim, string? badge, D2D_RECT_F box)
    {
        var fit = new Fit(style);
        var ink = new IslandInk(style.Look);
        var size = fit.SystemIcon;
        var width = box.right - box.left;
        var height = box.bottom - box.top;
        dc->SetTextAntialiasMode(D2D1_TEXT_ANTIALIAS_MODE.D2D1_TEXT_ANTIALIAS_MODE_GRAYSCALE);
        ID2D1SolidColorBrush* brush = null;
        TextLine under = default;
        TextLine over = default;
        TextLine shield = default;
        TextLine uncut = default;
        try
        {
            // The taskbar's icons' colour, which the status icons beside these share (IslandInk.SystemIcon).
            var color = ink.SystemIcon;
            brush = Gfx.Brush(dc, color);
            // The Wi-Fi arcs a weak signal doesn't reach, dimmed under the ones it does, as Windows draws them.
            if (back is not null)
            {
                under = TextLine.Lay(gfx, back, size, Weight.Regular, null, gfx.IconFamily);
            }
            over = TextLine.Lay(gfx, glyph, size, Weight.Regular, null, gfx.IconFamily);
            // By the ink, as every other glyph of Karpo's is placed (Gfx.InkOrigin): these glyphs
            // share one advance and not one ink, so the cell's middle was giving the speaker at its
            // quietest a third of a glyph of air on its right and none on its left. Where there are
            // two layers they are placed by the fuller one, so the arcs a weak signal does not
            // reach sit exactly over the ones it does.
            var place = under.IsEmpty ? over : under;
            // With the VPN's shield on, by the glyph the cut-away one was cut from: its ink is the
            // icon's without the shield, so the arcs stay where they were and the shield takes the corner.
            if (badge is not null)
            {
                uncut = TextLine.Lay(gfx, SystemStatus.Uncut(back ?? glyph), size, Weight.Regular, null, gfx.IconFamily);
                place = uncut;
            }
            var origin = Karpo.Dock.Gfx.InkOrigin(place.Layout, width, height);
            var x = box.left + origin.x;
            var y = box.top + origin.y;
            if (!under.IsEmpty)
            {
                under.Draw(dc, brush, IslandInk.Faded(color, 0.3f), x, y);
            }
            over.Draw(dc, brush, dim ? ink.Disabled : color, x, y);
            // A VPN connected: Windows' shield, at the network glyph's own size and from its own origin,
            // which is where its font puts it (the em square's top-left corner, the glyph under it cut
            // away for it), in the icon's colour as Windows draws it.
            if (badge is not null)
            {
                shield = TextLine.Lay(gfx, badge, size, Weight.Regular, null, gfx.IconFamily);
                shield.Draw(dc, brush, color, x, y);
            }
        }
        finally
        {
            shield.Release();
            uncut.Release();
            over.Release();
            under.Release();
            ComUtil.Release(ref brush);
        }
    }
}
