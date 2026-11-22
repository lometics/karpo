// quakes: the earthquakes (docs\widgets.md §240) — the USGS's feed read from a real answer, distances
// and bearings on the sphere and their compass words, which earthquakes a widget shows, which are new
// and which a widget says, the store's news across restarts and its wait after failures, and the file:
// a line's settings written where chosen, a hand-edited number read as the nearest choice, a file from
// before read as it was.

using Karpo.Dock;

namespace Karpo.Tests.Forecasts;

public sealed class QuakeTests
{
    private static readonly (double, double) Zurich = (47.37, 8.54);

    // --- The feed ----------------------------------------------------------------------------------

    [Fact]
    public void ARealAnswerIsReadNewestFirstWithoutTheQuarryBlast()
    {
        var quakes = QuakeFeeds.Parse(SavedQuakes.Sample);
        Assert.Equal(["us6000u3fl", "us6000u3de", "us6000u3bs", "us6000u2ww", "us6000u2mq", "us6000u214"], quakes.Select(quake => quake.Id));
        var first = quakes[0];
        Assert.Equal(3.3, first.Magnitude);
        Assert.Equal("23 km SSW of Lovington, New Mexico", first.Place);
        Assert.Equal(new DateTime(2026, 10, 19, 17, 25, 28, 209, DateTimeKind.Utc), first.TimeUtc);
        Assert.Equal(32.759, first.Latitude);
        Assert.Equal(-103.4715, first.Longitude);
        Assert.Equal(6.52, first.Depth);
        Assert.Equal("https://earthquake.usgs.gov/earthquakes/eventpage/us6000u3fl", first.Url);
        // The same event under every network's id.
        Assert.Equal(["us6000u3de", "aka2026usthoq"], quakes[1].Ids);
        Assert.Equal(3, quakes[5].Ids.Length);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("""{"type":"FeatureCollection"}""")]
    public void WhatIsNoFeedIsNoAnswer(string json) =>
        Assert.Throws<InvalidDataException>(() => QuakeFeeds.Parse(json));

    [Fact]
    public void AnEventWithoutAMagnitudeOrAPlaceIsLeftOut()
    {
        const string json = """
            {"type":"FeatureCollection","features":[
              {"id":"a","properties":{"mag":null,"time":1,"type":"earthquake"},"geometry":{"coordinates":[1,2,3]}},
              {"id":"b","properties":{"mag":3,"time":1,"type":"earthquake"},"geometry":null},
              {"id":"c","properties":{"mag":3,"time":1,"type":"explosion"},"geometry":{"coordinates":[1,2,3]}},
              {"id":"d","properties":{"mag":3,"time":1,"url":"https://example.com/x"},"geometry":{"coordinates":[1,2]}},
              null
            ]}
            """;
        var quake = Assert.Single(QuakeFeeds.Parse(json));
        Assert.Equal("d", quake.Id);
        Assert.Null(quake.Depth);
        // Only the USGS's own pages are opened.
        Assert.Equal("https://earthquake.usgs.gov/earthquakes/eventpage/d", quake.Url);
    }

    // --- Where ---------------------------------------------------------------------------------------

    [Fact]
    public void DistancesAndBearingsAreTheSpheres()
    {
        // Big Ben to the Statue of Liberty, some 5,575 km.
        Assert.InRange(QuakeGeo.Distance(51.5007, -0.1246, 40.6892, -74.0445), 5565, 5585);
        Assert.Equal(0, QuakeGeo.Distance(47.37, 8.54, 47.37, 8.54), 6);
        Assert.InRange(QuakeGeo.Distance(0, 0, 0, 180), 20015, 20016);
        Assert.Equal(90, QuakeGeo.Bearing(0, 0, 0, 10), 6);
        Assert.Equal(0, QuakeGeo.Bearing(0, 0, 10, 0), 6);
        Assert.Equal(180, QuakeGeo.Bearing(10, 0, 0, 0), 6);
        Assert.Equal(270, QuakeGeo.Bearing(0, 10, 0, 0), 6);
        // Romania's 4.5 from Zürich: 1,368 km, a little south of east.
        Assert.InRange(QuakeGeo.Distance(47.37, 8.54, 45.481, 26.2168), 1367, 1369);
        Assert.InRange(QuakeGeo.Bearing(47.37, 8.54, 45.481, 26.2168), 92, 93);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(22.4, 0)]
    [InlineData(22.6, 1)]
    [InlineData(90, 2)]
    [InlineData(135, 3)]
    [InlineData(200, 4)]
    [InlineData(225, 5)]
    [InlineData(292.4, 6)]
    [InlineData(315, 7)]
    [InlineData(350, 0)]
    [InlineData(-45, 7)]
    [InlineData(720, 0)]
    public void ABearingIsTheNearestOfEightPoints(double bearing, int point) =>
        Assert.Equal(point, QuakeGeo.Point(bearing));

