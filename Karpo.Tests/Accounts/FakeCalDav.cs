// A CalDAV server in memory for §616's sync (no real server is ever asked): a
// home of calendars, each a set of objects with etags and a change log that
// sync-collection tokens count in. Its ways are switches, one per way a real
// server behaves: no sync-collection, no ctag, no supported-report-set, pages
// of a sync-collection, tokens it prunes, multigets it cuts short, a calendar it
// refuses, hrefs it escapes one way here and another there, deletions it loses,
// requests it throttles. It writes down every request it is sent.

using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Karpo.Tests.Accounts;

internal sealed partial class FakeCalDav : HttpMessageHandler
{
    public const string Host = "https://dav.example.com";

    public const string HomePath = "/cal/";

    private readonly Lock gate = new();

    private readonly List<Calendar> calendars = [];

    /// <summary>It answers sync-collection; else a REPORT of one is refused with <see cref="NoSyncStatus"/>.</summary>
    public bool SyncCollection { get; set; } = true;

    /// <summary>What a sync-collection is refused with where the server has none.</summary>
    public int NoSyncStatus { get; set; } = 501;

    /// <summary>A calendar's listing carries its getctag.</summary>
    public bool CTag { get; set; } = true;

    /// <summary>A calendar's listing says which REPORTs it takes; else it says nothing and is tried.</summary>
    public bool SupportedReportSet { get; set; } = true;

    /// <summary>A sync-collection with any Depth but 0 is refused (400), as RFC 6578 has it.</summary>
    public bool RequireDepth0 { get; set; } = true;

    /// <summary>A sync-collection answers at most this many changes, the calendar itself with a 507 and a token to go on from.</summary>
    public int? PageSize { get; set; }

    /// <summary>Its pages never move on: the token it gives with a 507 is the one it was asked with.</summary>
    public bool PagesStuck { get; set; }

    /// <summary>A token of a version before this is refused: 403 with DAV:valid-sync-token, as Nextcloud prunes them.</summary>
    public int PruneBefore { get; set; }

    /// <summary>A multiget answers at most this many objects, then a 507 for the calendar.</summary>
    public int? MultigetMost { get; set; }

    /// <summary>A multiget leaves these out of its answer altogether, without a 507: they "did not come".</summary>
    public HashSet<string> MultigetDrops { get; } = new(StringComparer.Ordinal);

    /// <summary>The calendars (by their path's name) whose every request is refused with 403.</summary>
    public HashSet<string> Refuse { get; } = new(StringComparer.Ordinal);

    /// <summary>A listing writes hrefs absolute, its host in capitals, '@' as %40; a multiget writes paths with '@' plain.</summary>
    public bool EscapeHrefsDifferently { get; set; }

    /// <summary>A sync-collection never says an object went: only a whole listing shows it.</summary>
    public bool LoseDeletions { get; set; }

    /// <summary>The next this many requests are answered 429, too many.</summary>
    public int Throttle { get; set; }

    /// <summary>After this many more requests, every one is answered 503: a server that goes away partway through.</summary>
    public int? FailAfter { get; set; }

    /// <summary>Every request: method, path, Depth, and for a REPORT its kind and size ("sync-collection token=…", "multiget 100").</summary>
    public List<string> Requests { get; } = [];

    public int Count(string kind)
    {
        lock (gate)
        {
            return Requests.Count(request => request.Contains(kind, StringComparison.Ordinal));
        }
    }

    public void Forget()
    {
        lock (gate)
        {
            Requests.Clear();
        }
    }

    /// <summary>A calendar, its path <c>/cal/{name}/</c>, shown as <paramref name="display"/>.</summary>
    public FakeCalDav With(string name, string? display = null)
    {
        lock (gate)
        {
            calendars.Add(new Calendar(name, display ?? name));
        }
        return this;
    }

    /// <summary>The calendar's id as the reader gives it (its href, absolute).</summary>
    public static string Id(string calendar) => $"{Host}{HomePath}{calendar}/";

