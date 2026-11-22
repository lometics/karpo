// Colours (the person, 2026-10-09: "in appearance you should be able to customize individual colors"; 2026-10-31: "an
// opacity selector … integrated into the color selector and … an option to have None"; docs\widgets.md §405, §459).
//
// A style says each part's colours (Styles: a part's Background, Outline, Text and Accent, each Windows' own, a colour
// with its opacity, or None). Every other colour Karpo draws — the quieter texts, the highlight under the pointer, a
// separator's line — is derived from a part's own by fixed rules (ToneRules.Dress), so it always matches: from the text,
// at Windows' own steps. A text and an accent are made readable on what they stand on (4.5:1, 3:1: ToneRules.Readable).
// No colour is adapted for the other mode: a style with different colours in dark mode says them (Styles).
//
// High contrast wins over all of them: Windows asks every app to draw its system colours then, and Karpo does
// (Karpo.Dock's Look carries no palette in high contrast).
//
// A value the file has that does not read — a typo, a word from a newer build — counts as Default and is left
// in the file as it is, so nothing is lost behind the person's back.

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Karpo.Core;

/// <summary>§405, §459: the colours of a style's part, in the order its editor lists them.</summary>
public enum Tone
{
    /// <summary>What the part stands on: the bar, a panel, a card.</summary>
    Background,

    /// <summary>The line round it.</summary>
    Outline,

    /// <summary>Its text and glyphs; the quieter ones, the highlight and the separators are it, fainter.</summary>
    Text,

    /// <summary>What is on or chosen: a switch, a slider, the focused app's pill; Windows' accent for Windows' own.</summary>
    Accent,

    /// <summary>
    /// §513: the ink its pattern (§462) is drawn in, as said, its opacity the pattern's; Windows' own (unset) its text at the
    /// pattern's strength (<see cref="ToneRules.PatternOf"/>). A part has it where it has a pattern.
    /// </summary>
    PatternColor,

    /// <summary>
    /// §623: the Flyouts' alone: what lies over every display, behind the folder open and Karpo's bars, while a folder is open
    /// at rest — to darken the rest of the screen, say. Unset or None: nothing there, the screen as it is (Karpo's own).
    /// </summary>
    Backdrop,

    /// <summary>
    /// §649: the Flyouts' too: the area an attached folder (§647, FolderStyle.Attached) stands on, joined to its bar. Unset:
    /// the bar's own colour, solid — it follows the bar (Karpo's own). Never None: an attached folder's lines have no card.
    /// </summary>
    Ground,
}

/// <summary>§405, §459: the tones, in their order, and what each is called.</summary>
public static class ToneNames
{
    /// <summary>A card's tones, in the order a menu lists them.</summary>
    public static readonly Tone[] All = [Tone.Background, Tone.Outline, Tone.Text, Tone.Accent, Tone.PatternColor];

    /// <summary>§623: every tone any section has: a card's, and the Flyouts' backdrop; §649 and their attached folders' ground.</summary>
    public static readonly Tone[] Every = [.. All, Tone.Backdrop, Tone.Ground];

    /// <summary>
    /// §623: the opacity a backdrop picked from none starts at: half, as Windows dims behind what wants the attention (edit
    /// mode's dim too); any other colour opaque. A colour picked over one in force keeps that one's.
    /// </summary>
    public const byte BackdropAlpha = 0x80;

    /// <summary>What a tone is called: "Background", "Outline", "Text color", "Accent color", "Pattern color".</summary>
    public static string Name(Tone tone) => tone switch
    {
        Tone.Background => S.SettingsAppearanceCustomBackgroundHeader,
        Tone.Outline => S.SettingsAppearanceStylesOutline,
        Tone.Text => S.SettingsAppearanceCustomTextHeader,
        Tone.PatternColor => S.SettingsLookPatternColor,
        Tone.Backdrop => S.SettingsLookBackdrop,
        Tone.Ground => S.SettingsLookGround,
        _ => S.SettingsAppearanceCustomAccentHeader,
    };

