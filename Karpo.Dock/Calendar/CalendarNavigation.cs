// How one moves through the calendar's views: which days a view shows, what a
// page turn or a key does to the day shown and the day selected, and the week
// numbers as the user's region counts them. Pure, so every rule has a test
// (Karpo.Tests\Calendar).
//
// The views themselves are Karpo.Core's (CalendarView), because which one a
// widget's panel opens in is the widget's setting, and settings live in Core.
// There is no switching between them here: the panel shows the view it opened in.

using Karpo.Core;

namespace Karpo.Dock;

/// <summary>A key that moves through the calendar.</summary>
public enum CalendarKey
{
    Left,
    Right,
    Up,
    Down,
    PageUp,
    PageDown,
    Home,
}

internal static class CalendarNavigation
{
    private static readonly DateOnly Earliest = new(1, 1, 1);
    private static readonly DateOnly Latest = new(9999, 12, 31);

    /// <summary>The first day of the week that holds <paramref name="day"/>, weeks starting on <paramref name="firstDay"/>.</summary>
    public static DateOnly WeekStart(DateOnly day, DayOfWeek firstDay)
    {
        var back = ((int)day.DayOfWeek - (int)firstDay + 7) % 7;
        return day.DayNumber - back < Earliest.DayNumber ? Earliest : day.AddDays(-back);
    }

    /// <summary>
    /// The days <paramref name="view"/> shows on the page with <paramref name="shown"/>, [start, end):
    /// the day, its week, the month's six-week grid (the days of the months either side included), or
    /// its year.
    /// </summary>
    public static (DateOnly Start, DateOnly End) Range(CalendarView view, DateOnly shown, DayOfWeek firstDay) => view switch
    {
        CalendarView.Day => (shown, Add(shown, 1)),
        CalendarView.Week => (WeekStart(shown, firstDay), Add(WeekStart(shown, firstDay), 7)),
        CalendarView.Month => (CalendarGrid.First(shown.Year, shown.Month, firstDay), Add(CalendarGrid.First(shown.Year, shown.Month, firstDay), 42)),
        _ => (new DateOnly(shown.Year, 1, 1), shown.Year < 9999 ? new DateOnly(shown.Year + 1, 1, 1) : Latest),
    };

    /// <summary>
    /// Whether <paramref name="day"/> is on the page with <paramref name="shown"/>: the same day, week,
    /// month (the grid's days of other months are not) or year.
    /// </summary>
    public static bool OnPage(CalendarView view, DateOnly shown, DateOnly day, DayOfWeek firstDay) => view switch
    {
        CalendarView.Day => day == shown,
        CalendarView.Week => WeekStart(day, firstDay) == WeekStart(shown, firstDay),
        CalendarView.Month => day.Year == shown.Year && day.Month == shown.Month,
        _ => day.Year == shown.Year,
    };

    /// <summary>
    /// <paramref name="shown"/> <paramref name="pages"/> pages on (back when negative): days, weeks,
    /// months or years, the day of the month kept where the month has it (else its last).
    /// </summary>
    public static DateOnly Turn(CalendarView view, DateOnly shown, int pages) => view switch
    {
        CalendarView.Day => Add(shown, pages),
        CalendarView.Week => Add(shown, 7L * pages),
        CalendarView.Month => CalendarGrid.AddMonths(shown, pages),
        _ => CalendarGrid.AddMonths(shown, 12 * pages),
    };

