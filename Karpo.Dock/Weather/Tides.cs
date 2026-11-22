// The tides (docs\widgets.md §236): Open-Meteo's sea level as far as Karpo reads it, the highs and
// lows found in it, and the words a face and a line say about one answer.
//
// **The source.** Open-Meteo's marine service (marine-api.open-meteo.com), the weather's own
// provider: free, no account, no key, CC BY 4.0. Its `sea_level_height_msl` is the sea's height over
// mean sea level from a global tide model with the surge in it, every quarter of an hour
// (`minutely_15`) and every hour, for any point of the sea; for a point inland, or a harbour the
// model's grid does not reach, every value is null. It is a model: Open-Meteo says plainly it is not
// for navigation and is less accurate near coasts (the lines said so until the person had it go, §408). Heights are over
// mean sea level, not over a chart's datum, so a low is often below zero and the figures are not a
// harbour's tide table's.
//
// **The times** are asked as Unix seconds (timeformat=unixtime): the service writes local times with
// the offset of the moment it answers for the whole answer, so on the day the clocks change every
// local time after the change would be an hour off. Instants are kept and turned into the place's
// clock only to be written (TideClock).
//
// **The highs and lows** are not in the answer; they are its turns, found here (TideTurns.Find): a
// turn counts only once the sea has moved back from it by a share of the series' range, which leaves
// out the model's ripples and an estuary's small second bump; a flat top is its middle; a single
// sample's top is refined between its neighbours by a parabola, so a high is to the minute and not
// to the quarter hour; and nothing at a series' end, or beside a gap, is called a turn, since the sea
// may go on past it.

using System.Globalization;
using System.Text.Json.Serialization;
using Karpo.Core;
using Karpo.Dock.Panels;
using Karpo.Dock.Weather;

namespace Karpo.Dock;

/// <summary>The parts of Open-Meteo's marine answer Karpo reads.</summary>
internal sealed class TideAnswer
{
    [JsonPropertyName("latitude")]
    public double Latitude { get; set; }

    [JsonPropertyName("longitude")]
    public double Longitude { get; set; }

    /// <summary>How far the place's clock was from UTC when the answer was made.</summary>
    [JsonPropertyName("utc_offset_seconds")]
    public int? UtcOffsetSeconds { get; set; }

    /// <summary>The sea level every quarter of an hour.</summary>
    [JsonPropertyName("minutely_15")]
    public TideSeries? Quarters { get; set; }

    /// <summary>The sea level every hour: what is read where the quarters are missing.</summary>
    [JsonPropertyName("hourly")]
    public TideSeries? Hourly { get; set; }
}

/// <summary>A series of the sea's height over mean sea level, in metres, at Unix seconds.</summary>
internal sealed class TideSeries
{
    [JsonPropertyName("time")]
    public long[]? Time { get; set; }

    [JsonPropertyName("sea_level_height_msl")]
    public double?[]? Height { get; set; }
}

/// <summary>Reading Open-Meteo's marine answer.</summary>
internal static class TideAnswers
{
    /// <summary>The day before today, so the curve and the last turn before now are there from the morning.</summary>
    private const int PastDays = 1;

    /// <summary>Today and the two after: the next highs and lows are there through a day without a fetch.</summary>
    private const int Days = 3;

    /// <summary>
    /// The one request for a place: the sea level every quarter of an hour and every hour, in
    /// instants, from yesterday to the day after tomorrow. Two decimals of a degree, as the weather's.
    /// </summary>
    public static string Url(double latitude, double longitude)
    {
        var lat = Math.Round(latitude, 2).ToString(CultureInfo.InvariantCulture);
        var lon = Math.Round(longitude, 2).ToString(CultureInfo.InvariantCulture);
        return $"https://marine-api.open-meteo.com/v1/marine?latitude={lat}&longitude={lon}"
            + "&minutely_15=sea_level_height_msl&hourly=sea_level_height_msl"
            + $"&past_days={PastDays}&forecast_days={Days}&timezone=auto&timeformat=unixtime";
    }

    /// <summary>
    /// The answer in <paramref name="json"/>. Throws <see cref="InvalidDataException"/> when it is not
    /// one: no series at all. A series of nulls is an answer — the place is not on the sea.
    /// </summary>
    public static TideAnswer Parse(string json)
    {
        TideAnswer? answer;
        try
        {
            answer = System.Text.Json.JsonSerializer.Deserialize(json, IslandJson.Default.TideAnswer);
        }
        catch (System.Text.Json.JsonException error)
        {
            throw new InvalidDataException("the tides could not be read", error);
        }
        if (answer is null || (!Has(answer.Quarters) && !Has(answer.Hourly)))
        {
            throw new InvalidDataException("no sea level");
        }
        return answer;
    }

