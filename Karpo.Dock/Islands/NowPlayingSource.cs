// Where what is playing comes from, as Windows 11's media flyout shows it: a small icon and a name
// over the track (backlog 7). The playing app's icon — a packaged app's logo, a desktop app's exe
// icon — found from the session's SourceAppUserModelId; and for a browser, where the session says
// which site it plays on, that site's favicon and its name instead.
//
// **What Windows gives for a browser's session.** Nothing but the app's id ("Chrome", "MSEdge",
// "Brave") and the media properties: no address, no tab, no site field. Chromium fills those from
// the page's own MediaSession metadata, and where the page names no artist it puts the page's
// origin there instead ("www.youtube.com", no scheme) — which is the site name Windows' own flyout
// shows under the title. So that is what is used: a browser session's artist that is a host is the
// site. A page that names its artist (YouTube names the channel) says nothing about its site, and
// the source is the browser's icon and name — the fallback, as it is for every other app. Reading
// the address out of the browser's window (UI Automation) would see only the front tab and turns
// the browser's accessibility tree on, a cost the person's browser would pay all day; it is not
// done.
//
// **What counts as a host.** The artist is the page's to write, so it is read strictly: written as
// Chromium writes an origin's host (lowercase, labels and dots, an optional port, nothing after
// it), and ending in a suffix the web really uses (Suffixes, a short list of the common ones).
// "Daft.Punk", "Dr.Dre" and "Vol.II" are artists; so is anything with a capital, or a space.
//
// **The favicon** comes from the favicon fetcher pins use (FaviconStore), under its limits (a
// short timeout, a size cap, no cookies), asking the site itself and nobody else, over https only,
// and only for the well-known media sites in Known: a page cannot make Karpo contact a host by
// naming it as its artist. Every request is checked against those hosts (MayFetch) — the page, each
// icon it links to, each redirect on the way, which Karpo follows itself — so neither a page's icon
// link nor a redirect can send it anywhere else; an address off the list is not asked, as if it
// were not there, and /favicon.ico is tried as usual. Another subdomain of a listed site
// ("artist.bandcamp.com") shows the listed site's favicon and is never contacted itself (IconHost).
// Any other site is named, with the browser's own icon beside it.
// Reading the browser's own favicon cache was weighed and not done: it is a database in the
// person's browser profile, locked while the browser runs, and Karpo has no business in it. The
// media store keeps nothing on disk: which sites the person plays media on is not written
// anywhere. It is made by the dock on its own thread (Start), so a test run fetches nothing, and a
// favicon that arrives raises MediaSessions.Changed, which every now-playing face reads again on.
//
// Everything here that looks anything up runs on the thread pool, where the faces read sessions.

using System.Diagnostics;
using System.Text.RegularExpressions;
using Karpo.Dock.Favicons;
using Karpo.Dock.QuickSettings.Services;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

/// <summary>
/// Where a track plays: the name the source line says, its icon (null where none was found yet, and
/// the line then goes without one), and the site, for a browser session that names one.
/// </summary>
/// <param name="Name">The site's name for a browser session that names one, else the app's.</param>
/// <param name="App">The app's own name, whatever the line says.</param>
internal sealed record MediaSource(string Name, Pixels? Icon, string App, string? Site = null)
{
    /// <summary>The line Windows' flyout puts over the track: the site and the browser, or the app.</summary>
    public string Line => Site is not null && App.Length > 0 && App != Name ? $"{Name} · {App}" : Name;
}

internal static partial class NowPlayingSource
{
    /// <summary>The icon's size, decoded once: a 16 DIP mark at up to 200 %.</summary>
    internal const int IconPx = 32;

    /// <summary>How long an app whose icon was not found is left before it is looked for again.</summary>
    private static readonly TimeSpan Retry = TimeSpan.FromSeconds(30);

    /// <summary>Apps kept; past this the oldest goes.</summary>
    private const int Most = 16;

    private static readonly Lock Gate = new();
    private static readonly Dictionary<string, (Pixels? Icon, DateTime Found)> Icons = new(StringComparer.OrdinalIgnoreCase);
    private static readonly LinkedList<string> Order = [];

