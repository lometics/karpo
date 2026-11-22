// What each site of ⚙ › Online wears beside its name: the search engine (the person, 2026-10-20: "search
// engine configuration should show the search engine logo too", §337) and, §597, the map, the weather site
// and the finance site (the person, 2026-11-09: "download the logos for the map and markets sites"): the
// site's own logo, the favicon it serves, carried inside the dock (assets\site-logos, embedded in the exe)
// so it shows offline; and for the person's own search address, which has no logo, the globe glyph the
// card itself wears. Each logo is there at 16 and 32 pixels, the sizes the sites serve their favicons at:
// 16 for a screen at 100 %, 32, scaled down as it is drawn, above that. A file is named for its kind and
// its site: "search-google-32.png", "map-google-32.png", "markets-google-32.png", "weather-yr-16.png".
// The logos stay in colour in high contrast, as Windows' Settings keeps an app's own icon in colour there:
// each is drawn on a shape of its own, a disc, a shield or a tile, that stands on a black page as on a
// white one. Plain code, for the tests; the dock decodes the files (MenuPictures, Dock.SettingsBackup.cs).

namespace Karpo.Core;

public static class SiteLogo
{
    /// <summary>The name the dock carries the logos under, inside its exe ("SiteLogos.&lt;file&gt;", Karpo.Dock.csproj).</summary>
    public const string Folder = "SiteLogos";

    /// <summary>How big a logo stands beside its name, in DIPs: a list's icon, as "Find a setting"'s are.</summary>
    public const double Shown = 16;

    /// <summary>The sizes each logo is there at, in pixels, smallest first.</summary>
    public static readonly int[] Sizes = [16, 32];

    /// <summary>The glyph a search engine with no logo wears, the person's own address: the card's globe (Segoe Fluent Icons).</summary>
    public const string Glyph = "";

    /// <summary>
    /// The logo's file for <paramref name="engine"/> at <paramref name="size"/> pixels,
    /// "search-duckduckgo-32.png"; null for the person's own address, which has none.
    /// </summary>
    public static string? File(SearchEngine engine, int size) => engine == SearchEngine.Custom ? null : Named("search", engine, size);

    /// <summary>The logo's file for <paramref name="map"/> at <paramref name="size"/> pixels, "map-openstreetmap-32.png".</summary>
    public static string? File(MapProvider map, int size) => Named("map", map, size);

    /// <summary>The logo's file for <paramref name="site"/> at <paramref name="size"/> pixels, "weather-yr-32.png".</summary>
    public static string? File(WeatherProvider site, int size) => Named("weather", site, size);

    /// <summary>The logo's file for <paramref name="site"/> at <paramref name="size"/> pixels, "markets-tradingview-32.png".</summary>
    public static string? File(MarketsProvider site, int size) => Named("markets", site, size);

    /// <summary>Every logo file there is: each site's of every kind, at every size.</summary>
    public static IEnumerable<string> All =>
        Sizes.SelectMany(size => SearchUrls.All.Select(each => File(each, size))
            .Concat(LinkProviders.Maps.Select(each => File(each, size)))
            .Concat(LinkProviders.Weathers.Select(each => File(each, size)))
            .Concat(LinkProviders.Markets.Select(each => File(each, size))))
            .OfType<string>();

    /// <summary>"&lt;kind&gt;-&lt;site&gt;-&lt;size&gt;.png", the site's name in lower case; null for a site this build doesn't know.</summary>
    private static string? Named<T>(string kind, T site, int size) where T : struct, Enum =>
        Enum.IsDefined(site) ? $"{kind}-{site.ToString().ToLowerInvariant()}-{size}.png" : null;

    /// <summary>
    /// The size to draw from on a screen at <paramref name="scale"/> (1 at 100 %): the smallest that
    /// is as big as the logo stands there, or the biggest there is.
    /// </summary>
    public static int SizeFor(double scale)
    {
        var wanted = Shown * (double.IsFinite(scale) && scale > 0 ? scale : 1);
        return Sizes.FirstOrDefault(size => size >= wanted - 0.01, Sizes[^1]);
    }
}
