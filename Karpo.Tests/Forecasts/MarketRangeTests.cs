// stocks: how far back a widget's chart goes (backlog item 32). What each range asks Yahoo's
// chart endpoint for, how the change follows the range, how the answer is read and kept, and that
// the setting survives the file. No network: the answers are made here, in the endpoint's shape.

using System.Globalization;
using System.Text;
using System.Text.Json;
using Karpo.Core;
using Karpo.Dock.Markets;
using Karpo.Tests.Core;

namespace Karpo.Tests.Forecasts;

public sealed class MarketRangeTests
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private static MarketOptions Share(MarketRange range, int years = 3, string symbol = "MSFT") =>
        new() { Symbol = symbol, Range = range, RangeYears = years };

    private static MarketAsk Ask(MarketOptions options) => MarketAsk.Of(options)!.Value;

    // --- The request ------------------------------------------------------------------------

    /// <summary>
    /// Each range is one of the endpoint's own ranges at the finest interval it keeps that far back
    /// that still draws a few hundred points: minutes for a day and five days, an hour for a month,
    /// days for six months to a year, weeks for five years and months for all of it.
    /// </summary>
    [Theory]
    [InlineData(MarketRange.Day, "1d", "5m")]
    [InlineData(MarketRange.FiveDays, "5d", "15m")]
    [InlineData(MarketRange.Month, "1mo", "60m")]
    [InlineData(MarketRange.SixMonths, "6mo", "1d")]
    [InlineData(MarketRange.YearToDate, "ytd", "1d")]
    [InlineData(MarketRange.Year, "1y", "1d")]
    [InlineData(MarketRange.FiveYears, "5y", "1wk")]
    [InlineData(MarketRange.Max, "max", "1mo")]
    public void EveryRangeAsksForItsOwnRangeAndInterval(MarketRange range, string word, string interval)
    {
        var ask = Ask(Share(range));
        var url = ask.YahooUrl(ask.Symbol, new DateTime(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc));
        Assert.StartsWith("https://query1.finance.yahoo.com/v8/finance/chart/MSFT?", url, StringComparison.Ordinal);
        Assert.EndsWith($"?range={word}&interval={interval}", url, StringComparison.Ordinal);
        Assert.Equal(word, ask.Word);
    }

    /// <summary>
    /// The last N years is no range of the endpoint's, so it is asked as two moments: midnight UTC
    /// N years before today, and now. Its interval coarsens with the span, so a long one stays a
    /// few hundred points: a day up to two years, a week up to ten, a month past that.
    /// </summary>
    [Theory]
    [InlineData(1, "1d")]
    [InlineData(2, "1d")]
    [InlineData(3, "1wk")]
    [InlineData(10, "1wk")]
    [InlineData(11, "1mo")]
    [InlineData(50, "1mo")]
    public void TheLastYearsAreAskedAsTwoMoments(int years, string interval)
    {
        var now = new DateTime(2026, 10, 3, 15, 30, 0, DateTimeKind.Utc);
        var ask = Ask(Share(MarketRange.Years, years));
        var from = new DateTimeOffset(new DateTime(2026 - years, 10, 3, 0, 0, 0, DateTimeKind.Utc)).ToUnixTimeSeconds();
        var to = new DateTimeOffset(now).ToUnixTimeSeconds();
        Assert.Equal($"period1={from}&period2={to}&interval={interval}", ask.Query(now));
        Assert.Equal($"{years}y", ask.Word);
    }

    /// <summary>
    /// The Years field sets whole years: a fraction typed is the whole number it rounds to (a half
    /// up), which the field then shows; past either end it is that end; emptied — NaN — it sets
    /// nothing, and the field shows the years it had.
    /// </summary>
    [Theory]
    [InlineData(3, 3)]
    [InlineData(2.6, 3)]
    [InlineData(2.4, 2)]
    [InlineData(2.5, 3)]
    [InlineData(0.4, 1)]
    [InlineData(-7, 1)]
    [InlineData(50.4, 50)]
    [InlineData(1e9, 50)]
    public void TheYearsFieldSetsWholeYears(double typed, int years) =>
        Assert.Equal(years, MarketRanges.YearsFrom(typed));

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void AnEmptiedYearsFieldSetsNothing(double typed) =>
        Assert.Null(MarketRanges.YearsFrom(typed));

    /// <summary>A number of years outside 1 to 50 asks for the nearest that is allowed; a range this build does not know asks for the default, five years.</summary>
    [Fact]
    public void ARangeOutsideWhatIsOfferedIsHeldToIt()
    {
        Assert.Equal(1, Ask(Share(MarketRange.Years, 0)).Years);
        Assert.Equal(50, Ask(Share(MarketRange.Years, 400)).Years);
        Assert.Equal(MarketRange.FiveYears, Ask(Share((MarketRange)(-1))).Range);
        Assert.Equal(MarketRange.FiveYears, Ask(Share((MarketRange)42)).Range);
        // Only the custom range carries a number of years: five years is one range however many
        // years the field last held.
        Assert.Equal(Ask(Share(MarketRange.FiveYears, 3)), Ask(Share(MarketRange.FiveYears, 7)));
    }

    /// <summary>Every range is a fetch and a key of its own, the type and the symbol before it.</summary>
    [Fact]
    public void EveryRangeIsKeyedByItsOwn()
    {
        Assert.Equal("stock:MSFT 1d", Ask(Share(MarketRange.Day, symbol: " msft ")).Key);
        Assert.Equal("stock:MSFT 5d", Ask(Share(MarketRange.FiveDays)).Key);
        Assert.Equal("stock:MSFT 3y", Ask(Share(MarketRange.Years, 3)).Key);
        var keys = MarketRanges.All.Select(range => Ask(Share(range)).Key).ToList();
        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    /// <summary>Two widgets of one share over two ranges ask the source twice, each for its own range, and each reads its own answer back.</summary>
    [Fact]
    public async Task TwoRangesOfOneShareAreTwoFetches()
    {
        var asked = new List<MarketAsk>();
        var store = new MarketStore(
            new MarketAnswers((ask, _) =>
            {
                lock (asked)
                {
                    asked.Add(ask);
                }
                return Task.FromResult(MarketQuotes.Parse(ask.Range == MarketRange.Day
                    ? MarketOffscreenTests.Answer("MSFT")
                    : Ranged("MSFT", 30, 86400, 100, 120, 90)));
            }),
            MarketDisk.None);
        var day = new object();
        var month = new object();
        store.Use(day, Ask(Share(MarketRange.Day)));
        store.Use(month, Ask(Share(MarketRange.Month)));
        store.Poll(day);
        store.Poll(month);
        var until = DateTime.UtcNow.AddSeconds(10);
        while ((store.Read(day).Shown is null || store.Read(month).Shown is null) && DateTime.UtcNow < until)
        {
            await Task.Delay(5);
        }
        Assert.Equal(2, asked.Count);
        Assert.Contains(asked, ask => ask.Range == MarketRange.Day);
        Assert.Contains(asked, ask => ask.Range == MarketRange.Month);
        Assert.Equal(30, store.Read(month).Shown!.Quote!.Points.Count);
        Assert.NotEqual(30, store.Read(day).Shown!.Quote!.Points.Count);
    }

    // --- The change ---------------------------------------------------------------------------

    /// <summary>
    /// Over a day, the change is the answer's own — against the previous close; over any other
    /// range it is from the close before the range began to the price now, as Yahoo's page and
    /// Windows' stock card have it.
    /// </summary>
    [Fact]
    public void TheChangeFollowsTheRange()
    {
        var quote = MarketQuotes.Parse(Ranged("MSFT", 60, 86400, 100, 120, 80, price: 126, dayPercent: 0.5));
        Assert.Equal(0.5, MarketReading.ChangeOver(quote, MarketRange.Day)!.Value, 6);
        // From the close before the range (80) to now (126): +57.5 %, not from its first point (100).
        Assert.Equal(57.5, MarketReading.ChangeOver(quote, MarketRange.Year)!.Value, 6);
        Assert.Equal(57.5, MarketReading.ChangeOver(quote, MarketRange.Years)!.Value, 6);

        var reading = MarketReading.Of(Kept(quote), Share(MarketRange.Year), DateTime.UtcNow, null);
        Assert.Equal("+57.50%", reading.Percent);
        Assert.Equal(1, reading.Direction);
        Assert.Equal("Past year", reading.Span);
    }

    /// <summary>Where the answer gives no close before the range, the change is measured from the range's first price; with no price at all, there is none.</summary>
    [Fact]
    public void WithoutACloseBeforeTheRangeTheFirstPriceIsTheStart()
    {
        var quote = MarketQuotes.Parse(Ranged("MSFT", 20, 86400, 200, 150, null, price: 150));
        Assert.Equal(-25, MarketReading.ChangeOver(quote, MarketRange.Month)!.Value, 6);
        var reading = MarketReading.Of(Kept(quote), Share(MarketRange.Month), DateTime.UtcNow, null);
        Assert.Equal("-25.00%", reading.Percent);
        Assert.Equal(-1, reading.Direction);

        var empty = new MarketQuote { Symbol = "MSFT" };
        Assert.Null(MarketReading.ChangeOver(empty, MarketRange.FiveYears));
        Assert.Equal("", MarketText.Percent(MarketReading.ChangeOver(empty, MarketRange.FiveYears)));
    }

    /// <summary>A day's reading is what it always was: the answer's change and the session's hours.</summary>
    [Fact]
    public void ADayIsReadAsItAlwaysWas()
    {
        var quote = MarketQuotes.Parse(MarketOffscreenTests.Answer("AAPL", percent: -1.057));
        var reading = MarketReading.Of(Kept(quote), Share(MarketRange.Day, symbol: "AAPL"), DateTime.UtcNow, null);
        Assert.Equal("-1.06%", reading.Percent);
        Assert.Equal(MarketRange.Day, reading.Range);
        Assert.Equal("Today", reading.Span);
        Assert.Empty(reading.Ticks);
        Assert.NotNull(reading.Session);
    }

    // --- The series ---------------------------------------------------------------------------

    /// <summary>
    /// A range is drawn in trading time, one step a point — five sessions side by side, not five
    /// sessions and four nights — from the line's first point to its last, the mark on the last.
    /// </summary>
    [Fact]
    public void ARangeIsOneStepAPoint()
    {
        var quote = MarketQuotes.Parse(Ranged("MSFT", 130, 900, 100, 110, 99, sessionPoints: 26));
        var reading = MarketReading.Of(Kept(quote), Share(MarketRange.FiveDays), DateTime.UtcNow, null);
        Assert.Equal(130, reading.Day.Count);
        Assert.Equal(Enumerable.Range(0, 130).Select(index => (double)index), reading.Day.Select(point => point.At));
        Assert.Equal((0.0, 129.0), reading.Axis);
        Assert.Null(reading.Session);
        Assert.Equal(129, reading.NowAt);
        Assert.Equal(reading.Day[^1].Price, reading.NowValue);
    }

    /// <summary>
    /// The panel writes under a range the first point of each day over five days, of each week over
    /// a month, of each month over a year and of each year over five.
    /// </summary>
    [Fact]
    public void ARangeIsWrittenInDaysWeeksMonthsOrYears()
    {
        var monday = new DateTime(2026, 9, 28, 9, 30, 0);
        // Five sessions of 26 quarter-hours: a tick at the start of each day but the first.
        var days = Enumerable.Range(0, 5).SelectMany(day => Enumerable.Range(0, 26).Select(step => monday.AddDays(day).AddMinutes(15 * step))).ToList();
        var ticks = MarketReading.TicksOf(days);
        Assert.Equal([26, 52, 78, 104], ticks.Select(tick => tick.Index));
        Assert.Equal(Karpo.Dock.Panels.Nls.ShortDayName(DayOfWeek.Tuesday), ticks[0].Label);

        // A year of days: one a month, eleven turns.
        var year = Enumerable.Range(0, 365).Select(day => new DateTime(2025, 10, 3).AddDays(day)).ToList();
        var months = MarketReading.TicksOf(year);
        Assert.Equal(12, months.Count);
        Assert.All(months, tick => Assert.Equal(1, year[tick.Index].Day));

        // Five years of weeks: the years.
        var weeks = Enumerable.Range(0, 261).Select(week => new DateTime(2021, 10, 4).AddDays(7 * week)).ToList();
        var years = MarketReading.TicksOf(weeks);
        Assert.Equal(["2022", "2023", "2024", "2025", "2026"], years.Select(tick => tick.Label));

        // A month of hours: a tick at each new week.
        var hours = Enumerable.Range(0, 30).SelectMany(day => Enumerable.Range(0, 7).Select(hour => new DateTime(2026, 9, 1, 10, 0, 0).AddDays(day).AddHours(hour))).ToList();
        var mondays = MarketReading.TicksOf(hours);
        Assert.All(mondays, tick => Assert.Equal(DayOfWeek.Monday, hours[tick.Index].DayOfWeek));
        Assert.Equal(4, mondays.Count);
    }

    /// <summary>An answer longer than a widget keeps is cut from its start: a chart ends at now.</summary>
    [Fact]
    public void ALongAnswerKeepsItsNewestPoints()
    {
        var count = MarketQuotes.MostPoints + 100;
        var quote = MarketQuotes.Parse(Ranged("MSFT", count, 86400, 100, 300, 99));
        Assert.Equal(MarketQuotes.MostPoints, quote.Points.Count);
        Assert.Equal(300, quote.Points[^1].Price, 3);
        Assert.Equal(100 + (200.0 * 100 / (count - 1)), quote.Points[0].Price, 3);
    }

    /// <summary>A range's answer kept in markets.json comes back with what its change is measured from.</summary>
    [Fact]
    public void AKeptRangeRemembersTheCloseBeforeIt()
    {
        var quote = MarketQuotes.Parse(Ranged("MSFT", 40, 86400, 100, 120, 80, price: 120));
        var back = MarketSaved.Of(quote).Read()!;
        Assert.Equal(80, back.ChartPreviousClose);
        Assert.Equal(MarketReading.ChangeOver(quote, MarketRange.Year), MarketReading.ChangeOver(back, MarketRange.Year));
    }

    // --- The setting --------------------------------------------------------------------------

    /// <summary>The range is written in the endpoint's own words and read back as written, the custom years with it.</summary>
    [Theory]
    [InlineData(MarketRange.Day, "1d")]
    [InlineData(MarketRange.FiveDays, "5d")]
    [InlineData(MarketRange.YearToDate, "ytd")]
    [InlineData(MarketRange.Max, "max")]
    [InlineData(MarketRange.Years, "years")]
    public void TheRangeIsWrittenInWordsAndReadBack(MarketRange range, string word)
    {
        var stock = ListKind.Markets.New();
        stock.Id = "share";
        stock.Market = Share(range, 7);
        var folder = Configs.Holding(stock);
        folder.Id = "f";
        var config = Configs.With(folder);
        using var written = JsonDocument.Parse(config.ToJson());
        var saved = Configs.Find(written.RootElement, "market")!.Value;
        Assert.Equal(word, saved.GetProperty("range").GetString());

        var back = Configs.RoundTrip(config, out var changed);
        Assert.False(changed);
        Assert.Equal(range, Configs.Islands(back)[0].Folder!.Cells.First().Island.Market!.Range);
        Assert.Equal(7, Configs.Islands(back)[0].Folder!.Cells.First().Island.Market!.RangeYears);
    }

    /// <summary>
    /// A file with no range — written before ranges existed — or one this build does not know, shows
    /// five years, the default; a range somebody picked is kept, a day included; a number of years
    /// past the ends is held to them.
    /// </summary>
    [Theory]
    [InlineData(""", "range": "decade" """, MarketRange.FiveYears)]
    [InlineData(""", "range": null """, MarketRange.FiveYears)]
    [InlineData("", MarketRange.FiveYears)]
    [InlineData(""", "range": "5y" """, MarketRange.FiveYears)]
    [InlineData(""", "range": "1d" """, MarketRange.Day)]
    [InlineData(""", "range": "1y" """, MarketRange.Year)]
    public void ARangeThisBuildDoesNotKnowIsTheDefault(string key, MarketRange expected)
    {
        var config = Configs.Repaired($$"""
            { "layouts": [ { "id": "default", "name": "Default", "islands": [
              { "id": "f", "kind": "folderList", "folder": { "lists": [ [ { "island": { "id": "share", "kind": "markets", "market": { "symbol": "MSFT", "rangeYears": 400{{key}} } } } ] ] } } ] } ] }
            """);
        Assert.Equal(expected, Configs.Islands(config)[0].Folder!.Cells.First().Island.Market!.Range);
        Assert.Equal(MarketOptions.MostYears, Configs.Islands(config)[0].Folder!.Cells.First().Island.Market!.RangeYears);
    }

    /// <summary>What the settings offer and what the widget says the change is over, a word each.</summary>
    [Fact]
    public void EveryRangeHasItsWords()
    {
        Assert.Equal(9, MarketRanges.All.Count);
        Assert.Equal(MarketRanges.All.Count, MarketRanges.All.Select(MarketRanges.Choice).Distinct().Count());
        Assert.Equal("Past 3 years", MarketRanges.Span(MarketRange.Years, 3));
        Assert.Equal("Past year", MarketRanges.Span(MarketRange.Years, 1));
        Assert.Equal("Year to date", MarketRanges.Span(MarketRange.YearToDate, 0));
        Assert.Equal("Past 5 years", MarketRanges.Span((MarketRange)(-1), 0));
        Assert.Equal("Today", MarketRanges.Span(MarketRange.Day, 0));
    }

    // --- The default ---------------------------------------------------------------------------

    /// <summary>
    /// "default chart range should be 5 years": a new widget, one a ready-made group adds and one a
    /// folder's line makes, asks for five years, a week a point — a range the endpoint has as its own.
    /// </summary>
    [Fact]
    public void ANewWidgetShowsFiveYears()
    {
        Assert.Equal(MarketRange.FiveYears, MarketRanges.Default);
        Assert.Equal(MarketRange.FiveYears, new MarketOptions().Range);
        var made = ListKind.Markets.New();
        made.Market = new MarketOptions { Symbol = "MSFT" };
        made.Normalize();
        Assert.Equal(MarketRange.FiveYears, made.Market!.Range);

        var ask = Ask(new MarketOptions { Symbol = "MSFT" });
        Assert.Equal("5y", ask.Word);
        Assert.Equal("1wk", ask.Interval);
        Assert.Equal("stock:MSFT 5y", ask.Key);
        Assert.EndsWith("/MSFT?range=5y&interval=1wk", ask.YahooUrl(ask.Symbol, new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc)), StringComparison.Ordinal);
    }

    /// <summary>The settings offer five years, and it is what the list shows for a widget never set.</summary>
    [Fact]
    public void TheSettingsOfferFiveYears()
    {
        Assert.Contains(MarketRange.FiveYears, MarketRanges.All);
        Assert.Equal("5 years", MarketRanges.Choice(MarketRanges.Default));
        Assert.Equal("5 years", MarketRanges.Choice((MarketRange)(-1)));
    }

    // --- The range's label ---------------------------------------------------------------------

    /// <summary>The small label beside the change, as Windows' stock card and Yahoo Finance write their range buttons.</summary>
    [Theory]
    [InlineData(MarketRange.Day, 3, "1D")]
    [InlineData(MarketRange.FiveDays, 3, "5D")]
    [InlineData(MarketRange.Month, 3, "1M")]
    [InlineData(MarketRange.SixMonths, 3, "6M")]
    [InlineData(MarketRange.YearToDate, 3, "YTD")]
    [InlineData(MarketRange.Year, 3, "1Y")]
    [InlineData(MarketRange.FiveYears, 3, "5Y")]
    [InlineData(MarketRange.Max, 3, "Max")]
    [InlineData(MarketRange.Years, 3, "3Y")]
    [InlineData(MarketRange.Years, 1, "1Y")]
    [InlineData(MarketRange.Years, 400, "50Y")]
    [InlineData(MarketRange.Years, 0, "1Y")]
    [InlineData((MarketRange)(-1), 3, "5Y")]
    public void TheRangeIsLabelledAsItsButtonIs(MarketRange range, int years, string label) =>
        Assert.Equal(label, MarketRanges.Label(range, years));

    /// <summary>Every range has a label of its own, so two widgets over two ranges never read alike.</summary>
    [Fact]
    public void EveryRangeHasALabelOfItsOwn() =>
        Assert.Equal(MarketRanges.All.Count, MarketRanges.All.Select(range => MarketRanges.Label(range, 3)).Distinct().Count());

    /// <summary>A reading carries its range's label, over a day as over five years and the last three.</summary>
    [Fact]
    public void AReadingSaysItsRangesLabel()
    {
        var now = DateTime.UtcNow;
        var day = MarketReading.Of(Kept(MarketQuotes.Parse(MarketOffscreenTests.Answer("MSFT"))), Share(MarketRange.Day), now, null);
        var five = MarketReading.Of(Kept(MarketQuotes.Parse(Ranged("MSFT", 261, 7 * 86400, 289, 412.34, 289.1))), Share(MarketRange.FiveYears), now, null);
        var three = MarketReading.Of(Kept(MarketQuotes.Parse(Ranged("MSFT", 157, 7 * 86400, 327, 412.34, 313.39))), Share(MarketRange.Years, 3), now, null);
        Assert.Equal("1D", day.RangeLabel);
        Assert.Equal("5Y", five.RangeLabel);
        Assert.Equal("3Y", three.RangeLabel);
        // The currency the answer quotes it in, for the price to say beside it.
        Assert.Equal("USD", five.Currency);
        Assert.Equal("412.34", five.Price);
    }

    // --- Answers ------------------------------------------------------------------------------

    private static MarketCache Kept(MarketQuote quote) => new()
    {
        FetchedUtc = DateTime.UtcNow,
        Key = quote.Symbol,
        Quote = quote,
    };

    /// <summary>
    /// An answer shaped as the endpoint's for a range: <paramref name="count"/> points
    /// <paramref name="step"/> seconds apart — in sessions of <paramref name="sessionPoints"/> a day
    /// where given — rising evenly from <paramref name="first"/> to <paramref name="last"/>, ending
    /// now, with the close before the range where there is one, and <paramref name="wander"/> of a
    /// market's wandering on top for a picture; <paramref name="name"/> is the company's.
    /// </summary>
    internal static string Ranged(
        string symbol, int count, int step, double first, double last, double? chartPreviousClose,
        double? price = null, double dayPercent = 0.4, int sessionPoints = 0, double wander = 0, string name = "Microsoft Corporation")
    {
        var end = (long)(DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds;
        var times = new StringBuilder();
        var closes = new StringBuilder();
        for (var index = 0; index < count; index++)
        {
            var back = count - 1 - index;
            var at = sessionPoints > 0
                ? end - ((back / sessionPoints) * 86400L) - ((back % sessionPoints) * (long)step)
                : end - (back * (long)step);
            var comma = index == 0 ? "" : ",";
            times.Append(comma).Append(at.ToString(Invariant));
            var value = first + ((last - first) * index / Math.Max(1, count - 1));
            // A market's wander on top of the trend, nothing at either end: the tests read the ends.
            value += wander * Math.Sin(Math.PI * index / Math.Max(1, count - 1)) * ((Math.Sin(index / 9.0) * 0.7) + (Math.Sin(index / 2.3) * 0.3));
            closes.Append(comma).Append(value.ToString("0.####", Invariant));
        }
        var previous = chartPreviousClose is { } close ? $"\"chartPreviousClose\": {close.ToString("0.####", Invariant)}," : "";
        return $$"""
            {
              "chart": {
                "result": [
                  {
                    "meta": {
                      "currency": "USD", "symbol": "{{symbol}}", "regularMarketTime": {{end}}, "gmtoffset": -14400,
                      "regularMarketPrice": {{(price ?? last).ToString("0.####", Invariant)}},
                      "regularMarketChangePercent": {{dayPercent.ToString("0.####", Invariant)}},
                      {{previous}}
                      "longName": "{{name}}"
                    },
                    "timestamp": [{{times}}],
                    "indicators": { "quote": [ { "close": [{{closes}}] } ] }
                  }
                ],
                "error": null
              }
            }
            """;
    }
}
