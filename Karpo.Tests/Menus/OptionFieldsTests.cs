// menus: the settings a person types or searches for, in a widget's menu (Karpo.Core\OptionFields.cs,
// Karpo.Dock\Dock.MenuFields.cs, docs\widgets.md §219). Held here:
//
//  - **Each is a field.** A face's label and a widget's names a text field, a zone, a place, a share
//    and a feed a search field; none of them sent to the page ("More settings…") any more.
//  - **The same write as Settings.** What a field keeps leaves the widget or the folder exactly as the
//    page's box does — the same file, byte for byte: the face's through FolderRows and FolderFace.Set,
//    as the folder page sets them; a widget's through OptionTexts, which the widget page's model
//    (IslandOptions) calls.
//  - **A pair** is two choices, the common currencies first, a pick making the share's symbol a pair
//    named as the page names one, a currency the other already is swapping the two.
//  - **The zones in use**, most used first, which a zone's search offers after this PC's.

using System.Text.Json;
using Karpo.Core;
using Karpo.Dock;

namespace Karpo.Tests.Menus;

public sealed class OptionFieldsTests
{
    private static string Json(WidgetItem item) => item switch
    {
        BarItem bar => JsonSerializer.Serialize(bar, ConfigJson.Default.BarItem),
        CellItem cell => JsonSerializer.Serialize(cell, ConfigJson.Default.CellItem),
        _ => "",
    };

    private static T Copy<T>(T item)
        where T : WidgetItem => item switch
        {
            BarItem bar => (T)(WidgetItem)JsonSerializer.Deserialize(Json(bar), ConfigJson.Default.BarItem)!,
            CellItem cell => (T)(WidgetItem)JsonSerializer.Deserialize(Json(cell), ConfigJson.Default.CellItem)!,
            _ => item,
        };

    private static BarItem Folder(params string[] faces)
    {
        var island = BarKind.FolderList.New();
        island.Id = "box";
        island.Folder!.Faces = [.. faces.Select(FolderFace.Of)];
        return island;
    }

    /// <summary>What the menu keeps for the field <paramref name="key"/> of <paramref name="item"/> (the face at <paramref name="face"/>; -1 the widget's own): typed words, or a search's pick.</summary>
    private static bool Keep(WidgetItem item, string key, string text, OptionFound? found = null, int face = -1)
    {
        List<OptionPick> picks = [];
        _ = face < 0
            ? OptionMenu.Section(OptionRows.Of(item), picks)
            : OptionMenu.FaceSection(((BarItem)item).Folder!, picks);
        var pick = picks.Single(pick => pick.Kind == OptionPickKind.Field && pick.Key == key && pick.Face == face);
        return FieldPicks.Keep(item, pick, new FieldCommit(pick.Id, text, found));
    }

    private static readonly Instrument Apple = new("AAPL", "Apple Inc.", "USD", "NASDAQ", 230.5, 2);

    private static readonly Place Bern = new("Bern", "Bern, Switzerland", 46.948_09, 7.447_44);

    // --- Each is a field ----------------------------------------------------------------------------

    [Fact]
    public void TextsAndSearchesAreFieldsInTheMenu()
    {
        OptionRow[] options =
        [
            new("label", OptionRowKind.Text, "Label") { Text = "Tokyo", Placeholder = "Tokyo, Home, …", Longest = 40 },
            new("zone", OptionRowKind.Search, "Time zone") { Text = "This PC's time zone", Finder = OptionFields.OfWidget(ListKind.Clock.New()).Single(row => row.Key == OptionFields.ClockZone).Finder },
        ];
        List<OptionPick> picks = [];
        var rows = OptionMenu.Rows(options, picks);
        Assert.Equal(["Label", "Time zone"], rows.Select(row => row.Text));
        // A line of text (§246): a row saying what it holds, opening a level of its field alone.
        Assert.Equal(("Tokyo", OptionPickKind.None), (rows[0].Value!.Text, OptionMenu.PickOf(picks, rows[0].Id)!.Kind));
        var label = Assert.Single(rows[0].Children!);
        Assert.Equal(new FlyoutField("Tokyo", "Tokyo, Home, …", 40), label.Control);
        Assert.Equal(OptionPickKind.Field, OptionMenu.PickOf(picks, label.Id)!.Kind);
        // A search is a level of its own (§226): its field, the results under it.
        var zone = Assert.Single(rows[1].Children!);
        Assert.NotNull(Assert.IsType<FlyoutField>(zone.Control).Finder);
        Assert.Equal(("Time zone", OptionPickKind.Field), (zone.Text, OptionMenu.PickOf(picks, zone.Id)!.Kind));
        // No "More settings…": the menu sets them itself.
        Assert.DoesNotContain(rows, row => row.Text == S.DockEditMoreSettings);
    }

