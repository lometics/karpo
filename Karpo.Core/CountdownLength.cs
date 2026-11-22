// A timer's or a focus session's length and name on its settings page, and the clock's two lengths:
// "The Settings page edits the length with number fields, in whole minutes from 1 minute to 12
// hours" (2026-09-27). The length is the one its steppers set on its face, kept in the same option
// (CountdownOptions, ClockOptions), so the page and the widget can never disagree about it. And a
// focus session's "Turn on do not disturb", on unless somebody turned it off. Nothing about a
// running countdown is shown here: the page shows settings.

namespace Karpo.Core;

public static class CountdownLength
{
    /// <summary>The least and the most a field takes: a minute, and twelve hours of them.</summary>
    public const int Fewest = CountdownOptions.FewestMinutes;

    public const int Most = CountdownOptions.MostMinutes;

    /// <summary>
    /// What a number field's <paramref name="value"/> sets a length to: whole minutes, a minute to
    /// twelve hours. Null where it sets nothing — the field emptied, or given something that is not
    /// a number — and the field then shows the length it has again.
    /// </summary>
    public static int? Minutes(double value) =>
        double.IsFinite(value) ? (int)Math.Clamp(Math.Round(value, MidpointRounding.AwayFromZero), Fewest, Most) : null;

    /// <summary>The length <paramref name="widget"/> starts at, as its field shows it: its own, or its kind's where it has none.</summary>
    public static int Of(WidgetItem widget) =>
        CountdownOptions.Clamp(widget.Countdown?.Minutes, KindMinutes(widget));

    /// <summary>What a new widget of <paramref name="widget"/>'s kind starts at: five for a timer, 25 for a focus session.</summary>
    public static int KindMinutes(WidgetItem widget) =>
        widget.Info == WidgetInfos.Focus ? CountdownOptions.FocusMinutes : CountdownOptions.TimerMinutes;

    /// <summary>A name as it is kept: trimmed, and empty for its kind's own.</summary>
    public static string Name(string? text) => (text ?? "").Trim();

    /// <summary>
    /// A focus session's "Turn on do not disturb" as it is kept: nothing for on, which it is unless
    /// somebody turned it off, and false for off — so a file says it only where somebody did.
    /// </summary>
    public static bool? DoNotDisturb(bool on) => on ? null : false;
}
