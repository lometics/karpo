// §554: the public holidays on the settings panel's Karpo page (Services › Public holidays), as the person sets them
// (2026-11-07: "adding and changing properties of public holidays like region and stuff just does not work properly at
// all"). Each control of a calendar is found on the page as the panel lays it out (PanelLayout), done as the dock does a
// Karpo row (Dock.SettingsPicked, SettingsKept: the row read again from a copy of the config, set, the copy applied), and
// what the calendar widgets then show read from the holidays' calendar (HolidayCalendar, Nager.Date's answers as saved:
// HolidayAnswers, Germany's a few days of its own; nothing reaches the network). Held here:
//
//  - **A calendar's region is its dropdown**: a country with regions its row (its colour button, Remove), §609 its region a
//    dropdown in that row before them, saying its region and opening them; one without, its buttons alone, where the other's
//    stand. Where the country's name would not stand whole beside it (a long name, a narrow panel) the region a row right
//    under it, §594's way. §520 to §553 it was a row of buttons alone, its regions nowhere to pick; §554 to §608 the region
//    a row of its own under it (§565 to §587 on a darker well, §588 a card of its own on the page).
//  - **One calendar, one row**: a country Windows' region's calendar shows already is not added again, nor made so by a
//    region taken off.
//  - **"Show from" follows a calendar's region**: a calendar widget that picked the calendar keeps it when its region changes.
//  - **A search's pick closes its popup** as a ComboBox's list closes: Add a country's (HolidaysPanelUiTests; the rule,
//    the flyout's one choice, OneChoiceTests).
//  - **End to end**: add, region, colour, off in a widget, remove; each change one step, the calendar widgets' holidays following.

using Karpo.Dock;
using Karpo.Tests.Calendars;
using Karpo.Tests.Menus;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Edit;

public sealed class HolidaysPanelTests
{
    private const float Scale = 1.5f;

    private static readonly string Holidays = "Karpo/" + S.SettingsSectionsServices + "/" + S.CoreHolidaysName;

    /// <summary>A config of <paramref name="calendars"/> (a country and a region each) and a folder with a calendar line in it.</summary>
    private static Config ConfigOf(params (string Country, string Region)[] calendars)
    {
        var config = new Config();
        config.EditScreen(MenuCorpus.Display, "One");
        config.Holidays = new HolidayOptions { Calendars = [.. calendars.Select(each => new HolidayCountry { Country = each.Country, Region = each.Region })] };
        var folder = BarKind.FolderList.New();
        var line = ListKind.Calendar.New();
        line.Id = LineId;
        folder.Folder!.Lists = [[new FolderCell { Island = line }]];
        config.Layouts[0].Islands.Add(folder);
        return config;
    }

    /// <summary>The calendar line of <see cref="ConfigOf"/>'s folder.</summary>
    private static WidgetItem CalendarOf(Config config) => Karpo.Core.Folders.All(config.AllIslands).Single(entry => entry.Island.Id == LineId).Island;

    private const string LineId = "holidays-calendar";

    private static List<(string, string)> Listed(Config config) => [.. config.Holidays!.Calendars.Select(calendar => (calendar.Country, calendar.Region))];

    /// <summary>
    /// The Karpo page as the dock builds it, its picks into <paramref name="picks"/>, Public holidays open; the flyout no wider
    /// than <paramref name="widest"/> DIP (0: its own 580).
    /// </summary>
    private static PanelLayout PageOf(Config config, List<OptionPick> picks, float widest = 0)
    {
        picks.Clear();
        var page = PanelRows.Page(PanelPage.Karpo, PanelRows.SideOf(PanelPage.Karpo), PanelRows.GlyphOf(PanelPage.Karpo), TheDock.SettingsItems(config, MenuCorpus.Services(), picks));
        return new PanelLayout(new SettingsPanelTests.Words(), Scale, new PanelInput(0, new SettingsContent([page]), PanelPage.Karpo, new HashSet<string> { Holidays }), PanelLayout.SizeOf(Scale, 1548, widest * Scale));
    }

    /// <summary>The line of the calendar named <paramref name="name"/> (the <paramref name="nth"/> of that name).</summary>
    private static PanelLine LineOf(PanelLayout layout, string name, int nth = 1) =>
        layout.LineOf(Holidays + "/" + name + (nth > 1 ? " #" + nth : ""))!;

