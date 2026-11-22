// A markets widget's chart (docs\widgets.md §246, §258): its range's prices behind its words and faded under
// them, in the chart it was set to (MarketChartStyle) — the line with its area under it in the colour of its
// move, the line alone, or candles, each green where it closed up and red where down — one drawing for a
// line in the ticker style, the converter's chart and the face on the bar (Graphs\GraphBackdrop.cs).

using Karpo.Core;
using Karpo.Dock.Graphs;
using Karpo.Dock.Native;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock.Markets;

internal static unsafe class MarketChart
{
    /// <summary>
    /// The tallest the chart under a line's words grows, in DIP: a line taller than its words and this keeps
    /// the chart at its foot this tall, and the room above it empty.
    /// </summary>
    internal const float ChartDip = 60;

    /// <summary>Windows' finance green and red: a move up, a move down.</summary>
    private static readonly DXGI_RGBA Up = Gfx.Ink(0.25f, 0.68f, 0.35f, 1f);

    /// <inheritdoc cref="Up"/>
    private static readonly DXGI_RGBA Down = Gfx.Ink(0.90f, 0.30f, 0.27f, 1f);

    /// <summary>Where a line of two rows or more draws its chart in <paramref name="box"/>: at its foot, no taller than <see cref="ChartDip"/>.</summary>
    internal static D2D_RECT_F Box(D2D_RECT_F box, in TileStyle style) =>
        box with { top = MathF.Max(box.top, box.bottom - style.Px(ChartDip)) };

    /// <summary>
    /// Up in green, down in red, and neither in the quiet ink. In high contrast the theme's own colours stand
    /// instead: a colour nobody chose is worse than none.
    /// </summary>
    internal static DXGI_RGBA Moved(int direction, in Look look, in IslandInk ink) =>
        look.HighContrast || direction == 0 ? ink.Secondary : direction > 0 ? Up : Down;

    /// <summary>Whether candles take the up and down colours: not for a price gone stale, and not in high contrast.</summary>
    internal static bool Coloured(MarketReading reading, in Look look) => !reading.Stale && !look.HighContrast;

    /// <summary>
    /// The chart behind <paramref name="box"/>, edge to edge, faded where the <paramref name="words"/> stand
    /// (Graphs\GraphBackdrop.cs): a line or an area in <paramref name="ink"/>; candles in the up and down
    /// colours where <paramref name="coloured"/> (not stale, not high contrast), else all in <paramref name="ink"/>,
    /// those that closed up hollow.
    /// </summary>
    internal static void Behind(ID2D1DeviceContext* dc, D2D_RECT_F box, MarketReading reading, DXGI_RGBA ink, bool coloured, bool down, ReadOnlySpan<(float From, float To)> words, float soft, float radius = 0) =>
        GraphBackdrop.Paint(dc, box, radius, down, words, soft, () =>
        {
            if (reading.Chart == MarketChartStyle.Candles)
            {
                Candles(dc, box, reading, ink, coloured);
            }
            else
            {
                Draw(dc, box, reading, ink, area: reading.Chart != MarketChartStyle.Line);
            }
        });

    /// <summary>The stretch of the axis the chart draws: the session where the market keeps one — from the opening bell to the close, the day's last trade reaching the edge at the close — else the reading's own axis.</summary>
    private static (double From, double Span) Across(MarketReading reading, double last)
    {
        var (from, to) = reading.Session is { } session ? (session.From, Math.Max(session.To, last)) : reading.Axis;
        return (from, Math.Max(0.01, to - from));
    }

    /// <summary>
    /// The reading's prices across <paramref name="at"/>, on its own axis, as a line in <paramref name="ink"/>,
    /// with its fill under it where <paramref name="area"/>, from the lowest price at the bottom to the highest
    /// at the top; the line keeps a little room at the top so it is not cut by the edge.
    /// </summary>
    private static void Draw(ID2D1DeviceContext* dc, D2D_RECT_F at, MarketReading reading, DXGI_RGBA ink, bool area)
    {
        var points = reading.Day;
        if (points.Count < 2)
        {
            return;
        }
        var (from, span) = Across(reading, points[^1].At);
        var low = double.MaxValue;
        var high = double.MinValue;
        foreach (var (_, price) in points)
        {
            low = Math.Min(low, price);
            high = Math.Max(high, price);
        }
        var range = Math.Max(high - low, Math.Abs(high) * 0.002 + 1e-9);
        var width = at.right - at.left;
        var height = at.bottom - at.top;
        var stroke = MathF.Max(1, MathF.Round(height / 60f));
        var top = at.top + (height * 0.12f);
        var room = at.bottom - (height * 0.08f) - top;
        ID2D1Factory* factory = null;
        ID2D1PathGeometry* line = null;
        ID2D1PathGeometry* fill = null;
        ID2D1SolidColorBrush* brush = null;
        ID2D1StrokeStyle* joins = null;
        try
        {
            dc->GetFactory(&factory);
            if (factory == null
                || factory->CreatePathGeometry(&line).FAILED
                || factory->CreatePathGeometry(&fill).FAILED)
            {
                return;
            }
            D2D_POINT_2F Point(double x, double price) => new(
                at.left + (float)((x - from) / span * width),
                top + room - (float)((price - low) / range * room));
            bool Trace(ID2D1PathGeometry* geometry, bool closed)
            {
                ID2D1GeometrySink* into = null;
                if (geometry->Open(&into).FAILED)
                {
                    return false;
                }
                try
                {
                    var first = Point(points[0].At, points[0].Price);
                    into->BeginFigure(closed ? new D2D_POINT_2F(first.x, at.bottom) : first, closed ? D2D1_FIGURE_BEGIN.D2D1_FIGURE_BEGIN_FILLED : D2D1_FIGURE_BEGIN.D2D1_FIGURE_BEGIN_HOLLOW);
                    if (closed)
                    {
                        into->AddLine(first);
                    }
                    for (var index = 1; index < points.Count; index++)
                    {
                        into->AddLine(Point(points[index].At, points[index].Price));
                    }
                    if (closed)
                    {
                        into->AddLine(new D2D_POINT_2F(Point(points[^1].At, points[^1].Price).x, at.bottom));
                    }
                    into->EndFigure(closed ? D2D1_FIGURE_END.D2D1_FIGURE_END_CLOSED : D2D1_FIGURE_END.D2D1_FIGURE_END_OPEN);
                    return into->Close().SUCCEEDED;
                }
                finally
                {
                    ComUtil.Release(ref into);
                }
            }
            if ((area && !Trace(fill, closed: true)) || !Trace(line, closed: false))
            {
                return;
            }
            if (area)
            {
                GraphBackdrop.Fill(dc, (ID2D1Geometry*)fill, ink, top, at.bottom);
            }
            // One stroke of one width along the whole path, its turns and ends round: mitred joins drew a
            // spike at every sharp move (2026-10-11: "it should be properly drawn").
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
            brush = Gfx.Brush(dc, ink);
            dc->DrawGeometry((ID2D1Geometry*)line, (ID2D1Brush*)brush, stroke, joins);
        }
        finally
        {
            ComUtil.Release(ref joins);
            ComUtil.Release(ref brush);
            ComUtil.Release(ref fill);
            ComUtil.Release(ref line);
            ComUtil.Release(ref factory);
        }
    }

