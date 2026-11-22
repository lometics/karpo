// edit: ⚙ Settings' general sections and the taskbar's behaviour (docs\widgets.md §337; Karpo.Core\
// KarpoMenu.Startup.cs, .Language.cs, .Formats.cs, .Online.cs, BarLooks.Behaviour). Held here:
//
//  - **Every row writes the key the settings app's page writes**, read off the file: Start with Windows,
//    the language, each format, the update interval, the search engine and its address, and the
//    taskbar's behaviour.
//  - **Start with Windows** turns through Windows' side as the General page did (StartupSwitch), here a
//    test's seam: off removes the entry at once, on clears Task Manager's "disabled"; its Why line says
//    what holds against it. Nothing of the registry or the logon task is touched.
//  - **The language** lists Windows default first, each language in its own words; a pick takes hold, and
//    the menu made again speaks it; the dock opens ⚙ again after it.
//  - **The formats** are the Localization page's lists; Follow Windows greyed while everything follows.
//  - **Online**: an interval the file has that is none of the choices still shows; the engines wear their
//    logos; the address only while it is the person's own, saying what is wrong with it.

using System.Text.Json;
using System.Text.Json.Nodes;
using Karpo.Dock;
using Karpo.Tests.Localization;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Menus;

public sealed class KarpoMenuGeneralTests
{
    /// <summary>Windows' side of "Start with Windows" as a test holds it: what it reads, what was asked of it.</summary>
    private sealed class FakeStartup(StartupState state)
    {
        public StartupState State { get; set; } = state;

        public int Unregistered { get; private set; }

        public int Approved { get; private set; }

        public StartupSwitch Switch => new(() => State, () => ++Unregistered > 0, () => ++Approved > 0);
    }

    private static readonly StartupState Registered = new("\"C:\\Karpo\\Karpo.exe\" --startup", "C:\\Karpo\\Karpo.exe", true, StartupApproval.NotSet);

    private static KarpoServices Services(FakeStartup? startup = null, string[]? windows = null, Action<string>? open = null) =>
        new(null, () => { })
        {
            Startup = (startup ?? new FakeStartup(Registered)).Switch,
            Windows = () => windows ?? ["en-US"],
            Open = open ?? (_ => { }),
        };

    /// <summary>The file's keys and values, every leaf by its path ("formats.shortDate").</summary>
    private static Dictionary<string, string> Leaves(Config config)
    {
        Dictionary<string, string> leaves = [];
        void Walk(JsonNode? node, string path)
        {
            switch (node)
            {
                case JsonObject map:
                    foreach (var (key, value) in map)
                    {
                        Walk(value, path.Length == 0 ? key : path + "." + key);
                    }
                    break;
                case JsonArray list:
                    // §345: a layout's own settings by its place in the list ("layouts.0.combine").
                    for (var at = 0; at < list.Count; at++)
                    {
                        Walk(list[at], path + "." + at);
                    }
                    leaves[path] = list.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    break;
                default:
                    leaves[path] = node?.ToJsonString() ?? "null";
                    break;
            }
        }
        Walk(JsonNode.Parse(config.ToJson()), "");
        return leaves;
    }

    /// <summary>The paths of the file <paramref name="change"/> changes in a new config, as it is saved.</summary>
    private static string[] KeysWritten(Action<Config> change)
    {
        var config = new Config();
        // The bar's menu makes the display's layout as it is read: made already, so it is no change.
        _ = config.ActiveLayout("display");
        var before = Leaves(config);
        change(config);
        var after = Leaves(config);
        return [.. before.Keys.Union(after.Keys).Where(key => before.GetValueOrDefault(key) != after.GetValueOrDefault(key)).Order(StringComparer.Ordinal)];
    }

    private static OptionRow Row(IReadOnlyList<OptionRow> rows, string key) => OptionRows.Find(rows, key)!;

