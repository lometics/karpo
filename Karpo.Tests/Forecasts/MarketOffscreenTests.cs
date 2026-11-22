// The markets widget's made-up answers and stores, for every test that draws one: nothing is fetched,
// nothing is written, and no window appears on the person's desktop. The markets line and face are
// MarketPresetTests' (docs\widgets.md §98, §246).
//
// Nothing here reads or writes markets.json: the store is built on MarketDisk.None or a file of its
// own, so the person's own holdings are never touched by a test run.

using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Karpo.Core;
using Karpo.Dock;
using Karpo.Dock.Graphs;
using Karpo.Dock.Native;
using Karpo.Dock.QuickSettings;
using Karpo.Dock.QuickSettings.Controls;
using Karpo.Dock.Markets;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.DirectX.DirectX;
using static TerraFX.Interop.Windows.Windows;
using Color = Windows.UI.Color;
using IDispatcherQueueController = TerraFX.Interop.WinRT.IDispatcherQueueController;

namespace Karpo.Tests.Forecasts;

public sealed unsafe class MarketOffscreenTests
{
    // A class of these tests' own, so another test's window procedure can't have the panel's messages.
    private const string ClassName = "karpo.stock.test";

    private static readonly RECT Work = new() { left = 0, top = 0, right = 1920, bottom = 1040 };
    private static readonly POINT Anchor = new() { x = 1500, y = 1040 };

    /// <summary>The session in every fixture, in the exchange's own hours.</summary>
    private const double OpensHour = 9.5;
    private const double ClosesHour = 16;

    /// <summary>
    /// The clock the panel's tests freeze, so a sheet is the same every run. The bar's pictures do
    /// not freeze it: their fixture is laid out from whatever the clock says, and nothing the cell
    /// draws depends on the time.
    /// </summary>
    private static readonly DateTime Fixed = new(2026, 9, 23, 14, 20, 0, DateTimeKind.Local);

    private static CultureInfo Invariant => CultureInfo.InvariantCulture;

    private static string Pictures
    {
        get
        {
            var folder = Environment.GetEnvironmentVariable("KARPO_PNG_DIR") is { Length: > 0 } named
                ? named
                : Path.Combine(Path.GetTempPath(), "karpo-tests", "pictures");
            Directory.CreateDirectory(folder);
            return folder;
        }
    }

