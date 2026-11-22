// Patterns (the person, 2026-10-31: "you should be able to set a background pattern for the folder widgets and for the
// taskbar. make up some default patterns. the pattern is part of the styles settings and should be continuous between
// widgets inside the same group but not between groups of widgets"; docs\widgets.md §462).
//
// A pattern is a value of a look (§507; LookPart: the Taskbar, the Taskbar widgets — a folder's faces among them, §499 — and the Widgets
// in folders; PartPaints.Pattern), drawn over the part's background in the part's own ink (§513: its Pattern color as said, where one is), else its text's colour at the
// pattern's low strength (ToneRules.Dress), so it reads in every style, light and dark, and the text is made readable over
// it as over the plain background. Each is a tile that repeats seamlessly, laid out here as coverage per pixel at the
// display's scale — each feature on whole pixels or drawn round by its distance, so a 1-DIP line is one crisp pixel at
// 100 %, at 150 % and at 225 % alike — and drawn by the dock as a sheet: the bar one sheet; a group of widgets on the bar,
// and a section of a folder, one sheet each, every card of it a window onto it.

using System.Text.Json.Serialization;

namespace Karpo.Core;

/// <summary>§462: the patterns a part's background may wear, in the order its choice lists them; None its default.</summary>
[JsonConverter(typeof(TolerantEnumConverter<Pattern>))]
public enum Pattern
{
    [JsonStringEnumMemberName("none")] None,
    [JsonStringEnumMemberName("dots")] Dots,
    [JsonStringEnumMemberName("grid")] Grid,
    [JsonStringEnumMemberName("lines")] Lines,
    [JsonStringEnumMemberName("crosshatch")] Crosshatch,
    [JsonStringEnumMemberName("waves")] Waves,
    [JsonStringEnumMemberName("checks")] Checks,
    [JsonStringEnumMemberName("contours")] Contours,
    [JsonStringEnumMemberName("grain")] Grain,
}

/// <summary>§462: a pattern as it is drawn: which, and its ink, the pattern's strength in its alpha.</summary>
public readonly record struct Patterned(Pattern Pattern, Argb Ink);

/// <summary>
/// §462: a pattern's tile at one scale: <see cref="Width"/> × <see cref="Height"/> px of coverage (0 to 255), repeating
/// seamlessly in both directions.
/// </summary>
public sealed class PatternTile(int width, int height, byte[] coverage)
{
    public int Width { get; } = width;

    public int Height { get; } = height;

    /// <summary>Row by row, a byte a pixel: how much of the ink the pixel takes.</summary>
    public byte[] Coverage { get; } = coverage;

    /// <summary>The coverage at (<paramref name="x"/>, <paramref name="y"/>) of the endless sheet the tile repeats into.</summary>
    public byte At(int x, int y) => Coverage[(Patterns.Wrap(y, Height) * Width) + Patterns.Wrap(x, Width)];
}

/// <summary>§462: the patterns, what each is called, how strong its ink is and its tile; see the top of the file.</summary>
public static class Patterns
{
    /// <summary>Every pattern but None, in the order the choice lists them.</summary>
    public static readonly Pattern[] All = [Pattern.Dots, Pattern.Grid, Pattern.Lines, Pattern.Crosshatch, Pattern.Waves, Pattern.Checks, Pattern.Contours, Pattern.Grain];

    /// <summary>What a pattern is called: "None", "Dots", "Grid", …</summary>
    public static string Name(Pattern pattern) => pattern switch
    {
        Pattern.Dots => S.SettingsAppearanceStylesPatternDots,
        Pattern.Grid => S.SettingsAppearanceStylesPatternGrid,
        Pattern.Lines => S.SettingsAppearanceStylesPatternLines,
        Pattern.Crosshatch => S.SettingsAppearanceStylesPatternCrosshatch,
        Pattern.Waves => S.SettingsAppearanceStylesPatternWaves,
        Pattern.Checks => S.SettingsAppearanceStylesPatternChecks,
        Pattern.Contours => S.SettingsAppearanceStylesPatternContours,
        Pattern.Grain => S.SettingsAppearanceStylesPatternGrain,
        _ => S.SettingsAppearanceSeparatorStyleNone,
    };

