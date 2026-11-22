// The tides as a line of a list (docs\widgets.md §236): the sea at its place, read from Open-Meteo's
// marine service through the tides' store (TideStore), which keeps one fetch per place for every
// tides widget showing it.
//
// The place is the line's own (WeatherOptions, as every weather line's). Nothing is asked until the
// line is looked at (§11), then a few times a day; between fetches the next high or low, how soon it
// is and the height now are worked out of the series it holds, every minute.
//
// Two widgets of fixed content (§603; the person, 2026-11-09: "split all of them"), each the same at both of its sizes:
//   tides now    the kit's Row: the tides' drop in the leading column, "Tides", the height now at the end, and which
//                way it goes with the next turn, its height and how soon as the row's detail (§497) — at its end on
//                Compact's one row, under the name as its caption at Full
//   tide graph   the curve from three hours ago to a day on, edge to edge and down to the line's bottom edge, its
//                area filled and no plate behind it (§9, "One gutter"), a mark at now, each high's and low's time by
//                it — its band shorter at Compact, taller at Full
// Where a taller line once showed the curve under the row (§236, §401) it is the tide graph, a widget of its own.
// Nothing come yet or out of reach, it is the kit's one Status, at its size's height all the same. Nothing on it is pressed;
// "Refresh" on its right click fetches it again, the answer's age under it (IRefreshable, §451).

using System.Numerics;
using Karpo.Core;
using Karpo.Dock.Graphs;
using Karpo.Dock.Native;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

/// <summary>
/// Where a tides line's answer comes from, where it is not the store: a test's, so nothing is fetched
/// and no clock is waited on.
/// </summary>
internal sealed class TideSource
{
    public required Func<(TideCache? Shown, string? Problem, bool Fetching)> Read { get; init; }
}

/// <summary>The tides for a place, as a line of a list: the tides now, or the tide graph (see the top of the file).</summary>
internal sealed unsafe class TidesTile : CellIsland, IRefreshable, IListContent
{
    private WeatherOptions options = new();
    private bool looked;

    public TidesTile(string key, WidgetItem island, Gfx gfx, Config config)
        : base(key, island, gfx)
    {
        Graph = island.Info == WidgetInfos.TideGraph;
        Configure(island, config);
    }

    /// <summary>§603: whether it is the tide graph (else the tides now).</summary>
    internal bool Graph { get; }

    /// <summary>The last answer for the place, why a newer one is not there, and whether one is on its way.</summary>
    private (TideCache? Shown, string? Problem, bool Fetching) held;

    /// <summary>Tests only: where the answer comes from instead of the store.</summary>
    internal TideSource? Source { get; set; } = Samples.On ? Samples.TideSource() : null;

    /// <summary>Tests only: heights in feet, or metres, whatever the region says; null for the region's.</summary>
    internal bool? Imperial { get; set; }

    /// <summary>The store this line registers with: the shared one, or a test's own.</summary>
    internal TideStore Store { get; set; } = TideStore.Shared;

    /// <summary>The place it shows.</summary>
    internal WeatherOptions Options => options;

    public override int IntervalMs => options.IsSet ? (int)Store.Every.TotalMilliseconds : 0;

    protected override int Count => 1;

    /// <summary>The graph's curve runs edge to edge, under the folder's rim, as every graph of a list (§192).</summary>
    protected internal override bool UnderRim => Graph;

    public override void Configure(WidgetItem island, Config config)
    {
        Settings = island;
        var next = WeatherPlaces.Settled(island.Weather);
        var same = next.IsSet == options.IsSet && WeatherStore.KeyOf(next) == WeatherStore.KeyOf(options);
        options = next;
        if (looked && !same && Source is null)
        {
            Store.Use(this, options);
            Store.Poll(this);
            Take();
        }
    }

    /// <summary>Nothing set, or this PC's location off with nothing ever fetched: "Set up in Settings" at the place.</summary>
    internal override SetupNeed? Unset =>
        !options.IsSet ? base.Unset
        : LocationOff ? SetupNeed.Of(Key, SetupField.Place)
        : null;

    private bool LocationOff => options.UseDeviceLocation && held.Shown is null && WeatherStore.IsLocationOff(held.Problem);

    public override string? Tooltip(int index) => null;

    public override IslandAction Click(int index) => default;

    /// <summary>A new minute: how soon the next turn is, the height now and the mark move on.</summary>
    public override bool MinuteChanged(Gfx gfx) => Redraw(gfx);

    /// <summary>A place to fetch for ("Refresh", IRefreshable).</summary>
    public bool CanRefresh => options.IsSet;

    /// <summary>A fetch is on its way.</summary>
    public bool Refreshing
    {
        get
        {
            Take();
            return held.Fetching;
        }
    }

