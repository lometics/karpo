// Signing in to Google over the sign-in every provider shares (Karpo.Accounts\OAuthClient.cs,
// Google\GoogleAuth.cs): the authorization request — PKCE, offline access, the consent screen and the
// account picker every time, the scopes, the hint on a reconnect —, a whole sign-in through the loopback
// port with the Desktop client's secret sent beside its id, a refresh that asks for no scope and is
// refused as "sign in again", and the scopes the person granted deciding which kinds get a token.

using System.Net;
using System.Text;
using Karpo.Accounts;
using Karpo.Accounts.Google;

namespace Karpo.Tests.Accounts;

[Collection(MicrosoftAuthTests.Collection)]
public sealed class GoogleAuthTests : IDisposable
{
    private readonly List<Account> added = [];

    public void Dispose()
    {
        foreach (var account in added)
        {
            AccountBook.Remove(account.Id);
        }
    }

    /// <summary>Google's endpoints, answering from a list and keeping what each request was sent.</summary>
    private sealed class Endpoint(params (HttpStatusCode Status, string Body)[] answers) : HttpMessageHandler
    {
        private int next;

        public List<(string Url, string? Bearer, string Body)> Sent { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Sent.Add((request.RequestUri!.ToString(), request.Headers.Authorization?.Parameter, request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken)));
            var (status, body) = answers[Math.Min(next++, answers.Length - 1)];
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private Account Kept(string refresh)
    {
        var account = AccountBook.Add(new Account { Id = Account.NewId(), Kind = AccountKind.Google, Address = "ada@gmail.com" }, refresh);
        added.Add(account);
        return account;
    }

    private static GoogleAuth Auth(Endpoint endpoint, Func<Uri, CancellationToken, Task>? open = null) =>
        new(new HttpClient(endpoint), "client.apps.googleusercontent.com", "GOCSPX-secret", open ?? ((_, _) => Task.CompletedTask));

    private static string Granted(params string[] scopes) => string.Join(' ', ["openid", "https://www.googleapis.com/auth/userinfo.email", .. scopes]);

    [Fact]
    public void TheRequestAsksForOfflineAccessTheConsentScreenAndEveryKind()
    {
        var auth = Auth(new Endpoint());
        var url = auth.Authorize("http://localhost:5000", "verifier", "state");
        var query = Uri.UnescapeDataString(url.Query);

        Assert.StartsWith("https://accounts.google.com/o/oauth2/v2/auth?", url.ToString());
        Assert.Contains("client_id=client.apps.googleusercontent.com", query);
        Assert.Contains("redirect_uri=http://localhost:5000", query);
        Assert.Contains("response_type=code", query);
        Assert.Contains($"code_challenge={OAuthClient.Challenge("verifier")}", query);
        Assert.Contains("code_challenge_method=S256", query);
        Assert.Contains("state=state", query);
        // A refresh token every time, and the account picked every time.
        Assert.Contains("access_type=offline", query);
        Assert.Contains("prompt=consent select_account", query);
        // Who, and each kind: calendars, contacts and mail read, the tasks read and written; nothing sent or changed.
        Assert.Contains("scope=openid email profile https://www.googleapis.com/auth/calendar.readonly https://www.googleapis.com/auth/contacts.readonly https://www.googleapis.com/auth/gmail.readonly https://www.googleapis.com/auth/tasks&", query + "&");
        Assert.DoesNotContain("gmail.modify", query);
        Assert.DoesNotContain("gmail.send", query);
        Assert.DoesNotContain("login_hint", query);
        // The secret is the token endpoint's, never the browser's.
        Assert.DoesNotContain("secret", query);

        // Reconnect: Google offers that account first.
        Assert.Contains("login_hint=ada@gmail.com", Uri.UnescapeDataString(auth.Authorize("http://localhost:5000", "v", "s", "ada@gmail.com").Query));
    }

    [Fact]
    public void WithoutAClientIdNoGoogleAccountIsOffered()
    {
        Assert.False(new GoogleAuth(new HttpClient(new Endpoint()), "", "", (_, _) => Task.CompletedTask).HasClientId);
        Assert.True(Auth(new Endpoint()).HasClientId);
        // Karpo's own: whatever the constants hold, the add follows them.
        // §713: offered where this build was given Google's client secret, and not in one that was not (CI, a fork's).
        Assert.Equal(GoogleAuth.ClientId.Length > 0 && GoogleAuth.ClientSecret.Length > 0, GoogleAuth.Shared.HasClientId);
        Assert.Equal(GoogleAuth.ClientId, GoogleAuth.Offered(GoogleAuth.ClientId, "GOCSPX-secret"));
        Assert.Equal("", GoogleAuth.Offered(GoogleAuth.ClientId, ""));
        Assert.Equal(GoogleAuth.Shared.HasClientId, AccountSignIn.CanSignIn(AccountKind.Google));
        Assert.False(AccountSignIn.CanSignIn(AccountKind.Imap));
    }

