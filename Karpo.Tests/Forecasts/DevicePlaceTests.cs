// §253: a widget for the device's location says where the device is — "Zürich, Zurich, Switzerland",
// as a place picked says it — not "Current location", which stands only until that is known or
// where it can't be found. The service's answer read (fixtures shaped as BigDataCloud's), a position
// named once and kept, a failure left as "Current location", the names kept in weather.json, the
// one rule every place-based widget and the menu's place row read, and the lines drawn
// (device-place-<kind>.png, dark, 150 %, to KARPO_PNG_DIR; UI tests only).
//
// What the device is called is one value for the whole dock (PlaceNames.Device), so these run on
// their own, after the rest, and leave it unknown.

using System.Text.Json;
using Karpo.Core;
using Karpo.Dock;
using Karpo.Dock.Weather;
using Karpo.Tests.Folders;
using Karpo.Tests.Islands;
using static Karpo.Tests.Islands.Offscreen;

namespace Karpo.Tests.Forecasts;

[CollectionDefinition(Collection, DisableParallelization = true)]
public sealed class DevicePlace
{
    public const string Collection = "the device's place";
}

[Collection(DevicePlace.Collection)]
public sealed class DevicePlaceTests : IDisposable
{
    private const string Zurich = "Zürich, Zurich, Switzerland";

    public DevicePlaceTests() => PlaceNames.Forget();

    public void Dispose() => PlaceNames.Forget();

    /// <summary>The service's answer for the middle of Zürich, in German, cut to what is read and a little around it.</summary>
    private const string ZurichGerman = """
        {
          "latitude": 47.37, "lookupSource": "coordinates", "longitude": 8.54, "localityLanguageRequested": "de",
          "continent": "Europa", "continentCode": "EU", "countryName": "Schweiz", "countryCode": "CH",
          "principalSubdivision": "Kanton Zürich", "principalSubdivisionCode": "CH-ZH",
          "city": "Zürich", "locality": "Lindenhof", "postcode": "", "plusCode": "8FVC9GCR+22",
          "localityInfo": { "administrative": [ { "name": "Schweiz", "order": 3, "adminLevel": 2 } ], "informative": [] }
        }
        """;

    /// <summary>Out at sea: no city, no country, the sea's name as its locality.</summary>
    private const string Atlantic = """
        { "latitude": 36.0, "longitude": -40.0, "countryName": "", "principalSubdivision": "", "city": "", "locality": "Atlantic Ocean" }
        """;

    private static WeatherOptions Device(string place = "") => new() { UseDeviceLocation = true, Place = place };

    [Fact]
    public void TheServicesAnswerIsWrittenAsAPickedPlaceIs()
    {
        Assert.Equal("Zürich, Kanton Zürich, Schweiz", DeviceNames.Parse(ZurichGerman));
        Assert.Equal("Atlantic Ocean", DeviceNames.Parse(Atlantic));
        // No city: the locality; a region called as the city is not said twice.
        Assert.Equal("Lindenhof, Schweiz", DeviceNames.Parse("""{ "city": "", "locality": "Lindenhof", "principalSubdivision": "", "countryName": "Schweiz" }"""));
        Assert.Equal("Genève, Suisse", DeviceNames.Parse("""{ "city": "Genève", "principalSubdivision": "Genève", "countryName": "Suisse" }"""));
        // Nothing named: no name, the placeholder stays.
        Assert.Null(DeviceNames.Parse("""{ "city": "", "locality": "", "countryName": "" }"""));
        Assert.Null(DeviceNames.Parse("""{ "city": null, "locality": 3 }"""));
        Assert.ThrowsAny<JsonException>(() => DeviceNames.Parse("<html>"));
        Assert.Throws<InvalidDataException>(() => DeviceNames.Parse("[]"));
        // The search's places are written by the same rule.
        Assert.Equal(new Place("Zürich", PlaceNames.Detail("Zürich", "Zurich", "Switzerland"), 47.37, 8.54).Label, PlaceNames.Label("Zürich", "Zurich", "Switzerland"));
        Assert.Equal(Zurich, PlaceNames.Label(" Zürich ", "Zurich", "Switzerland"));
    }

