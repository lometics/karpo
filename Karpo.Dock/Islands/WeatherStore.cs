// Forecasts from Open-Meteo, which needs no account or key, fetched once per
// place however many weather widgets show it, in whichever scale Localization
// says: the answer is always asked for in degrees Celsius and kilometres an
// hour, and converted where it is shown (Weather\WeatherReading.cs). One
// answer carries the current conditions, the next hours and the next week, so
// the bar's cell, a folder's card and the flyout all read the same fetch.
//
// Its users are islands rather than weather widgets: the weather widget on the
// bar, and each of a list's three weather rows (the conditions now, today's
// curve, the week), which carry a place of their own and ask for it themselves.
// Keying on the place and not on the kind is what makes three rows and a bar
// widget showing one place one fetch rather than four, and what makes them read
// the same cache, so none of them can call a reading fresh that another calls
// past due.
//
// Nothing is requested until a widget has a place (or device location on), and
// nothing is fetched for a place no widget shows: the entry goes with the last
// widget that used it. A widget asks on its own tick, and the place is fetched
// when the one interval every online widget follows (OnlineUpdates) has passed,
// held to the weather's own floor (LeastMinutes); the flyout asks when the
// person presses Refresh, whatever the interval. The results are kept in weather.json in the local
// folder, so a restart shows the last known weather at once, and a failed
// request keeps showing it with the time it was fetched. What the device's position is called is
// kept there too (DeviceNames, docs\widgets.md §253).

using System.Globalization;
using Karpo.Core;
using Karpo.Dock.Weather;
using Windows.Devices.Geolocation;

namespace Karpo.Dock;

internal sealed class WeatherStore
{
    private const string FileName = "weather.json";

    /// <summary>Places kept in weather.json that no widget shows now, for when one comes back.</summary>
    private const int KeptPlaces = 8;

    /// <summary>How soon after an attempt another may start, so a failure that posts cannot loop.</summary>
    private static readonly TimeSpan Retry = TimeSpan.FromMinutes(5);

    /// <summary>Days of the forecast: the flyout lists a week, the bar's cell and the cards need today.</summary>
    private const int Days = 7;

    /// <summary>
    /// The weather's floor, in minutes: however short the online interval is set, a place is not
    /// fetched again sooner than this. Open-Meteo's current conditions move in quarter-hour steps
    /// (its "current" is the latest 15-minutely value), so asking sooner only fetches the same
    /// answer again from a free service that asks its users to be sparing; it was the least the
    /// widget's own interval offered, for the same reason.
    /// </summary>
    internal const int LeastMinutes = 15;

    private readonly Lock gate = new();
    private readonly Dictionary<string, Entry> entries = [];
    private readonly Dictionary<object, Entry> users = new(ReferenceEqualityComparer.Instance);
    private List<WeatherCache>? saved;

    // How a forecast is fetched, and whether weather.json is read and written. The shared store
    // asks Open-Meteo and keeps the file; a test makes a store of its own that answers from a
    // function it counts and keeps nothing, so it neither reaches the network nor reads or writes
    // a file another test might be reading.
    private readonly Func<Request, CancellationToken, Task<WeatherCache>> download;
    private readonly bool keeps;

    // What the device's positions are called (§253): looked up through the service for the shared
    // store, through a test's function — or not at all — for a test's.
    private readonly DeviceNames names;

    // The interval every online widget follows; the dock's, or a test's own.
    private readonly OnlineUpdates updates;

    private WeatherStore()
        : this(Download, keeps: true, lookup: DeviceNames.Download)
    {
    }

    /// <summary>
    /// A store of its own, fetching through <paramref name="download"/> — a test's, which counts
    /// what is asked of it and answers without the network. Nothing is read from or written to
    /// weather.json unless <paramref name="keeps"/>. The dock makes one store, <see cref="Shared"/>.
    /// The device's position is named through <paramref name="lookup"/>; with none, it is not named.
    /// </summary>
    internal WeatherStore(
        Func<Request, CancellationToken, Task<WeatherCache>> download,
        bool keeps = false,
        OnlineUpdates? updates = null,
        Func<double, double, string, CancellationToken, Task<string?>>? lookup = null)
    {
        this.download = download;
        this.keeps = keeps;
        this.updates = updates ?? OnlineUpdates.Shared;
        names = new DeviceNames(lookup ?? ((_, _, _, _) => Task.FromResult<string?>(null)), () => Save(null));
    }

