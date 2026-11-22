// A clock in a folder: a tile like the other widgets, with the time large, the
// date, and the label or zone it stands for. A click searches the time where it
// stands in the browser once the folder has gone (TimeSearch, §377). The bar has no
// clock of its own: its clock is a folder wearing the clock face (the clock preset),
// whose click opens that folder, as Windows' clock opens its calendar.
//
// In a list folder a clock is its line on the kit's blocks (docs\widgets.md §401; the person, 2026-09-27: "the
// clock does not properly show for 1 cell heights"): on one row the kit's row — the clock's glyph, the date or
// the place it stands for, and the time against the trailing gutter; taller, the time as the Display heading the
// line on the gutter with the date under it and the place under that, and a face of the time against the
// trailing gutter. A clock has nothing more to show on a taller line, so it stands at its top (Line).
//
// Its looks (§443, ClockStyles; the person: "the mode where it just shows the time without the date … digital, two
// numbers"): the time and the date beside a face, as above; the time alone; the hours over the minutes; the face alone.
// Each is this line in its look, Compact the kit's row and Full the look whole.

using System.Globalization;
using System.Numerics;
using Karpo.Core;
using Karpo.Dock.Native;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

internal sealed unsafe class ClockTile : CellIsland, IListContent, ILinePieces, IOpensOnceGone
{
    private ClockOptions options = new();

    // The settings a click searches with (the app's engine), and the search it chose (§377).
    private Config? searchWith;
    private readonly TimeSearch search = new();

    public Action<string> Opened
    {
        get => search.Opened;
        set => search.Opened = value;
    }

    /// <summary>The dock, once the folder has gone: opens the search a click chose, once.</summary>
    public void Open() => search.Open();

    public ClockTile(string key, WidgetItem island, Gfx gfx, Config config)
        : base(key, island, gfx)
    {
        Configure(island, config);
    }

    /// <summary>The folder's flyout ticks it (each minute, or second with seconds); nothing does while it is closed.</summary>
    public override int IntervalMs => 0;

    /// <summary>Whether it shows seconds: the folder's flyout then ticks every second.</summary>
    public bool Seconds => options.Seconds;

    /// <summary>The Windows time zone id it shows; empty for this PC's.</summary>
    public string Zone => options.TimeZone;

    protected override int Count => 1;

    public override void Configure(WidgetItem island, Config config)
    {
        // The shape is a function of the settings, so the shape must be given the
        // settings just handed over: keeping the ones this island was born with
        // left the size measured for the old options while the drawing used the
        // new ones (a folder whose name was switched on kept its square).
        Settings = island;
        searchWith = config;
        var next = island.Clock ?? new ClockOptions();
        options = new ClockOptions { Seconds = next.Seconds, TimeZone = next.TimeZone ?? "", Label = next.Label ?? "", Style = ClockStyles.Known(next.Style) };
        Forget();
    }

    /// <summary>
    /// No hover tooltip: a folder's cards have none, and the card says everything; nor has a face on
    /// the bar (§367).
    /// </summary>
    public override string? Tooltip(int index) => null;

    /// <summary>
    /// The time where the clock stands searched for in the browser once the folder has gone (§377): a
    /// clock of another zone its city's, this PC's own the time here — the zone's, never the label's,
    /// which is the person's own word ("Home", "Mum") and no place an engine knows.
    /// </summary>
    public override IslandAction Click(int index)
    {
        search.Choose(searchWith, options.TimeZone);
        return new IslandAction(View: IslandView.App);
    }

    public override bool MinuteChanged(Gfx gfx) => Redraw(gfx);

    protected override bool Sample(bool everything) => false;

    // stage 3: the size is WidgetShapes.Clock, measured on the longest the time ever reads in
    // this format rather than on the time showing — which is what it used to do, so a 12-hour
    // clock was narrower before ten in the morning.

    internal override void PaintCell(ID2D1DeviceContext* dc, int index, in IslandStyle style, float width, float height)
    {
        var fit = new Fit(style);
        var ink = new IslandInk(style.Look);
        var now = Clock.Read(options.TimeZone, options.Seconds);
        PopupPaint.Centred(dc, Gfx, now.Time, fit.Primary, Weight.Regular, ink.Text, PopupPaint.Rect(0, 0, width, height), ink: false);
    }

