// One style for every widget's line (docs\widgets.md §388; the person, 2026-10-28: "the widget to widget
// style has quite a lot of variance to it. this makes the layouts feel bloated or not coherent with each
// other"). Every line is drawn on one small kit, so a folder of ten widgets reads as one design:
//
//   * Type: six roles in five sizes, Windows 11's ramp — Caption 12, Body 14, Body strong 14 semibold,
//     Value 20, Hero 28, Display 40. Hero is fixed: a taller line shows more, never bigger type (the
//     person, 2026-10-29). Nothing is drawn under 12, a dense calendar included: the year view shows
//     fewer months across instead.
//   * Glyphs: Inline 12 (in a caption or a button), Row 16 (the leading column), Feature 24 (beside a Hero).
//   * Space: 2, 4, 8, 12 (the gutter, ListForm.Gutter) and 16.
//   * Corners: 4 (controls, plates, a chosen row), 8 (cards and panels), and the pill.
//   * Rows: on the folder's cells, half a row each (§442): 40 (two cells), 60 with a caption or a caption and a meter
//     (three; LineRow.Height), the block of words standing in their middle, and the compact 32 of a dense list, an
//     inner pitch (LineRow.Compact, §418).
//
// The sizes are DIP at 100 %, times the display scale, and text times the text scale (TileStyle.Text).
// LineStyleTests holds every line's drawing to them.

using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

/// <summary>The type ramp's sizes in px for one style (<see cref="Of"/>): the only text sizes a line draws.</summary>
internal readonly record struct LineType(float Caption, float Body, float Value, float Hero, float Display)
{
    /// <summary>Details, units, ages, axis labels, a button's word.</summary>
    public const float CaptionDip = 12;

    /// <summary>Names, rows, messages; a header's title in semibold.</summary>
    public const float BodyDip = 14;

    /// <summary>The reading on a row.</summary>
    public const float ValueDip = 20;

    /// <summary>The reading when the line shows the whole widget, at every height.</summary>
    public const float HeroDip = 28;

    /// <summary>A time that is the widget: the clock's header, a timer, a stopwatch, an alarm in full.</summary>
    public const float DisplayDip = 40;

    /// <summary>The ramp, smallest first.</summary>
    public static readonly float[] Dips = [CaptionDip, BodyDip, ValueDip, HeroDip, DisplayDip];

    public static LineType Of(in TileStyle style) =>
        new(style.Text(CaptionDip), style.Text(BodyDip), style.Text(ValueDip), style.Text(HeroDip), style.Text(DisplayDip));

    /// <summary>
    /// The largest of the ramp's sizes in px, at most <paramref name="most"/>, at which
    /// <paramref name="fits"/> holds; the Caption where none does. What a figure that has to fit its
    /// room is drawn at, instead of a size of its own between two of the ramp's.
    /// </summary>
    public static float Largest(in TileStyle style, float most, Func<float, bool> fits)
    {
        for (var step = Dips.Length - 1; step >= 0; step--)
        {
            var px = style.Text(Dips[step]);
            if (px <= most + 0.01f && fits(px))
            {
                return px;
            }
        }
        return style.Text(CaptionDip);
    }

    /// <summary>The ramp's step under <paramref name="px"/>: a time's fraction beside it; the Caption at the least.</summary>
    public static float Under(in TileStyle style, float px)
    {
        for (var step = Dips.Length - 1; step >= 0; step--)
        {
            if (style.Text(Dips[step]) < px - 0.01f)
            {
                return style.Text(Dips[step]);
            }
        }
        return style.Text(CaptionDip);
    }
}

/// <summary>The three glyph sizes, in DIP before the text scale.</summary>
internal static class LineGlyph
{
    /// <summary>In a caption or a button.</summary>
    public const float InlineDip = 12;

    /// <summary>The leading column, an icon button.</summary>
    public const float RowDip = 16;

    /// <summary>Beside a Hero only.</summary>
    public const float FeatureDip = 24;

    public static readonly float[] Dips = [InlineDip, RowDip, FeatureDip];

    public static float Inline(in TileStyle style) => style.Text(InlineDip);

    public static float Row(in TileStyle style) => style.Text(RowDip);

    public static float Feature(in TileStyle style) => style.Text(FeatureDip);

    /// <summary>The largest of the three sizes in px no larger than <paramref name="room"/> px; the Inline at the least.</summary>
    public static float Fitting(in TileStyle style, float room)
    {
        for (var step = Dips.Length - 1; step > 0; step--)
        {
            if (style.Text(Dips[step]) <= room + 0.01f)
            {
                return style.Text(Dips[step]);
            }
        }
        return style.Text(InlineDip);
    }
}

/// <summary>The five spacing steps, in DIP; the gutter is the list's own (<see cref="ListForm.Gutter"/>).</summary>
internal static class LineSpace
{
    /// <summary>A title and its caption.</summary>
    public const float HairDip = 2;

    /// <summary>Lines within one block.</summary>
    public const float TightDip = 4;

    /// <summary>A glyph to its text, a block to the next, a button to the next.</summary>
    public const float GapDip = 8;

    /// <summary>Sections of one widget.</summary>
    public const float SectionDip = 16;
}

