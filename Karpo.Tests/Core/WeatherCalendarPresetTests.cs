// The weather and the calendar are presets (2026-10-09: "now do the same for the weather forecast and
// calendar widget. make sure that their styles are preseved as well as possible"), as the clock is
// (ClockPresetTests). What the library's "Weather" and "Calendar" add are ordinary list folders, the
// one wearing the weather face and the other the calendar face, holding their flyouts' pieces as
// widgets, top to bottom — and after that each is a folder like any other. There is no weather or
// calendar taskbar entry any more, and a file that has one reads without it. Plain code: no window.

namespace Karpo.Tests.Core;

using Folders = Karpo.Core.Folders;

public sealed class WeatherCalendarPresetTests
{
    private static readonly PresetMachine Machine = new(HasBattery: false, FixedDrives: []);

    private static Preset Weather => Presets.Find(Presets.WeatherId)!;

    private static Preset Calendar => Presets.Find(Presets.CalendarId)!;

    /// <summary>The weather's lines, in order, one card as the clock's (2026-10-11).</summary>
    private static readonly string[] WeatherLines = ["weatherNow", "weatherHours", "weatherDays"];

    /// <summary>
    /// The calendar's lines, in order, one card with its footer as the clock's: the events over the
    /// month, as the person has them in their own (2026-10-11).
    /// </summary>
    private static readonly string[] CalendarLines = ["dateHeader", "month", "events", "actions"];

    /// <summary>
    /// Added at a "+" of the bar, the weather is one list folder: the weather face in its simple
    /// look, its name and its glyph, and its lines in order — the conditions, the next hours, the week.
    /// </summary>
    [Fact]
    public void TheWeatherAddsAListFolderWithTheWeatherFaceAndItsLines()
    {
        List<BarItem> bar = [BarKind.Start.New()];

        var id = Presets.AddToBar(bar, new BarSlot(BarSection.Start, 0), Weather, Machine);

        var folder = Assert.Single(bar, island => island.Id == id);
        Assert.Equal(BarKind.FolderList, folder.Kind);
        var options = folder.Folder!;
        Assert.Equal(FolderFaces.WeatherId, options.Face.Kind);
        Assert.Equal(WeatherFace.Simple, WeatherFace.LookOf(options.Face));
        Assert.Equal(WeatherLines, Assert.Single(options.Lists).Select(cell => cell.Island.Kind));
        Assert.All(options.Cells, cell => Assert.Equal(FolderView.List, cell.Island.View));
        // Each piece has a place of its own (WeatherPlaces), the face's included: the device's location (2026-10-11).
        Assert.True(WeatherPlaces.OfFace(options)!.UseDeviceLocation);
        Assert.All(options.Cells.Where(cell => !cell.Island.Info!.Id.Equals("divider", StringComparison.Ordinal)), cell => Assert.True(cell.Island.Weather!.UseDeviceLocation));
    }

    /// <summary>
    /// Added at a "+" of the bar, the calendar is one list folder: the calendar face writing the day
    /// and the date, as the calendar entry did, and its lines in order as the person arranged them
    /// (2026-10-11) — the date's header, the month, the day's events, the footer with Date &amp; time
    /// as its link and the calendar app as its button.
    /// The month publishes the folder's day and the events follow it; the header publishes today.
    /// </summary>
    [Fact]
    public void TheCalendarAddsAListFolderWithTheCalendarFaceAndItsLines()
    {
        List<BarItem> bar = [BarKind.Start.New(), BarKind.ShowDesktop.New()];
        bar[1].Section = BarSection.End;

        var id = Presets.AddToBar(bar, new BarSlot(BarSection.End, 0), Calendar, Machine);

        var folder = Assert.Single(bar, island => island.Id == id);
        var options = folder.Folder!;
        Assert.Equal(FolderFaces.CalendarId, options.Face.Kind);
        Assert.False(CalendarFace.IsPage(options.Face));
        Assert.True(CalendarFace.WithDay(options.Face));
        Assert.Equal(CalendarLines, Assert.Single(options.Lists).Select(cell => cell.Island.Kind));
        Assert.Equal(CalendarLines, Folders.ReadingOrder(options).Select(cell => cell.Island.Kind));
        var actions = options.Cells.Single(cell => cell.Island.List == ListKind.Actions).Island.Actions!;
        Assert.Equal(("dateTime", SettingsActions.CalendarId), (actions.Left, actions.Right));
        var month = options.Cells.Single(cell => cell.Island.List == ListKind.Month).Island;
        var events = options.Cells.Single(cell => cell.Island.List == ListKind.Events).Island;
        Assert.True(Channels.Follows(month, Channels.Day));
        Assert.True(Channels.Follows(events, Channels.Day));
        Assert.Contains(Channels.Day, ListWidgets.Of(ListKind.Month).Publishes);
        Assert.Contains(Channels.Day, ListWidgets.Of(ListKind.DateHeader).Publishes);
    }