    /// <summary>What the device's positions are called, as this store keeps them (§253).</summary>
    internal DeviceNames Names => names;

    /// <summary>
    /// At the dock's start: weather.json read, so the device's position is called what it was called
    /// last before any widget draws (§253) — an air or tides widget too, which reads its own file.
    /// </summary>
    internal void Restore()
    {
        if (!keeps)
        {
            return;
        }
        lock (gate)
        {
            Loaded();
        }
    }

    /// <summary>
    /// How often a place is fetched: every online widget's interval, never sooner than the
    /// weather's floor (<see cref="LeastMinutes"/>). Asked afresh each time, so a change to the
    /// setting is followed at the next tick.
    /// </summary>
    internal TimeSpan Every => updates.Every(LeastMinutes);

    /// <summary>
    /// How many fetches this store has started. Counted as a fetch is started, under the store's
    /// lock and on the asking thread — the download itself runs on the pool, so counting there
    /// would say "not yet" about a fetch that has already been decided.
    /// </summary>
    internal int Started { get; private set; }

    /// <summary>How many places are being fetched for now: one however many widgets show each.</summary>
    internal int Places
    {
        get
        {
            lock (gate)
            {
                return entries.Count;
            }
        }
    }

    private static WeatherStore? shared;

    /// <summary>
    /// Tests only, set before <see cref="Shared"/> is first asked for (the test run's isolation): what
    /// the shared store fetches through instead of Windows' location and the network, keeping no
    /// weather.json. A new weather widget is for the device's location (2026-10-11), so the test
    /// run's bars would otherwise ask both.
    /// </summary>
    internal static Func<Request, CancellationToken, Task<WeatherCache>>? SharedFetch { get; set; }

    public static WeatherStore Shared =>
        LazyInitializer.EnsureInitialized(ref shared, () => SharedFetch is { } fetch ? new WeatherStore(fetch) : new WeatherStore());

    /// <summary>
    /// On the dock's thread, once a fetch has come back or failed: an open flyout draws itself
    /// again. Raised through <see cref="StoreSignal"/>, so a burst of news raises it once.
    /// </summary>
    public event Action? Changed;

    /// <summary>What a forecast is fetched once for: the place, or device location. The unit is not part of it.</summary>
    public static string KeyOf(WeatherOptions options) =>
        options.UseDeviceLocation
            ? "device"
            : $"{options.Latitude?.ToString("R", CultureInfo.InvariantCulture)}|{options.Longitude?.ToString("R", CultureInfo.InvariantCulture)}";

    /// <summary>
    /// <paramref name="island"/> shows the weather for <paramref name="options"/>
    /// now (not set: nothing): a widget, which is woken when an answer comes, or the place's data
    /// source (WeatherData), which keeps it fetched from the dock's start whether or not a widget
    /// of it is drawn. Called on the dock's thread.
    /// </summary>
    public void Use(object island, WeatherOptions options)
    {
        Release(island);
        if (!options.IsSet)
        {
            return;
        }
        var key = KeyOf(options);
        lock (gate)
        {
            if (!entries.TryGetValue(key, out var entry))
            {
                var request = new Request(options.Latitude, options.Longitude, options.UseDeviceLocation);
                entry = new Entry(key, request, () => Changed?.Invoke()) { Shown = Cached(request) };
                entries[key] = entry;
            }
            entry.Users.Add(island);
            if (island is CellIsland woken)
            {
                entry.Signal.Add(woken);
            }
            users[island] = entry;
        }
    }

    public void Release(object island)
    {
        lock (gate)
        {
            if (!users.Remove(island, out var entry))
            {
                return;
            }
            entry.Users.Remove(island);
            if (island is CellIsland woken)
            {
                entry.Signal.Remove(woken);
            }
            if (entry.Users.Count == 0)
            {
                // Nobody shows this place any more: what is still on its way is dropped.
                entry.Stop.Cancel();
                entries.Remove(entry.Key);
            }
        }
    }

