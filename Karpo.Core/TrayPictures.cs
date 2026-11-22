// Tray icon pictures at the size the bar draws them, and how long an icon its app took away is kept.
//
// Size. An app hands the tray one bitmap, not a group to pick a size from, and most hand over their
// small icon for the display's DPI: 24 px at 150 %, where the bar draws an app icon's 36. Drawn
// bigger with DrawIconEx, GDI copied pixels (no filtering): a blocky, "very low resolution" icon.
// The picture is taken at the size its app made it and scaled here with a cubic filter instead, which
// is what Direct2D does for every other picture on the bar (Gfx.DrawPixels). The dock looks for a
// sharper source first (Tray.Render: the app's own icon group; the executable's icon where the app's
// picture is that icon), and only scales what has none.
//
// Grace. An app that takes its icon away and adds it again at once (WhatsApp's H.NotifyIcon deletes,
// adds it hidden and shows it again whenever a taskbar asks for icons, TaskbarCreated) made the bar
// lose the icon for a moment and get it back: it flickered. An icon taken away, hidden by its app or
// left behind by a window that went is kept where it stands for a moment first (Grace).

namespace Karpo.Core;

/// <summary>Square premultiplied BGRA pictures, as the tray captures them; pure.</summary>
public static class TrayPictures
{
    /// <summary>
    /// How long, in ms, an icon its app took away, hid or left behind is kept where it stands before
    /// it goes: long enough for a delete and an add that follow each other, short enough that one
    /// that really went is gone at once to the eye.
    /// </summary>
    public const long GraceMs = 1500;

    /// <summary>Whether an icon gone since <paramref name="since"/> (ms) is still kept at <paramref name="now"/>.</summary>
    public static bool Kept(long since, long now) => now - since < GraceMs;

    /// <summary>The ms until an icon gone since <paramref name="since"/> stops being kept; 0 when it has.</summary>
    public static long Left(long since, long now) => Math.Max(0, GraceMs - (now - since));

    /// <summary>
    /// <paramref name="data"/>, a <paramref name="from"/>-px square of premultiplied BGRA, as a
    /// <paramref name="to"/>-px square: a cubic filter (Catmull-Rom) both ways, widened when it
    /// shrinks so every source pixel counts. A picture's edge is its own last pixel, not transparency,
    /// so an opaque icon keeps a hard edge. The same size comes back as a copy.
    /// </summary>
    public static byte[] Resize(ReadOnlySpan<byte> data, int from, int to)
    {
        if (from <= 0 || to <= 0 || data.Length < from * from * 4)
        {
            return new byte[Math.Max(0, to) * Math.Max(0, to) * 4];
        }
        if (from == to)
        {
            return data[..(from * from * 4)].ToArray();
        }
        var taps = Taps(from, to);
        // Along x first, into floats, then along y.
        var across = new float[to * from * 4];
        for (var y = 0; y < from; y++)
        {
            for (var x = 0; x < to; x++)
            {
                var (first, weights) = taps[x];
                for (var channel = 0; channel < 4; channel++)
                {
                    var sum = 0f;
                    for (var k = 0; k < weights.Length; k++)
                    {
                        var source = Math.Clamp(first + k, 0, from - 1);
                        sum += weights[k] * data[(y * from + source) * 4 + channel];
                    }
                    across[(y * to + x) * 4 + channel] = sum;
                }
            }
        }
        var result = new byte[to * to * 4];
        Span<float> pixel = stackalloc float[4];
        for (var y = 0; y < to; y++)
        {
            var (first, weights) = taps[y];
            for (var x = 0; x < to; x++)
            {
                for (var channel = 0; channel < 4; channel++)
                {
                    var sum = 0f;
                    for (var k = 0; k < weights.Length; k++)
                    {
                        var source = Math.Clamp(first + k, 0, from - 1);
                        sum += weights[k] * across[(source * to + x) * 4 + channel];
                    }
                    pixel[channel] = sum;
                }
                // The filter overshoots at a hard edge: alpha within 0..255, and no colour above its
                // alpha, which premultiplied colour never is.
                var alpha = (byte)Math.Clamp(MathF.Round(pixel[3]), 0f, 255f);
                var at = (y * to + x) * 4;
                result[at + 3] = alpha;
                for (var channel = 0; channel < 3; channel++)
                {
                    result[at + channel] = (byte)Math.Clamp(MathF.Round(pixel[channel]), 0f, alpha);
                }
            }
        }
        return result;
    }

    /// <summary>
    /// Whether two pictures of one size show the same thing: on average no channel of any pixel more
    /// than a few steps of 255 apart, which two renderings of one icon are and an icon with a badge,
    /// another state or another picture altogether is not. Different sizes are never alike.
    /// </summary>
    public static bool Alike(ReadOnlySpan<byte> one, ReadOnlySpan<byte> two)
    {
        if (one.Length != two.Length || one.Length == 0)
        {
            return false;
        }
        long apart = 0;
        for (var i = 0; i < one.Length; i++)
        {
            apart += Math.Abs(one[i] - two[i]);
        }
        return apart <= one.Length * 6L;
    }

    /// <summary>Per output pixel: the first source pixel it reads and the weight of each from there.</summary>
    private static (int First, float[] Weights)[] Taps(int from, int to)
    {
        var scale = (float)from / to;
        // Shrinking widens the filter to the source pixels each output pixel covers.
        var widen = MathF.Max(1f, scale);
        var reach = 2f * widen;
        var taps = new (int, float[])[to];
        for (var at = 0; at < to; at++)
        {
            var centre = (at + 0.5f) * scale;
            var first = (int)MathF.Floor(centre - reach);
            var last = (int)MathF.Ceiling(centre + reach);
            var weights = new float[last - first + 1];
            var total = 0f;
            for (var k = 0; k < weights.Length; k++)
            {
                weights[k] = CatmullRom((first + k + 0.5f - centre) / widen);
                total += weights[k];
            }
            if (total != 0f)
            {
                for (var k = 0; k < weights.Length; k++)
                {
                    weights[k] /= total;
                }
            }
            taps[at] = (first, weights);
        }
        return taps;
    }

    private static float CatmullRom(float x)
    {
        x = MathF.Abs(x);
        return x < 1f ? (1.5f * x - 2.5f) * x * x + 1f
            : x < 2f ? ((-0.5f * x + 2.5f) * x - 4f) * x + 2f
            : 0f;
    }
}
