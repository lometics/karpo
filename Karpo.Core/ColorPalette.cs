// The custom colours' choices and their one write (2026-10-19, the person: "inline every setting ... into
// its edit menu"): what every colour row of a menu offers in its swatch grid — Customize's, a folder's and a
// widget's own (OptionPickers, Karpo.Dock\MenuPickers.cs) — is these.
//
// §690: one set of presets for every picker (Presets), under a line of its own of the recent colours. Before: which colours, by the tone (§405), §627 two lines of six — the picker's swatches stand in a column beside its mixer, six to
// a line, two lines of presets under one of recent colours (the person, 2026-11-09: "a maximum of one line of recent colors
// and two lines of color presets"); every other colour is the mixer's:
//
//   * The background gets backgrounds: six neutrals over six tints, one hue each (blue, teal, green, brown, red, purple),
//     dark for the dark mode and pale for the light one, so text in the mode's own colour stays readable over every one.
//     Windows' accent colours would be shouting as a bar.
//   * The accent gets twelve of Windows' own accent colours round the wheel: red to teal, then blue to pink and grey.
//   * The text and an outline get the other mode's neutrals — light ones over a dark bar, dark over a light one — over six
//     of Windows' colours.
//   * §623: the backdrop while a folder is open gets both modes' neutrals, the dark line first (to darken the screen,
//     say); picked from none at half its opacity (ToneNames.BackdropAlpha), never a screen made opaque at once.
//   * §649: an attached folder's ground the backgrounds, as the bar it grows out of.
//
// Every one opaque: how much of it shows through is the opacity the colour row keeps (§459), whichever is picked.
//
// Recent colours are the ones last mixed in a custom colour dialog, newest first, one line's worth (§627: six, the oldest
// dropped past it), shared by every tone, every level and both modes as Windows shares its own (Config.RecentColors).
// §405 to §626 had 24 backgrounds and Windows' 48 accent colours, eight a line, and eight recent colours.

namespace Karpo.Core;

/// <summary>The colours a custom colour offers, where a picker starts, and how a pick is written.</summary>
public static class ColorPalette
{
    /// <summary>§627: a line of a colour's swatches in its picker: six, its swatches' column beside its mixer.</summary>
    public const int Columns = 6;

    /// <summary>§627: how many lines of presets a picker shows: two.</summary>
    public const int Lines = 2;

    /// <summary>How many recent colours are kept: one line.</summary>
    public const int MostRecent = Columns;

    /// <summary>Windows' own blue (its default accent): what a picker mixes from where nothing is set.</summary>
    public static readonly Argb Blue = new(0xFF, 0x00, 0x78, 0xD4);

    /// <summary>
    /// §690 (the person, 2026-11-12: "there should be one row that is filled with the most recent color choices. but other than
    /// that the color pickers everywhere should have the same preset colors"): the presets of every colour picker — the
    /// Appearance's, a widget's and a folder's, an account's calendar's, a holiday calendar's —, two lines of six: Windows' red,
    /// orange, gold, green, teal and blue; its purple, pink, a brown, its grey, and the dark and the light taskbar's own grounds.
    /// </summary>
    public static readonly IReadOnlyList<Argb> Presets = Opaque(
        0xE81123, 0xF7630C, 0xFFB900, 0x10893E, 0x0099BC, 0x0078D4,
        0x8764B8, 0xE3008C, 0x8E562E, 0x767676, 0x202020, 0xF3F3F3);

    /// <summary>§690: what a preset is called, its tooltip and a screen reader's name ("Red"); null for one without a name (a ground, say its hex).</summary>
    public static string? NameOf(Argb color) => (color with { A = 255 }).ToString().ToUpperInvariant() switch
    {
        "#FFE81123" or "#E81123" => S.SettingsAccountsColorRed,
        "#FFF7630C" or "#F7630C" => S.SettingsAccountsColorOrange,
        "#FFFFB900" or "#FFB900" => S.SettingsAccountsColorGold,
        "#FF10893E" or "#10893E" => S.SettingsAccountsColorGreen,
        "#FF0099BC" or "#0099BC" => S.SettingsAccountsColorTeal,
        "#FF0078D4" or "#0078D4" => S.SettingsAccountsColorBlue,
        "#FF8764B8" or "#8764B8" => S.SettingsAccountsColorPurple,
        "#FFE3008C" or "#E3008C" => S.SettingsAccountsColorPink,
        "#FF8E562E" or "#8E562E" => S.SettingsAccountsColorBrown,
        "#FF767676" or "#767676" => S.SettingsAccountsColorGray,
        _ => null,
    };

    /// <summary>§690: the group a picker's presets stand in, a line of their own under the recent colours (MenuPickers.Tiles rules a group off).</summary>
    public const string PresetsGroup = "presets";

    /// <summary>
    /// What Karpo draws for <paramref name="tone"/> on a taskbar that reads in one mode where nothing sets it
    /// (<see cref="ToneRules"/>; §547 Karpo's own, never Windows'): what an Inherited row shows it inherits at the top of the
    /// chain — the accent Karpo's blue in its shade for that mode (<see cref="KarpoLook.AccentOn"/>). None for an outline and
    /// a pattern's colour, which are derived where they are drawn.
    /// </summary>
    public static Argb? Unset(Tone tone, bool dark) => tone switch
    {
        Tone.Background => ToneRules.WindowsBackground(dark),
        Tone.Text => ToneRules.WindowsText(dark),
        Tone.Accent => KarpoLook.AccentOn(dark),
        _ => null,
    };

    /// <summary>The recent colours <paramref name="config"/> keeps, newest first: each once, read or left out, at most a line.</summary>
    public static IReadOnlyList<Argb> Recent(Config config) =>
        [.. (config.RecentColors ?? []).Select(Argb.Parse).OfType<Argb>().Select(color => color with { A = 255 }).Distinct().Take(MostRecent)];

    /// <summary>
    /// <paramref name="color"/>, mixed in a custom colour dialog, first among the recent colours:
    /// moved there where it was one already, the oldest dropped past a line. False when it already led.
    /// </summary>
    public static bool Remember(Config config, Argb color)
    {
        var before = Recent(config);
        if (before.Count > 0 && before[0] == color && config.RecentColors?.Count == before.Count)
        {
            return false;
        }
        config.RecentColors = [.. new[] { color }.Concat(before.Where(one => one != color)).Take(MostRecent).Select(one => one.ToString())];
        return true;
    }

    private static Argb[] Opaque(params uint[] colors) =>
        [.. colors.Select(rgb => new Argb(0xFF, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb))];
}
