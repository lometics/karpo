// §635: the samples of a place's forecasts — the weather, the air and its pollen, the tides, the sun and the earthquakes round
// it — each shaped as its service's answer is and read by the widget's own parser, against the clock now (Clock.Now), so a
// sample is always current: Zürich's weather, air and pollen (an inland city of Europe with every pollutant and pollen
// measured), Brest's sea (a coast whose tides turn twice a day), Zürich's sun, a week of earthquakes round Zürich.

using System.Globalization;
using System.Text;
using Karpo.Core;
using Karpo.Dock.Weather;

namespace Karpo.Dock;

internal static partial class Samples
{
    /// <summary>Zürich's clock: two hours ahead of UTC, as the weather's and the air's answers say.</summary>
    public const int ZurichOffsetSeconds = 7200;

    /// <summary>Zürich, where the weather, the air, the sun and the earthquakes are for.</summary>
    public static (double Latitude, double Longitude) Zurich => (47.37, 8.54);

    // --- The weather ---------------------------------------------------------------------------------

    /// <summary>Zürich's forecast as the store would hold it, fetched a quarter of an hour ago.</summary>
    public static WeatherCache Weather(DateTime? fetchedUtc = null) => new()
    {
        FetchedUtc = fetchedUtc ?? Clock.Now().ToUniversalTime().AddMinutes(-15),
        Latitude = Zurich.Latitude,
        Longitude = Zurich.Longitude,
        Fahrenheit = false,
        Forecast = WeatherForecasts.Parse(WeatherAnswer()),
    };

    /// <summary>A weather line's source over the sample: nothing is fetched, Refresh does nothing.</summary>
    public static WeatherSource WeatherSource(WeatherCache? cache = null, string? problem = null)
    {
        var held = cache ?? Weather();
        return new WeatherSource { Read = () => (held, problem, false), Ask = () => false };
    }

    /// <summary>
    /// Zürich's forecast, shaped as Open-Meteo's: the reading at the place's local time now, two days of hours from its local
    /// midnight, a week of days from today.
    /// </summary>
    public static string WeatherAnswer()
    {
        var localNow = Clock.Now().ToUniversalTime().AddSeconds(ZurichOffsetSeconds);
        var start = localNow.Date;
        int[] codes = [0, 1, 2, 3, 45, 61, 63, 71, 80, 95];
        var times = new StringBuilder();
        var temperatures = new StringBuilder();
        var weather = new StringBuilder();
        var daylight = new StringBuilder();
        var chance = new StringBuilder();
        for (var hour = 0; hour < 48; hour++)
        {
            var at = start.AddHours(hour);
            var comma = hour == 0 ? "" : ",";
            times.Append(comma).Append('"').Append(Minute(at)).Append('"');
            // A day that wanders as a real one does: coldest before dawn, warmest mid-afternoon.
            var value = 11 + (7 * Math.Sin((at.Hour - 4) * Math.PI / 14)) + (hour % 5 * 0.3);
            temperatures.Append(comma).Append(Math.Round(value, 1).ToString("0.0", CultureInfo.InvariantCulture));
            weather.Append(comma).Append(codes[at.Hour % codes.Length].ToString(CultureInfo.InvariantCulture));
            daylight.Append(comma).Append(at.Hour is >= 7 and < 19 ? '1' : '0');
            chance.Append(comma).Append(((at.Hour * 7) % 90).ToString(CultureInfo.InvariantCulture));
        }
        var days = new StringBuilder();
        var dayCodes = new StringBuilder();
        var highs = new StringBuilder();
        var lows = new StringBuilder();
        var dayChance = new StringBuilder();
        var sunrise = new StringBuilder();
        var sunset = new StringBuilder();
        for (var day = 0; day < 7; day++)
        {
            var on = start.AddDays(day);
            var comma = day == 0 ? "" : ",";
            days.Append(comma).Append('"').Append(on.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append('"');
            dayCodes.Append(comma).Append(codes[(day * 3) % codes.Length].ToString(CultureInfo.InvariantCulture));
            highs.Append(comma).Append((18.4 - day).ToString("0.0", CultureInfo.InvariantCulture));
            lows.Append(comma).Append((8.1 - (day * 0.8)).ToString("0.0", CultureInfo.InvariantCulture));
            dayChance.Append(comma).Append(((day * 17) % 80).ToString(CultureInfo.InvariantCulture));
            sunrise.Append(comma).Append('"').Append(Minute(on.AddHours(7).AddMinutes(12 + day))).Append('"');
            sunset.Append(comma).Append('"').Append(Minute(on.AddHours(19).AddMinutes(28 - day))).Append('"');
        }
        var now = localNow.Date.AddHours(localNow.Hour);
        return $$"""
            {
              "latitude": 47.37, "longitude": 8.54, "timezone": "Europe/Zurich",
              "utc_offset_seconds": {{ZurichOffsetSeconds}},
              "current": {
                "time": "{{Minute(now)}}",
                "temperature_2m": 16.4, "apparent_temperature": 15.1,
                "weather_code": 2, "wind_speed_10m": 7.2, "is_day": 1
              },
              "hourly": {
                "time": [{{times}}],
                "temperature_2m": [{{temperatures}}],
                "weather_code": [{{weather}}],
                "is_day": [{{daylight}}],
                "precipitation_probability": [{{chance}}]
              },
              "daily": {
                "time": [{{days}}],
                "weather_code": [{{dayCodes}}],
                "temperature_2m_max": [{{highs}}],
                "temperature_2m_min": [{{lows}}],
                "precipitation_probability_max": [{{dayChance}}],
                "sunrise": [{{sunrise}}],
                "sunset": [{{sunset}}]
              }
            }
            """;
    }

    private static string Minute(DateTime at) => at.ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture);

