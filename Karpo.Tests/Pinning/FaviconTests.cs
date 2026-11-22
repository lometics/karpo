// A web pin shows the site's own favicon.
//
// The parts with rules in them are pure text and pure arithmetic, so they are tested without a
// network and without a window: which addresses a site's icon might be at and in what order, which
// frame of a multi-size .ico to draw, what one cache entry is keyed by, when a kept copy is old
// enough to fetch again, and — the one that matters most — that a fetch which fails leaves the
// picture a pin already had exactly where it was.
//
// No test here reads or writes the person's own folder: the store takes a FaviconDisk, and these
// hand in one that keeps nothing or one that keeps a string. Which sites somebody has pinned is
// theirs.

using System.Text.Json;
using Karpo.Dock.Favicons;

namespace Karpo.Tests.Pinning;

public sealed class FaviconTests
{
    /// <summary>A disk that keeps what it was given, so a test can read back what the store saved.</summary>
    private sealed class Paper
    {
        public string? Text { get; set; }

        public FaviconDisk Disk => new() { Read = () => Text, Write = text => Text = text };
    }

    private static FaviconSource Source(
        Func<string, Task<string?>>? page = null,
        Func<string, Task<(byte[] Bytes, string? ContentType)?>>? fetch = null) => new()
        {
            Page = (address, _) => page?.Invoke(address) ?? Task.FromResult<string?>(null),
            Fetch = (address, _) => fetch?.Invoke(address) ?? Task.FromResult<(byte[], string?)?>(null),
        };

    // --- The origin one entry and one fetch are shared by ----------------------------

    [Theory]
    [InlineData("https://example.com", "https://example.com")]
    [InlineData("https://example.com/", "https://example.com")]
    [InlineData("https://Example.COM/deep/page?q=1#top", "https://example.com")]
    [InlineData("https://example.com:443/x", "https://example.com")]
    [InlineData("http://example.com:80/x", "http://example.com")]
    [InlineData("http://example.com:8080/x", "http://example.com:8080")]
    [InlineData("https://sub.example.com/x", "https://sub.example.com")]
    public void TheKeyIsTheOrigin(string target, string origin) =>
        Assert.Equal(origin, FaviconUrls.OriginOf(target));

    /// <summary>Two pins to two pages of one site are one entry, one fetch and one picture.</summary>
    [Fact]
    public void TwoPagesOfOneSiteShareTheKey() =>
        Assert.Equal(
            FaviconUrls.OriginOf("https://example.com/one"),
            FaviconUrls.OriginOf("https://example.com/two?x=1"));

    [Theory]
    [InlineData(@"C:\Windows\notepad.exe")]
    [InlineData(@"shell:AppsFolder\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App")]
    [InlineData("ftp://example.com/x")]
    [InlineData("mailto:someone@example.com")]
    [InlineData("")]
    [InlineData(null)]
    public void EverythingThatIsNotAWebAddressHasNone(string? target) =>
        Assert.Null(FaviconUrls.OriginOf(target));

    // --- What a page declares ---------------------------------------------------------

    [Fact]
    public void TheDeclaredIconsAreReadOutOfTheHead()
    {
        const string html = """
            <html><head>
            <link rel="icon" type="image/png" sizes="32x32" href="/icons/32.png">
            <link rel='shortcut icon' href='/favicon.ico'>
            <link rel="apple-touch-icon" sizes="180x180" href="https://cdn.example.net/touch.png">
            <link rel="stylesheet" href="/site.css">
            <link rel="mask-icon" href="/mask.svg" color="#000">
            </head><body></body></html>
            """;

        var links = FaviconUrls.Links(html, "https://example.com/page");

        Assert.Equal(3, links.Count);
        // Relative hrefs resolved against the page, absolute ones left alone, and a stylesheet or a
        // Safari mask — a one-colour silhouette — left out.
        Assert.Contains(links, link => link.Address == "https://example.com/icons/32.png" && link.Side == 32);
        Assert.Contains(links, link => link.Address == "https://example.com/favicon.ico" && link.Side == 0);
        Assert.Contains(links, link => link.Address == "https://cdn.example.net/touch.png" && link.Side == 180);
        Assert.DoesNotContain(links, link => link.Address.Contains("mask", StringComparison.Ordinal));
        Assert.DoesNotContain(links, link => link.Address.Contains("site.css", StringComparison.Ordinal));
    }