    internal static Look LookFor(bool dark, float textScale = 1f) => new(
        Glass: false,
        Dark: dark,
        Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4),
        AccentLight2: Color.FromArgb(255, 0x99, 0xEB, 0xFF),
        AccentDark1: Color.FromArgb(255, 0x00, 0x5F, 0xB8))
    { TextScale = textScale };

    private static Look HighContrast() => new(
        Glass: false,
        Dark: true,
        Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4),
        HighContrast: true,
        HcWindow: Color.FromArgb(255, 0, 0, 0),
        HcText: Color.FromArgb(255, 255, 255, 255),
        HcHighlight: Color.FromArgb(255, 0x1A, 0xEB, 0xFF),
        HcHighlightText: Color.FromArgb(255, 0, 0, 0));

    // --- The fixture ------------------------------------------------------------------

    /// <summary>A widget of <paramref name="symbol"/> over a day, which is what these pictures and answers are of.</summary>
    internal static MarketOptions Held(string symbol, string label = "") => new()
    {
        Symbol = symbol,
        Label = label,
        Range = MarketRange.Day,
    };

    /// <summary>A stock line set to <paramref name="options"/>.</summary>
    internal static CellItem Widget(string id, MarketOptions? options)
    {
        var line = ListKind.Markets.New();
        line.Id = id;
        line.Market = options;
        return line;
    }

    /// <summary>
    /// The exchange's clock is set from this PC's, so its own local time now is
    /// <paramref name="nowHour"/> whatever zone the test runs in. Everything else — the session,
    /// the trades, the mark — is then at a fixed place on the axis, and the pictures are the same
    /// in Zürich and in Auckland.
    /// </summary>
    private static int OffsetFor(DateTime utcNow, double nowHour)
    {
        var here = (utcNow - utcNow.Date).TotalHours;
        var shift = nowHour - here;
        while (shift <= -12)
        {
            shift += 24;
        }
        while (shift > 12)
        {
            shift -= 24;
        }
        return (int)Math.Round(shift * 3600);
    }

    /// <summary>
    /// An answer shaped as Yahoo's chart endpoint's. <paramref name="nowHour"/> is what the
    /// exchange's own clock says now, <paramref name="tradedHour"/> when the last trade was, and
    /// <paramref name="daysBack"/> how many exchange days ago the session it belonged to was;
    /// <paramref name="wander"/> how far the day wanders, as a share of a share's (a currency pair's is a thousandth).
    /// </summary>
    internal static string Answer(
        string symbol = "AAPL",
        string name = "Apple Inc.",
        string currency = "USD",
        double price = 336.16,
        double percent = -1.057,
        double previous = 339.75,
        double nowHour = 12.0,
        double tradedHour = 12.0,
        int daysBack = 0,
        double wander = 1)
    {
        var utcNow = Clock.Now().ToUniversalTime();
        var offset = OffsetFor(utcNow, nowHour);
        var midnight = utcNow.AddSeconds(offset).Date.AddDays(-daysBack).AddSeconds(-offset);
        var opens = Seconds(midnight.AddHours(OpensHour));
        var closes = Seconds(midnight.AddHours(ClosesHour));
        var traded = Seconds(midnight.AddHours(tradedHour));

        // Five minutes apart from the bell to the last trade, wandering as a day does, with two
        // slots that saw no trade at all: the reading leaves those out rather than drawing a zero.
        var times = new StringBuilder();
        var closesAt = new StringBuilder();
        var steps = (int)Math.Round((tradedHour - OpensHour) * 12);
        for (var step = 0; step <= steps; step++)
        {
            var comma = step == 0 ? "" : ",";
            times.Append(comma).Append((opens + (step * 300)).ToString(Invariant));
            var drift = (Math.Sin(step / 9.0) * 1.9 + Math.Sin(step / 2.7) * 0.6) * wander;
            var value = previous + ((price - previous) * step / Math.Max(1, steps)) + drift;
            closesAt.Append(comma).Append(step is 11 or 29 ? "null" : value.ToString(wander == 1 ? "0.###" : "0.######", Invariant));
        }

        return $$"""
            {
              "chart": {
                "result": [
                  {
                    "meta": {
                      "currency": "{{currency}}", "symbol": "{{symbol}}", "exchangeName": "NMS",
                      "regularMarketTime": {{traded}}, "gmtoffset": {{offset}},
                      "timezone": "EDT", "exchangeTimezoneName": "America/New_York",
                      "regularMarketPrice": {{price.ToString("0.####", Invariant)}},
                      "regularMarketChangePercent": {{percent.ToString("0.####", Invariant)}},
                      "previousClose": {{previous.ToString("0.####", Invariant)}},
                      "chartPreviousClose": {{previous.ToString("0.####", Invariant)}},
                      "longName": "{{name}}", "shortName": "{{name}}",
                      "currentTradingPeriod": {
                        "pre": { "start": {{opens - 19800}}, "end": {{opens}} },
                        "regular": { "start": {{opens}}, "end": {{closes}} },
                        "post": { "start": {{closes}}, "end": {{closes + 14400}} }
                      }
                    },
                    "timestamp": [{{times}}],
                    "indicators": { "quote": [ { "close": [{{closesAt}}] } ] }
                  }
                ],
                "error": null
              }
            }
            """;
    }

    private static long Seconds(DateTime utc) => (long)(utc - DateTime.UnixEpoch).TotalSeconds;

    /// <summary>A price kept as the store would keep it, fetched <paramref name="agoMinutes"/> ago.</summary>
    internal static MarketCache Kept(string json, int agoMinutes = 3)
    {
        var quote = MarketQuotes.Parse(json);
        return new MarketCache
        {
            FetchedUtc = Clock.Now().ToUniversalTime().AddMinutes(-agoMinutes),
            Key = quote.Symbol,
            Quote = quote,
        };
    }

    /// <summary>A store that answers from a table of saved answers, by the price service's symbol, and never reaches the network or the disk.</summary>
    internal static MarketStore Store(Dictionary<string, string> answers) =>
        new(
            new MarketAnswers((ask, _) => answers.TryGetValue(ask.Symbol, out var json)
                ? Task.FromResult(MarketQuotes.Parse(json))
                : throw new InvalidDataException("nothing for that symbol")),
            MarketDisk.None);

    /// <summary>
    /// The store's key for a test's: the price service's symbol, and after it the range's word where
    /// it is not a day — "MSFT", "MSFT 5y", "EURCHF=X", "BTC-USD 1mo".
    /// </summary>
    internal static string KeyOf(string said)
    {
        var parts = said.Split(' ');
        var word = parts.Length > 1 ? parts[1] : "1d";
        var options = new MarketOptions { Symbol = MarketSymbols.Canonical(parts[0]) };
        options.Range = Enum.GetValues<MarketRange>().First(range => MarketAsk.Of(new MarketOptions { Symbol = "X", Range = range, RangeYears = 3 })!.Value.Word == word);
        return MarketAsk.Of(options)!.Value.Key;
    }

    /// <summary>
    /// A store that already holds a price for each of <paramref name="answers"/> — by the price service's
    /// symbol for a day ("MSFT", "EURCHF=X") and the symbol and its range's word for another ("MSFT 5y")
    /// (<see cref="KeyOf"/>) — kept <paramref name="agoMinutes"/> before the clock the test froze, and whose
    /// service never answers. What a widget draws is that price, there the moment it asks — nothing is
    /// fetched on the pool, so nothing has to be waited for. For pictures and measurements that must come
    /// out the same on every run, at any time of day.
    /// </summary>
    internal static MarketStore Settled(Dictionary<string, string> answers, int agoMinutes = 3)
    {
        List<MarketCache> kept = [.. answers.Select(answer => new MarketCache
        {
            FetchedUtc = Clock.Now().ToUniversalTime().AddMinutes(-agoMinutes),
            Key = KeyOf(answer.Key),
            Saved = MarketSaved.Of(MarketQuotes.Parse(answer.Value)),
        })];
        var file = JsonSerializer.Serialize(new MarketFile { Prices = kept }, MarketJson.Default.MarketFile);
        return new(
            new MarketAnswers((_, cancel) => new TaskCompletionSource<MarketQuote>().Task.WaitAsync(cancel)),
            new MarketDisk { Read = () => file, Write = _ => { } });
    }

    /// <summary>A store whose answer never comes: the widget is loading for as long as the test wants.</summary>
    internal static MarketStore Waiting() =>
        new(new MarketAnswers((_, cancel) => new TaskCompletionSource<MarketQuote>().Task.WaitAsync(cancel)), MarketDisk.None);

    /// <summary>A store that cannot reach its service.</summary>
    internal static MarketStore Broken() =>
        new(new MarketAnswers((_, _) => throw new HttpRequestException("no route")), MarketDisk.None);

    /// <summary>
    /// The Markets preset's folder (Presets.MarketId) cut to its first line: its face over one share's line, for a test of one
    /// line. The preset holds four shares since it took the Watchlist in (§648).
    /// </summary>
    internal static BarItem OneLineFolder(PresetMachine machine)
    {
        var holder = Presets.Folder(Presets.Find(Presets.MarketId)!, machine);
        holder.Folder!.Lists = [[holder.Folder.Lists[0][0]]];
        return holder;
    }

    /// <summary>Waits for the fetch the widget started, then lets the widget take what came back.</summary>
    internal static MarketIsland Made(Gfx gfx, Config config, MarketStore store, string id, MarketOptions? options, bool answers = true)
    {
        var island = new MarketIsland(Config.IslandKey(null, id), Widget(id, options), gfx, config, store);
        // The fetch the widget started runs on the pool; waiting for it to come back is what stands
        // in for the dock's own tick. A store whose answer never comes is not waited on at all — that
        // is the point of it.
        if (answers)
        {
            Assert.True(Patience.Until(() => !island.Quote.Fetching), "the fetch never came back");
        }
        _ = island.Refresh(gfx);
        return island;
    }
}

/// <summary>A test's source: what <paramref name="ask"/> answers, and nothing from the network.</summary>
internal sealed class MarketAnswers(Func<MarketAsk, CancellationToken, Task<MarketQuote>> ask) : IMarketSource
{
    public Task<MarketQuote> AskAsync(MarketAsk asked, CancellationToken cancel) => ask(asked, cancel);
}
