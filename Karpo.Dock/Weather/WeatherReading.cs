// Everything the weather flyout shows, worked out from Open-Meteo's answer
// alone: no window, no network and no clock of its own, so the words, the
// units, the rounding and the hour and day windows can all be checked against
// saved answers.
//
// Karpo always asks Open-Meteo for degrees Celsius and kilometres an hour and
// converts here, into the scale Localization says (Nls.Fahrenheit), so
// switching between °C and °F costs no request.
//
// The hour window takes the entries that follow the current reading in the
// order the answer lists them, never "now plus an hour, now plus two": on the
// day a region's clocks go back that list has two entries reading 02:00, and on
// the day they go forward it has none, and the strip must follow the list
// rather than the wall clock.

using System.Globalization;
using Karpo.Core;
using Karpo.Dock.Panels;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock.Weather;

/// <summary>Reading Open-Meteo's answer: the one place a downloaded forecast is turned into objects.</summary>
internal static class WeatherForecasts
{
    /// <summary>
    /// The forecast in <paramref name="json"/>. Throws <see cref="InvalidDataException"/> when it is
    /// not one: an answer without a current temperature would leave the widget with nothing to show.
    /// </summary>
    public static Forecast Parse(string json)
    {
        Forecast? forecast;
        try
        {
            forecast = System.Text.Json.JsonSerializer.Deserialize(json, IslandJson.Default.Forecast);
        }
        catch (System.Text.Json.JsonException error)
        {
            throw new InvalidDataException("the forecast could not be read", error);
        }
        if (forecast?.Current?.Temperature is null)
        {
            throw new InvalidDataException("no current temperature");
        }
        return forecast;
    }
}

/// <summary>A WMO weather code in words and in a Segoe Fluent Icons glyph.</summary>
internal static class WeatherCodes
{
    // Segoe Fluent Icons has no full weather set; these are the glyphs it has
    // that read as weather: Brightness, QuietHours (a moon), Cloud, Drop,
    // Frigid, LightningBolt, Unknown, and MapPin for "no place yet".
    internal const string Sun = "";
    internal const string Moon = "";
    internal const string Cloud = "";
    internal const string Drop = "";
    internal const string Frigid = "";
    internal const string Bolt = "";
    internal const string Unknown = "";
    internal const string Location = "";

    /// <summary>
    /// Every word <see cref="Describe"/> can answer with, so a widget can
    /// measure the widest and keep its size whatever the weather does. Built
    /// once from the codes below.
    /// </summary>
    public static IReadOnlyList<string> Conditions => AllConditions();

    private static string[] AllConditions()
    {
        int[] codes = [0, 1, 2, 3, 45, 51, 56, 61, 63, 65, 66, 71, 73, 75, 77, 80, 81, 82, 85, 86, 95, 96, -1];
        var texts = new List<string>();
        foreach (var code in codes)
        {
            foreach (var day in (ReadOnlySpan<bool>)[true, false])
            {
                var text = Describe(code, day).Text;
                if (!texts.Contains(text))
                {
                    texts.Add(text);
                }
            }
        }
        return [.. texts];
    }

    /// <summary>Open-Meteo's weather_code in words, and its glyph; <paramref name="day"/> picks the sun or the moon.</summary>
    public static (string Text, string Glyph) Describe(int? code, bool day) => code switch
    {
        0 => (day ? S.WidgetWeatherConditionSunny : S.WidgetWeatherConditionClear, day ? Sun : Moon),
        1 => (day ? S.WidgetWeatherConditionMostlySunny : S.WidgetWeatherConditionMostlyClear, day ? Sun : Moon),
        2 => (S.WidgetWeatherConditionPartlyCloudy, Cloud),
        3 => (S.WidgetWeatherConditionCloudy, Cloud),
        45 or 48 => (S.WidgetWeatherConditionFog, Cloud),
        51 or 53 or 55 => (S.WidgetWeatherConditionDrizzle, Drop),
        56 or 57 => (S.WidgetWeatherConditionFreezingDrizzle, Frigid),
        61 => (S.WidgetWeatherConditionLightRain, Drop),
        63 => (S.WidgetWeatherConditionRain, Drop),
        65 => (S.WidgetWeatherConditionHeavyRain, Drop),
        66 or 67 => (S.WidgetWeatherConditionFreezingRain, Frigid),
        71 => (S.WidgetWeatherConditionLightSnow, Frigid),
        73 => (S.WidgetWeatherConditionSnow, Frigid),
        75 => (S.WidgetWeatherConditionHeavySnow, Frigid),
        77 => (S.WidgetWeatherConditionSnowGrains, Frigid),
        80 => (S.WidgetWeatherConditionLightShowers, Drop),
        81 => (S.WidgetWeatherConditionShowers, Drop),
        82 => (S.WidgetWeatherConditionHeavyShowers, Drop),
        85 => (S.WidgetWeatherConditionSnowShowers, Frigid),
        86 => (S.WidgetWeatherConditionHeavySnowShowers, Frigid),
        95 => (S.WidgetWeatherConditionThunderstorm, Bolt),
        96 or 99 => (S.WidgetWeatherConditionThunderstormWithHail, Bolt),
        _ => (S.WidgetWeatherConditionUnknown, Unknown),
    };