    [Fact]
    public void TheRequestSaysWhereToAKilometreInKarposLanguage()
    {
        Assert.Equal(
            "https://api.bigdatacloud.net/data/reverse-geocode-client?latitude=47.38&longitude=8.54&localityLanguage=de",
            DeviceNames.Url(47.37654, 8.5412, "de"));
        Assert.Equal("-33.87|151.21", DeviceNames.Key(-33.8688, 151.2093));
        Assert.Equal(DeviceNames.Key(47.371, 8.539), DeviceNames.Key(47.369, 8.541));
        Assert.Equal("en", DeviceNames.Language);
    }

    [Fact]
    public async Task APositionIsNamedOnceAndKept()
    {
        var asked = new List<(double, double, string)>();
        var saved = 0;
        var names = new DeviceNames(
            (latitude, longitude, language, _) =>
            {
                lock (asked)
                {
                    asked.Add((latitude, longitude, language));
                }
                return Task.FromResult<string?>(latitude > 47 ? Zurich : "Bern, Bern, Switzerland");
            },
            () => Interlocked.Increment(ref saved));
        // Three widgets asking at once, a few metres apart: one look-up, to two decimals.
        await Task.WhenAll(names.At(47.3712, 8.5391, default), names.At(47.3714, 8.5402, default), names.At(47.3688, 8.5395, default));
        Assert.Equal([(47.37, 8.54, "en")], asked);
        Assert.Equal(Zurich, PlaceNames.Device);
        Assert.Equal("Zürich", PlaceNames.Of(Device()));
        Assert.Equal(1, saved);

        await names.At(46.948, 7.447, default);
        Assert.Equal("Bern, Bern, Switzerland", PlaceNames.Device);
        // Back where it was: known, not asked again, and the newest kept again.
        await names.At(47.37, 8.54, default);
        Assert.Equal(2, asked.Count);
        Assert.Equal(2, names.Asked);
        Assert.Equal(Zurich, PlaceNames.Device);
        Assert.Equal([Zurich, "Bern, Bern, Switzerland"], names.Snapshot().Select(name => name.Name));

        // Only the newest few are kept.
        for (var step = 0; step < DeviceNames.Kept + 2; step++)
        {
            await names.At(10 + step, 10, default);
        }
        Assert.Equal(DeviceNames.Kept, names.Snapshot().Count);
    }

    [Fact]
    public async Task ANameNotFoundLeavesCurrentLocation()
    {
        var calls = 0;
        var names = new DeviceNames((latitude, _, _, _) =>
        {
            Interlocked.Increment(ref calls);
            return latitude > 47 ? Task.FromResult<string?>(Zurich) : Task.FromException<string?>(new HttpRequestException("offline"));
        });
        await names.At(47.37, 8.54, default);
        Assert.Equal(Zurich, PlaceNames.Device);
        // Moved, and the look-up fails: not Zürich's name for Bern, the placeholder.
        await names.At(46.95, 7.45, default);
        Assert.Null(PlaceNames.Device);
        Assert.Equal(S.WidgetWeatherHere, PlaceNames.Of(Device()));
        Assert.Equal(S.DockEditDeviceLocation, PlaceRow(new WeatherOptions { UseDeviceLocation = true }));
        // Not asked again at once, nor kept.
        await names.At(46.95, 7.45, default);
        Assert.Equal(2, calls);
        Assert.DoesNotContain(names.Snapshot(), name => name.Latitude < 47);

        // A position with no name at all is kept, empty: not asked again, and said as the placeholder.
        var none = 0;
        var nameless = new DeviceNames((_, _, _, _) =>
        {
            none++;
            return Task.FromResult<string?>(null);
        });
        await nameless.At(36, -40, default);
        await nameless.At(36, -40, default);
        Assert.Equal(1, none);
        Assert.Equal("", nameless.Snapshot().Single().Name);
        Assert.Null(PlaceNames.Device);
    }

