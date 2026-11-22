// Which addresses a site's icon might be at, and which of them to try first.
//
// Everything here is pure text: a URL in, addresses out. The fetching, the cache and the decoding
// are FaviconStore's and FaviconImage's, so the part with all the rules in it can be read and
// tested without a network.
//
// Two places hold a site's icon. `/favicon.ico` at the origin is the old convention and is still
// what most sites answer; the page's own <link rel="icon"> is where the good one now lives, often
// a PNG at several sizes, and it is the only place a site can say what sizes it has. So the page is
// read first and its declarations ranked, with /favicon.ico as the last candidate rather than the
// first — it is the one address that needs no page and always exists to try.
//
// **No third-party favicon service.** Asking someone else's endpoint for these would hand that
// company the list of sites the person has pinned, which is exactly the thing a taskbar should not
// leak, and it would buy nothing a direct request does not already give.

using System.Text.RegularExpressions;

namespace Karpo.Dock.Favicons;

/// <summary>One <c>&lt;link rel="icon"&gt;</c> a page declared.</summary>
/// <param name="Address">Absolute, resolved against the page it was declared on.</param>
/// <param name="Rel">The <c>rel</c> as written, lowercased.</param>
/// <param name="Side">The largest square side in its <c>sizes</c>, 0 when it declared none.</param>
/// <param name="Scalable">An SVG, or <c>sizes="any"</c>: any size, and nothing here can decode it.</param>
internal sealed record FaviconLink(string Address, string Rel, int Side, bool Scalable);

internal static partial class FaviconUrls
{
    /// <summary>How much of a page is read looking for its icon links: the head is at the top of it.</summary>
    public const int PageLimit = 512 * 1024;

    /// <summary>And how large a picture may be. A favicon is a few KB; anything like this is not one.</summary>
    public const int IconLimit = 512 * 1024;