    /// <summary>Whether a tone may be None: a background, an outline or §623 a backdrop, not there at all; a text and an accent always are.</summary>
    public static bool MayBeNone(Tone tone) => tone is Tone.Background or Tone.Outline or Tone.Backdrop;

    /// <summary>What the file calls a tone: "background".</summary>
    public static string JsonName(Tone tone) => tone.ToString() is var name ? char.ToLowerInvariant(name[0]) + name[1..] : "";
}

/// <summary>A colour with its alpha: "#AARRGGBB", or "#RRGGBB" for an opaque one.</summary>
public readonly record struct Argb(byte A, byte R, byte G, byte B)
{
    /// <summary>"#AARRGGBB", upper case.</summary>
    public override string ToString() => $"#{A:X2}{R:X2}{G:X2}{B:X2}";

    /// <summary>"#RRGGBB", upper case, as the file keeps a colour picked: opaque, the alpha its surface's.</summary>
    public string Rgb => $"#{R:X2}{G:X2}{B:X2}";

    /// <summary>The colour at <paramref name="alpha"/> (0 to 1).</summary>
    public Argb At(double alpha) => this with { A = (byte)Math.Round(Math.Clamp(alpha, 0, 1) * 255, MidpointRounding.AwayFromZero) };

    /// <summary>
    /// <paramref name="text"/> read as "#AARRGGBB" or "#RRGGBB" (opaque), with or without the '#'
    /// and in either case; null for anything else.
    /// </summary>
    public static Argb? Parse(string? text)
    {
        var digits = text?.Trim();
        if (string.IsNullOrEmpty(digits))
        {
            return null;
        }
        if (digits[0] == '#')
        {
            digits = digits[1..];
        }
        if (digits.Length is not (6 or 8)
            || !uint.TryParse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value))
        {
            return null;
        }
        if (digits.Length == 6)
        {
            value |= 0xFF000000u;
        }
        return new Argb((byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value);
    }
}

/// <summary>
/// §405, §459: every colour Karpo draws on one surface in one mode, derived from a style's parts (<see cref="ToneRules.Dress"/>),
/// each null where Windows' own is drawn: the bar (or the panel a folder's look stands on) and its outline, the cards and
/// their outline, the panels (menus, flyouts, folders) and their outline, the highlight, the text and
/// the accent on the cards, and the text on the panels; §462: the patterns on the bar and on the cards, each in its ink,
/// null for none. A colour with no alpha is None: not there. Internal to the painters: nobody picks these. Equal when every
/// colour is, so a look that carries it compares by what it draws.
/// </summary>
public readonly record struct Palette(
    Argb? Taskbar = null,
    Argb? Flyout = null,
    Argb? Card = null,
    Argb? CardOutline = null,
    Argb? FlyoutOutline = null,
    Argb? Highlight = null,
    Argb? Text = null,
    Argb? Accent = null,
    Argb? TaskbarOutline = null,
    Argb? PanelText = null,
    Patterned? TaskbarPattern = null,
    Patterned? CardPattern = null)
{
    /// <summary>Windows' own everywhere.</summary>
    public bool IsAutomatic => this == default;
}

/// <summary>
/// §459, §464: what a surface is dressed in, a part each: the ground (the bar; for a folder nothing but its outline), the
/// plates (its widgets' cards, a folder's face, a folder's lines), the panels opened from it (menus, flyouts: their
/// background and their outline) and what stands in those (their rows). A plate with a background of its own stands on
/// the desktop, not on the ground (§464: no surface over another); one of Windows' own has no card and is the ground's.
/// </summary>
public readonly record struct Dressing(PartPaints Ground = default, PartPaints Plate = default, PartPaints Panel = default, PartPaints InPanel = default);

/// <summary>
/// §405, §459: the fixed tonal rules that dress a surface from a style's parts, in the mode drawn in. See the top of the
/// file; docs\widgets.md §459 has the table.
/// </summary>
public static class ToneRules
{
    /// <summary>Text against what it stands on, at least: WCAG AA for body text.</summary>
    public const double TextContrast = 4.5;

