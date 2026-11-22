// menus: what a submenu's row is set to, at its end (2026-10-19, the person: "also show the set
// configuration value in the settings as well"; docs\widgets.md §228). Windows 11's own settings rows
// say what they are set to before their chevron, in the quieter colour: "Style      Graph  ›", "Time
// zone   Tokyo  ›"; a colour its swatch, an icon its glyph.
//
//  - **Where.** Right-aligned 8 DIP in from the chevron's box, upright in the row's middle (Flyout.Open);
//    on a row with no chevron and no tick's slot (a search's result, §240) at the row's very end.
//  - **The row is shared** (§315; the person, 2026-10-23: "the value display (UTC+X) sometimes is not
//    shown when the title is too wide ... the width should be shared by title and value"). §228 had the
//    name win, the value only what it left and none under 40 DIP, so a long name lost its value. Now
//    where both fit, both are whole; where not, the shorter keeps all it needs if that is within its
//    share — the value two fifths of the row, the name the rest — and the other has what is left; where
//    neither is that short, each has its share, both cut with an ellipsis (Share). A value is never
//    left out. Every row with a value at its end has this one rule: a submenu's, a search's result, a
//    place's. A swatch or a glyph is an icon's 16 DIP, always shown.
//  - **The menu's width** (Flyout.Width.cs) counts it whole, to the most a menu grows to, so a value is
//    cut only where the menu could grow no wider.

namespace Karpo.Dock;

/// <summary>
/// menus: what a row is set to, shown at its end before its chevron (Flyout.Values.cs): its
/// <paramref name="Text"/>, or an <paramref name="Icon"/> — a swatch (<see cref="FlyoutTile"/>, not a
/// cell) or a glyph. §411 (R4): <paramref name="Implied"/>, greyed: what applies where a line of text is
/// empty (a pin's name, "None").
/// </summary>
public sealed record FlyoutValue(string Text = "", FlyoutIcon? Icon = null, bool Implied = false, bool Whole = false)
{
    /// <summary>§556: the font its words are drawn in where it names one (a look's Font: the one set, or what it comes to); null for the row's own.</summary>
    public Karpo.Core.TextStyle? Face { get; init; }
}

public sealed partial class Flyout
{
    /// <summary>How far a value stands in from the chevron's box, in DIP.</summary>
    internal const float ValueGapDip = 8;

    /// <summary>Whether <paramref name="item"/> keeps the far slot at its end: a chevron's, or a tick's in a menu with ticks (<paramref name="anyCheck"/>).</summary>
    internal static bool FarSlot(FlyoutItem item, bool anyCheck) => (anyCheck && !ClearsCheckSlot(item)) || item.Children is not null;

    /// <summary>The value's share of a row whose name and value do not both fit (§315); the name's is the rest.</summary>
    internal const float ValueShare = 0.4f;

    /// <summary>
    /// How a row of <paramref name="room"/> px is shared by its name, needing <paramref name="words"/> px,
    /// and its value, needing <paramref name="natural"/> (§315): each whole where both fit; else the one
    /// within its share (<see cref="ValueShare"/>) whole and the other the rest; else each its share.
    /// </summary>
    internal static (float Words, float Value) Share(float room, float words, float natural)
    {
        room = MathF.Max(0, room);
        if (words + natural <= room)
        {
            return (words, natural);
        }
        var value = natural <= room * ValueShare ? natural
            : words <= room * (1 - ValueShare) ? room - words
            : room * ValueShare;
        value = MathF.Floor(value);
        return (room - value, value);
    }
}