/// <summary>The corners a line's fills take, in DIP; a pill is half its own height.</summary>
internal static class LineCorner
{
    /// <summary>Controls, plates, a chosen row.</summary>
    public const float ControlDip = 4;

    /// <summary>Cards and panels.</summary>
    public const float CardDip = 8;
}

/// <summary>
/// The kit's one Status (§388): what a line says while it has nothing to show — empty, out of reach, loading,
/// not set up. One row at the line's top, never centred, never large: the kind's glyph in the list's leading
/// column in the tertiary ink, one sentence where every name begins in the secondary ink, cut with an ellipsis
/// where the row is short, and at most one action, a standard button against the trailing gutter —
/// <paramref name="trailing"/> px short of it where the stale mark stands there.
/// </summary>
internal static class LineStatus
{
    /// <summary>Where its pieces stand (px): the glyph's column, the words, the button — any absent — and which of the actions it is.</summary>
    internal readonly record struct Laid(D2D_RECT_F? Glyph, D2D_RECT_F? Words, D2D_RECT_F? Button, int Action);

    /// <summary>
    /// The row in <paramref name="box"/>: the first of <paramref name="actions"/> (words, and a glyph or none)
    /// whose button leaves the words room, else the last that fits at all; no button where none is asked
    /// for or none fits. The words are left out where not even their first letters read.
    /// </summary>
    public static Laid Lay(Gfx gfx, D2D_RECT_F box, in TileStyle style, bool glyph, string words, IReadOnlyList<(string Text, string? Glyph)> actions, float trailing = 0)
    {
        var form = ListForm.Of(gfx, style);
        var buttons = LineButton.Of(gfx, style);
        var middle = box.top + PopupPaint.Round(MathF.Min(TilePaint.Height(box), LineRows.Px(1, style)) / 2);
        var end = box.right - form.Inset - trailing;
        var text = box.left + form.TextLeft;
        D2D_RECT_F? column = glyph ? form.IconBox(box.left, middle - (form.Line / 2), form.Line) : null;
        Laid? last = null;
        for (var index = 0; index <= actions.Count; index++)
        {
            D2D_RECT_F? button = null;
            if (index < actions.Count)
            {
                var (said, mark) = actions[index];
                var wide = buttons.Width(gfx, said, glyph: mark is not null && said.Length > 0);
                var at = buttons.At(end - wide, middle, wide);
                if (at.left < box.left + form.Inset + (glyph ? form.Icon + form.Gap : 0))
                {
                    continue;
                }
                button = at;
            }
            else if (actions.Count > 0)
            {
                break;
            }
            var room = (button is { } b ? b.left - form.Gap : end) - text;
            D2D_RECT_F? said_ = null;
            if (words.Length > 0 && room > 0 && TilePaint.Readable(gfx, words, form.TextPx, Weight.Regular, room))
            {
                said_ = PopupPaint.Rect(text, PopupPaint.Round(middle - (form.Line / 2)), MathF.Min(room, PopupPaint.Measure(gfx, words, form.TextPx, Weight.Regular).X), form.Line);
            }
            var laid = new Laid(column, said_, button, button is null ? -1 : index);
            if (said_ is not null || words.Length == 0)
            {
                return laid;
            }
            last ??= laid;
        }
        return last ?? new Laid(column, null, null, -1);
    }

    /// <summary>The row drawn (and written down) on <paramref name="sketch"/>: see <see cref="Lay"/>. What it laid out.</summary>
    public static Laid Draw(LineSketch sketch, D2D_RECT_F box, in TileStyle style, string? glyph, string words, IReadOnlyList<(string Text, string? Glyph)> actions,
        float trailing = 0, bool hot = false, bool pressed = false)
    {
        var laid = Lay(sketch.Gfx, box, style, glyph is not null, words, actions, trailing);
        var form = ListForm.Of(sketch.Gfx, style);
        if (laid.Glyph is { } column && glyph is not null)
        {
            sketch.Glyph("status glyph", glyph, form.Glyph, style.Theme.TextTertiary, column);
        }
        if (laid.Words is { } said)
        {
            sketch.Text("status", words, form.TextPx, Weight.Regular, style.Theme.TextSecondary, said.left, said.top, said.right - said.left);
        }
        if (laid.Button is { } button && laid.Action >= 0)
        {
            var (text, mark) = actions[laid.Action];
            sketch.Button("status action", LineButton.Of(sketch.Gfx, style), style.Theme, button, text, mark, QuickSettings.Controls.ButtonStyle.Standard, hot, pressed);
        }
        return laid;
    }
}

/// <summary>How a row's reading against its trailing gutter is drawn (<see cref="LineRow"/>).</summary>
internal enum RowReading
{
    /// <summary>The kit's Value, 20 semibold: the reading on a row (a temperature, a time, a level).</summary>
    Value,

    /// <summary>The Body, 14: a reading that is a word or a time among words (a sunrise, "419 GB free").</summary>
    Body,

    /// <summary>The Caption in the secondary ink: a header's meta (how fresh, how many, which area), an item's age.</summary>
    Meta,
}

