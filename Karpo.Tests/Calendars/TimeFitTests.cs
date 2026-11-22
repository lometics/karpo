// A big time sized to its room, never cut (Karpo.Dock\Clock\TimeFit.cs): the size steps down
// until the widest time the format reads fits, and only then; it is the format's size, so the
// time on show does not change it; and the shapes it is chosen on are the region's widest, every
// figure an eight, both of a 12-hour clock's markers among them. The clock's flyout drawn at
// every text size and width is ClockPanesTests.TheBigTimeFitsAtEveryTextSizeAndWidth.

using Karpo.Dock;

namespace Karpo.Tests.Calendars;

public sealed class TimeFitTests
{
    /// <summary>A fake measure: every character a fifth of the size wide, so a shape of 8 at 34 px is 54.4 px.</summary>
    private static float Width(string text, float px) => text.Length * px / 5;

    [Fact]
    public void ATimeThatFitsKeepsItsSize()
    {
        Assert.Equal(34, TimeFit.Px(34, 300, 12, ["88:88:88"], Width));
        // Exactly its room is a fit.
        Assert.Equal(34, TimeFit.Px(34, 8 * 34 / 5f, 12, ["88:88:88"], Width));
    }

    /// <summary>
    /// The largest text on the narrowest panel: 76.5 px of "88:88:88 AM" is 168 px with the fake; in
    /// 150 px of room the size comes down to the largest whole px that fits, and no further.
    /// </summary>
    [Theory]
    [InlineData(76.5f, 150)]
    [InlineData(76.5f, 100)]
    [InlineData(51, 100)]
    [InlineData(34, 17)]
    public void ATimeTooWideStepsDownToTheLargestSizeThatFits(float px, float room)
    {
        string[] shapes = ["88:88:88", "88:88:88 AM"];
        var fitted = TimeFit.Px(px, room, 1, shapes, Width);
        Assert.True(fitted < px);
        Assert.True(shapes.All(shape => Width(shape, fitted) <= room), $"{fitted} px does not fit {room} px");
        // A whole px more would not: the steps stop at the first that fits.
        Assert.True(shapes.Any(shape => Width(shape, fitted + 1) > room), $"{fitted + 1} px fits {room} px too");
    }

    /// <summary>The widest shape decides: a 12-hour clock's marker makes the size smaller than the figures alone would.</summary>
    [Fact]
    public void TheWidestShapeDecides()
    {
        var figures = TimeFit.Px(76.5f, 150, 1, ["88:88:88"], Width);
        var marked = TimeFit.Px(76.5f, 150, 1, ["88:88:88", "88:88:88 PM"], Width);
        Assert.True(marked < figures);
    }

    /// <summary>Never under the least, however little room there is.</summary>
    [Fact]
    public void NeverUnderTheLeast()
    {
        Assert.Equal(12, TimeFit.Px(34, 1, 12, ["88:88:88"], Width));
        Assert.Equal(34, TimeFit.Px(34, 1, 40, ["88:88:88"], Width));
    }

    /// <summary>
    /// The shapes are the format's widest, figures as eights and the hour of two, so every time of
    /// day has the size of every other: whatever hour the region's format writes, its shape is one of
    /// them, and the time on show is among them too.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheShapesAreTheFormatsWidest(bool seconds)
    {
        var shapes = TimeFit.Shapes(seconds, Clock.At(9, 5, 1, seconds));
        Assert.NotEmpty(shapes);
        Assert.All(shapes, shape => Assert.DoesNotContain(shape, letter => char.IsDigit(letter) && letter != '8'));
        foreach (var hour in Enumerable.Range(0, 24))
        {
            var shape = ClockTile.TimeShape(Clock.At(hour, 7, 3, seconds));
            Assert.Contains(shape, shapes);
        }
        Assert.Contains(ClockTile.TimeShape("14:40:07"), TimeFit.Shapes(seconds, "14:40:07"));
    }
}
