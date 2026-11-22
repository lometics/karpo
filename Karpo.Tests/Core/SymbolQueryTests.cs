// The stock widget's symbol box (Karpo.Core\SymbolQuery.cs): when what
// is typed is worth searching for, what could be a symbol at all, and what the
// two answers the service gives actually say.
//
// The fixtures are trimmed copies of real answers from Yahoo's search and chart
// endpoints, so the field names and their shapes are the ones the settings app
// really gets. Nothing here reaches the network, and no key or cookie exists to
// be committed.

using Karpo.Core;

namespace Karpo.Tests.Core;

public sealed class SymbolQueryTests
{
    // --- Typing -----------------------------------------------------------------------

    [Theory]
    [InlineData("apple", null, true)]
    [InlineData("ap", null, true)]
    [InlineData("a", null, false)]
    [InlineData("", null, false)]
    [InlineData("   ", null, false)]
    [InlineData(null, null, false)]
    // The same words again ask nothing: the list is already what they found.
    [InlineData("apple", "apple", false)]
    [InlineData("  apple ", "apple", false)]
    [InlineData("APPLE", "apple", false)]
    [InlineData("apple in", "apple", true)]
    public void TypingSearchesOnceItIsWorthIt(string? text, string? asked, bool searches) =>
        Assert.Equal(searches, SymbolQuery.Searches(text, asked));

    [Fact]
    public void TheWaitIsShortEnoughToFeelLiveAndLongEnoughToSkipKeystrokes()
    {
        Assert.InRange(SymbolQuery.Wait.TotalMilliseconds, 150, 600);
        Assert.Equal(2, SymbolQuery.LeastLetters);
    }

    [Theory]
    [InlineData("  aapl ", "AAPL")]
    [InlineData("brk b", "BRKB")]
    [InlineData("eurusd=x", "EURUSD=X")]
    [InlineData(null, "")]
    public void WhatIsTypedReadsAsASymbol(string? text, string expected) =>
        Assert.Equal(expected, SymbolQuery.Symbol(text));

    // --- What could be a symbol -------------------------------------------------------