/// <summary>
/// One thing a row holds against its trailing gutter, right of its reading: a line button
/// (<see cref="LineButton"/>, drawn by the row), or room the line draws in itself — a switch, a
/// picture.
/// </summary>
internal readonly record struct RowControl(string Name, float Width, float Height, string Text = "", string? Glyph = null,
    QuickSettings.Controls.ButtonStyle? Style = null, bool Hot = false, bool Pressed = false, bool Enabled = true)
{
    /// <summary>A line button holding <paramref name="text"/> after <paramref name="glyph"/> (a glyph alone is square), in <paramref name="style"/>.</summary>
    public static RowControl Button(Gfx gfx, in LineButton sizes, string name, string text, string? glyph, QuickSettings.Controls.ButtonStyle style,
        bool hot = false, bool pressed = false, bool enabled = true) =>
        new(name, sizes.Width(gfx, text, glyph: glyph is not null && text.Length > 0), sizes.Height, text, glyph, style, hot, pressed, enabled);

    /// <summary>Room <paramref name="width"/> by <paramref name="height"/> px the line draws in itself.</summary>
    public static RowControl Room(string name, float width, float height) => new(name, width, height);
}

/// <summary>
/// What a row of the kit shows (<see cref="LineRow"/>): the T1 Row; with <see cref="Strong"/> the T2 Header;
/// with a <see cref="Marker"/> or <see cref="ReadingOnName"/> one of the T4 Items. Every part but the name may be absent.
/// </summary>
internal sealed record RowSpec(string Name)
{
    /// <summary>The kind's glyph in the leading column (the kit's Row size, 16).</summary>
    public string? Glyph { get; init; }

    /// <summary>The glyph's ink: the text's where none is given.</summary>
    public DXGI_RGBA? GlyphInk { get; init; }

    /// <summary>A bar in the leading column instead of a glyph, as tall as the name and its caption: an event's calendar colour.</summary>
    public DXGI_RGBA? Marker { get; init; }

    /// <summary>The name in the Body strong: a header's title, an unread or the next item.</summary>
    public bool Strong { get; init; }

    /// <summary>The name's ink: the text's where none is given.</summary>
    public DXGI_RGBA? NameInk { get; init; }

    /// <summary>One caption line under the name, in the secondary ink: the row is three cells tall, 60 DIP, not 40 (unless <see cref="CaptionBeside"/>).</summary>
    public string? Caption { get; init; }

    /// <summary>The caption after the name on its line (" · " between), not under it: the row stays 40 DIP (an agenda's event, §413).</summary>
    public bool CaptionBeside { get; init; }

    /// <summary>
    /// §442: room kept under the name and its caption, px, in the one block the row stands on its middle: a meter's
    /// (<see cref="LineMeter.Room"/>). The row is three cells with it; where it stands is <see cref="LineRow.Laid.Under"/>.
    /// </summary>
    public float Under { get; init; }

    /// <summary>
    /// §497: the row's detail — how soon, how much is left, how long ago: under the name as its caption where the row is
    /// tall enough for one (and has no <see cref="Caption"/> of its own), else at the row's end before the reading in the
    /// Body's secondary ink, where the name keeps all of its words beside it; the first thing left out where the row is short.
    /// </summary>
    public string? Detail { get; init; }

    /// <summary>The reading against the trailing gutter, left of the controls.</summary>
    public string? Reading { get; init; }

    /// <summary>How the reading is drawn.</summary>
    public RowReading Look { get; init; } = RowReading.Value;

    /// <summary>The reading's ink: the text's (the secondary's for a meta) where none is given.</summary>
    public DXGI_RGBA? ReadingInk { get; init; }

    /// <summary>The reading on the name's line rather than on the row's middle: an item's age.</summary>
    public bool ReadingOnName { get; init; }

    /// <summary>The controls against the trailing gutter, leading first.</summary>
    public IReadOnlyList<RowControl> Controls { get; init; } = [];

    /// <summary>What the row keeps clear at its end past the controls, px: the stale mark's room.</summary>
    public float Trailing { get; init; }

    /// <summary>What the sketch writes its pieces down as: the line's own names for them (its main piece among them).</summary>
    public string GlyphPiece { get; init; } = "glyph";

    public string NamePiece { get; init; } = "name";

    public string CaptionPiece { get; init; } = "caption";

    public string ReadingPiece { get; init; } = "reading";

    public string DetailPiece { get; init; } = "detail";
}

/// <summary>
/// The kit's Row (T1), and the Header (T2) and an Item (T4) it makes with a strong name or a marker
/// (§401): the glyph in the list's leading column on the gutter, the name where every name begins
/// (the text edge, 44) with a caption under it, the reading and the controls against the trailing
/// gutter. One row is two of the folder's cells, 40 DIP, three with a caption (60, §442); its pieces stand on its middle. Drawn on a
/// <see cref="LineSketch"/>, so one pass lays it out, draws it and writes down where each piece stood.
/// </summary>
internal static class LineRow
{
    /// <summary>Where a row's pieces stood (px): absent where not drawn; the controls in the order given.</summary>
    internal readonly record struct Laid(float Top, float Bottom, float Middle, D2D_RECT_F? Glyph, D2D_RECT_F? Name, D2D_RECT_F? Caption,
        D2D_RECT_F? Reading, D2D_RECT_F[] Controls, float Under = 0, D2D_RECT_F? Detail = null);

    /// <summary>Between two parts of a caption: "Local disk · 512 of 931 GB used".</summary>
    public const string Dot = " · ";

