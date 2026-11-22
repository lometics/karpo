// The list's weather rows stand alone: each carries its own place, asks for it the first time
// somebody looks, and three rows for one place are one fetch.
//
// They used to show the place their folder was about, read off the folder's face; faces are gone, and
// a row carries WeatherOptions of its own. What must not change with that is the store's one
// promise — one Entry, one fetch and one cache per place — because three rows that each fetched
// would be three requests a minute apart and three readings that could disagree about their age.
// The list's weather is its conditions alone since the person took the hours and the week out as
// widgets (2026-09-26), so the rows here are three of those.
//
// The store here is a test's own (WeatherStore's download seam): it answers from a function that
// counts what it is asked and never reaches the network, and it keeps no weather.json, so another
// test's cached place can never make a fetch look unnecessary.

using Karpo.Core;
using Karpo.Dock;
using Karpo.Dock.Weather;
using Karpo.Tests.Islands;

namespace Karpo.Tests.Forecasts;

public sealed class WeatherRowsTests
{
    private static readonly ListKind[] Rows = [ListKind.WeatherNow, ListKind.WeatherNow, ListKind.WeatherNow];

    /// <summary>A place no other test uses, so nothing another test left behind can answer for it.</summary>
    private static WeatherOptions Somewhere(double latitude) => new()
    {
        Place = $"Test place {latitude:0.###}",
        Latitude = latitude,
        Longitude = 8.765432,
    };

    private static WeatherPartTile Row(Gfx gfx, ListKind kind, WeatherOptions? place, WeatherStore store)
    {
        var island = kind.New();
        island.Weather = place;
        island.Normalize();
        var row = new WeatherPartTile(Config.IslandKey(null, island.Id), island, gfx, new Config()) { Store = store };
        return row;
    }

