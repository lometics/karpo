// The air quality (docs\widgets.md §210): Open-Meteo's air-quality answer as far as Karpo reads it,
// the two indices' bands and colours, and the words a face and a line say about one answer.
//
// **The source.** Open-Meteo's air-quality service (air-quality-api.open-meteo.com), the weather's
// own provider: free, no account, no key, CC BY 4.0 as the forecast is. It answers for any place
// with the European AQI and the US AQI side by side, the pollutants behind them (PM2.5, PM10, ozone,
// nitrogen dioxide, sulphur dioxide, carbon monoxide) in µg/m³, the next days hour by hour, and in
// Europe the pollen of six plants (CAMS Europe; null everywhere else).
//
// **Which index.** Chosen per widget (AirIndexes); where nobody chose, the place picks: the European
// index inside Copernicus' European model's area, roughly 35-72° N and 25° W-45° E, the US index
// everywhere else — what MSN Weather, Windows' own, shows outside Europe. The place is the answer's,
// so a widget for this PC's location picks by where the PC turns out to be.
//
// **The bands** are each index's own, with its agency's colours:
//   European (EEA): 0-20 Good, -40 Fair, -60 Moderate, -80 Poor, -100 Very poor, past 100 Extremely poor
//   US (EPA):       0-50 Good, -100 Moderate, -150 Unhealthy for sensitive groups, -200 Unhealthy,
//                   -300 Very unhealthy, past 300 Hazardous
// A value is rounded to a whole number first, as both agencies report it: 50.4 is Good, 50.5 Moderate.

using System.Globalization;
using System.Text.Json.Serialization;
using Karpo.Core;
using Karpo.Dock.Panels;
using Karpo.Dock.Weather;

namespace Karpo.Dock;

/// <summary>The parts of Open-Meteo's air-quality answer Karpo reads.</summary>
internal sealed class AirAnswer
{
    [JsonPropertyName("latitude")]
    public double Latitude { get; set; }

    [JsonPropertyName("longitude")]
    public double Longitude { get; set; }

    /// <summary>How far the place's own clock is from UTC: the hours are in its local time.</summary>
    [JsonPropertyName("utc_offset_seconds")]
    public int? UtcOffsetSeconds { get; set; }

    [JsonPropertyName("current")]
    public AirNow? Current { get; set; }

    [JsonPropertyName("hourly")]
    public AirHours? Hourly { get; set; }
}

/// <summary>The air now: both indices, the pollutants in µg/m³ and the pollen in grains/m³ (null outside Europe).</summary>
internal sealed class AirNow
{
    [JsonPropertyName("time")]
    public string? Time { get; set; }

    [JsonPropertyName("european_aqi")]
    public double? EuropeanAqi { get; set; }

    [JsonPropertyName("us_aqi")]
    public double? UsAqi { get; set; }

    [JsonPropertyName("pm2_5")]
    public double? Pm25 { get; set; }

    [JsonPropertyName("pm10")]
    public double? Pm10 { get; set; }

    [JsonPropertyName("ozone")]
    public double? Ozone { get; set; }

    [JsonPropertyName("nitrogen_dioxide")]
    public double? NitrogenDioxide { get; set; }

    [JsonPropertyName("sulphur_dioxide")]
    public double? SulphurDioxide { get; set; }

    [JsonPropertyName("carbon_monoxide")]
    public double? CarbonMonoxide { get; set; }

    [JsonPropertyName("alder_pollen")]
    public double? AlderPollen { get; set; }

    [JsonPropertyName("birch_pollen")]
    public double? BirchPollen { get; set; }

    [JsonPropertyName("grass_pollen")]
    public double? GrassPollen { get; set; }

    [JsonPropertyName("mugwort_pollen")]
    public double? MugwortPollen { get; set; }

    [JsonPropertyName("olive_pollen")]
    public double? OlivePollen { get; set; }

    [JsonPropertyName("ragweed_pollen")]
    public double? RagweedPollen { get; set; }
}

/// <summary>Both indices hour by hour, in the place's local time ("2026-10-18T14:00").</summary>
internal sealed class AirHours
{
    [JsonPropertyName("time")]
    public string?[]? Time { get; set; }

    [JsonPropertyName("european_aqi")]
    public double?[]? EuropeanAqi { get; set; }

    [JsonPropertyName("us_aqi")]
    public double?[]? UsAqi { get; set; }
}

