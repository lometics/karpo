// Windows' "restart required" mark: the red dot its tray icon for a restart waiting wears over Windows
// Update's two arrows (WindowsUpdateImportant.ico, in C:\Windows\UUS\amd64), as the status icon for
// updates draws it (IndicatorsIsland).
//
// WHERE AND HOW BIG, read from the icon file at every size it carries (16 to 64 px): the arrows fill
// the icon's square, and they are Windows Update's glyph in Settings (E895); the dot is a circle half
// the square across, its middle three quarters of the way across and down, so it fills the square's
// bottom-right quarter to the edges; and the arrows are cut away about a sixteenth of the square
// clear of it, so the dot never touches them (1 px at the tray's 16). Karpo draws the glyph at the
// size of the square it is given and cuts it clear of the dot the same way (a layer whose mask is
// everything but the dot and its gap), then fills the dot.
//
// ITS COLOUR is Windows' critical red, SystemFillColorCritical (#C42B1C on light, #FF99A4 on dark),
// which is what the person asked for over the icon file's own pure red; in high contrast it follows
// the system's colours, the highlight every warning of Karpo's takes there (IslandInk.Alarm). The
// arrows stay in whatever ink the caller draws its glyphs in: the taskbar's primary text colour on
// the bar, as every status icon is.
//
// Windows' two other update icons have the same arrows and dot in other colours: orange
// (WindowsUpdateWarning.ico, a restart for drivers alone) and blue (WindowsUpdateSeeker.ico, updates
// waiting or under way). Those are the files' own colours (Ink(UpdateBadge, ...)); the status icon for
// updates wears whichever Windows would (WindowsUpdate.Badge).

using System.Numerics;
using Karpo.Dock.Native;
using Karpo.Dock.QuickSettings.Services;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

internal static unsafe class RestartDot
{
    /// <summary>Where the dot's middle is, across and down the icon's square, as a share of its side.</summary>
    internal const float Middle = 0.75f;

    /// <summary>How wide the dot is, as a share of the square's side: half of it.</summary>
    internal const float Across = 0.5f;

    /// <summary>How far the arrows are cut clear of the dot, as a share of the square's side: 1 px at 16.</summary>
    internal const float Gap = 1f / 16f;

    /// <summary>
    /// The dot for an icon drawn in <paramref name="box"/> (the largest square in it, in its middle):
    /// its middle, its radius, and how far from its middle the glyph under it is cut away.
    /// </summary>
    internal static (Vector2 Centre, float Radius, float Clear) In(D2D_RECT_F box)
    {
        var width = box.right - box.left;
        var height = box.bottom - box.top;
        var side = MathF.Min(width, height);
        var left = box.left + ((width - side) / 2);
        var top = box.top + ((height - side) / 2);
        var radius = side * Across / 2;
        return (new Vector2(left + (side * Middle), top + (side * Middle)), radius, radius + (side * Gap));
    }

    /// <summary>
    /// The square Windows draws its tray icon in, <paramref name="size"/> across, in the middle of a
    /// cell <paramref name="width"/> by <paramref name="height"/>: where the status icon's dot goes.
    /// </summary>
    internal static D2D_RECT_F Centred(float width, float height, float size) =>
        PopupPaint.Rect((width - size) / 2, (height - size) / 2, size, size);

    /// <summary>The dot's colour on the bar: Windows' critical red, the system's highlight in high contrast.</summary>
    internal static DXGI_RGBA Ink(in IslandInk ink) => ink.Critical;

    /// <summary>
    /// The orange of Windows' WindowsUpdateWarning.ico and the blue of its WindowsUpdateSeeker.ico,
    /// read from the files (#FC9A03, #0078D7): the same on light and dark, as the files are.
    /// </summary>
    internal static readonly DXGI_RGBA Orange = Gfx.Ink(0xFC / 255f, 0x9A / 255f, 0x03 / 255f, 1f);

    internal static readonly DXGI_RGBA Blue = Gfx.Ink(0x00 / 255f, 0x78 / 255f, 0xD7 / 255f, 1f);

