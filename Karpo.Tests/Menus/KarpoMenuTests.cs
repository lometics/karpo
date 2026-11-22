// edit: the folders' room's menu and the edit bar's ⚙ Karpo ▾ (docs\widgets.md §335, §517; Karpo.Core\KarpoMenu*.cs,
// Karpo.Dock\Dock.SettingsMenu.cs). Held here:
//
//  - **The folder room's menu** (§334, LooksScope.Folders) is the menu a right click on an arranged folder's
//    room opens: its Open folders, Taskbar ▾'s row, and the way to Look ▾.
//  - **⚙ Karpo ▾** is KarpoMenu's sections under their headings (§517): a section without rows is not there and a
//    heading without any neither (Karpo Pro, Back up and reset and About Karpo without their services, §339);
//    each level says what it holds; Online's sites written as the General page wrote them; every account, unfiltered;
//    the holidays' level; Quit Karpo (§703 the taskbar menu's alone) asked first, then the host's quit and nothing of the config.
//  - **The dock's menus** are those rows; Quit Karpo (§703 on none of them) goes the host's one way out
//    (DockHost.QuitBy.Menu, Closing).

using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Menus;

public sealed class KarpoMenuTests
{
    private static readonly OptionAccounts Accounts = new(
        [
            new OptionAccount("ms", "me@outlook.com", OptionAccountKind.Microsoft),
            new OptionAccount("imap", "me@example.com", OptionAccountKind.Mail),
            new OptionAccount("dav", "dav.example.com", OptionAccountKind.Dav),
        ],
        _ => { },
        _ => { },
        _ => { },
        new AccountForms((_, _) => Task.FromResult(new AccountTry("")), work => work(), () => { }));

    private static KarpoServices Services(OptionAccounts? accounts = null, Action? quit = null) => new(accounts, quit ?? (() => { }));

    /// <summary><paramref name="services"/> with the third band's (§339): a free licence, Back up and reset, Karpo itself.</summary>
    private static KarpoServices ThirdBand(KarpoServices services) => services with
    {
        Licence = new KarpoLicence(new LicenseStatus(LicenseTier.Free, LicenseBasis.StoreFree), HasStore: true, CanPurchase: true, () => { }, () => { }),
        Backup = new KarpoBackup(() => { }, () => { }, () => { }, _ => { }),
        About = new KarpoAbout("1.4.2", ".NET 10.0.1"),
    };

    /// <summary>
    /// §517 (the person, 2026-11-05: "1. sections"): one page under three headings in a fixed order — General (Start with
    /// Windows, the animations, Language, Formats), Services (Public holidays, Online), Karpo (Karpo Pro, Back up and
    /// reset, About Karpo); §703 (the owner: "dont show quit karpo") no Quit Karpo on it. §535: the
    /// accounts listed or not, no Accounts row (a page of their own); without the licence, the backup and Karpo's own given,
    /// no Karpo section at all.
    /// </summary>
    [Fact]
    public void OnlyTheSectionsWithRowsAreThereUnderTheirHeadings()
    {
        var config = new Config();
        string[] KeysOf(KarpoServices services) => [.. KarpoMenu.Rows(config, services).Select(row => row.Key)];
        string[] general =
        [
            KarpoMenu.GeneralHeading, KarpoMenu.StartupRow, KarpoMenu.LanguageRow,
            KarpoMenu.ShortDateRow, KarpoMenu.LongDateRow, KarpoMenu.TimeRow, KarpoMenu.FirstDayRow, KarpoMenu.TemperatureRow, KarpoMenu.SizesRow, KarpoMenu.RegionRow, KarpoMenu.FollowWindowsRow + "-", KarpoMenu.FollowWindowsRow,
        ];
        string[] online = [KarpoMenu.UpdateRow, KarpoMenu.EngineRow, KarpoMenu.MapRow, KarpoMenu.WeatherRow, KarpoMenu.MarketsRow];

        Assert.Equal([.. general, KarpoMenu.ServicesHeading, OptionHolidays.Calendars, .. online], KeysOf(Services(Accounts)));
        Assert.Equal([.. general, KarpoMenu.ServicesHeading, OptionHolidays.Calendars, .. online], KeysOf(Services()));
        foreach (var section in new Func<Config, KarpoServices, IReadOnlyList<OptionRow>>[] { KarpoMenu.Pro, KarpoMenu.Backup, KarpoMenu.About })
        {
            Assert.Empty(section(config, Services(Accounts)));
        }

        var all = ThirdBand(Services(Accounts));
        var rows = KarpoMenu.Rows(config, all);
        Assert.Equal(
            [KarpoMenu.GeneralHeading, KarpoMenu.ServicesHeading, KarpoMenu.KarpoHeading, KarpoMenu.AboutHeading],
            rows.Where(row => row.Kind == OptionRowKind.Heading && row.Group is null).Select(row => row.Key));
        // §543, §553: the taskbar's menu is not here; it is the Taskbar page's last section.
        Assert.DoesNotContain(rows, row => row.Key.StartsWith(KarpoMenu.BarMenuRow, StringComparison.Ordinal));
        Assert.Equal([S.SettingsGeneralHeader, S.SettingsSectionsServices, S.SettingsSectionsKarpo, S.DockSettingsAbout], rows.Where(row => row.Kind == OptionRowKind.Heading && row.Group is null).Select(row => row.Title));
        var karpo = rows.SkipWhile(row => row.Key != KarpoMenu.KarpoHeading).Skip(1).TakeWhile(row => row.Kind != OptionRowKind.Heading).ToList();
        Assert.Equal([S.SettingsLicenseProTitle, S.DockSettingsBackup], karpo.Select(row => row.Group).Distinct());
        // §703: Quit Karpo on no row of the page.
        Assert.DoesNotContain(OptionRows.Flat(rows), row => row.Key == KarpoMenu.QuitRow || row.Title == S.SettingsAboutQuitHeader);
        // §579: About Karpo the last section, §630 Lometics its last row, §638 its site beside it.
        Assert.Equal([KarpoMenu.MakerRow, KarpoMenu.MakerSiteRow], rows.TakeLast(2).Select(row => row.Key));
    }