    // --- The air -------------------------------------------------------------------------------------

    /// <summary>The air as the store would hold it, fetched a quarter of an hour ago: Zürich's (pollen, the European index) or New York's (no pollen, the US index).</summary>
    public static AirCache Air(double european = 23, double us = 54, bool europe = true, DateTime? fetchedUtc = null, string pollen = "0.0") => new()
    {
        FetchedUtc = fetchedUtc ?? Clock.Now().ToUniversalTime().AddMinutes(-15),
        Latitude = europe ? 47.37 : 40.71,
        Longitude = europe ? 8.54 : -74.01,
        Answer = AirAnswers.Parse(AirAnswer(european, us, europe, pollen)),
    };

    /// <summary>An air line's source over <paramref name="cache"/> (the sample where none): nothing fetched.</summary>
    public static AirSource AirSource(AirCache? cache = null, string? problem = null)
    {
        var held = cache ?? Air();
        return new AirSource { Read = () => (held, problem, false) };
    }

    /// <summary>
    /// The air shaped as Open-Meteo's air-quality answer: both indices, the pollutants and the pollen now, and two days of both
    /// indices hour by hour from the place's local midnight; Zürich's (birch and ragweed pollen in the air) or New York's (none).
    /// </summary>
    public static string AirAnswer(double european = 23, double us = 54, bool europe = true, string pollen = "0.0")
    {
        var offset = europe ? ZurichOffsetSeconds : -14400;
        var localNow = Clock.Now().ToUniversalTime().AddSeconds(offset);
        var start = localNow.Date;
        var times = new StringBuilder();
        var eu = new StringBuilder();
        var usa = new StringBuilder();
        for (var hour = 0; hour < 48; hour++)
        {
            var comma = hour == 0 ? "" : ",";
            times.Append(comma).Append('"').Append(Minute(start.AddHours(hour))).Append('"');
            // A day that climbs through the afternoon and falls in the night, across the bands.
            var wave = Math.Sin((hour - 6) * Math.PI / 12);
            eu.Append(comma).Append(Math.Round(european + (wave * 18) + (hour % 3), 0).ToString(CultureInfo.InvariantCulture));
            usa.Append(comma).Append(Math.Round(us + (wave * 40) + (hour % 4), 0).ToString(CultureInfo.InvariantCulture));
        }
        var nowHour = Minute(localNow.Date.AddHours(localNow.Hour));
        var plants = europe
            ? $$""" "alder_pollen": 0.0, "birch_pollen": 3.4, "grass_pollen": {{pollen}}, "mugwort_pollen": 0.0, "olive_pollen": 0.0, "ragweed_pollen": 1.2 """
            : """ "alder_pollen": null, "birch_pollen": null, "grass_pollen": null, "mugwort_pollen": null, "olive_pollen": null, "ragweed_pollen": null """;
        return $$"""
            {
              "latitude": {{(europe ? "47.4" : "40.7")}}, "longitude": {{(europe ? "8.5" : "-74.0")}}, "generationtime_ms": 1.2,
              "utc_offset_seconds": {{offset}}, "timezone": "{{(europe ? "Europe/Zurich" : "America/New_York")}}",
              "current_units": { "time": "iso8601", "interval": "seconds", "european_aqi": "EAQI", "us_aqi": "USAQI", "pm2_5": "μg/m³" },
              "current": {
                "time": "{{nowHour}}", "interval": 3600,
                "european_aqi": {{european.ToString(CultureInfo.InvariantCulture)}}, "us_aqi": {{us.ToString(CultureInfo.InvariantCulture)}},
                "pm10": 14.2, "pm2_5": 8.6, "nitrogen_dioxide": 12.0, "ozone": 61.4, "sulphur_dioxide": 0.2, "carbon_monoxide": 167.0,
                {{plants}}
              },
              "hourly_units": { "time": "iso8601", "european_aqi": "EAQI", "us_aqi": "USAQI" },
              "hourly": {
                "time": [{{times}}],
                "european_aqi": [{{eu}}],
                "us_aqi": [{{usa}}]
              }
            }
            """;
    }