    /// <summary>
    /// How much of its ink a pattern's fully covered pixel takes (0 to <see cref="ToneRules.PatternMost"/>): the sparser the
    /// pattern, the stronger, so each reads about as quietly as the others. 0 for None.
    /// </summary>
    public static double Strength(Pattern pattern) => pattern switch
    {
        Pattern.Dots => 0.16,
        Pattern.Grid => 0.08,
        Pattern.Lines => 0.10,
        Pattern.Crosshatch => 0.08,
        Pattern.Waves => 0.11,
        Pattern.Checks => 0.045,
        Pattern.Contours => 0.12,
        Pattern.Grain => 0.08,
        _ => 0,
    };

    /// <summary><paramref name="value"/> into 0 to <paramref name="period"/> − 1, negatives too.</summary>
    public static int Wrap(int value, int period) => ((value % period) + period) % period;

    /// <summary>A length of <paramref name="dip"/> DIPs at <paramref name="scale"/> in whole px, at least <paramref name="least"/>.</summary>
    private static int Px(double dip, float scale, int least = 2) => Math.Max(least, (int)Math.Round(dip * scale, MidpointRounding.AwayFromZero));

    /// <summary>A line's half thickness at <paramref name="scale"/>: half a px at 100 %, and on with the scale.</summary>
    private static double Half(float scale) => Math.Max(0.5, 0.45 * scale);

    /// <summary>How much of a pixel a line or a dot <paramref name="half"/> px from its middle to its edge covers, its centre <paramref name="distance"/> px off.</summary>
    private static double Cover(double distance, double half) => Math.Clamp(half + 0.5 - distance, 0, 1);

    /// <summary><paramref name="value"/> folded to the nearest multiple of <paramref name="period"/>: how far it is from it.</summary>
    private static double Off(double value, double period)
    {
        var rest = value - (Math.Floor(value / period) * period);
        return Math.Min(rest, period - rest);
    }

    /// <summary>The tile's size: the pattern's period across and down, in px, at <paramref name="scale"/>.</summary>
    public static (int Width, int Height) Period(Pattern pattern, float scale) => pattern switch
    {
        Pattern.Dots => (Px(8, scale), 2 * Px(8, scale)),
        Pattern.Grid => (Px(12, scale), Px(12, scale)),
        Pattern.Lines => (Px(7, scale), Px(7, scale)),
        Pattern.Crosshatch => (Px(9, scale), Px(9, scale)),
        Pattern.Waves => (Px(28, scale), Px(9, scale)),
        Pattern.Checks => (2 * Px(4, scale), 2 * Px(4, scale)),
        Pattern.Contours => (Px(120, scale), Px(120, scale)),
        Pattern.Grain => (64 * GrainCell(scale), 64 * GrainCell(scale)),
        _ => (1, 1),
    };

