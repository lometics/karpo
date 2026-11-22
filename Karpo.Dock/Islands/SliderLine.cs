// A slider's line in a list folder, once for every slider a list has: the volume (SoundPartTile) and
// the brightness (BrightnessIsland). The person, 2026-10-11: "the volume and brigness slider look
// different in terms of layout, this should not be." So the two ask one question here, and place
// the same controls — the quick settings' own button, slider and level (QuickSettings.Controls) —
// where the answer says, at every height a line is given:
//
//   * One row: the flyout's own volume row — the glyph's button on the leading gutter, the slider
//     filling the middle, the level in its field on the trailing gutter (SoundRows.Line).
//   * Two rows: the button, what the slider sets (the output, the word Brightness) and the level on
//     one row, the slider under them from where the words begin to the trailing gutter (§443).
//   * Taller: the button and what it sets at the top, the level large under them as a reading's
//     figure is (ListTiers.HeroPx), the slider under that, spread over the line up to a little
//     air over and between them — none under the slider, whose own box keeps the air under its
//     thumb, so the three stand in the middle of their cells (§442); a taller line leaves the rest
//     under them.
//
// Which one is what fits the height, the fullest first. Pure: the layout only, in the line's px.
//
// **The glyph stands in the list's icon column** (2026-10-16, the person: "the brigtness and volume
// icons should be aligned to be the same as the night light icon"): the button is centred on the
// column every switch's glyph is centred in (ListForm.IconBox), and its glyph is the list's glyph
// size (ListForm.Glyph), so the sun and the speaker stand over and under the moon, not 10 DIPs to
// its right. What the slider sets and the large level begin where the names do.
//
// **A page's ">" is the button's row's last control** (§443; the person, 2026-10-30: "the speakers widget layout seems
// off"): the volume's ">" to its outputs stood centred on the whole line, alone between the name's row and the
// slider's. It is placed here now, its middle the button's, the level and the name before it.
//
// **What stands before the ">" keeps the switches' lines' air from it** (§605; the person: "make sure that the buttons
// are properly spaced and have the proper spacing on the side as well"). The level and the name end
// SwitchIsland.ChevronAirDip before the ">"'s glyph, as a switch's line's words do (SwitchIsland.WordsEnd), not at the
// edge of the button round it: the level stood 9 DIP from the ">" there. The ">"'s glyph stands on the trailing gutter
// as every line's without a switch does, the button on the icon column; the gutters are the list's on both sides.

using Karpo.Dock.QuickSettings;
using Karpo.Dock.QuickSettings.Controls;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

/// <summary>How much of itself a slider's line shows (<see cref="SliderLine"/>).</summary>
internal enum SliderLineForm
{
    /// <summary>The flyout's one row: the button, the slider and the level.</summary>
    Row,

    /// <summary>The button, what it sets and the level on a row, the slider under them.</summary>
    Two,

    /// <summary>The button and what it sets, the level large, the slider.</summary>
    Tall,
}

internal static class SliderLine
{
    /// <summary>
    /// Where a slider's line puts its parts, in the line's px: the glyph's button, the slider, the
    /// level's field (none in the tall form, where the level is large), what the slider sets (none
    /// on one row) and the large level with its size.
    /// </summary>
    internal readonly record struct Boxes(
        SliderLineForm Form,
        D2D_RECT_F Button,
        D2D_RECT_F Slider,
        D2D_RECT_F Number,
        D2D_RECT_F Name,
        D2D_RECT_F Big,
        float BigPx)
    {
        /// <summary>The button's glyph, in px: the list's glyphs' size (ListForm.Glyph).</summary>
        public float GlyphPx { get; init; }

        /// <summary>The ">" to the line's page, the button's own row's trailing control; none where the line has no page.</summary>
        public D2D_RECT_F Page { get; init; }
    }

