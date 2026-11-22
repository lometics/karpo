// The prices the markets widgets show (docs\widgets.md §246), fetched once per question (MarketAsk.Key: a
// share, a rate or a coin over a range) however many lines and faces show it, from the sources of its type
// (MarketSources.cs). The services are undocumented or free, so this file holds firmly to what an ordinary
// client may do: a cache hard enough that a bar with three markets widgets is a handful of requests an
// hour rather than anything like polling. A price is fetched again when the one interval every online
// widget follows (OnlineUpdates) has passed, never sooner than LeastMinutes; Refresh fetches at once,
// whatever the interval. Kept in markets.json beside the other widgets' files, so a price stands at once
// when Karpo starts. A failure keeps the last price, which goes quiet and says its age (stale), and is
// tried again in five minutes, or in half an hour where a source asked for a slower pace.

using System.Text.Json;
using System.Text.Json.Serialization;
using Karpo.Core;

namespace Karpo.Dock.Markets;

/// <summary>
/// Where the kept prices live: markets.json for the dock; nothing for a test, so no test reads or writes
/// the person's own folder — a held symbol is their list of holdings and none of a test's business.
/// </summary>
internal sealed class MarketDisk
{
    public required Func<string?> Read { get; init; }

    public required Action<string> Write { get; init; }

    public static MarketDisk File { get; } = new()
    {
        Read = () => IslandFiles.Read(MarketStore.FileName),
        Write = text => IslandFiles.Write(MarketStore.FileName, text),
    };

    /// <summary>Remembers nothing between runs: what a test uses.</summary>
    public static MarketDisk None { get; } = new() { Read = () => null, Write = _ => { } };
}

internal sealed class MarketStore
{
    internal const string FileName = "markets.json";

    /// <summary>Answers kept in the file that no widget shows now, for when one comes back.</summary>
    private const int Kept = 12;

    /// <summary>How soon after a failed attempt another may start.</summary>
    private static readonly TimeSpan Retry = TimeSpan.FromMinutes(5);

    /// <summary>And after a source said it was busy: long enough to be a good guest.</summary>
    private static readonly TimeSpan Backoff = TimeSpan.FromMinutes(30);

    /// <summary>
    /// The floor, in minutes: however short the online interval is set, nothing is fetched again sooner
    /// than this. The services are asked as sparingly as an ordinary client would ask them, and a bar is
    /// not a trading screen.
    /// </summary>
    internal const int LeastMinutes = 5;

