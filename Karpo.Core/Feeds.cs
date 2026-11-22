// rss: a news feed, as the RSS widget reads it (docs\widgets.md §39) — one address per widget, RSS
// 2.0 (and the older RDF form of RSS 1.0) or Atom, read into its title, the site it belongs to and
// its newest items.
//
// It is here, in Karpo.Core, because both apps read feeds: the dock for the widget, and the
// settings app to prove an address is a feed before it is saved, as the stock's symbol is proved
// against the price service. One reader, so the two cannot disagree about what a feed is.
//
// **What a feed may do to Karpo: nothing.** It is somebody else's XML, so it is read as data and
// never as instructions:
//
//   * System.Xml with the DTD ignored and no resolver: no entity of the feed's own is expanded, and
//     nothing the document names is fetched. A document that needs one is not read at all.
//   * Every text is plain text in the end: the tags of whatever HTML a title carries are taken out,
//     its entities decoded, its white space folded, and it is cut to a length a widget can show.
//   * Every link is kept only where it is an absolute http or https address (Opens): a feed that
//     names a file, a script or another program has that link dropped, and a click on its item
//     does nothing. Nothing a feed names is ever run.
//   * At most MostItems items, and a document of at most MostBytes.
//
// No serializer is involved: the reader walks the elements it knows by name, which is what keeps it
// as safe under trimming and NativeAOT as the rest of the dock (no reflection, no XmlSerializer).

using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Karpo.Core;

/// <summary>
/// rss: one RSS widget's own settings: the feed it follows, and whether its taskbar entry says the
/// newest headline beside its glyph. Nothing is fetched until an address is set, so a fresh install
/// makes no request of anybody.
/// </summary>
public sealed class RssOptions
{
    /// <summary>The feed's address as it was proved (http or https); empty = not set.</summary>
    public string Address { get; set; } = "";

    /// <summary>
    /// What the feed called itself when its address was proved — "BBC News" — so the settings card
    /// can say which feed this is without asking the network every time the page opens. Never a
    /// headline: a headline kept in a settings file is old news waiting to be believed.
    /// </summary>
    public string Title { get; set; } = "";

    /// <summary>The longest a feed's title is kept in the file.</summary>
    public const int MostTitle = 120;

    /// <summary>
    /// A Segoe Fluent Icons glyph chosen to draw the feed with in place of its site's own icon (§306);
    /// null for none, and left out of the file then.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Glyph { get; set; }

    public bool IsSet => Feeds.Opens(Address);
}

/// <summary>One item of a feed, newest first in <see cref="Feed.Items"/>.</summary>
public sealed record FeedItem
{
    /// <summary>The headline, plain text, never empty.</summary>
    public required string Title { get; init; }

    /// <summary>Where it is read: an http or https address, or empty where the feed gave none Karpo opens.</summary>
    public string Link { get; init; } = "";

    /// <summary>When it was published, or last updated where that is all the feed says; null where it says neither.</summary>
    public DateTimeOffset? Published { get; init; }

    /// <summary>A picture for it, http or https: media:thumbnail, an image in media:content, an image enclosure; else empty.</summary>
    public string Thumbnail { get; init; } = "";

    /// <summary>What tells it from the others across two reads: its guid or id, else its link, else its title.</summary>
    public string Key { get; init; } = "";
}

/// <summary>A feed as it was read: its title, its site and its newest items.</summary>
public sealed record Feed
{
    /// <summary>What it calls itself, plain text; the host of its site where it says nothing.</summary>
    public required string Title { get; init; }

    /// <summary>The site it belongs to, http or https, for the panel's footer; empty where it names none.</summary>
    public string Site { get; init; } = "";

    /// <summary>
    /// A square icon the feed names for itself, http or https (§306): Atom's icon, or an RSS image
    /// that says it is as wide as it is tall; empty where it names none. Its site's own favicon is
    /// tried first, this after it.
    /// </summary>
    public string Icon { get; init; } = "";

    /// <summary>Its items, newest first, at most <see cref="Feeds.MostItems"/>.</summary>
    public IReadOnlyList<FeedItem> Items { get; init; } = [];
}