    /// <summary>
    /// The browsers whose sessions may name their site, by the stem of the id Windows reports their
    /// sessions under (an AUMID or an exe's name, case aside). Firefox's release AUMID is a hash of
    /// its install path, the same on every PC that installs it in the usual place.
    /// </summary>
    private static readonly HashSet<string> Browsers = new(StringComparer.OrdinalIgnoreCase)
    {
        "chrome", "msedge", "brave", "firefox", "opera", "vivaldi", "chromium", "arc", "thorium",
        "yandex", "browser", "waterfox", "librewolf", "zen", "floorp", "308046B0AF4A39CB",
    };

    // A host as Chromium writes a page's origin for display: lowercase labels and dots, a port
    // maybe, a scheme maybe, and nothing after it — no spaces, no path. Whether its last label is a
    // real suffix is Suffixes' to say.
    [GeneratedRegex("""^(?:https?://)?((?:[\p{Ll}\p{Lo}\p{N}](?:[\p{Ll}\p{Lo}\p{N}-]{0,61}[\p{Ll}\p{Lo}\p{N}])?\.)+(\p{Ll}{2,63}))(:\d{1,5})?/?$""")]
    private static partial Regex HostShape { get; }

    /// <summary>
    /// The suffixes a site's host may end in: the generic ones in common use, and the countries'
    /// that sites people play media on use. Short on purpose — a host it misses is named as the
    /// artist, which is what the session says; a name it took for a host would be named as a site.
    /// Some an artist's name ends in ("will.i.am") are left out where no media site needs them.
    /// </summary>
    private static readonly HashSet<string> Suffixes = new(StringComparer.Ordinal)
    {
        "com", "net", "org", "edu", "gov", "info", "biz", "io", "co", "tv", "fm", "me", "app", "dev",
        "ai", "gg", "ly", "to", "cc", "xyz", "live", "music", "video", "stream", "radio", "media",
        "online", "site", "art", "show", "audio", "watch", "garden", "eu", "asia",
        "uk", "de", "fr", "nl", "be", "lu", "ch", "at", "it", "es", "pt", "ie", "dk", "se", "no",
        "fi", "is", "pl", "cz", "sk", "hu", "ro", "bg", "gr", "tr", "ru", "ua", "il", "za", "in",
        "jp", "kr", "cn", "tw", "hk", "sg", "au", "nz", "ca", "us", "mx", "br", "ar", "cl",
    };

    /// <summary>
    /// The sites whose favicon is fetched: well-known places media plays, each as the host it is
    /// fetched from, with its "www." and "m." forms (<see cref="Hosts"/>) — and the few of their
    /// subdomains a site plays on with an icon of its own ("music.youtube.com", "open.spotify.com"),
    /// named here one by one. No other host is ever contacted: not a page's other subdomains, not
    /// a host a page links its icon to, not a host a redirect points at.
    /// </summary>
    private static readonly string[] Known =
    [
        "youtube.com", "music.youtube.com", "youtu.be", "soundcloud.com", "spotify.com", "open.spotify.com", "twitch.tv", "kick.com",
        "netflix.com", "vimeo.com", "dailymotion.com", "deezer.com", "tidal.com", "qobuz.com",
        "bandcamp.com", "mixcloud.com", "music.apple.com", "tv.apple.com", "podcasts.apple.com",
        "music.amazon.com", "primevideo.com", "disneyplus.com", "max.com", "hulu.com",
        "peacocktv.com", "paramountplus.com", "crunchyroll.com", "plex.tv", "pandora.com",
        "iheart.com", "tunein.com", "radio.garden", "last.fm", "audible.com", "napster.com",
        "nebula.tv", "rumble.com", "bilibili.com", "nicovideo.jp", "bbc.co.uk", "ted.com",
    ];

    /// <summary>Every host a media favicon may be asked of: each of <see cref="Known"/>, and its "www." and "m." forms.</summary>
    private static readonly HashSet<string> Hosts = new(
        Known.SelectMany(site => new[] { site, "www." + site, "m." + site }), StringComparer.Ordinal);

    /// <summary>The store the media favicons come from; null where no dock started one (a test).</summary>
    private static FaviconStore? sites;

