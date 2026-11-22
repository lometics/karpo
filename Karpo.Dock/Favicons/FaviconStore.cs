// A pinned site's own picture, fetched once and kept for a month.
//
// **What it fetches, and from whom.** The site itself, and nothing else. The page is read for its
// <link rel="icon"> declarations and the best of those is downloaded; failing that, /favicon.ico at
// the origin. No third-party favicon endpoint is involved, ever: routing these through one would
// hand a company the list of sites the person pinned, and it would buy nothing. Requests carry
// Karpo's own user agent, no cookies (IslandWeb's handler has them off), no credentials, a short
// timeout and a hard size cap, and redirects are followed by the handler as an ordinary client's
// are.
//
// **When.** Lazily, on the first draw that wants one, and then only when the kept copy is older
// than a month. A favicon changes about never, so a start costs nothing: the file on disk answers
// before any request is made, and a pin that has an icon shows it immediately. Fetching on pinning
// instead would never refresh, and would miss every pin made in the settings app, which does not
// run this.
//
// **Until it arrives**, and if it never does, the pin shows exactly what it shows today: the first
// letter of its host, as a tile. There is no blank square at any moment, and because the answer is
// on disk from the first run onwards there is no flash of one picture replaced by another.
//
// Threading: the dock is single-threaded and has no render loop, so nothing here blocks it. Reads
// are a dictionary lookup under a lock; the fetch runs on the thread pool and posts back through
// StoreSignal, the way WeatherStore and MarketStore do.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Karpo.Dock.Favicons;

/// <summary>Where the bytes come from. The real one asks the site; a test hands in its own.</summary>
internal sealed class FaviconSource
{
    /// <summary>A page's text, capped and short-lived. Null when it cannot be read as one.</summary>
    public required Func<string, CancellationToken, Task<string?>> Page { get; init; }

    /// <summary>One candidate's bytes and its content type. Throws or returns null to mean "not this one".</summary>
    public required Func<string, CancellationToken, Task<(byte[] Bytes, string? ContentType)?>> Fetch { get; init; }

    public static FaviconSource Web { get; } = new()
    {
        Page = (address, cancel) => ReadPage(Plain, address, cancel),
        Fetch = (address, cancel) => ReadIcon(Plain, address, cancel),
    };

    /// <summary>The most redirects <see cref="Within"/> follows for one address.</summary>
    internal const int MostRedirects = 5;

    /// <summary>One GET with its Accept: the response, or null where it was not asked at all.</summary>
    private delegate Task<HttpResponseMessage?> Sender(string address, string accept, CancellationToken cancel);

    /// <summary>
    /// The site's own answers, asked only of the addresses <paramref name="allowed"/> lets by: the
    /// page, every icon its links name, and every redirect on the way to any of them, each checked
    /// before it is asked — an address it does not let by is not asked, as if it were not there.
    /// What the now-playing source fetches through (NowPlayingSource.MayFetch), so neither a page
    /// nor a redirect can make Karpo contact a host of its choosing. <paramref name="client"/> must
    /// not follow redirects itself; a test hands in its own, and the default is Karpo's own that
    /// does not.
    /// </summary>
    public static FaviconSource Within(Func<Uri, bool> allowed, HttpClient? client = null)
    {
        Task<HttpResponseMessage?> Send(string address, string accept, CancellationToken cancel) =>
            Guarded(client ?? Unfollowed.Value, allowed, address, accept, cancel);
        return new()
        {
            Page = (address, cancel) => ReadPage(Send, address, cancel),
            Fetch = (address, cancel) => ReadIcon(Send, address, cancel),
        };
    }

