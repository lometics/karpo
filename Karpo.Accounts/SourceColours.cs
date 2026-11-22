// The colour of each part of an account: every calendar, inbox and address book wears one, in the
// settings app's Accounts page and on the bar (docs\accounts.md, "Colours").
//
// "in the account settings you should be able to set colors for different calendars, email inboxes,
// or contact lists." (2026-10-12)
//
// A part's colour is, first that there is: the one the person picked (Account.Colours), the one the
// account gives it (Graph's hexColor or Outlook's named colour, CalDAV's calendar-color), else one of
// the palette's, by the part's place among its kind — the next the palette has that no account gives.
// So two inboxes never start alike, picking a colour for one part moves no other, and the dock and the
// settings app, which both work it out from the same accounts in the same order, say the same colour
// without either writing it down. Nothing is written until the person picks one.
//
// The palette is Outlook's calendar colours, as Fluent's shared colours have them: twelve, a shade deep
// enough that a dot or a stripe reads on a light bar and on a dark one.

using System.Globalization;
using Karpo.Core;

namespace Karpo.Accounts;

public static class SourceColours
{
    /// <summary>
    /// The colours to pick from, in the order a picker shows them, by hue: red, orange, gold, green,
    /// teal, blue, cornflower, purple, lilac, pink, brown, grey (Karpo.Core's, which a menu offers, §338).
    /// </summary>
    public static IReadOnlyList<string> Palette => PartColours.Palette;

    /// <summary>
    /// The order parts without a colour take the palette's in (indexes into <see cref="Palette"/>):
    /// hues far apart first — blue, teal, purple, orange, green, pink — so the first few never look alike.
    /// </summary>
    private static readonly int[] Order = [5, 4, 7, 1, 3, 9, 2, 0, 6, 10, 8, 11];

    /// <summary><c>#RRGGBB</c> in capitals for "#RGB", "#RRGGBB" or "#RRGGBBAA" (Apple's, its alpha left out); null for anything else.</summary>
    public static string? Normal(string? value)
    {
        var text = value?.Trim() ?? "";
        if (text.Length < 2 || text[0] != '#')
        {
            return null;
        }
        var hex = text[1..];
        if (hex.Length == 3)
        {
            hex = new string([hex[0], hex[0], hex[1], hex[1], hex[2], hex[2]]);
        }
        if (hex.Length is not (6 or 8) || !uint.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out _))
        {
            return null;
        }
        return "#" + hex[..6].ToUpperInvariant();
    }

    /// <summary>
    /// The colour of each of one kind's parts, in their order across the accounts, and what it would
    /// wear unpicked: the account's own where <paramref name="parts"/> gives one, else the palette's
    /// first that no account gives and no part before took — once the palette is all taken, round it
    /// again by place. A part the person picked a colour for wears it, and keeps its place in the
    /// palette's round all the same: picking one colour never moves another.
    /// </summary>
    /// <param name="parts">Each part's picked colour and the account's own, <c>#RRGGBB</c>; null where there is none.</param>
    public static IReadOnlyList<(string Colour, string Automatic)> Assign(IReadOnlyList<(string? Picked, string? Given)> parts)
    {
        var taken = new HashSet<string>(parts.Select(part => part.Given).OfType<string>(), StringComparer.OrdinalIgnoreCase);
        var found = new List<(string, string)>(parts.Count);
        var place = 0;
        foreach (var (picked, given) in parts)
        {
            var automatic = given;
            if (automatic is null)
            {
                automatic = Order.Select(index => Palette[index]).FirstOrDefault(each => !taken.Contains(each))
                    ?? Palette[Order[place % Order.Length]];
                taken.Add(automatic);
                place++;
            }
            found.Add((picked ?? automatic, automatic));
        }
        return found;
    }

    /// <summary>
    /// The colours of one kind's parts (each its account and its <see cref="SyncKeys"/> key, with the
    /// colour the account gives it, in the order the accounts were added and list them), keyed
    /// <see cref="Key"/>: what both apps draw.
    /// </summary>
    public static IReadOnlyDictionary<string, (string Colour, string Automatic)> Of(IEnumerable<(Account Account, string Key, string? Given)> parts)
    {
        var list = parts.ToList();
        var colours = Assign([.. list.Select(part => (part.Account.Colour(part.Key), Normal(part.Given)))]);
        var found = new Dictionary<string, (string, string)>(StringComparer.Ordinal);
        for (var at = 0; at < list.Count; at++)
        {
            found.TryAdd(Key(list[at].Account.Id, list[at].Key), colours[at]);
        }
        return found;
    }

    /// <summary>What <see cref="Of"/> keys a part by: its account's id and its <see cref="SyncKeys"/> key.</summary>
    public static string Key(string account, string key) => account + "|" + key;

    /// <summary>The red, green and blue of <c>#RRGGBB</c>; black for anything else.</summary>
    public static (byte R, byte G, byte B) Rgb(string? colour) =>
        Normal(colour) is { } hex && uint.TryParse(hex.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var rgb)
            ? ((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb)
            : ((byte)0, (byte)0, (byte)0);

    /// <summary><c>#RRGGBB</c> of a red, green and blue.</summary>
    public static string Hex(byte r, byte g, byte b) => $"#{r:X2}{g:X2}{b:X2}";
}