    /// <summary>The parts of a caption that say something, joined by <see cref="Dot"/>.</summary>
    public static string Join(params string?[] parts) => string.Join(Dot, parts.Where(part => part is { Length: > 0 }));

    /// <summary>
    /// The compact row of a dense list, DIP before the text scale (§418): an agenda's events and its day headings, one
    /// line each. A button's reach (<see cref="LineButton.ReachDip"/>), so a pointer hits it as it hits any button.
    /// </summary>
    public const float CompactDip = 32;

    /// <summary>How tall a compact row is, px (<see cref="CompactDip"/>): the Body's line and room for a line's button.</summary>
    public static float Compact(in TileStyle style) => PopupPaint.Round(CompactDip * style.Scale * style.TextScale);

    /// <summary>
    /// How tall a row is, px, on the folder's cells (§442): one of the list's rows, two cells, 40 DIP; three with a
    /// caption, 60, the name and its caption standing in their middle — what was 52 DIP and made a line of it two whole
    /// rows, 77, a band of air under it.
    /// </summary>
    public static float Height(in TileStyle style, bool caption) => caption ? LineRows.Cells(CaptionCells, style) : LineRows.Px(1, style);

    /// <summary>The cells a row with its caption (and a meter under it) is: three, 60 DIP.</summary>
    public const int CaptionCells = 3;

    /// <summary>
    /// How tall <paramref name="row"/> is, px (<see cref="Height(in TileStyle, bool)"/>): three cells with a caption (or its
    /// <see cref="RowSpec.Detail"/>) under its name or room under it.
    /// </summary>
    public static float Height(in TileStyle style, RowSpec row) =>
        Height(style, ((row.Caption ?? row.Detail) is { Length: > 0 } && !row.CaptionBeside) || row.Under > 0);

    /// <summary>The reading's size and weight for <paramref name="look"/>.</summary>
    internal static (float Px, Weight Weight) Type(in TileStyle style, RowReading look) => look switch
    {
        RowReading.Value => (style.Text(LineType.ValueDip), Weight.Semibold),
        RowReading.Body => (style.Text(LineType.BodyDip), Weight.Regular),
        _ => (style.Text(LineType.CaptionDip), Weight.Regular),
    };

