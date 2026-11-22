// Where what is playing comes from (backlog 7): which sessions are a browser's, and the site a
// browser's session names — Chromium puts a page's origin in the artist where the page names none.
// Pure text, and a handler of the tests' own for the fetching: no session, no shell, no site is
// asked anything.

using Karpo.Dock;

namespace Karpo.Tests.Islands;

public sealed class NowPlayingSourceTests
{
    [Theory]
    [InlineData("Chrome")]
    [InlineData("MSEdge")]
    [InlineData("Brave")]
    [InlineData("brave.exe")]
    [InlineData(@"C:\Program Files\Mozilla Firefox\firefox.exe")]
    [InlineData("308046B0AF4A39CB")]
    [InlineData("TheBrowserCompany.Arc_ttt1ap7aakyb4!Arc")]
    [InlineData("Opera.exe")]
    public void BrowsersAreKnownByTheirSessionsId(string app) =>
        Assert.True(NowPlayingSource.IsBrowser(app));

    [Theory]
    [InlineData("SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify")]
    [InlineData("Spotify.exe")]
    [InlineData("Chrome._crx_agimnkijcaahngcdmfeangaknmldooml")]
    [InlineData("vlc.exe")]
    [InlineData("")]
    [InlineData(null)]
    public void AppsAndInstalledWebAppsAreNotBrowsers(string? app) =>
        Assert.False(NowPlayingSource.IsBrowser(app));

    [Theory]
    [InlineData("Chrome", "www.youtube.com", "www.youtube.com")]
    [InlineData("MSEdge", "open.spotify.com", "open.spotify.com")]
    [InlineData("Brave", "https://store.steampowered.com/", "store.steampowered.com")]
    [InlineData("Brave", "music.youtube.com", "music.youtube.com")]
    [InlineData("Chrome", "radio.example.co.uk:8443", "radio.example.co.uk:8443")]
    [InlineData("Chrome", "bücher.de", "bücher.de")]
    [InlineData("Chrome", "radio.garden", "radio.garden")]
    public void ABrowsersArtistThatIsAHostIsTheSite(string app, string artist, string site) =>
        Assert.Equal(site, NowPlayingSource.SiteOf(app, artist));

    [Theory]
    // A page that names its artist (YouTube names the channel) says nothing about its site.
    [InlineData("Chrome", "Lofi Girl")]
    [InlineData("Chrome", "Mr. Bungle")]
    [InlineData("Chrome", "will.i.am feat. someone")]
    [InlineData("Chrome", "youtube.com/watch?v=1")]
    [InlineData("Chrome", "localhost")]
    // Regression: artists with a dot in their name were taken for hosts, and their "site" asked
    // for its favicon. A host is written as Chromium writes one — lowercase — and ends in a suffix
    // the web uses.
    [InlineData("Chrome", "Daft.Punk")]
    [InlineData("Chrome", "daft.punk")]
    [InlineData("Chrome", "will.i.am")]
    [InlineData("Chrome", "Dr.Dre")]
    [InlineData("Chrome", "dr.dre")]
    [InlineData("Chrome", "Vol.II")]
    [InlineData("Chrome", "vol.ii")]
    [InlineData("Chrome", "Music.YouTube.com")]
    [InlineData("Chrome", "localhost.test:8080")]
    [InlineData("Chrome", "router.lan")]
    [InlineData("Chrome", "")]
    [InlineData("Chrome", null)]
    // An app's artist is the artist, however much it looks like a host.
    [InlineData("Spotify.exe", "deadmau5.com")]
    public void AnythingElseIsNoSite(string app, string? artist) =>
        Assert.Null(NowPlayingSource.SiteOf(app, artist));

