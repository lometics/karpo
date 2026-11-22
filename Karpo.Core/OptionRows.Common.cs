// edit, §528: the settings several widgets have in common (the person, 2026-11-07: "i should be able to select multiple
// widgets and edit their common properties like look together. this should be implemented generically so that i could also
// select mutliple of the same widget and change their propertier together"; docs\widgets.md §528). One rule for every kind,
// read from the one row model (OptionRows, LookRows) and nothing a kind says of its own:
//
//  - **The same setting** is a row of one key in every widget's rows, of one kind and one shape: a choice of the same choices
//    (the one that says what it comes to unset, "Default (…)", §518, may say another thing in each), a colour's or an icon's
//    tiles the same past their first, a number in one unit, a search of one field, a link to one place. A level's rows are
//    its rows in common, the level gone where none are. What is the person's own (a line's notes, the accounts, a list put
//    in order) is no setting several widgets share, nor is a note. Two clocks so share their whole page; a clock and the
//    weather the rows they both have — any setting both read by one key and one shape (a style of other choices is another
//    setting under the same key). §567: no widget has a look of its own to share.
//  - **Its value** is theirs where they agree; where they do not it is mixed (OptionRow.Mixed): a toggle off and mixed, a
//    choice of none with "Mixed" at its end, a number, a time, a text and a colour saying "Mixed".
//    §529: a mixed row holds each one's own (OptionRow.Each), so the settings panel may open it into each one's value, set on
//    that one alone.
//  - **Setting it** sets every one (each row's own Set, SetText, Pick, SetColor called in turn): a toggle mixed or off turns
//    them all on, a number steps them all to the first's value stepped, a choice, a colour, a text, a reset are each the same
//    for all. The caller writes them into one config and saves it once: one step of edit mode's undo (EditHistory).

namespace Karpo.Core;

/// <summary>§528: a row standing for the same setting of several widgets.</summary>
public sealed partial record OptionRow
{
    /// <summary>§528: the widgets it stands for do not agree on its value; it says "Mixed" (see OptionRows.Common.cs).</summary>
    public bool Mixed { get; init; }

    /// <summary>§529: a <see cref="Mixed"/> row's widgets' own rows, in the selection's order: each one's value, set on it alone.</summary>
    public IReadOnlyList<OptionRow> Each { get; init; } = [];
}

/// <summary>§528: the settings several widgets have in common; see the top of the file.</summary>
public static partial class OptionRows
{
    /// <summary>
    /// The rows every one of <paramref name="lists"/> (a widget's rows each) has (see the top of the file), in the first's
    /// order, each the setting of them all; a heading with nothing of its own under it gone. The one list itself for one.
    /// </summary>
    public static IReadOnlyList<OptionRow> Common(IReadOnlyList<IReadOnlyList<OptionRow>> lists)
    {
        if (lists.Count == 0)
        {
            return [];
        }
        if (lists.Count == 1)
        {
            return lists[0];
        }
        List<OptionRow> rows = [];
        foreach (var row in lists[0])
        {
            if (Common([.. lists.Select(list => list.FirstOrDefault(each => each.Key == row.Key))]) is { } common)
            {
                rows.Add(common);
            }
        }
        // A heading (or a rule, a heading of no words) over nothing: none, as a page shows none.
        return [.. rows.Where((row, at) => row.Kind != OptionRowKind.Heading || (at + 1 < rows.Count && rows[at + 1].Kind != OptionRowKind.Heading))];
    }

