// Signing in to an account in the browser the way RFC 8252 says a desktop app must — the system
// browser, a redirect to a loopback port (Loopback), PKCE (RFC 7636) — and keeping it signed in with
// its refresh token: what Microsoft's and Google's sign-ins share (MicrosoftAuth, GoogleAuth). A
// provider is its endpoints, its client, the scopes a sign-in asks for, what else its authorization
// request carries, and where it says who signed in (OAuthProvider).
//
// Not a vendor's library: MSAL's and Google's lean on reflection native AOT keeps nothing for, and all
// Karpo needs is two form posts. The proof is PKCE: the token endpoint only trades the code for tokens
// when the verifier comes back with it.
//
// A refresh writes a new refresh token back where the provider hands one out (Microsoft does with every
// refresh; Google does not, and the one it gave is kept): the old one stays valid until it expires, so
// whichever app refreshed last writes its token back and neither ever loses the account. An access
// token is held per slot — an account, or an account's part with a token of its own (Microsoft's To
// Do) — until it nearly expires, with the scopes the provider says it grants.

using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace Karpo.Accounts;

/// <summary>Who signed in, as the provider answered, and the refresh token this PC keeps.</summary>
public sealed record OAuthSignIn(string Address, string Name, string RefreshToken);

/// <summary>An access token, and the scopes the provider says it grants; null where it did not say.</summary>
public sealed record OAuthToken(string Access, IReadOnlySet<string>? Granted);

/// <summary>What sets one provider's sign-in apart (see the top of the file).</summary>
public sealed record OAuthProvider
{
    /// <summary>Its name in Karpo's words ("Microsoft refused: …"); a name, never translated.</summary>
    public required string Name { get; init; }

    public required string AuthorizeUrl { get; init; }

    public required string TokenUrl { get; init; }

    /// <summary>Karpo's client there; empty where Karpo has none yet, and no sign-in is offered.</summary>
    public required string ClientId { get; init; }

    /// <summary>
    /// The client's secret, where the provider wants one at its token endpoint even of an app installed on a PC (Google's
    /// Desktop clients); empty for none. Such a secret is no secret: it ships in the app.
    /// </summary>
    public string ClientSecret { get; init; } = "";

    /// <summary>What a sign-in asks the person to consent to.</summary>
    public required IReadOnlyList<string> Scopes { get; init; }

    /// <summary>Whatever else the authorization request carries: which account to pick, offline access.</summary>
    public IReadOnlyDictionary<string, string> Authorize { get; init; } = new Dictionary<string, string>();

    /// <summary>The code's exchange names the scopes again (Microsoft's v2 endpoint wants them; Google's takes them from the code).</summary>
    public bool ScopeOnExchange { get; init; }

    /// <summary>Where a signed-in access token says who it is.</summary>
    public required string WhoUrl { get; init; }

    /// <summary>Who that answer names: an address and a name; null where it names no address.</summary>
    public required Func<JsonObject, (string? Address, string? Name)> Who { get; init; }
}

/// <summary>A provider's sign-in and its tokens (see the top of the file); one per provider in a process.</summary>
public abstract class OAuthClient
{
    /// <summary>An access token this close to expiring is treated as expired.</summary>
    private static readonly TimeSpan Early = TimeSpan.FromMinutes(2);

    private readonly HttpClient http;
    private readonly Func<Uri, CancellationToken, Task> open;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<string, (OAuthToken Token, DateTimeOffset Until)> tokens = new(StringComparer.Ordinal);

    /// <summary>Without the provider: <paramref name="http"/> answers the endpoints and <paramref name="open"/> is the browser.</summary>
    protected OAuthClient(HttpClient http, OAuthProvider provider, Func<Uri, CancellationToken, Task> open)
    {
        this.http = http;
        Provider = provider;
        this.open = open;
    }

    public OAuthProvider Provider { get; }

    /// <summary>Whether Karpo has a client there to sign in with at all.</summary>
    public bool HasClientId => Provider.ClientId.Length > 0;

