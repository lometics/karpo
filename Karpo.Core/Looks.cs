// Looks (the person, 2026-11-05: "the styles are too complicated. styles should not exist anymore, everywhere where we use
// styles there should be the values to configure yourself again."; docs\widgets.md §507, which replaces §459's, §464's and §499's
// styles). §567 (the person, 2026-11-08: "you should not be able to set widget level or folder level looks anymore. remove
// that feature completely. just one global look configuration. (rename it to Appearance) also not on folder level."): there
// is one look in Karpo, its Appearance (Config.Appearance), Karpo-wide — no layout, widget, folder or line has one of its own.
//
// There are no named styles. A look is a set of values (LookValues): a background, an outline, a text and an accent colour,
// a pattern and its colour (§513), a font (§556: its family and weight as one) — each left unset or said. The Appearance
// holds them in four sections (LookPart; §626 named so on the page):
//
//   Taskbar              the bar: its background, outline and pattern; §547 the blur of the wallpaper under it
//   Taskbar Widgets      every card on the bar — a widget's, a folder's face, Windows' own menus' faces —: all seven
//   Flyouts              the outline of every flyout a widget opens — an open folder (its panel has no background, §464),
//                        the tray's overflow, a widget's list —, one thing (§514); §623 the backdrop over every display
//                        while a folder is open at rest (none by default); §649 the area an attached folder stands on
//                        (the bar's colour by default)
//   Flyout Widgets       a folder's lines, and every menu and flyout of Karpo's, whose rows are lines too: all seven
//                        (FolderWidgets; "Widgets in folders" until §626)
//
// A value the file leaves out is the palette worn's (§547 KarpoLook for Karpo's own and Custom; what a design leaves out
// Karpo's) — the one resolver, Over.
// §568 (the person, 2026-11-08: "dark and light mode should be merged together again and the dark and light mode setting of
// the operating system should be respected on the taskbar again"): every colour is a pair (ColorPair), one for Windows' dark
// mode and one for its light mode, each its own default (Karpo's own of that mode, KarpoLook.Paints); the taskbar is drawn
// in the half of the mode Windows is in, live as it changes. The font, the pattern and the bar's blur are one each.
//
// The font is a value like the colours (§556: a family installed on this PC and one of its weights, one value, inherited
// whole): a widget's text is in its section's (the bar's Taskbar widgets', a line's Widgets in folders'); the menus and
// flyouts are in the Widgets in folders' font. The kit's ramp stands on its weight (TextStyle.Weights).
//
// §577 (the person, 2026-11-09: "you should be able to select the palettes or you can also select custom ... when you have
// graphite selected for example you should also be able to change the settings but then you can also reset them to the
// default for the style again ... the style configurations for the other styles should also be saved even if it is not
// active."): the Appearance wears one palette (LookPalettes: Karpo's own, the four looks Karpo designed, and Custom, the
// person's own, which starts from Karpo's), each designed for both modes (§568). Each palette keeps its own values over its
// design (PaletteValues, in the four sections), active or not: what is drawn is the palette worn's design under its values.
// A designed palette's values reset to its design; Custom has nothing to reset to.
//
// §613 (the person, 2026-11-09: "the appearance should actually only show for dark or light mode including all the
// settings. you can switch between those with a switcher at the top, either dark light or system."; "this is a real setting.
// default is from system."): the Appearance says which mode Karpo's taskbar is in (Appearance.Mode, TaskbarMode): System, the
// default, the mode Windows is in, live as it changes; or Dark or Light, whatever Windows is in. Every bar, folder, menu and
// flyout of the taskbar wears that mode's half of every colour pair (Looks.Dark); both halves stay kept, the page showing
// and writing the one of the mode in force.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Karpo.Core;

/// <summary>§507: the sections of the Appearance, in the order its page lists them.</summary>
public enum LookPart
{
    /// <summary>The bar itself: its background, its outline, its pattern.</summary>
    Taskbar,

    /// <summary>Everything on the bar, each on its card: every widget, every folder's face and Windows' own menus' faces.</summary>
    Widgets,

    /// <summary>
    /// §514: the outline of every flyout a widget opens — an open folder, whose panel has no background (its lines drawn each
    /// on its own, §464), and a widget's flyout (the tray's overflow, a widget's list) alike. The file's "flyouts".
    /// </summary>
    Flyouts,

    /// <summary>A folder's lines, each on its card; and every menu's and flyout's rows, lines too.</summary>
    FolderWidgets,
}

/// <summary>
/// §556: a text's font — a family installed on this PC and one of its weights, one value (the person, 2026-11-07: "font and
/// weight should also be combined into one setting"). The file's form is "Family/Weight", the weight DirectWrite's 1 to 999
/// ("Sitka Text/600"); <c>default</c> is Karpo's own, Segoe UI Variable Text at 400. A family not installed here is drawn
/// in Karpo's own (Gfx.TextFamily) and kept as said.
/// </summary>
public readonly record struct TextStyle
{
    /// <summary>Karpo's own family, the one a text nobody set is in.</summary>
    public const string KarpoFamily = "Segoe UI Variable Text"; // not-ui: a font

    /// <summary>Regular: the weight a text nobody set is in, and one with no weight said.</summary>
    public const int Regular = 400;

    // Karpo's own family null and Regular 0, so default is Karpo's own font and one font is one value.
    private readonly string? family;
    private readonly int weight;

    /// <summary><paramref name="family"/> (Karpo's own for null or none) at <paramref name="weight"/>, held to 1 to 999.</summary>
    public TextStyle(string? family, int weight = Regular)
    {
        var named = family?.Trim();
        this.family = string.IsNullOrEmpty(named) || string.Equals(named, KarpoFamily, StringComparison.OrdinalIgnoreCase) ? null : named;
        var held = Math.Clamp(weight, 1, 999);
        this.weight = held == Regular ? 0 : held;
    }

    /// <summary>The family DirectWrite is asked for.</summary>
    public string Family => family ?? KarpoFamily;

    /// <summary>The weight its body is drawn in, DirectWrite's 1 to 999.</summary>
    public int Weight => weight == 0 ? Regular : weight;

    /// <summary>Whether it is in Karpo's own family, whatever its weight.</summary>
    public bool IsKarpoFamily => family is null;

    /// <summary>
    /// The weights the kit's two roles are drawn in (the body's, the strong one's: a title, a figure): the body the font's
    /// own, the strong one two steps over it (Regular's 400 and 600, as the kit always drew), one step from Semibold on.
    /// </summary>
    public (int Body, int Strong) Weights => (Weight, Weight < 600 ? Weight + 200 : Math.Min(Weight + 100, 999));

    /// <summary>As the file has it: "Segoe UI Variable Text/400", "Sitka Text/600".</summary>
    public string Text => Family + "/" + Weight.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary><paramref name="text"/> as the file has it, "Family/Weight" (a family alone Regular); null for nothing, which is unset.</summary>
    public static TextStyle? Parse(string? text)
    {
        var said = text?.Trim() ?? "";
        var slash = said.LastIndexOf('/');
        if (slash >= 0 && int.TryParse(said.AsSpan(slash + 1), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var weight))
        {
            var family = said[..slash].Trim();
            return family.Length == 0 ? null : new TextStyle(family, weight);
        }
        return said.Length == 0 ? null : new TextStyle(said);
    }
}

/// <summary>§556: a font family installed on this PC, its name as DirectWrite has it, and the weights its upright faces are in, lightest first.</summary>
public sealed record InstalledFont(string Family, IReadOnlyList<int> Weights);

/// <summary>
/// §556: the font families on this PC a text may be in, each with its weights, by name — read once, when first asked (the
/// dock reads DirectWrite's system collection, Karpo.Dock's SystemFonts), again after Windows says the fonts changed
/// (<see cref="Forget"/>). With no reader set (a test) the families Windows 11 ships that Karpo offered before (<see cref="Shipped"/>).
/// </summary>
public static class InstalledFonts
{
    private static Func<IReadOnlyList<InstalledFont>>? reader;
    private static IReadOnlyList<InstalledFont>? read;

    /// <summary>Where the families are read from; set, they are read afresh when next asked.</summary>
    public static Func<IReadOnlyList<InstalledFont>> Reader
    {
        get => reader ?? (() => Shipped);
        set
        {
            reader = value;
            read = null;
        }
    }

    /// <summary>The families, by name, each its weights lightest first; read once.</summary>
    public static IReadOnlyList<InstalledFont> All => read ??= Sorted(Reader());