    [Theory]
    // The four marks that carry meaning in a ticker, and a one-letter symbol, which is Ford.
    [InlineData("AAPL")]
    [InlineData("F")]
    [InlineData("^GSPC")]
    [InlineData("BRK-B")]
    [InlineData("BTC-USD")]
    [InlineData("EURUSD=X")]
    [InlineData("AAPL.TO")]
    [InlineData("aapl")]
    public void ATickerIsAcceptable(string symbol) => Assert.True(SymbolQuery.Acceptable(symbol));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("apple inc")]
    [InlineData("https://example.com/AAPL")]
    [InlineData("AAPL/../../etc")]
    [InlineData("AAPL?range=1d")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAA")]
    public void AnythingElseIsNot(string? text) => Assert.False(SymbolQuery.Acceptable(text));

    /// <summary>
    /// The gate is in front of the request, so a pasted URL never becomes one. That is the whole
    /// point of checking before asking: the service is not the place to find out.
    /// </summary>
    [Fact]
    public void TheRequestsAreOneUrlEachWithNothingPersonalInThem()
    {
        var search = SymbolQuery.SearchUrl("euro dollar");
        Assert.StartsWith("https://query1.finance.yahoo.com/v1/finance/search", search, StringComparison.Ordinal);
        Assert.Contains("euro%20dollar", search, StringComparison.Ordinal);
        Assert.DoesNotContain(" ", search, StringComparison.Ordinal);

        var look = SymbolQuery.LookUpUrl("^GSPC");
        Assert.StartsWith("https://query1.finance.yahoo.com/v8/finance/chart/", look, StringComparison.Ordinal);
        Assert.Contains("%5EGSPC", look, StringComparison.Ordinal);
        // No key, no token, no account: there is none, and there is nowhere one could be hiding.
        Assert.DoesNotContain("key", search, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("key", look, StringComparison.OrdinalIgnoreCase);
    }

    // --- Reading a search answer ------------------------------------------------------

    private const string Found = """
        {
          "explains": [], "count": 4,
          "quotes": [
            { "exchange": "NMS", "shortname": "Apple Inc.", "quoteType": "EQUITY", "symbol": "AAPL",
              "typeDisp": "Equity", "longname": "Apple Inc.", "exchDisp": "NASDAQ" },
            { "exchange": "TOR", "shortname": "APPLE CDR (CAD HEDGED)", "quoteType": "EQUITY", "symbol": "AAPL.TO",
              "typeDisp": "Equity", "longname": "Apple Inc.", "exchDisp": "Toronto" },
            { "exchange": "CCY", "shortname": "EUR/USD", "quoteType": "CURRENCY", "symbol": "EURUSD=X",
              "typeDisp": "Currency", "longname": "EUR/USD", "exchDisp": "CCY" },
            { "exchange": "NMS", "quoteType": "EQUITY", "typeDisp": "Equity", "exchDisp": "NASDAQ" }
          ],
          "news": []
        }
        """;

    [Fact]
    public void ASearchAnswerReadsAsListings()
    {
        var listings = SymbolQuery.Listings(Found);
        // Four quotes, three listings: the one with no symbol is nothing a widget could ask for.
        Assert.Equal(3, listings.Count);
        Assert.Equal("AAPL", listings[0].Symbol);
        Assert.Equal("Apple Inc.", listings[0].Name);
        Assert.Equal("NASDAQ", listings[0].Exchange);
        Assert.Equal("Equity", listings[0].Kind);
        // The same company in two places, which is the whole reason the exchange is shown.
        Assert.Equal("AAPL.TO", listings[1].Symbol);
        Assert.Equal("Apple Inc.", listings[1].Name);
        Assert.Equal("Toronto", listings[1].Exchange);
        Assert.Equal("NASDAQ · Equity", listings[0].Detail);
        // A currency pair comes back named the way a person says it, from the service itself.
        Assert.Equal("EURUSD=X", listings[2].Symbol);
        Assert.Equal("EUR/USD", listings[2].Name);
    }

    [Fact]
    public void AnAnswerThatIsNotOneIsAnEmptyList()
    {
        Assert.Empty(SymbolQuery.Listings("{ not json"));
        Assert.Empty(SymbolQuery.Listings("""{"quotes":[]}"""));
        Assert.Empty(SymbolQuery.Listings("""{"count":0}"""));
        Assert.Empty(SymbolQuery.Listings("""{"quotes":"lots"}"""));
    }

    // --- Reading a chart answer -------------------------------------------------------

    private const string Share = """
        {"chart":{"result":[{"meta":{
          "currency":"USD","symbol":"AAPL","exchangeName":"NMS","fullExchangeName":"NasdaqGS",
          "regularMarketPrice":336.16,"priceHint":2,"longName":"Apple Inc.","shortName":"Apple Inc."
        }}],"error":null}}
        """;

    private const string Pair = """
        {"chart":{"result":[{"meta":{
          "currency":"USD","symbol":"EURUSD=X","exchangeName":"CCY","fullExchangeName":"CCY",
          "regularMarketPrice":1.1395,"priceHint":4,"longName":"EUR/USD","shortName":"EUR/USD"
        }}],"error":null}}
        """;

    [Fact]
    public void AChartAnswerSaysWhichListingItIs()
    {
        var apple = SymbolQuery.Read(Share);
        Assert.NotNull(apple);
        Assert.Equal("AAPL", apple!.Symbol);
        Assert.Equal("Apple Inc.", apple.Name);
        Assert.Equal("USD", apple.Currency);
        Assert.Equal("NasdaqGS", apple.Exchange);
        Assert.Equal(336.16, apple.Price);
        Assert.Equal(2, apple.PriceHint);
    }

    /// <summary>
    /// Null means "there is no such symbol", which is what stops a typo becoming a widget that
    /// draws an empty cell for ever. A service that cannot be reached is a different thing and
    /// never comes through here.
    /// </summary>
    [Fact]
    public void NothingForThatSymbolIsNull()
    {
        Assert.Null(SymbolQuery.Read("{ not json"));
        Assert.Null(SymbolQuery.Read("""{"chart":{"result":null,"error":{"code":"Not Found","description":"No data found, symbol may be delisted"}}}"""));
        Assert.Null(SymbolQuery.Read("""{"chart":{"result":[],"error":null}}"""));
        Assert.Null(SymbolQuery.Read("""{"chart":{"result":[{"meta":{}}],"error":null}}"""));
        Assert.Null(SymbolQuery.Read("""{"finance":{"result":[]}}"""));
    }

    // --- What the card shows ----------------------------------------------------------

    [Fact]
    public void TheCardSaysWhatItIsWhereItTradesAndInWhatMoney()
    {
        Assert.Equal("Apple Inc. · NasdaqGS · USD 336.16", SymbolQuery.Describe(SymbolQuery.Read(Share)));
        Assert.Equal("EUR/USD · CCY · USD 1.1395", SymbolQuery.Describe(SymbolQuery.Read(Pair)));
        Assert.Equal("", SymbolQuery.Describe(null));
    }

    /// <summary>
    /// A pair is quoted to four decimals and a share to two, and the service says which — Karpo
    /// does not decide it per kind. The decimals are always written out, so a rate that moves from
    /// 1.0832 to 1.1000 stays the same number of characters.
    /// </summary>
    [Fact]
    public void APriceIsWrittenToTheInstrumentsOwnPrecision()
    {
        Assert.Equal("USD 1.1000", SymbolQuery.Price(new Instrument("EURUSD=X", "EUR/USD", "USD", "CCY", 1.1, 4)));
        Assert.Equal("USD 1.0832", SymbolQuery.Price(new Instrument("EURUSD=X", "EUR/USD", "USD", "CCY", 1.0832, 4)));
        Assert.Equal(
            SymbolQuery.Price(new Instrument("EURUSD=X", "EUR/USD", "USD", "CCY", 1.1, 4)).Length,
            SymbolQuery.Price(new Instrument("EURUSD=X", "EUR/USD", "USD", "CCY", 1.0832, 4)).Length);
        Assert.Equal("USD 336.16", SymbolQuery.Price(new Instrument("AAPL", "Apple Inc.", "USD", "NasdaqGS", 336.16, 2)));
        // No hint: the number's own size decides, as it did before any of this.
        Assert.Equal("USD 6,417", SymbolQuery.Price(new Instrument("^GSPC", "S&P 500", "USD", "SNP", 6417.29, null)));
        Assert.Equal("", SymbolQuery.Price(new Instrument("X", "", "", "", null, 2)));
    }

    // A share's symbol as typed, upper case. A pair and a coin are a markets widget's own types (§246),
    // named by their codes there, never a share's symbol to rename.
    [Theory]
    [InlineData("aapl", "AAPL")]
    [InlineData(" nesn.sw ", "NESN.SW")]
    [InlineData("^GSPC", "^GSPC")]
    [InlineData("", "")]
    public void ASymbolIsWrittenAsItIsSaid(string symbol, string expected) =>
        Assert.Equal(expected, SymbolQuery.Display(symbol));
}