    /// <summary>
    /// The day selected after <paramref name="key"/>, or null when the key moves no day: the arrows
    /// move a day (a month in the year view); up and down a week in the month view, a row of
    /// <paramref name="yearColumns"/> months in the year view, and scroll the hours in the day and
    /// week views (where they scroll at all); Page Up and Page Down turn a month in the month view
    /// and a year in the year view; Home goes to <paramref name="today"/>.
    /// <para>
    /// In the day and week views the days slide by: the arrows and Page Up and Down move a day, as a
    /// notch of the wheel does, and a week with <paramref name="wide"/> (Ctrl or Shift held); the
    /// days shown go along with the selection (CalendarViewTile).
    /// </para>
    /// </summary>
    public static DateOnly? Key(CalendarView view, DateOnly selected, CalendarKey key, DateOnly today, int yearColumns = 4, bool wide = false) => key switch
    {
        CalendarKey.Home => today,
        CalendarKey.Left or CalendarKey.Right or CalendarKey.PageUp or CalendarKey.PageDown when view is CalendarView.Day or CalendarView.Week =>
            Add(selected, (key is CalendarKey.Left or CalendarKey.PageUp ? -1 : 1) * (wide ? 7 : 1)),
        CalendarKey.Left or CalendarKey.Right when view == CalendarView.Year =>
            CalendarGrid.AddMonths(selected, key == CalendarKey.Left ? -1 : 1),
        CalendarKey.Left => Add(selected, -1),
        CalendarKey.Right => Add(selected, 1),
        CalendarKey.Up or CalendarKey.Down => view switch
        {
            CalendarView.Month => Add(selected, key == CalendarKey.Up ? -7 : 7),
            CalendarView.Year => CalendarGrid.AddMonths(selected, (key == CalendarKey.Up ? -1 : 1) * Math.Max(1, yearColumns)),
            _ => null,
        },
        CalendarKey.PageUp or CalendarKey.PageDown => view switch
        {
            CalendarView.Month => CalendarGrid.AddMonths(selected, key == CalendarKey.PageUp ? -1 : 1),
            _ => CalendarGrid.AddMonths(selected, key == CalendarKey.PageUp ? -12 : 12),
        },
        _ => null,
    };

    /// <summary>
    /// The week of the year <paramref name="day"/> is in, as the region counts
    /// (LOCALE_IFIRSTWEEKOFYEAR): <paramref name="rule"/> 0, week 1 holds January 1st; 1, week 1 is
    /// the first whole week; 2, week 1 is the first week with at least four days of the year (ISO
    /// 8601 with Monday). The days of a week that crosses the new year have one number.
    /// </summary>
    public static int WeekOfYear(DateOnly day, DayOfWeek firstDay, int rule)
    {
        var start = FirstWeek(day.Year, firstDay, rule);
        if (day < start && day.Year > 1)
        {
            start = FirstWeek(day.Year - 1, firstDay, rule);
        }
        else if (day.Year < 9999 && day >= FirstWeek(day.Year + 1, firstDay, rule))
        {
            start = FirstWeek(day.Year + 1, firstDay, rule);
        }
        return Math.Max(1, (day.DayNumber - start.DayNumber) / 7 + 1);
    }

    /// <summary>Where week 1 of <paramref name="year"/> starts.</summary>
    private static DateOnly FirstWeek(int year, DayOfWeek firstDay, int rule)
    {
        var january = new DateOnly(year, 1, 1);
        // The days of January 1st's week that are still in December.
        var before = ((int)january.DayOfWeek - (int)firstDay + 7) % 7;
        var holding = january.DayNumber - before < Earliest.DayNumber ? Earliest : january.AddDays(-before);
        return rule switch
        {
            0 => holding,
            1 => before == 0 ? january : holding.AddDays(7),
            _ => 7 - before >= 4 ? holding : holding.AddDays(7),
        };
    }

    /// <summary><paramref name="day"/> <paramref name="days"/> days on, kept within what DateOnly holds.</summary>
    private static DateOnly Add(DateOnly day, long days) =>
        DateOnly.FromDayNumber((int)Math.Clamp(day.DayNumber + days, Earliest.DayNumber, Latest.DayNumber));
}

/// <summary>The month grid's dates.</summary>
internal static class CalendarGrid
{
    /// <summary>The date in the first cell of a six-week grid for the month: on or before its first day, on <paramref name="firstDay"/>.</summary>
    public static DateOnly First(int year, int month, DayOfWeek firstDay)
    {
        var start = new DateOnly(year, month, 1);
        var back = ((int)start.DayOfWeek - (int)firstDay + 7) % 7;
        return start.DayNumber - back < 0 ? start : start.AddDays(-back);
    }

    /// <summary><paramref name="day"/> <paramref name="months"/> months on, the day of the month kept where the month has it, else its last.</summary>
    public static DateOnly AddMonths(DateOnly day, int months)
    {
        // Months since year 0, kept within what DateOnly holds.
        var total = Math.Clamp(day.Year * 12L + (day.Month - 1) + months, 12, 9999 * 12 + 11);
        var year = (int)(total / 12);
        var month = (int)(total % 12) + 1;
        return new DateOnly(year, month, Math.Min(day.Day, DateTime.DaysInMonth(year, month)));
    }
}