    /// <summary>The key of the region's dropdown of the calendar named <paramref name="name"/>.</summary>
    private static string RegionKey(string name) => Holidays + "/" + name + "/" + S.SettingsHolidaysRegionHeader;

    /// <summary>The region of the calendar named <paramref name="name"/>, as the page shows it: its row, its dropdown.</summary>
    private static (PanelRow Row, PanelHit Pop) RegionOf(PanelLayout layout, string name)
    {
        var key = RegionKey(name);
        var (hit, _) = layout.Controls(layout.LineOf(key)!).Single(each => each.Hit.Key == key);
        return (layout.Row(key)!, hit);
    }

    /// <summary>§609: nothing of <paramref name="line"/>'s controls over its name: it stands whole before them, a gutter off.</summary>
    private static void NameWhole(PanelLayout layout, PanelLine line) =>
        Assert.True(layout.NameX(line) + new SettingsPanelTests.Words().Width(line.Row.Text, Px(14)) + Px(16) <= layout.ControlLeft(line), $"{line.Row.Text}'s name under its controls");

    private static float Px(float dip) => MathF.Round(dip * Scale, MidpointRounding.AwayFromZero);

    /// <summary>
    /// What the dock does with a control's pick (Dock.SettingsPicked): the row read again from a copy of the config, an
    /// action asked first (answered yes), set; the copy the config where it changed. Whether it changed.
    /// </summary>
    private static bool Picked(ref Config config, List<OptionPick> picks, uint id)
    {
        var pick = OptionMenu.PickOf(picks, id)!;
        var next = config.Clone();
        var row = OptionRows.Find(KarpoMenu.Rows(next, MenuCorpus.Services()), pick.Key);
        if (row is not { Enabled: false } && OptionMenu.ApplyRow(row, pick))
        {
            config = next;
            return true;
        }
        return false;
    }

    /// <summary>What the dock does with a search's pick (Dock.SettingsKept): kept through its row, on a copy.</summary>
    private static bool Kept(ref Config config, List<OptionPick> picks, uint id, OptionFound found)
    {
        var pick = OptionMenu.PickOf(picks, id)!;
        var next = config.Clone();
        if (FieldPicks.KeepRow(OptionRows.Find(KarpoMenu.Rows(next, MenuCorpus.Services()), pick.Key), new FieldCommit(id, "", found)))
        {
            config = next;
            return true;
        }
        return false;
    }

    /// <summary>The popup's rows of the row at <paramref name="key"/>, as the dock opens them (Dock.PanelPopup: the row's own level).</summary>
    private static IReadOnlyList<FlyoutItem> PopupOf(Config config, List<OptionPick> picks, string key)
    {
        picks.Clear();
        return PanelRows.Find(TheDock.SettingsItems(config, MenuCorpus.Services(), picks), nameof(PanelPage.Karpo), key)!.Children!;
    }

    // --- A calendar's region is its dropdown -------------------------------------------------------------------------

