// §590: the weather and the air quality split into widgets of fixed content, each in a few sizes (docs\widgets.md §590).
//
// The person, 2026-11-09: "these widgets should be broken down into smaller ones that always show the same amount of
// data ... those widgets then should also not have the size given in rows but just a few different sizes that make
// sense". What holds without a window: the file's words for each kind, style and size, read and written back as they
// are; no Height on any of them; what the readings give the new forms (the next hours past midnight, all six plants);
// and where the daily graph puts a day's high and low in its band. The drawing's own (each size the same data, a
// taller box no more) is WeatherSplitLinesTests'.

using Karpo.Core;
using Karpo.Dock;
using Karpo.Dock.Weather;
using Karpo.Tests.Core;

namespace Karpo.Tests.Forecasts;

public sealed class WeatherSplitTests
{
    /// <summary>A file with one list folder, its lines <paramref name="lines"/> (JSON, comma-separated).</summary>
    private static string File(string lines) => $$"""
        { "version": {{Config.Shape}}, "layouts": [ { "id": "L", "islands": [
          { "id": "f", "kind": "folderList", "section": "end", "folder": { "lists": [ [ {{lines}} ] ] } } ] } ] }
        """;

    private static List<CellItem> Lines(Config config) =>
        [.. config.Layouts.Single().Islands.Single().Folder!.Cells.Select(cell => cell.Island)];

    /// <summary>Every split kind, its style where it has one, and both sizes: the words a file holds and the form each reads as.</summary>
    public static TheoryData<string, string?, string, ListKind> Words => new()
    {
        { "weatherNow", null, "compact", ListKind.WeatherNow },
        { "weatherNow", null, "full", ListKind.WeatherNow },
        { "hourlyForecast", null, "compact", ListKind.WeatherHours },
        { "hourlyForecast", null, "full", ListKind.WeatherHours },
        { "hourlyForecast", "list", "compact", ListKind.WeatherHoursList },
        { "hourlyForecast", "list", "full", ListKind.WeatherHoursList },
        { "dailyForecast", null, "compact", ListKind.WeatherDays },
        { "dailyForecast", null, "full", ListKind.WeatherDays },
        { "dailyForecast", "graph", "compact", ListKind.WeatherDaysGraph },
        { "dailyForecast", "graph", "full", ListKind.WeatherDaysGraph },
        { "airQuality", null, "compact", ListKind.AirQuality },
        { "airQuality", null, "full", ListKind.AirQuality },
        { "airPollutants", null, "compact", ListKind.AirPollutants },
        { "airPollutants", null, "full", ListKind.AirPollutants },
        { "airPollen", null, "compact", ListKind.AirPollen },
        { "airPollen", null, "full", ListKind.AirPollen },
        { "airForecast", null, "compact", ListKind.AirForecast },
        { "airForecast", null, "full", ListKind.AirForecast },
    };

    /// <summary>
    /// A split line is read as its form at its size, keeps no height, and is written back word for word: its kind, its
    /// style where it is not the first, its size, its place.
    /// </summary>
    [Theory]
    [MemberData(nameof(Words))]
    public void EachKindStyleAndSizeIsReadAndWrittenBackAsItIs(string kind, string? style, string size, ListKind form)
    {
        var styleWord = style is null ? "" : $""", "style": "{style}" """;
        var config = Configs.Repaired(File($$"""
            { "island": { "id": "a", "kind": "{{kind}}"{{styleWord}}, "size": "{{size}}", "weather": { "place": "Bern", "latitude": 46.95, "longitude": 7.45 } } }
            """));
        var line = Lines(config).Single();
        Assert.Equal(form, line.List);
        Assert.Equal(size, line.Size!.Value.ToString().ToLowerInvariant());
        Assert.Null(line.Height);
        Assert.Equal("Bern", line.Weather!.Place);
        Assert.Equal((kind, style), (line.FileKind, line.FileStyle));
        var again = Configs.RoundTrip(config, out var changed);
        Assert.False(changed);
        Assert.Equal(config.ToJson(), again.ToJson());
    }

    /// <summary>
    /// A height a file gives a split line is no height of it: the line is its size (where it starts, where none is
    /// given), as every sized line's (§432); a size its kind has not is where it starts. No migration: a height of
    /// rows is not turned into a size, which is the conversion's, once, on the person's own file (§590).
    /// </summary>
    [Fact]
    public void AHeightIsNoneOfASplitLinesAndItsSizeIsWhereItStarts()
    {
        var config = Configs.Repaired(File("""
            { "island": { "id": "a", "kind": "weatherNow", "height": { "fixed": true, "cells": 16 } } },
            { "island": { "id": "b", "kind": "airQuality", "height": { "fixed": false, "cells": 8 } } },
            { "island": { "id": "c", "kind": "hourlyForecast", "size": "medium" } }
            """));
        var lines = Lines(config);
        Assert.All(lines, line => Assert.Null(line.Height));
        Assert.Equal([LineSize.Full, LineSize.Full, LineSize.Compact], lines.Select(line => line.Size!.Value));
    }

