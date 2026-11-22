// holidays: the public holidays as rows of a calendar's menu (docs\widgets.md §237), the rows the Accounts
// page has for them. They are one submenu, "Public holidays", saying at its end what they are set to
// ("Off", "Switzerland"), §240.
//
// Since §265 (the person: "you should be able to add multiple holiday calendars") the submenu is a list:
// a row for each calendar, named for its country, its region on a second line under it, opening its region
// where the country has regions, a bin at its end taking it out. None listed is off: there is no switch any
// more. The rows say nothing about where the holidays come from. Since §324 (the person: "Holiday calendars
// delete without asking, and adding is a separate search row") it is a level of the person's own things as
// a line's notes are (OptionRow.Owned): "Add a country" at its top, a search of the countries opening in
// place, Windows' region's first, each pick one more calendar; then the calendars; the bin asks first.
//
// They are the person's, not the widget's (Config.Holidays: every calendar widget shows every calendar).
// §324 to §350 had the same rows in a calendar line in every style and in the calendar face of a folder
// too; since §351 (the person, 2026-10-25: "some settings appear multiple times ... i think in that case
// it would be better to link into the account settings instead of duplicating them") they are ⚙ Settings'
// alone (KarpoMenu.Holidays, with their colour), and a calendar's menu has "Public holidays", a link
// (OptionRowKind.Link) there: in a line's own rows, in a face's own level (the person, 2026-10-22: "public
// holidays setting should be in the calendar widget/face itself, not on the root configuration"). A row
// writes into the config it was read from, which is the copy the dock saves. §529: no link of their own; the calendar's
// accounts line names them, its "Manage accounts" the one way to ⚙ (OptionAccountRows.Into).
//
// §423 (the person, 2026-10-30: "the color for the public holidays should be settable per country / entry"): each
// calendar its own colour (HolidayCountry.Colour), Automatic the holidays' red: a palette button at its row's end,
// beside its bin, opening the swatches in place (OptionThing.Colour); its row still opens its regions themselves, so
// nothing is a level deeper (§411, R1).

namespace Karpo.Core;

/// <summary>The holidays' rows of a calendar's menu; see the top of the file.</summary>
public static class OptionHolidays
{
    /// <summary>§324: the one row, "Public holidays", whose level the calendars are.</summary>
    public const string Calendars = "Holidays";

    /// <summary>§324: a calendar's delete, by its place in the list ("HolidaysRemove0").</summary>
    public const string Delete = "HolidaysRemove";

    /// <summary>A calendar's region, by the calendar's place in the list ("HolidaysRegion0").</summary>
    public const string Region = "HolidaysRegion";

    /// <summary>§423: a calendar's colour, by the calendar's place in the list ("HolidaysColour0").</summary>
    public const string Colour = "HolidaysColour";

    /// <summary>§265: "Add a country", the search that adds a calendar.</summary>
    public const string Add = "HolidaysAdd";

    /// <summary>Whether <paramref name="item"/> shows the calendars: a calendar line of any style. §529: its accounts' line names them (OptionAccountRows.Into).</summary>
    public static bool Shows(WidgetItem? item) => WidgetStyles.Of(item)?.Widget == WidgetStyles.Calendar;

