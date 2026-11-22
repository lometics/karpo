// §704: Liquid glass's light, drawn (Karpo.Core\LiquidGlass.cs has the material and its values; docs\widgets.md §704): over
// a surface's tint the glow and the lens bands, under what stands on it, and over that the dividers and the rim — each white
// (a divider its own ink) at its gradient's stops, every gradient down the whole glass shape (a joined set's body). One
// drawing for the compositor (Glaze: the bar's cards, a floating bar's panels, every flyout's panel) and one for Direct2D (the
// Appearance preview's folder lines). The only gradients Karpo draws on its surfaces (Scene.cs's "flat materials only" holds
// for every other look).

using System.Numerics;
using Karpo.Core;
using TerraFX.Interop.DirectX;
using Windows.UI.Composition;
using Color = Windows.UI.Color;

namespace Karpo.Dock;

/// <summary>
/// §704: one rounded surface's glass light in the compositor: the glow (a fill falling in from the top edge,
/// <see cref="GlassLook.GlowDip"/> deep) and the lens (<see cref="GlassLook.LensBands"/> rings
/// <see cref="GlassLook.LensBandDip"/> wide from the rim in), shapes laid over its tint and under its content; the rim
/// (<see cref="GlassLook.RimDip"/>) a ring of its own over the content (<see cref="Over"/>). Its gradients are aimed down the
/// glass shape at every <see cref="Place"/>.
/// </summary>
internal sealed class Glaze
{
    private readonly CompositionRoundedRectangleGeometry glowShape;
    private readonly CompositionLinearGradientBrush glowBrush;
    private readonly List<(CompositionRoundedRectangleGeometry Shape, CompositionLinearGradientBrush Brush)> bands = [];
    private readonly float bandPx;
    private readonly float glowPx;

    /// <summary>The shapes of <paramref name="glass"/>'s glow and lens at <paramref name="scale"/>, made into <paramref name="owned"/>.</summary>
    public Glaze(Compositor c, List<IDisposable> owned, GlassLook glass, float scale)
    {
        bandPx = MathF.Max(1, MathF.Round(glass.LensBandDip * scale));
        glowPx = MathF.Max(1, MathF.Round(glass.GlowDip * scale));
        glowShape = c.CreateRoundedRectangleGeometry();
        glowBrush = GlassPaint.Brush(c, glass.Glow, owned);
        var glow = c.CreateSpriteShape(glowShape);
        glow.FillBrush = glowBrush;
        owned.AddRange([glowShape, glow]);
        var shapes = new List<CompositionSpriteShape> { glow };
        for (var band = 0; band < glass.LensBands.Count; band++)
        {
            var shape = c.CreateRoundedRectangleGeometry();
            var brush = GlassPaint.Brush(c, glass.LensStops(band), owned);
            var ring = c.CreateSpriteShape(shape);
            ring.StrokeBrush = brush;
            ring.StrokeThickness = bandPx;
            owned.AddRange([shape, ring]);
            bands.Add((shape, brush));
            shapes.Add(ring);
        }
        Shapes = shapes;
    }

    /// <summary>The glow's shape, then the lens's bands from the rim in: what a surface's shapes hold over its tint, in this order.</summary>
    public IReadOnlyList<CompositionSpriteShape> Shapes { get; }

    /// <summary>
    /// The glass laid over a shape <paramref name="size"/> px with corners of <paramref name="radius"/>, its top left at
    /// <paramref name="at"/> in the shapes' visual. <paramref name="body"/> (from the same origin) is the whole glass shape it
    /// is part of where it is joined (LiquidGlass.Join), drawn there and cut to the visual by its clip; the shape itself else.
    /// </summary>
    public void Place(Vector2 size, float radius, Vector2 at = default, (Vector2 At, Vector2 Size)? body = null)
    {
        var (from, whole) = body ?? (at, size);
        glowShape.Offset = from;
        glowShape.Size = whole;
        glowShape.CornerRadius = new Vector2(radius, radius);
        (glowBrush.StartPoint, glowBrush.EndPoint) = (from, from + new Vector2(0, glowPx));
        for (var band = 0; band < bands.Count; band++)
        {
            var (shape, brush) = bands[band];
            var inset = (band * bandPx) + (bandPx / 2);
            shape.Offset = from + new Vector2(inset, inset);
            shape.Size = Vector2.Max(whole - new Vector2(2 * inset), Vector2.Zero);
            shape.CornerRadius = new Vector2(MathF.Max(0, radius - inset), MathF.Max(0, radius - inset));
            GlassPaint.Down(brush, whole, from);
        }
    }

    /// <summary>
    /// §704: <paramref name="glass"/>'s rim brush: white, its light running down the glass, aimed by <see cref="GlassPaint.Down"/>
    /// at its glass shape. What a surface's ring strokes in place of a colour.
    /// </summary>
    public static CompositionLinearGradientBrush Over(Compositor c, List<IDisposable> owned, GlassLook glass) => GlassPaint.Brush(c, glass.Rim, owned);
}

/// <summary>§704: Liquid glass's white gradients, for the compositor and for Direct2D.</summary>
internal static unsafe class GlassPaint
{
    /// <summary>White at <paramref name="stops"/>' alphas, its ends placed in px (<see cref="Down"/>).</summary>
    public static CompositionLinearGradientBrush Brush(Compositor c, IReadOnlyList<GlassStop> stops, List<IDisposable> owned)
    {
        var brush = c.CreateLinearGradientBrush();
        brush.MappingMode = CompositionMappingMode.Absolute;
        owned.Add(brush);
        foreach (var stop in stops)
        {
            var at = c.CreateColorGradientStop((float)stop.Offset, White(stop.Alpha));
            brush.ColorStops.Add(at);
            owned.Add(at);
        }
        return brush;
    }

