// Tests only: where a widget's drawing puts its text, its glyphs and its shapes, seen as they are
// drawn.
//
// "A taskbar entry is one row" (docs\widgets.md §12.5) is a claim about the drawing, so the tests
// check it on the drawing rather than on a plan the drawing might not follow. Every line of text
// and every glyph a cell draws goes through one of two places — TextLine.Draw and PopupPaint's own
// Draw — and the few shapes that stand beside them as pieces of their own (a drive's capacity bar,
// a performance graph, a track's art) say where they are with Shape. A test on the painting thread
// can watch them all go by: each one's box in the painter's own coordinates, and what it is. It may
// answer with another colour for the text, so a picture can tell text ink from everything else.
//
// Nothing in the dock sets it. With nobody watching, a draw costs one null check.

using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

/// <summary>What a watched piece of a drawing is (<see cref="PaintWatch"/>).</summary>
internal enum Painted
{
    /// <summary>A line of text.</summary>
    Text,

    /// <summary>A glyph of the icon font.</summary>
    Glyph,

    /// <summary>A shape that is a piece of its own: a bar, a graph, a picture.</summary>
    Shape,
}

/// <summary>What a piece seen for its style is (<see cref="PaintWatch.Styled"/>).</summary>
internal enum PaintedKind
{
    Text,
    Glyph,
    Fill,
    Outline,
}

/// <summary>What a piece the kit keeps apart from others is (<see cref="PaintWatch.Spaced"/>, §449).</summary>
internal enum Spacing
{
    /// <summary>A meter, a progress bar, a timeline, a slider's track as far as its thumb goes.</summary>
    Meter,

    /// <summary>A button (plated or subtle), a switch, a refresh button: what a press lands on.</summary>
    Control,
}

/// <summary>
/// One piece's style as it was drawn (<see cref="PaintWatch.Styled"/>): its box in the painter's own
/// coordinates, a text's or a glyph's size in px, weight and family, its ink, and a fill's corner in px.
/// </summary>
internal readonly record struct PaintedStyle(PaintedKind What, D2D_RECT_F Box, float Px, int Weight, string Family, DXGI_RGBA Ink, float Radius);

internal static unsafe class PaintWatch
{
    /// <summary>
    /// Shown one piece of a drawing as it is drawn: its box in the painter's own coordinates (a line
    /// of text's or a glyph's line box), what it is, and the colour it was about to be drawn in.
    /// Answers the colour to draw it in — the same one, unless the test wants text told apart. A
    /// shape's colour is not asked for and the answer is not used.
    /// </summary>
    internal delegate DXGI_RGBA Watcher(D2D_RECT_F box, Painted what, DXGI_RGBA color);

    /// <summary>Per thread, so a test watching its own painting can't see another test's beside it.</summary>
    [ThreadStatic]
    private static Watcher? watching;

    internal static Watcher? Watching
    {
        get => watching;
        set => watching = value;
    }

    /// <summary>
    /// Leaves out every graph drawn as a widget's ground (Graphs\GraphBackdrop.cs), on this thread,
    /// while set: a test that reads a drawing's words back a row of pixels at a time reads them
    /// without the graph behind them, and reads the graph from a second drawing with it.
    /// </summary>
    [ThreadStatic]
    internal static bool NoBackdrops;

    /// <summary>
    /// How many lines drawn while somebody watched were cut with an ellipsis (DirectWrite trimmed
    /// them). A test that holds a drawing to its whole words sees a line that was cut here: counted per thread, and only while a watcher is set.
    /// </summary>
    [ThreadStatic]
    internal static int Trimmed;

    /// <summary>
    /// Told each line of text drawn on this thread with its words and its box, while it is set: the
    /// text its layout was made from (<see cref="Made"/>) and where it went. A test that holds a
    /// drawing to what it says — a list line one row tall showing its reading — listens here. Per
    /// thread, and nothing is remembered while nobody listens.
    /// </summary>
    [ThreadStatic]
    private static Action<string, D2D_RECT_F>? said;

    /// <summary>The words of the layouts made while somebody listens, by layout (<see cref="Made"/>).</summary>
    [ThreadStatic]
    private static Dictionary<nint, string>? words;

    internal static Action<string, D2D_RECT_F>? Said
    {
        get => said;
        set
        {
            said = value;
            words = null;
        }
    }

    /// <summary>A layout made from <paramref name="text"/> (Gfx.TextLayout), remembered while somebody listens for what is said.</summary>
    internal static void Made(IDWriteTextLayout* layout, string text)
    {
        if (said is null || layout == null)
        {
            return;
        }
        (words ??= [])[(nint)layout] = text;
    }

