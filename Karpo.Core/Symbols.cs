// Finding an instrument for the stock widget, and proving the one that was
// chosen is real: Yahoo's public search and chart endpoints, the same two the
// bar uses, which need no account and no key.
//
// **There is no key here and there must never be one.** If a keyed provider is
// ever added, its key belongs in the user's own config and never in a URL that
// reaches a log line.
//
// Like the weather's place search (Places.cs), in the core: the settings app's symbol box and the
// symbol field of a stock's menu on the bar (docs\widgets.md §219) ask the same two calls. Both are
// awaited off the UI thread and can be cancelled, so a slow service never freezes a window.
//
// The client keeps no cookies. Both endpoints answer a plain request — checked,
// and the check is written down in the notes — and a client that stores cookies
// is a client that can be walked through a consent gate. If either endpoint ever
// starts demanding a crumb, a cookie or a browser check, the request fails like
// any other failure and the card says it could not look the symbol up. Going
// around a gate is a different thing from calling an endpoint that answers.

using System.Net.Http;
using System.Text.Json;

namespace Karpo.Core;

public static class Symbols
{
    /// <summary>Longer than a person will wait for a box to fill, and shorter than a hung socket.</summary>
    static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    static readonly HttpClient Http = CreateClient();

    static HttpClient CreateClient()
    {
        // No cookie container: nothing is kept between requests, and nothing is replayed.
        var handler = new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false };
        var client = new HttpClient(handler) { Timeout = Patience };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("karpo");
        return client;
    }

    /// <summary>
    /// Listings matching <paramref name="text"/>, best first. Throws on network and service errors,
    /// which the card turns into "couldn't search right now" rather than into silence.
    /// </summary>
    public static async Task<List<Listing>> SearchAsync(string text, CancellationToken cancel)
    {
        using var response = await Http.GetAsync(SymbolQuery.SearchUrl(text), cancel).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancel).ConfigureAwait(false);
        return SymbolQuery.Listings(json);
    }

    /// <summary>
    /// What <paramref name="symbol"/> turns out to be, or null when the service has nothing for it.
    /// Null is an answer, not a failure: it is how a typo is caught at the moment it is set instead
    /// of by a widget that shows nothing for ever. A service that cannot be reached throws instead,
    /// because "we could not check" and "there is no such symbol" are different things to be told.
    /// </summary>
    public static async Task<Instrument?> LookUpAsync(string symbol, CancellationToken cancel)
    {
        using var response = await Http.GetAsync(SymbolQuery.LookUpUrl(symbol), cancel).ConfigureAwait(false);
        // The endpoint says "no such symbol" with a 404 and a body explaining itself.
        if (response.StatusCode is System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.BadRequest)
        {
            return null;
        }
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancel).ConfigureAwait(false);
        return SymbolQuery.Read(json);
    }

    /// <summary>The exceptions a lookup is allowed to fail with; anything else is a bug worth seeing.</summary>
    public static bool Expected(Exception error) =>
        error is HttpRequestException or TaskCanceledException or OperationCanceledException or JsonException;
}
