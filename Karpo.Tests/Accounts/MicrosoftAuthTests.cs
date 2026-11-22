// Signing in to Microsoft by hand (Karpo.Accounts\Microsoft\MicrosoftAuth.cs):
// PKCE as RFC 7636 computes it, the authorization request, and the refresh —
// an access token kept until it nearly expires, a new refresh token written
// back, and a refused one turned into "sign in again".

using System.Net;
using System.Text;
using Karpo.Accounts;
using Karpo.Accounts.Microsoft;

namespace Karpo.Tests.Accounts;

[Collection(Collection)]
public sealed class MicrosoftAuthTests : IDisposable
{
    /// <summary>Tests that write the run's own accounts.json run one at a time.</summary>
    public const string Collection = "accounts.json";

    private readonly List<Account> added = [];

    public void Dispose()
    {
        foreach (var account in added)
        {
            AccountBook.Remove(account.Id);
        }
    }

    /// <summary>The token endpoint, answering from a list and keeping what it was sent.</summary>
    private sealed class Endpoint(params (HttpStatusCode Status, string Body)[] answers) : HttpMessageHandler
    {
        private int next;

        public List<string> Sent { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Sent.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken));
            var (status, body) = answers[Math.Min(next++, answers.Length - 1)];
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private Account Kept(string refresh)
    {
        var account = AccountBook.Add(new Account { Id = Account.NewId(), Kind = AccountKind.Microsoft, Address = "me@outlook.com" }, refresh);
        added.Add(account);
        return account;
    }

    [Fact]
    public void TheChallengeIsRfc7636sOwnExample() =>
        Assert.Equal("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM", MicrosoftAuth.Challenge("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk"));

    [Fact]
    public void TheRequestAsksToReadAndForWhichAccount()
    {
        var auth = new MicrosoftAuth(new HttpClient(new Endpoint()), "client", (_, _) => Task.CompletedTask);
        var url = Uri.UnescapeDataString(auth.Authorize("http://localhost:5000", "verifier", "state").Query);

        Assert.Contains("client_id=client", url);
        Assert.Contains("redirect_uri=http://localhost:5000", url);
        Assert.Contains("code_challenge_method=S256", url);
        Assert.Contains("prompt=select_account", url);
        // Calendars, contacts and mail read; To Do's tasks read and written; nothing else.
        Assert.Contains("offline_access User.Read Calendars.Read Contacts.Read Mail.Read Tasks.ReadWrite", url);
        Assert.DoesNotContain("Mail.ReadWrite", url);
        Assert.DoesNotContain("Calendars.ReadWrite", url);
        Assert.DoesNotContain("Send", url);
        Assert.DoesNotContain("login_hint", url);

        // Signed in to again for its tasks: Microsoft offers that account first.
        Assert.Contains("login_hint=me@outlook.com", Uri.UnescapeDataString(auth.Authorize("http://localhost:5000", "v", "s", "me@outlook.com").Query));
    }

    [Fact]
    public async Task TheReadsAndTheTasksHaveTokensOfTheirOwn()
    {
        // The reads' refresh asks for exactly what every account consented to, so one added before
        // To Do loses nothing; the tasks' asks for To Do alone.
        var account = Kept("first");
        var endpoint = new Endpoint(
            (HttpStatusCode.OK, """{ "access_token": "READ", "expires_in": 3600 }"""),
            (HttpStatusCode.OK, """{ "access_token": "TASKS", "expires_in": 3600 }"""));
        var auth = new MicrosoftAuth(new HttpClient(endpoint), "client", (_, _) => Task.CompletedTask);

        Assert.Equal("READ", await auth.TokenAsync(account, CancellationToken.None));
        Assert.Equal("TASKS", await auth.TasksTokenAsync(account, CancellationToken.None));
        Assert.Equal("READ", await auth.TokenAsync(account, CancellationToken.None));
        Assert.Equal("TASKS", await auth.TasksTokenAsync(account, CancellationToken.None));

        Assert.Equal(2, endpoint.Sent.Count);
        Assert.Contains("scope=offline_access+User.Read+Calendars.Read+Contacts.Read+Mail.Read&", endpoint.Sent[0] + "&");
        Assert.DoesNotContain("Tasks", endpoint.Sent[0]);
        Assert.Contains("scope=offline_access+Tasks.ReadWrite", endpoint.Sent[1]);
    }