    /// <summary>
    /// The last forecast for the island's place, why a newer one isn't there if it failed, and
    /// whether one is on its way now.
    /// </summary>
    public (WeatherCache? Shown, string? Problem, bool Fetching) Read(object island)
    {
        lock (gate)
        {
            return users.TryGetValue(island, out var entry)
                ? (entry.Shown, entry.Failure is { } failure ? Explain(failure) : null, entry.Work is { IsCompleted: false })
                : (null, null, false);
        }
    }

    /// <summary>Starts a request for the island's place when its forecast is due.</summary>
    public void Poll(object island)
    {
        lock (gate)
        {
            if (!users.TryGetValue(island, out var entry) || !Due(entry))
            {
                return;
            }
            Fetch(entry);
        }
    }

    /// <summary>
    /// A fetch for the island's place now, however recent the last one was: the flyout's Refresh.
    /// False when one is already on its way (which the flyout is already showing).
    /// </summary>
    public bool Ask(object island)
    {
        lock (gate)
        {
            if (!users.TryGetValue(island, out var entry) || entry.Work is { IsCompleted: false })
            {
                return false;
            }
            Fetch(entry);
            return true;
        }
    }

    /// <summary>On the dock's thread: raises <see cref="Changed"/> once for all the news since the last time.</summary>
    internal void Deliver(object island)
    {
        StoreSignal? signal;
        lock (gate)
        {
            signal = users.TryGetValue(island, out var entry) ? entry.Signal : null;
        }
        signal?.Deliver();
    }

    private bool Due(Entry entry)
    {
        var now = Clock.RealUtc();
        // A minute short of the interval, so a tick that lands a moment early still fetches.
        var every = Every - TimeSpan.FromMinutes(1);
        return (entry.Shown is null || WallClock.Since(entry.Shown.FetchedUtc, now) >= every)
            && (WallClock.Since(entry.Attempted, now) >= Retry || updates.RetryNow(entry.Failure is not null, entry.Online))
            && entry.Work is not { IsCompleted: false };
    }

    private void Fetch(Entry entry)
    {
        Started++;
        entry.Attempted = Clock.RealUtc();
        entry.Online = updates.Comebacks;
        var cancel = entry.Stop.Token;
        entry.Work = Task.Run(
            async () =>
            {
                try
                {
                    var result = await download(entry.Request, cancel).ConfigureAwait(false);
                    lock (gate)
                    {
                        entry.Shown = result;
                        entry.Failure = null;
                    }
                    Save(result);
                }
                catch (OperationCanceledException) when (cancel.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception error) when (error is not OutOfMemoryException)
                {
                    Console.Error.WriteLine($"Karpo: weather not updated: {error.GetType().Name}");
                    lock (gate)
                    {
                        entry.Failure = error;
                    }
                }
                entry.Signal.Notify();
            },
            cancel);
    }

    private static async Task<WeatherCache> Download(Request request, CancellationToken cancel)
    {
        var (latitude, longitude) = await Where(request, cancel).ConfigureAwait(false);
        using var response = await IslandWeb.Client
            .GetAsync(Url(latitude, longitude), HttpCompletionOption.ResponseHeadersRead, cancel)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var text = await IslandWeb.Text(response, 1 << 20, cancel).ConfigureAwait(false)
            ?? throw new InvalidDataException("forecast too long");
        return new WeatherCache
        {
            FetchedUtc = Clock.RealUtc(),
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            Device = request.Device,
            // Always asked for in Celsius; what shows it converts (WeatherUnits).
            Fahrenheit = false,
            Forecast = WeatherForecasts.Parse(text),
        };
    }

