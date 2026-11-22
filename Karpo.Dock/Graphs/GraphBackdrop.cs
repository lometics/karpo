// A graph as the widget's background: the processor, the memory, the graphics, the network and
// the disk, and a share's chart, run across the whole widget — its whole width, and its whole
// height — and the words stand on them (docs\widgets.md §24).
//
// "graphs across the whole widget ... span the whole width and fade behind the text": the graph
// used to be a strip beside the reading on the bar and under it on a card or a line, and the
// widget's room was split between them. Now the graph is the ground the reading is written on, as
// Windows 11's Task Manager draws its small graphs and its Widgets board a share's card: one
// picture, with the words over it.
//
// What keeps the words legible is a soft fade: where the words stand the graph shows at a
// fraction of its strength, and past them it comes back to full over a short ramp. The fade is an
// opacity mask on the graph alone — a layer whose opacity is a gradient — never a band of the
// card's colour laid over it, because the ground under a widget is the bar's glass, a folder's
// card or a list's flyout, in either theme, and a band of any one colour is wrong on the others.
// The words are drawn after it, at full strength, so nothing here touches them.

using Karpo.Dock.Native;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock.Graphs;

internal static unsafe class GraphBackdrop
{
    /// <summary>
    /// How much of the graph shows under the words: enough to read as the same graph running on,
    /// faint enough that a line crossing a figure is a tint behind it and not a stroke through it.
    /// </summary>
    public const float UnderWords = 0.28f;

    /// <summary>
    /// The corner of the card a taskbar entry stands on (Layout.CardRadius: 6 DIP at the icon's
    /// scale, held between 4 and 6 DIP), so a graph reaching the entry's edges takes the card's
    /// curve; none on a connected or a floating bar, which wear no cards and whose widgets stand
    /// flush and square (<see cref="IslandStyle.Square"/>), so a graph runs into its
    /// neighbour's edge rather than rounding off before it.
    /// </summary>
    public static float BarRadius(in IslandStyle style)
    {
        if (style.Square)
        {
            return 0;
        }
        var unit = style.Icon > 0 ? style.Icon / 24f : style.Scale;
        return Math.Clamp(MathF.Round(6 * unit), MathF.Round(4 * style.Scale), MathF.Round(6 * style.Scale));
    }

    /// <summary>
    /// The graph across <paramref name="box"/>, drawn by <paramref name="draw"/>, faded where the
    /// words stand: each of <paramref name="words"/> is a stretch of the box — across it, or down
    /// it where <paramref name="down"/> — in px of the surface, over which the graph shows at
    /// <see cref="UnderWords"/>, coming back to full <paramref name="soft"/> px past either end.
    /// Clipped to the box, its corners rounded by <paramref name="radius"/>.
    /// </summary>
    public static void Paint(
        ID2D1DeviceContext* dc,
        D2D_RECT_F box,
        float radius,
        bool down,
        ReadOnlySpan<(float From, float To)> words,
        float soft,
        Action draw)
    {
        if (box.right - box.left < 1 || box.bottom - box.top < 1)
        {
            return;
        }
        PaintWatch.Shape(box);
        if (PaintWatch.NoBackdrops)
        {
            return;
        }
        var start = down ? box.top : box.left;
        var end = down ? box.bottom : box.right;
        Span<(float At, float Alpha)> stops = stackalloc (float, float)[2 + (words.Length * 4)];
        var count = Stops(start, end, words, MathF.Max(1, soft), stops);

        ID2D1Factory* factory = null;
        ID2D1RoundedRectangleGeometry* shape = null;
        ID2D1GradientStopCollection* collection = null;
        ID2D1LinearGradientBrush* mask = null;
        var layered = false;
        try
        {
            D2D1_GRADIENT_STOP* gradient = stackalloc D2D1_GRADIENT_STOP[count];
            for (var index = 0; index < count; index++)
            {
                gradient[index] = new D2D1_GRADIENT_STOP
                {
                    position = (stops[index].At - start) / (end - start),
                    color = new DXGI_RGBA { r = 0, g = 0, b = 0, a = stops[index].Alpha },
                };
            }
            if (dc->CreateGradientStopCollection(gradient, (uint)count, D2D1_GAMMA.D2D1_GAMMA_1_0, D2D1_EXTEND_MODE.D2D1_EXTEND_MODE_CLAMP, &collection).SUCCEEDED)
            {
                var along = new D2D1_LINEAR_GRADIENT_BRUSH_PROPERTIES
                {
                    startPoint = down ? new D2D_POINT_2F(box.left, start) : new D2D_POINT_2F(start, box.top),
                    endPoint = down ? new D2D_POINT_2F(box.left, end) : new D2D_POINT_2F(end, box.top),
                };
                _ = dc->CreateLinearGradientBrush(&along, null, collection, &mask);
            }
            dc->GetFactory(&factory);
            if (radius > 0 && factory != null)
            {
                var rounded = new D2D1_ROUNDED_RECT { rect = box, radiusX = radius, radiusY = radius };
                _ = factory->CreateRoundedRectangleGeometry(&rounded, &shape);
            }
            var parameters = new D2D1_LAYER_PARAMETERS1
            {
                contentBounds = box,
                geometricMask = (ID2D1Geometry*)shape,
                maskAntialiasMode = D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_PER_PRIMITIVE,
                maskTransform = D2D_MATRIX_3X2_F.Identity,
                opacity = 1,
                opacityBrush = (ID2D1Brush*)mask,
                layerOptions = D2D1_LAYER_OPTIONS1.D2D1_LAYER_OPTIONS1_NONE,
            };
            dc->PushLayer(&parameters, null);
            layered = true;
            var clip = box;
            dc->PushAxisAlignedClip(&clip, D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_ALIASED);
            try
            {
                draw();
            }
            finally
            {
                dc->PopAxisAlignedClip();
            }
        }
        finally
        {
            if (layered)
            {
                dc->PopLayer();
            }
            ComUtil.Release(ref mask);
            ComUtil.Release(ref collection);
            ComUtil.Release(ref shape);
            ComUtil.Release(ref factory);
        }
    }