    /// <summary>An object put in, or changed: a new etag, a change logged.</summary>
    public void Put(string calendar, string name, string text)
    {
        lock (gate)
        {
            var found = Find(calendar);
            found.Version++;
            found.Objects[name] = ($"\"{found.Version}-{name.GetHashCode(StringComparison.Ordinal):x}\"", text);
            found.Log.Add((found.Version, name));
        }
    }

    /// <summary>An event of an hour, its UID the object's name.</summary>
    public void PutEvent(string calendar, string name, string summary, DateTime startUtc, string? more = null) =>
        Put(calendar, name, Event(name, summary, startUtc, more));

    public static string Event(string uid, string summary, DateTime startUtc, string? more = null) =>
        "BEGIN:VCALENDAR\r\nVERSION:2.0\r\nBEGIN:VEVENT\r\n"
        + $"UID:{uid}\r\nSUMMARY:{summary}\r\nDTSTART:{startUtc:yyyyMMdd'T'HHmmss'Z'}\r\nDTEND:{startUtc.AddHours(1):yyyyMMdd'T'HHmmss'Z'}\r\n"
        + "DESCRIPTION:Private notes that must never be kept\r\nATTENDEE:mailto:someone@example.com\r\n"
        + more
        + "END:VEVENT\r\nEND:VCALENDAR\r\n";

    public void Delete(string calendar, string name)
    {
        lock (gate)
        {
            var found = Find(calendar);
            if (found.Objects.Remove(name))
            {
                found.Version++;
                found.Log.Add((found.Version, name));
            }
        }
    }

    /// <summary>The objects a calendar holds now, by name.</summary>
    public IReadOnlyCollection<string> Names(string calendar)
    {
        lock (gate)
        {
            return [.. Find(calendar).Objects.Keys];
        }
    }