    /// <summary>When the answer shown was fetched.</summary>
    public DateTime? UpdatedUtc => held.Shown?.FetchedUtc;

    /// <summary>Why a newer answer is not there.</summary>
    string? IRefreshable.Problem
    {
        get
        {
            Take();
            return held.Problem;
        }
    }

    /// <summary>"Refresh": a fetch now whatever the age.</summary>
    public void RefreshNow()
    {
        Look();
        if (Source is null)
        {
            _ = Store.Ask(this);
        }
        Take();
    }

    /// <summary>
    /// list: its size's form, whatever the answer (§603): the tides now its row (Compact) or its row with the caption
    /// (Full); the tide graph its band with the times over and under it, shorter at Compact.
    /// </summary>
    public float ListHeight(float width, in TileStyle style)
    {
        var compact = LineSizes.Of(Settings) == LineSize.Compact;
        return Graph
            ? style.Px(compact ? CompactBandDip : FullBandDip) + LabelOver(style, ListForm.Of(Gfx, style)) + LabelUnder(style, ListForm.Of(Gfx, style))
            : LineRow.Height(style, caption: !compact);
    }

    public override void Dispose()
    {
        if (looked)
        {
            looked = false;
            Store.Release(this);
        }
        base.Dispose();
    }

    protected override bool Sample(bool everything)
    {
        Look();
        if (Source is null)
        {
            if (everything)
            {
                Store.Deliver(this);
            }
            Store.Poll(this);
        }
        Take();
        return false;
    }

    /// <summary>The first look: the place registered with the store and asked for if it is due (§11). Once.</summary>
    private void Look()
    {
        if (looked || Source is not null)
        {
            return;
        }
        looked = true;
        Store.Use(this, options);
        Store.Poll(this);
    }

    private void Take()
    {
        Look();
        held = Source is { } from ? from.Read() : Store.Read(this);
    }

    /// <summary>What it says now, through Clock.Now so a test can hold the minute still.</summary>
    internal TideReading Read()
    {
        Take();
        return TideReading.From(held.Shown, Clock.Now().ToUniversalTime(), held.Problem, Imperial);
    }

    /// <summary>Never drawn on a bar: on the bar it is the tides face.</summary>
    internal override void PaintCell(ID2D1DeviceContext* dc, int index, in IslandStyle style, float width, float height) =>
        throw new NotSupportedException($"{Info.Id} is a line of a folder's list, not a widget of a bar.");

    // --- The drawing ------------------------------------------------------------------------------

    /// <summary>The room over the curve's band for a high's time.</summary>
    private static float LabelOver(in TileStyle style, in ListForm form) => form.SmallLine + style.Px(2);

    /// <summary>The room under the band for a low's time, clear of the line's bottom edge and its rounded corner.</summary>
    private static float LabelUnder(in TileStyle style, in ListForm form) => form.SmallLine + style.Px(8);

    /// <summary>The graph's band at Compact, DIP: the curve between the times over its highs and under its lows.</summary>
    private const float CompactBandDip = 36;

    /// <summary>The graph's band at Full, DIP.</summary>
    private const float FullBandDip = 76;

    protected internal override void PaintCard(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var reading = Read();
        dc->PushAxisAlignedClip(&box, D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_ALIASED);
        try
        {
            if (!Graph)
            {
                Row(new LineSketch(dc, Gfx), box, style, reading);
            }
            else if (reading.State != TideState.Ready || reading.Curve.Count < 2)
            {
                LineStatus.Draw(new LineSketch(dc, Gfx), box, style, WidgetInfos.Tides.Glyph, reading.State != TideState.Ready ? reading.Why : S.DockTidesNoneAhead, []);
            }
            else
            {
                // The curve takes the whole line, down to its bottom edge with no pad (§192).
                Curve(dc, box, style, ListForm.Of(Gfx, style), reading, box.top, TilePaint.Height(box));
            }
        }
        finally
        {
            dc->PopAxisAlignedClip();
        }
    }

    /// <summary>Tests only: what the tides now's row lays out in <paramref name="box"/>, piece by piece.</summary>
    internal IReadOnlyList<LinePiece> RowPieces(D2D_RECT_F box, in TileStyle style)
    {
        var sketch = new LineSketch(null, Gfx);
        Row(sketch, box, style, Read());
        return sketch.Pieces;
    }

