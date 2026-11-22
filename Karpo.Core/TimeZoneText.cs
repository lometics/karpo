// A time zone said short, as its offset from UTC now (2026-10-19, the person: "the timezone value
// should only show UTC + 1 or so, not the complete text"; docs\widgets.md §230): "UTC+1", "UTC+5:30",
// "UTC−3", "UTC". The offset in force on the day — Zürich's "UTC+2" in summer, "UTC+1" in winter —,
// not Windows' standard one in "(UTC+01:00) Amsterdam, Berlin, …". Whatever shows a zone where its
// whole name has no room says it so: a menu's row at its end, a clock's or a date's zone.

using System.Globalization;

namespace Karpo.Core;

public static class TimeZoneText
{
    /// <summary><paramref name="zone"/>'s offset at <paramref name="now"/>, short (see the top of the file).</summary>
    public static string Short(TimeZoneInfo zone, DateTimeOffset now) => Short(zone.GetUtcOffset(now));

    /// <summary>
    /// The zone of Windows' id <paramref name="id"/> — this PC's for none — at <paramref name="now"/>,
    /// short; the id itself where Windows has no such zone.
    /// </summary>
    public static string Short(string? id, DateTimeOffset now) => Zone(id) is { } zone ? Short(zone, now) : id ?? "";

    /// <summary>An offset short: whole hours without minutes ("UTC+1"), minutes after a colon ("UTC+5:30"), a true minus sign ("UTC−3"), none for "UTC".</summary>
    public static string Short(TimeSpan offset)
    {
        const string utc = "UTC"; // not-ui: the same in every language, as Windows writes it
        if (offset == TimeSpan.Zero)
        {
            return utc;
        }
        var sign = offset < TimeSpan.Zero ? '−' : '+';
        var whole = offset.Duration();
        var hours = (int)whole.TotalHours;
        return whole.Minutes == 0
            ? string.Create(CultureInfo.InvariantCulture, $"{utc}{sign}{hours}")
            : string.Create(CultureInfo.InvariantCulture, $"{utc}{sign}{hours}:{whole.Minutes:00}");
    }

    /// <summary>Windows' zone of <paramref name="id"/>, this PC's for none; null where Windows has no such zone.</summary>
    public static TimeZoneInfo? Zone(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return TimeZoneInfo.Local;
        }
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id.Trim());
        }
        catch (Exception error) when (error is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return null;
        }
    }
}