    /// <summary>
    /// <paramref name="address"/> asked of <paramref name="client"/>, its redirects followed here, at
    /// most <see cref="MostRedirects"/>, each only where <paramref name="allowed"/> lets it by. Null
    /// where an address on the way was not let by, or the redirects did not end.
    /// </summary>
    internal static async Task<HttpResponseMessage?> Guarded(HttpClient client, Func<Uri, bool> allowed, string address, string accept, CancellationToken cancel)
    {
        if (!Uri.TryCreate(address, UriKind.Absolute, out var url))
        {
            return null;
        }
        for (var hop = 0; hop <= MostRedirects; hop++)
        {
            if (!allowed(url))
            {
                return null;
            }
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Accept.ParseAdd(accept);
            var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancel).ConfigureAwait(false);
            if ((int)response.StatusCode is 301 or 302 or 303 or 307 or 308 && response.Headers.Location is { } location)
            {
                response.Dispose();
                url = location.IsAbsoluteUri ? location : new Uri(url, location);
                continue;
            }
            return response;
        }
        return null;
    }

    /// <summary>
    /// The client <see cref="Within"/> asks through: IslandWeb's settings — no cookies, Karpo's own
    /// user agent, a short life for a connection — but redirects are its caller's to follow.
    /// </summary>
    private static readonly Lazy<HttpClient> Unfollowed = new(() =>
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            PooledConnectionIdleTimeout = TimeSpan.FromSeconds(30),
            AutomaticDecompression = System.Net.DecompressionMethods.All,
            UseCookies = false,
            AllowAutoRedirect = false,
        };
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Karpo/1.0");
        return client;
    });

    /// <summary>A plain GET through IslandWeb's client, which follows redirects as an ordinary client's do.</summary>
    private static async Task<HttpResponseMessage?> Plain(string address, string accept, CancellationToken cancel)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, address);
        request.Headers.Accept.ParseAdd(accept);
        return await IslandWeb.Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancel).ConfigureAwait(false);
    }

    private static async Task<string?> ReadPage(Sender send, string address, CancellationToken cancel)
    {
        using var response = await send(address, "text/html", cancel).ConfigureAwait(false);
        if (response is null || !response.IsSuccessStatusCode)
        {
            return null;
        }
        // Only a page is worth parsing; anything else is not where a <link> lives.
        var type = response.Content.Headers.ContentType?.MediaType;
        return type is null || type.Contains("html", StringComparison.OrdinalIgnoreCase)
            ? await IslandWeb.Text(response, FaviconUrls.PageLimit, cancel).ConfigureAwait(false)
            : null;
    }

    private static async Task<(byte[] Bytes, string? ContentType)?> ReadIcon(Sender send, string address, CancellationToken cancel)
    {
        using var response = await send(address, "image/*", cancel).ConfigureAwait(false);
        if (response is null || !response.IsSuccessStatusCode)
        {
            return null;
        }
        var type = response.Content.Headers.ContentType?.MediaType;
        await using var stream = await response.Content.ReadAsStreamAsync(cancel).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancel).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > FaviconUrls.IconLimit)
            {
                // Far too large to be a favicon: not read any further and not kept.
                return null;
            }
            buffer.Write(chunk, 0, read);
        }
        return buffer.Length > 0 ? (buffer.ToArray(), type) : null;
    }
}

/// <summary>
/// Where the kept pictures live. The dock's is favicons.json beside the other widgets' files; a
/// test hands in one that keeps nothing, so no test ever reads or writes the person's own folder —
/// which sites they have pinned is theirs and none of a test's business.
/// </summary>
internal sealed class FaviconDisk
{
    public required Func<string?> Read { get; init; }

    public required Action<string> Write { get; init; }

    public static FaviconDisk File { get; } = new()
    {
        Read = () => IslandFiles.Read(FaviconStore.FileName),
        Write = text => IslandFiles.Write(FaviconStore.FileName, text),
    };

    /// <summary>Remembers nothing between runs: what a test uses.</summary>
    public static FaviconDisk None { get; } = new() { Read = () => null, Write = _ => { } };
}

/// <summary>One origin's kept answer, as favicons.json holds it.</summary>
internal sealed class FaviconCache
{
    /// <summary>The origin, <c>https://example.com</c> — what <see cref="FaviconUrls.OriginOf"/> gives.</summary>
    public string Origin { get; set; } = "";

    public DateTime FetchedUtc { get; set; }

    /// <summary>
    /// The picture as it was downloaded — the .ico or .png itself, not pixels. Kept that way so a
    /// bar made taller, or a second display at another DPI, decodes it again at the size it wants
    /// instead of fetching the site a second time. Null when the site had none to give.
    /// </summary>
    public string? Bytes { get; set; }

    /// <summary>Where it came from, for the record; not used to decide anything.</summary>
    public string? Address { get; set; }
}

internal sealed class FaviconFile
{
    public List<FaviconCache> Sites { get; set; } = [];
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(FaviconFile))]
internal sealed partial class FaviconJson : JsonSerializerContext;

internal sealed class FaviconStore
{
    internal const string FileName = "favicons.json";

