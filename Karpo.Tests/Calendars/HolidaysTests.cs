// The public holidays' core (Karpo.Core\Holidays.cs, docs\widgets.md §237): Nager.Date's answers read as
// it gives them (HolidayAnswers, saved on 2026-10-19), which of them a country and a region show, what
// they are called, the setting in the file (a file of before reads as it did, a hand edit costs nothing),
// and the rows of a calendar's menu. §265: several calendars, each labelled where two would read alike;
// the one country of before carried into the list; added and taken out from the menu; and no word Karpo
// shows, in any language, says where the holidays come from.

using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Karpo.Tests.Core;

namespace Karpo.Tests.Calendars;

public sealed class HolidaysTests
{
    private static IReadOnlyList<Holiday> Swiss => PublicHolidays.Parse(HolidayAnswers.Switzerland2026)!;

    private static List<string> Names(IEnumerable<Holiday> holidays) => [.. holidays.Select(holiday => holiday.Name)];

    [Fact]
    public void TheServicesAnswerReadsAsItGaveIt()
    {
        var holidays = Swiss;
        Assert.Equal(33, holidays.Count);
        var first = holidays[0];
        Assert.Equal(new DateOnly(2026, 1, 1), first.Date);
        Assert.Equal("Neujahr", first.LocalName);
        Assert.Equal("New Year's Day", first.Name);
        Assert.True(first.Global);
        Assert.Empty(first.Counties);
        Assert.True(first.IsPublic);
        var berchtold = holidays[1];
        Assert.False(berchtold.Global);
        Assert.Contains("CH-BE", berchtold.Counties);
        Assert.Equal(["Public"], berchtold.Types);
        Assert.Contains(holidays, holiday => holiday.Types.SequenceEqual(["Observance"]) && !holiday.IsPublic);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{\"type\":\"https://tools.ietf.org/html/rfc9110#section-15.5.5\",\"status\":404}")]
    [InlineData("<html>Bad gateway</html>")]
    [InlineData("[{\"date\":\"2026-01-0")]
    public void AnAnswerThatIsNoListIsNone(string answer) => Assert.Null(PublicHolidays.Parse(answer));

    [Fact]
    public void AnEntryWithoutADateOrANameIsPassedBy()
    {
        var holidays = PublicHolidays.Parse("""[{"date":"2026-13-01","name":"x"},{"localName":"y"},{"date":"2026-05-01","name":"","localName":""},{"date":"2026-05-01","name":"Labour Day","types":["Public",3],"counties":null}]""")!;
        var one = Assert.Single(holidays);
        Assert.Equal("Labour Day", one.LocalName);
        Assert.Equal(["Public"], one.Types);
        Assert.True(one.Global);
    }

    /// <summary>Without a region, only what the whole country has off: four days in Switzerland, its regional holidays left out.</summary>
    [Fact]
    public void WithoutARegionTheWholeCountrysAlone()
    {
        var shown = PublicHolidays.Shown(Swiss, "CH", "");
        Assert.Equal(["New Year's Day", "Ascension Day", "Swiss National Day", "Christmas Day"], Names(shown));
    }

    /// <summary>Zürich's: the country's and the canton's Public days, by date, none of another canton's, no observance.</summary>
    [Fact]
    public void ARegionAddsItsOwn()
    {
        var shown = PublicHolidays.Shown(Swiss, "CH", "CH-ZH");
        Assert.Equal(
            ["New Year's Day", "Good Friday", "Easter Monday", "Labour Day", "Ascension Day", "Whit Monday", "Swiss National Day", "Christmas Day", "St. Stephen's Day"],
            Names(shown));
        Assert.DoesNotContain(shown, holiday => holiday.Name == "Federal Day of Thanksgiving");
    }

