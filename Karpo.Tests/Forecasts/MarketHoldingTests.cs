// markets: an amount owned of a share or a coin (MarketOptions.Holding). The person: "you should also be
// able to add a holding amount, very simple, just an amount you own, it should then show the amount you
// hold." What a typed amount is kept as, how it and what it is worth are written, what a reading says,
// that the setting survives the file and a merge with the other app's save, and that it is a row of a
// share's and a coin's menu. No network and no window.

using System.Text.Json;
using System.Text.Json.Nodes;
using Karpo.Core;
using Karpo.Dock.Markets;
using Karpo.Tests.Core;

namespace Karpo.Tests.Forecasts;

public sealed class MarketHoldingTests
{
    // --- What is kept --------------------------------------------------------------------------

    /// <summary>
    /// What the field sets: the amount, decimals allowed, to eight places; none for an emptied field,
    /// for 0, for less than nothing and for what is not a number; at most a trillion.
    /// </summary>
    [Theory]
    [InlineData(10, 10)]
    [InlineData(2.5, 2.5)]
    [InlineData(0.00012345, 0.00012345)]
    [InlineData(0.123456789, 0.12345679)]
    [InlineData(0, 0)]
    [InlineData(-3, 0)]
    [InlineData(double.NaN, 0)]
    [InlineData(double.PositiveInfinity, 0)]
    [InlineData(double.NegativeInfinity, 0)]
    [InlineData(1e20, 1e12)]
    public void TheFieldKeepsAnAmountOrNone(double typed, double kept)
    {
        Assert.Equal(kept, MarketHoldings.From(typed));
        Assert.Equal(kept > 0, MarketHoldings.Holds(typed));
    }

    /// <summary>An amount as it is written: grouped, with the decimals it has and no trailing zeros.</summary>
    [Theory]
    [InlineData(10, "10")]
    [InlineData(2.5, "2.5")]
    [InlineData(1250, "1,250")]
    [InlineData(0.0125, "0.0125")]
    [InlineData(1234567.125, "1,234,567.125")]
    public void AnAmountIsWrittenAsItIs(double amount, string written) =>
        Assert.Equal(written, Currencies.Plain(amount));

    /// <summary>What the amount is of: shares of a share, a fund or an index; a coin's own ticker of a coin.</summary>
    [Theory]
    [InlineData(1, MarketKind.Stock, "", "1 share")]
    [InlineData(10, MarketKind.Stock, "", "10 shares")]
    [InlineData(2.5, MarketKind.Stock, "", "2.5 shares")]
    [InlineData(1250, MarketKind.Stock, "", "1,250 shares")]
    [InlineData(0.5, MarketKind.Crypto, "BTC", "0.5 BTC")]
    [InlineData(1.25, MarketKind.Crypto, "ETH", "1.25 ETH")]
    public void AnAmountSaysWhatItIsOf(double amount, MarketKind kind, string coin, string said) =>
        Assert.Equal(said, MarketHoldings.Units(amount, kind, coin));

    // --- What it is worth -----------------------------------------------------------------------

    /// <summary>The amount times the price, in the price's currency; a currency's hundredths are the currency itself.</summary>
    [Fact]
    public void ItIsWorthTheAmountTimesThePrice()
    {
        Assert.Equal((4123.4, "USD"), Round(MarketHoldings.Worth(10, 412.34, "USD")));
        Assert.Equal((1030.85, "USD"), Round(MarketHoldings.Worth(2.5, 412.34, "USD")));
        // London quotes in pence: a hundred shares at 1,234.50p are 1,234.50 pounds.
        Assert.Equal((1234.5, "GBP"), Round(MarketHoldings.Worth(100, 1234.5, "GBp")));
        Assert.Equal((500, "ZAR"), Round(MarketHoldings.Worth(10, 5000, "ZAc")));
        Assert.Equal((25, "ILS"), Round(MarketHoldings.Worth(10, 250, "ILA")));
        // No code: the figure all the same.
        Assert.Equal((41.234, ""), Round(MarketHoldings.Worth(0.1, 412.34, null)));
    }

