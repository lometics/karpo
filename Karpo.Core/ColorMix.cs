// A colour of one's own, mixed in a colour row's picker (docs\widgets.md §523; Karpo.Dock\Flyout.Mixer.cs draws it): the
// arithmetic of it, kept apart from the drawing so it is the same wherever a colour is chosen and is tested on its own.
//
//  - **Hue, saturation and value** (Hsv): what the spectrum square and the hue slider stand for — saturation across the
//    square, value up it, the hue along the slider, 0° to 360° from red round to red —, to and from a colour's red, green
//    and blue. A grey has no hue of its own: whoever mixes keeps the hue the slider stands at (Flyout.Mixer.cs), which a
//    round trip through red, green and blue would lose.
//  - **The spectrum and the sliders** (Spectrum, SpectrumPoint, Slider, SliderPoint): a point of the square, or along a
//    track, to what it stands for and back, a point past an edge held at it.
//  - **The keys** (Stepped): the arrows move the spectrum's point and the sliders a step at a time, Page Up and Page Down
//    ten, held at either end.
//  - **Hex** (Hex, ParseHex): "#RRGGBB", or "#RRGGBBAA" for a colour that is see-through where the value takes an alpha —
//    the order a web page writes it in, the alpha last. Typed: with or without the '#', either case, spaces round it; a
//    value that takes no alpha keeps none of what is typed.

using System.Globalization;

namespace Karpo.Core;

/// <summary>A colour as the spectrum and the hue slider have it: <paramref name="H"/> 0 to 360 (degrees), <paramref name="S"/> and <paramref name="V"/> 0 to 1.</summary>
public readonly record struct Hsv(double H, double S, double V);

/// <summary>The parts of a colour row's picker that are not the swatches (Flyout.Mixer.cs).</summary>
public enum MixPart
{
    /// <summary>The square: saturation across, value up.</summary>
    Spectrum,

    /// <summary>The hue, along a rainbow.</summary>
    Hue,

    /// <summary>The opacity, from none to the colour whole, over a checker.</summary>
    Opacity,
}

/// <summary>The arithmetic of a colour of one's own; see the top of the file.</summary>
public static class ColorMix
{
    /// <summary>The hue slider's and the opacity slider's ends: degrees round the colour wheel, percent.</summary>
    public const double MostHue = 360;

    public const double MostOpacity = 100;

    /// <summary>A step of the keys, and Page Up's and Page Down's: one, ten.</summary>
    public const double Step = 1;

    public const double PageStep = 10;

    /// <summary><paramref name="color"/>'s hue, saturation and value; a grey's hue 0.</summary>
    public static Hsv ToHsv(Argb color)
    {
        var (r, g, b) = (color.R / 255.0, color.G / 255.0, color.B / 255.0);
        var most = Math.Max(r, Math.Max(g, b));
        var least = Math.Min(r, Math.Min(g, b));
        var span = most - least;
        double hue = 0;
        if (span > 0)
        {
            hue = most == r ? 60 * (((g - b) / span) % 6)
                : most == g ? 60 * (((b - r) / span) + 2)
                : 60 * (((r - g) / span) + 4);
            if (hue < 0)
            {
                hue += 360;
            }
        }
        return new Hsv(hue, most == 0 ? 0 : span / most, most);
    }

    /// <summary><paramref name="hsv"/> as a colour of <paramref name="alpha"/>; a hue of 360 is red, as 0 is.</summary>
    public static Argb ToArgb(Hsv hsv, byte alpha = 255)
    {
        var hue = ((hsv.H % 360) + 360) % 360;
        var (s, v) = (Math.Clamp(hsv.S, 0, 1), Math.Clamp(hsv.V, 0, 1));
        var chroma = v * s;
        var x = chroma * (1 - Math.Abs(((hue / 60) % 2) - 1));
        var m = v - chroma;
        var (r, g, b) = (hue / 60) switch
        {
            < 1 => (chroma, x, 0.0),
            < 2 => (x, chroma, 0.0),
            < 3 => (0.0, chroma, x),
            < 4 => (0.0, x, chroma),
            < 5 => (x, 0.0, chroma),
            _ => (chroma, 0.0, x),
        };
        return new Argb(alpha, Byte(r + m), Byte(g + m), Byte(b + m));
    }

