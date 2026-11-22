// Right to left, once for everything drawn (docs\widgets.md §36, "Right to left").
//
// Arabic and Urdu are read from the right, and so is a menu or a list in them: a row's icon stands
// at its right, its words after it to the left, a submenu's chevron at the left pointing left,
// what ends a row at its left. Nothing here is laid out twice for that. The layout stays the one
// it is, written from the left, and is mirrored where it is put on the screen:
//
//   * A menu (Flyout) places its rows' visuals as ever, then turns each across the panel
//     (Across): offset x becomes width − x − size, and so do the rows' boxes the pointer and the
//     keys find them by.
//   * A drawing painted with Direct2D (a folder's line) is painted under a mirroring transform
//     (Begin, End), so every fill, bar and plate lands mirrored; and every piece of text and every
//     picture is drawn through Text and Picture here, which turn it back about its own middle, so
//     its words read as words and only where it stands is mirrored. A glyph that points along the
//     line (a chevron, back and forward) is left mirrored, as Windows turns it (Turns). The
//     pointer is mirrored back into the line before the drawing's own code hears of it.
//
// What a line of text itself does — its words from the right, its ellipsis at the left — is
// DirectWrite's, which lays every line with a right-to-left letter in it right to left
// (Gfx.TextLayout, Languages.ReadsRightToLeft). Numbers, times and clock faces read left to right
// in every language: a mirrored line still writes "12:45" as "12:45".

using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

internal static unsafe class Mirror
{
    /// <summary>Whether Karpo's menus and a folder's lines are mirrored now: it speaks a right-to-left language.</summary>
    public static bool On => Karpo.Core.Languages.RightToLeft;

    /// <summary>
    /// How deep this thread is in a mirrored drawing (<see cref="Begin"/>): where it is, text and
    /// pictures are turned back about their own middle as they are drawn.
    /// </summary>
    [ThreadStatic]
    private static int depth;

    /// <summary>Whether a drawing on this thread is being mirrored (<see cref="Begin"/>).</summary>
    public static bool Drawing => depth > 0;

    /// <summary><paramref name="x"/> of something <paramref name="size"/> wide, turned across a <paramref name="width"/>-wide panel.</summary>
    public static float Across(float x, float size, float width) => width - x - size;

    /// <summary>
    /// The parts of the drawing on this thread kept upright (<see cref="Keep"/>), each by its span
    /// across, in the drawing's own px: what <see cref="End"/> hands back for the pointer.
    /// </summary>
    [ThreadStatic]
    private static List<(float From, float To)>? kept;

    /// <summary>
    /// A point's x in a <paramref name="width"/>-wide mirrored drawing, as the drawing's own code
    /// knows it: mirrored back, and turned back again inside a part kept upright (<paramref name="upright"/>,
    /// from <see cref="End"/>), whose own code knows it as it stands.
    /// </summary>
    public static float Back(float x, float width, IReadOnlyList<(float From, float To)>? upright = null)
    {
        var back = width - x;
        foreach (var (from, to) in upright ?? [])
        {
            if (back >= from && back < to)
            {
                return from + to - back;
            }
        }
        return back;
    }

    /// <summary>
    /// A box of a mirrored drawing, in the drawing's own px, as it stands on the screen: turned in the
    /// part kept upright it is in (<paramref name="upright"/>), then mirrored across the drawing.
    /// </summary>
    public static D2D_RECT_F Back(D2D_RECT_F box, float width, IReadOnlyList<(float From, float To)>? upright = null)
    {
        var middle = (box.left + box.right) / 2;
        foreach (var (from, to) in upright ?? [])
        {
            if (middle >= from && middle < to)
            {
                box = new(from + to - box.right, box.top, from + to - box.left, box.bottom);
                break;
            }
        }
        return new(width - box.right, box.top, width - box.left, box.bottom);
    }

    /// <summary>
    /// From here to <see cref="End"/>, what <paramref name="dc"/> draws between x =
    /// <paramref name="left"/> and <paramref name="right"/> lands mirrored across that span, text and
    /// pictures turned back to read. Gives the transform <see cref="End"/> puts back.
    /// </summary>
    public static D2D_MATRIX_3X2_F Begin(ID2D1DeviceContext* dc, float left, float right)
    {
        D2D_MATRIX_3X2_F was;
        dc->GetTransform(&was);
        var turned = Turned(was, left + right);
        dc->SetTransform(&turned);
        depth++;
        kept = [];
        return was;
    }

    /// <summary>
    /// The end of a mirrored drawing <see cref="Begin"/> started: <paramref name="was"/> is the
    /// transform again. Gives the parts it kept upright (<see cref="Keep"/>), for <see cref="Back(float, float, IReadOnlyList{ValueTuple{float, float}}?)"/>.
    /// </summary>
    public static IReadOnlyList<(float From, float To)> End(ID2D1DeviceContext* dc, D2D_MATRIX_3X2_F was)
    {
        depth = Math.Max(0, depth - 1);
        dc->SetTransform(&was);
        var upright = (IReadOnlyList<(float From, float To)>?)kept ?? [];
        kept = null;
        return upright;
    }