    /// <summary>
    /// §517: every row opening a level on ⚙'s page says what it holds at its end where it holds anything set: Formats whether
    /// they follow Windows, Online how often it updates, the holidays their first, Karpo Pro the licence,
    /// §579 Karpo's row (About Karpo a section) the version under its name; Back up and reset, its acts alone, nothing.
    /// </summary>
    [Fact]
    public void ItsLevelsSayWhatTheyHold()
    {
        var items = TheDock.SettingsItems(new Config { Holidays = new HolidayOptions() }, ThirdBand(Services(Accounts)), []);
        string? Said(string title) => items.Single(item => item.Text == title).Value?.Text;
        Assert.Equal(S.SettingsValueDefault(S.SettingsValueWindows), Said(S.SettingsLocalizationFormatsSection));
        Assert.Equal(S.WidgetCommonMinutes(15), Said(S.SettingsOnlineSection));
        Assert.NotNull(Said(S.CoreHolidaysName));
        // §579: the version Karpo's own row's, About Karpo no level any more.
        Assert.Contains("1.4.2", items.Single(item => item.Note && item.Text == "Karpo").Detail, StringComparison.Ordinal);
        Assert.NotNull(Said(S.SettingsLicenseProTitle));
        Assert.Null(Said(S.DockSettingsBackup));
    }