    /// <summary>
    /// Signs in through the system browser and comes back with the refresh token this PC keeps.
    /// Returns once the browser has come back; cancel to give up waiting.
    /// </summary>
    /// <param name="hint">The address of an account signed in to again (Reconnect), which the provider offers first.</param>
    /// <exception cref="AccountException">The provider refused, or the answer was not a sign-in.</exception>
    public async Task<OAuthSignIn> SignInAsync(CancellationToken cancellation, string? hint = null)
    {
        var name = Provider.Name;
        if (!HasClientId)
        {
            throw new AccountException(S.CoreAccountsOauthNoRegistration(name));
        }

        using var loopback = new Loopback();
        var verifier = Random();
        var state = Random();
        await open(Authorize(loopback.RedirectUri, verifier, state, hint), cancellation).ConfigureAwait(false);

        var answer = await loopback.WaitAsync(cancellation).ConfigureAwait(false);
        if (answer.TryGetValue("error", out var refused))
        {
            throw new AccountException(refused == "access_denied"
                ? S.CoreAccountsOauthCancelled(name)
                : S.CoreAccountsOauthRefusedSignIn(provider: name, said: answer.GetValueOrDefault("error_description") ?? refused));
        }
        if (!answer.TryGetValue("state", out var came) || came != state)
        {
            // Something other than the browser Karpo sent came back to the port.
            throw new AccountException(S.CoreAccountsOauthNotBack);
        }
        if (!answer.TryGetValue("code", out var code) || code.Length == 0)
        {
            throw new AccountException(S.CoreAccountsOauthNoCode(name));
        }

        var form = Form("authorization_code");
        form["code"] = code;
        form["redirect_uri"] = loopback.RedirectUri;
        form["code_verifier"] = verifier;
        if (Provider.ScopeOnExchange)
        {
            form["scope"] = string.Join(' ', Provider.Scopes);
        }
        var tokens = await PostAsync(form, cancellation).ConfigureAwait(false);

        var refresh = Web.Str(tokens["refresh_token"]) ?? "";
        if (refresh.Length == 0)
        {
            throw new AccountException(S.CoreAccountsOauthNoRefresh(name));
        }
        var (address, who) = await WhoAsync(Web.Str(tokens["access_token"]) ?? "", cancellation).ConfigureAwait(false);
        return new OAuthSignIn(address, who, refresh);
    }

