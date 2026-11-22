// The sun (docs\widgets.md §368): sunrise, sunset, civil dawn and dusk, solar noon and the sun's path
// across the sky, worked out on this PC from a latitude, a longitude and an instant — no network, no
// ephemeris file, no time-zone database. Ported from Themia's astronomy (Themia.Sdk\Astronomy:
// SolarPosition, SkyPath, SunPathGeometry; Themia.Widget.Sun\SunDayBar; the person's own app), which
// keeps its shape: the day is the *solar* day at the place's longitude, an absent event is a polar day
// or night rather than a made-up time, the next event is the earliest still ahead, and nothing here
// reads the clock, so every case is a test.
//
// **What differs from Themia.** Themia's sunrise equation reads the sun's declination and the equation
// of time once, at the day's transit, which is up to a minute and a half off in the middle latitudes
// and several minutes near the polar circles. Here they are NOAA's (Meeus's low-precision series: the
// sun's true longitude, the nutation in it, the obliquity), read again at each event's own instant
// until it settles (three or four rounds): within a few seconds of the US Naval Observatory's tables
// wherever the sun rises at all, which the tests hold it to.
//
// Times come out as instants (UTC); a widget writes them in this PC's zone, as Themia does: without a
// zone database the place's own clock is not known, and for the places a person looks at it is theirs.

using System.Globalization;
using Karpo.Core;
using Karpo.Dock.Panels;

namespace Karpo.Dock;

/// <summary>Whether the sun stays up or stays down all day at a place.</summary>
internal enum PolarState
{
    /// <summary>The sun rises and sets.</summary>
    None,

    /// <summary>Polar day: it never sets (for twilight: it never gets darker than civil twilight).</summary>
    Day,

    /// <summary>Polar night: it never rises (for twilight: it never gets that light).</summary>
    Night,
}

/// <summary>Sunrise and sunset of one solar day, as instants; both null where <see cref="Polar"/> is not <see cref="PolarState.None"/>.</summary>
internal readonly record struct SunTimes(DateTimeOffset? Sunrise, DateTimeOffset? Sunset, PolarState Polar);

/// <summary>Which of the two daily events.</summary>
internal enum SunEventKind
{
    Sunrise,
    Sunset,
}

/// <summary>One sunrise or sunset.</summary>
internal readonly record struct SunEvent(SunEventKind Kind, DateTimeOffset At);

/// <summary>Solar noon and how high the sun stands then (negative in polar night).</summary>
internal readonly record struct SolarNoon(DateTimeOffset At, double AltitudeDegrees);

/// <summary>Where the sun is now as seen from a place: its declination and its hour angle (west of the meridian, -180..180).</summary>
internal readonly record struct SunNow(double DeclinationDegrees, double HourAngleDegrees);

/// <summary>Sunrise, sunset and where the sun is, computed from a latitude and longitude (see the top of the file).</summary>
internal static class SolarPosition
{
    /// <summary>Radians per degree.</summary>
    private const double Deg = Math.PI / 180;

    /// <summary>The upper limb on the horizon, refraction included: what a sunrise is.</summary>
    private const double HorizonDegrees = -0.833;

    /// <summary>The centre 6° under the horizon: where civil twilight begins and ends.</summary>
    private const double CivilDegrees = -6;

    private const double UnixEpochJulian = 2440587.5;
    private const double J2000 = 2451545.0;

    private static double Julian(DateTimeOffset moment) => ((moment.UtcDateTime - DateTime.UnixEpoch).TotalDays) + UnixEpochJulian;

    private static DateTimeOffset FromJulian(double julian) => DateTimeOffset.UnixEpoch.AddDays(julian - UnixEpochJulian);

    private static double Norm360(double degrees)
    {
        var wrapped = degrees % 360;
        return wrapped < 0 ? wrapped + 360 : wrapped;
    }

    private static double Norm180(double degrees)
    {
        var wrapped = Norm360(degrees);
        return wrapped > 180 ? wrapped - 360 : wrapped;
    }

