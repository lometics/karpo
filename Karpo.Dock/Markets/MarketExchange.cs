// §494: the rate a markets widget's amounts are shown in another currency at (MarketOptions.Currency: a share
// listed in dollars shown in francs). The rate is one more question to the same store — the pair of the
// listed currency in the shown one over a day ("currency:USDCHF 1d"), by a rate's routes: Yahoo's pair
// direct, inverse or through the dollar, then the ECB's reference rates — so it is fetched in the background,
// kept in markets.json, asked again on the widget's interval and shared with every other widget asking it.
// The widget's own drawing never waits for it: until it has come the figures stand as listed. The listed
// currency is the price's once one has come, before that what the search named (MarketMoney.Listed), so the
// rate is asked beside the first price rather than after it.

using Karpo.Core;

namespace Karpo.Dock.Markets;

/// <summary>§494: one of <paramref name="From"/> in <paramref name="To"/>, now: what a widget's amounts are converted at.</summary>
internal readonly record struct MarketRate(string From, string To, double Rate);

/// <summary>A widget's rate, followed in <paramref name="store"/>; <paramref name="wakes"/> is woken when it comes (none for a test). See the top of the file.</summary>
internal sealed class MarketExchange(MarketStore store, CellIsland? wakes)
{
    private MarketAsk? asked;

    /// <summary>What it asks the store; null where nothing is converted.</summary>
    internal MarketAsk? Asked => asked;

    /// <summary>
    /// The question for the rate <paramref name="options"/> are shown at: the currency they are listed in —
    /// <paramref name="quote"/>'s, else what the search named — in the one they are shown in, over a day.
    /// Null where nothing is converted: shown as listed, a rate, or nothing says what it is listed in yet.
    /// </summary>
    internal static MarketAsk? AskOf(MarketOptions options, MarketQuote? quote)
    {
        var shown = options.ShownIn;
        if (shown.Length == 0)
        {
            return null;
        }
        var listed = quote is { Currency.Length: > 0 } said ? MarketReading.PricedIn(options, said.Currency) : MarketMoney.Listed(options);
        return Currencies.Code(MarketMoney.Whole(listed).Code) is { } from && from != shown
            ? new MarketAsk(MarketKind.Currency, from, shown, MarketRange.Day, 0)
            : null;
    }

    /// <summary>Follows the rate <paramref name="options"/> want now, asking it where it is due. On the dock's thread.</summary>
    public void Follow(MarketOptions options, MarketQuote? quote)
    {
        var ask = AskOf(options, quote);
        if (ask != asked)
        {
            asked = ask;
            store.Use(this, ask, wakes);
        }
        if (asked is not null)
        {
            store.Poll(this);
        }
    }

    /// <summary>The rate now, where it has come; null before, and where nothing is converted.</summary>
    public MarketRate? Rate =>
        asked is { } ask && store.Read(this).Shown?.Quote?.Now is { } now ? new MarketRate(ask.Name, ask.To, now) : null;

    /// <summary>The store woke the widget: its news taken.</summary>
    public void Deliver()
    {
        if (asked is not null)
        {
            store.Deliver(this);
        }
    }

    /// <summary>"Refresh": the rate fetched again with the price.</summary>
    public void Refresh()
    {
        if (asked is not null)
        {
            _ = store.Ask(this);
        }
    }

    /// <summary>The widget goes: its place among the store's users with it.</summary>
    public void Release()
    {
        asked = null;
        store.Release(this);
    }
}