    /// <summary>The accent against what it stands on, at least: WCAG AA for what is not text (a switch, a pill).</summary>
    public const double AccentContrast = 3;

    // The highlight: the text at Windows' own step (SubtleFill).
    internal const double HighlightDark = 0.10, HighlightLight = 0.07;

    /// <summary>The least alpha a plate is read as standing on its own at, rather than as a tint of what is under it.</summary>
    private const byte Solid = 128;

    /// <summary>What Windows' bar is, opaque, where nobody picks a background (Karpo.Dock's Scene.Automatic's tint).</summary>
    public static Argb WindowsBackground(bool dark) => dark ? new(255, 0x20, 0x20, 0x20) : new(255, 0xF3, 0xF3, 0xF3);

    /// <summary>What Windows' panels are, opaque (FlyoutTheme's surface).</summary>
    public static Argb WindowsFlyout(bool dark) => dark ? new(255, 0x2C, 0x2C, 0x2C) : new(255, 0xF9, 0xF9, 0xF9);

    /// <summary>What Windows' text is, opaque: white, and its 89 % black on its light surfaces.</summary>
    public static Argb WindowsText(bool dark) => dark ? new(255, 0xFF, 0xFF, 0xFF) : new(255, 0x1B, 0x1B, 0x1B);

    /// <summary>A colour as it is seen over <paramref name="under"/>: blended; <paramref name="under"/> for none, and for None.</summary>
    public static Argb Seen(Argb? color, Argb under) => color is { } over ? Blend(over, under) : under;

    /// <summary>Whether <paramref name="color"/> reads as dark: below mid grey as the eye has it.</summary>
    public static bool IsDark(Argb color) => Luminance(color) < MidGrey;

    /// <summary>
    /// Every colour Karpo draws on a surface dressed in <paramref name="dressing"/> while Windows is in the mode
    /// <paramref name="windowsDark"/>, each null where Windows' own is drawn, and the mode the surface is drawn in — dark
    /// where its ground is a colour half opaque or more that reads dark of itself, light where it reads light (§546: the
    /// same in both of Windows' modes), else Windows':
    /// <list type="bullet">
    /// <item>The bar, the cards, the panels and their outlines: the colours as said, with their opacity; None a colour of no
    /// alpha (no plate, no line); Windows' own null.</item>
    /// <item>Text: the plate's text, else Windows' own of the mode the plate reads in, made 4.5:1 on what it stands on —
    /// its plate where that is solid, else the plate and what is under it both (<see cref="Readable"/>): §464 the desktop
    /// for a plate with a background of its own (Windows' own surface of the mode in its place), the ground for one of
    /// Windows' own —, its opacity kept; Windows' own where nothing on it is coloured. The accent the same at 3:1. The
    /// highlight: the text at Windows' own step. The panels' text: what stands in them, on their
    /// background laid over Windows' own flyout (§464: a menu is never less than Windows' flyout, so it reads over
    /// anything).</item>
    /// </list>
    /// </summary>
    public static (Palette Palette, bool Dark) Dress(Dressing dressing, bool windowsDark)
    {
        var ground = Of(dressing.Ground.Background);
        // §546: a ground half opaque or more reads as its own colour, the same in both of Windows' modes — not as laid over
        // Windows' surface of the mode, which made a see-through one (an orange at 50 %) dark in the one and light in the other.
        var dark = ground is { A: >= Solid } solid ? IsDark(solid with { A = 255 }) : windowsDark;
        // §464: the desktop, which is under every surface: Windows' own surface of the mode Windows is in, in its place.
        var desktop = WindowsBackground(windowsDark);
        var seen = Seen(ground, WindowsBackground(dark));
        // §462: the bar's pattern in the ink of the text on it, where that shows on it, else Windows' own text's; §513 in its
        // own colour where one is said.
        var groundInk = dressing.Plate.Text.Color is { } said && Contrast(said with { A = 255 }, seen) >= AccentContrast ? said : WindowsText(dark);
        var groundPattern = PatternOf(dressing.Ground, groundInk);
        var (card, text, accent, cardPattern, barPattern) = OnPlate(dressing.Plate, ground is not null, seen, desktop, groundPattern, dressing.Ground.PatternColor.Color is not null);
        // §464: a menu's background laid over Windows' own flyout of the mode the surface is drawn in, as FlyoutTheme lays it.
        var flyout = Of(dressing.Panel.Background);
        var menu = Seen(flyout, WindowsFlyout(dark));
        var (_, panelText, _, _, _) = OnPlate(dressing.InPanel with { Background = default, Pattern = Pattern.None }, flyout is not null, menu, menu, null);
        return (new Palette(
            Taskbar: ground,
            Flyout: flyout,
            Card: card,
            CardOutline: Of(dressing.Plate.Outline) is { A: > 0 } rim ? rim : null,
            FlyoutOutline: Of(dressing.Panel.Outline),
            Highlight: text is { } lit ? HighlightOf(lit) : null,
            Text: text,
            Accent: accent,
            TaskbarOutline: Of(dressing.Ground.Outline),
            PanelText: panelText,
            TaskbarPattern: barPattern,
            CardPattern: cardPattern), dark);
    }