    /// <summary>The sun's declination (degrees) and the equation of time (minutes) at Julian day <paramref name="julian"/>: NOAA's series.</summary>
    private static (double Declination, double EquationOfTime) At(double julian)
    {
        var t = (julian - J2000) / 36525;
        var meanLongitude = Norm360(280.46646 + (t * (36000.76983 + (t * 0.0003032))));
        var meanAnomaly = 357.52911 + (t * (35999.05029 - (0.0001537 * t)));
        var eccentricity = 0.016708634 - (t * (0.000042037 + (0.0000001267 * t)));
        var m = meanAnomaly * Deg;
        var centre = (Math.Sin(m) * (1.914602 - (t * (0.004817 + (0.000014 * t))))) + (Math.Sin(2 * m) * (0.019993 - (0.000101 * t))) + (Math.Sin(3 * m) * 0.000289);
        var omega = (125.04 - (1934.136 * t)) * Deg;
        var apparent = (meanLongitude + centre - 0.00569 - (0.00478 * Math.Sin(omega))) * Deg;
        var meanObliquity = 23 + ((26 + ((21.448 - (t * (46.815 + (t * (0.00059 - (t * 0.001813)))))) / 60)) / 60);
        var obliquity = (meanObliquity + (0.00256 * Math.Cos(omega))) * Deg;
        var declination = Math.Asin(Math.Sin(obliquity) * Math.Sin(apparent)) / Deg;
        var y = Math.Pow(Math.Tan(obliquity / 2), 2);
        var l = meanLongitude * Deg;
        var equation = 4 * ((y * Math.Sin(2 * l)) - (2 * eccentricity * Math.Sin(m)) + (4 * eccentricity * y * Math.Sin(m) * Math.Cos(2 * l))
            - (0.5 * y * y * Math.Sin(4 * l)) - (1.25 * eccentricity * eccentricity * Math.Sin(2 * m))) / Deg;
        return (declination, equation);
    }

    /// <summary>
    /// The mean noon (Julian day) of the solar day holding <paramref name="moment"/> at
    /// <paramref name="longitude"/>: the day is the place's solar day, not the UTC day, so Sydney's
    /// morning is not handed the day before's times (Themia's [S-06]).
    /// </summary>
    private static double MeanNoon(DateTimeOffset moment, double longitude)
    {
        var day = Math.Round(Julian(moment) - J2000 + (longitude / 360));
        return J2000 + day - (longitude / 360);
    }

    /// <summary>Sunrise and sunset of the solar day holding <paramref name="moment"/>.</summary>
    public static SunTimes TimesFor(DateTimeOffset moment, double latitude, double longitude) =>
        Crossings(moment, latitude, longitude, HorizonDegrees);

    /// <summary>Civil dawn (<see cref="SunTimes.Sunrise"/>) and dusk (<see cref="SunTimes.Sunset"/>) of the solar day holding <paramref name="moment"/>.</summary>
    public static SunTimes CivilTwilightFor(DateTimeOffset moment, double latitude, double longitude) =>
        Crossings(moment, latitude, longitude, CivilDegrees);

    /// <summary>Solar noon of the solar day holding <paramref name="moment"/>, and the sun's altitude then: 90° less the latitude's distance from the declination.</summary>
    public static SolarNoon NoonFor(DateTimeOffset moment, double latitude, double longitude)
    {
        var noon = MeanNoon(moment, longitude);
        var transit = noon;
        var declination = 0d;
        for (var round = 0; round < 3; round++)
        {
            var (dec, equation) = At(transit);
            declination = dec;
            transit = noon - (equation / 1440);
        }
        return new SolarNoon(FromJulian(transit), 90 - Math.Abs(latitude - declination));
    }

