// Where a CalDAV/CardDAV account's calendars and address books live
// (RFC 6764, ported from hearth): the domain's .well-known address, what it
// announces over SRV, and the server the person typed — each asked for the
// current user's principal, and the principal for its two homes.
//
// The typed server is last: a person types a host when the domain has nothing
// to say for itself. A typed whole URL is taken at its word all the same.

using System.Globalization;
using System.Xml.Linq;

namespace Karpo.Accounts.Dav;

/// <summary>Where the collections are: the principal, and the two homes hanging off it (empty where there is none).</summary>
public sealed record DavHome(Uri Principal, string Calendars, string Books, string Name, string Step)
{
    public bool Anything => Calendars.Length > 0 || Books.Length > 0;
}

public sealed class DavDiscovery(IDnsLookup lookup)
{
    internal const string WellKnown = "the domain's .well-known address";
    internal const string Announced = "what the domain announces (SRV)";
    internal const string Typed = "the server you typed";

    private const string Principal = "<current-user-principal/><displayname/>";
    private const string Homes = "<displayname/><c:calendar-home-set/><a:addressbook-home-set/>";

    public DavDiscovery()
        : this(new SystemDnsLookup())
    {
    }

    /// <summary>
    /// Where <paramref name="user"/>'s collections are, having tried every step; the first that
    /// finds a home wins.
    /// </summary>
    /// <param name="typed">A server or URL the person typed; may be empty.</param>
    /// <param name="address">The account's address, whose domain is asked first; may be empty.</param>
    /// <exception cref="AccountException">No server answered; the message says what was tried.</exception>
    /// <exception cref="SignInRequiredException">A server refused the password.</exception>
    public async Task<DavHome> FindAsync(
        string typed, string address, string user, string password, CancellationToken cancellation)
    {
        var client = new DavClient(Web.Http, user, password);
        var domain = Domain(address);
        var tried = new List<string>();

        var found = await OverAsync(client, "caldav", typed, domain, tried, cancellation).ConfigureAwait(false);
        if (found is { Books.Length: 0 })
        {
            // Address books somewhere else entirely, which is what the second .well-known name is for.
            if (await OverAsync(client, "carddav", typed, domain, tried, cancellation).ConfigureAwait(false) is { Books.Length: > 0 } books)
            {
                found = found with { Books = books.Books };
            }
        }
        found ??= await OverAsync(client, "carddav", typed, domain, tried, cancellation).ConfigureAwait(false);

        return found ?? throw new AccountException(domain.Length > 0
            ? S.CoreAccountsNoDavFor(domain: domain, tried: string.Join("; ", tried))
            : S.CoreAccountsNoDav(string.Join("; ", tried)));
    }

    /// <summary>The domain of an address, or empty where it is not one.</summary>
    internal static string Domain(string address)
    {
        var at = address.LastIndexOf('@');
        return at > 0 && at < address.Length - 1 ? address[(at + 1)..].Trim().ToLowerInvariant() : "";
    }

    /// <summary>The places to try for one service, in RFC 6764's order.</summary>
    internal async Task<IReadOnlyList<(string Step, Uri Url)>> CandidatesAsync(
        string service, string typed, string domain, CancellationToken cancellation)
    {
        var candidates = new List<(string, Uri)>();
        if (domain.Length > 0 && Uri.TryCreate($"https://{domain}/.well-known/{service}", UriKind.Absolute, out var known))
        {
            candidates.Add((WellKnown, known));
        }
        if (await AnnouncedAsync(service, domain, cancellation).ConfigureAwait(false) is { } announced)
        {
            candidates.Add((Announced, announced));
        }
        foreach (var url in TypedUrls(typed.Trim(), service))
        {
            candidates.Add((Typed, url));
        }
        return [.. candidates.DistinctBy(candidate => candidate.Item2.AbsoluteUri)];
    }