    /// <summary>
    /// What a plate of <paramref name="part"/>'s colours draws, standing on a ground seen as <paramref name="ground"/> where
    /// its background is Windows' own, else (§464) straight on the desktop, seen as <paramref name="desktop"/>: its card (none
    /// for None and Windows' own), its text and its accent, readable on what they stand on; the text Windows' own (null)
    /// where neither the ground (<paramref name="groundColoured"/>), the plate nor the text is coloured. §462: and its pattern,
    /// in the ink of its text as the part says it (else Windows' own), and the ground's (<paramref name="groundPattern"/>) as
    /// it is drawn — under the plate only where it stands on the ground —: the text and the accent made readable on every
    /// surface with the patterns on it too, and a pattern made fainter where that cannot be, so a text on a pattern is never
    /// less readable than on the same surface bare. §513: a pattern in a colour said (the plate's, the ground's:
    /// <paramref name="groundSaid"/>) is drawn as said, never fainter: the person's colour, as a background is.
    /// </summary>
    private static (Argb? Card, Argb? Text, Argb? Accent, Patterned? Pattern, Patterned? Ground) OnPlate(PartPaints part, bool groundColoured, Argb ground, Argb desktop, Patterned? groundPattern, bool groundSaid = false)
    {
        var own = Of(part.Background);
        var under = own is null ? ground : desktop;
        var card = own is { A: > 0 } plate ? plate : (Argb?)null;
        var seen = Seen(card, under);
        // §464: None a card all the same, of no alpha: a plate of its own over the desktop, nothing of the ground under it.
        card ??= own;
        // §464, §462: the bar's pattern is under a plate only where no card of its own is.
        var barPattern = groundPattern;
        groundPattern = own is null ? groundPattern : null;
        var dark = IsDark(seen);
        var coloured = groundColoured || own is not null || part.Text.Color is not null;
        var ink = part.Text.Color ?? WindowsText(dark);
        var pattern = PatternOf(part, ink);
        // What a text stands on over a ground: a card of its own (half opaque or more), a fainter one with its ground too.
        Argb[] On(Argb ground) => card is { A: >= Solid } solid ? [Seen(solid, ground)] : card is { } faint ? [ground, Seen(faint, ground)] : [ground];
        Argb[] Lined(Patterned? lined, Argb[] surfaces) => lined is { } drawn ? [.. surfaces.Select(surface => Blend(drawn.Ink, surface))] : [];
        Argb[] Grounds(Patterned? lined) => [.. On(under), .. lined is { } drawn ? On(Blend(drawn.Ink, under)) : []];
        var bare = Grounds(groundPattern);
        Argb[] surfaces = [.. bare, .. Lined(pattern, bare)];
        Argb? text = coloured ? Readable(ink, surfaces, dark, TextContrast) with { A = ink.A } : null;
        Argb? accent = part.Accent.Color is { } picked ? Readable(picked, surfaces, dark, AccentContrast) with { A = picked.A } : null;
        // A pattern never takes a text under what it reads at on the same surface bare (4.5:1, else as it is): where no
        // text reads as well on it (a mid-tone card), it gives way, fainter, else none — the ground's first, then the card's.
        foreach (var (over, ratio) in (ReadOnlySpan<(Argb?, double)>)[(text, TextContrast), (accent, AccentContrast)])
        {
            if (over is not { } drawn)
            {
                continue;
            }
            drawn = drawn with { A = 255 };
            bool Hurts(Argb[] bareOnes, Argb[] lined) => bareOnes.Zip(lined).Any(pair => Contrast(drawn, pair.Second) < Math.Min(ratio, Contrast(drawn, pair.First)) - 1e-9);
            while (!groundSaid && groundPattern is { } lined && Hurts(On(under), On(Blend(lined.Ink, under))))
            {
                groundPattern = Fainter(lined);
            }
            bare = Grounds(groundPattern);
            while (part.PatternColor.Color is null && pattern is { } lined && Hurts(bare, Lined(lined, bare)))
            {
                pattern = Fainter(lined);
            }
        }
        return (card, text, accent, pattern, own is null ? groundPattern : barPattern);
    }