    /// <summary>
    /// Where <paramref name="request"/> is for: its place, or the device's position where Windows gives
    /// it — the place kept with it where it does not — else no location at all (the air quality's store
    /// asks the same, AirQualityStore).
    /// </summary>
    internal static async Task<(double Latitude, double Longitude)> Where(Request request, CancellationToken cancel)
    {
        if (!request.Device)
        {
            return (request.Latitude!.Value, request.Longitude!.Value);
        }
        if (await Locate(cancel).ConfigureAwait(false) is { } here)
        {
            // §253: what it is called there, known or looked up now, before what was asked for is
            // fetched, so the answer comes with the name.
            await Shared.Names.At(here.Latitude, here.Longitude, cancel).ConfigureAwait(false);
            return here;
        }
        PlaceNames.Unlocated();
        if (request.Latitude is { } savedLatitude && request.Longitude is { } savedLongitude)
        {
            return (savedLatitude, savedLongitude);
        }
        throw new NoLocationException();
    }

    /// <summary>The one request: the conditions now, the next hours and the next week, in the place's own time.</summary>
    internal static string Url(double latitude, double longitude)
    {
        // Two decimals are about a kilometre: all a forecast needs, and no more
        // of where the user is than that leaves the machine.
        var lat = Math.Round(latitude, 2).ToString(CultureInfo.InvariantCulture);
        var lon = Math.Round(longitude, 2).ToString(CultureInfo.InvariantCulture);
        return $"https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}"
            + "&current=temperature_2m,apparent_temperature,weather_code,wind_speed_10m,is_day"
            + "&hourly=temperature_2m,weather_code,is_day,precipitation_probability"
            + "&daily=weather_code,temperature_2m_max,temperature_2m_min,precipitation_probability_max"
            + $"&forecast_days={Days}&timezone=auto";
    }

    private static Tuple<double, double>? deviceAt;

    /// <summary>
    /// menus: where the device was when a forecast last asked Windows (<see cref="Locate"/>), for "Open
    /// forecast" and "Open on map" of a widget that follows the device (Dock.ForecastPage); null before.
    /// </summary>
    internal static (double Latitude, double Longitude)? DeviceAt
    {
        get => Volatile.Read(ref deviceAt) is { } at ? (at.Item1, at.Item2) : null;
        private set => Volatile.Write(ref deviceAt, value is (var latitude, var longitude) ? new Tuple<double, double>(latitude, longitude) : null);
    }

    /// <summary>
    /// The device's position, as Windows gives it to desktop apps when
    /// "Let desktop apps access your location" is on; null otherwise.
    /// </summary>
    private static async Task<(double Latitude, double Longitude)?> Locate(CancellationToken cancel)
    {
        try
        {
            var access = await Geolocator.RequestAccessAsync().AsTask(cancel).ConfigureAwait(false);
            if (access != GeolocationAccessStatus.Allowed)
            {
                return null;
            }
            var locator = new Geolocator { DesiredAccuracy = PositionAccuracy.Default };
            var position = await locator.GetGeopositionAsync(TimeSpan.FromMinutes(30), TimeSpan.FromSeconds(15))
                .AsTask(cancel)
                .ConfigureAwait(false);
            var point = position.Coordinate.Point.Position;
            DeviceAt = (point.Latitude, point.Longitude);
            return (point.Latitude, point.Longitude);
        }
        catch (Exception error) when (error is not OperationCanceledException and not OutOfMemoryException)
        {
            return null;
        }
    }

    /// <summary>Whether <paramref name="problem"/> (<see cref="Explain"/>'s words) is that Windows gives no location.</summary>
    internal static bool IsLocationOff(string? problem) => problem == S.WidgetWeatherLocationOff;

    internal static string Explain(Exception error) => error switch
    {
        NoLocationException => S.WidgetWeatherLocationOff,
        HttpRequestException => S.WidgetWeatherUnreachable,
        TaskCanceledException or TimeoutException => S.WidgetWeatherNoAnswer,
        _ => S.WidgetWeatherUnreadable,
    };

    /// <summary>The saved forecast for a request, if weather.json has one. Under <see cref="gate"/>.</summary>
    private WeatherCache? Cached(Request request)
    {
        if (!keeps)
        {
            return null;
        }
        return Loaded().FirstOrDefault(cache => Matches(cache, request));
    }

