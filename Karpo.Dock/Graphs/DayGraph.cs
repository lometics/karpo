// A day drawn as a line: a fixed axis from one midnight to the next, a smooth
// curve through the points there are, a mark where the moment of interest
// falls, and rows of labels under it at an even rhythm.
//
// It began as the weather's hourly graph and is shared, because a day of
// temperatures and a day of prices are the same drawing with a different
// series: the weather fills it with hours and glyphs, the stock widget with
// trades. What differs is what a point carries; none of the geometry differs at
// all, and a second copy of it would have drifted from this one within a month.
// Nothing is shaded behind the line and nothing crosses it: §572 took the
// weather's shaded night and its sunrise and sunset hairlines off (the person,
// 2026-11-08: "day night should not be displayed on the weather widget anymore").
//
// The axis is a span of the day rather than the data: by default all twenty-four
// hours, whatever time it is and whatever the answer happens to cover, so the
// morning is on the left and the night on the right wherever the clock has got
// to, and a series that starts at nine leaves the small hours empty rather than
// stretching itself across them. A caller whose day is shorter than the clock's
// says so — a market's day is its session, and seventeen hours of shut exchange
// is not what the panel is for — but within that span the axis is still fixed,
// so the line does not stretch or shrink as the day fills in.
//
// Nothing here animates and nothing reads the clock; the caller says where the
// mark goes.
//
// One way across (§632): the graph is its line's picture and runs the line's whole
// width, edge to edge, in equal slices, a whole hour each, every hour's point in the
// middle of its slice — the slice that lights under the pointer, as wide as every
// other hour's (Axis, Slices). What it writes keeps to the list's gutter: every
// column still stands on its own hour, and one that would reach past the gutter is
// not written at all (Gutter).

using Karpo.Dock.QuickSettings.Controls;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock.Graphs;

/// <summary>
/// One point of a day: where it sits on the axis (hours since midnight, so 14.0 is two in the
/// afternoon), the hour written out, an optional glyph over it, the value written out, a quieter
/// note under it, and the number itself, which is what the line is drawn from.
/// </summary>
internal readonly record struct GraphPoint(double At, string Label, string Glyph, string Value, string Note, double? Number);

/// <summary>
/// One column of the graph: the point it stands for, the x its every row is centred on, how far
/// that column reaches either side of it, and whether it carries a label and a value or is one of
/// the glyphs in between.
/// </summary>
internal readonly record struct GraphColumn(int Index, float Centre, float Half, float TextHalf, bool Labelled)
{
    public float Left => Centre - Half;

    public float Right => Centre + Half;
}

internal sealed unsafe class DayGraph : Widget
{
    /// <summary>The axis is a whole day, midnight to midnight, unless a caller narrows it.</summary>
    public const int AxisHours = 24;

    /// <summary>The first hour of the axis, and the last: midnight to midnight by default.</summary>
    public double From { get; set; }

    public double To { get; set; } = AxisHours;

    /// <summary>How many hours the axis spans; never zero, so nothing divides by it.</summary>
    private float Span => MathF.Max(0.5f, (float)(To - From));

    /// <summary><see cref="NowAt"/> when there is nothing to mark.</summary>
    public const double NoMark = -1;

    private const float GlyphDip = 18;

    /// <summary>The curve's band, DIP: the panel's, a stock's and a Compact hourly forecast's (§590).</summary>
    public const float BandDip = 64;

    /// <summary>§590: a Full hourly forecast's band, the same hours, glyphs and values on a curve half as tall again.</summary>
    public const float TallBandDip = 96;

    /// <summary>How tall the curve's band is, DIP: <see cref="BandDip"/> unless a line's size says otherwise.</summary>
    public float Band { get; set; } = BandDip;

    /// <summary>The line keeps this much room below it inside the band; above it, room for a value.</summary>
    private const float InsetDip = 14;

    /// <summary>The mark: a filled disc with a ring of the panel behind it.</summary>
    private const float MarkDip = 5;

    /// <summary>How near a whole hour a point has to be to stand for it: half a minute.</summary>
    private const double Beat = 0.5 / 60;

    public IReadOnlyList<GraphPoint> Points { get; set; } = [];

    /// <summary>Where the mark falls, in hours since midnight; <see cref="NoMark"/> for none.</summary>
    public double NowAt { get; set; } = NoMark;