    [Fact]
    public async Task AnAccountThatSignsInButNotForToDoNeedsAReconnectForItsTasksAlone()
    {
        var account = Kept("before-to-do");
        var endpoint = new Endpoint(
            (HttpStatusCode.BadRequest, """{ "error": "invalid_grant", "error_description": "AADSTS65001: The user or administrator has not consented to use the application." }"""),
            (HttpStatusCode.OK, """{ "access_token": "READ", "expires_in": 3600 }"""));
        var auth = new MicrosoftAuth(new HttpClient(endpoint), "client", (_, _) => Task.CompletedTask);

        await Assert.ThrowsAsync<ConsentException>(() => auth.TasksTokenAsync(account, CancellationToken.None));
        // The mail and the calendars go on with the token the probe brought.
        Assert.Equal("READ", await auth.TokenAsync(account, CancellationToken.None));
        Assert.Equal(2, endpoint.Sent.Count);
    }

    [Fact]
    public async Task AnAccountThatDoesNotSignInAtAllIsASignInNotAReconnect()
    {
        var account = Kept("revoked");
        var endpoint = new Endpoint((HttpStatusCode.BadRequest, """{ "error": "invalid_grant", "error_description": "AADSTS70000: The grant is expired." }"""));
        var auth = new MicrosoftAuth(new HttpClient(endpoint), "client", (_, _) => Task.CompletedTask);

        await Assert.ThrowsAsync<SignInRequiredException>(() => auth.TasksTokenAsync(account, CancellationToken.None));
    }

    [Fact]
    public async Task ARefreshIsKeptUntilItNearlyExpiresAndItsSuccessorIsWrittenBack()
    {
        var account = Kept("first");
        var endpoint = new Endpoint((HttpStatusCode.OK, """{ "access_token": "A1", "refresh_token": "second", "expires_in": 3600 }"""));
        var auth = new MicrosoftAuth(new HttpClient(endpoint), "client", (_, _) => Task.CompletedTask);

        Assert.Equal("A1", await auth.TokenAsync(account, CancellationToken.None));
        Assert.Equal("A1", await auth.TokenAsync(account, CancellationToken.None));

        Assert.Single(endpoint.Sent);
        Assert.Contains("grant_type=refresh_token", endpoint.Sent[0]);
        Assert.Contains("refresh_token=first", endpoint.Sent[0]);
        Assert.Equal("second", AccountBook.Secret(AccountBook.Find(account.Id)!));
    }

    [Fact]
    public async Task TheRefreshTokenIsTheFilesNotTheCallersCopy()
    {
        // The other app refreshed since this account was read: the newer token is the one used.
        var account = Kept("old");
        AccountBook.SetSecret(account.Id, "newer");
        var endpoint = new Endpoint((HttpStatusCode.OK, """{ "access_token": "A", "expires_in": 3600 }"""));
        var auth = new MicrosoftAuth(new HttpClient(endpoint), "client", (_, _) => Task.CompletedTask);

        await auth.TokenAsync(account, CancellationToken.None);

        Assert.Contains("refresh_token=newer", endpoint.Sent[0]);
    }

    [Fact]
    public async Task ARefusedRefreshIsASignInToDoAgain()
    {
        var account = Kept("revoked");
        var endpoint = new Endpoint((HttpStatusCode.BadRequest, """{ "error": "invalid_grant", "error_description": "AADSTS70000: The grant is expired." }"""));
        var auth = new MicrosoftAuth(new HttpClient(endpoint), "client", (_, _) => Task.CompletedTask);

        var refused = await Assert.ThrowsAsync<SignInRequiredException>(() => auth.TokenAsync(account, CancellationToken.None));
        Assert.Contains("me@outlook.com", refused.Message);
    }

    [Fact]
    public async Task AnAccountWithoutASecretHereIsASignInToDoAgain()
    {
        var account = new Account { Id = Account.NewId(), Kind = AccountKind.Microsoft, Address = "gone@outlook.com" };
        var auth = new MicrosoftAuth(new HttpClient(new Endpoint()), "client", (_, _) => Task.CompletedTask);
        await Assert.ThrowsAsync<SignInRequiredException>(() => auth.TokenAsync(account, CancellationToken.None));
    }
}