    /// <summary>Nothing to say with no holding, no price, or no number.</summary>
    [Fact]
    public void NoHoldingOrNoPriceIsWorthNothingToSay()
    {
        Assert.Null(MarketHoldings.Worth(0, 412.34, "USD"));
        Assert.Null(MarketHoldings.Worth(-5, 412.34, "USD"));
        Assert.Null(MarketHoldings.Worth(double.NaN, 412.34, "USD"));
        Assert.Null(MarketHoldings.Worth(10, null, "USD"));
        Assert.Null(MarketHoldings.Worth(10, double.NaN, "USD"));
        Assert.Null(MarketHoldings.Worth(10, double.PositiveInfinity, "USD"));
    }

    /// <summary>A sum to its currency's minor unit (§494: the yen none, the dinar three), grouped, the code after it.</summary>
    [Theory]
    [InlineData(4123.4, "USD", "4,123.40 USD")]
    [InlineData(1234567.891, "EUR", "1,234,567.89 EUR")]
    [InlineData(0.5, "CHF", "0.50 CHF")]
    [InlineData(0.004, "USD", "0.00 USD")]
    [InlineData(12.346, "", "12.35")]
    [InlineData(61849.6, "JPY", "61,850 JPY")]
    [InlineData(12.3456, "KWD", "12.346 KWD")]
    public void ASumIsWrittenToTheCentWithItsCode(double value, string currency, string written) =>
        Assert.Equal(written, MarketHoldings.Money(value, currency));

    /// <summary>
    /// §494: what stands where the worth stood once the worth is the main figure: the amount and the price of
    /// one where there is room, the price of one alone where there is less.
    /// </summary>
    [Fact]
    public void WhereTheWorthStoodTheAmountAndThePriceOfOne()
    {
        Assert.Equal(("10 shares × 412.34 USD", "412.34 USD"), MarketHoldings.Each(10, MarketKind.Stock, "", "412.34", "USD"));
        Assert.Equal(("2.5 shares × 412.34 USD", "412.34 USD"), MarketHoldings.Each(2.5, MarketKind.Stock, "", "412.34", "USD"));
        Assert.Equal(("1 share × 412.34 USD", "412.34 USD"), MarketHoldings.Each(1, MarketKind.Stock, "", "412.34", "USD"));
        Assert.Equal(("0.5 BTC × 62,000.00 USD", "62,000.00 USD"), MarketHoldings.Each(0.5, MarketKind.Crypto, "BTC", "62,000.00", "USD"));
        Assert.Equal(("100 shares × 1,234.50 GBp", "1,234.50 GBp"), MarketHoldings.Each(100, MarketKind.Stock, "", "1,234.50", "GBp"));
    }

    // --- What the widget says ------------------------------------------------------------------

    private static MarketCache Kept(string json) => new()
    {
        FetchedUtc = DateTime.UtcNow,
        Key = "stock:MSFT 1d",
        Quote = MarketQuotes.Parse(json),
    };

    /// <summary>
    /// §494 (the person: "when i enter the amount owned in the markets widget it should actually show the
    /// total amount instead of the single stock price. the places of the two values should basically be
    /// swapped"): with an amount owned the main figure is what it is worth at the price now, and the price of
    /// one stands where the worth stood — over a day as over five years; with none set, the price as before.
    /// </summary>
    [Fact]
    public void AReadingSaysTheHoldingsWorthFirstAndThePriceOfOneWhereItStood()
    {
        var now = DateTime.UtcNow;
        var day = new MarketOptions { Symbol = "MSFT", Range = MarketRange.Day, Holding = 10 };
        var dayAnswer = MarketOffscreenTests.Answer("MSFT", "Microsoft Corporation", price: 412.34, percent: -1.24, previous: 417.5);
        var held = MarketReading.Of(Kept(dayAnswer), day, now, null);
        Assert.Equal(("4,123.40", "USD", "-1.24%"), (held.Price, held.Currency, held.Percent));
        Assert.Equal(("10 shares × 412.34 USD", "412.34 USD"), held.Holding);

        var five = new MarketOptions { Symbol = "MSFT", Holding = 2.5 };
        var fiveAnswer = MarketRangeTests.Ranged("MSFT", 261, 7 * 86400, 289, 412.34, 289.1);
        var reading = MarketReading.Of(Kept(fiveAnswer), five, now, null);
        Assert.Equal(MarketRange.FiveYears, reading.Range);
        Assert.Equal("1,030.85", reading.Price);
        Assert.Equal(("2.5 shares × 412.34 USD", "412.34 USD"), reading.Holding);
        // The change is the same in per cent whether of one share or of all of them.
        var alone = MarketReading.Of(Kept(fiveAnswer), new MarketOptions { Symbol = "MSFT" }, now, null);
        Assert.Equal(alone.Percent, reading.Percent);

        // None owned: the price of one, as it always was, and nothing beside it.
        Assert.Equal(("412.34", "USD"), (alone.Price, alone.Currency));
        Assert.Null(alone.Holding);
        // Before a price, nothing to work it out from.
        Assert.Null(MarketReading.Of(null, five, now, null).Holding);
        // London in pence: the worth in pounds, the price of one in pence as quoted.
        var london = MarketReading.Of(Kept(MarketOffscreenTests.Answer("VOD.L", "Vodafone", "GBp", price: 72.5, percent: 0.4, previous: 72.2)), new MarketOptions { Symbol = "VOD.L", Range = MarketRange.Day, Holding = 1000 }, now, null);
        Assert.Equal(("725.00", "GBP", ("1,000 shares × 72.50 GBp", "72.50 GBp")), (london.Price, london.Currency, london.Holding!.Value));
    }

