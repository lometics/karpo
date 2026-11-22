// air: the air quality (docs\widgets.md §210) — Open-Meteo's answer read, the two indices' bands and
// colours at their edges, which index a place gets, the face's words by their room, the reading's
// age and staleness, the store's one fetch per place, and the file: an index chosen on a line and on
// a face written and read back, nothing written where nobody chose, and a file from before read as it
// was.

using Karpo.Dock;
using Karpo.Dock.Weather;

namespace Karpo.Tests.Forecasts;

public sealed class AirQualityTests
{
    // --- The answer --------------------------------------------------------------------------------

    [Fact]
    public void AnAnswerIsReadWithItsPollutantsPollenAndHours()
    {
        SavedWeather.AtAFixedTime(() =>
        {
            var answer = AirAnswers.Parse(SavedAir.Answer(european: 23, us: 54));
            Assert.Equal(23, answer.Current!.EuropeanAqi);
            Assert.Equal(54, answer.Current.UsAqi);
            Assert.Equal(8.6, answer.Current.Pm25);
            Assert.Equal(61.4, answer.Current.Ozone);
            Assert.Equal(3.4, answer.Current.BirchPollen);
            Assert.Equal(48, answer.Hourly!.Time!.Length);
            Assert.Equal(7200, answer.UtcOffsetSeconds);

            var american = AirAnswers.Parse(SavedAir.Answer(europe: false));
            Assert.Null(american.Current!.GrassPollen);
            Assert.Null(AirReading.PollenOf(american.Current));
            // Europe in October: a pollen forecast with nothing in the air is no pollen, not no forecast.
            Assert.Equal(["Birch", "Ragweed"], AirReading.PollenOf(answer.Current)!.Select(pollen => pollen.Name));
        });
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("""{ "current": { "time": "2026-10-18T14:00", "pm2_5": 3.1 } }""")]
    [InlineData("""{ "current": { "european_aqi": "x" } }""")]
    public void AnAnswerWithoutAnIndexIsNoAnswer(string json) =>
        Assert.Throws<InvalidDataException>(() => AirAnswers.Parse(json));

    [Fact]
    public void OneIndexIsEnough()
    {
        var answer = AirAnswers.Parse("""{ "current": { "us_aqi": 42 } }""");
        Assert.Equal(42, answer.Current!.UsAqi);
        Assert.Null(answer.Current.EuropeanAqi);
    }

    [Fact]
    public void TheRequestAsksForBothIndicesAtAKilometre()
    {
        var url = AirAnswers.Url(47.376_88, 8.541_69);
        Assert.StartsWith("https://air-quality-api.open-meteo.com/v1/air-quality?latitude=47.38&longitude=8.54&", url);
        Assert.Contains("current=european_aqi,us_aqi,pm2_5,pm10,ozone,nitrogen_dioxide", url);
        Assert.Contains("grass_pollen", url);
        Assert.Contains("&hourly=european_aqi,us_aqi", url);
        Assert.Contains("timezone=auto", url);
    }

    // --- The bands ---------------------------------------------------------------------------------

    [Theory]
    [InlineData(0, 0)]
    [InlineData(20, 0)]
    [InlineData(20.4, 0)]
    [InlineData(20.5, 1)]
    [InlineData(40, 1)]
    [InlineData(41, 2)]
    [InlineData(60, 2)]
    [InlineData(61, 3)]
    [InlineData(80, 3)]
    [InlineData(81, 4)]
    [InlineData(100, 4)]
    [InlineData(101, 5)]
    [InlineData(250, 5)]
    [InlineData(-3, 0)]
    public void TheEuropeanIndexHasTheAgencysBands(double value, int level) =>
        Assert.Equal(level, AirBands.Level(AirScale.European, value));

    [Theory]
    [InlineData(0, 0)]
    [InlineData(50, 0)]
    [InlineData(50.4, 0)]
    [InlineData(50.5, 1)]
    [InlineData(51, 1)]
    [InlineData(100, 1)]
    [InlineData(101, 2)]
    [InlineData(150, 2)]
    [InlineData(151, 3)]
    [InlineData(200, 3)]
    [InlineData(201, 4)]
    [InlineData(300, 4)]
    [InlineData(301, 5)]
    [InlineData(500, 5)]
    public void TheUsIndexHasTheEpasBands(double value, int level) =>
        Assert.Equal(level, AirBands.Level(AirScale.Us, value));

