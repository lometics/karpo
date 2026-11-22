// A part's pattern, drawn (docs\widgets.md §462; Karpo.Core\Patterns.cs has the patterns).
//
// The rule the person gave: the pattern runs on from widget to widget of a group, and starts again in the next group. So
// each group — the bar, a section of a folder — is one sheet of the pattern, laid from the group's own corner, and each
// card of it shows the piece of that sheet it stands on: a window onto it, not a pattern of its own. A card says where its
// corner stands on its group's sheet (PatternLayer.Show's "at"), and draws from there. §515: a widget's card on the bar is
// a sheet of its own (no groups on the bar).
//
// The compositor has no tiling brush, so each layer is a surface of its own, drawn once with Direct2D (a bitmap brush of
// the tile, wrapping) and drawn again only when what it shows changes: its pattern, its ink, its size, its scale or where
// it stands on the sheet, all of which change only at a build. Nothing is drawn per frame; a hover, a press, a glide or a
// folder's flight moves or lights the card and its layer with it.

using System.Numerics;
using Karpo.Core;
using Karpo.Dock.Native;
using TerraFX.Interop.DirectX;
using Windows.UI.Composition;

namespace Karpo.Dock;

/// <summary>§462: a pattern drawn with Direct2D: the endless sheet of its tile, in its ink.</summary>
internal static unsafe class PatternPaint
{
    /// <summary>
    /// <paramref name="pattern"/> at <paramref name="scale"/> filling <paramref name="rect"/> (px of <paramref name="dc"/>), the
    /// sheet's corner at <paramref name="origin"/>: the pixel at (x, y) shows the sheet's (x − origin.X, y − origin.Y).
    /// <paramref name="radius"/> rounds the fill's corners. <paramref name="boost"/> strengthens the ink (a swatch's, too small
    /// to show a quiet pattern), never past half of its colour by the boost: an ink said stronger (§513, a pattern colour) is
    /// drawn as said.
    /// </summary>
    public static void Fill(ID2D1DeviceContext* dc, Patterned pattern, float scale, D2D_RECT_F rect, Vector2 origin, float radius = 0, float boost = 1)
    {
        var tile = Patterns.Tile(pattern.Pattern, scale);
        var ink = pattern.Ink;
        var alpha = MathF.Min(ink.A / 255f * boost, MathF.Max(ink.A / 255f, 0.5f));
        var pixels = new byte[tile.Coverage.Length * 4];
        for (var at = 0; at < tile.Coverage.Length; at++)
        {
            // Premultiplied BGRA.
            var a = alpha * tile.Coverage[at] / 255f;
            pixels[(at * 4) + 0] = (byte)MathF.Round(ink.B * a);
            pixels[(at * 4) + 1] = (byte)MathF.Round(ink.G * a);
            pixels[(at * 4) + 2] = (byte)MathF.Round(ink.R * a);
            pixels[(at * 4) + 3] = (byte)MathF.Round(255 * a);
        }
        var properties = new D2D1_BITMAP_PROPERTIES1
        {
            pixelFormat = new D2D1_PIXEL_FORMAT
            {
                format = DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM,
                alphaMode = D2D1_ALPHA_MODE.D2D1_ALPHA_MODE_PREMULTIPLIED,
            },
            dpiX = 96,
            dpiY = 96,
        };
        ID2D1Bitmap1* bitmap = null;
        ID2D1BitmapBrush1* brush = null;
        try
        {
            fixed (byte* data = pixels)
            {
                ComUtil.Check(dc->CreateBitmap(new D2D_SIZE_U((uint)tile.Width, (uint)tile.Height), data, (uint)tile.Width * 4, &properties, &bitmap));
            }
            ComUtil.Check(dc->CreateBitmapBrush((ID2D1Bitmap*)bitmap, &brush));
            brush->SetExtendModeX(D2D1_EXTEND_MODE.D2D1_EXTEND_MODE_WRAP);
            brush->SetExtendModeY(D2D1_EXTEND_MODE.D2D1_EXTEND_MODE_WRAP);
            // A tile's px on a px of the surface, whole: nothing between them to blur.
            brush->SetInterpolationMode1(D2D1_INTERPOLATION_MODE.D2D1_INTERPOLATION_MODE_NEAREST_NEIGHBOR);
            var shift = D2D_MATRIX_3X2_F.Translation(Patterns.Wrap((int)MathF.Round(origin.X), tile.Width), Patterns.Wrap((int)MathF.Round(origin.Y), tile.Height));
            brush->SetTransform(&shift);
            var shape = new D2D1_ROUNDED_RECT { rect = rect, radiusX = radius, radiusY = radius };
            dc->FillRoundedRectangle(&shape, (ID2D1Brush*)brush);
        }
        finally
        {
            ComUtil.Release(ref brush);
            ComUtil.Release(ref bitmap);
        }
    }
}

