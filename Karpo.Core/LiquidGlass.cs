// §704 (the owner, 2026-11-15: a "Liquid glass" look, Apple's Liquid Glass of iOS 26 and macOS Tahoe, as Themia draws it,
// its SPEC [S-2123]; docs\widgets.md §704; the same day: "the new, modern iOS 26 look that Themia now has"): the one palette
// that is a material, not colours alone. Its values here, pure; the palette (LookPalettes.LiquidGlassId) says its colours,
// and the dock's painters draw the rest of it where the palette is worn (LookPaints.Glass): bottom up,
//
//   1 lift      a large, very faint shadow, black, where the glass stands on a frosted backdrop (a floating bar's panel):
//               darkest at the glass's edge (ShadowOpacity), falling off as (1 − d / ShadowBlurDip)² (Lift)
//   2 frost     the wallpaper blurred under it: Karpo's own wallpaper blur, as Acrylic's and Glacier's bars (§547)
//   3 tint      white in the light mode, #1C1C1E in the dark, as clear as the text over it lets it be (TintOpacity)
//   4 glow      the light falling in at the top edge, GlowDip deep: no sheen over the rest of the surface
//   5 lens      LensBands bands LensBandDip wide from the rim in, each its weight times the Lens profile down the glass
//   6 content
//   7 dividers  between the parts of a joined glass (a folder's lines in one piece, a flush bar's cards side by side): a
//               hairline DividerInsetDip short of the glass's outer edge (Join)
//   8 rim       a 1 DIP outline, white, its light running down the glass: bright along the top, fading down the sides,
//               catching again along the foot
//
// Every stop's offset runs down the whole glass shape (a joined set's: Join's body). What is approximated, as in Themia: the
// refraction of what lies behind at the edges is the lens's stepped bands, not sampled; the light is fixed from above (it
// follows no pointer or tilt); the blur is the bar's one wallpaper blur, not a stronger one of the glass's own; Apple's
// continuous corners are circular arcs.

namespace Karpo.Core;

/// <summary>§704: one stop of a glass gradient: where along it (0 to 1) and white's alpha there (0 to 1).</summary>
public readonly record struct GlassStop(double Offset, double Alpha);