    /// <summary>A gradient from the top edge of a glass shape <paramref name="size"/> px (its top left at <paramref name="at"/>) down to its bottom edge.</summary>
    public static void Down(CompositionLinearGradientBrush brush, Vector2 size, Vector2 at = default)
    {
        brush.StartPoint = at;
        brush.EndPoint = at + new Vector2(0, MathF.Max(1, size.Y));
    }

    /// <summary>§704: white at <paramref name="alpha"/>.</summary>
    public static Color White(double alpha) =>
        Color.FromArgb((byte)MathF.Round((float)Math.Clamp(alpha, 0, 1) * 255), 255, 255, 255);

    /// <summary>§704: <paramref name="glass"/>'s divider ink.</summary>
    public static Color DividerInk(GlassLook glass) => Color.FromArgb(glass.Divider.A, glass.Divider.R, glass.Divider.G, glass.Divider.B);

    /// <summary>
    /// §704: Direct2D's glow and lens over a glass shape <paramref name="rect"/> with corners of <paramref name="radius"/>, at
    /// <paramref name="scale"/>: what lies over its tint, under what stands on it.
    /// </summary>
    public static void Under(ID2D1DeviceContext* dc, D2D_RECT_F rect, float radius, GlassLook glass, float scale)
    {
        var glowPx = MathF.Max(1, MathF.Round(glass.GlowDip * scale));
        Painted(dc, glass.Glow, (new D2D_POINT_2F(rect.left, rect.top), new D2D_POINT_2F(rect.left, rect.top + glowPx)), brush =>
        {
            var shape = new D2D1_ROUNDED_RECT { rect = rect, radiusX = radius, radiusY = radius };
            dc->FillRoundedRectangle(&shape, (ID2D1Brush*)brush);
        });
        var band = MathF.Max(1, MathF.Round(glass.LensBandDip * scale));
        var down = (new D2D_POINT_2F(rect.left, rect.top), new D2D_POINT_2F(rect.left, rect.bottom));
        for (var at = 0; at < glass.LensBands.Count; at++)
        {
            var inset = at * band;
            var inner = new D2D_RECT_F(rect.left + inset, rect.top + inset, rect.right - inset, rect.bottom - inset);
            if (inner.right - inner.left > 2 * band && inner.bottom - inner.top > 2 * band)
            {
                Stroked(dc, inner, MathF.Max(0, radius - inset), band, glass.LensStops(at), down);
            }
        }
    }

    /// <summary>§704: Direct2D's rim round a glass shape <paramref name="rect"/> with corners of <paramref name="radius"/>: over what stands on it, its light down the shape.</summary>
    public static void Rim(ID2D1DeviceContext* dc, D2D_RECT_F rect, float radius, GlassLook glass, float scale) =>
        Stroked(dc, rect, radius, MathF.Max(1, MathF.Round(glass.RimDip * scale)), glass.Rim,
            (new D2D_POINT_2F(rect.left, rect.top), new D2D_POINT_2F(rect.left, rect.bottom)));

    /// <summary>A ring <paramref name="width"/> px inside <paramref name="rect"/> in the gradient of <paramref name="stops"/> along <paramref name="along"/>.</summary>
    private static void Stroked(ID2D1DeviceContext* dc, D2D_RECT_F rect, float radius, float width, IReadOnlyList<GlassStop> stops, (D2D_POINT_2F From, D2D_POINT_2F To) along) =>
        Painted(dc, stops, along, brush =>
        {
            var half = width / 2;
            var shape = new D2D1_ROUNDED_RECT
            {
                rect = new D2D_RECT_F(rect.left + half, rect.top + half, rect.right - half, rect.bottom - half),
                radiusX = MathF.Max(0, radius - half),
                radiusY = MathF.Max(0, radius - half),
            };
            dc->DrawRoundedRectangle(&shape, (ID2D1Brush*)brush, width, null);
        });

    /// <summary><paramref name="paint"/> with a brush of white at <paramref name="stops"/>' alphas along <paramref name="along"/>; nothing where Direct2D makes none.</summary>
    private static void Painted(ID2D1DeviceContext* dc, IReadOnlyList<GlassStop> stops, (D2D_POINT_2F From, D2D_POINT_2F To) along, Action<nint> paint)
    {
        var count = stops.Count;
        var made = stackalloc D2D1_GRADIENT_STOP[count];
        for (var at = 0; at < count; at++)
        {
            made[at] = new D2D1_GRADIENT_STOP { position = (float)stops[at].Offset, color = new DXGI_RGBA { r = 1, g = 1, b = 1, a = (float)stops[at].Alpha } };
        }
        ID2D1GradientStopCollection* collection = null;
        ID2D1LinearGradientBrush* brush = null;
        try
        {
            if (dc->CreateGradientStopCollection(made, (uint)count, D2D1_GAMMA.D2D1_GAMMA_2_2, D2D1_EXTEND_MODE.D2D1_EXTEND_MODE_CLAMP, &collection).FAILED)
            {
                return;
            }
            var line = new D2D1_LINEAR_GRADIENT_BRUSH_PROPERTIES { startPoint = along.From, endPoint = along.To };
            if (dc->CreateLinearGradientBrush(&line, null, collection, &brush).FAILED)
            {
                return;
            }
            paint((nint)brush);
        }
        finally
        {
            if (brush != null)
            {
                brush->Release();
            }
            if (collection != null)
            {
                collection->Release();
            }
        }
    }
}