    /// <summary>
    /// <paramref name="row"/> laid out across <paramref name="left"/> to <paramref name="right"/> (the line's
    /// edges, px) from <paramref name="top"/>, <paramref name="height"/> px tall (its own where 0), drawn on
    /// <paramref name="sketch"/>. The reading wins: the name and the caption take what it and the controls leave,
    /// cut with an ellipsis.
    /// </summary>
    public static Laid Draw(LineSketch sketch, float left, float right, float top, in TileStyle style, RowSpec row, float height = 0)
    {
        var gfx = sketch.Gfx;
        var form = ListForm.Of(gfx, style);
        var theme = style.Theme;
        var tall = height > 0 ? height : Height(style, row);
        var middle = top + PopupPaint.Round(tall / 2);
        var gap = style.Px(LineSpace.GapDip);
        var text = left + form.TextLeft;
        var end = right - form.Inset - row.Trailing;

        // The controls, from the trailing gutter back.
        var controls = new D2D_RECT_F[row.Controls.Count];
        var x = end;
        for (var index = row.Controls.Count - 1; index >= 0; index--)
        {
            var control = row.Controls[index];
            controls[index] = PopupPaint.Rect(x - control.Width, PopupPaint.Round(middle - (control.Height / 2)), control.Width, control.Height);
            x = controls[index].left - gap;
        }
        var readingEnd = row.Controls.Count > 0 ? x : end;

        // The name and its caption, one block on the row's middle.
        var weight = row.Strong ? Weight.Semibold : Weight.Regular;
        var nameLine = TilePaint.LineHeight(gfx, form.TextPx, weight);
        var captionPx = style.Text(LineType.CaptionDip);
        var captionLine = TilePaint.LineHeight(gfx, captionPx);
        // §497: the detail is the caption where the row holds one under its name and has none of its own; where the row
        // is one row, it stands at the end (below); where a caption of the row's own is under the name, that says it.
        var underName = !row.CaptionBeside && tall + 0.5f >= Height(style, caption: true);
        var captionWords = row.Caption is { Length: > 0 } ? row.Caption : underName ? row.Detail : null;
        var atEnd = underName ? null : row.Detail;
        var hasCaption = captionWords is { Length: > 0 };
        var hair = style.Px(LineSpace.HairDip);
        var block = nameLine + (hasCaption && !row.CaptionBeside ? hair + captionLine : 0) + row.Under;
        var nameTop = PopupPaint.Round(middle - (block / 2));

        // The reading against the controls, on the row's middle or the name's line.
        D2D_RECT_F? reading = null;
        var wordsEnd = readingEnd;
        if (row.Reading is { Length: > 0 } said)
        {
            var (px, readingWeight) = Type(style, row.Look);
            var ink = row.ReadingInk ?? (row.Look == RowReading.Meta ? theme.TextSecondary : theme.Text);
            var line = TilePaint.LineHeight(gfx, px, readingWeight);
            var readingTop = row.ReadingOnName ? nameTop + PopupPaint.Round((nameLine - line) / 2) : PopupPaint.Round(middle - (line / 2));
            var drawn = sketch.TextRight(row.ReadingPiece, said, px, readingWeight, ink, readingEnd, readingTop, MathF.Max(1, readingEnd - text));
            if (drawn.right > drawn.left)
            {
                reading = drawn;
                wordsEnd = drawn.left - gap;
            }
        }

        // §497: the detail at the end, before the reading, on the name's line in the Body's secondary ink — only where
        // the name keeps all of its words beside it: on a short row it is the first thing left out.
        D2D_RECT_F? detail = null;
        if (atEnd is { Length: > 0 })
        {
            var wide = PopupPaint.Measure(gfx, atEnd, form.TextPx, Weight.Regular).X;
            var named = PopupPaint.Measure(gfx, row.Name, form.TextPx, weight).X;
            if (text + named + gap + wide <= wordsEnd + 0.5f)
            {
                var line = TilePaint.LineHeight(gfx, form.TextPx, Weight.Regular);
                var drawn = sketch.TextRight(row.DetailPiece, atEnd, form.TextPx, Weight.Regular, theme.TextSecondary, wordsEnd,
                    nameTop + PopupPaint.Round((nameLine - line) / 2), MathF.Ceiling(wide) + 1);
                if (drawn.right > drawn.left)
                {
                    detail = drawn;
                    wordsEnd = drawn.left - gap;
                }
            }
        }

        D2D_RECT_F? glyph = null;
        if (row.Marker is { } marker)
        {
            var column = form.IconBox(left, top, tall);
            var thick = MathF.Max(2, style.Px(3));
            var bar = PopupPaint.Rect(PopupPaint.Round(((column.left + column.right) / 2) - (thick / 2)), nameTop, thick, block);
            sketch.Plate(row.GlyphPiece, bar, thick / 2, marker);
            glyph = bar;
        }
        else if (row.Glyph is { Length: > 0 } mark)
        {
            glyph = form.IconBox(left, top, tall);
            sketch.Glyph(row.GlyphPiece, mark, form.Glyph, row.GlyphInk ?? theme.Text, glyph.Value);
        }

        var nameRoom = MathF.Max(1, wordsEnd - text);
        var name = sketch.Text(row.NamePiece, row.Name, form.TextPx, weight, row.NameInk ?? theme.Text, text, nameTop, nameRoom);
        D2D_RECT_F? caption = null;
        if (hasCaption)
        {
            // Beside, in the name's size so the two share a baseline, cut where the name leaves off.
            var drawn = row.CaptionBeside
                ? sketch.Text(row.CaptionPiece, Dot + captionWords, form.TextPx, Weight.Regular, theme.TextSecondary, name.right, nameTop, MathF.Max(1, wordsEnd - name.right))
                : sketch.Text(row.CaptionPiece, captionWords!, captionPx, Weight.Regular, theme.TextSecondary, text, nameTop + nameLine + hair, nameRoom);
            caption = drawn.right > drawn.left ? drawn : null;
        }

        for (var index = 0; index < row.Controls.Count; index++)
        {
            var control = row.Controls[index];
            if (control.Style is { } kind)
            {
                sketch.Button(control.Name, LineButton.Of(gfx, style), theme, controls[index], control.Text, control.Glyph, kind, control.Hot, control.Pressed, control.Enabled);
            }
            else
            {
                sketch.Placed(control.Name, controls[index]);
            }
        }
        return new Laid(top, top + tall, middle, glyph, name.right > name.left ? name : null, caption, reading, controls, nameTop + block - row.Under, detail);
    }
}

/// <summary>
/// The kit's list item (§428; the person, 2026-10-30: "the items when hovering over them in a widget like the rss or
/// folder widget should go from edge to edge and also touch each other"): what an item of a list — a headline, a
/// message, a file, a network, a process — lights under the pointer and takes a press on. Its <see cref="Band"/> is the
/// line's whole width, edge to edge, from where the room over it is halved with the item over it to where the room
/// under it is halved with the one under it: the items of a list touch, and every pixel of them is one's. Its words
/// stand where they stood; only what lights and takes the press grew. Square: the line's own clip rounds what reaches a
/// corner (a folder's section, §410). A control on an item — a Join, an Eject, a switch — keeps its own hit and light
/// over the band.
/// From the line's edges (§436; "the items should also go all the way to the top or bottom edge if applicable", "the
/// last item should not be stretched"): a list keeps no air over its first item where nothing stands over it, nor under
/// its last at the height it asks for — the first band starts at the line's top, the last ends at its foot — and every
/// band keeps the height every other has: the first and the last as much room on their open side as on the other
/// (<see cref="Foot"/>). A line taller than its items leaves the room under the last unlit; no band grows into it.
/// </summary>
internal static unsafe class LineItem
{
    /// <summary>The room a list keeps under its last item, whose words have <paramref name="air"/> px to the one over it: half of it, the band's own, so the line's foot is the band's.</summary>
    public static float Foot(float air) => PopupPaint.Round(air / 2);

    /// <summary>The band of an item from <paramref name="top"/> to <paramref name="bottom"/> on a line <paramref name="box"/>: its whole width, on whole px.</summary>
    public static D2D_RECT_F Band(in D2D_RECT_F box, float top, float bottom) =>
        new(box.left, PopupPaint.Round(top), box.right, PopupPaint.Round(bottom));

