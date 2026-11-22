// §472: the layouts a new one starts from (Karpo.Core\LayoutPresets.cs): §628 "Add a layout"'s page lists them, Blank first,
// a pick makes one ordinary layout of the one picked, shown on the display, and nothing remembers the preset. Held here:
//
//  - **Each is a valid layout**: Config's repair changes nothing of it, every widget is a kind this build has, every
//    folder's lines are lines a folder holds at heights their kinds allow, every id its own.
//  - **A pick adds one layout**: exactly one, the preset's type, sizes, look (its palette's values), icon and widgets, under its name ("Windows",
//    "Windows 2"), shown on the display; nothing in the file names the preset.
//  - **Karpo's is what a new layout was**, and a new config's first layout still is it.
//  - **Windows' is Windows' own** (§474): Start, Search, Task view as Windows' buttons; quick settings' button and the
//    clock Windows' own widgets wearing Windows' faces, opening quick settings and the notifications — never pressed here.
//    No Widgets button (§485). No folder of Karpo's stands for a menu of Windows'; no other preset has one of those
//    widgets.
//  - **Windows' faces are fixed** (§474): what each of the three wears, the battery only with one, and nothing else wears
//    one; their menu has no face nor contents to edit.
//  - **A folder opens itself** (§474, §472's "Opens" gone): no row offers anything else, and an "opens" an older file has
//    is not kept.
//  - §628 the page of them, each a tile picturing its bar, is AddLayoutPageTests' (§472 to §627 a menu's level here).

using System.Text.Json.Nodes;
using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using Karpo.Tests.Localization;

namespace Karpo.Tests.Core;

[Collection(InLanguage.Collection)]
public sealed class LayoutPresetsTests
{
    private const string Display = "\\\\.\\DISPLAY1";

    /// <summary>A laptop as Windows ships it: Windows' own pins, a pen and a touch screen, a battery.</summary>
    internal static readonly LayoutMachine Laptop = new(
        [@"C:\Windows\explorer.exe", WindowsTaskbar.StoreApp],
        new InputHardware(Pen: true, Touch: true, Slate: false),
        WindowsTaskbar.OutOfTheBox);

    /// <summary>A desktop PC: no pins, no pen, no touch, no battery.</summary>
    internal static readonly LayoutMachine Desktop = new([], new InputHardware(Pen: false, Touch: false, Slate: false), WindowsTaskbar.OutOfTheBox with { Battery = false });

    public static TheoryData<string> Ids()
    {
        var data = new TheoryData<string>();
        foreach (var preset in LayoutPresets.All)
        {
            data.Add(preset.Id);
        }
        return data;
    }

    private static BarSpot Spot() => new(Display, "One");

    /// <summary>A config as a new install has it, its display known.</summary>
    private static Config Fresh()
    {
        var config = Config.Parse("{}");
        config.Repair();
        config.EditScreen(Display, "One");
        config.Repair();
        return config;
    }

    /// <summary>Every widget of <paramref name="layout"/>, a folder's lines among them.</summary>
    private static IEnumerable<WidgetItem> Everything(TaskbarLayout layout) =>
        layout.Islands.SelectMany(island => (IEnumerable<WidgetItem>)[island, .. island.Folder?.Cells.Select(cell => cell.Island) ?? []]);

    /// <summary>The folder of <paramref name="layout"/> whose first face is <paramref name="face"/>.</summary>
    private static BarItem FolderOf(TaskbarLayout layout, string face) =>
        Assert.Single(layout.Islands, island => island.Folder?.Face.Kind == face);

