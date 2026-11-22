// When an alarm rings, as Windows 11's Clock app says it on its alarm cards (AlarmTile, docs\widgets.md §187,
// §196): "Rings in 8 h 23 min" — the first part of the alarm line's caption while it is set (§401).
//
// Pure, so every rule has a test (Karpo.Tests\Calendars\AlarmCardTextTests).

using Karpo.Core;

namespace Karpo.Dock;

internal static class AlarmCardText
{
    /// <summary>
    /// "Rings in 8 h 23 min": how long until <paramref name="until"/> ends, a minute begun counted
    /// whole — the days from a day up and the hours after them, else the hours and the minutes, each
    /// left out where it is nought; never under a minute.
    /// </summary>
    public static string RingsIn(TimeSpan until)
    {
        var minutes = Math.Max(1, (long)Math.Ceiling(until.TotalMinutes));
        var days = (int)(minutes / (24 * 60));
        var hours = (int)(minutes / 60 % 24);
        var rest = (int)(minutes % 60);
        var parts = new List<string>();
        if (days > 0)
        {
            parts.Add(S.WidgetRssAgeDays(days));
        }
        if (hours > 0)
        {
            parts.Add(S.DockClockHours(hours));
        }
        if (rest > 0 && days == 0)
        {
            parts.Add(S.DockClockMinutes(rest));
        }
        return S.DockAlarmRingsIn(string.Join(" ", parts));
    }
}
