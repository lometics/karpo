// The earthquakes (docs\widgets.md §240): the USGS's feed as far as Karpo reads it, how far and which
// way each one is from a widget's place, which a widget shows, which are new, and the words a face and
// a line say about them.
//
// **The source.** The USGS Earthquake Hazards Program's GeoJSON summary feed of the past week's
// earthquakes of magnitude 2.5 and up (earthquake.usgs.gov/earthquakes/feed/v1.0/summary/2.5_week.geojson):
// public domain, no account, no key, renewed every minute behind a CDN, some 30 KB compressed. One
// feed for every widget: each one's radius and magnitude are worked out here, from the one answer, so
// the place a widget is for never leaves the PC — the FDSN query service would filter by place on the
// server, but it would be told where the person is, and the USGS asks that polling clients use the
// summary feeds. Only events of type "earthquake" are kept (the feed's quarry blasts and explosions
// are not).
//
// **Which are new.** Every id the feed has listed is remembered with its magnitude (quakes.json), the
// same event's other networks' ids with it (the feed's "ids"), so a new answer's new events are those
// with no id seen before — or one seen under the widget's least magnitude that has since been revised
// over it. Never on the first answer ever (nothing seen yet), never for an event older than three hours
// (a PC asleep for a day wakes to no alarms for yesterday's), and never twice: the seen ids outlive a
// restart.

using System.Globalization;
using System.Text.Json.Serialization;
using Karpo.Core;
using Karpo.Dock.Panels;
using Karpo.Dock.Weather;

namespace Karpo.Dock;

/// <summary>The parts of the USGS's GeoJSON feed Karpo reads.</summary>
internal sealed class QuakeFeedAnswer
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("features")]
    public List<QuakeFeature?>? Features { get; set; }
}

/// <summary>One event of the feed.</summary>
internal sealed class QuakeFeature
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("properties")]
    public QuakeProperties? Properties { get; set; }

    [JsonPropertyName("geometry")]
    public QuakeGeometry? Geometry { get; set; }
}

/// <summary>An event's properties: its magnitude, where in words, when (ms since 1970, UTC), its page and its other ids.</summary>
internal sealed class QuakeProperties
{
    [JsonPropertyName("mag")]
    public double? Mag { get; set; }

    [JsonPropertyName("place")]
    public string? Place { get; set; }

    [JsonPropertyName("time")]
    public long? Time { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    /// <summary>Every network's id for the event, ",us6000u3de,aka2026usthoq,".</summary>
    [JsonPropertyName("ids")]
    public string? Ids { get; set; }

    /// <summary>"earthquake", "quarry blast", "explosion", "ice quake"…</summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }
}

/// <summary>Where: longitude, latitude and depth in km.</summary>
internal sealed class QuakeGeometry
{
    [JsonPropertyName("coordinates")]
    public double[]? Coordinates { get; set; }
}

/// <summary>One earthquake, as Karpo keeps it.</summary>
internal sealed class Quake
{
    /// <summary>The feed's preferred id, "us6000u3fl".</summary>
    public string Id { get; set; } = "";

    /// <summary>Every id the event goes by, the preferred one among them.</summary>
    public string[] Ids { get; set; } = [];

    public double Magnitude { get; set; }

    /// <summary>The USGS's words for where, "23 km SSW of Lovington, New Mexico"; English, as the feed has it.</summary>
    public string Place { get; set; } = "";

    public DateTime TimeUtc { get; set; }

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    /// <summary>How deep, in km; null where the feed gives none.</summary>
    public double? Depth { get; set; }

    /// <summary>Its page on the USGS's site.</summary>
    public string Url { get; set; } = "";
}

/// <summary>Reading the feed.</summary>
internal static class QuakeFeeds
{
    /// <summary>The one feed every widget reads: the past week's earthquakes of 2.5 and up.</summary>
    public const string Url = "https://earthquake.usgs.gov/earthquakes/feed/v1.0/summary/2.5_week.geojson";

    /// <summary>Where an event's page is, for an id whose own address is not the USGS's.</summary>
    private const string Pages = "https://earthquake.usgs.gov/earthquakes/eventpage/";