    [Theory]
    [MemberData(nameof(Ids))]
    public void EachIsAValidLayout(string id)
    {
        foreach (var machine in new[] { Laptop, Desktop })
        {
            var config = Fresh();
            var preset = LayoutPresets.Find(id)!;
            var layout = BarLooks.Add(config, preset, machine: machine);
            var written = config.ToJson();

            // Repair finds nothing to set right, and the file reads back as it was written.
            Assert.False(config.Repair(), $"{id}: repair changed the layout");
            Assert.Equal(written, config.ToJson());
            var again = Config.Parse(written);
            again.Repair();
            Assert.Equal(written, again.ToJson());

            // §628: Blank's bar holds nothing, and a file of it is as valid; every other's something.
            Assert.Equal(preset == LayoutPresets.Blank, layout.Islands.Count == 0);
            foreach (var island in layout.Islands)
            {
                Assert.NotNull(BarEntries.Find(island.Kind));
                foreach (var cell in island.Folder?.Cells ?? [])
                {
                    var line = cell.Island;
                    Assert.True(Karpo.Core.Folders.CanAdd(island.Kind, line.Kind), $"{id}: a folder holds no {line.Kind}");
                    if (line.Height is { } height && ListWidgets.Find(line.Info?.Id) is { } kind)
                    {
                        Assert.Equal(height.Within(kind.Kind), height);
                    }
                }
            }
            var ids = config.Layouts.SelectMany(each => Karpo.Core.Folders.All(each.Islands)).Select(entry => entry.Island.Id).ToList();
            Assert.Equal(ids.Count, ids.Distinct(StringComparer.OrdinalIgnoreCase).Count());
            // Its battery's face only on a PC with one, as Windows shows it.
            Assert.Equal(machine.Taskbar.Battery && layout.Islands.Any(island => island.Folder?.Face.Kind == FolderFaces.WifiId),
                layout.Islands.Any(island => island.Folder?.Faces.Any(face => face.Kind == FolderFaces.BatteryId) == true));
        }
    }

    /// <summary>Each with a name, a line saying what it is for and an icon of its own; §628 Blank first, then Windows' and Karpo's.</summary>
    [Fact]
    public void EachHasItsNameItsLineAndAnIconOfItsOwn()
    {
        Assert.Equal([LayoutPresets.BlankId, LayoutPresets.WindowsId, LayoutPresets.KarpoId], LayoutPresets.All.Take(3).Select(preset => preset.Id));
        Assert.All(LayoutPresets.All, preset =>
        {
            Assert.False(string.IsNullOrWhiteSpace(preset.Title));
            Assert.False(string.IsNullOrWhiteSpace(preset.Blurb));
            Assert.NotEqual(preset.Id, preset.Title);
            Assert.InRange(preset.Size, Config.MinHeight, Config.MaxHeight);
        });
        Assert.Equal(LayoutPresets.All.Length, LayoutPresets.All.Select(preset => preset.Glyph).Distinct().Count());
        Assert.Equal(LayoutPresets.All.Length, LayoutPresets.All.Select(preset => preset.Title).Distinct().Count());
    }

    /// <summary>
    /// A pick adds exactly one layout: at the end, the preset's type, sizes, icon and widgets, under its name, shown on the
    /// display; every other layout as it was; §567 the Appearance as it was (a layout has no look); and nothing in the file
    /// says which preset it was.
    /// </summary>
    [Theory]
    [MemberData(nameof(Ids))]
    public void APickAddsOneLayoutOfThePresetAndNothingLinksBack(string id)
    {
        var config = Fresh();
        var preset = LayoutPresets.Find(id)!;
        var before = JsonNode.Parse(config.ToJson())!["layouts"]!.ToJsonString();
        var appearance = config.Appearance;
        // §628: its tile on the Add a layout page.
        OptionRow Tile() => OptionRows.Find(BarLooks.Rows(config, Spot(), dark: true, scope: LooksScope.AddLayout, machine: Laptop), BarLooks.KeyOf(BarLooks.PresetRow, id))!;

        Assert.True(Tile().Set!(0));

        Assert.Equal(2, config.Layouts.Count);
        var made = config.Layouts[^1];
        Assert.Equal(made.Id, config.ActiveLayout(Display).Id);
        Assert.Equal((preset.Title, preset.Glyph, preset.Type, preset.Size), (made.Name, made.Icon, made.Type, made.Size));
        Assert.Same(appearance, config.Appearance);
        var file = JsonNode.Parse(config.ToJson())!["layouts"]!.AsArray();
        Assert.Equal(before, new JsonArray([.. file.Take(1).Select(node => node!.DeepClone())]).ToJsonString());
        // Its widgets the preset's, in their order, each with an id of its own.
        var fresh = LayoutPresets.Make(preset, Laptop, "x");
        Assert.Equal(
            fresh.Islands.Select(island => (island.Kind, island.Section, island.Folder?.Face.Kind)),
            made.Islands.Select(island => (island.Kind, island.Section, island.Folder?.Face.Kind)));
        // Nothing names it: no key of the file is about a preset, and the layout has the properties every layout has.
        Assert.DoesNotContain(Keys(file[^1]!), key => key.Contains("preset", StringComparison.OrdinalIgnoreCase));
        var plain = JsonNode.Parse(Config.Parse(config.ToJson()).ToJson())!["layouts"]![0]!.AsObject().Select(pair => pair.Key).ToHashSet();
        Assert.Subset(plain, file[^1]!.AsObject().Select(pair => pair.Key).ToHashSet());

        // Picked again: "Windows 2".
        Assert.True(Tile().Set!(0));
        Assert.Equal(S.SettingsLayoutsNumbered(preset.Title, 2), config.ActiveLayout(Display).Name);
    }

