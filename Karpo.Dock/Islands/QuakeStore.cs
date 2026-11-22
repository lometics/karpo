// The earthquakes from the USGS's feed (docs\widgets.md §240), fetched once for every earthquake
// widget however many there are and wherever they look: there is one feed, the past week's of 2.5 and
// up, and each widget picks its own from it (QuakeFilter). Its users are the earthquake face's folder,
// each earthquake line, and the dock, which watches it from its start so a new earthquake is said
// whether or not a widget is looked at (Dock.Quakes.cs).
//
// It is fetched every five minutes while anybody uses it — the feed is renewed every minute, and an
// alert an hour late is no alert, so not every online widget's interval — and at once on Refresh; a
// failure waits five minutes, then ten, twenty and so on to an hour, and the last answer keeps showing
// with its age. This PC's position is asked of Windows with each fetch while a widget is for it.
//
// quakes.json in the local folder keeps the last answer, this PC's last position and every event id
// seen (QuakeNews), so a restart shows the last answer at once and says nothing it said before.

using Karpo.Core;
using Karpo.Dock.Weather;

namespace Karpo.Dock;

internal sealed class QuakeStore
{
    private const string FileName = "quakes.json";

    /// <summary>How often the feed is fetched while it is used.</summary>
    internal static readonly TimeSpan Every = TimeSpan.FromMinutes(5);

    /// <summary>The longest a failure makes it wait.</summary>
    private static readonly TimeSpan LongestWait = TimeSpan.FromHours(1);

    private readonly Lock gate = new();
    private readonly HashSet<object> users = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<object> located = new(ReferenceEqualityComparer.Instance);
    private readonly StoreSignal signal;
    private readonly List<QuakeNew> news = [];

    // How the feed and this PC's position are fetched, and where the file is read and written: the
    // shared store's are the USGS's, Windows' and quakes.json; a test's its own, or nothing kept.
    private readonly Func<CancellationToken, Task<string>> download;
    private readonly Func<CancellationToken, Task<(double Latitude, double Longitude)?>> locate;
    private readonly Func<string?>? read;
    private readonly Action<string>? write;
    private readonly Func<DateTime> utcNow;

    private bool loaded;
    private QuakeFile? shown;
    private Dictionary<string, QuakeSeen>? seen;
    private Exception? failure;
    private string? deviceProblem;
    private int failures;
    private DateTime attempted = DateTime.MinValue;

    /// <summary>OnlineUpdates.Comebacks as the last fetch was attempted.</summary>
    private int online;
    private Task? work;

    /// <summary>Only for the network coming back (OnlineUpdates.RetryNow): the feed keeps a pace of its own.</summary>
    private readonly OnlineUpdates updates;

    private QuakeStore()
        : this(Download, Locate, () => IslandFiles.Read(FileName), text => IslandFiles.Write(FileName, text))
    {
    }

    /// <summary>A store of its own: the feed through <paramref name="download"/>, the position through <paramref name="locate"/>, the file through <paramref name="read"/> and <paramref name="write"/> (none: nothing kept).</summary>
    internal QuakeStore(
        Func<CancellationToken, Task<string>> download,
        Func<CancellationToken, Task<(double Latitude, double Longitude)?>>? locate = null,
        Func<string?>? read = null,
        Action<string>? write = null,
        Func<DateTime>? utcNow = null,
        OnlineUpdates? updates = null)
    {
        this.updates = updates ?? OnlineUpdates.Shared;
        this.download = download;
        this.locate = locate ?? (_ => Task.FromResult<(double, double)?>(null));
        this.read = read;
        this.write = write;
        this.utcNow = utcNow ?? (() => Clock.RealUtc());
        signal = new StoreSignal(() => Changed?.Invoke());
    }

    private static QuakeStore? shared;

    /// <summary>
    /// Tests only, set before <see cref="Shared"/> is first asked for (the test run's isolation): what
    /// the shared store fetches through instead of the network, keeping no file.
    /// </summary>
    internal static Func<CancellationToken, Task<string>>? SharedFetch { get; set; }

