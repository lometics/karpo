// menus: one wording, one order and nothing that can't apply, over every menu at rest (§308; the
// person, 2026-10-23, from the review of every menu, artifacts\menus-sheet): a Windows page is
// "X settings", under one name wherever it is offered and never "in Windows"; a switch says what a
// pick does, "Turn X on" or "Turn X off"; a row that can do nothing now is left out, not greyed, and
// "Edit folder" stands only where nothing else does, last; a folder's heading is its name; the desktops'
// rows are in one order; the bar's layouts stand under their heading. Decisions, not drawings: the
// drawn menus are EveryMenuSheet's.

using Karpo.Dock;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Localization;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Menus;

public sealed class MenuWordingTests
{
    private static readonly PresetMachine Machine = new(HasBattery: true, FixedDrives: []);

    private static readonly FaceMenuParts Parts = new(
        TheDock.DesktopRows(new Switchers.DesktopActions(CanRename: true, null, CanDelete: true, null, CanMove: true, null)),
        TheDock.LayoutRows([new TaskbarLayout { Id = "work", Name = "Work" }, new TaskbarLayout { Id = "home", Name = "Home" }], "work"));

    /// <summary>Every menu at rest, as the dock builds it with nothing live: every bar entry with one, every premade folder and a plain one, every line.</summary>
    private static IEnumerable<(string What, List<FlyoutItem> Items, List<WidgetActRow> Acts)> RestMenus()
    {
        foreach (var kind in Enum.GetValues<BarKind>().Where(kind => kind is not (BarKind.Start or BarKind.Pin or BarKind.Indicators)))
        {
            var (items, acts) = TheDock.RestMenuRows(kind.New(), inFolder: false, widget: null, Parts);
            yield return ($"the {kind} entry", items, acts);
        }
        foreach (var preset in Presets.All)
        {
            var (items, acts) = TheDock.RestMenuRows(Presets.Folder(preset, Machine), inFolder: false, widget: null, Parts);
            yield return ($"the {preset.Title} folder", items, acts);
        }
        foreach (var kind in ListWidgets.All.Select(entry => entry.Kind))
        {
            var (items, acts) = TheDock.RestMenuRows(kind.New(), inFolder: true);
            yield return ($"the {kind} line", items, acts);
        }
    }

    /// <summary>Every status icon at once, each with its menu.</summary>
    private static IReadOnlyList<Indicator> EveryIcon() => Indicators.Of(new IndicatorState
    {
        Microphone = ["Teams"], Camera = ["Teams"], Presence = ["Teams"], AirplaneMode = true, Hotspot = true, Metered = true,
        EnergySaver = true, DoNotDisturb = true, Update = UpdateState.Due,
    });

    // A row a face before it already showed stands once (Dock.FaceMenus.cs): its act has no item.
    private static FlyoutItem? ItemOf(List<FlyoutItem> items, WidgetActRow act) => items.SingleOrDefault(item => item.Id == act.Id);

    // --- One wording ----------------------------------------------------------------------------------

    /// <summary>
    /// Every page Karpo offers in Windows' Settings is "X settings", Windows 11's name for it, each page
    /// once and under one name, never "in Windows"; every widget's page is one of them. In every
    /// language a page's name says "Windows" only where its English does (Windows Ink, Windows Update).
    /// </summary>
    [Fact]
    public void EveryWindowsPageIsItsNameAndSettings()
    {
        Assert.Equal(WindowsPages.All.Count, WindowsPages.All.Select(page => page.Uri).Distinct().Count());
        Assert.Equal(WindowsPages.All.Count, WindowsPages.All.Select(page => page.Title).Distinct().Count());
        Assert.All(WindowsPages.All, page =>
        {
            Assert.EndsWith(" settings", page.Title, StringComparison.Ordinal);
            Assert.DoesNotContain("in Windows", page.Title, StringComparison.OrdinalIgnoreCase);
        });
        Assert.All(WidgetInfos.All.Where(info => info.Settings is not null), info => Assert.Contains(info.Settings!.Value, WindowsPages.All));
        Assert.Equal(["Sound settings", "Power & battery settings", "Network & internet settings"], [WindowsPages.Sound.Title, WindowsPages.Power.Title, WindowsPages.Network.Title]);

        var english = StringFiles.Map("en");
        foreach (var language in StringFiles.Translations)
        {
            foreach (var (key, text) in StringFiles.Map(language).Where(pair => pair.Key.StartsWith("core.link.", StringComparison.Ordinal)))
            {
                Assert.True(!text.Contains("Windows", StringComparison.Ordinal) || english[key].Contains("Windows", StringComparison.Ordinal), $"{language}.json \"{key}\": \"{text}\"");
            }
        }
    }

