// The bar a new install or a reset gets is Windows 11's own taskbar as 24H2 and 25H2 show it out
// of the box (2026-10-13: "for the default layout when a user first starts karpo, show the date and
// time over each other like windows does it and also group the notifications functionality with the
// bell there as well ... so that it matches the windows 11 default behaviour / layout as close as
// possible"; before it 2026-10-05 and 2026-10-10: "everything of the vanilla taskbar"), but at the
// left whatever Windows' alignment, the middle empty, and without the Widgets button or the weather
// in its place (2026-10-14: "the start should be on the left together with the app pins ... center
// should be empty by default", "dont actually show the widgets button"): its sections and their
// order button by button, every other button whatever Windows' own settings switched off, the
// input indicator with one keyboard layout as with several, no battery on a PC without one, and Windows' pins — or the ones
// Windows gives a new taskbar where its own can't be read (Sections.DefaultLayout, WindowsTaskbar).
// Windows' quick settings button is one button (the quick settings preset), its clock button one
// with the bell (the notification center preset). The person's own arrangement of 2026-10-11 is the
// presets' still (PersonsPresetsTests); an existing config keeps its own bar (DefaultsTests).

using static Karpo.Core.BarKind;

namespace Karpo.Tests.Core;

public sealed class DefaultBarTests
{
    private static readonly InputHardware Desktop = new(Pen: false, Touch: false, Slate: false);

    private static List<BarItem> Bar(WindowsTaskbar windows, params string[] pins) =>
        Sections.DefaultLayout(pins, Desktop, windows);

    private static BarItem[] Items(List<BarItem> bar, BarSection section) =>
        [.. bar.Where(island => island.Section == section)];

    private static BarKind[] In(List<BarItem> bar, BarSection section) =>
        [.. Items(bar, section).Select(island => island.Kind)];

    /// <summary>What each of a section's entries is: its kind, or a folder's first face.</summary>
    private static string[] Shape(List<BarItem> bar, BarSection section) =>
        [.. Items(bar, section).Select(island => island.Folder is { } folder ? folder.Face.Kind : island.Kind.ToString())];

    /// <summary>The weather preset: a folder wearing the weather face.</summary>
    private static bool IsWeather(BarItem island) => island.Folder?.Face.Kind == FolderFaces.WeatherId;

    /// <summary>Windows' clock button: the notification center preset, wearing the time over the date.</summary>
    private static BarItem TheClock(List<BarItem> bar) =>
        Assert.Single(bar, island => island.Folder?.Face.Kind == FolderFaces.DateTimeId);

    // --- The sections and their order --------------------------------------------------------

