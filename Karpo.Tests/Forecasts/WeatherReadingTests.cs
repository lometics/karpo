// The weather flyout's reading, without a window and without the network: a
// saved Open-Meteo answer goes in, and everything the panel says comes out.
//
// The fixtures are trimmed copies of real answers (api.open-meteo.com/v1/
// forecast with current, hourly and daily), so the field names and their shapes
// are the ones Karpo actually gets. Nothing here reaches the network.

using Karpo.Core;
using Karpo.Dock;
using Karpo.Dock.QuickSettings.Controls;
using Karpo.Dock.Weather;

namespace Karpo.Tests.Forecasts;

public sealed class WeatherReadingTests : IDisposable
{
    /// <summary>The place's clock is two hours ahead of UTC in the fixtures.</summary>
    private const int OffsetSeconds = 7200;

    private readonly LocalizationOptions? formats = Karpo.Dock.Panels.Nls.Formats;

    /// <summary>
    /// The readings here are in Celsius whatever region this PC is set to: the scale is
    /// Localization's (Nls.Fahrenheit), which follows the region until set, so it is set.
    /// </summary>
    public WeatherReadingTests() => Karpo.Dock.Panels.Nls.Formats = new LocalizationOptions { Temperature = TemperatureUnit.Celsius };

    public void Dispose() => Karpo.Dock.Panels.Nls.Formats = formats;

    private static WeatherOptions Zurich() => new()
    {
        Place = "Zürich, Switzerland",
        Latitude = 47.37,
        Longitude = 8.54,
    };

    /// <summary>A trimmed real answer: the reading at 22:15 local, six hours and three days.</summary>
    private const string Sample = """
        {
          "latitude": 47.36, "longitude": 8.539999, "utc_offset_seconds": 7200, "timezone": "Europe/Zurich",
          "current_units": { "temperature_2m": "°C", "wind_speed_10m": "km/h" },
          "current": { "time": "2026-09-22T22:15", "temperature_2m": 16.5, "apparent_temperature": 14.5,
                       "weather_code": 0, "wind_speed_10m": 7.2, "is_day": 0 },
          "hourly": {
            "time": ["2026-09-22T21:00","2026-09-22T22:00","2026-09-22T23:00","2026-09-23T00:00","2026-09-23T01:00","2026-09-23T02:00"],
            "temperature_2m": [17.2, 16.7, 15.7, 15.1, 14.6, 14.2],
            "weather_code": [0, 0, 0, 2, 2, 61],
            "is_day": [0, 0, 0, 0, 0, 0],
            "precipitation_probability": [0, 0, 3, 9, 10, 55]
          },
          "daily": {
            "time": ["2026-09-22","2026-09-23","2026-09-24"],
            "weather_code": [2, 2, 3],
            "temperature_2m_max": [20.8, 22.0, 21.1],
            "temperature_2m_min": [11.0, 10.8, 10.9],
            "precipitation_probability_max": [0, 5, 60]
          }
        }
        """;

    // --- Reading the answer -----------------------------------------------------------

    [Fact]
    public void ReadsWhatOpenMeteoSends()
    {
        var forecast = WeatherForecasts.Parse(Sample);
        Assert.Equal("Europe/Zurich", forecast.Timezone);
        Assert.Equal(OffsetSeconds, forecast.UtcOffsetSeconds!.Value);
        Assert.Equal(16.5, forecast.Current!.Temperature);
        Assert.Equal(14.5, forecast.Current.Apparent);
        Assert.Equal(0, forecast.Current.WeatherCode!.Value);
        Assert.Equal(0, forecast.Current.IsDay!.Value);
        Assert.Equal(6, forecast.Hourly!.Time!.Length);
        Assert.Equal(55d, forecast.Hourly.PrecipitationChance![5]!.Value);
        Assert.Equal(3, forecast.Daily!.Time!.Length);
        Assert.Equal(20.8, forecast.Daily.High![0]);
        Assert.Equal(60d, forecast.Daily.PrecipitationChance![2]!.Value);
    }

    [Fact]
    public void AnAnswerWithNoTemperatureIsRefused()
    {
        Assert.Throws<InvalidDataException>(() => WeatherForecasts.Parse("""{ "current": { "time": "2026-09-22T22:15" } }"""));
        Assert.Throws<InvalidDataException>(() => WeatherForecasts.Parse("{ not json"));
        Assert.Throws<InvalidDataException>(() => WeatherForecasts.Parse("null"));
    }

    [Fact]
    public void OneRequestAskedPerPlaceCarriesTheHoursAndTheWeek()
    {
        var url = WeatherStore.Url(47.3744, 8.5410);
        // Two decimals: about a kilometre, and no more of where the person is than that.
        Assert.Contains("latitude=47.37", url, StringComparison.Ordinal);
        Assert.Contains("longitude=8.54", url, StringComparison.Ordinal);
        Assert.Contains("hourly=temperature_2m,weather_code,is_day,precipitation_probability", url, StringComparison.Ordinal);
        Assert.Contains("precipitation_probability_max", url, StringComparison.Ordinal);
        // §572: no day and night shown on the weather's graph any more, so no sun times asked for.
        Assert.DoesNotContain("sunrise", url, StringComparison.Ordinal);
        Assert.DoesNotContain("sunset", url, StringComparison.Ordinal);
        Assert.Contains("forecast_days=7", url, StringComparison.Ordinal);
        Assert.Contains("timezone=auto", url, StringComparison.Ordinal);
        // Always Celsius: the unit is chosen where the forecast is shown, not where it is fetched.
        Assert.DoesNotContain("temperature_unit", url, StringComparison.Ordinal);
    }