    /// <summary>Every property name in <paramref name="node"/>, all the way down.</summary>
    private static IEnumerable<string> Keys(JsonNode node) => node switch
    {
        JsonObject o => o.SelectMany(pair => (IEnumerable<string>)[pair.Key, .. pair.Value is null ? [] : Keys(pair.Value)]),
        JsonArray a => a.SelectMany(each => each is null ? [] : Keys(each)),
        _ => [],
    };

    /// <summary>
    /// Past what the licence holds the row is greyed, saying why, and §628 so is every tile of its page, each making nothing.
    /// </summary>
    [Fact]
    public void PastTheLicenceAddIsGreyedAndMakesNothing()
    {
        var config = Fresh();
        var free = new LicenseStatus(LicenseTier.Free, LicenseBasis.StoreFree);
        while (LicenseGate.Layouts(free, config.Layouts.Count) is null)
        {
            Assert.True(BarLooks.AddShown(config, Spot(), LayoutPresets.Focus, free));
        }
        var count = config.Layouts.Count;
        var add = OptionRows.Find(BarLooks.Rows(config, Spot(), dark: true, LooksScope.Layouts, free), BarLooks.AddRow)!;
        Assert.False(add.Enabled);
        Assert.False(string.IsNullOrEmpty(add.Why));
        var tiles = BarLooks.Rows(config, Spot(), dark: true, LooksScope.AddLayout, free, Laptop);
        Assert.Equal(LayoutPresets.All.Length, tiles.Count);
        Assert.All(tiles, tile =>
        {
            Assert.False(tile.Enabled);
            Assert.Equal(add.Why, tile.Why);
            Assert.False(tile.Set!(0));
        });
        Assert.Equal(count, config.Layouts.Count);
    }

    /// <summary>
    /// Karpo's is what a new layout was until now — Windows 11's buttons at the left, Karpo's quick settings, keyboard and clock
    /// at the right (Sections.DefaultLayout) —, and a new config's first layout is still it.
    /// </summary>
    [Fact]
    public void KarposIsTodaysDefaultAndANewConfigsFirst()
    {
        var made = LayoutPresets.Make(LayoutPresets.Karpo, Laptop, "Karpo");
        var today = Sections.DefaultLayout(Laptop.Pins, Laptop.Inputs, Laptop.Taskbar);
        Assert.Equal(
            today.Select(island => (island.Kind, island.Section, island.Folder?.Face.Kind, island.Pin?.Target)),
            made.Islands.Select(island => (island.Kind, island.Section, island.Folder?.Face.Kind, island.Pin?.Target)));
        Assert.Equal((LayoutType.Taskbar, Config.DefaultHeight, EdgeMode.Reserve, TaskbarLayouts.LayoutGlyph), (made.Type, made.Size, made.Show, made.Icon));

        var first = Config.FirstLayout();
        var karpo = LayoutPresets.Make(LayoutPresets.Karpo, LayoutMachine.Read(), TaskbarLayouts.DefaultName);
        Assert.Equal((TaskbarLayouts.DefaultId, TaskbarLayouts.DefaultName), (first.Id, first.Name));
        Assert.Equal(karpo.Islands.Select(island => (island.Kind, island.Section)), first.Islands.Select(island => (island.Kind, island.Section)));
        Assert.Equal(first.Islands.Select(island => island.Kind), Config.Parse("{}").DefaultLayout.Islands.Select(island => island.Kind));
    }