    /// <summary>
    /// Every row of ⚙'s general sections and of the taskbar's behaviour writes the very key the settings
    /// app's General and Localization pages write (SettingsModel: StartWithWindows, LanguageIndex, the
    /// formats' indices, OnlineUpdateIndex, SearchEngineIndex, SearchCustomUrl, CombineIndex,
    /// PreviewsIndex, HoverFlyoutsIndex), and nothing else.
    /// </summary>
    [Fact]
    public void EveryRowWritesThePagesKey()
    {
        var startup = new FakeStartup(Registered);
        KarpoServices With() => Services(startup);
        string[] Set(Func<Config, IReadOnlyList<OptionRow>> rows, string key, int value) =>
            KeysWritten(config => Assert.True(Row(rows(config), key).Set!(value), key));

        Assert.Equal(["startWithWindows"], Set(config => KarpoMenu.Startup(config, With()), KarpoMenu.StartupRow, 0));
        Assert.Equal(["language"], KeysWritten(config => Assert.True(LanguageSearchTests.Pick(Row(KarpoMenu.Language(config, With()), KarpoMenu.LanguageRow), "de"))));
        Assert.Equal(["formats.firstDayOfWeek"], Set(config => KarpoMenu.Formats(config, With()), KarpoMenu.FirstDayRow, 2));
        Assert.Equal(["formats.temperature"], Set(config => KarpoMenu.Formats(config, With()), KarpoMenu.TemperatureRow, 2));
        Assert.Equal(["formats.sizes"], Set(config => KarpoMenu.Formats(config, With()), KarpoMenu.SizesRow, 2));
        foreach (var (key, file, patterns) in new[]
        {
            (KarpoMenu.ShortDateRow, "formats.shortDate", RegionFormats.DateFormats(@long: false)),
            (KarpoMenu.LongDateRow, "formats.longDate", RegionFormats.DateFormats(@long: true)),
            (KarpoMenu.TimeRow, "formats.time", RegionFormats.TimeFormats()),
        })
        {
            // The region's own patterns, as the page lists them; this PC's region has some.
            Assert.NotEmpty(patterns);
            Assert.Equal([file], Set(config => KarpoMenu.Formats(config, With()), key, 1));
        }
        Assert.Equal(["onlineUpdateMinutes"], Set(config => KarpoMenu.Online(config, With()), KarpoMenu.UpdateRow, OnlineUpdateRow.IndexOf(60)));
        Assert.Equal(["searchEngine"], Set(config => KarpoMenu.Online(config, With()), KarpoMenu.EngineRow, Array.IndexOf(SearchUrls.All, SearchEngine.DuckDuckGo)));
        Assert.Equal(["searchCustomUrl", "searchEngine"], KeysWritten(config =>
        {
            config.SearchEngine = SearchEngine.Custom;
            Assert.True(Row(KarpoMenu.Online(config, With()), KarpoMenu.AddressRow).SetText!("  https://example.org/?s={q} "));
            Assert.Equal("https://example.org/?s={q}", config.SearchCustomUrl);
        }));
        // The behaviour is the layout shown's.
        var bar = new BarSpot("display", "One");
        Assert.Equal(["layouts.0.combine"], Set(config => BarLooks.Rows(config, bar, dark: true, scope: LooksScope.Taskbar), BarLooks.CombineRow, Array.IndexOf(BarLooks.Combines, ButtonCombine.Never)));
        Assert.Equal(["layouts.0.previews"], Set(config => BarLooks.Rows(config, bar, dark: true, scope: LooksScope.Taskbar), BarLooks.PreviewsRow, 0));
        Assert.Equal(["layouts.0.openOnHover"], Set(config => BarLooks.Rows(config, bar, dark: true, scope: LooksScope.Taskbar), BarLooks.HoverRow, 1));
    }

