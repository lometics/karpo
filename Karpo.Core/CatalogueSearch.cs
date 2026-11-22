// edit: what the add list's search field finds (docs\widgets.md §7). The person: "the add widget
// dialog should have a search field where you can search for widgets". This only says whether a
// row matches; the list lists the ones that do (Karpo.Dock\AddList.cs, Results).
//
// A row is found by everything its WidgetInfo calls it — its name, its one-line description and
// its search words (WidgetInfo.Keywords) — so "internet" finds Wi-Fi and "disk" finds Drive
// without a word list of the add list's own. Every word typed has to be found, anywhere inside a
// word of one of those ("vol" finds Volume, "play" Now playing), so a second word narrows rather
// than widens; and a capital or an accent never decides it ("cafe" finds "Café", "ÉCRAN" finds
// "écran").
//
// In another language than English a row is found by its English words as well as its own:
// "Clock" finds "Uhr" in German, so somebody who knows a widget by its English name, or Windows'
// English one, still finds it (docs\widgets.md §36).
//
// The accents come off by a table of our own rather than string.Normalize: the dock runs in
// invariant globalization (Karpo.Dock.csproj), where Normalize leaves a string as it is and the
// culture's accent-blind comparisons are plain ordinal ones.

namespace Karpo.Core;

/// <summary>Whether a widget of the add list matches what was typed into its search field.</summary>
public static class CatalogueSearch
{
    /// <summary>The words typed, in the order they were typed and folded (<see cref="Fold"/>); empty for a query that says nothing.</summary>
    public static string[] Words(string? query) =>
        Fold(query ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// Whether a widget called <paramref name="title"/> and found by <paramref name="words"/> (its
    /// description and its search words) matches <paramref name="query"/>: every word of the query is
    /// somewhere in one of them. A query that says nothing matches everything, which is what an
    /// empty field shows.
    /// </summary>
    public static bool Matches(string? query, string title, IEnumerable<string?>? words)
    {
        var wanted = Words(query);
        if (wanted.Length == 0)
        {
            return true;
        }
        var said = new List<string>(8) { Fold(title) };
        if (words is not null)
        {
            foreach (var word in words)
            {
                if (!string.IsNullOrEmpty(word))
                {
                    said.Add(Fold(word));
                }
            }
        }
        foreach (var word in wanted)
        {
            if (!said.Exists(text => text.Contains(word, StringComparison.Ordinal)))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>Whether <paramref name="row"/> matches <paramref name="query"/>, by its name, its description and its search words.</summary>
    public static bool Matches(string? query, CatalogueEntry row) =>
        Matches(query, row.Title, WordsOf(row));

    /// <summary>
    /// What a row is found by besides its name: its description, then its search words, then — where
    /// Karpo speaks another language — its English name and description (its English search words
    /// are among its search words already).
    /// </summary>
    public static IReadOnlyList<string> WordsOf(CatalogueEntry row) => [row.Blurb, .. row.Keywords, .. row.English];

    /// <summary>
    /// <paramref name="text"/> as the search compares it: small letters, the accents off the Latin
    /// letters that carry them, "ß", "æ" and "œ" spelt out, and combining marks dropped.
    /// </summary>
    public static string Fold(string text)
    {
        var folded = new System.Text.StringBuilder(text.Length);
        foreach (var c in text)
        {
            var small = char.ToLowerInvariant(c);
            // Combining Diacritical Marks: an accent typed after its letter.
            if (small is >= '̀' and <= 'ͯ')
            {
                continue;
            }
            switch (small)
            {
                case 'ß':
                    folded.Append("ss");
                    continue;
                case 'æ':
                    folded.Append("ae");
                    continue;
                case 'œ':
                    folded.Append("oe");
                    continue;
            }
            folded.Append(Plain.TryGetValue(small, out var plain) ? plain : small);
        }
        return folded.ToString();
    }

    // The Latin letters with accents, each with the letter under it: Latin-1, Latin Extended-A and
    // the accented letters of Latin Extended-B and Additional that languages written with them use.
    private static readonly Dictionary<char, char> Plain = Table(
        ('a', "àáâãäåāăąǎǟǡǻȁȃȧạảấầẩẫậắằẳẵặ"),
        ('c', "çćĉċč"),
        ('d', "ďđ"),
        ('e', "èéêëēĕėęěȅȇȩẹẻẽếềểễệ"),
        ('g', "ĝğġģǧǵ"),
        ('h', "ĥħ"),
        ('i', "ìíîïĩīĭįıǐȉȋỉị"),
        ('j', "ĵ"),
        ('k', "ķǩ"),
        ('l', "ĺļľŀł"),
        ('n', "ñńņňǹ"),
        ('o', "òóôõöøōŏőơǒǫǭǿȍȏȫȭȯȱọỏốồổỗộớờởỡợ"),
        ('r', "ŕŗřȑȓ"),
        ('s', "śŝşšș"),
        ('t', "ţťŧț"),
        ('u', "ùúûüũūŭůűųưǔǖǘǚǜȕȗụủứừửữự"),
        ('w', "ŵẁẃẅ"),
        ('y', "ýÿŷȳỳỵỷỹ"),
        ('z', "źżž"));

    private static Dictionary<char, char> Table(params (char Plain, string Marked)[] letters)
    {
        var table = new Dictionary<char, char>();
        foreach (var (plain, marked) in letters)
        {
            foreach (var letter in marked)
            {
                table[letter] = plain;
            }
        }
        return table;
    }
}