    /// <summary>Where an item ending at <paramref name="above"/> and the next starting at <paramref name="below"/> meet: the room's middle, on a whole px.</summary>
    public static float Seam(float above, float below) => PopupPaint.Round((above + below) / 2);

    /// <summary>The band of an item whose words stand from <paramref name="top"/> to <paramref name="bottom"/> with <paramref name="air"/> px between it and each neighbour: half of it over, half under.</summary>
    public static D2D_RECT_F Around(in D2D_RECT_F box, float top, float bottom, float air) => Band(box, top - (air / 2), bottom + (air / 2));

    /// <summary>
    /// §611, §632: <paramref name="count"/> items across <paramref name="line"/> — a forecast's hours or days — each as wide
    /// as the others: the line's width in as many equal bands, the first from its leading edge, the last to its trailing
    /// one, each the line's whole height, top to foot, on whole px. What an item shows stands in its band's middle, so the
    /// items stand as evenly apart as their bands are wide; none is wider for being at an end.
    /// </summary>
    public static List<D2D_RECT_F> Across(in D2D_RECT_F line, int count)
    {
        var each = (line.right - line.left) / Math.Max(1, count);
        var bands = new List<D2D_RECT_F>(Math.Max(0, count));
        for (var index = 0; index < count; index++)
        {
            var left = index == 0 ? line.left : PopupPaint.Round(line.left + (index * each));
            var right = index == count - 1 ? line.right : PopupPaint.Round(line.left + ((index + 1) * each));
            bands.Add(new D2D_RECT_F(left, PopupPaint.Round(line.top), right, PopupPaint.Round(line.bottom)));
        }
        return bands;
    }

    /// <summary>
    /// §661: <paramref name="count"/> tiles in a grid <paramref name="across"/> wide from <paramref name="top"/>, each row
    /// <paramref name="tall"/> px: every row <see cref="Across"/> the line's whole width, edge to edge, its cells as wide as
    /// each other, the rows touching. A short last row keeps the others' cells (its room past them nobody's, as File
    /// Explorer's grid). What a tile shows stands in its cell's middle, its words kept to the gutters (<see cref="Words"/>).
    /// </summary>
    public static List<D2D_RECT_F> Grid(in D2D_RECT_F line, float top, int count, int across, float tall)
    {
        across = Math.Max(1, across);
        var cells = new List<D2D_RECT_F>(Math.Max(0, count));
        for (var row = 0; row * across < count; row++)
        {
            var band = new D2D_RECT_F(line.left, top + (row * tall), line.right, top + ((row + 1) * tall));
            cells.AddRange(Across(band, across).Take(count - (row * across)));
        }
        return cells;
    }

    /// <summary>
    /// §661: where words go in <paramref name="cell"/> of a line from <paramref name="lineLeft"/> to <paramref name="lineRight"/>:
    /// <paramref name="pad"/> px in from the cell's sides and never past the line's gutters (<paramref name="gutter"/>), so a cell
    /// at the line's edge lights to it while its words stand where every line's do.
    /// </summary>
    public static (float Left, float Right) Words(in D2D_RECT_F cell, float lineLeft, float lineRight, float gutter, float pad) =>
        (MathF.Max(cell.left + pad, lineLeft + gutter), MathF.Min(cell.right - pad, lineRight - gutter));

    /// <summary>§611: <paramref name="count"/> items down <paramref name="line"/>, each as tall as the others: the first from its top, the last to its foot, every one its whole width.</summary>
    public static List<D2D_RECT_F> Down(in D2D_RECT_F line, int count)
    {
        var each = (line.bottom - line.top) / Math.Max(1, count);
        var bands = new List<D2D_RECT_F>(Math.Max(0, count));
        for (var index = 0; index < count; index++)
        {
            bands.Add(Band(line, line.top + (index * each), index == count - 1 ? line.bottom : line.top + ((index + 1) * each)));
        }
        return bands;
    }

    /// <summary>The band under <paramref name="point"/> of <paramref name="bands"/>, from 1; 0 outside them all.</summary>
    public static int At(IReadOnlyList<D2D_RECT_F> bands, System.Numerics.Vector2 point)
    {
        for (var index = 0; index < bands.Count; index++)
        {
            var band = bands[index];
            if (point.X >= band.left && point.X < band.right && point.Y >= band.top && point.Y < band.bottom)
            {
                return index + 1;
            }
        }
        return 0;
    }

    /// <summary><paramref name="band"/> lit: the subtle fill under the pointer, the pressed one while a press is down on it.</summary>
    public static void Light(ID2D1DeviceContext* dc, in D2D_RECT_F band, QuickSettings.Controls.QuickTheme theme, bool pressed) =>
        PopupPaint.Fill(dc, band, 0, pressed ? theme.SubtlePressed : theme.SubtleHover);

    /// <inheritdoc cref="Light(ID2D1DeviceContext*, in D2D_RECT_F, QuickSettings.Controls.QuickTheme, bool)"/>
    public static void Light(ID2D1DeviceContext* dc, in D2D_RECT_F band, FlyoutTheme theme, bool pressed) =>
        PopupPaint.Fill(dc, band, 0, PopupPaint.Ink(pressed ? theme.SubtlePressed : theme.SubtleHover));