    /// <summary>
    /// Start with Windows, through a test's seam: a toggle, on while the setting is and Task Manager has
    /// not turned it off; off removes the entry at once; on again after Task Manager clears its
    /// "disabled" and changes no setting; what holds against it is its Why line, nothing where it is
    /// plainly on or off.
    /// </summary>
    [Fact]
    public void StartWithWindowsTurnsThroughWindowsSide()
    {
        var startup = new FakeStartup(Registered);
        var config = new Config();
        OptionRow Toggle() => Assert.Single(KarpoMenu.Startup(config, Services(startup)), row => row.Key == KarpoMenu.StartupRow);

        Assert.Equal((OptionRowKind.Toggle, S.SettingsGeneralStartupHeader, true, null), (Toggle().Kind, Toggle().Title, Toggle().On, Toggle().Why));
        Assert.True(OptionMenu.ApplyRow(Toggle(), new OptionPick(OptionMenu.FirstId, OptionPickKind.Flip, Key: KarpoMenu.StartupRow)));
        Assert.Equal((false, 1, 0), (config.StartWithWindows, startup.Unregistered, startup.Approved));
        Assert.Equal((false, null), (Toggle().On, Toggle().Why));

        Assert.True(Toggle().Set!(1));
        Assert.Equal((true, 1, 0), (config.StartWithWindows, startup.Unregistered, startup.Approved));
        Assert.False(Toggle().Set!(1));

        startup.State = Registered with { Approval = StartupApproval.Disabled };
        Assert.Equal((false, S.SettingsGeneralStartupTurnedOff), (Toggle().On, Toggle().Why));
        Assert.False(Toggle().Set!(1));
        Assert.Equal((true, 1, 1), (config.StartWithWindows, startup.Unregistered, startup.Approved));

        startup.State = new StartupState(null, null, false, StartupApproval.NotSet);
        Assert.Equal((true, S.SettingsGeneralStartupNotYet), (Toggle().On, Toggle().Why));
        startup.State = Registered with { ProgramExists = false };
        Assert.Equal(S.SettingsGeneralStartupAgain, Toggle().Why);
        Assert.Equal(S.SettingsGeneralStartupAgain, StartupSwitch.Said(config, startup.State));
        config.StartWithWindows = false;
        Assert.Equal(S.SettingsGeneralStartupOff, StartupSwitch.Said(config, startup.State));
    }

    /// <summary>
    /// The language: "Default (Deutsch)" first (§518), naming Windows' language in its own words, then every
    /// language Karpo has in its own words — §631 a search's finds, every one before anything is typed —; a pick writes
    /// the code, the default writes none, the one in force nothing.
    /// </summary>
    [Fact]
    public void TheLanguagesAreWindowsDefaultThenEachInItsOwnWords()
    {
        var config = new Config();
        var row = Assert.Single(KarpoMenu.Language(config, Services(windows: ["de-CH", "en-US"])));
        Assert.Equal((OptionRowKind.Search, S.SettingsLanguageSection, S.SettingsLanguageSearch), (row.Kind, row.Title, row.Placeholder));
        var all = LanguageSearchTests.Finds(row, "");
        Assert.Equal("Default (Deutsch)", all[0].Title);
        Assert.True(all[0].Chosen);
        // §518: the row's end says the default as its choice does.
        Assert.Equal(all[0].Title, row.Summary);
        Assert.Equal(Languages.Offered.Select(Languages.NativeName), all.Skip(1).Select(found => found.Title));

        Assert.False(LanguageSearchTests.Pick(row, ""));
        Assert.True(LanguageSearchTests.Pick(row, "fr"));
        Assert.Equal("fr", config.Language);
        var again = Assert.Single(KarpoMenu.Language(config, Services()));
        Assert.Equal("Français", again.Summary);
        Assert.Equal("Français", Assert.Single(LanguageSearchTests.Finds(again, ""), found => found.Chosen).Title);
        Assert.True(LanguageSearchTests.Pick(again, ""));
        Assert.Null(config.Language);
    }