    /// <summary>Every widget that had its text and search settings on its page alone has them in its menu now, by the page's names.</summary>
    [Fact]
    public void EachWidgetHasItsFields()
    {
        string[] Keys(WidgetItem item) => [.. OptionRows.Of(item).Where(row => row.Kind is OptionRowKind.Text or OptionRowKind.Search).Select(row => row.Key)];
        // In the menus' one order (§292): what it is of, then its words.
        Assert.Equal([OptionFields.ClockZone, OptionFields.ClockLabel], Keys(ListKind.Clock.New()));
        Assert.Equal([OptionFields.CountdownName], Keys(ListKind.Timer.New()));
        Assert.Equal([OptionFields.CountdownName], Keys(ListKind.Focus.New()));
        Assert.Equal([OptionFields.AlarmName], Keys(ListKind.Alarm.New()));
        Assert.Equal([OptionFields.Heading], Keys(ListKind.Heading.New()));
        Assert.Equal([OptionFields.WeatherPlace], Keys(ListKind.WeatherNow.New()));
        // §494: and the currency its amounts are shown in, a search of the currencies the rates reach.
        Assert.Equal([MarketRows.Symbol, MarketRows.Holding, MarketRows.Currency, MarketRows.Label], Keys(ListKind.Markets.New()));
        Assert.Equal([OptionFields.RssAddress], Keys(ListKind.Rss.New()));
        // §425: what a pin opens is a level of its kinds, each with its field.
        Assert.Equal([OptionFields.PinName], Keys(ListKind.Pin.New()));
        Assert.Equal([OptionPickers.PinApp, OptionPickers.PinFolder, OptionPickers.PinFile, OptionPickers.PinSite, OptionPickers.PinSetting],
            OptionRows.Of(ListKind.Pin.New()).Single(row => row.Key == OptionPickers.PinTarget).Level.Where(row => row.Kind == OptionRowKind.Search).Select(row => row.Key));
        Assert.Equal([OptionRows.ActionLeft, OptionRows.ActionRight], Keys(ListKind.Actions.New()));
        // §427: a folder has no name: no text row of its own.
        Assert.Empty(Keys(Folder(FolderFaces.IconId)));
        // A widget with none has none.
        Assert.Empty(Keys(ListKind.Cpu.New()));
    }

    /// <summary>
    /// §630: Open-Meteo's credit (its CC BY 4.0 licence asks for it; About Karpo no longer gives it) under the Place of every
    /// widget and face whose data is Open-Meteo's — the weather's, the air's, the tides' — and of none else that has a place
    /// (the sun's, computed; the earthquakes'); the menu carries it to the row the page draws.
    /// </summary>
    [Fact]
    public void OpenMeteosCreditStandsUnderThePlaceOfItsDataAlone()
    {
        string? Credit(IEnumerable<OptionRow> rows, string key) => rows.Single(row => row.Key == key).Detail;
        foreach (var kind in new[] { ListKind.WeatherNow, ListKind.WeatherDays, ListKind.Tides })
        {
            Assert.Equal(S.SettingsWidgetWeatherSource, Credit(OptionRows.Of(kind.New()), OptionFields.WeatherPlace));
        }
        Assert.Null(Credit(OptionRows.Of(ListKind.SunArc.New()), OptionFields.WeatherPlace));
        Assert.Null(Credit(OptionRows.Of(ListKind.Quakes.New()), OptionFields.WeatherPlace));
        foreach (var (face, credited) in new[] { (FolderFaces.WeatherId, true), (FolderFaces.AirQualityId, true), (FolderFaces.TidesId, true), (FolderFaces.QuakesId, false), (FolderFaces.SunId, false) })
        {
            Assert.Equal(credited ? S.SettingsWidgetWeatherSource : null, Credit(OptionRows.OfFace(Folder(face).Folder!, 0), OptionFields.FacePlace));
        }

        var place = OptionMenu.Rows(OptionRows.Of(ListKind.WeatherNow.New()), []).Single(row => row.Text == S.SettingsWidgetWeatherPlaceHeader);
        Assert.Equal(S.SettingsWidgetWeatherSource, place.Detail);
        // On the page: the Place's dropdown, the credit its second line.
        var row = PanelRows.Of([place], "Widget").Single();
        Assert.Equal((PanelRowKind.Choice, S.SettingsWidgetWeatherSource), (row.Kind, row.Second));
    }

