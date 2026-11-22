// tray: where each notification icon shows is the tray's menu's in edit mode, and no longer Settings'
// General page's (§261, the person: "tray icon configuration should go into the tray icon menu and not
// be shown in general settings"; Karpo.Core\OptionTray.cs). Held here:
//
//  - **The rows.** A tray's menu has one group, "Tray icons", a row per live icon in the bar's order,
//    each once: its name, its own picture's identity, and a choice of Show on taskbar, Show in hidden
//    icon menu and Hide completely, each with its icon — the one in force the icon's state in this tray,
//    or Windows' choice where nobody chose. None without icons, and none on any other widget.
//  - **Its own tray** (§347): a pick writes the tray whose menu it is, and no other.
//  - **The names.** What the app calls itself, else its tooltip, else its file; Explorer's speaker is
//    "Volume"; two icons of one name are called by their tooltips.
//  - **A pick.** Set as a drag sets it (TrayLayout.Set), its place in the order kept; one hidden
//    completely is still listed and comes back from its row; the one in force changes nothing.
//  - **The menu.** One row at the root opening the icons, each row wearing its icon's own picture and
//    saying what it is set to, short ("On taskbar", "Behind arrow", "Hidden", §323) and so whole beside
//    its name, in English and German, at 100 and 150 %; at rest nothing of it (RestMenusTests holds every
//    kind to that).
//  - **Settings.** General has no tray icons any more, and their page's words are gone.
//  - **On a real menu** (KARPO_UI_TESTS=1): the group opened, then an icon's choices. Pictures in
//    KARPO_PNG_DIR at 150 %: menu-tray-icons-<look>.png and menu-tray-icons-choices-<look>.png.

using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using Karpo.Tests.Localization;
using TerraFX.Interop.Windows;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Menus;

[Collection(InLanguage.Collection)]
public sealed class TrayMenuRowsTests
{
    /// <summary>Four live icons: an app on the taskbar, one Windows hides, Explorer's two, as the dock lists them.</summary>
    private static readonly TrayMenuIcon[] Icons =
    [
        new("spotify|0", "Spotify", AppHidden: false, "Spotify Premium"),
        new("onedrive|1", "Microsoft OneDrive", AppHidden: true, "OneDrive - Personal"),
        new("{7820ae78-23e3-4229-82c1-e41cb67d5b9c}", "Windows Explorer", AppHidden: true, "Safely Remove Hardware and Eject Media"),
        new("explorer|5", "Windows Explorer", AppHidden: false, "Meet Now"),
    ];

    private static OptionDevices Listed(params TrayMenuIcon[] icons) => new() { TrayIcons = icons };

    private static OptionPlace Place(Config config) => new(config, Dark: true);

    /// <summary>A tray of its own id, its icons' places <paramref name="states"/> and its order <paramref name="order"/>.</summary>
    private static BarItem Tray(string id = "tray", Dictionary<string, TrayState>? states = null, List<string>? order = null)
    {
        var tray = BarKind.Tray.New();
        tray.Id = id;
        tray.TrayIcons = states is null && order is null ? null : new TrayIconOptions { States = new(states ?? [], StringComparer.OrdinalIgnoreCase), Order = order ?? [] };
        return tray;
    }

    private static IReadOnlyList<OptionRow> Rows(BarItem tray, params TrayMenuIcon[] icons) =>
        OptionRows.Of(tray, Listed(icons.Length > 0 ? icons : Icons));

    // --- The rows -----------------------------------------------------------------------------