    private static bool Has(TideSeries? series) => series is { Time.Length: > 0, Height: not null };

    /// <summary>
    /// The samples of <paramref name="answer"/>, in time order: the quarters where they have a value,
    /// else the hours. Empty where neither has one: no tide here.
    /// </summary>
    public static IReadOnlyList<TideSample> Samples(TideAnswer answer)
    {
        var quarters = Of(answer.Quarters);
        return quarters.Any(sample => sample.Height is not null) ? quarters : Of(answer.Hourly) is var hours && hours.Any(sample => sample.Height is not null) ? hours : [];
    }

    private static List<TideSample> Of(TideSeries? series)
    {
        var list = new List<TideSample>();
        if (series is not { Time: { } times, Height: { } heights })
        {
            return list;
        }
        for (var index = 0; index < times.Length; index++)
        {
            var height = index < heights.Length ? heights[index] : null;
            list.Add(new TideSample(DateTime.UnixEpoch.AddSeconds(times[index]), height is { } value && double.IsFinite(value) ? value : null));
        }
        list.Sort((a, b) => a.Utc.CompareTo(b.Utc));
        return list;
    }
}

/// <summary>The sea's height over mean sea level in metres at an instant; null where the model has none.</summary>
internal readonly record struct TideSample(DateTime Utc, double? Height);

/// <summary>A high or a low: when (UTC), how high in metres over mean sea level, and which.</summary>
internal readonly record struct TideTurn(DateTime Utc, double Height, bool High);

/// <summary>The highs and lows of a series, and where the sea stands between them.</summary>
internal static class TideTurns
{
    /// <summary>The least the sea must move back from a turn for it to count, in metres: under the model's ripples.</summary>
    public const double LeastMove = 0.03;

    /// <summary>The share of the series' range the sea must move back from a turn for it to count.</summary>
    public const double ShareOfRange = 0.15;

    /// <summary>How much longer than the series' step a gap may be and the samples still be one run.</summary>
    private const double Joined = 1.5;

    /// <summary>
    /// The highs and lows of <paramref name="samples"/>, in order. A turn counts once the sea has
    /// moved back from it by <paramref name="least"/> metres (by default <see cref="LeastMove"/> or
    /// <see cref="ShareOfRange"/> of the series' range, the larger), so ripples and a small second
    /// bump are not turns; a flat top is its middle; a single sample's top is placed between its
    /// neighbours by a parabola. A null or a gap ends a run, and the last top of a run — at the
    /// series' end or beside a gap — is never a turn: the sea may go on past it. Nor is a run's first
    /// sample: the sea may have come from further.
    /// </summary>
    public static IReadOnlyList<TideTurn> Find(IReadOnlyList<TideSample> samples, double? least = null)
    {
        var turns = new List<TideTurn>();
        var low = double.MaxValue;
        var high = double.MinValue;
        foreach (var sample in samples)
        {
            if (sample.Height is { } value)
            {
                low = Math.Min(low, value);
                high = Math.Max(high, value);
            }
        }
        if (low > high)
        {
            return turns;
        }
        var move = least ?? Math.Max(LeastMove, ShareOfRange * (high - low));
        var step = Step(samples);
        foreach (var run in Runs(samples, step))
        {
            Zigzag(samples, run.From, run.To, move, step, turns);
        }
        return turns;
    }

    /// <summary>The series' usual step: the smallest gap between two samples, a quarter of an hour by default.</summary>
    internal static TimeSpan Step(IReadOnlyList<TideSample> samples)
    {
        var step = TimeSpan.MaxValue;
        for (var index = 1; index < samples.Count; index++)
        {
            var gap = samples[index].Utc - samples[index - 1].Utc;
            if (gap > TimeSpan.Zero && gap < step)
            {
                step = gap;
            }
        }
        return step == TimeSpan.MaxValue ? TimeSpan.FromMinutes(15) : step;
    }