    /// <summary>
    /// The sun in its own colour, as Windows' weather shows it, and every other glyph in the text's —
    /// wherever the weather draws one in its own colours: the flyout's reading and days, its hours,
    /// and a list's line. On a dark surface Windows' amber; on a light one that amber is
    /// a faint 1.7:1 against a white card, where a graphic needs 3:1, so the light theme has Fluent's
    /// caution amber, its darker shade (<see cref="SunOnLight"/>). High contrast keeps the text's.
    /// </summary>
    public static DXGI_RGBA Ink(string glyph, FlyoutTheme theme) =>
        glyph != Sun || theme.HighContrast ? theme.Text : theme.Dark ? SunOnDark : SunOnLight;

    /// <summary>Windows' weather amber, #FFB900: the sun on a dark surface.</summary>
    internal static readonly DXGI_RGBA SunOnDark = Gfx.Ink(1f, 0.725f, 0f, 1f);

    /// <summary>
    /// Fluent's caution fill for the light theme, #9D5D00: the sun on a light surface, about 5:1
    /// against a white card and the flyout's own ground.
    /// </summary>
    internal static readonly DXGI_RGBA SunOnLight = Gfx.Ink(0x9D / 255f, 0x5D / 255f, 0f, 1f);
}

/// <summary>Temperatures and wind speeds in the scale Localization says, and how they are written.</summary>
internal static class WeatherUnits
{
    /// <summary>
    /// Whether the weather is shown in Fahrenheit: Localization's scale, which follows Windows'
    /// region until somebody sets it (<see cref="Nls.Fahrenheit"/>). The widget had a scale of its
    /// own, taken out at the person's word (2026-10-05): one setting, never two that disagree.
    /// </summary>
    public static bool Fahrenheit => Nls.Fahrenheit();

    /// <summary>Below this a chance of rain is not worth a line of its own, as Windows leaves it out.</summary>
    public const int LeastChance = 10;

    public static double ToFahrenheit(double celsius) => (celsius * 9 / 5) + 32;

    public static double ToCelsius(double fahrenheit) => (fahrenheit - 32) * 5 / 9;

    public static double ToMilesPerHour(double kilometresPerHour) => kilometresPerHour * 0.621371192;

    public static double ToKilometresPerHour(double milesPerHour) => milesPerHour / 0.621371192;

    /// <summary>A temperature held in one unit, in the other: what a forecast fetched in °C says in °F.</summary>
    public static double? Temperature(double? value, bool held, bool wanted) => value switch
    {
        null => null,
        { } degrees when held == wanted => degrees,
        { } degrees => wanted ? ToFahrenheit(degrees) : ToCelsius(degrees),
    };

    public static double? Wind(double? value, bool held, bool wanted) => value switch
    {
        null => null,
        { } speed when held == wanted => speed,
        { } speed => wanted ? ToMilesPerHour(speed) : ToKilometresPerHour(speed),
    };

    /// <summary>"18°"; a rounding that lands on zero from below is still "0°", never "-0°".</summary>
    public static string Degrees(double? value)
    {
        if (value is not { } degrees || double.IsNaN(degrees))
        {
            return "–";
        }
        var whole = Math.Round(degrees, MidpointRounding.AwayFromZero);
        return whole == 0 ? "0°" : $"{whole.ToString("0", CultureInfo.InvariantCulture)}°";
    }

    /// <summary>"18°C": the temperature with the unit it is in, as the flyout's first line shows it.</summary>
    public static string DegreesWithUnit(double? value, bool fahrenheit) =>
        value is null ? "–" : Degrees(value) + (fahrenheit ? "F" : "C");

