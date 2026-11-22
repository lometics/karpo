// Drawing for the calendar and notification flyouts. Each draws its content
// with Direct2D into a few composition surfaces (the frame, the month's grid,
// a card), so the same painting also goes into a bitmap for an offscreen
// picture of the flyout; only hover fills are composition shapes of their own.

using System.Numerics;
using Karpo.Dock.Native;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using Color = Windows.UI.Color;

namespace Karpo.Dock;

internal static unsafe class PopupPaint
{
    /// <summary>
    /// A control's corners: WinUI's ControlCornerRadius, 4 DIPs, the bar's highlights' own. Every
    /// text and search box Karpo draws takes it — on the bar, in a flyout, on a folder's card —
    /// rather than the pill of Windows' taskbar search box (SearchPill.Radius).
    /// </summary>
    public const float ControlRadiusDip = 4;

    /// <summary>A fill colour of the theme as Direct2D takes it.</summary>
    public static DXGI_RGBA Ink(Color color) => FlyoutTheme.Ink(color);

    public static D2D_RECT_F Rect(float x, float y, float width, float height) => new(x, y, x + width, y + height);

    /// <summary>
    /// The gripper: six dots, two across and three down, 3 DIP each, 7 and 6.5 DIP apart, centred in
    /// <paramref name="box"/> at <paramref name="scale"/>, as Windows' reorderable lists draw it (Preferred languages;
    /// Settings' own bar menu list) — a desktop's row (DesktopsLine), a page's row put in order (§551).
    /// </summary>
    public static void Gripper(ID2D1DeviceContext* dc, D2D_RECT_F box, float scale, DXGI_RGBA ink)
    {
        // A drawing, not text: at the display's scale, whatever the text size, as Windows' own.
        var dot = MathF.Max(2, Round(Round(3 * scale)));
        var across = Round(Round(7 * scale)) - dot;
        var down = Round(Round(6.5f * scale)) - dot;
        var width = (2 * dot) + across;
        var height = (3 * dot) + (2 * down);
        var left = Round(box.left + ((box.right - box.left - width) / 2));
        var top = Round(box.top + ((box.bottom - box.top - height) / 2));
        for (var row = 0; row < 3; row++)
        {
            for (var column = 0; column < 2; column++)
            {
                Fill(dc, Rect(left + (column * (dot + across)), top + (row * (dot + down)), dot, dot), dot / 2, ink);
            }
        }
    }

    public static void Fill(ID2D1DeviceContext* dc, D2D_RECT_F rect, float radius, DXGI_RGBA color)
    {
        // Tests only: a test painting on this thread sees each fill's corner (PaintWatch.Styled).
        PaintWatch.Rounded(rect, radius, color, outline: false);
        var brush = Gfx.Brush(dc, color);
        try
        {
            if (radius > 0)
            {
                var shape = new D2D1_ROUNDED_RECT { rect = rect, radiusX = radius, radiusY = radius };
                dc->FillRoundedRectangle(&shape, (ID2D1Brush*)brush);
            }
            else
            {
                dc->FillRectangle(&rect, (ID2D1Brush*)brush);
            }
        }
        finally
        {
            brush->Release();
        }
    }

    /// <summary>A rounded outline inside <paramref name="rect"/>: the stroke doesn't reach past it.</summary>
    public static void Stroke(ID2D1DeviceContext* dc, D2D_RECT_F rect, float radius, float width, DXGI_RGBA color)
    {
        PaintWatch.Rounded(rect, radius, color, outline: true);
        var brush = Gfx.Brush(dc, color);
        try
        {
            var half = width / 2;
            var shape = new D2D1_ROUNDED_RECT
            {
                rect = new D2D_RECT_F(rect.left + half, rect.top + half, rect.right - half, rect.bottom - half),
                radiusX = MathF.Max(0, radius - half),
                radiusY = MathF.Max(0, radius - half),
            };
            dc->DrawRoundedRectangle(&shape, (ID2D1Brush*)brush, width, null);
        }
        finally
        {
            brush->Release();
        }
    }