    /// <summary>
    /// The earthquakes in <paramref name="json"/>, newest first; an event with no magnitude, time or
    /// place, or of another type, is left out. Throws <see cref="InvalidDataException"/> for what is
    /// no feed at all.
    /// </summary>
    public static List<Quake> Parse(string json)
    {
        QuakeFeedAnswer? answer;
        try
        {
            answer = System.Text.Json.JsonSerializer.Deserialize(json, IslandJson.Default.QuakeFeedAnswer);
        }
        catch (System.Text.Json.JsonException error)
        {
            throw new InvalidDataException("the earthquakes could not be read", error);
        }
        if (answer?.Features is not { } features)
        {
            throw new InvalidDataException("no earthquake feed");
        }
        var quakes = new List<Quake>();
        foreach (var feature in features)
        {
            if (Of(feature) is { } quake)
            {
                quakes.Add(quake);
            }
        }
        quakes.Sort((one, other) => other.TimeUtc.CompareTo(one.TimeUtc));
        return quakes;
    }

    private static Quake? Of(QuakeFeature? feature)
    {
        if (feature is not { Id: { Length: > 0 } id, Properties: { } properties, Geometry.Coordinates: { Length: >= 2 } at }
            || properties.Mag is not { } magnitude || double.IsNaN(magnitude)
            || properties.Time is not { } time
            || !string.Equals(properties.Type ?? "earthquake", "earthquake", StringComparison.OrdinalIgnoreCase)
            || at[1] is < -90 or > 90 || at[0] is < -180 or > 180)
        {
            return null;
        }
        var ids = (properties.Ids ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Prepend(id).Distinct(StringComparer.Ordinal).ToArray();
        var url = properties.Url is { } own && own.StartsWith("https://earthquake.usgs.gov/", StringComparison.Ordinal) ? own : Pages + Uri.EscapeDataString(id);
        return new Quake
        {
            Id = id,
            Ids = ids,
            Magnitude = Math.Round(magnitude, 1, MidpointRounding.AwayFromZero),
            Place = (properties.Place ?? "").Trim(),
            TimeUtc = DateTime.UnixEpoch.AddMilliseconds(time),
            Latitude = at[1],
            Longitude = at[0],
            Depth = at.Length >= 3 && !double.IsNaN(at[2]) ? at[2] : null,
            Url = url,
        };
    }
}

/// <summary>Distances and bearings on the Earth, as a sphere.</summary>
internal static class QuakeGeo
{
    /// <summary>The Earth's mean radius, km.</summary>
    private const double Radius = 6371.0088;

    private static double Rad(double degrees) => degrees * Math.PI / 180;

    /// <summary>How far apart two points are along the ground, km (haversine).</summary>
    public static double Distance(double fromLatitude, double fromLongitude, double toLatitude, double toLongitude)
    {
        var dLat = Rad(toLatitude - fromLatitude);
        var dLon = Rad(toLongitude - fromLongitude);
        var a = (Math.Sin(dLat / 2) * Math.Sin(dLat / 2))
            + (Math.Cos(Rad(fromLatitude)) * Math.Cos(Rad(toLatitude)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2));
        return 2 * Radius * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    /// <summary>Which way the second point is from the first, degrees clockwise from north, 0 to 360.</summary>
    public static double Bearing(double fromLatitude, double fromLongitude, double toLatitude, double toLongitude)
    {
        var start = Rad(fromLatitude);
        var end = Rad(toLatitude);
        var dLon = Rad(toLongitude - fromLongitude);
        var y = Math.Sin(dLon) * Math.Cos(end);
        var x = (Math.Cos(start) * Math.Sin(end)) - (Math.Sin(start) * Math.Cos(end) * Math.Cos(dLon));
        var bearing = Math.Atan2(y, x) * 180 / Math.PI;
        return (bearing + 360) % 360;
    }

    /// <summary>The nearest of the compass's eight points to <paramref name="bearing"/>: 0 north, 1 north-east … 7 north-west.</summary>
    public static int Point(double bearing) => (int)Math.Floor(((((bearing % 360) + 360) % 360) + 22.5) / 45) % 8;

    /// <summary>The point in the language's words: "NE".</summary>
    public static string Compass(int point) => (point % 8) switch
    {
        0 => S.DockQuakesCompassN,
        1 => S.DockQuakesCompassNe,
        2 => S.DockQuakesCompassE,
        3 => S.DockQuakesCompassSe,
        4 => S.DockQuakesCompassS,
        5 => S.DockQuakesCompassSw,
        6 => S.DockQuakesCompassW,
        _ => S.DockQuakesCompassNw,
    };
}

/// <summary>How strong an earthquake reads: its colour, by the magnitude's class.</summary>
internal static class QuakeLevels
{
    /// <summary>How many classes there are.</summary>
    public const int Count = 6;