    // --- The same write as Settings -----------------------------------------------------------------

    [Fact]
    public void AFacesLabelAndZoneAreSetAsTheFolderPageSetsThem()
    {
        var menu = Folder(FolderFaces.ClockId);
        var page = Copy(menu);
        var label = FolderFaces.Clock.Option(ClockFace.Label)!;
        var zone = FolderFaces.Clock.Option(ClockFace.Zone)!;

        Assert.True(Keep(menu, label.Key, "  Tokyo office ", face: 0));
        page.Folder!.Faces[0].Set(label, "  Tokyo office ");
        Assert.Equal(Json(page), Json(menu));
        // What it says already: nothing changes.
        Assert.False(Keep(menu, label.Key, "Tokyo office", face: 0));

        var tokyo = TimeZoneSearch.Windows.Single(entry => entry.Id == "Tokyo Standard Time");
        Assert.True(Keep(menu, zone.Key, "tok", new OptionFound("Tokyo", "", tokyo), face: 0));
        page.Folder.Faces[0].Set(zone, tokyo.Id);
        Assert.Equal(Json(page), Json(menu));
    }

    [Fact]
    public void AFacesPlaceIsSetAsTheFolderPageSetsIt()
    {
        foreach (var kind in new[] { FolderFaces.WeatherId, FolderFaces.AirQualityId, FolderFaces.TidesId })
        {
            var menu = Folder(kind);
            var page = Copy(menu);
            Assert.True(FaceRows.UsesDevice(menu.Folder!.Faces[0]));

            // A place picked: the device's location off, as the page's switch, then the place, as its box.
            Assert.True(Keep(menu, "place", "bern", new OptionFound(Bern.Name, Bern.Detail, Bern), face: 0));
            FaceRows.UseDevice(page.Folder!.Faces[0], false);
            WeatherPlaces.Choose(FaceRows.PlaceKept(page.Folder.Faces[0]), Bern.Label, Bern.Latitude, Bern.Longitude);
            Assert.Equal(Json(page), Json(menu));

            // "Use my device's location": the page's switch on, the switch over the place's search (§240).
            Assert.True(OptionRows.Apply(OptionRows.OfFace(menu.Folder, 0), "device", 1));
            FaceRows.UseDevice(page.Folder.Faces[0], true);
            Assert.Equal(Json(page), Json(menu));
            Assert.False(OptionRows.Apply(OptionRows.OfFace(menu.Folder, 0), "device", 1));
        }
    }

    [Fact]
    public void AFacesShareAndItsNameAreSetAsTheFolderPageSetsThem()
    {
        var menu = Folder(FolderFaces.MarketsId);
        var page = Copy(menu);
        Assert.True(Keep(menu, MarketRows.Symbol, "apple", new OptionFound(Apple.Name, "", Apple), face: 0));
        MarketRows.Choose(MarketFaces.Kept(page.Folder!.Faces[0]), Apple);
        Assert.Equal(Json(page), Json(menu));

        Assert.True(Keep(menu, MarketRows.Label, "  Apple shares and more ", face: 0));
        MarketFaces.Kept(page.Folder.Faces[0]).Label = MarketOptions.Labelled("  Apple shares and more ");
        Assert.Equal(Json(page), Json(menu));
        Assert.Equal("Apple shares", menu.Folder!.Faces[0].Market!.Label);
        // A listing not yet proved (no instrument) is never kept.
        Assert.False(Keep(menu, MarketRows.Symbol, "msft", new OptionFound("Microsoft", "", new Listing("MSFT", "Microsoft", "NASDAQ", "Equity")), face: 0));
    }

