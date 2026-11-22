// The weather widget's place box (Karpo.Core\PlaceQuery.cs): when what
// is typed is worth searching for, and how a chosen place reads in the box.

using Karpo.Core;

namespace Karpo.Tests.Core;

public sealed class PlaceQueryTests
{
    [Theory]
    [InlineData("Zürich", null, true)]
    [InlineData("zu", null, true)]
    [InlineData("z", null, false)]
    [InlineData("", null, false)]
    [InlineData("   ", null, false)]
    [InlineData(null, null, false)]
    // The same words again ask nothing: the list is already what they found.
    [InlineData("Zürich", "Zürich", false)]
    [InlineData("  Zürich ", "Zürich", false)]
    [InlineData("zürich", "ZÜRICH", false)]
    [InlineData("Zürich H", "Zürich", true)]
    public void TypingSearchesOnceItIsWorthIt(string? text, string? asked, bool searches)
    {
        Assert.Equal(searches, PlaceQuery.Searches(text, asked));
    }

    [Fact]
    public void TheWaitIsShortEnoughToFeelLiveAndLongEnoughToSkipKeystrokes()
    {
        Assert.InRange(PlaceQuery.Wait.TotalMilliseconds, 150, 600);
        Assert.Equal(2, PlaceQuery.LeastLetters);
    }

    [Theory]
    [InlineData("Zürich", "Zurich, Switzerland", "Zürich, Zurich, Switzerland")]
    [InlineData(" Zürich ", "", "Zürich")]
    [InlineData("", "Switzerland", "Switzerland")]
    [InlineData("Bern", null, "Bern")]
    [InlineData("  ", "  ", "")]
    public void AChosenPlaceReadsAsItsNameAndWhereItIs(string? name, string? detail, string expected)
    {
        Assert.Equal(expected, PlaceQuery.Label(name, detail));
    }

    [Fact]
    public void CleanSqueezesWhatWasTyped()
    {
        Assert.Equal("New York", PlaceQuery.Clean("  New   York  "));
        Assert.Equal("", PlaceQuery.Clean(null));
    }
}