    /// <summary>
    /// §609 (the person, 2026-11-09: "region selector for public holidays should be on the same line as the color selector"): a
    /// country with regions (Switzerland, Zürich) is one row, its region's dropdown (the one 240 DIP width) 8 before its colour
    /// button and Remove at its end, saying Zürich; no row of its own. The keys stop on the region, its colour, its Remove, as
    /// they stand; a screen reader hears a combo box named for the region and the country, saying Zürich; its popup the whole
    /// country and every canton, Zürich ticked; a canton picked is the calendar's region. A country without (France) its
    /// buttons alone, where Switzerland's stand. Each name whole before its controls.
    /// </summary>
    [Fact]
    public void ACalendarsRegionIsItsDropdownInItsRow()
    {
        var config = ConfigOf(("CH", "CH-ZH"), ("FR", ""));
        List<OptionPick> picks = [];
        var layout = PageOf(config, picks);
        var swiss = LineOf(layout, HolidayPlaces.Name("CH"));
        Assert.Equal(PanelRowKind.Item, swiss.Row.Kind);
        Assert.Null(swiss.Row.Second);
        Assert.Equal([S.SettingsAccountsColorHeader, S.DockMenuRemove], swiss.Row.Tools.Select(tool => tool.Name));
        var controls = layout.Controls(swiss);
        Assert.Equal([PanelHitKind.Pop, PanelHitKind.Tool, PanelHitKind.Tool], controls.Select(each => each.Hit.Kind));
        var region = RegionOf(layout, HolidayPlaces.Name("CH"));
        Assert.Equal((PanelRowKind.Choice, "Zürich"), (region.Row.Kind, region.Row.Value));
        Assert.Same(region.Row, swiss.Row.Pair);
        Assert.Same(swiss, layout.LineOf(RegionKey(HolidayPlaces.Name("CH"))));
        Assert.Equal(region.Pop, controls[0].Hit);
        // No row of its own: Public holidays' card its head, the two countries, Add a country.
        var card = layout.Blocks.Single(block => block.Lines.Contains(swiss)).Lines.ToList();
        Assert.DoesNotContain(card, line => line.Row.Kind == PanelRowKind.Choice && !line.Row.Adds);
        Assert.Equal(Px(PanelMetrics.OneLine), swiss.Rect.Height);
        // The one dropdown width, 8 before the colour button, in the buttons' middle; the colour 4 before Remove, ending where
        // every control ends.
        var (box, colour, remove) = (controls[0].Rect, controls[1].Rect, controls[2].Rect);
        Assert.Equal((Px(240), Px(32)), (box.Width, box.Height));
        Assert.Equal(colour.X - Px(8), box.Right);
        Assert.Equal((colour.Y, remove.Y), (box.Y, box.Y));
        Assert.Equal(layout.ControlEnd(swiss), remove.Right);
        NameWhole(layout, swiss);
        // The keys: the region, its colour, its Remove, as they stand.
        var stops = layout.Stops();
        Assert.Equal([region.Pop, controls[1].Hit, controls[2].Hit], stops.Skip(stops.IndexOf(region.Pop)).Take(3));
        var things = DeckUia.Things(layout, bar: false);
        var said = Assert.Single(things, thing => thing.Hit == region.Pop);
        Assert.Equal((S.SettingsHolidaysRegionHeader + " · " + HolidayPlaces.Name("CH"), UiaRole.ComboBox, UiaPattern.Expand), (said.Name, said.Role, said.Pattern));
        Assert.Equal(S.SettingsAccountsColorHeader + " · " + HolidayPlaces.Name("CH"), things.Single(thing => thing.Hit == controls[1].Hit).Name);
        Assert.Equal(S.DockMenuRemove + " · " + HolidayPlaces.Name("CH"), things.Single(thing => thing.Hit == controls[2].Hit).Name);
        var tree = DeckUia.Tree(layout, new TerraFX.Interop.Windows.RECT { left = 0, top = 0, right = 1000, bottom = 3200 }, PanelHit.None, bar: false);
        Assert.Equal("Zürich", Nodes(tree.Root).Single(node => node.Name == said.Name).Status);

        var france = LineOf(layout, HolidayPlaces.Name("FR"));
        Assert.Equal(PanelRowKind.Item, france.Row.Kind);
        Assert.Null(france.Row.Pair);
        var theirs = layout.Controls(france);
        Assert.Equal([PanelHitKind.Tool, PanelHitKind.Tool], theirs.Select(each => each.Hit.Kind));
        // Its buttons where Switzerland's stand: one column.
        Assert.Equal((colour.X, remove.X), (theirs[0].Rect.X, theirs[1].Rect.X));
        NameWhole(layout, france);
        // Shut, the level says the calendars by name.
        Assert.Equal($"{HolidayPlaces.Name("CH")}, {HolidayPlaces.Name("FR")}", layout.LineOf(Holidays)!.Row.Value);

        var regions = PopupOf(config, picks, region.Row.Key);
        Assert.Equal([S.SettingsHolidaysRegionWhole, .. HolidayPlaces.Regions("CH").Select(region => region.Name)], regions.Select(choice => choice.Text));
        Assert.Equal("Zürich", Assert.Single(regions, choice => choice.Checked == true).Text);
        Assert.True(Picked(ref config, picks, regions.Single(choice => choice.Text == "Bern").Id));
        Assert.Equal([("CH", "CH-BE"), ("FR", "")], Listed(config));
        Assert.Equal("Bern", RegionOf(PageOf(config, picks), HolidayPlaces.Name("CH")).Row.Value);
    }