    /// <summary>A widget's names and its words, set by the same function as its page's model (IslandOptions → OptionTexts), trimmed and held alike.</summary>
    [Fact]
    public void AWidgetsTextsAreSetAsItsPageSetsThem()
    {
        void Same<T>(Func<T> make, string key, string typed, Func<T, bool> page)
            where T : WidgetItem
        {
            var menu = make();
            var settings = Copy(menu);
            Assert.True(Keep(menu, key, typed));
            Assert.True(page(settings));
            Assert.Equal(Json(settings), Json(menu));
            Assert.False(Keep(menu, key, typed));
        }
        Same(() => ListKind.Heading.New(), OptionFields.Heading, "  Today\r\nand tomorrow ", item => OptionTexts.SetHeading(item, "  Today\r\nand tomorrow "));
        Same(() => ListKind.Clock.New(), OptionFields.ClockLabel, " Tokyo ", item => OptionTexts.SetClockLabel(item, " Tokyo "));
        Same(() => ListKind.Timer.New(), OptionFields.CountdownName, " Tea ", item => OptionTexts.SetCountdownName(item, " Tea "));
        Same(() => ListKind.Focus.New(), OptionFields.CountdownName, "Deep work", item => OptionTexts.SetCountdownName(item, "Deep work"));
        Same(() => ListKind.Alarm.New(), OptionFields.AlarmName, " Wake up", item => OptionTexts.SetAlarmName(item, " Wake up"));
        Same(() => ListKind.Markets.New(), MarketRows.Label, "Apple shares!", item =>
        {
            item.Market!.Label = MarketOptions.Labelled("Apple shares!");
            return true;
        });
        Same(() => ListKind.Pin.New(), OptionFields.PinName, " Notes ", item => OptionTexts.SetPinName(item.Pin!, " Notes "));

        // What the page writes, spelled out: one line, trimmed; a countdown's record made with its kind's length.
        var heading = ListKind.Heading.New();
        Assert.True(Keep(heading, OptionFields.Heading, "  Today\r\nand tomorrow "));
        Assert.Equal("Today and tomorrow", heading.Heading!.Text);
        var focus = ListKind.Focus.New();
        Assert.True(Keep(focus, OptionFields.CountdownName, " Deep work "));
        Assert.Equal(("Deep work", CountdownOptions.FocusMinutes), (focus.Countdown!.Name, focus.Countdown.Minutes));
    }

    /// <summary>An actions line's slots are searched for among the pages, by name or section, "None" first; a pick sets the slot as the page's picker does.</summary>
    [Fact]
    public async Task AnActionsSlotIsSearchedForAndSetAsItsPageSetsIt()
    {
        var line = ListKind.Actions.New();
        line.Actions = new ActionsOptions { Left = "dateTime" };
        var left = OptionRows.Of(line).Single(row => row.Key == OptionRows.ActionLeft);
        Assert.Equal((OptionRowKind.Search, OptionField.Search), (left.Kind, left.Field));
        Assert.Equal(SettingsActions.Find("dateTime")!.Name, left.Text);
        var none = Assert.Single(left.Finder!.Offered);
        Assert.Equal(S.SettingsWidgetActionsNone, none.Title);
        Assert.False(none.Chosen);
        var found = await left.Finder.FindAsync(SettingsActions.Find("bluetooth")!.Name, CancellationToken.None);
        var bluetooth = found.First(row => row.Value is SettingsAction { Id: "bluetooth" });
        Assert.Equal(ActionSection.Devices.Title(), bluetooth.Detail);
        // Everything, nothing typed, the one in force ticked.
        Assert.Contains(await left.Finder.FindAsync("", CancellationToken.None), row => row.Chosen && row.Value is SettingsAction { Id: "dateTime" });

        Assert.True(Keep(line, OptionRows.ActionRight, "", bluetooth));
        Assert.Equal(("dateTime", "bluetooth"), (line.Actions.Left, line.Actions.Right));
        Assert.False(Keep(line, OptionRows.ActionRight, "", bluetooth));
        Assert.True(Keep(line, OptionRows.ActionLeft, "", none));
        Assert.Equal((null, "bluetooth"), (line.Actions.Left, line.Actions.Right));
    }

