// edit, §520, §575: the edit bar and its flyouts as Karpo draws them (docs\widgets.md §520, §575): a display 1920 × 1080 DIP
// at 150 % in edit mode, Karpo's taskbar along its bottom, the edit bar at its top centred, dimmed as edit mode dims it
// (EditPlacePictures.EditedDisplay); the real bar (EditDeck), an entry for each page the dock builds
// (SettingsPanelTests.Content), the flyout of the entry open hung from it. To KARPO_PNG_DIR as panel-<scene>-<dark|light>.png:
//
//  - closed: the edit bar, its entries, two errors on it, no flyout open;
//  - look: the Appearance flyout, Taskbar widgets open;
//  - karpo: the Karpo flyout, Public holidays open;
//  - §535: accounts (the Accounts flyout, a Microsoft account open: its calendars' colour buttons and switches), colours
//    (the Appearance flyout's colours, each a colour button);
//  - widget: a folder open, a calendar in it selected: its entries, the widget's flyout, Cut, Copy and Remove its header's;
//  - foot: the taskbar along the top, the edit bar at the bottom: the Appearance flyout opens up from its entry;
//  - §575 add: "+ Add"'s flyout, the add list a page of the settings' look, §578 its widgets tiles side by side; add-open
//    a folder of it open, its tiles in its card; §586 add-counts, add-counts-open the same with a layout's counts (Counted):
//    a number at the end of each tile its layout has and at each shut folder's head; §590 add-weather the folder's add page
//    with Weather and news open (the weather's three widgets, the air's four), add-weather-bar the bar's (the ready-made
//    folders); §603 add-system the folder's add page with System info open (the graphs, screen time today and its history);
//  - §521: layouts (the Layouts page, Work open), display (the Display page), text (a layout's name typed in place),
//    number (the Taskbar page's Size typed in its number box), things (a toggles line's page: its toggles, each with its
//    Remove), backup (the Karpo page's Back up and reset open);
//  - §528: same (two clocks selected: their page whole, a switch neither on nor off), several (a clock and the weather:
//    what they share);
//  - §532, §553: barmenu (the Taskbar page's last section, Taskbar menu, the default, §551 its preview in sight), barmenu-own
//    (the person's own, its shortcut open); §551 barmenu-drag (File Explorer dragged down by its handle, held, the rows it
//    passes making room);
//  - §545: general (the Karpo flyout's General; §703 no Quit Karpo);
//  - §584: every flyout 12 DIP off the bar, its beak over its entry; edge (a display 1280 DIP wide: the Karpo flyout pushed
//    in at its left, its beak still over Karpo).
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using System.Numerics;
using Karpo.Dock;
using Karpo.Tests.Islands;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    /// <summary>
    /// A scene: its name, the page shown, the expander open there, the errors on the bar; §521 the widget whose page it is (a
    /// calendar's where none is named), and a box typed in place (its row's words, what is typed in it).
    /// </summary>
    private sealed record PanelScene(string Name, PanelPage? Page, string Open = "", bool Widget = false, int Errors = 0, bool Light = false, bool Top = false, ListKind? Kind = null, string Box = "", string Into = "", bool Several = false)
    {
        /// <summary>§553: the row brought to the top where nothing is open (a section's first row).</summary>
        public string Land { get; init; } = "";

        /// <summary>§532: the config changed for the scene (the taskbar menu the person's own).</summary>
        public Action<Config>? Edit { get; init; }

        /// <summary>§551: a row lifted by its handle and dragged up so many px (down for fewer than none), still held (its words); none for none.</summary>
        public (string Row, float Up)? Lift { get; init; }

        /// <summary>§576: the add page opened from a bar's "+", §593 at the edit bar's top all the same.</summary>
        public bool Plus { get; init; }

        /// <summary>§586: the add page with <see cref="Counted"/>'s counts.</summary>
        public bool Counts { get; init; }
        /// <summary>§584: the display's size in DIP where not 1920 × 1080 (a narrow one, a flyout pushed in at its side).</summary>
        public Vector2? Dips { get; init; }
        /// <summary>§590: whose add page it is: the bar's (its ready-made folders), or a folder's (its widgets).</summary>
        public WidgetType AddOn { get; init; } = WidgetType.Bar;
    }

    /// <summary>
    /// §586: a layout of the pictures, counted: Start, Search, Task view, the apps, the tray, the volume, Wi-Fi's switch, keep
    /// awake twice, and a folder holding two clocks and the weather now.
    /// </summary>
    internal static IReadOnlyDictionary<string, int> Counted()
    {
        var folder = Karpo.Tests.Core.Configs.FolderKind(FolderView.List).New();
        folder.Folder!.Put(ListKind.Clock.New());
        folder.Folder.Put(ListKind.Clock.New());
        folder.Folder.Put(ListKind.WeatherNow.New());
        return AddCounts.Placed([BarKind.Start.New(), BarKind.Search.New(), BarKind.TaskView.New(), BarKind.Apps.New(), BarKind.Tray.New(),
            BarKind.Volume.New(), BarKind.WifiSwitch.New(), BarKind.KeepAwake.New(), BarKind.KeepAwake.New(), folder]);
    }

    /// <summary>§586: the key of the first folder of the counted add page whose head and two of whose tiles say a count.</summary>
    private static string CountedOpen() => SettingsPanelTests.AddPage(Counted()).Rows
        .First(row => row is { Kind: PanelRowKind.Expander, Value: not null } && row.Rows.Count(each => each.Value is not null) >= 2).Key;

    /// <summary>
    /// §532: the taskbar menu of the pictures: Taskbar settings; a heading, "Tools", over File Explorer, Terminal and a shortcut to
    /// Notepad named "Notes"; the layouts between rules; Quit Karpo; Task Manager the very last.
    /// </summary>
    internal static void PersonsBarMenu(Config config) => _ = BarMenus.Change(config, list =>
    {
        list.Clear();
        list.AddRange(
        [
            BarMenuEntry.Of(BarMenuAction.TaskbarSettings, "settings"),
            BarMenuEntry.Heading("Tools"),
            BarMenuEntry.Of(BarMenuAction.FileExplorer),
            BarMenuEntry.Of(BarMenuAction.Terminal),
            BarMenuEntry.Shortcut(@"C:\Windows\notepad.exe", name: "Notes"),
            BarMenuEntry.Divider(),
            BarMenuEntry.Of(BarMenuAction.Layouts),
            BarMenuEntry.Divider(),
            BarMenuEntry.Of(BarMenuAction.QuitKarpo),
            BarMenuEntry.Of(BarMenuAction.TaskManager),
        ]);
        return true;
    });

    /// <summary>§592: the Appearance's one preview, over its tabs.</summary>
    private static string AppearancePreview => "Appearance/" + PanelRows.PreviewKey;
    /// <summary>§585: the Taskbar page's expander Taskbar menu, Behavior's last row.</summary>
    private static string BarMenuKey => "Taskbar/" + S.DockTaskbarBehaviour + "/" + S.SettingsBarMenuHeader;

    private static readonly PanelScene[] PanelScenes =
    [
        new("closed", null, Errors: 2, Light: true),
        // §592: the Appearance's one preview over its tabs, the first chosen.
        new("look", PanelPage.Appearance, Light: true) { Land = AppearancePreview },
        new("karpo", PanelPage.Karpo, "Karpo/" + S.SettingsSectionsServices + "/" + S.CoreHolidaysName, Light: true),
        new("accounts", PanelPage.Accounts, "Accounts/" + S.SettingsAccountsYours + "/me@outlook.com", Light: true),
        new("colours", PanelPage.Appearance, Light: true) { Land = "Appearance/" + PanelTabs.Mark + LookParts.Name(LookPart.Widgets) + "/" + S.SettingsAppearanceCustomBackgroundHeader },
        new("widget", PanelPage.Widget, Widget: true, Light: true),
        new("foot", PanelPage.Appearance, Top: true, Light: true) { Land = AppearancePreview },
        new("layouts", PanelPage.Layouts, "Layouts/" + S.CoreBarMenuLayouts + "/Work"),
        // §585: the Layouts page, this display's expander open (two displays: PagesSheets).
        new("display", PanelPage.Layouts, "Layouts/" + S.SettingsSectionsDisplays + "/One"),
        new("text", PanelPage.Layouts, "Layouts/" + S.CoreBarMenuLayouts + "/Work", Box: S.SettingsLayoutsNameHeader, Into: "Work desk"),
        new("number", PanelPage.Taskbar, Box: S.SettingsLayoutsSizeHeader, Into: "56"),
        new("things", PanelPage.Widget, Widget: true, Kind: ListKind.Toggles),
        new("backup", PanelPage.Karpo, "Karpo/" + S.SettingsSectionsKarpo + "/" + S.DockSettingsBackup),
        // §528: several selected: two clocks, their page whole, a switch mixed; a clock and the weather, what they share.
        new("same", PanelPage.Widget, Several: true, Light: true),
        new("several", PanelPage.Widget, Several: true, Kind: ListKind.WeatherNow, Light: true),
        // §532, §553, §585: the Taskbar page's expander Taskbar menu, the default (§551 its preview in sight) and the person's own
        // (its shortcut open).
        new("barmenu", PanelPage.Taskbar, Light: true) { Land = BarMenuKey + "/" + PanelRows.PreviewKey },
        new("barmenu-own", PanelPage.Taskbar, BarMenuKey + "/Notes", Light: true) { Edit = PersonsBarMenu },
        // §551: File Explorer dragged down past Terminal, held: the rows it passes making room.
        new("barmenu-drag", PanelPage.Taskbar, BarMenuKey, Light: true) { Edit = PersonsBarMenu, Lift = (S.CoreBarMenuFileExplorer, -170) },
        // §545: General (§703 no Quit Karpo).
        new("general", PanelPage.Karpo, Light: true),
        // §548: the Taskbar flyout as it opens, dark and light.
        new("taskbar", PanelPage.Taskbar, Light: true),
        // §575: "+ Add"'s flyout.
        new("add", PanelPage.Add, Light: true),
        // §578: a folder of the add tree open, its widgets tiles in its card.
        new("add-open", PanelPage.Add, SettingsPanelTests.AddPage().Rows.First(row => row.Kind == PanelRowKind.Expander).Key, Light: true),
        // §576: the add page of a bar's "+", that "+" pressed, the place it adds at marked; §593 at the edit bar's top, as wide.
        new("plus", PanelPage.Add, Light: true) { Plus = true },
        // §586: the add page counting what the layout has: each tile its widget's, each shut folder its widgets' sum; one open.
        new("add-counts", PanelPage.Add, Light: true) { Counts = true },
        new("add-counts-open", PanelPage.Add, CountedOpen(), Light: true) { Counts = true },
        // §584: on a display 1280 DIP wide, the Karpo flyout pushed in at its left, its beak still over Karpo.
        new("edge", PanelPage.Karpo, Light: true) { Dips = new Vector2(1280f, 800f) },
        // §590: Weather and news open, a folder's add page (the weather's three widgets, the air's four) and the bar's.
        new("add-weather", PanelPage.Add, WeatherShelf(WidgetType.List), Light: true) { AddOn = WidgetType.List },
        new("add-weather-bar", PanelPage.Add, WeatherShelf(WidgetType.Bar), Light: true),
        // §603: System info open, a folder's add page (the graphs, screen time today and its history before the ring and the apps).
        new("add-system", PanelPage.Add, Shelf(S.CoreAddTreeSystemTitle, WidgetType.List), Light: true) { AddOn = WidgetType.List },
    ];

    /// <summary>§590: the key of the add page's Weather and news, on <paramref name="on"/>'s page.</summary>
    private static string WeatherShelf(WidgetType on) => Shelf(S.CoreAddTreeWeatherTitle, on);

    /// <summary>The key of the add page's subject <paramref name="title"/>, on <paramref name="on"/>'s page.</summary>
    private static string Shelf(string title, WidgetType on) =>
        SettingsPanelTests.AddPage(on: on).Rows.First(row => row.Kind == PanelRowKind.Expander && row.Text == title).Key;

    [UiFact]
    public void SettingsPanelSheets()
    {
        WindowShots.OnHiddenDesktop("settings-panel", (gfx, instance) =>
        {
            const uint dpi = 144;
            // §578: the bar one width in every scene, whatever is selected, open or erring.
            var widths = new HashSet<int>();
            foreach (var dark in new[] { true, false })
            {
                var look = AddListPictures.LookFor(dark) with { Animations = false };
                foreach (var scene in PanelScenes.Where(each => dark || each.Light))
                {
                    var layout = PictureLayout(LayoutType.Taskbar, scene.Top ? Edge.Top : Edge.Bottom);
                    var file = $"panel-{scene.Name}-{(dark ? "dark" : "light")}.png";
                    // §576: the add page from a "+" of the bar, §593 at the edit bar's top all the same, as wide, the "+" off it.
                    Action<EditDeck, TerraFX.Interop.Windows.RECT>? fromPlus = !scene.Plus ? null : (deck, plus) =>
                    {
                        var content = SettingsPanelTests.Content(dark: dark);
                        content = content with { Pages = [.. content.Pages, SettingsPanelTests.AddPage()] };
                        deck.Memory = new PanelMemory();
                        deck.Show(gfx, look, dpi / 96f, deck.Room!, deck.State ?? PictureState(), content, PanelPage.Add);
                        Assert.Equal(PanelPage.Add, deck.Page);
                        var hung = deck.FlyoutOnScreen;
                        HungFromTheBar(deck);
                        Assert.False(hung.left < plus.right && plus.left < hung.right && hung.top < plus.bottom && plus.top < hung.bottom, "the add page stands on its \"+\"");
                    };
                    EditedDisplay(gfx, instance, dpi, new Config(), look, layout, file, dips: scene.Dips ?? new Vector2(1920f, 1080f), fromPlus: fromPlus, panel: deck =>
                    {
                        var room = deck.State is { } state ? state : PictureState();
                        var content = SettingsPanelTests.Content(widget: scene.Widget, dark: dark, kind: scene.Kind, several: scene.Several, edit: scene.Edit);
                        if (scene.Page is not { } page)
                        {
                            deck.Show(gfx, look, dpi / 96f, RoomOf(deck), room with { Errors = scene.Errors }, content);
                            Assert.False(deck.FlyoutOpen);
                            widths.Add(deck.PanelOnScreen.right - deck.PanelOnScreen.left);
                            return;
                        }
                        if (page == PanelPage.Add)
                        {
                            content = content with { Pages = [.. content.Pages, SettingsPanelTests.AddPage(scene.Counts ? Counted() : null, scene.AddOn)] };
                        }
                        deck.Memory = new PanelMemory();
                        if (scene.Open.Length > 0)
                        {
                            deck.Memory.Open([scene.Open]);
                        }
                        deck.Show(gfx, look, dpi / 96f, RoomOf(deck), room, content, page);
                        Assert.True(deck.FlyoutOpen);
                        if (scene.Dips is null)
                        {
                            widths.Add(deck.PanelOnScreen.right - deck.PanelOnScreen.left);
                        }
                        Assert.Equal(page, deck.Page);
                        // §584: its beak over its entry's middle, out of the side facing the bar; pushed in at a narrow display's side
                        // all the same. §593 the add page hung from the whole bar, as wide, no beak.
                        if (page == PanelPage.Add)
                        {
                            HungFromTheBar(deck);
                        }
                        else
                        {
                            var entry = deck.OnScreen(new PanelHit(PanelHitKind.Nav, Page: page));
                            var flyout = deck.FlyoutOnScreen;
                            Assert.Equal(scene.Top ? Edge.Bottom : Edge.Top, deck.FlyoutBeak!.Value.Side);
                            Assert.InRange(flyout.left + deck.FlyoutBeak.Value.Tip - ((entry.left + entry.right) / 2f), -1, 1);
                            if (scene.Dips is not null)
                            {
                                Assert.True(MathF.Abs(((flyout.left + flyout.right) / 2f) - ((entry.left + entry.right) / 2f)) > 50, "the flyout not pushed in");
                            }
                        }
                        // §521: the expander open in sight (Back up and reset is low on the Karpo page).
                        if (scene.Open.Length > 0 || scene.Land.Length > 0)
                        {
                            deck.Land(page, scene.Open.Length > 0 ? scene.Open : scene.Land, light: false);
                        }
                        // §521: a box typed in place, as a click and the keys type in it.
                        if (scene.Box.Length > 0)
                        {
                            var line = deck.Layout!.Blocks.SelectMany(block => block.Lines).First(each => each.Row.Text == scene.Box);
                            var hit = deck.Layout.Controls(line).First(each => each.Hit.Kind is PanelHitKind.Text or PanelHitKind.Number).Hit;
                            _ = EditDeckClickTests.Click(deck, hit);
                            foreach (var c in scene.Into)
                            {
                                _ = deck.Message(deck.Hwnd, TerraFX.Interop.Windows.WM.WM_CHAR, (nuint)c, 0);
                            }
                            Assert.Equal(scene.Into, deck.Editing);
                        }
                        // §551: a row lifted by its handle and carried, held there.
                        if (scene.Lift is { } lift)
                        {
                            var key = deck.Layout!.Blocks.SelectMany(block => block.Lines).Last(each => each.Row.Text == lift.Row).Row.Key;
                            deck.Land(page, key, light: false);
                            var line = deck.Layout!.Blocks.SelectMany(block => block.Lines).Last(each => each.Row.Key == key);
                            var handle = deck.Layout.RectOf(deck.Layout.Controls(line)[0].Hit).Center;
                            _ = deck.Message(deck.FlyoutHwnd, TerraFX.Interop.Windows.WM.WM_LBUTTONDOWN, 1, EditDeckClickTests.At(deck, handle, flyout: true));
                            _ = deck.Message(deck.FlyoutHwnd, TerraFX.Interop.Windows.WM.WM_MOUSEMOVE, 1, EditDeckClickTests.At(deck, handle - new Vector2(0, lift.Up), flyout: true));
                            Assert.NotNull(deck.Reordering);
                        }
                    });
                }
            }
            Assert.Single(widths);
        });

        // The room the edit bar was shown by, kept (EditedDisplay hands the bar over shown).
        static EditDeckRoom RoomOf(EditDeck deck) => deck.Room!;
    }

    /// <summary>
    /// §596: the selection's entries on the real bar, entries as the pages' are, §608 a divider before them, to KARPO_PNG_DIR as selection-&lt;scene&gt;-&lt;dark|light&gt;.png
    /// — none (nothing selected, the room empty), widget (a calendar), folder (a folder selected), both (a calendar in a folder:
    /// two entries, the widget's flyout), both-folder (the same, the folder's flyout), several (two clocks: one entry) — and
    /// selection-widths.txt, the bar's width in each (px at 150 %): one width, every flyout's beak over its own entry.
    /// </summary>
    [UiFact]
    public void SelectionSheets()
    {
        (string Name, PanelPage[] Chosen, PanelPage? Open, bool Several)[] scenes =
        [
            ("none", [], null, false),
            ("widget", [PanelPage.Widget], PanelPage.Widget, false),
            ("folder", [PanelPage.Folder], PanelPage.Folder, false),
            ("both", [PanelPage.Folder, PanelPage.Widget], PanelPage.Widget, false),
            ("both-folder", [PanelPage.Folder, PanelPage.Widget], PanelPage.Folder, false),
            ("several", [PanelPage.Widget], PanelPage.Widget, true),
        ];
        WindowShots.OnHiddenDesktop("selection", (gfx, instance) =>
        {
            const uint dpi = 144;
            List<string> said = [];
            foreach (var dark in new[] { true, false })
            {
                var look = AddListPictures.LookFor(dark) with { Animations = false };
                foreach (var (name, chosen, open, several) in scenes)
                {
                    var file = $"selection-{name}-{(dark ? "dark" : "light")}.png";
                    EditedDisplay(gfx, instance, dpi, new Config(), look, PictureLayout(LayoutType.Taskbar, Edge.Bottom), file, dips: new Vector2(1920f, 1080f), panel: deck =>
                    {
                        var all = SettingsPanelTests.Content(widget: !several, dark: dark, several: several);
                        var content = all with { Pages = [.. all.Pages.Where(page => page.Page is not (PanelPage.Widget or PanelPage.Folder) || chosen.Contains(page.Page))] };
                        deck.Memory = new PanelMemory();
                        deck.Show(gfx, look, dpi / 96f, deck.Room!, deck.State ?? PictureState(), content, open);
                        var layout = deck.Layout!;
                        var entries = layout.Header.Where(part => part.Page is PanelPage.Widget or PanelPage.Folder).ToList();
                        Assert.Equal(chosen, entries.Select(part => part.Page));
                        Assert.All(entries, part => Assert.Equal(layout.ButtonWide(part.Words, chevron: false), part.Rect.Width));
                        // §608: the divider before them while there are any.
                        Assert.Equal(chosen.Length == 0, layout.Divider.IsEmpty);
                        var wide = deck.PanelOnScreen.right - deck.PanelOnScreen.left;
                        said.Add($"{file}: bar {wide} px; {string.Join(", ", entries.Select(part => $"{part.Words} {part.Rect.Width} px"))}");
                        if (open is { } page)
                        {
                            var entry = deck.OnScreen(new PanelHit(PanelHitKind.Nav, Page: page));
                            Assert.InRange(deck.FlyoutOnScreen.left + deck.FlyoutBeak!.Value.Tip - ((entry.left + entry.right) / 2f), -1, 1);
                        }
                    });
                }
            }
            if (Environment.GetEnvironmentVariable("KARPO_PNG_DIR") is { Length: > 0 } dir)
            {
                File.WriteAllLines(Path.Combine(dir, "selection-widths.txt"), said);
            }
            Assert.Single(said.Select(line => line.Split("bar ")[1].Split(' ')[0]).Distinct());
        });
    }

    /// <summary>
    /// §593 (the person, 2026-11-09: "i want the add menu to be shown on the top in the same width as the edit bar all the
    /// time"): <paramref name="deck"/>'s add page hung from the whole bar, flush with its sides, 12 DIP off it, no beak.
    /// </summary>
    internal static void HungFromTheBar(EditDeck deck)
    {
        var (bar, page) = (deck.PanelOnScreen, deck.FlyoutOnScreen);
        Assert.Equal(PanelPage.Add, deck.Page);
        Assert.Null(deck.FlyoutBeak);
        Assert.Equal((bar.left, bar.right), (page.left, page.right));
        var gap = (int)MathF.Round(Beak.GapDip * deck.Layout!.Scale, MidpointRounding.AwayFromZero);
        Assert.True(Math.Abs(page.top - bar.bottom - gap) <= 1 || Math.Abs(bar.top - page.bottom - gap) <= 1,
            $"the add page {page.left},{page.top}..{page.right},{page.bottom} is not {gap} px off the bar {bar.left},{bar.top}..{bar.right},{bar.bottom}");
    }
}