    /// <summary>
    /// Online's sites, a level under "Online": the map, the weather site and the finance site, each its
    /// sites by name, the default ticked while none is chosen; a pick writes the file's key as the
    /// General page's Links cards did, the one in force changes nothing.
    /// </summary>
    [Fact]
    public void OnlinesSitesAreChosenAsTheGeneralPageChoseThem()
    {
        var config = new Config();
        // After the update interval and the search engine (§337).
        IReadOnlyList<OptionRow> Sites() => [.. KarpoMenu.Online(config, Services()).Skip(2)];
        var rows = Sites();
        Assert.All(rows, row => Assert.Equal((S.SettingsOnlineSection, OptionRowKind.Choice), (row.Group, row.Kind)));
        Assert.Equal([S.SettingsMapProviderHeader, S.SettingsLinksWeatherHeader, S.SettingsLinksMarketsHeader], rows.Select(row => row.Title));
        Assert.Equal(LinkProviders.Maps.Select(LinkProviders.Name), rows[0].Choices);
        Assert.Equal(LinkProviders.Weathers.Select(LinkProviders.Name), rows[1].Choices);
        Assert.Equal(LinkProviders.Markets.Select(LinkProviders.Name), rows[2].Choices);
        Assert.Equal(
            [Array.IndexOf(LinkProviders.Maps, LinkProviders.DefaultMap), Array.IndexOf(LinkProviders.Weathers, LinkProviders.DefaultWeather), Array.IndexOf(LinkProviders.Markets, LinkProviders.DefaultMarkets)],
            rows.Select(row => row.Chosen));

        Assert.False(rows[0].Set!(rows[0].Chosen));
        Assert.Null(config.MapProvider);
        Assert.True(rows[0].Set!(Array.IndexOf(LinkProviders.Maps, MapProvider.Google)));
        Assert.True(rows[1].Set!(Array.IndexOf(LinkProviders.Weathers, WeatherProvider.Meteoblue)));
        Assert.True(rows[2].Set!(Array.IndexOf(LinkProviders.Markets, MarketsProvider.TradingView)));
        Assert.Equal((MapProvider.Google, WeatherProvider.Meteoblue, MarketsProvider.TradingView), (config.MapProvider, config.WeatherProvider, config.MarketsProvider));
        Assert.False(rows[0].Set!(LinkProviders.Maps.Length));
        Assert.Equal(Array.IndexOf(LinkProviders.Maps, MapProvider.Google), Sites()[0].Chosen);
    }

    /// <summary>
    /// §535: the Accounts page: every account, of every kind, §324's level unfiltered, under "Your accounts" — each account
    /// listed on the page itself, a Microsoft account's "Sign in again", each one's Remove asked first —, then under "Add an
    /// account" every kind to add. None of it on the Karpo page.
    /// </summary>
    [Fact]
    public void TheAccountsAreEveryAccount()
    {
        var page = KarpoMenu.Accounts(new Config(), Services(Accounts));
        Assert.Equal(
            [OptionAccountRows.YoursHeading, OptionAccountRows.Accounts, OptionAccountRows.AddHeading, OptionAccountRows.AddMicrosoft, OptionAccountRows.AddGoogle, OptionAccountRows.Form + "Mail", OptionAccountRows.Form + "Dav"],
            page.Select(row => row.Key));
        Assert.Equal(KarpoMenu.Accounts(new Config(), Services(Accounts)).Select(row => row.Key), KarpoMenu.Of(KarpoPage.Accounts, new Config(), Services(Accounts)).Select(row => row.Key));
        Assert.DoesNotContain(KarpoMenu.Rows(new Config(), Services(Accounts)), row => row.Key.StartsWith(OptionAccountRows.Accounts, StringComparison.Ordinal));
        var row = page[1];
        Assert.Equal((OptionRowKind.Things, S.SettingsNavAccounts, true), (row.Kind, row.Title, row.Inline));
        var owned = row.Owned!;
        // §411: a way to add each kind, right under the accounts, under its own heading.
        Assert.Null(owned.Add);
        Assert.Equal([S.SettingsAccountsYours, S.SettingsAccountsAddHeader, S.SettingsAccountsAddMicrosoft, S.SettingsAccountsAddGoogle, S.SettingsAccountsAddMail, S.SettingsAccountsAddDav], page.Where(each => each.Kind != OptionRowKind.Things).Select(each => each.Title));
        Assert.Equal(["me@outlook.com", "me@example.com", "dav.example.com"], owned.Things.Select(thing => thing.Title));
        // Not listed: no page at all.
        Assert.Empty(KarpoMenu.Accounts(new Config(), Services()));
        Assert.Equal([true, false, false], owned.Things.Select(thing => thing.Acts.Count > 0));
        Assert.All(owned.Things, thing => Assert.NotNull(thing.Delete!.Ask));
    }

