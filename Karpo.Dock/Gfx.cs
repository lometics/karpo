// The one Direct2D device Karpo draws with. Each icon, tile and piece of text
// is painted once into a composition surface; after that DWM holds the pixels
// and this process does not touch them again.

using System.Numerics;
using System.Text;
using Karpo.Dock.Native;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using Windows.Graphics.DirectX;
using Windows.UI.Composition;
using static TerraFX.Interop.DirectX.DirectX;
using static TerraFX.Interop.Windows.Windows;
using ICompositionDrawingSurfaceInterop = TerraFX.Interop.WinRT.ICompositionDrawingSurfaceInterop;
using ICompositionGraphicsDevice = TerraFX.Interop.WinRT.ICompositionGraphicsDevice;
using ICompositorInterop = TerraFX.Interop.WinRT.ICompositorInterop;

namespace Karpo.Dock;

public enum Weight
{
    Regular,
    Semibold,
}

/// <summary>
/// Paints into a surface through a device context that <see cref="Gfx.Draw"/> or
/// <see cref="Gfx.Redraw"/> has already set up: 96 DPI, origin at the surface's
/// top-left, cleared to transparent.
/// </summary>
public unsafe delegate void Paint(ID2D1DeviceContext* dc);

public sealed unsafe class Gfx : IDisposable
{
    /// <summary>A laid-out line waiting to be drawn, and where.</summary>
    private struct Laid
    {
        public IDWriteTextLayout* Layout;
        public float Width;
        public float Top;
    }

    /// <summary>The WinUI 3 icon font, which every glyph in the dock comes from.</summary>
    public const string IconFont = "Segoe Fluent Icons"; // not-ui: a font

    /// <summary>
    /// Windows 10 does not ship Segoe Fluent Icons; its older icon font has most
    /// of the same glyphs at the same code points.
    /// </summary>
    internal const string OlderIconFont = "Segoe MDL2 Assets"; // not-ui: a font

    private const string TextFont = "Segoe UI Variable Text"; // not-ui: a font

    /// <summary>
    /// §459: the text style the line of text measured or drawn now is laid out in: its family, where it is installed, else
    /// Karpo's own (§556), and its weight, the base the kit's two weights stand on (<see cref="Karpo.Core.TextStyle.Weights"/>). §507:
    /// each thing's own while it is measured and drawn (<see cref="Wearing"/>: a widget's in its look's font, a line's in
    /// its, the bar's text in its layout's Taskbar widgets', the menus in their layout's Widgets in folders'), else Karpo's
    /// chrome's — the default layout's menus' (the dock sets it from its look with every rebuild, Dock.Rebuild). A font other
    /// than Segoe UI Variable is laid out on Segoe UI Variable's line, its height and its baseline, so every row, card and
    /// box Karpo sizes for its text keeps its size and nothing is cut, whatever the font's own ascent (<see cref="Line"/>).
    /// </summary>
    public Karpo.Core.TextStyle Typeface { get; set; }

    /// <summary>
    /// §507: <see cref="Typeface"/> <paramref name="typeface"/> until the scope handed back is disposed, then what it was: what
    /// a widget, a line, a bar or a menu is measured and drawn in, whoever asks.
    /// </summary>
    public TypefaceScope Wearing(Karpo.Core.TextStyle typeface)
    {
        var was = Typeface;
        Typeface = typeface;
        return new TypefaceScope(this, was);
    }

    /// <summary>§507: a <see cref="Wearing"/>, which puts the typeface back as it ends.</summary>
    public readonly struct TypefaceScope(Gfx gfx, Karpo.Core.TextStyle was) : IDisposable
    {
        public void Dispose() => gfx.Typeface = was;
    }

    // The family each font's comes to on this PC, found once a family.
    private readonly Dictionary<string, string> textFamilies = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The family a line of text is laid out in: <see cref="Typeface"/>'s, §556 Karpo's own (Segoe UI Variable Text) where it
    /// is not installed here — a font of another PC's, kept in the file as said.
    /// </summary>
    internal string TextFamily
    {
        get
        {
            var family = Typeface.Family;
            if (!textFamilies.TryGetValue(family, out var found))
            {
                textFamilies[family] = found = Typeface.IsKarpoFamily || !Installed(family) ? TextFont : family;
            }
            return found;
        }
    }
    // Segoe UI Variable Text's line, its height and its baseline over the em, measured once (Line).
    private (float Height, float Baseline)? line;

    private readonly CompositionGraphicsDevice device;
    private ID3D11Device* d3d;
    private ID2D1Factory1* factory;
    private ID2D1Device* d2d;
    private IDWriteFactory* dwrite;

    public Compositor Compositor { get; }

    /// <summary>
    /// <see cref="IconFont"/> when it is installed, otherwise Segoe MDL2 Assets. Looked up
    /// once: an icon font that DirectWrite cannot find is silently replaced by
    /// one without these code points, and every glyph would draw as a box.
    /// </summary>
    public string IconFamily { get; }