    /// <summary>The stretches of samples with a value each and no gap longer than the step allows, as first and last index.</summary>
    private static List<(int From, int To)> Runs(IReadOnlyList<TideSample> samples, TimeSpan step)
    {
        var runs = new List<(int, int)>();
        var start = -1;
        for (var index = 0; index <= samples.Count; index++)
        {
            var ends = index == samples.Count || samples[index].Height is null
                || (start >= 0 && samples[index].Utc - samples[index - 1].Utc > step * Joined);
            if (ends && start >= 0)
            {
                runs.Add((start, index - 1));
                start = -1;
            }
            if (index < samples.Count && samples[index].Height is not null && start < 0)
            {
                start = index;
            }
        }
        return runs;
    }

    /// <summary>
    /// One run's turns, by hysteresis: the top so far is a high once the sea has fallen
    /// <paramref name="move"/> below it, and the bottom so far a low once it has risen as far.
    /// </summary>
    private static void Zigzag(IReadOnlyList<TideSample> samples, int from, int to, double move, TimeSpan step, List<TideTurn> turns)
    {
        double H(int index) => samples[index].Height!.Value;
        // 0 while the way is not known yet, +1 rising towards a high, -1 falling towards a low.
        var way = 0;
        // The highest and lowest so far since the last turn, each as the first and last index of its flat.
        var (top, topEnd) = (from, from);
        var (bottom, bottomEnd) = (from, from);
        for (var index = from + 1; index <= to; index++)
        {
            var value = H(index);
            if (way >= 0)
            {
                if (value > H(top))
                {
                    (top, topEnd) = (index, index);
                }
                else if (value == H(top) && topEnd == index - 1)
                {
                    topEnd = index;
                }
            }
            if (way <= 0)
            {
                if (value < H(bottom))
                {
                    (bottom, bottomEnd) = (index, index);
                }
                else if (value == H(bottom) && bottomEnd == index - 1)
                {
                    bottomEnd = index;
                }
            }
            if (way >= 0 && H(top) - value >= move)
            {
                // A high, where the sea had come up to it from far enough below.
                if (way > 0 || (top > from && H(top) - Lowest(samples, from, top) >= move))
                {
                    turns.Add(Turn(samples, top, topEnd, from, to, step, high: true));
                }
                way = -1;
                (bottom, bottomEnd) = (index, index);
            }
            else if (way <= 0 && value - H(bottom) >= move)
            {
                if (way < 0 || (bottom > from && Highest(samples, from, bottom) - H(bottom) >= move))
                {
                    turns.Add(Turn(samples, bottom, bottomEnd, from, to, step, high: false));
                }
                way = 1;
                (top, topEnd) = (index, index);
            }
        }
    }

    private static double Lowest(IReadOnlyList<TideSample> samples, int from, int to)
    {
        var least = double.MaxValue;
        for (var index = from; index <= to; index++)
        {
            least = Math.Min(least, samples[index].Height!.Value);
        }
        return least;
    }

    private static double Highest(IReadOnlyList<TideSample> samples, int from, int to)
    {
        var most = double.MinValue;
        for (var index = from; index <= to; index++)
        {
            most = Math.Max(most, samples[index].Height!.Value);
        }
        return most;
    }

    /// <summary>
    /// The turn at the flat <paramref name="first"/> to <paramref name="last"/>: its middle where it is
    /// flat, else the top of the parabola through the sample and its two neighbours, held to half a
    /// step either side of it.
    /// </summary>
    private static TideTurn Turn(IReadOnlyList<TideSample> samples, int first, int last, int from, int to, TimeSpan step, bool high)
    {
        var height = samples[first].Height!.Value;
        if (last > first)
        {
            var middle = samples[first].Utc + ((samples[last].Utc - samples[first].Utc) / 2);
            return new TideTurn(middle, height, high);
        }
        if (first <= from || first >= to)
        {
            return new TideTurn(samples[first].Utc, height, high);
        }
        var before = samples[first - 1].Height!.Value;
        var after = samples[first + 1].Height!.Value;
        var bend = before - (2 * height) + after;
        if (bend == 0)
        {
            return new TideTurn(samples[first].Utc, height, high);
        }
        var shift = Math.Clamp(0.5 * (before - after) / bend, -0.5, 0.5);
        var top = height - (0.25 * (before - after) * shift);
        return new TideTurn(samples[first].Utc + (step * shift), top, high);
    }