    [Fact]
    public void EachLevelHasItsAgencysColourAndAWord()
    {
        Assert.Equal([0x50F0E6u, 0x50CCAA, 0xF0E641, 0xFF5050, 0x960032, 0x7D2181], Enumerable.Range(0, 6).Select(level => AirBands.Colour(AirScale.European, level)));
        Assert.Equal([0x00E400u, 0xFFFF00, 0xFF7E00, 0xFF0000, 0x8F3F97, 0x7E0023], Enumerable.Range(0, 6).Select(level => AirBands.Colour(AirScale.Us, level)));
        Assert.Equal(["Good", "Fair", "Moderate", "Poor", "Very poor", "Extremely poor"], Enumerable.Range(0, 6).Select(level => AirBands.Word(AirScale.European, level)));
        Assert.Equal(["Good", "Moderate", "Unhealthy for sensitive groups", "Unhealthy", "Very unhealthy", "Hazardous"], Enumerable.Range(0, 6).Select(level => AirBands.Word(AirScale.Us, level)));
        // The bar's word for the long one; every other level as it is.
        Assert.Equal("Sensitive groups", AirBands.Short(AirScale.Us, 2));
        Assert.Equal("Very poor", AirBands.Short(AirScale.European, 4));
        // Past the end, the last.
        Assert.Equal(AirBands.Colour(AirScale.Us, 5), AirBands.Colour(AirScale.Us, 9));
    }

    [Theory]
    [InlineData(null, 47.37, 8.54, true)] // Zürich
    [InlineData(null, 64.15, -21.94, true)] // Reykjavík
    [InlineData(null, 55.75, 37.62, true)] // Moscow
    [InlineData(null, 40.71, -74.01, false)] // New York
    [InlineData(null, 28.61, 77.21, false)] // Delhi
    [InlineData(null, 35.68, 139.69, false)] // Tokyo
    [InlineData(null, -33.87, 151.21, false)] // Sydney
    [InlineData(null, null, null, false)]
    [InlineData("auto", 47.37, 8.54, true)]
    [InlineData("uk", 47.37, 8.54, true)]
    [InlineData("us", 47.37, 8.54, false)]
    [InlineData("european", 40.71, -74.01, true)]
    public void TheIndexIsTheChosenOneElseThePlacesRegions(string? chosen, double? latitude, double? longitude, bool european) =>
        Assert.Equal(european ? AirScale.European : AirScale.Us, AirBands.ScaleOf(chosen, latitude, longitude));

    // --- The face's words --------------------------------------------------------------------------

    [Fact]
    public void TheFaceSaysAsMuchAsItsWidthAndItsRoomLet()
    {
        Assert.Equal(["AQI 32 · Good", "32 · Good", "AQI 32", "32"], AirText.Forms("32", "Good", AirQualityFace.Wide));
        Assert.Equal(["32 · Good", "32"], AirText.Forms("32", "Good", AirQualityFace.Standard));
        Assert.Equal(["32"], AirText.Forms("32", "Good", AirQualityFace.Compact));
        // A word from a newer build is the standard width.
        Assert.Equal(["32 · Good", "32"], AirText.Forms("32", "Good", "hologram"));

        static float Letters(string text) => text.Length;
        var wide = AirText.Forms("32", "Good", AirQualityFace.Wide);
        Assert.Equal("AQI 32 · Good", AirText.Fit(wide, 40, Letters));
        Assert.Equal("AQI 32 · Good", AirText.Fit(wide, 13, Letters));
        Assert.Equal("32 · Good", AirText.Fit(wide, 12, Letters));
        Assert.Equal("AQI 32", AirText.Fit(wide, 6, Letters));
        Assert.Equal("32", AirText.Fit(wide, 5, Letters));
        // Never nothing: the value where nothing fits.
        Assert.Equal("32", AirText.Fit(wide, 0, Letters));
    }