    /// <summary>
    /// The formats: each list the Localization page's, Windows' own first, "Default (…)" with what it comes to (§518),
    /// the row's end saying it too; Reset all greyed while everything follows, and putting every format back; Windows'
    /// region settings opened by the dock.
    /// </summary>
    [Fact]
    public void TheFormatsAreTheLocalizationPagesLists()
    {
        var config = new Config();
        List<string> opened = [];
        IReadOnlyList<OptionRow> Rows() => KarpoMenu.Formats(config, Services(open: opened.Add));
        var rows = Rows();
        Assert.All(rows, row => Assert.Equal(S.SettingsLocalizationFormatsSection, row.Group));
        // §411: the region's settings, then under a rule Reset all, last.
        Assert.Equal(
            [S.SettingsLocalizationShortDateHeader, S.SettingsLocalizationLongDateHeader, S.SettingsLocalizationTimeHeader, S.SettingsLocalizationFirstDayHeader, S.SettingsLocalizationTemperatureHeader, S.SettingsLocalizationSizesHeader, S.SettingsLocalizationRegionHeader, "", S.SettingsAppearanceCustomResetAll],
            rows.Select(row => row.Title));
        var shortDates = RegionFormats.DateFormats(@long: false);
        Assert.Equal(FormatChoices.ShortDates(shortDates), Row(rows, KarpoMenu.ShortDateRow).Choices);
        Assert.Equal(FormatChoices.FirstDays, Row(rows, KarpoMenu.FirstDayRow).Choices);
        // §517 (R4): whether they follow Windows at its row's end.
        Assert.Equal(S.SettingsValueDefault(S.SettingsValueWindows), rows[0].GroupSummary);
        Assert.All(rows.Take(6), row => Assert.Equal(0, row.Chosen));
        Assert.All(rows.Take(6), row => Assert.Equal((true, row.Choices[0]), (row.Choices[0].StartsWith("Default (", StringComparison.Ordinal), row.SaidOf(0))));
        Assert.Equal(S.SettingsValueDefault(FormatChoices.FirstDays[1 + (int)RegionFormats.FollowedFirstDay()]), Row(rows, KarpoMenu.FirstDayRow).Choices[0]);
        Assert.Equal(S.SettingsValueDefault(RegionFormats.FollowedTime(FormatChoices.Sample)!), Row(rows, KarpoMenu.TimeRow).Choices[0]);
        Assert.Equal("Default (KB, MB, GB)", Row(rows, KarpoMenu.SizesRow).SaidOf(0));
        Assert.False(Row(rows, KarpoMenu.FollowWindowsRow).Enabled);

        Assert.True(Row(Rows(), KarpoMenu.ShortDateRow).Set!(1));
        Assert.True(Row(Rows(), KarpoMenu.FirstDayRow).Set!(FormatChoices.FirstDayIndex(DayOfWeek.Monday)));
        Assert.Equal((shortDates[0], DayOfWeek.Monday), (config.Formats.ShortDate, config.Formats.FirstDayOfWeek));
        Assert.Equal(1, Row(Rows(), KarpoMenu.ShortDateRow).Chosen);
        var follow = Row(Rows(), KarpoMenu.FollowWindowsRow);
        Assert.True(follow.Enabled);
        Assert.True(OptionMenu.ApplyRow(follow, new OptionPick(OptionMenu.FirstId, OptionPickKind.Act, Key: KarpoMenu.FollowWindowsRow)));
        Assert.True(config.Formats.Follows);

        Assert.False(Row(Rows(), KarpoMenu.RegionRow).Set!(0));
        Assert.Equal([KarpoMenu.RegionSettings], opened);
    }

    /// <summary>
    /// Online: the interval, the engine and, while it is the person's own, its address before the sites;
    /// an interval the file has that is none of the choices still shows, ticked, where it falls; each
    /// engine wears its logo, the person's own the globe; the address says what is wrong with it and
    /// offers the engines' own addresses.
    /// </summary>
    [Fact]
    public void OnlineHasTheIntervalAndTheSearchEngineBeforeTheSites()
    {
        var config = new Config { OnlineUpdateMinutes = 45 };
        var rows = KarpoMenu.Online(config, Services());
        Assert.Equal([KarpoMenu.UpdateRow, KarpoMenu.EngineRow, KarpoMenu.MapRow, KarpoMenu.WeatherRow, KarpoMenu.MarketsRow], rows.Select(row => row.Key));
        var update = rows[0];
        Assert.Equal([5, 15, 30, 45, 60, 120, 180], OnlineUpdateRow.MinutesWith(45));
        Assert.Equal((OnlineUpdateRow.Say(45), 3), (update.Choices[3], update.Chosen));
        Assert.False(update.Set!(3));

        var engine = rows[1];
        Assert.Equal(SearchUrls.All.Select(each => each.Info().Name), engine.Choices);
        Assert.Equal(Array.IndexOf(SearchUrls.All, SearchUrls.Default), engine.Chosen);
        Assert.Equal(["search-google-32.png", "search-bing-32.png", "search-duckduckgo-32.png", "search-ecosia-32.png", "search-startpage-32.png", "search-brave-32.png", ""], engine.ChoicePictures);
        Assert.Equal(SiteLogo.Glyph, engine.ChoiceGlyph(SearchUrls.All.Length - 1));
        // §597: every map, weather site and finance site its own logo, in the order the choices list them.
        Assert.Equal(["map-openstreetmap-32.png", "map-google-32.png", "map-bing-32.png", "map-apple-32.png", "map-here-32.png"], rows[2].ChoicePictures);
        Assert.Equal(["weather-weathercom-32.png", "weather-msn-32.png", "weather-yr-32.png", "weather-meteoblue-32.png"], rows[3].ChoicePictures);
        Assert.Equal(["markets-yahoo-32.png", "markets-google-32.png", "markets-tradingview-32.png"], rows[4].ChoicePictures);
        Assert.Equal([LinkProviders.Maps.Length, LinkProviders.Weathers.Length, LinkProviders.Markets.Length], rows.Skip(2).Select(row => row.Choices.Count));

        Assert.True(engine.Set!(Array.IndexOf(SearchUrls.All, SearchEngine.Custom)));
        var address = Row(KarpoMenu.Online(config, Services()), KarpoMenu.AddressRow);
        Assert.Equal((OptionRowKind.Text, S.SettingsSearchNeeded("Google"), SearchUrls.Example), (address.Kind, address.Why, address.Placeholder));
        Assert.Contains(address.Finder!.Suggested, found => found.Value as string == SearchEngine.DuckDuckGo.Info().Search);
        Assert.True(address.SetText!("example.org"));
        Assert.Equal(S.CoreWebSearchBadScheme, Row(KarpoMenu.Online(config, Services()), KarpoMenu.AddressRow).Why);
        Assert.True(Row(KarpoMenu.Online(config, Services()), KarpoMenu.AddressRow).SetText!(""));
        Assert.Null(config.SearchCustomUrl);
    }