    /// <summary>
    /// The colour <paramref name="layout"/> is to be drawn in at <paramref name="origin"/>, after the
    /// watcher, if there is one, has seen it.
    /// </summary>
    internal static DXGI_RGBA Seen(IDWriteTextLayout* layout, D2D_POINT_2F origin, DXGI_RGBA color)
    {
        if (said is { } listener && layout != null && words is { } made && made.TryGetValue((nint)layout, out var text))
        {
            DWRITE_TEXT_METRICS spoken;
            if (layout->GetMetrics(&spoken).SUCCEEDED)
            {
                var at = origin.x + spoken.left;
                listener(text, new D2D_RECT_F(at, origin.y + spoken.top, at + spoken.widthIncludingTrailingWhitespace, origin.y + spoken.top + spoken.height));
            }
        }
        if ((watching is null && styled is null) || layout == null)
        {
            return color;
        }
        DWRITE_TEXT_METRICS metrics;
        if (layout->GetMetrics(&metrics).FAILED)
        {
            return color;
        }
        if (styled is not null)
        {
            var at = origin.x + metrics.left;
            Style(layout, new D2D_RECT_F(at, origin.y + metrics.top, at + metrics.widthIncludingTrailingWhitespace, origin.y + metrics.top + metrics.height), color);
        }
        if (watching is not { } watcher)
        {
            return color;
        }
        DWRITE_LINE_METRICS line;
        uint lines;
        if (layout->GetLineMetrics(&line, 1, &lines).SUCCEEDED && lines >= 1 && line.isTrimmed)
        {
            Trimmed++;
        }
        var left = origin.x + metrics.left;
        var top = origin.y + metrics.top;
        var box = new D2D_RECT_F(left, top, left + metrics.widthIncludingTrailingWhitespace, top + metrics.height);
        return watcher(box, IsGlyph(layout) ? Painted.Glyph : Painted.Text, color);
    }

    /// <summary>A shape drawn in <paramref name="box"/> as a piece of its own, for the watcher to see.</summary>
    internal static void Shape(in D2D_RECT_F box)
    {
        if (watching is { } watcher)
        {
            _ = watcher(box, Painted.Shape, default);
        }
    }

    /// <summary>
    /// Told the style of each line of text, glyph and fill drawn on this thread, while it is set
    /// (LineStyleTests, §388): what it is, its box, its size, weight and family, its ink, and a fill's
    /// corner. Per thread, and nothing is read while nobody listens.
    /// </summary>
    [ThreadStatic]
    private static Action<PaintedStyle>? styled;

    internal static Action<PaintedStyle>? Styled
    {
        get => styled;
        set => styled = value;
    }

    /// <summary>A rounded fill or outline in <paramref name="box"/> with corners <paramref name="radius"/> px, for the listener.</summary>
    internal static void Rounded(in D2D_RECT_F box, float radius, DXGI_RGBA color, bool outline)
    {
        if (styled is { } listener)
        {
            listener(new PaintedStyle(outline ? PaintedKind.Outline : PaintedKind.Fill, box, 0, 0, "", color, radius));
        }
    }

    /// <summary>
    /// Told each meter and each control drawn on this thread, its box in the painter's own coordinates, while it is set
    /// (ListLinesControlsTests, §449: a meter keeps a button's gap from every control). Per thread.
    /// </summary>
    [ThreadStatic]
    private static Action<Spacing, D2D_RECT_F>? spaced;

    internal static Action<Spacing, D2D_RECT_F>? Spaced
    {
        get => spaced;
        set => spaced = value;
    }

    /// <summary>A meter or a control drawn in <paramref name="box"/>, for the listener.</summary>
    internal static void Space(Spacing what, in D2D_RECT_F box) => spaced?.Invoke(what, box);

    private static void Style(IDWriteTextLayout* layout, in D2D_RECT_F box, DXGI_RGBA color)
    {
        if (styled is not { } listener)
        {
            return;
        }
        const int room = 64;
        var name = stackalloc char[room];
        var family = layout->GetFontFamilyName(0, name, room, null).SUCCEEDED ? new string(name) : "";
        var glyph = family is Gfx.IconFont or Gfx.OlderIconFont;
        float size = 0;
        _ = layout->GetFontSize(0, &size, null);
        DWRITE_FONT_WEIGHT weight = default;
        _ = layout->GetFontWeight(0, &weight, null);
        listener(new PaintedStyle(glyph ? PaintedKind.Glyph : PaintedKind.Text, box, size, (int)weight, family, color, 0));
    }

    /// <summary>Whether the layout is set in the icon font: a glyph, not a line of text.</summary>
    private static bool IsGlyph(IDWriteTextLayout* layout)
    {
        const int room = 64;
        var name = stackalloc char[room];
        if (layout->GetFontFamilyName(0, name, room, null).FAILED)
        {
            return false;
        }
        var family = new string(name);
        return family is Gfx.IconFont or Gfx.OlderIconFont;
    }
}