    private Calendar Find(string name) => calendars.Single(calendar => calendar.Name == name);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
        var path = Uri.UnescapeDataString(request.RequestUri!.AbsolutePath);
        var depth = request.Headers.TryGetValues("Depth", out var depths) ? depths.First() : "";
        lock (gate)
        {
            var kind = request.Method.Method == "REPORT" ? Kind(body) : "";
            Requests.Add($"{request.Method.Method} {path} {depth} {kind}".TrimEnd());
            if (FailAfter is { } left)
            {
                FailAfter = left - 1;
                if (left <= 0)
                {
                    return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("") };
                }
            }
            if (Throttle > 0)
            {
                Throttle--;
                return new HttpResponseMessage((HttpStatusCode)429) { Content = new StringContent("") };
            }
            var at = calendars.FirstOrDefault(calendar => path.StartsWith($"{HomePath}{calendar.Name}/", StringComparison.Ordinal));
            if (at is not null && Refuse.Contains(at.Name))
            {
                return new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = new StringContent("") };
            }
            if (request.Method.Method == "PROPFIND")
            {
                return path == HomePath ? Answer(HomeListing()) : at is null ? NotFound() : Answer(Listing(at));
            }
            if (at is null)
            {
                return NotFound();
            }
            if (body.Contains("sync-collection", StringComparison.Ordinal))
            {
                if (!SyncCollection)
                {
                    return new HttpResponseMessage((HttpStatusCode)NoSyncStatus) { Content = new StringContent("") };
                }
                if (RequireDepth0 && depth != "0")
                {
                    return new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("") };
                }
                return Sync(at, XDocument.Parse(body).Descendants(XName.Get("sync-token", "DAV:")).Single().Value);
            }
            if (body.Contains("calendar-multiget", StringComparison.Ordinal))
            {
                return Multiget(at, [.. XDocument.Parse(body).Descendants(XName.Get("href", "DAV:")).Select(href => Uri.UnescapeDataString(href.Value))]);
            }
            return new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("") };
        }
    }

    private static string Kind(string body)
    {
        if (body.Contains("sync-collection", StringComparison.Ordinal))
        {
            var token = XDocument.Parse(body).Descendants(XName.Get("sync-token", "DAV:")).Single().Value;
            return $"sync-collection token={token}";
        }
        return body.Contains("multiget", StringComparison.Ordinal)
            ? $"multiget {HrefCount().Count(body)}"
            : "report";
    }

    private string HomeListing()
    {
        var xml = new StringBuilder($"""<d:multistatus xmlns:d="DAV:" xmlns:c="urn:ietf:params:xml:ns:caldav" xmlns:cs="http://calendarserver.org/ns/">""");
        xml.Append($"<d:response><d:href>{HomePath}</d:href><d:propstat><d:prop><d:resourcetype><d:collection/></d:resourcetype></d:prop><d:status>HTTP/1.1 200 OK</d:status></d:propstat></d:response>");
        foreach (var calendar in calendars)
        {
            xml.Append($"<d:response><d:href>{HomePath}{calendar.Name}/</d:href><d:propstat><d:prop>")
                .Append($"<d:displayname>{calendar.Display}</d:displayname><d:resourcetype><d:collection/><c:calendar/></d:resourcetype>")
                .Append("""<c:supported-calendar-component-set><c:comp name="VEVENT"/></c:supported-calendar-component-set>""");
            if (SyncCollection)
            {
                xml.Append($"<d:sync-token>{Token(calendar, calendar.Version)}</d:sync-token>");
            }
            if (CTag)
            {
                xml.Append($"<cs:getctag>ctag-{calendar.Version}</cs:getctag>");
            }
            if (SupportedReportSet)
            {
                xml.Append("<d:supported-report-set>")
                    .Append("<d:supported-report><d:report><c:calendar-multiget/></d:report></d:supported-report>")
                    .Append("<d:supported-report><d:report><c:calendar-query/></d:report></d:supported-report>");
                if (SyncCollection)
                {
                    xml.Append("<d:supported-report><d:report><d:sync-collection/></d:report></d:supported-report>");
                }
                xml.Append("</d:supported-report-set>");
            }
            xml.Append("</d:prop><d:status>HTTP/1.1 200 OK</d:status></d:propstat></d:response>");
        }
        return xml.Append("</d:multistatus>").ToString();
    }

    private string Listing(Calendar calendar)
    {
        var xml = new StringBuilder("""<d:multistatus xmlns:d="DAV:">""");
        xml.Append($"<d:response><d:href>{HomePath}{calendar.Name}/</d:href><d:propstat><d:prop><d:resourcetype><d:collection/></d:resourcetype></d:prop><d:status>HTTP/1.1 200 OK</d:status></d:propstat></d:response>");
        foreach (var (name, (etag, _)) in calendar.Objects)
        {
            xml.Append($"<d:response><d:href>{Href(calendar, name, listing: true)}</d:href><d:propstat><d:prop><d:getetag>{Escape(etag)}</d:getetag><d:resourcetype/></d:prop><d:status>HTTP/1.1 200 OK</d:status></d:propstat></d:response>");
        }
        return xml.Append("</d:multistatus>").ToString();
    }

    private HttpResponseMessage Sync(Calendar calendar, string token)
    {
        // A token is "…/sync/{calendar}/{version}", or "…+{offset}" partway through the changes since that version.
        var since = 0;
        var offset = 0;
        if (token.Length > 0)
        {
            var match = TokenPattern().Match(token);
            if (!match.Success || match.Groups["calendar"].Value != calendar.Name)
            {
                return Refused("valid-sync-token");
            }
            since = int.Parse(match.Groups["version"].Value, CultureInfo.InvariantCulture);
            offset = match.Groups["offset"].Success ? int.Parse(match.Groups["offset"].Value, CultureInfo.InvariantCulture) : 0;
            if (since < PruneBefore)
            {
                return Refused("valid-sync-token");
            }
        }
        var changes = token.Length == 0
            ? calendar.Objects.Keys.ToList()
            : [.. calendar.Log.Where(change => change.Version > since).Select(change => change.Name).Distinct()];
        if (LoseDeletions)
        {
            changes = [.. changes.Where(calendar.Objects.ContainsKey)];
        }
        var page = changes.Skip(offset).Take(PageSize ?? int.MaxValue).ToList();
        var more = offset + page.Count < changes.Count;
        var xml = new StringBuilder("""<d:multistatus xmlns:d="DAV:">""");
        foreach (var name in page)
        {
            xml.Append(calendar.Objects.TryGetValue(name, out var held)
                ? $"<d:response><d:href>{Href(calendar, name, listing: true)}</d:href><d:propstat><d:prop><d:getetag>{Escape(held.Etag)}</d:getetag></d:prop><d:status>HTTP/1.1 200 OK</d:status></d:propstat></d:response>"
                : $"<d:response><d:href>{Href(calendar, name, listing: true)}</d:href><d:status>HTTP/1.1 404 Not Found</d:status></d:response>");
        }
        string next;
        if (more)
        {
            xml.Append($"<d:response><d:href>{HomePath}{calendar.Name}/</d:href><d:status>HTTP/1.1 507 Insufficient Storage</d:status></d:response>");
            next = PagesStuck ? token : $"{Token(calendar, since)}+{offset + page.Count}";
        }
        else
        {
            next = Token(calendar, calendar.Version);
        }
        return Answer(xml.Append($"<d:sync-token>{next}</d:sync-token></d:multistatus>").ToString());
    }

    private HttpResponseMessage Multiget(Calendar calendar, List<string> paths)
    {
        var xml = new StringBuilder("""<d:multistatus xmlns:d="DAV:" xmlns:c="urn:ietf:params:xml:ns:caldav">""");
        var answered = 0;
        foreach (var path in paths)
        {
            var name = path[(path.LastIndexOf('/') + 1)..];
            if (MultigetDrops.Contains(name))
            {
                continue;
            }
            if (MultigetMost is { } most && answered >= most)
            {
                xml.Append($"<d:response><d:href>{HomePath}{calendar.Name}/</d:href><d:status>HTTP/1.1 507 Insufficient Storage</d:status></d:response>");
                break;
            }
            answered++;
            xml.Append(calendar.Objects.TryGetValue(name, out var held)
                ? $"<d:response><d:href>{Href(calendar, name, listing: false)}</d:href><d:propstat><d:prop><d:getetag>{Escape(held.Etag)}</d:getetag><c:calendar-data>{Escape(held.Text)}</c:calendar-data></d:prop><d:status>HTTP/1.1 200 OK</d:status></d:propstat></d:response>"
                : $"<d:response><d:href>{Href(calendar, name, listing: false)}</d:href><d:status>HTTP/1.1 404 Not Found</d:status></d:response>");
        }
        return Answer(xml.Append("</d:multistatus>").ToString());
    }

    private string Href(Calendar calendar, string name, bool listing) =>
        EscapeHrefsDifferently && listing
            ? $"https://DAV.EXAMPLE.COM{HomePath}{calendar.Name}/{Uri.EscapeDataString(name)}"
            : $"{HomePath}{calendar.Name}/{name}";

    private static string Token(Calendar calendar, int version) => $"http://dav.example.com/sync/{calendar.Name}/{version}";

    private static HttpResponseMessage Refused(string precondition) =>
        new(HttpStatusCode.Forbidden) { Content = new StringContent($"""<?xml version="1.0"?><d:error xmlns:d="DAV:"><d:{precondition}/></d:error>""", Encoding.UTF8, "application/xml") };

    private static HttpResponseMessage NotFound() => new(HttpStatusCode.NotFound) { Content = new StringContent("") };

    private static HttpResponseMessage Answer(string xml) =>
        new(HttpStatusCode.MultiStatus) { Content = new StringContent(xml, Encoding.UTF8, "application/xml") };

    private static string Escape(string text) => System.Security.SecurityElement.Escape(text)!;

    [GeneratedRegex("/sync/(?<calendar>[^/]+)/(?<version>[0-9]+)(\\+(?<offset>[0-9]+))?$")]
    private static partial Regex TokenPattern();

    [GeneratedRegex("<d:href>")]
    private static partial Regex HrefCount();

    private sealed class Calendar(string name, string display)
    {
        public string Name { get; } = name;

        public string Display { get; } = display;

        public Dictionary<string, (string Etag, string Text)> Objects { get; } = new(StringComparer.Ordinal);

        public List<(int Version, string Name)> Log { get; } = [];

        public int Version { get; set; }
    }
}