    [Fact]
    public void OneForecastServesEveryWidgetOfThePlaceWhateverItsUnit()
    {
        var named = Zurich();
        named.Place = "Zurich";
        Assert.Equal(WeatherStore.KeyOf(Zurich()), WeatherStore.KeyOf(named));
        Assert.NotEqual(WeatherStore.KeyOf(Zurich()), WeatherStore.KeyOf(new WeatherOptions { Latitude = 51.5, Longitude = -0.13 }));
        Assert.NotEqual(WeatherStore.KeyOf(Zurich()), WeatherStore.KeyOf(new WeatherOptions { UseDeviceLocation = true }));
    }

    // --- Codes, units and rounding -----------------------------------------------------

    [Fact]
    public void CodesBecomeWordsAndGlyphs()
    {
        Assert.Equal(("Sunny", WeatherCodes.Sun), WeatherCodes.Describe(0, day: true));
        Assert.Equal(("Clear", WeatherCodes.Moon), WeatherCodes.Describe(0, day: false));
        Assert.Equal(("Mostly clear", WeatherCodes.Moon), WeatherCodes.Describe(1, day: false));
        Assert.Equal(("Cloudy", WeatherCodes.Cloud), WeatherCodes.Describe(3, day: true));
        Assert.Equal(("Fog", WeatherCodes.Cloud), WeatherCodes.Describe(48, day: false));
        Assert.Equal(("Rain", WeatherCodes.Drop), WeatherCodes.Describe(63, day: true));
        Assert.Equal(("Heavy snow", WeatherCodes.Frigid), WeatherCodes.Describe(75, day: true));
        Assert.Equal(("Thunderstorm", WeatherCodes.Bolt), WeatherCodes.Describe(95, day: true));
        // A code Open-Meteo may add later, and a missing one, both say so rather than lying.
        Assert.Equal(("Unknown", WeatherCodes.Unknown), WeatherCodes.Describe(4, day: true));
        Assert.Equal(("Unknown", WeatherCodes.Unknown), WeatherCodes.Describe(null, day: true));
    }

    /// <summary>
    /// The sun keeps its own colour in both themes, and reads in each against what it is drawn on — a
    /// graphic needs 3:1. Windows' amber, #FFB900, stays on a dark surface; on a light one it was a
    /// faint 1.7:1 against a white card, and Fluent's caution amber stands in for it there. Every other
    /// glyph is the text's, and high contrast has the text's for the sun as well.
    /// </summary>
    [Fact]
    public void TheSunReadsOnEitherTheme()
    {
        foreach (var dark in new[] { false, true })
        {
            var theme = FlyoutTheme.For(Karpo.Tests.Folders.TestLooks.LookFor(dark));
            var sun = WeatherCodes.Ink(WeatherCodes.Sun, theme);
            // The flyout's own ground, and a card on it.
            var ground = (theme.Base.R / 255.0, theme.Base.G / 255.0, theme.Base.B / 255.0);
            var alpha = theme.CardBackground.A / 255.0;
            var card = (
                (theme.CardBackground.R / 255.0 * alpha) + (ground.Item1 * (1 - alpha)),
                (theme.CardBackground.G / 255.0 * alpha) + (ground.Item2 * (1 - alpha)),
                (theme.CardBackground.B / 255.0 * alpha) + (ground.Item3 * (1 - alpha)));
            foreach (var (under, on) in new[] { (ground, "the flyout"), (card, "a card") })
            {
                var contrast = Contrast((sun.r, sun.g, sun.b), under);
                Assert.True(contrast >= 3, $"the sun on {on} in the {(dark ? "dark" : "light")} theme is {contrast:0.0}:1");
            }
            Assert.Equal(theme.Text, WeatherCodes.Ink(WeatherCodes.Cloud, theme));
        }
        // Windows' own amber in the dark, as before.
        var night = WeatherCodes.Ink(WeatherCodes.Sun, FlyoutTheme.For(Karpo.Tests.Folders.TestLooks.LookFor(dark: true)));
        Assert.Equal((1f, 0.725f, 0f, 1f), (night.r, night.g, night.b, night.a));
    }

    /// <summary>WCAG's contrast ratio between two opaque sRGB colours, each channel from 0 to 1.</summary>
    private static double Contrast((double R, double G, double B) one, (double R, double G, double B) two)
    {
        static double Linear(double channel) => channel <= 0.04045 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);
        static double Luminance((double R, double G, double B) colour) => (0.2126 * Linear(colour.R)) + (0.7152 * Linear(colour.G)) + (0.0722 * Linear(colour.B));
        var (a, b) = (Luminance(one), Luminance(two));
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    [Fact]
    public void DegreesRoundAwayFromZeroAndNeverReadMinusZero()
    {
        Assert.Equal("18°", WeatherUnits.Degrees(18.4));
        Assert.Equal("19°", WeatherUnits.Degrees(18.5));
        Assert.Equal("-19°", WeatherUnits.Degrees(-18.5));
        Assert.Equal("0°", WeatherUnits.Degrees(0.4));
        Assert.Equal("0°", WeatherUnits.Degrees(-0.4));
        Assert.Equal("-1°", WeatherUnits.Degrees(-0.6));
        Assert.Equal("–", WeatherUnits.Degrees(null));
        Assert.Equal("17°C", WeatherUnits.DegreesWithUnit(16.5, fahrenheit: false));
        Assert.Equal("62°F", WeatherUnits.DegreesWithUnit(61.7, fahrenheit: true));
    }

