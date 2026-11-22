// Signing in to a Microsoft account against the Microsoft identity platform's v2 endpoints, over the
// sign-in every provider shares (OAuthClient: the system browser, a loopback redirect, PKCE, refresh).
// There is no client secret — a desktop app cannot keep one — and the proof is PKCE.
//
// Karpo's app registration is its own, "Karpo" in Entra, a public client
// for personal and work accounts with http://localhost as its redirect (the
// port is Windows' pick each time, which Entra allows for localhost). It asks
// for read scopes for calendars, contacts and mail, which Karpo shows and never
// writes, and since 2026-10-16 for To Do's tasks, which the to-do line ticks,
// adds, renames and deletes (Tasks.ReadWrite).
//
// An account added before that consented to the reads alone. Its refresh still
// asks for exactly those (ReadScopes), so its calendars and mail never notice;
// the tasks have a token of their own (TaskScopes), and where Microsoft refuses
// that one while the reads' still comes, the account needs signing in again for
// its tasks only — ConsentException, "Reconnect to sync tasks".
//
// Microsoft hands out a new refresh token with every refresh and the old one
// keeps working until it expires, so whichever app refreshed last writes its
// token back and neither ever loses the account (OAuthClient).

namespace Karpo.Accounts.Microsoft;

public sealed class MicrosoftAuth : OAuthClient
{
    /// <summary>Karpo's app registration in Entra (public client, personal and work accounts).</summary>
    public const string ClientId = "09fc8db5-849d-4059-8d98-b71075df8cec";

    public const string Authority = "https://login.microsoftonline.com/common/oauth2/v2.0";

    /// <summary>
    /// What a refresh asks for, and every account consented to: reading calendars, contacts and mail.
    /// Karpo writes none of them.
    /// </summary>
    public static readonly string[] ReadScopes =
    [
        "offline_access",
        "User.Read",
        "Calendars.Read",
        "Contacts.Read",
        "Mail.Read",
    ];

    /// <summary>What the tasks' token asks for: To Do's lists and tasks, read and written.</summary>
    public static readonly string[] TaskScopes = ["offline_access", "Tasks.ReadWrite"];

    /// <summary>What a sign-in asks the person to consent to: the reads, and To Do.</summary>
    public static readonly string[] Scopes = [.. ReadScopes, "Tasks.ReadWrite"];

    public MicrosoftAuth()
        : this(Web.Http, ClientId, Browse)
    {
    }

    /// <summary>Without Microsoft: <paramref name="http"/> answers the endpoints and <paramref name="open"/> is the browser.</summary>
    internal MicrosoftAuth(HttpClient http, string clientId, Func<Uri, CancellationToken, Task> open)
        : base(http, Registration(clientId), open)
    {
    }

    /// <summary>The one every account shares in a process, so a token is refreshed once and not per store.</summary>
    public static MicrosoftAuth Shared { get; } = new();

    private static OAuthProvider Registration(string clientId) => new()
    {
        Name = "Microsoft",
        AuthorizeUrl = $"{Authority}/authorize",
        TokenUrl = $"{Authority}/token",
        ClientId = clientId,
        Scopes = Scopes,
        Authorize = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["response_mode"] = "query",
            // Which account, every time: adding an account is a deliberate act, never whatever the
            // browser happens to be signed in to.
            ["prompt"] = "select_account",
        },
        ScopeOnExchange = true,
        WhoUrl = $"{Graph.V1}/me?$select=displayName,mail,userPrincipalName",
        Who = who => (Web.Str(who["mail"]) is { Length: > 0 } mail ? mail : Web.Str(who["userPrincipalName"]), Web.Str(who["displayName"])),
    };

    /// <summary>
    /// An access token for <paramref name="account"/>'s reads: the one in hand while it has long enough to
    /// live, else a fresh one from the refresh token, whose successor is written back.
    /// </summary>
    /// <exception cref="SignInRequiredException">Only the person can settle it.</exception>
    public async Task<string> TokenAsync(Account account, CancellationToken cancellation) =>
        (await TokenAsync(account, ReadScopes, account.Id, cancellation).ConfigureAwait(false)).Access;

    /// <summary>
    /// An access token for <paramref name="account"/>'s To Do (<see cref="TaskScopes"/>), held apart from
    /// the reads' so an account that never consented to it loses nothing else.
    /// </summary>
    /// <exception cref="ConsentException">The account signs in, but not for its tasks: it needs signing in to again.</exception>
    /// <exception cref="SignInRequiredException">The account does not sign in at all.</exception>
    public async Task<string> TasksTokenAsync(Account account, CancellationToken cancellation)
    {
        try
        {
            return (await TokenAsync(account, TaskScopes, account.Id + "|tasks", cancellation).ConfigureAwait(false)).Access;
        }
        catch (SignInRequiredException refused) when (refused.InnerException is AccountException)
        {
            // Refused for the tasks: where the reads' token still comes, the sign-in is good and it is
            // the consent to To Do that is missing (Microsoft words it AADSTS65001, or as an invalid
            // grant for a scope never granted); where it does not, the sign-in itself is gone.
            await TokenAsync(account, cancellation).ConfigureAwait(false);
            throw new ConsentException(AccountKind.Microsoft, AccountData.Tasks, refused);
        }
    }
}
