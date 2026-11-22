// The colours a calendar, an inbox, an address book and the public holidays wear, as a menu offers them
// (docs\widgets.md §338):
// "Default (Blue)" — what the part wears while none is picked (§518): the account's own colour, else the palette's by
// its place — then Outlook's twelve calendar colours (Karpo.Accounts' SourceColours works out the
// automatic one from the same palette), and a colour of one's own mixed in the picker (§523), opaque, written as it is
// let go (an account's own colours are its service's, no look to take while it is dragged). Each written as the page
// writes it, "#RRGGBB", and null for the default.

using System.Globalization;

namespace Karpo.Core;

/// <summary>The parts' colours and the one row that picks one; see the top of the file.</summary>
public static class PartColours
{
    /// <summary>
    /// The colours to pick from, in the order a picker shows them, by hue: red, orange, gold, green,
    /// teal, blue, cornflower, purple, lilac, pink, brown, grey.
    /// </summary>
    public static readonly IReadOnlyList<string> Palette =
    [
        "#D13438", "#CA5010", "#C19C00", "#498205", "#038387", "#3A96DD",
        "#4F6BED", "#8764B8", "#B146C2", "#E3008C", "#8E562E", "#7A7574",
    ];

    /// <summary>A colour in words, for a tooltip and a screen reader: the palette's name ("Blue"), else its <c>#RRGGBB</c>.</summary>
    public static string Name(string colour)
    {
        var at = -1;
        for (var index = 0; index < Palette.Count; index++)
        {
            if (string.Equals(Palette[index], colour, StringComparison.OrdinalIgnoreCase))
            {
                at = index;
            }
        }
        return at switch
        {
            0 => S.SettingsAccountsColorRed,
            1 => S.SettingsAccountsColorOrange,
            2 => S.SettingsAccountsColorGold,
            3 => S.SettingsAccountsColorGreen,
            4 => S.SettingsAccountsColorTeal,
            5 => S.SettingsAccountsColorBlue,
            6 => S.SettingsAccountsColorCornflower,
            7 => S.SettingsAccountsColorPurple,
            8 => S.SettingsAccountsColorLilac,
            9 => S.SettingsAccountsColorPink,
            10 => S.SettingsAccountsColorBrown,
            11 => S.SettingsAccountsColorGray,
            _ => Hex(colour) ?? colour,
        };
    }

    /// <summary><c>#RRGGBB</c> in capitals for "#RRGGBB" or "#AARRGGBB" (its alpha left out); null for anything else.</summary>
    public static string? Hex(string? colour) => Argb.Parse(colour) is { } parsed ? Hex(parsed) : null;

    /// <summary><paramref name="colour"/> as <c>#RRGGBB</c>, its alpha left out.</summary>
    public static string Hex(Argb colour) => string.Create(CultureInfo.InvariantCulture, $"#{colour.R:X2}{colour.G:X2}{colour.B:X2}");

    /// <summary>
    /// A part's colour as a menu's colour row, keyed <paramref name="key"/> and named <paramref name="title"/>:
    /// "Default (…)" (<paramref name="automatic"/>, what it wears unpicked, §518), then the palette; <paramref name="picked"/>
    /// the one the person picked, null for none; <paramref name="write"/> keeps a pick, null for the default.
    /// </summary>
    public static OptionRow Row(string key, string title, string? picked, string automatic, Func<string?, bool> write)
    {
        var now = Hex(picked);
        List<OptionTile> tiles = [new(S.SettingsValueDefault(Name(automatic)), Glyph: OptionGlyphs.Automatic, Plain: true)];
        // §690: the presets every colour picker has, a line under Default's.
        tiles.AddRange(ColorPalette.Presets.Select(colour => new OptionTile(ColorPalette.NameOf(colour) ?? Hex(colour), colour, Group: ColorPalette.PresetsGroup)));
        return new OptionRow(key, OptionRowKind.Color, title)
        {
            Glyph = OptionGlyphs.Palette,
            Slot = OptionSlot.Look,
            Tiles = tiles,
            Chosen = now is null ? 0 : tiles.FindIndex(tile => tile.Color is { } colour && Hex(colour) == now),
            // What it wears at the row's end, picked or not: a swatch of it.
            Color = Argb.Parse(now ?? automatic),
            Set = at => at >= 0 && at < tiles.Count && write(tiles[at].Color is { } colour ? Hex(colour) : null),
            // §523: opaque, written once kept; a live look while it is dragged writes nothing (an account's colours are its own).
            SetColor = (mixed, kept) => kept && write(Hex(mixed)),
        };
    }
}