    [Fact]
    public void TemperaturesAndSpeedsConvertBetweenTheUnits()
    {
        Assert.Equal(32, WeatherUnits.ToFahrenheit(0), 6);
        Assert.Equal(212, WeatherUnits.ToFahrenheit(100), 6);
        Assert.Equal(-40, WeatherUnits.ToFahrenheit(-40), 6);
        Assert.Equal(37, WeatherUnits.ToCelsius(98.6), 6);
        // Held in one unit and wanted in the same one: not a number touched.
        Assert.Equal(16.5, WeatherUnits.Temperature(16.5, held: false, wanted: false));
        Assert.Equal(61.7, WeatherUnits.Temperature(16.5, held: false, wanted: true)!.Value, 1);
        Assert.Equal(16.5, WeatherUnits.Temperature(61.7, held: true, wanted: false)!.Value, 1);
        Assert.Null(WeatherUnits.Temperature(null, held: false, wanted: true));
        Assert.Equal("6 mph", WeatherUnits.Speed(WeatherUnits.Wind(10, held: false, wanted: true), fahrenheit: true));
        Assert.Equal("10 km/h", WeatherUnits.Speed(WeatherUnits.Wind(10, held: false, wanted: false), fahrenheit: false));
        Assert.Equal("", WeatherUnits.Speed(null, fahrenheit: false));
    }

    [Fact]
    public void ASmallChanceOfRainIsLeftOut()
    {
        Assert.Equal("", WeatherUnits.Chance(null));
        Assert.Equal("", WeatherUnits.Chance(0));
        Assert.Equal("", WeatherUnits.Chance(WeatherUnits.LeastChance - 1));
        Assert.Equal("10%", WeatherUnits.Chance(WeatherUnits.LeastChance));
        Assert.Equal("55%", WeatherUnits.Chance(55));
    }

    // --- The hour and day windows ------------------------------------------------------

    [Fact]
    public void TheHourStripStartsAfterTheReadingsOwnHour()
    {
        var forecast = WeatherForecasts.Parse(Sample);
        var hours = WeatherWindow.Hours(forecast, At("2026-09-22T22:15"), 12);
        // The reading itself is 22:15, so 22:00 has been and 23:00 is next.
        Assert.Equal(At("2026-09-22T23:00"), hours[0].At);
        Assert.Equal(15.7, hours[0].Temperature);
        Assert.Equal(3, hours[0].Chance!.Value);
        Assert.False(hours[0].Day);
        Assert.Equal(4, hours.Count);
        Assert.Equal(At("2026-09-23T02:00"), hours[^1].At);
        Assert.Equal(61, hours[^1].Code!.Value);
        Assert.Equal(55, hours[^1].Chance!.Value);
        // Fewer asked for, fewer given; none asked for, none given.
        Assert.Equal(2, WeatherWindow.Hours(forecast, At("2026-09-22T22:15"), 2).Count);
        Assert.Empty(WeatherWindow.Hours(forecast, At("2026-09-22T22:15"), 0));
    }

    [Fact]
    public void TheHourStripFollowsTheListWhenTheClocksGoBack()
    {
        // A day with two 02:00s: Open-Meteo lists both, and the strip must not stop at the first.
        var forecast = WeatherForecasts.Parse(Autumn);
        var hours = WeatherWindow.Hours(forecast, At("2026-10-25T00:30"), 12);
        Assert.Equal(
            new[] { "01:00", "02:00", "02:00", "03:00", "04:00" },
            hours.Select(hour => hour.At.ToString("HH:mm")).ToArray());
        // Five entries covering four real hours, but only three on the clock: the day is
        // twenty-five hours long, and adding an hour at a time would have missed one of them.
        Assert.Equal(5, hours.Count);
        Assert.Equal(3, (hours[^1].At - hours[0].At).TotalHours);
    }

    [Fact]
    public void TheHourStripFollowsTheListWhenTheClocksGoForward()
    {
        // A day with no 02:00 at all: the strip goes straight from 01:00 to 03:00.
        var forecast = WeatherForecasts.Parse(Spring);
        var hours = WeatherWindow.Hours(forecast, At("2026-03-29T00:30"), 12);
        Assert.Equal(
            new[] { "01:00", "03:00", "04:00", "05:00" },
            hours.Select(hour => hour.At.ToString("HH:mm")).ToArray());
        Assert.DoesNotContain(hours, hour => hour.At.Hour == 2);
    }

    [Fact]
    public void TheDayListStartsWithToday()
    {
        var forecast = WeatherForecasts.Parse(Sample);
        var days = WeatherWindow.Days(forecast, new DateOnly(2026, 9, 22), 7);
        Assert.Equal(3, days.Count);
        Assert.Equal(new DateOnly(2026, 9, 22), days[0].On);
        Assert.Equal(20.8, days[0].High);
        Assert.Equal(11.0, days[0].Low);
        Assert.Equal(60, days[^1].Chance!.Value);
        // A day already past is left out, and the list never runs longer than asked.
        Assert.Equal(2, WeatherWindow.Days(forecast, new DateOnly(2026, 9, 23), 7).Count);
        Assert.Equal(2, WeatherWindow.Days(forecast, new DateOnly(2026, 9, 22), 2).Count);
    }