/// <summary>
/// §704: Liquid glass in one of Windows' modes, every layer's value (see the top of the file). One instance a mode
/// (<see cref="LiquidGlass.Light"/>, <see cref="LiquidGlass.Dark"/>), so a look that carries it compares by which.
/// </summary>
public sealed record GlassLook(
    bool Dark,
    Argb Tint,
    double TintMin,
    double TintMax,
    Argb Text,
    IReadOnlyList<GlassStop> Glow,
    float GlowDip,
    IReadOnlyList<GlassStop> Lens,
    IReadOnlyList<double> LensBands,
    float LensBandDip,
    IReadOnlyList<GlassStop> Rim,
    float RimDip,
    Argb Divider,
    float DividerDip,
    float DividerInsetDip,
    double ShadowOpacity,
    float ShadowBlurDip,
    float ShadowOffsetDip,
    float RadiusDip,
    Argb FlyoutOutline)
{
    /// <summary>The tint at its most, as the palette designs it: what a surface over a backdrop not known wears.</summary>
    public Argb Designed => Tint.At(TintMax);

    /// <summary>
    /// §704: lens band <paramref name="band"/>'s stops (0 the outermost, against the rim): the <see cref="Lens"/> profile at
    /// that band's weight, so the light fades in from the edge; all clear for a band the glass has not.
    /// </summary>
    public IReadOnlyList<GlassStop> LensStops(int band)
    {
        var weight = band >= 0 && band < LensBands.Count ? LensBands[band] : 0;
        return [.. Lens.Select(stop => stop with { Alpha = stop.Alpha * weight })];
    }

    /// <summary>
    /// §704: how dark the lift makes a point <paramref name="dip"/> DIPs out from the glass's edge: <see cref="ShadowOpacity"/>
    /// at the edge, falling off as (1 − d / <see cref="ShadowBlurDip"/>)², nothing past it (Themia's falloff).
    /// </summary>
    public double Lift(double dip) => ShadowOpacity * Math.Pow(Math.Max(0, 1 - (Math.Max(0, dip) / ShadowBlurDip)), 2);

    /// <summary>
    /// §704: how opaque the tint is over a backdrop of relative luminance <paramref name="behind"/> (0 black to 1 white; null
    /// where none is known, which takes <see cref="TintMax"/>): the least, from <see cref="TintMin"/> up, at which
    /// <see cref="Text"/> reads at <see cref="ToneRules.TextContrast"/> on the glass — the tint and the backdrop mixed in sRGB as
    /// the compositor mixes them (bg = back + (tint − back)·α), linearised for the luminance —, found by halving to a hundredth;
    /// <see cref="TintMax"/> where even that does not reach it. Over a dark wallpaper light glass clouds over, over a bright
    /// one it stays clear; dark glass the other way. Apple's adaptive tint.
    /// </summary>
    public double TintOpacity(double? behind)
    {
        if (behind is not { } luminance || !double.IsFinite(luminance))
        {
            return TintMax;
        }
        var back = LiquidGlass.ToSrgb(Math.Clamp(luminance, 0, 1));
        var tint = LiquidGlass.ToSrgb(ToneRules.Luminance(Tint));
        var text = ToneRules.Luminance(Text);
        bool Reads(double alpha) => LiquidGlass.Contrast(LiquidGlass.ToLinear(back + ((tint - back) * alpha)), text) >= ToneRules.TextContrast;
        if (Reads(TintMin))
        {
            return TintMin;
        }
        if (!Reads(TintMax))
        {
            return TintMax;
        }
        // The contrast grows with the tint between the two: the least that reads, to a hundredth — rounded up, so what is
        // drawn always reads (Themia rounds to the nearest, which can fall a trace under 4.5:1).
        var (low, high) = (TintMin, TintMax);
        while (high - low > 0.005)
        {
            var mid = (low + high) / 2;
            (low, high) = Reads(mid) ? (low, mid) : (mid, high);
        }
        var least = Math.Ceiling((high * 100) - 1e-9) / 100;
        return Math.Min(TintMax, Reads(least - 0.01) && least - 0.01 >= TintMin ? least - 0.01 : least);
    }

    /// <summary>
    /// §704: what a surface said to be <paramref name="said"/> is filled with over a backdrop of luminance
    /// <paramref name="behind"/>: the tint at <see cref="TintOpacity"/> where it is the palette's own tint
    /// (<see cref="Designed"/>); anything else is a colour somebody chose, which overrides the tint and is drawn as chosen.
    /// </summary>
    public Argb Tinted(Argb said, double? behind) => said == Designed ? Tint.At(TintOpacity(behind)) : said;
}

/// <summary>§704: a box of a glass surface in any one unit (px): left, top, right, bottom.</summary>
public readonly record struct GlassBox(double Left, double Top, double Right, double Bottom)
{
    public double Width => Right - Left;

    public double Height => Bottom - Top;
}

/// <summary>
/// §704: a surface's part of a joined glass (<see cref="LiquidGlass.Join"/>): the whole glass shape it is part of
/// (<see cref="Body"/>, its own box where it stands alone), and the hairline on its leading side toward the mate before it
/// (<see cref="Divider"/>, a box a hairline thick; null for none).
/// </summary>
public readonly record struct GlassPart(GlassBox Body, GlassBox? Divider);

/// <summary>§704: Liquid glass's values and the reading of a backdrop's light (see the top of the file).</summary>
public static class LiquidGlass
{
    private static readonly Argb White = new(255, 0xFF, 0xFF, 0xFF);

    /// <summary>§704: the light mode's glass: a white tint barely there, a crisp bright rim, a faint lift.</summary>
    public static GlassLook Light { get; } = new(
        Dark: false,
        Tint: White,
        TintMin: 0.06,
        TintMax: 0.70,
        Text: new Argb(255, 0x1A, 0x1A, 0x1A),
        Glow: [new(0, 0.16), new(0.35, 0.05), new(1, 0)],
        GlowDip: 22,
        Lens: [new(0, 0.80), new(0.12, 0.40), new(0.5, 0.16), new(0.88, 0.34), new(1, 0.66)],
        LensBands: [0.34, 0.18, 0.08],
        LensBandDip: 2,
        Rim: [new(0, 0.92), new(0.06, 0.58), new(0.3, 0.16), new(0.55, 0.08), new(0.9, 0.24), new(1, 0.62)],
        RimDip: 1,
        Divider: new Argb(255, 0, 0, 0).At(0.10),
        DividerDip: 1,
        DividerInsetDip: 14,
        ShadowOpacity: 0.10,
        ShadowBlurDip: 40,
        ShadowOffsetDip: 12,
        RadiusDip: 24,
        FlyoutOutline: White.At(0.30));

