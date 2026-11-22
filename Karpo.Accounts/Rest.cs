// A provider's JSON API over plain HTTP, as Microsoft Graph and Google's APIs are both read: a bearer
// token per request, throttling waited out instead of failed on (after hearth's GraphClient), and a
// refusal in Karpo's words naming the provider. Each API pages its own way (Graph's @odata.nextLink,
// Google's nextPageToken) and says some things its own way (Google's 403 for a rate or a scope not
// granted): the subclasses, Microsoft\Graph.cs and Google\GoogleApi.cs.

using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;

namespace Karpo.Accounts;

internal abstract class Rest(HttpClient http, Func<CancellationToken, Task<string>> token, string provider)
{
    /// <summary>Tries of one request that was throttled (429) or could not be answered (503, 504).</summary>
    private const int Attempts = 4;

    /// <summary>The longest Retry-After Karpo sits out; longer, and the read fails and is tried again later.</summary>
    private static readonly TimeSpan LongestWait = TimeSpan.FromSeconds(30);

    /// <summary>The provider's name in Karpo's words ("Google refused: …").</summary>
    protected string Provider { get; } = provider;

    /// <summary>One GET, as JSON.</summary>
    /// <param name="prefer">A <c>Prefer</c> header, such as the time zone events come back in.</param>
    public async Task<JsonObject> GetAsync(string url, CancellationToken cancellation, string? prefer = null) =>
        await SendAsync(HttpMethod.Get, url, null, null, cancellation, prefer).ConfigureAwait(false)
            ?? throw new AccountException(S.CoreAccountsOauthNotJson(Provider), 204);

    /// <summary>
    /// One request — a GET, or a task's POST, PATCH or DELETE — as JSON; null where the answer has no body (204).
    /// </summary>
    /// <param name="ifMatch">The version the change is made against; a newer one there is a <see cref="TaskConflictException"/>.</param>
    public async Task<JsonObject?> SendAsync(
        HttpMethod method, string url, JsonObject? content, string? ifMatch, CancellationToken cancellation, string? prefer = null)
    {
        var (status, bytes) = await RawAsync(method, url, content, ifMatch, cancellation, prefer).ConfigureAwait(false);
        return status == 204 || bytes.Length == 0
            ? null
            : Web.Parse(System.Text.Encoding.UTF8.GetString(bytes).TrimStart('﻿')) ?? throw new AccountException(S.CoreAccountsOauthNotJson(Provider), status);
    }

    /// <summary>One GET of something that is not JSON — a contact's picture — as its bytes; null where there is none (404).</summary>
    public async Task<byte[]?> BytesAsync(string url, CancellationToken cancellation)
    {
        try
        {
            return (await RawAsync(HttpMethod.Get, url, null, null, cancellation, null).ConfigureAwait(false)).Body;
        }
        catch (AccountException missing) when (missing.Status == 404)
        {
            return null;
        }
    }

    /// <summary>Whether a refusal is a throttle, waited out and asked again: a 429, a 503 or 504, and whatever else the API says one with.</summary>
    protected virtual bool Throttled(int status, JsonNode? error) => status is 429 or 503 or 504;

    /// <summary>The API's own reading of a refusal (its <c>error</c> object), thrown in place of Karpo's; null for none.</summary>
    protected virtual Exception? Refusal(int status, JsonNode? error) => null;

    /// <summary>One request's status and body where it succeeded, throttling waited out; a refusal where it did not.</summary>
    private async Task<(int Status, byte[] Body)> RawAsync(
        HttpMethod method, string url, JsonObject? content, string? ifMatch, CancellationToken cancellation, string? prefer)
    {
        for (var attempt = 1; ; attempt++)
        {
            using var request = new HttpRequestMessage(method, url);
            request.Headers.Authorization = new("Bearer", await token(cancellation).ConfigureAwait(false));
            if (prefer is not null)
            {
                request.Headers.TryAddWithoutValidation("Prefer", prefer);
            }
            if (ifMatch is not null)
            {
                request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
            }
            if (content is not null)
            {
                request.Content = new StringContent(content.ToJsonString(), System.Text.Encoding.UTF8, "application/json");
            }
            using var response = await http.SendAsync(request, cancellation).ConfigureAwait(false);
            var bytes = await response.Content.ReadAsByteArrayAsync(cancellation).ConfigureAwait(false);
            var status = (int)response.StatusCode;

            if (response.IsSuccessStatusCode)
            {
                return (status, bytes);
            }
            var error = Web.Parse(System.Text.Encoding.UTF8.GetString(bytes))?["error"];
            if (response.StatusCode == HttpStatusCode.PreconditionFailed)
            {
                throw new TaskConflictException(S.CoreAccountsOauthAnswered(provider: Provider, answer: "412"));
            }
            if (Throttled(status, error) && attempt < Attempts)
            {
                var wait = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(1 << attempt);
                if (wait <= LongestWait)
                {
                    await Task.Delay(wait, cancellation).ConfigureAwait(false);
                    continue;
                }
            }
            if (Refusal(status, error) is { } own)
            {
                throw own;
            }

            // Graph's code is a word ("ErrorItemNotFound"), Google's a number beside a word (its status).
            var code = Web.Str(error?["code"]) ?? Web.Str(error?["status"]) ?? "";
            var said = Web.Str(error?["message"]);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new SignInRequiredException(S.CoreAccountsOauthSignInAgain(Provider));
            }
            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                // A work account whose organization does not allow the permission, or a mailbox
                // that is not there (MailboxNotEnabledForRESTAPI): the person or an admin decides.
                throw new AccountException(
                    said is { Length: > 0 } ? S.CoreAccountsOauthRefusedSaid(provider: Provider, said: said) : S.CoreAccountsOauthRefused403(Provider), status);
            }
            throw new AccountException(
                said is { Length: > 0 }
                    ? S.CoreAccountsOauthAnsweredSaid(provider: Provider, status: status.ToString(CultureInfo.InvariantCulture), said: said)
                    : S.CoreAccountsOauthAnswered(provider: Provider, answer: $"{status.ToString(CultureInfo.InvariantCulture)} {code}".TrimEnd()),
                status);
        }
    }

    /// <summary>
    /// Every item of a list, page by page as <paramref name="next"/> finds the next page's address in a page, at most
    /// <paramref name="most"/> — and where the list goes on past that, <see cref="Pages.More"/> says so, so a reader can tell
    /// the person that not all of it is shown instead of cutting it off without a word.
    /// </summary>
    protected async Task<Pages> AllAsync(
        string url, string items, Func<JsonObject, string?> next, CancellationToken cancellation, string? prefer, int most)
    {
        var found = new Pages();
        string? page = url;
        while (page is not null && found.Count < most)
        {
            var answer = await GetAsync(page, cancellation, prefer).ConfigureAwait(false);
            found.AddRange(Web.Items(answer, items));
            page = next(answer);
        }
        found.More = page is not null;
        return found;
    }
}

/// <summary>The items of a list an API gave page by page (<see cref="Rest.AllAsync"/>).</summary>
internal sealed class Pages : List<JsonObject>
{
    /// <summary>The list has more than was read: the read stopped at its most.</summary>
    public bool More { get; set; }
}