    [Fact]
    public void AnSvgIsSeenForWhatItIs()
    {
        const string html = """
            <head>
            <link rel="icon" href="/icon.svg" type="image/svg+xml">
            <link rel="icon" href="/any" sizes="any">
            <link rel="icon" href="/plain.png" sizes="48x48">
            </head>
            """;

        var links = FaviconUrls.Links(html, "https://example.com/");

        Assert.True(links.Single(link => link.Address.EndsWith("icon.svg", StringComparison.Ordinal)).Scalable);
        Assert.True(links.Single(link => link.Address.EndsWith("/any", StringComparison.Ordinal)).Scalable);
        Assert.False(links.Single(link => link.Address.EndsWith("plain.png", StringComparison.Ordinal)).Scalable);
    }

    [Fact]
    public void APageThatDeclaresNothingDeclaresNothing()
    {
        Assert.Empty(FaviconUrls.Links("<html><body>hello</body></html>", "https://example.com/"));
        Assert.Empty(FaviconUrls.Links("", "https://example.com/"));
    }

    [Theory]
    [InlineData("16x16 32x32 48x48", 48)]
    [InlineData("180X180", 180)]
    [InlineData("any", 0)]
    [InlineData("", 0)]
    [InlineData("nonsense", 0)]
    public void TheLargestDeclaredSquareIsRead(string sizes, int side) =>
        Assert.Equal(side, FaviconUrls.Largest(sizes));

    // --- Which of them to try first ---------------------------------------------------

    /// <summary>
    /// The nearest size at or above the cell wins, a larger picture beats a smaller one, an SVG is
    /// last because nothing here can decode it, and /favicon.ico is always the final candidate.
    /// </summary>
    [Fact]
    public void TheCandidatesAreOrderedByHowWellTheyFit()
    {
        List<FaviconLink> links =
        [
            new("https://example.com/16.png", "icon", 16, false),
            new("https://example.com/180.png", "apple-touch-icon", 180, false),
            new("https://example.com/32.png", "icon", 32, false),
            new("https://example.com/icon.svg", "icon", 0, true),
        ];

        var order = FaviconUrls.Rank(links, "https://example.com", wanted: 24);

        Assert.Equal(
            [
                "https://example.com/32.png",
                "https://example.com/180.png",
                "https://example.com/16.png",
                "https://example.com/icon.svg",
                "https://example.com/favicon.ico",
            ],
            order);
    }

    [Fact]
    public void TheFallbackIsAlwaysThereAndOnlyOnce()
    {
        var none = FaviconUrls.Rank([], "https://example.com", 24);
        Assert.Equal(["https://example.com/favicon.ico"], none);

        // A page that declares /favicon.ico itself does not get it twice.
        var declared = FaviconUrls.Rank(
            [new FaviconLink("https://example.com/favicon.ico", "icon", 0, false)],
            "https://example.com",
            24);
        Assert.Equal(["https://example.com/favicon.ico"], declared);
    }

    // --- Which frame of a multi-size .ico ---------------------------------------------

    /// <summary>
    /// The smallest frame at least as large as the cell: shrinking keeps detail that growing
    /// invents. With nothing large enough, the largest there is.
    /// </summary>
    [Theory]
    [InlineData(new[] { 16, 32, 48 }, 24, 1)]
    [InlineData(new[] { 16, 32, 48 }, 32, 1)]
    [InlineData(new[] { 16, 32, 48 }, 16, 0)]
    [InlineData(new[] { 16, 32, 48 }, 64, 2)]
    [InlineData(new[] { 48, 32, 16 }, 24, 1)]
    [InlineData(new[] { 64 }, 24, 0)]
    [InlineData(new[] { 8 }, 24, 0)]
    public void TheFrameNearestTheCellIsDrawn(int[] sides, int wanted, int frame) =>
        Assert.Equal(frame, FaviconImage.BestFrame(sides, wanted));