    /// <summary>
    /// How old a kept picture may be before it is fetched again. A favicon changes about never, so
    /// this is in days: a pinned site is one request a month, not one a start.
    /// </summary>
    internal static readonly TimeSpan Fresh = TimeSpan.FromDays(30);

    /// <summary>
    /// And how long a site that gave nothing is left alone — a site with no favicon, or one that was
    /// down. Long enough that no bar hammers it, short enough that a site that gains one is picked
    /// up within a day.
    /// </summary>
    internal static readonly TimeSpan Retry = TimeSpan.FromHours(6);

    /// <summary>Origins kept in the file that no pin shows now, for when one comes back.</summary>
    private const int KeptSites = 32;

    private readonly Lock gate = new();
    private readonly Dictionary<string, Entry> entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly FaviconSource source;
    private readonly FaviconDisk disk;
    private readonly StoreSignal signal;
    private List<FaviconCache>? saved;

    internal FaviconStore(FaviconSource source, FaviconDisk? disk = null)
    {
        this.source = source;
        this.disk = disk ?? FaviconDisk.File;
        signal = new StoreSignal(() => Changed?.Invoke());
        // Its making is its look, and that is deliberate rather than the default every other store
        // has outgrown (StoreSignal.Look). The dock makes this store at startup on its own thread
        // because that is the thread a fetch posts its news back to (see Current), and nothing ever
        // attaches an island to it or delivers it — the signal's queue is its only way to raise
        // Changed. Waiting for a look that never comes would be favicons that never arrive.
        signal.Look();
    }

    /// <summary>
    /// The one the running dock uses, or null where there is no dock. The dock makes it at startup
    /// (<c>Dock.HookFavicons</c>) on its own thread, because that is the thread a fetch posts its
    /// news back to.
    /// <para>
    /// Null rather than made on demand, and that is the point: <see cref="Item.Icon"/> asks this for
    /// every web pin it draws, and a test draws web pins. Made on demand it would mean a test run
    /// reading the person's favicons.json and asking real sites for their pictures. With no dock
    /// there is no store, <see cref="Item.Icon"/> gets null, and a link is drawn as the letter tile
    /// it has always been.
    /// </para>
    /// </summary>
    public static FaviconStore? Current { get; private set; }

    /// <summary>The dock's, made once, on the thread that calls this.</summary>
    public static FaviconStore Start() => Current ??= new FaviconStore(FaviconSource.Web);

    /// <summary>On the dock's thread, once a picture has arrived. Nothing is raised for a failure.</summary>
    public event Action? Changed;

    /// <summary>
    /// The picture for <paramref name="target"/> at <paramref name="size"/> px, or null when there
    /// is none to draw yet — which is the pin's signal to keep its letter tile. Never blocks: this
    /// is a lookup, and a fetch it may start runs on the thread pool.
    /// <para>
    /// On the dock's thread, from <see cref="Item.Icon"/> while a bar is being built, and from a feed's
    /// line or face (RssStore.Icon), whose feed may name an icon of its own (<paramref name="declared"/>,
    /// §306): tried after every address of the site's own.
    /// </para>
    /// </summary>
    public Pixels? Picture(string? target, int size, string? declared = null)
    {
        if (FaviconUrls.OriginOf(target) is not { } origin || size <= 0)
        {
            return null;
        }
        Entry entry;
        lock (gate)
        {
            if (!entries.TryGetValue(origin, out var found))
            {
                found = new Entry(origin) { Cached = Cached(origin) };
                entries[origin] = found;
            }
            entry = found;
            entry.Page ??= FaviconUrls.PageOf(target);
            entry.Declared ??= FaviconUrls.OriginOf(declared) is not null ? declared : null;
            entry.Wanted = Math.Max(entry.Wanted, size);
            if (Due(entry, Clock.RealUtc()))
            {
                Fetch(entry);
            }
        }
        return entry.Drawn(size);
    }

    /// <summary>On the dock's thread: raises <see cref="Changed"/> once for all the news since last time.</summary>
    public void Deliver() => signal.Deliver();

    /// <summary>
    /// Forgets the origins no pin shows any more, so a bar that dropped its last web pin stops
    /// holding its picture in memory. What is on disk is left alone: pinning it again should not
    /// mean fetching it again.
    /// </summary>
    public void Keep(IEnumerable<string?> targets)
    {
        var wanted = targets
            .Select(FaviconUrls.OriginOf)
            .Where(origin => origin is not null)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        lock (gate)
        {
            foreach (var origin in entries.Keys.ToList())
            {
                if (!wanted.Contains(origin) && entries[origin].Work is not { IsCompleted: false })
                {
                    entries.Remove(origin);
                }
            }
        }
    }

