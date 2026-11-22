// The calendar's settings that went (2026-10-05), in the person's words: "show events for shoudl
// not be a configuration option", "there should not be a label setting for the calendar", "there
// should not be a calendar size option". Read from the rows a widget's menu has (OptionRows; the
// settings app's widget page that showed them is gone, §228): none of the three is a row of the
// calendar's, and the label that stays is the clock's alone (a clock names its place, "Tokyo").

namespace Karpo.Tests.Core;

public sealed class CalendarCardsTests
{
    /// <summary>Every row of every widget's menu.</summary>
    private static List<OptionRow> Rows() =>
        [.. Enum.GetValues<ListKind>().Select(kind => (WidgetItem)kind.New())
            .Concat(Enum.GetValues<BarKind>().Select(kind => (WidgetItem)kind.New()))
            .SelectMany(item => OptionRows.Of(item))];

    [Fact]
    public void TheCalendarsRemovedCardsAreGone()
    {
        var titles = Rows().Select(row => row.Title).ToList();

        Assert.DoesNotContain("Show events for", titles);
        Assert.DoesNotContain("Calendar size", titles);
        // The view went with the calendar's flyout (2026-10-09): each view is a line of its own. (A
        // folder's contents' View, File Explorer's views (§497), and a sports line's (§484) are theirs.)
        ListKind[] calendars = [ListKind.Calendar, ListKind.Month, ListKind.DateHeader, ListKind.Events, ListKind.DayView, ListKind.WeekView, ListKind.YearView];
        Assert.DoesNotContain(calendars.SelectMany(kind => OptionRows.Of(kind.New())), row => row.Title == "View");
        // §493: the agenda's Days ahead is a line's own setting, no card of the calendar's flyout.
        Assert.DoesNotContain(Rows(), row => row.Key.StartsWith("Calendar", StringComparison.Ordinal) && row.Key != OptionRows.CalendarDaysAhead);
    }

    /// <summary>The one label left is the clock's: a row of a clock only, never of the calendar's date.</summary>
    [Fact]
    public void TheLabelIsTheClocksAlone()
    {
        Assert.Contains(OptionRows.Of(ListKind.Clock.New()), row => row.Key == OptionRows.ClockName && row.Title == "Name");
        foreach (var kind in Enum.GetValues<ListKind>().Where(kind => kind != ListKind.Clock))
        {
            Assert.DoesNotContain(OptionRows.Of(kind.New()), row => row.Title == "Label");
        }
    }
}