    /// <summary>
    /// The sea's height at <paramref name="utc"/>, straight between the two samples either side of it;
    /// null outside the series, beside a null or across a gap.
    /// </summary>
    public static double? HeightAt(IReadOnlyList<TideSample> samples, DateTime utc)
    {
        if (samples.Count == 0 || utc < samples[0].Utc || utc > samples[^1].Utc)
        {
            return null;
        }
        var step = Step(samples);
        for (var index = 0; index < samples.Count; index++)
        {
            var sample = samples[index];
            if (sample.Utc == utc)
            {
                return sample.Height;
            }
            if (sample.Utc > utc)
            {
                var before = samples[index - 1];
                if (before.Height is not { } from || sample.Height is not { } to || sample.Utc - before.Utc > step * Joined)
                {
                    return null;
                }
                var share = (utc - before.Utc) / (sample.Utc - before.Utc);
                return from + ((to - from) * share);
            }
        }
        return null;
    }

    /// <summary>The first turn after <paramref name="utc"/>; null where the series has none.</summary>
    public static TideTurn? Next(IReadOnlyList<TideTurn> turns, DateTime utc)
    {
        foreach (var turn in turns)
        {
            if (turn.Utc > utc)
            {
                return turn;
            }
        }
        return null;
    }

    /// <summary>
    /// Whether the sea is rising at <paramref name="utc"/>: towards a high where one is next, else as
    /// it moves over the next quarter of an hour; null where neither can be told.
    /// </summary>
    public static bool? Rising(IReadOnlyList<TideSample> samples, IReadOnlyList<TideTurn> turns, DateTime utc)
    {
        if (Next(turns, utc) is { } next)
        {
            return next.High;
        }
        if (HeightAt(samples, utc) is { } now && HeightAt(samples, utc + TimeSpan.FromMinutes(15)) is { } later && later != now)
        {
            return later > now;
        }
        return null;
    }
}

/// <summary>An instant as the place's clock says it.</summary>
internal static class TideClock
{
    /// <summary>
    /// <paramref name="utc"/> on the place's clock: through <paramref name="zone"/> (this PC's) where
    /// the place kept this PC's offset when the answer was made — so the clocks changing on a day of
    /// the series is followed — else by the answer's own offset.
    /// </summary>
    public static DateTime Wall(DateTime utc, int? offsetSeconds, DateTime fetchedUtc, TimeZoneInfo zone)
    {
        var utcKind = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        if (offsetSeconds is not { } offset || zone.GetUtcOffset(DateTime.SpecifyKind(fetchedUtc, DateTimeKind.Utc)) == TimeSpan.FromSeconds(offset))
        {
            return DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(utcKind, zone), DateTimeKind.Unspecified);
        }
        return DateTime.SpecifyKind(utcKind.AddSeconds(offset), DateTimeKind.Unspecified);
    }
}

/// <summary>A tide's words: heights in metres or feet, a turn's time and how soon.</summary>
internal static class TideText
{
    /// <summary>Feet in a metre.</summary>
    public const double FeetPerMetre = 3.28084;

    /// <summary>Whether heights are in feet: where temperatures are in Fahrenheit (Localization's scale, else the region's), as the weather's wind is in miles.</summary>
    public static bool Imperial => WeatherUnits.Fahrenheit;

    /// <summary>
    /// "2.1 m" or "6.9 ft", one decimal with <paramref name="separator"/> (the region's where none is
    /// given); a rounding to zero from below is "0.0", never "-0.0".
    /// </summary>
    public static string Height(double metres, bool imperial, string? separator = null)
    {
        var value = Math.Round(imperial ? metres * FeetPerMetre : metres, 1, MidpointRounding.AwayFromZero);
        if (value == 0)
        {
            value = 0;
        }
        var figure = value.ToString("0.0", CultureInfo.InvariantCulture).Replace(".", separator ?? Nls.DecimalSeparator(), StringComparison.Ordinal);
        return $"{figure} {(imperial ? "ft" : "m")}";
    }

    /// <summary>The widest a height reads: two figures and a decimal under the sea's level, in the unit shown.</summary>
    public static string WidestHeight(bool imperial) => imperial ? "-88.8 ft" : "-88.8 m";

    /// <summary>"High" or "Low".</summary>
    public static string Word(bool high) => high ? S.DockTidesHigh : S.DockTidesLow;

    /// <summary>"High 14:32".</summary>
    public static string At(bool high, string time) => high ? S.DockTidesHighAt(time) : S.DockTidesLowAt(time);

