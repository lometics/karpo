// Signing in to a Google account, over the sign-in every provider shares (OAuthClient: the system
// browser, a loopback redirect, PKCE, refresh) — docs\accounts.md, "Google".
//
// "i want to integrate google oauth api into karpo as well. … implement the sync on our side so that
// users can login with google directly and give the application consent to access their data."
// (the person, 2026-11-12)
//
// Karpo's OAuth client in Google Cloud is of the Desktop app type. Google takes any loopback port for
// one, so the redirect is the one Loopback listens on, as Microsoft's is. Unlike Entra, Google's token
// endpoint wants the Desktop client's secret beside its id; Google says itself that an installed app
// cannot keep a secret, so every copy of Karpo carries it, and the proof is still PKCE. The source does
// not carry it: it comes in at build time, and a build without it offers no Google account (§713).
//
// The sign-in asks for offline access and shows the consent screen every time (prompt=consent), so a
// refresh token comes back every time, the account picker too (select_account). Google's consent
// screen lets the person untick any of the data scopes: the token says which were granted (its scope),
// and a kind whose scope was not is that kind's ConsentException alone — its calendars, contacts, mail
// or tasks say "Reconnect", the rest is read as ever. Google keeps the refresh token it gave (it hands
// out no new one on a refresh) until the person takes the access back; a refused refresh (invalid_grant)
// is "sign in again".

using System.Reflection;

namespace Karpo.Accounts.Google;

public sealed class GoogleAuth : OAuthClient
{
    /// <summary>
    /// Karpo's OAuth client in Google Cloud: "Karpo for Windows", a Desktop app client in project lometics-karpo
    /// (organization lometics.com), made 2026-11-12. Empty would mean no Google account is offered
    /// (<see cref="OAuthClient.HasClientId"/>).
    /// </summary>
    public const string ClientId = "599122296092-0hlu35sg937avm32uqk2loavc3s8ak65.apps.googleusercontent.com";

    /// <summary>The name of the assembly's mark that carries <see cref="ClientSecret"/> (AssemblyMetadata, Karpo.Accounts.csproj).</summary>
    public const string SecretMetadata = "KarpoGoogleClientSecret";

    /// <summary>
    /// The Desktop client's secret, which Google's token endpoint wants beside its id. Google documents a Desktop app as
    /// a public client that cannot keep one, and every copy of Karpo that has it carries it; all the same it is not
    /// written in the source, which is public: it comes in when Karpo is built (the MSBuild property
    /// KarpoGoogleClientSecret, from Local.Build.props or the environment's KARPO_GOOGLE_CLIENT_SECRET; §713). A build
    /// without it has none, and offers no Google account (<see cref="Offered"/>).
    /// </summary>
    public static string ClientSecret { get; } =
        typeof(GoogleAuth).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(mark => mark.Key == SecretMetadata)?.Value?.Trim() ?? "";

    /// <summary>
    /// The id a sign-in is made with: <see cref="ClientId"/> where this build has the secret Google wants beside it,
    /// else none, which is what offers no Google account (<see cref="OAuthClient.HasClientId"/>).
    /// </summary>
    public static string Offered(string clientId, string clientSecret) => clientSecret.Length > 0 ? clientId : "";

    public const string AuthorizeUrl = "https://accounts.google.com/o/oauth2/v2/auth";

    public const string TokenUrl = "https://oauth2.googleapis.com/token";

    /// <summary>Who signed in (OpenID Connect's userinfo: email, name), under the openid, email and profile scopes.</summary>
    public const string WhoUrl = "https://openidconnect.googleapis.com/v1/userinfo";

    public const string CalendarScope = "https://www.googleapis.com/auth/calendar.readonly";

    public const string ContactsScope = "https://www.googleapis.com/auth/contacts.readonly";

    public const string MailScope = "https://www.googleapis.com/auth/gmail.readonly";

    /// <summary>Google Tasks read and written, as Microsoft To Do is: the one scope that writes.</summary>
    public const string TasksScope = "https://www.googleapis.com/auth/tasks";

    /// <summary>What a sign-in asks the person to consent to: who they are, and each kind of what the account holds.</summary>
    public static readonly string[] Scopes = ["openid", "email", "profile", CalendarScope, ContactsScope, MailScope, TasksScope];

    public GoogleAuth()
        : this(Web.Http, Offered(ClientId, ClientSecret), ClientSecret, Browse)
    {
    }

    /// <summary>Without Google: <paramref name="http"/> answers the endpoints and <paramref name="open"/> is the browser.</summary>
    internal GoogleAuth(HttpClient http, string clientId, string clientSecret, Func<Uri, CancellationToken, Task> open)
        : base(http, Registration(clientId, clientSecret), open)
    {
    }

    /// <summary>The one every account shares in a process, so a token is refreshed once and not per store.</summary>
    public static GoogleAuth Shared { get; } = new();

    private static OAuthProvider Registration(string clientId, string clientSecret) => new()
    {
        Name = "Google",
        AuthorizeUrl = AuthorizeUrl,
        TokenUrl = TokenUrl,
        ClientId = clientId,
        ClientSecret = clientSecret,
        Scopes = Scopes,
        Authorize = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["access_type"] = "offline",
            // The consent screen every time, so a refresh token comes every time; the account picker, so adding an
            // account is a deliberate act and never whatever the browser happens to be signed in to.
            ["prompt"] = "consent select_account",
        },
        WhoUrl = WhoUrl,
        Who = who => (Web.Str(who["email"]), Web.Str(who["name"])),
    };

    /// <summary>The scope <paramref name="kind"/> is read (or, for the tasks, written) under.</summary>
    public static string Scope(AccountData kind) => kind switch
    {
        AccountData.Calendars => CalendarScope,
        AccountData.Contacts => ContactsScope,
        AccountData.Mail => MailScope,
        _ => TasksScope,
    };

    /// <summary>
    /// An access token for <paramref name="account"/>'s <paramref name="kind"/>: one token for every kind, held until it
    /// nearly expires; refused for a kind whose scope the person did not grant.
    /// </summary>
    /// <exception cref="ConsentException">The account signs in, but the person did not let Karpo at <paramref name="kind"/>.</exception>
    /// <exception cref="SignInRequiredException">The account does not sign in at all.</exception>
    public async Task<string> TokenAsync(Account account, AccountData kind, CancellationToken cancellation)
    {
        var token = await TokenAsync(account, null, account.Id, cancellation).ConfigureAwait(false);
        return token.Granted is { } granted && !granted.Contains(Scope(kind))
            ? throw new ConsentException(AccountKind.Google, kind)
            : token.Access;
    }
}
