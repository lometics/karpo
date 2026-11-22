// One saved air-quality answer (docs\widgets.md §210), shaped as Open-Meteo's air-quality service
// answers: both indices, the pollutants and the pollen now, and two days of both indices hour by hour
// from the place's local midnight. Zürich's (pollen) or New York's (none), at the index values a
// test asks for, against the weather's frozen clock (SavedWeather.Fixed). §635: the samples' own
// (Karpo.Dock\Samples\Samples.Forecasts.cs), what a ghost of an air widget shows: one copy.

using Karpo.Dock;

namespace Karpo.Tests.Forecasts;

internal static class SavedAir
{
    /// <summary>The answer for Zürich (pollen, the European index) or New York (no pollen, the US index), with these indices now.</summary>
    public static string Answer(double european = 23, double us = 54, bool europe = true, string pollen = "0.0") =>
        Samples.AirAnswer(european, us, europe, pollen);

    /// <summary>The answer as the store would hold it, fetched a quarter of an hour ago.</summary>
    public static AirCache Cache(double european = 23, double us = 54, bool europe = true, DateTime? fetchedUtc = null, string pollen = "0.0") =>
        Samples.Air(european, us, europe, fetchedUtc, pollen);

    /// <summary>A source over <paramref name="cache"/>: nothing fetched.</summary>
    public static AirSource Source(AirCache? cache, string? problem = null) => new() { Read = () => (cache, problem, false) };
}