    /// <summary>Lit on <paramref name="sketch"/> where it draws; never written down as a piece.</summary>
    public static void Light(LineSketch sketch, in D2D_RECT_F band, FlyoutTheme theme, bool pressed)
    {
        if (sketch.Draws)
        {
            Light(sketch.Device, band, theme, pressed);
        }
    }

    /// <summary>
    /// <paramref name="band"/> the chosen one of its list (the layout in use, the desktop shown, the output in use): the
    /// subtle selected fill; under the pointer a step quieter, as WinUI's selected list item, so it shows it is lit.
    /// The accent's <see cref="Pill"/> goes with it.
    /// </summary>
    public static void Chosen(ID2D1DeviceContext* dc, in D2D_RECT_F band, QuickSettings.Controls.QuickTheme theme, bool lit) =>
        PopupPaint.Fill(dc, band, 0, lit ? theme.SubtlePressed : theme.SubtleSelected);

    /// <summary>
    /// The chosen item's accent pill, WinUI's 3 × 16 DIP (shorter in a short row), on the band's middle <paramref name="at"/>
    /// px in from its leading edge: <see cref="PillInDip"/> short of the glyph's column, where it stood when an item's
    /// plate stood in from the line's.
    /// </summary>
    public static void Pill(ID2D1DeviceContext* dc, in D2D_RECT_F band, float at, float scale, float textScale, DXGI_RGBA accent)
    {
        var tall = MathF.Min(16 * scale * textScale, band.bottom - band.top - (8 * scale));
        var wide = MathF.Max(2, PopupPaint.Round(3 * scale));
        PopupPaint.Fill(dc, PopupPaint.Rect(band.left + at, PopupPaint.Round(((band.top + band.bottom) / 2) - (tall / 2)), wide, PopupPaint.Round(tall)),
            wide / 2, accent);
    }

    /// <summary>The pill on a line's item: <see cref="PillInDip"/> short of the gutter, 4 DIP in at the least.</summary>
    public static void Pill(ID2D1DeviceContext* dc, in D2D_RECT_F band, in TileStyle style, DXGI_RGBA accent) =>
        Pill(dc, band, MathF.Max(style.Px(4), ListForm.Gutter(style) - style.Px(PillInDip)), style.Scale, style.TextScale, accent);

    /// <summary>How far short of the glyph's column the chosen item's pill stands, DIP: the rows' pad (QuickSettings ListRow.PadDip).</summary>
    public const float PillInDip = 8;

    /// <summary><paramref name="band"/> <paramref name="by"/> px smaller on every side: where a ring round it is stroked so the line's clip leaves all of it.</summary>
    public static D2D_RECT_F Inside(in D2D_RECT_F band, float by) => new(band.left + by, band.top + by, band.right - by, band.bottom - by);
}

/// <summary>
/// The kit's Hero (T3, §401): the reading when the line shows the whole widget — the Feature glyph (24) in the
/// leading column, grown into the room before the text, the value in the Hero (28; the Display, 40, for a time
/// that is the widget) where every name begins, a status in the Body's secondary ink beside it on its baseline,
/// and caption lines under it. One size at every height: a taller line shows more, never larger.
/// </summary>
internal static class LineHero
{
    /// <summary>Where a hero's pieces stood (px), and its foot.</summary>
    internal readonly record struct Laid(float Top, float Bottom, D2D_RECT_F? Glyph, D2D_RECT_F Value, D2D_RECT_F? Status, D2D_RECT_F[] Captions);

    /// <summary>Segoe's ascent over its size: where a line's baseline stands under its top.</summary>
    private const float Ascent = 1.079f;

    /// <summary>The value's size, px: the Hero, or the Display for a time.</summary>
    public static float ValuePx(in TileStyle style, bool display) => style.Text(display ? LineType.DisplayDip : LineType.HeroDip);

    /// <summary>How tall a hero with <paramref name="captions"/> lines under it is, px: its air, its value's line and theirs.</summary>
    public static float Height(Gfx gfx, in TileStyle style, int captions, bool display = false) =>
        style.Px(LineSpace.TightDip) + TilePaint.LineHeight(gfx, ValuePx(style, display), Weight.Semibold)
        + (captions * TilePaint.LineHeight(gfx, style.Text(LineType.CaptionDip))) + style.Px(LineSpace.GapDip);