    // --- The tides -----------------------------------------------------------------------------------

    /// <summary>Brest's clock: two hours ahead of UTC, as the answer says.</summary>
    public const int BrestOffsetSeconds = 7200;

    /// <summary>The semidiurnal period, 12 h 25 min.</summary>
    public static readonly TimeSpan TidePeriod = TimeSpan.FromMinutes(745);

    /// <summary>A high of the sample's sea: today's at 15:32 UTC, every period either side of it another.</summary>
    public static DateTime TideHighUtc => new DateTime(Clock.Now().ToUniversalTime().Date.Ticks, DateTimeKind.Utc).AddHours(15).AddMinutes(32);

    /// <summary>Where the sample's series starts: yesterday's midnight UTC; four days of quarters from it.</summary>
    public static DateTime TideStart => new DateTime(Clock.Now().ToUniversalTime().Date.Ticks, DateTimeKind.Utc).AddDays(-1);

    /// <summary>Brest's sea: 2.1 m either side of mean sea level, its highs at <see cref="TideHighUtc"/> and every period from it, a little higher every other.</summary>
    public static double TideWave(DateTime utc)
    {
        var turns = (utc - TideHighUtc).TotalMinutes / TidePeriod.TotalMinutes;
        return (2.1 * Math.Cos(2 * Math.PI * turns)) + (0.3 * Math.Cos(Math.PI * turns));
    }

    /// <summary>The sea, or an inland place's nulls, as the store would hold it, fetched an hour ago.</summary>
    public static TideCache Tides(bool inland = false, DateTime? fetchedUtc = null) => new()
    {
        FetchedUtc = fetchedUtc ?? Clock.Now().ToUniversalTime().AddHours(-1),
        Latitude = 48.38,
        Longitude = -4.49,
        Answer = TideAnswers.Parse(TideAnswer(at => inland ? null : TideWave(at), TideStart, 4 * 96)),
    };

    /// <summary>A tides line's source over <paramref name="cache"/> (the sample where none): nothing fetched.</summary>
    public static TideSource TideSource(TideCache? cache = null, string? problem = null)
    {
        var held = cache ?? Tides();
        return new TideSource { Read = () => (held, problem, false) };
    }

