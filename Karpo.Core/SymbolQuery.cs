// The markets widget's search (MarketSearch, docs\widgets.md §258): when what is typed is worth searching for, what
// counts as a symbol at all, and how to read the two answers the service gives —
// a list of listings to choose from, and the one instrument a chosen symbol
// turns out to be.
//
// Reading the answers lives here rather than beside the fetch so that it can be
// tested without a network: a payload that changes shape should cost a field and
// be caught by a test, not surface as an empty widget on somebody's taskbar.
//
// The bar's own price reading (Karpo.Dock\Markets) reads the chart a second time for
// what it draws. Only the handful of fields a card shows are read here, and the one
// thing that must not drift — what an instrument is called — is Display.
//
// In the core, so a markets widget's menu on the bar searches, checks and says things one way.

using System.Text.Json;

namespace Karpo.Core;

/// <summary>
/// One listing the search offered: what to ask for, what it is called, where it trades and what
/// kind of thing it is. The same company trades in several places — Apple is AAPL on NASDAQ and
/// AAPL.TO in Toronto — so the exchange is not decoration, it is how two listings are told apart.
/// </summary>
public sealed record Listing(string Symbol, string Name, string Exchange, string Kind)
{
    /// <summary>The line under the name in the list: "NASDAQ · Equity".</summary>
    public string Detail => string.Join(" · ", new[] { Exchange, Kind }.Where(part => part.Length > 0));

    /// <summary>What the widget will ask for, which is what the box shows once one is picked.</summary>
    public override string ToString() => Symbol;
}

/// <summary>
/// What the price service said about one symbol: enough to prove it resolves, and enough to tell
/// which listing it is. The currency is here and not on <see cref="Listing"/> because the search
/// does not give one, and a price without its currency means nothing.
/// </summary>
public sealed record Instrument(string Symbol, string Name, string Currency, string Exchange, double? Price, int? PriceHint);

public static class SymbolQuery
{
    /// <summary>
    /// Fewer letters than this match half the market, so the box waits for more before it searches.
    /// A symbol that is typed out and submitted is not held to this — "F" is Ford and "V" is Visa —
    /// because that one is looked up exactly rather than searched for.
    /// </summary>
    public const int LeastLetters = 2;

    /// <summary>Longer than any real symbol; anything longer is a mistake rather than a ticker.</summary>
    public const int MostSymbol = 20;

    /// <summary>How long the box waits after the last keystroke before it asks.</summary>
    public static readonly TimeSpan Wait = TimeSpan.FromMilliseconds(300);

    /// <summary>The most listings the box offers: a screenful, not a market.</summary>
    public const int Most = 8;

    /// <summary>
    /// The search request, written here rather than beside the fetch so that the one thing worth
    /// checking about it — that it carries a query and nothing else, no key and nothing personal —
    /// can be checked without a network.
    /// </summary>
    public static string SearchUrl(string text) =>
        "https://query1.finance.yahoo.com/v1/finance/search"
        + $"?q={Uri.EscapeDataString(text)}&quotesCount={Most}&newsCount=0";

    /// <summary>The lookup request: the bar's own, asking for as little of the day as it can.</summary>
    public static string LookUpUrl(string symbol) =>
        "https://query1.finance.yahoo.com/v8/finance/chart/"
        + Uri.EscapeDataString(symbol.Trim())
        + "?range=1d&interval=1d";

    /// <summary>What a query really asks for: trimmed, and with the spaces inside it squeezed.</summary>
    public static string Clean(string? text) =>
        string.Join(' ', (text ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries));

    /// <summary>
    /// What is typed, read as a symbol: upper case, with the spaces taken out rather than squeezed,
    /// because no symbol has one in it and "aapl " is plainly meant as AAPL.
    /// </summary>
    public static string Symbol(string? text) =>
        string.Concat((text ?? "").Where(letter => !char.IsWhiteSpace(letter))).ToUpperInvariant();

    /// <summary>
    /// Whether what is in the box now is worth asking the service for: long enough, and not what
    /// the last search already asked (<paramref name="asked"/>, null when none has).
    /// </summary>
    public static bool Searches(string? text, string? asked)
    {
        var clean = Clean(text);
        return clean.Length >= LeastLetters
            && !string.Equals(clean, Clean(asked), StringComparison.CurrentCultureIgnoreCase);
    }

    /// <summary>
    /// Whether <paramref name="text"/> could be a symbol at all. The alphabet is the one the
    /// service uses — letters, digits, and the four marks that carry meaning in a ticker: a dot for
    /// a listing abroad (AAPL.TO), a dash for a share class or a coin (BRK-B, BTC-USD), a caret for
    /// an index (^GSPC) and an equals for a future or a currency pair (GC=F, EURUSD=X).
    ///
    /// A space is what tells a name from a symbol, and no symbol has one: "apple inc" is a thing to
    /// search for, and squeezing it into APPLEINC and looking that up would answer a question
    /// nobody asked with "no prices for APPLEINC".
    ///
    /// This is a gate in front of the request, not a claim that the symbol exists: only the service
    /// can say that, and the card asks it before it saves anything. Its job is to turn a pasted
    /// sentence or URL into a plain "that isn't a symbol" without a request going out at all.
    /// </summary>
    public static bool Acceptable(string? text)
    {
        var trimmed = (text ?? "").Trim();
        return trimmed.Length is > 0 and <= MostSymbol
            && !trimmed.Any(char.IsWhiteSpace)
            && trimmed.ToUpperInvariant().All(letter =>
                char.IsAsciiLetterOrDigit(letter) || letter is '.' or '-' or '^' or '=');
    }

