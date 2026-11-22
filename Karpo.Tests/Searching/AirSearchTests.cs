// §614 (2026-11-09: "i should also be able to click on the air quality widget to do a search for it"): a click on any of
// the air's four lines — the index, the pollutants, the pollen, the day — searches the air quality at its place with the
// app's engine, as a weather line's searches the weather and a clock's the time: "air quality <place>", "air quality"
// alone for the device's location. The line forwards and lights whole; the dock opens the search once the folder has gone,
// through Karpo's one launcher. Nothing reaches the browser or the network: the address built is asserted.

using Karpo.Dock;
using Karpo.Tests.Forecasts;
using Karpo.Tests.Islands;

namespace Karpo.Tests.Searching;

public sealed class AirSearchTests
{
    /// <summary>The address: "air quality" and the place, encoded, with the app's engine or Google's where a custom one can't; the device's location the query alone.</summary>
    [Fact]
    public void TheAirQualityIsSearchedForAtItsPlace()
    {
        Assert.Equal("https://www.google.com/search?q=air+quality+Z%C3%BCrich%2C+Switzerland", SearchUrls.Air(new Config(), " Zürich, Switzerland "));
        Assert.Equal("https://www.google.com/search?q=air+quality", SearchUrls.Air(new Config(), ""));
        Assert.Equal("https://www.google.com/search?q=air+quality", SearchUrls.Air(new Config(), "   "));
        Assert.Equal("https://duckduckgo.com/?q=air+quality+Bern", SearchUrls.Air(new Config { SearchEngine = SearchEngine.DuckDuckGo }, "Bern"));
        Assert.Equal("https://www.bing.com/search?q=air+quality+New+York", SearchUrls.Air(new Config { SearchEngine = SearchEngine.Bing }, "New York"));
        Assert.Equal("https://example.com/?q=air+quality+Bern", SearchUrls.Air(new Config { SearchEngine = SearchEngine.Custom, SearchCustomUrl = "https://example.com/?q={q}" }, "Bern"));
        Assert.Equal("https://www.google.com/search?q=air+quality+Bern", SearchUrls.Air(new Config { SearchEngine = SearchEngine.Custom, SearchCustomUrl = "not an address" }, "Bern"));
    }

    /// <summary>Every one of the air's four acts on its click (CellAct.Action), so the folder forwards it.</summary>
    [Fact]
    public void EachOfTheAirsFourActs()
    {
        foreach (var kind in new[] { ListKind.AirQuality, ListKind.AirPollutants, ListKind.AirPollen, ListKind.AirForecast })
        {
            Assert.Equal(CellAct.Action, ListWidgets.Of(kind).Act);
        }
    }

    /// <summary>
    /// Each of the air's four lines at a place: a click asks the folder to go and forwards, the line lights whole, and the
    /// dock's Open then searches, once. A line with no place is its "Set up": its click chooses nothing and opens nothing.
    /// </summary>
    [UiFact]
    public void AnAirLinesClickSearchesItsPlaceOnceTheFolderHasGone()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var config = new Config { SearchEngine = SearchEngine.DuckDuckGo };
            foreach (var kind in new[] { ListKind.AirQuality, ListKind.AirPollutants, ListKind.AirPollen, ListKind.AirForecast })
            {
                foreach (var placed in new[] { true, false })
                {
                    var item = kind.New();
                    item.Id = kind.Id();
                    item.Weather = placed ? SavedWeather.Options() : new WeatherOptions();
                    var cell = new FolderCell { Island = item };
                    var opened = new List<string>();
                    using var line = (AirQualityTile)Karpo.Dock.Islands.CreateLine(Config.IslandKey("layout:default", item.Id), item, kind, gfx, config)!;
                    line.Source = SavedAir.Source(SavedAir.Cache());
                    line.Opened = opened.Add;
                    var action = line.Click(0);
                    if (!placed)
                    {
                        Assert.NotNull(line.Unset);
                        Assert.Equal(IslandView.None, action.View);
                        line.Open();
                        Assert.Empty(opened);
                        continue;
                    }
                    Assert.Equal(IslandView.App, action.View);
                    Assert.True(FolderFlyout.Forwards(action, FolderFlyout.ActOf(cell, line)), $"{kind}");
                    Assert.True(FolderFlyout.LightsWhole(cell, line), $"{kind}: the whole line is the one thing to press");
                    Assert.Empty(opened);
                    line.Open();
                    line.Open();
                    Assert.Equal(["https://duckduckgo.com/?q=air+quality+Z%C3%BCrich%2C+Switzerland"], opened);
                }
            }
        });
    }
}