/// <summary>Reading Open-Meteo's answer.</summary>
internal static class AirAnswers
{
    private const string Pollutants = "pm2_5,pm10,ozone,nitrogen_dioxide,sulphur_dioxide,carbon_monoxide";
    private const string Pollen = "alder_pollen,birch_pollen,grass_pollen,mugwort_pollen,olive_pollen,ragweed_pollen";

    /// <summary>How many days of hours are asked for: today's rest and tomorrow's.</summary>
    private const int Days = 2;

    /// <summary>
    /// The one request for a place: both indices, the pollutants and the pollen now, and both indices
    /// hour by hour, in the place's own time. Two decimals of a degree, about a kilometre, as the
    /// weather's: all the air needs, and no more of where the person is than that leaves the machine.
    /// </summary>
    public static string Url(double latitude, double longitude)
    {
        var lat = Math.Round(latitude, 2).ToString(CultureInfo.InvariantCulture);
        var lon = Math.Round(longitude, 2).ToString(CultureInfo.InvariantCulture);
        return $"https://air-quality-api.open-meteo.com/v1/air-quality?latitude={lat}&longitude={lon}"
            + $"&current=european_aqi,us_aqi,{Pollutants},{Pollen}"
            + "&hourly=european_aqi,us_aqi"
            + $"&forecast_days={Days}&timezone=auto";
    }

    /// <summary>
    /// The answer in <paramref name="json"/>. Throws <see cref="InvalidDataException"/> when it is not
    /// one: an answer with neither index now would leave the widget with nothing to show.
    /// </summary>
    public static AirAnswer Parse(string json)
    {
        AirAnswer? answer;
        try
        {
            answer = System.Text.Json.JsonSerializer.Deserialize(json, IslandJson.Default.AirAnswer);
        }
        catch (System.Text.Json.JsonException error)
        {
            throw new InvalidDataException("the air quality could not be read", error);
        }
        if (answer?.Current is not { } now || (now.EuropeanAqi is null && now.UsAqi is null))
        {
            throw new InvalidDataException("no air quality index");
        }
        return answer;
    }
}

/// <summary>Which index a reading is on.</summary>
internal enum AirScale
{
    /// <summary>The European Environment Agency's, 0 to 100 and past it.</summary>
    European,

    /// <summary>The US EPA's, 0 to 500.</summary>
    Us,
}

/// <summary>The indices' bands, their colours and their words.</summary>
internal static class AirBands
{
    /// <summary>How many levels each index has.</summary>
    public const int Levels = 6;

    /// <summary>The index for <paramref name="chosen"/> (<see cref="AirIndexes"/>) at a place: the chosen one, else the region's.</summary>
    public static AirScale ScaleOf(string? chosen, double? latitude, double? longitude) => AirIndexes.Chosen(chosen) switch
    {
        AirIndexes.European => AirScale.European,
        AirIndexes.Us => AirScale.Us,
        _ => InEurope(latitude, longitude) ? AirScale.European : AirScale.Us,
    };

    /// <summary>Whether a place is in Europe as Copernicus' European air-quality model covers it: 35-72° N, 25° W-45° E.</summary>
    public static bool InEurope(double? latitude, double? longitude) =>
        latitude is >= 35 and <= 72 && longitude is >= -25 and <= 45;

    /// <summary>The index's upper bounds of its first five levels; past the last is the sixth.</summary>
    private static ReadOnlySpan<int> Bounds(AirScale scale) => scale == AirScale.European ? [20, 40, 60, 80, 100] : [50, 100, 150, 200, 300];

    /// <summary>The level, 0 (good) to 5, of <paramref name="value"/> on <paramref name="scale"/>, rounded to a whole number first.</summary>
    public static int Level(AirScale scale, double value)
    {
        var whole = Math.Round(value, MidpointRounding.AwayFromZero);
        var bounds = Bounds(scale);
        for (var level = 0; level < bounds.Length; level++)
        {
            if (whole <= bounds[level])
            {
                return level;
            }
        }
        return Levels - 1;
    }

    /// <summary>The least and most of the scale a gauge draws: 0 to 120 for the European index, 0 to 500 for the US one.</summary>
    public static (double Least, double Most) Span(AirScale scale) => scale == AirScale.European ? (0, 120) : (0, 500);