/// <summary>Why a document is not a feed Karpo can read.</summary>
public enum FeedProblem
{
    /// <summary>It is not XML at all, or XML that breaks off: a web page, an error, half a file.</summary>
    NotXml,

    /// <summary>Well-formed XML that is neither RSS nor Atom.</summary>
    NotAFeed,

    /// <summary>Longer than <see cref="Feeds.MostBytes"/>.</summary>
    TooLong,
}

/// <summary>A document that is not a feed Karpo can read, and why (<see cref="FeedProblem"/>).</summary>
public sealed class FeedException(FeedProblem problem, Exception? inner = null)
    : Exception($"not a feed Karpo reads: {problem}", inner)
{
    public FeedProblem Problem { get; } = problem;
}

/// <summary>Reading a feed, and the few rules every place that shows one shares.</summary>
public static partial class Feeds
{
    /// <summary>The most items a feed is kept with: more than any widget or panel shows.</summary>
    public const int MostItems = 30;

    /// <summary>The longest a headline is kept, in characters; a longer one ends in "…".</summary>
    public const int MostTitle = 300;

    /// <summary>The longest a feed's document may be.</summary>
    public const int MostBytes = 4 << 20;

    /// <summary>The longest an address is kept.</summary>
    public const int MostAddress = 2048;

    private const string Media = "http://search.yahoo.com/mrss/";
    private const string Dc = "http://purl.org/dc/elements/1.1/";
    private const string Atom = "http://www.w3.org/2005/Atom";
    private const string Rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
    private const string Rss1 = "http://purl.org/rss/1.0/";

    /// <summary>
    /// How a feed is read: no DTD processed, nothing resolved, no entity of the document's own
    /// expanded, and no more characters than a feed of <see cref="MostBytes"/> can hold.
    /// </summary>
    private static XmlReaderSettings Safe() => new()
    {
        DtdProcessing = DtdProcessing.Ignore,
        XmlResolver = null,
        MaxCharactersFromEntities = 1024,
        MaxCharactersInDocument = MostBytes,
        IgnoreComments = true,
        IgnoreProcessingInstructions = true,
        CloseInput = false,
    };

    /// <summary>
    /// The feed in <paramref name="bytes"/>, which came from <paramref name="from"/> (the address
    /// relative links are read against). Its encoding is the document's own, as its declaration
    /// says. Throws <see cref="FeedException"/> for anything that is not a feed.
    /// </summary>
    public static Feed Parse(byte[] bytes, Uri? from = null)
    {
        if (bytes.Length > MostBytes)
        {
            throw new FeedException(FeedProblem.TooLong);
        }
        using var stream = new MemoryStream(bytes, writable: false);
        return Parse(stream, from);
    }

    /// <summary>The feed in <paramref name="xml"/>, as <see cref="Parse(byte[], Uri?)"/> reads it.</summary>
    public static Feed Parse(string xml, Uri? from = null)
    {
        if (xml.Length > MostBytes)
        {
            throw new FeedException(FeedProblem.TooLong);
        }
        using var text = new StringReader(xml);
        using var reader = XmlReader.Create(text, Safe());
        return Read(Load(reader), from);
    }

    private static Feed Parse(Stream stream, Uri? from)
    {
        using var reader = XmlReader.Create(stream, Safe());
        return Read(Load(reader), from);
    }

    private static XDocument Load(XmlReader reader)
    {
        try
        {
            return XDocument.Load(reader, LoadOptions.None);
        }
        catch (Exception error) when (error is XmlException or InvalidOperationException or ArgumentException or DecoderFallbackException)
        {
            throw new FeedException(FeedProblem.NotXml, error);
        }
    }