    /// <summary>The class of <paramref name="magnitude"/>: 0 under 4 (minor), 1 light, 2 moderate, 3 strong, 4 major, 5 great (8 and up).</summary>
    public static int Of(double magnitude) => magnitude switch
    {
        < 4 => 0,
        < 5 => 1,
        < 6 => 2,
        < 7 => 3,
        < 8 => 4,
        _ => 5,
    };

    /// <summary>
    /// The class's colour, 0xRRGGBB: Windows 11's own signal colours — its success green, caution
    /// yellow, the orange of its warnings, its critical red — then the deeper reds past them.
    /// </summary>
    public static uint Colour(int level) => Math.Clamp(level, 0, Count - 1) switch
    {
        0 => 0x6CCB5F,
        1 => 0xFCE100,
        2 => 0xF7630C,
        3 => 0xE81123,
        4 => 0xC30052,
        _ => 0x881798,
    };

    /// <summary><see cref="Colour"/> as the drawing's ink, at <paramref name="alpha"/>.</summary>
    public static TerraFX.Interop.DirectX.DXGI_RGBA Ink(int level, float alpha = 1)
    {
        var rgb = Colour(level);
        return new(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, alpha);
    }
}

/// <summary>A seen event: its magnitude when it was last seen, and when it struck, so the list can be pruned.</summary>
internal sealed class QuakeSeen
{
    public string Id { get; set; } = "";

    public double Magnitude { get; set; }

    public DateTime TimeUtc { get; set; }
}

/// <summary>An earthquake a new answer brought: new, or <paramref name="Before"/> the magnitude it had when last seen.</summary>
internal readonly record struct QuakeNew(Quake Quake, double? Before);

/// <summary>Which earthquakes of an answer are news, and which widget says them.</summary>
internal static class QuakeNews
{
    /// <summary>How recent an event must be to be said: older ones only join the list.</summary>
    public static readonly TimeSpan FreshFor = TimeSpan.FromHours(3);

    /// <summary>How long a seen id is kept after its event: past the feed's week, so it never comes back as new.</summary>
    public static readonly TimeSpan KeptFor = TimeSpan.FromDays(8);

    /// <summary>
    /// The news in <paramref name="quakes"/> against <paramref name="seen"/> (null: nothing seen ever,
    /// so no news — the first answer is only learnt): each event none of whose ids was seen, or one
    /// whose magnitude grew since; either way only if it struck within <see cref="FreshFor"/> of
    /// <paramref name="utcNow"/>.
    /// </summary>
    public static IReadOnlyList<QuakeNew> Of(IReadOnlyDictionary<string, QuakeSeen>? seen, IReadOnlyList<Quake> quakes, DateTime utcNow)
    {
        if (seen is null)
        {
            return [];
        }
        var news = new List<QuakeNew>();
        foreach (var quake in quakes)
        {
            if (utcNow - quake.TimeUtc > FreshFor)
            {
                continue;
            }
            var before = quake.Ids.Select(id => seen.TryGetValue(id, out var was) ? was : null).OfType<QuakeSeen>().ToList();
            if (before.Count == 0)
            {
                news.Add(new QuakeNew(quake, null));
            }
            else if (before.Max(was => was.Magnitude) is var most && quake.Magnitude > most + 1e-9)
            {
                news.Add(new QuakeNew(quake, most));
            }
        }
        return news;
    }

    /// <summary>
    /// What is seen after <paramref name="quakes"/>: every id of theirs at its magnitude now, beside
    /// what was seen before, those struck longer than <see cref="KeptFor"/> before <paramref name="utcNow"/> forgotten.
    /// </summary>
    public static Dictionary<string, QuakeSeen> Learn(IReadOnlyDictionary<string, QuakeSeen>? seen, IReadOnlyList<Quake> quakes, DateTime utcNow)
    {
        var next = new Dictionary<string, QuakeSeen>(StringComparer.Ordinal);
        foreach (var (id, was) in seen ?? new Dictionary<string, QuakeSeen>())
        {
            if (utcNow - was.TimeUtc <= KeptFor)
            {
                next[id] = was;
            }
        }
        foreach (var quake in quakes)
        {
            foreach (var id in quake.Ids)
            {
                next[id] = new QuakeSeen { Id = id, Magnitude = quake.Magnitude, TimeUtc = quake.TimeUtc };
            }
        }
        return next;
    }

