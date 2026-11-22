// Which language Karpo speaks (Languages.cs): Windows' tags mapped to Karpo's languages, the
// first of Windows' list Karpo has, a chosen language over Windows', and the settings app's list.
//
// The languages after the first five are offered once their file is there (Languages.Offered),
// so what depends on a file being there is said for whichever files there are.

namespace Karpo.Tests.Localization;

public sealed class LanguageTests
{
    [Theory]
    [InlineData("en-US", "en")]
    [InlineData("en-GB", "en")]
    [InlineData("de-DE", "de")]
    [InlineData("de-CH", "de")]
    [InlineData("de-AT", "de")]
    [InlineData("fr-FR", "fr")]
    [InlineData("fr-CA", "fr")]
    [InlineData("es-ES", "es")]
    [InlineData("es-MX", "es")]
    [InlineData("zh-CN", "zh-Hans")]
    [InlineData("zh-SG", "zh-Hans")]
    [InlineData("zh-Hans-CN", "zh-Hans")]
    [InlineData("zh-Hans-HK", "zh-Hans")]
    [InlineData("zh-Hans", "zh-Hans")]
    [InlineData("zh", "zh-Hans")]
    [InlineData("ZH-cn", "zh-Hans")]
    [InlineData("zh_CN", "zh-Hans")]
    [InlineData("zh-TW", "zh-Hant")]
    [InlineData("zh-HK", "zh-Hant")]
    [InlineData("zh-MO", "zh-Hant")]
    [InlineData("zh-Hant", "zh-Hant")]
    [InlineData("zh-Hant-TW", "zh-Hant")]
    [InlineData("pt-BR", "pt-BR")]
    [InlineData("pt-PT", "pt-BR")]
    [InlineData("pt", "pt-BR")]
    [InlineData("ja-JP", "ja")]
    [InlineData("ru-RU", "ru")]
    [InlineData("ko-KR", "ko")]
    [InlineData("it-IT", "it")]
    [InlineData("it-CH", "it")]
    [InlineData("pl-PL", "pl")]
    [InlineData("tr-TR", "tr")]
    [InlineData("nl-NL", "nl")]
    [InlineData("nl-BE", "nl")]
    [InlineData("id-ID", "id")]
    [InlineData("in-ID", "id")]
    [InlineData("hi-IN", "hi")]
    [InlineData("ar-SA", "ar")]
    [InlineData("ar-EG", "ar")]
    [InlineData("ar-AE", "ar")]
    [InlineData("bn-IN", "bn")]
    [InlineData("bn-BD", "bn")]
    [InlineData("ur-PK", "ur")]
    [InlineData("ur-IN", "ur")]
    [InlineData("mr-IN", "mr")]
    [InlineData("vi-VN", "vi")]
    [InlineData("te-IN", "te")]
    [InlineData("ta-IN", "ta")]
    [InlineData("ta-LK", "ta")]
    [InlineData("fa-IR", "fa")]
    [InlineData("fa-AF", "fa")]
    [InlineData("prs-AF", "fa")]
    [InlineData("ms-MY", "ms")]
    [InlineData("ms-BN", "ms")]
    [InlineData("ms-SG", "ms")]
    [InlineData("th-TH", "th")]
    [InlineData("uk-UA", "uk")]
    [InlineData("ro-RO", "ro")]
    [InlineData("ro-MD", "ro")]
    [InlineData("el-GR", "el")]
    [InlineData("el-CY", "el")]
    [InlineData("cs-CZ", "cs")]
    [InlineData("sw-KE", "sw")]
    [InlineData("ha-NG", "ha")]
    [InlineData("pa-IN", "pa")]
    [InlineData("fil-PH", "fil")]
    [InlineData("gu-IN", "gu")]
    [InlineData("kn-IN", "kn")]
    [InlineData("ml-IN", "ml")]
    [InlineData("am-ET", "am")]
    [InlineData("yo-NG", "yo")]
    [InlineData("my-MM", "my")]
    [InlineData("tl-PH", "fil")]
    [InlineData("pa-Arab-PK", null)]
    [InlineData("he-IL", null)]
    [InlineData("sk-SK", null)]
    [InlineData("sv-SE", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void WindowsTagsStandForKarposLanguages(string? tag, string? language) =>
        Assert.Equal(language, Languages.Code(tag));

    [Theory]
    [InlineData("de-CH", "de")]
    [InlineData("zh-CN", "zh-Hans")]
    [InlineData("ar-SA", "ar")]
    [InlineData("bn-BD", "bn")]
    [InlineData("it-IT", "it")]
    [InlineData("zh-TW", "zh-Hant")]
    [InlineData("pt-PT", "pt-BR")]
    public void ATagMapsToItsLanguageWhereThereIsATable(string tag, string? language) =>
        Assert.Equal(language is not null && Strings.Has(language) ? language : null, Languages.Map(tag));

    [Fact]
    public void TheFirstOfWindowsListKarpoHasIsTheOne()
    {
        Assert.Equal("de", Languages.FromWindows(["de-CH", "en-US"]));
        // Swedish first, German second: German, as Windows' own resources fall back.
        Assert.Equal("de", Languages.FromWindows(["sv-SE", "de-DE", "en-US"]));
        // Hebrew and Slovak have no table: the next one.
        Assert.Equal("zh-Hans", Languages.FromWindows(["he-IL", "zh-CN"]));
        Assert.Equal("fa", Languages.FromWindows(["sk-SK", "fa-IR"]));
        Assert.Equal("en", Languages.FromWindows(["sk-SK"]));
        Assert.Equal("en", Languages.FromWindows(["he-IL", "sv-SE"]));
        Assert.Equal("en", Languages.FromWindows([]));
    }

    [Fact]
    public void AChosenLanguageGoesBeforeWindows()
    {
        string[] german = ["de-DE"];
        Assert.Equal("fr", Languages.Resolve("fr", german));
        Assert.Equal("zh-Hans", Languages.Resolve("zh-Hans", german));
        Assert.Equal("zh-Hans", Languages.Resolve("ZH-HANS", german));
        Assert.Equal("en", Languages.Resolve("en", german));
        // The pseudo-locale, which the list doesn't offer, is still a language.
        Assert.Equal("qps-ploc", Languages.Resolve("qps-ploc", german));
    }

    [Fact]
    public void NoChoiceOrOneThisBuildLacksFollowsWindows()
    {
        string[] spanish = ["es-ES", "en-US"];
        Assert.Equal("es", Languages.Resolve(null, spanish));
        Assert.Equal("es", Languages.Resolve("", spanish));
        // A language a newer Karpo offers, or a hand edit.
        Assert.Equal("es", Languages.Resolve("sv", spanish));
        Assert.Equal("es", Languages.Resolve("he", spanish));
    }

    [Fact]
    public void AnIsolatedRunReadsTheLanguageItIsGivenNotThePCs()
    {
        // Isolation.cs gives every test English, whatever the PC's display language.
        Assert.True(Config.IsIsolated);
        Assert.Equal(["en"], Languages.WindowsPreferred());
        Assert.Equal("en", Languages.Resolve(null));
    }

    [Fact]
    public void KarpoKnowsFortyLanguagesTheFirstFiveFirst()
    {
        Assert.Equal(
            ["en", "de", "fr", "es", "zh-Hans", "pt-BR", "ja", "ru", "ko", "it", "zh-Hant", "pl", "tr", "nl", "id", "hi", "ar", "bn", "ur", "mr", "vi",
                "te", "ta", "fa", "ms", "th", "uk", "ro", "el", "cs",
                "sw", "ha", "pa", "fil", "gu", "kn", "ml", "am", "yo", "my"],
            Languages.All.Select(known => known.Code));
        Assert.Equal(
            ["English", "Deutsch", "Français", "Español", "简体中文", "Português (Brasil)", "日本語", "Русский", "한국어", "Italiano", "繁體中文", "Polski", "Türkçe", "Nederlands", "Bahasa Indonesia", "हिन्दी", "العربية", "বাংলা", "اردو", "मराठी", "Tiếng Việt",
                "తెలుగు", "தமிழ்", "فارسی", "Bahasa Melayu", "ไทย", "Українська", "Română", "Ελληνικά", "Čeština",
                "Kiswahili", "Hausa", "ਪੰਜਾਬੀ", "Filipino", "ગુજરાતી", "ಕನ್ನಡ", "മലയാളം", "አማርኛ", "Yorùbá", "မြန်မာ"],
            Languages.All.Select(known => Languages.NativeName(known.Code)));
        // Every code is its own: what Windows' tag for it stands for.
        Assert.All(Languages.All, known => Assert.Equal(known.Code, Languages.Code(known.Tag)));
        Assert.All(Languages.All, known => Assert.Equal(known.Code, Languages.Code(known.Code)));
    }

    [Fact]
    public void TheListOffersTheLanguagesThereIsAFileForInKarposOrder()
    {
        // The first five are there; the others as their files come.
        Assert.Equal(["en", "de", "fr", "es", "zh-Hans"], Languages.Offered.Take(5));
        Assert.Equal(Languages.All.Select(known => known.Code).Where(Strings.Has), Languages.Offered);
        // And every file is a language Karpo knows: none would be offered otherwise.
        Assert.All(Strings.All.Where(code => code != Languages.Pseudo), code => Assert.Contains(code, Languages.Offered));
    }

    [Fact]
    public void TheListIsWindowsDefaultThenEachLanguageInItsOwnWords()
    {
        Assert.Equal([null, .. Languages.Offered], Languages.Choices(null));
        Assert.Equal(0, Languages.ChoiceIndex(null));
        Assert.Equal(2, Languages.ChoiceIndex("de"));
        Assert.Equal(5, Languages.ChoiceIndex("zh-Hans"));
        Assert.Equal(5, Languages.ChoiceIndex("ZH-HANS"));
    }

    [Fact]
    public void AFilesLanguageTheListLacksIsListedLast()
    {
        var offered = Languages.Offered.Count;
        Assert.Equal([null, .. Languages.Offered, "qps-ploc"], Languages.Choices("qps-ploc"));
        Assert.Equal(offered + 1, Languages.ChoiceIndex("qps-ploc"));
        Assert.Equal(offered + 1, Languages.ChoiceIndex("sv"));
        Assert.Equal(offered + 1, Languages.Choices("DE").Count);
    }

    [Theory]
    [InlineData("en", "en-US")]
    [InlineData("de", "de-DE")]
    [InlineData("fr", "fr-FR")]
    [InlineData("es", "es-ES")]
    [InlineData("zh-Hans", "zh-CN")]
    [InlineData("pt-BR", "pt-BR")]
    [InlineData("ja", "ja-JP")]
    [InlineData("ru", "ru-RU")]
    [InlineData("ko", "ko-KR")]
    [InlineData("it", "it-IT")]
    [InlineData("zh-Hant", "zh-TW")]
    [InlineData("pl", "pl-PL")]
    [InlineData("tr", "tr-TR")]
    [InlineData("nl", "nl-NL")]
    [InlineData("id", "id-ID")]
    [InlineData("hi", "hi-IN")]
    [InlineData("ar", "ar-SA")]
    [InlineData("bn", "bn-IN")]
    [InlineData("ur", "ur-PK")]
    [InlineData("mr", "mr-IN")]
    [InlineData("vi", "vi-VN")]
    [InlineData("te", "te-IN")]
    [InlineData("ta", "ta-IN")]
    [InlineData("fa", "fa-IR")]
    [InlineData("ms", "ms-MY")]
    [InlineData("th", "th-TH")]
    [InlineData("uk", "uk-UA")]
    [InlineData("ro", "ro-RO")]
    [InlineData("el", "el-GR")]
    [InlineData("cs", "cs-CZ")]
    [InlineData("sw", "sw-KE")]
    [InlineData("ha", "ha-NG")]
    [InlineData("pa", "pa-IN")]
    [InlineData("fil", "fil-PH")]
    [InlineData("gu", "gu-IN")]
    [InlineData("kn", "kn-IN")]
    [InlineData("ml", "ml-IN")]
    [InlineData("am", "am-ET")]
    [InlineData("yo", "yo-NG")]
    [InlineData("my", "my-MM")]
    [InlineData("qps-ploc", "qps-ploc")]
    [InlineData("sv", "en-US")]
    public void EachLanguageHasTheTagWindowsKnowsItBy(string language, string tag) =>
        Assert.Equal(tag, Languages.Tag(language));

    [Theory]
    // English, German and the like: one alone.
    [InlineData("en", new long[] { 1, -1 }, new long[] { 0, 2, 11, 21, 101 })]
    [InlineData("de", new long[] { 1 }, new long[] { 0, 2, 21 })]
    [InlineData("pl", new long[] { 1 }, new long[] { 0, 2, 5, 21, 22 })]
    [InlineData("tr", new long[] { 1 }, new long[] { 0, 2 })]
    [InlineData("mr", new long[] { 1 }, new long[] { 0, 2 })]
    [InlineData("ur", new long[] { 1 }, new long[] { 0, 2, 11 })]
    [InlineData("te", new long[] { 1 }, new long[] { 0, 2 })]
    [InlineData("ta", new long[] { 1 }, new long[] { 0, 2 })]
    [InlineData("el", new long[] { 1 }, new long[] { 0, 2, 21 })]
    // Czech's few and many, and Romanian's few, take ".other".
    [InlineData("cs", new long[] { 1 }, new long[] { 0, 2, 5, 21 })]
    [InlineData("ro", new long[] { 1 }, new long[] { 0, 2, 20, 21 })]
    // Arabic: one alone; its dual, few and many take ".other".
    [InlineData("ar", new long[] { 1 }, new long[] { 0, 2, 3, 11, 100, 101 })]
    // French and Brazilian Portuguese: nought too.
    [InlineData("fr", new long[] { 0, 1, -1 }, new long[] { 2, 21 })]
    [InlineData("pt-BR", new long[] { 0, 1 }, new long[] { 2, 21 })]
    // Hindi, Bengali and Persian: nought too.
    [InlineData("hi", new long[] { 0, 1 }, new long[] { 2, 11, 21 })]
    [InlineData("fa", new long[] { 0, 1 }, new long[] { 2, 11, 21 })]
    [InlineData("bn", new long[] { 0, 1 }, new long[] { 2, 21 })]
    // Russian and Ukrainian: 1, 21, 101 … but not 11 or 111.
    [InlineData("ru", new long[] { 1, 21, 31, 101, 1001, -21 }, new long[] { 0, 2, 5, 11, 12, 111, 211 })]
    [InlineData("uk", new long[] { 1, 21, 31, 101, 1001 }, new long[] { 0, 2, 5, 11, 12, 111, 211 })]
    // Punjabi, Gujarati, Kannada and Amharic: nought too; Swahili, Hausa, Malayalam: one alone.
    [InlineData("pa", new long[] { 0, 1 }, new long[] { 2, 21 })]
    [InlineData("gu", new long[] { 0, 1 }, new long[] { 2, 21 })]
    [InlineData("kn", new long[] { 0, 1 }, new long[] { 2, 21 })]
    [InlineData("am", new long[] { 0, 1 }, new long[] { 2, 21 })]
    [InlineData("sw", new long[] { 1 }, new long[] { 0, 2, 21 })]
    [InlineData("ha", new long[] { 1 }, new long[] { 0, 2, 21 })]
    [InlineData("ml", new long[] { 1 }, new long[] { 0, 2, 21 })]
    // No plural at all.
    [InlineData("yo", new long[0], new long[] { 0, 1, 2 })]
    [InlineData("my", new long[0], new long[] { 0, 1, 2 })]
    [InlineData("zh-Hans", new long[0], new long[] { 0, 1, 2 })]
    [InlineData("zh-Hant", new long[0], new long[] { 0, 1, 2 })]
    [InlineData("ja", new long[0], new long[] { 1 })]
    [InlineData("ko", new long[0], new long[] { 1 })]
    [InlineData("id", new long[0], new long[] { 1 })]
    [InlineData("vi", new long[0], new long[] { 0, 1, 2 })]
    [InlineData("ms", new long[0], new long[] { 0, 1, 2 })]
    [InlineData("th", new long[0], new long[] { 0, 1, 2 })]
    // The pseudo-locale and an unknown code count as English does.
    [InlineData("qps-ploc", new long[] { 1 }, new long[] { 0, 2 })]
    [InlineData("sv", new long[] { 1 }, new long[] { 0, 2 })]
    public void EachLanguageCountsOneByItsOwnRule(string language, long[] ones, long[] others)
    {
        Assert.All(ones, count => Assert.True(Languages.IsOne(language, count), $"{language} {count}"));
        Assert.All(others, count => Assert.False(Languages.IsOne(language, count), $"{language} {count}"));
    }

    [Fact]
    public void ArabicUrduAndPersianReadRightToLeftAndNoOtherDoes()
    {
        Assert.Equal(["ar", "ur", "fa"], Languages.All.Where(known => known.RightToLeft).Select(known => known.Code));
        Assert.True(Languages.IsRightToLeft("ar"));
        Assert.True(Languages.IsRightToLeft("UR"));
        Assert.True(Languages.IsRightToLeft("fa"));
        Assert.False(Languages.IsRightToLeft("th"));
        Assert.False(Languages.IsRightToLeft("hi"));
        Assert.False(Languages.IsRightToLeft("qps-ploc"));
        Assert.False(Languages.IsRightToLeft("he"));
        // Every test reads English.
        Assert.False(Languages.RightToLeft);
    }

    [Theory]
    // Karpo in Arabic: any Arabic letter turns the line, wherever it stands.
    [InlineData("الإعدادات", true, true)]
    [InlineData("Wi-Fi متصل", true, true)]
    [InlineData("اردو", true, true)]
    [InlineData("فارسی", true, true)]
    // Numbers, times and Latin words alone stay left to right; Arabic's digits are numbers.
    [InlineData("12:45", true, false)]
    [InlineData("88.8 MB", true, false)]
    [InlineData("٢٠٢٦", true, false)]
    [InlineData("Microsoft Edge", true, false)]
    [InlineData("", true, false)]
    // In a left-to-right language, the first letter decides: an Arabic window's title turns.
    [InlineData("مستند - Word", false, true)]
    [InlineData("Word - مستند", false, false)]
    [InlineData("123 مستند", false, true)]
    [InlineData("تنظیمات - Karpo", false, true)]
    [InlineData("Einstellungen", false, false)]
    [InlineData("हिन्दी", false, false)]
    [InlineData("हिन्दी", true, false)]
    public void ALineReadsRightToLeftWhereItsWordsDo(string text, bool rightToLeft, bool turns) =>
        Assert.Equal(turns, Languages.ReadsRightToLeft(text, rightToLeft));

    [Fact]
    public void ThePeekReadsTheLanguageAloneAndNeverThrows()
    {
        var folder = Path.Combine(Isolation.Folder, "peek-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "config.json");

        File.WriteAllText(path, "{ \"version\": 17, \"language\": \"fr\", \"height\": 48 }");
        Assert.Equal("fr", Languages.Peek(path));

        File.WriteAllText(path, "{ \"version\": 17 }");
        Assert.Null(Languages.Peek(path));

        File.WriteAllText(path, "{ \"language\": 3 }");
        Assert.Null(Languages.Peek(path));

        File.WriteAllText(path, "{ not json");
        Assert.Null(Languages.Peek(path));

        File.WriteAllText(path, "[]");
        Assert.Null(Languages.Peek(path));

        Assert.Null(Languages.Peek(Path.Combine(folder, "missing.json")));
    }
}