    /// <summary>When the sun's centre crosses <paramref name="altitude"/> going up and coming down, each read at its own instant.</summary>
    private static SunTimes Crossings(DateTimeOffset moment, double latitude, double longitude, double altitude)
    {
        var noon = MeanNoon(moment, longitude);
        var times = new DateTimeOffset?[2];
        var polar = PolarState.None;
        for (var side = 0; side < 2; side++)
        {
            var sign = side == 0 ? -1 : 1;
            var at = noon;
            for (var round = 0; round < 5; round++)
            {
                var (declination, equation) = At(at);
                var cosine = Cosine(latitude, declination, altitude);
                if (double.IsNaN(cosine))
                {
                    // An exactly-zero cosine at a pole: up all day where the sun is in the observer's hemisphere.
                    polar = declination * latitude > 0 ? PolarState.Day : PolarState.Night;
                    break;
                }
                if (cosine > 1 || cosine < -1)
                {
                    // Read at the event's guessed instant it may leave the range only there; at noon it decides.
                    if (round == 0)
                    {
                        polar = cosine > 1 ? PolarState.Night : PolarState.Day;
                        break;
                    }
                    cosine = Math.Clamp(cosine, -1, 1);
                }
                var hourAngle = Math.Acos(cosine) / Deg;
                at = noon - (equation / 1440) + (sign * hourAngle / 360);
            }
            if (polar != PolarState.None)
            {
                return new SunTimes(null, null, polar);
            }
            times[side] = FromJulian(at);
        }
        return new SunTimes(times[0], times[1], PolarState.None);
    }

    private static double Cosine(double latitude, double declination, double altitude)
    {
        var lat = latitude * Deg;
        var dec = declination * Deg;
        return (Math.Sin(altitude * Deg) - (Math.Sin(lat) * Math.Sin(dec))) / (Math.Cos(lat) * Math.Cos(dec));
    }

    /// <summary>How long the sun is up, or null where it neither rises nor sets.</summary>
    public static TimeSpan? Daylight(SunTimes times) =>
        times is { Sunrise: { } rise, Sunset: { } set } ? set - rise : null;

    /// <summary>Today's daylight against yesterday's, positive while the days lengthen; null where either day has no sunrise or sunset.</summary>
    public static TimeSpan? DaylightChange(DateTimeOffset moment, double latitude, double longitude) =>
        Daylight(TimesFor(moment, latitude, longitude)) is { } today && Daylight(TimesFor(moment.AddDays(-1), latitude, longitude)) is { } yesterday
            ? today - yesterday
            : null;

    /// <summary>
    /// The next sunrise or sunset strictly after <paramref name="moment"/>: the earliest still ahead of
    /// the solar days before, of and after it, so it is never one already past however the solar day
    /// meets the clock (Themia's [S-07]). Null inside the polar circles, where those days hold none.
    /// </summary>
    /// <param name="only">§404: the next of this kind alone (a sunrise widget's next sunrise); null for either.</param>
    public static SunEvent? NextEvent(DateTimeOffset moment, double latitude, double longitude, SunEventKind? only = null)
    {
        SunEvent? earliest = null;
        for (var day = -1; day <= 1; day++)
        {
            var times = TimesFor(moment.AddDays(day), latitude, longitude);
            Consider(times.Sunrise, SunEventKind.Sunrise);
            Consider(times.Sunset, SunEventKind.Sunset);
        }
        return earliest;

        void Consider(DateTimeOffset? at, SunEventKind kind)
        {
            if ((only ?? kind) == kind && at is { } instant && instant > moment && (earliest is null || instant < earliest.Value.At))
            {
                earliest = new SunEvent(kind, instant);
            }
        }
    }

    /// <summary>Whether the sun is up at <paramref name="moment"/>: its upper limb over the horizon, as a sunrise and a sunset are reckoned.</summary>
    public static bool Up(DateTimeOffset moment, double latitude, double longitude)
    {
        var sun = Now(moment, longitude);
        return SkyPath.AltitudeAtHourAngle(sun.HourAngleDegrees, sun.DeclinationDegrees, latitude) >= HorizonDegrees;
    }

    /// <summary>Where the sun is at <paramref name="moment"/> from <paramref name="longitude"/>: its declination and how far west of the meridian.</summary>
    public static SunNow Now(DateTimeOffset moment, double longitude)
    {
        var julian = Julian(moment);
        var (declination, equation) = At(julian);
        var minutes = moment.UtcDateTime.TimeOfDay.TotalMinutes;
        var solarMinutes = minutes + equation + (4 * longitude);
        return new SunNow(declination, Norm180((solarMinutes / 4) - 180));
    }
}