    [Fact]
    public void AFacesRoomHoldsTheWidestWordOfItsIndexes()
    {
        Assert.Contains("Extremely poor", AirBands.ShortWords(AirBands.ScalesFor(AirIndexes.European)));
        Assert.DoesNotContain("Hazardous", AirBands.ShortWords(AirBands.ScalesFor(AirIndexes.European)));
        Assert.Contains("Hazardous", AirBands.ShortWords(AirBands.ScalesFor(null)));
        Assert.Contains("Extremely poor", AirBands.ShortWords(AirBands.ScalesFor("auto")));
        Assert.DoesNotContain("Unhealthy for sensitive groups", AirBands.ShortWords(AirBands.ScalesFor(AirIndexes.Us)));
    }

    // --- The reading, its age and staleness --------------------------------------------------------

    [Fact]
    public void AReadingSaysTheIndexItsLevelAndThePlacesDay()
    {
        SavedWeather.AtAFixedTime(() =>
        {
            var now = Clock.Now().ToUniversalTime();
            var reading = AirReading.From(SavedAir.Cache(european: 64.4, us: 112), null, now, null);
            Assert.Equal(AirState.Ready, reading.State);
            Assert.Equal(AirScale.European, reading.Scale);
            Assert.Equal(64, reading.Value);
            Assert.Equal(3, reading.Level);
            Assert.Equal("Poor", reading.Word);
            Assert.Equal("European AQI", reading.Name);
            Assert.Equal(["PM2.5", "PM10", "O₃", "NO₂"], reading.Pollutants.Select(pollutant => pollutant.Name));
            Assert.Equal("9", reading.Pollutants[0].Value);
            Assert.Equal("µg/m³", reading.Pollutants[0].Unit);
            Assert.False(reading.Stale);
            // The place's day, midnight to midnight; now its present hour: 14:20 here is 14:00 in Zürich (two hours ahead
            // of UTC, as the answer says).
            Assert.Equal(AirReading.DayHours, reading.Hours.Count);
            var placeNow = now.AddSeconds(SavedWeather.OffsetSeconds);
            Assert.Equal(placeNow.Hour, reading.Now);
            Assert.Equal(placeNow.Date, reading.Hours[0].At);

            var us = AirReading.From(SavedAir.Cache(european: 64.4, us: 112), AirIndexes.Us, now, null);
            Assert.Equal((AirScale.Us, 112, 2, "Unhealthy for sensitive groups", "Sensitive groups"), (us.Scale, us.Value, us.Level, us.Word, us.Short));

            var american = AirReading.From(SavedAir.Cache(us: 27, europe: false), null, now, null);
            Assert.Equal(AirScale.Us, american.Scale);
            Assert.Null(american.Pollen);

            // An answer fetched a day ago lists the next day, its last; one of two days ago no day of today at all.
            var old = AirReading.From(SavedAir.Cache(), null, now.AddHours(24), null);
            Assert.Equal(AirReading.DayHours, old.Hours.Count);
            Assert.All(old.Hours, hour => Assert.Equal(placeNow.Date.AddDays(1), hour.At.Date));
            var older = AirReading.From(SavedAir.Cache(), null, now.AddHours(48), null);
            Assert.Empty(older.Hours);
            Assert.Equal(AirReading.NoNow, older.Now);
        });
    }

    /// <summary>
    /// §501, the person (2026-11-04): "the graph on the air quality widget goes from right to left in time but should go
    /// from left to right in time". The bars were the next 24 hours from now, so each hour moved a bar to the left every
    /// hour: the day ran right to left. Now they are the place's day on one axis, midnight first, the hours rising to the
    /// right; every hour keeps its place through the day, and now moves on to the right — morning, noon and night, in the
    /// place's time and not this PC's (New York's answer, four hours behind UTC, read in Zürich).
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheDayRunsLeftToRightAndNowMovesOnToTheRight(bool europe)
    {
        SavedWeather.AtAFixedTime(() =>
        {
            var utc = Clock.Now().ToUniversalTime();
            var offset = europe ? SavedWeather.OffsetSeconds : -14400;
            var cache = SavedAir.Cache(europe: europe);
            var midnight = utc.AddSeconds(offset).Date;
            var nows = new List<int>();
            IReadOnlyList<AirHour>? first = null;
            // Through the place's day, from a little after its midnight to a little before the next.
            foreach (var hours in new[] { 0.2, 6.5, 13.0, 18.75, 23.9 })
            {
                var at = midnight.AddHours(hours).AddSeconds(-offset);
                var reading = AirReading.From(cache, null, at, null);
                Assert.Equal(AirReading.DayHours, reading.Hours.Count);
                for (var index = 0; index < reading.Hours.Count; index++)
                {
                    Assert.Equal(midnight.AddHours(index), reading.Hours[index].At);
                }
                first ??= reading.Hours;
                Assert.Equal(first, reading.Hours);
                nows.Add(reading.Now);
            }
            Assert.Equal([0, 6, 13, 18, 23], nows);
        });
    }

