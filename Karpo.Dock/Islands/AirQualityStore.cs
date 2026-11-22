// The air quality from Open-Meteo's air-quality service (docs\widgets.md §210), fetched once per
// place however many air-quality widgets show it, as the weather's store does for forecasts
// (WeatherStore): its users are islands — the air-quality face's folder and each air-quality line —
// keyed by their place (WeatherStore.KeyOf, the same places the weather's are), so a face and a line
// of one place are one fetch and read one answer.
//
// Nothing is asked until a widget is looked at with a place (or this PC's location on); a place is
// fetched again when the interval every online widget follows has passed, held to the weather's
// floor (WeatherStore.LeastMinutes) — the air is reported by the hour, so asking sooner fetches the
// same answer — and at once on Refresh. Both indices come in one answer, so the index a widget shows
// is not part of the key. The answers are kept in air.json in the local folder, so a restart shows
// the last known air at once, and a failed request keeps showing it with its age.

using Karpo.Core;
using Karpo.Dock.Weather;

namespace Karpo.Dock;

internal sealed class AirQualityStore
{
    private const string FileName = "air.json";

    /// <summary>Places kept in air.json that no widget shows now, for when one comes back.</summary>
    private const int KeptPlaces = 8;

    /// <summary>How soon after an attempt another may start, so a failure that posts cannot loop.</summary>
    private static readonly TimeSpan Retry = TimeSpan.FromMinutes(5);

    private readonly Lock gate = new();
    private readonly Dictionary<string, Entry> entries = [];
    private readonly Dictionary<object, Entry> users = new(ReferenceEqualityComparer.Instance);
    private List<AirCache>? saved;

    // How an answer is fetched, and whether air.json is read and written: the shared store asks
    // Open-Meteo and keeps the file; a test's answers from a function and keeps nothing.
    private readonly Func<WeatherStore.Request, CancellationToken, Task<AirCache>> download;
    private readonly bool keeps;
    private readonly OnlineUpdates updates;

    private AirQualityStore()
        : this(Download, keeps: true)
    {
    }

    /// <summary>A store of its own, fetching through <paramref name="download"/> — a test's; nothing read or written unless <paramref name="keeps"/>.</summary>
    internal AirQualityStore(Func<WeatherStore.Request, CancellationToken, Task<AirCache>> download, bool keeps = false, OnlineUpdates? updates = null)
    {
        this.download = download;
        this.keeps = keeps;
        this.updates = updates ?? OnlineUpdates.Shared;
    }

    /// <summary>How often a place is fetched: every online widget's interval, never sooner than the weather's floor.</summary>
    internal TimeSpan Every => updates.Every(WeatherStore.LeastMinutes);

    /// <summary>How many fetches this store has started, counted as each is decided.</summary>
    internal int Started { get; private set; }

    private static AirQualityStore? shared;

    /// <summary>
    /// Tests only, set before <see cref="Shared"/> is first asked for (the test run's isolation): what
    /// the shared store fetches through instead of Windows' location and the network.
    /// </summary>
    internal static Func<WeatherStore.Request, CancellationToken, Task<AirCache>>? SharedFetch { get; set; }

    public static AirQualityStore Shared =>
        LazyInitializer.EnsureInitialized(ref shared, () => SharedFetch is { } fetch ? new AirQualityStore(fetch) : new AirQualityStore());

    /// <summary>On the dock's thread, once a fetch has come back or failed (through <see cref="StoreSignal"/>).</summary>
    public event Action? Changed;

    /// <summary><paramref name="island"/> shows the air for <paramref name="options"/> now (not set: nothing). On the dock's thread.</summary>
    public void Use(object island, WeatherOptions options)
    {
        Release(island);
        if (!options.IsSet)
        {
            return;
        }
        var key = WeatherStore.KeyOf(options);
        lock (gate)
        {
            if (!entries.TryGetValue(key, out var entry))
            {
                var request = new WeatherStore.Request(options.Latitude, options.Longitude, options.UseDeviceLocation);
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
                entry.Stop.Cancel();
                entries.Remove(entry.Key);
            }
        }
    }

    /// <summary>The last answer for the island's place, why a newer one isn't there, and whether one is on its way.</summary>
    public (AirCache? Shown, string? Problem, bool Fetching) Read(object island)
    {
        lock (gate)
        {
            return users.TryGetValue(island, out var entry)
                ? (entry.Shown, entry.Failure is { } failure ? Explain(failure) : null, entry.Work is { IsCompleted: false })
                : (null, null, false);
        }
    }