    // --- The file --------------------------------------------------------------------------------

    private static BarItem Share(double holding)
    {
        var stock = ListKind.Markets.New();
        stock.Id = "share";
        stock.Market = new MarketOptions { Symbol = "MSFT", Holding = holding };
        var folder = Configs.Holding(stock);
        folder.Id = "f";
        return folder;
    }

    private static JsonElement Saved(Config config)
    {
        using var written = JsonDocument.Parse(config.ToJson());
        return Configs.Find(written.RootElement, "market")!.Value.Clone();
    }

    /// <summary>The holding is written with the stock's other settings and read back as written, decimals and all.</summary>
    [Fact]
    public void TheHoldingIsWrittenAndReadBack()
    {
        var config = Configs.With(Share(12.5));
        Assert.Equal(12.5, Saved(config).GetProperty("holding").GetDouble());

        var back = Configs.RoundTrip(config, out var changed);
        Assert.False(changed);
        Assert.Equal(12.5, Configs.Islands(back)[0].Folder!.Cells.First().Island.Market!.Holding);
    }

    /// <summary>No holding is not written at all, so a file without one is the file it was.</summary>
    [Fact]
    public void NoHoldingIsNotWritten()
    {
        var config = Configs.With(Share(0));
        Assert.False(Saved(config).TryGetProperty("holding", out _));
        var back = Configs.RoundTrip(config, out var changed);
        Assert.False(changed);
        Assert.Equal(0, Configs.Islands(back)[0].Folder!.Cells.First().Island.Market!.Holding);
    }

    /// <summary>A hand-edited file's negative, or past eight decimals, is held to an amount or none.</summary>
    [Theory]
    [InlineData("-4", 0)]
    [InlineData("0", 0)]
    [InlineData("0.123456789", 0.12345679)]
    [InlineData("7", 7)]
    public void AHoldingInAFileIsHeldToAnAmount(string written, double kept)
    {
        var config = Configs.Repaired($$"""
            { "layouts": [ { "id": "default", "name": "Default", "islands": [
              { "id": "f", "kind": "folderList", "folder": { "lists": [ [ { "island": { "id": "share", "kind": "markets", "market": { "symbol": "MSFT", "holding": {{written}} } } } ] ] } } ] } ] }
            """);
        Assert.Equal(kept, Configs.Islands(config)[0].Folder!.Cells.First().Island.Market!.Holding);
    }