    /// <summary>"7 km/h" or "4 mph".</summary>
    public static string Speed(double? value, bool fahrenheit)
    {
        if (value is not { } speed)
        {
            return "";
        }
        var whole = Math.Round(speed, MidpointRounding.AwayFromZero);
        return $"{whole.ToString("0", CultureInfo.InvariantCulture)} {(fahrenheit ? "mph" : "km/h")}";
    }

    /// <summary>"40%", or nothing at all when the chance is too small to be worth saying.</summary>
    public static string Chance(int? percent) =>
        percent is { } value && value >= LeastChance ? value.ToString("0", CultureInfo.InvariantCulture) + "%" : "";
}

/// <summary>One hour of the forecast, as Open-Meteo listed it (its own local time).</summary>
internal readonly record struct HourSlot(DateTime At, int? Code, bool Day, double? Temperature, int? Chance);

/// <summary>One day of the forecast.</summary>
internal readonly record struct DaySlot(DateOnly On, int? Code, double? High, double? Low, int? Chance);

/// <summary>Which hours and days a forecast has to say something about, around the reading's own time.</summary>
internal static class WeatherWindow
{
    /// <summary>Open-Meteo writes its local times like this.</summary>
    private const string HourFormat = "yyyy-MM-dd'T'HH:mm";

    /// <summary>
    /// The <paramref name="most"/> hours that follow <paramref name="after"/> in the order the answer
    /// lists them. The list, not the clock, decides what comes next: a day whose clocks go back has
    /// two entries reading the same hour, and one whose clocks go forward is missing an hour.
    /// </summary>
    /// <param name="fromHourInProgress">
    /// Start at the hour that is running rather than the next one. The graph needs it: the mark for
    /// now has to fall between two points, and without the hour it is in it would sit off the left
    /// edge of the strip.
    /// </param>
    public static List<HourSlot> Hours(Forecast forecast, DateTime after, int most, bool fromHourInProgress = false)
    {
        var slots = new List<HourSlot>();
        if (forecast.Hourly is not { Time: { } times } hours || most <= 0)
        {
            return slots;
        }
        var anchor = ParseHour(forecast.Current?.Time) ?? after;
        if (fromHourInProgress)
        {
            // A tick before the running hour began, so that hour's own entry is the first taken.
            anchor = anchor.Date.AddHours(anchor.Hour).AddTicks(-1);
        }
        var started = false;
        for (var index = 0; index < times.Length && slots.Count < most; index++)
        {
            if (ParseHour(times[index]) is not { } at)
            {
                continue;
            }
            // The first entry past the reading's own hour starts the strip; every
            // entry after it belongs to it, whatever its wall clock says.
            if (!started)
            {
                if (at <= anchor)
                {
                    continue;
                }
                started = true;
            }
            var day = hours.IsDay is { } daylight && index < daylight.Length
                ? daylight[index] != 0
                : at.Hour is >= 7 and < 19;
            slots.Add(new HourSlot(
                at,
                At(hours.WeatherCode, index),
                day,
                At(hours.Temperature, index),
                Whole(At(hours.PrecipitationChance, index))));
        }
        return slots;
    }

    /// <summary>The <paramref name="most"/> days from <paramref name="from"/> on, today first.</summary>
    public static List<DaySlot> Days(Forecast forecast, DateOnly from, int most)
    {
        var slots = new List<DaySlot>();
        if (forecast.Daily is not { Time: { } dates } daily || most <= 0)
        {
            return slots;
        }
        for (var index = 0; index < dates.Length && slots.Count < most; index++)
        {
            if (dates[index] is not { } text
                || !DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var on)
                || on < from)
            {
                continue;
            }
            slots.Add(new DaySlot(
                on,
                At(daily.WeatherCode, index),
                At(daily.High, index),
                At(daily.Low, index),
                Whole(At(daily.PrecipitationChance, index))));
        }
        return slots;
    }

    /// <summary>
    /// Every hour of <paramref name="day"/> the answer holds, in the order it lists them. The hours
    /// already gone are in there too — Open-Meteo's hourly block starts at the local midnight of the
    /// first day — and they are history rather than forecast, which is why they are drawn but never
    /// made up: a day the answer says nothing about simply comes back empty.
    /// </summary>
    public static List<HourSlot> Today(Forecast forecast, DateOnly day)
    {
        var slots = new List<HourSlot>();
        if (forecast.Hourly is not { Time: { } times } hours)
        {
            return slots;
        }
        // The axis ends on midnight, and the temperature at midnight is a real number the answer
        // already holds — the first hour of tomorrow. It is taken rather than 23:00 repeated, and
        // where the answer stops at 23:00 the line stops there too.
        var end = day.AddDays(1);
        for (var index = 0; index < times.Length; index++)
        {
            if (ParseHour(times[index]) is not { } at)
            {
                continue;
            }
            var on = DateOnly.FromDateTime(at);
            if (on != day && !(on == end && at.Hour == 0))
            {
                continue;
            }
            var daylight = hours.IsDay is { } light && index < light.Length
                ? light[index] != 0
                : at.Hour is >= 7 and < 19;
            slots.Add(new HourSlot(
                at,
                At(hours.WeatherCode, index),
                daylight,
                At(hours.Temperature, index),
                Whole(At(hours.PrecipitationChance, index))));
            if (on == end)
            {
                break;
            }
        }
        return slots;
    }

    /// <summary>The place's own local time now, from the offset its answer carried.</summary>
    public static DateTime LocalNow(Forecast forecast, DateTime utcNow) =>
        utcNow.AddSeconds(forecast.UtcOffsetSeconds ?? 0);

    /// <summary>Open-Meteo's "2026-09-22T22:00"; null when it is missing or unreadable.</summary>
    public static DateTime? ParseHour(string? text) =>
        text is { Length: > 0 } value
        && DateTime.TryParseExact(value, HourFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var at)
            ? at
            : null;

    private static T? At<T>(T?[]? values, int index) where T : struct =>
        values is not null && index < values.Length ? values[index] : null;

    private static int? Whole(double? value) =>
        value is { } number ? (int)Math.Round(number, MidpointRounding.AwayFromZero) : null;
}