    /// <summary>
    /// <paramref name="config"/>'s holidays as one row, "Public holidays", saying "Off" or the countries
    /// (<see cref="OptionRow.Summary"/>); its level (<see cref="OptionRow.Owned"/>, §324) "Add a country"
    /// at its top (a search, Windows' region's first), then each calendar, named for its country, its
    /// region under it and its region's choice its submenu where its country has regions, its bin asking
    /// first. Showing the rows writes nothing: the record is made the first time a country is added.
    /// </summary>
    public static IReadOnlyList<OptionRow> Rows(Config config)
    {
        var shown = config.Holidays ?? new HolidayOptions();
        HolidayOptions Kept() => config.Holidays ??= shown;
        // What of the calendar it shows (§292), right after what it is of.
        const OptionSlot slot = OptionSlot.Scope;
        var things = new List<OptionThing>();
        for (var index = 0; index < shown.Calendars.Count; index++)
        {
            var at = index;
            var calendar = shown.Calendars[at];
            var country = PublicHolidays.CountryOf(calendar);
            var regions = HolidayPlaces.Regions(country);
            List<OptionRow> own = [];
            if (regions.Count > 0)
            {
                own.Add(new OptionRow(Region + at, OptionRowKind.Choice, S.SettingsHolidaysRegionHeader)
                {
                    Glyph = OptionGlyphs.Place,
                    Slot = slot,
                    Choices = [S.SettingsHolidaysRegionWhole, .. regions.Select(region => region.Name)],
                    ChoiceGlyphs = [OptionGlyphs.Zone, .. regions.Select(_ => OptionGlyphs.Place)],
                    Chosen = RegionIndex(calendar, regions),
                    Set = choice => choice >= 0 && choice <= regions.Count && SetRegion(config, at, country, choice == 0 ? "" : regions[choice - 1].Code),
                });
            }
            var name = CountryText(calendar.Country);
            // §554: its region said by its region's choice alone (the settings panel's dropdown at its end), not under its name too.
            things.Add(new OptionThing(name, OptionGlyphs.Zone)
            {
                Rows = own,
                // §423 (the person, 2026-10-30: "the color for the public holidays should be settable per country / entry"):
                // its own colour, as the Accounts page's swatch offered the holidays' one (§338): Automatic the holidays' red.
                Colour = PartColours.Row(Colour + at, S.SettingsAccountsColorHeader, calendar.Colour, PublicHolidays.DefaultColour, picked => SetColour(Kept(), at, picked)) with { Slot = slot },
                Delete = new OptionRow(Delete + at, OptionRowKind.Action, S.DockMenuRemove)
                {
                    Glyph = OptionGlyphs.Delete,
                    Slot = slot,
                    Ask = new DeleteQuestion(S.CoreHolidaysRemoveTitle(name), S.CoreHolidaysRemoveBody, S.SettingsCommonRemove, S.DockDeleteCancel, 0),
                    Set = _ => Remove(Kept(), at),
                },
            });
        }
        var add = new OptionRow(Add, OptionRowKind.Search, S.CoreHolidaysAdd)
        {
            Glyph = OptionGlyphs.New,
            // §521: a way to add one: a button whose list is its search, not a value.
            Adds = true,
            Slot = slot,
            Field = OptionField.Search,
            Placeholder = S.SettingsHolidaysCountrySearch,
            Finder = new CountryFinder([.. shown.Calendars.Where(calendar => calendar.Region.Length == 0).Select(PublicHolidays.CountryOf)]),
            Pick = found => found.Value is string code && AddCountry(Kept(), code),
        };
        return
        [
            new OptionRow(Calendars, OptionRowKind.Things, S.CoreHolidaysName)
            {
                Glyph = OptionGlyphs.Flag,
                Slot = slot,
                Summary = Summary(shown),
                Owned = new OptionOwned(add, things),
            },
        ];
    }

    /// <summary>What the group says it is set to: "Off", else the countries shown, each once ("Switzerland, Germany").</summary>
    public static string Summary(HolidayOptions options) =>
        PublicHolidays.Calendars(options) is { Count: > 0 } calendars
            ? string.Join(", ", calendars.Select(calendar => HolidayPlaces.Name(calendar.Country)).Distinct())
            : S.CoreHolidaysOff;

    /// <summary>The country as its field shows it at rest: its name, or Windows' region's ("Default (Switzerland)", §518).</summary>
    public static string CountryText(string country) =>
        country.Length > 0 ? HolidayPlaces.Name(country)
        : PublicHolidays.Home() is { Length: > 0 } home ? S.SettingsValueDefault(HolidayPlaces.Name(home))
        : "";

    /// <summary>The region's place among the region row's choices: the whole country's first; -1 for one the list does not have.</summary>
    public static int RegionIndex(HolidayCountry calendar, IReadOnlyList<HolidayRegion> regions)
    {
        if (calendar.Region.Length == 0)
        {
            return 0;
        }
        for (var at = 0; at < regions.Count; at++)
        {
            if (string.Equals(regions[at].Code, calendar.Region, StringComparison.OrdinalIgnoreCase))
            {
                return at + 1;
            }
        }
        return -1;
    }

    /// <summary>
    /// §265: <paramref name="code"/>'s holidays (empty: Windows' region's) one more calendar, after the
    /// others, the whole country's. False where the country's whole calendar is listed already.
    /// </summary>
    public static bool AddCountry(HolidayOptions options, string code)
    {
        var adding = new HolidayCountry { Country = code }.Normalized();
        if (adding.Country != code.Trim().ToUpperInvariant() || options.Calendars.Any(calendar => Shows(calendar, adding)))
        {
            return false;
        }
        options.Calendars.Add(adding);
        return true;
    }

    /// <summary>
    /// §554: whether <paramref name="a"/> and <paramref name="b"/> show the one calendar: the same country — Windows' region's for
    /// one that follows it, so "Default (Switzerland)" and Switzerland are one, as PublicHolidays.Calendars shows them — and the
    /// same region. The panel lists each calendar it shows once.
    /// </summary>
    private static bool Shows(HolidayCountry a, HolidayCountry b) =>
        a.Same(b) || (PublicHolidays.CountryOf(a) is { Length: > 0 } country && country == PublicHolidays.CountryOf(b) && a.Region == b.Region);

    /// <summary>§265: the calendar at <paramref name="at"/> taken out; none left is off. False where there is none there.</summary>
    public static bool Remove(HolidayOptions options, int at)
    {
        if (at < 0 || at >= options.Calendars.Count)
        {
            return false;
        }
        options.Calendars.RemoveAt(at);
        return true;
    }