    /// <summary>
    /// Whether a fetch may start. Under <see cref="gate"/>. Nothing starts while one is on its way;
    /// a site that answered waits <see cref="Fresh"/>; a site that gave nothing waits
    /// <see cref="Retry"/>, so a site without a favicon is asked once and then left alone.
    /// </summary>
    internal static bool Due(Entry entry, DateTime nowUtc)
    {
        if (entry.Work is { IsCompleted: false })
        {
            return false;
        }
        // Whatever else is true, a site is not asked twice inside the retry window. That is what
        // stops a fetch which found nothing from starting another on the very next build: it leaves
        // the picture from before exactly as it was, so the kept copy's own age never moves, and
        // without this the bar would ask a site that had stopped answering on every rebuild.
        if (entry.Attempted != default && nowUtc - entry.Attempted < Retry)
        {
            return false;
        }
        if (entry.Cached is not { } kept)
        {
            return true;
        }
        // A clock moved backwards leaves a stamp in the future: read as fresh rather than as
        // infinitely stale, so a wrong clock costs no requests.
        var age = nowUtc - kept.FetchedUtc;
        return age >= (kept.Bytes is { Length: > 0 } ? Fresh : Retry);
    }

    /// <summary>The fetch on its way for an origin, or null. For tests, which wait on it.</summary>
    internal Task? Working(string origin)
    {
        lock (gate)
        {
            return entries.TryGetValue(origin, out var entry) ? entry.Work : null;
        }
    }

    /// <summary>What is kept for an origin now. For tests.</summary>
    internal FaviconCache? Kept(string origin)
    {
        lock (gate)
        {
            return entries.TryGetValue(origin, out var entry) ? entry.Cached : null;
        }
    }

    private void Fetch(Entry entry)
    {
        entry.Attempted = Clock.RealUtc();
        var cancel = entry.Stop.Token;
        var asking = source;
        var origin = entry.Origin;
        var page = entry.Page;
        var declared = entry.Declared;
        var wanted = Math.Max(entry.Wanted, 16);
        entry.Work = Task.Run(
            async () =>
            {
                try
                {
                    var found = await Look(asking, origin, page, declared, wanted, cancel).ConfigureAwait(false);
                    var result = new FaviconCache
                    {
                        Origin = origin,
                        FetchedUtc = Clock.RealUtc(),
                        Bytes = found is { } got ? Convert.ToBase64String(got.Bytes) : null,
                        Address = found?.Address,
                    };
                    lock (gate)
                    {
                        // A site that gave nothing keeps whatever was kept before: a fetch that
                        // failed must never take a picture away from a pin that had one.
                        if (found is not null || entry.Cached?.Bytes is not { Length: > 0 })
                        {
                            entry.Cached = result;
                            entry.Forget();
                        }
                    }
                    Save(result, keepPicture: found is not null);
                    if (found is not null)
                    {
                        signal.Notify();
                    }
                }
                catch (OperationCanceledException) when (cancel.IsCancellationRequested)
                {
                }
                catch (Exception error) when (error is not OutOfMemoryException)
                {
                    // The address is not written out: which sites are pinned is the person's own.
                    Console.Error.WriteLine($"Karpo: a site icon was not fetched: {error.GetType().Name}");
                }
            },
            cancel);
    }