    /// <summary>
    /// The kit's Row: the tides' drop, "Tides", the height now at the end, and its detail — which way it goes, the next
    /// turn with its height and how soon, as much as fits — at the row's end on one row (Compact) or under the name as its
    /// caption (Full, §497). With nothing to show, the kit's one Status.
    /// </summary>
    private void Row(LineSketch sketch, D2D_RECT_F box, in TileStyle style, TideReading reading)
    {
        if (reading.State != TideState.Ready)
        {
            LineStatus.Draw(sketch, box, style, WidgetInfos.Tides.Glyph, reading.Why, []);
            return;
        }
        var form = ListForm.Of(Gfx, style);
        var compact = LineSizes.Of(Settings) == LineSize.Compact;
        var value = reading.HeightText();
        var (px, weight) = LineRow.Type(style, RowReading.Value);
        // The detail as much as the one row holds beside the name, in the Body, at both sizes: Full says the same words
        // under the name as its caption, the line no fuller for being taller (§603).
        var room = TilePaint.Width(box) - form.TextLeft - form.Inset
            - (value.Length > 0 ? PopupPaint.Measure(Gfx, value, px, weight).X + style.Px(LineSpace.GapDip) : 0)
            - PopupPaint.Measure(Gfx, S.CoreFaceTidesTitle, form.TextPx, Weight.Regular).X - style.Px(LineSpace.GapDip);
        var way = reading.Rising is { } rising ? TideText.Way(rising) : null;
        var detail = reading.Next is null
            ? S.DockTidesNoneAhead
            : ListTiers.Whole(Gfx, [LineRow.Join(way, reading.NextAtHeight(), reading.NextIn), LineRow.Join(way, reading.NextAtHeight()), reading.NextAtHeight()],
                form.TextPx, Weight.Regular, room) ?? reading.NextAt;
        LineRow.Draw(sketch, box.left, box.right, box.top, style, new RowSpec(S.CoreFaceTidesTitle)
        {
            Glyph = WidgetInfos.Tides.Glyph,
            Detail = detail,
            Reading = value,
            ReadingInk = reading.Stale ? style.Theme.TextSecondary : null,
            ReadingPiece = "height",
            DetailPiece = "next",
            CaptionPiece = "next",
        }, LineRow.Height(style, caption: !compact));
    }

    /// <summary>
    /// The curve from three hours ago to a day on across the whole line, edge to edge and down to its
    /// bottom edge: its area filled from the line down to <paramref name="top"/> + <paramref name="height"/>, no plate behind it; the time of each high over it and of each low under it,
    /// each centred on its turn and left out where it would reach past the gutter or into another; a
    /// hairline and a mark at now.
    /// </summary>
    private void Curve(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, in ListForm form, TideReading reading, float top, float height)
    {
        var theme = style.Theme;
        var foot = top + height;
        var bandTop = top + LabelOver(style, form);
        var bandBottom = foot - LabelUnder(style, form);
        var left = box.left;
        var width = TilePaint.Width(box);
        var span = (reading.CurveTo - reading.CurveFrom).TotalMinutes;
        var low = double.MaxValue;
        var high = double.MinValue;
        foreach (var sample in reading.Curve)
        {
            if (sample.Height is { } value)
            {
                low = Math.Min(low, value);
                high = Math.Max(high, value);
            }
        }
        if (low > high || span <= 0)
        {
            return;
        }
        // A flat sea still reads as a line in the middle; a tiny range is not stretched into a storm.
        var range = Math.Max(high - low, 0.2);
        var middle = (high + low) / 2;
        float X(DateTime utc) => left + (float)((utc - reading.CurveFrom).TotalMinutes / span * width);
        float Y(double value) => bandBottom - (float)((value - (middle - (range / 2))) / range * (bandBottom - bandTop));
        var accent = theme.HighContrast ? theme.Text : PopupPaint.Ink(theme.Accent);
        var points = new List<Vector2>();
        var runs = new List<List<Vector2>>();
        foreach (var sample in reading.Curve)
        {
            if (sample.Height is not { } value)
            {
                if (points.Count > 1)
                {
                    runs.Add(points);
                }
                points = [];
                continue;
            }
            points.Add(new Vector2(X(sample.Utc), Y(value)));
        }
        if (points.Count > 1)
        {
            runs.Add(points);
        }
        foreach (var run in runs)
        {
            Trace(dc, run, accent, MathF.Max(1.5f, style.Px(2)), bandTop, foot);
        }

        // Now: a hairline from the band's top down to the line's foot, as the fill runs, and the mark on the line.
        var line = MathF.Max(1, style.Px(1));
        var nowX = X(reading.Now);
        PopupPaint.Fill(dc, PopupPaint.Rect(PopupPaint.Round(nowX), bandTop, line, foot - bandTop), 0, IslandInk.Faded(theme.Text, 0.25f));
        if (reading.Height is { } now)
        {
            var radius = style.Px(5);
            var ring = radius + MathF.Max(1.5f, style.Px(2));
            var centre = new Vector2(nowX, Y(now));
            PopupPaint.Circle(dc, centre, ring, PopupPaint.Ink(theme.Base));
            PopupPaint.Circle(dc, centre, radius, accent);
        }

        // The highs' and lows' times, each centred on its own turn, inside the gutter and apart.
        var gutter = form.Inset;
        var apart = style.Gap;
        var written = new List<(float Left, float Right)>();
        foreach (var mark in reading.CurveMarks)
        {
            var x = X(mark.Turn.Utc);
            var y = Y(mark.Turn.Height);
            var text = mark.Time;
            var wide = PopupPaint.Measure(Gfx, text, form.SmallPx, Weight.Regular).X;
            var from = x - (wide / 2);
            var to = x + (wide / 2);
            // A turn whose time is not written has no dot either: a half dot at the line's edge says nothing.
            if (from < left + gutter - 0.5f || to > box.right - gutter + 0.5f || written.Any(other => from < other.Right + apart && to > other.Left - apart))
            {
                continue;
            }
            written.Add((from, to));
            PopupPaint.Circle(dc, new Vector2(x, y), MathF.Max(1.5f, style.Px(2.5f)), accent);
            var at = mark.Turn.High ? y - style.Px(4) - form.SmallLine : y + style.Px(4);
            TilePaint.Line(dc, Gfx, text, form.SmallPx, Weight.Regular, theme.TextSecondary, from, PopupPaint.Round(at), wide + 1);
        }
    }