    /// <summary>Where each level starts along the gauge, from 0 to its <see cref="Span"/>'s most.</summary>
    public static double Start(AirScale scale, int level) =>
        level <= 0 ? 0 : Bounds(scale)[Math.Min(level, Levels - 1) - 1];

    /// <summary>The level's colour, as its agency publishes it, 0xRRGGBB.</summary>
    public static uint Colour(AirScale scale, int level) => (scale, Math.Clamp(level, 0, Levels - 1)) switch
    {
        (AirScale.European, 0) => 0x50F0E6,
        (AirScale.European, 1) => 0x50CCAA,
        (AirScale.European, 2) => 0xF0E641,
        (AirScale.European, 3) => 0xFF5050,
        (AirScale.European, 4) => 0x960032,
        (AirScale.European, _) => 0x7D2181,
        (_, 0) => 0x00E400,
        (_, 1) => 0xFFFF00,
        (_, 2) => 0xFF7E00,
        (_, 3) => 0xFF0000,
        (_, 4) => 0x8F3F97,
        _ => 0x7E0023,
    };

    /// <summary><see cref="Colour"/> as the drawing's ink, at <paramref name="alpha"/>.</summary>
    public static TerraFX.Interop.DirectX.DXGI_RGBA Ink(AirScale scale, int level, float alpha = 1)
    {
        var rgb = Colour(scale, level);
        return new(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, alpha);
    }

    /// <summary>The level in words, as its agency names it: "Good", "Unhealthy for sensitive groups".</summary>
    public static string Word(AirScale scale, int level) => (scale, Math.Clamp(level, 0, Levels - 1)) switch
    {
        (AirScale.European, 0) => S.DockAirEuropeanGood,
        (AirScale.European, 1) => S.DockAirEuropeanFair,
        (AirScale.European, 2) => S.DockAirEuropeanModerate,
        (AirScale.European, 3) => S.DockAirEuropeanPoor,
        (AirScale.European, 4) => S.DockAirEuropeanVeryPoor,
        (AirScale.European, _) => S.DockAirEuropeanExtremelyPoor,
        (_, 0) => S.DockAirUsGood,
        (_, 1) => S.DockAirUsModerate,
        (_, 2) => S.DockAirUsSensitive,
        (_, 3) => S.DockAirUsUnhealthy,
        (_, 4) => S.DockAirUsVeryUnhealthy,
        _ => S.DockAirUsHazardous,
    };

    /// <summary>The level in the bar's words: <see cref="Word"/>, but the US index's long third level, "Sensitive groups".</summary>
    public static string Short(AirScale scale, int level) =>
        scale == AirScale.Us && level == 2 ? S.DockAirUsSensitiveShort : Word(scale, level);

    /// <summary>The index's name: "European AQI", "US AQI".</summary>
    public static string Name(AirScale scale) =>
        scale == AirScale.European ? S.CoreFaceAirQualityIndexEuropean : S.CoreFaceAirQualityIndexUs;

    /// <summary>Every short level word <paramref name="scales"/> say, so a face keeps the room of the widest.</summary>
    public static IEnumerable<string> ShortWords(IEnumerable<AirScale> scales) =>
        scales.SelectMany(scale => Enumerable.Range(0, Levels).Select(level => Short(scale, level)));

    /// <summary>The scales a widget set to <paramref name="chosen"/> can show: the chosen one, or both where the place picks.</summary>
    public static IReadOnlyList<AirScale> ScalesFor(string? chosen) => AirIndexes.Chosen(chosen) switch
    {
        AirIndexes.European => [AirScale.European],
        AirIndexes.Us => [AirScale.Us],
        _ => [AirScale.European, AirScale.Us],
    };

    /// <summary>
    /// The scales a face set to <paramref name="chosen"/> for <paramref name="place"/> can show: the
    /// chosen one; where the place picks, its region's — both for this PC's location, which is not
    /// known until it is read, and within half a degree of the European area's edge, where the
    /// answer's grid point may fall on its other side.
    /// </summary>
    public static IReadOnlyList<AirScale> ScalesAt(string? chosen, WeatherOptions? place)
    {
        if (AirIndexes.Chosen(chosen) is not null || place is not { UseDeviceLocation: false, Latitude: { } latitude, Longitude: { } longitude })
        {
            return ScalesFor(chosen);
        }
        const double Margin = 0.5;
        var europe = InEurope(latitude, longitude);
        foreach (var (up, east) in (ReadOnlySpan<(double, double)>)[(-Margin, -Margin), (-Margin, Margin), (Margin, -Margin), (Margin, Margin)])
        {
            if (InEurope(latitude + up, longitude + east) != europe)
            {
                return ScalesFor(chosen);
            }
        }
        return [europe ? AirScale.European : AirScale.Us];
    }

