// The history as a filled area under a line: the one drawing of a performance reading's graph,
// on a list's line, behind its words or, at its tallest, as the last minute the reading's flyout drew
// (PerformanceIsland, docs\widgets.md §97). It draws what
// it is handed and keeps nothing: the samples are the source's (Sources\PerformanceData.cs), and
// the box it draws in is the caller's.

using Karpo.Dock.Native;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock.Graphs;

internal static unsafe class Sparkline
{
    /// <summary>
    /// The network graph is scaled to its busiest sample, but never to less than this, so
    /// background chatter stays a ripple instead of filling the cell.
    /// </summary>
    public const double NetworkFloor = 64 * 1024;

    /// <summary>
    /// The scale of a rate graph over the newest <paramref name="count"/> steps of
    /// <paramref name="history"/>: the busiest of them, never under <see cref="NetworkFloor"/>.
    /// Over everything kept rather than only what is drawn, so the scale does not jump each time a
    /// spike scrolls out of the box.
    /// </summary>
    public static float Ceiling(in Series history, int count)
    {
        var busiest = NetworkFloor;
        for (var age = 0; age < Math.Min(count, history.Count); age++)
        {
            busiest = Math.Max(busiest, history.Ago(age));
        }
        return (float)busiest;
    }

    /// <summary>
    /// A rate graph's scale where it is written beside it (the flyout's "8 MB/s"): <paramref name="busiest"/>
    /// rounded up to a power of two, so the figure is a round one and the scale moves in steps rather
    /// than with every sample.
    /// </summary>
    public static float Round(float busiest) => MathF.Pow(2, MathF.Ceiling(MathF.Log2(MathF.Max(busiest, 1))));

    /// <summary>
    /// <paramref name="history"/> as a filled area under a line, newest at <paramref name="box"/>'s
    /// right, one sample every <paramref name="step"/> px back to its left. A sample counts as
    /// <c>value / full</c> of the box's height. <paramref name="points"/> is the caller's scratch,
    /// two longer than the most samples drawn, so a tick allocates no array.
    /// </summary>
    public static void Paint(
        ID2D1DeviceContext* dc,
        ID2D1SolidColorBrush* brush,
        DXGI_RGBA accent,
        in D2D_RECT_F box,
        float step,
        float stroke,
        in Series history,
        float full,
        D2D_POINT_2F[] points)
    {
        var left = box.left;
        var right = box.right;
        var count = Math.Min(Math.Min(history.Count, (int)((right - left) / MathF.Max(step, 0.5f)) + 2), points.Length - 2);
        if (count < 2)
        {
            return;
        }
        var line = stroke;
        // The line's centre stays half its width inside, so a flat 0 % or 100 % is drawn whole.
        var low = box.bottom - (line / 2);
        var span = MathF.Max(box.bottom - box.top - line, 0);

        ID2D1Factory* factory = null;
        ID2D1PathGeometry* area = null;
        ID2D1PathGeometry* edge = null;
        ID2D1GeometrySink* sink = null;
        ID2D1StrokeStyle* joins = null;
        fixed (D2D_POINT_2F* at = points)
        {
            for (var i = 0; i < count; i++)
            {
                var age = count - 1 - i;
                var of = Math.Clamp(history.Ago(age) / full, 0f, 1f);
                at[i] = new D2D_POINT_2F(right - (age * step), low - (of * span));
            }
            try
            {
                dc->GetFactory(&factory);

                ComUtil.Check(factory->CreatePathGeometry(&area));
                ComUtil.Check(area->Open(&sink));
                sink->BeginFigure(new D2D_POINT_2F(at[0].x, box.bottom), D2D1_FIGURE_BEGIN.D2D1_FIGURE_BEGIN_FILLED);
                sink->AddLines(at, (uint)count);
                sink->AddLine(new D2D_POINT_2F(at[count - 1].x, box.bottom));
                sink->EndFigure(D2D1_FIGURE_END.D2D1_FIGURE_END_CLOSED);
                ComUtil.Check(sink->Close());
                ComUtil.Release(ref sink);
                GraphBackdrop.Fill(dc, (ID2D1Geometry*)area, accent, box.top, box.bottom);

                ComUtil.Check(factory->CreatePathGeometry(&edge));
                ComUtil.Check(edge->Open(&sink));
                sink->BeginFigure(at[0], D2D1_FIGURE_BEGIN.D2D1_FIGURE_BEGIN_HOLLOW);
                sink->AddLines(at + 1, (uint)(count - 1));
                sink->EndFigure(D2D1_FIGURE_END.D2D1_FIGURE_END_OPEN);
                ComUtil.Check(sink->Close());
                // Round joins: a mitred spike at every sharp turn would poke above a 100 % line.
                var properties = new D2D1_STROKE_STYLE_PROPERTIES
                {
                    startCap = D2D1_CAP_STYLE.D2D1_CAP_STYLE_ROUND,
                    endCap = D2D1_CAP_STYLE.D2D1_CAP_STYLE_ROUND,
                    lineJoin = D2D1_LINE_JOIN.D2D1_LINE_JOIN_ROUND,
                    miterLimit = 1,
                };
                ComUtil.Check(factory->CreateStrokeStyle(&properties, null, 0, &joins));
                var colour = accent;
                brush->SetColor(&colour);
                dc->DrawGeometry((ID2D1Geometry*)edge, (ID2D1Brush*)brush, line, joins);
            }
            finally
            {
                ComUtil.Release(ref joins);
                ComUtil.Release(ref sink);
                ComUtil.Release(ref edge);
                ComUtil.Release(ref area);
                ComUtil.Release(ref factory);
            }
        }
    }
}