/// <summary>
/// §462: a card's or a sheet's pattern in the visual tree: a sprite of its own over the card's fill, under what stands on it,
/// showing the piece of its group's sheet it stands on (<see cref="Show"/>). Clipped by whoever holds it to the card's shape.
/// </summary>
internal sealed class PatternLayer : IDisposable
{
    private readonly CompositionSurfaceBrush brush;
    private CompositionDrawingSurface? surface;

    /// <summary>What the surface was last drawn from: the pattern, the scale, its size in px and where its corner stands on the sheet, a period's worth.</summary>
    private (Patterned Pattern, float Scale, int Width, int Height, int X, int Y)? drawn;

    public PatternLayer(Compositor compositor)
    {
        Visual = compositor.CreateSpriteVisual();
        brush = compositor.CreateSurfaceBrush();
        brush.Stretch = CompositionStretch.None;
        brush.HorizontalAlignmentRatio = 0;
        brush.VerticalAlignmentRatio = 0;
        Visual.Brush = brush;
        Visual.IsVisible = false;
        views.Add(Visual);
    }

    public SpriteVisual Visual { get; }

    /// <summary>Every sprite showing the layer's surface: <see cref="Visual"/>, and those made by <see cref="Also"/>.</summary>
    private readonly List<SpriteVisual> views = [];

    /// <summary>One more sprite showing the same surface, shown, sized and hidden with it (a floating bar's docks, each a window onto the bar's one sheet); the layer's, closed with it.</summary>
    public SpriteVisual Also(Compositor compositor)
    {
        var view = compositor.CreateSpriteVisual();
        view.Brush = brush;
        view.Size = Visual.Size;
        view.IsVisible = Visual.IsVisible;
        views.Add(view);
        return view;
    }

    /// <summary>tests: how many times the surface was drawn, over the layer's life.</summary>
    internal int Draws { get; private set; }

    /// <summary>tests: what it shows now — the pattern and where its corner stands on its group's sheet —; null while it shows none.</summary>
    internal (Patterned Pattern, Vector2 At)? Showing { get; private set; }

    /// <summary>
    /// <paramref name="pattern"/> over <paramref name="size"/> px from the layer's corner, which stands at <paramref name="at"/>
    /// on its group's sheet (px from the group's corner), at <paramref name="scale"/>; nothing for none. Drawn again only where
    /// any of it changed.
    /// </summary>
    public unsafe void Show(Gfx gfx, Patterned? pattern, float scale, Vector2 size, Vector2 at)
    {
        var (width, height) = ((int)MathF.Ceiling(size.X), (int)MathF.Ceiling(size.Y));
        if (pattern is not { } shown || width <= 0 || height <= 0)
        {
            foreach (var view in views)
            {
                view.IsVisible = false;
            }
            Showing = null;
            return;
        }
        var (across, down) = Patterns.Period(shown.Pattern, scale);
        var key = (shown, scale, width, height, Patterns.Wrap((int)MathF.Round(at.X), across), Patterns.Wrap((int)MathF.Round(at.Y), down));
        if (drawn != key)
        {
            void Lay(ID2D1DeviceContext* dc) =>
                PatternPaint.Fill(dc, shown, scale, new D2D_RECT_F(0, 0, width, height), new Vector2(-key.Item5, -key.Item6));
            if (surface is null || drawn is not { } was || was.Width != width || was.Height != height)
            {
                surface?.Dispose();
                surface = gfx.Draw(width, height, Lay);
                brush.Surface = surface;
            }
            else
            {
                gfx.Redraw(surface, Lay);
            }
            drawn = key;
            Draws++;
        }
        foreach (var view in views)
        {
            view.Size = new Vector2(width, height);
            view.IsVisible = true;
        }
        Showing = (shown, at);
    }

    public void Dispose()
    {
        foreach (var view in views)
        {
            view.Dispose();
        }
        surface?.Dispose();
        brush.Dispose();
    }
}
