// WebDAV over HTTP, as much of it as reading needs: PROPFIND, REPORT and GET, the
// sign-in they carry (Basic, or Digest once a server asks), and the rules about
// hrefs a DAV client gets wrong once each. Ported from hearth's DAV provider,
// without the writes.
//
// An href's id is its AbsoluteUri, never its ToString: ToString unescapes, so
// /cal/a%20b.ics and /cal/a b.ics would be two ids for one resource. What a
// sync keeps by href goes through Key, because one server escapes an href one
// way in a sync-collection and another in a multiget (%40 and @, a host's case).
// And a collection and its members are compared through Same, because servers
// differ on the trailing slash.
//
// A REPORT answers its multistatus and, for a sync-collection (RFC 6578), the
// sync-token at its end; a refusal carries its status and the precondition a
// DAV:error body names (valid-sync-token: the token is too old).

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace Karpo.Accounts.Dav;

/// <summary>One <c>response</c> of a multistatus: what it is about and what it said.</summary>
internal sealed record DavAnswer(Uri Href, int Status, IReadOnlyList<XElement> Props)
{
    internal XElement? Prop(XName name) => Props.FirstOrDefault(prop => prop.Name == name);

    internal string? Value(XName name) => Prop(name)?.Value;
}

/// <summary>What a PROPFIND answered, and where: not where it was sent when the server redirected it.</summary>
internal sealed record DavFound(Uri At, IReadOnlyList<DavAnswer> Answers);

/// <summary>What a REPORT answered: its responses, and the multistatus's own sync-token (RFC 6578), where it gave one.</summary>
internal sealed record DavReport(IReadOnlyList<DavAnswer> Answers, string? SyncToken);

/// <summary>Every XML name this client sends or reads.</summary>
internal static class Dav
{
    internal static readonly XNamespace D = "DAV:";
    internal static readonly XNamespace Cal = "urn:ietf:params:xml:ns:caldav";
    internal static readonly XNamespace Card = "urn:ietf:params:xml:ns:carddav";
    internal static readonly XNamespace Apple = "http://apple.com/ns/ical/";
    internal static readonly XNamespace Server = "http://calendarserver.org/ns/";
    internal static readonly XName DisplayName = D + "displayname";
    internal static readonly XName ResourceType = D + "resourcetype";
    internal static readonly XName CurrentUserPrincipal = D + "current-user-principal";
    internal static readonly XName CalendarHome = Cal + "calendar-home-set";
    internal static readonly XName BookHome = Card + "addressbook-home-set";
    internal static readonly XName Components = Cal + "supported-calendar-component-set";
    internal static readonly XName CalendarData = Cal + "calendar-data";
    internal static readonly XName CardData = Card + "address-data";
    internal static readonly XName CalendarColor = Apple + "calendar-color";
    internal static readonly XName GetEtag = D + "getetag";
    internal static readonly XName SyncToken = D + "sync-token";
    internal static readonly XName SupportedReports = D + "supported-report-set";
    internal static readonly XName GetCtag = Server + "getctag";
}

internal sealed class DavClient(HttpClient http, string user, string password)
{
    private static readonly HttpMethod Propfind = new("PROPFIND");
    private static readonly HttpMethod Report = new("REPORT");

    /// <summary>The digest challenge this client works under, where the server asked for one.</summary>
    private DigestChallenge? digest;

    private int nonceCount;

    /// <summary>Whether two hrefs name one resource, whatever the trailing slash and however each is escaped.</summary>
    internal static bool Same(Uri one, Uri other) =>
        string.Equals(Key(one).TrimEnd('/'), Key(other).TrimEnd('/'), StringComparison.Ordinal);

    /// <summary>
    /// An href as one string whoever spelled it: absolute, its host in lower case, its default port left out, and
    /// each part of its path unescaped and escaped again the one way, so <c>/cal/me%40x/a.ics</c> and
    /// <c>/cal/me@x/a.ics</c>, or a host in capitals, are one key. What a sync keeps an object by.
    /// </summary>
    internal static string Key(Uri href)
    {
        if (!href.IsAbsoluteUri)
        {
            return href.OriginalString;
        }
        var path = string.Join('/', href.AbsolutePath.Split('/').Select(part => Uri.EscapeDataString(Uri.UnescapeDataString(part))));
        var port = href.IsDefaultPort ? "" : ":" + href.Port.ToString(CultureInfo.InvariantCulture);
        return $"{href.Scheme}://{href.Host.ToLowerInvariant()}{port}{path}";
    }