    /// <summary>
    /// The most <paramref name="level"/> of <paramref name="scale"/> reads: its band's upper bound, and
    /// for the last band, which has none, three figures — the European index's past 100 and the US
    /// index's past 300 do not leave them in practice.
    /// </summary>
    public static int Most(AirScale scale, int level) => level < Levels - 1 ? Bounds(scale)[Math.Max(0, level)] : 999;

    /// <summary>The value of <paramref name="scale"/> in <paramref name="now"/>.</summary>
    public static double? ValueOf(AirNow now, AirScale scale) => scale == AirScale.European ? now.EuropeanAqi : now.UsAqi;
}

/// <summary>The face's words, by how much room they have.</summary>
internal static class AirText
{
    /// <summary>
    /// What a reading of <paramref name="value"/> at <paramref name="level"/> may say for a face set to
    /// <paramref name="width"/> (<see cref="AirQualityFace"/>), fullest first: "AQI 32 · Good",
    /// "32 · Good", "AQI 32", "32"; never more than the setting says.
    /// </summary>
    public static IReadOnlyList<string> Forms(string value, string level, string width) => width switch
    {
        AirQualityFace.Wide => [S.DockAirAqi(value) + FaceDrawings.Dot + level, value + FaceDrawings.Dot + level, S.DockAirAqi(value), value],
        AirQualityFace.Compact => [value],
        _ => [value + FaceDrawings.Dot + level, value],
    };

    /// <summary>The fullest of <paramref name="forms"/> <paramref name="measure"/> says fits <paramref name="room"/> px; the last where none does.</summary>
    public static string Fit(IReadOnlyList<string> forms, float room, Func<string, float> measure)
    {
        foreach (var form in forms)
        {
            if (measure(form) <= room + 0.5f)
            {
                return form;
            }
        }
        return forms[^1];
    }

    /// <summary>The widest a value reads: three figures, past which no index goes in practice.</summary>
    public const string WidestValue = "888";

    /// <summary>
    /// The widest a value at <paramref name="level"/> of <paramref name="scale"/> reads: as many figures
    /// as its band's most (<see cref="AirBands.Most"/>), each an eight — "88" for a European "Good",
    /// "888" past 100 — so the face keeps the room its level can need and no more.
    /// </summary>
    public static string WidestAt(AirScale scale, int level) =>
        new('8', AirBands.Most(scale, level).ToString(System.Globalization.CultureInfo.InvariantCulture).Length);
}

/// <summary>Where an air-quality widget is: nothing yet, a failure with nothing to show, or a reading.</summary>
internal enum AirState
{
    Waiting,
    Failed,
    Ready,
}

/// <summary>One pollutant now: its formula, its value and its unit.</summary>
internal readonly record struct AirPollutant(string Name, string Value, string Unit);

/// <summary>One hour of the place's day: its time, its value and its level.</summary>
internal readonly record struct AirHour(DateTime At, string Time, int Value, int Level);

/// <summary>What a face and a line say about one answer, worded once (<see cref="From"/>).</summary>
internal sealed record AirReading
{
    public AirState State { get; init; }

    public AirScale Scale { get; init; }

    /// <summary>The index now, a whole number; 0 while there is none.</summary>
    public int Value { get; init; }

    /// <summary>Its level, 0 (good) to 5.</summary>
    public int Level { get; init; }

    /// <summary>"Good".</summary>
    public string Word { get; init; } = "";

    /// <summary>The bar's word for it: "Sensitive groups" for the US index's long third level.</summary>
    public string Short { get; init; } = "";

    /// <summary>"European AQI".</summary>
    public string Name { get; init; } = "";

    /// <summary>PM2.5, PM10, O₃ and NO₂ where the answer has them.</summary>
    public IReadOnlyList<AirPollutant> Pollutants { get; init; } = [];

    /// <summary>The pollen in the air now, the most first ("Grass", "12"); null where the place has no pollen forecast.</summary>
    public IReadOnlyList<(string Name, string Value)>? Pollen { get; init; }