    /// <summary>
    /// Only well-known media sites are asked for their favicon, each as the host it is listed as or
    /// its "www." and "m." forms — never a host a page merely names, never on a port of its own. A
    /// site the list names one by one (music.youtube.com, open.spotify.com) is asked itself; any other
    /// subdomain of a listed site shows that site's favicon, asked of the listed site alone.
    /// </summary>
    [Theory]
    [InlineData("www.youtube.com", "www.youtube.com")]
    [InlineData("m.youtube.com", "m.youtube.com")]
    [InlineData("music.youtube.com", "music.youtube.com")]
    [InlineData("youtube.com", "youtube.com")]
    [InlineData("open.spotify.com", "open.spotify.com")]
    [InlineData("www.twitch.tv", "www.twitch.tv")]
    [InlineData("music.apple.com", "music.apple.com")]
    [InlineData("www.bbc.co.uk", "www.bbc.co.uk")]
    [InlineData("x.bandcamp.com", "bandcamp.com")]
    [InlineData("deep.under.bandcamp.com", "bandcamp.com")]
    [InlineData("studio.youtube.com", "youtube.com")]
    [InlineData("x.music.youtube.com", "music.youtube.com")]
    [InlineData("apple.com", null)]
    [InlineData("www.apple.com", null)]
    [InlineData("notyoutube.com", null)]
    [InlineData("youtube.com.evil.example", null)]
    [InlineData("tracker.example.com", null)]
    [InlineData("www.youtube.com:8443", null)]
    [InlineData("bücher.de", null)]
    public void OnlyKnownMediaSitesAreFetched(string site, string? host)
    {
        Assert.Equal(host, NowPlayingSource.IconHost(site));
        Assert.Equal(host is not null, NowPlayingSource.Fetches(site));
    }

    /// <summary>
    /// Every request for a media favicon is checked: https on its own port, no user part, and a
    /// listed host exactly — not any subdomain of one, not a CDN a page links its icon to.
    /// </summary>
    [Theory]
    [InlineData("https://www.youtube.com/", true)]
    [InlineData("https://m.youtube.com/favicon.ico", true)]
    [InlineData("https://music.youtube.com/img/favicon_48.png", true)]
    [InlineData("https://bandcamp.com/favicon.ico", true)]
    [InlineData("https://WWW.YouTube.com/favicon.ico", true)]
    [InlineData("https://www.youtube.com:443/favicon.ico", true)]
    [InlineData("http://www.youtube.com/favicon.ico", false)]
    [InlineData("ftp://www.youtube.com/favicon.ico", false)]
    [InlineData("https://www.youtube.com:8443/favicon.ico", false)]
    [InlineData("https://user@www.youtube.com/favicon.ico", false)]
    [InlineData("https://x.bandcamp.com/favicon.ico", false)]
    [InlineData("https://open.spotifycdn.com/cdn/images/favicon32.png", false)]
    [InlineData("https://tracker.example.com/pixel.png", false)]
    [InlineData("https://youtube.com.evil.example/favicon.ico", false)]
    [InlineData("https://127.0.0.1/favicon.ico", false)]
    public void EveryRequestIsToAListedHost(string address, bool allowed) =>
        Assert.Equal(allowed, NowPlayingSource.MayFetch(new Uri(address)));

    /// <summary>A handler that answers as told and writes down every address it was asked.</summary>
    private sealed class Answers(Func<Uri, HttpResponseMessage> answer) : HttpMessageHandler
    {
        public List<string> Asked { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (Asked)
            {
                Asked.Add(request.RequestUri!.AbsoluteUri);
            }
            return Task.FromResult(answer(request.RequestUri!));
        }
    }

    private static HttpResponseMessage Redirect(string to, int status = 302)
    {
        var response = new HttpResponseMessage((System.Net.HttpStatusCode)status);
        response.Headers.Location = new Uri(to, UriKind.RelativeOrAbsolute);
        return response;
    }

    private static HttpResponseMessage Page(string html) => new(System.Net.HttpStatusCode.OK)
    {
        Content = new StringContent(html, System.Text.Encoding.UTF8, "text/html"),
    };

    private static HttpResponseMessage Icon() => new(System.Net.HttpStatusCode.OK)
    {
        Content = new ByteArrayContent([1, 2, 3]) { Headers = { ContentType = new("image/png") } },
    };

    private static HttpResponseMessage Missing() => new(System.Net.HttpStatusCode.NotFound);