    /// <summary>The fonts changed (WM_FONTCHANGE): read again when next asked.</summary>
    public static void Forget() => read = null;

    /// <summary>Windows 11's families Karpo offered before it read this PC's (§459), with their weights: what a test is offered.</summary>
    public static readonly InstalledFont[] Shipped =
    [
        new("Bahnschrift", [300, 350, 400, 600, 700]), // not-ui: a font
        new("Cascadia Mono", [200, 300, 350, 400, 600, 700]), // not-ui: a font
        new("Georgia", [400, 700]), // not-ui: a font
        new("Segoe UI", [300, 350, 400, 600, 700, 900]), // not-ui: a font
        new(TextStyle.KarpoFamily, [300, 350, 400, 600, 700]),
        new("Sitka Text", [400, 500, 600, 700]), // not-ui: a font
    ];

    /// <summary><paramref name="fonts"/> by name, once each, each one's weights once each, lightest first; a family of none left out.</summary>
    private static InstalledFont[] Sorted(IReadOnlyList<InstalledFont> fonts) =>
        [.. fonts.Where(font => font.Family.Trim().Length > 0)
            .DistinctBy(font => font.Family, StringComparer.OrdinalIgnoreCase)
            .Select(font => font with { Weights = [.. font.Weights.Where(weight => weight is >= 1 and <= 999).Distinct().Order()] })
            .Where(font => font.Weights.Count > 0)
            .OrderBy(font => font.Family, StringComparer.CurrentCultureIgnoreCase)];
}

/// <summary>
/// §556: the fonts a look's text may be in, and what each is called; §638 (the person, 2026-11-09: "font weight should be
/// selectable individually but on the same row") a family and its weight picked apart: the families, then a family's weights.
/// </summary>
public static class TextStyles
{
    /// <summary>
    /// §638: every family installed (<see cref="InstalledFonts"/>), by name, and <paramref name="own"/>'s where it is none of
    /// them (a font of another PC's, kept as said) in its place among them.
    /// </summary>
    public static List<string> Families(TextStyle own)
    {
        List<string> families = [.. InstalledFonts.All.Select(font => font.Family)];
        if (!families.Contains(own.Family, StringComparer.OrdinalIgnoreCase))
        {
            families.Add(own.Family);
            families.Sort(StringComparer.CurrentCultureIgnoreCase);
        }
        return families;
    }

    /// <summary>
    /// §638: the weights <paramref name="family"/> is installed in, lightest first, and <paramref name="own"/> where it is none
    /// of them (a weight of another PC's, kept as said) in its place; a family not installed <paramref name="own"/> alone, else
    /// Regular.
    /// </summary>
    public static List<int> Weights(string family, int? own = null)
    {
        List<int> weights = [.. InstalledFonts.All.FirstOrDefault(font => string.Equals(font.Family, family, StringComparison.OrdinalIgnoreCase))?.Weights ?? []];
        if (own is { } kept && !weights.Contains(kept))
        {
            weights.Add(kept);
            weights.Sort();
        }
        return weights.Count > 0 ? weights : [TextStyle.Regular];
    }

    /// <summary>
    /// §638: <paramref name="font"/> moved to <paramref name="family"/>: its weight where that family has it, else the family's
    /// nearest — of two as near the heavier past Medium, else the lighter, as CSS matches a weight —; in its own family itself.
    /// </summary>
    public static TextStyle In(string family, TextStyle font)
    {
        if (string.Equals(family, font.Family, StringComparison.OrdinalIgnoreCase))
        {
            return font;
        }
        var heavier = font.Weight > 500;
        return new TextStyle(family, Weights(family).MinBy(weight => (Math.Abs(weight - font.Weight), heavier ? -weight : weight)));
    }

    /// <summary>A weight's name, OpenType's for the nearest of its named weights: "Thin" (100) to "Extra black" (950), "Regular" for 400.</summary>
    public static string WeightName(int weight) => Named.MinBy(each => Math.Abs(each.Weight - weight)).Name();

    private static readonly (int Weight, Func<string> Name)[] Named =
    [
        (100, () => S.SettingsAppearanceStylesWeightThin),
        (200, () => S.SettingsAppearanceStylesWeightExtraLight),
        (300, () => S.SettingsAppearanceStylesWeightLight),
        (350, () => S.SettingsAppearanceStylesWeightSemilight),
        (400, () => S.SettingsAppearanceStylesWeightRegular),
        (500, () => S.SettingsAppearanceStylesWeightMedium),
        (600, () => S.SettingsAppearanceStylesWeightSemibold),
        (700, () => S.SettingsAppearanceStylesWeightBold),
        (800, () => S.SettingsAppearanceStylesWeightExtraBold),
        (900, () => S.SettingsAppearanceStylesWeightBlack),
        (950, () => S.SettingsAppearanceStylesWeightExtraBlack),
    ];
}

/// <summary>
/// §459: one colour of a look: Windows' own (the default: nothing set), a colour with its opacity, or None — not there at
/// all: no plate, no outline.
/// </summary>
public readonly record struct Paint(Argb? Color = null, bool None = false)
{
    /// <summary>None: not there.</summary>
    public static readonly Paint Nothing = new(None: true);

    /// <summary>Windows' own: nothing set.</summary>
    public bool IsWindows => Color is null && !None;

    /// <summary>As the file has it: "#RRGGBB", "#AARRGGBB" where it is not opaque, "none"; null for Windows' own.</summary>
    public string? Text => None ? NoneWord : Color is { } color ? color.A == 255 ? color.Rgb : color.ToString() : null;

    /// <summary>The file's word for None.</summary>
    public const string NoneWord = "none";

    /// <summary><paramref name="text"/> as the file has it; null for anything that does not read (unset).</summary>
    public static Paint? Parse(string? text) =>
        string.Equals(text?.Trim(), NoneWord, StringComparison.OrdinalIgnoreCase) ? Nothing : Argb.Parse(text) is { } color ? new Paint(color) : null;
}

/// <summary>
/// §507: one section's look as it is drawn: its background, outline, text and accent (a bar's and a panel's text
/// and accent unused), §462 the pattern over its background and §513 the colour it is drawn in (Windows' own: its text's,
/// fainter), and the text it is written in (<see cref="Type"/>: its font and weight; the Taskbar's and the Flyouts'
/// unused); §547 the Taskbar's the blur of the wallpaper under its see-through background, 0 to 100 (<see cref="Blur"/>,
/// <see cref="KarpoLook.Blur"/>; null unset, the others' unused); §623 the Flyouts' backdrop over every display while a
/// folder is open (<see cref="Backdrop"/>; unset or None nothing, the others' unused); §649 the Flyouts' area an attached
/// folder stands on (<see cref="Ground"/>; unset the bar's colour, the others' unused).
/// </summary>
public readonly record struct PartPaints(Paint Background = default, Paint Outline = default, Paint Text = default, Paint Accent = default, Pattern Pattern = Pattern.None, TextStyle Type = default, Paint PatternColor = default, int? Blur = null, Paint Backdrop = default, Paint Ground = default)
{
    public Paint this[Tone tone] => tone switch
    {
        Tone.Background => Background,
        Tone.Outline => Outline,
        Tone.Text => Text,
        Tone.PatternColor => PatternColor,
        Tone.Backdrop => Backdrop,
        Tone.Ground => Ground,
        _ => Accent,
    };

    /// <summary>
    /// §623: the colour laid over every display while a folder is open, as drawn: the backdrop said, with something of it to
    /// see; null for nothing there (unset, None, or clear).
    /// </summary>
    public Argb? BackdropColor => Backdrop.Color is { A: > 0 } color ? color : null;

    /// <summary>These with <paramref name="tone"/> <paramref name="paint"/>.</summary>
    public PartPaints With(Tone tone, Paint paint) => tone switch
    {
        Tone.Background => this with { Background = paint },
        Tone.Outline => this with { Outline = paint },
        Tone.Text => this with { Text = paint },
        Tone.PatternColor => this with { PatternColor = paint },
        Tone.Backdrop => this with { Backdrop = paint },
        Tone.Ground => this with { Ground = paint },
        _ => this with { Accent = paint },
    };
}