/// <summary>How old a reading is, and whether it is old enough to say so.</summary>
internal static class WeatherAge
{
    /// <summary>A reading older than this many times its refresh interval is called stale.</summary>
    public const int StaleAfterIntervals = 3;

    /// <summary>
    /// How often an open panel has to work its age out again, in ms. A minute, because that is the
    /// finest step <see cref="Updated"/> has — "just now" becomes "1 minute ago", then "20 minutes
    /// ago", then a clock time — and <see cref="IsStale"/> turns over on a minute boundary as well;
    /// anything shorter would redraw to write the same words.
    /// <para>
    /// The cadence is one answer in one place, beside the two functions it is the cadence of, and
    /// both panels that show an age (the weather's and the stock's) arm a timer of their own with
    /// it. The timer stays with the panel that owns the window on purpose: a tick armed somewhere
    /// else, for something else's needs, is what once left Karpo's clock flyout frozen.
    /// </para>
    /// </summary>
    public const uint AgeEveryMs = 60_000;

    /// <summary>
    /// Whether a reading is old enough that it may no longer hold: three refreshes have come and
    /// gone and none of them brought anything. A reading is shown either way — an old forecast
    /// tells more than an empty panel — and what stale changes is said elsewhere (the graph's mark
    /// for now, the weather's trend arrow, and the ink the age is written in).
    /// <para>
    /// <paramref name="refreshMinutes"/> is the interval that kind actually refreshes at, already
    /// held to its own range by the caller, and it is taken as given. This used to clamp it again
    /// to 15–180 — the weather's range, which followed the weather's code here and was then applied
    /// to everybody. A stock set to ask every 5 minutes was judged on 15, so it went stale at 45
    /// minutes rather than 15, which is three quarters of an hour of a price shown as current by
    /// the one person who had said they wanted it fresher than that. The rule is relative on
    /// purpose: it means "Karpo has missed three refreshes", and how fast the subject goes off is
    /// the interval the person chose, not a number written down here.
    /// </para>
    /// </summary>
    public static bool IsStale(DateTime fetchedUtc, DateTime utcNow, int refreshMinutes) =>
        utcNow - fetchedUtc >= TimeSpan.FromMinutes(Math.Max(refreshMinutes, 1) * StaleAfterIntervals);

    /// <summary>
    /// Whether what a widget is showing is stale: three refreshes missed, or a fetch that failed
    /// and left the last answer standing. Nothing fetched yet is not stale — that is a widget still
    /// loading, and it says so itself.
    /// <para>
    /// This overload is the whole answer, and the cell on the bar and the panel both ask it, so the
    /// two cannot reach different conclusions about the same reading — which is the only way a
    /// person sees "past due" in one place and a confident number in the other.
    /// </para>
    /// </summary>
    public static bool IsStale(DateTime? fetchedUtc, DateTime utcNow, int refreshMinutes, string? problem) =>
        problem is { Length: > 0 }
        || (fetchedUtc is { } fetched && IsStale(fetched, utcNow, refreshMinutes));