    [Fact]
    public async Task ASignInWithoutAClientIdIsRefusedBeforeTheBrowserOpens()
    {
        var opened = false;
        var auth = new GoogleAuth(new HttpClient(new Endpoint()), "", "", (_, _) => { opened = true; return Task.CompletedTask; });

        var refused = await Assert.ThrowsAsync<AccountException>(() => auth.SignInAsync(CancellationToken.None));

        Assert.Contains("Google", refused.Message);
        Assert.False(opened);
    }

    /// <summary>
    /// A whole sign-in: the browser comes back to the loopback port with the code and the state; the code is traded with the
    /// verifier and the Desktop client's secret (Google wants it), naming no scope; and who signed in is OpenID's userinfo.
    /// </summary>
    [Fact]
    public async Task ASignInTradesTheCodeWithTheSecretAndAsksWhoSignedIn()
    {
        var endpoint = new Endpoint(
            (HttpStatusCode.OK, $$"""{ "access_token": "A", "refresh_token": "R", "expires_in": 3599, "scope": "{{Granted(GoogleAuth.CalendarScope)}}" }"""),
            (HttpStatusCode.OK, """{ "sub": "1", "email": "ada@gmail.com", "name": "Ada Lovelace" }"""));
        using var browser = new HttpClient();
        string? verifierChallenge = null;
        var auth = Auth(endpoint, (url, cancellation) =>
        {
            // The browser: Google sends it back to the redirect with the code and the state it was given.
            var query = System.Web.HttpUtility.ParseQueryString(url.Query);
            verifierChallenge = query["code_challenge"];
            _ = browser.GetAsync($"{query["redirect_uri"]}/?code=THE-CODE&state={Uri.EscapeDataString(query["state"]!)}&scope=openid", cancellation);
            return Task.CompletedTask;
        });

        using var patience = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var found = await auth.SignInAsync(patience.Token);

        Assert.Equal(new OAuthSignIn("ada@gmail.com", "Ada Lovelace", "R"), found);
        Assert.Equal(2, endpoint.Sent.Count);
        var (tokenUrl, _, form) = endpoint.Sent[0];
        Assert.Equal(GoogleAuth.TokenUrl, tokenUrl);
        var sent = System.Web.HttpUtility.ParseQueryString(form);
        Assert.Equal("authorization_code", sent["grant_type"]);
        Assert.Equal("THE-CODE", sent["code"]);
        Assert.Equal("client.apps.googleusercontent.com", sent["client_id"]);
        Assert.Equal("GOCSPX-secret", sent["client_secret"]);
        Assert.Equal(verifierChallenge, OAuthClient.Challenge(sent["code_verifier"]!));
        Assert.StartsWith("http://localhost:", sent["redirect_uri"]);
        Assert.Null(sent["scope"]);
        Assert.Equal((GoogleAuth.WhoUrl, "A"), (endpoint.Sent[1].Url, endpoint.Sent[1].Bearer));
    }

    [Fact]
    public async Task ARefreshSendsTheSecretAsksForNoScopeAndKeepsTheTokenGoogleGave()
    {
        var account = Kept("R1");
        var endpoint = new Endpoint((HttpStatusCode.OK, $$"""{ "access_token": "A1", "expires_in": 3599, "scope": "{{Granted(GoogleAuth.CalendarScope, GoogleAuth.MailScope)}}" }"""));
        var auth = Auth(endpoint);

        Assert.Equal("A1", await auth.TokenAsync(account, AccountData.Calendars, CancellationToken.None));
        // One token for every kind, held until it nearly expires.
        Assert.Equal("A1", await auth.TokenAsync(account, AccountData.Mail, CancellationToken.None));

        Assert.Single(endpoint.Sent);
        var sent = System.Web.HttpUtility.ParseQueryString(endpoint.Sent[0].Body);
        Assert.Equal(("refresh_token", "R1", "GOCSPX-secret"), (sent["grant_type"], sent["refresh_token"], sent["client_secret"]));
        Assert.Null(sent["scope"]);
        // Google gave no new refresh token: the one it gave stays.
        Assert.Equal("R1", AccountBook.Secret(AccountBook.Find(account.Id)!));
    }

