// The day and week views as a strip of days that slides sideways, over the
// whole day's hours. Pure, so every rule has a test
// (Karpo.Tests\Calendars\DayStripTests.cs).
//
// The hours: always the whole day, midnight to midnight, whatever the events,
// as Windows' and Outlook's calendars have them. The person: "in week view it
// should show the whole day from 0-24 all the time, not only when there is a
// entry there" (2026-10-05). They used to be only the hours the events needed,
// the rest folded, and that made the panel taller or shorter as the days slid
// under it. An hour is as tall as reads well (a least), more where the room
// holds the whole day at more, whole px so the lines sit on pixels; the hours
// scroll in a window of their own, which is as tall as the panel's room and
// never follows the events, so the panel keeps its height as the days slide
// and the hours scroll.
//
// The days: the visible window (seven days, or one) is part of a longer strip
// laid out and painted once, with days to spare on either side, so a slide is
// the compositor moving a surface and nothing has to be drawn while it moves.
// The strip is laid out again around the window when the window comes near its
// end, and at rest. The events of a margin beyond the strip are asked for as
// well, so the calendars read them before they come into view.

namespace Karpo.Dock;

/// <summary>
/// Where a minute of the day is in the hours, px from the top of the scrolling part: a margin of
/// <paramref name="Top"/>, the whole day from midnight to midnight at <paramref name="Hour"/> px an
/// hour, and a margin as tall under it.
/// </summary>
internal readonly record struct HourMap(float Top, float Hour)
{
    /// <summary>Midnight at the day's end: the last hour's foot.</summary>
    public float Bottom => Top + 24 * Hour;

    /// <summary>All of it, with a margin as tall as the top's under it.</summary>
    public float Height => Bottom + Top;

    /// <summary>The y of <paramref name="minute"/>, by the hour; within the day.</summary>
    public float Y(double minute) => Top + (float)(Math.Clamp(minute, 0, TimeGrid.DayMinutes) / 60 * Hour);
}

internal static class DayStrip
{
    /// <summary>Days laid out on either side of the window: a week's either side of a week, three of a day.</summary>
    public static int Spare(int visible) => visible > 1 ? 7 : 3;

    /// <summary>The minutes before now the hours open on: about an hour, so the hour before the now-line is in view over it.</summary>
    public const double OpenBefore = 60;

    /// <summary>
    /// The whole day's hours for a window <paramref name="room"/> px tall: a margin of
    /// <paramref name="margin"/> above and below, and an hour <paramref name="least"/> px tall, or as
    /// tall as the room allows the whole day where that is more, in whole px. The room is the panel's
    /// and not the events', so the hours are the same whatever days are shown.
    /// </summary>
    public static HourMap Hours(float room, float margin, float least)
    {
        var hour = MathF.Max(least, MathF.Floor((room - 2 * margin) / 24));
        return new HourMap(margin, hour);
    }

    /// <summary>
    /// Where the hours start scrolled to, to show <paramref name="minute"/> on: the line of the hour it
    /// is in at the window's top, the margin over it, so that hour's label is whole and not cut in
    /// half by the band above; an hour earlier for as long as that is further down than the hours
    /// scroll (<paramref name="most"/>), so the last hours fill the window and still no label is cut.
    /// </summary>
    public static float FirstScroll(HourMap map, double minute, float most)
    {
        var hour = Math.Floor(Math.Clamp(minute, 0, TimeGrid.DayMinutes) / 60);
        float At(double whole) => PopupPaint.Round(map.Y(whole * 60) - map.Top);
        while (hour > 0 && At(hour) > most)
        {
            hour--;
        }
        return Math.Clamp(At(hour), 0, MathF.Max(0, most));
    }

    /// <summary>
    /// The strip's first day, for a window of <paramref name="visible"/> days from
    /// <paramref name="first"/>: <paramref name="current"/> while the window is in it with a day to
    /// spare either side (a slide in between may show part of the next day), and at rest with half
    /// the spare days either side; else a new strip with the spare days either side of the window.
    /// </summary>
    public static DateOnly Origin(DateOnly? current, DateOnly first, int visible, bool settled)
    {
        var spare = Spare(visible);
        var fresh = Add(first, -spare);
        if (current is not { } strip)
        {
            return fresh;
        }
        var margin = settled ? Math.Max(1, spare / 2) : 1;
        var days = visible + 2 * spare;
        var before = first.DayNumber - strip.DayNumber;
        var after = strip.DayNumber + days - (first.DayNumber + visible);
        return before >= margin && after >= margin ? strip : fresh;
    }

    /// <summary>
    /// The days whose events are asked for with a strip of <paramref name="days"/> from
    /// <paramref name="first"/>, [start, end): the strip, and as many days again as it spares on
    /// either side beyond it, so what is about to come into view has been read.
    /// </summary>
    public static (DateOnly Start, DateOnly End) Prefetch(DateOnly first, int days, int visible)
    {
        var spare = Spare(visible);
        return (Add(first, -spare), Add(first, days + spare));
    }

    /// <summary>
    /// The window's first day once <paramref name="day"/> is in it: the same when it already is, else
    /// moved by as little as shows it.
    /// </summary>
    public static DateOnly Reveal(DateOnly first, int visible, DateOnly day) =>
        day < first ? day
        : day.DayNumber >= first.DayNumber + visible ? Add(day, 1 - visible)
        : first;

    /// <summary><paramref name="day"/> <paramref name="days"/> on, kept within what DateOnly holds.</summary>
    public static DateOnly Add(DateOnly day, long days) =>
        DateOnly.FromDayNumber((int)Math.Clamp(day.DayNumber + days, DateOnly.MinValue.DayNumber, DateOnly.MaxValue.DayNumber));
}