    /// <summary>
    /// How fresh a line's data is, the one wording of every line's meta (§388): "Now", "3 min ago", "2 h ago",
    /// "3 d ago" — as an earthquake's age reads.
    /// </summary>
    public static string Ago(DateTime fetchedUtc, DateTime utcNow)
    {
        var at = new DateTimeOffset(DateTime.SpecifyKind(fetchedUtc, DateTimeKind.Utc));
        var now = new DateTimeOffset(DateTime.SpecifyKind(utcNow < fetchedUtc ? fetchedUtc : utcNow, DateTimeKind.Utc));
        var old = now - at;
        return old < TimeSpan.FromMinutes(1) ? S.WidgetRssAgeNow
            : old < TimeSpan.FromHours(1) ? S.DockQuakesAgo(S.DockClockMinutes((int)old.TotalMinutes))
            : S.DockQuakesAgo(Karpo.Core.Feeds.Age(at, now));
    }

    /// <summary>"Updated just now", "Updated 20 minutes ago", "Updated at 21:15", "Updated 21 Sept at 21:15": what a line's "Refresh" says under it (§451).</summary>
    public static string Updated(DateTime fetchedLocal, DateTime localNow)
    {
        var old = localNow - fetchedLocal;
        if (old < TimeSpan.Zero)
        {
            // A clock that has been put back: the reading cannot be from the future.
            old = TimeSpan.Zero;
        }
        if (old < TimeSpan.FromMinutes(1))
        {
            return S.WidgetWeatherUpdatedJustNow;
        }
        if (old < TimeSpan.FromHours(1))
        {
            var minutes = (int)old.TotalMinutes;
            return S.WidgetWeatherUpdatedMinutesAgo(minutes);
        }
        var time = Nls.Time(fetchedLocal);
        return fetchedLocal.Date == localNow.Date
            ? S.WidgetWeatherUpdatedAt(time)
            : S.WidgetWeatherUpdatedOn(day: Nls.MonthDay(DateOnly.FromDateTime(fetchedLocal)), time: time);
    }
}

/// <summary>Where the temperature is heading. Steady is its own answer, never a weak rise.</summary>
internal enum WeatherTrend
{
    /// <summary>Not enough of the forecast to say, or nothing fresh enough to say it about.</summary>
    Unknown,

    Rising,
    Steady,
    Falling,
}

/// <summary>
/// Which way the temperature is going. "Going" has to mean something definite, so: the forecast
/// three hours out against the reading now.
///
/// Three hours because one is mostly rounding — a forecast moves by a tenth of a degree an hour on
/// a still afternoon — and because three is the horizon a person means by "is it getting colder?".
/// Further out and the answer stops describing the rest of the hour you are in.
///
/// The dead zone is a whole degree Celsius either way, and it is a degree of real temperature
/// rather than of whichever unit the widget shows, so the answer does not change with the unit. A
/// day that is going nowhere says so instead of being drawn as a faint rise.
/// </summary>
internal static class WeatherTrends
{
    /// <summary>How far ahead the answer looks.</summary>
    public const int LookAheadHours = 3;

    /// <summary>Less change than this, either way, is no change (°C).</summary>
    public const double SteadyWithin = 1.0;

    /// <summary>Fewer hours than this left in the forecast and there is nothing to compare with.</summary>
    public const int LeastHours = 2;

    /// <summary>
    /// The trend at <paramref name="localNow"/> (the place's own clock). Temperatures are taken in
    /// the unit the answer was fetched in, which is the same for both sides of the comparison.
    /// </summary>
    public static WeatherTrend Of(Forecast forecast, DateTime localNow)
    {
        if (forecast.Current?.Temperature is not { } now)
        {
            return WeatherTrend.Unknown;
        }
        var ahead = WeatherWindow.Hours(forecast, localNow, LookAheadHours);
        if (ahead.Count < LeastHours || ahead[^1].Temperature is not { } later)
        {
            return WeatherTrend.Unknown;
        }
        var change = later - now;
        return change >= SteadyWithin ? WeatherTrend.Rising
            : change <= -SteadyWithin ? WeatherTrend.Falling
            : WeatherTrend.Steady;
    }
}

/// <summary>What the weather flyout is showing: the state it is in and, when it has one, the forecast.</summary>
internal enum WeatherState
{
    /// <summary>No place has been chosen for this widget yet.</summary>
    NoPlace,

    /// <summary>A place is set and the first forecast for it has not arrived.</summary>
    Loading,

