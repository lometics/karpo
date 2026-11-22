// markets, §494: a share's or a coin's amounts shown in another currency than it is listed in (the person,
// 2026-11-03: "for the markets widget you should be able to override the currency in which the stock for
// example is shown, it should then lookup the conversion in the background of course and do the currency
// conversion for you"). The arithmetic and each currency's decimals; the listed figures standing until a
// rate has come; the swap of a holding's worth and the price of one in another currency; what hovering
// says; the rate asked of the store beside the price; the setting's row and its search; and the file. Stand-in
// answers only: nothing reaches the network.

using System.Text.Json;
using Karpo.Core;
using Karpo.Dock.Markets;
using Karpo.Tests.Core;
using Karpo.Tests.Forecasts;

namespace Karpo.Tests.Markets;

public sealed class MarketCurrencyTests
{
    private static MarketCache Kept(string json) => MarketOffscreenTests.Kept(json);

    /// <summary>Microsoft at 640 dollars over a day, up 1.24 %.</summary>
    private static string Msft() => MarketOffscreenTests.Answer("MSFT", "Microsoft Corporation", price: 640, percent: 1.24, previous: 632.16);

    private static MarketOptions InFrancs(double holding = 0) => new() { Symbol = "MSFT", Range = MarketRange.Day, Currency = "CHF", Holding = holding, Detail = "Microsoft Corporation · NASDAQ · USD" };

    private static readonly MarketRate DollarInFrancs = new("USD", "CHF", 0.8);

    // --- The arithmetic ---------------------------------------------------------------------------

    /// <summary>A quote's code as the whole currency it counts in, and how many of it make one.</summary>
    [Theory]
    [InlineData("USD", "USD", 1)]
    [InlineData("GBp", "GBP", 100)]
    [InlineData("GBX", "GBP", 100)]
    [InlineData("ZAc", "ZAR", 100)]
    [InlineData("ILA", "ILS", 100)]
    [InlineData(" CHF ", "CHF", 1)]
    [InlineData(null, "", 1)]
    public void AQuotesCodeIsAWholeCurrency(string? code, string whole, double units) =>
        Assert.Equal((whole, units), MarketMoney.Whole(code));

    /// <summary>What one listed is worth shown: the rate, a hundredth of it for pence; nothing to convert as listed, to itself or without a rate.</summary>
    [Fact]
    public void TheFactorIsTheRateOrNothing()
    {
        Assert.Equal(0.8, MarketMoney.Factor("USD", "CHF", 0.8));
        Assert.Equal(0.0112, MarketMoney.Factor("GBp", "CHF", 1.12)!.Value, 12);
        Assert.Null(MarketMoney.Factor("USD", "", 0.8));
        Assert.Null(MarketMoney.Factor("CHF", "CHF", 1));
        Assert.Null(MarketMoney.Factor("USD", "CHF", null));
        Assert.Null(MarketMoney.Factor("USD", "CHF", 0));
        Assert.Null(MarketMoney.Factor("USD", "CHF", double.NaN));
        Assert.Null(MarketMoney.Factor("", "CHF", 0.8));
    }

    /// <summary>A price in a currency it is shown in, to that currency's decimals: the yen none, the dinar three; under one as many as it needs.</summary>
    [Theory]
    [InlineData("CHF", 512.3, 2)]
    [InlineData("JPY", 96_150.4, 0)]
    [InlineData("KRW", 880_000, 0)]
    [InlineData("KWD", 196.6, 3)]
    [InlineData("EUR", 0.42, 4)]
    [InlineData("JPY", 0.0034, 5)]
    public void APriceShownIsWrittenToItsCurrencysDecimals(string code, double value, int places) =>
        Assert.Equal(places, MarketMoney.Places(code, value));

