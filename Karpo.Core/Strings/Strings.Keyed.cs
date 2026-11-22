// Texts a registry names by key rather than by S's member.
//
// A registry with a record per widget (WidgetInfos, Presets) keys its words by the record's id —
// core.widget.<id>.title, .blurb, .keywords — so a record holds no text at all, only the id it
// had already, and reads its words in the language Karpo speaks each time it is asked: a
// language change reaches every title with nothing to rebuild. The keys are checked by a test
// rather than the build (Karpo.Tests\Localization\RegistryWordsTests.cs): every record has its three.
//
// The search finds a widget by its English words as well as by the language's own (docs\widgets.md
// §36, "English search keywords stay"), so these also read a key in English.

namespace Karpo.Core;

public static partial class Strings
{
    /// <summary>What <paramref name="key"/> says in the language now; the key itself where there is none, so a missing key shows rather than blanks.</summary>
    public static string Text(string key) => Lookup(key) ?? key;

    /// <summary>What <paramref name="key"/> says in English.</summary>
    public static string English(string key) => Lookup(key, "en") ?? key;

    /// <summary>
    /// A key whose text is a comma list ("forecast, temperature, rain"), as its words: the language's
    /// own, then English's that are not among them, each once. In English, English's alone.
    /// </summary>
    public static string[] List(string key)
    {
        var words = new List<string>(16);
        Add(words, Lookup(key));
        if (!string.Equals(Language, "en", StringComparison.Ordinal))
        {
            Add(words, Lookup(key, "en"));
        }
        return [.. words];
    }

    /// <summary>What <paramref name="keys"/> say in English, where Karpo speaks another language; nothing in English, where they are the words already.</summary>
    public static IEnumerable<string> AlsoEnglish(params string[] keys) =>
        string.Equals(Language, "en", StringComparison.Ordinal) ? [] : keys.Select(English);

    /// <summary>The language's texts, each to its place among the keys, for <see cref="EnglishOf"/>: made once a language.</summary>
    private sealed record Places(int Language, Dictionary<string, int> Of);

    private static volatile Places? places;

    /// <summary>
    /// What <paramref name="text"/> — words Karpo shows in the language now, a key's whole text
    /// ("Wöchentlich") — says in English ("Weekly"), so a list's filter finds a row by its English
    /// too (Karpo.Dock\Flyout.Inline.cs); null in English, or for words no key has.
    /// </summary>
    public static string? EnglishOf(string text)
    {
        if (text.Length == 0 || string.Equals(Language, "en", StringComparison.Ordinal))
        {
            return null;
        }
        var now = Now;
        var made = places;
        if (made is null || made.Language != now.Index)
        {
            var of = new Dictionary<string, int>(now.Texts.Length, StringComparer.Ordinal);
            for (var at = 0; at < now.Texts.Length; at++)
            {
                of.TryAdd(now.Texts[at], at);
            }
            places = made = new Places(now.Index, of);
        }
        return made.Of.TryGetValue(text, out var place) && Table(0)[place] is var english && english != text ? english : null;
    }

    /// <summary>
    /// The commas a list is written with: the Latin one, Arabic's (Arabic, Urdu and Persian write
    /// "rss، atom"), and Chinese's and Japanese's ideographic and full-width ones.
    /// </summary>
    private static readonly char[] Commas = [',', '،', '、', '，'];

    /// <summary>A comma list's words (<see cref="Commas"/>, whichever the language writes), trimmed, none empty.</summary>
    public static string[] Words(string list) => list.Split(Commas, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static void Add(List<string> words, string? list)
    {
        if (list is null)
        {
            return;
        }
        foreach (var word in Words(list))
        {
            if (!words.Contains(word, StringComparer.Ordinal))
            {
                words.Add(word);
            }
        }
    }
}