    /// <summary>
    /// The words before §590 are no words of this build's: "weather" and its styles "now", "hourly" and "daily" read as
    /// nothing a list holds, dropped and the file written back without them — the person's own file is converted once,
    /// before (artifacts\weather-split\convert-weather-split.py).
    /// </summary>
    [Fact]
    public void TheWeatherOfBeforeIsNoWordAnyMore()
    {
        var config = Configs.Repaired(File("""
            { "island": { "id": "a", "kind": "weather", "height": { "fixed": true, "cells": 8 } } },
            { "island": { "id": "b", "kind": "weather", "style": "hourly" } },
            { "island": { "id": "c", "kind": "clock" } }
            """), out var changed);
        Assert.True(changed);
        Assert.Equal(["c"], Lines(config).Select(line => line.Id));
        Assert.Null(WidgetStyles.Find("weather"));
    }

    /// <summary>
    /// Every split kind has two sizes, the same content at each (§590): Compact smaller than Full, both whole cells
    /// where nothing measures them; the air's and the weather's where a new line starts as the add page places it.
    /// </summary>
    [Fact]
    public void EverySplitKindIsTwoSizesAndNoHeight()
    {
        foreach (var kind in LineSizeTests.Split)
        {
            Assert.Equal([LineSize.Compact, LineSize.Full], kind.Offered());
            Assert.False(kind.HasHeight(), $"{kind} has a Height");
            var line = ListWidgets.Of(kind);
            Assert.True(LineSizes.CellsOf(line, LineSize.Compact) < LineSizes.CellsOf(line, LineSize.Full), $"{kind}: Compact is no smaller than Full");
        }
        Assert.Equal(LineSize.Compact, ListWidgets.Of(ListKind.WeatherHours).Starts);
    }

    /// <summary>
    /// The hourly forecast's List is the next five hours, whatever the clock says: past midnight they go on into
    /// tomorrow, each its own time — never fewer for a late evening, never the axis's "24:00".
    /// </summary>
    [Theory]
    [InlineData(14, 20)]
    [InlineData(22, 55)]
    public void TheNextHoursAreFiveAndGoOnPastMidnight(int hour, int minute)
    {
        var was = Clock.Now;
        var now = new DateTime(2026, 9, 23, hour, minute, 0, DateTimeKind.Local);
        Clock.Now = () => now;
        try
        {
            var reading = WeatherReading.From(SavedWeather.Cache(), SavedWeather.Options(), now.ToUniversalTime(), null);
            Assert.Equal(WeatherReading.NextHours, reading.Next.Count);
            Assert.All(reading.Next, next => Assert.True(next.Hour > reading.NowAt, $"{next.Time} is not after now"));
            Assert.Equal(reading.Next.OrderBy(next => next.Hour).Select(next => next.Hour), reading.Next.Select(next => next.Hour));
            Assert.DoesNotContain(reading.Next, next => next.Time == "24:00");
            Assert.All(reading.Next, next => Assert.True(next.Temperature.Length > 0 && next.Glyph.Length > 0));
            if (hour == 22)
            {
                Assert.Contains(reading.Next, next => next.Hour >= WeatherReading.AxisHours);
            }
        }
        finally
        {
            Clock.Now = was;
        }
    }

    /// <summary>
    /// The pollen line's plants are always the six the forecast has, in one order, 0 where none is in the air — the
    /// same whatever the season —, and none at all where the place has no pollen forecast (outside Europe).
    /// </summary>
    [Fact]
    public void ThePlantsAreAlwaysTheSixInOneOrder()
    {
        var europe = AirReading.From(SavedAir.Cache(european: 46, us: 54, pollen: "12.0"), null, DateTime.UtcNow, null);
        Assert.Equal(
            [S.DockAirPollenGrass, S.DockAirPollenBirch, S.DockAirPollenAlder, S.DockAirPollenRagweed, S.DockAirPollenMugwort, S.DockAirPollenOlive],
            europe.Plants!.Select(plant => plant.Name));
        Assert.Equal(["12", "3", "0", "1", "0", "0"], europe.Plants!.Select(plant => plant.Value));
        // The pollen in the air, for the face's tooltip, is still those above 0, the most first.
        Assert.Equal([S.DockAirPollenGrass, S.DockAirPollenBirch, S.DockAirPollenRagweed], europe.Pollen!.Select(plant => plant.Name));
        var us = AirReading.From(SavedAir.Cache(european: 30, us: 128, europe: false), null, DateTime.UtcNow, null);
        Assert.Null(us.Plants);
    }