    /// <summary>
    /// How much of the ink pixel (<paramref name="x"/>, <paramref name="y"/>) of the endless sheet takes, 0 to 1: the pattern
    /// read at the pixel's centre, the same at every period across and down (<see cref="Period"/>).
    /// </summary>
    public static double Coverage(Pattern pattern, float scale, int x, int y)
    {
        var (width, height) = Period(pattern, scale);
        // Every pattern is periodic by its own sums; folding the pixel into the tile first keeps the sums small.
        (x, y) = (Wrap(x, width), Wrap(y, height));
        var (cx, cy) = (x + 0.5, y + 0.5);
        switch (pattern)
        {
            case Pattern.Dots:
            {
                // Rows of round dots a period apart, every other row half a period along: centred on a pixel's middle.
                var period = width;
                var row = (int)Math.Floor(y / (double)period);
                var along = (period / 2) + 0.5 + (row % 2 == 1 ? period / 2 : 0);
                var down = (row * period) + (period / 2) + 0.5;
                var (dx, dy) = (Off(cx - along, period), cy - down);
                return Cover(Math.Sqrt((dx * dx) + (dy * dy)), Math.Max(0.75, 0.95 * scale));
            }
            case Pattern.Grid:
            {
                // Hairlines on whole pixels: a px at 100 % and 150 %, two at 225 %.
                var thick = Math.Max(1, (int)Math.Round(0.75 * scale, MidpointRounding.AwayFromZero));
                return x < thick || y < thick ? 1 : 0;
            }
            case Pattern.Lines:
                // Rising lines, 45°, a period apart across.
                return Cover(Off(cx + cy, width) / Math.Sqrt(2), Half(scale));
            case Pattern.Crosshatch:
                return Math.Max(Cover(Off(cx + cy, width) / Math.Sqrt(2), Half(scale)), Cover(Off(cx - cy, width) / Math.Sqrt(2), Half(scale)));
            case Pattern.Waves:
            {
                // Gentle waves a period apart down, their distance read across the slope.
                var amplitude = 1.75 * scale;
                var turn = 2 * Math.PI * cx / width;
                var slope = amplitude * 2 * Math.PI / width * Math.Cos(turn);
                return Cover(Off(cy - (amplitude * Math.Sin(turn)), height) / Math.Sqrt(1 + (slope * slope)), Half(scale));
            }
            case Pattern.Checks:
            {
                var side = width / 2;
                return ((x / side) + (y / side)) % 2 == 0 ? 1 : 0;
            }
            case Pattern.Contours:
            {
                // A land of hills, every wave of it a whole number of times across the tile, cut at even heights.
                double value = 0, dx = 0, dy = 0;
                foreach (var (across, down, size, start) in Hills)
                {
                    var turn = (2 * Math.PI * ((across * cx / width) + (down * cy / height))) + start;
                    value += size * Math.Sin(turn);
                    dx += size * Math.Cos(turn) * 2 * Math.PI * across / width;
                    dy += size * Math.Cos(turn) * 2 * Math.PI * down / height;
                }
                var steep = Math.Max(Math.Sqrt((dx * dx) + (dy * dy)), 1e-3);
                return Cover(Off(value, ContourStep) / steep, Half(scale));
            }
            case Pattern.Grain:
            {
                // A paper's grain: every px (two at 225 %) a little more of the ink or less, as it falls.
                var cell = width / 64;
                return Noise(x / cell, y / cell);
            }
            default:
                return 0;
        }
    }

    /// <summary>The grain's px: one up to 150 %, two at 200 % and over, so it stays a grain and not a speckle.</summary>
    private static int GrainCell(float scale) => Math.Max(1, (int)Math.Floor(scale + 0.25));

    /// <summary>The contours' land: each wave's turns across and down the tile, its height and where it starts.</summary>
    private static readonly (int Across, int Down, double Size, double Start)[] Hills =
        [(1, 0, 1.0, 0.3), (0, 1, 0.9, 1.7), (1, 1, 0.6, 2.9), (1, -1, 0.5, 0.8), (2, 1, 0.35, 4.1), (1, 2, 0.3, 5.3)];

    /// <summary>How far apart the contours' heights are.</summary>
    private const double ContourStep = 0.85;

    /// <summary>A number 0 to 1 for cell (<paramref name="x"/>, <paramref name="y"/>), the same every time: the grain's.</summary>
    private static double Noise(int x, int y)
    {
        var hash = unchecked((uint)((x * 374761393) + (y * 668265263)));
        hash = unchecked((hash ^ (hash >> 13)) * 1274126177u);
        hash ^= hash >> 16;
        return (hash & 0xFFFFFF) / (double)0x1000000;
    }

    private static readonly Dictionary<(Pattern, float), PatternTile> tiles = [];

    /// <summary>The tile of <paramref name="pattern"/> at <paramref name="scale"/>: one period across and down; made once.</summary>
    public static PatternTile Tile(Pattern pattern, float scale)
    {
        lock (tiles)
        {
            if (tiles.TryGetValue((pattern, scale), out var made))
            {
                return made;
            }
        }
        var (width, height) = Period(pattern, scale);
        var coverage = new byte[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                coverage[(y * width) + x] = (byte)Math.Round(Coverage(pattern, scale, x, y) * 255, MidpointRounding.AwayFromZero);
            }
        }
        var tile = new PatternTile(width, height, coverage);
        lock (tiles)
        {
            tiles[(pattern, scale)] = tile;
        }
        return tile;
    }
}
