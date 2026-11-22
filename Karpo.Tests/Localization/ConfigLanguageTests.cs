// Config.Language in the file: left out while it follows Windows, written as its code once chosen,
// kept as it is when this build has no table for it, and merged like any other setting when the
// dock and the settings app save at the same time.

namespace Karpo.Tests.Localization;

public sealed class ConfigLanguageTests
{
    [Fact]
    public void ANewConfigFollowsWindowsAndSaysNothingOfIt()
    {
        var config = new Config();
        Assert.Null(config.Language);
        Assert.DoesNotContain("\"language\"", config.ToJson());
    }

    [Theory]
    [InlineData("de")]
    [InlineData("zh-Hans")]
    [InlineData("qps-ploc")]
    // A language a newer Karpo offers is kept for it, not dropped.
    [InlineData("it")]
    public void AChosenLanguageGoesThroughTheFileAsItIs(string language)
    {
        var json = new Config { Language = language }.ToJson();
        Assert.Contains($"\"language\": \"{language}\"", json);
        var read = Config.Parse(json);
        Assert.Equal(language, read.Language);
        read.Repair();
        Assert.Equal(language, read.Language);
        Assert.Equal(language, read.Clone().Language);
    }

    [Fact]
    public void ALanguageChosenInOneAppSurvivesAnotherAppsSave()
    {
        var @base = new Config().ToJson();
        // The settings app chose French; the dock, meanwhile, saved a more solid tint.
        var settings = new Config { Language = "fr" }.ToJson();
        var dock = LookShorthand.Wearing(taskbarOpacity: 60).ToJson();

        var merged = Config.Parse(ConfigMerge.Merge(@base, dock, settings));
        Assert.Equal("fr", merged.Language);
        Assert.Equal(60, merged.TaskbarOpacity);

        merged = Config.Parse(ConfigMerge.Merge(@base, settings, dock));
        Assert.Equal("fr", merged.Language);
        Assert.Equal(60, merged.TaskbarOpacity);
    }

    [Fact]
    public void BackToWindowsDefaultInOneAppSurvivesTheOthersSave()
    {
        var @base = new Config { Language = "de" }.ToJson();
        var settings = new Config().ToJson();
        var german = LookShorthand.Wearing(taskbarOpacity: 60);
        german.Language = "de";
        var dock = german.ToJson();

        var merged = Config.Parse(ConfigMerge.Merge(@base, dock, settings));
        Assert.Null(merged.Language);
        Assert.Equal(60, merged.TaskbarOpacity);
    }

    [Fact]
    public void TwoDifferentChoicesAtOnceGoToTheAppSavingNow()
    {
        var @base = new Config().ToJson();
        var merged = Config.Parse(ConfigMerge.Merge(@base, new Config { Language = "es" }.ToJson(), new Config { Language = "de" }.ToJson()));
        Assert.Equal("es", merged.Language);
    }
}
