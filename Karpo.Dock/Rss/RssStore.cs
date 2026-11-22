// rss: the feeds the RSS widgets follow, each read once however many widgets show it, and the
// pictures their headlines carry (docs\widgets.md §39).
//
// A feed is the site's own address: a plain GET with Karpo's own user agent, no cookies (IslandWeb's
// handler keeps none), a timeout, a cap on the size, and the answer's ETag and Last-Modified sent
// back the next time, so a feed that has not changed costs the site a 304 and nothing more. It is
// read on the pool and never on the dock's thread; the dock hears of it through StoreSignal, as it
// hears of a price (MarketStore) or a forecast (WeatherStore).
//
// **When.** Every feed is read again at the one interval every online widget keeps (OnlineUpdates,
// docs\widgets.md §37: "other online stuff should also just update in that intervall"), asked at
// each poll, so a change to the setting applies at once and there is no interval of the widget's
// own (Every). The panel's Refresh reads at once. A failed read waits five minutes; a site that says
// it is busy (429) half an hour, Refresh or not.
//
// **What is kept.** The last read of each feed in rss.json, so a widget shows its headlines the
// moment Karpo starts and not after the first answer; with them, which items had been seen when the
// panel was last opened, so the new ones can wear a dot. A failure keeps what was read before and
// says why beside it, never an empty widget pretending there is no news.
//
// **Pictures** are asked for only when something is about to show them — the panel, a large card —
// and only from http or https addresses the feed named (Feeds.Link), capped in size, decoded by WIC
// off the dock's thread (FaviconImage.Decode) and cropped square. They are kept in memory only, a
// few dozen at most; a picture that fails is simply not drawn.
//
// **The site's icon** (§306, the person: "for the rss show the favicon of the website from which the
// news come from") is a pinned site's: the pins' store (FaviconStore) fetches it once a month and
// keeps it in favicons.json, asked of the site the feed names (else the host it is read from), with
// the icon the feed names for itself (Atom's icon, a square RSS image) after the site's own. Asked
// as the feed is drawn once read (Icon); its coming wakes the feed's widgets as a read does.

using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Karpo.Core;
using Karpo.Dock.Favicons;

namespace Karpo.Dock.Rss;

/// <summary>What is asked of a feed's site: its address, and what its last answer said, to ask whether anything changed.</summary>
internal readonly record struct RssAsk(string Address, string? ETag, string? LastModified);

/// <summary>A site's answer: the feed, or null where it has not changed since the last; and what to send back next time.</summary>
internal readonly record struct RssAnswer(Feed? Feed, string? ETag, string? LastModified);

/// <summary>Where feeds and their pictures come from. The real one asks the sites; a test hands in its own.</summary>
internal sealed class RssSource
{
    /// <summary>Asks for one feed. Throws to mean "no feed this time"; the reason is said (<see cref="RssStore.Explain"/>).</summary>
    public required Func<RssAsk, CancellationToken, Task<RssAnswer>> Ask { get; init; }

    /// <summary>A picture's bytes, or null for none. Null itself: nothing is fetched and no picture is drawn.</summary>
    public Func<string, CancellationToken, Task<byte[]?>>? Picture { get; init; }

    /// <summary>The sites themselves, as an ordinary reader asks them.</summary>
    public static RssSource Web { get; } = new() { Ask = FromSite, Picture = PictureFromSite };

    /// <summary>The most a picture may weigh.</summary>
    internal const int MostPictureBytes = 2 << 20;

    private const string Accepts = "application/rss+xml, application/atom+xml, application/rdf+xml;q=0.9, application/xml;q=0.8, text/xml;q=0.8, */*;q=0.1";