    [Fact]
    public void ThePointsHaveTheirWords()
    {
        Assert.Equal(["N", "NE", "E", "SE", "S", "SW", "W", "NW"], Enumerable.Range(0, 8).Select(QuakeGeo.Compass));
        var romania = QuakeFeeds.Parse(SavedQuakes.Sample).Single(quake => quake.Id == "us6000u3bs");
        Assert.Equal("1368 km E", QuakeReading.Away(romania, Zurich));
        Assert.Equal("", QuakeReading.Away(romania, null));
        Assert.Equal("M4.5", QuakeReading.Magnitude(4.5));
    }

    [Theory]
    [InlineData(2.5, 0)]
    [InlineData(3.9, 0)]
    [InlineData(4.0, 1)]
    [InlineData(5.5, 2)]
    [InlineData(6.9, 3)]
    [InlineData(7.0, 4)]
    [InlineData(9.1, 5)]
    public void AMagnitudeHasItsClass(double magnitude, int level) =>
        Assert.Equal(level, QuakeLevels.Of(magnitude));

    // --- Which a widget shows -------------------------------------------------------------------------

    [Fact]
    public void AWidgetShowsWhatIsWithinItsRadiusAndOverItsMagnitude()
    {
        var quakes = QuakeFeeds.Parse(SavedQuakes.Sample);
        string[] Shown(QuakeSettings settings, (double, double)? origin) =>
            [.. quakes.Where(quake => QuakeFilter.Shows(quake, settings, origin)).Select(quake => quake.Id)];
        Assert.Equal(["us6000u3bs", "us6000u2ww"], Shown(new(2000, 4, true), Zurich));
        Assert.Equal([], Shown(new(1000, 4, true), Zurich));
        // Greece's 4.6 alone over 4.5.
        Assert.Equal(["us6000u2ww"], Shown(new(2000, 4.6, true), Zurich));
        // The world, wherever the place is, or none is known.
        Assert.Equal(["us6000u2mq", "us6000u214"], Shown(new(QuakeChoices.World, 6, true), null));
        // A radius from nowhere shows nothing.
        Assert.Equal([], Shown(new(2000, 2.5, true), null));
    }

    [Fact]
    public void TheReadingListsTheWeekAndTheFaceTheDay()
    {
        var held = new QuakeHeld { Quakes = QuakeFeeds.Parse(SavedQuakes.Sample), FetchedUtc = SavedQuakes.Generated.AddMinutes(-2) };
        var reading = QuakeReading.From(held, new(2000, 4, true), SavedQuakes.Zurich(), SavedQuakes.Generated);
        Assert.Equal(QuakeState.Ready, reading.State);
        Assert.Equal(["us6000u3bs", "us6000u2ww"], reading.Rows.Select(row => row.Quake.Id));
        Assert.Equal("us6000u3bs", reading.Latest!.Quake.Id);
        Assert.Equal("1368 km E · 8 h ago", reading.Latest.Under);
        Assert.False(reading.Stale);
        // Three days on, the week's are still listed, but the day is quiet.
        var later = QuakeReading.From(held with { FetchedUtc = SavedQuakes.Generated.AddDays(3) }, new(2000, 4, true), SavedQuakes.Zurich(), SavedQuakes.Generated.AddDays(3));
        Assert.Null(later.Latest);
        Assert.Equal(2, later.Rows.Count);
        Assert.Contains("No earthquakes in 24 h", later.Tooltip("Zürich"));
        // This PC's position, off and never known: why, and nothing listed.
        var nowhere = QuakeReading.From(held with { DeviceProblem = "off" }, new(2000, 4, true), WeatherOptions.New(), SavedQuakes.Generated);
        Assert.Equal(QuakeState.Failed, nowhere.State);
        Assert.Equal("off", nowhere.Problem);
        // Nothing fetched yet: waiting; a failure past the last answer: stale.
        Assert.Equal(QuakeState.Waiting, QuakeReading.From(QuakeHeld.None, QuakeSettings.Default, SavedQuakes.Zurich(), SavedQuakes.Generated).State);
        Assert.True(QuakeReading.From(held with { Problem = "unreachable" }, QuakeSettings.Default, SavedQuakes.Zurich(), SavedQuakes.Generated).Stale);
        Assert.True(QuakeReading.From(held, QuakeSettings.Default, SavedQuakes.Zurich(), SavedQuakes.Generated.AddMinutes(20)).Stale);
    }