    [Fact]
    public void AReadingGoesStaleAsTheWeathersDoes()
    {
        SavedWeather.AtAFixedTime(() =>
        {
            var now = Clock.Now().ToUniversalTime();
            var every = WeatherReading.Refresh();
            Assert.False(AirReading.From(SavedAir.Cache(fetchedUtc: now.AddMinutes(-every)), null, now, null).Stale);
            var late = AirReading.From(SavedAir.Cache(fetchedUtc: now.AddMinutes(-3 * every)), null, now, null);
            Assert.True(late.Stale);
            Assert.Equal(AirState.Ready, late.State);
            Assert.NotEqual("", late.Updated);

            // A failure with an answer standing: the answer, stale, and why.
            var failed = AirReading.From(SavedAir.Cache(), null, now, "Open-Meteo can't be reached");
            Assert.Equal(AirState.Ready, failed.State);
            Assert.True(failed.Stale);
            Assert.Equal("Open-Meteo can't be reached", failed.Problem);

            // Nothing yet: waiting, never stale; a failure with nothing: failed, with its words.
            var waiting = AirReading.From(null, null, now, null);
            Assert.Equal(AirState.Waiting, waiting.State);
            Assert.False(waiting.Stale);
            Assert.Equal("", waiting.Updated);
            Assert.Equal(AirState.Failed, AirReading.From(null, null, now, "Open-Meteo didn't answer").State);

            var tip = AirReading.From(SavedAir.Cache(european: 23), null, now, null).Tooltip("Zürich, Switzerland");
            Assert.StartsWith("Zürich, Switzerland\nEuropean AQI: 23, Fair\nPM2.5 9 µg/m³ · PM10 14 µg/m³ · O₃ 61 µg/m³ · NO₂ 12 µg/m³\nPollen: Birch 3", tip);
            Assert.EndsWith("15 min ago", tip); // §388: one meta wording
        });
    }

    [Fact]
    public void AnUnreadableAnswerIsSaidAsTheAirsAndTheRestAsTheWeathers()
    {
        Assert.Equal("The air quality couldn't be read", AirQualityStore.Explain(new InvalidDataException()));
        Assert.Equal(WeatherStore.Explain(new HttpRequestException()), AirQualityStore.Explain(new HttpRequestException()));
    }

    // --- The store ---------------------------------------------------------------------------------

    [Fact]
    public async Task OnePlaceIsOneFetchHoweverManyShowItAndRefreshAsksAgain()
    {
        var asked = 0;
        var store = new AirQualityStore((_, _) =>
        {
            Interlocked.Increment(ref asked);
            return Task.FromResult(SavedAir.Cache());
        });
        object face = new(), line = new(), elsewhere = new();
        var zurich = SavedWeather.Options();
        store.Use(face, zurich);
        store.Use(line, new WeatherOptions { Place = "Zürich", Latitude = 47.37, Longitude = 8.54, AirIndex = AirIndexes.Us });
        store.Poll(face);
        store.Poll(line);
        // Over, not just answered: the answer is in before the fetch's task has finished, and Ask refuses while it runs.
        await Until(() => store.Read(line) is { Shown: not null, Fetching: false });
        Assert.Equal(1, asked);
        Assert.Same(store.Read(face).Shown, store.Read(line).Shown);

        Assert.True(store.Ask(line));
        await Until(() => !store.Read(line).Fetching);
        Assert.Equal(2, asked);

        // Another place is another fetch; one nobody shows any more is let go.
        store.Use(elsewhere, new WeatherOptions { Latitude = 40.71, Longitude = -74.01 });
        store.Poll(elsewhere);
        await Until(() => store.Read(elsewhere).Shown is not null);
        Assert.Equal(3, asked);
        store.Release(elsewhere);
        Assert.Equal((null, null, false), store.Read(elsewhere));

        // Nothing set: nothing asked.
        var nowhere = new object();
        store.Use(nowhere, new WeatherOptions());
        store.Poll(nowhere);
        Assert.Equal(3, store.Started);
    }