    // --- Today's graph: the mark for now, and the trend ---------------------------------

    [Fact]
    public void TheAxisIsTheWholeDayWhateverTimeItIs()
    {
        var forecast = WeatherForecasts.Parse(Sample);
        // The answer holds 21:00, 22:00 and 23:00 of the 22nd, and 00:00 to 02:00 of the 23rd. The
        // axis ends on midnight, so the 22nd's graph takes the 23rd's first hour as its 24:00 —
        // a real temperature the answer holds, rather than 23:00 written out twice.
        var first = WeatherWindow.Today(forecast, new DateOnly(2026, 9, 22));
        Assert.Equal([21d, 22, 23, 24], first.Select(slot => Hours(slot.At, new DateOnly(2026, 9, 22))).ToArray());
        Assert.Equal(15.1, first[^1].Temperature);
        var second = WeatherWindow.Today(forecast, new DateOnly(2026, 9, 23));
        // The 24th is not in the answer, so the 23rd's graph simply ends at its last hour.
        Assert.Equal([0d, 1, 2], second.Select(slot => Hours(slot.At, new DateOnly(2026, 9, 23))).ToArray());
        // A day the answer says nothing about is empty; the axis still spans it, and nothing is
        // made up for the hours nobody reported.
        Assert.Empty(WeatherWindow.Today(forecast, new DateOnly(2026, 9, 24)));
        Assert.Empty(WeatherWindow.Today(WeatherForecasts.Parse(NoHours), new DateOnly(2026, 9, 22)));
    }

    [Fact]
    public void TheMarkSitsAtTheTimeOfDayItReallyIs()
    {
        // Hours since midnight, so the mark is at the same place on the axis whatever the answer
        // happens to cover: twenty past ten at night is 22.33 of the way through the day.
        Assert.Equal(22 + (1d / 3), Mark("2026-09-22T22:20"), 3);
        // Just after midnight and just before it: the day has rolled, and the mark with it.
        Assert.Equal(23 + (59d / 60), Mark("2026-09-22T23:59", "2026-09-22T23:30"), 3);
        Assert.Equal(1d / 60, Mark("2026-09-23T00:01", "2026-09-23T00:00"), 3);
        Assert.Equal(0d, Mark("2026-09-23T00:00", "2026-09-23T00:00"), 3);
        // Dawn, noon and the evening of a day whose hours the answer has.
        Assert.Equal(1d, Mark("2026-09-23T01:00", "2026-09-23T00:30"), 3);
    }

    /// <summary>Hours from <paramref name="day"/>'s midnight, as the axis counts them.</summary>
    private static double Hours(DateTime at, DateOnly day) => (at - day.ToDateTime(TimeOnly.MinValue)).TotalHours;

    /// <summary>
    /// Where the mark lands at <paramref name="localNow"/>, with the reading taken at
    /// <paramref name="reading"/> (the same time by default), through the whole chain.
    /// </summary>
    private static double Mark(string localNow, string? reading = null)
    {
        var at = At(localNow);
        var cache = new WeatherCache
        {
            FetchedUtc = Utc(localNow).AddMinutes(-5),
            Latitude = 47.37,
            Longitude = 8.54,
            Forecast = WeatherForecasts.Parse(Sample.Replace("2026-09-22T22:15", reading ?? localNow)),
        };
        // Two hours ahead of UTC, as the answer says, so the place's own clock reads localNow.
        return WeatherReading.From(cache, Zurich(), DateTime.SpecifyKind(at.AddHours(-2), DateTimeKind.Utc), null).NowAt;
    }

    [Fact]
    public void ARisingFallingOrFlatTemperatureEachSaySo()
    {
        var now = At("2026-09-22T22:20");
        // Three hours out against the reading now: +3.4 is rising, -3.4 is falling.
        Assert.Equal(WeatherTrend.Rising, WeatherTrends.Of(WeatherForecasts.Parse(Hourly(16.5, 17.0, 18.2, 19.9)), now));
        Assert.Equal(WeatherTrend.Falling, WeatherTrends.Of(WeatherForecasts.Parse(Hourly(16.5, 16.0, 14.4, 13.1)), now));
        // Dead flat is steady, and so is anything inside the dead zone either way.
        Assert.Equal(WeatherTrend.Steady, WeatherTrends.Of(WeatherForecasts.Parse(Hourly(16.5, 16.5, 16.5, 16.5)), now));
        Assert.Equal(WeatherTrend.Steady, WeatherTrends.Of(WeatherForecasts.Parse(Hourly(16.5, 17.5, 18.0, 17.4)), now));
        Assert.Equal(WeatherTrend.Steady, WeatherTrends.Of(WeatherForecasts.Parse(Hourly(16.5, 16.0, 15.8, 15.6)), now));
        // Exactly a degree counts as movement; a hair under does not.
        Assert.Equal(WeatherTrend.Rising, WeatherTrends.Of(WeatherForecasts.Parse(Hourly(16.5, 16.6, 17.0, 17.5)), now));
        Assert.Equal(WeatherTrend.Steady, WeatherTrends.Of(WeatherForecasts.Parse(Hourly(16.5, 16.6, 17.0, 17.49)), now));
        // Nothing to compare with: no hourly forecast at all, and one that runs out too soon.
        Assert.Equal(WeatherTrend.Unknown, WeatherTrends.Of(WeatherForecasts.Parse(NoHours), now));
        Assert.Equal(WeatherTrend.Unknown, WeatherTrends.Of(WeatherForecasts.Parse(Hourly(16.5, 19.9)), now));
    }

