// What the device's position is called (docs\widgets.md §253), for every widget that is for the
// device's location — the weather, the air quality, the tides, the earthquakes — through the one
// place they all ask where the device is (WeatherStore.Where), so no widget looks a name up itself.
//
// The source is BigDataCloud's free client-side reverse geocoding, which needs no account or key
// and is meant for exactly this: an app on the device itself, asking about the position the device
// just gave (its terms rule out a server asking for others, and anything but the device's own
// position). Open-Meteo, which the place search uses, finds places by name only; Windows' own
// MapLocationFinder wants a Bing Maps key from a desktop app, and Bing Maps is gone; Nominatim asks
// apps to keep to one request a second across all their users and to bring their own server for
// more. The position is sent to two decimals, a kilometre — what the forecast already sends — and
// a name is asked once per such square and language: the answer is kept in weather.json with the
// forecasts (WeatherStore), the newest few, so a restart or no network still shows the last name.
// A failed look-up is not kept, and not asked again for a quarter of an hour; meanwhile, and for a
// position with no name at all, the widgets say "Current location".

using System.Globalization;
using System.Text.Json;
using Karpo.Core;

namespace Karpo.Dock;

/// <summary>The names of the device's positions, looked up once each and kept (<see cref="DeviceName"/>).</summary>
internal sealed class DeviceNames
{
    /// <summary>How many positions' names are kept: the newest, the one the device is at first.</summary>
    internal const int Kept = 8;

    /// <summary>How soon a look-up that failed may be tried again for the same position.</summary>
    internal static readonly TimeSpan Retry = TimeSpan.FromMinutes(15);

    private readonly Lock gate = new();
    private readonly List<DeviceName> names = [];
    private readonly Dictionary<string, Task<string?>> asking = [];
    private readonly Dictionary<string, DateTime> failed = [];
    private readonly Func<double, double, string, CancellationToken, Task<string?>> lookup;
    private readonly Action changed;

    /// <param name="lookup">What a name is asked of: the service, or a test's.</param>
    /// <param name="changed">A new name is kept: weather.json is written again.</param>
    internal DeviceNames(Func<double, double, string, CancellationToken, Task<string?>> lookup, Action? changed = null)
    {
        this.lookup = lookup;
        this.changed = changed ?? (() => { });
    }

    /// <summary>How many look-ups were started: once per position and language, however many widgets ask.</summary>
    internal int Asked { get; private set; }

    /// <summary>The names kept, newest first, as weather.json has them.</summary>
    internal List<DeviceName> Snapshot()
    {
        lock (gate)
        {
            return [.. names];
        }
    }

    /// <summary>
    /// The names weather.json kept: the newest is where the device was last, and widgets call it that
    /// from the start, before Windows is asked again.
    /// </summary>
    internal void Restore(IEnumerable<DeviceName?>? kept)
    {
        lock (gate)
        {
            // Read after a name was looked up (weather.json read late): the names here are newer.
            var fresh = names.Count == 0;
            // A file edited by hand may say null for words: read as none.
            foreach (var name in (kept ?? []).OfType<DeviceName>().Where(name => double.IsFinite(name.Latitude) && double.IsFinite(name.Longitude)))
            {
                var read = new DeviceName { Latitude = name.Latitude, Longitude = name.Longitude, Language = name.Language ?? "", Name = name.Name ?? "" };
                if (names.Count < Kept && !names.Exists(known => Key(known.Latitude, known.Longitude) == Key(read.Latitude, read.Longitude) && known.Language == read.Language))
                {
                    names.Add(read);
                }
            }
            if (fresh && names.Count > 0)
            {
                PlaceNames.Located(Best(Key(names[0].Latitude, names[0].Longitude), Language));
            }
        }
    }