/// <summary>Where a body is in an observer's sky: up or not, how far along its stretch (0..1), and how high.</summary>
internal readonly record struct SkyPosition(bool Up, double Progress, double AltitudeDegrees);

/// <summary>One altitude reading along a stretch: 0..1 along it, and degrees over the horizon.</summary>
internal readonly record struct SkySample(double Progress, double AltitudeDegrees);

/// <summary>
/// The shape the sun traces: altitude against hour angle, not a semicircle — flat in winter, tall in
/// summer, barely a bump inside the polar circles. Pure functions of angles (Themia's SkyPath).
/// </summary>
internal static class SkyPath
{
    private const double Deg = Math.PI / 180;

    /// <summary>Altitude, degrees, at an hour angle for a declination and a latitude.</summary>
    public static double AltitudeAtHourAngle(double hourAngle, double declination, double latitude)
    {
        var lat = latitude * Deg;
        var dec = declination * Deg;
        return Math.Asin(Math.Clamp((Math.Sin(lat) * Math.Sin(dec)) + (Math.Cos(lat) * Math.Cos(dec) * Math.Cos(hourAngle * Deg)), -1, 1)) / Deg;
    }

    /// <summary>Half the time over the horizon as an hour angle: 0 where it never rises, 180 where it never sets.</summary>
    public static double SemiDiurnalArc(double declination, double latitude)
    {
        var cosine = -Math.Tan(latitude * Deg) * Math.Tan(declination * Deg);
        return cosine <= -1 ? 180 : cosine >= 1 ? 0 : Math.Acos(cosine) / Deg;
    }

    /// <summary>
    /// The hour angle at <paramref name="progress"/> along a stretch: over the horizon rise → meridian →
    /// set, -arc..arc; under it on round the same way, set → nadir → rise.
    /// </summary>
    public static double HourAngleAt(double progress, double arc, bool below)
    {
        var p = Math.Clamp(progress, 0, 1);
        if (!below)
        {
            return ((2 * p) - 1) * arc;
        }
        var half = 180 - arc;
        return p <= 0.5 ? arc + (2 * p * half) : -180 + ((p - 0.5) * 2 * half);
    }

    /// <summary>Where the sun is in the sky of <paramref name="latitude"/>, from where it is now (<see cref="SolarPosition.Now"/>).</summary>
    public static SkyPosition PositionOf(SunNow sun, double latitude)
    {
        var hourAngle = sun.HourAngleDegrees;
        var altitude = AltitudeAtHourAngle(hourAngle, sun.DeclinationDegrees, latitude);
        var arc = SemiDiurnalArc(sun.DeclinationDegrees, latitude);
        if (altitude >= 0)
        {
            return new SkyPosition(true, arc > 0 ? Math.Clamp(0.5 + (hourAngle / (2 * arc)), 0, 1) : 0.5, altitude);
        }
        var half = 180 - arc;
        if (half <= 0)
        {
            return new SkyPosition(false, 0.5, altitude);
        }
        var below = hourAngle >= 0 ? (hourAngle - arc) / (2 * half) : 0.5 + ((hourAngle + 180) / (2 * half));
        return new SkyPosition(false, Math.Clamp(below, 0, 1), altitude);
    }

    /// <summary>The stretch sampled evenly from <paramref name="from"/> to <paramref name="to"/> of it, <paramref name="steps"/> + 1 readings.</summary>
    public static SkySample[] Samples(double declination, double latitude, bool below, int steps = 48, double from = 0, double to = 1)
    {
        var count = Math.Max(1, steps);
        var arc = SemiDiurnalArc(declination, latitude);
        var samples = new SkySample[count + 1];
        for (var i = 0; i <= count; i++)
        {
            var progress = from + ((to - from) * i / count);
            samples[i] = new SkySample(progress, AltitudeAtHourAngle(HourAngleAt(progress, arc, below), declination, latitude));
        }
        return samples;
    }

