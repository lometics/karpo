// markets: the markets face's price (docs\widgets.md §246), read as a line's is: what the face tracks is its
// own (FolderFace.Market), never a line's; the folder registers it with the markets store as a user of its
// own, asks for it on its tick where it is due, and takes what came when the store wakes it — so the face is
// fresh with the folder shut, and a face and a line of one question are one fetch. A click on the face
// searches what it tracks in the browser (§263): anywhere on a button it is alone on, its own room among others.

using Karpo.Core;
using Karpo.Dock.Markets;

namespace Karpo.Dock;

internal sealed partial class FolderIsland
{
    // markets: the store every markets line reads — a test's where one was handed in (MarketIsland.Made) —,
    // what the face tracks while the folder wears it, what it asks, and what the store holds for it.
    private readonly MarketStore markets = MarketIsland.Made ?? (Samples.On ? Samples.Markets() : MarketStore.Shared);
    private MarketOptions? faceMarket;
    private MarketAsk? faceAsk;
    private (MarketCache? Shown, string? Problem, bool Fetching) marketQuote;

    // §494: the rate the face's amounts are shown in another currency at, where one is chosen.
    private MarketExchange? faceExchange;

    private MarketExchange FaceExchange => faceExchange ??= new MarketExchange(markets, this);

    /// <summary>markets: whether the folder asks a price for its face, so it ticks at the markets' pace.</summary>
    private bool AsksMarket => faceAsk is not null;

    /// <summary>markets: what the face reads now; nothing come yet where it asks nothing.</summary>
    internal MarketReading MarketReading() =>
        Markets.MarketReading.Of(marketQuote.Shown, faceMarket ?? MarketFaces.Of(options) ?? MarketOptions.New(), Clock.Now().ToUniversalTime(), marketQuote.Problem, markets.Updates, faceExchange?.Rate);

    /// <summary>markets: the face's last answer, why a newer one is not there, and whether one is coming.</summary>
    internal (MarketCache? Shown, string? Problem, bool Fetching) MarketQuote => markets.Read(this);

    /// <summary>markets: what the face asks; null where it wears none or asks nothing.</summary>
    internal MarketAsk? MarketAsk => faceAsk;

    /// <summary>
    /// markets: the face follows what it tracks — registered with the store, asked for where it is due, and
    /// read — or nothing where the folder wears no such face. The same question again changes nothing; a
    /// name, an amount or a style is only how it is shown.
    /// </summary>
    private void FollowMarket()
    {
        var next = MarketFaces.Of(options) is { } own ? own.Copy() : null;
        next?.Normalize();
        var ask = next is null ? null : Markets.MarketAsk.Of(next);
        var same = faceMarket is not null && next is not null && ask == faceAsk;
        faceMarket = next;
        if (!same)
        {
            faceAsk = ask;
            markets.Use(this, ask);
            markets.Poll(this);
            marketQuote = markets.Read(this);
        }
        FollowRate();
    }

    /// <summary>markets, §494: the face's rate followed as its currency and its price say; none where it wears no such face.</summary>
    private void FollowRate()
    {
        if (faceMarket is { } shown)
        {
            FaceExchange.Follow(shown, marketQuote.Shown?.Quote);
        }
        else
        {
            faceExchange?.Release();
        }
    }

    /// <summary>markets: the store woke the folder, or its tick came: news raised, a fetch that is due started, and the answer taken.</summary>
    private void SampleMarket(bool everything)
    {
        if (!AsksMarket)
        {
            return;
        }
        if (everything)
        {
            markets.Deliver(this);
            faceExchange?.Deliver();
        }
        markets.Poll(this);
        marketQuote = markets.Read(this);
        FollowRate();
    }

    /// <summary>markets: the folder goes, and with it its place among the store's users, its rate's too.</summary>
    private void ReleaseMarket()
    {
        markets.Release(this);
        faceExchange?.Release();
    }

    // --- Its click (§263) ------------------------------------------------------------------------

    [ThreadStatic]
    private static Action<string>? marketOpening;

    /// <summary>markets: for a test, per thread (the dock's, which the face is clicked on): where a click's search opens instead of the browser.</summary>
    internal static Action<string>? MarketOpening
    {
        get => marketOpening;
        set => marketOpening = value;
    }

    /// <summary>markets: whether the button wears the markets face and nothing else, so its whole click is the face's.</summary>
    private bool MarketAlone => FacesOnBar is [var only] && only.Info == FolderFaces.Markets;

    /// <summary>
    /// markets: what the face tracks, searched for with the app's engine in the default browser
    /// (<see cref="SearchUrls.Market"/>: "AAPL stock", "EUR CHF", "BTC EUR"). False where it asks
    /// nothing — no symbol yet —, whose click stays the folder's.
    /// </summary>
    internal bool SearchMarket()
    {
        if (faceAsk is not { } ask || SearchUrls.Market(lastConfig ?? new Config(), ask.Symbol) is not { } url)
        {
            return false;
        }
        if (MarketOpening is { } test)
        {
            test(url);
            return true;
        }
        _ = Launch.Open(url);
        return true;
    }

    /// <summary>
    /// markets: a click on a markets face among others searches what it tracks: its room along the bar
    /// and the bar's whole thickness across it — the face's box (<see cref="FaceBoxes"/>). Anywhere else
    /// on the button is the folder's click.
    /// </summary>
    private bool MarketAt(System.Numerics.Vector2 point, System.Numerics.Vector2 size, in IslandStyle style) =>
        AsksMarket && FaceAt(point, size, style) is { } at && at.Face.Info == FolderFaces.Markets && SearchMarket();
}