    /// <summary>
    /// The reading's candles across <paramref name="at"/>, on the line's axis, the lowest low at the bottom and
    /// the highest high at the top: a wick from the high to the low and a body from the open to the close, each
    /// on whole pixels. Neighbours are joined (<see cref="MarketCandles.Fit"/>) until one candle has
    /// <see cref="Pitch"/> to itself, so a narrow face draws fewer, wider candles rather than a smear.
    /// </summary>
    private static void Candles(ID2D1DeviceContext* dc, D2D_RECT_F at, MarketReading reading, DXGI_RGBA ink, bool coloured)
    {
        var all = reading.Candles;
        if (all.Count < 2)
        {
            return;
        }
        var (from, span) = Across(reading, all[^1].At);
        var width = at.right - at.left;
        var height = at.bottom - at.top;
        var step = (float)((all[^1].At - all[0].At) / (all.Count - 1) / span * width);
        var candles = MarketCandles.Fit(all, (int)MathF.Ceiling(Pitch(height) / MathF.Max(0.01f, step)));
        var pitch = candles.Count > 1 ? (float)((candles[^1].At - candles[0].At) / (candles.Count - 1) / span * width) : step;
        var low = double.MaxValue;
        var high = double.MinValue;
        foreach (var candle in candles)
        {
            low = Math.Min(low, candle.Low);
            high = Math.Max(high, candle.High);
        }
        var range = Math.Max(high - low, Math.Abs(high) * 0.002 + 1e-9);
        var top = at.top + (height * 0.12f);
        var room = at.bottom - (height * 0.08f) - top;
        var wick = MathF.Max(1, MathF.Round(height / 60f));
        var body = MathF.Max(wick, MathF.Round(pitch * 0.6f));
        float Y(double price) => top + room - (float)((price - low) / range * room);
        ID2D1SolidColorBrush* up = null;
        ID2D1SolidColorBrush* down = null;
        try
        {
            up = Gfx.Brush(dc, coloured ? Up : ink);
            down = Gfx.Brush(dc, coloured ? Down : ink);
            foreach (var candle in candles)
            {
                var brush = (ID2D1Brush*)(candle.Up ? up : down);
                var middle = at.left + (float)((candle.At - from) / span * width);
                var left = MathF.Round(middle - (body / 2));
                var line = MathF.Round(middle - (wick / 2));
                var wickBox = new D2D_RECT_F(line, MathF.Round(Y(candle.High)), line + wick, MathF.Max(MathF.Round(Y(candle.High)) + 1, MathF.Round(Y(candle.Low))));
                var upper = MathF.Round(Y(Math.Max(candle.Open, candle.Close)));
                var bodyBox = new D2D_RECT_F(left, upper, left + body, MathF.Max(upper + 1, MathF.Round(Y(Math.Min(candle.Open, candle.Close)))));
                // Without colours, a candle that closed up is hollow: its body drawn round, its wick outside it.
                if (!coloured && candle.Up && body >= 3 * wick)
                {
                    var over = wickBox with { bottom = bodyBox.top };
                    var under = wickBox with { top = bodyBox.bottom };
                    dc->FillRectangle(&over, brush);
                    dc->FillRectangle(&under, brush);
                    var inside = new D2D_RECT_F(bodyBox.left + (wick / 2), bodyBox.top + (wick / 2), bodyBox.right - (wick / 2), bodyBox.bottom - (wick / 2));
                    dc->DrawRectangle(&inside, brush, wick, null);
                    continue;
                }
                dc->FillRectangle(&wickBox, brush);
                dc->FillRectangle(&bodyBox, brush);
            }
        }
        finally
        {
            ComUtil.Release(ref down);
            ComUtil.Release(ref up);
        }
    }

    /// <summary>The least room one candle has, in px of a chart <paramref name="height"/> px tall: a body and a gap either side that read apart.</summary>
    internal static float Pitch(float height) => MathF.Max(5, height / 12);
}
