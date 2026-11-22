// S and Strings: switching the language, texts with places, the .one/.other pairs, the lookups,
// the pseudo-locale, and the locale the dock lays text out in.

namespace Karpo.Tests.Localization;

[Collection(InLanguage.Collection)]
public sealed class StringsTests
{
    [Fact]
    public void EveryTestReadsEnglish()
    {
        Assert.Equal("en", Strings.Language);
        Assert.Equal("Display language", S.SettingsLanguageHeader);
    }

    [Fact]
    public void UseSwitchesWhatSSaysAndSaysWhetherItChanged()
    {
        using (InLanguage.Use("de"))
        {
            Assert.Equal("de", Strings.Language);
            Assert.Equal("Anzeigesprache", S.SettingsLanguageHeader);
            Assert.False(Strings.Use("de"));
            Assert.False(Strings.Use("DE"));
            Assert.True(Strings.Use("zh-Hans"));
            Assert.Equal("显示语言", S.SettingsLanguageHeader);
        }
        Assert.Equal("Display language", S.SettingsLanguageHeader);
    }

    [Fact]
    public void ALanguageThereIsNoTableForIsEnglish()
    {
        using (InLanguage.Use("fr"))
        {
            Assert.True(Strings.Use("sv"));
            Assert.Equal("en", Strings.Language);
            Strings.Use("fr");
            Assert.True(Strings.Use(null));
            Assert.Equal("en", Strings.Language);
        }
    }

    [Fact]
    public void ATextWithPlacesTakesItsValuesInEachLanguagesOrder()
    {
        Assert.Equal("Default (Deutsch)", S.SettingsValueDefault("Deutsch"));
        Assert.Equal("Feels like 21°", S.WidgetWeatherFeelsLike("21°"));
        using (InLanguage.Use("de"))
        {
            Assert.Equal("Standard (Deutsch)", S.SettingsValueDefault("Deutsch"));
            Assert.Equal("Gefühlt 21°", S.WidgetWeatherFeelsLike("21°"));
        }
        using (InLanguage.Use("zh-Hans"))
        {
            Assert.Equal("默认 (简体中文)", S.SettingsValueDefault("简体中文"));
        }
    }

    [Fact]
    public void APairPicksTheSingularByEachLanguagesRule()
    {
        Assert.Equal("1 notification", S.DockNotificationsCount(1));
        Assert.Equal("0 notifications", S.DockNotificationsCount(0));
        Assert.Equal("2 notifications", S.DockNotificationsCount(2));
        using (InLanguage.Use("de"))
        {
            Assert.Equal("1 Benachrichtigung", S.DockNotificationsCount(1));
            Assert.Equal("0 Benachrichtigungen", S.DockNotificationsCount(0));
        }
        using (InLanguage.Use("fr"))
        {
            // French takes the singular for nought too.
            Assert.Equal("0 notification", S.DockNotificationsCount(0));
            Assert.Equal("1 notification", S.DockNotificationsCount(1));
            Assert.Equal("2 notifications", S.DockNotificationsCount(2));
        }
        using (InLanguage.Use("zh-Hans"))
        {
            Assert.Equal("1 条通知", S.DockNotificationsCount(1));
            Assert.False(Strings.IsOne(1));
        }
    }

    [Fact]
    public void LookupFindsByKeyOrBySNameInTheLanguageNow()
    {
        Assert.Equal("Display language", S.Lookup("settings.language.header"));
        Assert.Equal("Display language", S.Lookup("SettingsLanguageHeader"));
        Assert.Equal("Default ({value})", S.Lookup("settings.value.default"));
        Assert.Null(S.Lookup("settings.language.nothing"));
        Assert.Null(S.Lookup("settings.Language.header"));
        using (InLanguage.Use("es"))
        {
            Assert.Equal("Idioma para mostrar", S.Lookup("SettingsLanguageHeader"));
            // And in English, for a search that finds a setting by its English words too.
            Assert.Equal("Display language", S.Lookup("SettingsLanguageHeader", "en"));
        }
    }

    [Theory]
    [InlineData("rss, atom, feed")]
    [InlineData("rss، atom، feed")]
    [InlineData("rss、atom、feed")]
    [InlineData("rss，atom，feed")]
    [InlineData(" rss ,, atom،feed ")]
    public void AListsWordsAreSplitAtWhicheverCommaTheLanguageWrites(string list) =>
        Assert.Equal(["rss", "atom", "feed"], Strings.Words(list));

    [Fact]
    public void ArabicKeywordsAreWordsEachNotOneLongOne()
    {
        // Arabic, Urdu and Persian write the Arabic comma between keywords (a regression: Arabic's
        // were once one word, and only its first one found the widget).
        foreach (var language in new[] { "ar", "ur", "fa" })
        {
            using (InLanguage.Use(language))
            {
                var words = Strings.List("core.widget.rss.keywords");
                Assert.Contains("rss", words);
                Assert.Contains("atom", words);
                Assert.All(words, word => Assert.DoesNotContain('،', word));
                Assert.DoesNotContain('،', new IconChoice("", "font").Name);
            }
        }
    }

    [Fact]
    public void TheDockLaysTextOutInTheLanguagesLocale()
    {
        Assert.Equal("en-US", Languages.TextLocale);
        using (InLanguage.Use("zh-Hans"))
        {
            // So DirectWrite falls back to Microsoft YaHei UI, not Japanese's Yu Gothic UI.
            Assert.Equal("zh-CN", Languages.TextLocale);
        }
        using (InLanguage.Use("qps-ploc"))
        {
            Assert.Equal("en-US", Languages.TextLocale);
        }
    }

    [Fact]
    public void ThePseudoLocaleIsEnglishAccentedLongerAndBracketed()
    {
        var english = StringFiles.Map("en");
        foreach (var key in Strings.Keys)
        {
            var plain = Strings.Lookup(key, "en")!;
            var pseudo = Strings.Lookup(key, "qps-ploc")!;
            Assert.StartsWith("[", pseudo);
            Assert.EndsWith("]", pseudo);
            // About 40 % longer: a width measured from English shows in a picture.
            Assert.True(pseudo.Length >= plain.Length * 1.3, $"{key}: \"{pseudo}\" is not longer than \"{plain}\"");
            // The same places, by name.
            Assert.True(StringFiles.Places(pseudo).SetEquals(StringFiles.Places(english[key])), key);
            // No plain letter outside the places: a word still in English is a word not in en.json.
            var words = System.Text.RegularExpressions.Regex.Replace(pseudo, @"\{[^{}]*\}", "");
            Assert.DoesNotMatch("[A-Za-z]", words);
        }
        using (InLanguage.Use("qps-ploc"))
        {
            Assert.Equal("[Đíšþĺáý ĺáñĝúáĝé·····]", S.SettingsLanguageHeader);
            Assert.Equal("[1 ñóţíƒíçáţíóñ······]", S.DockNotificationsCount(1));
        }
    }
}