    /// <summary>The value the mark sits at, in the same unit as the points.</summary>
    public double? NowValue { get; set; }

    /// <summary>
    /// The points that may carry a label, by index, for a series that is not a day of hours — a
    /// share over five days or five years, one step a point, whose labels are the first point of
    /// each day, month or year rather than a rhythm of hours. Null for a day: every point may, at
    /// a rhythm of whole hours (<see cref="Plan"/>). With them, the labels go at a rhythm of the
    /// ticks instead — every one, every second, every third — counted back from the last, and the
    /// last point need not be one of them.
    /// </summary>
    public IReadOnlyList<int>? Ticks { get; set; }

    /// <summary>The colour a glyph is drawn in; the text's own when nothing says otherwise.</summary>
    public Func<string, FlyoutTheme, DXGI_RGBA>? GlyphInk { get; set; }

    /// <summary>The colour of the line and the mark; the accent when nothing says otherwise.</summary>
    public Func<QuickTheme, DXGI_RGBA>? LineInk { get; set; }

    /// <summary>
    /// The list's gutter, in px, that what the graph writes keeps inside: the day runs the widget's
    /// whole width, edge to edge (<see cref="Axis"/>), and every column still stands on its own
    /// hour, so the gaps between them stay equal; a column whose words or glyph would reach past the
    /// gutter is left unwritten rather than pulled in beside its neighbour. The day's ends are the
    /// line's own edges, so the midnights need no label to say where they are.
    /// </summary>
    public float Gutter { get; set; }

    public override bool HitTestVisible => false;

    /// <summary>
    /// The point lit under the pointer or the keys, by index; -1 for none. A ring stands on the line
    /// where it is (the weather's hours, docs\widgets.md §131); the hour's band is its caller's.
    /// </summary>
    public int LitPoint { get; set; } = -1;

    /// <summary>§632: how many slices the axis is cut in: a whole hour each, from <see cref="From"/> to <see cref="To"/>, both ends' hours their own.</summary>
    internal int Slices => (int)MathF.Round(Span) + 1;

    /// <summary>§632: the slice point <paramref name="index"/> stands in the middle of, from 0: its hour's, the band it lights (LineItem.Across).</summary>
    internal int SliceOf(int index) => Math.Clamp((int)Math.Round(Points[index].At - From), 0, Slices - 1);

    /// <summary>
    /// How tall it is with a band of <paramref name="band"/> DIP. The same whatever it holds, so nothing moves when the
    /// mark appears or the hour turns; the row for a note is kept even where no point has one.
    /// </summary>
    public static float Measure(QuickContext context, int count, bool glyphs = true, float band = BandDip) =>
        count <= 0 ? 0 : context.T(16) + context.D(2) + (glyphs ? context.D(GlyphDip) + context.D(4) : 0)
            + context.D(band) + context.T(16) + context.D(6);

    /// <summary>
    /// Where the day's first hour stands across the widget, and how wide an hour is (§632): the
    /// widget's whole width in <see cref="Slices"/> equal slices, edge to edge, every hour in the
    /// middle of its own — so the first and the last hour's slices are as wide as every other's,
    /// and the gaps between the hours are equal by construction. Only the columns that stand
    /// inside the <see cref="Gutter"/> are written (<see cref="Plan"/>).
    /// </summary>
    internal (float Edge, float Hour) Axis()
    {
        var hour = MathF.Max(1, Width) / Slices;
        return (hour / 2, hour);
    }

    /// <summary>Where hour <paramref name="at"/> falls across the widget.</summary>
    internal float X(float edge, float hour, double at) => edge + ((float)(at - From) * hour);