    /// <summary>
    /// <paramref name="layout"/> drawn at <paramref name="origin"/>: as it is, or, in a mirrored
    /// drawing, turned back about the middle of its own words so it reads, where the mirror put it.
    /// A glyph that points along the line (<paramref name="turns"/>) is left mirrored.
    /// </summary>
    public static void Text(ID2D1DeviceContext* dc, D2D_POINT_2F origin, IDWriteTextLayout* layout, ID2D1Brush* brush, D2D1_DRAW_TEXT_OPTIONS options, bool turns = false)
    {
        if (depth == 0 || turns)
        {
            dc->DrawTextLayout(origin, layout, brush, options);
            return;
        }
        DWRITE_TEXT_METRICS metrics;
        if (layout->GetMetrics(&metrics).FAILED)
        {
            dc->DrawTextLayout(origin, layout, brush, options);
            return;
        }
        // Whole pixels either side, so a line on a whole pixel stays on one once turned.
        var from = MathF.Floor(origin.x + metrics.left);
        var to = from + MathF.Ceiling(metrics.left - MathF.Floor(metrics.left) + metrics.width);
        var was = Upright(dc, from, to);
        try
        {
            dc->DrawTextLayout(origin, layout, brush, options);
        }
        finally
        {
            dc->SetTransform(&was);
        }
    }

    /// <summary>
    /// What is drawn between x = <paramref name="left"/> and <paramref name="right"/> from here to
    /// the transform given back is turned about its middle — in a mirrored drawing, a picture's
    /// way back to upright; otherwise nothing. The caller sets the transform it gives back.
    /// </summary>
    public static D2D_MATRIX_3X2_F Upright(ID2D1DeviceContext* dc, float left, float right)
    {
        D2D_MATRIX_3X2_F was;
        dc->GetTransform(&was);
        if (depth > 0)
        {
            var turned = Turned(was, left + right);
            dc->SetTransform(&turned);
        }
        return was;
    }

    /// <summary>
    /// A part of a mirrored drawing kept as it is, only its place mirrored: <paramref name="box"/>,
    /// turned back whole, and nothing in it turned again until <see cref="Resume"/>. What reads left
    /// to right in every language is drawn so — a clock's face, the media's previous, play and next
    /// — and a text box, whose caret and selection DirectWrite places from the left. The pointer
    /// in it is turned back with it (<see cref="End"/>). Outside a mirrored drawing it does nothing.
    /// </summary>
    public static (D2D_MATRIX_3X2_F Was, int Depth) Keep(ID2D1DeviceContext* dc, D2D_RECT_F box)
    {
        var state = (Upright(dc, box.left, box.right), depth);
        if (depth > 0 && box.right > box.left)
        {
            kept?.Add((box.left, box.right));
        }
        depth = 0;
        return state;
    }

    /// <summary>The end of a part <see cref="Keep"/> kept upright.</summary>
    public static void Resume(ID2D1DeviceContext* dc, (D2D_MATRIX_3X2_F Was, int Depth) state)
    {
        depth = state.Depth;
        var was = state.Was;
        dc->SetTransform(&was);
    }

    /// <summary>
    /// x → <paramref name="twice"/> − x, a mirror about x = <paramref name="twice"/> / 2, and then
    /// <paramref name="was"/>: the mirror in the drawing's own space.
    /// </summary>
    /// <summary>
    /// §589: what is drawn from here to the transform given back is turned a quarter clockwise about (<paramref name="cx"/>,
    /// <paramref name="cy"/>), on top of the transform in force: a glyph drawn turned (Karpo.Core.OptionGlyphs.Turned). The
    /// caller sets the transform it gives back.
    /// </summary>
    public static D2D_MATRIX_3X2_F Quarter(ID2D1DeviceContext* dc, float cx, float cy)
    {
        D2D_MATRIX_3X2_F was;
        dc->GetTransform(&was);
        var turned = QuarterOf(was, cx, cy);
        dc->SetTransform(&turned);
        return was;
    }

    /// <summary>§589: <paramref name="was"/> after a quarter turn clockwise about (<paramref name="cx"/>, <paramref name="cy"/>): (x, y) to (cx + cy − y, cy − cx + x), then <paramref name="was"/>.</summary>
    internal static D2D_MATRIX_3X2_F QuarterOf(D2D_MATRIX_3X2_F was, float cx, float cy)
    {
        var (dx, dy) = (cx + cy, cy - cx);
        return new D2D_MATRIX_3X2_F(
            was._21,
            was._22,
            -was._11,
            -was._12,
            (dx * was._11) + (dy * was._21) + was._31,
            (dx * was._12) + (dy * was._22) + was._32);
    }

    internal static D2D_MATRIX_3X2_F Turned(D2D_MATRIX_3X2_F was, float twice) => new(
        -was._11,
        -was._12,
        was._21,
        was._22,
        (twice * was._11) + was._31,
        (twice * was._12) + was._32);

    /// <summary>
    /// Segoe Fluent Icons glyphs that point a way along a line and so are mirrored with it, as
    /// Windows turns them: back and forward, the chevrons, the caret arrows, open in a new window,
    /// undo and redo, send and reply. Media transport (play, previous, next) keeps its direction in
    /// every language, and so does everything else.
    /// </summary>
    private static readonly HashSet<int> Pointing =
    [
        0xE72B, // Back
        0xE72A, // Forward
        0xE76B, // ChevronLeft
        0xE76C, // ChevronRight
        0xE973, // ChevronLeftSmall
        0xE974, // ChevronRightSmall
        0xEDD9, // CaretLeftSolid8
        0xEDDA, // CaretRightSolid8
        0xE8A7, // OpenInNewWindow
        0xE7A7, // Undo
        0xE7A6, // Redo
        0xE724, // Send
        0xE8CA, // MailReply
    ];

    /// <summary>Whether <paramref name="glyph"/> points along the line, and is mirrored with it (<see cref="Pointing"/>).</summary>
    public static bool Turns(string? glyph) =>
        glyph is { Length: > 0 } && System.Text.Rune.TryGetRuneAt(glyph, 0, out var rune) && rune.Utf16SequenceLength == glyph.Length && Pointing.Contains(rune.Value);
}