    /// <summary>
    /// §716 (the person, 2026-11-22: "current version should not have google sign in enabled … or hidden i mean"):
    /// a Karpo that cannot sign in to Google has no "Add a Google account" at all, where it had a greyed one. A
    /// Microsoft account it cannot sign in to stays, greyed, saying why.
    /// </summary>
    [Fact]
    public void AKarpoWithoutGoogleSignInNamesNoGoogleAccount()
    {
        var without = KarpoMenu.Accounts(new Config(), Services(Accounts with { CanSignIn = kind => kind != OptionAccountKind.Google }));
        Assert.Equal(
            [OptionAccountRows.YoursHeading, OptionAccountRows.Accounts, OptionAccountRows.AddHeading, OptionAccountRows.AddMicrosoft, OptionAccountRows.Form + "Mail", OptionAccountRows.Form + "Dav"],
            without.Select(row => row.Key));
        var neither = KarpoMenu.Accounts(new Config(), Services(Accounts with { CanSignIn = _ => false }));
        Assert.DoesNotContain(neither, row => row.Key == OptionAccountRows.AddGoogle);
        var microsoft = Assert.Single(neither, row => row.Key == OptionAccountRows.AddMicrosoft);
        Assert.False(microsoft.Enabled);
        Assert.NotNull(microsoft.Why);
    }

    /// <summary>Public holidays: the calendars' level every calendar widget has (OptionHolidays.Rows), written to the same config.</summary>
    [Fact]
    public void ThePublicHolidaysAreTheCalendarsLevel()
    {
        var config = new Config();
        var row = Assert.Single(KarpoMenu.Holidays(config, Services()));
        Assert.Equal((OptionHolidays.Calendars, S.CoreHolidaysName), (row.Key, row.Title));
        Assert.True(row.Owned!.Add!.Pick!(new OptionFound("Switzerland", "CH", "CH")));
        Assert.Equal("CH", Assert.Single(config.Holidays!.Calendars).Country);
    }

    /// <summary>
    /// §423 (the person, 2026-10-30: "the color for the public holidays should be settable per country / entry"): each
    /// calendar has its colour, as the Accounts page's swatch offered it — Automatic (the holidays' red, its swatch at
    /// the row's end), one of the accounts' calendars' twelve, or one's own —, written to that calendar's "colour" as
    /// "#RRGGBB", Automatic as none; the others' untouched. No colour of them all any more. In the menu a palette button
    /// at the calendar's row's end, beside its bin, opens the swatches in place; the row still opens its regions
    /// themselves (§411, R1: nothing a level deeper).
    /// </summary>
    [Fact]
    public void EachPublicHolidayCalendarHasItsOwnColour()
    {
        var config = new Config { Holidays = new HolidayOptions { Calendars = [new HolidayCountry { Country = "CH", Region = "CH-ZH" }, new HolidayCountry { Country = "GB" }] } };
        var row = Assert.Single(KarpoMenu.Holidays(config, Services()));
        Assert.Empty(row.Owned!.After);
        OptionRow ColourOf(int at) => Assert.Single(KarpoMenu.Holidays(config, Services())).Owned!.Things[at].Colour!;
        Assert.Equal([OptionHolidays.Region + 0], row.Owned.Things[0].Rows.Select(each => each.Key));
        Assert.Equal([OptionHolidays.Colour + 0, OptionHolidays.Colour + 1], row.Owned.Things.Select(thing => thing.Colour!.Key));
        var colour = ColourOf(0);
        Assert.Equal((OptionRowKind.Color, S.SettingsAccountsColorHeader, 0), (colour.Kind, colour.Title, colour.Chosen));

        List<OptionPick> picks = [];
        var swiss = OptionMenu.Rows(KarpoMenu.Holidays(config, Services()), picks).Single().Children![0];
        // §609: its region in its row on the settings panel, before its buttons (§554 to §608 a row nested under it).
        Assert.True(Assert.Single(swiss.Children!).Beside);
        Assert.Contains(Assert.Single(swiss.Children!).Children!, child => child.Text == "Zürich");
        var tools = Assert.IsType<FlyoutControl.Tools>(swiss.Control).Buttons;
        Assert.Equal([S.SettingsAccountsColorHeader, S.DockMenuRemove], tools.Select(tool => tool.Name));
        // §452: the button a swatch of the colour the calendar wears, Automatic the holidays' red (§423 had the palette's glyph).
        Assert.Equal(Argb.Parse(PublicHolidays.DefaultColour), tools[0].Swatch);
        // §518: unset "Default (…)" with the colour it wears, the holidays' red's name or value.
        var unset = S.SettingsValueDefault(PartColours.Name(PublicHolidays.DefaultColour));
        Assert.Equal(unset, tools[0].Level![0].Text);
        Assert.Contains(tools[0].Level!, item => OptionMenu.PickOf(picks, item.Id) is { Kind: OptionPickKind.Set, Key: OptionHolidays.Colour + "0" });
        Assert.Equal(Argb.Parse(PublicHolidays.DefaultColour), colour.Color);
        // §690: the presets every colour picker has.
        Assert.Equal([unset, .. ColorPalette.Presets.Select(each => ColorPalette.NameOf(each) ?? PartColours.Hex(each))], colour.Tiles.Select(tile => tile.Name));

        var blue = ColorPalette.Presets.ToList().IndexOf(Argb.Parse("#0078D4")!.Value) + 1;
        Assert.True(colour.Set!(blue));
        Assert.Equal(["#0078D4", null], config.Holidays.Calendars.Select(calendar => calendar.Colour));
        Assert.Equal(blue, ColourOf(0).Chosen);
        Assert.Equal(Argb.Parse("#0078D4"), ColourOf(0).Color);
        var now = OptionMenu.Rows(KarpoMenu.Holidays(config, Services()), []).Single().Children![0];
        Assert.Equal(Argb.Parse("#0078D4"), Assert.IsType<FlyoutControl.Tools>(now.Control).Buttons[0].Swatch);
        Assert.False(ColourOf(0).Set!(blue));
        Assert.False(ColourOf(1).SetColor!(new Argb(0x80, 0x12, 0x34, 0x56), false));
        Assert.True(ColourOf(1).SetColor!(new Argb(0x80, 0x12, 0x34, 0x56), true));
        Assert.Equal(["#0078D4", "#123456"], config.Holidays.Calendars.Select(calendar => calendar.Colour));
        Assert.True(ColourOf(0).Set!(0));
        Assert.Equal([null, "#123456"], config.Holidays.Calendars.Select(calendar => calendar.Colour));
    }