    /// <summary>
    /// The person unticked Gmail and Google Tasks on the consent screen: the token says so (its scope), and those kinds are
    /// refused for consent — the one refresh, no other request — while the calendars and contacts get the token.
    /// </summary>
    [Fact]
    public async Task AKindWhoseScopeWasNotGrantedIsThatKindsConsentAlone()
    {
        var account = Kept("R1");
        var endpoint = new Endpoint((HttpStatusCode.OK, $$"""{ "access_token": "A1", "expires_in": 3599, "scope": "{{Granted(GoogleAuth.CalendarScope, GoogleAuth.ContactsScope)}}" }"""));
        var auth = Auth(endpoint);

        Assert.Equal("A1", await auth.TokenAsync(account, AccountData.Calendars, CancellationToken.None));
        Assert.Equal("A1", await auth.TokenAsync(account, AccountData.Contacts, CancellationToken.None));
        var mail = await Assert.ThrowsAsync<ConsentException>(() => auth.TokenAsync(account, AccountData.Mail, CancellationToken.None));
        var tasks = await Assert.ThrowsAsync<ConsentException>(() => auth.TokenAsync(account, AccountData.Tasks, CancellationToken.None));

        Assert.Equal(AccountData.Mail, mail.Kind);
        Assert.Equal(AccountData.Tasks, tasks.Kind);
        Assert.Equal(S.CoreAccountsOauthConsent("Gmail"), mail.Message);
        Assert.Equal(S.CoreAccountsOauthConsent("Google Tasks"), tasks.Message);
        Assert.Single(endpoint.Sent);

        // Signed in to again with everything ticked (Reconnect forgets the token): every kind has it.
        auth.Forget(account.Id);
        var again = Auth(new Endpoint((HttpStatusCode.OK, $$"""{ "access_token": "A2", "expires_in": 3599, "scope": "{{Granted(GoogleAuth.Scopes[3..])}}" }""")));
        Assert.Equal("A2", await again.TokenAsync(account, AccountData.Mail, CancellationToken.None));
    }

    [Fact]
    public async Task ATokenThatSaysNoScopeIsTakenForEveryKind()
    {
        var account = Kept("R1");
        var auth = Auth(new Endpoint((HttpStatusCode.OK, """{ "access_token": "A1", "expires_in": 3599 }""")));

        Assert.Equal("A1", await auth.TokenAsync(account, AccountData.Mail, CancellationToken.None));
    }

    [Fact]
    public async Task ARefusedRefreshIsGoogleWantingASignInAgain()
    {
        var account = Kept("revoked");
        var auth = Auth(new Endpoint((HttpStatusCode.BadRequest, """{ "error": "invalid_grant", "error_description": "Token has been expired or revoked." }""")));

        var refused = await Assert.ThrowsAsync<SignInRequiredException>(() => auth.TokenAsync(account, AccountData.Calendars, CancellationToken.None));

        Assert.Equal(S.CoreAccountsOauthSignInAgainTo(provider: "Google", address: "ada@gmail.com"), refused.Message);
    }

    [Fact]
    public async Task AnAccountWithoutASecretHereIsASignInToDoAgain()
    {
        var account = new Account { Id = Account.NewId(), Kind = AccountKind.Google, Address = "gone@gmail.com" };
        await Assert.ThrowsAsync<SignInRequiredException>(() => Auth(new Endpoint()).TokenAsync(account, AccountData.Mail, CancellationToken.None));
    }

    [Fact]
    public void AGoogleAccountGivesEverythingAndSignsInInTheBrowser()
    {
        var account = new Account { Id = "g", Kind = AccountKind.Google, Address = "ada@gmail.com" };

        Assert.Equal(AccountData.Calendars | AccountData.Contacts | AccountData.Mail | AccountData.Tasks, account.Gives);
        Assert.Equal("Google", account.KindName);
        Assert.True(account.InBrowser);
        Assert.Same(GoogleAuth.Shared, AccountSignIn.Browser(AccountKind.Google));
        var reader = Readers.For(account);
        Assert.IsAssignableFrom<ITaskReader>(reader);
        Assert.Same(account, reader.Account);
    }
}