    [Fact]
    public void AForecastWithNoHoursHasNoGraphNoMarkAndNoTrend()
    {
        var cache = new WeatherCache { FetchedUtc = Utc("2026-09-22T20:15"), Latitude = 47.37, Longitude = 8.54, Forecast = WeatherForecasts.Parse(NoHours) };
        var reading = WeatherReading.From(cache, Zurich(), Utc("2026-09-22T20:20"), problem: null);
        Assert.Equal(WeatherState.Ready, reading.State);
        Assert.Empty(reading.Hours);
        Assert.Equal(WeatherReading.NoMark, reading.NowAt);
        Assert.Null(reading.NowValue);
        Assert.Equal(WeatherTrend.Unknown, reading.Trend);
    }

    [Fact]
    public void AReadingTooOldToTrustMarksNothingAndPointsNowhere()
    {
        // The temperature "now" is not known any more, so the graph says nothing about now; the
        // hours themselves are still drawn, which is all the old answer can honestly offer.
        var old = Cache(fetchedUtc: Utc("2026-09-21T20:15"));
        var reading = WeatherReading.From(old, Zurich(), Utc("2026-09-22T20:20"), problem: null);
        Assert.True(reading.Stale);
        Assert.NotEmpty(reading.Hours);
        Assert.Equal(WeatherReading.NoMark, reading.NowAt);
        Assert.Null(reading.NowValue);
        Assert.Equal(WeatherTrend.Unknown, reading.Trend);
    }

    // --- The curve ----------------------------------------------------------------------

    /// <summary>
    /// The promise the curve makes: between two hours it never leaves those two hours' own
    /// temperatures. A plain spline would overshoot around a turn and invent a peak the forecast
    /// does not contain, which on a temperature graph is a lie about the weather.
    /// </summary>
    [Theory]
    [InlineData(new[] { 8.0, 6.0, 8.0, 14.0, 20.0, 22.0, 20.0, 14.0 })]      // a day's own shape
    [InlineData(new[] { 5.0, 5.0, 5.0, 18.0, 5.0, 5.0, 5.0 })]               // a spike out of a plateau
    [InlineData(new[] { 20.0, 19.9, 19.8, 3.0, 2.9, 2.8 })]                  // a front coming through
    [InlineData(new[] { 0.0, 10.0, 0.0, 10.0, 0.0, 10.0 })]                  // nonsense, sawing up and down
    [InlineData(new[] { -4.0, -4.0, -4.0, -4.0 })]                           // dead flat, below zero
    [InlineData(new[] { 11.0, 12.0 })]                                       // two points
    public void TheCurveNeverLeavesTheHoursItRunsBetween(double[] values)
    {
        var x = values.Select((_, index) => (double)index).ToArray();
        var slopes = WeatherCurve.Tangents(x, values);
        for (var index = 0; index + 1 < values.Length; index++)
        {
            var low = Math.Min(values[index], values[index + 1]);
            var high = Math.Max(values[index], values[index + 1]);
            for (var step = 0; step <= 20; step++)
            {
                var at = index + (step / 20.0);
                var value = WeatherCurve.Value(x, values, slopes, at);
                Assert.InRange(value, low - 1e-9, high + 1e-9);
            }
        }
        // And it still passes through the hours themselves.
        for (var index = 0; index < values.Length; index++)
        {
            Assert.Equal(values[index], WeatherCurve.Value(x, values, slopes, index), 9);
        }
    }

    [Fact]
    public void TheCurveIsFlatWhereTheWeatherIs()
    {
        // A plateau pins its ends flat, so nothing bulges out of a run of equal hours.
        double[] x = [0, 1, 2, 3, 4];
        double[] y = [5, 5, 5, 9, 12];
        var slopes = WeatherCurve.Tangents(x, y);
        Assert.Equal(0, slopes[0]);
        Assert.Equal(0, slopes[1]);
        Assert.Equal(0, slopes[2]);
        Assert.True(slopes[3] > 0);
        // A single point has nowhere to go, and no points at all do not throw.
        Assert.Equal([0d], WeatherCurve.Tangents([0], [7]));
        Assert.Empty(WeatherCurve.Tangents([], []));
        Assert.Equal(0, WeatherCurve.Value([], [], [], 3));
        // The Bézier's control points are a third of the way along, as the Hermite form says.
        var (x1, y1, x2, y2) = WeatherCurve.Bezier(x, y, slopes, 3);
        Assert.Equal(3 + (1 / 3.0), x1, 9);
        Assert.Equal(9 + (slopes[3] / 3), y1, 9);
        Assert.Equal(4 - (1 / 3.0), x2, 9);
        Assert.Equal(12 - (slopes[4] / 3), y2, 9);
    }

    [Fact]
    public void TheRingTurnsOnEveryTickAndNowhereElse()
    {
        // quick's pure step, which the weather flyout uses rather than a second one of its own.
        Assert.Equal(Spinner.StepDegrees, Spinner.Next(0));
        Assert.Equal(16, Spinner.Next(Spinner.Next(0)));
        // A whole turn in a second and a half, at thirty steps a second.
        Assert.Equal(360, Spinner.StepDegrees * (1500 / Spinner.TickMs), 0);
        // It wraps rather than running away, and nonsense cannot get in.
        Assert.Equal(0, Spinner.Next(352));
        Assert.Equal(4, Spinner.Next(356));
        Assert.Equal(0, Spinner.Next(float.NaN));
    }