    /// <summary>
    /// Whether <paramref name="app"/> — a session's <c>SourceAppUserModelId</c> — is a browser's: its
    /// AUMID's app part or its exe's stem is one of <see cref="Browsers"/>. An installed web app has
    /// an id of its own ("Chrome._crx_…") and is an app, with its own icon.
    /// </summary>
    public static bool IsBrowser(string? app)
    {
        if (string.IsNullOrWhiteSpace(app))
        {
            return false;
        }
        var id = app.Trim();
        var bang = id.IndexOf('!');
        if (bang >= 0)
        {
            // A packaged browser ("TheBrowserCompany.Arc_…!Arc"): the app's part names it.
            id = id[(bang + 1)..];
        }
        var stem = Path.GetFileName(id);
        if (stem.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            stem = stem[..^4];
        }
        return Browsers.Contains(stem);
    }

    /// <summary>
    /// The site a session plays on, as a host ("www.youtube.com"), or null: only a browser's, and
    /// only where its artist is a host as Chromium writes one, ending in a real suffix — what
    /// Chromium puts there for a page that names no artist. An artist that is anything else is the
    /// artist.
    /// </summary>
    public static string? SiteOf(string? app, string? artist)
    {
        if (!IsBrowser(app) || string.IsNullOrWhiteSpace(artist))
        {
            return null;
        }
        var match = HostShape.Match(artist.Trim());
        if (!match.Success || !Suffixes.Contains(match.Groups[2].Value))
        {
            return null;
        }
        return match.Groups[1].Value + match.Groups[3].Value;
    }