    /// <summary>
    /// The hero across <paramref name="left"/> to <paramref name="right"/> from <paramref name="top"/>, drawn on
    /// <paramref name="sketch"/>; <paramref name="trailing"/> px kept clear at its end (a picture beside it).
    /// <paramref name="gutter"/>: the value and its captions on the gutter with no glyph, a time that heads its line.
    /// </summary>
    public static Laid Draw(LineSketch sketch, float left, float right, float top, in TileStyle style, string? glyph, string value,
        string? status, IReadOnlyList<string> captions, bool display = false, float trailing = 0, bool gutter = false, DXGI_RGBA? glyphInk = null)
    {
        var gfx = sketch.Gfx;
        var form = ListForm.Of(gfx, style);
        var theme = style.Theme;
        var px = ValuePx(style, display);
        var line = TilePaint.LineHeight(gfx, px, Weight.Semibold);
        var y = top + style.Px(LineSpace.TightDip);
        var text = left + (gutter ? form.Inset : form.TextLeft);
        var end = right - form.Inset - trailing;
        D2D_RECT_F? mark = null;
        if (!gutter && glyph is { Length: > 0 })
        {
            var feature = LineGlyph.Feature(style);
            mark = ListTiers.GlyphBox(form, left, y, line, feature);
            sketch.Glyph("hero glyph", glyph, feature, glyphInk ?? theme.Text, mark.Value);
        }
        var drawn = sketch.Text("hero", value, px, Weight.Semibold, theme.Text, text, y, MathF.Max(1, end - text));
        D2D_RECT_F? said = null;
        if (status is { Length: > 0 })
        {
            var beside = drawn.right + style.Px(LineSpace.GapDip);
            var statusTop = y + PopupPaint.Round(Ascent * (px - form.TextPx));
            var shown = sketch.Text("hero status", status, form.TextPx, Weight.Regular, theme.TextSecondary, beside, statusTop, MathF.Max(1, end - beside));
            said = shown.right > shown.left ? shown : null;
        }
        y += line;
        var captionPx = style.Text(LineType.CaptionDip);
        var captionLine = TilePaint.LineHeight(gfx, captionPx);
        var under = new List<D2D_RECT_F>();
        foreach (var words in captions)
        {
            if (words.Length == 0)
            {
                continue;
            }
            under.Add(sketch.Text("hero caption", words, captionPx, Weight.Regular, theme.TextSecondary, text, y, MathF.Max(1, end - text)));
            y += captionLine;
        }
        return new Laid(top, y + style.Px(LineSpace.GapDip), mark, drawn, said, [.. under]);
    }
}

/// <summary>
/// The kit's meter (§388, §401): 4 DIP, a pill, from where every name begins to the trailing gutter, the fill over
/// the track. Drawn on a <see cref="LineSketch"/>, written down as "meter".
/// §449 (the person, 2026-10-31: "in the focus time when the timer is running the progress bar and the pause button
/// touch. this should not be."): a meter keeps a button's gap from every control of its row (<see cref="Box"/>); where
/// it would come nearer one across or down, it ends that gap short of the first, under the row's words.
/// </summary>
internal static class LineMeter
{
    /// <summary>The meter's thickness, px.</summary>
    public static float Thick(in TileStyle style) => ListTiers.MeterPx(style);

    /// <summary>
    /// §442: the room a meter takes under a row's caption, px (<see cref="RowSpec.Under"/>): a tight step and its
    /// thickness, so the name, the caption and the meter stand as one block in the row's three cells.
    /// </summary>
    public static float Room(in TileStyle style) => style.Px(LineSpace.TightDip) + Thick(style);

    /// <summary>
    /// Where a meter <paramref name="thick"/> px thick (the kit's where 0) from <paramref name="top"/> on a line across
    /// <paramref name="left"/> to <paramref name="right"/> stands (§449): from where every name begins to the trailing
    /// gutter, or a button's gap (<see cref="LineButton.GapDip"/>) short of the first of <paramref name="controls"/> it
    /// would otherwise come nearer than that gap, across or down.
    /// </summary>
    public static D2D_RECT_F Box(Gfx gfx, float left, float right, float top, in TileStyle style, IReadOnlyList<D2D_RECT_F> controls, float thick = 0)
    {
        var form = ListForm.Of(gfx, style);
        thick = thick > 0 ? thick : Thick(style);
        var gap = style.Px(LineButton.GapDip);
        var start = left + form.TextLeft;
        var end = right - form.Inset;
        foreach (var control in controls)
        {
            if (control.right > control.left && control.right > start && control.top < top + thick + gap && control.bottom > top - gap)
            {
                end = MathF.Min(end, control.left - gap);
            }
        }
        return PopupPaint.Rect(start, top, MathF.Max(1, end - start), thick);
    }

    /// <summary>The meter in the room <paramref name="row"/> kept for it (<see cref="Room"/>), clear of its controls, <paramref name="fraction"/> full.</summary>
    public static void Under(LineSketch sketch, in LineRow.Laid row, float left, float right, in TileStyle style, float fraction, DXGI_RGBA fill) =>
        Draw(sketch, Box(sketch.Gfx, left, right, row.Under + style.Px(LineSpace.TightDip), style, row.Controls), style, fraction, fill);

    /// <summary>The meter a tight step over the foot of <paramref name="row"/>, clear of its controls, <paramref name="fraction"/> full: a timer's while it runs.</summary>
    public static void Foot(LineSketch sketch, in LineRow.Laid row, float left, float right, in TileStyle style, float fraction, DXGI_RGBA fill) =>
        Draw(sketch, Box(sketch.Gfx, left, right, row.Bottom - style.Px(LineSpace.TightDip) - Thick(style), style, row.Controls), style, fraction, fill);

    /// <summary>The meter in <paramref name="box"/>, <paramref name="fraction"/> full.</summary>
    private static unsafe void Draw(LineSketch sketch, D2D_RECT_F box, in TileStyle style, float fraction, DXGI_RGBA fill)
    {
        var track = TilePaint.Track(style);
        sketch.Picture("meter", box, (dc, at) => TilePaint.Bar(dc, at, Math.Clamp(fraction, 0, 1), track, fill));
    }
}