    [Fact]
    public void EachIconIsARowOfTheThreePlacesInTheBarsOrder()
    {
        // OneDrive dragged first, Spotify hidden completely; the rest where Windows put them.
        var tray = Tray(states: new() { ["spotify|0"] = TrayState.Hidden }, order: ["onedrive|1"]);

        var rows = Rows(tray);

        Assert.Equal(["onedrive|1", "spotify|0", "{7820ae78-23e3-4229-82c1-e41cb67d5b9c}", "explorer|5"], rows.Select(row => row.Picture));
        Assert.Equal([.. rows.Select(row => OptionTray.Icon + row.Picture)], rows.Select(row => row.Key));
        Assert.Equal(["Microsoft OneDrive", "Spotify", "Safely Remove Hardware and Eject Media", "Meet Now"], rows.Select(row => row.Title));
        // OneDrive where Windows hides it, Spotify hidden completely, Explorer's as Windows has them.
        Assert.Equal([1, 2, 1, 0], rows.Select(row => row.Chosen));
        foreach (var row in rows)
        {
            Assert.Equal(OptionRowKind.Choice, row.Kind);
            Assert.Equal([S.SettingsTrayShowOnTaskbar, S.SettingsTrayShowInMenu, S.SettingsTrayHide], row.Choices);
            Assert.Equal([OptionGlyphs.Taskbar, OptionGlyphs.More, OptionGlyphs.Hide], row.ChoiceGlyphs);
            Assert.Equal(WidgetInfos.Tray.Glyph, row.Glyph);
            Assert.Equal(S.SettingsTrayIconsHeader, row.Group);
            Assert.Equal(WidgetInfos.Tray.Glyph, row.GroupGlyph);
            Assert.False(row.Segmented);
        }
        Assert.Equal("Tray icons", S.SettingsTrayIconsHeader);
    }

    [Fact]
    public void AnIconTheTrayHasTwiceIsOneRow()
    {
        var rows = Rows(Tray(), [Icons[0], Icons[0] with { Tip = "again" }, Icons[1]]);

        Assert.Equal(["spotify|0", "onedrive|1"], rows.Select(row => row.Picture));
    }

    /// <summary>Only a tray has them, and only with its icons listed; it writes into itself, with no config to name.</summary>
    [Fact]
    public void NoneWithoutIconsNorOnAnyOtherWidget()
    {
        var config = new Config();
        Assert.DoesNotContain(OptionRows.Of(BarKind.Tray.New(), null, Place(config)), row => row.Key.StartsWith(OptionTray.Icon, StringComparison.Ordinal));
        Assert.Equal(Icons.Length, OptionRows.Of(BarKind.Tray.New(), Listed(Icons)).Count(row => row.Key.StartsWith(OptionTray.Icon, StringComparison.Ordinal)));
        Assert.Empty(OptionRows.Of(BarKind.Tray.New(), Listed(), Place(config)));
        foreach (var other in Enum.GetValues<BarKind>().Where(kind => kind != BarKind.Tray).Select(kind => (WidgetItem)kind.New())
            .Concat(Enum.GetValues<ListKind>().Select(kind => kind.New())))
        {
            Assert.DoesNotContain(OptionRows.Of(other, Listed(Icons), Place(config)), row => row.Key.StartsWith(OptionTray.Icon, StringComparison.Ordinal));
        }
    }

    // --- The names ----------------------------------------------------------------------------

    [Fact]
    public void AnIconIsNamedAsWindowsNamesIt()
    {
        Assert.Equal("Spotify", OptionTray.NameOf("spotify|0", @"C:\Apps\Spotify.exe", "Spotify Premium", "Spotify", "Spotify Product"));
        Assert.Equal("Spotify Product", OptionTray.NameOf("spotify|0", @"C:\Apps\Spotify.exe", "Spotify Premium", " ", "Spotify Product"));
        Assert.Equal("Spotify Premium", OptionTray.NameOf("spotify|0", @"C:\Apps\Spotify.exe", "Spotify Premium\nsecond line", null, null));
        Assert.Equal("Spotify", OptionTray.NameOf("spotify|0", @"C:\Apps\Spotify.exe", "", null, null));
        Assert.Equal("Spotify", OptionTray.NameOf("spotify|0", @"C:\Apps\Spotify.exe", TrayCatalogue.NoName, null, null));
        Assert.Equal(TrayCatalogue.NoName, OptionTray.NameOf("{guid}", "", "", null, null));
        // Explorer's speaker is Windows' "Volume", not Explorer's own name; a second one of it too.
        Assert.Equal(S.SettingsTrayVolume, OptionTray.NameOf(OptionTray.VolumeIcon, @"C:\Windows\explorer.exe", "Headphones (Bluetooth): 22%", "Windows Explorer", null));
        Assert.True(OptionTray.IsVolume(OptionTray.VolumeIcon.ToUpperInvariant() + "#2"));
        Assert.False(OptionTray.IsVolume("{7820ae78-23e3-4229-82c1-e41cb67d5b9c}"));
    }