    // --- Which are new ----------------------------------------------------------------------------------

    private static readonly DateTime Now = SavedQuakes.Generated;

    private static List<Quake> Answer(params (string Id, double Magnitude, double Latitude, double Longitude, DateTime Utc, string? Ids)[] events) =>
        QuakeFeeds.Parse(SavedQuakes.Feed(events));

    [Fact]
    public void TheFirstAnswerIsOnlyLearnt()
    {
        var quakes = Answer(("a", 5, 45, 9, Now.AddMinutes(-5), null));
        Assert.Empty(QuakeNews.Of(null, quakes, Now));
        Assert.Contains("a", QuakeNews.Learn(null, quakes, Now).Keys);
    }

    [Fact]
    public void AnEventIsNewOnceAndOnlyWhileFresh()
    {
        var first = Answer(("a", 5, 45, 9, Now.AddMinutes(-30), null));
        var seen = QuakeNews.Learn(null, first, Now);
        var second = Answer(
            ("a", 5, 45, 9, Now.AddMinutes(-30), null),
            ("b", 4.2, 46, 9, Now.AddMinutes(-10), null),
            // Unseen, but struck four hours ago: a PC that slept wakes to no alarm for it.
            ("old", 6, 46, 9, Now.AddHours(-4), null),
            // Seen under another network's id.
            ("c", 4.5, 46, 9, Now.AddMinutes(-3), ",c,a,"));
        var news = QuakeNews.Of(seen, second, Now);
        Assert.Equal(["b"], news.Select(each => each.Quake.Id));
        Assert.Null(news[0].Before);
        seen = QuakeNews.Learn(seen, second, Now);
        Assert.Empty(QuakeNews.Of(seen, second, Now.AddMinutes(5)));
    }

    [Fact]
    public void AMagnitudeRevisedUpIsNewToAWidgetItNowPasses()
    {
        var seen = QuakeNews.Learn(null, Answer(("a", 3.9, 47, 9, Now.AddMinutes(-20), null)), Now);
        var news = Assert.Single(QuakeNews.Of(seen, Answer(("a", 4.2, 47, 9, Now.AddMinutes(-20), null)), Now));
        Assert.Equal(3.9, news.Before);
        // Said by a widget from 4 up, which never showed it; not by one from 3, which did.
        Assert.True(QuakeNews.Says(news, new(500, 4, true), Zurich));
        Assert.False(QuakeNews.Says(news, new(500, 3, true), Zurich));
        // Not with the alert off, nor out of its radius.
        Assert.False(QuakeNews.Says(news, new(500, 4, false), Zurich));
        Assert.False(QuakeNews.Says(news, new(100, 4, true), (52.52, 13.40)));
        // Revised down: nothing.
        Assert.Empty(QuakeNews.Of(seen, Answer(("a", 3.5, 47, 9, Now.AddMinutes(-20), null)), Now));
    }

    [Fact]
    public void SeenIdsAreForgottenOnlyPastTheFeedsWeek()
    {
        var seen = QuakeNews.Learn(null, Answer(("a", 5, 47, 9, Now.AddDays(-7.5), null)), Now);
        Assert.Contains("a", QuakeNews.Learn(seen, [], Now).Keys);
        Assert.DoesNotContain("a", QuakeNews.Learn(seen, [], Now.AddDays(1)).Keys);
    }

    // --- The store ----------------------------------------------------------------------------------------