    /// <summary>A symbol as a person would say it, upper case. One definition, in Karpo.Core.</summary>
    public static string Display(string? symbol) => (symbol ?? "").Trim().ToUpperInvariant();

    /// <summary>
    /// What a symbol search remembers about a listing (<see cref="MarketOptions.Detail"/>): what and
    /// where, "Apple Inc. · NASDAQ · USD", never the price. A markets line's and a markets face's alike.
    /// </summary>
    public static string Caption(Instrument instrument) =>
        string.Join(" · ", new[] { instrument.Name, instrument.Exchange, instrument.Currency }.Where(part => part.Length > 0).Distinct());

    /// <summary>
    /// The listings in a search answer, best match first. Anything without a symbol is dropped, and
    /// so is anything the service itself does not price — a news hit or a research note is not
    /// something a widget can show a number for.
    /// </summary>
    public static List<Listing> Listings(string json)
    {
        var found = new List<Listing>();
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return found;
        }
        using (document)
        {
            if (!document.RootElement.TryGetProperty("quotes", out var quotes) || quotes.ValueKind != JsonValueKind.Array)
            {
                return found;
            }
            foreach (var quote in quotes.EnumerateArray())
            {
                var symbol = Text(quote, "symbol");
                if (symbol.Length == 0 || !Acceptable(symbol))
                {
                    continue;
                }
                var name = Text(quote, "longname") is { Length: > 0 } longName ? longName : Text(quote, "shortname");
                found.Add(new Listing(
                    symbol,
                    name.Length > 0 ? name : Display(symbol),
                    Text(quote, "exchDisp") is { Length: > 0 } exchange ? exchange : Text(quote, "exchange"),
                    Text(quote, "typeDisp")));
            }
        }
        return found;
    }

    /// <summary>
    /// What a chart answer says about the symbol it was asked for, or null when it says nothing —
    /// a symbol that does not resolve, an error where a result should be, a result with no price.
    /// Null is what the card turns into "there is no such symbol", so a typo is caught at the
    /// moment it is set rather than by a widget that shows nothing for ever.
    /// </summary>
    public static Instrument? Read(string json)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }
        using (document)
        {
            if (!document.RootElement.TryGetProperty("chart", out var chart))
            {
                return null;
            }
            if (chart.TryGetProperty("error", out var failed) && failed.ValueKind is not JsonValueKind.Null)
            {
                return null;
            }
            if (!chart.TryGetProperty("result", out var results)
                || results.ValueKind != JsonValueKind.Array
                || results.GetArrayLength() == 0
                || !results[0].TryGetProperty("meta", out var meta))
            {
                return null;
            }
            var symbol = Text(meta, "symbol");
            if (symbol.Length == 0)
            {
                return null;
            }
            var name = Text(meta, "longName") is { Length: > 0 } longName ? longName : Text(meta, "shortName");
            var hint = Number(meta, "priceHint");
            var price = Number(meta, "regularMarketPrice");
            return new Instrument(
                symbol,
                name.Length > 0 ? name : Display(symbol),
                Text(meta, "currency"),
                Text(meta, "fullExchangeName") is { Length: > 0 } full ? full : Text(meta, "exchangeName"),
                double.IsNaN(price) ? null : price,
                hint is >= 0 and <= 8 ? (int)hint : null);
        }
    }

    /// <summary>
    /// The line the card shows once a symbol is set: what it is, where it trades, and the price it
    /// has now with its currency, so two listings of one company are plainly different things.
    /// </summary>
    public static string Describe(Instrument? instrument)
    {
        if (instrument is null)
        {
            return "";
        }
        var parts = new List<string>();
        if (instrument.Name.Length > 0)
        {
            parts.Add(instrument.Name);
        }
        if (instrument.Exchange.Length > 0)
        {
            parts.Add(instrument.Exchange);
        }
        if (Price(instrument) is { Length: > 0 } price)
        {
            parts.Add(price);
        }
        return string.Join(" · ", parts);
    }

    /// <summary>
    /// The price with its currency code, to the precision the service says this instrument is
    /// quoted to: a share to the penny, a penny stock to the fourth decimal. The same rule the bar
    /// uses, for the same reason.
    /// </summary>
    public static string Price(Instrument? instrument)
    {
        if (instrument?.Price is not { } price || !double.IsFinite(price))
        {
            return "";
        }
        var size = Math.Abs(price);
        var places = instrument.PriceHint is { } hint and >= 0 and <= 8 ? hint : size >= 1000 ? 0 : size >= 1 ? 2 : 4;
        var number = price.ToString("N" + places.ToString(System.Globalization.CultureInfo.InvariantCulture),
            System.Globalization.CultureInfo.InvariantCulture);
        return instrument.Currency.Length > 0 ? $"{instrument.Currency} {number}" : number;
    }

    static string Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()?.Trim() ?? ""
            : "";

    static double Number(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetDouble(out var number)
        && double.IsFinite(number)
            ? number
            : double.NaN;
}
