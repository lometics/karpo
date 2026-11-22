// The place a weather or air line is for, as every such line says it (docs\widgets.md §607; the person, 2026-11-09: "the
// weather widget should not show the location all the way at the top. it should display it smaller somewhere else").
//
// Never a heading: the place is a caption — the Caption's size, the secondary ink — under what the line is about, as
// Windows 11's own weather says where it is: small, after the weather. It leads its caption line, what else that line
// says after it with the kit's dot: the current weather's Compact "Zürich · Feels like 15° · High 18° · Low 8°", the
// air's "Zürich · European AQI", the current weather's Full under its hero "Zürich · 15 min ago" (Foot).

using TerraFX.Interop.DirectX;

namespace Karpo.Dock.Weather;

/// <summary>The place of a weather or air line: the first words of a caption (see the top of the file).</summary>
internal static class PlaceLabel
{
    /// <summary>What a piece of the place is written down as (LinePiece.Name).</summary>
    public const string Piece = "place";

    /// <summary>A caption that says the place first, then <paramref name="then"/>, joined by the kit's dot; the place alone where nothing follows.</summary>
    public static string Caption(string place, params string?[] then) => LineRow.Join([place, .. then]);

    /// <summary>
    /// The place as a caption line of its own from <paramref name="top"/> (px), where every name begins on a line across
    /// <paramref name="left"/> to <paramref name="right"/>; <paramref name="after"/> after it on its line in
    /// <paramref name="afterInk"/> (written down as <paramref name="afterPiece"/>) only where the place still reads whole
    /// beside it — the place wins. Its foot, px.
    /// </summary>
    public static float Foot(LineSketch sketch, float left, float right, float top, in TileStyle style, string place,
        string? after, DXGI_RGBA afterInk, string afterPiece)
    {
        var gfx = sketch.Gfx;
        var form = ListForm.Of(gfx, style);
        var px = style.Text(LineType.CaptionDip);
        var ink = style.Theme.TextSecondary;
        var text = left + form.TextLeft;
        var end = right - form.Inset;
        var drawn = sketch.Text(Piece, place, px, Weight.Regular, ink, text, top, MathF.Max(1, end - text));
        if (after is { Length: > 0 } && drawn.right > drawn.left)
        {
            var dot = PopupPaint.Measure(gfx, LineRow.Dot, px, Weight.Regular).X;
            var wide = PopupPaint.Measure(gfx, after, px, Weight.Regular).X;
            var whole = PopupPaint.Measure(gfx, place, px, Weight.Regular).X;
            if (text + whole + dot + wide <= end + 0.5f)
            {
                sketch.Text("place dot", LineRow.Dot, px, Weight.Regular, ink, drawn.right, top, MathF.Ceiling(dot) + 1);
                sketch.Text(afterPiece, after, px, Weight.Regular, afterInk, drawn.right + dot, top, MathF.Ceiling(wide) + 1);
            }
        }
        return top + TilePaint.LineHeight(gfx, px);
    }
}