    [Fact]
    public void TwoIconsOfOneNameAreCalledByTheirTooltips()
    {
        var rows = Rows(Tray(), [Icons[2], Icons[3], Icons[3] with { Identity = "explorer|6", Tip = "" }]);

        // The one with no tooltip keeps the name: there is nothing better to call it.
        Assert.Equal(["Safely Remove Hardware and Eject Media", "Meet Now", "Windows Explorer"], rows.Select(row => row.Title));
        Assert.Equal("Spotify", Rows(Tray(), [Icons[0]]).Single().Title);
    }

    // --- A pick -------------------------------------------------------------------------------

    /// <summary>
    /// A pick in the menu, as the dock makes it (Dock.OptionPicked): the rows read from the tray in a
    /// copy of the config, the pick applied to that tray, the copy saved where it changed.
    /// </summary>
    private static bool Pick(BarItem tray, string identity, int choice)
    {
        List<OptionPick> picks = [];
        _ = OptionMenu.Section(Rows(tray), picks);
        var pick = picks.Single(each => each is { Kind: OptionPickKind.Set } && each.Key == OptionTray.Icon + identity && each.Value == choice);
        return OptionMenu.Apply(tray, pick, Listed(Icons));
    }

    [Fact]
    public void APickIsWrittenAsADragWritesItTheOrderKept()
    {
        var config = Karpo.Tests.Core.Configs.With(Tray(order: ["explorer|5", "spotify|0"]));
        var entries = Icons.Select(icon => new TrayEntry(icon.Identity, icon.AppHidden)).ToList();

        // Spotify hidden completely: on neither the bar nor the hidden icon menu, still in its row.
        var before = config.Clone();
        Assert.True(Pick(TrayLayout.Trays(config).Single(), "spotify|0", 2));
        var read = Karpo.Tests.Core.Configs.RoundTrip(config, out _);
        var tray = TrayLayout.Trays(read).Single();
        Assert.Equal(TrayState.Hidden, tray.TrayIcons!.States["spotify|0"]);
        Assert.Equal(["explorer|5", "spotify|0"], tray.TrayIcons.Order);
        Assert.DoesNotContain(TrayLayout.Split(tray.TrayIcons, entries).Shown, entry => entry.Identity == "spotify|0");
        Assert.DoesNotContain(TrayLayout.Split(tray.TrayIcons, entries).Overflow, entry => entry.Identity == "spotify|0");
        Assert.Equal(2, Rows(tray).Single(row => row.Picture == "spotify|0").Chosen);

        // The one in force is no change; its row brings it back.
        Assert.False(Pick(tray, "spotify|0", 2));
        Assert.True(Pick(tray, "spotify|0", 0));
        Assert.Contains(TrayLayout.Split(tray.TrayIcons, entries).Shown, entry => entry.Identity == "spotify|0");

        // OneDrive, which Windows hides, onto the bar; then where Windows has it, which is now a choice of its own.
        Assert.True(Pick(tray, "onedrive|1", 0));
        Assert.Equal(TrayState.Taskbar, tray.TrayIcons.States["onedrive|1"]);
        Assert.True(Pick(tray, "onedrive|1", 1));
        Assert.Equal(TrayState.Overflow, tray.TrayIcons.States["onedrive|1"]);
        Assert.False(Pick(tray, "onedrive|1", 1));
        // Windows hears of each (PromoteChoices): Spotify promoted, OneDrive not.
        Assert.Equal(new[] { ("onedrive|1", false), ("spotify|0", true) }, TrayLayout.NewChoices(before, read, _ => null).OrderBy(change => change.Identity, StringComparer.Ordinal).ToArray());
    }

