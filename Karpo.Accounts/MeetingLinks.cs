// Where an event's online meeting is joined (docs\widgets.md §205): the link the next meeting's Join
// button opens, found in what the account gives — the meeting's own field first (Graph's
// onlineMeeting.joinUrl, a CalDAV event's CONFERENCE or Google's X-GOOGLE-CONFERENCE), else its
// place, else its notes — and the services Karpo knows a meeting link of: Teams, Zoom, Google Meet,
// Webex, GoTo, Whereby, Jitsi, Slack's huddles, Amazon Chime and RingCentral.
//
// A link is known by its host and its path, never by a word near it: "Join Zoom Meeting" over a
// link to a company's wiki is no meeting. A link Outlook's Safe Links (or Google's redirect) wraps
// is unwrapped first, so the button opens the meeting and not a scanner's page. Only http and https
// are ever handed out, and Zoom's own zoommtg: where an invitation gives it; the apps' own schemes
// (msteams:, zoommtg:) are made from a link that maps onto one cleanly (AppUri), for the dock to
// try before the browser.
//
// Read once, as an account's events are read (MicrosoftReader, ICalendar): what is kept, and drawn,
// is the link alone, never the notes it came from.

using System.Text.RegularExpressions;

namespace Karpo.Accounts;

/// <summary>A service whose meeting links Karpo knows (<see cref="MeetingLinks"/>).</summary>
public enum MeetingService
{
    Teams,
    Zoom,
    Meet,
    Webex,
    GoTo,
    Whereby,
    Jitsi,
    Slack,
    Chime,
    RingCentral,
}

/// <summary>An online meeting's link, and whose it is.</summary>
/// <param name="Url">An http, https or zoommtg address, unwrapped from any Safe Links around it.</param>
public sealed record MeetingLink(string Url, MeetingService Service);

public static partial class MeetingLinks
{
    /// <summary>The longest text read for a link: an invitation's notes past it are no longer its join block.</summary>
    private const int MostText = 64 * 1024;

    /// <summary>
    /// The first meeting link in <paramref name="texts"/>, read in their order — the meeting's own
    /// field, its place, its notes — and in each from its start; null where none has one.
    /// </summary>
    public static MeetingLink? Find(params ReadOnlySpan<string?> texts)
    {
        foreach (var text in texts)
        {
            if (In(text) is { } found)
            {
                return found;
            }
        }
        return null;
    }

