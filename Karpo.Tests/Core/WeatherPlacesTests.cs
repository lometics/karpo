// weather: every weather widget has its own place (Karpo.Core\WeatherPlaces.cs, docs\widgets.md §80;
// the person, 2026-10-10: "location should not actually be shared in the folder ... you are actually
// able to show different places per folder which is a feature i want"). Each line and the weather
// face keep their own; none follows another. Two one-off copies are all that is left of sharing:
// the weather preset's first answer given to its pieces still without a place (Spread), and a file
// from the days of the shared place read once into the pieces that followed it (Adopt).

namespace Karpo.Tests.Core;

public sealed class WeatherPlacesTests
{
    private static readonly PresetMachine Machine = new(HasBattery: false, FixedDrives: []);

    private static WeatherOptions Bern => new() { Place = "Bern", Latitude = 46.95, Longitude = 7.45 };

    private static WeatherOptions Chur => new() { Place = "Chur", Latitude = 46.85, Longitude = 9.53 };

    private static BarItem Preset()
    {
        var folder = Presets.Folder(Presets.Find(Presets.WeatherId)!, Machine);
        folder.Id = "weather";
        return folder;
    }

    /// <summary>The preset with every piece's place taken away: switched off, or a file from before a new one was the device's.</summary>
    private static BarItem Unplaced()
    {
        var island = Preset();
        foreach (var line in Lines(island.Folder!))
        {
            line.Weather = new WeatherOptions();
        }
        island.Folder!.Face.Weather = new WeatherOptions();
        return island;
    }

    private static List<CellItem> Lines(FolderOptions folder) =>
        [.. folder.Cells.Select(cell => cell.Island).Where(item => item.Weather is not null)];

    /// <summary>
    /// A new weather preset: three weather lines and a face, each with a place of its own, the
    /// device's location (2026-10-11), so none needs setting up; a piece without a place needs its
    /// own, and two of them set to two places show two places.
    /// </summary>
    [Fact]
    public void EveryPieceHasItsOwnPlace()
    {
        var folder = Preset().Folder!;
        var lines = Lines(folder);
        Assert.Equal(3, lines.Count);
        Assert.All(lines, line => Assert.True(line.Weather!.UseDeviceLocation));
        Assert.All(lines, line => Assert.Null(WidgetSetup.Needs(line)));
        Assert.True(WeatherPlaces.OfFace(folder)!.UseDeviceLocation);
        Assert.Equal(3, lines.Select(line => line.Weather).Distinct().Count());
        Assert.DoesNotContain(folder.Face.Weather, lines.Select(line => line.Weather));

        folder = Unplaced().Folder!;
        lines = Lines(folder);
        Assert.All(lines, line => Assert.Equal(SetupField.Place, WidgetSetup.Needs(line)));
        Assert.Null(WeatherPlaces.OfFace(folder));

        lines[0].Weather = Bern;
        lines[2].Weather = Chur;
        Assert.Null(WidgetSetup.Needs(lines[0]));
        Assert.Equal(SetupField.Place, WidgetSetup.Needs(lines[1]));
        Assert.Equal("Chur", lines[2].Weather!.Place);
        Assert.Null(WeatherPlaces.OfFace(folder));
    }

    /// <summary>
    /// The preset's one question: the place chosen for one piece goes to every other piece still
    /// without one, the face too, as copies; a piece that has one keeps it; asked again, nothing is
    /// left to give.
    /// </summary>
    [Fact]
    public void SpreadGivesTheFirstPlaceToThePiecesWithoutOne()
    {
        var folder = Unplaced().Folder!;
        var lines = Lines(folder);
        lines[2].Weather = Chur;
        lines[0].Weather = Bern;

        Assert.Equal(2, WeatherPlaces.Spread(folder, lines[0].Weather!));
        Assert.Equal("Bern", lines[1].Weather!.Place);
        Assert.Equal("Chur", lines[2].Weather!.Place);
        Assert.Equal("Bern", WeatherPlaces.OfFace(folder)!.Place);
        // Copies: each its own from now on.
        Assert.NotSame(lines[0].Weather, lines[1].Weather);
        Assert.NotSame(lines[0].Weather, folder.Face.Weather);
        lines[0].Weather!.Place = "Thun";
        Assert.Equal("Bern", lines[1].Weather!.Place);

        Assert.Equal(0, WeatherPlaces.Spread(folder, lines[0].Weather!));
        // An unset place gives nothing; the device's location is a place like any other.
        var other = Unplaced().Folder!;
        Assert.Equal(0, WeatherPlaces.Spread(other, new WeatherOptions()));
        Assert.Equal(4, WeatherPlaces.Spread(other, new WeatherOptions { UseDeviceLocation = true }));
        Assert.All(Lines(other), line => Assert.True(line.Weather!.UseDeviceLocation));
    }