    /// <summary>
    /// Three rows for one place, looked at together, are one entry and one fetch; making them asks
    /// nothing at all; and once the answer is in, all three read the very same forecast.
    /// </summary>
    [UiFact]
    public void ThreeRowsForOnePlaceAreOneFetch()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var asked = 0;
            var answer = new TaskCompletionSource<WeatherCache>(TaskCreationOptions.RunContinuationsAsynchronously);
            var store = new WeatherStore((request, cancel) =>
            {
                Interlocked.Increment(ref asked);
                return answer.Task;
            });
            var place = Somewhere(12.345678);
            var rows = Rows.Select(kind => Row(gfx, kind, place, store)).ToList();
            try
            {
                // Made, and nothing asked: a row in a folder nobody opened reaches nothing (§11).
                Assert.Equal(0, store.Places);
                Assert.Equal(0, store.Started);

                // The folder opening is each row's first look: registered, and asked for once. What
                // is counted is the fetch the store started, which it decides on this thread; the
                // download runs on the pool and is waited for below.
                foreach (var row in rows)
                {
                    row.Refresh(gfx);
                }
                Assert.Equal(1, store.Places);
                Assert.Equal(1, store.Started);

                // Looking again while the answer is on its way asks nothing more.
                foreach (var row in rows)
                {
                    row.Tick(gfx);
                }
                Assert.Equal(1, store.Started);
                Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref asked) == 1, Patience.Ceiling), "the one fetch never reached the download");

                var fetched = new WeatherCache { FetchedUtc = DateTime.UtcNow, Latitude = place.Latitude, Longitude = place.Longitude };
                answer.SetResult(fetched);
                Assert.True(
                    SpinWait.SpinUntil(() => rows.All(row => ReferenceEquals(store.Read(row).Shown, fetched)), Patience.Ceiling),
                    "the rows did not all come to read the one answer");
            }
            finally
            {
                foreach (var row in rows)
                {
                    row.Dispose();
                }
            }
            // The last row to go takes the place with it: nothing is fetched for nobody.
            Assert.Equal(0, store.Places);
        });
    }

    /// <summary>Two places are two fetches — the dedupe is by place, not by being rows of one list.</summary>
    [UiFact]
    public void RowsForTwoPlacesAreTwoFetches()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var asked = 0;
            var store = new WeatherStore((request, cancel) =>
            {
                Interlocked.Increment(ref asked);
                return new TaskCompletionSource<WeatherCache>().Task;
            });
            var rows = new[]
            {
                Row(gfx, ListKind.WeatherNow, Somewhere(21.1), store),
                Row(gfx, ListKind.WeatherNow, Somewhere(21.1), store),
                Row(gfx, ListKind.WeatherNow, Somewhere(34.2), store),
            };
            try
            {
                foreach (var row in rows)
                {
                    row.Refresh(gfx);
                }
                Assert.Equal(2, store.Places);
                Assert.Equal(2, store.Started);
                Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref asked) == 2, Patience.Ceiling), "the two fetches never both reached the download");
            }
            finally
            {
                foreach (var row in rows)
                {
                    row.Dispose();
                }
            }
        });
    }

    /// <summary>
    /// A row draws its own place, with no folder around it at all. The same forecast handed to a row
    /// with no place and to a row with one gives "no location yet" and the conditions there — so
    /// what decides is the row's own options, and nothing about where it is drawn.
    /// </summary>
    [UiFact]
    public void ARowShowsItsOwnPlaceWithNoFolderAroundIt()
    {
        Offscreen.OnStaThread(gfx => SavedWeather.AtAFixedTime(() =>
        {
            var style = FolderFlyout.StyleFor(1f, Look(), FlyoutTheme.For(Look()));
            var unused = new WeatherStore((request, cancel) => throw new InvalidOperationException("a row with a source of its own fetched"));

            using var nowhere = Row(gfx, ListKind.WeatherNow, null, unused);
            nowhere.Source = SavedWeather.Source();
            using var zurich = Row(gfx, ListKind.WeatherNow, SavedWeather.Options(), unused);
            zurich.Source = SavedWeather.Source();

            // "Refresh" only where a place is set (§451), as the panel hid its own button before one was;
            // and a reading says its temperature where "no location yet" says none. Both are their size's height (§590).
            Assert.False(nowhere.CanRefresh);
            Assert.True(zurich.CanRefresh);
            var height = zurich.ListHeight(360, style);
            Assert.Equal(height, nowhere.ListHeight(360, style));
            Assert.Contains(zurich.LayOut(360, height, style), piece => piece.Name == "place" && piece.Text == "Zürich");
            Assert.DoesNotContain(nowhere.LayOut(360, height, style), piece => piece.Text == "Zürich");
            // And neither reached the store: a row with a source of its own registers nothing.
            Assert.Equal(0, unused.Places);
        }));
    }

    /// <summary>
    /// §607 (the person, 2026-11-09: "the weather widget should not show the location all the way at the top. it should
    /// display it smaller somewhere else"): the Full line has no header; the place is a caption under the hero — the
    /// Caption's size, under the temperature and what it feels like —, the reading's age after it on its line (§388's
    /// one meta wording), none without a place, and none where the place would no longer read whole beside it.
    /// </summary>
    [UiFact]
    public void ThePlaceIsACaptionUnderTheHeroItsAgeAfterIt()
    {
        Offscreen.OnStaThread(gfx => SavedWeather.AtAFixedTime(() =>
        {
            var style = FolderFlyout.StyleFor(1f, Look(), FlyoutTheme.For(Look()));
            var unused = new WeatherStore((request, cancel) => throw new InvalidOperationException("fetched"));
            using var zurich = Row(gfx, ListKind.WeatherNow, SavedWeather.Options(), unused);
            zurich.Source = SavedWeather.Source();
            using var nowhere = Row(gfx, ListKind.WeatherNow, null, unused);
            nowhere.Source = SavedWeather.Source();

            var height = zurich.ListHeight(360, style);
            var drawn = zurich.LayOut(360, height, style);
            var place = Assert.Single(drawn, piece => piece.Name == PlaceLabel.Piece);
            Assert.Equal("Zürich", place.Text);
            var caption = TilePaint.LineHeight(gfx, style.Text(LineType.CaptionDip));
            Assert.True(MathF.Abs((place.Box.bottom - place.Box.top) - caption) <= 1, $"the place is {place.Box.bottom - place.Box.top} px tall, a caption {caption}");

            // Under everything else the line says: the temperature, the conditions, what it feels like.
            var temperature = Assert.Single(drawn, piece => piece.Name == "hero");
            Assert.All(drawn.Where(piece => piece.Name != PlaceLabel.Piece && piece.Name != WeatherLineTile.UpdatedName && piece.Name != "place dot"),
                piece => Assert.True(piece.Box.bottom <= place.Box.top + 0.5f, $"{piece.Name} \"{piece.Text}\" ends at {piece.Box.bottom}, under the place at {place.Box.top}"));
            Assert.True(temperature.Box.top < place.Box.top);
            Assert.True(place.Box.bottom <= height + 0.5f, $"the place ends at {place.Box.bottom}, the line at {height}");

            // The age after it on its line.
            var age = Assert.Single(drawn, piece => piece.Name == WeatherLineTile.UpdatedName);
            Assert.EndsWith("min ago", age.Text, StringComparison.Ordinal);
            Assert.True(age.Box.left >= place.Box.right, "the age stands after the place");
            Assert.True(MathF.Abs(age.Box.top - place.Box.top) <= 0.5f, "the age is not on the place's line");

            // No place yet: no age either.
            Assert.DoesNotContain(nowhere.LayOut(360, height, style), piece => piece.Name == WeatherLineTile.UpdatedName);

            // Where the place would no longer read whole beside it, the place wins and the age goes.
            var far = SavedWeather.Options();
            far.Place = "Zürich-Oerlikon, Kanton Zürich, Switzerland";
            using var longer = Row(gfx, ListKind.WeatherNow, far, unused);
            longer.Source = SavedWeather.Source();
            var narrow = longer.LayOut(200, height, style);
            Assert.Contains(narrow, piece => piece.Name == PlaceLabel.Piece);
            Assert.DoesNotContain(narrow, piece => piece.Name == WeatherLineTile.UpdatedName);
        }));
    }
    /// <summary>
    /// A click on the weather's conditions searches the weather at its place in the browser (2026-10-11), with
    /// the app's search engine (Config.SearchEngine), Google while none is chosen and in place of a
    /// custom engine with no address; a line with no place is its "Set up in Settings" and searches
    /// nothing.
    /// </summary>
    [UiFact]
    public void AClickSearchesTheWeatherAtItsPlace()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var store = new WeatherStore((_, _) => new TaskCompletionSource<WeatherCache>().Task);
            var opened = new List<string>();
            WeatherLineTile.Opening = opened.Add;
            try
            {
                var place = new WeatherOptions { Place = "Bern", Latitude = 46.948, Longitude = 7.4474 };
                using (var row = Row(gfx, ListKind.WeatherNow, place, store))
                {
                    Assert.Equal(CellAct.Action, ListWidgets.Find(ListKind.WeatherNow)!.Act);
                    // The whole line is the conditions (§379): a click anywhere on it searches.
                    _ = row.Click(0);
                    Assert.Equal(["https://www.google.com/search?q=weather+Bern"], opened);
                }

                var island = ListKind.WeatherNow.New();
                island.Weather = place;
                island.Normalize();
                foreach (var (engine, custom, expected) in new (SearchEngine, string?, string)[]
                {
                    (SearchEngine.DuckDuckGo, null, "https://duckduckgo.com/?q=weather+Bern"),
                    (SearchEngine.Custom, "https://example.com/find?q={q}", "https://example.com/find?q=weather+Bern"),
                    (SearchEngine.Custom, null, "https://www.google.com/search?q=weather+Bern"),
                })
                {
                    opened.Clear();
                    var config = new Config { SearchEngine = engine, SearchCustomUrl = custom };
                    using var row = new WeatherPartTile(Config.IslandKey(null, island.Id), island, gfx, config) { Store = store };
                    _ = row.Click(0);
                    Assert.Equal([expected], opened);
                }

                opened.Clear();
                using (var nowhere = Row(gfx, ListKind.WeatherNow, new WeatherOptions(), store))
                {
                    _ = nowhere.Click(0);
                    Assert.Empty(opened);
                }
            }
            finally
            {
                WeatherLineTile.Opening = null;
            }
        });
    }
    private static Look Look() => new(Glass: false, Dark: true, Accent: Windows.UI.Color.FromArgb(255, 0, 0x78, 0xD4));
}
