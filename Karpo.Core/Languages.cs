// Which language Karpo speaks.
//
// Config.Language says, where somebody chose one in Settings' Localization page; null — the
// default — follows Windows' display language, as every app on Windows does. Windows' list of
// preferred languages is read by P/Invoke (GetUserPreferredUILanguages) rather than through
// CultureInfo, which the dock doesn't have (InvariantGlobalization), and the first one Karpo has a
// table for is the one: de-CH is German, zh-CN and zh-SG are Simplified Chinese, zh-TW and zh-HK
// Traditional, pt-PT Brazilian Portuguese, and a language Karpo has no table for passes on to the
// next, then English.
//
// All is every language Karpo knows how to speak: its name in itself, the tag Windows knows it
// by, its plural rule. Which of them Karpo offers is the files' to say: a language is offered once
// Karpo.Core\Strings\<code>.json is there (Strings.Has), so a translation lands by adding its file.
// Arabic, Urdu and Persian read right to left (Known.RightToLeft): the settings window flows right to left,
// and the dock lays their words out right to left and mirrors its menus and a folder's lines
// (docs\widgets.md §36, "Right to left"). The bar itself keeps its sections where they are.
//
// Text Windows provides — app names, device names, the shell's menus — stays in Windows' own
// language in the dock. The settings app asks WinUI for Karpo's (SetPreferred), so its own
// controls' words match the page around them.

using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace Karpo.Core;

public static partial class Languages
{
    /// <summary>
    /// When a language takes the ".one" of a pair of keys: <see cref="One"/> for one alone (English,
    /// German), <see cref="NoughtAndOne"/> for nought and one too (French, Brazilian Portuguese),
    /// <see cref="EndsInOne"/> for 1, 21, 31 … but not 11 (Russian, Ukrainian), <see cref="Never"/> for a language
    /// that writes both alike (Chinese, Japanese, Korean, Indonesian, Malay, Thai). Russian's and Polish's "few"
    /// (2–4) has no key of its own: it takes ".other".
    /// </summary>
    public enum Plural
    {
        One,
        NoughtAndOne,
        EndsInOne,
        Never,
    }

    /// <summary>
    /// A language Karpo knows: its code (its file's name), its name in itself, the tag Windows knows
    /// it by, its plural rule, and whether it reads right to left.
    /// </summary>
    public sealed record Known(string Code, string Name, string Tag, Plural Plural, bool RightToLeft = false);