    /// <summary>
    /// The site's page for its declarations, then each candidate in turn until one decodes. A page
    /// that cannot be read is not a failure — /favicon.ico is tried anyway, which is the address
    /// every site has whether it says so or not; and after it the icon a feed names for itself
    /// (<paramref name="declared"/>, §306), where the site has none.
    /// </summary>
    private static async Task<(byte[] Bytes, string Address)?> Look(
        FaviconSource source,
        string origin,
        string? page,
        string? declared,
        int wanted,
        CancellationToken cancel)
    {
        IReadOnlyList<FaviconLink> links = [];
        if (page is { Length: > 0 })
        {
            try
            {
                if (await source.Page(page, cancel).ConfigureAwait(false) is { Length: > 0 } html)
                {
                    links = FaviconUrls.Links(html, page);
                }
            }
            catch (Exception error) when (error is not OutOfMemoryException and not OperationCanceledException)
            {
                // No page, no declarations: the fallback below still stands.
            }
        }
        foreach (var address in FaviconUrls.Rank(links, origin, wanted).Append(declared ?? "").Where(each => each.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancel.ThrowIfCancellationRequested();
            if (!FaviconImage.Decodable(address, null))
            {
                // An SVG: nothing here can draw it, so it is not even downloaded.
                continue;
            }
            try
            {
                if (await source.Fetch(address, cancel).ConfigureAwait(false) is not { } answer
                    || !FaviconImage.Decodable(address, answer.ContentType)
                    || FaviconImage.Decode(answer.Bytes, wanted) is null)
                {
                    continue;
                }
                return (answer.Bytes, address);
            }
            catch (Exception error) when (error is not OutOfMemoryException and not OperationCanceledException)
            {
                // This candidate did not answer; the next one may.
            }
        }
        return null;
    }

    /// <summary>The kept answer for an origin, if favicons.json has one. Under <see cref="gate"/>.</summary>
    private FaviconCache? Cached(string origin)
    {
        saved ??= Load();
        return saved.FirstOrDefault(site => string.Equals(site.Origin, origin, StringComparison.OrdinalIgnoreCase));
    }

    private List<FaviconCache> Load()
    {
        if (disk.Read() is not { } json)
        {
            return [];
        }
        try
        {
            return JsonSerializer.Deserialize(json, FaviconJson.Default.FaviconFile) is { Sites: { } list }
                ? list.Where(site => site is { Origin.Length: > 0 }).ToList()
                : [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private void Save(FaviconCache result, bool keepPicture)
    {
        string json;
        lock (gate)
        {
            saved ??= Load();
            var was = saved.FirstOrDefault(site => string.Equals(site.Origin, result.Origin, StringComparison.OrdinalIgnoreCase));
            // Nothing found this time: the stamp moves on so the site is not asked again at once,
            // and the picture from before stays exactly as it was.
            if (!keepPicture && was is { Bytes.Length: > 0 })
            {
                result.Bytes = was.Bytes;
                result.Address = was.Address;
            }
            saved.RemoveAll(site => string.Equals(site.Origin, result.Origin, StringComparison.OrdinalIgnoreCase));
            saved.Insert(0, result);
            var keep = KeptSites + entries.Count;
            if (saved.Count > keep)
            {
                saved.RemoveRange(keep, saved.Count - keep);
            }
            json = JsonSerializer.Serialize(new FaviconFile { Sites = [.. saved] }, FaviconJson.Default.FaviconFile);
        }
        disk.Write(json);
    }

    /// <summary>One origin: what is kept for it, what is being fetched, and the pixels already decoded.</summary>
    internal sealed class Entry(string origin)
    {
        private readonly Dictionary<int, Pixels?> drawn = [];

        public string Origin { get; } = origin;

        public CancellationTokenSource Stop { get; } = new();

        /// <summary>A page of the site to read for its declarations: the first pin's own address.</summary>
        public string? Page { get; set; }

        /// <summary>An icon a feed names for itself, tried after the site's own (§306); null for none.</summary>
        public string? Declared { get; set; }

        /// <summary>The largest cell any bar draws this in, which is the size asked for.</summary>
        public int Wanted { get; set; }

        public FaviconCache? Cached { get; set; }

        public DateTime Attempted { get; set; }

        public Task? Work { get; set; }

        /// <summary>
        /// The kept bytes decoded for a <paramref name="size"/> px cell, once per size. Null while
        /// there is nothing kept, or when what is kept cannot be decoded — the pin keeps its letter
        /// either way, and it is remembered as null so a bad picture is not decoded on every build.
        /// </summary>
        public Pixels? Drawn(int size)
        {
            lock (drawn)
            {
                if (drawn.TryGetValue(size, out var had))
                {
                    return had;
                }
                Pixels? pixels = null;
                if (Cached?.Bytes is { Length: > 0 } text)
                {
                    try
                    {
                        pixels = FaviconImage.Decode(Convert.FromBase64String(text), size);
                    }
                    catch (FormatException)
                    {
                    }
                }
                drawn[size] = pixels;
                return pixels;
            }
        }

        /// <summary>A newer picture arrived: what was decoded from the old one is dropped.</summary>
        public void Forget()
        {
            lock (drawn)
            {
                drawn.Clear();
            }
        }
    }
}