    /// <summary>
    /// Whether a widget set to <paramref name="settings"/> at <paramref name="origin"/> says
    /// <paramref name="news"/>: its alert on, the event one it shows, and new to it — never seen, or
    /// seen under its least magnitude.
    /// </summary>
    public static bool Says(QuakeNew news, QuakeSettings settings, (double Latitude, double Longitude)? origin) =>
        settings.Alerts && QuakeFilter.Shows(news.Quake, settings, origin) && (news.Before is not { } before || before < settings.Magnitude - 1e-9);
}

/// <summary>Which earthquakes a widget shows.</summary>
internal static class QuakeFilter
{
    /// <summary>
    /// Whether a widget set to <paramref name="settings"/> at <paramref name="origin"/> shows
    /// <paramref name="quake"/>: its least magnitude or more, and within its radius — anywhere for the
    /// world, nowhere while the origin is not known.
    /// </summary>
    public static bool Shows(Quake quake, QuakeSettings settings, (double Latitude, double Longitude)? origin) =>
        quake.Magnitude >= settings.Magnitude - 1e-9
        && (settings.Worldwide || (origin is { } at && QuakeGeo.Distance(at.Latitude, at.Longitude, quake.Latitude, quake.Longitude) <= settings.Radius));
}

/// <summary>What a widget holds from the store: the last answer, why a newer one is not there, and where this PC is.</summary>
internal sealed record QuakeHeld
{
    public static QuakeHeld None { get; } = new();

    /// <summary>The last answer's earthquakes, newest first; null before the first.</summary>
    public IReadOnlyList<Quake>? Quakes { get; init; }

    public DateTime? FetchedUtc { get; init; }

    /// <summary>Why a newer answer is not there; null while nothing failed.</summary>
    public string? Problem { get; init; }

    public bool Fetching { get; init; }

    /// <summary>This PC's position, as Windows last gave it; null where it never has.</summary>
    public (double Latitude, double Longitude)? Device { get; init; }

    /// <summary>Why Windows gives no position (its location off); null where it does or was not asked.</summary>
    public string? DeviceProblem { get; init; }
}

/// <summary>Where an earthquake widget is: nothing yet, a failure with nothing to show, or a reading.</summary>
internal enum QuakeState
{
    Waiting,
    Failed,
    Ready,
}

/// <summary>One earthquake as a widget says it.</summary>
internal sealed record QuakeRow(Quake Quake, int Level, string Magnitude, string Away, string Age)
{
    /// <summary>"120 km NE · 2 h ago", or the age alone where the widget's place is not known.</summary>
    public string Under => Away.Length > 0 ? Away + FaceDrawings.Dot + S.DockQuakesAgo(Age) : S.DockQuakesAgo(Age);
}

/// <summary>What a face and a line say about the store's answer, for one widget's settings and place (<see cref="From"/>).</summary>
internal sealed record QuakeReading
{
    public QuakeState State { get; init; }

    public QuakeSettings Settings { get; init; }

    /// <summary>The week's earthquakes it shows, newest first.</summary>
    public IReadOnlyList<QuakeRow> Rows { get; init; } = [];

    /// <summary>The newest of the past day's, which a face shows; null for a quiet day.</summary>
    public QuakeRow? Latest { get; init; }

    /// <summary>"Updated 20 minutes ago"; empty while nothing has been fetched.</summary>
    public string Updated { get; init; } = "";

    /// <summary>Past due, or a fetch failed and left it standing.</summary>
    public bool Stale { get; init; }

    /// <summary>Why a newer answer is not there, or why the place is not known; null while nothing failed.</summary>
    public string? Problem { get; init; }

    /// <summary>How far back a face looks for its earthquake.</summary>
    public static readonly TimeSpan Day = TimeSpan.FromDays(1);

    /// <summary>How long an answer stands before it is stale: three fetches missed.</summary>
    private const int StaleMinutes = 15;

    /// <summary>Where a widget for <paramref name="place"/> is: its place, or this PC's position; null where it is not known.</summary>
    public static (double Latitude, double Longitude)? OriginOf(WeatherOptions? place, QuakeHeld held) =>
        place is null ? null
        : place.UseDeviceLocation ? held.Device ?? (place.Latitude is { } lat && place.Longitude is { } lon ? (lat, lon) : null)
        : place.Latitude is { } latitude && place.Longitude is { } longitude ? (latitude, longitude)
        : null;