    /// <summary>A folder wearing another face keeps no place for it: Spread gives the lines alone.</summary>
    [Fact]
    public void SpreadLeavesAnotherFaceAlone()
    {
        var folder = Unplaced().Folder!;
        folder.Face = FolderFace.Of(FolderFaces.ClockId);
        Assert.Equal(3, WeatherPlaces.Spread(folder, Bern));
        Assert.Null(folder.Face.Weather);
    }

    /// <summary>
    /// Every new weather widget is for where the device is (2026-10-11): a line of each weather kind,
    /// the weather face, the preset's pieces. Only a new one: a file's place with no
    /// "useDeviceLocation" in it, or none at all, reads off as it did, and a place set or switched
    /// off stays so through the file.
    /// </summary>
    [Fact]
    public void ANewWeatherWidgetIsForTheDevicesLocation()
    {
        var kinds = ListWidgets.All.Where(line => line.Info.Options == WidgetOptionsKind.Weather).Select(line => line.Kind).ToList();
        Assert.Equal([ListKind.WeatherNow, ListKind.WeatherHours, ListKind.WeatherDays, ListKind.AirQuality, ListKind.Tides, ListKind.Quakes, ListKind.SunArc, ListKind.SunDayBar, ListKind.Sunrise, ListKind.Sunset, ListKind.Daylight, ListKind.SolarNoon, ListKind.Dawn, ListKind.Dusk,
            ListKind.WeatherHoursList, ListKind.WeatherDaysGraph, ListKind.AirPollutants, ListKind.AirPollen, ListKind.AirForecast, ListKind.TideGraph], kinds.Order());
        Assert.All(kinds, kind => Assert.True(kind.New().Weather!.UseDeviceLocation));
        Assert.All(kinds, kind => Assert.Null(WidgetSetup.Needs(kind.New())));
        Assert.True(FolderFace.Of(FolderFaces.WeatherId).Weather!.UseDeviceLocation);
        Assert.True(WeatherFace.New().Weather!.UseDeviceLocation);
        Assert.False(new WeatherOptions().UseDeviceLocation);

        var island = Unplaced();
        Lines(island.Folder!)[0].Weather = Bern;
        var back = Karpo.Core.Folders.FolderIn(Configs.Islands(Configs.RoundTrip(Configs.With(island), out _)), "weather")!;
        Assert.Equal(["Bern", "", ""], Lines(back).Select(line => line.Weather!.Place));
        Assert.All(Lines(back), line => Assert.False(line.Weather!.UseDeviceLocation));
        Assert.Null(WeatherPlaces.OfFace(back));

        var old = Configs.Repaired("""
            { "layouts": [ { "id": "L", "islands": [
              { "id": "w", "kind": "folderList", "folder": { "face": { "kind": "weather", "weather": { "place": "" } }, "lists": [ [
                { "island": { "id": "n", "kind": "weatherNow", "weather": { "place": "" } } },
                { "island": { "id": "h", "kind": "weatherHours" } } ] ] } } ] } ] }
            """);
        var read = Karpo.Core.Folders.FolderIn(Configs.Islands(old), "w")!;
        Assert.Equal(2, Lines(read).Count);
        Assert.All(Lines(read), line => Assert.False(line.Weather!.IsSet));
        Assert.Null(WeatherPlaces.OfFace(read));
    }

    /// <summary>The face's place survives the file, and a face of another kind writes none.</summary>
    [Fact]
    public void TheFacesPlaceSurvivesTheFile()
    {
        var island = Preset();
        island.Folder!.Face.Weather = Chur;
        var back = Configs.RoundTrip(Configs.With(island), out var changed);
        Assert.False(changed);
        Assert.Equal("Chur", WeatherPlaces.OfFace(Karpo.Core.Folders.FolderIn(Configs.Islands(back), "weather")!)!.Place);
        Assert.Null(FolderFace.Of(FolderFaces.ClockId).Weather);
        Assert.Equal("Chur", island.Folder.Face.Clone().Weather!.Place);
        Assert.NotSame(island.Folder.Face.Weather, island.Folder.Face.Clone().Weather);
    }
}