    /// <summary>The forecasts weather.json keeps, read the first time they are asked for — and with them the device's names. Under <see cref="gate"/>.</summary>
    private List<WeatherCache> Loaded()
    {
        if (saved is null)
        {
            var file = Read(IslandFiles.Read(FileName));
            saved = [.. (file.Places ?? []).OfType<WeatherCache>()];
            names.Restore(file.Names);
        }
        return saved;
    }

    /// <summary>weather.json's <paramref name="json"/>: a list of places and the device's names, or (before revision 8) one place; nothing where it can't be read.</summary>
    internal static WeatherFile Read(string? json)
    {
        if (json is null)
        {
            return new WeatherFile();
        }
        try
        {
            if (System.Text.Json.JsonSerializer.Deserialize(json, IslandJson.Default.WeatherFile) is { Places: not null } file)
            {
                return file;
            }
            return new WeatherFile { Places = System.Text.Json.JsonSerializer.Deserialize(json, IslandJson.Default.WeatherCache) is { Forecast: not null } single ? [single] : [] };
        }
        catch (System.Text.Json.JsonException)
        {
            return new WeatherFile();
        }
    }

    /// <summary>Puts a new forecast into weather.json, newest first, dropping the oldest places; with none, the device's names as they are now.</summary>
    private void Save(WeatherCache? result)
    {
        if (!keeps)
        {
            return;
        }
        string json;
        lock (gate)
        {
            var places = Loaded();
            if (result is not null)
            {
                places.RemoveAll(cache => Matches(cache, result.Request));
                places.Insert(0, result);
            }
            if (places.Count > KeptPlaces + entries.Count)
            {
                places.RemoveRange(KeptPlaces + entries.Count, places.Count - KeptPlaces - entries.Count);
            }
            json = System.Text.Json.JsonSerializer.Serialize(new WeatherFile { Places = [.. places], Names = names.Snapshot() }, IslandJson.Default.WeatherFile);
        }
        IslandFiles.Write(FileName, json);
    }

    /// <summary>
    /// Whether a saved forecast answers a request. The unit is not part of it: a file written by an
    /// older Karpo may hold Fahrenheit, and what shows it converts either way.
    /// </summary>
    private static bool Matches(WeatherCache cache, Request request) =>
        cache.Device == request.Device
        && (request.Device || (cache.Latitude == request.Latitude && cache.Longitude == request.Longitude));

    private sealed class Entry(string key, Request request, Action changed)
    {
        public string Key { get; } = key;

        public Request Request { get; } = request;

        public StoreSignal Signal { get; } = new(changed);

        public CancellationTokenSource Stop { get; } = new();

        /// <summary>The widgets and the sources showing this place.</summary>
        public HashSet<object> Users { get; } = new(ReferenceEqualityComparer.Instance);

        public WeatherCache? Shown { get; set; }

        /// <summary>What the last attempt ran into, said in words only when read (<see cref="Explain"/>), so the words follow the language.</summary>
        public Exception? Failure { get; set; }

        public DateTime Attempted { get; set; }

        /// <summary>OnlineUpdates.Comebacks as it was attempted.</summary>
        public int Online { get; set; }

        public Task? Work { get; set; }
    }

    internal readonly record struct Request(double? Latitude, double? Longitude, bool Device);

    private sealed class NoLocationException : Exception;
}

/// <summary>weather.json.</summary>
internal sealed class WeatherFile
{
    public List<WeatherCache>? Places { get; set; }

    /// <summary>§253: what the device's positions were called, newest first; none in a file from before.</summary>
    public List<DeviceName>? Names { get; set; }
}

/// <summary>One forecast in weather.json, and what it was asked for.</summary>
internal sealed class WeatherCache
{
    public DateTime FetchedUtc { get; set; }

    /// <summary>Only in files from before revision 8, which kept the place name here.</summary>
    public string? Place { get; set; }

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public bool Device { get; set; }

    /// <summary>The unit the numbers are in. Karpo now always fetches Celsius; older files may say true.</summary>
    public bool Fahrenheit { get; set; }

    public Forecast? Forecast { get; set; }

    internal WeatherStore.Request Request => new(Latitude, Longitude, Device);
}