    /// <summary>
    /// §590: the six plants the pollen forecast has, always all six and always in one order — grass, birch, alder,
    /// ragweed, mugwort, olive — each with its grains per m³ now, 0 where none; null where the place has no pollen
    /// forecast at all. The pollen line's, which shows the same six whatever the season.
    /// </summary>
    public IReadOnlyList<(string Name, string Value)>? Plants { get; init; }

    /// <summary>
    /// §501: the index for the place's day, hour by hour from its midnight (<see cref="HoursOf"/>), on the weather's own
    /// axis: an hour stands where it stands all day, and now moves along it from left to right (<see cref="Now"/>).
    /// </summary>
    public IReadOnlyList<AirHour> Hours { get; init; } = [];

    /// <summary>§501: the place's hour now, 0 to 23, where its day's bar stands; <see cref="NoNow"/> where the hours are not today's.</summary>
    public int Now { get; init; } = NoNow;

    /// <summary><see cref="Now"/> where nothing is now.</summary>
    public const int NoNow = -1;

    /// <summary>"Updated 20 minutes ago"; empty while nothing has been fetched.</summary>
    public string Updated { get; init; } = "";

    /// <summary>Past due, or a fetch failed and left it standing (WeatherAge.IsStale).</summary>
    public bool Stale { get; init; }

    /// <summary>Why a newer answer is not there; null while nothing failed.</summary>
    public string? Problem { get; init; }

    /// <summary>How many hours a day of the reading's lists: midnight to midnight, the weather's axis (DayGraph.AxisHours).</summary>
    public const int DayHours = 24;

    /// <summary>The unit Open-Meteo gives every pollutant in.</summary>
    public const string Micrograms = "µg/m³";

    /// <summary>
    /// The reading for <paramref name="cache"/> (the last answer for the widget's place, if any), on the
    /// index <paramref name="chosen"/> says, at <paramref name="utcNow"/>; <paramref name="problem"/> is
    /// why a newer one is not there.
    /// </summary>
    public static AirReading From(AirCache? cache, string? chosen, DateTime utcNow, string? problem, OnlineUpdates? updates = null)
    {
        var stale = WeatherAge.IsStale(cache?.FetchedUtc, utcNow, WeatherReading.Refresh(updates), problem);
        var updated = cache is null ? "" : WeatherAge.Ago(cache.FetchedUtc, utcNow);
        var answer = cache?.Answer;
        var scale = AirBands.ScaleOf(chosen, answer?.Latitude ?? cache?.Latitude, answer?.Longitude ?? cache?.Longitude);
        if (answer?.Current is not { } now || AirBands.ValueOf(now, scale) is not { } value)
        {
            return new AirReading
            {
                State = problem is null ? AirState.Waiting : AirState.Failed,
                Scale = scale,
                Name = AirBands.Name(scale),
                Problem = problem,
                Stale = stale,
                Updated = updated,
            };
        }
        var level = AirBands.Level(scale, value);
        var (hours, hour) = HoursOf(answer, scale, utcNow);
        return new AirReading
        {
            State = AirState.Ready,
            Scale = scale,
            Value = Whole(value),
            Level = level,
            Word = AirBands.Word(scale, level),
            Short = AirBands.Short(scale, level),
            Name = AirBands.Name(scale),
            Pollutants = PollutantsOf(now),
            Pollen = PollenOf(now),
            Plants = PlantsOf(now),
            Hours = hours,
            Now = hours.Any(each => each.At.Hour == hour) ? hour : NoNow,
            Updated = updated,
            Stale = stale,
            Problem = problem,
        };
    }

    private static int Whole(double value) => (int)Math.Max(0, Math.Round(value, MidpointRounding.AwayFromZero));

    private static IReadOnlyList<AirPollutant> PollutantsOf(AirNow now)
    {
        var list = new List<AirPollutant>();
        void Add(string name, double? value)
        {
            if (value is { } amount)
            {
                list.Add(new AirPollutant(name, Whole(amount).ToString(CultureInfo.InvariantCulture), Micrograms));
            }
        }
        Add("PM2.5", now.Pm25);
        Add("PM10", now.Pm10);
        Add("O₃", now.Ozone);
        Add("NO₂", now.NitrogenDioxide);
        return list;
    }