    /// <summary>
    /// A rounded outline inside <paramref name="rect"/> as WinUI's ControlElevationBorderBrush draws
    /// one: <paramref name="color"/>, its bottom <paramref name="band"/> px shading to
    /// <paramref name="bottom"/>, which it is from <paramref name="share"/> of the way up the band (a
    /// third, ControlElevationBorderBrush's; a half, TextControlElevationBorderBrush's) down to the edge;
    /// §544 <paramref name="top"/>: the top edge instead (the dark theme's ControlElevationBorderBrush,
    /// not flipped). One colour for both is the plain <see cref="Stroke"/>.
    /// </summary>
    public static void ElevationStroke(ID2D1DeviceContext* dc, D2D_RECT_F rect, float radius, float width, DXGI_RGBA color, DXGI_RGBA bottom, float band, bool top = false, float share = 1f / 3f)
    {
        if (band <= 0 || color.Equals(bottom))
        {
            Stroke(dc, rect, radius, width, color);
            return;
        }
        D2D1_GRADIENT_STOP* stops = stackalloc D2D1_GRADIENT_STOP[2];
        stops[0] = new D2D1_GRADIENT_STOP { position = share, color = bottom };
        stops[1] = new D2D1_GRADIENT_STOP { position = 1f, color = color };
        ID2D1GradientStopCollection* collection = null;
        ID2D1LinearGradientBrush* brush = null;
        try
        {
            if (dc->CreateGradientStopCollection(stops, 2, D2D1_GAMMA.D2D1_GAMMA_2_2, D2D1_EXTEND_MODE.D2D1_EXTEND_MODE_CLAMP, &collection).FAILED)
            {
                Stroke(dc, rect, radius, width, color);
                return;
            }
            // From the edge into the band: past it, the outline's own colour.
            var along = new D2D1_LINEAR_GRADIENT_BRUSH_PROPERTIES
            {
                startPoint = new D2D_POINT_2F(rect.left, top ? rect.top : rect.bottom),
                endPoint = new D2D_POINT_2F(rect.left, top ? rect.top + band : rect.bottom - band),
            };
            if (dc->CreateLinearGradientBrush(&along, null, collection, &brush).FAILED)
            {
                Stroke(dc, rect, radius, width, color);
                return;
            }
            var half = width / 2;
            var shape = new D2D1_ROUNDED_RECT
            {
                rect = new D2D_RECT_F(rect.left + half, rect.top + half, rect.right - half, rect.bottom - half),
                radiusX = MathF.Max(0, radius - half),
                radiusY = MathF.Max(0, radius - half),
            };
            dc->DrawRoundedRectangle(&shape, (ID2D1Brush*)brush, width, null);
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

    public static void Circle(ID2D1DeviceContext* dc, Vector2 centre, float radius, DXGI_RGBA color)
    {
        var brush = Gfx.Brush(dc, color);
        try
        {
            var shape = new D2D1_ELLIPSE { point = new D2D_POINT_2F(centre.X, centre.Y), radiusX = radius, radiusY = radius };
            dc->FillEllipse(&shape, (ID2D1Brush*)brush);
        }
        finally
        {
            brush->Release();
        }
    }

    /// <summary>A ring whose outer edge is <paramref name="radius"/> from the centre.</summary>
    public static void Ring(ID2D1DeviceContext* dc, Vector2 centre, float radius, float width, DXGI_RGBA color)
    {
        var brush = Gfx.Brush(dc, color);
        try
        {
            var shape = new D2D1_ELLIPSE
            {
                point = new D2D_POINT_2F(centre.X, centre.Y),
                radiusX = radius - width / 2,
                radiusY = radius - width / 2,
            };
            dc->DrawEllipse(&shape, (ID2D1Brush*)brush, width, null);
        }
        finally
        {
            brush->Release();
        }
    }

    /// <summary>The size one line takes, cut with an ellipsis at <paramref name="maxWidth"/>, in <paramref name="family"/> or the text font.</summary>
    public static Vector2 Measure(Gfx gfx, string text, float px, Weight weight, float? maxWidth = null, string? family = null)
    {
        if (text.Length == 0)
        {
            return Vector2.Zero;
        }
        var layout = gfx.TextLayout(text, px, weight, maxWidth, family);
        try
        {
            DWRITE_TEXT_METRICS metrics;
            ComUtil.Check(layout->GetMetrics(&metrics));
            var width = maxWidth is { } most ? MathF.Min(metrics.widthIncludingTrailingWhitespace, most) : metrics.widthIncludingTrailingWhitespace;
            return new Vector2(MathF.Ceiling(width), MathF.Ceiling(metrics.height));
        }
        finally
        {
            layout->Release();
        }
    }

    /// <summary>
    /// The size one line takes with tabular figures, every digit as wide as every other: a time,
    /// whose width then does not change as its digits do (<see cref="Figures"/>).
    /// </summary>
    public static Vector2 MeasureFigures(Gfx gfx, string text, float px, Weight weight)
    {
        if (text.Length == 0)
        {
            return Vector2.Zero;
        }
        var layout = gfx.TextLayout(text, px, weight, null);
        try
        {
            TextLine.Figures(layout, text.Length);
            DWRITE_TEXT_METRICS metrics;
            ComUtil.Check(layout->GetMetrics(&metrics));
            return new Vector2(MathF.Ceiling(metrics.widthIncludingTrailingWhitespace), MathF.Ceiling(metrics.height));
        }
        finally
        {
            layout->Release();
        }
    }

    /// <summary>One line with tabular figures, its top-left at (<paramref name="x"/>, <paramref name="y"/>), on whole pixels.</summary>
    public static void Figures(ID2D1DeviceContext* dc, Gfx gfx, string text, float px, Weight weight, DXGI_RGBA color, float x, float y)
    {
        if (text.Length == 0)
        {
            return;
        }
        var layout = gfx.TextLayout(text, px, weight, null);
        try
        {
            TextLine.Figures(layout, text.Length);
            Draw(dc, layout, color, new D2D_POINT_2F(Round(x), Round(y)));
        }
        finally
        {
            layout->Release();
        }
    }

    /// <summary>One line with its top-left at (<paramref name="x"/>, <paramref name="y"/>), on whole pixels, in <paramref name="family"/> or the text font.</summary>
    public static void Text(ID2D1DeviceContext* dc, Gfx gfx, string text, float px, Weight weight, DXGI_RGBA color, float x, float y, float? maxWidth = null, string? family = null)
    {
        if (text.Length == 0)
        {
            return;
        }
        var layout = gfx.TextLayout(text, px, weight, maxWidth, family);
        try
        {
            var origin = new D2D_POINT_2F(Round(x), Round(y));
            Draw(dc, layout, color, origin);
            // A line cut at its room, said whole in its host's tooltip (CutWords).
            CutWords.Drawn(layout, text, origin);
        }
        finally
        {
            layout->Release();
        }
    }

    /// <summary>
    /// One line centred in <paramref name="box"/>: by its ink when <paramref name="ink"/> (digits and
    /// glyphs, whose line box carries the font's ascent and descent), else by its line box.
    /// </summary>
    public static void Centred(ID2D1DeviceContext* dc, Gfx gfx, string text, float px, Weight weight, DXGI_RGBA color, D2D_RECT_F box, bool ink, string? family = null)
    {
        if (text.Length == 0)
        {
            return;
        }
        // §589: a glyph a quarter turn, turned about the box's middle, where its ink is centred.
        if (family == gfx.IconFamily && Karpo.Core.OptionGlyphs.IsTurned(text, out var upright))
        {
            var was = Mirror.Quarter(dc, (box.left + box.right) / 2, (box.top + box.bottom) / 2);
            try
            {
                Centred(dc, gfx, upright, px, weight, color, box, ink, family);
            }
            finally
            {
                dc->SetTransform(&was);
            }
            return;
        }
        var layout = gfx.TextLayout(text, px, weight, null, family);
        try
        {
            float width = box.right - box.left;
            float height = box.bottom - box.top;
            D2D_POINT_2F origin;
            if (ink)
            {
                origin = InkOrigin(layout, width, height);
            }
            else
            {
                DWRITE_TEXT_METRICS metrics;
                ComUtil.Check(layout->GetMetrics(&metrics));
                origin = new D2D_POINT_2F(Round((width - metrics.widthIncludingTrailingWhitespace) / 2), Round((height - metrics.height) / 2));
            }
            Draw(dc, layout, color, new D2D_POINT_2F(Round(box.left) + origin.x, Round(box.top) + origin.y), family == gfx.IconFamily && Mirror.Turns(text));
        }
        finally
        {
            layout->Release();
        }
    }

    /// <summary>A Segoe Fluent Icons glyph centred by its ink in <paramref name="box"/> (<see cref="Gfx.InkOrigin"/>).</summary>
    public static void Glyph(ID2D1DeviceContext* dc, Gfx gfx, string code, float px, DXGI_RGBA color, D2D_RECT_F box) =>
        Centred(dc, gfx, code, px, Weight.Regular, color, box, ink: true, gfx.IconFamily);

    /// <summary>
    /// The same, placed by the ink of <paramref name="over"/> — the glyph it is drawn on top of,
    /// which is the faint full set of Wi-Fi arcs under the ones a signal reaches. Both are then
    /// placed alike, so the one with less ink sits exactly inside the other instead of being
    /// centred on its own and sliding out of it.
    /// </summary>
    public static void GlyphOver(ID2D1DeviceContext* dc, Gfx gfx, string code, string over, float px, DXGI_RGBA color, D2D_RECT_F box)
    {
        if (code.Length == 0)
        {
            return;
        }
        if (over.Length == 0 || over == code)
        {
            Glyph(dc, gfx, code, px, color, box);
            return;
        }
        var layout = gfx.TextLayout(code, px, Weight.Regular, null, gfx.IconFamily);
        var under = gfx.TextLayout(over, px, Weight.Regular, null, gfx.IconFamily);
        try
        {
            var origin = Gfx.InkOrigin(under, box.right - box.left, box.bottom - box.top);
            Draw(dc, layout, color, new D2D_POINT_2F(Round(box.left) + origin.x, Round(box.top) + origin.y), Mirror.Turns(code));
        }
        finally
        {
            under->Release();
            layout->Release();
        }
    }

    /// <summary>
    /// Text wrapped to <paramref name="width"/> in at most <paramref name="lines"/> lines, the last one
    /// cut with an ellipsis when there is more; drawn at (<paramref name="x"/>, <paramref name="y"/>)
    /// when <paramref name="dc"/> is set, each line centred in the width when <paramref name="centred"/>.
    /// Returns the height it takes.
    /// </summary>
    public static float Wrapped(ID2D1DeviceContext* dc, Gfx gfx, string text, float px, Weight weight, DXGI_RGBA color, float x, float y, float width, int lines, bool centred = false)
    {
        if (text.Length == 0 || width <= 1 || lines <= 0)
        {
            return 0;
        }
        var (shown, height) = Fit(gfx, text, px, weight, width, lines);
        if (dc != null && shown.Length > 0)
        {
            var layout = WrappedLayout(gfx, shown, px, weight, width);
            try
            {
                if (centred)
                {
                    ComUtil.Check(layout->SetTextAlignment(DWRITE_TEXT_ALIGNMENT.DWRITE_TEXT_ALIGNMENT_CENTER));
                }
                var origin = new D2D_POINT_2F(Round(x), Round(y));
                Draw(dc, layout, color, origin);
                // Its last line ending in an ellipsis where the whole did not fit, said whole in its host's tooltip (CutWords).
                CutWords.Drawn(layout, text, origin, cut: shown != text);
            }
            finally
            {
                layout->Release();
            }
        }
        return height;
    }

    /// <summary>
    /// The longest start of <paramref name="text"/> (ending in "…" when cut) that wraps into at most
    /// <paramref name="lines"/> lines of <paramref name="width"/>, and its height. Cut at a word where
    /// the last line allows it.
    /// </summary>
    internal static (string Text, float Height) Fit(Gfx gfx, string text, float px, Weight weight, float width, int lines)
    {
        var (count, height, ends) = Lines(gfx, text, px, weight, width, lines);
        if (count <= lines)
        {
            return (text, height);
        }
        // What the allowed lines hold, then shorter until the ellipsis fits too.
        var keep = ends;
        while (keep > 0)
        {
            var cut = text[..keep].TrimEnd();
            var candidate = cut + "…";
            var (fits, fitHeight, _) = Lines(gfx, candidate, px, weight, width, lines);
            if (fits <= lines)
            {
                return (candidate, fitHeight);
            }
            // Back a word where there is one on the last line, else a character.
            var space = cut.LastIndexOf(' ');
            keep = space > 0 && keep - space < 24 ? space : keep - 1;
        }
        return ("…", Measure(gfx, "…", px, weight).Y);
    }

    /// <summary>§604: how tall <paramref name="text"/> stands wrapped at <paramref name="width"/>, every line of it (<see cref="Wrapped"/> with no most).</summary>
    public static float WrappedTall(Gfx gfx, string text, float px, Weight weight, float width) =>
        text.Length == 0 || width <= 1 ? 0 : Lines(gfx, text, px, weight, width, int.MaxValue).Height;

    /// <summary>How many lines <paramref name="text"/> wraps into, the height of the first <paramref name="lines"/>, and where they end.</summary>
    private static (int Count, float Height, int End) Lines(Gfx gfx, string text, float px, Weight weight, float width, int lines)
    {
        var layout = WrappedLayout(gfx, text, px, weight, width);
        try
        {
            uint count = 0;
            _ = layout->GetLineMetrics(null, 0, &count);
            if (count == 0)
            {
                return (0, 0, 0);
            }
            var metrics = new DWRITE_LINE_METRICS[count];
            fixed (DWRITE_LINE_METRICS* first = metrics)
            {
                ComUtil.Check(layout->GetLineMetrics(first, count, &count));
            }
            float height = 0;
            int end = 0;
            for (int i = 0; i < Math.Min((int)count, lines); i++)
            {
                height += metrics[i].height;
                end += (int)metrics[i].length;
            }
            return ((int)count, MathF.Ceiling(height), Math.Min(end, text.Length));
        }
        finally
        {
            layout->Release();
        }
    }

    private static IDWriteTextLayout* WrappedLayout(Gfx gfx, string text, float px, Weight weight, float width)
    {
        // No maximum width here: Gfx's layouts then come without trimming, and wrapping is set on this one.
        var layout = gfx.TextLayout(text, px, weight, null);
        try
        {
            ComUtil.Check(layout->SetWordWrapping(DWRITE_WORD_WRAPPING.DWRITE_WORD_WRAPPING_WRAP));
            ComUtil.Check(layout->SetMaxWidth(width));
            return layout;
        }
        catch
        {
            layout->Release();
            throw;
        }
    }

    /// <summary>
    /// <paramref name="pixels"/> over <paramref name="box"/>, its corners rounded by <paramref name="radius"/>:
    /// filling it, cut at its edges like a cover — now playing's art, a camera's picture — or, with
    /// <paramref name="whole"/>, whole inside it and centred, its shape kept. The right way round in a
    /// mirrored drawing (Mirror.cs).
    /// </summary>
    public static void Covered(ID2D1DeviceContext* dc, Pixels pixels, D2D_RECT_F box, float radius, bool whole = false)
    {
        var width = box.right - box.left;
        var height = box.bottom - box.top;
        if (pixels.Width <= 0 || pixels.Height <= 0 || pixels.Data.Length < (long)pixels.Width * pixels.Height * 4 || width <= 0 || height <= 0)
        {
            return;
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
            fixed (byte* data = pixels.Data)
            {
                ComUtil.Check(dc->CreateBitmap(new D2D_SIZE_U((uint)pixels.Width, (uint)pixels.Height), data, (uint)pixels.Width * 4, &properties, &bitmap));
            }
            ComUtil.Check(dc->CreateBitmapBrush((ID2D1Bitmap*)bitmap, &brush));
            brush->SetInterpolationMode1(D2D1_INTERPOLATION_MODE.D2D1_INTERPOLATION_MODE_HIGH_QUALITY_CUBIC);
            var fit = whole ? MathF.Min(width / pixels.Width, height / pixels.Height) : MathF.Max(width / pixels.Width, height / pixels.Height);
            var (drawnWidth, drawnHeight) = (pixels.Width * fit, pixels.Height * fit);
            var (left, top) = (box.left + ((width - drawnWidth) / 2), box.top + ((height - drawnHeight) / 2));
            var shift = new D2D_MATRIX_3X2_F(fit, 0, 0, fit, left, top);
            brush->SetTransform(&shift);
            var into = whole ? new D2D_RECT_F(left, top, left + drawnWidth, top + drawnHeight) : box;
            var shape = new D2D1_ROUNDED_RECT { rect = into, radiusX = radius, radiusY = radius };
            var was = Mirror.Upright(dc, box.left, box.right);
            dc->FillRoundedRectangle(&shape, (ID2D1Brush*)brush);
            dc->SetTransform(&was);
        }
        finally
        {
            ComUtil.Release(ref brush);
            ComUtil.Release(ref bitmap);
        }
    }

    /// <summary><paramref name="pixels"/> fitted into <paramref name="box"/>, its corners rounded by <paramref name="radius"/>.</summary>
    public static void Picture(ID2D1DeviceContext* dc, Pixels pixels, D2D_RECT_F box, float radius, float opacity = 1f)
    {
        if (pixels.Width <= 0 || pixels.Height <= 0 || pixels.Data.Length < (long)pixels.Width * pixels.Height * 4)
        {
            return;
        }
        float side = MathF.Min(box.right - box.left, box.bottom - box.top);
        ID2D1Factory* factory = null;
        ID2D1RoundedRectangleGeometry* shape = null;
        bool layered = false;
        try
        {
            if (radius > 0)
            {
                dc->GetFactory(&factory);
                var rounded = new D2D1_ROUNDED_RECT { rect = box, radiusX = radius, radiusY = radius };
                if (factory != null && factory->CreateRoundedRectangleGeometry(&rounded, &shape).SUCCEEDED)
                {
                    var parameters = new D2D1_LAYER_PARAMETERS1
                    {
                        contentBounds = box,
                        geometricMask = (ID2D1Geometry*)shape,
                        maskAntialiasMode = D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_PER_PRIMITIVE,
                        maskTransform = D2D_MATRIX_3X2_F.Identity,
                        opacity = 1,
                        opacityBrush = null,
                        layerOptions = D2D1_LAYER_OPTIONS1.D2D1_LAYER_OPTIONS1_NONE,
                    };
                    dc->PushLayer(&parameters, null);
                    layered = true;
                }
            }
            Gfx.DrawPixels(dc, pixels, box.left + (box.right - box.left - side) / 2, box.top + (box.bottom - box.top - side) / 2, side, opacity);
        }
        finally
        {
            if (layered)
            {
                dc->PopLayer();
            }
            ComUtil.Release(ref shape);
            ComUtil.Release(ref factory);
        }
    }

    /// <summary>
    /// <paramref name="paint"/> clipped to <paramref name="box"/> with its corners rounded by
    /// <paramref name="radius"/> — what a panel does to everything inside it (a flyout's body
    /// carries a geometric clip of its own rounded rectangle), so a drawing flush with an edge
    /// takes the panel's curve rather than being cut square by it.
    /// </summary>
    public static void Clipped(ID2D1DeviceContext* dc, D2D_RECT_F box, float radius, Action paint)
    {
        if (radius <= 0)
        {
            // Square corners need no layer: the cheap clip does it.
            var square = box;
            dc->PushAxisAlignedClip(&square, D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_ALIASED);
            try
            {
                paint();
            }
            finally
            {
                dc->PopAxisAlignedClip();
            }
            return;
        }
        ID2D1Factory* factory = null;
        ID2D1RoundedRectangleGeometry* shape = null;
        var layered = false;
        try
        {
            {
                dc->GetFactory(&factory);
                var rounded = new D2D1_ROUNDED_RECT { rect = box, radiusX = radius, radiusY = radius };
                if (factory != null && factory->CreateRoundedRectangleGeometry(&rounded, &shape).SUCCEEDED)
                {
                    var parameters = new D2D1_LAYER_PARAMETERS1
                    {
                        contentBounds = box,
                        geometricMask = (ID2D1Geometry*)shape,
                        maskAntialiasMode = D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_PER_PRIMITIVE,
                        maskTransform = D2D_MATRIX_3X2_F.Identity,
                        opacity = 1,
                        opacityBrush = null,
                        layerOptions = D2D1_LAYER_OPTIONS1.D2D1_LAYER_OPTIONS1_NONE,
                    };
                    dc->PushLayer(&parameters, null);
                    layered = true;
                }
            }
            paint();
        }
        finally
        {
            if (layered)
            {
                dc->PopLayer();
            }
            ComUtil.Release(ref shape);
            ComUtil.Release(ref factory);
        }
    }

    /// <summary>A horizontal hairline across [<paramref name="left"/>, <paramref name="right"/>) at <paramref name="y"/>.</summary>
    public static void Divider(ID2D1DeviceContext* dc, float left, float right, float y, float thickness, DXGI_RGBA color) =>
        Fill(dc, new D2D_RECT_F(left, y, right, y + thickness), 0, color);

    /// <summary>
    /// <paramref name="layout"/> at <paramref name="origin"/>; in a mirrored drawing turned back to read,
    /// but for a glyph that points along the line (<paramref name="turns"/>, Mirror.cs).
    /// </summary>
    private static void Draw(ID2D1DeviceContext* dc, IDWriteTextLayout* layout, DXGI_RGBA color, D2D_POINT_2F origin, bool turns = false)
    {
        // Tests only: a test painting on this thread sees each line go by (PaintWatch).
        var brush = Gfx.Brush(dc, PaintWatch.Seen(layout, origin, color));
        try
        {
            Mirror.Text(dc, origin, layout, (ID2D1Brush*)brush, D2D1_DRAW_TEXT_OPTIONS.D2D1_DRAW_TEXT_OPTIONS_ENABLE_COLOR_FONT, turns);
        }
        finally
        {
            brush->Release();
        }
    }

    /// <summary>Where to draw a layout so its ink sits in the middle of a box (<see cref="Gfx.InkOrigin"/>).</summary>
    private static D2D_POINT_2F InkOrigin(IDWriteTextLayout* layout, float width, float height) =>
        Gfx.InkOrigin(layout, width, height);

    public static float Round(float value) => MathF.Round(value, MidpointRounding.AwayFromZero);
}
