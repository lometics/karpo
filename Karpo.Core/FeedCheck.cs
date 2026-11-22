// rss: proving that an address is a feed before the RSS widget is given it, as the stock's symbol
// is proved against the price service (Symbols.cs) and the weather's place is found (Places.cs).
//
// The address is read once, through the reader the bar uses (Feeds.cs) — by the settings app's
// address box and by the address field of the widget's menu on the bar (docs\widgets.md §219) — so
// an address the card accepts is one the widget can read, and one it refuses is refused with words
// now rather than by a widget that says "can't be read" for ever.
// The request is the bar's: a plain GET, Karpo's user agent, no cookies, a timeout, a cap on the
// size; redirects are followed as an ordinary reader follows them, since feeds move. It is awaited
// off the UI thread and can be cancelled, so a slow site never freezes the window.

using System.Net.Http;

namespace Karpo.Core;

public static class FeedCheck
{
    /// <summary>Longer than a person will wait for a card to answer, and shorter than a hung socket.</summary>
    static readonly TimeSpan Patience = TimeSpan.FromSeconds(15);

    static readonly HttpClient Http = CreateClient();

    static HttpClient CreateClient()
    {
        // No cookie container: nothing is kept between requests, and nothing is replayed.
        var handler = new HttpClientHandler { UseCookies = false, AllowAutoRedirect = true, MaxAutomaticRedirections = 5 };
        var client = new HttpClient(handler) { Timeout = Patience };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("karpo");
        return client;
    }

    /// <summary>
    /// The feed at <paramref name="address"/>, read as the bar reads it. Throws
    /// <see cref="FeedException"/> where the answer is not a feed — "that address isn't a feed" —
    /// and the network's own exceptions where it could not be asked — "couldn't check it now",
    /// which is a different thing to be told.
    /// </summary>
    public static async Task<Feed> ReadAsync(string address, CancellationToken cancel)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, address);
        request.Headers.TryAddWithoutValidation("Accept", "application/rss+xml, application/atom+xml, application/xml;q=0.9, text/xml;q=0.8, */*;q=0.1");
        using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancel).ConfigureAwait(false);
        // A page that is not there is no feed; a site that cannot answer now is a failure to check.
        if (response.StatusCode is System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.Gone)
        {
            throw new FeedException(FeedProblem.NotAFeed);
        }
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is > Feeds.MostBytes)
        {
            throw new FeedException(FeedProblem.TooLong);
        }
        await using var stream = await response.Content.ReadAsStreamAsync(cancel).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancel).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > Feeds.MostBytes)
            {
                throw new FeedException(FeedProblem.TooLong);
            }
            buffer.Write(chunk, 0, read);
        }
        return Feeds.Parse(buffer.ToArray(), response.RequestMessage?.RequestUri ?? new Uri(address));
    }

    /// <summary>The exceptions a check is allowed to fail with because it could not ask; anything else is a bug worth seeing.</summary>
    public static bool Unreachable(Exception error) =>
        error is HttpRequestException or TaskCanceledException or OperationCanceledException;
}
