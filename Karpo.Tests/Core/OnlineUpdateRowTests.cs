// The General page's "Update online widgets" row (Karpo.Core\OnlineUpdateRow.cs): the
// one interval everything online updates at, what it offers and how it says each.

using Karpo.Tests.Localization;

namespace Karpo.Tests.Core;

public sealed class OnlineUpdateRowTests
{
    /// <summary>The weather's and the stocks' choices together, shortest first, and the default among them.</summary>
    [Fact]
    public void TheRowOffersWhatBothWidgetsOfferedShortestFirst()
    {
        Assert.Equal([5, 15, 30, 60, 120, 180], OnlineUpdateRow.Minutes);
        Assert.Equal(OnlineUpdateRow.Minutes.Order(), OnlineUpdateRow.Minutes);
        Assert.Equal(1, OnlineUpdateRow.IndexOf(OnlineUpdates.DefaultMinutes));
        Assert.Equal(OnlineUpdateRow.Minutes.Count, OnlineUpdateRow.Labels.Count);
    }

    [Theory]
    [InlineData(5, "Every 5 minutes")]
    [InlineData(15, "Every 15 minutes")]
    [InlineData(60, "Every hour")]
    [InlineData(120, "Every 2 hours")]
    [InlineData(180, "Every 3 hours")]
    // A hand edit that is none of the choices still says what it is.
    [InlineData(45, "Every 45 minutes")]
    [InlineData(90, "Every 90 minutes")]
    public void EachIntervalIsSaidInWords(int minutes, string said) =>
        Assert.Equal(said, OnlineUpdateRow.Say(minutes));

    /// <summary>A value that is none of the choices selects none, and is never rounded to one behind the person's back.</summary>
    [Theory]
    [InlineData(45)]
    [InlineData(0)]
    [InlineData(181)]
    public void AValueThatIsNoChoiceSelectsNone(int minutes) =>
        Assert.Equal(-1, OnlineUpdateRow.IndexOf(minutes));
}

/// <summary>The row's words in the other languages: they are made when asked, so a language change reaches them.</summary>
[Collection(InLanguage.Collection)]
public sealed class OnlineUpdateRowLanguageTests
{
    [Fact]
    public void TheWordsFollowTheLanguage()
    {
        using (InLanguage.Use("de"))
        {
            Assert.Equal("Jede Stunde", OnlineUpdateRow.Say(60));
            Assert.Equal("Alle 15 Minuten", OnlineUpdateRow.Labels[1]);
        }
        Assert.Equal("Every 15 minutes", OnlineUpdateRow.Labels[1]);
    }
}