    /// <summary>
    /// §488: the Monitor is one taskbar (it was a thin bar along the top over a taskbar, a Top and bottom, until that type
    /// went), 48 DIPs: Start, the search, Task view and the desktops at the left; the pins and the open apps in the
    /// middle; at the right the processor, the graphics, the memory, the disk and the network each its figure, then the
    /// clipboard, the tray and its icons, quick settings, the notifications and show desktop.
    /// </summary>
    [Fact]
    public void MonitorIsOneTaskbarWithItsReadingsBesideTheTray()
    {
        var layout = LayoutPresets.Make(LayoutPresets.Monitor, Desktop, "Monitor");
        Assert.Equal((LayoutType.Taskbar, Config.DefaultHeight, EdgeMode.Reserve), (layout.Type, layout.Size, layout.Show));
        string Of(BarItem island) => island.Folder?.Face.Kind ?? island.Kind.ToString();
        string Folder(string id) => Of(Presets.Folder(Presets.Find(id)!, Desktop.Folders));
        IEnumerable<string> In(BarSection section) => layout.Islands.Where(island => island.Section == section).Select(Of);
        Assert.Equal([nameof(BarKind.Start), nameof(BarKind.Search), nameof(BarKind.TaskView), Folder(Presets.DesktopsId)], In(BarSection.Start));
        Assert.Equal([nameof(BarKind.Apps)], In(BarSection.Middle));
        Assert.Equal(
            [
                Folder(Presets.CpuId), Folder(Presets.GpuId), Folder(Presets.MemoryId), Folder(Presets.DiskId), Folder(Presets.NetworkActivityId),
                Folder(Presets.ClipboardId), nameof(BarKind.Tray), nameof(BarKind.Indicators),
                Folder(Presets.QuickPanelId), Folder(Presets.NotificationCenterId), nameof(BarKind.ShowDesktop),
            ],
            In(BarSection.End));
        // A laptop's pins and its pen and touch buttons where Windows has them.
        var laptop = LayoutPresets.Make(LayoutPresets.Monitor, Laptop, "Monitor");
        Assert.Equal([nameof(BarKind.Pin), nameof(BarKind.Pin), nameof(BarKind.Apps)], laptop.Islands.Where(island => island.Section == BarSection.Middle).Select(Of));
        Assert.Contains(laptop.Islands, island => island.Kind == BarKind.PenMenu && island.Section == BarSection.End);
    }

    /// <summary>
    /// Windows' is Windows 11's taskbar as it comes but its Widgets button (§485: the person, "no"): nothing at the left;
    /// Start, the search box, Task view, the pins and the open apps centred, the "^" and its icons, the pen and touch buttons where Windows shows them, the status
    /// icons, quick settings' button, the clock, show desktop; no separator, no input indicator (Windows shows it only with
    /// two keyboard layouts), and no folder of Karpo's (§474); Follow Windows at 48 DIPs.
    /// </summary>
    [Fact]
    public void WindowsIsWindows11sTaskbar()
    {
        var layout = LayoutPresets.Make(LayoutPresets.Windows, Laptop, "Windows");
        Assert.Equal((LayoutType.Taskbar, 48, EdgeMode.Reserve), (layout.Type, layout.Size, layout.Show));
        IEnumerable<BarKind> In(BarSection section) => layout.Islands.Where(island => island.Section == section).Select(island => island.Kind);
        Assert.Empty(In(BarSection.Start));
        Assert.DoesNotContain(layout.Islands, island => island.Kind == BarKind.WidgetsBoard);
        Assert.Equal([BarKind.Start, BarKind.Search, BarKind.TaskView, BarKind.Pin, BarKind.Pin, BarKind.Apps], In(BarSection.Middle));
        Assert.Equal(
            [BarKind.Tray, BarKind.PenMenu, BarKind.TouchKeyboard, BarKind.Indicators, BarKind.WindowsQuickSettings, BarKind.WindowsNotifications, BarKind.ShowDesktop],
            In(BarSection.End));
        Assert.True(layout.Islands.Single(island => island.Kind == BarKind.Search).WindowsSearch!.ShowBox);
        Assert.DoesNotContain(layout.Islands, island => island.Folder is not null);
    }

