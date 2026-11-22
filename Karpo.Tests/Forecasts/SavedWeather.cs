// One saved forecast, for any test that needs the weather to have arrived without a network.
//
// It is shaped as Open-Meteo's real answer is: the reading at the place's own local time now, two
// days of hours from its local midnight, and a week of days from today — so everything the panel
// and the folder's cards read is in it (the current conditions, the hourly curve with its chances
// of rain, the week's highs and lows, and the day's sunrise and sunset).
//
// It stands apart from any one test class because two of them need it now: the panel's own picture
// sheets, and the folder the panel became (docs\widgets.md §9). A second copy would drift, and the
// two sheets would then be of two different days, which is exactly what makes them incomparable.
// §635: and it is the samples' own (Karpo.Dock\Samples\Samples.Forecasts.cs), what a ghost of a
// weather widget shows: one copy for the tests and the ghosts.

using Karpo.Core;
using Karpo.Dock;
using Karpo.Dock.Weather;

namespace Karpo.Tests.Forecasts;

internal static class SavedWeather
{
    /// <summary>The place's clock is two hours ahead of UTC, as the answer says.</summary>
    public const int OffsetSeconds = Samples.ZurichOffsetSeconds;

    /// <summary>
    /// The clock a test freezes, so a picture sheet is the same every run and the mark for now
    /// lands in the same place. Twenty past two in the afternoon: daylight, and a fifth of the way
    /// between two hours, so the mark is plainly not snapped to a point.
    /// </summary>
    public static readonly DateTime Fixed = new(2026, 9, 23, 14, 20, 0, DateTimeKind.Local);

    /// <summary>Runs <paramref name="body"/> with the clock frozen at <see cref="Fixed"/>.</summary>
    public static void AtAFixedTime(Action body)
    {
        var was = Clock.Now;
        Clock.Now = () => Fixed;
        try
        {
            body();
        }
        finally
        {
            Clock.Now = was;
        }
    }

    /// <summary>The place the forecast is for, as a widget carries it.</summary>
    public static WeatherOptions Options() => new()
    {
        Place = "Zürich, Switzerland",
        Latitude = 47.37,
        Longitude = 8.54,
    };

    /// <summary>The forecast as the store would hold it, fetched a quarter of an hour ago.</summary>
    public static WeatherCache Cache(DateTime? fetchedUtc = null) => Samples.Weather(fetchedUtc);

    /// <summary>
    /// A source over that forecast: nothing is fetched, Refresh is counted and not acted on. One of
    /// these handed to every card of a folder is what the store does for them in earnest — one
    /// place, one Entry, one cache — said out loud where a test can see it.
    /// </summary>
    public static WeatherSource Source(WeatherCache? cache = null, string? problem = null) => Samples.WeatherSource(cache, problem);

    /// <summary>The answer itself: the reading now, forty-eight hours, and a week of days (§635: the samples' own, what a ghost shows).</summary>
    public static string Answer() => Samples.WeatherAnswer();
}