    private readonly Lock gate = new();
    private readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);
    private readonly Dictionary<object, Entry> users = new(ReferenceEqualityComparer.Instance);
    private readonly IMarketSource source;
    private readonly MarketDisk disk;
    private readonly OnlineUpdates updates;
    private List<MarketCache>? saved;
    private readonly Func<MarketAsk, MarketQuote>? settled;

    /// <param name="updates">The interval every online widget follows: the dock's, or a test's own.</param>
    /// <param name="settled">§635: the answer to a question there the moment it is asked, fetched now (the samples'); none: the file's, else none until fetched.</param>
    internal MarketStore(IMarketSource source, MarketDisk? disk = null, OnlineUpdates? updates = null, Func<MarketAsk, MarketQuote>? settled = null)
    {
        this.source = source;
        this.disk = disk ?? MarketDisk.File;
        this.updates = updates ?? OnlineUpdates.Shared;
        this.settled = settled;
    }

    public static MarketStore Shared { get; } = new(MarketSources.Web);

    /// <summary>How often a price is fetched: every online widget's interval, never sooner than <see cref="LeastMinutes"/>.</summary>
    internal TimeSpan Every => updates.Every(LeastMinutes);

    /// <summary>The interval this store's staleness is judged on (<see cref="MarketReading.Refresh"/>).</summary>
    internal OnlineUpdates Updates => updates;

    /// <summary>
    /// <paramref name="user"/> shows <paramref name="ask"/>'s answer now: a line, which is woken when an
    /// answer comes; a folder for its face, woken the same; or a data source (MarketData), which keeps it
    /// fetched from the dock's start whether or not a widget of it is drawn. Nothing for nothing asked. On
    /// the dock's thread. <paramref name="wakes"/>: the widget woken when an answer comes, where the user is
    /// not itself one (§494: a widget's rate, <see cref="MarketExchange"/>); the user where it is a widget.
    /// </summary>
    public void Use(object user, MarketAsk? ask, CellIsland? wakes = null)
    {
        Release(user);
        if (ask is not { } asked)
        {
            return;
        }
        lock (gate)
        {
            if (!entries.TryGetValue(asked.Key, out var entry))
            {
                entry = new Entry(asked)
                {
                    Shown = settled is { } now
                        ? new MarketCache { FetchedUtc = Clock.RealUtc(), Key = asked.Key, Saved = MarketSaved.Of(now(asked)) }
                        : Cached(asked.Key),
                };
                entries[asked.Key] = entry;
            }
            entry.Users.Add(user);
            if ((wakes ?? user as CellIsland) is { } woken)
            {
                entry.Signal.Add(woken);
                entry.Wakes[user] = woken;
            }
            users[user] = entry;
        }
    }

    public void Release(object user)
    {
        lock (gate)
        {
            if (!users.Remove(user, out var entry))
            {
                return;
            }
            entry.Users.Remove(user);
            // A widget is woken no more once none of its users follow the question.
            if (entry.Wakes.Remove(user, out var woken) && !entry.Wakes.ContainsValue(woken))
            {
                entry.Signal.Remove(woken);
            }
            if (entry.Users.Count == 0)
            {
                // Nobody follows it any more: what is on its way is dropped.
                entry.Stop.Cancel();
                entries.Remove(entry.Ask.Key);
            }
        }
    }

    /// <summary>The last answer for the user's question, why a newer one isn't there, and whether one is coming.</summary>
    public (MarketCache? Shown, string? Problem, bool Fetching) Read(object user)
    {
        lock (gate)
        {
            return users.TryGetValue(user, out var entry)
                ? (entry.Shown, entry.Failure is { } error ? Explain(error) : null, entry.Work is { IsCompleted: false })
                : (null, null, false);
        }
    }

    /// <summary>Starts a request for the user's question when its answer is due.</summary>
    public void Poll(object user)
    {
        lock (gate)
        {
            if (users.TryGetValue(user, out var entry) && Due(entry))
            {
                Fetch(entry);
            }
        }
    }

    /// <summary>A fetch now, however recent the last one: Refresh. False when one is on its way, or a source asked to wait.</summary>
    public bool Ask(object user)
    {
        lock (gate)
        {
            if (!users.TryGetValue(user, out var entry) || entry.Work is { IsCompleted: false }
                || (entry.Busy && WallClock.Since(entry.Attempted, Clock.RealUtc()) < Backoff))
            {
                return false;
            }
            Fetch(entry);
            return true;
        }
    }

    /// <summary>On the dock's thread: wakes the user's widgets once for all the news since last time.</summary>
    internal void Deliver(object user)
    {
        StoreSignal? signal;
        lock (gate)
        {
            signal = users.TryGetValue(user, out var entry) ? entry.Signal : null;
        }
        signal?.Deliver();
    }

    private bool Due(Entry entry)
    {
        var now = Clock.RealUtc();
        if (entry.Work is { IsCompleted: false })
        {
            return false;
        }
        var since = WallClock.Since(entry.Attempted, now);
        if (entry.Busy)
        {
            return since >= Backoff;
        }
        if (entry.Failure is not null)
        {
            return since >= Retry || updates.RetryNow(true, entry.Online);
        }
        return entry.Shown is null || WallClock.Since(entry.Shown.FetchedUtc, now) >= Every;
    }

    private void Fetch(Entry entry)
    {
        entry.Attempted = Clock.RealUtc();
        entry.Online = updates.Comebacks;
        var cancel = entry.Stop.Token;
        entry.Work = Task.Run(
            async () =>
            {
                try
                {
                    var quote = await source.AskAsync(entry.Ask, cancel).ConfigureAwait(false);
                    var result = new MarketCache { FetchedUtc = Clock.RealUtc(), Key = entry.Ask.Key, Saved = MarketSaved.Of(quote) };
                    lock (gate)
                    {
                        entry.Shown = result;
                        entry.Failure = null;
                        entry.Busy = false;
                    }
                    Save(result);
                }
                catch (OperationCanceledException) when (cancel.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception error) when (error is not OutOfMemoryException)
                {
                    // What was asked is not written out: it is the person's own list of holdings.
                    Console.Error.WriteLine($"Karpo: a price was not updated: {error.GetType().Name}");
                    lock (gate)
                    {
                        entry.Failure = error;
                        entry.Busy = error is MarketBusyException;
                    }
                }
                entry.Signal.Notify();
            },
            cancel);
    }

    /// <summary>Why a price is not there, in the language of the moment.</summary>
    internal static string Explain(Exception error) => error switch
    {
        MarketBusyException => S.DockMarketsBusy,
        MarketGateException => S.DockMarketsGate,
        MarketNotQuotedException => S.DockMarketsNotQuoted,
        HttpRequestException => S.DockMarketsUnreachable,
        TaskCanceledException or TimeoutException => S.DockMarketsNoAnswer,
        InvalidDataException { Message.Length: > 0 } known => char.ToUpperInvariant(known.Message[0]) + known.Message[1..],
        _ => S.DockMarketsUnreadable,
    };

    /// <summary>The saved answer for a key, if markets.json has one. Under <see cref="gate"/>.</summary>
    private MarketCache? Cached(string key)
    {
        saved ??= Load();
        return saved.FirstOrDefault(cache => cache.Key == key);
    }

    private List<MarketCache> Load()
    {
        if (disk.Read() is not { } json)
        {
            return [];
        }
        try
        {
            return JsonSerializer.Deserialize(json, MarketJson.Default.MarketFile) is { Prices: { } list }
                ? [.. list.Where(cache => cache is { Key.Length: > 0, Quote: not null })]
                : [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>Puts an answer into markets.json, newest first, dropping the oldest.</summary>
    private void Save(MarketCache result)
    {
        string json;
        lock (gate)
        {
            saved ??= Load();
            saved.RemoveAll(cache => cache.Key == result.Key);
            saved.Insert(0, result);
            var keep = Kept + entries.Count;
            if (saved.Count > keep)
            {
                saved.RemoveRange(keep, saved.Count - keep);
            }
            json = JsonSerializer.Serialize(new MarketFile { Prices = [.. saved] }, MarketJson.Default.MarketFile);
        }
        disk.Write(json);
    }

    private sealed class Entry(MarketAsk ask)
    {
        public MarketAsk Ask { get; } = ask;

        public StoreSignal Signal { get; } = new(() => { });

        public CancellationTokenSource Stop { get; } = new();

        /// <summary>The widgets and the sources following it.</summary>
        public HashSet<object> Users { get; } = new(ReferenceEqualityComparer.Instance);

        /// <summary>The widget each user wakes when an answer comes: itself, or the widget whose rate it is (§494).</summary>
        public Dictionary<object, CellIsland> Wakes { get; } = new(ReferenceEqualityComparer.Instance);

        public MarketCache? Shown { get; set; }

        /// <summary>What the last attempt ran into, kept rather than said, so it is said in the language of the moment.</summary>
        public Exception? Failure { get; set; }

        /// <summary>A source asked for a slower pace; nothing is asked again for a while.</summary>
        public bool Busy { get; set; }

        public DateTime Attempted { get; set; }

        /// <summary>OnlineUpdates.Comebacks as it was attempted.</summary>
        public int Online { get; set; }

        public Task? Work { get; set; }
    }
}

/// <summary>markets.json.</summary>
internal sealed class MarketFile
{
    public List<MarketCache>? Prices { get; set; }
}

/// <summary>One answer in markets.json, and when it was fetched.</summary>
internal sealed class MarketCache
{
    public DateTime FetchedUtc { get; set; }

    /// <summary>What it was fetched for (<see cref="MarketAsk.Key"/>): "stock:MSFT 5y".</summary>
    public string Key { get; set; } = "";

    /// <summary>What the answer said, written out flat.</summary>
    public MarketSaved? Saved { get; set; }

    /// <summary>The answer itself; built from <see cref="Saved"/> when the cache came off the disk.</summary>
    [JsonIgnore]
    public MarketQuote? Quote
    {
        get => quote ??= Saved?.Read();
        init => quote = value;
    }

    private MarketQuote? quote;
}

/// <summary>An answer as markets.json keeps it: the numbers only, and the prices as pairs.</summary>
internal sealed class MarketSaved
{
    public string Symbol { get; set; } = "";
    public string Name { get; set; } = "";
    public string Currency { get; set; } = "";
    public double? Price { get; set; }
    public double? PreviousClose { get; set; }
    public double? ChartPreviousClose { get; set; }
    public double? ChangePercent { get; set; }
    public int? PriceHint { get; set; }
    public long? TradedUtc { get; set; }
    public int OffsetSeconds { get; set; }
    public long? OpensUtc { get; set; }
    public long? ClosesUtc { get; set; }
    public MarketRoute Route { get; set; }

    /// <summary>Seconds since the epoch and the price at it, one pair after the other: the chart's series.</summary>
    public double[]? Points { get; set; }

    /// <summary>
    /// Each point's open, high and low, three after three, 0 where the answer gave none; none at all where no
    /// point had them (and in a file from before candles, which reads as closes alone).
    /// </summary>
    public double[]? Bars { get; set; }

    public static MarketSaved Of(MarketQuote quote)
    {
        var points = new double[quote.Points.Count * 2];
        var bars = quote.Points.Any(point => point.HasBar) ? new double[quote.Points.Count * 3] : null;
        for (var index = 0; index < quote.Points.Count; index++)
        {
            var point = quote.Points[index];
            points[index * 2] = (point.At - DateTime.UnixEpoch).TotalSeconds;
            points[(index * 2) + 1] = point.Price;
            if (bars is not null && point.HasBar)
            {
                (bars[index * 3], bars[(index * 3) + 1], bars[(index * 3) + 2]) = (point.Open, point.High, point.Low);
            }
        }
        return new MarketSaved
        {
            Symbol = quote.Symbol,
            Name = quote.Name,
            Currency = quote.Currency,
            Price = quote.Price,
            PreviousClose = quote.PreviousClose,
            ChartPreviousClose = quote.ChartPreviousClose,
            ChangePercent = quote.ChangePercent,
            PriceHint = quote.PriceHint,
            TradedUtc = Seconds(quote.TradedUtc),
            OffsetSeconds = quote.OffsetSeconds,
            OpensUtc = Seconds(quote.SessionOpensUtc),
            ClosesUtc = Seconds(quote.SessionClosesUtc),
            Route = quote.Route,
            Points = points,
            Bars = bars,
        };
    }

    /// <summary>The answer kept; null for one that names nothing (a file written by hand, or broken).</summary>
    public MarketQuote? Read()
    {
        if (Symbol.Length == 0)
        {
            return null;
        }
        var points = new List<MarketPoint>();
        if (Points is { } pairs)
        {
            for (var index = 0; index + 1 < pairs.Length; index += 2)
            {
                var at = DateTime.UnixEpoch.AddSeconds(pairs[index]);
                var bar = index / 2 * 3;
                points.Add(Bars is { } bars && bar + 2 < bars.Length && bars[bar] > 0
                    ? new MarketPoint(at, pairs[index + 1], bars[bar], bars[bar + 1], bars[bar + 2])
                    : new MarketPoint(at, pairs[index + 1]));
            }
        }
        return new MarketQuote
        {
            Symbol = Symbol,
            Name = Name,
            Currency = Currency,
            Price = Price,
            PreviousClose = PreviousClose,
            ChartPreviousClose = ChartPreviousClose,
            ChangePercent = ChangePercent,
            PriceHint = PriceHint,
            TradedUtc = Moment(TradedUtc),
            OffsetSeconds = OffsetSeconds,
            SessionOpensUtc = Moment(OpensUtc),
            SessionClosesUtc = Moment(ClosesUtc),
            Route = Route,
            Points = points,
        };
    }

    private static long? Seconds(DateTime? at) => at is { } value ? (long)(value - DateTime.UnixEpoch).TotalSeconds : null;

    private static DateTime? Moment(long? seconds) => seconds is { } value ? DateTime.UnixEpoch.AddSeconds(value) : null;
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true,
    NumberHandling = JsonNumberHandling.AllowReadingFromString)]
[JsonSerializable(typeof(MarketFile))]
internal sealed partial class MarketJson : JsonSerializerContext;