    /// <summary>In a list folder the clock is its line at the height it is given (<see cref="Line"/>).</summary>
    protected internal override unsafe void PaintCard(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, WidgetSurface surface) =>
        Line(new LineSketch(dc, Gfx), box, style);

    /// <summary>
    /// The shape a time is sized on: its figures read as eights, and an hour of one figure as two,
    /// so "9:59" and "10:00" are the same size and the line does not change at ten.
    /// </summary>
    internal static string TimeShape(string time)
    {
        var shape = string.Concat(time.Select(letter => char.IsDigit(letter) ? '8' : letter));
        return shape.Length > 1 && shape[0] == '8' && !char.IsDigit(time[1]) ? "8" + shape : shape;
    }

    /// <summary>
    /// The time on a clock face, as large as the room it is given: a ring, the twelve hours as
    /// ticks, the hour and the minute hand, and the second hand in the accent where the clock
    /// shows seconds. A face a row's size (§443's Analog in Compact) keeps the quarters' ticks alone.
    /// </summary>
    private PictureDraw Dial(TileStyle style) => (dc, box) =>
    {
        var side = MathF.Min(box.right - box.left, box.bottom - box.top);
        if (side < style.Px(FaceSmallestDip))
        {
            return;
        }
        // A clock's face goes round the same way in every language: upright in a mirrored line (Mirror.cs).
        var kept = Mirror.Keep(dc, box);
        try
        {
            Face(dc, box, side, style);
        }
        finally
        {
            Mirror.Resume(dc, kept);
        }
    };

    private unsafe void Face(ID2D1DeviceContext* dc, D2D_RECT_F box, float side, in TileStyle style)
    {
        var theme = style.Theme;
        var centre = new Vector2((box.left + box.right) / 2, (box.top + box.bottom) / 2);
        var radius = (side / 2) - MathF.Max(1, style.Px(1));
        var ring = MathF.Max(1, MathF.Round(side * 0.02f));
        PopupPaint.Ring(dc, centre, radius - (ring / 2), ring, theme.TextTertiary);
        var tick = MathF.Max(1, MathF.Round(side * 0.025f));
        var small = side < style.Px(FaceLeastDip);
        for (var hour = 0; hour < 12; hour++)
        {
            if (small && hour % 3 != 0)
            {
                continue;
            }
            var length = hour % 3 == 0 ? side * 0.08f : side * 0.045f;
            Hand(dc, centre, hour * 30, radius - ring - (side * 0.03f), radius - ring - (side * 0.03f) - length, tick, theme.TextSecondary);
        }
        var at = Clock.NowIn(options.TimeZone);
        var minutes = at.Minute + (at.Second / 60f);
        var hours = (at.Hour % 12) + (minutes / 60f);
        Hand(dc, centre, hours * 30, radius * 0.52f, -radius * 0.08f, MathF.Max(2, MathF.Round(side * 0.05f)), theme.Text);
        Hand(dc, centre, minutes * 6, radius * 0.78f, -radius * 0.08f, MathF.Max(2, MathF.Round(side * 0.035f)), theme.Text);
        if (options.Seconds)
        {
            Hand(dc, centre, at.Second * 6, radius * 0.84f, -radius * 0.14f, MathF.Max(1, MathF.Round(side * 0.012f)), TilePaint.Accent(style));
        }
        PopupPaint.Circle(dc, centre, MathF.Max(2, side * 0.035f), theme.Text);
    }

    /// <summary>
    /// A hand of the face: a rounded bar from <paramref name="inner"/> to <paramref name="outer"/>
    /// px out from <paramref name="centre"/>, turned <paramref name="degrees"/> clockwise from twelve.
    /// </summary>
    private static void Hand(ID2D1DeviceContext* dc, Vector2 centre, float degrees, float outer, float inner, float width, DXGI_RGBA color)
    {
        D2D_MATRIX_3X2_F before;
        dc->GetTransform(&before);
        var turn = Matrix3x2.CreateRotation(degrees * MathF.PI / 180, centre)
            * new Matrix3x2(before._11, before._12, before._21, before._22, before._31, before._32);
        var turned = new D2D_MATRIX_3X2_F { _11 = turn.M11, _12 = turn.M12, _21 = turn.M21, _22 = turn.M22, _31 = turn.M31, _32 = turn.M32 };
        dc->SetTransform(&turned);
        try
        {
            PopupPaint.Fill(dc, new D2D_RECT_F(centre.X - (width / 2), centre.Y - outer, centre.X + (width / 2), centre.Y - inner), width / 2, color);
        }
        finally
        {
            dc->SetTransform(&before);
        }
    }