    /// <summary>A host as every face says it: without the "www." nobody reads.</summary>
    public static string Say(string host) =>
        host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) && host.Length > 4 ? host[4..] : host;

    /// <summary>
    /// The host <paramref name="site"/>'s favicon is fetched from: the site itself where it is one
    /// of <see cref="Hosts"/>; the listed site it is under where it is another subdomain of one
    /// ("artist.bandcamp.com" shows bandcamp.com's), so the subdomain itself is never contacted;
    /// null for any other site, and for one on a port of its own.
    /// </summary>
    public static string? IconHost(string site)
    {
        if (site.Contains(':'))
        {
            return null;
        }
        if (Hosts.Contains(site))
        {
            return site;
        }
        // The longest listed site it is under: "x.music.youtube.com" is music's, not youtube.com's.
        return Known
            .Where(known => site.EndsWith("." + known, StringComparison.Ordinal))
            .OrderByDescending(known => known.Length)
            .FirstOrDefault();
    }

    /// <summary>Whether <paramref name="site"/> shows a fetched favicon at all (<see cref="IconHost"/>).</summary>
    public static bool Fetches(string site) => IconHost(site) is not null;

    /// <summary>
    /// Whether <paramref name="address"/> may be asked for a media favicon: https on its own port, no
    /// user part, and its host one of <see cref="Hosts"/> exactly. Every request is checked — the
    /// page, each icon it links, each redirect on the way (FaviconSource.Within).
    /// </summary>
    public static bool MayFetch(Uri address) =>
        address.IsAbsoluteUri
        && address.Scheme == Uri.UriSchemeHttps
        && address.IsDefaultPort
        && address.UserInfo.Length == 0
        && Hosts.Contains(address.IdnHost);

    /// <summary>
    /// What a face says in the artist's place: the site as <see cref="Say"/> says it where the
    /// artist is a browser's site, else the artist. Every face says it so, so none shows "www." where
    /// another does not.
    /// </summary>
    public static string Artist(string artist, MediaSource? source) =>
        source is { Site: not null } ? source.Name : artist;

    /// <summary>
    /// Where <paramref name="read"/> plays, looked up on the thread pool: the site for a browser
    /// session that names one, with its favicon where that is fetched and has arrived, else the
    /// browser's icon; and the app and its icon for anything else.
    /// </summary>
    public static MediaSource Of(SessionRead read)
    {
        var app = MediaNames.Of(read.App);
        if (SiteOf(read.App, read.Artist) is not { } site)
        {
            return new MediaSource(app, AppIcon(read.App), app);
        }
        if (IconHost(site) is { } host && Volatile.Read(ref sites) is { } store)
        {
            var origin = $"https://{host}";
            if (store.Picture(origin + "/", IconPx) is { } favicon)
            {
                return new MediaSource(Say(site), favicon, app, site);
            }
            if (store.Working(origin) is { IsCompleted: false })
            {
                // On its way: no icon for the moment, rather than the browser's swapped for it.
                return new MediaSource(Say(site), null, app, site);
            }
        }
        return new MediaSource(Say(site), AppIcon(read.App), app, site);
    }

    /// <summary>
    /// The dock's: made on its own thread, which is where a fetched favicon's news is raised, and
    /// told to <paramref name="arrived"/>. Not in a test run, which must ask no site for anything.
    /// </summary>
    public static void Start(Action arrived)
    {
        if (Karpo.Core.Config.IsIsolated || Volatile.Read(ref sites) is not null)
        {
            return;
        }
        var store = new FaviconStore(FaviconSource.Within(MayFetch), FaviconDisk.None);
        store.Changed += arrived;
        Volatile.Write(ref sites, store);
    }

    /// <summary>
    /// The app's icon at <see cref="IconPx"/>, from the shell: the Start entry of an AUMID (a packaged
    /// app's logo), else the exe's own — a path as given, or a bare exe's name found among the
    /// running processes. Kept once found; not found, looked for again after <see cref="Retry"/>.
    /// </summary>
    public static Pixels? AppIcon(string app)
    {
        if (string.IsNullOrWhiteSpace(app))
        {
            return null;
        }
        app = app.Trim();
        lock (Gate)
        {
            if (Icons.TryGetValue(app, out var kept) && (kept.Icon is not null || Clock.RealUtc() - kept.Found < Retry))
            {
                return kept.Icon;
            }
        }
        Pixels? icon = null;
        try
        {
            icon = Look(app);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Console.Error.WriteLine($"Karpo: a media app's icon was not read: {error.GetType().Name}");
        }
        lock (Gate)
        {
            Icons[app] = (icon, Clock.RealUtc());
            Order.Remove(app);
            Order.AddLast(app);
            while (Order.Count > Most && Order.First is { } oldest)
            {
                Icons.Remove(oldest.Value);
                Order.RemoveFirst();
            }
        }
        return icon;
    }

    private static Pixels? Look(string app)
    {
        var exe = Path.IsPathRooted(app) ? app : Running(app);
        // An AUMID first: the Start entry of a packaged app, or of a desktop app that names itself
        // ("Chrome", "Brave"), is the logo Windows' flyout shows.
        var item = Item.Running(Path.IsPathRooted(app) ? null : app, exe ?? app);
        return item.Icon(IconPx);
    }

    /// <summary>
    /// The path of a running process named as <paramref name="app"/> says ("Spotify.exe", or a bare
    /// "Brave"), or null where none runs, or none can be asked.
    /// </summary>
    private static string? Running(string app)
    {
        if (app.Contains('!'))
        {
            return null;
        }
        var stem = Path.GetFileNameWithoutExtension(app);
        if (stem.Length == 0)
        {
            return null;
        }
        Process[] found = [];
        try
        {
            found = Process.GetProcessesByName(stem);
            foreach (var process in found)
            {
                if (PathOf((uint)process.Id) is { Length: > 0 } path)
                {
                    return path;
                }
            }
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            // Processes could not be listed: no path, and the AUMID alone is tried.
        }
        finally
        {
            foreach (var process in found)
            {
                process.Dispose();
            }
        }
        return null;
    }

    /// <summary>A process's exe, asked with the least access there is, so an elevated one answers too.</summary>
    private static unsafe string? PathOf(uint pid)
    {
        var process = OpenProcess((uint)PROCESS.PROCESS_QUERY_LIMITED_INFORMATION, BOOL.FALSE, pid);
        if (process == HANDLE.NULL)
        {
            return null;
        }
        try
        {
            var buffer = stackalloc char[1024];
            uint size = 1024;
            return QueryFullProcessImageNameW(process, 0, buffer, &size) ? new string(buffer, 0, (int)size) : null;
        }
        finally
        {
            _ = CloseHandle(process);
        }
    }
}
