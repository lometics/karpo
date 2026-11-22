// What an event's block in the day and week views says (Karpo.Dock\Calendar\EventText.cs):
// the type steps down from Caption as the hours get short, to a floor and no further; the title
// wraps between words over the lines the block holds, its last line taking the rest; the time and
// then the place only on lines the whole title leaves. Measured with a fake: every letter of the title 6 px at 12 px type and
// in proportion at other sizes, a line 1.4 times the type.

using Karpo.Dock;

namespace Karpo.Tests.Calendars;

public sealed class EventTextTests
{
    /// <summary>A line's height at <paramref name="px"/>: 1.4 of it, whole px up.</summary>
    private static float Line(float px) => MathF.Ceiling(px * 1.4f);

    /// <summary>A text's width at 12 px: 6 px a letter.</summary>
    private static float Width(string text, bool bold) => text.Length * 6;

    // --- The size --------------------------------------------------------------------

    /// <summary>
    /// An hour that holds two lines of Caption and the block's air has Caption; a shorter one the
    /// next step down, then the floor; and the floor however short the hour, never smaller.
    /// </summary>
    [Theory]
    // Caption: two lines of 17 px and 5 px of air.
    [InlineData(48, 12)]
    [InlineData(39, 12)]
    // Two lines of 16 px at 11, not of 17 at 12.
    [InlineData(38, 11)]
    [InlineData(37, 11)]
    // Two of 14 at 10.
    [InlineData(36, 10)]
    [InlineData(33, 10)]
    // The busy week at 100 %: an hour of 19 px; and shorter still. The floor.
    [InlineData(19, 10)]
    [InlineData(4, 10)]
    public void TheTypeStepsDownAsTheHoursGetShort(float hour, float px) =>
        Assert.Equal(px, EventText.Px(hour, 5, dip => dip, Line));

    /// <summary>The steps are the text size's and the scale's: at 150 % with the largest text, Caption is 40.5 px and the floor 33.75.</summary>
    [Fact]
    public void TheStepsFollowTheScaleAndTheTextSize()
    {
        float Size(float dip) => dip * 1.5f * 2.25f;
        Assert.Equal(Size(12), EventText.Px(200, 8, Size, Line));
        Assert.Equal(Size(10), EventText.Px(30, 8, Size, Line));
    }

    [Fact]
    public void TheStepsRunFromCaptionToAReadableFloor()
    {
        Assert.Equal(12, EventText.StepsDip[0]);
        Assert.Equal(10, EventText.FloorDip);
        Assert.True(EventText.StepsDip.Zip(EventText.StepsDip.Skip(1)).All(pair => pair.First > pair.Second), "the steps go down");
    }

    // --- The lines ---------------------------------------------------------------------

    /// <summary>A title wider than the block wraps between words over the lines the block holds.</summary>
    [Fact]
    public void ATitleWrapsBetweenWordsOverTheBlocksLines()
    {
        // 60 px across holds ten letters; 3 lines of 17 px in 52 px.
        var says = EventText.Lay("Design review with the team", "10:00–11:30", "Room 4", 12, 17, 60, 52, Width);
        Assert.Equal(["Design", "review", "with the team"], says.Title);
        Assert.Empty(says.Details);
    }

    /// <summary>The last line the block holds takes the rest of the title, to be cut with "…" where it is drawn.</summary>
    [Fact]
    public void TheLastLineTakesTheRest()
    {
        var says = EventText.Lay("Design review with the team", "10:00–11:30", null, 12, 17, 60, 34, Width);
        Assert.Equal(["Design", "review with the team"], says.Title);
    }

    /// <summary>The time goes before the title does: a block of one line says the title alone.</summary>
    [Fact]
    public void TheTimeIsDroppedBeforeTheTitle()
    {
        var says = EventText.Lay("Standup", "9:30–9:45", "Teams", 12, 17, 120, 20, Width);
        Assert.Equal(["Standup"], says.Title);
        Assert.Empty(says.Details);
    }

    /// <summary>The title whole and lines to spare: the time, then the place, a line each.</summary>
    [Fact]
    public void TheTimeAndThePlaceHaveTheLinesTheTitleLeaves()
    {
        var two = EventText.Lay("Standup", "9:30–9:45", "Teams", 12, 17, 120, 40, Width);
        Assert.Equal(["Standup"], two.Title);
        Assert.Equal(["9:30–9:45"], two.Details);
        var three = EventText.Lay("Standup", "9:30–9:45", "Teams", 12, 17, 120, 60, Width);
        Assert.Equal(["9:30–9:45", "Teams"], three.Details);
        var more = EventText.Lay("Standup", "9:30–9:45", "Teams", 12, 17, 120, 200, Width);
        Assert.Equal(["9:30–9:45", "Teams"], more.Details);
        Assert.Equal(12, more.Px);
        Assert.Equal(17, more.Line);
    }

    /// <summary>A title cut on its last line leaves no line to the time: the title is what the block is.</summary>
    [Fact]
    public void ATitleThatDoesNotFitWholeTakesEveryLine()
    {
        // One word wider than the block: its own line, cut where drawn, and nothing after it.
        var says = EventText.Lay("Superextraordinary", "9:00", null, 12, 17, 60, 40, Width);
        Assert.Equal(["Superextraordinary"], says.Title);
        Assert.Empty(says.Details);
    }

    /// <summary>A block shorter than a line still says its title's first line, which its edge cuts.</summary>
    [Fact]
    public void AShortBlockStillSaysItsTitle()
    {
        var says = EventText.Lay("Standup meeting", "9:30", null, 12, 17, 120, 9, Width);
        Assert.Equal(["Standup meeting"], says.Title);
        Assert.Empty(says.Details);
    }

    /// <summary>A block with no room across says nothing.</summary>
    [Fact]
    public void ABlockWithoutRoomSaysNothing()
    {
        var says = EventText.Lay("Standup", "9:30", null, 12, 17, 2, 100, Width);
        Assert.Empty(says.Title);
        Assert.Empty(says.Details);
    }

    [Fact]
    public void WrappingKeepsEveryWordInOrder()
    {
        bool Fits(string text) => text.Length <= 10;
        Assert.Equal(["one two", "three four", "five"], EventText.Wrap("one two three four five", 5, Fits));
        Assert.Equal(["one two", "three four five"], EventText.Wrap("one two three four five", 2, Fits));
        // Runs of spaces are one break.
        Assert.Equal(["one two"], EventText.Wrap("one  two", 1, Fits));
        Assert.Empty(EventText.Wrap("", 3, Fits));
        Assert.True(EventText.Whole(["one two", "three four", "five"], "one two three four five", Fits));
        Assert.False(EventText.Whole(["one two", "three four five"], "one two three four five", Fits));
    }
}
