// A list line at whatever height it is given (docs\widgets.md §9, "A line at every height").
//
// The user, of the list folders: "the grid ones have been optimized very well for all the different
// sizes. for the list view widgets they have not been optimized ad all. they should also be
// optimized to look good from 1 to 6 cell heights (maybe even higher depending on the type of
// widget). they should keep their functionality for every size if possible." A line used to be one
// fixed drawing: given less than it measured it was cut at its edge, given more it left the rest
// empty. Now each height is designed, not stretched:
//
//   * One row: one line — the glyph, the name and the main reading, perhaps a sparkline or a bar
//     between them, and at most one control.
//   * Two and three rows: the second readings, and a compact graph or bar.
//   * Four rows and up: the whole widget — its history, each item's detail — filling the height,
//     with no large empty regions.
//   * Past six, a line with items to list shows more of them, not bigger type.
//
// Which of those a line draws is its own question, answered by what its pieces need at the text
// size in force (ListTiers.Of): the fullest that fits, so a line whose whole widget fits three rows
// shows it from three, and a line of one row at the largest text is still a line of one row. On one
// row the reading wins and the name takes what is left (ListTiers.Row, ListTiers.Name).
// This file holds what every such line shares — the tiers, the sizes that grow modestly with the
// height, and the one-row line's pieces on the list's edges — so the lines of a
// folder read as one design. It is added beside ListForm, which it builds on and does not change.

using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

/// <summary>How much of its widget a list line shows at the height it was given.</summary>
internal enum ListTier
{
    /// <summary>One row: the glyph, the name and the main reading on one line, and at most one control.</summary>
    One,

    /// <summary>The compact line, from two rows: the second readings, and a compact graph or bar.</summary>
    Compact,

    /// <summary>The whole widget, filling the height: from three to five rows at the list's text, as its pieces fit.</summary>
    Full,
}

/// <summary>The sizes a list line is drawn at, for its tier and its height (see the note at the top of this file).</summary>
internal static class ListTiers
{
    /// <summary>
    /// The tier a line <paramref name="height"/> px tall draws: the fullest whose pieces fit, given
    /// how much each needs at the text size in force — <paramref name="compact"/> for the second
    /// readings and a compact picture, <paramref name="full"/> for the whole widget.
    /// </summary>
    public static ListTier Of(float height, float compact, float full) =>
        height + 0.5f >= full ? ListTier.Full : height + 0.5f >= compact ? ListTier.Compact : ListTier.One;

    /// <summary>The main reading's size on one row and in a compact line: the kit's Value, 20 DIP (<see cref="LineType"/>).</summary>
    public static float ValuePx(in TileStyle style) => style.Text(LineType.ValueDip);

    /// <summary>
    /// The main reading's size in a line showing the whole widget: the kit's Hero, 28 DIP, at every
    /// height (§388: a taller line shows more, never bigger type).
    /// </summary>
    public static float HeroPx(in TileStyle style) => style.Text(LineType.HeroDip);

    /// <summary>
    /// The main reading's size in a line showing the whole widget, where the rest of what it shows
    /// takes <paramref name="rest"/> px of its <paramref name="height"/>: <see cref="HeroPx(in TileStyle)"/>,
    /// or <see cref="ValuePx"/> — the size a line's fit for the tier is reckoned at — where the Hero's
    /// line leaves it no room. At the largest text a line so reaches the whole widget with its figure
    /// a step smaller, rather than stopping at the compact line and leaving the height empty; never a
    /// size between the two (§388).
    /// </summary>
    public static float HeroPx(Gfx gfx, in TileStyle style, float height, float rest) =>
        TilePaint.LineHeight(gfx, HeroPx(style), Weight.Semibold) > height - rest + 0.5f ? ValuePx(style) : HeroPx(style);

    /// <summary>
    /// A time that is the widget, in a line showing the whole of it (a timer, a stopwatch, an alarm,
    /// the clock's header): the kit's Display, 40 DIP, where its line fits what the rest leaves it, else
    /// <see cref="HeroPx(Gfx, in TileStyle, float, float)"/> (§388).
    /// </summary>
    public static float DisplayPx(Gfx gfx, in TileStyle style, float height, float rest) =>
        TilePaint.LineHeight(gfx, style.Text(LineType.DisplayDip), Weight.Semibold) > height - rest + 0.5f
            ? HeroPx(gfx, style, height, rest) : style.Text(LineType.DisplayDip);

