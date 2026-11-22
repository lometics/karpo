// menus: every context menu Karpo draws, on numbered contact sheets to be looked at together (the
// person, 2026-10-23: "i want to improve the context menus that are everywhere. can you basically
// screenshot them all and then show me all in one image numbered"). Each menu is built by the code the
// dock builds it with (Dock.Menus.cs, Dock.FaceMenus.cs, Dock.WidgetMenus.cs, Dock.MenuFloor.cs,
// OptionMenu.cs, Dock.BarLooks.cs, QuickLinks.cs, WindowMenus.cs, AddList.cs), shown on a real flyout
// as ShowFlyout sets it (Configures for a widget's menu, Configuration arranged) at 150 %, dark, on a
// desktop of its own, and captured as the compositor draws it (Edit\WindowShots). Nothing is live: a
// row a widget's state decides (what is playing, a drive plugged in, a pin's file) is as it is with
// nothing there. Menus that read alike (a lone "… settings" row) share one tile, the others named.
//
// Written to KARPO_MENUS_SHEET (else %TEMP%\karpo-menus-sheet): sheet-1.png, sheet-2.png, … (a grid of
// tiles, each with its number and a caption), tiles\NNN.png, index.md (number → menu, mode, how it is
// opened, the code that builds it) and rows.txt (each menu's rows as text).
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using System.Text;
using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using Karpo.Tests.Localization;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Menus;

[Collection(InLanguage.Collection)]
public sealed unsafe class EveryMenuSheet
{
    private const float Scale = 1.5f;

    private const string Rest = "View mode (at rest)";

    private const string Arranged = "Edit mode (arranged)";

    private static readonly RECT Work = new() { right = (int)(1920 * Scale), bottom = (int)(1040 * Scale) };

    private static readonly POINT Anchor = new() { x = Work.right / 2, y = Work.bottom };

    private const string Display = "\\\\.\\DISPLAY1";

    // The sheets: at most this wide and tall, in px.
    private const int SheetWidth = 4000;
    private const int SheetHeight = 6000;
    private const int Gap = 28;
    private const int Band = 78;
    private const int CaptionHeight = 112;
    private const int HeaderHeight = 96;

    /// <summary>A menu of the sheets: what it is, how it is opened, what builds it, its rows, and the levels opened in it.</summary>
    private sealed record Tile(string Family, string What, string How, string Code, List<FlyoutItem> Items)
    {
        public bool Configures { get; init; }

        public bool Configuration { get; init; }

        /// <summary>§517: an edit bar's menu or a right click's, as tall and as wide as its rows (Flyout.FitsContent).</summary>
        public bool FitsContent { get; init; }

        public string? Title { get; init; }

        /// <summary>What a configuration menu is of, at the start of its top row (Flyout.TypeTitle, §323).</summary>
        public string? TypeTitle { get; init; }

        public FlyoutLayout Layout { get; init; } = FlyoutLayout.List;

        /// <summary>The rows clicked one after another once it shows: a level opened in it.</summary>
        public Func<FlyoutItem, bool>[] Path { get; init; } = [];

        /// <summary>Whether a tile with the same rows stands for it (see <see cref="Signature"/>).</summary>
        public bool Shared { get; init; }

        /// <summary>The other menus that read as this one does.</summary>
        public List<string> Same { get; } = [];
    }

    private static readonly OptionDevices Devices = new(["GPU 0: Intel UHD Graphics 770", "GPU 1: NVIDIA GeForce RTX 4060 Ti"], ["Disk 0 (C:)", "Disk 1 (D:)"])
    {
        Zones = ["W. Europe Standard Time", "Tokyo Standard Time"],
        TrayIcons =
        [
            new TrayMenuIcon("onedrive", "Microsoft OneDrive", false, "OneDrive - Personal"),
            new TrayMenuIcon("teams", "Microsoft Teams", false, "Microsoft Teams"),
            new TrayMenuIcon("steam", "Steam", true, "Steam"),
            new TrayMenuIcon("nvidia", "NVIDIA Settings", true, "NVIDIA Settings"),
        ],
        Targets = new TargetChoices
        {
            Folders =
            [
                TargetChoices.Row("Documents", @"C:\Users\Ada\Documents", OptionGlyphs.Document),
                TargetChoices.Row("Downloads", @"C:\Users\Ada\Downloads", OptionGlyphs.Download),
                TargetChoices.Row("Pictures", @"C:\Users\Ada\Pictures", OptionGlyphs.Picture),
            ],
            Apps = [TargetChoices.App("Calculator", @"shell:AppsFolder\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App")],
        },
    };

    private static Config Fresh()
    {
        var config = new Config();
        config.EditScreen(Display, "One");
        return config;
    }

    // --- What each menu is, built as the dock builds it -----------------------------------------

    /// <summary>A widget's menu at rest, on the bar or in a folder (Dock.cs, WidgetMenu → RestMenuRows).</summary>
    private static List<FlyoutItem> AtRest(WidgetItem item, FaceMenuParts? parts = null) =>
        TheDock.RestMenuRows(item, inFolder: item is CellItem, widget: null, parts).Items;

    /// <summary>A widget's menu while its bar is arranged (Dock.cs, WidgetMenu): its buttons, its settings, a folder's faces, a line's height, its look (§311).</summary>
    private static List<FlyoutItem> Configured(WidgetItem item, OptionPlace place)
    {
        List<OptionPick> picks = [];
        return [.. TheDock.WidgetRowsFor(item), .. OptionMenu.Settings(item, picks, Devices, place, FolderSide.Face, roomCells: item is CellItem ? LineHeight.Most : null)];
    }

    private static Pixels? IconOf(string file) => File.Exists(file) ? Items.Icon(file, (int)(16 * Scale)) : null;

    private static FlyoutIcon AppIcon(string file, string glyph = "\uE737") =>
        IconOf(file) is { } pixels ? new FlyoutIcon.Picture(pixels) : new FlyoutIcon.Glyph(glyph);