    /// <summary>"High in 3 h 10 min": a part minute rounded up, as a meeting's "in 4 min".</summary>
    public static string In(bool high, TimeSpan until)
    {
        var length = MeetingText.Length(until < TimeSpan.Zero ? TimeSpan.Zero : until, up: true);
        return high ? S.DockTidesHighIn(length) : S.DockTidesLowIn(length);
    }

    /// <summary>"Rising" or "Falling".</summary>
    public static string Way(bool rising) => rising ? S.DockTidesRising : S.DockTidesFalling;

    /// <summary>Segoe Fluent Icons' Up and Down: the sea rising or falling.</summary>
    public static string Arrow(bool rising) => rising ? "" : "";
}

/// <summary>Where a tides widget is: nothing yet, a failure with nothing to show, a place off the sea, or a reading.</summary>
internal enum TideState
{
    Waiting,
    Failed,
    Inland,
    Ready,
}

/// <summary>A turn as a reading shows it: the turn, its time on the place's clock and that written out.</summary>
internal readonly record struct TideMark(TideTurn Turn, DateTime Wall, string Time);

/// <summary>What a face and a line say about one answer, worded once (<see cref="From"/>).</summary>
internal sealed record TideReading
{
    public TideState State { get; init; }

    /// <summary>The sea's height now, in metres over mean sea level; null where the series does not reach now.</summary>
    public double? Height { get; init; }

    /// <summary>Whether the sea is rising now; null where it cannot be told.</summary>
    public bool? Rising { get; init; }

    /// <summary>The next high or low; null where the series has none ahead.</summary>
    public TideMark? Next { get; init; }

    /// <summary>The next highs and lows, the next first, at most <see cref="MarksAhead"/>.</summary>
    public IReadOnlyList<TideMark> Ahead { get; init; } = [];

    /// <summary>The samples of the curve's window, <see cref="CurveFrom"/> to <see cref="CurveTo"/>.</summary>
    public IReadOnlyList<TideSample> Curve { get; init; } = [];

    /// <summary>The highs and lows inside the curve's window, past ones too.</summary>
    public IReadOnlyList<TideMark> CurveMarks { get; init; } = [];

    /// <summary>Where the curve's window starts and ends (UTC).</summary>
    public DateTime CurveFrom { get; init; }

    public DateTime CurveTo { get; init; }

    /// <summary>Now (UTC), where the curve's mark stands.</summary>
    public DateTime Now { get; init; }

    /// <summary>Heights in feet.</summary>
    public bool Imperial { get; init; }

    /// <summary>"Updated 20 minutes ago"; empty while nothing has been fetched.</summary>
    public string Updated { get; init; } = "";

    /// <summary>A fetch failed and left it standing, or three fetches have been missed.</summary>
    public bool Stale { get; init; }

    /// <summary>Why a newer answer is not there; null while nothing failed.</summary>
    public string? Problem { get; init; }

    /// <summary>How many highs and lows ahead a reading lists.</summary>
    public const int MarksAhead = 4;

    /// <summary>How far back from now the curve starts: the last few hours, so now is not its edge.</summary>
    public static readonly TimeSpan CurveBefore = TimeSpan.FromHours(3);

    /// <summary>How far past now the curve runs: with <see cref="CurveBefore"/>, a day.</summary>
    public static readonly TimeSpan CurveAfter = TimeSpan.FromHours(21);

    /// <summary>"2.1 m", the height now; empty where there is none.</summary>
    public string HeightText(string? separator = null) => Height is { } metres ? TideText.Height(metres, Imperial, separator) : "";