    /// <summary>RSS 2.0 (and 0.9x), RSS 1.0's RDF, or Atom, by the document's root.</summary>
    private static Feed Read(XDocument document, Uri? from)
    {
        var root = document.Root ?? throw new FeedException(FeedProblem.NotAFeed);
        return root.Name.LocalName switch
        {
            "rss" when Child(root, "channel") is { } channel => Channel(channel, channel.Elements().Where(element => element.Name.LocalName == "item"), from),
            "RDF" when root.Name.NamespaceName == Rdf && Child(root, "channel") is { } channel =>
                Channel(channel, root.Elements().Where(element => element.Name.LocalName == "item"), from),
            "feed" when root.Name.NamespaceName is Atom or "" => AtomFeed(root, from),
            _ => throw new FeedException(FeedProblem.NotAFeed),
        };
    }

    private static Feed Channel(XElement channel, IEnumerable<XElement> entries, Uri? from)
    {
        var site = Link(Text(Child(channel, "link")), from);
        var at = site.Length > 0 ? new Uri(site) : from;
        var items = new List<FeedItem>();
        foreach (var entry in entries)
        {
            var title = Plain(Text(Child(entry, "title")), MostTitle);
            var description = Text(Child(entry, "description"));
            if (title.Length == 0)
            {
                // An item with no title is a post of a few words: its words are its headline.
                title = Plain(description, MostTitle);
            }
            if (title.Length == 0)
            {
                continue;
            }
            var link = Link(Text(Child(entry, "link")), at);
            var guid = Child(entry, "guid");
            if (link.Length == 0 && guid is not null && !string.Equals((string?)guid.Attribute("isPermaLink"), "false", StringComparison.OrdinalIgnoreCase))
            {
                link = Link(Text(guid), at);
            }
            var published = Date(Text(Child(entry, "pubDate"))) ?? Date(Text(entry.Element(XName.Get("date", Dc))));
            var thumbnail = Thumbnail(entry, at);
            if (thumbnail.Length == 0)
            {
                thumbnail = Enclosures(entry.Elements().Where(element => element.Name.LocalName == "enclosure"), "url", at);
            }
            if (thumbnail.Length == 0)
            {
                thumbnail = Pictured(description, at);
            }
            var key = Text(guid).Trim() is { Length: > 0 } id ? id : link.Length > 0 ? link : title;
            items.Add(new FeedItem { Title = title, Link = link, Published = published, Thumbnail = thumbnail, Key = key });
            if (items.Count >= MostItems * 2)
            {
                break;
            }
        }
        var name = Plain(Text(Child(channel, "title")), RssOptions.MostTitle);
        return new Feed { Title = name.Length > 0 ? name : HostOf(site.Length > 0 ? site : from?.ToString()), Site = site, Icon = Image(Child(channel, "image"), at), Items = Newest(items) };
    }

    /// <summary>
    /// An RSS channel's image where it says it is square, its width its height: one that says no size
    /// is 88 by 31 by the spec, a banner and no icon.
    /// </summary>
    private static string Image(XElement? image, Uri? at) =>
        image is not null && Text(Child(image, "width")).Trim() is { Length: > 0 } width && width == Text(Child(image, "height")).Trim()
            ? Link(Text(Child(image, "url")), at)
            : "";

    private static Feed AtomFeed(XElement feed, Uri? from)
    {
        var at = Base(feed, from);
        var site = Alternate(feed, at);
        var items = new List<FeedItem>();
        foreach (var entry in feed.Elements().Where(element => element.Name.LocalName == "entry"))
        {
            var here = Base(entry, at);
            var summary = Child(entry, "summary") ?? Child(entry, "content");
            var title = Plain(AtomText(Child(entry, "title")), MostTitle);
            if (title.Length == 0)
            {
                title = Plain(AtomText(summary), MostTitle);
            }
            if (title.Length == 0)
            {
                continue;
            }
            var link = Alternate(entry, here);
            var published = Date(Text(Child(entry, "published"))) ?? Date(Text(Child(entry, "updated")));
            var thumbnail = Thumbnail(entry, here);
            if (thumbnail.Length == 0)
            {
                thumbnail = Enclosures(entry.Elements().Where(element => element.Name.LocalName == "link"
                    && string.Equals((string?)element.Attribute("rel"), "enclosure", StringComparison.OrdinalIgnoreCase)), "href", here);
            }
            if (thumbnail.Length == 0)
            {
                thumbnail = Pictured(AtomText(summary), here);
            }
            var id = Text(Child(entry, "id")).Trim();
            items.Add(new FeedItem { Title = title, Link = link, Published = published, Thumbnail = thumbnail, Key = id.Length > 0 ? id : link.Length > 0 ? link : title });
            if (items.Count >= MostItems * 2)
            {
                break;
            }
        }
        var name = Plain(AtomText(Child(feed, "title")), RssOptions.MostTitle);
        // Atom's icon is square by its spec (RFC 4287, 4.2.5); its logo is a banner, and no icon.
        var icon = Link(Text(Child(feed, "icon")), at);
        return new Feed { Title = name.Length > 0 ? name : HostOf(site.Length > 0 ? site : from?.ToString()), Site = site, Icon = icon, Items = Newest(items) };
    }

