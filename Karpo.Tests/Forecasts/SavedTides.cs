// One saved marine answer (docs\widgets.md §236), shaped as Open-Meteo's marine service answers with
// timeformat=unixtime: the sea level every quarter of an hour and every hour from yesterday for four
// days, for Brest — a semidiurnal sea, its highs some 2.4 m over mean sea level, the next after the
// weather's frozen clock (SavedWeather.Fixed, 12:20 UTC) at 15:32 UTC — or for a place inland, every
// value null. §635: the samples' own (Karpo.Dock\Samples\Samples.Forecasts.cs), what a ghost of a
// tides widget shows: one copy.

using Karpo.Dock;

namespace Karpo.Tests.Forecasts;

internal static class SavedTides
{
    /// <summary>The place's clock is two hours ahead of UTC, as the weather's.</summary>
    public const int OffsetSeconds = Samples.BrestOffsetSeconds;

    /// <summary>The semidiurnal period, 12 h 25 min.</summary>
    public static readonly TimeSpan Period = Samples.TidePeriod;

    /// <summary>A high of the saved sea: the one after the frozen clock.</summary>
    public static DateTime HighUtc => Samples.TideHighUtc;

    /// <summary>A sea of 2.1 m either side of mean sea level, its highs at <see cref="HighUtc"/> and every period from it, a little higher every other.</summary>
    public static double Wave(DateTime utc) => Samples.TideWave(utc);

    /// <summary>
    /// The answer: <paramref name="height"/> every <paramref name="step"/> minutes (null for none) from
    /// <paramref name="startUtc"/>, <paramref name="count"/> samples, and every hour of it as the hourly series.
    /// </summary>
    public static string Answer(Func<DateTime, double?> height, DateTime startUtc, int count, int step = 15, int offset = OffsetSeconds, bool quarters = true) =>
        Samples.TideAnswer(height, startUtc, count, step, offset, quarters);

    /// <summary>Where the saved series starts: yesterday's midnight UTC; four days of quarters from it.</summary>
    public static DateTime Start => Samples.TideStart;

    /// <summary>The saved sea, or an inland place's nulls, as the store would hold it, fetched an hour ago.</summary>
    public static TideCache Cache(bool inland = false, DateTime? fetchedUtc = null) => Samples.Tides(inland, fetchedUtc);

    /// <summary>A source over <paramref name="cache"/>: nothing fetched.</summary>
    public static TideSource Source(TideCache? cache, string? problem = null) => new() { Read = () => (cache, problem, false) };

    /// <summary>The place the saved sea is for, as a widget carries it.</summary>
    public static Karpo.Core.WeatherOptions Options() => new() { Place = "Brest, France", Latitude = 48.38, Longitude = -4.49 };
}