/// <summary>
/// §507: every section of the Appearance as drawn; §704 and the material it is drawn in where its palette is one
/// (<see cref="Glass"/>: Liquid glass's, of the mode it is drawn in), null for colours alone — the palette's, never a value of
/// the file's.
/// </summary>
public readonly record struct LookPaints(PartPaints Taskbar = default, PartPaints Widgets = default, PartPaints Flyouts = default, PartPaints FolderWidgets = default, GlassLook? Glass = null)
{
    public PartPaints this[LookPart part] => part switch
    {
        LookPart.Taskbar => Taskbar,
        LookPart.Widgets => Widgets,
        LookPart.Flyouts => Flyouts,
        _ => FolderWidgets,
    };

    /// <summary>These with <paramref name="part"/>'s look <paramref name="paints"/>.</summary>
    public LookPaints With(LookPart part, PartPaints paints) => part switch
    {
        LookPart.Taskbar => this with { Taskbar = paints },
        LookPart.Widgets => this with { Widgets = paints },
        LookPart.Flyouts => this with { Flyouts = paints },
        _ => this with { FolderWidgets = paints },
    };
}

/// <summary>§507: the sections, in the Appearance page's order, the values each has, and what each is called.</summary>
public static class LookParts
{
    public static readonly LookPart[] All = [LookPart.Taskbar, LookPart.Widgets, LookPart.Flyouts, LookPart.FolderWidgets];

    /// <summary>
    /// The colours <paramref name="part"/> has: the bar a background, an outline and its pattern's (§513), the Flyouts their
    /// outline alone (§464, §514: a folder has no background, a widget's flyout its rows', the Widgets in folders'; nor a pattern),
    /// the Taskbar widgets all five; §624 the Widgets in folders all but an outline — a folder's border is the folder's own,
    /// the Flyouts', never one round each of its lines; §623 the Flyouts' backdrop over every display while a folder is open;
    /// §649 the Flyouts' ground an attached folder stands on, last: the one colour of theirs only some folders show.
    /// </summary>
    public static IReadOnlyList<Tone> TonesOf(LookPart part) => part switch
    {
        LookPart.Taskbar => [Tone.Background, Tone.Outline, Tone.PatternColor],
        LookPart.Flyouts => [Tone.Outline, Tone.Backdrop, Tone.Ground],
        LookPart.FolderWidgets => [Tone.Background, Tone.Text, Tone.Accent, Tone.PatternColor],
        _ => ToneNames.All,
    };

    /// <summary>§462: whether <paramref name="part"/> has a pattern: the bar and the cards; not the Flyouts, whose rows and lines cover them.</summary>
    public static bool HasPattern(LookPart part) => part is not LookPart.Flyouts;

    /// <summary>§547: whether <paramref name="part"/> has a blur of the wallpaper under it: the bar alone (<see cref="KarpoLook.Blur"/>).</summary>
    public static bool HasBlur(LookPart part) => part is LookPart.Taskbar;

    /// <summary>Whether <paramref name="part"/> has a font and a weight: the cards, which hold text.</summary>
    public static bool HasText(LookPart part) => part is LookPart.Widgets or LookPart.FolderWidgets;

    /// <summary>
    /// What a section is called: §626 "Taskbar", "Taskbar Widgets", "Flyouts", "Flyout Widgets" — §592 each a tab of the
    /// Appearance page (LookRows); the Taskbar the Taskbar page's own word.
    /// </summary>
    public static string Name(LookPart part) => part switch
    {
        LookPart.Taskbar => S.DockEditTaskbar,
        LookPart.Widgets => S.SettingsAppearanceStylesWidgets,
        LookPart.Flyouts => S.SettingsLookFlyouts,
        _ => S.SettingsAppearanceStylesFolderWidgets,
    };

    /// <summary>What the file calls a section: "taskbar", "widgets", "flyouts", "folderWidgets".</summary>
    public static string JsonName(LookPart part) => part.ToString() is var name ? char.ToLowerInvariant(name[0]) + name[1..] : "";

    /// <summary>A section's icon on the Appearance page.</summary>
    public static string Glyph(LookPart part) => part switch
    {
        LookPart.Taskbar => OptionGlyphs.Taskbar,
        LookPart.Widgets => OptionGlyphs.Tiles,
        LookPart.Flyouts => OptionGlyphs.FolderOpen,
        _ => OptionGlyphs.Details,
    };
}

/// <summary>
/// §568 (the person, 2026-11-08: "dark and light mode should be merged together again and the dark and light mode setting of
/// the operating system should be respected on the taskbar again"): a colour of the Appearance as the file has it, one for
/// each of Windows' modes — <c>{ "dark": "#D9202020", "light": "#D9F3F3F3" }</c> —, each "#RRGGBB", "#AARRGGBB" or "none",
/// each left out while it is Karpo's own of its mode. What is no object (a colour alone) reads as nothing.
/// </summary>
[JsonConverter(typeof(ColorPairConverter))]
public sealed record ColorPair
{
    /// <summary>The colour while Windows is in its dark mode; null for Karpo's own of it.</summary>
    public string? Dark { get; set; }

    /// <summary>The colour while Windows is in its light mode; null for Karpo's own of it.</summary>
    public string? Light { get; set; }

    /// <summary>The colour of the mode <paramref name="dark"/>.</summary>
    public string? this[bool dark]
    {
        get => dark ? Dark : Light;
        set
        {
            if (dark)
            {
                Dark = value;
            }
            else
            {
                Light = value;
            }
        }
    }

    /// <summary>Neither said.</summary>
    public bool IsEmpty => Dark is null && Light is null;

    /// <summary>Each half held to a colour that reads; null where neither does.</summary>
    public ColorPair? Normalized()
    {
        var held = new ColorPair { Dark = Paint.Parse(Dark)?.Text, Light = Paint.Parse(Light)?.Text };
        return held.IsEmpty ? null : held;
    }
}

/// <summary>§568: a colour pair read and written as an object of its two halves; anything else read as nothing (a word this build does not know).</summary>
public sealed class ColorPairConverter : JsonConverter<ColorPair?>
{
    public override ColorPair? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            reader.Skip();
            return null;
        }
        var pair = new ColorPair();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var name = reader.GetString();
            _ = reader.Read();
            var text = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
            if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
            {
                reader.Skip();
            }
            if (string.Equals(name, "dark", StringComparison.Ordinal))
            {
                pair.Dark = text;
            }
            else if (string.Equals(name, "light", StringComparison.Ordinal))
            {
                pair.Light = text;
            }
        }
        return pair;
    }

    public override void Write(Utf8JsonWriter writer, ColorPair? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }
        writer.WriteStartObject();
        if (value.Dark is { } dark)
        {
            writer.WriteString("dark", dark);
        }
        if (value.Light is { } light)
        {
            writer.WriteString("light", light);
        }
        writer.WriteEndObject();
    }
}