    /// <summary>
    /// Public days alone, since "Also show other days" went (§240): Epiphany is Ticino's (Public) and not
    /// Schwyz's (an observance there); Columbus Day, a bank holiday everywhere, is not the country's. A day
    /// the service lists twice is shown once.
    /// </summary>
    [Fact]
    public void OnlyPublicDaysAndADayOnce()
    {
        var schwyz = PublicHolidays.Shown(Swiss, "CH", "CH-SZ");
        Assert.DoesNotContain(schwyz, holiday => holiday.Name == "Epiphany");
        Assert.DoesNotContain(schwyz, holiday => holiday.Name == "Federal Day of Thanksgiving");
        Assert.Single(PublicHolidays.Shown(Swiss, "CH", "CH-TI"), holiday => holiday.Name == "Epiphany");
        var states = PublicHolidays.Parse(HolidayAnswers.States2026)!;
        Assert.DoesNotContain(PublicHolidays.Shown(states, "US", ""), holiday => holiday.Name == "Columbus Day");
        var twice = PublicHolidays.Parse("""[{"date":"2026-08-01","localName":"Bundesfeier","name":"Swiss National Day","global":true,"types":["Public"]},{"date":"2026-08-01","localName":"Bundesfeier","name":"Swiss National Day","global":false,"counties":["CH-ZH"],"types":["Public"]}]""")!;
        Assert.Single(PublicHolidays.Shown(twice, "CH", "CH-ZH"));
    }

    /// <summary>A holiday the service lists for every region rather than as global (the United Kingdom's New Year's Day) is the whole country's.</summary>
    [Fact]
    public void ADayOfEveryRegionIsTheWholeCountrys()
    {
        var britain = PublicHolidays.Parse(HolidayAnswers.Britain2026)!;
        var shown = Names(PublicHolidays.Shown(britain, "GB", ""));
        Assert.Contains("New Year's Day", shown);
        Assert.DoesNotContain("Saint Patrick's Day", shown);
        Assert.DoesNotContain("Easter Monday", shown);
        Assert.Contains("Saint Patrick's Day", Names(PublicHolidays.Shown(britain, "GB", "GB-NIR")));
    }

    [Fact]
    public void AHolidayIsCalledByItsCountrysNameOrEnglishsAsKarpoSpeaks()
    {
        var national = Swiss.Single(holiday => holiday.Name == "Swiss National Day");
        Assert.Equal("Bundesfeier", PublicHolidays.Title(national, "de"));
        Assert.Equal("Swiss National Day", PublicHolidays.Other(national, "de"));
        Assert.Equal("Swiss National Day", PublicHolidays.Title(national, "en"));
        Assert.Equal("Bundesfeier", PublicHolidays.Other(national, "en"));
        var britain = PublicHolidays.Parse(HolidayAnswers.Britain2026)!;
        Assert.Null(PublicHolidays.Other(britain[0], "en"));
    }

    /// <summary>Every region the service named in its answers is in the list, so Settings and the menu offer them without asking it.</summary>
    [Fact]
    public void TheListedRegionsAreTheOnesTheServiceNames()
    {
        foreach (var answer in new[] { HolidayAnswers.Switzerland2026, HolidayAnswers.Switzerland2027, HolidayAnswers.Britain2026, HolidayAnswers.States2026 })
        {
            foreach (var county in PublicHolidays.Parse(answer)!.SelectMany(holiday => holiday.Counties))
            {
                var country = county[..2];
                Assert.True(HolidayPlaces.Has(country));
                Assert.Contains(HolidayPlaces.Regions(country), region => region.Code == county);
            }
        }
        Assert.Equal("Zürich", HolidayPlaces.RegionName("CH-ZH"));
        Assert.Equal("XY", HolidayPlaces.RegionName("CH-XY"));
        Assert.Empty(HolidayPlaces.Regions("FR"));
    }

    // --- The setting -------------------------------------------------------------------------------

    private static HolidayCountry Of(string country, string region = "") => new() { Country = country, Region = region };

    private static List<(string, string)> Listed(HolidayOptions options) => [.. options.Calendars.Select(calendar => (calendar.Country, calendar.Region))];