    /// <summary>
    /// The taskbar's behaviour (Q4 A): §400, Layout ▾'s Behavior page, its four rows, in the layout's menu and
    /// the whole, never in the folders' menu, which keeps its own "Open
    /// folders": combine the app buttons in Windows' three ways, window previews, the windows of this desktop or every one (§362), the widgets' flyouts on
    /// hover — the layout's own (§345). Hide the Windows taskbar is no layout's: ⚙ Settings' general band
    /// has it, after Start with Windows, a toggle.
    /// </summary>
    [Fact]
    public void TheTaskbarsBehaviourIsAGroupOfItsMenu()
    {
        var config = new Config();
        var bar = new BarSpot("display", "One");
        string[] behaviour = [BarLooks.CombineRow, BarLooks.PreviewsRow, BarLooks.DesktopsRow, BarLooks.HoverRow];
        foreach (var scope in new[] { LooksScope.Taskbar })
        {
            // §517: Taskbar ▾'s "Behavior" heading over them (§400 to §513 a page of their own).
            var rows = BarLooks.Rows(config, bar, dark: true, scope: scope).ToList();
            var heading = Assert.Single(rows, row => row.Key == BarLooks.BehaviourHeading);
            Assert.Equal((OptionRowKind.Heading, S.DockTaskbarBehaviour), (heading.Kind, heading.Title));
            var page = rows.Skip(rows.IndexOf(heading) + 1).Take(behaviour.Length).ToList();
            Assert.Equal(behaviour, page.Select(row => row.Key));
            Assert.All(page, row => Assert.Equal(((string?)null, OptionSlot.Behaviour), (row.Group, row.Slot)));
        }
        var folders = BarLooks.Rows(config, bar, dark: true, scope: LooksScope.Folders);
        // The last of its own, over its way to Look ▾ (§517).
        Assert.Equal(BarLooks.HoverRow, folders[^3].Key);
        Assert.Null(folders[^3].Group);

        var group = BarLooks.Behaviour(config.DefaultLayout);
        Assert.Equal([S.SettingsGeneralCombineAlways, S.SettingsGeneralCombineWhenFull, S.SettingsGeneralCombineNever], group[0].Choices);
        Assert.Equal((int)config.DefaultLayout.Combine, group[0].Chosen);
        // §411 (R3): on or off, a switch.
        Assert.Equal((OptionRowKind.Toggle, true), (group[1].Kind, group[1].On));
        Assert.False(group[1].Set!(1));
        // §362: the windows of this desktop only, as Windows has it unless told otherwise; all of them, the layout's own.
        Assert.Equal([S.SettingsGeneralDesktopsThis, S.SettingsGeneralDesktopsAll], group[2].Choices);
        Assert.Equal(0, group[2].Chosen);
        Assert.True(group[2].Set!(1));
        Assert.True(config.DefaultLayout.AllDesktops);
        Assert.False(group[2].Set!(1));
        // §489: no folder animation of the layout's: Karpo moves one way, set in ⚙ › General.
        Assert.DoesNotContain(group, row => row.Title.Contains("anim", StringComparison.OrdinalIgnoreCase));

        // In the dock's menu: the heading "Behavior", the five under it, §585 the taskbar menu's expander the last.
        List<OptionPick> picks = [];
        var items = TheDock.LooksItems(config, bar, dark: true, picks, scope: LooksScope.Taskbar);
        Assert.Equal(
            [S.SettingsGeneralCombineHeader, S.SettingsGeneralPreviewsHeader, S.SettingsGeneralDesktopsHeader, S.SettingsGeneralHoverFlyoutsHeader, S.SettingsBarMenuHeader],
            items.SkipWhile(item => !(item.Header && item.Text == S.DockTaskbarBehaviour)).Skip(1).TakeWhile(item => !item.Header).Select(item => item.Text));

        // ⚙: Start with Windows alone; §465: nothing turns the hiding of Windows' taskbar off.
        Assert.Equal([KarpoMenu.StartupRow], KarpoMenu.Startup(config, Services()).Select(each => each.Key));
    }