    public override void Paint(ID2D1DeviceContext* dc, QuickContext context)
    {
        if (Points.Count == 0)
        {
            return;
        }
        var theme = context.Theme;
        var stroke = MathF.Max(1.5f, context.D(2));
        var labelHeight = context.T(16);
        var glyphs = false;
        foreach (var point in Points)
        {
            glyphs |= point.Glyph.Length > 0;
        }
        var glyph = glyphs ? context.D(GlyphDip) : 0;
        var noteHeight = context.T(16);
        var band = PopupPaint.Rect(0, labelHeight + context.D(2) + (glyphs ? glyph + context.D(4) : 0), Width, context.D(Band));
        var (edge, hour) = Axis();

        var (low, high) = Range();
        var x = new float[Points.Count];
        var y = new float[Points.Count];
        for (var index = 0; index < Points.Count; index++)
        {
            x[index] = X(edge, hour, Points[index].At);
            y[index] = At(context, band, Points[index].Number, low, high);
        }
        Line(dc, context, x, y, stroke);

        // A label, not on, selected or an action: the secondary ink, never the accent (§388).
        var noteInk = theme.HighContrast ? theme.Text : theme.TextSecondary;
        // The mark stays whole inside the line's edges.
        var mark = MarkAt(context, band, edge, hour, low, high);
        foreach (var column in Plan(context, x, hour, glyphs ? glyph : context.D(GlyphDip)))
        {
            var point = Points[column.Index];
            // One centre for the whole column, so a point's label, glyph and value stand on the
            // same line. Three boxes of three widths, each pulled out of the gutter by its own
            // distance, is what used to pull them apart at the ends of the axis.
            if (glyphs && point.Glyph.Length > 0)
            {
                PopupPaint.Glyph(dc, context.Gfx, point.Glyph, LineGlyph.RowDip * context.Scale * context.TextScale, Ink(point.Glyph, theme.Popup),
                    PopupPaint.Rect(column.Centre - (glyph / 2), labelHeight + context.D(2), glyph, glyph));
            }
            if (!column.Labelled)
            {
                continue;
            }
            var label = Label.WidthOf(context, point.Label, LabelStyle.Caption);
            PopupPaint.Centred(dc, context.Gfx, point.Label, context.SmallPx, Weight.Regular, theme.TextSecondary,
                PopupPaint.Rect(column.Centre - (label / 2), 0, label, labelHeight), ink: false);
            // The value sits above its own point, in the room the band's inset keeps for it — unless
            // the mark stands there: the mark is that reading, the number is at the top of the panel
            // in the largest type on it, and the disc drawn over it left half a number showing.
            var value = PopupPaint.Measure(context.Gfx, point.Value, context.SmallPx, Weight.Semibold);
            var valueTop = y[column.Index] - context.D(5) - value.Y;
            if (mark is not { } disc || !Overlaps(disc, column.Centre - (value.X / 2), valueTop, value.X, value.Y))
            {
                PopupPaint.Text(dc, context.Gfx, point.Value, context.SmallPx, Weight.Semibold, theme.Text,
                    column.Centre - (value.X / 2), valueTop, value.X);
            }
            if (point.Note.Length > 0)
            {
                // The kit's Caption, never under it (§388).
                var small = context.SmallPx;
                var note = PopupPaint.Measure(context.Gfx, point.Note, small, Weight.Regular);
                PopupPaint.Centred(dc, context.Gfx, point.Note, small, Weight.Regular, noteInk,
                    PopupPaint.Rect(column.Centre - (note.X / 2), band.bottom, note.X, noteHeight), ink: false);
            }
        }
        if (mark is { } at)
        {
            Mark(dc, context, at);
        }
        // The point lit: a ring on the line, the line's colour round the panel's.
        if (LitPoint >= 0 && LitPoint < Points.Count && Points[LitPoint].Number is not null)
        {
            var centre = new System.Numerics.Vector2(x[LitPoint], y[LitPoint]);
            var radius = context.D(MarkDip);
            PopupPaint.Circle(dc, centre, radius, LineInk?.Invoke(theme) ?? (theme.HighContrast ? theme.Text : theme.Accent));
            PopupPaint.Circle(dc, centre, radius - MathF.Max(1.5f, context.D(2)), PopupPaint.Ink(theme.Popup.Base));
        }
    }

    /// <summary>Whether the mark's disc, ring and all, reaches into a box of text.</summary>
    private static bool Overlaps((System.Numerics.Vector2 Centre, float Radius, float Ring) mark, float left, float top, float width, float height) =>
        mark.Centre.X + mark.Ring > left && mark.Centre.X - mark.Ring < left + width
        && mark.Centre.Y + mark.Ring > top && mark.Centre.Y - mark.Ring < top + height;

    private DXGI_RGBA Ink(string glyph, FlyoutTheme theme) => GlyphInk?.Invoke(glyph, theme) ?? theme.Text;