    /// <summary>
    /// A new config has no holidays and writes none; one that has several reads them back, each held to what it may
    /// say; §423: each its own colour, "#RRGGBB", written only where one is picked.
    /// </summary>
    [Fact]
    public void ANewConfigHasNoneAndTheSettingRoundTrips()
    {
        var fresh = Configs.Repaired(new Config().ToJson());
        Assert.Null(fresh.Holidays);
        Assert.DoesNotContain("holidays", Configs.Keys(fresh));

        var swiss = Of("ch", "ch-zh");
        swiss.Colour = "#498205";
        var config = new Config { Holidays = new HolidayOptions { Calendars = [swiss, Of("gb"), Of("CH", "CH-ZH"), Of("de", "ch-be")] } };
        var read = Configs.RoundTrip(config, out _);
        Assert.NotNull(read.Holidays);
        Assert.True(read.Holidays.On);
        // Each once; a region of another country is none.
        Assert.Equal([("CH", "CH-ZH"), ("GB", ""), ("DE", "")], Listed(read.Holidays));
        Assert.Equal(["#498205", null, null], read.Holidays.Calendars.Select(calendar => calendar.Colour));
        Assert.Equal(("CH-ZH", "#498205"), (read.Clone().Holidays!.Calendars[0].Region, read.Clone().Holidays!.Calendars[0].Colour));
        var written = JsonNode.Parse(read.ToJson())!["holidays"]!.AsObject();
        Assert.Equal(["calendars"], written.Select(pair => pair.Key));
        var calendars = written["calendars"]!.AsArray();
        Assert.Equal(["country", "region", "colour"], calendars[0]!.AsObject().Select(pair => pair.Key));
        Assert.Equal("#498205", (string?)calendars[0]!["colour"]);
        Assert.Equal(["country", "region"], calendars[1]!.AsObject().Select(pair => pair.Key));

        // None listed is off.
        var none = Configs.RoundTrip(new Config { Holidays = new HolidayOptions() }, out _);
        Assert.False(none.Holidays!.On);
    }

    /// <summary>A hand edit there costs the holidays, or one calendar of them, at most, never the config: everything else reads as it was.</summary>
    [Theory]
    [InlineData("\"yes\"", 0)]
    [InlineData("[1, 2]", 0)]
    [InlineData("{ \"on\": \"true\", \"country\": 41, \"region\": \"DE-BY\", \"colour\": \"red\", \"more\": { \"a\": [1] } }", 0)]
    [InlineData("{ \"calendars\": [1, \"CH\", { \"country\": 41 }, { \"country\": \"XYZ\" }, { \"region\": \"CH-ZH\" }, { \"country\": \"ch\", \"region\": \"de-by\", \"colour\": \"red\" }], \"colour\": \"#498205\" }", 1)]
    [InlineData("{ \"calendars\": { \"country\": \"CH\" } }", 0)]
    public void AHandEditCostsNothingElse(string holidays, int calendars)
    {
        var good = new Config { OnlineUpdateMinutes = 60, Holidays = new HolidayOptions { Calendars = [Of("CH")] } };
        var file = JsonNode.Parse(good.ToJson())!.AsObject();
        Assert.True(file.ContainsKey("holidays"));
        file["holidays"] = JsonNode.Parse(holidays);
        var text = file.ToJsonString();
        var read = Configs.Repaired(text);
        Assert.Equal(60, read.OnlineUpdateMinutes);
        if (read.Holidays is { } kept)
        {
            Assert.Equal(calendars, kept.Calendars.Count);
            // A region of another country is none; a colour that is no #RRGGBB is the holidays' own.
            Assert.All(kept.Calendars, calendar => Assert.Equal(("CH", "", (string?)null), (calendar.Country, calendar.Region, calendar.Colour)));
        }
        else
        {
            Assert.Equal(0, calendars);
        }
    }

    /// <summary>
    /// §265: the calendars as the calendar shows them, each once, labelled by their countries, or by their
    /// regions where two of one country are listed; Windows' region's stands for one that follows it.
    /// </summary>
    [Fact]
    public void EachCalendarIsLabelledSoTwoNeverReadAlike()
    {
        static List<(string, string, string)> Shown(params HolidayCountry[] calendars) =>
            [.. PublicHolidays.Calendars(new HolidayOptions { Calendars = [.. calendars] }).Select(shown => (shown.Country, shown.Region, shown.Label))];

        Assert.Empty(Shown());
        Assert.Empty(PublicHolidays.Calendars(null));
        Assert.Equal([("CH", "CH-ZH", HolidayPlaces.Name("CH")), ("GB", "", HolidayPlaces.Name("GB"))], Shown(Of("CH", "CH-ZH"), Of("GB")));
        Assert.Equal([("CH", "CH-ZH", "Zürich"), ("CH", "CH-TI", "Ticino")], Shown(Of("CH", "CH-ZH"), Of("CH", "CH-TI")));
        Assert.Equal([("CH", "", HolidayPlaces.Name("CH")), ("CH", "CH-ZH", "Zürich")], Shown(Of("CH"), Of("CH", "CH-ZH")));
        if (PublicHolidays.Home() is { Length: > 0 } home)
        {
            // One following Windows' region and the same country chosen are one.
            Assert.Equal([(home, "", HolidayPlaces.Name(home))], Shown(Of(""), Of(home)));
        }
    }