    [Fact]
    public async Task NothingIsSaidTwiceAcrossARestart()
    {
        string? file = null;
        var feed = SavedQuakes.Feed(("a", 5, 47, 9, Now.AddMinutes(-10), null));
        QuakeStore Start() => new(_ => Task.FromResult(feed), read: () => file, write: text => file = text, utcNow: () => Now);

        // The first answer ever: learnt, nothing said.
        var first = Start();
        first.Use(this, device: false);
        Assert.True(first.Ask());
        await first.Work!;
        Assert.Empty(first.TakeNews());
        Assert.NotNull(file);

        // A restart, the same answer: nothing; a new event: it alone.
        var second = Start();
        second.Use(this, device: false);
        Assert.Equal("a", Assert.Single(second.Read().Quakes!).Id);
        feed = SavedQuakes.Feed(("a", 5, 47, 9, Now.AddMinutes(-10), null), ("b", 4.4, 47, 9, Now.AddMinutes(-1), null));
        Assert.True(second.Ask());
        await second.Work!;
        Assert.Equal(["b"], second.TakeNews().Select(each => each.Quake.Id));
        Assert.Empty(second.TakeNews());

        // Once more after that: nothing.
        var third = Start();
        third.Use(this, device: false);
        Assert.True(third.Ask());
        await third.Work!;
        Assert.Empty(third.TakeNews());
    }

    [Fact]
    public async Task AFailureKeepsTheLastAnswerAndWaitsLongerEachTime()
    {
        var fail = false;
        var store = new QuakeStore(_ => fail ? Task.FromException<string>(new HttpRequestException("down")) : Task.FromResult(SavedQuakes.Sample));
        store.Use(this, device: false);
        store.Ask();
        await store.Work!;
        fail = true;
        store.Ask();
        await store.Work!;
        var held = store.Read();
        Assert.Equal(6, held.Quakes!.Count);
        Assert.NotNull(held.Problem);
        Assert.Equal(TimeSpan.FromMinutes(5), QuakeStore.WaitAfter(0));
        Assert.Equal(TimeSpan.FromMinutes(5), QuakeStore.WaitAfter(1));
        Assert.Equal(TimeSpan.FromMinutes(10), QuakeStore.WaitAfter(2));
        Assert.Equal(TimeSpan.FromMinutes(20), QuakeStore.WaitAfter(3));
        Assert.Equal(TimeSpan.FromHours(1), QuakeStore.WaitAfter(5));
        Assert.Equal(TimeSpan.FromHours(1), QuakeStore.WaitAfter(50));
    }

    [Fact]
    public async Task NobodyUsingItFetchesNothingAndAPollIsHeldToTheInterval()
    {
        var at = Now;
        var store = new QuakeStore(_ => Task.FromResult(SavedQuakes.Sample), utcNow: () => at);
        store.Poll();
        Assert.Equal(0, store.Started);
        store.Use(this, device: false);
        store.Poll();
        await store.Work!;
        store.Poll();
        Assert.Equal(1, store.Started);
        at = at.AddMinutes(5);
        store.Poll();
        Assert.Equal(2, store.Started);
    }

    [Fact]
    public void AFileThatCannotBeReadIsNone()
    {
        var store = new QuakeStore(_ => Task.FromResult(SavedQuakes.Sample), read: () => "{ not json");
        Assert.Null(store.Read().Quakes);
    }

    // --- The settings and the file --------------------------------------------------------------------------

    [Theory]
    [InlineData(null, 1000)]
    [InlineData(-5, 1000)]
    [InlineData(0, 0)]
    [InlineData(80, 100)]
    [InlineData(300, 250)]
    [InlineData(750, 1000)]
    [InlineData(1400, 1000)]
    [InlineData(1500, 2000)]
    [InlineData(50_000, 2000)]
    public void ARadiusReadsAsTheNearestChoice(int? km, int radius) =>
        Assert.Equal(radius, QuakeChoices.NearestRadius(km));

    [Theory]
    [InlineData(null, 4.0)]
    [InlineData(double.NaN, 4.0)]
    [InlineData(1.0, 2.5)]
    [InlineData(3.4, 3.0)]
    [InlineData(4.25, 4.0)]
    [InlineData(4.4, 4.5)]
    [InlineData(9.0, 7.0)]
    public void AMagnitudeReadsAsTheNearestChoice(double? magnitude, double nearest) =>
        Assert.Equal(nearest, QuakeChoices.NearestMagnitude(magnitude));