    /// <summary>
    /// The fullest form that fits a line <paramref name="width"/> by <paramref name="height"/> px, its rows standing
    /// between the list's gutters; with <paramref name="page"/> a ">" to the line's page as the last control of the
    /// button's row (§443).
    /// </summary>
    internal static Boxes Lay(Gfx gfx, QuickContext context, in TileStyle style, float width, float height, bool page = false)
    {
        var rowLeft = ListForm.Gutter(style);
        var rowRight = MathF.Max(rowLeft + 1, width - rowLeft);
        // The list's icon column, which starts on the gutter (rowLeft), and where its names begin.
        var form = ListForm.Of(gfx, style);
        var centre = rowLeft + (form.Icon / 2);
        var words = rowLeft + form.Icon + form.Inset;
        var row = MathF.Max(context.D(36), TilePaint.LineHeight(gfx, context.TextPx));
        var slider = context.D(36);
        var gap = context.D(4);
        // A slider's own row: from where the words begin to the trailing gutter, under the name, the level and the ">"
        // (§443: it ran from under the button and stopped short of the ">"); its thumb at either end touches the edge, never past.
        D2D_RECT_F Under(float top) => PopupPaint.Rect(words, top, MathF.Max(1, rowRight - words), slider);
        // The ">" on the button's row, and where the row's words and level end: the switches' lines' air before its glyph
        // (§605), else the trailing gutter.
        var copy = style;
        D2D_RECT_F PageOn(D2D_RECT_F button) => page ? PageAt(gfx, context, copy, width, height, (button.top + button.bottom) / 2) : default;
        var end = page ? MathF.Min(PageWordsEnd(style, width, height), PageAt(gfx, context, style, width, height, height / 2).left) : rowRight;

        var bigPx = ListTiers.HeroPx(style);
        var bigLine = TilePaint.LineHeight(gfx, bigPx, Weight.Semibold);
        // Air over the button, under it and under the level; the slider's box has its own under its thumb.
        var spare = (height - row - bigLine - slider) / 3;
        if (spare >= context.D(4))
        {
            spare = MathF.Min(spare, context.D(12));
            var top = PopupPaint.Round(spare);
            var bigTop = PopupPaint.Round(top + row + spare);
            var button = ButtonAt(context, top, row, centre);
            var chevron = PageOn(button);
            var levelLeft = MathF.Max(button.right + gap, words);
            return new Boxes(
                SliderLineForm.Tall,
                button,
                Under(MathF.Min(PopupPaint.Round(bigTop + bigLine + spare), MathF.Floor(height - slider))),
                default,
                NameAt(gfx, context, top, row, levelLeft, page ? end : end - gap),
                PopupPaint.Rect(levelLeft, bigTop, MathF.Max(1, rowRight - levelLeft), bigLine),
                bigPx) { GlyphPx = form.Glyph, Page = chevron };
        }
        if (row + gap + slider <= height + 0.5f)
        {
            var top = LineRows.Top(0, height, row + gap + slider, context.D(8));
            var button = ButtonAt(context, top, row, centre);
            var chevron = PageOn(button);
            var numberWidth = context.T(36);
            var number = PopupPaint.Rect(end - numberWidth, top, numberWidth, row);
            return new Boxes(
                SliderLineForm.Two,
                button,
                Under(top + row + gap),
                number,
                NameAt(gfx, context, top, row, MathF.Max(button.right + gap, words), number.left - gap),
                default,
                0) { GlyphPx = form.Glyph, Page = chevron };
        }
        // The page's row keeps its button 4 in from where it starts and its level's field 12 in from
        // where it ends; started and ended that much further out, both stand on the gutters (or the level before the ">").
        // The button moved onto the icon column, and the slider's start with it.
        var chevronOnRow = page ? PageAt(gfx, context, style, width, height, height / 2) : default;
        var line = SoundRows.Line(context, rowLeft - context.D(4), end + context.D(12), 0, height);
        var moved = OnColumn(line.Button, centre);
        var shift = moved.left - line.Button.left;
        var track = line.Slider with { left = line.Slider.left + shift };
        return new Boxes(SliderLineForm.Row, moved, track, line.Number, default, default, 0) { GlyphPx = form.Glyph, Page = chevronOnRow };
    }