    /// <summary>The reading for <paramref name="held"/>, for a widget set to <paramref name="settings"/> for <paramref name="place"/>, at <paramref name="utcNow"/>.</summary>
    public static QuakeReading From(QuakeHeld held, QuakeSettings settings, WeatherOptions? place, DateTime utcNow)
    {
        var updated = held.FetchedUtc is { } fetched ? WeatherAge.Ago(fetched, utcNow) : "";
        var stale = WeatherAge.IsStale(held.FetchedUtc, utcNow, StaleMinutes / WeatherAge.StaleAfterIntervals, held.Problem);
        var origin = OriginOf(place, held);
        if (held.Quakes is not { } quakes || (origin is null && !settings.Worldwide))
        {
            var problem = held.Quakes is null ? held.Problem : held.DeviceProblem ?? held.Problem;
            return new QuakeReading
            {
                State = problem is null ? QuakeState.Waiting : QuakeState.Failed,
                Settings = settings,
                Problem = problem,
                Updated = updated,
                Stale = stale,
            };
        }
        var rows = quakes.Where(quake => QuakeFilter.Shows(quake, settings, origin)).Select(quake => RowOf(quake, origin, utcNow)).ToList();
        return new QuakeReading
        {
            State = QuakeState.Ready,
            Settings = settings,
            Rows = rows,
            Latest = rows.FirstOrDefault(row => utcNow - row.Quake.TimeUtc <= Day),
            Updated = updated,
            Stale = stale,
            Problem = held.Problem,
        };
    }

    /// <summary>"M4.8", in the language's decimal sign: the magnitude's symbol, M, as every language writes it.</summary>
    public static string Magnitude(double magnitude) =>
        "M" + magnitude.ToString("0.0", CultureInfo.InvariantCulture).Replace(".", Nls.DecimalSeparator(), StringComparison.Ordinal);

    /// <summary>"120 km NE" from <paramref name="origin"/>; empty where it is not known.</summary>
    public static string Away(Quake quake, (double Latitude, double Longitude)? origin)
    {
        if (origin is not { } at)
        {
            return "";
        }
        var km = QuakeGeo.Distance(at.Latitude, at.Longitude, quake.Latitude, quake.Longitude);
        var point = QuakeGeo.Point(QuakeGeo.Bearing(at.Latitude, at.Longitude, quake.Latitude, quake.Longitude));
        return S.DockQuakesAway(distance: Math.Round(km).ToString("0", CultureInfo.InvariantCulture), direction: QuakeGeo.Compass(point));
    }

    private static QuakeRow RowOf(Quake quake, (double Latitude, double Longitude)? origin, DateTime utcNow) =>
        new(quake, QuakeLevels.Of(quake.Magnitude), Magnitude(quake.Magnitude), Away(quake, origin),
            Feeds.Age(new DateTimeOffset(quake.TimeUtc, TimeSpan.Zero), new DateTimeOffset(utcNow, TimeSpan.Zero)));

    /// <summary>
    /// The face's tooltip: the place it is for and how far it looks, the day's earthquake — its
    /// magnitude, how far, where and when, how deep — or that the day was quiet, and the answer's age.
    /// </summary>
    public string Tooltip(string place)
    {
        var lines = new List<string>();
        var within = Settings.Worldwide ? QuakeChoices.RadiusName(QuakeChoices.World) : S.DockQuakesWithin(QuakeChoices.RadiusName(Settings.Radius));
        lines.Add(place.Length > 0 ? place + FaceDrawings.Dot + within : within);
        if (State != QuakeState.Ready)
        {
            lines.Add(Problem ?? S.DockQuakesLoading);
        }
        else if (Latest is { } latest)
        {
            lines.Add(latest.Magnitude + FaceDrawings.Dot + latest.Under);
            if (latest.Quake.Place.Length > 0)
            {
                lines.Add(latest.Quake.Place);
            }
            if (latest.Quake.Depth is { } depth)
            {
                lines.Add(S.DockQuakesDepth(Math.Round(depth).ToString("0", CultureInfo.InvariantCulture)));
            }
        }
        else
        {
            lines.Add(S.DockQuakesNoneDay);
        }
        if (Updated.Length > 0)
        {
            lines.Add(Updated);
        }
        return string.Join("\n", lines);
    }
}