    /// <summary>The highest the sun gets at <paramref name="latitude"/> all year: the arc's axis, so June's and December's differ.</summary>
    public static double YearHighest(double latitude) => Math.Min(90, 90 - Math.Abs(latitude) + 23.44);
}

/// <summary>A sunrise or sunset on the day bar: which, when, and how far across (0..1).</summary>
internal readonly record struct SunMark(SunEventKind Kind, DateTimeOffset At, double Share);

/// <summary>
/// The day bar at one instant (Themia's SunDayBar): the clock's 24 hours of the day holding now as 0..1 across,
/// the stretches lit while the sun is up in them, a mark at each sunrise and sunset on it, and now.
/// </summary>
/// <param name="Lit">The lit stretches in order: all of it in polar day, none in polar night, two where the place's daylight crosses this PC's midnight.</param>
/// <param name="Marks">Each sunrise and sunset on the bar, in order.</param>
internal sealed record SunDayBarPlan(double Now, IReadOnlyList<(double From, double To)> Lit, IReadOnlyList<SunMark> Marks)
{
    /// <summary>Whether now is in daylight.</summary>
    public bool NowLit => Lit.Any(stretch => Now >= stretch.From && Now <= stretch.To);

    /// <summary>The first sunrise or sunset on the bar, or null where it has none.</summary>
    public SunMark? First(SunEventKind kind)
    {
        foreach (var mark in Marks)
        {
            if (mark.Kind == kind)
            {
                return mark;
            }
        }
        return null;
    }
}

/// <summary>
/// §657: the day bar is always one full day of 24 hours — this PC's clock from 00:00 to 24:00 of the day holding
/// now, noon in its middle, each instant where its clock time is (a 23- or 25-hour day still the clock's 24
/// hours). The daylight on it is wherever the sun is up in those hours, from the sunrises and sunsets of every solar
/// day of the place that touches them — not one solar day's pair, which falls off the bar where the place's day and
/// this PC's do not line up (after midnight before the place's solar day turns; a place in another zone).
/// </summary>
internal static class SunDayBar
{
    public static SunDayBarPlan Of(DateTimeOffset now, double latitude, double longitude, TimeZoneInfo zone)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        var marks = new List<SunMark>();
        // The solar days either side reach this PC's day wherever its zone is against the place's longitude.
        for (var day = -2; day <= 2; day++)
        {
            var times = SolarPosition.TimesFor(now.AddDays(day), latitude, longitude);
            Add(times.Sunrise, SunEventKind.Sunrise);
            Add(times.Sunset, SunEventKind.Sunset);
        }
        marks.Sort((a, b) => a.At.CompareTo(b.At));
        // Lit at 00:00 where the day's first event is a sunset; with none, as the sun is all day.
        double? open = (marks.Count > 0 ? marks[0].Kind == SunEventKind.Sunset : SolarPosition.Up(now, latitude, longitude)) ? 0 : null;
        var lit = new List<(double From, double To)>();
        foreach (var mark in marks)
        {
            if (mark.Kind == SunEventKind.Sunrise)
            {
                open ??= mark.Share;
            }
            else if (open is { } from)
            {
                lit.Add((from, mark.Share));
                open = null;
            }
        }
        if (open is { } rest)
        {
            lit.Add((rest, 1));
        }
        return new SunDayBarPlan(Share(now, today, zone) ?? 0, lit, marks);

        void Add(DateTimeOffset? at, SunEventKind kind)
        {
            if (at is { } instant && Share(instant, today, zone) is { } share && !marks.Exists(mark => mark.At == instant))
            {
                marks.Add(new SunMark(kind, instant, share));
            }
        }
    }

    /// <summary>An instant's place across <paramref name="day"/>: its clock time in <paramref name="zone"/> over 24 hours; null where it is on another day.</summary>
    public static double? Share(DateTimeOffset at, DateOnly day, TimeZoneInfo zone)
    {
        var local = TimeZoneInfo.ConvertTime(at, zone).DateTime;
        return DateOnly.FromDateTime(local) == day ? local.TimeOfDay.Ticks / (double)TimeSpan.TicksPerDay : null;
    }
}

