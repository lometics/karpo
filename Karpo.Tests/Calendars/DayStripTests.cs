// DayStrip (Karpo.Dock\Calendar\DayStrip.cs): the day and week views' hours,
// always the whole day: the hours' maths and where they open, which strip is laid
// out and which days are read ahead.

using Karpo.Dock;

namespace Karpo.Tests.Calendars;

public sealed class DayStripTests
{
    private static double H(double hours) => hours * 60;

    // --- The whole day --------------------------------------------------------------

    /// <summary>
    /// The hours are always the whole day, midnight to midnight, whatever the events (the person: "in
    /// week view it should show the whole day from 0-24 all the time, not only when there is a entry
    /// there"): a margin, 24 hours of one height, a margin.
    /// </summary>
    [Fact]
    public void TheHoursAreTheWholeDay()
    {
        var map = DayStrip.Hours(room: 500, margin: 10, least: 48);

        Assert.Equal(48, map.Hour);
        Assert.Equal(10, map.Y(0));
        Assert.Equal(10 + 8 * 48, map.Y(H(8)));
        Assert.Equal(10 + 24 * 48, map.Y(H(24)));
        Assert.Equal(map.Y(H(24)), map.Bottom);
        Assert.Equal(map.Bottom + 10, map.Height);
        // Taller than the room: the hours scroll in their window.
        Assert.True(map.Height > 500);
    }

    /// <summary>Every hour is as tall as every other: nothing is folded, before the morning or after the evening.</summary>
    [Fact]
    public void EveryHourIsAsTall()
    {
        var map = DayStrip.Hours(room: 500, margin: 10, least: 48);
        for (var hour = 0; hour < 24; hour++)
        {
            Assert.Equal(map.Hour, map.Y(H(hour + 1)) - map.Y(H(hour)));
        }
        // Outside the day, the day's ends.
        Assert.Equal(map.Y(0), map.Y(-30));
        Assert.Equal(map.Bottom, map.Y(H(25)));
    }

    [Fact]
    public void YNeverGoesBackwards()
    {
        var map = DayStrip.Hours(room: 500, margin: 10, least: 48);
        var last = float.MinValue;
        for (var minute = 0; minute <= 1440; minute += 5)
        {
            var y = map.Y(minute);
            Assert.True(y >= last, $"{minute} min is at {y}, above {last}");
            last = y;
        }
    }

    [Theory]
    // Rooms the whole day does not fit at the least: the least, and the hours scroll.
    [InlineData(500, 48)]
    [InlineData(1000, 48)]
    // A room that holds the whole day at more: as much as it holds, whole px, so the day fills it.
    [InlineData(1400, 57)]
    [InlineData(2000, 82)]
    public void AnHourIsTheLeastOrWhatTheRoomHoldsTheWholeDayAt(float room, float hour)
    {
        var map = DayStrip.Hours(room, margin: 10, least: 48);

        Assert.Equal(hour, map.Hour);
        Assert.True(map.Height > room - 24, $"{map.Height} px of hours leave more than an hour's rounding of {room} empty");
    }

    /// <summary>
    /// Hours that scroll start with an hour's line where the unscrolled top's is, the margin under the
    /// window's top, so its label (centred on it) is whole: never in the middle of an hour, which cut
    /// the top label in half under the all-day band (the large-text day and week, 2026-10-04). Near
    /// the end, an hour earlier rather than past what the hours scroll.
    /// </summary>
    [Fact]
    public void ScrolledHoursStartOnAWholeHourLine()
    {
        var map = DayStrip.Hours(room: 300, margin: 12, least: 36);
        var most = map.Height - 300;

        // 7:30 starts at 7:00, its line the margin under the top.
        var scroll = DayStrip.FirstScroll(map, H(7) + 30, most);
        Assert.Equal(map.Top, map.Y(H(7)) - scroll);
        // Past what the hours scroll: the last hour line that still is not, and never more than the most.
        var late = DayStrip.FirstScroll(map, H(23), most);
        Assert.True(late <= most, $"{late} past the most, {most}");
        Assert.True(late > most - map.Hour, $"{late} is more than an hour short of the most, {most}");
        Assert.Contains(Enumerable.Range(0, 24), hour => map.Y(H(hour)) - late == map.Top);
        // Before midnight (twenty past twelve, less the hour): the top.
        Assert.Equal(0, DayStrip.FirstScroll(map, 20 - DayStrip.OpenBefore, most));
    }