    /// <summary>
    /// The person: "dont tell from where the data comes from" (§265). No word Karpo shows, in any of its
    /// languages, names the service or its address; the Settings page's description and its search words
    /// among them.
    /// </summary>
    [Fact]
    public void NoWordKarpoShowsSaysWhereTheHolidaysComeFrom()
    {
        var service = new Regex(@"(?<![a-z])nager|date\.nager", RegexOptions.IgnoreCase);
        var host = new Uri(PublicHolidays.Service).Host;
        Assert.True(Strings.All.Count >= 30);
        foreach (var language in Strings.All)
        {
            foreach (var key in Strings.Keys)
            {
                var text = Strings.Lookup(key, language) ?? "";
                Assert.False(service.IsMatch(text), $"{language}: {key} says \"{text}\"");
                Assert.DoesNotContain(host, text, StringComparison.OrdinalIgnoreCase);
            }
            Assert.DoesNotMatch(service, Strings.Lookup("settings.holidays.description", language)!);
            Assert.DoesNotMatch(service, Strings.Lookup("settings.holidays.keywords", language)!);
        }
        // Nor the menu's rows, a country added.
        var config = new Config { Holidays = new HolidayOptions { Calendars = [Of("CH")] } };
        foreach (var row in OptionRows.Flat(OptionHolidays.Rows(config)))
        {
            Assert.DoesNotMatch(service, string.Join(" ", [row.Title, row.Text, row.Placeholder, row.Group ?? "", row.GroupSummary ?? "", row.Summary ?? "", .. row.Choices]));
        }
    }

    // --- The menu's rows ----------------------------------------------------------------------------

    private static OptionPlace Place(Config config) => new(config, Dark: true);