    /// <summary>
    /// The settings app sets a holding while the dock saves the folder's alignment: both
    /// survive the three-way merge; and a holding taken away on one side goes, while the other side
    /// changed something else.
    /// </summary>
    [Fact]
    public void AHoldingSurvivesTheMerge()
    {
        var @base = Configs.With(Share(0)).ToJson();

        var settings = Config.Parse(@base);
        settings.DefaultLayout.Islands[0].Folder!.Cells.First().Island.Market!.Holding = 3.75;
        var dock = Config.Parse(@base);
        dock.DefaultLayout.Islands[0].Folder!.Align = FolderAlign.Right;
        var merged = Configs.Repaired(ConfigMerge.Merge(@base, settings.ToJson(), dock.ToJson()));
        Assert.Equal(3.75, Configs.Islands(merged)[0].Folder!.Cells.First().Island.Market!.Holding);
        Assert.Equal(FolderAlign.Right, Configs.Islands(merged)[0].Folder!.Align);

        // Now held: the settings app clears it while the dock changes the range.
        var held = Configs.With(Share(3.75)).ToJson();
        var cleared = Config.Parse(held);
        cleared.DefaultLayout.Islands[0].Folder!.Cells.First().Island.Market!.Holding = 0;
        var refreshed = Config.Parse(held);
        refreshed.DefaultLayout.Islands[0].Folder!.Align = FolderAlign.Right;
        var after = Configs.Repaired(ConfigMerge.Merge(held, cleared.ToJson(), refreshed.ToJson()));
        Assert.Equal(0, Configs.Islands(after)[0].Folder!.Cells.First().Island.Market!.Holding);
        Assert.Equal(FolderAlign.Right, Configs.Islands(after)[0].Folder!.Align);
        Assert.False(JsonNode.Parse(ConfigMerge.Merge(held, cleared.ToJson(), refreshed.ToJson()))!
            ["layouts"]![0]!["islands"]![0]!["folder"]!["lists"]![0]![0]!["island"]!["market"]!.AsObject().ContainsKey("holding"));
    }

    // --- The settings ------------------------------------------------------------------------------

    /// <summary>
    /// The amount is a text field of a markets widget's menu, a share's and a coin's (never a rate's):
    /// what is typed is kept to eight decimals, none for nothing or 0, a comma or a point its decimals'
    /// mark (Currencies.Parse); words that are no amount change nothing, and the field shows the amount
    /// kept, ungrouped, so it reads back as itself.
    /// </summary>
    [Fact]
    public void TheAmountYouOwnIsTypedInAStocksMenu()
    {
        Assert.DoesNotContain(OptionRows.Of(ListKind.Clock.New()), row => row.Key == MarketRows.Holding);
        var stock = ListKind.Markets.New();
        OptionRow Row() => OptionRows.Of(stock).Single(row => row.Key == MarketRows.Holding);
        Assert.Equal((OptionRowKind.Text, OptionField.Text, "", S.SettingsWidgetMarketHoldingNone), (Row().Kind, Row().Field, Row().Text, Row().Placeholder));

        Assert.True(Row().SetText!("2,5"));
        Assert.Equal(2.5, stock.Market!.Holding);
        Assert.Equal("2.5", Row().Text);
        Assert.False(Row().SetText!("2.5"));
        Assert.True(Row().SetText!(" 1'250.123456789 "));
        Assert.Equal(1250.12345679, stock.Market.Holding);
        Assert.Equal("1250.12345679", Row().Text);
        Assert.True(Row().SetText!("1.000.000,5"));
        Assert.Equal(1_000_000.5, stock.Market.Holding);
        // No number: nothing changes; nothing or 0 or less: none.
        Assert.False(Row().SetText!("ten"));
        Assert.False(Row().SetText!("1,2,3.4,5"));
        Assert.Equal(1_000_000.5, stock.Market.Holding);
        Assert.True(Row().SetText!(""));
        Assert.Equal(0, stock.Market.Holding);
        Assert.Equal("", Row().Text);
        Assert.False(Row().SetText!("-3"));
        Assert.Null(MarketHoldings.Parse("5e20", NumberMarks.Invariant));
        Assert.Null(MarketHoldings.Parse("99999999999999", NumberMarks.Invariant));
        // A rate has no holding: its amount is what it is said for.
        stock.Market.Symbol = "EURCHF=X";
        Assert.DoesNotContain(OptionRows.Of(stock), row => row.Key == MarketRows.Holding);
    }

    private static (double, string) Round((double Value, string Currency)? worth)
    {
        Assert.NotNull(worth);
        return (Math.Round(worth.Value.Value, 6), worth.Value.Currency);
    }
}