    [Fact]
    public void TheWordsOfTheChoicesGoBothWays()
    {
        Assert.Equal(["km100", "km250", "km500", "km1000", "km2000", "world"], QuakeChoices.RadiusWords);
        Assert.Equal(["m25", "m3", "m4", "m45", "m5", "m6", "m7"], QuakeChoices.MagnitudeWords);
        Assert.All(QuakeChoices.Radii, radius => Assert.Equal(radius, QuakeChoices.RadiusOf(QuakeChoices.RadiusWord(radius))));
        Assert.All(QuakeChoices.Magnitudes, magnitude => Assert.Equal(magnitude, QuakeChoices.MagnitudeOf(QuakeChoices.MagnitudeWord(magnitude))));
        Assert.Equal(QuakeChoices.DefaultRadius, QuakeChoices.RadiusOf("km3000"));
        Assert.Equal("1,000 km", QuakeChoices.RadiusName(1000));
        Assert.Equal("4.5+", QuakeChoices.MagnitudeName(4.5));
    }

    [Fact]
    public void ALinesSettingsAreWrittenWhereChosenAndReadBack()
    {
        var island = BarKind.FolderList.New();
        island.Id = "quakes";
        var line = ListKind.Quakes.New();
        line.Id = "line";
        // A new line is for where the device is, at the defaults, which write nothing.
        Assert.True(line.Weather!.UseDeviceLocation);
        Assert.Null(line.Weather.Quakes);
        Assert.Equal(QuakeSettings.Default, QuakeChoices.Of(line.Weather));
        line.Weather = new WeatherOptions { Place = "Zürich", Latitude = 47.37, Longitude = 8.54, Quakes = new QuakeOptions { Radius = 2000, Magnitude = 4.5, Alerts = false } };
        island.Folder!.Put(line);
        var weather = ListKind.WeatherNow.New();
        weather.Id = "weather";
        // Settings on a line that has none are not written.
        weather.Weather!.Quakes = new QuakeOptions { Radius = 100 };
        weather.Normalize();
        Assert.Null(weather.Weather.Quakes);
        island.Folder.Put(weather);
        island.Normalize();
        var config = Karpo.Tests.Core.Configs.With(island);
        var json = config.ToJson();

        var back = Karpo.Tests.Core.Configs.RoundTrip(config, out var changed);
        Assert.False(changed);
        var read = Karpo.Core.Folders.FolderIn(Karpo.Tests.Core.Configs.Islands(back), "quakes")!.Cells.Select(cell => cell.Island).ToList();
        Assert.Equal(new QuakeSettings(2000, 4.5, false), QuakeChoices.Of(read.Single(item => item.List == ListKind.Quakes).Weather));
        Assert.Null(read.Single(item => item.List == ListKind.WeatherNow).Weather!.Quakes);

        // A hand-edited radius reads as the nearest choice, and the default is not written again.
        var edited = Karpo.Tests.Core.Configs.Repaired(json.Replace("\"radius\": 2000", "\"radius\": 900", StringComparison.Ordinal));
        var line2 = Karpo.Core.Folders.FolderIn(Karpo.Tests.Core.Configs.Islands(edited), "quakes")!.Cells.Select(cell => cell.Island).Single(item => item.List == ListKind.Quakes);
        Assert.Equal(new QuakeSettings(1000, 4.5, false), QuakeChoices.Of(line2.Weather));
        Assert.Null(line2.Weather!.Quakes!.Radius);
        Assert.Equal("Zürich", line2.Weather.Place);
    }