    // --- As a line of a list ----------------------------------------------------

    /// <summary>The look its line is in (§443): the time and the date beside a face, the time alone, the hours over the minutes, the face alone.</summary>
    private ClockStyle Look => options.Style;

    /// <summary>What a line of one row shows first: the time, the hours over the minutes, or the face.</summary>
    public string MainPiece => Look switch
    {
        ClockStyle.Analog => "dial",
        ClockStyle.Stacked => "hours",
        _ => "time",
    };

    public IReadOnlyList<LinePiece> LinePieces(D2D_RECT_F box, in TileStyle style)
    {
        var sketch = new LineSketch(null, Gfx);
        Line(sketch, box, style);
        return sketch.Pieces;
    }

    /// <summary>
    /// Its form for its size (§432) in its look (§443): the kit's row (Compact); or the look whole (Full, §401) in the
    /// fewest whole cells that hold it (§442) — the time as the Display over the date and the place, a face beside them as
    /// tall as the words; the time alone over the place; the hours over the minutes; the face alone, as tall as the time
    /// and the date would be, so the clock changed to it or from it keeps its height.
    /// </summary>
    public float ListHeight(float width, in TileStyle style)
    {
        if (LineSizes.Of(Settings) == LineSize.Compact)
        {
            return LineRow.Height(style, caption: false);
        }
        var now = Clock.Read(options.TimeZone, options.Seconds);
        return LineRows.Whole(Whole(style, style.Text(LineType.DisplayDip), Place(now).Length > 0), style);
    }

    /// <summary>The least a face of the time beside the words is drawn at, in DIPs; a smaller face keeps the quarters' ticks alone.</summary>
    private const float FaceLeastDip = 48;

    /// <summary>The least a face is drawn at, in DIPs: a row's, the face alone in Compact.</summary>
    private const float FaceSmallestDip = 20;

    /// <summary>How far under the hours' line the minutes' stands, of the figures' size: close, as figures have nothing under the line.</summary>
    private const float PitchOfSize = 0.85f;

    /// <summary>
    /// How tall the look's whole form stands with the line's air, its figures at <paramref name="timePx"/>, the place
    /// under its words where it has one: the time and the date (the face beside them, or alone, as tall); the time alone;
    /// the hours over the minutes.
    /// </summary>
    private float Whole(in TileStyle style, float timePx, bool place) =>
        style.Px(LineSpace.TightDip) + style.Px(LineSpace.GapDip) + Look switch
        {
            ClockStyle.TimeOnly => TilePaint.LineHeight(Gfx, timePx, Weight.Semibold) + (place ? TilePaint.LineHeight(Gfx, style.Text(LineType.CaptionDip)) : 0),
            ClockStyle.Stacked => Stack(timePx),
            _ => Words(style, timePx, place),
        };

    /// <summary>The time's line, the date's and the place's, one under the other.</summary>
    private float Words(in TileStyle style, float timePx, bool place) =>
        TilePaint.LineHeight(Gfx, timePx, Weight.Semibold) + TilePaint.LineHeight(Gfx, style.Text(LineType.BodyDip))
        + (place ? TilePaint.LineHeight(Gfx, style.Text(LineType.CaptionDip)) : 0);

    /// <summary>The hours' line over the minutes' at <paramref name="px"/>, the minutes <see cref="PitchOfSize"/> of it under.</summary>
    private float Stack(float px) => PopupPaint.Round(px * PitchOfSize) + TilePaint.LineHeight(Gfx, px, Weight.Semibold);

    /// <summary>
    /// The line, at whatever height it was given (docs\widgets.md §9, "A line at every height"), on the kit's blocks
    /// (§401), in its look (§443), drawn where the sketch draws: the kit's row where the look's whole form has not the
    /// room (<see cref="Row"/>), else the whole form in the middle of the line's height (§442).
    /// </summary>
    private void Line(LineSketch sketch, D2D_RECT_F box, in TileStyle style)
    {
        var now = Clock.Read(options.TimeZone, options.Seconds);
        var place = Place(now);
        var height = box.bottom - box.top;
        if (height + 0.5f < Whole(style, style.Text(LineType.HeroDip), place.Length > 0))
        {
            Row(sketch, box, style, now, place);
            return;
        }
        var timePx = style.Text(height + 0.5f >= Whole(style, style.Text(LineType.DisplayDip), place.Length > 0) ? LineType.DisplayDip : LineType.HeroDip);
        switch (Look)
        {
            case ClockStyle.TimeOnly:
                Alone(sketch, box, style, now, place, timePx);
                break;
            case ClockStyle.Stacked:
                Stacked(sketch, box, style, now, place, timePx);
                break;
            default:
                Faced(sketch, box, style, now, place, timePx);
                break;
        }
    }