    /// <summary>Nothing could be fetched and nothing was kept: only the reason is left.</summary>
    Failed,

    /// <summary>A forecast is there. <see cref="WeatherReading.Problem"/> says a newer one failed.</summary>
    Ready,
}

/// <summary>
/// One point of today's graph: where it sits on the day-long axis (hours since midnight, so 14.0
/// is two in the afternoon), the hour written out, what it looks like, the temperature written out
/// and the number itself (in the unit the widget shows), which is what the line is drawn from.
/// </summary>
internal readonly record struct HourCell(double Hour, string Time, string Glyph, string Temperature, string Chance, double? Value, string Condition = "");

/// <summary>
/// One row of the daily list. <paramref name="From"/> and <paramref name="To"/> place its range on the
/// bar, as fractions of the whole window's range; <paramref name="On"/> is the day it is.
/// </summary>
internal readonly record struct DayRow(string Name, string Glyph, string Condition, string Low, string High, string Chance, float From, float To, bool Today, DateOnly On = default);

/// <summary>
/// The whole flyout, in words: made from a forecast, the widget's options and the time, and from
/// nothing else, so every line of it can be checked without a window.
/// </summary>
internal sealed record WeatherReading
{
    /// <summary>
    /// The graph is the whole of today, midnight to midnight, however far into it the clock has
    /// got: the axis never scrolls or re-bases, so a morning shows the day ahead and a late evening
    /// shows the day that has gone. And how many days the list below it holds.
    /// </summary>
    public const int AxisHours = 24;
    public const int ListDays = 7;

    /// <summary>§590: how many hours the hourly forecast's List shows, from the next one on, past midnight where it comes.</summary>
    public const int NextHours = 5;

    public required WeatherState State { get; init; }

    /// <summary>The place as the user named it, or what stands in for it.</summary>
    public required string Place { get; init; }

    /// <summary>"16°C"; empty without a forecast.</summary>
    public string Temperature { get; init; } = "";

    public string Glyph { get; init; } = WeatherCodes.Unknown;

    public string Condition { get; init; } = "";

    /// <summary>"Feels like 15° · H 19° · L 8°".</summary>
    public string Detail { get; init; } = "";

    public IReadOnlyList<HourCell> Hours { get; init; } = [];

    /// <summary>
    /// §590: the <see cref="NextHours"/> hours after now, whatever day they fall on — the hourly forecast's List —
    /// each at its hours since today's midnight (<see cref="HourCell.Hour"/>, 24 and on for tomorrow's), its time
    /// written as the region writes it. Fewer only where the answer holds fewer.
    /// </summary>
    public IReadOnlyList<HourCell> Next { get; init; } = [];

    /// <summary>Which way the temperature is going, for the header's arrow.</summary>
    public WeatherTrend Trend { get; init; }

    /// <summary>
    /// Where now falls on the day-long axis, in hours since midnight: 14.33 is twenty past two.
    /// <see cref="NoMark"/> when there is nothing to mark — no hourly forecast for today, or a
    /// reading too old to call the temperature now.
    /// </summary>
    public double NowAt { get; init; } = NoMark;

    /// <summary>The temperature the mark sits at, in the unit shown; null with <see cref="NoMark"/>.</summary>
    public double? NowValue { get; init; }

    public IReadOnlyList<DayRow> Days { get; init; } = [];

    /// <summary>"Updated at 21:15"; empty while nothing has ever been fetched.</summary>
    public string Updated { get; init; } = "";

    /// <summary>Why a newer forecast is not there, or what to do when there is none at all.</summary>
    public string? Problem { get; init; }

    /// <summary>The reading is old enough to say so, or a refresh has failed since.</summary>
    public bool Stale { get; init; }

    /// <summary><see cref="NowAt"/> when the graph has no mark for now.</summary>
    public const double NoMark = -1;

    /// <summary>
    /// The day's warmest and coldest, written out (the user: "high and low should be written out an
    /// not abbreviated"). Windows' own locale data has names for months and days and nothing for
    /// these, so there is no region's word to prefer here and these are the words.
    /// </summary>
    public static string HighWord => S.WidgetWeatherHigh;
    public static string LowWord => S.WidgetWeatherLow;

    /// <summary>What a place that has no name of its own is called.</summary>
    public static string Here => S.WidgetWeatherHere;

    public static string NoPlaceTitle => S.WidgetWeatherNoPlaceTitle;

    public static string NoPlaceText => S.WidgetWeatherNoPlaceText;

    public static string LoadingText => S.WidgetWeatherLoading;