    private static async Task<RssAnswer> FromSite(RssAsk ask, CancellationToken cancel)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ask.Address);
        _ = request.Headers.TryAddWithoutValidation("Accept", Accepts);
        if (ask.ETag is { Length: > 0 } tag)
        {
            _ = request.Headers.TryAddWithoutValidation("If-None-Match", tag);
        }
        if (ask.LastModified is { Length: > 0 } modified)
        {
            _ = request.Headers.TryAddWithoutValidation("If-Modified-Since", modified);
        }
        using var response = await IslandWeb.Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancel).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotModified)
        {
            return new RssAnswer(null, ask.ETag, ask.LastModified);
        }
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            throw new RssBusyException();
        }
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound or HttpStatusCode.Gone)
        {
            throw new RssRefusedException();
        }
        response.EnsureSuccessStatusCode();
        var bytes = await Bytes(response, Feeds.MostBytes, cancel).ConfigureAwait(false)
            ?? throw new FeedException(FeedProblem.TooLong);
        var feed = Feeds.Parse(bytes, response.RequestMessage?.RequestUri ?? new Uri(ask.Address));
        var etag = response.Headers.ETag?.ToString();
        var lastModified = response.Content.Headers.LastModified?.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        return new RssAnswer(feed, etag, lastModified);
    }

    private static async Task<byte[]?> PictureFromSite(string address, CancellationToken cancel)
    {
        if (!Feeds.Opens(address))
        {
            return null;
        }
        using var request = new HttpRequestMessage(HttpMethod.Get, address);
        _ = request.Headers.TryAddWithoutValidation("Accept", "image/*");
        using var response = await IslandWeb.Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancel).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode || !FaviconImage.Decodable(address, response.Content.Headers.ContentType?.MediaType))
        {
            return null;
        }
        return await Bytes(response, MostPictureBytes, cancel).ConfigureAwait(false);
    }

    /// <summary>At most <paramref name="limit"/> bytes of an answer; null where it is longer.</summary>
    private static async Task<byte[]?> Bytes(HttpResponseMessage response, int limit, CancellationToken cancel)
    {
        if (response.Content.Headers.ContentLength is > 0 and var length && length > limit)
        {
            return null;
        }
        await using var stream = await response.Content.ReadAsStreamAsync(cancel).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancel).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > limit)
            {
                return null;
            }
            buffer.Write(chunk, 0, read);
        }
        return buffer.ToArray();
    }
}

/// <summary>The site asked for the requests to slow down.</summary>
internal sealed class RssBusyException : Exception;

/// <summary>The site answered that the feed is not there, or not for Karpo.</summary>
internal sealed class RssRefusedException : Exception;

/// <summary>
/// Where the kept feeds live. The dock's is rss.json beside the other widgets' files; a test hands
/// in one that keeps nothing, so no test reads or writes the person's own folder.
/// </summary>
internal sealed class RssDisk
{
    public required Func<string?> Read { get; init; }

    public required Action<string> Write { get; init; }

    public static RssDisk File { get; } = new()
    {
        Read = () => IslandFiles.Read(RssStore.FileName),
        Write = text => IslandFiles.Write(RssStore.FileName, text),
    };

    /// <summary>Remembers nothing between runs: what a test uses.</summary>
    public static RssDisk None { get; } = new() { Read = () => null, Write = _ => { } };
}

/// <summary>What a widget reads of its feed: the last read, why a newer one is not there, whether one is coming, and what is new.</summary>
internal readonly record struct RssRead(Feed? Feed, DateTime? FetchedUtc, string? Problem, bool Fetching, IReadOnlySet<string> Seen)
{
    /// <summary>Whether <paramref name="item"/> came after the panel was last opened.</summary>
    public bool IsNew(FeedItem item) => Feed is not null && !Seen.Contains(item.Key);

    /// <summary>How many of the items came after the panel was last opened.</summary>
    public int NewCount => Feed?.Items.Count(IsNew) ?? 0;
}

internal sealed class RssStore
{
    internal const string FileName = "rss.json";

    /// <summary>Feeds kept in the file that no widget shows now, for when one comes back.</summary>
    private const int KeptFeeds = 8;

    /// <summary>The most pictures kept in memory, across every feed.</summary>
    private const int KeptPictures = 64;