    /// <summary>
    /// An access token for <paramref name="account"/> held in <paramref name="slot"/>: the one in hand while it has long
    /// enough to live, else a fresh one from the refresh token — asking for <paramref name="scopes"/> where given, else
    /// for what the person consented to —, whose successor is written back.
    /// </summary>
    /// <exception cref="SignInRequiredException">Only the person can settle it: no secret here, or the provider refused it.</exception>
    protected async Task<OAuthToken> TokenAsync(Account account, IReadOnlyList<string>? scopes, string slot, CancellationToken cancellation)
    {
        await gate.WaitAsync(cancellation).ConfigureAwait(false);
        try
        {
            if (tokens.TryGetValue(slot, out var held) && held.Until > DateTimeOffset.UtcNow + Early)
            {
                return held.Token;
            }

            // The file's, not the one the caller holds: the other app may have refreshed since.
            var current = AccountBook.Find(account.Id) ?? account;
            var refresh = AccountBook.Secret(current)
                ?? throw new SignInRequiredException(S.CoreAccountsSignInAgainInSettings(account.Address));

            JsonObject answer;
            try
            {
                var form = Form("refresh_token");
                form["refresh_token"] = refresh;
                if (scopes is not null)
                {
                    form["scope"] = string.Join(' ', scopes);
                }
                answer = await PostAsync(form, cancellation).ConfigureAwait(false);
            }
            catch (AccountException refused) when (refused.Status is 400 or 401)
            {
                // Revoked, expired, a changed password, or consent withdrawn (invalid_grant): nothing here mends it.
                throw new SignInRequiredException(S.CoreAccountsOauthSignInAgainTo(provider: Provider.Name, address: account.Address), refused);
            }

            var access = Web.Str(answer["access_token"]) ?? "";
            if (access.Length == 0)
            {
                throw new SignInRequiredException(S.CoreAccountsOauthSignInAgainTo(provider: Provider.Name, address: account.Address));
            }
            if (Web.Str(answer["refresh_token"]) is { Length: > 0 } next && next != refresh)
            {
                try
                {
                    AccountBook.SetSecret(account.Id, next);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    // accounts.json held by another program, or not written: it keeps every account and
                    // the token before this one, which the provider still takes. The next refresh writes one.
                    Console.Error.WriteLine($"Karpo: a new {Provider.Name} refresh token not kept ({error.GetType().Name})");
                }
            }
            var granted = Web.Str(answer["scope"]) is { Length: > 0 } said
                ? said.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal)
                : null;
            var token = new OAuthToken(access, granted);
            tokens[slot] = (token, DateTimeOffset.UtcNow + TimeSpan.FromSeconds(Web.Int(answer["expires_in"]) ?? 3600));
            return token;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Forgets every access token this process holds for the account, its own slots' too.</summary>
    public void Forget(string accountId)
    {
        gate.Wait();
        try
        {
            foreach (var slot in tokens.Keys.Where(slot => slot == accountId || slot.StartsWith(accountId + "|", StringComparison.Ordinal)).ToList())
            {
                tokens.Remove(slot);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>The authorization request, PKCE and all.</summary>
    internal Uri Authorize(string redirect, string verifier, string state, string? hint = null)
    {
        var query = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["client_id"] = Provider.ClientId,
            ["response_type"] = "code",
            ["redirect_uri"] = redirect,
            ["scope"] = string.Join(' ', Provider.Scopes),
            ["code_challenge"] = Challenge(verifier),
            ["code_challenge_method"] = "S256",
            ["state"] = state,
        };
        foreach (var (name, value) in Provider.Authorize)
        {
            query[name] = value;
        }
        if (hint is { Length: > 0 })
        {
            query["login_hint"] = hint;
        }
        return new Uri($"{Provider.AuthorizeUrl}?{string.Join('&', query.Select(Encoded))}");
    }

    /// <summary>The SHA-256 of the verifier, base64url without padding (PKCE S256).</summary>
    internal static string Challenge(string verifier) => Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    /// <summary>A token request's form: the grant, the client, and its secret where it has one.</summary>
    private Dictionary<string, string> Form(string grant)
    {
        var form = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["client_id"] = Provider.ClientId,
            ["grant_type"] = grant,
        };
        if (Provider.ClientSecret.Length > 0)
        {
            form["client_secret"] = Provider.ClientSecret;
        }
        return form;
    }

    private async Task<(string Address, string Name)> WhoAsync(string access, CancellationToken cancellation)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Provider.WhoUrl);
        request.Headers.Authorization = new("Bearer", access);
        using var response = await http.SendAsync(request, cancellation).ConfigureAwait(false);
        var who = Web.Parse(await response.Content.ReadAsStringAsync(cancellation).ConfigureAwait(false));
        var (address, name) = who is null ? (null, null) : Provider.Who(who);
        return response.IsSuccessStatusCode && address is { Length: > 0 }
            ? (address, name ?? "")
            : throw new AccountException(S.CoreAccountsOauthWhichAccount(Provider.Name), (int)response.StatusCode);
    }

    /// <summary>A form post to the token endpoint; the provider's refusal in its words, with its status.</summary>
    private async Task<JsonObject> PostAsync(IReadOnlyDictionary<string, string> form, CancellationToken cancellation)
    {
        using var content = new FormUrlEncodedContent(form);
        using var response = await http.PostAsync(new Uri(Provider.TokenUrl), content, cancellation).ConfigureAwait(false);
        var answer = Web.Parse(await response.Content.ReadAsStringAsync(cancellation).ConfigureAwait(false));
        if (response.IsSuccessStatusCode && answer is not null)
        {
            return answer;
        }
        var code = Web.Str(answer?["error"]) ?? "invalid_response";
        var said = Web.Str(answer?["error_description"])?.Split('\n')[0].Trim();
        throw new AccountException(S.CoreAccountsOauthRefusedSaid(provider: Provider.Name, said: said is { Length: > 0 } ? said : code), (int)response.StatusCode);
    }

    private static string Encoded(KeyValuePair<string, string> pair) =>
        $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}";

    private static string Random() => Base64Url(RandomNumberGenerator.GetBytes(32));

    private static string Base64Url(ReadOnlySpan<byte> bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>
    /// §491: how the system browser is opened — the dock's one launcher (Launch.Open), which closes what of
    /// Karpo's is open first. Nothing opens before the dock has said so.
    /// </summary>
    public static Action<string>? Browser { get; set; }

    /// <summary>The system browser, never a window Karpo draws: a password belongs in the person's own browser.</summary>
    protected static Task Browse(Uri url, CancellationToken cancellation)
    {
        Browser?.Invoke(url.ToString());
        return Task.CompletedTask;
    }
}