    /// <summary>The six plants with what the answer says of each, in <see cref="Plants"/>' order.</summary>
    private static (string Name, double? Value)[] AllPlants(AirNow now) =>
    [
        (S.DockAirPollenGrass, now.GrassPollen), (S.DockAirPollenBirch, now.BirchPollen), (S.DockAirPollenAlder, now.AlderPollen),
        (S.DockAirPollenRagweed, now.RagweedPollen), (S.DockAirPollenMugwort, now.MugwortPollen), (S.DockAirPollenOlive, now.OlivePollen),
    ];

    /// <summary>§590: the six plants, each with its grains per m³ (0 where the answer has none for it); null where the place has no pollen forecast at all.</summary>
    internal static IReadOnlyList<(string Name, string Value)>? PlantsOf(AirNow now)
    {
        var all = AllPlants(now);
        return all.All(pollen => pollen.Value is null)
            ? null
            : [.. all.Select(pollen => (pollen.Name, Whole(pollen.Value ?? 0).ToString(CultureInfo.InvariantCulture)))];
    }

    /// <summary>The pollen in the air now, the most first, those at 0 left out; empty where none is; null where the place has no pollen forecast at all.</summary>
    internal static IReadOnlyList<(string Name, string Value)>? PollenOf(AirNow now)
    {
        var all = AllPlants(now);
        if (all.All(pollen => pollen.Value is null))
        {
            return null;
        }
        return
        [
            .. all.Where(pollen => pollen.Value is { } count && Whole(count) > 0)
                .OrderByDescending(pollen => pollen.Value)
                .Select(pollen => (pollen.Name, Whole(pollen.Value!.Value).ToString(CultureInfo.InvariantCulture))),
        ];
    }

    /// <summary>
    /// §501: the index for the place's day, its hours from its midnight to the next in the order the answer lists them
    /// (Open-Meteo's own, earliest first, from the place's midnight), and the place's hour now. A day on one axis, as the
    /// weather's hours are (DayGraph): each hour keeps its place all day and now moves on from left to right. It was the
    /// next 24 hours from now, so every hour moved one bar to the left each hour and the day ran from right to left (the
    /// person, 2026-11-04: "the graph on the air quality widget goes from right to left in time but should go from left to
    /// right in time"). An answer of another day has none of today's: no hours.
    /// </summary>
    internal static (IReadOnlyList<AirHour> Hours, int Now) HoursOf(AirAnswer answer, AirScale scale, DateTime utcNow)
    {
        var list = new List<AirHour>();
        var local = utcNow.AddSeconds(answer.UtcOffsetSeconds ?? 0);
        if (answer.Hourly is not { Time: { } times } hourly)
        {
            return (list, local.Hour);
        }
        var values = scale == AirScale.European ? hourly.EuropeanAqi : hourly.UsAqi;
        for (var index = 0; index < times.Length && list.Count < DayHours; index++)
        {
            if (WeatherWindow.ParseHour(times[index]) is not { } at || at.Date != local.Date
                || values is null || index >= values.Length || values[index] is not { } value)
            {
                continue;
            }
            list.Add(new AirHour(at, Nls.Time(at), Whole(value), AirBands.Level(scale, value)));
        }
        return (list, local.Hour);
    }

    /// <summary>
    /// The face's tooltip: the place, the index and its level, the pollutants, the pollen and the
    /// reading's age, a line each; the problem where there is one.
    /// </summary>
    public string Tooltip(string place)
    {
        var lines = new List<string>();
        if (place.Length > 0)
        {
            lines.Add(place);
        }
        if (State == AirState.Ready)
        {
            lines.Add(S.DockAirTipIndex(index: Name, value: Value.ToString(CultureInfo.InvariantCulture), level: Word));
            if (Pollutants.Count > 0)
            {
                lines.Add(string.Join(FaceDrawings.Dot, Pollutants.Select(pollutant => $"{pollutant.Name} {pollutant.Value} {pollutant.Unit}")));
            }
            if (Pollen is { Count: > 0 } pollen)
            {
                lines.Add($"{S.DockAirPollen}: {string.Join(", ", pollen.Select(each => $"{each.Name} {each.Value}"))}");
            }
        }
        else
        {
            lines.Add(Problem ?? S.DockAirLoading);
        }
        if (Updated.Length > 0)
        {
            lines.Add(Updated);
        }
        return string.Join("\n", lines);
    }
}