    /// <summary>
    /// The items newest first, at most <see cref="MostItems"/>. A feed is usually in that order
    /// already; where every item says when it was published they are put in it, and where some do
    /// not, the feed's own order is the only one there is.
    /// </summary>
    private static List<FeedItem> Newest(List<FeedItem> items)
    {
        IEnumerable<FeedItem> ordered = items.Count > 0 && items.TrueForAll(item => item.Published is not null)
            ? items.OrderByDescending(item => item.Published!.Value)
            : items;
        return [.. ordered.Take(MostItems)];
    }

    /// <summary>The first child named <paramref name="name"/> in any namespace but media's: RSS's own elements have none, or RSS 1.0's.</summary>
    private static XElement? Child(XElement parent, string name) =>
        parent.Elements().FirstOrDefault(element => element.Name.LocalName == name && element.Name.NamespaceName is "" or Atom or Rss1);

    private static string Text(XElement? element) => element?.Value ?? "";

    /// <summary>An Atom text construct: its words, whether it says it is text, html or xhtml.</summary>
    private static string AtomText(XElement? element)
    {
        if (element is null)
        {
            return "";
        }
        // xhtml is markup in the document itself: its text is the words of every element in it.
        return string.Equals((string?)element.Attribute("type"), "xhtml", StringComparison.OrdinalIgnoreCase)
            ? string.Concat(element.DescendantNodes().OfType<XText>().Select(text => text.Value))
            : element.Value;
    }

    /// <summary>Where Atom reads a relative link against: the element's xml:base, else its parent's.</summary>
    private static Uri? Base(XElement element, Uri? parent)
    {
        var written = (string?)element.Attribute(XNamespace.Xml + "base");
        if (string.IsNullOrWhiteSpace(written))
        {
            return parent;
        }
        return parent is not null && Uri.TryCreate(parent, written.Trim(), out var joined) ? joined
            : Uri.TryCreate(written.Trim(), UriKind.Absolute, out var whole) ? whole
            : parent;
    }

    /// <summary>An Atom element's link to the page it stands for: rel="alternate", or a link with no rel.</summary>
    private static string Alternate(XElement element, Uri? at)
    {
        foreach (var link in element.Elements().Where(child => child.Name.LocalName == "link" && child.Name.NamespaceName is Atom or ""))
        {
            var rel = ((string?)link.Attribute("rel"))?.Trim() ?? "";
            if (rel.Length == 0 || rel.Equals("alternate", StringComparison.OrdinalIgnoreCase))
            {
                var href = Link((string?)link.Attribute("href"), at);
                if (href.Length > 0)
                {
                    return href;
                }
            }
        }
        return "";
    }

    /// <summary>
    /// A picture Media RSS names for <paramref name="entry"/>: its media:thumbnail, an image of its
    /// media:content (or a thumbnail inside it), or one of a media:group's.
    /// </summary>
    private static string Thumbnail(XElement entry, Uri? at)
    {
        foreach (var element in entry.Descendants())
        {
            if (element.Name.NamespaceName != Media)
            {
                continue;
            }
            switch (element.Name.LocalName)
            {
                case "thumbnail":
                    if (Link((string?)element.Attribute("url"), at) is { Length: > 0 } thumbnail)
                    {
                        return thumbnail;
                    }
                    break;
                case "content" when IsImage((string?)element.Attribute("medium"), (string?)element.Attribute("type"), (string?)element.Attribute("url")):
                    if (Link((string?)element.Attribute("url"), at) is { Length: > 0 } content)
                    {
                        return content;
                    }
                    break;
            }
        }
        return "";
    }