    /// <summary>The hue alone, whole and bright: the spectrum's colour at its top right corner.</summary>
    public static Argb Pure(double hue) => ToArgb(new Hsv(hue, 1, 1));

    /// <summary>
    /// The spectrum's point (<paramref name="x"/>, <paramref name="y"/>) of a square <paramref name="w"/> by <paramref name="h"/>
    /// at <paramref name="hue"/>: saturation from its left (none) to its right (whole), value from its bottom (black) to its top;
    /// a point past an edge held at it.
    /// </summary>
    public static Hsv Spectrum(double hue, double x, double y, double w, double h) =>
        new(hue, w > 0 ? Math.Clamp(x / w, 0, 1) : 0, h > 0 ? 1 - Math.Clamp(y / h, 0, 1) : 1);

    /// <summary>Where <paramref name="hsv"/> stands in a spectrum <paramref name="w"/> by <paramref name="h"/>, from its top left corner.</summary>
    public static (double X, double Y) SpectrumPoint(Hsv hsv, double w, double h) =>
        (Math.Clamp(hsv.S, 0, 1) * w, (1 - Math.Clamp(hsv.V, 0, 1)) * h);

    /// <summary>A track <paramref name="w"/> long's point <paramref name="x"/> from its start, as its value from 0 to <paramref name="most"/>; held at either end.</summary>
    public static double Slider(double x, double w, double most) => w > 0 ? Math.Clamp(x / w, 0, 1) * most : 0;

    /// <summary>Where <paramref name="value"/> (0 to <paramref name="most"/>) stands along a track <paramref name="w"/> long.</summary>
    public static double SliderPoint(double value, double w, double most) => most > 0 ? Math.Clamp(value / most, 0, 1) * w : 0;

    /// <summary><paramref name="value"/> stepped <paramref name="by"/>, held between 0 and <paramref name="most"/>.</summary>
    public static double Stepped(double value, double by, double most) => Math.Clamp(value + by, 0, most);

    /// <summary>
    /// <paramref name="color"/> as its row's hex box says it: "#RRGGBB", and "#RRGGBBAA" where it is see-through and the value
    /// takes an <paramref name="alpha"/>.
    /// </summary>
    public static string Hex(Argb color, bool alpha) =>
        alpha && color.A < 255
            ? string.Create(CultureInfo.InvariantCulture, $"#{color.R:X2}{color.G:X2}{color.B:X2}{color.A:X2}")
            : string.Create(CultureInfo.InvariantCulture, $"#{color.R:X2}{color.G:X2}{color.B:X2}");

    /// <summary>
    /// <paramref name="text"/> typed in a hex box: "#RRGGBB" or "#RRGGBBAA", the '#' and the case as typed, spaces round it.
    /// A value that takes no <paramref name="alpha"/> is opaque whatever was typed. Null for anything else.
    /// </summary>
    public static Argb? ParseHex(string? text, bool alpha)
    {
        var digits = text?.Trim() ?? "";
        if (digits.StartsWith('#'))
        {
            digits = digits[1..];
        }
        if (digits.Length is not (6 or 8) || !digits.All(char.IsAsciiHexDigit)
            || !uint.TryParse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value))
        {
            return null;
        }
        var rgb = digits.Length == 8 ? value >> 8 : value;
        var a = digits.Length == 8 && alpha ? (byte)value : (byte)255;
        return new Argb(a, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
    }

    /// <summary>How opaque <paramref name="color"/> is, in percent, as the opacity slider says it.</summary>
    public static double Opacity(Argb color) => Math.Round(color.A * MostOpacity / 255.0, 1);

    /// <summary><paramref name="percent"/> opaque as an alpha.</summary>
    public static byte Alpha(double percent) => Byte(Math.Clamp(percent, 0, MostOpacity) / MostOpacity);

    private static byte Byte(double share) => (byte)Math.Round(Math.Clamp(share, 0, 1) * 255, MidpointRounding.AwayFromZero);
}