    /// <summary>
    /// <paramref name="region"/> of <paramref name="country"/> the region of the calendar at
    /// <paramref name="at"/> (empty: the whole country). A region is a country's, so following Windows'
    /// region becomes that country, which a move of Windows' region then no longer changes under it. False
    /// where nothing changed, or where it would be another calendar listed already.
    /// </summary>
    public static bool SetRegion(HolidayOptions options, int at, string country, string region)
    {
        if (at < 0 || at >= options.Calendars.Count)
        {
            return false;
        }
        var calendar = options.Calendars[at];
        if (string.Equals(calendar.Region, region, StringComparison.Ordinal))
        {
            return false;
        }
        var next = new HolidayCountry { Country = region.Length > 0 ? country : calendar.Country, Region = region };
        if (options.Calendars.Any(other => other != calendar && Shows(other, next)))
        {
            return false;
        }
        (calendar.Country, calendar.Region) = (next.Country, next.Region);
        return true;
    }

    /// <summary>
    /// §554: the region of <paramref name="config"/>'s calendar at <paramref name="at"/> set (<see cref="SetRegion(HolidayOptions, int, string, string)"/>),
    /// and every widget that picked that calendar to show (its "Show from", keyed by its region, else its country:
    /// WidgetSources.Holidays) picking it under its new key: a calendar widget showing Switzerland's holidays shows them as
    /// Zürich's on. §455 to §553 the widget lost it.
    /// </summary>
    public static bool SetRegion(Config config, int at, string country, string region)
    {
        var options = config.Holidays ??= new HolidayOptions();
        if (at < 0 || at >= options.Calendars.Count)
        {
            return false;
        }
        var before = WidgetSources.Holidays(options.Calendars[at]);
        if (!SetRegion(options, at, country, region))
        {
            return false;
        }
        var after = WidgetSources.Holidays(options.Calendars[at]);
        foreach (var (_, item) in Folders.All(config.AllIslands))
        {
            if (item.Sources is { } picked && Array.IndexOf(picked, before) is >= 0 and var spot)
            {
                item.Sources = WidgetSources.Normalized([.. picked[..spot], after, .. picked[(spot + 1)..]]);
            }
        }
        return true;
    }

    /// <summary>§423: <paramref name="picked"/> (<c>#RRGGBB</c>; null for the holidays' own) the colour of the calendar at <paramref name="at"/>. False where nothing changed.</summary>
    public static bool SetColour(HolidayOptions options, int at, string? picked)
    {
        var colour = PublicHolidays.Hex(picked);
        if (at < 0 || at >= options.Calendars.Count || string.Equals(options.Calendars[at].Colour, colour, StringComparison.Ordinal))
        {
            return false;
        }
        options.Calendars[at].Colour = colour;
        return true;
    }

    /// <summary>
    /// The service's countries, found by their names in Karpo's language or their codes; Windows' region's
    /// first. Those whose whole country is listed already (<paramref name="listed"/>) are ticked.
    /// </summary>
    private sealed class CountryFinder(IReadOnlyList<string> listed) : OptionFinder
    {
        public override int LeastLetters => 0;

        public override Task<IReadOnlyList<OptionFound>> FindAsync(string query, CancellationToken cancel) => Task.FromResult(Find(query));

        public override string NoneFound(string query) => S.SettingsHolidaysCountryNotFound(query.Trim());

        private IReadOnlyList<OptionFound> Find(string query)
        {
            // Every country that matches, as the menu shows them all (§271).
            var typed = query.Trim();
            var home = PublicHolidays.Home();
            var found = new List<OptionFound>();
            if (home.Length > 0 && (typed.Length == 0 || HolidayPlaces.Name(home).Contains(typed, StringComparison.CurrentCultureIgnoreCase)))
            {
                found.Add(new OptionFound(S.SettingsValueDefault(HolidayPlaces.Name(home)), home, "") { Chosen = listed.Contains(home) });
            }
            var byName = HolidayPlaces.ByName();
            // Before anything is typed, Windows' region's and those listed, then every other (§411: a search always
            // lists something before anything is typed); typed, those whose names hold the words.
            var matches = typed.Length == 0
                ? byName.Where(pair => listed.Contains(pair.Code)).Concat(byName.Where(pair => !listed.Contains(pair.Code)))
                : byName.Where(pair => pair.Name.StartsWith(typed, StringComparison.CurrentCultureIgnoreCase) || string.Equals(pair.Code, typed, StringComparison.OrdinalIgnoreCase))
                    .Concat(byName.Where(pair => !pair.Name.StartsWith(typed, StringComparison.CurrentCultureIgnoreCase) && pair.Name.Contains(typed, StringComparison.CurrentCultureIgnoreCase)));
            foreach (var (code, name) in matches.Distinct())
            {
                found.Add(new OptionFound(name, code, code) { Chosen = listed.Contains(code) });
            }
            return found;
        }
    }
}
