// Text placed by its ink: what a list line's figures, a heading's first letter and a slider's level
// are measured and drawn with. A value is placed by where its ink falls rather than by its line box,
// which carries the font's ascent and descent — figures and glyphs would otherwise sit low.
//
// These were the grid cards' painters' own measures (GridCard, docs\widgets.md §16) until the grid
// folder went (2026-10-09); the lines use them still, and the card painters went with the grid.

using System.Numerics;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

/// <summary>Paints a picture into <paramref name="box"/> (px of the surface).</summary>
internal unsafe delegate void PictureDraw(ID2D1DeviceContext* dc, D2D_RECT_F box);

/// <summary>Text measured and drawn by its ink.</summary>
internal static unsafe class InkText
{
    /// <summary>How far a size steps down while it is fitted: a tenth, which is under what anyone sees jump.</summary>
    private const float Step = 0.92f;

    /// <summary>
    /// Where a line of text or a glyph puts its ink, from the origin its line is drawn at: left, top,
    /// right and bottom, in px.
    /// </summary>
    public static (float Left, float Top, float Right, float Bottom) Ink(Gfx gfx, string text, float px, Weight weight, string? family = null)
    {
        if (text.Length == 0)
        {
            return default;
        }
        var layout = gfx.TextLayout(text, px, weight, null, family);
        try
        {
            DWRITE_OVERHANG_METRICS overhang;
            if (layout->GetOverhangMetrics(&overhang).FAILED)
            {
                return default;
            }
            return (-overhang.left, -overhang.top, layout->GetMaxWidth() + overhang.right, layout->GetMaxHeight() + overhang.bottom);
        }
        finally
        {
            layout->Release();
        }
    }

    /// <summary>The size of <paramref name="text"/>'s ink at <paramref name="px"/>.</summary>
    public static Vector2 InkSize(Gfx gfx, string text, float px, Weight weight, string? family = null)
    {
        var ink = Ink(gfx, text, px, weight, family);
        return new Vector2(MathF.Max(0, ink.Right - ink.Left), MathF.Max(0, ink.Bottom - ink.Top));
    }

    /// <summary>Draws <paramref name="text"/> with its ink's top-left at (<paramref name="x"/>, <paramref name="y"/>).</summary>
    public static void AtInk(ID2D1DeviceContext* dc, Gfx gfx, string text, float px, Weight weight, DXGI_RGBA color, float x, float y, string? family = null)
    {
        if (text.Length == 0)
        {
            return;
        }
        var ink = Ink(gfx, text, px, weight, family);
        if (family is null)
        {
            PopupPaint.Text(dc, gfx, text, px, weight, color, x - ink.Left, y - ink.Top);
        }
        else
        {
            var size = InkSize(gfx, text, px, weight, family);
            PopupPaint.Glyph(dc, gfx, text, px, color, PopupPaint.Rect(x, y, size.X, size.Y));
        }
    }

    /// <summary>
    /// The fullest of <paramref name="forms"/> and the largest size, from <paramref name="most"/> down
    /// to <paramref name="least"/> px, at which its <b>template</b> fits <paramref name="width"/> ×
    /// <paramref name="height"/> by its ink; null where not even the shortest fits at the least.
    /// </summary>
    public static (int Form, float Px)? Fit(Gfx gfx, IReadOnlyList<(string Text, string Shape)> forms, Weight weight, float most, float least, float width, float height)
    {
        for (var form = 0; form < forms.Count; form++)
        {
            var px = MathF.Floor(MathF.Max(most, least));
            while (true)
            {
                var ink = InkSize(gfx, forms[form].Shape, px, weight);
                if (ink.X <= width && ink.Y <= height)
                {
                    return (form, px);
                }
                if (px <= least)
                {
                    break;
                }
                px = MathF.Max(least, MathF.Floor(px * Step));
            }
        }
        return null;
    }

    /// <summary>Whether a line of <paramref name="text"/> fits <paramref name="room"/> px whole at <paramref name="px"/>.</summary>
    public static bool Whole(Gfx gfx, string text, float px, Weight weight, float room) =>
        text.Length > 0 && PopupPaint.Measure(gfx, text, px, weight).X <= room;

    /// <summary>
    /// A plate a step off the panel, in either theme: under a row the pointer is on, or a square that
    /// stands in for a picture that is not there yet.
    /// </summary>
    public static DXGI_RGBA Shade(in TileStyle style) => style.Theme.Dark ? Gfx.Ink(1, 1, 1, 0.06f) : Gfx.Ink(0, 0, 0, 0.05f);
}
