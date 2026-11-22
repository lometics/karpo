// The registries' words in every language (G57 chunk D): a widget's, a preset's and a Windows
// link's words are keyed by its id rather than written in the record, so the build cannot check
// them; these do. And they are read when asked for, never kept, so a language change reaches them
// with nothing rebuilt; the add list's search finds a widget by its English name too.

using Karpo.Dock;
using Karpo.Dock.Weather;

namespace Karpo.Tests.Localization;

[Collection(InLanguage.Collection)]
public sealed class RegistryWordsTests
{
    public static TheoryData<string> Languages => [.. StringFiles.Codes];

    /// <summary>Every widget has a title, a blurb and keywords in every language: the generator makes every language have English's keys.</summary>
    [Fact]
    public void EveryWidgetHasItsWords()
    {
        var english = StringFiles.Map("en");
        foreach (var info in WidgetInfos.All)
        {
            foreach (var part in (string[])["title", "blurb", "keywords"])
            {
                Assert.True(english.ContainsKey($"{info.WordsKey}.{part}"), $"en.json has no {info.WordsKey}.{part}");
            }
        }
    }

    /// <summary>Every ready-made group has its words, and every link to Windows' settings its title.</summary>
    [Fact]
    public void EveryPresetAndLinkHasItsWords()
    {
        var english = StringFiles.Map("en");
        foreach (var preset in Presets.All)
        {
            foreach (var part in (string[])["title", "blurb", "keywords"])
            {
                Assert.True(english.ContainsKey($"core.preset.{preset.Id}.{part}"), $"en.json has no core.preset.{preset.Id}.{part}");
            }
        }
        foreach (var info in WidgetInfos.All)
        {
            if (info.Settings is { } link)
            {
                Assert.True(english.ContainsKey(link.TitleKey), $"{info.Id}'s link names {link.TitleKey}, which en.json has not");
            }
        }
    }

    /// <summary>The search compares small letters, and the record's own test holds English's keywords to them; every language's are too.</summary>
    [Theory]
    [MemberData(nameof(Languages))]
    public void EveryLanguagesKeywordsAreSmallLettersAndNoneEmpty(string language)
    {
        foreach (var (key, text) in StringFiles.Read(language).Where(pair => pair.Key.EndsWith(".keywords", StringComparison.Ordinal) && pair.Key.StartsWith("core.", StringComparison.Ordinal)))
        {
            var words = text.Split(',', StringSplitOptions.TrimEntries);
            Assert.All(words, word => Assert.False(string.IsNullOrWhiteSpace(word), $"{language}.json {key} has an empty keyword"));
            Assert.Equal(text.ToLowerInvariant(), text);
        }
    }

    [Fact]
    public void TheClocksTitleIsGermanInGermanAndEnglishAgainAfter()
    {
        Assert.Equal("Clock", WidgetInfos.Clock.Title);
        using (InLanguage.Use("de"))
        {
            Assert.Equal("Uhr", WidgetInfos.Clock.Title);
            Assert.Equal("Uhr", ListWidgets.Of(ListKind.Clock).Info.Title);
            Assert.Equal("Uhr", CatalogueEntry.Of(ListWidgets.Of(ListKind.Clock)).Title);
        }
        Assert.Equal("Clock", WidgetInfos.Clock.Title);
    }

    [Fact]
    public void AWeatherConditionIsChineseInChinese()
    {
        using (InLanguage.Use("zh-Hans"))
        {
            Assert.Equal("多云", WeatherCodes.Describe(3, day: true).Text);
            Assert.Contains("多云", WeatherCodes.Conditions);
        }
        Assert.Equal("Cloudy", WeatherCodes.Describe(3, day: true).Text);
    }

    /// <summary>In German the add list finds the clock by "Uhr" and still by "clock", and the presets by their English names.</summary>
    [Fact]
    public void TheAddListFindsAWidgetByItsEnglishNameInGerman()
    {
        using (InLanguage.Use("de"))
        {
            var clock = CatalogueEntry.Of(ListWidgets.Of(ListKind.Clock));
            Assert.True(CatalogueSearch.Matches("uhr", clock));
            Assert.True(CatalogueSearch.Matches("clock", clock));
            Assert.True(CatalogueSearch.Matches("stopwatch", clock));
            Assert.True(CatalogueSearch.Matches("stoppuhr", clock));
            Assert.False(CatalogueSearch.Matches("bluetooth", clock));
            var network = CatalogueEntry.Of(ListWidgets.Of(ListKind.Wifi));
            Assert.True(CatalogueSearch.Matches("network", network));
            Assert.True(CatalogueSearch.Matches("netzwerk", network));

            var system = Presets.Find("systemGroup")!;
            Assert.Contains("System", Presets.WordsOf(system));
            var connectivity = Presets.Find("connectivityGroup")!;
            Assert.Equal("Verbindungen", connectivity.Title);
            Assert.Contains("Connectivity", Presets.WordsOf(connectivity));
        }
    }

    /// <summary>In English the English words are not doubled: a row's words are what they were before there were languages.</summary>
    [Fact]
    public void InEnglishARowsWordsAreItsBlurbAndKeywordsAlone()
    {
        var clock = CatalogueEntry.Of(ListWidgets.Of(ListKind.Clock));
        Assert.Empty(clock.English);
        Assert.Equal([clock.Blurb, .. clock.Keywords], CatalogueSearch.WordsOf(clock));
        Assert.Equal(WidgetInfos.Clock.Keywords.Length, WidgetInfos.Clock.Keywords.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>A keyword list in another language is its own words, then English's, each once.</summary>
    [Fact]
    public void KeywordsAreTheLanguagesThenEnglishs()
    {
        var english = WidgetInfos.WeatherNow.Keywords;
        using (InLanguage.Use("fr"))
        {
            var words = WidgetInfos.WeatherNow.Keywords;
            Assert.Equal("météo", words[0]);
            Assert.Contains("weather", words);
            Assert.Equal(words.Length, words.Distinct(StringComparer.Ordinal).Count());
            Assert.Equal(english.Length, english.Intersect(words).Count());
        }
    }

    [Fact]
    public void TheRegistriesRowsFollowTheLanguage()
    {
        using (InLanguage.Use("de"))
        {
            Assert.Equal("Task-Manager", BarMenus.Info(BarMenuAction.TaskManager)?.Name);
            Assert.Equal("Leistung", WidgetCategory.Performance.Title());
            Assert.Equal("Soundeinstellungen", WidgetInfos.Volume.Settings?.Title);
            Assert.Equal("5J", MarketRanges.Label(MarketRange.FiveYears, 0));
            Assert.Equal("10 Aktien", MarketHoldings.Units(10, MarketKind.Stock, ""));
        }
        Assert.Equal("5Y", MarketRanges.Label(MarketRange.FiveYears, 0));
        Assert.Equal("Task Manager", BarMenus.Info(BarMenuAction.TaskManager)?.Name);
    }
}