    /// <summary>
    /// §347, the person: "scope these settings to the widget even". Two trays, on two layouts: a pick in
    /// one's menu writes that one; the other's rows still say where Windows has the icon.
    /// </summary>
    [Fact]
    public void APickWritesTheTrayWhoseMenuItIs()
    {
        var config = Karpo.Tests.Core.Configs.With(Tray("home"));
        config.Layouts.Add(new TaskbarLayout { Id = "work", Name = "Work", Islands = [Tray("work")] });
        var (home, work) = (TrayLayout.Trays(config).First(), TrayLayout.Trays(config).Last());

        Assert.True(Pick(work, "spotify|0", 2));

        Assert.Null(home.TrayIcons);
        Assert.Equal(TrayState.Hidden, work.TrayIcons!.States["spotify|0"]);
        Assert.Equal(0, Rows(home).Single(row => row.Picture == "spotify|0").Chosen);
        Assert.Equal(2, Rows(work).Single(row => row.Picture == "spotify|0").Chosen);
        var read = TrayLayout.Trays(Karpo.Tests.Core.Configs.RoundTrip(config, out _)).ToList();
        Assert.Null(read[0].TrayIcons);
        Assert.Equal(TrayState.Hidden, read[1].TrayIcons!.States["spotify|0"]);
    }

    // --- The menu -----------------------------------------------------------------------------

    private static Pixels Solid(int size, uint argb)
    {
        var data = new byte[size * size * 4];
        for (var at = 0; at < data.Length; at += 4)
        {
            (data[at], data[at + 1], data[at + 2], data[at + 3]) = ((byte)argb, (byte)(argb >> 8), (byte)(argb >> 16), (byte)(argb >> 24));
        }
        return new Pixels { Data = data, Width = size, Height = size };
    }

    /// <summary>The live pictures, as the dock hands them (Dock.TrayPicture): every icon's but Explorer's second.</summary>
    private static readonly Dictionary<string, FlyoutIcon> Pictures = new()
    {
        ["spotify|0"] = new FlyoutIcon.Picture(Solid(24, 0xFF1DB954)),
        ["onedrive|1"] = new FlyoutIcon.Picture(Solid(24, 0xFF0078D4)),
        ["{7820ae78-23e3-4229-82c1-e41cb67d5b9c}"] = new FlyoutIcon.Picture(Solid(24, 0xFF8A8A8A)),
    };

    private static List<FlyoutItem> Menu(BarItem tray, List<OptionPick> picks) =>
        [.. TheDock.WidgetRowsFor(BarKind.Tray.New()), .. OptionMenu.Section(Rows(tray), picks, identity => Pictures.GetValueOrDefault(identity))];

    [Fact]
    public void TheIconsAreOneRowOpeningEachWithItsPicture()
    {
        List<OptionPick> picks = [];
        var menu = Menu(Tray(), picks);

        // Cut, Copy, Remove, the rule, then the one group: no "Settings" any more.
        Assert.Equal(["Cut", "Copy", "Remove", "", S.SettingsTrayIconsHeader], menu.Select(row => row.Text));
        var group = menu[^1];
        Assert.Equal(new FlyoutIcon.Glyph(WidgetInfos.Tray.Glyph), group.Icon);
        // §517 (R4): how many show at the group's end, as the status icons' says.
        Assert.Equal(S.DockEditShownCount(4), group.Value?.Text);
        var icons = group.Children!;
        Assert.Equal(["Spotify", "Microsoft OneDrive", "Safely Remove Hardware and Eject Media", "Meet Now"], icons.Select(row => row.Text));
        Assert.Equal(Pictures["spotify|0"], icons[0].Icon);
        Assert.Equal(Pictures["onedrive|1"], icons[1].Icon);
        // No picture: the tray's glyph.
        Assert.Equal(new FlyoutIcon.Glyph(WidgetInfos.Tray.Glyph), icons[3].Icon);
        // Each says where it shows, short (§323), and opens the three, the one in force ticked, picked without the menu closing.
        Assert.Equal([S.SettingsTraySaidTaskbar, S.SettingsTraySaidArrow, S.SettingsTraySaidArrow, S.SettingsTraySaidTaskbar], icons.Select(row => row.Value?.Text));
        foreach (var row in icons)
        {
            Assert.Equal([S.SettingsTrayShowOnTaskbar, S.SettingsTrayShowInMenu, S.SettingsTrayHide], row.Children!.Select(choice => choice.Text));
            Assert.Single(row.Children!, choice => choice.Checked == true);
            Assert.All(row.Children!, choice => Assert.True(choice.Stays));
            Assert.All(row.Children!, choice => Assert.NotNull(choice.Icon));
            Assert.All(row.Children!, choice => Assert.True(OptionMenu.KeepsOpen(OptionMenu.PickOf(picks, choice.Id)!.Kind)));
        }
        // None of it is Windows' or the settings app's: picked, the screen stays Karpo's.
        Assert.All(icons.SelectMany(row => row.Children!), choice => Assert.False(TheDock.PickHandsAway([], choice.Id, picks)));
    }