    [Fact]
    public void TheNamesAreKeptInWeatherJson()
    {
        var file = new WeatherFile
        {
            Places = [],
            Names = [new DeviceName { Latitude = 47.37, Longitude = 8.54, Language = "de", Name = "Zürich, Kanton Zürich, Schweiz" }, new DeviceName { Latitude = 46.95, Longitude = 7.45, Language = "en", Name = "Bern" }],
        };
        var json = JsonSerializer.Serialize(file, IslandJson.Default.WeatherFile);
        var read = WeatherStore.Read(json);
        var names = new DeviceNames((_, _, _, _) => throw new InvalidOperationException("nothing is looked up"));
        names.Restore(read.Names);
        // The newest is where the device was last: called that from the start, in another language
        // than Karpo's while no name in Karpo's is known.
        Assert.Equal("Zürich, Kanton Zürich, Schweiz", PlaceNames.Device);
        Assert.Equal(2, names.Snapshot().Count);

        // A file from before has none; one edited by hand may say null.
        PlaceNames.Forget();
        Assert.Null(WeatherStore.Read("""{ "places": [] }""").Names);
        new DeviceNames((_, _, _, _) => Task.FromResult<string?>(null)).Restore(WeatherStore.Read("""{ "places": [], "names": [ { "latitude": 1, "longitude": 2, "name": null }, null ] }""").Names);
        Assert.Null(PlaceNames.Device);
        Assert.Empty(WeatherStore.Read("not json").Places ?? []);
    }

    [Fact]
    public void EveryWidgetCallsItsPlaceByTheOneRule()
    {
        var picked = new WeatherOptions { Place = "Bern, Switzerland", Latitude = 46.95, Longitude = 7.45 };
        var kept = new WeatherOptions { UseDeviceLocation = true, Place = "Bern, Switzerland", Latitude = 46.95, Longitude = 7.45 };
        Assert.Equal("", PlaceNames.Of(new WeatherOptions()));
        Assert.Equal("Bern", PlaceNames.Of(picked));
        Assert.Equal(S.WidgetWeatherHere, PlaceNames.Of(kept));
        Assert.Equal(S.DockEditDeviceLocation, PlaceRow(kept));
        // The edit list's header: no detail while the device's name is not known, not the place kept.
        var line = ListKind.WeatherNow.New();
        line.Weather = kept;
        Assert.Equal(WidgetInfos.WeatherNow.Title, IslandEdits.Header(line));

        PlaceNames.Located(Zurich);
        Assert.Equal($"{WidgetInfos.WeatherNow.Title} · Zürich", IslandEdits.Header(line));
        Assert.Equal("Bern", PlaceNames.Of(picked));
        // The device's name, not the place kept beside the switch: its town alone (§283), the whole
        // only for a web search of its weather.
        Assert.Equal("Zürich", PlaceNames.Of(kept));
        Assert.Equal("Zürich", PlaceNames.Known(Device()));
        Assert.Equal(Zurich, PlaceNames.Whole(Device()));
        Assert.Equal("Zürich", WeatherReading.From(SavedWeather.Cache(), Device(), DateTime.UtcNow, null).Place);
        Assert.Equal("Zürich", WeatherReading.From(null, Device(), DateTime.UtcNow, null).Place);
        // The menu's place row: where the device is, short; a place picked, short too.
        Assert.Equal("Zürich (this device)", PlaceRow(kept));
        Assert.Equal("Bern", PlaceRow(picked));

        // Windows gives no position: the place kept stands in (WeatherStore.Where), and is what is said.
        PlaceNames.Unlocated();
        Assert.Equal("Bern", PlaceNames.Of(kept));
        Assert.Equal(S.WidgetWeatherHere, PlaceNames.Of(Device()));
        Assert.Equal(S.DockEditDeviceLocation, PlaceRow(kept));
        // Given again: its name again.
        PlaceNames.Located(Zurich);
        Assert.Equal("Zürich", PlaceNames.Of(kept));
    }