    /// <summary>
    /// Draws with WARP, on the CPU, unless <paramref name="gpu"/> is set. Karpo only draws when
    /// something first appears or changes, and DWM composes the result on the
    /// GPU either way. A hardware device loads the GPU driver into this
    /// process, measured at 47 MB private against WARP's 1 MB, and on NVIDIA a
    /// driver thread came with it that woke 74 times a second while the dock
    /// sat idle.
    /// </summary>
    public Gfx(bool gpu)
    {
        Compositor = new Compositor();
        try
        {
            ID3D11Device* created = null;
            if (!gpu || D3d(D3D_DRIVER_TYPE.D3D_DRIVER_TYPE_HARDWARE, &created).FAILED)
            {
                ComUtil.Check(D3d(D3D_DRIVER_TYPE.D3D_DRIVER_TYPE_WARP, &created));
            }
            d3d = created;

            ID2D1Factory1* d2dFactory = null;
            ComUtil.Check(D2D1CreateFactory(
                D2D1_FACTORY_TYPE.D2D1_FACTORY_TYPE_SINGLE_THREADED,
                __uuidof<ID2D1Factory1>(),
                (void**)&d2dFactory));
            factory = d2dFactory;

            var dxgi = ComUtil.Query<IDXGIDevice>((IUnknown*)d3d);
            ID2D1Device* d2dDevice = null;
            try
            {
                ComUtil.Check(factory->CreateDevice(dxgi, &d2dDevice));
            }
            finally
            {
                dxgi->Release();
            }
            d2d = d2dDevice;

            var interop = ComUtil.Query<ICompositorInterop>(Compositor);
            ICompositionGraphicsDevice* graphics = null;
            try
            {
                ComUtil.Check(interop->CreateGraphicsDevice((IUnknown*)d2d, &graphics));
            }
            finally
            {
                interop->Release();
            }
            device = ComUtil.Adopt<CompositionGraphicsDevice>(graphics);

            IDWriteFactory* text = null;
            ComUtil.Check(DWriteCreateFactory(
                DWRITE_FACTORY_TYPE.DWRITE_FACTORY_TYPE_SHARED,
                __uuidof<IDWriteFactory>(),
                (IUnknown**)&text));
            dwrite = text;
            IconFamily = Installed(IconFont) ? IconFont : OlderIconFont;
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>An icon fitted and centred in a <paramref name="size"/> px square.</summary>
    public CompositionDrawingSurface Icon(Pixels pixels, float size)
    {
        Validate(pixels);
        return Draw(size, size, dc => DrawPixels(dc, pixels, 0, 0, size));
    }

    /// <summary>
    /// A new picture in a surface from <see cref="Icon"/>, fitted to the size it already has: an
    /// icon that changes (a tray icon's) is painted again where it is, without a new surface.
    /// </summary>
    public void RedrawIcon(CompositionDrawingSurface surface, Pixels pixels)
    {
        Validate(pixels);
        var size = (float)Math.Min(surface.Size.Width, surface.Size.Height);
        Redraw(surface, dc => DrawPixels(dc, pixels, 0, 0, size));
    }

    /// <summary>
    /// A pin group's icon, like a Windows 11 Start folder: a subtly filled rounded
    /// square with the first four <paramref name="members"/> in a 2 × 2 grid, in reading order.
    /// A member without pixels leaves its cell empty, so the others keep their places.
    /// </summary>
    public CompositionDrawingSurface GroupIcon(IReadOnlyList<Pixels?> members, float size, bool dark)
    {
        int count = Math.Min(members.Count, 4);
        for (int i = 0; i < count; i++)
        {
            if (members[i] is { } member)
            {
                Validate(member);
            }
        }
        // Whole pixels, as for the Windows logo: at 36 px (24 DIP at 150 %)
        // this is a 4 px margin around 13 + 2 + 13.
        float cell = float.MaxNumber(Round(size * 0.36f), 1);
        float gap = float.MaxNumber(Round(size * 0.06f), 1);
        float margin = Round((size - (2 * cell + gap)) / 2);
        var fill = dark ? Ink(1, 1, 1, 0.08f) : Ink(0, 0, 0, 0.06f);
        return Draw(size, size, dc =>
        {
            var shape = new D2D1_ROUNDED_RECT
            {
                rect = new D2D_RECT_F(0, 0, size, size),
                radiusX = size * 0.2f,
                radiusY = size * 0.2f,
            };
            var brush = Brush(dc, fill);
            dc->FillRoundedRectangle(&shape, (ID2D1Brush*)brush);
            brush->Release();
            for (int i = 0; i < count; i++)
            {
                // An empty image has nothing to show and would fail CreateBitmap.
                if (members[i] is { Width: > 0, Height: > 0 } member)
                {
                    DrawPixels(dc, member, margin + i % 2 * (cell + gap), margin + i / 2 * (cell + gap), cell);
                }
            }
        });
    }

    /// <summary>
    /// A rounded tile with the first letter of <paramref name="name"/>, coloured by the name,
    /// for links: a favicon would need a network request per pin. Flat, as
    /// Fluent draws it: the Rust tile's lighter upper half matched a glass
    /// panel this dock no longer has.
    /// </summary>
    public CompositionDrawingSurface Tile(string name, float size)
    {
        var letter = "?";
        foreach (var rune in name.EnumerateRunes())
        {
            if (Rune.IsLetter(rune) || Rune.IsNumber(rune))
            {
                letter = Rune.ToUpperInvariant(rune).ToString();
                break;
            }
        }
        // FNV-1a over the UTF-8 bytes, so a name keeps the colour the Rust
        // Karpo gave it.
        uint hash = 0x811c_9dc5;
        foreach (var b in Encoding.UTF8.GetBytes(name))
        {
            hash = unchecked((hash ^ b) * 0x0100_0193);
        }
        var fill = Hsl(hash % 360, 0.55f, 0.45f);
        var layout = TextLayout(letter, size * 0.48f, Weight.Semibold, null);
        try
        {
            var glyph = Metrics(layout);
            return Draw(size, size, dc =>
            {
                float inset = Round(size * 0.07f);
                var shape = new D2D1_ROUNDED_RECT
                {
                    rect = new D2D_RECT_F(inset, inset, size - inset, size - inset),
                    radiusX = size * 0.22f,
                    radiusY = size * 0.22f,
                };
                var brush = Brush(dc, fill);
                dc->FillRoundedRectangle(&shape, (ID2D1Brush*)brush);
                brush->Release();
                var origin = new D2D_POINT_2F(
                    Round((size - glyph.width) / 2 - glyph.left),
                    Round((size - glyph.height) / 2));
                DrawText(dc, origin, layout, Ink(1, 1, 1, 1));
            });
        }
        finally
        {
            layout->Release();
        }
    }

    /// <summary>
    /// The Windows 11 logo for the Start button: four squares under one diagonal
    /// gradient, half as wide as the <paramref name="size"/> px surface and centred in it.
    /// </summary>
    public CompositionDrawingSurface WindowsLogo(float size, bool dark)
    {
        // Whole pixels throughout: a square edge that falls between two pixels
        // is drawn as a blurred, half-covered row. At 48 px this gives the
        // taskbar's own 11 + 2 + 11.
        float gap = float.MaxNumber(Round(size * 0.5f / 12), 1);
        float square = Round((size * 0.5f - gap) / 2);
        float logo = 2 * square + gap;
        float left = Round((size - logo) / 2);
        var (start, end) = dark
            ? (Rgb(0x77F3FF), Rgb(0x0B9DFF))
            : (Rgb(0x1EA0FF), Rgb(0x005FB8));
        return Draw(size, size, dc =>
        {
            D2D1_GRADIENT_STOP* stops = stackalloc D2D1_GRADIENT_STOP[2];
            stops[0] = new D2D1_GRADIENT_STOP { position = 0, color = start };
            stops[1] = new D2D1_GRADIENT_STOP { position = 1, color = end };
            ID2D1GradientStopCollection* collection = null;
            ComUtil.Check(dc->CreateGradientStopCollection(
                stops,
                2,
                D2D1_GAMMA.D2D1_GAMMA_2_2,
                D2D1_EXTEND_MODE.D2D1_EXTEND_MODE_CLAMP,
                &collection));
            ID2D1LinearGradientBrush* brush = null;
            try
            {
                var diagonal = new D2D1_LINEAR_GRADIENT_BRUSH_PROPERTIES
                {
                    startPoint = new D2D_POINT_2F(left, left),
                    endPoint = new D2D_POINT_2F(left + logo, left + logo),
                };
                ComUtil.Check(dc->CreateLinearGradientBrush(&diagonal, null, collection, &brush));
            }
            finally
            {
                collection->Release();
            }
            for (int row = 0; row < 2; row++)
            {
                for (int column = 0; column < 2; column++)
                {
                    float x = left + column * (square + gap);
                    float y = left + row * (square + gap);
                    var cell = new D2D_RECT_F(x, y, x + square, y + square);
                    dc->FillRectangle(&cell, (ID2D1Brush*)brush);
                }
            }
            brush->Release();
        });
    }

    /// <summary>The name pill shown over a hovered item, and its size in px.</summary>
    public (CompositionDrawingSurface Surface, Vector2 Size) Pill(string text, float scale)
    {
        var layout = TextLayout(text, 13 * scale, Weight.Semibold, 420 * scale);
        try
        {
            var box = Metrics(layout);
            float padX = Round(10 * scale);
            float padY = Round(5 * scale);
            float width = MathF.Ceiling(
                float.MinNumber(box.widthIncludingTrailingWhitespace, box.layoutWidth) + 2 * padX);
            float height = MathF.Ceiling(box.height + 2 * padY);
            var surface = Draw(width, height, dc =>
            {
                var pill = new D2D1_ROUNDED_RECT
                {
                    rect = new D2D_RECT_F(0, 0, width, height),
                    radiusX = height / 2,
                    radiusY = height / 2,
                };
                var brush = Brush(dc, Ink(0.06f, 0.08f, 0.12f, 0.82f));
                dc->FillRoundedRectangle(&pill, (ID2D1Brush*)brush);
                brush->Release();
                DrawText(dc, new D2D_POINT_2F(padX, padY), layout, Ink(1, 1, 1, 1));
            });
            return (surface, new Vector2(width, height));
        }
        finally
        {
            layout->Release();
        }
    }

    /// <summary>
    /// Lines of text, each centred, stacked, in one surface: the clock's time
    /// over its date. Also a single line, anywhere text is needed.
    /// </summary>
    public (CompositionDrawingSurface Surface, Vector2 Size) Lines(
        IReadOnlyList<(string Text, float Px, Weight Weight)> lines,
        DXGI_RGBA color,
        float? maxWidth)
    {
        var laid = new Laid[lines.Count];
        int count = 0;
        try
        {
            float width = 1;
            float height = 0;
            for (int i = 0; i < lines.Count; i++)
            {
                var (text, px, weight) = lines[i];
                var layout = TextLayout(text, px, weight, maxWidth);
                // Counted before anything else can throw, so the finally
                // releases it.
                laid[count++].Layout = layout;
                var size = Metrics(layout);
                float lineWidth = maxWidth is { } max
                    ? float.MinNumber(size.widthIncludingTrailingWhitespace, max)
                    : size.widthIncludingTrailingWhitespace;
                width = float.MaxNumber(width, MathF.Ceiling(lineWidth));
                laid[i].Width = lineWidth;
                laid[i].Top = height;
                height += size.height;
            }
            height = float.MaxNumber(MathF.Ceiling(height), 1);
            var surface = Draw(width, height, dc =>
            {
                var brush = Brush(dc, color);
                for (int i = 0; i < count; i++)
                {
                    var origin = new D2D_POINT_2F(Round((width - laid[i].Width) / 2), Round(laid[i].Top));
                    dc->DrawTextLayout(
                        origin,
                        laid[i].Layout,
                        (ID2D1Brush*)brush,
                        D2D1_DRAW_TEXT_OPTIONS.D2D1_DRAW_TEXT_OPTIONS_ENABLE_COLOR_FONT);
                }
                brush->Release();
            });
            return (surface, new Vector2(width, height));
        }
        finally
        {
            for (int i = 0; i < count; i++)
            {
                laid[i].Layout->Release();
            }
        }
    }

    public (CompositionDrawingSurface Surface, Vector2 Size) Text(
        string text,
        float px,
        Weight weight,
        DXGI_RGBA color,
        float? maxWidth) =>
        Lines([(text, px, weight)], color, maxWidth);

    /// <summary>
    /// <paramref name="text"/> wrapped onto as many lines as it takes at <paramref name="maxWidth"/>,
    /// a word too long for a line broken where it has to be, and its size: a menu row's second line,
    /// which says the whole of what it says rather than an ellipsis (Flyout's Detail).
    /// </summary>
    public (CompositionDrawingSurface Surface, Vector2 Size) Wrapped(string text, float px, Weight weight, DXGI_RGBA color, float maxWidth)
    {
        // No maximum width for TextLayout: it then sets no trimming, and the wrapping is this one's.
        var layout = TextLayout(text, px, weight, null);
        try
        {
            ComUtil.Check(layout->SetWordWrapping(DWRITE_WORD_WRAPPING.DWRITE_WORD_WRAPPING_WRAP));
            ComUtil.Check(layout->SetMaxWidth(float.MaxNumber(maxWidth, 1)));
            var size = Metrics(layout);
            var width = float.MaxNumber(MathF.Ceiling(float.MinNumber(size.widthIncludingTrailingWhitespace, maxWidth)), 1);
            var height = float.MaxNumber(MathF.Ceiling(size.height), 1);
            // A right-to-left line stands at the box's right: drawn from where its words start.
            var surface = Draw(width, height, dc => DrawText(dc, new D2D_POINT_2F(-size.left, 0), layout, color));
            return (surface, new Vector2(width, height));
        }
        finally
        {
            layout->Release();
        }
    }

    /// <summary>
    /// Whether <paramref name="text"/> is cut with an ellipsis in <paramref name="maxWidth"/>, as a line drawn there is:
    /// the kit's one rule (<see cref="CutWords.Trimmed"/>), for words drawn once and kept (a menu's rows).
    /// </summary>
    public bool Cuts(string text, float px, Weight weight, float maxWidth)
    {
        if (text.Length == 0)
        {
            return false;
        }
        var layout = TextLayout(text, px, weight, maxWidth);
        try
        {
            return CutWords.Trimmed(layout);
        }
        finally
        {
            layout->Release();
        }
    }

    /// <summary>The width and height <paramref name="text"/> needs, without drawing it.</summary>
    public Vector2 Measure(string text, float px, Weight weight)
    {
        var layout = TextLayout(text, px, weight, null);
        try
        {
            var size = Metrics(layout);
            return new Vector2(MathF.Ceiling(size.widthIncludingTrailingWhitespace), MathF.Ceiling(size.height));
        }
        finally
        {
            layout->Release();
        }
    }

    public static DXGI_RGBA Ink(float r, float g, float b, float a) => new(r, g, b, a);

    /// <summary>An opaque colour from its <c>#RRGGBB</c> value.</summary>
    private static DXGI_RGBA Rgb(uint rgb) =>
        Ink(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1);

    public static DXGI_RGBA Hsl(float hue, float saturation, float lightness)
    {
        float chroma = (1 - MathF.Abs(2 * lightness - 1)) * saturation;
        float sector = hue / 60;
        float x = chroma * (1 - MathF.Abs(sector % 2 - 1));
        // A float-to-integer cast saturates in .NET 9 and later, as Rust's
        // `as` does, so a negative or NaN hue lands in sector 0 in both.
        var (r, g, b) = (uint)sector switch
        {
            0 => (chroma, x, 0f),
            1 => (x, chroma, 0f),
            2 => (0f, chroma, x),
            3 => (0f, x, chroma),
            4 => (x, 0f, chroma),
            _ => (chroma, 0f, x),
        };
        float m = lightness - chroma / 2;
        return Ink(r + m, g + m, b + m, 1);
    }

    /// <summary>
    /// Releases the Direct3D, Direct2D and DirectWrite objects. The compositor
    /// and graphics device are left to the projection rather than closed: the
    /// Rust drop only releases a reference too, and surfaces already handed out
    /// still draw from that device.
    /// </summary>
    public void Dispose()
    {
        ComUtil.Release(ref dwrite);
        ComUtil.Release(ref d2d);
        ComUtil.Release(ref factory);
        ComUtil.Release(ref d3d);
    }

    /// <summary>
    /// One line, cut with an ellipsis at <paramref name="maxWidth"/> when there is one,
    /// in <paramref name="family"/> or Segoe UI Variable Text. The caller releases it.
    /// </summary>
    public IDWriteTextLayout* TextLayout(string text, float px, Weight weight, float? maxWidth, string? family = null)
    {
        IDWriteTextFormat* format = null;
        IDWriteInlineObject* ellipsis = null;
        try
        {
            fixed (char* familyName = family ?? TextFamily)
            // The language Karpo speaks, so DirectWrite falls back to that language's font for what
            // Segoe UI lacks: Microsoft YaHei UI for Simplified Chinese, not Japanese's Yu Gothic UI.
            fixed (char* locale = Karpo.Core.Languages.TextLocale)
            {
                ComUtil.Check(dwrite->CreateTextFormat(
                    familyName,
                    null,
                    FontWeight(weight),
                    DWRITE_FONT_STYLE.DWRITE_FONT_STYLE_NORMAL,
                    DWRITE_FONT_STRETCH.DWRITE_FONT_STRETCH_NORMAL,
                    float.MaxNumber(px, 1),
                    locale,
                    &format));
            }
            ComUtil.Check(format->SetWordWrapping(DWRITE_WORD_WRAPPING.DWRITE_WORD_WRAPPING_NO_WRAP));
            if (family is null && !Typeface.IsKarpoFamily && Line() is var (height, baseline))
            {
                // §459: on Segoe UI Variable's line, whatever the font's own (see Typeface).
                var size = float.MaxNumber(px, 1);
                ComUtil.Check(format->SetLineSpacing(DWRITE_LINE_SPACING_METHOD.DWRITE_LINE_SPACING_METHOD_UNIFORM, height * size, baseline * size));
            }
            if (family != IconFamily && Karpo.Core.Languages.ReadsRightToLeft(text))
            {
                // Arabic and Urdu: the line runs right to left, its first words at its right and an
                // ellipsis at its left. Trailing is the left edge then, so the words sit where a
                // left-to-right line's do and every caller's box and place stay as they are: what
                // stands at the right of a menu's row is the menu's to mirror (Mirror.cs).
                ComUtil.Check(format->SetReadingDirection(DWRITE_READING_DIRECTION.DWRITE_READING_DIRECTION_RIGHT_TO_LEFT));
                ComUtil.Check(format->SetTextAlignment(DWRITE_TEXT_ALIGNMENT.DWRITE_TEXT_ALIGNMENT_TRAILING));
            }
            if (maxWidth.HasValue)
            {
                ComUtil.Check(dwrite->CreateEllipsisTrimmingSign(format, &ellipsis));
                var trimming = new DWRITE_TRIMMING
                {
                    granularity = DWRITE_TRIMMING_GRANULARITY.DWRITE_TRIMMING_GRANULARITY_CHARACTER,
                };
                ComUtil.Check(format->SetTrimming(&trimming, ellipsis));
            }
            IDWriteTextLayout* layout = null;
            fixed (char* chars = text)
            {
                ComUtil.Check(dwrite->CreateTextLayout(
                    chars,
                    (uint)text.Length,
                    format,
                    maxWidth ?? 10_000f,
                    10_000f,
                    &layout));
            }
            // Tests only: a test listening for what a drawing says hears this layout's words (PaintWatch).
            PaintWatch.Made(layout, text);
            return layout;
        }
        finally
        {
            ComUtil.Release(ref ellipsis);
            ComUtil.Release(ref format);
        }
    }

    /// <summary>A <paramref name="width"/> × <paramref name="height"/> px surface with <paramref name="paint"/> drawn into it once.</summary>
    public CompositionDrawingSurface Draw(float width, float height, Paint paint)
    {
        var surface = Create(width, height);
        Redraw(surface, paint);
        return surface;
    }

    /// <summary>
    /// <paramref name="glyph"/> from <see cref="IconFamily"/> at <paramref name="px"/>, centred in a transparent
    /// <paramref name="box"/> × <paramref name="box"/> px surface.
    /// </summary>
    public CompositionDrawingSurface Glyph(string glyph, float px, DXGI_RGBA color, float box)
    {
        var surface = Create(box, box);
        RedrawGlyph(surface, glyph, px, color);
        return surface;
    }

    /// <summary>
    /// menus: an SVG document (<paramref name="svg"/>, UTF-8) fitted to a transparent <paramref name="box"/>
    /// × <paramref name="box"/> px surface, by Direct2D's own SVG renderer (Windows 10 1703 on): Explorer's
    /// two-colour icons, read from Windows at runtime (ExplorerIcons). Null where Direct2D can't read it.
    /// </summary>
    public CompositionDrawingSurface? Svg(byte[] svg, float box)
    {
        if (svg.Length == 0 || box < 1)
        {
            return null;
        }
        var surface = Create(box, box);
        var drawn = false;
        try
        {
            Redraw(surface, dc =>
            {
                ID2D1DeviceContext5* five = null;
                IStream* stream = null;
                ID2D1SvgDocument* document = null;
                try
                {
                    if (((IUnknown*)dc)->QueryInterface(__uuidof<ID2D1DeviceContext5>(), (void**)&five).FAILED)
                    {
                        return;
                    }
                    fixed (byte* bytes = svg)
                    {
                        stream = SHCreateMemStream(bytes, (uint)svg.Length);
                    }
                    if (stream is null || five->CreateSvgDocument(stream, new D2D_SIZE_F(box, box), &document).FAILED)
                    {
                        return;
                    }
                    five->DrawSvgDocument(document);
                    drawn = true;
                }
                finally
                {
                    ComUtil.Release(ref document);
                    ComUtil.Release(ref stream);
                    ComUtil.Release(ref five);
                }
            });
        }
        catch
        {
            surface.Dispose();
            throw;
        }
        if (!drawn)
        {
            surface.Dispose();
            return null;
        }
        return surface;
    }

    /// <summary>
    /// <paramref name="glyph"/> drawn again into a surface from <see cref="Glyph"/>, centred in
    /// the size it already has by its ink (<see cref="InkOrigin"/>), so a hover can recolour an
    /// icon in place.
    /// </summary>
    public void RedrawGlyph(CompositionDrawingSurface surface, string glyph, float px, DXGI_RGBA color)
    {
        var box = surface.Size;
        // §589: a glyph a quarter turn (OptionGlyphs.Turned), turned about the surface's middle, where its ink is centred.
        var turned = Karpo.Core.OptionGlyphs.IsTurned(glyph, out var upright);
        var layout = TextLayout(upright, px, Weight.Regular, null, IconFamily);
        try
        {
            var origin = InkOrigin(layout, (float)box.Width, (float)box.Height);
            Redraw(surface, dc =>
            {
                if (!turned)
                {
                    DrawText(dc, origin, layout, color);
                    return;
                }
                var was = Mirror.Quarter(dc, (float)box.Width / 2, (float)box.Height / 2);
                DrawText(dc, origin, layout, color);
                dc->SetTransform(&was);
            });
        }
        finally
        {
            layout->Release();
        }
    }

    /// <summary>
    /// Paints all of <paramref name="surface"/> again, at the size it already has. Every
    /// visual showing it, in every dock window, picks up the new pixels without
    /// being touched, which is what lets a live island redraw every second for
    /// the price of one draw.
    /// </summary>
    public void Redraw(CompositionDrawingSurface surface, Paint paint)
    {
        var interop = ComUtil.Query<ICompositionDrawingSurfaceInterop>(surface);
        ID2D1DeviceContext* dc = null;
        try
        {
            POINT offset = default;
            ComUtil.Check(interop->BeginDraw(null, __uuidof<ID2D1DeviceContext>(), (void**)&dc, &offset));
            // The context is a slice of a shared atlas at a DPI of its choosing.
            // Pin both, or a pixel here is not a pixel on screen.
            dc->SetDpi(96, 96);
            var shift = D2D_MATRIX_3X2_F.Translation(offset.x, offset.y);
            dc->SetTransform(&shift);
            var clear = default(DXGI_RGBA);
            dc->Clear(&clear);
            try
            {
                paint(dc);
            }
            finally
            {
                // Only one surface can be mid-draw at a time, so a paint that
                // failed still ends its draw, or every later draw fails too.
                // An EndDraw failure replaces the paint's, as in the Rust.
                ComUtil.Check(interop->EndDraw());
            }
        }
        finally
        {
            ComUtil.Release(ref dc);
            interop->Release();
        }
    }

    /// <summary>
    /// Direct2D reads width × height × 4 bytes from the pinned array whatever its
    /// length, so a short one would be read past its end. Rust's u32 sizes
    /// cannot be negative; these ints can.
    /// </summary>
    private static void Validate(Pixels pixels)
    {
        if (pixels.Width < 0 || pixels.Height < 0
            || pixels.Data.Length < (long)pixels.Width * pixels.Height * 4)
        {
            throw new ArgumentException(
                $"{pixels.Data.Length} bytes cannot hold {pixels.Width}×{pixels.Height} BGRA",
                nameof(pixels));
        }
    }

    /// <summary>
    /// <paramref name="pixels"/> fitted and centred in a <paramref name="side"/> px square at
    /// (<paramref name="left"/>, <paramref name="top"/>); pins draw a broken one faintly with
    /// <paramref name="opacity"/>.
    /// </summary>
    internal static void DrawPixels(ID2D1DeviceContext* dc, Pixels pixels, float left, float top, float side, float opacity = 1f)
    {
        var properties = new D2D1_BITMAP_PROPERTIES1
        {
            pixelFormat = new D2D1_PIXEL_FORMAT
            {
                format = DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM,
                alphaMode = D2D1_ALPHA_MODE.D2D1_ALPHA_MODE_PREMULTIPLIED,
            },
            dpiX = 96,
            dpiY = 96,
            bitmapOptions = D2D1_BITMAP_OPTIONS.D2D1_BITMAP_OPTIONS_NONE,
            colorContext = null,
        };
        ID2D1Bitmap1* bitmap = null;
        fixed (byte* data = pixels.Data)
        {
            ComUtil.Check(dc->CreateBitmap(
                new D2D_SIZE_U((uint)pixels.Width, (uint)pixels.Height),
                data,
                (uint)pixels.Width * 4,
                &properties,
                &bitmap));
        }
        float fit = side / Math.Max(pixels.Width, pixels.Height);
        float width = pixels.Width * fit;
        float height = pixels.Height * fit;
        var into = new D2D_RECT_F(
            left + (side - width) / 2,
            top + (side - height) / 2,
            left + (side + width) / 2,
            top + (side + height) / 2);
        // A picture in a mirrored drawing stands where the mirror puts it, the right way round (Mirror.cs).
        var was = Mirror.Upright(dc, left, left + side);
        dc->DrawBitmap(
            (ID2D1Bitmap*)bitmap,
            &into,
            Math.Clamp(opacity, 0f, 1f),
            D2D1_INTERPOLATION_MODE.D2D1_INTERPOLATION_MODE_HIGH_QUALITY_CUBIC,
            null,
            null);
        dc->SetTransform(&was);
        bitmap->Release();
    }

    private CompositionDrawingSurface Create(float width, float height) =>
        device.CreateDrawingSurface(
            new Windows.Foundation.Size(float.MaxNumber(width, 1), float.MaxNumber(height, 1)),
            DirectXPixelFormat.B8G8R8A8UIntNormalized,
            DirectXAlphaMode.Premultiplied);

    private bool Installed(string family)
    {
        IDWriteFontCollection* fonts = null;
        // A lookup that fails says nothing about the font, so it keeps the
        // one Windows 11 has.
        if (dwrite->GetSystemFontCollection(&fonts, BOOL.FALSE).FAILED)
        {
            return true;
        }
        try
        {
            uint index = 0;
            BOOL exists = BOOL.FALSE;
            fixed (char* name = family)
            {
                return fonts->FindFamilyName(name, &index, &exists).FAILED || exists;
            }
        }
        finally
        {
            fonts->Release();
        }
    }

    /// <summary>
    /// Where to draw <paramref name="layout"/> so that its ink, not its line box, sits in the
    /// middle of a <paramref name="width"/> × <paramref name="height"/> box, on whole pixels. An icon
    /// glyph's line box carries the font's ascent and descent, which would put the glyph
    /// visibly off centre.
    /// </summary>
    /// <summary>
    /// Where to draw a layout so its ink sits in the middle of a width × height box, on whole
    /// pixels. Icons are placed by their ink and never by their box: Segoe Fluent gives every icon
    /// the same advance and not the same ink — the speaker's waves stretch to the right edge of
    /// the box as the volume rises, so a speaker placed by its box drifts left as it quietens, by a
    /// third of the glyph at the lowest level. Two glyphs drawn one over the other (the Wi-Fi arcs
    /// over the faint full set) must share one origin, and it is the fuller one's: the caller lays
    /// that one out and places both by it.
    /// </summary>
    internal static D2D_POINT_2F InkOrigin(IDWriteTextLayout* layout, float width, float height)
    {
        DWRITE_OVERHANG_METRICS overhang = default;
        ComUtil.Check(layout->GetOverhangMetrics(&overhang));
        // Overhangs are measured outward from the edges of the layout's own
        // max-width × max-height box.
        float left = -overhang.left;
        float top = -overhang.top;
        float right = layout->GetMaxWidth() + overhang.right;
        float bottom = layout->GetMaxHeight() + overhang.bottom;
        return new D2D_POINT_2F(
            Round((width - (right - left)) / 2 - left),
            Round((height - (bottom - top)) / 2 - top));
    }

    private static HRESULT D3d(D3D_DRIVER_TYPE driver, ID3D11Device** device)
    {
        *device = null;
        HRESULT hr = D3D11CreateDevice(
            null,
            driver,
            HMODULE.NULL,
            (uint)D3D11_CREATE_DEVICE_FLAG.D3D11_CREATE_DEVICE_BGRA_SUPPORT,
            null,
            0,
            D3D11.D3D11_SDK_VERSION,
            device,
            null,
            null);
        return hr.SUCCEEDED && *device == null ? (HRESULT)E.E_FAIL : hr;
    }

    private static DWRITE_TEXT_METRICS Metrics(IDWriteTextLayout* layout)
    {
        DWRITE_TEXT_METRICS size = default;
        ComUtil.Check(layout->GetMetrics(&size));
        return size;
    }

    /// <summary>A solid brush on <paramref name="dc"/>. The caller releases it.</summary>
    public static ID2D1SolidColorBrush* Brush(ID2D1DeviceContext* dc, DXGI_RGBA color)
    {
        ID2D1SolidColorBrush* brush = null;
        ComUtil.Check(dc->CreateSolidColorBrush(&color, null, &brush));
        return brush;
    }

    private static void DrawText(ID2D1DeviceContext* dc, D2D_POINT_2F origin, IDWriteTextLayout* layout, DXGI_RGBA color)
    {
        var brush = Brush(dc, color);
        dc->DrawTextLayout(
            origin,
            layout,
            (ID2D1Brush*)brush,
            D2D1_DRAW_TEXT_OPTIONS.D2D1_DRAW_TEXT_OPTIONS_ENABLE_COLOR_FONT);
        brush->Release();
    }

    /// <summary>
    /// Rust's round: halves go away from zero. MathF.Round alone sends them to
    /// the even neighbour, which moves some edges by a pixel.
    /// </summary>
    private static float Round(float value) => MathF.Round(value, MidpointRounding.AwayFromZero);

    /// <summary>§459: a role's weight in <see cref="Typeface"/>'s: the body's or the strong one's (<see cref="Karpo.Core.TextStyle.Weights"/>).</summary>
    private DWRITE_FONT_WEIGHT FontWeight(Weight weight) =>
        (DWRITE_FONT_WEIGHT)(weight == Weight.Semibold ? Typeface.Weights.Strong : Typeface.Weights.Body);

    /// <summary>
    /// §459: Segoe UI Variable Text's line over its em — its height and its baseline —, measured once off a line of it at
    /// 100 px, which every other font is laid out on.
    /// </summary>
    private (float Height, float Baseline) Line()
    {
        if (line is { } known)
        {
            return known;
        }
        IDWriteTextFormat* format = null;
        IDWriteTextLayout* layout = null;
        try
        {
            fixed (char* familyName = TextFont)
            fixed (char* locale = "en-us")
            {
                ComUtil.Check(dwrite->CreateTextFormat(familyName, null, DWRITE_FONT_WEIGHT.DWRITE_FONT_WEIGHT_NORMAL, DWRITE_FONT_STYLE.DWRITE_FONT_STYLE_NORMAL,
                    DWRITE_FONT_STRETCH.DWRITE_FONT_STRETCH_NORMAL, 100, locale, &format));
            }
            fixed (char* chars = "Ag")
            {
                ComUtil.Check(dwrite->CreateTextLayout(chars, 2, format, 10_000f, 10_000f, &layout));
            }
            DWRITE_LINE_METRICS metrics = default;
            uint count = 0;
            ComUtil.Check(layout->GetLineMetrics(&metrics, 1, &count));
            line = (metrics.height / 100f, metrics.baseline / 100f);
        }
        catch
        {
            line = (1.33f, 1.064f);
        }
        finally
        {
            ComUtil.Release(ref layout);
            ComUtil.Release(ref format);
        }
        return line.Value;
    }
}
