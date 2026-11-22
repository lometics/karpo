// What the battery is doing and how long it has, in the words Windows 11's battery flyout uses: the
// battery's line says them (BatteryIsland). They were the battery flyout's, which went with the
// battery widget when it became the battery preset (2026-10-10, docs\widgets.md §94).

using Karpo.Dock.QuickSettings.Services;

namespace Karpo.Dock.QuickSettings;

internal static class BatteryText
{
    /// <summary>What the battery is doing, as Windows' battery flyout says it.</summary>
    internal static string StatusText(BatteryState state) => state.PluggedIn
        ? state.Percent >= 100 ? S.DockQuickBatteryFullyCharged : state.Charging ? S.DockQuickBatteryCharging : S.DockQuickBatteryNotCharging
        : S.DockQuickBatteryOnBattery;

    /// <summary>"About 3 h 17 min remaining", "About 45 min until fully charged"; null when Windows doesn't know.</summary>
    internal static string? TimeText(BatteryState state)
    {
        if (!state.PluggedIn && state.Remaining is { } remaining)
        {
            return S.DockQuickBatteryRemaining(Span(remaining));
        }
        if (state.PluggedIn && state.Charging && state.UntilFull is { } until)
        {
            return S.DockQuickBatteryUntilFull(Span(until));
        }
        return null;
    }

    /// <summary>A time in hours and minutes, rounded to the minute: "3 h 17 min", "45 min", "1 min".</summary>
    internal static string Span(TimeSpan span)
    {
        var minutes = Math.Max(1, (int)Math.Round(span.TotalMinutes, MidpointRounding.AwayFromZero));
        var hours = minutes / 60;
        minutes %= 60;
        return hours == 0
            ? S.DockQuickBatteryMinutes(minutes)
            : minutes == 0 ? S.DockQuickBatteryHours(hours) : S.DockQuickBatteryHoursMinutes(hours: hours, minutes: minutes);
    }
}