    /// <summary>
    /// ⚙ in the dock: its General section first (§517: on the page, §485 to §513 a level) — Start with Windows, its Why under
    /// it, the animations, Language, Formats (§703 no Quit Karpo) —; the sites' logos (§597) are carried in the exe and decode; another language picked
    /// opens ⚙ again in it.
    /// </summary>
    [Fact]
    public void TheSettingsMenuHasTheGeneralSectionFirst()
    {
        var startup = new FakeStartup(Registered with { Approval = StartupApproval.Disabled });
        List<OptionPick> picks = [];
        var items = TheDock.SettingsItems(new Config(), Services(startup), picks);
        Assert.Equal(
            [S.SettingsGeneralHeader, S.SettingsGeneralStartupHeader, S.SettingsLanguageSection, S.SettingsLocalizationFormatsSection],
            items.TakeWhile(item => !(item.Header && item.Text == S.SettingsSectionsServices)).Select(item => item.Text));
        Assert.True(items[0].Header);
        Assert.Equal(S.SettingsGeneralStartupTurnedOff, items[1].Detail);
        // §517: Formats says at its end whether they follow Windows.
        Assert.Equal(S.SettingsValueDefault(S.SettingsValueWindows), items[3].Value?.Text);
        var online = items.Single(item => item.Text == S.SettingsOnlineSection);
        var engines = online.Children!.Single(item => item.Text == S.SettingsSearchEngineHeader).Children!;
        Assert.All(engines.SkipLast(1), engine => Assert.IsType<FlyoutIcon.Picture>(engine.Icon));
        Assert.IsType<FlyoutIcon.Glyph>(engines[^1].Icon);
        // §597: the map's, the weather site's and the finance site's choices each their logo, every one its own — in Online's
        // level, as the engines (§594: the levels back; §597's test was made on §588's flat page).
        foreach (var name in new[] { S.SettingsMapProviderHeader, S.SettingsLinksWeatherHeader, S.SettingsLinksMarketsHeader })
        {
            var sites = online.Children!.Single(item => item.Text == name).Children!.Where(kid => kid.Checked is not null).ToList();
            Assert.All(sites, site => Assert.IsType<FlyoutIcon.Picture>(site.Icon));
            Assert.Equal(sites.Count, sites.Select(site => ((FlyoutIcon.Picture)site.Icon!).Pixels).Distinct().Count());
        }

        var menu = File.ReadAllText(Path.Combine(RepoSource.Root(), "Karpo.Dock", "Dock.SettingsMenu.cs"));
        // §356, §520: another language reads the Karpo page's rows again where it stands (Dock.Apply, the chrome built again).
        Assert.Contains("picks => SettingsItems(config, SettingsServices(KarpoPage.Karpo), picks));", File.ReadAllText(Path.Combine(RepoSource.Root(), "Karpo.Dock", "Dock.SettingsPanel.cs")));
        Assert.Contains("MenuAgain(open);", File.ReadAllText(Path.Combine(RepoSource.Root(), "Karpo.Dock", "Dock.cs")));
        Assert.Contains("Open = uri => LaunchLater(uri),", menu);
    }

    /// <summary>
    /// The person, 2026-11-03: "remove the text under the menu row icons again, it should also not be configurable to show
    /// again": no "Show names in the top row" in any menu, and a file's "topRowNames" read past and left out when it is
    /// written again; the top row's buttons their icons alone.
    /// </summary>
    [Fact]
    public void TheTopRowHasNoNamesAndNoSwitchForThem()
    {
        Assert.DoesNotContain(MenuCorpus.All(), menu => Deep(menu.Items).Any(item => item.Text.Contains("top row", StringComparison.OrdinalIgnoreCase)));
        var config = JsonSerializer.Deserialize("{\"topRowNames\": false}", ConfigJson.Default.Config)!;
        Assert.DoesNotContain("topRowNames", JsonSerializer.Serialize(config, ConfigJson.Default.Config), StringComparison.Ordinal);
        Assert.Null(typeof(Config).GetProperty("TopRowNames"));
        Assert.Null(typeof(Flyout).GetProperty("ButtonNames"));
    }

