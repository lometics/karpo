// §335: "Online ›", a level of what Karpo does on the internet for every widget alike. Today the sites a
// widget opens what it shows on (§319, §322: LinkProviders) — the map every "Open on map" goes to, the
// weather site "Open forecast" goes to, the finance site a share's "Open website" goes to —, each a
// choice of its sites under their own names, written as the General page's Links cards write them
// ("mapProvider", "weatherProvider", "marketsProvider"). The dock takes them as the config is applied
// (LinkProviders.Use). §597: each site wears its own logo, as the search engines do (SiteLogo's file).
//
// §337: before the sites, the General page's Online and Search engine cards: how often everything online
// updates ("onlineUpdateMinutes", OnlineUpdateRow's choices; one the file has that is none of them still
// shows, ticked, in its place), the search engine every web search goes to ("searchEngine", each engine
// wearing its logo, SiteLogo's file, which the dock carries; the person's own address the globe), and,
// only while that is the person's own, its address ("searchCustomUrl", a line of text saying under it
// what is wrong with it or where the words go, SearchUrls.Status, and offering the engines' own
// addresses to start from, §304).

namespace Karpo.Core;

public static partial class KarpoMenu
{
    /// <summary>The map's row.</summary>
    public const string MapRow = "OnlineMap";

    /// <summary>The weather site's row.</summary>
    public const string WeatherRow = "OnlineWeather";

    /// <summary>The finance site's row.</summary>
    public const string MarketsRow = "OnlineMarkets";

    /// <summary>The update interval's row.</summary>
    public const string UpdateRow = "OnlineUpdate";

    /// <summary>The search engine's row.</summary>
    public const string EngineRow = "SearchEngine";

    /// <summary>The person's own search address's row.</summary>
    public const string AddressRow = "SearchAddress";

    /// <summary>The longest a search address may be, in UTF-16 units: a long address with room to spare.</summary>
    public const int AddressLongest = 2048;

    /// <summary>
    /// "Online ›": how often everything online updates, the search engine and, while it is the person's
    /// own, its address; then the map, the weather site and the finance site; each of <paramref name="config"/>'s.
    /// </summary>
    public static IReadOnlyList<OptionRow> Online(Config config, KarpoServices services)
    {
        var minutes = OnlineUpdateRow.MinutesWith(config.OnlineUpdateMinutes);
        string Short(int each) => each % 60 == 0 ? S.WidgetCommonHours(each / 60) : S.WidgetCommonMinutes(each);
        // §517: at its row's end how often everything online updates, short.
        OptionRow In(OptionRow row) => row with { Group = S.SettingsOnlineSection, GroupGlyph = OptionGlyphs.Globe, GroupSummary = Short(config.OnlineUpdateMinutes) };
        var engine = config.SearchEngineOrDefault;
        return
        [
            In(new OptionRow(UpdateRow, OptionRowKind.Choice, S.SettingsOnlineUpdateHeader)
            {
                Glyph = OptionGlyphs.Automatic,
                Choices = [.. minutes.Select(OnlineUpdateRow.Say)],
                // At the row's end short, "15 min", "2 h", where "Every 15 minutes" would be cut (§323).
                ChoicesSaid = [.. minutes.Select(Short)],
                Chosen = IndexOf(minutes, config.OnlineUpdateMinutes),
                Set = at =>
                {
                    if (at < 0 || at >= minutes.Count || minutes[at] == config.OnlineUpdateMinutes)
                    {
                        return false;
                    }
                    config.OnlineUpdateMinutes = minutes[at];
                    return true;
                },
            }),
            In(Site(EngineRow, S.SettingsSearchEngineHeader, OptionGlyphs.Search, SearchUrls.All, engine, each => each.Info().Name, each => SiteLogo.File(each, SiteLogo.Sizes[^1]), chosen => config.SearchEngine = chosen) with
            {
                ChoiceGlyphs = [.. SearchUrls.All.Select(each => each == SearchEngine.Custom ? SiteLogo.Glyph : OptionGlyphs.Search)],
            }),
            .. engine == SearchEngine.Custom ? [In(Address(config))] : Array.Empty<OptionRow>(),
            In(Site(MapRow, S.SettingsMapProviderHeader, OptionGlyphs.Place, LinkProviders.Maps, config.MapProviderOrDefault, LinkProviders.Name, each => SiteLogo.File(each, SiteLogo.Sizes[^1]), chosen => config.MapProvider = chosen)),
            In(Site(WeatherRow, S.SettingsLinksWeatherHeader, OptionGlyphs.Cloud, LinkProviders.Weathers, config.WeatherProviderOrDefault, LinkProviders.Name, each => SiteLogo.File(each, SiteLogo.Sizes[^1]), chosen => config.WeatherProvider = chosen)),
            In(Site(MarketsRow, S.SettingsLinksMarketsHeader, OptionGlyphs.Trend, LinkProviders.Markets, config.MarketsProviderOrDefault, LinkProviders.Name, each => SiteLogo.File(each, SiteLogo.Sizes[^1]), chosen => config.MarketsProvider = chosen)),
        ];
    }