    /// <summary>
    /// What a line calls its subject in <paramref name="room"/> px at <paramref name="px"/>: the first
    /// of <paramref name="forms"/>, fullest first, that fits whole; else the last, cut with an
    /// ellipsis, where at least its first four letters still show; else nothing. A name cut to a
    /// letter or two says nothing, and the room it would take is the reading's.
    /// </summary>
    public static string? Name(Gfx gfx, IReadOnlyList<string> forms, float px, Weight weight, float room)
    {
        if (Whole(gfx, forms, px, weight, room) is { } whole)
        {
            return whole;
        }
        var last = forms.LastOrDefault(form => form.Length > 0);
        if (last is null)
        {
            return null;
        }
        var letters = new System.Globalization.StringInfo(last);
        var head = letters.SubstringByTextElements(0, Math.Min(4, letters.LengthInTextElements)).TrimEnd();
        return PopupPaint.Measure(gfx, head + "…", px, weight).X <= room ? last : null;
    }

    /// <summary>The first of <paramref name="forms"/>, fullest first, that fits <paramref name="room"/> px whole at <paramref name="px"/>; none where none does.</summary>
    public static string? Whole(Gfx gfx, IReadOnlyList<string> forms, float px, Weight weight, float room)
    {
        foreach (var form in forms)
        {
            if (form.Length > 0 && PopupPaint.Measure(gfx, form, px, weight).X <= room)
            {
                return form;
            }
        }
        return null;
    }

    /// <summary>
    /// A glyph standing for the widget in the leading column: the list's own (the kit's Row, 16 DIP) on
    /// one row and in the compact line, the Feature's 24 beside the whole widget's Hero (§388).
    /// </summary>
    public static float GlyphPx(in ListForm form, in TileStyle style, ListTier tier) =>
        tier == ListTier.Full ? LineGlyph.Feature(style) : form.Glyph;

    /// <summary>
    /// The box a glyph <paramref name="px"/> px across (<see cref="GlyphPx"/>) is centred in, on a row
    /// that starts at <paramref name="top"/> and is <paramref name="height"/> px tall, in a line whose
    /// box starts at <paramref name="left"/>: the list's leading column, moved in by half of what the
    /// glyph is wider than it — so a glyph larger than the column grows into the room before the text,
    /// and never into the gutter (docs\widgets.md §9, "One gutter").
    /// </summary>
    public static D2D_RECT_F GlyphBox(in ListForm form, float left, float top, float height, float px)
    {
        var column = form.IconBox(left, top, height);
        var grow = MathF.Max(0, PopupPaint.Round((px - form.Icon) / 2));
        return new D2D_RECT_F(column.left + grow, column.top, column.right + grow, column.bottom);
    }

    /// <summary>A meter's thickness: the kit's one, 4 DIP, a pill, at every height (§388).</summary>
    public static float MeterPx(in TileStyle style) => MathF.Max(2, style.Px(4));

    /// <summary>The least a graph or a bar is worth drawing at beside a one-row line's words, in px.</summary>
    public static float LeastPicture(in TileStyle style) => style.Px(32);