    /// <summary>The menu shown again after a pick has the icon's tick where it went and the rows round it as they were.</summary>
    [Fact]
    public void AfterAPickTheMenuShowsItWhereItWent()
    {
        var tray = Tray();
        List<OptionPick> picks = [];
        var before = Menu(tray, picks);
        Assert.True(Pick(tray, "spotify|0", 2));

        var after = OptionMenu.Updated(before, OptionMenu.Section(Rows(tray), [], identity => Pictures.GetValueOrDefault(identity)));

        Assert.Equal(before.Take(3).Select(row => row.Id), after.Take(3).Select(row => row.Id));
        var spotify = after[^1].Children!.Single(row => row.Text == "Spotify");
        Assert.Equal(S.SettingsTraySaidHidden, spotify.Value?.Text);
        Assert.True(spotify.Children![2].Checked);
    }

    // --- Settings -----------------------------------------------------------------------------

    /// <summary>The tray icons' page words are gone from every language: the menu's and the status icons' stay.</summary>
    [Fact]
    public void TheTrayPagesOwnWordsAreGone()
    {
        foreach (var key in new[] { "settings.tray.intro", "settings.tray.empty", "settings.tray.icons.description" })
        {
            Assert.Null(S.Lookup(key));
        }
        // The rows' words stay: the menu's and the status icons'.
        Assert.NotNull(S.Lookup("settings.tray.icons.header"));
        Assert.NotNull(S.Lookup("settings.tray.volume"));
    }

    // --- On a real menu -----------------------------------------------------------------------

    private static void KeysTo(Flyout menu, uint id, Func<uint?> hot)
    {
        for (var step = 0; step < 40 && hot() != id; step++)
        {
            _ = menu.Message(WM.WM_KEYDOWN, VK.VK_DOWN, 0);
        }
        Assert.Equal(id, hot());
    }

    private static bool Save(Flyout menu, string name, bool dark) =>
        WindowShots.SaveTogether([.. menu.OpenPanels], name, dark ? ((byte)0, (byte)0, (byte)0) : ((byte)243, (byte)243, (byte)243));