    /// <summary>One run of the curve: its area under it to <paramref name="foot"/> in the accent's fading fill, and its line.</summary>
    private static void Trace(ID2D1DeviceContext* dc, List<Vector2> run, DXGI_RGBA accent, float stroke, float top, float foot)
    {
        ID2D1Factory* factory = null;
        ID2D1PathGeometry* line = null;
        ID2D1PathGeometry* area = null;
        ID2D1SolidColorBrush* brush = null;
        ID2D1StrokeStyle* joins = null;
        try
        {
            dc->GetFactory(&factory);
            if (factory == null || factory->CreatePathGeometry(&line).FAILED || factory->CreatePathGeometry(&area).FAILED)
            {
                return;
            }
            bool Path(ID2D1PathGeometry* geometry, bool closed)
            {
                ID2D1GeometrySink* into = null;
                if (geometry->Open(&into).FAILED)
                {
                    return false;
                }
                try
                {
                    var first = new D2D_POINT_2F(run[0].X, run[0].Y);
                    into->BeginFigure(closed ? new D2D_POINT_2F(first.x, foot) : first, closed ? D2D1_FIGURE_BEGIN.D2D1_FIGURE_BEGIN_FILLED : D2D1_FIGURE_BEGIN.D2D1_FIGURE_BEGIN_HOLLOW);
                    if (closed)
                    {
                        into->AddLine(first);
                    }
                    for (var index = 1; index < run.Count; index++)
                    {
                        into->AddLine(new D2D_POINT_2F(run[index].X, run[index].Y));
                    }
                    if (closed)
                    {
                        into->AddLine(new D2D_POINT_2F(run[^1].X, foot));
                    }
                    into->EndFigure(closed ? D2D1_FIGURE_END.D2D1_FIGURE_END_CLOSED : D2D1_FIGURE_END.D2D1_FIGURE_END_OPEN);
                    return into->Close().SUCCEEDED;
                }
                finally
                {
                    ComUtil.Release(ref into);
                }
            }
            if (!Path(area, closed: true) || !Path(line, closed: false))
            {
                return;
            }
            GraphBackdrop.Fill(dc, (ID2D1Geometry*)area, accent, top, foot);
            var properties = new D2D1_STROKE_STYLE_PROPERTIES
            {
                startCap = D2D1_CAP_STYLE.D2D1_CAP_STYLE_ROUND,
                endCap = D2D1_CAP_STYLE.D2D1_CAP_STYLE_ROUND,
                lineJoin = D2D1_LINE_JOIN.D2D1_LINE_JOIN_ROUND,
                miterLimit = 1,
            };
            if (factory->CreateStrokeStyle(&properties, null, 0, &joins).FAILED)
            {
                return;
            }
            brush = Gfx.Brush(dc, accent);
            dc->DrawGeometry((ID2D1Geometry*)line, (ID2D1Brush*)brush, stroke, joins);
        }
        finally
        {
            ComUtil.Release(ref joins);
            ComUtil.Release(ref brush);
            ComUtil.Release(ref area);
            ComUtil.Release(ref line);
            ComUtil.Release(ref factory);
        }
    }


}