    /// <summary>
    /// Brest's sea shaped as Open-Meteo's marine answer with timeformat=unixtime: <paramref name="height"/> every
    /// <paramref name="step"/> minutes (null for none) from <paramref name="startUtc"/>, <paramref name="count"/> of them, and
    /// every hour of it as the hourly series.
    /// </summary>
    public static string TideAnswer(Func<DateTime, double?> height, DateTime startUtc, int count, int step = 15, int offset = BrestOffsetSeconds, bool quarters = true)
    {
        var times = new StringBuilder();
        var heights = new StringBuilder();
        var hourTimes = new StringBuilder();
        var hourHeights = new StringBuilder();
        for (var index = 0; index < count; index++)
        {
            var at = startUtc.AddMinutes(index * step);
            var seconds = (long)(at - DateTime.UnixEpoch).TotalSeconds;
            var value = height(at) is { } metres ? Math.Round(metres, 2).ToString(CultureInfo.InvariantCulture) : "null";
            times.Append(index == 0 ? "" : ",").Append(seconds.ToString(CultureInfo.InvariantCulture));
            heights.Append(index == 0 ? "" : ",").Append(value);
            if (at.Minute == 0)
            {
                hourTimes.Append(hourTimes.Length == 0 ? "" : ",").Append(seconds.ToString(CultureInfo.InvariantCulture));
                hourHeights.Append(hourHeights.Length == 0 ? "" : ",").Append(value);
            }
        }
        var quarterly = quarters
            ? $$""" "minutely_15_units": { "time": "unixtime", "sea_level_height_msl": "m" }, "minutely_15": { "time": [{{times}}], "sea_level_height_msl": [{{heights}}] }, """
            : "";
        return $$"""
            {
              "latitude": 48.291664, "longitude": -4.4583282, "generationtime_ms": 0.2,
              "utc_offset_seconds": {{offset}}, "timezone": "Europe/Paris", "timezone_abbreviation": "GMT+2", "elevation": 0.0,
              {{quarterly}}
              "hourly_units": { "time": "unixtime", "sea_level_height_msl": "m" },
              "hourly": { "time": [{{hourTimes}}], "sea_level_height_msl": [{{hourHeights}}] }
            }
            """;
    }

    // --- The earthquakes -----------------------------------------------------------------------------

    /// <summary>
    /// A week of earthquakes round Zürich as the store would hold it, newest first — one of each class, the newest
    /// <paramref name="newest"/> 42 minutes ago — fetched three minutes ago, this PC said to be in Zürich.
    /// </summary>
    public static QuakeHeld Quakes(double newest = 4.8, DateTime? fetched = null, string? problem = null)
    {
        var now = Clock.Now().ToUniversalTime();
        var feed = QuakeFeed(
            ("n1", newest, 46.5, 9.9, now.AddMinutes(-42), "Graubünden, Switzerland"),
            ("n2", 3.1, 45.9, 6.9, now.AddHours(-5), "Haute-Savoie, France"),
            ("n3", 5.6, 42.3, 13.4, now.AddHours(-30), "Central Italy"),
            ("n4", 6.4, 38.1, 20.9, now.AddDays(-2), "Ionian Sea"),
            ("n5", 4.1, 47.8, 7.6, now.AddDays(-3), "Baden-Württemberg, Germany"),
            ("n6", 7.3, 40.2, 27.1, now.AddDays(-4), "Western Turkey"),
            ("n7", 2.6, 47.1, 8.2, now.AddDays(-5), "Lucerne, Switzerland"),
            ("n8", 8.1, 36.5, 22.0, now.AddDays(-6), "Southern Greece"));
        return new QuakeHeld { Quakes = QuakeFeeds.Parse(feed), FetchedUtc = fetched ?? now.AddMinutes(-3), Problem = problem, Device = Zurich };
    }

    /// <summary>A USGS feed of these earthquakes, in its own shape.</summary>
    private static string QuakeFeed(params (string Id, double Magnitude, double Latitude, double Longitude, DateTime Utc, string Place)[] events)
    {
        var features = new StringBuilder();
        foreach (var (id, magnitude, latitude, longitude, utc, place) in events)
        {
            var ms = (long)(utc - DateTime.UnixEpoch).TotalMilliseconds;
            var mag = magnitude.ToString(CultureInfo.InvariantCulture);
            var lat = latitude.ToString(CultureInfo.InvariantCulture);
            var lon = longitude.ToString(CultureInfo.InvariantCulture);
            features.Append(features.Length == 0 ? "" : ",");
            features.Append($$"""{"type":"Feature","properties":{"mag":{{mag}},"place":"{{place}}","time":{{ms}},"url":"https://earthquake.usgs.gov/earthquakes/eventpage/{{id}}","ids":",{{id}},","type":"earthquake"},"geometry":{"type":"Point","coordinates":[{{lon}},{{lat}},10]},"id":"{{id}}"}""");
        }
        return $$"""{"type":"FeatureCollection","metadata":{"count":{{events.Length}}},"features":[{{features}}]}""";
    }
}