    /// <summary>
    /// §549: "Animations" one row — Off, Slower, Normal, Faster —, Normal for a new config and left out of the file; each
    /// choice written as "animation" and read back; a word of a newer build Normal; the switch and the speed of §489 gone.
    /// </summary>
    [Fact]
    public void AnimationsAreOneRowOfOffAndTheThreeSpeeds()
    {
        var config = new Config();
        // §682: the Appearance's now, General Start with Windows alone.
        Assert.Equal([KarpoMenu.StartupRow], KarpoMenu.General(config, Services()).Select(each => each.Key));
        var rows = LookRows.Rows(config, config.BarLayout, windows: true);
        var row = Assert.Single(rows, each => each.Key == KarpoMenu.AnimationsRow);
        Assert.Equal(OptionRowKind.Choice, row.Kind);
        Assert.Equal(S.SettingsGeneralAnimationsHeader, row.Title);
        Assert.Equal([S.WidgetSwitchOff, S.SettingsGeneralAnimationSpeedSlower, S.SettingsGeneralAnimationSpeedNormal, S.SettingsGeneralAnimationSpeedFaster], row.Choices);
        Assert.Equal((Animation.Normal, 2), (config.Animation, row.Chosen));
        Assert.DoesNotContain("\"animation", config.ToJson(), StringComparison.Ordinal);
        foreach (var (at, animation, word) in new[] { (0, Animation.Off, "off"), (1, Animation.Slower, "slower"), (3, Animation.Faster, "faster"), (2, Animation.Normal, null) })
        {
            Assert.True(row.Set!(at));
            Assert.Equal(animation, config.Animation);
            Assert.False(row.Set!(at));
            var json = config.ToJson();
            if (word is null)
            {
                Assert.DoesNotContain("\"animation", json, StringComparison.Ordinal);
            }
            else
            {
                Assert.Contains($"\"animation\": \"{word}\"", json, StringComparison.Ordinal);
            }
            Assert.Equal(animation, Karpo.Tests.Core.Configs.RoundTrip(config, out _).Animation);
            row = KarpoMenu.AnimationRow(config);
            Assert.Equal(at, row.Chosen);
        }
        Assert.Equal(Animation.Normal, Karpo.Tests.Core.Configs.Repaired("{\"animation\": \"warp\"}").Animation);
        Assert.Null(typeof(Config).GetProperty("AnimationSpeed"));
        Assert.Null(typeof(Config).GetProperty("Animations"));
    }

    private static IEnumerable<FlyoutItem> Deep(IEnumerable<FlyoutItem> items) =>
        items.SelectMany(item => (IEnumerable<FlyoutItem>)[item, .. Deep(item.Children ?? [])]);
}

/// <summary>§337: the language picked in ⚙ takes hold, and the menu made again speaks it.</summary>
[Collection(InLanguage.Collection)]
public sealed class KarpoMenuLanguageTests
{
    [Fact]
    public void ALanguagePickedIsSpokenByTheMenuMadeAgain()
    {
        var config = new Config();
        var services = new KarpoServices(null, () => { }) { Windows = () => ["en-US"] };
        Assert.True(LanguageSearchTests.Pick(Assert.Single(KarpoMenu.Language(config, services)), "de"));
        try
        {
            // What the dock does with it as the config is applied (Dock.Apply).
            Assert.True(Languages.Apply(config.Language));
            Assert.Equal("de", Strings.Language);
            var row = Assert.Single(KarpoMenu.Language(config, services));
            Assert.Equal(("Sprache", "Deutsch"), (row.Title, row.Summary));
            Assert.Equal("Standard (English)", LanguageSearchTests.Finds(row, "")[0].Title);
            var source = File.ReadAllText(Path.Combine(RepoSource.Root(), "Karpo.Dock", "Dock.cs"));
            Assert.Contains("if (before.Language != config.Language && Languages.Apply(config.Language))", source);
        }
        finally
        {
            Strings.Use("en");
        }
    }
}