    /// <summary>§283: a place is shown by its town, village or city alone, however it was kept.</summary>
    [Theory]
    [InlineData("Gais, Appenzell Ausserrhoden, Schweiz", "Gais", "Appenzell Ausserrhoden, Schweiz")]
    [InlineData("New York, NY, United States", "New York", "NY, United States")]
    [InlineData("Paris, France", "Paris", "France")]
    [InlineData("  Gais  ", "Gais", "")]
    [InlineData("Appenzell Ausserrhoden, Schweiz", "Appenzell Ausserrhoden", "Schweiz")]
    [InlineData("Schweiz", "Schweiz", "")]
    [InlineData(" , Kanton Zürich, Schweiz", "Kanton Zürich", "Schweiz")]
    [InlineData("", "", "")]
    [InlineData(" ", "", "")]
    [InlineData(null, "", "")]
    public void APlaceIsShownByItsTown(string? kept, string town, string detail)
    {
        Assert.Equal(town, PlaceNames.Short(kept));
        Assert.Equal((town, detail), PlaceNames.Parts(kept));
    }

    /// <summary>§283: a place picked is kept whole and shown short: the line and the menu's row at rest.</summary>
    [Fact]
    public void APlacePickedIsKeptWholeAndShownShort()
    {
        var gais = new WeatherOptions { Place = "Gais, Appenzell Ausserrhoden, Schweiz", Latitude = 47.36, Longitude = 9.45 };
        Assert.Equal("Gais", PlaceNames.Of(gais));
        Assert.Equal("Gais", WeatherReading.From(SavedWeather.Cache(), gais, DateTime.UtcNow, null).Place);
        Assert.Equal("Gais", PlaceRow(gais));
        Assert.Equal("Gais, Appenzell Ausserrhoden, Schweiz", PlaceNames.Whole(gais));
        // Only coordinates: those, whole.
        var bare = new WeatherOptions { Latitude = 47.36, Longitude = 9.45 };
        Assert.Equal(string.Create(System.Globalization.CultureInfo.CurrentCulture, $"{47.36:0.###}, {9.45:0.###}"), PlaceRow(bare));
    }