    /// <summary>
    /// The columns, worked out once and used for every row of them, so a point's label, glyph and
    /// value cannot drift apart.
    ///
    /// Labels go at as wide a rhythm as the widest of them needs, counted back from the last point,
    /// which therefore always carries one. Counting back from the end rather than forward from
    /// midnight is what lets the same graph label a series that does not begin on the hour: a day
    /// of temperatures ending at midnight labels the whole hours as it always did, and a day of
    /// trades ending at the closing bell labels its own hours rather than none at all.
    ///
    /// Glyphs go at a stride that **divides** the labels', so every labelled hour carries one and
    /// the two rows never fall out of phase, and as densely as they fit — about twice as often as
    /// the labels where there is room. A point whose label did not fit carries nothing at all, so
    /// there is never half a column standing.
    /// </summary>
    internal List<GraphColumn> Plan(QuickContext context, float[] x, float hour, float glyph)
    {
        var gap = context.D(4);
        var last = Points.Count - 1;

        var stride = 0;
        var labelled = new List<GraphColumn>();
        if (Ticks is { } ticks)
        {
            // A range's ticks: the densest rhythm of them whose labels stand apart, and no glyphs.
            for (var every = 1; every <= Math.Max(1, ticks.Count); every++)
            {
                if (Even(every) is { Count: > 0 } row)
                {
                    labelled = row;
                    break;
                }
            }
            return labelled;
        }
        for (var every = 1; every <= AxisHours; every++)
        {
            var row = Even(every);
            if (row.Count == 0)
            {
                continue;
            }
            if (stride == 0)
            {
                (stride, labelled) = (every, row);
            }
            // A rhythm the glyphs can halve is worth a slightly emptier row of labels: the glyph
            // row is meant to be about twice as full, and it can only be so at a stride it divides.
            if (Denser(every))
            {
                (stride, labelled) = (every, row);
                break;
            }
            if (every >= 2 * stride)
            {
                break;
            }
        }
        // Nowhere near enough room for a row of labels: the glyphs carry the day on their own.
        if (stride == 0)
        {
            stride = AxisHours;
        }

        var columns = new List<GraphColumn>(labelled);
        var glyphStride = Halved(stride);
        var kept = labelled.Select(column => column.Index).ToHashSet();
        for (var index = 0; index < Points.Count; index++)
        {
            if (!OnBeat(index, glyphStride) || kept.Contains(index))
            {
                continue;
            }
            var column = Column(index, carries: false);
            // A glyph only ever has to clear another glyph: the labels and the values are rows of
            // their own, further up and further down.
            if (Fits(column) && columns.TrueForAll(other => MathF.Abs(other.Centre - column.Centre) >= glyph + gap))
            {
                columns.Add(column);
            }
        }
        columns.Sort((left, right) => left.Centre.CompareTo(right.Centre));
        return columns;

        int Halved(int every)
        {
            for (var each = 1; each <= every; each++)
            {
                if (every % each == 0 && glyph + gap <= each * hour)
                {
                    return each;
                }
            }
            return every;
        }

        bool Denser(int every) => every > 1 && Halved(every) <= every / 2;

        /// <summary>
        /// Whether this point falls a whole number of <paramref name="every"/> hours before the
        /// last one. Hours rather than indices: a series may be one point an hour or one every
        /// five minutes, and the rhythm on show is a rhythm of the axis either way.
        /// </summary>
        bool OnBeat(int index, int every)
        {
            var steps = (Points[last].At - Points[index].At) / every;
            return Math.Abs(steps - Math.Round(steps)) <= Beat / every;
        }

        List<GraphColumn> Even(int every)
        {
            var row = new List<GraphColumn>();
            var candidates = Ticks ?? Enumerable.Range(0, Points.Count).ToList();
            for (var at = 0; at < candidates.Count; at++)
            {
                var index = candidates[at];
                if (index < 0 || index > last || (Ticks is null ? !OnBeat(index, every) : (candidates.Count - 1 - at) % every != 0))
                {
                    continue;
                }
                var column = Column(index, carries: true);
                // On a list's line an hour whose words would reach past the gutter goes unwritten,
                // and the rhythm carries on either side of it.
                if (!Fits(column))
                {
                    continue;
                }
                if (row.Count > 0 && column.Centre - column.TextHalf < row[^1].Centre + row[^1].TextHalf + gap)
                {
                    return [];
                }
                row.Add(column);
            }
            // The day's ends are the line's edges, and the last point may be one of the hours too
            // near an edge to be written.
            return row.Count >= (Ticks is null ? 3 : 2) ? row : [];
        }

        // Whether a column stands whole inside the line's gutter.
        bool Fits(GraphColumn column) => column.Left >= Gutter - 0.5f && column.Right <= Width - Gutter + 0.5f;

        GraphColumn Column(int index, bool carries)
        {
            var point = Points[index];
            var text = carries
                ? MathF.Max(
                    Label.WidthOf(context, point.Label, LabelStyle.Caption),
                    PopupPaint.Measure(context.Gfx, point.Value, context.SmallPx, Weight.Semibold).X) / 2
                : 0;
            // No clamping: a centre is simply where its hour falls, so the gaps stay equal; a column
            // that would cross the gutter is left out (Fits).
            return new GraphColumn(index, x[index], MathF.Max(glyph / 2, text), text, carries);
        }
    }

