// The calendar's line in a folder lists every event of every day it names — today, tomorrow and
// the weekdays after, a week of them where nobody set its Days ahead (CalendarIsland.AgendaDays) —
// whatever its hour, and none of a day past them. How far ahead it looks was a setting until
// 2026-10-05 and is one again since 2026-11-03 (§496, AgendaDaysAheadTests).

using Karpo.Dock;
using Karpo.Tests.Core;
using Karpo.Tests.Islands;

namespace Karpo.Tests.Calendars;

public sealed class CalendarAgendaTests
{
    private static readonly DateTime Now = new(2026, 9, 22, 14, 40, 0);

    private static CalendarEvent At(string title, DateTime start, double hours) => new(start, start.AddHours(hours), false, title, null);

    private static readonly List<CalendarEvent> Events =
    [
        At("Standup", new DateTime(2026, 9, 22, 9, 30, 0), 0.25),
        At("Focus time", new DateTime(2026, 9, 22, 14, 0, 0), 2),
        new(new DateTime(2026, 9, 22), new DateTime(2026, 9, 23), true, "Autumn equinox", null),
        At("Late call", new DateTime(2026, 9, 22, 23, 30, 0), 0.25),
        At("Night bus", new DateTime(2026, 9, 23, 0, 15, 0), 0.5),
        At("Dentist", new DateTime(2026, 9, 24, 8, 0, 0), 1),
        At("Gym", new DateTime(2026, 9, 28, 6, 0, 0), 1),
        At("Last train", new DateTime(2026, 9, 28, 23, 0, 0), 0.9),
        At("Planning", new DateTime(2026, 9, 29, 9, 0, 0), 1),
        At("Review", new DateTime(2026, 10, 14, 10, 0, 0), 1),
    ];

    [UiFact]
    public void TheCardListsEveryEventOfEveryDayItNames()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var store = new CalendarStore();
            store.Add(new ListCalendar("test:agenda", "Agenda", default, Events));
            CalendarIsland.Made = store;
            try
            {
                foreach (var file in new[]
                {
                    """{ "id": "c", "kind": "calendar" }""",
                    // A week set as such is the week nobody set.
                    """{ "id": "c", "kind": "calendar", "calendar": { "daysAhead": 7 } }""",
                })
                {
                    var folder = $$"""{ "id": "f", "kind": "folderList", "folder": { "lists": [ [ { "island": {{file}} } ] ] } }""";
                    var entry = Configs.Islands(Configs.Repaired($$"""{ "layouts": [ { "id": "default", "name": "Default", "islands": [ {{folder}} ] } ] }"""))[0].Folder!.Cells[0].Island;
                    using var island = new CalendarIsland("test/agenda", entry, gfx, new Config());
                    var titles = island.Upcoming(Now).Select(item => item.Title).ToList();

                    // Today's that have not ended, the all-day one among them; every one of the six
                    // days after, at any hour; none of the eighth day or later.
                    Assert.Equal(
                        ["Autumn equinox", "Dentist", "Focus time", "Gym", "Last train", "Late call", "Night bus"],
                        titles.Order(StringComparer.Ordinal));
                    Assert.Equal(7, island.AgendaDays);
                }
            }
            finally
            {
                CalendarIsland.Made = null;
            }
        });
    }
}