    /// <summary>The first meeting link in <paramref name="text"/>; null where it has none.</summary>
    public static MeetingLink? In(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }
        if (text.Length > MostText)
        {
            text = text[..MostText];
        }
        foreach (Match match in Candidates().Matches(text))
        {
            if (Of(match.Value) is { } link)
            {
                return link;
            }
        }
        return null;
    }

    /// <summary>
    /// <paramref name="address"/> as a meeting link, where it is one: unwrapped, its ends cleaned of
    /// what a sentence puts round it, and known by its host and path. Null for anything else.
    /// </summary>
    public static MeetingLink? Of(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return null;
        }
        // An HTML body's link has its ampersands written as entities.
        var text = Trimmed(address.Trim().Replace("&amp;", "&", StringComparison.OrdinalIgnoreCase));
        // A bare Meet or Zoom address, as a place often holds one ("meet.google.com/abc-defg-hij").
        if (!text.Contains("://", StringComparison.Ordinal))
        {
            text = "https://" + text;
        }
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri))
        {
            return null;
        }
        uri = Unwrap(uri);
        if (uri.Scheme == "zoommtg")
        {
            return new MeetingLink(uri.OriginalString, MeetingService.Zoom);
        }
        if (uri.Scheme is not ("https" or "http"))
        {
            return null;
        }
        return ServiceOf(uri) is { } service ? new MeetingLink(uri.OriginalString, service) : null;
    }

    /// <summary>
    /// The address a scanner wraps round a link, taken off, as often as it was wrapped: Outlook's Safe
    /// Links (*.safelinks.protection.outlook.com, Teams' atp-safelinks page) and Google's redirect
    /// (google.com/url?q=). Anything else as it is.
    /// </summary>
    public static Uri Unwrap(Uri uri)
    {
        for (var depth = 0; depth < 4; depth++)
        {
            var host = uri.Host.ToLowerInvariant();
            var inner =
                host.EndsWith(".safelinks.protection.outlook.com", StringComparison.Ordinal)
                || host.EndsWith(".safelinks.protection.office365.us", StringComparison.Ordinal)
                || (host == "statics.teams.cdn.office.net" && uri.AbsolutePath.Contains("safelinks", StringComparison.OrdinalIgnoreCase))
                    ? Query(uri, "url")
                : (host is "www.google.com" or "google.com") && uri.AbsolutePath == "/url" ? Query(uri, "q") ?? Query(uri, "url")
                : null;
            if (inner is null || !Uri.TryCreate(inner, UriKind.Absolute, out var unwrapped))
            {
                return uri;
            }
            uri = unwrapped;
        }
        return uri;
    }

    /// <summary>
    /// The app's own address for <paramref name="link"/>, where the link maps onto one cleanly: a
    /// Teams meeting at teams.microsoft.com as msteams:/l/meetup-join/…, a Zoom meeting by its number
    /// as zoommtg://zoom.us/join?confno=…&amp;pwd=…, Zoom's own zoommtg: as it is. Null for every other.
    /// </summary>
    public static string? AppUri(MeetingLink link)
    {
        if (!Uri.TryCreate(link.Url, UriKind.Absolute, out var uri))
        {
            return null;
        }
        switch (link.Service)
        {
            case MeetingService.Teams when uri.Host.Equals("teams.microsoft.com", StringComparison.OrdinalIgnoreCase)
                && uri.AbsolutePath.StartsWith("/l/meetup-join/", StringComparison.OrdinalIgnoreCase):
                // The link's own text after its host, never Uri's reading of it: a join link's context is
                // escaped JSON, and it goes to Teams escaped as it came.
                return "msteams:" + link.Url[(link.Url.IndexOf(uri.Host, StringComparison.OrdinalIgnoreCase) + uri.Host.Length)..];
            case MeetingService.Zoom when uri.Scheme == "zoommtg":
                return link.Url;
            case MeetingService.Zoom when ZoomNumber().Match(uri.AbsolutePath) is { Success: true } number:
            {
                var join = $"zoommtg://{uri.Host}/join?action=join&confno={number.Groups[1].Value}";
                return Query(uri, "pwd") is { Length: > 0 } pwd ? $"{join}&pwd={Uri.EscapeDataString(pwd)}" : join;
            }
            default:
                return null;
        }
    }

    /// <summary>What the service is called, as its own app writes it: the Join button's tooltip names it.</summary>
    public static string Name(MeetingService service) => service switch
    {
        MeetingService.Teams => "Microsoft Teams", // not-ui
        MeetingService.Zoom => "Zoom", // not-ui
        MeetingService.Meet => "Google Meet", // not-ui
        MeetingService.Webex => "Webex", // not-ui
        MeetingService.GoTo => "GoTo Meeting", // not-ui
        MeetingService.Whereby => "Whereby", // not-ui
        MeetingService.Jitsi => "Jitsi Meet", // not-ui
        MeetingService.Slack => "Slack", // not-ui
        MeetingService.Chime => "Amazon Chime", // not-ui
        _ => "RingCentral", // not-ui
    };

    /// <summary>Whose meeting <paramref name="uri"/> is, by its host and path; null where it is no meeting Karpo knows.</summary>
    private static MeetingService? ServiceOf(Uri uri)
    {
        var host = uri.Host.ToLowerInvariant();
        var path = uri.AbsolutePath;
        var all = uri.PathAndQuery + uri.Fragment;
        bool Under(string domain) => host == domain || host.EndsWith("." + domain, StringComparison.Ordinal);
        bool Starts(params ReadOnlySpan<string> prefixes)
        {
            foreach (var prefix in prefixes)
            {
                if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && path.Length > prefix.Length)
                {
                    return true;
                }
            }
            return false;
        }
        // Teams: a meeting's join link, its new short /meet/ link, or the launcher page round either;
        // a chat's or a channel's link is no meeting. Microsoft's government clouds alike.
        if (Under("teams.microsoft.com") || Under("teams.microsoft.us") || Under("teams.live.com") || Under("teams.cloud.microsoft"))
        {
            return all.Contains("meetup-join", StringComparison.OrdinalIgnoreCase) || Starts("/meet/") ? MeetingService.Teams : null;
        }
        // Zoom: zoom.us and every company's vanity host under it (acme.zoom.us, us02web.zoom.us), and
        // ZoomGov; a meeting's /j/, a webinar's /w/, a personal room's /my/ and /s/, the web client's /wc/.
        if (Under("zoom.us") || Under("zoomgov.com") || Under("zoom.com"))
        {
            return Starts("/j/", "/w/", "/my/", "/s/", "/wc/") ? MeetingService.Zoom : null;
        }
        if (host == "meet.google.com")
        {
            return MeetCode().IsMatch(path) || Starts("/lookup/") ? MeetingService.Meet : null;
        }
        // Webex: every site under webex.com — a personal room's /meet/, a meeting's /join/ or j.php, its
        // join service; the site's front page is no meeting.
        if (Under("webex.com"))
        {
            return path.Length > 1 && (Starts("/meet/", "/join/", "/wbxmjs/", "/webappng/") || path.Contains("j.php", StringComparison.OrdinalIgnoreCase)
                || all.Contains("MTID=", StringComparison.OrdinalIgnoreCase)) ? MeetingService.Webex : null;
        }
        if (Under("gotomeeting.com") || host == "meet.goto.com" || host == "gotomeet.me" || host == "app.goto.com")
        {
            return Starts("/join/", "/meeting/") || (host is "meet.goto.com" or "gotomeet.me" && path.Length > 1) ? MeetingService.GoTo : null;
        }
        if (Under("whereby.com"))
        {
            return path.Length > 1 && !Starts("/information/", "/user/") ? MeetingService.Whereby : null;
        }
        if (host is "meet.jit.si" or "8x8.vc")
        {
            return path.Length > 1 ? MeetingService.Jitsi : null;
        }
        // Slack: a huddle's link (app.slack.com/huddle/T…/C…, or a workspace's own host).
        if (Under("slack.com"))
        {
            return Starts("/huddle/") ? MeetingService.Slack : null;
        }
        if (host is "chime.aws" or "app.chime.aws")
        {
            return path.Length > 1 ? MeetingService.Chime : null;
        }
        if (Under("ringcentral.com") && host.StartsWith("meetings", StringComparison.Ordinal))
        {
            return Starts("/j/") ? MeetingService.RingCentral : null;
        }
        return null;
    }

    /// <summary>The value of <paramref name="name"/> in <paramref name="uri"/>'s query, decoded; null where it has none.</summary>
    private static string? Query(Uri uri, string name)
    {
        var query = uri.Query.TrimStart('?');
        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var equals = part.IndexOf('=');
            if (equals > 0 && part.AsSpan(0, equals).Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return Uri.UnescapeDataString(part[(equals + 1)..].Replace('+', ' '));
            }
        }
        return null;
    }

    /// <summary>
    /// What a sentence or a mail's text puts round a link taken off its end: a full stop, a comma, a
    /// closing bracket or quote, Outlook's "&lt;…&gt;" round a link in plain text.
    /// </summary>
    private static string Trimmed(string text)
    {
        text = text.TrimStart('<', '(', '[', '"', '\'');
        while (text.Length > 0 && ".,;:!?)]}>\"'".Contains(text[^1], StringComparison.Ordinal))
        {
            // A closing bracket that closes one opened inside the link stays: a wiki-style path's "(…)".
            if (text[^1] == ')' && text.Count(c => c == '(') >= text.Count(c => c == ')'))
            {
                break;
            }
            text = text[..^1];
        }
        return text;
    }

    /// <summary>
    /// What may be a link in a text: an http, https or zoommtg address up to the first space, quote or
    /// angle bracket, or a bare Meet, Zoom, Teams or Webex address without its scheme.
    /// </summary>
    [GeneratedRegex(@"(?:(?:https?|zoommtg)://|\b(?:meet\.google\.com|(?:[a-z0-9-]+\.)*zoom\.us|teams\.microsoft\.com|[a-z0-9-]+\.webex\.com)/)[^\s<>""'`]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Candidates();

    /// <summary>A Meet code, "abc-defg-hij".</summary>
    [GeneratedRegex(@"^/[a-z]{3}-[a-z]{4}-[a-z]{3}(?:/|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MeetCode();

    /// <summary>A Zoom meeting's or webinar's number in its path: /j/123456789, /w/123456789.</summary>
    [GeneratedRegex(@"^/[jw]/(\d{9,12})(?:/|$)", RegexOptions.CultureInvariant)]
    private static partial Regex ZoomNumber();
}
