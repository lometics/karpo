// Karpo's words, in the language Karpo speaks now.
//
// Every text Karpo writes itself is a key in Strings\en.json, translated in a file per language
// beside it (de.json, zh-Hans.json, pt-BR.json …). Karpo.Strings.Generator turns them into the class S at
// build time — S.SettingsLanguageHeader, S.DockVolumeLevel(level) — and into one table per
// language, so a text is read as one array element, with no lookup by name, no reflection and
// no CultureInfo: the dock runs as native AOT with InvariantGlobalization.
//
// Which table S reads is decided here, once for the process: Use swaps one reference, so a
// thread reading a text mid-switch gets the old language or the new one, never a mix of both
// within a text. Which language that is comes from the config (Config.Language) or Windows'
// own (Languages.cs). docs\widgets.md, "Karpo's words", says how to add a string.

namespace Karpo.Core;

public static partial class Strings
{
    /// <summary>The language S reads now: its place among the tables, its code, and its texts.</summary>
    private sealed class Active(int index, string code, string[] texts)
    {
        public int Index { get; } = index;

        public string Code { get; } = code;

        public string[] Texts { get; } = texts;
    }

    // English until somebody says otherwise. Made on first use rather than in a field initializer:
    // the generated half of this class has fields of its own, and C# does not order the
    // initializers of two files of one partial class.
    private static volatile Active? current;

    private static Active Now => current ??= new Active(0, Codes()[0], Table(0));

    /// <summary>The texts of the language now, by a key's place; S's properties read this.</summary>
    internal static string[] Current => Now.Texts;

    /// <summary>The language now by its place among <see cref="All"/>; S's methods pick their wording by it.</summary>
    internal static int Index => Now.Index;

    /// <summary>The language now: a code of <see cref="Languages.All"/> ("en", "de", "zh-Hans" …) there is a file for, or "qps-ploc".</summary>
    public static string Language => Now.Code;

    /// <summary>Every language there is a table for, English first and the pseudo-locale last.</summary>
    public static IReadOnlyList<string> All => Codes();

    /// <summary>Every key of en.json, in its order.</summary>
    public static IReadOnlyList<string> Keys => KeyList();

    /// <summary>Whether there is a table for <paramref name="language"/> (any case).</summary>
    public static bool Has(string? language) => Find(language) >= 0;

    /// <summary>
    /// Makes <paramref name="language"/> the one S reads; English for one there is no table for.
    /// True when that changed what S says. Everything already drawn keeps its words until it is
    /// drawn again: the dock rebuilds its bars, the settings window its pages.
    /// </summary>
    public static bool Use(string? language)
    {
        var index = Math.Max(0, Find(language));
        if (Now.Index == index)
        {
            return false;
        }
        current = new Active(index, Codes()[index], Table(index));
        return true;
    }

    /// <summary>
    /// What <paramref name="key"/> says in the language now: a key of en.json
    /// ("settings.language.header") or S's name for it ("SettingsLanguageHeader"). A text with
    /// places comes back as its template, places in braces. Null for a key there is none of.
    /// </summary>
    public static string? Lookup(string key) => Lookup(key, Language);

    /// <summary>What <paramref name="key"/> says in <paramref name="language"/>, English where there is no such language.</summary>
    public static string? Lookup(string key, string language)
    {
        var at = KeyIndex(key);
        return at < 0 ? null : Table(Math.Max(0, Find(language)))[at];
    }

    /// <summary>
    /// Whether <paramref name="count"/> takes a language's singular — the ".one" of a pair of keys
    /// rather than the ".other" — by its rule (<see cref="Languages.Plural"/>): one in English and
    /// German; nought and one in French; 1, 21, 31 … in Russian; never in Chinese, which has no
    /// plural and writes both alike.
    /// </summary>
    public static bool IsOne(long count) => Languages.IsOne(Language, count);

    private static int Find(string? language)
    {
        if (string.IsNullOrEmpty(language))
        {
            return -1;
        }
        var codes = Codes();
        for (var index = 0; index < codes.Length; index++)
        {
            if (string.Equals(codes[index], language, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }
        return -1;
    }

    private static Dictionary<string, int>? keyIndex;

    private static int KeyIndex(string key)
    {
        var map = keyIndex;
        if (map is null)
        {
            map = new Dictionary<string, int>(StringComparer.Ordinal);
            var keys = KeyList();
            var names = NameList();
            for (var index = 0; index < keys.Length; index++)
            {
                map[keys[index]] = index;
                map[names[index]] = index;
            }
            keyIndex = map;
        }
        return map.TryGetValue(key, out var at) ? at : -1;
    }

    // The generated half (Karpo.Strings.Generator): the codes, the keys, S's names for them, and
    // each language's texts.
    private static partial string[] Codes();

    private static partial string[] KeyList();

    private static partial string[] NameList();

    private static partial string[] Table(int language);
}