    /// <summary>The lowest and the highest of the points the line is drawn between.</summary>
    private (double Low, double High) Range()
    {
        var low = double.MaxValue;
        var high = double.MinValue;
        foreach (var point in Points)
        {
            if (point.Number is { } value)
            {
                low = Math.Min(low, value);
                high = Math.Max(high, value);
            }
        }
        return low > high ? (0, 0) : (low, high);
    }

    /// <summary>
    /// Where <paramref name="value"/> sits in the band; the middle when every point is the same.
    /// The room above the line is a whole line of text, because the value is written there:
    /// without it the highest point's number would climb out of the band and into the glyphs.
    /// </summary>
    private float At(QuickContext context, D2D_RECT_F band, double? value, double low, double high)
    {
        var top = band.top + context.T(16) + context.D(6);
        var bottom = band.bottom - context.D(InsetDip);
        if (value is not { } number || high - low <= 0)
        {
            return PopupPaint.Round((top + bottom) / 2);
        }
        return bottom - (float)((number - low) / (high - low) * (bottom - top));
    }

    /// <summary>
    /// The line: one smooth geometry through the points, as a monotone cubic (Fritsch-Carlson, in
    /// <see cref="Weather.WeatherCurve"/>) turned segment by segment into the cubic Bézier that
    /// equals it. Smooth at any zoom without sampling, and never rising above the highest point or
    /// dipping below the lowest, which a plain spline's overshoot would have invented.
    ///
    /// A gap in the series stays a gap: each run of points an hour apart is its own figure.
    /// </summary>
    private void Line(ID2D1DeviceContext* dc, QuickContext context, float[] x, float[] y, float thickness)
    {
        var runs = new List<int[]>();
        var run = new List<int>();
        for (var index = 0; index < Points.Count; index++)
        {
            if (Points[index].Number is null)
            {
                Close(run);
                continue;
            }
            if (run.Count > 0 && Points[index].At - Points[run[^1]].At > Gap)
            {
                Close(run);
            }
            run.Add(index);
        }
        Close(run);
        Smooth(dc, [.. runs.Select(figure => (figure.Select(index => x[index]).ToArray(), figure.Select(index => y[index]).ToArray()))],
            thickness, LineInk?.Invoke(context.Theme) ?? (context.Theme.HighContrast ? context.Theme.Text : context.Theme.Accent));

        void Close(List<int> figure)
        {
            if (figure.Count > 1)
            {
                runs.Add([.. figure]);
            }
            figure.Clear();
        }
    }