    /// <summary>
    /// The hours open an hour before now, as Outlook opens on the time it is: at 14:40 the 13:00 line
    /// is at the window's top, so the hour before the now-line and the hours after it are in view.
    /// </summary>
    [Fact]
    public void TheHoursOpenAnHourBeforeNow()
    {
        var map = DayStrip.Hours(room: 500, margin: 10, least: 48);
        var most = map.Height - 500;

        var scroll = DayStrip.FirstScroll(map, H(14) + 40 - DayStrip.OpenBefore, most);

        Assert.Equal(map.Top, map.Y(H(13)) - scroll);
        Assert.InRange(map.Y(H(14) + 40) - scroll, map.Hour, 500);
    }

    // --- The strip and what is read ahead -------------------------------------------

    private static readonly DateOnly Monday = new(2026, 9, 21);

    [Fact]
    public void ANewStripSparesAWeekEitherSideOfTheWeek()
    {
        Assert.Equal(Monday.AddDays(-7), DayStrip.Origin(null, Monday, 7, settled: true));
        Assert.Equal(Monday.AddDays(-3), DayStrip.Origin(null, Monday, 1, settled: true));
    }

    [Theory]
    // While sliding the strip is kept with a day to spare either side of the window.
    [InlineData(6, false, true)]
    [InlineData(13, false, true)]
    [InlineData(14, false, false)]
    [InlineData(0, false, false)]
    // At rest only with three days to spare, then laid out around the window.
    [InlineData(3, true, true)]
    [InlineData(11, true, true)]
    [InlineData(2, true, false)]
    [InlineData(12, true, false)]
    public void TheStripIsKeptWhileTheWindowIsWellInside(int windowAt, bool settled, bool kept)
    {
        // A strip of 21 days; the window seven of them from day windowAt.
        var strip = Monday.AddDays(-7);
        var first = strip.AddDays(windowAt);

        var origin = DayStrip.Origin(strip, first, 7, settled);

        Assert.Equal(kept ? strip : first.AddDays(-7), origin);
    }

    [Fact]
    public void TheDaysBeyondTheStripAreReadAhead()
    {
        // The week's strip is 21 days from a week before; a week more either side is asked for.
        var (start, end) = DayStrip.Prefetch(Monday.AddDays(-7), 21, 7);

        Assert.Equal(Monday.AddDays(-14), start);
        Assert.Equal(Monday.AddDays(21), end);
        // The day view: three either side of its seven.
        Assert.Equal((Monday.AddDays(-6), Monday.AddDays(7)), DayStrip.Prefetch(Monday.AddDays(-3), 7, 1));
    }

    [Fact]
    public void AStripAtTheEndOfTimeStaysInIt()
    {
        Assert.Equal(DateOnly.MinValue, DayStrip.Origin(null, DateOnly.MinValue, 7, settled: true));
        Assert.Equal(DateOnly.MaxValue, DayStrip.Prefetch(DateOnly.MaxValue.AddDays(-3), 21, 7).End);
    }

    [Theory]
    [InlineData(3, 0)]
    [InlineData(-1, -1)]
    [InlineData(7, 1)]
    [InlineData(10, 4)]
    public void ADayIsRevealedByAsLittleAsShowsIt(int day, int firstAfter) =>
        Assert.Equal(Monday.AddDays(firstAfter), DayStrip.Reveal(Monday, 7, Monday.AddDays(day)));
}