    /// <summary>§704: the dark mode's glass: a graphite tint, the light dimmer, the lift a little deeper.</summary>
    public static GlassLook Dark { get; } = new(
        Dark: true,
        Tint: new Argb(255, 0x1C, 0x1C, 0x1E),
        TintMin: 0.14,
        TintMax: 0.78,
        Text: White,
        Glow: [new(0, 0.09), new(0.35, 0.03), new(1, 0)],
        GlowDip: 22,
        Lens: [new(0, 0.60), new(0.12, 0.26), new(0.5, 0.10), new(0.88, 0.22), new(1, 0.46)],
        LensBands: [0.34, 0.18, 0.08],
        LensBandDip: 2,
        Rim: [new(0, 0.56), new(0.06, 0.34), new(0.3, 0.10), new(0.55, 0.05), new(0.9, 0.13), new(1, 0.36)],
        RimDip: 1,
        Divider: White.At(0.12),
        DividerDip: 1,
        DividerInsetDip: 14,
        ShadowOpacity: 0.24,
        ShadowBlurDip: 40,
        ShadowOffsetDip: 12,
        RadiusDip: 24,
        FlyoutOutline: White.At(0.20));

    /// <summary>The glass of the mode <paramref name="dark"/>.</summary>
    public static GlassLook For(bool dark) => dark ? Dark : Light;

    /// <summary>The light the glow, the lens and the rim are drawn in, at their stops' alphas: white.</summary>
    public static Argb Highlight => White;

    /// <summary>
    /// §704: <paramref name="boxes"/> (surfaces of glass, in one unit) joined where they meet — side by side with the same top
    /// and bottom, or one on another with the same left and right, within <paramref name="touch"/> — into one glass shape a
    /// set: each box's part, its set's body (the bounds of all of it), and the hairline toward the mate before it (to its
    /// left, else above), <paramref name="hairline"/> thick on the box's own side of the seam and <paramref name="inset"/>
    /// short of the body's outer edges; none where that leaves nothing. A box that meets none is a glass of its own. As
    /// Themia's groups (CardJoins.Bodies): one rim, one glow and lens down the body, the parts parted by hairlines.
    /// </summary>
    public static IReadOnlyList<GlassPart> Join(IReadOnlyList<GlassBox> boxes, double hairline, double inset, double touch = 1)
    {
        ArgumentNullException.ThrowIfNull(boxes);
        var count = boxes.Count;
        var root = Enumerable.Range(0, count).ToArray();
        int Find(int i) => root[i] == i ? i : root[i] = Find(root[i]);
        bool Near(double a, double b) => Math.Abs(a - b) <= touch;
        bool Beside(GlassBox a, GlassBox b) => Near(a.Right, b.Left) && Near(a.Top, b.Top) && Near(a.Bottom, b.Bottom);
        bool Below(GlassBox a, GlassBox b) => Near(a.Bottom, b.Top) && Near(a.Left, b.Left) && Near(a.Right, b.Right);
        bool Some(GlassBox box) => box.Width > 0 && box.Height > 0;
        var before = new (int Mate, bool Side)?[count];
        for (var a = 0; a < count; a++)
        {
            for (var b = 0; b < count; b++)
            {
                if (a == b || !Some(boxes[a]) || !Some(boxes[b]))
                {
                    continue;
                }
                var side = Beside(boxes[a], boxes[b]);
                if (side || Below(boxes[a], boxes[b]))
                {
                    root[Find(a)] = Find(b);
                    before[b] ??= (a, side);
                }
            }
        }
        var bodies = new Dictionary<int, GlassBox>();
        for (var i = 0; i < count; i++)
        {
            var (box, set) = (boxes[i], Find(i));
            bodies[set] = bodies.TryGetValue(set, out var body)
                ? new GlassBox(Math.Min(body.Left, box.Left), Math.Min(body.Top, box.Top), Math.Max(body.Right, box.Right), Math.Max(body.Bottom, box.Bottom))
                : box;
        }
        var parts = new GlassPart[count];
        for (var i = 0; i < count; i++)
        {
            var (box, body) = (boxes[i], bodies[Find(i)]);
            GlassBox? divider = before[i] switch
            {
                { Side: true } when body.Height - (2 * inset) > 0 => new GlassBox(box.Left, body.Top + inset, box.Left + hairline, body.Bottom - inset),
                { Side: false } when body.Width - (2 * inset) > 0 => new GlassBox(body.Left + inset, box.Top, body.Right - inset, box.Top + hairline),
                _ => null,
            };
            parts[i] = new GlassPart(body, divider);
        }
        return parts;
    }

