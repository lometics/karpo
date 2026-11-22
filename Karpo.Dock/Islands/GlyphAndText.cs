// Where a glyph and its text go in the room a widget has, so that they never
// meet.
//
// Nearly every widget on the bar is a glyph and a short piece of text: the
// battery's bolt and "100%", the stopwatch and "3h 16m", the cloud and "18°".
// The failure they all share is that the glyph is placed from one side and the
// text from the other, so at a narrow width they run into each other instead of
// one giving way.
//
// The rule here is that each gets room of its own, worked out from the room the
// cell has, and that where there is not room for both, the **glyph goes**: the
// text is the information and keeps the cell, the glyph is decoration. Neither
// is squeezed to make both fit — a squeezed glyph beside squeezed text reads
// worse than the text by itself.
//
// Both answers are positions inside the content rectangle, from its start; the
// caller adds its own padding (Fit.Edge). Because the text always begins a
// whole Fit.Gap past the end of the glyph, the two boxes cannot intersect, and
// Karpo.Tests\Sizing\GlyphAndTextTests.cs holds that over every width, glyph
// and text size the bar can produce.

using Karpo.Core;

namespace Karpo.Dock;

/// <summary>
/// A glyph and its text side by side along the dock (see <see cref="GlyphAndText.Row"/>).
/// </summary>
/// <param name="WithGlyph">False where the glyph gave way: only the text is drawn.</param>
/// <param name="GlyphAt">The glyph's box's start, from the content rectangle's start.</param>
/// <param name="TextAt">The text's start, a whole gap past the glyph where there is one.</param>
/// <param name="TextRoom">How wide the text may be drawn; longer text is cut with an ellipsis.</param>
internal readonly record struct GlyphRow(bool WithGlyph, float GlyphAt, float TextAt, float TextRoom);

/// <summary>
/// A glyph over its text across the dock (see <see cref="GlyphAndText.Stack"/>): down a vertical bar
/// only, the one place a taskbar entry stacks (docs\widgets.md §12.5).
/// </summary>
/// <param name="WithGlyph">False where the glyph gave way: only the text is drawn.</param>
/// <param name="GlyphTop">The glyph's box's top, from the cell's top.</param>
/// <param name="TextTop">The text's top, right under the glyph where there is one.</param>
/// <param name="TextRoom">How wide the text may be drawn.</param>
internal readonly record struct GlyphStack(bool WithGlyph, float GlyphTop, float TextTop, float TextRoom);

/// <summary>
/// Where a cell's content sits in the room the cell was given, along the bar —
/// so on a vertical bar <see cref="Start"/> is the top and <see cref="End"/>
/// the bottom, not left and right.
/// </summary>
internal enum CellPlace
{
    /// <summary>In the middle of the room: what a widget with one thing to show does.</summary>
    Centre,

    /// <summary>Against the start of the bar, so the room shows after it.</summary>
    Start,

    /// <summary>Against the end of the bar, so the room shows before it.</summary>
    End,
}

internal static class GlyphAndText
{
    /// <summary>
    /// Where <paramref name="content"/> px of content begins in <paramref name="room"/> px,
    /// placed <paramref name="place"/>. Never before the room starts, so content wider than its
    /// room begins at the start and is cut at the end rather than hanging off both.
    /// </summary>
    public static float Place(CellPlace place, float room, float content)
    {
        var spare = MathF.Max(0f, room - content);
        return place switch
        {
            CellPlace.Start => 0f,
            CellPlace.End => MathF.Floor(spare),
            _ => MathF.Floor(spare / 2f),
        };
    }

    /// <summary>
    /// The content length of a widget that is one glyph in a cell — a switch, the Wi-Fi button,
    /// a folder showing no name — so that it is <b>exactly one cell</b> on every bar.
    ///
    /// The room at its ends gives way before the widget takes a second cell: the full
    /// <see cref="Fit.Edge"/> first, then a sliver, and at worst the cell itself, since a glyph
    /// with a little air round it is what the widget is and a second cell of nothing is not. A
    /// glyph is never grown or shrunk to make this come out; the padding is.
    /// </summary>
    public static float Square(in IslandStyle style, in Fit fit, float glyph)
    {
        var cell = MathF.Max(1f, style.Cell);
        foreach (var pad in (ReadOnlySpan<float>)[MathF.Max(fit.Edge, fit.Px(6)), MathF.Max(1f, fit.Px(2))])
        {
            var wanted = glyph + (2 * pad);
            if (style.Cells(wanted) <= 1)
            {
                return wanted;
            }
        }
        // A glyph wider than its own cell: the cell is what it gets, and the
        // drawing centres the glyph in it rather than the widget growing.
        return cell;
    }

    /// <summary>
    /// A <paramref name="glyph"/> px glyph and text <paramref name="textWidth"/> px wide in
    /// <paramref name="room"/> px along the dock, centred in it. The glyph is drawn only where
    /// what is left after the text and the gap is at least its own box, so the two never share a
    /// pixel; without it the text has the room to itself.
    /// <para>
    /// <paramref name="textWidth"/> is the width of the text <b>being drawn</b>, never the width
    /// its shape reserved. A widget reserves the worst thing it can ever say so that its size
    /// never moves, and then draws something shorter: centring on the reservation would leave all
    /// of that slack on one side, which is how the screen time came to sit left in its cell.
    /// The room is the reservation; what goes in it is centred on its own ink.
    /// </para>
    /// </summary>
    public static GlyphRow Row(float room, in Fit fit, float glyph, float textWidth)
    {
        room = MathF.Max(0f, room);
        var text = MathF.Min(MathF.Max(textWidth, 0f), room);
        if (glyph <= 0f || room - text - fit.Gap < glyph)
        {
            // No room for both: the text keeps the cell, in the middle of it.
            return new GlyphRow(false, 0f, MathF.Floor((room - text) / 2), text);
        }
        var at = MathF.Floor((room - (glyph + fit.Gap + text)) / 2);
        return new GlyphRow(true, at, at + glyph + fit.Gap, text);
    }

    /// <summary>
    /// The same pair one over the other, in <paramref name="across"/> px across the dock and
    /// <paramref name="room"/> px along it: down a vertical bar, where a row would run across the
    /// bar's width. Across a horizontal one a pair is always a <see cref="Row"/>, at every height
    /// (docs\widgets.md §12.5). The glyph is drawn only where it and the text's line both fit across;
    /// without it the text is centred across the cell on its own.
    /// </summary>
    public static GlyphStack Stack(float across, float room, in Fit fit, float glyph, float textWidth, float textHeight)
    {
        across = MathF.Max(0f, across);
        var text = MathF.Min(MathF.Max(textWidth, 0f), MathF.Max(0f, room));
        if (glyph <= 0f || glyph + textHeight > across)
        {
            return new GlyphStack(false, 0f, MathF.Floor((across - textHeight) / 2f), text);
        }
        var top = MathF.Floor((across - (glyph + textHeight)) / 2f);
        return new GlyphStack(true, top, top + glyph, text);
    }
}