    /// <summary>
    /// Quit Karpo: an action, asked first in Windows' dialog with the About page's words; acted, it
    /// calls the way out once and changes nothing of the config.
    /// </summary>
    [Fact]
    public void QuitKarpoAsksThenTakesTheWayOut()
    {
        var quits = 0;
        var row = Assert.Single(KarpoMenu.Quit(new Config(), Services(quit: () => quits++)));
        Assert.Equal((OptionRowKind.Action, S.SettingsAboutQuitHeader, OptionGlyphs.Power), (row.Kind, row.Title, row.Glyph));
        Assert.Equal(new DeleteQuestion(S.SettingsAboutQuitHeader, S.SettingsAboutQuitDescription, S.SettingsAboutQuitButton, S.DockDeleteCancel, 0), row.Ask);
        Assert.Equal(0, quits);
        Assert.False(OptionMenu.ApplyRow(row, new OptionPick(OptionMenu.FirstId, OptionPickKind.Act, Key: KarpoMenu.QuitRow)));
        Assert.Equal(1, quits);
    }

    /// <summary>
    /// The folders' look (§334) is the menu a right click on an arranged folder's room opens
    /// (LooksScope.Folders), named "Folders · Default" (§364, the layout's); §352, §400: Layout ▾'s Folders page has the same rows, keyed
    /// the same rows as Layout ▾'s (§405).
    /// </summary>
    [Fact]
    public void TheFoldersMenuIsTheFoldersLook()
    {
        var config = new Config();
        var bar = new BarSpot("display", "One");
        var folders = BarLooks.FolderRows(config.DefaultLayout).Select(row => row.Key).ToList();
        // §517: and under a rule the way to Look ▾ (§423 to §513 its foot, Layout ▾).
        Assert.Equal([.. folders, BarLooks.AppearanceLinkRow + "-", BarLooks.AppearanceLinkRow], BarLooks.Rows(config, bar, dark: true, scope: LooksScope.Folders).Select(row => row.Key));
        Assert.Equal(S.DockEditOfLayout(S.CoreCategoryFolders, config.DefaultLayout.Name), BarLooks.Kind(config, bar, LooksScope.Folders));
        // §405: the same rows as Taskbar ▾ has them, the same keys: one setting, one row.
        var layout = OptionRows.Flat(BarLooks.Rows(config, bar, dark: true, scope: LooksScope.Taskbar)).Select(row => row.Key).ToList();
        Assert.All(folders, key => Assert.Contains(key, layout));
    }