    /// <summary>
    /// Every language Karpo knows, in the order Settings lists them: the first five, then the rest
    /// by how many people use them. One is offered when its file is there (<see cref="Offered"/>).
    /// </summary>
    public static IReadOnlyList<Known> All { get; } =
    [
        new("en", "English", "en-US", Plural.One),
        new("de", "Deutsch", "de-DE", Plural.One),
        new("fr", "Français", "fr-FR", Plural.NoughtAndOne),
        new("es", "Español", "es-ES", Plural.One),
        new("zh-Hans", "简体中文", "zh-CN", Plural.Never),
        new("pt-BR", "Português (Brasil)", "pt-BR", Plural.NoughtAndOne), // not-ui: a name, the same in every language
        new("ja", "日本語", "ja-JP", Plural.Never),
        new("ru", "Русский", "ru-RU", Plural.EndsInOne),
        new("ko", "한국어", "ko-KR", Plural.Never),
        new("it", "Italiano", "it-IT", Plural.One),
        new("zh-Hant", "繁體中文", "zh-TW", Plural.Never),
        new("pl", "Polski", "pl-PL", Plural.One),
        new("tr", "Türkçe", "tr-TR", Plural.One),
        new("nl", "Nederlands", "nl-NL", Plural.One),
        new("id", "Bahasa Indonesia", "id-ID", Plural.Never), // not-ui: a name, the same in every language
        // Hindi and Bengali take the singular for nought too, as CLDR has it; Arabic's dual and its
        // few and many have no keys of their own and take ".other". Bengali is laid out as India's
        // (bn-IN): Bangladesh's default digits are Bengali ones, wider than the Latin figures every
        // width here is measured on.
        new("hi", "हिन्दी", "hi-IN", Plural.NoughtAndOne),
        new("ar", "العربية", "ar-SA", Plural.One, RightToLeft: true),
        new("bn", "বাংলা", "bn-IN", Plural.NoughtAndOne),
        new("ur", "اردو", "ur-PK", Plural.One, RightToLeft: true),
        new("mr", "मराठी", "mr-IN", Plural.One),
        new("vi", "Tiếng Việt", "vi-VN", Plural.Never), // not-ui: a name, the same in every language
        // Persian takes the singular for nought too (CLDR); Ukrainian counts as Russian does, its
        // few taking ".other"; Czech's few and many, and Romanian's few, take ".other" too.
        new("te", "తెలుగు", "te-IN", Plural.One),
        new("ta", "தமிழ்", "ta-IN", Plural.One),
        new("fa", "فارسی", "fa-IR", Plural.NoughtAndOne, RightToLeft: true),
        new("ms", "Bahasa Melayu", "ms-MY", Plural.Never), // not-ui: a name, the same in every language
        new("th", "ไทย", "th-TH", Plural.Never),
        new("uk", "Українська", "uk-UA", Plural.EndsInOne),
        new("ro", "Română", "ro-RO", Plural.One),
        new("el", "Ελληνικά", "el-GR", Plural.One),
        new("cs", "Čeština", "cs-CZ", Plural.One),
        // The ten that came after: Punjabi (Gurmukhi), Gujarati, Kannada and Amharic take the singular
        // for nought too (CLDR); Filipino words its one and other alike, so its rule never shows;
        // Yoruba and Burmese write both alike.
        new("sw", "Kiswahili", "sw-KE", Plural.One), // not-ui: a name, the same in every language
        new("ha", "Hausa", "ha-NG", Plural.One), // not-ui: a name, the same in every language
        new("pa", "ਪੰਜਾਬੀ", "pa-IN", Plural.NoughtAndOne),
        new("fil", "Filipino", "fil-PH", Plural.One), // not-ui: a name, the same in every language
        new("gu", "ગુજરાતી", "gu-IN", Plural.NoughtAndOne),
        new("kn", "ಕನ್ನಡ", "kn-IN", Plural.NoughtAndOne),
        new("ml", "മലയാളം", "ml-IN", Plural.One),
        new("am", "አማርኛ", "am-ET", Plural.NoughtAndOne),
        new("yo", "Yorùbá", "yo-NG", Plural.Never), // not-ui: a name, the same in every language
        new("my", "မြန်မာ", "my-MM", Plural.Never),
    ];

    /// <summary>
    /// The languages Settings offers, in its list's order: those of <see cref="All"/> there is a
    /// table for. The pseudo-locale is left out: it is for pictures.
    /// </summary>
    public static IReadOnlyList<string> Offered { get; } = [.. All.Select(known => known.Code).Where(Strings.Has)];

    /// <summary>The hidden pseudo-locale (Strings.All has it): English, accented and longer, for pictures and smoke runs.</summary>
    public const string Pseudo = "qps-ploc";

    /// <summary>
    /// What Settings' language box lists, by code: null (Windows default) first, then
    /// <see cref="Offered"/>, then <paramref name="chosen"/> where it is none of them — the
    /// pseudo-locale, or a language a newer Karpo offers —, so the box shows what the file says.
    /// </summary>
    public static List<string?> Choices(string? chosen)
    {
        var codes = new List<string?> { null };
        codes.AddRange(Offered);
        if (!string.IsNullOrWhiteSpace(chosen) && !codes.Exists(code => string.Equals(code, chosen, StringComparison.OrdinalIgnoreCase)))
        {
            codes.Add(chosen);
        }
        return codes;
    }

    /// <summary>Where <paramref name="chosen"/> stands in <see cref="Choices"/> (any case); 0, Windows default, for null.</summary>
    public static int ChoiceIndex(string? chosen) =>
        Math.Max(0, Choices(chosen).FindIndex(code => code is not null && string.Equals(code, chosen, StringComparison.OrdinalIgnoreCase)));

    /// <summary>
    /// <see cref="Choices"/> as the list says them: "Default (Deutsch)" (§518) — Windows' language
    /// (<paramref name="windows"/>, most wanted first) in its own words —, then each language in its own
    /// words; one a newer Karpo offers by its code, the one thing this build knows of it. The settings
    /// app's language box and the edit bar's ⚙ › Language › (§337) alike.
    /// </summary>
    public static List<string> ChoiceNames(string? chosen, IEnumerable<string> windows) =>
    [
        .. Choices(chosen).Select(code => code switch
        {
            null => S.SettingsValueDefault(NativeName(FromWindows(windows))),
            _ when Strings.Has(code) => NativeName(Strings.All.First(known => string.Equals(known, code, StringComparison.OrdinalIgnoreCase))),
            _ => code,
        }),
    ];

