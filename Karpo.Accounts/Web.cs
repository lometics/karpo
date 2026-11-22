// What every account's requests share: one HttpClient, the exception that
// means "only the person can fix this", and reading JSON a value at a time.

using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;

namespace Karpo.Accounts;

/// <summary>
/// The account needs the person: the password was refused, the sign-in expired or was revoked, or
/// there is no secret on this PC. Nothing Karpo retries will mend it, so a store shows the message
/// and stops asking until the account changes.
/// </summary>
public sealed class SignInRequiredException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// The account signs in, but the person has not let Karpo at one kind of what it holds: a Microsoft account added
/// before Karpo asked for To Do, a Google account whose consent screen had a box unticked (docsccounts.md,
/// "Google"). Only signing in to it again mends it (Reconnect, AccountSignIn.SignInAgainAsync); its other kinds go
/// on as they were meanwhile. The stores say so under that kind's heading on the Accounts page (SyncPart.Consent).
/// </summary>
public sealed class ConsentException(AccountKind provider, AccountData kind, Exception? inner = null)
    : Exception(S.CoreAccountsOauthConsent(Service(provider, kind)), inner)
{
    /// <summary>The kind the person has not allowed: calendars, contacts, mail or tasks.</summary>
    public AccountData Kind { get; } = kind;

    /// <summary>
    /// What <paramref name="provider"/> calls its service for <paramref name="kind"/>, the name the person consented to it
    /// by ("Microsoft To Do", "Google Calendar", "Gmail"): products' names, never translated.
    /// </summary>
    public static string Service(AccountKind provider, AccountData kind) => (provider, kind) switch
    {
        (AccountKind.Google, AccountData.Calendars) => "Google Calendar", // not-ui: a product's name
        (AccountKind.Google, AccountData.Contacts) => "Google Contacts", // not-ui: a product's name
        (AccountKind.Google, AccountData.Mail) => "Gmail",
        (AccountKind.Google, _) => "Google Tasks", // not-ui: a product's name
        (_, AccountData.Tasks) => "Microsoft To Do", // not-ui: a product's name
        _ => "Outlook",
    };
}

/// <summary>A server answered, and said no, for a reason that is not the sign-in.</summary>
public sealed class AccountException(string message, int status = 0) : Exception(message)
{
    public int Status { get; } = status;

    /// <summary>
    /// The precondition a WebDAV server named for its refusal (RFC 4918 §16, its <c>DAV:error</c> body): the
    /// element's local name, such as <c>valid-sync-token</c>; null where it named none.
    /// </summary>
    public string? Precondition { get; init; }
}

internal static class Web
{
    /// <summary>
    /// How long one request may take before it is given up on: a bar must never wait on a dead
    /// server. First, because the clients below are made with it and static fields are set in
    /// the order they are written.
    /// </summary>
    internal static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    /// <summary>
    /// One client for every account: connections are pooled per host, and a pooled connection is
    /// let go after a few minutes so a server that moved (DNS) is followed.
    /// </summary>
    internal static readonly HttpClient Http = Make(redirects: false);

    /// <summary>For plain GETs that may be redirected, such as a mail autoconfig file.</summary>
    internal static readonly HttpClient Following = Make(redirects: true);

    private static HttpClient Make(bool redirects)
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            AutomaticDecompression = DecompressionMethods.All,
            // A DAV server's .well-known answer is a redirect it means; DavClient follows them itself
            // so a PROPFIND is not turned into a GET on the way.
            AllowAutoRedirect = redirects,
        };
        var client = new HttpClient(handler) { Timeout = Patience };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Karpo/1.0");
        return client;
    }

    internal static string? Str(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue(out string? text) ? text : null;

    internal static bool? Bool(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue(out bool flag) ? flag : null;

    internal static int? Int(JsonNode? node) => node is JsonValue value
        ? value.TryGetValue(out int number)
            ? number
            : value.TryGetValue(out string? text)
              && int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : null
        : null;

    internal static IReadOnlyList<JsonObject> Items(JsonNode? node, string name) =>
        [.. (node?[name] as JsonArray ?? []).OfType<JsonObject>()];

    internal static JsonObject? Parse(string body)
    {
        try
        {
            return JsonNode.Parse(body) as JsonObject;
        }
        catch (System.Text.Json.JsonException)
        {
            // Not JSON: a proxy's page, or a sign-in page. The status already says as much as it could.
            return null;
        }
    }
}