    /// <summary>
    /// §609: where a country's name would not stand whole beside its region's dropdown — a long name (Bosnia and Herzegovina) on
    /// the flyout's own width, any on a narrow one (420 DIP) — its region stands a row of its own right under it (§594's way: a
    /// row of the card, as far in, 48 DIP), its buttons alone at its end, nothing over its name; a screen reader names it for
    /// the country all the same, and the keys go on to it after the country's buttons. A name that fits keeps it beside.
    /// </summary>
    [Fact]
    public void ARegionStandsUnderItsCountryWhereItsNameWouldNotStandWhole()
    {
        var config = ConfigOf(("CH", "CH-ZH"), ("BA", ""), ("FR", ""));
        List<OptionPick> picks = [];
        foreach (var (widest, stacked) in new[] { (0f, new[] { "BA" }), (420f, new[] { "CH", "BA" }) })
        {
            var layout = PageOf(config, picks, widest);
            foreach (var country in new[] { "CH", "BA" })
            {
                var name = HolidayPlaces.Name(country);
                var line = LineOf(layout, name);
                var region = RegionOf(layout, name);
                NameWhole(layout, line);
                var said = DeckUia.Things(layout, bar: false).Single(thing => thing.Hit == region.Pop);
                Assert.Equal(S.SettingsHolidaysRegionHeader + " · " + name, said.Name);
                if (!stacked.Contains(country))
                {
                    Assert.Same(line, layout.LineOf(RegionKey(name)));
                    continue;
                }
                Assert.Null(line.Row.Pair);
                Assert.Equal([PanelHitKind.Tool, PanelHitKind.Tool], layout.Controls(line).Select(each => each.Hit.Kind));
                var under = layout.LineOf(RegionKey(name))!;
                var card = layout.Blocks.Single(block => block.Lines.Contains(line)).Lines.ToList();
                Assert.Same(under, card[card.IndexOf(line) + 1]);
                Assert.Equal((PanelRowKind.Choice, true, line.Rect.Bottom, layout.NameX(line)), (under.Row.Kind, under.Inner, under.Rect.Y, layout.NameX(under)));
                Assert.Equal(Px(PanelMetrics.OneLine), under.Rect.Height);
                var stops = layout.Stops();
                Assert.Equal(stops.IndexOf(layout.Controls(line)[^1].Hit) + 1, stops.IndexOf(region.Pop));
            }
        }
    }

    private static IEnumerable<UiaNode> Nodes(UiaNode node) => [node, .. node.Children.SelectMany(Nodes)];

    // --- One calendar, one row ---------------------------------------------------------------------------------------

    /// <summary>
    /// The calendar of Windows' region (listed as "Default (…)") and that country's whole calendar are one calendar: neither
    /// is added beside the other, Add a country ticks both, and a region taken off a calendar of that country is refused.
    /// </summary>
    [Fact]
    public async Task TheWindowsRegionsCountryIsOneCalendar()
    {
        var home = PublicHolidays.Home();
        if (home.Length == 0 || HolidayPlaces.Regions(home).Count == 0)
        {
            return;
        }
        var options = new HolidayOptions { Calendars = [new HolidayCountry()] };
        Assert.False(OptionHolidays.AddCountry(options, home));
        var config = new Config { Holidays = options };
        var found = await OptionRows.Find(OptionHolidays.Rows(config), OptionHolidays.Add)!.Finder!.FindAsync("", CancellationToken.None);
        Assert.True(found.Single(each => each.Value is string code && code == home).Chosen);

        var pinned = new HolidayOptions { Calendars = [new HolidayCountry { Country = home }] };
        Assert.False(OptionHolidays.AddCountry(pinned, ""));
        Assert.Single(pinned.Calendars);

        var region = HolidayPlaces.Regions(home)[0].Code;
        var two = new HolidayOptions { Calendars = [new HolidayCountry(), new HolidayCountry { Country = home, Region = region }] };
        Assert.False(OptionHolidays.SetRegion(two, 1, home, ""));
        Assert.Equal(region, two.Calendars[1].Region);
    }

    // --- "Show from" follows a calendar's region ---------------------------------------------------------------------

