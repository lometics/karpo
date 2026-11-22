// One of Google's JSON APIs — Calendar, People, Gmail, Tasks — over plain HTTP (Rest: a bearer token per
// request, throttling waited out), for one kind of an account: its token is that kind's
// (GoogleAuth.TokenAsync), so a kind the person did not grant fails here alone. Paging is Google's own,
// a nextPageToken given back as pageToken. Google says two things with a 403 that are not a refusal of
// the request: a rate limit (rateLimitExceeded, userRateLimitExceeded), waited out as a 429 is, and a
// token without the kind's scope (insufficientPermissions, ACCESS_TOKEN_SCOPE_INSUFFICIENT) — the
// person's consent missing, a ConsentException for that kind.

using System.Text.Json.Nodes;

namespace Karpo.Accounts.Google;

internal sealed class GoogleApi(HttpClient http, Func<CancellationToken, Task<string>> token, AccountData kind) : Rest(http, token, "Google")
{
    public const string Calendar = "https://www.googleapis.com/calendar/v3";

    public const string People = "https://people.googleapis.com/v1";

    public const string Gmail = "https://gmail.googleapis.com/gmail/v1/users/me";

    public const string Tasks = "https://tasks.googleapis.com/tasks/v1";

    /// <summary>
    /// Every item of a list (its array <paramref name="items"/>), page by page through <c>nextPageToken</c>, at most
    /// <paramref name="most"/> — and where the list goes on past that, <see cref="Pages.More"/> says so.
    /// </summary>
    public Task<Pages> AllAsync(string url, string items, CancellationToken cancellation, int most = 20_000) =>
        AllAsync(url, items, page => Web.Str(page["nextPageToken"]) is { Length: > 0 } next
            ? $"{url}{(url.Contains('?', StringComparison.Ordinal) ? '&' : '?')}pageToken={Uri.EscapeDataString(next)}"
            : null, cancellation, null, most);

    protected override bool Throttled(int status, JsonNode? error) =>
        base.Throttled(status, error) || status == 403 && Reasons(error).Any(reason => reason is "rateLimitExceeded" or "userRateLimitExceeded");

    protected override Exception? Refusal(int status, JsonNode? error) =>
        status == 403 && Reasons(error).Any(reason => reason is "insufficientPermissions" or "ACCESS_TOKEN_SCOPE_INSUFFICIENT")
            ? new ConsentException(AccountKind.Google, kind)
            : null;

    /// <summary>What a Google error says it is: its <c>errors[].reason</c> (the APIs of before) and <c>details[].reason</c> (google.rpc.ErrorInfo).</summary>
    internal static IEnumerable<string> Reasons(JsonNode? error) =>
        Web.Items(error, "errors").Concat(Web.Items(error, "details")).Select(item => Web.Str(item["reason"])).OfType<string>();
}