    /// <summary>
    /// Each figure's points joined as one smooth curve in <paramref name="ink"/>: the monotone cubic of
    /// <see cref="Weather.WeatherCurve"/>, segment by segment the Bézier that equals it, round at its ends. The day's
    /// line, and the week's highs and lows (§590, WeatherDaysGraphTile). A figure of fewer than two points draws nothing.
    /// </summary>
    internal static void Smooth(ID2D1DeviceContext* dc, IReadOnlyList<(float[] X, float[] Y)> figures, float thickness, DXGI_RGBA ink)
    {
        if (!figures.Any(figure => figure.X.Length > 1))
        {
            return;
        }
        ID2D1Factory* factory = null;
        ID2D1PathGeometry* path = null;
        ID2D1GeometrySink* sink = null;
        ID2D1SolidColorBrush* brush = null;
        ID2D1StrokeStyle* round = null;
        try
        {
            dc->GetFactory(&factory);
            if (factory == null || factory->CreatePathGeometry(&path).FAILED || path->Open(&sink).FAILED)
            {
                return;
            }
            foreach (var (xs, ys) in figures)
            {
                if (xs.Length < 2)
                {
                    continue;
                }
                var across = xs.Select(value => (double)value).ToArray();
                var up = ys.Select(value => (double)value).ToArray();
                var slopes = Weather.WeatherCurve.Tangents(across, up);
                sink->BeginFigure(new D2D_POINT_2F((float)across[0], (float)up[0]), D2D1_FIGURE_BEGIN.D2D1_FIGURE_BEGIN_HOLLOW);
                for (var index = 0; index + 1 < across.Length; index++)
                {
                    var (x1, y1, x2, y2) = Weather.WeatherCurve.Bezier(across, up, slopes, index);
                    var curve = new D2D1_BEZIER_SEGMENT
                    {
                        point1 = new D2D_POINT_2F((float)x1, (float)y1),
                        point2 = new D2D_POINT_2F((float)x2, (float)y2),
                        point3 = new D2D_POINT_2F((float)across[index + 1], (float)up[index + 1]),
                    };
                    sink->AddBezier(&curve);
                }
                sink->EndFigure(D2D1_FIGURE_END.D2D1_FIGURE_END_OPEN);
            }
            if (sink->Close().FAILED)
            {
                return;
            }
            var properties = new D2D1_STROKE_STYLE_PROPERTIES
            {
                startCap = D2D1_CAP_STYLE.D2D1_CAP_STYLE_ROUND,
                endCap = D2D1_CAP_STYLE.D2D1_CAP_STYLE_ROUND,
                dashCap = D2D1_CAP_STYLE.D2D1_CAP_STYLE_ROUND,
                lineJoin = D2D1_LINE_JOIN.D2D1_LINE_JOIN_ROUND,
                miterLimit = 10,
                dashStyle = D2D1_DASH_STYLE.D2D1_DASH_STYLE_SOLID,
            };
            _ = factory->CreateStrokeStyle(&properties, null, 0, &round);
            brush = Gfx.Brush(dc, ink);
            dc->DrawGeometry((ID2D1Geometry*)path, (ID2D1Brush*)brush, thickness, round);
        }
        finally
        {
            if (round != null)
            {
                round->Release();
            }
            if (brush != null)
            {
                brush->Release();
            }
            if (sink != null)
            {
                sink->Release();
            }
            if (path != null)
            {
                path->Release();
            }
            if (factory != null)
            {
                factory->Release();
            }
        }
    }

    /// <summary>How far apart two points may be and still be joined: an hour and a half of the axis.</summary>
    private double Gap => Points.Count > 2 ? MathF.Max(1.5f, (float)(Points[^1].At - Points[0].At) / Points.Count * 3) : 1.5;

    /// <summary>
    /// The mark: a filled disc on a ring of the panel's own colour, so it reads over the line
    /// wherever it lands. Still, as everything here is — nothing may depend on an animation. It
    /// carries no number: the value is already at the top of the panel in the largest type on it,
    /// and a second copy at this size would sit on the line it marks.
    /// </summary>
    private void Mark(ID2D1DeviceContext* dc, QuickContext context, (System.Numerics.Vector2 Centre, float Radius, float Ring) mark)
    {
        var theme = context.Theme;
        PopupPaint.Circle(dc, mark.Centre, mark.Ring, PopupPaint.Ink(theme.Popup.Base));
        PopupPaint.Circle(dc, mark.Centre, mark.Radius, LineInk?.Invoke(theme) ?? (theme.HighContrast ? theme.Text : theme.Accent));
    }

    /// <summary>
    /// Where the mark stands and how big it is, or null where there is nothing to mark: worked out
    /// before anything is written, so a value it would sit on can be left out.
    /// </summary>
    private (System.Numerics.Vector2 Centre, float Radius, float Ring)? MarkAt(QuickContext context, D2D_RECT_F band, float edge, float hour, double low, double high)
    {
        if (NowAt <= NoMark || NowValue is null || Points.Count == 0)
        {
            return null;
        }
        var at = Math.Clamp(NowAt, From, To);
        var radius = context.D(MarkDip);
        var ring = radius + MathF.Max(1.5f, context.D(2));
        // A mark at either end of the axis sits on it, so the disc is kept inside the line.
        var centre = new System.Numerics.Vector2(
            Math.Clamp(X(edge, hour, at), ring, MathF.Max(ring, Width - ring)),
            At(context, band, NowValue, low, high));
        return (centre, radius, ring);
    }
}