    /// <summary>
    /// The reading for <paramref name="cache"/> (the last answer for the widget's place, if any) at
    /// <paramref name="utcNow"/>, its times on <paramref name="zone"/>'s clock where the place keeps
    /// it (<see cref="TideClock"/>); <paramref name="problem"/> is why a newer one is not there.
    /// </summary>
    public static TideReading From(TideCache? cache, DateTime utcNow, string? problem, bool? imperial = null, TimeZoneInfo? zone = null, TimeSpan? every = null)
    {
        var feet = imperial ?? TideText.Imperial;
        var refresh = (int)(every ?? TideStore.Interval).TotalMinutes;
        var stale = WeatherAge.IsStale(cache?.FetchedUtc, utcNow, refresh, problem);
        var updated = cache is null ? "" : WeatherAge.Ago(cache.FetchedUtc, utcNow);
        if (cache?.Answer is not { } answer)
        {
            return new TideReading { State = problem is null ? TideState.Waiting : TideState.Failed, Problem = problem, Imperial = feet, Updated = updated, Stale = stale, Now = utcNow };
        }
        var samples = TideAnswers.Samples(answer);
        if (samples.Count == 0)
        {
            return new TideReading { State = TideState.Inland, Problem = problem, Imperial = feet, Updated = updated, Stale = stale, Now = utcNow };
        }
        zone ??= TimeZoneInfo.Local;
        TideMark Mark(TideTurn turn)
        {
            var wall = TideClock.Wall(turn.Utc, answer.UtcOffsetSeconds, cache.FetchedUtc, zone);
            return new TideMark(turn, wall, Nls.Time(wall));
        }
        var turns = TideTurns.Find(samples);
        var ahead = turns.Where(turn => turn.Utc > utcNow).Take(MarksAhead).Select(Mark).ToList();
        var from = utcNow - CurveBefore;
        var to = utcNow + CurveAfter;
        // The curve's samples, and one either side, so its line reaches the window's edges.
        var first = Math.Max(0, LastAtOrBefore(samples, from));
        var last = Math.Min(samples.Count - 1, FirstAtOrAfter(samples, to));
        var curve = first <= last ? samples.Skip(first).Take(last - first + 1).ToList() : [];
        return new TideReading
        {
            State = TideState.Ready,
            Height = TideTurns.HeightAt(samples, utcNow),
            Rising = TideTurns.Rising(samples, turns, utcNow),
            Next = ahead.Count > 0 ? ahead[0] : null,
            Ahead = ahead,
            Curve = curve,
            CurveMarks = turns.Where(turn => turn.Utc >= from && turn.Utc <= to).Select(Mark).ToList(),
            CurveFrom = from,
            CurveTo = to,
            Now = utcNow,
            Imperial = feet,
            Updated = updated,
            Stale = stale,
            Problem = problem,
        };
    }

    private static int LastAtOrBefore(IReadOnlyList<TideSample> samples, DateTime utc)
    {
        var found = -1;
        for (var index = 0; index < samples.Count && samples[index].Utc <= utc; index++)
        {
            found = index;
        }
        return found;
    }

    private static int FirstAtOrAfter(IReadOnlyList<TideSample> samples, DateTime utc)
    {
        for (var index = 0; index < samples.Count; index++)
        {
            if (samples[index].Utc >= utc)
            {
                return index;
            }
        }
        return samples.Count - 1;
    }

    /// <summary>"High 14:32"; empty where no turn is ahead.</summary>
    public string NextAt => Next is { } next ? TideText.At(next.Turn.High, next.Time) : "";

    /// <summary>"High 14:32 · 2.1 m"; empty where no turn is ahead.</summary>
    public string NextAtHeight(string? separator = null) =>
        Next is { } next ? TideText.At(next.Turn.High, next.Time) + FaceDrawings.Dot + TideText.Height(next.Turn.Height, Imperial, separator) : "";

    /// <summary>"High in 3 h 10 min"; empty where no turn is ahead.</summary>
    public string NextIn => Next is { } next ? TideText.In(next.Turn.High, next.Turn.Utc - Now) : "";

    /// <summary>What is said where there is no reading: the problem, the place off the sea, or that it is coming.</summary>
    public string Why => State switch
    {
        TideState.Inland => S.DockTidesInland,
        _ => Problem ?? S.DockTidesLoading,
    };

    /// <summary>
    /// The face's tooltip: the place, the sea now and which way, the next highs and lows, the age,
    /// a line each; why there is no reading where there is none.
    /// </summary>
    public string Tooltip(string place)
    {
        var lines = new List<string>();
        if (place.Length > 0)
        {
            lines.Add(place);
        }
        if (State == TideState.Ready)
        {
            if (Height is not null)
            {
                lines.Add(Rising is { } rising ? HeightText() + FaceDrawings.Dot + TideText.Way(rising) : HeightText());
            }
            foreach (var mark in Ahead)
            {
                lines.Add(TideText.At(mark.Turn.High, mark.Time) + FaceDrawings.Dot + TideText.Height(mark.Turn.Height, Imperial));
            }
            if (Ahead.Count == 0)
            {
                lines.Add(S.DockTidesNoneAhead);
            }
        }
        else
        {
            lines.Add(Why);
        }
        if (Updated.Length > 0)
        {
            lines.Add(Updated);
        }
        return string.Join("\n", lines);
    }
}