/// <summary>
/// §507, §567: a section of the Appearance as the file has it: §568 its colours each a pair, one for Windows' dark mode and one
/// for its light mode (<see cref="ColorPair"/>; §513 its pattern colour among them); §462 its pattern's word ("none" for None
/// over what it would inherit); §556 its font, a family and a weight in one ("Sitka Text/600"); §547 the bar's blur — the font,
/// the pattern and the blur one each, in both modes. Every value left out is unset: Karpo's own (<see cref="KarpoLook"/>) of
/// the mode Windows is in.
/// </summary>
public sealed class LookValues
{
    /// <summary>§556: its text's font, family and weight as one (<see cref="TextStyle.Text"/>: "Sitka Text/600"); unset Karpo's own.</summary>
    [JsonPropertyOrder(0), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Font { get; set; }

    [JsonPropertyOrder(10), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ColorPair? Background { get; set; }

    [JsonPropertyOrder(11), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ColorPair? Outline { get; set; }

    [JsonPropertyOrder(12), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ColorPair? Text { get; set; }

    [JsonPropertyOrder(13), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ColorPair? Accent { get; set; }

    [JsonPropertyOrder(14), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Pattern? Pattern { get; set; }

    /// <summary>§513: the ink its pattern is drawn in, as said, its opacity the pattern's; unset its text's, fainter (<see cref="ToneRules.PatternOf"/>).</summary>
    [JsonPropertyOrder(15), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ColorPair? PatternColor { get; set; }

    /// <summary>
    /// §547: the Taskbar's: how blurred the wallpaper under its see-through background is, 0 (sharp) to 100
    /// (<see cref="KarpoLook.MostBlur"/>); unset Karpo's own (<see cref="KarpoLook.Blur"/>).
    /// </summary>
    [JsonPropertyOrder(16), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Blur { get; set; }

    /// <summary>
    /// §623: the Flyouts': the colour laid over every display, behind the folder and Karpo's bars, while a folder is open at
    /// rest; unset (Karpo's own) or "none" nothing there.
    /// </summary>
    [JsonPropertyOrder(17), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ColorPair? Backdrop { get; set; }

    /// <summary>
    /// §649: the Flyouts': the area an attached folder stands on, joined to its bar (§647); unset (Karpo's own) the bar's colour,
    /// solid.
    /// </summary>
    [JsonPropertyOrder(18), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ColorPair? Ground { get; set; }

    /// <summary>§568: <paramref name="tone"/>'s pair.</summary>
    public ColorPair? this[Tone tone]
    {
        get => tone switch
        {
            Tone.Background => Background,
            Tone.Outline => Outline,
            Tone.Text => Text,
            Tone.PatternColor => PatternColor,
            Tone.Backdrop => Backdrop,
            Tone.Ground => Ground,
            _ => Accent,
        };
        set
        {
            switch (tone)
            {
                case Tone.Background: Background = value; break;
                case Tone.Outline: Outline = value; break;
                case Tone.Text: Text = value; break;
                case Tone.PatternColor: PatternColor = value; break;
                case Tone.Backdrop: Backdrop = value; break;
                case Tone.Ground: Ground = value; break;
                default: Accent = value; break;
            }
        }
    }

    /// <summary>§568: <paramref name="tone"/> in the mode <paramref name="dark"/>, as the file has it; null unset.</summary>
    public string? this[Tone tone, bool dark]
    {
        get => this[tone]?[dark];
        set
        {
            var pair = this[tone] ?? new ColorPair();
            pair[dark] = value;
            this[tone] = pair.IsEmpty ? null : pair;
        }
    }

    /// <summary>Nothing said at all.</summary>
    [JsonIgnore]
    public bool IsEmpty => Background is null && Outline is null && Text is null && Accent is null && Pattern is null && PatternColor is null && Font is null && Blur is null && Backdrop is null && Ground is null;

    /// <summary>A copy.</summary>
    public LookValues Copy() => new()
    {
        Font = Font,
        Background = Background is { } background ? background with { } : null,
        Outline = Outline is { } outline ? outline with { } : null,
        Text = Text is { } text ? text with { } : null,
        Accent = Accent is { } accent ? accent with { } : null,
        Pattern = Pattern,
        PatternColor = PatternColor is { } ink ? ink with { } : null,
        Blur = Blur,
        Backdrop = Backdrop is { } backdrop ? backdrop with { } : null,
        Ground = Ground is { } ground ? ground with { } : null,
    };

    /// <summary>
    /// <paramref name="under"/> (Karpo's own section of the mode <paramref name="dark"/>) with these values over it, of what
    /// <paramref name="part"/> has (<see cref="LookParts.TonesOf"/>, §568 each colour its half of that mode, its pattern where it
    /// has one, its font where it holds text — §556 family and weight as one, inherited whole): each that reads, else
    /// <paramref name="under"/>'s.
    /// </summary>
    public PartPaints Over(PartPaints under, LookPart part, bool dark)
    {
        var paints = under;
        foreach (var tone in LookParts.TonesOf(part))
        {
            if (Paint.Parse(this[tone, dark]) is { } paint)
            {
                paints = paints.With(tone, paint);
            }
        }
        if (LookParts.HasPattern(part) && Pattern is { } pattern)
        {
            paints = paints with { Pattern = pattern };
        }
        if (LookParts.HasBlur(part) && Blur is { } blur)
        {
            paints = paints with { Blur = KarpoLook.Held(blur) };
        }
        return LookParts.HasText(part) && TextStyle.Parse(Font) is { } font ? paints with { Type = font } : paints;
    }

    /// <summary>
    /// Held to what <paramref name="part"/> may say, and nothing else (a word this build does not know unset): its colours,
    /// §568 each half that reads, its pattern where it has one, the font where it holds text (§556: in the file's one form,
    /// "Family/Weight").
    /// </summary>
    public void Normalize(LookPart part)
    {
        var tones = LookParts.TonesOf(part);
        foreach (var tone in ToneNames.Every)
        {
            this[tone] = tones.Contains(tone) ? this[tone]?.Normalized() : null;
        }
        if (Pattern is { } pattern && (!Enum.IsDefined(pattern) || !LookParts.HasPattern(part)))
        {
            Pattern = null;
        }
        // §547: the bar's blur, held to 0 to 100; no other section's.
        Blur = LookParts.HasBlur(part) && Blur is { } blur ? KarpoLook.Held(blur) : null;
        Font = LookParts.HasText(part) && TextStyle.Parse(Font) is { } font ? font.Text : null;
    }

    /// <summary>
    /// <paramref name="light"/> and <paramref name="dark"/>, a section as drawn in each of Windows' modes (§568), written out:
    /// every colour of what <paramref name="part"/> has, each mode's in its half; the pattern and the font (the light one's:
    /// one in both). What is left at Karpo's own (unset) left out. §577 a palette's design so laid over Karpo's own
    /// (<see cref="LookPalette.Drawn"/>).
    /// </summary>
    public static LookValues? Of(PartPaints light, PartPaints dark, LookPart part)
    {
        var look = new LookValues();
        foreach (var tone in LookParts.TonesOf(part))
        {
            look[tone, false] = light[tone].Text;
            look[tone, true] = dark[tone].Text;
        }
        look.Pattern = LookParts.HasPattern(part) && light.Pattern != Core.Pattern.None ? light.Pattern : null;
        look.Blur = LookParts.HasBlur(part) ? light.Blur : null;
        look.Font = LookParts.HasText(part) && light.Type != default ? light.Type.Text : null;
        return look.IsEmpty ? null : look;
    }
}

/// <summary>
/// §567, §577: a palette's values as the file has them (the file's "appearance.palettes.&lt;id&gt;"): its four sections over its
/// design, each left out while it says nothing.
/// </summary>
public sealed class PaletteValues
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LookValues? Taskbar { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LookValues? Widgets { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LookValues? Flyouts { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LookValues? FolderWidgets { get; set; }

    public LookValues? this[LookPart part]
    {
        get => part switch
        {
            LookPart.Taskbar => Taskbar,
            LookPart.Widgets => Widgets,
            LookPart.Flyouts => Flyouts,
            _ => FolderWidgets,
        };
        set
        {
            switch (part)
            {
                case LookPart.Taskbar: Taskbar = value; break;
                case LookPart.Widgets: Widgets = value; break;
                case LookPart.Flyouts: Flyouts = value; break;
                default: FolderWidgets = value; break;
            }
        }
    }

    /// <summary>Nothing said: its palette's design throughout.</summary>
    [JsonIgnore]
    public bool IsEmpty => LookParts.All.All(part => this[part] is null or { IsEmpty: true });

    /// <summary>A deep copy.</summary>
    public PaletteValues Copy() => new()
    {
        Taskbar = Taskbar?.Copy(),
        Widgets = Widgets?.Copy(),
        Flyouts = Flyouts?.Copy(),
        FolderWidgets = FolderWidgets?.Copy(),
    };

    /// <summary>Each section held to what it may say, one that says nothing left out; null where none says anything.</summary>
    internal PaletteValues? Normalized()
    {
        foreach (var part in LookParts.All)
        {
            this[part]?.Normalize(part);
            this[part] = this[part] is { IsEmpty: false } kept ? kept : null;
        }
        return IsEmpty ? null : this;
    }
}

/// <summary>
/// §613: the mode Karpo's taskbar is in (<see cref="Appearance.Mode"/>): Windows' own, live (the default), or one whatever
/// Windows is in, as Windows 11's "Choose your default Windows mode" pins its taskbar's. In the order the Appearance's switcher
/// shows them. A word this build does not know reads as Windows' (<see cref="Appearance"/>'s Normalized).
/// </summary>
[JsonConverter(typeof(TolerantEnumConverter<TaskbarMode>))]
public enum TaskbarMode
{
    [JsonStringEnumMemberName("system")] System,

    [JsonStringEnumMemberName("dark")] Dark,

    [JsonStringEnumMemberName("light")] Light,
}

/// <summary>
/// §567, §577: Karpo's one look as the file has it (<see cref="Config.Appearance"/>, the file's "appearance"): the palette
/// worn (<see cref="LookPalettes"/>; left out for Karpo's own) and each palette's own values over its design, worn or not
/// (Custom's the person's own), each left out while it says nothing.
/// </summary>
public sealed class Appearance
{
    /// <summary>§613: the mode Karpo's taskbar is in: Windows' (System, left out of the file), or dark or light whatever Windows is in.</summary>
    [JsonPropertyOrder(0), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public TaskbarMode Mode { get; set; }

    /// <summary>The palette worn, its id ("graphite", "custom"); null for Karpo's own.</summary>
    [JsonPropertyOrder(1), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Palette { get; set; }

    /// <summary>Each palette's values over its design, by its id; a palette with none left out.</summary>
    [JsonPropertyOrder(2), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, PaletteValues>? Palettes { get; set; }

    /// <summary>Palette <paramref name="id"/>'s values; set, null or nothing said takes them out.</summary>
    public PaletteValues? this[string id]
    {
        get => Palettes?.GetValueOrDefault(id);
        set
        {
            if (value is { IsEmpty: false })
            {
                (Palettes ??= new(StringComparer.Ordinal))[id] = value;
                return;
            }
            _ = Palettes?.Remove(id);
            if (Palettes is { Count: 0 })
            {
                Palettes = null;
            }
        }
    }

    /// <summary>Nothing said: Windows' mode, Karpo's own palette, as designed, and no palette's values.</summary>
    [JsonIgnore]
    public bool IsEmpty => Mode == TaskbarMode.System && Palette is null && Palettes is null or { Count: 0 };

    /// <summary>A deep copy.</summary>
    public Appearance Copy() => new()
    {
        Mode = Mode,
        Palette = Palette,
        Palettes = Palettes is { } palettes ? new(palettes.Select(each => KeyValuePair.Create(each.Key, each.Value.Copy())), StringComparer.Ordinal) : null,
    };

    /// <summary>
    /// Held to what it may say: §613 a mode this build does not know Windows'; a palette this build does not know worn as
    /// Karpo's own, Karpo's left out; each palette's values held to what they may say, a palette this build does not know or
    /// one that says nothing left out; null where nothing is said.
    /// </summary>
    internal Appearance? Normalized()
    {
        Mode = Mode is TaskbarMode.Dark or TaskbarMode.Light ? Mode : TaskbarMode.System;
        Palette = LookPalettes.Of(Palette) is { } worn && worn.Id != LookPalettes.KarpoId ? worn.Id : null;
        var said = Palettes;
        Palettes = null;
        foreach (var (id, values) in said ?? [])
        {
            if (LookPalettes.Of(id) is { } palette && values?.Normalized() is { } kept)
            {
                this[palette.Id] = kept;
            }
        }
        return IsEmpty ? null : this;
    }
}

public sealed partial class Config
{
    /// <summary>
    /// §567: Karpo's one look (<see cref="Core.Appearance"/>, the file's "appearance"): the look of every bar, its widgets, its
    /// folders and every flyout and menu opened from them, in four sections (<see cref="Looks"/>); §577 the palette worn and
    /// each palette's values; null, not written, for Karpo's own palette as designed and no palette's values.
    /// </summary>
    [JsonPropertyName("appearance"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Appearance? Appearance { get; set; }
}

/// <summary>
/// §429, §459: what a look shows on a menu's row: its card, its rim where it has one, its text (none on a pattern's choice,
/// which shows its pattern); §462: the pattern on it, where it has one.
/// </summary>
public readonly record struct LookSwatch(Argb Fill, Argb? Rim, Argb? Ink, Patterned? Pattern = null);

/// <summary>§567: a section of the Appearance, where it is written (<see cref="Looks.Section"/>): the section, its values now, how new ones are put there.</summary>
public sealed record LookSlot(LookPart Part, Func<LookValues?> Get, Action<LookValues?> Put);

/// <summary>§507: the one resolver and the one place a look is written; see the top of the file.</summary>
public static class Looks
{
    /// <summary>
    /// §567, §568: <paramref name="config"/>'s Appearance as drawn while Windows is in the mode <paramref name="dark"/>: §577 the
    /// palette worn's values of that mode over its design of it (<see cref="LookPalette.Drawn"/>).
    /// </summary>
    public static LookPaints Of(Config config, bool dark) => Of(config.Appearance, dark);

    /// <summary>
    /// §613: whether Karpo's taskbar is dark, <paramref name="appearance"/> saying its mode and Windows being in its dark mode
    /// (<paramref name="windows"/>) or not: Windows' for System, else the one said. The mode every bar, folder and flyout of the
    /// taskbar wears, and the Appearance page shows and edits.
    /// </summary>
    public static bool Dark(Appearance? appearance, bool windows) => (appearance?.Mode ?? TaskbarMode.System) switch
    {
        TaskbarMode.Dark => true,
        TaskbarMode.Light => false,
        _ => windows,
    };

    /// <summary>§613: <paramref name="config"/>'s taskbar in <paramref name="mode"/>; false where it is already.</summary>
    public static bool WriteMode(Config config, TaskbarMode mode)
    {
        var appearance = config.Appearance ?? new Appearance();
        if (appearance.Mode == mode)
        {
            return false;
        }
        appearance.Mode = mode;
        config.Appearance = appearance.IsEmpty ? null : appearance;
        return true;
    }

    /// <summary><paramref name="appearance"/> as drawn in the mode <paramref name="dark"/>: §577 the palette worn's values of it over its design of it.</summary>
    public static LookPaints Of(Appearance? appearance, bool dark)
    {
        var palette = LookPalettes.Worn(appearance);
        return Of(appearance?[palette.Id], palette.Drawn(dark), dark);
    }

    /// <summary>§577: <paramref name="values"/> of the mode <paramref name="dark"/> over <paramref name="under"/>, section by section.</summary>
    public static LookPaints Of(PaletteValues? values, LookPaints under, bool dark)
    {
        var paints = under;
        foreach (var part in LookParts.All)
        {
            paints = paints.With(part, Over(values?[part], paints[part], part, dark));
        }
        return paints;
    }

    /// <summary>
    /// The one resolver: <paramref name="look"/>'s values of the mode <paramref name="dark"/> over <paramref name="under"/>,
    /// Karpo's own of it (§547, §568), for <paramref name="part"/>; what is left unset is Karpo's own (<see cref="ToneRules.Dress"/>).
    /// </summary>
    public static PartPaints Over(LookValues? look, PartPaints under, LookPart part, bool dark) => look?.Over(under, part, dark) ?? under;

    /// <summary>
    /// §649: the area an attached folder stands on (§647) in <paramref name="paints"/>, the Appearance as drawn in the mode
    /// <paramref name="dark"/>: the Flyouts' ground as said, its opacity its own; unset the bar's — its Taskbar background solid,
    /// Windows' own bar of the mode where it has none of its own. What the row shows where nothing is said (Karpo.Dock's
    /// FolderFlyout.AttachedGround draws the bar's as the bar is drawn, high contrast too).
    /// </summary>
    public static Argb Ground(LookPaints paints, bool dark) =>
        paints.Flyouts.Ground.Color is { } said ? said
        : paints.Taskbar.Background.Color is { } bar ? bar with { A = 255 }
        : ToneRules.WindowsBackground(dark);

    /// <summary>The font Karpo's menus and flyouts are in: the Appearance's Widgets in folders' (one in both modes).</summary>
    public static TextStyle MenuText(Config config) => Of(config, dark: true).FolderWidgets.Type;

    /// <summary>
    /// §567: <paramref name="config"/>'s Appearance's section <paramref name="part"/>, where it is written: §577 the palette
    /// worn's values', kept with that palette.
    /// </summary>
    public static LookSlot Section(Config config, LookPart part) => new(part, () => config.Appearance?[LookPalettes.Worn(config).Id]?[part], look =>
    {
        var appearance = config.Appearance ?? new Appearance();
        var id = LookPalettes.Worn(appearance).Id;
        var values = appearance[id] ?? new PaletteValues();
        values[part] = look;
        appearance[id] = values;
        config.Appearance = appearance.IsEmpty ? null : appearance;
    });

    /// <summary>Changes the look kept at <paramref name="slot"/>, held to what it may say, one that says nothing taken out; false where nothing changed.</summary>
    public static bool Write(LookSlot slot, Action<LookValues> change)
    {
        var now = slot.Get();
        var look = now?.Copy() ?? new LookValues();
        change(look);
        look.Normalize(slot.Part);
        var next = look.IsEmpty ? null : look;
        if (Same(now, next))
        {
            return false;
        }
        slot.Put(next);
        return true;
    }

    /// <summary>§568: <paramref name="tone"/> of the mode <paramref name="dark"/> at <paramref name="slot"/> set to <paramref name="paint"/>, unset for null; the other mode's as it was.</summary>
    public static bool WritePaint(LookSlot slot, Tone tone, bool dark, Paint? paint) => Write(slot, look => look[tone, dark] = paint?.Text);

    /// <summary>§462: the pattern at <paramref name="slot"/>, unset for null.</summary>
    public static bool WritePattern(LookSlot slot, Pattern? pattern) => Write(slot, look => look.Pattern = pattern);

    /// <summary>§556: the font at <paramref name="slot"/>, its family and its weight as one; unset for null.</summary>
    public static bool WriteFont(LookSlot slot, TextStyle? font) => Write(slot, look => look.Font = font?.Text);

    /// <summary>§547: the bar's blur at <paramref name="slot"/> (the Taskbar), 0 to 100; unset for null.</summary>
    public static bool WriteBlur(LookSlot slot, int? blur) => Write(slot, look => look.Blur = blur);

    /// <summary>Whether two looks say the same, value for value.</summary>
    public static bool Same(LookValues? a, LookValues? b) =>
        (a is null || a.IsEmpty) && (b is null || b.IsEmpty)
        || a is not null && b is not null && a.Font == b.Font && a.Background == b.Background && a.Outline == b.Outline
            && a.Text == b.Text && a.Accent == b.Accent && a.Pattern == b.Pattern && a.PatternColor == b.PatternColor
            && a.Blur == b.Blur && a.Backdrop == b.Backdrop && a.Ground == b.Ground;

    /// <summary>
    /// What section <paramref name="part"/> of <paramref name="paints"/> (the Appearance as drawn in the mode <paramref name="dark"/>)
    /// shows on a menu's row (§464: each on its own, over the desktop, here Windows' own surface of the mode): the bar its
    /// background and outline and its widgets' text on it; the Flyouts Windows' flyout, their outline round it; a card its
    /// background — a widget's of Windows' own the bar it stands on, a line's Windows' flyout —, its outline and its text.
    /// </summary>
    public static LookSwatch Swatch(LookPaints paints, LookPart part, bool dark) => Swatch(DressingOf(paints, part), part, dark, words: true);

    /// <summary>
    /// §462: what <paramref name="part"/> of <paramref name="paints"/> with <paramref name="pattern"/> shows on a pattern's
    /// choice: the bar itself for the Taskbar, else the section's card as <see cref="Swatch(LookPaints, LookPart, bool)"/> shows
    /// it, the pattern on it in its ink; no line of text across it.
    /// </summary>
    public static LookSwatch PatternSwatch(LookPaints paints, LookPart part, Pattern pattern, bool dark) =>
        Swatch(part == LookPart.Taskbar
            ? new Dressing(paints.Taskbar with { Pattern = pattern })
            : DressingOf(paints.With(part, paints[part] with { Pattern = pattern }), part), part, dark, words: false);

    /// <summary>What <paramref name="part"/> of <paramref name="paints"/> is dressed in on a menu's row, a card on its bar.</summary>
    private static Dressing DressingOf(LookPaints paints, LookPart part) => part switch
    {
        LookPart.Taskbar => new Dressing(paints.Taskbar, paints.Widgets),
        LookPart.Flyouts => new Dressing(Plate: new PartPaints(Outline: paints.Flyouts.Outline)),
        LookPart.FolderWidgets => new Dressing(Plate: paints.FolderWidgets),
        _ => new Dressing(paints.Taskbar, paints[part]),
    };

    /// <summary>A row's swatch of <paramref name="dressing"/>: its card, else what it stands on, its rim, the pattern on it, and with <paramref name="words"/> its text.</summary>
    private static LookSwatch Swatch(Dressing dressing, LookPart part, bool dark, bool words)
    {
        var (drawn, mode) = ToneRules.Dress(dressing, dark);
        var desktop = part is LookPart.Flyouts or LookPart.FolderWidgets ? ToneRules.WindowsFlyout(mode) : ToneRules.WindowsBackground(mode);
        var under = dressing.Plate.Background.IsWindows ? ToneRules.Seen(drawn.Taskbar, desktop) : desktop;
        var fill = drawn.Card is { } card ? ToneRules.Blend(card, under) : under;
        var outline = part == LookPart.Taskbar ? drawn.CardOutline ?? drawn.TaskbarOutline : drawn.CardOutline;
        var rim = outline is { A: > 0 } line ? ToneRules.Blend(line, fill) : (Argb?)null;
        return new LookSwatch(fill, rim, words ? drawn.Text ?? ToneRules.WindowsText(mode) : null, drawn.CardPattern ?? drawn.TaskbarPattern);
    }
}

/// <summary>
/// §507, §568: one of the looks Karpo designed: its family (§574 Karpo's own, Graphite, Paper, Harbor, Bloom; §618 Daylight,
/// Midnight, Contrast, Acrylic, Glacier, Ember, Blueprint, Moss; §704 Liquid glass; §577 Custom, the
/// person's own, Karpo's design), its design in Windows' light mode and in its dark one, each a whole Appearance (§509 to §567
/// two palettes, "Graphite light" and "Graphite dark", one set each); §704 a design's material with it (<see cref="LookPaints.Glass"/>).
/// </summary>
public sealed class LookPalette(string id, LookPaints light, LookPaints dark)
{
    private LookPaints? drawnLight;
    private LookPaints? drawnDark;

    /// <summary>The file's word for it ("graphite").</summary>
    public string Id { get; } = id;

    /// <summary>Its design in Windows' light mode; what it leaves out (<c>default</c>) Karpo's own.</summary>
    public LookPaints Light { get; } = light;

    /// <summary>Its design in Windows' dark mode.</summary>
    public LookPaints Dark { get; } = dark;

    /// <summary>Its design in the mode <paramref name="dark"/>.</summary>
    public LookPaints In(bool dark) => dark ? Dark : Light;

    /// <summary>
    /// §577: what it is drawn as in the mode <paramref name="dark"/> with no values of its own: its design written out over
    /// Karpo's own (<see cref="KarpoLook.Paints"/>), what a design leaves out Karpo's; §704 its material its design's, which no
    /// value of the file's changes.
    /// </summary>
    public LookPaints Drawn(bool dark) => dark
        ? drawnDark ??= Looks.Of(LookPalettes.Values(this), KarpoLook.Paints(dark: true) with { Glass = Dark.Glass }, dark: true)
        : drawnLight ??= Looks.Of(LookPalettes.Values(this), KarpoLook.Paints(dark: false) with { Glass = Light.Glass }, dark: false);

    /// <summary>§577: whether it has a design of its own to reset its values to: every palette but Custom.</summary>
    public bool Resets => Id != LookPalettes.CustomId;

    /// <summary>What it is called, in the language Karpo speaks: "Graphite".</summary>
    public string Name => Id switch
    {
        LookPalettes.KarpoId => S.SettingsLookPaletteKarpo,
        LookPalettes.PaperId => S.SettingsAppearanceThemePaper,
        LookPalettes.HarborId => S.SettingsAppearanceStylesHarbor,
        LookPalettes.BloomId => S.SettingsAppearanceStylesBloom,
        LookPalettes.DaylightId => S.SettingsLookPaletteDaylight,
        LookPalettes.MidnightId => S.SettingsLookPaletteMidnight,
        LookPalettes.ContrastId => S.SettingsLookPaletteContrast,
        LookPalettes.AcrylicId => S.SettingsLookPaletteAcrylic,
        LookPalettes.GlacierId => S.SettingsLookPaletteGlacier,
        LookPalettes.LiquidGlassId => S.SettingsLookPaletteLiquidGlass,
        LookPalettes.EmberId => S.SettingsLookPaletteEmber,
        LookPalettes.BlueprintId => S.SettingsLookPaletteBlueprint,
        LookPalettes.MossId => S.SettingsLookPaletteMoss,
        LookPalettes.CustomId => S.SettingsLookCustom,
        _ => S.SettingsAppearanceThemeGraphite,
    };
}

/// <summary>
/// §507, §568: the palettes — §574 Karpo's own and the four looks Karpo designed (§459's, their cards as §464 drew them, Bloom's
/// §499's), §618 eight more (see-through, patterned, light first, dark first, high contrast, accent forward), each in Windows' light mode and in its dark one; §577 Custom, the person's own — and what is done with them: one
/// worn, each its values kept with it, a designed one's reset to its design.
/// </summary>
public static class LookPalettes
{
    public const string KarpoId = "karpo";
    public const string GraphiteId = "graphite";
    public const string PaperId = "paper";
    public const string HarborId = "harbor";
    public const string BloomId = "bloom";
    public const string DaylightId = "daylight";
    public const string MidnightId = "midnight";
    public const string ContrastId = "contrast";
    public const string AcrylicId = "acrylic";
    public const string GlacierId = "glacier";
    public const string LiquidGlassId = "liquidGlass";
    public const string EmberId = "ember";
    public const string BlueprintId = "blueprint";
    public const string MossId = "moss";
    public const string CustomId = "custom";

    /// <summary>A colour from its 0xAARRGGBB.</summary>
    private static Paint C(uint argb) => new(new Argb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb));

    /// <summary>A colour from its 0xAARRGGBB, None for none.</summary>
    private static Paint Line(uint? argb) => argb is { } said ? C(said) : Paint.Nothing;

    /// <summary>A card: its background, its outline (none unless said), its text and accent, in <paramref name="type"/>.</summary>
    private static PartPaints Card(uint background, uint text, uint accent, TextStyle type = default, uint? outline = null) =>
        new(C(background), Line(outline), C(text), C(accent), Type: type);

    /// <summary>
    /// §624: a folder's lines: their background, text and accent, in <paramref name="type"/>; never an outline of their own —
    /// where a palette wants a border it is the folder's, the Flyouts' (<see cref="Rimmed"/>).
    /// </summary>
    private static PartPaints FolderLines(uint background, uint text, uint accent, TextStyle type = default) =>
        new(C(background), Text: C(text), Accent: C(accent), Type: type);

    /// <summary>
    /// The bar: its background, its outline (none unless said); §618 a pattern over it in its own colour where one is said,
    /// and the blur of the wallpaper under it where it says one (else Karpo's own, <see cref="KarpoLook.Blur"/>).
    /// </summary>
    private static PartPaints Ground(uint background, Pattern pattern = Pattern.None, uint? ink = null, int? blur = null, uint? outline = null) =>
        new(C(background), Line(outline), Pattern: pattern, PatternColor: ink is { } said ? C(said) : default, Blur: blur);

    /// <summary>§464, §514: the Flyouts: no outline (a folder has no background).</summary>
    private static readonly PartPaints Unlined = new(Outline: Paint.Nothing);

    /// <summary>§618: the Flyouts: every open flyout outlined in <paramref name="outline"/>.</summary>
    private static PartPaints Rimmed(uint outline) => new(Outline: C(outline));

    /// <summary>
    /// §704: Liquid glass in one mode (<paramref name="glass"/>): the bar, its cards, a folder's lines and the menus its tint at
    /// its most (<see cref="GlassLook.Designed"/>, which the painters make as clear as what is behind allows; a colour somebody
    /// chose is drawn as chosen), the bar over the wallpaper blurred at <see cref="GlassBlur"/>, the text Windows' own of the
    /// mode; no accent (the system's: Karpo.Dock's Look.Wearing) and no outline (the glass's rim is its material's); every flyout
    /// outlined white (<see cref="GlassLook.FlyoutOutline"/>); and the material itself (<see cref="LookPaints.Glass"/>).
    /// </summary>
    private static LookPaints Glass(GlassLook glass)
    {
        var (tint, text) = (new Paint(glass.Designed), new Paint(glass.Text));
        return new(
            new PartPaints(tint, Paint.Nothing, Blur: GlassBlur),
            new PartPaints(tint, Paint.Nothing, text),
            new PartPaints(Outline: new Paint(glass.FlyoutOutline)),
            new PartPaints(tint, Text: text),
            glass);
    }

    /// <summary>§704: the blur of the wallpaper under Liquid glass's bar: between Glacier's 60 and Acrylic's 80.</summary>
    public const int GlassBlur = 70;

    private static readonly TextStyle Sitka = new("Sitka Text"); // not-ui: a font
    private static readonly TextStyle Bahnschrift = new("Bahnschrift"); // not-ui: a font
    private static readonly TextStyle Semibold = new(TextStyle.KarpoFamily, 600);

    /// <summary>
    /// The palettes Karpo designed, in the order the Palette row lists them: §574 Karpo's own first (<see cref="KarpoLook.Paints"/>:
    /// what a new config wears); the neutrals — Graphite (calm neutral grey), §618 Daylight (bright white, light first: its dark
    /// half a soft charcoal), Midnight (true black, dark first: its light half a dim lavender grey, stars of dots on the bar) and
    /// Contrast (black and white, every card and flyout outlined, yellow or deep blue its accent, semibold) —; §618 the
    /// see-through ones — Acrylic (Windows' own greys, the bar half clear over the wallpaper blurred at 80, its cards three
    /// quarters, hairline rims), Glacier (icy blue, frosted grain on its clear bar, white rims) and §704 Liquid glass (Apple's,
    /// a material: <see cref="Glass"/>) —; §624 a folder's lines
    /// never outlined (<see cref="FolderLines"/>): a palette's border in a folder is the folder's, its Flyouts'; the colours — Paper (warm
    /// paper, Sitka), Harbor (navy and teal, Bahnschrift), Bloom (plum and raspberry, its cards on the bar berry, semibold) and
    /// §618 Ember (its cards on the bar burnt orange, the accent forward, semibold) —; and §618 the patterned — Blueprint (navy
    /// and blue, a grid on the bar and its cards, Bahnschrift) and Moss (forest greens, contours on the bar and its cards). Each
    /// its light design and its dark one, its light bar light and its dark one dark (§613's Mode says which a taskbar wears),
    /// one font, pattern and blur in both. Every text 4.5:1 and every accent 3:1 on what it stands on, a see-through card over
    /// a black and a white wallpaper alike (LookTests).
    /// </summary>
    public static readonly LookPalette[] All =
    [
        new(KarpoId, light: KarpoLook.Paints(dark: false), dark: KarpoLook.Paints(dark: true)),
        new(GraphiteId,
            light: new(Ground(0xE0EBECEE), Card(0xF6FAFAFB, 0xFF1D2024, 0xFF3456C8), Unlined, FolderLines(0xFCFDFDFE, 0xFF1D2024, 0xFF3456C8)),
            dark: new(Ground(0xE01B1C1F), Card(0xF927292D, 0xFFECEDEF, 0xFF8FA6FF), Unlined, FolderLines(0xFD2C2E33, 0xFFECEDEF, 0xFF8FA6FF))),
        new(DaylightId,
            light: new(Ground(0xF2FFFFFF), Card(0xFFFFFFFF, 0xFF1A1A1A, 0xFF0067C0, outline: 0x1A000000), Rimmed(0x1F000000), FolderLines(0xFFFFFFFF, 0xFF1A1A1A, 0xFF0067C0)),
            dark: new(Ground(0xF22B2D31), Card(0xFF383A40, 0xFFF2F3F5, 0xFF4CC2FF, outline: 0x14FFFFFF), Rimmed(0x1FFFFFFF), FolderLines(0xFF35373C, 0xFFF2F3F5, 0xFF4CC2FF))),
        new(MidnightId,
            light: new(Ground(0xF2E8E6F0, Pattern.Dots, ink: 0x262A2540), Card(0xFFF6F5FA, 0xFF1E1B2E, 0xFF5A40D0, outline: 0x1A1E1B2E), Rimmed(0x1F1E1B2E), FolderLines(0xFFF8F7FC, 0xFF1E1B2E, 0xFF5A40D0)),
            dark: new(Ground(0xFF000000, Pattern.Dots, ink: 0x2EFFFFFF), Card(0xFF121214, 0xFFECECF1, 0xFFA99BFF, outline: 0x1FFFFFFF), Rimmed(0x24FFFFFF), FolderLines(0xFF0C0C0E, 0xFFECECF1, 0xFFA99BFF))),
        new(ContrastId,
            light: new(Ground(0xFFFFFFFF), Card(0xFFFFFFFF, 0xFF000000, 0xFF0037C8, Semibold, outline: 0xFF000000), Rimmed(0xFF000000), FolderLines(0xFFFFFFFF, 0xFF000000, 0xFF0037C8, Semibold)),
            dark: new(Ground(0xFF000000), Card(0xFF000000, 0xFFFFFFFF, 0xFFFFE500, Semibold, outline: 0xFFFFFFFF), Rimmed(0xFFFFFFFF), FolderLines(0xFF000000, 0xFFFFFFFF, 0xFFFFE500, Semibold))),
        new(AcrylicId,
            light: new(Ground(0x99F3F3F3, blur: 80), Card(0xBFFFFFFF, 0xFF1B1B1B, 0xFF005FB8, outline: 0x1A000000), Rimmed(0x1F000000), FolderLines(0xE0FCFCFC, 0xFF1B1B1B, 0xFF005FB8)),
            dark: new(Ground(0x80202020, blur: 80), Card(0xBF2C2C2C, 0xFFFFFFFF, 0xFF60CDFF, outline: 0x1FFFFFFF), Rimmed(0x26FFFFFF), FolderLines(0xE02C2C2C, 0xFFFFFFFF, 0xFF60CDFF))),
        new(GlacierId,
            light: new(Ground(0x99DDEBF5, Pattern.Grain, ink: 0x1F0B2236, blur: 60), Card(0xCCF2F8FC, 0xFF0B2236, 0xFF0A6B9C, outline: 0x99FFFFFF), Rimmed(0x660A6B9C), FolderLines(0xE6F5FAFD, 0xFF0B2236, 0xFF0A6B9C)),
            dark: new(Ground(0x990D1B26, Pattern.Grain, ink: 0x1FCFEAFF, blur: 60), Card(0xCC16283A, 0xFFE3F2FC, 0xFF7FD4FF, outline: 0x339FD8FF), Rimmed(0x4D7FD4FF), FolderLines(0xE6142536, 0xFFE3F2FC, 0xFF7FD4FF))),
        new(LiquidGlassId, light: Glass(LiquidGlass.Light), dark: Glass(LiquidGlass.Dark)),
        new(PaperId,
            light: new(Ground(0xEBF1EBDF), Card(0xFDFAF6EE, 0xFF3A3027, 0xFFA84B24, Sitka), Unlined, FolderLines(0xFEFEFBF5, 0xFF3A3027, 0xFFA84B24, Sitka)),
            dark: new(Ground(0xEB25201B), Card(0xFD322B24, 0xFFF0E6D5, 0xFFE89B6C, Sitka), Unlined, FolderLines(0xFE362E27, 0xFFF0E6D5, 0xFFE89B6C, Sitka))),
        new(HarborId,
            light: new(Ground(0xE6D8E3EE), Card(0xFBEBF2F7, 0xFF0E2A44, 0xFF06708A, Bahnschrift), Unlined, FolderLines(0xFEF5F8FB, 0xFF0E2A44, 0xFF06708A, Bahnschrift)),
            dark: new(Ground(0xE60B192A), Card(0xFB13283F, 0xFFDDEBF7, 0xFF4CC8DF, Bahnschrift), Unlined, FolderLines(0xFD162E47, 0xFFDDEBF7, 0xFF4CC8DF, Bahnschrift))),
        new(BloomId,
            light: new(Ground(0xEBFFE4D6), Card(0xFFF7CFDE, 0xFF4A1530, 0xFFB0204F, Semibold), Unlined, FolderLines(0xFEFFFCFB, 0xFF4A1530, 0xFFC4285A, Semibold)),
            dark: new(Ground(0xEB1E0F24), Card(0xFF5C1E47, 0xFFFFE3EF, 0xFFFF7AA8, Semibold), Unlined, FolderLines(0xFE371C42, 0xFFFBE7F0, 0xFFFF7AA8, Semibold))),
        new(EmberId,
            light: new(Ground(0xEBFBE6D8), Card(0xFFB23F14, 0xFFFFFFFF, 0xFFFFE0B8, Semibold), Unlined, FolderLines(0xFEFFF8F3, 0xFF3D1A0C, 0xFFB8461B, Semibold)),
            dark: new(Ground(0xEB1A1210), Card(0xFF7A2E12, 0xFFFFE9DC, 0xFFFFB077, Semibold), Unlined, FolderLines(0xFE2A1C17, 0xFFFBE7DC, 0xFFFF9A5C, Semibold))),
        new(BlueprintId,
            light: new(Ground(0xF0DCE8F7, Pattern.Grid, ink: 0x2E1D5FC2), Card(0xFAEEF4FC, 0xFF0B2545, 0xFF1D5FC2, Bahnschrift) with { Pattern = Pattern.Grid }, Rimmed(0x4D1D5FC2), FolderLines(0xFEF4F8FD, 0xFF0B2545, 0xFF1D5FC2, Bahnschrift)),
            dark: new(Ground(0xF00B2545, Pattern.Grid, ink: 0x2E7CC4FF), Card(0xFA13315C, 0xFFE8F1FF, 0xFF7CC4FF, Bahnschrift) with { Pattern = Pattern.Grid }, Rimmed(0x4D7CC4FF), FolderLines(0xFE102B4F, 0xFFE8F1FF, 0xFF7CC4FF, Bahnschrift))),
        new(MossId,
            light: new(Ground(0xEBE3EBDD, Pattern.Contours, ink: 0x2E2F5233), Card(0xFCF2F6EE, 0xFF1E3322, 0xFF3B7A35) with { Pattern = Pattern.Contours }, Unlined, FolderLines(0xFEF7FAF4, 0xFF1E3322, 0xFF3B7A35)),
            dark: new(Ground(0xEB14201A, Pattern.Contours, ink: 0x339CCB9A), Card(0xFC1E2E25, 0xFFE4F0E2, 0xFF8FD18A) with { Pattern = Pattern.Contours }, Unlined, FolderLines(0xFE213328, 0xFFE4F0E2, 0xFF8FD18A))),
    ];

    /// <summary>
    /// §577: Custom, the person's own palette: Karpo's design under its values, which are always kept and never reset to
    /// anything — the first time it is worn, with none yet, Karpo's own.
    /// </summary>
    public static readonly LookPalette Custom = new(CustomId, KarpoLook.Paints(dark: false), KarpoLook.Paints(dark: true));

    /// <summary>§577: what the Palette row offers, in its order: the designed ones (<see cref="All"/>), then Custom.</summary>
    public static readonly LookPalette[] Choices = [.. All, Custom];

    /// <summary>The palette <paramref name="id"/>, a designed one or Custom; null for none.</summary>
    public static LookPalette? Of(string? id) => Choices.FirstOrDefault(palette => palette.Id == id);

    /// <summary>§577: the palette <paramref name="appearance"/> wears: the one it names, Karpo's own where it names none.</summary>
    public static LookPalette Worn(Appearance? appearance) => Of(appearance?.Palette) ?? All[0];

    /// <summary>§577: the palette <paramref name="config"/> wears.</summary>
    public static LookPalette Worn(Config config) => Worn(config.Appearance);

    /// <summary>
    /// §568: <paramref name="palette"/>'s design written out as values: each section's, each mode's colours in their half, its
    /// font; what it leaves at Karpo's own left out.
    /// </summary>
    public static PaletteValues? Values(LookPalette palette)
    {
        var values = new PaletteValues();
        foreach (var part in LookParts.All)
        {
            values[part] = LookValues.Of(palette.Light[part], palette.Dark[part], part);
        }
        return values.Normalized();
    }

    /// <summary>§577: <paramref name="palette"/> worn by <paramref name="config"/>, with the values it keeps; false where it is worn already.</summary>
    public static bool Wear(Config config, LookPalette palette)
    {
        if (Worn(config) == palette)
        {
            return false;
        }
        var appearance = config.Appearance ?? new Appearance();
        appearance.Palette = palette.Id == KarpoId ? null : palette.Id;
        config.Appearance = appearance.IsEmpty ? null : appearance;
        return true;
    }

    /// <summary>
    /// §577: whether <paramref name="config"/>'s values for <paramref name="palette"/> draw it otherwise than designed, in either
    /// mode: what its reset would undo. A value said that is its design's own changes nothing.
    /// </summary>
    public static bool Changed(Config config, LookPalette palette) =>
        config.Appearance?[palette.Id] is { } values
        && (Looks.Of(values, palette.Drawn(false), dark: false) != palette.Drawn(false) || Looks.Of(values, palette.Drawn(true), dark: true) != palette.Drawn(true));

    /// <summary>
    /// §577: <paramref name="palette"/>'s values gone, worn or not: it is drawn as designed again. Custom has no design to go
    /// back to: nothing. False where nothing changed.
    /// </summary>
    public static bool Reset(Config config, LookPalette palette)
    {
        if (!palette.Resets || config.Appearance is not { } appearance || appearance[palette.Id] is null)
        {
            return false;
        }
        appearance[palette.Id] = null;
        config.Appearance = appearance.IsEmpty ? null : appearance;
        return true;
    }
}
