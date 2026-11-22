// ⚙ › Online shows each site's logo beside its name: the search engine (the person, 2026-10-20: "search
// engine configuration should show the search engine logo too"; docs\widgets.md §264, §337) and, §597, the
// map, the weather site and the finance site (the person, 2026-11-09: "download the logos for the map and
// markets sites"). The logos are the sites' own favicons, carried inside the dock (assets\site-logos, embedded
// in Karpo.exe) so they show offline, at 16 and 32 pixels; the person's own search address wears a glyph.
// Which file a site wears is Karpo.Core\SiteLogo.cs.

using System.Buffers.Binary;
using Karpo.Dock;

namespace Karpo.Tests.Core;

public sealed class SiteLogoTests
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static string Folder(string repo) => Path.Combine(repo, "assets", "site-logos");

    /// <summary>Every site Karpo offers that has a logo: each kind's, by its name, the files of each size.</summary>
    private static IEnumerable<(string Site, Func<int, string?> File)> Sites =>
        SearchUrls.All.Where(engine => engine != SearchEngine.Custom).Select(each => (each.Info().Name, (Func<int, string?>)(size => SiteLogo.File(each, size))))
            .Concat(LinkProviders.Maps.Select(each => (each.Name(), (Func<int, string?>)(size => SiteLogo.File(each, size)))))
            .Concat(LinkProviders.Weathers.Select(each => (each.Name(), (Func<int, string?>)(size => SiteLogo.File(each, size)))))
            .Concat(LinkProviders.Markets.Select(each => (each.Name(), (Func<int, string?>)(size => SiteLogo.File(each, size)))));

    /// <summary>A PNG's width and height, from its header; null for a file that is no PNG.</summary>
    private static (int Width, int Height)? PngSize(byte[] bytes)
    {
        if (bytes.Length < 24 || !bytes.AsSpan(0, 8).SequenceEqual(PngSignature) || !bytes.AsSpan(12, 4).SequenceEqual("IHDR"u8))
        {
            return null;
        }
        return (BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4)), BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4)));
    }

    /// <summary>
    /// Every search engine, map, weather site and finance site Karpo offers has a logo at every size, there in the assets,
    /// a PNG of that size; no two sites share a file.
    /// </summary>
    [Fact]
    public void EverySiteHasItsLogo()
    {
        var repo = RepoSource.Root();
        Assert.NotEqual("", repo);
        // Every engine of the enum is one the choice lists, so none goes without; and every map, weather and finance site.
        Assert.Equal(Enum.GetValues<SearchEngine>().Order(), SearchUrls.All.Order());
        Assert.Equal(Enum.GetValues<MapProvider>(), LinkProviders.Maps);
        Assert.Equal(Enum.GetValues<WeatherProvider>(), LinkProviders.Weathers);
        Assert.Equal(Enum.GetValues<MarketsProvider>(), LinkProviders.Markets);
        Assert.Equal(6 + 5 + 4 + 3, Sites.Count());
        foreach (var (site, file) in Sites)
        {
            foreach (var size in SiteLogo.Sizes)
            {
                var name = file(size);
                Assert.False(name is null, $"{site} has no logo at {size}");
                var path = Path.Combine(Folder(repo), name!);
                Assert.True(File.Exists(path), $"{site}'s {name} is not in assets\\site-logos");
                Assert.Equal((size, size), PngSize(File.ReadAllBytes(path)));
                // A favicon, not a stray big picture: a few kilobytes at most.
                Assert.InRange(new FileInfo(path).Length, 100, 16 * 1024);
            }
        }
        Assert.Equal(Sites.Count() * SiteLogo.Sizes.Length, SiteLogo.All.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    /// <summary>A file is named for its kind and its site: Google's search, map and finance logos apart.</summary>
    [Fact]
    public void AFileIsNamedForItsKindAndItsSite()
    {
        Assert.Equal("search-google-32.png", SiteLogo.File(SearchEngine.Google, 32));
        Assert.Equal("map-google-32.png", SiteLogo.File(MapProvider.Google, 32));
        Assert.Equal("markets-google-16.png", SiteLogo.File(MarketsProvider.Google, 16));
        Assert.Equal("map-openstreetmap-16.png", SiteLogo.File(MapProvider.OpenStreetMap, 16));
        Assert.Equal("weather-weathercom-32.png", SiteLogo.File(WeatherProvider.WeatherCom, 32));
        Assert.Equal("markets-tradingview-32.png", SiteLogo.File(MarketsProvider.TradingView, 32));
        // A site this build doesn't know has none.
        Assert.Null(SiteLogo.File((MapProvider)99, 32));
        Assert.Null(SiteLogo.File((MarketsProvider)99, 32));
        Assert.Null(SiteLogo.File((WeatherProvider)99, 32));
    }

    /// <summary>The person's own address has no logo: it wears the globe glyph.</summary>
    [Fact]
    public void TheOwnAddressWearsTheGlyph()
    {
        foreach (var size in SiteLogo.Sizes)
        {
            Assert.Null(SiteLogo.File(SearchEngine.Custom, size));
            Assert.Null(SiteLogo.File((SearchEngine)99, size));
        }
        Assert.Equal("", SiteLogo.Glyph);
    }

    /// <summary>Every logo in the folder is a site's, and the README says where it came from.</summary>
    [Fact]
    public void EveryLogoIsASitesAndIsRecorded()
    {
        var repo = RepoSource.Root();
        Assert.NotEqual("", repo);
        var worn = SiteLogo.All.ToHashSet(StringComparer.Ordinal);
        var readme = RepoSource.Read("assets", "site-logos", "README.md");

        foreach (var path in Directory.GetFiles(Folder(repo)).Where(path => !path.EndsWith("README.md", StringComparison.Ordinal)))
        {
            var name = Path.GetFileName(path);
            Assert.Contains(name, worn);
            Assert.Contains($"`{name}`", readme, StringComparison.Ordinal);
        }
        foreach (var (site, _) in Sites)
        {
            Assert.Contains($"| {site.Split(' ')[0]} ", readme, StringComparison.Ordinal);
        }
    }

    /// <summary>The dock carries every logo inside its exe, under the name it reads it by (MenuPictures), a PNG of its size, and decodes it.</summary>
    [Fact]
    public void TheDockCarriesTheLogos()
    {
        Assert.Contains(@"<EmbeddedResource Include=""..\assets\site-logos\*.png"" LogicalName=""SiteLogos.%(Filename)%(Extension)"" />", RepoSource.Read("Karpo.Dock", "Karpo.Dock.csproj"), StringComparison.Ordinal);
        var dock = typeof(MenuPictures).Assembly;
        foreach (var (site, file) in Sites)
        {
            foreach (var size in SiteLogo.Sizes)
            {
                using var stream = dock.GetManifestResourceStream(SiteLogo.Folder + "." + file(size));
                Assert.True(stream is not null, $"{site}'s {file(size)} is not in the exe");
                using var bytes = new MemoryStream();
                stream.CopyTo(bytes);
                Assert.Equal((size, size), PngSize(bytes.ToArray()));
            }
            Assert.IsType<FlyoutIcon.Picture>(MenuPictures.Of(file(SiteLogo.Sizes[^1])!));
        }
        Assert.Null(MenuPictures.Of("map-nowhere-32.png"));
    }

    /// <summary>The 16 on a screen at 100 %, the 32 above it, and the 16 for a scale that is none.</summary>
    [Theory]
    [InlineData(1.0, 16)]
    [InlineData(1.25, 32)]
    [InlineData(1.5, 32)]
    [InlineData(2.0, 32)]
    [InlineData(3.0, 32)]
    [InlineData(0.0, 16)]
    [InlineData(-1.0, 16)]
    [InlineData(double.NaN, 16)]
    public void TheSizeFitsTheScreen(double scale, int size) =>
        Assert.Equal(size, SiteLogo.SizeFor(scale));
}