    /// <summary>Two weathers and two calendars are four folders: faces and lines their own, nothing shared.</summary>
    [Fact]
    public void EachIsItsOwn()
    {
        foreach (var preset in new[] { Weather, Calendar })
        {
            var one = Presets.Folder(preset, Machine);
            var two = Presets.Folder(preset, Machine);
            Assert.NotSame(one.Folder!.Face, two.Folder!.Face);
            Assert.Empty(one.Folder.Cells.Select(cell => cell.Island.Id).Intersect(two.Folder.Cells.Select(cell => cell.Island.Id)));
        }
        var first = Presets.Folder(Weather, Machine);
        first.Folder!.Face.Set(FolderFaces.Weather.Option(WeatherFace.Look)!, WeatherFace.Forecast);
        Assert.Equal(WeatherFace.Simple, WeatherFace.LookOf(Weather.Face!));
        Assert.Equal(WeatherFace.Simple, WeatherFace.LookOf(Presets.Folder(Weather, Machine).Folder!.Face));
    }

    /// <summary>
    /// After it is added it is nothing special: a view added, the events taken out, the face changed
    /// to the weather's forecast or the calendar's page — the file written and read back has all of
    /// it, as it would any folder's.
    /// </summary>
    [Fact]
    public void AfterwardsEachIsAnOrdinaryFolder()
    {
        var config = Configs.With(BarKind.Start.New());
        var list = config.DefaultLayout.Islands;
        var calendarId = Presets.AddToBar(list, new BarSlot(BarSection.End, 0), Calendar, Machine);
        var weatherId = Presets.AddToBar(list, new BarSlot(BarSection.Start, 0), Weather, Machine);
        var calendar = list.Single(island => island.Id == calendarId);
        var weather = list.Single(island => island.Id == weatherId);

        Assert.NotNull(Folders.AddNew(list, calendar, "weekView"));
        var events = calendar.Folder!.Cells.Single(cell => cell.Island.List == ListKind.Events).Island;
        Assert.Same(events, Folders.Remove(list, Folders.Path(calendar.Id, events.Id)));
        calendar.Folder.Face.Set(FolderFaces.Calendar.Option(CalendarFace.Look)!, CalendarFace.Page);
        weather.Folder!.Face = WeatherFace.New(WeatherFace.Forecast);
        weather.Folder.Cells.Single(cell => cell.Island.List == ListKind.WeatherNow).Island.Weather = new WeatherOptions { Place = "Bern", Latitude = 46.95, Longitude = 7.45 };

        var read = Configs.RoundTrip(config, out var changed);
        Assert.False(changed);
        var islands = read.DefaultLayout.Islands;
        var again = islands.Single(island => island.Id == calendar.Id).Folder!;
        Assert.Equal(["dateHeader", "month", "actions", "weekView"], Folders.ReadingOrder(again).Select(cell => cell.Island.Kind));
        Assert.True(CalendarFace.IsPage(again.Face));
        var clouds = islands.Single(island => island.Id == weather.Id).Folder!;
        Assert.Equal(WeatherFace.Forecast, WeatherFace.LookOf(clouds.Face));
        Assert.Equal("Bern", clouds.Cells.Single(cell => cell.Island.List == ListKind.WeatherNow).Island.Weather!.Place);
        // The line's place is the line's: the face has its own, the device's.
        Assert.True(WeatherPlaces.OfFace(clouds)!.UseDeviceLocation);
    }

    /// <summary>
    /// The weather and calendar taskbar entries are gone: no kind of the bar is either, the bar's add
    /// list offers neither, a file that has one reads without it (their lines in a folder stay), and
    /// the library's "Weather" and "Calendar" are the presets. No entry has named looks any more: the
    /// weather's were the one kind's that had them, and they are its face's (§507: a widget's look is its own values).
    /// </summary>
    [Fact]
    public void ThereAreNoWeatherOrCalendarEntriesAnyMore()
    {
        Assert.Null(BarEntries.Find(Configs.Kind("weather")));
        Assert.Null(BarEntries.Find(Configs.Kind("calendar")));
        Assert.DoesNotContain(Catalogue.On(WidgetType.Bar), entry => entry.Id is "weather" or "calendar" or "weatherNow");
        Assert.Contains(Presets.On(WidgetType.Bar), preset => preset.Id == Presets.WeatherId);
        Assert.Contains(Presets.On(WidgetType.Bar), preset => preset.Id == Presets.CalendarId);
        Assert.Null(typeof(BarEntry).GetProperty("Looks"));
        // §567: and no widget has a look of its own at all.
        Assert.Null(typeof(BarItem).GetProperty("Look"));

        var config = Configs.Repaired("""
            { "layouts": [ { "id": "L", "islands": [
              { "id": "s", "kind": "start" },
              { "id": "w", "kind": "weather", "weather": { "place": "Zurich", "latitude": 47.37, "longitude": 8.54 } },
              { "id": "c", "kind": "calendar", "calendar": { "view": "week" } },
              { "id": "f", "kind": "folderList", "folder": { "lists": [ [
                { "island": { "id": "l", "kind": "calendar" } },
                { "island": { "id": "n", "kind": "weatherNow", "weather": { "place": "Zurich", "latitude": 47.37, "longitude": 8.54 } } } ] ] } } ] } ] }
            """, out var changed);

        Assert.True(changed);
        Assert.Equal(["s", "f"], Configs.Islands(config).Select(island => island.Id));
        Assert.Equal([ListKind.Calendar, ListKind.WeatherNow], Configs.Islands(config)[1].Folder!.Cells.Select(cell => cell.Island.List!.Value));
    }
}