/// <summary>
/// Everything a sun face or line says at one instant for one place: the solar day's sunrise and sunset,
/// the next event, noon, civil twilight, the change since yesterday, where the sun is, and the day bar.
/// Worked out at once from the instant, so a face and a line of one place at one minute say the same.
/// </summary>
internal sealed record SunDay(
    DateTimeOffset Now,
    double Latitude,
    double Longitude,
    SunTimes Times,
    SunEvent? Next,
    SolarNoon Noon,
    SunTimes Twilight,
    TimeSpan? Change,
    SunNow Sun,
    SkyPosition Position,
    SunDayBarPlan Bar)
{
    public static SunDay Of(DateTimeOffset now, double latitude, double longitude, TimeZoneInfo zone)
    {
        var times = SolarPosition.TimesFor(now, latitude, longitude);
        var sun = SolarPosition.Now(now, longitude);
        return new SunDay(
            now, latitude, longitude, times,
            SolarPosition.NextEvent(now, latitude, longitude),
            SolarPosition.NoonFor(now, latitude, longitude),
            SolarPosition.CivilTwilightFor(now, latitude, longitude),
            SolarPosition.DaylightChange(now, latitude, longitude),
            sun,
            SkyPath.PositionOf(sun, latitude),
            SunDayBar.Of(now, latitude, longitude, zone))
        {
            NextSunrise = SolarPosition.NextEvent(now, latitude, longitude, SunEventKind.Sunrise),
            NextSunset = SolarPosition.NextEvent(now, latitude, longitude, SunEventKind.Sunset),
        };
    }

    /// <summary>§404: the next sunrise, whichever comes first; null inside the polar circles.</summary>
    public SunEvent? NextSunrise { get; init; }

    /// <summary>§404: the next sunset, whichever comes first; null inside the polar circles.</summary>
    public SunEvent? NextSunset { get; init; }

    /// <summary>The next of <paramref name="kind"/>, what its widget says (§404).</summary>
    public SunEvent? NextOf(SunEventKind kind) => kind == SunEventKind.Sunrise ? NextSunrise : NextSunset;

    /// <summary>How soon the next event is.</summary>
    public TimeSpan? Until => Next is { } next ? next.At - Now : null;
}

/// <summary>
/// §404: the sun's day for a place at a minute, worked out once for every line and face of that place — the
/// sun widget, each of its times, the face on the bar — and kept until the minute turns.
/// </summary>
internal static class SunDays
{
    private static readonly Lock gate = new();
    private static readonly Dictionary<(double Latitude, double Longitude, string Zone), SunDay> days = [];
    private static long minute = -1;

    /// <summary>The day at <paramref name="now"/>'s minute for the place: the one already worked out this minute, or a new one.</summary>
    public static SunDay Of(DateTimeOffset now, double latitude, double longitude, TimeZoneInfo zone)
    {
        var at = now.UtcTicks / TimeSpan.TicksPerMinute;
        var key = (latitude, longitude, zone.Id);
        lock (gate)
        {
            if (at != minute)
            {
                days.Clear();
                minute = at;
            }
            if (days.TryGetValue(key, out var known))
            {
                return known;
            }
            var day = SunDay.Of(now, latitude, longitude, zone);
            days[key] = day;
            return day;
        }
    }
}

/// <summary>The sun's words: times in this PC's format, durations, the change against yesterday, how soon.</summary>
internal static class SunText
{
    /// <summary>Where nothing is to say: inside the polar circles an event has no time.</summary>
    public const string None = "—";

    /// <summary>"07:28" on the region's clock (the person's format), in this PC's zone; a dash for none.</summary>
    public static string Time(DateTimeOffset? at) =>
        at is { } instant ? Nls.Time(instant.ToLocalTime().DateTime) : None;

    /// <summary>"11 h 33 min"; "24 h" in a polar day, "0 h" in a polar night.</summary>
    public static string Daylight(SunTimes times) =>
        SolarPosition.Daylight(times) is { } span ? Length((int)Math.Round(span.TotalMinutes)) : S.WidgetCommonHours(times.Polar == PolarState.Day ? 24 : 0);