    /// <summary>The first of <paramref name="enclosures"/> that is an image, its address in <paramref name="attribute"/>.</summary>
    private static string Enclosures(IEnumerable<XElement> enclosures, string attribute, Uri? at)
    {
        foreach (var enclosure in enclosures)
        {
            var address = (string?)enclosure.Attribute(attribute);
            if (IsImage(null, (string?)enclosure.Attribute("type"), address) && Link(address, at) is { Length: > 0 } picture)
            {
                return picture;
            }
        }
        return "";
    }

    private static bool IsImage(string? medium, string? type, string? address)
    {
        if (string.Equals(medium, "image", StringComparison.OrdinalIgnoreCase)
            || (type?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ?? false))
        {
            return true;
        }
        if (medium is { Length: > 0 } || type is { Length: > 0 })
        {
            return false;
        }
        var path = Uri.TryCreate(address?.Trim() ?? "", UriKind.Absolute, out var url) ? url.AbsolutePath : address ?? "";
        return path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".gif", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".webp", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The first picture an item's HTML shows (<c>&lt;img src&gt;</c>), where nothing else names one.</summary>
    private static string Pictured(string html, Uri? at)
    {
        if (html.Length == 0 || ImageTag.Match(html) is not { Success: true } found)
        {
            return "";
        }
        return Link(WebUtility.HtmlDecode(found.Groups[1].Value), at);
    }

    [GeneratedRegex("""<img\b[^>]*?\bsrc\s*=\s*["']([^"']+)["']""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ImageTag { get; }

    [GeneratedRegex("""<[^>]*>""", RegexOptions.CultureInvariant)]
    private static partial Regex Tag { get; }

    [GeneratedRegex("""\s+""", RegexOptions.CultureInvariant)]
    private static partial Regex Space { get; }

    /// <summary>
    /// <paramref name="text"/> as plain words: the tags of any HTML in it taken out (a line break and
    /// a paragraph are a space), its entities decoded — twice where a feed encoded them twice — its
    /// white space folded to single spaces, and cut at <paramref name="most"/> characters with "…".
    /// </summary>
    public static string Plain(string? text, int most)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "";
        }
        var plain = text;
        // Two rounds: a title written as escaped HTML ("&lt;b&gt;") is HTML once it is decoded.
        for (var round = 0; round < 2; round++)
        {
            plain = Tag.Replace(plain, " ");
            var decoded = WebUtility.HtmlDecode(plain);
            if (decoded == plain)
            {
                break;
            }
            plain = decoded;
        }
        plain = Tag.Replace(plain, " ");
        // What no headline means to say: control characters, and the zero-width marks some feeds sow.
        var kept = new StringBuilder(plain.Length);
        foreach (var c in plain)
        {
            if (!char.IsControl(c) && c is not ('​' or '‌' or '‍' or '﻿'))
            {
                kept.Append(c);
            }
            else if (c is '\t' or '\n' or '\r')
            {
                kept.Append(' ');
            }
        }
        plain = Space.Replace(kept.ToString(), " ").Trim();
        return Cut(plain, most);
    }

    /// <summary><paramref name="text"/> no longer than <paramref name="most"/> characters, ending in "…" where it was cut, never in half a pair.</summary>
    public static string Cut(string text, int most)
    {
        if (most <= 0)
        {
            return "";
        }
        if (text.Length <= most)
        {
            return text;
        }
        var keep = most - 1;
        if (keep > 0 && char.IsHighSurrogate(text[keep - 1]))
        {
            keep--;
        }
        return text[..keep].TrimEnd() + "…";
    }

    /// <summary>
    /// <paramref name="link"/> as an absolute http or https address, read against
    /// <paramref name="at"/> where it is relative; empty for anything else — a file, a script, a
    /// mail address, another program's scheme.
    /// </summary>
    public static string Link(string? link, Uri? at)
    {
        var text = (link ?? "").Trim();
        if (text.Length == 0 || text.Length > MostAddress)
        {
            return "";
        }
        Uri? url;
        if (!Uri.TryCreate(text, UriKind.Absolute, out url) || url.IsFile || url.Scheme.Length <= 1)
        {
            // Not absolute (a Windows path reads as absolute "file:" and is not one either): relative
            // to the page it is in, where there is one.
            if (at is null || !Uri.TryCreate(at, text, out url))
            {
                return "";
            }
        }
        return Opens(url) ? url.AbsoluteUri : "";
    }

    /// <summary>
    /// Whether <paramref name="link"/> is something Karpo opens for a feed: an absolute http or https
    /// address with a host. Every place that opens a feed's link asks this first, so nothing else a
    /// feed names — a file, a script, a program — is ever opened.
    /// </summary>
    public static bool Opens(string? link) =>
        link is { Length: > 0 and <= MostAddress } && Uri.TryCreate(link.Trim(), UriKind.Absolute, out var url) && Opens(url);

    private static bool Opens(Uri url) =>
        (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps) && url.Host.Length > 0 && !url.IsFile && !url.IsUnc;

    /// <summary>
    /// What a person typed as a feed's address, as one Karpo can ask for: trimmed, and "https://"
    /// put in front of a bare host ("example.com/feed"); null where it is no http or https address.
    /// </summary>
    public static string? Address(string? typed)
    {
        var text = (typed ?? "").Trim();
        if (text.Length == 0 || text.Length > MostAddress)
        {
            return null;
        }
        // A feed: scheme some readers register is the http address behind it.
        if (text.StartsWith("feed:", StringComparison.OrdinalIgnoreCase))
        {
            text = text[5..].TrimStart('/');
            text = text.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? text : "https://" + text;
        }
        if (!text.Contains("://", StringComparison.Ordinal))
        {
            text = "https://" + text;
        }
        return Opens(text) && Uri.TryCreate(text, UriKind.Absolute, out var url) ? url.AbsoluteUri : null;
    }

    /// <summary>A site's host without "www.", for naming a feed that names itself nothing; empty where there is none.</summary>
    public static string HostOf(string? address)
    {
        if (!Uri.TryCreate(address?.Trim() ?? "", UriKind.Absolute, out var url) || url.Host.Length == 0)
        {
            return "";
        }
        var host = url.Host;
        return host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host[4..] : host;
    }

    private static readonly string[] Months = ["jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "oct", "nov", "dec"];

    [GeneratedRegex("""^(?:[A-Za-z]{2,9},?\s*)?(\d{1,2})\s+([A-Za-z]{3,9})\.?\s+(\d{2,4})\s+(\d{1,2}):(\d{2})(?::(\d{2}(?:\.\d+)?))?\s*(.*)$""", RegexOptions.CultureInvariant)]
    private static partial Regex Rfc822 { get; }

    /// <summary>
    /// When a feed says something happened: RSS's RFC 822 ("Tue, 10 Jun 2003 04:00:00 GMT", with a
    /// numeric zone, a named one or none; two-figure years), or Atom's and Dublin Core's ISO 8601
    /// ("2003-12-13T18:30:02Z"). Null where it is neither.
    /// </summary>
    public static DateTimeOffset? Date(string? text)
    {
        var written = (text ?? "").Trim();
        if (written.Length == 0)
        {
            return null;
        }
        if (Rfc822.Match(written) is { Success: true } rfc)
        {
            var month = Array.IndexOf(Months, rfc.Groups[2].Value[..3].ToLowerInvariant()) + 1;
            var year = int.Parse(rfc.Groups[3].Value, CultureInfo.InvariantCulture);
            year = rfc.Groups[3].Value.Length == 2 ? (year < 50 ? 2000 + year : 1900 + year) : year;
            var day = int.Parse(rfc.Groups[1].Value, CultureInfo.InvariantCulture);
            var hour = int.Parse(rfc.Groups[4].Value, CultureInfo.InvariantCulture);
            var minute = int.Parse(rfc.Groups[5].Value, CultureInfo.InvariantCulture);
            var second = rfc.Groups[6].Success ? (int)double.Parse(rfc.Groups[6].Value, CultureInfo.InvariantCulture) : 0;
            if (month > 0 && Zone(rfc.Groups[7].Value) is { } offset
                && year is >= 1 and <= 9999 && day >= 1 && day <= DateTime.DaysInMonth(year, month) && hour < 24 && minute < 60 && second < 61)
            {
                try
                {
                    return new DateTimeOffset(year, month, day, hour, minute, Math.Min(second, 59), offset);
                }
                catch (ArgumentOutOfRangeException)
                {
                    return null;
                }
            }
        }
        return DateTimeOffset.TryParse(written, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces, out var iso)
            ? iso
            : null;
    }

    /// <summary>An RFC 822 zone: "GMT", "Z", "+0200", "-05:00", a US zone's letters; nothing is UTC. Null for what is none of them.</summary>
    private static TimeSpan? Zone(string zone)
    {
        var text = zone.Trim();
        if (text.Length == 0)
        {
            return TimeSpan.Zero;
        }
        if (text[0] is '+' or '-')
        {
            var digits = text[1..].Replace(":", "", StringComparison.Ordinal);
            if (digits.Length is 2 or 4 && digits.All(char.IsAsciiDigit))
            {
                var hours = int.Parse(digits[..2], CultureInfo.InvariantCulture);
                var minutes = digits.Length == 4 ? int.Parse(digits[2..], CultureInfo.InvariantCulture) : 0;
                if (hours > 14 || minutes > 59)
                {
                    return null;
                }
                var span = new TimeSpan(hours, minutes, 0);
                return text[0] == '-' ? -span : span;
            }
            return null;
        }
        return text.ToUpperInvariant() switch
        {
            "GMT" or "UT" or "UTC" or "Z" => TimeSpan.Zero,
            "EST" => TimeSpan.FromHours(-5),
            "EDT" => TimeSpan.FromHours(-4),
            "CST" => TimeSpan.FromHours(-6),
            "CDT" => TimeSpan.FromHours(-5),
            "MST" => TimeSpan.FromHours(-7),
            "MDT" => TimeSpan.FromHours(-6),
            "PST" => TimeSpan.FromHours(-8),
            "PDT" => TimeSpan.FromHours(-7),
            // RFC 822's military letters are known to be wrong in the wild; they read as UTC.
            { Length: 1 } letter when char.IsAsciiLetter(letter[0]) => TimeSpan.Zero,
            _ => null,
        };
    }

    /// <summary>
    /// How old an item is, as a news card says it, short and in the language Karpo speaks: "Now",
    /// "5 m", "2 h", "3 d", "2 w", "1 y". Empty where the feed did not say when. An item from the
    /// future — a clock somewhere is wrong — is now.
    /// </summary>
    public static string Age(DateTimeOffset? published, DateTimeOffset now)
    {
        if (published is not { } at)
        {
            return "";
        }
        var age = now - at;
        if (age < TimeSpan.FromMinutes(1))
        {
            return S.WidgetRssAgeNow;
        }
        if (age < TimeSpan.FromHours(1))
        {
            return S.WidgetRssAgeMinutes((int)age.TotalMinutes);
        }
        if (age < TimeSpan.FromDays(1))
        {
            return S.WidgetRssAgeHours((int)age.TotalHours);
        }
        if (age < TimeSpan.FromDays(7))
        {
            return S.WidgetRssAgeDays((int)age.TotalDays);
        }
        if (age < TimeSpan.FromDays(365))
        {
            return S.WidgetRssAgeWeeks((int)(age.TotalDays / 7));
        }
        return S.WidgetRssAgeYears((int)(age.TotalDays / 365));
    }

    /// <summary>
    /// The line under a headline: the feed's name and the item's age, joined as the language joins
    /// them ("BBC News · 2 h"); either alone where the other is empty.
    /// </summary>
    public static string Byline(string source, string age) =>
        source.Length == 0 ? age : age.Length == 0 ? source : S.WidgetRssByline(source: source, age: age);
}