    [Fact]
    public void AWidgetsZonePlaceShareAndFeedAreSetAsItsPageSetsThem()
    {
        var clock = ListKind.Clock.New();
        var tokyo = TimeZoneSearch.Windows.Single(entry => entry.Id == "Tokyo Standard Time");
        Assert.True(Keep(clock, OptionFields.ClockZone, "tokyo", new OptionFound("Tokyo", "", tokyo)));
        Assert.Equal(tokyo.Id, clock.Clock!.TimeZone);
        // This PC's again: the empty id.
        Assert.True(Keep(clock, OptionFields.ClockZone, "", new OptionFound("This PC", "", new ZoneEntry("", "This PC", "", TimeSpan.Zero))));
        Assert.Equal("", clock.Clock.TimeZone);

        var weather = ListKind.WeatherNow.New();
        var page = Copy(weather);
        Assert.True(Keep(weather, OptionFields.WeatherPlace, "bern", new OptionFound(Bern.Name, Bern.Detail, Bern)));
        page.Weather!.UseDeviceLocation = false;
        WeatherPlaces.Choose(page.Weather, Bern.Label, Bern.Latitude, Bern.Longitude);
        Assert.Equal(Json(page), Json(weather));
        Assert.Equal((46.9481, 7.4474), (weather.Weather!.Latitude, weather.Weather.Longitude));

        var stock = ListKind.Markets.New();
        page = Copy(stock);
        Assert.True(Keep(stock, MarketRows.Symbol, "apple", new OptionFound(Apple.Name, "", Apple)));
        MarketRows.Choose(page.Market!, Apple);
        Assert.Equal(Json(page), Json(stock));
        Assert.Equal("Apple Inc. · NASDAQ · USD", stock.Market!.Detail);

        var rss = ListKind.Rss.New();
        Assert.True(Keep(rss, OptionFields.RssAddress, "example.com/feed", new OptionFound("", "", new ProvedFeed("https://example.com/feed", "  Example   News "))));
        Assert.Equal(("https://example.com/feed", "Example News"), (rss.Rss!.Address, rss.Rss.Title));
    }

    // --- A rate's currencies ----------------------------------------------------------------------------

    [Fact]
    public void ACurrencysListPutsTheCommonOnesFirst()
    {
        var order = Currencies.Ordered("CHF");
        Assert.Equal(["CHF", "USD", "EUR", "JPY", "GBP", "CNY", "CAD", "AUD"], order.Take(8));
        Assert.Equal(Currencies.Codes.Count, order.Count);
        Assert.Equal(order.Count, order.Distinct().Count());
        // The rest in the order of their codes.
        Assert.Equal(order.Skip(8).Order(StringComparer.Ordinal), order.Skip(8));
    }

    // --- The zones in use -----------------------------------------------------------------------------

    [Fact]
    public void TheZonesInUseAreMostUsedFirst()
    {
        CellItem Clock(string zone)
        {
            var line = ListKind.Clock.New();
            line.Clock!.TimeZone = zone;
            return line;
        }
        var folder = Folder(FolderFaces.ClockId, FolderFaces.ClockId);
        folder.Folder!.Faces[0].Set(FolderFaces.Clock.Option(ClockFace.Zone)!, "Tokyo Standard Time");
        folder.Folder.Faces[1].Set(FolderFaces.Clock.Option(ClockFace.Zone)!, "Pacific Standard Time");
        folder.Folder.Lists = [[new FolderCell { Island = Clock("Pacific Standard Time") }, new FolderCell { Island = Clock("") }, new FolderCell { Island = Clock("pacific standard time") }]];
        var config = new Config { Layouts = [new TaskbarLayout { Islands = [folder] }] };
        Assert.Equal(["Pacific Standard Time", "Tokyo Standard Time"], OptionFields.ZonesInUse(config));
    }
}