    /// <summary>§462: a pattern at half its ink; none where that is next to nothing.</summary>
    private static Patterned? Fainter(Patterned pattern) => pattern.Ink.A <= 2 ? null : pattern with { Ink = pattern.Ink with { A = (byte)(pattern.Ink.A / 2) } };

    /// <summary>
    /// §462: the most of its ink a pattern's fully covered pixel takes: a text's colour at no more than this over a card leaves
    /// the card where it is to the eye, and a text made readable on the card still is where the pattern runs under it
    /// (<see cref="OnPlate"/> makes sure).
    /// </summary>
    public const double PatternMost = 0.16;

    /// <summary>
    /// §462: <paramref name="pattern"/> in <paramref name="ink"/>, at its strength (<see cref="Patterns.Strength"/>, never over
    /// <see cref="PatternMost"/>), a light ink (on a dark card) at <see cref="PatternOnDark"/> of it, which the eye reads as alike;
    /// null for None.
    /// </summary>
    public static Patterned? PatternOf(Pattern pattern, Argb ink) =>
        pattern == Pattern.None || !Enum.IsDefined(pattern) ? null
        : new Patterned(pattern, ink.At(ink.A / 255.0 * Math.Min(Patterns.Strength(pattern), PatternMost) * (IsDark(ink) ? 1 : PatternOnDark)));

    /// <summary>
    /// §513: <paramref name="part"/>'s pattern as it is drawn: in its pattern colour as said, its opacity the pattern's, where
    /// one is; else in <paramref name="ink"/>, its text's, at the pattern's strength (<see cref="PatternOf(Pattern, Argb)"/>).
    /// </summary>
    public static Patterned? PatternOf(PartPaints part, Argb ink) =>
        part.PatternColor.Color is { } said && PatternOf(part.Pattern, said) is { } drawn ? drawn with { Ink = said } : PatternOf(part.Pattern, ink);

    /// <summary>§462: how much of its strength a pattern has in a light ink: light lines on a dark card stand out more than dark ones on a light card.</summary>
    internal const double PatternOnDark = 0.7;

    /// <summary>§459: the highlight under the pointer where <paramref name="text"/> is drawn: the text at Windows' own step, a light text's (on a dark surface) or a dark one's.</summary>
    public static Argb HighlightOf(Argb text) => text.At(text.A / 255.0 * (IsDark(text) ? HighlightLight : HighlightDark));

    /// <summary>A paint as the painters take it: its colour; None no alpha; Windows' own null.</summary>
    private static Argb? Of(Paint paint) => paint.None ? new Argb(0, 0, 0, 0) : paint.Color;

    /// <summary>The middle shine between black and white as the eye has it (L* 50).</summary>
    private const double MidGrey = 0.18;