    /// <summary>The setting keeps a code Karpo offers, whatever its case; nothing else.</summary>
    [Theory]
    [InlineData("CHF", "CHF")]
    [InlineData(" chf ", "CHF")]
    [InlineData("XYZ", null)]
    [InlineData("francs", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void TheSettingKeepsAnOfferedCode(string? typed, string? kept) => Assert.Equal(kept, MarketMoney.Kept(typed));

    /// <summary>What it is listed in before a price says: a coin's and a rate's second currency, a share's as its search named it.</summary>
    [Fact]
    public void WhatItIsListedInBeforeAPrice()
    {
        Assert.Equal("USD", MarketMoney.Listed(new MarketOptions { Symbol = "MSFT", Detail = "Microsoft Corporation · NASDAQ · USD" }));
        Assert.Equal("GBP", MarketMoney.Listed(new MarketOptions { Symbol = "VOD.L", Detail = "Vodafone Group · LSE · GBp" }));
        Assert.Equal("", MarketMoney.Listed(new MarketOptions { Symbol = "MSFT" }));
        Assert.Equal("EUR", MarketMoney.Listed(new MarketOptions { Symbol = "BTC-EUR" }));
        Assert.Equal("CHF", MarketMoney.Listed(new MarketOptions { Symbol = "EURCHF=X" }));
    }

    /// <summary>Shown in a currency only where one is chosen and it is no rate: a rate's figure is between the currencies it tracks.</summary>
    [Fact]
    public void ARateIsNeverShownInAnotherCurrency()
    {
        Assert.Equal("CHF", InFrancs().ShownIn);
        Assert.Equal("CHF", new MarketOptions { Symbol = "BTC-USD", Currency = "CHF" }.ShownIn);
        Assert.Equal("", new MarketOptions { Symbol = "EURUSD=X", Currency = "CHF" }.ShownIn);
        Assert.Equal("", new MarketOptions { Symbol = "MSFT" }.ShownIn);
        Assert.Equal("", new MarketOptions { Symbol = "MSFT", Currency = "XYZ" }.ShownIn);
    }

    // --- The reading ------------------------------------------------------------------------------

    /// <summary>
    /// A share in dollars shown in francs: the price at the rate now, in francs, to the centime; the change
    /// the same in per cent; the chart and its last point at the rate; and hovering says both and the rate.
    /// </summary>
    [Fact]
    public void ASharesPriceIsShownInTheChosenCurrency()
    {
        var now = DateTime.UtcNow;
        var listed = MarketReading.Of(Kept(Msft()), new MarketOptions { Symbol = "MSFT", Range = MarketRange.Day }, now, null);
        var reading = MarketReading.Of(Kept(Msft()), InFrancs(), now, null, rate: DollarInFrancs);
        Assert.Equal(("512.00", "CHF", "+1.24%", 1), (reading.Price, reading.Currency, reading.Percent, reading.Direction));
        Assert.Equal(listed.Percent, reading.Percent);
        Assert.Equal(listed.Day.Count, reading.Day.Count);
        Assert.All(listed.Day.Zip(reading.Day), pair => Assert.Equal(pair.First.Price * 0.8, pair.Second.Price, 9));
        Assert.Equal(512, reading.NowValue!.Value, 9);
        Assert.Equal(("640.00 USD", 0.8), reading.Exchange);
        Assert.Equal("512.00 CHF (640.00 USD × 0.8000)", reading.ExchangeTip());
        // Shown as listed: no rate, nothing more to say than the line says.
        Assert.Null(listed.Exchange);
        Assert.Null(listed.ExchangeTip());
    }

    /// <summary>Until a rate has come — or where the one at hand is of another pair — the figures stand as listed, never nothing.</summary>
    [Fact]
    public void BeforeARateTheListedFiguresStand()
    {
        var now = DateTime.UtcNow;
        foreach (var rate in new MarketRate?[] { null, new MarketRate("EUR", "CHF", 0.93), new MarketRate("USD", "JPY", 150) })
        {
            var reading = MarketReading.Of(Kept(Msft()), InFrancs(), now, null, rate: rate);
            Assert.Equal((MarketShows.Ready, "640.00", "USD"), (reading.State, reading.Price, reading.Currency));
            Assert.Null(reading.ExchangeTip());
        }
        // Before a price, it waits as ever, in what it is listed in.
        var waiting = MarketReading.Of(null, InFrancs(), now, null, rate: DollarInFrancs);
        Assert.Equal(MarketShows.Loading, waiting.State);
    }

    /// <summary>In yen a share has no decimals; a coin under one keeps the figures it needs.</summary>
    [Fact]
    public void EachCurrencyRoundsItsOwnWay()
    {
        var now = DateTime.UtcNow;
        var yen = MarketReading.Of(Kept(Msft()), new MarketOptions { Symbol = "MSFT", Range = MarketRange.Day, Currency = "JPY" }, now, null, rate: new MarketRate("USD", "JPY", 150.237));
        Assert.Equal(("96,152", "JPY"), (yen.Price, yen.Currency));
        Assert.Equal("96,152 JPY (640.00 USD × 150.24)", yen.ExchangeTip());

        var doge = MarketOffscreenTests.Answer("DOGE-USD", "Dogecoin USD", price: 0.42, percent: 2, previous: 0.41, wander: 0.001);
        var coin = MarketReading.Of(Kept(doge), new MarketOptions { Symbol = "DOGE-USD", Range = MarketRange.Day, Currency = "CHF" }, now, null, rate: DollarInFrancs);
        Assert.Equal(("0.3360", "CHF"), (coin.Price, coin.Currency));
    }

    /// <summary>London in pence shown in francs: the pounds' rate, a hundredth of it to the penny; hovering names the price in pounds.</summary>
    [Fact]
    public void PenceAreConvertedAsHundredthsOfThePound()
    {
        var vod = MarketOffscreenTests.Answer("VOD.L", "Vodafone Group", "GBp", price: 72.6, percent: 0.4, previous: 72.2);
        var options = new MarketOptions { Symbol = "VOD.L", Range = MarketRange.Day, Currency = "CHF" };
        var reading = MarketReading.Of(Kept(vod), options, DateTime.UtcNow, null, rate: new MarketRate("GBP", "CHF", 1.12));
        Assert.Equal(("0.8131", "CHF"), (reading.Price, reading.Currency));
        Assert.Equal("0.8131 CHF (0.73 GBP × 1.1200)", reading.ExchangeTip());
    }

    /// <summary>
    /// With an amount owned and another currency: the worth in that currency is the main figure, the price of
    /// one in it where the worth stood, and hovering says the worth in both, then the amount and one's price.
    /// </summary>
    [Fact]
    public void AHoldingInAnotherCurrencySwapsToo()
    {
        var reading = MarketReading.Of(Kept(Msft()), InFrancs(10), DateTime.UtcNow, null, rate: DollarInFrancs);
        Assert.Equal(("5,120.00", "CHF"), (reading.Price, reading.Currency));
        Assert.Equal(("10 shares × 512.00 CHF", "512.00 CHF"), reading.Holding);
        Assert.Equal("5,120.00 CHF (6,400.00 USD × 0.8000)\n10 shares × 512.00 CHF", reading.ExchangeTip());
        // Before the rate: the worth and one's price in dollars, swapped all the same.
        var listed = MarketReading.Of(Kept(Msft()), InFrancs(10), DateTime.UtcNow, null);
        Assert.Equal(("6,400.00", "USD", ("10 shares × 640.00 USD", "640.00 USD")), (listed.Price, listed.Currency, listed.Holding!.Value));
    }

    // --- The rate, asked in the background --------------------------------------------------------

    /// <summary>
    /// The rate asked is the listed currency's in the shown one, over a day — the price's currency once it
    /// has come, what the search named before —; none as listed, for a rate, or to itself.
    /// </summary>
    [Fact]
    public void TheRateAskedIsTheListedInTheShown()
    {
        var quote = Kept(Msft()).Quote;
        Assert.Equal(new MarketAsk(MarketKind.Currency, "USD", "CHF", MarketRange.Day, 0), MarketExchange.AskOf(InFrancs(), quote));
        Assert.Equal("currency:USDCHF 1d", MarketExchange.AskOf(InFrancs(), null)!.Value.Key);
        Assert.Equal("GBP", MarketExchange.AskOf(new MarketOptions { Symbol = "VOD.L", Currency = "CHF" }, Kept(MarketOffscreenTests.Answer("VOD.L", "Vodafone", "GBp")).Quote)!.Value.Name);
        Assert.Equal(("BTC", "EUR", "CHF"), ("BTC", MarketExchange.AskOf(new MarketOptions { Symbol = "BTC-EUR", Currency = "CHF" }, null)!.Value.Name, "CHF"));
        Assert.Null(MarketExchange.AskOf(new MarketOptions { Symbol = "MSFT" }, quote));
        Assert.Null(MarketExchange.AskOf(new MarketOptions { Symbol = "MSFT", Currency = "USD" }, quote));
        Assert.Null(MarketExchange.AskOf(new MarketOptions { Symbol = "EURUSD=X", Currency = "CHF" }, null));
        // A share nothing names the currency of yet asks once its price has come.
        Assert.Null(MarketExchange.AskOf(new MarketOptions { Symbol = "MSFT", Currency = "CHF" }, null));
    }

    /// <summary>
    /// The rate is one more question to the store — kept in its file, so it is there at once — and stands
    /// beside the price; a store whose rate never comes leaves the listed figures, and a pair never asks one.
    /// </summary>
    [Fact]
    public void TheRateComesFromTheStore()
    {
        var store = MarketOffscreenTests.Settled(new()
        {
            ["MSFT"] = Msft(),
            ["USDCHF=X"] = MarketOffscreenTests.Answer("USDCHF=X", "USD/CHF", "CHF", price: 0.8, percent: 0.1, previous: 0.799, wander: 0.0001),
        });
        var exchange = new MarketExchange(store, null);
        exchange.Follow(InFrancs(), null);
        Assert.Equal(new MarketRate("USD", "CHF", 0.8), exchange.Rate);
        exchange.Follow(new MarketOptions { Symbol = "MSFT" }, null);
        Assert.Null(exchange.Asked);
        Assert.Null(exchange.Rate);
        exchange.Release();

        var waiting = new MarketExchange(MarketOffscreenTests.Waiting(), null);
        waiting.Follow(InFrancs(), null);
        Assert.NotNull(waiting.Asked);
        Assert.Null(waiting.Rate);
        Assert.Equal("640.00", MarketReading.Of(Kept(Msft()), InFrancs(), DateTime.UtcNow, null, rate: waiting.Rate).Price);
        waiting.Release();
    }

    // --- The setting ---------------------------------------------------------------------------------

    private static CellItem Line(MarketOptions market)
    {
        var line = ListKind.Markets.New();
        line.Market = market.Copy();
        return line;
    }

    /// <summary>
    /// The Currency row of a share's and a coin's menu, line and face: "As listed (USD)" until one is
    /// chosen; a pick writes the code, as listed writes none; a rate has no such row.
    /// </summary>
    [Fact]
    public void TheCurrencyIsSearchedAndPicked()
    {
        var line = Line(new MarketOptions { Symbol = "MSFT", Detail = "Microsoft Corporation · NASDAQ · USD" });
        OptionRow Row() => OptionRows.Of(line).Single(row => row.Key == MarketRows.Currency);
        Assert.Equal((OptionRowKind.Search, S.SettingsWidgetMarketCurrencyListed("USD"), S.SettingsWidgetMarketCurrencyListed("USD")), (Row().Kind, Row().Text, Row().Summary));
        var finder = Assert.IsType<MarketRows.CurrencyFinder>(Row().Finder);
        Assert.Equal(0, finder.LeastLetters);

        Assert.True(Row().Pick!(new OptionFound("CHF", "", "CHF")));
        Assert.Equal("CHF", line.Market!.Currency);
        Assert.Equal("CHF", Row().Summary);
        Assert.False(Row().Pick!(new OptionFound("CHF", "", "CHF")));
        Assert.True(Row().Pick!(new OptionFound(S.SettingsWidgetMarketCurrencyListed("USD"), "", "")));
        Assert.Null(line.Market.Currency);

        Assert.Contains(MarketRows.Currency, OptionRows.Of(Line(new MarketOptions { Symbol = "BTC-USD" })).Select(row => row.Key));
        Assert.DoesNotContain(MarketRows.Currency, OptionRows.Of(Line(new MarketOptions { Symbol = "EURCHF=X" })).Select(row => row.Key));
        var folder = BarKind.FolderList.New();
        folder.Folder!.Faces = [FolderFace.Of(FolderFaces.MarketsId)];
        folder.Folder.Faces[0].Market = new MarketOptions { Symbol = "MSFT" };
        Assert.Contains(MarketRows.Currency, OptionRows.OfFace(folder.Folder, 0).Select(row => row.Key));
        // Before anything names its listing, plainly as listed.
        Assert.Equal(S.SettingsWidgetMarketCurrencyListedAny, OptionRows.Of(Line(new MarketOptions { Symbol = "MSFT" })).Single(row => row.Key == MarketRows.Currency).Text);
    }

    /// <summary>
    /// The search: before anything is typed as listed, ticked, then the region's and the world's most traded
    /// first, every currency the rates reach; typed, a code or a name in Karpo's language or English.
    /// </summary>
    [Fact]
    public void TheCurrencySearchFindsCodesAndNames()
    {
        var finder = new MarketRows.CurrencyFinder("", "USD", "CHF");
        var all = finder.Find("");
        Assert.Equal(("", true), ((string)all[0].Value!, all[0].Chosen));
        Assert.Equal(["CHF", "USD", "EUR", "JPY"], all.Skip(1).Take(4).Select(row => (string)row.Value!));
        Assert.Equal(Currencies.Codes.Count + 1, all.Count);

        var chosen = new MarketRows.CurrencyFinder("JPY", "USD", "CHF").Find("");
        Assert.Equal(["JPY"], chosen.Where(row => row.Chosen).Select(row => (string)row.Value!));

        Assert.Equal("CHF", (string)finder.Find("ch")[0].Value!);
        Assert.Equal("GBP", (string)finder.Find("gbp")[0].Value!);
        // Names are ICU's, from Windows: "yen" finds the yen where it has them, its code always.
        Assert.Contains("JPY", finder.Find("JP").Select(row => (string)row.Value!));
        Assert.Empty(finder.Find("zzzz"));
    }

    // --- The file ------------------------------------------------------------------------------------

    private static BarItem Holder(MarketOptions market)
    {
        var stock = ListKind.Markets.New();
        stock.Id = "share";
        stock.Market = market;
        var folder = Configs.Holding(stock);
        folder.Id = "f";
        return folder;
    }

    /// <summary>The currency is written with the share's other settings and read back; as listed is not written at all; a word nobody offers reads as listed.</summary>
    [Fact]
    public void TheCurrencyIsWrittenAndReadBack()
    {
        var config = Configs.With(Holder(InFrancs()));
        using (var written = JsonDocument.Parse(config.ToJson()))
        {
            Assert.Equal("CHF", Configs.Find(written.RootElement, "market")!.Value.GetProperty("currency").GetString());
        }
        var back = Configs.RoundTrip(config, out var changed);
        Assert.False(changed);
        Assert.Equal("CHF", Configs.Islands(back)[0].Folder!.Cells.First().Island.Market!.Currency);

        var listed = Configs.With(Holder(new MarketOptions { Symbol = "MSFT" }));
        using (var written = JsonDocument.Parse(listed.ToJson()))
        {
            Assert.False(Configs.Find(written.RootElement, "market")!.Value.TryGetProperty("currency", out _));
        }

        var typed = Configs.Repaired("""
            { "layouts": [ { "id": "default", "name": "Default", "islands": [
              { "id": "f", "kind": "folderList", "folder": { "lists": [ [
                { "island": { "id": "a", "kind": "markets", "market": { "symbol": "MSFT", "currency": "chf" } } },
                { "island": { "id": "b", "kind": "markets", "market": { "symbol": "MSFT", "currency": "XYZ" } } } ] ] } } ] } ] }
            """);
        var cells = Configs.Islands(typed)[0].Folder!.Cells.Select(cell => cell.Island.Market!.Currency).ToList();
        Assert.Equal(["CHF", null], cells);
    }
}
