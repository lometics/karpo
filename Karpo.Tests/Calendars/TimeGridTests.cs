// TimeGrid (Karpo.Dock\Calendar\TimeGrid.cs): the day and week views' hours as
// numbers. Events side by side where they overlap, the all-day row's lanes, and
// events over midnight split per day. Where the hours start scrolled is
// DayStripTests' (an hour before now).

using Karpo.Dock;

namespace Karpo.Tests.Calendars;

public sealed class TimeGridTests
{
    private static (double Start, double End) Hours(double from, double to) => (from * 60, to * 60);

    private static Placement[] Arrange(params (double Start, double End)[] items) => TimeGrid.Arrange(items, minimum: 30);

    [Fact]
    public void AnEventAloneTakesTheWholeColumn()
    {
        Assert.Equal([new Placement(0, 1, 1)], Arrange(Hours(9, 10)));
    }

    [Fact]
    public void EventsThatDontOverlapEachTakeTheWholeColumn()
    {
        // One ending as the next starts does not overlap it.
        Assert.Equal([new Placement(0, 1, 1), new Placement(0, 1, 1)], Arrange(Hours(9, 10), Hours(10, 11)));
    }

    [Fact]
    public void TwoOverlappingEventsSitSideBySide()
    {
        Assert.Equal([new Placement(0, 2, 1), new Placement(1, 2, 1)], Arrange(Hours(9, 11), Hours(10, 12)));
    }

    [Fact]
    public void AChainOfOverlapsSharesColumnsWhereItCan()
    {
        // 9–11 and 11–13 never meet: they share the first column beside 10–12.
        var placed = Arrange(Hours(9, 11), Hours(10, 12), Hours(11, 13));

        Assert.Equal(new Placement(0, 2, 1), placed[0]);
        Assert.Equal(new Placement(1, 2, 1), placed[1]);
        Assert.Equal(new Placement(0, 2, 1), placed[2]);
    }

    [Fact]
    public void TheLongerOfTwoEventsThatStartTogetherComesFirst()
    {
        var placed = Arrange(Hours(9, 10), Hours(9, 12));

        Assert.Equal(new Placement(1, 2, 1), placed[0]);
        Assert.Equal(new Placement(0, 2, 1), placed[1]);
    }

    [Fact]
    public void AnEventReachesRightOverColumnsWithNothingItOverlaps()
    {
        // 9–12 takes the first column, 9–10 and 9:30–10 the next two; 11–12 fits in the second
        // column and has nothing in the third, so it spans both.
        var placed = Arrange(Hours(9, 12), Hours(9, 10), Hours(9.5, 10), Hours(11, 12));

        Assert.Equal(new Placement(0, 3, 1), placed[0]);
        Assert.Equal(new Placement(1, 3, 1), placed[1]);
        Assert.Equal(new Placement(2, 3, 1), placed[2]);
        Assert.Equal(new Placement(1, 3, 2), placed[3]);
    }

    [Fact]
    public void GroupsThatDontTouchAreLaidOutApart()
    {
        var placed = Arrange(Hours(9, 10), Hours(9, 10), Hours(14, 15));

        Assert.Equal(2, placed[0].Columns);
        Assert.Equal(2, placed[1].Columns);
        Assert.Equal(new Placement(0, 1, 1), placed[2]);
    }

    [Fact]
    public void AShortEventIsLaidOutAsLongAsItIsDrawn()
    {
        // Five minutes, drawn as half an hour: the event at 9:15 overlaps it on screen.
        var placed = Arrange((9 * 60, 9 * 60 + 5), Hours(9.25, 10));

        Assert.Equal(2, placed[0].Columns);
        Assert.NotEqual(placed[0].Column, placed[1].Column);
    }

    [Fact]
    public void AnEventWithoutALengthIsDrawnAtItsStart()
    {
        var placed = Arrange((600, 600), (600, 600));

        Assert.Equal([new Placement(0, 2, 1), new Placement(1, 2, 1)], placed);
    }

    [Fact]
    public void AllDayBarsTakeTheFirstFreeLane()
    {
        // Monday–Wednesday, Tuesday, Thursday–Friday: the Thursday one fits in the first lane again.
        var (lane, count) = TimeGrid.Lanes([(0, 3), (1, 2), (3, 5)]);

        Assert.Equal([0, 1, 0], lane);
        Assert.Equal(2, count);
    }

    [Fact]
    public void TheLongerBarThatStartsTheSameDayGoesFirst()
    {
        var (lane, count) = TimeGrid.Lanes([(0, 1), (0, 7)]);

        Assert.Equal([1, 0], lane);
        Assert.Equal(2, count);
    }

    [Fact]
    public void AnEventOverMidnightHasAPartOnEachDay()
    {
        var night = new CalendarEvent(new DateTime(2026, 9, 21, 22, 0, 0), new DateTime(2026, 9, 22, 2, 0, 0), false, "Night", null);

        var parts = TimeGrid.Segments([night], new DateOnly(2026, 9, 21), 7);

        Assert.Equal([new Segment(0, 22 * 60, 24 * 60, night), new Segment(1, 0, 120, night)], parts);
    }

    [Fact]
    public void AllDayEventsAndThoseOfADayOrMoreGoToTheAllDayRow()
    {
        var first = new DateOnly(2026, 9, 21);
        var holiday = new CalendarEvent(new DateTime(2026, 9, 22), new DateTime(2026, 9, 23), true, "Holiday", null);
        var conference = new CalendarEvent(new DateTime(2026, 9, 23, 10, 0, 0), new DateTime(2026, 9, 25, 16, 0, 0), false, "Conference", null);
        // From the week before to the week after: all seven days.
        var trip = new CalendarEvent(new DateTime(2026, 9, 18), new DateTime(2026, 10, 2), true, "Trip", null);
        var meeting = new CalendarEvent(new DateTime(2026, 9, 22, 9, 0, 0), new DateTime(2026, 9, 22, 10, 0, 0), false, "Meeting", null);

        var (bars, lanes) = TimeGrid.AllDayBars([holiday, conference, trip, meeting], first, 7);

        Assert.Equal(3, bars.Count);
        Assert.Equal((1, 2), (bars[0].First, bars[0].Last));
        Assert.Equal((2, 5), (bars[1].First, bars[1].Last));
        Assert.Equal((0, 7), (bars[2].First, bars[2].Last));
        Assert.Equal(0, bars[2].Lane);
        Assert.Equal(2, lanes);
        Assert.Empty(TimeGrid.Segments([holiday, conference, trip], first, 7));
        Assert.Single(TimeGrid.Segments([meeting], first, 7));
    }
}