    /// <summary>
    /// The daily graph's points: a day's high over its low, the week's highest high a figure's line under the band's
    /// top and its lowest low a figure's line over its foot, so every figure written at its point stays in the band;
    /// a day of one temperature has its two points together; a band too short for its figures puts them all on one line.
    /// </summary>
    [Fact]
    public void TheDailyGraphKeepsEveryFigureInsideItsBand()
    {
        const float top = 100;
        const float bottom = 196;
        const float figure = 18;
        var (highest, _) = WeatherDaysGraphTile.Points(top, bottom, figure, from: 0.4f, to: 1f);
        var (_, lowest) = WeatherDaysGraphTile.Points(top, bottom, figure, from: 0f, to: 0.6f);
        Assert.Equal(top + figure, highest);
        Assert.Equal(bottom - figure, lowest);
        var (high, low) = WeatherDaysGraphTile.Points(top, bottom, figure, from: 0.25f, to: 0.75f);
        Assert.True(high < low, "the high is not over the low");
        Assert.Equal(((top + figure) + (bottom - figure)) / 2, (high + low) / 2, 3);
        var (same, sameLow) = WeatherDaysGraphTile.Points(top, bottom, figure, from: 0.5f, to: 0.5f);
        Assert.Equal(same, sameLow);
        // Past the scale's ends (a rounding), held to them.
        Assert.Equal(WeatherDaysGraphTile.Points(top, bottom, figure, from: 0, to: 1), WeatherDaysGraphTile.Points(top, bottom, figure, from: -0.2f, to: 1.3f));
        var (flat, flatLow) = WeatherDaysGraphTile.Points(top, top + 20, figure, from: 0, to: 1);
        Assert.Equal(flat, flatLow);
        // Compact's band and Full's: the hourly graph's own two, Full half as tall again.
        Assert.Equal(DayGraphBands.Compact * 1.5f, DayGraphBands.Full);
        Assert.Equal(DayGraphBands.Compact, WeatherDaysGraphTile.BandOf(LineSize.Compact));
        Assert.Equal(DayGraphBands.Full, WeatherDaysGraphTile.BandOf(LineSize.Full));
    }

    /// <summary>The two bands of the graphs, as DayGraph has them.</summary>
    private static class DayGraphBands
    {
        public const float Compact = Karpo.Dock.Graphs.DayGraph.BandDip;
        public const float Full = Karpo.Dock.Graphs.DayGraph.TallBandDip;
    }

    /// <summary>
    /// The add page's Weather and news: the weather's ready-made folder and its three widgets, a line, the air's and its
    /// four, a line, then the tides, the earthquakes and the sun — every split kind offered once, the hourly and the
    /// daily forecast as their first style.
    /// </summary>
    [Fact]
    public void TheAddPageOffersEachSplitWidgetOnce()
    {
        var weather = AddTree.Browse.Rows.OfType<AddShelf>().Single(shelf => shelf.Id == "weather");
        Assert.Equal(
            [Presets.WeatherId, "weatherNow", "weatherHours", "weatherDays", "|", Presets.AirQualityId, "airQuality", "airPollutants", "airPollen", "airForecast", "|"],
            weather.Rows.Take(11).Select(row => row is AddLeaf leaf ? leaf.Id : "|"));
        var offered = Catalogue.On(WidgetType.List).Select(entry => entry.List).ToList();
        Assert.Contains(ListKind.WeatherHours, offered);
        Assert.DoesNotContain(ListKind.WeatherHoursList, offered);
        Assert.Contains(ListKind.WeatherDays, offered);
        Assert.DoesNotContain(ListKind.WeatherDaysGraph, offered);
        Assert.Equal(S.CoreStyledHourlyForecastTitle, Catalogue.On(WidgetType.List).Single(entry => entry.List == ListKind.WeatherHours).Title);
        Assert.Equal(S.CoreStyledDailyForecastTitle, Catalogue.On(WidgetType.List).Single(entry => entry.List == ListKind.WeatherDays).Title);
        Assert.Equal(S.CoreWidgetWeatherNowTitle, Catalogue.On(WidgetType.List).Single(entry => entry.List == ListKind.WeatherNow).Title);
    }
}
