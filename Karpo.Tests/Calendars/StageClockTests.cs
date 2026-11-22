// The stage's clock (Karpo.Dock\Clock.cs, KARPO_NOW): the shift that gives a picture its time of day whatever hour it is
// taken at, and the samples' week it then shows (Samples.Accounts.cs): full on the other days, today's meetings clear of
// one another.

using Karpo.Dock;

namespace Karpo.Tests.Calendars;

public sealed class StageClockTests
{
    /// <summary>The samples' meetings of the week of 9 November 2026.</summary>
    private static IEnumerable<CalendarEvent> Week() =>
        Samples.Calendars().SelectMany(each => each.EventsIn(new DateTime(2026, 11, 9), new DateTime(2026, 11, 16), TimeZoneInfo.Local));

    [Theory]
    [InlineData("13:10", 1, 42, 37)]
    [InlineData("13:10", 13, 10, 0)]
    [InlineData("09:05", 23, 59, 59)]
    [InlineData("00:00", 12, 0, 1)]
    public void TheShiftGivesTheTimeAskedForOnTheSameDateAndLeavesTheSecondsAlone(string asked, int hour, int minute, int second)
    {
        var real = new DateTime(2026, 11, 12, hour, minute, second, 250);

        var staged = real + Clock.StageShift(asked, real);

        Assert.Equal(real.Date, staged.Date);
        Assert.Equal(asked, staged.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture));
        // Whole minutes: a minute of the stage's starts when the PC's does.
        Assert.Equal((real.Second, real.Millisecond), (staged.Second, staged.Millisecond));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("noon")]
    [InlineData("25:00")]
    [InlineData("1:5")]
    public void NoTimeAskedForIsNoShift(string? asked) =>
        Assert.Equal(TimeSpan.Zero, Clock.StageShift(asked, new DateTime(2026, 11, 12, 1, 42, 37)));

    /// <summary>Monday to Sunday at ten past one: a next meeting to join, and none of the day's meetings over another.</summary>
    [Theory]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    public void TheSamplesDayHasANextMeetingAndNoTwoAtOnce(int day)
    {
        var now = new DateTime(2026, 11, day, 13, 10, 0);
        using var still = Clock.Still(now);

        var today = Week().Where(each => !each.AllDay && each.Start.Date == now.Date).OrderBy(each => each.Start).ToList();

        Assert.Contains(today, each => each.Start > now && each.Join is not null);
        for (var i = 1; i < today.Count; i++)
        {
            Assert.True(today[i].Start >= today[i - 1].End, $"{today[i - 1].Title} and {today[i].Title} at once");
        }
    }

    /// <summary>Whichever day it is, the other weekdays each hold three meetings or more: a week view never looks empty.</summary>
    [Fact]
    public void TheSamplesOtherWeekdaysAreFull()
    {
        var now = new DateTime(2026, 11, 12, 13, 10, 0);
        using var still = Clock.Still(now);
        var monday = new DateTime(2026, 11, 9);

        var events = Week().Where(each => !each.AllDay).ToList();

        foreach (var day in Enumerable.Range(0, 5).Select(each => monday.AddDays(each)).Where(each => each != now.Date))
        {
            Assert.True(events.Count(each => each.Start.Date == day) >= 3, $"{day:dddd} holds fewer than three meetings");
        }
    }
}
