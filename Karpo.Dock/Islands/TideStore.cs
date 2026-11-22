// The tides from Open-Meteo's marine service (docs\widgets.md §236), fetched once per place however
// many tides widgets show it, as the air's store does (AirQualityStore): its users are islands — the
// tides face's folder and each tides line — keyed by their place (WeatherStore.KeyOf), so a face and
// a line of one place are one fetch and read one answer.
//
// The answer is a forecast of the sea's height for days, so it is asked for a few times a day, every
// six hours (Interval), and at once on Refresh; between fetches every widget works its next high or
// low and the height now out of the series it holds, which stays good for days. The answers are kept
// in tides.json in the local folder, so a restart shows the last known tides at once, and a failed
// request keeps showing them with their age.

using Karpo.Core;

namespace Karpo.Dock;

internal sealed class TideStore
{
    private const string FileName = "tides.json";

    /// <summary>Places kept in tides.json that no widget shows now, for when one comes back.</summary>
    private const int KeptPlaces = 8;

    /// <summary>How often a place is fetched: four times a day. The series is a three days' forecast.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    /// <summary>How soon after an attempt another may start, so a failure that posts cannot loop.</summary>
    private static readonly TimeSpan Retry = TimeSpan.FromMinutes(5);

    private readonly Lock gate = new();
    private readonly Dictionary<string, Entry> entries = [];
    private readonly Dictionary<object, Entry> users = new(ReferenceEqualityComparer.Instance);
    private List<TideCache>? saved;

    // How an answer is fetched, and whether tides.json is read and written: the shared store asks
    // Open-Meteo and keeps the file; a test's answers from a function and keeps nothing.
    private readonly Func<WeatherStore.Request, CancellationToken, Task<TideCache>> download;
    private readonly bool keeps;

    /// <summary>Only for the network coming back (OnlineUpdates.RetryNow): the tides keep a pace of their own.</summary>
    private readonly OnlineUpdates updates;

    private TideStore()
        : this(Download, keeps: true)
    {
    }

    /// <summary>A store of its own, fetching through <paramref name="download"/> — a test's; nothing read or written unless <paramref name="keeps"/>.</summary>
    internal TideStore(Func<WeatherStore.Request, CancellationToken, Task<TideCache>> download, bool keeps = false, OnlineUpdates? updates = null)
    {
        this.download = download;
        this.keeps = keeps;
        this.updates = updates ?? OnlineUpdates.Shared;
    }

    /// <summary>How often a place is fetched (<see cref="Interval"/>).</summary>
    internal TimeSpan Every => Interval;

    /// <summary>How many fetches this store has started, counted as each is decided.</summary>
    internal int Started { get; private set; }

    private static TideStore? shared;

    /// <summary>
    /// Tests only, set before <see cref="Shared"/> is first asked for (the test run's isolation): what
    /// the shared store fetches through instead of Windows' location and the network.
    /// </summary>
    internal static Func<WeatherStore.Request, CancellationToken, Task<TideCache>>? SharedFetch { get; set; }

    public static TideStore Shared =>
        LazyInitializer.EnsureInitialized(ref shared, () => SharedFetch is { } fetch ? new TideStore(fetch) : new TideStore());

    /// <summary><paramref name="island"/> shows the tides for <paramref name="options"/> now (not set: nothing). On the dock's thread.</summary>
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
                entry = new Entry(key, request, () => { }) { Shown = Cached(request) };
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
    public (TideCache? Shown, string? Problem, bool Fetching) Read(object island)
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

    /// <summary>On the dock's thread: the islands woken for the news since the last time.</summary>
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
                    Console.Error.WriteLine($"Karpo: tides not updated: {error.GetType().Name}");
                    lock (gate)
                    {
                        entry.Failure = error;
                    }
                }
                entry.Signal.Notify();
            },
            cancel);
    }

    private static async Task<TideCache> Download(WeatherStore.Request request, CancellationToken cancel)
    {
        var (latitude, longitude) = await WeatherStore.Where(request, cancel).ConfigureAwait(false);
        using var response = await IslandWeb.Client
            .GetAsync(TideAnswers.Url(latitude, longitude), HttpCompletionOption.ResponseHeadersRead, cancel)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var text = await IslandWeb.Text(response, 1 << 20, cancel).ConfigureAwait(false)
            ?? throw new InvalidDataException("tides too long");
        return new TideCache
        {
            FetchedUtc = Clock.RealUtc(),
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            Device = request.Device,
            Answer = TideAnswers.Parse(text),
        };
    }

    /// <summary>The weather's words for what went wrong, but the tides' own for an answer it could not read.</summary>
    internal static string Explain(Exception error) =>
        error is InvalidDataException or System.Text.Json.JsonException ? S.DockTidesUnreadable : WeatherStore.Explain(error);

    private TideCache? Cached(WeatherStore.Request request)
    {
        if (!keeps)
        {
            return null;
        }
        saved ??= Load();
        return saved.FirstOrDefault(cache => Matches(cache, request));
    }

    private static List<TideCache> Load()
    {
        if (IslandFiles.Read(FileName) is not { } json)
        {
            return [];
        }
        return Read(json);
    }

    /// <summary>The places in a tides.json: those with an answer; none where it cannot be read.</summary>
    internal static List<TideCache> Read(string json)
    {
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize(json, IslandJson.Default.TideFile) is { Places: { } places }
                ? places.Where(place => place?.Answer is not null).ToList()
                : [];
        }
        catch (System.Text.Json.JsonException)
        {
            return [];
        }
    }

    private void Save(TideCache result)
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
            json = System.Text.Json.JsonSerializer.Serialize(new TideFile { Places = [.. saved] }, IslandJson.Default.TideFile);
        }
        IslandFiles.Write(FileName, json);
    }

    private static bool Matches(TideCache cache, WeatherStore.Request request) =>
        cache.Device == request.Device
        && (request.Device || (cache.Latitude == request.Latitude && cache.Longitude == request.Longitude));

    private sealed class Entry(string key, WeatherStore.Request request, Action changed)
    {
        public string Key { get; } = key;

        public WeatherStore.Request Request { get; } = request;

        public StoreSignal Signal { get; } = new(changed);

        public CancellationTokenSource Stop { get; } = new();

        public HashSet<object> Users { get; } = new(ReferenceEqualityComparer.Instance);

        public TideCache? Shown { get; set; }

        public Exception? Failure { get; set; }

        public DateTime Attempted { get; set; }

        /// <summary>OnlineUpdates.Comebacks as it was attempted.</summary>
        public int Online { get; set; }

        public Task? Work { get; set; }
    }
}

/// <summary>tides.json.</summary>
internal sealed class TideFile
{
    public List<TideCache>? Places { get; set; }
}

/// <summary>One answer in tides.json, and what it was asked for.</summary>
internal sealed class TideCache
{
    public DateTime FetchedUtc { get; set; }

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public bool Device { get; set; }

    public TideAnswer? Answer { get; set; }

    internal WeatherStore.Request Request => new(Latitude, Longitude, Device);
}
