// The coins the markets widget offers before anything is typed (docs\widgets.md §246): the most traded,
// by their tickers, with their names and CoinGecko's ids for them. A coin not here is found by the
// price service's search (its "XXX-USD" listings) and is just as good; the table only saves a search
// for the ones nearly everybody means, and names them where an answer does not.

namespace Karpo.Core;

/// <summary>A coin: its ticker, its name, and CoinGecko's id for it.</summary>
public sealed record Coin(string Code, string Name, string GeckoId);

/// <summary>The coins offered first, and what a coin's ticker is written as; see the top of the file.</summary>
public static class Coins
{
    /// <summary>The most traded coins, by market value (2026-10), stablecoins after the rest.</summary>
    public static readonly IReadOnlyList<Coin> Common =
    [
        new("BTC", "Bitcoin", "bitcoin"),
        new("ETH", "Ethereum", "ethereum"),
        new("XRP", "XRP", "ripple"),
        new("BNB", "BNB", "binancecoin"),
        new("SOL", "Solana", "solana"),
        new("DOGE", "Dogecoin", "dogecoin"),
        new("TRX", "TRON", "tron"),
        new("ADA", "Cardano", "cardano"),
        new("LINK", "Chainlink", "chainlink"),
        new("AVAX", "Avalanche", "avalanche-2"),
        new("XLM", "Stellar", "stellar"),
        new("TON", "Toncoin", "the-open-network"),
        new("SUI", "Sui", "sui"),
        new("BCH", "Bitcoin Cash", "bitcoin-cash"), // not-ui: the coin's name
        new("LTC", "Litecoin", "litecoin"),
        new("DOT", "Polkadot", "polkadot"),
        new("SHIB", "Shiba Inu", "shiba-inu"), // not-ui: the coin's name
        new("XMR", "Monero", "monero"),
        new("UNI", "Uniswap", "uniswap"),
        new("ATOM", "Cosmos", "cosmos"),
        new("USDT", "Tether", "tether"),
        new("USDC", "USD Coin", "usd-coin"), // not-ui: the coin's name
    ];

    /// <summary><paramref name="code"/> as a coin's ticker is written: "BTC" from " btc "; null for anything that is none.</summary>
    public static string? Code(string? code)
    {
        var upper = (code ?? "").Trim().ToUpperInvariant();
        return upper.Length is >= 2 and <= 10 && upper.All(char.IsAsciiLetterOrDigit) && char.IsAsciiLetter(upper[0]) ? upper : null;
    }

    /// <summary>Whether <paramref name="code"/> is one of the <see cref="Common"/> coins.</summary>
    public static bool Known(string? code) => Find(code) is not null;

    /// <summary>The common coin <paramref name="code"/>; null for another.</summary>
    public static Coin? Find(string? code) => Common.FirstOrDefault(coin => coin.Code == Code(code));

    /// <summary>What <paramref name="code"/> is called: a common coin's name; null for another.</summary>
    public static string? Name(string? code) => Find(code)?.Name;

    /// <summary>The price service's symbol for <paramref name="coin"/> in <paramref name="currency"/>: "BTC-USD".</summary>
    public static string Symbol(string coin, string currency) => $"{coin}-{currency}";
}