    public static QuakeStore Shared =>
        LazyInitializer.EnsureInitialized(ref shared, () => SharedFetch is { } fetch ? new QuakeStore(fetch) : new QuakeStore());

    /// <summary>How many fetches it has started, counted as each is decided.</summary>
    internal int Started { get; private set; }

    /// <summary>On the dock's thread, once a fetch has come back or failed (through <see cref="StoreSignal"/>).</summary>
    public event Action? Changed;

    /// <summary>The dock's thread looks at it: where its news is raised when no widget is there to take it.</summary>
    public void Watch() => signal.Look();

    /// <summary><paramref name="user"/> shows the earthquakes now, for this PC's position where <paramref name="device"/>. On the dock's thread.</summary>
    public void Use(object user, bool device)
    {
        lock (gate)
        {
            Load();
            users.Add(user);
            if (device)
            {
                located.Add(user);
            }
            else
            {
                located.Remove(user);
            }
        }
        if (user is CellIsland woken)
        {
            signal.Add(woken);
        }
    }

    public void Release(object user)
    {
        lock (gate)
        {
            users.Remove(user);
            located.Remove(user);
        }
        if (user is CellIsland woken)
        {
            signal.Remove(woken);
        }
    }

    /// <summary>The last answer, why a newer one is not there, whether one is on its way, and this PC's position.</summary>
    public QuakeHeld Read()
    {
        lock (gate)
        {
            Load();
            return new QuakeHeld
            {
                Quakes = shown?.Quakes,
                FetchedUtc = shown?.FetchedUtc,
                Problem = failure is { } error ? Explain(error) : null,
                Fetching = work is { IsCompleted: false },
                Device = shown is { DeviceLatitude: { } lat, DeviceLongitude: { } lon } ? (lat, lon) : null,
                DeviceProblem = deviceProblem,
            };
        }
    }

    /// <summary>Starts a fetch where one is due and somebody uses it.</summary>
    public void Poll()
    {
        lock (gate)
        {
            Load();
            if (users.Count > 0 && Due())
            {
                Fetch();
            }
        }
    }

    /// <summary>A fetch now, however recent the last: Refresh. False when one is on its way.</summary>
    public bool Ask()
    {
        lock (gate)
        {
            if (work is { IsCompleted: false })
            {
                return false;
            }
            Load();
            Fetch();
            return true;
        }
    }

    /// <summary>On the dock's thread: raises <see cref="Changed"/> once for all the news since the last time.</summary>
    internal void Deliver() => signal.Deliver();

    /// <summary>The earthquakes the answers since the last call brought (<see cref="QuakeNews"/>), each once.</summary>
    public IReadOnlyList<QuakeNew> TakeNews()
    {
        lock (gate)
        {
            var taken = news.ToList();
            news.Clear();
            return taken;
        }
    }

    /// <summary>Tests only: the fetch on its way, if any.</summary>
    internal Task? Work
    {
        get
        {
            lock (gate)
            {
                return work;
            }
        }
    }

    /// <summary>How long after an attempt the next may start: the interval, doubled with each failure in a row, an hour at most.</summary>
    internal static TimeSpan WaitAfter(int failures) =>
        failures <= 0 ? Every : TimeSpan.FromTicks(Math.Min(LongestWait.Ticks, Every.Ticks << Math.Min(failures - 1, 10)));

    private bool Due()
    {
        var now = utcNow();
        if (work is { IsCompleted: false } || (now - attempted < WaitAfter(failures) - TimeSpan.FromSeconds(30) && !updates.RetryNow(failures > 0, online)))
        {
            return false;
        }
        return shown is null || now - shown.FetchedUtc >= Every - TimeSpan.FromSeconds(30);
    }