    /// <summary>
    /// Where what stands before a line's ">" ends, px: <see cref="SwitchIsland.ChevronAirDip"/> before its glyph on the trailing
    /// gutter (<see cref="SwitchIsland.WordsEnd"/> of a line drawing no switch) — the air a switch's line's words keep (§605); at
    /// a large text, where the button round the ">" grows past that, short of the button (<see cref="Lay"/>).
    /// </summary>
    internal static float PageWordsEnd(in TileStyle style, float width, float height) =>
        SwitchIsland.WordsEnd(PopupPaint.Rect(0, 0, width, height), style, switched: false);

    /// <summary>
    /// The ">" to a line's page, its middle at <paramref name="middle"/>: the lines' one button round the switches' ">",
    /// whose glyph stands where a line with no switch has it, on the trailing gutter (SwitchIsland.ChevronGlyphBox; the
    /// person, 2026-10-16: "the angled icons should not have a blank space on the right of them if they dont have a
    /// toggle button to the right"), inside the line.
    /// </summary>
    private static D2D_RECT_F PageAt(Gfx gfx, QuickContext context, in TileStyle style, float width, float height, float middle)
    {
        var side = MathF.Min(LineButton.Of(gfx, context.Scale, context.TextScale).Height, height);
        var column = SwitchIsland.ChevronGlyphBox(PopupPaint.Rect(0, 0, width, height), style, switched: false);
        var top = Math.Clamp(PopupPaint.Round(middle - (side / 2)), 0, MathF.Max(0, height - side));
        return PopupPaint.Rect(MathF.Min(PopupPaint.Round(((column.left + column.right) / 2) - (side / 2)), width - side), top, side, side);
    }

    /// <summary>
    /// §432: how tall a slider's line is in <paramref name="size"/>, both sliders the same: the flyout's one row
    /// (Compact); the button, what it sets and the level over the slider (Medium); the level large between them,
    /// in the whole rows that hold it with a little air, which it spreads its parts over (Full).
    /// </summary>
    internal static float Height(Gfx gfx, QuickContext context, in TileStyle style, Karpo.Core.LineSize size)
    {
        var row = MathF.Max(context.D(36), TilePaint.LineHeight(gfx, context.TextPx));
        var slider = context.D(36);
        return size switch
        {
            Karpo.Core.LineSize.Compact => LineRows.Px(1, style),
            Karpo.Core.LineSize.Medium => MathF.Max(LineRows.Px(1, style), row + context.D(4) + slider),
            _ => LineRows.Whole(row + TilePaint.LineHeight(gfx, ListTiers.HeroPx(style), Weight.Semibold) + slider + (3 * context.D(4)), style),
        };
    }

    /// <summary>The button centred on the icon column at <paramref name="centre"/>, in the middle of a row <paramref name="row"/> px tall at <paramref name="top"/>.</summary>
    private static D2D_RECT_F ButtonAt(QuickContext context, float top, float row, float centre)
    {
        var button = context.D(36);
        // Square, so the words after it begin where every name does (§388).
        return OnColumn(PopupPaint.Rect(0, top + PopupPaint.Round((row - button) / 2), button, button), centre);
    }

    /// <summary><paramref name="button"/> moved across so that its middle, where its glyph is drawn, is <paramref name="centre"/>, on a whole px.</summary>
    private static D2D_RECT_F OnColumn(D2D_RECT_F button, float centre)
    {
        var width = button.right - button.left;
        var left = PopupPaint.Round(centre - (width / 2));
        return button with { left = left, right = left + width };
    }

    /// <summary>What the slider sets, beside the button to <paramref name="right"/>; none where there is no room.</summary>
    private static D2D_RECT_F NameAt(Gfx gfx, QuickContext context, float top, float row, float left, float right)
    {
        if (right - left <= 1)
        {
            return default;
        }
        var line = TilePaint.LineHeight(gfx, context.TextPx);
        return PopupPaint.Rect(left, top + PopupPaint.Round((row - line) / 2), right - left, line);
    }
}