    /// <summary>Where <paramref name="value"/> stands in <paramref name="values"/>; -1 for nowhere.</summary>
    private static int IndexOf(IReadOnlyList<int> values, int value)
    {
        for (var at = 0; at < values.Count; at++)
        {
            if (values[at] == value)
            {
                return at;
            }
        }
        return -1;
    }

    /// <summary>
    /// The person's own search address, as <paramref name="config"/> has it: a line of text saying under it
    /// what it is worth (<see cref="SearchUrls.Status"/>), offered the engines' own addresses to start
    /// from; kept trimmed, none when empty.
    /// </summary>
    private static OptionRow Address(Config config)
    {
        var now = config.SearchCustomUrl ?? "";
        return new(AddressRow, OptionRowKind.Text, S.SettingsSearchAddressHeader)
        {
            Glyph = SiteLogo.Glyph,
            Field = OptionField.Text,
            Text = now,
            Placeholder = SearchUrls.Example,
            Longest = AddressLongest,
            Why = SearchUrls.Status(now),
            Finder = SuggestFinder.Of(now, SearchUrls.All.Where(each => each != SearchEngine.Custom).Select(each => new OptionFound(each.Info().Name, each.Info().Search, each.Info().Search))),
            SetText = typed =>
            {
                var kept = typed.Trim() is { Length: > 0 } trimmed ? trimmed : null;
                if (kept == config.SearchCustomUrl)
                {
                    return false;
                }
                config.SearchCustomUrl = kept;
                return true;
            },
        };
    }

    /// <summary>
    /// A kind of site as a choice of <paramref name="all"/> by their names, each wearing its logo (<paramref name="logo"/>,
    /// SiteLogo's file; none for its glyph, §597), <paramref name="now"/> ticked; a pick kept (<paramref name="keep"/>)
    /// where it is another than the one in force.
    /// </summary>
    private static OptionRow Site<T>(string key, string title, string glyph, T[] all, T now, Func<T, string> name, Func<T, string?> logo, Action<T> keep) where T : struct, Enum =>
        new(key, OptionRowKind.Choice, title)
        {
            Glyph = glyph,
            Choices = [.. all.Select(name)],
            ChoicePictures = [.. all.Select(each => logo(each) ?? "")],
            Chosen = Math.Max(0, Array.IndexOf(all, now)),
            Set = at =>
            {
                if (at < 0 || at >= all.Length || EqualityComparer<T>.Default.Equals(all[at], now))
                {
                    return false;
                }
                keep(all[at]);
                return true;
            },
        };
}

/// <summary>§337: what a choice's choices wear beside their names where the menu has pictures of its own.</summary>
public sealed partial record OptionRow
{
    /// <summary>
    /// Each of <see cref="Choices"/>' picture the menu has of its own, by what it names (a site's
    /// logo, SiteLogo's file), in their order; "" or none for its glyph (<see cref="ChoiceGlyph"/>).
    /// </summary>
    public IReadOnlyList<string> ChoicePictures { get; init; } = [];
}