    private async Task<DavHome?> OverAsync(
        DavClient client, string service, string typed, string domain, List<string> tried, CancellationToken cancellation)
    {
        foreach (var (step, url) in await CandidatesAsync(service, typed, domain, cancellation).ConfigureAwait(false))
        {
            try
            {
                if (await AtAsync(client, step, url, cancellation).ConfigureAwait(false) is { Anything: true } home)
                {
                    return home;
                }
                tried.Add($"{step} ({url.Host}): no calendars or address books there");
            }
            catch (Exception refused) when (refused is AccountException or HttpRequestException or IOException
                || (refused is OperationCanceledException && !cancellation.IsCancellationRequested))
            {
                tried.Add($"{step} ({url.Host}): {refused.Message}");
            }
        }
        return null;
    }

    /// <summary>One candidate: the principal it names, and the homes that principal names.</summary>
    private static async Task<DavHome?> AtAsync(DavClient client, string step, Uri url, CancellationToken cancellation)
    {
        var root = await client.PropfindAsync(url, Principal, 0, cancellation).ConfigureAwait(false);
        var principal = root.Answers
            .Select(answer => answer.Prop(Dav.CurrentUserPrincipal)?.Element(Dav.D + "href")?.Value)
            .Where(href => href is { Length: > 0 })
            .Select(href => DavClient.Absolute(href!.Trim(), root.At))
            .FirstOrDefault(found => found is not null);
        if (principal is null)
        {
            return null;
        }
        var found = await client.PropfindAsync(principal, Homes, 0, cancellation).ConfigureAwait(false);
        return new DavHome(
            principal,
            Href(found, Dav.CalendarHome),
            Href(found, Dav.BookHome),
            found.Answers.Select(answer => answer.Value(Dav.DisplayName)).FirstOrDefault(name => name is { Length: > 0 }) ?? "",
            step);
    }

    /// <summary>The one href inside a home-set property, absolute; empty where there is none.</summary>
    private static string Href(DavFound found, XName name) => found.Answers
        .Select(answer => answer.Prop(name)?.Element(Dav.D + "href")?.Value)
        .Where(href => href is { Length: > 0 })
        .Select(href => DavClient.Absolute(href!.Trim(), found.At))
        .FirstOrDefault(url => url is not null)
        ?.AbsoluteUri ?? "";

    /// <summary>What the domain announces: <c>_caldavs._tcp</c>'s host and port, with the path its TXT names.</summary>
    private async Task<Uri?> AnnouncedAsync(string service, string domain, CancellationToken cancellation)
    {
        if (domain.Length == 0)
        {
            return null;
        }
        var name = $"_{service}s._tcp.{domain}";
        var records = await lookup.ServicesAsync(name, cancellation).ConfigureAwait(false);
        if (records.Count == 0)
        {
            return null;
        }
        var path = (await lookup.TextAsync(name, cancellation).ConfigureAwait(false))
            .Select(text => text.Trim())
            .FirstOrDefault(text => text.StartsWith("path=", StringComparison.OrdinalIgnoreCase))
            ?[5..] ?? $"/.well-known/{service}";
        var record = records[0];
        var port = record.Port == 443 ? "" : ":" + record.Port.ToString(CultureInfo.InvariantCulture);
        return Uri.TryCreate($"https://{record.Host}{port}{(path.StartsWith('/') ? path : "/" + path)}", UriKind.Absolute, out var url)
            ? url
            : null;
    }

    /// <summary>What the person typed, as URLs: a whole URL as it stands, a bare host as its well-known address and its root.</summary>
    internal static IReadOnlyList<Uri> TypedUrls(string typed, string service)
    {
        if (typed.Length == 0)
        {
            return [];
        }
        if (typed.Contains("://", StringComparison.Ordinal))
        {
            return Uri.TryCreate(typed, UriKind.Absolute, out var url) ? [url] : [];
        }
        var urls = new List<Uri>();
        if (Uri.TryCreate($"https://{typed}/.well-known/{service}", UriKind.Absolute, out var known))
        {
            urls.Add(known);
        }
        if (Uri.TryCreate($"https://{typed}/", UriKind.Absolute, out var root))
        {
            urls.Add(root);
        }
        return urls;
    }
}
