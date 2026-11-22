// What a new config starts with (backlog 25 and 30): Windows' taskbar hidden and Karpo's bar
// never hidden — a new display's layout a Taskbar along the bottom, shown all the time and holding
// its strip — and the input indicator on every new bar where Windows 11 keeps it. A file that says
// otherwise keeps what it says. A first start gets Windows 11's taskbar (2026-10-13), and a config
// that has a bar keeps it as it is.

namespace Karpo.Tests.Core;

public sealed class DefaultsTests
{
    private static readonly InputHardware Desktop = new(Pen: false, Touch: false, Slate: false);
    private static readonly InputHardware PenTablet = new(Pen: true, Touch: true, Slate: true);

    // --- Backlog 25: the defaults ---------------------------------------------------------------

    [Fact]
    public void ANewConfigNeverHidesKarpos()
    {
        var config = new Config();
        config.Repair();
        config.AddDisplay("D1", "Dell");

        Assert.Equal([Edge.Bottom], config.EdgesOf("D1").Standing);
        Assert.Equal(EdgeMode.Reserve, config.EdgesOf("D1").Bottom.Mode);
    }

    [Fact]
    public void AFileThatSaysNothingGetsTheNewDefaults()
    {
        var config = Configs.Repaired("""{ "screens": [ { "id": "D1" } ] }""");

        Assert.Equal(EdgeMode.Reserve, config.EdgesOf("D1").Bottom.Mode);
        Assert.False(config.EdgesOf("D1").Top.Shown);
    }

    [Fact]
    public void AFileThatChoseKeepsItsChoice()
    {
        var config = Configs.Repaired("""{ "layouts": [ { "id": "L", "name": "Mine", "type": "taskbar", "edge": "bottom", "size": 48, "show": "hover", "islands": [] } ], "defaultLayoutId": "L", "screens": [ { "id": "D1", "name": "", "enabled": true, "activeLayoutId": "L" } ] }""", out var changed);

        Assert.False(changed);
        Assert.Equal(EdgeMode.Hover, config.EdgesOf("D1").Bottom.Mode);
        var again = Configs.RoundTrip(config, out var twice);
        Assert.False(twice);
        Assert.Equal(EdgeMode.Hover, again.EdgesOf("D1").Bottom.Mode);
    }

    [Fact]
    public void TheDefaultsAreWrittenDownSoTheyStay()
    {
        var config = new Config();
        config.AddDisplay("D1", "Dell");
        var json = config.ToJson();

        Assert.Contains("\"show\": \"reserve\"", json);
    }

    // --- Backlog 30: the input indicator --------------------------------------------------------

    /// <summary>
    /// Every new bar has the indicator, whatever the PC (2026-10-10: "why is there the keyboard
    /// selector not in the default layout?"). It used to follow Windows, whose indicator shows only
    /// with more than one keyboard layout installed, so a PC with one — the person's — got a bar
    /// without it. Karpo's always shows the layout in use (KeyboardIslandTests), and a new bar has
    /// everything of the vanilla taskbar.
    /// </summary>
    [Fact]
    public void EveryNewBarHasTheIndicator()
    {
        Assert.Single(Sections.DefaultLayout(pins: [], inputs: Desktop), island => island.Folder?.Face.Kind == FolderFaces.KeyboardId);
        var config = new Config();
        config.Repair();
        Assert.Single(config.DefaultLayout.Islands, island => island.Folder?.Face.Kind == FolderFaces.KeyboardId);
    }

    [Fact]
    public void ANewBarHasTheIndicatorLeftOfQuickSettings()
    {
        var bar = Sections.DefaultLayout(pins: [], inputs: Desktop);
        var kinds = Configs.Kinds(bar).ToList();

        // The keyboard preset, a folder wearing the indicator (2026-10-11), after the tray and the status icons
        // Windows shows beside it, and right before Windows' quick settings button.
        var keyboard = bar.FindIndex(island => island.Folder?.Face.Kind == FolderFaces.KeyboardId);
        Assert.Single(bar, island => island.Folder?.Face.Kind == FolderFaces.KeyboardId);
        Assert.Equal(kinds.IndexOf(BarKind.Tray) + 2, keyboard);
        Assert.Equal(keyboard - 1, kinds.IndexOf(BarKind.Indicators));
        Assert.Equal(FolderFaces.WifiId, bar[keyboard + 1].Folder?.Face.Kind);
    }

    [Fact]
    public void ThePenAndTouchButtonsStayBesideTheTray()
    {
        var kinds = Configs.Kinds(Sections.DefaultLayout(pins: [], inputs: PenTablet)).ToList();

        Assert.Equal(
            [BarKind.Tray, BarKind.PenMenu, BarKind.TouchKeyboard, BarKind.Indicators, BarKind.FolderList],
            kinds.Skip(kinds.IndexOf(BarKind.Tray)).Take(5));
    }