    /// <summary>The side, in px, a picture is decoded to before it is cropped square: a large card's at 200 %.</summary>
    internal const int PictureSide = 320;

    /// <summary>How soon after a failed attempt another may start.</summary>
    private static readonly TimeSpan Retry = TimeSpan.FromMinutes(5);

    /// <summary>And after the site said it was busy: long enough to be a good guest.</summary>
    private static readonly TimeSpan Backoff = TimeSpan.FromMinutes(30);

    /// <summary>
    /// The feeds' floor, in minutes: however short the online interval is set, a feed is not read
    /// again sooner than this. A site serves every reader of its feed, and one that changes more
    /// often than this is no news ticker on a taskbar; it is the least the setting offers anyway.
    /// </summary>
    internal const int LeastMinutes = 5;

    /// <summary>
    /// How often a feed is read again: every online widget's interval (<see cref="OnlineUpdates"/>),
    /// never sooner than the feeds' floor. Asked afresh at each poll, so a change to the setting is
    /// followed at the next tick; no widget has an interval of its own.
    /// </summary>
    internal TimeSpan Every => updates.Every(LeastMinutes);

    private readonly Lock gate = new();
    private readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);
    private readonly Dictionary<object, Entry> users = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<string, Pixels?> pictures = new(StringComparer.Ordinal);
    private readonly HashSet<string> fetchingPictures = new(StringComparer.Ordinal);
    private readonly RssSource source;
    private readonly RssDisk disk;
    private readonly Func<DateTime> now;
    private readonly OnlineUpdates updates;
    private readonly Func<FaviconStore?> sites;
    private List<RssCache>? saved;
    private readonly Func<string, RssCache>? settled;

    /// <param name="updates">The interval every online widget follows: the dock's, or a test's own.</param>
    /// <param name="now">The clock a read's age is told by; a test hands in its own.</param>
    /// <param name="sites">Where a feed's site's own icon comes from (§306): the pins' store, the dock's, which is none in a test; or a test's own.</param>
    /// <param name="settled">§635: a feed's read there the moment it is asked for, by its address (the samples'); none: the file's, else none until read.</param>
    internal RssStore(RssSource source, RssDisk? disk = null, Func<DateTime>? now = null, OnlineUpdates? updates = null, Func<FaviconStore?>? sites = null, Func<string, RssCache>? settled = null)
    {
        this.source = source;
        this.disk = disk ?? RssDisk.File;
        this.now = now ?? (() => Clock.RealUtc());
        this.updates = updates ?? OnlineUpdates.Shared;
        this.sites = sites ?? (() => FaviconStore.Current);
        this.settled = settled;
    }

    public static RssStore Shared { get; } = new(RssSource.Web);

    /// <summary>On the dock's thread, once a read has come back or failed, or a picture has arrived.</summary>
    public event Action? Changed;

    /// <summary>What a feed is read once for: its address, as the options keep it.</summary>
    public static string KeyOf(RssOptions options) => (options.Address ?? "").Trim();

    /// <summary>
    /// <paramref name="island"/> shows <paramref name="options"/>' feed now: a widget, woken when an
    /// answer comes, or the feed's data source (RssData), which keeps it read from the dock's start.
    /// On the dock's thread.
    /// </summary>
    public void Use(object island, RssOptions options)
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
                entry = new Entry(key, () => Changed?.Invoke()) { Shown = settled?.Invoke(key) ?? Cached(key) };
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
                // Nobody follows this feed any more: what is on its way is dropped.
                entry.Stop.Cancel();
                entries.Remove(entry.Key);
            }
        }
    }

    /// <summary>The last read of the island's feed, why a newer one isn't there, whether one is coming, and what has been seen.</summary>
    public RssRead Read(object island)
    {
        lock (gate)
        {
            return users.TryGetValue(island, out var entry)
                ? new RssRead(entry.Shown?.Feed, entry.Shown?.FetchedUtc, entry.Problem, entry.Work is { IsCompleted: false }, entry.Seen)
                : new RssRead(null, null, null, false, new HashSet<string>());
        }
    }

    /// <summary>Starts a read of the island's feed when it is due.</summary>
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

    /// <summary>A read now, however recent the last one: the panel's Refresh and the menu's. False when one is on its way.</summary>
    public bool Ask(object island)
    {
        lock (gate)
        {
            if (!users.TryGetValue(island, out var entry) || entry.Work is { IsCompleted: false })
            {
                return false;
            }
            // Even asked for by hand, a site that said to slow down waits its turn.
            if (entry.Busy && WallClock.Since(entry.Attempted, now()) < Backoff)
            {
                return false;
            }
            Fetch(entry);
            return true;
        }
    }

    /// <summary>
    /// The panel was opened on the island's feed: everything it holds now has been seen, and the dot
    /// on the bar, the cards and the lines goes until something newer comes.
    /// </summary>
    public void Seen(object island)
    {
        RssCache? kept;
        lock (gate)
        {
            if (!users.TryGetValue(island, out var entry) || entry.Shown?.Feed is not { } feed)
            {
                return;
            }
            var keys = new HashSet<string>(feed.Items.Select(item => item.Key), StringComparer.Ordinal);
            if (keys.SetEquals(entry.Seen))
            {
                return;
            }
            entry.Seen = keys;
            kept = entry.Shown with { Seen = [.. keys] };
            entry.Shown = kept;
        }
        Save(kept);
        Notify(island);
    }

    /// <summary>On the dock's thread: raises <see cref="Changed"/> once for all the news since last time.</summary>
    internal void Deliver(object island)
    {
        StoreSignal? signal;
        lock (gate)
        {
            signal = users.TryGetValue(island, out var entry) ? entry.Signal : null;
        }
        signal?.Deliver();
    }

    private void Notify(object island)
    {
        StoreSignal? signal;
        lock (gate)
        {
            signal = users.TryGetValue(island, out var entry) ? entry.Signal : null;
        }
        signal?.Notify();
    }

    /// <summary>
    /// The picture at <paramref name="address"/>, cropped square, for <paramref name="island"/>'s feed:
    /// null until it has arrived — and asked for now, once, the first time anything wants it — and
    /// null for good where it cannot be had. Its arrival wakes the island.
    /// </summary>
    public Pixels? Picture(object island, string address)
    {
        if (!Feeds.Opens(address) || source.Picture is not { } fetch)
        {
            return null;
        }
        StoreSignal? signal;
        lock (gate)
        {
            if (pictures.TryGetValue(address, out var kept))
            {
                return kept;
            }
            if (!fetchingPictures.Add(address) || !users.TryGetValue(island, out var entry))
            {
                return null;
            }
            signal = entry.Signal;
        }
        _ = Task.Run(async () =>
        {
            Pixels? picture = null;
            try
            {
                using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                if (await fetch(address, stop.Token).ConfigureAwait(false) is { Length: > 0 } bytes)
                {
                    picture = Square(FaviconImage.Decode(bytes, PictureSide));
                }
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                Console.Error.WriteLine($"Karpo: a headline's picture was not read: {error.GetType().Name}");
            }
            lock (gate)
            {
                if (pictures.Count >= KeptPictures)
                {
                    pictures.Clear();
                }
                pictures[address] = picture;
                fetchingPictures.Remove(address);
            }
            if (picture is not null)
            {
                signal.Notify();
            }
        });
        return null;
    }

    /// <summary>
    /// The icon of <paramref name="island"/>'s feed's site at <paramref name="px"/> (§306): its
    /// favicon, fetched and kept as a pinned site's is (FaviconStore: once a month, favicons.json),
    /// asked of the site the feed names, else of the feed's own host, with the icon the feed names
    /// for itself tried after the site's own. Asked as the feed is drawn once it is read, so a feed
    /// costs its site nothing more until then. Null until it has come and where there is none; its
    /// coming wakes the island as its feed's does.
    /// </summary>
    public Pixels? Icon(object island, int px)
    {
        if (sites() is not { } store || px <= 0)
        {
            return null;
        }
        string site;
        string declared;
        Entry entry;
        lock (gate)
        {
            if (!users.TryGetValue(island, out entry!) || entry.Shown?.Feed is not { } feed)
            {
                return null;
            }
            (site, declared) = (SiteOf(entry.Key, feed), feed.Icon);
        }
        if (store.Picture(site, px, declared) is { } pixels)
        {
            return pixels;
        }
        if (FaviconUrls.OriginOf(site) is { } origin && store.Working(origin) is { IsCompleted: false } work)
        {
            lock (gate)
            {
                if (entry.IconWork == work)
                {
                    return null;
                }
                entry.IconWork = work;
            }
            _ = work.ContinueWith(_ => Troubles.Guard("RSS: a site's icon came", entry.Signal.Notify), TaskScheduler.Default);
        }
        return null;
    }

    /// <summary>The sites whose icons the feeds read now show, for the pins' store to keep (Dock.KeepFavicons).</summary>
    internal IReadOnlyList<string> Sites()
    {
        lock (gate)
        {
            return [.. entries.Values.Where(entry => entry.Shown?.Feed is not null).Select(entry => SiteOf(entry.Key, entry.Shown!.Feed!))];
        }
    }

    /// <summary>Whose icon a feed wears: the site it names, else the host it is read from.</summary>
    internal static string SiteOf(string address, Feed feed) => Feeds.Opens(feed.Site) ? feed.Site : address;

    /// <summary>
    /// <paramref name="picture"/> cut to the square in its middle, as a news card crops a photo to
    /// its box rather than squeezing it; null stays null.
    /// </summary>
    internal static Pixels? Square(Pixels? picture)
    {
        if (picture is not { Width: > 0, Height: > 0 } whole || whole.Width == whole.Height)
        {
            return picture;
        }
        var side = Math.Min(whole.Width, whole.Height);
        var left = (whole.Width - side) / 2;
        var top = (whole.Height - side) / 2;
        var data = new byte[side * side * 4];
        for (var row = 0; row < side; row++)
        {
            Buffer.BlockCopy(whole.Data, (((top + row) * whole.Width) + left) * 4, data, row * side * 4, side * 4);
        }
        return new Pixels { Data = data, Width = side, Height = side };
    }

    private bool Due(Entry entry)
    {
        if (entry.Work is { IsCompleted: false })
        {
            return false;
        }
        var at = now();
        var since = WallClock.Since(entry.Attempted, at);
        if (entry.Busy)
        {
            return since >= Backoff;
        }
        if (entry.Failure is not null)
        {
            return since >= Retry || updates.RetryNow(true, entry.Online);
        }
        return entry.Shown is null || WallClock.Since(entry.Shown.FetchedUtc, at) >= Every;
    }

    private void Fetch(Entry entry)
    {
        entry.Attempted = now();
        entry.Online = updates.Comebacks;
        var cancel = entry.Stop.Token;
        var asking = source;
        var ask = new RssAsk(entry.Key, entry.Shown?.ETag, entry.Shown?.LastModified);
        entry.Work = Task.Run(
            async () =>
            {
                RssCache? result = null;
                try
                {
                    var answer = await asking.Ask(ask, cancel).ConfigureAwait(false);
                    lock (gate)
                    {
                        var before = entry.Shown;
                        var feed = answer.Feed ?? before?.Feed;
                        if (feed is not null)
                        {
                            // The first read of a feed has nothing new in it: what it holds is where the
                            // dots start from.
                            if (before is null && entry.Seen.Count == 0)
                            {
                                entry.Seen = new HashSet<string>(feed.Items.Select(item => item.Key), StringComparer.Ordinal);
                            }
                            result = new RssCache
                            {
                                Address = entry.Key,
                                FetchedUtc = now(),
                                ETag = answer.ETag,
                                LastModified = answer.LastModified,
                                Saved = answer.Feed is { } fresh ? RssSaved.Of(fresh) : before?.Saved,
                                Seen = [.. entry.Seen],
                            };
                            entry.Shown = result;
                        }
                        entry.Failure = null;
                        entry.Busy = false;
                    }
                }
                catch (OperationCanceledException) when (cancel.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception error) when (error is not OutOfMemoryException)
                {
                    // The address is not written out: which feeds somebody reads is their business.
                    Console.Error.WriteLine($"Karpo: a feed was not updated: {error.GetType().Name}");
                    lock (gate)
                    {
                        entry.Failure = error;
                        entry.Busy = error is RssBusyException;
                    }
                }
                if (result is not null)
                {
                    Save(result);
                }
                entry.Signal.Notify();
            },
            cancel);
    }

    /// <summary>Why a read failed, in the words the widget and its panel say it, in the language of the moment.</summary>
    internal static string Explain(Exception error) => error switch
    {
        RssBusyException => S.DockMarketsBusy,
        RssRefusedException => S.DockRssRefused,
        FeedException { Problem: FeedProblem.TooLong } => S.DockRssTooLong,
        FeedException { Problem: FeedProblem.NotAFeed } => S.DockRssNotAFeed,
        FeedException => S.DockRssUnreadable,
        HttpRequestException => S.DockRssUnreachable,
        TaskCanceledException or TimeoutException => S.DockRssNoAnswer,
        _ => S.DockRssUnreadable,
    };

    /// <summary>The kept read of a feed, if rss.json has one. Under <see cref="gate"/>.</summary>
    private RssCache? Cached(string address)
    {
        saved ??= Load();
        return saved.FirstOrDefault(cache => string.Equals(cache.Address, address, StringComparison.Ordinal));
    }

    private List<RssCache> Load()
    {
        if (disk.Read() is not { } json)
        {
            return [];
        }
        try
        {
            return JsonSerializer.Deserialize(json, RssJson.Default.RssFile) is { Feeds: { } list }
                ? list.Where(cache => cache is { Address.Length: > 0, Saved: not null }).ToList()
                : [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>Puts a feed into rss.json, newest first, dropping the oldest feeds nobody shows.</summary>
    private void Save(RssCache result)
    {
        string json;
        lock (gate)
        {
            saved ??= Load();
            saved.RemoveAll(cache => string.Equals(cache.Address, result.Address, StringComparison.Ordinal));
            saved.Insert(0, result);
            var keep = KeptFeeds + entries.Count;
            if (saved.Count > keep)
            {
                saved.RemoveRange(keep, saved.Count - keep);
            }
            json = JsonSerializer.Serialize(new RssFile { Feeds = [.. saved] }, RssJson.Default.RssFile);
        }
        disk.Write(json);
    }

    private sealed class Entry(string key, Action changed)
    {
        public string Key { get; } = key;

        public StoreSignal Signal { get; } = new(changed);

        public CancellationTokenSource Stop { get; } = new();

        /// <summary>The widgets and the sources following this feed.</summary>
        public HashSet<object> Users { get; } = new(ReferenceEqualityComparer.Instance);

        private RssCache? shown;

        public RssCache? Shown
        {
            get => shown;
            set
            {
                shown = value;
                if (value?.Seen is { } seen && Seen.Count == 0)
                {
                    Seen = new HashSet<string>(seen, StringComparer.Ordinal);
                }
            }
        }

        /// <summary>The items the panel had shown when it was last opened, by key.</summary>
        public HashSet<string> Seen { get; set; } = new(StringComparer.Ordinal);

        /// <summary>What the last attempt ran into, kept rather than said, so it is said in the language of the moment.</summary>
        public Exception? Failure { get; set; }

        public string? Problem => Failure is { } error ? Explain(error) : null;

        /// <summary>The site asked for a slower pace; nothing is asked again for a while.</summary>
        public bool Busy { get; set; }

        public DateTime Attempted { get; set; }

        /// <summary>OnlineUpdates.Comebacks as it was attempted.</summary>
        public int Online { get; set; }

        public Task? Work { get; set; }

        /// <summary>The fetch of its site's icon it waits on to wake its islands, once (§306).</summary>
        public Task? IconWork { get; set; }
    }
}

/// <summary>rss.json.</summary>
internal sealed class RssFile
{
    public List<RssCache>? Feeds { get; set; }
}

/// <summary>One feed's last read in rss.json, when it was read, and what to send its site next time.</summary>
internal sealed record RssCache
{
    public string Address { get; init; } = "";

    public DateTime FetchedUtc { get; init; }

    public string? ETag { get; init; }

    public string? LastModified { get; init; }

    public RssSaved? Saved { get; init; }

    /// <summary>The keys of the items seen when the panel was last opened.</summary>
    public List<string>? Seen { get; init; }

    /// <summary>The feed itself, built from <see cref="Saved"/>.</summary>
    [JsonIgnore]
    public Feed? Feed => Saved?.Read();
}

/// <summary>A feed as rss.json keeps it: its words and links, and nothing it did not say.</summary>
internal sealed class RssSaved
{
    public string Title { get; set; } = "";

    public string Site { get; set; } = "";

    /// <summary>The icon the feed names for itself (§306); null for none, and left out then.</summary>
    public string? Icon { get; set; }

    public List<RssSavedItem>? Items { get; set; }

    private Feed? read;

    public static RssSaved Of(Feed feed) => new()
    {
        Title = feed.Title,
        Site = feed.Site,
        Icon = feed.Icon.Length > 0 ? feed.Icon : null,
        Items = [.. feed.Items.Select(item => new RssSavedItem
        {
            Title = item.Title,
            Link = item.Link,
            Published = item.Published,
            Thumbnail = item.Thumbnail,
            Key = item.Key,
        })],
        read = feed,
    };

    /// <summary>
    /// The feed again, held to what a feed from the site is held to: a file is somebody's to edit, so
    /// its titles are plain words again and its links http or https again before anything shows them.
    /// </summary>
    public Feed Read() => read ??= new Feed
    {
        Title = Feeds.Plain(Title, RssOptions.MostTitle),
        Site = Feeds.Link(Site, null),
        Icon = Feeds.Link(Icon, null),
        Items = [.. (Items ?? [])
            .Select(item => new FeedItem
            {
                Title = Feeds.Plain(item.Title, Feeds.MostTitle),
                Link = Feeds.Link(item.Link, null),
                Published = item.Published,
                Thumbnail = Feeds.Link(item.Thumbnail, null),
                Key = item.Key ?? "",
            })
            .Where(item => item.Title.Length > 0)
            .Take(Feeds.MostItems)],
    };
}

/// <summary>One item in rss.json.</summary>
internal sealed class RssSavedItem
{
    public string Title { get; set; } = "";

    public string Link { get; set; } = "";

    public DateTimeOffset? Published { get; set; }

    public string Thumbnail { get; set; } = "";

    public string? Key { get; set; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(RssFile))]
internal sealed partial class RssJson : JsonSerializerContext;

/// <summary>A feed's headlines, kept read from the dock's start (RssStore).</summary>
internal sealed class RssData : DataSource
{
    private readonly RssOptions options;
    private readonly RssStore store;

    public RssData(RssOptions options, RssStore? store = null)
        : base(KeyOf(options) ?? throw new ArgumentException("No feed is set.", nameof(options)))
    {
        this.options = new RssOptions { Address = options.Address };
        this.store = store ?? RssStore.Shared;
    }

    /// <summary>The source of a widget set to <paramref name="options"/>: the feed's address; null where none is set.</summary>
    public static SourceKey? KeyOf(RssOptions? options) =>
        options is { IsSet: true } set ? new SourceKey("rss", RssStore.KeyOf(set)) : null;

    /// <summary>A minute: the store decides whether the feed is due.</summary>
    public override int IntervalMs => 60_000;

    protected internal override void Start()
    {
        store.Use(this, options);
        store.Poll(this);
    }

    protected override bool Gather(long now)
    {
        store.Poll(this);
        return false;
    }

    public override void Dispose() => store.Release(this);
}