    /// <summary>The language <paramref name="index"/> of <see cref="Choices"/> stands for, as Config.Language holds it; null for Windows default or past the list.</summary>
    public static string? ChosenAt(string? chosen, int index) =>
        Choices(chosen) is var codes && index > 0 && index < codes.Count ? codes[index] : null;

    private static Known? Find(string language)
    {
        foreach (var known in All)
        {
            if (string.Equals(known.Code, language, StringComparison.OrdinalIgnoreCase))
            {
                return known;
            }
        }
        return null;
    }

    /// <summary>
    /// A language's name as it writes it itself — "Deutsch", "简体中文" —, which is how a list of
    /// languages is written everywhere, so anybody finds their own whatever the list is in.
    /// </summary>
    public static string NativeName(string language) => language switch
    {
        Pseudo => "Pseudo (qps-ploc)", // not-ui: a test language, named for developers
        _ => Find(language)?.Name ?? "English",
    };

    /// <summary>
    /// §631: <paramref name="code"/>'s name in <paramref name="language"/> — "Deutsch" is "German" in English, "Allemand" in
    /// French —, as Karpo's files have it (core.language.name.&lt;code&gt;, CLDR's names); the pseudo-locale's own name, and
    /// the code of one this build doesn't know.
    /// </summary>
    public static string NameIn(string code, string language) =>
        Strings.Lookup("core.language.name." + code.Replace("-", "", StringComparison.Ordinal), language)
        ?? (Find(code) is null && code != Pseudo ? code : NativeName(code));

    /// <summary>
    /// §631: what a search finds <paramref name="code"/> by: its own name, its English name, its name in the language Karpo
    /// speaks now, and its code; one this build doesn't know (a newer Karpo's) its code alone.
    /// </summary>
    public static IReadOnlyList<string> Words(string code) =>
        Find(code) is null ? [code] : [NativeName(code), NameIn(code, "en"), NameIn(code, Strings.Language), code];

    /// <summary>
    /// §631: how well <paramref name="query"/> finds a language — <paramref name="name"/> as the list says it, <paramref name="code"/>
    /// null for Windows default — by <see cref="Words"/>, every word typed found, accents and capitals aside (CatalogueSearch):
    /// 0 its code typed ("de", "pt-BR"), 1 one of its names starting so, 2 found anywhere in them; null not found. Nothing
    /// typed finds every one alike.
    /// </summary>
    public static int? Rank(string? code, string name, string query)
    {
        IReadOnlyList<string> words = code is null ? [] : Words(code);
        if (!CatalogueSearch.Matches(query, name, words))
        {
            return null;
        }
        var typed = CatalogueSearch.Fold(query.Trim());
        return typed.Length == 0 ? 2
            : code is not null && CatalogueSearch.Fold(code) == typed ? 0
            : words.Prepend(name).Any(word => CatalogueSearch.Fold(word).StartsWith(typed, StringComparison.Ordinal)) ? 1
            : 2;
    }