    // --- The whole reading -------------------------------------------------------------

    [Fact]
    public void TheReadingSaysWhatTheFlyoutShows()
    {
        var reading = WeatherReading.From(Cache(), Zurich(), Utc("2026-09-22T20:20"), problem: null);
        Assert.Equal(WeatherState.Ready, reading.State);
        Assert.Equal("Zürich", reading.Place);
        Assert.Equal("17°C", reading.Temperature);
        Assert.Equal("Clear", reading.Condition);
        Assert.Equal(WeatherCodes.Moon, reading.Glyph);
        Assert.Equal("Feels like 15° · High 21° · Low 11°", reading.Detail);
        Assert.False(reading.Stale);
        Assert.Null(reading.Problem);
        // The graph is today: the hours of the 22nd the answer carries, and midnight to end on.
        Assert.Equal(4, reading.Hours.Count);
        Assert.Equal(21d, reading.Hours[0].Hour);
        Assert.Equal("17°", reading.Hours[0].Temperature);
        Assert.Equal(17.2, reading.Hours[0].Value);
        Assert.Equal("", reading.Hours[0].Chance);
        Assert.Equal(WeatherTrend.Falling, reading.Trend);
        // Twenty past ten at night, on an axis that runs midnight to midnight.
        Assert.Equal(22 + (1d / 3), reading.NowAt, 3);
        Assert.Equal(16.5, reading.NowValue);
        // The axis ends at 24:00, on the temperature the answer gives for the coming midnight.
        Assert.Equal(24d, reading.Hours[^1].Hour);
        Assert.Equal("15°", reading.Hours[^1].Temperature);
        Assert.Equal(WeatherReading.EndOfDay(At("2026-09-23T00:00")), reading.Hours[^1].Time);
        // A region that counts to 24 calls it 24:00 rather than the next day's 00:00.
        Assert.Equal(23d, reading.Hours[^2].Hour);
        Assert.Equal("16°", reading.Hours[^2].Temperature);
        Assert.Equal("", reading.Hours[^2].Chance);
        Assert.All(reading.Hours, hour => Assert.NotEqual("", hour.Time));
        Assert.Equal(3, reading.Days.Count);
        Assert.True(reading.Days[0].Today);
        Assert.Equal("Today", reading.Days[0].Name);
        Assert.Equal("21°", reading.Days[0].High);
        Assert.Equal("11°", reading.Days[0].Low);
        Assert.False(reading.Days[1].Today);
        Assert.NotEqual("Today", reading.Days[1].Name);
        Assert.NotEqual("", reading.Days[1].Name);
    }

    /// <summary>
    /// The weather has no scale of its own (2026-10-05, "remove the own setting completely"): it
    /// shows Localization's, and while that follows Windows, the region's measurement system.
    /// </summary>
    [Fact]
    public void TheWeatherShowsLocalizationsScale()
    {
        Assert.False(WeatherUnits.Fahrenheit);
        Assert.Equal("17°C", WeatherReading.From(Cache(), Zurich(), Utc("2026-09-22T20:20"), problem: null).Temperature);

        Karpo.Dock.Panels.Nls.Formats = new LocalizationOptions { Temperature = TemperatureUnit.Fahrenheit };
        Assert.True(WeatherUnits.Fahrenheit);
        Assert.Equal("62°F", WeatherReading.From(Cache(), Zurich(), Utc("2026-09-22T20:20"), problem: null).Temperature);
        // The widest reading the cell keeps room for follows the scale too.
        Assert.Equal("188°", WeatherFaceDrawing.Warmest);

        // Following Windows: the region's answer, whatever it is on this PC.
        Karpo.Dock.Panels.Nls.Formats = new LocalizationOptions();
        Assert.Equal(Karpo.Dock.Panels.Nls.RegionFahrenheit(), WeatherUnits.Fahrenheit);
        Karpo.Dock.Panels.Nls.Formats = null;
        Assert.Equal(Karpo.Dock.Panels.Nls.RegionFahrenheit(), WeatherUnits.Fahrenheit);
    }

    /// <summary>The widget's options carry no scale and no interval: a file's old ones are not read (OnlineUpdateConfigTests).</summary>
    [Fact]
    public void TheWidgetsOptionsHaveNoScaleOrIntervalOfTheirOwn()
    {
        var names = typeof(WeatherOptions).GetProperties().Select(property => property.Name).ToList();
        Assert.DoesNotContain("Fahrenheit", names);
        Assert.DoesNotContain("RefreshMinutes", names);
        Assert.DoesNotContain("RefreshMinutes", typeof(MarketOptions).GetProperties().Select(property => property.Name));
    }

    [Fact]
    public void AWidgetSetToFahrenheitShowsACelsiusForecastInFahrenheit()
    {
        var reading = WeatherReading.From(Cache(), Zurich(), Utc("2026-09-22T20:20"), problem: null, fahrenheit: true);
        Assert.Equal("62°F", reading.Temperature);
        Assert.Equal("Feels like 58° · High 69° · Low 52°", reading.Detail);
        Assert.Equal("63°", reading.Hours[0].Temperature);
        // The mark and the line are in the unit shown, from the very same Celsius answer.
        Assert.Equal(62.96, reading.Hours[0].Value!.Value, 2);
        Assert.Equal(61.7, reading.NowValue!.Value, 1);
    }