    /// <summary>
    /// §351: the calendars are ⚙ Settings' alone (KarpoMenu.Holidays). §529: a calendar line in every style (§240: not the
    /// agenda's alone) and the calendar's folder's calendar face name them in their accounts' line, "Manage accounts" its
    /// way to ⚙, in the face's own level, not the folder's own rows (the person, 2026-10-22: "public holidays setting should
    /// be in the calendar widget/face itself, not on the root configuration"); none links to them on its own (§351 to §528
    /// "Public holidays", a link of its own). No widget has the calendars' rows themselves.
    /// </summary>
    [Fact]
    public void ACalendarsMenuNamesTheHolidaysInItsAccountsLine()
    {
        var config = new Config { Holidays = new HolidayOptions { Calendars = [Of("CH")] } };
        static bool Holidays(OptionRow row) => row is { Kind: OptionRowKind.Link, Link: OptionLink.Holidays };
        static bool Accounts(OptionRow row) => row is { Kind: OptionRowKind.Link, Link: OptionLink.Accounts };
        static bool Own(OptionRow row) => row.Key.StartsWith("Holidays", StringComparison.Ordinal);
        var country = PublicHolidays.Calendars(config.Holidays.Normalized()).Single().Label;
        Assert.All(WidgetStyles.Calendar.Styles, style =>
        {
            var rows = OptionRows.Of(style.Form.New(), place: Place(config));
            Assert.Equal(country, Assert.Single(rows, Accounts).Summary);
            Assert.DoesNotContain(rows, Holidays);
            Assert.DoesNotContain(OptionRows.Flat(rows), Own);
        });
        Assert.DoesNotContain(OptionRows.Of(ListKind.DateHeader.New(), place: Place(config)), Accounts);
        Assert.Null(Assert.Single(OptionRows.Of(ListKind.Mail.New(), place: Place(config)), Accounts).Summary);
        var calendar = Presets.Folder(Presets.Find(Presets.CalendarId)!, new PresetMachine(HasBattery: false, FixedDrives: []));
        Assert.DoesNotContain(OptionRows.Of(calendar, place: Place(config)), Accounts);
        var folder = calendar.Folder!;
        folder.Faces.Insert(0, ClockFace.New());
        var face = folder.Faces.FindIndex(each => each.Kind == FolderFaces.CalendarId);
        Assert.Equal(country, Assert.Single(OptionRows.OfFace(folder, face, place: Place(config)), Accounts).Summary);
        Assert.DoesNotContain(OptionRows.OfFace(folder, face, place: Place(config)), Holidays);
        Assert.DoesNotContain(OptionRows.Flat(OptionRows.OfFace(folder, face, place: Place(config))), Own);
        Assert.DoesNotContain(OptionRows.OfFace(folder, 0, place: Place(config)), Accounts);
        // In the face's level: a link, its words under its name, nothing under it.
        var items = Karpo.Dock.OptionMenu.FaceSection(folder, [], place: Place(config));
        var link = items.SelectMany(item => item.Children ?? []).Single(child => child.Opens == OptionLinks.Opens(OptionLink.Accounts));
        Assert.Null(link.Children);
        Assert.Equal((S.DockWidgetMenuManageAccounts, country), (link.Text, link.Detail));
    }
    /// <summary>
    /// §265, §324: one row, "Public holidays", saying what it is set to: none listed, "Off" and its level
    /// "Add a country" alone; then a calendar for each country, named for it, its region its own row where
    /// the country has regions (§554: said there alone), deleted by its delete, asked first; the countries
    /// at the row's end. A country's whole calendar is added once; two of one country may stand with
    /// different regions, never the same twice.
    /// </summary>
    [Fact]
    public void CountriesAreAddedGivenTheirRegionsAndTakenOut()
    {
        var config = new Config();
        OptionRow Holidays() => OptionHolidays.Rows(config).Single(row => row.Key == OptionHolidays.Calendars);
        IReadOnlyList<OptionThing> Things() => Holidays().Owned!.Things;
        bool Add(string code) => Holidays().Owned!.Add!.Pick!(new OptionFound(HolidayPlaces.Name(code), code, code));
        var holidays = Holidays();
        Assert.Equal((OptionRowKind.Things, S.CoreHolidaysName, S.CoreHolidaysOff), (holidays.Kind, holidays.Title, holidays.Summary));
        var add = holidays.Owned!.Add!;
        Assert.Equal((OptionHolidays.Add, OptionRowKind.Search, S.CoreHolidaysAdd), (add.Key, add.Kind, add.Title));
        Assert.Empty(holidays.Owned.Things);

        Assert.True(Add("CH"));
        Assert.Equal([("CH", "")], Listed(config.Holidays!));
        Assert.Equal(HolidayPlaces.Name("CH"), Holidays().Summary);
        var swiss = Assert.Single(Things());
        Assert.Equal(HolidayPlaces.Name("CH"), swiss.Title);
        var region = Assert.Single(swiss.Rows);
        Assert.Equal(OptionHolidays.Region + 0, region.Key);
        Assert.Equal(27, region.Choices.Count);
        Assert.Equal(0, region.Chosen);
        Assert.True(region.Set!(region.Choices.ToList().IndexOf("Zürich")));
        Assert.Equal([("CH", "CH-ZH")], Listed(config.Holidays!));
        // §554: its region said by its region's row alone, not under its name too.
        Assert.Equal((HolidayPlaces.Name("CH"), "Zürich"), (Things()[0].Title, Things()[0].Rows[0].SaidOf(Things()[0].Rows[0].Chosen)));
        Assert.Null(Things()[0].Summary);

        // Another country, without regions: no rows of its own. The row says both.
        Assert.True(Add("FR"));
        Assert.Empty(Things()[1].Rows);
        Assert.Equal($"{HolidayPlaces.Name("CH")}, {HolidayPlaces.Name("FR")}", Holidays().Summary);
        // The whole of Switzerland beside Zürich, once; its region never Zürich again.
        Assert.True(Add("CH"));
        Assert.False(Add("CH"));
        Assert.False(Add("XYZ"));
        var third = OptionRows.Find([Holidays()], OptionHolidays.Region + 2)!;
        Assert.False(third.Set!(third.Choices.ToList().IndexOf("Zürich")));
        Assert.True(third.Set!(third.Choices.ToList().IndexOf("Ticino")));
        Assert.Equal([("CH", "CH-ZH"), ("FR", ""), ("CH", "CH-TI")], Listed(config.Holidays!));

        // Deleted, one by one, asked first, the others' rows following; none left is off, the record kept.
        var france = Things()[1].Delete!;
        Assert.Equal((OptionHolidays.Delete + 1, OptionRowKind.Action), (france.Key, france.Kind));
        Assert.Equal((S.CoreHolidaysRemoveTitle(HolidayPlaces.Name("FR")), S.CoreHolidaysRemoveBody), (france.Ask!.Title, france.Ask.Content));
        Assert.True(france.Set!(0));
        Assert.Equal([("CH", "CH-ZH"), ("CH", "CH-TI")], Listed(config.Holidays!));
        Assert.Equal(OptionHolidays.Region + 1, Assert.Single(Things()[1].Rows).Key);
        Assert.True(Things()[0].Delete!.Set!(0));
        Assert.True(Things()[0].Delete!.Set!(0));
        Assert.False(OptionHolidays.Remove(config.Holidays!, 0));
        Assert.NotNull(config.Holidays);
        Assert.False(config.Holidays!.On);
        Assert.Equal(S.CoreHolidaysOff, Holidays().Summary);
        Assert.Empty(Things());
    }