    /// <summary>
    /// §511: the bar has no separator, and no layout a person starts from places one — each preset, a new config's
    /// first layout, the layout of every ready-made folder: written, the file says "separator" nowhere. Where one
    /// stood between two groups the bar's own gap between groups stands.
    /// </summary>
    [Fact]
    public void NoPresetPlacesASeparator()
    {
        List<TaskbarLayout> made = [.. LayoutPresets.All.Select(preset => LayoutPresets.Make(preset, Laptop, preset.Id)), Config.FirstLayout(), PresetLayout.Make(PresetsTests.Laptop)];

        foreach (var layout in made)
        {
            var json = new Config { Layouts = [layout], DefaultLayoutId = layout.Id }.ToJson();
            Assert.DoesNotContain("\"separator\"", json, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// §474: quick settings' button and the clock of Windows' are Windows' own widgets, which wear Windows' faces and open
    /// what Windows' taskbar opens from them — quick settings, the notifications and calendar (§485: no Widgets button) —,
    /// as the Widgets button, added from the list, opens the Widgets board, by §466's targets and panels (asked for, never pressed); Start, Search and Task view are Windows' own
    /// buttons. No other preset has one: theirs are Karpo's folders.
    /// </summary>
    [Fact]
    public void WindowsOpensWindowsOwnSurfaces()
    {
        var layout = LayoutPresets.Make(LayoutPresets.Windows, Laptop, "Windows");
        Assert.Equal([BarKind.WindowsQuickSettings, BarKind.WindowsNotifications], layout.Islands.Where(island => island.Kind.WearsWindowsFace()).Select(island => island.Kind));
        foreach (var kind in new[] { BarKind.WidgetsBoard, BarKind.WindowsQuickSettings, BarKind.WindowsNotifications })
        {
            var entry = BarEntries.Of(kind);
            Assert.Equal((BarClick.Panel, WidgetSource.WindowsFaces, (FolderView?)null), (entry.Click, entry.Drawing, entry.Opens));
            Assert.Equal(OpenBy.Shortcut, WindowsButtons.Target(kind, _ => true)?.By);
            Assert.NotNull(WindowsPanels.PanelOf(kind));
        }
        Assert.Equal(new ShellTarget(OpenBy.Shortcut, Key: 'W'), WindowsButtons.Target(BarKind.WidgetsBoard, _ => true));
        Assert.Equal(new ShellTarget(OpenBy.Shortcut, Key: 'A'), WindowsButtons.Target(BarKind.WindowsQuickSettings, _ => true));
        Assert.Equal(new ShellTarget(OpenBy.Shortcut, Key: 'N'), WindowsButtons.Target(BarKind.WindowsNotifications, _ => true));
        // Start, Search and Task view are Windows' buttons, which open Windows' own (no flyout of Karpo's).
        Assert.All(new[] { BarKind.Start, BarKind.Search, BarKind.TaskView }, kind => Assert.Contains(layout.Islands, island => island.Kind == kind));
        Assert.All(new[] { BarKind.Start, BarKind.Search, BarKind.TaskView }, kind => Assert.Equal(BarClick.Panel, BarEntries.Find(kind)!.Click));
        // Every other preset's network, clock and weather are Karpo's folders, which open themselves.
        Assert.All(LayoutPresets.All.Where(preset => preset != LayoutPresets.Windows), preset =>
            Assert.DoesNotContain(LayoutPresets.Make(preset, Laptop, "x").Islands, island => island.Kind.WearsWindowsFace()));
    }

    /// <summary>
    /// §474: what Windows' own widgets wear, fixed, as Windows' buttons show it: the Widgets button the weather's glyph and the
    /// temperature over the conditions, where this PC is; quick settings' button the network, the volume and — only on a PC
    /// with one — the battery; the clock the time over the date and the bell. Each a new set, so nothing shares it; nothing
    /// else wears Windows' face, and those three are no glyph button of the scene's.
    /// </summary>
    [Fact]
    public void WindowsOwnWidgetsWearWindowsFaces()
    {
        static IEnumerable<string?> Faces(BarKind kind, bool battery) => WindowsFaces.Of(kind, battery, percent: true)!.Faces.Select(face => face.Kind);
        Assert.Equal([FolderFaces.WeatherId], Faces(BarKind.WidgetsBoard, battery: true));
        var weather = WindowsFaces.Of(BarKind.WidgetsBoard, battery: true, percent: true)!.Face;
        Assert.Equal(WeatherFace.Conditions, WeatherFace.LookOf(weather));
        Assert.True(weather.Weather is { UseDeviceLocation: true });
        Assert.Equal([FolderFaces.WifiId, FolderFaces.SoundId, FolderFaces.BatteryId], Faces(BarKind.WindowsQuickSettings, battery: true));
        Assert.Equal([FolderFaces.WifiId, FolderFaces.SoundId], Faces(BarKind.WindowsQuickSettings, battery: false));
        Assert.Equal([FolderFaces.DateTimeId, FolderFaces.NotificationsId], Faces(BarKind.WindowsNotifications, battery: false));
        Assert.NotSame(WindowsFaces.Of(BarKind.WindowsNotifications, true, true), WindowsFaces.Of(BarKind.WindowsNotifications, true, true));
        foreach (var kind in Enum.GetValues<BarKind>())
        {
            Assert.Equal(kind.WearsWindowsFace(), WindowsFaces.Of(kind, battery: true, percent: true) is not null);
            Assert.Equal(kind.WearsWindowsFace(), BarEntries.Of(kind).Drawing == WidgetSource.WindowsFaces);
            if (kind.WearsWindowsFace())
            {
                Assert.True(kind.IsWindowsOwn() && kind.IsWindowsMenu() || kind == BarKind.WidgetsBoard, $"{kind}");
                Assert.False(kind.IsWindowsButton(), $"{kind} is drawn by the scene as a glyph");
            }
        }
    }

    /// <summary>
    /// §474 (§472's "Opens" gone): a folder opens itself. A folder whose face is the clock offers no more of its own than one
    /// of its icon — where it stands as it opens —, none of Windows' own widgets has a folder's options, and an "opens" an
    /// older file has is not kept.
    /// </summary>
    [Fact]
    public void AFolderOpensItselfAndNothingElse()
    {
        var plain = BarKind.FolderList.New();
        var clock = Presets.Folder(Presets.Find(Presets.NotificationCenterId)!, PresetsTests.Laptop);
        Assert.Equal(OptionRows.Of(plain, null, null).Select(row => row.Key), OptionRows.Of(clock, null, null).Select(row => row.Key));
        Assert.All(new[] { BarKind.WidgetsBoard, BarKind.WindowsQuickSettings, BarKind.WindowsNotifications }, kind => Assert.Null(kind.New().Folder));

        var config = Fresh();
        config.DefaultLayout.Islands.Add(clock);
        var text = config.ToJson();
        Assert.DoesNotContain("\"opens\"", text);
        var older = Config.Parse(text.Replace("\"face\": {", "\"opens\": \"windows\", \"face\": {", StringComparison.Ordinal));
        Assert.Contains("\"opens\"", text.Replace("\"face\": {", "\"opens\": \"windows\", \"face\": {", StringComparison.Ordinal));
        older.Repair();
        Assert.DoesNotContain("\"opens\"", older.ToJson());
        Assert.Equal(BarKind.FolderList, older.DefaultLayout.Islands.Single(island => island.Id == clock.Id).Kind);
    }
}