    /// <summary>
    /// No row of any menu at rest says "in Windows", and every row opening a page of Windows' Settings
    /// reads as the page's name, "… settings": a widget's, a face's, a line's and a status icon's.
    /// </summary>
    [Fact]
    public void NoRowSaysInWindowsAndEveryPageRowSaysSettings()
    {
        foreach (var (what, items, acts) in RestMenus())
        {
            Assert.All(items, item => Assert.DoesNotContain("in Windows", item.Text, StringComparison.OrdinalIgnoreCase));
            foreach (var act in acts.Where(act => act.Act == WidgetAct.WindowsSettings || TheDock.PageOf(act.Act) is not null))
            {
                if (ItemOf(items, act) is { } item)
                {
                    Assert.True(item.Text.EndsWith(" settings", StringComparison.Ordinal), $"{what}: \"{item.Text}\"");
                }
            }
        }
        foreach (var row in EveryIcon().SelectMany(icon => icon.Menu))
        {
            Assert.DoesNotContain("in Windows", row.Text, StringComparison.OrdinalIgnoreCase);
            if (row.Act == IndicatorAct.Open && row.Uri != WindowsUpdate.RestartOptions)
            {
                Assert.EndsWith(" settings", row.Text, StringComparison.Ordinal);
            }
        }
        Assert.DoesNotContain(TheDock.BarRows(Parts.Layouts!), item => item.Text.Contains("in Windows", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// A page offered in two places reads the same in both: the sound face's and the volume line's
    /// "Sound settings", the battery face's and the battery line's "Power &amp; battery settings", the
    /// energy saver icon's and the energy saver switch's page.
    /// </summary>
    [Fact]
    public void ThePagesThatWereTwoAreOne()
    {
        Assert.Equal(TheDock.TextOf(WidgetAct.SoundSettings), TheDock.WindowsLinkOf(WidgetInfos.SoundVolume)!.Value.Title);
        Assert.Equal(TheDock.TextOf(WidgetAct.PowerSettings), TheDock.WindowsLinkOf(WidgetInfos.Battery)!.Value.Title);
        Assert.Equal(TheDock.TextOf(WidgetAct.NetworkSettings), TheDock.WindowsLinkOf(WidgetInfos.WifiSwitch)!.Value.Title);
        Assert.Equal(TheDock.TextOf(WidgetAct.HotspotSettings), TheDock.WindowsLinkOf(WidgetInfos.Hotspot)!.Value.Title);
        var icons = EveryIcon();
        Assert.Contains(icons.Single(icon => icon.Kind == IndicatorKind.EnergySaver).Menu, row => row.Text == WidgetInfos.EnergySaver.Settings!.Value.Title);
        Assert.Contains(icons.Single(icon => icon.Kind == IndicatorKind.DoNotDisturb).Menu, row => row.Text == TheDock.TextOf(WidgetAct.NotificationSettings));
        // An app opened says so; the volume mixer one way.
        Assert.Equal(["Open Task Manager", "Open Resource Monitor", "Open File Explorer", "Open volume mixer"],
            new[] { WidgetAct.TaskManager, WidgetAct.ResourceMonitor, WidgetAct.FileExplorer, WidgetAct.OpenVolumeMixer }.Select(TheDock.TextOf));
        Assert.Contains(WidgetAct.OpenVolumeMixer, TheDock.ActsFor(ListKind.SoundOutput));
    }

    /// <summary>Every switch a menu offers says what a pick does: "Turn X on", "Turn X off" — a face's, a line's, a status icon's.</summary>
    [Fact]
    public void EverySwitchSaysTurnOnOrOff()
    {
        foreach (var act in new[] { WidgetAct.WifiRadio, WidgetAct.BluetoothRadio, WidgetAct.EnergySaver, WidgetAct.DoNotDisturb })
        {
            Assert.Matches("^Turn .+ on$", TheDock.TurnText(act, on: true));
            Assert.Matches("^Turn .+ off$", TheDock.TurnText(act, on: false));
        }
        var offs = EveryIcon().SelectMany(icon => icon.Menu).Where(row => row.Act != IndicatorAct.Open).ToList();
        Assert.Equal(4, offs.Count);
        Assert.All(offs, row => Assert.Matches("^Turn .+ off$", row.Text));
        Assert.Contains(offs, row => row.Text == TheDock.TurnText(WidgetAct.EnergySaver, on: false));
        Assert.Contains(offs, row => row.Text == TheDock.TurnText(WidgetAct.DoNotDisturb, on: false));
    }

    // --- Only what applies ----------------------------------------------------------------------------

    /// <summary>
    /// With nothing live, no menu at rest has a greyed row — what can't apply is left out (the timer's
    /// and the stopwatch's buttons, what is playing's, the notifications' clear and switch, a site not
    /// named) — and none is empty. "Edit folder" stands only where nothing else does, and last; only a
    /// delete Windows refuses stays greyed, saying why.
    /// </summary>
    [Fact]
    public void AMenuAtRestShowsOnlyWhatWorksAndEditFolderOnlyAlone()
    {
        foreach (var (what, items, _) in RestMenus())
        {
            var rows = items.Where(item => !item.Header && !item.Separator).ToList();
            Assert.True(rows.Count > 0, $"{what} has no row");
            Assert.All(rows, row => Assert.True(row.Enabled || row.Detail is not null, $"{what}: \"{row.Text}\" is greyed"));
            if (rows.Exists(row => row.Text == S.DockWidgetMenuEditFolder))
            {
                Assert.True(rows.Count == 1, $"{what}: \"Edit folder\" beside {string.Join(", ", rows.Select(row => row.Text))}");
                Assert.Equal(S.DockWidgetMenuEditFolder, items[^1].Text);
            }
        }
        // The ones the review named: a timer, a stopwatch, what is playing with nothing to control, a
        // notifications face with nothing to clear, a sports face with no match.
        Assert.Equal([S.DockWidgetMenuEditFolder], Texts(TheDock.RestMenuRows(ListKind.Timer.New(), inFolder: true).Items));
        Assert.Equal([S.DockWidgetMenuEditFolder], Texts(TheDock.RestMenuRows(ListKind.Stopwatch.New(), inFolder: true).Items));
        Assert.Equal([S.DockWidgetMenuEditFolder], Texts(TheDock.RestMenuRows(ListKind.Media.New(), inFolder: true).Items));
        Assert.Equal([S.DockWidgetMenuOpenClock], Texts(TheDock.RestMenuRows(Presets.Folder(Presets.Find(Presets.TimerId)!, Machine), inFolder: false).Items));
        Assert.Equal([WindowsPages.Notifications.Title], Texts(TheDock.RestMenuRows(Presets.Folder(Presets.Find(Presets.NotificationsId)!, Machine), inFolder: false).Items));
    }

    /// <summary>
    /// What reads from the web opens where it comes from, at its place: the weather's and the air
    /// quality's forecast on weather.com, the tides on a map, the earthquakes on USGS's — a face's
    /// with no line to ask still has it, and a place followed from the device is where it was last
    /// found; with no place known there is no row.
    /// </summary>
    [Fact]
    public void AWebFaceOpensWhereItReadsFrom()
    {
        var zurich = new WeatherOptions { Place = "Zürich", Latitude = 47.3769, Longitude = 8.5417 };
        FolderFace Face(string id)
        {
            var face = FolderFace.Of(id);
            face.Weather = zurich;
            return face;
        }
        string? Opens(params string[] faces)
        {
            var folder = BarKind.FolderList.New();
            folder.Folder!.Faces = [.. faces.Select(Face)];
            var (items, acts) = TheDock.FaceRows(folder.Folder, island: null);
            var act = Assert.Single(acts);
            Assert.Equal(TheDock.TextOf(act.Act), Assert.Single(items).Text);
            return act.On;
        }
        Assert.Equal("https://weather.com/weather/today/l/47.3769,8.5417", Opens(FolderFaces.WeatherId));
        Assert.Equal("https://weather.com/forecast/air-quality/l/47.3769,8.5417", Opens(FolderFaces.AirQualityId));
        Assert.Equal("https://www.openstreetmap.org/?mlat=47.3769&mlon=8.5417#map=10/47.3769/8.5417", Opens(FolderFaces.TidesId));
        Assert.Equal(TheDock.QuakeMap, Opens(FolderFaces.QuakesId));
        // Two faces whose rows read alike: the first one's, once, as any row a face before showed.
        var both = BarKind.FolderList.New();
        both.Folder!.Faces = [Face(FolderFaces.WeatherId), Face(FolderFaces.TidesId)];
        var (two, twoActs) = TheDock.FaceRows(both.Folder, island: null);
        Assert.Equal(["Open forecast", "—", "Open on map"], Texts(two));
        var opened = new List<string>();
        TheDock.RunAct(twoActs[0], widget: null, (target, _) => opened.Add(target));
        Assert.Equal([twoActs[0].On!], opened);

        Assert.Null(TheDock.ForecastPage(WeatherOptions.New(), air: false, device: null));
        Assert.Null(TheDock.MapPage(new WeatherOptions(), device: (1, 2)));
        Assert.Equal("https://weather.com/weather/today/l/-33.8688,151.2093", TheDock.ForecastPage(WeatherOptions.New(), air: false, device: (-33.8688, 151.2093)));
    }

    // --- Headings and order ---------------------------------------------------------------------------

    /// <summary>
    /// A folder's heading is "Folder" (never "Folder · Folder"), a premade one's and one wearing other faces
    /// alike: a folder has no name (§427); a pin's names its file without the extension, a web pin's its
    /// address as it is.
    /// </summary>
    [Fact]
    public void AHeadingNamesWhatItIs()
    {
        var clock = Presets.Find(Presets.ClockId)!;
        Assert.Equal(S.CoreWidgetFolderListTitle, IslandEdits.Header(Presets.Folder(clock, Machine)));
        var faced = BarKind.FolderList.New();
        faced.Folder!.Faces = [FolderFace.Of(FolderFaces.IconId), ClockFace.New(), WeatherFace.New()];
        Assert.Equal(S.CoreWidgetFolderListTitle, IslandEdits.Header(faced));
        Assert.Equal(S.CoreWidgetFolderListTitle, IslandEdits.Header(BarKind.FolderList.New()));

        var file = BarKind.Pin.New();
        file.Pin!.Target = @"C:\Users\Ada\Documents\budget.xlsx";
        Assert.Equal($"{WidgetInfos.Pin.Title} · budget", IslandEdits.Header(file));
        Assert.Equal("notepad", IslandEdits.WithoutExtension("notepad.exe", @"C:\Windows\notepad.exe"));
        Assert.Equal("Notepad", IslandEdits.WithoutExtension("Notepad", @"C:\Windows\notepad.exe"));
        Assert.Equal("example.com", IslandEdits.WithoutExtension("example.com", "https://example.com"));
    }

    /// <summary>§366 ("the title for the different layouts should also not be shown"): the bar's menu has its layouts ruled off under no heading; a lone layout none.</summary>
    [Fact]
    public void TheBarsLayoutsStandUnderNoHeading()
    {
        var items = TheDock.BarRows(Parts.Layouts!);
        var work = items.FindIndex(item => item.Text == "Work");
        // §581: the menu's first rows, ruled off from Windows' Task Manager and Taskbar settings under them.
        Assert.Equal(0, work);
        Assert.Equal("Home", items[work + 1].Text);
        Assert.True(items[items.FindIndex(item => item.Text == S.CoreBarMenuTaskManager) - 1].Separator);
        Assert.DoesNotContain(items, item => item.Header);
        Assert.DoesNotContain(TheDock.BarRows(TheDock.LayoutRows([new TaskbarLayout { Id = "work", Name = "Work" }], "work")), item => item.Header);
    }

    /// <summary>Every desktop menu has one order: New desktop, Task view, Rename, Delete — the desktops face's, the desktops line's, Task view's.</summary>
    [Fact]
    public void TheDesktopsRowsAreInOneOrder()
    {
        Assert.Equal([S.DockMenuNewDesktop, S.DockMenuTaskView, S.DockMenuRenameDesktop, S.DockMenuDeleteDesktop], Texts(Parts.Desktops!));
        Assert.Equal([WidgetAct.NewDesktop, WidgetAct.TaskView], TheDock.ActsFor(ListKind.Desktops));
        Assert.Equal([WidgetAct.NewDesktop, WidgetAct.TaskView], TheDock.ActsFor(ListKind.TaskView));
        Assert.Equal(WidgetAct.NewDesktop, TheDock.ActsFor(BarKind.TaskView)[0]);
    }

    private static string[] Texts(IEnumerable<FlyoutItem> rows) =>
        [.. rows.Select(row => row.Separator ? "—" : row.Header ? "# " + row.Text : row.Text)];
}