    /// <summary>
    /// One row, the kit's: the clock's glyph in the leading column, where every name begins the place a clock of another
    /// zone or with a label stands for — else the date, but for the time alone, which says no date — and against the
    /// trailing gutter what the look reads: the time in the Value, the face as tall as the row leaves it, or the hours
    /// over the minutes in the Body strong (the mark of a 12-hour clock before them).
    /// </summary>
    private void Row(LineSketch sketch, D2D_RECT_F box, in TileStyle style, Now now, string place)
    {
        var form = ListForm.Of(Gfx, style);
        var height = box.bottom - box.top;
        var gap = style.Px(LineSpace.GapDip);
        var end = box.right - form.Inset;
        var middle = box.top + PopupPaint.Round(height / 2);
        string? reading = null;
        float field;
        switch (Look)
        {
            case ClockStyle.TimeOnly:
                AloneRow(sketch, box, style, now, place);
                return;
            case ClockStyle.Analog:
                field = MathF.Max(1, MathF.Min(height, LineRows.Px(1, style)) - (2 * style.Px(LineSpace.TightDip)));
                sketch.Picture("dial", PopupPaint.Rect(end - field, middle - PopupPaint.Round(field / 2), field, field), Dial(style));
                break;
            case ClockStyle.Stacked:
                field = end - StackedRow(sketch, style, Split(now.Time), end, middle);
                break;
            default:
                reading = now.Time;
                field = PopupPaint.Measure(Gfx, TimeShape(now.Time), style.Text(LineType.ValueDip), Weight.Semibold).X;
                break;
        }
        var hasPlace = place.Length > 0;
        var room = TilePaint.Width(box) - form.TextLeft - form.Inset - field - gap;
        var name = ListTiers.Name(Gfx, hasPlace ? [place, Name(now)] : DateForms(now), form.TextPx, Weight.Regular, room) ?? "";
        LineRow.Draw(sketch, box.left, box.right, box.top, style, new RowSpec(name)
        {
            Glyph = Info.Glyph,
            Reading = reading,
            Trailing = reading is null ? field + gap : 0,
            NamePiece = hasPlace ? "place" : "date",
            ReadingPiece = "time",
        }, height);
    }

    /// <summary>
    /// The time alone on one row: the clock's glyph, the time in the Value where every name begins — it is what the row
    /// says — and the place a clock of another zone stands for, quiet, against the trailing gutter, in the room the time
    /// leaves it. No date.
    /// </summary>
    private void AloneRow(LineSketch sketch, D2D_RECT_F box, in TileStyle style, Now now, string place)
    {
        var form = ListForm.Of(Gfx, style);
        var height = box.bottom - box.top;
        var valuePx = style.Text(LineType.ValueDip);
        var field = PopupPaint.Measure(Gfx, TimeShape(now.Time), valuePx, Weight.Semibold).X;
        var room = TilePaint.Width(box) - form.TextLeft - form.Inset - field - style.Px(LineSpace.SectionDip);
        var said = place.Length > 0 ? ListTiers.Name(Gfx, [place, Name(now)], form.TextPx, Weight.Regular, room) : null;
        var laid = LineRow.Draw(sketch, box.left, box.right, box.top, style, new RowSpec("")
        {
            Glyph = Info.Glyph,
            Reading = said,
            Look = RowReading.Body,
            ReadingInk = style.Theme.TextSecondary,
            ReadingPiece = "place",
        }, height);
        var line = TilePaint.LineHeight(Gfx, valuePx, Weight.Semibold);
        var end = laid.Reading is { } reading ? reading.left - style.Px(LineSpace.GapDip) : box.right - form.Inset;
        sketch.Text("time", now.Time, valuePx, Weight.Semibold, style.Theme.Text, box.left + form.TextLeft, laid.Middle - PopupPaint.Round(line / 2),
            MathF.Max(1, end - box.left - form.TextLeft));
    }

