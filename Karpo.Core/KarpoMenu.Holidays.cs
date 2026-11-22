// §335: "Public holidays ›", the person's holiday calendars: §324's level (OptionHolidays.Rows) — "Add a
// country" (a search, Windows' region's first), each calendar with its region and its bin, asked first —,
// written to the config every calendar shows them from. §351: the one place they are set; a calendar's
// menu links here.
// §423 (the person, 2026-10-30: "the color for the public holidays should be settable per country / entry"): each
// calendar's level has its own colour (OptionHolidays.Rows, "holidays.calendars[].colour"); §338 to §418 had one
// colour of them all under the calendars ("holidays.colour").

namespace Karpo.Core;

public static partial class KarpoMenu
{
    /// <summary>"Public holidays ›": <paramref name="config"/>'s holiday calendars, as a calendar's menu has them, each with its colour.</summary>
    public static IReadOnlyList<OptionRow> Holidays(Config config, KarpoServices services) => OptionHolidays.Rows(config);
}
