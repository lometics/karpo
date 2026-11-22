// Karpo.Core\Strings\*.json: every language has exactly English's keys, each translation keeps
// English's places, and every brace makes sense. The generator refuses to build with any of these
// wrong (Karpo.Strings.Generator); these say the same of the files themselves, and that the tables
// the build made are what the files say.

namespace Karpo.Tests.Localization;

public sealed class StringFileTests
{
    public static TheoryData<string> Translations => [.. StringFiles.Translations];

    [Fact]
    public void EveryFileIsALanguageKarpoKnowsWithATable()
    {
        // The first five at least.
        Assert.Equal(["en", "de", "fr", "es", "zh-Hans"], StringFiles.Codes.Take(5));
        // A file whose name is no language Karpo knows would never be offered.
        Assert.All(StringFiles.Codes, code => Assert.Contains(code, Languages.All.Select(known => known.Code)));
        // A table for each: English first, the rest by their code, the pseudo-locale last.
        Assert.Equal(["en", .. StringFiles.Translations.Order(StringComparer.Ordinal), "qps-ploc"], Strings.All);
    }

    [Fact]
    public void EveryLanguageKarpoKnowsHasItsFile()
    {
        // All thirty are translated: none of them is waiting for its file any more.
        Assert.Equal(Languages.All.Select(known => known.Code), StringFiles.Codes);
        Assert.Equal(Languages.All.Select(known => known.Code), Languages.Offered);
    }

    [Theory]
    [MemberData(nameof(Translations))]
    public void ATranslationHasExactlyEnglishsKeys(string language)
    {
        var english = StringFiles.Read("en").Select(pair => pair.Key).ToList();
        var translated = StringFiles.Read(language).Select(pair => pair.Key).ToList();
        Assert.Empty(english.Except(translated));
        Assert.Empty(translated.Except(english));
    }

    [Theory]
    [MemberData(nameof(Translations))]
    public void ATranslationKeepsEnglishsPlaces(string language)
    {
        var english = StringFiles.Map("en");
        foreach (var (key, text) in StringFiles.Read(language))
        {
            Assert.True(
                StringFiles.Places(english[key]).SetEquals(StringFiles.Places(text)),
                $"{language}.json \"{key}\": places {string.Join(",", StringFiles.Places(text))}, English has {string.Join(",", StringFiles.Places(english[key]))}");
        }
    }

    [Fact]
    public void EveryTextsBracesBalance()
    {
        foreach (var language in StringFiles.Codes)
        {
            foreach (var (key, text) in StringFiles.Read(language))
            {
                Assert.True(StringFiles.Balanced(text), $"{language}.json \"{key}\": \"{text}\"");
            }
        }
    }

    [Fact]
    public void NoTextIsEmptyOrPaddedWithSpaces()
    {
        foreach (var language in StringFiles.Codes)
        {
            foreach (var (key, text) in StringFiles.Read(language))
            {
                Assert.False(string.IsNullOrWhiteSpace(text), $"{language}.json \"{key}\" is empty");
                Assert.Equal(text.Trim(), text);
            }
        }
    }

    [Fact]
    public void EveryKeyHasOneOfTheFourPrefixes()
    {
        // settings.* the settings app, dock.* the bar's menus and flyouts, core.* the registries
        // both apps read, widget.* what a widget's face says.
        foreach (var (key, _) in StringFiles.Read("en"))
        {
            Assert.Matches(@"^(settings|dock|core|widget)(\.[a-z][A-Za-z0-9]*)+$", key);
        }
    }

    [Fact]
    public void TheTablesSayWhatTheFilesSay()
    {
        foreach (var language in StringFiles.Codes)
        {
            foreach (var (key, text) in StringFiles.Read(language))
            {
                Assert.Equal(StringFiles.Stored(text), Strings.Lookup(key, language));
            }
        }
        Assert.Equal(StringFiles.Read("en").Select(pair => pair.Key), Strings.Keys);
    }

    [Theory]
    [InlineData("de", "Taskleiste")]
    [InlineData("fr", "barre des tâches")]
    [InlineData("es", "barra de tareas")]
    [InlineData("zh-Hans", "任务栏")]
    [InlineData("pt-BR", "barra de tarefas")]
    [InlineData("ja", "タスク バー")]
    [InlineData("ru", "панели задач")]
    [InlineData("ko", "작업 표시줄")]
    [InlineData("it", "barra delle applicazioni")]
    [InlineData("zh-Hant", "工作列")]
    [InlineData("pl", "paska zadań")]
    [InlineData("tr", "görev çubuğu")]
    [InlineData("nl", "taakbalk")]
    [InlineData("id", "bilah tugas")]
    [InlineData("hi", "टास्कबार")]
    [InlineData("ar", "شريط المهام")]
    [InlineData("bn", "টাস্কবার")]
    [InlineData("ur", "ٹاسک بار")]
    [InlineData("mr", "टास्कबार")]
    [InlineData("vi", "thanh tác vụ")]
    [InlineData("te", "టాస్క్‌బార్")]
    [InlineData("ta", "பணிப்பட்டி")]
    [InlineData("fa", "نوار وظیفه")]
    [InlineData("ms", "bar tugas")]
    [InlineData("th", "แถบงาน")]
    [InlineData("uk", "панелі завдань")]
    [InlineData("ro", "bară de activități")]
    [InlineData("el", "γραμμής εργασιών")]
    [InlineData("cs", "hlavního panelu")]
    [InlineData("sw", "upau wa kazi")]
    [InlineData("ha", "sandar ɗawainiya")]
    [InlineData("pa", "ਟਾਸਕਬਾਰ")]
    [InlineData("fil", "taskbar")]
    [InlineData("gu", "ટાસ્કબાર")]
    [InlineData("kn", "ಕಾರ್ಯಪಟ್ಟಿ")]
    [InlineData("ml", "ടാസ്‌ക്ബാർ")]
    [InlineData("am", "የተግባር አሞሌ")]
    [InlineData("yo", "ọ̀pá iṣẹ́")]
    [InlineData("my", "တာ့စ်ဘား")]
    public void TheWindows11TermsAreTheOnesWindowsUses(string language, string taskbar)
    {
        // "Taskbar" is what each language's Windows 11 calls it (docs\widgets.md, "Karpo's words"),
        // for each language there is a file for yet.
        if (!StringFiles.Codes.Contains(language))
        {
            return;
        }
        // Japanese and Korean Windows write a space inside the word ("タスク バー"); one left out is no mistake.
        var text = StringFiles.Map(language)["core.link.taskbar"].Replace(" ", "", StringComparison.Ordinal);
        Assert.Contains(taskbar.Replace(" ", "", StringComparison.Ordinal), text, StringComparison.OrdinalIgnoreCase);
    }
}