    /// <summary>
    /// The Karpo language one of Windows' language tags ("de-CH", "zh-Hant-TW", "pt-PT") stands
    /// for, whether or not there is a table for it yet. Chinese is told apart by its script:
    /// Traditional for zh-Hant, zh-TW, zh-HK and zh-MO, Simplified for the rest. Portuguese is
    /// Brazil's, the one Karpo has; "in" is Indonesian's old code; Bengali is Bengali whether India's
    /// (bn-IN) or Bangladesh's (bn-BD); Persian is Iran's and Afghanistan's (fa-AF, and Dari's own
    /// prs-AF); Malay is Malaysia's, Brunei's and Singapore's. Null for a language Karpo doesn't know.
    /// </summary>
    public static string? Code(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return null;
        }
        var parts = tag.Trim().Replace('_', '-').Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return null;
        }
        var language = parts[0].ToLowerInvariant();
        var rest = parts.Skip(1).Select(part => part.ToLowerInvariant()).ToList();
        var code = language switch
        {
            "zh" => rest.Contains("hant") || (!rest.Contains("hans") && (rest.Contains("tw") || rest.Contains("hk") || rest.Contains("mo"))) ? "zh-Hant" : "zh-Hans",
            "pt" => "pt-BR",
            "in" => "id",
            "prs" => "fa",
            // Tagalog (older Windows) is Filipino; Punjabi is the Gurmukhi table, so Shahmukhi (pa-Arab) is none.
            "tl" => "fil",
            "pa" when rest.Contains("arab") => "",
            _ => language,
        };
        return Find(code)?.Code;
    }

    /// <summary>The Karpo language for one of Windows' language tags (<see cref="Code"/>) where there is a table for it; null otherwise.</summary>
    public static string? Map(string? tag) => Code(tag) is { } code && Strings.Has(code) ? code : null;

    /// <summary>The first of Windows' preferred languages (<paramref name="preferred"/>, most wanted first) that Karpo has; English for none.</summary>
    public static string FromWindows(IEnumerable<string> preferred)
    {
        foreach (var tag in preferred)
        {
            if (Map(tag) is { } language)
            {
                return language;
            }
        }
        return "en";
    }

    /// <summary>
    /// The language Karpo speaks for <paramref name="chosen"/>, Config.Language: the one chosen when
    /// Karpo has it (the pseudo-locale too), Windows' (<paramref name="windows"/>) for null or one
    /// this build doesn't know, such as a language a newer Karpo offers.
    /// </summary>
    public static string Resolve(string? chosen, IEnumerable<string> windows)
    {
        if (!string.IsNullOrWhiteSpace(chosen))
        {
            foreach (var language in Strings.All)
            {
                if (string.Equals(language, chosen.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return language;
                }
            }
        }
        return FromWindows(windows);
    }

    /// <summary><see cref="Resolve(string?, IEnumerable{string})"/> with Windows' own list.</summary>
    public static string Resolve(string? chosen) => Resolve(chosen, string.IsNullOrWhiteSpace(chosen) || !Strings.Has(chosen) ? WindowsPreferred() : []);

    /// <summary>Makes <paramref name="chosen"/>'s language the one S reads (<see cref="Strings.Use"/>). True when that changed it.</summary>
    public static bool Apply(string? chosen) => Strings.Use(Resolve(chosen));

    /// <summary>
    /// The language tag the rest of Windows knows a Karpo language by: what DirectWrite picks a
    /// fallback font by and what WinUI is asked for. Chinese, Japanese and Korean share characters,
    /// and only the tag tells which font draws them: Microsoft YaHei UI for zh-CN, Microsoft
    /// JhengHei UI for zh-TW, Yu Gothic UI for ja-JP, Malgun Gothic for ko-KR.
    /// </summary>
    public static string Tag(string language) => language switch
    {
        Pseudo => Pseudo,
        _ => Find(language)?.Tag ?? "en-US",
    };

    /// <summary>Whether <paramref name="count"/> takes <paramref name="language"/>'s singular, by its <see cref="Plural"/> rule; English's for one Karpo doesn't know.</summary>
    public static bool IsOne(string language, long count)
    {
        var n = count < 0 ? -count : count;
        return (Find(language)?.Plural ?? Plural.One) switch
        {
            Plural.NoughtAndOne => n is 0 or 1,
            Plural.EndsInOne => n % 10 == 1 && n % 100 != 11,
            Plural.Never => false,
            _ => n == 1,
        };
    }

    /// <summary>Whether <paramref name="language"/> reads right to left: Arabic, Urdu and Persian. False for one Karpo doesn't know.</summary>
    public static bool IsRightToLeft(string language) => Find(language)?.RightToLeft ?? false;

    /// <summary>Whether the language Karpo speaks now reads right to left (<see cref="IsRightToLeft"/>).</summary>
    public static bool RightToLeft => IsRightToLeft(Strings.Language);

    /// <summary>
    /// Whether a line of <paramref name="text"/> is laid out right to left, its first words at its
    /// right: where it has a letter of a right-to-left script and Karpo speaks a right-to-left
    /// language (<paramref name="rightToLeft"/>), so "Wi-Fi" and the Arabic after it read in Arabic's
    /// order; or, in any language, where its first letter is one, as an Arabic window's title.
    /// Numbers, times and Latin words alone stay left to right in every language.
    /// </summary>
    public static bool ReadsRightToLeft(string text, bool rightToLeft)
    {
        foreach (var rune in text.EnumerateRunes())
        {
            if (IsRightToLeftLetter(rune))
            {
                return true;
            }
            if (!rightToLeft && Rune.IsLetter(rune))
            {
                return false;
            }
        }
        return false;
    }

    /// <summary><see cref="ReadsRightToLeft(string, bool)"/> in the language Karpo speaks now.</summary>
    public static bool ReadsRightToLeft(string text) => ReadsRightToLeft(text, RightToLeft);

    /// <summary>
    /// A letter of a script written right to left: Hebrew, Arabic (with Urdu's and Persian's letters),
    /// Syriac, Thaana, N'Ko and their presentation forms. Arabic's digits and its comma are no
    /// letters: they set no direction, as the Unicode bidi algorithm has it.
    /// </summary>
    private static bool IsRightToLeftLetter(Rune rune) => Rune.IsLetter(rune) && rune.Value switch
    {
        >= 0x0590 and <= 0x08FF => true,
        >= 0xFB1D and <= 0xFDFF => true,
        >= 0xFE70 and <= 0xFEFC => true,
        >= 0x10800 and <= 0x10FFF => true,
        >= 0x1E800 and <= 0x1EFFF => true,
        _ => false,
    };

    /// <summary>The locale DirectWrite lays Karpo's text out in now: the language's own (<see cref="Tag"/>), English's for the pseudo-locale, which is English letters.</summary>
    public static string TextLocale => Strings.Language is var language && language != Pseudo ? Tag(language) : "en-US";

    /// <summary>
    /// Windows' display languages, the most wanted first ("de-CH", "en-US"). A run on a config of
    /// its own — a test, a smoke run — reads KARPO_LANGUAGE instead where it is set, so a test
    /// that says nothing of a language reads English on any PC.
    /// </summary>
    public static IReadOnlyList<string> WindowsPreferred()
    {
        if (Config.IsIsolated && Environment.GetEnvironmentVariable("KARPO_LANGUAGE") is { Length: > 0 } pinned)
        {
            return [pinned];
        }
        try
        {
            return ReadPreferred();
        }
        catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException)
        {
            return [];
        }
    }

    private static unsafe List<string> ReadPreferred()
    {
        uint count = 0;
        uint length = 0;
        if (!GetUserPreferredUILanguages(MUI_LANGUAGE_NAME, &count, null, &length) || length == 0)
        {
            return [];
        }
        var buffer = new char[length];
        fixed (char* chars = buffer)
        {
            if (!GetUserPreferredUILanguages(MUI_LANGUAGE_NAME, &count, chars, &length))
            {
                return [];
            }
        }
        // A list of names, each ended by a nought, the whole ended by another.
        return new string(buffer, 0, (int)length).Split('\0', StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    /// <summary>
    /// Asks Windows to give this process's own resources — WinUI's words in the settings app — in
    /// <paramref name="language"/>; null leaves Windows' choice. The settings app calls it before
    /// its first window and again when the language changes.
    /// </summary>
    public static unsafe void SetPreferred(string? language)
    {
        try
        {
            if (language is null)
            {
                SetProcessPreferredUILanguages(MUI_LANGUAGE_NAME, null, null);
                return;
            }
            // A list of names, each ended by a nought, the whole ended by another.
            var list = Tag(language) + "\0\0";
            fixed (char* chars = list)
            {
                uint set = 0;
                SetProcessPreferredUILanguages(MUI_LANGUAGE_NAME, chars, &set);
            }
        }
        catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException)
        {
        }
    }

    /// <summary>
    /// The language config.json at <paramref name="path"/> chose, read without loading the config:
    /// the settings app needs it before WinUI starts, and loading the config is the model's to do
    /// once (it repairs and reports what it finds). Null for none, and for a file that doesn't read.
    /// </summary>
    public static string? Peek(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var document = JsonDocument.Parse(stream, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("language", out var language)
                && language.ValueKind == JsonValueKind.String
                    ? language.GetString()
                    : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private const uint MUI_LANGUAGE_NAME = 0x8;

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool GetUserPreferredUILanguages(uint flags, uint* count, char* buffer, uint* length);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool SetProcessPreferredUILanguages(uint flags, char* languages, uint* count);
}