    // --- 2026-10-13: Windows 11's taskbar for a first start, and only for one ------------------

    /// <summary>What each of a bar's entries in <paramref name="section"/> is: its kind, or a folder's faces.</summary>
    private static string[] Shape(IEnumerable<BarItem> bar, BarSection section) =>
        [.. bar.Where(island => island.Section == section)
            .Select(island => island.Folder is { } folder ? string.Join("+", folder.Faces.Select(face => face.Kind)) : island.Kind.ToString())];

    /// <summary>
    /// A first start — no config at all — gets Windows 11's taskbar on its first display, but at
    /// the left (2026-10-14): Start, the search box, Task view and the open apps at the left;
    /// nothing in the middle; the tray, the status icons, the keyboard, quick settings (the network,
    /// the sound and the battery on one button), the clock button (the time over the date beside
    /// the bell) and show desktop at the right. No weather.
    /// </summary>
    [Fact]
    public void AFirstStartGetsWindowsTaskbar()
    {
        var config = new Config();
        config.Repair();
        config.AddDisplay("D1", "Dell");
        var bar = config.ActiveLayout("D1").Islands;

        Assert.Equal(["Start", "Search", "TaskView", "Apps"], Shape(bar, BarSection.Start));
        Assert.Empty(Shape(bar, BarSection.Middle));
        Assert.Equal(
            ["Tray", "Indicators", FolderFaces.KeyboardId, $"{FolderFaces.WifiId}+{FolderFaces.SoundId}+{FolderFaces.BatteryId}",
             $"{FolderFaces.DateTimeId}+{FolderFaces.NotificationsId}", "ShowDesktop"],
            Shape(bar, BarSection.End));
        // And what it writes reads back as it is.
        var again = Configs.RoundTrip(config, out var changed);
        Assert.False(changed);
        Assert.Equal(Shape(bar, BarSection.End), Shape(again.ActiveLayout("D1").Islands, BarSection.End));
    }

    /// <summary>
    /// A config somebody has already — the bar a new install got before 2026-10-13, the clock, the
    /// date and the bell three folders, Bluetooth beside the network — is read as it is: the new
    /// default changes nothing of a layout that exists, and nothing is written back.
    /// </summary>
    [Fact]
    public void AnExistingBarIsLeftAsItIs()
    {
        var machine = new PresetMachine(HasBattery: false, FixedDrives: []);
        List<BarItem> islands =
        [
            BarKind.Start.New(), BarKind.TaskView.New(), BarKind.Search.New(), BarKind.Apps.New(),
            Presets.Folder(Presets.Find(Presets.WifiId)!, machine), Presets.Folder(Presets.Find(Presets.BluetoothId)!, machine),
            Presets.Folder(Presets.Find(Presets.SoundId)!, machine),
            Presets.Folder(Presets.Find(Presets.ClockId)!, machine), Presets.Folder(Presets.Find(Presets.CalendarId)!, machine),
            Presets.Folder(Presets.Find(Presets.NotificationsId)!, machine), BarKind.ShowDesktop.New(),
        ];
        foreach (var island in islands)
        {
            island.Section = island.Kind is BarKind.Start or BarKind.TaskView or BarKind.Search or BarKind.Apps ? BarSection.Start : BarSection.End;
        }
        var theirs = new Config
        {
            Layouts = [new TaskbarLayout { Id = TaskbarLayouts.DefaultId, Name = "Default", Type = LayoutType.Taskbar, Show = EdgeMode.Reserve, Islands = islands }],
            DefaultLayoutId = TaskbarLayouts.DefaultId,
        };
        theirs.AddDisplay("D1", "Dell");
        var json = theirs.ToJson();

        var read = Configs.Repaired(json, out var changed);

        Assert.False(changed);
        Assert.Equal(json, read.ToJson());
        var bar = read.ActiveLayout("D1").Islands;
        Assert.Equal(
            [FolderFaces.WifiId, FolderFaces.BluetoothId, FolderFaces.SoundId, FolderFaces.ClockId, FolderFaces.CalendarId, FolderFaces.NotificationsId, "ShowDesktop"],
            Shape(bar, BarSection.End));
        Assert.DoesNotContain(bar, island => island.Folder?.Face.Kind == FolderFaces.DateTimeId);
        Assert.Single(read.Layouts!);
    }

    [Fact]
    public void ANewConfigsFirstDisplayHasItToo() =>
        Assert.Contains(Configs.Islands(new Config()), island => island.Folder?.Face.Kind == FolderFaces.KeyboardId);
}