    [Fact]
    public async Task AFailureKeepsTheLastAnswerAndSaysWhy()
    {
        var fail = false;
        var store = new AirQualityStore((_, _) => fail ? Task.FromException<AirCache>(new HttpRequestException()) : Task.FromResult(SavedAir.Cache()));
        var line = new object();
        store.Use(line, SavedWeather.Options());
        store.Poll(line);
        await Until(() => store.Read(line) is { Shown: not null, Fetching: false });
        fail = true;
        Assert.True(store.Ask(line));
        await Until(() => store.Read(line).Problem is not null);
        var (shown, problem, _) = store.Read(line);
        Assert.NotNull(shown);
        Assert.Equal(WeatherStore.Explain(new HttpRequestException()), problem);
    }

    private static async Task Until(Func<bool> done)
    {
        for (var tries = 0; tries < 200 && !done(); tries++)
        {
            await Task.Delay(10);
        }
        Assert.True(done());
    }

    // --- The file ----------------------------------------------------------------------------------

    [Fact]
    public void ALinesIndexIsWrittenWhereChosenAndReadBack()
    {
        var island = BarKind.FolderList.New();
        island.Id = "air";
        var line = ListKind.AirQuality.New();
        line.Id = "line";
        // A new line is for where the device is, as the weather's, and the region's index.
        Assert.True(line.Weather!.UseDeviceLocation);
        Assert.Null(line.Weather.AirIndex);
        line.Weather = new WeatherOptions { Place = "Zürich", Latitude = 47.37, Longitude = 8.54, AirIndex = AirIndexes.Us };
        island.Folder!.Put(line);
        var weather = ListKind.WeatherNow.New();
        weather.Id = "weather";
        // An index on a line that shows none is not written.
        weather.Weather!.AirIndex = AirIndexes.European;
        weather.Normalize();
        Assert.Null(weather.Weather.AirIndex);
        island.Folder.Put(weather);
        island.Normalize();
        var config = Karpo.Tests.Core.Configs.With(island);
        var json = config.ToJson();
        Assert.Contains("\"airIndex\": \"us\"", json);
        Assert.DoesNotContain("\"airIndex\": \"european\"", json);

        var back = Karpo.Tests.Core.Configs.RoundTrip(config, out var changed);
        Assert.False(changed);
        var folder = Karpo.Core.Folders.FolderIn(Karpo.Tests.Core.Configs.Islands(back), "air")!;
        var read = folder.Cells.Select(cell => cell.Island).ToList();
        Assert.Equal(AirIndexes.Us, read.Single(item => item.List == ListKind.AirQuality).Weather!.AirIndex);
        Assert.Null(read.Single(item => item.List == ListKind.WeatherNow).Weather!.AirIndex);
    }

    [Fact]
    public void AnIndexFromANewerBuildIsKeptAndReadAsTheRegions()
    {
        var line = ListKind.AirQuality.New();
        line.Weather!.AirIndex = "china";
        line.Normalize();
        Assert.Equal("china", line.Weather.AirIndex);
        Assert.Null(WeatherPlaces.Settled(line.Weather).AirIndex);
        Assert.Equal(AirScale.Us, AirBands.ScaleOf(line.Weather.AirIndex, 40.7, -74));
    }

    [Fact]
    public void TheFacesIndexAndWidthAreItsOptionsAndItsPlaceItsOwn()
    {
        var face = AirQualityFace.New();
        Assert.True(face.Weather!.UseDeviceLocation);
        Assert.Equal(AirIndexes.Auto, AirQualityFace.IndexOf(face));
        Assert.Equal(AirQualityFace.Standard, AirQualityFace.WidthOf(face));
        Assert.Empty(face.Options);

        var island = BarKind.FolderList.New();
        island.Id = "air";
        island.Folder!.Face = face;
        face.Set(FolderFaces.AirQuality.Option(AirQualityFace.Index)!, AirIndexes.European);
        face.Set(FolderFaces.AirQuality.Option(AirQualityFace.Width)!, AirQualityFace.Wide);
        face.Weather = new WeatherOptions { Place = "Bern", Latitude = 46.95, Longitude = 7.45 };
        var back = Karpo.Tests.Core.Configs.RoundTrip(Karpo.Tests.Core.Configs.With(island), out var changed);
        Assert.False(changed);
        var read = Karpo.Core.Folders.FolderIn(Karpo.Tests.Core.Configs.Islands(back), "air")!;
        Assert.Equal(AirIndexes.European, AirQualityFace.IndexOf(read.Face));
        Assert.Equal(AirQualityFace.Wide, AirQualityFace.WidthOf(read.Face));
        Assert.Equal("Bern", AirIndexes.PlaceOfFace(read)!.Place);
        Assert.Null(WeatherPlaces.OfFace(read));

        // A word from a newer build is kept and read as the default.
        face.Set(FolderFaces.AirQuality.Option(AirQualityFace.Index)!, "china");
        Assert.Equal(AirIndexes.Auto, AirQualityFace.IndexOf(face));
    }