    /// <summary>
    /// A calendar widget that picked Switzerland's calendar among what it shows keeps it as the calendar's region changes
    /// (its key the region's: "holidays|CH-ZH"), and back to the whole country; the widget's other picks as they were.
    /// </summary>
    [Fact]
    public void ShowFromFollowsACalendarsRegion()
    {
        var config = ConfigOf(("CH", ""), ("FR", ""));
        CalendarOf(config).Sources = ["me@outlook.com|calendar:work", "holidays|CH"];
        var region = OptionRows.Find(OptionHolidays.Rows(config), OptionHolidays.Region + 0)!;
        Assert.True(region.Set!(region.Choices.ToList().IndexOf("Zürich")));
        Assert.Equal(["me@outlook.com|calendar:work", "holidays|CH-ZH"], CalendarOf(config).Sources);
        region = OptionRows.Find(OptionHolidays.Rows(config), OptionHolidays.Region + 0)!;
        Assert.True(region.Set!(0));
        Assert.Equal(["me@outlook.com|calendar:work", "holidays|CH"], CalendarOf(config).Sources);
        // A widget that picked nothing still picks nothing.
        var other = ConfigOf(("CH", ""));
        region = OptionRows.Find(OptionHolidays.Rows(other), OptionHolidays.Region + 0)!;
        Assert.True(region.Set!(1));
        Assert.Null(CalendarOf(other).Sources);
    }

    // --- End to end --------------------------------------------------------------------------------------------------

    /// <summary>
    /// On a config with no holidays, the panel's way: Germany added from Add a country's finds; Switzerland too, its region
    /// Zürich from its dropdown (Good Friday, Easter Monday and Labour Day come, Zürich's); its colour picked from its colour
    /// button (its holidays in it); turned off in the calendar widget's Show from (gone there, still listed), on again; then
    /// removed by its Remove (gone, Germany's left), and Germany too: Off, no calendar. Each a change of the config the dock
    /// saves as one step; the config read again (a restart) shows what was set.
    /// </summary>
    [Fact]
    public async Task AddRegionColourOffAndRemove()
    {
        var config = ConfigOf();
        config.Holidays = null;
        List<OptionPick> picks = [];
        var steps = 0;
        using var calendar = new HolidayCalendar(Answer, null, () => new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc));
        var store = new CalendarStore(holidays: calendar);
        store.Open();
        async Task<List<CalendarEvent>> Shown()
        {
            calendar.Configure(config.Holidays);
            await calendar.Idle();
            return [.. store.EventsIn(new DateTime(2026, 1, 1), new DateTime(2027, 1, 1), shown: SourceFilter.Of(CalendarOf(config)))];
        }
        void Step(bool changed)
        {
            Assert.True(changed);
            steps++;
        }
        Assert.Equal(S.CoreHolidaysOff, PageOf(config, picks).LineOf(Holidays)!.Row.Value);
        Assert.Empty(await Shown());

        // Add a country: its button's popup its search; a country picked one more calendar; the popup closes on it.
        var add = PageOf(config, picks).LineOf(Holidays + "/" + S.CoreHolidaysAdd)!;
        Assert.True(add.Row.Adds);
        var search = PopupOf(config, picks, add.Row.Key).Single(item => item.Control is FlyoutField);
        var finds = await OptionRows.Find(OptionHolidays.Rows(config), OptionHolidays.Add)!.Finder!.FindAsync("Germ", CancellationToken.None);
        var germany = finds.First();
        Assert.Equal("DE", germany.Value);
        Step(Kept(ref config, picks, search.Id, germany));
        Assert.Contains(await Shown(), holiday => holiday.Title == "Tag der Deutschen Einheit" || holiday.Title == "German Unity Day");
        search = PopupOf(config, picks, add.Row.Key).Single(item => item.Control is FlyoutField);
        Step(Kept(ref config, picks, search.Id, new OptionFound(HolidayPlaces.Name("CH"), "CH", "CH")));
        Assert.Equal([("DE", ""), ("CH", "")], Listed(config));
        // The whole country's: none of a canton's (Labour Day is Zürich's, and some others').
        var whole = await Shown();
        Assert.Contains(whole, holiday => holiday.Start == new DateTime(2026, 8, 1));
        Assert.DoesNotContain(whole, holiday => holiday.Start == new DateTime(2026, 5, 1));

        // Its region from its dropdown: Zürich's own holidays come.
        var region = RegionOf(PageOf(config, picks), HolidayPlaces.Name("CH"));
        Assert.Equal(S.SettingsHolidaysRegionWhole, region.Row.Value);
        var zurich = PopupOf(config, picks, region.Row.Key).Single(choice => choice.Text == "Zürich");
        Step(Picked(ref config, picks, zurich.Id));
        Assert.Equal([("DE", ""), ("CH", "CH-ZH")], Listed(config));
        var regional = await Shown();
        Assert.Contains(regional, holiday => holiday.Start == new DateTime(2026, 5, 1));
        Assert.Contains(regional, holiday => holiday.Start == new DateTime(2026, 4, 3));
        Assert.Equal("Zürich", RegionOf(PageOf(config, picks), HolidayPlaces.Name("CH")).Row.Value);