    /// <summary>
    /// The hours over the minutes in the Body strong, their ends on <paramref name="end"/>, the two in the middle of
    /// the row at <paramref name="middle"/>; a 12-hour clock's mark before them in the Caption. Returns where they begin.
    /// </summary>
    private float StackedRow(LineSketch sketch, in TileStyle style, Parts parts, float end, float middle)
    {
        var theme = style.Theme;
        var px = style.Text(LineType.BodyDip);
        var line = TilePaint.LineHeight(Gfx, px, Weight.Semibold);
        // As far apart as the row lets them, a hair from its edges: small figures close together would read as one.
        var pitch = MathF.Max(PopupPaint.Round(px * PitchOfSize), MathF.Min(line, LineRows.Px(1, style) - line - (2 * style.Px(LineSpace.HairDip))));
        var top = middle - PopupPaint.Round((pitch + line) / 2);
        var room = MathF.Max(1, end);
        var hours = sketch.TextRight("hours", parts.Hours, px, Weight.Semibold, theme.Text, end, top, room);
        var minutes = sketch.TextRight("minutes", parts.Minutes, px, Weight.Semibold, theme.Text, end, top + pitch, room);
        var left = MathF.Min(hours.left, minutes.left);
        if (parts.Mark.Length > 0)
        {
            var captionPx = style.Text(LineType.CaptionDip);
            var mark = sketch.TextRight("mark", parts.Mark, captionPx, Weight.Regular, theme.TextSecondary, left - style.Px(LineSpace.TightDip),
                middle - PopupPaint.Round(TilePaint.LineHeight(Gfx, captionPx) / 2), room);
            left = mark.right > mark.left ? mark.left : left;
        }
        return left;
    }

    /// <summary>
    /// The time and the date beside a face (the clock as it was), or the face alone with the date beside it (Analog): the
    /// time as the Display (the Hero where the line has not the room) on the gutter, the date under it, the place under
    /// that — the words in the middle of the face's height (§442) — and a face of the time against the trailing gutter as
    /// tall as the line leaves it, where the words keep beside it the room they need; the face alone keeps its face and
    /// gives its words what is left.
    /// </summary>
    private void Faced(LineSketch sketch, D2D_RECT_F box, in TileStyle style, Now now, string place, float timePx)
    {
        var form = ListForm.Of(Gfx, style);
        var theme = style.Theme;
        var dates = DateForms(now);
        var hasPlace = place.Length > 0;
        var digital = Look != ClockStyle.Analog;
        var height = box.bottom - box.top;
        var left = box.left + form.Inset;
        var right = box.right - form.Inset;
        var top = box.top + style.Px(LineSpace.TightDip);
        var gap = style.Px(LineSpace.SectionDip);
        var side = MathF.Min(height - (2 * style.Px(LineSpace.TightDip)), (right - left) * 0.4f);
        var shape = TimeShape(now.Time);
        var need = MathF.Max(digital ? PopupPaint.Measure(Gfx, shape, timePx, Weight.Semibold).X : 0,
            dates.Min(date => PopupPaint.Measure(Gfx, date, form.TextPx, Weight.Regular).X));
        var face = digital ? side >= style.Px(FaceLeastDip) && right - left - side - gap >= need : side >= style.Px(FaceSmallestDip);
        var words = MathF.Max(1, face ? right - left - side - gap : right - left);
        var captionLine = hasPlace ? TilePaint.LineHeight(Gfx, style.Text(LineType.CaptionDip)) : 0;
        var block = digital ? Words(style, timePx, hasPlace) : form.Line + captionLine;
        // The words in the middle of the face's height, so neither leaves a band of air under it (§442).
        var y = MathF.Max(top, box.top + PopupPaint.Round((height - block) / 2));
        if (digital)
        {
            // The time whole across the words' room: a step down the ramp where it is wider (a narrow list, a large text).
            var fitted = LineType.Largest(style, timePx, px => PopupPaint.Measure(Gfx, shape, px, Weight.Semibold).X <= words);
            var timeLine = TilePaint.LineHeight(Gfx, timePx, Weight.Semibold);
            sketch.Text("time", now.Time, fitted, Weight.Semibold, theme.Text, left, y + PopupPaint.Round((timeLine - TilePaint.LineHeight(Gfx, fitted, Weight.Semibold)) / 2), words);
            y += timeLine;
        }
        // The face alone says the date in the text's ink: it is the line's only words.
        var date = ListTiers.Name(Gfx, dates, form.TextPx, Weight.Regular, words) ?? "";
        sketch.Text("date", date, form.TextPx, Weight.Regular, digital ? theme.TextSecondary : theme.Text, left, y, words);
        if (hasPlace)
        {
            sketch.Text("place", place, style.Text(LineType.CaptionDip), Weight.Regular, digital ? theme.TextTertiary : theme.TextSecondary, left, y + form.Line, words);
        }
        if (face)
        {
            // In the middle of the line's height: narrower than the line is tall at a large text, it leaves air over and under it alike.
            sketch.Picture("dial", PopupPaint.Rect(right - side, box.top + PopupPaint.Round((height - side) / 2), side, side), Dial(style));
        }
    }