    /// <summary>
    /// One PROPFIND, following redirects and saying where it ended up. The redirect is discovery's
    /// first step: RFC 6764 has <c>/.well-known/caldav</c> answer with one.
    /// </summary>
    internal async Task<DavFound> PropfindAsync(Uri url, string props, int depth, CancellationToken cancellation)
    {
        var body =
            $"""<?xml version="1.0" encoding="utf-8"?><propfind xmlns="DAV:" """
            + $"""xmlns:c="{Dav.Cal}" xmlns:a="{Dav.Card}" xmlns:i="{Dav.Apple}" xmlns:cs="{Dav.Server}">"""
            + $"<prop>{props}</prop></propfind>";

        var at = url;
        for (var hop = 0; hop < 5; hop++)
        {
            using var answer = await SendAsync(
                () => Request(Propfind, at, body, depth), cancellation).ConfigureAwait(false);
            if (Redirect(answer, at) is { } next)
            {
                at = next;
                continue;
            }
            var text = await Read(answer, cancellation).ConfigureAwait(false);
            return new DavFound(at, Multistatus(text, at));
        }
        throw new AccountException(S.CoreAccountsRedirectLoop, 508);
    }

    /// <summary>
    /// One REPORT and its answers: depth 1 for a query or a multiget, 0 for a sync-collection (RFC 6578 §3.2), which
    /// also answers the sync-token its multistatus ends with.
    /// </summary>
    internal async Task<DavReport> ReportAsync(Uri url, string body, CancellationToken cancellation, int depth = 1)
    {
        using var answer = await SendAsync(() => Request(Report, url, body, depth), cancellation).ConfigureAwait(false);
        var text = await Read(answer, cancellation).ConfigureAwait(false);
        return new DavReport(Multistatus(text, url), Token(text));
    }

    /// <summary>One GET, signed in as the others: a card, or a picture on the account's own server. Its body.</summary>
    internal async Task<byte[]> GetAsync(Uri url, CancellationToken cancellation)
    {
        using var answer = await SendAsync(() => Request(HttpMethod.Get, url, null, null), cancellation).ConfigureAwait(false);
        await CheckAsync(answer, cancellation).ConfigureAwait(false);
        return await answer.Content.ReadAsByteArrayAsync(cancellation).ConfigureAwait(false);
    }

    /// <summary>
    /// The answers of one multistatus. A body that is not XML is no answers: it is a proxy's error
    /// page, and the status has already been checked. Every href leaves here absolute.
    /// </summary>
    internal static IReadOnlyList<DavAnswer> Multistatus(string xml, Uri? at = null)
    {
        XDocument document;
        try
        {
            document = XDocument.Parse(xml);
        }
        catch (System.Xml.XmlException)
        {
            return [];
        }

        var answers = new List<DavAnswer>();
        foreach (var response in document.Descendants(Dav.D + "response"))
        {
            if (response.Element(Dav.D + "href")?.Value is not { Length: > 0 } href
                || Absolute(href.Trim(), at) is not { } url)
            {
                continue;
            }
            var props = new List<XElement>();
            var status = Status(response.Element(Dav.D + "status")?.Value) ?? 200;
            foreach (var propstat in response.Elements(Dav.D + "propstat"))
            {
                var part = Status(propstat.Element(Dav.D + "status")?.Value) ?? 200;
                if (part is >= 200 and < 300)
                {
                    props.AddRange(propstat.Element(Dav.D + "prop")?.Elements() ?? []);
                }
            }
            answers.Add(new DavAnswer(url, status, props));
        }
        return answers;
    }