        // Its colour from its colour button's swatches: its holidays in it, Germany's in the holidays' red.
        var swiss = LineOf(PageOf(config, picks), HolidayPlaces.Name("CH"));
        var colour = swiss.Row.Tools[0];
        Assert.NotNull(PanelSwatch.Of(colour));
        var green = colour.Level!.First(swatch => swatch.Icon is FlyoutTile { Color: { } shade } && shade != Argb.Parse(PublicHolidays.DefaultColour));
        Step(Picked(ref config, picks, green.Id));
        var picked = config.Holidays!.Calendars[1].Colour!;
        Assert.Equal(Argb.Parse(picked), PanelSwatch.Of(LineOf(PageOf(config, picks), HolidayPlaces.Name("CH")).Row.Tools[0])!.Color);
        var coloured = await Shown();
        Assert.All(coloured.Where(holiday => holiday.Start == new DateTime(2026, 5, 1)), holiday => Assert.Equal(CalendarColours.Of(picked), holiday.Colour));
        Assert.Contains(coloured, holiday => holiday.Start == new DateTime(2026, 10, 3) && holiday.Colour == CalendarColours.Of(PublicHolidays.DefaultColour));

        // Off in the calendar widget's Show from: its holidays gone there, the calendar still listed; on again.
        var parts = WidgetSources.HolidayParts(config.Holidays);
        Step(WidgetSources.Turn(CalendarOf(config), parts, "holidays|CH-ZH", on: false));
        Assert.DoesNotContain(await Shown(), holiday => holiday.Start == new DateTime(2026, 5, 1));
        Assert.Equal(2, config.Holidays.Calendars.Count);
        Step(WidgetSources.Turn(CalendarOf(config), parts, "holidays|CH-ZH", on: true));
        Assert.Contains(await Shown(), holiday => holiday.Start == new DateTime(2026, 5, 1));

        // Read again, as at a restart: what was set is what the page shows.
        config = Karpo.Tests.Core.Configs.Repaired(config.ToJson());
        var page = PageOf(config, picks);
        swiss = LineOf(page, HolidayPlaces.Name("CH"));
        Assert.Equal(("Zürich", Argb.Parse(picked)), (RegionOf(page, HolidayPlaces.Name("CH")).Row.Value, PanelSwatch.Of(swiss.Row.Tools[0])!.Color));

        // Removed by its Remove, asked first: gone from the config and the calendars; Germany's stay. Then Germany: Off.
        var remove = swiss.Row.Tools[1];
        Assert.NotNull(OptionRows.Find(KarpoMenu.Rows(config, MenuCorpus.Services()), OptionMenu.PickOf(picks, remove.Id)!.Key)!.Ask);
        Step(Picked(ref config, picks, remove.Id));
        Assert.Equal([("DE", "")], Listed(config));
        var left = await Shown();
        Assert.DoesNotContain(left, holiday => holiday.Start == new DateTime(2026, 8, 1));
        Assert.Contains(left, holiday => holiday.Start == new DateTime(2026, 10, 3));
        Step(Picked(ref config, picks, LineOf(PageOf(config, picks), HolidayPlaces.Name("DE")).Row.Tools[1].Id));
        Assert.Equal(S.CoreHolidaysOff, PageOf(config, picks).LineOf(Holidays)!.Row.Value);
        Assert.Empty(await Shown());
        Assert.False(store.HasCalendars);
        Assert.Equal(8, steps);
    }

    /// <summary>The saved answers: Switzerland's, Germany's made of a few of its days; nothing reaches the network.</summary>
    private static Task<string> Answer(string country, int year, CancellationToken cancel) => Task.FromResult(country switch
    {
        "CH" => HolidayAnswers.Switzerland2026.Replace("\"2026-", $"\"{year}-", StringComparison.Ordinal),
        "DE" => $$"""[{"date":"{{year}}-10-03","localName":"Tag der Deutschen Einheit","name":"German Unity Day","countryCode":"DE","global":true,"counties":null,"types":["Public"]},{"date":"{{year}}-12-25","localName":"Erster Weihnachtstag","name":"Christmas Day","countryCode":"DE","global":true,"counties":null,"types":["Public"]}]""",
        _ => "[]",
    });
}