    /// <summary>
    /// The one setting <paramref name="rows"/> (a row of each widget, by one key) are, as a row: their value, mixed where they
    /// do not agree, setting every one; null where one has none or they are not the same setting (see the top of the file).
    /// The row itself for one.
    /// </summary>
    public static OptionRow? Common(IReadOnlyList<OptionRow?> rows)
    {
        if (rows.Count == 0 || rows[0] is not { } first)
        {
            return null;
        }
        if (rows.Count == 1)
        {
            return first;
        }
        if (rows.Any(row => row is null || !Alike(first, row)))
        {
            return null;
        }
        var all = rows.Select(row => row!).ToList();
        bool Differ<T>(Func<OptionRow, T> value) => all.Any(row => !EqualityComparer<T>.Default.Equals(value(row), value(first)));
        T? Agreed<T>(Func<OptionRow, T?> value) => Differ(value) ? default : value(first);
        var merged = first with
        {
            Set = Each(all, row => row.Set),
            SetText = Each(all, row => row.SetText),
            Pick = Each(all, row => row.Pick),
            SetColor = all.All(row => row.SetColor is not null) ? (color, kept) => all.Aggregate(false, (changed, row) => row.SetColor!(color, kept) | changed) : null,
            Move = null,
            Colour = first.Colour is null ? null : Common([.. all.Select(row => row.Colour)]),
            // An action (Reset all) may be done where any of them has something to do; anything else where all may.
            Enabled = first.Kind == OptionRowKind.Action ? all.Any(row => row.Enabled) : all.All(row => row.Enabled),
            Why = Agreed(row => row.Why),
            Detail = Agreed(row => row.Detail),
            Summary = Agreed(row => row.Summary),
            GroupSummary = Agreed(row => row.GroupSummary),
            ValuePicture = Agreed(row => row.ValuePicture),
            Choices = first.Kind == OptionRowKind.Choice ? Joined(all, row => row.Choices) : first.Choices,
            Tiles = first.Kind is OptionRowKind.Color or OptionRowKind.Glyph && first.Tiles.Count > 0 ? [Tile(all), .. first.Tiles.Skip(1)] : first.Tiles,
        };
        var mixed = first.Kind switch
        {
            OptionRowKind.Toggle => Differ(row => row.On),
            OptionRowKind.Choice => Differ(row => row.Chosen),
            OptionRowKind.Number or OptionRowKind.Time or OptionRowKind.Duration => Differ(row => row.Value),
            OptionRowKind.Color => Differ(row => row.Chosen) || Differ(row => row.Color),
            OptionRowKind.Text or OptionRowKind.Search or OptionRowKind.Glyph or OptionRowKind.Target => Differ(row => row.Text),
            _ => false,
        };
        merged = first.Kind switch
        {
            // The rows of a level they all have; none in common, no level.
            OptionRowKind.Level => Common([.. all.Select(row => row.Level)]) is { Count: > 0 } level
                ? merged with { Level = level, Mixed = Differ(row => row.Summary), Summary = Differ(row => row.Summary) ? S.SettingsValueMixed : first.Summary }
                : null,
            // A number within the range every one takes.
            OptionRowKind.Number or OptionRowKind.Duration => all.Max(row => row.Least) is var least && all.Min(row => row.Most) is var most && least <= most
                ? merged with { Least = least, Most = most }
                : null,
            _ => merged,
        };
        if (merged is null || !mixed)
        {
            return merged;
        }
        var said = S.SettingsValueMixed;
        // §529: each one's own row with it, so the panel may show each one's value.
        merged = merged with { Mixed = true, Each = all };
        return first.Kind switch
        {
            OptionRowKind.Toggle => merged with { On = false },
            OptionRowKind.Choice => merged with { Chosen = -1, Summary = said },
            // Its box empty, "Mixed" in it; its stepper saying so.
            OptionRowKind.Number => merged with { Said = said, Text = "", Placeholder = said },
            OptionRowKind.Color => merged with { Chosen = -1, Color = null },
            OptionRowKind.Text => merged with { Text = "", Placeholder = said, Empty = said, Summary = first.Masked ? null : said },
            OptionRowKind.Search => merged with { Text = "", Empty = said, Summary = said },
            // An icon's field keeps its own words (what it filters by); none of theirs is in force.
            OptionRowKind.Glyph => merged with { Text = "", Chosen = -1 },
            OptionRowKind.Target => merged with { Text = "" },
            _ => merged,
        };
    }