    [GeneratedRegex("""<link\b[^>]*>""", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex LinkTag { get; }

    [GeneratedRegex("""(\w[\w-]*)\s*=\s*("([^"]*)"|'([^']*)'|([^\s"'>]+))""", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex Attribute { get; }

    /// <summary>
    /// What one cache entry and one fetch are shared by: the origin, <c>https://example.com</c> —
    /// scheme and host lowercased, a port only when it is not the scheme's own. Two pins to two
    /// pages of one site are one icon, which is both true and one request instead of two.
    /// <para>
    /// Null when the target is not a web address, which is every other kind of pin.
    /// </para>
    /// </summary>
    public static string? OriginOf(string? target)
    {
        if (target is null || !Uri.TryCreate(target.Trim(), UriKind.Absolute, out var url))
        {
            return null;
        }
        if (url.Scheme is not ("http" or "https"))
        {
            return null;
        }
        var host = url.IdnHost.ToLowerInvariant();
        if (host.Length == 0)
        {
            return null;
        }
        var scheme = url.Scheme.ToLowerInvariant();
        var standard = (scheme == "http" && url.Port == 80) || (scheme == "https" && url.Port == 443);
        return standard ? $"{scheme}://{host}" : $"{scheme}://{host}:{url.Port}";
    }

    /// <summary>The page to read for declared icons: the pinned address itself, without its fragment.</summary>
    public static string? PageOf(string? target) =>
        Uri.TryCreate(target?.Trim() ?? "", UriKind.Absolute, out var url) && url.Scheme is "http" or "https"
            ? url.GetLeftPart(UriPartial.Query)
            : null;

    /// <summary>The address every site has to be asked at when its page declares nothing.</summary>
    public static string Fallback(string origin) => origin.TrimEnd('/') + "/favicon.ico";

    /// <summary>
    /// The icon links <paramref name="html"/> declares, resolved against <paramref name="pageUrl"/>.
    /// Only <c>rel</c>s that mean "this is the site's picture": <c>icon</c>, <c>shortcut icon</c> and
    /// <c>apple-touch-icon</c>. <c>mask-icon</c> is left out on purpose — it is a one-colour Safari
    /// silhouette and drawing it would be worse than the letter it replaces.
    /// </summary>
    public static IReadOnlyList<FaviconLink> Links(string html, string pageUrl)
    {
        var found = new List<FaviconLink>();
        if (!Uri.TryCreate(pageUrl, UriKind.Absolute, out var page))
        {
            return found;
        }
        foreach (var tag in LinkTag.EnumerateMatches(html))
        {
            var text = html.AsSpan(tag.Index, tag.Length).ToString();
            var attributes = Read(text);
            if (!attributes.TryGetValue("rel", out var rel) || !attributes.TryGetValue("href", out var href))
            {
                continue;
            }
            rel = rel.Trim().ToLowerInvariant();
            var words = rel.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var wanted = words.Contains("icon")
                || words.Contains("apple-touch-icon")
                || words.Contains("apple-touch-icon-precomposed");
            if (!wanted || words.Contains("mask-icon") || href.Trim().Length == 0)
            {
                continue;
            }
            if (!Uri.TryCreate(page, href.Trim(), out var address) || address.Scheme is not ("http" or "https"))
            {
                continue;
            }
            var sizes = attributes.GetValueOrDefault("sizes", "").Trim().ToLowerInvariant();
            var type = attributes.GetValueOrDefault("type", "").Trim().ToLowerInvariant();
            var scalable = sizes == "any"
                || type.Contains("svg", StringComparison.Ordinal)
                || address.AbsolutePath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase);
            found.Add(new FaviconLink(address.AbsoluteUri, rel, Largest(sizes), scalable));
        }
        return found;
    }

    /// <summary>
    /// The addresses to try, best first, for a picture drawn at <paramref name="wanted"/> px — and
    /// <see cref="Fallback"/> last, always, because a page that declares nothing usually still has
    /// one there. Each address appears once.
    /// <para>
    /// Best means, in order: something this build can decode (an SVG is ranked last, because WIC has
    /// no SVG codec — see <see cref="FaviconImage"/>); then a picture at least as large as the cell
    /// over one that is not, since shrinking keeps detail that growing invents; then the nearest
    /// size within that.
    /// </para>
    /// </summary>
    public static IReadOnlyList<string> Rank(IEnumerable<FaviconLink> links, string origin, int wanted)
    {
        var ordered = links
            .OrderBy(link => link.Scalable ? 1 : 0)
            .ThenBy(link => Group(link, wanted))
            .ThenBy(link => Distance(link, wanted))
            .Select(link => link.Address)
            .ToList();
        ordered.Add(Fallback(origin));
        return [.. ordered.Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>
    /// Which of three a declaration falls in, best first — the same rule
    /// <see cref="FaviconImage.BestFrame"/> uses to pick a frame out of a multi-size .ico, because
    /// they are the same question asked of a list of addresses rather than a list of frames:
    /// <list type="number">
    /// <item>large enough for the cell, which only has to be shrunk;</item>
    /// <item>no size declared — most often a multi-size .ico, so worth a try before;</item>
    /// <item>smaller than the cell, which cannot be drawn at full size without inventing detail.</item>
    /// </list>
    /// </summary>
    private static int Group(FaviconLink link, int wanted) => link.Side switch
    {
        <= 0 => 1,
        var side when side >= wanted => 0,
        _ => 2,
    };

    /// <summary>
    /// How far from the cell, within a group: the least shrinking among those large enough, the
    /// largest among those that are not.
    /// </summary>
    private static int Distance(FaviconLink link, int wanted) => link.Side switch
    {
        <= 0 => 0,
        var side when side >= wanted => side - wanted,
        var side => wanted - side,
    };

    /// <summary>
    /// The largest square in a <c>sizes</c> list ("16x16 32x32"); 0 for none or "any". The
    /// separator is a letter, and HTML does not care which case it is written in.
    /// </summary>
    internal static int Largest(string sizes)
    {
        var best = 0;
        foreach (var word in sizes.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries))
        {
            var cross = word.IndexOf('x', StringComparison.OrdinalIgnoreCase);
            if (cross <= 0 || !int.TryParse(word[..cross], out var side) || side <= 0)
            {
                continue;
            }
            best = Math.Max(best, side);
        }
        return best;
    }

    private static Dictionary<string, string> Read(string tag)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in Attribute.Matches(tag))
        {
            var name = match.Groups[1].Value;
            var value = match.Groups[3].Success ? match.Groups[3].Value
                : match.Groups[4].Success ? match.Groups[4].Value
                : match.Groups[5].Value;
            attributes.TryAdd(name, value);
        }
        return attributes;
    }
}