    [Fact]
    public void TheDailyBarsShareOneScale()
    {
        var reading = WeatherReading.From(Cache(), Zurich(), Utc("2026-09-22T20:20"), problem: null);
        var coldest = reading.Days.Min(day => day.From);
        var warmest = reading.Days.Max(day => day.To);
        Assert.Equal(0f, coldest);
        Assert.Equal(1f, warmest);
        // The coldest low (10.8 on the second day) starts its bar, the warmest high (22.0) ends it.
        Assert.Equal(0f, reading.Days[1].From);
        Assert.Equal(1f, reading.Days[1].To);
        Assert.All(reading.Days, day => Assert.True(day.From <= day.To));
        Assert.All(reading.Days, day => Assert.InRange(day.From, 0f, 1f));
    }

    [Fact]
    public void AForecastThatCouldNotBeRenewedIsStillShownWithItsTime()
    {
        // The rule: a failure never empties the panel. What was fetched stays, the reason is
        // added, and the reading is marked stale so the flyout can say it may no longer hold.
        var cache = Cache(fetchedUtc: Utc("2026-09-22T20:15"));
        var reading = WeatherReading.From(cache, Zurich(), Utc("2026-09-22T20:20"), problem: "Open-Meteo can't be reached");
        Assert.Equal(WeatherState.Ready, reading.State);
        Assert.Equal("17°C", reading.Temperature);
        Assert.Equal(3, reading.Days.Count);
        Assert.Equal("Open-Meteo can't be reached", reading.Problem);
        Assert.True(reading.Stale);
        Assert.NotEqual("", reading.Updated);
    }

    [Fact]
    public void AnOldForecastIsCalledStaleAndAFreshOneIsNot()
    {
        var fetched = Utc("2026-09-22T12:00");
        Assert.False(WeatherAge.IsStale(fetched, fetched.AddMinutes(89), 30));
        Assert.True(WeatherAge.IsStale(fetched, fetched.AddMinutes(90), 30));
        // The interval the kind really asks at decides (WeatherReading.Refresh), taken as given.
        Assert.False(WeatherAge.IsStale(fetched, fetched.AddMinutes(100), 180));
        Assert.True(WeatherAge.IsStale(fetched, fetched.AddDays(1), 180));
        // A forecast on show that is old but unexplained is still stale.
        var reading = WeatherReading.From(Cache(fetchedUtc: Utc("2026-09-21T20:15")), Zurich(), Utc("2026-09-22T20:20"), problem: null);
        Assert.True(reading.Stale);
        Assert.Null(reading.Problem);
    }

    /// <summary>
    /// The cell on the bar and the panel it opens answer staleness from one function. The cell
    /// builds no reading of its own — it would be laying out hour strips and day lists to colour a
    /// temperature — so it asks <see cref="WeatherReading.IsStale"/>, and so does
    /// <see cref="WeatherReading.From"/>. This holds the two together: a stale forecast is drawn
    /// quiet on the bar and its age line goes critical in the panel, and there is no arrangement in
    /// which one of those happens without the other.
    /// </summary>
    [Fact]
    public void TheCellAndThePanelAgreeOnWhatIsStale()
    {
        var options = Zurich();
        foreach (var (fetched, problem) in new (string Fetched, string? Problem)[]
        {
            ("2026-09-22T20:15", null),          // minutes old: fresh
            ("2026-09-22T18:40", null),          // an hour and forty: past three refreshes
            ("2026-09-22T20:15", "No answer"),   // fresh, but the last fetch failed
            ("2026-09-21T20:15", null),          // a day old
        })
        {
            var cache = Cache(fetchedUtc: Utc(fetched));
            var now = Utc("2026-09-22T20:20");
            Assert.Equal(
                WeatherReading.From(cache, options, now, problem).Stale,
                WeatherReading.IsStale(cache, now, problem));
        }

        // Nothing fetched at all is not stale: that widget is still loading and says so itself.
        Assert.False(WeatherReading.IsStale(null, Utc("2026-09-22T20:20"), problem: null));
    }

    [Fact]
    public void WithoutAPlaceItSaysSoAndWithoutAForecastItWaits()
    {
        var nothing = WeatherReading.From(null, new WeatherOptions(), Utc("2026-09-22T20:20"), problem: null);
        Assert.Equal(WeatherState.NoPlace, nothing.State);
        Assert.Equal(WeatherReading.NoPlaceTitle, nothing.Place);
        Assert.Empty(nothing.Hours);
        Assert.Empty(nothing.Days);

        var waiting = WeatherReading.From(null, Zurich(), Utc("2026-09-22T20:20"), problem: null);
        Assert.Equal(WeatherState.Loading, waiting.State);
        Assert.Equal("Zürich", waiting.Place);
        Assert.Equal("", waiting.Updated);

        var failed = WeatherReading.From(null, Zurich(), Utc("2026-09-22T20:20"), problem: "Open-Meteo didn't answer");
        Assert.Equal(WeatherState.Failed, failed.State);
        Assert.Equal("Open-Meteo didn't answer", failed.Problem);

        // A place with no name of its own is still called something.
        var here = WeatherReading.From(Cache(), new WeatherOptions { UseDeviceLocation = true }, Utc("2026-09-22T20:20"), null);
        Assert.Equal(WeatherReading.Here, here.Place);
    }