    [Fact]
    public void NoFramesMeansNoPicture()
    {
        Assert.Equal(-1, FaviconImage.BestFrame([], 24));
        Assert.Equal(-1, FaviconImage.BestFrame([0, 0], 24));
    }

    [Theory]
    [InlineData("https://example.com/icon.svg", null, false)]
    [InlineData("https://example.com/icon", "image/svg+xml", false)]
    [InlineData("https://example.com/icon.png", null, true)]
    [InlineData("https://example.com/favicon.ico", "image/x-icon", true)]
    [InlineData("https://example.com/icon.svg?v=2", null, false)]
    public void OnlyAnSvgIsRuledOutBeforeItIsEvenDownloaded(string address, string? type, bool decodable) =>
        Assert.Equal(decodable, FaviconImage.Decodable(address, type));

    // --- When a kept copy is fetched again --------------------------------------------

    private static FaviconStore.Entry Entry(DateTime? fetchedUtc, bool hasPicture, DateTime attempted = default) =>
        new("https://example.com")
        {
            Attempted = attempted,
            Cached = fetchedUtc is { } when
                ? new FaviconCache { Origin = "https://example.com", FetchedUtc = when, Bytes = hasPicture ? "AAAA" : null }
                : null,
        };

    [Fact]
    public void AKeptPictureIsGoodForAMonth()
    {
        var now = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        Assert.False(FaviconStore.Due(Entry(now.AddDays(-29), hasPicture: true), now));
        Assert.True(FaviconStore.Due(Entry(now.AddDays(-31), hasPicture: true), now));
        // A favicon changes about never, so a start is not a reason to ask.
        Assert.False(FaviconStore.Due(Entry(now.AddHours(-1), hasPicture: true), now));
    }

    [Fact]
    public void ASiteThatGaveNothingIsLeftAloneForHours()
    {
        var now = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        Assert.False(FaviconStore.Due(Entry(now.AddHours(-1), hasPicture: false), now));
        Assert.True(FaviconStore.Due(Entry(now.AddHours(-7), hasPicture: false), now));
    }

    /// <summary>A site nothing is known about is asked the first time it is drawn, and not before.</summary>
    [Fact]
    public void AnUnknownSiteIsAskedOnce()
    {
        var now = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        Assert.True(FaviconStore.Due(Entry(null, hasPicture: false), now));
        // And not again while that attempt is recent, which is what keeps a failed fetch from
        // starting another on the next build — the kept picture's own age does not move.
        Assert.False(FaviconStore.Due(Entry(now.AddYears(-1), hasPicture: true, attempted: now.AddMinutes(-1)), now));
        Assert.True(FaviconStore.Due(Entry(now.AddYears(-1), hasPicture: true, attempted: now.AddHours(-7)), now));
    }

    /// <summary>A clock moved backwards leaves a stamp in the future; that is fresh, not stale.</summary>
    [Fact]
    public void AStampInTheFutureCostsNoRequests()
    {
        var now = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        Assert.False(FaviconStore.Due(Entry(now.AddDays(5), hasPicture: true), now));
    }

    // --- A failure changes nothing ----------------------------------------------------

    /// <summary>
    /// The one that matters: a fetch that finds nothing leaves the picture a pin already had. Not a
    /// blank square, not a letter where there was a picture — what was there stays there, on disk as
    /// well as on the bar, so the next start still draws it.
    /// </summary>
    [Fact]
    public async Task AFailedFetchLeavesThePictureThatWasThere()
    {
        const string origin = "https://example.com";
        var paper = new Paper
        {
            Text = JsonSerializer.Serialize(
                new FaviconFile
                {
                    Sites =
                    [
                        new FaviconCache
                        {
                            Origin = origin,
                            // Old enough to be fetched again.
                            FetchedUtc = DateTime.UtcNow - TimeSpan.FromDays(60),
                            Bytes = "T0xE",
                            Address = origin + "/favicon.ico",
                        },
                    ],
                },
                FaviconJson.Default.FaviconFile),
        };
        // Every candidate answers with nothing: no network, a 404, a site that is down.
        var store = new FaviconStore(Source(), paper.Disk);

        store.Picture(origin + "/page", 24);
        if (store.Working(origin) is { } work)
        {
            await work;
        }

        // In memory: the kept entry still carries the picture it had.
        Assert.Equal("T0xE", store.Kept(origin)?.Bytes);
        // And on disk, with the stamp moved on so the site is not asked again at once.
        var saved = JsonSerializer.Deserialize(paper.Text!, FaviconJson.Default.FaviconFile)!.Sites.Single();
        Assert.Equal("T0xE", saved.Bytes);
        Assert.Equal(origin + "/favicon.ico", saved.Address);
        Assert.True(DateTime.UtcNow - saved.FetchedUtc < TimeSpan.FromMinutes(1), "the attempt was recorded");
    }