    /// <summary>The sync-token a multistatus ends with (RFC 6578 §6.4); null where it has none or is not XML.</summary>
    internal static string? Token(string xml)
    {
        try
        {
            return XDocument.Parse(xml).Root?.Element(Dav.SyncToken)?.Value.Trim() is { Length: > 0 } token ? token : null;
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
    }

    /// <summary>
    /// The precondition a refusal's body names (RFC 4918 §16: <c>&lt;d:error&gt;&lt;d:valid-sync-token/&gt;&lt;/d:error&gt;</c>),
    /// its local name; null where the body is no DAV:error.
    /// </summary>
    internal static string? Precondition(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }
        try
        {
            return XDocument.Parse(body).Root is { } root && root.Name == Dav.D + "error" ? root.Elements().FirstOrDefault()?.Name.LocalName : null;
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
    }

    /// <summary>The number out of a status line such as <c>HTTP/1.1 404 Not Found</c>.</summary>
    internal static int? Status(string? line)
    {
        foreach (var part in (line ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (int.TryParse(part, CultureInfo.InvariantCulture, out var status) && status is >= 100 and < 600)
            {
                return status;
            }
        }
        return null;
    }

    /// <summary>An href as an absolute URI: a server may answer with a path alone.</summary>
    internal static Uri? Absolute(string href, Uri? at)
    {
        if (Uri.TryCreate(href, UriKind.Absolute, out var absolute) && absolute.Scheme is "http" or "https")
        {
            return absolute;
        }
        return at is not null && Uri.TryCreate(at, href, out var resolved) ? resolved : null;
    }

    /// <summary>The path a request names an href by, escaped as it arrived.</summary>
    internal static string Path(Uri href) => href.IsAbsoluteUri ? href.AbsolutePath : href.OriginalString;

    internal static string Escaped(string text) =>
        text.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);

    private static Uri? Redirect(HttpResponseMessage answer, Uri at) =>
        (int)answer.StatusCode is 301 or 302 or 303 or 307 or 308 && answer.Headers.Location is { } location
            ? location.IsAbsoluteUri ? location : new Uri(at, location)
            : null;

    private HttpRequestMessage Request(HttpMethod method, Uri url, string? body, int? depth)
    {
        var request = new HttpRequestMessage(method, url);
        if (body is not null)
        {
            request.Content = new StringContent(body, Encoding.UTF8);
            request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/xml; charset=utf-8");
        }
        if (depth is { } deep)
        {
            request.Headers.TryAddWithoutValidation("Depth", deep.ToString(CultureInfo.InvariantCulture));
        }
        request.Headers.Authorization = digest is { } challenge
            ? new AuthenticationHeaderValue("Digest", Digest(challenge, method.Method, url, Interlocked.Increment(ref nonceCount)))
            : new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{password}")));
        return request;
    }

    /// <summary>One request, made again once where the server answered 401 with a Digest challenge.</summary>
    private async Task<HttpResponseMessage> SendAsync(Func<HttpRequestMessage> make, CancellationToken cancellation)
    {
        using (var first = make())
        {
            var answer = await http.SendAsync(first, cancellation).ConfigureAwait(false);
            if (answer.StatusCode != HttpStatusCode.Unauthorized)
            {
                return answer;
            }
            Refused(answer);
            if (DigestChallenge.Of(answer) is not { } challenge || digest is not null)
            {
                answer.Dispose();
                throw new SignInRequiredException(S.CoreAccountsRefusedPassword);
            }
            digest = challenge;
            answer.Dispose();
        }

        using var again = make();
        var second = await http.SendAsync(again, cancellation).ConfigureAwait(false);
        if (second.StatusCode == HttpStatusCode.Unauthorized)
        {
            second.Dispose();
            throw new SignInRequiredException(S.CoreAccountsRefusedPassword);
        }
        return second;
    }

    /// <summary>A server offering only a bearer token wants OAuth, which a password account cannot give.</summary>
    private static void Refused(HttpResponseMessage answer)
    {
        var schemes = answer.Headers.WwwAuthenticate.Select(scheme => scheme.Scheme).ToList();
        if (schemes.Count > 0 && schemes.All(scheme => scheme.Equals("Bearer", StringComparison.OrdinalIgnoreCase)))
        {
            answer.Dispose();
            throw new SignInRequiredException(
                S.CoreAccountsOauthOnly);
        }
    }

    /// <summary>The body, or a refusal carrying the status.</summary>
    private static async Task<string> Read(HttpResponseMessage answer, CancellationToken cancellation)
    {
        await CheckAsync(answer, cancellation).ConfigureAwait(false);
        return await answer.Content.ReadAsStringAsync(cancellation).ConfigureAwait(false);
    }

    /// <summary>Nothing for a 2xx; a refusal carrying the status, and the precondition its body names, for anything else.</summary>
    private static async Task CheckAsync(HttpResponseMessage answer, CancellationToken cancellation)
    {
        var status = (int)answer.StatusCode;
        if (status is >= 200 and < 300)
        {
            return;
        }
        // Only a refusal that may name one is read: a 403, 409 or 412 (RFC 6578 §3.2's valid-sync-token among them).
        var precondition = status is 403 or 409 or 412
            ? Precondition(await answer.Content.ReadAsStringAsync(cancellation).ConfigureAwait(false))
            : null;
        throw new AccountException(status switch
        {
            403 => S.CoreAccountsRefused403,
            404 => S.CoreAccountsNotFound404,
            405 => S.CoreAccountsNotAllowed405,
            _ => S.CoreAccountsServerAnswered($"{status.ToString(CultureInfo.InvariantCulture)} {answer.ReasonPhrase}".TrimEnd()),
        }, status)
        {
            Precondition = precondition,
        };
    }

    /// <summary>The digest response of one request, as RFC 7616 computes it.</summary>
    private string Digest(DigestChallenge challenge, string method, Uri url, int count)
    {
        var path = url.IsAbsoluteUri ? url.PathAndQuery : url.OriginalString;
        var nc = count.ToString("x8", CultureInfo.InvariantCulture);
        var cnonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant();
        var ha1 = challenge.Hash($"{user}:{challenge.Realm}:{password}");
        var ha2 = challenge.Hash($"{method}:{path}");
        var response = challenge.Qop.Length > 0
            ? challenge.Hash($"{ha1}:{challenge.Nonce}:{nc}:{cnonce}:{challenge.Qop}:{ha2}")
            : challenge.Hash($"{ha1}:{challenge.Nonce}:{ha2}");

        var header = new StringBuilder();
        header.Append("username=\"").Append(user)
            .Append("\", realm=\"").Append(challenge.Realm)
            .Append("\", nonce=\"").Append(challenge.Nonce)
            .Append("\", uri=\"").Append(path)
            .Append("\", response=\"").Append(response)
            .Append("\", algorithm=").Append(challenge.Algorithm);
        if (challenge.Opaque is { Length: > 0 } opaque)
        {
            header.Append(", opaque=\"").Append(opaque).Append('"');
        }
        if (challenge.Qop.Length > 0)
        {
            header.Append(", qop=").Append(challenge.Qop).Append(", nc=").Append(nc).Append(", cnonce=\"").Append(cnonce).Append('"');
        }
        return header.ToString();
    }
}

/// <summary>A server's Digest challenge: in practice an app password over TLS.</summary>
internal sealed record DigestChallenge(string Realm, string Nonce, string Qop, string Algorithm, string? Opaque)
{
    internal static DigestChallenge? Of(HttpResponseMessage answer)
    {
        foreach (var scheme in answer.Headers.WwwAuthenticate)
        {
            if (!string.Equals(scheme.Scheme, "Digest", StringComparison.OrdinalIgnoreCase)
                || scheme.Parameter is not { Length: > 0 } parameters)
            {
                continue;
            }
            var parts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var one in Split(parameters))
            {
                var equals = one.IndexOf('=', StringComparison.Ordinal);
                if (equals > 0)
                {
                    parts[one[..equals].Trim()] = one[(equals + 1)..].Trim().Trim('"');
                }
            }
            if (!parts.TryGetValue("nonce", out var nonce))
            {
                continue;
            }
            // qop may be a list; auth is the only one without a body to hash.
            var qop = parts.TryGetValue("qop", out var offered) && offered.Split(',').Any(one => one.Trim() == "auth") ? "auth" : "";
            return new DigestChallenge(
                parts.GetValueOrDefault("realm", ""), nonce, qop, parts.GetValueOrDefault("algorithm", "MD5"), parts.GetValueOrDefault("opaque"));
        }
        return null;
    }

    /// <summary>The hash the challenge names: MD5, or SHA-256. Neither protects the password; TLS does.</summary>
    internal string Hash(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var hash = Algorithm.StartsWith("SHA-256", StringComparison.OrdinalIgnoreCase) ? SHA256.HashData(bytes) : MD5.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static IEnumerable<string> Split(string parameters)
    {
        var part = new StringBuilder();
        var quoted = false;
        foreach (var c in parameters)
        {
            if (c == '"')
            {
                quoted = !quoted;
            }
            if (c == ',' && !quoted)
            {
                yield return part.ToString();
                part.Clear();
                continue;
            }
            part.Append(c);
        }
        if (part.Length > 0)
        {
            yield return part.ToString();
        }
    }
}