    /// <summary>
    /// The time alone (§443, the person: "the mode where it just shows the time without the date"): the time as the
    /// Display on the gutter, whole across the line (a step down the ramp where it is wider), the place a clock of another
    /// zone stands for under it, the two in the middle of the line's height. No date at any size.
    /// </summary>
    private void Alone(LineSketch sketch, D2D_RECT_F box, in TileStyle style, Now now, string place, float timePx)
    {
        var form = ListForm.Of(Gfx, style);
        var theme = style.Theme;
        var room = MathF.Max(1, TilePaint.Width(box) - (2 * form.Inset));
        var left = box.left + form.Inset;
        var shape = TimeShape(now.Time);
        var fitted = LineType.Largest(style, timePx, px => PopupPaint.Measure(Gfx, shape, px, Weight.Semibold).X <= room);
        var timeLine = TilePaint.LineHeight(Gfx, timePx, Weight.Semibold);
        var captionPx = style.Text(LineType.CaptionDip);
        var block = timeLine + (place.Length > 0 ? TilePaint.LineHeight(Gfx, captionPx) : 0);
        var y = box.top + MathF.Max(style.Px(LineSpace.TightDip), PopupPaint.Round((box.bottom - box.top - block) / 2));
        sketch.Text("time", now.Time, fitted, Weight.Semibold, theme.Text, left, y + PopupPaint.Round((timeLine - TilePaint.LineHeight(Gfx, fitted, Weight.Semibold)) / 2), room);
        if (place.Length > 0)
        {
            sketch.Text("place", place, captionPx, Weight.Regular, theme.TextSecondary, left, y + timeLine, room);
        }
    }

    /// <summary>
    /// The hours over the minutes (§443, the person: "two numbers"): both as the Display on the gutter, the minutes close
    /// under the hours, the pair in the middle of the line's height; the seconds where the clock shows them after the
    /// minutes and a 12-hour clock's mark after the hours, in the Value, quiet, on their figures' line; and beside the pair
    /// the date and the place, in the middle too, where the line has the room.
    /// </summary>
    private void Stacked(LineSketch sketch, D2D_RECT_F box, in TileStyle style, Now now, string place, float timePx)
    {
        var form = ListForm.Of(Gfx, style);
        var theme = style.Theme;
        var parts = Split(now.Time);
        var height = box.bottom - box.top;
        var left = box.left + form.Inset;
        var right = box.right - form.Inset;
        var line = TilePaint.LineHeight(Gfx, timePx, Weight.Semibold);
        var pitch = PopupPaint.Round(timePx * PitchOfSize);
        var top = box.top + MathF.Max(0, PopupPaint.Round((height - (pitch + line)) / 2));
        var room = MathF.Max(1, right - left);
        var hours = sketch.Text("hours", parts.Hours, timePx, Weight.Semibold, theme.Text, left, top, room);
        var minutes = sketch.Text("minutes", parts.Minutes, timePx, Weight.Semibold, theme.Text, left, top + pitch, room);
        // The small figures on the large ones' line: their tops as far under it as their ascents are short of the large.
        var smallPx = style.Text(LineType.ValueDip);
        var drop = PopupPaint.Round((line - TilePaint.LineHeight(Gfx, smallPx, Weight.Semibold)) * 0.78f);
        var hair = style.Px(LineSpace.HairDip);
        var end = MathF.Max(hours.right, minutes.right);
        if (parts.Mark.Length > 0)
        {
            end = MathF.Max(end, sketch.Text("mark", parts.Mark, smallPx, Weight.Semibold, theme.TextSecondary, hours.right + hair, top + drop, MathF.Max(1, right - hours.right - hair)).right);
        }
        if (parts.Seconds.Length > 0)
        {
            end = MathF.Max(end, sketch.Text("seconds", parts.Seconds, smallPx, Weight.Semibold, theme.TextSecondary, minutes.right + hair, top + pitch + drop, MathF.Max(1, right - minutes.right - hair)).right);
        }
        var wordsLeft = end + style.Px(LineSpace.SectionDip);
        var words = right - wordsLeft;
        var dates = DateForms(now);
        var date = ListTiers.Name(Gfx, dates, form.TextPx, Weight.Regular, words);
        if (date is null || words < 1)
        {
            return;
        }
        var captionPx = style.Text(LineType.CaptionDip);
        var block = form.Line + (place.Length > 0 ? TilePaint.LineHeight(Gfx, captionPx) : 0);
        var y = box.top + PopupPaint.Round((height - block) / 2);
        sketch.Text("date", date, form.TextPx, Weight.Regular, theme.TextSecondary, wordsLeft, y, words);
        if (place.Length > 0)
        {
            sketch.Text("place", place, captionPx, Weight.Regular, theme.TextTertiary, wordsLeft, y + form.Line, words);
        }
    }