    /// <summary>"Add a country" offers Windows' region's first and ticks what is listed; typed, it finds countries by their names or codes.</summary>
    [Fact]
    public async Task AddACountryFindsCountries()
    {
        var config = new Config { Holidays = new HolidayOptions { Calendars = [Of("DE")] } };
        var add = OptionRows.Find(OptionHolidays.Rows(config), OptionHolidays.Add)!;
        var untyped = await add.Finder!.FindAsync("", CancellationToken.None);
        Assert.Contains(untyped, found => found.Value is "DE" && found.Chosen);
        if (PublicHolidays.Home() is { Length: > 0 })
        {
            Assert.Equal("", untyped[0].Value);
        }
        var typed = await add.Finder.FindAsync(HolidayPlaces.Name("CH")[..4], CancellationToken.None);
        Assert.Contains(typed, found => found.Value is "CH" && !found.Chosen);
        Assert.Contains(await add.Finder.FindAsync("gb", CancellationToken.None), found => found.Value is "GB");
    }

    /// <summary>
    /// In ⚙ the calendars are one level, "Public holidays ▸", saying "Off" or the countries at its
    /// end (§240): "Add a country" at its top, its search a level of its own, ruled off from the calendars
    /// (§324); each calendar a row with a bin at its end, Delete while lit, opening its region where it has
    /// regions (§265). The bin asks first (the dock's DeleteAsk), then deletes, and the menu stays.
    /// </summary>
    [Fact]
    public void TheMenuListsTheCalendarsInALevelOfTheirOwn()
    {
        var config = new Config();
        List<Karpo.Dock.OptionPick> picks = [];
        Karpo.Dock.FlyoutItem Level()
        {
            picks.Clear();
            return Karpo.Dock.OptionMenu.Rows(OptionHolidays.Rows(config), picks).Single(item => item.Text == S.CoreHolidaysName);
        }
        // As ⚙ acts (Dock.SettingsPicked, SettingsKept): the row read again from the config as it is now.
        OptionRow? RowOf(Karpo.Dock.OptionPick pick) => OptionRows.Find(OptionHolidays.Rows(config), pick.Key);

        var off = Level();
        // §517: a level of the person's own things says at its end the first of them, or greyed that there are none (§411
        // to §516 a page said nothing there).
        Assert.Equal(new Karpo.Dock.FlyoutValue(S.CoreHolidaysOff, Implied: true), off.Value);
        var adding = Assert.Single(off.Children!);
        Assert.Equal(S.CoreHolidaysAdd, adding.Text);
        Assert.IsType<Karpo.Dock.FlyoutField>(Assert.Single(adding.Children!).Control);

        config.Holidays = new HolidayOptions { Calendars = [Of("CH"), Of("FR")] };
        var on = Level();
        Assert.Equal(new Karpo.Dock.FlyoutValue(HolidayPlaces.Name("CH")), on.Value);
        // §411: the calendars, then right under them, ruled off (§452), Add a country (R9); §609 a country's region its dropdown
        // in its row on the settings panel (§411 R1 to §553 the country's row opened the regions themselves, §554 to §608 a row
        // nested under it).
        Assert.Equal([HolidayPlaces.Name("CH"), HolidayPlaces.Name("FR"), "", S.CoreHolidaysAdd], on.Children!.Select(child => child.Text));
        var swiss = on.Children![0];
        Assert.Null(swiss.Detail);
        Assert.Null(swiss.Checked);
        // §610: the region stands beside its country (one rule: the choice Beside, the thing's level it alone).
        Assert.True(Assert.Single(swiss.Children!).Beside);
        Assert.Contains(Assert.Single(swiss.Children!).Children!, child => child.Text == "Zürich");
        Assert.Null(on.Children[1].Children);
        // The bins in one column: every calendar's row keeps the chevron's slot.
        Assert.All(on.Children.Take(2), child => Assert.Equal(Karpo.Dock.OptionMenu.ItemGroup, child.Reorder));
        // §423: its colour's button before its bin.
        var buttons = Assert.IsType<Karpo.Dock.FlyoutControl.Tools>(swiss.Control).Buttons;
        Assert.Equal([S.SettingsAccountsColorHeader, S.DockMenuRemove], buttons.Select(button => button.Name));
        var bin = buttons[^1];
        Assert.Equal(Karpo.Dock.MenuChord.Delete, bin.Keys);
        var pick = Karpo.Dock.OptionMenu.PickOf(picks, bin.Id)!;
        Assert.Equal(Karpo.Dock.OptionPickKind.Act, pick.Kind);
        Assert.Equal(S.CoreHolidaysRemoveTitle(HolidayPlaces.Name("CH")), RowOf(pick)!.Ask!.Title);
        Assert.True(Karpo.Dock.OptionMenu.ApplyRow(RowOf(pick), pick));
        Assert.Equal([("FR", "")], Listed(config.Holidays!));
        Assert.Equal([HolidayPlaces.Name("FR"), "", S.CoreHolidaysAdd], Level().Children!.Select(child => child.Text));

        // A region's pick, a level down, is found and set through the calendar's row.
        config.Holidays.Calendars.Insert(0, Of("CH"));
        var zurich = Level().Children![0].Children![0].Children!.Single(choice => choice.Text == "Zürich");
        var region = Karpo.Dock.OptionMenu.PickOf(picks, zurich.Id)!;
        Assert.True(Karpo.Dock.OptionMenu.ApplyRow(RowOf(region), region));
        Assert.Equal([("CH", "CH-ZH"), ("FR", "")], Listed(config.Holidays));
        Assert.Equal("Zürich", Level().Children![0].Children![0].Children!.Single(choice => choice.Checked == true).Text);
        Assert.Null(Level().Children![0].Detail);

        // A country picked in "Add a country" is one more calendar.
        var field = Level().Children![^1].Children![0];
        Assert.True(Karpo.Dock.FieldPicks.KeepRow(RowOf(Karpo.Dock.OptionMenu.PickOf(picks, field.Id)!), new Karpo.Dock.FieldCommit(field.Id, "", new OptionFound(HolidayPlaces.Name("DE"), "DE", "DE"))));
        Assert.Equal([("CH", "CH-ZH"), ("FR", ""), ("DE", "")], Listed(config.Holidays));
    }

    /// <summary>A region picked while the country follows Windows' region makes it that country, so a move of Windows' region cannot leave a region of another.</summary>
    [Fact]
    public void ARegionPinsItsCountry()
    {
        var options = new HolidayOptions { Calendars = [Of("")] };
        Assert.True(OptionHolidays.SetRegion(options, 0, "DE", "DE-BY"));
        Assert.Equal([("DE", "DE-BY")], Listed(options));
        Assert.True(OptionHolidays.SetRegion(options, 0, "DE", ""));
        Assert.Equal([("DE", "")], Listed(options));
        Assert.False(OptionHolidays.SetRegion(options, 1, "DE", "DE-BY"));
        Assert.Equal("", Of("", "DE-BY").Normalized().Region);
    }
}