    /// <summary>
    /// The dot for Windows Update's <paramref name="badge"/> on the bar, or null for none: red as
    /// above for Important, Windows' orange for Warning and its blue for Seeker; every one the
    /// system's highlight in high contrast, as every warning of Karpo's is there.
    /// </summary>
    internal static DXGI_RGBA? Ink(UpdateBadge badge, in IslandInk ink) => badge switch
    {
        UpdateBadge.Important => ink.Critical,
        UpdateBadge.Warning => ink.HighContrast ? ink.Critical : Orange,
        UpdateBadge.Seeker => ink.HighContrast ? ink.Critical : Blue,
        _ => null,
    };

    /// <summary>The same in a folder, for the folder's theme.</summary>
    internal static DXGI_RGBA? Ink(UpdateBadge badge, FlyoutTheme theme) => badge switch
    {
        UpdateBadge.Important => Ink(theme),
        UpdateBadge.Warning => theme.HighContrast ? Ink(theme) : Orange,
        UpdateBadge.Seeker => theme.HighContrast ? Ink(theme) : Blue,
        _ => null,
    };

    /// <summary>The dot's colour in a folder: the same red for the folder's theme.</summary>
    internal static DXGI_RGBA Ink(FlyoutTheme theme) =>
        theme.HighContrast ? PopupPaint.Ink(theme.Accent) : theme.Dark ? Gfx.Ink(1f, 0x99 / 255f, 0xA4 / 255f, 1f) : Gfx.Ink(0xC4 / 255f, 0x2B / 255f, 0x1C / 255f, 1f);

    /// <summary>
    /// Paints <paramref name="glyph"/> at the size of <paramref name="box"/>, by its ink in the middle
    /// of it, in <paramref name="ink"/>; and with a <paramref name="dot"/> colour, the dot in Windows'
    /// place over it, the glyph cut clear of it.
    /// </summary>
    internal static void Paint(ID2D1DeviceContext* dc, Gfx gfx, D2D_RECT_F box, string glyph, DXGI_RGBA ink, DXGI_RGBA? dot)
    {
        var px = MathF.Min(box.right - box.left, box.bottom - box.top);
        if (dot is not { } red)
        {
            PopupPaint.Glyph(dc, gfx, glyph, px, ink, box);
            return;
        }
        var (centre, radius, clear) = In(box);
        ID2D1Factory* factory = null;
        ID2D1RectangleGeometry* all = null;
        ID2D1EllipseGeometry* hole = null;
        ID2D1GeometryGroup* mask = null;
        var layered = false;
        try
        {
            dc->GetFactory(&factory);
            // Everything the glyph could reach, with the dot and its gap taken out: a rectangle and a
            // circle in it, filled alternately, leave the circle a hole.
            var bounds = new D2D_RECT_F(box.left - px, box.top - px, box.right + px, box.bottom + px);
            var ellipse = new D2D1_ELLIPSE { point = new D2D_POINT_2F(centre.X, centre.Y), radiusX = clear, radiusY = clear };
            if (factory != null
                && factory->CreateRectangleGeometry(&bounds, &all).SUCCEEDED
                && factory->CreateEllipseGeometry(&ellipse, &hole).SUCCEEDED)
            {
                var parts = stackalloc ID2D1Geometry*[2] { (ID2D1Geometry*)all, (ID2D1Geometry*)hole };
                if (factory->CreateGeometryGroup(D2D1_FILL_MODE.D2D1_FILL_MODE_ALTERNATE, parts, 2, &mask).SUCCEEDED)
                {
                    var parameters = new D2D1_LAYER_PARAMETERS1
                    {
                        contentBounds = bounds,
                        geometricMask = (ID2D1Geometry*)mask,
                        maskAntialiasMode = D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_PER_PRIMITIVE,
                        maskTransform = D2D_MATRIX_3X2_F.Identity,
                        opacity = 1,
                        opacityBrush = null,
                        layerOptions = D2D1_LAYER_OPTIONS1.D2D1_LAYER_OPTIONS1_NONE,
                    };
                    dc->PushLayer(&parameters, null);
                    layered = true;
                }
            }
            PopupPaint.Glyph(dc, gfx, glyph, px, ink, box);
        }
        finally
        {
            if (layered)
            {
                dc->PopLayer();
            }
            ComUtil.Release(ref mask);
            ComUtil.Release(ref hole);
            ComUtil.Release(ref all);
            ComUtil.Release(ref factory);
        }
        PopupPaint.Circle(dc, centre, radius, red);
    }
}