    [Fact]
    public void TheFacesSettingsAreItsOptionsAndItsPlaceItsOwn()
    {
        var face = QuakesFace.New();
        Assert.True(face.Weather!.UseDeviceLocation);
        Assert.Equal(QuakeSettings.Default, QuakesFace.SettingsOf(face));
        Assert.Equal(QuakesFace.Standard, QuakesFace.WidthOf(face));
        Assert.Empty(face.Options);

        var island = BarKind.FolderList.New();
        island.Id = "quakes";
        island.Folder!.Face = face;
        face.Set(FolderFaces.Quakes.Option(QuakesFace.Radius)!, "world");
        face.Set(FolderFaces.Quakes.Option(QuakesFace.Magnitude)!, "m6");
        face.Set(FolderFaces.Quakes.Option(QuakesFace.Alerts)!, false);
        face.Weather = new WeatherOptions { Place = "Bern", Latitude = 46.95, Longitude = 7.45 };
        var back = Karpo.Tests.Core.Configs.RoundTrip(Karpo.Tests.Core.Configs.With(island), out var changed);
        Assert.False(changed);
        var read = Karpo.Core.Folders.FolderIn(Karpo.Tests.Core.Configs.Islands(back), "quakes")!;
        Assert.Equal(new QuakeSettings(QuakeChoices.World, 6, false), QuakesFace.SettingsOf(read.Face));
        Assert.Equal("Bern", QuakesFace.PlaceOfFace(read)!.Place);
        // A word from a newer build reads as the default.
        face.Set(FolderFaces.Quakes.Option(QuakesFace.Radius)!, "km5000");
        Assert.Equal(QuakeChoices.DefaultRadius, QuakesFace.SettingsOf(face).Radius);
    }

    [Fact]
    public void AFileFromBeforeReadsAsItWas()
    {
        var island = BarKind.FolderList.New();
        island.Id = "weather";
        island.Folder!.Face = WeatherFace.New();
        var line = ListKind.WeatherNow.New();
        line.Weather = SavedWeather.Options();
        island.Folder.Put(line);
        island.Normalize();
        var json = Karpo.Tests.Core.Configs.With(island).ToJson();
        Assert.DoesNotContain("quakes", json);
        var back = Karpo.Tests.Core.Configs.Repaired(json, out var changed);
        Assert.False(changed);
        Assert.Equal(json, back.ToJson());
    }

    [Fact]
    public void ThePresetIsTheFaceOverTheLineAndItsFirstPlaceIsGivenToBoth()
    {
        var preset = Presets.Find(Presets.QuakesId)!;
        var folder = Presets.Folder(preset, new PresetMachine(false, [])).Folder!;
        Assert.Equal(FolderFaces.QuakesId, folder.Face.Kind);
        var line = Assert.Single(folder.Cells).Island;
        Assert.Equal(ListKind.Quakes, line.List);

        folder.Face.Weather = new WeatherOptions();
        line.Weather = new WeatherOptions { Quakes = new QuakeOptions { Radius = 500 } };
        var chosen = new WeatherOptions { Place = "Bern", Latitude = 46.95, Longitude = 7.45 };
        Assert.Equal(2, WeatherPlaces.Spread(folder, chosen));
        Assert.Equal("Bern", folder.Face.Weather.Place);
        // The line keeps its own radius.
        Assert.Equal("Bern", line.Weather.Place);
        Assert.Equal(500, QuakeChoices.Of(line.Weather).Radius);

        var weather = AddTree.Browse.Rows.OfType<AddShelf>().Single(shelf => shelf.Id == "weather");
        var ids = weather.Rows.OfType<AddLeaf>().Select(leaf => leaf.Id).ToList();
        // §603: after the tides' widgets, the tides now and the tide graph.
        Assert.Equal(ids.IndexOf("tideGraph") + 1, ids.IndexOf(Presets.QuakesId));
        Assert.Equal(ids.IndexOf(Presets.QuakesId) + 1, ids.IndexOf("quakes"));
    }

    [Fact]
    public void ANewQuakeIsSaidByTheFolderThatShowsIt()
    {
        var held = new QuakeHeld();
        var news = new List<QuakeNew> { new(Answer(("a", 4.6, 47.0, 9.0, Now, null))[0], null) };
        var folder = BarKind.FolderList.New().Folder!;
        folder.Face = QuakesFace.New();
        folder.Face.Weather = SavedQuakes.Zurich();
        Assert.True(Karpo.Dock.Dock.SaysQuake(folder, news, held));
        folder.Face.Set(FolderFaces.Quakes.Option(QuakesFace.Alerts)!, false);
        Assert.False(Karpo.Dock.Dock.SaysQuake(folder, news, held));
        var line = ListKind.Quakes.New();
        line.Weather = new WeatherOptions { Latitude = 46, Longitude = 8, Quakes = new QuakeOptions { Magnitude = 5 } };
        folder.Put(line);
        Assert.False(Karpo.Dock.Dock.SaysQuake(folder, news, held));
        line.Weather.Quakes = null;
        Assert.True(Karpo.Dock.Dock.SaysQuake(folder, news, held));
    }
}