    /// <summary>§704: how many cells of a backdrop's light <see cref="Grid"/> keeps across and down.</summary>
    public const int GridColumns = 32, GridRows = 18;

    /// <summary>How many samples a cell's light is the mean of, across and down: a few are enough on a blurred picture.</summary>
    private const int Samples = 4;

    /// <summary>
    /// §704: a picture's light as a grid of <paramref name="columns"/> × <paramref name="rows"/> cells, each the mean relative
    /// luminance of <see cref="Samples"/> × <see cref="Samples"/> of its pixels, row after row: what <see cref="Mean"/> reads
    /// under a surface. The picture is BGRA, opaque, top row first, <paramref name="width"/> × <paramref name="height"/>.
    /// </summary>
    public static float[] Grid(byte[] bgra, int width, int height, int columns = GridColumns, int rows = GridRows)
    {
        ArgumentNullException.ThrowIfNull(bgra);
        if (width <= 0 || height <= 0 || columns <= 0 || rows <= 0 || bgra.Length < (long)width * height * 4)
        {
            throw new ArgumentException("a picture of width × height BGRA pixels and a grid of at least one cell", nameof(bgra));
        }
        var grid = new float[columns * rows];
        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                double sum = 0;
                for (var sy = 0; sy < Samples; sy++)
                {
                    var y = Math.Min(height - 1, (int)((row + ((sy + 0.5) / Samples)) * height / rows));
                    for (var sx = 0; sx < Samples; sx++)
                    {
                        var x = Math.Min(width - 1, (int)((column + ((sx + 0.5) / Samples)) * width / columns));
                        var at = ((y * width) + x) * 4;
                        sum += ToneRules.Luminance(new Argb(255, bgra[at + 2], bgra[at + 1], bgra[at]));
                    }
                }
                grid[(row * columns) + column] = (float)(sum / (Samples * Samples));
            }
        }
        return grid;
    }

    /// <summary>
    /// §704: the mean light of <paramref name="grid"/> under a box given as fractions of its picture (left, top, right,
    /// bottom; held to it), each cell weighed by how much of it the box covers; null for a box outside it or of no size.
    /// </summary>
    public static double? Mean(float[] grid, int columns, int rows, double left, double top, double right, double bottom)
    {
        ArgumentNullException.ThrowIfNull(grid);
        (left, right) = (Math.Clamp(left, 0, 1), Math.Clamp(right, 0, 1));
        (top, bottom) = (Math.Clamp(top, 0, 1), Math.Clamp(bottom, 0, 1));
        if (right <= left || bottom <= top || columns <= 0 || rows <= 0 || grid.Length < columns * rows)
        {
            return null;
        }
        double sum = 0, weight = 0;
        for (var row = 0; row < rows; row++)
        {
            var h = Math.Min(bottom, (row + 1.0) / rows) - Math.Max(top, (double)row / rows);
            if (h <= 0)
            {
                continue;
            }
            for (var column = 0; column < columns; column++)
            {
                var w = Math.Min(right, (column + 1.0) / columns) - Math.Max(left, (double)column / columns);
                if (w <= 0)
                {
                    continue;
                }
                sum += grid[(row * columns) + column] * w * h;
                weight += w * h;
            }
        }
        return weight > 0 ? sum / weight : null;
    }

    /// <summary>
    /// §704: the light under the middle of a picture <paramref name="margin"/> px in from each of its edges (a bar's wallpaper
    /// strip and the margin its blur reads past the bar): <see cref="Mean"/> of its <see cref="Grid"/>; null where nothing is left.
    /// </summary>
    public static double? Behind(byte[] bgra, int width, int height, int margin)
    {
        if (width <= 2 * margin || height <= 2 * margin)
        {
            return null;
        }
        var grid = Grid(bgra, width, height);
        return Mean(grid, GridColumns, GridRows, margin / (double)width, margin / (double)height, 1 - (margin / (double)width), 1 - (margin / (double)height));
    }

    /// <summary>WCAG's contrast ratio of two relative luminances.</summary>
    public static double Contrast(double a, double b) => (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);

    internal static double ToLinear(double c) => c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);

    internal static double ToSrgb(double l) => l <= 0.0031308 ? l * 12.92 : (1.055 * Math.Pow(l, 1 / 2.4)) - 0.055;
}