    /// <summary>
    /// ⚙ Karpo ▾ in the dock (§517): Karpo's sections under their headings, §703 no Quit Karpo, every row's
    /// command kept to find it again.
    /// </summary>
    [Fact]
    public void TheSettingsMenuIsTheSectionsUnderTheirHeadings()
    {
        List<OptionPick> picks = [];
        var items = TheDock.SettingsItems(new Config(), ThirdBand(Services(Accounts)), picks);
        Assert.Equal(
            [
                "h:" + S.SettingsGeneralHeader, S.SettingsGeneralStartupHeader, S.SettingsLanguageSection, S.SettingsLocalizationFormatsSection,
                "h:" + S.SettingsSectionsServices, S.CoreHolidaysName, S.SettingsOnlineSection,
                "h:" + S.SettingsSectionsKarpo, S.SettingsLicenseProTitle, S.DockSettingsBackup,
                // §579: About Karpo a section of its own.
                "h:" + S.DockSettingsAbout, "Karpo", "Lometics GmbH", KarpoMenu.MakerSiteName,
            ],
            items.Select(item => item.Separator ? "" : item.Header ? "h:" + item.Text : item.Text));
        Assert.Equal(KarpoMenu.UpdateRow, OptionMenu.PickOf(picks, items[6].Id)?.Key);
        // §545: nothing stands still at the menu's foot (§409 to §544 Quit Karpo did).
        Assert.Equal(items.Count, Flyout.FootFrom(items));
    }

    /// <summary>
    /// §520: the settings panel's pages do it the dock's way: the Karpo page the menu ⚙ was (its picks SettingsPicked's), the
    /// Display, Taskbar and Look pages the bar's looks (no Taskbar nor Look on a display that shows None), the folder's page
    /// its panel's settings; the settings app is no longer opened from the edit bar; a pick, a field and (§523) a colour mixed
    /// go through the rows; Quit Karpo (§703 the taskbar menu's, §532) posts the host's marked close, which the host takes for a
    /// quit and says so; §575 no side list's footer.
    /// </summary>
    [Fact]
    public void ThePanelsPagesAreTheDocksWay()
    {
        var repo = RepoSource.Root();
        string Read(string file) => File.ReadAllText(Path.Combine(repo, "Karpo.Dock", file));
        var panel = Read("Dock.SettingsPanel.cs");
        Assert.Contains("new Pending(Topic.LayoutEditor, owner) { SettingsMenu = new SettingsMenu(id, KarpoPage.Karpo) },", panel);
        // §535: the Accounts page the same way, its rows the accounts'.
        Assert.Contains("new Pending(Topic.LayoutEditor, owner) { SettingsMenu = new SettingsMenu(id, KarpoPage.Accounts) },", panel);
        Assert.Contains("if (own is null && scope == LooksScope.Taskbar)", panel);
        Assert.Contains("OptionMenu.Settings(TargetsOf(config, [key]), picks, FolderSide.Panel,", panel);
        // §575: no side list, no footer: what is placed against the licence is the Karpo page's Karpo Pro.
        Assert.DoesNotContain("LicenseView.Footer", panel);
        Assert.DoesNotContain("item.Foot", panel);
        Assert.Contains("Chosen(built.What, id);", panel);
        Assert.DoesNotContain("OpenSettings(", Read("Dock.EditDeck.cs"));
        var menu = Read("Dock.SettingsMenu.cs");
        Assert.Contains("PostMessageW(host, WM.WM_CLOSE, (uint)DockHost.QuitBy.Menu, 0)", menu);
        Assert.Contains("SettingsPicked(what, pick, list);", Read("Dock.Folders.cs"));
        Assert.Contains("SettingsKept(settings.Page, pick, kept);", Read("Dock.MenuFields.cs"));
        var pickers = Read("Dock.MenuPickers.cs");
        Assert.Contains("return of => SettingsRow(of, settings.Page, pick);", pickers);
        Assert.Contains("return of => LooksRow(of, bar, pick, scope);", pickers);
        Assert.Contains("Action back = what.Drops || key.Length == 0 ? static () => { } :", pickers);

        Assert.Equal(QuitWay.Menu, Closing.Way((uint)DockHost.QuitBy.Menu));
        Assert.Equal(ExitKind.Quit, DockExit.Kind((uint)DockHost.QuitBy.Menu));
        Assert.Contains("edit bar", Closing.Line(QuitWay.Menu), StringComparison.Ordinal);
    }
}