    /// <summary>
    /// Redirects are followed one by one, and each is checked before it is asked: to another listed
    /// host, or a relative one, it is followed; to a host off the list, or to plain http, it is not,
    /// and that host hears nothing. Redirects that do not end stop after five.
    /// </summary>
    [Fact]
    public async Task RedirectsAreFollowedOnlyToListedHosts()
    {
        var handler = new Answers(url => url.AbsoluteUri switch
        {
            "https://youtube.com/favicon.ico" => Redirect("https://www.youtube.com/favicon.ico", 301),
            "https://www.youtube.com/favicon.ico" => Redirect("/s/favicon.ico", 307),
            "https://www.youtube.com/s/favicon.ico" => Icon(),
            "https://soundcloud.com/favicon.ico" => Redirect("https://a-v2.sndcdn.com/favicon.ico"),
            "https://twitch.tv/favicon.ico" => Redirect("http://www.twitch.tv/favicon.ico", 308),
            "https://kick.com/favicon.ico" => Redirect("https://kick.com/favicon.ico"),
            _ => Missing(),
        });
        using var client = new HttpClient(handler);
        var source = Karpo.Dock.Favicons.FaviconSource.Within(NowPlayingSource.MayFetch, client);

        Assert.NotNull(await source.Fetch("https://youtube.com/favicon.ico", default));
        Assert.Equal(["https://youtube.com/favicon.ico", "https://www.youtube.com/favicon.ico", "https://www.youtube.com/s/favicon.ico"], handler.Asked);

        handler.Asked.Clear();
        Assert.Null(await source.Fetch("https://soundcloud.com/favicon.ico", default));
        Assert.Null(await source.Fetch("https://twitch.tv/favicon.ico", default));
        Assert.Equal(["https://soundcloud.com/favicon.ico", "https://twitch.tv/favicon.ico"], handler.Asked);

        handler.Asked.Clear();
        Assert.Null(await source.Fetch("https://kick.com/favicon.ico", default));
        Assert.Equal(1 + Karpo.Dock.Favicons.FaviconSource.MostRedirects, handler.Asked.Count);

        // Off the list, or not https, from the start: not asked at all.
        handler.Asked.Clear();
        Assert.Null(await source.Page("http://www.youtube.com/", default));
        Assert.Null(await source.Fetch("https://cdn.example.net/favicon.ico", default));
        Assert.Null(await source.Fetch("ftp://www.youtube.com/favicon.ico", default));
        Assert.Empty(handler.Asked);
    }

    /// <summary>
    /// End to end, as the media store fetches: a listed site's page links its icons to a CDN, to
    /// plain http, to another subdomain and to a tracker; none of them is asked, and /favicon.ico at
    /// the site is.
    /// </summary>
    [Fact]
    public async Task APageCannotSendTheFetchElsewhere()
    {
        var handler = new Answers(url => url.AbsoluteUri switch
        {
            "https://open.spotify.com/" => Page("""
                <html><head>
                <link rel="icon" sizes="192x192" href="https://open.spotifycdn.com/cdn/images/icons/Spotify_192.png">
                <link rel="icon" sizes="96x96" href="http://open.spotify.com/plain.png">
                <link rel="icon" sizes="64x64" href="https://evil.spotify.com/icon.png">
                <link rel="icon" sizes="48x48" href="//tracker.example.com/pixel.png">
                </head></html>
                """),
            _ => Missing(),
        });
        using var client = new HttpClient(handler);
        var store = new Karpo.Dock.Favicons.FaviconStore(
            Karpo.Dock.Favicons.FaviconSource.Within(NowPlayingSource.MayFetch, client), Karpo.Dock.Favicons.FaviconDisk.None);

        store.Picture("https://open.spotify.com/", 24);
        if (store.Working("https://open.spotify.com") is { } work)
        {
            await work;
        }

        Assert.Equal(["https://open.spotify.com/", "https://open.spotify.com/favicon.ico"], handler.Asked);
    }

    /// <summary>
    /// Every face says the site the one way, without its "www.": the grid card's and the bar's
    /// artist line as the flyout's source line. An app's artist is the artist.
    /// </summary>
    [Fact]
    public void EveryFaceSaysTheSiteWithoutItsWww()
    {
        var site = new MediaSource(NowPlayingSource.Say("www.youtube.com"), null, "Brave", "www.youtube.com");
        Assert.Equal("youtube.com", NowPlayingSource.Artist("www.youtube.com", site));
        Assert.Equal("Daft Punk", NowPlayingSource.Artist("Daft Punk", new MediaSource("Spotify", null, "Spotify")));
        Assert.Equal("will.i.am", NowPlayingSource.Artist("will.i.am", null));
    }

    [Fact]
    public void TheLineSaysTheSiteWithoutItsWwwAndTheBrowser()
    {
        Assert.Equal("youtube.com", NowPlayingSource.Say("www.youtube.com"));
        Assert.Equal("open.spotify.com", NowPlayingSource.Say("open.spotify.com"));
        Assert.Equal("www.", NowPlayingSource.Say("www."));
        Assert.Equal("youtube.com · Brave", new MediaSource("youtube.com", null, "Brave", "www.youtube.com").Line);
        Assert.Equal("Spotify", new MediaSource("Spotify", null, "Spotify").Line);
        Assert.Equal("youtube.com", new MediaSource("youtube.com", null, "", "www.youtube.com").Line);
    }
}