    private static string System32(string file) => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), file);

    private static string WindowsDir(string file) => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), file);

    private static string EdgeExe => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft", "Edge", "Application", "msedge.exe");

    private static bool Named(FlyoutItem item, params string[] words) =>
        words.Any(word => item.Text.Contains(word, StringComparison.OrdinalIgnoreCase));

    private static List<Tile> Tiles()
    {
        var tiles = new List<Tile>();
        var place = new OptionPlace(Fresh(), Dark: true);
        var machine = new Karpo.Core.PresetMachine(HasBattery: true, FixedDrives: []);
        var layouts = TheDock.LayoutRows([new TaskbarLayout { Id = "work", Name = "Work" }, new TaskbarLayout { Id = "home", Name = "Home" }], "work");
        var parts = new FaceMenuParts(
            TheDock.DesktopRows(new Switchers.DesktopActions(CanRename: true, null, CanDelete: true, null, CanMove: true, null)),
            layouts);
        Karpo.Core.Preset Preset(string id) => Karpo.Core.Presets.Find(id)!;
        // A pin's item as the bar makes it, for its rows (PinRows): a file, a folder or a link on no PC in particular.
        static Item Pinned(string target, ItemKind kind = ItemKind.Shell) => new() { Target = target, Name = Path.GetFileName(target), Kind = kind, Keys = [] };
        BarItem Folder(string id) => Karpo.Core.Presets.Folder(Preset(id), machine);

        // ---- At rest: the bar and what Windows' own taskbar has --------------------------------
        tiles.Add(new Tile(Rest, "Taskbar (empty bar space)", "right-click on the bar between widgets", "Dock.BarMenu.cs ShowBarMenu → Dock.Menus.cs BarRows (BarMenus.Default + LayoutRows)",
            TheDock.BarRows(layouts)));
        tiles.Add(new Tile(Rest, "Start button", "right-click on Start (Win+X)", "QuickLinks.Items (Dock.cs Context, HitKind.Start)",
            QuickLinks.Items(new QuickLinkMachine(Battery: true, WindowsTerminal: true, Sleep: true, Hibernate: false))));
        var jumps = new List<FlyoutItem>
        {
            new(0, "Pinned", Header: true),
            new(9000, "Inbox – Outlook", AppIcon(EdgeExe)),
            new(0, "Recent", Header: true),
            new(9001, "github.com/karpo/karpo-app", AppIcon(EdgeExe)),
            new(9002, "Weather – MeteoSwiss", AppIcon(EdgeExe)),
            new(0, "Tasks", Header: true),
            new(9003, "New window", AppIcon(EdgeExe)),
            new(9004, "New InPrivate window", AppIcon(EdgeExe)),
        };
        tiles.Add(new Tile(Rest, "Running app button (2 windows, End task on)", "right-click on an app's button", "Dock.cs AppMenuAt → Dock.Menus.cs AppRows",
            TheDock.AppRows(jumps, "Microsoft Edge", AppIcon(EdgeExe), pinned: false, open: 2, buttons: null, Edge.Bottom, endTask: true)));
        List<FlyoutItem> noteJumps = [new(0, "Recent", Header: true), new(9000, "notes.txt", new FlyoutIcon.Glyph("\uE8A5")), new(9001, "todo.md", new FlyoutIcon.Glyph("\uE8A5")), new(9002, "hosts", new FlyoutIcon.Glyph("\uE8A5"))];
        tiles.Add(new Tile(Rest, "App pin (not running)", "right-click on a pinned app", "Dock.cs AppMenuAt → AppRows (PinButtons null at rest)",
            TheDock.AppRows(noteJumps, "Notepad", AppIcon(WindowsDir("notepad.exe")), pinned: true, open: 0, buttons: null, Edge.Bottom, file: TheDock.PinRows(Pinned(WindowsDir("notepad.exe"))).Items)));
        tiles.Add(new Tile(Rest, "App pin (running, 1 window)", "right-click on a pinned, open app", "Dock.cs AppMenuAt → AppRows",
            TheDock.AppRows(noteJumps, "Notepad", AppIcon(WindowsDir("notepad.exe")), pinned: true, open: 1, buttons: null, Edge.Bottom, endTask: true, file: TheDock.PinRows(Pinned(WindowsDir("notepad.exe"))).Items)));
        List<FlyoutItem> folderJumps = [new(0, "Pinned", Header: true), new(9000, "Projects", new FlyoutIcon.Glyph("\uE8B7")), new(0, "Frequent", Header: true), new(9001, "Downloads", new FlyoutIcon.Glyph("\uE8B7")), new(9002, "Pictures", new FlyoutIcon.Glyph("\uE8B7"))];
        tiles.Add(new Tile(Rest, "Folder pin (Documents)", "right-click on a pin to a folder", "Dock.cs AppMenuAt → AppRows (Explorer's jump list)",
            TheDock.AppRows(folderJumps, "Documents", new FlyoutIcon.Glyph("\uE8B7"), pinned: true, open: 0, buttons: null, Edge.Bottom, file: TheDock.PinRows(Pinned(@"C:\Users\Ana\Documents")).Items)));
        tiles.Add(new Tile(Rest, "File pin (budget.xlsx)", "right-click on a pin to a file", "Dock.cs AppMenuAt → AppRows",
            TheDock.AppRows([], "budget.xlsx", new FlyoutIcon.Glyph("\uE9F9"), pinned: true, open: 0, buttons: null, Edge.Bottom, file: TheDock.PinRows(Pinned(@"C:\Users\Ana\Documents\budget.xlsx")).Items)));
        tiles.Add(new Tile(Rest, "Web link pin", "right-click on a pin to a web address", "Dock.cs AppMenuAt → AppRows",
            TheDock.AppRows([], "example.com", new FlyoutIcon.Glyph("\uE774"), pinned: true, open: 0, buttons: null, Edge.Bottom, file: TheDock.PinRows(Pinned("https://example.com", ItemKind.Link)).Items)));
        tiles.Add(new Tile(Rest, "Missing pin (target gone)", "right-click (or click) on a broken pin", "Dock.Pins.cs BrokenFlyout → BrokenMenu → BrokenRows",
            TheDock.BrokenRows("sync", "This item can't be found. It may have been moved or deleted.", @"D:\Tools\sync.exe", inFolder: false)));
        tiles.Add(new Tile(Rest, "App pin inside a folder", "right-click on a pin line in an open folder", "Dock.Pins.cs PinMenu → AppRows (+ PinRows, as on the bar)",
            TheDock.AppRows(noteJumps, "Notepad", AppIcon(WindowsDir("notepad.exe")), pinned: true, open: 0, buttons: null, Edge.Bottom, file: TheDock.PinRows(Pinned(WindowsDir("notepad.exe"))).Items, inFolder: true)) { Shared = true });
        tiles.Add(new Tile(Rest, "Window menu (Shift+right-click, 1 window)", "Shift+right-click on an app with one window", "Dock.AppClicks.cs WindowMenu → WindowMenus.Rows",
            WindowMenus.Rows(
                [
                    new MenuLine(WindowMenus.RestoreCommand, "&Restore", Enabled: false), new MenuLine(WindowMenus.MoveCommand, "&Move"), new MenuLine(WindowMenus.SizeCommand, "&Size"),
                    new MenuLine(WindowMenus.MinimizeCommand, "Mi&nimize"), new MenuLine(WindowMenus.MaximizeCommand, "Ma&ximize"), new MenuLine(0, "", Separator: true),
                    new MenuLine(WindowMenus.CloseCommand, "&Close\tAlt+F4"),
                ],
                new WindowShape())));
        tiles.Add(new Tile(Rest, "Window menu (Shift+right-click, 3 windows)", "Shift+right-click on an app with several windows", "Dock.AppClicks.cs WindowMenu → WindowMenus.GroupRows",
            WindowMenus.GroupRows([new WindowShape(), new WindowShape(Minimized: true), new WindowShape(Maximized: true)], Edge.Bottom, 1, 2, 3)));
        foreach (var (what, state) in new (string, Karpo.Dock.QuickSettings.Services.IndicatorState)[]
        {
            ("Status icon: microphone in use", new() { Microphone = ["Microsoft Teams"] }),
            ("Status icon: do not disturb", new() { DoNotDisturb = true }),
            ("Status icon: energy saver", new() { EnergySaver = true }),
        })
        {
            var icon = Karpo.Dock.QuickSettings.Services.Indicators.Of(state)[0];
            tiles.Add(new Tile(Rest, what, "right-click on a status icon", "Dock.cs Context (IWidgetMenu) → IndicatorsIsland.MenuRows (Indicators.Of)",
                [.. icon.Menu.Select((row, at) => new FlyoutItem(9000 + (uint)at, row.Text, new FlyoutIcon.Glyph(row.Glyph)))]));
        }
        foreach (var kind in Enum.GetValues<BarKind>())
        {
            if (kind is BarKind.Start or BarKind.Pin or BarKind.FolderList or BarKind.Indicators)
            {
                continue;
            }
            var entry = kind.New();
            tiles.Add(new Tile(Rest, $"{kind.Title()}{(kind == BarKind.Apps ? " (running apps strip, its … overflow)" : kind == BarKind.Tray ? " (tray, its ^ chevron)" : "")}", "right-click on the entry", "Dock.cs WidgetMenu → RestMenuRows → Dock.FaceMenus.cs BarMenuRows (ActsFor(BarKind)), floor: Dock.MenuFloor.cs",
                AtRest(entry)) { Configures = true, Shared = true });
        }

        // ---- At rest: folders on the bar, every premade one -----------------------------------
        foreach (var preset in Karpo.Core.Presets.All)
        {
            var folder = Karpo.Core.Presets.Folder(preset, machine);
            var faces = string.Join(" + ", folder.Folder!.Faces.Select(face => face.Info.Title));
            tiles.Add(new Tile(Rest, $"{preset.Title} folder [{faces}]", "right-click on the folder's button", "Dock.cs WidgetMenu → RestMenuRows → Dock.FaceMenus.cs FaceRows (ActsFor(FaceInfo))",
                AtRest(folder, parts)) { Configures = true, Shared = true });
        }
        {
            var plain = BarKind.FolderList.New();
            tiles.Add(new Tile(Rest, "Folder (own icon face, no preset)", "right-click on the folder's button", "Dock.FaceMenus.cs FaceRows (IconId: none), floor: Edit folder",
                AtRest(plain, parts)) { Configures = true, Shared = true });
        }

        // ---- At rest: lines in an open folder ---------------------------------------------------
        foreach (var kind in new[]
        {
            ListKind.Media, ListKind.SoundOutput, ListKind.SoundMixer, ListKind.Timer, ListKind.Stopwatch, ListKind.Alarm,
            ListKind.Drive, ListKind.RemovableDevices, ListKind.Calendar, ListKind.Month, ListKind.NotificationsList, ListKind.Contacts,
            ListKind.Mail, ListKind.Todo, ListKind.Note, ListKind.Heading, ListKind.Cpu, ListKind.CpuProcesses, ListKind.Markets, ListKind.Rss, ListKind.Sports,
            ListKind.WeatherNow, ListKind.Clipboard, ListKind.KeepAwake, ListKind.SnippingTool, ListKind.Battery, ListKind.Wifi, ListKind.ScreenTimeApps,
            ListKind.Desktops, ListKind.Layouts, ListKind.WifiSwitch, ListKind.Sleep, ListKind.SignOut,
        })
        {
            var line = kind.New();
            tiles.Add(new Tile(Rest, $"{line.Info?.Title ?? kind.ToString()} line in a folder", "right-click on the line in an open folder", "Dock.Folders.cs → Dock.cs WidgetMenu → RestMenuRows → FolderMenuRows (ActsFor(ListKind)), floor: Edit folder",
                AtRest(line)) { Configures = true, Shared = true });
        }

        // ---- Arranged: the bar's entries ------------------------------------------------------
        Tile Edit(WidgetItem item, string what, string how, string? code = null) =>
            new(Arranged, what, how, code ?? "Dock.cs WidgetMenu (arranged) → WidgetRowsFor + OptionMenu.Section(OptionRows.Of) [+ FaceSection | + LineHeightRows]", Configured(item, place))
            {
                Configures = true,
                Configuration = true,
                TypeTitle = IslandEdits.Kind(item),
            };
        var app = BarKind.Pin.New();
        app.Pin!.Target = WindowsDir("notepad.exe");
        tiles.Add(Edit(app, "App pin (Notepad)", "click / right-click a pin in edit mode"));
        var documents = ListKind.FolderContents.New();
        documents.Pin!.Target = @"C:\Users\Ada\Documents";
        tiles.Add(Edit(documents, "Folder contents (Documents)", "click / right-click a folder's contents in edit mode"));
        var file = BarKind.Pin.New();
        file.Pin!.Target = @"C:\Users\Ada\Documents\budget.xlsx";
        tiles.Add(Edit(file, "File pin (budget.xlsx)", "click / right-click a file pin in edit mode"));
        var web = BarKind.Pin.New();
        web.Pin!.Target = "https://example.com/news";
        tiles.Add(Edit(web, "Web link pin", "click / right-click a web pin in edit mode"));
        foreach (var kind in Enum.GetValues<BarKind>())
        {
            if (kind is BarKind.Pin or BarKind.FolderList)
            {
                continue;
            }
            tiles.Add(Edit(kind.New(), kind.Title(), "click / right-click the entry in edit mode") with { Shared = true });
        }

        // ---- Arranged: folders on the bar ------------------------------------------------------
        foreach (var id in new[]
        {
            Karpo.Core.Presets.ClockId, Karpo.Core.Presets.NotificationCenterId, Karpo.Core.Presets.CalendarId, Karpo.Core.Presets.MeetingId, Karpo.Core.Presets.WorldClockId,
            Karpo.Core.Presets.WeatherId, Karpo.Core.Presets.AirQualityId, Karpo.Core.Presets.TidesId, Karpo.Core.Presets.QuakesId, Karpo.Core.Presets.SportsId,
            Karpo.Core.Presets.MarketId, Karpo.Core.Presets.NewsId, Karpo.Core.Presets.MailId, Karpo.Core.Presets.ContactsId, Karpo.Core.Presets.NotificationsId,
            Karpo.Core.Presets.TimerId, Karpo.Core.Presets.FocusId, Karpo.Core.Presets.StopwatchId, Karpo.Core.Presets.CpuId, Karpo.Core.Presets.MediaId,
            Karpo.Core.Presets.SoundId, Karpo.Core.Presets.WifiId, Karpo.Core.Presets.BluetoothId, Karpo.Core.Presets.BatteryId, Karpo.Core.Presets.KeyboardId,
            Karpo.Core.Presets.DesktopsId, Karpo.Core.Presets.LayoutsId, Karpo.Core.Presets.ClipboardId, Karpo.Core.Presets.DriveId, Karpo.Core.Presets.RemovableId,
            Karpo.Core.Presets.QuickPanelId, Karpo.Core.Presets.TodayId,
        })
        {
            var folder = Folder(id);
            var faces = string.Join(" + ", folder.Folder!.Faces.Select(face => face.Info.Title));
            tiles.Add(Edit(folder, $"{Preset(id).Title} folder [{faces}]", "click / right-click the folder in edit mode") with { Shared = true });
        }
        {
            var several = BarKind.FolderList.New();
            several.Folder!.Faces = [ClockFace.New(), WeatherFace.New(), FolderFace.Of(FolderFaces.SoundId), FolderFace.Of(FolderFaces.BatteryId), FolderFace.Of(FolderFaces.WifiId)];
            tiles.Add(Edit(several, "Folder with five faces (clock, weather, sound, battery, Wi-Fi)", "click / right-click the folder in edit mode"));
            tiles.Add(new Tile(Rest, "Folder with five faces (clock, weather, sound, battery, Wi-Fi)", "right-click on the folder's button", "Dock.FaceMenus.cs FaceRows",
                AtRest(several, parts)) { Configures = true });
            tiles.Add(Edit(BarKind.FolderList.New(), "Folder (own icon face)", "click / right-click the folder in edit mode"));
        }

        // ---- Arranged: lines in an open folder -------------------------------------------------
        foreach (var kind in new[]
        {
            ListKind.Heading, ListKind.Divider, ListKind.Clock, ListKind.WeatherNow, ListKind.Markets, ListKind.Cpu, ListKind.Gpu, ListKind.Todo,
            ListKind.Note, ListKind.Media, ListKind.Calendar, ListKind.Alarm, ListKind.Timer, ListKind.Rss, ListKind.Sports, ListKind.Quakes, ListKind.Toggles,
            ListKind.Actions, ListKind.Pin, ListKind.Contacts, ListKind.WifiSwitch,
        })
        {
            var line = kind.New();
            tiles.Add(Edit(line, $"{line.Info?.Title ?? kind.ToString()} line in a folder", "click / right-click a line of an open folder in edit mode") with { Shared = true });
        }

        // ---- Arranged: levels opened, the bar's looks, the add lists ----------------------------
        Tile Level(Tile of, string what, params Func<FlyoutItem, bool>[] path) => of with { What = what, How = of.How + " → " + what, Path = path, Shared = false };
        var cpu = ListKind.Cpu.New();
        tiles.Add(Level(Edit(cpu, "Processor line", "edit mode"), "Style list (Processor line)", item => Named(item, "Style")));
        var weather = ListKind.WeatherNow.New();
        weather.Weather = new WeatherOptions { Place = "Gais, Appenzell Ausserrhoden, Schweiz", Latitude = 47.36, Longitude = 9.45 };
        tiles.Add(Level(Edit(weather, "Weather line", "edit mode"), "Place search (Weather line)", item => Named(item, "Location", "Place")));
        var clock = ListKind.Clock.New();
        clock.Clock!.TimeZone = "Tokyo Standard Time";
        tiles.Add(Level(Edit(clock, "Clock line", "edit mode"), "Time zone search (Clock line)", item => Named(item, "Time zone")));
        var markets = ListKind.Markets.New();
        markets.Market = new MarketOptions { Symbol = "MSFT", Detail = "Microsoft Corporation · NASDAQ · USD" };
        tiles.Add(Level(Edit(markets, "Markets line", "edit mode"), "Share search (Markets line)", item => Named(item, "Share", "Symbol", "What it shows", "Stock")));
        tiles.Add(Level(Edit(app, "App pin", "edit mode"), "Icon picker (App pin)", item => Named(item, "Icon")));
        tiles.Add(Level(Edit(app, "App pin", "edit mode"), "Target search (App pin)", item => Named(item, "Target")));
        tiles.Add(Level(Edit(ListKind.Alarm.New(), "Alarm line", "edit mode"), "Time picker (Alarm line)", item => Named(item, "Time")));
        tiles.Add(Level(Edit(ListKind.Alarm.New(), "Alarm line", "edit mode"), "Repeat on (Alarm line)", item => Named(item, "Repeat")));
        tiles.Add(Level(Edit(ListKind.Toggles.New(), "Toggles line", "edit mode"), "Add a toggle (Toggles line)", item => Named(item, "Add a toggle")));
        tiles.Add(Level(Edit(Folder(Karpo.Core.Presets.WeatherId), "Weather folder", "edit mode"), "A face's level (Weather folder → Weather face)", item => item.Reorder == OptionMenu.FaceGroup));
        tiles.Add(Level(Edit(Folder(Karpo.Core.Presets.MarketId), "Markets folder", "edit mode"), "A face's level (Markets folder, a share)", item => item.Reorder == OptionMenu.FaceGroup));
        {
            var currency = BarKind.FolderList.New();
            var face = FolderFace.Of(FolderFaces.MarketsId);
            face.Market = new MarketOptions { Symbol = "EURCHF=X" };
            currency.Folder!.Faces = [face];
            tiles.Add(Level(Edit(currency, "Markets folder", "edit mode"), "A face's level (Markets folder, a currency EUR/CHF)", item => item.Reorder == OptionMenu.FaceGroup));
        }
        tiles.Add(Level(Edit(Folder(Karpo.Core.Presets.ClockId), "Clock folder", "edit mode"), "A face's level (Clock folder → Clock face)", item => item.Reorder == OptionMenu.FaceGroup));
        tiles.Add(Level(Edit(Folder(Karpo.Core.Presets.WeatherId), "Weather folder", "edit mode"), "Add a face (Weather folder)", item => Named(item, "Add a face")));
        tiles.Add(Level(Edit(BarKind.FolderList.New(), "Folder", "edit mode"), "Icon face's level (Folder, own icon)", item => item.Reorder == OptionMenu.FaceGroup));
        tiles.Add(Level(Edit(BarKind.Tray.New(), "Tray", "edit mode"), "Tray icons level (Tray)", item => item.Children is { Count: > 0 } && !Named(item, "Colour", "Color")));
        tiles.Add(Level(Edit(ListKind.Note.New(), "Note line", "edit mode"), "Height level (Note line)", item => LineHeightRows.Holds(item.Id) && item.Children is { Count: > 0 }));
        var config = Fresh();
        var spot = new BarSpot(Display, "One");
        Tile Looks(string what, LooksScope scope, params Func<FlyoutItem, bool>[] path) => new(Arranged, what, (scope == LooksScope.Appearance ? "the edit bar's Look ▾" : "right-click the empty bar in edit mode") + (path.Length > 0 ? " → " + what : ""), "Dock.BarLooks.cs LooksMenu → LooksItems (Karpo.Core BarLooks.Rows)", TheDock.LooksItems(config, spot, dark: true, [], scope: scope))
        {
            Configures = true,
            FitsContent = true,
            TypeTitle = BarLooks.Kind(config, spot, scope),
            Path = path,
        };
        tiles.Add(Looks("Bar's looks (BarLooks)", LooksScope.Taskbar));
        tiles.Add(Looks("Bar's looks → Size", LooksScope.Taskbar, item => item.Text == S.SettingsLayoutsSizeHeader));
        // §517: the Appearance: its rows (§583 its sections page sections, no level), a colour of it open.
        tiles.Add(Looks("Look", LooksScope.Appearance));
        tiles.Add(Looks("Look → Outline", LooksScope.Appearance, item => item.Text == S.SettingsAppearanceStylesOutline));
        return tiles;
    }

    /// <summary>What a menu reads as, for telling two that read alike: its rows' words; a lone row opening a page in Windows reads as any other such.</summary>
    /// <remarks>
    /// Arranged, the heading naming the widget is left out, and a folder's face reads as a face with
    /// settings or without: a folder of one face reads as any other.
    /// </remarks>
    private static string Signature(Tile tile)
    {
        var rows = tile.Items.Where(item => !item.Primary && !(item.Header && tile.Family == Arranged)).ToList();
        if (tile.Family == Rest && rows.Count == 1 && rows[0].Text.EndsWith(" settings", StringComparison.Ordinal))
        {
            return "page";
        }
        return tile.Family + "|" + string.Join("|", rows.Select(item =>
            item.Reorder == OptionMenu.FaceGroup ? (item.Children is { Count: > 0 } ? "face>" : "face")
            : $"{(item.Separator ? "—" : item.Text)}{(item.Header ? "#" : "")}{(item.Children is { Count: > 0 } c ? $"[{c.Count}]" : "")}"));
    }

    // --- Shown and captured ---------------------------------------------------------------------

    private sealed record Shot(Tile Tile, int Number, byte[] Pixels, int Width, int Height);

    /// <summary>The menu's own posted messages handed to it for a moment, as the dock's window procedure hands them.</summary>
    private static void Pump(Flyout menu, int ms = 300)
    {
        var until = Environment.TickCount64 + ms;
        MSG msg;
        while (Environment.TickCount64 < until)
        {
            while (PeekMessageW(&msg, menu.Hwnd, WM.WM_USER, WM.WM_USER + 0xFF, PM.PM_REMOVE) || PeekMessageW(&msg, menu.Hwnd, WM.WM_TIMER, WM.WM_TIMER, PM.PM_REMOVE))
            {
                _ = menu.Message(msg.message, msg.wParam, msg.lParam);
            }
            Thread.Sleep(10);
        }
    }

    /// <summary>Clicks the rows <paramref name="path"/> names, one level after another; false where one is not there.</summary>
    private static bool OpenPath(Flyout menu, IReadOnlyList<FlyoutItem> items, IReadOnlyList<Func<FlyoutItem, bool>> path, out string missed)
    {
        IReadOnlyList<FlyoutItem> level = items;
        missed = "";
        foreach (var step in path)
        {
            var row = level.FirstOrDefault(item => !item.Separator && !item.Header && step(item));
            if (row is null)
            {
                missed = string.Join(", ", level.Where(item => !item.Separator).Select(item => item.Text));
                return false;
            }
            _ = menu.Click(row.Id);
            Pump(menu);
            level = row.Children ?? [];
        }
        return true;
    }

    [UiFact]
    public void EveryMenuSheets()
    {
        var folder = Environment.GetEnvironmentVariable("KARPO_MENUS_SHEET") is { Length: > 0 } named ? named : Path.Combine(Path.GetTempPath(), "karpo-menus-sheet");
        Directory.CreateDirectory(Path.Combine(folder, "tiles"));
        foreach (var old in Directory.GetFiles(folder, "sheet-*.png").Concat(Directory.GetFiles(Path.Combine(folder, "tiles"), "*.png")))
        {
            File.Delete(old);
        }
        var all = Tiles();
        // Menus that read alike share the first one's tile.
        var tiles = new List<Tile>();
        var seen = new Dictionary<string, Tile>();
        foreach (var tile in all)
        {
            if (tile.Items.Count == 0 && tile.Path.Length == 0)
            {
                continue;
            }
            var key = Signature(tile);
            if (tile.Shared && tile.Path.Length == 0)
            {
                if (seen.TryGetValue(key, out var first))
                {
                    // A lone page row reads alike but names its own page.
                    first.Same.Add(key == "page" ? $"{tile.What} (\"{tile.Items.Single(item => !item.Separator && !item.Header).Text}\")" : tile.What);
                    continue;
                }
                seen.Add(key, tile);
            }
            tiles.Add(tile);
        }
        // View mode first, then edit mode, each in the order made.
        tiles = [.. tiles.Where(tile => tile.Family == Rest), .. tiles.Where(tile => tile.Family == Arranged)];

        var shots = new List<Shot>();
        var problems = new List<string>();
        var rows = new StringBuilder();
        WindowShots.OnHiddenDesktop("every-menu", (gfx, instance) =>
        {
            var look = AddListPictures.LookFor(dark: true);
            // One flyout shown again and again, as the dock keeps one.
            using var menu = new Flyout(gfx, instance, 0);
            for (var at = 0; at < tiles.Count; at++)
            {
                var tile = tiles[at];
                var number = at + 1;
                menu.Configures = tile.Configures;
                menu.Configuration = tile.Configuration;
                menu.FitsContent = tile.FitsContent;
                menu.TypeTitle = tile.TypeTitle;
                menu.SpokenName = tile.What;
                menu.Show(gfx, look, Scale, tile.Items, tile.Layout, Anchor, Work, Edge.Bottom, tile.Title);
                WindowShots.Settle();
                if (tile.Path.Length > 0 && !OpenPath(menu, tile.Items, tile.Path, out var missed))
                {
                    problems.Add($"#{number} {tile.What}: no row to open among: {missed}");
                }
                WindowShots.Settle(250);
                if (menu.InlineDepth == 0 && TopRowOff(menu, tile.Items) is { } off)
                {
                    problems.Add($"#{number} {tile.What}: {off}");
                }
                var panels = menu.OpenPanels;
                var frame = new RECT
                {
                    left = panels.Min(panel => panel.Panel.left),
                    top = panels.Min(panel => panel.Panel.top),
                    right = panels.Max(panel => panel.Panel.right),
                    bottom = panels.Max(panel => panel.Panel.bottom),
                };
                var (canvas, every) = WindowShots.Together(panels, frame, "every-menu-scratch.png", (0x2B, 0x24, 0x1E));
                if (!every)
                {
                    problems.Add($"#{number} {tile.What}: came back blank");
                }
                var width = frame.right - frame.left;
                var height = frame.bottom - frame.top;
                WindowShots.WritePng(canvas, width, height, Path.Combine(folder, "tiles", $"{number:000}.png"));
                shots.Add(new Shot(tile, number, canvas, width, height));
                rows.AppendLine($"#{number} {tile.What} — {tile.Family}{(menu.InlineTitle is { } title ? $" (level: {title})" : "")}");
                Dump(rows, tile.Items, "    ", 2);
                rows.AppendLine();
                menu.Hide();
            }
        });
        File.WriteAllText(Path.Combine(folder, "rows.txt"), rows.ToString());
        var sheets = Sheets(shots, folder);
        File.WriteAllText(Path.Combine(folder, "index.md"), Index(shots, sheets));
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    /// <summary>
    /// What is wrong with the menu's top row, null for nothing (§350): its icon buttons all together at the
    /// row's right end, the last its edge and plate in from the panel's right, the gaps between them alike.
    /// </summary>
    private static string? TopRowOff(Flyout menu, IReadOnlyList<FlyoutItem> items)
    {
        var boxes = items.Where(Flyout.IsButton).Select(item => menu.ItemOnScreen(item.Id)).OfType<RECT>().ToList();
        if (boxes.Count == 0)
        {
            return null;
        }
        static int Px(float dip) => (int)MathF.Round(dip * Scale, MidpointRounding.AwayFromZero);
        var panel = menu.OpenPanels[0].Panel;
        var inFromEnd = panel.right - boxes[^1].right;
        var least = Px(Flyout.PrimaryEdgeDip + Flyout.PrimaryPlateDip);
        if (inFromEnd < least || inFromEnd > least + 2)
        {
            return $"its last icon button {inFromEnd} px in from the right, not {least}";
        }
        var gaps = boxes.Zip(boxes.Skip(1), (near, far) => far.left - near.right).Distinct().ToList();
        return gaps.Count > 1 ? $"its icon buttons' gaps differ: {string.Join(", ", gaps)} px" : null;
    }

    private static void Dump(StringBuilder into, IReadOnlyList<FlyoutItem> items, string indent, int depth)
    {
        foreach (var item in items)
        {
            if (item.Separator)
            {
                into.AppendLine(indent + "────");
                continue;
            }
            var marks = new List<string>();
            if (item.Header) marks.Add("heading");
            if (item.Primary) marks.Add("button");
            if (!item.Enabled) marks.Add("greyed");
            if (item.Checked == true) marks.Add("ticked");
            if (item.Icon is null && !item.Header) marks.Add("no icon");
            if (item.Value is not null) marks.Add("value");
            if (item.Control is not null) marks.Add(item.Control.GetType().Name);
            if (item.Foot) marks.Add("foot");
            if (item.Detail is { Length: > 0 } detail) marks.Add("detail: " + detail.Replace('\n', ' '));
            if (item.Shortcut is { Length: > 0 } keys) marks.Add("keys: " + keys);
            into.AppendLine($"{indent}{item.Text}{(marks.Count > 0 ? "  [" + string.Join(", ", marks) + "]" : "")}");
            if (depth > 1 && item.Children is { Count: > 0 } children)
            {
                Dump(into, children, indent + "    ", depth - 1);
            }
        }
    }

    // --- The sheets -----------------------------------------------------------------------------

    private sealed record Placed(Shot Shot, int X, int Y, int CellWidth);

    private sealed record Sheet(int Index, string Family, int Width, int Height, List<Placed> Tiles);

    /// <summary>The shots laid out in a grid, a family's sheets after another's, and drawn; the sheets made.</summary>
    private static List<Sheet> Sheets(IReadOnlyList<Shot> shots, string folder)
    {
        var sheets = new List<Sheet>();
        foreach (var family in new[] { Rest, Arranged })
        {
            var mine = shots.Where(shot => shot.Tile.Family == family).ToList();
            if (mine.Count == 0)
            {
                continue;
            }
            // One column width for the family: its common menus', a wider one spanning columns.
            var cell = Math.Max(560, mine.Where(shot => shot.Width <= 900).Select(shot => shot.Width).DefaultIfEmpty(560).Max());
            var columns = Math.Max(1, (SheetWidth - Gap) / (cell + Gap));
            var sheet = new Sheet(sheets.Count + 1, family, Gap + (columns * (cell + Gap)), 0, []);
            var y = HeaderHeight;
            var column = 0;
            var rowHeight = 0;
            foreach (var shot in mine)
            {
                var span = Math.Min(columns, Math.Max(1, (shot.Width + Gap + cell - 1) / (cell + Gap)));
                var tall = Band + shot.Height + CaptionHeight;
                if (column + span > columns)
                {
                    y += rowHeight + Gap;
                    column = 0;
                    rowHeight = 0;
                }
                if (y + tall > SheetHeight && sheet.Tiles.Count > 0)
                {
                    sheets.Add(sheet with { Height = y + rowHeight + Gap });
                    sheet = new Sheet(sheets.Count + 1, family, sheet.Width, 0, []);
                    y = HeaderHeight;
                    column = 0;
                    rowHeight = 0;
                }
                sheet.Tiles.Add(new Placed(shot, Gap + (column * (cell + Gap)), y, (span * cell) + ((span - 1) * Gap)));
                column += span;
                rowHeight = Math.Max(rowHeight, tall);
            }
            sheets.Add(sheet with { Height = y + rowHeight + Gap });
        }
        foreach (var sheet in sheets)
        {
            Draw(sheet, sheets.Count, Path.Combine(folder, $"sheet-{sheet.Index}.png"));
        }
        return sheets;
    }

    private static uint Rgb(byte r, byte g, byte b) => r | ((uint)g << 8) | ((uint)b << 16);

    private static HFONT Font(int px, bool bold)
    {
        fixed (char* face = "Segoe UI")
        {
            return CreateFontW(-px, 0, 0, 0, bold ? 700 : 400, 0, 0, 0, 1, 0, 0, 4, 0, face);
        }
    }

    private static void Text(HDC dc, string text, RECT box, HFONT font, uint color, uint format)
    {
        var old = SelectObject(dc, (HGDIOBJ)font.Value);
        _ = SetTextColor(dc, (COLORREF)color);
        fixed (char* chars = text)
        {
            _ = DrawTextW(dc, chars, text.Length, &box, format | 0x800 /* DT_NOPREFIX */);
        }
        _ = SelectObject(dc, old);
    }

    private static void Fill(HDC dc, RECT box, uint color, int round = 0)
    {
        var brush = CreateSolidBrush((COLORREF)color);
        if (round == 0)
        {
            _ = FillRect(dc, &box, brush);
        }
        else
        {
            var oldBrush = SelectObject(dc, (HGDIOBJ)brush.Value);
            var oldPen = SelectObject(dc, GetStockObject(8 /* NULL_PEN */));
            _ = RoundRect(dc, box.left, box.top, box.right + 1, box.bottom + 1, round, round);
            _ = SelectObject(dc, oldPen);
            _ = SelectObject(dc, oldBrush);
        }
        _ = DeleteObject((HGDIOBJ)brush.Value);
    }

    private static void Draw(Sheet sheet, int count, string path)
    {
        var width = sheet.Width;
        var height = sheet.Height;
        var screen = GetDC(HWND.NULL);
        var dc = CreateCompatibleDC(screen);
        var info = new BITMAPINFO();
        info.bmiHeader.biSize = (uint)sizeof(BITMAPINFOHEADER);
        info.bmiHeader.biWidth = width;
        info.bmiHeader.biHeight = -height;
        info.bmiHeader.biPlanes = 1;
        info.bmiHeader.biBitCount = 32;
        info.bmiHeader.biCompression = BI.BI_RGB;
        void* bits = null;
        var dib = CreateDIBSection(dc, &info, DIB_RGB_COLORS, &bits, HANDLE.NULL, 0);
        var oldBitmap = SelectObject(dc, dib);
        var big = Font(44, bold: true);
        var title = Font(30, bold: true);
        var caption = Font(24, bold: true);
        var small = Font(21, bold: false);
        try
        {
            Fill(dc, new RECT { right = width, bottom = height }, Rgb(0x1E, 0x24, 0x2B));
            _ = SetBkMode(dc, 1 /* TRANSPARENT */);
            Text(dc, $"Karpo context menus · sheet {sheet.Index} of {count} · {sheet.Family} · 150 % · dark", new RECT { left = Gap, top = 0, right = width - Gap, bottom = HeaderHeight }, title, Rgb(0xFF, 0xFF, 0xFF), 0x4 | 0x20 /* DT_VCENTER | DT_SINGLELINE */);
            foreach (var placed in sheet.Tiles)
            {
                var shot = placed.Shot;
                // A card behind the tile, so each reads as one.
                var card = new RECT { left = placed.X - 10, top = placed.Y - 6, right = placed.X + placed.CellWidth + 10, bottom = placed.Y + Band + shot.Height + CaptionHeight - 8 };
                Fill(dc, card, Rgb(0x28, 0x30, 0x3A), 18);
                var number = shot.Number.ToString();
                var badge = new RECT { left = placed.X, top = placed.Y + 6, right = placed.X + Math.Max(76, 34 + (number.Length * 26)), bottom = placed.Y + Band - 8 };
                Fill(dc, badge, Rgb(0x00, 0x78, 0xD4), 20);
                Text(dc, number, badge, big, Rgb(0xFF, 0xFF, 0xFF), 0x1 | 0x4 | 0x20 /* DT_CENTER | DT_VCENTER | DT_SINGLELINE */);
                var mode = shot.Tile.Family == Rest ? "VIEW MODE" : "EDIT MODE";
                Text(dc, mode, new RECT { left = badge.right + 14, top = badge.top, right = placed.X + placed.CellWidth, bottom = badge.bottom }, small, shot.Tile.Family == Rest ? Rgb(0x9C, 0xDC, 0xFE) : Rgb(0xFF, 0xC8, 0x6B), 0x4 | 0x20);
                GdiFlush();
                var top = placed.Y + Band;
                for (var y = 0; y < shot.Height && top + y < height; y++)
                {
                    var into = (byte*)bits + ((((top + y) * width) + placed.X) * 4);
                    fixed (byte* from = &shot.Pixels[y * shot.Width * 4])
                    {
                        Buffer.MemoryCopy(from, into, (width - placed.X) * 4, Math.Min(shot.Width, width - placed.X) * 4);
                    }
                }
                var words = new RECT { left = placed.X, top = top + shot.Height + 8, right = placed.X + placed.CellWidth, bottom = top + shot.Height + 8 + 34 };
                Text(dc, $"{shot.Number}. {shot.Tile.What}", words, caption, Rgb(0xFF, 0xFF, 0xFF), 0x20 | 0x8000 /* DT_SINGLELINE | DT_END_ELLIPSIS */);
                var how = shot.Tile.How + (shot.Tile.Same.Count > 0 ? $" · same: {string.Join(", ", shot.Tile.Same)}" : "");
                var lines = new RECT { left = placed.X, top = words.bottom, right = placed.X + placed.CellWidth, bottom = top + shot.Height + CaptionHeight - 14 };
                Text(dc, how, lines, small, Rgb(0xC8, 0xCE, 0xD6), 0x10 | 0x2000 | 0x8000 /* DT_WORDBREAK | DT_EDITCONTROL | DT_END_ELLIPSIS */);
            }
            GdiFlush();
            var pixels = new byte[width * height * 4];
            fixed (byte* to = pixels)
            {
                Buffer.MemoryCopy(bits, to, pixels.Length, pixels.Length);
            }
            for (var at = 3; at < pixels.Length; at += 4)
            {
                pixels[at] = 255;
            }
            WindowShots.WritePng(pixels, width, height, path);
        }
        finally
        {
            foreach (var font in new[] { big, title, caption, small })
            {
                _ = DeleteObject((HGDIOBJ)font.Value);
            }
            _ = SelectObject(dc, oldBitmap);
            _ = DeleteObject(dib);
            _ = DeleteDC(dc);
            _ = ReleaseDC(HWND.NULL, screen);
        }
    }

    private static string Index(IReadOnlyList<Shot> shots, IReadOnlyList<Sheet> sheets)
    {
        var text = new StringBuilder();
        text.AppendLine("# Karpo context menus — index");
        text.AppendLine();
        text.AppendLine("Every menu built by the dock's own code, shown on a real flyout at 150 %, dark (Karpo.Tests/Menus/EveryMenuSheet.cs).");
        text.AppendLine("Nothing is live: rows a widget's state decides (what is playing, a drive plugged in, a pin's file, a contact) show as with nothing there.");
        text.AppendLine();
        foreach (var sheet in sheets)
        {
            text.AppendLine($"## sheet-{sheet.Index}.png — {sheet.Family}");
            text.AppendLine();
            text.AppendLine("| # | Menu | Mode | Opened by | Built by | Same menu for |");
            text.AppendLine("|---|------|------|-----------|----------|---------------|");
            foreach (var placed in sheet.Tiles)
            {
                var tile = placed.Shot.Tile;
                static string Cell(string words) => words.Replace("|", "\\|");
                text.AppendLine($"| {placed.Shot.Number} | {Cell(tile.What)} | {(tile.Family == Rest ? "view" : "edit")} | {Cell(tile.How)} | {Cell(tile.Code)} | {Cell(string.Join(", ", tile.Same))} |");
            }
            text.AppendLine();
        }
        return text.ToString();
    }
}
