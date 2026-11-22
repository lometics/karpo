// A timer's and a focus session's length and name on the Settings page (Karpo.Core\
// CountdownLength.cs): whole minutes from a minute to twelve hours in a number field, the length a
// widget nobody set starts at, and a name kept trimmed.


namespace Karpo.Tests.Core;

public sealed class CountdownLengthTests
{
    /// <summary>
    /// A number field's value is whole minutes, a minute to twelve hours: a fraction is the whole
    /// number it rounds to, a number past either end the end, and an emptied field — NaN — sets nothing.
    /// </summary>
    [Theory]
    [InlineData(25, 25)]
    [InlineData(1, 1)]
    [InlineData(720, 720)]
    [InlineData(0, 1)]
    [InlineData(-3, 1)]
    [InlineData(721, 720)]
    [InlineData(9999, 720)]
    [InlineData(4.4, 4)]
    [InlineData(4.5, 5)]
    public void AFieldSetsWholeMinutesAMinuteToTwelveHours(double value, int minutes) =>
        Assert.Equal(minutes, CountdownLength.Minutes(value));

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void AFieldThatIsNoNumberSetsNothing(double value) => Assert.Null(CountdownLength.Minutes(value));

    /// <summary>A widget nobody set shows its kind's length; one that was set, its own.</summary>
    [Fact]
    public void TheFieldShowsItsOwnLengthOrItsKinds()
    {
        var timer = ListKind.Timer.New();
        var focus = ListKind.Focus.New();
        Assert.Null(timer.Countdown);
        Assert.Equal(CountdownOptions.TimerMinutes, CountdownLength.Of(timer));
        Assert.Equal(CountdownOptions.FocusMinutes, CountdownLength.Of(focus));

        timer.Countdown = new CountdownOptions { Minutes = 90 };
        Assert.Equal(90, CountdownLength.Of(timer));
        // A record with a name and no length yet: the kind's length.
        focus.Countdown = new CountdownOptions { Name = "Deep work" };
        Assert.Equal(CountdownOptions.FocusMinutes, CountdownLength.Of(focus));
    }

    [Theory]
    [InlineData("Tea", "Tea")]
    [InlineData("  Deep work ", "Deep work")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData(null, "")]
    public void ANameIsKeptTrimmed(string? typed, string kept) => Assert.Equal(kept, CountdownLength.Name(typed));
}