    /// <summary>
    /// The device is at <paramref name="latitude"/>, <paramref name="longitude"/>: what it is called
    /// there, known or looked up now, is what the widgets call it (<see cref="PlaceNames.Located"/>).
    /// Never throws but for <paramref name="cancel"/>: a name not found leaves "Current location".
    /// </summary>
    internal async Task At(double latitude, double longitude, CancellationToken cancel)
    {
        var (lat, lon) = (Round(latitude), Round(longitude));
        var key = Key(lat, lon);
        var language = Language;
        var asked = key + "|" + language;
        Task<string?> work;
        lock (gate)
        {
            if (names.FindIndex(name => Key(name.Latitude, name.Longitude) == key && name.Language == language) is var at and >= 0)
            {
                var known = names[at];
                names.RemoveAt(at);
                names.Insert(0, known);
                PlaceNames.Located(known.Name);
                return;
            }
            if (asking.TryGetValue(asked, out var pending))
            {
                work = pending;
            }
            else if (failed.TryGetValue(asked, out var when) && Clock.RealUtc() - when < Retry)
            {
                PlaceNames.Located(Best(key, language));
                return;
            }
            else
            {
                Asked++;
                // On the pool, so the look-up never runs under the lock, whatever the lookup is.
                work = Task.Run(() => Ask(lat, lon, key, language));
                asking[asked] = work;
            }
        }
        var found = await work.WaitAsync(cancel).ConfigureAwait(false);
        lock (gate)
        {
            if (asking.TryGetValue(asked, out var done) && done == work)
            {
                asking.Remove(asked);
            }
            PlaceNames.Located(found ?? Best(key, language));
        }
    }

    private async Task<string?> Ask(double latitude, double longitude, string key, string language)
    {
        string? found;
        try
        {
            found = await lookup(latitude, longitude, language, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Console.Error.WriteLine($"Karpo: the device's place not named: {error.GetType().Name}");
            lock (gate)
            {
                failed[key + "|" + language] = Clock.RealUtc();
            }
            return null;
        }
        lock (gate)
        {
            failed.Remove(key + "|" + language);
            // A position with no name at all is kept too, empty, so it is not asked again.
            names.RemoveAll(name => Key(name.Latitude, name.Longitude) == key && name.Language == language);
            names.Insert(0, new DeviceName { Latitude = latitude, Longitude = longitude, Language = language, Name = found ?? "" });
            if (names.Count > Kept)
            {
                names.RemoveRange(Kept, names.Count - Kept);
            }
        }
        changed();
        return found;
    }

    /// <summary>The name of <paramref name="key"/> in <paramref name="language"/>, else in any language kept (a language changed since); null for none. Under <see cref="gate"/>.</summary>
    private string? Best(string key, string language)
    {
        var here = names.Where(name => Key(name.Latitude, name.Longitude) == key && name.Name.Length > 0).ToList();
        return (here.Find(name => name.Language == language) ?? here.FirstOrDefault())?.Name;
    }

    /// <summary>A position to two decimals, about a kilometre: what is asked, and what a name is kept for.</summary>
    internal static double Round(double degrees) => Math.Round(degrees, 2);

    internal static string Key(double latitude, double longitude) =>
        string.Create(CultureInfo.InvariantCulture, $"{Round(latitude):0.00}|{Round(longitude):0.00}");

    /// <summary>The language names are asked in: Karpo's own, as two letters ("de", "pt", "zh"); English for the test language.</summary>
    internal static string Language =>
        Strings.Language is var language && language != Languages.Pseudo ? language.Split('-')[0].ToLowerInvariant() : "en";

    /// <summary>The one request: the place at a position, its words in <paramref name="language"/>.</summary>
    internal static string Url(double latitude, double longitude, string language) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"https://api.bigdatacloud.net/data/reverse-geocode-client?latitude={Round(latitude):0.00}&longitude={Round(longitude):0.00}&localityLanguage={Uri.EscapeDataString(language)}");

    /// <summary>The service's answer: the city (else the locality, a sea's name out at sea) and its region and country, as a picked place is written (<see cref="PlaceNames.Label"/>); null where it names nothing.</summary>
    internal static string? Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("not a place");
        }
        var name = Text(root, "city") is { Length: > 0 } city ? city : Text(root, "locality");
        var label = PlaceNames.Label(name, Text(root, "principalSubdivision"), Text(root, "countryName"));
        return label.Length > 0 ? label : null;
    }

    /// <summary>Asks the service.</summary>
    internal static async Task<string?> Download(double latitude, double longitude, string language, CancellationToken cancel)
    {
        using var response = await IslandWeb.Client.GetAsync(Url(latitude, longitude, language), HttpCompletionOption.ResponseHeadersRead, cancel).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var text = await IslandWeb.Text(response, 1 << 18, cancel).ConfigureAwait(false) ?? throw new InvalidDataException("place too long");
        return Parse(text);
    }

    private static string Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()?.Trim() ?? "" : "";
}

/// <summary>What the device's position was called, in weather.json: the position to two decimals, the language asked in, and its name (empty: it has none).</summary>
internal sealed class DeviceName
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string Language { get; set; } = "";
    public string Name { get; set; } = "";
}
