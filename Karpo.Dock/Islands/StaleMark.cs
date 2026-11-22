// The mark of data that could not be refreshed: the calendar's and the mail's
// (docs\accounts.md, "Offline"). When an account's last read failed and what it
// gave before is still shown, every place that shows it says so the same way —
// Segoe's sync-error glyph, small and in the secondary ink, and over it the
// tooltip "Last updated 14:05 — couldn't refresh" (Freshness.Tip):
//
//   * a line of a list: at its trailing gutter — on the row of a one-row line,
//     at the top of a taller one — with the line's content kept clear of it, and
//     its tooltip over it (CellIsland.CardTip);
//   * a face on the bar (the mail's envelope): on the glyph's lower corner, as the
//     unread badge is on its upper one (GlyphBadge); no tooltip there, as over no
//     face (docs\widgets.md §367), the words said to a screen reader instead;
//   * a flyout: the same words as its footer's note.
//
// Nothing changes size: the mark takes room only from inside the line, and a
// line that is not stale lays out exactly as before.

using System.Numerics;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

internal static class StaleMark
{
    /// <summary>Segoe Fluent Icons: SyncError, the sync arrows with a stop.</summary>
    internal const string Glyph = "";

    /// <summary>The name of its piece in a line's sketch (ILinePieces).</summary>
    internal const string Piece = "stale";

    /// <summary>The glyph's size in a line: the list's small text.</summary>
    public static float Size(ListForm form) => form.SmallPx;

    /// <summary>What it takes from the trailing edge of a line that shows it: the glyph and a gap; nothing for a line that does not.</summary>
    public static float Room(in TileStyle style, ListForm form, bool stale) => stale ? Size(form) + style.Gap : 0;

    /// <summary>
    /// The mark at the trailing gutter of <paramref name="box"/>, in the middle of the band from
    /// <paramref name="top"/> that is <paramref name="height"/> tall: the row it stands beside.
    /// </summary>
    public static void Sketch(LineSketch sketch, D2D_RECT_F box, in TileStyle style, ListForm form, float top, float height)
    {
        var size = Size(form);
        var right = box.right - form.Inset;
        var at = PopupPaint.Rect(right - size, top + PopupPaint.Round((height - size) / 2), size, size);
        sketch.Glyph(Piece, Glyph, size, style.Theme.TextSecondary, at);
    }

    /// <summary>
    /// The tooltip of a line whose sketch is <paramref name="pieces"/>, where <paramref name="point"/>
    /// is on its mark (with a little air round it, as small a target as it is): <paramref name="tip"/>
    /// and the mark's box. Null off the mark, or with nothing to say.
    /// </summary>
    public static (string Text, D2D_RECT_F Part)? Tip(IReadOnlyList<LinePiece> pieces, Vector2 point, string? tip, in TileStyle style)
    {
        if (tip is null)
        {
            return null;
        }
        foreach (var piece in pieces)
        {
            if (piece.Name != Piece)
            {
                continue;
            }
            var air = style.Px(6);
            var box = piece.Box;
            if (point.X >= box.left - air && point.X < box.right + air && point.Y >= box.top - air && point.Y < box.bottom + air)
            {
                return (tip, box);
            }
        }
        return null;
    }
}