    /// <summary>The time's parts as Stacked stands them: the hours, the minutes, the seconds (empty without), and a 12-hour clock's mark.</summary>
    internal readonly record struct Parts(string Hours, string Minutes, string Seconds, string Mark);

    /// <summary>
    /// <paramref name="time"/>, as Windows wrote it in the region's own format, in its parts: its runs of figures the
    /// hours, the minutes and the seconds; what stands before its first figure or after its last ("PM", "午後") its mark.
    /// A time of one run of figures is all hours.
    /// </summary>
    internal static Parts Split(string time)
    {
        var runs = new List<string>();
        var first = -1;
        var last = -1;
        for (var at = 0; at < time.Length;)
        {
            if (!char.IsDigit(time[at]))
            {
                at++;
                continue;
            }
            var start = at;
            while (at < time.Length && char.IsDigit(time[at]))
            {
                at++;
            }
            runs.Add(time[start..at]);
            first = first < 0 ? start : first;
            last = at;
        }
        if (runs.Count == 0)
        {
            return new Parts(time.Trim(), "", "", "");
        }
        var mark = $"{time[..first].Trim()} {time[last..].Trim()}".Trim();
        return new Parts(runs[0], runs.Count > 1 ? runs[1] : "", runs.Count > 2 ? runs[2] : "", mark);
    }

    /// <summary>The date's forms, longest first: the long date without the year, the weekday and the short date, the short date.</summary>
    private string[] DateForms(Now now)
    {
        var day = DateOnly.FromDateTime(Clock.NowIn(options.TimeZone));
        return [Panels.Nls.LongDateWithoutYear(day), Panels.Nls.DayAndShortDate(day, year: false), Panels.Nls.ShortDateWithoutYear(day)];
    }

    /// <summary>
    /// What the clock stands for, in a line: its label or its zone's city, and how far that is from
    /// this PC's time ("Tokyo · 7 h ahead"); empty for this PC's own time with no label.
    /// </summary>
    private string Place(Now now)
    {
        var name = Name(now);
        var offset = Offset();
        return name.Length == 0 ? offset
            : offset.Length == 0 ? name
            : $"{name} · {offset}";
    }

    /// <summary>Its label, else the first city of its zone's name; empty for this PC's time without a label.</summary>
    private string Name(Now now)
    {
        if (options.Label.Length > 0)
        {
            return options.Label;
        }
        // "(UTC+09:00) Osaka, Sapporo, Tokyo" → "Osaka".
        return Clock.City(now.Zone);
    }

    /// <summary>"8 h ahead", "5 h 30 min behind": how far the zone is from this PC's time; empty for none.</summary>
    private string Offset()
    {
        if (options.TimeZone.Length == 0)
        {
            return "";
        }
        var minutes = (int)Math.Round((Clock.NowIn(options.TimeZone) - Clock.Now()).TotalMinutes / 15) * 15;
        if (minutes == 0)
        {
            return S.WidgetClockSameTime;
        }
        var span = Math.Abs(minutes);
        var text = span % 60 == 0
            ? S.WidgetCommonHours(span / 60)
            : $"{S.WidgetCommonHours(span / 60)} {S.WidgetCommonMinutes(span % 60)}";
        return minutes > 0 ? S.WidgetClockAhead(text) : S.WidgetClockBehind(text);
    }
}