    /// <summary>
    /// §323, the person: "the Tray icons level cuts its values off" ("Show on task…", "Show in hidden i…"):
    /// the three places said short at the rows' end, each whole beside an app's name — a long one's too —,
    /// in English and German, at 100 and 150 %; the level's choices still say them in full.
    /// </summary>
    [UiFact]
    public void TheIconsLevelSaysEachPlaceWhole()
    {
        WindowShots.OnHiddenDesktop("menu-tray-said", (gfx, instance) =>
        {
            var tray = Tray(states: new() { ["spotify|0"] = TrayState.Hidden });
            TrayMenuIcon[] icons = [.. Icons, new("teams|2", "Microsoft Teams", AppHidden: false, "Microsoft Teams"), new("nvidia|3", "NVIDIA Settings", AppHidden: true, "NVIDIA Settings")];
            foreach (var language in new[] { "en", "de" })
            {
                using var speaking = InLanguage.Use(language);
                foreach (var scale in new[] { 1f, 1.5f })
                {
                    var work = new RECT { right = (int)(1920 * scale), bottom = (int)(1040 * scale) };
                    using var menu = new Flyout(gfx, instance, 0) { Configures = true, Configuration = true };
                    List<FlyoutItem> items = [.. TheDock.WidgetRowsFor(BarKind.Tray.New()), .. OptionMenu.Section(Rows(tray, icons), [], identity => Pictures.GetValueOrDefault(identity))];
                    menu.Show(gfx, AddListPictures.LookFor(true), scale, items, FlyoutLayout.List, new POINT { x = work.right / 4, y = work.bottom }, work, Edge.Bottom, null);
                    WindowShots.Settle();
                    _ = menu.Click(items[^1].Id);
                    WindowShots.Settle(250);
                    Assert.Equal(1, menu.InlineDepth);
                    var looks = menu.Looks.ToDictionary(look => look.Id);
                    var rows = items[^1].Children!;
                    Assert.Equal(icons.Length, rows.Count);
                    Assert.Equal(OptionTray.Said.Order(StringComparer.Ordinal), rows.Select(row => row.Value!.Text).Distinct().Order(StringComparer.Ordinal));
                    foreach (var row in rows)
                    {
                        var natural = MathF.Floor(gfx.Measure(row.Value!.Text, 14 * scale, Weight.Regular).X);
                        Assert.True(looks[row.Id].Value.W >= natural - 1, $"{language} at {scale:P0}: {row.Text}'s \"{row.Value.Text}\" cut to {looks[row.Id].Value.W} of {natural} px");
                    }
                    // The choices in full.
                    Assert.Equal([S.SettingsTrayShowOnTaskbar, S.SettingsTrayShowInMenu, S.SettingsTrayHide], rows[0].Children!.Select(choice => choice.Text));
                    Assert.True(Save(menu, $"menu-tray-said-{language}-{scale * 100:0}.png", dark: true));
                    menu.Hide();
                }
            }
        });
    }

    [UiFact]
    public void OnARealMenuTheIconsOpenAndEachItsChoices()
    {
        WindowShots.OnHiddenDesktop("menu-tray-icons", (gfx, instance) =>
        {
            const float scale = 1.5f;
            var tray = Tray(states: new() { ["spotify|0"] = TrayState.Hidden });
            foreach (var dark in new[] { true, false })
            {
                var look = AddListPictures.LookFor(dark);
                var tone = dark ? "dark" : "light";
                var work = new RECT { right = (int)(1920 * scale), bottom = (int)(1040 * scale) };
                var anchor = new POINT { x = work.right / 4, y = work.bottom };
                using var menu = new Flyout(gfx, instance, 0) { Configures = true };
                var items = Menu(tray, []);
                menu.Show(gfx, look, scale, items, FlyoutLayout.List, anchor, work, Edge.Bottom, null);
                WindowShots.Settle();

                var group = items[^1];
                KeysTo(menu, group.Id, () => menu.HotId);
                _ = menu.Message(WM.WM_KEYDOWN, VK.VK_RIGHT, 0);
                WindowShots.Settle();
                Assert.Equal(1, menu.InlineDepth);
                Assert.True(Save(menu, $"menu-tray-icons-{tone}.png", dark));

                // The hidden one's choices, "Hide completely" ticked.
                var spotify = group.Children!.Single(row => row.Text == "Spotify");
                KeysTo(menu, spotify.Id, () => menu.HotId);
                _ = menu.Message(WM.WM_KEYDOWN, VK.VK_RIGHT, 0);
                WindowShots.Settle();
                Assert.Equal(2, menu.InlineDepth);
                Assert.True(Save(menu, $"menu-tray-icons-choices-{tone}.png", dark));
                menu.Hide();
            }
        });
    }
}