    /// <summary>"6 h 45 min", "12 min", "3 h".</summary>
    public static string Length(int minutes)
    {
        if (minutes < 60)
        {
            return S.WidgetTileMinutes(Math.Max(0, minutes));
        }
        var (hours, rest) = Math.DivRem(minutes, 60);
        return rest == 0 ? S.WidgetCommonHours(hours) : S.WidgetTileHoursMinutes(hours, rest);
    }

    /// <summary>
    /// "2 min 14 s longer than yesterday", "58 s shorter than yesterday", "Same as yesterday" where it rounds to
    /// nothing, as it does round a solstice. In words since §404: a bare "−58 s" read as what was left of the day.
    /// </summary>
    public static string Change(TimeSpan change)
    {
        var seconds = (int)Math.Round(change.TotalSeconds);
        if (seconds == 0)
        {
            return S.DockSunSameAsYesterday;
        }
        var magnitude = Math.Abs(seconds);
        var amount = magnitude >= 60 ? S.DockSunMinutesSeconds(magnitude / 60, magnitude % 60) : S.DockClockSeconds(magnitude);
        return seconds < 0 ? S.DockSunShorter(amount) : S.DockSunLonger(amount);
    }

    /// <summary>§404: "1 h 23 min left": a part minute rounded up, never under a minute, as <see cref="In"/>.</summary>
    public static string Left(TimeSpan until) => S.DockSunLeft(MeetingText.Length(until < TimeSpan.Zero ? TimeSpan.Zero : until, up: true));

    /// <summary>
    /// §404, the daylight's note: while the sun is up how much of the day is left ("1 h 23 min left"); while it
    /// is down the change since yesterday (<see cref="Change"/>); a polar day or night where there is neither.
    /// </summary>
    public static string? DaylightNote(SunDay day) =>
        day.Next is { Kind: SunEventKind.Sunset } set ? Left(set.At - day.Now)
        : day.Times.Polar == PolarState.Day ? S.DockSunPolarDay
        : day.Times.Polar == PolarState.Night ? S.DockSunPolarNight
        : day.Change is { } change ? Change(change)
        : null;

    /// <summary>"in 6 h 45 min": a part minute rounded up, never under a minute, as a meeting's.</summary>
    public static string In(TimeSpan until) => S.DockMeetingIn(MeetingText.Length(until < TimeSpan.Zero ? TimeSpan.Zero : until, up: true));

    /// <summary>"34°", "−3°" (U+2212): whole degrees, never "-0°".</summary>
    public static string Degrees(double degrees) =>
        ((int)Math.Round(degrees)) is var whole && whole < 0 ? "−" + (-whole).ToString(CultureInfo.InvariantCulture) + "°" : whole.ToString(CultureInfo.InvariantCulture) + "°";

    /// <summary>"Sunrise" or "Sunset".</summary>
    public static string Word(SunEventKind kind) => kind == SunEventKind.Sunrise ? S.DockSunSunrise : S.DockSunSunset;

    /// <summary>"Sunset 18:32".</summary>
    public static string At(SunEventKind kind, string time) => kind == SunEventKind.Sunrise ? S.DockSunSunriseAt(time) : S.DockSunSunsetAt(time);

    /// <summary>"Sunset in 3 h 10 min".</summary>
    public static string NamedIn(SunEventKind kind, TimeSpan until)
    {
        var length = MeetingText.Length(until < TimeSpan.Zero ? TimeSpan.Zero : until, up: true);
        return kind == SunEventKind.Sunrise ? S.DockSunSunriseIn(length) : S.DockSunSunsetIn(length);
    }

    /// <summary>Segoe Fluent Icons' sun on the horizon, its rays long for a sunrise and short for a sunset.</summary>
    public static string Glyph(SunEventKind kind) => kind == SunEventKind.Sunrise ? Sunrise : Sunset;

    public const string Sunrise = "";
    public const string Sunset = "";

    /// <summary>The sun up (Brightness) and down (QuietHours): a polar day, a polar night, noon and dusk.</summary>
    public const string Sun = "";
    public const string Moon = "";

    /// <summary>Daylight's clock.</summary>
    public const string Clock = "";
}