    /// <summary>
    /// Where a one-row line's pieces go, on the list's own edges: the glyph's column on the leading
    /// gutter, the name where every row's name begins, the reading's field against the trailing
    /// gutter — <paramref name="trailing"/> px short of it where a control stands there — and
    /// between the name's end and the reading's field whatever room is left, for a sparkline or a
    /// bar, or nothing. The row is the line's middle band: as tall as its text, with the same air
    /// above and below it as at either side.
    /// <para>
    /// The reading wins (docs\widgets.md §9, "A line at every height"): its field is <paramref name="field"/>
    /// px wide wherever the row has that much after the leading column, and the name takes what is
    /// left — the first of <paramref name="names"/> that fits it whole, else cut where it still reads
    /// (<see cref="Name"/>), else none.
    /// </para>
    /// </summary>
    public static OneRow Row(Gfx gfx, D2D_RECT_F box, in TileStyle style, IReadOnlyList<string> names, Weight weight, float field, float trailing = 0)
    {
        var form = ListForm.Of(gfx, style);
        var height = box.bottom - box.top;
        var middle = box.top + (height / 2);
        var text = box.left + form.TextLeft;
        var end = box.right - form.Inset - trailing;
        var fieldLeft = MathF.Max(text, end - field);
        var room = fieldLeft - style.Gap - text;
        var name = Name(gfx, names, form.TextPx, weight, room);
        var nameRight = name is null ? text : MathF.Min(text + PopupPaint.Measure(gfx, name, form.TextPx, weight).X, text + room);
        var between = new D2D_RECT_F(name is null ? text : nameRight + style.Gap, middle - (form.Line / 2), fieldLeft - style.Gap, middle + (form.Line / 2));
        return new OneRow(form, middle, name, text, nameRight, fieldLeft, end, between);
    }

    /// <summary>
    /// The lines a taller line says under its name: which of <paramref name="said"/>, in order, fit
    /// <paramref name="room"/> px of height at <paramref name="width"/> px wide and <paramref name="px"/>,
    /// each wrapped to at most its own lines — as many as the height leaves it — and cut with an
    /// ellipsis where it says more. The first that does not fit even on one line ends them, so what
    /// is said first is what stays. Each with the lines it was given and its height.
    /// </summary>
    public static List<(string Text, int Lines, float Height)> Lines(Gfx gfx, IEnumerable<(string Text, int Lines)> said, float width, float room, float px)
    {
        var line = MathF.Max(1, TilePaint.LineHeight(gfx, px));
        var fitted = new List<(string, int, float)>();
        foreach (var (words, most) in said)
        {
            if (words.Length == 0)
            {
                continue;
            }
            var lines = (int)MathF.Min(most, MathF.Floor((room + 0.5f) / line));
            if (lines <= 0 || width < 1)
            {
                break;
            }
            var (_, tall) = PopupPaint.Fit(gfx, words, px, Weight.Regular, width, lines);
            if (tall <= 0 || tall > room + 0.5f)
            {
                break;
            }
            fitted.Add((words, lines, tall));
            room -= tall;
        }
        return fitted;
    }

    /// <summary><paramref name="lines"/> (<see cref="Lines"/>) one under the other from <paramref name="top"/>, at <paramref name="left"/>: each one's box, words and lines.</summary>
    public static List<(D2D_RECT_F Box, string Text, int Lines)> Stacked(IEnumerable<(string Text, int Lines, float Height)> lines, float left, float width, float top)
    {
        var placed = new List<(D2D_RECT_F, string, int)>();
        var y = top;
        foreach (var (words, count, tall) in lines)
        {
            placed.Add((PopupPaint.Rect(left, y, width, tall), words, count));
            y += tall;
        }
        return placed;
    }
}

/// <summary>
/// A one-row line's places (<see cref="ListTiers.Row"/>): its middle line, the name it has the room
/// for — none where no form of it reads — and where that begins and ends, where its reading's field
/// begins and ends, and the room between the two.
/// </summary>
internal readonly record struct OneRow(ListForm Form, float Middle, string? Name, float NameLeft, float NameRight, float FieldLeft, float FieldRight, D2D_RECT_F Between)
{
    /// <summary>The reading's field, in px: what it was asked for, or the row's room after the leading column where that is less.</summary>
    public float Field => FieldRight - FieldLeft;

    /// <summary>The name's room, in px.</summary>
    public float NameRoom => MathF.Max(1, NameRight - NameLeft);

    /// <summary>The top of a line of the list's text standing on the row, in the line's own space.</summary>
    public float TextTop => PopupPaint.Round(Middle - (Form.Line / 2));

    /// <summary>Whether the room between the name and the reading is worth a sparkline or a bar.</summary>
    public bool HasRoom(float least) => Between.right - Between.left >= least;
}