    /// <summary>
    /// The area under a graph's line, <paramref name="area"/>, in <paramref name="accent"/>: strongest
    /// at <paramref name="top"/>, where the line can reach, thinning to a breath at
    /// <paramref name="bottom"/>, as Windows' own small charts fill theirs. A flat fill made the
    /// lower half of a tall card one block of colour; the gradient keeps the line the thing that is
    /// read, and the ground under the words light.
    /// </summary>
    public static void Fill(ID2D1DeviceContext* dc, ID2D1Geometry* area, DXGI_RGBA accent, float top, float bottom)
    {
        D2D1_GRADIENT_STOP* stops = stackalloc D2D1_GRADIENT_STOP[2];
        stops[0] = new D2D1_GRADIENT_STOP { position = 0, color = accent with { a = accent.a * 0.36f } };
        stops[1] = new D2D1_GRADIENT_STOP { position = 1, color = accent with { a = accent.a * 0.06f } };
        ID2D1GradientStopCollection* collection = null;
        ID2D1LinearGradientBrush* brush = null;
        try
        {
            if (dc->CreateGradientStopCollection(stops, 2, D2D1_GAMMA.D2D1_GAMMA_2_2, D2D1_EXTEND_MODE.D2D1_EXTEND_MODE_CLAMP, &collection).FAILED)
            {
                return;
            }
            var along = new D2D1_LINEAR_GRADIENT_BRUSH_PROPERTIES
            {
                startPoint = new D2D_POINT_2F(0, top),
                endPoint = new D2D_POINT_2F(0, MathF.Max(bottom, top + 1)),
            };
            if (dc->CreateLinearGradientBrush(&along, null, collection, &brush).FAILED)
            {
                return;
            }
            dc->FillGeometry(area, (ID2D1Brush*)brush, null);
        }
        finally
        {
            ComUtil.Release(ref brush);
            ComUtil.Release(ref collection);
        }
    }

    /// <summary>
    /// The fade's stops from <paramref name="start"/> to <paramref name="end"/>, in order, into
    /// <paramref name="into"/>; how many. Each stretch of words holds the graph at
    /// <see cref="UnderWords"/>, ramping to full over <paramref name="soft"/> px either side of it,
    /// and where two stretches' ramps meet the fainter of the two wins.
    /// </summary>
    internal static int Stops(float start, float end, ReadOnlySpan<(float From, float To)> words, float soft, Span<(float At, float Alpha)> into)
    {
        Span<float> at = stackalloc float[2 + (words.Length * 4)];
        var count = 0;
        at[count++] = start;
        at[count++] = end;
        foreach (var (from, to) in words)
        {
            at[count++] = from - soft;
            at[count++] = from;
            at[count++] = to;
            at[count++] = to + soft;
        }
        var points = at[..count];
        points.Sort();
        var made = 0;
        foreach (var point in points)
        {
            var place = Math.Clamp(point, start, end);
            if (made > 0 && place - into[made - 1].At < 0.01f)
            {
                continue;
            }
            into[made++] = (place, Alpha(place, words, soft));
        }
        return made;
    }

    /// <summary>How strongly the graph shows at <paramref name="place"/>: 1 clear of the words, <see cref="UnderWords"/> under them.</summary>
    internal static float Alpha(float place, ReadOnlySpan<(float From, float To)> words, float soft)
    {
        var alpha = 1f;
        foreach (var (from, to) in words)
        {
            var away = place < from ? from - place : place > to ? place - to : 0;
            var shown = UnderWords + ((1 - UnderWords) * Math.Clamp(away / soft, 0, 1));
            alpha = MathF.Min(alpha, shown);
        }
        return alpha;
    }
}