    private void Fetch()
    {
        Started++;
        var now = utcNow();
        attempted = now;
        online = updates.Comebacks;
        var device = located.Count > 0;
        work = Task.Run(async () =>
        {
            try
            {
                var text = await download(CancellationToken.None).ConfigureAwait(false);
                var quakes = QuakeFeeds.Parse(text);
                (double Latitude, double Longitude)? here = null;
                string? nowhere = null;
                if (device)
                {
                    here = await locate(CancellationToken.None).ConfigureAwait(false);
                    nowhere = here is null ? S.WidgetWeatherLocationOff : null;
                }
                string json;
                lock (gate)
                {
                    var at = utcNow();
                    var previous = shown;
                    news.AddRange(QuakeNews.Of(seen, quakes, at));
                    seen = QuakeNews.Learn(seen, quakes, at);
                    shown = new QuakeFile
                    {
                        FetchedUtc = at,
                        Quakes = quakes,
                        // Where Windows said nothing this time, the last position it gave stands.
                        DeviceLatitude = here?.Latitude ?? previous?.DeviceLatitude,
                        DeviceLongitude = here?.Longitude ?? previous?.DeviceLongitude,
                        Seen = [.. seen.Values.DistinctBy(each => each.Id)],
                    };
                    deviceProblem = device ? nowhere : null;
                    failure = null;
                    failures = 0;
                    json = System.Text.Json.JsonSerializer.Serialize(shown, IslandJson.Default.QuakeFile);
                }
                write?.Invoke(json);
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                Console.Error.WriteLine($"Karpo: earthquakes not updated: {error.GetType().Name}");
                lock (gate)
                {
                    failure = error;
                    failures++;
                }
            }
            signal.Notify();
        });
    }

    /// <summary>The weather's words for what went wrong, but the earthquakes' own for a feed it could not read.</summary>
    internal static string Explain(Exception error) =>
        error is InvalidDataException or System.Text.Json.JsonException ? S.DockQuakesUnreadable : WeatherStore.Explain(error);

    /// <summary>quakes.json, once: the last answer and what was seen; nothing where there is no file, or none to be read.</summary>
    private void Load()
    {
        if (loaded)
        {
            return;
        }
        loaded = true;
        if (read?.Invoke() is not { } json)
        {
            return;
        }
        try
        {
            if (System.Text.Json.JsonSerializer.Deserialize(json, IslandJson.Default.QuakeFile) is { } file)
            {
                file.Quakes = [.. (file.Quakes ?? []).Where(quake => quake is { Id.Length: > 0 })];
                shown = file.FetchedUtc == default ? null : file;
                seen = (file.Seen ?? []).Where(each => each is { Id.Length: > 0 }).DistinctBy(each => each.Id).ToDictionary(each => each.Id, StringComparer.Ordinal);
            }
        }
        catch (System.Text.Json.JsonException)
        {
        }
    }

    private static async Task<string> Download(CancellationToken cancel)
    {
        using var response = await IslandWeb.Client.GetAsync(QuakeFeeds.Url, HttpCompletionOption.ResponseHeadersRead, cancel).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await IslandWeb.Text(response, 4 << 20, cancel).ConfigureAwait(false)
            ?? throw new InvalidDataException("earthquake feed too long");
    }

    /// <summary>This PC's position as Windows gives it, the weather's way; null where its location is off.</summary>
    private static async Task<(double Latitude, double Longitude)?> Locate(CancellationToken cancel)
    {
        try
        {
            return await WeatherStore.Where(new WeatherStore.Request(null, null, true), cancel).ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OperationCanceledException and not OutOfMemoryException)
        {
            return null;
        }
    }
}

/// <summary>quakes.json: the last answer, this PC's last position and every event id seen.</summary>
internal sealed class QuakeFile
{
    public DateTime FetchedUtc { get; set; }

    public List<Quake>? Quakes { get; set; }

    public double? DeviceLatitude { get; set; }

    public double? DeviceLongitude { get; set; }

    public List<QuakeSeen>? Seen { get; set; }
}