    /// <summary>
    /// <paramref name="color"/> made readable on every one of <paramref name="surfaces"/> at <paramref name="ratio"/>: as it is
    /// where it is; else mixed towards white in the dark mode, black in the light, the least that does; else towards the
    /// other end; else whichever end reads best.
    /// </summary>
    public static Argb Readable(Argb color, IReadOnlyList<Argb> surfaces, bool dark, double ratio)
    {
        color = color with { A = 255 };
        double Least(Argb ink) => surfaces.Min(surface => Contrast(ink, surface));
        if (Least(color) >= ratio)
        {
            return color;
        }
        Argb white = new(255, 255, 255, 255), black = new(255, 0, 0, 0);
        foreach (var end in dark ? new[] { white, black } : [black, white])
        {
            for (var share = 0.05; share <= 1.0001; share += 0.05)
            {
                var mixed = Mix(color, end, share);
                if (Least(mixed) >= ratio)
                {
                    return mixed;
                }
            }
        }
        return Least(white) >= Least(black) ? white : black;
    }

    /// <summary><paramref name="a"/> mixed <paramref name="share"/> of the way to <paramref name="b"/>, opaque.</summary>
    public static Argb Mix(Argb a, Argb b, double share)
    {
        byte Of(byte from, byte to) => (byte)Math.Round(from + ((to - from) * share), MidpointRounding.AwayFromZero);
        return new Argb(255, Of(a.R, b.R), Of(a.G, b.G), Of(a.B, b.B));
    }

    /// <summary>§464: <paramref name="top"/> laid over <paramref name="bottom"/>, both may be see-through: the one colour they come to together.</summary>
    public static Argb Over(Argb top, Argb bottom)
    {
        var (a, b) = (top.A / 255.0, bottom.A / 255.0);
        var alpha = a + (b * (1 - a));
        if (alpha <= 0)
        {
            return default;
        }
        byte Of(byte up, byte down) => (byte)Math.Round(((up * a) + (down * b * (1 - a))) / alpha, MidpointRounding.AwayFromZero);
        return new Argb((byte)Math.Round(alpha * 255, MidpointRounding.AwayFromZero), Of(top.R, bottom.R), Of(top.G, bottom.G), Of(top.B, bottom.B));
    }

    /// <summary><paramref name="over"/> laid over the opaque <paramref name="under"/>, as it shows.</summary>
    public static Argb Blend(Argb over, Argb under) => Mix(under, over with { A = 255 }, over.A / 255.0);

    /// <summary>The WCAG contrast of two opaque colours: 1 to 21.</summary>
    public static double Contrast(Argb a, Argb b)
    {
        var (one, two) = (Luminance(a), Luminance(b));
        return (Math.Max(one, two) + 0.05) / (Math.Min(one, two) + 0.05);
    }

    /// <summary>The WCAG relative luminance of a colour, its alpha left aside: 0 for black, 1 for white.</summary>
    public static double Luminance(Argb color)
    {
        static double Linear(byte channel)
        {
            var value = channel / 255.0;
            return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }
        return (0.2126 * Linear(color.R)) + (0.7152 * Linear(color.G)) + (0.0722 * Linear(color.B));
    }
}

/// <summary>
/// A list of colours' text as the file has it; an entry that is not text is left out, and anything
/// but a list reads as none, rather than making the whole file unreadable.
/// </summary>
public sealed class ColorListConverter : JsonConverter<List<string>?>
{
    public override List<string>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            reader.Skip();
            return null;
        }
        var list = new List<string>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                list.Add(reader.GetString()!);
            }
            else
            {
                reader.Skip();
            }
        }
        return list;
    }

    public override void Write(Utf8JsonWriter writer, List<string>? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }
        writer.WriteStartArray();
        foreach (var text in value)
        {
            writer.WriteStringValue(text);
        }
        writer.WriteEndArray();
    }
}

/// <summary>
/// A colour's text as the file has it; a number, a switch, a list or anything else where a colour's
/// text belongs reads as Inherited, rather than making the whole file unreadable.
/// </summary>
public sealed class ColorTextConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return reader.GetString();
        }
        reader.Skip();
        return null;
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStringValue(value);
        }
    }
}