    /// <summary>Starts a request for the island's place when its answer is due.</summary>
    public void Poll(object island)
    {
        lock (gate)
        {
            if (users.TryGetValue(island, out var entry) && Due(entry))
            {
                Fetch(entry);
            }
        }
    }

    /// <summary>A fetch for the island's place now, however recent the last: Refresh. False when one is on its way.</summary>
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
                    Console.Error.WriteLine($"Karpo: air quality not updated: {error.GetType().Name}");
                    lock (gate)
                    {
                        entry.Failure = error;
                    }
                }
                entry.Signal.Notify();
            },
            cancel);
    }

    private static async Task<AirCache> Download(WeatherStore.Request request, CancellationToken cancel)
    {
        var (latitude, longitude) = await WeatherStore.Where(request, cancel).ConfigureAwait(false);
        using var response = await IslandWeb.Client
            .GetAsync(AirAnswers.Url(latitude, longitude), HttpCompletionOption.ResponseHeadersRead, cancel)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var text = await IslandWeb.Text(response, 1 << 20, cancel).ConfigureAwait(false)
            ?? throw new InvalidDataException("air quality too long");
        return new AirCache
        {
            FetchedUtc = Clock.RealUtc(),
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            Device = request.Device,
            Answer = AirAnswers.Parse(text),
        };
    }

    /// <summary>The weather's words for what went wrong, but the air's own for an answer it could not read.</summary>
    internal static string Explain(Exception error) =>
        error is InvalidDataException or System.Text.Json.JsonException ? S.DockAirUnreadable : WeatherStore.Explain(error);

    private AirCache? Cached(WeatherStore.Request request)
    {
        if (!keeps)
        {
            return null;
        }
        saved ??= Load();
        return saved.FirstOrDefault(cache => Matches(cache, request));
    }

    private static List<AirCache> Load()
    {
        if (IslandFiles.Read(FileName) is not { } json)
        {
            return [];
        }
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize(json, IslandJson.Default.AirFile) is { Places: { } places }
                ? places.Where(place => place?.Answer is not null).ToList()
                : [];
        }
        catch (System.Text.Json.JsonException)
        {
            return [];
        }
    }

    private void Save(AirCache result)
    {
        if (!keeps)
        {
            return;
        }
        string json;
        lock (gate)
        {
            saved ??= Load();
            saved.RemoveAll(cache => Matches(cache, result.Request));
            saved.Insert(0, result);
            if (saved.Count > KeptPlaces + entries.Count)
            {
                saved.RemoveRange(KeptPlaces + entries.Count, saved.Count - KeptPlaces - entries.Count);
            }
            json = System.Text.Json.JsonSerializer.Serialize(new AirFile { Places = [.. saved] }, IslandJson.Default.AirFile);
        }
        IslandFiles.Write(FileName, json);
    }

    private static bool Matches(AirCache cache, WeatherStore.Request request) =>
        cache.Device == request.Device
        && (request.Device || (cache.Latitude == request.Latitude && cache.Longitude == request.Longitude));

    private sealed class Entry(string key, WeatherStore.Request request, Action changed)
    {
        public string Key { get; } = key;

        public WeatherStore.Request Request { get; } = request;

        public StoreSignal Signal { get; } = new(changed);

        public CancellationTokenSource Stop { get; } = new();

        public HashSet<object> Users { get; } = new(ReferenceEqualityComparer.Instance);

        public AirCache? Shown { get; set; }

        public Exception? Failure { get; set; }

        public DateTime Attempted { get; set; }

        /// <summary>OnlineUpdates.Comebacks as it was attempted.</summary>
        public int Online { get; set; }

        public Task? Work { get; set; }
    }
}

/// <summary>air.json.</summary>
internal sealed class AirFile
{
    public List<AirCache>? Places { get; set; }
}

/// <summary>One answer in air.json, and what it was asked for.</summary>
internal sealed class AirCache
{
    public DateTime FetchedUtc { get; set; }

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public bool Device { get; set; }

    public AirAnswer? Answer { get; set; }

    internal WeatherStore.Request Request => new(Latitude, Longitude, Device);
}