    /// <summary>
    /// Whether <paramref name="row"/> is the same setting as <paramref name="first"/> (see the top of the file): one key, one
    /// kind, one shape; never the person's own things, a list put in order, a note.
    /// </summary>
    private static bool Alike(OptionRow first, OptionRow? row) =>
        row is not null && row.Key == first.Key && row.Kind == first.Kind
        && first.Owned is null && row.Owned is null && !first.Ordered && !row.Ordered && first.Move is null && row.Move is null
        && first.Kind is not (OptionRowKind.Things or OptionRowKind.Note)
        && row.Inline == first.Inline && row.Segmented == first.Segmented && row.Within == first.Within && row.Group == first.Group
        && (row.Colour is null) == (first.Colour is null)
        && first.Kind switch
        {
            OptionRowKind.Choice => SameWords(first.Choices, row.Choices),
            OptionRowKind.Color or OptionRowKind.Glyph => row.Tiles.Count == first.Tiles.Count
                && row.Tiles.Skip(1).Zip(first.Tiles.Skip(1)).All(pair => pair.First.Color == pair.Second.Color && pair.First.Glyph == pair.Second.Glyph),
            OptionRowKind.Number => row.Unit == first.Unit,
            OptionRowKind.Search or OptionRowKind.Text => row.Field == first.Field && row.Masked == first.Masked,
            OptionRowKind.Link => row.Link == first.Link,
            OptionRowKind.Heading => row.Title == first.Title,
            OptionRowKind.Level => row.Form == first.Form,
            _ => true,
        };

    /// <summary>
    /// Whether <paramref name="a"/> and <paramref name="b"/> are the same choices: word for word, but where each says what
    /// it comes to unset (§518: "Default (…)"), which a widget of another layout, another section, says of its own.
    /// </summary>
    private static bool SameWords(IReadOnlyList<string> a, IReadOnlyList<string> b) =>
        a.Count == b.Count && a.Zip(b).All(pair => pair.First == pair.Second || (SaysDefault(pair.First) && SaysDefault(pair.Second)));

    /// <summary>§518: whether <paramref name="words"/> are a "Default (…)", in the words Karpo speaks now.</summary>
    private static bool SaysDefault(string words)
    {
        var shape = S.SettingsValueDefault("\u0001");
        var at = shape.IndexOf('\u0001', StringComparison.Ordinal);
        return at >= 0 && words.Length >= shape.Length - 1
            && words.StartsWith(shape[..at], StringComparison.Ordinal) && words.EndsWith(shape[(at + 1)..], StringComparison.Ordinal);
    }

    /// <summary>The choices' words as all say them; one they say apart (each its "Default (…)") "Default (Mixed)".</summary>
    private static IReadOnlyList<string> Joined(IReadOnlyList<OptionRow> all, Func<OptionRow, IReadOnlyList<string>> words)
    {
        var first = words(all[0]);
        return [.. first.Select((word, at) => all.All(row => words(row)[at] == word) ? word : S.SettingsValueDefault(S.SettingsValueMixed))];
    }

    /// <summary>A colour's or an icon's first tile, its plain one ("Default (…)"), as all say it: "Default (Mixed)" where they do not.</summary>
    private static OptionTile Tile(IReadOnlyList<OptionRow> all)
    {
        var first = all[0].Tiles[0];
        return all.All(row => row.Tiles[0].Name == first.Name && row.Tiles[0].Color == first.Color) ? first
            : first with { Name = S.SettingsValueDefault(S.SettingsValueMixed), Color = all.All(row => row.Tiles[0].Color == first.Color) ? first.Color : null };
    }

    /// <summary>The write of every one of <paramref name="all"/> (<paramref name="of"/>) in turn, each called; null where one has none.</summary>
    private static Func<T, bool>? Each<T>(IReadOnlyList<OptionRow> all, Func<OptionRow, Func<T, bool>?> of)
    {
        var writes = all.Select(of).ToList();
        if (writes.Any(write => write is null))
        {
            return null;
        }
        return value => writes.Aggregate(false, (changed, write) => write!(value) | changed);
    }
}
