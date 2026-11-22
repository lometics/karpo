# Site logos

The logos beside each site's name in the settings panel's Karpo page, Online section: the search engine
(docs\widgets.md §264, §337), the map, the weather site and the finance site (§597), in each dropdown's list
and on its closed box. Each is the favicon the site's own domain serves, at the two sizes the dropdown draws
from: 16 pixels for a screen at 100 %, 32 above that. A size the site serves is taken unchanged; one it
doesn't is its next bigger picture scaled down (bicubic). The person's own search address (Custom) has no
logo and wears the card's globe glyph instead.

A file is `<kind>-<site>-<size>.png`, the site's name as in `Karpo.Core\WebSearch.cs` (`SearchEngine`) or
`Karpo.Core\LinkProviders.cs` (`MapProvider`, `WeatherProvider`, `MarketsProvider`), in lower case
(`Karpo.Core\SiteLogo.cs`). `Karpo.Dock.csproj` embeds them in the exe (`SiteLogos.<file>`), so they show
offline. `SiteLogoTests` holds that every site has both sizes, that each is a PNG of its size, that every file
is a site's and named here, and that the exe carries them.

## Search engines (downloaded 2026-10-20)

| Engine     | Files                                                  | From                                                                                                    |
| ---------- | ------------------------------------------------------ | ------------------------------------------------------------------------------------------------------- |
| Google     | `search-google-16.png`, `search-google-32.png`         | `https://www.google.com/favicon.ico`, its 16 and 32 frames                                              |
| Bing       | `search-bing-16.png`, `search-bing-32.png`             | `https://www.bing.com/favicon.ico`, its one frame (32); the 16 scaled down from it                      |
| DuckDuckGo | `search-duckduckgo-16.png`, `search-duckduckgo-32.png` | `https://duckduckgo.com/static-assets/favicons/favicon.ico`, its 16 and 32 frames                       |
| Ecosia     | `search-ecosia-16.png`, `search-ecosia-32.png`         | `https://www.ecosia.org/static/icons/favicon.ico`, its 16 and 32 frames                                 |
| Startpage  | `search-startpage-16.png`, `search-startpage-32.png`   | `https://cdn.startpage.com/sp/cdn/favicons/favicon-16x16-gradient.png` and `favicon-32x32-gradient.png` |
| Brave      | `search-brave-16.png`, `search-brave-32.png`           | `search.brave.com`'s `favicon.ico`, its 16 frame; the 32 scaled down from its `favicon-96x96.png`       |

## Maps (downloaded 2026-11-09)

| Map           | Files                                                    | From                                                                                                     |
| ------------- | -------------------------------------------------------- | -------------------------------------------------------------------------------------------------------- |
| OpenStreetMap | `map-openstreetmap-16.png`, `map-openstreetmap-32.png`   | `https://www.openstreetmap.org/assets/favicon-16x16-474476c1b2381628a81361a9e2bf04b936d21f77c59e84b48c6c69ea376fb6cf.png` and `favicon-32x32-99b88fcadeef736889823c8a886b89d8cada9d4423a49a27de29bacc0a6bebd1.png` (its `<link rel="icon">`s) |
| Google Maps   | `map-google-16.png`, `map-google-32.png`                 | `https://www.google.com/images/branding/product/ico/web_maps_icon_32dp.ico` (a 32 PNG); the 16 scaled down |
| Bing Maps     | `map-bing-16.png`, `map-bing-32.png`                     | `https://www.bing.com/maps/favicon.ico`, its one frame (32; Bing's own b); the 16 scaled down from it    |
| Apple Maps    | `map-apple-16.png`, `map-apple-32.png`                   | `https://maps.apple.com/favicon.ico`, its 16 and 32 frames                                               |
| HERE WeGo     | `map-here-16.png`, `map-here-32.png`                     | `https://wego.here.com/favicon.png` (32); the 16 scaled down from it                                     |

## Finance sites (downloaded 2026-11-09)

| Site           | Files                                                      | From                                                                                         |
| -------------- | ---------------------------------------------------------- | -------------------------------------------------------------------------------------------- |
| Yahoo Finance  | `markets-yahoo-16.png`, `markets-yahoo-32.png`             | `https://s.yimg.com/cv/apiv2/finance/YF_Favicon_16.png` and `YF_Favicon_32.png` (finance.yahoo.com's icons) |
| Google Finance | `markets-google-16.png`, `markets-google-32.png`           | `https://www.gstatic.com/finance/favicon/favicon.png` (32, www.google.com/finance's icon); the 16 scaled down |
| TradingView    | `markets-tradingview-16.png`, `markets-tradingview-32.png` | `https://static.tradingview.com/static/images/favicon.ico` (www.tradingview.com's icon), its 16 and 32 frames |

## Weather sites (downloaded 2026-11-09)

| Site        | Files                                                    | From                                                                                                      |
| ----------- | -------------------------------------------------------- | --------------------------------------------------------------------------------------------------------- |
| weather.com | `weather-weathercom-16.png`, `weather-weathercom-32.png` | `https://weather.com/_next/static/immutable/media/favicon-16x16.3n4geuwmpscvl.png` and `favicon-32x32.1vzmkh_tafy7j.png` (its `<link rel="icon">`s) |
| MSN Weather | `weather-msn-16.png`, `weather-msn-32.png`               | `https://www.msn.com/favicon.ico`, its one frame (32); the 16 scaled down from it                         |
| Yr          | `weather-yr-16.png`, `weather-yr-32.png`                 | `https://www.yr.no/assets/images/favicon-16x16.png` and `favicon-32x32.png`                                |
| meteoblue   | `weather-meteoblue-16.png`, `weather-meteoblue-32.png`   | `https://www.meteoblue.com/favicon.ico`, its 16 and 32 frames                                             |

The logos are their owners' trademarks, shown only to name the site a search, a place, a forecast or a share
opens on.