    /// <summary>
    /// Windows 11 out of the box but at the left: Start, the search box, Task view, the pins and
    /// the open apps at the left, nothing between them; nothing in the middle; the "^" and the
    /// tray, the status icons, the input indicator, the quick settings button, the clock button and
    /// show desktop at the right (§511: no separator after the tray nor before the clock, where the
    /// person had put theirs, 2026-10-16). No Widgets button, no weather.
    /// </summary>
    [Fact]
    public void ANewBarIsWindowsOutOfTheBoxButtonByButton()
    {
        var bar = Bar(WindowsTaskbar.OutOfTheBox, @"C:\Windows\explorer.exe", @"C:\Links\Microsoft Edge.lnk");

        Assert.Equal([Start, Search, TaskView, Pin, Pin, Apps], In(bar, BarSection.Start));
        Assert.Empty(In(bar, BarSection.Middle));
        Assert.Equal(
            [nameof(Tray), nameof(Indicators), FolderFaces.KeyboardId, FolderFaces.WifiId, FolderFaces.DateTimeId, nameof(ShowDesktop)],
            Shape(bar, BarSection.End));
        Assert.Equal(
            [@"C:\Windows\explorer.exe", @"C:\Links\Microsoft Edge.lnk"],
            bar.Where(island => island.Kind == Pin).Select(island => island.Pin!.Target));
        Assert.DoesNotContain(bar, island => island.Kind is WidgetsBoard);
        Assert.DoesNotContain(bar, IsWeather);
        // The list is in its sections' order already, and every entry is there once.
        Assert.False(Sections.Order([.. bar]));
        Assert.Equal(bar.Count, bar.Select(island => island.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    /// <summary>
    /// Everything of the vanilla taskbar, whatever the person switched off in Windows' own taskbar
    /// settings (2026-10-10): the search box, Task view and show desktop are on every new bar, and
    /// the input indicator with them, which Windows shows only with more than one keyboard layout
    /// but Karpo's always shows (KeyboardIslandTests). Only the battery follows the PC. No button of
    /// its own for Bluetooth, the sound or the date, which Windows' taskbar has none of: they are
    /// the quick settings' and the clock's. The Widgets button alone is left out (2026-10-14).
    /// </summary>
    [Fact]
    public void EveryButtonOfTheVanillaTaskbarIsThere()
    {
        foreach (var windows in new[] { WindowsTaskbar.OutOfTheBox, WindowsTaskbar.OutOfTheBox with { Battery = false, Seconds = true } })
        {
            var bar = Bar(windows);

            Assert.DoesNotContain(bar, IsWeather);
            Assert.True(Assert.Single(bar, island => island.Kind == Search).WindowsSearch?.ShowBox);
            foreach (var kind in new[] { Start, TaskView, Apps, Tray, Indicators, ShowDesktop })
            {
                Assert.Single(bar, island => island.Kind == kind);
            }
            Assert.Single(bar, island => island.Folder?.Face.Kind == FolderFaces.KeyboardId);
            Assert.Single(bar, island => island.Folder?.Face.Kind == FolderFaces.WifiId);
            Assert.Single(bar, island => island.Folder?.Face.Kind == FolderFaces.DateTimeId);
            Assert.Equal(FolderFaces.NotificationsId, Assert.Single(TheClock(bar).Folder!.Faces.Skip(1)).Kind);
            foreach (var gone in new[] { FolderFaces.ClockId, FolderFaces.CalendarId, FolderFaces.BluetoothId, FolderFaces.SoundId, FolderFaces.BatteryId, FolderFaces.NotificationsId })
            {
                Assert.DoesNotContain(bar, island => island.Folder?.Face.Kind == gone);
            }
            Assert.Equal(ShowDesktop, bar[^1].Kind);
            // Copilot is no button of Windows' taskbar (Karpo has no entry for it), Chat neither.
            Assert.DoesNotContain(bar, island => island.Kind is WidgetsBoard);
        }
    }

    /// <summary>Windows' search box, the pill, as Windows 11 shows it out of the box, right after Start.</summary>
    [Fact]
    public void ANewBarHasTheSearchBoxAfterStart()
    {
        var bar = Bar(WindowsTaskbar.OutOfTheBox);
        var search = Assert.Single(bar, island => island.Kind == Search);

        Assert.True(search.WindowsSearch?.ShowBox);
        Assert.Equal(BarSection.Start, search.Section);
        Assert.Equal(Start, bar[bar.IndexOf(search) - 1].Kind);
        Assert.Equal(TaskView, bar[bar.IndexOf(search) + 1].Kind);
    }

    /// <summary>
    /// Windows' quick settings button: the quick settings preset, the network, the speaker and, on a
    /// PC with one, the battery side by side on one button, quick settings inside.
    /// </summary>
    [Fact]
    public void TheNetworkSoundAndBatteryAreOneButton()
    {
        foreach (var battery in new[] { true, false })
        {
            var bar = Bar(WindowsTaskbar.OutOfTheBox with { Battery = battery });
            var quick = Assert.Single(bar, island => island.Folder?.Face.Kind == FolderFaces.WifiId);
            var made = Presets.Folder(Presets.Find(Presets.QuickPanelId)!, new PresetMachine(battery, []));

            Assert.Equal(battery ? [FolderFaces.WifiId, FolderFaces.SoundId, FolderFaces.BatteryId] : [FolderFaces.WifiId, FolderFaces.SoundId], quick.Folder.Faces.Select(face => face.Kind));
            Assert.Equal(made.Folder!.Cells.Select(cell => cell.Island.Kind), quick.Folder.Cells.Select(cell => cell.Island.Kind));
            Assert.Equal(FolderFaces.KeyboardId, bar[bar.IndexOf(quick) - 1].Folder?.Face.Kind);
            Assert.Equal(FolderFaces.DateTimeId, bar[bar.IndexOf(quick) + 1].Folder?.Face.Kind);
        }
    }

    /// <summary>
    /// Windows' clock button, the bell grouped with it (2026-10-13): the notification center preset,
    /// the time over the date with the bell and its count to their right, the notifications over the
    /// calendar inside, right before show desktop. The seconds follow Windows' own clock.
    /// </summary>
    [Fact]
    public void TheTimeTheDateAndTheBellAreOneButton()
    {
        var bar = Bar(WindowsTaskbar.OutOfTheBox);
        var clock = TheClock(bar);
        var preset = Presets.Find(Presets.NotificationCenterId)!;
        var made = Presets.Folder(preset, new PresetMachine(false, []));

        Assert.Equal([FolderFaces.DateTimeId, FolderFaces.NotificationsId], clock.Folder.Faces.Select(face => face.Kind));
        Assert.True(NotificationsFace.ShowsCount(clock.Folder.Faces[1]));
        Assert.Equal(DateTimeFace.Windows, clock.Folder.Face.Text(FolderFaces.DateTime.Option(DateTimeFace.Seconds)!));
        Assert.Equal(made.Folder!.Cells.Select(cell => cell.Island.Kind), clock.Folder.Cells.Select(cell => cell.Island.Kind));
        Assert.Equal(BarSection.End, clock.Section);
        Assert.Equal(ShowDesktop, bar[bar.IndexOf(clock) + 1].Kind);
    }

    /// <summary>A PC without a battery: the quick settings button without the battery's face, and no battery anywhere.</summary>
    [Fact]
    public void NoBatteryWithoutOne()
    {
        var bar = Bar(WindowsTaskbar.OutOfTheBox with { Battery = false });

        Assert.DoesNotContain(bar, island => island.Folder?.Faces.Any(face => face.Kind == FolderFaces.BatteryId) == true);
        Assert.DoesNotContain(Karpo.Core.Folders.All(bar), entry => entry.Island is CellItem { Kind: "battery" });
        Assert.Equal(
            [nameof(Tray), nameof(Indicators), FolderFaces.KeyboardId, FolderFaces.WifiId, FolderFaces.DateTimeId, nameof(ShowDesktop)],
            Shape(bar, BarSection.End));
    }

    /// <summary>
    /// The dividers inside a new bar's folders are where the person put theirs (2026-10-16), on a PC
    /// like theirs, without a battery: one between the notifications and the calendar's header, none
    /// in the input indicator's or in quick settings, whose foot stands right under the volume.
    /// </summary>
    [Fact]
    public void TheFoldersDividersAreThePersons()
    {
        var bar = Bar(WindowsTaskbar.OutOfTheBox with { Battery = false });
        string[] Lines(string face) =>
            [.. Assert.Single(bar, island => island.Folder?.Face.Kind == face).Folder!.Cells.Select(cell => cell.Island.Kind)];

        Assert.Equal(["notificationsList", "divider", "dateHeader", "month", "focus"], Lines(FolderFaces.DateTimeId));
        Assert.Equal(["keyboard", "actions"], Lines(FolderFaces.KeyboardId));
        // What is playing over the tiles, a divider between (2026-10-16).
        Assert.Equal(["media", "divider", "wifiSwitch", "bluetoothSwitch", "accessibility", "liveCaptions", "cast", "project", "toggles", "brightness", "soundVolume", "actions"], Lines(FolderFaces.WifiId));
    }

    /// <summary>
    /// A new bar's quick settings and notifications &amp; calendar are the person's line for line,
    /// heights too (2026-10-16, for new installs: the separate switch lines and the notifications at
    /// their rows; §196): what is playing automatic, the kit's row (§401), each switch a line of one row without its label,
    /// the toggles' row two, the brightness and the volume one; the notifications Fill from five
    /// rows (ten cells), the date one, the month eight (sixteen), the focus band one.
    /// </summary>
    [Fact]
    public void TheFoldersLinesAreAsTallAsThePersons()
    {
        var bar = Bar(WindowsTaskbar.OutOfTheBox with { Battery = false });
        (string, string?, bool?)[] Lines(string face) =>
        [
            .. Assert.Single(bar, island => island.Folder?.Face.Kind == face).Folder!.Cells
                .Select(cell => (cell.Island.Kind, cell.Island.Height is { } height ? $"{height} {height.Cells}" : cell.Island.Size is { } size ? $"size {size}" : null, cell.Island.Switch?.ShowLabel)),
        ];

        Assert.Equal(
        [
            // §432: the sized lines' sizes; the toggles row is its one form.
            ("media", "size Medium", null), ("divider", null, null),
            ("wifiSwitch", "size Compact", false), ("bluetoothSwitch", "size Compact", false), ("accessibility", "size Compact", false),
            ("liveCaptions", "size Compact", false), ("cast", "size Compact", false), ("project", "size Compact", false),
            ("toggles", null, null), ("brightness", "size Compact", null), ("soundVolume", "size Compact", null), ("actions", null, null),
        ], Lines(FolderFaces.WifiId));
        Assert.Equal(
        [
            ("notificationsList", "fill 10", null), ("divider", null, null),
            ("dateHeader", null, null), ("month", "fixed 16 16", null), ("focus", null, null),
        ], Lines(FolderFaces.DateTimeId));
        Assert.False(Assert.Single(bar, island => island.Folder?.Face.Kind == FolderFaces.DateTimeId).Folder!.Cells[0].Island.Height!.Value.Fixed);
    }

    /// <summary>
    /// Rotation lock is in a new bar's quick settings only where the screen turns, as Windows offers
    /// its tile (2026-10-16): the toggles row has it between the VPN and energy saver, else not.
    /// </summary>
    [Fact]
    public void RotationLockOnlyWhereTheScreenTurns()
    {
        foreach (var rotates in new[] { true, false })
        {
            var bar = Bar(WindowsTaskbar.OutOfTheBox with { Rotates = rotates });
            var quick = Assert.Single(bar, island => island.Folder?.Face.Kind == FolderFaces.WifiId);
            var row = quick.Folder!.Cells.Single(cell => cell.Island.Kind == "toggles").Island.Toggles!.Shown;
            Assert.Equal(
                rotates
                    ? [Toggle.AirplaneMode, Toggle.Vpn, Toggle.RotationLock, Toggle.EnergySaver, Toggle.NightLight, Toggle.MobileHotspot, Toggle.NearbyShare]
                    : [Toggle.AirplaneMode, Toggle.Vpn, Toggle.EnergySaver, Toggle.NightLight, Toggle.MobileHotspot, Toggle.NearbyShare],
                row);
        }
    }

    /// <summary>A PC with a pen and a touch screen: their buttons right after the tray, where Windows shows them.</summary>
    [Fact]
    public void ThePenAndTouchButtonsStayBesideTheTray()
    {
        var tablet = Sections.DefaultLayout([], new InputHardware(Pen: true, Touch: true, Slate: true), WindowsTaskbar.OutOfTheBox);

        Assert.Equal(
            [nameof(Tray), nameof(PenMenu), nameof(TouchKeyboard), nameof(Indicators), FolderFaces.KeyboardId, FolderFaces.WifiId, FolderFaces.DateTimeId, nameof(ShowDesktop)],
            Shape(tablet, BarSection.End));
    }

    // --- Reading Windows' values -------------------------------------------------------------

    private static Func<string, int?> Advanced(params (string Name, int Value)[] values) =>
        name => values.Where(value => value.Name == name).Select(value => (int?)value.Value).FirstOrDefault();

    /// <summary>A taskbar nobody changed has none of the values: Windows' defaults, which are OutOfTheBox.</summary>
    [Fact]
    public void NoValuesAreWindowsDefaults() =>
        Assert.Equal(WindowsTaskbar.OutOfTheBox, WindowsTaskbar.From(Advanced(), battery: true));

    /// <summary>
    /// The seconds and the battery are read; the buttons Windows' settings switch off (the Widgets
    /// button, Task view, the far corner) and the alignment change nothing of a new bar.
    /// </summary>
    [Fact]
    public void EachValueIsRead()
    {
        var off = WindowsTaskbar.From(
            Advanced(("TaskbarDa", 0), ("ShowTaskViewButton", 0), ("TaskbarSd", 0), ("ShowSecondsInSystemClock", 1), ("TaskbarAl", 0)),
            battery: false);

        Assert.Equal(new WindowsTaskbar { Seconds = true, Battery = false }, off);
        var on = WindowsTaskbar.From(
            Advanced(("TaskbarDa", 1), ("ShowTaskViewButton", 1), ("TaskbarSd", 1), ("ShowSecondsInSystemClock", 0), ("TaskbarAl", 1)),
            battery: true);
        Assert.Equal(WindowsTaskbar.OutOfTheBox, on);
        // Left-aligned or centred, a new bar is the same.
        Assert.Equal(WindowsTaskbar.OutOfTheBox, WindowsTaskbar.From(Advanced(("TaskbarAl", 0)), battery: true));
    }

    /// <summary>Tests never read the person's taskbar: an isolated config is given Windows' out-of-the-box one.</summary>
    [Fact]
    public void AnIsolatedConfigReadsNothingOfThisPc() =>
        Assert.Equal(WindowsTaskbar.OutOfTheBox, WindowsTaskbar.Read());

    // --- The pins ----------------------------------------------------------------------------

    /// <summary>Windows' Favorites value is the taskbar's pins, in its order; with none left in it, a new bar pins none.</summary>
    [Fact]
    public void WindowsOwnPinsComeFirst()
    {
        Assert.Equal(["a", "b"], WindowsTaskbarPins.ForNewBar(["a", "b"], ["z"]));
        // An entry the shell gives no name is left out, as before.
        Assert.Equal(["a"], WindowsTaskbarPins.ForNewBar(["a", null], ["z"]));
        // Everything unpinned: Windows' taskbar shows no pin, and neither does a new bar.
        var none = WindowsTaskbarPins.ForNewBar([], ["z"]);
        Assert.NotNull(none);
        Assert.Empty(none);
    }

    /// <summary>Without a Favorites value that names anything, the folder's shortcuts; with none of those either, nothing is known.</summary>
    [Fact]
    public void TheFolderStandsInAndThenNothingIsKnown()
    {
        Assert.Equal(["z"], WindowsTaskbarPins.ForNewBar(null, ["z"]));
        Assert.Equal(["z"], WindowsTaskbarPins.ForNewBar([null, null], ["z"]));
        Assert.Null(WindowsTaskbarPins.ForNewBar(null, []));
        Assert.Null(WindowsTaskbarPins.ForNewBar(null, null));
        Assert.Null(WindowsTaskbarPins.ForNewBar([null], null));
    }

    private sealed class FakePc : ITargetSystem
    {
        public readonly HashSet<string> Files = new(StringComparer.OrdinalIgnoreCase)
        {
            @"C:\Windows\explorer.exe",
            @"C:\ProgramData\Microsoft\Windows\Start Menu\Programs\Microsoft Edge.lnk",
        };

        public readonly List<string> Packages =
        [
            "Microsoft.WindowsStore_22509.1401.5.0_x64__8wekyb3d8bbwe",
            "Microsoft.WindowsCalculator_11.2607.0.0_x64__8wekyb3d8bbwe",
        ];

        public bool FileExists(string path) => Files.Contains(path);

        public bool DirectoryExists(string path) => path is @"C:\" or @"C:\Windows";

        public DriveType? Drive(string root) => root == @"C:\" ? DriveType.Fixed : null;

        public byte[]? ReadShortcut(string path) => null;

        public IReadOnlyList<string> UserSubKeys(string path) => path == PinTargets.PackagesKey ? Packages : [];

        public string Expand(string text) => text;

        public string Folder(Environment.SpecialFolder folder) => folder switch
        {
            Environment.SpecialFolder.Windows => @"C:\Windows",
            Environment.SpecialFolder.CommonPrograms => @"C:\ProgramData\Microsoft\Windows\Start Menu\Programs",
            _ => "",
        };
    }

    /// <summary>Where Windows' own pins can't be read: the ones Windows gives a new taskbar, in its order — File Explorer, Edge, the Store.</summary>
    [Fact]
    public void WindowsDefaultPinsInWindowsOrder() =>
        Assert.Equal(
            [@"C:\Windows\explorer.exe", @"C:\ProgramData\Microsoft\Windows\Start Menu\Programs\Microsoft Edge.lnk", WindowsTaskbar.StoreApp],
            WindowsTaskbar.DefaultPins(new FakePc()));

    /// <summary>Whichever of them this PC has: no Edge where its shortcut is gone, no Store where it isn't installed.</summary>
    [Fact]
    public void OnlyTheDefaultPinsThisPcHas()
    {
        var pc = new FakePc();
        pc.Files.Remove(@"C:\ProgramData\Microsoft\Windows\Start Menu\Programs\Microsoft Edge.lnk");
        pc.Packages.RemoveAt(0);

        Assert.Equal([@"C:\Windows\explorer.exe"], WindowsTaskbar.DefaultPins(pc));
    }
}