    [Fact]
    public void TheUpdatedLineReadsAsHowLongAgoItWas()
    {
        var at = new DateTime(2026, 9, 22, 21, 15, 0, DateTimeKind.Local);
        Assert.Equal("Updated just now", WeatherAge.Updated(at, at.AddSeconds(20)));
        Assert.Equal("Updated 1 minute ago", WeatherAge.Updated(at, at.AddMinutes(1)));
        Assert.Equal("Updated 20 minutes ago", WeatherAge.Updated(at, at.AddMinutes(20)));
        Assert.StartsWith("Updated at ", WeatherAge.Updated(at, at.AddHours(2)), StringComparison.Ordinal);
        // A day later the date is in it too, so "Updated at 21:15" cannot read as today.
        var older = WeatherAge.Updated(at, at.AddDays(1));
        Assert.StartsWith("Updated ", older, StringComparison.Ordinal);
        Assert.Contains(" at ", older, StringComparison.Ordinal);
        Assert.DoesNotContain("Updated at ", older, StringComparison.Ordinal);
        // A clock put back does not make a reading come from the future.
        Assert.Equal("Updated just now", WeatherAge.Updated(at, at.AddHours(-3)));
    }

    // --- Fixtures -----------------------------------------------------------------------

    private static WeatherCache Cache(DateTime? fetchedUtc = null) => new()
    {
        FetchedUtc = fetchedUtc ?? Utc("2026-09-22T20:15"),
        Latitude = 47.37,
        Longitude = 8.54,
        Fahrenheit = false,
        Forecast = WeatherForecasts.Parse(Sample),
    };

    private static DateTime At(string text) => WeatherWindow.ParseHour(text)!.Value;

    /// <summary>Hours on the hour from 22:00, for the mark's arithmetic.</summary>
    private static List<HourSlot> Slots(params string[] times) =>
        [.. times.Select(time => new HourSlot(At(time), 0, Day: false, 15, 0))];

    /// <summary>An answer whose reading is 16.5 at 22:15 and whose hours are <paramref name="hours"/>, from 23:00.</summary>
    private static string Hourly(double reading, params double[] hours)
    {
        var start = At("2026-09-22T23:00");
        var times = string.Join(",", hours.Select((_, index) =>
            "\"" + start.AddHours(index).ToString("yyyy-MM-dd'T'HH:mm", System.Globalization.CultureInfo.InvariantCulture) + "\""));
        var values = string.Join(",", hours.Select(value => value.ToString("0.0#", System.Globalization.CultureInfo.InvariantCulture)));
        return $$"""
            {
              "utc_offset_seconds": 7200,
              "current": { "time": "2026-09-22T22:15", "temperature_2m": {{reading.ToString("0.0#", System.Globalization.CultureInfo.InvariantCulture)}}, "weather_code": 0, "is_day": 0 },
              "hourly": { "time": [{{times}}], "temperature_2m": [{{values}}], "weather_code": [0], "is_day": [0] },
              "daily": { "time": ["2026-09-22"], "weather_code": [0], "temperature_2m_max": [20.0], "temperature_2m_min": [11.0] }
            }
            """;
    }

    /// <summary>An answer with a reading and days but no hours at all.</summary>
    private const string NoHours = """
        {
          "utc_offset_seconds": 7200,
          "current": { "time": "2026-09-22T22:15", "temperature_2m": 16.5, "weather_code": 0, "is_day": 0 },
          "daily": { "time": ["2026-09-22"], "weather_code": [0], "temperature_2m_max": [20.0], "temperature_2m_min": [11.0] }
        }
        """;

    private static DateTime Utc(string text) => DateTime.SpecifyKind(At(text), DateTimeKind.Utc);

    /// <summary>The night the clocks go back in central Europe: 02:00 comes round twice.</summary>
    private const string Autumn = """
        {
          "utc_offset_seconds": 3600, "timezone": "Europe/Zurich",
          "current": { "time": "2026-10-25T00:30", "temperature_2m": 9.0, "weather_code": 3, "is_day": 0 },
          "hourly": {
            "time": ["2026-10-25T00:00","2026-10-25T01:00","2026-10-25T02:00","2026-10-25T02:00","2026-10-25T03:00","2026-10-25T04:00"],
            "temperature_2m": [9.0, 8.6, 8.2, 8.0, 7.8, 7.5],
            "weather_code": [3, 3, 3, 3, 61, 61],
            "is_day": [0, 0, 0, 0, 0, 0],
            "precipitation_probability": [0, 0, 5, 12, 40, 45]
          },
          "daily": { "time": ["2026-10-25"], "weather_code": [61], "temperature_2m_max": [12.0], "temperature_2m_min": [7.0], "precipitation_probability_max": [45] }
        }
        """;

    /// <summary>The night the clocks go forward: there is no 02:00 at all.</summary>
    private const string Spring = """
        {
          "utc_offset_seconds": 3600, "timezone": "Europe/Zurich",
          "current": { "time": "2026-03-29T00:30", "temperature_2m": 4.0, "weather_code": 0, "is_day": 0 },
          "hourly": {
            "time": ["2026-03-29T00:00","2026-03-29T01:00","2026-03-29T03:00","2026-03-29T04:00","2026-03-29T05:00"],
            "temperature_2m": [4.0, 3.6, 3.2, 3.0, 2.8],
            "weather_code": [0, 0, 0, 1, 1],
            "is_day": [0, 0, 0, 0, 0],
            "precipitation_probability": [0, 0, 0, 0, 0]
          },
          "daily": { "time": ["2026-03-29"], "weather_code": [1], "temperature_2m_max": [11.0], "temperature_2m_min": [2.0], "precipitation_probability_max": [0] }
        }
        """;
}