    /// <summary>
    /// And a pin whose site has no picture yet shows nothing rather than something: null is what
    /// leaves the letter tile in place, and the pin's size never depended on it either way.
    /// </summary>
    [Fact]
    public void ASiteWithNothingKeptDrawsNothingYet()
    {
        var store = new FaviconStore(Source(), FaviconDisk.None);

        Assert.Null(store.Picture("https://example.com/page", 24));
        // And a pin that is not a web address never reaches the store at all.
        Assert.Null(store.Picture(@"C:\Windows\notepad.exe", 24));
        Assert.Null(store.Picture(null, 24));
    }

    /// <summary>
    /// With no dock there is no store, and a web pin is drawn exactly as it always was. This is what
    /// keeps a test run — which draws web pins — off the person's favicons.json and off the network:
    /// the store is something the dock starts on its own thread, not something the first caller
    /// conjures. A test that ever sees this fail is a test about to ask a real site for a picture.
    /// </summary>
    [Fact]
    public void WithNoDockThereIsNoStoreAndNoFetch()
    {
        Assert.Null(FaviconStore.Current);
        Assert.Null(new Karpo.Dock.Item
        {
            Target = "https://example.com/page",
            Name = "example.com",
            Kind = Karpo.Dock.ItemKind.Link,
            Keys = [],
        }.Icon(24));
    }

    /// <summary>The page is read for its declarations, and the best of them is what gets fetched.</summary>
    [Fact]
    public async Task TheDeclaredIconIsPreferredToTheFallback()
    {
        const string origin = "https://example.com";
        var asked = new List<string>();
        var store = new FaviconStore(
            Source(
                page: _ => Task.FromResult<string?>("""<head><link rel="icon" sizes="32x32" href="/best.png"></head>"""),
                fetch: address =>
                {
                    asked.Add(address);
                    return Task.FromResult<(byte[], string?)?>(null);
                }),
            FaviconDisk.None);

        store.Picture(origin + "/page", 24);
        if (store.Working(origin) is { } work)
        {
            await work;
        }

        Assert.Equal([origin + "/best.png", origin + "/favicon.ico"], asked);
    }

    /// <summary>An SVG is never even downloaded: nothing in this build can decode one.</summary>
    [Fact]
    public async Task AnSvgIsNotFetched()
    {
        const string origin = "https://example.com";
        var asked = new List<string>();
        var store = new FaviconStore(
            Source(
                page: _ => Task.FromResult<string?>("""<head><link rel="icon" href="/icon.svg" type="image/svg+xml"></head>"""),
                fetch: address =>
                {
                    asked.Add(address);
                    return Task.FromResult<(byte[], string?)?>(null);
                }),
            FaviconDisk.None);

        store.Picture(origin + "/page", 24);
        if (store.Working(origin) is { } work)
        {
            await work;
        }

        Assert.Equal([origin + "/favicon.ico"], asked);
    }

    /// <summary>A page that cannot be read is not a failure: /favicon.ico is asked for anyway.</summary>
    [Fact]
    public async Task APageThatCannotBeReadStillLeavesTheFallback()
    {
        const string origin = "https://example.com";
        var asked = new List<string>();
        var store = new FaviconStore(
            Source(
                page: _ => throw new HttpRequestException("no"),
                fetch: address =>
                {
                    asked.Add(address);
                    return Task.FromResult<(byte[], string?)?>(null);
                }),
            FaviconDisk.None);

        store.Picture(origin + "/page", 24);
        if (store.Working(origin) is { } work)
        {
            await work;
        }

        Assert.Equal([origin + "/favicon.ico"], asked);
    }
}
