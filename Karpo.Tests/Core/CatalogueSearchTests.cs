// The add list's search (Karpo.Core\CatalogueSearch.cs): a widget is found by its name, its
// description and its search words, whatever the capitals and the accents, anywhere inside a word,
// every word typed narrowing; an empty field finds everything. Plain code: no window, no Direct2D.

namespace Karpo.Tests.Core;

public sealed class CatalogueSearchTests
{
    [Fact]
    public void AnEmptyFieldFindsEverything()
    {
        Assert.True(CatalogueSearch.Matches(null, "Clock", ["The time"]));
        Assert.True(CatalogueSearch.Matches("", "Clock", null));
        Assert.True(CatalogueSearch.Matches("   ", "Clock", []));
        Assert.Empty(CatalogueSearch.Words(" \t "));
    }

    [Theory]
    [InlineData("clock")]
    [InlineData("CLOCK")]
    [InlineData("cLoCk")]
    [InlineData("loc")]
    [InlineData("ock")]
    public void TheNameIsFoundWhateverTheCapitalsAndAnywhereInAWord(string query) =>
        Assert.True(CatalogueSearch.Matches(query, "Clock", null));

    [Fact]
    public void TheDescriptionAndTheSearchWordsFindItToo()
    {
        Assert.True(CatalogueSearch.Matches("time", "Clock", ["The time; opens the clock", "alarm"]));
        Assert.True(CatalogueSearch.Matches("stopw", "Clock", ["The time", "stopwatch"]));
        Assert.False(CatalogueSearch.Matches("battery", "Clock", ["The time", "stopwatch"]));
    }

    [Theory]
    [InlineData("cafe", "Café")]
    [InlineData("CAFÉ", "cafe")]
    [InlineData("ecran", "Écran")]
    [InlineData("uber", "Über")]
    [InlineData("strasse", "Straße")]
    [InlineData("zurich", "Zürich")]
    [InlineData("lodz", "Łódź")]
    [InlineData("noel", "Noël")]
    public void AnAccentNeverDecides(string query, string title) =>
        Assert.True(CatalogueSearch.Matches(query, title, null), $"\"{query}\" should find \"{title}\"");

    /// <summary>An accent typed as a mark after its letter (decomposed) is found as the letter it sits on.</summary>
    [Fact]
    public void AnAccentTypedAfterItsLetterIsFoundToo()
    {
        Assert.True(CatalogueSearch.Matches("café", "Cafe", null));
        Assert.True(CatalogueSearch.Matches("cafe", "Café", null));
        Assert.Equal("cafe", CatalogueSearch.Fold("CAFÉ"));
    }

    /// <summary>Every word typed has to be found, each anywhere in the name or the words: a second word narrows.</summary>
    [Fact]
    public void EveryWordNarrows()
    {
        string[] words = ["A volume slider on the taskbar itself", "audio", "mute"];
        Assert.True(CatalogueSearch.Matches("volume", "Volume slider", words));
        Assert.True(CatalogueSearch.Matches("slider mute", "Volume slider", words));
        Assert.True(CatalogueSearch.Matches("  mute   vol ", "Volume slider", words));
        Assert.False(CatalogueSearch.Matches("slider battery", "Volume slider", words));
    }

    /// <summary>The catalogue's own rows: "disk" finds Drive by its search words, which is what they are for.</summary>
    [Fact]
    public void TheCataloguesRowsAreFoundByTheirSearchWords()
    {
        var drive = Catalogue.On(WidgetType.List).Single(row => row.Id == "drive");
        Assert.True(CatalogueSearch.Matches("disk", drive));
        Assert.True(CatalogueSearch.Matches("how full", drive));
        Assert.True(CatalogueSearch.Matches("DRI", drive));
        Assert.False(CatalogueSearch.Matches("weather", drive));
        Assert.Equal([drive.Blurb, .. drive.Keywords], CatalogueSearch.WordsOf(drive));
    }
}