    /// <summary>
    /// How often the weather really asks, in minutes: every online widget's interval
    /// (<paramref name="updates"/>, the dock's own where none is given), held to the weather's own
    /// floor (<see cref="WeatherStore.LeastMinutes"/>) — the one its island and its store refresh
    /// on. One answer, because the cell and the panel both work staleness out from it and a cell
    /// that disagreed with its own panel would be the drift.
    /// </summary>
    public static int Refresh(OnlineUpdates? updates = null) =>
        (int)(updates ?? OnlineUpdates.Shared).Every(WeatherStore.LeastMinutes).TotalMinutes;

    /// <summary>
    /// Whether <paramref name="cache"/> is past due at <paramref name="utcNow"/>. <see cref="From"/>
    /// answers with this and so does the weather face (<c>WeatherFaceDrawing</c>), which is the
    /// point: the cell draws no reading of its own, so without one function to ask it would be
    /// carrying a second copy of this rule, and the two would drift until a widget was quiet beside
    /// a panel that called it fresh.
    /// </summary>
    public static bool IsStale(WeatherCache? cache, DateTime utcNow, string? problem, OnlineUpdates? updates = null) =>
        WeatherAge.IsStale(cache?.FetchedUtc, utcNow, Refresh(updates), problem);

    /// <summary>
    /// The reading for <paramref name="cache"/> (the last forecast for the widget's place, if any) for
    /// a widget set to <paramref name="options"/>, at <paramref name="utcNow"/>; <paramref name="problem"/>
    /// is what the last attempt ran into. Temperatures are in Localization's scale
    /// (<see cref="WeatherUnits.Fahrenheit"/>) unless <paramref name="fahrenheit"/> says which.
    /// </summary>
    public static WeatherReading From(WeatherCache? cache, WeatherOptions options, DateTime utcNow, string? problem, bool? fahrenheit = null)
    {
        // §253: the place picked, or what the device's position is called, else "Current location".
        var place = PlaceNames.Of(options);
        if (!options.IsSet)
        {
            return new WeatherReading { State = WeatherState.NoPlace, Place = NoPlaceTitle, Problem = null };
        }
        if (cache?.Forecast is not { Current: { } now } forecast)
        {
            return new WeatherReading
            {
                State = problem is { Length: > 0 } ? WeatherState.Failed : WeatherState.Loading,
                Place = place,
                Problem = problem,
            };
        }
        // The answer is kept in the unit it was fetched in; everything shown is converted from it.
        var held = cache.Fahrenheit;
        var wanted = fahrenheit ?? WeatherUnits.Fahrenheit;
        double? Degrees(double? value) => WeatherUnits.Temperature(value, held, wanted);
        var localNow = WeatherWindow.LocalNow(forecast, utcNow);
        var (condition, glyph) = WeatherCodes.Describe(now.WeatherCode, now.IsDay != 0);
        double? todayHigh = forecast.Daily is { High: [{ } first, ..] } ? first : null;
        double? todayLow = forecast.Daily is { Low: [{ } least, ..] } ? least : null;
        var detail = new List<string>();
        if (Degrees(now.Apparent) is { } feels)
        {
            detail.Add(S.WidgetWeatherFeelsLike(WeatherUnits.Degrees(feels)));
        }
        if (Degrees(todayHigh) is { } high)
        {
            detail.Add($"{HighWord} {WeatherUnits.Degrees(high)}");
        }
        if (Degrees(todayLow) is { } low)
        {
            detail.Add($"{LowWord} {WeatherUnits.Degrees(low)}");
        }
        // The one answer the cell on the bar asks too, on the weather's own interval: WeatherAge no
        // longer guesses at a range, so each kind hands over the one it really asks at.
        var stale = IsStale(cache, utcNow, problem);
        var today = DateOnly.FromDateTime(localNow);
        var slots = WeatherWindow.Today(forecast, today);
        // A reading too old to trust is no reading of "now": the mark and the arrow stay away
        // rather than saying where a temperature nobody has measured lately is heading. Nor is
        // there anything to mark on a day the answer says nothing about.
        var mark = stale || slots.Count == 0 ? NoMark : (localNow - localNow.Date).TotalHours;
        return new WeatherReading
        {
            State = WeatherState.Ready,
            Place = place,
            Temperature = WeatherUnits.DegreesWithUnit(Degrees(now.Temperature), wanted),
            Glyph = glyph,
            Condition = condition,
            Detail = string.Join(" · ", detail),
            Hours = Strip(slots, today, held, wanted),
            Next = Ahead(WeatherWindow.Hours(forecast, localNow, NextHours), today, held, wanted),
            Trend = stale ? WeatherTrend.Unknown : WeatherTrends.Of(forecast, localNow),
            NowAt = mark,
            NowValue = mark > NoMark ? Degrees(now.Temperature) : null,
            Days = List(forecast, today, held, wanted),
            Updated = WeatherAge.Ago(cache.FetchedUtc, utcNow),
            Problem = problem,
            Stale = stale,
        };
    }