    /// <summary>
    /// No widget writes its place's name out itself: "Current location", a place's short form and
    /// the device's name are PlaceNames' alone, so a widget added later reads the same rule.
    /// </summary>
    [Fact]
    public void NoWidgetNamesItsPlaceItself()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Karpo.slnx")))
        {
            root = root.Parent;
        }
        Assert.NotNull(root);
        var found = new List<string>();
        foreach (var project in new[] { "Karpo.Core", "Karpo.Dock" })
        {
            foreach (var path in Directory.EnumerateFiles(Path.Combine(root.FullName, project), "*.cs", SearchOption.AllDirectories))
            {
                if (path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) || Path.GetFileName(path) == "PlaceNames.cs")
                {
                    continue;
                }
                var lines = File.ReadAllLines(path);
                for (var at = 0; at < lines.Length; at++)
                {
                    var line = lines[at];
                    if ((line.Contains("S.WidgetWeatherHere", StringComparison.Ordinal) && !line.Contains("public static string Here => S.WidgetWeatherHere", StringComparison.Ordinal))
                        || line.Contains("options.Place.Length > 0", StringComparison.Ordinal)
                        || line.Contains("Weather?.Place ??", StringComparison.Ordinal)
                        || line.Contains("IndexOf(',', StringComparison.Ordinal) is > 0 and var comma", StringComparison.Ordinal))
                    {
                        found.Add($"{Path.GetRelativePath(root.FullName, path)}:{at + 1}: {line.Trim()}");
                    }
                }
            }
        }
        Assert.True(found.Count == 0, "Name the place through PlaceNames (§253):\n" + string.Join("\n", found));
    }

    /// <summary>
    /// The four lines for the device's location, named: the weather's, the air's, the tides' and the
    /// earthquakes', dark at 150 % (device-place-*.png); and the faces' tooltips say the name.
    /// </summary>
    [UiFact]
    public void TheLinesAndTheFacesSayWhereTheDeviceIs()
    {
        PlaceNames.Located(Zurich);
        SavedWeather.AtAFixedTime(() => OnStaThread(gfx =>
        {
            var look = ListFormPictures.LookFor(true);
            T Line<T>(ListKind kind, Action<T> set)
                where T : CellIsland
            {
                var item = kind.New();
                item.Id = kind.Id();
                item.Weather = Device();
                var tile = (T)Karpo.Dock.Islands.CreateLine(Config.IslandKey("layout:default", item.Id), item, kind, gfx, new Config())!;
                set(tile);
                return tile;
            }
            using var weather = Line<WeatherLineTile>(ListKind.WeatherNow, tile => tile.Source = SavedWeather.Source());
            using var air = Line<AirQualityTile>(ListKind.AirQuality, tile => tile.Source = SavedAir.Source(SavedAir.Cache(european: 46, us: 54, pollen: "12.0")));
            using var tides = Line<TidesTile>(ListKind.Tides, tile =>
            {
                tile.Source = SavedTides.Source(SavedTides.Cache());
                tile.Imperial = false;
            });
            using var quakes = Line<QuakesLine>(ListKind.Quakes, tile => tile.Source = () => Quakes());
            ListLinesTests.Sheet(gfx, weather, ListKind.WeatherNow, 1.5f, look, Path.Combine(Pictures, "device-place-weather.png"));
            ListLinesTests.Sheet(gfx, air, ListKind.AirQuality, 1.5f, look, Path.Combine(Pictures, "device-place-air.png"));
            ListLinesTests.Sheet(gfx, tides, ListKind.Tides, 1.5f, look, Path.Combine(Pictures, "device-place-tides.png"));
            ListLinesTests.Sheet(gfx, quakes, ListKind.Quakes, 1.5f, look, Path.Combine(Pictures, "device-place-quakes.png"));

            var style = new IslandStyle(1f, false, 48, 24, look);
            string? Tip(FolderFace face)
            {
                var island = BarKind.FolderList.New();
                island.Id = face.Kind;
                face.Weather = Device();
                island.Folder!.Face = face;
                island.Normalize();
                (FolderIsland.AirStand, FolderIsland.TidesStand, FolderIsland.QuakeStand) = (SavedAir.Cache(32, 72), SavedTides.Cache(), Quakes());
                try
                {
                    using var folder = new FolderIsland(Config.IslandKey(null, island.Id), island, gfx, new Config());
                    return FaceDrawings.Of(folder.Options.Face).Tooltip(new FaceContext(gfx, style, folder.Options, folder));
                }
                finally
                {
                    (FolderIsland.AirStand, FolderIsland.TidesStand, FolderIsland.QuakeStand) = (null, null, null);
                }
            }
            Assert.StartsWith("Zürich\n", Tip(AirQualityFace.New()));
            Assert.StartsWith("Zürich\n", Tip(TidesFace.New()));
            Assert.StartsWith("Zürich · ", Tip(QuakesFace.New()));
        }));
    }

    /// <summary>A day of earthquakes around Zürich, the device's position Zürich.</summary>
    private static QuakeHeld Quakes()
    {
        var now = Clock.Now().ToUniversalTime();
        var feed = SavedQuakes.Feed(("n1", 4.8, 46.5, 9.9, now.AddMinutes(-42), null), ("n2", 3.1, 45.9, 6.9, now.AddHours(-5), null));
        return new QuakeHeld { Quakes = QuakeFeeds.Parse(feed), FetchedUtc = now.AddMinutes(-3), Device = (47.37, 8.54) };
    }

    /// <summary>What a weather line's place row says at its end in the bar's menu.</summary>
    private static string PlaceRow(WeatherOptions weather)
    {
        var line = ListKind.WeatherNow.New();
        line.Weather = weather;
        var rows = OptionMenu.Rows(OptionRows.Of(line), []);
        return rows.Single(row => row.Text == S.SettingsWidgetWeatherPlaceHeader).Value!.Text;
    }
}