    /// <summary>
    /// §590: the preset is the face over the air's four lines — the index Full, the pollutants and the pollen Compact, the
    /// day Full —, and the first place chosen is given to the face and every line, each keeping its own index.
    /// </summary>
    [Fact]
    public void ThePresetIsTheFaceOverTheLinesAndItsFirstPlaceIsGivenToAll()
    {
        var preset = Presets.Find(Presets.AirQualityId)!;
        var folder = Presets.Folder(preset, new PresetMachine(false, [])).Folder!;
        Assert.Equal(FolderFaces.AirQualityId, folder.Face.Kind);
        var lines = folder.Cells.Select(cell => cell.Island).ToList();
        Assert.Equal([ListKind.AirQuality, ListKind.AirPollutants, ListKind.AirPollen, ListKind.AirForecast], lines.Select(line => line.List!.Value));
        Assert.Equal([LineSize.Full, LineSize.Compact, LineSize.Compact, LineSize.Full], lines.Select(LineSizes.Of));
        var line = lines[0];

        folder.Face.Weather = new WeatherOptions();
        line.Weather = new WeatherOptions { AirIndex = AirIndexes.Us };
        foreach (var other in lines.Skip(1))
        {
            other.Weather = new WeatherOptions();
        }
        var chosen = new WeatherOptions { Place = "Bern", Latitude = 46.95, Longitude = 7.45, AirIndex = AirIndexes.European };
        Assert.Equal(5, WeatherPlaces.Spread(folder, chosen));
        Assert.All(lines, each => Assert.Equal("Bern", each.Weather!.Place));
        Assert.Equal("Bern", folder.Face.Weather.Place);
        Assert.Null(folder.Face.Weather.AirIndex);
        // The line keeps its own index.
        Assert.Equal("Bern", line.Weather.Place);
        Assert.Equal(AirIndexes.Us, line.Weather.AirIndex);
    }

    [Fact]
    public void AFileFromBeforeReadsAsItWas()
    {
        // A weather folder written before the air quality: nothing in it changes, nothing is added.
        var island = BarKind.FolderList.New();
        island.Id = "weather";
        island.Folder!.Face = WeatherFace.New();
        var line = ListKind.WeatherNow.New();
        line.Weather = SavedWeather.Options();
        island.Folder.Put(line);
        island.Normalize();
        var json = Karpo.Tests.Core.Configs.With(island).ToJson();
        Assert.DoesNotContain("airIndex", json);
        var back = Karpo.Tests.Core.Configs.Repaired(json, out var changed);
        Assert.False(changed);
        Assert.Equal(json, back.ToJson());
    }

    [Fact]
    public void TheWidgetIsInTheWeatherPageBesideTheWeather()
    {
        Assert.Equal(WidgetCategory.Weather, WidgetInfos.AirQuality.Category);
        Assert.Equal(WidgetOptionsKind.Weather, WidgetInfos.AirQuality.Options);
        var weather = AddTree.Browse.Rows.OfType<AddShelf>().Single(shelf => shelf.Id == "weather");
        var ids = weather.Rows.OfType<AddLeaf>().Select(leaf => leaf.Id).ToList();
        // §590: after the weather's three, the air's ready-made folder and its four.
        Assert.Equal(ids.IndexOf("weatherDays") + 1, ids.IndexOf(Presets.AirQualityId));
        Assert.Equal(["airQuality", "airPollutants", "airPollen", "airForecast"], ids.Skip(ids.IndexOf(Presets.AirQualityId) + 1).Take(4));
    }
}