    private static List<HourCell> Strip(IReadOnlyList<HourSlot> slots, DateOnly day, bool held, bool wanted)
    {
        var midnight = day.ToDateTime(TimeOnly.MinValue);
        var cells = new List<HourCell>();
        foreach (var slot in slots)
        {
            var value = WeatherUnits.Temperature(slot.Temperature, held, wanted);
            // Hours from this day's midnight, so tomorrow's first hour is the axis's 24.
            var hour = (slot.At - midnight).TotalHours;
            cells.Add(new HourCell(
                hour,
                hour >= AxisHours ? EndOfDay(slot.At) : Nls.Time(slot.At),
                WeatherCodes.Describe(slot.Code, slot.Day).Glyph,
                WeatherUnits.Degrees(value),
                WeatherUnits.Chance(slot.Chance),
                value,
                WeatherCodes.Describe(slot.Code, slot.Day).Text));
        }
        return cells;
    }

    /// <summary>§590: the next hours as the List writes them: each its own time, never the axis's "24:00".</summary>
    private static List<HourCell> Ahead(IReadOnlyList<HourSlot> slots, DateOnly day, bool held, bool wanted)
    {
        var midnight = day.ToDateTime(TimeOnly.MinValue);
        return [.. slots.Select(slot =>
        {
            var value = WeatherUnits.Temperature(slot.Temperature, held, wanted);
            var (text, glyph) = WeatherCodes.Describe(slot.Code, slot.Day);
            return new HourCell((slot.At - midnight).TotalHours, Nls.Time(slot.At), glyph, WeatherUnits.Degrees(value), WeatherUnits.Chance(slot.Chance), value, text);
        })];
    }

    /// <summary>
    /// What the end of the axis is called. A region that counts to 24 says "24:00" there, which is
    /// how the day ends rather than how the next one starts; one that counts to 12 keeps its own
    /// midnight ("12 AM"), where a 24 would mean nothing. The test is whether the region writes one
    /// in the afternoon as 13 or as 1.
    /// </summary>
    internal static string EndOfDay(DateTime midnight)
    {
        var afternoon = Nls.Time(midnight.Date.AddHours(13));
        return afternoon.Contains("13", StringComparison.Ordinal) ? "24:00" : Nls.Time(midnight);
    }

    private static List<DayRow> List(Forecast forecast, DateOnly today, bool held, bool wanted)
    {
        var slots = WeatherWindow.Days(forecast, today, ListDays);
        // One scale for the whole list, so a bar's place means the same on every row.
        var coldest = double.MaxValue;
        var warmest = double.MinValue;
        foreach (var slot in slots)
        {
            if (WeatherUnits.Temperature(slot.Low, held, wanted) is { } low)
            {
                coldest = Math.Min(coldest, low);
                warmest = Math.Max(warmest, low);
            }
            if (WeatherUnits.Temperature(slot.High, held, wanted) is { } high)
            {
                coldest = Math.Min(coldest, high);
                warmest = Math.Max(warmest, high);
            }
        }
        var span = warmest - coldest;
        var rows = new List<DayRow>();
        foreach (var slot in slots)
        {
            var low = WeatherUnits.Temperature(slot.Low, held, wanted);
            var high = WeatherUnits.Temperature(slot.High, held, wanted);
            var (from, to) = span > 0 && low is { } start && high is { } end
                ? ((float)((start - coldest) / span), (float)((end - coldest) / span))
                : (0f, 1f);
            rows.Add(new DayRow(
                slot.On == today ? S.WidgetWeatherToday : Nls.ShortDayName(slot.On.DayOfWeek),
                WeatherCodes.Describe(slot.Code, day: true).Glyph,
                WeatherCodes.Describe(slot.Code, day: true).Text,
                WeatherUnits.Degrees(low),
                WeatherUnits.Degrees(high),
                WeatherUnits.Chance(slot.Chance),
                Math.Clamp(from, 0, 1),
                Math.Clamp(to, 0, 1),
                slot.On == today,
                slot.On));
        }
        return rows;
    }
}
