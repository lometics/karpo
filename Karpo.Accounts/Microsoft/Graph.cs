// Microsoft Graph over plain HTTP (Rest: a bearer token per request, throttling waited out), paging
// through @odata.nextLink. Reads, and To Do's tasks written (MicrosoftTodo).

namespace Karpo.Accounts.Microsoft;

internal sealed class Graph(HttpClient http, Func<CancellationToken, Task<string>> token) : Rest(http, token, "Microsoft")
{
    public const string V1 = "https://graph.microsoft.com/v1.0";

    /// <summary>
    /// Every item of a list, following <c>@odata.nextLink</c> to the end, at most <paramref name="most"/>
    /// — and where the list goes on past that, <see cref="Pages.More"/> says so.
    /// </summary>
    public Task<Pages> AllAsync(string url, CancellationToken cancellation, string? prefer = null, int most = 20_000) =>
        AllAsync(url, "value", page => Web.Str(page["@odata.nextLink"]), cancellation, prefer, most);
}
